using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SeaPower.WeaponAllocation;
using SeapowerUI.ViewModels;
using UniRx;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;

namespace SeaPower;

public class Taskforce
{
	public enum TfType
	{
		None,
		Player,
		Ally,
		Enemy,
		Neutral
	}

	private static readonly ProfilerMarker _profMissileGroupsUpdate = new ProfilerMarker("Taskforce: Missile groups update");

	private static readonly ProfilerMarker _profAirstrikeUpdate = new ProfilerMarker("Taskforce: Airstrike update");

	private static readonly ProfilerMarker _profUnitGroupsUpdate = new ProfilerMarker("Taskforce: Unit groups update");

	private static readonly ProfilerMarker _profLinkUpdate = new ProfilerMarker("Taskforce: Link update");

	private static readonly ProfilerMarker _profPlottingTables = new ProfilerMarker("Taskforce: Plotting Tables");

	private static readonly ProfilerMarker _profAirstrikeFixedUpdate = new ProfilerMarker("Taskforce: Airstrike FixedUpdate");

	public SensorSystemsLink _sensorSystemsLink;

	public string _nameInMissionFile = "";

	public List<ObjectBase> _taskforceDisabledUnits = new List<ObjectBase>();

	public List<ObjectBase> _taskforceLandUnits = new List<ObjectBase>();

	public List<ObjectBase> _taskforceVessels = new List<ObjectBase>();

	public List<ObjectBase> _taskforceSubmarines = new List<ObjectBase>();

	public List<ObjectBase> _taskforceBiologics = new List<ObjectBase>();

	public List<ObjectBase> _taskforceAircraft = new List<ObjectBase>();

	public List<ObjectBase> _taskforceHelicopters = new List<ObjectBase>();

	public ObservableCollection<ObjectBase> _taskforceUnits = new ObservableCollection<ObjectBase>();

	public List<ObjectBase> _taskforceWeapons = new List<ObjectBase>();

	public List<ObjectBase> _taskforceAirstrikeTargets = new List<ObjectBase>();

	public List<ObjectBase> _identifiedSpyUnits = new List<ObjectBase>();

	public Dictionary<ObjectBase, (float launchTime, float hitTime)> _taskforceMissileTargets = new Dictionary<ObjectBase, (float, float)>();

	public Dictionary<ObjectBase, (float launchTime, float hitTime)> _taskforceTorpedoTargets = new Dictionary<ObjectBase, (float, float)>();

	public WeaponAllocator TaskforceAllocator = new WeaponAllocator();

	public PlottingTable PlottingTable;

	public List<MissileGroup> _missileGroups = new List<MissileGroup>();

	public List<AirStrike> _airStrikes = new List<AirStrike>();

	public List<PatrolZone> _patrolZones = new List<PatrolZone>();

	public ObservableCollection<UnitFormation> Formations = new ObservableCollection<UnitFormation>();

	public Dictionary<string, List<ObjectBase>> _usedCallsigns = new Dictionary<string, List<ObjectBase>>();

	public bool _isOnAlert;

	public float _noHostileContactsTime = -1f;

	public float _maintenanceTime = -1f;

	public List<Aircraft> _activeAirSupport = new List<Aircraft>();

	public List<(string iniName, string squadron, string loadout)> _airSupportList = new List<(string, string, string)>();

	public (GeoPosition position, float heading) _airSupportSpawnPoint;

	public float _airSupportTimeStart = -10000f;

	public float _airSupportInterval = 900f;

	public float _airSupportReloadTimeStart = -10000f;

	public float _airSupportReloadInterval = 1200f;

	public float _aiCheckTime = -10000f;

	private Dictionary<ObjectBase, (int id, float reportTime)> _fadedContactsToReport = new Dictionary<ObjectBase, (int, float)>();

	private List<ObjectBase> _newContactsToReport = new List<ObjectBase>();

	private List<ObjectBase> _classifiedContactsToReport = new List<ObjectBase>();

	public float _newContactsToReportTime = -1000f;

	public float _classifiedContactsToReportTime = -1000f;

	public float _fadedCContactsToReportTime = -1000f;

	public float _reconDataCheckTime = -1000f;

	public List<(float completionTime, List<ObjectBase> objectsToSpot)> _reconDataAnalyze = new List<(float, List<ObjectBase>)>();

	public List<ObjectBase> _spottedObjects = new List<ObjectBase>();

	public MissionManager.ROE _roe = MissionManager.ROE.Restricted;

	public float _killScore;

	public float _lossScore;

	public float _civKillScore;

	public float _neutralMilitaryKillScore;

	public List<UnitRegister> _unitStatusRegister = new List<UnitRegister>();

	public List<UnitRegister> _tmpUnitStatusRegister = new List<UnitRegister>();

	public Dictionary<string, WeaponsSpent> _weaponsSpent = new Dictionary<string, WeaponsSpent>();

	public int _survivorsResqued;

	public TfType Side { get; private set; }

	public string Nation { get; private set; } = "";

	public bool WPactAffinity { get; private set; }

	public string VoiceoverNation
	{
		get
		{
			if (_taskforceLandUnits.Count > 0 && _taskforceLandUnits[0] != null)
			{
				return _taskforceLandUnits[0].Nation.Value;
			}
			if (_taskforceVessels.Count > 0 && _taskforceVessels[0] != null)
			{
				return _taskforceVessels[0].Nation.Value;
			}
			if (_taskforceSubmarines.Count > 0 && _taskforceSubmarines[0] != null)
			{
				return _taskforceSubmarines[0].Nation.Value;
			}
			if (_taskforceAircraft.Count > 0 && _taskforceAircraft[0] != null)
			{
				return _taskforceAircraft[0].Nation.Value;
			}
			if (_taskforceHelicopters.Count > 0 && _taskforceHelicopters[0] != null)
			{
				return _taskforceHelicopters[0].Nation.Value;
			}
			if (TaskforceObjects.Count > 0 && TaskforceObjects[0] != null)
			{
				return TaskforceObjects[0].Nation.Value;
			}
			if (Nation == "WP")
			{
				return "Soviet";
			}
			if (WPactAffinity)
			{
				return "Soviet";
			}
			return "US";
		}
	}

	public bool IsAIControlled { get; private set; }

	public ObservableCollection<ObjectBase> TaskforceObjects { get; } = new ObservableCollection<ObjectBase>();

	public Entity Entity { get; internal set; }

	public List<Trigger> Triggers { get; private set; } = new List<Trigger>();

	public Taskforce(TfType side)
	{
		Side = side;
		_sensorSystemsLink = new SensorSystemsLink(this);
		Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
		string text = side.ToString();
		Entity = defaultGameObjectInjectionWorld.EntityManager.CreateEntity(typeof(TaskforceReference), typeof(TaskforceData), typeof(ShouldSaveComponent));
		EntityManagerManagedComponentExtensions.AddComponentData(defaultGameObjectInjectionWorld.EntityManager, Entity, new TaskforceReference
		{
			Taskforce = this
		});
		PlottingTable = new PlottingTable(this);
		defaultGameObjectInjectionWorld.EntityManager.AddComponentData(Entity, new TaskforceData
		{
			Name = text,
			PlottingTable = PlottingTable.Entity
		});
		if (Side == TfType.Player)
		{
			AddVehcleListeners();
		}
	}

	private void AddVehcleListeners()
	{
		PlottingTable.NewVehicle += delegate(object table, Vehicle veh)
		{
			if (veh.IsVisible())
			{
				CompositeDisposable compositeDisposable = new CompositeDisposable();
				if (Side == TfType.Player && veh.BaseObject._taskforce != Globals._playerTaskforce && !veh.BaseObject.IsDestroyed)
				{
					if (!_newContactsToReport.Contains(veh.BaseObject))
					{
						_newContactsToReport.Add(veh.BaseObject);
						_newContactsToReportTime = GameTime.time + 1f;
					}
					compositeDisposable.Add(veh.IsDormant.Subscribe(delegate
					{
						if (veh.IsDormant.Value && !_fadedContactsToReport.ContainsKey(veh.BaseObject))
						{
							_fadedContactsToReport.Add(veh.BaseObject, (veh.Id, GameTime.time + 5f));
						}
					}));
					compositeDisposable.Add(veh.UnitTaskforce.Subscribe(delegate(Taskforce taskforce)
					{
						if (taskforce != null && !_classifiedContactsToReport.Contains(veh.BaseObject))
						{
							_classifiedContactsToReport.Add(veh.BaseObject);
							_classifiedContactsToReportTime = GameTime.time + 1.1f;
						}
					}));
				}
			}
		};
	}

	public void SetNation(string nation)
	{
		if (!string.IsNullOrEmpty(nation) && string.IsNullOrEmpty(Nation))
		{
			nation = nation.ToLower();
			switch (nation)
			{
			case "red":
				return;
			case "blue":
				return;
			case "wp":
			case "bulgaria":
			case "china":
			case "cuba":
			case "czechoslovakia":
			case "ddr":
			case "egypt":
			case "hungary":
			case "india":
			case "iraq":
			case "myanmar":
			case "poland":
			case "romania":
			case "syria":
			case "soviet":
			case "vietnam":
			case "pact":
				WPactAffinity = true;
				break;
			}
			Nation = nation;
			if (this == Globals._playerTaskforce)
			{
				Singleton<MusicManager>.Instance.MusicManagerMode = (WPactAffinity ? MusicManagerMode.WP : MusicManagerMode.NATO);
			}
		}
	}

	public string VoiceOverAffinity(string nation)
	{
		if (string.IsNullOrEmpty(nation))
		{
			return "US";
		}
		nation = nation.ToLower();
		switch (nation)
		{
		case "wp":
		case "bulgaria":
		case "china":
		case "cuba":
		case "czechoslovakia":
		case "ddr":
		case "egypt":
		case "hungary":
		case "india":
		case "iraq":
		case "myanmar":
		case "poland":
		case "romania":
		case "syria":
		case "soviet":
		case "vietnam":
		case "pact":
			return "Soviet";
		default:
			return "US";
		}
	}

