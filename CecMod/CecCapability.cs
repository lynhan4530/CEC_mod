using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SeaPower;
using UnityEngine;

namespace CecMod
{
    /// <summary>
    /// Real-world Cooperative Engagement Capability (CEC), NIFC-CA, NATO APAR/PAAMS,
    /// Soviet/Russian Fort/Poliment-Redut/Zaslon, and PLA/PLAN "A-Shoot-B-Guide" network engine
    /// for Sea Power 0.8.7+.
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

        /// <summary>
        /// RF / waveform compatibility families for Semi-Active Radar Homing (SARH) terminal illumination.
        /// SARH seekers require a physically compatible CW / TVM / command-SARH fire-control radar and
        /// can never be terminally illuminated by a synthetic datalink transceiver or UHF/L/S-band AWACS.
        /// </summary>
        public enum SarhIllumFamily
        {
            None = 0,
            /// <summary>US / NATO X-band CW ICWI &amp; dish illuminators (SM-1/SM-2, RIM-7/ESSM Blk 1).</summary>
            UsNatoXBandCw = 1,
            /// <summary>Soviet / Russian S-300F/FM Fort TVM / X-band phased-array FCR (5V55RM, 48N6 series).</summary>
            RuFortTvm = 2,
            /// <summary>Soviet / Russian Uragan / Shtil-1 CW illuminators (9M38, 9M317).</summary>
            RuShtilCw = 3,
            /// <summary>PLAN HHQ-9 / HQ-9 S/C/X-band Track-via-Missile / SARH+RC multifunction arrays.</summary>
            PlanHhq9 = 4,
            /// <summary>PLAN HHQ-16 / HQ-16A X-band CW illuminators (LJG-316 / Front Dome).</summary>
            PlanHhq16 = 5,
            /// <summary>MiG-31 Zaslon PESA cooperative SARH illumination (R-33 / AA-9 Amos).</summary>
            RuZaslonPesa = 6
        }

        /// <summary>Surface-to-surface line-of-sight DDS/datalink cap (~60 nmi; radio horizon is typically 25-40 nmi).</summary>
        public const float SurfaceNetRangeCapUnits = 60f * 27.559496f;

        /// <summary>Airborne (AWACS / fighter) line-of-sight datalink cap (~300 nmi; 4/3-earth horizon at 30,000 ft is ~225 nmi).</summary>
        public const float AirborneNetRangeCapUnits = 300f * 27.559496f;

        private static readonly ConditionalWeakTable<WeaponSystem, HashSet<SensorSystem>> _injectedSensors =
            new ConditionalWeakTable<WeaponSystem, HashSet<SensorSystem>>();

        private static readonly ConditionalWeakTable<WeaponSystem, HashSet<string>> _injectedExternalNames =
            new ConditionalWeakTable<WeaponSystem, HashSet<string>>();

