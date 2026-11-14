#!/usr/bin/env python3
"""
tools/generate_cec_tables.py
Generates CecMod/CecDataTables.g.cs from verified CSV datasets in data/cec_tables/.
Supports --verify flag to validate CSV data without generating code.
"""

import sys
import os
import csv
import re
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent
DATA_DIR = PROJECT_ROOT / "data" / "cec_tables"
OUTPUT_CS = PROJECT_ROOT / "CecMod" / "CecDataTables.g.cs"

# 10 standards in the 10x10 matrix in order:
MATRIX_STANDARDS = [
    ("USG-2/3 CEC", "Usg2Cec"),
    ("TTNT", "Ttnt"),
    ("Link 16 (MIDS/JTIDS)", "Link16"),
    ("Link 11 (TADIL-A)", "Link11"),
    ("IFDL (F-22)", "Ifdl"),
    ("MADL (F-35)", "Madl"),
    ("JSIDLS", "Jsidls"),
    ("HN-900", "Hn900"),
    ("RuNaval / Fort-TVM", "RuFortTvm"),
    ("TKS-2 / APD-518", "Tks2Apd518"),
]

TIER_MAP = {
    "Tier 1 - Full CEC": "Tier1_FullCec",
    "Tier 1 - Intra-Flight Only": "Tier1_IntraFlightOnly",
    "Tier 2 - Cue Only": "Tier2_CueOnly",
    "Tier 2 - Midcourse Rescue": "Tier2_MidcourseRescue",
    "Tier 3 - Track Relay": "Tier3_TrackRelay",
    "Gateway Required": "GatewayRequired",
    "N/A": "Incompatible",
}

SYNTHETIC_KNOWN = {
    "SYR-1", "SYR-1_NTU", "SYS-1", "SYS-2", "SPW-2",
    "eu_AEGIS_BL5", "eu_AEGIS_BL9", "eu_AEGIS_BL10",
    "eu_2T_Datalink", "eu_UYK-43", "eu_UYK-44", "eu_UYK-44B",
    "eu_tacticalpcgen1", "eu_tacticalpcgen2", "eu_tacticalpcgen3",
    "eu_JUWL", "eu_aster_datalink",
    "eu_EMPAR_2", "eu_KRONOS_guidance", "eu_MFRA_guidance", "eu_SAMPSON_guidance", "EMPAR_2",
    "spa_SCOMBA_v1", "DD115_TacticalDatalink", "Mk92_Mod12_MCG", "LJG-318",
    "Link-16_Receiver", "eu_GPS_Receiver", "F16_GPS_Link", "IAF_GPS_Link", "GPS_Link", "WZ-8_Datalink"
}

PURE_CW_KNOWN = {
    "SPG-62", "eu_SPG-62", "SPG-55", "SPG-55B", "SPG-51", "SPG-51D", "SPG-60",
    "MK-95", "MK-115", "MK92_FCS", "MK92_Mod6_FCS", "MK92_Mod12_FCS",
    "eu_CEROS-200_CWI", "dk_iver_CEROS-200_CWI",
    "eu_stir120", "eu_stir180", "eu_stir240", "Stir", "STIR-180", "STIR-240",
    "Front_Dome", "rfn_mr90", "rfn_3r90m", "LJG-316"
}

def normalize_key(k: str) -> str:
    if not k:
        return ""
    return k.replace(" (P2027)", "").strip()

def parse_tier(tier_str: str) -> str:
    s = (tier_str or "").strip().lower()
    if "tier 1" in s or s == "1":
        return "Tier1"
    if "tier 2" in s or s == "2":
        return "Tier2"
    if "tier 0" in s or s == "0":
        return "Tier0"
    return "Tier3"

