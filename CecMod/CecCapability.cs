using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SeaPower;
using UnityEngine;

namespace CecMod
{
    /// <summary>
    /// Single source of truth for "is this consort radar usable as a CEC guidance source".
    /// A consort qualifies when it is a same-task-force vessel, within the datalink net, carrying an
    /// active datalink-capable radar that matches the launcher's own illuminator type and has a free
    /// guidance channel. Used by both the launch-time injection and the mid-flight handoff paths.
    /// </summary>
    internal static class CecCapability
    {
        /// <summary>
        /// CEC datalink net range. The engine measures distance in units where 1 nmi ~= 27.5595f
        /// (see RadarParameters range conversion); 50 nmi is the design net range.
        /// </summary>
        public const float NetRangeUnits = 50f * 27.559496f;

        public static float NetRangeSq => NetRangeUnits * NetRangeUnits;

        // Tracks the consort radars WE injected into each launcher's _associatedSensors, so a refresh
        // can pull our stale entries without ever touching the ship's native sensors.
        private static readonly ConditionalWeakTable<WeaponSystem, HashSet<SensorSystem>> _injected =
            new ConditionalWeakTable<WeaponSystem, HashSet<SensorSystem>>();

        /// <summary>
        /// Launch-time CEC: refresh the set of consort radars injected into <paramref name="ws"/>'s
        /// associated-sensor pool so that activateGuidingSystem()/FCR checks can pick a consort
        /// illuminator. Acceptable illuminator types are derived from the launcher's own radar FCRs,
        /// so a consort only qualifies if it carries the same illuminator the launcher would use.
        /// </summary>
        public static void RefreshConsortRadars(WeaponSystem ws)
        {
            var platform = ws?._baseObject;
            if (platform == null || platform.IsDestroyed) return;
            var tf = platform._taskforce;
            var associated = ws._vwp?._associatedSensors;
            if (tf == null || associated == null) return;

            // Illuminator types this launcher can use = system names of its own radar FCRs.
            var acceptable = new HashSet<string>();
            foreach (var s in associated)
            {
                if (s is SensorSystemRadar r0 && !string.IsNullOrEmpty(r0._systemName))
                    acceptable.Add(r0._systemName);
            }
            if (acceptable.Count == 0) return; // no FCR config -> nothing to share or receive

            var injected = _injected.GetOrCreateValue(ws);
            foreach (var prev in injected)
                associated.Remove(prev);
            injected.Clear();

            var fromPos = platform.transform.position;
            foreach (var vessel in tf._taskforceVessels)
            {
                if (vessel == null || vessel == platform || vessel.IsDestroyed) continue;
                if ((vessel.transform.position - fromPos).sqrMagnitude > NetRangeSq) continue;
                if (vessel._obp?._sensorSystems == null) continue;
                foreach (var s in vessel._obp._sensorSystems)
                {
                    if (s is SensorSystemRadar r && Qualifies(r, acceptable) && !associated.Contains(r))
                    {
                        associated.Add(r);
                        injected.Add(r);
                    }
                }
            }
        }

        /// <summary>
        /// Mid-flight handoff: find the best consort radar that can pick up guidance for an in-flight
        /// <paramref name="missile"/>, or null if none. Filters on the missile's own
        /// _compatibleGuidanceSystems (we know the ammo here, unlike the launch-time path).
        /// </summary>
        public static SensorSystemRadar FindHandoffRadar(Missile missile)
        {
            var ap = missile?._ap;
            var launcher = missile?._launchPlatform;
            var tf = launcher?._taskforce;
            if (ap == null || tf == null) return null;

            var acceptable = ap._compatibleGuidanceSystems;
            // Net range measured ship-to-ship from the launching platform (the CEC requirement);
            // the engine's own LOS/horizon checks validate the consort -> target geometry afterwards.
            var fromPos = launcher.transform.position;
            var current = missile._guidingRadarSystem;

            SensorSystemRadar best = null;
            float bestSq = float.MaxValue;
            foreach (var vessel in tf._taskforceVessels)
            {
                if (vessel == null || vessel.IsDestroyed) continue;
                float distSq = (vessel.transform.position - fromPos).sqrMagnitude;
                if (distSq > NetRangeSq) continue;
                if (vessel._obp?._sensorSystems == null) continue;
                foreach (var s in vessel._obp._sensorSystems)
                {
                    if (!(s is SensorSystemRadar r) || r == current) continue;
                    if (!QualifiesCompatible(r, acceptable)) continue;
                    if (distSq < bestSq)
                    {
                        bestSq = distSq;
                        best = r;
                    }
                }
            }
            return best;
        }

        // Launch-time gate: acceptable name set comes from the launcher's own FCR types.
        private static bool Qualifies(SensorSystemRadar r, HashSet<string> acceptableNames)
        {
            if (!BaseUsable(r)) return false;
            if (!acceptableNames.Contains(r._systemName)) return false;
            return r.HasFreeChannels();
        }

        // Handoff gate: acceptable names come from the missile's compatible guidance systems.
        private static bool QualifiesCompatible(SensorSystemRadar r, List<string> compatible)
        {
            if (!BaseUsable(r)) return false;
            if (compatible != null && compatible.Count > 0 && !compatible.Contains(r._systemName))
                return false;
            return r.HasFreeChannels();
        }

        // Shared physical requirements: on, operable, datalink-equipped radar.
        private static bool BaseUsable(SensorSystemRadar r)
        {
            if (r == null || r._baseObject == null || r._baseObject.IsDestroyed) return false;
            if (r.Inoperable.Value || !r.IsOn.Value) return false;
            var rp = r._radar?._rp;
            return rp != null && rp._hasDataLink;
        }
    }
}
