using Unity.Profiling;
using UnityEngine;

namespace SeaPower;

public class SensorSystemRadar : SensorSystem
{
	public enum RadarType
	{
		Undefined,
		Search,
		DirectedSearch,
		Targeting
	}

	public enum Mode
	{
		Undefined,
		RadioCommand,
		Illuminate
	}

	private static readonly ProfilerMarker _profSystem = new ProfilerMarker("System: SensorSystemRadar");

	public RadarType _type;

	public Mode _mode;

	private int _rotationDirection;

	private float _rpm;

	private float _currentRPM;

	private bool _targetRPMReached;

	public bool _lookDownMode = true;

	public float _lookDownMultiplier;

	public float _lookDownRange;

	public Radar _radar;

	public override ISensor Sensor => _radar;

	public SensorSystemRadar()
	{
	}

	public SensorSystemRadar(ObjectBase baseObject, string systemName, GameObject mountObject)
	{
		_baseObject = baseObject;
		_systemName = systemName;
		_mountObject = mountObject;
		_sectionName = "None";
		if (!string.IsNullOrEmpty(_systemName))
		{
			IniHandler iniHandler = Utils.openVirtualIniFile("language_" + Singleton<LanguageResourceHandler>.Instance.Language, "systemgroups", "systemgroups");
			base.DisplayName.Value = _systemName;
			if (iniHandler != null)
			{
				base.DisplayName.Value = iniHandler.readValue("SystemNames", _systemName, _systemName).Split("|")[0];
			}
			else
			{
				Log.Error("Could not create virtual Ini for systemgroups.ini!", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Sensors\\SensorSystemRadar.cs", 52, ".ctor");
			}
		}
		LoadSensorSystemRadar();
	}

	public override void LoadFromInI(IniHandler objectIni, string sectionName, ObjectBaseParameters obp, string keyName = "")
	{
		base.LoadFromInI(objectIni, sectionName, obp, "");
		obp._objectsDictionary.TryGetValue(objectIni.readValue(sectionName, "Radar", "", performLogging: false), out _sensorObject);
		obp._objectsDictionary.TryGetValue(objectIni.readValue(sectionName, "SlaveMount", "", performLogging: false), out _slaveMountObject);
		obp._objectsDictionary.TryGetValue(objectIni.readValue(sectionName, "SlaveRadar", "", performLogging: false), out _slaveRadarObject);
		if (_baseObject.GetType() == typeof(Submarine))
		{
			_isSubmarineSensor = true;
		}
		LoadSensorSystemRadar();
		ReadViewArcs(objectIni, _sectionName);
		_radar._radarOffset = objectIni.readValue(sectionName, "RadarOffset", 0f, performLogging: false) * 0.00453571f;
	}

	public void LoadSensorSystemRadar()
	{
		IniHandler iniHandler = Utils.openVirtualIniFile("systems", "sensors", "sensors");
		if (!iniHandler.doesSectionExist(_systemName))
		{
			Log.Error("Could not find section '" + _systemName + "' in sensors.ini", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Sensors\\SensorSystemRadar.cs", 81, "LoadSensorSystemRadar");
		}
		string text = iniHandler.readValue(_systemName, "Type", "").ToLower();
		switch (text)
		{
		case "search":
		{
			_systemType = SystemType.SensorSearchRadar;
			_type = RadarType.Search;
			_rotationDirection = iniHandler.readValue(_systemName, "RotationDirection", 1);
			_rpm = iniHandler.readValue(_systemName, "RPM", 15f);
			RadarParameters rp4 = new RadarParameters(_systemName);
			_radar = new SearchRadar();
			_radar.init(_baseObject, rp4);
			_radar.setActive(active: true);
			Disable();
			break;
		}
		case "directedsearch":
		{
			_systemType = SystemType.SensorSearchRadar;
			_type = RadarType.DirectedSearch;
			RadarParameters rp3 = new RadarParameters(_systemName);
			_radar = new SearchRadar();
			_radar.init(_baseObject, rp3);
			_radar.setActive(active: true);
			break;
		}
		case "targeting":
		{
			_systemType = SystemType.SensorFCRadar;
			_type = RadarType.Targeting;
			_rpm = iniHandler.readValue(_systemName, "RPM", 60f);
			RadarParameters rp2 = new RadarParameters(_systemName);
			_radar = new FireControlRadar();
			_radar.init(_baseObject, rp2);
			_radar.setActive(active: false);
			break;
		}
		default:
		{
			Log.DebugWarning("No sensor type defined, using undefined - value is: '" + text + "'", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Sensors\\SensorSystemRadar.cs", 123, "LoadSensorSystemRadar");
			_type = RadarType.Undefined;
			_rotationDirection = 1;
			_rpm = 15f;
			RadarParameters rp = new RadarParameters(_systemName);
			_radar = new DummyRadar();
			_radar.init(_baseObject, rp);
			break;
		}
		}
		if (_systemType == SystemType.SensorFCRadar)
		{
			_identificationTime = 1f;
		}
		else if (_radar._rp._role == RadarParameters.Role.Surface)
		{
			_identificationTime = 60f;
			_radar._rp._minAltitude = -10f;
			_radar._rp._maxAltitude = -10f;
		}
		else
		{
			_identificationTime = 5f;
		}
		_identificationTime = iniHandler.readValue(_systemName, "IdentificationTime", _identificationTime);
		_radar._parentRadarSystem = this;
		_degreesPerSecond = 360f * _rpm / 60f;
		_verticalDegreesPerSecond = iniHandler.readValue(_systemName, "VerticalDegreesPerSecond", _degreesPerSecond);
		_lookDownMultiplier = iniHandler.readValue(_systemName, "LookDownMultiplier", 1f);
		_lookDownRange = _radar._rp._maxRange * _lookDownMultiplier;
		string text2 = iniHandler.readValue(_systemName, "Mode", "").ToLower();
		if (!(text2 == "radiocommand"))
		{
			if (text2 == "illuminate")
			{
				_mode = Mode.Illuminate;
			}
			else
			{
				_mode = Mode.Undefined;
			}
		}
		else
		{
			_mode = Mode.RadioCommand;
		}
		base.IsActiveSensor = true;
		_mountRotatable = _radar._rp._mountRotatable;
		_sensorRotatable = _radar._rp._radarRotatable;
	}

	public override void init()
	{
		base.init();
		if (_type == RadarType.Search || _type == RadarType.DirectedSearch)
		{
			_baseObject._obp._searchRadarSystems.Add(this);
			base.IsOn.Value = false;
			if (_radar._rp._role == RadarParameters.Role.Surface)
			{
				_baseObject._obp._surfaceSearchRadars.Add(this);
			}
			else if (_radar._rp._role == RadarParameters.Role.Air || _radar._rp._role == RadarParameters.Role.SurfaceAndAir)
			{
				_baseObject._obp._airSearchRadars.Add(this);
			}
		}
		else if (_type == RadarType.Targeting)
		{
			_baseObject._obp._targetingRadarSystems.Add(this);
		}
	}

	public GameObject getRadarMountObject()
	{
		return _mountObject;
	}

	public Radar getRadar()
	{
		return _radar;
	}

	public override void OnUpdate()
	{
		base.OnUpdate();
		if (_state == State.Destroyed)
		{
			_radar.setActive(active: false);
			return;
		}
		switch (_type)
		{
		case RadarType.Search:
			if (base.IsOn.Value)
			{
				_radar.setActive(active: true);
				_isReadyForClosing = false;
				if (!_targetRPMReached)
				{
					_currentRPM += 50f * GameTime.deltaTime;
					if (_currentRPM > _degreesPerSecond)
					{
						_targetRPMReached = true;
						_currentRPM = _degreesPerSecond;
					}
				}
				else
				{
					_currentRPM = _degreesPerSecond;
				}
				_currentRPM *= _rotationDirection;
			}
			else if (!base.IsOn.Value || _shutDownRequest)
			{
				_radar.setActive(active: false);
				_targetRPMReached = false;
				if (GameTime.TimeCompression > 2f)
				{
					_currentRPM = 0f;
					if (_mountObject != null)
					{
						_mountObject.transform.localEulerAngles = new Vector3(_mountObject.transform.localEulerAngles.x, 0f, _mountObject.transform.localEulerAngles.z);
					}
					_isReadyForClosing = true;
				}
				if (_isSubmarineSensor)
				{
					_currentRPM *= Mathf.Pow(0.0001f, GameTime.deltaTime);
					if (_currentRPM < 1f)
					{
						_isReadyForClosing = true;
					}
				}
				else
				{
					float num = 20f;
					if (_currentRPM <= num)
					{
						GameObject mountObject = _mountObject;
						if ((object)mountObject != null && mountObject.transform.localEulerAngles.y >= 0f && _mountObject?.transform.localEulerAngles.y < num / 20f)
						{
							_currentRPM = 0f;
							_mountObject.transform.localEulerAngles = new Vector3(_mountObject.transform.localEulerAngles.x, 0f, _mountObject.transform.localEulerAngles.z);
							_isReadyForClosing = true;
						}
						else
						{
							_currentRPM = num;
						}
					}
					else
					{
						GameObject mountObject2 = _mountObject;
						if ((object)mountObject2 != null && mountObject2.transform.localEulerAngles.y > 300f)
						{
							_currentRPM *= Mathf.Pow(0.0001f, GameTime.deltaTime);
						}
					}
				}
			}
			if (_isSubmarineSensor)
			{
				if (_state == State.Run)
				{
					((Submarine)_baseObject).setRadarMastOutState(isOut: true);
				}
				else
				{
					((Submarine)_baseObject).setRadarMastOutState(isOut: false);
				}
			}
			_mountObject?.transform.Rotate(new Vector3(0f, _currentRPM * GameTime.deltaTime, 0f));
			break;
		case RadarType.DirectedSearch:
			if (base.IsOn.Value)
			{
				_radar.setActive(active: true);
			}
			else
			{
				_radar.setActive(active: false);
			}
			break;
		case RadarType.Targeting:
			if (_isTargetTrackingSystem && (_targetObject != null || _preLaunchTargetObject != null || _associatedTargets.Count > 0))
			{
				bool flag = false;
				foreach (SensorSystem targetProviderSystem in _targetProviderSystems)
				{
					if (targetProviderSystem._isGuiding || targetProviderSystem._preLaunchTargetObject != null)
					{
						flag = true;
						break;
					}
				}
			}
			if (_isGuiding)
			{
				AlignToTarget(_targetObject);
			}
			else if (_preLaunchTargetObject != null)
			{
				AlignToTarget(_preLaunchTargetObject);
			}
			else if (!_restPositionReached)
			{
				AlignToRestPosition();
			}
			break;
		}
	}

	public Mode getMode()
	{
		return _mode;
	}

	public override SensorDistanceData GetRange(ObjectBase targetObject)
	{
		SensorDistanceData sensorDistanceData = new SensorDistanceData();
		if (targetObject == null || _radar == null)
		{
			return sensorDistanceData;
		}
		RadarParameters rp = _radar._rp;
		float altitudeOverClutter = targetObject.AltitudeOverClutter;
		if (rp._minAltitude >= 0f && altitudeOverClutter < rp._minAltitude && targetObject.isAirTarget())
		{
			sensorDistanceData.Issue = SensorIssue.MinAlt;
			sensorDistanceData.Distance = 0f;
			return sensorDistanceData;
		}
		float num = float.MaxValue;
		if (rp._maxAltitude >= 0f && targetObject.transform.position.y > rp._maxAltitude)
		{
			sensorDistanceData.Issue = SensorIssue.MaxAlt;
			sensorDistanceData.Distance = 0f;
			return sensorDistanceData;
		}
		float radarHorizon = RadarCalculator.getRadarHorizon(_mountObject.transform.position.y, targetObject.VisibleAltitude, visual: false, Utils.TerrainAltForHorizon(_baseObject, targetObject));
		if (_mountObject != null && IsInLookDown(_mountObject, targetObject, radarHorizon))
		{
			sensorDistanceData.Issue = SensorIssue.LookDown;
			num = _lookDownRange;
		}
		float num2;
		if (_type == RadarType.Targeting)
		{
			num2 = RadarCalculator.GetFCRDetectionRange(_mountObject.transform.position, targetObject._sensorData._maxRCS, rp._waveLength, rp._gainFactor, rp._peakPower, _baseObject._ECMSystemsTryingToJamMe, rp._frequency, _baseObject._crew);
		}
		else
		{
			SensorEcho sensorEcho = Globals._sensorEchoes.getFreeInstance();
			Vector3 position = _mountObject.transform.position;
			position.y = targetObject.transform.position.y;
			RadarCalculator.createRadarEcho(position, targetObject, rp._waveLength, rp._gainFactor, rp._peakPower, _baseObject._ECMSystemsTryingToJamMe, rp._frequency, _baseObject._crew, out sensorEcho, ignoreAspect: true);
			if (sensorEcho == null)
			{
				sensorDistanceData.Distance = 0f;
				return sensorDistanceData;
			}
			num2 = Mathf.Min(sensorEcho._minBurnThroughRange, rp._maxRange);
		}
		sensorDistanceData.Distance = Mathf.Min(num2, radarHorizon, num);
		return sensorDistanceData;
	}
}
