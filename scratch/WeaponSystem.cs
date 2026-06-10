using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace SeaPower;

public class WeaponSystem : BaseSystem
{
	public enum WeaponType
	{
		UNKNOWN,
		Missile,
		Torpedo,
		RBU,
		Noisemaker,
		Gun,
		CIWS,
		Chaff,
		Hardpoint
	}

	public enum SystemState
	{
		SystemClosed,
		SystemOpen,
		OpeningSystem,
		ClosingSystem
	}

	public enum HatchesState
	{
		HatchesOpen,
		HatchesClosed,
		OpeningHatches,
		ClosingHatches
	}

	public enum LoadingState
	{
		Idle,
		Loading,
		LoadingDone,
		Unloading,
		UnloadingDone
	}

	public enum EngageState
	{
		NotObserved,
		Idle,
		Offline,
		CheckingAbort,
		WarmingUp,
		TargetIsNotDetectableBySearchSensor,
		PrelaunchSensorAlignment,
		PrelaunchSensorCheck,
		ChoosingContainer,
		OpeningSystem,
		OnRailWarmup,
		EngagementNotAllowed,
		SpeedTooLow,
		SpeedTooHigh,
		TargetTooClose,
		NoLOSToTarget,
		TargetIsBeyondFCRHorizon,
		TargetIsNotDetectableByFCR,
		LaserCantPaintTarget,
		TVSeekerCantSeeTarget,
		TargetTooFar,
		TargetTooFarToOpenSystem,
		NotInRearAspect,
		LauncherTooHigh,
		LauncherTooLow,
		TargetIsNotInFiringArcs,
		TargetIsNotInVerticalArc,
		AligningLauncher,
		OpeningHatches,
		WaitingForPitch,
		WaitingForRoll,
		PreLaunchDelay,
		FirerateDelay,
		SharedLaunchDelay,
		LaunchingSalvo,
		Launch,
		ClosingHatches,
		MissileCanNotReachTarget
	}

	public enum LoadingType
	{
		Unknown,
		NotReloadable,
		LoadWhenNeeded,
		SpawnWhenNeeded,
		ReloadsLowerDeck
	}

	private static readonly ProfilerMarker _profSystem = new ProfilerMarker("System: WeaponSystem");

	public string _name;

	public WeaponParameters _vwp;

	public ObjectBase _targetObject;

	public ObjectBase _previousTargetObject;

	public Ammunition _ammunitionOnWeapon;

	public SystemState _systemState;

	protected HatchesState _hatchesState = HatchesState.HatchesClosed;

	protected LoadingState _loadingState;

	public EngageState _engageState;

	public Dictionary<string, int> _loadedAmmunitionCount = new Dictionary<string, int>();

	public List<string> _associatedAmmunitionNames = new List<string>();

	protected float _reloadStartTime;

	public float _remainingReloadTime;

	public bool _isReloading;

	public bool _isEmpty;

	protected float _lastUsageTime;

	public bool _isInRestPosition = true;

	public float _restPositionReachedTime;

	public List<WeaponContainer> _containers = new List<WeaponContainer>();

	protected float _nextTargetPointSwitchTime;

	public bool _executingEngageTask;

	public bool _cantInterruptEngage;

	public int _engageTaskPriority;

	public float _engageStartTime;

	public float _engageDelay;

	protected float _asssociatedSensorsAllOnTime;

	public EngageTask _takenEngageTask;

	public Vector3 _targetPosition = Vector3.zero;

	public Vector3 _offsetVector = Vector3.zero;

	public LandUnitParameters.TargetPoint _currentTargetPoint;

	protected Vector3Event _relocationEvent;

	public int _alignmentUID;

	public Ammunition _ammoForEngage;

	public float _lastLaunchTime;

	protected ObjectBase _currentTargetObject;

	protected Vector3 _lastInterceptPosition;

	public WeaponType _weaponType;

	public bool _sensorPreLaunchAligned;

	public bool _isTargetObjectRequired;

	public bool _isOffensive;

	public List<WeaponBase> _launchedWeapons = new List<WeaponBase>();

	public bool _isAutoEngaging;

	public bool _alwaysDisplayWeaponsOnSystem;

	private string displayState;

	private string sonoDisplayDeployMode;

	public string _alignmentDeltas = "";

	protected float _horizonRange;

	public float _currentMaxLaunchRange = -1f;

	private bool _isHot;

	public bool _ignoreIfUndetected;

	public bool _reloadableWithoutMagazine;

	public float _lastSearchForSensorTime;

	public float _targetAcquisitionStartTime = -1000f;

	protected bool _isPlayingOpenAnim;

	protected bool _isPlayingCloseAnim;

	protected bool _isPlayingUnloadAnim;

	protected bool _isPlayingLoadAnim;

	public ObjectCodeAnimation _unloadAmmunitionAnimation;

	public ObjectCodeAnimation _loadAmmunitionAnimation;

	protected bool _triggerCloseAnimation;

	protected float _closeStartTime;

	protected float _closeStartDelay;

	public bool _isOffline;

	public string DisplayState
	{
		get
		{
			return displayState;
		}
		set
		{
			if (!(displayState == value))
			{
				displayState = value;
				NotifyPropertyChanged("DisplayState");
			}
		}
	}

	public string SonoDisplayDeployMode
	{
		get
		{
			return sonoDisplayDeployMode;
		}
		set
		{
			sonoDisplayDeployMode = value;
			NotifyPropertyChanged("SonoDisplayDeployMode");
		}
	}

	public bool IsHot
	{
		get
		{
			return _isHot;
		}
		set
		{
			_isHot = value;
			_isInRestPosition = false;
		}
	}

