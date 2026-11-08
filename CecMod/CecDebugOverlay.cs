using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using SeaPower;
using UnityEngine;

namespace CecMod
{
    /// <summary>
    /// Interactive real-time diagnostic overlay and developer debug functions integrated
    /// with Sea Power's F10 Developer Menu (<see cref="SeaPower.DM"/>).
    /// </summary>
    public class CecDebugOverlay : MonoBehaviour
    {
        public static CecDebugOverlay Instance { get; private set; }

        public static bool IsOpen = false;

        private static Rect _windowRect = new Rect(720f, 60f, 540f, 620f);
        private static Vector2 _scrollPos = Vector2.zero;

        private void Awake()
        {
            Instance = this;
            try
            {
                // Ensure Sea Power's F10 Developer Menu hotkey is enabled
                DM._allowDMHotkey = true;
            }
            catch (Exception ex)
            {
                CecPlugin.Log?.LogWarning($"[CEC] Could not set DM._allowDMHotkey: {ex.Message}");
            }
        }

        private void Update()
        {
            // Keep F10 hotkey enabled in developer mode
            if (!DM._allowDMHotkey)
            {
                DM._allowDMHotkey = true;
            }

            // Keyboard shortcut: Ctrl + F10 or F10 when DM is active
            if (Input.GetKeyDown(KeyCode.F10))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    IsOpen = !IsOpen;
                }
            }
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            // Handle mouse over to prevent map click-through
            if (_windowRect.Contains(Event.current.mousePosition))
            {
                if (Singleton<MouseControlState>.InstanceExists())
                {
                    Singleton<MouseControlState>.Instance.setMouseIsOverUIWindow(isOverUIWindow: true);
                }
            }

