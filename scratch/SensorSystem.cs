using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using UniRx;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;

namespace SeaPower;

public abstract class SensorSystem : BaseSystem
{
	public enum SensorType
	{
		Unknown,
		Self,
		Visual,
		TVSeeker,
		SearchRadar,
		FCR,
		ARS,
		ActiveSonar,
		PassiveSonar,
		ActiveIntercept,
		ESM,
		MAD
	}

	[Flags]
	public enum SensorTypeSet
	{
		Unknown = 0,
		Self = 1,
		Visual = 2,
		TVSeeker = 4,
		SearchRadar = 8,
		FCR = 0x10,
		ARS = 0x20,
		ActiveSonar = 0x40,
		PassiveSonar = 0x80,
		ActiveIntercept = 0x100,
		ESM = 0x200,
		MAD = 0x400
	}

	public enum State
	{
		Idle,
		Enable,
		OpenSystem,
		WaitForOpenSystem,
		Run,
		Disable,
		WaitForCloseReadyness,
		CloseSystem,
		WaitForCloseSystem,
		Destroyed
	}

	public class SensorDistanceData
	{
		public SensorIssue Issue;

		public float SensorEffectiveness = 1f;

		public float Distance = float.MaxValue;
	}

	public enum SensorIssue
	{
		None,
		LookDown,
		MinAlt,
		MaxAlt
	}

	private static readonly ProfilerMarker _profSystem = new ProfilerMarker("System: SensorSystem");

	private static Dictionary<SensorTypeSet, string> sensorDescriptions = new Dictionary<SensorTypeSet, string>();

	public bool _isDippingSonar;

	public Entity _dataEntity;

	public TowedSystem _parentTowedSystem;

	public bool _searchBelowLayer = true;

	protected float _restHorizontalAngle;

	protected float _restVerticalAngle;

	public float _turnedOnTime = float.MinValue;

	public float _turnOffTime = -1f;

	public List<SensorSystem> _associatedFCRs = new List<SensorSystem>();

	public List<WeaponParameters> _associatedWeaponSystemParameters = new List<WeaponParameters>();

	public bool _allowTurnOn = true;

	protected bool _requestEnable;

	protected bool _requestDisable;

	protected bool _isReadyForClosing = true;

	protected State _state;

	protected bool _shutDownRequest;

	public bool _wasSetOn;

	public bool _isWithinOnConditions;

	public List<ObjectBase> _associatedTargets = new List<ObjectBase>();

	public List<ObjectBase> _associatedWeapons = new List<ObjectBase>();

	public List<ObjectBase> _detectedObjects = new List<ObjectBase>();

	public List<ObjectBase> _identifiedObjects = new List<ObjectBase>();

	public ObjectBase _targetObject;

	public ObjectBase _preLaunchTargetObject;

	public WeaponSystemGun.ShootingMode _currentShootingMode;

	private float _checkSolutionTime = -1000f;

	public float _solution;

	private float _prevTargetHeading;

	private float _prevTargetVelocityInKnots;

	private bool _firstCheckDone;

	public bool _isGuiding;

	public bool _restPositionReached = true;

	public int _targetChannels;

	public int _weaponChannels;

	public float _weaponGuidancePenalty;

	public GameObject _sensorObject;

	protected GameObject _slaveMountObject;

	protected GameObject _slaveRadarObject;

	public bool _isTargetTrackingSystem;

	public List<SensorSystem> _targetTrackingSystems = new List<SensorSystem>();

	public List<SensorSystem> _targetProviderSystems = new List<SensorSystem>();

	protected bool _mountRotatable;

	protected bool _sensorRotatable;

	public float _degreesPerSecond;

	public float _verticalDegreesPerSecond;

	public Vector2 _horizontalViewArc = new Vector2(-181f, 181f);

	public Vector2 _verticalViewArc = new Vector2(-91f, 91f);

	public bool _requiresAlignment = true;

	private string displayState;

	public float _identificationTime = 30f;

	public SensorSystem _masterSystem;

	public string _masterSystemSectionName = "";

	public bool _requiresMasterOn;

	public bool _masterSystemCheckDone;

	public bool IsActiveSensor { get; set; }

	public bool IsTowedSensor { get; set; }

	public bool IsVDS { get; set; }

	public Entity InstanceEntity { get; internal set; }

	public ReactiveProperty<bool> SearchBelowLayer { get; private set; } = new ReactiveProperty<bool>();

	public bool CanProduceContacts
	{
		get
		{
			if (base.IsOn.Value && !base.Inoperable.Value && !_requestDisable && !_requestEnable)
			{
				if (!(_baseObject != null))
				{
					return true;
				}
				return _baseObject._hasControl;
			}
			return false;
		}
	}

	public abstract ISensor Sensor { get; }

	public string DisplayState
	{
		get
		{
			return displayState;
		}
		set
		{
			displayState = value;
			NotifyPropertyChanged("DisplayState");
		}
	}

	public ICommand ToggleEnableCommand { get; protected set; }

	public static SensorTypeSet SensorSetForSensor(SensorType sensor)
	{
		return sensor switch
		{
			SensorType.Unknown => SensorTypeSet.Unknown, 
			SensorType.Self => SensorTypeSet.Self, 
			SensorType.Visual => SensorTypeSet.Visual, 
			SensorType.TVSeeker => SensorTypeSet.Visual, 
			SensorType.SearchRadar => SensorTypeSet.SearchRadar, 
			SensorType.FCR => SensorTypeSet.FCR, 
			SensorType.ARS => SensorTypeSet.ARS, 
			SensorType.ActiveSonar => SensorTypeSet.ActiveSonar, 
			SensorType.PassiveSonar => SensorTypeSet.PassiveSonar, 
			SensorType.ActiveIntercept => SensorTypeSet.ActiveIntercept, 
			SensorType.ESM => SensorTypeSet.ESM, 
			SensorType.MAD => SensorTypeSet.MAD, 
			_ => SensorTypeSet.Unknown, 
		};
	}

