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
		MissileCanNotReachTarget,
		Firing
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

	protected double _reloadStartTime;

	public float _remainingReloadTime;

	public bool _isReloading;

	public bool _isEmpty;

	protected double _lastUsageTime;

	public bool _isInRestPosition = true;

	public double _restPositionReachedTime;

	public List<WeaponContainer> _containers = new List<WeaponContainer>();

	protected float _nextTargetPointSwitchTime;

	public bool _executingEngageTask;

	public bool _cantInterruptEngage;

	public int _engageTaskPriority;

	public double _engageStartTime;

	public float _engageDelay;

	protected double _asssociatedSensorsAllOnTime;

	public EngageTask _takenEngageTask;

	public Vector3 _targetPosition = Vector3.zero;

	public Vector3 _offsetVector = Vector3.zero;

	public LandUnitParameters.TargetPoint _currentTargetPoint;

	protected Vector3Event _relocationEvent;

	public int _alignmentUID;

	public Ammunition _ammoForEngage;

	public SensorSystemLaserDesignator _engageLaserDesignator;

	public double _lastLaunchTime;

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

	private bool _loggedMissingRotationObject;

	protected float _horizonRange;

	public float _currentMaxLaunchRange = -1f;

	private bool _isHot;

	public bool _ignoreIfUndetected;

	public bool _reloadableWithoutMagazine;

	public double _lastSearchForSensorTime;

	public double _targetAcquisitionStartTime = -1000.0;

	protected bool _isPlayingOpenAnim;

	protected bool _isPlayingCloseAnim;

	protected bool _isPlayingUnloadAnim;

	protected bool _isPlayingLoadAnim;

	public ObjectCodeAnimation _unloadAmmunitionAnimation;

	public ObjectCodeAnimation _loadAmmunitionAnimation;

	protected bool _triggerCloseAnimation;

	protected double _closeStartTime;

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
				_lastLaunchTime = GameTime.missionElapsedTime - (double)_vwp._magazineReloadTime - (double)_remainingReloadTime;
				_reloadStartTime = GameTime.missionElapsedTime - (double)_vwp._magazineReloadTime + (double)_remainingReloadTime;
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
		string[] array2;
		if (this is WeaponSystemLauncher weaponSystemLauncher && _vwp._associatedMagazine == null)
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string[] array3 = array2[i].Split(",");
				if (array3.Length >= 2 && int.TryParse(array3[1], out var result) && result >= 0)
				{
					if (dictionary.ContainsKey(array3[0]))
					{
						dictionary[array3[0]] += result;
					}
					else
					{
						dictionary[array3[0]] = result;
					}
				}
			}
			weaponSystemLauncher.ApplyLoadoutOverride(dictionary);
			return;
		}
		array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			string[] array4 = array2[i].Split(",");
			string key = array4[0];
			int result2 = -1;
			int.TryParse(array4[1], out result2);
			if (result2 < 0)
			{
				continue;
			}
			if (this is WeaponSystemHardpoint weaponSystemHardpoint)
			{
				if (_loadedAmmunitionCount.ContainsKey(key))
				{
					int num2 = _loadedAmmunitionCount[key] - result2;
					for (int j = 0; j < weaponSystemHardpoint._weapons.Count; j++)
					{
						if (num2 < 1)
						{
							break;
						}
						if (!(weaponSystemHardpoint._weapons[j]._ap._ammunitionFileName != array4[0]))
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
				int num4 = num3 - result2;
				foreach (WeaponContainer container2 in _containers)
				{
					if (num4 < 1)
					{
						break;
					}
					num4 -= container2.destroyWeapons(num4);
				}
			}
			_loadedAmmunitionCount[key] = result2;
		}
	}

	public void ReseatPreloadedAmmunitionFromMagazine()
	{
		if (_vwp._associatedMagazine == null || !_vwp._hasPreloadedAmmunition || _vwp._perContainerReload || _vwp._loadingType == LoadingType.SpawnWhenNeeded)
		{
			return;
		}
		foreach (KeyValuePair<string, int> item in new List<KeyValuePair<string, int>>(_loadedAmmunitionCount))
		{
			if (item.Value > 0)
			{
				decreaseLoadedAmmoCount(item.Key, item.Value);
			}
		}
		foreach (WeaponContainer container in _containers)
		{
			container.ClearWeapons();
		}
		_ammunitionOnWeapon = null;
		Ammunition preloadAmmunition = _vwp._associatedMagazine.getPreloadAmmunition(_vwp._preloadAmmunitionIndex - 1);
		if (preloadAmmunition == null || _vwp._associatedMagazine.getAmmunitionCount(preloadAmmunition._ap._ammunitionFileName) <= 0)
		{
			return;
		}
		foreach (WeaponContainer container2 in _containers)
		{
			container2.load(preloadAmmunition, initialLoad: true);
		}
	}

	public override void init()
	{
		base.init();
		_relocationEvent = new Vector3Event();
		_relocationEvent.AddListener(onRelocate);
		Singleton<RelocationHandler>.Instance.addToEventsList(_relocationEvent);
		_lastUsageTime = -1000.0;
		_lastLaunchTime = -1000.0;
		_reloadStartTime = -1000.0;
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
			_lastUsageTime = GameTime.missionElapsedTime;
			if (IsTubeHoldingWire())
			{
				_reloadStartTime += GameTime.deltaTime;
				return;
			}
			float num = (float)(GameTime.missionElapsedTime - _reloadStartTime);
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
				if (!_vwp._perContainerReload && ((_weaponType == WeaponType.Torpedo && _vwp._isMountRotatable) || _vwp._systemName == "ASROC" || _vwp._forceMoveToLoadPosition) && _remainingReloadTime > 0f && num > 2f && !moveToLoadPosition())
				{
					return;
				}
				if (_weaponType == WeaponType.RBU || _vwp._loadingType == LoadingType.ReloadsLowerDeck)
				{
					((WeaponSystemLauncher)this).updateLoadSystem();
					if (((WeaponSystemLauncher)this)._state != 0 && _remainingReloadTime < 0f)
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
			if (!(this is WeaponSystemLauncher weaponSystemLauncher2) || weaponSystemLauncher2._loadAmmunitionAnimation != null || !_vwp._hasPreloadedAmmunition || _vwp._loadingType == LoadingType.SpawnWhenNeeded || _vwp._associatedMagazine == null)
			{
				return;
			}
			Ammunition preloadAmmunition = _vwp._associatedMagazine.getPreloadAmmunition(_vwp._preloadAmmunitionIndex - 1);
			{
				foreach (WeaponContainer container in _containers)
				{
					container.load(preloadAmmunition);
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
			if ((float)(GameTime.missionElapsedTime - _lastUsageTime) - GameTime.deltaTime > _vwp._moveToRestPositionTime && !flag && moveToRestPosition())
			{
				_isInRestPosition = true;
				_restPositionReachedTime = GameTime.missionElapsedTime;
			}
		}
		else if (IsHot && !_executingEngageTask && GameTime.missionElapsedTime - _restPositionReachedTime > 1800.0)
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

	public virtual bool CanForceReload()
	{
		if (_isReloading || _vwp._associatedMagazine == null || _ammoForEngage == null)
		{
			return false;
		}
		string ammunitionFileName = _ammoForEngage._ap._ammunitionFileName;
		if (getMagazineAmmoCount(ammunitionFileName) < 1)
		{
			return false;
		}
		return getLoadedAmmoCount(ammunitionFileName) < _vwp._maxAmmoCount;
	}

	public virtual void ForceReload()
	{
		if (CanForceReload())
		{
			string ammunitionFileName = _ammoForEngage._ap._ammunitionFileName;
			int loadedAmmoCount = getLoadedAmmoCount(ammunitionFileName);
			if (loadedAmmoCount > 0)
			{
				decreaseLoadedAmmoCount(ammunitionFileName, loadedAmmoCount);
				_vwp._associatedMagazine.increaseAmmunitionCount(ammunitionFileName, loadedAmmoCount);
			}
			StartReload();
		}
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

	public virtual int getLoadedAmmoCount(string ammunitionName)
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

	public bool CheckTargetIsDetectableByFCR(ObjectBase.SensorRangeOptions options = default(ObjectBase.SensorRangeOptions))
	{
		for (int num = _vwp._associatedSensors.Count - 1; num >= 0; num--)
		{
			if (_vwp._associatedSensors[num] is SensorSystemRadar sensorSystemRadar && sensorSystemRadar.HasFreeTargetChannels(_targetObject) && sensorSystemRadar.HasFreeWeaponChannels() && CheckTargetIsDetectableByRadar(sensorSystemRadar, _targetObject, options))
			{
				return true;
			}
		}
		return false;
	}

	public bool CheckTargetIsDetectableByRadar(SensorSystemRadar ssr)
	{
		return CheckTargetIsDetectableByRadar(ssr, _targetObject);
	}

	public bool CheckTargetIsDetectableByRadar(SensorSystemRadar ssr, ObjectBase targetObject, ObjectBase.SensorRangeOptions options = default(ObjectBase.SensorRangeOptions))
	{
		if (!ssr._radar.IsDetectableTarget_Unconditional(targetObject))
		{
			return false;
		}
		if (!ssr._radar.IsDetectableTarget_Conditional(targetObject))
		{
			return false;
		}
		Vector3 position = ssr._mountObject.transform.position;
		position.y = targetObject.transform.position.y;
		SensorEcho sensorEcho;
		bool result = RadarCalculator.createRadarEcho(position, targetObject, ssr, out sensorEcho, ignoreAspect: true, options);
		if (sensorEcho == null)
		{
			return false;
		}
		return result;
	}

	public float FCRDetectionRange(ObjectBase targetObject, Ammunition ammo, out ObjectBase.EngagementIssue? issue, ObjectBase.SensorRangeOptions options = default(ObjectBase.SensorRangeOptions))
	{
		issue = null;
		bool ignoreLookdownPenalty = options.IgnoreLookdownPenalty;
		if (ammo == null)
		{
			return -1f;
		}
		bool flag = ammo._ap.GuidanceTypeForTarget(targetObject) == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming;
		if (ammo._ap._type == Ammunition.Type.Projectile)
		{
			return -1f;
		}
		if (!flag && !ammo._ap._midCourseCorrection.AllowsLauncherGuidance)
		{
			return -1f;
		}
		float num = -1f;
		float num2 = float.MaxValue;
		CheckExternalRadarGuidance();
		for (int num3 = _vwp._associatedSensors.Count - 1; num3 >= 0; num3--)
		{
			if (_vwp._associatedSensors[num3] == null || _vwp._associatedSensors[num3]._baseObject == null)
			{
				_vwp._associatedSensors.RemoveAt(num3);
			}
			else if (!_vwp._associatedSensors[num3].Inoperable.Value && _vwp._associatedSensors[num3] is SensorSystemRadar sensorSystemRadar && _vwp._associatedSensors[num3].CanGuideAmmo(ammo._ap))
			{
				RadarParameters rp = sensorSystemRadar._radar._rp;
				ObjectBase.SensorRangeOptions options2 = options;
				options2.IgnoreAltitudeBrackets = true;
				SensorSystem.SensorDistanceData range = sensorSystemRadar.GetRange(targetObject, options2);
				num2 = range.Distance;
				if (!flag)
				{
					num2 = RadarCalculator.GetFCRDetectionRange(sensorSystemRadar.SensorPosition, ammo._ap._maxRCS, rp._waveLength, rp._gainFactor, rp._peakPower, sensorSystemRadar._baseObject._ECMSystemsTryingToJamMe, rp._frequency, sensorSystemRadar._baseObject._crew, rp._jamResistance, null, options.IgnoreTargetJamming ? targetObject : null, options.IgnoreAllOffensiveJamming);
					if (!ignoreLookdownPenalty && range.Issue == SensorSystem.SensorIssue.LookDown)
					{
						num2 *= sensorSystemRadar._lookDownMultiplier;
					}
				}
				num2 = Mathf.Min(num2, range.Distance);
				if (num2 > rp._maxRange)
				{
					num2 = rp._maxRange;
				}
				if (num < num2)
				{
					num = num2;
					switch (range.Issue)
					{
					case SensorSystem.SensorIssue.MinAlt:
					case SensorSystem.SensorIssue.MaxAlt:
						issue = ObjectBase.EngagementIssue.OutOfAltitude;
						break;
					case SensorSystem.SensorIssue.LookDown:
						issue = ObjectBase.EngagementIssue.InLookdown;
						break;
					default:
						issue = null;
						break;
					}
				}
			}
		}
		return num;
	}

	public bool CanSearchSensorsSeeTarget(ObjectBase targetObject, out float guidanceSystemPenalty, ObjectBase.SensorRangeOptions options = default(ObjectBase.SensorRangeOptions))
	{
		return CanSensorsSeeTarget(_vwp._associatedSearchSensors, targetObject, out guidanceSystemPenalty, options);
	}

	public bool CrewVisionBlocked(AmmunitionParameters ap, ObjectBase targetObject)
	{
		return _baseObject.CrewVisionBlocksAmmo(ap, targetObject);
	}

	public bool IsVisionRequirementMet(ObjectBase targetObject, out float guidanceSystemPenalty, ObjectBase.SensorRangeOptions options = default(ObjectBase.SensorRangeOptions), AmmunitionParameters engageAp = null)
	{
		guidanceSystemPenalty = 0f;
		if (targetObject != null && !targetObject.isAirTarget())
		{
			CanSearchSensorsSeeTarget(targetObject, out guidanceSystemPenalty, options);
			return !CrewVisionBlocked(engageAp ?? _ammoForEngage?._ap, targetObject);
		}
		switch (Singleton<OptionsManager>.Instance.RequireVisionToEngage)
		{
		case VisionToEngageMode.AnySensor:
			return CanSensorsSeeTarget(_baseObject._obp._sensorSystems, targetObject, out guidanceSystemPenalty, options);
		case VisionToEngageMode.AssociatedSensors:
			if (_vwp._associatedSearchSensors.Count > 0)
			{
				return CanSensorsSeeTarget(_vwp._associatedSearchSensors, targetObject, out guidanceSystemPenalty, options);
			}
			return CanSensorsSeeTarget(_baseObject._obp._sensorSystems, targetObject, out guidanceSystemPenalty, options);
		default:
			return true;
		}
	}

	public bool CanSensorsSeeTarget(IList<SensorSystem> sensors, ObjectBase targetObject, out float guidanceSystemPenalty, ObjectBase.SensorRangeOptions options = default(ObjectBase.SensorRangeOptions))
	{
		guidanceSystemPenalty = 0f;
		if (targetObject == null)
		{
			return true;
		}
		if (sensors == null || sensors.Count == 0)
		{
			return true;
		}
		foreach (SensorSystem sensor in sensors)
		{
			if (!sensor.Inoperable.Value && sensor.IsOn.Value && sensor is SensorSystemRadar sensorSystemRadar && (sensorSystemRadar.CanSeeAirTargets || !targetObject.isAirTarget()) && CheckTargetIsDetectableByRadar(sensorSystemRadar, targetObject, options))
			{
				return true;
			}
		}
		ObjectBase.SensorRangeOptions options2 = options;
		options2.IgnoreCloudCover = true;
		foreach (SensorSystem sensor2 in sensors)
		{
			if (!sensor2.Inoperable.Value && sensor2.IsOn.Value && !(sensor2 is SensorSystemRadar))
			{
				Vector3 vector = ((sensor2._mountObject != null) ? sensor2._mountObject.transform.position : sensor2._baseObject.transform.position);
				float magnitude = (targetObject.transform.position - vector).magnitude;
				if (sensor2.GetRange(targetObject, options2).Distance >= magnitude)
				{
					guidanceSystemPenalty = sensor2._weaponGuidancePenalty;
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
		if (targetObject != null && !targetObject.isAirTarget())
		{
			return true;
		}
		CheckExternalRadarGuidance();
		for (int num = _vwp._associatedSensors.Count - 1; num >= 0; num--)
		{
			if (_vwp._associatedSensors[num] is SensorSystemRadar && _vwp._associatedSensors[num].CanGuideAmmo(ammo._ap))
			{
				return true;
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
				if (!associatedSensor.CanGuideAmmo(ammo._ap))
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
		return text + string.Format("  Wires: {0} / {1}", (_baseObject != null) ? _baseObject.WiresInUse() : WiresInUse(), (_baseObject != null && _baseObject._maxWireCount < int.MaxValue) ? _baseObject._maxWireCount.ToString() : "∞");
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
			if (associatedSensor.IsOn.Value && associatedSensor.CanGuideAmmo(_ammoForEngage._ap))
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
			if (associatedSensor.CanGuideAmmo(ap) && associatedSensor.IsOn.Value && associatedSensor.HasFreeWeaponChannels())
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
				if (associatedSensor.Inoperable.Value || !associatedSensor.CanGuideAmmo(_ammoForEngage._ap))
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
		if (!(_baseObject == null))
		{
			return _baseObject.HasFreeWire();
		}
		return true;
	}

	public bool IsTubeHoldingWire()
	{
		if (_weaponType == WeaponType.Torpedo)
		{
			return FirstWeaponOnWire() != null;
		}
		return false;
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
		_lastUsageTime = GameTime.missionElapsedTime;
		if (_vwp._isMountRotatable)
		{
			_vwp._mount.rotate(direction * _vwp._horizontalDegreesPerSecond, ignoreLimits);
		}
	}

	public virtual void rotateContainerVertical(float direction)
	{
		_isInRestPosition = false;
		_lastUsageTime = GameTime.missionElapsedTime;
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

	public virtual bool targetIsInFiringArc(Vector3 interceptPosition)
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
		return RelativeBearingInFiringArcs(num);
	}

	protected bool RelativeBearingInFiringArcs(float relBearingToTarget)
	{
		foreach (Vector2 firingArc in _vwp._firingArcs)
		{
			if (firingArc == Vector2.zero)
			{
				continue;
			}
			if (firingArc.x < firingArc.y)
			{
				if (firingArc.x < relBearingToTarget && relBearingToTarget < firingArc.y)
				{
					return true;
				}
			}
			else if (firingArc.y >= relBearingToTarget || relBearingToTarget >= firingArc.x)
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
		_alignmentDeltas = string.Empty;
		_isInRestPosition = false;
		_lastUsageTime = GameTime.missionElapsedTime;
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
		_asssociatedSensorsAllOnTime = -1.0;
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
		if (endAlignment && !OtherSystemStillOnAlignment())
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

	protected bool OtherSystemStillOnAlignment()
	{
		if (_baseObject?._obp == null)
		{
			return false;
		}
		foreach (WeaponSystem weaponSystem in _baseObject._obp._weaponSystems)
		{
			if (weaponSystem != this && weaponSystem._executingEngageTask && weaponSystem._alignmentUID == _alignmentUID)
			{
				return true;
			}
		}
		return false;
	}

	public void ReturnEngageTask()
	{
		if (_ammoForEngage != null && (!_isTargetObjectRequired || _targetObject != null))
		{
			int shotsToFire = ((_takenEngageTask == null) ? 1 : Mathf.Max(1, _takenEngageTask._shotsToFire));
			EngageTask engageTask = _baseObject.InsertEngageTask(_ammoForEngage._ap._ammunitionFileName, _targetObject, _targetPosition, shotsToFire, _engageTaskPriority, _isAutoEngaging);
			engageTask._uid = _alignmentUID;
			engageTask._ignoreIfUndetected = _ignoreIfUndetected;
			if (_takenEngageTask != null)
			{
				engageTask._solutionChannelOwner = _takenEngageTask._solutionChannelOwner;
				engageTask._solutionDirector = _takenEngageTask._solutionDirector;
				engageTask._solutionStartTime = _takenEngageTask._solutionStartTime;
			}
		}
		EndEngageTask();
		if (!OtherSystemStillOnAlignment())
		{
			_baseObject.EndAlignment(_alignmentUID, "ReturnEngageTask");
		}
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
				_closeStartTime = GameTime.missionElapsedTime;
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
			if ((float)(GameTime.missionElapsedTime - sharesMountWithSystem._lastLaunchTime) < sharesMountWithSystem._vwp._moveToRestPositionTime || flag2)
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
			if ((float)(GameTime.missionElapsedTime - sharesMountWithSystem._lastLaunchTime) < sharesMountWithSystem._vwp._moveToRestPositionTime || flag2)
			{
				return false;
			}
		}
		_isInRestPosition = false;
		_lastUsageTime = GameTime.missionElapsedTime;
		if (_vwp._anyLoadAngle)
		{
			return true;
		}
		return moveToPosition(_vwp._loadAngles.x, _vwp._loadAngles.y);
	}

	protected bool moveToPosition(float horizontalAngle, float verticalAngle)
	{
		_alignmentDeltas = string.Empty;
		bool flag = !_vwp._isMountRotatable || !hasRotationObject(_vwp._mountObject, "mount") || RotateWeaponToAngle(_vwp._mountObject.transform, horizontalAngle, horizontalRotation: true);
		bool flag2;
		if (_vwp._areContainersRotatable)
		{
			if (_vwp._areContainersJoinedTogether)
			{
				flag2 = !hasRotationObject(_vwp._containerBaseObject, "container base") || RotateWeaponToAngle(_vwp._containerBaseObject.transform, verticalAngle, horizontalRotation: false);
			}
			else
			{
				flag2 = true;
				for (int i = 0; i < _containers.Count; i++)
				{
					if (hasRotationObject(_containers[i]._gunObject, "container", i + 1) && !RotateWeaponToAngle(_containers[i]._gunObject.transform, verticalAngle, horizontalRotation: false))
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

	private bool hasRotationObject(GameObject rotationObject, string objectDescription, int objectNumber = 0)
	{
		if (rotationObject != null)
		{
			return true;
		}
		if (!_loggedMissingRotationObject)
		{
			_loggedMissingRotationObject = true;
			if (objectNumber > 0)
			{
				objectDescription += $" {objectNumber}";
			}
			string text = ((_baseObject != null) ? _baseObject.getUIDAndName() : "<unknown object>");
			Log.Error(text + ": Cannot rotate [" + _sectionName + "] " + _systemName + ": " + objectDescription + " object is missing", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\WeaponSystem.cs", 1650, "hasRotationObject");
		}
		return false;
	}

	protected bool RotateWeaponToAngle(Transform transform, float targetAngle, bool horizontalRotation, bool preciseAim = false)
	{
		Vector3 localEulerAngles = transform.localEulerAngles;
		float num = 0f;
		if (horizontalRotation)
		{
			float num2 = Mathf.MoveTowardsAngle(localEulerAngles.y, targetAngle, _vwp._horizontalDegreesPerSecond * GameTime.deltaTime);
			transform.localEulerAngles = new Vector3(localEulerAngles.x, num2, localEulerAngles.z);
			num = Mathf.DeltaAngle(num2, targetAngle);
		}
		else
		{
			float num3 = Mathf.MoveTowardsAngle(localEulerAngles.x, targetAngle, _vwp._verticalDegreesPerSecond * GameTime.deltaTime);
			transform.localEulerAngles = new Vector3(num3, localEulerAngles.y, localEulerAngles.z);
			num = Mathf.DeltaAngle(num3, targetAngle);
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

	public virtual string GetStatus()
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
		if (num > 0f && !IsTubeHoldingWire())
		{
			float num2 = Mathf.Floor(num / 60f);
			return string.Format("{0}{1:00}:{2:00}{3}", Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Reloading"), num2, num - num2 * 60f, GetLoadedAmmoSuffix());
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
			float num3 = AcquisitionRemaining();
			if (num3 > 0.05f)
			{
				return string.Format("{0} ({1} {2:0}s)", Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Engaging"), Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "DevelopingSolution", logError: false, verboseError: false), num3);
			}
			if (_engageState != 0)
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
		return Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Ready") + GetLoadedAmmoSuffix();
	}

	public float AcquisitionRemaining()
	{
		double num = AcquisitionClockStart();
		if (num < 0.0)
		{
			return -1f;
		}
		return ReactionTime.Acquisition(_baseObject, _vwp) - (float)(GameTime.missionElapsedTime - num);
	}

	protected virtual double AcquisitionClockStart()
	{
		return -1.0;
	}

	protected bool AcquisitionComplete(double anchorTime)
	{
		return ReactionTime.AcquiredAfter((float)(GameTime.missionElapsedTime - anchorTime), _baseObject, _vwp);
	}

	private string GetLoadedAmmoSuffix()
	{
		(int, int)? loadedAmmoReadout = GetLoadedAmmoReadout();
		if (!loadedAmmoReadout.HasValue)
		{
			return string.Empty;
		}
		return $" ({loadedAmmoReadout.Value.Item1}/{loadedAmmoReadout.Value.Item2})";
	}

	public (int ready, int max)? GetLoadedAmmoReadout()
	{
		bool num = _weaponType == WeaponType.Noisemaker || _weaponType == WeaponType.Chaff;
		bool flag = (_weaponType == WeaponType.Gun || _weaponType == WeaponType.CIWS) && _vwp._internalAmmoCount <= 1;
		if (num || flag || _vwp._maxAmmoCount <= 1)
		{
			return null;
		}
		return (GetReadyAmmoCount(), _vwp._maxAmmoCount);
	}

	protected virtual int GetReadyAmmoCount()
	{
		if (_ammoForEngage != null)
		{
			return getLoadedAmmoCount(_ammoForEngage._ap._ammunitionFileName);
		}
		return _vwp._internalAmmoCount;
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
		if (_vwp._externalGuidingSystems.Count <= 0 || !(GameTime.missionElapsedTime > _lastSearchForSensorTime))
		{
			return;
		}
		_lastSearchForSensorTime = GameTime.missionElapsedTime + 30.0;
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
		bool flag = false;
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
					flag = true;
				}
			}
		}
		if (flag)
		{
			SortAssociatedSensors();
		}
	}

	public void SortAssociatedSensors()
	{
		if (_vwp?._associatedSensors != null && _vwp._associatedSensors.Count >= 2)
		{
			_vwp._associatedSensors.Sort(delegate(SensorSystem a, SensorSystem b)
			{
				float value = ((a?._mountObject != null) ? a._mountObject.transform.position.y : float.NegativeInfinity);
				return ((b?._mountObject != null) ? b._mountObject.transform.position.y : float.NegativeInfinity).CompareTo(value);
			});
		}
	}

	private SensorSystemLaserDesignator FindExternalLaserDesignatorForEngage()
	{
		return _baseObject._taskforce._sensorSystemsLink.findLaserDesignatorFromTaskForceLink(_targetObject);
	}

	private SensorSystemLaserDesignator FindHardpointLaserDesignatorForEngage()
	{
		SensorSystemLaserDesignator result = null;
		foreach (SensorSystem sensorSystem in _baseObject._obp._sensorSystems)
		{
			if (sensorSystem._systemType == SystemType.SensorLaserDesignator)
			{
				SensorSystemLaserDesignator sensorSystemLaserDesignator = sensorSystem as SensorSystemLaserDesignator;
				if (sensorSystemLaserDesignator.CanLaserHitTarget(_targetObject, initiating: true))
				{
					sensorSystemLaserDesignator.TryToAddTarget(_targetObject);
					result = sensorSystemLaserDesignator;
					break;
				}
			}
		}
		return result;
	}

	private bool PrepareLaserDesignator()
	{
		SensorSystemLaserDesignator sensorSystemLaserDesignator = FindExternalLaserDesignatorForEngage();
		if (sensorSystemLaserDesignator == null)
		{
			sensorSystemLaserDesignator = FindHardpointLaserDesignatorForEngage();
			sensorSystemLaserDesignator?.TryToAddTarget(_targetObject);
		}
		_engageLaserDesignator = sensorSystemLaserDesignator;
		if (sensorSystemLaserDesignator == null)
		{
			_engageState = EngageState.LaserCantPaintTarget;
			return false;
		}
		return true;
	}

	private bool LaserGuidanceRequired()
	{
		if (_ammoForEngage._ap._requiresGuidance)
		{
			return _ammoForEngage._ap.GuidanceTypeForTarget(_targetObject) == AmmunitionParameters.GuidanceType.LaserHoming;
		}
		return false;
	}

	protected bool InterruptedByLaserGuidanceReadiness()
	{
		if (LaserGuidanceRequired() && !PrepareLaserDesignator())
		{
			return true;
		}
		return false;
	}

	public void StartReload(bool instantReload = false)
	{
		_isReloading = true;
		_reloadStartTime = GameTime.missionElapsedTime;
		if (instantReload)
		{
			_reloadStartTime -= _vwp._magazineReloadTime;
		}
		foreach (WeaponSystem sharesReloadWithSystem in _vwp._sharesReloadWithSystems)
		{
			sharesReloadWithSystem._isReloading = true;
			sharesReloadWithSystem._reloadStartTime = GameTime.missionElapsedTime;
			if (instantReload)
			{
				sharesReloadWithSystem._reloadStartTime -= sharesReloadWithSystem._vwp._magazineReloadTime;
			}
		}
	}

	public virtual void RefillAmmo(List<WeaponMagazineSystem> processedMagazines = null)
	{
		if (_vwp._associatedMagazine != null && (processedMagazines == null || !processedMagazines.Contains(_vwp._associatedMagazine)))
		{
			_vwp._associatedMagazine.RefillAmmo();
			processedMagazines?.Add(_vwp._associatedMagazine);
		}
		if (_vwp._campaignMagazine != null && (processedMagazines == null || !processedMagazines.Contains(_vwp._campaignMagazine)))
		{
			_vwp._campaignMagazine.RefillAmmo();
			processedMagazines?.Add(_vwp._campaignMagazine);
		}
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
