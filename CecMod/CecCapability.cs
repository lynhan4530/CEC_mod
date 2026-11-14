using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SeaPower;
using UnityEngine;

namespace CecMod
{
    /// <summary>
    /// Real-world Cooperative Engagement Capability (CEC), NIFC-CA, NATO Link 16 / Link 11,
    /// Soviet/Russian Fort/Poliment-Redut/Zaslon/TKS-2, and PLA/PLAN JSIDLS/HN-900 multi-tier
    /// qualification and combat datalink network engine for Sea Power.
    /// </summary>
    internal static class CecCapability
    {
        [Flags]
        public enum CecNetwork
        {
            None = 0,
            /// <summary>USN / JMSDF / RAN / ROKN / RCN / Spanish AEGIS, AN/USG-2B CEC &amp; NIFC-CA.</summary>
            UsAlliedNaval = 1 << 0,
            /// <summary>NATO / European APAR (ICWI), PAAMS (SAMPSON / EMPAR / MFRA / Herakles / Sea Fire 500) &amp; Link 16.</summary>
            NatoEuroNaval = 1 << 1,
            /// <summary>Soviet / Russian Fort (S-300F/FM), Poliment-Redut (5P-20K), Shtil-1 &amp; A-50/Ka-31 network.</summary>
            RuNaval = 1 << 2,
            /// <summary>PLA / PLAN Integrated Naval Datalink ("A-Shoot-B-Guide": Type 346/A/B, KJ-500/600/3000, Z-18J).</summary>
            PlanNaval = 1 << 3,
            /// <summary>US / NATO Air-to-Air Link 16 / MADL / IFDL &amp; AWACS cooperative guidance.</summary>
            UsNatoAir = 1 << 4,
            /// <summary>Soviet / Russian APD-518 / AK-RLDN (MiG-31 Zaslon, Su-35/Su-57, A-50 Shmel).</summary>
            RuAirZaslon = 1 << 5,
            /// <summary>PLA Air-to-Air &amp; AWACS "A-Shoot-B-Guide" (J-20, J-16, J-15T/D, J-35, KJ-500/2000/3000).</summary>
            PlaAir = 1 << 6,

            WesternJoint = UsAlliedNaval | NatoEuroNaval | UsNatoAir,
            RuJoint = RuNaval | RuAirZaslon,
            PlaJoint = PlanNaval | PlaAir
        }

        private const string P2027Suffix = " (P2027)";

        private static readonly ConditionalWeakTable<WeaponSystem, HashSet<SensorSystem>> _injectedSensors =
            new ConditionalWeakTable<WeaponSystem, HashSet<SensorSystem>>();

        private static readonly ConditionalWeakTable<WeaponSystem, HashSet<string>> _injectedExternalNames =
            new ConditionalWeakTable<WeaponSystem, HashSet<string>>();

        private static readonly ConditionalWeakTable<Aircraft, ObjectBase> _flightGroupLeaders =
            new ConditionalWeakTable<Aircraft, ObjectBase>();

        // =========================================================================================
        // 1. PROFILE RESOLVERS & LOOKUPS ($O(1)$ from CecDataTables)
        // =========================================================================================

        public static string NormalizeRadarKey(string systemName)
        {
            if (string.IsNullOrEmpty(systemName))
                return string.Empty;

            if (systemName.EndsWith(P2027Suffix, StringComparison.OrdinalIgnoreCase))
            {
                return systemName.Substring(0, systemName.Length - P2027Suffix.Length).TrimEnd();
            }

            return systemName;
        }

        public static PlatformProfile GetPlatformProfile(ObjectBase platform)
        {
            if (platform?._obp == null || string.IsNullOrEmpty(platform._obp._objectIniName))
                return PlatformProfile.DefaultTier3;

            string key = NormalizeRadarKey(platform._obp._objectIniName);
            if (CecDataTables.Platforms.TryGetValue(key, out var p))
                return p;

            return PlatformProfile.DefaultTier3;
        }

        public static RadarProfile GetRadarProfile(SensorSystemRadar radar)
        {
            if (radar == null || string.IsNullOrEmpty(radar._systemName))
                return RadarProfile.DefaultTier3;

            string key = NormalizeRadarKey(radar._systemName);
            if (CecDataTables.Radars.TryGetValue(key, out var r))
                return r;

            return RadarProfile.DefaultTier3;
        }

        public static MissileProfile GetMissileProfile(AmmunitionParameters ap)
        {
            if (ap == null || string.IsNullOrEmpty(ap._ammunitionFileName))
                return MissileProfile.DefaultTier3;

            string key = NormalizeRadarKey(ap._ammunitionFileName);
            if (CecDataTables.Missiles.TryGetValue(key, out var m))
                return m;

            return MissileProfile.DefaultTier3;
        }

        public static bool IsPureCwDishIlluminator(string systemName)
        {
            if (string.IsNullOrEmpty(systemName)) return false;
            string key = NormalizeRadarKey(systemName);
            if (CecDataTables.Radars.TryGetValue(key, out var r))
                return r.IsPureCwIlluminator;
            return false;
        }

        public static bool IsSyntheticDatalinkSensor(SensorSystem s)
        {
            if (s == null || string.IsNullOrEmpty(s._systemName)) return false;
            string key = NormalizeRadarKey(s._systemName);
            if (CecDataTables.Radars.TryGetValue(key, out var r))
                return r.IsSyntheticDatalink;
            return false;
        }

        public static bool IsAwacsRadar(SensorSystem s)
        {
            if (s == null || string.IsNullOrEmpty(s._systemName)) return false;
            string key = NormalizeRadarKey(s._systemName);
            if (CecDataTables.Radars.TryGetValue(key, out var r))
                return r.IsAwacs;
            return false;
        }

        public static SarhIllumFamily GetSarhFamily(SensorSystemRadar r)
        {
            if (r == null || string.IsNullOrEmpty(r._systemName)) return SarhIllumFamily.None;
            string key = NormalizeRadarKey(r._systemName);
            if (CecDataTables.Radars.TryGetValue(key, out var prof))
                return prof.SarhIllumFamily;
            return SarhIllumFamily.None;
        }

        public static SarhIllumFamily GetRequiredSarhFamily(ObjectBase launcher, WeaponSystem ws = null)
        {
            if (ws?._ammoForEngage?._ap != null)
            {
                var mp = GetMissileProfile(ws._ammoForEngage._ap);
                if (mp.RequiredSarhFamily != SarhIllumFamily.None)
                    return mp.RequiredSarhFamily;
            }

            if (ws?._vwp?._associatedSensors != null)
            {
                foreach (var s in ws._vwp._associatedSensors)
                {
                    if (s is SensorSystemRadar r && s._baseObject == launcher)
                    {
                        var fam = GetSarhFamily(r);
                        if (fam != SarhIllumFamily.None) return fam;
                    }
                }
            }
            if (launcher?._obp?._sensorSystems != null)
            {
                foreach (var s in launcher._obp._sensorSystems)
                {
                    if (s is SensorSystemRadar r)
                    {
                        var fam = GetSarhFamily(r);
                        if (fam != SarhIllumFamily.None) return fam;
                    }
                }
            }
            return SarhIllumFamily.None;
        }

        public static CecNetwork GetSensorNetwork(SensorSystem s)
        {
            if (s == null || string.IsNullOrEmpty(s._systemName)) return CecNetwork.None;
            var prof = s is SensorSystemRadar r ? GetRadarProfile(r) : null;
            if (prof == null || prof.CecTier == CecTier.Tier0) return CecNetwork.None;

            if ((prof.Datalinks & (DatalinkStandard.Usg2Cec | DatalinkStandard.Ttnt | DatalinkStandard.Link16 | DatalinkStandard.Link11 | DatalinkStandard.Ifdl | DatalinkStandard.Madl)) != 0)
                return CecNetwork.WesternJoint;
            if ((prof.Datalinks & (DatalinkStandard.Jsidls | DatalinkStandard.Hn900)) != 0)
                return CecNetwork.PlaJoint;
            if ((prof.Datalinks & (DatalinkStandard.RuFortTvm | DatalinkStandard.Tks2Apd518 | DatalinkStandard.RuNavalTactical)) != 0)
                return CecNetwork.RuJoint;

            return CecNetwork.WesternJoint;
        }

        public static CecNetwork GetPlatformNetwork(ObjectBase platform)
        {
            if (platform == null) return CecNetwork.None;
            var p = GetPlatformProfile(platform);
            if (p.CecTier == CecTier.Tier0) return CecNetwork.None;

            if ((p.Datalinks & (DatalinkStandard.Usg2Cec | DatalinkStandard.Ttnt | DatalinkStandard.Link16 | DatalinkStandard.Link11 | DatalinkStandard.Ifdl | DatalinkStandard.Madl)) != 0)
                return CecNetwork.WesternJoint;
            if ((p.Datalinks & (DatalinkStandard.Jsidls | DatalinkStandard.Hn900)) != 0)
                return CecNetwork.PlaJoint;
            if ((p.Datalinks & (DatalinkStandard.RuFortTvm | DatalinkStandard.Tks2Apd518 | DatalinkStandard.RuNavalTactical)) != 0)
                return CecNetwork.RuJoint;

            return CecNetwork.WesternJoint;
        }

