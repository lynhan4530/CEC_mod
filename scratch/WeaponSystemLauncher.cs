using System;
using System.Collections.Generic;
using System.Text;
using PathologicalGames;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Audio;

namespace SeaPower;

public class WeaponSystemLauncher : WeaponSystem
{
	public enum State
	{
		Idle,
		InitiateLoading,
		MoveToLoadPosition,
		MoveToRestPosition,
		CheckForLoadedAmmunition,
		WaitForCloseSystem,
		UnloadAmmunition,
		WaitForUnloadAnimation,
		LoadAmmunition,
		WaitForLoadAnimation
	}

	private class SharedVentGroup
	{
		public ObjectCodeAnimation _openAnimation;

		public ObjectCodeAnimation _closeAnimation;

		public List<WeaponContainer> _containers = new List<WeaponContainer>();

		public bool _isOpen;

		public bool _playingOpen;

		public bool _playingClose;
	}

	private static readonly ProfilerMarker _profSystem = new ProfilerMarker("System: WeaponSystemLauncher");

	private GameObject _smokeEffect;

	private List<int> _containersToClose = new List<int>();

	private Ammunition _spawnedAmmunition;

	private int _spawnedAmmunitionCount;

	private bool _triggerShutDown;

	private bool _isSleeping;

	private bool _isLaunchingSalvo;

	private int _currentShotInSalvo;

	private double _readyToLaunchTime;

	private bool _onRail;

	private double _onRailWarmupStartTime;

	private int _nextLoadContainer;

	private Ammunition _prepAmmo;

	private ObjectBase _prepTarget;

	private double _prepRequestedTime = -1000.0;

	public State _state;

	private bool _sameTypeTopUp;

	private double _blockedSinceTime = -1.0;

	private double _nextBearingHandoverCheckTime;

	public const int OUT_OF_AMMO = -2;

	private List<SharedVentGroup> _ventGroups;