	public void OnUpdate()
	{
		for (int num = _missileGroups.Count - 1; num >= 0; num--)
		{
			if (_missileGroups[num].Count < 1)
			{
				_missileGroups.RemoveAt(num);
			}
			else
			{
				_missileGroups[num].OnUpdate();
			}
		}
		for (int num2 = _airStrikes.Count - 1; num2 >= 0; num2--)
		{
			if (_airStrikes[num2]._isFinished)
			{
				_airStrikes.RemoveAt(num2);
			}
			else
			{
				_airStrikes[num2].OnUpdate();
			}
		}
		for (int num3 = Formations.Count - 1; num3 >= 0; num3--)
		{
			Formations[num3].OnUpdate();
		}
		if (GameTime.time > _reconDataCheckTime)
		{
			_reconDataCheckTime = GameTime.time + 2f;
			for (int num4 = _reconDataAnalyze.Count - 1; num4 >= 0; num4--)
			{
				if (!(_reconDataAnalyze[num4].completionTime > GameTime.time))
				{
					List<ObjectBase> list = new List<ObjectBase>();
					foreach (ObjectBase item2 in _reconDataAnalyze[num4].objectsToSpot)
					{
						if (item2 == null || item2.IsDestroyed || item2._taskforce == this || _spottedObjects.Contains(item2))
						{
							continue;
						}
						if (item2.GetMapVehicle(this) == null)
						{
							foreach (ObjectBase taskforceUnit in _taskforceUnits)
							{
								if (!(taskforceUnit == null) && !taskforceUnit.IsDestroyed)
								{
									Utils.RevealContactToObject(item2, taskforceUnit, "Comms", 3);
									break;
								}
							}
						}
						_spottedObjects.Add(item2);
						list.Add(item2);
					}
					_reconDataAnalyze.RemoveAt(num4);
					if (list.Count > 0)
					{
						string text = Singleton<LanguageResourceHandler>.Instance.getText("Intel", "ProcessedSpottedUnitsMessage");
						text = text.Replace("${Count}", list.Count.ToString());
						Globals._mainGameViewModel.RaiseAlert(text);
						text = Singleton<LanguageResourceHandler>.Instance.getText("Intel", "ProcessedSpottedUnitsIntel");
						text = text.Replace("${Count}", list.Count.ToString());
						text = text.Replace("${Contacts}", Utils.StringListToDisplayString(list, addSpace: true, localizedNames: true));
						Environment instance = Singleton<Environment>.Instance;
						Intelligence item = new Intelligence(new DateTime(instance.Year, instance.Month, instance.Day, instance.Hour, instance.Minutes, (int)instance.Seconds), text);
						Singleton<EventLog>.Instance.InsertIntelligence(item);
					}
					break;
				}
			}
		}
		UpdateAirSupport();
		if (GameTime.time > _aiCheckTime)
		{
			_aiCheckTime = GameTime.time + 10f;
			CheckAI();
		}
		if (GameTime.time > _maintenanceTime)
		{
			_maintenanceTime = GameTime.time + 60f;
			for (int num5 = _identifiedSpyUnits.Count - 1; num5 >= 0; num5--)
			{
				if (_identifiedSpyUnits[num5].GetMapVehicle(this) == null)
				{
					_identifiedSpyUnits.RemoveAt(num5);
				}
			}
			for (int num6 = _taskforceAirstrikeTargets.Count - 1; num6 >= 0; num6--)
			{
				if (_taskforceAirstrikeTargets[num6] == null)
				{
					_taskforceAirstrikeTargets.RemoveAt(num6);
				}
				else if (_taskforceAirstrikeTargets[num6].IsDestroyed)
				{
					_taskforceAirstrikeTargets.RemoveAt(num6);
				}
			}
		}
		if (Side == TfType.Player)
		{
			CheckNewContactMessage();
			CheckContactClassifiedMessage();
			CheckContactFadedMessage();
		}
		foreach (PatrolZone patrolZone in _patrolZones)
		{
			patrolZone.OnUpdate();
		}
	}

	public void OnFixedUpdate()
	{
		for (int num = _airStrikes.Count - 1; num >= 0; num--)
		{
			if (_airStrikes[num]._isFinished)
			{
				_airStrikes.RemoveAt(num);
			}
			else
			{
				_airStrikes[num].OnFixedUpdate();
			}
		}
		foreach (PatrolZone patrolZone in _patrolZones)
		{
			patrolZone.OnFixedUpdate();
		}
	}

	public void updateDataLinkStart()
	{
		foreach (ObjectBase taskforceObject in TaskforceObjects)
		{
			taskforceObject._rwrBearings.Clear();
			taskforceObject._jamBearings.Clear();
		}
		_sensorSystemsLink.SetupUpdate();
	}

	public void updateDataLink(float percentageAlong)
	{
		_sensorSystemsLink.OnUpdate(percentageAlong);
	}