        // =========================================================================================
        // 2. PAIRWISE INTERACTION & FLIGHT ISOLATION
        // =========================================================================================

        public static bool IsSarhMissile(AmmunitionParameters ap)
        {
            if (ap == null) return false;
            if (ap._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
                return true;
            var mp = GetMissileProfile(ap);
            return mp.GuidanceFamily == GuidanceFamily.SARH
                || mp.GuidanceFamily == GuidanceFamily.TVM
                || mp.RequiredSarhFamily != SarhIllumFamily.None;
        }

        public static void ForceEnableRadarSynchronous(SensorSystemRadar r)
        {
            if (r == null || r.Inoperable.Value) return;
            if (!r.IsOn.Value)
            {
                r.Enable(allowMessage: false, turnOffAfterEngage: true);
                r.IsOn.Value = true;
                r._wasSetOn = true;
            }
        }

        public static bool HasAwacsRadar(ObjectBase unit)
        {
            var systems = unit?._obp?._sensorSystems;
            if (systems == null) return false;
            for (int i = 0; i < systems.Count; i++)
            {
                if (systems[i] is SensorSystemRadar r && IsAwacsRadar(r))
                    return true;
            }
            return false;
        }

        private static int GetEffectiveInteractionRank(InteractionTier tier)
        {
            return tier switch
            {
                InteractionTier.SatcomGlobal => 6,
                InteractionTier.Tier1_FullCec => 5,
                InteractionTier.Tier1_IntraFlightOnly => 4,
                InteractionTier.Tier2_MidcourseRescue => 3,
                InteractionTier.Tier2_CueOnly => 2,
                InteractionTier.Tier3_TrackRelay => 1,
                _ => 0
            };
        }

        public static InteractionTier ResolveBestDatalinkPair(
            ObjectBase shooter,
            ObjectBase consort,
            out DatalinkStandard bestS1,
            out DatalinkStandard bestS2,
            out bool usedGateway)
        {
            bestS1 = DatalinkStandard.None;
            bestS2 = DatalinkStandard.None;
            usedGateway = false;

            if (shooter == null || consort == null)
                return InteractionTier.Incompatible;

            var p1 = GetPlatformProfile(shooter);
            var p2 = GetPlatformProfile(consort);

            if (shooter == consort)
            {
                foreach (var s in CecDataTables.GetIndividualStandards(p1.Datalinks))
                {
                    bestS1 = s;
                    bestS2 = s;
                    if (s != DatalinkStandard.SatcomUplink)
                        break;
                }
                return p1.CecTier == CecTier.Tier0 ? InteractionTier.Incompatible : InteractionTier.Tier1_FullCec;
            }

            if (p1.CecTier == CecTier.Tier0 || p2.CecTier == CecTier.Tier0)
                return InteractionTier.Incompatible;

            // SATCOM weapons/platforms bypass terrestrial links
            if ((p1.Datalinks & DatalinkStandard.SatcomUplink) != 0 || (p2.Datalinks & DatalinkStandard.SatcomUplink) != 0)
            {
                bestS1 = DatalinkStandard.SatcomUplink;
                bestS2 = DatalinkStandard.SatcomUplink;
                return InteractionTier.SatcomGlobal;
            }

            bool sameFlight = IsSameFlightGroup(shooter, consort);
            bool hasAwacs = HasAwacsRadar(shooter) || HasAwacsRadar(consort);
            bool hasGateway = TaskforceHasActiveGateway(shooter._taskforce);

            InteractionTier bestTier = InteractionTier.Incompatible;
            int bestRank = 0;

            foreach (DatalinkStandard s1 in CecDataTables.GetIndividualStandards(p1.Datalinks))
            {
                foreach (DatalinkStandard s2 in CecDataTables.GetIndividualStandards(p2.Datalinks))
                {
                    InteractionTier raw = CecDataTables.QueryDatalinkMatrix(s1, s2);
                    InteractionTier effective = raw;
                    bool pairUsedGw = false;

                    if (raw == InteractionTier.Tier1_IntraFlightOnly)
                    {
                        // TKS-2 / APD-518 links Russian fighters with A-50U AWACS across flight groups
                        bool tksAwacsLink = s1 == DatalinkStandard.Tks2Apd518 && s2 == DatalinkStandard.Tks2Apd518 && hasAwacs;
                        effective = (sameFlight || tksAwacsLink)
                            ? InteractionTier.Tier1_FullCec
                            : InteractionTier.Incompatible;
                    }
                    else if (raw == InteractionTier.GatewayRequired)
                    {
                        if (hasGateway)
                        {
                            effective = InteractionTier.Tier1_FullCec;
                            pairUsedGw = true;
                        }
                        else
                        {
                            effective = InteractionTier.Tier2_CueOnly;
                        }
                    }
                    else if (raw == InteractionTier.Tier2_MidcourseRescue
                             && s1 == DatalinkStandard.Link16 && s2 == DatalinkStandard.Link16
                             && p1.CecTier == CecTier.Tier1 && p2.CecTier == CecTier.Tier1)
                    {
                        // Tier 1 Link 16 platforms (e.g. GlobalEye, Eurofighter, Rafale Late, JAS-39, KF-21)
                        effective = InteractionTier.Tier1_FullCec;
                    }

                    int rank = GetEffectiveInteractionRank(effective);
                    bool isBetter = rank > bestRank
                        || (rank == bestRank && rank > 0 && s1 == s2 && bestS1 != bestS2)
                        || (rank == bestRank && rank > 0 && !pairUsedGw && usedGateway);

                    if (isBetter)
                    {
                        bestRank = rank;
                        bestTier = effective;
                        bestS1 = s1;
                        bestS2 = s2;
                        usedGateway = pairUsedGw;
                    }
                }
            }

            return bestTier;
        }

        public static InteractionTier EvaluateInteraction(ObjectBase shooter, ObjectBase consort)
        {
            if (shooter == null || consort == null || shooter == consort)
                return InteractionTier.Incompatible;

            return ResolveBestDatalinkPair(shooter, consort, out _, out _, out _);
        }

        public static bool IsSameFlightGroup(ObjectBase a1, ObjectBase a2)
        {
            if (a1 == null || a2 == null) return false;
            if (a1 == a2) return true;
            if (!(a1 is Aircraft ac1) || !(a2 is Aircraft ac2)) return false;

            ObjectBase leader1 = GetFlightLeader(ac1);
            ObjectBase leader2 = GetFlightLeader(ac2);
            return leader1 != null && leader1 == leader2;
        }

        private static ObjectBase GetFlightLeader(Aircraft ac)
        {
            if (_flightGroupLeaders.TryGetValue(ac, out var leader) && leader != null && !leader.IsDestroyed)
                return leader;

            leader = ac.ActiveFormationLeader ?? ac.Formation?.LeaderStation?.UnitObject ?? ac;
            _flightGroupLeaders.Remove(ac);
            _flightGroupLeaders.Add(ac, leader);
            return leader;
        }

        public static bool TaskforceHasActiveGateway(Taskforce tf)
        {
            if (tf == null) return false;

            if (tf._taskforceAircraft != null)
            {
                for (int i = 0; i < tf._taskforceAircraft.Count; i++)
                {
                    var ac = tf._taskforceAircraft[i];
                    if (ac != null && !ac.IsDestroyed && GetPlatformProfile(ac).IsGateway)
                        return true;
                }
            }

            if (tf._taskforceVessels != null)
            {
                for (int i = 0; i < tf._taskforceVessels.Count; i++)
                {
                    var v = tf._taskforceVessels[i];
                    if (v != null && !v.IsDestroyed && GetPlatformProfile(v).IsGateway)
                        return true;
                }
            }

            return false;
        }

        // =========================================================================================
        // 3. RANGE & HORIZON PHYSICS
        // =========================================================================================

        public static bool ArePlatformsInDatalinkReach(ObjectBase p1, ObjectBase p2, float linkDist)
        {
            float h1 = DatalinkMastHeight(p1, requireRadarOn: false);
            float h2 = DatalinkMastHeight(p2, requireRadarOn: true);
            if (h1 < 0f || h2 < 0f) return false;

            // Ship-to-ship datalinks have unlimited range (naval SATCOM)
            if (p1 is Vessel && p2 is Vessel) return true;

            // If SATCOM transceiver present, range is planetary
            if (HasSatcomTransceiver(p1) || HasSatcomTransceiver(p2)) return true;

            float horizon = RadarCalculator.getRadarHorizon(
                Mathf.Max(h1, 20f), Mathf.Max(h2, 20f), visual: false, 0f);
            float maxInstrumented = Mathf.Max(GetMaxDatalinkRange(p1), GetMaxDatalinkRange(p2));
            float maxReach = Mathf.Min(horizon, maxInstrumented);
            return linkDist <= maxReach;
        }

        public static float GetMaxDatalinkRange(ObjectBase p)
        {
            if (p == null) return 0f;
            if (p is Aircraft || p is Helicopter)
                return 300f * 27.559496f; // ~300 nmi in Unity world units
            return 150f * 27.559496f; // ~150 nmi surface LOS cap
        }

        public static bool HasSatcomTransceiver(ObjectBase p)
        {
            if (p == null) return false;
            var prof = GetPlatformProfile(p);
            if ((prof.Datalinks & DatalinkStandard.SatcomUplink) != 0) return true;

            var systems = p._obp?._sensorSystems;
            if (systems != null)
            {
                for (int i = 0; i < systems.Count; i++)
                {
                    string sysName = systems[i]?._systemName;
                    if (!string.IsNullOrEmpty(sysName) &&
                        (sysName.IndexOf("GPS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         sysName.IndexOf("SATCOM", StringComparison.OrdinalIgnoreCase) >= 0))
                        return true;
                }
            }
            return false;
        }

        public static float DatalinkMastHeight(ObjectBase platform, bool requireRadarOn)
        {
            var systems = platform?._obp?._sensorSystems;
            if (systems == null) return -1f;

            float best = -1f;
            for (int i = 0; i < systems.Count; i++)
            {
                if (!(systems[i] is SensorSystemRadar r)) continue;
                if (r.Inoperable.Value || r._radar?._rp == null || r._mountObject == null) continue;
                if (requireRadarOn && !r.IsOn.Value) continue;

                var rProf = GetRadarProfile(r);
                if (r._radar._rp._hasDataLink || rProf.CecTier != CecTier.Tier0 || rProf.Datalinks != DatalinkStandard.None || rProf.IsSyntheticDatalink)
                {
                    float y = r._mountObject.transform.position.y;
                    if (y > best) best = y;
                }
            }

            if (best < 0f && platform != null && (platform is Aircraft || platform is Helicopter || platform is Vessel))
            {
                best = Mathf.Max(platform.transform.position.y, 10f);
            }

            return best;
        }

        public static bool InLinkHorizon(float nearAntennaY, float farAntennaY, float horizontalDist)
        {
            float horizon = RadarCalculator.getRadarHorizon(
                Mathf.Max(nearAntennaY, 10f), Mathf.Max(farAntennaY, 10f), visual: false, 0f);
            return horizontalDist <= horizon;
        }

        public static bool RadarCanSee(SensorSystemRadar r, ObjectBase obj)
        {
            var mount = r?._mountObject;
            if (mount == null || obj == null || obj.IsDestroyed) return false;
            float range = r._radar._rp._maxRange;
            float horizon = RadarCalculator.getRadarHorizon(
                mount.transform.position.y, obj.VisibleAltitude, visual: false,
                Utils.TerrainAltForHorizon(r._baseObject, obj));
            if (horizon < range) range = horizon;
            if (HorizontalDistance(mount.transform.position, obj.transform.position) > range)
                return false;

            bool enforceFov = r._type == SensorSystemRadar.RadarType.DirectedSearch
                              || (!IsAwacsRadar(r) && (r._baseObject is Aircraft || r._baseObject is Helicopter));
            if (enforceFov &&
                !r.IsObjectInFOV(r.SensorPosition, mount.transform.forward, obj.transform.position))
                return false;

            return true;
        }

        public static bool RadarCanReachMissile(SensorSystemRadar r, Missile m)
        {
            var mount = r?._mountObject;
            if (mount == null || m == null || m.IsDestroyed) return false;
            float horizon = RadarCalculator.getRadarHorizon(
                mount.transform.position.y, m.VisibleAltitude, visual: false,
                Utils.TerrainAltForHorizon(r._baseObject, m));
            return HorizontalDistance(mount.transform.position, m.transform.position) <= horizon;
        }

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return (a - b).magnitude;
        }

        public static bool UsableGuidanceRadar(SensorSystemRadar r)
        {
            if (r == null || r._baseObject == null || r._baseObject.IsDestroyed) return false;
            if (r.Inoperable.Value || !r.IsOn.Value) return false;
            return r._radar?._rp != null;
        }

        public static bool IsPlatformInAirRadarEmcon(ObjectBase platform)
        {
            var radars = platform?._obp?._airSearchRadars;
            if (radars != null && radars.Count > 0)
            {
                for (int i = 0; i < radars.Count; i++)
                {
                    var r = radars[i];
                    if (r != null && !r.Inoperable.Value && r.IsOn.Value)
                        return false;
                }
                return true;
            }
            return false;
        }

        public static bool IsPlatformInSurfaceRadarEmcon(ObjectBase platform)
        {
            var radars = platform?._obp?._surfaceSearchRadars;
            if (radars != null && radars.Count > 0)
            {
                for (int i = 0; i < radars.Count; i++)
                {
                    var r = radars[i];
                    if (r != null && !r.Inoperable.Value && r.IsOn.Value)
                        return false;
                }
                return true;
            }
            return false;
        }

        public static bool HasLocalRadarSight(ObjectBase platform, ObjectBase target)
        {
            if (platform == null || platform.IsDestroyed || target == null || target.IsDestroyed) return false;

            // If platform is in air EMCON and target is airborne, local radar sight is impossible
            if (target.isAirTarget() && IsPlatformInAirRadarEmcon(platform))
                return false;

            // If platform is in surface EMCON and target is on the surface, local radar sight is impossible
            bool isSurface = !target.isAirTarget() && !(target is Submarine);
            if (isSurface && IsPlatformInSurfaceRadarEmcon(platform))
                return false;

            // If the platform has not even spotted the object organically, it cannot have local radar sight
            if (platform._spottedObjects != null && !platform._spottedObjects.Contains(target))
                return false;

            var systems = platform._obp?._sensorSystems;
            if (systems == null) return false;

            float distToTarget = HorizontalDistance(platform.transform.position, target.transform.position);
            for (int i = 0; i < systems.Count; i++)
            {
                if (!(systems[i] is SensorSystemRadar r)) continue;
                if (!UsableGuidanceRadar(r) || IsSyntheticDatalinkSensor(r)) continue;
                if (target.isAirTarget() && !r.CanSeeAirTargets) continue;
                if (!RadarCanSee(r, target)) continue;

                var rangeData = r.GetRange(target);
                if (rangeData.Distance > 0f && distToTarget <= rangeData.Distance)
                    return true;
            }
            return false;
        }

        // =========================================================================================
        // 4. STRICT 4-TIER GATING ENGINE
        // =========================================================================================

        /// <summary>
        /// Evaluates whether <paramref name="shooter"/> can fire <paramref name="ap"/> at
        /// <paramref name="target"/> using remote CEC/NIFC-CA or Mountain Top Forward Pass guidance.
        /// </summary>
        public static bool CanPerformLaunchOnRemote(ObjectBase shooter, AmmunitionParameters ap, ObjectBase target)
        {
            if (shooter == null || shooter.IsDestroyed || target == null || target.IsDestroyed)
                return false;

            var shooterProfile = GetPlatformProfile(shooter);
            bool shooterSatcom = (shooterProfile.Datalinks & DatalinkStandard.SatcomUplink) != 0;
            if (shooterProfile.CecTier != CecTier.Tier1 && !shooterSatcom)
                return false;

            if (ap == null)
            {
                var wsList = shooter._obp?._weaponSystems;
                if (wsList != null)
                {
                    for (int i = 0; i < wsList.Count; i++)
                    {
                        var ws = wsList[i];
                        if (ws?._ammoForEngage?._ap != null && CanPerformLaunchOnRemote(shooter, ws._ammoForEngage._ap, target))
                            return true;
                        if (ws?._ammunitionOnWeapon?._ap != null && CanPerformLaunchOnRemote(shooter, ws._ammunitionOnWeapon._ap, target))
                            return true;

                        var magList = ws?._vwp?._associatedMagazine?._ammunitionList;
                        if (magList != null)
                        {
                            for (int j = 0; j < magList.Count; j++)
                            {
                                if (magList[j]?._ap != null && CanPerformLaunchOnRemote(shooter, magList[j]._ap, target))
                                    return true;
                            }
                        }

                        if (ws is WeaponSystemHardpoint hp && hp._weapons != null)
                        {
                            for (int j = 0; j < hp._weapons.Count; j++)
                            {
                                if (hp._weapons[j]?._ap != null && CanPerformLaunchOnRemote(shooter, hp._weapons[j]._ap, target))
                                    return true;
                            }
                        }
                    }
                }
                return false;
            }

            var missileProfile = GetMissileProfile(ap);
            if (missileProfile.CecTier == CecTier.Tier3 || missileProfile.CecTier == CecTier.Tier0)
                return false;

            var tf = shooter._taskforce;
            if (tf == null)
                return false;

            bool isSarh = IsSarhMissile(ap);

            // 1. Tier 1 ARH / IR / ARM / Command blind Launch-on-Remote
            if (!isSarh && missileProfile.AllowLaunchOnRemote)
            {
                return HasTrackingCooperatorForRemoteLaunch(shooter, target);
            }

            // 2. Tier 2 SARH Forward Pass (Project Mountain Top)
            if (isSarh)
            {
                return HasForwardPassIlluminatorCoverage(shooter, ap, target);
            }

            return false;
        }

        private static bool HasTrackingCooperatorForRemoteLaunch(ObjectBase shooter, ObjectBase target)
        {
            var tf = shooter._taskforce;
            if (tf == null) return false;

            if (tf._taskforceAircraft != null)
            {
                for (int i = 0; i < tf._taskforceAircraft.Count; i++)
                {
                    if (QualifiesAsRemoteTracker(shooter, tf._taskforceAircraft[i], target))
                        return true;
                }
            }
            if (tf._taskforceHelicopters != null)
            {
                for (int i = 0; i < tf._taskforceHelicopters.Count; i++)
                {
                    if (QualifiesAsRemoteTracker(shooter, tf._taskforceHelicopters[i], target))
                        return true;
                }
            }
            if (tf._taskforceVessels != null)
            {
                for (int i = 0; i < tf._taskforceVessels.Count; i++)
                {
                    if (QualifiesAsRemoteTracker(shooter, tf._taskforceVessels[i], target))
                        return true;
                }
            }

            return false;
        }

        private static bool QualifiesAsRemoteTracker(ObjectBase shooter, ObjectBase cooperator, ObjectBase target)
        {
            if (cooperator == null || cooperator == shooter || cooperator.IsDestroyed) return false;

            var coopProfile = GetPlatformProfile(cooperator);
            bool coopSatcom = (coopProfile.Datalinks & DatalinkStandard.SatcomUplink) != 0;
            if (coopProfile.CecTier != CecTier.Tier1 && !coopSatcom) return false;

            InteractionTier interaction = EvaluateInteraction(shooter, cooperator);
            if (interaction != InteractionTier.Tier1_FullCec &&
                interaction != InteractionTier.SatcomGlobal &&
                interaction != InteractionTier.Tier1_IntraFlightOnly)
                return false;

            float linkDist = HorizontalDistance(shooter.transform.position, cooperator.transform.position);
            if (!ArePlatformsInDatalinkReach(shooter, cooperator, linkDist)) return false;

            return CooperatorSeesTarget(cooperator, target);
        }

        private static bool HasForwardPassIlluminatorCoverage(ObjectBase shooter, AmmunitionParameters ap, ObjectBase target)
        {
            var tf = shooter._taskforce;
            if (tf == null) return false;

            SarhIllumFamily reqFam = GetMissileProfile(ap).RequiredSarhFamily;
            if (reqFam == SarhIllumFamily.None) reqFam = GetRequiredSarhFamily(shooter);

            if (tf._taskforceVessels != null)
            {
                for (int i = 0; i < tf._taskforceVessels.Count; i++)
                {
                    if (ConsortHasIlluminatorForTarget(shooter, tf._taskforceVessels[i], reqFam, target))
                        return true;
                }
            }
            if (tf._taskforceAircraft != null)
            {
                for (int i = 0; i < tf._taskforceAircraft.Count; i++)
                {
                    if (ConsortHasIlluminatorForTarget(shooter, tf._taskforceAircraft[i], reqFam, target))
                        return true;
                }
            }

            return false;
        }

        private static bool ConsortHasIlluminatorForTarget(ObjectBase shooter, ObjectBase consort, SarhIllumFamily reqFam, ObjectBase target)
        {
            if (consort == null || consort == shooter || consort.IsDestroyed) return false;

            InteractionTier interaction = EvaluateInteraction(shooter, consort);
            if (interaction != InteractionTier.Tier1_FullCec &&
                interaction != InteractionTier.SatcomGlobal &&
                interaction != InteractionTier.Tier1_IntraFlightOnly)
                return false;

            float linkDist = HorizontalDistance(shooter.transform.position, consort.transform.position);
            if (!ArePlatformsInDatalinkReach(shooter, consort, linkDist)) return false;

            var systems = consort._obp?._sensorSystems;
            if (systems == null) return false;

            for (int j = 0; j < systems.Count; j++)
            {
                if (!(systems[j] is SensorSystemRadar r)) continue;
                if (r.Inoperable.Value || r._radar?._rp == null || r._mountObject == null) continue;
                if (IsSyntheticDatalinkSensor(r) || IsAwacsRadar(r)) continue;

                SarhIllumFamily rFam = GetSarhFamily(r);
                if (r._type != SensorSystemRadar.RadarType.Targeting && rFam == SarhIllumFamily.None) continue;
                if (reqFam != SarhIllumFamily.None && rFam != reqFam) continue;

                if (GetRadarProfile(r).IsMfr)
                    EnsureMidCourseChannels(r);

                if (!r.HasFreeChannels()) continue;
                bool canSee = RadarCanSee(r, target)
                    && (r.IsOn.Value || !IsPureCwDishIlluminator(r._systemName) || HasLocalRadarSight(consort, target));
                if (canSee)
                    return true;
            }

            return false;
        }

        public static bool CanRadarGuideWeapon(SensorSystemRadar r)
        {
            if (r == null || r._baseObject == null || r._baseObject.IsDestroyed)
                return false;
            if (r.Inoperable.Value || r._radar?._rp == null)
                return false;
            if (IsPlatformInAirRadarEmcon(r._baseObject))
                return false;
            return true;
        }

        public static bool CanPerformMidCourseHandoff(Missile m, SensorSystemRadar candidateRadar)
        {
            if (m == null || m.IsDestroyed || candidateRadar == null)
                return false;

            var missileProfile = GetMissileProfile(m._ap);
            if (!missileProfile.AllowMidCourseHandoff)
                return false;

            ObjectBase shooter = m._launchPlatform;
            ObjectBase candidateHost = candidateRadar._baseObject;
            if (candidateHost == null || candidateHost.IsDestroyed)
                return false;

            if (shooter != null)
            {
                InteractionTier tier = EvaluateInteraction(shooter, candidateHost);
                if (tier != InteractionTier.Tier1_FullCec
                    && tier != InteractionTier.Tier2_MidcourseRescue
                    && tier != InteractionTier.SatcomGlobal
                    && tier != InteractionTier.Tier1_IntraFlightOnly)
                {
                    return false;
                }

                if (!shooter.IsDestroyed)
                {
                    float linkDist = HorizontalDistance(shooter.transform.position, candidateHost.transform.position);
                    if (!ArePlatformsInDatalinkReach(shooter, candidateHost, linkDist))
                        return false;
                }
            }

            if (!CanRadarGuideWeapon(candidateRadar))
                return false;

            if (IsAwacsRadar(candidateRadar) || GetRadarProfile(candidateRadar).IsMfr)
                EnsureMidCourseChannels(candidateRadar);

            if (!CanSensorGuideAmmo(candidateRadar, m._ap))
                return false;

            ObjectBase target = m.CurrentIntendedTargetObject;
            if (target == null || target.IsDestroyed)
                return false;

            if (!candidateRadar.HasFreeTargetChannels(target) && !candidateRadar.IsAimedAtTarget(target))
                return false;
            if (!candidateRadar.HasFreeWeaponChannels() && !candidateRadar.IsGuidingWeapon(m))
                return false;

            bool canSee = RadarCanSee(candidateRadar, target)
                && (candidateRadar.IsOn.Value || !IsPureCwDishIlluminator(candidateRadar._systemName) || HasLocalRadarSight(candidateHost, target));
            if (!canSee)
                return false;

            if (!RadarCanReachMissile(candidateRadar, m))
                return false;

            bool isSarh = IsSarhMissile(m._ap);
            if (isSarh)
            {
                bool hasMccDatalink = m._ap._midCourseCorrection != null && m._ap._midCourseCorrection.IsDatalink;
                bool inTerminal = EgBridge.IsActive
                    ? EgBridge.IsTerminalIlluminationDue(m)
                    : (m.getFlightStage() == WeaponBase.FlightStage.TerminalApproach);
                bool needsTerminalNow = !hasMccDatalink || inTerminal;

                SarhIllumFamily rFam = GetSarhFamily(candidateRadar);
                if (needsTerminalNow)
                {
                    if (IsAwacsRadar(candidateRadar) || IsSyntheticDatalinkSensor(candidateRadar))
                        return false;
                    if (candidateRadar._type != SensorSystemRadar.RadarType.Targeting && rFam == SarhIllumFamily.None)
                        return false;
                }

                SarhIllumFamily reqFam = missileProfile.RequiredSarhFamily;
                if (reqFam == SarhIllumFamily.None && shooter != null)
                    reqFam = GetRequiredSarhFamily(shooter);

                if (reqFam != SarhIllumFamily.None && !IsSyntheticDatalinkSensor(candidateRadar) && rFam != reqFam)
                    return false;
            }

            return true;
        }

        public static SensorSystemRadar FindHandoffRadar(Missile missile, bool requireTerminalIlluminator = false)
        {
            if (missile == null || missile.IsDestroyed || missile._ap == null)
                return null;

            var missileProfile = GetMissileProfile(missile._ap);
            if (!missileProfile.AllowMidCourseHandoff)
                return null;

            ObjectBase target = missile.CurrentIntendedTargetObject;
            if (target == null || target.IsDestroyed)
                return null;

            ObjectBase shooter = missile._launchPlatform;
            bool isPlayer = missile.IsPlayerObject || (shooter != null && shooter.IsPlayerObject);

            Taskforce tf = shooter?._taskforce
                           ?? missile._taskforce
                           ?? (isPlayer ? Globals._playerTaskforce : null)
                           ?? missile.UnitTaskforce?.Value;

            if (tf == null && Singleton<TaskforceManager>.InstanceExists() && Singleton<TaskforceManager>.Instance._taskForces != null)
            {
                var allTfs = Singleton<TaskforceManager>.Instance._taskForces;
                for (int i = 0; i < allTfs.Count; i++)
                {
                    if (allTfs[i] != null && ((isPlayer && allTfs[i].Side == Taskforce.TfType.Player) || (!isPlayer && allTfs[i].Side != Taskforce.TfType.Player)))
                    {
                        tf = allTfs[i];
                        break;
                    }
                }
            }

            if (tf == null)
                return null;

            SensorSystemRadar best = null;
            float bestDistSq = float.MaxValue;

            void CheckCooperator(ObjectBase cooperator)
            {
                if (cooperator == null || cooperator.IsDestroyed) return;
                var systems = cooperator._obp?._sensorSystems;
                if (systems == null) return;

                for (int i = 0; i < systems.Count; i++)
                {
                    if (!(systems[i] is SensorSystemRadar r)) continue;
                    if (requireTerminalIlluminator)
                    {
                        if (IsSyntheticDatalinkSensor(r) || IsAwacsRadar(r))
                            continue;
                        if (r._type != SensorSystemRadar.RadarType.Targeting && GetSarhFamily(r) == SarhIllumFamily.None)
                            continue;
                    }
                    if (!CanPerformMidCourseHandoff(missile, r))
                        continue;

                    float distSq = (r._mountObject.transform.position - target.transform.position).sqrMagnitude;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = r;
                    }
                }
            }

            if (tf._taskforceVessels != null)
            {
                for (int i = 0; i < tf._taskforceVessels.Count; i++)
                    CheckCooperator(tf._taskforceVessels[i]);
            }
            if (tf._taskforceAircraft != null)
            {
                for (int i = 0; i < tf._taskforceAircraft.Count; i++)
                    CheckCooperator(tf._taskforceAircraft[i]);
            }
            if (tf._taskforceHelicopters != null)
            {
                for (int i = 0; i < tf._taskforceHelicopters.Count; i++)
                    CheckCooperator(tf._taskforceHelicopters[i]);
            }

            return best;
        }

        // =========================================================================================
        // 5. LAUNCH-TIME POOL INJECTION & SYNC
        // =========================================================================================

        public static void RefreshConsortRadars(WeaponSystem ws)
        {
            if (ws == null || ws._vwp == null || ws._baseObject == null) return;

            ObjectBase launcher = ws._baseObject;
            var launcherProfile = GetPlatformProfile(launcher);
            if (launcherProfile.CecTier == CecTier.Tier3 || launcherProfile.CecTier == CecTier.Tier0)
                return;

            AmmunitionParameters ap = ws._ammoForEngage?._ap;
            if (ap != null)
            {
                var mProfile = GetMissileProfile(ap);
                if (mProfile.CecTier == CecTier.Tier3 || mProfile.CecTier == CecTier.Tier0)
                    return;
            }

            var tf = launcher._taskforce;
            if (tf == null) return;

            GetLauncherGuidanceNeeds(ws, out bool needsSarh, out bool needsMidCourse);
            if (!needsSarh && !needsMidCourse) return;

            SarhIllumFamily launcherSarhFamily = GetRequiredSarhFamily(launcher, ws);

            var nativeRadarNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (launcher._obp?._sensorSystems != null)
            {
                foreach (var s in launcher._obp._sensorSystems)
                {
                    if (s is SensorSystemRadar r && !string.IsNullOrEmpty(r._systemName))
                    {
                        nativeRadarNames.Add(r._systemName);
                        nativeRadarNames.Add(NormalizeRadarKey(r._systemName));
                    }
                }
            }

            if (!_injectedSensors.TryGetValue(ws, out var injected))
            {
                injected = new HashSet<SensorSystem>();
                _injectedSensors.Add(ws, injected);
            }
            if (!_injectedExternalNames.TryGetValue(ws, out var injectedExt))
            {
                injectedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _injectedExternalNames.Add(ws, injectedExt);
            }

            var associated = ws._vwp._associatedSensors;
            var externalGuidingSystems = ws._vwp._externalGuidingSystems;

            for (int i = associated.Count - 1; i >= 0; i--)
            {
                SensorSystem s = associated[i];
                if (injected.Contains(s))
                {
                    if (s == null || s._baseObject == null || s._baseObject.IsDestroyed || s.Inoperable.Value)
                    {
                        associated.RemoveAt(i);
                        injected.Remove(s);
                        if (externalGuidingSystems != null && !string.IsNullOrEmpty(s?._systemName))
                        {
                            externalGuidingSystems.Remove(s._systemName);
                            injectedExt.Remove(s._systemName);
                        }
                    }
                }
            }

            var deferredIdleIlluminators = new List<SensorSystemRadar>();

            void TryInject(ObjectBase cooperator)
            {
                if (cooperator == null || cooperator == launcher || cooperator.IsDestroyed) return;

                InteractionTier interaction = EvaluateInteraction(launcher, cooperator);
                if (interaction != InteractionTier.Tier1_FullCec &&
                    interaction != InteractionTier.SatcomGlobal &&
                    interaction != InteractionTier.Tier1_IntraFlightOnly)
                {
                    return;
                }

                float linkDist = HorizontalDistance(launcher.transform.position, cooperator.transform.position);
                if (!ArePlatformsInDatalinkReach(launcher, cooperator, linkDist)) return;

                var systems = cooperator._obp?._sensorSystems;
                if (systems == null) return;

                for (int j = 0; j < systems.Count; j++)
                {
                    if (!(systems[j] is SensorSystemRadar r)) continue;
                    if (r.Inoperable.Value || r._radar?._rp == null) continue;

                    string rKey = NormalizeRadarKey(r._systemName);
                    if (!r.IsOn.Value)
                    {
                        if (!needsSarh || r._type != SensorSystemRadar.RadarType.Targeting) continue;
                        if (IsSyntheticDatalinkSensor(r) || IsAwacsRadar(r)) continue;

                        SarhIllumFamily idleFam = GetSarhFamily(r);
                        bool idleMatch = (launcherSarhFamily != SarhIllumFamily.None && idleFam == launcherSarhFamily) ||
                                         nativeRadarNames.Contains(r._systemName) ||
                                         nativeRadarNames.Contains(rKey);
                        if (!idleMatch || !r.HasFreeChannels()) continue;

                        deferredIdleIlluminators.Add(r);
                        continue;
                    }

                    if (needsSarh && !IsSyntheticDatalinkSensor(r) && !IsAwacsRadar(r))
                    {
                        SarhIllumFamily rFam = GetSarhFamily(r);
                        bool familyMatch = (launcherSarhFamily != SarhIllumFamily.None && rFam == launcherSarhFamily) ||
                                           nativeRadarNames.Contains(r._systemName) ||
                                           nativeRadarNames.Contains(rKey);
                        if (familyMatch && r.HasFreeChannels())
                        {
                            if (!associated.Contains(r))
                            {
                                associated.Add(r);
                                injected.Add(r);
                            }
                            if (externalGuidingSystems != null && !string.IsNullOrEmpty(r._systemName) &&
                                !externalGuidingSystems.Contains(r._systemName))
                            {
                                externalGuidingSystems.Add(r._systemName);
                                injectedExt.Add(r._systemName);
                            }
                            continue;
                        }
                    }

                    if (needsMidCourse && !IsPureCwDishIlluminator(r._systemName))
                    {
                        if (IsAwacsRadar(r) || GetRadarProfile(r).IsMfr)
                            EnsureMidCourseChannels(r);

                        if (r.HasFreeChannels())
                        {
                            if (!associated.Contains(r))
                            {
                                associated.Add(r);
                                injected.Add(r);
                            }
                            if (externalGuidingSystems != null && !string.IsNullOrEmpty(r._systemName) &&
                                !externalGuidingSystems.Contains(r._systemName))
                            {
                                externalGuidingSystems.Add(r._systemName);
                                injectedExt.Add(r._systemName);
                            }
                        }
                    }
                }
            }

            if (tf._taskforceVessels != null)
            {
                for (int i = 0; i < tf._taskforceVessels.Count; i++)
                    TryInject(tf._taskforceVessels[i]);
            }
            if (tf._taskforceAircraft != null)
                for (int i = 0; i < tf._taskforceAircraft.Count; i++)
                    TryInject(tf._taskforceAircraft[i]);
            if (tf._taskforceHelicopters != null)
                for (int i = 0; i < tf._taskforceHelicopters.Count; i++)
                    TryInject(tf._taskforceHelicopters[i]);

            for (int i = 0; i < deferredIdleIlluminators.Count; i++)
            {
                var r = deferredIdleIlluminators[i];
                if (!associated.Contains(r))
                {
                    associated.Add(r);
                    injected.Add(r);
                }
                if (externalGuidingSystems != null && !string.IsNullOrEmpty(r._systemName) &&
                    !externalGuidingSystems.Contains(r._systemName))
                {
                    externalGuidingSystems.Add(r._systemName);
                    injectedExt.Add(r._systemName);
                }
            }
        }

        private static readonly ConditionalWeakTable<AmmunitionParameters, HashSet<SensorSystem>> _injectedAmmoSensors =
            new ConditionalWeakTable<AmmunitionParameters, HashSet<SensorSystem>>();

        public static void SyncConsortIlluminatorsToAmmo(WeaponSystem ws, AmmunitionParameters ap)
        {
            if (ws?._vwp?._associatedSensors == null) return;
            if (!_injectedSensors.TryGetValue(ws, out var injected) || injected == null || injected.Count == 0)
                return;

            _injectedExternalNames.TryGetValue(ws, out var injectedExt);

            if (ap == null) return;

            var missileProfile = GetMissileProfile(ap);
            if (missileProfile.CecTier == CecTier.Tier3 || missileProfile.CecTier == CecTier.Tier0)
            {
                var assocList = ws._vwp._associatedSensors;
                bool removedAny = false;
                for (int i = assocList.Count - 1; i >= 0; i--)
                {
                    var s = assocList[i];
                    if (injected.Contains(s))
                    {
                        assocList.RemoveAt(i);
                        injected.Remove(s);
                        removedAny = true;
                        if (ws._vwp._externalGuidingSystems != null && !string.IsNullOrEmpty(s?._systemName))
                        {
                            ws._vwp._externalGuidingSystems.Remove(s._systemName);
                            injectedExt?.Remove(s._systemName);
                        }
                    }
                }
                if (removedAny)
                    ws._lastSearchForSensorTime = 0.0;
                return;
            }

            bool isSarh = IsSarhMissile(ap);
            SarhIllumFamily targetFam = isSarh ? missileProfile.RequiredSarhFamily : SarhIllumFamily.None;
            if (isSarh && targetFam == SarhIllumFamily.None)
                targetFam = GetRequiredSarhFamily(ws._baseObject, ws);

            if (isSarh)
            {
                var associated = ws._vwp._associatedSensors;
                for (int i = associated.Count - 1; i >= 0; i--)
                {
                    SensorSystem s = associated[i];
                    if (!injected.Contains(s)) continue;

                    if (s is SensorSystemRadar r && !IsSyntheticDatalinkSensor(r) && !IsAwacsRadar(r))
                    {
                        if (targetFam != SarhIllumFamily.None && GetSarhFamily(r) != SarhIllumFamily.None && GetSarhFamily(r) != targetFam)
                        {
                            associated.RemoveAt(i);
                            injected.Remove(s);
                            if (ws._vwp._externalGuidingSystems != null && !string.IsNullOrEmpty(r._systemName))
                            {
                                ws._vwp._externalGuidingSystems.Remove(r._systemName);
                                injectedExt?.Remove(r._systemName);
                            }
                        }
                    }
                }

                // Register compatible consort targeting/CW illuminators in ap._associatedSensors
                // so ExtendedGuidance's Illuminator runtime can discover and reserve them on Forward Pass.
                if (ap._associatedSensors != null)
                {
                    var ammoInjected = _injectedAmmoSensors.GetOrCreateValue(ap);
                    ammoInjected.RemoveWhere(s =>
                    {
                        if (s is not SensorSystemRadar sr || sr._baseObject == null || sr._baseObject.IsDestroyed || sr.Inoperable.Value)
                        {
                            ap._associatedSensors.Remove(s);
                            return true;
                        }
                        return false;
                    });

                    foreach (var s in injected)
                    {
                        if (s is not SensorSystemRadar r) continue;
                        if (r._baseObject == null || r._baseObject.IsDestroyed || r.Inoperable.Value) continue;
                        if (IsSyntheticDatalinkSensor(r) || IsAwacsRadar(r)) continue;

                        SarhIllumFamily rFam = GetSarhFamily(r);
                        if (r._type != SensorSystemRadar.RadarType.Targeting && rFam == SarhIllumFamily.None) continue;
                        if (targetFam != SarhIllumFamily.None && rFam != targetFam) continue;

                        if (!ap._associatedSensors.Contains(r))
                        {
                            ap._associatedSensors.Add(r);
                            ammoInjected.Add(r);
                        }

                        if (ap._compatibleGuidanceSystems != null
                            && ap._compatibleGuidanceSystems.Count > 0
                            && !string.IsNullOrEmpty(r._systemName)
                            && !ap._compatibleGuidanceSystems.Contains(r._systemName))
                        {
                            ap._compatibleGuidanceSystems.Add(r._systemName);
                        }
                    }
                }
            }
        }

        private static void GetLauncherGuidanceNeeds(WeaponSystem ws, out bool needsSarh, out bool needsMidCourse)
        {
            needsSarh = false;
            needsMidCourse = false;
            if (ws?._vwp == null) return;

            InspectAmmoGuidanceNeeds(ws._ammoForEngage?._ap, ref needsSarh, ref needsMidCourse);
            InspectAmmoGuidanceNeeds(ws._ammunitionOnWeapon?._ap, ref needsSarh, ref needsMidCourse);

            var magList = ws._vwp._associatedMagazine?._ammunitionList;
            if (magList != null)
            {
                for (int i = 0; i < magList.Count; i++)
                {
                    InspectAmmoGuidanceNeeds(magList[i]?._ap, ref needsSarh, ref needsMidCourse);
                }
            }

            if (ws is WeaponSystemHardpoint hp && hp._weapons != null)
            {
                for (int i = 0; i < hp._weapons.Count; i++)
                {
                    InspectAmmoGuidanceNeeds(hp._weapons[i]?._ap, ref needsSarh, ref needsMidCourse);
                }
            }
        }

        private static void InspectAmmoGuidanceNeeds(AmmunitionParameters ap, ref bool needsSarh, ref bool needsMidCourse)
        {
            if (ap == null) return;
            if (IsSarhMissile(ap))
                needsSarh = true;

            if (ap._midCourseCorrection != null && ap._midCourseCorrection.AllowsLauncherGuidance)
                needsMidCourse = true;
        }

        public static bool IsCecMultifunctionRadar(SensorSystemRadar r)
        {
            if (r == null) return false;
            return GetRadarProfile(r).IsMfr;
        }

        private static void EnsureMidCourseChannels(SensorSystemRadar r)
        {
            if (r == null) return;
            if (r._targetChannels <= 0) r._targetChannels = 32;
            if (r._weaponChannels <= 0) r._weaponChannels = 32;
        }

        // =========================================================================================
        // 6. SITUATIONAL AWARENESS & TELEMETRY
        // =========================================================================================

        public static bool CanSensorGuideAmmo(SensorSystem sensor, AmmunitionParameters ap)
        {
            if (sensor == null || ap == null) return true;

            var mp = GetMissileProfile(ap);
            bool isSarh = IsSarhMissile(ap);

            if (IsAwacsRadar(sensor))
            {
                if (isSarh) return false;
                if (mp.CecTier == CecTier.Tier0 || mp.CecTier == CecTier.Tier3) return false;
                return ap._midCourseCorrection != null && ap._midCourseCorrection.AllowsLauncherGuidance;
            }

            if (IsSyntheticDatalinkSensor(sensor))
            {
                if (!EgBridge.IsActive || mp.CecTier == CecTier.Tier0 || mp.CecTier == CecTier.Tier3)
                    return false;
                return ap._midCourseCorrection != null && ap._midCourseCorrection.IsDatalink;
            }

            if (IsPureCwDishIlluminator(sensor._systemName))
            {
                if (!isSarh)
                    return false;
                if (sensor is SensorSystemRadar cwRadar && mp.RequiredSarhFamily != SarhIllumFamily.None)
                {
                    if (GetSarhFamily(cwRadar) != mp.RequiredSarhFamily)
                        return false;
                }
                return true;
            }

            if (isSarh && sensor is SensorSystemRadar sarhRadar && mp.RequiredSarhFamily != SarhIllumFamily.None)
            {
                SarhIllumFamily rFam = GetSarhFamily(sarhRadar);
                if (rFam != SarhIllumFamily.None && rFam != mp.RequiredSarhFamily)
                    return false;
            }

            return true;
        }

        private static bool PlatformHasDedicatedMidCourseSensor(ObjectBase platform, List<SensorSystem> associated)
        {
            if (platform?._obp?._sensorSystems == null) return false;
            var systems = platform._obp._sensorSystems;
            for (int i = 0; i < systems.Count; i++)
            {
                if (systems[i] is SensorSystemRadar r)
                {
                    if (r.Inoperable.Value || r._radar?._rp == null) continue;
                    if (IsSyntheticDatalinkSensor(r) || GetRadarProfile(r).IsMfr)
                    {
                        if (associated == null || associated.Contains(r)) return true;
                    }
                }
            }
            return false;
        }

        public static bool HasCooperativeAirTrack(ObjectBase shooter, ObjectBase target, ObjectBase.SensorRangeOptions options = default)
        {
            if (shooter == null || shooter.IsDestroyed || target == null || target.IsDestroyed) return false;
            if (!target.isAirTarget()) return false;

            var tf = shooter._taskforce;
            if (tf == null) return false;

            var shooterProfile = GetPlatformProfile(shooter);
            if (shooterProfile.CecTier == CecTier.Tier0) return false;

            float shooterAntennaY = DatalinkMastHeight(shooter, requireRadarOn: false);
            if (shooterAntennaY < 0f) return false;

            var shooterPos = shooter.transform.position;

            bool CheckCoop(ObjectBase coop)
            {
                if (coop == null || coop == shooter || coop.IsDestroyed) return false;
                InteractionTier tier = EvaluateInteraction(shooter, coop);
                if (tier == InteractionTier.Incompatible) return false;

                float linkDist = HorizontalDistance(shooterPos, coop.transform.position);
                if (!ArePlatformsInDatalinkReach(shooter, coop, linkDist)) return false;

                return CooperatorSeesTarget(coop, target);
            }

            if (tf._taskforceAircraft != null)
            {
                for (int i = 0; i < tf._taskforceAircraft.Count; i++)
                    if (CheckCoop(tf._taskforceAircraft[i])) return true;
            }
            if (tf._taskforceHelicopters != null)
            {
                for (int i = 0; i < tf._taskforceHelicopters.Count; i++)
                    if (CheckCoop(tf._taskforceHelicopters[i])) return true;
            }
            if (tf._taskforceVessels != null)
            {
                for (int i = 0; i < tf._taskforceVessels.Count; i++)
                    if (CheckCoop(tf._taskforceVessels[i])) return true;
            }

            return false;
        }

        public static bool HasCooperativeTrack(ObjectBase shooter, ObjectBase target)
        {
            if (shooter == null || shooter.IsDestroyed || target == null || target.IsDestroyed) return false;
            return HasCooperativeAirTrack(shooter, target);
        }

        public static bool HasRemoteFcrCoverage(ObjectBase shooter, AmmunitionParameters ap, ObjectBase target)
        {
            return CanPerformLaunchOnRemote(shooter, ap, target);
        }

        private static bool CooperatorSeesTarget(ObjectBase cooperator, ObjectBase target)
        {
            if (cooperator?._obp?._sensorSystems == null || target == null || target.IsDestroyed)
                return false;

            var systems = cooperator._obp._sensorSystems;
            float distToTarget = HorizontalDistance(cooperator.transform.position, target.transform.position);

            for (int i = 0; i < systems.Count; i++)
            {
                if (!(systems[i] is SensorSystemRadar r)) continue;
                if (!UsableGuidanceRadar(r)) continue;
                if (target.isAirTarget() && !r.CanSeeAirTargets) continue;

                if (RadarCanSee(r, target))
                {
                    var rangeData = r.GetRange(target);
                    if (rangeData.Distance > 0f && distToTarget <= rangeData.Distance)
                        return true;
                }
            }
            return false;
        }

        // =========================================================================================
        // 7. HOVER TELEMETRY & UI FORMATTERS
        // =========================================================================================

        public static ObjectBase FindBestTrackingCooperator(ObjectBase shooter, ObjectBase target)
        {
            if (shooter == null || target == null || target.IsDestroyed) return null;
            var tf = shooter._taskforce ?? Globals._playerTaskforce;
            if (tf == null) return null;

            ObjectBase best = null;
            float bestDistSq = float.MaxValue;

            void Check(ObjectBase coop)
            {
                if (coop == null || coop == shooter || coop.IsDestroyed) return;
                InteractionTier tier = EvaluateInteraction(shooter, coop);
                if (tier == InteractionTier.Incompatible) return;

                if (!shooter.IsDestroyed)
                {
                    float linkDist = HorizontalDistance(shooter.transform.position, coop.transform.position);
                    if (!ArePlatformsInDatalinkReach(shooter, coop, linkDist)) return;
                }

                if (CooperatorSeesTarget(coop, target))
                {
                    float distSq = (coop.transform.position - target.transform.position).sqrMagnitude;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = coop;
                    }
                }
            }

            if (tf._taskforceAircraft != null)
                for (int i = 0; i < tf._taskforceAircraft.Count; i++) Check(tf._taskforceAircraft[i]);

            if (tf._taskforceHelicopters != null)
                for (int i = 0; i < tf._taskforceHelicopters.Count; i++) Check(tf._taskforceHelicopters[i]);

            if (tf._taskforceVessels != null)
                for (int i = 0; i < tf._taskforceVessels.Count; i++) Check(tf._taskforceVessels[i]);

            return best;
        }

        public static SensorSystemRadar FindConsortIlluminatorForTarget(ObjectBase shooter, AmmunitionParameters ap, ObjectBase target)
        {
            if (shooter == null || ap == null || target == null || target.IsDestroyed) return null;
            var tf = shooter._taskforce;
            if (tf == null) return null;

            SarhIllumFamily reqFam = GetMissileProfile(ap).RequiredSarhFamily;
            if (reqFam == SarhIllumFamily.None) reqFam = GetRequiredSarhFamily(shooter);

            SensorSystemRadar best = null;
            float bestDistSq = float.MaxValue;

            void Check(ObjectBase consort)
            {
                if (consort == null || consort == shooter || consort.IsDestroyed) return;
                InteractionTier tier = EvaluateInteraction(shooter, consort);
                if (tier != InteractionTier.Tier1_FullCec && tier != InteractionTier.SatcomGlobal && tier != InteractionTier.Tier1_IntraFlightOnly)
                    return;

                float linkDist = HorizontalDistance(shooter.transform.position, consort.transform.position);
                if (!ArePlatformsInDatalinkReach(shooter, consort, linkDist)) return;

                var systems = consort._obp?._sensorSystems;
                if (systems == null) return;

                for (int i = 0; i < systems.Count; i++)
                {
                    if (!(systems[i] is SensorSystemRadar r)) continue;
                    if (r.Inoperable.Value || r._radar?._rp == null) continue;
                    if (IsSyntheticDatalinkSensor(r) || IsAwacsRadar(r)) continue;

                    SarhIllumFamily rFam = GetSarhFamily(r);
                    if (r._type != SensorSystemRadar.RadarType.Targeting && rFam == SarhIllumFamily.None) continue;
                    if (reqFam != SarhIllumFamily.None && rFam != reqFam) continue;

                    if (GetRadarProfile(r).IsMfr)
                        EnsureMidCourseChannels(r);

                    if (!r.HasFreeChannels()) continue;
                    bool canSee = RadarCanSee(r, target)
                        && (r.IsOn.Value || !IsPureCwDishIlluminator(r._systemName) || HasLocalRadarSight(consort, target));
                    if (!canSee) continue;

                    float distSq = (r._mountObject.transform.position - target.transform.position).sqrMagnitude;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = r;
                    }
                }
            }

            if (tf._taskforceVessels != null)
                for (int i = 0; i < tf._taskforceVessels.Count; i++) Check(tf._taskforceVessels[i]);

            if (tf._taskforceAircraft != null)
                for (int i = 0; i < tf._taskforceAircraft.Count; i++) Check(tf._taskforceAircraft[i]);

            return best;
        }

