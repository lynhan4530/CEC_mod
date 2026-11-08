using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniRx;
using UnityEngine;

namespace SeaPower;

public class AmmunitionParameters
{
	public enum WarheadType
	{
		Unknown,
		None,
		HighExplosive,
		ArmorPiercing,
		HEAT,
		Illumination,
		Cluster,
		RunwayCratering,
		Fragmentation
	}

	public enum Penetration
	{
		Minor,
		Moderate,
		Heavy,
		Always
	}

	public enum ImpactSize
	{
		Tiny,
		VerySmall,
		Small,
		SemiSmall,
		Medium,
		Large,
		VeryLarge,
		Custom
	}

	public enum FuzeType
	{
		Impact,
		Proximity,
		GatedProximity,
		Timed
	}

	public enum GuidanceType
	{
		None,
		IRHoming,
		SemiActiveRadarHoming,
		ActiveRadarHoming,
		AntiRadiationHoming,
		LaserHoming,
		TVHoming,
		ActiveSonarHoming,
		PassiveSonarHoming,
		WakeHoming,
		ActivePassiveSonarHoming
	}

	public enum SecondaryPassiveRadarGuidanceType
	{
		None,
		Full,
		HomeOnJam
	}

	[JsonConverter(typeof(JsonConverter))]
	public abstract class MidCourseCorrection
	{
		public sealed class NoneMCC : MidCourseCorrection
		{
			public override bool IsNone => true;

			public override int Index => 0;

			internal NoneMCC()
			{
			}

			public override string ToString()
			{
				return "None";
			}
		}

		public sealed class RadioCommandMCC : MidCourseCorrection
		{
			public override bool IsRadioCommand => true;

			public override int Index => 1;

			internal RadioCommandMCC()
			{
				base.AllowsLauncherGuidance = true;
				base.RequiresMissileTracking = true;
				base.GivesRWRWarning = true;
			}

			public override string ToString()
			{
				return "RadioCommand";
			}
		}

		public sealed class WireGuidedMCC : MidCourseCorrection
		{
			public override bool IsWireGuided => true;

			public override int Index => 2;

			internal WireGuidedMCC()
			{
			}

			public override string ToString()
			{
				return "WireGuided";
			}
		}

		public sealed class DatalinkMCC : MidCourseCorrection
		{
			public override bool IsDatalink => true;

			public override int Index => 3;

			internal DatalinkMCC()
			{
				base.AllowsLauncherGuidance = true;
			}

			public override string ToString()
			{
				return "Datalink";
			}
		}

		public class JsonConverter : JsonConverter<MidCourseCorrection>
		{
			public override MidCourseCorrection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
				if (reader.TokenType == JsonTokenType.Number)
				{
					return FromIndex(reader.GetInt32());
				}
				if (reader.TokenType == JsonTokenType.String)
				{
					return reader.GetString() switch
					{
						"RadioCommand" => RadioCommand, 
						"WireGuided" => WireGuided, 
						"Datalink" => Datalink, 
						_ => None, 
					};
				}
				return None;
			}

			public override void Write(Utf8JsonWriter writer, MidCourseCorrection value, JsonSerializerOptions options)
			{
				writer.WriteNumberValue(value.Index);
			}
		}

		public static readonly MidCourseCorrection None = new NoneMCC();

		public static readonly MidCourseCorrection RadioCommand = new RadioCommandMCC();

		public static readonly MidCourseCorrection WireGuided = new WireGuidedMCC();

		public static readonly MidCourseCorrection Datalink = new DatalinkMCC();

		public virtual bool IsNone => false;

		public virtual bool IsRadioCommand => false;

		public virtual bool IsWireGuided => false;

		public virtual bool IsDatalink => false;

		public bool RequiresConnection
		{
			get
			{
				if (!IsRadioCommand)
				{
					return IsWireGuided;
				}
				return true;
			}
		}

		public bool RequiresSearchSensor => IsRadioCommand;

		public bool AllowsLauncherGuidance { get; protected set; }

		public bool RequiresMissileTracking { get; protected set; }

		public bool GivesRWRWarning { get; protected set; }

		public abstract int Index { get; }

