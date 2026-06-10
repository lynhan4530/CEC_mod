using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SeapowerUI.ViewModels;
using UniRx;
using UnityEngine;

namespace SeaPower;

[Serializable]
public abstract class BaseSystem : INotifyPropertyChanged
{
	public enum SystemType
	{
		Generic,
		Cargo,
		SensorVisual,
		SensorPeriscope,
		SensorInfrared,
		SensorSearchRadar,
		SensorFCRadar,
		SensorSonar,
		SensorESM,
		SensorECM,
		SensorMAD,
		SensorLaserDesignator,
		WeaponMagazine,
		WeaponLauncher,
		WeaponHardPoint,
		WeaponGun,
		WeaponCIWS,
		WeaponNoisemaker,
		WeaponChaff,
		TowedDecoy,
		HitZone,
		SnorkelMast,
		SensorDataOverride
	}

	public enum ArmorType
	{
		None,
		Minor,
		Moderate,
		Heavy
	}

	public enum PowerplantType
	{
		Nuclear,
		Steam,
		GasTurbine,
		Diesel,
		Electric,
		AIP,
		None
	}

	protected class TowedDeployConditions
	{
		public float _minTowedSystemDeploySpeed;

		public float _maxTowedSystemDeploySpeed;

		public float _maxTowedSystemDeployAltitude;

		public float _minDeployDepth;
	}

	public string _sectionName;

	public string _moduleName;

	public string _saveSectionName;

	public float _resilenceModifier;

	public bool _debugCheckModuleNameLoad = true;

	public ObjectBase _baseObject;

	public SystemType _systemType;

	public string _systemName;

	public GameObject _mountObject;

	public ObjectCodeAnimation _openSystemAnimation;

	public ObjectCodeAnimation _closeSystemAnimation;

	public bool _isSubmarineSensor;

	public bool _isSubmarineMast;

	public float _transmitterRange;

	protected TowedDeployConditions _towedDeployConditions = new TowedDeployConditions();

	public TowedSystem _cableSystem;

	public List<string> _colliderNames = new List<string>();

	public List<HitBox> _colliders = new List<HitBox>();

	public List<MeshCollider> _meshColliders = new List<MeshCollider>();

	public int _compartmentIndexMin = -1;

	public int _compartmentIndexMax = -1;

	public Effect _secondaryExplosionEffect;

	private GameObject _secondaryExplosionEffectGO;

	private ParticleSystem _secondaryExplosionEffectPS;

	public float _secondaryExplosionEffectStartTime = -1f;

	public float _maxIntegrity = 1f;

	private float _lastIntegrity = 1f;

	private float _currentIntegrity = 1f;

	public bool _alwaysRepairable;

	public bool _repairable = true;

	public float _explosionPower;

	public AmmunitionParameters.ImpactSize _explosionRadius = AmmunitionParameters.ImpactSize.Small;

	public AmmunitionParameters.Penetration _penetration;

	private float _currentIntegrityPercent;

	private DamageValues.DamageType _currentDamageType;

	private DamageValues.DamageType _biggestDamageType = DamageValues.DamageType.Light;

	public DamageSummary damageSummary = new DamageSummary();

	private int repairningUpdateCounter = -1;

	public ArmorType _armorType;

	public bool _isCoveredByMainArmor;

	public float _fireProbability;

	public float _explosionProbability;

	public int _currentFireValue;

	public int _lastFireRoll;

	public DamageValues.FireType _currentFire;

	public ReactiveProperty<string> DisplayName { get; } = new ReactiveProperty<string>();

	public Vector3 AimPointPosition
	{
		get
		{
			if (_colliders.Count > 0)
			{
				return _baseObject.transform.TransformPoint(_colliders[0]._position);
			}
			if (_mountObject != null)
			{
				return _mountObject.transform.position;
			}
			return _baseObject.transform.position;
		}
	}

	public ReactiveProperty<bool> IsOn { get; } = new ReactiveProperty<bool>();

