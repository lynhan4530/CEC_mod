using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UniRx;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;

namespace SeaPower;

public abstract class WeaponBase : ObjectBase
{
	public enum FlightStage
	{
		None,
		DropFromAircraft,
		Launch,
		GoToSurface,
		ToBearing,
		MaintainHeight,
		MoveToLoftAlt,
		MaintainLoftAlt,
		MoveToSeaSkimming,
		MaintainSeaSkimming,
		MoveToFinalFlightAlt,
		MaintainFinalFlightAlt,
		AlignSensorToTarget,
		TerminalApproach,
		Deployed,
		Debris,
		Terminated,
		SelfDestruct,
		UnderwaterLaunch,
		AboveWaterLaunch,
		TorpedoAboveWaterPhase,
		AircraftDropLaunch,
		ASROCFlight,
		ASROCSeparation,
		TorpedoDropping,
		TorpedoParachuteEject,
		TorpedoParachuteDropping,
		UnderwaterToBearing,
		UnderwaterToPosition,
		TorpedoWaterEntry,
		UnderwaterToDepth,
		UnderwaterMovement,
		EnterSearchMode,
		EnterSpiralSearch,
		SpiralSearchDown,
		SpiralSearchUp,
		EnterSnakeSearch,
		PerformSnakeSearch,
		EnterCircleSearch,
		PerformCircleSearch,
		EnterLadderSearch,
		PerformLadderSearch,
		Reattack,
		ReattackCircle,
		GuidedByWire
	}

	public struct Course
	{
		public Vector3 Direction;

		public Vector3 Position;
	}

	private static readonly ProfilerMarker<int> _profSensorUpdates = new ProfilerMarker<int>("WeaponBase sensor updates", "Count");

	private static readonly ProfilerMarker _profGeoPosition = new ProfilerMarker("WeaponBase geo position");

	private static readonly ProfilerMarker _profSensorsUpdate = new ProfilerMarker("WeaponBase sensors update");

	private static readonly ProfilerMarker _profRaycast = new ProfilerMarker("WeaponBase raycast");

	private static readonly ProfilerMarker _profHitsLoop = new ProfilerMarker("WeaponBase hits loop");

	public Vector3 _gravity = Vector3.zero;

	public int _attachmentNumber;

	protected Vector3Event _relocationEvent;

	public Vector3 _newPos;

	public Vector3 _lastPos;

	public Vector3 _currentPos;

	public List<Waypoint> Waypoints = new List<Waypoint>();

	public GeoPosition _lastWaypointPosition;

	private bool _initializedLastUnrelocatedPos;

	private Vector3 _lastUnrelocatedPos = Vector3.zero;

	public Vector3 _deviationVector;

	public float _dt;

	public bool _isSubmunition;

	public ObjectBase _launchPlatform;

	public WeaponBase _parentWeapon;

	public bool _hasSubmunition;

	public GeoPosition _launchPosition;

	public Vector3 _moveOfLaunchPlatform = Vector3.zero;

	public WeaponSystem _weaponSystem;

	protected Vector3 _launcherSpeedVector;

	protected bool _isLaunched;

	public float _launchTime;

	public float _dropSpeed;

	public bool _isBoosterEffectStarted;

	public float _boosterEffectStartTime;

	public bool _isInFlightEffectStarted;

	protected Vector3 _rotation;

	public ObjectBase _initialSetTargetObject;

	public ObjectBase _currentIntendedTargetObject;

	public ObjectBase _lastTargetObject;

	public Vector3 _initialSetTargetPosition;

	public Vector3 _currentKnownTargetPosition;

	protected GeoPosition _aimPointGeoPosition;

	public Vector3 _estimatedInterceptTargetPosition;

	public GeoPosition _estimatedInterceptTargetPosition2;

	public LandUnitParameters.TargetPoint _currentTargetPoint;

	public Vector3 _offsetVector = Vector3.zero;

	protected float _guideStartTime;

	public bool _requiresExternalGuidance;

	public float _traveledDistance;

	public float _remainingRangePercent;

	public float _velocityBleedRangePercentStart;

	public WeaponInstance _weaponInstance;

	public AmmunitionParameters _ap;

	private FlightStage _flightStageInternal = FlightStage.Launch;

	protected FlightStage _prevflightState;

	public float _stageStartTime;

	public bool _launchObjectSwitched;

	public bool _collisionNoticed;

	protected float _closestDistToTargetSq;

	public bool _isTargetAirborne;

	public bool _isInterceptable;

	public GameObject _boosterEffect;

	public GameObject _inFlightEffect;

	public GameObject _underwaterTrailEffect;

	public bool _isUnderwaterTrailEffectStarted;

	public GameObject _parachuteEffect;

	protected GameObject _debrisFireEffect;

	public SensorSystemRadar _guidingRadarSystem;

	public bool _laserDesignatorOnTheLaunchPlatform;

	public bool _onWire;

	protected Vector3 _lastVelocityVector;

	public float _targetLostTime = -1f;

	public bool _targetLost;

	protected bool _didSeekerInitiallyPickUpTarget;

	public bool _maintainingHeight;

	protected Vector3 _estimatedInterceptTargetVelocityVector;

	protected Collider[] _quickOverlapCheckResults = new Collider[1];

	protected RaycastParams _raycastParams = new RaycastParams();

	public TrulyObservableCollection<SensorSystem> _sensors = new TrulyObservableCollection<SensorSystem>();

	public SeekerBase _seeker;

	protected bool _noAssociatedSensorSystemWarningLogged;

	public float _lostGuidanceTime;

	public bool _jammed;

	public float _deviationMagnitudeWithJam;

	public List<ECM> _defensiveECMsCheckedThisWeapon = new List<ECM>();

	protected float _debrisStartTime;

	protected float _debrisLifeTime;

	protected Vector3 _debrisMoveDirection;

	protected float _debrisVelocityDampening;

	protected float _debrisHeightLossAdd;

	protected float _debrisSpeed;

	protected float _debrisDownSpeed;

	protected bool _resourcesLoaded;

	public bool _checkForWaterExit;

	public bool _proximityFuse;

	public bool _fuseActive;

	public bool _blastzoneCreated;

	public bool _ignoreCollisions;

	protected GameObject _proximityRadius;

	public float _lastDistanceToCollisionSq;

	public bool _smartFuseActive;

	protected Transform _smartFuseTargetTransform;

	public int _alignmentUID;

	public TrailRenderer _debugTrail;

	public bool _hitLand;

	public (bool _enabled, GeoPosition _lastPosGP, GeoPosition _curPosGP, GeoPosition _estimatedInterceptTargetGP, Vector3 _fwdVec) _ballisticsOverride = (_enabled: false, _lastPosGP: default(GeoPosition), _curPosGP: default(GeoPosition), _estimatedInterceptTargetGP: default(GeoPosition), _fwdVec: Vector3.forward);

	public float _TVRCdeviationMagnitude;