        private static string FormatSingleDatalinkStandard(DatalinkStandard s)
        {
            return s switch
            {
                DatalinkStandard.Usg2Cec => "USG-2/3 CEC",
                DatalinkStandard.Ttnt => "TTNT",
                DatalinkStandard.Jsidls => "JSIDLS",
                DatalinkStandard.Hn900 => "HN-900",
                DatalinkStandard.RuFortTvm => "Fort-TVM",
                DatalinkStandard.Tks2Apd518 => "TKS-2 / APD-518",
                DatalinkStandard.RuNavalTactical => "RU Naval Link",
                DatalinkStandard.Ifdl => "IFDL",
                DatalinkStandard.Madl => "MADL",
                DatalinkStandard.Link16 => "Link 16",
                DatalinkStandard.Link11 => "Link 11",
                DatalinkStandard.SatcomUplink => "SATCOM",
                _ => null
            };
        }

        public static string GetDatalinkProtocolDisplayName(DatalinkStandard d1, DatalinkStandard d2)
        {
            DatalinkStandard shared = d1 & d2;

            if ((shared & DatalinkStandard.Usg2Cec) != 0) return "USG-2/3 CEC";
            if ((shared & DatalinkStandard.Ttnt) != 0) return "TTNT";
            if ((shared & DatalinkStandard.Jsidls) != 0) return "JSIDLS";
            if ((shared & DatalinkStandard.Hn900) != 0) return "HN-900";
            if ((shared & DatalinkStandard.RuFortTvm) != 0) return "Fort-TVM";
            if ((shared & DatalinkStandard.Tks2Apd518) != 0) return "TKS-2 / APD-518";
            if ((shared & DatalinkStandard.RuNavalTactical) != 0) return "RU Naval Link";
            if ((shared & DatalinkStandard.Madl) != 0) return "MADL";
            if ((shared & DatalinkStandard.Ifdl) != 0) return "IFDL";
            if ((shared & DatalinkStandard.Link16) != 0) return "Link 16";
            if ((shared & DatalinkStandard.Link11) != 0) return "Link 11";
            if ((shared & DatalinkStandard.SatcomUplink) != 0) return "SATCOM";

            DatalinkStandard combined = d1 | d2;
            if ((combined & (DatalinkStandard.Ifdl | DatalinkStandard.Madl)) != 0
                && (combined & (DatalinkStandard.Usg2Cec | DatalinkStandard.Ttnt)) != 0)
                return "MADL/IFDL GW";
            if ((combined & DatalinkStandard.Usg2Cec) != 0 && (combined & DatalinkStandard.Ttnt) != 0)
                return "USG-2/3 / TTNT";
            if ((combined & DatalinkStandard.Jsidls) != 0 && (combined & DatalinkStandard.Hn900) != 0)
                return "JSIDLS / HN-900";
            if ((combined & DatalinkStandard.RuFortTvm) != 0 && (combined & DatalinkStandard.RuNavalTactical) != 0)
                return "Fort-TVM / RU Link";

            foreach (DatalinkStandard s in CecDataTables.GetIndividualStandards(combined))
            {
                string label = FormatSingleDatalinkStandard(s);
                if (label != null) return label;
            }

            return "Datalink";
        }

