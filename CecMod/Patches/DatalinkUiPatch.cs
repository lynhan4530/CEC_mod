using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace CecMod.Patches
{
    /// <summary>
    /// Patch 4 (UI datalink display). The missile-control panel shows the launching ship's name. When
    /// a consort is actually guiding (launch-time CEC or after a handoff), rewrite it to
    /// "launcher -> guiding ship" so the panel reflects who currently holds the missile, updating live
    /// as handoffs occur.
    /// </summary>
    [HarmonyPatch(typeof(ObjectMissileControlViewModel))]
    internal static class DatalinkUiPatch
    {
        [HarmonyPatch("GoodConnection")]
        [HarmonyPostfix]
        private static void GoodConnectionPostfix(Missile m, ref string __result)
        {
            // GoodConnection substitutes ${ParentName} with _launchPlatform.getName().
            __result = InjectGuider(m, __result, m._launchPlatform?.getName());
        }

        [HarmonyPatch("BadConnection")]
        [HarmonyPostfix]
        private static void BadConnectionPostfix(Missile m, ref string __result)
        {
            // Bad/LostConnection substitute ${ParentName} with _launchPlatform.Name.Value.
            __result = InjectGuider(m, __result, m._launchPlatform?.Name.Value);
        }

        [HarmonyPatch("LostConnection")]
        [HarmonyPostfix]
        private static void LostConnectionPostfix(Missile m, ref string __result)
        {
            __result = InjectGuider(m, __result, m._launchPlatform?.Name.Value);
        }

        private static string InjectGuider(Missile m, string result, string launcherName)
        {
            if (string.IsNullOrEmpty(result) || string.IsNullOrEmpty(launcherName)) return result;
            var guider = m._guidingRadarSystem?._baseObject;
            if (guider == null || m._launchPlatform == null || guider == m._launchPlatform) return result;
            return result.Replace(launcherName, launcherName + " -> " + guider.getName());
        }
    }
}
