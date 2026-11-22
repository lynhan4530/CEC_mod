#!/usr/bin/env python3
"""
Generates the interactive GitHub Pages HTML viewer (`docs/index.html`)
directly from the verified `data/cec_tables/*.csv` files, with per-column
search and dropdown filtering on every table (except the 10x10 matrix).
"""

import csv
import json
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
CSV_DIR = REPO_ROOT / "data" / "cec_tables"


def load_csv(filename):
    path = CSV_DIR / filename
    with open(path, mode="r", encoding="utf-8-sig") as f:
        return list(csv.DictReader(f))


def clean_mod_name(name):
    if not name:
        return "Vanilla / Community"
    if "Pacific 2027" in name:
        return "Modern Pack"
    return name


def clean_str(val):
    if not val:
        return ""
    return val.replace(" (P2027)", "")


def build_data():
    matrix_rows = load_csv("02_Datalink_Compatibility_Matrix.csv")
    core_tiers = []
    for r in load_csv("01_Core_CEC_matrix.csv"):
        if r.get("Tier") == "Tier 0":
            continue
        core_tiers.append({
            "tier": r.get("Tier", ""),
            "name": r.get("Tier Name", ""),
            "lor": r.get("Remote Launch? (LaunchOnRemote)", ""),
            "rescue": r.get("Mid-Course Handoff? (DatalinkRescue)", ""),
            "minRadar": r.get("Minimum Radar Required", ""),
            "typicalWeapon": r.get("Typical Seeker / Weapon", ""),
        })

    glossary = []
    for r in load_csv("00_Glossary.csv"):
        if r.get("Acronym") == "P2027":
            continue
        glossary.append({
            "acronym": r.get("Acronym", ""),
            "fullTerm": r.get("Full Term", ""),
            "definition": r.get("Definition / Context in Mod", ""),
        })

    ships = []
    for r in load_csv("03_Ships.csv"):
        ships.append({
            "name": clean_str(r.get("DisplayName", "")),
            "variants": r.get("VariantsOrCarriersList", ""),
            "id": r.get("EntityId", ""),
            "classType": r.get("Category", ""),
            "nation": r.get("Nations", ""),
            "tier": r.get("CecTier", "Tier 3"),
            "datalink": r.get("DataLinkType", ""),
            "arch": r.get("ArchitectureGroup", ""),
            "sarh": r.get("GuidanceFamily", "") or "—",
            "sensors": clean_str(r.get("LinkedSensors", "")),
            "weapons": clean_str(r.get("LinkedWeapons", "")),
            "mod": clean_mod_name(r.get("SourceModName", "")),
        })

    aircraft = []
    for r in load_csv("04_Aircraft.csv"):
        aircraft.append({
            "name": clean_str(r.get("DisplayName", "")),
            "variants": r.get("VariantsOrCarriersList", ""),
            "id": r.get("EntityId", ""),
            "roleType": r.get("Role", ""),
            "nation": r.get("Nations", ""),
            "tier": r.get("CecTier", "Tier 3"),
            "datalink": r.get("DataLinkType", ""),
            "arch": r.get("ArchitectureGroup", ""),
            "sensors": clean_str(r.get("LinkedSensors", "")),
            "weapons": clean_str(r.get("LinkedWeapons", "")),
            "mod": clean_mod_name(r.get("SourceModName", "")),
        })

    radars = []
    for r in load_csv("05_Radars.csv"):
        radars.append({
            "name": clean_str(r.get("DisplayName", "")),
            "id": clean_str(r.get("EntityId", "")),
            "band": r.get("SecondaryName", ""),
            "role": f"{r.get('Category', '')} ({r.get('Role', '')})",
            "tier": r.get("CecTier", "Tier 3"),
            "datalink": r.get("DataLinkType", ""),
            "sarh": r.get("GuidanceFamily", "") or "—",
            "specs": r.get("CecRoleOrCapability", ""),
            "arch": r.get("ArchitectureGroup", ""),
            "mod": clean_mod_name(r.get("SourceModName", "")),
        })

    missiles = []
    for r in load_csv("06_Missiles.csv"):
        missiles.append({
            "name": clean_str(r.get("DisplayName", "")),
            "id": r.get("EntityId", ""),
            "cat": r.get("Category", ""),
            "seeker": r.get("GuidanceFamily", ""),
            "tier": r.get("CecTier", "Tier 3"),
            "datalink": r.get("DataLinkType", ""),
            "cap": r.get("CecRoleOrCapability", ""),
            "sensors": clean_str(r.get("LinkedSensors", "")),
            "carriers": clean_str(r.get("VariantsOrCarriersList", "")),
            "mod": clean_mod_name(r.get("SourceModName", "")),
        })

    return {
        "coreTiers": core_tiers,
        "matrix": matrix_rows,
        "ships": ships,
        "aircraft": aircraft,
        "radars": radars,
        "missiles": missiles,
        "glossary": glossary,
    }