	public WeaponSystemLauncher(IniHandler ini, string sectionName, ObjectBase hostObject, WeaponParameters vwp, ObjectBaseParameters obp)
	{
		if (hostObject == null)
		{
			Log.Error("Launcher weapon system [" + sectionName + "] in ini file '" + ini.getFileName() + "' has a null hostObject.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 62, ".ctor");
		}
		if (vwp == null)
		{
			Log.Error("Launcher weapon system [" + sectionName + "] in ini file '" + ini.getFileName() + "' has null WeaponParameters.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 63, ".ctor");
		}
		if (vwp._mountObject == null)
		{
			Log.Error("Launcher weapon system [" + sectionName + "] in ini file '" + ini.getFileName() + "' has null mountObject in WeaponParameters.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 64, ".ctor");
		}
		_baseObject = hostObject;
		_vwp = vwp;
		_mountObject = vwp._mountObject;
		_systemName = vwp._systemName;
		_sectionName = vwp._sectionName;
		if (obp._codeAnimations.TryGetValue(_vwp._openSystemAnimation, out var value))
		{
			_openSystemAnimation = new ObjectCodeAnimation(value);
		}
		if (obp._codeAnimations.TryGetValue(_vwp._closeSystemAnimation, out var value2))
		{
			_closeSystemAnimation = new ObjectCodeAnimation(value2);
		}
		if (obp._codeAnimations.TryGetValue(_vwp._unloadAmmunitionAnimation, out var value3))
		{
			_unloadAmmunitionAnimation = new ObjectCodeAnimation(value3);
		}
		if (obp._codeAnimations.TryGetValue(_vwp._loadAmmunitionAnimation, out var value4))
		{
			_loadAmmunitionAnimation = new ObjectCodeAnimation(value4);
		}
		_reloadableWithoutMagazine = ini.readValue(sectionName, "ReloadableWithoutMagazine", defaultValue: false);
		if (_vwp._mount == null)
		{
			_vwp._mount = new WeaponMount(_vwp);
		}
		if (ini.doesKeyExist(sectionName, "NumberOfContainers"))
		{
			int num = ini.readValue(sectionName, "NumberOfContainers", 1);
			int num2 = ini.readValue(sectionName, "NumberOfHatchesPerContainer", 0, performLogging: false);
			AudioClip audioClip = null;
			if (num2 > 0)
			{
				string text = ini.readValue(sectionName, "HatchAudioClip", "", performLogging: false);
				if (!string.IsNullOrEmpty(text))
				{
					audioClip = Singleton<ResourceLoader>.Instance.getAudioClipResource(text);
				}
			}
			Vector3 defaultValue = ini.readValue(sectionName, "ContainerDefault_Rotation", Vector3.zero, performLogging: false);
			for (int i = 1; i <= num; i++)
			{
				WeaponContainer weaponContainer = new WeaponContainer(hostObject, this, _vwp)
				{
					_containerIndex = i
				};
				string text2 = ini.readValue(sectionName, "Container" + i, "");
				bool flag = text2.ToLower() == "dummy";
				bool flag2 = !flag && obp._objectsDictionary.TryGetValue(text2, out weaponContainer._gunObject) && weaponContainer._gunObject != null;
				if (flag || !flag2)
				{
					if (!flag)
					{
						string text3 = ((hostObject != null) ? hostObject.getUIDAndName() : "<unknown object>");
						Log.Error($"{text3}: Could not find container object '{text2}' for [{sectionName}] container {i}. Using a fallback ammunition parent.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 115, ".ctor");
					}
					Vector3 localPosition = ini.readValue(sectionName, "Container" + i + "_Position", Vector3.zero);
					Vector3 localEulerAngles = ini.readValue(sectionName, "Container" + i + "_Rotation", defaultValue, performLogging: false);
					weaponContainer._gunObject = new GameObject("AmmunitionParent");
					GameObject gameObject = (ini.readValue(sectionName, $"Container{i}_AttachWeaponsToMount", defaultValue: false) ? vwp._mountObject : vwp._containerBaseObject);
					if (gameObject == null)
					{
						gameObject = ((vwp._mountObject != null) ? vwp._mountObject : hostObject?.gameObject);
					}
					if (gameObject != null)
					{
						weaponContainer._gunObject.transform.SetParent(gameObject.transform);
					}
					weaponContainer._gunObject.transform.localPosition = localPosition;
					weaponContainer._gunObject.transform.localEulerAngles = localEulerAngles;
				}
				if (num2 > 0)
				{
					string text4 = ini.readValue(sectionName, "Container" + i + "_Hatch1", "", performLogging: false);
					if (text4 == "FALSE")
					{
						text4 = "";
					}
					string text5 = ini.readValue(sectionName, "Container" + i + "_OpenHatchAnimation", "", performLogging: false);
					string text6 = ini.readValue(sectionName, "Container" + i + "_CloseHatchAnimation", "", performLogging: false);
					if (string.IsNullOrEmpty(text5) && text4.Length > 0)
					{
						text5 = text4 + "_Open";
					}
					if (string.IsNullOrEmpty(text6) && text4.Length > 0)
					{
						text6 = text4 + "_Close";
					}
					ObjectCodeAnimation value5 = null;
					ObjectCodeAnimation value6 = null;
					obp._codeAnimations.TryGetValue(text5, out value5);
					obp._codeAnimations.TryGetValue(text6, out value6);
					if (value5 != null)
					{
						weaponContainer._openAnimation = new ObjectCodeAnimation(value5);
					}
					if (value6 != null)
					{
						weaponContainer._closeAnimation = new ObjectCodeAnimation(value6);
					}
					for (int j = 1; j <= num2; j++)
					{
						string text7 = ini.readValue(sectionName, "Container" + i + "_Hatch" + j, "");
						if (text7 == "FALSE")
						{
							continue;
						}
						GameObject value7 = null;
						obp._objectsDictionary.TryGetValue(text7, out value7);
						if (value7 == null)
						{
							if (string.IsNullOrEmpty(text7))
							{
								text7 = "(search key = Container" + i + "_Hatch" + j + ")";
							}
							Log.Error("Could not find hatch object '" + text7 + "' in section '" + sectionName + "' in vessel file '" + ini.getFileName() + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 168, ".ctor");
						}
						else
						{
							weaponContainer.addHatch(value7);
						}
					}
					if (audioClip != null)
					{
						weaponContainer.addAudioSource();
						weaponContainer._hatchSoundClip = audioClip;
					}
				}
				_vwp._mount.addAttachment(weaponContainer);
				_containers.Add(weaponContainer);
			}
			Dictionary<string, SharedVentGroup> dictionary = null;
			for (int k = 1; k <= num && k <= _containers.Count; k++)
			{
				string text8 = ini.readValue(sectionName, "Container" + k + "_Vent", "", performLogging: false);
				if (string.IsNullOrEmpty(text8))
				{
					continue;
				}
				if (dictionary == null)
				{
					dictionary = new Dictionary<string, SharedVentGroup>();
				}
				if (!dictionary.TryGetValue(text8, out var value8))
				{
					obp._codeAnimations.TryGetValue(text8 + "_Open", out var value9);
					obp._codeAnimations.TryGetValue(text8 + "_Close", out var value10);
					if (value9 == null && value10 == null)
					{
						Log.Error("Launcher weapon system [" + sectionName + "] in ini file '" + ini.getFileName() + "': no '" + text8 + "_Open'/'" + text8 + "_Close' animation found for vent '" + text8 + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 202, ".ctor");
						value8 = null;
					}
					else
					{
						value8 = new SharedVentGroup
						{
							_openAnimation = ((value9 != null) ? new ObjectCodeAnimation(value9) : null),
							_closeAnimation = ((value10 != null) ? new ObjectCodeAnimation(value10) : null)
						};
					}
					dictionary.Add(text8, value8);
				}
				value8?._containers.Add(_containers[k - 1]);
			}
			if (dictionary == null)
			{
				return;
			}
			_ventGroups = new List<SharedVentGroup>();
			{
				foreach (SharedVentGroup value11 in dictionary.Values)
				{
					if (value11 != null)
					{
						_ventGroups.Add(value11);
					}
				}
				return;
			}
		}
		Log.Error("Launcher weapon system [" + sectionName + "] in ini file '" + ini.getFileName() + "' has no 'NumberOfContainers' entry.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 224, ".ctor");
	}

	public override void LoadFromInI(IniHandler ini, string sectionName, ObjectBaseParameters obp, string keyName = "")
	{
		base.LoadFromInI(ini, sectionName, obp, keyName);
		LoadBaseParameters(ini, sectionName, obp);
	}

	public override void init()
	{
		base.init();
		foreach (WeaponContainer container in _containers)
		{
			container.init(_baseObject._obp._launcherAmmunitionGroups, _sectionName);
		}
		_systemState = SystemState.SystemClosed;
		_hatchesState = HatchesState.HatchesClosed;
		_loadingState = LoadingState.Idle;
		_engageState = EngageState.Idle;
		_systemName = _vwp._systemName;
		_sectionName = _vwp._sectionName;
		_isLaunchingSalvo = false;
		_currentShotInSalvo = 0;
		_readyToLaunchTime = -1000.0;
		_nextLoadContainer = 0;
	}

	public void WarmUp()
	{
		if (_vwp._hasWarmUp && _vwp._hasWarmUpAmmo && !base.IsHot)
		{
			if (DM._disableWarmUp)
			{
				base.IsHot = true;
				return;
			}
			StartReload();
			base.IsHot = true;
		}
	}

	public override void OnUpdate()
	{
		base.OnUpdate();
		if (base.Inoperable.Value)
		{
			return;
		}
		_vwp._mount.update();
		updateAnimations();
		if (_vwp._perContainerReload)
		{
			updatePerContainerReload();
		}
		if (_isReloading)
		{
			return;
		}
		if (_executingEngageTask && _triggerShutDown && (_state == State.Idle || _state == State.MoveToLoadPosition))
		{
			_triggerShutDown = false;
			_state = State.Idle;
		}
		if (_executingEngageTask && GameTime.missionElapsedTime - _engageStartTime > (double)_engageDelay)
		{
			if (_isOffline)
			{
				DM.I(_baseObject.getUIDAndName() + ": <color=orange>Aborting engage task. System disabled.</color>");
				EndEngageTask();
				return;
			}
			_engageState = EngageState.CheckingAbort;
			if (_targetObject == null && _isTargetObjectRequired)
			{
				DM.I(_baseObject.getUIDAndName() + ": <color=orange>Aborting engage task. Target is null.</color>");
				EndEngageTask();
				return;
			}
			if (_targetObject != null)
			{
				if (_targetObject.GetMapVehicle(_baseObject._taskforce) != null)
				{
					_targetPosition = _targetObject.transform.position;
				}
				else
				{
					if (!_ignoreIfUndetected)
					{
						DM.I(_baseObject.getUIDAndName() + ": <color=orange>Aborting engage task.</color> " + _targetObject.getObjectIniName() + " - no longer detected");
						EndEngageTask();
						return;
					}
					_targetPosition = _targetObject.transform.position;
				}
				if (_targetObject._type == ObjectBase.ObjectType.LandUnit)
				{
					if (_baseObject.Aligning && _baseObject._alignmentTasks[0]._uid == _alignmentUID)
					{
						_currentTargetPoint = _baseObject._alignmentTasks[0]._currentTargetPoint;
					}
					_targetPosition = _targetObject.transform.TransformPoint(_currentTargetPoint._position);
				}
			}
			float num = Vector3.Distance(_baseObject.transform.position, _targetPosition);
			if (num <= _ammoForEngage._ap._launchRangesInUnity.x && _targetObject != null && _targetObject._type == ObjectBase.ObjectType.Missile)
			{
				DM.I(_baseObject.getUIDAndName() + ": <color=orange>Aborting engage task.</color> " + _targetObject.getObjectIniName() + " is already passed minimum range of " + _ammoForEngage._ap._displayedName);
				EndEngageTask();
				return;
			}
			if (!AcquisitionComplete(_engageStartTime))
			{
				if (_targetObject != null && _ammoForEngage != null && !_sensorPreLaunchAligned && !CheckForMissileGroup(_ammoForEngage._ap))
				{
					activatePreLaunchGuidance(_targetObject);
				}
				ReadyUpWhileWaiting();
				_engageState = EngageState.AligningLauncher;
				return;
			}
			bool flag = false;
			if (_baseObject is Submarine)
			{
				if ((DM._subAIAppliesToPlayer || !_baseObject.IsPlayerObject) && _ammoForEngage._ap._type == Ammunition.Type.Missile && _baseObject.transform.position.y < 0f - _ammoForEngage._ap._maxDepthUnity)
				{
					float x = _ammoForEngage._ap._launchRangesInUnity.x;
					float num2 = ((_targetObject != null) ? _ammoForEngage._ap.MaxRangeRough(_baseObject, _targetObject.transform.position, _targetObject._velocityVecInUnity, _ammoForEngage._ap.AssumeEvasiveTarget(_targetObject)) : float.MaxValue);
					if (num <= x)
					{
						_engageState = EngageState.TargetTooClose;
						return;
					}
					if (num > num2)
					{
						_engageState = EngageState.TargetTooFar;
						return;
					}
				}
				if (_baseObject.DesiredAltitude.Value < 0f - _ammoForEngage._ap._maxDepthUnity + 0.1f)
				{
					_baseObject.DesiredAltitude.Value = 0f - _ammoForEngage._ap._maxDepthUnity + 0.115f;
				}
				_engageState = EngageState.LauncherTooLow;
				if (_baseObject.transform.position.y < 0f - _ammoForEngage._ap._maxDepthUnity && (_ammoForEngage._ap._maxDepthUnity > 0f || _baseObject.isSubmerged()))
				{
					return;
				}
				if (_ammoForEngage._ap._minDepthUnity > 0f)
				{
					if (_baseObject.DesiredAltitude.Value > 0f - _ammoForEngage._ap._minDepthUnity - 0.1f)
					{
						_baseObject.DesiredAltitude.Value = 0f - _ammoForEngage._ap._minDepthUnity - 0.115f;
					}
					_engageState = EngageState.LauncherTooHigh;
					if (_baseObject.transform.position.y > 0f - _ammoForEngage._ap._minDepthUnity)
					{
						return;
					}
				}
			}
			if (_ammoForEngage._ap._requiresGuidance)
			{
				CheckExternalRadarGuidance();
				flag = CheckForMissileGroup(_ammoForEngage._ap);
				if (!flag)
				{
					if (_vwp._associatedSensors.Count < 1)
					{
						if (_vwp._externalGuidingSystems.Count == 0)
						{
							Log.Error(_ammoForEngage._ap._displayedName + " can not be launched without associated sensor. Add 'AssociatedSensor' to " + _vwp._sectionName, Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 467, "OnUpdate");
						}
						EndEngageTask();
						return;
					}
					if (!hasFreeWeaponChannel())
					{
						TryHandOverToBearingLauncher();
						ReadyUpWhileWaiting();
						return;
					}
				}
			}
			if (_targetObject != null && _ammoForEngage._ap.GuidanceTypeForTargetType(_targetObject._type) == AmmunitionParameters.GuidanceType.IRHoming && _ammoForEngage._ap._isRearAspectOnly && !Utils.isInRearAspect(_baseObject, _targetObject, _ammoForEngage._ap))
			{
				DM.Log(_baseObject.FullIDData() + ": Cancel engage of " + _targetObject.FullIDData() + ". Not in rear aspect of the target.", DM.EventLogFilters.EngageTask);
				Log.Debug(_baseObject.getUIDAndName() + ": <color=orange>Aborting engage task.</color> " + _ammoForEngage._ap._displayedName + " is rear aspect only and cannot be launched at current target " + _targetObject.getObjectIniName() + " at this moment", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 492, "OnUpdate");
				EndEngageTask();
				return;
			}
			_isSleeping = false;
			if (!flag && _targetObject != null && !_sensorPreLaunchAligned)
			{
				activatePreLaunchGuidance(_targetObject);
			}
			_engageState = EngageState.WarmingUp;
			if (_vwp._hasWarmUp && _ammoForEngage._ap._requiresWarmUp && !base.IsHot)
			{
				base.IsHot = true;
				if (!DM._disableWarmUp && !_vwp._perContainerReload)
				{
					StartReload();
					return;
				}
			}
			if (InterruptedByLaserGuidanceReadiness())
			{
				return;
			}
			if (GameTime.TimeCompression > Globals._weaponsTCLimit && _ammoForEngage._ap._subType != Ammunition.Type.Sonobuoy && _ammoForEngage._ap._subType != Ammunition.Type.Paratrooper && _ammoForEngage._ap._type != Ammunition.Type.Noisemaker)
			{
				GameTime.SetTimeCompression(Globals._weaponsTCLimit);
			}
			_currentMaxLaunchRange = _ammoForEngage._ap.EffectiveMaxLaunchRange(_baseObject);
			if (_targetObject != null)
			{
				_currentMaxLaunchRange = _ammoForEngage._ap.MaxRangeRough(_baseObject, _targetObject.transform.position, _targetObject._velocityVecInUnity, _ammoForEngage._ap.AssumeEvasiveTarget(_targetObject));
			}
			_currentMaxLaunchRange = ((_horizonRange > 0f && _horizonRange < _currentMaxLaunchRange) ? _horizonRange : _currentMaxLaunchRange);
			if (_weaponType != WeaponType.Noisemaker && _ammoForEngage._ap._subType != Ammunition.Type.Fueltank)
			{
				_engageState = EngageState.TargetTooClose;
				if (num <= _ammoForEngage._ap._launchRangesInUnity.x && (_weaponType != WeaponType.Torpedo || !_baseObject.IsAirUnit))
				{
					ReadyUpWhileWaiting();
					return;
				}
				_engageState = EngageState.TargetTooFar;
				if (_weaponType == WeaponType.Torpedo && _baseObject.IsAirUnit)
				{
					if (num >= 27.559496f)
					{
						return;
					}
				}
				else
				{
					if (_targetObject != null && !_baseObject.IsPlayerObject)
					{
						if (_targetObject is Aircraft || _targetObject is Helicopter)
						{
							_currentMaxLaunchRange *= 0.85f;
						}
						if (_weaponType == WeaponType.Torpedo)
						{
							if (_ammoForEngage._ap._guidanceType == AmmunitionParameters.GuidanceType.WakeHoming || (_ammoForEngage._ap._vesselAttackGuidanceType == AmmunitionParameters.GuidanceType.WakeHoming && _targetObject is Vessel) || (_targetObject is Submarine && _targetObject.transform.position.y > -0.1360713f))
							{
								_currentMaxLaunchRange *= 0.45f;
							}
							else
							{
								_currentMaxLaunchRange *= 0.8f;
							}
						}
						else
						{
							_currentMaxLaunchRange *= 0.95f;
						}
					}
					if (num > _currentMaxLaunchRange || num < _ammoForEngage._ap._launchRangesInUnity.x)
					{
						if (_targetObject != null && !_baseObject.IsPlayerObject && !_baseObject.IsAirUnit && (_targetObject.IsAirUnit || _targetObject is WeaponBase))
						{
							float num3 = ObjectBase.ClosingRateBetween(_baseObject, _targetObject);
							if (!(num > _currentMaxLaunchRange) || !(num3 > 0.001f) || !((num - _currentMaxLaunchRange) / num3 < 90f))
							{
								DM.I($"{_baseObject.getUIDAndName()}: <color=orange>Aborting engage task. Target has flown away.</color>:  {num} > {_currentMaxLaunchRange} || {num} < {_ammoForEngage._ap._launchRangesInUnity.x}");
								EndEngageTask();
							}
						}
						ReadyUpWhileWaiting();
						return;
					}
				}
				if (_ammoForEngage._ap.GuidanceTypeForTarget(_targetObject) == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
				{
					_engageState = EngageState.TargetIsNotDetectableByFCR;
					if (!CheckTargetIsDetectableByFCR(ObjectBase.HomeOnJamOptions(_baseObject, _targetObject, _ammoForEngage._ap)))
					{
						ReadyUpWhileWaiting();
						return;
					}
				}
				else if (_targetObject != null && _ammoForEngage._ap.RequiresFcrControl(_targetObject))
				{
					_engageState = EngageState.TargetIsNotDetectableByFCR;
					if (_baseObject.GetMaxRangeForFCR(_ammoForEngage, _targetObject).Range < num)
					{
						ReadyUpWhileWaiting();
						return;
					}
				}
			}
			float guidanceSystemPenalty = 0f;
			bool flag2 = false;
			if (_ammoForEngage._ap.GuidanceTypeForTarget(_targetObject) == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
			{
				flag2 = true;
			}
			if (_ammoForEngage._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
			{
				flag2 = true;
			}
			VisionToEngageMode requireVisionToEngage = Singleton<OptionsManager>.Instance.RequireVisionToEngage;
			bool flag3 = _vwp._associatedSearchSensors.Count > 0 && flag2;
			if (_targetObject != null && flag3)
			{
				bool flag4 = true;
				_engageState = EngageState.TargetIsNotDetectableBySearchSensor;
				if (flag3)
				{
					foreach (SensorSystem associatedSearchSensor in _vwp._associatedSearchSensors)
					{
						if (!associatedSearchSensor.Inoperable.Value)
						{
							if (!associatedSearchSensor.IsOn.Value)
							{
								flag4 = false;
							}
							if (associatedSearchSensor is SensorSystemRadar && !associatedSearchSensor.IsOn.Value && !associatedSearchSensor._wasSetOn)
							{
								associatedSearchSensor.Enable(allowMessage: true, turnOffAfterEngage: true);
							}
						}
					}
				}
				ObjectBase.SensorRangeOptions options = ObjectBase.HomeOnJamOptions(_baseObject, _targetObject, _ammoForEngage._ap);
				bool flag5;
				if (requireVisionToEngage != 0)
				{
					flag5 = IsVisionRequirementMet(_targetObject, out var _, options);
					CanSearchSensorsSeeTarget(_targetObject, out guidanceSystemPenalty, options);
				}
				else
				{
					flag5 = CanSearchSensorsSeeTarget(_targetObject, out guidanceSystemPenalty, options);
				}
				if (requireVisionToEngage != 0 && !flag5)
				{
					if (_asssociatedSensorsAllOnTime < 0.0 && flag4)
					{
						_asssociatedSensorsAllOnTime = GameTime.missionElapsedTime + 5.0;
					}
					if (GameTime.missionElapsedTime > _asssociatedSensorsAllOnTime)
					{
						EndEngageTask();
					}
					ReadyUpWhileWaiting();
					return;
				}
			}
			_engageState = EngageState.PrelaunchSensorAlignment;
			if (!_isTargetObjectRequired || !_ammoForEngage._ap._requiresGuidance)
			{
				_sensorPreLaunchAligned = true;
			}
			if (!flag && _isTargetObjectRequired && !isAimedAtObject(_targetObject))
			{
				_sensorPreLaunchAligned = false;
				ReadyUpWhileWaiting();
				return;
			}
			_engageState = EngageState.ChoosingContainer;
			int container = getContainer();
			if (container == -2)
			{
				DM.I(_baseObject.getUIDAndName() + ": <color=orange>Aborting engage task - out of " + _ammoForEngage._ap._displayedName + ".</color>");
				EngageTask takenEngageTask = _takenEngageTask;
				EndEngageTask();
				if (takenEngageTask != null && !_baseObject.AnyWeaponHasAmmo(takenEngageTask._ammoId))
				{
					_baseObject.RemoveEngageTask(takenEngageTask);
				}
			}
			else
			{
				if (container < 0)
				{
					return;
				}
				if (_baseObject is LandUnit landUnit && landUnit._isMobile && Mathf.Abs(_baseObject._velocityInKnots) > _vwp._maxVelocityForLandUnitKn)
				{
					if (_systemState == SystemState.OpeningSystem)
					{
						return;
					}
					if (_systemState != 0)
					{
						if (_systemState != SystemState.ClosingSystem)
						{
							for (int i = 0; i < _containers.Count; i++)
							{
								closeHatches(i, 0f);
							}
							closeSystem(0f);
						}
						return;
					}
					if (_isInRestPosition)
					{
						return;
					}
					if (moveToRestPosition())
					{
						_isInRestPosition = true;
						_restPositionReachedTime = GameTime.missionElapsedTime;
					}
				}
				_engageState = EngageState.OpeningSystem;
				if (!openSystem())
				{
					return;
				}
				if (_ammoForEngage._ap._launcherPositions.ContainsKey(_systemName))
				{
					if (!_onRail)
					{
						_onRail = true;
						_onRailWarmupStartTime = GameTime.missionElapsedTime;
					}
					_engageState = EngageState.OnRailWarmup;
					if (GameTime.missionElapsedTime - _onRailWarmupStartTime < (double)_ammoForEngage._ap._launcherPositions[_systemName]._onRailWarmup)
					{
						return;
					}
				}
				_engageState = EngageState.SpeedTooLow;
				if (_baseObject._velocityInKnots < _vwp._minSpeedForLaunnchKnots)
				{
					return;
				}
				if (_weaponType != WeaponType.Noisemaker && _ammoForEngage._ap._subType != Ammunition.Type.Fueltank)
				{
					_engageState = EngageState.TargetIsNotInFiringArcs;
					if (!targetIsInFiringArc(_targetPosition))
					{
						TryHandOverToBearingLauncher();
						if (_executingEngageTask)
						{
							alignToTarget(ClosestReachableAimPosition(_targetPosition), _vwp._fixVerticalLaunchAngleForLauncher, container);
						}
						return;
					}
					_engageState = EngageState.AligningLauncher;
					if (!alignToTarget(_targetPosition, _vwp._fixVerticalLaunchAngleForLauncher, container))
					{
						return;
					}
				}
				_engageState = EngageState.OpeningHatches;
				if (!openHatches(container))
				{
					return;
				}
				bool flag6 = _baseObject._sharedLaunchIntervals.ContainsKey(_systemName);
				if (!_isLaunchingSalvo)
				{
					if (_weaponType == WeaponType.Torpedo && GameTime.missionElapsedTime - _baseObject._torpedoLastLaunchTime < (double)_baseObject._torpedoLaunchDelay)
					{
						return;
					}
					_engageState = EngageState.PreLaunchDelay;
					if (GameTime.missionElapsedTime - _readyToLaunchTime < (double)_vwp._preLaunchDelay)
					{
						return;
					}
					_engageState = EngageState.FirerateDelay;
					if (GameTime.missionElapsedTime - _lastLaunchTime < (double)(60f / (float)_vwp._fireRatePerMinute))
					{
						return;
					}
					_engageState = EngageState.SharedLaunchDelay;
					if (flag6 && GameTime.missionElapsedTime - _baseObject._sharedLaunchTimers[_systemName] < (double)_baseObject._sharedLaunchIntervals[_systemName])
					{
						return;
					}
				}
				if (Globals._preventFriendlyFire && _targetObject != null && _targetObject is Missile incomingMissile && Utils.IsDangerousToLaunch(incomingMissile, _baseObject, _ammoForEngage._ap, -1f, 4f, logInfo: false, null, "Launch"))
				{
					DM.I(_baseObject.getUIDAndName() + ": <color=orange>Aborting engage task. Target missile is danger close.</color>");
					DM.Log(_baseObject.getUIDAndName() + ": <color=orange>Aborting engage task on " + _targetObject.getUIDAndName() + ". Target missile is danger close.</color>", DM.EventLogFilters.General);
					EndEngageTask();
					return;
				}
				_cantInterruptEngage = true;
				if (_weaponType == WeaponType.RBU)
				{
					_engageState = EngageState.LaunchingSalvo;
					_isLaunchingSalvo = true;
					if (_vwp._salvoFireTargetCircleRadius > 0f)
					{
						float num4 = MathF.PI * 2f * (float)_currentShotInSalvo / (float)_vwp._salvoFireAmount;
						if (_vwp._invertedPattern)
						{
							num4 += MathF.PI;
						}
						_offsetVector.x = _vwp._salvoFireTargetCircleRadius * Mathf.Sin(num4) * 0.0148809375f;
						_offsetVector.z = _vwp._salvoFireTargetCircleRadius * Mathf.Cos(num4) * 0.0148809375f;
					}
					if (GameTime.missionElapsedTime - _lastLaunchTime < (double)_vwp._salvoFireTime)
					{
						return;
					}
					launch(container, _targetObject, _targetPosition, _offsetVector, guidanceSystemPenalty);
					_currentShotInSalvo++;
					_lastLaunchTime = GameTime.missionElapsedTime;
					if (flag6)
					{
						_baseObject._sharedLaunchTimers[_systemName] = GameTime.missionElapsedTime;
					}
					if (_containers[container]._weapons.Count > 0)
					{
						return;
					}
					_currentShotInSalvo = 0;
					triggerLoadSystem();
				}
				else
				{
					if (_vwp._salvoFireAmount > 1)
					{
						_engageState = EngageState.LaunchingSalvo;
						_isLaunchingSalvo = true;
						if (_vwp._salvoFireTargetCircleRadius > 0f)
						{
							float num5 = MathF.PI * 2f * (float)_currentShotInSalvo / (float)_vwp._salvoFireAmount;
							if (_vwp._invertedPattern)
							{
								num5 += MathF.PI;
							}
							_offsetVector.x = _vwp._salvoFireTargetCircleRadius * Mathf.Sin(num5) * 0.0148809375f;
							_offsetVector.z = _vwp._salvoFireTargetCircleRadius * Mathf.Cos(num5) * 0.0148809375f;
						}
						if (GameTime.missionElapsedTime - _lastLaunchTime < (double)_vwp._salvoFireTime)
						{
							return;
						}
						launch(container, _targetObject, _targetPosition, _offsetVector, guidanceSystemPenalty);
						_currentShotInSalvo++;
						_lastLaunchTime = GameTime.missionElapsedTime;
						if (flag6)
						{
							_baseObject._sharedLaunchTimers[_systemName] = GameTime.missionElapsedTime;
						}
						if (_currentShotInSalvo < _vwp._salvoFireAmount && getLoadedAmmoCount(_ammoForEngage._ap._ammunitionFileName) > 0)
						{
							return;
						}
					}
					else
					{
						DM.I($"_offsetVector = {_offsetVector}");
						_engageState = EngageState.Launch;
						launch(container, _targetObject, _targetPosition, _offsetVector, guidanceSystemPenalty);
						if (_weaponType == WeaponType.Noisemaker && _baseObject == Singleton<RenderPosition>.Instance.getSelectedObject())
						{
							string text = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "DropNoisemaker");
							Globals._mainGameViewModel.RaiseAlert(_baseObject, text);
							Singleton<VoiceMessage>.Instance.PlayVoiceMessage("DecoyOut", _baseObject);
						}
					}
					if (_weaponType == WeaponType.Torpedo)
					{
						_baseObject._torpedoLastLaunchTime = GameTime.missionElapsedTime;
					}
				}
				_onRail = false;
				_blockedSinceTime = -1.0;
				_offsetVector = Vector3.zero;
				if (flag6)
				{
					_baseObject._sharedLaunchTimers[_systemName] = GameTime.missionElapsedTime;
				}
				bool flag7 = false;
				if (_ammoForEngage != null && _baseObject?._currentEngageTasks != null)
				{
					foreach (EngageTask currentEngageTask in _baseObject._currentEngageTasks)
					{
						if (!(currentEngageTask?._targetObject == null) && !currentEngageTask._targetObject.IsDestroyed && !(currentEngageTask._ammoId != _ammoForEngage._ap._ammunitionFileName))
						{
							flag7 = true;
							break;
						}
					}
				}
				if (!flag7)
				{
					_engageState = EngageState.ClosingHatches;
					closeHatches(container, 3f);
				}
				if (_weaponType == WeaponType.Torpedo && _containers.Count == 1 && _containers[0]._attachmentPositions.Count == 1 && !_vwp._isMountRotatable)
				{
					closeSystem(3f);
				}
				DM.I(_baseObject.getUIDAndName() + ": <color=green>Engage task complete</color>");
				EndEngageTask();
				_lastLaunchTime = GameTime.missionElapsedTime;
				_isLaunchingSalvo = false;
				_currentShotInSalvo = 0;
			}
		}
		else
		{
			if (UpdateSilentPrep() || _isSleeping || !(GameTime.missionElapsedTime - _lastLaunchTime > (double)_vwp._moveToRestPositionTime) || !_isInRestPosition || _systemState == SystemState.OpeningSystem)
			{
				return;
			}
			if (_systemState != 0)
			{
				if (_systemState != SystemState.ClosingSystem)
				{
					for (int j = 0; j < _containers.Count; j++)
					{
						closeHatches(j, 0f);
					}
					closeSystem(0f);
				}
			}
			else
			{
				if (!_vwp._hasPreloadedAmmunition && _ammunitionOnWeapon != null && _vwp._magazineReloadTime > 0f && _state == State.Idle && !IsTubeHoldingWire())
				{
					_triggerShutDown = true;
				}
				_isSleeping = true;
			}
		}
	}

	public void RequestPrep(ObjectBase target, Ammunition ammo)
	{
		if (!_executingEngageTask && !(target == null) && ammo != null)
		{
			_prepTarget = target;
			_prepAmmo = ammo;
			_prepRequestedTime = GameTime.missionElapsedTime;
			_lastUsageTime = GameTime.missionElapsedTime;
		}
	}

	private bool UpdateSilentPrep()
	{
		if (_executingEngageTask || _prepAmmo == null || _prepTarget == null)
		{
			ClearPrep();
			return false;
		}
		if (GameTime.missionElapsedTime - _prepRequestedTime > 2.0 || _prepTarget.IsDestroyed)
		{
			ClearPrep();
			return false;
		}
		_isSleeping = false;
		_isInRestPosition = false;
		_lastUsageTime = GameTime.missionElapsedTime;
		_ammoForEngage = _prepAmmo;
		if (_vwp._hasWarmUp && _prepAmmo._ap._requiresWarmUp && !base.IsHot)
		{
			base.IsHot = true;
			if (!DM._disableWarmUp && !_vwp._perContainerReload)
			{
				StartReload();
				return true;
			}
		}
		if (_isReloading)
		{
			return true;
		}
		if (TryTopUpRails(_prepAmmo))
		{
			return true;
		}
		int container = getContainer();
		if (container == -2)
		{
			ClearPrep();
			return false;
		}
		if (container < 0)
		{
			return true;
		}
		if (!openSystem())
		{
			return true;
		}
		if (_weaponType != WeaponType.Noisemaker && _prepAmmo._ap._subType != Ammunition.Type.Fueltank)
		{
			Vector3 position = _prepTarget.transform.position;
			if (targetIsInFiringArc(position))
			{
				alignToTarget(position, _vwp._fixVerticalLaunchAngleForLauncher, container);
			}
		}
		return true;
	}

	private void ReadyUpWhileWaiting()
	{
		if (!_executingEngageTask || _ammoForEngage == null || _ammoForEngage._ap._type != Ammunition.Type.Missile || _baseObject.IsAirUnit || (_baseObject is LandUnit landUnit && landUnit._isMobile && Mathf.Abs(_baseObject._velocityInKnots) > _vwp._maxVelocityForLandUnitKn) || _isReloading)
		{
			return;
		}
		if (_blockedSinceTime < 0.0)
		{
			_blockedSinceTime = GameTime.missionElapsedTime;
		}
		if (GameTime.missionElapsedTime - _blockedSinceTime > 5.0 && TryTopUpRails(_ammoForEngage))
		{
			return;
		}
		int container = getContainer();
		if (container >= 0 && openSystem())
		{
			if (!_onRail && _ammoForEngage._ap._launcherPositions.ContainsKey(_systemName))
			{
				_onRail = true;
				_onRailWarmupStartTime = GameTime.missionElapsedTime;
			}
			alignToTarget(ClosestReachableAimPosition(_targetPosition), _vwp._fixVerticalLaunchAngleForLauncher, container);
		}
	}

	private int FreeRailSpaceFor(Ammunition ammo)
	{
		int num = 0;
		foreach (WeaponContainer container in _containers)
		{
			num += container.LoadableCount(ammo);
		}
		return num;
	}

	private bool TryTopUpRails(Ammunition ammo)
	{
		if (ammo == null)
		{
			return false;
		}
		if (IsTubeHoldingWire())
		{
			return false;
		}
		if (_vwp._perContainerReload || _vwp._associatedMagazine == null || _state != 0)
		{
			return false;
		}
		if (GameTime.missionElapsedTime - _lastLaunchTime <= (double)Mathf.Min(_vwp._magazineReloadTime, 5f))
		{
			return false;
		}
		if (_ammunitionOnWeapon == null || _ammunitionOnWeapon._ap._ammunitionFileName != ammo._ap._ammunitionFileName)
		{
			return false;
		}
		if (getMagazineAmmoCount(ammo._ap._ammunitionFileName) <= 0)
		{
			return false;
		}
		int num = FreeRailSpaceFor(ammo);
		if (num <= 0)
		{
			return false;
		}
		if (getNumberOfAmmunitionInAllContainers() + num > 2)
		{
			return false;
		}
		if (_vwp._magazineReloadTime >= 60f)
		{
			return false;
		}
		_sameTypeTopUp = true;
		StartReload();
		triggerLoadSystem();
		return true;
	}

	private Vector3 ClosestReachableAimPosition(Vector3 targetPosition)
	{
		if (_vwp._firingArcs.Count == 0 || _vwp._firingArcs[0] == Vector2.zero)
		{
			return targetPosition;
		}
		if (targetIsInFiringArc(targetPosition))
		{
			return targetPosition;
		}
		Vector3 vector = targetPosition - _baseObject.transform.position;
		float num = Utils.WrapAngle(Mathf.Atan2(vector.x, vector.z) * 57.29578f - _baseObject.transform.eulerAngles.y);
		float num2 = float.MaxValue;
		float num3 = num;
		foreach (Vector2 firingArc in _vwp._firingArcs)
		{
			if (!(firingArc == Vector2.zero))
			{
				float num4 = firingArc.y - firingArc.x;
				if (num4 < 0f)
				{
					num4 += 360f;
				}
				float num5 = Mathf.Min(1f, num4 / 4f);
				float num6 = Mathf.Abs(Mathf.DeltaAngle(num, firingArc.x));
				float num7 = Mathf.Abs(Mathf.DeltaAngle(num, firingArc.y));
				if (num6 < num2)
				{
					num2 = num6;
					num3 = firingArc.x + num5;
				}
				if (num7 < num2)
				{
					num2 = num7;
					num3 = firingArc.y - num5;
				}
			}
		}
		float f = (_baseObject.transform.eulerAngles.y + num3) * (MathF.PI / 180f);
		float magnitude = new Vector2(vector.x, vector.z).magnitude;
		Vector3 result = _baseObject.transform.position + new Vector3(Mathf.Sin(f) * magnitude, 0f, Mathf.Cos(f) * magnitude);
		result.y = targetPosition.y;
		return result;
	}

	private void TryHandOverToBearingLauncher()
	{
		if ((!(_baseObject is Vessel) && !(_baseObject is Submarine)) || _ammoForEngage == null || _ammoForEngage._ap._type != Ammunition.Type.Missile || _vwp._linkedFire || _cantInterruptEngage || _isLaunchingSalvo || GameTime.missionElapsedTime < _nextBearingHandoverCheckTime)
		{
			return;
		}
		_nextBearingHandoverCheckTime = GameTime.missionElapsedTime + 1.0;
		List<WeaponSystem> weaponSystemsForAmmunition = _baseObject.GetWeaponSystemsForAmmunition(_ammoForEngage._ap._ammunitionFileName);
		if (weaponSystemsForAmmunition == null)
		{
			return;
		}
		foreach (WeaponSystem item in weaponSystemsForAmmunition)
		{
			if (item == this || !(item is WeaponSystemLauncher) || item._vwp._linkedFire || item._isOffline || !item.targetIsInFiringArc(_targetPosition) || !WeaponSelector.isWeaponSystemUsable(item, _ammoForEngage, _targetObject) || item.getLoadedAmmoCount(_ammoForEngage._ap._ammunitionFileName) < 1)
			{
				continue;
			}
			bool flag = false;
			foreach (WeaponSystem sharesMountWithSystem in item._vwp._sharesMountWithSystems)
			{
				if (sharesMountWithSystem._executingEngageTask && (!(sharesMountWithSystem._targetObject != null) || !(sharesMountWithSystem._targetObject == _targetObject)))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				DM.I(_baseObject.getUIDAndName() + ": " + _vwp._sectionName + " hands engage task over to already-bearing launcher " + item._vwp._sectionName);
				DM.Log(_baseObject.getUIDAndName() + ": " + _vwp._sectionName + " hands engage task over to already-bearing launcher " + item._vwp._sectionName, DM.EventLogFilters.EngageTask);
				item._takenEngageTask = _takenEngageTask;
				item._executingEngageTask = true;
				item._ignoreIfUndetected = _ignoreIfUndetected;
				item._engageTaskPriority = _engageTaskPriority;
				item._ammoForEngage = _ammoForEngage;
				item._isTargetObjectRequired = _isTargetObjectRequired;
				item._alignmentUID = _alignmentUID;
				item._engageStartTime = ((_takenEngageTask != null && _takenEngageTask._solutionStartTime >= 0.0) ? _takenEngageTask._solutionStartTime : _engageStartTime);
				item._engageDelay = (float)Globals._rnd.NextDouble() * item._vwp._maxReactiontime;
				item._targetObject = _targetObject;
				item._currentTargetPoint = _currentTargetPoint;
				item._targetPosition = _targetPosition;
				item._isAutoEngaging = _isAutoEngaging;
				item._offsetVector = _offsetVector;
				EndEngageTask(endAlignment: false);
				break;
			}
		}
	}

	private void ClearPrep()
	{
		if (_prepAmmo != null || !(_prepTarget == null))
		{
			_prepAmmo = null;
			_prepTarget = null;
		}
	}

	private int getContainer()
	{
		if (_ammoForEngage == null)
		{
			return -1;
		}
		if (IsTubeHoldingWire())
		{
			return -1;
		}
		if (_vwp._perContainerReload)
		{
			if (_vwp._associatedMagazine == null)
			{
				return -2;
			}
			if (_vwp._associatedMagazine.getAmmunitionCount(_ammoForEngage._ap._ammunitionFileName) <= 0)
			{
				return -2;
			}
			for (int i = 0; i < _containers.Count; i++)
			{
				int num = (_nextLoadContainer + i) % _containers.Count;
				if (_containers[num].HasReadyCell())
				{
					return num;
				}
			}
			return -1;
		}
		if (_vwp._associatedMagazine == null)
		{
			for (int j = 0; j < _containers.Count; j++)
			{
				if (_containers[j].HasAmmo(_ammoForEngage._ap._ammunitionFileName))
				{
					return j;
				}
			}
			return -2;
		}
		if (_ammunitionOnWeapon == null)
		{
			if (_state == State.Idle && getMagazineAmmoCount(_ammoForEngage._ap._ammunitionFileName) <= 0 && getNumberOfAmmunitionInAllContainers() == 0)
			{
				return -2;
			}
			triggerLoadSystem();
			return -1;
		}
		if (_ammunitionOnWeapon._ap._ammunitionFileName != _ammoForEngage._ap._ammunitionFileName)
		{
			triggerLoadSystem();
			return -1;
		}
		for (int k = 0; k < _containers.Count; k++)
		{
			int num2 = (_nextLoadContainer + k) % _containers.Count;
			WeaponContainer containerByIndex = getContainerByIndex(num2);
			if (containerByIndex._partialAmmoName == _ammoForEngage._ap._ammunitionFileName && containerByIndex.getNumberOfWeapons() > 0)
			{
				return num2;
			}
		}
		for (int l = 0; l < _containers.Count; l++)
		{
			int num3 = (_nextLoadContainer + l) % _containers.Count;
			if (getContainerByIndex(num3).HasAmmo(_ammoForEngage._ap._ammunitionFileName))
			{
				return num3;
			}
		}
		if (_vwp._loadingType == LoadingType.SpawnWhenNeeded)
		{
			return _nextLoadContainer % _containers.Count;
		}
		if (getMagazineAmmoCount(_ammoForEngage._ap._ammunitionFileName) > 0)
		{
			triggerLoadSystem();
			return -1;
		}
		return -2;
	}

	private void updatePerContainerReload()
	{
		foreach (WeaponContainer container in _containers)
		{
			container.EnsureCellState();
			for (int i = 0; i < container._cellReloadEnd.Count; i++)
			{
				if (container._cellReloadEnd[i] > 0.0 && GameTime.missionElapsedTime >= container._cellReloadEnd[i])
				{
					container._cellReloadEnd[i] = 0.0;
				}
			}
			container.EnsureSlotStatus();
			for (int j = 0; j < container.SlotStatus.Count; j++)
			{
				double num = ((j < container._cellReloadEnd.Count) ? container._cellReloadEnd[j] : 0.0);
				if (num <= 0.0)
				{
					container.SlotStatus[j].Value = "Ready";
					continue;
				}
				float num2 = Mathf.Max(0f, (float)(num - GameTime.missionElapsedTime));
				float num3 = Mathf.Floor(num2 / 60f);
				container.SlotStatus[j].Value = $"{num3:00}:{num2 - num3 * 60f:00}";
			}
		}
	}

	public new bool openSystem()
	{
		if (_isPlayingCloseAnim | _isPlayingUnloadAnim | _isPlayingLoadAnim)
		{
			return false;
		}
		if (_openSystemAnimation == null || _systemState == SystemState.SystemOpen)
		{
			_systemState = SystemState.SystemOpen;
			return true;
		}
		if (_isPlayingOpenAnim)
		{
			return false;
		}
		_systemState = SystemState.OpeningSystem;
		_openSystemAnimation.playAnim();
		_isPlayingOpenAnim = true;
		return false;
	}

	protected override double AcquisitionClockStart()
	{
		if (!_executingEngageTask)
		{
			return -1.0;
		}
		return _engageStartTime;
	}

	public override bool alignToTarget(Vector3 targetPosition, bool useFixedLaunchAngle, int containerIndexToUse, bool preciseAim = false)
	{
		_isInRestPosition = false;
		_lastUsageTime = GameTime.missionElapsedTime;
		if (!_vwp._isMountRotatable && !_vwp._areContainersRotatable)
		{
			return true;
		}
		if (_vwp._isMountRotatable)
		{
			if (_vwp._useLaunchAngle)
			{
				return RotateToFixedAngles(_vwp._launchAngle, useFixedLaunchAngle, containerIndexToUse, preciseAim);
			}
			if (_vwp._preferredArcs.Count > 0 && _vwp._preferredArcs[0] != Vector2.zero && !targetIsInPreferedArc(targetPosition))
			{
				float num = Utils.WrapAngle(0f - Utils.calculateYawToTarget(_baseObject.transform.forward, _baseObject.transform.position, targetPosition));
				float num2 = 1000f;
				int index = 0;
				int num3 = 0;
				bool flag = false;
				foreach (Vector2 preferredArc in _vwp._preferredArcs)
				{
					float num4 = Mathf.Abs(preferredArc.x - num);
					if (num2 > num4)
					{
						num2 = num4;
						index = num3;
						flag = true;
					}
					num4 = Mathf.Abs(preferredArc.y - num);
					if (num2 > num4)
					{
						num2 = num4;
						index = num3;
						flag = false;
					}
					num3++;
				}
				float launchAngle = (flag ? _vwp._preferredArcs[index].x : _vwp._preferredArcs[index].y);
				return RotateToFixedAngles(launchAngle, useFixedLaunchAngle, containerIndexToUse, preciseAim);
			}
		}
		return base.alignToTarget(targetPosition, useFixedLaunchAngle, containerIndexToUse);
	}

	public bool RotateToFixedAngles(float launchAngle, bool useFixedLaunchAngle, int containerIndexToUse, bool preciseAim = false)
	{
		_isInRestPosition = false;
		_lastUsageTime = GameTime.missionElapsedTime;
		Transform transform = _vwp._mountObject.transform;
		Vector3 forward = _baseObject.getTransform().forward;
		Vector3 vector = Quaternion.AngleAxis(launchAngle, Vector3.up) * forward;
		Vector3 vector2 = vector;
		vector2.y = 0f;
		_vwp._mount.rotate(vector2, _vwp._horizontalDegreesPerSecond, preciseAim: true);
		Quaternion a = Quaternion.LookRotation(vector2, Vector3.up);
		Vector3 forward2 = transform.forward;
		forward2.y = 0f;
		Quaternion b = Quaternion.LookRotation(forward2, Vector3.up);
		float num = Quaternion.Angle(a, b);
		_alignmentDeltas = $"({num}/0.5 | 0/0)";
		bool num2 = num < 0.5f;
		bool flag;
		if (_vwp._areContainersRotatable)
		{
			float num3 = Mathf.Asin(transform.forward.y) * 57.29578f;
			float num4 = (useFixedLaunchAngle ? (_vwp._fixVerticalLaunchAngle - num3) : (Mathf.Asin(vector.normalized.y) * 57.29578f - num3));
			if (num4 < _vwp._elevationArc.x)
			{
				num4 = _vwp._elevationArc.x;
			}
			if (num4 > _vwp._elevationArc.y)
			{
				num4 = _vwp._elevationArc.y;
			}
			num4 = 0f - num4;
			flag = ((!_vwp._areContainersJoinedTogether) ? RotateWeaponToAngle(_containers[containerIndexToUse]._gunObject.transform, num4, horizontalRotation: false, preciseAim) : RotateWeaponToAngle(_vwp._containerBaseObject.transform, num4, horizontalRotation: false, preciseAim));
		}
		else
		{
			_alignmentDeltas += "| 0/0)";
			flag = true;
		}
		return num2 && flag;
	}

	public bool openHatches(int containerId)
	{
		if (_containers[containerId]._closeAnimation != null && _containers[containerId]._playCloseAnimation)
		{
			return false;
		}
		if (!_containers[containerId]._areHatchesOpen)
		{
			_readyToLaunchTime = GameTime.missionElapsedTime;
		}
		if (_containers[containerId]._openAnimation == null || _containers[containerId]._areHatchesOpen)
		{
			_containers[containerId]._areHatchesOpen = true;
			return true;
		}
		if (_containers[containerId]._openAnimation != null)
		{
			if (!_containers[containerId]._playOpenAnimation)
			{
				_containers[containerId]._openAnimation.playAnim();
				_containers[containerId]._playOpenAnimation = true;
				if (_containers[containerId]._hatchSoundClip != null)
				{
					_containers[containerId]._audioSource.clip = _containers[containerId]._hatchSoundClip;
					_containers[containerId]._audioSource.Stop();
					_containers[containerId]._audioSource.Play();
				}
			}
			else
			{
				_containers[containerId].update();
			}
		}
		return false;
	}

	public void launch(int containerIdx, ObjectBase targetObject, Vector3 targetPosition, Vector3 offsetVector, float guidanceSystemPenalty)
	{
		if (containerIdx >= _containers.Count)
		{
			return;
		}
		if (_baseObject is Submarine submarine && submarine._pendingClearDatum)
		{
			Ammunition ammoForEngage = _ammoForEngage;
			if (ammoForEngage != null && ammoForEngage._ap?._type == Ammunition.Type.Missile)
			{
				submarine._clearDatumSawMissileTask = true;
				submarine._lastCruiseLaunchTime = GameTime.missionElapsedTime;
			}
		}
		if (_vwp._vidIncrease > 0f && _baseObject?._sensorData != null && _baseObject._sensorData._vidIncrease < _vwp._vidIncrease + 0.001f)
		{
			_baseObject._sensorData._vidIncrease = _vwp._vidIncrease;
			_baseObject._sensorData._vidIncreaseEndTime = GameTime.missionElapsedTime + (double)_vwp._vidIncreaseInterval;
		}
		if (_vwp._perContainerReload)
		{
			_containers[containerIdx].LaunchFromMagazine(_ammoForEngage?._ap?._ammunitionFileName, targetObject, targetPosition, offsetVector, guidanceSystemPenalty, _vwp._magazineReloadTime);
		}
		else
		{
			_containers[containerIdx].launch(targetObject, targetPosition, offsetVector, guidanceSystemPenalty, _ammoForEngage?._ap?._ammunitionFileName);
		}
		if (_containers[containerIdx].getNumberOfWeapons() == 0)
		{
			_nextLoadContainer = (containerIdx + 1) % _containers.Count;
		}
		else
		{
			_nextLoadContainer = containerIdx;
		}
		Transform parent = ((!_vwp._smokeEffectDirectional) ? _vwp._mountObject.transform : _containers[containerIdx]._gunObject.transform);
		if (_vwp._smokeEffect != null)
		{
			_smokeEffect = PathologicalGames.PoolManager.Pools["Effects"].Spawn(_vwp._smokeEffect, parent).gameObject;
			_smokeEffect.transform.localPosition = _vwp._smokeEffectPosition;
			_smokeEffect.transform.localRotation = Quaternion.identity;
			_smokeEffect.transform.SetParent(_baseObject.transform);
			Singleton<EffectsManager>.Instance._spawnedEffects.Add(_smokeEffect);
			ParticleUtils.SetAutoDestroyAndCustomWorldSpace(_smokeEffect);
		}
		if (_vwp._dustEffectObject != null)
		{
			GameObject gameObject = null;
			gameObject = PathologicalGames.PoolManager.Pools["Effects"].Spawn(_vwp._dustEffectObject, _mountObject.transform).gameObject;
			gameObject.transform.position = _mountObject.transform.position;
			gameObject.transform.localPosition = _vwp._dustEffectPosition;
			gameObject.transform.localEulerAngles = _vwp._dustEffectRotation;
			Singleton<EffectsManager>.Instance._spawnedEffects.Add(gameObject);
			gameObject.transform.SetParent(_baseObject.transform);
			ParticleUtils.SetAutoDestroyAndCustomWorldSpace(gameObject);
			AudioSource component = gameObject.GetComponent<AudioSource>();
			if (component != null)
			{
				AudioMixerGroup outputAudioMixerGroup = Singleton<EnvironmentAudioManager>.Instance.getMixer().FindMatchingGroups("Sfx")[0];
				component.outputAudioMixerGroup = outputAudioMixerGroup;
			}
		}
		if (!_vwp._perContainerReload && getNumberOfAmmunitionInAllContainers() == 0)
		{
			_ammunitionOnWeapon = null;
			StartReload();
			if (_vwp._loadingType == LoadingType.ReloadsLowerDeck)
			{
				triggerLoadSystem();
			}
		}
	}

	public void testLaunchWeapon(int containerIdx)
	{
		_containers[containerIdx].testLaunch();
		if (_vwp._smokeEffect != null)
		{
			_smokeEffect = PathologicalGames.PoolManager.Pools["Effects"].Spawn(_vwp._smokeEffect, _vwp._mountObject.transform).gameObject;
			_smokeEffect.transform.localPosition = _vwp._smokeEffectPosition;
			_smokeEffect.transform.localRotation = Quaternion.identity;
			_smokeEffect.transform.SetParent(_baseObject.transform);
			Singleton<EffectsManager>.Instance._spawnedEffects.Add(_smokeEffect);
			ParticleUtils.SetAutoDestroyAndCustomWorldSpace(_smokeEffect);
		}
		if (getNumberOfAmmunitionInAllContainers() == 0)
		{
			_ammunitionOnWeapon = null;
		}
		if (_containers.Count == 1)
		{
			StartReload();
		}
	}

	public void closeHatches(int containerId, float delay)
	{
		if (_containers[containerId]._areHatchesOpen)
		{
			if (_containers[containerId]._closeAnimation == null)
			{
				_containers[containerId]._areHatchesOpen = false;
			}
			else if (!_containers[containerId]._triggerCloseAnimation && !_containers[containerId]._closeAnimation.isPlaying())
			{
				_containers[containerId]._closeStartTime = GameTime.missionElapsedTime;
				_containers[containerId]._closeStartDelay = delay;
				_containers[containerId]._triggerCloseAnimation = true;
			}
		}
	}

	public void triggerLoadSystem()
	{
		if (!IsTubeHoldingWire() && !_triggerShutDown && _state == State.Idle)
		{
			_state = State.InitiateLoading;
		}
	}

	public void updateLoadSystem()
	{
		if (_triggerShutDown)
		{
			return;
		}
		switch (_state)
		{
		case State.InitiateLoading:
			_state = State.MoveToLoadPosition;
			_isReloading = true;
			break;
		case State.MoveToLoadPosition:
			if (GameTime.missionElapsedTime - _reloadStartTime > 1.0 && moveToLoadPosition())
			{
				if (_vwp._loadingType == LoadingType.ReloadsLowerDeck)
				{
					closeSystem(0f);
					_state = State.WaitForCloseSystem;
				}
				else if (_ammunitionOnWeapon == null || _sameTypeTopUp)
				{
					_state = State.LoadAmmunition;
					_sameTypeTopUp = false;
				}
				else
				{
					_state = State.UnloadAmmunition;
				}
			}
			break;
		case State.WaitForCloseSystem:
			if (!_isPlayingCloseAnim && !_triggerCloseAnimation)
			{
				if (_ammunitionOnWeapon == null || _sameTypeTopUp)
				{
					_state = State.LoadAmmunition;
					_sameTypeTopUp = false;
				}
				else
				{
					_state = State.UnloadAmmunition;
				}
			}
			break;
		case State.UnloadAmmunition:
		{
			if (_ammunitionOnWeapon == null)
			{
				_state = State.LoadAmmunition;
				break;
			}
			addWeaponGameObjectsToUnloadAnim();
			if (_ammunitionOnWeapon._ap._launcherPositions.TryGetValue(_vwp._systemName, out var value))
			{
				setCurrentAmmunitionLauncherPositions(value);
			}
			playUnloadAnimation();
			_state = State.WaitForUnloadAnimation;
			break;
		}
		case State.WaitForUnloadAnimation:
			if (hasFinishedUnloadAnimation())
			{
				destroyWeapons();
				_state = State.LoadAmmunition;
			}
			break;
		case State.LoadAmmunition:
			_spawnedAmmunition = _ammoForEngage;
			playLoadAnimation(spawnWeapons);
			_state = State.WaitForLoadAnimation;
			break;
		case State.WaitForLoadAnimation:
			if (hasFinishedLoadAnimation())
			{
				if (_spawnedAmmunition != null && _spawnedAmmunitionCount > 0)
				{
					_ammunitionOnWeapon = _spawnedAmmunition;
				}
				_state = State.Idle;
			}
			break;
		case State.Idle:
		case State.MoveToRestPosition:
		case State.CheckForLoadedAmmunition:
			break;
		}
	}

	public bool unloadSystem()
	{
		switch (_state)
		{
		case State.Idle:
			_state = State.MoveToLoadPosition;
			break;
		case State.MoveToLoadPosition:
			if (moveToLoadPosition())
			{
				if (_vwp._loadingType == LoadingType.ReloadsLowerDeck)
				{
					closeSystem(0f);
					_state = State.WaitForCloseSystem;
				}
				else
				{
					_state = State.UnloadAmmunition;
				}
			}
			break;
		case State.WaitForCloseSystem:
			if (!_isPlayingCloseAnim)
			{
				_state = State.UnloadAmmunition;
			}
			break;
		case State.UnloadAmmunition:
			addWeaponGameObjectsToUnloadAnim();
			if (_ammunitionOnWeapon != null)
			{
				if (_ammunitionOnWeapon._ap._launcherPositions.TryGetValue(_vwp._systemName, out var value))
				{
					setCurrentAmmunitionLauncherPositions(value);
				}
				playUnloadAnimation();
				_state = State.WaitForUnloadAnimation;
				break;
			}
			_state = State.Idle;
			_triggerShutDown = false;
			return true;
		case State.WaitForUnloadAnimation:
			if (hasFinishedUnloadAnimation())
			{
				destroyWeapons();
				_ammunitionOnWeapon = null;
				_state = State.MoveToRestPosition;
			}
			break;
		case State.MoveToRestPosition:
			if (moveToRestPosition())
			{
				_state = State.Idle;
				_triggerShutDown = false;
				return true;
			}
			break;
		}
		return false;
	}

	private void updateVents()
	{
		if (_ventGroups == null)
		{
			return;
		}
		foreach (SharedVentGroup ventGroup in _ventGroups)
		{
			bool flag = false;
			foreach (WeaponContainer container in ventGroup._containers)
			{
				if (container._areHatchesOpen || container._playOpenAnimation)
				{
					flag = true;
					break;
				}
			}
			if (flag && !ventGroup._isOpen && !ventGroup._playingOpen && !ventGroup._playingClose)
			{
				if (ventGroup._openAnimation != null)
				{
					ventGroup._openAnimation.playAnim();
					ventGroup._playingOpen = true;
				}
				else
				{
					ventGroup._isOpen = true;
				}
			}
			else if (!flag && ventGroup._isOpen && !ventGroup._playingOpen && !ventGroup._playingClose)
			{
				if (ventGroup._closeAnimation != null)
				{
					ventGroup._closeAnimation.playAnim();
					ventGroup._playingClose = true;
				}
				else
				{
					ventGroup._isOpen = false;
				}
			}
			if (ventGroup._playingOpen && !ventGroup._openAnimation.update())
			{
				ventGroup._playingOpen = false;
				ventGroup._isOpen = true;
			}
			else if (ventGroup._playingClose && !ventGroup._closeAnimation.update())
			{
				ventGroup._playingClose = false;
				ventGroup._isOpen = false;
			}
		}
	}

	private void updateAnimations()
	{
		updateVents();
		foreach (WeaponContainer container in _containers)
		{
			if (container._closeAnimation != null && !container._playCloseAnimation)
			{
				if (container._triggerCloseAnimation && (float)(GameTime.missionElapsedTime - container._closeStartTime) > container._closeStartDelay)
				{
					container._triggerCloseAnimation = false;
					container._closeAnimation.playAnim();
					if (container._hatchSoundClip != null)
					{
						container._audioSource.clip = container._hatchSoundClip;
						container._audioSource.Stop();
						container._audioSource.Play();
					}
					container._playCloseAnimation = true;
				}
			}
			else if (container._closeAnimation != null && container._playCloseAnimation)
			{
				container.update();
			}
		}
		if (_openSystemAnimation != null && _isPlayingOpenAnim)
		{
			_openSystemAnimation.update();
			if (_isPlayingOpenAnim && !_openSystemAnimation.isPlaying())
			{
				_systemState = SystemState.SystemOpen;
				_isPlayingOpenAnim = false;
			}
		}
		if (_closeSystemAnimation != null)
		{
			if (!_isPlayingCloseAnim && _triggerCloseAnimation)
			{
				if (GameTime.missionElapsedTime - _closeStartTime > (double)_closeStartDelay)
				{
					_triggerCloseAnimation = false;
					_closeSystemAnimation.playAnim();
					_systemState = SystemState.ClosingSystem;
					_isPlayingCloseAnim = true;
				}
			}
			else if (_isPlayingCloseAnim)
			{
				_closeSystemAnimation.update();
				if (!_closeSystemAnimation.isPlaying())
				{
					_systemState = SystemState.SystemClosed;
					_isPlayingCloseAnim = false;
				}
			}
		}
		if (_isPlayingLoadAnim && _loadAmmunitionAnimation != null && !_loadAmmunitionAnimation.update())
		{
			_loadingState = LoadingState.LoadingDone;
			_isPlayingLoadAnim = false;
		}
		if (_isPlayingUnloadAnim && _unloadAmmunitionAnimation != null && !_unloadAmmunitionAnimation.update())
		{
			_loadingState = LoadingState.UnloadingDone;
			_isPlayingUnloadAnim = false;
		}
		if (_triggerShutDown)
		{
			unloadSystem();
		}
	}

	public void setNextAmmunitionLauncherPositions(AmmunitionLauncherPosition alp)
	{
		if (_loadAmmunitionAnimation != null)
		{
			_loadAmmunitionAnimation.setAmmunitionLoaderPositions(alp);
		}
	}

	public void setCurrentAmmunitionLauncherPositions(AmmunitionLauncherPosition alp)
	{
		if (_unloadAmmunitionAnimation != null)
		{
			_unloadAmmunitionAnimation.setAmmunitionLoaderPositions(alp);
		}
	}

	public void playUnloadAnimation()
	{
		if (_unloadAmmunitionAnimation == null)
		{
			_loadingState = LoadingState.UnloadingDone;
			destroyWeapons();
			_isPlayingUnloadAnim = false;
		}
		else if (!(_isPlayingOpenAnim | _isPlayingCloseAnim | _isPlayingUnloadAnim | _isPlayingLoadAnim))
		{
			_loadingState = LoadingState.Unloading;
			_unloadAmmunitionAnimation.playAnim();
			_isPlayingUnloadAnim = true;
		}
	}

	public void playLoadAnimation(Action spawnWeaponCallback)
	{
		if (_loadAmmunitionAnimation == null)
		{
			spawnWeapons();
			_loadingState = LoadingState.LoadingDone;
			_isPlayingLoadAnim = false;
		}
		else if (!(_isPlayingOpenAnim | _isPlayingCloseAnim | _isPlayingUnloadAnim | _isPlayingLoadAnim))
		{
			_loadingState = LoadingState.Loading;
			_loadAmmunitionAnimation.playAnim(spawnWeaponCallback);
			_isPlayingLoadAnim = true;
		}
	}

	public bool hasFinishedUnloadAnimation()
	{
		return _loadingState == LoadingState.UnloadingDone;
	}

	public bool hasFinishedLoadAnimation()
	{
		return _loadingState == LoadingState.LoadingDone;
	}

	public void playOpenAnimation()
	{
		if (_openSystemAnimation == null || _systemState == SystemState.SystemOpen)
		{
			_systemState = SystemState.SystemOpen;
			_isPlayingOpenAnim = false;
		}
		else if (!(_isPlayingOpenAnim | _isPlayingCloseAnim | _isPlayingUnloadAnim | _isPlayingLoadAnim))
		{
			_systemState = SystemState.OpeningSystem;
			_openSystemAnimation.playAnim();
			_isPlayingOpenAnim = true;
		}
	}

	public void playCloseAnimation()
	{
		if (_closeSystemAnimation == null || _systemState == SystemState.SystemClosed)
		{
			_systemState = SystemState.SystemClosed;
			_isPlayingCloseAnim = false;
		}
		else if (!(_isPlayingOpenAnim | _isPlayingCloseAnim | _isPlayingUnloadAnim | _isPlayingLoadAnim))
		{
			_systemState = SystemState.ClosingSystem;
			_closeSystemAnimation.playAnim();
			_isPlayingCloseAnim = true;
		}
	}

	public bool openContainer(int containerIdx)
	{
		if (containerIdx >= _containers.Count)
		{
			Log.Error($"Cannot open container: {containerIdx}  of {_containers.Count} containers.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 2321, "openContainer");
			return false;
		}
		return _containers[containerIdx].openHatches();
	}

	public bool closeContainer(int containerIdx)
	{
		if (containerIdx >= _containers.Count)
		{
			Log.Error($"Cannot close container: {containerIdx} of {_containers.Count} containers.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 2332, "closeContainer");
			return false;
		}
		return _containers[containerIdx].closeHatches();
	}

	public int getNumberOfContainers()
	{
		return _containers.Count;
	}

	public WeaponContainer getContainerByIndex(int idx)
	{
		return _containers[idx];
	}

	public int getNumberOfAmmunitionInAllContainers()
	{
		int num = 0;
		foreach (WeaponContainer container in _containers)
		{
			num += container.getNumberOfWeapons();
		}
		return num;
	}

	public Dictionary<string, int> GetLoadedAmmoInfo()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (KeyValuePair<string, int> item in _loadedAmmunitionCount)
		{
			if (item.Value <= 0)
			{
				continue;
			}
			string key = item.Key;
			foreach (WeaponContainer container in _containers)
			{
				bool flag = false;
				foreach (WeaponBase weapon in container._weapons)
				{
					if (weapon._ap._ammunitionFileName == item.Key)
					{
						key = weapon._ap._displayedName;
						flag = true;
						break;
					}
				}
				if (flag)
				{
					break;
				}
			}
			if (!dictionary.ContainsKey(key))
			{
				dictionary.Add(key, item.Value);
			}
			else
			{
				dictionary[key] += item.Value;
			}
		}
		return dictionary;
	}

	public Dictionary<string, int> GetLoadedAmmoInfoSorted()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (KeyValuePair<string, int> item in _loadedAmmunitionCount)
		{
			if (item.Value > 0)
			{
				if (!dictionary.ContainsKey(item.Key))
				{
					dictionary.Add(item.Key, item.Value);
				}
				else
				{
					dictionary[item.Key] += item.Value;
				}
			}
		}
		return dictionary;
	}

	public void ApplyLoadoutOverride(Dictionary<string, int> loadout)
	{
		if (loadout == null)
		{
			return;
		}
		foreach (WeaponContainer container in _containers)
		{
			container.ClearWeapons();
		}
		foreach (KeyValuePair<string, int> item in new List<KeyValuePair<string, int>>(_loadedAmmunitionCount))
		{
			if (item.Value > 0)
			{
				_baseObject.changeAmmunitionAmount(item.Key, -item.Value);
			}
		}
		_loadedAmmunitionCount.Clear();
		_ammunitionOnWeapon = null;
		int num = 0;
		foreach (KeyValuePair<string, int> item2 in loadout)
		{
			if (item2.Value <= 0)
			{
				continue;
			}
			Ammunition ammunition = ResolveOverrideAmmo(item2.Key);
			if (ammunition == null)
			{
				continue;
			}
			int num2 = item2.Value;
			while (num2 > 0 && num < _containers.Count)
			{
				WeaponContainer weaponContainer = _containers[num];
				if (weaponContainer.FreeSlots <= 0)
				{
					num++;
					continue;
				}
				int num3 = weaponContainer.LoadTypeNoCount(ammunition, Mathf.Min(weaponContainer.FreeSlots, num2));
				if (num3 <= 0)
				{
					num++;
					continue;
				}
				num2 -= num3;
				if (weaponContainer.FreeSlots <= 0)
				{
					num++;
				}
			}
			int num4 = item2.Value - num2;
			if (num4 > 0)
			{
				_loadedAmmunitionCount[item2.Key] = num4;
				_baseObject.changeAmmunitionAmount(item2.Key, num4);
				_ammunitionOnWeapon = ammunition;
			}
		}
	}

	private Ammunition ResolveOverrideAmmo(string ammunitionName)
	{
		if (string.IsNullOrEmpty(ammunitionName))
		{
			return null;
		}
		Ammunition ammunition = Singleton<ResourceLoader>.Instance.loadAmmunitionNoMagazine(_baseObject, ammunitionName, this, addToPool: true);
		if (_vwp._maxAmmoCount == 0 && getLoadedAmmoCount(ammunitionName) > 0)
		{
			decreaseLoadedAmmoCount(ammunitionName);
		}
		_baseObject.addAmmunitionToWeaponReference(ammunitionName, this);
		if (ammunition != null)
		{
			Utils.AddAmmunitionGroupToDictionary(ammunition, _baseObject._obp._launcherAmmunitionGroups);
		}
		return ammunition;
	}

	public void addWeaponGameObjectsToUnloadAnim()
	{
		if (_unloadAmmunitionAnimation == null)
		{
			return;
		}
		List<GameObject> list = new List<GameObject>();
		foreach (WeaponContainer container in _containers)
		{
			container.getWeaponGameObjects(out var missileGameObjects);
			list.AddRange(missileGameObjects);
		}
		_unloadAmmunitionAnimation.addSpawnedObjects(list);
	}

	public void spawnWeapons()
	{
		List<GameObject> list = new List<GameObject>();
		string text = _spawnedAmmunition?._ap?._ammunitionFileName;
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < _containers.Count; j++)
			{
				WeaponContainer weaponContainer = _containers[(_nextLoadContainer + j) % _containers.Count];
				bool flag = !string.IsNullOrEmpty(weaponContainer._partialAmmoName) && weaponContainer._partialAmmoName == text;
				if (i == 0 == flag)
				{
					weaponContainer.load(_spawnedAmmunition, out var spawnedObjects);
					list.AddRange(spawnedObjects);
				}
			}
		}
		_spawnedAmmunitionCount = list.Count;
		if (_loadAmmunitionAnimation != null)
		{
			_loadAmmunitionAnimation.addSpawnedObjects(list);
		}
	}

	public override void rotateMount(float direction, bool ignoreLimits = false)
	{
		base.rotateMount(direction, ignoreLimits);
	}

	public override void rotateContainerVertical(float direction)
	{
		base.rotateContainerVertical(direction);
	}

	protected void destroyWeapons()
	{
		foreach (WeaponContainer container in _containers)
		{
			container.unload();
		}
		_spawnedAmmunition = null;
		_ammunitionOnWeapon = null;
	}

	public void activatePreLaunchGuidance(ObjectBase targetObject)
	{
		_sensorPreLaunchAligned = true;
		if (!(targetObject != null) || _vwp._associatedSensors.Count <= 0)
		{
			return;
		}
		bool flag = false;
		if (_ammoForEngage._ap._requiresGuidance)
		{
			flag = true;
		}
		if (_ammoForEngage._ap._type == Ammunition.Type.Projectile)
		{
			flag = true;
		}
		if (!flag)
		{
			return;
		}
		foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
		{
			if (!associatedSensor.IsOn.Value)
			{
				if (!_vwp._autoEnableSensor || associatedSensor.isBusy())
				{
					continue;
				}
				associatedSensor.Enable(allowMessage: false, turnOffAfterEngage: true);
			}
			if (associatedSensor._preLaunchTargetObject == targetObject)
			{
				associatedSensor._restPositionReached = false;
				CalculateHorizon(associatedSensor, targetObject);
				break;
			}
			if (associatedSensor.CanGuideAmmo(_ammoForEngage._ap) && (associatedSensor.HasFreeTargetChannels() || associatedSensor.IsAimedAtTarget(targetObject)) && associatedSensor.HasFreeWeaponChannels())
			{
				associatedSensor._restPositionReached = false;
				if (associatedSensor._preLaunchTargetObject == null)
				{
					associatedSensor.SetPrelaunchObject(targetObject);
					CalculateHorizon(associatedSensor, targetObject);
				}
				break;
			}
		}
	}

	private void CalculateHorizon(SensorSystem vbs, ObjectBase targetObject)
	{
		if (_ammoForEngage._ap.GuidanceTypeForTarget(targetObject) != AmmunitionParameters.GuidanceType.SemiActiveRadarHoming && (_ammoForEngage._ap.GuidanceTypeForTarget(targetObject) != 0 || _ammoForEngage._ap._midCourseCorrection != AmmunitionParameters.MidCourseCorrection.RadioCommand))
		{
			return;
		}
		_horizonRange = RadarCalculator.getRadarHorizon(vbs._mountObject.transform.position.y, _ammoForEngage._ap.FcrTrackedAltitude(targetObject), visual: false, Utils.TerrainAltForHorizon(vbs._baseObject, targetObject));
		foreach (AlignUnitToTarget alignmentTask in _baseObject._alignmentTasks)
		{
			if (alignmentTask._uid == _alignmentUID)
			{
				alignmentTask.ApplyHorizon(_horizonRange);
				break;
			}
		}
	}

	public override void SaveStateToFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.SaveStateToFile(ini, sectionName, isCampaignStateSave);
		if (isCampaignStateSave || _containers.Count == 0)
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		string text = "";
		for (int i = 0; i < _containers.Count; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append('|');
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			foreach (WeaponBase weapon in _containers[i]._weapons)
			{
				dictionary.TryGetValue(weapon._ap._ammunitionFileName, out var value);
				dictionary[weapon._ap._ammunitionFileName] = value + 1;
			}
			bool flag = true;
			foreach (KeyValuePair<string, int> item in dictionary)
			{
				if (!flag)
				{
					stringBuilder.Append('&');
				}
				stringBuilder.Append(item.Key).Append(':').Append(item.Value);
				flag = false;
			}
			if (!string.IsNullOrEmpty(_containers[i]._partialAmmoName))
			{
				if (text != "")
				{
					text += "|";
				}
				text += $"{i}:{_containers[i]._partialAmmoName}:{_containers[i]._partialRemaining}";
			}
		}
		ini.writeValue(sectionName, "ContainerContents", stringBuilder.ToString());
		if (_nextLoadContainer != 0)
		{
			ini.writeValue(sectionName, "NextLoadContainer", _nextLoadContainer);
		}
		if (text != "")
		{
			ini.writeValue(sectionName, "PartialPacks", text);
		}
	}

	public override void LoadStateFromFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.LoadStateFromFile(ini, sectionName, isCampaignStateSave);
		if (isCampaignStateSave || _containers.Count == 0)
		{
			return;
		}
		_nextLoadContainer = ini.readValue(sectionName, "NextLoadContainer", 0, performLogging: false);
		if (_nextLoadContainer < 0 || _nextLoadContainer >= _containers.Count)
		{
			_nextLoadContainer = 0;
		}
		string[] array2;
		if (ini.doesKeyExist(sectionName, "ContainerContents"))
		{
			string[] array = ini.readValue(sectionName, "ContainerContents", "", performLogging: false).Split('|');
			if (array.Length == _containers.Count)
			{
				foreach (WeaponContainer container in _containers)
				{
					container.ClearWeapons();
				}
				_loadedAmmunitionCount.Clear();
				_ammunitionOnWeapon = null;
				for (int i = 0; i < _containers.Count; i++)
				{
					if (string.IsNullOrEmpty(array[i]))
					{
						continue;
					}
					array2 = array[i].Split('&');
					for (int j = 0; j < array2.Length; j++)
					{
						string[] array3 = array2[j].Split(':');
						if (array3.Length != 2 || !int.TryParse(array3[1], out var result) || result <= 0)
						{
							continue;
						}
						Ammunition ammunition = ResolveSavedAmmo(array3[0]);
						if (ammunition == null)
						{
							Log.Error(_baseObject.getUIDAndName() + ": could not resolve saved ammunition '" + array3[0] + "' for " + _sectionName, Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 2685, "LoadStateFromFile");
						}
						else
						{
							int num = _containers[i].RestoreWeapons(ammunition, result);
							if (num > 0)
							{
								_loadedAmmunitionCount.TryGetValue(array3[0], out var value);
								_loadedAmmunitionCount[array3[0]] = value + num;
								_ammunitionOnWeapon = ammunition;
							}
						}
					}
				}
			}
		}
		string text = ini.readValue(sectionName, "PartialPacks", "", performLogging: false);
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		array2 = text.Split('|');
		for (int j = 0; j < array2.Length; j++)
		{
			string[] array4 = array2[j].Split(':');
			if (array4.Length == 3 && int.TryParse(array4[0], out var result2) && result2 >= 0 && result2 < _containers.Count && int.TryParse(array4[2], out var result3) && result3 > 0 && _containers[result2].SlotDensity(ResolveSavedAmmo(array4[1])?._ap) > 1)
			{
				_containers[result2]._partialAmmoName = array4[1];
				_containers[result2]._partialRemaining = result3;
			}
		}
	}

	private Ammunition ResolveSavedAmmo(string ammunitionName)
	{
		if (string.IsNullOrEmpty(ammunitionName))
		{
			return null;
		}
		if (_vwp._associatedMagazine != null)
		{
			Ammunition ammunitionByName = _vwp._associatedMagazine.getAmmunitionByName(ammunitionName);
			if (ammunitionByName != null)
			{
				return ammunitionByName;
			}
		}
		foreach (WeaponContainer container in _containers)
		{
			if (container._initialAmmunition?._ap?._ammunitionFileName == ammunitionName)
			{
				return container._initialAmmunition;
			}
		}
		return ResolveOverrideAmmo(ammunitionName);
	}

	public override bool CanForceReload()
	{
		return false;
	}

	public override void RefillAmmo(List<WeaponMagazineSystem> processedMagazines = null)
	{
		base.RefillAmmo(processedMagazines);
		_nextLoadContainer = 0;
		foreach (WeaponContainer container in _containers)
		{
			container.ClearPartialPack();
		}
		if (_vwp._associatedMagazine == null)
		{
			foreach (WeaponContainer container2 in _containers)
			{
				if (!container2.IsFull())
				{
					container2.RefillAmmo();
				}
			}
			return;
		}
		foreach (WeaponContainer container3 in _containers)
		{
			container3.unload(returnAmmo: false);
		}
		StartReload(instantReload: true);
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
