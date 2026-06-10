using System;
using System.Collections.Generic;
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

	private static readonly ProfilerMarker _profSystem = new ProfilerMarker("System: WeaponSystemLauncher");

	private GameObject _smokeEffect;

	private List<int> _containersToClose = new List<int>();

	private Ammunition _spawnedAmmunition;

	private int _spawnedAmmunitionCount;

	private bool _triggerShutDown;

	private bool _isSleeping;

	private bool _isLaunchingSalvo;

	private int _currentShotInSalvo;

	private float _readyToLaunchTime;

	private bool _onRail;

	private float _onRailWarmupStartTime;

	public State _state;

	public WeaponSystemLauncher(IniHandler ini, string sectionName, ObjectBase hostObject, WeaponParameters vwp, ObjectBaseParameters obp)
	{
		if (hostObject == null)
		{
			Log.Error("Launcher weapon system [" + sectionName + "] in ini file '" + ini.getFileName() + "' has a null hostObject.", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 51, ".ctor");
		}
		if (vwp == null)
		{
			Log.Error("Launcher weapon system [" + sectionName + "] in ini file '" + ini.getFileName() + "' has null WeaponParameters.", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 52, ".ctor");
		}
		if (vwp._mountObject == null)
		{
			Log.Error("Launcher weapon system [" + sectionName + "] in ini file '" + ini.getFileName() + "' has null mountObject in WeaponParameters.", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 53, ".ctor");
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
			for (int i = 1; i <= num; i++)
			{
				WeaponContainer weaponContainer = new WeaponContainer(hostObject, this, _vwp);
				string text2 = ini.readValue(sectionName, "Container" + i, "");
				if (text2.ToLower() == "dummy")
				{
					Vector3 localPosition = ini.readValue(sectionName, "Container" + i + "_Position", Vector3.zero);
					Vector3 localEulerAngles = ini.readValue(sectionName, "Container" + i + "_Rotation", Vector3.zero);
					weaponContainer._gunObject = new GameObject("AmmunitionParent");
					if (ini.readValue(sectionName, $"Container{i}_AttachWeaponsToMount", defaultValue: false))
					{
						weaponContainer._gunObject.transform.SetParent(vwp._mountObject.transform);
					}
					else
					{
						weaponContainer._gunObject.transform.SetParent(vwp._containerBaseObject.transform);
					}
					weaponContainer._gunObject.transform.localPosition = localPosition;
					weaponContainer._gunObject.transform.localEulerAngles = localEulerAngles;
				}
				else
				{
					obp._objectsDictionary.TryGetValue(text2, out weaponContainer._gunObject);
				}
				if (num2 > 0)
				{
					string key = ini.readValue(sectionName, "Container" + i + "_OpenHatchAnimation", "", performLogging: false);
					string key2 = ini.readValue(sectionName, "Container" + i + "_CloseHatchAnimation", "", performLogging: false);
					ObjectCodeAnimation value5 = null;
					ObjectCodeAnimation value6 = null;
					obp._codeAnimations.TryGetValue(key, out value5);
					obp._codeAnimations.TryGetValue(key2, out value6);
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
						string text3 = ini.readValue(sectionName, "Container" + i + "_Hatch" + j, "");
						if (text3 == "FALSE")
						{
							continue;
						}
						GameObject value7 = null;
						obp._objectsDictionary.TryGetValue(text3, out value7);
						if (value7 == null)
						{
							if (string.IsNullOrEmpty(text3))
							{
								text3 = "(search key = Container" + i + "_Hatch" + j + ")";
							}
							Log.Error("Could not find hatch object '" + text3 + "' in section '" + sectionName + "' in vessel file '" + ini.getFileName() + "'", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 137, ".ctor");
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
				if (_vwp._mount == null)
				{
					_vwp._mount = new WeaponMount(_vwp);
				}
				_vwp._mount.addAttachment(weaponContainer);
				_containers.Add(weaponContainer);
			}
		}
		else
		{
			Log.Error("Launcher weapon system [" + sectionName + "] in ini file '" + ini.getFileName() + "' has no 'NumberOfContainers' entry.", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 160, ".ctor");
		}
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
		_readyToLaunchTime = -1000f;
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
		if (_isReloading)
		{
			return;
		}
		if (_executingEngageTask && GameTime.time - _engageStartTime > _engageDelay)
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
			bool flag = false;
			if (_baseObject is Submarine)
			{
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
							Log.Error(_ammoForEngage._ap._displayedName + " can not be launched without associated sensor. Add 'AssociatedSensor' to " + _vwp._sectionName, Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 350, "OnUpdate");
						}
						EndEngageTask();
						return;
					}
					if (!hasFreeWeaponChannel())
					{
						return;
					}
				}
			}
			if (_targetObject != null && _ammoForEngage._ap.GuidanceTypeForTargetType(_targetObject._type) == AmmunitionParameters.GuidanceType.IRHoming && _ammoForEngage._ap._isRearAspectOnly && !Utils.isInRearAspect(_baseObject, _targetObject, _ammoForEngage._ap))
			{
				DM.Log(_baseObject.FullIDData() + ": Cancel engage of " + _targetObject.FullIDData() + ". Not in rear aspect of the target.", DM.EventLogFilters.EngageTask);
				Log.Debug(_baseObject.getUIDAndName() + ": <color=orange>Aborting engage task.</color> " + _ammoForEngage._ap._displayedName + " is rear aspect only and cannot be launched at current target " + _targetObject.getObjectIniName() + " at this moment", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 373, "OnUpdate");
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
				if (!DM._disableWarmUp)
				{
					StartReload();
					return;
				}
			}
			if (_ammoForEngage._ap._requiresGuidance && _ammoForEngage._ap.GuidanceTypeForTarget(_targetObject) == AmmunitionParameters.GuidanceType.LaserHoming)
			{
				SensorSystemLaserDesignator sensorSystemLaserDesignator = null;
				sensorSystemLaserDesignator = _baseObject._taskforce._sensorSystemsLink.findLaserDesignatorFromTaskForceLink(_targetObject);
				if (sensorSystemLaserDesignator == null)
				{
					foreach (SensorSystem sensorSystem in _baseObject._obp._sensorSystems)
					{
						if (sensorSystem._systemType != SystemType.SensorLaserDesignator)
						{
							continue;
						}
						SensorSystemLaserDesignator sensorSystemLaserDesignator2 = sensorSystem as SensorSystemLaserDesignator;
						if (sensorSystemLaserDesignator2.rangeTargetWithLaser(_targetObject))
						{
							if (sensorSystemLaserDesignator2._associatedTargets.Count < sensorSystemLaserDesignator2._targetChannels)
							{
								sensorSystemLaserDesignator2._associatedTargets.Add(_targetObject);
								sensorSystemLaserDesignator = sensorSystemLaserDesignator2;
							}
							else
							{
								sensorSystemLaserDesignator2._associatedTargets.RemoveAt(0);
								sensorSystemLaserDesignator2._associatedTargets.Add(_targetObject);
								sensorSystemLaserDesignator = sensorSystemLaserDesignator2;
							}
							break;
						}
					}
				}
				if (sensorSystemLaserDesignator == null)
				{
					_engageState = EngageState.LaserCantPaintTarget;
					return;
				}
			}
			if (GameTime.TimeCompression > Globals._weaponsTCLimit && _ammoForEngage._ap._subType != Ammunition.Type.Sonobuoy && _ammoForEngage._ap._subType != Ammunition.Type.Paratrooper && _ammoForEngage._ap._type != Ammunition.Type.Noisemaker)
			{
				GameTime.SetTimeCompression(Globals._weaponsTCLimit);
			}
			_currentMaxLaunchRange = _ammoForEngage._ap._launchRangesInUnity.y;
			if (_targetObject != null)
			{
				_currentMaxLaunchRange = _ammoForEngage._ap.MaxRangeRough(_baseObject, _targetObject.transform.position, _targetObject._velocityVecInUnity);
			}
			_currentMaxLaunchRange = ((_horizonRange > 0f && _horizonRange < _currentMaxLaunchRange) ? _horizonRange : _currentMaxLaunchRange);
			if (_weaponType != WeaponType.Noisemaker && _ammoForEngage._ap._subType != Ammunition.Type.Fueltank)
			{
				_engageState = EngageState.TargetTooClose;
				if (num <= _ammoForEngage._ap._launchRangesInUnity.x && (_weaponType != WeaponType.Torpedo || !_baseObject.IsAirUnit))
				{
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
							DM.I($"{_baseObject.getUIDAndName()}: <color=orange>Aborting engage task. Target has flown away.</color>:  {num} > {_currentMaxLaunchRange} || {num} < {_ammoForEngage._ap._launchRangesInUnity.x}");
							EndEngageTask();
						}
						return;
					}
				}
				if (_ammoForEngage._ap.GuidanceTypeForTarget(_targetObject) == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
				{
					_engageState = EngageState.TargetIsNotDetectableByFCR;
					if (!CheckTargetIsDetectableByFCR())
					{
						return;
					}
				}
				else if (_ammoForEngage._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand && _ammoForEngage._ap.GuidanceTypeForTarget(_targetObject) == AmmunitionParameters.GuidanceType.None && _targetObject != null && !(_targetObject is LandUnit { _isMobile: false }))
				{
					_engageState = EngageState.TargetIsNotDetectableByFCR;
					if (FCRDetectionRange(null, _ammoForEngage) < num)
					{
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
			if (flag2 && _targetObject != null && _vwp._associatedSearchSensors.Count > 0)
			{
				bool flag3 = true;
				_engageState = EngageState.TargetIsNotDetectableBySearchSensor;
				bool flag4 = false;
				foreach (SensorSystem associatedSearchSensor in _vwp._associatedSearchSensors)
				{
					if (associatedSearchSensor.Inoperable.Value)
					{
						continue;
					}
					if (!associatedSearchSensor.IsOn.Value)
					{
						flag3 = false;
					}
					if (associatedSearchSensor is SensorSystemRadar sensorSystemRadar)
					{
						if (!associatedSearchSensor.IsOn.Value && !associatedSearchSensor._wasSetOn)
						{
							associatedSearchSensor.Enable(allowMessage: true, turnOffAfterEngage: true);
						}
						if (!flag4 && sensorSystemRadar._detectedObjects.Contains(_targetObject))
						{
							flag4 = true;
						}
					}
				}
				if (!flag4)
				{
					foreach (SensorSystem associatedSearchSensor2 in _vwp._associatedSearchSensors)
					{
						if (!associatedSearchSensor2.Inoperable.Value && !(associatedSearchSensor2 is SensorSystemRadar) && associatedSearchSensor2._detectedObjects.Contains(_targetObject))
						{
							flag4 = true;
							guidanceSystemPenalty = associatedSearchSensor2._weaponGuidancePenalty;
							break;
						}
					}
				}
				if (DM_TestTools._allowAssociatedSearchSensors && !flag4)
				{
					if (_asssociatedSensorsAllOnTime < 0f && flag3)
					{
						_asssociatedSensorsAllOnTime = GameTime.time + 5f;
					}
					if (GameTime.time > _asssociatedSensorsAllOnTime)
					{
						EndEngageTask();
					}
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
				return;
			}
			_engageState = EngageState.ChoosingContainer;
			int container = getContainer();
			if (container < 0)
			{
				return;
			}
			if (_baseObject is LandUnit { _isMobile: not false } && Mathf.Abs(_baseObject._velocityInKnots) > _vwp._maxVelocityForLandUnitKn)
			{
				if (_systemState == SystemState.OpeningSystem)
				{
					return;
				}
				if (_systemState != SystemState.SystemClosed)
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
					_restPositionReachedTime = GameTime.time;
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
					_onRailWarmupStartTime = GameTime.time;
				}
				_engageState = EngageState.OnRailWarmup;
				if (GameTime.time - _onRailWarmupStartTime < _ammoForEngage._ap._launcherPositions[_systemName]._onRailWarmup)
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
			bool flag5 = _baseObject._sharedLaunchIntervals.ContainsKey(_systemName);
			if (!_isLaunchingSalvo)
			{
				if (_weaponType == WeaponType.Torpedo && GameTime.time - _baseObject._torpedoLastLaunchTime < _baseObject._torpedoLaunchDelay)
				{
					return;
				}
				_engageState = EngageState.PreLaunchDelay;
				if (GameTime.time - _readyToLaunchTime < _vwp._preLaunchDelay)
				{
					return;
				}
				_engageState = EngageState.FirerateDelay;
				if (GameTime.time - _lastLaunchTime < 60f / (float)_vwp._fireRatePerMinute)
				{
					return;
				}
				_engageState = EngageState.SharedLaunchDelay;
				if (flag5 && GameTime.time - _baseObject._sharedLaunchTimers[_systemName] < _baseObject._sharedLaunchIntervals[_systemName])
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
					float num2 = MathF.PI * 2f * (float)_currentShotInSalvo / (float)_vwp._salvoFireAmount;
					if (_vwp._invertedPattern)
					{
						num2 += MathF.PI;
					}
					_offsetVector.x = _vwp._salvoFireTargetCircleRadius * Mathf.Sin(num2) * 0.0148809375f;
					_offsetVector.z = _vwp._salvoFireTargetCircleRadius * Mathf.Cos(num2) * 0.0148809375f;
				}
				if (GameTime.time - _lastLaunchTime < _vwp._salvoFireTime)
				{
					return;
				}
				launch(container, _targetObject, _targetPosition, _offsetVector, guidanceSystemPenalty);
				_currentShotInSalvo++;
				_lastLaunchTime = GameTime.time;
				if (flag5)
				{
					_baseObject._sharedLaunchTimers[_systemName] = GameTime.time;
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
						float num3 = MathF.PI * 2f * (float)_currentShotInSalvo / (float)_vwp._salvoFireAmount;
						if (_vwp._invertedPattern)
						{
							num3 += MathF.PI;
						}
						_offsetVector.x = _vwp._salvoFireTargetCircleRadius * Mathf.Sin(num3) * 0.0148809375f;
						_offsetVector.z = _vwp._salvoFireTargetCircleRadius * Mathf.Cos(num3) * 0.0148809375f;
					}
					if (GameTime.time - _lastLaunchTime < _vwp._salvoFireTime)
					{
						return;
					}
					launch(container, _targetObject, _targetPosition, _offsetVector, guidanceSystemPenalty);
					_currentShotInSalvo++;
					_lastLaunchTime = GameTime.time;
					if (flag5)
					{
						_baseObject._sharedLaunchTimers[_systemName] = GameTime.time;
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
						Singleton<VoiceMessage>.Instance.PlayVoiceMessage("DecoyOut", _baseObject.getNation());
					}
				}
				if (_weaponType == WeaponType.Torpedo)
				{
					_baseObject._torpedoLastLaunchTime = GameTime.time;
				}
			}
			_onRail = false;
			_offsetVector = Vector3.zero;
			if (flag5)
			{
				_baseObject._sharedLaunchTimers[_systemName] = GameTime.time;
			}
			_engageState = EngageState.ClosingHatches;
			closeHatches(container, 3f);
			if (_weaponType == WeaponType.Torpedo && _containers.Count == 1 && _containers[0]._attachmentPositions.Count == 1 && !_vwp._isMountRotatable)
			{
				closeSystem(3f);
			}
			DM.I(_baseObject.getUIDAndName() + ": <color=green>Engage task complete</color>");
			EndEngageTask();
			_lastLaunchTime = GameTime.time;
			_isLaunchingSalvo = false;
			_currentShotInSalvo = 0;
		}
		else
		{
			if (_isSleeping || !(GameTime.time - _lastLaunchTime > _vwp._moveToRestPositionTime) || !_isInRestPosition || _systemState == SystemState.OpeningSystem)
			{
				return;
			}
			if (_systemState != SystemState.SystemClosed)
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
				if (!_vwp._hasPreloadedAmmunition && _ammunitionOnWeapon != null)
				{
					_triggerShutDown = true;
				}
				_isSleeping = true;
			}
		}
	}

	private int getContainer()
	{
		if (_ammoForEngage == null)
		{
			return -1;
		}
		if (_ammunitionOnWeapon == null)
		{
			triggerLoadSystem();
			return -1;
		}
		if (_ammunitionOnWeapon._ap._ammunitionFileName != _ammoForEngage._ap._ammunitionFileName)
		{
			triggerLoadSystem();
			return -1;
		}
		for (int i = 0; i < _containers.Count; i++)
		{
			if (getContainerByIndex(i).getNumberOfWeapons() > 0 || _vwp._loadingType == LoadingType.SpawnWhenNeeded)
			{
				return i;
			}
		}
		return -1;
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

	public override bool alignToTarget(Vector3 targetPosition, bool useFixedLaunchAngle, int containerIndexToUse, bool preciseAim = false)
	{
		_isInRestPosition = false;
		_lastUsageTime = GameTime.time;
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
		_lastUsageTime = GameTime.time;
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
			_readyToLaunchTime = GameTime.time;
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
		if (_vwp._vidIncrease > 0f && _baseObject?._sensorData != null && _baseObject._sensorData._vidIncrease < _vwp._vidIncrease + 0.001f)
		{
			_baseObject._sensorData._vidIncrease = _vwp._vidIncrease;
			_baseObject._sensorData._vidIncreaseEndTime = GameTime.time + _vwp._vidIncreaseInterval;
		}
		_containers[containerIdx].launch(targetObject, targetPosition, offsetVector, guidanceSystemPenalty);
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
		if (getNumberOfAmmunitionInAllContainers() == 0)
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
				_containers[containerId]._closeStartTime = GameTime.time;
				_containers[containerId]._closeStartDelay = delay;
				_containers[containerId]._triggerCloseAnimation = true;
			}
		}
	}

	public void triggerLoadSystem()
	{
		if (_state == State.Idle)
		{
			_state = State.InitiateLoading;
		}
	}

	public void updateLoadSystem()
	{
		switch (_state)
		{
		case State.InitiateLoading:
			_state = State.MoveToLoadPosition;
			_isReloading = true;
			break;
		case State.MoveToLoadPosition:
			if (GameTime.time - _reloadStartTime > 1f && moveToLoadPosition())
			{
				if (_vwp._loadingType == LoadingType.ReloadsLowerDeck)
				{
					closeSystem(0f);
					_state = State.WaitForCloseSystem;
				}
				else if (_ammunitionOnWeapon == null)
				{
					_state = State.LoadAmmunition;
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
				if (_ammunitionOnWeapon == null)
				{
					_state = State.LoadAmmunition;
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

	private void updateAnimations()
	{
		foreach (WeaponContainer container in _containers)
		{
			if (container._closeAnimation != null && !container._playCloseAnimation)
			{
				if (container._triggerCloseAnimation && GameTime.time - container._closeStartTime > container._closeStartDelay)
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
				if (GameTime.time - _closeStartTime > _closeStartDelay)
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
			Log.Error($"Cannot open container: {containerIdx}  of {_containers.Count} containers.", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 1716, "openContainer");
			return false;
		}
		return _containers[containerIdx].openHatches();
	}

	public bool closeContainer(int containerIdx)
	{
		if (containerIdx >= _containers.Count)
		{
			Log.Error($"Cannot close container: {containerIdx} of {_containers.Count} containers.", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystemLauncher.cs", 1727, "closeContainer");
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

	public void addWeaponGameObjectsToUnloadAnim()
	{
		if (_unloadAmmunitionAnimation == null)
		{
			return;
		}
		List<GameObject> missileGameObjects = new List<GameObject>();
		foreach (WeaponContainer container in _containers)
		{
			container.getWeaponGameObjects(out missileGameObjects);
		}
		_unloadAmmunitionAnimation.addSpawnedObjects(missileGameObjects);
	}

	public void spawnWeapons()
	{
		List<GameObject> spawnedObjects = new List<GameObject>();
		foreach (WeaponContainer container in _containers)
		{
			container.load(_spawnedAmmunition, out spawnedObjects);
		}
		_spawnedAmmunitionCount = spawnedObjects.Count;
		if (_loadAmmunitionAnimation != null)
		{
			_loadAmmunitionAnimation.addSpawnedObjects(spawnedObjects);
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
			if ((_ammoForEngage._ap._compatibleGuidanceSystems.Count <= 0 || _ammoForEngage._ap._compatibleGuidanceSystems.Contains(associatedSensor._systemName)) && (associatedSensor.HasFreeTargetChannels() || associatedSensor.IsAimedAtTarget(targetObject)) && associatedSensor.HasFreeWeaponChannels())
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
		if ((_ammoForEngage._ap.GuidanceTypeForTarget(targetObject) != AmmunitionParameters.GuidanceType.SemiActiveRadarHoming && (_ammoForEngage._ap.GuidanceTypeForTarget(targetObject) != AmmunitionParameters.GuidanceType.None || _ammoForEngage._ap._midCourseCorrection != AmmunitionParameters.MidCourseCorrection.RadioCommand)) || (_ammoForEngage._ap._hasSubmunition && targetObject != null && targetObject.isSubmerged()))
		{
			return;
		}
		_horizonRange = RadarCalculator.getRadarHorizon(vbs._mountObject.transform.position.y, targetObject.VisibleAltitude, visual: false, Utils.TerrainAltForHorizon(vbs._baseObject, targetObject));
		foreach (AlignUnitToTarget alignmentTask in _baseObject._alignmentTasks)
		{
			if (alignmentTask._uid == _alignmentUID)
			{
				alignmentTask.ApplyHorizon(_horizonRange);
				break;
			}
		}
	}
}