        public static string GetDatalinkProtocolDisplayName(ObjectBase u1, ObjectBase u2)
        {
            if (u1 == null && u2 == null) return "Datalink";
            if (u1 == null)
            {
                var p2 = GetPlatformProfile(u2);
                return GetDatalinkProtocolDisplayName(p2.Datalinks, p2.Datalinks);
            }
            if (u2 == null || u1 == u2)
            {
                var p1 = GetPlatformProfile(u1);
                return GetDatalinkProtocolDisplayName(p1.Datalinks, p1.Datalinks);
            }

            ResolveBestDatalinkPair(u1, u2, out DatalinkStandard s1, out DatalinkStandard s2, out bool usedGateway);
            if (s1 != DatalinkStandard.None && s2 != DatalinkStandard.None)
            {
                string n1 = FormatSingleDatalinkStandard(s1);
                string n2 = FormatSingleDatalinkStandard(s2);
                if (s1 == s2 && n1 != null)
                    return usedGateway ? $"{n1} (GW)" : n1;
                if (n1 != null && n2 != null)
                    return usedGateway ? $"{n1} / {n2} (GW)" : $"{n1} / {n2}";
            }

            var prof1 = GetPlatformProfile(u1);
            var prof2 = GetPlatformProfile(u2);
            return GetDatalinkProtocolDisplayName(prof1.Datalinks, prof2.Datalinks);
        }

