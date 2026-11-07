using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace CecMod.Patches
{
    /// <summary>
    /// Bottom-bar UI indicator showing the active CEC guider's name when a missile's guidance
    /// or terminal illumination is handled by a consort ship or AWACS (<c>guider != _launchPlatform</c>).
    /// <para>
    /// Works seamlessly in both standalone Sea Power 0.8.7 and alongside <c>ExtendedGuidance.dll</c>
    /// (which skips <c>GoodConnection</c> and writes custom telemetry directly to
    /// <see cref="ObjectMissileControlViewModel.ObjectState"/> every 0.25s).
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class DatalinkUiPatch
    {
        private static readonly ConditionalWeakTable<ObjectMissileControlViewModel, Missile> _viewMissiles =
            new ConditionalWeakTable<ObjectMissileControlViewModel, Missile>();

        /// <summary>
        /// Captures the <see cref="ObjectMissileControlViewModel"/>-to-<see cref="Missile"/> mapping
        /// before <c>ExtendedGuidance.Hooks.MissileRow</c> (<c>Priority.High = 800</c>) skips vanilla <c>GetState</c>.
        /// </summary>
        [HarmonyPatch(typeof(ObjectMissileControlViewModel), "GetState")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void GetState_Prefix(ObjectMissileControlViewModel __instance, Missile missile)
        {
            if (__instance == null || missile == null)
                return;

            _viewMissiles.Remove(__instance);
            _viewMissiles.Add(__instance, missile);
        }

        [HarmonyPatch(typeof(ObjectMissileControlViewModel), nameof(ObjectMissileControlViewModel.ObjectState), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SetObjectState_Prefix(ObjectMissileControlViewModel __instance, ref string value)
        {
            try
            {
                if (__instance == null || string.IsNullOrEmpty(value))
                    return true;

                if (!_viewMissiles.TryGetValue(__instance, out Missile m) || m == null)
                    return true;

                if (m._seeker != null && m._seeker._guidanceType == AmmunitionParameters.GuidanceType.LaserHoming)
                    return true;

                SensorSystemRadar activeRadar = EgBridge.GetActiveEgIlluminator(m) ?? m._guidingRadarSystem;
                ObjectBase guider = activeRadar?._baseObject;
                if (guider == null || guider.IsDestroyed)
                    return true;

                ObjectBase launcher = m._launchPlatform;
                if (guider == launcher)
                    return true;

                string guiderName = guider.getName();
                if (string.IsNullOrEmpty(guiderName))
                    return true;

                string safeGuiderName = EscapeInlineText(guiderName);

                string launcherGetName = launcher != null ? launcher.getName() : null;
                string launcherRawName = launcher?.Name != null ? launcher.Name.Value : null;

                bool replacedVanillaToken = false;
                if (!string.IsNullOrEmpty(launcherGetName) && value.Contains(launcherGetName))
                {
                    value = value.Replace(launcherGetName, safeGuiderName);
                    replacedVanillaToken = true;
                }
                else if (!string.IsNullOrEmpty(launcherRawName) && value.Contains(launcherRawName))
                {
                    value = value.Replace(launcherRawName, safeGuiderName);
                    replacedVanillaToken = true;
                }
                else if (value.Contains("Nibiru"))
                {
                    value = value.Replace("Nibiru", safeGuiderName);
                    replacedVanillaToken = true;
                }
                else if (value.Contains("Other side"))
                {
                    value = value.Replace("Other side", safeGuiderName);
                    replacedVanillaToken = true;
                }

                if (!replacedVanillaToken)
                {
                    // ExtendedGuidance / EnhancedUI format (no parent unit name token present):
                    // insert "<LineBreak/>{guiderName}" right before the trailing "<LineBreak/>Target: ...".
                    string guiderLine = "<LineBreak/>" + safeGuiderName;
                    if (value.IndexOf(guiderLine, StringComparison.Ordinal) < 0)
                    {
                        int lastBreak = value.LastIndexOf("<LineBreak/>", StringComparison.Ordinal);
                        value = lastBreak >= 0
                            ? value.Insert(lastBreak, guiderLine)
                            : value + guiderLine;
                    }
                }

                // Skip redundant PropertyChanged notifications when ExtendedGuidance's 0.25s refresh
                // writes identical telemetry that maps to the exact same ObjectState string.
                if (string.Equals(__instance.ObjectState, value, StringComparison.Ordinal))
                    return false;
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] DatalinkUiPatch exception: {ex}");
            }

            return true;
        }

        private static string EscapeInlineText(string text)
        {
            return (text ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