        // =========================================================================================
        // 1. SYNTHETIC DATALINK TRANSCEIVERS (Mode=RadioCommand combat system / uplink proxies)
        //    - CAN guide Active Radar Homing (ARH) / IIR mid-course datalink weapons
        //    - CANNOT terminally illuminate Semi-Active Radar Homing (SARH) weapons!
        // =========================================================================================
        private static readonly HashSet<string> SyntheticDatalinkSensors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SYR-1", "SYR-1_NTU", "SYS-1", "SYS-2", "SPW-2",
            "eu_AEGIS_BL5", "eu_AEGIS_BL9", "eu_AEGIS_BL10",
            "eu_2T_Datalink", "eu_UYK-43", "eu_UYK-44", "eu_UYK-44B",
            "eu_tacticalpcgen1", "eu_tacticalpcgen2", "eu_tacticalpcgen3",
            "eu_JUWL", "eu_aster_datalink",
            "eu_EMPAR_2", "eu_KRONOS_guidance", "eu_MFRA_guidance", "eu_SAMPSON_guidance", "EMPAR_2",
            "spa_SCOMBA_v1", "DD115_TacticalDatalink", "Mk92_Mod12_MCG",
            "Link-16_Receiver", "eu_GPS_Receiver", "F16_GPS_Link", "IAF_GPS_Link", "GPS_Link", "WZ-8_Datalink"
        };

        // Pure CW illuminator dishes that only perform terminal SARH illumination (no mid-course ARH datalink)
        // when the platform also has a dedicated datalink/MFR sensor in _associatedSensors.
        private static readonly HashSet<string> PureCwDishIlluminators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SPG-62", "eu_SPG-62", "SPG-55", "SPG-55B", "SPG-51", "SPG-51D",
            "MK-95", "MK-115", "MK92_FCS", "MK92_Mod6_FCS", "MK92_Mod12_FCS",
            "eu_CEROS-200_CWI", "dk_iver_CEROS-200_CWI",
            "eu_stir120", "eu_stir180", "eu_stir240", "Stir", "STIR-180", "STIR-240",
            "Front_Dome", "rfn_mr90", "LJG-316"
        };

        // =========================================================================================
        // 2. AWACS / AEW&C RADARS (Track providers & ARH mid-course datalink uplinks)
        // =========================================================================================
        private static readonly Dictionary<string, CecNetwork> AwacsRadars = new Dictionary<string, CecNetwork>(StringComparer.OrdinalIgnoreCase)
        {
            // US / Allied / NATO AEW&C (E-2C/D Hawkeye, E-3 Sentry, E-7A Wedgetail, Saab GlobalEye)
            { "AN/APS-125", CecNetwork.WesternJoint },
            { "AN/APS-138", CecNetwork.WesternJoint },
            { "AN/APS-145", CecNetwork.WesternJoint },
            { "eu_AN/APS-145", CecNetwork.WesternJoint },
            { "AN/APY-9", CecNetwork.WesternJoint },
            { "AN/APY-1", CecNetwork.WesternJoint },
            { "AN/APY-2", CecNetwork.WesternJoint },
            { "AEWC_AN_APY_2", CecNetwork.WesternJoint },
            { "MESA", CecNetwork.WesternJoint },
            { "AEWC_MESA", CecNetwork.WesternJoint },
            { "Erieye_ER", CecNetwork.WesternJoint },
            { "GE_ERIEYE_ER", CecNetwork.WesternJoint },
            { "Searchwater", CecNetwork.WesternJoint },
            { "Searchwater_asac", CecNetwork.WesternJoint },
            { "Thorn EMI Searchwater", CecNetwork.WesternJoint },

            // Soviet / Russian AEW&C (A-50 Shmel, A-50U Shmel-M, Ka-31 E-801E Oko)
            { "N035_Shmel", CecNetwork.RuJoint },
            { "N035M_Shmel-M", CecNetwork.RuJoint },
            { "E-801E", CecNetwork.RuNaval | CecNetwork.PlanNaval },

            // PLA / PLAN AEW&C (KJ-500, KJ-600, KJ-2000, KJ-3000, Z-18J)
            { "KLC-500", CecNetwork.PlaJoint },
            { "KLC-600", CecNetwork.PlaJoint },
            { "KLC-600_low-band_mode", CecNetwork.PlaJoint },
            { "KJ3000_Aircraft_Radar", CecNetwork.PlaJoint },
            { "KLC-4", CecNetwork.PlaJoint },
            { "Z18_AEW_Radar", CecNetwork.PlaJoint }
        };

        // =========================================================================================
        // 3. SARH TERMINAL ILLUMINATOR FAMILIES (Radar _systemName -> SarhIllumFamily)
        // =========================================================================================
        private static readonly Dictionary<string, SarhIllumFamily> RadarSarhFamilies = new Dictionary<string, SarhIllumFamily>(StringComparer.OrdinalIgnoreCase)
        {
            // US / NATO / Allied X-Band CW & ICWI Illuminators
            { "SPG-62", SarhIllumFamily.UsNatoXBandCw },
            { "eu_SPG-62", SarhIllumFamily.UsNatoXBandCw },
            { "SPG-55", SarhIllumFamily.UsNatoXBandCw },
            { "SPG-55B", SarhIllumFamily.UsNatoXBandCw },
            { "SPG-51", SarhIllumFamily.UsNatoXBandCw },
            { "SPG-51D", SarhIllumFamily.UsNatoXBandCw },
            { "SPG-60", SarhIllumFamily.UsNatoXBandCw },
            { "MK92_FCS", SarhIllumFamily.UsNatoXBandCw },
            { "MK92_Mod6_FCS", SarhIllumFamily.UsNatoXBandCw },
            { "MK92_Mod12_FCS", SarhIllumFamily.UsNatoXBandCw },
            { "MK-95", SarhIllumFamily.UsNatoXBandCw },
            { "MK-115", SarhIllumFamily.UsNatoXBandCw },
            { "eu_apar", SarhIllumFamily.UsNatoXBandCw },
            { "eu_apar_block2", SarhIllumFamily.UsNatoXBandCw },
            { "eu_stir120", SarhIllumFamily.UsNatoXBandCw },
            { "eu_stir180", SarhIllumFamily.UsNatoXBandCw },
            { "eu_stir240", SarhIllumFamily.UsNatoXBandCw },
            { "Stir", SarhIllumFamily.UsNatoXBandCw },
            { "STIR-180", SarhIllumFamily.UsNatoXBandCw },
            { "STIR-240", SarhIllumFamily.UsNatoXBandCw },
            { "eu_CEROS-200_CWI", SarhIllumFamily.UsNatoXBandCw },
            { "dk_iver_CEROS-200_CWI", SarhIllumFamily.UsNatoXBandCw },
            { "WM25_FCS", SarhIllumFamily.UsNatoXBandCw },
            { "eu_wm25", SarhIllumFamily.UsNatoXBandCw },
            { "CEAFAR", SarhIllumFamily.UsNatoXBandCw },
            { "CEAFAR2_X", SarhIllumFamily.UsNatoXBandCw },
            { "DD115_FCS3A_Guidance", SarhIllumFamily.UsNatoXBandCw },
            { "DD115_FCS3A_Search", SarhIllumFamily.UsNatoXBandCw },
            { "hyuga_FCS3_Guidance", SarhIllumFamily.UsNatoXBandCw },
            { "hyuga_FCS3_Search", SarhIllumFamily.UsNatoXBandCw },
            { "eu_OPY-1", SarhIllumFamily.UsNatoXBandCw },
            { "FFM_Multifunction_Radar_Proxy", SarhIllumFamily.UsNatoXBandCw },

            // Soviet / Russian S-300F/FM Fort / Rif (5V55RM, 48N6 series)
            { "Top_Dome", SarhIllumFamily.RuFortTvm },
            { "30N6E1_FCR", SarhIllumFamily.RuFortTvm },
            { "30N6E2", SarhIllumFamily.RuFortTvm },
            { "rfn_30n6e2", SarhIllumFamily.RuFortTvm },
            { "rfn_92n6e", SarhIllumFamily.RuFortTvm },

            // Soviet / Russian Uragan / Shtil-1 (9M38, 9M317)
            { "Front_Dome", SarhIllumFamily.RuShtilCw },
            { "rfn_mr90", SarhIllumFamily.RuShtilCw },

            // PLAN HHQ-9 / HQ-9 (Type 346 / LJG-346 series)
            { "LJG-346", SarhIllumFamily.PlanHhq9 },
            { "LJG-346A", SarhIllumFamily.PlanHhq9 },
            { "LJG-346B", SarhIllumFamily.PlanHhq9 },
            { "LJQ-346B", SarhIllumFamily.PlanHhq9 },
            { "Type_346", SarhIllumFamily.PlanHhq9 },
            { "HT-233", SarhIllumFamily.PlanHhq9 },
            { "HT-233_AESA", SarhIllumFamily.PlanHhq9 },

            // PLAN HHQ-16 / HQ-16 (LJG-316 / Front Dome)
            { "LJG-316", SarhIllumFamily.PlanHhq16 },

            // Soviet / Russian MiG-31 Zaslon PESA (R-33 / AA-9)
            { "Zaslon", SarhIllumFamily.RuZaslonPesa },
            { "Zaslon-M", SarhIllumFamily.RuZaslonPesa },
            { "Zaslon_Targeting", SarhIllumFamily.RuZaslonPesa },
            { "Zaslon-M_Targeting", SarhIllumFamily.RuZaslonPesa }
        };

        // =========================================================================================
        // 4. RADAR -> CEC NETWORK MEMBERSHIP (Search, Multifunction, FCR, Datalink & Fighter Radars)
        // =========================================================================================
        private static readonly Dictionary<string, CecNetwork> RadarNetworks = new Dictionary<string, CecNetwork>(StringComparer.OrdinalIgnoreCase)
        {
            // --- US / Allied AEGIS, NTU, CEC & NIFC-CA ---
            { "SPY-1A", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "SPY-1B", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "SPY-1D", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "KG_R2_SPY1D", CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "SPY-3", CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "SPY-4", CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1A", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1A(V)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1B", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1B(BMD)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1B(V)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1D", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1D(BMD)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1D(V)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1E", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1F", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1F(V)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-1K", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "eu_SPY-3", CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-4", CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-5", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-6(V1)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-6(V2)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-6(V3)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-6(V4)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-7(V1)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-7(V2)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-7(V3)", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_SPY-7_LRDR", CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "SPS-48", CecNetwork.UsAlliedNaval },
            { "SPS-48E", CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "SPS-49", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "SPS-49V5", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "SPS-49V8", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "SPS-49A(V)1", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "SPS-52", CecNetwork.UsAlliedNaval },
            { "SPG-62", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "eu_SPG-62", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "SPG-55", CecNetwork.UsAlliedNaval },
            { "SPG-55B", CecNetwork.UsAlliedNaval },
            { "SPG-51", CecNetwork.UsAlliedNaval },
            { "SPG-51D", CecNetwork.UsAlliedNaval },
            { "MK92_FCS", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "MK92_Mod6_FCS", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "MK92_Mod12_FCS", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "MK-95", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "MK-115", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "SYR-1", CecNetwork.UsAlliedNaval },
            { "SYR-1_NTU", CecNetwork.UsAlliedNaval },
            { "SYS-1", CecNetwork.UsAlliedNaval },
            { "SYS-2", CecNetwork.UsAlliedNaval },
            { "eu_AEGIS_BL5", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "eu_AEGIS_BL9", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "eu_AEGIS_BL10", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "spa_SCOMBA_v1", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "CEAFAR", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "CEAFAR2_L", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir },
            { "CEAFAR2_X", CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval },
            { "DD115_FCS3A_Search", CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "DD115_FCS3A_Guidance", CecNetwork.UsAlliedNaval },
            { "DD115_TacticalDatalink", CecNetwork.UsAlliedNaval },
            { "hyuga_FCS3_Search", CecNetwork.UsAlliedNaval },
            { "hyuga_FCS3_Guidance", CecNetwork.UsAlliedNaval },
            { "eu_OPY-1", CecNetwork.UsAlliedNaval },
            { "FFM_Multifunction_Radar_Proxy", CecNetwork.UsAlliedNaval },

            // --- NATO / European APAR, PAAMS, Sea Ceptor & Link 16 ---
            { "eu_apar", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "eu_apar_block2", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "eu_smart_l", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_smart_l_mm", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_smart_l_S1850M", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_SMART-L", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_SMART-L_S1850M", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "SMART-L", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "S1850M", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_SAMPSON", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "eu_SAMPSON_guidance", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "EMPAR", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "EMPAR_2", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_EMPAR", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_EMPAR_2", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_MFRA", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_MFRA_guidance", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_KRONOS_c", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_KRONOS_x", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_KRONOS_s", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_KRONOS_l", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_KRONOS_guidance", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "Herakles", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "Herakles+", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_Herakles", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_Herakles+", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "Sea_fire_500", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "eu_Sea-fire-500", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "ARABEL", CecNetwork.NatoEuroNaval },
            { "eu_ARABEL", CecNetwork.NatoEuroNaval },
            { "Artisan3D", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_Type_997", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_TRS-3D_16", CecNetwork.NatoEuroNaval },
            { "eu_TRS-3D_20", CecNetwork.NatoEuroNaval },
            { "eu_TRS-3D_32", CecNetwork.NatoEuroNaval },
            { "eu_TRS-4D_NR", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_TRS-4D_Rot", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_TRS-4D_LR_Rot", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_SeaGiraffe_4A", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_SeaGiraffe_4A_NR", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_SeaGiraffe_AMB_3D", CecNetwork.NatoEuroNaval },
            { "Sea_Giraffe_AMB", CecNetwork.NatoEuroNaval },
            { "SMART-S-MK2", CecNetwork.NatoEuroNaval },
            { "eu_SMART-S_Mk2", CecNetwork.NatoEuroNaval },
            { "eu_sm400", CecNetwork.NatoEuroNaval },
            { "eu_ns100", CecNetwork.NatoEuroNaval },
            { "eu_stir120", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_stir180", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_stir240", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "Stir", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_CEROS-200_CWI", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "dk_iver_CEROS-200_CWI", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_tacticalpcgen1", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_tacticalpcgen2", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_tacticalpcgen3", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_2T_Datalink", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_JUWL", CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval },
            { "eu_aster_datalink", CecNetwork.NatoEuroNaval },

            // --- Soviet / Russian Naval & Air Defense ---
            { "Top_Dome", CecNetwork.RuNaval },
            { "30N6E1_FCR", CecNetwork.RuNaval | CecNetwork.PlanNaval },
            { "30N6E2", CecNetwork.RuNaval },
            { "rfn_30n6e2", CecNetwork.RuNaval },
            { "rfn_92n6e", CecNetwork.RuNaval },
            { "rfn_5p20k", CecNetwork.RuNaval | CecNetwork.RuAirZaslon },
            { "rfn_5p20k_FCR", CecNetwork.RuNaval | CecNetwork.RuAirZaslon },
            { "rfn_5p20k-a", CecNetwork.RuNaval | CecNetwork.RuAirZaslon },
            { "rfn_5p20k-a_FCR", CecNetwork.RuNaval | CecNetwork.RuAirZaslon },
            { "rfn_mf_rlk", CecNetwork.RuNaval | CecNetwork.RuAirZaslon },
            { "rfn_mf_rlk_FCR", CecNetwork.RuNaval | CecNetwork.RuAirZaslon },
            { "rfn_5p27", CecNetwork.RuNaval },
            { "rfn_5p27-0", CecNetwork.RuNaval },
            { "rfn_5p27m", CecNetwork.RuNaval },
            { "rfn_mr750", CecNetwork.RuNaval },
            { "rfn_mr760", CecNetwork.RuNaval },
            { "Top_Pair", CecNetwork.RuNaval },
            { "Top_Steer", CecNetwork.RuNaval },
            { "Top_Plate", CecNetwork.RuNaval | CecNetwork.PlanNaval },
            { "Sky_Watch", CecNetwork.RuNaval },
            { "Fregat_MAM", CecNetwork.RuNaval },
            { "Front_Dome", CecNetwork.RuNaval | CecNetwork.PlanNaval },
            { "rfn_mr90", CecNetwork.RuNaval },
            { "Big_Bird", CecNetwork.RuNaval },
            { "36H6", CecNetwork.RuNaval },
            { "96L6", CecNetwork.RuNaval },

            // --- Chinese PLA / PLAN "A-Shoot-B-Guide" ---
            { "LJG-346", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJG-346A", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJG-346B", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJQ-346B", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "Type_346", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJQ-382", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "Type_382", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJQ-384", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJQ-368", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJQ-36X", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJQ-364", CecNetwork.PlanNaval },
            { "Type_364", CecNetwork.PlanNaval },
            { "LJQ-517A", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJQ-517B", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJQ-518", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJQ-520", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "LJG-316", CecNetwork.PlanNaval },
            { "HT-233", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "HT-233_AESA", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "H-200A", CecNetwork.PlanNaval | CecNetwork.PlaAir },
            { "SLC-14", CecNetwork.PlanNaval | CecNetwork.PlaAir },

            // --- US / NATO Fighter Radars (Link 16 / MADL / IFDL) ---
            { "AN/APG-81", CecNetwork.WesternJoint },
            { "AN/APG-77", CecNetwork.WesternJoint },
            { "AN/APG-77v4", CecNetwork.WesternJoint },
            { "AN/APG-79", CecNetwork.WesternJoint },
            { "AN/APG-79(V)", CecNetwork.WesternJoint },
            { "AN/APG-82(V)1", CecNetwork.WesternJoint },
            { "AN/APG-83", CecNetwork.WesternJoint },
            { "AN/APG-85", CecNetwork.WesternJoint },
            { "AN/APG-68(V)7", CecNetwork.UsNatoAir },
            { "AN/APG-68(V)9", CecNetwork.UsNatoAir },
            { "HAF_APG68_V9", CecNetwork.UsNatoAir },
            { "HAF_APG68_V9_ADV", CecNetwork.UsNatoAir },
            { "Captor-E", CecNetwork.WesternJoint },
            { "Captor-M", CecNetwork.UsNatoAir },
            { "RBE2_AA", CecNetwork.WesternJoint },
            { "PS-05/A Mk4 AESA", CecNetwork.WesternJoint },
            { "burger_aesa_radar", CecNetwork.UsAlliedNaval | CecNetwork.UsNatoAir },
            { "Link-16_Receiver", CecNetwork.WesternJoint },

            // --- Soviet / Russian Fighter Radars (Zaslon APD-518 / Irbis-E / Byelka) ---
            { "Zaslon", CecNetwork.RuJoint },
            { "Zaslon-M", CecNetwork.RuJoint },
            { "Zaslon_Targeting", CecNetwork.RuJoint },
            { "Zaslon-M_Targeting", CecNetwork.RuJoint },
            { "Irbis-E", CecNetwork.RuJoint | CecNetwork.PlaAir },
            { "N036-1-01", CecNetwork.RuJoint },
            { "N011M", CecNetwork.RuJoint },
            { "N001VE", CecNetwork.RuJoint | CecNetwork.PlaAir },

            // --- PLA Fighter Radars (J-20, J-16, J-15T/D, J-10C, J-35) ---
            { "KLJ-5", CecNetwork.PlaJoint },
            { "KLJ-10A", CecNetwork.PlaJoint },
            { "KLJ-11A", CecNetwork.PlaJoint },
            { "KLJ-15", CecNetwork.PlaJoint },
            { "KLJ-16", CecNetwork.PlaJoint },
            { "KLJ-35", CecNetwork.PlaJoint },
            { "1893", CecNetwork.PlaJoint },
            { "1893-A", CecNetwork.PlaJoint },
            { "J-11BG_AESA", CecNetwork.PlaJoint },
            { "1493", CecNetwork.PlaAir }
        };

        static CecCapability()
        {
            foreach (var kv in AwacsRadars)
            {
                if (!RadarNetworks.ContainsKey(kv.Key))
                    RadarNetworks[kv.Key] = kv.Value;
            }
        }

        // =========================================================================================
        // 5. CORE HELPERS: Sensor Classification & Compatibility
        // =========================================================================================

        /// <summary>True if the sensor is a synthetic mid-course datalink proxy that cannot provide CW SARH illumination.</summary>
        public static bool IsSyntheticDatalinkSensor(SensorSystem s)
        {
            if (s == null || string.IsNullOrEmpty(s._systemName)) return false;
            return SyntheticDatalinkSensors.Contains(s._systemName);
        }

        /// <summary>True if the sensor is an airborne AWACS / AEW&amp;C surveillance/datalink radar.</summary>
        public static bool IsAwacsRadar(SensorSystem s)
        {
            if (s == null || string.IsNullOrEmpty(s._systemName)) return false;
            return AwacsRadars.ContainsKey(s._systemName);
        }

        /// <summary>
        /// Evaluates whether <paramref name="sensor"/> can guide <paramref name="ap"/> on its own platform,
        /// fixing Euromod's synthetic datalink (`eu_AEGIS_BL9`) channel-bypass bug when ExtendedGuidance.dll is disabled.
        /// </summary>
        public static bool CanSensorGuideAmmo(SensorSystem sensor, AmmunitionParameters ap)
        {
            if (sensor == null || ap == null) return true;

            // Rule 1: A synthetic datalink transceiver (eu_AEGIS_BL9, SYR-1, spa_SCOMBA_v1, etc.) or AWACS radar
            // can NEVER terminally illuminate a Semi-Active Radar Homing (SARH) missile.
            if (ap._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
            {
                if (IsSyntheticDatalinkSensor(sensor) || IsAwacsRadar(sensor))
                    return false;
            }

            // Rule 2: When firing an Active Radar Homing (ARH) or IIR missile with mid-course datalink
            // (SM-6, SM-2 Blk IIIC, ESSM Blk 2, Aster 15/30), do NOT waste a pure X-band CW dish illuminator
            // (SPG-62, STIR, CEROS-200) if the launcher's associated sensors already include an operational
            // synthetic datalink or multifunction radar on the same ship.
            if ((ap._guidanceType == AmmunitionParameters.GuidanceType.ActiveRadarHoming ||
                 ap._guidanceType == AmmunitionParameters.GuidanceType.IRHoming) &&
                ap._midCourseCorrection != null && ap._midCourseCorrection.AllowsLauncherGuidance &&
                !string.IsNullOrEmpty(sensor._systemName) &&
                PureCwDishIlluminators.Contains(sensor._systemName))
            {
                if (PlatformHasDedicatedMidCourseSensor(sensor._baseObject, ap._associatedSensors))
                    return false;
            }

            return true;
        }

        private static bool PlatformHasDedicatedMidCourseSensor(ObjectBase platform, List<SensorSystem> associated)
        {
            if (platform == null || associated == null) return false;
            for (int i = 0; i < associated.Count; i++)
            {
                var s = associated[i];
                if (s != null && s._baseObject == platform && !s.Inoperable.Value &&
                    !string.IsNullOrEmpty(s._systemName) && !PureCwDishIlluminators.Contains(s._systemName) &&
                    (IsSyntheticDatalinkSensor(s) || s is SensorSystemRadar))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Returns the CEC network mask supported by a sensor system.</summary>
        public static CecNetwork GetSensorNetwork(SensorSystem s)
        {
            if (s == null || string.IsNullOrEmpty(s._systemName)) return CecNetwork.None;
            if (RadarNetworks.TryGetValue(s._systemName, out var net))
                return net;

            // Prefix fallback for modded variants
            string name = s._systemName;
            if (name.StartsWith("eu_SPY-", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("SPY-", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("SPG-", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("eu_AEGIS_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("CEAFAR", StringComparison.OrdinalIgnoreCase))
                return CecNetwork.UsAlliedNaval | CecNetwork.NatoEuroNaval | CecNetwork.UsNatoAir;

            if (name.StartsWith("AN/APG-7", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("AN/APG-8", StringComparison.OrdinalIgnoreCase))
                return CecNetwork.WesternJoint;

            if (name.StartsWith("eu_apar", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("eu_stir", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("eu_SAMPSON", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("eu_EMPAR", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("eu_KRONOS", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("eu_Herakles", StringComparison.OrdinalIgnoreCase))
                return CecNetwork.NatoEuroNaval | CecNetwork.UsAlliedNaval;

            if (name.StartsWith("rfn_5p20", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("rfn_30n6", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("rfn_mf_rlk", StringComparison.OrdinalIgnoreCase))
                return CecNetwork.RuNaval | CecNetwork.RuAirZaslon;

            if (name.StartsWith("LJG-346", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("LJQ-346", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Type346", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("KLJ-", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("KLC-", StringComparison.OrdinalIgnoreCase))
                return CecNetwork.PlaJoint;

            return CecNetwork.None;
        }

        /// <summary>Returns the aggregate CEC network mask of an entire unit (ship, submarine, aircraft, land SAM).</summary>
        public static CecNetwork GetPlatformNetwork(ObjectBase platform)
        {
            var systems = platform?._obp?._sensorSystems;
            if (systems == null) return CecNetwork.None;
            CecNetwork mask = CecNetwork.None;
            for (int i = 0; i < systems.Count; i++)
            {
                var s = systems[i];
                if (s == null || s.Inoperable.Value) continue;
                mask |= GetSensorNetwork(s);
            }
            return mask;
        }

        /// <summary>Returns the SARH terminal illuminator family for a radar, or None if not a known CEC SARH illuminator.</summary>
        public static SarhIllumFamily GetSarhFamily(SensorSystemRadar r)
        {
            if (r == null || string.IsNullOrEmpty(r._systemName)) return SarhIllumFamily.None;
            if (RadarSarhFamilies.TryGetValue(r._systemName, out var fam))
                return fam;
            return SarhIllumFamily.None;
        }

        /// <summary>Infers the SARH terminal illuminator family required by a launcher or missile.</summary>
        public static SarhIllumFamily GetRequiredSarhFamily(ObjectBase launcher, WeaponSystem ws = null)
        {
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

        // =========================================================================================
        // 6. LAUNCH-TIME CEC: Consort & AWACS Sensor Pool Injection
        // =========================================================================================

        /// <summary>
        /// Refreshes the set of qualifying CEC consort radars (and AWACS radars for ARH/mid-course missiles)
        /// appended to <paramref name="ws"/>'s <c>_vwp._associatedSensors</c> and <c>_vwp._externalGuidingSystems</c>.
        /// Native local sensors remain first in <c>_associatedSensors</c> so normal local shots use local sensors first.
        /// </summary>
        public static void RefreshConsortRadars(WeaponSystem ws)
        {
            var platform = ws?._baseObject;
            if (platform == null || platform.IsDestroyed) return;
            var tf = platform._taskforce;
            var vwp = ws._vwp;
            var associated = vwp?._associatedSensors;
            if (tf == null || associated == null) return;

            // Remove previously injected sensors so we never accumulate stale references.
            var injected = _injectedSensors.GetOrCreateValue(ws);
            foreach (var prev in injected)
                associated.Remove(prev);
            injected.Clear();

            var injectedExt = _injectedExternalNames.GetOrCreateValue(ws);
            if (vwp._externalGuidingSystems != null)
            {
                foreach (var extName in injectedExt)
                    vwp._externalGuidingSystems.Remove(extName);
            }
            injectedExt.Clear();

            // Determine launcher's CEC network, native FCR names, and SARH illuminator family.
            CecNetwork launcherNet = GetPlatformNetwork(platform);
            if (launcherNet == CecNetwork.None) return;

            var nativeRadarNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            SarhIllumFamily launcherSarhFamily = SarhIllumFamily.None;
            foreach (var s in associated)
            {
                if (s is SensorSystemRadar r0 && s._baseObject == platform && !string.IsNullOrEmpty(r0._systemName))
                {
                    nativeRadarNames.Add(r0._systemName);
                    if (launcherSarhFamily == SarhIllumFamily.None)
                        launcherSarhFamily = GetSarhFamily(r0);
                }
            }
            if (nativeRadarNames.Count == 0) return;

            // Launcher needs an intact datalink mast (even if radar transmitter is off for EMCON).
            float launcherAntennaY = DatalinkMastHeight(platform, requireRadarOn: false);
            if (launcherAntennaY < 0f) return;

            var ap = ws._ammoForEngage?._ap;
            bool isArhOrMidCourseOnly = ap != null &&
                                        ap._guidanceType != AmmunitionParameters.GuidanceType.SemiActiveRadarHoming &&
                                        ap._midCourseCorrection != null &&
                                        ap._midCourseCorrection.AllowsLauncherGuidance;

            // Scan taskforce vessels for consort illuminators / multifunction radars
            if (tf._taskforceVessels != null)
            {
                for (int i = 0; i < tf._taskforceVessels.Count; i++)
                {
                    TryInjectFromCooperator(
                        platform, launcherAntennaY, launcherNet, launcherSarhFamily,
                        nativeRadarNames, isArhOrMidCourseOnly, tf._taskforceVessels[i],
                        associated, vwp._externalGuidingSystems, injected, injectedExt);
                }
            }

            // Scan taskforce aircraft (AWACS / fighters) for ARH mid-course or fighter-to-fighter CEC
            if ((isArhOrMidCourseOnly || platform is Aircraft) && tf._taskforceAircraft != null)
            {
                for (int i = 0; i < tf._taskforceAircraft.Count; i++)
                {
                    TryInjectFromCooperator(
                        platform, launcherAntennaY, launcherNet, launcherSarhFamily,
                        nativeRadarNames, isArhOrMidCourseOnly, tf._taskforceAircraft[i],
                        associated, vwp._externalGuidingSystems, injected, injectedExt);
                }
            }

            // Scan taskforce helicopters (e.g. Ka-31, Z-18J AEW helicopters) for ARH mid-course
            if (isArhOrMidCourseOnly && tf._taskforceHelicopters != null)
            {
                for (int i = 0; i < tf._taskforceHelicopters.Count; i++)
                {
                    TryInjectFromCooperator(
                        platform, launcherAntennaY, launcherNet, launcherSarhFamily,
                        nativeRadarNames, isArhOrMidCourseOnly, tf._taskforceHelicopters[i],
                        associated, vwp._externalGuidingSystems, injected, injectedExt);
                }
            }
        }

        private static void TryInjectFromCooperator(
            ObjectBase launcher,
            float launcherAntennaY,
            CecNetwork launcherNet,
            SarhIllumFamily launcherSarhFamily,
            HashSet<string> nativeRadarNames,
            bool isArhOrMidCourseOnly,
            ObjectBase cooperator,
            List<SensorSystem> associated,
            List<string> externalGuidingSystems,
            HashSet<SensorSystem> injected,
            HashSet<string> injectedExt)
        {
            if (cooperator == null || cooperator == launcher || cooperator.IsDestroyed) return;
            var systems = cooperator._obp?._sensorSystems;
            if (systems == null) return;

            CecNetwork coopNet = GetPlatformNetwork(cooperator);
            if ((launcherNet & coopNet) == 0) return;

            bool airborneLink = (launcher is Aircraft || launcher is Helicopter ||
                                 cooperator is Aircraft || cooperator is Helicopter);
            float maxCap = airborneLink ? AirborneNetRangeCapUnits : SurfaceNetRangeCapUnits;
            float linkDist = HorizontalDistance(launcher.transform.position, cooperator.transform.position);
            if (linkDist > maxCap) return;

            // Cooperator must have an ACTIVE datalink radar on the net.
            float coopAntennaY = DatalinkMastHeight(cooperator, requireRadarOn: true);
            if (coopAntennaY < 0f) return;
            if (!InLinkHorizon(launcherAntennaY, coopAntennaY, linkDist)) return;

            for (int i = 0; i < systems.Count; i++)
            {
                if (!(systems[i] is SensorSystemRadar r)) continue;
                if (!QualifiesForLaunchInjection(r, launcherNet, launcherSarhFamily, nativeRadarNames, isArhOrMidCourseOnly))
                    continue;

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

        private static bool QualifiesForLaunchInjection(
            SensorSystemRadar r,
            CecNetwork launcherNet,
            SarhIllumFamily launcherSarhFamily,
            HashSet<string> nativeRadarNames,
            bool isArhOrMidCourseOnly)
        {
            if (!UsableGuidanceRadar(r)) return false;

            if (isArhOrMidCourseOnly)
            {
                // For ARH / mid-course datalink missiles (SM-6, SM-2IIIC, ESSM Blk 2, Aster, AIM-120D, AIM-174B, PL-15, 40N6):
                // Accept multifunction radars, synthetic datalinks, or AWACS radars in the same CEC network.
                if (PureCwDishIlluminators.Contains(r._systemName)) return false;
                CecNetwork rNet = GetSensorNetwork(r);
                if ((rNet & launcherNet) == 0 && !nativeRadarNames.Contains(r._systemName))
                    return false;
                if (IsAwacsRadar(r) || IsCecMultifunctionRadar(r))
                    EnsureMidCourseChannels(r);
                return r.HasFreeChannels();
            }
            else
            {
                // For SARH missiles: require a physically compatible terminal illuminator (never a synthetic datalink or AWACS).
                if (IsSyntheticDatalinkSensor(r) || IsAwacsRadar(r)) return false;
                SarhIllumFamily rFam = GetSarhFamily(r);
                bool familyMatch = (launcherSarhFamily != SarhIllumFamily.None && rFam == launcherSarhFamily) ||
                                   nativeRadarNames.Contains(r._systemName);
                if (!familyMatch) return false;
                return r.HasFreeChannels();
            }
        }

        // =========================================================================================
        // 7. MID-FLIGHT HANDOFF: Find Best Consort or AWACS Radar for an In-Flight Missile
        // =========================================================================================

        /// <summary>
        /// Finds the best qualifying CEC consort or AWACS radar to take over guidance of <paramref name="missile"/>.
        /// Handles both SARH terminal illuminator handoffs and ARH/command mid-course datalink handoffs.
        /// </summary>
        public static SensorSystemRadar FindHandoffRadar(Missile missile)
        {
            var ap = missile?._ap;
            var launcher = missile?._launchPlatform;
            var target = missile?.CurrentIntendedTargetObject;
            var tf = launcher?._taskforce ?? missile?._taskforce;
            if (ap == null || tf == null || target == null || target.IsDestroyed) return null;

            bool isSarh = missile._seeker != null &&
                          missile._seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming;
            bool isMidCourse = ap._midCourseCorrection != null && ap._midCourseCorrection.AllowsLauncherGuidance;
            if (!isSarh && !isMidCourse) return null;

            // If an ARH missile has already gone pitbull (locked onto target with its own active seeker),
            // it no longer needs a consort radar channel.
            if (!isSarh && missile._seeker != null &&
                missile._seeker._guidanceType == AmmunitionParameters.GuidanceType.ActiveRadarHoming &&
                missile.CurrentStrongestTargetEcho != null)
            {
                return null;
            }

            CecNetwork reqNet = launcher != null ? GetPlatformNetwork(launcher) : CecNetwork.None;
            if (reqNet == CecNetwork.None && missile._guidingRadarSystem != null)
                reqNet = GetSensorNetwork(missile._guidingRadarSystem);
            if (reqNet == CecNetwork.None)
                reqNet = InferNetworkFromAmmoName(ap._ammunitionFileName);
            if (reqNet == CecNetwork.None) return null;

            SarhIllumFamily reqSarhFam = isSarh ? GetRequiredSarhFamily(launcher) : SarhIllumFamily.None;
            if (isSarh && reqSarhFam == SarhIllumFamily.None && missile._guidingRadarSystem != null)
                reqSarhFam = GetSarhFamily(missile._guidingRadarSystem);

            var current = missile._guidingRadarSystem;
            var targetPos = target.transform.position;

            bool launcherAlive = launcher != null && !launcher.IsDestroyed;
            float launcherAntennaY = launcherAlive ? DatalinkMastHeight(launcher, requireRadarOn: false) : -1f;
            var launcherPos = launcherAlive ? launcher.transform.position : Vector3.zero;
            bool launcherAirborne = launcher is Aircraft || launcher is Helicopter;

            SensorSystemRadar best = null;
            float bestScore = float.MaxValue;

            // 1. Search taskforce vessels
            if (tf._taskforceVessels != null)
            {
                for (int i = 0; i < tf._taskforceVessels.Count; i++)
                {
                    EvaluateHandoffCooperator(
                        missile, ap, target, targetPos, isSarh, reqNet, reqSarhFam, current,
                        launcherAlive, launcherAirborne, launcherAntennaY, launcherPos,
                        tf._taskforceVessels[i], ref best, ref bestScore);
                }
            }

            // 2. Search taskforce aircraft (AWACS & fighters for ARH mid-course or MiG-31 Zaslon SARH)
            if ((!isSarh || reqSarhFam == SarhIllumFamily.RuZaslonPesa) && tf._taskforceAircraft != null)
            {
                for (int i = 0; i < tf._taskforceAircraft.Count; i++)
                {
                    EvaluateHandoffCooperator(
                        missile, ap, target, targetPos, isSarh, reqNet, reqSarhFam, current,
                        launcherAlive, launcherAirborne, launcherAntennaY, launcherPos,
                        tf._taskforceAircraft[i], ref best, ref bestScore);
                }
            }

            // 3. Search taskforce helicopters (Ka-31 / Z-18J AEW for ARH mid-course)
            if (!isSarh && tf._taskforceHelicopters != null)
            {
                for (int i = 0; i < tf._taskforceHelicopters.Count; i++)
                {
                    EvaluateHandoffCooperator(
                        missile, ap, target, targetPos, isSarh, reqNet, reqSarhFam, current,
                        launcherAlive, launcherAirborne, launcherAntennaY, launcherPos,
                        tf._taskforceHelicopters[i], ref best, ref bestScore);
                }
            }

            return best;
        }

        private static void EvaluateHandoffCooperator(
            Missile missile,
            AmmunitionParameters ap,
            ObjectBase target,
            Vector3 targetPos,
            bool isSarh,
            CecNetwork reqNet,
            SarhIllumFamily reqSarhFam,
            SensorSystemRadar current,
            bool launcherAlive,
            bool launcherAirborne,
            float launcherAntennaY,
            Vector3 launcherPos,
            ObjectBase cooperator,
            ref SensorSystemRadar best,
            ref float bestScore)
        {
            if (cooperator == null || cooperator.IsDestroyed) return;
            var systems = cooperator._obp?._sensorSystems;
            if (systems == null) return;

            CecNetwork coopNet = GetPlatformNetwork(cooperator);
            if ((reqNet & coopNet) == 0) return;

            float coopAntennaY = DatalinkMastHeight(cooperator, requireRadarOn: true);
            if (coopAntennaY < 0f) return;

            if (launcherAlive && cooperator != missile._launchPlatform)
            {
                bool airborneLink = launcherAirborne || cooperator is Aircraft || cooperator is Helicopter;
                float maxCap = airborneLink ? AirborneNetRangeCapUnits : SurfaceNetRangeCapUnits;
                float linkDist = HorizontalDistance(launcherPos, cooperator.transform.position);
                if (linkDist > maxCap) return;
                if (launcherAntennaY >= 0f && !InLinkHorizon(launcherAntennaY, coopAntennaY, linkDist))
                    return;
            }

            for (int i = 0; i < systems.Count; i++)
            {
                if (!(systems[i] is SensorSystemRadar r) || r == current) continue;
                if (!QualifiesForHandoff(r, ap, isSarh, reqNet, reqSarhFam, current?._systemName)) continue;
                if (r._mountObject == null) continue;

                // Must see the target right now (within range, radar horizon, and DirectedSearch FOV arc).
                if (!RadarCanSee(r, target)) continue;
                // Must also have radio line-of-sight / horizon to the missile for rear-reference or command uplink.
                if (!RadarCanReachMissile(r, missile)) continue;

                float dSq = (r._mountObject.transform.position - targetPos).sqrMagnitude;
                if (dSq < bestScore)
                {
                    bestScore = dSq;
                    best = r;
                }
            }
        }

        private static bool QualifiesForHandoff(
            SensorSystemRadar r,
            AmmunitionParameters ap,
            bool isSarh,
            CecNetwork reqNet,
            SarhIllumFamily reqSarhFam,
            string currentRadarName)
        {
            if (!UsableGuidanceRadar(r)) return false;

            var compatible = ap._compatibleGuidanceSystems;
            if (compatible != null && compatible.Count > 0 && !compatible.Contains(r._systemName))
                return false;

            if (isSarh)
            {
                if (IsSyntheticDatalinkSensor(r) || IsAwacsRadar(r)) return false;
                if (r._type != SensorSystemRadar.RadarType.Targeting &&
                    r._type != SensorSystemRadar.RadarType.DirectedSearch)
                    return false;

                SarhIllumFamily rFam = GetSarhFamily(r);
                bool match = (reqSarhFam != SarhIllumFamily.None && rFam == reqSarhFam) ||
                             (!string.IsNullOrEmpty(currentRadarName) &&
                              string.Equals(r._systemName, currentRadarName, StringComparison.OrdinalIgnoreCase));
                if (!match) return false;
                return r.HasFreeChannels();
            }
            else
            {
                // Mid-course datalink handoff for ARH / IIR / Command missiles
                if (PureCwDishIlluminators.Contains(r._systemName)) return false;
                CecNetwork rNet = GetSensorNetwork(r);
                if ((rNet & reqNet) == 0) return false;

                if (IsAwacsRadar(r) || IsCecMultifunctionRadar(r))
                    EnsureMidCourseChannels(r);
                else if (r._type != SensorSystemRadar.RadarType.Targeting &&
                         r._type != SensorSystemRadar.RadarType.DirectedSearch)
                    return false;

                return r.HasFreeChannels();
            }
        }

        // =========================================================================================
        // 8. OTH / EMCON "ENGAGE-ON-REMOTE" & COOPERATIVE TRACKING
        // =========================================================================================

        /// <summary>
        /// True if <paramref name="shooter"/> is part of a CEC network and a cooperative unit (AWACS or
        /// forward ship/fighter) in the same taskforce and network is actively tracking <paramref name="target"/>.
        /// Works even when <paramref name="shooter"/> has its own radars switched OFF (EMCON).
        /// </summary>
        public static bool HasCooperativeAirTrack(
            ObjectBase shooter,
            ObjectBase target,
            ObjectBase.SensorRangeOptions options = default)
        {
            if (shooter == null || shooter.IsDestroyed || target == null || target.IsDestroyed) return false;
            if (!target.isAirTarget()) return false;

            var tf = shooter._taskforce;
            if (tf == null) return false;

            CecNetwork shooterNet = GetPlatformNetwork(shooter);
            if (shooterNet == CecNetwork.None) return false;

            float shooterAntennaY = DatalinkMastHeight(shooter, requireRadarOn: false);
            if (shooterAntennaY < 0f) return false;

            bool shooterAirborne = shooter is Aircraft || shooter is Helicopter;
            var shooterPos = shooter.transform.position;

            // Check taskforce aircraft (AWACS / fighters) first — most common OTH track source
            if (tf._taskforceAircraft != null)
            {
                for (int i = 0; i < tf._taskforceAircraft.Count; i++)
                {
                    if (CooperatorSeesAirTarget(shooter, shooterPos, shooterAntennaY, shooterAirborne, shooterNet,
                                                tf._taskforceAircraft[i], target))
                        return true;
                }
            }

            // Check taskforce helicopters (Ka-31, Z-18J AEW)
            if (tf._taskforceHelicopters != null)
            {
                for (int i = 0; i < tf._taskforceHelicopters.Count; i++)
                {
                    if (CooperatorSeesAirTarget(shooter, shooterPos, shooterAntennaY, shooterAirborne, shooterNet,
                                                tf._taskforceHelicopters[i], target))
                        return true;
                }
            }

            // Check taskforce surface vessels (forward picket ships)
            if (tf._taskforceVessels != null)
            {
                for (int i = 0; i < tf._taskforceVessels.Count; i++)
                {
                    if (CooperatorSeesAirTarget(shooter, shooterPos, shooterAntennaY, shooterAirborne, shooterNet,
                                                tf._taskforceVessels[i], target))
                        return true;
                }
            }

            return false;
        }

        private static bool CooperatorSeesAirTarget(
            ObjectBase shooter,
            Vector3 shooterPos,
            float shooterAntennaY,
            bool shooterAirborne,
            CecNetwork shooterNet,
            ObjectBase cooperator,
            ObjectBase target)
        {
            if (cooperator == null || cooperator == shooter || cooperator.IsDestroyed) return false;
            var systems = cooperator._obp?._sensorSystems;
            if (systems == null) return false;

            CecNetwork coopNet = GetPlatformNetwork(cooperator);
            if ((shooterNet & coopNet) == 0) return false;

            bool airborneLink = shooterAirborne || cooperator is Aircraft || cooperator is Helicopter;
            float maxCap = airborneLink ? AirborneNetRangeCapUnits : SurfaceNetRangeCapUnits;
            float linkDist = HorizontalDistance(shooterPos, cooperator.transform.position);
            if (linkDist > maxCap) return false;

            float coopAntennaY = DatalinkMastHeight(cooperator, requireRadarOn: true);
            if (coopAntennaY < 0f) return false;
            if (!InLinkHorizon(shooterAntennaY, coopAntennaY, linkDist)) return false;

            float distToTarget = HorizontalDistance(cooperator.transform.position, target.transform.position);
            for (int i = 0; i < systems.Count; i++)
            {
                if (!(systems[i] is SensorSystemRadar r)) continue;
                if (!UsableGuidanceRadar(r) || !r.CanSeeAirTargets) continue;
                if (!r._radar._rp._hasDataLink && GetSensorNetwork(r) == CecNetwork.None) continue;

                if (RadarCanSee(r, target))
                {
                    var rangeData = r.GetRange(target);
                    if (rangeData != null && distToTarget <= rangeData.Distance)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks whether an allied CEC consort has a compatible operational FCR / illuminator that can
        /// cover <paramref name="target"/> at its current position, lifting the shooter's local FCR horizon limit.
        /// </summary>
        public static bool HasRemoteFcrCoverage(ObjectBase shooter, AmmunitionParameters ap, ObjectBase target)
        {
            if (shooter == null || shooter.IsDestroyed || ap == null || target == null || target.IsDestroyed)
                return false;

            var tf = shooter._taskforce;
            if (tf == null) return false;

            CecNetwork shooterNet = GetPlatformNetwork(shooter);
            if (shooterNet == CecNetwork.None) return false;

            float shooterAntennaY = DatalinkMastHeight(shooter, requireRadarOn: false);
            if (shooterAntennaY < 0f) return false;

            bool isSarh = ap._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming;
            SarhIllumFamily reqFam = isSarh ? GetRequiredSarhFamily(shooter) : SarhIllumFamily.None;

            // For ARH / Datalink missiles, any cooperative air track in the CEC net satisfies mid-course FCR range.
            if (!isSarh && ap._midCourseCorrection != null && ap._midCourseCorrection.AllowsLauncherGuidance)
            {
                return HasCooperativeAirTrack(shooter, target);
            }

            // For SARH missiles, a forward CEC consort must have a compatible illuminator within range & horizon of target.
            if (tf._taskforceVessels != null)
            {
                var shooterPos = shooter.transform.position;
                for (int i = 0; i < tf._taskforceVessels.Count; i++)
                {
                    var vessel = tf._taskforceVessels[i];
                    if (vessel == null || vessel == shooter || vessel.IsDestroyed) continue;
                    var systems = vessel._obp?._sensorSystems;
                    if (systems == null) continue;
                    if ((GetPlatformNetwork(vessel) & shooterNet) == 0) continue;

                    float linkDist = HorizontalDistance(shooterPos, vessel.transform.position);
                    if (linkDist > SurfaceNetRangeCapUnits) continue;
                    float coopAntennaY = DatalinkMastHeight(vessel, requireRadarOn: true);
                    if (coopAntennaY < 0f || !InLinkHorizon(shooterAntennaY, coopAntennaY, linkDist)) continue;

                    for (int j = 0; j < systems.Count; j++)
                    {
                        if (!(systems[j] is SensorSystemRadar r)) continue;
                        if (r.Inoperable.Value || r._radar?._rp == null || r._mountObject == null) continue;
                        if (IsSyntheticDatalinkSensor(r) || IsAwacsRadar(r)) continue;
                        if (reqFam != SarhIllumFamily.None && GetSarhFamily(r) != reqFam) continue;
                        if (!r.HasFreeChannels()) continue;
                        if (RadarCanSee(r, target))
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// True if <paramref name="platform"/> currently has all of its own air-search radars switched OFF (EMCON).
        /// </summary>
        public static bool IsPlatformInAirRadarEmcon(ObjectBase platform)
        {
            var systems = platform?._obp?._sensorSystems;
            if (systems == null) return false;
            bool hasAirSearchRadar = false;
            for (int i = 0; i < systems.Count; i++)
            {
                if (!(systems[i] is SensorSystemRadar r)) continue;
                if (r.Inoperable.Value || r._radar?._rp == null || !r.CanSeeAirTargets) continue;
                if (IsSyntheticDatalinkSensor(r) || PureCwDishIlluminators.Contains(r._systemName)) continue;
                if (r._type == SensorSystemRadar.RadarType.Search ||
                    r._type == SensorSystemRadar.RadarType.DirectedSearch)
                {
                    hasAirSearchRadar = true;
                    if (r.IsOn.Value) return false;
                }
            }
            return hasAirSearchRadar;
        }

        // =========================================================================================
        // 9. LOW-LEVEL GEOMETRY & RADAR HELPERS
        // =========================================================================================

        private static bool IsCecMultifunctionRadar(SensorSystemRadar r)
        {
            if (r == null || string.IsNullOrEmpty(r._systemName)) return false;
            string n = r._systemName;
            return n.StartsWith("SPY-", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("eu_SPY-", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("eu_SAMPSON", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("EMPAR", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("eu_EMPAR", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("eu_MFRA", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("eu_KRONOS", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("Herakles", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("eu_Herakles", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("Sea_fire_500", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("eu_Sea-fire-500", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("LJG-346", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("LJQ-346", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("Type_346", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("rfn_5p20k", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("rfn_mf_rlk", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureMidCourseChannels(SensorSystemRadar r)
        {
            if (r._targetChannels <= 0) r._targetChannels = 32;
            if (r._weaponChannels <= 0) r._weaponChannels = 32;
        }

        private static CecNetwork InferNetworkFromAmmoName(string ammoName)
        {
            if (string.IsNullOrEmpty(ammoName)) return CecNetwork.None;
            if (ammoName.StartsWith("usn_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("usa_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("usaf_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("dts_aim-", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("jmsdf_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("rok_", StringComparison.OrdinalIgnoreCase))
                return CecNetwork.WesternJoint;

            if (ammoName.StartsWith("mbda_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("fr_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("eu_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("spa_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("ger_", StringComparison.OrdinalIgnoreCase))
                return CecNetwork.WesternJoint;

            if (ammoName.StartsWith("rfn_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("wp_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("su_", StringComparison.OrdinalIgnoreCase))
                return CecNetwork.RuJoint;

            if (ammoName.StartsWith("plan_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("pla_", StringComparison.OrdinalIgnoreCase) ||
                ammoName.StartsWith("plaaf_", StringComparison.OrdinalIgnoreCase))
                return CecNetwork.PlaJoint;

            return CecNetwork.None;
        }

        public static bool UsableGuidanceRadar(SensorSystemRadar r)
        {
            if (r == null || r._baseObject == null || r._baseObject.IsDestroyed) return false;
            if (r.Inoperable.Value || !r.IsOn.Value) return false;
            return r._radar?._rp != null;
        }

        /// <summary>
        /// Height of the platform's highest datalink-capable radar mount, or -1f if none.
        /// When <paramref name="requireRadarOn"/> is false (for the shooter/receiver), an intact mast
        /// suffices even if the ship's high-power search radar transmitter is off for EMCON.
        /// </summary>
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
                if (r._radar._rp._hasDataLink || GetSensorNetwork(r) != CecNetwork.None)
                {
                    float y = r._mountObject.transform.position.y;
                    if (y > best) best = y;
                }
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

            if (r._type == SensorSystemRadar.RadarType.DirectedSearch &&
                !r.IsObjectInFOV(r.SensorPosition, mount.transform.forward, obj.transform.position))
                return false;

            return true;
        }

        private static bool RadarCanReachMissile(SensorSystemRadar r, Missile m)
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
    }
}