	public ReactiveProperty<bool> Inoperable { get; } = new ReactiveProperty<bool>(initialValue: false);

	public float CurrentIntegrity
	{
		get
		{
			return _currentIntegrity;
		}
		set
		{
			_currentIntegrity = value;
			NotifyPropertyChanged("CurrentIntegrity");
			CurrentIntegrityPercent = ((_maxIntegrity > 0f) ? (100f * CurrentIntegrity / _maxIntegrity) : 100f);
		}
	}

	public bool Repairable
	{
		get
		{
			return _repairable;
		}
		protected set
		{
			_repairable = value;
			NotifyPropertyChanged("Repairable");
		}
	}

	public float CurrentIntegrityPercent
	{
		get
		{
			return _currentIntegrityPercent;
		}
		protected set
		{
			_currentIntegrityPercent = value;
			NotifyPropertyChanged("CurrentIntegrityPercent");
			SetCurrentDamageType();
		}
	}

	public DamageValues.DamageType CurrentDamageType
	{
		get
		{
			return _currentDamageType;
		}
		protected set
		{
			_currentDamageType = value;
			NotifyPropertyChanged("CurrentDamageType");
			damageSummary.DamageIntensity = (int)CurrentDamageType;
			damageSummary.IsUnrepairable = BiggestDamageType > CurrentDamageType;
		}
	}

	public bool IsDestroyed => CurrentDamageType == DamageValues.DamageType.Destroyed;

	public DamageValues.DamageType BiggestDamageType
	{
		get
		{
			return _biggestDamageType;
		}
		protected set
		{
			_biggestDamageType = value;
			NotifyPropertyChanged("BiggestDamageType");
			damageSummary.IsUnrepairable = BiggestDamageType > CurrentDamageType;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public virtual void SaveStateToFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		if (Inoperable.Value)
		{
			ini.writeValue(sectionName, "Inoperable", Inoperable.Value);
		}
		ini.writeValue(sectionName, "CurrentIntegrity", CurrentIntegrity);
		if (!isCampaignStateSave)
		{
			ini.writeValue(sectionName, "IsOn", IsOn.Value);
			if (Globals._devMode)
			{
				ini.writeValue(sectionName, "DebugModuleName", _moduleName ?? "");
				ini.writeValue(sectionName, "DebugSystemName", _systemName ?? "");
			}
		}
	}

	public virtual void LoadStateFromFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		if (_baseObject == null)
		{
			DM.E("Base object for " + sectionName + " is null");
		}
		if (!isCampaignStateSave || !_baseObject._campaignRepair)
		{
			float num = ini.readValue(sectionName, "CurrentIntegrity", -1f);
			if (num > -1E-05f)
			{
				CurrentIntegrity = num;
				SetCurrentDamageType();
			}
			Inoperable.Value = ini.readValue(sectionName, "Inoperable", Inoperable.Value);
			if (!isCampaignStateSave)
			{
				IsOn.Value = ini.readValue(sectionName, "IsOn", IsOn.Value);
				_saveSectionName = sectionName;
			}
		}
	}

	private void SetCurrentDamageType()
	{
		if (_maxIntegrity < 1f)
		{
			CurrentDamageType = DamageValues.DamageType.No;
		}
		else if (CurrentIntegrityPercent > 90f)
		{
			CurrentDamageType = DamageValues.DamageType.No;
		}
		else if (CurrentIntegrityPercent > 60f)
		{
			CurrentDamageType = DamageValues.DamageType.Light;
		}
		else if (CurrentIntegrityPercent > 20f)
		{
			CurrentDamageType = DamageValues.DamageType.Medium;
		}
		else if (CurrentIntegrityPercent > 1f)
		{
			CurrentDamageType = DamageValues.DamageType.Severe;
		}
		else
		{
			CurrentDamageType = DamageValues.DamageType.Destroyed;
		}
	}

	public virtual void LoadFromInI(IniHandler ini, string sectionName, ObjectBaseParameters obp, string keyName = "")
	{
	}

