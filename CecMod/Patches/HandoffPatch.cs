using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;

namespace CecMod.Patches
{
    /// <summary>
    /// Patch 3 (true mid-flight handoff). The engine assigns a missile's guiding radar once at launch
    /// and never looks for a replacement: Missile.OnUpdateEveryFrame only inspects the single
    /// _guidingRadarSystem and, on loss, sets ConnectionLost -> ConnectionLostForever. This postfix
    /// detects a recoverable loss and re-acquires guidance from a CEC-capable consort, transferring
    /// the radar channels and (for SARH) re-pointing the seeker. All downstream LOS/horizon/frequency
    /// math reads off _guidingRadarSystem, so it adapts to the new ship automatically.
    /// </summary>
    [HarmonyPatch(typeof(Missile), nameof(Missile.OnUpdateEveryFrame))]
    internal static class HandoffPatch
    {
        private const float RetryInterval = 2.5f;

        private static readonly ConditionalWeakTable<Missile, StrongBox<float>> _nextAttempt =
            new ConditionalWeakTable<Missile, StrongBox<float>>();

        private static void Postfix(Missile __instance)
        {
            var m = __instance;
            if (m.IsDestroyed) return;

            bool isSarh = m._seeker != null &&
                          m._seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming;
            bool isRadioCmd = m._ap != null &&
                              m._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand;
            if (!isSarh && !isRadioCmd) return;

            // Act only on a recoverable loss with a live target.
            if (m.ConnectionLostForever.Value) return;
            bool needHandoff = m._guidingRadarSystem == null || m.ConnectionLost.Value;
            if (!needHandoff) return;
            if (m.CurrentIntendedTargetObject == null) return;

            // Throttle the per-missile task-force scan.
            var slot = _nextAttempt.GetOrCreateValue(m);
            if (GameTime.time < slot.Value) return;
            slot.Value = GameTime.time + RetryInterval;

            var candidate = CecCapability.FindHandoffRadar(m);
            var old = m._guidingRadarSystem;
            if (candidate == null || candidate == old) return;

            // Claim the new radar first; only release the old one once the new claim succeeds.
            if (!candidate.TryToAddTarget(m.CurrentIntendedTargetObject))
                return;
            if (!candidate.TryToAddWeapon(m))
            {
                candidate.FreeChannels(m.CurrentIntendedTargetObject, m); // roll back the target claim
                return;
            }
            old?.FreeChannels(m.CurrentIntendedTargetObject, m);

            m._guidingRadarSystem = candidate;
            candidate._isGuiding = true;
            m.ConnectionLost.Value = false;
            m.ConnectionLostForever.Value = false;
            if (isSarh)
                m._seeker.setGuidingSystem(candidate);

            CecPlugin.Log?.LogInfo(
                $"CEC handoff: {m.getName()} now guided by {candidate._baseObject?.getName()} ({candidate._systemName})");
        }
    }
}
