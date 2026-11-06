using System;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace CecMod.Patches
{
    /// <summary>
    /// Bottom-bar UI indicator showing when a missile's guidance has been handed off
    /// to a CEC consort or AWACS (<c>Launcher -&gt; Guider</c>).
    /// In Sea Power 0.8.7, the datalink status string is built by
    /// <see cref="ObjectMissileControlViewModel"/>.<c>GoodConnection(Missile)</c>.
    /// </summary>
    [HarmonyPatch(typeof(ObjectMissileControlViewModel), "GoodConnection")]
    internal static class DatalinkUiPatch
    {
        private static void Postfix(Missile m, ref string __result)
        {
            try
            {
                if (m == null || string.IsNullOrEmpty(__result))
                    return;

                ObjectBase guider = m._guidingRadarSystem?._baseObject;
                if (guider == null)
                    return;

                ObjectBase launcher = m._launchPlatform;
                if (launcher == guider)
                    return;

                string guiderName = guider.getName();
                if (string.IsNullOrEmpty(guiderName))
                    return;

                string launcherName = launcher != null ? launcher.getName() : null;
                if (!string.IsNullOrEmpty(launcherName) && __result.Contains(launcherName))
                {
                    __result = __result.Replace(launcherName, $"{launcherName} -> {guiderName}");
                }
                else if (__result.Contains("Nibiru"))
                {
                    string label = !string.IsNullOrEmpty(launcherName) ? launcherName : "Sunk Launcher";
                    __result = __result.Replace("Nibiru", $"{label} -> {guiderName}");
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] DatalinkUiPatch exception: {ex}");
            }
        }
    }
}