	public void AddTaskforceObject(ObjectBase tfObject)
	{
		SetNation(tfObject.Nation.Value);
		if (TaskforceObjects.Contains(tfObject))
		{
			return;
		}
		TaskforceObjects.Add(tfObject);
		bool flag = false;
		bool flag2 = false;
		if (tfObject._obp != null)
		{
			if (tfObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Deco))
			{
				flag = true;
			}
			if (tfObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SeaMine))
			{
				flag2 = true;
			}
		}
		if (!flag && !flag2)
		{
			if (tfObject.isWeapon())
			{
				if (tfObject._type != ObjectBase.ObjectType.Chaff && tfObject._type != ObjectBase.ObjectType.Noisemaker)
				{
					_taskforceWeapons.Add(tfObject);
				}
			}
			else if (tfObject.isUnit())
			{
				_taskforceUnits.Add(tfObject);
				if (tfObject._type == ObjectBase.ObjectType.Vessel)
				{
					_taskforceVessels.Add(tfObject);
				}
				else if (tfObject._type == ObjectBase.ObjectType.Submarine)
				{
					_taskforceSubmarines.Add(tfObject);
				}
				else if (tfObject._type == ObjectBase.ObjectType.Biologic)
				{
					_taskforceBiologics.Add(tfObject);
				}
				else if (tfObject._type == ObjectBase.ObjectType.LandUnit)
				{
					_taskforceLandUnits.Add(tfObject);
				}
				else if (tfObject._type == ObjectBase.ObjectType.Aircraft)
				{
					_taskforceAircraft.Add(tfObject);
				}
				else if (tfObject._type == ObjectBase.ObjectType.Helicopter)
				{
					_taskforceHelicopters.Add(tfObject);
				}
			}
		}
		bool flag3 = true;
		if (tfObject is Noisemaker)
		{
			flag3 = false;
		}
		if (tfObject._isUndetectable && flag)
		{
			flag3 = false;
		}
		if (tfObject.PlottingTable != null)
		{
			if (flag3)
			{
				tfObject.PlottingTable.OnNext(tfObject.SelfSensor.MakeTruth());
				PlottingTable.Ingest(tfObject.PlottingTable.VehicleForObject(tfObject));
			}
			tfObject.PlottingTable.Parent = PlottingTable;
		}
		if (tfObject._obp == null)
		{
			return;
		}
		foreach (BaseSystem system in tfObject._obp._systems)
		{
			_sensorSystemsLink.AddSensor(system);
		}
	}

	public void RemoveTaskForceObject(ObjectBase tfObject)
	{
		if (!TaskforceObjects.Contains(tfObject))
		{
			return;
		}
		if (tfObject._obp != null)
		{
			foreach (BaseSystem system in tfObject._obp._systems)
			{
				_sensorSystemsLink.RemoveSensor(system);
			}
			foreach (TowedSystem towedSystem in tfObject._obp._towedSystems)
			{
				if (towedSystem?._towedDecoy?._decoyObject != null)
				{
					Singleton<ObjectsManager>.Instance.removeObject(towedSystem._towedDecoy._decoyObject);
					RemoveTaskForceObject(towedSystem._towedDecoy._decoyObject);
				}
			}
		}
		TaskforceObjects.Remove(tfObject);
		if (tfObject is Aircraft item && _activeAirSupport.Contains(item))
		{
			_activeAirSupport.Remove(item);
			if (_activeAirSupport.Count == 0)
			{
				_airSupportReloadTimeStart = GameTime.time;
			}
		}
		if (tfObject.isWeapon())
		{
			_taskforceWeapons.Remove(tfObject);
		}
		else if (tfObject.isUnit())
		{
			_taskforceUnits.Remove(tfObject);
			if (tfObject._type == ObjectBase.ObjectType.Vessel)
			{
				_taskforceVessels.Remove(tfObject);
			}
			else if (tfObject._type == ObjectBase.ObjectType.Submarine)
			{
				_taskforceSubmarines.Remove(tfObject);
			}
			else if (tfObject._type == ObjectBase.ObjectType.LandUnit)
			{
				_taskforceLandUnits.Remove(tfObject);
			}
			else if (tfObject._type == ObjectBase.ObjectType.Aircraft)
			{
				_taskforceAircraft.Remove(tfObject);
			}
			else if (tfObject._type == ObjectBase.ObjectType.Helicopter)
			{
				_taskforceHelicopters.Remove(tfObject);
			}
		}
	}

	public void updateTaskforceContacts()
	{
		foreach (ObjectBase taskforceObject in TaskforceObjects)
		{
			if (taskforceObject._type != ObjectBase.ObjectType.Wakebubble && taskforceObject._type != ObjectBase.ObjectType.Chaff)
			{
				taskforceObject.UpdateOwnPlottingState();
			}
		}
		PlottingTable.Update(GameTime.time);
	}

	public void updateIncremental(float along)
	{
		PlottingTable.UpdateIncremental(along);
	}

	public RelationsState RelationshipTo(Taskforce other)
	{
		if (other == null)
		{
			return RelationsState.Neutral;
		}
		if (other == this)
		{
			return RelationsState.Friendly;
		}
		if (Side == TfType.Player || Side == TfType.Ally)
		{
			if (other.Side == TfType.Enemy)
			{
				return RelationsState.Hostile;
			}
			if (other.Side == TfType.Player || other.Side == TfType.Ally)
			{
				return RelationsState.Friendly;
			}
		}
		else if (Side == TfType.Enemy)
		{
			if (other.Side == TfType.Enemy)
			{
				return RelationsState.Friendly;
			}
			if (other.Side == TfType.Player || other.Side == TfType.Ally)
			{
				return RelationsState.Hostile;
			}
		}
		return RelationsState.Neutral;
	}

	private void CheckAlertState()
	{
		if (!_isOnAlert)
		{
			foreach (KeyValuePair<ObjectBase, Vehicle> localVehicle in PlottingTable.LocalVehicles)
			{
				if ((localVehicle.Key.isUnit() || localVehicle.Key.isWeapon()) && localVehicle.Value.CurrentRelationship() == RelationsState.Hostile)
				{
					DM.I("Taskforce: raise alert");
					RaiseAlert();
					break;
				}
			}
			return;
		}
		foreach (KeyValuePair<ObjectBase, Vehicle> localVehicle2 in PlottingTable.LocalVehicles)
		{
			if ((localVehicle2.Key.isUnit() || localVehicle2.Key.isWeapon()) && localVehicle2.Value.CurrentRelationship() == RelationsState.Hostile)
			{
				return;
			}
		}
		if (_noHostileContactsTime < 0f)
		{
			_noHostileContactsTime = GameTime.time;
		}
		else if (GameTime.time - _noHostileContactsTime > 10f)
		{
			CancelAlert();
			_noHostileContactsTime = -1f;
		}
	}

	public void RaiseAlert(bool panic = false)
	{
		if (_isOnAlert)
		{
			return;
		}
		_isOnAlert = true;
		if (AIPlayer.IsForcedToDisable)
		{
			panic = true;
		}
		foreach (ObjectBase taskforceObject in TaskforceObjects)
		{
			if (taskforceObject.isUnit())
			{
				if (taskforceObject._type != ObjectBase.ObjectType.Submarine)
				{
					taskforceObject.EnableAllActiveSensors();
				}
				if (panic)
				{
					taskforceObject._weaponStatus = ObjectBase.WeaponStatus.Free;
				}
			}
		}
		DM.I($"RaiseAlert!  panic = {panic}");
	}

	public void CancelAlert(bool panic = false, bool keepAirRadarsOn = true)
	{
		if (!_isOnAlert)
		{
			return;
		}
		_isOnAlert = false;
		foreach (ObjectBase taskforceObject in TaskforceObjects)
		{
			if (taskforceObject.isUnit() && (!keepAirRadarsOn || (!(taskforceObject is Helicopter) && !(taskforceObject is Aircraft))))
			{
				taskforceObject.DisableAllActiveSensors();
				if (!panic)
				{
					taskforceObject._weaponStatus = ObjectBase.WeaponStatus.Tight;
				}
			}
		}
		DM.I($"CancelAlert!  panic = {panic}");
	}

	public void SetWeaponStatus(ObjectBase.WeaponStatus status)
	{
		foreach (ObjectBase taskforceObject in TaskforceObjects)
		{
			if (taskforceObject.isUnit())
			{
				taskforceObject._weaponStatus = status;
			}
		}
	}

	public void SpawnAirSupport(bool selectFirst = false)
	{
		GeoPosition spawnPosition = _airSupportSpawnPoint.position;
		for (int i = 0; i < _airSupportList.Count; i++)
		{
			if (i > 0)
			{
				spawnPosition = spawnPosition.GeoOffsetByDistanceOnHeading(1f, _airSupportSpawnPoint.heading + 90f);
				SpawnAirSupportAircraft(spawnPosition, _airSupportList[i].iniName, _airSupportList[i].squadron, _airSupportList[i].loadout);
			}
			else
			{
				SpawnAirSupportAircraft(spawnPosition, _airSupportList[i].iniName, _airSupportList[i].squadron, _airSupportList[i].loadout, selectFirst);
			}
		}
		_airSupportList.Clear();
		_airSupportTimeStart = GameTime.time;
	}

	public void SpawnAirSupportAircraft(GeoPosition spawnPosition, string iniName, string squadron, string selectedLoadout, bool selectSpawned = false)
	{
		Aircraft aircraft = Singleton<ObjectsManager>.Instance.createAircraft(squadron, selectedLoadout, null, null, iniName, Singleton<ObjectsManager>.Instance._listOfAllObjects.Count + 1, Singleton<SceneCreator>.Instance.GeoCenterPosition, spawnPosition, new Vector3(0f, _airSupportSpawnPoint.heading, 0f), this, createMinimapSymbol: true, loadWeaponSystems: true);
		if (aircraft == null)
		{
			return;
		}
		_activeAirSupport.Add(aircraft);
		ObjectCodeAnimation value = null;
		string text = "Gear_Extend";
		if (!aircraft._obp._codeAnimations.TryGetValue(text, out value))
		{
			Log.Error("Could not find animation " + text + " for " + aircraft._obp._objectIniName, Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Taskforce\\TaskForce.cs", 834, "SpawnAirSupportAircraft");
		}
		else
		{
			value.setClosedState();
		}
		aircraft.ImmediateStartEngines();
		aircraft.setTelegraph(4);
		aircraft.SetFlightPhysicsModel(isStartingAirborne: true);
		aircraft._velocityInKnots = aircraft.SpeedCommand.Value.GetDemandedSpeedInKnots(aircraft);
		aircraft.transform.position = Utils.longLatToLocalV3(spawnPosition, Globals._currentCenterTile);
		Physics.SyncTransforms();
		aircraft._geoPosition = spawnPosition;
		aircraft.DesiredAltitude.Value = aircraft.transform.position.y;
		foreach (SensorSystemRadar searchRadarSystem in aircraft._obp._searchRadarSystems)
		{
			searchRadarSystem.Enable();
		}
		aircraft.IsAirSearchRadarsOn.Value = true;
		aircraft.IsSurfaceSearchRadarsOn.Value = true;
		foreach (SensorSystem sensorSystem in aircraft._obp._sensorSystems)
		{
			if (sensorSystem.GetType() == typeof(SensorSystemMAD))
			{
				sensorSystem.IsOn.Value = false;
			}
		}
		if (selectSpawned)
		{
			Singleton<ObjectsManager>.Instance.setActiveObject(aircraft);
			Singleton<RenderPosition>.Instance.switchToObject(aircraft, performSmoothMove: false);
		}
		GeoPosition geoCenterPosition = Singleton<SceneCreator>.Instance.GeoCenterPosition;
		geoCenterPosition._height = spawnPosition._height;
		aircraft.setWaypointTask(geoCenterPosition);
		aircraft.setTelegraph(4);
	}

	private void UpdateAirSupport()
	{
		for (int num = _activeAirSupport.Count - 1; num >= 0; num--)
		{
			Aircraft aircraft = _activeAirSupport[num];
			if (aircraft == null)
			{
				_activeAirSupport.RemoveAt(num);
			}
			else
			{
				float num2 = GameTime.time - _airSupportTimeStart;
				if (num2 > _airSupportInterval || (num2 > 10f && aircraft.Ap._currentFuelMass < 10f))
				{
					if (aircraft.IsControllable)
					{
						aircraft.CeaseFire(report: false);
						aircraft.RemoveWaypoints();
						aircraft.setWaypointTask(_airSupportSpawnPoint.position);
						aircraft.IsControllable = false;
						aircraft.setTelegraph(5);
					}
					else
					{
						float distToCurrentActiveWaypointInMiles = aircraft.GetDistToCurrentActiveWaypointInMiles();
						if (distToCurrentActiveWaypointInMiles < 5f && distToCurrentActiveWaypointInMiles > 0.001f)
						{
							_airSupportList.Add((aircraft._obp._objectIniName, $"Squadron{aircraft._obp._currentVariantReferenceIndex}", aircraft._obp._loadoutVariant));
							_activeAirSupport[num].destroyObject(keepInFlightEffectAlive: false, score: false);
						}
					}
				}
			}
		}
	}

	public void CheckAI()
	{
		if (Globals._allowEnemyUnitsAttackPlayer && this != Globals._playerTaskforce && this != Globals._neutralTaskforce)
		{
			CheckASWAssignment();
			CheckVIDAssignment();
		}
	}

	private void CheckForHelicopterLaunch(ObjectBaseParameters.UnitRoles role = ObjectBaseParameters.UnitRoles.None)
	{
		ObjectBase.ObjectType type = ObjectBase.ObjectType.Helicopter;
		int num = 0;
		foreach (ObjectBase taskforceHelicopter in _taskforceHelicopters)
		{
			if (((Helicopter)taskforceHelicopter)._isInFlight && (role == ObjectBaseParameters.UnitRoles.None || taskforceHelicopter._obp._unitRoles.Contains(role)))
			{
				num++;
			}
		}
		foreach (ObjectBase taskforceVessel in _taskforceVessels)
		{
			if (taskforceVessel._obp._flightDeck == null)
			{
				continue;
			}
			foreach (FlightDeckTask flightDeckTask in taskforceVessel._obp._flightDeck.FlightDeckTasks)
			{
				if (flightDeckTask.VehicleType != null && flightDeckTask.VehicleType._type == VehicleType.Helicopter && flightDeckTask.VehicleType._unitRoles.Contains(role))
				{
					num++;
				}
			}
		}
		foreach (ObjectBase taskforceLandUnit in _taskforceLandUnits)
		{
			if (taskforceLandUnit._obp._flightDeck == null)
			{
				continue;
			}
			foreach (FlightDeckTask flightDeckTask2 in taskforceLandUnit._obp._flightDeck.FlightDeckTasks)
			{
				if (flightDeckTask2.VehicleType != null && flightDeckTask2.VehicleType._type == VehicleType.Helicopter && flightDeckTask2.VehicleType._unitRoles.Contains(role))
				{
					num++;
				}
			}
		}
		if (num > 1)
		{
			return;
		}
		foreach (ObjectBase taskforceVessel2 in _taskforceVessels)
		{
			if (taskforceVessel2._hasControl && taskforceVessel2.FlightDeckHasUnit(type, role))
			{
				taskforceVessel2.FlightDeckLaunchUnit(type, "Default", role);
				return;
			}
		}
		foreach (ObjectBase taskforceLandUnit2 in _taskforceLandUnits)
		{
			if (taskforceLandUnit2._hasControl && taskforceLandUnit2.FlightDeckHasUnit(type, role))
			{
				taskforceLandUnit2.FlightDeckLaunchUnit(type, "Default", role);
				break;
			}
		}
	}

	public void CheckVIDAssignment()
	{
		if (!Globals._allowEnemyUnitsAttackPlayer || this == Globals._playerTaskforce || this == Globals._neutralTaskforce)
		{
			return;
		}
		foreach (KeyValuePair<ObjectBase, Vehicle> localVehicle in PlottingTable.LocalVehicles)
		{
			ObjectBase key = localVehicle.Key;
			Vehicle value = localVehicle.Value;
			if (key == null || !key.isUnit() || key.isSubmerged() || RelationshipTo(key._taskforce) != RelationsState.Hostile || value.Identified.Value || key._ai._enemiesInvestigatingThisUnit.Count > 0)
			{
				continue;
			}
			ObjectBase objectBase = null;
			float num = float.MaxValue;
			foreach (ObjectBase item in _taskforceAircraft)
			{
				if (item.Formation == null)
				{
					float sqrMagnitude = (item.transform.position - key.transform.position).sqrMagnitude;
					if (num > sqrMagnitude)
					{
						num = sqrMagnitude;
						objectBase = item;
					}
				}
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
				DM.I("Sending " + objectBase.getUIDAndName() + " toVID " + key.getUIDAndName());
			}
			else
			{
				if (key is Aircraft)
				{
					continue;
				}
				num = float.MaxValue;
				foreach (ObjectBase taskforceHelicopter in _taskforceHelicopters)
				{
					if (taskforceHelicopter.IsControllable && taskforceHelicopter._obp._visualSensors.Count != 0)
					{
						float sqrMagnitude = (taskforceHelicopter.transform.position - key.transform.position).sqrMagnitude;
						if (num > sqrMagnitude)
						{
							num = sqrMagnitude;
							objectBase = taskforceHelicopter;
						}
					}
				}
				if (objectBase != null && !(Mathf.Sqrt(num) > ((Helicopter)objectBase).RangeOnMap.Value * 0.8f))
				{
					objectBase.setOrder(Order.Type.Identify, key, displayOrderText: false);
					DM.I("Sending " + objectBase.getUIDAndName() + " toVID " + key.getUIDAndName());
				}
			}
		}
	}

	public void CheckASuWAssignment()
	{
		if (!Globals._allowEnemyUnitsAttackPlayer || this == Globals._playerTaskforce || this == Globals._neutralTaskforce)
		{
			return;
		}
		foreach (KeyValuePair<ObjectBase, Vehicle> localVehicle in PlottingTable.LocalVehicles)
		{
			ObjectBase key = localVehicle.Key;
			_ = localVehicle.Value;
			if (!(key is Vessel) || key._ai._enemiesInvestigatingThisUnit.Count > 0)
			{
				continue;
			}
			ObjectBase objectBase = null;
			float num = float.MaxValue;
			foreach (ObjectBase item in _taskforceAircraft)
			{
				if (item.Formation == null)
				{
					float sqrMagnitude = (item.transform.position - key.transform.position).sqrMagnitude;
					if (num > sqrMagnitude)
					{
						num = sqrMagnitude;
						objectBase = item;
					}
				}
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
				continue;
			}
			num = float.MaxValue;
			foreach (ObjectBase taskforceHelicopter in _taskforceHelicopters)
			{
				if (taskforceHelicopter.IsControllable)
				{
					float sqrMagnitude = (taskforceHelicopter.transform.position - key.transform.position).sqrMagnitude;
					if (num > sqrMagnitude)
					{
						num = sqrMagnitude;
						objectBase = taskforceHelicopter;
					}
				}
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
				continue;
			}
			num = float.MaxValue;
			foreach (ObjectBase taskforceVessel in _taskforceVessels)
			{
				if (taskforceVessel._obp._sonarSystems.Count != 0 && taskforceVessel._obp._unitRoles.ContainsAny(ObjectBaseParameters.UnitRoles.FAC, ObjectBaseParameters.UnitRoles.ASuW, ObjectBaseParameters.UnitRoles.ASuW_VLR, ObjectBaseParameters.UnitRoles.ASuW_Gun) && taskforceVessel.Formation == null)
				{
					float sqrMagnitude = (taskforceVessel.transform.position - key.transform.position).sqrMagnitude;
					if (num > sqrMagnitude)
					{
						num = sqrMagnitude;
						objectBase = taskforceVessel;
					}
				}
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
				continue;
			}
			foreach (ObjectBase taskforceSubmarine in _taskforceSubmarines)
			{
				if (taskforceSubmarine.Formation == null)
				{
					float sqrMagnitude = (taskforceSubmarine.transform.position - key.transform.position).sqrMagnitude;
					if (num > sqrMagnitude)
					{
						num = sqrMagnitude;
						objectBase = taskforceSubmarine;
					}
				}
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
			}
		}
	}

	public void CheckASWAssignment()
	{
		if (!Globals._allowEnemyUnitsAttackPlayer || this == Globals._playerTaskforce || this == Globals._neutralTaskforce)
		{
			return;
		}
		foreach (KeyValuePair<ObjectBase, Vehicle> localVehicle in PlottingTable.LocalVehicles)
		{
			ObjectBase key = localVehicle.Key;
			_ = localVehicle.Value;
			if (!(key is Submarine) || key._ai._enemiesInvestigatingThisUnit.Count > 0)
			{
				continue;
			}
			ObjectBase objectBase = null;
			float num = float.MaxValue;
			foreach (ObjectBase item in _taskforceAircraft)
			{
				_ = item.Formation;
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
				continue;
			}
			num = float.MaxValue;
			foreach (ObjectBase taskforceHelicopter in _taskforceHelicopters)
			{
				_ = taskforceHelicopter.Formation;
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
				continue;
			}
			num = float.MaxValue;
			foreach (ObjectBase taskforceVessel in _taskforceVessels)
			{
				if (taskforceVessel._obp._sonarSystems.Count != 0 && !taskforceVessel._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Carrier) && !taskforceVessel._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.CVS) && !taskforceVessel._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.CVL) && !taskforceVessel._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW_VLR) && taskforceVessel.Formation == null && !(key == null))
				{
					float sqrMagnitude = (taskforceVessel.transform.position - key.transform.position).sqrMagnitude;
					if (num > sqrMagnitude)
					{
						num = sqrMagnitude;
						objectBase = taskforceVessel;
					}
				}
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
				continue;
			}
			foreach (ObjectBase taskforceSubmarine in _taskforceSubmarines)
			{
				_ = taskforceSubmarine.Formation;
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
			}
		}
	}

	public int GetCallsignNumber(ObjectBase vehicleToLaunch, int squadronIndex, int callsignIndex)
	{
		string key = vehicleToLaunch._obp._objectIniName + squadronIndex + callsignIndex;
		if (!_usedCallsigns.ContainsKey(key))
		{
			_usedCallsigns.Add(key, new List<ObjectBase>());
		}
		for (int i = 0; i < _usedCallsigns[key].Count; i++)
		{
			if (_usedCallsigns[key][i] == null)
			{
				_usedCallsigns[key][i] = vehicleToLaunch;
				return i + 1;
			}
		}
		_usedCallsigns[key].Add(vehicleToLaunch);
		return _usedCallsigns[key].Count;
	}

	public bool DoesStackInRegister(ObjectBase tfObject)
	{
		if (tfObject.IsAirUnit)
		{
			return true;
		}
		if (tfObject is LandUnit landUnit)
		{
			if (landUnit.LandUnitParams._landUnitSubType == LandUnitParameters.LandUnitSubType.AirBase)
			{
				return false;
			}
			if (landUnit.LandUnitParams._landUnitSubType == LandUnitParameters.LandUnitSubType.Port)
			{
				return false;
			}
			return true;
		}
		return false;
	}

	public void RegisterUnitStatus(bool useTmpList, ObjectBase tfObject, bool destroyed = false, bool damaged = false)
	{
		if (tfObject._obp == null || tfObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Deco) || tfObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SeaMine))
		{
			return;
		}
		List<UnitRegister> list = (useTmpList ? _tmpUnitStatusRegister : _unitStatusRegister);
		if (DoesStackInRegister(tfObject))
		{
			foreach (UnitRegister item in list)
			{
				if (item.IniRef == tfObject._obp._objectIniName && item.IsDestroyed == destroyed && item.IsDamaged == damaged)
				{
					item.Count++;
					return;
				}
			}
		}
		list.Add(new UnitRegister(tfObject, destroyed, damaged));
	}

	public void RegisterNotLaunchedVehicle(bool useTmpList, VehicleTypeOnBoard vehicleType, int numbers, string nation, bool destroyed = false)
	{
		if (numbers < 1)
		{
			return;
		}
		List<UnitRegister> list = (useTmpList ? _tmpUnitStatusRegister : _unitStatusRegister);
		foreach (UnitRegister item in list)
		{
			if (item.IniRef == vehicleType._fileName && item.IsDestroyed == destroyed)
			{
				item.Count += numbers;
				return;
			}
		}
		list.Add(new UnitRegister(vehicleType, numbers, nation, destroyed));
	}

	public void AddSortedRegister(List<UnitRegister> targetRegister)
	{
		foreach (UnitRegister item in _unitStatusRegister)
		{
			bool flag = true;
			if (item.IsStackable)
			{
				foreach (UnitRegister item2 in _tmpUnitStatusRegister)
				{
					if (item2.IniRef == item.IniRef && item2.IsDestroyed == item.IsDestroyed && item2.IsDamaged == item.IsDamaged)
					{
						item2.Count++;
						flag = false;
						break;
					}
				}
			}
			if (flag)
			{
				_tmpUnitStatusRegister.Add(item);
			}
		}
		List<UnitRegister> sortList = new List<UnitRegister>();
		if (this == Globals._playerTaskforce)
		{
			SortRegister(targetRegister, sortList, destroyed: false, damaged: false);
		}
		SortRegister(targetRegister, sortList, destroyed: false, damaged: true);
		SortRegister(targetRegister, sortList, destroyed: true, damaged: false);
		_tmpUnitStatusRegister.Clear();
	}

	private void SortRegister(List<UnitRegister> targetRegister, List<UnitRegister> sortList, bool destroyed, bool damaged)
	{
		sortList.Clear();
		for (int i = 0; i < _tmpUnitStatusRegister.Count; i++)
		{
			if (_tmpUnitStatusRegister[i].IsDestroyed == destroyed && _tmpUnitStatusRegister[i].IsDamaged == damaged && _tmpUnitStatusRegister[i].ObjectType == ObjectBase.ObjectType.Vessel && _tmpUnitStatusRegister[i].Roles.Contains(ObjectBaseParameters.UnitRoles.Carrier))
			{
				sortList.Add(_tmpUnitStatusRegister[i]);
				_tmpUnitStatusRegister.RemoveAt(i);
				i--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num = 0; num < _tmpUnitStatusRegister.Count; num++)
		{
			if (_tmpUnitStatusRegister[num].IsDestroyed == destroyed && _tmpUnitStatusRegister[num].IsDamaged == damaged && _tmpUnitStatusRegister[num].ObjectType == ObjectBase.ObjectType.Vessel && _tmpUnitStatusRegister[num].Roles.Contains(ObjectBaseParameters.UnitRoles.CVL))
			{
				sortList.Add(_tmpUnitStatusRegister[num]);
				_tmpUnitStatusRegister.RemoveAt(num);
				num--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num2 = 0; num2 < _tmpUnitStatusRegister.Count; num2++)
		{
			if (_tmpUnitStatusRegister[num2].IsDestroyed == destroyed && _tmpUnitStatusRegister[num2].IsDamaged == damaged && _tmpUnitStatusRegister[num2].ObjectType == ObjectBase.ObjectType.Vessel && _tmpUnitStatusRegister[num2].Roles.Contains(ObjectBaseParameters.UnitRoles.CVS))
			{
				sortList.Add(_tmpUnitStatusRegister[num2]);
				_tmpUnitStatusRegister.RemoveAt(num2);
				num2--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num3 = 0; num3 < _tmpUnitStatusRegister.Count; num3++)
		{
			if (_tmpUnitStatusRegister[num3].IsDestroyed == destroyed && _tmpUnitStatusRegister[num3].IsDamaged == damaged && _tmpUnitStatusRegister[num3].ObjectType == ObjectBase.ObjectType.Vessel && _tmpUnitStatusRegister[num3].Roles.Contains(ObjectBaseParameters.UnitRoles.ASuW_VLR))
			{
				sortList.Add(_tmpUnitStatusRegister[num3]);
				_tmpUnitStatusRegister.RemoveAt(num3);
				num3--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num4 = 0; num4 < _tmpUnitStatusRegister.Count; num4++)
		{
			if (_tmpUnitStatusRegister[num4].IsDestroyed == destroyed && _tmpUnitStatusRegister[num4].IsDamaged == damaged && _tmpUnitStatusRegister[num4].ObjectType == ObjectBase.ObjectType.Vessel && _tmpUnitStatusRegister[num4].Roles.Contains(ObjectBaseParameters.UnitRoles.NGFS))
			{
				sortList.Add(_tmpUnitStatusRegister[num4]);
				_tmpUnitStatusRegister.RemoveAt(num4);
				num4--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num5 = 0; num5 < _tmpUnitStatusRegister.Count; num5++)
		{
			if (_tmpUnitStatusRegister[num5].IsDestroyed == destroyed && _tmpUnitStatusRegister[num5].IsDamaged == damaged && _tmpUnitStatusRegister[num5].ObjectType == ObjectBase.ObjectType.Vessel && _tmpUnitStatusRegister[num5].Roles[0] == ObjectBaseParameters.UnitRoles.ASuW)
			{
				sortList.Add(_tmpUnitStatusRegister[num5]);
				_tmpUnitStatusRegister.RemoveAt(num5);
				num5--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num6 = 0; num6 < _tmpUnitStatusRegister.Count; num6++)
		{
			if (_tmpUnitStatusRegister[num6].IsDestroyed == destroyed && _tmpUnitStatusRegister[num6].IsDamaged == damaged && _tmpUnitStatusRegister[num6].ObjectType == ObjectBase.ObjectType.Vessel && _tmpUnitStatusRegister[num6].Roles[0] == ObjectBaseParameters.UnitRoles.AAW)
			{
				sortList.Add(_tmpUnitStatusRegister[num6]);
				_tmpUnitStatusRegister.RemoveAt(num6);
				num6--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num7 = 0; num7 < _tmpUnitStatusRegister.Count; num7++)
		{
			if (_tmpUnitStatusRegister[num7].IsDestroyed == destroyed && _tmpUnitStatusRegister[num7].IsDamaged == damaged && _tmpUnitStatusRegister[num7].ObjectType == ObjectBase.ObjectType.Vessel && _tmpUnitStatusRegister[num7].Roles[0] == ObjectBaseParameters.UnitRoles.ASW)
			{
				sortList.Add(_tmpUnitStatusRegister[num7]);
				_tmpUnitStatusRegister.RemoveAt(num7);
				num7--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num8 = 0; num8 < _tmpUnitStatusRegister.Count; num8++)
		{
			if (_tmpUnitStatusRegister[num8].IsDestroyed == destroyed && _tmpUnitStatusRegister[num8].IsDamaged == damaged && _tmpUnitStatusRegister[num8].ObjectType == ObjectBase.ObjectType.Vessel && _tmpUnitStatusRegister[num8].Roles.Contains(ObjectBaseParameters.UnitRoles.ASuW))
			{
				sortList.Add(_tmpUnitStatusRegister[num8]);
				_tmpUnitStatusRegister.RemoveAt(num8);
				num8--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num9 = 0; num9 < _tmpUnitStatusRegister.Count; num9++)
		{
			if (_tmpUnitStatusRegister[num9].IsDestroyed == destroyed && _tmpUnitStatusRegister[num9].IsDamaged == damaged && _tmpUnitStatusRegister[num9].ObjectType == ObjectBase.ObjectType.Vessel && _tmpUnitStatusRegister[num9].Roles.Contains(ObjectBaseParameters.UnitRoles.AAW))
			{
				sortList.Add(_tmpUnitStatusRegister[num9]);
				_tmpUnitStatusRegister.RemoveAt(num9);
				num9--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num10 = 0; num10 < _tmpUnitStatusRegister.Count; num10++)
		{
			if (_tmpUnitStatusRegister[num10].IsDestroyed == destroyed && _tmpUnitStatusRegister[num10].IsDamaged == damaged && _tmpUnitStatusRegister[num10].ObjectType == ObjectBase.ObjectType.Vessel && _tmpUnitStatusRegister[num10].Roles.Contains(ObjectBaseParameters.UnitRoles.ASW))
			{
				sortList.Add(_tmpUnitStatusRegister[num10]);
				_tmpUnitStatusRegister.RemoveAt(num10);
				num10--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num11 = 0; num11 < _tmpUnitStatusRegister.Count; num11++)
		{
			if (_tmpUnitStatusRegister[num11].IsDestroyed == destroyed && _tmpUnitStatusRegister[num11].IsDamaged == damaged && _tmpUnitStatusRegister[num11].ObjectType == ObjectBase.ObjectType.Vessel && (_tmpUnitStatusRegister[num11].Roles.Contains(ObjectBaseParameters.UnitRoles.FAC) || _tmpUnitStatusRegister[num11].Roles.Contains(ObjectBaseParameters.UnitRoles.ASuW_Gun)))
			{
				sortList.Add(_tmpUnitStatusRegister[num11]);
				_tmpUnitStatusRegister.RemoveAt(num11);
				num11--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num12 = 0; num12 < _tmpUnitStatusRegister.Count; num12++)
		{
			if (_tmpUnitStatusRegister[num12].IsDestroyed == destroyed && _tmpUnitStatusRegister[num12].IsDamaged == damaged && _tmpUnitStatusRegister[num12].ObjectType == ObjectBase.ObjectType.Vessel && (_tmpUnitStatusRegister[num12].Roles.Contains(ObjectBaseParameters.UnitRoles.RAS) || _tmpUnitStatusRegister[num12].Roles.Contains(ObjectBaseParameters.UnitRoles.Landing) || _tmpUnitStatusRegister[num12].Roles.Contains(ObjectBaseParameters.UnitRoles.Transport) || _tmpUnitStatusRegister[num12].Roles.Contains(ObjectBaseParameters.UnitRoles.Minesweep) || _tmpUnitStatusRegister[num12].Roles.Contains(ObjectBaseParameters.UnitRoles.Merchant) || _tmpUnitStatusRegister[num12].Roles.Contains(ObjectBaseParameters.UnitRoles.Passenger) || _tmpUnitStatusRegister[num12].Roles.Contains(ObjectBaseParameters.UnitRoles.SmallCivilian) || _tmpUnitStatusRegister[num12].Roles.Contains(ObjectBaseParameters.UnitRoles.Spy)))
			{
				sortList.Add(_tmpUnitStatusRegister[num12]);
				_tmpUnitStatusRegister.RemoveAt(num12);
				num12--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num13 = 0; num13 < _tmpUnitStatusRegister.Count; num13++)
		{
			if (_tmpUnitStatusRegister[num13].IsDestroyed == destroyed && _tmpUnitStatusRegister[num13].IsDamaged == damaged && _tmpUnitStatusRegister[num13].ObjectType == ObjectBase.ObjectType.Vessel)
			{
				sortList.Add(_tmpUnitStatusRegister[num13]);
				_tmpUnitStatusRegister.RemoveAt(num13);
				num13--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num14 = 0; num14 < _tmpUnitStatusRegister.Count; num14++)
		{
			if (_tmpUnitStatusRegister[num14].IsDestroyed == destroyed && _tmpUnitStatusRegister[num14].IsDamaged == damaged && _tmpUnitStatusRegister[num14].ObjectType == ObjectBase.ObjectType.Submarine && _tmpUnitStatusRegister[num14].Roles.Contains(ObjectBaseParameters.UnitRoles.SSBN))
			{
				sortList.Add(_tmpUnitStatusRegister[num14]);
				_tmpUnitStatusRegister.RemoveAt(num14);
				num14--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num15 = 0; num15 < _tmpUnitStatusRegister.Count; num15++)
		{
			if (_tmpUnitStatusRegister[num15].IsDestroyed == destroyed && _tmpUnitStatusRegister[num15].IsDamaged == damaged && _tmpUnitStatusRegister[num15].ObjectType == ObjectBase.ObjectType.Submarine && _tmpUnitStatusRegister[num15].Roles.Contains(ObjectBaseParameters.UnitRoles.SSGN))
			{
				sortList.Add(_tmpUnitStatusRegister[num15]);
				_tmpUnitStatusRegister.RemoveAt(num15);
				num15--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num16 = 0; num16 < _tmpUnitStatusRegister.Count; num16++)
		{
			if (_tmpUnitStatusRegister[num16].IsDestroyed == destroyed && _tmpUnitStatusRegister[num16].IsDamaged == damaged && _tmpUnitStatusRegister[num16].ObjectType == ObjectBase.ObjectType.Submarine && _tmpUnitStatusRegister[num16].Roles.Contains(ObjectBaseParameters.UnitRoles.SSN))
			{
				sortList.Add(_tmpUnitStatusRegister[num16]);
				_tmpUnitStatusRegister.RemoveAt(num16);
				num16--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num17 = 0; num17 < _tmpUnitStatusRegister.Count; num17++)
		{
			if (_tmpUnitStatusRegister[num17].IsDestroyed == destroyed && _tmpUnitStatusRegister[num17].IsDamaged == damaged && _tmpUnitStatusRegister[num17].ObjectType == ObjectBase.ObjectType.Submarine && _tmpUnitStatusRegister[num17].Roles.Contains(ObjectBaseParameters.UnitRoles.SSB))
			{
				sortList.Add(_tmpUnitStatusRegister[num17]);
				_tmpUnitStatusRegister.RemoveAt(num17);
				num17--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num18 = 0; num18 < _tmpUnitStatusRegister.Count; num18++)
		{
			if (_tmpUnitStatusRegister[num18].IsDestroyed == destroyed && _tmpUnitStatusRegister[num18].IsDamaged == damaged && _tmpUnitStatusRegister[num18].ObjectType == ObjectBase.ObjectType.Submarine && _tmpUnitStatusRegister[num18].Roles.Contains(ObjectBaseParameters.UnitRoles.SSG))
			{
				sortList.Add(_tmpUnitStatusRegister[num18]);
				_tmpUnitStatusRegister.RemoveAt(num18);
				num18--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num19 = 0; num19 < _tmpUnitStatusRegister.Count; num19++)
		{
			if (_tmpUnitStatusRegister[num19].IsDestroyed == destroyed && _tmpUnitStatusRegister[num19].IsDamaged == damaged && _tmpUnitStatusRegister[num19].ObjectType == ObjectBase.ObjectType.Submarine && _tmpUnitStatusRegister[num19].Roles.Contains(ObjectBaseParameters.UnitRoles.SS))
			{
				sortList.Add(_tmpUnitStatusRegister[num19]);
				_tmpUnitStatusRegister.RemoveAt(num19);
				num19--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num20 = 0; num20 < _tmpUnitStatusRegister.Count; num20++)
		{
			if (_tmpUnitStatusRegister[num20].IsDestroyed == destroyed && _tmpUnitStatusRegister[num20].IsDamaged == damaged && _tmpUnitStatusRegister[num20].ObjectType == ObjectBase.ObjectType.Submarine)
			{
				sortList.Add(_tmpUnitStatusRegister[num20]);
				_tmpUnitStatusRegister.RemoveAt(num20);
				num20--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num21 = 0; num21 < _tmpUnitStatusRegister.Count; num21++)
		{
			if (_tmpUnitStatusRegister[num21].IsDestroyed == destroyed && _tmpUnitStatusRegister[num21].IsDamaged == damaged && _tmpUnitStatusRegister[num21].ObjectType == ObjectBase.ObjectType.Aircraft && _tmpUnitStatusRegister[num21].Roles.Contains(ObjectBaseParameters.UnitRoles.Fighter))
			{
				sortList.Add(_tmpUnitStatusRegister[num21]);
				_tmpUnitStatusRegister.RemoveAt(num21);
				num21--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num22 = 0; num22 < _tmpUnitStatusRegister.Count; num22++)
		{
			if (_tmpUnitStatusRegister[num22].IsDestroyed == destroyed && _tmpUnitStatusRegister[num22].IsDamaged == damaged && _tmpUnitStatusRegister[num22].ObjectType == ObjectBase.ObjectType.Aircraft && _tmpUnitStatusRegister[num22].Roles.Contains(ObjectBaseParameters.UnitRoles.HeavyBomber))
			{
				sortList.Add(_tmpUnitStatusRegister[num22]);
				_tmpUnitStatusRegister.RemoveAt(num22);
				num22--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num23 = 0; num23 < _tmpUnitStatusRegister.Count; num23++)
		{
			if (_tmpUnitStatusRegister[num23].IsDestroyed == destroyed && _tmpUnitStatusRegister[num23].IsDamaged == damaged && _tmpUnitStatusRegister[num23].ObjectType == ObjectBase.ObjectType.Aircraft && _tmpUnitStatusRegister[num23].Roles.Contains(ObjectBaseParameters.UnitRoles.Bomber))
			{
				sortList.Add(_tmpUnitStatusRegister[num23]);
				_tmpUnitStatusRegister.RemoveAt(num23);
				num23--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num24 = 0; num24 < _tmpUnitStatusRegister.Count; num24++)
		{
			if (_tmpUnitStatusRegister[num24].IsDestroyed == destroyed && _tmpUnitStatusRegister[num24].IsDamaged == damaged && _tmpUnitStatusRegister[num24].ObjectType == ObjectBase.ObjectType.Aircraft && _tmpUnitStatusRegister[num24].Roles.Contains(ObjectBaseParameters.UnitRoles.SEAD))
			{
				sortList.Add(_tmpUnitStatusRegister[num24]);
				_tmpUnitStatusRegister.RemoveAt(num24);
				num24--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num25 = 0; num25 < _tmpUnitStatusRegister.Count; num25++)
		{
			if (_tmpUnitStatusRegister[num25].IsDestroyed == destroyed && _tmpUnitStatusRegister[num25].IsDamaged == damaged && _tmpUnitStatusRegister[num25].ObjectType == ObjectBase.ObjectType.Aircraft && _tmpUnitStatusRegister[num25].Roles.Contains(ObjectBaseParameters.UnitRoles.EW))
			{
				sortList.Add(_tmpUnitStatusRegister[num25]);
				_tmpUnitStatusRegister.RemoveAt(num25);
				num25--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num26 = 0; num26 < _tmpUnitStatusRegister.Count; num26++)
		{
			if (_tmpUnitStatusRegister[num26].IsDestroyed == destroyed && _tmpUnitStatusRegister[num26].IsDamaged == damaged && _tmpUnitStatusRegister[num26].ObjectType == ObjectBase.ObjectType.Aircraft && _tmpUnitStatusRegister[num26].Roles.Contains(ObjectBaseParameters.UnitRoles.AEW))
			{
				sortList.Add(_tmpUnitStatusRegister[num26]);
				_tmpUnitStatusRegister.RemoveAt(num26);
				num26--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num27 = 0; num27 < _tmpUnitStatusRegister.Count; num27++)
		{
			if (_tmpUnitStatusRegister[num27].IsDestroyed == destroyed && _tmpUnitStatusRegister[num27].IsDamaged == damaged && _tmpUnitStatusRegister[num27].ObjectType == ObjectBase.ObjectType.Aircraft && _tmpUnitStatusRegister[num27].Roles.Contains(ObjectBaseParameters.UnitRoles.Recon))
			{
				sortList.Add(_tmpUnitStatusRegister[num27]);
				_tmpUnitStatusRegister.RemoveAt(num27);
				num27--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num28 = 0; num28 < _tmpUnitStatusRegister.Count; num28++)
		{
			if (_tmpUnitStatusRegister[num28].IsDestroyed == destroyed && _tmpUnitStatusRegister[num28].IsDamaged == damaged && _tmpUnitStatusRegister[num28].ObjectType == ObjectBase.ObjectType.Aircraft && _tmpUnitStatusRegister[num28].Roles.Contains(ObjectBaseParameters.UnitRoles.MPA))
			{
				sortList.Add(_tmpUnitStatusRegister[num28]);
				_tmpUnitStatusRegister.RemoveAt(num28);
				num28--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num29 = 0; num29 < _tmpUnitStatusRegister.Count; num29++)
		{
			if (_tmpUnitStatusRegister[num29].IsDestroyed == destroyed && _tmpUnitStatusRegister[num29].IsDamaged == damaged && _tmpUnitStatusRegister[num29].ObjectType == ObjectBase.ObjectType.Aircraft && _tmpUnitStatusRegister[num29].Roles.Contains(ObjectBaseParameters.UnitRoles.ASW))
			{
				sortList.Add(_tmpUnitStatusRegister[num29]);
				_tmpUnitStatusRegister.RemoveAt(num29);
				num29--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num30 = 0; num30 < _tmpUnitStatusRegister.Count; num30++)
		{
			if (_tmpUnitStatusRegister[num30].IsDestroyed == destroyed && _tmpUnitStatusRegister[num30].IsDamaged == damaged && _tmpUnitStatusRegister[num30].ObjectType == ObjectBase.ObjectType.Aircraft && _tmpUnitStatusRegister[num30].Roles.Contains(ObjectBaseParameters.UnitRoles.ESM))
			{
				sortList.Add(_tmpUnitStatusRegister[num30]);
				_tmpUnitStatusRegister.RemoveAt(num30);
				num30--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num31 = 0; num31 < _tmpUnitStatusRegister.Count; num31++)
		{
			if (_tmpUnitStatusRegister[num31].IsDestroyed == destroyed && _tmpUnitStatusRegister[num31].IsDamaged == damaged && _tmpUnitStatusRegister[num31].ObjectType == ObjectBase.ObjectType.Aircraft && _tmpUnitStatusRegister[num31].Roles.Contains(ObjectBaseParameters.UnitRoles.Airliner))
			{
				sortList.Add(_tmpUnitStatusRegister[num31]);
				_tmpUnitStatusRegister.RemoveAt(num31);
				num31--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num32 = 0; num32 < _tmpUnitStatusRegister.Count; num32++)
		{
			if (_tmpUnitStatusRegister[num32].IsDestroyed == destroyed && _tmpUnitStatusRegister[num32].IsDamaged == damaged && _tmpUnitStatusRegister[num32].ObjectType == ObjectBase.ObjectType.Aircraft)
			{
				sortList.Add(_tmpUnitStatusRegister[num32]);
				_tmpUnitStatusRegister.RemoveAt(num32);
				num32--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num33 = 0; num33 < _tmpUnitStatusRegister.Count; num33++)
		{
			if (_tmpUnitStatusRegister[num33].IsDestroyed == destroyed && _tmpUnitStatusRegister[num33].IsDamaged == damaged && _tmpUnitStatusRegister[num33].ObjectType == ObjectBase.ObjectType.Helicopter && _tmpUnitStatusRegister[num33].Roles.Contains(ObjectBaseParameters.UnitRoles.Attack))
			{
				sortList.Add(_tmpUnitStatusRegister[num33]);
				_tmpUnitStatusRegister.RemoveAt(num33);
				num33--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num34 = 0; num34 < _tmpUnitStatusRegister.Count; num34++)
		{
			if (_tmpUnitStatusRegister[num34].IsDestroyed == destroyed && _tmpUnitStatusRegister[num34].IsDamaged == damaged && _tmpUnitStatusRegister[num34].ObjectType == ObjectBase.ObjectType.Helicopter && _tmpUnitStatusRegister[num34].Roles.Contains(ObjectBaseParameters.UnitRoles.ASW))
			{
				sortList.Add(_tmpUnitStatusRegister[num34]);
				_tmpUnitStatusRegister.RemoveAt(num34);
				num34--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num35 = 0; num35 < _tmpUnitStatusRegister.Count; num35++)
		{
			if (_tmpUnitStatusRegister[num35].IsDestroyed == destroyed && _tmpUnitStatusRegister[num35].IsDamaged == damaged && _tmpUnitStatusRegister[num35].ObjectType == ObjectBase.ObjectType.Helicopter && _tmpUnitStatusRegister[num35].Roles.Contains(ObjectBaseParameters.UnitRoles.Targeting))
			{
				sortList.Add(_tmpUnitStatusRegister[num35]);
				_tmpUnitStatusRegister.RemoveAt(num35);
				num35--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num36 = 0; num36 < _tmpUnitStatusRegister.Count; num36++)
		{
			if (_tmpUnitStatusRegister[num36].IsDestroyed == destroyed && _tmpUnitStatusRegister[num36].IsDamaged == damaged && _tmpUnitStatusRegister[num36].ObjectType == ObjectBase.ObjectType.Helicopter && _tmpUnitStatusRegister[num36].Roles.Contains(ObjectBaseParameters.UnitRoles.Transport))
			{
				sortList.Add(_tmpUnitStatusRegister[num36]);
				_tmpUnitStatusRegister.RemoveAt(num36);
				num36--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num37 = 0; num37 < _tmpUnitStatusRegister.Count; num37++)
		{
			if (_tmpUnitStatusRegister[num37].IsDestroyed == destroyed && _tmpUnitStatusRegister[num37].IsDamaged == damaged && _tmpUnitStatusRegister[num37].ObjectType == ObjectBase.ObjectType.Helicopter)
			{
				sortList.Add(_tmpUnitStatusRegister[num37]);
				_tmpUnitStatusRegister.RemoveAt(num37);
				num37--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num38 = 0; num38 < _tmpUnitStatusRegister.Count; num38++)
		{
			if (_tmpUnitStatusRegister[num38].IsDestroyed == destroyed && _tmpUnitStatusRegister[num38].IsDamaged == damaged && _tmpUnitStatusRegister[num38].ObjectType == ObjectBase.ObjectType.LandUnit && _tmpUnitStatusRegister[num38].Roles.Contains(ObjectBaseParameters.UnitRoles.Airfield))
			{
				sortList.Add(_tmpUnitStatusRegister[num38]);
				_tmpUnitStatusRegister.RemoveAt(num38);
				num38--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num39 = 0; num39 < _tmpUnitStatusRegister.Count; num39++)
		{
			if (_tmpUnitStatusRegister[num39].IsDestroyed == destroyed && _tmpUnitStatusRegister[num39].IsDamaged == damaged && _tmpUnitStatusRegister[num39].ObjectType == ObjectBase.ObjectType.LandUnit && _tmpUnitStatusRegister[num39].Roles.Contains(ObjectBaseParameters.UnitRoles.Port))
			{
				sortList.Add(_tmpUnitStatusRegister[num39]);
				_tmpUnitStatusRegister.RemoveAt(num39);
				num39--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num40 = 0; num40 < _tmpUnitStatusRegister.Count; num40++)
		{
			if (_tmpUnitStatusRegister[num40].IsDestroyed == destroyed && _tmpUnitStatusRegister[num40].IsDamaged == damaged && _tmpUnitStatusRegister[num40].ObjectType == ObjectBase.ObjectType.LandUnit && _tmpUnitStatusRegister[num40].Roles.Contains(ObjectBaseParameters.UnitRoles.ASuW))
			{
				sortList.Add(_tmpUnitStatusRegister[num40]);
				_tmpUnitStatusRegister.RemoveAt(num40);
				num40--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num41 = 0; num41 < _tmpUnitStatusRegister.Count; num41++)
		{
			if (_tmpUnitStatusRegister[num41].IsDestroyed == destroyed && _tmpUnitStatusRegister[num41].IsDamaged == damaged && _tmpUnitStatusRegister[num41].ObjectType == ObjectBase.ObjectType.LandUnit && _tmpUnitStatusRegister[num41].Roles.Contains(ObjectBaseParameters.UnitRoles.SAM))
			{
				sortList.Add(_tmpUnitStatusRegister[num41]);
				_tmpUnitStatusRegister.RemoveAt(num41);
				num41--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num42 = 0; num42 < _tmpUnitStatusRegister.Count; num42++)
		{
			if (_tmpUnitStatusRegister[num42].IsDestroyed == destroyed && _tmpUnitStatusRegister[num42].IsDamaged == damaged && _tmpUnitStatusRegister[num42].ObjectType == ObjectBase.ObjectType.LandUnit && _tmpUnitStatusRegister[num42].Roles.Contains(ObjectBaseParameters.UnitRoles.ASM))
			{
				sortList.Add(_tmpUnitStatusRegister[num42]);
				_tmpUnitStatusRegister.RemoveAt(num42);
				num42--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num43 = 0; num43 < _tmpUnitStatusRegister.Count; num43++)
		{
			if (_tmpUnitStatusRegister[num43].IsDestroyed == destroyed && _tmpUnitStatusRegister[num43].IsDamaged == damaged && _tmpUnitStatusRegister[num43].ObjectType == ObjectBase.ObjectType.LandUnit && _tmpUnitStatusRegister[num43].Roles.Contains(ObjectBaseParameters.UnitRoles.AAW))
			{
				sortList.Add(_tmpUnitStatusRegister[num43]);
				_tmpUnitStatusRegister.RemoveAt(num43);
				num43--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num44 = 0; num44 < _tmpUnitStatusRegister.Count; num44++)
		{
			if (_tmpUnitStatusRegister[num44].IsDestroyed == destroyed && _tmpUnitStatusRegister[num44].IsDamaged == damaged && _tmpUnitStatusRegister[num44].ObjectType == ObjectBase.ObjectType.LandUnit && _tmpUnitStatusRegister[num44].Roles.Contains(ObjectBaseParameters.UnitRoles.Gun))
			{
				sortList.Add(_tmpUnitStatusRegister[num44]);
				_tmpUnitStatusRegister.RemoveAt(num44);
				num44--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num45 = 0; num45 < _tmpUnitStatusRegister.Count; num45++)
		{
			if (_tmpUnitStatusRegister[num45].IsDestroyed == destroyed && _tmpUnitStatusRegister[num45].IsDamaged == damaged && _tmpUnitStatusRegister[num45].ObjectType == ObjectBase.ObjectType.LandUnit && _tmpUnitStatusRegister[num45].Roles.Contains(ObjectBaseParameters.UnitRoles.Radar))
			{
				sortList.Add(_tmpUnitStatusRegister[num45]);
				_tmpUnitStatusRegister.RemoveAt(num45);
				num45--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num46 = 0; num46 < _tmpUnitStatusRegister.Count; num46++)
		{
			if (_tmpUnitStatusRegister[num46].IsDestroyed == destroyed && _tmpUnitStatusRegister[num46].IsDamaged == damaged && _tmpUnitStatusRegister[num46].ObjectType == ObjectBase.ObjectType.LandUnit && _tmpUnitStatusRegister[num46].Roles.Contains(ObjectBaseParameters.UnitRoles.Target))
			{
				sortList.Add(_tmpUnitStatusRegister[num46]);
				_tmpUnitStatusRegister.RemoveAt(num46);
				num46--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
		for (int num47 = 0; num47 < _tmpUnitStatusRegister.Count; num47++)
		{
			if (_tmpUnitStatusRegister[num47].IsDestroyed == destroyed && _tmpUnitStatusRegister[num47].IsDamaged == damaged && _tmpUnitStatusRegister[num47].ObjectType == ObjectBase.ObjectType.LandUnit)
			{
				sortList.Add(_tmpUnitStatusRegister[num47]);
				_tmpUnitStatusRegister.RemoveAt(num47);
				num47--;
			}
		}
		sortList = sortList.OrderByDescending((UnitRegister x) => x.Value).ToList();
		targetRegister.AddRange(sortList);
		sortList.Clear();
	}

	private List<string> GetContactTypeClips(List<ObjectBase> tempList)
	{
		List<string> list = new List<string> { "", "", "", "" };
		foreach (ObjectBase temp in tempList)
		{
			switch (temp._type)
			{
			case ObjectBase.ObjectType.Aircraft:
			case ObjectBase.ObjectType.Helicopter:
			case ObjectBase.ObjectType.Missile:
			case ObjectBase.ObjectType.ASROC:
			case ObjectBase.ObjectType.RBU:
			case ObjectBase.ObjectType.Bomb:
			case ObjectBase.ObjectType.AerialRocket:
			case ObjectBase.ObjectType.Chaff:
			case ObjectBase.ObjectType.Projectile:
				list[0] = "ContactAirMultiple";
				break;
			case ObjectBase.ObjectType.LandUnit:
				if ((temp as LandUnit).LandUnitParams._landUnitSubType == LandUnitParameters.LandUnitSubType.OilRig)
				{
					list[2] = "ContactSurfaceMultiple";
				}
				else
				{
					list[1] = "ContactLandMultiple";
				}
				break;
			case ObjectBase.ObjectType.Vessel:
				list[2] = "ContactSurfaceMultiple";
				break;
			case ObjectBase.ObjectType.Submarine:
			case ObjectBase.ObjectType.Biologic:
			case ObjectBase.ObjectType.Torpedo:
			case ObjectBase.ObjectType.Noisemaker:
			case ObjectBase.ObjectType.Decoy:
				list[3] = "ContactSubmergedMultiple";
				break;
			}
		}
		for (int num = list.Count - 1; num >= 0; num--)
		{
			if (list[num] == "")
			{
				list.RemoveAt(num);
			}
		}
		return list;
	}

	private string GetSingleContactTypeClip(ObjectBase obj)
	{
		string result = "";
		switch (obj._type)
		{
		case ObjectBase.ObjectType.Aircraft:
		case ObjectBase.ObjectType.Helicopter:
		case ObjectBase.ObjectType.Missile:
		case ObjectBase.ObjectType.ASROC:
		case ObjectBase.ObjectType.RBU:
		case ObjectBase.ObjectType.Bomb:
		case ObjectBase.ObjectType.AerialRocket:
		case ObjectBase.ObjectType.Chaff:
		case ObjectBase.ObjectType.Projectile:
			result = "ContactAirF";
			break;
		case ObjectBase.ObjectType.LandUnit:
			result = (((obj as LandUnit).LandUnitParams._landUnitSubType != LandUnitParameters.LandUnitSubType.OilRig) ? "ContactLandF" : "ContactSurfaceF");
			break;
		case ObjectBase.ObjectType.Vessel:
			result = "ContactSurfaceF";
			break;
		case ObjectBase.ObjectType.Submarine:
		case ObjectBase.ObjectType.Biologic:
		case ObjectBase.ObjectType.Torpedo:
		case ObjectBase.ObjectType.Noisemaker:
		case ObjectBase.ObjectType.Decoy:
			result = "ContactSubmergedF";
			break;
		}
		return result;
	}

	private void CheckNewContactMessage()
	{
		if (GameTime.time < _newContactsToReportTime)
		{
			return;
		}
		_newContactsToReportTime = GameTime.time + 3f;
		bool flag = true;
		List<ObjectBase> list = Utils.CopyList(_newContactsToReport);
		foreach (ObjectBase item in _classifiedContactsToReport)
		{
			if (list.Contains(item))
			{
				list.Remove(item);
			}
		}
		if (list.Count > 1)
		{
			flag = false;
			List<string> contactTypeClips = GetContactTypeClips(list);
			string text = Singleton<LanguageResourceHandler>.Instance.getText("Contact", contactTypeClips[0]);
			for (int i = 1; i < contactTypeClips.Count; i++)
			{
				text = ((i >= contactTypeClips.Count - 1) ? (text + " " + Singleton<LanguageResourceHandler>.Instance.getText("YesNo", "And") + " ") : (text + ", "));
				text += Singleton<LanguageResourceHandler>.Instance.getText("Contact", contactTypeClips[i]);
			}
			string text2 = Singleton<LanguageResourceHandler>.Instance.getText("Contact", "NewContactMultiple");
			text2 = text2.Replace("${ContactType}", text);
			Globals._mainGameViewModel.RaiseAlert(text2);
			if (Singleton<OptionsManager>.Instance.ContactVO)
			{
				if (VoiceoverNation == "Soviet")
				{
					Singleton<VoiceMessage>.Instance.PlayVoiceMessage("NewContactMultiple", VoiceoverNation);
				}
				else
				{
					Singleton<VoiceMessage>.Instance.AddVoiceToSequence("Multiple", VoiceoverNation);
					foreach (string item2 in contactTypeClips)
					{
						Singleton<VoiceMessage>.Instance.AddVoiceToSequence(item2, VoiceoverNation);
					}
					Singleton<VoiceMessage>.Instance.AddVoiceToSequence("Contacts", VoiceoverNation);
				}
			}
		}
		foreach (ObjectBase item3 in _newContactsToReport)
		{
			if (item3 == null)
			{
				continue;
			}
			Vehicle mapVehicle = item3.GetMapVehicle(Globals._playerTaskforce);
			if (mapVehicle == null || item3 is LaunchTransient)
			{
				continue;
			}
			ObjectBase objectBase = null;
			foreach (Entity item4 in mapVehicle.ValidTracks())
			{
				TrackComponent? trackComponent = TrackEntityUtils.GetTrackComponent(item4);
				if (trackComponent.HasValue)
				{
					ObjectBase objectBase2 = TrackEntityUtils.GetObjectBase(trackComponent.GetValueOrDefault().Source);
					if ((object)objectBase2 != null && !(objectBase2 == null))
					{
						objectBase = objectBase2;
					}
				}
			}
			Globals._lastContactReportObject = objectBase;
			string text3 = "";
			switch (mapVehicle.Type.Value)
			{
			case SensorSystem.SensorType.Visual:
				text3 = "VisualContact";
				break;
			case SensorSystem.SensorType.TVSeeker:
				text3 = "VisualContact";
				break;
			case SensorSystem.SensorType.SearchRadar:
			case SensorSystem.SensorType.FCR:
			case SensorSystem.SensorType.ARS:
				text3 = "RadarContact";
				break;
			case SensorSystem.SensorType.ActiveSonar:
				text3 = "ActiveContact";
				break;
			case SensorSystem.SensorType.PassiveSonar:
				text3 = "PassiveContact";
				break;
			case SensorSystem.SensorType.ActiveIntercept:
				text3 = "PassiveContact";
				break;
			case SensorSystem.SensorType.ESM:
				text3 = "ESMContact";
				break;
			case SensorSystem.SensorType.MAD:
				text3 = "MADContact";
				break;
			case SensorSystem.SensorType.Unknown:
			case SensorSystem.SensorType.Self:
				continue;
			}
			string text4 = Singleton<LanguageResourceHandler>.Instance.getText("Contact", "New" + text3);
			text4 = text4.Replace("${TrackId}", mapVehicle.Id.ToString());
			if (objectBase != null)
			{
				Globals._mainGameViewModel.RaiseAlert(objectBase, "${Name}: " + text4);
			}
			else
			{
				Globals._mainGameViewModel.RaiseAlert(text4);
			}
			if (!string.IsNullOrEmpty(text3) && Singleton<OptionsManager>.Instance.ContactVO && flag && list.Contains(item3))
			{
				Singleton<VoiceMessage>.Instance.AddVoiceToSequence("NewF", VoiceoverNation);
				Singleton<VoiceMessage>.Instance.AddVoiceToSequence(GetSingleContactTypeClip(item3), VoiceoverNation);
				Singleton<VoiceMessage>.Instance.AddVoiceToSequence("Contact", VoiceoverNation);
			}
		}
		_newContactsToReport.Clear();
	}

	private void CheckContactClassifiedMessage()
	{
		if (GameTime.time < _classifiedContactsToReportTime)
		{
			return;
		}
		_classifiedContactsToReportTime = GameTime.time + 3f;
		bool flag = true;
		int num = 0;
		for (int num2 = _classifiedContactsToReport.Count - 1; num2 >= 0; num2--)
		{
			if (_classifiedContactsToReport[num2] == null)
			{
				_classifiedContactsToReport.RemoveAt(num2);
			}
			else
			{
				if (_classifiedContactsToReport[num2]._taskforce.RelationshipTo(this) == RelationsState.Hostile)
				{
					num++;
				}
				if (!(_classifiedContactsToReport[num2] is Aircraft))
				{
					flag = false;
				}
			}
		}
		bool flag2 = true;
		if (_classifiedContactsToReport.Count > 1)
		{
			flag2 = false;
			string text;
			if (num > 0 && flag)
			{
				if (num > 1)
				{
					text = Singleton<LanguageResourceHandler>.Instance.getText("Contact", "ClassifiedHostileAirMultiple");
					text = text.Replace("${ContactType}", "ContactAirMultiple");
					if (Singleton<OptionsManager>.Instance.ContactVO)
					{
						Singleton<VoiceMessage>.Instance.PlayVoiceMessage("ClassifiedHostileAirMultiple", VoiceoverNation);
					}
				}
				else
				{
					text = Singleton<LanguageResourceHandler>.Instance.getText("Contact", "ClassifiedHostileAir");
					text = text.Replace("${ContactType}", "ContactAir");
					if (Singleton<OptionsManager>.Instance.ContactVO)
					{
						Singleton<VoiceMessage>.Instance.PlayVoiceMessage("ClassifiedHostileAir", VoiceoverNation);
					}
				}
			}
			else
			{
				List<string> contactTypeClips = GetContactTypeClips(_classifiedContactsToReport);
				string text2 = Singleton<LanguageResourceHandler>.Instance.getText("Contact", contactTypeClips[0]);
				for (int i = 1; i < contactTypeClips.Count; i++)
				{
					text2 = ((i >= contactTypeClips.Count - 1) ? (text2 + " " + Singleton<LanguageResourceHandler>.Instance.getText("YesNo", "And") + " ") : (text2 + ", "));
					text2 += Singleton<LanguageResourceHandler>.Instance.getText("Contact", contactTypeClips[i]);
				}
				text = ((num > 0) ? Singleton<LanguageResourceHandler>.Instance.getText("Contact", "ClassifiedHostileMultiple") : Singleton<LanguageResourceHandler>.Instance.getText("Contact", "ClassifiedContactMultiple"));
				text = text.Replace("${ContactType}", text2);
				if (Singleton<OptionsManager>.Instance.ContactVO)
				{
					if (VoiceoverNation == "Soviet")
					{
						if (num > 1)
						{
							Singleton<VoiceMessage>.Instance.PlayVoiceMessage("ClassifiedHostileMultiple", VoiceoverNation);
						}
						else
						{
							Singleton<VoiceMessage>.Instance.PlayVoiceMessage("ClassifiedContactMultiple", VoiceoverNation);
						}
					}
					else
					{
						Singleton<VoiceMessage>.Instance.AddVoiceToSequence("Multiple", VoiceoverNation);
						if (num > 1)
						{
							Singleton<VoiceMessage>.Instance.AddVoiceToSequence("HostileS", VoiceoverNation);
						}
						foreach (string item in contactTypeClips)
						{
							Singleton<VoiceMessage>.Instance.AddVoiceToSequence(item, VoiceoverNation);
						}
						Singleton<VoiceMessage>.Instance.AddVoiceToSequence("Contacts", VoiceoverNation);
					}
				}
			}
			Globals._mainGameViewModel.RaiseAlert(text);
		}
		if (num == 1 && flag)
		{
			string text3 = Singleton<LanguageResourceHandler>.Instance.getText("Contact", "ClassifiedHostileAir");
			text3 = text3.Replace("${ContactType}", "ContactAir");
			Globals._mainGameViewModel.RaiseAlert(text3);
			if (Singleton<LanguageResourceHandler>.Instance.Language != "ru")
			{
				if (Singleton<OptionsManager>.Instance.ContactVO)
				{
					Singleton<VoiceMessage>.Instance.PlayVoiceMessage("ClassifiedHostileAir", VoiceoverNation);
				}
				flag2 = false;
			}
		}
		foreach (ObjectBase item2 in _classifiedContactsToReport)
		{
			Vehicle mapVehicle = item2.GetMapVehicle(Globals._playerTaskforce);
			if (mapVehicle == null || mapVehicle.BaseObject is LaunchTransient)
			{
				continue;
			}
			string key = "";
			bool flag3 = true;
			if (mapVehicle.BaseObject is LandUnit landUnit)
			{
				switch (landUnit.LandUnitParams._landUnitSubType)
				{
				case LandUnitParameters.LandUnitSubType.AirBase:
					key = "Airbase";
					break;
				case LandUnitParameters.LandUnitSubType.Installation:
					key = "Building";
					break;
				case LandUnitParameters.LandUnitSubType.MobileUnit:
				case LandUnitParameters.LandUnitSubType.AAA:
					key = "LandVehicle";
					break;
				case LandUnitParameters.LandUnitSubType.SAM:
					key = "SAMSite";
					break;
				case LandUnitParameters.LandUnitSubType.MissileSite:
					key = "MissileSite";
					break;
				case LandUnitParameters.LandUnitSubType.Radar:
					key = "Radar";
					break;
				case LandUnitParameters.LandUnitSubType.Port:
					key = "Port";
					break;
				default:
					key = "Ground";
					break;
				}
			}
			else if (mapVehicle.BaseObject is Vessel)
			{
				key = (mapVehicle.BaseObject._obp._objectIniName.StartsWith("civ_") ? "CivShip" : ((!mapVehicle.BaseObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Spy)) ? "Vessel" : "CivShip"));
			}
			else if (mapVehicle.BaseObject is Submarine)
			{
				key = "Submarine";
			}
			else if (mapVehicle.BaseObject is Aircraft)
			{
				key = "Aircraft";
			}
			else if (mapVehicle.BaseObject is Helicopter)
			{
				key = "Helicopter";
			}
			else if (mapVehicle.BaseObject is WeaponBase)
			{
				if (mapVehicle.BaseObject is Missile)
				{
					key = "Missile";
				}
				else if (mapVehicle.BaseObject is Torpedo)
				{
					key = "Torpedo";
				}
				else
				{
					flag3 = false;
				}
			}
			else
			{
				flag3 = false;
			}
			if (flag3)
			{
				string[] array = Singleton<LanguageResourceHandler>.Instance.getText("Contact", key).Split("|");
				string newValue = array[0];
				string text4 = "";
				if (array.Length > 1)
				{
					text4 = array[1];
				}
				string text5 = Singleton<LanguageResourceHandler>.Instance.getText("Contact", "TrackClassified");
				text5 = text5.Replace("${TrackId}", mapVehicle.Id.ToString());
				text5 = ((mapVehicle.CurrentRelationship() != RelationsState.Neutral) ? text5.Replace("${Relation}", Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", item2._taskforce.RelationshipTo(this).ToString() + text4).ToLower()) : text5.Replace("${Relation}", Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "Neutral" + text4).ToLower()));
				text5 = text5.Replace("${Type}", newValue);
				Globals._mainGameViewModel.RaiseAlert(text5);
			}
		}
		if (flag2 && Singleton<OptionsManager>.Instance.ContactVO)
		{
			foreach (ObjectBase item3 in _classifiedContactsToReport)
			{
				Vehicle mapVehicle2 = item3.GetMapVehicle(Globals._playerTaskforce);
				if (mapVehicle2 == null || mapVehicle2.BaseObject is LaunchTransient)
				{
					continue;
				}
				string text6 = "";
				bool flag4 = true;
				if (mapVehicle2.BaseObject is LandUnit landUnit2)
				{
					switch (landUnit2.LandUnitParams._landUnitSubType)
					{
					case LandUnitParameters.LandUnitSubType.AirBase:
						text6 = "Airbase";
						break;
					case LandUnitParameters.LandUnitSubType.Installation:
						text6 = "Building";
						break;
					case LandUnitParameters.LandUnitSubType.MobileUnit:
					case LandUnitParameters.LandUnitSubType.AAA:
						text6 = "LandVehicle";
						break;
					case LandUnitParameters.LandUnitSubType.SAM:
						text6 = "SAMSite";
						break;
					case LandUnitParameters.LandUnitSubType.MissileSite:
						text6 = "MissileSite";
						break;
					case LandUnitParameters.LandUnitSubType.Radar:
						text6 = "Radar";
						break;
					case LandUnitParameters.LandUnitSubType.Port:
						text6 = "Port";
						break;
					default:
						text6 = "Ground";
						break;
					}
				}
				else if (mapVehicle2.BaseObject is Vessel)
				{
					text6 = (mapVehicle2.BaseObject._obp._objectIniName.StartsWith("civ_") ? "CivShip" : ((mapVehicle2.CurrentRelationship() != RelationsState.Neutral) ? "Vessel" : "CivShip"));
				}
				else if (mapVehicle2.BaseObject is Submarine)
				{
					text6 = "SubmergedSubmarine";
				}
				else if (mapVehicle2.BaseObject is Aircraft)
				{
					text6 = "Aircraft";
				}
				else if (mapVehicle2.BaseObject is Helicopter)
				{
					text6 = "Helicopter";
				}
				else if (mapVehicle2.BaseObject is WeaponBase)
				{
					flag4 = false;
					if (mapVehicle2.BaseObject is Missile)
					{
						text6 = "Missile";
					}
					else if (mapVehicle2.BaseObject is Torpedo)
					{
						text6 = "Torpedo";
					}
				}
				else
				{
					flag4 = false;
				}
				if (flag4)
				{
					string text7 = "";
					string[] array2 = Singleton<LanguageResourceHandler>.Instance.getText("Contact", text6).Split("|");
					if (array2.Length > 1)
					{
						text7 = array2[1];
					}
					Singleton<VoiceMessage>.Instance.AddVoiceToSequence("TrackNumber", VoiceoverNation, mapVehicle2.Id);
					Singleton<VoiceMessage>.Instance.AddVoiceToSequence("IsClassified", VoiceoverNation);
					Singleton<VoiceMessage>.Instance.AddVoiceToSequence(RelationshipTo(mapVehicle2.UnitTaskforce.Value).ToString() + text7, VoiceoverNation);
					Singleton<VoiceMessage>.Instance.AddVoiceToSequence(text6, VoiceoverNation);
				}
			}
		}
		_classifiedContactsToReport.Clear();
	}

	private void CheckContactFadedMessage()
	{
		if (GameTime.time < _fadedCContactsToReportTime)
		{
			return;
		}
		_fadedCContactsToReportTime = GameTime.time + 3f;
		if (_fadedContactsToReport.Count == 0)
		{
			return;
		}
		foreach (KeyValuePair<ObjectBase, (int, float)> item in _fadedContactsToReport)
		{
			Globals._mainGameViewModel.RaiseAlert(Singleton<LanguageResourceHandler>.Instance.getText("Contact", "ContactFaded").Replace("${TrackId}", item.Value.Item1.ToString()));
		}
		if (_fadedContactsToReport.Count > 1)
		{
			if (Singleton<OptionsManager>.Instance.ContactVO && Singleton<OptionsManager>.Instance.ContactFaded)
			{
				Globals._mainGameViewModel.RaiseAlert(Singleton<LanguageResourceHandler>.Instance.getText("Contact", "ContactFadedMultiple"));
			}
		}
		else if (Singleton<OptionsManager>.Instance.ContactVO && Singleton<OptionsManager>.Instance.ContactFaded)
		{
			Singleton<VoiceMessage>.Instance.PlayVoiceSequence("ContactFaded", VoiceoverNation, _fadedContactsToReport.ElementAt(0).Value.id);
		}
		_fadedContactsToReport.Clear();
	}
}