            _windowRect = GUI.Window(8899, _windowRect, DrawWindow, "CEC Datalink & Engagement Diagnostics (F10 Menu)");
        }

        private void DrawWindow(int windowId)
        {
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width - 30f, 25f));
            if (GUI.Button(new Rect(_windowRect.width - 25f, 3f, 20f, 20f), "X"))
            {
                IsOpen = false;
                return;
            }

            GUILayout.BeginArea(new Rect(10f, 25f, _windowRect.width - 20f, _windowRect.height - 35f));
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            ObjectBase selected = Singleton<RenderPosition>.InstanceExists()
                ? Singleton<RenderPosition>.Instance.getSelectedObject()
                : null;

            // 1. Shooter Platform Telemetry
            GUILayout.Label("<color=cyan><b>=== SHOOTER (SELECTED PLATFORM) ===</b></color>");
            if (selected != null)
            {
                string iniName = selected._obp?._objectIniName ?? "Unknown";
                var profile = CecCapability.GetPlatformProfile(selected);
                var network = CecCapability.GetPlatformNetwork(selected);
                bool airEmcon = CecCapability.IsPlatformInAirRadarEmcon(selected);
                bool surfEmcon = CecCapability.IsPlatformInSurfaceRadarEmcon(selected);

                GUILayout.Label($"<b>Unit:</b> {selected.FullIDData()} ({iniName})");
                GUILayout.Label($"<b>CEC Tier:</b> {FormatTier(profile.CecTier)}");
                GUILayout.Label($"<b>Datalinks:</b> {FormatDatalinks(profile.Datalinks)}");
                GUILayout.Label($"<b>Network:</b> {network}  |  <b>Gateway:</b> {(profile.IsGateway ? "<color=lime>YES</color>" : "No")}");
                
                string emconAirStr = airEmcon ? "<color=yellow>ACTIVE (RADARS OFF)</color>" : "<color=lime>RADARS ON</color>";
                string emconSurfStr = surfEmcon ? "<color=yellow>ACTIVE (RADARS OFF)</color>" : "<color=lime>RADARS ON</color>";
                GUILayout.Label($"<b>Air EMCON:</b> {emconAirStr}  |  <b>Surface EMCON:</b> {emconSurfStr}");

                // Radars list
                DrawRadarSummary(selected);
            }
            else
            {
                GUILayout.Label("<color=grey>No platform currently selected.</color>");
            }

            GUILayout.Space(10f);

            // 2. Aimed / Hovered Target Telemetry
            GUILayout.Label("<color=cyan><b>=== TARGET EVALUATION ===</b></color>");
            ObjectBase target = GetAimedOrHoveredTarget(selected);
            if (selected != null && target != null)
            {
                float distNm = CecCapability.HorizontalDistance(selected.transform.position, target.transform.position) * 0.036285132f;
                float altFt = target.transform.position.y * 220.47266f;
                float mastY = CecCapability.DatalinkMastHeight(selected, requireRadarOn: false);
                float horizonNm = RadarCalculator.getRadarHorizon(Mathf.Max(mastY, 10f), target.VisibleAltitude, visual: false, 0f) * 0.036285132f;
                
                bool spottedLocally = selected._spottedObjects != null && selected._spottedObjects.Contains(target);
                bool localSight = CecCapability.HasLocalRadarSight(selected, target);

                GUILayout.Label($"<b>Target:</b> {target.FullIDData()}");
                GUILayout.Label($"<b>Distance:</b> {distNm:F1} nmi  |  <b>Alt:</b> {altFt:F0} ft");
                GUILayout.Label($"<b>Radar Horizon:</b> {horizonNm:F1} nmi (Mast: {mastY * 220.47266f:F0} ft)");
                GUILayout.Label($"<b>Organic Spot:</b> {(spottedLocally ? "<color=lime>YES</color>" : "<color=yellow>NO (Consort Sighted)</color>")}");
                GUILayout.Label($"<b>Local Radar Sight:</b> {(localSight ? "<color=lime>TRUE</color>" : "<color=yellow>FALSE (Cooperative Track Required)</color>")}");

                GUILayout.Space(5f);

                // 3. Best Cooperative Consort
                GUILayout.Label("<color=cyan><b>=== COOPERATIVE CONSORT ===</b></color>");
                ObjectBase consort = CecCapability.FindBestTrackingCooperator(selected, target);
                if (consort != null)
                {
                    var consortProf = CecCapability.GetPlatformProfile(consort);
                    var interaction = CecCapability.EvaluateInteraction(selected, consort);
                    float consortDistNm = CecCapability.HorizontalDistance(selected.transform.position, consort.transform.position) * 0.036285132f;
                    bool inReach = CecCapability.ArePlatformsInDatalinkReach(selected, consort, CecCapability.HorizontalDistance(selected.transform.position, consort.transform.position));

                    GUILayout.Label($"<b>Consort:</b> {consort.FullIDData()} ({consort._obp?._objectIniName})");
                    GUILayout.Label($"<b>Consort Tier:</b> {FormatTier(consortProf.CecTier)}  |  <b>Link:</b> {FormatDatalinks(consortProf.Datalinks)}");
                    GUILayout.Label($"<b>Consort Range:</b> {consortDistNm:F1} nmi  |  <b>Horizon Reach:</b> {(inReach ? "<color=lime>IN REACH</color>" : "<color=red>OUT OF REACH</color>")}");
                    GUILayout.Label($"<b>Interaction:</b> <color=lime>{interaction}</color>");
                }
                else
                {
                    GUILayout.Label("<color=red>No cooperative consort currently tracking this target.</color>");
                }

                GUILayout.Space(5f);

                // 4. Weapon & Telemetry Tag
                GUILayout.Label("<color=cyan><b>=== WEAPON GATING & HOVER TELEMETRY ===</b></color>");
                Ammunition ammo = GetCurrentAmmunition(selected);
                if (ammo?._ap != null)
                {
                    var missileProf = CecCapability.GetMissileProfile(ammo._ap);
                    bool canLaunchRemote = CecCapability.CanPerformLaunchOnRemote(selected, ammo._ap, target);
                    string hoverTag = CecCapability.GetHoverTelemetryTag(selected, ammo._ap, target);

                    GUILayout.Label($"<b>Ammo:</b> {ammo._ap._ammunitionFileName} ({missileProf.GuidanceFamily})");
                    GUILayout.Label($"<b>Launch on Remote:</b> {(canLaunchRemote ? "<color=lime>ALLOWED</color>" : "<color=red>BLOCKED</color>")}");
                    GUILayout.Label($"<b>Tooltip Tag:</b> {CleanTagForDisplay(hoverTag)}");
                }
                else
                {
                    GUILayout.Label("<color=grey>No attack ammunition currently selected.</color>");
                }
            }
            else
            {
                GUILayout.Label("<color=grey>Hover or select a contact in AttackingState to evaluate telemetry.</color>");
            }

            GUILayout.Space(15f);

            // 5. Interactive Action Buttons
            GUILayout.Label("<color=cyan><b>=== ACTIONS ===</b></color>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Log Snapshot to F10 Event Log"))
            {
                SendSnapshotToDmLog(selected, target);
            }
            if (selected != null && GUILayout.Button("Toggle Ship EMCON"))
            {
                ToggleShipEmcon(selected);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void DrawRadarSummary(ObjectBase platform)
        {
            var obp = platform?._obp;
            if (obp == null) return;

            int airTotal = obp._airSearchRadars?.Count ?? 0;
            int airOn = 0;
            if (obp._airSearchRadars != null)
            {
                for (int i = 0; i < obp._airSearchRadars.Count; i++)
                {
                    var r = obp._airSearchRadars[i];
                    if (r != null && r.IsOn.Value && !r.Inoperable.Value) airOn++;
                }
            }

            int surfTotal = obp._surfaceSearchRadars?.Count ?? 0;
            int surfOn = 0;
            if (obp._surfaceSearchRadars != null)
            {
                for (int i = 0; i < obp._surfaceSearchRadars.Count; i++)
                {
                    var r = obp._surfaceSearchRadars[i];
                    if (r != null && r.IsOn.Value && !r.Inoperable.Value) surfOn++;
                }
            }

            int fcrTotal = 0, fcrOn = 0;
            if (obp._sensorSystems != null)
            {
                for (int i = 0; i < obp._sensorSystems.Count; i++)
                {
                    if (obp._sensorSystems[i] is SensorSystemRadar r)
                    {
                        if (r._type == SensorSystemRadar.RadarType.Targeting || CecCapability.IsPureCwDishIlluminator(r._systemName))
                        {
                            fcrTotal++;
                            if (r.IsOn.Value && !r.Inoperable.Value) fcrOn++;
                        }
                    }
                }
            }

            GUILayout.Label($"<b>Air Search:</b> {airOn}/{airTotal} Active  |  <b>Surface:</b> {surfOn}/{surfTotal} Active  |  <b>FCR:</b> {fcrOn}/{fcrTotal} Active");
        }

        private static void ToggleShipEmcon(ObjectBase platform)
        {
            try
            {
                bool newEmcon = !platform.Emcon.Value;
                platform.setEMCON(newEmcon, message: true);
                LogToDm($"[CEC] Toggled EMCON for {platform.FullIDData()} -> {(newEmcon ? "ACTIVE (Radars Off)" : "INACTIVE (Radars On)")}", "yellow");
            }
            catch (Exception ex)
            {
                CecPlugin.Log?.LogWarning($"[CEC] ToggleShipEmcon error: {ex.Message}");
            }
        }

        private static ObjectBase GetAimedOrHoveredTarget(ObjectBase selected)
        {
            try
            {
                var cur = Globals._mainGameViewModel?.StateManager?.CurrentState;
                if (cur != null && cur.GetType().Name == "AttackingState")
                {
                    var targetProp = AccessTools.Property(cur.GetType(), "TargetItem") ?? AccessTools.Field(cur.GetType(), "_targetItem") as System.Reflection.MemberInfo;
                    object targetItem = (targetProp is System.Reflection.PropertyInfo pi)
                        ? pi.GetValue(cur)
                        : (targetProp is System.Reflection.FieldInfo fi ? fi.GetValue(cur) : null);

                    if (targetItem is SeapowerUI.MapUnitViewModel muvm && muvm.Unit?.BaseObject != null)
                    {
                        return muvm.Unit.BaseObject;
                    }
                }
            }
            catch {}

            if (selected?._ai?._objectToAttack != null) return selected._ai._objectToAttack;
            return null;
        }

        private static Ammunition GetCurrentAmmunition(ObjectBase selected)
        {
            if (selected == null) return null;
            try
            {
                var cur = Globals._mainGameViewModel?.StateManager?.CurrentState;
                if (cur != null && cur.GetType().Name == "AttackingState")
                {
                    var ammoProp = AccessTools.Property(cur.GetType(), "Ammunition");
                    string ammoName = ammoProp?.GetValue(cur) as string;
                    if (!string.IsNullOrEmpty(ammoName))
                    {
                        return selected.getAmmunitionByName(ammoName);
                    }
                }
            }
            catch {}
            return null;
        }

        public static void LogToDm(string message, string color = "lime")
        {
            try
            {
                DM.Log(message, DM.EventLogFilters.General, color);
            }
            catch (Exception ex)
            {
                CecPlugin.Log?.LogWarning($"[CEC] DM.Log error: {ex.Message}");
            }
        }

        private static void SendSnapshotToDmLog(ObjectBase shooter, ObjectBase target)
        {
            if (shooter == null)
            {
                LogToDm("[CEC] Snapshot: No shooter selected.", "red");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append($"[CEC Diagnostic] Shooter: {shooter.FullIDData()} | Tier: {CecCapability.GetPlatformProfile(shooter).CecTier}");
            if (target != null)
            {
                bool localSight = CecCapability.HasLocalRadarSight(shooter, target);
                var consort = CecCapability.FindBestTrackingCooperator(shooter, target);
                sb.Append($" | Target: {target.FullIDData()} | LocalSight: {localSight}");
                sb.Append($" | Consort: {(consort != null ? consort.FullIDData() : "None")}");
            }
            LogToDm(sb.ToString(), "cyan");
        }

        private static string FormatTier(CecTier tier)
        {
            switch (tier)
            {
                case CecTier.Tier1: return "<color=lime>Tier 1 (Full CEC / NIFC-CA)</color>";
                case CecTier.Tier2: return "<color=yellow>Tier 2 (Cue Only / Handoff)</color>";
                case CecTier.Tier3: return "<color=orange>Tier 3 (Organic Only / Standalone)</color>";
                default: return "<color=grey>Tier 0 (No Datalink)</color>";
            }
        }

        private static string FormatDatalinks(DatalinkStandard links)
        {
            if (links == DatalinkStandard.None) return "None";
            List<string> names = new List<string>();
            foreach (DatalinkStandard s in CecDataTables.GetIndividualStandards(links))
            {
                names.Add(s.ToString());
            }
            return string.Join(", ", names);
        }

        private static string CleanTagForDisplay(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return "None";
            return tag.Replace("<LineBreak/>", " ")
                .Replace("<Run Foreground=\"#81C784\">", "[")
                .Replace("<Run Foreground=\"#FFF176\">", "[")
                .Replace("<Run Foreground=\"#4FC3F7\">", "[")
                .Replace("</Run>", "]");
        }
    }

    /// <summary>
    /// Patches Sea Power's Developer Menu (<see cref="SeaPower.DM"/>) to inject CEC options into F10 UI.
    /// </summary>
    [HarmonyPatch]
    public static class DmIntegrationPatches
    {
        [HarmonyPatch(typeof(DM), "MenuWindow")]
        [HarmonyPostfix]
        public static void MenuWindowPostfix()
        {
            try
            {
                // Sea Power MenuWindow grid layout:
                // num = 15f; num2 = 35f; num3 = 15f; num4 = 7f; num5 = 92f; num6 = 4f;
                // Row 7 (num4 = 7f) has Col 0 (Missile Test), Col 1 (Light AI), and Col 2 is empty!
                float num = 15f;
                float num2 = 35f;
                float num3 = 15f;
                float num4 = 7f;
                float num5 = 92f;
                float num6 = 4f;

                Rect cecBtnRect = new Rect(2f * (num5 + num6) + num, num3 + num2 * num4, num5, 20f);
                if (GUI.Button(cecBtnRect, CecDebugOverlay.IsOpen ? "<color=lime>CEC Datalink</color>" : "CEC Datalink"))
                {
                    CecDebugOverlay.IsOpen = !CecDebugOverlay.IsOpen;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log?.LogWarning($"[CEC] MenuWindowPostfix error: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(DM), "DoToolbarWindow")]
        [HarmonyPostfix]
        public static void DoToolbarWindowPostfix()
        {
            try
            {
                // Top developer toolbar: add a compact CEC toggle button at x=460f (after weapon status & allow control)
                if (GUI.Button(new Rect(460f, 3f, 42f, 40f), CecDebugOverlay.IsOpen ? "<color=lime>CEC</color>" : "CEC"))
                {
                    CecDebugOverlay.IsOpen = !CecDebugOverlay.IsOpen;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log?.LogWarning($"[CEC] DoToolbarWindowPostfix error: {ex.Message}");
            }
        }
    }
}