	public float EffectiveFOV
	{
		get
		{
			if (_ap._guidanceType == AmmunitionParameters.GuidanceType.None)
			{
				if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand || _ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.WireGuided)
				{
					return 140f;
				}
				return -1f;
			}
			if (_ap._guidanceType != AmmunitionParameters.GuidanceType.IRHoming)
			{
				return _ap._seekerFOV;
			}
			return _ap._seekerGimbalFOV;
		}
	}

	public ObjectBase CurrentTarget
	{
		get
		{
			if (_seeker == null)
			{
				return null;
			}
			if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.None)
			{
				return CurrentIntendedTargetObject;
			}
			if (!_seeker.IsOn.Value)
			{
				return CurrentIntendedTargetObject;
			}
			if (_seeker?._strongestSensorEcho?._objectBase != null)
			{
				return _seeker?._strongestSensorEcho?._objectBase;
			}
			if (_ap._targetMemory)
			{
				return CurrentIntendedTargetObject;
			}
			return null;
		}
	}

	public ObjectBase CurrentIntendedTargetObject
	{
		get
		{
			return _currentIntendedTargetObject;
		}
		set
		{
			_currentIntendedTargetObject = value;
		}
	}

	public SensorEcho CurrentStrongestTargetEcho => _seeker?._strongestSensorEcho;

	public GeoPosition InitialTargetGeoPosition { get; set; }

	public GeoPosition AimPointGeoPosition
	{
		get
		{
			return _aimPointGeoPosition;
		}
		set
		{
			_aimPointGeoPosition = value;
		}
	}

	public GeoPosition EstimatedInterceptTargetGeoPosition
	{
		get
		{
			return _estimatedInterceptTargetPosition2;
		}
		set
		{
			_estimatedInterceptTargetPosition2 = value;
		}
	}

	public FlightStage _flightStage
	{
		get
		{
			return _flightStageInternal;
		}
		set
		{
			if (_flightStageInternal != value)
			{
				_prevflightState = _flightStageInternal;
				_flightStageInternal = value;
				this.OnFlightStageChanged?.Invoke(_flightStageInternal);
			}
		}
	}

	public ReactiveProperty<bool> ConnectionLost { get; } = new ReactiveProperty<bool>
	{
		Value = false
	};

	public ReactiveProperty<bool> ConnectionLostForever { get; } = new ReactiveProperty<bool>
	{
		Value = false
	};

	public event Action<FlightStage> OnFlightStageChanged;

	public WeaponBase()
	{
	}

	public override void OnStart()
	{
		base.OnStart();
	}

	public virtual void initResources(string ammunitionFileName, WeaponInstance weaponInstance)
	{
		_weaponInstance = weaponInstance;
		if (!_resourcesLoaded)
		{
			loadResources(ammunitionFileName);
		}
	}

	public virtual void init(ObjectBase launchPlatform, Vector3 localPosition, AmmunitionParameters ap)
	{
		_traveledDistance = 0f;
		_relocationEvent = new Vector3Event();
		_relocationEvent.AddListener(onRelocate);
		Singleton<RelocationHandler>.Instance.addToEventsList(_relocationEvent);
		_launchPlatform = launchPlatform;
		if (_weaponInstance != null)
		{
			if (_weaponInstance._mainMeshForLaunch != null)
			{
				_weaponInstance._mainMesh.GetComponent<Renderer>().enabled = true;
				_launchObjectSwitched = false;
			}
			else
			{
				_launchObjectSwitched = true;
			}
			_weaponInstance._collidersObject.SetActive(value: false);
		}
		base.transform.localPosition = localPosition;
		base.transform.localEulerAngles = _rotation;
		if (launchPlatform != null)
		{
			setGeoPosition(Utils.worldPositionFromUnityToLongLat(base.transform.TransformPoint(localPosition), Globals._currentCenterTile));
		}
		_ap = ap;
		initSeeker(_ap._guidanceType);
		_guideStartTime = -1f;
		_isBoosterEffectStarted = false;
		_isInFlightEffectStarted = false;
		_collisionNoticed = false;
		_targetLost = false;
		_targetLostTime = -1f;
		_didSeekerInitiallyPickUpTarget = false;
		base.IsDestroyed = false;
		_hasControl = false;
		_currentTargetPoint = default(LandUnitParameters.TargetPoint);
		_flightStage = FlightStage.Launch;
		_isLaunched = false;
		_hitLand = false;
		_soundHandler = new ObjectSoundHandler(this);
		_deviationMagnitudeWithJam = 0f;
		if (_debugTrail != null)
		{
			_debugTrail.enabled = false;
		}
		int num = 1;
		if (!string.IsNullOrEmpty(_ap._radarList))
		{
			GameObject gameObject = new GameObject("Dummy Sensor Mount");
			gameObject.transform.SetParent(base.transform);
			gameObject.transform.localPosition = new Vector3(0f, 0f - _ap._sensorHeight, 0f);
			string[] array = _ap._radarList.Split(',');
			foreach (string systemName in array)
			{
				SensorSystemRadar sensorSystemRadar = new SensorSystemRadar(this, systemName, gameObject);
				sensorSystemRadar._transmitterRange = _ap._transmitterRange;
				sensorSystemRadar.Disable();
				_sensors.Add(sensorSystemRadar);
			}
		}
		if (!string.IsNullOrEmpty(_ap._ecmList))
		{
			GameObject gameObject2 = new GameObject("Dummy Sensor Mount");
			gameObject2.transform.SetParent(base.transform);
			gameObject2.transform.localPosition = new Vector3(0f, 0f - _ap._sensorHeight, 0f);
			string[] array = _ap._ecmList.Split(',');
			foreach (string systemName2 in array)
			{
				SensorSystemECM sensorSystemECM = new SensorSystemECM(this, systemName2, gameObject2);
				sensorSystemECM._sectionName = $"Sensor_{num}";
				num++;
				sensorSystemECM.Disable();
				_sensors.Add(sensorSystemECM);
			}
		}
	}

	protected void loadResources(string fileName)
	{
		IniHandler iniHandler = Utils.openIniFile("/ammunition", "/" + fileName + ".ini");
		if (iniHandler != null)
		{
			_defaultCameraDistance = iniHandler.readValue("General", "DefaultCameraDistance", 0.6f);
			_minCameraDistanceForeAft = iniHandler.readValue("General", "MinCameraDistanceForeAft", 0.4f);
			_minCameraDistanceBroadside = iniHandler.readValue("General", "MinCameraDistanceBroadside", 0.4f);
			_cameraPivotHeight = iniHandler.readValue("General", "CameraPivotHeight", 0f);
			_rotation = iniHandler.readValue("Models", "Rotation", Vector3.zero, performLogging: false);
			if (!iniHandler.doesKeyExist("Particles", "InFlightEffectClass"))
			{
				_isInFlightEffectStarted = true;
			}
			_resourcesLoaded = true;
		}
	}

	public override void OnUpdateEveryFrame()
	{
		if (!_isLaunched)
		{
			return;
		}
		if (GameTime.TimeCompression > Globals._weaponsTCLimit && _type != ObjectType.Noisemaker && _ap._subType != Ammunition.Type.Sonobuoy && _ap._subType != Ammunition.Type.Paratrooper)
		{
			GameTime.SetTimeCompression(Globals._weaponsTCLimit);
		}
		if (_onWire)
		{
			if (_launchPlatform != null)
			{
				if (_launchPlatform.IsDestroyed)
				{
					_onWire = false;
				}
				else
				{
					ObjectBase launchPlatform = _launchPlatform;
					if ((launchPlatform is Vessel || launchPlatform is Submarine) && _launchPlatform._velocityInKnots > 20f)
					{
						_onWire = false;
					}
				}
			}
			else
			{
				_onWire = false;
			}
			if (!_onWire)
			{
				ConnectionLost.Value = true;
				ConnectionLostForever.Value = true;
			}
		}
		if (_proximityRadius != null)
		{
			if (_proximityRadius.activeSelf && !DM._weaponVisualisationMode)
			{
				_proximityRadius.SetActive(value: false);
			}
			else if (DM._weaponVisualisationMode && _proximityFuse)
			{
				_proximityRadius.SetActive(value: true);
			}
		}
		if (_checkForWaterExit && base.transform.position.y > 0f)
		{
			_checkForWaterExit = false;
			if (_type != ObjectType.Torpedo)
			{
				_launchTime = GameTime.time;
				Vector3 position = new Vector3(base.transform.position.x, 0f, base.transform.position.z);
				Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_ap._hitWaterSplashEffect, null, position));
			}
		}
		foreach (SensorSystem sensor in _sensors)
		{
			sensor.OnUpdate();
		}
		base.OnUpdateEveryFrame();
		_soundHandler.OnUpdate();
	}

	public override void OnFixedUpdate()
	{
		if (!_isLaunched)
		{
			if (_debugTrail != null && _debugTrail.enabled)
			{
				_debugTrail.enabled = false;
			}
			return;
		}
		_dt = GameTime.time - _launchTime;
		if (!(this is Missile))
		{
			base.CommandPosition = AimPointGeoPosition;
		}
		foreach (SensorSystem sensor in _sensors)
		{
			sensor.OnFixedUpdate();
		}
		if (DM._showWeaponTrails && (DM._weaponVisualisationMode || _ap._type != Ammunition.Type.Chaff))
		{
			if (_debugTrail == null)
			{
				_debugTrail = base.transform.gameObject.AddComponent<TrailRenderer>();
				_debugTrail.time = 600f;
				_debugTrail.startWidth = 0.01f;
				_debugTrail.endWidth = 0.01f;
				_debugTrail.material.color = Color.black * 10f;
				Globals._trailRendererList.Add(_debugTrail);
			}
			else if (!_debugTrail.enabled)
			{
				_debugTrail.enabled = true;
			}
		}
		else if (base.transform.gameObject.GetComponent<TrailRenderer>() != null)
		{
			base.transform.gameObject.GetComponent<TrailRenderer>().enabled = false;
		}
		if (_launchObjectSwitched && !_ap._supportsBanking && _ap._axialRotationDegrees > 0f)
		{
			Vector3 zero = Vector3.zero;
			zero.z = _ap._axialRotationDegrees * GameTime.fixedDeltaTime;
			_weaponInstance._mainMesh.transform.Rotate(zero, Space.Self);
		}
		CheckForRelocationGeoPositionUpdate();
		base.OnFixedUpdate();
	}

	private void CheckForRelocationGeoPositionUpdate()
	{
		if (!_initializedLastUnrelocatedPos)
		{
			_lastUnrelocatedPos = base.transform.position;
			_initializedLastUnrelocatedPos = true;
		}
		bool num = (_lastUnrelocatedPos - base.transform.position).sqrMagnitude > 0.5f * Globals._rebaseToOriginSq;
		_lastUnrelocatedPos = base.transform.position;
		if (num)
		{
			CalculateGeoPosition();
		}
	}

	public override bool isSubmerged()
	{
		if (this == null)
		{
			return false;
		}
		return base.transform.position.y < 0f;
	}

	public void CheckAndSetLaserDesignatorOnTheLaunchPlatform(ObjectBase baseObject)
	{
		if (baseObject != _launchPlatform)
		{
			_laserDesignatorOnTheLaunchPlatform = false;
		}
		else
		{
			_laserDesignatorOnTheLaunchPlatform = true;
		}
	}

	public void initSeeker(AmmunitionParameters.GuidanceType guidanceType)
	{
		switch (guidanceType)
		{
		case AmmunitionParameters.GuidanceType.None:
			_seeker = new SeekerBase();
			break;
		case AmmunitionParameters.GuidanceType.IRHoming:
			_seeker = new IRSeeker();
			break;
		case AmmunitionParameters.GuidanceType.SemiActiveRadarHoming:
			_seeker = new SemiActiveRadarSeeker();
			break;
		case AmmunitionParameters.GuidanceType.ActiveRadarHoming:
			_seeker = new ActiveRadarSeeker();
			break;
		case AmmunitionParameters.GuidanceType.AntiRadiationHoming:
			_seeker = new RadiationSeeker();
			break;
		case AmmunitionParameters.GuidanceType.LaserHoming:
			_seeker = new LaserSeeker();
			break;
		case AmmunitionParameters.GuidanceType.TVHoming:
			_seeker = new TVSeeker();
			break;
		case AmmunitionParameters.GuidanceType.ActiveSonarHoming:
			_seeker = new ActiveSonarSeeker();
			break;
		case AmmunitionParameters.GuidanceType.PassiveSonarHoming:
			_seeker = new PassiveSonarSeeker();
			break;
		case AmmunitionParameters.GuidanceType.WakeHoming:
			_seeker = new WakeSeeker();
			break;
		case AmmunitionParameters.GuidanceType.ActivePassiveSonarHoming:
			_seeker = new ActivePassiveSonarSeeker();
			break;
		default:
			_seeker = new SeekerBase();
			break;
		}
	}

	protected void calculateEstimatedInterceptTargetPositionAndVelocity()
	{
		EstimatedInterceptTargetGeoPosition = AimPointGeoPosition;
		_estimatedInterceptTargetPosition = AimPointGeoPosition.ToUnity();
		_estimatedInterceptTargetVelocityVector = default(Vector3);
		if (!(CurrentIntendedTargetObject != null))
		{
			return;
		}
		if (base._taskforce == null || CurrentIntendedTargetObject._taskforce == base._taskforce)
		{
			EstimatedInterceptTargetGeoPosition = CurrentIntendedTargetObject._geoPosition;
			_estimatedInterceptTargetPosition = CurrentIntendedTargetObject._geoPosition.ToUnity();
			_estimatedInterceptTargetVelocityVector = CurrentIntendedTargetObject.UnityVelocityVector;
			_estimatedInterceptTargetPosition2.FromUnity(_estimatedInterceptTargetPosition);
			return;
		}
		Vehicle vehicle = null;
		if (_launchPlatform != null)
		{
			bool flag = true;
			if (_launchPlatform.isSubmerged())
			{
				flag = false;
			}
			vehicle = ((!flag) ? _launchPlatform?.PlottingTable?.VehicleForObject(CurrentIntendedTargetObject) : _launchPlatform._taskforce?.PlottingTable?.VehicleForObject(CurrentIntendedTargetObject));
		}
		if (vehicle?.KnownUnityPosition is Vector3)
		{
			EstimatedInterceptTargetGeoPosition = vehicle.PositionEstimate.Value.Item1;
			_estimatedInterceptTargetPosition = vehicle.KnownUnityPosition.Value;
			_estimatedInterceptTargetVelocityVector = vehicle.UnityVelocityVector;
			_estimatedInterceptTargetPosition2.FromUnity(_estimatedInterceptTargetPosition);
		}
	}

	public void activateGuidingSystem()
	{
		if (_weaponSystem == null)
		{
			return;
		}
		_weaponSystem._sensorPreLaunchAligned = false;
		if (CurrentIntendedTargetObject != null)
		{
			bool flag = false;
			if (_ap._requiresGuidance)
			{
				flag = true;
			}
			if (_ap._type == Ammunition.Type.Projectile)
			{
				flag = true;
			}
			if (!flag)
			{
				return;
			}
			if (_weaponSystem._vwp._associatedSensors != null)
			{
				foreach (SensorSystem associatedSensor in _weaponSystem._vwp._associatedSensors)
				{
					if ((_ap._compatibleGuidanceSystems.Count > 0 && !_ap._compatibleGuidanceSystems.Contains(associatedSensor._systemName)) || !(associatedSensor.GetType() == typeof(SensorSystemRadar)))
					{
						continue;
					}
					SensorSystemRadar sensorSystemRadar = (SensorSystemRadar)associatedSensor;
					if (sensorSystemRadar.TryToAddTarget(CurrentIntendedTargetObject) && sensorSystemRadar.TryToAddWeapon(this))
					{
						_guidingRadarSystem = sensorSystemRadar;
						_guidingRadarSystem._isGuiding = true;
						_guidingRadarSystem._restPositionReached = false;
						_guidingRadarSystem._preLaunchTargetObject = null;
						if (_guidingRadarSystem.getMode() == SensorSystemRadar.Mode.Illuminate)
						{
							_guidingRadarSystem.setTargetObject(CurrentIntendedTargetObject);
						}
						else
						{
							_guidingRadarSystem.setTargetObject(this);
						}
						if (!_guidingRadarSystem._requiresAlignment && _launchPlatform != null)
						{
							_launchPlatform.DelayedEndAlignment(_alignmentUID, 0f, "activateGuidingSystem");
						}
						return;
					}
				}
				for (int num = _weaponSystem._vwp._associatedSensors.Count - 1; num >= 0; num--)
				{
					if (_weaponSystem._vwp._associatedSensors[num].GetType() == typeof(SensorSystemRadar))
					{
						SensorSystemRadar sensorSystemRadar2 = (SensorSystemRadar)_weaponSystem._vwp._associatedSensors[num];
						if (sensorSystemRadar2.TryToAddWeapon(this))
						{
							if (sensorSystemRadar2._associatedTargets.Count > 0)
							{
								CurrentIntendedTargetObject = sensorSystemRadar2._associatedTargets[0];
							}
							_guidingRadarSystem = sensorSystemRadar2;
							_guidingRadarSystem._isGuiding = true;
							_guidingRadarSystem._restPositionReached = false;
							_guidingRadarSystem._preLaunchTargetObject = null;
							if (_guidingRadarSystem.getMode() == SensorSystemRadar.Mode.Illuminate)
							{
								_guidingRadarSystem.setTargetObject(CurrentIntendedTargetObject);
							}
							else
							{
								_guidingRadarSystem.setTargetObject(this);
							}
							if (!_guidingRadarSystem._requiresAlignment && _launchPlatform != null)
							{
								_launchPlatform.DelayedEndAlignment(_alignmentUID, 0f, "activateGuidingSystem");
							}
							return;
						}
					}
				}
				ConnectionLost.Value = true;
				ConnectionLostForever.Value = true;
			}
			else if (!_noAssociatedSensorSystemWarningLogged)
			{
				Log.DebugWarning("Missile '" + base.Name.Value + " (" + getObjectIniName() + ")' has no associated sensor system on vessel '" + _launchPlatform.getUIDAndName() + " (" + _launchPlatform.getObjectIniName() + ")'", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\WeaponBase.cs", 779, "activateGuidingSystem");
				_noAssociatedSensorSystemWarningLogged = true;
			}
		}
		else
		{
			if (_ap._midCourseCorrection != AmmunitionParameters.MidCourseCorrection.RadioCommand)
			{
				return;
			}
			if (_weaponSystem._vwp._associatedSensors != null)
			{
				foreach (SensorSystem associatedSensor2 in _weaponSystem._vwp._associatedSensors)
				{
					if ((_ap._compatibleGuidanceSystems.Count > 0 && !_ap._compatibleGuidanceSystems.Contains(associatedSensor2._systemName)) || !(associatedSensor2.GetType() == typeof(SensorSystemRadar)))
					{
						continue;
					}
					SensorSystemRadar sensorSystemRadar3 = (SensorSystemRadar)associatedSensor2;
					if (sensorSystemRadar3.HasFreeTargetChannels() && sensorSystemRadar3.TryToAddWeapon(this))
					{
						_guidingRadarSystem = sensorSystemRadar3;
						_guidingRadarSystem._isGuiding = true;
						_guidingRadarSystem._restPositionReached = false;
						_guidingRadarSystem._preLaunchTargetObject = null;
						_guidingRadarSystem.setTargetObject(this);
						if (!_guidingRadarSystem._requiresAlignment && _launchPlatform != null)
						{
							_launchPlatform.DelayedEndAlignment(_alignmentUID, 0f, "activateGuidingSystem");
						}
						return;
					}
				}
				for (int num2 = _weaponSystem._vwp._associatedSensors.Count - 1; num2 >= 0; num2--)
				{
					if (_weaponSystem._vwp._associatedSensors[num2].GetType() == typeof(SensorSystemRadar))
					{
						SensorSystemRadar sensorSystemRadar4 = (SensorSystemRadar)_weaponSystem._vwp._associatedSensors[num2];
						if (sensorSystemRadar4.TryToAddWeapon(this))
						{
							CurrentIntendedTargetObject = sensorSystemRadar4._associatedTargets[0];
							_guidingRadarSystem = sensorSystemRadar4;
							_guidingRadarSystem._isGuiding = true;
							_guidingRadarSystem._restPositionReached = false;
							_guidingRadarSystem._preLaunchTargetObject = null;
							if (_guidingRadarSystem.getMode() == SensorSystemRadar.Mode.Illuminate)
							{
								_guidingRadarSystem.setTargetObject(CurrentIntendedTargetObject);
							}
							else
							{
								_guidingRadarSystem.setTargetObject(this);
							}
							return;
						}
					}
				}
				ConnectionLost.Value = true;
				ConnectionLostForever.Value = true;
			}
			else if (!_noAssociatedSensorSystemWarningLogged)
			{
				Log.DebugWarning("Missile '" + base.Name.Value + " (" + getObjectIniName() + ")' has no associated sensor system on vessel '" + _launchPlatform.getName() + " (" + _launchPlatform.getObjectIniName() + ")'", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\WeaponBase.cs", 861, "activateGuidingSystem");
				_noAssociatedSensorSystemWarningLogged = true;
			}
		}
	}

	public void CheckSeekerSwap()
	{
		ObjectBase currentTarget = CurrentTarget;
		if (currentTarget == null)
		{
			return;
		}
		Ammunition.SeekerMode seekerMode = Ammunition.SeekerMode.Default;
		AmmunitionParameters.GuidanceType guidanceType = _ap._guidanceType;
		if (currentTarget is LandUnit)
		{
			seekerMode = Ammunition.SeekerMode.LandUnit;
			guidanceType = _ap._landAttackGuidanceType;
		}
		else if (currentTarget is Vessel)
		{
			seekerMode = Ammunition.SeekerMode.Vessel;
			guidanceType = _ap._vesselAttackGuidanceType;
		}
		if (_seeker._mode == seekerMode)
		{
			return;
		}
		if (_seeker._guidanceType == guidanceType)
		{
			_seeker._mode = seekerMode;
			return;
		}
		bool value = _seeker.IsOn.Value;
		if (_guidingRadarSystem != null)
		{
			_guidingRadarSystem.FreeChannels(CurrentIntendedTargetObject, this);
		}
		CurrentIntendedTargetObject = CurrentTarget;
		_seeker.destroy();
		initSeeker(guidanceType);
		_seeker._mode = seekerMode;
		_requiresExternalGuidance = false;
		_seeker.init(_launchPlatform, this, _ap);
		if (_ap._requiresGuidance)
		{
			_requiresExternalGuidance = true;
			activateGuidingSystem();
			if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming && _guidingRadarSystem != null)
			{
				_seeker.setGuidingSystem(_guidingRadarSystem);
			}
			else if (_ap._guidanceType == AmmunitionParameters.GuidanceType.LaserHoming)
			{
				CurrentIntendedTargetObject = currentTarget;
				_seeker.setCurrentTarget(currentTarget);
				_seeker.setGuidingSystem();
			}
		}
		if (value)
		{
			_seeker.setActive(active: true);
		}
	}

	public bool NoSeekerActive()
	{
		if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.None)
		{
			return true;
		}
		return !_seeker.isActive();
	}

	public GeoPosition GetMapTargetPosition(Taskforce taskforce)
	{
		GeoPosition result = base.Position.Value;
		if (this is Missile missile && missile.GroupWingman.Value)
		{
			return result;
		}
		ObjectBase objectBase = null;
		objectBase = ((CurrentStrongestTargetEcho == null) ? CurrentIntendedTargetObject : CurrentStrongestTargetEcho._objectBase);
		if (objectBase != null && (objectBase.isUnit() || objectBase.isWeapon()))
		{
			result = ((objectBase._type != ObjectType.Chaff) ? objectBase.GetMapPositionForTaskforce(Globals._playerTaskforce, base.Position.Value, useDefaultGP: true) : base.CommandPosition.Value);
		}
		else if (base.CommandPosition.HasValue)
		{
			result = base.CommandPosition.Value;
		}
		return result;
	}

	public bool TargetedAtUnitWhichCanReportDetection()
	{
		if (CurrentStrongestTargetEcho?._objectBase == null)
		{
			return false;
		}
		if (!CurrentStrongestTargetEcho._objectBase.isUnit())
		{
			return false;
		}
		if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.ActiveRadarHoming)
		{
			return true;
		}
		if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.ActiveSonarHoming || _seeker._guidanceType == AmmunitionParameters.GuidanceType.ActivePassiveSonarHoming)
		{
			return true;
		}
		return false;
	}

	public bool RetargetWeapon(ObjectBase targetObject)
	{
		if (targetObject == this)
		{
			return false;
		}
		if (!CanRetargetTo(targetObject))
		{
			return false;
		}
		if (this is Missile missile)
		{
			missile.SetNewTarget(targetObject);
			return true;
		}
		if (this is Torpedo { _onWire: not false } torpedo && (targetObject._type == ObjectType.Vessel || targetObject._type == ObjectType.Submarine))
		{
			if (targetObject == _launchPlatform)
			{
				return false;
			}
			torpedo.CurrentIntendedTargetObject = targetObject;
			return true;
		}
		return false;
	}

	public void RetargetWeaponToGeoPosition(GeoPosition targetGeoPosition)
	{
		if (!_ap._isRetargetable)
		{
			return;
		}
		bool flag = false;
		if (this is Missile missile)
		{
			if (missile._onWire)
			{
				flag = true;
			}
			else if (missile._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand && !missile.ConnectionLost.Value)
			{
				flag = true;
			}
			if (flag)
			{
				missile.CreateWaypoints();
			}
		}
		else if (this is Torpedo { _onWire: not false })
		{
			flag = true;
		}
		if (!flag)
		{
			return;
		}
		if (CurrentIntendedTargetObject != null)
		{
			if (_guidingRadarSystem != null)
			{
				_guidingRadarSystem.DropTarget(CurrentIntendedTargetObject, this);
			}
			CurrentIntendedTargetObject = null;
		}
		targetGeoPosition._height = AimPointGeoPosition._height;
		AimPointGeoPosition = targetGeoPosition;
		InitialTargetGeoPosition = targetGeoPosition;
	}

	public bool CanRetargetTo(ObjectBase possibleTarget)
	{
		if (possibleTarget == null)
		{
			return false;
		}
		if (possibleTarget == this)
		{
			return false;
		}
		if (!_ap._isRetargetable)
		{
			return false;
		}
		bool flag = false;
		if (this is Missile missile)
		{
			if (missile._onWire)
			{
				return true;
			}
			if (missile._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
			{
				if (ConnectionLost.Value || ConnectionLostForever.Value)
				{
					return false;
				}
				if (possibleTarget.isSubmerged() && (!_ap._hasSubmunition || _ap._submunitionType != Ammunition.Type.Torpedo))
				{
					return false;
				}
				if (_guidingRadarSystem == null)
				{
					return false;
				}
				flag = true;
			}
		}
		else if (this is Torpedo { _onWire: not false } && (possibleTarget._type == ObjectType.Vessel || possibleTarget._type == ObjectType.Submarine))
		{
			flag = true;
		}
		if (flag)
		{
			return DoesAmmoMatchTarget(possibleTarget);
		}
		return false;
	}

	public bool CanRetarget()
	{
		if (this is Missile missile && missile._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
		{
			if (ConnectionLost.Value || ConnectionLostForever.Value)
			{
				return false;
			}
			if (_guidingRadarSystem == null)
			{
				return false;
			}
			return true;
		}
		if (this is Torpedo { _onWire: not false })
		{
			return true;
		}
		return false;
	}

	public bool setHeight(float height, float maxVerticalAngle, float forwardDistToHeight = -1f, float maxTurnRateOverride = -1f, bool terminalDive = false, float heightOverTerrain = -1f)
	{
		maxVerticalAngle = Mathf.Abs(maxVerticalAngle);
		float num = _ap._maxTurnRateDegrees;
		if (this is Missile)
		{
			if (_flightStage != FlightStage.SelfDestruct && !_isTargetAirborne)
			{
				if (_ap._terrainFollowFlight)
				{
					height = TerrainUtils.GetSafeAltitudeOverSurface(this, height, _ap._currentMinFlightAltUnity, cantStayAtAltitude: false, isWeapon: true);
				}
				else if (!terminalDive)
				{
					height = TerrainUtils.GetSafeAltitudeOverSea(this, height, _ap._terminalAltUnity);
				}
			}
			if (_flightStage == FlightStage.TerminalApproach)
			{
				num = _ap._terminalVerticalTurnRate;
			}
		}
		else if (this is Torpedo)
		{
			float heightAt = TerrainUtils.GetHeightAt(base._geoPosition);
			if (height < heightAt)
			{
				height = heightAt;
			}
		}
		float num2 = new Vector2(_velocityVecInUnity.x, _velocityVecInUnity.z).magnitude;
		if (num2 < 0.01f)
		{
			num2 = 0.01f;
		}
		float num3 = ((maxTurnRateOverride > 0f) ? maxTurnRateOverride : num);
		float num4 = _velocityInUnity / Mathf.Max(num3, 1f) * 25f;
		if (forwardDistToHeight < 0f)
		{
			forwardDistToHeight = num4;
		}
		float num5 = Utils.WrapAngle(base.transform.localEulerAngles.x);
		float num6 = forwardDistToHeight / num2;
		float num7 = height - (base.transform.position + _velocityVecInUnity * num6).y;
		float num8 = (0f - Mathf.Atan(num7 / forwardDistToHeight)) * 57.29578f + num5;
		if (num8 > maxVerticalAngle)
		{
			num8 = maxVerticalAngle;
		}
		if (num8 < 0f - maxVerticalAngle)
		{
			num8 = 0f - maxVerticalAngle;
		}
		float num9 = num5;
		float value = (num8 - num5) * 5f;
		value = Mathf.Clamp(value, 0f - num3, num3);
		float num10 = num * 8f;
		_currentPitchRate = Mathf.MoveTowards(_currentPitchRate, value, num10 * GameTime.fixedDeltaTime);
		num9 += _currentPitchRate * GameTime.fixedDeltaTime;
		base.transform.localEulerAngles = new Vector3(num9, base.transform.localEulerAngles.y, base.transform.localEulerAngles.z);
		if (Mathf.Abs(num7) < 0.01f)
		{
			return Mathf.Abs(num5) < 5f;
		}
		return false;
	}

	public float getDesiredPitchChange(float height, float maxVerticalAngle, float forwardDistToHeight = -1f, float maxTurnRateOverride = -1f)
	{
		maxVerticalAngle = Mathf.Abs(maxVerticalAngle);
		float num = _ap._maxTurnRateDegrees;
		if (_flightStage == FlightStage.TerminalApproach && this is Missile)
		{
			num = _ap._terminalVerticalTurnRate;
		}
		float num2 = new Vector2(_velocityVecInUnity.x, _velocityVecInUnity.z).magnitude;
		if (num2 < 1f)
		{
			num2 = 1f;
		}
		float num3 = ((maxTurnRateOverride > 0f) ? maxTurnRateOverride : num);
		float num4 = _velocityInUnity / Mathf.Max(num3, 1f) * 25f;
		if (forwardDistToHeight < 0f)
		{
			forwardDistToHeight = num4;
		}
		float num5 = Utils.WrapAngle(base.transform.localEulerAngles.x);
		float num6 = forwardDistToHeight / num2;
		float num7 = (0f - Mathf.Atan((height - (base.transform.position + _velocityVecInUnity * num6).y) / forwardDistToHeight)) * 57.29578f + num5;
		if (num7 > maxVerticalAngle)
		{
			num7 = maxVerticalAngle;
		}
		if (num7 < 0f - maxVerticalAngle)
		{
			num7 = 0f - maxVerticalAngle;
		}
		return Mathf.Clamp((num7 - num5) * 5f, 0f - num3, num3);
	}

	protected void maintainHeight(float heightToMaintain, bool checkAltitude = true)
	{
		if (_type == ObjectType.Missile && _flightStage != FlightStage.SelfDestruct && checkAltitude && !_isTargetAirborne)
		{
			heightToMaintain = ((!_ap._terrainFollowFlight) ? TerrainUtils.GetSafeAltitudeOverSea(this, heightToMaintain, 0f) : TerrainUtils.GetSafeAltitudeOverSurface(this, heightToMaintain, _ap._currentMinFlightAltUnity, cantStayAtAltitude: false, isWeapon: true));
		}
		_maintainingHeight = true;
		Vector3 localEulerAngles = base.transform.localEulerAngles;
		localEulerAngles.x = 0f - _ap._seaSkimmingNoseUp;
		localEulerAngles.z = 0f;
		Quaternion localRotation = Quaternion.Slerp(base.transform.localRotation, Quaternion.Euler(localEulerAngles), 3f * GameTime.fixedDeltaTime);
		base.transform.localRotation = localRotation;
		float y = Mathf.SmoothStep(base.transform.position.y, heightToMaintain, 3f * GameTime.fixedDeltaTime);
		base.transform.position = new Vector3(base.transform.position.x, y, base.transform.position.z);
		_velocityVecInUnity = _velocityInUnity * new Vector3(base.transform.forward.x, 0f, base.transform.forward.z).normalized;
	}

	public float setCourseTowardsTarget(Vector3 targetPosition, bool maintainLocalX, bool isTorpedo = false)
	{
		if (CurrentStrongestTargetEcho?._objectBase is Vessel)
		{
			if (isTorpedo)
			{
				targetPosition.y = Utils.heightInFeetToUnity(-5f);
			}
			else
			{
				targetPosition.y = Utils.heightInFeetToUnity(5f);
			}
		}
		return setCourseTowardsPosition(targetPosition, maintainLocalX);
	}

	public void MoveOnCourse(Course[] courses, bool maintainLocalX, float pitchRate)
	{
		_ = base.transform.rotation;
	}

	public float setCourseTowardsPosition(Vector3 targetPosition, bool maintainLocalX, bool toBearingState = false, float gLimit = 200f)
	{
		float num = _ap._maxTurnRateDegrees;
		float num2 = 0.514444f * getVelocityInKnots();
		float num3 = num2 * (360f / num) / (MathF.PI * 2f);
		float num4 = MathF.Pow(num2, 2f) / (num3 * 9.8f);
		if (num4 > gLimit)
		{
			num *= gLimit / num4;
		}
		if (toBearingState && _ap._toBearingTurnRateDegrees > 0f && _ap._toBearingTurnRateDegrees > num)
		{
			num = _ap._toBearingTurnRateDegrees;
		}
		Vector3 vector = targetPosition - base.transform.position;
		Quaternion b = Quaternion.LookRotation(vector, base.transform.up);
		Vector3 eulerAngles = b.eulerAngles;
		float result = Quaternion.Angle(base.transform.rotation, b);
		float num5 = 0f;
		float num6 = 0f;
		float num7 = 2500f;
		float sqrMagnitude = (base.transform.position - targetPosition).sqrMagnitude;
		float num8 = 1f - (num7 - sqrMagnitude) / num7;
		if (num8 < 0f)
		{
			num8 = 0f;
		}
		else if (num8 > 1f)
		{
			num8 = 1f;
		}
		if (!toBearingState)
		{
			if (_ap._wobblingStrength > 0f)
			{
				num5 = _ap._wobblingStrength * num8 * Mathf.Sin(_ap._wobblingSpeed * GameTime.time);
			}
			if (_ap._verticalWobblingStrength > 0f)
			{
				float num9 = _ap._verticalWobblingStrength * num8;
				if (num9 > 0.1f)
				{
					num6 = num9 * Mathf.Sin(_ap._verticalWobblingSpeed * GameTime.time);
				}
			}
		}
		float x = eulerAngles.x;
		float num10 = num;
		if (_flightStage == FlightStage.TerminalApproach && _ap._terminalVerticalTurnRate > 0f)
		{
			num10 = _ap._terminalVerticalTurnRate;
		}
		if (maintainLocalX)
		{
			x = base.transform.localEulerAngles.x;
			result = ((!Mathf.Approximately(vector.sqrMagnitude, 0f)) ? Mathf.Abs(Utils.calculateYawToTarget(vector, base.transform.forward)) : 0f);
		}
		float z = 0f;
		if (_ap._supportsBanking)
		{
			z = base.transform.localEulerAngles.z;
		}
		if (Mathf.Abs(num10 - num) > 1f)
		{
			Quaternion to = Quaternion.Euler(base.transform.eulerAngles.x, base.transform.eulerAngles.y, z);
			base.transform.rotation = Quaternion.RotateTowards(base.transform.rotation, to, num * GameTime.fixedDeltaTime);
			Quaternion to2 = Quaternion.Euler(base.transform.eulerAngles.x, eulerAngles.y + num5, base.transform.eulerAngles.z);
			base.transform.rotation = Quaternion.RotateTowards(base.transform.rotation, to2, num * GameTime.fixedDeltaTime);
			Quaternion to3 = Quaternion.Euler(x + num6, base.transform.eulerAngles.y, base.transform.eulerAngles.z);
			base.transform.rotation = Quaternion.RotateTowards(base.transform.rotation, to3, num10 * GameTime.fixedDeltaTime);
		}
		else
		{
			Quaternion to4 = Quaternion.Euler(x, eulerAngles.y, z);
			base.transform.rotation = Quaternion.RotateTowards(base.transform.rotation, to4, num * GameTime.fixedDeltaTime);
			_ = _type;
			_ = 7;
		}
		if (_ap._supportsBanking)
		{
			performToTargetRoll(targetPosition);
		}
		return result;
	}

	protected void performToTargetRoll(Vector3 targetPosition)
	{
		float num = Utils.calculateYawToTarget(base.transform.forward, base.transform.position, targetPosition) * 10f;
		if (num > 60f)
		{
			num = 60f;
		}
		if (num < -60f)
		{
			num = -60f;
		}
		Quaternion to = Quaternion.Euler(base.transform.localEulerAngles.x, base.transform.localEulerAngles.y, num);
		base.transform.rotation = Quaternion.RotateTowards(base.transform.rotation, to, 60f * GameTime.fixedDeltaTime);
	}

	public override ISelectableViewModel CreateViewModel()
	{
		return new WeaponBaseViewModel(this);
	}

	public override IDisposable CreateContactViewModel(Vehicle contact, MapUnitViewModel baseVm)
	{
		return new WeaponContactViewModel(this, contact, baseVm);
	}

	public virtual IWeaponControlViewModel CreateControlViewModel()
	{
		return null;
	}

	public virtual float CalculateHitChance(ObjectBase target)
	{
		return 1f;
	}

	public virtual void Container_Launch(ObjectBase targetObject, Vector3 bearingOnlyTargetPosition, Vector3 offsetVector, WeaponSystem weaponSystemUsed)
	{
		DM.I("[" + getUIDAndName() + "]: Container launch");
		_isSubmunition = false;
		_parentWeapon = null;
		if ((bool)_debugTrail)
		{
			_debugTrail.Clear();
		}
	}

	public void CommonLaunchSettings(ObjectBase targetObject, Vector3 targetPosition, WeaponSystem weaponSystem, bool isSubmunition = false)
	{
		string text = (targetObject ? targetObject.getUIDAndName() : "position");
		string text2 = ((weaponSystem != null) ? (weaponSystem._baseObject.getUIDAndName() + " ") : "Nibiru");
		if (targetObject == null && targetPosition.y > 0f)
		{
			targetPosition.y = MathF.Max(0f, TerrainUtils.GetHeightAt(new GeoPosition(targetPosition)));
		}
		DM.I($"[{getUIDAndName()}]: launched at {text}, target position = {targetPosition.x:0.##},{targetPosition.y:0.##}, {targetPosition.z:0.##}, from {text2}");
		if (_ap._subType != Ammunition.Type.Fueltank && _ap._subType != Ammunition.Type.Paratrooper && _ap._type != Ammunition.Type.Noisemaker && _ap._type != Ammunition.Type.Chaff)
		{
			Vector3 zero = Vector3.zero;
			GeoPosition geoPosition = new GeoPosition(targetPosition);
			float y = 220.47266f * (float)geoPosition._height;
			geoPosition -= Singleton<SceneCreator>.Instance.GeoCenterPosition;
			zero.x = (float)(geoPosition._longitude * 111120.000000012 / 67.20006561279297);
			zero.z = (float)(geoPosition._latitude * 111120.000000012 / 67.20006561279297);
			zero = zero / 0.0148809375f / 1.852f / 1000f;
			zero.y = y;
			DM.Log($"[{getUIDAndName()}]: launched at {text}, target position (rel nm): {zero.x:F2}, {zero.y:F0}, {zero.z:F2}, from {text2}", DM.EventLogFilters.EngageTask);
		}
		_isLaunched = true;
		_hasControl = true;
		base.IsControllable = true;
		_canUpdate = true;
		_deviationMagnitudeWithJam = 0f;
		_traveledDistance = 0f;
		_launchTime = GameTime.time;
		_dt = 0f;
		_prevflightState = FlightStage.None;
		ConnectionLost.Value = false;
		ConnectionLostForever.Value = false;
		_fuseActive = false;
		_lastDistanceToCollisionSq = 1000f;
		_smartFuseActive = false;
		_smartFuseTargetTransform = null;
		base.transform.parent = null;
		_lastCenterTile = Globals._currentCenterTile;
		base._geoPosition = Utils.worldPositionFromUnityToLongLat(base.transform.position, Globals._currentCenterTile);
		_launchPosition = base._geoPosition;
		_closestDistToTargetSq = float.MaxValue;
		_initialSetTargetObject = targetObject;
		CurrentIntendedTargetObject = targetObject;
		_lastTargetObject = targetObject;
		_initialSetTargetPosition = targetPosition;
		AimPointGeoPosition = ((targetObject != null) ? targetObject._geoPosition : Utils.worldPositionFromUnityToLongLat(targetPosition, Globals._currentCenterTile));
		if (targetObject != null)
		{
			_ap._seekerFOV = _ap._narrowSeekerFOV;
		}
		else
		{
			_ap._seekerFOV = _ap._wideSeekerFOV;
		}
		if (targetObject != null && !(this is Noisemaker) && _type != ObjectType.Chaff)
		{
			targetObject._incomingWeapons.Add(this);
		}
		_ignoreCollisions = false;
		if (targetObject != null && targetObject._type == ObjectType.LandUnit && _seeker._guidanceType != AmmunitionParameters.GuidanceType.AntiRadiationHoming)
		{
			if (_type == ObjectType.Bomb || _ap._maxLaunchRangeInMiles < 1.5f)
			{
				_currentTargetPoint = weaponSystem._currentTargetPoint;
			}
			else
			{
				bool excludeDestroyedSystems = false;
				Vehicle mapVehicle = targetObject.GetMapVehicle(base._taskforce);
				if (mapVehicle != null && mapVehicle.IsObservedByVisual)
				{
					excludeDestroyedSystems = true;
				}
				_currentTargetPoint = Utils.AssignLandUnitTargetPoint(targetObject, excludeDestroyedSystems, _launchPlatform);
				_currentTargetPointDebug = _currentTargetPoint;
			}
			AimPointGeoPosition = Utils.worldPositionFromUnityToLongLat(targetObject.transform.TransformPoint(_currentTargetPoint._position), Globals._currentCenterTile);
		}
		InitialTargetGeoPosition = AimPointGeoPosition;
		base.CommandPosition = AimPointGeoPosition;
		_weaponSystem = weaponSystem;
		if (_weaponSystem != null)
		{
			_weaponSystem._launchedWeapons.Add(this);
			_launchPlatform = weaponSystem._baseObject;
			_ap._damageMultiplier = _weaponSystem._vwp._damageMultiplier;
		}
		if (_isSubmunition)
		{
			_launchPlatform = _parentWeapon._launchPlatform;
		}
		if (base.transform.position.y < 0f)
		{
			_checkForWaterExit = true;
		}
		if (_launchPlatform != null && _launchPlatform._type == ObjectType.Submarine && !((Submarine)_launchPlatform).isSubmerged())
		{
			_checkForWaterExit = false;
		}
		if (!_isSubmunition && _launchPlatform != null)
		{
			_velocityInKnots = _launchPlatform.getVelocityInKnots();
			if (_launchPlatform._obp != null && _launchPlatform._obp is AircraftParameters aircraftParameters)
			{
				aircraftParameters._loadoutMass -= _ap._massInKg;
			}
		}
		_velocityInUnity = _velocityInKnots * 0.0076554087f;
		Vector3 direction = Vector3.forward * _velocityInUnity;
		Vector3 direction2 = base.transform.TransformDirection(direction);
		_velocityVecInUnity = base.transform.TransformDirection(direction2);
		_dropSpeed = 0f;
		_launcherSpeedVector = base.transform.InverseTransformDirection(direction2);
		_isTargetAirborne = false;
		if (targetObject != null && (_ap._targetType == Ammunition.Target.AAW || _ap._secondaryTargetType == Ammunition.Target.AAW))
		{
			if (targetObject._type == ObjectType.Missile)
			{
				_isTargetAirborne = true;
			}
			else if (targetObject._type == ObjectType.Bomb)
			{
				_isTargetAirborne = true;
			}
			else if (targetObject._type == ObjectType.Aircraft)
			{
				_isTargetAirborne = true;
			}
			else if (targetObject._type == ObjectType.Helicopter)
			{
				_isTargetAirborne = true;
			}
		}
		_proximityFuse = false;
		switch (_type)
		{
		case ObjectType.Missile:
			_isInterceptable = true;
			((Missile)this)._poppingUp = false;
			((Missile)this)._hasPoppedUp = false;
			if (_isTargetAirborne)
			{
				if (_ap._fuzeType == AmmunitionParameters.FuzeType.Proximity)
				{
					_proximityFuse = true;
				}
				_isInterceptable = false;
			}
			if (targetObject != null)
			{
				((Missile)this)._currentIntendedTargetData = (taskforce: targetObject._taskforce, type: targetObject._type);
			}
			else
			{
				((Missile)this)._currentIntendedTargetData = (taskforce: null, type: ObjectType.Other);
			}
			break;
		case ObjectType.Torpedo:
			_isInterceptable = true;
			if (_ap._fuzeType == AmmunitionParameters.FuzeType.Proximity)
			{
				_proximityFuse = true;
			}
			break;
		case ObjectType.Bomb:
			_isInterceptable = _ap._isInterceptable;
			break;
		}
		if (_isSubmunition)
		{
			base._taskforce = _parentWeapon._taskforce;
		}
		else if (_launchPlatform != null)
		{
			base._taskforce = _launchPlatform._taskforce;
		}
		if (base.PlottingTable != null)
		{
			base.PlottingTable.ReportingSide = base._taskforce;
		}
		else
		{
			base.PlottingTable = new PlottingTable(base._taskforce);
			Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager.AddComponentData(base.Entity, new ObjectPlottingTable
			{
				PlottingTable = base.PlottingTable.Entity
			});
		}
		Singleton<ObjectsManager>.Instance.AddObjectToItsTaskforce(this);
		if (!_proximityRadius)
		{
			_proximityRadius = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			_proximityRadius.name = "ProximityFuseRadius";
			_proximityRadius.GetComponent<MeshRenderer>().material.color = Color.blue;
			_proximityRadius.GetComponent<SphereCollider>().enabled = false;
			_proximityRadius.SetActive(value: false);
		}
		if (_type == ObjectType.Noisemaker)
		{
			_proximityRadius.transform.SetParent((this as Noisemaker).getNoisemakerInstance()._object.transform);
		}
		else
		{
			_proximityRadius.transform.SetParent(_weaponInstance._object.transform);
		}
		_proximityRadius.transform.localPosition = Vector3.zero;
		_proximityRadius.transform.localScale = new Vector3(_ap._fuzeProximityDistance, _ap._fuzeProximityDistance, _ap._fuzeProximityDistance) * 2f;
		if (_debugTrail == null)
		{
			_debugTrail = base.transform.gameObject.AddComponent<TrailRenderer>();
			_debugTrail.time = 600f;
			_debugTrail.startWidth = 0.01f;
			_debugTrail.endWidth = 0.01f;
			Globals._trailRendererList.Add(_debugTrail);
		}
		float num = 10f;
		Material material = _debugTrail.material;
		bool flag = base._taskforce.Side == Taskforce.TfType.Player;
		if (_type == ObjectType.Torpedo)
		{
			if (flag)
			{
				material.color = Color.cyan * num;
			}
			else
			{
				material.color = Color.yellow * num;
			}
		}
		else if (_type == ObjectType.Missile)
		{
			if (_isTargetAirborne)
			{
				if (flag)
				{
					material.color = Color.green * num;
				}
				else
				{
					material.color = Color.magenta * num;
				}
			}
			else if (flag)
			{
				material.color = Color.blue * num;
			}
			else
			{
				material.color = Color.red * num;
			}
		}
		else if (_type == ObjectType.ASROC)
		{
			if (flag)
			{
				material.color = Color.blue * num;
			}
			else
			{
				material.color = Color.red * num;
			}
		}
		if (_type == ObjectType.RBU || _type == ObjectType.Bomb)
		{
			if (flag)
			{
				material.color = Color.black * num;
			}
			else
			{
				material.color = Color.black * num;
			}
		}
		_blastzoneCreated = false;
		if (weaponSystem != null)
		{
			_alignmentUID = weaponSystem._alignmentUID;
		}
		else
		{
			_alignmentUID = -1;
		}
		if (!(this is RBU) && base._taskforce != null && targetObject != null)
		{
			float num2 = GameTime.time + (base.transform.position - targetPosition).magnitude / (_ap._maxVelocityInKnots * 0.0076554087f);
			num2 += 20f;
			Dictionary<ObjectBase, (float, float)> dictionary = ((!(this is Torpedo) && !(this is ASROC)) ? base._taskforce._taskforceMissileTargets : base._taskforce._taskforceTorpedoTargets);
			if (!dictionary.ContainsKey(targetObject))
			{
				dictionary.Add(targetObject, (GameTime.time, num2));
			}
			else
			{
				(float, float) value = dictionary[targetObject];
				if (value.Item2 < GameTime.time)
				{
					value.Item1 = GameTime.time;
				}
				value.Item2 = num2;
				dictionary[targetObject] = value;
			}
		}
		if (!_isSubmunition && _launchPlatform != null)
		{
			if (_type != ObjectType.Chaff && _type != ObjectType.Noisemaker && _type != ObjectType.Other)
			{
				bool flag2 = false;
				if (Globals._autoSwitchToLaunchedWeapon == Globals.CameraSwitchTo.All)
				{
					flag2 = true;
				}
				else if (Globals._autoSwitchToLaunchedWeapon == Globals.CameraSwitchTo.Player && _launchPlatform._taskforce.Side == Taskforce.TfType.Player)
				{
					flag2 = true;
				}
				else if (Globals._autoSwitchToLaunchedWeapon == Globals.CameraSwitchTo.Enemy && _launchPlatform._taskforce.Side != Taskforce.TfType.Player)
				{
					flag2 = true;
				}
				else if (Globals._autoSwitchToLaunchedWeapon == Globals.CameraSwitchTo.Local && _launchPlatform == Singleton<RenderPosition>.Instance.getSelectedObject())
				{
					flag2 = true;
				}
				if (flag2)
				{
					Singleton<RenderPosition>.Instance.switchToObject(this, performSmoothMove: false);
				}
			}
			if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.WireGuided)
			{
				if (weaponSystem != null && weaponSystem.HasFreeWire())
				{
					if (this is Torpedo)
					{
						ObjectBase launchPlatform = _launchPlatform;
						if (launchPlatform is Vessel || launchPlatform is Submarine)
						{
							_onWire = true;
						}
					}
					else
					{
						_onWire = true;
					}
				}
				if (!_onWire)
				{
					ConnectionLost.Value = true;
					ConnectionLostForever.Value = true;
				}
			}
		}
		if (_ap._isInstantExplosion)
		{
			DM.Log(FullIDData() + " is destroyed by suicide explosion", DM.EventLogFilters.Destruction);
			Destruction(base.transform.position, base.transform.rotation, null, createBlastzone: true);
			if (_launchPlatform?.Compartments != null)
			{
				_launchPlatform.Compartments.Sink(Compartments.SinkFocus.All, maxFlood: true);
			}
		}
		if (!isSubmunition && _sensorData != null && _sensorData._hasTransient)
		{
			GameObject gameObject = new GameObject();
			gameObject.SetActive(value: true);
			gameObject.AddComponent<LaunchTransient>().Init(gameObject, this);
		}
		LaunchVoiceover();
		if (targetObject != null)
		{
			Ammunition.SeekerMode seekerMode = Ammunition.SeekerMode.Default;
			AmmunitionParameters.GuidanceType guidanceType = _ap._guidanceType;
			if (targetObject is LandUnit)
			{
				seekerMode = Ammunition.SeekerMode.LandUnit;
				guidanceType = _ap._landAttackGuidanceType;
			}
			else if (targetObject is Vessel)
			{
				seekerMode = Ammunition.SeekerMode.Vessel;
				guidanceType = _ap._vesselAttackGuidanceType;
			}
			if (_seeker._mode != seekerMode)
			{
				if (_guidingRadarSystem != null)
				{
					_guidingRadarSystem.FreeChannels(targetObject, this);
				}
				_seeker.destroy();
				initSeeker(guidanceType);
				_seeker.init(_launchPlatform, this, _ap);
				_seeker._mode = seekerMode;
			}
		}
		if (!isSubmunition && _ap._subType != Ammunition.Type.Paratrooper)
		{
			if (!base._taskforce._weaponsSpent.ContainsKey(_ap._ammunitionFileName))
			{
				base._taskforce._weaponsSpent.Add(_ap._ammunitionFileName, new WeaponsSpent(_ap._type, _ap._subType, _ap._targetType));
			}
			else
			{
				base._taskforce._weaponsSpent[_ap._ammunitionFileName]._number++;
			}
		}
		DM.I("[" + getUIDAndName() + "]: CommonLaunchSettings complete");
	}

	private void LaunchVoiceover()
	{
		if (_isSubmunition || _launchPlatform == null || !_launchPlatform.IsPlayerObject)
		{
			return;
		}
		float num = Globals._weaponLaunchReportInterval;
		if (_launchPlatform.IsAirUnit)
		{
			num = 3f;
		}
		if (GameTime.time - Globals._weaponLaunchReportTime < num)
		{
			return;
		}
		Globals._weaponLaunchReportTime = GameTime.time;
		string text = "WeaponAway";
		if (_launchPlatform.IsAirUnit)
		{
			if (_ap._type == Ammunition.Type.Missile)
			{
				if (_isTargetAirborne)
				{
					if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
					{
						text = "AAM_SARH_Fired";
					}
					else if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.IRHoming)
					{
						text = "AAM_IR_Fired";
					}
					else if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.ActiveRadarHoming)
					{
						text = "AAM_ARS_Fired";
					}
					else if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.AntiRadiationHoming)
					{
						text = "ARMFiredAir";
					}
				}
			}
			else if (_ap._type == Ammunition.Type.Bomb)
			{
				text = ((_ap._subType == Ammunition.Type.Paratrooper) ? "ImGoingDown" : ((_ap._subType == Ammunition.Type.Sonobuoy) ? "BouyAway" : ((_ap._subType != Ammunition.Type.Fueltank) ? "BombsAway" : "")));
			}
			else if (_ap._type == Ammunition.Type.Torpedo)
			{
				text = "TorpedoFiredAir";
			}
			if (!string.IsNullOrEmpty(text))
			{
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage(text, _launchPlatform.getNation());
			}
			return;
		}
		if (_ap._type == Ammunition.Type.Missile)
		{
			text = "ASHMFired";
			if (_isTargetAirborne)
			{
				text = "SAMFired";
			}
			else if (CurrentIntendedTargetObject is LandUnit)
			{
				text = "ASUMFired";
			}
		}
		Singleton<VoiceMessage>.Instance.PlayVoiceSequence(text, _launchPlatform.getNation(), -1);
		if (CurrentIntendedTargetObject != null)
		{
			int mapContactID = CurrentIntendedTargetObject.GetMapContactID();
			if (mapContactID > 0)
			{
				Singleton<VoiceMessage>.Instance.AddVoiceToSequence("Track", _launchPlatform.getNation(), mapContactID);
			}
		}
	}

	public void DisableSubModels()
	{
		foreach (WeaponSubModel subModel in _weaponInstance._subModels)
		{
			if (subModel._type == WeaponSubModel.Type.AFTERBURNER)
			{
				subModel._subModel.gameObject.SetActive(value: false);
			}
			if (subModel._type == WeaponSubModel.Type.Antenna)
			{
				subModel._subModel.gameObject.SetActive(value: false);
			}
		}
	}

	protected override void CeaseOffensiveJamming()
	{
		foreach (SensorSystem sensor in _sensors)
		{
			if (sensor is SensorSystemECM sensorSystemECM)
			{
				sensorSystemECM._ecm.ceaseOffensiveJam();
			}
		}
		base.CeaseOffensiveJamming();
	}

	private void CleanupSensors()
	{
		foreach (SensorSystem sensor in _sensors)
		{
			sensor.SetDisabled();
			base._taskforce._sensorSystemsLink.RemoveSensor(sensor);
		}
		_sensors.Clear();
	}

	private void TerminateLaserGuidance()
	{
		_launchPlatform.DelayedEndAlignment(_alignmentUID, 0f, "TerminateLaserGuidance");
		if (_laserDesignatorOnTheLaunchPlatform && CurrentIntendedTargetObject != null)
		{
			_launchPlatform._obp?._laserSystems.Where((SensorSystemLaserDesignator ls) => ls._associatedTargets.Contains(CurrentIntendedTargetObject)).All((SensorSystemLaserDesignator x) => x._associatedTargets.Remove(CurrentIntendedTargetObject));
		}
	}

	private void OnDestructionGuidanceSystems()
	{
		if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.LaserHoming && _launchPlatform != null)
		{
			TerminateLaserGuidance();
		}
		if (_seeker != null)
		{
			_seeker.destroy();
		}
		foreach (ECM item in _defensiveECMsCheckedThisWeapon)
		{
			item?.removeWeaponFromLists(this);
		}
		stopJammingOfThisObject();
		CeaseOffensiveJamming();
		CleanupSensors();
	}

	private void CreateDesctructionEffects(Vector3 position, Quaternion rotation, ObjectBase targetObject)
	{
		Vector3 position2 = new Vector3(position.x, 0.0001f, position.z);
		Quaternion identity = Quaternion.identity;
		identity.eulerAngles = new Vector3(0f, rotation.eulerAngles.y, 0f);
		Quaternion rotation2 = identity;
		if (this is Missile)
		{
			rotation2 = rotation;
		}
		bool flag = false;
		Vector3 position3 = position;
		if (_type == ObjectType.Torpedo && ((position.y < 0.5f) & (position.y > 0f)))
		{
			position.y = -0.01f;
		}
		else if (_type == ObjectType.Bomb && ((position.y < 0.5f) & (position.y > 0f)))
		{
			position.y = -0.01f;
		}
		flag = _hitLand;
		if (targetObject != null)
		{
			Effect effectToPlay = _ap.getEffectToPlay(targetObject);
			Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(effectToPlay, position3, rotation2, triggerCameraShake: true, _ap._warheadPower / 100f));
			if (position.y < 0.0001f && !flag)
			{
				GameObject gameObject = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_ap._torpedoSurfaceSplashEffect, null, position2, identity));
				if (gameObject != null)
				{
					gameObject.name = "SURFACE_SPLASH";
				}
			}
			destroyObject(keepInFlightEffectAlive: false, score: false);
			_flightStage = FlightStage.Terminated;
			return;
		}
		if (flag)
		{
			Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_ap._hitGroundExplosionEffect, position3, rotation2, triggerCameraShake: true, _ap._warheadPower / 100f));
		}
		else if (position.y < 0.0001f)
		{
			GameObject gameObject2 = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_ap._torpedoSurfaceSplashEffect, null, position2, identity));
			if (gameObject2 != null)
			{
				gameObject2.name = "SURFACE_SPLASH";
			}
			if (_ap._type == Ammunition.Type.RBU || _ap._type == Ammunition.Type.Torpedo || _ap._type == Ammunition.Type.Bomb)
			{
				Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_ap._hitAirExplosionEffect, position3, rotation2, triggerCameraShake: true, _ap._warheadPower / 100f));
			}
		}
		else
		{
			Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_ap._hitAirExplosionEffect, null, position3, rotation2));
		}
		if (_flightStage == FlightStage.Debris)
		{
			Log.Debug("Debris of " + getUIDAndName() + " hit by second missile", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\WeaponBase.cs", 2003, "CreateDesctructionEffects");
			if (_weaponInstance._debrisCollider != null)
			{
				_weaponInstance._debrisCollider.enabled = false;
			}
			if (GetType() == typeof(Missile))
			{
				((Missile)this).ChangeDebrisToTerminatedState();
			}
			else
			{
				destroyObject(keepInFlightEffectAlive: false, score: false);
			}
			_flightStage = FlightStage.Terminated;
		}
		else if (_weaponInstance._debrisMesh != null && this is Missile)
		{
			int num = Globals._rnd.Next(0, 101);
			if (num <= _weaponInstance._debrisProbability)
			{
				Log.Debug($"Debris probability ({_weaponInstance._debrisProbability}) >= dice ({num}) => create debris!", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\WeaponBase.cs", 2020, "CreateDesctructionEffects");
				_weaponInstance._mainMesh.SetActive(value: false);
				_weaponInstance._debrisMesh.transform.SetParent(null);
				_weaponInstance._debrisMesh.SetActive(value: true);
				if (_weaponInstance._isDummyDebrisMesh)
				{
					_weaponInstance._debrisMesh.GetComponent<Renderer>().enabled = false;
				}
				if (_flightStage == FlightStage.MaintainSeaSkimming)
				{
					_lastVelocityVector.y = 0f;
				}
				_debrisStartTime = GameTime.time;
				_debrisMoveDirection = _lastVelocityVector;
				_debrisLifeTime = _weaponInstance._debrisLifeTime;
				_debrisSpeed = _debrisMoveDirection.magnitude;
				_debrisMoveDirection.Normalize();
				_debrisDownSpeed = 0f;
				_debrisVelocityDampening = _weaponInstance._debrisVelocityDampening;
				_debrisHeightLossAdd = _weaponInstance._debrisHeightLossAdd;
				_debrisFireEffect = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_ap._debrisTrailEffect, _weaponInstance._debrisMesh.transform, position3, rotation));
				if (_debrisFireEffect != null)
				{
					_debrisFireEffect.transform.localPosition = _ap._debrisTrailEffect._position;
				}
				_flightStage = FlightStage.Debris;
				if (GetType() == typeof(Missile))
				{
					((Missile)this).ChangeToDebrisState();
				}
			}
			else
			{
				Log.Debug($"Debris probability ({_weaponInstance._debrisProbability}) < dice ({num}) => don't create debris!", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\WeaponBase.cs", 2055, "CreateDesctructionEffects");
				destroyObject(keepInFlightEffectAlive: false, score: false);
				_flightStage = FlightStage.Terminated;
			}
		}
		else
		{
			destroyObject(keepInFlightEffectAlive: false, score: false);
			_flightStage = FlightStage.Terminated;
		}
	}

	public virtual void Destruction(Vector3 position, Quaternion rotation, ObjectBase targetObject, bool createBlastzone = false)
	{
		if (!base.IsDestroyed)
		{
			base.IsDestroyed = true;
			_proximityRadius.SetActive(value: false);
			DisableSubModels();
			if (createBlastzone)
			{
				new Blastzone(this, position).CreateExplosion();
			}
			DM.I($"Destruction: {getUIDAndName()} createBlastzone = {createBlastzone}");
			_externalDestructionNotified = true;
			OnDestructionGuidanceSystems();
			CreateDesctructionEffects(position, rotation, targetObject);
		}
	}

	public override void destroyObject(bool keepInFlightEffectAlive = false, bool score = false, TacView.TCEvent eventType = TacView.TCEvent.Destroyed)
	{
		if (_userRoot != null)
		{
			_userRoot.ClearTasks();
		}
		DM.I("destroyObject " + getUIDAndName());
		DM.Log("Weapon object destroyed: " + FullIDData(), DM.EventLogFilters.Destruction);
		if (Singleton<RenderPosition>.Instance.getSelectedObject() == this)
		{
			Singleton<RenderPosition>.Instance.deselectObjectAndDetachCamera();
		}
		if (base.Formation != null)
		{
			base.Formation.DetachUnit(this);
		}
		if (base._taskforce != null)
		{
			base._taskforce.RemoveTaskForceObject(this);
		}
		Singleton<ObjectsManager>.Instance.removeObject(this);
		foreach (SensorSystem sensor in _sensors)
		{
			sensor.Disable();
		}
		if (this is Missile missile)
		{
			if (missile._inMissileGroup)
			{
				missile._missileGroup.Remove(missile);
			}
			missile._wasInMissileGroup = false;
			missile._inMissileGroup = false;
			keepInFlightEffectAlive = true;
		}
		_isLaunched = false;
		if (_relocationEvent != null)
		{
			_relocationEvent.RemoveAllListeners();
			Singleton<RelocationHandler>.Instance.removeFromEventsList(_relocationEvent);
		}
		setDestroyedFlag(score, eventType);
		if (_boosterEffect != null)
		{
			if (_boosterEffect.transform.parent != null && _boosterEffect.transform.parent.tag != "PoolManager")
			{
				_boosterEffect.transform.SetParent(null);
			}
			AudioSource component = _boosterEffect.GetComponent<AudioSource>();
			if (component != null)
			{
				component.Stop();
			}
			_boosterEffect.GetComponent<ParticleSystem>().Stop();
		}
		if (_inFlightEffect != null)
		{
			if (_inFlightEffect.transform.parent != null && _inFlightEffect.transform.parent.tag != "PoolManager")
			{
				_inFlightEffect.transform.SetParent(null);
			}
			AudioSource component2 = _inFlightEffect.GetComponent<AudioSource>();
			if (component2 != null)
			{
				component2.Stop();
			}
			if (!keepInFlightEffectAlive)
			{
				_inFlightEffect.GetComponent<ParticleSystem>().Stop();
			}
			else
			{
				ParticleSystem[] componentsInChildren = _inFlightEffect.GetComponentsInChildren<ParticleSystem>();
				foreach (ParticleSystem particleSystem in componentsInChildren)
				{
					if (particleSystem.name.ToLower() == "fireemitter")
					{
						particleSystem.Stop();
					}
				}
			}
			ParticleAutoDestroy particleAutoDestroy = ParticleUtils.SetAutoDestroyAndCustomWorldSpace(_inFlightEffect);
			if (particleAutoDestroy != null)
			{
				particleAutoDestroy.setTimer(20f);
			}
		}
		if ((bool)_parachuteEffect)
		{
			Singleton<EffectsManager>.Instance.free(_parachuteEffect);
		}
		if (_weaponInstance._debrisMesh != null)
		{
			_weaponInstance._mainMesh.SetActive(value: true);
			_weaponInstance._debrisMesh.SetActive(value: false);
		}
		if (_weaponInstance._mainMeshForLaunch != null)
		{
			_weaponInstance._mainMesh.SetActive(value: false);
			_weaponInstance._mainMeshForLaunch.SetActive(value: true);
		}
		_launchObjectSwitched = false;
		foreach (WeaponSubModel subModel in _weaponInstance._subModels)
		{
			if (subModel._type == WeaponSubModel.Type.AFTERBURNER)
			{
				subModel._subModel.gameObject.SetActive(value: false);
			}
			if (subModel._type == WeaponSubModel.Type.Antenna)
			{
				subModel._subModel.gameObject.SetActive(value: false);
			}
		}
		if (_weaponInstance._boosterAudioSource != null)
		{
			_weaponInstance._boosterAudioSource.Stop();
		}
		if (_weaponInstance._sustainerAudioSource != null)
		{
			_weaponInstance._sustainerAudioSource.Stop();
		}
		if (_weaponInstance._farAudioSource != null)
		{
			_weaponInstance._farAudioSource.Stop();
		}
		if (_weaponInstance._collidersObject != null)
		{
			_weaponInstance._collidersObject.SetActive(value: false);
		}
		_externalDestructionNotified = false;
		if (_guidingRadarSystem != null)
		{
			_guidingRadarSystem.FreeChannels(CurrentIntendedTargetObject, this);
		}
		_weaponSystem?.WeaponDestroyed(this);
		if (!_hasSubmunition)
		{
			Singleton<PoolManager>.Instance.freeObject(_ap._ammunitionFileName, base.gameObject, this);
		}
		else
		{
			UnityEngine.Object.Destroy(base.transform.gameObject, 0f);
		}
		if (_isSubmunition && _parentWeapon != null)
		{
			Singleton<PoolManager>.Instance.freeObject(_parentWeapon._ap._ammunitionFileName, _parentWeapon.gameObject, _parentWeapon);
			base.gameObject.transform.SetParent(_parentWeapon.gameObject.transform);
			base.gameObject.transform.position = Vector3.zero;
			base.gameObject.transform.rotation = Quaternion.identity;
		}
	}

	public void onRelocate(Vector3 deltaPos)
	{
		_initialSetTargetPosition += deltaPos;
		_estimatedInterceptTargetPosition += deltaPos;
		_lastPos += deltaPos;
		_currentPos += deltaPos;
		_newPos += deltaPos;
	}

	public void exportCollidersToIniFile(Transform child)
	{
		Log.Debug("Exporting colliders to ini file " + _ap._ammunitionFileName + ".ini", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\WeaponBase.cs", 2254, "exportCollidersToIniFile");
		IniHandler iniHandler = new IniHandler();
		if (File.Exists(Application.streamingAssetsPath + "/original/ammunition/" + _ap._ammunitionFileName + ".ini"))
		{
			iniHandler.open(Application.streamingAssetsPath + "/original/ammunition/" + _ap._ammunitionFileName + ".ini", useCaching: false);
			iniHandler.writeValue("Colliders", "Collider", child.name);
			if (child.GetComponent<BoxCollider>() != null)
			{
				iniHandler.writeValue(child.name, "Collider", "Box");
			}
			if (child.GetComponent<SphereCollider>() != null)
			{
				iniHandler.writeValue(child.name, "Collider", "Sphere");
			}
			iniHandler.writeValue(child.name, "Position", child.transform.localPosition);
			iniHandler.writeValue(child.name, "Rotation", child.transform.localEulerAngles);
			iniHandler.writeValue(child.name, "Scale", child.transform.localScale);
			iniHandler.saveFile();
		}
		else
		{
			Log.Error("Cant export to " + Application.streamingAssetsPath + "/original/ammunition/" + _ap._ammunitionFileName + ".ini! File not found", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\WeaponBase.cs", 2261, "exportCollidersToIniFile");
		}
	}

	public void showHideColliders()
	{
		if (_ap._hitbox._go != null)
		{
			MeshRenderer component = _ap._hitbox._go.GetComponent<MeshRenderer>();
			if (component != null)
			{
				component.enabled = !component.enabled;
			}
		}
	}

	public WeaponInstance getWeaponInstance()
	{
		return _weaponInstance;
	}

	public bool getLaunchObjectSwitched()
	{
		return _launchObjectSwitched;
	}

	public void setLaunchObjectSwitched(bool launchObjectSwitched)
	{
		_launchObjectSwitched = launchObjectSwitched;
	}

	public bool isLaunched()
	{
		return _isLaunched;
	}

	public FlightStage getFlightStage()
	{
		return _flightStage;
	}

	public SensorEcho getStrongestSensorEcho()
	{
		return _seeker._strongestSensorEcho;
	}

	public SeekerBase getSeeker()
	{
		return _seeker;
	}

	protected void InitiateCollisionCommand()
	{
		if (!(GameTime.time - _launchTime < 1f))
		{
			Vector3 vector = _newPos - _lastPos;
			float magnitude = vector.magnitude;
			SpherecastCommands.AddData(base.UniqueID, _newPos, _ap._fuzeProximityDistance, vector.normalized, magnitude, -131073);
		}
	}

	public void CheckCollision()
	{
		if (_ignoreCollisions || GameTime.time - _launchTime < 1f)
		{
			return;
		}
		Vector3 direction = _newPos - _lastPos;
		int num = 0;
		float magnitude = direction.magnitude;
		if (PhysicsUtils.FastOverlapSphereNonAlloc(_newPos, magnitude, ref _quickOverlapCheckResults, -131073))
		{
			num = ((!_proximityFuse || !_fuseActive || _collisionNoticed) ? PhysicsUtils.RaycastNonAlloc(_lastPos, direction, _newPos, ref _raycastParams, magnitude, -131073) : PhysicsUtils.SphereCastNonAlloc(_lastPos, _ap._fuzeProximityDistance, direction, _newPos, ref _raycastParams, magnitude, -131073));
		}
		if (num <= 0)
		{
			return;
		}
		for (int i = 0; i < num && _raycastParams.results[i].collider.gameObject.layer != 17; i++)
		{
			if (_raycastParams.results[i].collider.name == "col_missile_proximity" || (_launchPlatform != null && _raycastParams.results[i].collider.transform.root.gameObject == _launchPlatform.transform.root.gameObject && GameTime.time - _launchTime < 10f) || _raycastParams.results[i].collider.transform.root.gameObject == base.transform.root.gameObject)
			{
				continue;
			}
			DM.I($"{_ap._ammunitionFileName} hits {num} nonignorable colliders. Proximity fuse = {_proximityFuse}");
			ObjectBase componentInChildren = _raycastParams.results[i].transform.root.GetComponentInChildren<ObjectBase>();
			if (componentInChildren != null && componentInChildren.isWeapon() && componentInChildren._taskforce == base._taskforce && CurrentIntendedTargetObject != componentInChildren)
			{
				continue;
			}
			DM.I($"<color=magenta>{getUIDAndName()} hits {_raycastParams.results[i].collider.name}</color>  _fuseActive = {_fuseActive}");
			if (_fuseActive)
			{
				if (_proximityFuse && !_collisionNoticed)
				{
					bool flag = _ap._smartFuse;
					if (this is Torpedo && componentInChildren is Torpedo torpedo && (_ap._antiTorpedo || torpedo._ap._subType == Ammunition.Type.MOSS))
					{
						flag = false;
					}
					if (flag)
					{
						if (!(_raycastParams.results[i].collider.name == "col_missile_hit"))
						{
							_collisionNoticed = true;
							_lastDistanceToCollisionSq = (base.transform.position - _raycastParams.results[i].transform.position).sqrMagnitude;
							_smartFuseTargetTransform = _raycastParams.results[i].transform;
							break;
						}
						continue;
					}
					if (_ap._subType != Ammunition.Type.MOSS)
					{
						if (_isTargetAirborne && this is Missile missile && componentInChildren != null)
						{
							if (missile._interceptionData.ContainsKey(componentInChildren) && !missile._interceptionData[componentInChildren])
							{
								_ignoreCollisions = true;
								_fuseActive = false;
								_targetLost = true;
								_targetLostTime = GameTime.time - _ap._selfDestructDelay + 0.5f;
								break;
							}
							new Blastzone(this, _newPos).CreateExplosion(componentInChildren);
						}
						else
						{
							new Blastzone(this, _newPos).CreateExplosion();
						}
						_fuseActive = false;
					}
				}
				else
				{
					_newPos = _raycastParams.results[i].point - base.transform.forward * 0.005f;
					_ = _raycastParams.results[i].point;
					DM.I($"Explosion point: Real = {_raycastParams.results[i].point.x}, {_raycastParams.results[i].point.y}, {_raycastParams.results[i].point.z}, Adjusted = {_newPos.x}, {_newPos.y}, {_newPos.z}");
					if (_ap._subType != Ammunition.Type.MOSS)
					{
						new Blastzone(this, _newPos).CreateExplosion();
					}
				}
			}
			DM.Log(FullIDData() + " is destroyed by collision with " + ((componentInChildren != null) ? componentInChildren.getUIDAndName() : "Terrain"), DM.EventLogFilters.Destruction);
			Destruction(_newPos, base.transform.rotation, componentInChildren);
			break;
		}
	}

	public override void SaveStateToFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.SaveStateToFile(ini, sectionName, isCampaignStateSave);
		if (this == null)
		{
			return;
		}
		ini.writeValue(sectionName, "Heading", $"{base.transform.eulerAngles.x},{base.transform.eulerAngles.y},{base.transform.eulerAngles.z}");
		ini.writeValue(sectionName, "Type", _ap._ammunitionFileName);
		ini.writeValue(sectionName, "Nation", base.Nation.Value);
		if (_isUndetectable)
		{
			ini.writeValue(sectionName, "Undetectable", value: true);
		}
		ini.writeValue(sectionName, "FlightStage", _flightStage.ToString());
		if (base.StateMachine != null)
		{
			ini.writeValue(sectionName, "CurrentState", base.StateMachine.CurrentStateName);
		}
		ini.writeValue(sectionName, "LaunchTime", _launchTime - GameTime.time);
		ini.writeValue(sectionName, "TraveledDistance", _traveledDistance);
		if (_isBoosterEffectStarted)
		{
			ini.writeValue(sectionName, "BoosterEffectStarted", value: true);
		}
		if (_seeker._keepTarget)
		{
			ini.writeValue(sectionName, "SeekerKeepTarget", _seeker._keepTarget);
		}
		if (CurrentIntendedTargetObject != null)
		{
			ini.writeValue(sectionName, "TargetObject", CurrentIntendedTargetObject._sectionName);
		}
		if (base.CommandPosition.HasValue)
		{
			ini.writeValue(sectionName, "TargetGeoPosition", $"{base.CommandPosition.Value._latitude},{base.CommandPosition.Value._longitude},{base.CommandPosition.Value._height * 220.47265625}");
		}
		ObjectBase launchPlatform = _launchPlatform;
		if (launchPlatform != null)
		{
			ini.writeValue(sectionName, "HomeBase", launchPlatform._sectionName);
			int systemIndex = launchPlatform._obp.getSystemIndex(_weaponSystem);
			if (_weaponSystem != null)
			{
				string value = launchPlatform._sectionName + Utils.GetSectionNameFromObject(_weaponSystem) + systemIndex;
				ini.writeValue(sectionName, "WeaponSystem", value);
			}
		}
		if (_guidingRadarSystem != null)
		{
			ini.writeValue(sectionName, "GuidingUnit", _guidingRadarSystem._baseObject._sectionName);
			ini.writeValue(sectionName, "GuidingSystem", _guidingRadarSystem._sectionName);
		}
	}

	public override void LoadStateFromFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.LoadStateFromFile(ini, sectionName, isCampaignStateSave);
		if (!isCampaignStateSave)
		{
			_flightStage = ini.readValue(sectionName, "FlightStage", FlightStage.None);
		}
	}
}
