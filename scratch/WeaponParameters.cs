using System.Collections.Generic;
using UnityEngine;

namespace SeaPower;

public class WeaponParameters
{
	public WeaponSystem _weaponSystem;

	public string _sectionName;

	public string _systemName;

	public string _moduleName;

	public List<SensorSystem> _associatedSearchSensors = new List<SensorSystem>();

	public List<string> _externalSearchSystems = new List<string>();

	public List<SensorSystem> _associatedSensors = new List<SensorSystem>();

	public List<string> _externalGuidingSystems = new List<string>();

	public float _externalSensorSearchRadius = 27.559496f;

	public int _numberOfWires = 1;

	public float _targetAcquisitionTime;

	public string _openSystemAnimation;

	public string _closeSystemAnimation;

	public string _unloadAmmunitionAnimation;

	public string _loadAmmunitionAnimation;

	public GameObject _mountObject;

	public WeaponMount _mount;

	public bool _isMountRotatable;

	public GameObject _containerBaseObject;

	public int _numberOfHatchesPerContainers;

	public bool _areContainersRotatable;

	public bool _areContainersJoinedTogether = true;

	public bool _fixVerticalLaunchAngleForLauncher;

	public float _fixVerticalLaunchAngle;

	public float _additionalFixVerticalLaunchAngle;

	public float _launchAngle;

	public bool _useLaunchAngle;

	public float _arcBearingMin;

	public float _arcBearingMax;

	public List<Vector2> _firingArcs = new List<Vector2>();

	public List<Vector2> _preferredArcs = new List<Vector2>();

	public bool _engageOnlyInsideArc;

	public Vector2 _elevationArc;

	public Vector2 _restAngles;

	public Vector2 _coldAngles;

	public Vector2 _loadAngles = Vector2.zero;

	public bool _anyLoadAngle;

	public float _alignmentSide;

	public string _ammunitionFileName;

	public WeaponMagazineSystem _associatedMagazine;

	public WeaponMagazineSystem _campaignMagazine;

	public WeaponSystem.LoadingType _loadingType;

	public bool _hasPreloadedAmmunition;

	public bool _forceMoveToLoadPosition;

	public bool _linkedFire;

	public bool _backwardsLaunch;

	public float _minSpeedForLaunnchKnots = -1000f;

	public bool _invertedPattern;

	public bool _blockedByFlightDeck;

	public int _numberOfChaffEffects;

	public float _delayBetweenLaunches;

	public Vector3 _chaffCloudOffset = Vector3.zero;

	public bool _worksStandalone;

	public bool _autoEnableSensor;

	public bool _waitWhileSensorIsGuiding;

	public float _maxReactiontime;

	public float _maxVelocityForLandUnitKn = 0.1f;

	public float _vidIncrease = -1f;

	public float _vidIncreaseInterval = 20f;

	public string _sharesMountWith = "";

	public List<WeaponSystem> _sharesMountWithSystems = new List<WeaponSystem>();

	public string _sharesReloadWith = "";

	public List<WeaponSystem> _sharesReloadWithSystems = new List<WeaponSystem>();

	public int _salvoFireAmount;

	public float _salvoFireTime;

	public int _fireRatePerMinute;

	public float _burstTime;

	public float _preLaunchDelay;

	public float _salvoFireTargetCircleRadius;

	public float _horizontalDegreesPerSecond;

	public float _verticalDegreesPerSecond;

	public float _magazineReloadTime;

	public float _moveToRestPositionTime;

	public bool _hasWarmUp = true;

	public bool _hasWarmUpAmmo;

	public bool _attachmentsFixed;

	public GameObject _smokeEffect;

	public Vector3 _smokeEffectPosition;

	public bool _smokeEffectDirectional;

	public GameObject _dustEffectObject;

	public Vector3 _dustEffectPosition = Vector3.zero;

	public Vector3 _dustEffectRotation = Vector3.zero;

	public int _internalAmmoCount;

	public int _maxAmmoCount;

	public float _damageMultiplier = 1f;
}