        public static string GetHoverTelemetryTag(ObjectBase shooter, AmmunitionParameters ap, ObjectBase target)
        {
            if (shooter == null || ap == null || target == null || target.IsDestroyed)
                return null;

            var shooterProfile = GetPlatformProfile(shooter);
            var missileProfile = GetMissileProfile(ap);

            ObjectBase tracker = FindBestTrackingCooperator(shooter, target);
            if (tracker == null)
                return null;

            var trackerProfile = GetPlatformProfile(tracker);
            InteractionTier interaction = EvaluateInteraction(shooter, tracker);
            string protocol = GetDatalinkProtocolDisplayName(shooter, tracker);

            bool isSarh = IsSarhMissile(ap);

            // Tier 1 Forward Pass (SARH, e.g. SM-2MR)
            if (isSarh)
            {
                SensorSystemRadar illum = FindConsortIlluminatorForTarget(shooter, ap, target);
                if (illum != null)
                {
                    string shipName = illum._baseObject?.getName() ?? "Consort";
                    string radarName = illum._systemName ?? "CW";
                    return $"<Run Foreground=\"#81C784\">Forward Pass: {shipName} [{radarName}]</Run><LineBreak/>";
                }

                return $"<Run Foreground=\"#FFF176\">Cued via {protocol} (Local radar lock required to fire)</Run><LineBreak/>";
            }

            // Tier 1 Blind OTH / EMCON (ARH, e.g. SM-6)
            if (shooterProfile.CecTier == CecTier.Tier1 &&
                trackerProfile.CecTier == CecTier.Tier1 &&
                (interaction == InteractionTier.Tier1_FullCec || interaction == InteractionTier.SatcomGlobal || interaction == InteractionTier.Tier1_IntraFlightOnly) &&
                missileProfile.AllowLaunchOnRemote)
            {
                string guiderName = tracker.getName();
                return $"<Run Foreground=\"#4FC3F7\">CEC Track: {guiderName} [{protocol} | Tier 1]</Run><LineBreak/>";
            }

            // Tier 2 Cue Only
            if (interaction == InteractionTier.Tier2_CueOnly ||
                interaction == InteractionTier.Tier2_MidcourseRescue ||
                shooterProfile.CecTier >= CecTier.Tier2 ||
                missileProfile.CecTier >= CecTier.Tier2)
            {
                return $"<Run Foreground=\"#FFF176\">Cued via {protocol} (Local radar lock required to fire)</Run><LineBreak/>";
            }

            return null;
        }