HTML_TEMPLATE = """<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Sea Power CEC (v0.9.0) — Interactive Datalink Matrix & Unit Database</title>
  <style>
    :root {
      --bg-main: #070d17;
      --bg-panel: #0d1726;
      --bg-card: #122036;
      --bg-hover: #182b47;
      --border: #203758;
      --text-main: #e6f0ff;
      --text-muted: #8fa6c9;
      --accent-cyan: #4fc3f7;
      --accent-green: #81c784;
      --accent-yellow: #fff176;
      --accent-orange: #ffb74d;
      --accent-red: #e57373;
      --accent-purple: #ba68c8;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, Helvetica, Arial, sans-serif;
      background: var(--bg-main);
      color: var(--text-main);
      line-height: 1.5;
      padding-bottom: 48px;
    }
    header {
      background: linear-gradient(180deg, #0f203c 0%, #070d17 100%);
      border-bottom: 2px solid var(--border);
      padding: 28px 24px 20px;
      text-align: center;
    }
    header h1 {
      font-size: 1.9rem;
      letter-spacing: 1.2px;
      color: var(--accent-cyan);
      text-transform: uppercase;
      margin-bottom: 6px;
    }
    header p {
      color: var(--text-muted);
      font-size: 0.96rem;
      max-width: 880px;
      margin: 0 auto 16px;
    }
    .stats-bar {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: 12px;
      margin-top: 10px;
    }
    .stat-pill {
      background: var(--bg-card);
      border: 1px solid var(--border);
      border-radius: 6px;
      padding: 6px 14px;
      font-size: 0.85rem;
      color: var(--text-muted);
    }
    .stat-pill strong {
      color: var(--accent-cyan);
      font-size: 0.98rem;
      margin-right: 4px;
    }
    .container {
      max-width: 1560px;
      margin: 0 auto;
      padding: 20px;
    }
    .nav-tabs {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-bottom: 20px;
      border-bottom: 1px solid var(--border);
      padding-bottom: 12px;
    }
    .tab-btn {
      background: var(--bg-panel);
      color: var(--text-muted);
      border: 1px solid var(--border);
      border-radius: 6px;
      padding: 10px 18px;
      font-size: 0.92rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .tab-btn:hover {
      background: var(--bg-hover);
      color: var(--text-main);
    }
    .tab-btn.active {
      background: #143357;
      color: var(--accent-cyan);
      border-color: var(--accent-cyan);
      box-shadow: 0 0 10px rgba(79, 195, 247, 0.2);
    }
    .filter-bar {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      background: var(--bg-panel);
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 12px 14px;
      margin-bottom: 14px;
      align-items: center;
    }
    .filter-bar input {
      background: var(--bg-main);
      color: var(--text-main);
      border: 1px solid var(--border);
      border-radius: 6px;
      padding: 8px 12px;
      font-size: 0.9rem;
      flex: 1 1 280px;
    }
    .filter-bar input:focus, .col-filter:focus {
      outline: none;
      border-color: var(--accent-cyan);
    }
    .reset-btn {
      background: #172a45;
      color: var(--accent-cyan);
      border: 1px solid var(--border);
      border-radius: 6px;
      padding: 8px 14px;
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
    }
    .reset-btn:hover {
      background: #1e375a;
      border-color: var(--accent-cyan);
    }
    .count-label {
      font-size: 0.86rem;
      color: var(--text-muted);
      margin-left: auto;
    }
    .table-wrap {
      overflow-x: auto;
      background: var(--bg-panel);
      border: 1px solid var(--border);
      border-radius: 8px;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.85rem;
      text-align: left;
    }
    th {
      background: #102138;
      color: var(--accent-cyan);
      padding: 10px 10px;
      font-weight: 600;
      border-bottom: 1px solid var(--border);
      white-space: nowrap;
    }
    tr.filter-row th {
      background: #0b1829;
      padding: 6px 8px 9px;
      border-bottom: 2px solid var(--border);
    }
    .col-filter {
      width: 100%;
      min-width: 85px;
      background: var(--bg-main);
      color: var(--text-main);
      border: 1px solid #264269;
      border-radius: 4px;
      padding: 5px 7px;
      font-size: 0.78rem;
      font-family: inherit;
    }
    td {
      padding: 9px 10px;
      border-bottom: 1px solid #162842;
      vertical-align: top;
    }
    tbody tr:hover td {
      background: var(--bg-hover);
    }
    code {
      font-family: Consolas, 'Courier New', monospace;
      background: rgba(79, 195, 247, 0.08);
      color: #9be2ff;
      padding: 2px 5px;
      border-radius: 4px;
      font-size: 0.8rem;
    }
    .badge {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 4px;
      font-weight: 600;
      font-size: 0.78rem;
      white-space: nowrap;
    }
    .tier-1 { background: rgba(79, 195, 247, 0.16); color: var(--accent-cyan); border: 1px solid rgba(79, 195, 247, 0.45); }
    .tier-2 { background: rgba(129, 199, 132, 0.16); color: var(--accent-green); border: 1px solid rgba(129, 199, 132, 0.45); }
    .tier-3 { background: rgba(255, 241, 118, 0.14); color: var(--accent-yellow); border: 1px solid rgba(255, 241, 118, 0.4); }
    .tier-0 { background: rgba(143, 166, 201, 0.14); color: var(--text-muted); border: 1px solid rgba(143, 166, 201, 0.3); }
    .gw-req { background: rgba(186, 104, 200, 0.18); color: #e1bee7; border: 1px solid rgba(186, 104, 200, 0.45); }
    .intra-fl { background: rgba(79, 195, 247, 0.12); color: #80deea; border: 1px dashed rgba(79, 195, 247, 0.5); }
    .na-cell { color: #4a607f; font-size: 0.78rem; }
    .section-title {
      font-size: 1.15rem;
      color: var(--accent-cyan);
      margin: 18px 0 10px;
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .sub-note {
      font-size: 0.85rem;
      color: var(--text-muted);
      margin-bottom: 14px;
    }
    .hidden { display: none !important; }
    .truncate {
      max-width: 260px;
      font-size: 0.79rem;
      color: var(--text-muted);
    }
  </style>
</head>
<body>
  <header>
    <h1>Cooperative Engagement Capability (CEC) — v0.9.0</h1>
    <p>Interactive Datalink Compatibility Matrix & Verified Platform / Radar / Missile Capability Database for <em>Sea Power: Naval Combat in the Missile Age</em></p>
    <div class="stats-bar">
      <div class="stat-pill"><strong>10×10</strong> Pairwise Datalink Matrix</div>
      <div class="stat-pill"><strong>344</strong> Ships</div>
      <div class="stat-pill"><strong>134</strong> Aircraft</div>
      <div class="stat-pill"><strong>294</strong> Radars</div>
      <div class="stat-pill"><strong>232</strong> Missiles</div>
    </div>
  </header>

  <div class="container">
    <div class="nav-tabs">
      <button class="tab-btn active" onclick="switchTab('matrix')">10×10 Datalink Matrix & Tiers</button>
      <button class="tab-btn" onclick="switchTab('ships')">Ships (344)</button>
      <button class="tab-btn" onclick="switchTab('aircraft')">Aircraft (134)</button>
      <button class="tab-btn" onclick="switchTab('radars')">Radars (294)</button>
      <button class="tab-btn" onclick="switchTab('missiles')">Missiles (232)</button>
      <button class="tab-btn" onclick="switchTab('glossary')">Glossary</button>
    </div>

    <!-- TAB 1: MATRIX -->
    <div id="tab-matrix" class="tab-pane">
      <h2 class="section-title">3-Tier CEC Capability Rules</h2>
      <div class="table-wrap" style="margin-bottom: 24px;">
        <table id="core-tier-table">
          <thead id="core-tier-thead"></thead>
          <tbody id="core-tier-tbody"></tbody>
        </table>
      </div>

      <h2 class="section-title">10×10 Pairwise Datalink Standard Compatibility Matrix</h2>
      <p class="sub-note">
        When two units carry multiple datalink standards, <code>CecMod</code> evaluates all pairwise standard combinations and selects the highest interaction tier.
        <strong>Gateway Required</strong> pairs upgrade to <strong>Tier 1 - Full CEC</strong> when an active airborne Gateway (e.g. <em>E-2D Hawkeye, F/A-18E/F Block III</em>) is in radio-horizon reach of both units.
      </p>
      <div class="table-wrap">
        <table id="matrix-table"></table>
      </div>
    </div>

    <!-- TAB 2..6: FILTERABLE TABLES WITH PER-COLUMN SEARCH/FILTER -->
    <div id="tab-entities" class="tab-pane hidden">
      <div class="filter-bar">
        <input type="text" id="global-search" placeholder="Global search across all columns in this table..." oninput="renderTableBody(currentTab)" />
        <button class="reset-btn" onclick="resetColumnFilters(currentTab)">Clear All Filters</button>
        <span class="count-label" id="row-count"></span>
      </div>
      <div class="table-wrap">
        <table id="entity-table">
          <thead id="entity-thead"></thead>
          <tbody id="entity-tbody"></tbody>
        </table>
      </div>
    </div>
  </div>

  <script>
    const DB = __JSON_PAYLOAD__;
    let currentTab = 'matrix';

    // Column schemas for every table (with per-column 'text' or 'select' filter)
    const SCHEMAS = {
      coreTiers: [
        { key: 'tier', label: 'Tier', type: 'select', render: r => tierBadge(r.tier) },
        { key: 'name', label: 'Classification', type: 'text', placeholder: 'Filter class...', render: r => `<strong>${esc(r.name)}</strong>` },
        { key: 'lor', label: 'Launch-on-Remote (OTH / EMCON)', type: 'text', placeholder: 'Filter LOR...', render: r => esc(r.lor) },
        { key: 'rescue', label: 'Mid-Course Handoff / Rescue', type: 'text', placeholder: 'Filter handoff...', render: r => esc(r.rescue) },
        { key: 'minRadar', label: 'Minimum Radar Required', type: 'text', placeholder: 'Filter radar...', render: r => esc(r.minRadar) },
        { key: 'typicalWeapon', label: 'Typical Munitions', type: 'text', placeholder: 'Filter munition...', render: r => esc(r.typicalWeapon) }
      ],
      ships: [
        { key: 'name', label: 'Class / Display Name & Hulls', type: 'text', placeholder: 'Search class or hull...', matchKeys: ['name', 'variants'], render: r => `<strong>${esc(r.name)}</strong><div class="truncate">${esc(r.variants)}</div>` },
        { key: 'id', label: 'INI ID', type: 'text', placeholder: 'Search INI ID...', render: r => `<code>${esc(r.id)}</code>` },
        { key: 'classType', label: 'Type', type: 'select', render: r => esc(r.classType) },
        { key: 'nation', label: 'Nation', type: 'select', render: r => esc(r.nation) },
        { key: 'tier', label: 'CEC Tier', type: 'select', render: r => tierBadge(r.tier) },
        { key: 'datalink', label: 'Datalink Standard', type: 'select', render: r => esc(r.datalink) },
        { key: 'arch', label: 'Doctrinal Group', type: 'select', render: r => esc(r.arch) },
        { key: 'sarh', label: 'SARH Family', type: 'select', render: r => `<code>${esc(r.sarh)}</code>` },
        { key: 'sensors', label: 'Radars & FCR', type: 'text', placeholder: 'Search radar/FCR...', render: r => `<div class="truncate">${esc(r.sensors)}</div>` },
        { key: 'weapons', label: 'CEC Missiles', type: 'text', placeholder: 'Search missile...', render: r => `<div class="truncate">${esc(r.weapons)}</div>` },
        { key: 'mod', label: 'Source Mod', type: 'select', render: r => esc(r.mod) }
      ],
      aircraft: [
        { key: 'name', label: 'Aircraft Name & Squadrons', type: 'text', placeholder: 'Search aircraft...', matchKeys: ['name', 'variants'], render: r => `<strong>${esc(r.name)}</strong><div class="truncate">${esc(r.variants)}</div>` },
        { key: 'id', label: 'INI ID', type: 'text', placeholder: 'Search INI ID...', render: r => `<code>${esc(r.id)}</code>` },
        { key: 'roleType', label: 'Role', type: 'select', render: r => esc(r.roleType) },
        { key: 'nation', label: 'Nation', type: 'select', render: r => esc(r.nation) },
        { key: 'tier', label: 'CEC Tier', type: 'select', render: r => tierBadge(r.tier) },
        { key: 'datalink', label: 'Datalink Standard', type: 'select', render: r => esc(r.datalink) },
        { key: 'arch', label: 'Doctrinal Group', type: 'select', render: r => esc(r.arch) },
        { key: 'sensors', label: 'Radars', type: 'text', placeholder: 'Search radar...', render: r => `<div class="truncate">${esc(r.sensors)}</div>` },
        { key: 'weapons', label: 'Networked Weapons', type: 'text', placeholder: 'Search weapon...', render: r => `<div class="truncate">${esc(r.weapons)}</div>` },
        { key: 'mod', label: 'Source Mod', type: 'select', render: r => esc(r.mod) }
      ],
      radars: [
        { key: 'name', label: 'Radar Name', type: 'text', placeholder: 'Search radar name...', render: r => `<strong>${esc(r.name)}</strong>` },
        { key: 'id', label: 'INI ID', type: 'text', placeholder: 'Search INI ID...', render: r => `<code>${esc(r.id)}</code>` },
        { key: 'band', label: 'Band / Mode', type: 'select', render: r => esc(r.band) },
        { key: 'role', label: 'Role', type: 'select', render: r => esc(r.role) },
        { key: 'tier', label: 'CEC Tier', type: 'select', render: r => tierBadge(r.tier) },
        { key: 'datalink', label: 'Datalink Role', type: 'select', render: r => esc(r.datalink) },
        { key: 'sarh', label: 'SARH Family', type: 'select', render: r => `<code>${esc(r.sarh)}</code>` },
        { key: 'specs', label: 'Specs (Range / Ch / MFR / AWACS)', type: 'text', placeholder: 'e.g. AWACS: True...', render: r => `<div class="truncate">${esc(r.specs)}</div>` },
        { key: 'arch', label: 'Doctrinal Group', type: 'select', render: r => esc(r.arch) },
        { key: 'mod', label: 'Source Mod', type: 'select', render: r => esc(r.mod) }
      ],
      missiles: [
        { key: 'name', label: 'Missile Name', type: 'text', placeholder: 'Search missile...', render: r => `<strong>${esc(r.name)}</strong>` },
        { key: 'id', label: 'INI ID', type: 'text', placeholder: 'Search INI ID...', render: r => `<code>${esc(r.id)}</code>` },
        { key: 'cat', label: 'Category', type: 'select', render: r => esc(r.cat) },
        { key: 'seeker', label: 'Seeker / Family', type: 'select', render: r => `<code>${esc(r.seeker)}</code>` },
        { key: 'tier', label: 'CEC Tier', type: 'select', render: r => tierBadge(r.tier) },
        { key: 'datalink', label: 'Mid-Course Link', type: 'select', render: r => esc(r.datalink) },
        { key: 'cap', label: 'CEC Capability', type: 'select', render: r => esc(r.cap) },
        { key: 'sensors', label: 'Compatible Radars / Illuminators', type: 'text', placeholder: 'Search radar...', render: r => `<div class="truncate">${esc(r.sensors)}</div>` },
        { key: 'carriers', label: 'Carriers (Ships / Aircraft)', type: 'text', placeholder: 'Search carrier...', render: r => `<div class="truncate">${esc(r.carriers)}</div>` },
        { key: 'mod', label: 'Source Mod', type: 'select', render: r => esc(r.mod) }
      ],
      glossary: [
        { key: 'acronym', label: 'Acronym', type: 'text', placeholder: 'Filter acronym...', render: r => `<strong><code>${esc(r.acronym)}</code></strong>` },
        { key: 'fullTerm', label: 'Full Term', type: 'text', placeholder: 'Filter term...', render: r => `<strong>${esc(r.fullTerm)}</strong>` },
        { key: 'definition', label: 'Definition & Context in CecMod', type: 'text', placeholder: 'Filter definition...', render: r => esc(r.definition) }
      ]
    };

    // Store active column filter values per tab
    const colFilterState = {
      coreTiers: {},
      ships: {},
      aircraft: {},
      radars: {},
      missiles: {},
      glossary: {}
    };

    function tierBadge(val) {
      if (!val) return '<span class="na-cell">—</span>';
      if (val.includes('Tier 1') && val.includes('Intra-Flight')) return `<span class="badge intra-fl">${val}</span>`;
      if (val.includes('Tier 1')) return `<span class="badge tier-1">${val}</span>`;
      if (val.includes('Tier 2')) return `<span class="badge tier-2">${val}</span>`;
      if (val.includes('Tier 3')) return `<span class="badge tier-3">${val}</span>`;
      if (val.includes('Gateway')) return `<span class="badge gw-req">${val}</span>`;
      if (val === 'N/A') return `<span class="na-cell">N/A</span>`;
      return `<span class="badge tier-0">${val}</span>`;
    }

    function esc(s) {
      if (!s) return '';
      return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function buildTableHeader(tabKey, theadId) {
      const schema = SCHEMAS[tabKey];
      const rows = DB[tabKey] || [];
      const state = colFilterState[tabKey];

      let h1 = '<tr>';
      let h2 = '<tr class="filter-row">';

      schema.forEach(col => {
        h1 += `<th>${esc(col.label)}</th>`;
        const curVal = state[col.key] || '';
        if (col.type === 'select') {
          const uniq = Array.from(new Set(rows.map(r => r[col.key]).filter(Boolean))).sort();
          let opts = `<option value="">All</option>`;
          uniq.forEach(u => {
            const sel = (u === curVal) ? ' selected' : '';
            opts += `<option value="${esc(u)}"${sel}>${esc(u)}</option>`;
          });
          h2 += `<th><select class="col-filter" data-tab="${tabKey}" data-key="${col.key}" onchange="onColFilterChange(this)">${opts}</select></th>`;
        } else {
          h2 += `<th><input type="text" class="col-filter" data-tab="${tabKey}" data-key="${col.key}" value="${esc(curVal)}" placeholder="${esc(col.placeholder || 'Filter...')}" oninput="onColFilterChange(this)" /></th>`;
        }
      });

      h1 += '</tr>';
      h2 += '</tr>';
      document.getElementById(theadId).innerHTML = h1 + h2;
    }

    function onColFilterChange(el) {
      const tabKey = el.getAttribute('data-tab');
      const colKey = el.getAttribute('data-key');
      colFilterState[tabKey][colKey] = el.value;
      renderTableBody(tabKey);
    }

    function resetColumnFilters(tabKey) {
      colFilterState[tabKey] = {};
      const gSearch = document.getElementById('global-search');
      if (gSearch) gSearch.value = '';
      const theadId = (tabKey === 'coreTiers') ? 'core-tier-thead' : 'entity-thead';
      buildTableHeader(tabKey, theadId);
      renderTableBody(tabKey);
    }

    function renderTableBody(tabKey) {
      const schema = SCHEMAS[tabKey];
      const rows = DB[tabKey] || [];
      const state = colFilterState[tabKey];
      const isEntityPane = (tabKey !== 'coreTiers');
      const globalQ = isEntityPane ? (document.getElementById('global-search').value || '').toLowerCase().trim() : '';

      const filtered = rows.filter(r => {
        if (globalQ && !Object.values(r).some(v => String(v).toLowerCase().includes(globalQ))) {
          return false;
        }
        for (const col of schema) {
          const fVal = (state[col.key] || '').trim();
          if (!fVal) continue;
          if (col.type === 'select') {
            if (String(r[col.key] || '') !== fVal) return false;
          } else {
            const qLow = fVal.toLowerCase();
            const keys = col.matchKeys || [col.key];
            const matched = keys.some(k => String(r[k] || '').toLowerCase().includes(qLow));
            if (!matched) return false;
          }
        }
        return true;
      });

      if (isEntityPane) {
        document.getElementById('row-count').textContent = `Showing ${filtered.length} of ${rows.length}`;
      }

      const tbodyId = (tabKey === 'coreTiers') ? 'core-tier-tbody' : 'entity-tbody';
      let html = '';
      filtered.forEach(r => {
        html += '<tr>';
        schema.forEach(col => {
          html += `<td>${col.render(r)}</td>`;
        });
        html += '</tr>';
      });
      document.getElementById(tbodyId).innerHTML = html;
    }

    function initMatrix() {
      // 3-Tier table with per-column filters
      buildTableHeader('coreTiers', 'core-tier-thead');
      renderTableBody('coreTiers');

      // 10x10 Matrix (static lookup matrix, no column filters per user request)
      const cols = Object.keys(DB.matrix[0]);
      let mt = '<thead><tr>';
      cols.forEach(c => { mt += `<th>${esc(c)}</th>`; });
      mt += '</tr></thead><tbody>';
      DB.matrix.forEach(r => {
        mt += '<tr>';
        cols.forEach((c, idx) => {
          if (idx === 0) {
            mt += `<td><strong>${esc(r[c])}</strong></td>`;
          } else {
            mt += `<td>${tierBadge(r[c])}</td>`;
          }
        });
        mt += '</tr>';
      });
      mt += '</tbody>';
      document.getElementById('matrix-table').innerHTML = mt;
    }

    function switchTab(tab) {
      currentTab = tab;
      document.querySelectorAll('.tab-btn').forEach((btn, idx) => {
        const map = ['matrix', 'ships', 'aircraft', 'radars', 'missiles', 'glossary'];
        btn.classList.toggle('active', map[idx] === tab);
      });
      const isMatrix = (tab === 'matrix');
      document.getElementById('tab-matrix').classList.toggle('hidden', !isMatrix);
      document.getElementById('tab-entities').classList.toggle('hidden', isMatrix);

      if (!isMatrix) {
        document.getElementById('global-search').value = '';
        buildTableHeader(tab, 'entity-thead');
        renderTableBody(tab);
      }
    }

    initMatrix();
  </script>
</body>
</html>
"""


def main():
    data = build_data()
    payload = json.dumps(data, ensure_ascii=False)
    html = HTML_TEMPLATE.replace("__JSON_PAYLOAD__", payload)

    out_docs = REPO_ROOT / "docs" / "index.html"
    out_docs.write_text(html, encoding="utf-8")
    print(f"Wrote GitHub Pages viewer: {out_docs} ({out_docs.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