	protected void LoadBaseParameters(IniHandler ini, string sectionName, ObjectBaseParameters obp, string keyName = "")
	{
		_baseObject = obp._baseObject;
		_sectionName = sectionName;
		_systemName = ini.readValue(sectionName, keyName + "SystemName", _sectionName);
		if (!string.IsNullOrEmpty(_systemName))
		{
			IniHandler iniHandler = Utils.openVirtualIniFile("language_" + Singleton<LanguageResourceHandler>.Instance.Language, "systemgroups", "systemgroups");
			DisplayName.Value = _systemName;
			if (iniHandler != null)
			{
				DisplayName.Value = iniHandler.readValue("SystemNames", _systemName, _systemName).Split("|")[0];
			}
			else
			{
				Log.Error("Could not create virtual Ini for systemgroups.ini!", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\BaseSystem.cs", 231, "LoadBaseParameters");
			}
		}
		if (ini.doesKeyExist(sectionName, "SubModelsToHide"))
		{
			string[] array = ini.readValue(sectionName, "SubModelsToHide", "").Split(',');
			foreach (string text in array)
			{
				if (obp._objectsDictionary.ContainsKey(text))
				{
					obp._objectsDictionary[text].SetActive(value: false);
					obp._subModelsToHide.Add(obp._objectsDictionary[text]);
					if (obp._objectsDictionary.ContainsKey(text + "_d"))
					{
						obp._subModelsToHide.Add(obp._objectsDictionary[text + "_d"]);
					}
				}
			}
		}
		if (ini.doesKeyExist(sectionName, keyName + "OpenSystemAnimation"))
		{
			string key = ini.readValue(sectionName, keyName + "OpenSystemAnimation", "", performLogging: false);
			string key2 = ini.readValue(sectionName, keyName + "CloseSystemAnimation", "", performLogging: false);
			obp._codeAnimations.TryGetValue(key, out _openSystemAnimation);
			obp._codeAnimations.TryGetValue(key2, out _closeSystemAnimation);
		}
		_armorType = ArmorType.None;
		_resilenceModifier = ini.readValue(sectionName, keyName + "ResilenceModifier", 1f);
		if (ini.doesKeyExist(sectionName, keyName + "ModuleType"))
		{
			_moduleName = ini.readValue(sectionName, keyName + "ModuleType", "");
		}
		if (!string.IsNullOrEmpty(_moduleName))
		{
			LoadModuleParameters(obp);
		}
		if (ini.doesKeyExist(sectionName, keyName + "Integrity"))
		{
			_maxIntegrity = ini.readValue(sectionName, keyName + "Integrity", 10);
		}
		if (ini.doesKeyExist(sectionName, keyName + "AlwaysRepairable"))
		{
			_alwaysRepairable = ini.readValue(sectionName, keyName + "AlwaysRepairable", defaultValue: false);
		}
		if (ini.doesKeyExist(sectionName, keyName + "Repairable"))
		{
			Repairable = ini.readValue(sectionName, keyName + "Repairable", defaultValue: true);
		}
		if (ini.doesKeyExist(sectionName, keyName + "FireProbability"))
		{
			_fireProbability = ini.readValue(sectionName, keyName + "FireProbability", 0f);
		}
		if (ini.doesKeyExist(sectionName, keyName + "ExplosionProbability"))
		{
			_explosionProbability = ini.readValue(sectionName, keyName + "ExplosionProbability", 0f);
		}
		if (ini.doesKeyExist(sectionName, keyName + "ExplosionPower"))
		{
			_explosionPower = ini.readValue(sectionName, keyName + "ExplosionPower", 0f);
		}
		if (ini.doesKeyExist(sectionName, keyName + "ExplosionRadius"))
		{
			Enum.TryParse<AmmunitionParameters.ImpactSize>(ini.readValue(sectionName, keyName + "ExplosionRadius", ""), ignoreCase: true, out _explosionRadius);
		}
		if (ini.doesKeyExist(sectionName, keyName + "ExplosionPenetration"))
		{
			Enum.TryParse<AmmunitionParameters.Penetration>(ini.readValue(sectionName, keyName + "ExplosionPenetration", ""), ignoreCase: true, out _penetration);
		}
		switch (ini.readValue(sectionName, keyName + "ArmorType", "").ToLower())
		{
		case "none":
			_armorType = ArmorType.None;
			break;
		case "minor":
			_armorType = ArmorType.Minor;
			break;
		case "moderate":
			_armorType = ArmorType.Moderate;
			break;
		case "heavy":
			_armorType = ArmorType.Heavy;
			break;
		}
		if (ini.doesKeyExist(sectionName, keyName + "IsCoveredByMainArmor"))
		{
			_isCoveredByMainArmor = ini.readValue(sectionName, keyName + "IsCoveredByMainArmor", defaultValue: false);
		}
		_maxIntegrity *= _resilenceModifier;
		CurrentIntegrity = _maxIntegrity;
		if (_secondaryExplosionEffect == null || !_secondaryExplosionEffect._isAvailable)
		{
			_secondaryExplosionEffect = new Effect(ini, sectionName, "SecondaryExplosionEffect");
		}
		if (_secondaryExplosionEffect == null || !_secondaryExplosionEffect._isAvailable)
		{
			_secondaryExplosionEffect = obp._secondaryExplosionEffect;
		}
	}

	protected void LoadModuleParameters(ObjectBaseParameters obp)
	{
		if (string.IsNullOrEmpty(_moduleName))
		{
			_moduleName = "Default";
		}
		IniHandler iniHandler = Utils.openVirtualIniFile("systems", "modules", "modules");
		if (iniHandler != null)
		{
			if (iniHandler.doesSectionExist(_moduleName))
			{
				_maxIntegrity = iniHandler.readValue(_moduleName, "Integrity", 10f);
				if (iniHandler.readValue(_moduleName, "ScalesWithDisplacement", defaultValue: false))
				{
					float num = 1f;
					if (obp is VesselParameters vesselParameters)
					{
						num = vesselParameters._displacement;
					}
					else if (obp is SubmarineParameters submarineParameters)
					{
						num = submarineParameters._displacement;
					}
					if (num > 1f)
					{
						num = Mathf.Pow(num, 0.25f);
						if (obp._isCivilian)
						{
							num /= 2f;
						}
						if (num < 1f)
						{
							num = 1f;
						}
						_maxIntegrity *= num;
						if (_maxIntegrity < 10f)
						{
							_maxIntegrity = 10f;
						}
					}
				}
				_alwaysRepairable = iniHandler.readValue(_moduleName, "AlwaysRepairable", defaultValue: false);
				Repairable = iniHandler.readValue(_moduleName, "Repairable", defaultValue: true);
				_fireProbability = iniHandler.readValue(_moduleName, "FireProbability", 0f);
				_explosionProbability = iniHandler.readValue(_moduleName, "ExplosionProbability", 0f);
				_explosionPower = iniHandler.readValue(_moduleName, "ExplosionPower", 0f);
				if (iniHandler.doesKeyExist(_moduleName, "ExplosionRadius"))
				{
					Enum.TryParse<AmmunitionParameters.ImpactSize>(iniHandler.readValue(_moduleName, "ExplosionRadius", ""), ignoreCase: true, out _explosionRadius);
				}
				if (iniHandler.doesKeyExist(_moduleName, "ExplosionPenetration"))
				{
					Enum.TryParse<AmmunitionParameters.Penetration>(iniHandler.readValue(_moduleName, "ExplosionPenetration", ""), ignoreCase: true, out _penetration);
				}
				switch (iniHandler.readValue(_moduleName, "ArmorType", "").ToLower())
				{
				case "none":
					_armorType = ArmorType.None;
					break;
				case "minor":
					_armorType = ArmorType.Minor;
					break;
				case "moderate":
					_armorType = ArmorType.Moderate;
					break;
				case "heavy":
					_armorType = ArmorType.Heavy;
					break;
				}
				_isCoveredByMainArmor = iniHandler.readValue(_moduleName, "IsCoveredByMainArmor", defaultValue: false);
				_secondaryExplosionEffect = new Effect(iniHandler, _moduleName, "SecondaryExplosionEffect");
			}
			else
			{
				Log.Error("ModuleType '" + _moduleName + "' does not exist in modules.ini while reading section '" + _sectionName + "' of '" + obp._objectIniName + "'", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\BaseSystem.cs", 364, "LoadModuleParameters");
			}
		}
		else
		{
			Log.Error("Can not open '/systems/modules.ini'", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\BaseSystem.cs", 366, "LoadModuleParameters");
		}
	}

	public virtual void init()
	{
		damageSummary.Description = DisplayName.Value;
		IsOn.Value = true;
	}

	public void LoadSystemName()
	{
		if (!string.IsNullOrEmpty(_systemName))
		{
			IniHandler iniHandler = Utils.openVirtualIniFile("language_" + Singleton<LanguageResourceHandler>.Instance.Language, "systemgroups", "systemgroups");
			DisplayName.Value = iniHandler.readValue("SystemNames", _systemName, _systemName).Split("|")[0];
		}
	}

	public virtual void OnUpdate()
	{
		if (_secondaryExplosionEffectStartTime > 0f && GameTime.time > _secondaryExplosionEffectStartTime)
		{
			_secondaryExplosionEffectStartTime = -1f;
			ExplodeSystem();
		}
		if (repairningUpdateCounter < 0)
		{
			if (CurrentIntegrity > _lastIntegrity)
			{
				damageSummary.IsRepairing = true;
			}
			else
			{
				damageSummary.IsRepairing = false;
			}
			_lastIntegrity = CurrentIntegrity;
			repairningUpdateCounter = 10;
		}
		else
		{
			repairningUpdateCounter--;
		}
		if (Inoperable.Value && IsOn.Value)
		{
			IsOn.Value = false;
		}
	}

	public virtual void OnFixedUpdate()
	{
		if (BiggestDamageType < CurrentDamageType)
		{
			BiggestDamageType = CurrentDamageType;
		}
		if (CurrentIntegrity < 0f)
		{
			CurrentIntegrity = 0f;
		}
		else if (CurrentIntegrity > _maxIntegrity)
		{
			CurrentIntegrity = _maxIntegrity;
		}
	}

	public virtual void SetDisabled()
	{
	}

	public void DebugRepair()
	{
		_currentFire = DamageValues.FireType.No;
		CurrentIntegrity = _maxIntegrity;
		Inoperable.Value = false;
		if (_isSubmarineSensor)
		{
			return;
		}
		if (this is SensorSystemRadar sensorSystemRadar)
		{
			if (sensorSystemRadar._type != SensorSystemRadar.RadarType.Search && sensorSystemRadar._type != SensorSystemRadar.RadarType.DirectedSearch)
			{
				IsOn.Value = true;
			}
		}
		else
		{
			IsOn.Value = true;
		}
	}

	public bool isOnFire()
	{
		return _currentFire != DamageValues.FireType.No;
	}

	public void setTowedSystemDeployConditions(float minDeploySpeed, float maxDeploySpeed, float maxDeployAltitude, float minDeployDepth)
	{
		_towedDeployConditions._minTowedSystemDeploySpeed = minDeploySpeed;
		_towedDeployConditions._maxTowedSystemDeploySpeed = maxDeploySpeed;
		_towedDeployConditions._maxTowedSystemDeployAltitude = maxDeployAltitude;
		_towedDeployConditions._minDeployDepth = minDeployDepth;
	}

	public bool isTowedSystemSpeedWithinDeployConditions(ObjectBase baseObject)
	{
		int num = Mathf.RoundToInt(_towedDeployConditions._minTowedSystemDeploySpeed * 130.6266f);
		int num2 = Mathf.RoundToInt(_towedDeployConditions._maxTowedSystemDeploySpeed * 130.6266f);
		if (Mathf.RoundToInt(baseObject._velocityInKnots) >= num)
		{
			return Mathf.RoundToInt(baseObject._velocityInKnots) <= num2;
		}
		return false;
	}

	public bool isTowedSystemAltitudeWithinDeployConditions(ObjectBase baseObject)
	{
		bool result = false;
		int num = Mathf.RoundToInt(_towedDeployConditions._maxTowedSystemDeployAltitude * 220.47266f);
		if (Mathf.RoundToInt(baseObject.transform.position.y * 220.47266f) <= num)
		{
			result = true;
		}
		if (baseObject._type == ObjectBase.ObjectType.Aircraft || (baseObject._type == ObjectBase.ObjectType.Helicopter && baseObject.transform.position.y < float.Epsilon))
		{
			result = false;
		}
		return result;
	}

	public bool itIsTooShallow(ObjectBase baseObject)
	{
		bool result = false;
		if (TerrainUtils.GetHeightAt(Globals._currentGeoPosition) > _towedDeployConditions._minDeployDepth + -0.2267855f)
		{
			result = true;
		}
		return result;
	}

	public bool isSurfacedSubmarine(ObjectBase baseObject)
	{
		bool result = false;
		if (baseObject._type == ObjectBase.ObjectType.Submarine && !(baseObject as Submarine).IsSubmerged.Value)
		{
			result = true;
		}
		return result;
	}

	public bool isSubmarineNearBottom(ObjectBase baseObject)
	{
		bool result = false;
		float terrainHeightInUnity = baseObject.TerrainHeightInUnity;
		if (baseObject._type == ObjectBase.ObjectType.Submarine && terrainHeightInUnity > baseObject.transform.position.y + -0.2267855f)
		{
			result = true;
		}
		return result;
	}

	protected TowedDeployConditions getTowedSystemDeployConditions()
	{
		return _towedDeployConditions;
	}

	public void AddDamage(float damage)
	{
		if (damage < 0.0001f)
		{
			return;
		}
		CurrentIntegrity -= damage;
		if (damage > 0f && !Inoperable.Value)
		{
			if (CurrentDamageType == DamageValues.DamageType.Destroyed)
			{
				MakeInoperable();
				CheckSystemExplosion();
			}
			else if (CurrentDamageType == DamageValues.DamageType.Severe && Globals._rnd.Next(1, 21) > 5)
			{
				MakeInoperable();
				CheckSystemExplosion();
			}
			else if (CurrentDamageType == DamageValues.DamageType.Medium && Globals._rnd.Next(1, 21) > 15)
			{
				MakeInoperable();
			}
		}
	}

	public void CheckSystemExplosion()
	{
		if (Globals._allowSystemExplosions)
		{
			if (_baseObject._systemExplosionReduceEndTime < GameTime.time)
			{
				_baseObject._systemExplosionChanceMultiplier = 1f;
			}
			_baseObject._systemExplosionReduceEndTime = GameTime.time + 10f;
			int num = Globals._rnd.Next(101);
			if (_baseObject.IsPlayerObject)
			{
				num += Globals._systemExplosionChanceReduction;
			}
			_baseObject.Info.LogUnitInfo(string.Format("{0} explosion roll: <color={1}>{2} < {3}({4})</color>", DisplayName.Value, ((float)num < _explosionProbability * _baseObject._systemExplosionChanceMultiplier) ? "red" : "lime", num, _explosionProbability * _baseObject._systemExplosionChanceMultiplier, _explosionProbability));
			if ((float)num < _explosionProbability * _baseObject._systemExplosionChanceMultiplier)
			{
				DM.I($"{_baseObject.getUIDAndName()}: <color=red>SECONDARY EXPLOSION!</color> sys = {_sectionName};  Roll {num} < {_explosionProbability * _baseObject._systemExplosionChanceMultiplier}");
				_baseObject._systemExplosionChanceMultiplier /= 2f;
				Destroy();
				_secondaryExplosionEffectStartTime = GameTime.time + Utils.RandomFloat(0.2f, 2f);
			}
			else
			{
				_baseObject.Info.LogUnitInfo("<color=lime>" + DisplayName.Value + " passed explosion check.</color>");
			}
		}
	}

	public void ExplodeSystem()
	{
		if (_baseObject._obp._secondaryExplosionEffect == null)
		{
			return;
		}
		Vector3 position = _baseObject.transform.position;
		if (_colliders.Count > 0)
		{
			position = _colliders[0]._go.transform.position;
		}
		else if (_meshColliders.Count > 0)
		{
			position = _meshColliders[0].transform.position;
		}
		if (_mountObject != null)
		{
			position = _mountObject.transform.position;
		}
		if (_secondaryExplosionEffectGO == null)
		{
			_secondaryExplosionEffectGO = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_secondaryExplosionEffect, _baseObject.transform, position, triggerCameraShake: true, 0.5f));
			if (_secondaryExplosionEffectGO != null)
			{
				_secondaryExplosionEffectPS = _secondaryExplosionEffectGO.GetComponent<ParticleSystem>();
			}
		}
		if (_secondaryExplosionEffectGO != null)
		{
			_secondaryExplosionEffectGO.transform.localRotation = Quaternion.identity;
			_secondaryExplosionEffectPS.Play();
		}
		_baseObject.Info.LogUnitInfo("<color=yellow>" + DisplayName.Value + " has exploded!</color>");
		Blastzone blastzone = new Blastzone(new AmmunitionParameters
		{
			_addFire = ((_explosionPower > 0f) ? 3 : 0),
			_impactSize = _explosionRadius,
			_penetration = _penetration,
			_warheadPower = _explosionPower
		}, position, Vector3.zero, _baseObject);
		blastzone._isSecondaryExplosion = true;
		blastzone.CreateExplosion();
	}

	public void MakeInoperable()
	{
		IsOn.Value = false;
		Inoperable.Value = true;
		if (this is FlightDeck flightDeck)
		{
			flightDeck.KillAircraft();
		}
	}

	public void Destroy()
	{
		CurrentIntegrity = 0f;
		MakeInoperable();
	}

	public void addFire(int fire)
	{
		_lastFireRoll = fire;
		if (fire > 0)
		{
			_currentFireValue += fire;
			if (_currentFireValue <= Globals._damageValues._noFire)
			{
				_currentFire = DamageValues.FireType.No;
			}
			else if (_currentFireValue <= Globals._damageValues._selfExtinguishingFire)
			{
				_currentFire = DamageValues.FireType.SelfExtinguishing;
			}
			else if (_currentFireValue <= Globals._damageValues._moderateFire)
			{
				_currentFire = DamageValues.FireType.Moderate;
			}
			else
			{
				_currentFire = DamageValues.FireType.Severe;
			}
		}
	}

	public bool IsWithinTransmitterRange()
	{
		if (_transmitterRange <= 0f)
		{
			return true;
		}
		if (_baseObject == null)
		{
			Log.Error("System " + _sectionName + " " + _systemName + " does not have a base object", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\BaseSystem.cs", 651, "IsWithinTransmitterRange");
			return false;
		}
		foreach (ObjectBase listOfAllObject in Singleton<ObjectsManager>.Instance._listOfAllObjects)
		{
			if (!(listOfAllObject == null) && _baseObject._taskforce == listOfAllObject._taskforce && listOfAllObject.isUnit() && !listOfAllObject.IsDestroyed && !listOfAllObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Deco) && !listOfAllObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SeaMine) && (_baseObject.transform.position - listOfAllObject.transform.position).magnitude < _transmitterRange)
			{
				return true;
			}
		}
		return false;
	}

	public virtual void dispose()
	{
	}
}