        public static string FormatMissileDatalinkUiString(Missile m, SensorSystemRadar activeRadar, ObjectBase guider, ObjectBase launcher)
        {
            if (guider == null) return string.Empty;

            string guiderName = Patches.DatalinkUiPatch.FormatCleanUnitName(guider);
            var guiderProfile = GetPlatformProfile(guider);

            bool isRemote = launcher != null && guider != launcher;
            bool isAwacs = (guider is Aircraft || guider is Helicopter) && (activeRadar != null && IsAwacsRadar(activeRadar));
            bool isSynth = activeRadar != null && IsSyntheticDatalinkSensor(activeRadar);
            bool isSarh = IsSarhMissile(m?._ap);
            bool isRescue = m != null && (m.ConnectionLost.Value || (launcher != null && (launcher.IsDestroyed || IsPlatformInAirRadarEmcon(launcher))));

            InteractionTier interaction = isRemote
                ? EvaluateInteraction(launcher, guider)
                : (guiderProfile.CecTier == CecTier.Tier1 ? InteractionTier.Tier1_FullCec : InteractionTier.Tier2_MidcourseRescue);

            string baseProto = isRemote
                ? GetDatalinkProtocolDisplayName(launcher, guider)
                : GetDatalinkProtocolDisplayName(guider, guider);

            string protocol;
            string tierStr;

            if (guiderProfile.CecTier == CecTier.Tier1
                && (interaction == InteractionTier.Tier1_FullCec
                    || interaction == InteractionTier.Tier1_IntraFlightOnly
                    || interaction == InteractionTier.SatcomGlobal))
            {
                protocol = baseProto;
                if (isAwacs)
                    tierStr = "Tier 1 AWACS";
                else if (isSarh && !isSynth)
                    tierStr = "Tier 1 SARH";
                else
                    tierStr = "Tier 1";
            }
            else if (guiderProfile.CecTier <= CecTier.Tier2)
            {
                protocol = (isRemote && isRescue) ? $"{baseProto} Rescue" : baseProto;
                tierStr = isAwacs ? "Tier 2 AWACS" : "Tier 2";
            }
            else
            {
                protocol = isRemote ? baseProto : "Local FCR";
                tierStr = "Tier 3";
            }

            int mapId = Patches.DatalinkUiPatch.GetUnitContactId(guider);
            string idTag = mapId > 0 ? $"{mapId} " : "";
            return $"{idTag}{guiderName} [{protocol} | {tierStr}]";
        }
    }
}
