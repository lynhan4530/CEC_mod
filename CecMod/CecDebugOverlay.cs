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
        private bool _isMouseOver;

        private static Type _attackingStateType;
        private static System.Reflection.FieldInfo _attackingTargetItemField;
        private static System.Reflection.PropertyInfo _attackingAmmoProp;
        private static System.Reflection.PropertyInfo _attackingAmmoNameProp;

        private static ObjectBase _lastHoveredShooter;
        private static ObjectBase _lastHoveredTarget;
        private static Ammunition _lastHoveredAmmo;

        internal static void RecordAttackHover(ObjectBase shooter, ObjectBase target, Ammunition ammo)
        {
            _lastHoveredShooter = shooter;
            _lastHoveredTarget = target;
            _lastHoveredAmmo = ammo;
        }

        private static object GetActiveAttackingState()
        {
            object cur = Globals._mainGameViewModel?.StateManager?.CurrentState?.Value;
            if (cur == null)
                return null;

            Type curType = cur.GetType();
            if (_attackingStateType == null)
            {
                if (curType.Name != "AttackingState")
                    return null;
                _attackingStateType = curType;
                _attackingTargetItemField = AccessTools.Field(curType, "_targetItem");
                _attackingAmmoProp = AccessTools.Property(curType, "Ammo");
                _attackingAmmoNameProp = AccessTools.Property(curType, "Ammunition");
            }

            return curType == _attackingStateType ? cur : null;
        }

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

        private void SetMouseOver(bool over)
        {
            if (_isMouseOver == over)
                return;

            _isMouseOver = over;
            if (!Singleton<MouseControlState>.InstanceExists())
                return;

            if (over)
            {
                Singleton<MouseControlState>.Instance.setMouseIsOverUIWindow(isOverUIWindow: true);
            }
            else if (!DM._isMouseOver)
            {
                Singleton<MouseControlState>.Instance.setMouseIsOverUIWindow(isOverUIWindow: false);
            }
        }

        private void OnDisable()
        {
            SetMouseOver(false);
        }

        private void OnDestroy()
        {
            SetMouseOver(false);
        }

        private void Update()
        {
            // Keep F10 hotkey enabled in developer mode
            if (!DM._allowDMHotkey)
            {
                DM._allowDMHotkey = true;
            }

            // Keyboard shortcut: Ctrl + F10 when DM is active
            if (Input.GetKeyDown(KeyCode.F10))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    IsOpen = !IsOpen;
                    if (!IsOpen)
                        SetMouseOver(false);
                }
            }

            if (!IsOpen && _isMouseOver)
            {
                SetMouseOver(false);
            }
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                SetMouseOver(false);
                return;
            }

            _windowRect = GUI.Window(8899, _windowRect, DrawWindow, "CEC Datalink & Engagement Diagnostics (Ctrl+F10)");
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));

            bool over = IsOpen && _windowRect.Contains(Event.current.mousePosition);
            SetMouseOver(over);
        }

        private void DrawWindow(int windowId)
        {
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width - 30f, 25f));
            if (GUI.Button(new Rect(_windowRect.width - 25f, 3f, 20f, 20f), "X"))
            {
                IsOpen = false;
                SetMouseOver(false);
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
                float horizonNm = RadarCalculator.getRadarHorizon(Mathf.Max(mastY, CecCapability.MinDatalinkMastUnity), target.VisibleAltitude, visual: false, 0f) * 0.036285132f;
                
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
                ObjectBase consort = CecCapability.FindBestTrackingCooperator(selected, target, enforceReach: false);
                if (consort != null)
                {
                    var consortProf = CecCapability.GetPlatformProfile(consort);
                    var interaction = CecCapability.EvaluateInteraction(selected, consort);
                    float linkDist = CecCapability.HorizontalDistance(selected.transform.position, consort.transform.position);
                    float consortDistNm = linkDist * 0.036285132f;
                    bool inReach = CecCapability.ArePlatformsInDatalinkReach(selected, consort, linkDist);
                    float maxReachNm = CecCapability.GetDatalinkMaxReachNm(selected, consort);
                    string maxReachStr = float.IsPositiveInfinity(maxReachNm) ? "SATCOM (Unlimited)" : $"{maxReachNm:F1} nmi";

                    GUILayout.Label($"<b>Consort:</b> {consort.FullIDData()} ({consort._obp?._objectIniName})");
                    GUILayout.Label($"<b>Consort Tier:</b> {FormatTier(consortProf.CecTier)}  |  <b>Link:</b> {FormatDatalinks(consortProf.Datalinks)}");
                    GUILayout.Label($"<b>Consort Range:</b> {consortDistNm:F1} nmi (Max: {maxReachStr})  |  <b>Horizon Reach:</b> {(inReach ? "<color=lime>IN REACH</color>" : "<color=red>OUT OF REACH</color>")}");
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

                DrawWeaponStateSummary(selected, target, ammo);
            }
            else
            {
                GUILayout.Label("<color=grey>Hover or select a contact in AttackingState to evaluate telemetry.</color>");
                if (selected != null && FindDiagnosticWeaponSystem(selected, null, null) != null)
                {
                    DrawWeaponStateSummary(selected, null, GetCurrentAmmunition(selected));
                }
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
                object att = GetActiveAttackingState();
                if (att != null)
                {
                    object targetItem = _attackingTargetItemField?.GetValue(att);
                    if (targetItem is SeapowerUI.MapUnitViewModel muvm
                        && muvm.Unit?.BaseObject != null
                        && !muvm.Unit.BaseObject.IsDestroyed)
                    {
                        return muvm.Unit.BaseObject;
                    }

                    if (_lastHoveredShooter == selected
                        && _lastHoveredTarget != null
                        && !_lastHoveredTarget.IsDestroyed)
                    {
                        return _lastHoveredTarget;
                    }
                }
            }
            catch {}

            var tasks = selected?._currentEngageTasks;
            if (tasks != null)
            {
                for (int i = 0; i < tasks.Count; i++)
                {
                    ObjectBase t = tasks[i]?._targetObject;
                    if (t != null && !t.IsDestroyed)
                        return t;
                }
            }

            var weaponSystems = selected?._obp?._weaponSystems;
            if (weaponSystems != null)
            {
                for (int i = 0; i < weaponSystems.Count; i++)
                {
                    WeaponSystem ws = weaponSystems[i];
                    if (ws != null && ws._executingEngageTask && ws._targetObject != null && !ws._targetObject.IsDestroyed)
                        return ws._targetObject;
                }
            }

            if (selected?._ai?._objectToAttack != null && !selected._ai._objectToAttack.IsDestroyed)
                return selected._ai._objectToAttack;

            return null;
        }

        private static Ammunition GetCurrentAmmunition(ObjectBase selected)
        {
            if (selected == null) return null;
            try
            {
                object att = GetActiveAttackingState();
                if (att != null)
                {
                    if (_attackingAmmoProp?.GetValue(att) is Ammunition directAmmo && directAmmo._ap != null)
                        return directAmmo;

                    string ammoName = _attackingAmmoNameProp?.GetValue(att) as string;
                    if (!string.IsNullOrEmpty(ammoName))
                    {
                        Ammunition byName = selected.getAmmunitionByName(ammoName);
                        if (byName != null)
                            return byName;
                    }

                    if (_lastHoveredShooter == selected && _lastHoveredAmmo != null)
                        return _lastHoveredAmmo;
                }
            }
            catch {}

            var tasks = selected._currentEngageTasks;
            if (tasks != null)
            {
                for (int i = 0; i < tasks.Count; i++)
                {
                    Ammunition taskAmmo = tasks[i]?._ammunition;
                    if (taskAmmo?._ap != null)
                        return taskAmmo;
                }
            }

            var weaponSystems = selected._obp?._weaponSystems;
            if (weaponSystems != null)
            {
                for (int i = 0; i < weaponSystems.Count; i++)
                {
                    WeaponSystem ws = weaponSystems[i];
                    if (ws != null && ws._executingEngageTask && ws._ammoForEngage?._ap != null)
                        return ws._ammoForEngage;
                }
            }

            return null;
        }

        private static void DrawWeaponStateSummary(ObjectBase shooter, ObjectBase target, Ammunition ammo)
        {
            WeaponSystem ws = FindDiagnosticWeaponSystem(shooter, target, ammo);
            if (ws == null)
                return;

            string section = ws._sectionName ?? ws._name ?? "Weapon";
            string sysName = ws._systemName ?? "?";
            GUILayout.Label($"<b>Weapon State:</b> {section} ({sysName}) -> {FormatEngageState(ws)}  |  <b>Exec:</b> {(ws._executingEngageTask ? "YES" : "No")}  |  <b>Aligned:</b> {(ws._sensorPreLaunchAligned ? "YES" : "No")}");

            if (target != null && !target.IsDestroyed)
            {
                try
                {
                    float relDeg = ComputeRelativeBearingDeg(shooter, target.transform.position);
                    bool inArc = ws.targetIsInFiringArc(target.transform.position);
                    string arcStr = FormatFiringArcs(ws._vwp);
                    GUILayout.Label($"<b>Target Bearing:</b> {relDeg:+0.0;-0.0;0.0}°  |  <b>Firing Arc:</b> {arcStr} ({(inArc ? "<color=lime>IN ARC</color>" : "<color=red>OUT OF ARC</color>")})");
                }
                catch (Exception ex)
                {
                    CecPlugin.Log?.LogDebug($"[CEC] DrawWeaponStateSummary arc check failed: {ex.Message}");
                }
            }
        }

        private static WeaponSystem FindDiagnosticWeaponSystem(ObjectBase shooter, ObjectBase target, Ammunition ammo)
        {
            var weaponSystems = shooter?._obp?._weaponSystems;
            if (weaponSystems == null)
                return null;

            WeaponSystem firstExecuting = null;
            for (int i = 0; i < weaponSystems.Count; i++)
            {
                WeaponSystem ws = weaponSystems[i];
                if (ws == null || !ws._executingEngageTask)
                    continue;

                if (target == null || ws._targetObject == target)
                    return ws;

                if (firstExecuting == null)
                    firstExecuting = ws;
            }

            if (firstExecuting != null)
                return firstExecuting;

            string ammoName = ammo?._ap?._ammunitionFileName;
            if (string.IsNullOrEmpty(ammoName))
                return null;

            for (int i = 0; i < weaponSystems.Count; i++)
            {
                WeaponSystem ws = weaponSystems[i];
                if (ws == null || ws.Inoperable.Value || ws._isOffline || ws._isEmpty)
                    continue;

                if (ws.getLoadedAmmoCount(ammoName) > 0 || ws.getMagazineAmmoCount(ammoName) > 0)
                    return ws;
            }

            return null;
        }

        private static float ComputeRelativeBearingDeg(ObjectBase shooter, Vector3 targetPos)
        {
            if (shooter.IsAirUnit)
            {
                Transform frame = shooter._obp?._visibleObjectsParent != null
                    ? shooter._obp._visibleObjectsParent.transform
                    : shooter.transform;
                Vector3 local = frame.InverseTransformDirection(targetPos - frame.position);
                return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            }

            Vector3 delta = targetPos - shooter.transform.position;
            return Mathf.DeltaAngle(shooter.transform.eulerAngles.y, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg);
        }

        private static string FormatFiringArcs(WeaponParameters vwp)
        {
            var arcs = vwp?._firingArcs;
            if (arcs == null || arcs.Count == 0 || arcs[0] == Vector2.zero)
                return "360°";

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < arcs.Count; i++)
            {
                if (arcs[i] == Vector2.zero)
                    continue;
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append($"{arcs[i].x:0.#}°..{arcs[i].y:0.#}°");
            }
            return sb.Length > 0 ? sb.ToString() : "360°";
        }

        private static string FormatEngageState(WeaponSystem ws)
        {
            WeaponSystem.EngageState state = ws._engageState;
            if (!ws._executingEngageTask)
                return $"<color=grey>{state}</color>";

            switch (state)
            {
                case WeaponSystem.EngageState.Launch:
                case WeaponSystem.EngageState.LaunchingSalvo:
                case WeaponSystem.EngageState.Firing:
                    return $"<color=lime>{state}</color>";

                case WeaponSystem.EngageState.TargetIsNotInFiringArcs:
                case WeaponSystem.EngageState.TargetIsNotInVerticalArc:
                case WeaponSystem.EngageState.TargetTooFar:
                case WeaponSystem.EngageState.TargetTooFarToOpenSystem:
                case WeaponSystem.EngageState.NoLOSToTarget:
                case WeaponSystem.EngageState.TargetIsBeyondFCRHorizon:
                case WeaponSystem.EngageState.TargetIsNotDetectableByFCR:
                case WeaponSystem.EngageState.TargetIsNotDetectableBySearchSensor:
                case WeaponSystem.EngageState.MissileCanNotReachTarget:
                case WeaponSystem.EngageState.PrelaunchSensorAlignment:
                case WeaponSystem.EngageState.PrelaunchSensorCheck:
                case WeaponSystem.EngageState.LauncherTooLow:
                case WeaponSystem.EngageState.LauncherTooHigh:
                case WeaponSystem.EngageState.TargetTooClose:
                case WeaponSystem.EngageState.EngagementNotAllowed:
                case WeaponSystem.EngageState.NotInRearAspect:
                    return $"<color=red>{state}</color>";

                default:
                    return $"<color=yellow>{state}</color>";
            }
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
                var consort = CecCapability.FindBestTrackingCooperator(shooter, target, enforceReach: false);
                sb.Append($" | Target: {target.FullIDData()} | LocalSight: {localSight}");
                if (consort != null)
                {
                    bool inReach = CecCapability.ArePlatformsInDatalinkReach(
                        shooter, consort, CecCapability.HorizontalDistance(shooter.transform.position, consort.transform.position));
                    sb.Append($" | Consort: {consort.FullIDData()} (InReach: {inReach})");
                }
                else
                {
                    sb.Append(" | Consort: None");
                }
            }

            var wpnAmmo = GetCurrentAmmunition(shooter);
            var ws = FindDiagnosticWeaponSystem(shooter, target, wpnAmmo);
            if (ws != null)
            {
                sb.Append($" | Wpn: {ws._sectionName ?? ws._name}={ws._engageState} (Exec:{ws._executingEngageTask}, Aligned:{ws._sensorPreLaunchAligned}");
                if (target != null && !target.IsDestroyed)
                {
                    try
                    {
                        bool inArc = ws.targetIsInFiringArc(target.transform.position);
                        sb.Append($", InArc:{inArc}");
                    }
                    catch {}
                }
                sb.Append(")");
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
                // Row 7 (num4 = 7f) has Col 0 (Missile Test), Col 1 (Light AI Log), Col 2 (Map Labels), Col 3 (Airbases); Col 4 is empty.
                float num = 15f;
                float num2 = 35f;
                float num3 = 15f;
                float num4 = 7f;
                float num5 = 92f;
                float num6 = 4f;

                Rect cecBtnRect = new Rect(4f * (num5 + num6) + num, num3 + num2 * num4, num5, 20f);
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
        [HarmonyPrefix]
        public static void DoToolbarWindowPrefix()
        {
            try
            {
                if (!DM._showToolbar)
                    return;

                // Top developer toolbar: place compact CEC toggle in the empty slot below the "All"/"UI" state label
                // (after Obj at x=502..532, left of the control-buttons toggle at x=585), before GUI.DragWindow runs.
                Rect btnRect = new Rect(535f, 25f, 46f, 20f);
                if (GUI.Button(btnRect, CecDebugOverlay.IsOpen ? "<color=lime>CEC</color>" : "CEC"))
                {
                    CecDebugOverlay.IsOpen = !CecDebugOverlay.IsOpen;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log?.LogWarning($"[CEC] DoToolbarWindowPrefix error: {ex.Message}");
            }
        }
    }
}