	public static string MakeSourceDescription(SensorTypeSet sensors)
	{
		if (sensorDescriptions.TryGetValue(sensors, out var value))
		{
			return value;
		}
		bool flag = false;
		string text = "";
		string text2 = Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "DetectedBy_Separator");
		if ((sensors & SensorTypeSet.Visual) != SensorTypeSet.Unknown)
		{
			text = Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "DetectedByVisual");
			flag = true;
		}
		if ((sensors & SensorTypeSet.TVSeeker) != SensorTypeSet.Unknown)
		{
			text = Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "DetectedByTVS");
			flag = true;
		}
		if ((sensors & (SensorTypeSet.SearchRadar | SensorTypeSet.FCR)) != SensorTypeSet.Unknown)
		{
			if (flag)
			{
				text += text2;
			}
			text += Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "DetectedByRadar");
			flag = true;
		}
		if ((sensors & SensorTypeSet.ARS) != SensorTypeSet.Unknown)
		{
			if (flag)
			{
				text += text2;
			}
			text += Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "DetectedByARS");
			flag = true;
		}
		if ((sensors & (SensorTypeSet.ActiveSonar | SensorTypeSet.PassiveSonar)) != SensorTypeSet.Unknown)
		{
			if (flag)
			{
				text += text2;
			}
			text += Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "DetectedBySonar");
			flag = true;
		}
		if ((sensors & SensorTypeSet.ESM) != SensorTypeSet.Unknown)
		{
			if (flag)
			{
				text += text2;
			}
			text += Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "DetectedByESM");
			flag = true;
		}
		if ((sensors & SensorTypeSet.MAD) != SensorTypeSet.Unknown)
		{
			if (flag)
			{
				text += text2;
			}
			text += Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "DetectedByMAD");
			flag = true;
		}
		sensorDescriptions[sensors] = text;
		return text;
	}

	public override void LoadFromInI(IniHandler ini, string sectionName, ObjectBaseParameters obp, string keyName = "")
	{
		_systemName = ini.readValue(sectionName, keyName + "SystemName", _sectionName);
		IniHandler iniHandler = Utils.openVirtualIniFile("systems", "sensors", "sensors");
		_targetChannels = iniHandler.readValue(_systemName, "TargetChannels", 1);
		_weaponChannels = iniHandler.readValue(_systemName, "WeaponChannels", 1);
		_weaponGuidancePenalty = ini.readValue(sectionName, "WeaponGuidancePenalty", iniHandler.readValue(_systemName, "WeaponGuidancePenalty", 0f));
		ReadViewArcs(iniHandler, _systemName);
		_requiresAlignment = iniHandler.readValue(_systemName, "RequiresAlignment", defaultValue: true, performLogging: false);
		_identificationTime = iniHandler.readValue(_systemName, "IdentificationTime", _identificationTime);
		_moduleName = iniHandler.readValue(_systemName, "ModuleType", "");
		_masterSystemSectionName = ini.readValue(sectionName, "MasterSystem", "");
		_requiresMasterOn = ini.readValue(sectionName, "RequiresMasterOn", defaultValue: false);
		LoadBaseParameters(ini, sectionName, obp);
		LoadSensorSystemData(ini, obp);
		Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
		InstanceEntity = defaultGameObjectInjectionWorld.EntityManager.CreateEntity(typeof(SPSensor), typeof(SPSensorRef), typeof(ShouldSaveComponent));
		EntityManagerManagedComponentExtensions.SetComponentData(defaultGameObjectInjectionWorld.EntityManager, InstanceEntity, new SPSensor
		{
			Object = this
		});
		defaultGameObjectInjectionWorld.EntityManager.SetComponentData(InstanceEntity, new SPSensorRef
		{
			ObjectUID = _baseObject.UniqueID,
			SectionName = _sectionName
		});
	}

	public void ReadViewArcs(IniHandler ini, string sectionName)
	{
		if (!ini.doesKeyExist(sectionName, "ViewArcs"))
		{
			return;
		}
		string[] array = ini.readValue(sectionName, "ViewArcs", "", performLogging: false).Split('|');
		if (!string.IsNullOrEmpty(array[0]))
		{
			if (array.Length == 1 && array[0].Split(',').Length == 1 && float.TryParse(array[0], out var result) && result > 0f)
			{
				_horizontalViewArc = new Vector2((0f - result) / 2f, result / 2f);
				_verticalViewArc = _horizontalViewArc;
			}
			_horizontalViewArc = ParseArc(_horizontalViewArc, array[0]);
			if (array.Length > 1)
			{
				_verticalViewArc = ParseArc(_verticalViewArc, array[1]);
			}
		}
	}

	private Vector2 ParseArc(Vector2 vector, string arcsData)
	{
		string[] array = arcsData.Split(',');
		if (array.Length == 1)
		{
			if (float.TryParse(array[0], out var result) && result > 0f)
			{
				vector = new Vector2((0f - result) / 2f, result / 2f);
			}
		}
		else
		{
			Vector2 zero = Vector2.zero;
			bool flag = false;
			if (!float.TryParse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture, out zero.x))
			{
				flag = true;
			}
			if (!float.TryParse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture, out zero.y))
			{
				flag = true;
			}
			if (!flag && zero != Vector2.zero)
			{
				vector = zero;
			}
		}
		return vector;
	}

	public override void init()
	{
		base.init();
		DisplayState = GetStatus();
		SearchBelowLayer.Value = true;
		ToggleEnableCommand = new DelegateCommand(delegate
		{
			if (base.IsOn.Value)
			{
				Disable();
			}
			else
			{
				Enable();
			}
		});
	}

	public override void OnUpdate()
	{
		base.OnUpdate();
		_ = base.DisplayName.Value;
		if (base.IsDestroyed && _state != State.Destroyed)
		{
			_state = State.Destroyed;
			base.IsOn.Value = false;
		}
		else if (_state == State.Destroyed && !base.IsDestroyed)
		{
			_state = State.Idle;
		}
		if ((_requestEnable || _requestDisable) && (_state == State.Idle || _state == State.Run))
		{
			if (_requestEnable)
			{
				if (!base.IsOn.Value)
				{
					_state = State.Enable;
				}
				_requestEnable = false;
			}
			if (_requestDisable)
			{
				if (base.IsOn.Value)
				{
					_state = State.Disable;
				}
				_requestDisable = false;
			}
		}
		switch (_state)
		{
		case State.Enable:
			_shutDownRequest = false;
			_state = State.OpenSystem;
			break;
		case State.OpenSystem:
			if (_openSystemAnimation != null)
			{
				_openSystemAnimation.playAnim();
			}
			_state = State.WaitForOpenSystem;
			break;
		case State.WaitForOpenSystem:
			if (_openSystemAnimation != null)
			{
				if (!_openSystemAnimation.isPlaying())
				{
					base.IsOn.Value = true;
					_state = State.Run;
				}
			}
			else
			{
				base.IsOn.Value = true;
				_state = State.Run;
			}
			break;
		case State.Disable:
			_shutDownRequest = true;
			base.IsOn.Value = false;
			_detectedObjects.Clear();
			_identifiedObjects.Clear();
			if (GetType() == typeof(SensorSystemSonar))
			{
				((SensorSystemSonar)this)._sonar.clearContactLists();
				((SensorSystemSonar)this)._sonar.RawPassiveSonarReadings.Clear();
			}
			_state = State.WaitForCloseReadyness;
			break;
		case State.WaitForCloseReadyness:
			if (_isReadyForClosing)
			{
				_state = State.CloseSystem;
			}
			break;
		case State.CloseSystem:
			if (_closeSystemAnimation != null)
			{
				_closeSystemAnimation.playAnim();
			}
			_state = State.WaitForCloseSystem;
			break;
		case State.WaitForCloseSystem:
			if (_closeSystemAnimation != null)
			{
				if (!_closeSystemAnimation.isPlaying())
				{
					_state = State.Idle;
				}
			}
			else
			{
				_state = State.Idle;
			}
			break;
		}
		if (_state != State.Destroyed)
		{
			if (_openSystemAnimation != null)
			{
				_openSystemAnimation.update();
			}
			if (_closeSystemAnimation != null)
			{
				_closeSystemAnimation.update();
			}
			if (base.IsOn.Value && (_state == State.Run || _state == State.Idle) && _isGuiding && _currentShootingMode == WeaponSystemGun.ShootingMode.Naval && GameTime.time - _checkSolutionTime > Globals._solutionCheckInterval)
			{
				_checkSolutionTime = GameTime.time + Globals._solutionCheckInterval;
				float multiplier = 1.5f;
				if (this is SensorSystemRadar)
				{
					multiplier = 2f;
				}
				SensorUtils.BuildSolution(ref _solution, ref _prevTargetHeading, ref _prevTargetVelocityInKnots, ref _firstCheckDone, _targetObject, _mountObject, multiplier);
			}
		}
		if (_baseObject == Singleton<RenderPosition>.Instance.getSelectedObject())
		{
			DisplayState = GetStatus();
		}
		CheckChannels();
	}

	public void Enable(bool allowMessage = true, bool turnOffAfterEngage = false)
	{
		if (_state == State.Destroyed || base.Inoperable.Value || !_allowTurnOn)
		{
			return;
		}
		bool flag = allowMessage && _baseObject == Singleton<RenderPosition>.Instance.getSelectedObject() && _baseObject.AcceptsOrdersFromPlayer;
		if (_baseObject is Submarine submarine)
		{
			float num = 5f;
			string text = "#";
			if (submarine.IsSubmerged.Value)
			{
				if (_isSubmarineMast)
				{
					if (submarine.isBelowPeriscopeDepth())
					{
						text = "UnableToRaiseMast";
					}
					else if ((float)Singleton<Environment>.Instance.SeaState > num)
					{
						text = "UnableToRaiseMastSeaState";
					}
					else if (_baseObject.IsPlayerObject)
					{
						if (_systemType == SystemType.SensorPeriscope)
						{
							Singleton<VoiceMessage>.Instance.PlayVoiceMessage("RaiseScope", _baseObject.Nation.Value);
						}
						else if (this is SensorSystemRadar)
						{
							Singleton<VoiceMessage>.Instance.PlayVoiceMessage("RaiseRadar", _baseObject.Nation.Value);
						}
						else if (this is SensorSystemESM)
						{
							Singleton<VoiceMessage>.Instance.PlayVoiceMessage("RaiseESM", _baseObject.Nation.Value);
						}
					}
				}
				else
				{
					if (_systemType == SystemType.SensorVisual)
					{
						text = "UnableToSendWatch";
					}
					if (_systemType == SystemType.SensorFCRadar)
					{
						text = "UnableToUseFCRUnderwater";
					}
				}
			}
			if (text != "#")
			{
				if (flag)
				{
					string text2 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", text);
					text2 = text2.Replace("${Depth}", (submarine.SP._periscopeDepth * 220.47266f).ToString("0") + " ft");
					Globals._mainGameViewModel.RaiseAlert(_baseObject, text2);
				}
				return;
			}
		}
		if (_baseObject == Singleton<RenderPosition>.Instance.getSelectedObject())
		{
			Type type = GetType();
			if ((type == typeof(SensorSystemSonar) && IsTowedSensor) || type == typeof(DecoySystem))
			{
				bool flag2 = isTowedSystemSpeedWithinDeployConditions(_baseObject);
				bool flag3 = isTowedSystemAltitudeWithinDeployConditions(_baseObject);
				bool flag4 = !itIsTooShallow(_baseObject);
				bool flag5 = isSurfacedSubmarine(_baseObject);
				bool flag6 = isSubmarineNearBottom(_baseObject);
				if (IsTowedSensor && _baseObject is Helicopter helicopter && this is SensorSystemSonar)
				{
					if (_wasSetOn && !base.IsOn.Value)
					{
						Disable();
						return;
					}
					helicopter.DesiredAltitude.Value = 0.1814284f;
					_wasSetOn = true;
				}
				else
				{
					if (!flag2 && !flag3)
					{
						getTowedSystemDeployConditions();
						string text3 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "UnableToDeployTowedDueSpeedAndAlt");
						text3 = text3.Replace("${TowedSystemName}", base.DisplayName.Value);
						Globals._mainGameViewModel.RaiseAlert(_baseObject, text3);
						return;
					}
					if (!flag2)
					{
						TowedDeployConditions towedSystemDeployConditions = getTowedSystemDeployConditions();
						string text4 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "UnableToDeployTowedDueSpeed");
						float num2 = towedSystemDeployConditions._minTowedSystemDeploySpeed * 130.6266f;
						float num3 = towedSystemDeployConditions._maxTowedSystemDeploySpeed * 130.6266f;
						text4 = text4.Replace("${TowedSystemName}", base.DisplayName.Value);
						text4 = text4.Replace("${MinDeploySpeed}", num2.ToString("0") + " knots");
						text4 = text4.Replace("${MaxDeploySpeed}", num3.ToString("0") + " knots");
						Globals._mainGameViewModel.RaiseAlert(_baseObject, text4);
						return;
					}
					if (!flag3)
					{
						if (_baseObject._type == ObjectBase.ObjectType.Helicopter)
						{
							_baseObject.DesiredAltitude.Value = 0.1814284f;
							if (type == typeof(SensorSystemSonar))
							{
								((SensorSystemSonar)this)._wasSetOn = true;
							}
						}
						TowedDeployConditions towedSystemDeployConditions2 = getTowedSystemDeployConditions();
						string text5 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "UnableToDeployTowedDueAltitude");
						text5 = text5.Replace("${TowedSystemName}", base.DisplayName.Value);
						text5 = text5.Replace("${MaxDeployAltitude}", (towedSystemDeployConditions2._maxTowedSystemDeployAltitude * 220.47266f).ToString("0") + " ft");
						Globals._mainGameViewModel.RaiseAlert(_baseObject, text5);
						return;
					}
					if (!flag4)
					{
						TowedDeployConditions towedSystemDeployConditions3 = getTowedSystemDeployConditions();
						string text6 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "TooShallowToDeployTowed");
						text6 = text6.Replace("${TowedSystemName}", base.DisplayName.Value);
						SonarParameters.SonarType sonarType = SonarParameters.SonarType.Undefined;
						if (type == typeof(SensorSystemSonar))
						{
							sonarType = (this as SensorSystemSonar)._sonar._sp._type;
						}
						text6 = text6.Replace("${MinDepthUnderKeel}", sonarType switch
						{
							SonarParameters.SonarType.VariableDepthSonar => 200f.ToString("0"), 
							SonarParameters.SonarType.DippingSonar => 100f.ToString("0"), 
							_ => ((0f - towedSystemDeployConditions3._minDeployDepth) * 220.47266f + 50f).ToString("0"), 
						} + " ft");
						Globals._mainGameViewModel.RaiseAlert(_baseObject, text6);
						return;
					}
					if (flag5)
					{
						string text7 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "SurfacedSubsCannotDeployTowed");
						text7 = text7.Replace("${TowedSystemName}", base.DisplayName.Value);
						Globals._mainGameViewModel.RaiseAlert(_baseObject, text7);
						return;
					}
					if (flag6)
					{
						string text8 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "BottomIsTooCloseToSub");
						text8 = text8.Replace("${TowedSystemName}", base.DisplayName.Value);
						Globals._mainGameViewModel.RaiseAlert(_baseObject, text8);
						return;
					}
				}
			}
		}
		_requestEnable = true;
		_requestDisable = false;
		_wasSetOn = true;
		_turnedOnTime = GameTime.time;
		if (_baseObject is Submarine)
		{
			turnOffAfterEngage = false;
		}
		if (turnOffAfterEngage && !base.IsOn.Value && !_baseObject._sensorsToTurnOff.Contains(this))
		{
			_baseObject._sensorsToTurnOff.Add(this);
		}
		if (this is SensorSystemRadar sensorSystemRadar)
		{
			if (sensorSystemRadar._type == SensorSystemRadar.RadarType.Search || sensorSystemRadar._type == SensorSystemRadar.RadarType.DirectedSearch)
			{
				_baseObject.setEMCON(emcon: false);
			}
		}
		else if (this is SensorSystemSonar sensorSystemSonar)
		{
			if (sensorSystemSonar.IsActiveSensor && (sensorSystemSonar._sonar._sp._isActiveOnly || sensorSystemSonar._sonar._sp._type == SonarParameters.SonarType.VariableDepthSonar))
			{
				sensorSystemSonar._sonar.IsActive.Value = true;
				_baseObject.setEMCON(emcon: false);
			}
			if (flag)
			{
				if (sensorSystemSonar.IsVDS || sensorSystemSonar._isDippingSonar)
				{
					Singleton<VoiceMessage>.Instance.PlayVoiceMessage("VDSDeploy", _baseObject.Nation.Value);
				}
				else if (sensorSystemSonar.IsTowedSensor)
				{
					Singleton<VoiceMessage>.Instance.PlayVoiceMessage("TADeploy", _baseObject.Nation.Value);
				}
			}
		}
		else if (this is DecoySystem decoySystem)
		{
			_baseObject.setEMCON(emcon: false);
			if (decoySystem._decoyObject != null)
			{
				decoySystem._decoyObject._canUpdate = true;
			}
		}
		else if (GetType() == typeof(SensorSystemECM))
		{
			_baseObject.setEMCON(emcon: false);
		}
	}

	public void CheckChannels()
	{
		for (int num = _associatedTargets.Count - 1; num > -1; num--)
		{
			if (_associatedTargets[num] == null || _associatedTargets[num].IsDestroyed)
			{
				_associatedTargets.RemoveAt(num);
			}
		}
		for (int num2 = _associatedWeapons.Count - 1; num2 > -1; num2--)
		{
			if (_associatedWeapons[num2] == null || _associatedWeapons[num2].IsDestroyed || ((WeaponBase)_associatedWeapons[num2]).ConnectionLostForever.Value)
			{
				_associatedWeapons.RemoveAt(num2);
			}
		}
	}

	public void Disable(bool allowMessage = false)
	{
		_requestDisable = true;
		_requestEnable = false;
		_wasSetOn = false;
		if (this is SensorSystemSonar sensorSystemSonar)
		{
			if (sensorSystemSonar.IsActiveSensor)
			{
				sensorSystemSonar._sonar.IsActive.Value = false;
			}
		}
		else if (this is SensorSystemRadar || this is SensorSystemLaserDesignator)
		{
			ClearGuiding();
		}
		else if (this is SensorSystemECM sensorSystemECM)
		{
			sensorSystemECM._ecm.ceaseOffensiveJam();
			sensorSystemECM._ecm.ceaseDefensiveJam();
		}
		else if (this is DecoySystem decoySystem && decoySystem._decoyObject != null)
		{
			decoySystem._decoyObject._canUpdate = false;
		}
		if (!allowMessage || !(_baseObject == Singleton<RenderPosition>.Instance.getSelectedObject()) || base.Inoperable.Value || !_baseObject.IsPlayerObject)
		{
			return;
		}
		if (_isSubmarineMast)
		{
			if (_systemType == SystemType.SensorPeriscope)
			{
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("LowerScope", _baseObject.Nation.Value);
			}
			else if (this is SensorSystemRadar)
			{
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("LowerRadar", _baseObject.Nation.Value);
			}
			else if (this is SensorSystemESM)
			{
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("LowerESM", _baseObject.Nation.Value);
			}
		}
		else if (this is SensorSystemSonar sensorSystemSonar2)
		{
			if (sensorSystemSonar2.IsVDS || sensorSystemSonar2._isDippingSonar)
			{
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("VDSRetract", _baseObject.Nation.Value);
			}
			else if (sensorSystemSonar2.IsTowedSensor)
			{
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("TARetract", _baseObject.Nation.Value);
			}
		}
	}

	public bool isBusy()
	{
		if (_state != State.Idle)
		{
			return _state != State.Run;
		}
		return false;
	}

	public string CurrentState()
	{
		return _state.ToString();
	}

	public State CurrentStateEnum()
	{
		return _state;
	}

	public override void SetDisabled()
	{
		base.IsOn.Value = false;
		_state = State.Idle;
	}

	public void ToggleTowedSystemSearchDepth()
	{
		if (IsTowedSensor)
		{
			SearchBelowLayer.Value = !SearchBelowLayer.Value;
		}
	}

	public void LoadSensorSystemData(IniHandler ini, ObjectBaseParameters obp)
	{
		if (_baseObject is Submarine)
		{
			_isSubmarineSensor = true;
		}
		EntityManager entityManager = Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager;
		foreach (Entity systemDatum in obp.systemData)
		{
			if (!entityManager.HasComponent<MountedSystemDataComponent>(systemDatum))
			{
				continue;
			}
			MountedSystemDataComponent componentData = entityManager.GetComponentData<MountedSystemDataComponent>(systemDatum);
			if (entityManager.HasComponent<SensorDataComponent>(componentData._system))
			{
				SensorDataComponent componentData2 = entityManager.GetComponentData<SensorDataComponent>(componentData._system);
				if (componentData2.Name == _systemName)
				{
					_dataEntity = componentData._system;
					break;
				}
			}
		}
		string text = ini.readValue(_sectionName, "Mount", "Dummy");
		_restHorizontalAngle = ini.readValue(_sectionName, "RestHorizontalAngle", 0f, performLogging: false);
		_restVerticalAngle = ini.readValue(_sectionName, "RestVerticalAngle", 0f, performLogging: false);
		Vector2 vector = ini.readValue(_sectionName, "RestAngles", Vector2.zero, performLogging: false);
		if (vector == Vector2.zero)
		{
			vector.x = ini.readValue(_sectionName, "RestAngles", 0f, performLogging: false);
		}
		_restHorizontalAngle = vector.x;
		_restVerticalAngle = vector.y;
		GameObject value = null;
		if (text.ToLower() == "dummy")
		{
			value = new GameObject("Dummy Sensor Mount " + _systemName);
			Vector3 vector2 = ini.readValue(_sectionName, "MountPosition", Vector3.zero);
			Quaternion localRotation = Quaternion.Euler(ini.readValue(_sectionName, "MountRotation", Vector3.zero));
			if (this is SensorSystemVisual && vector2.y > obp._baseObject._viewPointLocalPosition.y)
			{
				obp._baseObject._viewPointLocalPosition = vector2;
			}
			string key = ini.readValue(_sectionName, "MountParent", "", performLogging: false);
			GameObject value2 = null;
			obp._objectsDictionary.TryGetValue(key, out value2);
			if (value2 != null)
			{
				value.transform.SetParent(value2.transform);
			}
			else
			{
				value.transform.SetParent(obp._dummySensorMountsParent.transform);
			}
			value.transform.localPosition = vector2;
			value.transform.localRotation = localRotation;
			if (ini.readValue(_sectionName, "HideWithActiveDamageModel", defaultValue: false, performLogging: false) && !obp._objectsToDisableWhenDamaged.Contains(value))
			{
				obp._objectsToDisableWhenDamaged.Add(value);
			}
		}
		else
		{
			obp._objectsDictionary.TryGetValue(text, out value);
			if (value == null)
			{
				Log.Error("Could not find sensor mount '" + text + "' for system '" + _systemName + "', section '" + _sectionName + "' in file '" + ini.getFileName() + "'", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Sensors\\SensorSystem.cs", 823, "LoadSensorSystemData");
			}
		}
		_mountObject = value;
	}

	public bool IsInLookDown(GameObject sensorMount, ObjectBase targetObj, float horizon)
	{
		Vector3 position = targetObj.transform.position;
		position.y = targetObj.VisibleAltitude;
		float num = Mathf.Atan(Mathf.Tan(sensorMount.transform.position.y / horizon)) * 57.29578f;
		float num2 = 90f - num;
		Vector3 to = position - sensorMount.transform.position;
		if (Vector3.Angle(Vector3.down, to) + 1.5f < num2)
		{
			return true;
		}
		return false;
	}

	public abstract SensorDistanceData GetRange(ObjectBase targetObject);

	public bool HasFreeChannels()
	{
		if (HasFreeTargetChannels())
		{
			return HasFreeWeaponChannels();
		}
		return false;
	}

	public bool HasFreeTargetChannels()
	{
		foreach (SensorSystem targetTrackingSystem in _targetTrackingSystems)
		{
			if (targetTrackingSystem.IsOn.Value && targetTrackingSystem._associatedTargets.Count < targetTrackingSystem._targetChannels)
			{
				return true;
			}
		}
		return _associatedTargets.Count < _targetChannels;
	}

	public bool HasFreeTargetChannels(ObjectBase targetObject)
	{
		foreach (SensorSystem targetTrackingSystem in _targetTrackingSystems)
		{
			if (targetTrackingSystem.IsOn.Value)
			{
				if (targetTrackingSystem._associatedTargets.Count < targetTrackingSystem._targetChannels)
				{
					return true;
				}
				if (targetTrackingSystem._associatedTargets.Contains(targetObject))
				{
					return true;
				}
			}
		}
		if (_associatedTargets.Count < _targetChannels)
		{
			return true;
		}
		if (_associatedTargets.Contains(targetObject))
		{
			return true;
		}
		return false;
	}

	public bool HasFreeWeaponChannels()
	{
		return _associatedWeapons.Count < _weaponChannels;
	}

	public bool IsAimedAtTarget(ObjectBase targetObject)
	{
		foreach (SensorSystem targetTrackingSystem in _targetTrackingSystems)
		{
			if (targetTrackingSystem.IsOn.Value || _wasSetOn)
			{
				if (targetTrackingSystem._preLaunchTargetObject == targetObject)
				{
					return true;
				}
				if (targetTrackingSystem._associatedTargets.Contains(targetObject))
				{
					return true;
				}
			}
		}
		if (!base.IsOn.Value)
		{
			return false;
		}
		if (targetObject == null)
		{
			return false;
		}
		if (_preLaunchTargetObject == targetObject)
		{
			return true;
		}
		return _associatedTargets.Contains(targetObject);
	}

	public bool IsGuidingWeapon(ObjectBase weaponObject)
	{
		if (!base.IsOn.Value)
		{
			return false;
		}
		return _associatedWeapons.Contains(weaponObject);
	}

	public bool TryToAddTarget(ObjectBase targetObject)
	{
		if (!base.IsOn.Value && !_wasSetOn)
		{
			return false;
		}
		foreach (SensorSystem targetTrackingSystem in _targetTrackingSystems)
		{
			if ((targetTrackingSystem.IsOn.Value || _wasSetOn) && (targetTrackingSystem._associatedTargets.Count < targetTrackingSystem._targetChannels || targetTrackingSystem._associatedTargets.Contains(targetObject)))
			{
				setTargetObject(targetObject);
				_associatedTargets.Add(targetObject);
				targetTrackingSystem._isGuiding = true;
				return true;
			}
		}
		if (_associatedTargets.Contains(targetObject))
		{
			return true;
		}
		if (_associatedTargets.Count >= _targetChannels)
		{
			return false;
		}
		_associatedTargets.Add(targetObject);
		return true;
	}

	public bool CanAddTarget(ObjectBase targetObject, bool ignoreOnStatus)
	{
		if (ignoreOnStatus || (!base.IsOn.Value && !_wasSetOn))
		{
			return false;
		}
		foreach (SensorSystem targetTrackingSystem in _targetTrackingSystems)
		{
			if (!ignoreOnStatus && (targetTrackingSystem.IsOn.Value || _wasSetOn) && (targetTrackingSystem._associatedTargets.Count < targetTrackingSystem._targetChannels || targetTrackingSystem._associatedTargets.Contains(targetObject)))
			{
				return true;
			}
		}
		if (_associatedTargets.Contains(targetObject))
		{
			return true;
		}
		if (_associatedTargets.Count >= _targetChannels)
		{
			return false;
		}
		return true;
	}

	public bool TryToAddWeapon(ObjectBase targetObject)
	{
		if (!base.IsOn.Value)
		{
			return false;
		}
		if (_associatedWeapons.Contains(targetObject))
		{
			return false;
		}
		if (_associatedWeapons.Count >= _weaponChannels)
		{
			return false;
		}
		_associatedWeapons.Add(targetObject);
		DM.I("[" + targetObject.getUIDAndName() + "] added to weapon channels of " + _sectionName + " " + _systemName + " of " + _baseObject.getUIDAndName());
		return true;
	}

	public void ClearGuiding()
	{
		if (_associatedTargets != null)
		{
			_associatedTargets.Clear();
		}
		if (_associatedWeapons != null)
		{
			_associatedWeapons.Clear();
		}
		_isGuiding = false;
		_targetObject = null;
		_preLaunchTargetObject = null;
		_restPositionReached = false;
		_solution = 0f;
		_firstCheckDone = false;
	}

	public bool IsObjectInFOV(Vector3 ownPosition, Vector3 ownTrajectory, Vector3 objPos)
	{
		if (_verticalViewArc.x < -90f && _verticalViewArc.y > 90f && _horizontalViewArc.x < -180f && _horizontalViewArc.y > 180f)
		{
			return true;
		}
		if (_horizontalViewArc == _verticalViewArc && _horizontalViewArc.x == 0f - _horizontalViewArc.y)
		{
			Vector3 to = objPos - ownPosition;
			if (Vector3.Angle(ownTrajectory, to) < _horizontalViewArc.y)
			{
				return true;
			}
		}
		float num = Utils.WrapAngle(0f - Utils.calculateYawToTarget(ownTrajectory, ownPosition, objPos));
		Vector3 vector = _mountObject.transform.InverseTransformPoint(objPos);
		float num2 = 180f / MathF.PI * Mathf.Asin(Mathf.Abs(vector.y) / vector.magnitude);
		if (vector.y < 0f)
		{
			num2 *= -1f;
		}
		if (_verticalViewArc.x > num2 || num2 > _verticalViewArc.y)
		{
			return false;
		}
		if (_horizontalViewArc.x < _horizontalViewArc.y)
		{
			if (_horizontalViewArc.x < num && num < _horizontalViewArc.y)
			{
				return true;
			}
		}
		else if (_horizontalViewArc.y >= num || num >= _horizontalViewArc.x)
		{
			return true;
		}
		return false;
	}

	public void FreeChannels(ObjectBase targetObject, WeaponBase weapon)
	{
		_baseObject.DelayedEndAlignment(weapon._alignmentUID, 0f, "FreeChannels");
		if (_associatedWeapons.Contains(weapon))
		{
			_associatedWeapons.Remove(weapon);
		}
		foreach (WeaponBase associatedWeapon in _associatedWeapons)
		{
			if (associatedWeapon.CurrentIntendedTargetObject == targetObject)
			{
				return;
			}
		}
		if (_associatedTargets.Contains(targetObject))
		{
			_associatedTargets.Remove(targetObject);
		}
		else if (weapon._initialSetTargetObject != null && _associatedTargets.Contains(weapon._initialSetTargetObject))
		{
			_associatedTargets.Remove(weapon._initialSetTargetObject);
		}
		if (_associatedTargets.Count < 1 && _associatedWeapons.Count < 1)
		{
			ClearGuiding();
		}
	}

	public void DropTarget(ObjectBase targetObject, WeaponBase weapon)
	{
		foreach (WeaponBase associatedWeapon in _associatedWeapons)
		{
			if (!(associatedWeapon == weapon) && associatedWeapon.CurrentIntendedTargetObject == targetObject)
			{
				return;
			}
		}
		if (_associatedTargets.Contains(targetObject))
		{
			_associatedTargets.Remove(targetObject);
		}
		else if (weapon._initialSetTargetObject != null && _associatedTargets.Contains(weapon._initialSetTargetObject))
		{
			_associatedTargets.Remove(weapon._initialSetTargetObject);
		}
		if (_associatedTargets.Count < 1 && _associatedWeapons.Count < 1)
		{
			ClearGuiding();
		}
	}

	public string GetStatus()
	{
		string result = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Inoperable");
		if (!base.Inoperable.Value)
		{
			if (!base.IsOn.Value)
			{
				result = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "SensorsOff");
			}
			else if (_associatedTargets.Count == 0)
			{
				result = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Ready");
			}
			else if (this is SensorSystemECM)
			{
				result = ((_baseObject.NumberOfFreeJamChannels.Value <= 0) ? Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "AllChannelsUsed") : Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Jamming"));
			}
			else if (this is SensorSystemLaserDesignator)
			{
				result = ((_baseObject.NumberOfFreeLaserChannels.Value <= 0) ? Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "AllChannelsUsed") : Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Highlighting"));
			}
		}
		return result;
	}

	public void setTargetObject(ObjectBase targetObject)
	{
		_targetObject = targetObject;
	}

	public void SetPrelaunchObject(ObjectBase targetObject)
	{
		_preLaunchTargetObject = targetObject;
		foreach (SensorSystem targetTrackingSystem in _targetTrackingSystems)
		{
			if ((targetTrackingSystem.IsOn.Value || _wasSetOn) && targetTrackingSystem.HasFreeTargetChannels(targetObject))
			{
				if (!targetTrackingSystem._associatedTargets.Contains(targetObject))
				{
					targetTrackingSystem._associatedTargets.Add(targetObject);
				}
				targetTrackingSystem.setTargetObject(targetObject);
				targetTrackingSystem._isGuiding = true;
				break;
			}
		}
	}

	protected void AlignToTarget(ObjectBase targetObject)
	{
		if (!(targetObject == null))
		{
			Vector3 vector = targetObject.getUnityPosition() - _mountObject.transform.position;
			Vector3 direction = _mountObject.transform.InverseTransformDirection(vector);
			direction.y = 0f;
			Vector3 vector2 = _mountObject.transform.TransformDirection(direction);
			if (_mountRotatable)
			{
				RotateMount(vector2);
			}
			if (_sensorRotatable)
			{
				RotateSensor(vector2, vector);
			}
			if (_slaveMountObject != null)
			{
				Vector3 direction2 = _slaveMountObject.transform.InverseTransformDirection(vector);
				direction2.y = 0f;
				Vector3 targetDirection = _slaveMountObject.transform.TransformDirection(direction2);
				RotateSlaveMount(targetDirection);
			}
			if (_slaveRadarObject != null)
			{
				RotateSlaveSensor(vector2, vector);
			}
		}
	}

	protected void AlignToRestPosition()
	{
		Vector3 forward = _baseObject.getTransform().forward;
		Vector3 vector = Quaternion.AngleAxis(_restHorizontalAngle, Vector3.up) * forward;
		Vector3 forward2 = Quaternion.AngleAxis(0f - _restVerticalAngle, Vector3.right) * _mountObject.transform.forward;
		RotateMount(vector);
		RotateSensorToRestAngle();
		float num = Quaternion.Angle(Quaternion.LookRotation(vector, _mountObject.transform.up), _mountObject.transform.rotation);
		if (_slaveMountObject != null)
		{
			RotateSlaveMount(vector);
			float num2 = Quaternion.Angle(Quaternion.LookRotation(vector, _slaveMountObject.transform.up), _slaveMountObject.transform.rotation);
			if (num2 > num)
			{
				num = num2;
			}
		}
		float num3 = 0f;
		if (_sensorObject != null)
		{
			num3 = Quaternion.Angle(Quaternion.LookRotation(forward2, _sensorObject.transform.up), _sensorObject.transform.rotation);
			num3 -= _restVerticalAngle;
		}
		if (_slaveRadarObject != null)
		{
			RotateSlaveSensorToRestAngle();
			float num4 = Quaternion.Angle(Quaternion.LookRotation(forward2, _sensorObject.transform.up), _sensorObject.transform.rotation);
			num4 -= _restVerticalAngle;
			if (num4 > num3)
			{
				num3 = num4;
			}
		}
		if (num < 0.1f && num3 < 0.1f)
		{
			_restPositionReached = true;
		}
	}

	protected void RotateMount(Vector3 targetDirection)
	{
		if (!(_degreesPerSecond < 0.001f))
		{
			Quaternion quaternion = Quaternion.LookRotation(targetDirection, _mountObject.transform.up);
			float num = Mathf.Min(Quaternion.Angle(quaternion, _mountObject.transform.rotation), _degreesPerSecond);
			_mountObject.transform.rotation = Quaternion.RotateTowards(_mountObject.transform.rotation, quaternion, num * GameTime.deltaTime);
		}
	}

	protected void RotateSensor(Vector3 horizontalTargetDirection, Vector3 targetDirection)
	{
		if (!(_verticalDegreesPerSecond < 0.001f) && !(_sensorObject == null))
		{
			float num = Utils.WrapAngle(0f - Vector3.Angle(horizontalTargetDirection, targetDirection) - _sensorObject.transform.eulerAngles.x);
			float num2 = Mathf.Min(num, _verticalDegreesPerSecond);
			if (Mathf.Abs(num) > 0.5f)
			{
				_sensorObject.transform.Rotate(Vector3.right, num2 * GameTime.deltaTime);
			}
		}
	}

	protected void RotateSensorToRestAngle()
	{
		if (!(_sensorObject == null))
		{
			Vector3 forward = _sensorObject.transform.forward;
			forward.y = 0f;
			float num = Utils.WrapAngle(_restVerticalAngle - _sensorObject.transform.eulerAngles.x);
			float num2 = Mathf.Min(num, _verticalDegreesPerSecond);
			if (Mathf.Abs(num) > 0.09f)
			{
				_sensorObject.transform.Rotate(Vector3.right, num2 * GameTime.deltaTime);
			}
		}
	}

	private void RotateSlaveMount(Vector3 targetDirection)
	{
		if (!(_degreesPerSecond < 0.001f))
		{
			Debug.DrawRay(_slaveMountObject.transform.position, _mountObject.transform.up, Color.red, 0.3f);
			Debug.DrawRay(_slaveMountObject.transform.position, targetDirection, Color.green, 0.3f);
			Quaternion quaternion = Quaternion.LookRotation(targetDirection, _slaveMountObject.transform.up);
			float num = Mathf.Min(Quaternion.Angle(quaternion, _slaveMountObject.transform.rotation), _degreesPerSecond);
			_slaveMountObject.transform.rotation = Quaternion.RotateTowards(_slaveMountObject.transform.rotation, quaternion, num * GameTime.deltaTime);
		}
	}

	protected void RotateSlaveSensor(Vector3 horizontalTargetDirection, Vector3 targetDirection)
	{
		if (!(_verticalDegreesPerSecond < 0.001f))
		{
			float num = Utils.WrapAngle(0f - Vector3.Angle(horizontalTargetDirection, targetDirection) - _slaveRadarObject.transform.eulerAngles.x);
			float num2 = Mathf.Min(num, _verticalDegreesPerSecond);
			if (Mathf.Abs(num) > 0.5f)
			{
				_slaveRadarObject.transform.Rotate(Vector3.right, num2 * GameTime.deltaTime);
			}
		}
	}

	protected void RotateSlaveSensorToRestAngle()
	{
		Vector3 forward = _slaveRadarObject.transform.forward;
		forward.y = 0f;
		float num = Utils.WrapAngle(_restVerticalAngle - _slaveRadarObject.transform.eulerAngles.x);
		float num2 = Mathf.Min(num, _verticalDegreesPerSecond);
		if (Mathf.Abs(num) > 0.5f)
		{
			_slaveRadarObject.transform.Rotate(Vector3.right, num2 * GameTime.deltaTime);
		}
	}

	public override void SaveStateToFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.SaveStateToFile(ini, sectionName, isCampaignStateSave);
		if (!isCampaignStateSave)
		{
			ini.writeValue(sectionName, "State", _state.ToString());
			if (_requestEnable)
			{
				ini.writeValue(sectionName, "RequestEnable", value: true);
			}
			else if (_requestDisable)
			{
				ini.writeValue(sectionName, "RequestDisable", value: true);
			}
			if (!_allowTurnOn)
			{
				ini.writeValue(sectionName, "AllowTurnOn", value: false);
			}
		}
	}

	public override void LoadStateFromFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.LoadStateFromFile(ini, sectionName, isCampaignStateSave);
		if (!isCampaignStateSave)
		{
			if (base.IsOn.Value)
			{
				Enable();
			}
			else
			{
				Disable();
			}
			Enum.TryParse<State>(ini.readValue(sectionName, "State", "Idle"), out _state);
			_requestEnable = ini.readValue(sectionName, "RequestEnable", defaultValue: false);
			_requestDisable = ini.readValue(sectionName, "RequestDisable", defaultValue: false);
			_allowTurnOn = ini.readValue(sectionName, "AllowTurnOn", defaultValue: true);
			if (_requestDisable && _parentTowedSystem != null)
			{
				_wasSetOn = false;
			}
		}
	}

	public override void dispose()
	{
		base.dispose();
		Unity.Entities.World.DefaultGameObjectInjectionWorld?.EntityManager.DestroyEntity(InstanceEntity);
	}
}
