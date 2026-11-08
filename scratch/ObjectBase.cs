using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using AircraftStates;
using HelicopterStates;
using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using Noesis;
using ObservableComputations;
using ParadoxNotion;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UniRx;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;

namespace SeaPower;

public abstract class ObjectBase : MonoBehaviour, IUnit, ISelectableObject, IMapShowable, INotifyPropertyChanged, IDragDropCompleted
{
	public struct LoadoutCapabilities
	{
		public enum AmmoCategoryDefinitions
		{
			None,
			Gun,
			SRAAM,
			MRAAM,
			LRAAM,
			Bomb,
			Cluster,
			Sonobuoy,
			ASWTorpedo,
			ASuWTorpedo,
			Rockets,
			SRAGM,
			MRAGM,
			LRAGM,
			AShM,
			ARM,
			ASWMissile,
			MLRAAM,
			AAM,
			AGM,
			Strike
		}

		public bool _hasGun;

		public bool _hasSRAAM;

		public bool _hasMRAAM;

		public bool _hasLRAAM;

		public bool _hasBombs;

		public bool _hasCluster;

		public bool _hasSonobuoys;

		public bool _hasASWMissiles;

		public bool _hasASWTorpedoes;

		public bool _hasASuWTorpedoes;

		public bool _hasRockets;

		public bool _hasSRAGM;

		public bool _hasMRAGM;

		public bool _hasLRAGM;

		public bool _missileAttackRequiresTerrainCheck;

		public bool _hasAShM;

		public bool _hasARM;

		public bool _hasJammer;

		public bool _hasFueltanks;

		public List<LandUnitParameters.TargetPoint.Category> _prioritisedCategories;

		public float Range_AAW;

		public float Range_ASuW;

		public float Range_ASW;

		public float _totalAAMRange;

		public bool HasMLRAAM
		{
			get
			{
				if (!_hasMRAAM)
				{
					return _hasLRAAM;
				}
				return true;
			}
		}

		public bool HasAAM
		{
			get
			{
				if (!_hasSRAAM && !_hasMRAAM)
				{
					return _hasLRAAM;
				}
				return true;
			}
		}

		public bool HasAGM
		{
			get
			{
				if (!_hasSRAGM && !_hasMRAGM)
				{
					return _hasLRAGM;
				}
				return true;
			}
		}

		public bool CanStrike
		{
			get
			{
				if (!_hasBombs && !_hasRockets && !_hasCluster && !_hasARM)
				{
					return HasAGM;
				}
				return true;
			}
		}
	}

	public enum WeaponStatus
	{
		Tight,
		Free,
		Hold
	}

	public enum ObjectType
	{
		Other,
		Vessel,
		Submarine,
		Aircraft,
		Helicopter,
		LandUnit,
		Biologic,
		Missile,
		Torpedo,
		ASROC,
		RBU,
		Bomb,
		AerialRocket,
		Chaff,
		Noisemaker,
		Decoy,
		Projectile,
		Wakebubble,
		AI_Marker,
		Any
	}

	public enum AI_Type
	{
		None,
		PosSub,
		LaunchTransient
	}

	public enum HiddenIn
	{
		None,
		Encyclopedia,
		MissionEditor,
		Signatures
	}

	public enum CrewSkill
	{
		Green,
		Trained,
		Seasoned,
		Veterans,
		Ultra
	}

	public enum Store
	{
		Depleted,
		Few,
		Medium,
		Full
	}

	public enum UnitDamage
	{
		None,
		Minor,
		Medium,
		Heavy
	}

	public enum AltitudeCorrection
	{
		None,
		VeryLow,
		Low,
		Medium,
		High,
		VeryHigh,
		Cruise,
		Ceiling
	}

	public struct FcrRangeData
	{
		public float Range;

		public EngagementIssue? Issue;
	}

	public struct EngagementRange
	{
		public float KinematicRange;

		public float SensorRange;

		public float TerrainRange;

		public EstimatedSensorRangeData SensorData;

		public float TimeOfFlight;

		public float UnscreenedSensorRange;
	}

	public enum EngagementIssue
	{
		ObscuredByAltitude,
		InLookdown,
		NotEmitting,
		MissingEmittedBands,
		Jammed,
		BackupTargeting,
		OutOfAltitude,
		TargetTooFast,
		TargetTooDeep,
		CantSeeTarget,
		DefensiveJamming,
		HomeOnJam
	}

	public struct EstimatedSensorRangeData
	{
		public SensorSystem.SensorDistanceData SensorData;

		public SensorSystem Sensor;

		public float WeaponSensorLimit;

		public List<EngagementIssue> EngagementIssues;

		public float UnscreenedSensorDistance;

		public float UnscreenedWeaponSensorLimit;
	}

	public struct SensorRangeOptions
	{
		public bool RequireOpenSensorChannel;

		public bool IgnoreLookdownPenalty;

		public bool IgnoreCloudCover;

		public bool FrontTargetAspect;

		public bool IgnoreSelfScreeningJammers;

		public bool IgnoreTargetJamming;

		public bool IgnoreAllOffensiveJamming;

		public bool IgnoreAltitudeBrackets;
	}

	private readonly List<UnityEngine.Renderer> _opticalViewHiddenRenderers = new List<UnityEngine.Renderer>();

	public PatrolZone _patrolZone;

	private static readonly ProfilerMarker<int> _profSystemUpdates;

	private static readonly ProfilerMarker _profFreeJamChannels;

	private static readonly ProfilerMarker _profFreeLaserChannels;

	private static readonly ProfilerMarker _profCalculateIncomingThreats;

	private static readonly ProfilerMarker _profHandleEngageTasks;

	private static readonly ProfilerMarker _profTrackPoints;

	private static readonly ProfilerMarker _profFixedUpdate;

	private static readonly ProfilerMarker _profSystemFixedUpdates;

	private static readonly ProfilerMarker _profSpeedCommand;

	private bool _hasLoggedInvalidGeoPosition;

	private int _lastValidGeoPositionFrame = -1;

	private Vector3 _lastValidGeoTransformPosition;

	private GeoPosition _lastValidCalculatedGeoPosition;

	private NotifyPropertyChangedStorage<float> _testRingRadius = new NotifyPropertyChangedStorage<float>
	{
		Value = 303.15445f
	};

	private bool _hasBeenInstantiated;

	protected GameObject _rootObject;

	protected OcConsumer _consumer = new OcConsumer();

	protected CompositeDisposable _disposer = new CompositeDisposable();

	public bool _spawnedByCivRoute;

	public bool _loadedFromSaveFile;

	public string _subState = "";

	public string _campaignTag = "";

	public bool _affectedByCampaignData;

	public bool _campaignRearm;

	public bool _campaignRepair;

	public float _consumptionCoefficient = 1f;

	public bool _unlimitedFuel;

	public int _quickAccessNumber = -1;

	private OwnSideSensor _selfSensor;

	public List<float> _rwrBearings = new List<float>();

	public List<float> _jamBearings = new List<float>();

	public bool _isCurrentWaypointReachable;

	public ObjectBase _currentWaypointParent;

	public bool _performingFormationTurn;

	public bool _performingOuterTurn;

	private GeoPosition? _commandPosition;

	public float _altitudeTreshold = 0.00453571f;

	private bool _isControllable = true;

	public bool _hasControl;

	private bool _disabled;

	public double _turnOffRigidbodyTime = -1.0;

	public bool _rigidBodyTurnedOff;

	public bool _fuelTanksDropped;

	public bool _navLightsAreFlickering;

	public PowerSystem _powerSystemInUse;

	public Crew _crew;

	public IState _lastBaseState;

	public string _flightDeckState = "";

	[Obsolete]
	public int _flightDeckWaypointsCount = -1;

	public bool _performingAirOps;

	public bool _ignoreInCollisionAvoidanceAfterLaunch;

	public List<SensorSystem> _sensorsToTurnOff = new List<SensorSystem>();

	[NonSerialized]
	public float _reactionBandSeconds = -1f;

	[NonSerialized]
	public int _datalinkTierResolved = -1;

	[NonSerialized]
	public float _longestScanPeriodSeconds = -1f;

	[NonSerialized]
	public float _softKillBaseSeconds = -1f;

	public List<EngageTask> _currentEngageTasks = new List<EngageTask>();

	public List<ObjectBase> _spottedObjects = new List<ObjectBase>();

	public List<AlignUnitToTarget> _alignmentTasks = new List<AlignUnitToTarget>();

	public AlignUnitToTarget _prepAlignment;

	public const int PREP_ALIGNMENT_UID = int.MaxValue;

	public Vector3 _alignmentPosition = Vector3.zero;

	public bool _isSpeedCommandOverridesInfo;

	public bool _speedSetByPlayer;

	public AI _ai;

	public BehaviourTreeOwner _agent;

	public BehaviourTree _behaviourTree;

	public UserRootNode _userRoot;

	public Vector2 _sonarBaffles = Vector2.zero;

	public string _sectionName;

	public LoadoutCapabilities _initialLoadoutCapabilities;

	public LoadoutCapabilities _currentLoadoutCapabilities;

	public bool _inCombatOverride;

	public bool _inCombat;

	private double _checkInCombatTime;

	public bool _searchForHomeBase = true;

	public float _accountableAmmoReloadPoints;

	public float _ammoReloadPoints;

	public float _ammoReloadCapability = 10f;

	private double _raftPickUpTime = -1000.0;

	public float _raftPickUpInterval = 3f;

	private double _mineCheckTime = -1000.0;

	public float _mineCheckInterval = 3f;

	private TacticalDoctrine _tacticalDoctrine;

	public ObjectType _type;

	protected string _objectIniName;

	private static readonly Dictionary<string, int> _nextVoiceoverProfileSlots;

	private int _voiceoverProfileSlot = -1;

	private string _voiceoverProfileNation;

	public float _velocityInKnots;

	public float _velocityInUnity;

	public Vector3 _velocityVecInUnity;

	protected float _defaultCameraDistance;

	protected float _minCameraDistanceForeAft;

	protected float _minCameraDistanceBroadside;

	protected float _cameraPivotHeight;

	public float _weaponLaunchReportTime = -1000f;

	public float _weaponLaunchReportInterval = 10f;

	public string _statusInfo = "";

	public bool _blockGeoPositionJob;

	public bool _teleportProtection;

	public bool _isUndetectable;

	public bool _showColliders;

	protected bool _hasWaypointBeenReached;

	public GeoPosition _defaultOrderGeoPosition;

	public AltitudeCorrection _altitudeCorrection;

	public bool _correctAltitudeOnSpawn = true;

	public bool _setDesiredAltitude;

	public float _desiredAltitudeToSet;

	protected List<GeoPosition> _trackPoints;

	protected float _cameraYOffsetForDirectControl;

	public bool _canUpdate;

	protected bool _camLookAroundActive;

	protected Camera _mainCam;

	protected Vector3 _objectForward;

	protected Vector3 _objectRight;

	protected Vector3 _objectUp;

	protected Vector3 _toTargetDirection;

	protected Vector3 _toTargetForward;

	protected Vector3 _targetInterceptPosition;

	protected float _deltaYawToTarget;

	protected float _deltaPitchToTarget;

	protected float _currentPitchRate;

	protected float _deltaRollToTarget;

	protected float _deltaHeightToTarget;

	protected float _distToTarget;

	protected double _hasControlStartTime;

	public float _overallDamage;

	public List<GameObject> _damageDecals = new List<GameObject>();

	public double _systemExplosionReduceEndTime = -1000.0;

	public float _systemExplosionChanceMultiplier = 1f;

	protected float _passiveContactsLastUpdateTime = -1000f;

	public Vector2 _lastCenterTile;

	public ObjectBase _homeBase;

	public List<ObjectBase> _childAircraft = new List<ObjectBase>();

	public List<GameObject> _debugGameObjectToDestroy = new List<GameObject>();

	public float _groundedPitchAngle;

	protected GameObject _collidersGO;

	protected Quaternion _lastCameraRotation;

	protected float _lastCameraDistToPivot;

	protected float _targetCourse;

	private int _orderFeedbackVersion;

	public bool _playerCommandOverride;

	protected bool _playerHeightControl;

	public string _selectedAmmunitionForEngage;

	private readonly HashSet<string> _missingAmmunitionReferencesLogged = new HashSet<string>();

	protected Dictionary<string, List<WeaponSystem>> _ammunitionToWeaponSystemsDictionary = new Dictionary<string, List<WeaponSystem>>();

	public Dictionary<string, List<BaseSystem>> _colliderToSystemDictionary = new Dictionary<string, List<BaseSystem>>();

	public Dictionary<string, int> _allAvailableAmmo = new Dictionary<string, int>();

	public float _torpedoLaunchDelay;

	public double _torpedoLastLaunchTime = -1000.0;

	public Dictionary<string, float> _sharedLaunchIntervals = new Dictionary<string, float>();

	public Dictionary<string, double> _sharedLaunchTimers = new Dictionary<string, double>();

	protected bool _lastEMCON;

	public List<SensorSystemECM> _ECMSystemsTryingToJamMe = new List<SensorSystemECM>();

	private const float RadarLockAlertInterval = 30f;

	private double _lastRadarLockAlertTime = -1000.0;

	public WeaponStatus _weaponStatus;

	public bool _previousSonobouyDepthAboveLayer;

	public Dictionary<string, ReactiveProperty<AmmunitionParameters.SearchDepth>> _searchDepthSettings = new Dictionary<string, ReactiveProperty<AmmunitionParameters.SearchDepth>>();

	public Dictionary<string, ReactiveProperty<AmmunitionParameters.SearchMode>> _searchModeSettings = new Dictionary<string, ReactiveProperty<AmmunitionParameters.SearchMode>>();

	public Dictionary<string, ReactiveProperty<int>> _launchSpeedSettings = new Dictionary<string, ReactiveProperty<int>>();

	public Dictionary<string, ReactiveProperty<AmmunitionParameters.LaunchSeekerMode>> _launchSeekerModeSettings = new Dictionary<string, ReactiveProperty<AmmunitionParameters.LaunchSeekerMode>>();

	public int _maxWireCount = int.MaxValue;

	public float _wakeBubbleDropInterval;

	public float _wakeBubbleCurrentLifetime;

	private float _wakeBubbleDropTimer;

	private float _tailingWakeBubbleDropTimer;

	private WakeBubble _tailWakeBubble;

	private Taskforce _innerTaskforceValue;

	private UnitFormation _formation;

	public List<WeaponBase> _incomingWeapons = new List<WeaponBase>();

	[NonSerialized]
	public bool _incomingWeaponsTargetThisTick;

	public bool _externalDestructionNotified;

	public bool _destroyedInSave;

	private bool isDestroyed;

	public double _destructionTime = -1000.0;

	protected GameObject _shotDownFireEffectGO;

	protected GameObject _surfaceBurningEffectGO;

	public List<ObjectBase> _threats = new List<ObjectBase>();

	public bool _evadingTorpedo;

	public HashSet<ObjectBase> _ownDetectedContacts = new HashSet<ObjectBase>();

	public List<ObjectBase> _knownPlayerUnitsSorted = new List<ObjectBase>();

	public Vector3 _viewPointLocalPosition = Vector3.zero;

	public SensorData _sensorData;

	[NonSerialized]
	public int _sonarCacheIndex = -1;

	[SerializeField]
	private int _uniqueID = Singleton<SceneCreator>.Instance.GenerateUid();

	public bool _isCavitating;

	public float _cavitationIntensity;

	public int _signalProcessingOffset;

	protected ObjectSoundHandler _soundHandler;

	public ObjectBaseParameters _obp;

	protected bool _damageModelActive;

	public float _totalDamagePoints;

	public float _ciwsDamageTaken;

	public int _ciwsHitsTaken;

	public int _ciwsLateHitsTaken;

	public float _ciwsCountableExpected;

	public float _ciwsLateExpected;

	public float _ciwsVitalPerHitLast;

	public float _ciwsHitsNeededLast = 1f;

	public float _ciwsShredNeededLast = float.MaxValue;

	private int _playerSetTelegraph;

	public int _previousTelegraphSetting;

	public LandUnitParameters.TargetPoint _currentTargetPointDebug;

	private double _torpedoCheckTime = -1000.0;

	public float _torpedoCheckInterval = 5f;

	private double _torpedoEvadeTimer;

	public double _counterLaunchCheckTime = -1000.0;

	public float _counterLaunchCooldownInterval = 900f;

	public List<WeaponBase> _reportedWeapons = new List<WeaponBase>();

	[SerializeField]
	private Torpedo _threatingTorpedo;

	public List<Torpedo> _dangerousTorpedoes = new List<Torpedo>();

	private double _missileCheckTime = -1000.0;

	public float _missileCheckInterval = 1f;

	private double _lastThreatCalculationTime = -1000.0;

	private const float ThreatCalculationInterval = 0.5f;

	public float _missileEvadeTimer;

	[SerializeField]
	private Missile _threateningMissile;

	public double _underGunFireSince = -1000.0;

	public double _lastUnderGunFireTime = -1000.0;

	public List<Missile> _dangerousMissiles = new List<Missile>();

	private List<ObjectCodeAnimation> _codeAnimationsToPlay = new List<ObjectCodeAnimation>();

	public bool _playingCodeAnims;

	public double _spawnTime = -1000.0;

	private sbyte _incomingWeaponsMode = -1;

	private double _nextCantSeeAirTargetCheckTime;

	private readonly HashSet<ObjectBase> _solutionTargetScratch = new HashSet<ObjectBase>();

	private readonly HashSet<SensorSystem> _claimedDirectorScratch = new HashSet<SensorSystem>();

	private readonly HashSet<SensorSystem> _capacityDirectorScratch = new HashSet<SensorSystem>();

	private readonly HashSet<ObjectBase> _engagedContactScratch = new HashSet<ObjectBase>();

	private readonly HashSet<SensorSystem> _totalChannelScratch = new HashSet<SensorSystem>();

	private const double SolutionAlreadyHeld = 3600.0;

	[NonSerialized]
	public float _ownPlottingPhase = Globals._unityRandom.NextFloat();

	private static readonly ProfilerMarker _profOwnState;

	private static readonly ProfilerMarker _profMakeTruth;

	private static readonly ProfilerMarker _profAddTrack;

	private bool? _hasDatalinkGuidanceFcr;

	public const float CANT_SEE_AIR_TARGET_RANGE = 20.669622f;

	private Dictionary<string, int> _initialAmmunitionLoadout;

	private const float AAM_THREAT_BONUS_STRIKE_PACKAGE = 5f;

	private const float AAM_THREAT_BONUS_CLOSING_AIRCRAFT = 4f;

	private const float AAM_THREAT_BONUS_AEW = 3f;

	private const float AAM_THREAT_BONUS_FIGHTER = 2f;

	private const float AAM_THREAT_BONUS_ASW = 1f;

	private const float AAM_THREAT_BONUS_AIRCRAFT = 0f;

	private const float AAM_SATURATION_STEP_NM = 30f;

	private const float AAM_MISSILE_INTERCEPT_BONUS = 80f;

	private const float AAM_CLAIM_CREDIBLE_DIST_FACTOR = 1.5f;

	private const float AAM_CLAIM_CREDIBLE_SLACK_NM = 10f;

	public float TestRingRadius
	{
		get
		{
			return _testRingRadius.Value;
		}
		set
		{
			_testRingRadius.SetField(this.PropertyChanged, this, value, "TestRingRadius");
		}
	}

	public PlottingTable PlottingTable { get; set; }

	public Entity Entity { get; internal set; }

	ObjectBase IUnit.BaseObject => this;

	public ReactiveProperty<Taskforce> UnitTaskforce { get; } = new ReactiveProperty<Taskforce>();


	public int Id => GetMapContactID();

	public ReactiveProperty<bool> Identified { get; } = new ReactiveProperty<bool>(initialValue: true);


	public ReactiveProperty<GeoPosition> Position { get; } = new ReactiveProperty<GeoPosition>(default(GeoPosition), GeoPositionComparer.Instance);


	public ReadOnlyReactiveProperty<GeoPosition> UpdatePosition { get; private set; }

	public Vector3 UnityVelocityVector => _velocityVecInUnity;

	public ReactiveProperty<string> IncomingSignalInfo { get; } = new ReactiveProperty<string>("");


	public ReactiveProperty<string> IncomingEmissionsInfo { get; } = new ReactiveProperty<string>("");


	public ReactiveProperty<float> RangeOnMap { get; } = new ReactiveProperty<float>();


	public ReactiveProperty<float> RangeInKm { get; } = new ReactiveProperty<float>();


	public string TypeAbbreviation
	{
		get
		{
			if (_obp != null)
			{
				return _obp._typeAbbr;
			}
			if (this is WeaponBase weaponBase)
			{
				return weaponBase._ap._displayedType;
			}
			return "";
		}
	}

	public string ClassNameShort
	{
		get
		{
			if (_obp != null)
			{
				return _obp._className;
			}
			if (this is WeaponBase weaponBase)
			{
				return weaponBase._ap._displayedName;
			}
			return "";
		}
	}

	public string ClassNameFull
	{
		get
		{
			if (_obp != null)
			{
				return _obp._defaultName;
			}
			if (this is WeaponBase weaponBase)
			{
				return weaponBase._ap._displayedName + " " + weaponBase._ap._displayedNickname;
			}
			return "";
		}
	}

	public string DisplayNameShort
	{
		get
		{
			if (IsPlayerObject && IsAirUnit && InFormation.Value)
			{
				return Name.Value;
			}
			if (_obp != null)
			{
				return _obp._shortName;
			}
			if (this is WeaponBase weaponBase)
			{
				return weaponBase._ap._displayedName;
			}
			return "";
		}
	}

	public string DisplayNameFull
	{
		get
		{
			if (_obp != null)
			{
				return _obp._objectName;
			}
			if (this is WeaponBase weaponBase)
			{
				return weaponBase._ap._displayedNickname;
			}
			return "";
		}
	}

	public OwnSideSensor SelfSensor
	{
		get
		{
			if (_selfSensor == null)
			{
				_selfSensor = new OwnSideSensor(this);
			}
			return _selfSensor;
		}
	}

	public bool AutomateRoute { get; set; }

	public MoraleLevel Morale { get; set; } = MoraleLevel.Normal;


	public bool IsFleeing => Morale == MoraleLevel.Fleeing;

	public float WaypointTolerance
	{
		get
		{
			float num = 0.1f;
			if (_obp != null)
			{
				num = _obp._waypointTolerance;
			}
			else if (this is WeaponBase weaponBase)
			{
				num = weaponBase._ap._waypointTolerance;
			}
			float num2 = num;
			if (IsAirUnit && Velocity.HasValue)
			{
				num2 = 0.0076554087f * Velocity.Value * 2f * GameTime.fixedDeltaTime;
				if (this is Helicopter helicopter && helicopter.StateMachine.CurrentState is HelicopterStates.MaritimePatrol)
				{
					num2 = 5.5118995f;
				}
			}
			if (GameTime.TimeCompression > 500f)
			{
				num2 = 10f;
			}
			else if (GameTime.TimeCompression > 50f)
			{
				num2 = 8f;
			}
			if (num < num2)
			{
				num = num2;
			}
			return num;
		}
	}

	public bool isLoitering { get; set; }

	public bool isLanding { get; set; }

	public bool IsLoitering
	{
		get
		{
			return isLoitering;
		}
		set
		{
			isLoitering = value;
		}
	}

	public bool IsLanding
	{
		get
		{
			return isLanding;
		}
		set
		{
			isLanding = value;
		}
	}

	public GeoPosition? CommandPosition
	{
		get
		{
			return _commandPosition;
		}
		set
		{
			_commandPosition = value;
			if (_commandPosition is GeoPosition)
			{
				LoiterPosition = value;
			}
		}
	}

	public GeoPosition? LoiterPosition { get; protected set; }

	public GeoPosition? LastCommandPosition { get; set; }

	public bool Aligning => _alignmentTasks.Count > 0;

	public ReactiveProperty<float> DesiredAltitude { get; } = new ReactiveProperty<float>();


	public bool OnAltitude => Mathf.Abs(base.transform.position.y - DesiredAltitude.Value) < _altitudeTreshold;

	public float AltitudeOverSurface
	{
		get
		{
			float num = TerrainHeightInUnity;
			if (num < 0f)
			{
				num = 0f;
			}
			return base.transform.position.y - num;
		}
	}

	public float AltitudeOverClutter
	{
		get
		{
			float num = TerrainHeightInUnity;
			if (num < 0f)
			{
				int num2 = Singleton<Environment>.Instance.SeaState;
				if (num2 < 0)
				{
					num2 = 0;
				}
				else if (num2 > 12)
				{
					num2 = 12;
				}
				num = Constants.WAVE_HEIGHTS[num2];
			}
			return base.transform.position.y - num;
		}
	}

	public float AltitudeOverSeaClutter
	{
		get
		{
			int num = Singleton<Environment>.Instance.SeaState;
			if (num < 0)
			{
				num = 0;
			}
			else if (num > 12)
			{
				num = 12;
			}
			return base.transform.position.y - Constants.WAVE_HEIGHTS[num];
		}
	}

	public bool OnCourse
	{
		get
		{
			if (this is Helicopter)
			{
				UnityEngine.Debug.LogError(getUIDAndName() + " check OnCourse");
			}
			float num = 0f;
			if (isUnit())
			{
				if (CourseCommand.Value != null)
				{
					num = Utils.WrapAngle(CourseCommand.Value.GetDemandedCourse(this) - Heading.Value);
				}
				else
				{
					GeoPosition? commandPosition = CommandPosition;
					if (commandPosition.HasValue)
					{
						GeoPosition valueOrDefault = commandPosition.GetValueOrDefault();
						num = Utils.WrapAngle(Position.Value.HeadingRangeMeters(valueOrDefault).heading - Heading.Value);
					}
				}
			}
			if (this is Helicopter)
			{
				UnityEngine.Debug.LogError($"{getUIDAndName()} deltaHeading = {num}");
			}
			return Math.Abs(num) < 2f;
		}
	}

	public bool IsPlayerObject => _taskforce.Side == Taskforce.TfType.Player;

	public bool IsPlayerUnit
	{
		get
		{
			if (_taskforce.Side == Taskforce.TfType.Player)
			{
				return isUnit();
			}
			return false;
		}
	}

	public bool IsEnemyObject => _taskforce.Side == Taskforce.TfType.Enemy;

	public bool AcceptsOrdersFromPlayer
	{
		get
		{
			if (_taskforce.Side != Taskforce.TfType.Player)
			{
				return Globals.AllowControlOfEnemyUnits.Value;
			}
			return true;
		}
	}

	public bool AcceptsOrdersFromAI
	{
		get
		{
			if (IsControllable)
			{
				return _hasControl;
			}
			return false;
		}
	}

	public bool IsControllable
	{
		get
		{
			return _isControllable;
		}
		set
		{
			_isControllable = value;
			NotifyPropertyChanged("IsControllable");
		}
	}

	public bool HasControl
	{
		get
		{
			return _hasControl;
		}
		set
		{
			_hasControl = value;
			NotifyPropertyChanged("HasControl");
		}
	}

	public bool Disabled
	{
		get
		{
			return _disabled;
		}
		set
		{
			_disabled = value;
			NotifyPropertyChanged("Disabled");
		}
	}

	public StateMachine StateMachine { get; protected set; }

	public bool IsAirUnit
	{
		get
		{
			if (!(this is Aircraft))
			{
				return this is Helicopter;
			}
			return true;
		}
	}

	public bool IsSurfaceUnit
	{
		get
		{
			if (!(this is Vessel))
			{
				return this is LandUnit;
			}
			return true;
		}
	}

	public bool IsSubUnit
	{
		get
		{
			if (!(this is Submarine))
			{
				return this is Biologic;
			}
			return true;
		}
	}

	public EngageTask FirstEngageTask
	{
		get
		{
			if (_obp == null)
			{
				return null;
			}
			if (_alignmentTasks.Count > 0)
			{
				return _alignmentTasks[0]?._weaponSystem?._takenEngageTask;
			}
			WeaponSystem weaponSystem = null;
			double num = double.MaxValue;
			foreach (WeaponSystem weaponSystem2 in _obp._weaponSystems)
			{
				if (weaponSystem2._executingEngageTask && weaponSystem2._engageStartTime < num)
				{
					num = weaponSystem2._engageStartTime;
					weaponSystem = weaponSystem2;
				}
			}
			return weaponSystem?._takenEngageTask;
		}
	}

	public bool HasSpottingSystem
	{
		get
		{
			if (_obp != null)
			{
				return _obp._visualSensors.Any((SensorSystemVisual x) => x._isRecon);
			}
			return false;
		}
	}

	public ReactiveProperty<int> SpottedUnitsCount { get; } = new ReactiveProperty<int>(0);


	public ReactiveProperty<bool> CavitationForbidden { get; } = new ReactiveProperty<bool>(initialValue: true);


	public ReactiveProperty<bool> SprintAndDriftDoctrine { get; } = new ReactiveProperty<bool>(initialValue: false);


	public ReactiveProperty<bool> BafflesClearingDoctrine { get; } = new ReactiveProperty<bool>(initialValue: false);


	public ReactiveProperty<bool> FighterDoNotEngageAntiShipMissilesDoctrine { get; } = new ReactiveProperty<bool>(initialValue: false);


	public ReactiveProperty<TacticalDoctrine.AircraftBombAttackProfile> AircraftBombAttackProfileDoctrine { get; } = new ReactiveProperty<TacticalDoctrine.AircraftBombAttackProfile>(TacticalDoctrine.AircraftBombAttackProfile.Automatic);


	public ReactiveProperty<ISpeedCommand> SpeedCommand { get; } = new ReactiveProperty<ISpeedCommand>();


	public bool AllowCombatSpeedControl
	{
		get
		{
			if (IsPlayerObject)
			{
				return !_speedSetByPlayer;
			}
			return true;
		}
	}

	public ReactiveProperty<ICourseCommand> CourseCommand { get; } = new ReactiveProperty<ICourseCommand>();


	public virtual float MaxForwardSpeedInKnots { get; set; }

	public virtual float MaxBackwardSpeedInKnots { get; }

	public virtual float MaxForwardSpeedSubmergedInKnots { get; }

	public virtual float MaxBackwardSpeedSubmergedInKnots { get; }

	public ReactiveProperty<int> NumberOfFreeJamChannels { get; } = new ReactiveProperty<int>();


	public ReactiveProperty<int> NumberOfFreeLaserChannels { get; } = new ReactiveProperty<int>();


	public bool HasBeviourTree { get; set; }

	public ObjectCollisionAvoidance CollisionAvoidance { get; set; }

	public ReactiveProperty<bool> IdentifyAir { get; } = new ReactiveProperty<bool>();


	public ReactiveProperty<bool> EngageAir { get; } = new ReactiveProperty<bool>();


	public ReactiveProperty<bool> IdentifySurface { get; } = new ReactiveProperty<bool>();


	public ReactiveProperty<bool> EngageSurface { get; } = new ReactiveProperty<bool>();


	public ReactiveProperty<bool> IdentifySubSurface { get; } = new ReactiveProperty<bool>();


	public ReactiveProperty<bool> EngageSubSurface { get; } = new ReactiveProperty<bool>();


	public float MissilesToSaturateValue
	{
		get
		{
			if (_obp != null)
			{
				return _obp._missilesToSaturateValue;
			}
			if (this is WeaponBase weaponBase)
			{
				return weaponBase._ap._maximumMissileValue;
			}
			return 2f;
		}
	}

	public float TorpedoesToSaturateValue
	{
		get
		{
			if (_obp != null)
			{
				return _obp._torpedoesToSaturateValue;
			}
			if (this is WeaponBase weaponBase)
			{
				return weaponBase._ap._maximumTorpedoValue;
			}
			return 2f;
		}
	}

	public ReactiveProperty<bool> SupplyAllowed { get; } = new ReactiveProperty<bool>(initialValue: true);


	public ReactiveProperty<bool> RaftPickUpAllowed { get; } = new ReactiveProperty<bool>(initialValue: true);


	public TacticalDoctrine TacticalDoctrine
	{
		get
		{
			if (_tacticalDoctrine == null)
			{
				_tacticalDoctrine = new TacticalDoctrine(this);
			}
			return _tacticalDoctrine;
		}
	}

	public ReactiveProperty<string> Name { get; } = new ReactiveProperty<string>();


	public string ClassName => _obp?._className;

	public bool IsTrackableObject
	{
		get
		{
			if (HasControl)
			{
				if (!isUnit())
				{
					if (isWeapon() && !(this is Noisemaker) && _type != ObjectType.Decoy && _obp != null && !_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Deco))
					{
						return !_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SeaMine);
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public GeoPosition _geoPosition
	{
		get
		{
			return Position.Value;
		}
		set
		{
			Position.Value = value;
		}
	}

	public ReactiveProperty<string> Nation { get; } = new ReactiveProperty<string>();


	public Compartments Compartments
	{
		get
		{
			if (this is Vessel vessel)
			{
				return vessel._compartments;
			}
			if (this is Submarine submarine)
			{
				return submarine._compartments;
			}
			return null;
		}
	}

	public ISelectableViewModel ViewModel => CreateViewModel();

	public float VisibleAltitude
	{
		get
		{
			if (_obp == null)
			{
				return base.transform.position.y;
			}
			if (this is Submarine submarine && submarine.isSubmerged() && submarine.getRaisedMastsCount() > 0)
			{
				return 0.044642814f;
			}
			return base.transform.position.y + _obp._visibleHeight;
		}
	}

	public bool NeedsTerrainHeightCalculation { get; protected set; } = true;


	public float TerrainHeightInMeters { get; set; }

	public float TerrainHeightInUnity { get; set; }

	public float SurfaceHeightInUnity
	{
		get
		{
			if (!(TerrainHeightInUnity > 0f))
			{
				return 0f;
			}
			return TerrainHeightInUnity;
		}
	}

	public float TerrainHeightInFeet { get; set; }

	public float TerrainHeightAheadInUnity { get; set; }

	public Rigidbody RigidBody { get; protected set; }

	public ReactiveProperty<Order> CurrentOrder { get; } = new ReactiveProperty<Order>();


	public ReactiveProperty<string> CurrentOrderText { get; } = new ReactiveProperty<string>();


	public ReactiveProperty<string> OrderFeedbackText { get; } = new ReactiveProperty<string>();


	public ReactiveDictionary<string, Ammunition> AmmunitionNameToAmmunitionDictionary { get; } = new ReactiveDictionary<string, Ammunition>();


	public ReactiveDictionary<string, int> AmmunitionAmountDictionary { get; } = new ReactiveDictionary<string, int>();


	public ReactiveProperty<bool> Emcon { get; } = new ReactiveProperty<bool>();


	public ReactiveProperty<bool> IsDormant { get; } = new ReactiveProperty<bool>(initialValue: false);


	public ReactiveProperty<WeaponStatus> WeaponState { get; private set; } = new ReactiveProperty<WeaponStatus>
	{
		Value = WeaponStatus.Tight
	};


	public ReactiveProperty<Bomb.SonobuoyDeployState> SonoDeployState { get; } = new ReactiveProperty<Bomb.SonobuoyDeployState>();


	internal float WakeBubbleDropTimer => _wakeBubbleDropTimer;

	public Taskforce _taskforce
	{
		get
		{
			return _innerTaskforceValue;
		}
		set
		{
			_innerTaskforceValue = value;
			UnitTaskforce.Value = _taskforce;
			if (_taskforce != null)
			{
				Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
				if (!defaultGameObjectInjectionWorld.EntityManager.HasComponent<SPObject>(Entity))
				{
					AddEntity();
				}
				if (defaultGameObjectInjectionWorld.EntityManager.HasComponent<TaskforceMember>(Entity))
				{
					defaultGameObjectInjectionWorld.EntityManager.SetComponentData(Entity, new TaskforceMember
					{
						Taskforce = _taskforce.Entity
					});
				}
				else
				{
					defaultGameObjectInjectionWorld.EntityManager.AddComponentData(Entity, new TaskforceMember
					{
						Taskforce = _taskforce.Entity
					});
				}
			}
		}
	}

	public UnitFormation Formation
	{
		get
		{
			return _formation;
		}
		set
		{
			if (value == _formation)
			{
				return;
			}
			if (_formation != null)
			{
				_formation.Stations.CollectionChanged -= Stations_CollectionChanged;
				if (IsAirUnit)
				{
					Name.Value = _obp._objectName;
				}
			}
			_formation = value;
			NotifyPropertyChanged("Formation");
			if (_formation == null)
			{
				InFormation.Value = false;
			}
			else
			{
				InFormation.Value = true;
			}
			if (_formation != null)
			{
				_formation.Stations.CollectionChanged += Stations_CollectionChanged;
				Stations_CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				if (_formation.LeaderStation != null && _formation.LeaderStation.UnitObject != null && _formation.LeaderStation.UnitObject.IsAirUnit)
				{
					_formation.CalculateAirUnitNames("Formation");
				}
			}
		}
	}

	public ReactiveProperty<bool> InFormation { get; set; } = new ReactiveProperty<bool>();


	public ReactiveProperty<bool> ActsIndependentlyInFormation { get; } = new ReactiveProperty<bool>(initialValue: false);


	public bool FollowingFormation
	{
		get
		{
			if (Formation != null && Formation.GetStationForUnit(this) != null)
			{
				return !Formation.GetStationForUnit(this).UnitActsIndependently.Value;
			}
			return false;
		}
	}

	public bool IsFormationLeader
	{
		get
		{
			if (Formation != null)
			{
				return Formation.GetStationForUnit(this) == Formation.LeaderStation;
			}
			return false;
		}
	}

	public bool IsFormationWingman
	{
		get
		{
			if (Formation != null)
			{
				return Formation.GetStationForUnit(this) != Formation.LeaderStation;
			}
			return false;
		}
	}

	public TrulyObservableCollection<ObjectBase> FollowerUnits { get; } = new TrulyObservableCollection<ObjectBase>();


	public Station FormationStation
	{
		get
		{
			if (Formation == null)
			{
				return null;
			}
			return Formation.GetStationForUnit(this);
		}
	}

	public bool IsDestroyed
	{
		get
		{
			return isDestroyed;
		}
		set
		{
			if (value)
			{
				_destructionTime = GameTime.missionElapsedTime;
			}
			isDestroyed = value;
			NotifyPropertyChanged("IsDestroyed");
		}
	}

	public int UniqueID => _uniqueID;

	public ReactiveProperty<bool> IsAirSearchRadarsOn { get; } = new ReactiveProperty<bool>();


	public ReactiveProperty<bool> IsSurfaceSearchRadarsOn { get; } = new ReactiveProperty<bool>();


	public ReactiveProperty<bool> IsActiveSonarsOn { get; } = new ReactiveProperty<bool>();


	public ReactiveProperty<bool> IsTowedDecoyDeployed { get; } = new ReactiveProperty<bool>();


	public ReactiveProperty<bool> IsOffensivelyJamming { get; } = new ReactiveProperty<bool>();


	public Torpedo ThreatingTorpedo
	{
		get
		{
			return _threatingTorpedo;
		}
		set
		{
			_threatingTorpedo = value;
			if (!IsPlayerObject || value == null)
			{
				return;
			}
			for (int num = _reportedWeapons.Count - 1; num >= 0; num--)
			{
				if (_reportedWeapons[num] == null)
				{
					_reportedWeapons.RemoveAt(num);
				}
			}
			if (!_reportedWeapons.Contains(value))
			{
				_reportedWeapons.Add(value);
				Globals._mainGameViewModel.RaiseAlert(this, Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "TorpedoIncoming"));
				if (!(GameTime.missionElapsedTime - Globals._lastIncomingTorpedoTime < (double)Globals._incomingTorpedoInterval))
				{
					Globals._lastIncomingTorpedoTime = GameTime.missionElapsedTime;
					Singleton<VoiceMessage>.Instance.PlayVoiceSequence("TorpedoIncoming", this, (int)Utils.WrapAngle(_geoPosition.HeadingToPosition(_threatingTorpedo._geoPosition)));
				}
			}
		}
	}

	public Missile ThreateningMissile
	{
		get
		{
			return _threateningMissile;
		}
		set
		{
			_threateningMissile = value;
			if (!IsPlayerObject || value == null)
			{
				return;
			}
			for (int num = _reportedWeapons.Count - 1; num >= 0; num--)
			{
				if (_reportedWeapons[num] == null)
				{
					_reportedWeapons.RemoveAt(num);
				}
			}
			if (!_reportedWeapons.Contains(value))
			{
				_reportedWeapons.Add(value);
				if (value.CurrentTarget == this)
				{
					RaiseMissileAlert();
				}
			}
		}
	}

	public ObjectBaseInfo Info { get; private set; }

	public ReactiveProperty<float> Heading { get; } = new ReactiveProperty<float>();


	public ReadOnlyReactiveProperty<float> UpdateHeading { get; private set; }

	public ReactiveProperty<float> Velocity { get; } = new ReactiveProperty<float>();


	public ReadOnlyReactiveProperty<float> UpdateVelocity { get; private set; }

	public ReactiveProperty<float> Altitude { get; } = new ReactiveProperty<float>();


	public DragDropCompletedCallback OnDragDropCompleted { get; protected set; }

	public bool HasDatalinkGuidanceFcr
	{
		get
		{
			if (!_hasDatalinkGuidanceFcr.HasValue)
			{
				_hasDatalinkGuidanceFcr = false;
				if (_obp?._sensorSystems != null)
				{
					foreach (SensorSystem sensorSystem in _obp._sensorSystems)
					{
						if (sensorSystem != null && sensorSystem._isDatalinkGuidance)
						{
							_hasDatalinkGuidanceFcr = true;
							break;
						}
					}
				}
			}
			return _hasDatalinkGuidanceFcr.Value;
		}
	}

	public bool HasSelfScreeningJammer => HasNoiseJammer(null, requireRadiating: false);

	public virtual KinematicEnvelope? TrueKinematics => null;

	public event PropertyChangedEventHandler PropertyChanged;

	public event System.EventHandler OnDestruction;

	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public void RefreshLocalizedDisplayNames()
	{
		if (_obp != null)
		{
			_obp.ApplyPreferredDefaultNames();
			NotifyPropertyChanged("ClassNameShort");
			NotifyPropertyChanged("ClassNameFull");
			NotifyPropertyChanged("DisplayNameShort");
			NotifyPropertyChanged("DisplayNameFull");
			NotifyPropertyChanged("ClassName");
		}
	}

	static ObjectBase()
	{
		_profSystemUpdates = new ProfilerMarker<int>("ObjectBase: System Updates", "Count");
		_profFreeJamChannels = new ProfilerMarker("ObjectBase: UpdateNumberOfFreeJamChannels");
		_profFreeLaserChannels = new ProfilerMarker("ObjectBase: UpdateNumberOfFreeLaserChannels");
		_profCalculateIncomingThreats = new ProfilerMarker("ObjectBase: CalculateIncomingThreats");
		_profHandleEngageTasks = new ProfilerMarker("ObjectBase: HandleEngageTasks");
		_profTrackPoints = new ProfilerMarker("ObjectBase: TrackPoints");
		_profFixedUpdate = new ProfilerMarker("ObjectBase: FixedUpdate");
		_profSystemFixedUpdates = new ProfilerMarker("ObjectBase: System FixedUpdates");
		_profSpeedCommand = new ProfilerMarker("ObjectBase: SpeedCommand Update");
		_nextVoiceoverProfileSlots = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
		_profOwnState = new ProfilerMarker("ObjectBase: Update Own Plotting");
		_profMakeTruth = new ProfilerMarker("ObjectBase: Make truth track");
		_profAddTrack = new ProfilerMarker("ObjectBase: Add track");
		ParadoxNotion.TypeConverter.Converters.Add(new ReactiveValueConverter());
	}

	public void SetSpeedCommand(ISpeedCommand speedCommand)
	{
		ISpeedCommand speedCommand2 = null;
		if (SpeedCommand.HasValue)
		{
			speedCommand2 = SpeedCommand.Value;
		}
		SpeedCommand.Value = speedCommand;
		if (speedCommand2 is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	internal void RestoreWakeBubbleEmitter(float dropTimer, WakeBubble tailWakeBubble)
	{
		_wakeBubbleDropTimer = Mathf.Max(0f, dropTimer);
		if (tailWakeBubble != null)
		{
			_tailWakeBubble = tailWakeBubble;
		}
	}

	private void Stations_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		NotifyPropertyChanged("FollowingFormation");
		NotifyPropertyChanged("IsFormationLeader");
		NotifyPropertyChanged("IsFormationWingman");
		FollowerUnits.Clear();
		if (IsFormationLeader)
		{
			foreach (ObjectBase item in from x in Formation.Stations
				select x.UnitObject into y
				where y != null
				select y)
			{
				FollowerUnits.Add(item);
			}
		}
		NotifyPropertyChanged("FollowerUnits");
	}

	public void SetUniqueId(int i)
	{
		_uniqueID = i;
		if (Singleton<SceneCreator>.Instance._UID < _uniqueID)
		{
			Singleton<SceneCreator>.Instance._UID = _uniqueID + 1;
		}
		_incomingWeaponsMode = -1;
	}

	public float GunDamageFraction()
	{
		CiwsBallistics.Durability durability = CiwsBallistics.GetDurability(this);
		if (!(durability._health > 0.01f))
		{
			return 0f;
		}
		return _ciwsDamageTaken / durability._health;
	}

	public int GunDamageTier()
	{
		if (_damageModelActive)
		{
			return 2;
		}
		if (_ciwsDamageTaken <= 0f)
		{
			return 0;
		}
		float num = GunDamageFraction();
		if (!(num >= 0.66f))
		{
			if (!(num >= 0.33f))
			{
				return 0;
			}
			return 1;
		}
		return 2;
	}

	public void UpdateGunDamageState()
	{
		if (IsAirUnit && !IsDestroyed && !_externalDestructionNotified && this is Aircraft aircraft)
		{
			aircraft.UpdateExhaustDamageTint();
		}
	}

	public virtual void RaiseMissileAlert()
	{
		Globals._mainGameViewModel.RaiseAlert(this, Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "MissileIncoming"));
		if (!(GameTime.missionElapsedTime - Globals._lastIncomingMissileTime < (double)Globals._incomingMissileInterval))
		{
			Globals._lastIncomingMissileTime = GameTime.missionElapsedTime;
			if (Singleton<OptionsManager>.Instance.ContactVO)
			{
				Singleton<VoiceMessage>.Instance.PlayVoiceSequence("MissileIncoming", this, (int)Utils.WrapAngle(_geoPosition.HeadingToPosition(ThreateningMissile._geoPosition)));
			}
		}
	}

	private void RefreshPlayerThreatAlerts()
	{
		if (!IsPlayerObject)
		{
			return;
		}
		foreach (SensorSystemECM item in _ECMSystemsTryingToJamMe)
		{
			if (item != null && !(item._baseObject == null) && !item.IsDestroyed && !item.Inoperable.Value && (item._ecm._ep._type == ECMParameters.ECMType.Offensive || item._ecm._ep._type == ECMParameters.ECMType.Universal))
			{
				if (Globals._mainGameViewModel.TryRaisePlayerJammingAlert(this, Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "BeingJammed")) && DM_TestTools._enemyJammingDetectedVO)
				{
					Singleton<VoiceMessage>.Instance.PlayVoiceMessage("BeingJammed", this);
				}
				break;
			}
		}
	}

	public void RegisterRadarLockWarningSource(ObjectBase source, float bearing)
	{
		if (IsPlayerObject && !(source == null) && !(source == this) && source._taskforce != _taskforce && !(GameTime.missionElapsedTime - _lastRadarLockAlertTime < 30.0))
		{
			int num = Mathf.RoundToInt(Utils.WrapAngle(bearing));
			if (num >= 360)
			{
				num -= 360;
			}
			_lastRadarLockAlertTime = GameTime.missionElapsedTime;
			string alert = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "TargetedByRadar").Replace("${Bearing}", num.ToString("000"));
			Globals._mainGameViewModel.RaiseAlert(this, alert);
			Singleton<VoiceMessage>.Instance.PlayVoiceSequence("TargetedByRadar", this, num);
		}
	}

	public void ReportUnderFireFrom(ObjectBase attacker)
	{
		if (isUnit() && IsPlayerObject && !(attacker == null) && !(attacker == this) && attacker._taskforce != _taskforce && Globals._mainGameViewModel.TryRaisePlayerUnderFireAlert(this, Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "UnderFire")))
		{
			Singleton<VoiceMessage>.Instance.PlayVoiceMessage("UnderFire", this);
		}
	}

	public void NotifyGunFireDirectedAtUs()
	{
		if (GameTime.missionElapsedTime - _lastUnderGunFireTime > 30.0)
		{
			_underGunFireSince = GameTime.missionElapsedTime;
		}
		_lastUnderGunFireTime = GameTime.missionElapsedTime;
	}

	public virtual void OnUpdateEveryFrame()
	{
		if (!_correctAltitudeOnSpawn && _setDesiredAltitude)
		{
			_setDesiredAltitude = false;
			DesiredAltitude.Value = _desiredAltitudeToSet;
		}
		if (base.transform.position.y > Globals._maxObjectAltitude && _type != ObjectType.Missile)
		{
			base.transform.position = new Vector3(base.transform.position.x, Globals._maxObjectAltitude - 4.5f, base.transform.position.z);
			if (Utils.WrapAngle(base.transform.eulerAngles.x) < 0f)
			{
				base.transform.eulerAngles = new Vector3(-3f, base.transform.eulerAngles.y, base.transform.eulerAngles.z);
			}
		}
	}

	public virtual void OnLazyUpdate()
	{
		if (!isUnit() || _obp == null)
		{
			return;
		}
		WeaponState.Value = _weaponStatus;
		ActsIndependentlyInFormation.Value = Formation != null && Formation.GetStationForUnit(this) != null && Formation.GetStationForUnit(this).UnitActsIndependently.Value;
		_overallDamage = 0f;
		SpeedCommand.Value?.Update(this);
		if (_obp._systems != null)
		{
			foreach (BaseSystem system in _obp._systems)
			{
				if (!GameTime.IsPaused())
				{
					system.OnUpdate();
				}
				_overallDamage += system._maxIntegrity - system.CurrentIntegrity;
			}
		}
		RefreshPlayerThreatAlerts();
		CheckEMCON();
		if (IsDestroyed)
		{
			return;
		}
		UpdateNumberOfFreeJamChannels();
		UpdateNumberOfFreeLaserChannels();
		if (!GameTime.IsPaused())
		{
			if (_ai != null)
			{
				_ai.OnUpdate();
			}
			if (IsControllable)
			{
				CalculateIncomingThreats();
				if (GameTime.missionElapsedTime > _checkInCombatTime)
				{
					_checkInCombatTime = GameTime.missionElapsedTime + (double)Utils.RandomFloat(1f, 2f);
					if (!_fuelTanksDropped && (!IsPlayerObject || DM_TestTools._autoDropTanksForPlayer) && IsAirUnit && ThreateningMissile != null && IsAirUnit)
					{
						DropFueltanks(combatDrop: true);
					}
				}
				if (DM._logMode.HasFlag(DM.LogMode.Ivan) && _currentEngageTasks.Count > 50)
				{
					DM.E($"{FullIDData()} _currentEngageTasks.Count = {_currentEngageTasks.Count}");
				}
				HandleEngageTasks();
				if (isUnit() && !IsDestroyed)
				{
					if (_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Minesweep))
					{
						CheckForMineRemoval();
					}
					CheckForRaftPickUp();
				}
			}
			else if (_currentEngageTasks.Count > 0)
			{
				_currentEngageTasks.Clear();
			}
			if (_type == ObjectType.Vessel || _type == ObjectType.Submarine)
			{
				dropWakeBubble();
			}
			for (int num = _sensorsToTurnOff.Count - 1; num >= 0; num--)
			{
				if (num >= _sensorsToTurnOff.Count)
				{
					continue;
				}
				SensorSystem sensorSystem = _sensorsToTurnOff[num];
				if (sensorSystem == null)
				{
					_sensorsToTurnOff.Remove(sensorSystem);
					continue;
				}
				if (!sensorSystem.IsOn.Value && !sensorSystem._wasSetOn)
				{
					_sensorsToTurnOff.Remove(sensorSystem);
					continue;
				}
				bool flag = true;
				if (sensorSystem._turnOffTime > 0.0)
				{
					if (!(sensorSystem._turnOffTime < GameTime.missionElapsedTime))
					{
						continue;
					}
					sensorSystem._turnOffTime = -1.0;
				}
				foreach (WeaponParameters associatedWeaponSystemParameter in sensorSystem._associatedWeaponSystemParameters)
				{
					if (associatedWeaponSystemParameter._weaponSystem._executingEngageTask)
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					foreach (SensorSystem associatedFCR in sensorSystem._associatedFCRs)
					{
						if (associatedFCR._isGuiding)
						{
							flag = false;
							break;
						}
					}
				}
				if (flag && (sensorSystem._preLaunchTargetObject != null || sensorSystem._associatedTargets.Count > 0 || sensorSystem._associatedWeapons.Count > 0))
				{
					flag = false;
				}
				if (flag)
				{
					sensorSystem.Disable();
					_sensorsToTurnOff.Remove(sensorSystem);
				}
			}
			if (_sensorData != null && _sensorData._vidIncreaseEndTime < GameTime.missionElapsedTime)
			{
				_sensorData._vidIncrease = 0f;
			}
		}
		if (Globals._maxTrackPoints <= 0 || _trackPoints == null || _trackPoints.Count <= 0)
		{
			return;
		}
		GeoPosition b = _trackPoints.Last();
		if (GeoPosition.GetDistanceInDeg(_geoPosition, b) > Globals._trackPointDistance)
		{
			if (_trackPoints.Count < Globals._maxTrackPoints)
			{
				_trackPoints.Add(_geoPosition);
				return;
			}
			_trackPoints.RemoveAt(0);
			_trackPoints.Add(_geoPosition);
		}
	}

	public virtual void OnFixedUpdate()
	{
		if (!_canUpdate)
		{
			return;
		}
		if (_teleportProtection)
		{
			_teleportProtection = false;
		}
		else if ((HasControl || Disabled) && _lastCenterTile != Globals._currentCenterTile)
		{
			base.transform.position = Utils.longLatToLocalV3(_geoPosition, Globals._currentCenterTile);
			_lastCenterTile = Globals._currentCenterTile;
		}
		if (Disabled)
		{
			return;
		}
		CalculateGeoPosition();
		Heading.Value = getHeading();
		Velocity.Value = getVelocityInKnots();
		Altitude.Value = getHeightInFeet();
		if (_ai != null)
		{
			_ai.OnFixedUpdate();
		}
		if (_obp != null)
		{
			foreach (BaseSystem system in _obp._systems)
			{
				_ = system._debugCheckModuleNameLoad;
				system.OnFixedUpdate();
			}
			if (IsAirUnit)
			{
				if (_playingCodeAnims)
				{
					UpdateCodeAnims();
				}
				if (IsDestroyed && GameTime.missionElapsedTime > _spawnTime)
				{
					if (this is Aircraft aircraft)
					{
						_spawnTime = GameTime.missionElapsedTime + (double)aircraft.Ap._pilotEjectDelay;
						if (!string.IsNullOrEmpty(aircraft.Ap._pilotIniName) && aircraft.Ap._pilotPositions.Count > 0)
						{
							Vector3 rotation = aircraft.Ap._pilotRotations[0];
							Vector3 offset = aircraft.Ap._pilotPositions[0];
							aircraft.Ap._pilotPositions.RemoveAt(0);
							if (aircraft.Ap._pilotRotations.Count > 1)
							{
								aircraft.Ap._pilotRotations.RemoveAt(0);
							}
							if (!_externalDestructionNotified || Globals._rnd.NextDouble() < (double)aircraft.Ap._pilotEjectChance)
							{
								SpawnPilot(aircraft.Ap._pilotIniName, offset, rotation);
							}
						}
					}
					else
					{
						Helicopter helicopter = this as Helicopter;
						if (!string.IsNullOrEmpty(helicopter._hp._pilotIniName) && helicopter._hp._pilotPositions.Count > 0)
						{
							_spawnTime = GameTime.missionElapsedTime + (double)helicopter._hp._pilotEjectDelay;
							Vector3 rotation = ((helicopter._hp._pilotRotations.Count > 0) ? helicopter._hp._pilotRotations[0] : new Vector3(90f, 0f, 0f));
							Vector3 offset = helicopter._hp._pilotPositions[0];
							helicopter._hp._pilotPositions.RemoveAt(0);
							if (helicopter._hp._pilotRotations.Count > 1)
							{
								helicopter._hp._pilotRotations.RemoveAt(0);
							}
							if (!_externalDestructionNotified || Globals._rnd.NextDouble() < (double)helicopter._hp._pilotEjectChance)
							{
								SpawnPilot(helicopter._hp._pilotIniName, offset, rotation);
							}
						}
					}
				}
			}
		}
		if (GetNumberOfWaypoints() == 0 && AutomateRoute)
		{
			int numberOfWpt = Globals._rnd.Next(3, 6);
			CreateRandomWaypointsPattern(numberOfWpt, 3f);
		}
		if (_rigidBodyTurnedOff || !(_turnOffRigidbodyTime > 0.0))
		{
			return;
		}
		if (base.transform.position.y < TerrainHeightInUnity)
		{
			DesiredAltitude.Value = TerrainUtils.GetHeightAt(_geoPosition);
			if (this is LandUnit landUnit)
			{
				DesiredAltitude.Value += landUnit._groundOffset;
			}
			base.transform.position = new Vector3(base.transform.position.x, DesiredAltitude.Value, base.transform.position.z);
			SetTeleportProtection();
			_rigidBodyTurnedOff = true;
			RigidBody.isKinematic = true;
		}
		else
		{
			if (!(GameTime.missionElapsedTime > _turnOffRigidbodyTime))
			{
				return;
			}
			if (RigidBody == null)
			{
				_rigidBodyTurnedOff = true;
			}
			else if (Compartments != null)
			{
				bool flag = true;
				if (base.transform.position.y > -4f)
				{
					FloodingCompartment[] portCompartments = Compartments._portCompartments;
					for (int i = 0; i < portCompartments.Length; i++)
					{
						if (portCompartments[i].CurrentFloodingPercent < 50f)
						{
							flag = false;
							break;
						}
					}
					if (flag)
					{
						portCompartments = Compartments._starboardCompartments;
						for (int i = 0; i < portCompartments.Length; i++)
						{
							if (portCompartments[i].CurrentFloodingPercent < 50f)
							{
								flag = false;
								break;
							}
						}
					}
				}
				if (flag)
				{
					_rigidBodyTurnedOff = true;
					RigidBody.isKinematic = true;
				}
			}
			else
			{
				_rigidBodyTurnedOff = true;
			}
		}
	}

	protected void CalculateGeoPosition()
	{
		double num = 0.2f * (base.transform.position.x / 330.71396f + (float)(int)Globals._currentCenterTile.x + 0.5f) + 1f / 120f;
		double num2 = num;
		if (num > 360.0)
		{
			num = 360.0;
		}
		double num3 = 0.2f * ((0f - base.transform.position.z) / 330.71396f + (float)(int)Globals._currentCenterTile.y + 0.5f) + 1f / 120f;
		GeoPosition geoPosition = default(GeoPosition);
		geoPosition._longitude = num - 180.0;
		if (geoPosition._longitude < -180.0)
		{
			geoPosition._longitude += 360.0;
		}
		if (geoPosition._longitude > 180.0)
		{
			geoPosition._longitude -= 360.0;
		}
		geoPosition._latitude = 0.0 - (num3 - 90.0);
		if (geoPosition._latitude < -90.0)
		{
			geoPosition._latitude += 180.0;
		}
		if (geoPosition._latitude > 90.0)
		{
			geoPosition._latitude -= 180.0;
		}
		geoPosition._height = base.transform.position.y;
		if (double.IsNaN(geoPosition._longitude) || double.IsInfinity(geoPosition._longitude) || double.IsNaN(geoPosition._latitude) || double.IsInfinity(geoPosition._latitude) || geoPosition._longitude < -180.0 || geoPosition._longitude > 180.0 || geoPosition._latitude < -90.0 || geoPosition._latitude > 90.0)
		{
			if (!_hasLoggedInvalidGeoPosition)
			{
				_hasLoggedInvalidGeoPosition = true;
				Vector3 vector = ((RigidBody != null) ? RigidBody.position : Vector3.zero);
				Vector3 vector2 = ((RigidBody != null) ? RigidBody.linearVelocity : Vector3.zero);
				Vector3 vector3 = ((_lastValidGeoPositionFrame >= 0) ? (base.transform.position - _lastValidGeoTransformPosition) : Vector3.zero);
				string text = (CommandPosition.HasValue ? CommandPosition.Value.ToString() : "<none>");
				string text2 = ((_currentWaypointParent != null) ? _currentWaypointParent.FullIDData() : "<none>");
				string text3 = ((_homeBase != null) ? _homeBase.FullIDData() : "<none>");
				string text4 = ((StateMachine != null) ? StateMachine.CurrentStateName : "<none>");
				int numberOfWaypoints = GetNumberOfWaypoints();
				string arg = ((numberOfWaypoints > 0) ? GetCurrentActiveWaypointName() : "<none>");
				string arg2 = ((numberOfWaypoints > 0) ? GetCurrentActiveWaypointGeoPos().ToString() : "<none>");
				string text5 = $"({base.transform.position.x:R}, {base.transform.position.y:R}, {base.transform.position.z:R})";
				string text6 = ((RigidBody != null) ? $"({vector.x:R}, {vector.y:R}, {vector.z:R})" : "<none>");
				string text7 = ((RigidBody != null) ? $"({vector2.x:R}, {vector2.y:R}, {vector2.z:R})" : "<none>");
				string text8 = $"({_velocityVecInUnity.x:R}, {_velocityVecInUnity.y:R}, {_velocityVecInUnity.z:R})";
				string arg3 = $"({vector3.x:R}, {vector3.y:R}, {vector3.z:R})";
				string text9 = ((_lastValidGeoPositionFrame >= 0) ? $"({_lastValidGeoTransformPosition.x:R}, {_lastValidGeoTransformPosition.y:R}, {_lastValidGeoTransformPosition.z:R})" : "<none>");
				int num4 = ((_lastValidGeoPositionFrame >= 0) ? (Time.frameCount - _lastValidGeoPositionFrame) : (-1));
				UnityEngine.Debug.LogError("ObjectBase.CalculateGeoPosition produced out of bounds geo coordinates for " + FullIDData() + ". " + $"type={_type}, ini={_objectIniName}, rawLongLat={num2 - 180.0:R}/{0.0 - (num3 - 90.0):R}, " + $"calculatedLongLat={geoPosition._longitude:R}/{geoPosition._latitude:R}, " + "transformPosition=" + text5 + ", rigidbodyPosition=" + text6 + ", rigidbodyVelocity=" + text7 + ", objectVelocity=" + text8 + ", " + $"velocityKnots={_velocityInKnots:R}, positionDeltaSinceLastValid={arg3}, " + $"lastValidFrame={_lastValidGeoPositionFrame}, framesSinceLastValid={num4}, " + "lastValidTransformPosition=" + text9 + ", lastValidGeoPosition=" + ((_lastValidGeoPositionFrame >= 0) ? _lastValidCalculatedGeoPosition.ToString() : "<none>") + ", " + $"currentCenterTile={Globals._currentCenterTile}, lastCenterTile={_lastCenterTile}, " + "state=" + text4 + ", subState=" + _subState + ", flightDeckState=" + _flightDeckState + ", commandPosition=" + text + ", " + $"waypointCount={numberOfWaypoints}, activeWaypointName={arg}, activeWaypointPosition={arg2}, " + $"waypointParent={text2}, homeBase={text3}, hasControl={_hasControl}, disabled={Disabled}, " + $"isLanding={isLanding}, performingAirOps={_performingAirOps}, aligning={Aligning}, " + $"gameTime={Time.time:R}, timeCompression={GameTime.TimeCompression:R}, frame={Time.frameCount}.");
			}
		}
		else
		{
			_lastValidGeoPositionFrame = Time.frameCount;
			_lastValidGeoTransformPosition = base.transform.position;
			_lastValidCalculatedGeoPosition = geoPosition;
		}
		_geoPosition = geoPosition;
	}

	private void CheckForMineRemoval()
	{
		if (GameTime.missionElapsedTime < _mineCheckTime)
		{
			return;
		}
		_mineCheckTime = GameTime.missionElapsedTime + (double)_mineCheckInterval;
		if ((Compartments != null && Compartments._isSinking) || _velocityInKnots > 15f || (IsAirUnit && AltitudeOverSurface > 0.2267855f))
		{
			return;
		}
		for (int num = Singleton<ObjectsManager>.Instance._listOfAllMines.Count - 1; num >= 0; num--)
		{
			ObjectBase objectBase = Singleton<ObjectsManager>.Instance._listOfAllMines[num];
			if (!objectBase.IsDestroyed)
			{
				Vector3 position = objectBase.transform.position;
				position.y = base.transform.position.y;
				if (!((position - base.transform.position).magnitude > 2.7559497f))
				{
					objectBase.destroyObject(keepInFlightEffectAlive: false, score: false);
					if (_taskforce == Globals._playerTaskforce)
					{
						Globals._mainGameViewModel.RaiseAlert(this, Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "DisarmedMine"));
					}
				}
			}
		}
	}

	private void CheckForRaftPickUp()
	{
		if (GameTime.missionElapsedTime < _raftPickUpTime)
		{
			return;
		}
		_raftPickUpTime = GameTime.missionElapsedTime + (double)_raftPickUpInterval;
		foreach (KeyValuePair<MeshRenderer, int> transportRenderer in _obp._transportRenderers)
		{
			if (!transportRenderer.Key.enabled && _obp.CurrentTransportedOnBoard.Value >= transportRenderer.Value)
			{
				transportRenderer.Key.enabled = true;
			}
		}
		if (!RaftPickUpAllowed.Value || _obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Deco) || (_obp.CurrentTransportedOnBoard.Value == _obp._transportCapacity && IsAirUnit) || (Compartments != null && Compartments._isSinking) || _velocityInKnots > 13f)
		{
			return;
		}
		if (IsAirUnit)
		{
			if (AltitudeOverSurface > 0.24946405f)
			{
				return;
			}
		}
		else if (this is Submarine && isSubmerged())
		{
			return;
		}
		foreach (ObjectBase listOfAllRaft in Singleton<ObjectsManager>.Instance._listOfAllRafts)
		{
			if (listOfAllRaft.IsDestroyed)
			{
				continue;
			}
			Vector3 position = listOfAllRaft.transform.position;
			position.y = base.transform.position.y;
			float num = ((this is Vessel || this is Submarine) ? 0.3f : 0.15f);
			if ((position - base.transform.position).magnitude > num * 27.559496f)
			{
				continue;
			}
			int num2 = listOfAllRaft._obp.CurrentTransportedOnBoard.Value;
			if (IsAirUnit)
			{
				if (num2 > _obp._transportCapacity - _obp.CurrentTransportedOnBoard.Value)
				{
					num2 = _obp._transportCapacity - _obp.CurrentTransportedOnBoard.Value;
				}
				_obp.CurrentTransportedOnBoard.Value += num2;
			}
			listOfAllRaft._obp.CurrentTransportedOnBoard.Value -= num2;
			_taskforce._survivorsResqued += num2;
			_taskforce._lossScore -= (float)num2 * listOfAllRaft._obp._collectScoreValue;
			if (_taskforce != listOfAllRaft._taskforce)
			{
				_taskforce._lossScore -= (float)num2 * listOfAllRaft._obp._collectScoreValue;
			}
			if (listOfAllRaft._obp.CurrentTransportedOnBoard.Value == 0)
			{
				listOfAllRaft.destroyObject(keepInFlightEffectAlive: false, score: false);
			}
			if (_taskforce == Globals._playerTaskforce)
			{
				Globals._mainGameViewModel.RaiseAlert(this, Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "PickedRaft"));
			}
			break;
		}
	}

	public void SetTeleportProtection()
	{
		_teleportProtection = true;
		Physics.SyncTransforms();
		_geoPosition = Utils.worldPositionFromUnityToLongLat(base.transform.position, Globals._currentCenterTile);
	}

	private void SpawnPilot(string pilotIniName, Vector3 offset, Vector3 rotation)
	{
		ObjectBase objectBase = null;
		objectBase = DevModeUtils.createAmmunitionObjectInstance(new Ammunition(pilotIniName, 0, null), _taskforce, _obp._nation, this).GetComponent<ObjectBase>();
		objectBase.transform.position = base.transform.position + base.transform.rotation * offset;
		objectBase._geoPosition = Utils.worldPositionFromUnityToLongLat(objectBase.transform.position, Globals._currentCenterTile);
		objectBase.transform.rotation = base.transform.rotation * Quaternion.AngleAxis(rotation.x, objectBase.transform.right) * Quaternion.AngleAxis(rotation.y, objectBase.transform.up) * Quaternion.AngleAxis(rotation.z, objectBase.transform.forward);
		if (!(objectBase is Bomb bomb))
		{
			return;
		}
		bomb._skipFirstMoveUpdate = true;
		Vector3 bearingOnlyTargetPosition = bomb.transform.position + bomb.transform.forward * 5f * 27.559496f;
		bearingOnlyTargetPosition.y = 0f;
		bomb._moveOfLaunchPlatform = _velocityVecInUnity;
		bomb.Container_Launch(null, bearingOnlyTargetPosition, Vector3.zero, null);
		if (this is Helicopter)
		{
			if (offset.x < 0f)
			{
				bomb._velocityVecInUnity -= base.transform.right * 15f * 0.0076554087f;
			}
			else
			{
				bomb._velocityVecInUnity += base.transform.right * 15f * 0.0076554087f;
			}
		}
		bool flag = false;
		if (Globals._autoSwitchToLaunchedWeapon == Globals.CameraSwitchTo.Local && Singleton<RenderPosition>.Instance.getSelectedObject() == this)
		{
			flag = true;
		}
		else if (Globals._autoSwitchToLaunchedWeapon == Globals.CameraSwitchTo.Player && IsPlayerObject)
		{
			flag = true;
		}
		else if (Globals._autoSwitchToLaunchedWeapon == Globals.CameraSwitchTo.Enemy && _taskforce == Globals._enemyTaskforce)
		{
			flag = true;
		}
		else if (Globals._autoSwitchToLaunchedWeapon == Globals.CameraSwitchTo.All)
		{
			flag = true;
		}
		if (flag)
		{
			Singleton<ObjectsManager>.Instance.setActiveObject(objectBase);
			Singleton<RenderPosition>.Instance.switchToObject(objectBase, performSmoothMove: false);
		}
	}

	public void Awake()
	{
		Singleton<GameMain>.Instance.addObject(this);
		OnDragDropCompleted = delegate(DependencyObject source, IDataObject originData, UIElement target, Point dropPoint, DragDropEffects effects)
		{
			if (target is FrameworkElement frameworkElement)
			{
				object dataContext = frameworkElement.DataContext;
				if (dataContext is ObjectBase objectBase && originData.GetDataPresent(typeof(ObjectBase)))
				{
					ObjectBase objectBase2 = originData.GetData(typeof(ObjectBase)) as ObjectBase;
					if (objectBase.InFormation.Value)
					{
						objectBase.Formation.AddUnitFromMenu(objectBase2, isLeader: false, "Awake");
					}
					else if (objectBase2 != objectBase)
					{
						UnitFormationParameters unitFormationParameters = new UnitFormationParameters(objectBase._type == ObjectType.Aircraft);
						unitFormationParameters._name = string.Empty;
						unitFormationParameters._leaderObject = objectBase;
						new UnitFormation(unitFormationParameters).AddUnitFromMenu(objectBase2, isLeader: false, "Awake");
					}
				}
				else if (dataContext is UnitFormation unitFormation && originData.GetDataPresent(typeof(ObjectBase)))
				{
					ObjectBase obj = originData.GetData(typeof(ObjectBase)) as ObjectBase;
					unitFormation.AddUnitFromMenu(obj, isLeader: false, "Awake");
				}
				else if (dataContext is Station station && originData.GetDataPresent(typeof(ObjectBase)))
				{
					ObjectBase obj2 = originData.GetData(typeof(ObjectBase)) as ObjectBase;
					station.ParentFormation?.AddUnitFromMenu(obj2, isLeader: false, "Awake");
				}
			}
			else if (target == null && originData.GetDataPresent(typeof(Station)))
			{
				ObjectBase unitObject = (originData.GetData(typeof(Station)) as Station).UnitObject;
				unitObject.Formation.DetachUnit(unitObject);
			}
		};
	}

	public virtual void OnStart()
	{
		_objectForward = Vector3.zero;
		_objectRight = Vector3.zero;
		_objectUp = Vector3.zero;
		_toTargetDirection = Vector3.zero;
		_toTargetForward = Vector3.zero;
		_mainCam = CameraManager.Main;
		CurrentOrder.Value = new Order(this);
		_camLookAroundActive = false;
		_lastCameraRotation = Singleton<RenderPosition>.Instance.getRotation();
		_lastCenterTile = Globals._currentCenterTile;
		UpdatePosition = new ReadOnlyReactiveProperty<GeoPosition>(Observable.EveryUpdate().WithLatestFrom(Position, (long t, GeoPosition gp) => gp), GeoPositionComparer.Instance);
		UpdateHeading = new ReadOnlyReactiveProperty<float>(Observable.EveryUpdate().WithLatestFrom(Heading, (long t, float gp) => gp));
		UpdateVelocity = new ReadOnlyReactiveProperty<float>(Observable.EveryUpdate().WithLatestFrom(Velocity, (long t, float gp) => gp));
		Type type = GetType();
		if (type == typeof(Vessel))
		{
			_type = ObjectType.Vessel;
		}
		else if (type == typeof(Submarine))
		{
			_type = ObjectType.Submarine;
		}
		else if (type == typeof(Aircraft))
		{
			_type = ObjectType.Aircraft;
		}
		else if (type == typeof(Helicopter))
		{
			_type = ObjectType.Helicopter;
		}
		else if (type == typeof(LandUnit))
		{
			_type = ObjectType.LandUnit;
		}
		else if (type == typeof(Biologic))
		{
			_type = ObjectType.Biologic;
		}
		else if (type == typeof(Missile))
		{
			_type = ObjectType.Missile;
		}
		else if (type == typeof(Torpedo))
		{
			_type = ObjectType.Torpedo;
		}
		else if (type == typeof(ASROC))
		{
			_type = ObjectType.ASROC;
		}
		else if (type == typeof(RBU))
		{
			_type = ObjectType.RBU;
		}
		else if (type == typeof(Bomb))
		{
			_type = ObjectType.Bomb;
		}
		else if (type == typeof(UnguidedRocket))
		{
			_type = ObjectType.AerialRocket;
		}
		else if (type == typeof(ChaffCloud))
		{
			_type = ObjectType.Chaff;
		}
		else if (type == typeof(Noisemaker))
		{
			_type = ObjectType.Noisemaker;
		}
		else if (type == typeof(Projectile))
		{
			_type = ObjectType.Projectile;
		}
		else if (type == typeof(WakeBubble))
		{
			_type = ObjectType.Wakebubble;
		}
		else
		{
			_type = ObjectType.Other;
		}
		if (isUnit())
		{
			_ai = new AI(this);
			if (this is Vessel || this is Submarine || this is Biologic)
			{
				CollisionAvoidance = new ObjectCollisionAvoidance(this);
			}
		}
		Info = new ObjectBaseInfo(this);
		if (!_hasBeenInstantiated)
		{
			OnInstantiated();
			_hasBeenInstantiated = true;
		}
	}

	public void setInitialized()
	{
		_canUpdate = true;
		PublishKinematics();
	}

	public void Enable()
	{
		if (Disabled)
		{
			Disabled = false;
			HasControl = true;
			_taskforce._taskforceDisabledUnits.Remove(this);
			Singleton<ObjectsManager>.Instance.AddObjectToItsTaskforce(this);
			if (_obp?._rootGameObject != null)
			{
				_obp._rootGameObject.SetActive(value: true);
			}
			EnableEntity();
		}
	}

	public void Disable()
	{
		if (!Disabled)
		{
			Disabled = true;
			HasControl = false;
			Singleton<ObjectsManager>.Instance.removeObject(this);
			_taskforce.RemoveTaskForceObject(this);
			if (!_taskforce._taskforceDisabledUnits.Contains(this))
			{
				_taskforce._taskforceDisabledUnits.Add(this);
			}
			if (_obp?._rootGameObject != null)
			{
				_obp._rootGameObject.SetActive(value: false);
			}
			DisableEntity();
		}
	}

	private void OnDestroy()
	{
		_consumer.Dispose();
		_disposer.Dispose();
		Position?.Dispose();
		Heading?.Dispose();
		UpdateHeading?.Dispose();
		Velocity?.Dispose();
		UpdateVelocity?.Dispose();
		CurrentOrder?.Dispose();
		CurrentOrderText?.Dispose();
		OrderFeedbackText?.Dispose();
		IdentifyAir?.Dispose();
		IdentifySubSurface?.Dispose();
		IdentifySurface?.Dispose();
		EngageAir?.Dispose();
		EngageSubSurface?.Dispose();
		EngageSurface?.Dispose();
		Emcon?.Dispose();
		IsDormant?.Dispose();
		Identified?.Dispose();
		UpdatePosition?.Dispose();
		IncomingEmissionsInfo?.Dispose();
		IncomingSignalInfo?.Dispose();
		RangeOnMap?.Dispose();
		if (_obp != null && _obp._systems != null)
		{
			foreach (BaseSystem system in _obp._systems)
			{
				system?.dispose();
			}
		}
		DestroyDetachedModelParts();
		_obp?.DestroyOwnedMaterials();
		_userRoot?.DisposeTaskViewModels();
		ReleaseEntityObject();
	}

	protected virtual void DestroyDetachedModelParts()
	{
	}

	public bool isDangerous(ObjectBase target, Vehicle threatVehicle)
	{
		if (target == null)
		{
			return false;
		}
		if (target.IsDestroyed)
		{
			return false;
		}
		if (this is Missile missile)
		{
			if ((target is Vessel || target is Submarine) && missile._isTargetAirborne)
			{
				return false;
			}
			if (missile.CurrentTarget == target)
			{
				return true;
			}
			if (missile.CurrentTarget != null && target.Formation != null && target is Vessel)
			{
				foreach (Station station in target.Formation.Stations)
				{
					if (missile.CurrentTarget == station.UnitObject)
					{
						return true;
					}
				}
			}
			if ((missile._seeker._strongestSensorEcho != null || missile.CurrentIntendedTargetObject != target) && _taskforce == target._taskforce && !missile._wasSpoofed)
			{
				return false;
			}
			if (target.IsAirUnit && missile._ap._targetType != Ammunition.Target.AAW && missile._ap._secondaryTargetType != Ammunition.Target.AAW)
			{
				return false;
			}
			if (target is Submarine submarine && submarine.isSubmerged())
			{
				return false;
			}
			float num = ((missile._ap._seekerActiveRange > missile._ap._seekerPassiveRange) ? missile._ap._seekerActiveRange : missile._ap._seekerPassiveRange);
			Vector3 lhs = target.transform.position - missile.transform.position;
			float magnitude = lhs.magnitude;
			if (Vector3.Dot(lhs, missile.transform.forward) < 0.86603f * magnitude)
			{
				return false;
			}
			if (missile._taskforce == _taskforce && !missile._seeker.isActive())
			{
				return false;
			}
			if (magnitude <= num)
			{
				return true;
			}
			float num2 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(lhs, missile.transform.forward) / magnitude, -1f, 1f)) * 57.29578f;
			float y = num * 0.5f;
			float x = magnitude - num * 0.13397f;
			float num3 = Mathf.Atan2(y, x) * 57.29578f;
			if (num2 > num3)
			{
				return false;
			}
			return true;
		}
		if (_type == ObjectType.Torpedo)
		{
			Torpedo torpedo = (Torpedo)this;
			if (base.transform.position.y > 0f)
			{
				return false;
			}
			if (torpedo._ap._subType == Ammunition.Type.MOSS)
			{
				return false;
			}
			SeekerBase seeker = torpedo.getSeeker();
			SensorEcho strongestSensorEcho = seeker._strongestSensorEcho;
			if (strongestSensorEcho != null && strongestSensorEcho._objectBase == target)
			{
				return true;
			}
			if (seeker._lockedOnTarget == target)
			{
				return true;
			}
			if (torpedo._onWire && torpedo._taskforce == target._taskforce)
			{
				return false;
			}
			if (torpedo._ap._targetType != Ammunition.Target.ASuW && torpedo._ap._secondaryTargetType != Ammunition.Target.ASuW)
			{
				if (!(target is Submarine submarine2))
				{
					return false;
				}
				if (!submarine2.isSubmerged())
				{
					return false;
				}
			}
			if (torpedo._ap._targetType != Ammunition.Target.ASW && torpedo._ap._secondaryTargetType != Ammunition.Target.ASW && target is Submarine submarine3 && submarine3.isSubmerged())
			{
				return false;
			}
			bool flag = false;
			bool flag2 = false;
			switch (torpedo.getFlightStage())
			{
			case WeaponBase.FlightStage.PerformSnakeSearch:
				flag2 = true;
				break;
			case WeaponBase.FlightStage.EnterSpiralSearch:
			case WeaponBase.FlightStage.SpiralSearchDown:
			case WeaponBase.FlightStage.SpiralSearchUp:
			case WeaponBase.FlightStage.EnterCircleSearch:
			case WeaponBase.FlightStage.PerformCircleSearch:
			case WeaponBase.FlightStage.Reattack:
			case WeaponBase.FlightStage.ReattackCircle:
				if (torpedo._ap._reattackMode == AmmunitionParameters.ReattackMode.Circle)
				{
					flag = true;
				}
				break;
			}
			float num4 = ((torpedo._ap._seekerActiveRange > torpedo._ap._seekerPassiveRange) ? torpedo._ap._seekerActiveRange : torpedo._ap._seekerPassiveRange);
			float magnitude2 = (getUnityPosition() - target.getUnityPosition()).magnitude;
			if (flag && magnitude2 <= num4 + 0.4f)
			{
				return true;
			}
			Vector3 from = target.transform.position - torpedo.transform.position;
			float num5 = Vector3.Angle(from, torpedo.transform.forward);
			float num6 = torpedo._ap._seekerFOV / 2f;
			num6 += 5f;
			if (flag2)
			{
				num6 += torpedo._ap._snakeSearchAngle;
				Vector3 to = Quaternion.Euler(0f, torpedo.EstimatedTargetHeading, 0f) * Vector3.forward;
				num5 = Vector3.Angle(from, to);
			}
			if (num5 > num6)
			{
				return false;
			}
			if (magnitude2 <= num4)
			{
				return true;
			}
			float y2 = num4 * Mathf.Sin(num6 * (MathF.PI / 180f));
			float x2 = magnitude2 - num4 * (1f - Mathf.Cos(num6 * (MathF.PI / 180f)));
			float num7 = Mathf.Atan2(y2, x2) * (180f / MathF.PI);
			if (num5 > num7)
			{
				float num8 = (torpedo.transform.position - target.transform.position).magnitude / torpedo._velocityInUnity;
				Vector3 from2 = target.transform.position + target._velocityInUnity * num8 * target.transform.forward - torpedo.transform.position;
				float num9 = Vector3.Angle(from2, torpedo.transform.forward);
				if (flag2)
				{
					Vector3 to2 = Quaternion.Euler(0f, torpedo.EstimatedTargetHeading, 0f) * Vector3.forward;
					num9 = Vector3.Angle(from2, to2);
				}
				if (num9 > num7)
				{
					return false;
				}
			}
			return true;
		}
		if (_type == ObjectType.Aircraft)
		{
			Aircraft aircraft = (Aircraft)this;
			if (target._taskforce.RelationshipTo(threatVehicle.UnitTaskforce.Value) != RelationsState.Hostile)
			{
				return false;
			}
			Vector3 from3 = target.transform.position - aircraft.transform.position;
			from3.y = 0f;
			Vector3 forward = aircraft.transform.forward;
			forward.y = 0f;
			float num10 = Vector3.Angle(from3, forward);
			float magnitude3 = from3.magnitude;
			float num11 = 15f;
			float num12 = 275.59497f;
			float y3 = num12 * Mathf.Sin(num11 * (MathF.PI / 180f));
			float x3 = magnitude3 - num12 * (1f - Mathf.Cos(num11 * (MathF.PI / 180f)));
			float num13 = Mathf.Atan2(y3, x3) * (180f / MathF.PI);
			if (num10 > num13)
			{
				return false;
			}
			return true;
		}
		return false;
	}

	public bool TracksIncomingWeapons()
	{
		if (_incomingWeaponsMode < 0)
		{
			if (_obp == null)
			{
				return EvaluateIncomingWeaponsEligibility();
			}
			_incomingWeaponsMode = ResolveIncomingWeaponsMode();
		}
		if (_incomingWeaponsMode == 0)
		{
			return false;
		}
		if (_incomingWeaponsMode == 1)
		{
			return true;
		}
		WeaponBase weaponBase = (WeaponBase)this;
		if (weaponBase._ap != null && weaponBase.isLaunched())
		{
			return weaponBase._ap._subType != Ammunition.Type.Sonobuoy;
		}
		return false;
	}

	private sbyte ResolveIncomingWeaponsMode()
	{
		if (_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Deco))
		{
			return 0;
		}
		if (_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SeaMine))
		{
			return 0;
		}
		bool flag = this is WeaponBase;
		if (!isUnit() && !flag)
		{
			return 0;
		}
		if (!flag)
		{
			return 1;
		}
		if (this is Noisemaker)
		{
			return 0;
		}
		if (this is ChaffCloud)
		{
			return 0;
		}
		if (this is RBU)
		{
			return 0;
		}
		if (this is UnguidedRocket)
		{
			return 0;
		}
		return 2;
	}

	private bool EvaluateIncomingWeaponsEligibility()
	{
		if (!isUnit() && !(this is WeaponBase))
		{
			return false;
		}
		if (this is WeaponBase weaponBase)
		{
			if (weaponBase._ap == null || !weaponBase.isLaunched())
			{
				return false;
			}
			if (this is Noisemaker)
			{
				return false;
			}
			if (this is ChaffCloud)
			{
				return false;
			}
			if (this is RBU)
			{
				return false;
			}
			if (this is UnguidedRocket)
			{
				return false;
			}
			if (weaponBase._ap._subType == Ammunition.Type.Sonobuoy)
			{
				return false;
			}
		}
		return true;
	}

	private void CalculateIncomingThreats()
	{
		if (GameTime.missionElapsedTime - _lastThreatCalculationTime < 0.5)
		{
			return;
		}
		_lastThreatCalculationTime = GameTime.missionElapsedTime;
		if ((_obp != null && (_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Deco) || _obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SeaMine))) || !isUnit())
		{
			return;
		}
		_threats.Clear();
		foreach (KeyValuePair<ObjectBase, Vehicle> item in (!ReactionTime.UsesLocalPlotOnly(this)) ? _taskforce.PlottingTable.LocalVehicles : PlottingTable.LocalVehicles)
		{
			if (!(item.Key == this))
			{
				ObjectBase key = item.Key;
				Vehicle value = item.Value;
				if (!(key == null) && !key.IsDestroyed && (key._type == ObjectType.Missile || key._type == ObjectType.Torpedo || key._type == ObjectType.Bomb || key._type == ObjectType.Aircraft || key._type == ObjectType.Vessel) && (!(key is Bomb bomb) || (bomb._ap._subType != Ammunition.Type.Sonobuoy && bomb._ap._guidanceType != 0)) && key.isDangerous(this, value))
				{
					_threats.Add(key);
				}
			}
		}
		if (_taskforce != Globals._playerTaskforce && _ai != null)
		{
			_ai.CheckForRaiseAlert();
		}
	}

	public string getDebugIncomingThreatsNames()
	{
		if (_threats == null || _threats.Count < 1)
		{
			return "None";
		}
		string text = "";
		foreach (ObjectBase threat in _threats)
		{
			text = text + threat.getUIDAndName() + " ";
		}
		return text;
	}

	public bool HasObservedIdentifiedInboundWeapon()
	{
		if (_taskforce == null || _incomingWeapons == null)
		{
			return false;
		}
		foreach (WeaponBase incomingWeapon in _incomingWeapons)
		{
			if (!(incomingWeapon == null) && !incomingWeapon.IsDestroyed && incomingWeapon.isLaunched())
			{
				Vehicle mapVehicle = incomingWeapon.GetMapVehicle(_taskforce);
				if (mapVehicle != null && mapVehicle.Identified.Value && mapVehicle.CurrentRelationship() == RelationsState.Hostile)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool CanAutomaticallyBreakForcedEmcon()
	{
		if (_ai != null && _ai._excludeFromAIRadarRoutine)
		{
			return HasObservedIdentifiedInboundWeapon();
		}
		return true;
	}

	public string GetDebugIncomingWeaponsNames()
	{
		if (_incomingWeapons == null || _incomingWeapons.Count < 1)
		{
			return "None";
		}
		string text = "";
		foreach (WeaponBase incomingWeapon in _incomingWeapons)
		{
			text = text + incomingWeapon.getUIDAndName() + " ";
		}
		return text;
	}

	public void SetWeaponStatus(WeaponStatus status, bool formationCheck = false)
	{
		bool flag = false;
		if (formationCheck && FollowingFormation)
		{
			if (Formation.SelectedControlMode == UnitFormation.ControlMode.Synchronized)
			{
				flag = true;
			}
			else if (Formation.SelectedControlMode == UnitFormation.ControlMode.FollowLeader && IsFormationLeader)
			{
				flag = true;
			}
		}
		if (flag)
		{
			foreach (Station station in Formation.Stations)
			{
				ObjectBase objectBase = station?.UnitObject;
				if (!(objectBase == null))
				{
					objectBase._weaponStatus = status;
				}
			}
			return;
		}
		_weaponStatus = status;
	}

	public void CheckForPlayerAbort()
	{
		if (IsPlayerObject && _weaponStatus == WeaponStatus.Free)
		{
			_weaponStatus = WeaponStatus.Tight;
			CeaseFire(report: false, clearOrder: true, formationCheck: false, stopJamming: false);
		}
	}

	public virtual void CeaseFire(bool report = true, bool clearOrder = true, bool formationCheck = true, bool stopJamming = true, bool airFormationCheck = true, bool clearAttackTasks = true)
	{
		if (!isUnit())
		{
			return;
		}
		_ai._objectToDestroy = null;
		_ai._objectsToDestroyList.Clear();
		if (report)
		{
			string text = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "CeaseFire");
			Globals._mainGameViewModel.RaiseAlert(this, text);
			Singleton<VoiceMessage>.Instance.PlayVoiceMessage("CeaseFire", this);
		}
		if (airFormationCheck && IsFormationLeader && IsAirUnit && Formation != null)
		{
			bool flag = Input.GetKey("left shift") || Input.GetKey("right shift");
			foreach (Station station in Formation.Stations)
			{
				ObjectBase objectBase = station?.UnitObject;
				if (objectBase == null || objectBase == this)
				{
					continue;
				}
				if (flag)
				{
					objectBase.CeaseFire(report: false);
					Formation.ReturnToFormation(objectBase);
				}
				else
				{
					if (!objectBase.FollowingFormation)
					{
						continue;
					}
					bool flag2 = false;
					foreach (AlignUnitToTarget alignmentTask in objectBase._alignmentTasks)
					{
						if (!alignmentTask._isFormationAttack)
						{
							flag2 = true;
							break;
						}
					}
					if (!flag2)
					{
						objectBase.CeaseFire(report: false);
					}
				}
			}
		}
		if (formationCheck && Globals._passOrdersToFormationMembers && AcceptsOrdersFromPlayer && FollowingFormation && Formation != null)
		{
			foreach (Station station2 in Formation.Stations)
			{
				ObjectBase objectBase2 = station2?.UnitObject;
				if (!(objectBase2 == null) && !(objectBase2 == this) && objectBase2.FollowingFormation)
				{
					objectBase2.CeaseFire(report: false, clearOrder: true, formationCheck: false);
				}
			}
			return;
		}
		DM.I(FullIDData() + ": <color=green>CEASE FIRE</color>");
		if (clearOrder)
		{
			setOrder(Order.Type.None, null, displayOrderText: true);
		}
		ClearEngageTasks();
		if (stopJamming)
		{
			CeaseOffensiveJamming();
		}
		ceaseLaserGuidance();
		if (clearAttackTasks)
		{
			ClearAttackTasks();
		}
	}

	protected void ceaseLaserGuidance()
	{
		if (_obp == null)
		{
			return;
		}
		foreach (SensorSystem sensorSystem in _obp._sensorSystems)
		{
			if (sensorSystem is SensorSystemLaserDesignator)
			{
				sensorSystem._associatedTargets.Clear();
			}
		}
	}

	protected virtual void CeaseOffensiveJamming()
	{
		if (_obp == null)
		{
			return;
		}
		foreach (SensorSystem sensorSystem in _obp._sensorSystems)
		{
			if (sensorSystem is SensorSystemECM sensorSystemECM)
			{
				sensorSystemECM._ecm.ceaseOffensiveJam();
			}
		}
	}

	public void ClearEngageTasksKeepGuiding()
	{
		_currentEngageTasks.Clear();
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			weaponSystem.ClearEngageTask();
		}
		for (int num = _alignmentTasks.Count - 1; num >= 0; num--)
		{
			if (!_alignmentTasks[num]._isGuidingWeapon)
			{
				EndAlignment(_alignmentTasks[num]._uid, "ClearEngageTasksKeepGuiding");
			}
		}
	}

	public void ClearEngageTasks()
	{
		_currentEngageTasks.Clear();
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			weaponSystem.ClearEngageTask();
		}
		EndAllAlignment("ClearEngageTasks");
	}

	public bool AnyWeaponHasAmmo(string ammoId)
	{
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			if (weaponSystem.getMagazineAmmoCount(ammoId) > 0 || weaponSystem.getLoadedAmmoCount(ammoId) > 0)
			{
				return true;
			}
		}
		return false;
	}

	public void RemoveEngageTask(EngageTask task)
	{
		int num = _currentEngageTasks.IndexOf(task);
		if (num >= 0)
		{
			_currentEngageTasks.RemoveAt(num);
		}
	}

	public void RemoveEngageTask(int index)
	{
		_currentEngageTasks.RemoveAt(index);
	}

	public int HeldTrackCount()
	{
		FillEngagedContacts();
		if (_ai?._processedObjects != null)
		{
			foreach (ObjectBase processedObject in _ai._processedObjects)
			{
				if (!(processedObject == null) && !processedObject.IsDestroyed && processedObject.isAirTarget() && !_ai._automaticOnlyTargets.Contains(processedObject.UniqueID))
				{
					_engagedContactScratch.Add(processedObject);
				}
			}
		}
		return CountAirIn(_engagedContactScratch);
	}

	public int EngagedAirContactCount()
	{
		FillEngagedContacts();
		return CountAirIn(_engagedContactScratch);
	}

	public int EngagedContactCount()
	{
		FillEngagedContacts();
		return _engagedContactScratch.Count;
	}

	public HashSet<ObjectBase> CurrentEngagedContacts()
	{
		FillEngagedContacts();
		return _engagedContactScratch;
	}

	private static int CountAirIn(HashSet<ObjectBase> contacts)
	{
		int num = 0;
		foreach (ObjectBase contact in contacts)
		{
			if (contact != null && contact.isAirTarget())
			{
				num++;
			}
		}
		return num;
	}

	private void FillEngagedContacts()
	{
		_engagedContactScratch.Clear();
		foreach (EngageTask currentEngageTask in _currentEngageTasks)
		{
			if (currentEngageTask?._targetObject != null)
			{
				_engagedContactScratch.Add(currentEngageTask._targetObject);
			}
		}
		if (_obp?._weaponSystems == null)
		{
			return;
		}
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			if (weaponSystem == null || (weaponSystem._vwp != null && (weaponSystem._vwp._worksStandalone || weaponSystem._vwp._automatic)))
			{
				continue;
			}
			if (weaponSystem._takenEngageTask?._targetObject != null)
			{
				_engagedContactScratch.Add(weaponSystem._takenEngageTask._targetObject);
			}
			else if (weaponSystem._executingEngageTask && weaponSystem._targetObject != null)
			{
				_engagedContactScratch.Add(weaponSystem._targetObject);
			}
			if (weaponSystem._launchedWeapons == null)
			{
				continue;
			}
			foreach (WeaponBase launchedWeapon in weaponSystem._launchedWeapons)
			{
				if (!(launchedWeapon == null) && !launchedWeapon.IsDestroyed)
				{
					ObjectBase currentIntendedTargetObject = launchedWeapon.CurrentIntendedTargetObject;
					if (currentIntendedTargetObject != null && !currentIntendedTargetObject.IsDestroyed)
					{
						_engagedContactScratch.Add(currentIntendedTargetObject);
					}
				}
			}
		}
	}

	public static float ClosingRateBetween(ObjectBase observer, ObjectBase target)
	{
		if (!(target == null))
		{
			return ClosingRateBetween(observer, target, target.transform.position);
		}
		return 0f;
	}

	public static float ClosingRateBetween(ObjectBase observer, ObjectBase target, Vector3 targetPosition)
	{
		if (observer == null || target == null)
		{
			return 0f;
		}
		Vector3 vector = observer.transform.position - targetPosition;
		float magnitude = vector.magnitude;
		if (magnitude <= 1f)
		{
			return 0f;
		}
		return Vector3.Dot(target._velocityVecInUnity - observer._velocityVecInUnity, vector / magnitude);
	}

	public static bool IsOperableMount(WeaponSystem ws)
	{
		if (ws?._vwp?._associatedSensors != null && !ws.Inoperable.Value)
		{
			return !ws._isOffline;
		}
		return false;
	}

	public static bool IsOperableDirector(SensorSystem director)
	{
		if (director != null)
		{
			return !director.Inoperable.Value;
		}
		return false;
	}

	public int FreeSolutionChannels()
	{
		if (_obp?._weaponSystems == null)
		{
			return 0;
		}
		_capacityDirectorScratch.Clear();
		int num = 0;
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			if (!IsOperableMount(weaponSystem))
			{
				continue;
			}
			foreach (SensorSystem associatedSensor in weaponSystem._vwp._associatedSensors)
			{
				if (IsOperableDirector(associatedSensor) && _capacityDirectorScratch.Add(associatedSensor))
				{
					int totalTargetChannels = associatedSensor.TotalTargetChannels;
					if (totalTargetChannels > 0)
					{
						num += Mathf.Max(0, totalTargetChannels - PendingSolutionCount(associatedSensor));
					}
				}
			}
		}
		return num;
	}

	public int TotalSolutionChannels()
	{
		if (_obp?._weaponSystems == null)
		{
			return 0;
		}
		_totalChannelScratch.Clear();
		int num = 0;
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			if (!IsOperableMount(weaponSystem))
			{
				continue;
			}
			foreach (SensorSystem associatedSensor in weaponSystem._vwp._associatedSensors)
			{
				if (IsOperableDirector(associatedSensor) && _totalChannelScratch.Add(associatedSensor))
				{
					num += Mathf.Max(0, associatedSensor.TotalTargetChannels);
				}
			}
		}
		return num;
	}

	public bool HasSolutionCapacityForTarget(ObjectBase targetObject)
	{
		if (targetObject == null || _obp?._weaponSystems == null)
		{
			return false;
		}
		_capacityDirectorScratch.Clear();
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			if (!IsOperableMount(weaponSystem))
			{
				continue;
			}
			foreach (SensorSystem associatedSensor in weaponSystem._vwp._associatedSensors)
			{
				if (!IsOperableDirector(associatedSensor) || !_capacityDirectorScratch.Add(associatedSensor))
				{
					continue;
				}
				int totalTargetChannels = associatedSensor.TotalTargetChannels;
				if (totalTargetChannels > 0)
				{
					if (HeldSolutionStart(associatedSensor, targetObject) >= 0.0)
					{
						return true;
					}
					if (PendingSolutionCount(associatedSensor) < totalTargetChannels)
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private double HeldSolutionStart(SensorSystem director, ObjectBase target)
	{
		double num = -1.0;
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			EngageTask engageTask = weaponSystem?._takenEngageTask;
			if (engageTask != null && engageTask._solutionDirector == director && !(engageTask._targetObject != target) && engageTask._solutionStartTime >= 0.0 && (num < 0.0 || engageTask._solutionStartTime < num))
			{
				num = engageTask._solutionStartTime;
			}
		}
		foreach (EngageTask currentEngageTask in _currentEngageTasks)
		{
			if (currentEngageTask != null && currentEngageTask._solutionDirector == director && !(currentEngageTask._targetObject != target) && currentEngageTask._solutionStartTime >= 0.0 && (num < 0.0 || currentEngageTask._solutionStartTime < num))
			{
				num = currentEngageTask._solutionStartTime;
			}
		}
		if (num >= 0.0)
		{
			return num;
		}
		if (IsTrackedBy(director, target))
		{
			return GameTime.missionElapsedTime - 3600.0;
		}
		foreach (SensorSystem targetTrackingSystem in director._targetTrackingSystems)
		{
			if (targetTrackingSystem != null && IsTrackedBy(targetTrackingSystem, target))
			{
				return GameTime.missionElapsedTime - 3600.0;
			}
		}
		return -1.0;
	}

	private static bool IsTrackedBy(SensorSystem sensor, ObjectBase target)
	{
		if (sensor._preLaunchTargetObject == target)
		{
			return true;
		}
		if (sensor._associatedTargets != null)
		{
			return sensor._associatedTargets.Contains(target);
		}
		return false;
	}

	private int PendingSolutionCount(SensorSystem director)
	{
		_solutionTargetScratch.Clear();
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			EngageTask engageTask = weaponSystem?._takenEngageTask;
			if (engageTask != null && engageTask._solutionDirector == director && engageTask._targetObject != null)
			{
				_solutionTargetScratch.Add(engageTask._targetObject);
			}
		}
		foreach (EngageTask currentEngageTask in _currentEngageTasks)
		{
			if (currentEngageTask != null && currentEngageTask._solutionDirector == director && currentEngageTask._targetObject != null)
			{
				_solutionTargetScratch.Add(currentEngageTask._targetObject);
			}
		}
		return _solutionTargetScratch.Count;
	}

	private void UpdateSolutionChannel(EngageTask task, List<WeaponSystem> candidates, AmmunitionParameters ap)
	{
		if (task._solutionChannelOwner != null && (task._solutionChannelOwner.Inoperable.Value || task._solutionChannelOwner._isOffline || task._solutionDirector == null || task._solutionDirector.Inoperable.Value))
		{
			task._solutionChannelOwner = null;
			task._solutionDirector = null;
			task._solutionStartTime = -1.0;
		}
		if (task._solutionChannelOwner == null && !(task._targetObject == null) && !IsAirUnit && (ap == null || ap._requiresGuidance) && !(GameTime.missionElapsedTime < task._nextSolutionClaimTime) && !TryClaimSolutionChannel(task, candidates, ap, heldOnly: true) && !TryClaimSolutionChannel(task, candidates, ap, heldOnly: false))
		{
			task._nextSolutionClaimTime = GameTime.missionElapsedTime + 1.0;
		}
	}

	private bool TryClaimSolutionChannel(EngageTask task, List<WeaponSystem> candidates, AmmunitionParameters ap, bool heldOnly)
	{
		_claimedDirectorScratch.Clear();
		foreach (WeaponSystem candidate in candidates)
		{
			if (!IsOperableMount(candidate))
			{
				continue;
			}
			foreach (SensorSystem associatedSensor in candidate._vwp._associatedSensors)
			{
				if (!IsOperableDirector(associatedSensor) || !associatedSensor.CanGuideAmmo(ap) || !_claimedDirectorScratch.Add(associatedSensor))
				{
					continue;
				}
				int totalTargetChannels = associatedSensor.TotalTargetChannels;
				if (totalTargetChannels > 0)
				{
					double num = HeldSolutionStart(associatedSensor, task._targetObject);
					if (!(num < 0.0) || (!heldOnly && PendingSolutionCount(associatedSensor) < totalTargetChannels))
					{
						task._solutionChannelOwner = candidate;
						task._solutionDirector = associatedSensor;
						task._solutionStartTime = ((num >= 0.0) ? num : GameTime.missionElapsedTime);
						return true;
					}
				}
			}
		}
		return false;
	}

	private bool AnySystemExecutingEngageTask(int taskUID)
	{
		if (_obp?._weaponSystems == null)
		{
			return false;
		}
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			if (weaponSystem != null && weaponSystem._executingEngageTask && weaponSystem._alignmentUID == taskUID)
			{
				return true;
			}
		}
		return false;
	}

	public void HandleEngageTasks()
	{
		if (_currentEngageTasks.Count == 0)
		{
			return;
		}
		bool flag = GameTime.missionElapsedTime >= _nextCantSeeAirTargetCheckTime;
		if (flag)
		{
			_nextCantSeeAirTargetCheckTime = GameTime.missionElapsedTime + 5.0;
		}
		for (int i = 0; i < _currentEngageTasks.Count; i++)
		{
			int uid = _currentEngageTasks[i]._uid;
			if (flag)
			{
				EngageTask engageTask = _currentEngageTasks[i];
				if (engageTask._targetObject != null && engageTask._targetObject.isAirTarget() && engageTask._ammunition?._ap != null && (engageTask._ammunition._ap._type == Ammunition.Type.Missile || engageTask._ammunition._ap._type == Ammunition.Type.Projectile) && Singleton<OptionsManager>.Instance.RequireVisionToEngage != 0 && CantSeeAirTarget(engageTask._targetObject))
				{
					DM.I(getUIDAndName() + ": Engage task discarded because the air target can no longer be seen: " + engageTask._ammoId);
					RemoveEngageTask(i);
					EndAlignment(uid, "HandleEngageTasks");
					i--;
					continue;
				}
			}
			if (this is Aircraft aircraft && aircraft.EngageTaskBeyondShotRange(_currentEngageTasks[i]))
			{
				continue;
			}
			if (flag && !IsAirUnit && _currentEngageTasks[i]._isAutoAttack)
			{
				int resolvedValue = TacticalDoctrine.RemoveEngageTaskAfter.ResolvedValue;
				if (resolvedValue > 0 && GameTime.missionElapsedTime - _currentEngageTasks[i].CreationTime > (double)resolvedValue && !AnySystemExecutingEngageTask(uid))
				{
					DM.Log($"{getUIDAndName()}: Engage task discarded due to age more than {resolvedValue} sec: {_currentEngageTasks[i]._ammoId}", DM.EventLogFilters.EngageTask);
					RemoveEngageTask(i);
					EndAlignment(uid, "HandleEngageTasks");
					i--;
					continue;
				}
			}
			List<WeaponSystem> list = WeaponSelector.GetWeaponSystemsForAmmunition(this, _currentEngageTasks[i].TargetPosition, _currentEngageTasks[i]._ammoId, _currentEngageTasks[i]._targetObject);
			if (list == null || list.Count < 1)
			{
				DM.I(getUIDAndName() + ": Engage task discarded because there are no systems found for it to execute");
				RemoveEngageTask(i);
				EndAlignment(uid, "HandleEngageTasks");
				i--;
				continue;
			}
			int num = 0;
			foreach (WeaponSystem item in list)
			{
				if (WeaponSelector.isWeaponSystemOutOfAmmo(item, _currentEngageTasks[i]._ammoId))
				{
					num++;
				}
			}
			if (list.Count == num)
			{
				DM.I(getUIDAndName() + ": Engage task discarded because all systems for it are empty");
				RemoveEngageTask(i);
				EndAlignment(uid, "HandleEngageTasks");
				i--;
				continue;
			}
			if (_currentEngageTasks[i]._targetObject != null)
			{
				bool flag2 = false;
				if (_currentEngageTasks[i]._ammunition._ap.GuidanceTypeForTarget(_currentEngageTasks[i]._targetObject) == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
				{
					flag2 = true;
				}
				if (_currentEngageTasks[i]._ammunition._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
				{
					flag2 = true;
				}
				if (flag2)
				{
					foreach (WeaponSystem item2 in list)
					{
						if (item2._vwp._associatedSearchSensors.Count <= 0)
						{
							continue;
						}
						foreach (SensorSystem associatedSearchSensor in item2._vwp._associatedSearchSensors)
						{
							if (!associatedSearchSensor.IsOn.Value && !associatedSearchSensor._wasSetOn)
							{
								associatedSearchSensor.Enable(allowMessage: true, turnOffAfterEngage: true);
							}
							associatedSearchSensor._turnOffTime = GameTime.missionElapsedTime + 20.0;
						}
					}
				}
				EngageTask engageTask2 = _currentEngageTasks[i];
				if (engageTask2._targetObject.isAirTarget() && (engageTask2._ammunition._ap._type == Ammunition.Type.Missile || engageTask2._ammunition._ap._type == Ammunition.Type.Projectile) && GetLocalAirTargetVisibilityRange(engageTask2._targetObject, default(SensorRangeOptions), excludeRadars: true) < 20.669622f)
				{
					foreach (SensorSystem sensorSystem in _obp._sensorSystems)
					{
						if (sensorSystem == null || sensorSystem._systemType != BaseSystem.SystemType.SensorSearchRadar || sensorSystem.IsDestroyed || sensorSystem.Inoperable.Value)
						{
							continue;
						}
						SensorSystem.SensorDistanceData range = sensorSystem.GetRange(engageTask2._targetObject, default(SensorRangeOptions));
						if (range != null && !(range.Distance < 20.669622f))
						{
							if (!sensorSystem.IsOn.Value && !sensorSystem._wasSetOn)
							{
								sensorSystem.Enable(allowMessage: true, turnOffAfterEngage: true);
							}
							sensorSystem._turnOffTime = GameTime.missionElapsedTime + 20.0;
						}
					}
				}
			}
			if (!_ai.IsProcessedForEngagement(_currentEngageTasks[i]._targetObject))
			{
				if (!_ai.IsAutomaticMountOnlyTarget(_currentEngageTasks[i]._targetObject))
				{
					continue;
				}
				for (int num2 = list.Count - 1; num2 >= 0; num2--)
				{
					if (list[num2]?._vwp == null || !list[num2]._vwp._automatic)
					{
						list.RemoveAt(num2);
					}
				}
				if (list.Count < 1)
				{
					continue;
				}
			}
			if (list[0]._weaponType == WeaponSystem.WeaponType.Chaff)
			{
				if (!ReactionTime.SoftKillReadyAfter((float)(GameTime.missionElapsedTime - _currentEngageTasks[i].CreationTime), this))
				{
					continue;
				}
				foreach (WeaponSystemChaff chaffWeapon in _obp._chaffWeapons)
				{
					if (chaffWeapon.readyToLaunch())
					{
						chaffWeapon.launchChaff();
					}
				}
				RemoveEngageTask(i);
				i--;
				continue;
			}
			AmmunitionParameters ap = _currentEngageTasks[i]._ammunition._ap;
			bool flag3 = false;
			if (ap._requiresGuidance)
			{
				flag3 = list[0].CheckForMissileGroup(ap);
			}
			if (list.Count > 1)
			{
				if (list[0]._weaponType == WeaponSystem.WeaponType.Hardpoint && (_type == ObjectType.Aircraft || _type == ObjectType.Helicopter) && (ap._type == Ammunition.Type.Torpedo || ap._subType == Ammunition.Type.Sonobuoy) && !WeaponSelector.isWeaponSystemUsable(list[0], _currentEngageTasks[i]._ammoId, _currentEngageTasks[i]._targetObject))
				{
					continue;
				}
				if (list[0]._weaponType == WeaponSystem.WeaponType.Torpedo)
				{
					int num3 = 0;
					if (list[0]._executingEngageTask)
					{
						num3 = 1;
					}
					if (((WeaponSystemLauncher)list[0]).getNumberOfAmmunitionInAllContainers() > num3 && !WeaponSelector.isWeaponSystemUsable(list[0], _currentEngageTasks[i]._ammoId, _currentEngageTasks[i]._targetObject))
					{
						continue;
					}
				}
				if (!flag3 && list[0]._vwp._associatedSensors.Count > 0 && list[0]._executingEngageTask && (list[0].IsAimedAtTarget(_currentEngageTasks[i]._targetObject) || list[0]._targetObject == _currentEngageTasks[i]._targetObject) && list[0].HasFreeWeaponChannels(ap) && !WeaponSelector.isWeaponSystemUsable(list[0], _currentEngageTasks[i]._ammoId, _currentEngageTasks[i]._targetObject) && _currentEngageTasks[i]._shotsToFire < 2)
				{
					continue;
				}
			}
			List<WeaponSystem> list2 = Utils.CopyList(list);
			UpdateSolutionChannel(_currentEngageTasks[i], list2, ap);
			for (int num4 = list.Count - 1; num4 >= 0; num4--)
			{
				bool ignoreSensors = flag3 || list[num4] is WeaponSystemGun;
				if (!WeaponSelector.isWeaponSystemUsable(list[num4], _currentEngageTasks[i]._ammoId, _currentEngageTasks[i]._targetObject, ignoreSensors))
				{
					list.RemoveAt(num4);
				}
			}
			if (list.Count == 0 && ap._type == Ammunition.Type.Missile)
			{
				ObjectBase objectBase = this;
				if (objectBase is Vessel || objectBase is Submarine)
				{
					foreach (WeaponSystem item3 in list2)
					{
						if (item3 is WeaponSystemLauncher && WeaponSelector.isWeaponSystemUsable(item3, _currentEngageTasks[i]._ammoId, _currentEngageTasks[i]._targetObject, ignoreSensors: true))
						{
							list.Add(item3);
						}
					}
				}
			}
			if (list.Count == 0 && ap._type == Ammunition.Type.Missile)
			{
				ObjectBase objectBase = this;
				if (objectBase is Vessel || objectBase is Submarine)
				{
					WeaponSystem weaponSystem = null;
					foreach (WeaponSystem item4 in list2)
					{
						if (item4 is WeaponSystemLauncher && item4._isReloading && (item4._ammoForEngage == null || !(item4._ammoForEngage._ap._ammunitionFileName != _currentEngageTasks[i]._ammoId)) && WeaponSelector.isWeaponSystemUsable(item4, _currentEngageTasks[i]._ammoId, _currentEngageTasks[i]._targetObject, ignoreSensors: true, ignoreExecuting: false, ignoreReloading: true) && (weaponSystem == null || item4._remainingReloadTime < weaponSystem._remainingReloadTime))
						{
							weaponSystem = item4;
						}
					}
					if (weaponSystem != null)
					{
						list.Add(weaponSystem);
					}
				}
			}
			List<WeaponSystem> list3 = new List<WeaponSystem>();
			for (int j = 0; j < list.Count; j++)
			{
				if (WeaponSelector.isWeaponSystemLoaded(list[j], _currentEngageTasks[i]._ammoId))
				{
					list3.Add(list[j]);
				}
			}
			bool flag4 = false;
			if (list.Count > 0 && list[0].GetType() == typeof(WeaponSystemGun))
			{
				flag4 = true;
			}
			if (list3.Count > 0)
			{
				list = list3;
			}
			for (int num5 = list.Count - 1; num5 >= 0; num5--)
			{
				if (list[num5]._vwp._sharesMountWithSystems.Count != 0)
				{
					foreach (WeaponSystem sharesMountWithSystem in list[num5]._vwp._sharesMountWithSystems)
					{
						if (sharesMountWithSystem._executingEngageTask && (!(sharesMountWithSystem._targetObject != null) || !(sharesMountWithSystem._targetObject == _currentEngageTasks[i]._targetObject)))
						{
							list.RemoveAt(num5);
							break;
						}
					}
				}
			}
			if (list.Count == 0)
			{
				if (!_currentEngageTasks[i]._isAutoAttack)
				{
					foreach (WeaponSystem item5 in list2)
					{
						if (item5 is WeaponSystemGun && item5._executingEngageTask && (!(item5._targetObject == _currentEngageTasks[i]._targetObject) || (!(item5._targetObject != null) && !((item5._targetPosition - _currentEngageTasks[i].TargetPosition).sqrMagnitude < 1f))) && (item5._isAutoEngaging || item5._takenEngageTask == null || !(item5._takenEngageTask.CreationTime >= _currentEngageTasks[i].CreationTime)))
						{
							DM.Log("[" + getUIDAndName() + "]: " + item5._vwp._sectionName + " breaks off " + ((item5._targetObject != null) ? item5._targetObject.getUIDAndName() : "position fire") + " for the player's order on " + ((_currentEngageTasks[i]._targetObject != null) ? _currentEngageTasks[i]._targetObject.getUIDAndName() : "position"), DM.EventLogFilters.EngageTask);
							item5.EndEngageTask();
						}
					}
				}
				if (!(_currentEngageTasks[i]._targetObject is WeaponBase))
				{
					continue;
				}
				foreach (WeaponSystemLauncher launcherWeapon in _obp._launcherWeapons)
				{
					if ((launcherWeapon.getLoadedAmmoCount(_currentEngageTasks[i]._ammoId) > 0 || launcherWeapon.getMagazineAmmoCount(_currentEngageTasks[i]._ammoId) > 0) && launcherWeapon._executingEngageTask && (launcherWeapon._targetObject == null || !launcherWeapon._targetObject.isWeapon()))
					{
						launcherWeapon.EndEngageTask();
					}
				}
				continue;
			}
			DM.Log("[" + getUIDAndName() + "]: <color=red>Engages</color> " + ((_currentEngageTasks[i]._targetObject != null) ? (_currentEngageTasks[i]._targetObject.getUIDAndName() ?? "") : "position") + " with " + _currentEngageTasks[i]._ammunition._ap._displayedName, DM.EventLogFilters.EngageTask);
			list[0]._takenEngageTask = _currentEngageTasks[i];
			list[0]._executingEngageTask = true;
			list[0]._ignoreIfUndetected = _currentEngageTasks[i]._ignoreIfUndetected;
			if (IsAirUnit && _currentEngageTasks[i]._targetObject != null && _currentEngageTasks[i]._targetObject == _ai._objectToDestroy)
			{
				list[0]._ignoreIfUndetected = true;
			}
			list[0]._engageTaskPriority = _currentEngageTasks[i]._priority;
			list[0]._ammoForEngage = _currentEngageTasks[i]._ammunition;
			list[0]._isTargetObjectRequired = _currentEngageTasks[i]._isTargetObjectRequired;
			list[0]._alignmentUID = _currentEngageTasks[i]._uid;
			list[0]._engageStartTime = ((_currentEngageTasks[i]._solutionStartTime >= 0.0) ? _currentEngageTasks[i]._solutionStartTime : GameTime.missionElapsedTime);
			list[0]._engageDelay = (float)Globals._rnd.NextDouble() * list[0]._vwp._maxReactiontime;
			list[0]._targetObject = _currentEngageTasks[i]._targetObject;
			list[0]._currentTargetPoint = _currentEngageTasks[i]._curentTargetPoint;
			list[0]._targetPosition = _currentEngageTasks[i].TargetPosition;
			if (list[0] is WeaponSystemCIWS weaponSystemCIWS)
			{
				weaponSystemCIWS._autoEngageTask = _currentEngageTasks[i]._isAutoAttack;
			}
			else
			{
				list[0]._isAutoEngaging = _currentEngageTasks[i]._isAutoAttack;
			}
			if (list[0]._ammoForEngage._ap._type == Ammunition.Type.Torpedo && _currentEngageTasks[i]._shotsToFire > 1 && _currentEngageTasks[i]._spreadPositions.Count < 1)
			{
				_currentEngageTasks[i].CalculateTorpedoSpread();
			}
			string text = "";
			if (_currentEngageTasks[i]._spreadPositions.Count > 0)
			{
				text += "Spread: ";
				list[0]._offsetVector = _currentEngageTasks[i]._spreadPositions.Dequeue();
			}
			text = ((!(_currentEngageTasks[i]._targetObject != null)) ? (text + "Shot to bearing.") : (text + "Target: " + list[0]._targetObject.getUIDAndName()));
			int num6 = 1;
			int num7;
			if (_currentEngageTasks[i]._isAutoAttack && this is Vessel && _currentEngageTasks[i]._targetObject != null && _currentEngageTasks[i]._targetObject.isAirTarget())
			{
				Ammunition ammunition = _currentEngageTasks[i]._ammunition;
				num7 = ((ammunition != null && ammunition._ap?._type == Ammunition.Type.Missile) ? 1 : 0);
			}
			else
			{
				num7 = 0;
			}
			bool flag5 = (byte)num7 != 0;
			if (!flag4 && list[0]._vwp._linkedFire)
			{
				int num8 = (flag5 ? (_currentEngageTasks[i]._shotsToFire - num6) : int.MaxValue);
				foreach (WeaponSystem item6 in list)
				{
					if (num8 <= 0)
					{
						break;
					}
					if (item6 != list[0] && item6._vwp._linkedFire && item6._systemName == list[0]._systemName)
					{
						item6._takenEngageTask = _currentEngageTasks[i];
						item6._executingEngageTask = true;
						item6._engageTaskPriority = _currentEngageTasks[i]._priority;
						item6._ammoForEngage = _currentEngageTasks[i]._ammunition;
						item6._isTargetObjectRequired = _currentEngageTasks[i]._isTargetObjectRequired;
						item6._alignmentUID = _currentEngageTasks[i]._uid;
						item6._engageStartTime = list[0]._engageStartTime;
						item6._engageDelay = (float)Globals._rnd.NextDouble() * item6._vwp._maxReactiontime;
						item6._targetObject = _currentEngageTasks[i]._targetObject;
						item6._currentTargetPoint = _currentEngageTasks[i]._curentTargetPoint;
						item6._targetPosition = _currentEngageTasks[i].TargetPosition;
						if (flag5)
						{
							num6++;
							num8--;
						}
					}
				}
			}
			if (flag4 && _currentEngageTasks[i]._targetObject != null)
			{
				foreach (WeaponSystemGun gunWeapon in _obp._gunWeapons)
				{
					if ((_currentEngageTasks[i]._ammoId == gunWeapon._ammoForEngage?._ap?._ammunitionFileName || gunWeapon.getLoadedAmmoCount(_currentEngageTasks[i]._ammoId) > 0 || gunWeapon.getMagazineAmmoCount(_currentEngageTasks[i]._ammoId) > 0) && (!gunWeapon._vwp._engageOnlyInsideArc || gunWeapon.targetIsInFiringArc(_currentEngageTasks[i]._targetObject.transform.position)))
					{
						gunWeapon._takenEngageTask = _currentEngageTasks[i];
						gunWeapon._executingEngageTask = true;
						gunWeapon._engageTaskPriority = _currentEngageTasks[i]._priority;
						gunWeapon._ammoForEngage = _currentEngageTasks[i]._ammunition;
						gunWeapon._targetObject = _currentEngageTasks[i]._targetObject;
						gunWeapon._currentTargetPoint = _currentEngageTasks[i]._curentTargetPoint;
						gunWeapon._targetPosition = _currentEngageTasks[i].TargetPosition;
						gunWeapon._alignmentUID = _currentEngageTasks[i]._uid;
						gunWeapon._engageStartTime = GameTime.missionElapsedTime;
						gunWeapon._engageDelay = (float)Globals._rnd.NextDouble() * gunWeapon._vwp._maxReactiontime;
					}
				}
			}
			bool flag6 = false;
			if (list.Count > 0 && list[0] is WeaponSystemHardpoint && _currentEngageTasks[i]._ammunition._ap._type == Ammunition.Type.Bomb && _currentEngageTasks[i]._ammunition._ap._subType != Ammunition.Type.Sonobuoy && _currentEngageTasks[i]._ammunition._ap.GuidanceTypeForTarget(_currentEngageTasks[i]._targetObject) == AmmunitionParameters.GuidanceType.None)
			{
				flag6 = true;
				foreach (WeaponSystem item7 in list2)
				{
					if (item7 is WeaponSystemHardpoint && item7 != list[0])
					{
						if (item7._executingEngageTask)
						{
							item7.EndEngageTask();
						}
						item7._takenEngageTask = _currentEngageTasks[i];
						item7._executingEngageTask = true;
						item7._engageTaskPriority = _currentEngageTasks[i]._priority;
						item7._ammoForEngage = _currentEngageTasks[i]._ammunition;
						item7._isTargetObjectRequired = _currentEngageTasks[i]._isTargetObjectRequired;
						item7._alignmentUID = _currentEngageTasks[i]._uid;
						item7._engageStartTime = GameTime.missionElapsedTime;
						item7._engageDelay = list[0]._engageDelay;
						item7._targetObject = _currentEngageTasks[i]._targetObject;
						item7._currentTargetPoint = _currentEngageTasks[i]._curentTargetPoint;
						item7._targetPosition = _currentEngageTasks[i].TargetPosition;
					}
				}
			}
			bool flag7 = false;
			if (list[0] is WeaponSystemCIWS && _currentEngageTasks[i]._targetObject != null)
			{
				if (this is Aircraft)
				{
					flag7 = true;
				}
				foreach (WeaponSystemCIWS ciwsWeapon in _obp._ciwsWeapons)
				{
					if (_currentEngageTasks[i]._ammoId == ciwsWeapon._ammoForEngage?._ap?._ammunitionFileName && (!ciwsWeapon._vwp._engageOnlyInsideArc || ciwsWeapon.targetIsInFiringArc(_currentEngageTasks[i]._targetObject.transform.position)))
					{
						ciwsWeapon._takenEngageTask = _currentEngageTasks[i];
						ciwsWeapon._executingEngageTask = true;
						ciwsWeapon._engageTaskPriority = _currentEngageTasks[i]._priority;
						ciwsWeapon._targetObject = _currentEngageTasks[i]._targetObject;
						ciwsWeapon._alignmentUID = _currentEngageTasks[i]._uid;
						ciwsWeapon._ignoreIfUndetected = _currentEngageTasks[i]._ignoreIfUndetected;
						if (IsAirUnit && _currentEngageTasks[i]._targetObject != null && _currentEngageTasks[i]._targetObject == _ai._objectToDestroy)
						{
							ciwsWeapon._ignoreIfUndetected = true;
						}
						ciwsWeapon._engageStartTime = GameTime.missionElapsedTime;
						ciwsWeapon._engageDelay = list[0]._engageDelay;
						ciwsWeapon._currentTargetPoint = _currentEngageTasks[i]._curentTargetPoint;
						ciwsWeapon._targetPosition = _currentEngageTasks[i].TargetPosition;
						ciwsWeapon._autoEngageTask = _currentEngageTasks[i]._isAutoAttack;
					}
				}
			}
			if (list[0]._ammoForEngage._ap._type != Ammunition.Type.Noisemaker && list[0]._ammoForEngage._ap._subType != Ammunition.Type.Fueltank && !list[0]._vwp._engageOnlyInsideArc && (!(this is LandUnit landUnit) || landUnit._isMobile))
			{
				ClearPrepAlignment();
				int alignmentUid = _currentEngageTasks[i]._uid;
				if (_alignmentTasks.Any((AlignUnitToTarget a) => a._uid == alignmentUid))
				{
					foreach (AlignUnitToTarget alignmentTask in _alignmentTasks)
					{
						if (alignmentTask._uid == alignmentUid)
						{
							alignmentTask._endAlignmentStartTime = -1000.0;
							alignmentTask._endAlignmentDelay = -1000f;
							alignmentTask._alignmentEndRequested = false;
						}
					}
				}
				else
				{
					AlignUnitToTarget alignUnitToTarget = new AlignUnitToTarget(this, _currentEngageTasks[i], list[0]);
					if (!flag4 && list.Count == 1)
					{
						alignUnitToTarget._firingArcs = list[0]._vwp._firingArcs;
					}
					if (list[0]._vwp._minSpeedForLaunnchKnots > 0f)
					{
						alignUnitToTarget._maintainMaxSpeed = true;
						if (this is Vessel vessel)
						{
							vessel.setTelegraph(5);
						}
						else if (this is Submarine submarine)
						{
							submarine.setTelegraph(5);
						}
					}
					if (flag7)
					{
						alignUnitToTarget._verticalAlignmentRequired = true;
					}
					if (list[0] is WeaponSystemHardpoint weaponSystemHardpoint && _currentEngageTasks[i]._targetObject != null)
					{
						weaponSystemHardpoint._verticalAlignmentRequired = false;
						if (_currentEngageTasks[i]._ammunition._ap._forceDirectPoint)
						{
							alignUnitToTarget._verticalAlignmentRequired = true;
							weaponSystemHardpoint._verticalAlignmentRequired = true;
						}
						if (_currentEngageTasks[i]._ammunition._ap._type == Ammunition.Type.Missile)
						{
							if (_currentEngageTasks[i]._targetObject.IsAirUnit || _currentEngageTasks[i]._targetObject is WeaponBase)
							{
								alignUnitToTarget._verticalAlignmentRequired = true;
								weaponSystemHardpoint._verticalAlignmentRequired = true;
							}
						}
						else if (_currentEngageTasks[i]._ammunition._ap._type == Ammunition.Type.AerialRocket)
						{
							alignUnitToTarget._verticalAlignmentRequired = true;
							weaponSystemHardpoint._verticalAlignmentRequired = true;
						}
					}
					int count = _alignmentTasks.Count;
					if ((_type == ObjectType.Aircraft || _type == ObjectType.Helicopter) && count > 0)
					{
						AlignUnitToTarget alignUnitToTarget2 = _alignmentTasks[count - 1];
						if (alignUnitToTarget2._isGuidingWeapon || alignUnitToTarget2._ap._type == Ammunition.Type.Bomb)
						{
							_alignmentTasks[count - 1] = alignUnitToTarget;
							_alignmentTasks.Add(alignUnitToTarget2);
						}
						else
						{
							_alignmentTasks.Add(alignUnitToTarget);
						}
					}
					else
					{
						_alignmentTasks.Add(alignUnitToTarget);
					}
				}
			}
			int num9 = ((!flag5) ? 1 : num6);
			if (flag6)
			{
				DM.I(getUIDAndName() + ": Bomb drop Engage task for " + _currentEngageTasks[i]._ammoId + " accepted by " + list[0]._vwp._sectionName + " " + text);
				RemoveEngageTask(i);
				i--;
			}
			else if (_currentEngageTasks[i]._shotsToFire > num9 && list[0].GetType() != typeof(WeaponSystemGun))
			{
				_currentEngageTasks[i]._shotsToFire -= num9;
				DM.I($"{getUIDAndName()}: Engage task for {_currentEngageTasks[i]._ammoId} accepted by {list[0]._vwp._sectionName} {text}. {_currentEngageTasks[i]._shotsToFire} shots still left");
			}
			else
			{
				DM.I($"{getUIDAndName()}: Engage task for {_currentEngageTasks[i]._ammoId} accepted by {list[0]._vwp._sectionName} {text}. Height = {_currentEngageTasks[i].TargetPosition.y * 220.47266f} ft");
				RemoveEngageTask(i);
				i--;
			}
		}
	}

	public EngageTask InsertEngageTask(string ammoId, ObjectBase targetObject, Vector3 targetPosition, int shotsToFire, int priority = 1000, bool autoAttack = false, bool markAsReturned = false, bool isFormationAttack = false)
	{
		EngageTask engageTask = ((!(targetObject != null)) ? new EngageTask(ammoId, targetPosition, this, shotsToFire, priority, autoAttack, isFormationAttack) : new EngageTask(ammoId, targetObject, this, shotsToFire, priority, autoAttack, isFormationAttack));
		AddEngageTask(engageTask);
		_currentEngageTasks = _currentEngageTasks.OrderBy((EngageTask x) => x._priority).ToList();
		return engageTask;
	}

	public void AddEngageTask(EngageTask engageTask)
	{
		_currentEngageTasks.Add(engageTask);
	}

	public void EnsurePrepAlignment(ObjectBase target, Ammunition ammo, WeaponSystem weaponSystem)
	{
		if (!(this is Vessel) || target == null || ammo == null || weaponSystem == null)
		{
			return;
		}
		if (!TacticalDoctrine.AllowAlignment.ResolvedValue)
		{
			ClearPrepAlignment();
			return;
		}
		foreach (AlignUnitToTarget alignmentTask in _alignmentTasks)
		{
			if (!alignmentTask._isPrep)
			{
				ClearPrepAlignment();
				return;
			}
		}
		if (_prepAlignment != null && _alignmentTasks.Contains(_prepAlignment) && _prepAlignment._targetObject == target)
		{
			_prepAlignment._prepRefreshedTime = GameTime.missionElapsedTime;
			return;
		}
		ClearPrepAlignment();
		AlignUnitToTarget alignUnitToTarget = new AlignUnitToTarget(this, target, ammo, weaponSystem, int.MaxValue);
		if (alignUnitToTarget._firingArcs != null && alignUnitToTarget._firingArcs.Count != 0)
		{
			_prepAlignment = alignUnitToTarget;
			_alignmentTasks.Add(alignUnitToTarget);
		}
	}

	public void ClearPrepAlignment()
	{
		if (_prepAlignment != null)
		{
			EndAlignment(_prepAlignment._uid, "ClearPrepAlignment");
			_prepAlignment = null;
		}
	}

	public void DelayedEndAlignment(int uid, float time = 0f, [CallerMemberName] string callerName = "")
	{
		if (uid < 0)
		{
			return;
		}
		for (int num = _alignmentTasks.Count - 1; num >= 0; num--)
		{
			if (_alignmentTasks[num]._uid == uid && _alignmentTasks[num]._endAlignmentDelay < -1f)
			{
				if (time < 0.01f)
				{
					time = _crew._endAlignmentDelay;
				}
				_alignmentTasks[num].DelayedEnd(time);
				break;
			}
		}
	}

	public void ClearDeadTargetAlignments()
	{
		for (int num = _alignmentTasks.Count - 1; num >= 0; num--)
		{
			AlignUnitToTarget alignUnitToTarget = _alignmentTasks[num];
			if (alignUnitToTarget._hadTarget && (alignUnitToTarget._targetObject == null || alignUnitToTarget._targetObject.IsDestroyed || alignUnitToTarget._targetObject._externalDestructionNotified))
			{
				EndAlignment(alignUnitToTarget._uid, "ClearDeadTargetAlignments");
			}
		}
	}

	public void EndAlignment(int uid, [CallerMemberName] string callerName = "")
	{
		if (uid < 0)
		{
			return;
		}
		for (int num = _alignmentTasks.Count - 1; num >= 0; num--)
		{
			if (_alignmentTasks[num]._uid == uid)
			{
				if (_alignmentTasks[num]._guidedWeapon != null)
				{
					WeaponBase guidedWeapon = _alignmentTasks[num]._guidedWeapon;
					bool flag = guidedWeapon._guidingRadarSystem != null && !guidedWeapon._guidingRadarSystem._requiresAlignment;
					if ((guidedWeapon._seeker == null || ((!guidedWeapon._seeker.isActive() || guidedWeapon._seeker._guidanceType != AmmunitionParameters.GuidanceType.ActiveRadarHoming) && !guidedWeapon._ap.CanFinishOnOwnSeeker(guidedWeapon._seeker._guidanceType))) && !flag)
					{
						guidedWeapon.ConnectionLost.Value = true;
						guidedWeapon.ConnectionLostForever.Value = true;
					}
				}
				DesiredAltitude.Value = ClampRestoredAltitudeForGuidance(_alignmentTasks[num]._initialDesiredAltitude);
				if (this is Aircraft && AllowCombatSpeedControl && _alignmentTasks[num]._initialSpeedCommandValue != null)
				{
					SetSpeedCommand(_alignmentTasks[num]._initialSpeedCommandValue);
				}
				if (_alignmentTasks[num]._telegraphWasChanged)
				{
					if (this is Vessel || this is Submarine)
					{
						setRudderAngle(0f);
						setTelegraph(_previousTelegraphSetting);
					}
				}
				_alignmentTasks[num].DestroyAimpoint(15f);
				_alignmentTasks.RemoveAt(num);
				break;
			}
		}
	}

	public void EndAllAlignment([CallerMemberName] string callerName = "")
	{
		foreach (AlignUnitToTarget alignmentTask in _alignmentTasks)
		{
			alignmentTask.DestroyAimpoint();
		}
		if (_alignmentTasks.Count > 0)
		{
			DesiredAltitude.Value = ClampRestoredAltitudeForGuidance(_alignmentTasks[0]._initialDesiredAltitude);
			if (this is Aircraft && AllowCombatSpeedControl && _alignmentTasks[0]._initialSpeedCommandValue != null)
			{
				SetSpeedCommand(_alignmentTasks[0]._initialSpeedCommandValue);
			}
		}
		_alignmentTasks.Clear();
		if (_type == ObjectType.Vessel)
		{
			((Vessel)this).setRudderAngle(0f);
			((Vessel)this).setTelegraph(((Vessel)this)._previousTelegraphSetting);
		}
		else if (_type == ObjectType.Submarine)
		{
			((Submarine)this).setRudderAngle(0f);
			((Submarine)this).setTelegraph(((Submarine)this)._previousTelegraphSetting);
		}
	}

	public bool HasGuidingAlignment()
	{
		foreach (AlignUnitToTarget alignmentTask in _alignmentTasks)
		{
			if (alignmentTask._isGuidingWeapon)
			{
				return true;
			}
		}
		return false;
	}

	private float ClampRestoredAltitudeForGuidance(float altitude)
	{
		if (!(this is Submarine submarine) || submarine._ai == null)
		{
			return altitude;
		}
		if (!submarine._ai.HasMissilesNeedingGuidance())
		{
			return altitude;
		}
		float b = (submarine._ai.GuidanceHoldRequiresSurface() ? 0f : (0f - submarine.SP._periscopeDepth));
		return Mathf.Max(altitude, b);
	}

	public bool IsHoldingLaserGuardFor(ObjectBase targetObject)
	{
		if (targetObject == null || !(this is Aircraft))
		{
			return false;
		}
		foreach (AlignUnitToTarget alignmentTask in _alignmentTasks)
		{
			if (alignmentTask._isGuidingWeapon && !(alignmentTask._guidedWeapon == null) && alignmentTask._guidedWeapon.IsSelfDesignatedLaserShot && !alignmentTask._guidedWeapon.IsDestroyed && !alignmentTask._guidedWeapon._externalDestructionNotified && !(alignmentTask._guidedWeapon.CurrentIntendedTargetObject != targetObject))
			{
				return true;
			}
		}
		return false;
	}

	public float BestOperableLaserDesignatorRange()
	{
		float num = 0f;
		List<SensorSystemLaserDesignator> list = _obp?._laserSystems;
		if (list == null)
		{
			return num;
		}
		foreach (SensorSystemLaserDesignator item in list)
		{
			if (item != null && !item.IsDestroyed && !item.Inoperable.Value && item._maxRange > num)
			{
				num = item._maxRange;
			}
		}
		return num;
	}

	public void UpdateAlignmentAltitudes()
	{
		foreach (AlignUnitToTarget alignmentTask in _alignmentTasks)
		{
			alignmentTask._initialDesiredAltitude = DesiredAltitude.Value;
		}
		if (StateMachine?.CurrentState is AircraftStates.Aligning aligning)
		{
			aligning._previousDesiredAltitude = DesiredAltitude.Value;
		}
	}

	public void UpdateAlignmentSpeeds()
	{
		foreach (AlignUnitToTarget alignmentTask in _alignmentTasks)
		{
			alignmentTask._initialSpeedCommandValue = SpeedCommand.Value;
		}
	}

	public void ReloadVariant()
	{
		IniHandler iniHandler = Utils.openIniFile(_obp._variantsFolder, _obp._variantsFile, logErrors: false);
		if (iniHandler != null)
		{
			_obp._objectVariantReference = ((_obp._currentVariantReferenceIndex > 0) ? ("Variant" + _obp._currentVariantReferenceIndex) : "Default");
			string hullnumberReference = iniHandler.readValue("General", "HullnumberReference", "");
			string emblemReference = iniHandler.readValue("General", "EmblemReference", "", performLogging: false);
			string nationalFlagReference = iniHandler.readValue("General", "NationFlagReference", "", performLogging: false);
			if (iniHandler.doesSectionExist(_obp._objectVariantReference))
			{
				IniHandler iniHandler2 = LocalizationLoader.OpenVirtualLocalizationFile("language_" + Singleton<LanguageResourceHandler>.Instance.Language, "vessel_names", "vessel_names");
				IniHandler localization = ((Singleton<LanguageResourceHandler>.Instance.Language != "en") ? LocalizationLoader.OpenVirtualLocalizationFile("language_en", "vessel_names", "vessel_names") : iniHandler2);
				if (iniHandler2 != null)
				{
					string[] array = iniHandler2.readValue(_obp._objectIniName, _obp._objectVariantReference, _obp._objectName).Split(',');
					_obp._objectName = array[0];
					_obp._shortName = ((array.Length > 1) ? array[1] : array[0]);
					string[] array2 = iniHandler2.readValue(_obp._objectIniName, "Type", "").Split(',');
					_obp._typeAbbr = array2[0];
					_obp._typeFullName = ((array2.Length > 1) ? array2[1] : array2[0]);
					string[] array3 = iniHandler2.readValue(_obp._objectIniName, "Default", _obp._objectName).Split(',');
					string[] array4 = LocalizationLoader.ReadNatoUnitDefault(localization, _obp._objectIniName, (string[])array3.Clone());
					_obp._nativeDefaultName = array3[0];
					_obp._nativeClassName = ((array3.Length > 1) ? array3[1] : array3[0]);
					_obp._natoDefaultName = array4[0];
					_obp._natoClassName = ((array4.Length > 1) ? array4[1] : array4[0]);
					_obp.ApplyPreferredDefaultNames();
				}
				else
				{
					_obp._objectName = "MISSING";
					_obp._className = _obp._objectName;
					_obp._defaultName = _obp._objectName;
					_obp._shortName = _obp._objectName;
					_obp._typeAbbr = _obp._objectName;
				}
				setName(_obp._objectName);
				_obp._nation = iniHandler.readValue(_obp._objectVariantReference, "Nation", "");
				setNation(_obp._nation);
				string resourcesHullnumberFolder = iniHandler.readValue(_obp._objectVariantReference, "ResourcesHullnumberFolder", "");
				string hullnumberTexture = iniHandler.readValue(_obp._objectVariantReference, "HullnumberTexture", "");
				string resourcesEmblemFolder = iniHandler.readValue(_obp._objectVariantReference, "ResourcesEmblemFolder", "", performLogging: false);
				string emblemTexture = iniHandler.readValue(_obp._objectVariantReference, "EmblemTexture", "", performLogging: false);
				string resourcesLiveryFolder = iniHandler.readValue(_obp._objectVariantReference, "ResourcesLiveryFolder", "", performLogging: false);
				string item = iniHandler.readValue(_obp._objectVariantReference, "LiveryTexture", "");
				string item2 = iniHandler.readValue(_obp._objectVariantReference, "SpecularTexture", "", performLogging: false);
				string item3 = iniHandler.readValue(_obp._objectVariantReference, "NormalTexture", "", performLogging: false);
				string resourcesFlagFolder = iniHandler.readValue(_obp._objectVariantReference, "ResourcesFlagFolder", "", performLogging: false);
				string flagTexture = iniHandler.readValue(_obp._objectVariantReference, "FlagTexture", "", performLogging: false);
				ObjectBaseLoader.LoadHullNumber(_obp, hullnumberReference, resourcesHullnumberFolder, hullnumberTexture);
				ObjectBaseLoader.LoadEmblem(_obp, emblemReference, resourcesEmblemFolder, emblemTexture);
				if (!string.IsNullOrEmpty(_obp._objectMaterialName) && _obp._materialsDictionary.ContainsKey(_obp._objectMaterialName))
				{
					ObjectBaseLoader.LoadLivery(_obp, resourcesLiveryFolder, new List<string> { item, item2, item3 }, _obp._materialsDictionary[_obp._objectMaterialName]);
				}
				ObjectBaseLoader.LoadFlag(_obp, nationalFlagReference, resourcesFlagFolder, flagTexture);
			}
			else
			{
				Log.DebugWarning("Could not find object name reference '" + _obp._objectVariantReference + " ' for object '" + _obp._objectIniName + " '", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 4236, "ReloadVariant");
			}
		}
		else
		{
			Log.Error("Could not find ini file '" + _obp._variantsFile + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 4239, "ReloadVariant");
		}
	}

	public void ReloadSquadron()
	{
		if (_obp._currentVariantReferenceIndex < 0)
		{
			_obp._currentVariantReferenceIndex = _obp._numberOfVariantsReferences;
		}
		else if (_obp._currentVariantReferenceIndex > _obp._numberOfVariantsReferences)
		{
			_obp._currentVariantReferenceIndex = 0;
		}
		if (_obp._currentSquadronSerialIndex < 0)
		{
			_obp._currentSquadronSerialIndex = _obp._squadronSerialNumbers.Count - 1;
		}
		else if (_obp._currentSquadronSerialIndex > _obp._squadronSerialNumbers.Count - 1)
		{
			_obp._currentSquadronSerialIndex = 0;
		}
		IniHandler iniHandler = Utils.openIniFile(_obp._variantsFolder, _obp._variantsFile, logErrors: false);
		if (iniHandler != null)
		{
			_obp._objectVariantReference = ((_obp._currentVariantReferenceIndex > 0) ? ("Squadron" + _obp._currentVariantReferenceIndex) : "Default");
			string[] serialnumberReferences = iniHandler.readValue("General", "SerialnumberReferences", "").Split(',');
			string emblemReference = iniHandler.readValue("General", "EmblemReference", "", performLogging: false);
			string nationalFlagReference = iniHandler.readValue("General", "NationFlagReference", "", performLogging: false);
			if (iniHandler.doesSectionExist(_obp._objectVariantReference))
			{
				IniHandler iniHandler2 = LocalizationLoader.OpenVirtualLocalizationFile("language_" + Singleton<LanguageResourceHandler>.Instance.Language, "aircraft_names", "aircraft_names");
				IniHandler localization = ((Singleton<LanguageResourceHandler>.Instance.Language != "en") ? LocalizationLoader.OpenVirtualLocalizationFile("language_en", "aircraft_names", "aircraft_names") : iniHandler2);
				if (iniHandler2 != null)
				{
					string[] array = iniHandler2.readValue(_obp._objectIniName, _obp._objectVariantReference, _obp._objectName).Split(',');
					_obp._objectName = array[0];
					_obp._shortName = ((array.Length > 1) ? array[1] : array[0]);
					_obp._typeAbbr = iniHandler2.readValue(_obp._objectIniName, "Type", "");
					string[] array2 = iniHandler2.readValue(_obp._objectIniName, "Default", _obp._objectName).Split(',');
					string[] array3 = LocalizationLoader.ReadNatoUnitDefault(localization, _obp._objectIniName, (string[])array2.Clone());
					_obp._nativeDefaultName = array2[0];
					_obp._nativeClassName = ((array2.Length > 1) ? array2[1] : array2[0]);
					_obp._natoDefaultName = array3[0];
					_obp._natoClassName = ((array3.Length > 1) ? array3[1] : array3[0]);
					_obp.ApplyPreferredDefaultNames();
				}
				else
				{
					_obp._objectName = "MISSING";
					_obp._className = _obp._objectName;
					_obp._defaultName = _obp._objectName;
					_obp._shortName = _obp._objectName;
					_obp._typeAbbr = _obp._objectName;
				}
				setName(_obp._objectName);
				_obp._nation = iniHandler.readValue(_obp._objectVariantReference, "Nation", "");
				setNation(_obp._nation);
				string resourcesSerialnumberFolder = iniHandler.readValue(_obp._objectVariantReference, "ResourcesSerialnumberFolder", "");
				string text = iniHandler.readValue(_obp._objectVariantReference, "SerialnumberPrefix", "");
				string text2 = iniHandler.readValue(_obp._objectVariantReference, "SerialnumberTextures", "");
				_obp._squadronSerialNumbers = text2.Split(',').ToList();
				if (_obp._squadronSerialNumbers.Count > 0)
				{
					if (_obp._currentSquadronSerialIndex > _obp._squadronSerialNumbers.Count)
					{
						_obp._currentSquadronSerialIndex = Globals._rnd.Next(0, _obp._squadronSerialNumbers.Count);
					}
					_obp._objectSerialNumber = text + _obp._squadronSerialNumbers[_obp._currentSquadronSerialIndex];
				}
				string resourcesEmblemFolder = iniHandler.readValue(_obp._objectVariantReference, "ResourcesEmblemFolder", "", performLogging: false);
				string emblemTexture = iniHandler.readValue(_obp._objectVariantReference, "EmblemTexture", "", performLogging: false);
				string resourcesLiveryFolder = iniHandler.readValue(_obp._objectVariantReference, "ResourcesLiveryFolder", "", performLogging: false);
				string item = iniHandler.readValue(_obp._objectVariantReference, "LiveryTexture", "");
				string item2 = iniHandler.readValue(_obp._objectVariantReference, "SpecularTexture", "", performLogging: false);
				string item3 = iniHandler.readValue(_obp._objectVariantReference, "NormalTexture", "", performLogging: false);
				string resourcesFlagFolder = iniHandler.readValue(_obp._objectVariantReference, "ResourcesFlagFolder", "", performLogging: false);
				string flagTexture = iniHandler.readValue(_obp._objectVariantReference, "FlagTexture", "", performLogging: false);
				ObjectBaseLoader.LoadSerialNumber(_obp, serialnumberReferences, resourcesSerialnumberFolder, "", _obp._objectSerialNumber == "Overused");
				ObjectBaseLoader.LoadEmblem(_obp, emblemReference, resourcesEmblemFolder, emblemTexture);
				if (!string.IsNullOrEmpty(_obp._objectMaterialName) && _obp._materialsDictionary.ContainsKey(_obp._objectMaterialName))
				{
					ObjectBaseLoader.LoadLivery(_obp, resourcesLiveryFolder, new List<string> { item, item2, item3 }, _obp._materialsDictionary[_obp._objectMaterialName]);
				}
				ObjectBaseLoader.LoadFlag(_obp, nationalFlagReference, resourcesFlagFolder, flagTexture);
			}
			else
			{
				Log.DebugWarning("Could not find object name reference '" + _obp._objectVariantReference + " ' for object '" + _obp._objectIniName + " '", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 4336, "ReloadSquadron");
			}
		}
		else
		{
			Log.Error("Could not find ini file '" + _obp._variantsFile + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 4339, "ReloadSquadron");
		}
	}

	public void ReloadUnit()
	{
		Taskforce taskforce = _taskforce;
		string objectVariantReference = _obp._objectVariantReference;
		string objectVariantReference2 = _obp._objectVariantReference;
		string loadoutVariant = _obp._loadoutVariant;
		string objectIniName = _objectIniName;
		int count = Singleton<ObjectsManager>.Instance._listOfAllObjects.Count;
		GeoPosition geoCenterPosition = Singleton<SceneCreator>.Instance.GeoCenterPosition;
		GeoPosition geoPosition = _geoPosition;
		int currentTelegraph = Singleton<ObjectsManager>.Instance.getCurrentTelegraph();
		Vector3 eulerAngles = base.transform.eulerAngles;
		int dateOverrideYear = _obp._dateOverrideYear;
		ObjectBase homeBase = null;
		GameObject parentForAircraftOrHelo = Singleton<ObjectsManager>.Instance.getParentForAircraftOrHelo(this);
		IniHandler.invalidateCache();
		UnityEngine.Debug.Log("ObjectBase.reloadObject called");
		destroyObject(keepInFlightEffectAlive: false, score: false);
		ObjectBase objectBase = null;
		Utils.DateBasedAmmoYearOverride = dateOverrideYear;
		try
		{
			if (this is Vessel)
			{
				objectBase = Singleton<ObjectsManager>.Instance.createShip(objectVariantReference, loadoutVariant, objectIniName, count, geoCenterPosition, geoPosition, currentTelegraph, eulerAngles, taskforce);
			}
			else if (this is Submarine)
			{
				objectBase = Singleton<ObjectsManager>.Instance.createSubmarine(objectVariantReference, loadoutVariant, objectIniName, count, geoCenterPosition, geoPosition, currentTelegraph, eulerAngles, taskforce);
			}
			else if (this is Aircraft)
			{
				Singleton<ObjectsManager>.Instance.FreeSerialNumber(_obp._objectIniName, _obp._objectVariantReference, _obp._objectSerialNumber);
				Aircraft aircraft = Singleton<ObjectsManager>.Instance.createAircraft(objectVariantReference2, loadoutVariant, homeBase, parentForAircraftOrHelo, objectIniName, count, geoCenterPosition, geoPosition, eulerAngles, taskforce, createMinimapSymbol: true, loadWeaponSystems: true);
				objectBase = aircraft;
				if (aircraft != null)
				{
					aircraft.setTelegraph(4);
					aircraft._velocityInKnots = aircraft.SpeedCommand.Value.GetDemandedSpeedInKnots(aircraft);
					ObjectCodeAnimation value = null;
					string text = "Gear_Extend";
					if (!aircraft._obp._codeAnimations.TryGetValue(text, out value))
					{
						Log.Error("Could not find animation " + text + " for " + aircraft._obp._objectIniName, Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 4396, "ReloadUnit");
					}
					else
					{
						value.setClosedState();
					}
					aircraft.ImmediateStartEngines();
				}
			}
			else if (this is Helicopter)
			{
				Singleton<ObjectsManager>.Instance.FreeSerialNumber(_obp._objectIniName, _obp._objectVariantReference, _obp._objectSerialNumber);
				Helicopter helicopter = Singleton<ObjectsManager>.Instance.createHelicopter(objectVariantReference2, loadoutVariant, homeBase, parentForAircraftOrHelo, objectIniName, count, geoCenterPosition, geoPosition, eulerAngles, taskforce);
				objectBase = helicopter;
				ObjectCodeAnimation value2 = null;
				if (helicopter != null && helicopter._obp._codeAnimations.TryGetValue("Gear_Extend", out value2))
				{
					value2.setClosedState();
				}
			}
			else if (this is LandUnit)
			{
				LandUnit landUnit = Singleton<ObjectsManager>.Instance.createLandUnit(objectVariantReference, loadoutVariant, objectIniName, count, geoCenterPosition, geoPosition, 0, eulerAngles, taskforce);
				objectBase = landUnit;
				if (landUnit != null)
				{
					landUnit._performAutoFlatten = false;
				}
				AutoFlattener.SetObject(landUnit);
			}
		}
		finally
		{
			Utils.DateBasedAmmoYearOverride = 0;
		}
		if (objectBase == null)
		{
			Log.Error("Could not respawn object " + objectIniName + " " + objectVariantReference + " with loadout '" + loadoutVariant + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 4427, "ReloadUnit");
			return;
		}
		if (objectBase._obp != null && dateOverrideYear > 0)
		{
			objectBase._obp._dateOverrideYear = dateOverrideYear;
		}
		if (objectBase._obp?._flightDeck != null)
		{
			objectBase._obp._flightDeck.LoadFlightDeckAirgroupAndAmmo();
		}
		objectBase.transform.position = Utils.longLatToLocalV3(geoPosition, Globals._currentCenterTile);
		Physics.SyncTransforms();
		objectBase._geoPosition = geoPosition;
		Singleton<ObjectsManager>.Instance.setActiveObject(objectBase);
		Singleton<RenderPosition>.Instance.switchToObject(objectBase, performSmoothMove: false);
		Log.Debug("Object " + objectIniName + " " + objectVariantReference + " has been reloaded", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 4445, "ReloadUnit");
	}

	public void ExportMeshCollidersToIniFile()
	{
		Log.Debug("Exporting mesh colliders to " + _obp._objectIniFolder + "/" + _obp._objectIniName + ".ini", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 4451, "ExportMeshCollidersToIniFile");
		IniHandler iniHandler = new IniHandler();
		iniHandler.open(Application.streamingAssetsPath + "/original/" + _obp._objectIniFolder + "/" + _obp._objectIniName + ".ini", useCaching: false);
		int num = 0;
		foreach (UnityEngine.Transform item in _obp._meshCollidersParent.transform)
		{
			string text = item.name;
			if (text.Contains(_obp._objectIniName))
			{
				num++;
				text = text.Remove(0, _obp._objectIniName.Length + 1);
				iniHandler.writeValue("Colliders", "MeshCollider" + num, text);
				iniHandler.writeValue(text, "Collider", item.name);
			}
		}
		if (num > 0)
		{
			iniHandler.writeValue("Colliders", "NumberOfMeshColliders", num);
		}
		iniHandler.saveFile();
	}

	public void ExportCollidersToIniFile()
	{
		Log.Debug("Exporting colliders to " + _obp._objectIniFolder + "/" + _obp._objectIniName + ".ini", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 4481, "ExportCollidersToIniFile");
		IniHandler iniHandler = new IniHandler();
		iniHandler.open(Application.streamingAssetsPath + "/original/" + _obp._objectIniFolder + "/" + _obp._objectIniName + ".ini", useCaching: false);
		int num = 0;
		iniHandler.writeValue("Colliders", "NumberOfColliders", num);
		foreach (UnityEngine.Transform item in _obp._systemCollidersParent.transform)
		{
			if (!item.gameObject.name.Contains("CargoSlot"))
			{
				num++;
				iniHandler.writeValue("Colliders", "Collider" + num, item.name);
				if (item.GetComponent<BoxCollider>() != null)
				{
					iniHandler.writeValue(item.name, "Collider", "Box");
				}
				if (item.GetComponent<SphereCollider>() != null)
				{
					iniHandler.writeValue(item.name, "Collider", "Sphere");
				}
				string value = $"{Math.Round(item.transform.localPosition.x, 4):0.####},{Math.Round(item.transform.localPosition.y, 4):0.####},{Math.Round(item.transform.localPosition.z, 4):0.####}";
				string value2 = $"{Utils.WrapAngle(item.transform.localEulerAngles.x):0.##},{Utils.WrapAngle(item.transform.localEulerAngles.y):0.##},{Utils.WrapAngle(item.transform.localEulerAngles.z):0.##}";
				string value3 = $"{Utils.WrapAngle(item.transform.localScale.x):0.###},{Utils.WrapAngle(item.transform.localScale.y):0.###},{Utils.WrapAngle(item.transform.localScale.z):0.###}";
				iniHandler.writeValue(item.name, "Position", value);
				iniHandler.writeValue(item.name, "Rotation", value2);
				iniHandler.writeValue(item.name, "Scale", value3);
			}
		}
		iniHandler.writeValue("Colliders", "NumberOfColliders", num);
		iniHandler.saveFile();
	}

	public void ShowHideColliders()
	{
		if (_obp == null)
		{
			return;
		}
		_showColliders = !_showColliders;
		foreach (HitBox hitbox in _obp._hitboxes)
		{
			if (!(hitbox._go != null))
			{
				continue;
			}
			if (_showColliders)
			{
				hitbox._go.layer = 0;
				if (_obp._systemCollidersParent != null)
				{
					_obp._systemCollidersParent.SetActive(value: true);
				}
			}
			else
			{
				if (_obp._kinematicCollidersParent != null && _obp._systemCollidersParent != null)
				{
					_obp._systemCollidersParent.SetActive(value: false);
				}
				hitbox._go.layer = 15;
			}
			MeshRenderer component = hitbox._go.GetComponent<MeshRenderer>();
			if (component != null)
			{
				component.enabled = _showColliders;
			}
		}
	}

	public Vehicle GetMapVehicle(Taskforce taskforce)
	{
		return taskforce?.PlottingTable?.VehicleForObject(this);
	}

	public GeoPosition GetMapPositionForTaskforce(Taskforce taskforce, GeoPosition defaultGP, bool useDefaultGP = false)
	{
		GeoPosition result = (useDefaultGP ? defaultGP : Position.Value);
		Vehicle mapVehicle = GetMapVehicle(taskforce);
		if (mapVehicle != null && mapVehicle.Position.HasValue)
		{
			result = mapVehicle.Position.Value;
		}
		return result;
	}

	public void UpdateOwnPlottingState()
	{
		if (this == null)
		{
			DM.E("UpdateOwnPlottingState for null");
		}
		if (PlottingTable != null && (!_isUndetectable || !(_obp?._unitRoles?.Contains(ObjectBaseParameters.UnitRoles.Deco)).GetValueOrDefault()))
		{
			Entity value = SelfSensor.MakeTruth();
			PlottingTable.OnNext(value);
			PlottingTable.Update(GameTime.missionElapsedTime);
			if (DM_Sensors._showTruthForPlayer && _taskforce != Globals._playerTaskforce && Globals._playerTaskforce.PlottingTable != null)
			{
				Globals._playerTaskforce.PlottingTable.OnNext(SelfSensor.MakeTruth());
				Globals._playerTaskforce.PlottingTable.Update(GameTime.missionElapsedTime);
			}
		}
	}

	public void TransferToTaskforce(Taskforce newTaskforce, bool clearWaypoints = true, bool detachFromFormation = false, bool transferAircraft = true)
	{
		if (_userRoot != null && clearWaypoints)
		{
			_userRoot.ClearTasks();
		}
		for (int num = _childAircraft.Count - 1; num >= 0; num--)
		{
			if (_childAircraft[num] == null || _childAircraft[num].IsDestroyed)
			{
				_childAircraft.RemoveAt(num);
			}
			else if ((transferAircraft && !_childAircraft[num].IsControllable) || !_childAircraft[num].HasControl)
			{
				_childAircraft[num].TransferToTaskforce(newTaskforce);
			}
		}
		if (_homeBase != null)
		{
			_homeBase._childAircraft.Remove(this);
			_homeBase = null;
		}
		if (Formation != null && detachFromFormation)
		{
			Formation.DetachUnit(this);
		}
		_taskforce.RemoveTaskForceObject(this);
		Singleton<ObjectsManager>.Instance.removeObject(this);
		_taskforce = newTaskforce;
		Singleton<ObjectsManager>.Instance.AddObjectToItsTaskforce(this);
		ResetEngagements(clearAttackTasks: false);
		if (_taskforce == Globals._playerTaskforce)
		{
			SetVisible(visible: true);
		}
		DM.I($"Object {_objectIniName}  {Name.Value} transferred to {newTaskforce.Side} taskforce");
		_searchForHomeBase = true;
	}

	public void ResetEngagements(bool clearAttackTasks = true)
	{
		CeaseFire(report: false, clearOrder: true, formationCheck: true, stopJamming: true, airFormationCheck: true, clearAttackTasks);
		foreach (ObjectBase listOfAllUnit in Singleton<ObjectsManager>.Instance._listOfAllUnits)
		{
			if (listOfAllUnit == this)
			{
				continue;
			}
			if (listOfAllUnit._currentEngageTasks.Count > 0)
			{
				for (int num = listOfAllUnit._currentEngageTasks.Count - 1; num >= 0; num--)
				{
					if (listOfAllUnit._currentEngageTasks[num]._targetObject == this)
					{
						listOfAllUnit.RemoveEngageTask(num);
					}
				}
			}
			foreach (WeaponSystem weaponSystem in listOfAllUnit._obp._weaponSystems)
			{
				if (weaponSystem._executingEngageTask && weaponSystem._targetObject == this)
				{
					weaponSystem.EndEngageTask();
				}
			}
		}
	}

	public void RemoveWaypoints()
	{
		LastCommandPosition = null;
		_currentWaypointParent = null;
		_userRoot?.ClearTasks();
	}

	public void CopyWaypointsFrom(ObjectBase source)
	{
		if (source?._userRoot == null)
		{
			return;
		}
		foreach (ITaskViewModel taskViewModel in source._userRoot.TaskViewModels)
		{
			if (taskViewModel?.Task != null)
			{
				if (taskViewModel.Task is AttackAtWaypoint attackAtWaypoint)
				{
					SetAttackAtWaypointTask(AddAttackData(attackAtWaypoint, CreateWaypointData(attackAtWaypoint)));
				}
				else if (taskViewModel.Task is GoToWaypointTask wpt)
				{
					SetWaypointTask(CreateWaypointData(wpt));
				}
			}
		}
	}

	public List<WaypointData> ExportWaypoints()
	{
		List<WaypointData> list = new List<WaypointData>();
		if (_userRoot == null)
		{
			return list;
		}
		foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
		{
			if (taskViewModel?.Task != null)
			{
				if (taskViewModel.Task is AttackAtWaypoint attackAtWaypoint)
				{
					list.Add(AddAttackData(attackAtWaypoint, CreateWaypointData(attackAtWaypoint)));
				}
				else if (taskViewModel.Task is GoToWaypointTask wpt)
				{
					list.Add(CreateWaypointData(wpt));
				}
				else if (taskViewModel.Task is GoToRelativePoint wpt2)
				{
					list.Add(CreateWaypointData(wpt2));
				}
			}
		}
		return list;
	}

	public void ImportWaypoints(List<WaypointData> waypointsData)
	{
		if (_userRoot == null)
		{
			return;
		}
		foreach (WaypointData waypointsDatum in waypointsData)
		{
			if (!string.IsNullOrEmpty(waypointsDatum._ammunitionForEngage))
			{
				SetAttackAtWaypointTask(waypointsDatum);
			}
			else if (!string.IsNullOrEmpty(waypointsDatum._patrolZoneID))
			{
				PatrolZone patrolZone = Singleton<MissionManager>.Instance.FindPatrolZone(waypointsDatum._patrolZoneID);
				if (patrolZone != null)
				{
					SetRelativeWaypointTask(patrolZone, waypointsDatum._patrolZoneRelativePosition, waypointsDatum.wpName);
				}
			}
			else if (waypointsDatum._parentObject != null)
			{
				if (waypointsDatum._isRelative)
				{
					string text = "";
					if (!waypointsDatum._reachable)
					{
						text = "station";
					}
					SetRelativeWaypointTask(waypointsDatum, text);
				}
				else
				{
					SetWaypointTask(waypointsDatum);
				}
			}
			else
			{
				SetWaypointTask(waypointsDatum);
			}
		}
	}

	public WaypointData CreateWaypointData(GoToRelativePoint wpt)
	{
		Vector3 value = wpt.relativePosition.value;
		return new WaypointData
		{
			_isRelative = true,
			_geoposition = wpt.WaypointPosition,
			_parentObject = wpt.target.value,
			_parentObjectKey = (wpt.target.value?._sectionName ?? string.Empty),
			_relativePosition = new Vector3(value.z, value.y, value.x),
			_rotatesWithParent = wpt.rotatesWithTarget.value,
			_patrolZoneID = (wpt._patrolZone?._zoneID ?? ""),
			_patrolZoneRelativePosition = wpt._patrolZoneRelativePosition,
			_heightState = wpt._heightState.value,
			_reachable = wpt._isReachable,
			_timeToFollow = wpt._timeToFollow.value,
			wpName = wpt.wpName.value,
			_completionOrder = wpt._completionOrder,
			_completionTargetKey = wpt._completionTargetKey,
			_setWeaponStatus = wpt._setWeaponStatus,
			_returnToFormation = wpt._returnToFormation,
			_isAutogenerated = wpt._isAutogenerated,
			_setTelegraph = (int)(wpt._setSpeed.value ? wpt._speedState.value : ((ObjectSpeed.SpeedSettings)(-100))),
			_setEmcon = wpt._setEmcon,
			_setAirSearchRadars = wpt._setAirSearchRadars,
			_setSurfaceSearchRadars = wpt._setSurfaceSearchRadars,
			_setActiveSonars = wpt._setActiveSonars,
			_setTowedSonars = wpt._setTowedSonars,
			_towedSonarsEnabledTime = wpt._towedSonarsEnabledTime,
			_objectsToVIDList = new List<ObjectBase>(wpt._objectsToVIDList),
			_waypointsData = new List<WaypointData>(wpt._waypointsData)
		};
	}

	public WaypointData CreateWaypointData(GoToWaypointTask wpt)
	{
		WaypointData waypointData = new WaypointData();
		waypointData._heightState = wpt._heightState.value;
		waypointData._geoposition = new GeoPosition
		{
			_latitude = wpt._waypointGeoPos.value._latitude,
			_longitude = wpt._waypointGeoPos.value._longitude,
			_height = wpt._waypointGeoPos.value._height
		};
		waypointData._setTurn = wpt._setTurn.value;
		waypointData._setCourse = wpt._setCourse.value;
		waypointData._course = wpt._course.value;
		waypointData._distance = wpt._distance.value;
		waypointData._toCurrentPosition = wpt._toCurrentPosition.value;
		waypointData._parentObject = wpt._parentObject.value;
		waypointData._timeToFollow = wpt._timeToFollow.value;
		waypointData._allowFormationManeuver = wpt._allowFormationManeuver.value;
		waypointData._completionOrder = wpt._completionOrder;
		waypointData._completionTargetKey = wpt._completionTargetKey;
		waypointData._setWeaponStatus = wpt._setWeaponStatus;
		waypointData._returnToFormation = wpt._returnToFormation;
		waypointData._isAutogenerated = wpt._isAutogenerated;
		waypointData._setTelegraph = -100;
		if (wpt._setSpeed.value)
		{
			waypointData._setTelegraph = (int)wpt._speedState.value;
		}
		waypointData.wpName = wpt.wpName.value;
		waypointData._setEmcon = wpt._setEmcon;
		waypointData._setAirSearchRadars = wpt._setAirSearchRadars;
		waypointData._setSurfaceSearchRadars = wpt._setSurfaceSearchRadars;
		waypointData._setActiveSonars = wpt._setActiveSonars;
		waypointData._setTowedSonars = wpt._setTowedSonars;
		waypointData._towedSonarsEnabledTime = wpt._towedSonarsEnabledTime;
		waypointData._objectsToVIDList = wpt._objectsToVIDList;
		waypointData._waypointsData = wpt._waypointsData;
		return waypointData;
	}

	public WaypointData AddAttackData(AttackAtWaypoint awp, WaypointData wpData)
	{
		wpData._ammunitionForEngage = awp._ammunitionName;
		wpData._targetObject = awp._targetObject;
		wpData._attackGeoposition = awp._targetGeoPosition;
		wpData._salvo = awp._salvo;
		wpData._salvoType = awp._salvoType;
		wpData._areaRadius = awp._areaRadius;
		wpData._formationAttack = awp._formationAttack;
		wpData._attackOnlyDetected = awp._attackOnlyDetected;
		return wpData;
	}

	public AttackAtWaypoint SetAttackAtWaypointTask(string ammunitionName, ObjectBase targetObject, GeoPosition targetGeoPosition, GeoPosition waypointGeoPosition, int salvo, VisualActionTask after, EngageTask.SalvoType salvoType = EngageTask.SalvoType.PerLauncher, float areaRadius = -1f, bool formationAttack = true, bool attackOnlyDetected = false)
	{
		if (_userRoot == null)
		{
			return null;
		}
		Ammunition ammunitionByName = getAmmunitionByName(ammunitionName);
		if (ammunitionByName == null)
		{
			return null;
		}
		if (targetObject != null)
		{
			if (!DoesAmmoMatchTarget(ammunitionName, targetObject))
			{
				return null;
			}
			if (Formation != null && targetObject.Formation != null && Formation == targetObject.Formation)
			{
				return null;
			}
		}
		else if (DoesEngageRequiresTarget(ammunitionName))
		{
			return null;
		}
		AttackAtWaypoint attackAtWaypoint = NodeCanvas.Framework.Task.Create<AttackAtWaypoint>(_userRoot.graph);
		attackAtWaypoint._baseObject = this;
		attackAtWaypoint._ammo = ammunitionByName;
		if (attackAtWaypoint._ammo == null)
		{
			return null;
		}
		AmmunitionParameters ap = attackAtWaypoint._ammo._ap;
		attackAtWaypoint._minAltitude = ap._launchAltitudesInUnity.x + attackAtWaypoint._altOffset;
		attackAtWaypoint._maxAltitude = ap._launchAltitudesInUnity.y - attackAtWaypoint._altOffset;
		if (attackAtWaypoint._maxAltitude.value < attackAtWaypoint._minAltitude.value + Globals._weaponLaunchAltitudeOffset)
		{
			attackAtWaypoint._maxAltitude = attackAtWaypoint._minAltitude.value + Globals._weaponLaunchAltitudeOffset + 0.001f;
		}
		if (targetObject != null)
		{
			attackAtWaypoint._targetObject = targetObject;
			attackAtWaypoint._hasTargetObject = true;
		}
		else
		{
			attackAtWaypoint._targetGeoPosition = targetGeoPosition;
		}
		attackAtWaypoint._salvo = salvo;
		attackAtWaypoint._ammunitionName = ammunitionName;
		attackAtWaypoint._engageAtWaypointPosition = EngageAtWaypointPosition(targetObject, ammunitionByName._ap);
		attackAtWaypoint._isReachable = true;
		attackAtWaypoint._triggerRange = 5f * WaypointTolerance;
		double height = Position.Value._height;
		VisualActionTask selectedTask = _userRoot.SelectedTask;
		if (selectedTask != null)
		{
			height = selectedTask.WaypointPosition._height;
		}
		if (attackAtWaypoint._engageAtWaypointPosition)
		{
			attackAtWaypoint._waypointGeoPos.value = new GeoPosition
			{
				_latitude = targetGeoPosition._latitude,
				_longitude = targetGeoPosition._longitude,
				_height = height
			};
			attackAtWaypoint._triggerRange = 68.89874f;
		}
		else
		{
			attackAtWaypoint._waypointGeoPos.value = new GeoPosition
			{
				_latitude = waypointGeoPosition._latitude,
				_longitude = waypointGeoPosition._longitude,
				_height = height
			};
		}
		attackAtWaypoint._heightState = WaypointData.WaypointHeightState.NoChange;
		float num = TerrainUtils.GetHeightAt(attackAtWaypoint._waypointGeoPos.value);
		if (num < 0f)
		{
			num = 0f;
		}
		attackAtWaypoint._targetAltitude = num;
		if (ap._subType == Ammunition.Type.Sonobuoy)
		{
			attackAtWaypoint._isSonobouyDrop = true;
		}
		attackAtWaypoint._salvoType = salvoType;
		attackAtWaypoint._areaRadius = areaRadius;
		attackAtWaypoint._formationAttack = formationAttack;
		attackAtWaypoint._attackOnlyDetected = attackOnlyDetected;
		if (_taskforce != Globals._playerTaskforce)
		{
			attackAtWaypoint._engageTaskCanAttackUndetectedTargets = true;
		}
		if (after == null)
		{
			_userRoot.AddTask(attackAtWaypoint);
		}
		else
		{
			_userRoot.AddTask(attackAtWaypoint, after);
		}
		return attackAtWaypoint;
	}

	public GoToWaypointTask SetDipSonarAtWaypointTask(GeoPosition waypointGeoPosition, VisualActionTask after)
	{
		if (_userRoot == null)
		{
			return null;
		}
		GoToWaypointTask goToWaypointTask = NodeCanvas.Framework.Task.Create<GoToWaypointTask>(_userRoot.graph);
		goToWaypointTask._baseObject = this;
		goToWaypointTask._waypointGeoPos.value = new GeoPosition
		{
			_latitude = waypointGeoPosition._latitude,
			_longitude = waypointGeoPosition._longitude,
			_height = waypointGeoPosition._height
		};
		goToWaypointTask.wpName = "dip";
		goToWaypointTask._isReachable = true;
		goToWaypointTask._heightState = ((this is Helicopter) ? WaypointData.WaypointHeightState.VeryLow : WaypointData.WaypointHeightState.Number);
		goToWaypointTask._setTowedSonars = (true, true);
		goToWaypointTask._towedSonarsEnabledTime = 120f;
		goToWaypointTask._isAutogenerated = true;
		if (after != null)
		{
			_userRoot.AddTask(goToWaypointTask, after);
		}
		else
		{
			VisualActionTask selectedTask = _userRoot.SelectedTask;
			if (selectedTask != null)
			{
				_userRoot.AddTask(goToWaypointTask, selectedTask);
			}
			else
			{
				_userRoot.AddTask(goToWaypointTask);
			}
		}
		return goToWaypointTask;
	}

	public void SetAttackAtWaypointTask(WaypointData wpData)
	{
		if (_userRoot == null)
		{
			return;
		}
		ObjectBase value = wpData._targetObject;
		if (value == null && !string.IsNullOrEmpty(wpData._targetObjectKeyOrID))
		{
			if (int.TryParse(wpData._targetObjectKeyOrID, out var result))
			{
				value = Singleton<SceneCreator>.Instance.FindObjectById(result);
			}
			else
			{
				Singleton<SceneCreator>.Instance.ObjectsByPvKey.TryGetValue(wpData._targetObjectKeyOrID, out value);
			}
			if (value == null)
			{
				return;
			}
		}
		VisualActionTask after = null;
		if (_userRoot.SelectedTask != null)
		{
			after = _userRoot.SelectedTask;
		}
		AttackAtWaypoint attackAtWaypoint = SetAttackAtWaypointTask(wpData._ammunitionForEngage, value, wpData._attackGeoposition, wpData._geoposition, wpData._salvo, after, wpData._salvoType, wpData._areaRadius, wpData._formationAttack, wpData._attackOnlyDetected);
		if (attackAtWaypoint != null)
		{
			attackAtWaypoint._waypointGeoPos.value = new GeoPosition
			{
				_latitude = wpData._geoposition._latitude,
				_longitude = wpData._geoposition._longitude,
				_height = wpData._geoposition._height
			};
			attackAtWaypoint._setTurn = wpData._setTurn;
			attackAtWaypoint._setCourse = wpData._setCourse;
			attackAtWaypoint._course = wpData._course;
			attackAtWaypoint._distance = wpData._distance;
			attackAtWaypoint._toCurrentPosition = wpData._toCurrentPosition;
			attackAtWaypoint._parentObject = wpData._parentObject;
			attackAtWaypoint._timeToFollow = wpData._timeToFollow;
			attackAtWaypoint._allowFormationManeuver = wpData._allowFormationManeuver;
			attackAtWaypoint._completionOrder = wpData._completionOrder;
			attackAtWaypoint._completionTargetKey = wpData._completionTargetKey;
			attackAtWaypoint._setWeaponStatus = wpData._setWeaponStatus;
			attackAtWaypoint._returnToFormation = wpData._returnToFormation;
			attackAtWaypoint._isAutogenerated = wpData._isAutogenerated;
			if (wpData._setTelegraph > -2)
			{
				attackAtWaypoint._setSpeed = true;
				attackAtWaypoint._speedState.value = (ObjectSpeed.SpeedSettings)wpData._setTelegraph;
			}
			attackAtWaypoint._setEmcon = wpData._setEmcon;
			attackAtWaypoint._setAirSearchRadars = wpData._setAirSearchRadars;
			attackAtWaypoint._setSurfaceSearchRadars = wpData._setSurfaceSearchRadars;
			attackAtWaypoint._setActiveSonars = wpData._setActiveSonars;
			attackAtWaypoint._setTowedSonars = wpData._setTowedSonars;
			attackAtWaypoint._towedSonarsEnabledTime = wpData._towedSonarsEnabledTime;
		}
	}

	public bool EngageAtWaypointPosition(ObjectBase targetObject, AmmunitionParameters ap)
	{
		if (targetObject != null)
		{
			return false;
		}
		if (!IsAirUnit)
		{
			return false;
		}
		if (ap._type == Ammunition.Type.Bomb || ap._type == Ammunition.Type.Torpedo)
		{
			return true;
		}
		return false;
	}

	public GoToWaypointTask setWaypointTask(GeoPosition geoPos, string name = "wp", WaypointData.WaypointHeightState wpHeightState = WaypointData.WaypointHeightState.Number)
	{
		GoToWaypointTask goToWaypointTask = NodeCanvas.Framework.Task.Create<GoToWaypointTask>(_userRoot.graph);
		goToWaypointTask._baseObject = this;
		goToWaypointTask._waypointGeoPos.value = new GeoPosition
		{
			_latitude = geoPos._latitude,
			_longitude = geoPos._longitude,
			_height = geoPos._height
		};
		goToWaypointTask.wpName = name;
		goToWaypointTask._isReachable = true;
		goToWaypointTask._heightState = wpHeightState;
		VisualActionTask selectedTask = _userRoot.SelectedTask;
		if (selectedTask != null)
		{
			_userRoot.AddTask(goToWaypointTask, selectedTask);
		}
		else
		{
			_userRoot.AddTask(goToWaypointTask);
		}
		return goToWaypointTask;
	}

	public GoToWaypointTask SetWaypointTask(WaypointData wpData)
	{
		GoToWaypointTask goToWaypointTask = NodeCanvas.Framework.Task.Create<GoToWaypointTask>(_userRoot.graph);
		setWaypointTaskParameters(goToWaypointTask, wpData);
		return goToWaypointTask;
	}

	public void setWaypointTaskParameters(GoToWaypointTask wpt, WaypointData wpData)
	{
		wpt._baseObject = this;
		wpt._heightState = wpData._heightState;
		wpt._waypointGeoPos.value = new GeoPosition
		{
			_latitude = wpData._geoposition._latitude,
			_longitude = wpData._geoposition._longitude,
			_height = wpData._geoposition._height
		};
		wpt._setTurn = wpData._setTurn;
		wpt._setCourse = wpData._setCourse;
		wpt._course = wpData._course;
		wpt._distance = wpData._distance;
		wpt._toCurrentPosition = wpData._toCurrentPosition;
		wpt._parentObject = wpData._parentObject;
		wpt._timeToFollow = wpData._timeToFollow;
		wpt._allowFormationManeuver = wpData._allowFormationManeuver;
		wpt._completionOrder = wpData._completionOrder;
		wpt._completionTargetKey = wpData._completionTargetKey;
		wpt._setWeaponStatus = wpData._setWeaponStatus;
		wpt._returnToFormation = wpData._returnToFormation;
		wpt._isAutogenerated = wpData._isAutogenerated;
		if (wpData._setTelegraph > -2)
		{
			wpt._setSpeed = true;
			wpt._speedState.value = (ObjectSpeed.SpeedSettings)wpData._setTelegraph;
		}
		wpt._isReachable = true;
		wpt.wpName = wpData.wpName;
		wpt._setEmcon = wpData._setEmcon;
		wpt._setAirSearchRadars = wpData._setAirSearchRadars;
		wpt._setSurfaceSearchRadars = wpData._setSurfaceSearchRadars;
		wpt._setActiveSonars = wpData._setActiveSonars;
		wpt._setTowedSonars = wpData._setTowedSonars;
		wpt._towedSonarsEnabledTime = wpData._towedSonarsEnabledTime;
		if (wpData._objectsToVIDList.Count > 0)
		{
			wpt._objectsToVIDList = new List<ObjectBase>(wpData._objectsToVIDList);
		}
		else if (wpData._objectToVIDKeyOrID.Count > 0)
		{
			foreach (string item in wpData._objectToVIDKeyOrID)
			{
				if (!string.IsNullOrEmpty(item))
				{
					ObjectBase value;
					if (int.TryParse(item, out var result))
					{
						value = Singleton<SceneCreator>.Instance.FindObjectById(result);
					}
					else
					{
						Singleton<SceneCreator>.Instance.ObjectsByPvKey.TryGetValue(item, out value);
					}
					if (!(value == null) && !value.isDestroyed && !wpt._objectsToVIDList.Contains(value))
					{
						wpt._objectsToVIDList.Add(value);
					}
				}
			}
		}
		wpt._waypointsData = wpData._waypointsData;
		VisualActionTask selectedTask = _userRoot.SelectedTask;
		if (selectedTask != null)
		{
			_userRoot.AddTask(wpt, selectedTask);
		}
		else
		{
			_userRoot.AddTask(wpt);
		}
	}

	public void SetRelativeWaypointTask(Vector3 position, ObjectBase parentObj, string name = "wp", bool reachable = true, bool setHeight = false, bool rotatesWithTarget = true)
	{
		GoToRelativePoint goToRelativePoint = NodeCanvas.Framework.Task.Create<GoToRelativePoint>(_userRoot.graph);
		goToRelativePoint._baseObject = this;
		goToRelativePoint.target.value = parentObj;
		Vector3 vector = position;
		vector.x = position.z;
		vector.z = position.x;
		position = vector;
		goToRelativePoint.relativePosition.value = position;
		goToRelativePoint.rotatesWithTarget.value = rotatesWithTarget;
		goToRelativePoint._isReachable = reachable;
		goToRelativePoint.wpName = name;
		goToRelativePoint.setHeight = setHeight;
		VisualActionTask selectedTask = _userRoot.SelectedTask;
		if (selectedTask != null)
		{
			_userRoot.AddTask(goToRelativePoint, selectedTask);
		}
		else
		{
			_userRoot.AddTask(goToRelativePoint);
		}
	}

	public GoToRelativePoint SetRelativeWaypointTask(PatrolZone patrolZone, GeoPosition relativePosition, string name = "wp", bool reachable = true)
	{
		GoToRelativePoint goToRelativePoint = NodeCanvas.Framework.Task.Create<GoToRelativePoint>(_userRoot.graph);
		goToRelativePoint._baseObject = this;
		goToRelativePoint._patrolZone = patrolZone;
		goToRelativePoint._patrolZoneRelativePosition = relativePosition;
		goToRelativePoint._isReachable = reachable;
		goToRelativePoint.wpName = name;
		goToRelativePoint.setHeight = false;
		goToRelativePoint._heightState = WaypointData.WaypointHeightState.Current;
		VisualActionTask selectedTask = _userRoot.SelectedTask;
		if (selectedTask != null)
		{
			_userRoot.AddTask(goToRelativePoint, selectedTask);
		}
		else
		{
			_userRoot.AddTask(goToRelativePoint);
		}
		return goToRelativePoint;
	}

	public GoToRelativeToStationPoint SetRelativeToStationWaypointTask(Vector3 position, Station station, string name = "wp", bool reachable = true, bool setHeight = false, bool rotatesWithTarget = true)
	{
		GoToRelativeToStationPoint goToRelativeToStationPoint = NodeCanvas.Framework.Task.Create<GoToRelativeToStationPoint>(_userRoot.graph);
		goToRelativeToStationPoint._baseObject = this;
		goToRelativeToStationPoint._formationLeader.value = station.UnitObject.Formation.LeaderStation.UnitObject;
		goToRelativeToStationPoint._targetStation.value = station;
		Vector3 vector = position;
		vector.x = position.z;
		vector.z = position.x;
		position = vector;
		goToRelativeToStationPoint.relativePosition.value = position;
		goToRelativeToStationPoint.rotatesWithTarget.value = rotatesWithTarget;
		goToRelativeToStationPoint._isReachable = reachable;
		goToRelativeToStationPoint.wpName = name;
		goToRelativeToStationPoint.setHeight = setHeight;
		VisualActionTask selectedTask = _userRoot.SelectedTask;
		if (selectedTask != null)
		{
			_userRoot.AddTask(goToRelativeToStationPoint, selectedTask);
		}
		else
		{
			_userRoot.AddTask(goToRelativeToStationPoint);
		}
		return goToRelativeToStationPoint;
	}

	public void SetRelativeWaypointTask(WaypointData wpData, string name = "wp")
	{
		GoToRelativePoint goToRelativePoint = NodeCanvas.Framework.Task.Create<GoToRelativePoint>(_userRoot.graph);
		goToRelativePoint._baseObject = this;
		goToRelativePoint.target.value = wpData._parentObject;
		goToRelativePoint.rotatesWithTarget.value = wpData._rotatesWithParent;
		if (!wpData._rotatesWithParent)
		{
			wpData._relativePosition = Matrix4x4.Rotate(Quaternion.AngleAxis(wpData._parentObject.getHeading(), Vector3.up)) * wpData._relativePosition;
		}
		Vector3 relativePosition = wpData._relativePosition;
		relativePosition.x = wpData._relativePosition.z;
		relativePosition.z = wpData._relativePosition.x;
		goToRelativePoint.relativePosition.value = relativePosition;
		goToRelativePoint._heightState = wpData._heightState;
		goToRelativePoint._isReachable = wpData._reachable;
		goToRelativePoint.wpName = (string.IsNullOrEmpty(wpData.wpName) ? name : wpData.wpName);
		goToRelativePoint.setHeight = wpData._heightState == WaypointData.WaypointHeightState.Number;
		goToRelativePoint._timeToFollow = wpData._timeToFollow;
		goToRelativePoint._completionOrder = wpData._completionOrder;
		goToRelativePoint._completionTargetKey = wpData._completionTargetKey;
		goToRelativePoint._setWeaponStatus = wpData._setWeaponStatus;
		goToRelativePoint._returnToFormation = wpData._returnToFormation;
		goToRelativePoint._isAutogenerated = wpData._isAutogenerated;
		if (wpData._setTelegraph > -2)
		{
			goToRelativePoint._setSpeed = true;
			goToRelativePoint._speedState.value = (ObjectSpeed.SpeedSettings)wpData._setTelegraph;
		}
		goToRelativePoint._setEmcon = wpData._setEmcon;
		goToRelativePoint._setAirSearchRadars = wpData._setAirSearchRadars;
		goToRelativePoint._setSurfaceSearchRadars = wpData._setSurfaceSearchRadars;
		goToRelativePoint._setActiveSonars = wpData._setActiveSonars;
		goToRelativePoint._setTowedSonars = wpData._setTowedSonars;
		goToRelativePoint._towedSonarsEnabledTime = wpData._towedSonarsEnabledTime;
		if (wpData._objectsToVIDList.Count > 0)
		{
			goToRelativePoint._objectsToVIDList = new List<ObjectBase>(wpData._objectsToVIDList);
		}
		else
		{
			foreach (string item in wpData._objectToVIDKeyOrID)
			{
				if (!string.IsNullOrEmpty(item))
				{
					ObjectBase value;
					if (int.TryParse(item, out var result))
					{
						value = Singleton<SceneCreator>.Instance.FindObjectById(result);
					}
					else
					{
						Singleton<SceneCreator>.Instance.ObjectsByPvKey.TryGetValue(item, out value);
					}
					if (!(value == null) && !value.isDestroyed && !goToRelativePoint._objectsToVIDList.Contains(value))
					{
						goToRelativePoint._objectsToVIDList.Add(value);
					}
				}
			}
		}
		goToRelativePoint._waypointsData = new List<WaypointData>(wpData._waypointsData);
		VisualActionTask selectedTask = _userRoot.SelectedTask;
		if (selectedTask != null)
		{
			_userRoot.AddTask(goToRelativePoint, selectedTask);
		}
		else
		{
			_userRoot.AddTask(goToRelativePoint);
		}
	}

	public bool isLastWaypoint()
	{
		int num = 0;
		if (_userRoot != null)
		{
			foreach (ITaskViewModel item in _userRoot?.TaskViewModels)
			{
				if (item?.Task is GoToWaypointTask || item?.Task is GoToRelativePoint || item?.Task is GoToRelativeToStationPoint)
				{
					if (num > 0)
					{
						return false;
					}
					num++;
				}
			}
		}
		return num == 1;
	}

	public void ClearAttackTasks()
	{
		if (_userRoot == null)
		{
			return;
		}
		ObservableCollection<ITaskViewModel> taskViewModels = _userRoot.TaskViewModels;
		for (int num = taskViewModels.Count - 1; num >= 0; num--)
		{
			if (taskViewModels[num].Task is AttackAtWaypoint)
			{
				_userRoot.DeleteTask(taskViewModels[num].Task);
			}
		}
	}

	public void DeleteSelectedWaypoint()
	{
		if (_userRoot == null)
		{
			return;
		}
		for (int num = _userRoot.TaskViewModels.Count - 1; num >= 0; num--)
		{
			if (_userRoot.TaskViewModels[num].Task == _userRoot.SelectedTask)
			{
				_userRoot.DeleteTask(_userRoot.TaskViewModels[num].Task);
			}
		}
	}

	public void DeleteWaypoints(int count)
	{
		if (_userRoot == null)
		{
			return;
		}
		for (int i = 0; i < count; i++)
		{
			if (_userRoot.TaskViewModels.Count <= 0)
			{
				break;
			}
			_userRoot.DeleteTask(_userRoot.TaskViewModels[0].Task);
		}
	}

	public int GetNumberOfWaypoints()
	{
		int num = 0;
		if (_userRoot == null)
		{
			return num;
		}
		foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
		{
			if (taskViewModel?.Task is GoToWaypointTask || taskViewModel?.Task is GoToRelativePoint || taskViewModel?.Task is GoToRelativeToStationPoint)
			{
				num++;
			}
		}
		return num;
	}

	public bool HasWaypoints()
	{
		if (_userRoot == null)
		{
			return false;
		}
		foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
		{
			if (taskViewModel?.Task is GoToWaypointTask || taskViewModel?.Task is GoToRelativePoint || taskViewModel?.Task is GoToRelativeToStationPoint)
			{
				return true;
			}
		}
		return false;
	}

	public VisualActionTask GetWaypoint(int number)
	{
		int num = 0;
		if (_userRoot == null)
		{
			return null;
		}
		foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
		{
			if (taskViewModel?.Task is GoToWaypointTask || taskViewModel?.Task is GoToRelativePoint || taskViewModel?.Task is GoToRelativeToStationPoint)
			{
				num++;
			}
			if (num == number)
			{
				return taskViewModel?.Task;
			}
		}
		return null;
	}

	public bool CurrentWaypointDeploysTowedSonar()
	{
		VisualActionTask waypoint = GetWaypoint(1);
		if (waypoint != null && waypoint._setTowedSonars.setSonars)
		{
			return waypoint._setTowedSonars.enable;
		}
		return false;
	}

	public VisualActionTask GetLastWaypoint()
	{
		if (_userRoot == null)
		{
			return null;
		}
		for (int num = _userRoot.TaskViewModels.Count - 1; num >= 0; num--)
		{
			if (_userRoot.TaskViewModels[num]?.Task is GoToWaypointTask || _userRoot.TaskViewModels[num]?.Task is GoToRelativePoint || _userRoot.TaskViewModels[num]?.Task is GoToRelativeToStationPoint)
			{
				return _userRoot.TaskViewModels[num].Task;
			}
		}
		return null;
	}

	public void SetDesiredAltitudeToCurrentWaypoint()
	{
		if (_userRoot == null)
		{
			return;
		}
		foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
		{
			if (taskViewModel?.Task is GoToWaypointTask goToWaypointTask)
			{
				DesiredAltitude.Value = (float)goToWaypointTask.WaypointPosition._height;
				DesiredAltitude.Value = goToWaypointTask._storedAltitude;
				break;
			}
			if (taskViewModel?.Task is GoToRelativePoint goToRelativePoint)
			{
				DesiredAltitude.Value = (float)goToRelativePoint.WaypointPosition._height;
				break;
			}
			if (taskViewModel?.Task is GoToRelativeToStationPoint goToRelativeToStationPoint)
			{
				DesiredAltitude.Value = (float)goToRelativeToStationPoint.WaypointPosition._height;
				break;
			}
			if (taskViewModel?.Task is AttackAtWaypoint attackAtWaypoint)
			{
				DesiredAltitude.Value = (float)attackAtWaypoint.WaypointPosition._height;
				break;
			}
		}
	}

	public GeoPosition GetCurrentActiveWaypointGeoPos()
	{
		if (_userRoot != null)
		{
			foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
			{
				if (taskViewModel?.Task is GoToWaypointTask goToWaypointTask)
				{
					return goToWaypointTask.WaypointPosition;
				}
				if (taskViewModel?.Task is GoToRelativePoint goToRelativePoint)
				{
					return goToRelativePoint.WaypointPosition;
				}
				if (taskViewModel?.Task is GoToRelativeToStationPoint goToRelativeToStationPoint)
				{
					return goToRelativeToStationPoint.WaypointPosition;
				}
			}
		}
		return default(GeoPosition);
	}

	public List<(GeoPosition wpGP, WaypointData.WaypointHeightState heightState)> GetAllWaypointsGeoPositions(bool getRelative = true)
	{
		List<(GeoPosition, WaypointData.WaypointHeightState)> list = new List<(GeoPosition, WaypointData.WaypointHeightState)>();
		if (_userRoot != null)
		{
			foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
			{
				if (taskViewModel?.Task is GoToWaypointTask goToWaypointTask)
				{
					list.Add((goToWaypointTask.WaypointPosition, goToWaypointTask._heightState.value));
				}
				else if (getRelative && Formation != null)
				{
					if (taskViewModel?.Task is GoToRelativePoint goToRelativePoint)
					{
						list.Add((goToRelativePoint.WaypointPosition, goToRelativePoint._heightState.value));
					}
					else if (taskViewModel?.Task is GoToRelativeToStationPoint goToRelativeToStationPoint)
					{
						list.Add((goToRelativeToStationPoint.WaypointPosition, goToRelativeToStationPoint._heightState.value));
					}
				}
			}
		}
		return list;
	}

	public void UpdateWaypointsAltitudes(int heigthStateIndex = -1)
	{
		if (CurrentOrder.Value.OrderType == Order.Type.ReturnToBase || _userRoot == null)
		{
			return;
		}
		foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
		{
			if (taskViewModel?.Task is GoToWaypointTask || taskViewModel?.Task is AttackAtWaypoint)
			{
				GeoPosition waypointPosition = taskViewModel.Task.WaypointPosition;
				waypointPosition._height = DesiredAltitude.Value;
				taskViewModel.Task.WaypointPosition = waypointPosition;
				if (this is Submarine || this is Biologic)
				{
					taskViewModel.Task._heightState = heigthStateIndex switch
					{
						0 => WaypointData.WaypointHeightState.Surface, 
						1 => WaypointData.WaypointHeightState.Periscope, 
						2 => WaypointData.WaypointHeightState.VeryLow, 
						3 => WaypointData.WaypointHeightState.Low, 
						4 => WaypointData.WaypointHeightState.Medium, 
						5 => WaypointData.WaypointHeightState.High, 
						6 => WaypointData.WaypointHeightState.VeryHigh, 
						_ => WaypointData.WaypointHeightState.Number, 
					};
				}
			}
		}
	}

	public void CreateRandomWaypointsPattern(int numberOfWpt, float mediumLegLengthNm)
	{
		if (_userRoot != null)
		{
			for (int i = 0; i < numberOfWpt; i++)
			{
				float heading = Utils.RandomFloat(0f, 360f);
				float dInNmi = mediumLegLengthNm * Utils.RandomFloat(0.8f, 1.2f);
				GeoPosition geoPos = _geoPosition.GeoOffsetByDistanceOnHeading(dInNmi, heading);
				geoPos._height = DesiredAltitude.Value;
				setWaypointTask(geoPos);
			}
		}
	}

	public bool IsCurrentActiveWaypointRelative()
	{
		if (_userRoot != null)
		{
			foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
			{
				if (taskViewModel?.Task is GoToWaypointTask)
				{
					return false;
				}
				if (taskViewModel?.Task is GoToRelativePoint)
				{
					return true;
				}
				if (taskViewModel?.Task is GoToRelativeToStationPoint)
				{
					return true;
				}
			}
		}
		return false;
	}

	public string GetCurrentActiveWaypointName()
	{
		if (_userRoot != null)
		{
			foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
			{
				if (taskViewModel?.Task is GoToWaypointTask goToWaypointTask)
				{
					return goToWaypointTask.wpName.value;
				}
				if (taskViewModel?.Task is GoToRelativePoint goToRelativePoint)
				{
					return goToRelativePoint.wpName.value;
				}
				if (taskViewModel?.Task is GoToRelativeToStationPoint goToRelativeToStationPoint)
				{
					return goToRelativeToStationPoint.wpName.value;
				}
			}
		}
		return "";
	}

	public void ChangeCurrentActiveWaypointName(string newName)
	{
		if (_userRoot == null)
		{
			return;
		}
		foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
		{
			if (taskViewModel?.Task is GoToWaypointTask goToWaypointTask)
			{
				goToWaypointTask.wpName.value = newName;
				break;
			}
			if (taskViewModel?.Task is GoToRelativePoint goToRelativePoint)
			{
				goToRelativePoint.wpName.value = newName;
				break;
			}
			if (taskViewModel?.Task is GoToRelativeToStationPoint goToRelativeToStationPoint)
			{
				goToRelativeToStationPoint.wpName.value = newName;
				break;
			}
		}
	}

	public bool ChangeLastWaypointName(string newName)
	{
		if (_userRoot == null)
		{
			return false;
		}
		VisualActionTask waypoint = GetWaypoint(GetNumberOfWaypoints());
		if (waypoint == null)
		{
			return false;
		}
		if (waypoint is GoToWaypointTask goToWaypointTask)
		{
			goToWaypointTask.wpName.value = newName;
		}
		else if (waypoint is GoToRelativePoint goToRelativePoint)
		{
			goToRelativePoint.wpName.value = newName;
		}
		else if (waypoint is GoToRelativeToStationPoint goToRelativeToStationPoint)
		{
			goToRelativeToStationPoint.wpName.value = newName;
		}
		return true;
	}

	public List<string> GetAllWaypointsNames()
	{
		List<string> list = new List<string>();
		if (_userRoot != null)
		{
			foreach (ITaskViewModel taskViewModel in _userRoot.TaskViewModels)
			{
				if (taskViewModel?.Task is GoToWaypointTask goToWaypointTask)
				{
					list.Add(goToWaypointTask.wpName.value);
				}
				else if (taskViewModel?.Task is GoToRelativePoint goToRelativePoint)
				{
					list.Add(goToRelativePoint.wpName.value);
				}
				else if (taskViewModel?.Task is GoToRelativeToStationPoint goToRelativeToStationPoint)
				{
					list.Add(goToRelativeToStationPoint.wpName.value);
				}
			}
		}
		return list;
	}

	public float GetDistToCurrentActiveWaypoint()
	{
		float result = -1f;
		if (CommandPosition.HasValue)
		{
			result = (float)_geoPosition.GetDistance(CommandPosition.Value);
		}
		return result;
	}

	public float GetDistToCurrentActiveWaypointInMiles()
	{
		float result = -1f;
		if (CommandPosition.HasValue)
		{
			result = (float)_geoPosition.getDistanceInMiles(CommandPosition.Value);
		}
		return result;
	}

	public Vector3 getCurrentWaypointDeviation()
	{
		GeoPosition? commandPosition = CommandPosition;
		if (commandPosition.HasValue)
		{
			GeoPosition valueOrDefault = commandPosition.GetValueOrDefault();
			return Utils.longLatToLocalV3(valueOrDefault, Globals._currentCenterTile) - base.transform.position;
		}
		return Vector3.zero;
	}

	public virtual void notifyOfHit(RaycastHit hit, AmmunitionParameters.Penetration penetration, AmmunitionParameters.ImpactSize impactSize, bool hasFuel, float remainingRangePercent)
	{
		Log.Error("Base class call of notifyOfHit, derived class needs own method!", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 5676, "notifyOfHit");
	}

	public virtual float getNoise()
	{
		return 0f;
	}

	public virtual float CavitationOnsetKts()
	{
		return float.MaxValue;
	}

	public void SetVisible(bool visible)
	{
		if (_obp != null && !(_obp._visibleObjectsParent == null) && _obp._visibleObjectsParent.activeSelf != visible)
		{
			_obp._visibleObjectsParent.SetActive(visible);
		}
	}

	public virtual void SetOpticalViewModelVisible(bool visible)
	{
		if (_obp?._visibleObjectsParent == null)
		{
			return;
		}
		if (visible)
		{
			foreach (UnityEngine.Renderer opticalViewHiddenRenderer in _opticalViewHiddenRenderers)
			{
				if (opticalViewHiddenRenderer != null)
				{
					opticalViewHiddenRenderer.enabled = true;
				}
			}
			_opticalViewHiddenRenderers.Clear();
		}
		else if (_opticalViewHiddenRenderers.Count <= 0)
		{
			hideOpticalViewRenderers(_obp._visibleObjectsParent.GetComponentsInChildren<MeshRenderer>(includeInactive: true));
			hideOpticalViewRenderers(_obp._visibleObjectsParent.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true));
		}
	}

	private void hideOpticalViewRenderers<T>(T[] renderers) where T : UnityEngine.Renderer
	{
		foreach (UnityEngine.Renderer renderer in renderers)
		{
			if (!(renderer == null) && renderer.enabled)
			{
				renderer.enabled = false;
				_opticalViewHiddenRenderers.Add(renderer);
			}
		}
	}

	public void setEMCON(bool emcon, bool message = false)
	{
		Emcon.Value = emcon;
		if (Emcon.Value && _obp != null)
		{
			foreach (SensorSystemRadar airSearchRadar in _obp._airSearchRadars)
			{
				airSearchRadar.Disable();
			}
			foreach (SensorSystemRadar surfaceSearchRadar in _obp._surfaceSearchRadars)
			{
				surfaceSearchRadar.Disable();
			}
			foreach (SensorSystemSonar activeSonarSystem in _obp._activeSonarSystems)
			{
				activeSonarSystem._sonar.IsActive.Value = false;
			}
			foreach (SensorSystemECM ecmSystem in _obp._ecmSystems)
			{
				ecmSystem._ecm.ceaseOffensiveJam();
			}
			foreach (SensorSystemSonar towedSonarSystem in _obp._towedSonarSystems)
			{
				if (towedSonarSystem.IsActiveSensor)
				{
					towedSonarSystem.Disable();
					towedSonarSystem._sonar.IsActive.Value = false;
				}
			}
			foreach (TowedSystem towedSystem in _obp._towedSystems)
			{
				if (towedSystem._isDecoySystem)
				{
					towedSystem.getDecoySystem().Disable();
				}
			}
			IsAirSearchRadarsOn.Value = false;
			IsSurfaceSearchRadarsOn.Value = false;
			IsActiveSonarsOn.Value = false;
		}
		if (message && IsPlayerObject)
		{
			Singleton<VoiceMessage>.Instance.PlayVoiceMessage("EMCON", this);
		}
	}

	public void CheckEMCON()
	{
		if (_obp == null)
		{
			return;
		}
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		bool flag5 = false;
		Emcon.Value = true;
		foreach (SensorSystemRadar airSearchRadar in _obp._airSearchRadars)
		{
			if (airSearchRadar.IsOn.Value)
			{
				flag = true;
				Emcon.Value = false;
				break;
			}
		}
		if (IsAirSearchRadarsOn.Value != flag)
		{
			IsAirSearchRadarsOn.Value = flag;
		}
		foreach (SensorSystemRadar surfaceSearchRadar in _obp._surfaceSearchRadars)
		{
			if (surfaceSearchRadar.IsOn.Value)
			{
				flag2 = true;
				Emcon.Value = false;
				break;
			}
		}
		if (IsSurfaceSearchRadarsOn.Value != flag2)
		{
			IsSurfaceSearchRadarsOn.Value = flag2;
		}
		foreach (SensorSystemSonar activeSonarSystem in _obp._activeSonarSystems)
		{
			if (activeSonarSystem.IsOn.Value && activeSonarSystem._sonar.IsActive.Value)
			{
				flag3 = true;
				Emcon.Value = false;
				break;
			}
		}
		if (IsActiveSonarsOn.Value != flag3)
		{
			IsActiveSonarsOn.Value = flag3;
		}
		foreach (SensorSystemSonar towedSonarSystem in _obp._towedSonarSystems)
		{
			if (towedSonarSystem.IsActiveSensor && towedSonarSystem._sonar.IsActive.Value && towedSonarSystem.IsOn.Value)
			{
				flag3 = true;
				Emcon.Value = false;
				break;
			}
		}
		if (IsActiveSonarsOn.Value != flag3)
		{
			IsActiveSonarsOn.Value = flag3;
		}
		foreach (TowedSystem towedSystem in _obp._towedSystems)
		{
			if (towedSystem._isDecoySystem && towedSystem.getDecoySystem().IsOn.Value)
			{
				flag4 = true;
				Emcon.Value = false;
				break;
			}
		}
		if (IsTowedDecoyDeployed.Value != flag4)
		{
			IsTowedDecoyDeployed.Value = flag4;
		}
		foreach (SensorSystemECM ecmSystem in _obp._ecmSystems)
		{
			ECMParameters.ECMType type = ecmSystem._ecm._ep._type;
			if ((type == ECMParameters.ECMType.Offensive || (type == ECMParameters.ECMType.Universal && ecmSystem.IsOn.Value)) && ecmSystem._associatedTargets.Count > 0)
			{
				flag5 = true;
				Emcon.Value = false;
				break;
			}
		}
		if (IsOffensivelyJamming.Value != flag5)
		{
			IsOffensivelyJamming.Value = flag5;
		}
		if (_navLightsAreFlickering)
		{
			return;
		}
		if (Compartments != null && Compartments._immobile)
		{
			foreach (MeshRenderer navLightsRenderer in _obp._navLightsRenderers)
			{
				if (navLightsRenderer.enabled)
				{
					navLightsRenderer.enabled = false;
				}
			}
			return;
		}
		if (_taskforce == Globals._neutralTaskforce)
		{
			return;
		}
		foreach (MeshRenderer navLightsRenderer2 in _obp._navLightsRenderers)
		{
			if (Emcon.Value && navLightsRenderer2.enabled)
			{
				navLightsRenderer2.enabled = false;
			}
			else if (!Emcon.Value && !navLightsRenderer2.enabled)
			{
				navLightsRenderer2.enabled = true;
			}
		}
	}

	public void setPlayerCommandOverride(bool commandOverride)
	{
		if (IsControllable)
		{
			_playerCommandOverride = commandOverride;
		}
	}

	public void setPlayerHeightControl(bool commandOverride)
	{
		_playerHeightControl = commandOverride;
	}

	private void dropWakeBubble()
	{
		float num = Mathf.Abs(getVelocityInKnots());
		if (num < 0.33f)
		{
			num = 0f;
		}
		if (_wakeBubbleDropTimer < _wakeBubbleDropInterval)
		{
			_wakeBubbleDropTimer += GameTime.deltaTime;
		}
		if (getHeightInFeet() > -33f && _wakeBubbleDropTimer >= _wakeBubbleDropInterval && num > 1f)
		{
			CreateWakeBubble();
		}
		if (getHeightInFeet() > -33f && num > 1f && _tailWakeBubble == null)
		{
			CreateWakeBubble(isTailWakeBubble: true);
		}
		_wakeBubbleDropInterval = getWakeBubbleDropInterval(num);
		if (_tailWakeBubble != null && getHeightInFeet() > -33f && num > 1f)
		{
			_tailWakeBubble.overrideLiftime(float.MaxValue);
			float num2 = -0.07440469f;
			float z = 0f - Compartments.getBounds().size.z / 2f + 0.29761875f;
			Vector3 vector = base.transform.TransformVector(new Vector3(0f, num2, z));
			if (vector.y < num2)
			{
				vector.y = num2;
			}
			_tailWakeBubble._rootObject.transform.position = base.transform.position + vector;
		}
	}

	private void CreateWakeBubble(bool isTailWakeBubble = false)
	{
		GameObject gameObject = new GameObject();
		gameObject.SetActive(value: true);
		if (isTailWakeBubble)
		{
			float num = -0.07440469f;
			float z = 0f - Compartments.getBounds().size.z / 2f + 0.29761875f;
			Vector3 vector = base.transform.TransformVector(new Vector3(0f, num, z));
			if (vector.y < num)
			{
				vector.y = num;
			}
			gameObject.transform.position = base.transform.position + vector;
			_tailWakeBubble = gameObject.AddComponent<WakeBubble>();
			_tailWakeBubble.init(this, gameObject);
			gameObject.name = "wakebubble tail " + _tailWakeBubble.getName();
			Singleton<ObjectsManager>.Instance.AddObjectToItsTaskforce(_tailWakeBubble);
			_wakeBubbleDropTimer = 0f;
			_tailWakeBubble.overrideLiftime(float.MaxValue);
			_tailWakeBubble._isPermamentTailWakeBubble = true;
		}
		else
		{
			int num2 = Singleton<Environment>.Instance.SeaState;
			if (num2 < 0)
			{
				num2 = 0;
			}
			else if (num2 > 12)
			{
				num2 = 12;
			}
			float num3 = 0f - Constants.WAVE_HEIGHTS[num2];
			if (num3 > -0.054f)
			{
				num3 = -0.054f;
			}
			gameObject.transform.position = new Vector3(base.transform.position.x, num3, base.transform.position.z);
			WakeBubble wakeBubble = gameObject.AddComponent<WakeBubble>();
			wakeBubble.init(this, gameObject);
			gameObject.name = "wakebubble " + wakeBubble.getName();
			Singleton<ObjectsManager>.Instance.AddObjectToItsTaskforce(wakeBubble);
			_wakeBubbleDropTimer = 0f;
		}
		gameObject.transform.SetParent(Singleton<Environment>.Instance._staticWakebubblesParent);
	}

	public float getWakeBubbleDropInterval(float launchPlatformSpeed)
	{
		if (launchPlatformSpeed != 0f)
		{
			return Globals.WakeBubbleParameters._dropInterval / (launchPlatformSpeed / 30f);
		}
		return 0f;
	}

	public void SetCodeAnimsForTaxi()
	{
		SetCodeAnim("Canopy_Retract");
		SetCodeAnim("Wings_Extend");
		SetCodeAnim("Gear_Retract");
	}

	public void PlayCodeAnimsForFlight(bool playSTOLAnims = true)
	{
		PlayCodeAnim("Gear_Retract");
		PlayCodeAnim("Flaps_Takeoff_Retract");
		if (playSTOLAnims && _obp._codeAnimations.ContainsKey("Nozzles_STOL_Retract"))
		{
			PlayCodeAnim("Nozzles_STOL_Retract");
		}
	}

	public void PlayCodeAnim(string animationKey, bool logErrors = false)
	{
		ObjectCodeAnimation value = null;
		if (_obp._codeAnimations.TryGetValue(animationKey, out value))
		{
			_playingCodeAnims = true;
			_codeAnimationsToPlay.Add(value);
			value.playAnim();
		}
		else if (logErrors)
		{
			Log.Error("Could not find animation " + animationKey + " for " + _obp._objectIniName, Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 5997, "PlayCodeAnim");
		}
	}

	public ObjectCodeAnimation GetCodeAnimationByName(string animationKey, bool logErrors = false)
	{
		ObjectCodeAnimation value = null;
		if (_obp._codeAnimations.TryGetValue(animationKey, out value))
		{
			return value;
		}
		if (logErrors)
		{
			Log.Error("Could not find animation " + animationKey + " for " + _obp._objectIniName, Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 6004, "GetCodeAnimationByName");
		}
		return null;
	}

	public void SetCodeAnim(string animationKey, bool logErrors = false, bool setOpen = false)
	{
		ObjectCodeAnimation value = null;
		if (_obp._codeAnimations.TryGetValue(animationKey, out value))
		{
			if (setOpen)
			{
				value.setOpenState();
			}
			else
			{
				value.setClosedState();
			}
		}
		else if (logErrors)
		{
			Log.DebugWarning("Could not find animation " + animationKey + " for " + _obp._objectIniName, Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 6016, "SetCodeAnim");
		}
	}

	public void UpdateCodeAnims()
	{
		for (int num = _codeAnimationsToPlay.Count - 1; num >= 0; num--)
		{
			if (!_codeAnimationsToPlay[num].isPlaying())
			{
				_codeAnimationsToPlay.RemoveAt(num);
			}
			else
			{
				_codeAnimationsToPlay[num].update();
			}
		}
		if (_codeAnimationsToPlay.Count < 1)
		{
			_playingCodeAnims = false;
		}
	}

	public bool getDamageModelActive()
	{
		return _damageModelActive;
	}

	public void setDamageModelActive(bool active)
	{
		_damageModelActive = active;
		_obp._damageObjectParent.SetActive(active);
		if (!_obp._hasDamageLODs && _obp._damagedMeshesObject[0] != null && !_obp._damagedMeshesObject[0].activeSelf)
		{
			_obp._damagedMeshesObject[0].SetActive(value: true);
		}
		DamageModel[] damageModels = _obp._damageModels;
		if (damageModels != null && damageModels.Length != 0)
		{
			if (active)
			{
				int num = Globals._rnd.Next(0, _obp._damageModels.Length);
				DamageModel damageModel = _obp._damageModels[num];
				if (damageModel._mesh != null)
				{
					damageModel._mesh.SetActive(value: true);
				}
				else
				{
					Log.Error("There is no damage model for " + _objectIniName, Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 6061, "setDamageModelActive");
				}
				if (_type == ObjectType.Aircraft)
				{
					((Aircraft)this)._damagedWingSide = _obp._damageModels[num]._side;
					((Aircraft)this).UpdateExhaustDamageTint();
				}
				if (_obp._numberedObjectsToDisableWhenDamaged.ContainsKey(num))
				{
					foreach (GameObject item in _obp._numberedObjectsToDisableWhenDamaged[num])
					{
						item.SetActive(value: false);
					}
				}
			}
			else
			{
				DamageModel[] damageModels2 = _obp._damageModels;
				for (int i = 0; i < damageModels2.Length; i++)
				{
					damageModels2[i]._mesh.SetActive(value: false);
				}
				foreach (KeyValuePair<int, List<GameObject>> item2 in _obp._numberedObjectsToDisableWhenDamaged)
				{
					foreach (GameObject item3 in item2.Value)
					{
						item3.SetActive(value: true);
					}
				}
			}
		}
		foreach (GameObject item4 in _obp._objectsToDisableWhenDamaged)
		{
			item4.SetActive(!active);
		}
		foreach (GameObject item5 in _obp._subModelsToHide)
		{
			item5.SetActive(value: false);
		}
	}

	public void setDestroyedFlag(bool score = true, TacView.TCEvent eventType = TacView.TCEvent.Destroyed)
	{
		if (isUnit())
		{
			if (score && !_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Deco))
			{
				_taskforce._lossScore += _obp._unitScoreValue;
				_taskforce.RegisterUnitStatus(useTmpList: false, this, destroyed: true);
				if (_obp._flightDeck != null)
				{
					foreach (VehicleTypeOnBoard item in _obp._flightDeck._vehiclesOnBoard)
					{
						_taskforce._lossScore += item._unitScoreValue * (float)item.Numbers;
						_taskforce.RegisterNotLaunchedVehicle(useTmpList: false, item, item.Numbers, Nation.Value, destroyed: true);
					}
					foreach (FlightDeckTask flightDeckTask in _obp._flightDeck.FlightDeckTasks)
					{
						if (flightDeckTask is PendingLaunchTask pendingLaunchTask && pendingLaunchTask.VehicleType != null)
						{
							_taskforce._lossScore += pendingLaunchTask.VehicleType._unitScoreValue * (float)pendingLaunchTask.LaunchCount;
							_taskforce.RegisterNotLaunchedVehicle(useTmpList: false, pendingLaunchTask.VehicleType, pendingLaunchTask.LaunchCount, Nation.Value, destroyed: true);
						}
					}
				}
				if (!IsAirUnit)
				{
					Taskforce taskforce = null;
					float num = -1f;
					foreach (KeyValuePair<Taskforce, float> item2 in Info._damageByTaskforce)
					{
						if (item2.Value > num)
						{
							num = item2.Value;
							taskforce = item2.Key;
						}
					}
					if (taskforce != null)
					{
						if (_taskforce == Globals._neutralTaskforce)
						{
							if (_obp._isCivilian)
							{
								taskforce._civKillScore += _obp._unitScoreValue;
							}
							else
							{
								taskforce._neutralMilitaryKillScore += _obp._unitScoreValue;
							}
						}
						else
						{
							taskforce._killScore += _obp._unitScoreValue;
						}
						if (_obp._flightDeck != null)
						{
							foreach (VehicleTypeOnBoard item3 in _obp._flightDeck._vehiclesOnBoard)
							{
								if (_taskforce == Globals._neutralTaskforce)
								{
									if (item3._fileName.Split('_')[0] == "civ")
									{
										taskforce._civKillScore += item3._unitScoreValue * (float)item3.Numbers;
									}
									else
									{
										taskforce._neutralMilitaryKillScore += item3._unitScoreValue * (float)item3.Numbers;
									}
								}
								else
								{
									taskforce._killScore += item3._unitScoreValue * (float)item3.Numbers;
								}
							}
							foreach (FlightDeckTask flightDeckTask2 in _obp._flightDeck.FlightDeckTasks)
							{
								if (!(flightDeckTask2 is PendingLaunchTask pendingLaunchTask2) || pendingLaunchTask2.VehicleType == null)
								{
									continue;
								}
								if (_taskforce == Globals._neutralTaskforce)
								{
									if (pendingLaunchTask2.VehicleType._fileName.Split('_')[0] == "civ")
									{
										taskforce._civKillScore += pendingLaunchTask2.VehicleType._unitScoreValue * (float)pendingLaunchTask2.LaunchCount;
									}
									else
									{
										taskforce._neutralMilitaryKillScore += pendingLaunchTask2.VehicleType._unitScoreValue * (float)pendingLaunchTask2.LaunchCount;
									}
								}
								else
								{
									taskforce._killScore += pendingLaunchTask2.VehicleType._unitScoreValue * (float)pendingLaunchTask2.LaunchCount;
								}
							}
						}
					}
				}
			}
			if (this is Vessel || this is Submarine)
			{
				Compartments.InitialiseDamageControlTeamsNumbers();
				Compartments._damageControlTeamsDisabledTimers.Clear();
			}
			CreateEternalTrackForPlayer();
			foreach (BaseSystem system in _obp._systems)
			{
				system.MakeInoperable();
			}
			foreach (MeshRenderer crewRenderer in _obp._crewRenderers)
			{
				crewRenderer.enabled = false;
			}
			foreach (ObjectBase item4 in _ai._contactsToInvestigate)
			{
				item4._ai._enemiesInvestigatingThisUnit.Remove(this);
			}
			_ai._contactsToInvestigate.Clear();
		}
		if (Singleton<TacView>.InstanceExists())
		{
			Singleton<TacView>.Instance.OnEvent(eventType, this);
		}
		IsDestroyed = true;
		IsControllable = false;
		if (!IsFormationLeader)
		{
			RemoveWaypoints();
		}
		if (this is Submarine && !Compartments._isSinking)
		{
			Compartments.Sink(Compartments.SinkFocus.All);
		}
	}

	public void CreateEternalTrackForPlayer()
	{
		if (Globals._playerTaskforce.PlottingTable != null)
		{
			Globals._playerTaskforce.PlottingTable.OnNext(SelfSensor.MakeTruth(float.MaxValue));
			Globals._playerTaskforce.PlottingTable.Update(GameTime.missionElapsedTime);
		}
	}

	public void notifyOfExternalDestruction()
	{
		if (DM._canKill && (!DM._godMode || !IsPlayerUnit) && !IsDestroyed && !_externalDestructionNotified)
		{
			if (_obp != null)
			{
				setDamageModelActive(active: true);
			}
			_externalDestructionNotified = true;
		}
	}

	public float GetPenetrationDamageMultiplier(AmmunitionParameters.Penetration penetration, BaseSystem.ArmorType armorType, float penetrationBonus = 0f)
	{
		if (armorType == BaseSystem.ArmorType.None)
		{
			return 1f;
		}
		Vector2 minMaxDamageMultiplier = getMinMaxDamageMultiplier(armorType, penetration);
		if (Utils.RandomFloat(0f, 1f) - penetrationBonus < minMaxDamageMultiplier.y)
		{
			return Utils.RandomFloat(minMaxDamageMultiplier.x, minMaxDamageMultiplier.y);
		}
		return 0f;
	}

	private Vector2 getMinMaxDamageMultiplier(BaseSystem.ArmorType armorType, AmmunitionParameters.Penetration penetration)
	{
		Globals._damageValues._penVsArmorMatrix.TryGetValue((int)armorType, out var value);
		return value[(int)penetration];
	}

	public IEnumerator FlickerLights(float duration, float minInterval, float maxInterval, bool setLightsEndState = false, bool lightsEndState = true)
	{
		_navLightsAreFlickering = true;
		double endTime = GameTime.missionElapsedTime + (double)duration;
		bool state = false;
		double nextLightsToggleTime = -1.0;
		while (GameTime.missionElapsedTime < endTime)
		{
			if (GameTime.missionElapsedTime > nextLightsToggleTime)
			{
				foreach (MeshRenderer navLightsRenderer in _obp._navLightsRenderers)
				{
					navLightsRenderer.enabled = state;
				}
				state = !state;
				nextLightsToggleTime = GameTime.missionElapsedTime + (double)Utils.RandomFloat(minInterval, maxInterval);
			}
			yield return new WaitForFixedUpdate();
		}
		_navLightsAreFlickering = false;
		if (!setLightsEndState)
		{
			yield break;
		}
		foreach (MeshRenderer navLightsRenderer2 in _obp._navLightsRenderers)
		{
			navLightsRenderer2.enabled = lightsEndState;
		}
	}

	public void initBehaviourTree(string overrideSingleUnitBehaviourTreePath = "")
	{
		_agent = base.gameObject.AddComponent<BehaviourTreeOwner>();
		_agent.blackboard = base.gameObject.AddComponent<Blackboard>();
		if (!AIPlayer.IsForcedToDisable)
		{
			if (overrideSingleUnitBehaviourTreePath != "")
			{
				BehaviourTree behaviourTree = Resources.Load<BehaviourTree>(overrideSingleUnitBehaviourTreePath);
				if (behaviourTree == null)
				{
					UnityEngine.Debug.LogError("Specific AIUnit behaviour " + overrideSingleUnitBehaviourTreePath + " not found!");
				}
				else
				{
					_agent.behaviour = UnityEngine.Object.Instantiate(behaviourTree);
					_agent.repeat = false;
					_agent.updateInterval = 0f;
				}
			}
			else if (_taskforce.Equals(Globals._enemyTaskforce))
			{
				BehaviourTree behaviourTree2 = Resources.Load<BehaviourTree>(AIPlayer.EnemyAIUnitBehaviourPath);
				if (AIPlayer.EnemyAIUnitBehaviourPath == "" || behaviourTree2 == null)
				{
					DM_AI.LogAIUnitMessage(DM_AI.TabCategory.DebugMessages, UniqueID, "Default AIUnit behaviour " + AIPlayer.EnemyAIUnitBehaviourPath + " not found!");
				}
				else
				{
					_agent.behaviour = UnityEngine.Object.Instantiate(behaviourTree2);
					_agent.repeat = false;
					_agent.updateInterval = 0f;
				}
			}
		}
		if (_agent.behaviour == null)
		{
			BehaviourTree behaviourTree3 = ScriptableObject.CreateInstance<BehaviourTree>();
			behaviourTree3.primeNode = behaviourTree3.AddNode<UserRootNode>();
			_agent.behaviour = UnityEngine.Object.Instantiate(behaviourTree3);
			_agent.repeat = true;
			_agent.updateInterval = 0f;
		}
		if (_agent.behaviour != null)
		{
			_behaviourTree = _agent.behaviour;
			_agent.StartBehaviour(_agent.behaviour);
			_userRoot = UserRootNode.UserRootInGraph(_agent.graph);
			if (_userRoot != null)
			{
				_userRoot.TargetObject = this;
			}
			else
			{
				UnityEngine.Debug.LogError("UserRootNode not found in the graph for " + base.name + "!");
			}
		}
		else
		{
			UnityEngine.Debug.LogError("ERROR: behaviour for " + base.name + " is not assigned for some reason!");
		}
	}

	public void setOrder(Order.Type type, ObjectBase targetObject, bool displayOrderText)
	{
		if ((!(this is Aircraft aircraft) || aircraft._isInFlight) && (!(this is Helicopter helicopter) || helicopter._isInFlight))
		{
			if (type != 0)
			{
				_speedSetByPlayer = false;
			}
			CurrentOrder.Value.setOrder(type, targetObject, _defaultOrderGeoPosition, displayOrderText);
		}
	}

	public void ShowOrderFeedback(string text, float durationSeconds = 5f)
	{
		int feedbackVersion = ++_orderFeedbackVersion;
		OrderFeedbackText.Value = text;
		Observable.Timer(TimeSpan.FromSeconds(durationSeconds)).ObserveOnMainThread().Subscribe(delegate
		{
			if (_orderFeedbackVersion == feedbackVersion)
			{
				OrderFeedbackText.Value = "";
			}
		})
			.AddTo(_disposer);
	}

	public void setOrder(Order.Type type, GeoPosition geoPosition, bool displayOrderText)
	{
		if (type != 0)
		{
			_speedSetByPlayer = false;
		}
		CurrentOrder.Value.setOrder(type, null, geoPosition, displayOrderText);
	}

	public LoadoutCapabilities GetLoadoutCapabilities()
	{
		LoadoutCapabilities result = default(LoadoutCapabilities);
		float num = 330.71396f;
		float num2 = 1240.1774f;
		float num3 = 826.78485f;
		float num4 = 1818.9268f;
		result.Range_AAW = 0f;
		result.Range_ASuW = 0f;
		result.Range_ASW = 0f;
		foreach (KeyValuePair<string, int> item in AmmunitionAmountDictionary)
		{
			if (item.Value < 1)
			{
				continue;
			}
			if (!AmmunitionNameToAmmunitionDictionary.TryGetValue(item.Key, out var value) || value?._ap == null)
			{
				if (_missingAmmunitionReferencesLogged.Add(item.Key))
				{
					string text = _obp?._objectIniName ?? base.name ?? "<unknown>";
					Log.Warning($"Unit '{text}' (id={UniqueID}): Ignoring {item.Value} round(s) of unregistered ammunition '{item.Key}' while calculating loadout capabilities. Check the unit, loadout, save, and active mod data.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 6456, "GetLoadoutCapabilities");
				}
				continue;
			}
			AmmunitionParameters ap = value._ap;
			if (ap._type == Ammunition.Type.Projectile)
			{
				result._hasGun = true;
			}
			else if (ap._type == Ammunition.Type.ASROC)
			{
				result._hasASWMissiles = true;
			}
			else if (ap._type == Ammunition.Type.Missile)
			{
				if (ap._targetType == Ammunition.Target.AAW || ap._secondaryTargetType == Ammunition.Target.AAW)
				{
					if (ap._launchRangesInUnity.y > num2)
					{
						result._hasLRAAM = true;
					}
					else if (ap._launchRangesInUnity.y > num)
					{
						result._hasMRAAM = true;
					}
					else
					{
						result._hasSRAAM = true;
					}
					result._totalAAMRange += ap._launchRangesInUnity.y * (float)item.Value;
				}
				if (ap._guidanceType == AmmunitionParameters.GuidanceType.AntiRadiationHoming)
				{
					result._hasARM = true;
				}
				else if (ap._targetType == Ammunition.Target.ASuW || (IsAirUnit && ap._secondaryTargetType == Ammunition.Target.ASuW))
				{
					if (ap._launchRangesInUnity.y > num4)
					{
						result._hasLRAGM = true;
					}
					else if (ap._launchRangesInUnity.y > num3)
					{
						result._hasMRAGM = true;
					}
					else
					{
						result._hasSRAGM = true;
					}
					if (!ap._canNotAttackTypes.Contains(ObjectType.Vessel))
					{
						result._hasAShM = true;
					}
					if (!ap._terrainFollowFlight)
					{
						float num5 = 1000f;
						if (num5 > ap._maxLoftAltUnity && ap._maxLoftAltUnity > 0f)
						{
							num5 = ap._maxLoftAltUnity;
						}
						if (num5 > ap._seaSkimmingAltUnity && ap._seaSkimmingAltUnity > 0f)
						{
							num5 = ap._seaSkimmingAltUnity;
						}
						if (num5 > ap._finalFlightPhaseAltUnity && ap._finalFlightPhaseAltUnity > 0f)
						{
							num5 = ap._finalFlightPhaseAltUnity;
						}
						if (num5 < 220472.66f)
						{
							result._missileAttackRequiresTerrainCheck = true;
						}
					}
				}
				if (ap._targetType == Ammunition.Target.ASW || ap._secondaryTargetType == Ammunition.Target.ASW)
				{
					result._hasASWMissiles = true;
				}
			}
			else if (ap._type == Ammunition.Type.Bomb)
			{
				if (ap._subType == Ammunition.Type.Sonobuoy)
				{
					result._hasSonobuoys = true;
				}
				else if (ap._subType == Ammunition.Type.Cluster)
				{
					result._hasCluster = true;
				}
				else if (ap._subType == Ammunition.Type.Fueltank)
				{
					result._hasFueltanks = true;
				}
				else
				{
					result._hasBombs = true;
				}
			}
			else if (ap._type == Ammunition.Type.Torpedo)
			{
				if (ap._targetType == Ammunition.Target.ASW || ap._secondaryTargetType == Ammunition.Target.ASW)
				{
					result._hasASWTorpedoes = true;
				}
				if (ap._targetType == Ammunition.Target.ASuW || ap._secondaryTargetType == Ammunition.Target.ASuW)
				{
					result._hasASuWTorpedoes = true;
				}
			}
			else if (ap._type == Ammunition.Type.AerialRocket || ap._type == Ammunition.Type.MLRS)
			{
				result._hasRockets = true;
			}
			if (ap._type != Ammunition.Type.Chaff && ap._type != Ammunition.Type.Noisemaker && ap._type != 0 && ap._subType != Ammunition.Type.Sonobuoy)
			{
				if ((ap._targetType == Ammunition.Target.AAW || ap._secondaryTargetType == Ammunition.Target.AAW) && result.Range_AAW < ap._launchRangesInUnity.y)
				{
					result.Range_AAW = ap._launchRangesInUnity.y;
				}
				if ((ap._targetType == Ammunition.Target.ASuW || ap._secondaryTargetType == Ammunition.Target.ASuW) && result.Range_ASuW < ap._launchRangesInUnity.y)
				{
					result.Range_ASuW = ap._launchRangesInUnity.y;
				}
				if ((ap._targetType == Ammunition.Target.ASW || ap._secondaryTargetType == Ammunition.Target.ASW) && result.Range_ASW < ap._launchRangesInUnity.y)
				{
					result.Range_ASW = ap._launchRangesInUnity.y;
				}
			}
		}
		foreach (SensorSystemECM ecmSystem in _obp._ecmSystems)
		{
			if (ecmSystem._ecm._ep._type != 0)
			{
				result._hasJammer = true;
				break;
			}
		}
		return result;
	}

	public void SetLevelAttackCapabilities()
	{
		IniHandler iniHandler = Utils.openIniFile("/" + _obp._objectIniFolder, "/" + _obp._objectIniName + ".ini");
		string text = "WeaponSystem1" + _obp._loadoutVariant;
		if (iniHandler.doesSectionExist(text + "_" + _obp._nation))
		{
			text = text + "_" + _obp._nation;
		}
		if (!iniHandler.doesKeyExist(text, "LevelAttack"))
		{
			return;
		}
		if (iniHandler.readValue(text, "LevelAttack", "") == "All")
		{
			foreach (AI.LevelAttackCapability item in Enum.GetValues(typeof(AI.LevelAttackCapability)).Cast<AI.LevelAttackCapability>())
			{
				_ai._levelAttackCapabilities.Add(item);
			}
			return;
		}
		_ai._levelAttackCapabilities = iniHandler.readValue(text, "LevelAttack", _ai._levelAttackCapabilities);
	}

	public void LoadSignalProcessingOffset()
	{
		if (_obp != null)
		{
			IniHandler iniHandler = Utils.openIniFile("/" + _obp._objectIniFolder, "/" + _obp._objectIniName + ".ini");
			if (iniHandler != null)
			{
				int defaultValue = iniHandler.readValue("SensorData", "SignalProcessingOffset", 0);
				_signalProcessingOffset = iniHandler.readValue("SensorData", "SignalProcessingOffset" + _obp._loadoutVariant, defaultValue);
				_signalProcessingOffset += _obp._combatSystemSignalProcessing;
			}
		}
	}

	public bool HasAutomaticMount()
	{
		if (_obp?._weaponSystems == null)
		{
			return false;
		}
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			if (weaponSystem?._vwp != null && weaponSystem._vwp._automatic && !weaponSystem.Inoperable.Value)
			{
				return true;
			}
		}
		return false;
	}

	public bool HasRole(ObjectBaseParameters.UnitRoles role)
	{
		if (_obp == null)
		{
			return false;
		}
		return _obp._unitRoles.Contains(role);
	}

	public bool IsInCombat(bool checkFormation = true)
	{
		if (_inCombatOverride)
		{
			return true;
		}
		if (_currentEngageTasks.Count > 0)
		{
			return true;
		}
		if (Aligning)
		{
			return true;
		}
		if (checkFormation && FollowingFormation)
		{
			foreach (Station station in Formation.Stations)
			{
				if (!(station.UnitObject == null) && !(station.UnitObject == this) && !station.UnitActsIndependently.Value && station.UnitObject.IsInCombat(checkFormation: false))
				{
					return true;
				}
			}
		}
		foreach (ObjectBase listOfAllObject in Singleton<ObjectsManager>.Instance._listOfAllObjects)
		{
			if (listOfAllObject == null || listOfAllObject.GetMapVehicle(_taskforce) == null)
			{
				continue;
			}
			if (listOfAllObject is WeaponBase weaponBase)
			{
				if (weaponBase.CurrentStrongestTargetEcho != null)
				{
					if (weaponBase.CurrentStrongestTargetEcho._objectBase == this)
					{
						return true;
					}
				}
				else if (weaponBase.CurrentIntendedTargetObject == this)
				{
					return true;
				}
			}
			else
			{
				if (!listOfAllObject.isUnit())
				{
					continue;
				}
				foreach (EngageTask currentEngageTask in listOfAllObject._currentEngageTasks)
				{
					if (currentEngageTask._targetObject == this)
					{
						return true;
					}
				}
				foreach (AlignUnitToTarget alignmentTask in listOfAllObject._alignmentTasks)
				{
					if (alignmentTask._targetObject == this)
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	public void CheckForThreateningMissile()
	{
		if (this == null || !(GameTime.missionElapsedTime - _missileCheckTime > (double)_missileCheckInterval))
		{
			return;
		}
		_missileCheckTime = GameTime.missionElapsedTime;
		for (int num = _dangerousMissiles.Count - 1; num >= 0; num--)
		{
			Missile missile2 = _dangerousMissiles[num];
			if (missile2.IsDestroyed)
			{
				_dangerousMissiles.RemoveAt(num);
			}
			else
			{
				bool flag = missile2.IsGivingRWRWarning(this);
				if (!_threats.Contains(missile2) && !flag)
				{
					_dangerousMissiles.RemoveAt(num);
				}
			}
		}
		if (_dangerousMissiles.Count > 0)
		{
			ThreateningMissile = (from m in _dangerousMissiles
				where !m.ConnectionLostForever.Value
				select m into missile
				orderby (base.transform.position - missile.transform.position).sqrMagnitude
				select missile).FirstOrDefault();
		}
		else
		{
			ThreateningMissile = null;
		}
	}

	public void CheckForThreateningTorpedo()
	{
		if (this == null || !(GameTime.missionElapsedTime - _torpedoCheckTime > (double)_torpedoCheckInterval))
		{
			return;
		}
		_torpedoCheckTime = GameTime.missionElapsedTime;
		for (int num = _dangerousTorpedoes.Count - 1; num >= 0; num--)
		{
			if (_dangerousTorpedoes[num].IsDestroyed)
			{
				_dangerousTorpedoes.RemoveAt(num);
			}
			else if (!_threats.Contains(_dangerousTorpedoes[num]))
			{
				_dangerousTorpedoes.RemoveAt(num);
			}
		}
		if (_dangerousTorpedoes.Count > 0)
		{
			ThreatingTorpedo = _dangerousTorpedoes.OrderBy((Torpedo torpedo) => (base.transform.position - torpedo.transform.position).sqrMagnitude).FirstOrDefault();
		}
		else
		{
			ThreatingTorpedo = null;
		}
	}

	public virtual void muteSoundInPauseMode()
	{
	}

	public virtual void enableSoundWhenSwitchingUnit()
	{
	}

	public virtual void disableSoundInPauseModeWhenSwitchingUnit()
	{
	}

	public void DefaultConfirmationMessageAndVO()
	{
		int num = Globals._rnd.Next(0, 3);
		string text = "RogerThat";
		switch (num)
		{
		case 1:
			text = "YesCommander";
			break;
		case 2:
			text = "Confirmed";
			break;
		}
		string alert = "${Name}: " + Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", text);
		Globals._mainGameViewModel.RaiseAlert(this, alert);
		Singleton<VoiceMessage>.Instance.PlayVoiceMessage(text, this);
	}

	public int GetMapContactID()
	{
		return GetMapVehicle(Globals._playerTaskforce)?.Id ?? (-1);
	}

	public string getUIDAndDisplayName()
	{
		if (this != null)
		{
			Vehicle mapVehicle = GetMapVehicle(Globals._playerTaskforce);
			if (mapVehicle != null)
			{
				return $"[{mapVehicle.Id}] {DisplayNameFull}";
			}
			return $"[id={UniqueID}] {DisplayNameFull}";
		}
		return "<null object>";
	}

	public string getUIDAndName()
	{
		if (this != null)
		{
			Vehicle mapVehicle = GetMapVehicle(Globals._playerTaskforce);
			if (mapVehicle != null)
			{
				return $"[{mapVehicle.Id}] {Name.Value}";
			}
			return $"[id={UniqueID}] {Name.Value}";
		}
		return "<null object>";
	}

	public string FullIDData()
	{
		if (this != null)
		{
			string text = $"[id={UniqueID}] {Name.Value}";
			Vehicle mapVehicle = GetMapVehicle(Globals._playerTaskforce);
			if (mapVehicle != null)
			{
				text = $"[{mapVehicle.Id}] {text}";
			}
			if (Formation != null && _obp != null)
			{
				text = text + " (" + _obp._className + ")";
			}
			return base.gameObject.name + " - " + text;
		}
		return "<null object>";
	}

	public void DisableColliders()
	{
		_obp._meshCollidersParent.SetActive(value: false);
		foreach (HitBox hitbox in _obp._hitboxes)
		{
			hitbox._go.SetActive(value: false);
		}
	}

	public void EnableColliders()
	{
		_obp._meshCollidersParent.SetActive(value: true);
		foreach (HitBox hitbox in _obp._hitboxes)
		{
			hitbox._go.SetActive(value: true);
		}
	}

	public ObjectBase getHomeBase()
	{
		return _homeBase;
	}

	public void rememberCameraRotationAndDistToPivot(Quaternion rotation, float distToPivot)
	{
		_lastCameraRotation = rotation;
		_lastCameraDistToPivot = distToPivot;
	}

	public Quaternion getLastCameraRotation()
	{
		return _lastCameraRotation;
	}

	public float getLastCameraDistToPivot()
	{
		return _lastCameraDistToPivot;
	}

	public void setSensorData(SensorData sensorData)
	{
		_sensorData = sensorData;
	}

	public SensorData getSensorData()
	{
		return _sensorData;
	}

	public void resetDeltaToTargetAngles()
	{
		_deltaPitchToTarget = 0f;
		_deltaRollToTarget = 0f;
		_deltaYawToTarget = 0f;
		_deltaHeightToTarget = 0f;
		_currentPitchRate = 0f;
	}

	public GameObject getRootGameObject()
	{
		return _rootObject;
	}

	public void setParent(UnityEngine.Transform parent)
	{
		base.transform.SetParent(parent, worldPositionStays: false);
	}

	public void setTaxiPositionAndRotation(Vector3 pos, float yaw, bool setRotation = true, float bumpAngle = 0f)
	{
		pos.y -= _obp._onGroundPivot.y;
		base.transform.localPosition = pos;
		if (setRotation)
		{
			base.transform.localEulerAngles = new Vector3(_groundedPitchAngle + bumpAngle, yaw, base.transform.localEulerAngles.z);
		}
		setGeoPosition(Utils.worldPositionFromUnityToLongLat(base.transform.position, Globals._currentCenterTile));
	}

	public float getCameraYOffsetForDirectControl()
	{
		return _cameraYOffsetForDirectControl;
	}

	public void setCameraDistancesToTarget(float defaultDistance, float minDistanceForeAft, float minDistanceBroadside, float cameraPivotHeight, float cameraYOffsetForDirectControl)
	{
		_defaultCameraDistance = defaultDistance;
		_minCameraDistanceForeAft = minDistanceForeAft;
		_minCameraDistanceBroadside = minDistanceBroadside;
		_cameraPivotHeight = cameraPivotHeight;
		_cameraYOffsetForDirectControl = cameraYOffsetForDirectControl;
	}

	public float getCameraPivotHeight()
	{
		return _cameraPivotHeight;
	}

	public void adjustCameraPivotHeight(float amount)
	{
		_cameraPivotHeight += amount;
	}

	public float getDefaultCameraDistanceToTarget()
	{
		return _defaultCameraDistance;
	}

	public void getMinCameraDistanceToTarget(out float minDistanceForeAft, out float minDistanceBroadside)
	{
		minDistanceForeAft = _minCameraDistanceForeAft;
		minDistanceBroadside = _minCameraDistanceBroadside;
	}

	public List<GeoPosition> getTrackPoints()
	{
		return _trackPoints;
	}

	public void inheritTaskforce(ObjectBase parent)
	{
		_taskforce = parent._taskforce;
	}

	public void setNation(string nation)
	{
		Nation.Value = nation;
		if (!IsVoiceoverUnit() || string.IsNullOrEmpty(nation))
		{
			_voiceoverProfileSlot = -1;
			_voiceoverProfileNation = null;
		}
		else if (_voiceoverProfileSlot < 0 || !string.Equals(_voiceoverProfileNation, nation, StringComparison.InvariantCultureIgnoreCase))
		{
			_nextVoiceoverProfileSlots.TryGetValue(nation, out var value);
			_voiceoverProfileSlot = value;
			_voiceoverProfileNation = nation;
			_nextVoiceoverProfileSlots[nation] = value + 1;
		}
	}

	public static void ResetVoiceoverProfileAssignments()
	{
		_nextVoiceoverProfileSlots.Clear();
	}

	private bool IsVoiceoverUnit()
	{
		if (!(this is Vessel) && !(this is Submarine) && !(this is Aircraft) && !(this is Helicopter))
		{
			return this is LandUnit;
		}
		return true;
	}

	public string getNation()
	{
		return Nation.Value;
	}

	public string getVoiceoverNation()
	{
		string value = Nation.Value;
		if (_voiceoverProfileSlot < 0 || !Singleton<VoiceMessage>.InstanceExists())
		{
			return value;
		}
		return Singleton<VoiceMessage>.Instance.ResolveVoiceProfile(value, _voiceoverProfileSlot);
	}

	public void setObjectIniName(string iniName)
	{
		_objectIniName = iniName;
	}

	public string getObjectIniName()
	{
		return _objectIniName;
	}

	public void setName(string name)
	{
		Name.Value = name;
	}

	public void SetTypeName(string name)
	{
	}

	public string getName()
	{
		return Name.Value;
	}

	public void setGeoPosition(GeoPosition geoPosition)
	{
		_geoPosition = geoPosition;
		if (_trackPoints == null)
		{
			_trackPoints = new List<GeoPosition>();
		}
		if (_trackPoints.Count == 0)
		{
			_trackPoints.Add(geoPosition);
		}
	}

	public GeoPosition getGeoPosition()
	{
		return _geoPosition;
	}

	public Vector3 getUnityPosition()
	{
		if (this == null)
		{
			return Vector3.zero;
		}
		return base.transform.position;
	}

	public float getHeading()
	{
		if (base.transform != null)
		{
			return base.transform.eulerAngles.y;
		}
		return 0f;
	}

	public UnityEngine.Transform getTransform()
	{
		return base.transform;
	}

	public float getVelocityInKnots()
	{
		return _velocityInKnots;
	}

	public float getVelocityInUnity()
	{
		return _velocityInUnity;
	}

	public void setVelocityVecInUnity(Vector3 vector3)
	{
		_velocityVecInUnity = vector3;
	}

	public void setVelocityInKnots(float velocityInKnots)
	{
		_velocityInKnots = velocityInKnots;
		_velocityInUnity = _velocityInKnots * 0.0076554087f;
		_velocityVecInUnity = _velocityInUnity * base.transform.forward;
	}

	public void setCamLookAroundActive(bool camLookAroundActive)
	{
		_camLookAroundActive = camLookAroundActive;
	}

	public float getHeight()
	{
		if (this == null)
		{
			return 0f;
		}
		float num = 0f;
		if (this is Submarine submarine)
		{
			num = submarine.SP._centerToKeelDistUnity;
		}
		return base.transform.position.y - num;
	}

	public float getHeightInMeters()
	{
		return Utils.unityHeightToMeter(getHeight());
	}

	public float getHeightInFeet()
	{
		return getHeightInMeters() * 3.28084f;
	}

	public void setTargetCourse(float targetCourse)
	{
		Log.Info($"Setting new course for {getUIDAndName()}' to {targetCourse}°", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 7065, "setTargetCourse");
		_targetCourse = targetCourse;
	}

	public void setInterceptPosition(Vector3 interceptPosition)
	{
		_targetInterceptPosition = interceptPosition;
	}

	public void setAmmunitionForEngage(string ammunitionName)
	{
		_selectedAmmunitionForEngage = ammunitionName;
	}

	public int WiresInUse()
	{
		int num = 0;
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			num += weaponSystem.WiresInUse();
		}
		return num;
	}

	public bool HasFreeWire()
	{
		return WiresInUse() < _maxWireCount;
	}

	public WeaponBase OldestWeaponOnWire()
	{
		WeaponBase weaponBase = null;
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			foreach (WeaponBase launchedWeapon in weaponSystem._launchedWeapons)
			{
				if (!(launchedWeapon == null) && !launchedWeapon.IsDestroyed && launchedWeapon._onWire && (weaponBase == null || launchedWeapon._launchTime < weaponBase._launchTime))
				{
					weaponBase = launchedWeapon;
				}
			}
		}
		return weaponBase;
	}

	public WeaponBase OldestBreakableWeaponOnWire()
	{
		WeaponBase weaponBase = null;
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			foreach (WeaponBase launchedWeapon in weaponSystem._launchedWeapons)
			{
				if (!(launchedWeapon == null) && !launchedWeapon.IsDestroyed && launchedWeapon._onWire && !launchedWeapon.WireIsSoleGuidance && (weaponBase == null || launchedWeapon._launchTime < weaponBase._launchTime))
				{
					weaponBase = launchedWeapon;
				}
			}
		}
		return weaponBase;
	}

	public bool UsesDirectFireAgainstAir(string ammunitionName)
	{
		List<WeaponSystem> weaponSystemsForAmmunition = GetWeaponSystemsForAmmunition(ammunitionName, copyList: false);
		if (weaponSystemsForAmmunition == null || weaponSystemsForAmmunition.Count == 0)
		{
			return true;
		}
		foreach (WeaponSystem item in weaponSystemsForAmmunition)
		{
			if (item is WeaponSystemGun)
			{
				return true;
			}
		}
		return false;
	}

	public List<WeaponSystem> GetWeaponSystemsForAmmunition(string ammunitionName, bool copyList = true)
	{
		List<WeaponSystem> value = new List<WeaponSystem>();
		if (_ammunitionToWeaponSystemsDictionary.TryGetValue(ammunitionName, out value) && copyList)
		{
			return Utils.CopyList(value);
		}
		return value;
	}

	public List<SensorSystem> getSensorSystems(Type type)
	{
		List<SensorSystem> list = new List<SensorSystem>();
		if (type == typeof(SensorSystemECM))
		{
			SensorSystemECM sensorSystemECM = _obp._ecmSystems.FirstOrDefault((SensorSystemECM x) => x._ecm._ep._type == ECMParameters.ECMType.Offensive || x._ecm._ep._type == ECMParameters.ECMType.Universal);
			if (sensorSystemECM != null)
			{
				list.Add(sensorSystemECM);
			}
		}
		if (type == typeof(SensorSystemLaserDesignator))
		{
			list.AddRange(_obp._laserSystems);
		}
		return list;
	}

	public Ammunition getAmmunitionByName(string ammunitionName)
	{
		Ammunition value = null;
		if (!string.IsNullOrEmpty(ammunitionName))
		{
			AmmunitionNameToAmmunitionDictionary.TryGetValue(ammunitionName, out value);
		}
		return value;
	}

	public void CalculateAmmunitionHorizons()
	{
		foreach (KeyValuePair<string, List<WeaponSystem>> item in _ammunitionToWeaponSystemsDictionary)
		{
			Ammunition ammunitionByName = getAmmunitionByName(item.Key);
			if (ammunitionByName != null)
			{
				ammunitionByName._ap._horizonRangesInUnity = new Vector2(0f, 0f);
				if (!IsAirUnit && ammunitionByName._ap.IsFcrHorizonLimited)
				{
					ammunitionByName._ap._horizonRangesInUnity = ComputeAmmunitionHorizons(ammunitionByName, item.Value, ammunitionByName._ap._defaultHorizonHeight, useLocalMountHeight: true);
				}
			}
		}
	}

	public Vector2 GetAmmunitionHorizons(string ammoName, float targetHeight)
	{
		Ammunition ammunitionByName = getAmmunitionByName(ammoName);
		if (ammunitionByName == null)
		{
			return new Vector2(-1f, -1f);
		}
		if (!ammunitionByName._ap.IsFcrHorizonLimited)
		{
			return new Vector2(ammunitionByName._ap._launchRangesInUnity.y, ammunitionByName._ap._launchRangesInUnity.y);
		}
		return ComputeAmmunitionHorizons(ammunitionByName, GetWeaponSystemsForAmmunition(ammoName), targetHeight, useLocalMountHeight: false);
	}

	private Vector2 ComputeAmmunitionHorizons(Ammunition ammo, IEnumerable<WeaponSystem> weaponSystems, float targetHeight, bool useLocalMountHeight)
	{
		float num = ammo._ap._launchRangesInUnity.y;
		float num2 = 0f;
		foreach (WeaponSystem weaponSystem in weaponSystems)
		{
			foreach (SensorSystem associatedSensor in weaponSystem._vwp._associatedSensors)
			{
				if (associatedSensor.CanGuideAmmo(ammo._ap))
				{
					bool visual = associatedSensor is SensorSystemVisual;
					float radarHeight = ((!(associatedSensor._mountObject != null)) ? base.transform.position.y : (useLocalMountHeight ? associatedSensor._mountObject.transform.localPosition.y : associatedSensor._mountObject.transform.position.y));
					float radarHorizon = RadarCalculator.getRadarHorizon(radarHeight, targetHeight, visual, Utils.TerrainAltForHorizon(this));
					if (num > radarHorizon)
					{
						num = radarHorizon;
					}
					if (num2 < radarHorizon)
					{
						num2 = radarHorizon;
					}
				}
			}
		}
		return new Vector2(num, num2);
	}

	public float GetMaxRangeForAmmo(Ammunition ammo, ObjectBase targetObject)
	{
		EngagementRange maxEngagementRanges = GetMaxEngagementRanges(targetObject, ammo, requireOpenSensorChannel: false);
		return Mathf.Min(maxEngagementRanges.KinematicRange, maxEngagementRanges.SensorRange, maxEngagementRanges.TerrainRange);
	}

	public FcrRangeData GetMaxRangeForFCR(Ammunition ammo, ObjectBase targetObject, float maxRangeOverride = -1f, SensorRangeOptions options = default(SensorRangeOptions))
	{
		EngagementIssue? issue = null;
		FcrRangeData result;
		if (ammo == null)
		{
			UnityEngine.Debug.LogError(getUIDAndName() + ": ammo is null in GetMaxRangeForFCR()");
			result = default(FcrRangeData);
			result.Range = -1f;
			return result;
		}
		float num = -1f;
		if (ammo._ap.IsTorpedoDeliveryShot(targetObject))
		{
			float num2 = ((maxRangeOverride > 0f) ? maxRangeOverride : ammo._ap._launchRangesInUnity.y);
			if (ammo._ap._seaSkimmingSubAltUnity > 0f)
			{
				Vector2 ammunitionHorizons = GetAmmunitionHorizons(ammo._ap._ammunitionFileName, ammo._ap.FcrTrackedAltitude(targetObject));
				if (ammunitionHorizons.y > 0f && num2 > ammunitionHorizons.y)
				{
					num2 = ammunitionHorizons.y;
				}
			}
			result = default(FcrRangeData);
			result.Range = num2;
			return result;
		}
		if (ammo._ap._midCourseCorrection.AllowsLauncherGuidance && ammo._ap.GuidanceTypeForTarget(targetObject) != 0 && ammo._ap.GuidanceTypeForTarget(targetObject) != AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
		{
			if (maxRangeOverride > 0f)
			{
				result = default(FcrRangeData);
				result.Range = maxRangeOverride;
				return result;
			}
			result = default(FcrRangeData);
			result.Range = ammo._ap._launchRangesInUnity.y;
			return result;
		}
		foreach (WeaponSystem item in GetWeaponSystemsForAmmunition(ammo._ap._ammunitionFileName))
		{
			EngagementIssue? issue2;
			float num3 = item.FCRDetectionRange(targetObject, ammo, out issue2, options);
			if (num < num3)
			{
				issue = issue2;
				num = num3;
			}
		}
		float y = ammo._ap._launchRangesInUnity.y;
		if (!IsAirUnit && targetObject != null && ammo._ap._type == Ammunition.Type.Missile && targetObject is Missile missile && Utils.HorizontalAngleToTarget(missile.transform.forward, missile.transform.position, base.transform.position) < 30f)
		{
			y += missile._velocityInUnity * ammo._ap._timeToMaxRange;
		}
		if (maxRangeOverride > 0f)
		{
			y = maxRangeOverride;
		}
		if (targetObject != null)
		{
			Vector2 ammunitionHorizons2 = GetAmmunitionHorizons(ammo._ap._ammunitionFileName, ammo._ap.FcrTrackedAltitude(targetObject));
			if (num > ammunitionHorizons2.y)
			{
				num = ammunitionHorizons2.y;
			}
		}
		result = default(FcrRangeData);
		result.Range = num;
		result.Issue = issue;
		return result;
	}

	public MissileSimulator.KinematicRangeResult GetKinematicRange(ObjectBase targetObject, Ammunition ammo, Vector3 firePos, bool usePreciseKinematics, float targetVelocity = -1f)
	{
		if (this == null)
		{
			return new MissileSimulator.KinematicRangeResult();
		}
		if (ammo._ap._type == Ammunition.Type.Bomb && IsAirUnit)
		{
			float targetAltitude = Mathf.Max(firePos.y, 0f);
			float num = Utils.BombTimeToDrop(UnityVelocityVector, base.transform.position.y, targetAltitude, ammo._ap._gravity);
			float magnitude = Vector3.ProjectOnPlane(UnityVelocityVector, Vector3.up).magnitude;
			return new MissileSimulator.KinematicRangeResult
			{
				MaxRange = magnitude * num
			};
		}
		if (ammo._ap._type == Ammunition.Type.Torpedo && IsAirUnit)
		{
			return new MissileSimulator.KinematicRangeResult
			{
				MaxRange = 27.559496f
			};
		}
		Vector3 targetVelocity2;
		if (targetObject != null)
		{
			targetVelocity2 = targetObject._velocityVecInUnity;
		}
		else if (targetVelocity > 0f)
		{
			Vector3 vector = firePos - base.transform.position;
			vector.y = 0f;
			vector.Normalize();
			targetVelocity2 = vector * (-1f * targetVelocity);
		}
		else
		{
			targetVelocity2 = Vector3.zero;
		}
		bool targetEvasiveManuver = ammo._ap.AssumeEvasiveTarget(targetObject);
		MissileSimulator.KinematicRangeResult kinematicRangeResult = ((!usePreciseKinematics) ? new MissileSimulator.KinematicRangeResult
		{
			MaxRange = ammo._ap.MaxRangeRough(this, firePos, (targetObject != null) ? targetObject._velocityVecInUnity : Vector3.zero, targetEvasiveManuver)
		} : ammo._ap.MaxRangePrecise(this, firePos, targetVelocity2, 8, targetEvasiveManuver));
		if (targetObject != null && targetObject.isAirTarget() && ammo._ap._type == Ammunition.Type.Projectile && UsesDirectFireAgainstAir(ammo._ap._ammunitionFileName))
		{
			float altitudeDiffUnity = targetObject.transform.position.y - base.transform.position.y;
			float num2 = GunBallistics.DirectFireRangeUnity(ammo._ap, altitudeDiffUnity, Globals._gunAADirectFireMaxElevationDeg);
			if (kinematicRangeResult.MaxRange > num2)
			{
				kinematicRangeResult.MaxRange = num2;
			}
		}
		return kinematicRangeResult;
	}

	public bool HasNoiseJammer(List<Globals.Frequency> frequencies, bool requireRadiating)
	{
		if (_obp?._ecmSystems == null)
		{
			return false;
		}
		foreach (SensorSystemECM ecmSystem in _obp._ecmSystems)
		{
			if (ecmSystem == null || ecmSystem._ecm == null)
			{
				continue;
			}
			if (requireRadiating)
			{
				if (!ecmSystem.CanProduceContacts)
				{
					continue;
				}
			}
			else if (ecmSystem.IsDestroyed || ecmSystem.Inoperable.Value)
			{
				continue;
			}
			ECMParameters ep = ecmSystem._ecm._ep;
			if (!ep.CanNoiseJam && ((ep._type != ECMParameters.ECMType.Offensive && ep._type != ECMParameters.ECMType.Universal) || !ecmSystem.Jams.Value))
			{
				continue;
			}
			if (frequencies == null)
			{
				return true;
			}
			foreach (Globals.Frequency frequency in ep._frequencies)
			{
				if (frequencies.Contains(frequency))
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool IsBeingOffensivelyJammedBy(ObjectBase jammerOwner, List<Globals.Frequency> frequencies)
	{
		if (jammerOwner == null)
		{
			return false;
		}
		foreach (SensorSystemECM item in _ECMSystemsTryingToJamMe)
		{
			if (item == null || item.IsDestroyed || item.Inoperable.Value || !item.IsOn.Value || item._baseObject != jammerOwner)
			{
				continue;
			}
			ECMParameters ep = item._ecm._ep;
			if (ep._type != ECMParameters.ECMType.Offensive && ep._type != ECMParameters.ECMType.Universal)
			{
				continue;
			}
			if (frequencies == null)
			{
				return true;
			}
			foreach (Globals.Frequency frequency in ep._frequencies)
			{
				if (frequencies.Contains(frequency))
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool CantSeeAirTarget(ObjectBase targetObject, SensorRangeOptions options = default(SensorRangeOptions))
	{
		options.FrontTargetAspect = true;
		return GetLocalAirTargetVisibilityRange(targetObject, options) < 20.669622f;
	}

	public float GetLocalAirTargetVisibilityRange(ObjectBase targetObject, SensorRangeOptions options, bool excludeRadars = false)
	{
		float num = 0f;
		if (_obp?._sensorSystems == null)
		{
			return num;
		}
		options.IgnoreCloudCover = true;
		foreach (SensorSystem sensorSystem in _obp._sensorSystems)
		{
			if (sensorSystem == null || sensorSystem.IsDestroyed || sensorSystem.Inoperable.Value)
			{
				continue;
			}
			BaseSystem.SystemType systemType = sensorSystem._systemType;
			if ((uint)(systemType - 2) <= 2u || (systemType == BaseSystem.SystemType.SensorSearchRadar && !excludeRadars && (!(sensorSystem is SensorSystemRadar sensorSystemRadar) || sensorSystemRadar.CanSeeAirTargets || !targetObject.isAirTarget())))
			{
				SensorSystem.SensorDistanceData range = sensorSystem.GetRange(targetObject, options);
				if (range != null && range.Distance > num)
				{
					num = range.Distance;
				}
			}
		}
		return num;
	}

	public EstimatedSensorRangeData GetSensorRange(ObjectBase targetObject, Ammunition ammo, SensorRangeOptions options)
	{
		bool flag = ammo._ap._type == Ammunition.Type.Bomb && ammo._ap._guidanceType != 0 && !ammo._ap._midCourseCorrection.AllowsLauncherGuidance;
		if (targetObject == null || (!DoesEngageRequiresTarget(ammo._ap) && !flag))
		{
			EstimatedSensorRangeData data = UnboundedSensorRange(targetObject, ammo, flag);
			AddCrewVisionIssue(ammo, targetObject, ref data);
			return data;
		}
		WeaponSystem weaponSystem = ammo.getWeaponSystems().FirstOrDefault();
		AmmunitionParameters.GuidanceType guidanceType = ammo._ap.GuidanceTypeForTarget(targetObject);
		if (guidanceType != AmmunitionParameters.GuidanceType.SemiActiveRadarHoming && guidanceType != AmmunitionParameters.GuidanceType.ActiveRadarHoming && !ammo._ap._midCourseCorrection.AllowsLauncherGuidance)
		{
			options.IgnoreSelfScreeningJammers = true;
		}
		options = HomeOnJamOptions(this, targetObject, ammo._ap, options, out var homeOnJamApplies);
		(SensorSystem.SensorDistanceData, SensorSystem, EngagementIssue?) tuple = EvaluateLimitingSensor(targetObject, ammo, weaponSystem, options);
		EstimatedSensorRangeData estimatedSensorRangeData = default(EstimatedSensorRangeData);
		estimatedSensorRangeData.SensorData = tuple.Item1;
		estimatedSensorRangeData.Sensor = tuple.Item2;
		estimatedSensorRangeData.UnscreenedSensorDistance = tuple.Item1.Distance;
		estimatedSensorRangeData.EngagementIssues = new List<EngagementIssue>();
		EstimatedSensorRangeData launchSensorLimit = estimatedSensorRangeData;
		if (tuple.Item3.HasValue)
		{
			launchSensorLimit.EngagementIssues.Add(tuple.Item3.Value);
		}
		SensorRangeOptions? unjammedOptions = GetUnjammedOptions(targetObject, options);
		if (unjammedOptions.HasValue)
		{
			launchSensorLimit.UnscreenedSensorDistance = EvaluateLimitingSensor(targetObject, ammo, weaponSystem, unjammedOptions.Value).Data.Distance;
		}
		float weaponSeekerLimit = (launchSensorLimit.UnscreenedWeaponSensorLimit = (launchSensorLimit.WeaponSensorLimit = GetWeaponSeekerLimit(targetObject, ammo, guidanceType)));
		ApplyLocalAirVisibilityLimit(targetObject, ammo, options, unjammedOptions, ref launchSensorLimit);
		AddEngagementIssues(targetObject, ammo, guidanceType, weaponSeekerLimit, options, unjammedOptions.HasValue, homeOnJamApplies, ref launchSensorLimit);
		AddCrewVisionIssue(ammo, targetObject, ref launchSensorLimit);
		return launchSensorLimit;
	}

	private void AddCrewVisionIssue(Ammunition ammo, ObjectBase targetObject, ref EstimatedSensorRangeData data)
	{
		if (CrewVisionBlocksAmmo(ammo?._ap, targetObject))
		{
			ref List<EngagementIssue> engagementIssues = ref data.EngagementIssues;
			if (engagementIssues == null)
			{
				engagementIssues = new List<EngagementIssue>();
			}
			data.EngagementIssues.Add(EngagementIssue.CantSeeTarget);
		}
	}

	public bool CrewVisionBlocksAmmo(AmmunitionParameters ap, ObjectBase targetObject)
	{
		if (!(this is Aircraft) || ap == null)
		{
			return false;
		}
		if (targetObject != null && targetObject.isAirTarget())
		{
			return false;
		}
		if (ap.HasDayOnlySeeker && SensorUtils.IsBlindingConditions())
		{
			return true;
		}
		if (Singleton<OptionsManager>.Instance.RequireVisionToEngage != 0 && ap.RequiresCrewVision)
		{
			return !SensorUtils.CrewCanSeeTargets(this);
		}
		return false;
	}

	private EstimatedSensorRangeData UnboundedSensorRange(ObjectBase targetObject, Ammunition ammo, bool bombSeekerLimited)
	{
		float num = ((targetObject == null && bombSeekerLimited && ammo._ap._seekerPassiveRange > 0f) ? ammo._ap._seekerPassiveRange : float.MaxValue);
		EstimatedSensorRangeData result = default(EstimatedSensorRangeData);
		result.WeaponSensorLimit = num;
		result.SensorData = new SensorSystem.SensorDistanceData();
		result.UnscreenedWeaponSensorLimit = num;
		result.UnscreenedSensorDistance = float.MaxValue;
		return result;
	}

	private (SensorSystem.SensorDistanceData Data, SensorSystem Sensor, EngagementIssue? Issue) EvaluateLimitingSensor(ObjectBase targetObject, Ammunition ammo, WeaponSystem weaponSystem, SensorRangeOptions options)
	{
		FcrRangeData maxRangeForFCR = GetMaxRangeForFCR(ammo, targetObject, -1f, options);
		if (maxRangeForFCR.Range < 0f)
		{
			maxRangeForFCR.Range = float.MaxValue;
		}
		SensorSystem.SensorDistanceData sensorDistanceData = new SensorSystem.SensorDistanceData
		{
			Distance = maxRangeForFCR.Range
		};
		SensorSystem item = null;
		if (Singleton<OptionsManager>.Instance.RequireVisionToEngage == VisionToEngageMode.AssociatedSensors && targetObject.isAirTarget() && weaponSystem != null && weaponSystem._vwp._associatedSearchSensors.Count > 0)
		{
			SensorSystem.SensorDistanceData sensorDistanceData2 = null;
			SensorSystem sensorSystem = null;
			foreach (SensorSystem associatedSearchSensor in weaponSystem._vwp._associatedSearchSensors)
			{
				if (!associatedSearchSensor.Inoperable.Value)
				{
					SensorSystem.SensorDistanceData range = associatedSearchSensor.GetRange(targetObject, options);
					if (sensorDistanceData2 == null || range.Distance > sensorDistanceData2.Distance)
					{
						sensorDistanceData2 = range;
						sensorSystem = associatedSearchSensor;
					}
				}
			}
			if (sensorDistanceData2 != null && sensorDistanceData2.Distance < sensorDistanceData.Distance)
			{
				item = sensorSystem;
				sensorDistanceData = sensorDistanceData2;
			}
			else if (sensorDistanceData2 == null)
			{
				sensorDistanceData.Distance = 0f;
			}
		}
		return (sensorDistanceData, item, maxRangeForFCR.Issue);
	}

	private SensorRangeOptions? GetUnjammedOptions(ObjectBase targetObject, SensorRangeOptions options)
	{
		if (options.IgnoreSelfScreeningJammers || !targetObject.HasSelfScreeningJammer)
		{
			return null;
		}
		SensorRangeOptions value = options;
		value.IgnoreSelfScreeningJammers = true;
		return value;
	}

	public static SensorRangeOptions HomeOnJamOptions(ObjectBase shooter, ObjectBase targetObject, AmmunitionParameters ap, SensorRangeOptions options = default(SensorRangeOptions))
	{
		bool homeOnJamApplies;
		return HomeOnJamOptions(shooter, targetObject, ap, options, out homeOnJamApplies);
	}

	public static SensorRangeOptions HomeOnJamOptions(ObjectBase shooter, ObjectBase targetObject, AmmunitionParameters ap, SensorRangeOptions options, out bool homeOnJamApplies)
	{
		homeOnJamApplies = false;
		if (shooter == null || targetObject == null || ap == null)
		{
			return options;
		}
		if (ap._secondaryPassiveRadarGuidanceType == AmmunitionParameters.SecondaryPassiveRadarGuidanceType.None)
		{
			return options;
		}
		if (!targetObject.HasNoiseJammer(ap._passiveRadarHomingFrequencies, requireRadiating: false))
		{
			return options;
		}
		homeOnJamApplies = true;
		options.IgnoreSelfScreeningJammers = true;
		options.IgnoreTargetJamming = true;
		if (shooter.IsBeingOffensivelyJammedBy(targetObject, ap._passiveRadarHomingFrequencies))
		{
			options.IgnoreAllOffensiveJamming = true;
		}
		return options;
	}

	private float GetWeaponSeekerLimit(ObjectBase targetObject, Ammunition ammo, AmmunitionParameters.GuidanceType guidanceType)
	{
		float num = float.MaxValue;
		if (ammo._ap._midCourseCorrection.AllowsLauncherGuidance)
		{
			return num;
		}
		if (guidanceType == AmmunitionParameters.GuidanceType.TVHoming)
		{
			Vector3 vector = targetObject.transform.position - base.transform.position;
			float x = ammo._ap._launchRangesInUnity.x;
			Vector3 ownPosition = targetObject.transform.position - vector.normalized * x;
			ownPosition.y = base.transform.position.y;
			num = SensorUtils.CalculateVisualRange(ownPosition, targetObject, ammo._ap._nightVisionLevel) * ammo._ap._zoom * 1.5f;
		}
		else if (guidanceType == AmmunitionParameters.GuidanceType.IRHoming && !ammo._ap._targetRequired && ammo._ap._type != Ammunition.Type.Bomb)
		{
			num = float.MaxValue;
		}
		else if (guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
		{
			num = Mathf.Min(num, ammo._ap._seekerPassiveRange);
		}
		else if (guidanceType == AmmunitionParameters.GuidanceType.AntiRadiationHoming)
		{
			num = (ammo._ap._targetRequired ? ammo._ap._seekerPassiveRange : float.MaxValue);
		}
		else if (guidanceType == AmmunitionParameters.GuidanceType.IRHoming)
		{
			num = MathF.Min(ammo._ap._seekerPassiveRange * SensorUtils.CalculateIRScalingFactor(targetObject, base.transform.position), num);
		}
		else if (guidanceType == AmmunitionParameters.GuidanceType.LaserHoming && ammo._ap._type == Ammunition.Type.Bomb && ammo._ap._seekerPassiveRange > 0f)
		{
			num = ammo._ap._seekerPassiveRange;
			if (_taskforce?._sensorSystemsLink == null || _taskforce._sensorSystemsLink.findLaserDesignatorFromTaskForceLink(targetObject) == null)
			{
				num = Mathf.Min(num, GetOwnLaserDesignatorRange());
			}
		}
		else if (guidanceType == AmmunitionParameters.GuidanceType.None || guidanceType == AmmunitionParameters.GuidanceType.ActiveRadarHoming || guidanceType == AmmunitionParameters.GuidanceType.LaserHoming || num < 1f || ammo._ap._targetMemory)
		{
			num = float.MaxValue;
		}
		return num;
	}

	private float GetOwnLaserDesignatorRange()
	{
		float num = 0f;
		if (_obp?._laserSystems == null)
		{
			return num;
		}
		foreach (SensorSystemLaserDesignator laserSystem in _obp._laserSystems)
		{
			if (laserSystem != null && !laserSystem.IsDestroyed && !laserSystem.Inoperable.Value && laserSystem._maxRange > num)
			{
				num = laserSystem._maxRange;
			}
		}
		return num;
	}

	private void ApplyLocalAirVisibilityLimit(ObjectBase targetObject, Ammunition ammo, SensorRangeOptions options, SensorRangeOptions? unjammedOptions, ref EstimatedSensorRangeData launchSensorLimit)
	{
		if (targetObject.isAirTarget() && (ammo._ap._type == Ammunition.Type.Missile || ammo._ap._type == Ammunition.Type.Projectile) && Singleton<OptionsManager>.Instance.RequireVisionToEngage != 0)
		{
			float localAirTargetVisibilityRange = GetLocalAirTargetVisibilityRange(targetObject, options);
			launchSensorLimit.WeaponSensorLimit = Mathf.Min(launchSensorLimit.WeaponSensorLimit, localAirTargetVisibilityRange);
			if (localAirTargetVisibilityRange < 20.669622f || CantSeeAirTarget(targetObject, options))
			{
				launchSensorLimit.EngagementIssues.Add(EngagementIssue.CantSeeTarget);
			}
			launchSensorLimit.UnscreenedWeaponSensorLimit = Mathf.Min(launchSensorLimit.UnscreenedWeaponSensorLimit, unjammedOptions.HasValue ? GetLocalAirTargetVisibilityRange(targetObject, unjammedOptions.Value) : localAirTargetVisibilityRange);
		}
	}

	private void AddEngagementIssues(ObjectBase targetObject, Ammunition ammo, AmmunitionParameters.GuidanceType guidanceType, float weaponSeekerLimit, SensorRangeOptions options, bool hasUnjammedEvaluation, bool homeOnJamApplies, ref EstimatedSensorRangeData launchSensorLimit)
	{
		float altitudeOverClutter = targetObject.AltitudeOverClutter;
		if (homeOnJamApplies)
		{
			launchSensorLimit.EngagementIssues.Add(EngagementIssue.HomeOnJam);
		}
		if (targetObject.IsAirUnit && (altitudeOverClutter < ammo._ap._attackAltitudesInUnity.x || altitudeOverClutter > ammo._ap._attackAltitudesInUnity.y))
		{
			launchSensorLimit.EngagementIssues.Add(EngagementIssue.OutOfAltitude);
		}
		if (ammo._ap._maxAttackVelocity < float.MaxValue && targetObject._velocityInKnots > ammo._ap._maxAttackVelocity)
		{
			launchSensorLimit.EngagementIssues.Add(EngagementIssue.TargetTooFast);
		}
		if (ammo._ap._guidanceSystemPenalty > 0f)
		{
			launchSensorLimit.EngagementIssues.Add(EngagementIssue.BackupTargeting);
		}
		if (!options.IgnoreLookdownPenalty)
		{
			SensorSystem.SensorDistanceData sensorData = launchSensorLimit.SensorData;
			if (sensorData != null && sensorData.Issue == SensorSystem.SensorIssue.LookDown)
			{
				launchSensorLimit.EngagementIssues.Add(EngagementIssue.InLookdown);
			}
		}
		if (guidanceType == AmmunitionParameters.GuidanceType.TVHoming && weaponSeekerLimit < ammo._ap._launchRangesInUnity.x)
		{
			launchSensorLimit.EngagementIssues.Add(EngagementIssue.ObscuredByAltitude);
		}
		if (hasUnjammedEvaluation && MathF.Min(launchSensorLimit.UnscreenedWeaponSensorLimit, launchSensorLimit.UnscreenedSensorDistance) > MathF.Min(launchSensorLimit.WeaponSensorLimit, launchSensorLimit.SensorData.Distance))
		{
			launchSensorLimit.EngagementIssues.Add(EngagementIssue.DefensiveJamming);
		}
		if (!options.IgnoreAllOffensiveJamming && _ECMSystemsTryingToJamMe.Any((SensorSystemECM ecm) => !ecm.IsDestroyed && !ecm.Inoperable.Value && (!options.IgnoreTargetJamming || !(ecm._baseObject == targetObject)) && (ecm._ecm._ep._type == ECMParameters.ECMType.Offensive || ecm._ecm._ep._type == ECMParameters.ECMType.Universal)))
		{
			launchSensorLimit.EngagementIssues.Add(EngagementIssue.Jammed);
		}
		if (guidanceType == AmmunitionParameters.GuidanceType.AntiRadiationHoming && RadarCalculator.getSignalStrength(targetObject, this, base.transform.position, ammo._ap._gainFactor, ammo._ap._passiveRadarHomingFrequencies, out var _) < Globals.RcsValues._minDetectableReturnPowerESM)
		{
			AddAntiRadiationEmissionIssue(targetObject, ammo, ref launchSensorLimit);
		}
	}

	private void AddAntiRadiationEmissionIssue(ObjectBase targetObject, Ammunition ammo, ref EstimatedSensorRangeData launchSensorLimit)
	{
		List<Globals.Frequency> passiveRadarHomingFrequencies = ammo._ap._passiveRadarHomingFrequencies;
		bool flag = targetObject.HasNoiseJammer(null, requireRadiating: true);
		bool flag2 = targetObject.HasNoiseJammer(passiveRadarHomingFrequencies, requireRadiating: true);
		TrulyObservableCollection<SensorSystem> trulyObservableCollection = targetObject._obp?._sensorSystems;
		if (trulyObservableCollection != null && !flag2)
		{
			foreach (SensorSystem item in trulyObservableCollection)
			{
				if (item is SensorSystemRadar sensorSystemRadar && sensorSystemRadar.CanProduceContacts && (sensorSystemRadar._radar._isActive || sensorSystemRadar._isGuiding))
				{
					flag = true;
					if (passiveRadarHomingFrequencies.Contains(sensorSystemRadar._radar._rp._frequency))
					{
						flag2 = true;
						break;
					}
				}
			}
		}
		if (!flag)
		{
			launchSensorLimit.EngagementIssues.Add(EngagementIssue.NotEmitting);
		}
		else if (!flag2)
		{
			launchSensorLimit.EngagementIssues.Add(EngagementIssue.MissingEmittedBands);
		}
	}

	public float GetTerrainRange(ObjectBase targetObject, Ammunition ammo, SensorSystem targetSensor, float sensorRange, float kinematicRange, bool shortCircuitTerrainCheck)
	{
		if (targetObject == null || !ammo._ap.RequiresDirectLineOfSight)
		{
			return float.MaxValue;
		}
		Vector3 vector = targetSensor?._baseObject.transform.position ?? base.transform.position;
		Ray ray = default(Ray);
		ray.origin = vector;
		ray.direction = targetObject.transform.position - vector;
		Ray ray2 = ray;
		float num = Vector3.Distance(vector, targetObject.transform.position);
		if ((num < sensorRange && num < kinematicRange) || !shortCircuitTerrainCheck)
		{
			Physics.Raycast(ray2, out var hitInfo, num * 0.999f, 1024);
			if (hitInfo.distance > 0.1f)
			{
				return hitInfo.distance;
			}
		}
		return float.MaxValue;
	}

	public EngagementRange GetMaxEngagementRanges(ObjectBase targetObject, Ammunition ammo, bool requireOpenSensorChannel, bool shortCircuitTerrainCheck = true, bool usePreciseKinematics = false, Vector3? bearingFirePos = null)
	{
		EngagementRange result;
		if (ammo?._ap == null || ammo._ap._subType == Ammunition.Type.Fueltank || this == null)
		{
			result = default(EngagementRange);
			result.KinematicRange = float.MaxValue;
			result.SensorRange = float.MaxValue;
			result.TerrainRange = float.MaxValue;
			result.UnscreenedSensorRange = float.MaxValue;
			return result;
		}
		Vector3 zero = Vector3.zero;
		zero = (targetObject ? targetObject.transform.position : ((!bearingFirePos.HasValue) ? new Vector3(base.transform.position.x + ammo._ap._launchRangesInUnity.y, ammo._ap.TypicalTargetAlt, base.transform.position.z) : bearingFirePos.Value));
		MissileSimulator.KinematicRangeResult kinematicRange = GetKinematicRange(targetObject, ammo, zero, usePreciseKinematics, ammo._ap.TypicalTargetVelocity);
		EstimatedSensorRangeData sensorRange = GetSensorRange(targetObject, ammo, new SensorRangeOptions
		{
			RequireOpenSensorChannel = requireOpenSensorChannel
		});
		if (targetObject != null && ammo._ap._type == Ammunition.Type.Torpedo && targetObject.isSubmerged() && ammo._ap._maxDepthUnity > 0f && targetObject.transform.position.y < 0f - ammo._ap._maxDepthUnity)
		{
			ref List<EngagementIssue> engagementIssues = ref sensorRange.EngagementIssues;
			if (engagementIssues == null)
			{
				engagementIssues = new List<EngagementIssue>();
			}
			sensorRange.EngagementIssues.Add(EngagementIssue.TargetTooDeep);
		}
		float sensorRange2 = MathF.Min(sensorRange.WeaponSensorLimit, sensorRange.SensorData.Distance);
		float terrainRange = GetTerrainRange(targetObject, ammo, sensorRange.Sensor, sensorRange2, kinematicRange.MaxRange, shortCircuitTerrainCheck);
		result = default(EngagementRange);
		result.KinematicRange = kinematicRange.MaxRange;
		result.SensorRange = sensorRange2;
		result.TerrainRange = terrainRange;
		result.SensorData = sensorRange;
		result.TimeOfFlight = kinematicRange.InterceptTime;
		result.UnscreenedSensorRange = MathF.Min(sensorRange.UnscreenedWeaponSensorLimit, sensorRange.UnscreenedSensorDistance);
		return result;
	}

	public bool IsWithinFCRAltitudes(Ammunition ammo, ObjectBase targetObject)
	{
		if (ammo == null)
		{
			UnityEngine.Debug.LogError(getUIDAndName() + ": ammo is null in IsWithinFCRAltitudes()");
			return false;
		}
		if (ammo._ap.GuidanceTypeForTarget(targetObject) != AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
		{
			return true;
		}
		if (targetObject.isSubmerged())
		{
			return false;
		}
		foreach (WeaponSystem item in GetWeaponSystemsForAmmunition(ammo._ap._ammunitionFileName))
		{
			if (item.IsWithinFCRAltitudes(targetObject, ammo))
			{
				return true;
			}
		}
		return false;
	}

	public void addAmmunitionToWeaponReference(string ammunitionName, WeaponSystem vws)
	{
		if (!_ammunitionToWeaponSystemsDictionary.ContainsKey(ammunitionName))
		{
			List<WeaponSystem> list = new List<WeaponSystem>();
			list.Add(vws);
			_ammunitionToWeaponSystemsDictionary.Add(ammunitionName, list);
		}
		else
		{
			List<WeaponSystem> value = null;
			_ammunitionToWeaponSystemsDictionary.TryGetValue(ammunitionName, out value);
			if (value != null && !value.Contains(vws))
			{
				value.Add(vws);
			}
		}
		if (!vws._associatedAmmunitionNames.Contains(ammunitionName))
		{
			vws._associatedAmmunitionNames.Add(ammunitionName);
		}
	}

	public void changeAmmunitionAmount(string ammunitionName, int amount)
	{
		if (!AmmunitionAmountDictionary.ContainsKey(ammunitionName))
		{
			AmmunitionAmountDictionary.Add(ammunitionName, amount);
		}
		else
		{
			AmmunitionAmountDictionary[ammunitionName] += amount;
		}
	}

	public void setAmmunitionAmount(string ammunitionName, int amount)
	{
		if (!AmmunitionAmountDictionary.ContainsKey(ammunitionName))
		{
			AmmunitionAmountDictionary.Add(ammunitionName, amount);
		}
		else
		{
			AmmunitionAmountDictionary[ammunitionName] = amount;
		}
	}

	public int getAmmunitionAmountByName(string ammunitionName)
	{
		int value = 0;
		AmmunitionAmountDictionary.TryGetValue(ammunitionName, out value);
		return value;
	}

	public void CaptureInitialAmmunitionLoadout()
	{
		_initialAmmunitionLoadout = new Dictionary<string, int>(AmmunitionAmountDictionary.Count);
		foreach (KeyValuePair<string, int> item in AmmunitionAmountDictionary)
		{
			_initialAmmunitionLoadout[item.Key] = item.Value;
		}
	}

	public int getInitialAmmunitionAmountByName(string ammunitionName)
	{
		if (_initialAmmunitionLoadout == null)
		{
			return getAmmunitionAmountByName(ammunitionName);
		}
		_initialAmmunitionLoadout.TryGetValue(ammunitionName, out var value);
		return value;
	}

	public bool WasInStartingLoadout(string ammunitionName)
	{
		if (string.IsNullOrEmpty(ammunitionName))
		{
			return false;
		}
		bool flag = false;
		if (_obp?._magazineSystems != null)
		{
			foreach (WeaponMagazineSystem magazineSystem in _obp._magazineSystems)
			{
				if (magazineSystem == null || magazineSystem._isCampaignMagazine)
				{
					continue;
				}
				foreach (Ammunition ammunition in magazineSystem._ammunitionList)
				{
					if (!(ammunition._ap._ammunitionFileName != ammunitionName))
					{
						flag = true;
						if (magazineSystem._initialAmmoCount.TryGetValue(ammunition, out var value) && value > 0)
						{
							return true;
						}
					}
				}
			}
		}
		if (flag)
		{
			return false;
		}
		return getInitialAmmunitionAmountByName(ammunitionName) > 0;
	}

	public void addAmmunitionNameToAmmunitionReference(string ammunitionName, Ammunition ammunition)
	{
		if (!AmmunitionNameToAmmunitionDictionary.ContainsKey(ammunitionName))
		{
			AmmunitionNameToAmmunitionDictionary.Add(ammunitionName, ammunition);
		}
	}

	public void RearmAll(bool reloadBroken = false)
	{
		if (!isUnit() || IsDestroyed)
		{
			return;
		}
		List<WeaponMagazineSystem> processedMagazines = new List<WeaponMagazineSystem>();
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			if (!weaponSystem.Inoperable.Value || reloadBroken)
			{
				weaponSystem.RefillAmmo(processedMagazines);
			}
		}
	}

	public bool CanBeRearmed(float maxAmmoPoints, Dictionary<string, int> accountableAmmunition)
	{
		if (_obp == null)
		{
			return false;
		}
		if (IsDestroyed)
		{
			return false;
		}
		foreach (WeaponMagazineSystem magazineSystem in _obp._magazineSystems)
		{
			if (magazineSystem.Inoperable.Value || magazineSystem.IsFull())
			{
				continue;
			}
			if (accountableAmmunition == null)
			{
				if (magazineSystem.CanAddCommonAmmo(maxAmmoPoints))
				{
					return true;
				}
				continue;
			}
			foreach (KeyValuePair<string, int> item in accountableAmmunition)
			{
				if (item.Value >= 1 && magazineSystem.CanAddAmmo(item.Key))
				{
					return true;
				}
			}
		}
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			if (weaponSystem.Inoperable.Value || weaponSystem._isReloading || weaponSystem._executingEngageTask || weaponSystem.hasAMagazine() || !weaponSystem._reloadableWithoutMagazine || !(weaponSystem is WeaponSystemLauncher))
			{
				continue;
			}
			foreach (WeaponContainer container in weaponSystem._containers)
			{
				if (container.IsFull())
				{
					continue;
				}
				if (accountableAmmunition == null)
				{
					if (container.CanAddCommonAmmo())
					{
						return true;
					}
					continue;
				}
				foreach (KeyValuePair<string, int> item2 in accountableAmmunition)
				{
					if (item2.Value >= 1 && container.CanAddAmmo(item2.Key))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	public float ReplenishAccountableAmmo(float ammoReloadPoints, Dictionary<string, int> accountableAmmunition)
	{
		if (_obp == null)
		{
			return ammoReloadPoints;
		}
		if (IsDestroyed)
		{
			return ammoReloadPoints;
		}
		if (ammoReloadPoints > 0f)
		{
			_accountableAmmoReloadPoints += ammoReloadPoints;
			foreach (WeaponMagazineSystem magazineSystem in _obp._magazineSystems)
			{
				if (magazineSystem.Inoperable.Value)
				{
					continue;
				}
				_accountableAmmoReloadPoints = magazineSystem.ReplenishAmmo(_accountableAmmoReloadPoints, float.MaxValue, accountableAmmunition);
				foreach (KeyValuePair<string, int> item in magazineSystem._lastLoadedAmmo)
				{
					if (accountableAmmunition.ContainsKey(item.Key))
					{
						accountableAmmunition[item.Key] -= item.Value;
					}
				}
				magazineSystem._lastLoadedAmmo.Clear();
			}
			foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
			{
				if (weaponSystem.Inoperable.Value || weaponSystem._isReloading || weaponSystem._executingEngageTask || weaponSystem.hasAMagazine() || !weaponSystem._reloadableWithoutMagazine || !(weaponSystem is WeaponSystemLauncher))
				{
					continue;
				}
				foreach (WeaponContainer container in weaponSystem._containers)
				{
					if (!container.IsFull())
					{
						_accountableAmmoReloadPoints = container.ReplenishAmmo(_accountableAmmoReloadPoints, float.MaxValue, accountableAmmunition);
					}
					foreach (KeyValuePair<string, int> item2 in container._lastLoadedAmmo)
					{
						if (accountableAmmunition.ContainsKey(item2.Key))
						{
							accountableAmmunition[item2.Key] -= item2.Value;
						}
					}
					container._lastLoadedAmmo.Clear();
				}
			}
		}
		if (!CanBeRearmed(float.MaxValue, accountableAmmunition))
		{
			_accountableAmmoReloadPoints = 0f;
		}
		return 0f;
	}

	public float ReplenishAmmo(float ammoReloadPoints, float maxAmmoPoints)
	{
		if (_obp == null)
		{
			return ammoReloadPoints;
		}
		if (IsDestroyed)
		{
			return ammoReloadPoints;
		}
		if (ammoReloadPoints > 0f)
		{
			_ammoReloadPoints += ammoReloadPoints;
			foreach (WeaponMagazineSystem magazineSystem in _obp._magazineSystems)
			{
				if (!magazineSystem.Inoperable.Value)
				{
					_ammoReloadPoints = magazineSystem.ReplenishAmmo(_ammoReloadPoints, maxAmmoPoints, null);
				}
			}
			foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
			{
				if (weaponSystem.Inoperable.Value || weaponSystem._isReloading || weaponSystem._executingEngageTask || weaponSystem.hasAMagazine() || !weaponSystem._reloadableWithoutMagazine || !(weaponSystem is WeaponSystemLauncher))
				{
					continue;
				}
				foreach (WeaponContainer container in weaponSystem._containers)
				{
					if (!container.IsFull())
					{
						_ammoReloadPoints = container.ReplenishAmmo(_ammoReloadPoints, maxAmmoPoints, null);
					}
				}
			}
		}
		if (!CanBeRearmed(maxAmmoPoints, null))
		{
			ammoReloadPoints = _ammoReloadPoints;
			_ammoReloadPoints = 0f;
			return ammoReloadPoints;
		}
		return 0f;
	}

	public void addColliderToSystemReference(string colliderName, BaseSystem sys)
	{
		sys._colliderNames.Add(colliderName);
		List<BaseSystem> value = null;
		if (_colliderToSystemDictionary.TryGetValue(colliderName, out value))
		{
			value.Add(sys);
			return;
		}
		value = new List<BaseSystem>();
		value.Add(sys);
		_colliderToSystemDictionary.Add(colliderName, value);
	}

	public string getAmmunitionForEngage()
	{
		return _selectedAmmunitionForEngage;
	}

	public void CalculateToWaypointDelta()
	{
		Vector3 currentWaypointDeviation = getCurrentWaypointDeviation();
		_toTargetDirection.x = currentWaypointDeviation.x;
		_toTargetDirection.y = currentWaypointDeviation.y;
		_toTargetDirection.z = currentWaypointDeviation.z;
		_distToTarget = currentWaypointDeviation.magnitude;
		calculateToTargetDelta();
	}

	public void CalculateToTargetDelta(Vector3 targetPosition)
	{
		Vector3 vector = targetPosition - base.transform.position;
		_toTargetDirection.x = vector.x;
		_toTargetDirection.y = vector.y;
		_toTargetDirection.z = vector.z;
		_distToTarget = vector.magnitude;
		calculateToTargetDelta();
	}

	private void calculateToTargetDelta()
	{
		_toTargetForward.x = _toTargetDirection.x;
		_toTargetForward.y = 0f;
		_toTargetForward.z = _toTargetDirection.z;
		_objectForward.x = base.transform.forward.x;
		_objectForward.y = 0f;
		_objectForward.z = base.transform.forward.z;
		_objectRight.x = base.transform.right.x;
		_objectRight.y = 0f;
		_objectRight.z = base.transform.right.z;
		float num = 57.29578f * Mathf.Atan2(_toTargetForward.z, _toTargetForward.x);
		float num2 = 57.29578f * Mathf.Atan2(base.transform.forward.z, base.transform.forward.x);
		_deltaYawToTarget = Utils.WrapAngle(num2 - num);
		_toTargetForward.x = 0f;
		_toTargetForward.y = _toTargetDirection.y;
		_objectForward.x = 0f;
		_objectForward.y = base.transform.forward.y;
		_objectUp.x = 0f;
		_objectUp.y = base.transform.up.y;
		_objectUp.z = base.transform.up.z;
		float f = Mathf.Clamp(_toTargetForward.y, -0.99f, 0.99f);
		float f2 = Mathf.Clamp(base.transform.forward.y, -0.99f, 0.99f);
		float num3 = -57.29578f * Mathf.Asin(f);
		float num4 = -57.29578f * Mathf.Asin(f2);
		float num5 = Mathf.Abs(_toTargetDirection.y) / 20f;
		if (num5 > 1f)
		{
			num5 = 1f;
		}
		num3 = num5 * 40f * (0f - Mathf.Sign(_toTargetDirection.y));
		_deltaPitchToTarget = num4 - num3;
		_deltaHeightToTarget = _toTargetDirection.y;
		_toTargetForward.x = _toTargetDirection.x;
		_toTargetForward.y = _toTargetDirection.y;
		_toTargetForward.z = 0f;
		_objectUp.x = base.transform.up.x;
		_objectUp.y = base.transform.up.y;
		_objectUp.z = 0f;
		_deltaRollToTarget = Vector3.Angle(_objectUp, _toTargetForward);
	}

	public ObjectBase swapObject(ObjectType newUnitType, string newObjectIniName, bool resetVariant = false, bool resetLoadout = false, bool keepWaypoints = true)
	{
		Log.Debug($"swapObject() objectType={_type}, newUnitType={newUnitType}, newObjectIniName={newObjectIniName}", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 8548, "swapObject");
		if (_obp != null)
		{
			Taskforce taskforce = _taskforce;
			string text = (resetVariant ? "Default" : _obp._objectVariantReference);
			string text2 = (resetVariant ? "Default" : _obp._objectVariantReference);
			string text3 = (resetLoadout ? "Default" : _obp._loadoutVariant);
			int count = Singleton<ObjectsManager>.Instance._listOfAllObjects.Count;
			string objectIniName = _objectIniName;
			GeoPosition geoCenterPosition = Singleton<SceneCreator>.Instance.GeoCenterPosition;
			GeoPosition geoPosition = _geoPosition;
			int currentTelegraph = Singleton<ObjectsManager>.Instance.getCurrentTelegraph();
			Vector3 eulerAngles = base.transform.eulerAngles;
			int dateOverrideYear = _obp._dateOverrideYear;
			ObjectBase homeBase = null;
			GameObject parentForAircraftOrHelo = Singleton<ObjectsManager>.Instance.getParentForAircraftOrHelo(this);
			List<(GeoPosition, WaypointData.WaypointHeightState)> allWaypointsGeoPositions = GetAllWaypointsGeoPositions(getRelative: false);
			destroyObject(keepInFlightEffectAlive: false, score: false);
			ObjectBase objectBase = null;
			Utils.DateBasedAmmoYearOverride = dateOverrideYear;
			try
			{
				switch (newUnitType)
				{
				case ObjectType.Vessel:
					eulerAngles.x = 0f;
					eulerAngles.z = 0f;
					geoPosition._height = 0.0;
					objectBase = Singleton<ObjectsManager>.Instance.createShip(text, text3, newObjectIniName, count, geoCenterPosition, geoPosition, currentTelegraph, eulerAngles, taskforce);
					break;
				case ObjectType.Submarine:
					eulerAngles.x = 0f;
					eulerAngles.z = 0f;
					if (geoPosition._height > -0.14880937337875366)
					{
						geoPosition._height = -0.14880937337875366;
					}
					objectBase = Singleton<ObjectsManager>.Instance.createSubmarine(text, text3, newObjectIniName, count, geoCenterPosition, geoPosition, currentTelegraph, eulerAngles, taskforce);
					break;
				case ObjectType.Aircraft:
				{
					if (geoPosition._height < 13.60713005065918)
					{
						geoPosition._height = 13.60713005065918;
					}
					Aircraft aircraft = Singleton<ObjectsManager>.Instance.createAircraft(text2, text3, homeBase, parentForAircraftOrHelo, newObjectIniName, count, geoCenterPosition, geoPosition, eulerAngles, taskforce, createMinimapSymbol: true, loadWeaponSystems: true);
					if (aircraft == null)
					{
						Log.Error("Could not swap object to aircraft '" + newObjectIniName + "' (squadron='" + text2 + "', loadout='" + text3 + "')", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 8608, "swapObject");
						return null;
					}
					aircraft.DesiredAltitude.Value = aircraft.transform.position.y;
					ObjectCodeAnimation value2 = null;
					string text4 = "Gear_Extend";
					if (!aircraft._obp._codeAnimations.TryGetValue(text4, out value2))
					{
						Log.Error("Could not find animation " + text4 + " for " + aircraft._obp._objectIniName, Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 8618, "swapObject");
					}
					else
					{
						value2.setClosedState();
					}
					aircraft.ImmediateStartEngines();
					if (!keepWaypoints)
					{
						GeoPosition geoPos = aircraft._geoPosition.OffsetByDistanceOnHeadingDoNotUseIt(18.52f, 0f - eulerAngles.y);
						geoPos._height = aircraft._geoPosition._height;
						aircraft.setWaypointTask(geoPos, "Initial_wp");
					}
					aircraft.setTelegraph(3);
					aircraft.SetFlightPhysicsModel(isStartingAirborne: true);
					aircraft._velocityInKnots = aircraft.SpeedCommand.Value.GetDemandedSpeedInKnots(aircraft);
					objectBase = aircraft;
					break;
				}
				case ObjectType.Helicopter:
				{
					if (geoPosition._height < 0.45357099175453186)
					{
						geoPosition._height = 0.45357099175453186;
					}
					objectBase = Singleton<ObjectsManager>.Instance.createHelicopter(text2, text3, homeBase, parentForAircraftOrHelo, newObjectIniName, count, geoCenterPosition, geoPosition, eulerAngles, taskforce);
					ObjectCodeAnimation value = null;
					if (objectBase._obp._codeAnimations.TryGetValue("Gear_Extend", out value))
					{
						value.setClosedState();
					}
					break;
				}
				case ObjectType.LandUnit:
				{
					eulerAngles.x = 0f;
					eulerAngles.z = 0f;
					float num = TerrainUtils.GetHeightAt(geoPosition);
					if (num < 0f)
					{
						num = 0f;
					}
					geoPosition._height = num;
					objectBase = Singleton<ObjectsManager>.Instance.createLandUnit(text, text3, newObjectIniName, count, geoCenterPosition, geoPosition, 0, eulerAngles, taskforce);
					if (objectBase is LandUnit landUnit)
					{
						if (num < 0.01f)
						{
							landUnit._performAutoFlatten = false;
						}
						AutoFlattener.SetObject(objectBase);
					}
					break;
				}
				}
			}
			finally
			{
				Utils.DateBasedAmmoYearOverride = 0;
			}
			if (objectBase == null)
			{
				return null;
			}
			if (objectBase._obp != null && dateOverrideYear > 0)
			{
				objectBase._obp._dateOverrideYear = dateOverrideYear;
			}
			if (objectBase._obp._flightDeck != null)
			{
				objectBase._obp._flightDeck.LoadFlightDeckAirgroupAndAmmo();
			}
			if (keepWaypoints && !(objectBase is LandUnit))
			{
				for (int i = 0; i < allWaypointsGeoPositions.Count; i++)
				{
					GeoPosition item = allWaypointsGeoPositions[i].Item1;
					item._height = objectBase.transform.position.y;
					objectBase.setWaypointTask(item);
				}
			}
			Singleton<ObjectsManager>.Instance.setActiveObject(objectBase);
			Singleton<RenderPosition>.Instance.switchToObject(objectBase, performSmoothMove: false);
			Log.Debug("Object " + objectIniName + " " + text + " has been swapped to " + newObjectIniName, Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 8690, "swapObject");
			return objectBase;
		}
		return null;
	}

	public void SetAircraftOrHelicopterDestroyed()
	{
		setDestroyedFlag();
		HasControl = false;
		_shotDownFireEffectGO = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_obp._shotDownFireEffect, base.transform));
		if (!(_shotDownFireEffectGO == null))
		{
			_shotDownFireEffectGO.transform.localPosition = _obp._shotDownFireEffect._position;
			_shotDownFireEffectGO.transform.localRotation = Quaternion.identity;
			_shotDownFireEffectGO.GetComponent<ParticleSystem>().Play();
		}
	}

	protected void CheckAircraftOrHelicopterWaterHit()
	{
		if (GameTime.IsPaused() || (Globals._ignoreSurfaceOnLanding && !IsDestroyed && ((this is Aircraft aircraft && !aircraft._isInFlight) || (this is Helicopter helicopter && !helicopter._isInFlight))) || !(base.transform.position.y < 0f))
		{
			return;
		}
		if (!IsDestroyed)
		{
			DM.E($"{getUIDAndName()} crashed into water while being alive (TC: {(int)GameTime.TimeCompression} Destrution cancelled). Flight model issue.  Own alt = {base.transform.position.y * 220.47266f:0} ft, Desired alt. = {DesiredAltitude.Value * 220.47266f:0} ft, Aligning = {_alignmentTasks.Count > 0}, Velocity = {_velocityInKnots} kn, Pitch = {base.transform.eulerAngles.x}, State = {StateMachine.CurrentStateName}, Time compression = {GameTime.TimeCompression}");
			base.transform.position = new Vector3(base.transform.position.x, DesiredAltitude.Value, base.transform.position.z);
			base.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, base.transform.eulerAngles.z);
			return;
		}
		Vector3 position = new Vector3(base.transform.position.x, 0f, base.transform.position.z);
		if (_shotDownFireEffectGO != null)
		{
			if (_shotDownFireEffectGO.transform.parent != null && _shotDownFireEffectGO.transform.parent.tag != "PoolManager")
			{
				_shotDownFireEffectGO.transform.SetParent(null);
				_shotDownFireEffectGO.transform.position = position;
			}
			ParticleUtils.SetAutoDestroyAndCustomWorldSpace(_shotDownFireEffectGO);
			_shotDownFireEffectGO.GetComponent<ParticleAutoDestroy>().setTimerAndStop(15f, 15f);
		}
		GameObject gameObject = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_obp._hitWaterEffect, null, position));
		if (gameObject != null)
		{
			gameObject.GetComponent<ParticleSystem>().Play();
			gameObject.GetComponent<ParticleAutoDestroy>().setTimer(25f);
		}
		else
		{
			Log.Error("Failed to spawn HitWaterEffect for " + getObjectIniName(), Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 8760, "CheckAircraftOrHelicopterWaterHit");
		}
		if (_surfaceBurningEffectGO != null && _surfaceBurningEffectGO.transform.parent != null && _surfaceBurningEffectGO.transform.parent.tag != "PoolManager")
		{
			_surfaceBurningEffectGO.transform.SetParent(null);
		}
		GameObject gameObject2 = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_obp._waterDebrisEffect, null, position, Quaternion.identity));
		if (gameObject2 != null)
		{
			gameObject2.GetComponent<ParticleSystem>().Play();
			gameObject2.GetComponent<ParticleAutoDestroy>().setTimer(15f);
		}
		GameObject gameObject3 = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_obp._waterOilSlickEffect, null, position));
		if (gameObject3 != null)
		{
			if (gameObject3.GetComponent<ParticleSystem>() != null)
			{
				gameObject3.GetComponent<ParticleSystem>().Play();
			}
			else
			{
				Log.Error("No ParticleSystem component found in WaterOilSlickEffect", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 8788, "CheckAircraftOrHelicopterWaterHit");
			}
			gameObject3.GetComponent<ParticleAutoDestroy>().setTimer(100f);
		}
		if (!IsDestroyed)
		{
			if (GameTime.TimeCompression > 29f)
			{
				return;
			}
			UnityEngine.Debug.LogWarning($"{getUIDAndName()} crashed into water while being alive. Flight model issue. (TC: {(int)GameTime.TimeCompression}). Own alt = {base.transform.position.y * 220.47266f:0} ft, Desired alt. = {DesiredAltitude.Value * 220.47266f:0} ft, Aligning = {_alignmentTasks.Count > 0}, Velocity = {_velocityInKnots} kn, Pitch = {base.transform.eulerAngles.x}, State = {StateMachine.CurrentStateName}, Time compression = {GameTime.TimeCompression}");
			DM.E($"{getUIDAndName()} crashed into water while being alive. Flight model issue. (TC: {(int)GameTime.TimeCompression}). Own alt = {base.transform.position.y * 220.47266f:0} ft, Desired alt. = {DesiredAltitude.Value * 220.47266f:0} ft, Aligning = {_alignmentTasks.Count > 0}, Velocity = {_velocityInKnots} kn, Pitch = {base.transform.eulerAngles.x}, State = {StateMachine.CurrentStateName}, Time compression = {GameTime.TimeCompression}");
			DM.Log($"{getUIDAndName()} <color=red>crashed into water while being alive. Flight model issue.</color> (TC: {(int)GameTime.TimeCompression}). Own alt = {base.transform.position.y * 220.47266f:0} ft, Desired alt. = {DesiredAltitude.Value * 220.47266f:0} ft, Aligning = {_alignmentTasks.Count > 0}, Velocity = {_velocityInKnots} kn, Pitch = {base.transform.eulerAngles.x}, State = {StateMachine.CurrentStateName}, Time compression = {GameTime.TimeCompression}", DM.EventLogFilters.Destruction);
		}
		destroyObject(keepInFlightEffectAlive: false, !IsDestroyed);
	}

	protected void CheckAircraftOrHelicopterTerrainHit()
	{
		if (_correctAltitudeOnSpawn || _teleportProtection || GameTime.TimeCompression > 80f || (Globals._ignoreSurfaceOnLanding && !IsDestroyed && ((this is Aircraft aircraft && !aircraft._isInFlight) || (this is Helicopter helicopter && !helicopter._isInFlight))))
		{
			return;
		}
		float heightAt = TerrainUtils.GetHeightAt(_geoPosition);
		if (!(base.transform.position.y < heightAt))
		{
			return;
		}
		if (!IsDestroyed)
		{
			DM.E($"{getUIDAndName()} crashed into terrain while being alive (TC: {(int)GameTime.TimeCompression} Destrution cancelled). Flight model issue. Own alt = {base.transform.position.y * 220.47266f:0} ft, Terrain height = {heightAt * 220.47266f:0} ft, Desired alt. = {DesiredAltitude.Value * 220.47266f:0} ft, Aligning = {_alignmentTasks.Count > 0}, Velocity = {_velocityInKnots} kn, Pitch = {base.transform.eulerAngles.x}, State = {StateMachine.CurrentStateName}, Time compression = {GameTime.TimeCompression}");
			base.transform.position = new Vector3(base.transform.position.x, heightAt + 0.2267855f, base.transform.position.z);
			return;
		}
		Vector3 position = base.transform.position;
		position.y = heightAt;
		if (_shotDownFireEffectGO != null)
		{
			if (_shotDownFireEffectGO.transform.parent != null && _shotDownFireEffectGO.transform.parent.tag != "PoolManager")
			{
				_shotDownFireEffectGO.transform.SetParent(null);
				_shotDownFireEffectGO.transform.position = position;
			}
			ParticleUtils.SetAutoDestroyAndCustomWorldSpace(_shotDownFireEffectGO);
			_shotDownFireEffectGO.GetComponent<ParticleAutoDestroy>().setTimerAndStop(15f, 15f);
		}
		GameObject gameObject = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_obp._hitGroundEffect, position, Quaternion.Euler(0f, base.transform.eulerAngles.y, 0f), triggerCameraShake: true));
		if (gameObject != null)
		{
			gameObject.GetComponent<ParticleSystem>().Play();
			gameObject.GetComponent<ParticleAutoDestroy>().setTimer(25f);
		}
		else
		{
			Log.Error("Failed to spawn HitGroundEffect for " + getObjectIniName(), Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 8859, "CheckAircraftOrHelicopterTerrainHit");
		}
		UnityEngine.Debug.Log("ObjectBase.CheckAircraftOrHelicopterTerrainHit called");
		if (!IsDestroyed)
		{
			if (GameTime.TimeCompression > 29f)
			{
				DM.E($"{getUIDAndName()} crashed into terrain while being alive (TC: {(int)GameTime.TimeCompression} Destrution cancelled). Flight model issue. Own alt = {base.transform.position.y * 220.47266f:0} ft, Terrain height = {heightAt * 220.47266f:0} ft, Desired alt. = {DesiredAltitude.Value * 220.47266f:0} ft, Aligning = {_alignmentTasks.Count > 0}, Velocity = {_velocityInKnots} kn, Pitch = {base.transform.eulerAngles.x}, State = {StateMachine.CurrentStateName}, Time compression = {GameTime.TimeCompression}");
				return;
			}
			UnityEngine.Debug.LogWarning($"{getUIDAndName()} crashed into terrain while being alive (TC: {(int)GameTime.TimeCompression}). Flight model issue. Own alt = {base.transform.position.y * 220.47266f:0} ft, Terrain height = {heightAt * 220.47266f:0} ft, Desired alt. = {DesiredAltitude.Value * 220.47266f:0} ft, Aligning = {_alignmentTasks.Count > 0}, Velocity = {_velocityInKnots} kn, Pitch = {base.transform.eulerAngles.x}, State = {StateMachine.CurrentStateName}, Time compression = {GameTime.TimeCompression}");
			DM.E($"{getUIDAndName()} crashed into terrain while being alive (TC: {(int)GameTime.TimeCompression}). Flight model issue. Own alt = {base.transform.position.y * 220.47266f:0} ft, Terrain height = {heightAt * 220.47266f:0} ft, Desired alt. = {DesiredAltitude.Value * 220.47266f:0} ft, Aligning = {_alignmentTasks.Count > 0}, Velocity = {_velocityInKnots} kn, Pitch = {base.transform.eulerAngles.x}, State = {StateMachine.CurrentStateName}, Time compression = {GameTime.TimeCompression}");
			DM.Log($"{getUIDAndName()} <color=red>crashed into terrain while being alive. Flight model issue</color> (TC: {(int)GameTime.TimeCompression}). Own alt = {base.transform.position.y * 220.47266f:0} ft, Terrain height = {heightAt * 220.47266f:0} ft, Desired alt. = {DesiredAltitude.Value * 220.47266f:0} ft, Aligning = {_alignmentTasks.Count > 0}, Velocity = {_velocityInKnots} kn, Pitch = {base.transform.eulerAngles.x}, State = {StateMachine.CurrentStateName}, Time compression = {GameTime.TimeCompression}", DM.EventLogFilters.Destruction);
		}
		destroyObject(keepInFlightEffectAlive: false, !IsDestroyed);
	}

	public virtual void destroyObject(bool keepInFlightEffectAlive = false, bool score = true, TacView.TCEvent eventType = TacView.TCEvent.Destroyed)
	{
		if (!(this is WakeBubble))
		{
			DM.I($"<color=red>{FullIDData()} is destroyed.</color> score = {score}");
		}
		if (Formation != null)
		{
			Formation.DetachUnit(this);
		}
		if (_userRoot != null)
		{
			_userRoot.ClearTasks();
		}
		for (int num = _childAircraft.Count - 1; num >= 0; num--)
		{
			if (!_childAircraft[num].IsControllable || !_childAircraft[num].HasControl)
			{
				_childAircraft[num].destroyObject(keepInFlightEffectAlive: false, score);
			}
		}
		foreach (GameObject item in _debugGameObjectToDestroy)
		{
			if (item != null)
			{
				UnityEngine.Object.Destroy(item);
			}
		}
		_debugGameObjectToDestroy.Clear();
		if (_taskforce != null)
		{
			_taskforce.RemoveTaskForceObject(this);
		}
		ClearEntity();
		Singleton<ObjectsManager>.Instance.removeObject(this);
		setDestroyedFlag(score, eventType);
		if (_obp != null && _obp._towedSystems != null && _obp._towedSystems.Count > 0)
		{
			foreach (TowedSystem towedSystem in _obp._towedSystems)
			{
				towedSystem.destroyTowedSystem();
			}
		}
		CeaseOffensiveJamming();
		stopJammingOfThisObject();
		this.OnDestruction?.Invoke(this, System.EventArgs.Empty);
		if (Singleton<RenderPosition>.Instance.getSelectedObject() == this)
		{
			Singleton<RenderPosition>.Instance.deselectObjectAndDetachCamera();
		}
		if (_rootObject != null)
		{
			if ((_rootObject.transform.parent != null) & (_type != ObjectType.AI_Marker) & (_type != ObjectType.Wakebubble) & (_type != ObjectType.Aircraft) & (_type != ObjectType.Helicopter))
			{
				UnityEngine.Object.Destroy(_rootObject.transform.parent.gameObject);
			}
			UnityEngine.Object.Destroy(_rootObject);
		}
	}

	protected void stopJammingOfThisObject()
	{
		foreach (SensorSystemECM item in _ECMSystemsTryingToJamMe)
		{
			item._associatedTargets.Remove(this);
		}
		_ECMSystemsTryingToJamMe.Clear();
	}

	public Dictionary<string, int> GetAllCampaignAmmo()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (WeaponMagazineSystem magazineSystem in _obp._magazineSystems)
		{
			if (!magazineSystem._isCampaignMagazine)
			{
				continue;
			}
			foreach (KeyValuePair<Ammunition, int> item in magazineSystem.GetMagazineContent())
			{
				if (dictionary.ContainsKey(item.Key._ap._displayedName))
				{
					dictionary[item.Key._ap._displayedName] += item.Value;
				}
				else
				{
					dictionary.Add(item.Key._ap._displayedName, item.Value);
				}
			}
		}
		return dictionary;
	}

	public Dictionary<string, int> GetAllAvailableAmmo()
	{
		_allAvailableAmmo.Clear();
		foreach (WeaponMagazineSystem magazineSystem in _obp._magazineSystems)
		{
			if (magazineSystem._isCampaignMagazine)
			{
				continue;
			}
			foreach (KeyValuePair<Ammunition, int> item in magazineSystem.GetMagazineContent())
			{
				if (_allAvailableAmmo.ContainsKey(item.Key._ap._displayedName))
				{
					_allAvailableAmmo[item.Key._ap._displayedName] += item.Value;
				}
				else
				{
					_allAvailableAmmo.Add(item.Key._ap._displayedName, item.Value);
				}
			}
		}
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			Type type = weaponSystem.GetType();
			int num = 0;
			string text = "";
			if (type == typeof(WeaponSystemLauncher))
			{
				WeaponSystemLauncher weaponSystemLauncher = (WeaponSystemLauncher)weaponSystem;
				if (weaponSystemLauncher._ammunitionOnWeapon != null)
				{
					text = weaponSystemLauncher._ammunitionOnWeapon._ap._displayedName;
					num = weaponSystemLauncher.getNumberOfAmmunitionInAllContainers();
				}
			}
			else if (type == typeof(WeaponSystemChaff))
			{
				WeaponSystemChaff weaponSystemChaff = (WeaponSystemChaff)weaponSystem;
				if (weaponSystemChaff._ammoInUse != null)
				{
					num = weaponSystemChaff.getLoadedAmmoCount(weaponSystemChaff._ammoInUse._ap._ammunitionFileName);
					text = weaponSystemChaff._ammoInUse._ap._displayedName;
				}
			}
			else if (type == typeof(WeaponSystemCIWS))
			{
				WeaponSystemCIWS weaponSystemCIWS = (WeaponSystemCIWS)weaponSystem;
				if (weaponSystemCIWS._ammoForEngage != null)
				{
					text = weaponSystemCIWS._ammoForEngage._ap._displayedName;
					num = weaponSystemCIWS.getLoadedAmmoCount(weaponSystemCIWS._ammoForEngage._ap._ammunitionFileName);
				}
			}
			if (type == typeof(WeaponSystemHardpoint))
			{
				foreach (KeyValuePair<string, int> item2 in ((WeaponSystemHardpoint)weaponSystem).GetLoadedAmmoInfo())
				{
					if (_allAvailableAmmo.ContainsKey(item2.Key))
					{
						_allAvailableAmmo[item2.Key] += item2.Value;
					}
					else if (!string.IsNullOrEmpty(item2.Key))
					{
						_allAvailableAmmo.Add(item2.Key, item2.Value);
					}
				}
			}
			else if (type == typeof(WeaponSystemLauncher) && ((WeaponSystemLauncher)weaponSystem)._vwp._associatedMagazine == null)
			{
				foreach (KeyValuePair<string, int> item3 in ((WeaponSystemLauncher)weaponSystem).GetLoadedAmmoInfo())
				{
					if (_allAvailableAmmo.ContainsKey(item3.Key))
					{
						_allAvailableAmmo[item3.Key] += item3.Value;
					}
					else if (!string.IsNullOrEmpty(item3.Key))
					{
						_allAvailableAmmo.Add(item3.Key, item3.Value);
					}
				}
			}
			else if (_allAvailableAmmo.ContainsKey(text))
			{
				_allAvailableAmmo[text] += num;
			}
			else if (!string.IsNullOrEmpty(text))
			{
				_allAvailableAmmo.Add(text, num);
			}
		}
		return _allAvailableAmmo;
	}

	public Dictionary<string, int> GetAllAvailableAmmoSorted()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		List<WeaponMagazineSystem> list = new List<WeaponMagazineSystem>();
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			Type type = weaponSystem.GetType();
			int num = 0;
			string text = "";
			if (type == typeof(WeaponSystemLauncher))
			{
				WeaponSystemLauncher weaponSystemLauncher = (WeaponSystemLauncher)weaponSystem;
				if (weaponSystemLauncher._ammunitionOnWeapon != null)
				{
					text = weaponSystemLauncher._ammunitionOnWeapon._ap._ammunitionFileName;
					num = weaponSystemLauncher.getNumberOfAmmunitionInAllContainers();
				}
			}
			else if (type == typeof(WeaponSystemChaff))
			{
				WeaponSystemChaff weaponSystemChaff = (WeaponSystemChaff)weaponSystem;
				if (weaponSystemChaff._ammoInUse != null)
				{
					num = weaponSystemChaff.getLoadedAmmoCount(weaponSystemChaff._ammoInUse._ap._ammunitionFileName);
					text = weaponSystemChaff._ammoInUse._ap._ammunitionFileName;
				}
			}
			else if (type == typeof(WeaponSystemCIWS))
			{
				WeaponSystemCIWS weaponSystemCIWS = (WeaponSystemCIWS)weaponSystem;
				if (weaponSystemCIWS._ammoForEngage != null)
				{
					text = weaponSystemCIWS._ammoForEngage._ap._ammunitionFileName;
					num = weaponSystemCIWS.getLoadedAmmoCount(weaponSystemCIWS._ammoForEngage._ap._ammunitionFileName);
				}
			}
			else if (type == typeof(WeaponSystemGun))
			{
				WeaponSystemGun weaponSystemGun = (WeaponSystemGun)weaponSystem;
				if (weaponSystemGun._ammoForEngage != null && weaponSystemGun._vwp._associatedMagazine == null)
				{
					text = weaponSystemGun._ammoForEngage._ap._ammunitionFileName;
					num = weaponSystemGun.getLoadedAmmoCount(weaponSystemGun._ammoForEngage._ap._ammunitionFileName);
				}
			}
			WeaponMagazineSystem associatedMagazine = weaponSystem._vwp._associatedMagazine;
			if (associatedMagazine != null && !list.Contains(associatedMagazine))
			{
				list.Add(associatedMagazine);
				foreach (KeyValuePair<Ammunition, int> item in associatedMagazine.GetMagazineContent())
				{
					if (dictionary.ContainsKey(item.Key._ap._ammunitionFileName))
					{
						dictionary[item.Key._ap._ammunitionFileName] += item.Value;
					}
					else
					{
						dictionary.Add(item.Key._ap._ammunitionFileName, item.Value);
					}
				}
			}
			if (type == typeof(WeaponSystemHardpoint))
			{
				foreach (KeyValuePair<string, int> item2 in ((WeaponSystemHardpoint)weaponSystem).GetLoadedAmmoInfoSorted())
				{
					if (dictionary.ContainsKey(item2.Key))
					{
						dictionary[item2.Key] += item2.Value;
					}
					else if (!string.IsNullOrEmpty(item2.Key))
					{
						dictionary.Add(item2.Key, item2.Value);
					}
				}
			}
			else if (type == typeof(WeaponSystemLauncher) && ((WeaponSystemLauncher)weaponSystem)._vwp._associatedMagazine == null)
			{
				foreach (KeyValuePair<string, int> item3 in ((WeaponSystemLauncher)weaponSystem).GetLoadedAmmoInfoSorted())
				{
					if (dictionary.ContainsKey(item3.Key))
					{
						dictionary[item3.Key] += item3.Value;
					}
					else if (!string.IsNullOrEmpty(item3.Key))
					{
						dictionary.Add(item3.Key, item3.Value);
					}
				}
			}
			else if (dictionary.ContainsKey(text))
			{
				dictionary[text] += num;
			}
			else if (!string.IsNullOrEmpty(text))
			{
				dictionary.Add(text, num);
			}
		}
		return dictionary;
	}

	public bool DoesEngageRequiresTarget()
	{
		return DoesEngageRequiresTarget(_selectedAmmunitionForEngage);
	}

	public bool DoesEngageRequiresTarget(string ammoName)
	{
		return DoesEngageRequiresTarget(getAmmunitionByName(ammoName)._ap);
	}

	public bool DoesEngageRequiresTarget(AmmunitionParameters ap)
	{
		if (ap._ammunitionFileName == "_test_asm")
		{
			return false;
		}
		if (ap._type == Ammunition.Type.Projectile)
		{
			return true;
		}
		if (ap._targetRequired)
		{
			return true;
		}
		if (ap._type == Ammunition.Type.Noisemaker || ap._type == Ammunition.Type.Chaff || ap._type == Ammunition.Type.Fueltank)
		{
			return false;
		}
		if (ap._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
		{
			return true;
		}
		if (ap._guidanceType == AmmunitionParameters.GuidanceType.LaserHoming)
		{
			return true;
		}
		if (ap._guidanceType == AmmunitionParameters.GuidanceType.None && ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand && !ap._hasSubmunition)
		{
			return true;
		}
		if (ap._guidanceType == AmmunitionParameters.GuidanceType.TVHoming && ap._midCourseCorrection != AmmunitionParameters.MidCourseCorrection.RadioCommand)
		{
			return true;
		}
		if (ap._targetType == Ammunition.Target.AAW || ap._secondaryTargetType == Ammunition.Target.AAW)
		{
			return true;
		}
		return false;
	}

	public bool DoesEngageRequiresSensor(AmmunitionParameters ap, ObjectBase targetObject)
	{
		if (ap._type == Ammunition.Type.Projectile)
		{
			return true;
		}
		if (ap.GuidanceTypeForTarget(targetObject) == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
		{
			return true;
		}
		if (ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
		{
			return true;
		}
		return false;
	}

	public string GetDefaultAmmunitionForEngage(ObjectBase targetObject, bool ignoreSituationalRestrictions = false)
	{
		string result = null;
		foreach (KeyValuePair<string, int> item in GetAllAvailableAmmoSorted())
		{
			if (item.Value > 0 && DoesAmmoMatchTarget(item.Key, targetObject, out var isSecondaryMatch, ignoreSituationalRestrictions))
			{
				if (!isSecondaryMatch)
				{
					return item.Key;
				}
				result = item.Key;
			}
		}
		return result;
	}

	public bool DoesAmmoMatchTarget(ObjectBase targetObject, bool ignoreSituationalRestrictions = false)
	{
		if (targetObject == null)
		{
			return false;
		}
		return DoesAmmoMatchTarget(_selectedAmmunitionForEngage, targetObject, ignoreSituationalRestrictions);
	}

	public bool DoesAmmoMatchTarget(string ammo, ObjectBase targetObject, bool ignoreSituationalRestrictions = false)
	{
		bool isSecondaryMatch;
		if (this is WeaponBase weaponBase)
		{
			return DoesAmmoMatchTarget(weaponBase._ap, targetObject, out isSecondaryMatch, ignoreSituationalRestrictions);
		}
		if (string.IsNullOrEmpty(ammo))
		{
			return false;
		}
		Ammunition ammunitionByName = getAmmunitionByName(ammo);
		if (ammunitionByName == null)
		{
			return false;
		}
		return DoesAmmoMatchTarget(ammunitionByName._ap, targetObject, out isSecondaryMatch, ignoreSituationalRestrictions);
	}

	public bool DoesAmmoMatchTarget(string ammo, ObjectBase targetObject, out bool isSecondaryMatch, bool ignoreSituationalRestrictions = false)
	{
		isSecondaryMatch = false;
		if (string.IsNullOrEmpty(ammo))
		{
			return false;
		}
		Ammunition ammunitionByName = getAmmunitionByName(ammo);
		if (ammunitionByName == null)
		{
			return false;
		}
		return DoesAmmoMatchTarget(ammunitionByName._ap, targetObject, out isSecondaryMatch, ignoreSituationalRestrictions);
	}

	public bool DoesAmmoMatchTarget(AmmunitionParameters ap, ObjectBase targetObject, out bool isSecondaryMatch, bool ignoreSituationalRestrictions = false)
	{
		AmmunitionParameters.GuidanceType guidanceType = ap.GuidanceTypeForTarget(targetObject);
		isSecondaryMatch = false;
		if (this == null || targetObject == null)
		{
			return false;
		}
		if (ap._type == Ammunition.Type.Chaff)
		{
			return false;
		}
		if (ap._type == Ammunition.Type.Noisemaker)
		{
			return false;
		}
		if (ap._type == Ammunition.Type.Unknown)
		{
			return false;
		}
		if (ap._subType == Ammunition.Type.Sonobuoy)
		{
			return false;
		}
		if (ap._subType == Ammunition.Type.Fueltank)
		{
			return false;
		}
		if (ap._subType == Ammunition.Type.MOSS)
		{
			return false;
		}
		if (ap._canNotAttackTypes.Contains(targetObject._type))
		{
			return false;
		}
		if (guidanceType == AmmunitionParameters.GuidanceType.AntiRadiationHoming)
		{
			float overHorizonMultiplier = 1f;
			if (ignoreSituationalRestrictions)
			{
				overHorizonMultiplier = 100f;
			}
			if (RadarCalculator.getSignalStrength(targetObject, this, base.transform.position, ap._gainFactor, ap._passiveRadarHomingFrequencies, out var _, overHorizonMultiplier) < Globals.RcsValues._minDetectableReturnPowerESM)
			{
				return false;
			}
		}
		if (!ignoreSituationalRestrictions)
		{
			if (guidanceType == AmmunitionParameters.GuidanceType.TVHoming && ap._midCourseCorrection != AmmunitionParameters.MidCourseCorrection.RadioCommand && SensorUtils.CalculateVisualRange(this, targetObject, ap._nightVisionLevel) * ap._zoom * 1.5f < ap._launchRangesInUnity.x)
			{
				return false;
			}
			if (!(this is WeaponBase) && targetObject.isAirTarget() && (ap._type == Ammunition.Type.Missile || ap._type == Ammunition.Type.Projectile) && Singleton<OptionsManager>.Instance.RequireVisionToEngage != 0 && CantSeeAirTarget(targetObject))
			{
				return false;
			}
			if (CrewVisionBlocksAmmo(ap, targetObject))
			{
				return false;
			}
		}
		if (targetObject is Missile missile && missile._isTargetAirborne)
		{
			return false;
		}
		if (targetObject.IsAirUnit || targetObject is Missile || targetObject is Bomb)
		{
			if (ap._type == Ammunition.Type.Torpedo)
			{
				return false;
			}
			if (ap._type == Ammunition.Type.Projectile && guidanceType == AmmunitionParameters.GuidanceType.LaserHoming)
			{
				return false;
			}
			if (ap._targetType == Ammunition.Target.AAW || ap._secondaryTargetType == Ammunition.Target.AAW)
			{
				if (ap._secondaryTargetType == Ammunition.Target.AAW)
				{
					isSecondaryMatch = true;
				}
				return true;
			}
		}
		else if (targetObject is Vessel vessel)
		{
			if (vessel.isSubmerged())
			{
				if (ap._targetType == Ammunition.Target.ASW || ap._secondaryTargetType == Ammunition.Target.ASW)
				{
					if (ap._secondaryTargetType == Ammunition.Target.ASW)
					{
						isSecondaryMatch = true;
					}
					return true;
				}
			}
			else if (ap._targetType == Ammunition.Target.ASuW || ap._secondaryTargetType == Ammunition.Target.ASuW)
			{
				if (ap._secondaryTargetType == Ammunition.Target.ASuW)
				{
					isSecondaryMatch = true;
				}
				return true;
			}
		}
		else
		{
			if (targetObject is LandUnit landUnit)
			{
				if (landUnit.LandUnitParams._landUnitSubType == LandUnitParameters.LandUnitSubType.OilRig && (ap._targetType == Ammunition.Target.ASuW || ap._secondaryTargetType == Ammunition.Target.ASuW))
				{
					if (ap._secondaryTargetType == Ammunition.Target.ASuW)
					{
						isSecondaryMatch = true;
					}
					return true;
				}
				if (ap._landAttack.Count > 0)
				{
					if (ap._landAttack.Contains(Ammunition.LandAttack.ShoreTargetsOnly) && !landUnit.isNearShore)
					{
						return false;
					}
					if (landUnit._isMobile)
					{
						if (!ap._landAttack.Contains(Ammunition.LandAttack.Mobile))
						{
							return false;
						}
					}
					else if (!ap._landAttack.Contains(Ammunition.LandAttack.Installation))
					{
						return false;
					}
					if (ap._targetType == Ammunition.Target.ASuW || ap._secondaryTargetType == Ammunition.Target.ASuW)
					{
						if (ap._secondaryTargetType == Ammunition.Target.ASuW)
						{
							isSecondaryMatch = true;
						}
						return true;
					}
				}
				_ = ap._type;
				_ = 5;
				return false;
			}
			if (targetObject is Submarine submarine)
			{
				if ((ap._targetType == Ammunition.Target.ASuW || ap._secondaryTargetType == Ammunition.Target.ASuW) && !submarine.isSubmerged())
				{
					if (ap._secondaryTargetType == Ammunition.Target.ASuW)
					{
						isSecondaryMatch = true;
					}
					return true;
				}
				if ((ap._targetType == Ammunition.Target.ASW || ap._secondaryTargetType == Ammunition.Target.ASW) && submarine.isSubmerged())
				{
					if (ap._secondaryTargetType == Ammunition.Target.ASW)
					{
						isSecondaryMatch = true;
					}
					return true;
				}
			}
			else
			{
				if (targetObject.transform.position.y < 0f)
				{
					if (ap._targetType == Ammunition.Target.ASW || ap._secondaryTargetType == Ammunition.Target.ASW)
					{
						if (targetObject._type == ObjectType.Torpedo && (targetObject as Torpedo)._ap._subType != Ammunition.Type.MOSS && !ap._antiTorpedo)
						{
							return false;
						}
						if (ap._secondaryTargetType == Ammunition.Target.ASW)
						{
							isSecondaryMatch = true;
						}
						return true;
					}
				}
				else if (targetObject.transform.position.y < 0.44642812f)
				{
					if (ap._type == Ammunition.Type.Torpedo)
					{
						return false;
					}
					if (ap._targetType == Ammunition.Target.ASuW || ap._secondaryTargetType == Ammunition.Target.ASuW)
					{
						if (ap._secondaryTargetType == Ammunition.Target.ASuW)
						{
							isSecondaryMatch = true;
						}
						return true;
					}
				}
				if (targetObject.transform.position.y > 0.14880937f)
				{
					if (ap._type == Ammunition.Type.Torpedo)
					{
						return false;
					}
					if (ap._targetType == Ammunition.Target.AAW || ap._secondaryTargetType == Ammunition.Target.AAW)
					{
						if (ap._secondaryTargetType == Ammunition.Target.AAW)
						{
							isSecondaryMatch = true;
						}
						return true;
					}
				}
			}
		}
		return false;
	}

	public void EngageWithECM()
	{
		if (AcceptsOrdersFromPlayer && isOffensiveEcmSystemAvailable())
		{
			Singleton<MouseControlState>.Instance.setActionToPerform(MouseControlState.ActionToPerform.EngageWithECM);
		}
	}

	public void DesignateByLaser()
	{
		if (AcceptsOrdersFromPlayer && isLaserDesignatorAvailable())
		{
			Singleton<MouseControlState>.Instance.setActionToPerform(MouseControlState.ActionToPerform.HighlightByLaser);
		}
	}

	public void LaunchChaff(bool message = false)
	{
		bool flag = false;
		foreach (WeaponSystemChaff chaffWeapon in _obp._chaffWeapons)
		{
			if (chaffWeapon.readyToLaunch())
			{
				chaffWeapon.launchChaff();
				flag = true;
			}
		}
		if (flag && message)
		{
			string text = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "ChaffOut");
			Globals._mainGameViewModel.RaiseAlert(this, text);
			Singleton<VoiceMessage>.Instance.PlayVoiceMessage("ChaffOut", this);
		}
	}

	public void LaunchNoisemaker(string ammoForEngageName)
	{
		if (!(this == null))
		{
			AddEngageTask(new EngageTask(ammoForEngageName, base.transform.position, this, 1));
		}
	}

	public void DropFueltanks(bool combatDrop = false)
	{
		if (_fuelTanksDropped || !HasControl || IsLanding)
		{
			return;
		}
		_fuelTanksDropped = true;
		_obp._maxFuel = _obp._defaultMaxFuel;
		if (_obp._currentFuelMass > _obp._maxFuel)
		{
			RangeInKm.Value = _obp._defaultMaxRangeInKm;
		}
		_obp._maxRangeInKm = _obp._defaultMaxRangeInKm;
		bool flag = false;
		foreach (KeyValuePair<string, AmmunitionGroup> fueltankAmmunitionGroup in _obp._fueltankAmmunitionGroups)
		{
			foreach (Ammunition ammunition in fueltankAmmunitionGroup.Value.getAmmunitionList())
			{
				InsertEngageTask(ammunition._ap._ammunitionFileName, null, base.transform.position, 1, -1);
				foreach (WeaponSystem item in GetWeaponSystemsForAmmunition(ammunition._ap._ammunitionFileName))
				{
					if (item._executingEngageTask)
					{
						item.ReturnEngageTask();
					}
				}
				flag = true;
			}
		}
		if (flag && Globals._mainGameViewModel != null && AcceptsOrdersFromPlayer)
		{
			string text = (combatDrop ? "DropFueltanksCombat" : "DropFueltanks");
			string text2 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", text);
			Globals._mainGameViewModel.RaiseAlert(this, text2);
			if (Singleton<RenderPosition>.Instance.getSelectedObject() == this)
			{
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage(text, this);
			}
		}
	}

	public bool isUnit()
	{
		if (this == null)
		{
			return false;
		}
		if (this is Vessel || this is Submarine || this is Aircraft || this is Helicopter || this is LandUnit || this is Biologic)
		{
			return true;
		}
		return false;
	}

	public bool isBiologic()
	{
		if (this == null)
		{
			return false;
		}
		if (this is Biologic)
		{
			return true;
		}
		return false;
	}

	public bool isWeapon()
	{
		if (this == null)
		{
			return false;
		}
		return this is WeaponBase;
	}

	public bool isAirTarget()
	{
		if (this == null)
		{
			return false;
		}
		if (this is Bomb bomb)
		{
			AmmunitionParameters ap = bomb._ap;
			if (ap != null)
			{
				if (ap._type == Ammunition.Type.Sonobuoy || ap._subType == Ammunition.Type.Sonobuoy)
				{
					return false;
				}
				if (ap._type == Ammunition.Type.Fueltank || ap._subType == Ammunition.Type.Fueltank)
				{
					return false;
				}
				if (ap._type == Ammunition.Type.Paratrooper || ap._subType == Ammunition.Type.Paratrooper)
				{
					return false;
				}
			}
			return true;
		}
		if (IsAirUnit || this is Missile || this is ChaffCloud)
		{
			return true;
		}
		return false;
	}

	public bool isSubTarget()
	{
		if (this == null)
		{
			return false;
		}
		if (IsSubUnit || this is Torpedo || this is Noisemaker)
		{
			return true;
		}
		return false;
	}

	public void RepairAll()
	{
		if (!isUnit() || IsDestroyed)
		{
			return;
		}
		if (Compartments != null)
		{
			Compartments.ResetFire();
			Compartments.RepairAllDamage();
			Compartments.ResetFireDecals();
		}
		RepairAllSystems();
		foreach (GameObject damageDecal in _damageDecals)
		{
			if (damageDecal != null)
			{
				UnityEngine.Object.Destroy(damageDecal);
			}
		}
		_damageDecals.Clear();
	}

	public void RepairAllSystems()
	{
		foreach (BaseSystem system in _obp._systems)
		{
			system.DebugRepair();
		}
	}

	public bool isWeaponSystemAvailable(Ammunition ammo)
	{
		bool undesired;
		return isWeaponSystemAvailable(ammo, discardCIWS: false, out undesired);
	}

	public bool isWeaponSystemAvailable(Ammunition ammo, bool discardCIWS, out bool undesired)
	{
		undesired = false;
		List<WeaponSystem> weaponSystemsForAmmunition = GetWeaponSystemsForAmmunition(ammo._ap._ammunitionFileName);
		if (weaponSystemsForAmmunition != null)
		{
			foreach (WeaponSystem item in weaponSystemsForAmmunition)
			{
				if (discardCIWS)
				{
					undesired = false;
					Type type = item.GetType();
					if (type == typeof(WeaponSystemChaff))
					{
						undesired = true;
					}
					else if (type == typeof(WeaponSystemCIWS) && !item._isOffensive)
					{
						undesired = true;
					}
					else if (ammo._ap._type == Ammunition.Type.Noisemaker)
					{
						undesired = true;
					}
				}
				if (undesired || item.Inoperable.Value)
				{
					continue;
				}
				bool flag = true;
				if (ammo._ap._requiresGuidance && item._vwp._associatedSensors != null)
				{
					if (ammo._ap._guidanceType != AmmunitionParameters.GuidanceType.LaserHoming)
					{
						flag = false;
					}
					foreach (SensorSystem associatedSensor in item._vwp._associatedSensors)
					{
						if (associatedSensor.CanGuideAmmo(ammo._ap) && !associatedSensor.Inoperable.Value)
						{
							flag = true;
							break;
						}
					}
				}
				if (flag && item.getLoadedAmmoCount(ammo._ap._ammunitionFileName) + item.getMagazineAmmoCount(ammo._ap._ammunitionFileName) > 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool isOffensiveEcmSystemAvailable()
	{
		bool result = false;
		foreach (SensorSystem sensorSystem in _obp._sensorSystems)
		{
			if (sensorSystem._systemType == BaseSystem.SystemType.SensorECM && sensorSystem.IsOn.Value)
			{
				SensorSystemECM sensorSystemECM = sensorSystem as SensorSystemECM;
				if (sensorSystemECM._ecm._ep._type == ECMParameters.ECMType.Offensive || sensorSystemECM._ecm._ep._type == ECMParameters.ECMType.Universal)
				{
					result = true;
					break;
				}
			}
		}
		return result;
	}

	public bool isLaserDesignatorAvailable()
	{
		bool result = false;
		foreach (SensorSystem sensorSystem in _obp._sensorSystems)
		{
			if (sensorSystem._systemType == BaseSystem.SystemType.SensorLaserDesignator && sensorSystem.IsOn.Value)
			{
				result = true;
				break;
			}
		}
		return result;
	}

	public virtual bool isSubmerged()
	{
		if (this == null || base.transform == null)
		{
			return false;
		}
		return base.transform.position.y < -0.025f;
	}

	public bool isNearSurface()
	{
		float num = 0.44642812f;
		float num2 = 0f;
		if (base.transform.position.y < num + num2)
		{
			return !isSubmerged();
		}
		return false;
	}

	public bool isObjectDetectedByTaskforce(ObjectBase targetObject)
	{
		if (_taskforce == targetObject._taskforce)
		{
			return true;
		}
		return _taskforce?.PlottingTable?.VehicleForObject(targetObject) != null;
	}

	public virtual void setRudderAngle(float angle)
	{
	}

	public virtual void setTelegraph(int telegraph)
	{
	}

	public virtual int getTelegraph()
	{
		return 0;
	}

	public bool CanAcceptAircraft(ObjectBase vehicle, bool message = true, bool checkDistance = true, bool logging = false)
	{
		if (_obp == null)
		{
			return false;
		}
		if (_obp._flightDeck == null)
		{
			return false;
		}
		if (_taskforce != vehicle._taskforce)
		{
			if (message)
			{
				DM.I("<color=red>MESSAGE:</color>Cant set '" + getUIDAndName() + "' as a base for  '" + vehicle.getUIDAndName() + "' - not a friendly");
			}
			return false;
		}
		if (this == vehicle._homeBase)
		{
			return false;
		}
		if (_obp._flightDeck.Inoperable.Value)
		{
			if (message)
			{
				string text = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "CanNotRebase");
				text += Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "RebaseInoperable");
				Globals._mainGameViewModel.RaiseAlert(vehicle, this, text);
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("UnableToComply", this);
			}
			if (logging)
			{
				Log.Error("Cant set '" + getUIDAndName() + "' as a base for '" + vehicle.getUIDAndName() + "' - FD inoperable", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 9845, "CanAcceptAircraft");
			}
			return false;
		}
		if (_obp._flightDeck._vehiclesOnBoard.FindIndex((VehicleTypeOnBoard x) => x._fileName == vehicle._obp._objectIniName) < 0)
		{
			if (message)
			{
				string text2 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "CanNotRebase");
				text2 += Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "RebaseUnsupported");
				Globals._mainGameViewModel.RaiseAlert(vehicle, this, text2);
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("UnableToComply", this);
			}
			if (logging)
			{
				Log.Error("Cant set '" + getUIDAndName() + "' as a base for '" + vehicle.getUIDAndName() + "' - unsupported", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 9859, "CanAcceptAircraft");
			}
			return false;
		}
		if (!_obp._flightDeck.CanRecoverHelicopterInCurrentSeaState(vehicle))
		{
			if (message)
			{
				string text3 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "CanNotRebase");
				text3 += _obp._flightDeck.GetHelicopterSeaStateMessage("OrderLog", "RebaseSeaState", vehicle);
				Globals._mainGameViewModel.RaiseAlert(vehicle, this, text3);
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("UnableToComply", this);
			}
			if (logging)
			{
				Log.Error("Cant set '" + getUIDAndName() + "' as a base for '" + vehicle.getUIDAndName() + "' - sea state too high", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 9872, "CanAcceptAircraft");
			}
			return false;
		}
		if (_obp._flightDeck.TotalVehiclesOnBoardIncludingCooldown() + _obp._flightDeck.TotalVehiclesInAir() + _obp._flightDeck.TotalVehiclesNotInFlight() >= _obp._flightDeck.MaxAircraftOnBoard)
		{
			if (message)
			{
				string text4 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "CanNotRebase");
				text4 += Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "RebaseNoSpace");
				Globals._mainGameViewModel.RaiseAlert(vehicle, this, text4);
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("Negative", this);
			}
			if (logging)
			{
				Log.Error("Cant set '" + getUIDAndName() + "' as a base for '" + vehicle.getUIDAndName() + "' - no free space", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 9889, "CanAcceptAircraft");
			}
			return false;
		}
		if (checkDistance)
		{
			float num = vehicle.RangeInKm.Value * 1000f * 0.0148809375f;
			num *= num;
			if ((base.transform.position - vehicle.transform.position).sqrMagnitude > num)
			{
				if (message)
				{
					string text5 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "CanNotRebase");
					text5 += Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "RebaseTooFar");
					Globals._mainGameViewModel.RaiseAlert(vehicle, this, text5);
					Singleton<VoiceMessage>.Instance.PlayVoiceMessage("UnableToComply", this);
				}
				if (logging)
				{
					Log.Error("Cant set '" + getUIDAndName() + "' as a base for '" + vehicle.getUIDAndName() + "' - too far", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 9907, "CanAcceptAircraft");
				}
				return false;
			}
		}
		return true;
	}

	public void UpdateNumberOfFreeJamChannels()
	{
		int num = 0;
		int num2 = 0;
		foreach (SensorSystem item in _obp?._sensorSystems)
		{
			if (item._systemType != BaseSystem.SystemType.SensorECM || !item.IsOn.Value)
			{
				continue;
			}
			SensorSystemECM sensorSystemECM = item as SensorSystemECM;
			if (sensorSystemECM._ecm._ep._type == ECMParameters.ECMType.Offensive || sensorSystemECM._ecm._ep._type == ECMParameters.ECMType.Universal)
			{
				num = 1;
				num2 = 0;
				if (sensorSystemECM._associatedTarget != null || sensorSystemECM._geoPositionTargeted)
				{
					num2 = 1;
				}
				break;
			}
		}
		NumberOfFreeJamChannels.Value = num - num2;
	}

	public void UpdateNumberOfFreeLaserChannels()
	{
		int num = 0;
		int num2 = 0;
		foreach (SensorSystem item in _obp?._sensorSystems)
		{
			if (item._systemType == BaseSystem.SystemType.SensorLaserDesignator && item.IsOn.Value)
			{
				SensorSystemLaserDesignator sensorSystemLaserDesignator = item as SensorSystemLaserDesignator;
				num += sensorSystemLaserDesignator._targetChannels;
				num2 += sensorSystemLaserDesignator._associatedTargets.Count;
			}
		}
		NumberOfFreeLaserChannels.Value = num - num2;
	}

	public void StoreTelegraphSetting(int telegraph)
	{
		_playerSetTelegraph = telegraph;
	}

	public int LoadTelegraphSetting()
	{
		return _playerSetTelegraph;
	}

	public virtual ISelectableViewModel CreateViewModel()
	{
		if (AcceptsOrdersFromPlayer)
		{
			return new ObjectBaseViewModel(this);
		}
		return new ContactBaseViewModel(this);
	}

	public virtual IDisposable CreateContactViewModel(Vehicle contact, MapUnitViewModel rootVm)
	{
		return null;
	}

	public void SetKinematicColliders(bool kinematicActive)
	{
		if (_obp != null && !(_obp._kinematicCollidersParent == null))
		{
			_obp._meshCollidersParent.SetActive(!kinematicActive);
			_obp._systemCollidersParent.SetActive(!kinematicActive);
			_obp._kinematicCollidersParent.SetActive(kinematicActive);
		}
	}

	public virtual void SaveStateToFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		if (this == null)
		{
			Log.Error($"SaveStateToFile for null object {sectionName} isCampaignStateSave = {isCampaignStateSave}", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 9999, "SaveStateToFile");
			return;
		}
		if (base.transform == null)
		{
			Log.Error("SaveStateToFile: transform is null for object " + FullIDData(), Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Objects\\ObjectBase.cs", 10004, "SaveStateToFile");
			return;
		}
		if (_initialAmmunitionLoadout != null && _initialAmmunitionLoadout.Count > 0)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, int> item in _initialAmmunitionLoadout)
			{
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append('|');
				}
				stringBuilder.Append(item.Key).Append(':').Append(item.Value);
			}
			ini.writeValue(sectionName, "InitialLoadout", stringBuilder.ToString());
		}
		if (!isCampaignStateSave)
		{
			ObjectBase selectedObject = Singleton<RenderPosition>.Instance.getSelectedObject();
			if (selectedObject != null && selectedObject == this)
			{
				ini.writeValue(sectionName, "SetSelected", "True");
			}
			ini.writeValue(sectionName, "UniqueID", UniqueID);
			if (!string.IsNullOrEmpty(_campaignTag))
			{
				ini.writeValue(sectionName, "CampaignTag", _campaignTag);
			}
			ini.writeValue(sectionName, "GeoPosition", $"{_geoPosition._latitude},{_geoPosition._longitude},{_geoPosition._height * 220.47265625}");
			ini.writeValue(sectionName, "VelocityInKnots", _velocityInKnots);
			UnityEngine.Transform parent = base.transform.parent;
			if (parent != null && (parent.GetComponent<Vessel>() != null || parent.GetComponent<LandUnit>() != null))
			{
				ini.writeValue(sectionName, "RelativePosition", base.transform.localPosition);
				ini.writeValue(sectionName, "RelativeOrientation", base.transform.localEulerAngles);
			}
			if (this is Vessel && IsDestroyed)
			{
				ini.writeValue(sectionName, "RelativeOrientation", base.transform.localEulerAngles);
			}
			if (Formation != null)
			{
				if (Formation.LeaderStation?.UnitObject == this && Formation._checkIfFormationAssembled)
				{
					ini.writeValue(sectionName, "CheckIfFormationAssembled", value: true);
				}
				Station stationForUnit = Formation.GetStationForUnit(this);
				if (stationForUnit != null && stationForUnit.StationPosition != Vector3.zero)
				{
					ini.writeValue(sectionName, "StationPosition", stationForUnit.StationPosition);
					if (Formation.RotateStationsAroundLeader)
					{
						ini.writeValue(sectionName, "StationPosition", stationForUnit.StationPosition);
						ini.writeValue(sectionName, "RotatesWithLeader", Formation.RotateStationsAroundLeader);
					}
				}
			}
			if (IsDestroyed || isCampaignStateSave)
			{
				ini.writeValue(sectionName, "IsDestroyed", IsDestroyed);
			}
			if (Compartments != null && Compartments._isSinking)
			{
				ini.writeValue(sectionName, "IsSinking", value: true);
			}
			ini.writeValue(sectionName, "Morale", (int)Morale);
			if (_ai != null && _ai._retreatAfterWeaponsExpended)
			{
				ini.writeValue(sectionName, "RetreatAfterWeaponsExpended", value: true);
			}
			if (Disabled)
			{
				ini.writeValue(sectionName, "Disabled", value: true);
			}
			if (_unlimitedFuel)
			{
				ini.writeValue(sectionName, "UnlimitedFuel", value: true);
			}
			if (SprintAndDriftDoctrine.Value)
			{
				ini.writeValue(sectionName, "SprintAndDriftDoctrine", value: true);
			}
			if (BafflesClearingDoctrine.Value)
			{
				ini.writeValue(sectionName, "BafflesClearingDoctrine", value: true);
			}
			if (FighterDoNotEngageAntiShipMissilesDoctrine.Value)
			{
				ini.writeValue(sectionName, "FighterDoNotEngageAntiShipMissilesDoctrine", value: true);
			}
			if (AircraftBombAttackProfileDoctrine.Value != 0)
			{
				ini.writeValue(sectionName, "AircraftBombAttackProfileDoctrine", AircraftBombAttackProfileDoctrine.Value.ToString());
			}
			string text = "";
			foreach (Taskforce taskForce in Singleton<TaskforceManager>.Instance._taskForces)
			{
				if (taskForce._spottedObjects.Contains(selectedObject))
				{
					text = text + taskForce._nameInMissionFile + ",";
				}
			}
			if (!string.IsNullOrEmpty(text))
			{
				text.Remove(text.Length - 1);
				ini.writeValue(sectionName, "SpottedBy", text);
			}
			if (_spottedObjects.Count > 0)
			{
				string text2 = "";
				foreach (ObjectBase spottedObject in _spottedObjects)
				{
					text2 = text2 + spottedObject.UniqueID + ",";
				}
				text2 = text2.Remove(text2.Length - 1);
				ini.writeValue(sectionName, "SpottedObjects", text2);
			}
			if (_ai != null)
			{
				if (_ai._turnOffTowedSonarsTime > 0.0)
				{
					ini.writeValue(sectionName, "TurnOffTowedSonarsTime", _ai._turnOffTowedSonarsTime - GameTime.missionElapsedTime);
				}
				if (_ai._resumeTelegraphAfterDip != int.MinValue)
				{
					ini.writeValue(sectionName, "ResumeTelegraphAfterDip", _ai._resumeTelegraphAfterDip);
				}
				if (_ai._airstrikeAllowedTime > 0.0)
				{
					ini.writeValue(sectionName, "AirstrikeAllowedTime", _ai._airstrikeAllowedTime - GameTime.missionElapsedTime);
				}
				if (_ai._objectsBeingProcessed.Count > 0)
				{
					string text3 = "";
					foreach (var item2 in _ai._objectsBeingProcessed)
					{
						text3 += $"{item2.obj.UniqueID},{(double)_crew._newContactProcessTime - GameTime.missionElapsedTime + item2.startTime}|";
					}
					text3 = text3.Remove(text3.Length - 1);
					ini.writeValue(sectionName, "ContactsInProcessing", text3);
				}
				if (_ai._processedObjects.Count > 0)
				{
					string text4 = "";
					foreach (ObjectBase processedObject in _ai._processedObjects)
					{
						text4 = text4 + processedObject.UniqueID + ",";
					}
					text4 = text4.Remove(text4.Length - 1);
					ini.writeValue(sectionName, "ProcessedObjects", text4);
				}
			}
			if (_obp != null && _obp._nationOverride)
			{
				ini.writeValue(sectionName, "Nation", _obp._nation);
			}
			IniHandler iniHandler = IniHandler.findByName(getObjectIniName());
			if (iniHandler != null)
			{
				ini.writeValue(sectionName, "Hash", iniHandler.ComputeHash());
			}
		}
		else
		{
			ini.writeValue(sectionName, "SaveDateTime", Singleton<Environment>.Instance.DateTime);
		}
		ini.writeValue(sectionName, "RaftPickUpAllowed", RaftPickUpAllowed.Value);
		if (_obp != null)
		{
			ini.writeValue(sectionName, "CurrentTransportedOnBoard", _obp.CurrentTransportedOnBoard.Value);
		}
		int num = 0;
		if (_obp == null || _obp._systems == null)
		{
			return;
		}
		foreach (BaseSystem system in _obp._systems)
		{
			num++;
			string sectionName2 = sectionName + Utils.GetSectionNameFromObject(system) + num;
			system.SaveStateToFile(ini, sectionName2, isCampaignStateSave);
			if (system is SupplySystem)
			{
				ini.writeValue(sectionName, "SupplySystem1_SystemName", system._systemName);
			}
		}
		if (isCampaignStateSave)
		{
			return;
		}
		int num2 = 1;
		foreach (WeaponSystem weaponSystem in _obp._weaponSystems)
		{
			if (weaponSystem._executingEngageTask && weaponSystem._takenEngageTask != null)
			{
				string sectionName3 = sectionName + "EngageTask" + num2;
				weaponSystem._takenEngageTask.SaveStateToFile(ini, sectionName3);
				num2++;
			}
		}
		foreach (EngageTask currentEngageTask in _currentEngageTasks)
		{
			string sectionName3 = sectionName + "EngageTask" + num2;
			currentEngageTask.SaveStateToFile(ini, sectionName3);
			num2++;
		}
		ini.writeValue(sectionName, "AutomateRoute", AutomateRoute);
	}

	public virtual void LoadStateFromFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		string text = ini.readValue(sectionName, "InitialLoadout", "");
		if (!string.IsNullOrEmpty(text))
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			string[] array = text.Split('|');
			foreach (string text2 in array)
			{
				int num = text2.LastIndexOf(':');
				if (num > 0 && int.TryParse(text2.Substring(num + 1), out var result))
				{
					dictionary[text2.Substring(0, num)] = result;
				}
			}
			_initialAmmunitionLoadout = dictionary;
		}
		if (!isCampaignStateSave)
		{
			_sectionName = sectionName;
			int num2 = ini.readValue(sectionName, "UniqueID", 0);
			if (num2 > 0)
			{
				SetUniqueId(num2);
			}
			float num3 = ini.readValue(sectionName, "VelocityInKnots", -1f);
			if (num3 >= 0f)
			{
				_velocityInKnots = num3;
			}
			SprintAndDriftDoctrine.Value = ini.readValue(sectionName, "SprintAndDriftDoctrine", defaultValue: false);
			BafflesClearingDoctrine.Value = ini.readValue(sectionName, "BafflesClearingDoctrine", defaultValue: false);
			FighterDoNotEngageAntiShipMissilesDoctrine.Value = ini.readValue(sectionName, "FighterDoNotEngageAntiShipMissilesDoctrine", defaultValue: false);
			AircraftBombAttackProfileDoctrine.Value = ini.readValue(sectionName, "AircraftBombAttackProfileDoctrine", TacticalDoctrine.AircraftBombAttackProfile.Automatic);
			if (ini.readValue(sectionName, "DisablePopUpAttackDoctrine", defaultValue: false))
			{
				AircraftBombAttackProfileDoctrine.Value = TacticalDoctrine.AircraftBombAttackProfile.LevelAttack;
			}
			IsDestroyed = ini.readValue(sectionName, "IsDestroyed", IsDestroyed);
			if (IsDestroyed)
			{
				if (isUnit())
				{
					setDamageModelActive(active: true);
				}
				else
				{
					_ = this is WeaponBase;
				}
				setDestroyedFlag(score: false);
				_externalDestructionNotified = true;
				_destroyedInSave = true;
				if (ini.doesKeyExist(sectionName, "RelativeOrientation"))
				{
					Vector3 localEulerAngles = ini.readValue(sectionName, "RelativeOrientation", Vector3.zero);
					base.transform.localEulerAngles = localEulerAngles;
				}
			}
			if (ini.doesKeyExist(sectionName, "SpottedObjects"))
			{
				string[] array = ini.readValue(sectionName, "SpottedObjects", "").Split(",");
				for (int i = 0; i < array.Length; i++)
				{
					if (int.TryParse(array[i], out var result2) && result2 > 0)
					{
						ObjectBase objectBase = Singleton<SceneCreator>.Instance.FindObjectById(result2);
						if (objectBase != null && !_spottedObjects.Contains(objectBase))
						{
							_spottedObjects.Add(objectBase);
						}
					}
				}
			}
			_subState = ini.readValue(sectionName, "SubState", "");
			if (_ai != null)
			{
				float num4 = ini.readValue(sectionName, "TurnOffTowedSonarsTime", -1f);
				if (num4 > 0f)
				{
					_ai._turnOffTowedSonarsTime = GameTime.missionElapsedTime + (double)num4;
				}
				_ai._resumeTelegraphAfterDip = ini.readValue(sectionName, "ResumeTelegraphAfterDip", int.MinValue);
				float num5 = ini.readValue(sectionName, "AirstrikeAllowedTime", -1f);
				if (num5 > 0f)
				{
					_ai._airstrikeAllowedTime = GameTime.missionElapsedTime + (double)num5;
				}
				if (_ai._objectsBeingProcessed.Count > 0)
				{
					string text3 = "";
					foreach (var item in _ai._objectsBeingProcessed)
					{
						text3 += $"{item.obj.UniqueID},{GameTime.missionElapsedTime - item.startTime}|";
					}
					text3 = text3.Remove(text3.Length - 1);
					ini.writeValue(sectionName, "ContactsInProcessing", text3);
				}
				if (ini.doesKeyExist(sectionName, "ContactsInProcessing"))
				{
					string[] array = ini.readValue(sectionName, "ContactsInProcessing", "").Split("|");
					for (int i = 0; i < array.Length; i++)
					{
						string[] array2 = array[i].Split(",");
						if (int.TryParse(array2[0], out var result3) && result3 > 0)
						{
							ObjectBase obj = Singleton<SceneCreator>.Instance.FindObjectById(result3);
							if (obj != null && !_ai._objectsBeingProcessed.Any(((ObjectBase obj, double startTime) x) => x.obj == obj))
							{
								double result4 = 0.0;
								double.TryParse(array2[1], out result4);
								result4 = GameTime.missionElapsedTime - result4;
								_ai._objectsBeingProcessed.Add((obj, result4));
							}
						}
					}
				}
				if (ini.doesKeyExist(sectionName, "ProcessedObjects"))
				{
					string[] array = ini.readValue(sectionName, "ProcessedObjects", "").Split(",");
					for (int i = 0; i < array.Length; i++)
					{
						if (int.TryParse(array[i], out var result5) && result5 > 0)
						{
							ObjectBase objectBase2 = Singleton<SceneCreator>.Instance.FindObjectById(result5);
							if (objectBase2 != null && !_ai._processedObjects.Contains(objectBase2))
							{
								_ai._processedObjects.Add(objectBase2);
							}
						}
					}
				}
			}
			IniHandler iniHandler = IniHandler.findByName(getObjectIniName());
			if (iniHandler != null)
			{
				string text4 = iniHandler.ComputeHash();
				string text5 = ini.readValue(sectionName, "Hash", "");
				if (text5 != text4)
				{
					if (text5 == "")
					{
						if (Globals._devMode)
						{
							UnityEngine.Debug.LogWarning("Old save version without hash");
						}
					}
					else
					{
						UnityEngine.Debug.LogWarning("Unit config mismatch detected");
					}
				}
			}
		}
		if (_obp != null)
		{
			_obp.CurrentTransportedOnBoard.Value = Mathf.Max(0, ini.readValue(sectionName, "CurrentTransportedOnBoard", _obp.CurrentTransportedOnBoard.Value));
			foreach (BaseSystem system in _obp._systems)
			{
				int systemIndex = _obp.getSystemIndex(system);
				string sectionName2 = sectionName + Utils.GetSectionNameFromObject(system) + systemIndex;
				system.LoadStateFromFile(ini, sectionName2, isCampaignStateSave);
			}
			UpdateAmmoCount();
			RaftPickUpAllowed.Value = ini.readValue(sectionName, "RaftPickUpAllowed", RaftPickUpAllowed.Value);
			if (isCampaignStateSave && !_campaignRearm)
			{
				bool flag = false;
				foreach (WeaponMagazineSystem magazineSystem in _obp._magazineSystems)
				{
					if (magazineSystem._isCampaignMagazine && !magazineSystem.Inoperable.Value && !magazineSystem._associatedWeaponSystem.Inoperable.Value && !magazineSystem.IsEmpty())
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					if (DateTime.TryParse(ini.readValue(sectionName, "SaveDateTime", ""), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AssumeLocal, out var result6))
					{
						ReloadfromCampaignMagazines((float)(Singleton<Environment>.Instance.DateTime - result6).TotalSeconds);
					}
					else
					{
						DM.E("Can not read DateTime from '" + ini.readValue(sectionName, "SaveDateTime", "") + "'");
					}
				}
			}
		}
		if (isCampaignStateSave)
		{
			return;
		}
		if (Formation == null && ini.doesKeyExist(sectionName, "RelativeWaypoint"))
		{
			Vector3 position = ini.readValue(sectionName, "RelativeWaypoint", Vector3.zero);
			int id = ini.readValue(sectionName, "WaypointParent", -1);
			ObjectBase objectBase3 = Singleton<SceneCreator>.Instance.FindObjectById(id);
			if (objectBase3 != null)
			{
				RemoveWaypoints();
				SetRelativeWaypointTask(position, objectBase3, "Station", reachable: false, setHeight: true);
			}
		}
		int num6 = 1;
		string sectionName3 = sectionName + "EngageTask" + num6;
		while (ini.doesSectionExist(sectionName3))
		{
			EngageTask engageTask = EngageTask.CreateFromFile(ini, sectionName3, this);
			if (engageTask != null)
			{
				_currentEngageTasks.Add(engageTask);
			}
			num6++;
			sectionName3 = sectionName + "EngageTask" + num6;
		}
		AutomateRoute = ini.readValue(sectionName, "AutomateRoute", defaultValue: false);
	}

	public void UpdateAmmoCount()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (BaseSystem system in _obp._systems)
		{
			if (system is WeaponSystem weaponSystem)
			{
				int num = 0;
				Ammunition onWeaponAmmunition = weaponSystem.getOnWeaponAmmunition();
				if (onWeaponAmmunition != null && !(weaponSystem is WeaponSystemChaff))
				{
					string ammunitionFileName = onWeaponAmmunition._ap._ammunitionFileName;
					num = weaponSystem._vwp._internalAmmoCount;
					if (dictionary.ContainsKey(ammunitionFileName))
					{
						dictionary[ammunitionFileName] += num;
					}
					else
					{
						dictionary[ammunitionFileName] = num;
					}
				}
				foreach (KeyValuePair<string, int> item in weaponSystem._loadedAmmunitionCount)
				{
					string ammunitionFileName = item.Key;
					num = item.Value;
					if (dictionary.ContainsKey(ammunitionFileName))
					{
						dictionary[ammunitionFileName] += num;
					}
					else
					{
						dictionary[ammunitionFileName] = num;
					}
				}
			}
			else
			{
				if (!(system is WeaponMagazineSystem weaponMagazineSystem) || weaponMagazineSystem._isCampaignMagazine)
				{
					continue;
				}
				for (int i = 0; i < weaponMagazineSystem._ammunitionList.Count; i++)
				{
					Ammunition ammunition = weaponMagazineSystem._ammunitionList[i];
					string ammunitionFileName2 = ammunition._ap._ammunitionFileName;
					int magazineAmmoCount = ammunition._ap._magazineAmmoCount;
					if (dictionary.ContainsKey(ammunitionFileName2))
					{
						dictionary[ammunitionFileName2] += magazineAmmoCount;
					}
					else
					{
						dictionary[ammunitionFileName2] = magazineAmmoCount;
					}
				}
			}
		}
		foreach (string item2 in AmmunitionAmountDictionary.Keys.ToList())
		{
			if (!dictionary.ContainsKey(item2))
			{
				setAmmunitionAmount(item2, 0);
			}
		}
		foreach (KeyValuePair<string, int> item3 in dictionary)
		{
			setAmmunitionAmount(item3.Key, item3.Value);
		}
	}

	private void ReloadfromCampaignMagazines(float secondsElapsed)
	{
		if (secondsElapsed < 0f)
		{
			return;
		}
		foreach (WeaponMagazineSystem magazineSystem in _obp._magazineSystems)
		{
			if (!magazineSystem._isCampaignMagazine || magazineSystem.Inoperable.Value || magazineSystem.IsEmpty())
			{
				continue;
			}
			WeaponSystem associatedWeaponSystem = magazineSystem._associatedWeaponSystem;
			if (associatedWeaponSystem.Inoperable.Value)
			{
				continue;
			}
			int num = (int)(secondsElapsed / magazineSystem._campaignTimeToLoadOneWeapon);
			if (associatedWeaponSystem.hasAMagazine())
			{
				foreach (KeyValuePair<string, Ammunition> item in associatedWeaponSystem._vwp._associatedMagazine._ammunitionDictionary)
				{
					if (num < 1)
					{
						break;
					}
					string key = item.Key;
					if (magazineSystem._ammunitionDictionary.ContainsKey(key))
					{
						Ammunition value = item.Value;
						int num2 = magazineSystem._ammunitionDictionary[key]._ap._magazineAmmoCount;
						if (num2 > num)
						{
							num2 = num;
						}
						if (value._ap._magazineAmmoCount + num2 > associatedWeaponSystem._vwp._associatedMagazine._initialAmmoCount[value])
						{
							num2 = associatedWeaponSystem._vwp._associatedMagazine._initialAmmoCount[value] - value._ap._magazineAmmoCount;
						}
						associatedWeaponSystem._vwp._associatedMagazine.increaseAmmunitionCount(value, num2);
						num -= num2;
						magazineSystem.decreaseAmmunitionCount(key, num2);
					}
				}
			}
			else
			{
				if (num < 1 || !(associatedWeaponSystem is WeaponSystemLauncher))
				{
					continue;
				}
				foreach (WeaponContainer container in associatedWeaponSystem._containers)
				{
					if (num < 1)
					{
						break;
					}
					if (container.IsFull())
					{
						continue;
					}
					if (!magazineSystem._ammunitionDictionary.ContainsKey(container._initialAmmunition._ap._ammunitionFileName))
					{
						DM.E("No '" + container._initialAmmunition._ap._ammunitionFileName + "' key in " + magazineSystem._sectionName + " _ammunitionDictionary");
						continue;
					}
					int num2 = magazineSystem._ammunitionDictionary[container._initialAmmunition._ap._ammunitionFileName]._ap._magazineAmmoCount;
					if (num2 > num)
					{
						num2 = num;
					}
					num2 = container.LoadAmmo(num2);
					num -= num2;
					magazineSystem.decreaseAmmunitionCount(container._initialAmmunition._ap._ammunitionFileName, num2);
				}
			}
		}
	}

	public void EnableAllRadars(bool setTimer = false)
	{
		EnableAirSearchRadars(setTimer);
		EnableSurfaceSearchRadars(setTimer);
	}

	public void EnableAllActiveSensors(bool setTimer = false)
	{
		EnableAirSearchRadars(setTimer);
		EnableSurfaceSearchRadars(setTimer);
		EnableActiveSonars(setTimer);
	}

	public void DisableAllActiveSensors()
	{
		DisableAirSearchRadars();
		DisableSurfaceSearchRadars();
		DisableActiveSonars();
	}

	public void DisableAllRadars()
	{
		DisableAirSearchRadars();
		DisableSurfaceSearchRadars();
	}

	public bool HasRadar(RadarParameters.Role radarRole)
	{
		if (_obp == null)
		{
			return false;
		}
		switch (radarRole)
		{
		case RadarParameters.Role.Air:
			return HasAirSearchRadar();
		case RadarParameters.Role.SurfaceAndAir:
			if (!HasAirSearchRadar())
			{
				return HasSurfaceSearchRadar();
			}
			return true;
		case RadarParameters.Role.Surface:
			return HasSurfaceSearchRadar();
		default:
			return false;
		}
	}

	public void EnableRadars(RadarParameters.Role radarRole, bool setTimer = false)
	{
		if (_obp != null)
		{
			switch (radarRole)
			{
			case RadarParameters.Role.Air:
				EnableAirSearchRadars(setTimer);
				break;
			case RadarParameters.Role.SurfaceAndAir:
				EnableAllRadars(setTimer);
				break;
			case RadarParameters.Role.Surface:
				EnableSurfaceSearchRadars(setTimer);
				break;
			}
		}
	}

	public void DisableRadars(RadarParameters.Role radarRole)
	{
		if (_obp != null)
		{
			switch (radarRole)
			{
			case RadarParameters.Role.Air:
				DisableAirSearchRadars();
				break;
			case RadarParameters.Role.SurfaceAndAir:
				DisableAllRadars();
				break;
			case RadarParameters.Role.Surface:
				DisableSurfaceSearchRadars();
				break;
			}
		}
	}

	public bool HasAirSearchRadar()
	{
		if (_obp == null)
		{
			return false;
		}
		foreach (SensorSystemRadar airSearchRadar in _obp._airSearchRadars)
		{
			if (!airSearchRadar.Inoperable.Value)
			{
				return true;
			}
		}
		return false;
	}

	public void EnableAirSearchRadars(bool setTimer = false, bool formationCheck = false)
	{
		if (_obp == null)
		{
			return;
		}
		if (_obp._airSearchRadars.Any((SensorSystemRadar searchRadar) => !searchRadar.Inoperable.Value && !searchRadar.IsOn.Value))
		{
			LogForcedEmconRadarActivationAttempt("air-search radar");
		}
		foreach (SensorSystemRadar airSearchRadar in _obp._airSearchRadars)
		{
			if (!airSearchRadar.Inoperable.Value && !airSearchRadar.IsOn.Value)
			{
				airSearchRadar.Enable();
			}
		}
		if (!formationCheck || !FollowingFormation)
		{
			return;
		}
		foreach (Station station in Formation.Stations)
		{
			if (!(station.UnitObject == this) && station.UnitObject.FollowingFormation)
			{
				station.UnitObject.EnableAirSearchRadars();
			}
		}
	}

	public void DisableAirSearchRadars(bool formationCheck = false)
	{
		if (_obp == null)
		{
			return;
		}
		foreach (SensorSystemRadar airSearchRadar in _obp._airSearchRadars)
		{
			if (!airSearchRadar.Inoperable.Value && airSearchRadar.IsOn.Value)
			{
				airSearchRadar.Disable();
			}
		}
		if (!formationCheck || !FollowingFormation)
		{
			return;
		}
		foreach (Station station in Formation.Stations)
		{
			if (!(station.UnitObject == this) && station.UnitObject.FollowingFormation)
			{
				station.UnitObject.DisableAirSearchRadars();
			}
		}
	}

	public bool HasSurfaceOrAirSurfaceSearchRadar(bool includeDamaged = false)
	{
		if (_obp == null)
		{
			return false;
		}
		foreach (SensorSystemRadar searchRadarSystem in _obp._searchRadarSystems)
		{
			if ((!searchRadarSystem.Inoperable.Value || includeDamaged) && (searchRadarSystem._radar._rp._role == RadarParameters.Role.Surface || searchRadarSystem._radar._rp._role == RadarParameters.Role.SurfaceAndAir))
			{
				return true;
			}
		}
		return false;
	}

	public bool HasAirAndSurfaceSurfaceSearchRadar(bool includeDamaged = false)
	{
		if (_obp == null)
		{
			return false;
		}
		foreach (SensorSystemRadar searchRadarSystem in _obp._searchRadarSystems)
		{
			if ((!searchRadarSystem.Inoperable.Value || includeDamaged) && searchRadarSystem._radar._rp._role == RadarParameters.Role.SurfaceAndAir)
			{
				return true;
			}
		}
		return false;
	}

	public void EnableRadarsForSurfaceSearch(bool setTimer = false, bool formationCheck = false)
	{
		if (_obp == null)
		{
			return;
		}
		if (formationCheck && FollowingFormation)
		{
			foreach (Station station in Formation.Stations)
			{
				if (!(station.UnitObject == this) && station.UnitObject.FollowingFormation)
				{
					station.UnitObject.EnableRadarsForSurfaceSearch();
				}
			}
		}
		bool flag = false;
		foreach (SensorSystemRadar surfaceSearchRadar in _obp._surfaceSearchRadars)
		{
			if (!surfaceSearchRadar.Inoperable.Value && !surfaceSearchRadar.IsOn.Value)
			{
				surfaceSearchRadar.Enable();
				flag = true;
			}
		}
		if (flag)
		{
			return;
		}
		foreach (SensorSystemRadar airSearchRadar in _obp._airSearchRadars)
		{
			if (airSearchRadar._radar._rp._role == RadarParameters.Role.SurfaceAndAir && !airSearchRadar.Inoperable.Value && !airSearchRadar.IsOn.Value)
			{
				airSearchRadar.Enable();
			}
		}
	}

	public void DisableRadarsForSurfaceSearch(bool formationCheck = false)
	{
		if (_obp == null)
		{
			return;
		}
		if (formationCheck && FollowingFormation)
		{
			foreach (Station station in Formation.Stations)
			{
				if (!(station.UnitObject == this) && station.UnitObject.FollowingFormation)
				{
					station.UnitObject.DisableRadarsForSurfaceSearch();
				}
			}
		}
		bool flag = false;
		foreach (SensorSystemRadar surfaceSearchRadar in _obp._surfaceSearchRadars)
		{
			if (!surfaceSearchRadar.Inoperable.Value && surfaceSearchRadar.IsOn.Value)
			{
				surfaceSearchRadar.Disable();
				flag = true;
			}
		}
		if (flag)
		{
			return;
		}
		foreach (SensorSystemRadar airSearchRadar in _obp._airSearchRadars)
		{
			if (airSearchRadar._radar._rp._role == RadarParameters.Role.SurfaceAndAir && !airSearchRadar.Inoperable.Value && airSearchRadar.IsOn.Value)
			{
				airSearchRadar.Disable();
			}
		}
	}

	public bool HasSurfaceSearchRadar()
	{
		if (_obp == null)
		{
			return false;
		}
		foreach (SensorSystemRadar surfaceSearchRadar in _obp._surfaceSearchRadars)
		{
			if (!surfaceSearchRadar.Inoperable.Value)
			{
				return true;
			}
		}
		return false;
	}

	public void EnableSurfaceSearchRadars(bool setTimer = false, bool formationCheck = false)
	{
		if (_obp == null)
		{
			return;
		}
		if (_obp._surfaceSearchRadars.Any((SensorSystemRadar searchRadar) => !searchRadar.Inoperable.Value && !searchRadar.IsOn.Value))
		{
			LogForcedEmconRadarActivationAttempt("surface-search radar");
		}
		foreach (SensorSystemRadar surfaceSearchRadar in _obp._surfaceSearchRadars)
		{
			if (!surfaceSearchRadar.Inoperable.Value && !surfaceSearchRadar.IsOn.Value)
			{
				surfaceSearchRadar.Enable();
			}
		}
		if (!formationCheck || !FollowingFormation)
		{
			return;
		}
		foreach (Station station in Formation.Stations)
		{
			if (!(station.UnitObject == this) && station.UnitObject.FollowingFormation)
			{
				station.UnitObject.EnableSurfaceSearchRadars();
			}
		}
	}

	public void DisableSurfaceSearchRadars(bool formationCheck = false)
	{
		if (_obp == null)
		{
			return;
		}
		foreach (SensorSystemRadar surfaceSearchRadar in _obp._surfaceSearchRadars)
		{
			if (!surfaceSearchRadar.Inoperable.Value && surfaceSearchRadar.IsOn.Value)
			{
				surfaceSearchRadar.Disable();
			}
		}
		if (!formationCheck || !FollowingFormation)
		{
			return;
		}
		foreach (Station station in Formation.Stations)
		{
			if (!(station.UnitObject == this) && station.UnitObject.FollowingFormation)
			{
				station.UnitObject.DisableSurfaceSearchRadars();
			}
		}
	}

	private void LogForcedEmconRadarActivationAttempt(string radarType)
	{
		AI ai = _ai;
		if (ai != null && ai._excludeFromAIRadarRoutine)
		{
			MethodBase methodBase = new StackTrace(1, fNeedFileInfo: false).GetFrames()?.Select((StackFrame frame) => frame.GetMethod()).FirstOrDefault((MethodBase method) => method?.DeclaringType != typeof(ObjectBase));
			string text = ((methodBase?.DeclaringType == null) ? "unknown caller" : (methodBase.DeclaringType.Name + "." + methodBase.Name));
			LightAITrace.Log("ForceEMCON", $"{getUIDAndName()} received an {radarType} activation request while forced EMCON is active; observed inbound exception: {HasObservedIdentifiedInboundWeapon()}; caller: {text}.");
		}
	}

	public bool HasActiveSonar()
	{
		if (_obp == null)
		{
			return false;
		}
		foreach (SensorSystemSonar activeSonarSystem in _obp._activeSonarSystems)
		{
			if (!activeSonarSystem.Inoperable.Value)
			{
				return true;
			}
		}
		return false;
	}

	public bool HasTowedSonar()
	{
		if (_obp == null)
		{
			return false;
		}
		foreach (SensorSystemSonar towedSonarSystem in _obp._towedSonarSystems)
		{
			if (!towedSonarSystem.Inoperable.Value)
			{
				return true;
			}
		}
		return false;
	}

	public bool HasClipOnArray()
	{
		if (IsDestroyed || _obp?._towedSonarSystems == null)
		{
			return false;
		}
		foreach (SensorSystemSonar towedSonarSystem in _obp._towedSonarSystems)
		{
			if (!towedSonarSystem.Inoperable.Value && towedSonarSystem._parentTowedSystem != null && towedSonarSystem._parentTowedSystem._isClipOnArray)
			{
				return true;
			}
		}
		return false;
	}

	public void EnableActiveSonars(bool excludeTowed = false, bool voiceover = true, bool formationCheck = false)
	{
		if (_obp == null)
		{
			return;
		}
		foreach (SensorSystemSonar activeSonarSystem in _obp._activeSonarSystems)
		{
			if (!excludeTowed || !_obp._towedSonarSystems.Contains(activeSonarSystem))
			{
				activeSonarSystem._sonar.IsActive.Value = true;
			}
		}
		if (voiceover && IsPlayerObject)
		{
			Singleton<VoiceMessage>.Instance.PlayVoiceMessage("ActiveSonarOn", this);
		}
		if (!formationCheck || !FollowingFormation)
		{
			return;
		}
		foreach (Station station in Formation.Stations)
		{
			if (!(station.UnitObject == this) && station.UnitObject.FollowingFormation)
			{
				station.UnitObject.EnableActiveSonars(excludeTowed, voiceover: false);
			}
		}
	}

	public void DisableActiveSonars(bool excludeTowed = false, bool voiceover = true, bool formationCheck = false)
	{
		if (_obp == null)
		{
			return;
		}
		foreach (SensorSystemSonar activeSonarSystem in _obp._activeSonarSystems)
		{
			if (!excludeTowed || !_obp._towedSonarSystems.Contains(activeSonarSystem))
			{
				activeSonarSystem._sonar.IsActive.Value = false;
			}
		}
		if (voiceover && IsPlayerObject)
		{
			Singleton<VoiceMessage>.Instance.PlayVoiceMessage("ActiveSonarOff", this);
		}
		if (!formationCheck || !FollowingFormation)
		{
			return;
		}
		foreach (Station station in Formation.Stations)
		{
			if (!(station.UnitObject == this) && station.UnitObject.FollowingFormation)
			{
				station.UnitObject.DisableActiveSonars(excludeTowed, voiceover: false);
			}
		}
	}

	public void DeployTowedSonars(bool allowMessage = true, bool deployActive = false, bool formationCheck = false)
	{
		if (_obp == null)
		{
			return;
		}
		foreach (SensorSystemSonar towedSonarSystem in _obp._towedSonarSystems)
		{
			if (!towedSonarSystem._wasSetOn && !towedSonarSystem.Inoperable.Value && (deployActive || !towedSonarSystem.IsActiveSensor))
			{
				towedSonarSystem.Enable(allowMessage);
			}
		}
		if (!formationCheck || !FollowingFormation)
		{
			return;
		}
		foreach (Station station in Formation.Stations)
		{
			if (!(station.UnitObject == this) && station.UnitObject.FollowingFormation)
			{
				station.UnitObject.DeployTowedSonars(allowMessage: false, deployActive);
			}
		}
	}

	public void RetractTowedSonars(bool formationCheck = false)
	{
		if (_obp == null)
		{
			return;
		}
		if (_ai != null)
		{
			_ai._turnOffTowedSonarsTime = -1.0;
			_ai._resumeTelegraphAfterDip = int.MinValue;
		}
		foreach (SensorSystemSonar towedSonarSystem in _obp._towedSonarSystems)
		{
			if ((towedSonarSystem._parentTowedSystem == null || !towedSonarSystem._parentTowedSystem._isClipOnArray) && !towedSonarSystem.Inoperable.Value && (towedSonarSystem.IsOn.Value || towedSonarSystem._wasSetOn))
			{
				towedSonarSystem.Disable();
			}
		}
		if (!formationCheck || !FollowingFormation)
		{
			return;
		}
		foreach (Station station in Formation.Stations)
		{
			if (!(station.UnitObject == this) && station.UnitObject.FollowingFormation)
			{
				station.UnitObject.RetractTowedSonars();
			}
		}
	}

	public void TowedSonarsSetActive(bool active)
	{
		if (_obp == null)
		{
			return;
		}
		foreach (SensorSystemSonar towedSonarSystem in _obp._towedSonarSystems)
		{
			if (towedSonarSystem.IsActiveSensor && !towedSonarSystem._sonar._sp._isActiveOnly && towedSonarSystem.IsOn.Value && towedSonarSystem._sonar.IsActive.Value != active)
			{
				towedSonarSystem._sonar.IsActive.Value = active;
			}
		}
	}

	public void TowedSonarsSetSearchDepth(bool below)
	{
		if (_obp == null)
		{
			return;
		}
		foreach (SensorSystemSonar towedSonarSystem in _obp._towedSonarSystems)
		{
			if (towedSonarSystem.IsOn.Value && towedSonarSystem.SearchBelowLayer.Value != below)
			{
				towedSonarSystem.SearchBelowLayer.Value = below;
			}
		}
	}

	public bool HasESM()
	{
		if (_obp == null)
		{
			return false;
		}
		foreach (SensorSystemESM esmSystem in _obp._esmSystems)
		{
			if (!esmSystem.Inoperable.Value)
			{
				return true;
			}
		}
		return false;
	}

	public void SearchForHomeBase()
	{
		_searchForHomeBase = false;
		if (_homeBase != null)
		{
			return;
		}
		float num = (15f + RangeInKm.Value) * 1000f * 0.0148809375f;
		num *= num;
		ObjectBase objectBase = null;
		float num2 = float.MaxValue;
		foreach (ObjectBase taskforceUnit in _taskforce._taskforceUnits)
		{
			if (taskforceUnit._hasControl && !taskforceUnit.IsDestroyed && taskforceUnit.CanAcceptAircraft(this, message: false))
			{
				float sqrMagnitude = (base.transform.position - taskforceUnit.transform.position).sqrMagnitude;
				if (!(sqrMagnitude > num) && num2 > sqrMagnitude)
				{
					num2 = sqrMagnitude;
					objectBase = taskforceUnit;
				}
			}
		}
		if (objectBase != null)
		{
			Rebase(objectBase);
		}
		else if (StateMachine.CurrentStateName == "BingoFuel")
		{
			if (IsPlayerObject)
			{
				Globals._mainGameViewModel.RaiseAlert("PACEHOLDER: " + getUIDAndName() + " destroyed because it was BingoFuel and its home base is no longer functioning");
			}
			destroyObject();
		}
	}

	public void Rebase(ObjectBase homeBase)
	{
		if (_homeBase != null && _homeBase._childAircraft.Contains(this))
		{
			_homeBase._childAircraft.Remove(this);
		}
		_homeBase = homeBase;
		if (!_homeBase._childAircraft.Contains(this))
		{
			_homeBase._childAircraft.Add(this);
		}
	}

	public bool FlightDeckHasUnit(ObjectType type, ObjectBaseParameters.UnitRoles role = ObjectBaseParameters.UnitRoles.None)
	{
		if (_obp?._flightDeck == null)
		{
			return false;
		}
		if (_obp._flightDeck.TotalVehiclesOnBoard() < 1)
		{
			return false;
		}
		foreach (VehicleTypeOnBoard item in _obp._flightDeck._vehiclesOnBoard)
		{
			if ((item._objectType == type || type == ObjectType.Any) && item.Numbers >= 1)
			{
				if (role == ObjectBaseParameters.UnitRoles.None)
				{
					return true;
				}
				if (item._unitRoles.Contains(role))
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool FlightDeckHasUnit(string fileName)
	{
		if (_obp?._flightDeck == null)
		{
			return false;
		}
		if (_obp._flightDeck.TotalVehiclesOnBoard() < 1)
		{
			return false;
		}
		foreach (VehicleTypeOnBoard item in _obp._flightDeck._vehiclesOnBoard)
		{
			if (!(item._fileName != fileName) && item.Numbers >= 1)
			{
				return true;
			}
		}
		return false;
	}

	public int FlightDeckGetNumberOfUnits(ObjectType type, ObjectBaseParameters.UnitRoles role = ObjectBaseParameters.UnitRoles.None)
	{
		if (_obp?._flightDeck == null)
		{
			return 0;
		}
		if (_obp._flightDeck.TotalVehiclesOnBoard() < 1)
		{
			return 0;
		}
		int num = 0;
		foreach (VehicleTypeOnBoard item in _obp._flightDeck._vehiclesOnBoard)
		{
			if ((item._objectType == type || type == ObjectType.Any) && item.Numbers >= 1 && (role == ObjectBaseParameters.UnitRoles.None || item._unitRoles.Contains(role)))
			{
				num += item.Numbers;
			}
		}
		return num;
	}

	public int FlightDeckGetNumberOfUnits(string fileName)
	{
		if (_obp?._flightDeck == null)
		{
			return 0;
		}
		if (_obp._flightDeck.TotalVehiclesOnBoard() < 1)
		{
			return 0;
		}
		foreach (VehicleTypeOnBoard item in _obp._flightDeck._vehiclesOnBoard)
		{
			if (item._fileName == fileName)
			{
				return item.Numbers;
			}
		}
		return 0;
	}

	public bool FlightDeckLaunchUnit(ObjectType type, string loadoutName = "Default", ObjectBaseParameters.UnitRoles role = ObjectBaseParameters.UnitRoles.None, FlightDeckTask.MissionType missionType = FlightDeckTask.MissionType.None, bool allowLaunch = true, float readyUpTime = float.NaN, bool allowDefaultLoadout = true)
	{
		if (_obp?._flightDeck == null)
		{
			return false;
		}
		FlightDeck flightDeck = _obp._flightDeck;
		if (flightDeck.TotalVehiclesOnBoard() < 1)
		{
			return false;
		}
		foreach (VehicleTypeOnBoard item in flightDeck._vehiclesOnBoard)
		{
			if ((item._objectType != type && type != ObjectType.Any) || item.Numbers < 1)
			{
				continue;
			}
			if (item.Loadouts.Count == 0)
			{
				return false;
			}
			if (role != 0 && !item._unitRoles.Contains(role))
			{
				continue;
			}
			foreach (Squadron squadron in item.Squadrons)
			{
				if (squadron.Numbers < 1)
				{
					continue;
				}
				Loadout loadout = item.GetLoadoutByName(loadoutName);
				if (loadout == null)
				{
					if (loadoutName == "ASW")
					{
						loadout = item.GetLoadoutByName("ASWHunter");
					}
					if (loadout == null)
					{
						if (!allowDefaultLoadout)
						{
							return false;
						}
						loadout = item.GetLoadoutByName("Default");
					}
				}
				if (loadout == null)
				{
					loadout = item.Loadouts[0];
				}
				string callsign = squadron.Callsigns[0];
				LaunchTaskParameters launchTaskParameters = new LaunchTaskParameters();
				launchTaskParameters._missionType = missionType;
				if (flightDeck.createLaunchTask(item, loadout, squadron, callsign, launchTaskParameters, allowLaunch, readyUpTime) == null)
				{
					return false;
				}
				DM.I(getUIDAndName() + " is auto-launching " + item.DisplayName);
				return true;
			}
		}
		return false;
	}

	public bool FlightDeckHasExactLoadout(string fileName, string loadoutName, string squadronName = "")
	{
		if (_obp?._flightDeck == null || string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(loadoutName))
		{
			return false;
		}
		return _obp._flightDeck._vehiclesOnBoard.Any((VehicleTypeOnBoard vehicle) => vehicle != null && string.Equals(vehicle._fileName, fileName, StringComparison.OrdinalIgnoreCase) && vehicle.Numbers > 0 && vehicle.Squadrons.Any((Squadron squadron) => squadron != null && squadron.Numbers > 0 && (string.IsNullOrWhiteSpace(squadronName) || string.Equals(squadron._name, squadronName, StringComparison.OrdinalIgnoreCase))) && vehicle.Loadouts.Any((Loadout loadout) => loadout != null && string.Equals(loadout.Name, loadoutName, StringComparison.OrdinalIgnoreCase)));
	}

	public bool FlightDeckLaunchUnit(string fileName, LaunchTaskParameters ltp, string loadoutName = "Default", bool requireExactLoadout = false, string squadronName = "")
	{
		if (_obp?._flightDeck == null)
		{
			return false;
		}
		FlightDeck flightDeck = _obp._flightDeck;
		if (flightDeck.TotalVehiclesOnBoard() < 1)
		{
			return false;
		}
		foreach (VehicleTypeOnBoard item in flightDeck._vehiclesOnBoard)
		{
			if (item._fileName != fileName)
			{
				continue;
			}
			if (item.Numbers < 1)
			{
				return false;
			}
			if (item.Loadouts.Count == 0)
			{
				return false;
			}
			foreach (Squadron squadron in item.Squadrons)
			{
				if (squadron.Numbers < 1 || (!string.IsNullOrWhiteSpace(squadronName) && !string.Equals(squadron._name, squadronName, StringComparison.OrdinalIgnoreCase)))
				{
					continue;
				}
				Loadout loadout = null;
				foreach (Loadout loadout2 in item.Loadouts)
				{
					if (string.Equals(loadout2.Name, loadoutName, StringComparison.OrdinalIgnoreCase))
					{
						loadout = loadout2;
						break;
					}
				}
				if (loadout == null)
				{
					if (requireExactLoadout)
					{
						return false;
					}
					foreach (Loadout loadout3 in item.Loadouts)
					{
						if (loadout3.Name == "Default")
						{
							loadout = loadout3;
							break;
						}
					}
				}
				if (loadout == null)
				{
					loadout = item.Loadouts[0];
				}
				string callsign = squadron.Callsigns[0];
				if (flightDeck.createLaunchTask(item, loadout, squadron, callsign, ltp) == null)
				{
					return false;
				}
				DM.I(getUIDAndName() + " is auto-launching " + item.DisplayName);
				return true;
			}
		}
		return false;
	}

	private static bool IsNightCapableFit(VehicleTypeOnBoard vehicle, Loadout loadout)
	{
		return LoadoutSummary.HasNightCapableSensors(loadout.OwnerIni ?? Utils.GetSOP("aircraft", vehicle._fileName)?._ini, loadout.Name, airTargets: true);
	}

	public bool FlightDeckLaunchCAP(ObjectBaseParameters.UnitRoles role = ObjectBaseParameters.UnitRoles.None)
	{
		if (_obp?._flightDeck == null)
		{
			return false;
		}
		FlightDeck flightDeck = _obp._flightDeck;
		if (flightDeck.TotalVehiclesOnBoard() < 1)
		{
			return false;
		}
		List<VehicleTypeOnBoard> list = new List<VehicleTypeOnBoard>();
		bool flag = SensorUtils.IsTooDarkForVisualOnlySensors();
		foreach (VehicleTypeOnBoard item in flightDeck._vehiclesOnBoard)
		{
			if (item._objectType != ObjectType.Aircraft || item.Numbers < 1 || item.Loadouts.Count == 0 || (item._permittedUsages.Count > 0 && (item._permittedUsages.Contains(VehicleTypeOnBoard.PermittedUsage.None) || (!item._permittedUsages.Contains(VehicleTypeOnBoard.PermittedUsage.All) && !item._permittedUsages.Contains(VehicleTypeOnBoard.PermittedUsage.CAP)))) || (role != 0 && !item._unitRoles.Contains(role)))
			{
				continue;
			}
			bool flag2 = false;
			foreach (Squadron squadron3 in item.Squadrons)
			{
				if (squadron3.Numbers < 1)
				{
					continue;
				}
				foreach (Loadout loadout2 in item.Loadouts)
				{
					if (loadout2._loadoutCapabilities.HasAAM && !loadout2._loadoutCapabilities.HasAGM && !loadout2._loadoutCapabilities._hasBombs && !loadout2._loadoutCapabilities._hasCluster && (!flag || IsNightCapableFit(item, loadout2)) && loadout2.Available.Value >= 1)
					{
						list.Add(item);
						flag2 = true;
						break;
					}
				}
				if (flag2)
				{
					break;
				}
			}
		}
		if (list.Count < 1)
		{
			return false;
		}
		VehicleTypeOnBoard vehicle = null;
		Loadout loadout = null;
		Squadron squadron = null;
		float num = -1f;
		bool flag3 = false;
		foreach (VehicleTypeOnBoard item2 in list)
		{
			Squadron squadron2 = null;
			foreach (Squadron squadron4 in item2.Squadrons)
			{
				if (squadron4.Numbers >= 1)
				{
					squadron2 = squadron4;
					break;
				}
			}
			if (squadron2 == null)
			{
				continue;
			}
			foreach (Loadout loadout3 in item2.Loadouts)
			{
				if (loadout3.Available.Value >= 1 && loadout3._loadoutCapabilities.HasAAM && (!flag || IsNightCapableFit(item2, loadout3)))
				{
					bool flag4 = !loadout3._loadoutCapabilities.CanStrike;
					float totalAAMRange = loadout3._loadoutCapabilities._totalAAMRange;
					if (loadout == null || (flag4 && !flag3) || (flag4 == flag3 && totalAAMRange > num))
					{
						vehicle = item2;
						loadout = loadout3;
						squadron = squadron2;
						num = totalAAMRange;
						flag3 = flag4;
					}
				}
			}
		}
		if (loadout == null)
		{
			return false;
		}
		LaunchTaskParameters launchTaskParameters = new LaunchTaskParameters();
		launchTaskParameters._missionType = FlightDeckTask.MissionType.CAP;
		_obp._flightDeck.createLaunchTask(vehicle, loadout, squadron, squadron.Callsigns[0], launchTaskParameters);
		return true;
	}

	public VehicleTypeOnBoard FlightDeckLaunchInterceptor(ObjectBaseParameters.UnitRoles role = ObjectBaseParameters.UnitRoles.None)
	{
		if (_obp?._flightDeck == null)
		{
			return null;
		}
		FlightDeck flightDeck = _obp._flightDeck;
		if (flightDeck.TotalVehiclesOnBoard() < 1)
		{
			return null;
		}
		List<VehicleTypeOnBoard> list = new List<VehicleTypeOnBoard>();
		bool flag = SensorUtils.IsTooDarkForVisualOnlySensors();
		foreach (VehicleTypeOnBoard item in flightDeck._vehiclesOnBoard)
		{
			if (item._objectType != ObjectType.Aircraft || item.Numbers < 1 || item.Loadouts.Count == 0 || (role != 0 && !item._unitRoles.Contains(role)) || (item._permittedUsages.Count > 0 && (item._permittedUsages.Contains(VehicleTypeOnBoard.PermittedUsage.None) || (!item._permittedUsages.Contains(VehicleTypeOnBoard.PermittedUsage.All) && !item._permittedUsages.Contains(VehicleTypeOnBoard.PermittedUsage.CAP)))))
			{
				continue;
			}
			bool flag2 = false;
			foreach (Squadron squadron in item.Squadrons)
			{
				if (squadron.Numbers < 1)
				{
					continue;
				}
				foreach (Loadout loadout in item.Loadouts)
				{
					if (loadout._loadoutCapabilities.HasAAM && (!flag || IsNightCapableFit(item, loadout)) && loadout.Available.Value >= 1)
					{
						list.Add(item);
						flag2 = true;
						break;
					}
				}
				if (flag2)
				{
					break;
				}
			}
		}
		if (list.Count < 1)
		{
			return null;
		}
		foreach (VehicleTypeOnBoard item2 in list)
		{
			foreach (Squadron squadron2 in item2.Squadrons)
			{
				if (squadron2.Numbers < 1)
				{
					continue;
				}
				foreach (Loadout loadout2 in item2.Loadouts)
				{
					if (loadout2.Available.Value < 1)
					{
						return null;
					}
					if (loadout2._loadoutCapabilities._hasLRAAM && !loadout2._loadoutCapabilities.CanStrike && (!flag || IsNightCapableFit(item2, loadout2)))
					{
						LaunchTaskParameters launchTaskParameters = new LaunchTaskParameters();
						launchTaskParameters._missionType = FlightDeckTask.MissionType.Intercept;
						if (_obp._flightDeck.createLaunchTask(item2, loadout2, squadron2, squadron2.Callsigns[0], launchTaskParameters) == null)
						{
							return null;
						}
						return item2;
					}
				}
			}
		}
		foreach (VehicleTypeOnBoard item3 in list)
		{
			foreach (Squadron squadron3 in item3.Squadrons)
			{
				if (squadron3.Numbers < 1)
				{
					continue;
				}
				foreach (Loadout loadout3 in item3.Loadouts)
				{
					if (loadout3.Available.Value < 1)
					{
						return null;
					}
					if (loadout3._loadoutCapabilities._hasMRAAM && !loadout3._loadoutCapabilities.CanStrike && (!flag || IsNightCapableFit(item3, loadout3)))
					{
						LaunchTaskParameters launchTaskParameters2 = new LaunchTaskParameters();
						launchTaskParameters2._missionType = FlightDeckTask.MissionType.Intercept;
						if (_obp._flightDeck.createLaunchTask(item3, loadout3, squadron3, squadron3.Callsigns[0], launchTaskParameters2) == null)
						{
							return null;
						}
						return item3;
					}
				}
			}
		}
		foreach (VehicleTypeOnBoard item4 in list)
		{
			foreach (Squadron squadron4 in item4.Squadrons)
			{
				if (squadron4.Numbers < 1)
				{
					continue;
				}
				foreach (Loadout loadout4 in item4.Loadouts)
				{
					if (loadout4.Available.Value < 1)
					{
						return null;
					}
					if (loadout4._loadoutCapabilities._hasSRAAM && !loadout4._loadoutCapabilities.CanStrike && (!flag || IsNightCapableFit(item4, loadout4)))
					{
						LaunchTaskParameters launchTaskParameters3 = new LaunchTaskParameters();
						launchTaskParameters3._missionType = FlightDeckTask.MissionType.Intercept;
						if (_obp._flightDeck.createLaunchTask(item4, loadout4, squadron4, squadron4.Callsigns[0], launchTaskParameters3) == null)
						{
							return null;
						}
						return item4;
					}
				}
			}
		}
		foreach (VehicleTypeOnBoard item5 in list)
		{
			foreach (Squadron squadron5 in item5.Squadrons)
			{
				if (squadron5.Numbers < 1)
				{
					continue;
				}
				foreach (Loadout loadout5 in item5.Loadouts)
				{
					if (loadout5.Available.Value < 1)
					{
						return null;
					}
					if (loadout5._loadoutCapabilities.HasMLRAAM && (!flag || IsNightCapableFit(item5, loadout5)))
					{
						LaunchTaskParameters launchTaskParameters4 = new LaunchTaskParameters();
						launchTaskParameters4._missionType = FlightDeckTask.MissionType.Intercept;
						if (_obp._flightDeck.createLaunchTask(item5, loadout5, squadron5, squadron5.Callsigns[0], launchTaskParameters4) == null)
						{
							return null;
						}
						return item5;
					}
				}
			}
		}
		foreach (VehicleTypeOnBoard item6 in list)
		{
			foreach (Squadron squadron6 in item6.Squadrons)
			{
				if (squadron6.Numbers < 1)
				{
					continue;
				}
				foreach (Loadout loadout6 in item6.Loadouts)
				{
					if (loadout6.Available.Value < 1)
					{
						return null;
					}
					if (loadout6._loadoutCapabilities.HasAAM && (!flag || IsNightCapableFit(item6, loadout6)))
					{
						LaunchTaskParameters launchTaskParameters5 = new LaunchTaskParameters();
						launchTaskParameters5._missionType = FlightDeckTask.MissionType.Intercept;
						if (_obp._flightDeck.createLaunchTask(item6, loadout6, squadron6, squadron6.Callsigns[0], launchTaskParameters5) == null)
						{
							return null;
						}
						return item6;
					}
				}
			}
		}
		return null;
	}

	public List<(VehicleTypeOnBoard, int)> FlightDeckGetUnitAndNumberForLoadout(string loadoutName, VehicleTypeOnBoard.PermittedUsage usage, bool allowLarge = true, bool allowSmall = true, bool allowVeryLarge = true)
	{
		List<(VehicleTypeOnBoard, int)> list = new List<(VehicleTypeOnBoard, int)>();
		if (_obp?._flightDeck == null)
		{
			return list;
		}
		FlightDeck flightDeck = _obp._flightDeck;
		flightDeck.UpdateLoadoutsAvailability();
		foreach (VehicleTypeOnBoard item in flightDeck._vehiclesOnBoard)
		{
			if (item.Numbers < 1 || (item._isVeryLarge && !allowVeryLarge) || (item._isLarge && !allowLarge) || (!item._isLarge && !allowSmall) || (item._permittedUsages.Count > 0 && (item._permittedUsages.Contains(VehicleTypeOnBoard.PermittedUsage.None) || (!item._permittedUsages.Contains(VehicleTypeOnBoard.PermittedUsage.All) && !item._permittedUsages.Contains(usage)))))
			{
				continue;
			}
			foreach (Loadout loadout in item.Loadouts)
			{
				if (loadout.Name == loadoutName)
				{
					int num = Mathf.Min(item.Numbers, loadout.Available.Value);
					if (num > 0)
					{
						list.Add((item, num));
					}
					break;
				}
			}
		}
		return list;
	}

	public int GetEngagePriority(ObjectBase target)
	{
		int result = 1000;
		if (this is Vessel)
		{
			result = GetVesselEngagePriority(target);
		}
		else if (this is Submarine)
		{
			result = GetSubmarineEngagePriority(target);
		}
		else if (this is Aircraft)
		{
			result = GetAircraftEngagePriority(target);
		}
		else if (this is LandUnit)
		{
			result = GetLandUnitEngagePriority(target);
		}
		return result;
	}

	public int GetVesselEngagePriority(ObjectBase target, float distanceInUnity = -1f)
	{
		if (distanceInUnity < 0f)
		{
			distanceInUnity = (target.transform.position - base.transform.position).magnitude;
		}
		target.GetMapVehicle(_taskforce);
		if (_threats.Contains(target))
		{
			return 1 + target._incomingWeapons.Count + _alignmentTasks.Count((AlignUnitToTarget x) => x._targetObject == target);
		}
		if (target is Missile)
		{
			return 3 + target._incomingWeapons.Count;
		}
		if (target is Vessel && distanceInUnity < 220.47597f)
		{
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.FAC))
			{
				return 2;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW))
			{
				return 3;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Carrier))
			{
				return 4;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Landing))
			{
				return 4;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Transport))
			{
				return 4;
			}
			return 5;
		}
		if (target is Submarine && !target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SSB) && !target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SSBN))
		{
			return 6;
		}
		if (target is Submarine)
		{
			return 7;
		}
		if (target.isUnit())
		{
			bool flag = false;
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Carrier))
			{
				flag = true;
			}
			else if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Landing))
			{
				flag = true;
			}
			else if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Transport))
			{
				flag = true;
			}
			else if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Airfield))
			{
				flag = true;
			}
			if (flag)
			{
				return 8;
			}
		}
		if (target is Aircraft)
		{
			return 9;
		}
		if (target is Vessel)
		{
			return 10;
		}
		if (target is LandUnit)
		{
			return 11;
		}
		return 100;
	}

	public int GetSubmarineEngagePriority(ObjectBase target, float distanceInUnity = -1f)
	{
		if (distanceInUnity < 0f)
		{
			distanceInUnity = (target.transform.position - base.transform.position).magnitude;
		}
		if (_threats.Contains(target))
		{
			return 1;
		}
		if (target.isUnit())
		{
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Carrier))
			{
				return 2;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Landing))
			{
				return 3;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Transport))
			{
				return 3;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.RAS))
			{
				return 3;
			}
		}
		if (target is Submarine)
		{
			if (distanceInUnity < 132.286f)
			{
				return 4;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SSB) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SSBN))
			{
				return 5;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SSG) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SSGN))
			{
				return 5;
			}
			return 6;
		}
		if (target is Vessel)
		{
			return 7;
		}
		return 100;
	}

	public int GetAircraftEngagePriority(ObjectBase target, float distanceInUnity = -1f)
	{
		if (distanceInUnity < 0f)
		{
			distanceInUnity = (target.transform.position - base.transform.position).magnitude;
		}
		if (_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Fighter))
		{
			return GetFighterEngagePriority(target, distanceInUnity);
		}
		if (_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.MPA))
		{
			return GetMPAEngagePriority(target, distanceInUnity);
		}
		if (_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Bomber) || _obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.HeavyBomber))
		{
			return GetBomberEngagePriority(target, distanceInUnity);
		}
		return 100;
	}

	public int GetFighterEngagePriority(ObjectBase target, float distanceInUnity)
	{
		LoadoutCapabilities currentLoadoutCapabilities = (this as Aircraft)._currentLoadoutCapabilities;
		if (target is Missile missile)
		{
			if (!currentLoadoutCapabilities.HasAAM)
			{
				return 100;
			}
			return GetAamMissileInterceptPriority(missile, distanceInUnity);
		}
		if (target is Aircraft || target is Helicopter)
		{
			if (!currentLoadoutCapabilities.HasAAM)
			{
				return 100;
			}
			if (currentLoadoutCapabilities.CanStrike && !AirTargetIsMissileThreat(target))
			{
				return 100;
			}
			return GetAamAirTargetPriority(target, distanceInUnity);
		}
		if (currentLoadoutCapabilities.CanStrike && target.isUnit())
		{
			if (target is Vessel vessel)
			{
				float num = 220.47597f;
				num *= num;
				foreach (ObjectBase taskforceVessel in _taskforce._taskforceVessels)
				{
					if (!((target.transform.position - taskforceVessel.transform.position).sqrMagnitude > num))
					{
						if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.FAC))
						{
							return 1;
						}
						if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW))
						{
							return 2;
						}
						break;
					}
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Carrier))
				{
					return 3;
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Landing))
				{
					return 4;
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Transport))
				{
					return 4;
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW_VLR) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW_Gun) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.NGFS))
				{
					if (!(vessel.VP._displacement > 5000f))
					{
						return 6;
					}
					return 5;
				}
				return 7;
			}
			if (target is Submarine submarine && !submarine.isSubmerged())
			{
				return 7;
			}
			if (target is LandUnit)
			{
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Radar))
				{
					return 8;
				}
				return 9;
			}
			return 100;
		}
		return 100;
	}

	private int GetAamAirTargetPriority(ObjectBase target, float distanceInUnity)
	{
		float num = ((target is Helicopter) ? 0f : 0f);
		if (_taskforce.IsContactThreateningFormation(target))
		{
			num = ((target is Aircraft && target._initialLoadoutCapabilities.CanStrike) ? 5f : 4f);
		}
		else if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.AEW))
		{
			num = 3f;
		}
		else if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Fighter))
		{
			num = 2f;
		}
		else if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASW))
		{
			num = 1f;
		}
		float num2 = distanceInUnity / 27.559496f;
		int num3 = CountLiveIncomingShots(target, distanceInUnity) + CountAlliedAamFightersEngaging(target, distanceInUnity);
		return Mathf.Clamp(Mathf.RoundToInt(num2 - num + (float)num3 * 30f), 1, 99);
	}

	private int GetAamMissileInterceptPriority(Missile missile, float distanceInUnity)
	{
		if (!missile._isInterceptable)
		{
			return 100;
		}
		ObjectBase objectBase = missile.CurrentTarget ?? missile.CurrentIntendedTargetObject;
		if (!(objectBase is Vessel))
		{
			return 100;
		}
		if (_taskforce.RelationshipTo(objectBase._taskforce) != RelationsState.Friendly)
		{
			return 100;
		}
		if (CountLiveIncomingShots(missile, distanceInUnity) > 0)
		{
			return 100;
		}
		if (CountAlliedAamFightersEngaging(missile, distanceInUnity) > 0)
		{
			return 100;
		}
		return Mathf.Clamp(Mathf.RoundToInt(distanceInUnity / 27.559496f - 80f), 1, 99);
	}

	private static float CredibleClaimDistance(float ourDistanceInUnity)
	{
		if (float.IsPositiveInfinity(ourDistanceInUnity))
		{
			return float.PositiveInfinity;
		}
		return ourDistanceInUnity * 1.5f + 275.59497f;
	}

	private int CountAlliedAamFightersEngaging(ObjectBase target, float ourDistanceInUnity = float.PositiveInfinity)
	{
		return _taskforce.AamAircraftEngaging(target, this, CredibleClaimDistance(ourDistanceInUnity));
	}

	private int CountLiveIncomingShots(ObjectBase target, float ourDistanceInUnity = float.PositiveInfinity)
	{
		float num = CredibleClaimDistance(ourDistanceInUnity);
		int num2 = 0;
		foreach (WeaponBase incomingWeapon in target._incomingWeapons)
		{
			if (!(incomingWeapon == null) && !incomingWeapon.IsDestroyed && !incomingWeapon._externalDestructionNotified && !incomingWeapon.IsDefeatedShot && (float.IsPositiveInfinity(num) || !((incomingWeapon.transform.position - target.transform.position).magnitude > num)))
			{
				num2++;
			}
		}
		return num2;
	}

	private bool AirTargetIsMissileThreat(ObjectBase target)
	{
		if (!(target is Aircraft))
		{
			return false;
		}
		return target._currentLoadoutCapabilities.HasAAM;
	}

	public int GetBomberEngagePriority(ObjectBase target, float distanceInUnity)
	{
		if (!target.isUnit())
		{
			return 100;
		}
		LoadoutCapabilities currentLoadoutCapabilities = (this as Aircraft)._currentLoadoutCapabilities;
		if (currentLoadoutCapabilities._hasARM || currentLoadoutCapabilities._hasCluster)
		{
			if (target.IsAirSearchRadarsOn.Value)
			{
				return 2;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Radar))
			{
				return 3;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SAM))
			{
				return 4;
			}
			return 100;
		}
		if (currentLoadoutCapabilities.CanStrike)
		{
			if (target is Vessel vessel)
			{
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Carrier))
				{
					return 1;
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Landing))
				{
					return 2;
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Transport))
				{
					return 2;
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.RAS))
				{
					return 2;
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW_VLR) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW_Gun) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.NGFS))
				{
					if (!(vessel.VP._displacement > 5000f))
					{
						return 4;
					}
					return 3;
				}
				return 5;
			}
			if (target is Submarine submarine && !submarine.isSubmerged())
			{
				return 5;
			}
			if (target is LandUnit)
			{
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Radar))
				{
					return 6;
				}
				return 7;
			}
			return 100;
		}
		return 100;
	}

	public int GetMPAEngagePriority(ObjectBase target, float distanceInUnity)
	{
		if (!target.isUnit())
		{
			return 100;
		}
		LoadoutCapabilities currentLoadoutCapabilities = (this as Aircraft)._currentLoadoutCapabilities;
		if (target is Submarine)
		{
			return 1;
		}
		if (currentLoadoutCapabilities.CanStrike)
		{
			if (target is Vessel vessel)
			{
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Carrier))
				{
					return 2;
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Landing))
				{
					return 3;
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Transport))
				{
					return 3;
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.RAS))
				{
					return 3;
				}
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW_VLR) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW_Gun) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.NGFS))
				{
					if (!(vessel.VP._displacement > 5000f))
					{
						return 5;
					}
					return 4;
				}
				return 6;
			}
			if (target is LandUnit)
			{
				if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Radar))
				{
					return 7;
				}
				return 8;
			}
		}
		return 100;
	}

	public int GetLandUnitEngagePriority(ObjectBase target, float distanceInUnity = -1f)
	{
		if (distanceInUnity < 0f)
		{
			distanceInUnity = (target.transform.position - base.transform.position).magnitude;
		}
		if (_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SAM))
		{
			return GetSAMSiteEngagePriority(target, distanceInUnity);
		}
		if (_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASM))
		{
			return GetASMSiteEngagePriority(target, distanceInUnity);
		}
		if (_obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Gun))
		{
			return GetASMSiteEngagePriority(target, distanceInUnity);
		}
		return 100;
	}

	public int GetSAMSiteEngagePriority(ObjectBase target, float distanceInUnity)
	{
		if (_threats.Contains(target))
		{
			if (!target.isWeapon())
			{
				return 2;
			}
			return 1;
		}
		if (target is Aircraft)
		{
			return 3;
		}
		if (target is Helicopter)
		{
			return 4;
		}
		return 100;
	}

	public int GetASMSiteEngagePriority(ObjectBase target, float distanceInUnity)
	{
		if (target is Vessel vessel)
		{
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Carrier))
			{
				return 1;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Landing))
			{
				return 2;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Transport))
			{
				return 2;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.RAS))
			{
				return 2;
			}
			if (target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW_VLR) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW_Gun) || target._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.NGFS))
			{
				if (!(vessel.VP._displacement > 5000f))
				{
					return 4;
				}
				return 3;
			}
			return 5;
		}
		if (target is Submarine submarine && !submarine.isSubmerged())
		{
			return 5;
		}
		return 100;
	}

	private void AddEntity()
	{
		Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
		if ((!(Entity != Entity.Null) || !defaultGameObjectInjectionWorld.EntityManager.Exists(Entity)) && !PoolManager.IsPoolLoading)
		{
			Entity = defaultGameObjectInjectionWorld.EntityManager.CreateEntity(typeof(SPObject), typeof(SPObjectRef), typeof(GeoPositioned), typeof(GeoVelocity), typeof(ShouldSaveComponent));
			EntityManagerManagedComponentExtensions.SetComponentData(defaultGameObjectInjectionWorld.EntityManager, Entity, new SPObject
			{
				Object = this
			});
			defaultGameObjectInjectionWorld.EntityManager.SetComponentData(Entity, new SPObjectRef
			{
				ObjectUID = UniqueID,
				Name = base.name
			});
			if (_taskforce != null)
			{
				defaultGameObjectInjectionWorld.EntityManager.AddComponentData(Entity, new TaskforceMember
				{
					Taskforce = _taskforce.Entity
				});
			}
		}
	}

	public void OnInstantiated()
	{
		AddEntity();
	}

	public void PublishKinematics()
	{
		KinematicEnvelope? trueKinematics = TrueKinematics;
		if (trueKinematics.HasValue)
		{
			KinematicEnvelope valueOrDefault = trueKinematics.GetValueOrDefault();
			Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
			if (defaultGameObjectInjectionWorld != null && !(Entity == Entity.Null) && defaultGameObjectInjectionWorld.EntityManager.Exists(Entity))
			{
				defaultGameObjectInjectionWorld.EntityManager.AddComponentData(Entity, valueOrDefault);
			}
		}
	}

	public void OnAlive()
	{
	}

	public void OnPulledFromPool()
	{
		AddEntity();
	}

	public void OnReturnedToPool()
	{
		ClearEntity();
	}

	private void ClearEntity()
	{
		Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
		if (Entity != Entity.Null && defaultGameObjectInjectionWorld.EntityManager.HasComponent<SPObject>(Entity))
		{
			SPObject componentData = EntityManagerManagedComponentExtensions.GetComponentData<SPObject>(defaultGameObjectInjectionWorld.EntityManager, Entity);
			componentData.Object = null;
			EntityManagerManagedComponentExtensions.SetComponentData(defaultGameObjectInjectionWorld.EntityManager, Entity, componentData);
		}
		if (Entity != Entity.Null && defaultGameObjectInjectionWorld.EntityManager.HasComponent<SPObjectRef>(Entity))
		{
			defaultGameObjectInjectionWorld.EntityManager.RemoveComponent<SPObjectRef>(Entity);
		}
		Entity = Entity.Null;
	}

	private void ReleaseEntityObject()
	{
		Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
		if (defaultGameObjectInjectionWorld != null && defaultGameObjectInjectionWorld.IsCreated && !(Entity == Entity.Null) && defaultGameObjectInjectionWorld.EntityManager.Exists(Entity) && defaultGameObjectInjectionWorld.EntityManager.HasComponent<SPObject>(Entity))
		{
			SPObject componentData = EntityManagerManagedComponentExtensions.GetComponentData<SPObject>(defaultGameObjectInjectionWorld.EntityManager, Entity);
			if (componentData != null && (object)componentData.Object == this)
			{
				componentData.Object = null;
			}
		}
	}

	private void DisableEntity()
	{
		Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
		if (Entity != Entity.Null && defaultGameObjectInjectionWorld.EntityManager.HasComponent<SPObjectRef>(Entity))
		{
			defaultGameObjectInjectionWorld.EntityManager.RemoveComponent<SPObjectRef>(Entity);
		}
	}

	private void EnableEntity()
	{
		Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
		if (Entity != Entity.Null && defaultGameObjectInjectionWorld.EntityManager.Exists(Entity) && !defaultGameObjectInjectionWorld.EntityManager.HasComponent<SPObjectRef>(Entity))
		{
			defaultGameObjectInjectionWorld.EntityManager.AddComponentData(Entity, new SPObjectRef
			{
				ObjectUID = UniqueID,
				Name = base.name
			});
		}
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