def parse_datalinks(raw_str: str, entity_id: str = "") -> list:
    raw = (raw_str or "").strip()
    res = []
    
    # Check GPS / SATCOM
    if "gps" in raw.lower() or "gps" in entity_id.lower() or "satcom" in raw.lower():
        res.append("SatcomUplink")
    
    # CEC / USG
    if re.search(r"usg-[23]|cec", raw, re.I):
        res.append("Usg2Cec")
        
    # TTNT
    if "ttnt" in raw.lower():
        res.append("Ttnt")
        
    # Link 16 / MIDS / JTIDS / Meteor
    if re.search(r"link\s*16|mids|jtids|meteor\s*datalink|k-jtdls|tidls|jdcs", raw, re.I):
        res.append("Link16")
        
    # Link 11 / TADIL-A
    if re.search(r"link\s*11|tadil-a", raw, re.I):
        res.append("Link11")
        
    # NatoEuroNaval
    if "natoeuronaval" in raw.lower():
        if "Link16" not in res:
            res.append("Link16")
        if "Link11" not in res:
            res.append("Link11")
        
    # IFDL
    if "ifdl" in raw.lower():
        res.append("Ifdl")
        
    # MADL
    if "madl" in raw.lower():
        res.append("Madl")
        
    # JSIDLS
    if re.search(r"jsidls|pla tactical|directional aesa", raw, re.I):
        res.append("Jsidls")
        
    # HN-900
    if re.search(r"hn-900|data link 4", raw, re.I):
        res.append("Hn900")
        
    # Fort-TVM / RuNaval
    if re.search(r"fort-tvm", raw, re.I):
        res.append("RuFortTvm")
        
    # RuNaval Tactical / C4I
    if re.search(r"runaval\s*(tactical|c4i)|e-801e", raw, re.I):
        res.append("RuNavalTactical")
        
    # TKS-2 / APD-518
    if re.search(r"tks-2|apd-518|shmel|s-111", raw, re.I):
        res.append("Tks2Apd518")
        
    return res

def datalinks_to_csharp(flags: list) -> str:
    if not flags:
        return "DatalinkStandard.None"
    return " | ".join(f"DatalinkStandard.{f}" for f in flags)

def parse_sarh_family(raw_str: str) -> str:
    s = (raw_str or "").strip()
    if not s:
        return "None"
    # Take first if semicolon separated
    first = [p.strip() for p in s.split(";") if p.strip()][0]
    valid = {"UsNatoXBandCw", "RuFortTvm", "RuShtilCw", "PlanHhq9", "PlanHhq16", "RuZaslonPesa"}
    if first in valid:
        return first
    return "None"

def parse_guidance_family(raw_str: str) -> str:
    s = (raw_str or "").strip().upper()
    if "SARH" in s:
        return "SARH"
    if "ARH" in s:
        return "ARH"
    if "TVM" in s:
        return "TVM"
    if "COMMAND" in s:
        return "Command"
    if "IR" in s or "INFRARED" in s:
        return "IR"
    if "ARM" in s or "ANTI-RADIATION" in s:
        return "ARM"
    return "Other"

def resolve_missile_sarh_family(row: dict, radar_guidance_map: dict) -> str:
    g_fam = row.get("GuidanceFamily", "").strip()
    if g_fam not in ["SARH", "TVM"]:
        return "None"
    
    sensors = [s.strip() for s in row.get("LinkedSensors", "").split(";") if s.strip()]
    matched = [radar_guidance_map[s] for s in sensors if s in radar_guidance_map]
    if matched:
        if "PlanHhq16" in matched:
            return "PlanHhq16"
        if "PlanHhq9" in matched:
            return "PlanHhq9"
        if "RuFortTvm" in matched:
            return "RuFortTvm"
        if "RuShtilCw" in matched:
            return "RuShtilCw"
        if "RuZaslonPesa" in matched:
            return "RuZaslonPesa"
        if "UsNatoXBandCw" in matched:
            return "UsNatoXBandCw"
        return matched[0]
        
    eid = row.get("EntityId", "").lower()
    arch = row.get("ArchitectureGroup", "").lower()
    if any(k in eid for k in ["rim-162", "rim-66", "rim-67", "rim-7", "aspide"]) or "us / nato" in arch:
        return "UsNatoXBandCw"
    if "48n6" in eid or "sa-n-6" in eid:
        return "RuFortTvm"
    if "9m317" in eid or "sa-n-7" in eid or "sa-n-12" in eid:
        return "RuShtilCw"
    if "hq-16" in eid:
        return "PlanHhq16"
    if "hq-9" in eid:
        return "PlanHhq9"
    if "r-33" in eid:
        return "RuZaslonPesa"
    return "None"