		public static MidCourseCorrection FromIndex(int index)
		{
			return index switch
			{
				1 => RadioCommand, 
				2 => WireGuided, 
				3 => Datalink, 
				_ => None, 
			};
		}
	}

	public enum SearchMode
	{
		None,
		Snake,
		Circle,
		Spiral,
		Ladder
	}

	public enum LaunchSeekerMode
	{
		Auto,
		Primary,
		Secondary
	}

	public enum ReattackMode
	{
		None,
		Circle
	}

	public enum SearchDepth
	{
		None,
		Shallow,
		AboveLayer,
		BelowLayer,
		Deep
	}

	public enum LoftMode
	{
		None,
		Maintain,
		Target,
		Launcher
	}

	public enum KinematicsLevel
	{
		None,
		Automatic,
		Full
	}

	public string _ammunitionFileName;

	public string _nativeDisplayedName;

	public string _nativeDisplayedNickname;

	public string _natoDisplayedName;

	public string _natoDisplayedNickname;

	public string _displayedType;

	public Ammunition.Target _targetType;

	public Ammunition.Target _secondaryTargetType;

	public List<Ammunition.LandAttack> _landAttack = new List<Ammunition.LandAttack>();

	public bool _hasSecondaryTargetType;

	public Ammunition.Type _type;

	public Ammunition.Type _subType = Ammunition.Type.None;

	public List<ObjectBase.ObjectType> _canNotAttackTypes = new List<ObjectBase.ObjectType>();

	public float _saturationValue = 1f;

	public float _maximumMissileValue = 2f;

	public float _maximumTorpedoValue = 1f;

	public float _massInKg;

	public float _frontalAreaM2;

	public float _sideAreaM2;

	public float _bottomAreaM2;

	public float _fuelMassInKg;

	public int _iocDate;

	public int _focDate;

	public List<SensorSystem> _associatedSensors;

	public List<WeaponSystem> _associatedWeaponSystems = new List<WeaponSystem>();

	public Dictionary<string, AmmunitionLauncherPosition> _launcherPositions = new Dictionary<string, AmmunitionLauncherPosition>();

	public float _ammoPoints;

	public bool _isAccountable;

	public string _supplyCategory;

	public int _density = 1;

	public int _magazineAmmoCount;

	public string _decalClass;

	public string _groundDecalClass;

	public string[] _onHitWaterSpawn;

	public string[] _onHitGroundSpawn;

	public string _spawnLoadout;

	public float _maxRCS;

	public Penetration _penetration;

	public float _samDefenceBonus;

	public float _ciwsTargetHealth = -1f;

	public float _ciwsTargetArmor;

	public float _ciwsMissileEvasiveness;

	public float _ciwsArmorPenOverride = -1f;

	public Effect _boosterEffect;

	public Effect _inFlightEffect;

	public float _inFlightEffectStartTime;

	public Effect _sustainerEffect;

	public float _sustainerEffectStartTime;

	public Effect _underwaterTrailEffect;

	public Effect _parachuteEffect;

	public Effect _dudExplosionEffect;

	public Effect _hitDefaultExplosionEffect;

	public Effect _hitAirExplosionEffect;

	public Effect _hitShipExplosionEffect;

	public Effect _hitObjectExplosionEffect;

	public Effect _hitGroundExplosionEffect;

	public Effect _torpedoSurfaceSplashEffect;

	public Effect _debrisTrailEffect;

	public Effect _debrisSplashEffect;

	public Effect _hitWaterSplashEffect;

	public Effect _waterEntrySplashEffect;

	public float _trimDepth;

	public float _waypointTolerance = 0.1f;

	public float _muzzleVelocityInMeterPerSecond;

	public float _muzzleVelocityInUnity;

	public float _gravity;

	public float _roundMaxDistance;

	public float _circularErrorProbable;

	public float _maxAARangeInUnity = -1f;

	public float _maxGuidedCorrectionUnity;

	public Effect _trailEffect;

	public float _defensiveEffectTime;

	public float _chanceToSpoof;

	public float _reactionRange;

	public float _parachuteSlowDown;

	public float _underWaterVelocityDamping;

	public float _parachuteEndVelocityInKnots;

	public float _parachuteEndVelocity;

	public float _parachuteEndVelocitySq;

	public List<Vector2> _parachuteVelocities = new List<Vector2>();

	public float _parachuteDeployDelay;

	public float _parachuteDeployMaxAltitude;

	public float _maxAngleWithParachute;

	public List<SearchMode> _availableSearchModes;

	public SearchMode _defaultSearchMode;

	public float _searchVelocityInKnots;

	public ReattackMode _reattackMode;

	public float _reattackCircleDegreesPerSecond;

	public float _ladderWidth;

	public float _ladderTurnDegreesPerSecond;

	public Vector2 _spiralSearchParams;

	public float _spiralSearchRadius;

	public float _spiralSearchAngle;

	public float _defaultDepth;

	public float _snakeSearchAngle;

	public float _snakeSearchTurnCycleTime;

	public float _circleDegreesPerSecond;

	public float _circleMoveFwdAfterCircleInSeconds;

	public float _minAfterSpoofEffectTime;

	public float _maxAfterSpoofEffectTime;

	public float _fractionOfRangeToActivateSeeker;

	public List<Vector2> _torpedoSpeedSettings = new List<Vector2>();

	public bool _wireControlsDepth;

	public float _wireAntiCountermeasuresBonus;

	public float _wireBreakSpeedInKnots;

	public float _maxSpeedOnWireInKnots;

	public float _wireMinSeafloorClearanceUnity;

	public WarheadType _warheadType;

	public float _warheadPower;

	public float _warheadDudPower;

	public float _damageMultiplier = 1f;

	public bool _isInstantExplosion;

	public ImpactSize _impactSize;

	public float _impactSizeUnity;

	public float _bombletsEjectAltitude;

	public FuzeType _fuzeType;

	public float _fuzeProximityDistance;

	public float _fuzeGateSeconds = -1f;

	public bool _smartFuse;

	public string _sonarList;

	public bool _isActiveSonobuoy;

	public float _sonarDepth;

	public string _radarList;

	public string _visualList;

	public string _periscopeList;

	public string _ecmList;

	public float _sensorHeight;

	public float _transmitterRange;

	public float _dropDuration;

	public float _initialFlightPhaseDuration;

	public bool _performsSlightBallisticInitialFlight;

	public bool _waterEntryLaunch;

	public GuidanceType _guidanceType;

	public GuidanceType _landAttackGuidanceType;

	public GuidanceType _vesselAttackGuidanceType;

	public bool _isRetargetable = true;

	public bool _autoRetarget;

	public float _arSeekerDeviationCoefficient = 0.02f;

	public float _sizeGate;

	public float _rangeGate;

	public float _insDrift;

	public bool _hasLandAttackGuidance;

	public bool _hasVesselAttackGuidance;

	public MidCourseCorrection _midCourseCorrection;

	public List<string> _compatibleGuidanceSystems = new List<string>();

	public bool _forceDirectPoint;

	public bool _forcePopUp;

	public bool _requiresGuidance;

	public bool _targetRequired;

	public bool _inertialGuidance;

	public SecondaryPassiveRadarGuidanceType _secondaryPassiveRadarGuidanceType;

	public Globals.Frequency _frequency;

	public float _waveLength;

	public List<Globals.Frequency> _passiveRadarHomingFrequencies = new List<Globals.Frequency>();

	public bool _hasESM;

	public bool _terrainFollowFlight;

	public float _currentMinFlightAltUnity;

	public LoftMode _loftAltMode;

	public float _maxLoftAngle;

	public float _maxLoftAltUnity;

	public float _maxLoftVelocityInKnots;

	public bool _loftToSkim;

	public bool _terminalLoft;

	public bool _optimizeLoftAlt = true;

	public bool _ballisticLoft;

	public float _seaSkimmingAltFeet;

	public float _seaSkimmingAltUnity;

	public float _seaSkimmingSubAltUnity;

	public float _seaSkimmingMaxDescentAngle;

	public float _seaSkimmingNoseUp;

	public float _seaSkimmingStartDistToTargetUnity;

	public float _finalFlightPhaseMaxAngle;

	public float _finalFlightPhaseAltUnity;

	public LoftMode _finalFlightPhaseAltMode;

	public float _finalFlightPhaseDistToTarget;

	public float _finalFlightPhaseDistToTargetUnity;

	public float _terminalVelocityInKnots;

	public float _terminalApproachDistMiles;

	public float _terminalApproachDist;

	public float _terminalDelay;

	public LoftMode _terminalAltMode;

	public float _terminalAltUnity;

	public float _terminalDiveDistance;

	public float _terminalPopUpDist;

	public float _terminalPopUpHeight;

	public float _terminalPopUpAngle;

	public bool _localTerminalOnly;

	public bool _selectivePopUp;

	public float _searchForTargetsTime;

	public int _maxGroupSize;

	public float _groupJoinRangeUnity;

	public float _groupJoinRangeUnitySq;

	public float _groupSpacing;

	public float _groupLeaderAltUnity;

	public float _groupWingmanAltUnity;

	public bool _ignoreHeightDifferenceForTargetDist;

	public bool _requiresTargetToProceed;

	public bool _supportsBanking;

	public bool _isRearAspectOnly;

	public float _targetRearCone;

	public bool _sharedSensorLink;

	public float _nightVisionLevel;

	public float _zoom = 1f;

	public float _killProbability;

	public float _guidanceSystemPenalty;

	public float _outOfAltitudePenalty = 0.4f;

	public float _speedPenaltyMultiplier = 1f;

	public float _sizePenaltyMultiplier = 1f;

	public float _navigationGain = 5f;

	public float _circularErrorRadius;

	public float _circularErrorRadiusLarge;

	public float _circularErrorRadiusInstallation;

	public float _circularErrorRadiusMobileUnit;

	public float _minAltMalusFactor;

	public float _maxVelocityInKnots;

	public float _sustainerBurnTime;

	public float _sustainerBurnAcceleration;

	private float _dragCoefficient;

	public float _maxFlightTime;

	public bool _timeLimited;

	public float _typicalFiringAlt;

	public float _typicalTargetAlt;

	public float _typicalLaunchVelocity;

	public float _typicalTargetVelocity;

	private bool? _useKinematics;

	public bool _airLaunched;

	public float _ejectVelocity;

	public float _accelerationTime;

	public float _acceleration;

	public float _deceleration;

	public float _sinkRate;

	public float _maxDepthUnity;

	public float _minDepthUnity;

	public float _maxTurnRateDegrees;

	public float _maxTurnG;

	public float _toBearingTurnRateDegrees;

	public float _terminalVerticalTurnRate;

	public float _axialRotationDegrees;

	public float _seekerFOV;

	public float _narrowSeekerFOV;

	public float _wideSeekerFOV;

	public float _seekerGimbalFOV;

	public float _seekerActiveRange;

	public float _seekerPassiveRange;

	public float _wakeHomingSeekerRange;

	public float _turnDelayStartRange;

	public float _turnDelay;

	public float _wakeHomingSeekerSensitivity;

	public int _wakeHomingTurnSettingReliability;

	public float _seekerGain;

	public float _gainFactor;

	public float _seekerOutputPower;

	public float _radarSeekerPeakPower;

	public bool _targetMemory;

	public bool _useFinalFlightInLandMode = true;

	public List<ObjectBase.ObjectType> _defaultIgnoredTypes = new List<ObjectBase.ObjectType>();

	public float _launchReliability;

	public float _seekerReliability;

	public float _warheadReliability;

	public float _antiChaffBonus;

	public float _antiJammerBonus;

	public float _minLaunchRangeInMiles;

	public float _maxLaunchRangeInMiles;

	public Vector2 _launchRangesInUnity;

	public bool _requiresLOS;

	public Vector2 _horizonRangesInUnity;

	public float _defaultHorizonHeight;

	public float _timeToMaxRange;

	public Vector2 _launchAltitudesInUnity;

	public Vector2 _attackAltitudesInUnity = new Vector2(-10000f, 10000f);

	public float _maxAttackVelocity = float.MaxValue;

	public bool _autoAttackOutsideAltitudes = true;

	public float _selfDestructDelay;

	public float _wobblingStrength;

	public double _wobblingSpeed;

	public float _verticalWobblingStrength;

	public float _verticalWobblingSpeed;

	public Rigidbody _rigidbody;

	public HitBox _hitbox;

	public GameObject _collidersObject;

	public bool _hasSubmunition;

	public string _submunitionFileName;

	public Ammunition.Type _submunitionType;

	public float _submunitionSeekerRange;

	public float _submunitionSeekerFOV;

	public SearchMode _submunitionSearchMode;

	public bool _isInterceptable;

	public bool _antiTorpedo;

	public bool _requiresWarmUp;

	public float _lifeTime;

	public float _decoySpoofChance = 0.3f;

	public bool _alwaysDisplayWhenSpawned;

	public ObjectSoundParameters.SoundMode _soundMode;

	public string _boosterAudioClipName;

	public Vector3 _boosterAudioPosition;

	public string _boosterAudioRollOff;

	public Vector2 _boosterAudioDistance;

	public float _boosterAudioPitch;

	public float _boosterAudioVolume;

	public GameObject _boosterAudioObject;

	public string _boosterAudioAudibility;

	public string _sustainerAudioClipName;

	public Vector3 _sustainerAudioPosition;

	public string _sustainerAudioRollOff;

	public Vector2 _sustainerAudioDistance;

	public float _sustainerAudioPitch;

	public Vector2 _sustainerAudioVolumeRange;

	public float _sustainerAudioAspectAngle;

	public float _sustainerAudioAspectArcHalfRange;

	public GameObject _sustainerAudioObject;

	public string _sustainerAudioAudibility;

	public string _exhaustAudioClipName;

	public Vector3 _exhaustAudioPosition;

	public string _exhaustAudioRollOff;

	public Vector2 _exhaustAudioDistance;

	public float _exhaustAudioPitch;

	public Vector2 _exhaustAudioVolumeRange;

	public float _exhaustAudioAspectAngle;

	public float _exhaustAudioAspectArcHalfRange;

	public GameObject _exhaustAudioObject;

	public string _exhaustAudioAudibility;

	public string _farAudioClipName;

	public Vector3 _farAudioPosition;

	public string _farAudioRollOff;

	public Vector2 _farAudioDistance;

	public float _farAudioPitch;

	public float _farAudioVolume;

	public GameObject _farAudioObject;

	public Vector2 _farAudioFadeRange;

	public string _farAudioAudibility;

	public string _propAudioClipName;

	public Vector3 _propAudioPosition;

	public string _propAudioRollOff;

	public Vector2 _propAudioDistance;

	public float _propAudioPitch;

	public float _propAudioVolume;

	public GameObject _propAudioObject;

	public Vector2 _propAudioFadeRange;

	public string _propAudioAudibility;

	public string _sonarAudioClipName;

	public Vector3 _sonarAudioPosition;

	public string _sonarAudioRollOff;

	public Vector2 _sonarAudioDistance;

	public float _sonarAudioPitch;

	public float _sonarAudioVolume;

	public AudioSource _sonarAudioSource;

	public GameObject _sonarAudioObject;

	public string _sonarAudioAudibility;

	public float _noisemakerAudioVolume;

	public string _noisemakerAudioAudibility;

	public int _addFire;

	public string _tacviewTag;

	public string _tacviewName;

	private static readonly ConcurrentDictionary<(string name, int version), AmmunitionParameters> _sharedByType = new ConcurrentDictionary<(string, int), AmmunitionParameters>();

	public string _displayedName
	{
		get
		{
			if (!OptionsManager.NatoWeaponNamesEnabled || string.IsNullOrEmpty(_natoDisplayedName))
			{
				return _nativeDisplayedName;
			}
			return _natoDisplayedName;
		}
		set
		{
			_nativeDisplayedName = value;
		}
	}

	public string _displayedNickname
	{
		get
		{
			if (!OptionsManager.NatoWeaponNamesEnabled || string.IsNullOrEmpty(_natoDisplayedNickname))
			{
				return _nativeDisplayedNickname;
			}
			return _natoDisplayedNickname;
		}
		set
		{
			_nativeDisplayedNickname = value;
		}
	}

	public ReactiveProperty<SearchMode> SearchModeSetting { get; private set; } = new ReactiveProperty<SearchMode>(SearchMode.None);


	public ReactiveProperty<SearchDepth> SearchDepthSetting { get; private set; } = new ReactiveProperty<SearchDepth>(SearchDepth.None);


	public bool HasProximitySensing => _fuzeType != FuzeType.Impact;

	public bool HasDayOnlySeeker
	{
		get
		{
			if (_guidanceType == GuidanceType.TVHoming)
			{
				return _nightVisionLevel <= 0f;
			}
			return false;
		}
	}

	public bool RequiresCrewVision
	{
		get
		{
			switch (_type)
			{
			case Ammunition.Type.Bomb:
				if (_subType != Ammunition.Type.Sonobuoy && _subType != Ammunition.Type.AirDepthCharge && _subType != Ammunition.Type.Fueltank)
				{
					return _subType != Ammunition.Type.Paratrooper;
				}
				return false;
			case Ammunition.Type.Projectile:
			case Ammunition.Type.AerialRocket:
			case Ammunition.Type.MLRS:
				return true;
			case Ammunition.Type.Missile:
				if (_guidanceType != GuidanceType.TVHoming || !(_nightVisionLevel <= 0f))
				{
					return _midCourseCorrection.IsRadioCommand;
				}
				return true;
			default:
				return false;
			}
		}
	}

	public float MinVelocity => MathF.Min(_maxVelocityInKnots * 0.33f, 600f);

	public float TotalDeltaV
	{
		get
		{
			float num = _acceleration * _accelerationTime * 9.81f * 1.94384f;
			float num2 = ((_sustainerBurnTime > 0f && _sustainerBurnAcceleration > 0f) ? (_sustainerBurnAcceleration * _sustainerBurnTime * 9.81f * 1.94384f) : 0f);
			return num + num2;
		}
	}

	public float TotalBurnTime => _accelerationTime + MathF.Max(_sustainerBurnAcceleration, 0f);

	public float LiftFactor { get; private set; } = 0.005f;


	public float TypicalFiringAlt
	{
		get
		{
			if (_typicalFiringAlt > -0.5f)
			{
				return _typicalFiringAlt * 0.00453571f;
			}
			if (!_airLaunched)
			{
				return 0f;
			}
			if (226.78f > _launchAltitudesInUnity.y)
			{
				return _launchAltitudesInUnity.y;
			}
			return 226.78f;
		}
	}

	public float TypicalTargetAlt
	{
		get
		{
			if (_typicalTargetAlt > -0.5f)
			{
				return _typicalTargetAlt * 0.00453571f;
			}
			if (_targetType != Ammunition.Target.AAW)
			{
				return 0f;
			}
			if (226.78f > _attackAltitudesInUnity.y)
			{
				return _attackAltitudesInUnity.y;
			}
			return 226.78f;
		}
	}

	public float TypicalLaunchVelocity
	{
		get
		{
			if (_typicalLaunchVelocity > -0.5f)
			{
				return _typicalLaunchVelocity * 0.0076554087f;
			}
			if (!_airLaunched)
			{
				return 0f;
			}
			return 7.655409f;
		}
	}

	public float TypicalTargetVelocity => _typicalTargetVelocity * 0.0076554087f;

	public KinematicsLevel Kinematics
	{
		get
		{
			if (!_useKinematics.HasValue)
			{
				return KinematicsLevel.None;
			}
			if (_useKinematics == true)
			{
				return KinematicsLevel.Full;
			}
			return KinematicsLevel.None;
		}
	}

	public bool IsSharedMaster { get; private set; }

	public static string ServiceMilestonesTooltip => Singleton<LanguageResourceHandler>.Instance.getText("Encyclopedia", "ServiceMilestonesTooltip");

	public bool RequiresDirectLineOfSight
	{
		get
		{
			if (_guidanceType != GuidanceType.SemiActiveRadarHoming && _guidanceType != GuidanceType.LaserHoming && _guidanceType != GuidanceType.TVHoming && _guidanceType != GuidanceType.ActiveSonarHoming && _guidanceType != GuidanceType.ActivePassiveSonarHoming && _guidanceType != GuidanceType.PassiveSonarHoming && (!_midCourseCorrection.AllowsLauncherGuidance || _guidanceType != 0))
			{
				return _midCourseCorrection == MidCourseCorrection.WireGuided;
			}
			return true;
		}
	}

	public GuidanceType PrimaryLaunchGuidanceType
	{
		get
		{
			if (_guidanceType != GuidanceType.ActivePassiveSonarHoming)
			{
				return _guidanceType;
			}
			return GuidanceType.PassiveSonarHoming;
		}
	}

	public bool IsFcrHorizonLimited
	{
		get
		{
			if (_guidanceType != GuidanceType.SemiActiveRadarHoming)
			{
				if (_guidanceType == GuidanceType.None)
				{
					if (_midCourseCorrection != MidCourseCorrection.RadioCommand)
					{
						return _midCourseCorrection == MidCourseCorrection.Datalink;
					}
					return true;
				}
				return false;
			}
			return true;
		}
	}

	public AmmunitionParameters()
	{
	}

	public static AmmunitionParameters GetShared(string ammunitionFileName)
	{
		(string, int) key = (ammunitionFileName ?? "", IniHandler.CacheVersion);
		return _sharedByType.GetOrAdd(key, ((string name, int version) k) => new AmmunitionParameters(k.name, 0, null)
		{
			IsSharedMaster = true
		});
	}

	public static AmmunitionParameters Create(string ammunitionFileName, int magazineAmmoCount, WeaponSystem associatedWeaponSystem)
	{
		AmmunitionParameters shared = GetShared(ammunitionFileName);
		AmmunitionParameters ammunitionParameters = (AmmunitionParameters)shared.MemberwiseClone();
		ammunitionParameters.IsSharedMaster = false;
		ammunitionParameters.SearchModeSetting = new ReactiveProperty<SearchMode>(shared.SearchModeSetting.Value);
		ammunitionParameters.SearchDepthSetting = new ReactiveProperty<SearchDepth>(shared.SearchDepthSetting.Value);
		ammunitionParameters._associatedWeaponSystems = new List<WeaponSystem>();
		ammunitionParameters._magazineAmmoCount = magazineAmmoCount;
		if (associatedWeaponSystem != null)
		{
			ammunitionParameters._associatedWeaponSystems.Add(associatedWeaponSystem);
			ammunitionParameters._associatedSensors = associatedWeaponSystem._vwp._associatedSensors;
		}
		return ammunitionParameters;
	}

	public AmmunitionParameters(string ammunitionFileName, int magazineAmmoCount, WeaponSystem associatedWeaponSystem)
	{
		_magazineAmmoCount = magazineAmmoCount;
		if (associatedWeaponSystem != null)
		{
			if (!_associatedWeaponSystems.Contains(associatedWeaponSystem))
			{
				_associatedWeaponSystems.Add(associatedWeaponSystem);
			}
			_associatedSensors = associatedWeaponSystem._vwp._associatedSensors;
		}
		_ammunitionFileName = ammunitionFileName;
		IniHandler iniHandler = Utils.openIniFile("/ammunition", "/" + ammunitionFileName + ".ini", logErrors: false);
		if (iniHandler == null)
		{
			Log.Warning("Original ammunition file '" + ammunitionFileName + "' not found, opening ammunition dummy file", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Weapons\\AmmunitionParameters.cs", 759, ".ctor");
			iniHandler = Utils.openIniFile("/ammunition", "/dummy.ini");
		}
		bool flag = false;
		string targetType = iniHandler.readValue("General", "TargetType", "").ToLower();
		_targetType = Ammunition.stringToTargetType(targetType);
		string targetType2 = iniHandler.readValue("General", "SecondaryTargetType", "", performLogging: false).ToLower();
		_secondaryTargetType = Ammunition.stringToTargetType(targetType2);
		if (_secondaryTargetType != 0)
		{
			_hasSecondaryTargetType = true;
		}
		_airLaunched = iniHandler.readValue("General", "AirLaunched", defaultValue: false);
		_type = iniHandler.readValue("General", "Type", Ammunition.Type.Unknown);
		if (_type == Ammunition.Type.Unknown)
		{
			Log.Error("Unknown ammunition type in file '" + ammunitionFileName + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Weapons\\AmmunitionParameters.cs", 778, ".ctor");
		}
		else if (_type == Ammunition.Type.Projectile)
		{
			loadProjectileParameters(iniHandler);
			flag = true;
		}
		else
		{
			switch (_type)
			{
			case Ammunition.Type.Bomb:
				flag = true;
				break;
			case Ammunition.Type.AerialRocket:
				flag = true;
				break;
			case Ammunition.Type.Sonobuoy:
				_type = Ammunition.Type.Bomb;
				_subType = Ammunition.Type.Sonobuoy;
				break;
			case Ammunition.Type.AirDepthCharge:
				_type = Ammunition.Type.Bomb;
				_subType = Ammunition.Type.AirDepthCharge;
				break;
			case Ammunition.Type.Fueltank:
				_type = Ammunition.Type.Bomb;
				_subType = Ammunition.Type.Fueltank;
				break;
			case Ammunition.Type.MLRS:
				_type = Ammunition.Type.RBU;
				_subType = Ammunition.Type.MLRS;
				flag = true;
				break;
			case Ammunition.Type.MOSS:
				_type = Ammunition.Type.Torpedo;
				_subType = Ammunition.Type.MOSS;
				break;
			case Ammunition.Type.Paratrooper:
				_type = Ammunition.Type.Bomb;
				_subType = Ammunition.Type.Paratrooper;
				break;
			}
			loadWeaponParameters(iniHandler);
		}
		IniHandler iniHandler2 = LocalizationLoader.OpenVirtualLocalizationFile("language_" + Singleton<LanguageResourceHandler>.Instance.Language, "ammunition_names", "ammunition_names");
		if (iniHandler2 != null)
		{
			_displayedName = iniHandler2.readValue("AmmunitionNames", ammunitionFileName, "MISSING TEXT");
			if (_subType == Ammunition.Type.Fueltank && _displayedName == "MISSING TEXT")
			{
				_displayedName = iniHandler2.readValue("AmmunitionNames", "Fueltank", "MISSING TEXT");
			}
		}
		else
		{
			iniHandler2 = LocalizationLoader.OpenVirtualLocalizationFile("language_en", "ammunition_names", "ammunition_names");
			if (iniHandler2 != null)
			{
				_displayedName = iniHandler2.readValue("AmmunitionNames", ammunitionFileName, "MISSING TEXT");
				if (_subType == Ammunition.Type.Fueltank && _displayedName == "MISSING TEXT")
				{
					_displayedName = iniHandler2.readValue("AmmunitionNames", "Fueltank", "MISSING TEXT");
				}
			}
			else
			{
				Log.Error("Localization file '/language_en/ammunition_names.ini' does not exist!", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Weapons\\AmmunitionParameters.cs", 847, ".ctor");
			}
		}
		if (_displayedName != "MISSING TEXT")
		{
			string[] array = _displayedName.Split(',');
			_displayedName = array[0];
			if (array.Length > 1)
			{
				_displayedNickname = array[1];
			}
			if (array.Length > 2)
			{
				_displayedType = array[2];
			}
		}
		(string, string)? tuple = LocalizationLoader.ReadNatoWeaponName(iniHandler2, ammunitionFileName);
		if (tuple.HasValue)
		{
			_natoDisplayedName = tuple.Value.Item1;
			_natoDisplayedNickname = tuple.Value.Item2;
		}
		if (_guidanceType == GuidanceType.AntiRadiationHoming)
		{
			flag = true;
		}
		else if (_guidanceType == GuidanceType.LaserHoming)
		{
			flag = true;
		}
		else if (_guidanceType == GuidanceType.TVHoming)
		{
			flag = true;
		}
		if (flag)
		{
			_landAttack.Add(Ammunition.LandAttack.Installation);
			_landAttack.Add(Ammunition.LandAttack.Mobile);
		}
		if (iniHandler.doesKeyExist("General", "LandAttackCapability"))
		{
			_landAttack.Clear();
			if (iniHandler.readValue("General", "LandAttackCapability", "") == "All")
			{
				_landAttack.Add(Ammunition.LandAttack.Installation);
				_landAttack.Add(Ammunition.LandAttack.Mobile);
			}
			else
			{
				string[] array2 = iniHandler.readValue("General", "LandAttackCapability", "").Split(',');
				for (int i = 0; i < array2.Length; i++)
				{
					if (Enum.TryParse<Ammunition.LandAttack>(array2[i], ignoreCase: true, out var result) && !_landAttack.Contains(result))
					{
						_landAttack.Add(result);
					}
				}
				if (_landAttack.Count == 1 && _landAttack[0] == Ammunition.LandAttack.ShoreTargetsOnly)
				{
					_landAttack.Add(Ammunition.LandAttack.Installation);
					_landAttack.Add(Ammunition.LandAttack.Mobile);
				}
			}
		}
		if (iniHandler.doesKeyExist("General", "CanNotAttackTypes"))
		{
			string[] array2 = iniHandler.readValue("General", "CanNotAttackTypes", "").Split(',');
			for (int i = 0; i < array2.Length; i++)
			{
				if (Enum.TryParse<ObjectBase.ObjectType>(array2[i], ignoreCase: true, out var result2) && !_canNotAttackTypes.Contains(result2))
				{
					_canNotAttackTypes.Add(result2);
				}
			}
		}
		_massInKg = iniHandler.readValue("General", "Mass", 0f);
		_fuelMassInKg = iniHandler.readValue("General", "Fuel", 0f);
		_frontalAreaM2 = iniHandler.readValue("General", "FrontalArea", 0f, performLogging: false);
		_sideAreaM2 = iniHandler.readValue("General", "SideArea", 0f, performLogging: false);
		_bottomAreaM2 = iniHandler.readValue("General", "BottomArea", 0f, performLogging: false);
		_iocDate = iniHandler.readValue("General", "IOCDate", 0, performLogging: false);
		_focDate = iniHandler.readValue("General", "FOCDate", 0, performLogging: false);
		_antiTorpedo = iniHandler.readValue("General", "Antitorpedo", defaultValue: false);
		_isInterceptable = iniHandler.readValue("General", "IsInterceptable", defaultValue: false);
		if (_type == Ammunition.Type.Missile && _targetType == Ammunition.Target.AAW)
		{
			_requiresWarmUp = true;
		}
		_requiresWarmUp = iniHandler.readValue("General", "RequiresWarmUp", _requiresWarmUp, performLogging: false);
		_saturationValue = iniHandler.readValue("General", "SaturationValue", 1f, performLogging: false);
		_decalClass = iniHandler.readValue("General", "DecalClass", "", performLogging: false);
		_groundDecalClass = iniHandler.readValue("General", "GroundDecalClass", _decalClass, performLogging: false);
		_onHitWaterSpawn = iniHandler.readValue("General", "OnHitWaterSpawn", "", performLogging: false).Split(',');
		_onHitGroundSpawn = iniHandler.readValue("General", "OnHitGroundSpawn", "", performLogging: false).Split(',');
		_spawnLoadout = iniHandler.readValue("General", "SpawnLoadout", "Default", performLogging: false);
		string text = iniHandler.readValue("SensorData", "RCS", "Medium");
		if (!float.TryParse(text, out _maxRCS))
		{
			_maxRCS = Globals.RcsValues.getRCSValue(text.ToLower());
		}
		int index = iniHandler.readValue("WarheadData", "WarheadType", 0);
		_warheadType = getWarheadTypeFromIniIndex(index);
		if (_warheadType == WarheadType.Cluster)
		{
			_bombletsEjectAltitude = iniHandler.readValue("WarheadData", "BombletsEjectAltitude", 300f) * 0.00453571f;
			_subType = Ammunition.Type.Cluster;
		}
		_isInstantExplosion = iniHandler.readValue("WarheadData", "Instant", defaultValue: false);
		switch (iniHandler.readValue("WarheadData", "Penetration", "").ToLower())
		{
		default:
			_penetration = Penetration.Minor;
			break;
		case "moderate":
			_penetration = Penetration.Moderate;
			break;
		case "heavy":
			_penetration = Penetration.Heavy;
			break;
		case "always":
			_penetration = Penetration.Always;
			break;
		}
		_warheadPower = iniHandler.readValue("WarheadData", "Power", 0f);
		if (_type == Ammunition.Type.Missile)
		{
			_warheadDudPower = _warheadPower * 0.5f;
		}
		else
		{
			_warheadDudPower = _warheadPower * 0.2f;
		}
		_warheadDudPower = iniHandler.readValue("WarheadData", "DudPower", _warheadDudPower);
		_ammoPoints = iniHandler.readValue("General", "AmmoPoints", _warheadPower);
		_supplyCategory = iniHandler.readValue("General", "SupplyCategory", ammunitionFileName);
		_isAccountable = iniHandler.doesKeyExist("General", "SupplyCategory");
		_density = iniHandler.readValue("General", "Density", 1, performLogging: false);
		if (_density < 1)
		{
			_density = 1;
		}
		if (float.TryParse(iniHandler.readValue("WarheadData", "ImpactSize", ""), out _impactSizeUnity))
		{
			_impactSizeUnity *= 0.0148809375f;
			_impactSize = ImpactSize.Custom;
		}
		else
		{
			_impactSize = iniHandler.readValue("WarheadData", "ImpactSize", ImpactSize.Custom);
			switch (_impactSize)
			{
			case ImpactSize.Tiny:
				_impactSizeUnity = Globals._damageValues._impactSizeTiny;
				break;
			case ImpactSize.VerySmall:
				_impactSizeUnity = Globals._damageValues._impactSizeVerySmall;
				break;
			case ImpactSize.Small:
				_impactSizeUnity = Globals._damageValues._impactSizeSmall;
				break;
			case ImpactSize.SemiSmall:
				_impactSizeUnity = Globals._damageValues._impactSizeSemiSmall;
				break;
			case ImpactSize.Medium:
				_impactSizeUnity = Globals._damageValues._impactSizeMedium;
				break;
			case ImpactSize.Large:
				_impactSizeUnity = Globals._damageValues._impactSizeLarge;
				break;
			case ImpactSize.VeryLarge:
				_impactSizeUnity = Globals._damageValues._impactSizeVeryLarge;
				break;
			}
		}
		_fuzeProximityDistance = iniHandler.readValue("WarheadData", "FuzeProximityDistance", 0f) * 0.0148809375f;
		_fuzeType = FuzeType.Impact;
		if (_fuzeProximityDistance > 0f)
		{
			_fuzeType = FuzeType.Proximity;
		}
		_fuzeType = iniHandler.readValue("WarheadData", "FuzeMode", _fuzeType);
		_fuzeGateSeconds = iniHandler.readValue("WarheadData", "FuzeGate", (_fuzeType == FuzeType.Timed) ? 0.02f : ((_fuzeType == FuzeType.GatedProximity) ? 1f : (-1f)));
		if (_fuzeType == FuzeType.Proximity || _fuzeType == FuzeType.Impact)
		{
			_fuzeGateSeconds = -1f;
		}
		if (_type == Ammunition.Type.Torpedo)
		{
			_smartFuse = true;
		}
		_killProbability = iniHandler.readValue("WarheadData", "KillProbability", 0.5f);
		IniHandler iniHandler3 = Utils.openIniFile("/ammunition", "/damage.ini");
		if (iniHandler3 != null)
		{
			_outOfAltitudePenalty = iniHandler3.readValue("General", "InterceptOutOfAltitudePenalty", 0.4f);
			_speedPenaltyMultiplier = iniHandler3.readValue("General", "InterceptSpeedPenaltyMultiplier", 1f);
			_sizePenaltyMultiplier = iniHandler3.readValue("General", "InterceptSizePenaltyMultiplier", 1f);
		}
		_outOfAltitudePenalty = iniHandler.readValue("WarheadData", "InterceptOutOfAltitudePenalty", _outOfAltitudePenalty);
		_speedPenaltyMultiplier = iniHandler.readValue("WarheadData", "InterceptSpeedPenaltyMultiplier", _speedPenaltyMultiplier);
		_sizePenaltyMultiplier = iniHandler.readValue("WarheadData", "InterceptSizePenaltyMultiplier", _sizePenaltyMultiplier);
		_boosterEffect = new Effect(iniHandler, "Particles", "BoosterEffect");
		_inFlightEffect = new Effect(iniHandler, "Particles", "InFlightEffect");
		_inFlightEffectStartTime = iniHandler.readValue("Particles", "InFlightEffectStartTime", 0f);
		_sustainerEffect = new Effect(iniHandler, "Particles", "SustainerEffect");
		_sustainerEffectStartTime = iniHandler.readValue("Particles", "SustainerEffectStartTime", 0f);
		_underwaterTrailEffect = new Effect(iniHandler, "Particles", "UnderwaterTrail");
		_parachuteEffect = new Effect(iniHandler, "Particles", "ParachuteEffect");
		_dudExplosionEffect = new Effect(iniHandler, "Particles", "DudExplosion");
		_hitDefaultExplosionEffect = new Effect(iniHandler, "Particles", "HitDefaultExplosion");
		_hitAirExplosionEffect = new Effect(iniHandler, "Particles", "HitAirExplosion");
		_hitShipExplosionEffect = new Effect(iniHandler, "Particles", "HitShipExplosion");
		_hitObjectExplosionEffect = new Effect(iniHandler, "Particles", "HitObjectExplosion");
		if (iniHandler.doesKeyExist("Particles", "HitGroundExplosion") || iniHandler.doesKeyExist("Particles", "HitGroundExplosionClass"))
		{
			_hitGroundExplosionEffect = new Effect(iniHandler, "Particles", "HitGroundExplosion");
		}
		else
		{
			_hitGroundExplosionEffect = new Effect(iniHandler, "Particles", "HitDefaultExplosion");
		}
		_hitWaterSplashEffect = new Effect(iniHandler, "Particles", "HitWaterSplash");
		if (iniHandler.doesKeyExist("Particles", "WaterEntrySplash") || iniHandler.doesKeyExist("Particles", "WaterEntrySplashClass"))
		{
			_waterEntrySplashEffect = new Effect(iniHandler, "Particles", "WaterEntrySplash");
		}
		else
		{
			_waterEntrySplashEffect = _hitWaterSplashEffect;
		}
		if (iniHandler.doesKeyExist("Particles", "ExplosionOverwaterSplash"))
		{
			_torpedoSurfaceSplashEffect = new Effect(iniHandler, "Particles", "ExplosionOverwaterSplash");
		}
		else
		{
			_torpedoSurfaceSplashEffect = new Effect(iniHandler, "Particles", "ExplosionOverwaterSplash");
			_torpedoSurfaceSplashEffect._isIgnored = true;
		}
		_alwaysDisplayWhenSpawned = iniHandler.readValue("General", "AlwaysDisplayWhenSpawned", defaultValue: false);
		_debrisTrailEffect = new Effect(iniHandler, "Particles", "DebrisTrail");
		_debrisSplashEffect = new Effect(iniHandler, "Particles", "DebrisSplash");
		string[] array3 = LocalizationLoader.OpenVirtualLocalizationFile("language_en", "ammunition_names", "ammunition_names").readValue("AmmunitionNames", ammunitionFileName, string.Empty).Split(',');
		_tacviewTag = iniHandler.readValue("Tacview", "Tag", string.Empty);
		_tacviewName = iniHandler.readValue("Tacview", "Name", array3[0]);
	}

	private void loadProjectileParameters(IniHandler ini)
	{
		_muzzleVelocityInMeterPerSecond = ini.readValue("Physics", "MuzzleVelocity", 800f);
		_muzzleVelocityInUnity = _muzzleVelocityInMeterPerSecond * 0.0148809375f;
		_maxLaunchRangeInMiles = ini.readValue("Physics", "MaxRange", 10000f) / 1000f * 0.5399568f;
		_minLaunchRangeInMiles = 0f;
		_launchRangesInUnity = new Vector2(0f, _maxLaunchRangeInMiles * 27.559496f);
		_circularErrorProbable = ini.readValue("Physics", "CircularErrorProbable", 50f) * 0.0148809375f;
		float num = ini.readValue("Physics", "MaxAARange", -1f);
		_maxAARangeInUnity = ((num > 0f) ? (num * 0.0148809375f) : (-1f));
		float num2 = ini.readValue("Physics", "GravityFactor", 1f);
		if (num2 < 0.001f)
		{
			num2 = 1f;
		}
		_gravity = Ballistics.rangeToGravity(_muzzleVelocityInUnity, _launchRangesInUnity.y * num2, 0f, 45f);
		_roundMaxDistance = ini.readValue("Physics", "RoundMaxDistance", 0f) * 0.0148809375f;
		_trailEffect = new Effect(ini, "Particles", "TrailEffect");
		_midCourseCorrection = MidCourseCorrection.FromIndex(ini.readValue("Guidance", "MidCourseCorrection", 0));
		_guidanceType = GetGuidanceTypeFromIniIndex(ini.readValue("Guidance", "GuidanceType", 0));
		_landAttackGuidanceType = _guidanceType;
		_vesselAttackGuidanceType = _guidanceType;
		if (_guidanceType == GuidanceType.LaserHoming)
		{
			_requiresGuidance = true;
		}
		_maxGuidedCorrectionUnity = ini.readValue("Guidance", "MaxGuidedCorrection", 800f) * 0.0148809375f;
	}

	private void loadWeaponParameters(IniHandler ini)
	{
		_samDefenceBonus = ini.readValue("General", "MissileDefenceBonus", 0f);
		_ciwsTargetHealth = ini.readValue("General", "Health", -1f);
		_ciwsTargetArmor = ini.readValue("General", "Armor", 0f);
		_ciwsArmorPenOverride = ini.readValue("WarheadData", "ArmorPen", -1f);
		float defaultValue = ini.readValue("General", "CIWSDefenceBonus", 0f);
		_ciwsMissileEvasiveness = ini.readValue("General", "MissileEvasiveness", defaultValue);
		if (_ciwsMissileEvasiveness < 0f)
		{
			_ciwsMissileEvasiveness = 0f;
		}
		if (_samDefenceBonus > 1f)
		{
			_samDefenceBonus /= 100f;
		}
		if (ini.doesKeyExist("Physics", "ParachuteSlowDown"))
		{
			_parachuteSlowDown = ini.readValue("Physics", "ParachuteSlowDown", 0.985f);
			_parachuteVelocities = ini.readValue("Physics", "ParachuteVelocities", _parachuteVelocities, performAdaptation: false);
			for (int num = _parachuteVelocities.Count - 1; num >= 0; num--)
			{
				if (_parachuteVelocities[num] == Vector2.zero)
				{
					_parachuteVelocities.RemoveAt(num);
				}
				else
				{
					_parachuteVelocities[num] = new Vector2(_parachuteVelocities[num].x * 0.00453571f, _parachuteVelocities[num].y * 0.0076554087f);
				}
			}
			_parachuteEndVelocityInKnots = ini.readValue("Physics", "ParachuteEndVelocity", 100f);
			_parachuteEndVelocity = _parachuteEndVelocityInKnots * 0.0076554087f;
			_parachuteEndVelocitySq = _parachuteEndVelocity * _parachuteEndVelocity;
			_maxAngleWithParachute = ini.readValue("Physics", "MaxAngleWithParachute", 80f);
			_parachuteDeployDelay = ini.readValue("Physics", "ParachuteDeployDelay", 0f);
			_parachuteDeployMaxAltitude = ini.readValue("Physics", "ParachuteDeployMaxAltitude", 15000f);
		}
		_underWaterVelocityDamping = ini.readValue("Physics", "UnderWaterVelocityDamping", 0.985f);
		_trimDepth = ini.readValue("Physics", "TrimDepth", 0f);
		_sonarList = ini.readValue("OnBoardSystems", "Sonar", "");
		_sonarDepth = ini.readValue("OnBoardSystems", "SonarDepth", 0f) * -0.00453571f;
		_radarList = ini.readValue("OnBoardSystems", "Radar", "");
		_visualList = ini.readValue("OnBoardSystems", "Visual", "");
		_periscopeList = ini.readValue("OnBoardSystems", "Periscope", "");
		_ecmList = ini.readValue("OnBoardSystems", "ECM", "");
		_sensorHeight = ini.readValue("OnBoardSystems", "SensorHeight", 0f) * 0.00453571f;
		_transmitterRange = ini.readValue("OnBoardSystems", "TransmitterRange", 0f) * 27.559496f;
		_guidanceType = GetGuidanceTypeFromIniIndex(ini.readValue("Guidance", "GuidanceType", 0));
		_isRetargetable = ini.readValue("General", "Retargetable", defaultValue: true);
		_autoRetarget = ini.readValue("General", "AutoRetarget", defaultValue: false);
		_arSeekerDeviationCoefficient = ini.readValue("Guidance", "ARSeekerDeviationCoefficient", 0.02f);
		_sizeGate = ini.readValue("Guidance", "SizeGate", -1f);
		_rangeGate = ini.readValue("Guidance", "RangeGate", -1f);
		_insDrift = ini.readValue("Guidance", "InsDrift", 0f);
		int num2 = ini.readValue("Guidance", "LandAttackGuidanceType", -1);
		if (num2 < 0)
		{
			_landAttackGuidanceType = _guidanceType;
		}
		else
		{
			_hasLandAttackGuidance = true;
			_landAttackGuidanceType = GetGuidanceTypeFromIniIndex(num2);
		}
		int num3 = ini.readValue("Guidance", "VesselAttackGuidanceType", -1);
		if (num3 < 0)
		{
			_vesselAttackGuidanceType = _guidanceType;
		}
		else
		{
			_hasVesselAttackGuidance = true;
			_vesselAttackGuidanceType = GetGuidanceTypeFromIniIndex(num3);
		}
		_midCourseCorrection = MidCourseCorrection.FromIndex(ini.readValue("Guidance", "MidCourseCorrection", 0));
		if (_midCourseCorrection.AllowsLauncherGuidance)
		{
			switch (_guidanceType)
			{
			case GuidanceType.ActiveRadarHoming:
				_sharedSensorLink = true;
				break;
			case GuidanceType.TVHoming:
				_sharedSensorLink = true;
				break;
			}
		}
		_sharedSensorLink = ini.readValue("Guidance", "SharedSensorLink", _sharedSensorLink);
		if (_guidanceType == GuidanceType.SemiActiveRadarHoming || _midCourseCorrection.AllowsLauncherGuidance || _guidanceType == GuidanceType.LaserHoming)
		{
			_requiresGuidance = true;
		}
		_compatibleGuidanceSystems = ini.readValue("Guidance", "CompatibleGuidanceSystems", "").Split(',').ToList();
		if (_compatibleGuidanceSystems.Count == 1 && _compatibleGuidanceSystems[0] == "")
		{
			_compatibleGuidanceSystems.Clear();
		}
		_forceDirectPoint = ini.readValue("Guidance", "ForceDirectPoint", defaultValue: false);
		_forcePopUp = ini.readValue("Guidance", "ForcePopUp", defaultValue: false);
		if (_guidanceType == GuidanceType.IRHoming && _targetType == Ammunition.Target.AAW)
		{
			_targetRequired = ini.readValue("Guidance", "TargetRequired", defaultValue: true);
		}
		else
		{
			_targetRequired = ini.readValue("Guidance", "TargetRequired", defaultValue: false);
		}
		_dropDuration = ini.readValue("Guidance", "DropDuration", -1f, performLogging: false);
		_initialFlightPhaseDuration = ini.readValue("Guidance", "InitialFlightPhaseDuration", 3f);
		_performsSlightBallisticInitialFlight = ini.readValue("Guidance", "PerformsSlightBallisticInitialFlight", defaultValue: false, performLogging: false);
		_waterEntryLaunch = ini.readValue("Guidance", "WaterEntryLaunch", defaultValue: false, performLogging: false);
		switch (ini.readValue("Guidance", "Frequency", "").ToLower())
		{
		case "vhf-band":
			_frequency = Globals.Frequency.VHF_Band;
			break;
		case "uhf-band":
			_frequency = Globals.Frequency.UHF_Band;
			break;
		case "l-band":
			_frequency = Globals.Frequency.L_Band;
			break;
		case "s-band":
			_frequency = Globals.Frequency.S_Band;
			break;
		case "c-band":
			_frequency = Globals.Frequency.C_Band;
			break;
		case "x-band":
			_frequency = Globals.Frequency.X_Band;
			break;
		case "ku-band":
			_frequency = Globals.Frequency.Ku_Band;
			break;
		case "k-band":
			_frequency = Globals.Frequency.K_Band;
			break;
		case "ka-band":
			_frequency = Globals.Frequency.Ka_Band;
			break;
		default:
			_frequency = Globals.Frequency.X_Band;
			break;
		}
		Globals._waveLengthsForFrequencyBands.TryGetValue(_frequency, out var value);
		_waveLength = value;
		string[] frequencies = ini.readValue("Guidance", "PassiveRadarGuidanceFrequencies", "").ToLower().Split(',');
		loadFrequenciesForPassiveRadarSeeker(frequencies);
		_inertialGuidance = ini.readValue("Guidance", "InertialGuidance", defaultValue: true);
		_hasESM = ini.readValue("Guidance", "HasESM", defaultValue: false);
		_maxVelocityInKnots = ini.readValue("Guidance", "MaxVelocity", 540f);
		if (_type == Ammunition.Type.Bomb || _type == Ammunition.Type.AerialRocket)
		{
			_gravity = ini.readValue("Guidance", "GravityFactor", 9.81f);
		}
		_terrainFollowFlight = ini.readValue("Guidance", "TerrainFollowFlight", defaultValue: false);
		_maxLoftAngle = ini.readValue("Guidance", "MaxLoftAngle", 30f);
		string text = ini.readValue("Guidance", "MaxLoftAlt", "").ToLower();
		float num4 = 0f;
		switch (text)
		{
		case "maintain":
			_loftAltMode = LoftMode.Maintain;
			break;
		case "target":
			_loftAltMode = LoftMode.Target;
			break;
		case "launcher":
			_loftAltMode = LoftMode.Launcher;
			break;
		default:
			_loftAltMode = LoftMode.None;
			num4 = ini.readValue("Guidance", "MaxLoftAlt", 0f);
			break;
		}
		_maxLoftAltUnity = Utils.heightInFeetToUnity(num4);
		_maxLoftVelocityInKnots = ini.readValue("Guidance", "MaxLoftVelocity", _maxVelocityInKnots);
		_loftToSkim = ini.readValue("Guidance", "LoftToSkim", defaultValue: true);
		_terminalLoft = ini.readValue("Guidance", "TerminalLoft", defaultValue: false);
		_optimizeLoftAlt = ini.readValue("Guidance", "OptimizeLoftAlt", defaultValue: true);
		_ballisticLoft = ini.readValue("Guidance", "BallisticLoft", defaultValue: false);
		_currentMinFlightAltUnity = _maxLoftAltUnity;
		_seaSkimmingAltFeet = ini.readValue("Guidance", "SeaSkimmingAlt", num4);
		_seaSkimmingAltUnity = Utils.heightInFeetToUnity(_seaSkimmingAltFeet);
		_seaSkimmingSubAltUnity = ini.readValue("Guidance", "SeaSkimmingSubAlt", _seaSkimmingAltFeet) * 0.00453571f;
		_seaSkimmingMaxDescentAngle = ini.readValue("Guidance", "SeaSkimmingMaxDescentAngle", 30f);
		_seaSkimmingNoseUp = ini.readValue("Guidance", "SeaSkimmingNoseUp", 0f);
		_seaSkimmingStartDistToTargetUnity = ini.readValue("Guidance", "SeaSkimmingStartDistToTarget", 0f) * 27.559496f;
		float num5 = 20f;
		switch (ini.readValue("Guidance", "FinalFlightPhaseAlt", "").ToLower())
		{
		case "maintain":
			_finalFlightPhaseAltMode = LoftMode.Maintain;
			break;
		case "target":
			_finalFlightPhaseAltMode = LoftMode.Target;
			break;
		case "launcher":
			_finalFlightPhaseAltMode = LoftMode.Launcher;
			break;
		default:
			_finalFlightPhaseAltMode = LoftMode.None;
			num5 = ini.readValue("Guidance", "FinalFlightPhaseAlt", _seaSkimmingAltFeet);
			break;
		}
		_finalFlightPhaseAltUnity = Utils.heightInFeetToUnity(num5);
		_finalFlightPhaseMaxAngle = ini.readValue("Guidance", "FinalFlightPhaseMaxAngle", 30f);
		_finalFlightPhaseDistToTarget = ini.readValue("Guidance", "FinalFlightPhaseDistToTarget", 0f);
		_finalFlightPhaseDistToTargetUnity = _finalFlightPhaseDistToTarget * 27.559496f;
		_terminalDelay = ini.readValue("Guidance", "TerminalDelay", -1f);
		_terminalApproachDistMiles = ini.readValue("Guidance", "TerminalApproachDist", float.MaxValue);
		_terminalApproachDist = _terminalApproachDistMiles * 27.559496f;
		float height = -1f;
		switch (ini.readValue("Guidance", "TerminalAlt", ""))
		{
		case "Maintain":
			_terminalAltMode = LoftMode.Maintain;
			break;
		case "Target":
			_terminalAltMode = LoftMode.Target;
			break;
		case "Launcher":
			_terminalAltMode = LoftMode.Launcher;
			break;
		default:
			_terminalAltMode = LoftMode.None;
			height = ini.readValue("Guidance", "TerminalAlt", -1f);
			break;
		}
		_terminalAltUnity = Utils.heightInFeetToUnity(height);
		_terminalDiveDistance = ini.readValue("Guidance", "TerminalDiveDistance", 0.3f) * 27.559496f;
		_terminalPopUpDist = ini.readValue("Guidance", "TerminalPopUpDist", -1f) * 27.559496f;
		_terminalPopUpHeight = ini.readValue("Guidance", "TerminalPopUpHeight", 1000f) * 0.00453571f;
		_terminalPopUpAngle = ini.readValue("Guidance", "TerminalPopUpAngle", 60f);
		_selectivePopUp = ini.readValue("Guidance", "SelectivePopUp", defaultValue: false, performLogging: false);
		_terminalVelocityInKnots = ini.readValue("Guidance", "TerminalVelocity", _maxVelocityInKnots);
		_localTerminalOnly = ini.readValue("Guidance", "LocalTerminalOnly", defaultValue: false, performLogging: false);
		_searchForTargetsTime = ini.readValue("Guidance", "SearchForTargetsTime", 0f, performLogging: false);
		_maxGroupSize = ini.readValue("Guidance", "GroupSize", 0, performLogging: false);
		if (_maxGroupSize > 0)
		{
			_groupJoinRangeUnity = ini.readValue("Guidance", "GroupJoinRange", 20f) * 27.559496f;
			_groupJoinRangeUnitySq = _groupJoinRangeUnity * _groupJoinRangeUnity;
			_groupSpacing = ini.readValue("Guidance", "GroupSpacing", 0.4f) * 27.559496f;
			_groupLeaderAltUnity = ini.readValue("Guidance", "GroupLeaderAlt", num5) * 0.00453571f;
			_groupWingmanAltUnity = ini.readValue("Guidance", "GroupWingmanAlt", num5) * 0.00453571f;
		}
		_ignoreHeightDifferenceForTargetDist = ini.readValue("Guidance", "IgnoreHeightDifferenceForTargetDist", defaultValue: false, performLogging: false);
		_requiresTargetToProceed = ini.readValue("Guidance", "RequiresTargetToProceed", defaultValue: false, performLogging: false);
		_supportsBanking = ini.readValue("Guidance", "SupportsBanking", defaultValue: false);
		_airLaunched = ini.readValue("General", "AirLaunched", defaultValue: false);
		_ejectVelocity = ini.readValue("Guidance", "EjectVelocity", 0f);
		_accelerationTime = ini.readValue("Guidance", "AccelerationTime", 6f);
		_acceleration = ini.readValue("Guidance", "Acceleration", 6f);
		_deceleration = ini.readValue("Guidance", "Deceleration", 3f);
		_maxTurnRateDegrees = ini.readValue("Guidance", "MaxTurnRate", 5f);
		_maxTurnG = ini.readValue("Guidance", "MaxTurnG", 200f, performLogging: false);
		_sustainerBurnTime = ini.readValue("Guidance", "SustainerAccelerationTime", -1f);
		_sustainerBurnAcceleration = ini.readValue("Guidance", "SustainerAcceleration", -1f);
		_dragCoefficient = ini.readValue("Guidance", "DragCoefficient", -1f);
		LiftFactor = ini.readValue("Guidance", "LiftFactor", 0.005f);
		_navigationGain = ini.readValue("Guidance", "NavigationGain", 5f);
		_maxFlightTime = ini.readValue("Guidance", "MaxFlightTime", float.MaxValue);
		_timeLimited = ini.readValue("Guidance", "TimeLimited", defaultValue: false);
		_useKinematics = ini.readValue("Guidance", "ApplyKinematics", (bool?)null, performLogging: true);
		_typicalFiringAlt = ini.readValue("Guidance", "TypicalFiringAlt", -1f);
		_typicalTargetAlt = ini.readValue("Guidance", "TypicalTargetAlt", -1f);
		_typicalLaunchVelocity = ini.readValue("Guidance", "TypicalLaunchVelocity", -1f);
		_typicalTargetVelocity = ini.readValue("Guidance", "TypicalTargetSpeed", 0f);
		if (_maxTurnRateDegrees < 0.1f)
		{
			_maxTurnRateDegrees = 0.1f;
		}
		_toBearingTurnRateDegrees = ini.readValue("Guidance", "LaunchTurnRate", -1f);
		_terminalVerticalTurnRate = ini.readValue("Guidance", "TerminalVerticalTurnRate", _maxTurnRateDegrees);
		_minLaunchRangeInMiles = ini.readValue("Guidance", "MinLaunchRange", 0f);
		_maxLaunchRangeInMiles = ini.readValue("Guidance", "MaxLaunchRange", 40f);
		_launchRangesInUnity = new Vector2(_minLaunchRangeInMiles * 27.559496f, _maxLaunchRangeInMiles * 27.559496f);
		_horizonRangesInUnity = new Vector2(_launchRangesInUnity.y, _launchRangesInUnity.y);
		_defaultHorizonHeight = ini.readValue("Guidance", "DefaultHorizonHeight", 20f) * 0.00453571f;
		_requiresLOS = ini.readValue("Guidance", "RequiresLOS", defaultValue: false);
		if (_type == Ammunition.Type.Missile && (_targetType == Ammunition.Target.AAW || _secondaryTargetType == Ammunition.Target.AAW))
		{
			float maxLaunchRangeInMiles = _maxLaunchRangeInMiles;
			float num6 = _terminalApproachDistMiles;
			if (num6 > maxLaunchRangeInMiles)
			{
				num6 = maxLaunchRangeInMiles;
			}
			_timeToMaxRange += num6 / _terminalVelocityInKnots;
			maxLaunchRangeInMiles -= num6;
			if (maxLaunchRangeInMiles > float.Epsilon)
			{
				num6 = _finalFlightPhaseDistToTarget;
				if (num6 > maxLaunchRangeInMiles)
				{
					num6 = maxLaunchRangeInMiles;
				}
				_timeToMaxRange += num6 / _maxVelocityInKnots;
				maxLaunchRangeInMiles -= num6;
				if (maxLaunchRangeInMiles > float.Epsilon)
				{
					num6 = _seaSkimmingStartDistToTargetUnity * 0.036285132f;
					if (num6 > maxLaunchRangeInMiles)
					{
						num6 = maxLaunchRangeInMiles;
					}
					_timeToMaxRange += num6 / _maxVelocityInKnots;
					maxLaunchRangeInMiles -= num6;
					if (maxLaunchRangeInMiles > float.Epsilon)
					{
						num6 = _maxLaunchRangeInMiles;
						if (num6 > maxLaunchRangeInMiles)
						{
							num6 = maxLaunchRangeInMiles;
						}
						_timeToMaxRange += num6 / _maxLoftVelocityInKnots;
					}
				}
			}
			_timeToMaxRange *= 3600f;
		}
		float num7 = ini.readValue("Guidance", "MinLaunchAltitude", 200f);
		float num8 = ini.readValue("Guidance", "MaxLaunchAltitude", 100000f);
		float num9 = Globals._weaponLaunchAltitudeOffset + 0.00453571f;
		if (num8 < num7 + num9)
		{
			num8 = num7 + num9;
			Log.Error($"{_ammunitionFileName}: MaxLaunchAltitude MUST be at least {num9 * 220.47266f} higher than MinLaunchAltitude! Recalculated value is used.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Weapons\\AmmunitionParameters.cs", 1523, "loadWeaponParameters");
		}
		_launchAltitudesInUnity = new Vector2(num7 * 0.00453571f, num8 * 0.00453571f);
		float num10 = ini.readValue("Guidance", "MinAttackAltitude", -500000f);
		float num11 = ini.readValue("Guidance", "MaxAttackAltitude", 500000f);
		_attackAltitudesInUnity = new Vector2(num10 * 0.00453571f, num11 * 0.00453571f);
		_autoAttackOutsideAltitudes = ini.readValue("Guidance", "AutoAttackOutsideAltitudes", defaultValue: true);
		_maxAttackVelocity = ini.readValue("Guidance", "MaxAttackVelocity", float.MaxValue);
		float defaultValue2 = 100f;
		if (_type == Ammunition.Type.Noisemaker)
		{
			defaultValue2 = 10000f;
		}
		if (_subType == Ammunition.Type.MLRS)
		{
			defaultValue2 = 0f;
		}
		_maxDepthUnity = ini.readValue("Guidance", "MaxDepth", defaultValue2) * 0.00453571f;
		_minDepthUnity = (ini.readValue("Guidance", "MinDepth", 0f) - 1f) * 0.00453571f;
		_sinkRate = ini.readValue("Guidance", "SinkRate", 10f);
		_launchReliability = ini.readValue("Guidance", "LaunchReliability", 1f);
		_seekerReliability = ini.readValue("Guidance", "SeekerReliability", 1f);
		_warheadReliability = ini.readValue("Guidance", "WarheadReliability", 1f);
		_axialRotationDegrees = ini.readValue("Guidance", "AxialRotation", 0f);
		_wobblingStrength = ini.readValue("Guidance", "WobblingStrength", 0f);
		_wobblingSpeed = ini.readValue("Guidance", "WobblingSpeed", 8f);
		_verticalWobblingStrength = ini.readValue("Guidance", "VerticalWobblingStrength", 0f);
		_verticalWobblingSpeed = ini.readValue("Guidance", "VerticalWobblingSpeed", 8f);
		_circularErrorRadius = ini.readValue("Guidance", "CircularErrorRadius", 0f);
		_circularErrorRadiusLarge = ini.readValue("Guidance", "CircularErrorRadiusLarge", _circularErrorRadius) * 0.0148809375f;
		_circularErrorRadiusMobileUnit = ini.readValue("Guidance", "CircularErrorMobileUnit", _circularErrorRadius) * 0.0148809375f;
		_circularErrorRadiusInstallation = ini.readValue("Guidance", "CircularErrorRadiusInstallation", _circularErrorRadius) * 0.0148809375f;
		_circularErrorRadius *= 0.0148809375f;
		_minAltMalusFactor = ini.readValue("Guidance", "MinAltMalusFactor", 3f);
		_isRearAspectOnly = ini.readValue("Guidance", "IsRearAspectOnly", defaultValue: false);
		_targetRearCone = ini.readValue("Guidance", "TargetRearCone", 90f);
		string[] array = ini.readValue("Guidance", "SeekerFOV", "").Split(',');
		_wideSeekerFOV = 60f;
		if (array.Length != 0 && float.TryParse(array[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			_wideSeekerFOV = result;
		}
		_narrowSeekerFOV = _wideSeekerFOV;
		if (array.Length > 1 && float.TryParse(array[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
		{
			_narrowSeekerFOV = result2;
		}
		_seekerFOV = _wideSeekerFOV;
		_seekerGimbalFOV = ini.readValue("Guidance", "SeekerGimbalFOV", 90f);
		_seekerActiveRange = ini.readValue("Guidance", "SeekerActiveRange", 0f) * 27.559496f;
		_seekerPassiveRange = ini.readValue("Guidance", "SeekerPassiveRange", 0f) * 27.559496f;
		if (_guidanceType == GuidanceType.SemiActiveRadarHoming)
		{
			float num12 = float.MaxValue;
			num12 = _terminalApproachDistMiles;
			if (_maxLaunchRangeInMiles < num12)
			{
				num12 = _maxLaunchRangeInMiles;
			}
			_seekerPassiveRange = num12 * 27.559496f;
		}
		_seekerGain = ini.readValue("Guidance", "SeekerGain", 0f);
		_gainFactor = Mathf.Pow(10f, _seekerGain / 10f);
		_radarSeekerPeakPower = ini.readValue("Guidance", "PeakPower", 100f);
		_seekerOutputPower = ini.readValue("Guidance", "SeekerOutputPower", 90f);
		string value2 = ini.readValue("Guidance", "SecondaryPassiveRadarGuidanceType", string.Empty);
		_secondaryPassiveRadarGuidanceType = SecondaryPassiveRadarGuidanceType.None;
		Enum.TryParse<SecondaryPassiveRadarGuidanceType>(value2, ignoreCase: true, out _secondaryPassiveRadarGuidanceType);
		_targetMemory = ini.readValue("Guidance", "TargetMemory", defaultValue: false);
		_useFinalFlightInLandMode = ini.readValue("Guidance", "UseFinalFlightInLandMode", defaultValue: true);
		if (ini.doesKeyExist("Guidance", "DefaultIgnoredTypes"))
		{
			string[] array2 = ini.readValue("Guidance", "DefaultIgnoredTypes", "").Split(',');
			for (int i = 0; i < array2.Length; i++)
			{
				if (Enum.TryParse<ObjectBase.ObjectType>(array2[i], ignoreCase: true, out var result3) && !_defaultIgnoredTypes.Contains(result3))
				{
					_defaultIgnoredTypes.Add(result3);
				}
			}
		}
		_nightVisionLevel = ini.readValue("Guidance", "NightVisionLevel", 0);
		_zoom = ini.readValue("Guidance", "Zoom", 1f);
		_turnDelayStartRange = ini.readValue("Guidance", "TurnDelayStartRange", 0.0215f) * 27.559496f;
		_turnDelay = ini.readValue("Guidance", "TurnDelay", 8f);
		_wakeHomingSeekerSensitivity = ini.readValue("Guidance", "WakeHomingSeekerSensitivity", 0f);
		_wakeHomingTurnSettingReliability = ini.readValue("Guidance", "WakeHomingTurnSettingReliability", 97);
		_antiChaffBonus = ini.readValue("Guidance", "AntiCountermeasuresBonus", 1f);
		_antiJammerBonus = ini.readValue("Guidance", "AntiJammerBonus", 1f);
		_minAfterSpoofEffectTime = ini.readValue("Guidance", "MinAfterSpoofEffectTime", 0f);
		_maxAfterSpoofEffectTime = ini.readValue("Guidance", "MaxAfterSpoofEffectTime", 0f);
		_fractionOfRangeToActivateSeeker = ini.readValue("Guidance", "FractionOfRangeToActivateSeeker", 0.3f);
		_wireControlsDepth = ini.readValue("Guidance", "WireControlsDepth", defaultValue: false);
		_wireAntiCountermeasuresBonus = ini.readValue("Guidance", "WireCountermeasureBonus", 0f);
		_wireBreakSpeedInKnots = ini.readValue("Guidance", "WireBreakSpeed", 20f);
		_maxSpeedOnWireInKnots = ini.readValue("Guidance", "MaxSpeedOnWire", float.MaxValue);
		_wireMinSeafloorClearanceUnity = ini.readValue("Guidance", "WireMinSeafloorClearance", 0f) * 0.00453571f;
		loadTorpedoSpeedSettings(ini);
		_availableSearchModes = ini.readValue("Guidance", "AllowedSearchModes", new List<SearchMode>());
		_defaultSearchMode = ini.readValue("Guidance", "SearchMode", SearchMode.None);
		SearchModeSetting.Value = _defaultSearchMode;
		_snakeSearchAngle = ini.readValue("Guidance", "SnakeSearchAngle", 30f);
		_snakeSearchTurnCycleTime = ini.readValue("Guidance", "SnakeSearchTurnCycleTime", 6f);
		if (_snakeSearchTurnCycleTime < 0.1f)
		{
			_snakeSearchTurnCycleTime = 0.1f;
		}
		_circleDegreesPerSecond = ini.readValue("Guidance", "CircleDegreesPerSecond", 5f);
		_circleMoveFwdAfterCircleInSeconds = ini.readValue("Guidance", "CircleMoveFwdAfterCircleInSeconds", 5f);
		_spiralSearchParams = ini.readValue("Guidance", "SpiralSearchParams", Vector2.zero) * -0.00453571f;
		_spiralSearchRadius = ini.readValue("Guidance", "SpiralSearchRadius", 0.25f);
		if (_spiralSearchRadius < 0.01f)
		{
			_spiralSearchRadius = 0.01f;
		}
		_spiralSearchAngle = ini.readValue("Guidance", "SpiralSearchAngle", 6f);
		_ladderWidth = ini.readValue("Guidance", "LadderWidth", 0.5f) * 27.559496f;
		_ladderTurnDegreesPerSecond = ini.readValue("Guidance", "LadderTurnDegreesPerSecond", 10f);
		_searchVelocityInKnots = ini.readValue("Guidance", "SearchVelocity", 30f);
		_defaultDepth = 0f - ini.readValue("Guidance", "DefaultDepth", 200f);
		if (_defaultDepth >= -3f)
		{
			_defaultDepth = -3f;
		}
		_defaultDepth *= 0.00453571f;
		_reattackMode = ini.readValue("Guidance", "ReattackMode", ReattackMode.None);
		_reattackCircleDegreesPerSecond = ini.readValue("Guidance", "ReattackCircleDegreesPerSecond", 10f);
		_selfDestructDelay = ini.readValue("Guidance", "SelfDestructDelay", 3f);
		if (ini.doesSectionExist("Submunition"))
		{
			_hasSubmunition = true;
			_submunitionFileName = ini.readValue("Submunition", "Ammunition", "");
			IniHandler iniHandler = Utils.openIniFile("/ammunition", "/" + _submunitionFileName + ".ini");
			if (iniHandler == null)
			{
				Log.Error("Original submunition ammunition file '" + _submunitionFileName + "' not found, opening ammunition dummy file", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Weapons\\AmmunitionParameters.cs", 1684, "loadWeaponParameters");
				iniHandler = Utils.openIniFile("/ammunition", "/dummy.ini");
			}
			else
			{
				string text2 = iniHandler.readValue("General", "Type", "").ToLower();
				switch (text2)
				{
				case "projectile":
					_submunitionType = Ammunition.Type.Projectile;
					break;
				case "missile":
					_submunitionType = Ammunition.Type.Missile;
					break;
				case "rbu":
					_submunitionType = Ammunition.Type.RBU;
					break;
				case "asroc":
					_submunitionType = Ammunition.Type.ASROC;
					break;
				case "torpedo":
					_submunitionType = Ammunition.Type.Torpedo;
					break;
				case "noisemaker":
					_submunitionType = Ammunition.Type.Noisemaker;
					break;
				case "chaff":
					_submunitionType = Ammunition.Type.Chaff;
					break;
				case "bomb":
					_submunitionType = Ammunition.Type.Bomb;
					break;
				case "unguidedrocket":
					_submunitionType = Ammunition.Type.AerialRocket;
					break;
				default:
					_submunitionType = Ammunition.Type.Unknown;
					Log.Error("Unknown submunition type in file '" + _submunitionFileName + "': " + text2, Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Weapons\\AmmunitionParameters.cs", 1704, "loadWeaponParameters");
					break;
				}
				float num13 = iniHandler.readValue("Guidance", "SeekerActiveRange", 0f) * 27.559496f;
				float num14 = iniHandler.readValue("Guidance", "SeekerPassiveRange", 0f) * 27.559496f;
				_submunitionSeekerRange = ((num14 > num13) ? num14 : num13);
				string[] array3 = iniHandler.readValue("Guidance", "SeekerFOV", "0").Split(',');
				_submunitionSeekerFOV = 0f;
				if (array3.Length != 0 && float.TryParse(array3[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result4))
				{
					_submunitionSeekerFOV = result4;
				}
				_submunitionSearchMode = iniHandler.readValue("Guidance", "SearchMode", SearchMode.None);
			}
		}
		else
		{
			_hasSubmunition = false;
		}
		_lifeTime = ini.readValue("General", "LifeTime", 120f);
		if (ini.doesSectionExist("NoisemakerData"))
		{
			_sinkRate = ini.readValue("NoisemakerData", "SinkRate", 5f);
			_lifeTime = ini.readValue("NoisemakerData", "LifeTime", 120f);
		}
		if (ini.doesSectionExist("ECM"))
		{
			_decoySpoofChance = ini.readValue("ECM", "SpoofChance", 0.3f);
			_defensiveEffectTime = ini.readValue("ECM", "DefensiveEffectTime", 60);
			_reactionRange = ini.readValue("ECM", "ReactionRange", 2.7f) * 27.559496f;
		}
		int num15 = ini.readValue("Sounds", "SoundMode", 0);
		switch (num15)
		{
		case 1:
			_soundMode = ObjectSoundParameters.SoundMode.Simple;
			break;
		case 2:
			_soundMode = ObjectSoundParameters.SoundMode.NearFar;
			break;
		case 3:
			_soundMode = ObjectSoundParameters.SoundMode.NearFarPlusAspect;
			break;
		default:
			if (ini.doesSectionExist("Sounds"))
			{
				Log.Error($"Unknown Soundmode '{num15}' in ammunition ini file '{_ammunitionFileName}'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Weapons\\AmmunitionParameters.cs", 1752, "loadWeaponParameters");
			}
			_soundMode = ObjectSoundParameters.SoundMode.Simple;
			break;
		}
		if (ini.doesKeyExist("Sounds", "BoosterAudioClip"))
		{
			_boosterAudioClipName = ini.readValue("Sounds", "BoosterAudioClip", "");
			_boosterAudioPosition = ini.readValue("Sounds", "BoosterAudioPosition", Vector3.zero);
			_boosterAudioRollOff = ini.readValue("Sounds", "BoosterAudioRollOff", "").ToLower();
			_boosterAudioDistance = ini.readValue("Sounds", "BoosterAudioDistance", Vector2.zero);
			_boosterAudioPitch = ini.readValue("Sounds", "BoosterAudioPitch", 1f);
			_boosterAudioVolume = ini.readValue("Sounds", "BoosterAudioVolume", 1f);
			_boosterAudioAudibility = ini.readValue("Sounds", "BoosterAudioAudibility", "").ToLower();
		}
		if (ini.doesKeyExist("Sounds", "SustainerAudioClip"))
		{
			_sustainerAudioClipName = ini.readValue("Sounds", "SustainerAudioClip", "");
			_sustainerAudioPosition = ini.readValue("Sounds", "SustainerAudioPosition", Vector3.zero);
			_sustainerAudioRollOff = ini.readValue("Sounds", "SustainerAudioRollOff", "").ToLower();
			_sustainerAudioDistance = ini.readValue("Sounds", "SustainerAudioDistance", Vector2.zero);
			_sustainerAudioPitch = ini.readValue("Sounds", "SustainerAudioPitch", 1f);
			_sustainerAudioVolumeRange = ini.readValue("Sounds", "SustainerAudioVolumeRange", Vector2.zero);
			_sustainerAudioAspectAngle = ini.readValue("Sounds", "SustainerAudioAspectAngle", 0f);
			_sustainerAudioAspectArcHalfRange = ini.readValue("Sounds", "SustainerAudioAspectArcHalfRange", 180f);
			if (_sustainerAudioAspectArcHalfRange < 1f)
			{
				_sustainerAudioAspectArcHalfRange = 1f;
			}
			_sustainerAudioAudibility = ini.readValue("Sounds", "SustainerAudioAudibility", "").ToLower();
		}
		if (ini.doesKeyExist("Sounds", "ExhaustAudioClip"))
		{
			_exhaustAudioClipName = ini.readValue("Sounds", "ExhaustAudioClip", "");
			_exhaustAudioPosition = ini.readValue("Sounds", "ExhaustAudioPosition", Vector3.zero);
			_exhaustAudioRollOff = ini.readValue("Sounds", "ExhaustAudioRollOff", "").ToLower();
			_exhaustAudioDistance = ini.readValue("Sounds", "ExhaustAudioDistance", Vector2.zero);
			_exhaustAudioPitch = ini.readValue("Sounds", "ExhaustAudioPitch", 1f);
			_exhaustAudioVolumeRange = ini.readValue("Sounds", "ExhaustAudioVolumeRange", Vector2.zero);
			_exhaustAudioAspectAngle = ini.readValue("Sounds", "ExhaustAudioAspectAngle", 180f);
			_exhaustAudioAspectArcHalfRange = ini.readValue("Sounds", "ExhaustAudioAspectArcHalfRange", 180f);
			if (_exhaustAudioAspectArcHalfRange < 1f)
			{
				_exhaustAudioAspectArcHalfRange = 1f;
			}
			_exhaustAudioAudibility = ini.readValue("Sounds", "ExhaustAudioAudibility", "").ToLower();
		}
		if (ini.doesKeyExist("Sounds", "FarAudioClip"))
		{
			_farAudioClipName = ini.readValue("Sounds", "FarAudioClip", "");
			_farAudioPosition = ini.readValue("Sounds", "FarAudioPosition", Vector3.zero);
			_farAudioRollOff = ini.readValue("Sounds", "FarAudioRollOff", "").ToLower();
			_farAudioDistance = ini.readValue("Sounds", "FarAudioDistance", Vector2.zero);
			_farAudioPitch = ini.readValue("Sounds", "FarAudioPitch", 1f);
			_farAudioVolume = ini.readValue("Sounds", "FarAudioVolume", 1f);
			_farAudioFadeRange = ini.readValue("Sounds", "FarAudioFadeRange", Vector2.zero);
			_farAudioFadeRange.x *= 27.559496f;
			_farAudioFadeRange.y *= 27.559496f;
			_farAudioAudibility = ini.readValue("Sounds", "FarAudioAudibility", "").ToLower();
		}
		if (ini.doesKeyExist("Sounds", "PropAudioClip"))
		{
			_propAudioClipName = ini.readValue("Sounds", "PropAudioClip", "");
			_propAudioPosition = ini.readValue("Sounds", "PropAudioPosition", Vector3.zero);
			_propAudioRollOff = ini.readValue("Sounds", "PropAudioRollOff", "").ToLower();
			_propAudioDistance = ini.readValue("Sounds", "PropAudioDistance", Vector2.zero);
			_propAudioPitch = ini.readValue("Sounds", "PropAudioPitch", 1f);
			_propAudioVolume = ini.readValue("Sounds", "PropAudioVolume", 1f);
			_propAudioFadeRange = ini.readValue("Sounds", "PropAudioFadeRange", Vector2.zero);
			_propAudioFadeRange.x *= 27.559496f;
			_propAudioFadeRange.y *= 27.559496f;
			_propAudioAudibility = ini.readValue("Sounds", "PropAudioAudibility", "").ToLower();
		}
		if (ini.doesKeyExist("Sounds", "SonarAudioClip"))
		{
			_sonarAudioClipName = ini.readValue("Sounds", "SonarAudioClip", "");
			_sonarAudioPosition = ini.readValue("Sounds", "SonarAudioPosition", Vector3.zero);
			_sonarAudioRollOff = ini.readValue("Sounds", "SonarAudioRollOff", "").ToLower();
			_sonarAudioDistance = ini.readValue("Sounds", "SonarAudioDistance", Vector2.zero);
			_sonarAudioPitch = ini.readValue("Sounds", "SonarAudioPitch", 0f);
			_sonarAudioVolume = ini.readValue("Sounds", "SonarAudioVolume", 1f);
			_sonarAudioAudibility = ini.readValue("Sounds", "SonarAudioAudibility", "").ToLower();
		}
		if (ini.doesKeyExist("Sounds", "NoisemakerAudioClip"))
		{
			_noisemakerAudioVolume = ini.readValue("Sounds", "NoisemakerAudioVolume", 1f);
			_noisemakerAudioAudibility = ini.readValue("Sounds", "NoisemakerAudioAudibility", "").ToLower();
		}
		int num16 = ini.readValue("Launchers", "NumberOfLaunchers", 0, performLogging: false);
		for (int j = 1; j <= num16; j++)
		{
			string key = "Launcher" + j;
			string text3 = ini.readValue("Launchers", key, "");
			AmmunitionLauncherPosition value3 = default(AmmunitionLauncherPosition);
			value3._loadedPosition = ini.readValue(text3, "LoadedPosition", Vector3.zero);
			value3._unloadedPosition = ini.readValue(text3, "UnloadedPosition", Vector3.zero);
			value3._loadedRotation = ini.readValue(text3, "LoadedRotation", Vector3.zero);
			value3._unloadedRotation = ini.readValue(text3, "UnloadedRotation", Vector3.zero);
			value3._onRailWarmup = ini.readValue(text3, "OnRailWarmup", 0f);
			_launcherPositions.Add(text3, value3);
		}
	}

	private void loadFrequenciesForPassiveRadarSeeker(string[] frequencies)
	{
		if (frequencies[0].ToLower() == "all")
		{
			Globals.Frequency[] array = (Globals.Frequency[])Enum.GetValues(typeof(Globals.Frequency));
			foreach (Globals.Frequency frequency in array)
			{
				if (frequency != 0)
				{
					_passiveRadarHomingFrequencies.Add(frequency);
				}
			}
			return;
		}
		for (int i = 0; i < frequencies.Length; i++)
		{
			switch (frequencies[i])
			{
			case "vhf-band":
				_passiveRadarHomingFrequencies.Add(Globals.Frequency.VHF_Band);
				break;
			case "uhf-band":
				_passiveRadarHomingFrequencies.Add(Globals.Frequency.UHF_Band);
				break;
			case "l-band":
				_passiveRadarHomingFrequencies.Add(Globals.Frequency.L_Band);
				break;
			case "s-band":
				_passiveRadarHomingFrequencies.Add(Globals.Frequency.S_Band);
				break;
			case "c-band":
				_passiveRadarHomingFrequencies.Add(Globals.Frequency.C_Band);
				break;
			case "x-band":
				_passiveRadarHomingFrequencies.Add(Globals.Frequency.X_Band);
				break;
			case "ku-band":
				_passiveRadarHomingFrequencies.Add(Globals.Frequency.Ku_Band);
				break;
			case "k-band":
				_passiveRadarHomingFrequencies.Add(Globals.Frequency.K_Band);
				break;
			case "ka-band":
				_passiveRadarHomingFrequencies.Add(Globals.Frequency.Ka_Band);
				break;
			}
		}
		_passiveRadarHomingFrequencies = Utils.OnlyUniqueElementsOfList(_passiveRadarHomingFrequencies);
	}

	private void loadTorpedoSpeedSettings(IniHandler ini)
	{
		_torpedoSpeedSettings.Clear();
		string text = ini.readValue("Guidance", "SpeedSettings", "");
		string text2 = ini.readValue("Guidance", "SpeedSettingRanges", "");
		if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2))
		{
			return;
		}
		string[] array = text.Split(',');
		string[] array2 = text2.Split(',');
		if (array.Length != array2.Length)
		{
			Log.Error("SpeedSettings and SpeedSettingRanges have different lengths in ammunition ini file '" + _ammunitionFileName + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Weapons\\AmmunitionParameters.cs", 1906, "loadTorpedoSpeedSettings");
			return;
		}
		for (int i = 0; i < array.Length; i++)
		{
			if (!float.TryParse(array[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !float.TryParse(array2[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
			{
				Log.Error($"Could not parse SpeedSettings/SpeedSettingRanges entry {i + 1} in ammunition ini file '{_ammunitionFileName}'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Weapons\\AmmunitionParameters.cs", 1915, "loadTorpedoSpeedSettings");
				_torpedoSpeedSettings.Clear();
				return;
			}
			_torpedoSpeedSettings.Add(new Vector2(result, result2 * 27.559496f));
		}
		_torpedoSpeedSettings.Sort((Vector2 a, Vector2 b) => a.x.CompareTo(b.x));
		float x = _torpedoSpeedSettings[_torpedoSpeedSettings.Count - 1].x;
		float num = 0f;
		foreach (Vector2 torpedoSpeedSetting in _torpedoSpeedSettings)
		{
			if (torpedoSpeedSetting.y > num)
			{
				num = torpedoSpeedSetting.y;
			}
		}
		if (ini.doesKeyExist("Guidance", "MaxVelocity"))
		{
			if (x > _maxVelocityInKnots)
			{
				_maxVelocityInKnots = x;
			}
		}
		else
		{
			_maxVelocityInKnots = x;
			if (!ini.doesKeyExist("Guidance", "TerminalVelocity"))
			{
				_terminalVelocityInKnots = x;
			}
		}
		if (!ini.doesKeyExist("Guidance", "MaxLaunchRange"))
		{
			_maxLaunchRangeInMiles = num * 0.036285132f;
			_launchRangesInUnity = new Vector2(_minLaunchRangeInMiles * 27.559496f, num);
			_horizonRangesInUnity = new Vector2(_launchRangesInUnity.y, _launchRangesInUnity.y);
		}
	}

	public float TorpedoRangeAtSpeed(float speedInKnots)
	{
		if (_torpedoSpeedSettings.Count == 0)
		{
			return _launchRangesInUnity.y;
		}
		if (speedInKnots <= _torpedoSpeedSettings[0].x)
		{
			return _torpedoSpeedSettings[0].y;
		}
		for (int i = 1; i < _torpedoSpeedSettings.Count; i++)
		{
			if (speedInKnots <= _torpedoSpeedSettings[i].x)
			{
				float t = (speedInKnots - _torpedoSpeedSettings[i - 1].x) / (_torpedoSpeedSettings[i].x - _torpedoSpeedSettings[i - 1].x);
				return Mathf.Lerp(_torpedoSpeedSettings[i - 1].y, _torpedoSpeedSettings[i].y, t);
			}
		}
		return _torpedoSpeedSettings[_torpedoSpeedSettings.Count - 1].y;
	}

	public float EffectiveMaxLaunchRange(ObjectBase launcher)
	{
		if (_type == Ammunition.Type.Torpedo && _torpedoSpeedSettings.Count > 0 && launcher != null && launcher._launchSpeedSettings.TryGetValue(_ammunitionFileName, out var value) && value.Value >= 0 && value.Value < _torpedoSpeedSettings.Count)
		{
			return _torpedoSpeedSettings[value.Value].y;
		}
		return _launchRangesInUnity.y;
	}

	public Effect getEffectToPlay(ObjectBase obj)
	{
		if (obj == null)
		{
			return _hitDefaultExplosionEffect;
		}
		Effect effect = null;
		switch (obj._type)
		{
		case ObjectBase.ObjectType.Vessel:
		case ObjectBase.ObjectType.Submarine:
			return _hitShipExplosionEffect;
		case ObjectBase.ObjectType.Aircraft:
		case ObjectBase.ObjectType.Helicopter:
		case ObjectBase.ObjectType.Missile:
		case ObjectBase.ObjectType.Torpedo:
		case ObjectBase.ObjectType.ASROC:
			return _hitAirExplosionEffect;
		case ObjectBase.ObjectType.LandUnit:
			if (_hitGroundExplosionEffect._effectClassName != string.Empty || _hitGroundExplosionEffect._singleEffect != null)
			{
				return _hitGroundExplosionEffect;
			}
			return _hitDefaultExplosionEffect;
		default:
			return _hitDefaultExplosionEffect;
		}
	}

	public static string FormatServiceMilestones(int iocDate, int focDate)
	{
		if (iocDate <= 0 && focDate <= 0)
		{
			return string.Empty;
		}
		List<string> list = new List<string>();
		if (iocDate > 0)
		{
			list.Add(string.Format("{0}: {1}", Singleton<LanguageResourceHandler>.Instance.getText("Encyclopedia", "IOC"), iocDate));
		}
		if (focDate > 0)
		{
			list.Add(string.Format("{0}: {1}", Singleton<LanguageResourceHandler>.Instance.getText("Encyclopedia", "FOC"), focDate));
		}
		return "(" + string.Join(", ", list) + ")";
	}

	public static WarheadType getWarheadTypeFromIniIndex(int index)
	{
		return index switch
		{
			-1 => WarheadType.None, 
			0 => WarheadType.HighExplosive, 
			1 => WarheadType.ArmorPiercing, 
			2 => WarheadType.HEAT, 
			3 => WarheadType.Illumination, 
			4 => WarheadType.Cluster, 
			5 => WarheadType.RunwayCratering, 
			6 => WarheadType.Fragmentation, 
			_ => WarheadType.Unknown, 
		};
	}

	public static GuidanceType GetGuidanceTypeFromIniIndex(int index)
	{
		return index switch
		{
			0 => GuidanceType.None, 
			1 => GuidanceType.IRHoming, 
			2 => GuidanceType.SemiActiveRadarHoming, 
			3 => GuidanceType.ActiveRadarHoming, 
			4 => GuidanceType.AntiRadiationHoming, 
			5 => GuidanceType.LaserHoming, 
			6 => GuidanceType.TVHoming, 
			7 => GuidanceType.ActiveSonarHoming, 
			8 => GuidanceType.PassiveSonarHoming, 
			9 => GuidanceType.WakeHoming, 
			10 => GuidanceType.ActivePassiveSonarHoming, 
			_ => GuidanceType.None, 
		};
	}

	public float GetDragFactor(bool isAirLaunched)
	{
		if (Kinematics == KinematicsLevel.None)
		{
			return -1f;
		}
		if (_dragCoefficient > -0.1f)
		{
			return _dragCoefficient;
		}
		return KinematicsCache.Instance.CalcDragFactor(this, isAirLaunched);
	}

	public bool AssumeEvasiveTarget(ObjectBase target)
	{
		if (!(target != null))
		{
			return _targetType == Ammunition.Target.AAW;
		}
		return target.IsAirUnit;
	}

	public float MaxRangeRough(ObjectBase launcher, float targetAltitude)
	{
		return MaxRangeRough(launcher, new Vector3(_launchRangesInUnity.y, targetAltitude, 0f), new Vector3(0f, 0f, 0f));
	}

	public float MaxRangeRough(ObjectBase launcher, Vector3 targetPos, Vector3 targetVelocity, bool targetEvasiveManuver = true)
	{
		if (_type == Ammunition.Type.Torpedo)
		{
			return EffectiveMaxLaunchRange(launcher);
		}
		if (Kinematics == KinematicsLevel.None)
		{
			return _launchRangesInUnity.y;
		}
		float launchVelocity = 0f;
		if (launcher.IsAirUnit)
		{
			launchVelocity = launcher._velocityInKnots;
		}
		return KinematicsCache.Instance.CalcMaxRange(launcher, launchVelocity, this, targetVelocity, targetPos, targetEvasiveManuver);
	}

	public float MaxRangeRough(ObjectBase launcher)
	{
		return MaxRangeRough(launcher, new Vector3(_launchRangesInUnity.y, TypicalTargetAlt, 0f), new Vector3(0f - TypicalTargetVelocity, 0f, 0f), AssumeEvasiveTarget(null));
	}

	public MissileSimulator.KinematicRangeResult MaxRangePrecise(ObjectBase launcher, Vector3 targetPos, Vector3 targetVelocity, int iterations = 0, bool targetEvasiveManuver = true)
	{
		if (Kinematics == KinematicsLevel.None || _type != Ammunition.Type.Missile)
		{
			MissileSimulator.KinematicRangeResult kinematicRangeResult = MaxRangePrecise(launcher._geoPosition.ToUnity(), targetPos, targetVelocity, launcher._velocityInKnots, launcher.IsAirUnit, 0, launcher, targetEvasiveManuver);
			return new MissileSimulator.KinematicRangeResult
			{
				MaxRange = EffectiveMaxLaunchRange(launcher),
				InterceptTime = kinematicRangeResult.InterceptTime
			};
		}
		return MaxRangePrecise(launcher._geoPosition.ToUnity(), targetPos, targetVelocity, launcher._velocityInKnots, launcher.IsAirUnit, iterations, launcher, targetEvasiveManuver);
	}

	public MissileSimulator.KinematicRangeResult MaxRangePrecise(Vector3 launchPos, Vector3 targetPos, Vector3 targetVelocity, float launchVelocityInKnots, bool isAirUnit, int iterations = 0, ObjectBase launcher = null, bool targetEvasiveManuver = true)
	{
		if (_type != Ammunition.Type.Missile)
		{
			return new MissileSimulator.KinematicRangeResult
			{
				MaxRange = _launchRangesInUnity.y
			};
		}
		if (Kinematics == KinematicsLevel.None)
		{
			iterations = 0;
		}
		float startVelocityKnots = 0f;
		if (isAirUnit)
		{
			startVelocityKnots = launchVelocityInKnots;
		}
		Vector3 vector = new Vector3(targetPos.x - launchPos.x, 0f, targetPos.z - launchPos.z);
		float magnitude = vector.magnitude;
		MissileSimulator.KinematicRangeResult kinematicRangeResult = MissileSimulator.EstimateShot(this, launchPos, startVelocityKnots, targetVelocity, targetPos, isAirUnit, -1f, -1f, -1f, targetEvasiveManuver);
		float interceptTime = kinematicRangeResult.InterceptTime;
		if (iterations <= 0 || magnitude < 0.01f)
		{
			if (Kinematics == KinematicsLevel.None)
			{
				return new MissileSimulator.KinematicRangeResult
				{
					MaxRange = _launchRangesInUnity.y,
					InterceptTime = kinematicRangeResult.InterceptTime
				};
			}
			return kinematicRangeResult;
		}
		Vector3 normalized = vector.normalized;
		float y = targetPos.y;
		float num = 0f;
		MissileSimulator.KinematicRangeResult kinematicRangeResult2 = null;
		float num2 = float.MaxValue;
		if (kinematicRangeResult.InterceptSpeed > 0f || kinematicRangeResult.TargetTooClose)
		{
			num = magnitude;
		}
		else
		{
			num2 = magnitude;
		}
		for (int i = 0; i < iterations; i++)
		{
			float num3 = ((kinematicRangeResult != null && kinematicRangeResult.TargetTooClose && kinematicRangeResult.InterceptSpeed < 0f) ? (kinematicRangeResult.MaxRange * 2f) : kinematicRangeResult.MaxRange);
			if (num3 == 0f)
			{
				num3 = num2 / 2f;
			}
			if (num > 0f && num2 < float.MaxValue)
			{
				num3 = ((num2 != float.MaxValue) ? ((num + num2) / 2f) : (num * 2f));
				if (num3 == 0f)
				{
					num3 = num2 / 2f;
				}
			}
			Vector3 targetPosition = launchPos + normalized * num3;
			targetPosition.y = y;
			kinematicRangeResult = MissileSimulator.EstimateShot(this, launchPos, startVelocityKnots, targetVelocity, targetPosition, isAirUnit, -1f, -1f, -1f, targetEvasiveManuver);
			if ((kinematicRangeResult.InterceptSpeed > 0f || kinematicRangeResult.TargetTooClose) && num < num3)
			{
				num = num3;
				kinematicRangeResult2 = kinematicRangeResult;
			}
			else if (kinematicRangeResult.InterceptSpeed < 0f && num2 > num3)
			{
				num2 = num3;
			}
			if ((kinematicRangeResult.InterceptSpeed > 0f && kinematicRangeResult.DistPercent < 1.005f) || MathF.Abs(num - num2) < 0.5f)
			{
				break;
			}
		}
		if (interceptTime > 0f && kinematicRangeResult2 != null)
		{
			MissileSimulator.KinematicRangeResult kinematicRangeResult3 = kinematicRangeResult2;
			kinematicRangeResult3.InterceptTime = interceptTime;
			kinematicRangeResult2 = kinematicRangeResult3;
		}
		return kinematicRangeResult2 ?? kinematicRangeResult;
	}

	public MissileSimulator.KinematicRangeResult MaxRangePrecise(ObjectBase launcher)
	{
		return MaxRangePrecise(launcher, new Vector3(launcher.transform.position.x + _launchRangesInUnity.y, TypicalTargetAlt, launcher.transform.position.z), new Vector3(0f - TypicalTargetVelocity, 0f, 0f), 10, AssumeEvasiveTarget(null));
	}

	public float NoEscapeRangePrecise(ObjectBase launcher, ObjectBase targetObject, float maxSearchRange, int iterations = 8)
	{
		if (_type != Ammunition.Type.Missile || launcher == null || targetObject == null || !targetObject.IsAirUnit)
		{
			return -1f;
		}
		if (maxSearchRange <= 0.01f)
		{
			return 0f;
		}
		MissileSimulator.FleePerformance fleePerf = MissileSimulator.GetFleePerformance(targetObject);
		Vector3 launchPos = launcher._geoPosition.ToUnity();
		Vector3 targetPos = targetObject.transform.position;
		Vector3 dir = new Vector3(targetPos.x - launchPos.x, 0f, targetPos.z - launchPos.z);
		if (dir.sqrMagnitude < 1E-06f)
		{
			dir = Vector3.forward;
		}
		else
		{
			dir.Normalize();
		}
		Vector3 targetVel = targetObject._velocityVecInUnity;
		float launchVelocity = (launcher.IsAirUnit ? launcher._velocityInKnots : 0f);
		if (Catches(maxSearchRange))
		{
			return maxSearchRange;
		}
		float num = Mathf.Min(maxSearchRange * 0.05f, 2f);
		if (!Catches(num))
		{
			return 0f;
		}
		float num2 = maxSearchRange;
		for (int i = 0; i < iterations; i++)
		{
			float num3 = (num + num2) * 0.5f;
			if (Catches(num3))
			{
				num = num3;
			}
			else
			{
				num2 = num3;
			}
		}
		return num;
		bool Catches(float dist)
		{
			Vector3 targetPosition = launchPos + dir * dist;
			targetPosition.y = targetPos.y;
			MissileSimulator.KinematicRangeResult kinematicRangeResult = MissileSimulator.EstimateShotNoEscape(this, launchPos, launchVelocity, targetPosition, targetVel, fleePerf.MaxSpeedKnots, fleePerf.MaxGs, fleePerf.VelocityGain, fleePerf.EscapeAltUnity, fleePerf.DiveAngleDeg, launcher.IsAirUnit);
			if (kinematicRangeResult.InterceptTime >= 0f)
			{
				return kinematicRangeResult.InterceptSpeed > 0f;
			}
			return false;
		}
	}

	public GuidanceType GuidanceTypeForTarget(ObjectBase targetObject)
	{
		if (targetObject == null)
		{
			return _guidanceType;
		}
		return GuidanceTypeForTargetType(targetObject._type);
	}

	public GuidanceType GuidanceTypeForTargetType(ObjectBase.ObjectType targetType)
	{
		return targetType switch
		{
			ObjectBase.ObjectType.LandUnit => _landAttackGuidanceType, 
			ObjectBase.ObjectType.Vessel => _vesselAttackGuidanceType, 
			_ => _guidanceType, 
		};
	}

	public GuidanceType GuidanceTypeForLaunchSelection(LaunchSeekerMode selection, ObjectBase.ObjectType targetType)
	{
		return selection switch
		{
			LaunchSeekerMode.Primary => PrimaryLaunchGuidanceType, 
			LaunchSeekerMode.Secondary => _vesselAttackGuidanceType, 
			_ => GuidanceTypeForTargetType(targetType), 
		};
	}

	public bool IsTorpedoDeliveryShot(ObjectBase targetObject)
	{
		if (!_hasSubmunition || _submunitionType != Ammunition.Type.Torpedo)
		{
			return false;
		}
		if (!(targetObject == null) && targetObject._type != ObjectBase.ObjectType.Submarine)
		{
			return targetObject.isSubmerged();
		}
		return true;
	}

	public float FcrTrackedAltitude(ObjectBase targetObject)
	{
		if (IsTorpedoDeliveryShot(targetObject) && _seaSkimmingSubAltUnity > 0f)
		{
			return _seaSkimmingSubAltUnity;
		}
		if (!(targetObject != null))
		{
			return 0f;
		}
		return targetObject.VisibleAltitude;
	}

	public bool RequiresFcrControl(ObjectBase targetObject)
	{
		if (!_midCourseCorrection.IsRadioCommand)
		{
			return false;
		}
		if (GuidanceTypeForTarget(targetObject) != 0)
		{
			return false;
		}
		if (!(targetObject == null) && targetObject is LandUnit landUnit)
		{
			return landUnit._isMobile;
		}
		return true;
	}

	public bool CanFinishOnOwnSeeker(GuidanceType seekerGuidanceType)
	{
		if (_midCourseCorrection.IsRadioCommand)
		{
			if (seekerGuidanceType != GuidanceType.IRHoming)
			{
				return seekerGuidanceType == GuidanceType.ActiveRadarHoming;
			}
			return true;
		}
		return false;
	}

	public float getDecalScaleForImpactSize()
	{
		float result = 1f;
		switch (_impactSize)
		{
		case ImpactSize.Tiny:
			result = Globals._damageValues._tinyDecalScale;
			break;
		case ImpactSize.VerySmall:
			result = Globals._damageValues._verySmallDecalScale;
			break;
		case ImpactSize.Small:
			result = Globals._damageValues._smallDecalScale;
			break;
		case ImpactSize.Medium:
			result = Globals._damageValues._mediumDecalScale;
			break;
		case ImpactSize.Large:
			result = Globals._damageValues._largeDecalScale;
			break;
		case ImpactSize.VeryLarge:
			result = Globals._damageValues._veryLargeDecalScale;
			break;
		}
		return result;
	}

	internal static MidCourseCorrection GetMidCourseCorrectionFromIndex(int index)
	{
		return MidCourseCorrection.FromIndex(index);
	}

	public float CalculateHitChance(ObjectBase shooter, ObjectBase target)
	{
		if (target == null)
		{
			return 0f;
		}
		Dictionary<string, float> dictionary = new Dictionary<string, float>();
		if (target.isAirTarget() && _type == Ammunition.Type.Missile)
		{
			dictionary.Add("Intercept chance", Utils.GetInterceptionChance(target, this, shooter?.IsPlayerObject ?? false).hitChance);
		}
		if (target._obp?._sensorSystems != null)
		{
			float num = 0f;
			Globals.Frequency weaponJammingFrequency = GetWeaponJammingFrequency(shooter, target);
			if (weaponJammingFrequency != 0)
			{
				foreach (SensorSystem sensorSystem in target._obp._sensorSystems)
				{
					if (sensorSystem is SensorSystemECM sensorSystemECM && !sensorSystem.IsDestroyed && (sensorSystemECM._ecm._ep._type == ECMParameters.ECMType.Defensive || sensorSystemECM._ecm._ep._type == ECMParameters.ECMType.Universal) && weaponJammingFrequency != 0 && sensorSystemECM._ecm._ep._frequencies.Contains(weaponJammingFrequency))
					{
						float jamChance = sensorSystemECM._ecm._ep._jamChance;
						float num2 = 1f;
						if ((object)shooter != null && shooter.IsPlayerObject)
						{
							num2 -= Globals._ecmJamChanceReduction;
						}
						else if (target.IsPlayerObject)
						{
							num2 += Globals._ecmJamChanceBonus;
						}
						jamChance *= num2;
						jamChance = Mathf.Clamp01(jamChance - _antiJammerBonus);
						if (jamChance > num)
						{
							num = jamChance;
						}
					}
				}
				dictionary.Add("Jam survival chance", 1f - num);
			}
		}
		if (target.AmmunitionNameToAmmunitionDictionary != null && _type == Ammunition.Type.Missile)
		{
			foreach (Ammunition value in target.AmmunitionNameToAmmunitionDictionary.Values)
			{
				if (value?._ap != null && value._ap._type == Ammunition.Type.Chaff)
				{
					dictionary.Add("Spoof survival chance", 1f - Mathf.Clamp01(value._ap._decoySpoofChance - _antiChaffBonus));
					break;
				}
			}
		}
		return Mathf.Clamp01(dictionary.Values.Aggregate(1f, (float x, float y) => x * y));
	}

	public float CalculateExpectedDeviation(ObjectBase shooter, Vector3 targetPos, ObjectBase target = null)
	{
		if (shooter is Aircraft aircraft)
		{
			float aimErrorSigma = Bomb.GetAimErrorSigma(aircraft, targetPos, target);
			float ballisticScatterSigma = Bomb.GetBallisticScatterSigma(this);
			float num = MathF.Max(0.907142f, (float)shooter._geoPosition._height - Mathf.Max(targetPos.y, 0f));
			float num2 = Utils.BombDistanceToDrop(shooter.UnityVelocityVector, shooter.transform.position.y, Mathf.Max(targetPos.y, 0f), _gravity);
			float num3 = MathF.Sqrt(num * num + num2 * num2) / 81.64278f;
			float num4 = (aircraft.Ap._bombAimErrorFixedWithAltitude ? MathF.Min(aimErrorSigma, 1.7315173f * num3) : (MathF.Min(aimErrorSigma, 16.328556f) * num3));
			return 1.1774f * MathF.Sqrt(num4 * num4 + ballisticScatterSigma * ballisticScatterSigma * num3 * num3) * 220.47266f;
		}
		return 0f;
	}

	private Globals.Frequency GetWeaponJammingFrequency(ObjectBase shooter, ObjectBase target)
	{
		GuidanceType guidanceType = GuidanceTypeForTarget(target);
		Globals.Frequency result = Globals.Frequency.Undefined;
		if (guidanceType == GuidanceType.ActiveRadarHoming)
		{
			result = _frequency;
		}
		if (shooter != null && (guidanceType == GuidanceType.SemiActiveRadarHoming || (guidanceType == GuidanceType.None && _midCourseCorrection.AllowsLauncherGuidance)))
		{
			SensorSystem sensorSystem = shooter.GetWeaponSystemsForAmmunition(_ammunitionFileName)?.SelectMany((WeaponSystem x) => x._vwp._associatedSensors.Where((SensorSystem w) => w is SensorSystemRadar)).FirstOrDefault();
			if (sensorSystem != null && sensorSystem is SensorSystemRadar sensorSystemRadar)
			{
				result = sensorSystemRadar._radar._rp._frequency;
			}
		}
		return result;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
