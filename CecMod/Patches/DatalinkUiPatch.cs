using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace CecMod.Patches
{
    /// <summary>
    /// Bottom-bar UI indicator showing the active CEC guider's name and protocol/tier telemetry
    /// on the HUD missile control panel (<see cref="ObjectMissileControlViewModel.ObjectState"/>).
    /// <para>
    /// Formats cleanly both with Extended Guidance active (<c>"From 1004 (CG-67 Shiloh) / 1.7 s"</c>,
    /// <c>"Illum: 1004 (CG-67 Shiloh)"</c>) and in native Sea Power (<c>"Link to [1005] E-2D VAW-113 [NIFC-CA / USG-3 | Tier 1]"</c>).
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

                _viewMissiles.TryGetValue(__instance, out Missile m);

                // 1. Resolve ExtendedGuidance's numeric "From <ContactID>" token to include both ID and platform name
                // Example: "From 1004 / 1.7 s" -> "From 1004 (CG-67 Shiloh) / 1.7 s"
                int fromIdx = value.IndexOf("From ", StringComparison.Ordinal);
                if (fromIdx >= 0)
                {
                    int start = fromIdx + 5;
                    int end = start;
                    while (end < value.Length && char.IsDigit(value[end])) end++;
                    if (end > start && int.TryParse(value.Substring(start, end - start), out int contactId))
                    {
                        ObjectBase guiderPlatform = FindObjectByContactId(contactId);
                        if (guiderPlatform == null && m != null)
                        {
                            if (m._launchPlatform != null && (GetUnitContactId(m._launchPlatform) == contactId || m._launchPlatform.UniqueID == contactId))
                                guiderPlatform = m._launchPlatform;
                            else if (m._guidingRadarSystem?._baseObject != null && (GetUnitContactId(m._guidingRadarSystem._baseObject) == contactId || m._guidingRadarSystem._baseObject.UniqueID == contactId))
                                guiderPlatform = m._guidingRadarSystem._baseObject;
                        }

                        if (guiderPlatform != null)
                        {
                            string cleanName = FormatCleanUnitName(guiderPlatform);
                            if (!string.IsNullOrEmpty(cleanName) && !value.Contains(cleanName))
                            {
                                string replacement = $"From {contactId} ({cleanName})";
                                value = value.Substring(0, fromIdx) + replacement + value.Substring(end);
                            }
                        }
                    }
                }

                // 2. Resolve ExtendedGuidance's "Terminal local" to indicate active illuminator platform
                // Example: "Terminal local" -> "Illum: 1004 (CG-67 Shiloh)" or "Terminal local (1001 DDG-51 Arleigh Burke)"
                if (value.Contains("Terminal local") && m != null)
                {
                    SensorSystemRadar termRadar = EgBridge.GetActiveEgIlluminator(m) ?? m._guidingRadarSystem;
                    ObjectBase termGuider = termRadar?._baseObject ?? m._launchPlatform;
                    if (termGuider != null && !termGuider.IsDestroyed)
                    {
                        int gId = GetUnitContactId(termGuider);
                        string gName = FormatCleanUnitName(termGuider);
                        string idPrefix = gId > 0 ? $"{gId} " : "";
                        string termLabel = (termGuider == m._launchPlatform)
                            ? $"Terminal local ({idPrefix}{gName})"
                            : $"Illum: {idPrefix}({gName})";
                        value = value.Replace("Terminal local", termLabel);
                    }
                }

                if (m == null)
                    return true;

                if (m._seeker != null && m._seeker._guidanceType == AmmunitionParameters.GuidanceType.LaserHoming)
                    return true;

                // 3. Identify the active guider: consort FCR illuminator OR cooperative tracking consort (e.g. E-2D) OR launcher
                SensorSystemRadar activeRadar = EgBridge.GetActiveEgIlluminator(m) ?? m._guidingRadarSystem;
                ObjectBase guider = activeRadar?._baseObject;
                if (guider == null || guider.IsDestroyed)
                {
                    ObjectBase target = m.CurrentStrongestTargetEcho?._objectBase ?? m.CurrentIntendedTargetObject;
                    if (target != null && m._launchPlatform != null)
                    {
                        guider = CecCapability.FindBestTrackingCooperator(m._launchPlatform, target);
                    }
                }
                if (guider == null || guider.IsDestroyed)
                {
                    guider = m._launchPlatform;
                }

                if (guider == null || guider.IsDestroyed)
                    return true;

                ObjectBase launcher = m._launchPlatform;
                string telemetryString = CecCapability.FormatMissileDatalinkUiString(m, activeRadar, guider, launcher);
                if (string.IsNullOrEmpty(telemetryString))
                    return true;

                string safeGuiderName = EscapeInlineText(telemetryString);

                // If already formatted, skip re-processing
                if (value.Contains(safeGuiderName))
                    return true;

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

                if (!replacedVanillaToken && guider != launcher)
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

        public static int GetUnitContactId(ObjectBase obj)
        {
            if (obj == null) return -1;
            int id = obj.GetMapContactID();
            if (id > 0) return id;

            var tf = Globals._playerTaskforce;
            if (tf?.PlottingTable?.LocalVehicles != null && tf.PlottingTable.LocalVehicles.TryGetValue(obj, out var v) && v != null && v.Id > 0)
                return v.Id;

            return obj.UniqueID;
        }

        public static string FormatCleanUnitName(ObjectBase obj)
        {
            if (obj == null) return "Unknown";
            string name = obj.getName() ?? "";

            // Strip nickname in single quotes if present (e.g. "E-2D VAW-113 'Black Eagles'" -> "E-2D VAW-113")
            int quoteIdx = name.IndexOf('\'');
            if (quoteIdx > 0)
            {
                name = name.Substring(0, quoteIdx).Trim();
            }

            string shortModel = obj._obp?._shortName;
            if (!string.IsNullOrEmpty(shortModel) && !name.StartsWith(shortModel, StringComparison.OrdinalIgnoreCase))
            {
                name = $"{shortModel} {name}".Trim();
            }
            return name;
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

        private static ObjectBase FindObjectByContactId(int contactId)
        {
            if (contactId <= 0) return null;

            var tf = Globals._playerTaskforce;
            if (tf?.PlottingTable?.LocalVehicles != null)
            {
                foreach (var kvp in tf.PlottingTable.LocalVehicles)
                {
                    if (kvp.Value != null && kvp.Value.Id == contactId && kvp.Key != null)
                        return kvp.Key;
                }
            }

            if (tf?.PlottingTable?.Vehicles != null)
            {
                for (int i = 0; i < tf.PlottingTable.Vehicles.Count; i++)
                {
                    var v = tf.PlottingTable.Vehicles[i];
                    if (v != null && v.Id == contactId && v.Object != null)
                        return v.Object;
                }
            }

            if (tf != null)
            {
                if (tf._taskforceVessels != null)
                {
                    for (int i = 0; i < tf._taskforceVessels.Count; i++)
                    {
                        var v = tf._taskforceVessels[i];
                        if (v != null && (v.GetMapContactID() == contactId || v.UniqueID == contactId)) return v;
                    }
                }
                if (tf._taskforceAircraft != null)
                {
                    for (int i = 0; i < tf._taskforceAircraft.Count; i++)
                    {
                        var ac = tf._taskforceAircraft[i];
                        if (ac != null && (ac.GetMapContactID() == contactId || ac.UniqueID == contactId)) return ac;
                    }
                }
                if (tf._taskforceHelicopters != null)
                {
                    for (int i = 0; i < tf._taskforceHelicopters.Count; i++)
                    {
                        var h = tf._taskforceHelicopters[i];
                        if (h != null && (h.GetMapContactID() == contactId || h.UniqueID == contactId)) return h;
                    }
                }
            }

            if (Singleton<ObjectsManager>.InstanceExists() && Singleton<ObjectsManager>.Instance._listOfAllObjects != null)
            {
                var all = Singleton<ObjectsManager>.Instance._listOfAllObjects;
                for (int i = 0; i < all.Count; i++)
                {
                    var o = all[i];
                    if (o != null && (o.GetMapContactID() == contactId || o.UniqueID == contactId)) return o;
                }
            }

            return null;
        }
    }
}