def load_matrix(csv_path: Path):
    with open(csv_path, "r", encoding="utf-8-sig") as f:
        reader = csv.reader(f)
        headers = [h.strip() for h in next(reader)[1:]]
        matrix = []
        for row in reader:
            sender = row[0].strip()
            row_vals = [TIER_MAP.get(c.strip(), "Incompatible") for c in row[1:]]
            matrix.append((sender, row_vals))
    return headers, matrix

def escape_cs_string(s: str) -> str:
    return (s or "").replace("\\", "\\\\").replace('"', '\\"')

def generate_code(is_verify: bool = False):
    warnings = []
    
    # 1. Parse Matrix
    matrix_csv = DATA_DIR / "02_Datalink_Compatibility_Matrix.csv"
    headers, matrix = load_matrix(matrix_csv)
    if len(matrix) != 10 or any(len(row[1]) != 10 for row in matrix):
        warnings.append(f"Matrix dimension mismatch: {len(matrix)}x{[len(r[1]) for r in matrix]}")

    # 2. Parse Radars to populate radar_guidance_map
    radars_csv = DATA_DIR / "05_Radars.csv"
    radar_guidance_map = {}
    radar_rows = []
    with open(radars_csv, "r", encoding="utf-8-sig") as f:
        for r in csv.DictReader(f):
            eid = r["EntityId"].strip()
            g = parse_sarh_family(r.get("GuidanceFamily", ""))
            if g != "None":
                radar_guidance_map[eid] = g
                norm_eid = normalize_key(eid)
                if norm_eid != eid:
                    radar_guidance_map[norm_eid] = g
            radar_rows.append(r)

    # 3. Parse Ships and Aircraft (Platforms)
    platform_rows = []
    ships_csv = DATA_DIR / "03_Ships.csv"
    with open(ships_csv, "r", encoding="utf-8-sig") as f:
        for r in csv.DictReader(f):
            platform_rows.append((r, "ship"))

    aircraft_csv = DATA_DIR / "04_Aircraft.csv"
    with open(aircraft_csv, "r", encoding="utf-8-sig") as f:
        for r in csv.DictReader(f):
            platform_rows.append((r, "aircraft"))

    # 4. Parse Missiles
    missile_rows = []
    missiles_csv = DATA_DIR / "06_Missiles.csv"
    with open(missiles_csv, "r", encoding="utf-8-sig") as f:
        for r in csv.DictReader(f):
            missile_rows.append(r)

    print(f"Loaded datasets from {DATA_DIR}:")
    print(f"  Matrix rows: {len(matrix)}x10")
    print(f"  Ships: {len([p for p in platform_rows if p[1] == 'ship'])}")
    print(f"  Aircraft: {len([p for p in platform_rows if p[1] == 'aircraft'])}")
    print(f"  Total Platforms: {len(platform_rows)}")
    print(f"  Radars: {len(radar_rows)}")
    print(f"  Missiles: {len(missile_rows)}")

    # Check unmapped
    unmapped_platforms = 0
    for r, kind in platform_rows:
        dl_str = r.get("DataLinkType", "").strip()
        parsed_dl = parse_datalinks(dl_str, r.get("EntityId", ""))
        if dl_str and not parsed_dl:
            unmapped_platforms += 1
            warnings.append(f"Unmapped datalink for platform {r['EntityId']}: '{dl_str}'")

    if warnings:
        print(f"WARNINGS ({len(warnings)}):")
        for w in warnings:
            print(f"  - {w}")
    else:
        print("Verification: 0 unmapped entities, 0 missing matrix pairs.")

    if is_verify:
        print("Verify mode completed successfully. No files written.")
        return 0

    # Generate C# source
    lines = []
    lines.append("// <auto-generated>")
    lines.append("// This file was generated by tools/generate_cec_tables.py.")
    lines.append("// Do not edit manually.")
    lines.append("// </auto-generated>")
    lines.append("")
    lines.append("using System;")
    lines.append("using System.Collections.Generic;")
    lines.append("")
    lines.append("namespace CecMod")
    lines.append("{")
    lines.append("    public enum CecTier")
    lines.append("    {")
    lines.append("        Tier0 = 0,")
    lines.append("        Tier3 = 3,")
    lines.append("        Tier2 = 2,")
    lines.append("        Tier1 = 1")
    lines.append("    }")
    lines.append("")
    lines.append("    [Flags]")
    lines.append("    public enum DatalinkStandard")
    lines.append("    {")
    lines.append("        None = 0,")
    lines.append("        Usg2Cec = 1 << 0,          // USG-2/3 CEC")
    lines.append("        Ttnt = 1 << 1,             // TTNT")
    lines.append("        Link16 = 1 << 2,           // Link 16 / MIDS / JTIDS / Meteor DL")
    lines.append("        Link11 = 1 << 3,           // Link 11 / TADIL-A")
    lines.append("        Ifdl = 1 << 4,             // IFDL (F-22)")
    lines.append("        Madl = 1 << 5,             // MADL (F-35)")
    lines.append("        Jsidls = 1 << 6,           // JSIDLS (PLA/PLAN)")
    lines.append("        Hn900 = 1 << 7,            // HN-900 (PLAN)")
    lines.append("        RuFortTvm = 1 << 8,        // RuNaval / Fort-TVM")
    lines.append("        Tks2Apd518 = 1 << 9,       // TKS-2 / APD-518 (Russian Air)")
    lines.append("        RuNavalTactical = 1 << 10, // RuNaval Tactical / C4I")
    lines.append("        SatcomUplink = 1 << 11     // SATCOM / GPS link (eu_GPS_Receiver)")
    lines.append("    }")
    lines.append("")
    lines.append("    public enum InteractionTier")
    lines.append("    {")
    lines.append("        Incompatible = 0,")
    lines.append("        Tier3_TrackRelay = 1,")
    lines.append("        Tier2_CueOnly = 2,")
    lines.append("        Tier2_MidcourseRescue = 3,")
    lines.append("        Tier1_FullCec = 4,")
    lines.append("        Tier1_IntraFlightOnly = 5,")
    lines.append("        GatewayRequired = 6,")
    lines.append("        SatcomGlobal = 7")
    lines.append("    }")
    lines.append("")
    lines.append("    public enum SarhIllumFamily")
    lines.append("    {")
    lines.append("        None = 0,")
    lines.append("        UsNatoXBandCw = 1,")
    lines.append("        RuFortTvm = 2,")
    lines.append("        RuShtilCw = 3,")
    lines.append("        PlanHhq9 = 4,")
    lines.append("        PlanHhq16 = 5,")
    lines.append("        RuZaslonPesa = 6")
    lines.append("    }")
    lines.append("")
    lines.append("    public enum GuidanceFamily")
    lines.append("    {")
    lines.append("        Unknown = 0,")
    lines.append("        ARH = 1,")
    lines.append("        SARH = 2,")
    lines.append("        TVM = 3,")
    lines.append("        Command = 4,")
    lines.append("        IR = 5,")
    lines.append("        ARM = 6,")
    lines.append("        Other = 7")
    lines.append("    }")
    lines.append("")
    lines.append("    public sealed class PlatformProfile")
    lines.append("    {")
    lines.append("        public string EntityId { get; }")
    lines.append("        public string DisplayName { get; }")
    lines.append("        public CecTier CecTier { get; }")
    lines.append("        public DatalinkStandard Datalinks { get; }")
    lines.append("        public SarhIllumFamily SarhIllumFamily { get; }")
    lines.append("        public bool IsGateway { get; }")
    lines.append("")
    lines.append("        public PlatformProfile(string entityId, string displayName, CecTier cecTier, DatalinkStandard datalinks, SarhIllumFamily sarhIllumFamily, bool isGateway)")
    lines.append("        {")
    lines.append("            EntityId = entityId;")
    lines.append("            DisplayName = displayName;")
    lines.append("            CecTier = cecTier;")
    lines.append("            Datalinks = datalinks;")
    lines.append("            SarhIllumFamily = sarhIllumFamily;")
    lines.append("            IsGateway = isGateway;")
    lines.append("        }")
    lines.append("")
    lines.append("        public static readonly PlatformProfile DefaultTier3 = new PlatformProfile(")
    lines.append("            \"DefaultTier3\", \"Default Tier 3 Platform\", CecTier.Tier3, DatalinkStandard.Link11, SarhIllumFamily.None, false);")
    lines.append("    }")
    lines.append("")
    lines.append("    public sealed class RadarProfile")
    lines.append("    {")
    lines.append("        public string EntityId { get; }")
    lines.append("        public string DisplayName { get; }")
    lines.append("        public CecTier CecTier { get; }")
    lines.append("        public DatalinkStandard Datalinks { get; }")
    lines.append("        public SarhIllumFamily SarhIllumFamily { get; }")
    lines.append("        public bool IsAwacs { get; }")
    lines.append("        public bool IsSyntheticDatalink { get; }")
    lines.append("        public bool IsPureCwIlluminator { get; }")
    lines.append("        public bool IsMfr { get; }")
    lines.append("")
    lines.append("        public RadarProfile(string entityId, string displayName, CecTier cecTier, DatalinkStandard datalinks, SarhIllumFamily sarhIllumFamily, bool isAwacs, bool isSyntheticDatalink, bool isPureCwIlluminator, bool isMfr)")
    lines.append("        {")
    lines.append("            EntityId = entityId;")
    lines.append("            DisplayName = displayName;")
    lines.append("            CecTier = cecTier;")
    lines.append("            Datalinks = datalinks;")
    lines.append("            SarhIllumFamily = sarhIllumFamily;")
    lines.append("            IsAwacs = isAwacs;")
    lines.append("            IsSyntheticDatalink = isSyntheticDatalink;")
    lines.append("            IsPureCwIlluminator = isPureCwIlluminator;")
    lines.append("            IsMfr = isMfr;")
    lines.append("        }")
    lines.append("")
    lines.append("        public static readonly RadarProfile DefaultTier3 = new RadarProfile(")
    lines.append("            \"DefaultTier3\", \"Default Tier 3 Radar\", CecTier.Tier3, DatalinkStandard.None, SarhIllumFamily.None, false, false, false, false);")
    lines.append("    }")
    lines.append("")
    lines.append("    public sealed class MissileProfile")
    lines.append("    {")
    lines.append("        public string EntityId { get; }")
    lines.append("        public string DisplayName { get; }")
    lines.append("        public CecTier CecTier { get; }")
    lines.append("        public GuidanceFamily GuidanceFamily { get; }")
    lines.append("        public bool AllowLaunchOnRemote { get; }")
    lines.append("        public bool AllowMidCourseHandoff { get; }")
    lines.append("        public SarhIllumFamily RequiredSarhFamily { get; }")
    lines.append("")
    lines.append("        public MissileProfile(string entityId, string displayName, CecTier cecTier, GuidanceFamily guidanceFamily, bool allowLaunchOnRemote, bool allowMidCourseHandoff, SarhIllumFamily requiredSarhFamily)")
    lines.append("        {")
    lines.append("            EntityId = entityId;")
    lines.append("            DisplayName = displayName;")
    lines.append("            CecTier = cecTier;")
    lines.append("            GuidanceFamily = guidanceFamily;")
    lines.append("            AllowLaunchOnRemote = allowLaunchOnRemote;")
    lines.append("            AllowMidCourseHandoff = allowMidCourseHandoff;")
    lines.append("            RequiredSarhFamily = requiredSarhFamily;")
    lines.append("        }")
    lines.append("")
    lines.append("        public static readonly MissileProfile DefaultTier3 = new MissileProfile(")
    lines.append("            \"DefaultTier3\", \"Default Tier 3 Missile\", CecTier.Tier3, GuidanceFamily.Unknown, false, false, SarhIllumFamily.None);")
    lines.append("    }")
    lines.append("")
    lines.append("    public static partial class CecDataTables")
    lines.append("    {")
    lines.append("        public static readonly Dictionary<string, PlatformProfile> Platforms = new Dictionary<string, PlatformProfile>(StringComparer.OrdinalIgnoreCase)")
    lines.append("        {")

    # Populate Platforms
    plat_entries = {}
    for r, kind in platform_rows:
        eid = r["EntityId"].strip()
        disp = escape_cs_string(r.get("DisplayName", eid).strip())
        tier = parse_tier(r.get("CecTier", ""))
        dl_list = parse_datalinks(r.get("DataLinkType", ""), eid)
        dl_cs = datalinks_to_csharp(dl_list)
        sarh_fam = parse_sarh_family(r.get("GuidanceFamily", ""))
        
        # Gateway logic
        dl_lower = r.get("DataLinkType", "").lower()
        role_lower = r.get("CecRoleOrCapability", "").lower()
        disp_lower = disp.lower()
        eid_lower = eid.lower()
        is_gateway = "gateway" in dl_lower or "gateway" in role_lower or eid_lower in ["usn_e-2d", "fr_e2d", "usn_fa-18f_blk3"] or "e-2d" in disp_lower
        is_gateway_cs = "true" if is_gateway else "false"

        entry_val = f'new PlatformProfile("{escape_cs_string(eid)}", "{disp}", CecTier.{tier}, {dl_cs}, SarhIllumFamily.{sarh_fam}, {is_gateway_cs})'
        k_lower = eid.lower()
        if k_lower not in plat_entries:
            plat_entries[k_lower] = (eid, entry_val)

    # Pass 2: normalized aliases
    for r, kind in platform_rows:
        eid = r["EntityId"].strip()
        norm_eid = normalize_key(eid)
        if norm_eid != eid:
            k_lower = norm_eid.lower()
            if k_lower not in plat_entries:
                orig_eid = r["EntityId"].strip()
                orig_val = plat_entries[orig_eid.lower()][1]
                plat_entries[k_lower] = (norm_eid, orig_val)

    for k_lower, (actual_key, val) in plat_entries.items():
        lines.append(f'            {{ "{escape_cs_string(actual_key)}", {val} }},')

    lines.append("        };")
    lines.append("")

    # Populate Radars
    lines.append("        public static readonly Dictionary<string, RadarProfile> Radars = new Dictionary<string, RadarProfile>(StringComparer.OrdinalIgnoreCase)")
    lines.append("        {")

    radar_entries = {}
    for r in radar_rows:
        eid = r["EntityId"].strip()
        disp = escape_cs_string(r.get("DisplayName", eid).strip())
        tier = parse_tier(r.get("CecTier", ""))
        dl_list = parse_datalinks(r.get("DataLinkType", ""), eid)
        dl_cs = datalinks_to_csharp(dl_list)
        sarh_fam = parse_sarh_family(r.get("GuidanceFamily", ""))
        
        cap = r.get("CecRoleOrCapability", "")
        mfr_match = re.search(r"MFR:\s*(True|False)", cap, re.I)
        awacs_match = re.search(r"AWACS:\s*(True|False)", cap, re.I)
        
        is_mfr = mfr_match and mfr_match.group(1).lower() == "true"
        is_awacs = awacs_match and awacs_match.group(1).lower() == "true"
        is_synth = r.get("DataLinkType", "").strip() == "Synthetic Net Node" or eid in SYNTHETIC_KNOWN or normalize_key(eid) in SYNTHETIC_KNOWN
        is_pure_cw = eid in PURE_CW_KNOWN or normalize_key(eid) in PURE_CW_KNOWN

        is_awacs_cs = "true" if is_awacs else "false"
        is_synth_cs = "true" if is_synth else "false"
        is_pure_cw_cs = "true" if is_pure_cw else "false"
        is_mfr_cs = "true" if is_mfr else "false"

        entry_val = f'new RadarProfile("{escape_cs_string(eid)}", "{disp}", CecTier.{tier}, {dl_cs}, SarhIllumFamily.{sarh_fam}, {is_awacs_cs}, {is_synth_cs}, {is_pure_cw_cs}, {is_mfr_cs})'
        k_lower = eid.lower()
        if k_lower not in radar_entries:
            radar_entries[k_lower] = (eid, entry_val)

    # Pass 2: normalized aliases
    for r in radar_rows:
        eid = r["EntityId"].strip()
        norm_eid = normalize_key(eid)
        if norm_eid != eid:
            k_lower = norm_eid.lower()
            if k_lower not in radar_entries:
                orig_eid = r["EntityId"].strip()
                orig_val = radar_entries[orig_eid.lower()][1]
                radar_entries[k_lower] = (norm_eid, orig_val)

    for k_lower, (actual_key, val) in radar_entries.items():
        lines.append(f'            {{ "{escape_cs_string(actual_key)}", {val} }},')

    lines.append("        };")
    lines.append("")

    # Populate Missiles
    lines.append("        public static readonly Dictionary<string, MissileProfile> Missiles = new Dictionary<string, MissileProfile>(StringComparer.OrdinalIgnoreCase)")
    lines.append("        {")

    missile_entries = {}
    for r in missile_rows:
        eid = r["EntityId"].strip()
        disp = escape_cs_string(r.get("DisplayName", eid).strip())
        tier = parse_tier(r.get("CecTier", ""))
        g_fam = parse_guidance_family(r.get("GuidanceFamily", ""))
        req_sarh = resolve_missile_sarh_family(r, radar_guidance_map)
        
        # Blind Launch on remote allowed for Tier 1 ARH/IR/ARM/Command
        allow_lor = (tier == "Tier1" and g_fam in ["ARH", "IR", "ARM", "Command"])
        # Mid-course handoff allowed for Tier 1 and Tier 2
        allow_handoff = (tier in ["Tier1", "Tier2"])

        allow_lor_cs = "true" if allow_lor else "false"
        allow_handoff_cs = "true" if allow_handoff else "false"

        entry_val = f'new MissileProfile("{escape_cs_string(eid)}", "{disp}", CecTier.{tier}, GuidanceFamily.{g_fam}, {allow_lor_cs}, {allow_handoff_cs}, SarhIllumFamily.{req_sarh})'
        k_lower = eid.lower()
        if k_lower not in missile_entries:
            missile_entries[k_lower] = (eid, entry_val)

    # Pass 2: normalized aliases
    for r in missile_rows:
        eid = r["EntityId"].strip()
        norm_eid = normalize_key(eid)
        if norm_eid != eid:
            k_lower = norm_eid.lower()
            if k_lower not in missile_entries:
                orig_eid = r["EntityId"].strip()
                orig_val = missile_entries[orig_eid.lower()][1]
                missile_entries[k_lower] = (norm_eid, orig_val)

    for k_lower, (actual_key, val) in missile_entries.items():
        lines.append(f'            {{ "{escape_cs_string(actual_key)}", {val} }},')

    lines.append("        };")
    lines.append("")

    # Populate 10x10 Matrix
    lines.append("        public static readonly InteractionTier[,] DatalinkMatrix = new InteractionTier[10, 10]")
    lines.append("        {")
    for sender, vals in matrix:
        val_str = ", ".join(f"InteractionTier.{v}" for v in vals)
        lines.append(f"            /* {sender:22} */ {{ {val_str} }},")
    lines.append("        };")
    lines.append("")

    # Emitting helper methods
    lines.append("        public static InteractionTier GetMatrixInteraction(int senderIdx, int receiverIdx)")
    lines.append("        {")
    lines.append("            if (senderIdx < 0 || senderIdx >= 10 || receiverIdx < 0 || receiverIdx >= 10)")
    lines.append("                return InteractionTier.Incompatible;")
    lines.append("            return DatalinkMatrix[senderIdx, receiverIdx];")
    lines.append("        }")
    lines.append("")
    lines.append("        public static int StandardToIndex(DatalinkStandard s)")
    lines.append("        {")
    lines.append("            switch (s)")
    lines.append("            {")
    for idx, (lbl, enum_name) in enumerate(MATRIX_STANDARDS):
        lines.append(f"                case DatalinkStandard.{enum_name}: return {idx};")
    lines.append("                default: return -1;")
    lines.append("            }")
    lines.append("        }")
    lines.append("")
    lines.append("        public static InteractionTier QueryDatalinkMatrix(DatalinkStandard s1, DatalinkStandard s2)")
    lines.append("        {")
    lines.append("            if (s1 == DatalinkStandard.None || s2 == DatalinkStandard.None)")
    lines.append("                return InteractionTier.Incompatible;")
    lines.append("")
    lines.append("            if (s1 == DatalinkStandard.SatcomUplink || s2 == DatalinkStandard.SatcomUplink)")
    lines.append("                return InteractionTier.SatcomGlobal;")
    lines.append("")
    lines.append("            if (s1 == DatalinkStandard.RuNavalTactical || s2 == DatalinkStandard.RuNavalTactical)")
    lines.append("            {")
    lines.append("                bool s1Ru = s1 == DatalinkStandard.RuNavalTactical || s1 == DatalinkStandard.RuFortTvm || s1 == DatalinkStandard.Tks2Apd518;")
    lines.append("                bool s2Ru = s2 == DatalinkStandard.RuNavalTactical || s2 == DatalinkStandard.RuFortTvm || s2 == DatalinkStandard.Tks2Apd518;")
    lines.append("                return (s1Ru && s2Ru) ? InteractionTier.Tier3_TrackRelay : InteractionTier.Incompatible;")
    lines.append("            }")
    lines.append("")
    lines.append("            int idx1 = StandardToIndex(s1);")
    lines.append("            int idx2 = StandardToIndex(s2);")
    lines.append("            if (idx1 >= 0 && idx2 >= 0)")
    lines.append("                return DatalinkMatrix[idx1, idx2];")
    lines.append("")
    lines.append("            return InteractionTier.Incompatible;")
    lines.append("        }")
    lines.append("")
    lines.append("        public static IEnumerable<DatalinkStandard> GetIndividualStandards(DatalinkStandard standard)")
    lines.append("        {")
    lines.append("            if (standard == DatalinkStandard.None)")
    lines.append("                yield break;")
    lines.append("            for (int i = 0; i < 12; i++)")
    lines.append("            {")
    lines.append("                DatalinkStandard bit = (DatalinkStandard)(1 << i);")
    lines.append("                if ((standard & bit) != 0)")
    lines.append("                    yield return bit;")
    lines.append("            }")
    lines.append("        }")
    lines.append("    }")
    lines.append("}")
    lines.append("")

    OUTPUT_CS.parent.mkdir(parents=True, exist_ok=True)
    with open(OUTPUT_CS, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"Generated {OUTPUT_CS} ({len(lines)} lines).")
    return 0

if __name__ == "__main__":
    is_verify = "--verify" in sys.argv
    ret = generate_code(is_verify)
    sys.exit(ret)