	public override void SaveStateToFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.SaveStateToFile(ini, sectionName, isCampaignStateSave);
		if (_vwp._internalAmmoCount < _vwp._maxAmmoCount)
		{
			ini.writeValue(sectionName, "InternalAmmoCount", _vwp._internalAmmoCount);
		}
		string text = "";
		foreach (KeyValuePair<string, int> item in _loadedAmmunitionCount)
		{
			if (text != "")
			{
				text += "|";
			}
			text = text + item.Key + "," + item.Value;
		}
		if (text != "")
		{
			ini.writeValue(sectionName, "LoadedAmmunitions", text);
		}
		if (!isCampaignStateSave)
		{
			if (_remainingReloadTime > 0f)
			{
				ini.writeValue(sectionName, "RemainingReloadTime", _remainingReloadTime);
			}
			if (_isOffline)
			{
				ini.writeValue(sectionName, "IsOffline", value: true);
			}
		}
	}

	public override void LoadFromInI(IniHandler ini, string sectionName, ObjectBaseParameters obp, string keyName = "")
	{
		base.LoadFromInI(ini, sectionName, obp, keyName);
		_debugCheckModuleNameLoad = false;
		_moduleName = _vwp._moduleName;
	}

	public override void LoadStateFromFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.LoadStateFromFile(ini, sectionName, isCampaignStateSave);
		if (_baseObject._campaignRearm && isCampaignStateSave)
		{
			return;
		}
		LoadAmmoCountFromFile(ini, sectionName);
		if (!isCampaignStateSave)
		{
			_remainingReloadTime = ini.readValue(sectionName, "RemainingReloadTime", 0f);
			_isOffline = ini.readValue(sectionName, "IsOffline", defaultValue: false);
			if (_remainingReloadTime > 0f)
			{
				_isReloading = true;
				_lastLaunchTime = GameTime.time - _vwp._magazineReloadTime - _remainingReloadTime;
				_reloadStartTime = GameTime.time - _vwp._magazineReloadTime + _remainingReloadTime;
			}
		}
	}

	public void LoadAmmoCountFromFile(IniHandler ini, string sectionName)
	{
		if (getOnWeaponAmmunition() != null)
		{
			int num = ini.readValue(sectionName, "InternalAmmoCount", -1);
			if (num >= 0 && num < _vwp._maxAmmoCount)
			{
				_vwp._internalAmmoCount = num;
			}
		}
		string text = ini.readValue(sectionName, "LoadedAmmunitions", "");
		if (!(text != ""))
		{
			return;
		}
		string[] array = text.Split('|');
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(",");
			string key = array2[0];
			int result = -1;
			int.TryParse(array2[1], out result);
			if (result < 0)
			{
				continue;
			}
			if (this is WeaponSystemHardpoint weaponSystemHardpoint)
			{
				if (_loadedAmmunitionCount.ContainsKey(key))
				{
					int num2 = _loadedAmmunitionCount[key] - result;
					for (int j = 0; j < weaponSystemHardpoint._weapons.Count; j++)
					{
						if (num2 < 1)
						{
							break;
						}
						if (!(weaponSystemHardpoint._weapons[j]._ap._ammunitionFileName != array2[0]))
						{
							weaponSystemHardpoint._weapons[j].destroyObject(keepInFlightEffectAlive: false, score: false);
							weaponSystemHardpoint._weapons.RemoveAt(j);
							num2--;
							j--;
						}
					}
				}
			}
			else
			{
				int num3 = 0;
				foreach (WeaponContainer container in _containers)
				{
					num3 += container.getNumberOfWeapons();
				}
				int num4 = num3 - result;
				foreach (WeaponContainer container2 in _containers)
				{
					if (num4 < 1)
					{
						break;
					}
					num4 -= container2.destroyWeapons(num4);
				}
			}
			_loadedAmmunitionCount[key] = result;
		}
	}

	public override void init()
	{
		base.init();
		_relocationEvent = new Vector3Event();
		_relocationEvent.AddListener(onRelocate);
		Singleton<RelocationHandler>.Instance.addToEventsList(_relocationEvent);
		_lastUsageTime = -1000f;
		_lastLaunchTime = -1000f;
		_reloadStartTime = -1000f;
		DisplayState = GetStatus();
	}

	private void onRelocate(Vector3 deltaPos)
	{
		_targetPosition += deltaPos;
	}

	public override void OnUpdate()
	{
		if (_baseObject == Singleton<RenderPosition>.Instance.getSelectedObject())
		{
			DisplayState = GetStatus();
		}
		if (GameTime.IsPaused() || base.Inoperable.Value)
		{
			return;
		}
		if (_isOffline)
		{
			_engageState = EngageState.Offline;
		}
		base.OnUpdate();
		if (_targetObject != null && (_targetObject._externalDestructionNotified || _targetObject.IsDestroyed))
		{
			_targetObject = null;
		}
		if (this is WeaponSystemLauncher weaponSystemLauncher && weaponSystemLauncher._vwp._associatedMagazine != null)
		{
			weaponSystemLauncher.updateLoadSystem();
		}
		_currentMaxLaunchRange = -1f;
		if (_isReloading)
		{
			_isInRestPosition = false;
			_lastUsageTime = GameTime.time;
			float num = GameTime.time - _reloadStartTime;
			_remainingReloadTime = _vwp._magazineReloadTime - num;
			if (_vwp._associatedMagazine != null && _vwp._associatedMagazine.IsEmpty())
			{
				num = _vwp._magazineReloadTime + 1f;
				_remainingReloadTime = -0.1f;
			}
			if (_weaponType == WeaponType.Gun || _weaponType == WeaponType.CIWS || _weaponType == WeaponType.Chaff)
			{
				if (num > _vwp._magazineReloadTime)
				{
					FinishReload();
				}
			}
			else
			{
				if (((_weaponType == WeaponType.Torpedo && _vwp._isMountRotatable) || _vwp._systemName == "ASROC" || _vwp._forceMoveToLoadPosition) && _remainingReloadTime > 0f && num > 2f && !moveToLoadPosition())
				{
					return;
				}
				if (_weaponType == WeaponType.RBU || _vwp._loadingType == LoadingType.ReloadsLowerDeck)
				{
					((WeaponSystemLauncher)this).updateLoadSystem();
					if (((WeaponSystemLauncher)this)._state != WeaponSystemLauncher.State.Idle && _remainingReloadTime < 0f)
					{
						_remainingReloadTime = 1f;
						return;
					}
				}
			}
			if (!(_remainingReloadTime < 0f))
			{
				return;
			}
			_isReloading = false;
			_remainingReloadTime = 0f;
			if (!(this is WeaponSystemLauncher { _loadAmmunitionAnimation: null }) || !_vwp._hasPreloadedAmmunition || _vwp._loadingType == LoadingType.SpawnWhenNeeded || _vwp._associatedMagazine == null)
			{
				return;
			}
			Ammunition ammunitionByIndex = _vwp._associatedMagazine.getAmmunitionByIndex(0);
			{
				foreach (WeaponContainer container in _containers)
				{
					container.load(ammunitionByIndex);
				}
				return;
			}
		}
		if (!_isInRestPosition)
		{
			bool flag = false;
			if (_loadingState == LoadingState.Loading || _loadingState == LoadingState.Unloading)
			{
				flag = true;
			}
			if (GameTime.time - _lastUsageTime > _vwp._moveToRestPositionTime && !flag && moveToRestPosition())
			{
				_isInRestPosition = true;
				_restPositionReachedTime = GameTime.time;
			}
		}
		else if (IsHot && !_executingEngageTask && GameTime.time - _restPositionReachedTime > 1800f)
		{
			IsHot = false;
		}
		_cantInterruptEngage = false;
	}

	public virtual void FinishReload()
	{
	}

	public void setOnWeaponAmmunition(Ammunition ammunition)
	{
		_ammunitionOnWeapon = ammunition;
	}

	public Ammunition getOnWeaponAmmunition()
	{
		return _ammunitionOnWeapon;
	}

	public bool hasAMagazine()
	{
		return _vwp._associatedMagazine != null;
	}

	public int getMagazineAmmoCount(Ammunition ammo)
	{
		return getMagazineAmmoCount(ammo._ap._ammunitionFileName);
	}

	public int getMagazineAmmoCount(string ammunitionName)
	{
		int result = 0;
		if (_vwp._associatedMagazine != null)
		{
			result = _vwp._associatedMagazine.getAmmunitionCount(ammunitionName);
		}
		return result;
	}

	public void decreaseMagazineAmmoCount(string ammunitionName, int number, bool initialLoad = false)
	{
		if (_vwp._associatedMagazine != null)
		{
			int magazineAmmoCount = getMagazineAmmoCount(ammunitionName);
			if (number > magazineAmmoCount)
			{
				number = magazineAmmoCount;
			}
			_baseObject.changeAmmunitionAmount(ammunitionName, -number);
			_vwp._associatedMagazine.decreaseAmmunitionCount(ammunitionName, number, initialLoad);
		}
	}

	public int getLoadedAmmoCount(Ammunition ammo)
	{
		return getLoadedAmmoCount(ammo._ap._ammunitionFileName);
	}

	public int getLoadedAmmoCount(string ammunitionName)
	{
		int value = 0;
		_loadedAmmunitionCount.TryGetValue(ammunitionName, out value);
		return value;
	}

	public void increaseLoadedAmmoCount(string ammunitionName)
	{
		if (!_loadedAmmunitionCount.ContainsKey(ammunitionName))
		{
			_loadedAmmunitionCount.Add(ammunitionName, 0);
		}
		_loadedAmmunitionCount[ammunitionName]++;
		_baseObject.changeAmmunitionAmount(ammunitionName, 1);
	}

	public void decreaseLoadedAmmoCount(string ammunitionName)
	{
		if (_loadedAmmunitionCount.ContainsKey(ammunitionName) && _loadedAmmunitionCount[ammunitionName] > 0)
		{
			_loadedAmmunitionCount[ammunitionName]--;
			_baseObject.changeAmmunitionAmount(ammunitionName, -1);
		}
	}

	public void decreaseLoadedAmmoCount(string ammunitionName, int number)
	{
		if (_loadedAmmunitionCount.ContainsKey(ammunitionName) && _loadedAmmunitionCount[ammunitionName] > 0)
		{
			_loadedAmmunitionCount[ammunitionName] -= number;
			_baseObject.changeAmmunitionAmount(ammunitionName, -number);
			if (_loadedAmmunitionCount[ammunitionName] < 0)
			{
				_baseObject.changeAmmunitionAmount(ammunitionName, -_loadedAmmunitionCount[ammunitionName]);
				_loadedAmmunitionCount[ammunitionName] = 0;
			}
		}
	}

	public void changeLoadedAmmoCount(string ammunitionName, int amount)
	{
		if (!_loadedAmmunitionCount.ContainsKey(ammunitionName))
		{
			_loadedAmmunitionCount.Add(ammunitionName, 0);
		}
		_loadedAmmunitionCount[ammunitionName] = amount;
		_baseObject.changeAmmunitionAmount(ammunitionName, amount);
	}

	public bool CheckEmissionsForAntiRadiationWeapons(string ammoID, ObjectBase targetObject)
	{
		return CheckEmissionsForAntiRadiationWeapons(_baseObject.getAmmunitionByName(ammoID), targetObject);
	}

	public bool CheckEmissionsForAntiRadiationWeapons(Ammunition ammo, ObjectBase targetObject)
	{
		AmmunitionParameters ap = ammo._ap;
		if (ap.GuidanceTypeForTarget(targetObject) != AmmunitionParameters.GuidanceType.AntiRadiationHoming)
		{
			return true;
		}
		if (!ap._targetRequired)
		{
			return true;
		}
		if (targetObject == null)
		{
			return false;
		}
		Vector3 position = _baseObject.transform.position;
		foreach (SensorSystemESM esmSystem in _baseObject._obp._esmSystems)
		{
			if (esmSystem._mountObject.transform.position.y > position.y)
			{
				position = esmSystem._mountObject.transform.position;
			}
		}
		if (RadarCalculator.getSignalStrength(targetObject, _baseObject, position, ap._gainFactor, ap._passiveRadarHomingFrequencies, out var _, 100f) < Globals.RcsValues._minDetectableReturnPowerESM)
		{
			return false;
		}
		return true;
	}

	public bool CheckTargetInRangeOfTVSeeker(string ammoId, ObjectBase targetObject)
	{
		return CheckTargetInRangeOfTVSeeker(_baseObject.getAmmunitionByName(ammoId), targetObject);
	}

	public bool CheckTargetInRangeOfTVSeeker(Ammunition ammo, ObjectBase targetObject)
	{
		AmmunitionParameters ap = ammo._ap;
		if (ap.GuidanceTypeForTarget(targetObject) != AmmunitionParameters.GuidanceType.TVHoming)
		{
			return true;
		}
		Vector3 unityPosition = _baseObject.getUnityPosition();
		float magnitude = (targetObject.getUnityPosition() - unityPosition).magnitude;
		float num = SensorUtils.CalculateVisualRange(_baseObject, targetObject, ap._nightVisionLevel);
		num *= ap._zoom;
		if (magnitude > num * 1.5f)
		{
			return false;
		}
		float radarHorizon = RadarCalculator.getRadarHorizon(_baseObject.transform.position.y, targetObject.VisibleAltitude, visual: true, Utils.TerrainAltForHorizon(_baseObject, _targetObject));
		if (magnitude > radarHorizon)
		{
			return false;
		}
		return true;
	}

	public bool CheckTargetIsDetectableByFCR()
	{
		for (int num = _vwp._associatedSensors.Count - 1; num >= 0; num--)
		{
			if (_vwp._associatedSensors[num] is SensorSystemRadar sensorSystemRadar && sensorSystemRadar.HasFreeTargetChannels(_targetObject) && sensorSystemRadar.HasFreeWeaponChannels() && CheckTargetIsDetectableByRadar(sensorSystemRadar))
			{
				return true;
			}
		}
		return false;
	}

	public bool CheckTargetIsDetectableByRadar(SensorSystemRadar ssr)
	{
		if (!ssr._radar.IsDetectableTarget_Unconditional(_targetObject))
		{
			return false;
		}
		if (!ssr._radar.IsDetectableTarget_Conditional(_targetObject))
		{
			return false;
		}
		SensorEcho sensorEcho = Globals._sensorEchoes.getFreeInstance();
		RadarParameters rp = ssr._radar._rp;
		Vector3 position = ssr._mountObject.transform.position;
		position.y = _targetObject.transform.position.y;
		bool result = RadarCalculator.createRadarEcho(position, _targetObject, rp._waveLength, rp._gainFactor, rp._peakPower, ssr._baseObject._ECMSystemsTryingToJamMe, rp._frequency, ssr._baseObject._crew, out sensorEcho, ignoreAspect: true);
		if (sensorEcho == null)
		{
			return false;
		}
		return result;
	}

	public float FCRDetectionRange(ObjectBase targetObject, Ammunition ammo)
	{
		if (ammo == null)
		{
			return -1f;
		}
		bool flag = ammo._ap.GuidanceTypeForTarget(targetObject) == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming;
		if (!flag && ammo._ap._midCourseCorrection != AmmunitionParameters.MidCourseCorrection.RadioCommand)
		{
			return -1f;
		}
		float num = -1f;
		CheckExternalRadarGuidance();
		for (int num2 = _vwp._associatedSensors.Count - 1; num2 >= 0; num2--)
		{
			if (_vwp._associatedSensors[num2] == null || _vwp._associatedSensors[num2]._baseObject == null)
			{
				_vwp._associatedSensors.RemoveAt(num2);
			}
			else
			{
				if (_vwp._associatedSensors[num2].Inoperable.Value || !(_vwp._associatedSensors[num2] is SensorSystemRadar sensorSystemRadar) || (ammo._ap._compatibleGuidanceSystems.Count > 0 && !ammo._ap._compatibleGuidanceSystems.Contains(_vwp._associatedSensors[num2]._systemName)))
				{
					continue;
				}
				RadarParameters rp = sensorSystemRadar._radar._rp;
				float num3;
				if (flag)
				{
					RadarCalculator.createRadarEcho(sensorSystemRadar._radar.UnityPosition, targetObject, rp._waveLength, rp._gainFactor, rp._peakPower, sensorSystemRadar._baseObject._ECMSystemsTryingToJamMe, rp._frequency, sensorSystemRadar._baseObject._crew, out var sensorEcho, ignoreAspect: true);
					if (sensorEcho == null)
					{
						continue;
					}
					num3 = sensorEcho._minBurnThroughRange;
				}
				else
				{
					num3 = RadarCalculator.GetFCRDetectionRange(sensorSystemRadar._radar.UnityPosition, ammo._ap._maxRCS, rp._waveLength, rp._gainFactor, rp._peakPower, sensorSystemRadar._baseObject._ECMSystemsTryingToJamMe, rp._frequency, sensorSystemRadar._baseObject._crew);
				}
				if (num3 > rp._maxRange)
				{
					num3 = rp._maxRange;
				}
				if (num < num3)
				{
					num = num3;
				}
			}
		}
		return num;
	}

	public bool CheckTargetIsDetectableBySearchRadar(ObjectBase targetObject)
	{
		if (_vwp._associatedSearchSensors.Count == 0)
		{
			return true;
		}
		foreach (SensorSystem associatedSearchSensor in _vwp._associatedSearchSensors)
		{
			if (associatedSearchSensor is SensorSystemRadar sensorSystemRadar && sensorSystemRadar._radar.IsDetectableTarget_Unconditional(targetObject) && sensorSystemRadar._radar.IsDetectableTarget_Conditional(targetObject))
			{
				SensorEcho sensorEcho = Globals._sensorEchoes.getFreeInstance();
				RadarParameters rp = sensorSystemRadar._radar._rp;
				Vector3 position = sensorSystemRadar._mountObject.transform.position;
				position.y = targetObject.transform.position.y;
				bool flag = RadarCalculator.createRadarEcho(position, targetObject, rp._waveLength, rp._gainFactor, rp._peakPower, sensorSystemRadar._baseObject._ECMSystemsTryingToJamMe, rp._frequency, sensorSystemRadar._baseObject._crew, out sensorEcho, ignoreAspect: true);
				if (sensorEcho != null && flag)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool IsWithinFCRAltitudes(ObjectBase targetObject, Ammunition ammo)
	{
		if (ammo == null)
		{
			return false;
		}
		if (ammo._ap.GuidanceTypeForTarget(targetObject) != AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
		{
			return true;
		}
		CheckExternalRadarGuidance();
		for (int num = _vwp._associatedSensors.Count - 1; num >= 0; num--)
		{
			if (_vwp._associatedSensors[num] is SensorSystemRadar sensorSystemRadar && (ammo._ap._compatibleGuidanceSystems.Count <= 0 || ammo._ap._compatibleGuidanceSystems.Contains(_vwp._associatedSensors[num]._systemName)))
			{
				RadarParameters rp = sensorSystemRadar._radar._rp;
				if (targetObject.transform.position.y > rp._minAltitude && targetObject.transform.position.y < rp._maxAltitude)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool HasFreeGuidingSensors(string ammoID, ObjectBase targetObject, bool canTurnOnSensor = false)
	{
		return HasFreeGuidingSensors(_baseObject.getAmmunitionByName(ammoID), targetObject, canTurnOnSensor);
	}

	public bool HasFreeGuidingSensors(Ammunition ammo, ObjectBase targetObject, bool canTurnOnSensor = false)
	{
		if (!_baseObject.DoesEngageRequiresSensor(ammo._ap, targetObject))
		{
			return true;
		}
		bool result = false;
		if (_vwp._associatedSensors.Count < 1)
		{
			result = true;
		}
		foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
		{
			if (associatedSensor.Inoperable.Value)
			{
				continue;
			}
			if (associatedSensor.IsOn.Value || canTurnOnSensor)
			{
				if (ammo._ap._compatibleGuidanceSystems.Count > 0 && !ammo._ap._compatibleGuidanceSystems.Contains(associatedSensor._systemName))
				{
					continue;
				}
				if (associatedSensor is SensorSystemRadar sensorSystemRadar)
				{
					if ((sensorSystemRadar.HasFreeTargetChannels() || sensorSystemRadar.IsAimedAtTarget(targetObject)) && sensorSystemRadar.HasFreeWeaponChannels())
					{
						result = true;
						break;
					}
					continue;
				}
				if (associatedSensor.GetType() == typeof(SensorSystemSonar))
				{
					result = true;
					break;
				}
				if (associatedSensor is SensorSystemVisual)
				{
					if (associatedSensor.IsOn.Value || associatedSensor._systemType != SystemType.SensorPeriscope)
					{
						result = true;
						break;
					}
					if (associatedSensor._systemType == SystemType.SensorPeriscope && _vwp._autoEnableSensor && !associatedSensor.isBusy())
					{
						associatedSensor.Enable(allowMessage: false, turnOffAfterEngage: true);
						result = true;
						break;
					}
				}
			}
			else if (_vwp._autoEnableSensor && !associatedSensor.isBusy())
			{
				associatedSensor.Enable(allowMessage: false, turnOffAfterEngage: true);
			}
		}
		return result;
	}

	public string GetSensorAvaliability(ObjectBase targetObject)
	{
		string text = "\n";
		text += $"Search Count = {_vwp._associatedSearchSensors.Count}";
		if (_vwp._associatedSearchSensors.Count < 1)
		{
			text += "\nNo associated search sensors - no acquiring needed.";
		}
		foreach (SensorSystem associatedSearchSensor in _vwp._associatedSearchSensors)
		{
			text += " \n ";
			text = (associatedSearchSensor.IsOn.Value ? (text + associatedSearchSensor._sectionName + ": ON ") : (text + associatedSearchSensor._sectionName + ": <color=orange>OFF</color> "));
		}
		text += "\n";
		text = text + "\nFCR Count = " + _vwp._associatedSensors.Count + " ";
		if (_vwp._associatedSensors.Count < 1)
		{
			text += "No associated sensors - free to fire.";
		}
		foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
		{
			text += " \n ";
			if (!associatedSensor.IsOn.Value)
			{
				text = text + associatedSensor._sectionName + ": <color=orange>OFF</color> ";
				text += $" Channels: T = {associatedSensor._associatedTargets.Count}/{associatedSensor._targetChannels} W = {associatedSensor._associatedWeapons.Count}/{associatedSensor._weaponChannels}";
			}
			else
			{
				text = ((!associatedSensor.HasFreeTargetChannels() && !associatedSensor.IsAimedAtTarget(targetObject)) ? (text + associatedSensor._sectionName + ": busy ") : ((!associatedSensor.HasFreeWeaponChannels()) ? (text + associatedSensor._sectionName + ": busy ") : (text + associatedSensor._sectionName + ": FREE ")));
				text += $" Channels: T = {associatedSensor._associatedTargets.Count}/{associatedSensor._targetChannels} W = {associatedSensor._associatedWeapons.Count}/{associatedSensor._weaponChannels}";
			}
			text += $"  Solution: {associatedSensor._solution:0.##}";
		}
		return text + $"  Wires: {WiresInUse()} / {_vwp._numberOfWires}";
	}

	public bool IsAimedAtTarget(ObjectBase targetObject)
	{
		if (targetObject == null)
		{
			return false;
		}
		if (_targetObject == targetObject)
		{
			return true;
		}
		foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
		{
			if (associatedSensor.IsOn.Value && associatedSensor.IsAimedAtTarget(targetObject))
			{
				return true;
			}
		}
		return false;
	}

	public bool isAimedAtObject(ObjectBase targetObject)
	{
		_sensorPreLaunchAligned = true;
		if (targetObject == null)
		{
			return false;
		}
		if (_vwp._associatedSensors.Count == 0)
		{
			return true;
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
			return true;
		}
		foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
		{
			if (associatedSensor.IsOn.Value && (_ammoForEngage._ap._compatibleGuidanceSystems.Count <= 0 || _ammoForEngage._ap._compatibleGuidanceSystems.Contains(associatedSensor._systemName)))
			{
				if (associatedSensor.IsAimedAtTarget(targetObject))
				{
					return true;
				}
				if (_vwp._externalGuidingSystems.Contains(associatedSensor._systemName) && associatedSensor.HasFreeChannels())
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool HasFreeWeaponChannels(AmmunitionParameters ap)
	{
		foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
		{
			if ((ap._compatibleGuidanceSystems.Count <= 0 || ap._compatibleGuidanceSystems.Contains(associatedSensor._systemName)) && associatedSensor.IsOn.Value && associatedSensor.HasFreeWeaponChannels())
			{
				return true;
			}
		}
		return false;
	}

	public bool hasFreeWeaponChannel()
	{
		if (_vwp._associatedSensors != null)
		{
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
				return true;
			}
			foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
			{
				if (associatedSensor.Inoperable.Value || (_ammoForEngage._ap._compatibleGuidanceSystems.Count > 0 && !_ammoForEngage._ap._compatibleGuidanceSystems.Contains(associatedSensor._systemName)))
				{
					continue;
				}
				if (!associatedSensor.IsOn.Value)
				{
					if (_vwp._autoEnableSensor && !associatedSensor.isBusy())
					{
						associatedSensor.Enable(allowMessage: false, turnOffAfterEngage: true);
					}
				}
				else if (associatedSensor.HasFreeWeaponChannels())
				{
					return true;
				}
			}
		}
		return false;
	}

	public int WiresInUse()
	{
		int num = 0;
		foreach (WeaponBase launchedWeapon in _launchedWeapons)
		{
			if (!(launchedWeapon == null) && !launchedWeapon.IsDestroyed && launchedWeapon._onWire)
			{
				num++;
			}
		}
		return num;
	}

	public bool HasFreeWire()
	{
		return WiresInUse() < _vwp._numberOfWires;
	}

	public WeaponBase FirstWeaponOnWire()
	{
		foreach (WeaponBase launchedWeapon in _launchedWeapons)
		{
			if (!(launchedWeapon == null) && !launchedWeapon.IsDestroyed && launchedWeapon._onWire)
			{
				return launchedWeapon;
			}
		}
		return null;
	}

	public virtual void rotateMount(float direction, bool ignoreLimits = false)
	{
		_isInRestPosition = false;
		_lastUsageTime = GameTime.time;
		if (_vwp._isMountRotatable)
		{
			_vwp._mount.rotate(direction * _vwp._horizontalDegreesPerSecond, ignoreLimits);
		}
	}

	public virtual void rotateContainerVertical(float direction)
	{
		_isInRestPosition = false;
		_lastUsageTime = GameTime.time;
		if (!_vwp._areContainersRotatable)
		{
			return;
		}
		float num = 0f;
		float num2 = 0f;
		if (_vwp._areContainersJoinedTogether)
		{
			num = _vwp._containerBaseObject.transform.localEulerAngles.y;
			num2 = _vwp._containerBaseObject.transform.localEulerAngles.z;
			_vwp._containerBaseObject.transform.Rotate(direction * _vwp._verticalDegreesPerSecond * GameTime.deltaTime, num, num2, Space.Self);
			float num3 = _vwp._containerBaseObject.transform.localEulerAngles.x;
			if (num3 >= 180f)
			{
				num3 -= 360f;
			}
			num3 = 0f - num3;
			if (num3 > _vwp._elevationArc.y)
			{
				_vwp._containerBaseObject.transform.localEulerAngles = new Vector3(0f - _vwp._elevationArc.y, num, num2);
			}
			if (num3 < _vwp._elevationArc.x)
			{
				_vwp._containerBaseObject.transform.localEulerAngles = new Vector3(0f - _vwp._elevationArc.x, num, num2);
			}
		}
		else
		{
			_containers[Globals._testWSWeaponContainer - 1]._gunObject.transform.Rotate(direction * _vwp._verticalDegreesPerSecond * GameTime.deltaTime, num, num2, Space.Self);
			float num4 = _containers[Globals._testWSWeaponContainer - 1]._gunObject.transform.localEulerAngles.x;
			if (num4 >= 180f)
			{
				num4 -= 360f;
			}
			num4 = 0f - num4;
			if (num4 > _vwp._elevationArc.x)
			{
				_containers[Globals._testWSWeaponContainer - 1]._gunObject.transform.localEulerAngles = new Vector3(0f - _vwp._elevationArc.y, num, num2);
			}
			if (num4 < _vwp._elevationArc.x)
			{
				_containers[Globals._testWSWeaponContainer - 1]._gunObject.transform.localEulerAngles = new Vector3(0f - _vwp._elevationArc.x, num, num2);
			}
		}
	}

	public bool targetIsInFiringArc(Vector3 interceptPosition)
	{
		if (_vwp._firingArcs[0] == Vector2.zero)
		{
			return true;
		}
		Vector3 vector = interceptPosition - _baseObject.transform.position;
		float num = Mathf.Atan2(vector.x, vector.z) * 57.29578f - _baseObject.transform.eulerAngles.y;
		if (num > 180f)
		{
			num -= 360f;
		}
		else if (num < -180f)
		{
			num += 360f;
		}
		foreach (Vector2 firingArc in _vwp._firingArcs)
		{
			if (firingArc == Vector2.zero)
			{
				continue;
			}
			if (firingArc.x < firingArc.y)
			{
				if (firingArc.x < num && num < firingArc.y)
				{
					return true;
				}
			}
			else if (firingArc.y >= num || num >= firingArc.x)
			{
				return true;
			}
		}
		return false;
	}

	public bool targetIsInPreferedArc(Vector3 interceptPosition)
	{
		if (_vwp._preferredArcs[0] == Vector2.zero)
		{
			return false;
		}
		Vector3 vector = interceptPosition - _baseObject.transform.position;
		float num = Mathf.Atan2(vector.x, vector.z) * 57.29578f - _baseObject.transform.eulerAngles.y;
		if (num > 180f)
		{
			num -= 360f;
		}
		else if (num < -180f)
		{
			num += 360f;
		}
		foreach (Vector2 preferredArc in _vwp._preferredArcs)
		{
			if (preferredArc == Vector2.zero)
			{
				continue;
			}
			if (preferredArc.x < preferredArc.y)
			{
				if (preferredArc.x < num && num < preferredArc.y)
				{
					return true;
				}
			}
			else if (preferredArc.y >= num || num >= preferredArc.x)
			{
				return true;
			}
		}
		return false;
	}

	public bool isTargetCourseAcceptable(ObjectBase targetObject)
	{
		if (targetObject == null)
		{
			return false;
		}
		if (!targetObject.isWeapon() || _baseObject is Aircraft)
		{
			return true;
		}
		if (targetObject != _currentTargetObject)
		{
			_currentTargetObject = targetObject;
			_lastInterceptPosition = targetObject.getUnityPosition();
			return false;
		}
		float sqrMagnitude = (_mountObject.transform.position - _lastInterceptPosition).sqrMagnitude;
		float sqrMagnitude2 = (_mountObject.transform.position - targetObject.getUnityPosition()).sqrMagnitude;
		_lastInterceptPosition = targetObject.getUnityPosition();
		return sqrMagnitude2 < sqrMagnitude;
	}

	public virtual bool alignToTarget(ObjectBase targetObject, bool useFixedLaunchAngle, int containerIndexToUse)
	{
		return alignToTarget(targetObject.getUnityPosition(), useFixedLaunchAngle, containerIndexToUse);
	}

	public virtual bool alignToTarget(Vector3 targetObjectPosition, bool useFixedLaunchAngle, int containerIndexToUse, bool preciseAim = false)
	{
		_isInRestPosition = false;
		_lastUsageTime = GameTime.time;
		Transform transform = _vwp._mountObject.transform;
		Vector3 targetDirection = targetObjectPosition - transform.position;
		bool num = _vwp._mount.rotate(targetDirection, _vwp._horizontalDegreesPerSecond, preciseAim);
		float num2 = Mathf.Asin(transform.forward.y) * 57.29578f;
		float num3 = Mathf.Asin(targetDirection.normalized.y) * 57.29578f - num2;
		if (useFixedLaunchAngle)
		{
			num3 = _vwp._fixVerticalLaunchAngle - num2;
		}
		num3 += _vwp._additionalFixVerticalLaunchAngle;
		if (num3 < _vwp._elevationArc.x)
		{
			num3 = _vwp._elevationArc.x;
		}
		if (num3 > _vwp._elevationArc.y)
		{
			num3 = _vwp._elevationArc.y;
		}
		num3 = 0f - num3;
		bool flag;
		if (_vwp._areContainersRotatable)
		{
			flag = ((!_vwp._areContainersJoinedTogether) ? RotateWeaponToAngle(_containers[containerIndexToUse]._gunObject.transform, num3, horizontalRotation: false, preciseAim) : RotateWeaponToAngle(_vwp._containerBaseObject.transform, num3, horizontalRotation: false, preciseAim));
		}
		else
		{
			_alignmentDeltas += "| 0/0)";
			flag = true;
		}
		return num && flag;
	}

	public void EndEngageTask(bool endAlignment = true)
	{
		DM.I(_baseObject.getUIDAndName() + ": End engage task");
		_executingEngageTask = false;
		_isAutoEngaging = false;
		_asssociatedSensorsAllOnTime = -1f;
		if (GetType() == typeof(WeaponSystemLauncher))
		{
			_engageState = EngageState.Idle;
		}
		if (this is WeaponSystemGun weaponSystemGun)
		{
			_engageState = EngageState.Idle;
			weaponSystemGun.CleanData();
		}
		if (this is WeaponSystemHardpoint weaponSystemHardpoint)
		{
			weaponSystemHardpoint._offsetCalculated = false;
		}
		_takenEngageTask = null;
		if (endAlignment)
		{
			_baseObject.DelayedEndAlignment(_alignmentUID, 0f, "EndEngageTask");
		}
		_isTargetObjectRequired = false;
		_targetObject = null;
		_sensorPreLaunchAligned = false;
		_horizonRange = 0f;
		_baseObject._currentLoadoutCapabilities = _baseObject.GetLoadoutCapabilities();
		foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
		{
			if (associatedSensor.GetType() == typeof(SensorSystemRadar))
			{
				((SensorSystemRadar)associatedSensor)._preLaunchTargetObject = null;
			}
		}
	}

	public void ReturnEngageTask()
	{
		if (_ammoForEngage != null)
		{
			EngageTask engageTask = _baseObject.InsertEngageTask(_ammoForEngage._ap._ammunitionFileName, _targetObject, _targetPosition, 1, _engageTaskPriority, _isAutoEngaging);
			engageTask._uid = _alignmentUID;
			engageTask._ignoreIfUndetected = _ignoreIfUndetected;
		}
		EndEngageTask();
		_baseObject.EndAlignment(_alignmentUID, "ReturnEngageTask");
	}

	public virtual void ClearEngageTask()
	{
		_executingEngageTask = false;
		_targetObject = null;
		_sensorPreLaunchAligned = false;
		_horizonRange = 0f;
		foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
		{
			if (associatedSensor is SensorSystemRadar sensorSystemRadar)
			{
				sensorSystemRadar._preLaunchTargetObject = null;
				if (this is WeaponSystemGun)
				{
					sensorSystemRadar.ClearGuiding();
				}
			}
		}
	}

	public bool openSystem()
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

	public void closeSystem(float delay)
	{
		if (_systemState != SystemState.OpeningSystem && _systemState != SystemState.ClosingSystem)
		{
			if (_closeSystemAnimation == null || _systemState != SystemState.SystemOpen)
			{
				_systemState = SystemState.SystemClosed;
			}
			else if (_closeSystemAnimation != null)
			{
				_closeStartTime = GameTime.time;
				_closeStartDelay = delay;
				_triggerCloseAnimation = true;
			}
		}
	}

	public bool moveToRestPosition()
	{
		bool flag = true;
		foreach (WeaponContainer container in _containers)
		{
			if (container._playCloseAnimation || container._playOpenAnimation)
			{
				flag = false;
				break;
			}
		}
		if (!flag)
		{
			return false;
		}
		if (_vwp._waitWhileSensorIsGuiding)
		{
			foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
			{
				if (associatedSensor._isGuiding)
				{
					return false;
				}
			}
		}
		foreach (WeaponSystem sharesMountWithSystem in _vwp._sharesMountWithSystems)
		{
			if (sharesMountWithSystem._executingEngageTask)
			{
				return false;
			}
			bool flag2 = false;
			if (sharesMountWithSystem._loadingState == LoadingState.Loading || sharesMountWithSystem._loadingState == LoadingState.Unloading)
			{
				flag2 = true;
			}
			if (GameTime.time - sharesMountWithSystem._lastLaunchTime < sharesMountWithSystem._vwp._moveToRestPositionTime || flag2)
			{
				return false;
			}
		}
		if (IsHot)
		{
			return moveToPosition(_vwp._restAngles.x, _vwp._restAngles.y);
		}
		return moveToPosition(_vwp._coldAngles.x, _vwp._coldAngles.y);
	}

	public bool moveToLoadPosition()
	{
		if (_vwp._waitWhileSensorIsGuiding)
		{
			foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
			{
				if (associatedSensor._isGuiding)
				{
					return false;
				}
			}
		}
		bool flag = true;
		foreach (WeaponContainer container in _containers)
		{
			if (container._playCloseAnimation || container._playOpenAnimation)
			{
				flag = false;
				break;
			}
		}
		if (!flag)
		{
			return false;
		}
		foreach (WeaponSystem sharesMountWithSystem in _vwp._sharesMountWithSystems)
		{
			if (sharesMountWithSystem._executingEngageTask)
			{
				return false;
			}
			bool flag2 = false;
			if (sharesMountWithSystem._loadingState == LoadingState.Loading || sharesMountWithSystem._loadingState == LoadingState.Unloading)
			{
				flag2 = true;
			}
			if (GameTime.time - sharesMountWithSystem._lastLaunchTime < sharesMountWithSystem._vwp._moveToRestPositionTime || flag2)
			{
				return false;
			}
		}
		_isInRestPosition = false;
		_lastUsageTime = GameTime.time;
		if (_vwp._anyLoadAngle)
		{
			return true;
		}
		return moveToPosition(_vwp._loadAngles.x, _vwp._loadAngles.y);
	}

	protected bool moveToPosition(float horizontalAngle, float verticalAngle)
	{
		bool flag = !_vwp._isMountRotatable || RotateWeaponToAngle(_vwp._mountObject.transform, horizontalAngle, horizontalRotation: true);
		bool flag2;
		if (_vwp._areContainersRotatable)
		{
			if (_vwp._areContainersJoinedTogether)
			{
				flag2 = RotateWeaponToAngle(_vwp._containerBaseObject.transform, verticalAngle, horizontalRotation: false);
			}
			else
			{
				flag2 = true;
				for (int i = 0; i < _containers.Count; i++)
				{
					if (!RotateWeaponToAngle(_containers[i]._gunObject.transform, verticalAngle, horizontalRotation: false))
					{
						flag2 = false;
					}
				}
			}
		}
		else
		{
			flag2 = true;
		}
		return flag && flag2;
	}

	protected bool RotateWeaponToAngle(Transform transform, float targetAngle, bool horizontalRotation, bool preciseAim = false)
	{
		Vector3 localEulerAngles = transform.localEulerAngles;
		float num = 0f;
		if (horizontalRotation)
		{
			float num2 = Mathf.MoveTowardsAngle(localEulerAngles.y, targetAngle, _vwp._horizontalDegreesPerSecond * GameTime.deltaTime);
			num = Mathf.DeltaAngle(localEulerAngles.y, num2);
			transform.localEulerAngles = new Vector3(localEulerAngles.x, num2, localEulerAngles.z);
		}
		else
		{
			float num3 = Mathf.MoveTowardsAngle(localEulerAngles.x, targetAngle, _vwp._verticalDegreesPerSecond * GameTime.deltaTime);
			num = Mathf.DeltaAngle(localEulerAngles.x, num3);
			transform.localEulerAngles = new Vector3(num3, localEulerAngles.y, localEulerAngles.z);
		}
		_alignmentDeltas += $"| {num:F2}/0.2)";
		if (preciseAim)
		{
			return Mathf.Abs(num) < 0.1f;
		}
		return Mathf.Abs(num) < 0.2f;
	}

	protected bool moveToPosition_qu(float horizontalAngle, float verticalAngle)
	{
		if (!_vwp._isMountRotatable)
		{
			return true;
		}
		Transform transform = _vwp._mount.getMountObject().transform;
		Transform transform2 = _vwp._containerBaseObject.transform;
		float num = transform.localEulerAngles.y;
		float num2 = transform2.localEulerAngles.x;
		if (num > 180f)
		{
			num -= 360f;
		}
		else if (num < -180f)
		{
			num += 360f;
		}
		if (num2 > 180f)
		{
			num2 -= 360f;
		}
		else if (num2 < -180f)
		{
			num2 += 360f;
		}
		float num3 = num;
		float num4 = num2;
		float num5 = _vwp._horizontalDegreesPerSecond * GameTime.deltaTime / 10f;
		float num6 = _vwp._verticalDegreesPerSecond * GameTime.deltaTime / 10f;
		DM.I($"Enter: angles = ({horizontalAngle}/{verticalAngle}) Mount = {num} Gun = {num2} Speed = ({num5}/{num6})");
		if (num < horizontalAngle - 0.2f)
		{
			num3 += num5;
			if (num3 > horizontalAngle)
			{
				num3 = horizontalAngle;
			}
		}
		else if (num > horizontalAngle + 0.2f)
		{
			num3 -= num5;
			if (num3 < horizontalAngle)
			{
				num3 = horizontalAngle;
			}
		}
		else
		{
			num3 = horizontalAngle;
		}
		if (num2 < verticalAngle - 0.2f)
		{
			num4 += num6;
			if (num4 > verticalAngle)
			{
				num4 = verticalAngle;
			}
		}
		else if (num2 > verticalAngle + 0.2f)
		{
			num4 -= num6;
			if (num4 < verticalAngle)
			{
				num4 = verticalAngle;
			}
		}
		else
		{
			num4 = verticalAngle;
		}
		float angle = num3 - num;
		float angle2 = num4 - num2;
		float f = horizontalAngle - num3;
		bool flag = Mathf.Abs(verticalAngle - num4) < 0.1f;
		if (Mathf.Abs(f) < 0.1f && flag)
		{
			return true;
		}
		transform.rotation = Quaternion.AngleAxis(angle, transform.up);
		transform2.rotation = Quaternion.AngleAxis(angle2, transform2.right);
		DM.I($"Exit: angles = ({horizontalAngle}/{verticalAngle}) Mount = {num3} Gun = {num4}");
		return false;
	}

	public bool CheckForMissileGroup(AmmunitionParameters ap)
	{
		if (ap._type != Ammunition.Type.Missile)
		{
			return false;
		}
		if (ap._maxGroupSize < 2)
		{
			return false;
		}
		float num = 275.59497f;
		if (num < ap._seekerActiveRange)
		{
			num = ap._seekerActiveRange;
		}
		if (num < ap._seekerPassiveRange)
		{
			num = ap._seekerPassiveRange;
		}
		num *= num;
		GeoPosition other = ((_targetObject != null) ? _targetObject._geoPosition : Utils.worldPositionFromUnityToLongLat(_targetPosition, Globals._currentCenterTile));
		foreach (MissileGroup missileGroup in _baseObject._taskforce._missileGroups)
		{
			if (missileGroup._leader._ap._ammunitionFileName == ap._ammunitionFileName && missileGroup.Count != missileGroup._maxMembers && !(missileGroup._leader.AimPointGeoPosition.UnityDistance(other) > num) && !((missileGroup._leader.transform.position - _mountObject.transform.position).sqrMagnitude > ap._groupJoinRangeUnitySq))
			{
				return true;
			}
		}
		return false;
	}

	public string GetStatus()
	{
		if (base.Inoperable.Value)
		{
			return Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Inoperable");
		}
		if (_isOffline)
		{
			return Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Offline");
		}
		if (_isEmpty)
		{
			return Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Empty");
		}
		float num = _remainingReloadTime;
		if (this is WeaponSystemChaff weaponSystemChaff)
		{
			num = weaponSystemChaff._timeToNextLaunch;
		}
		if (num > 0f)
		{
			float num2 = Mathf.Floor(num / 60f);
			return string.Format("{0}{1:00}:{2:00}", Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Reloading"), num2, num - num2 * 60f);
		}
		bool flag = true;
		if (_vwp._associatedSensors.Count > 0)
		{
			flag = false;
			foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
			{
				if (associatedSensor.IsOn.Value)
				{
					flag = true;
					break;
				}
			}
		}
		if (!flag)
		{
			return Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "SensorsOff");
		}
		if (_executingEngageTask || _isAutoEngaging)
		{
			if (_engageState != EngageState.NotObserved)
			{
				return Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Engaging") + " (" + Singleton<LanguageResourceHandler>.Instance.getText("Status", $"Weapon_{_engageState}") + ")";
			}
			return Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Engaging");
		}
		WeaponBase weaponBase = FirstWeaponOnWire();
		if (weaponBase != null)
		{
			return Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "StatusGuiding").Replace("${WeaponName}", weaponBase._ap._displayedName);
		}
		string text = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Ready");
		bool flag2 = _weaponType == WeaponType.Noisemaker || _weaponType == WeaponType.Chaff;
		bool flag3 = (_weaponType == WeaponType.Gun || _weaponType == WeaponType.CIWS) && _vwp._internalAmmoCount <= 1;
		if (_vwp._internalAmmoCount > 0 && !flag2 && !flag3)
		{
			text += $"({_vwp._internalAmmoCount}/{_vwp._maxAmmoCount})";
		}
		return text;
	}

	internal void WeaponDestroyed(WeaponBase weaponBase)
	{
		if (_launchedWeapons.Contains(weaponBase))
		{
			_launchedWeapons.Remove(weaponBase);
		}
	}

	public void CheckExternalRadarGuidance()
	{
		if (_vwp._externalGuidingSystems.Count <= 0 || !(GameTime.time > _lastSearchForSensorTime))
		{
			return;
		}
		_lastSearchForSensorTime = GameTime.time + 30f;
		bool flag = false;
		foreach (SensorSystem associatedSensor in _vwp._associatedSensors)
		{
			if (associatedSensor != null && !associatedSensor.Inoperable.Value && !associatedSensor._isGuiding)
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			SearchForExternalGuidance();
		}
	}

	public void SearchForExternalGuidance()
	{
		float num = _vwp._externalSensorSearchRadius * _vwp._externalSensorSearchRadius;
		foreach (ObjectBase taskforceObject in _baseObject._taskforce.TaskforceObjects)
		{
			if (taskforceObject == null || taskforceObject.IsDestroyed || !taskforceObject.isUnit() || (taskforceObject.transform.position - _baseObject.transform.position).sqrMagnitude > num)
			{
				continue;
			}
			foreach (SensorSystem sensorSystem in taskforceObject._obp._sensorSystems)
			{
				if (_vwp._externalGuidingSystems.Contains(sensorSystem._systemName) && !_vwp._associatedSensors.Contains(sensorSystem))
				{
					_vwp._associatedSensors.Add(sensorSystem);
				}
			}
		}
	}

	public void StartReload()
	{
		_isReloading = true;
		_reloadStartTime = GameTime.time;
		foreach (WeaponSystem sharesReloadWithSystem in _vwp._sharesReloadWithSystems)
		{
			sharesReloadWithSystem._isReloading = true;
			sharesReloadWithSystem._reloadStartTime = GameTime.time;
		}
	}
}
