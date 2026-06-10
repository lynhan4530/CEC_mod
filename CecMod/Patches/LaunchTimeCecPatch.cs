using HarmonyLib;
using SeaPower;

namespace CecMod.Patches
{
    /// <summary>
    /// Patch 1 (launch-time CEC). The engine only runs external-guidance search for land SAMs
    /// (the body bails when _externalGuidingSystems.Count == 0). For vessel launchers we instead run
    /// a task-force-wide scan that injects qualifying consort radars into the launcher's associated
    /// sensors, so the stock activateGuidingSystem()/FCR checks naturally pick a consort illuminator.
    /// Land units keep vanilla behavior.
    /// </summary>
    [HarmonyPatch(typeof(WeaponSystem), nameof(WeaponSystem.CheckExternalRadarGuidance))]
    internal static class LaunchTimeCecPatch
    {
        // Reuse the engine's own throttle field (unused for ships, since vanilla returns before it).
        private static readonly AccessTools.FieldRef<WeaponSystem, float> LastSearchTime =
            AccessTools.FieldRefAccess<WeaponSystem, float>("_lastSearchForSensorTime");

        private static bool Prefix(WeaponSystem __instance)
        {
            if (!(__instance._baseObject is Vessel))
                return true; // land SAM trucks etc. -> vanilla external-guidance search

            // Skip CEC while one of the launcher's own sensors is free to guide.
            if (HasFreeLocalSensor(__instance))
                return false;

            // Throttle to the engine's ~30s external-search cadence.
            ref float next = ref LastSearchTime.Invoke(__instance);
            if (GameTime.time <= next)
                return false;
            next = GameTime.time + 30f;

            CecCapability.RefreshConsortRadars(__instance);
            return false; // CEC handled; the vanilla body would no-op for ships anyway
        }

        private static bool HasFreeLocalSensor(WeaponSystem ws)
        {
            var associated = ws._vwp?._associatedSensors;
            if (associated == null) return false;
            foreach (var s in associated)
            {
                if (s != null && !s.Inoperable.Value && !s._isGuiding && s._baseObject == ws._baseObject)
                    return true;
            }
            return false;
        }
    }
}
