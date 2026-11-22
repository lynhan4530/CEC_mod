#!/usr/bin/env python3
"""
Generates the interactive GitHub Pages HTML viewer (`docs/index.html` and `index.html`)
directly from the verified `data/cec_tables/*.csv` files.
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
    core_tiers = [r for r in load_csv("01_Core_CEC_matrix.csv") if r.get("Tier") != "Tier 0"]
    glossary = [r for r in load_csv("00_Glossary.csv") if r.get("Acronym") != "P2027"]

    ships = []
    for r in load_csv("03_Ships.csv"):
        ships.append({
            "id": r.get("EntityId", ""),
            "name": clean_str(r.get("DisplayName", "")),
            "classType": r.get("Category", ""),
            "nation": r.get("Nations", ""),
            "arch": r.get("ArchitectureGroup", ""),
            "tier": r.get("CecTier", "Tier 3"),
            "datalink": r.get("DataLinkType", ""),
            "role": r.get("CecRoleOrCapability", ""),
            "sarh": r.get("GuidanceFamily", "") or "—",
            "sensors": clean_str(r.get("LinkedSensors", "")),
            "weapons": clean_str(r.get("LinkedWeapons", "")),
            "variants": r.get("VariantsOrCarriersList", ""),
            "mod": clean_mod_name(r.get("SourceModName", "")),
        })

    aircraft = []
    for r in load_csv("04_Aircraft.csv"):
        aircraft.append({
            "id": r.get("EntityId", ""),
            "name": clean_str(r.get("DisplayName", "")),
            "roleType": r.get("Role", ""),
            "nation": r.get("Nations", ""),
            "arch": r.get("ArchitectureGroup", ""),
            "tier": r.get("CecTier", "Tier 3"),
            "datalink": r.get("DataLinkType", ""),
            "role": r.get("CecRoleOrCapability", ""),
            "sarh": r.get("GuidanceFamily", "") or "—",
            "sensors": clean_str(r.get("LinkedSensors", "")),
            "weapons": clean_str(r.get("LinkedWeapons", "")),
            "variants": r.get("VariantsOrCarriersList", ""),
            "mod": clean_mod_name(r.get("SourceModName", "")),
        })

    radars = []
    for r in load_csv("05_Radars.csv"):
        radars.append({
            "id": clean_str(r.get("EntityId", "")),
            "rawId": r.get("EntityId", ""),
            "name": clean_str(r.get("DisplayName", "")),
            "band": r.get("SecondaryName", ""),
            "cat": r.get("Category", ""),
            "targetType": r.get("Role", ""),
            "arch": r.get("ArchitectureGroup", ""),
            "tier": r.get("CecTier", "Tier 3"),
            "datalink": r.get("DataLinkType", ""),
            "specs": r.get("CecRoleOrCapability", ""),
            "sarh": r.get("GuidanceFamily", "") or "—",
            "mod": clean_mod_name(r.get("SourceModName", "")),
        })

    missiles = []
    for r in load_csv("06_Missiles.csv"):
        missiles.append({
            "id": r.get("EntityId", ""),
            "name": clean_str(r.get("DisplayName", "")),
            "cat": r.get("Category", ""),
            "role": r.get("Role", ""),
            "arch": r.get("ArchitectureGroup", ""),
            "tier": r.get("CecTier", "Tier 3"),
            "datalink": r.get("DataLinkType", ""),
            "cap": r.get("CecRoleOrCapability", ""),
            "seeker": r.get("GuidanceFamily", ""),
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
      max-width: 1480px;
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
      padding: 14px;
      margin-bottom: 18px;
      align-items: center;
    }
    .filter-bar input, .filter-bar select {
      background: var(--bg-main);
      color: var(--text-main);
      border: 1px solid var(--border);
      border-radius: 6px;
      padding: 8px 12px;
      font-size: 0.9rem;
    }
    .filter-bar input {
      flex: 1 1 280px;
    }
    .filter-bar select {
      min-width: 170px;
    }
    .filter-bar input:focus, .filter-bar select:focus {
      outline: none;
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
      font-size: 0.86rem;
      text-align: left;
    }
    th {
      background: #102138;
      color: var(--accent-cyan);
      padding: 11px 12px;
      font-weight: 600;
      border-bottom: 2px solid var(--border);
      white-space: nowrap;
      position: sticky;
      top: 0;
    }
    td {
      padding: 9px 12px;
      border-bottom: 1px solid #162842;
      vertical-align: top;
    }
    tr:hover td {
      background: var(--bg-hover);
    }
    code {
      font-family: Consolas, 'Courier New', monospace;
      background: rgba(79, 195, 247, 0.08);
      color: #9be2ff;
      padding: 2px 5px;
      border-radius: 4px;
      font-size: 0.81rem;
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
      max-width: 280px;
      font-size: 0.8rem;
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
      <div class="stat-pill"><strong id="stat-ships">344</strong> Ships</div>
      <div class="stat-pill"><strong id="stat-aircraft">134</strong> Aircraft</div>
      <div class="stat-pill"><strong id="stat-radars">294</strong> Radars</div>
      <div class="stat-pill"><strong id="stat-missiles">232</strong> Missiles</div>
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
        <table id="core-tier-table"></table>
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

    <!-- TAB 2..5: FILTERABLE ENTITY TABLES -->
    <div id="tab-entities" class="tab-pane hidden">
      <div class="filter-bar">
        <input type="text" id="search-input" placeholder="Search by unit name, INI ID, radar, weapon, variant, or mod..." oninput="renderActiveEntityTab()" />
        <select id="tier-filter" onchange="renderActiveEntityTab()">
          <option value="">All CEC Tiers</option>
          <option value="Tier 1">Tier 1 — Full CEC / LOR</option>
          <option value="Tier 2">Tier 2 — Mid-Course Cooperative</option>
          <option value="Tier 3">Tier 3 — Local FCR Only</option>
        </select>
        <select id="arch-filter" onchange="renderActiveEntityTab()">
          <option value="">All Doctrinal Networks</option>
        </select>
        <select id="mod-filter" onchange="renderActiveEntityTab()">
          <option value="">All Mods / Sources</option>
        </select>
        <span class="count-label" id="row-count"></span>
      </div>
      <div class="table-wrap">
        <table id="entity-table"></table>
      </div>
    </div>

    <!-- TAB 6: GLOSSARY -->
    <div id="tab-glossary" class="tab-pane hidden">
      <div class="filter-bar">
        <input type="text" id="glossary-search" placeholder="Search acronym or definition..." oninput="renderGlossary()" />
      </div>
      <div class="table-wrap">
        <table id="glossary-table"></table>
      </div>
    </div>
  </div>

  <script>
    const DB = __JSON_PAYLOAD__;
    let currentTab = 'matrix';

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
      return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function initMatrix() {
      // Core Tiers
      let ct = '<thead><tr><th>Tier</th><th>Classification</th><th>Launch-on-Remote (OTH / EMCON)</th><th>Mid-Course Handoff / Rescue</th><th>Minimum Radar Required</th><th>Typical Munitions</th></tr></thead><tbody>';
      DB.coreTiers.forEach(r => {
        ct += `<tr>
          <td>${tierBadge(r['Tier'])}</td>
          <td><strong>${esc(r['Tier Name'])}</strong></td>
          <td>${esc(r['Remote Launch? (LaunchOnRemote)'])}</td>
          <td>${esc(r['Mid-Course Handoff? (DatalinkRescue)'])}</td>
          <td>${esc(r['Minimum Radar Required'])}</td>
          <td>${esc(r['Typical Seeker / Weapon'])}</td>
        </tr>`;
      });
      ct += '</tbody>';
      document.getElementById('core-tier-table').innerHTML = ct;

      // 10x10 Matrix
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

    function populateFilters(rows) {
      const archSet = new Set();
      const modSet = new Set();
      rows.forEach(r => {
        if (r.arch) archSet.add(r.arch);
        if (r.mod) modSet.add(r.mod);
      });
      const archSel = document.getElementById('arch-filter');
      const curArch = archSel.value;
      archSel.innerHTML = '<option value="">All Doctrinal Networks</option>' +
        Array.from(archSet).sort().map(a => `<option value="${esc(a)}">${esc(a)}</option>`).join('');
      archSel.value = Array.from(archSet).includes(curArch) ? curArch : '';

      const modSel = document.getElementById('mod-filter');
      const curMod = modSel.value;
      modSel.innerHTML = '<option value="">All Mods / Sources</option>' +
        Array.from(modSet).sort().map(m => `<option value="${esc(m)}">${esc(m)}</option>`).join('');
      modSel.value = Array.from(modSet).includes(curMod) ? curMod : '';
    }

    function switchTab(tab) {
      currentTab = tab;
      document.querySelectorAll('.tab-btn').forEach((btn, idx) => {
        const map = ['matrix', 'ships', 'aircraft', 'radars', 'missiles', 'glossary'];
        btn.classList.toggle('active', map[idx] === tab);
      });
      document.getElementById('tab-matrix').classList.toggle('hidden', tab !== 'matrix');
      document.getElementById('tab-entities').classList.toggle('hidden', !['ships', 'aircraft', 'radars', 'missiles'].includes(tab));
      document.getElementById('tab-glossary').classList.toggle('hidden', tab !== 'glossary');

      if (['ships', 'aircraft', 'radars', 'missiles'].includes(tab)) {
        populateFilters(DB[tab]);
        renderActiveEntityTab();
      } else if (tab === 'glossary') {
        renderGlossary();
      }
    }

    function renderActiveEntityTab() {
      const rows = DB[currentTab] || [];
      const q = (document.getElementById('search-input').value || '').toLowerCase().trim();
      const tierF = document.getElementById('tier-filter').value;
      const archF = document.getElementById('arch-filter').value;
      const modF = document.getElementById('mod-filter').value;

      const filtered = rows.filter(r => {
        if (tierF && r.tier !== tierF) return false;
        if (archF && r.arch !== archF) return false;
        if (modF && r.mod !== modF) return false;
        if (!q) return true;
        return Object.values(r).some(v => String(v).toLowerCase().includes(q));
      });

      document.getElementById('row-count').textContent = `Showing ${filtered.length} of ${rows.length}`;
      const tbl = document.getElementById('entity-table');

      if (currentTab === 'ships') {
        let html = '<thead><tr><th>Class / Display Name</th><th>INI ID</th><th>Type</th><th>Nation</th><th>CEC Tier</th><th>Datalink Standard</th><th>Doctrinal Group</th><th>SARH Family</th><th>Radars & FCR</th><th>CEC Missiles</th><th>Source Mod</th></tr></thead><tbody>';
        filtered.forEach(r => {
          html += `<tr>
            <td><strong>${esc(r.name)}</strong><div class="truncate">${esc(r.variants)}</div></td>
            <td><code>${esc(r.id)}</code></td>
            <td>${esc(r.classType)}</td>
            <td>${esc(r.nation)}</td>
            <td>${tierBadge(r.tier)}</td>
            <td>${esc(r.datalink)}</td>
            <td>${esc(r.arch)}</td>
            <td><code>${esc(r.sarh)}</code></td>
            <td class="truncate">${esc(r.sensors)}</td>
            <td class="truncate">${esc(r.weapons)}</td>
            <td>${esc(r.mod)}</td>
          </tr>`;
        });
        tbl.innerHTML = html + '</tbody>';
      } else if (currentTab === 'aircraft') {
        let html = '<thead><tr><th>Aircraft Name</th><th>INI ID</th><th>Role</th><th>Nation</th><th>CEC Tier</th><th>Datalink Standard</th><th>Doctrinal Group</th><th>Radars</th><th>Networked Weapons</th><th>Source Mod</th></tr></thead><tbody>';
        filtered.forEach(r => {
          html += `<tr>
            <td><strong>${esc(r.name)}</strong><div class="truncate">${esc(r.variants)}</div></td>
            <td><code>${esc(r.id)}</code></td>
            <td>${esc(r.roleType)}</td>
            <td>${esc(r.nation)}</td>
            <td>${tierBadge(r.tier)}</td>
            <td>${esc(r.datalink)}</td>
            <td>${esc(r.arch)}</td>
            <td class="truncate">${esc(r.sensors)}</td>
            <td class="truncate">${esc(r.weapons)}</td>
            <td>${esc(r.mod)}</td>
          </tr>`;
        });
        tbl.innerHTML = html + '</tbody>';
      } else if (currentTab === 'radars') {
        let html = '<thead><tr><th>Radar Name</th><th>INI ID</th><th>Band / Mode</th><th>Role</th><th>CEC Tier</th><th>Datalink Role</th><th>SARH Family</th><th>Specs (Range / Channels / MFR / AWACS)</th><th>Doctrinal Group</th><th>Source Mod</th></tr></thead><tbody>';
        filtered.forEach(r => {
          html += `<tr>
            <td><strong>${esc(r.name)}</strong></td>
            <td><code>${esc(r.id)}</code></td>
            <td>${esc(r.band)}</td>
            <td>${esc(r.cat)} (${esc(r.targetType)})</td>
            <td>${tierBadge(r.tier)}</td>
            <td>${esc(r.datalink)}</td>
            <td><code>${esc(r.sarh)}</code></td>
            <td class="truncate">${esc(r.specs)}</td>
            <td>${esc(r.arch)}</td>
            <td>${esc(r.mod)}</td>
          </tr>`;
        });
        tbl.innerHTML = html + '</tbody>';
      } else if (currentTab === 'missiles') {
        let html = '<thead><tr><th>Missile Name</th><th>INI ID</th><th>Category</th><th>Seeker / Family</th><th>CEC Tier</th><th>Mid-Course Link</th><th>CEC Capability</th><th>Compatible Radars / Illuminators</th><th>Carriers</th><th>Source Mod</th></tr></thead><tbody>';
        filtered.forEach(r => {
          html += `<tr>
            <td><strong>${esc(r.name)}</strong></td>
            <td><code>${esc(r.id)}</code></td>
            <td>${esc(r.cat)}</td>
            <td><code>${esc(r.seeker)}</code></td>
            <td>${tierBadge(r.tier)}</td>
            <td>${esc(r.datalink)}</td>
            <td>${esc(r.cap)}</td>
            <td class="truncate">${esc(r.sensors)}</td>
            <td class="truncate">${esc(r.carriers)}</td>
            <td>${esc(r.mod)}</td>
          </tr>`;
        });
        tbl.innerHTML = html + '</tbody>';
      }
    }

    function renderGlossary() {
      const q = (document.getElementById('glossary-search').value || '').toLowerCase().trim();
      const rows = DB.glossary.filter(r => !q || Object.values(r).some(v => String(v).toLowerCase().includes(q)));
      let html = '<thead><tr><th>Acronym</th><th>Full Term</th><th>Definition & Context in CecMod</th></tr></thead><tbody>';
      rows.forEach(r => {
        html += `<tr>
          <td><strong><code>${esc(r['Acronym'])}</code></strong></td>
          <td><strong>${esc(r['Full Term'])}</strong></td>
          <td>${esc(r['Definition / Context in Mod'])}</td>
        </tr>`;
      });
      document.getElementById('glossary-table').innerHTML = html + '</tbody>';
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
