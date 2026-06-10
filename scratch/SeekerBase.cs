using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace SeaPower;

public class SeekerBase
{
	protected WeaponBase _ammoObject;

	public AmmunitionParameters _ap;

	private bool _isInitialized;

	protected List<SensorSystem> _detectedEmittingSystems = new List<SensorSystem>();

	public float _activationTime = -1f;

	protected Vector3Event _relocationEvent;

	public AmmunitionParameters.GuidanceType _guidanceType;

	public Ammunition.SeekerMode _mode;

	public bool _keepTarget;

	public SensorEcho _strongestSensorEcho;

	public List<ObjectBase> _potentialContacts = new List<ObjectBase>();

	public List<ObjectBase> _contactsInRange = new List<ObjectBase>();

	public List<SensorEcho> _sensorEchoesInRange = new List<SensorEcho>();

	public List<ObjectBase> _ignoredObjects = new List<ObjectBase>();

	public List<ChaffAttacher> _ignoredChaffs = new List<ChaffAttacher>();

	public List<ObjectBase> _ignoredTargets = new List<ObjectBase>();

	public ObjectBase _lockedOnTarget;

	public ObjectBase _firstLockedOnTarget;

	public float _originalTargetSize = -1f;

	public GeoPosition _originalTargetPosition;

	public Vector3 _originalTargetVelocity = Vector3.zero;

	protected float _targetLastSeenTime;

	protected const float _targetLostLatency = 2f;

	public float _maxRange;

	public float _waveLength = -1f;

	public float _spoofEndTime;

	public int _latestWakeBubbleUID;

	public bool _wakeHomingTurnsInRightDirection;

	public bool _wakeHomingTurnDelayed;

	protected float _wakeHomingTurnDelayTimer;

	protected ObjectBase _launcherObject;

	protected Vector3 _launcherPositionAtActivation;

	public GameObject _mountObject;

	protected const float _ignoredCountermeasuresListRefreshPeriod = 300f;

	public ObjectBase _lockedOnCountermeasure;

	protected float _timeOfLastIgnoredCountermeasuresListRefresh;

	protected float _countermeasureCheckTime = -1000f;

	public ReactiveProperty<bool> ActiveMode { get; } = new ReactiveProperty<bool>
	{
		Value = false
	};

	public ReactiveProperty<bool> IsOn { get; } = new ReactiveProperty<bool>
	{
		Value = false
	};

	public virtual void init(ObjectBase launcherObject, ObjectBase ammoObject, AmmunitionParameters ap)
	{
		_launcherObject = launcherObject;
		if (launcherObject != null)
		{
			_launcherPositionAtActivation = _launcherObject.getUnityPosition();
		}
		else
		{
			_launcherPositionAtActivation = ammoObject.transform.position - ammoObject.transform.forward * 30f;
		}
		_ammoObject = (WeaponBase)ammoObject;
		_ap = ap;
		_relocationEvent = new Vector3Event();
		_relocationEvent.AddListener(onRelocate);
		Singleton<RelocationHandler>.Instance.addToEventsList(_relocationEvent);
		_timeOfLastIgnoredCountermeasuresListRefresh = GameTime.time;
		_isInitialized = true;
		_activationTime = -1f;
		_keepTarget = _ap._targetMemory;
		if (ammoObject is Missile missile)
		{
			if (missile.CurrentIntendedTargetObject != null)
			{
				if (missile.CurrentIntendedTargetObject._sensorData != null)
				{
					_originalTargetSize = missile.CurrentIntendedTargetObject._sensorData._maxRCS;
				}
				_originalTargetPosition = missile.CurrentIntendedTargetObject._geoPosition;
				_originalTargetVelocity = missile.CurrentIntendedTargetObject.UnityVelocityVector;
			}
			else
			{
				_originalTargetPosition = missile.AimPointGeoPosition;
			}
		}
		if (_guidanceType == AmmunitionParameters.GuidanceType.ActiveRadarHoming || _guidanceType == AmmunitionParameters.GuidanceType.ActiveSonarHoming)
		{
			_maxRange = _ap._seekerActiveRange;
		}
		else if (_guidanceType == AmmunitionParameters.GuidanceType.ActivePassiveSonarHoming)
		{
			_maxRange = Mathf.Max(_ap._seekerActiveRange, _ap._seekerPassiveRange);
		}
		if (_guidanceType == AmmunitionParameters.GuidanceType.WakeHoming)
		{
			_maxRange = _ap._wakeHomingSeekerRange;
		}
		else
		{
			_maxRange = _ap._seekerPassiveRange;
		}
		_spoofEndTime = -1f;
		setWakeHomingInitialTurnDirection();
	}

	private void setWakeHomingInitialTurnDirection()
	{
		int num = Globals._rnd.Next(0, 101);
		if (_ap._wakeHomingTurnSettingReliability > num)
		{
			_wakeHomingTurnsInRightDirection = true;
		}
	}

	public virtual void destroy()
	{
		_isInitialized = false;
		IsOn.Value = false;
		if (_relocationEvent != null)
		{
			_relocationEvent.RemoveAllListeners();
			Singleton<RelocationHandler>.Instance.removeFromEventsList(_relocationEvent);
		}
	}

	public void onRelocate(Vector3 deltaPos)
	{
		_launcherPositionAtActivation += deltaPos;
		if (_strongestSensorEcho != null)
		{
			_strongestSensorEcho._unityPosition += deltaPos;
		}
	}

	public virtual void update()
	{
		if (!isActive())
		{
			return;
		}
		refreshCountermeasures();
		_potentialContacts.Clear();
		_sensorEchoesInRange.Clear();
		if (!CheckCountermeasureLock() && GameTime.time > _spoofEndTime)
		{
			createListOfPotentialContacts();
		}
		if (!_ammoObject.ConnectionLost.Value && (_ammoObject._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand || _ammoObject._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.WireGuided))
		{
			for (int num = _potentialContacts.Count - 1; num >= 0; num--)
			{
				if (!(_potentialContacts[num] is Noisemaker) && _potentialContacts[num]._type != ObjectBase.ObjectType.Decoy && _ammoObject.CurrentIntendedTargetObject != _potentialContacts[num] && _potentialContacts[num]._taskforce == _ammoObject._taskforce)
				{
					_potentialContacts.RemoveAt(num);
				}
			}
		}
		searchForTarget();
	}

	public SensorEcho getStrongestSensorEcho()
	{
		return _strongestSensorEcho;
	}

	public virtual bool isInRange(Vector3 position)
	{
		return false;
	}

	public virtual bool isInFractionOfRange(Vector3 position, float rangeFraction)
	{
		return false;
	}

	public virtual bool isInRange(ObjectBase targetObject)
	{
		return false;
	}

	public void setActive(bool active)
	{
		IsOn.Value = active;
		if (active)
		{
			_targetLastSeenTime = GameTime.time;
		}
		_activationTime = GameTime.time;
		if (_ammoObject is Torpedo torpedo && (torpedo._seeker._guidanceType == AmmunitionParameters.GuidanceType.ActiveSonarHoming || torpedo._seeker._guidanceType == AmmunitionParameters.GuidanceType.ActivePassiveSonarHoming) && !torpedo.IsDestroyed && torpedo._weaponInstance._sonarAudioSource != null)
		{
			torpedo._weaponInstance._sonarAudioSource.Play();
			torpedo._weaponInstance._sonarAudioSource.maxDistance = torpedo._ap._seekerActiveRange * 2f;
		}
	}

	public bool isActive()
	{
		if (IsOn.Value)
		{
			return _isInitialized;
		}
		return false;
	}

	public virtual void setGuidingSystem(SensorSystemRadar guidingRadar)
	{
	}

	public virtual void setGuidingSystem()
	{
	}

	public ObjectBase getLockedOnCountermeasure()
	{
		return _lockedOnCountermeasure;
	}

	public virtual Vector3 getEstimatedTargetPosition()
	{
		Vector3 result = Vector3.zero;
		if (_strongestSensorEcho != null && _strongestSensorEcho._objectBase != null)
		{
			result = _strongestSensorEcho._unityPosition;
			if (_strongestSensorEcho._localOffset != Vector3.zero)
			{
				Transform transform = _strongestSensorEcho._objectBase.transform;
				result += transform.right * _strongestSensorEcho._localOffset.x + transform.up * _strongestSensorEcho._localOffset.y + transform.forward * _strongestSensorEcho._localOffset.z;
			}
		}
		return result;
	}

	public void OffsetStrongestEchoPosition()
	{
		_strongestSensorEcho._localOffset = Vector3.zero;
		ObjectBase objectBase = _strongestSensorEcho._objectBase;
		if (objectBase._type == ObjectBase.ObjectType.Vessel || objectBase._type == ObjectBase.ObjectType.Submarine)
		{
			Compartments compartments = objectBase.Compartments;
			float y = objectBase._obp._floodingCompartmentsHeight / 2f;
			if (_ammoObject._type == ObjectBase.ObjectType.Torpedo)
			{
				y = 0f;
			}
			double num = 0.0;
			for (int i = 0; i < 11; i++)
			{
				int num2 = compartments._portCompartments.Length - 1;
				if (num2 >= 0)
				{
					double deviation = Math.Abs((double)((compartments._portCompartments[0]._position - compartments._portCompartments[num2]._position) / 2f)) / 2.0;
					num = TMAUtils.GenerateRandomVariant(0.0, deviation, ref Globals._unityRandom);
					if (num < (double)compartments._portCompartments[0]._position && num > (double)compartments._portCompartments[num2]._position)
					{
						break;
					}
				}
			}
			_strongestSensorEcho._localOffset = new Vector3(0f, y, (float)num);
		}
		else if (objectBase._type == ObjectBase.ObjectType.LandUnit && _guidanceType != AmmunitionParameters.GuidanceType.AntiRadiationHoming && (_ammoObject.CurrentIntendedTargetObject == null || _ammoObject.CurrentIntendedTargetObject != objectBase))
		{
			LandUnitParameters.TargetPoint targetPoint = Utils.AssignLandUnitTargetPoint(objectBase, excludeDestroyedSystems: false);
			_ammoObject._currentTargetPointDebug = targetPoint;
			_strongestSensorEcho._localOffset = targetPoint._position;
			_ammoObject._currentTargetPoint = targetPoint;
		}
	}

	public void OffsetStrongestEchoPosition_old()
	{
		_strongestSensorEcho._localOffset = Vector3.zero;
		ObjectBase objectBase = _strongestSensorEcho._objectBase;
		if (objectBase._type == ObjectBase.ObjectType.Vessel || objectBase._type == ObjectBase.ObjectType.Submarine)
		{
			Compartments compartments = objectBase.Compartments;
			int num = 5;
			int num2 = Globals._rnd.Next(0, num);
			if (num2 == 0 || num2 == num - 1)
			{
				num2 = Globals._rnd.Next(0, num);
			}
			float y = objectBase._obp._floodingCompartmentsHeight / 2f;
			if (_ammoObject._type == ObjectBase.ObjectType.Torpedo)
			{
				y = 0f;
			}
			_strongestSensorEcho._localOffset = new Vector3(0f, y, compartments._portCompartments[num2]._position);
		}
		else if (objectBase._type == ObjectBase.ObjectType.LandUnit && _guidanceType != AmmunitionParameters.GuidanceType.AntiRadiationHoming && (_ammoObject.CurrentIntendedTargetObject == null || _ammoObject.CurrentIntendedTargetObject != objectBase))
		{
			LandUnitParameters.TargetPoint targetPoint = Utils.AssignLandUnitTargetPoint(objectBase, excludeDestroyedSystems: false);
			_ammoObject._currentTargetPointDebug = targetPoint;
			_strongestSensorEcho._localOffset = targetPoint._position;
			_ammoObject._currentTargetPoint = targetPoint;
		}
	}

	public virtual Vector3 getEstimatedTargetVelocityVector()
	{
		Vector3 result = Vector3.zero;
		if (_strongestSensorEcho != null)
		{
			result = _strongestSensorEcho._unitySpeedVector;
		}
		return result;
	}

	protected virtual void createListOfPotentialContacts()
	{
	}

	protected virtual void searchForTarget()
	{
	}

	public bool CheckCountermeasureLock(ObjectBase obj)
	{
		if (_ap._antiChaffBonus > 0.999f)
		{
			return false;
		}
		if (obj == _lockedOnCountermeasure)
		{
			return true;
		}
		if (obj._type != ObjectBase.ObjectType.Noisemaker && obj._type != ObjectBase.ObjectType.Decoy && obj._type != ObjectBase.ObjectType.Torpedo && obj._type != ObjectBase.ObjectType.Chaff)
		{
			return true;
		}
		if (obj is Torpedo torpedo && torpedo._ap._subType != Ammunition.Type.MOSS)
		{
			return true;
		}
		if (GameTime.time - _countermeasureCheckTime < 35f)
		{
			_ignoredObjects.Add(obj);
			return false;
		}
		_countermeasureCheckTime = GameTime.time;
		DM.I($"{_ammoObject.getUIDAndName()} checkCounterMeasuresRejection vs {obj.getUIDAndName()}; Type = {obj._type}");
		if (DM._chaffOverride == 1)
		{
			Log.Debug("The " + _ammoObject.getUIDAndName() + ":  <color=cyan>locks on</color> " + obj.name + " because chaff override is set to always spoof", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Guidance\\SeekerBase.cs", 384, "CheckCountermeasureLock");
			DM.Log("The " + _ammoObject.getUIDAndName() + ":  <color=cyan>locks on</color> " + obj.name + " because chaff override is set to always spoof", DM.EventLogFilters.Chaff);
			_lockedOnCountermeasure = obj;
			if (!_ammoObject._taskforce._weaponsSpent.ContainsKey(_ammoObject._ap._ammunitionFileName))
			{
				DM.E("Impossible");
				_ammoObject._taskforce._weaponsSpent.Add(_ammoObject._ap._ammunitionFileName, new WeaponsSpent(_ammoObject._ap._type, _ammoObject._ap._subType, _ammoObject._ap._targetType));
			}
			_ammoObject._taskforce._weaponsSpent[_ammoObject._ap._ammunitionFileName]._chaffed++;
			return true;
		}
		if (DM._chaffOverride == 2)
		{
			Log.Debug("The " + _ammoObject.getUIDAndName() + ": <color=magenta>rejects</color> " + obj.name + " because chaff override is set to always fail", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Guidance\\SeekerBase.cs", 399, "CheckCountermeasureLock");
			DM.Log("The " + _ammoObject.getUIDAndName() + ": <color=magenta>rejects</color> " + obj.name + " because chaff override is set to always fail", DM.EventLogFilters.Chaff);
			_ignoredObjects.RemoveAll((ObjectBase x) => x == null);
			if (!_ignoredObjects.Contains(obj))
			{
				_ignoredObjects.Add(obj);
			}
			_lockedOnCountermeasure = null;
			return false;
		}
		WeaponBase weaponBase = obj as WeaponBase;
		string text = "";
		string text2 = _ammoObject.FullIDData();
		if (_ammoObject._launchPlatform != null)
		{
			text = " of " + text;
		}
		float decoySpoofChance = weaponBase._ap._decoySpoofChance;
		float num = decoySpoofChance;
		decoySpoofChance -= _ap._antiChaffBonus;
		if (decoySpoofChance < 0.01f)
		{
			decoySpoofChance = 0.01f;
		}
		float num2 = Utils.RandomFloatAdv();
		if (num2 < 0f)
		{
			num2 = Utils.RandomFloat(0f, decoySpoofChance);
		}
		if (num2 > 1f)
		{
			num2 = Utils.RandomFloat(decoySpoofChance + 0.001f, 1f);
		}
		string text3 = string.Format("base = {0:0.##}, anti chaff bonus = {1}; <color={2}>Result</color> = {3:0.##} < {4:0.##}", num, _ap._antiChaffBonus, (num2 < decoySpoofChance) ? "cyan" : "magenta", num2, decoySpoofChance);
		if (num2 < decoySpoofChance)
		{
			if (obj.IsPlayerObject)
			{
				Globals._playerMissilesSpoofed++;
				if (Globals._playerMissilesSpoofed > Globals._playerMissilesSpoofedLimit)
				{
					Globals._playerMissilesSpoofed = 0;
					DM.Log("<color=orange>IGNORE:</color>: The " + _ammoObject.getUIDAndName() + ": <color=magenta>ignores</color> " + obj.getUIDAndName() + " <color=magenta>by override</color>; Dice data was " + text3 + "; but it is not used.", DM.EventLogFilters.Chaff);
					_ignoredObjects.RemoveAll((ObjectBase x) => x == null);
					if (!_ignoredObjects.Contains(obj))
					{
						_ignoredObjects.Add(obj);
					}
					_lockedOnCountermeasure = null;
					return false;
				}
			}
			DM.Log("<color=cyan>SPOOF</color>:" + text2 + text + " <color=cyan>spoofed</color> by " + weaponBase.getUIDAndName() + ": " + text3, DM.EventLogFilters.Chaff);
			_lockedOnCountermeasure = obj;
			_keepTarget = false;
			_firstLockedOnTarget = null;
			if (_ammoObject._type == ObjectBase.ObjectType.Missile)
			{
				((Missile)_ammoObject)._wasSpoofed = true;
			}
			if (!_ammoObject._taskforce._weaponsSpent.ContainsKey(_ammoObject._ap._ammunitionFileName))
			{
				DM.E("Impossible");
				_ammoObject._taskforce._weaponsSpent.Add(_ammoObject._ap._ammunitionFileName, new WeaponsSpent(_ammoObject._ap._type, _ammoObject._ap._subType, _ammoObject._ap._targetType));
			}
			_ammoObject._taskforce._weaponsSpent[_ammoObject._ap._ammunitionFileName]._chaffed++;
			return true;
		}
		DM.Log("<color=magenta>IGNORE</color>: " + text2 + text + " <color=magenta>ignores</color> " + weaponBase.getUIDAndName() + ": " + text3, DM.EventLogFilters.Chaff);
		if (obj.IsPlayerObject)
		{
			Globals._playerMissilesSpoofed = 0;
		}
		_ignoredObjects.RemoveAll((ObjectBase x) => x == null);
		if (!_ignoredObjects.Contains(obj))
		{
			_ignoredObjects.Add(obj);
		}
		_lockedOnCountermeasure = null;
		return false;
	}

	protected virtual void refreshCountermeasures()
	{
		if (GameTime.time - _timeOfLastIgnoredCountermeasuresListRefresh > 300f)
		{
			_ignoredObjects.Clear();
			_timeOfLastIgnoredCountermeasuresListRefresh = GameTime.time;
			_lockedOnCountermeasure = null;
			Log.Debug($"The list of ignored countermeasures is refreshed for {_ammoObject}", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Guidance\\SeekerBase.cs", 490, "refreshCountermeasures");
			DM.I($"The list of ignored countermeasures is refreshed for {_ammoObject}");
		}
	}

	protected bool isPositionInIgnoreAngle(Vector3 objPos)
	{
		bool result = false;
		Vector3 vector = objPos - _launcherPositionAtActivation;
		if (vector.sqrMagnitude < 0.1f)
		{
			result = true;
		}
		else
		{
			Vector3 forward = _ammoObject.getTransform().forward;
			vector.y = 0f;
			forward.y = 0f;
			if (Vector3.Angle(vector, forward) > 170f)
			{
				result = true;
			}
		}
		return result;
	}

	public virtual void setCurrentTarget(ObjectBase targetObject)
	{
	}

	public bool CheckCountermeasureLock()
	{
		if (_lockedOnCountermeasure == null)
		{
			return false;
		}
		if (!_lockedOnCountermeasure.IsDestroyed && isInSensorFOV(_ammoObject.transform.position, _ammoObject.transform.forward, _lockedOnCountermeasure.transform.position))
		{
			_potentialContacts.Add(_lockedOnCountermeasure);
			return true;
		}
		_lockedOnCountermeasure = null;
		if (this is IRSeeker)
		{
			_ammoObject.CurrentIntendedTargetObject = null;
		}
		if (_ammoObject._ap._minAfterSpoofEffectTime > 0f)
		{
			_spoofEndTime = GameTime.time + Utils.RandomFloat(_ammoObject._ap._minAfterSpoofEffectTime, _ammoObject._ap._maxAfterSpoofEffectTime);
		}
		else
		{
			_spoofEndTime = -1f;
		}
		return false;
	}

	public bool isInSensorFOV(GeoPosition ownGP, Vector3 ownTrajectory, GeoPosition targetGP, bool narrowFOVDown = false)
	{
		return isInSensorFOV(ownGP.ToUnity(), ownTrajectory, targetGP.ToUnity(), narrowFOVDown);
	}

	public bool isInSensorFOV(Vector3 ownPosition, Vector3 ownTrajectory, ObjectBase obj, bool narrowFOVDown = false)
	{
		if (obj == null)
		{
			return false;
		}
		if (_ammoObject == null)
		{
			return false;
		}
		Vector3 objPos = obj.transform.position;
		if (((obj is Vessel || obj is Submarine || obj is LandUnit) && _strongestSensorEcho != null && _strongestSensorEcho._objectBase == obj) || (_strongestSensorEcho == null && _ammoObject.CurrentIntendedTargetObject == obj))
		{
			if (_ammoObject._ap._guidanceType != AmmunitionParameters.GuidanceType.IRHoming)
			{
				objPos = _ammoObject.AimPointGeoPosition.ToUnity();
			}
			if (_ammoObject._ap._guidanceType != AmmunitionParameters.GuidanceType.AntiRadiationHoming && _strongestSensorEcho != null && _strongestSensorEcho._objectBase is LandUnit)
			{
				objPos = obj.transform.TransformPoint(_ammoObject._currentTargetPoint._position);
			}
		}
		if (!isInSensorFOV(ownPosition, ownTrajectory, objPos, narrowFOVDown))
		{
			return isInSensorFOV(ownPosition, ownTrajectory, obj.transform.position, narrowFOVDown);
		}
		return true;
	}

	public bool isInSensorFOV(Vector3 ownPosition, Vector3 ownTrajectory, Vector3 objPos, bool narrowFOVDown = false)
	{
		if (_ammoObject == null)
		{
			return false;
		}
		if (_ammoObject is Missile missile && (missile._poppingUp || missile._hasPoppedUp))
		{
			objPos.y = 0f;
			ownPosition.y = 0f;
			ownTrajectory.y = 0f;
		}
		if (_ammoObject._ap._ignoreHeightDifferenceForTargetDist)
		{
			objPos.y = ownPosition.y;
			ownTrajectory.y = 0f;
		}
		Vector3 to = objPos - ownPosition;
		float num = Vector3.Angle(ownTrajectory, to);
		if (_ammoObject._seeker is WakeSeeker && _ap._seekerFOV < 350f)
		{
			_ap._seekerFOV = 350f;
		}
		float num2 = (narrowFOVDown ? (_ap._seekerFOV / 3f) : _ap._seekerFOV);
		return num < num2 / 2f;
	}

	public bool isInGimbalFOV(GeoPosition ownGP, Vector3 ownTrajectory, GeoPosition targetGP)
	{
		return isInGimbalFOV(ownGP.ToUnity(), ownTrajectory, targetGP.ToUnity());
	}

	public bool isInGimbalFOV(Vector3 ownPosition, Vector3 ownTrajectory, ObjectBase obj)
	{
		Vector3 objPos = obj.transform.position;
		if ((obj is Vessel || obj is Submarine || obj is LandUnit) && _strongestSensorEcho != null && _strongestSensorEcho._objectBase == obj)
		{
			objPos = getEstimatedTargetPosition();
			if (_ammoObject._ap._guidanceType != AmmunitionParameters.GuidanceType.AntiRadiationHoming && _strongestSensorEcho != null && _strongestSensorEcho._objectBase is LandUnit)
			{
				objPos = _ammoObject.CurrentIntendedTargetObject.transform.TransformPoint(_ammoObject._currentTargetPoint._position);
			}
		}
		return isInGimbalFOV(ownPosition, ownTrajectory, objPos);
	}

	public bool isInGimbalFOV(Vector3 ownPosition, Vector3 ownTrajectory, Vector3 objPos)
	{
		bool result = false;
		Vector3 to = objPos - ownPosition;
		if (Vector3.Angle(ownTrajectory, to) < _ap._seekerGimbalFOV / 2f)
		{
			result = true;
		}
		return result;
	}

	protected bool isInRearAspect(ObjectBase targetObject)
	{
		if (!_ap._isRearAspectOnly)
		{
			return true;
		}
		return Utils.isInRearAspect(_ammoObject, targetObject, _ap);
	}

	protected bool isValidTarget(ObjectBase targetObject)
	{
		if (targetObject == null)
		{
			return false;
		}
		if (_ap._canNotAttackTypes.Contains(targetObject._type))
		{
			return false;
		}
		if (_guidanceType != AmmunitionParameters.GuidanceType.AntiRadiationHoming)
		{
			if (targetObject._sensorData == null)
			{
				return false;
			}
		}
		else if (targetObject._taskforce == _ammoObject._taskforce)
		{
			return false;
		}
		if (!targetObject._hasControl)
		{
			return false;
		}
		bool result = false;
		if ((_ap._targetType == Ammunition.Target.ASW || _ap._secondaryTargetType == Ammunition.Target.ASW) && targetObject.isSubmerged())
		{
			result = true;
		}
		if (_ap._targetType == Ammunition.Target.ASuW || _ap._secondaryTargetType == Ammunition.Target.ASuW)
		{
			if (targetObject is Vessel || targetObject is Submarine)
			{
				result = true;
			}
			else if (targetObject._type == ObjectBase.ObjectType.Chaff && _guidanceType != AmmunitionParameters.GuidanceType.AntiRadiationHoming && _guidanceType != AmmunitionParameters.GuidanceType.TVHoming && _guidanceType != AmmunitionParameters.GuidanceType.LaserHoming)
			{
				result = true;
			}
			else if (targetObject is LandUnit landUnit)
			{
				if (landUnit.LandUnitParams._landUnitSubType == LandUnitParameters.LandUnitSubType.OilRig)
				{
					result = true;
				}
				if (_ap._type == Ammunition.Type.Projectile || (_ap._type == Ammunition.Type.Bomb && _ap._subType != Ammunition.Type.Sonobuoy))
				{
					result = true;
				}
				else if (_ap._landAttack.Count > 0 && (!_ap._landAttack.Contains(Ammunition.LandAttack.ShoreTargetsOnly) || landUnit.isNearShore))
				{
					if (landUnit._isMobile)
					{
						if (_ap._landAttack.Contains(Ammunition.LandAttack.Mobile))
						{
							result = true;
						}
					}
					else if (_ap._landAttack.Contains(Ammunition.LandAttack.Installation))
					{
						result = true;
					}
				}
			}
		}
		if (_ap._targetType == Ammunition.Target.AAW || _ap._secondaryTargetType == Ammunition.Target.AAW)
		{
			if (targetObject._type == ObjectBase.ObjectType.Aircraft || targetObject._type == ObjectBase.ObjectType.Helicopter || targetObject._type == ObjectBase.ObjectType.Missile)
			{
				result = true;
			}
			if (_guidanceType != AmmunitionParameters.GuidanceType.AntiRadiationHoming && _guidanceType != AmmunitionParameters.GuidanceType.TVHoming && _guidanceType != AmmunitionParameters.GuidanceType.LaserHoming && targetObject._type == ObjectBase.ObjectType.Chaff)
			{
				result = true;
			}
		}
		if (_ammoObject is Missile && targetObject is Missile { _isTargetAirborne: not false })
		{
			result = false;
		}
		return result;
	}

	protected bool isDetectable(ObjectBase target)
	{
		bool result = false;
		if (target == null)
		{
			return result;
		}
		bool flag = true;
		if (_guidanceType == AmmunitionParameters.GuidanceType.WakeHoming)
		{
			flag = false;
		}
		if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand && !_ammoObject.ConnectionLost.Value)
		{
			flag = false;
		}
		if (flag && !Singleton<UnitSensorCache>.Instance.UnitsHaveDirectlLOS(_ammoObject, target, (_ammoObject.transform.position - target.transform.position).magnitude))
		{
			return result;
		}
		AmmunitionParameters.GuidanceType guidanceType = _guidanceType;
		if ((guidanceType != AmmunitionParameters.GuidanceType.TVHoming && guidanceType != AmmunitionParameters.GuidanceType.IRHoming) ? isInRange(target.getUnityPosition()) : isInRange(target))
		{
			switch (_guidanceType)
			{
			case AmmunitionParameters.GuidanceType.IRHoming:
			case AmmunitionParameters.GuidanceType.SemiActiveRadarHoming:
			case AmmunitionParameters.GuidanceType.ActiveRadarHoming:
				if (target is Submarine submarine3 && !submarine3.isSubmerged())
				{
					result = true;
				}
				else if (target.isUnit() || target._type == ObjectBase.ObjectType.Chaff)
				{
					result = true;
				}
				else if (target is Missile)
				{
					result = true;
				}
				else if (target is Bomb bomb2 && bomb2._seeker._guidanceType != AmmunitionParameters.GuidanceType.None)
				{
					result = true;
				}
				break;
			case AmmunitionParameters.GuidanceType.LaserHoming:
			case AmmunitionParameters.GuidanceType.TVHoming:
				if (target is Submarine submarine2 && !submarine2.isSubmerged())
				{
					result = true;
				}
				else if (target.isUnit() || target._type == ObjectBase.ObjectType.Chaff)
				{
					result = true;
				}
				break;
			case AmmunitionParameters.GuidanceType.ActiveSonarHoming:
				if ((target._type == ObjectBase.ObjectType.Submarine || target._type == ObjectBase.ObjectType.Biologic || target._type == ObjectBase.ObjectType.Noisemaker || (target is Torpedo torpedo4 && (torpedo4._ap._subType == Ammunition.Type.MOSS || _ammoObject._ap._antiTorpedo))) && target.isSubmerged())
				{
					result = true;
				}
				break;
			case AmmunitionParameters.GuidanceType.PassiveSonarHoming:
				if (_ammoObject._ap._targetType == Ammunition.Target.ASuW || _ammoObject._ap._secondaryTargetType == Ammunition.Target.ASuW)
				{
					if (target._type == ObjectBase.ObjectType.Vessel)
					{
						if (target.getUnityPosition().y >= -0.14880937f)
						{
							result = true;
						}
					}
					else if (target._type == ObjectBase.ObjectType.Submarine || target._type == ObjectBase.ObjectType.Biologic)
					{
						if (!target.isSubmerged())
						{
							result = true;
						}
					}
					else if ((target._type == ObjectBase.ObjectType.Noisemaker || (target is Torpedo torpedo && torpedo._ap._subType == Ammunition.Type.MOSS)) && target.getUnityPosition().y >= -0.29761875f)
					{
						result = true;
					}
				}
				if ((_ammoObject._ap._targetType == Ammunition.Target.ASW || _ammoObject._ap._secondaryTargetType == Ammunition.Target.ASW) && (target._type == ObjectBase.ObjectType.Submarine || target._type == ObjectBase.ObjectType.Noisemaker || (target is Torpedo torpedo2 && (torpedo2._ap._subType == Ammunition.Type.MOSS || _ammoObject._ap._antiTorpedo))) && target.isSubmerged())
				{
					result = true;
				}
				break;
			case AmmunitionParameters.GuidanceType.ActivePassiveSonarHoming:
				if (target._type == ObjectBase.ObjectType.Submarine || target._type == ObjectBase.ObjectType.Biologic || target._type == ObjectBase.ObjectType.Vessel || target._type == ObjectBase.ObjectType.Noisemaker || (target is Torpedo torpedo3 && (torpedo3._ap._subType == Ammunition.Type.MOSS || _ammoObject._ap._antiTorpedo)))
				{
					result = true;
				}
				break;
			case AmmunitionParameters.GuidanceType.WakeHoming:
				if (target.GetType() == typeof(WakeBubble) && target.getHeightInMeters() < 0f)
				{
					result = true;
				}
				break;
			case AmmunitionParameters.GuidanceType.AntiRadiationHoming:
				if (target.IsAirUnit)
				{
					result = false;
				}
				else if (!target.isSubmerged())
				{
					result = true;
				}
				break;
			case AmmunitionParameters.GuidanceType.None:
				if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
				{
					if (target is Submarine submarine && !submarine.isSubmerged())
					{
						result = true;
					}
					else if (target.isUnit() || target._type == ObjectBase.ObjectType.Chaff)
					{
						result = true;
					}
					else if (target is Missile)
					{
						result = true;
					}
					else if (target is Bomb bomb && bomb._seeker._guidanceType != AmmunitionParameters.GuidanceType.None)
					{
						result = true;
					}
				}
				break;
			}
		}
		return result;
	}

	protected void searchPassiveRadioEmissionTarget()
	{
		_contactsInRange.Clear();
		Vector3 forward = _ammoObject.transform.forward;
		Vector3 unityPosition = _ammoObject.getUnityPosition();
		SensorEcho strongestSensorEcho = _strongestSensorEcho;
		_lockedOnTarget = null;
		foreach (ObjectBase potentialContact in _potentialContacts)
		{
			if (isInSensorFOV(unityPosition, forward, potentialContact.getUnityPosition()))
			{
				_contactsInRange.Add(potentialContact);
			}
		}
		double num = 0.0;
		_strongestSensorEcho = null;
		foreach (ObjectBase item in _contactsInRange)
		{
			_detectedEmittingSystems.Clear();
			double num2 = 0.0;
			if (_ammoObject._ap._secondaryPassiveRadarGuidanceType != AmmunitionParameters.SecondaryPassiveRadarGuidanceType.None)
			{
				num2 = RadarCalculator.getSignalStrength(item, _ammoObject, unityPosition, _ap._gainFactor, _ap._passiveRadarHomingFrequencies, out _detectedEmittingSystems, 1.3f, _ammoObject._ap._secondaryPassiveRadarGuidanceType == AmmunitionParameters.SecondaryPassiveRadarGuidanceType.Full);
			}
			if (!(num2 < Globals.RcsValues._minDetectableReturnPowerESM) && _detectedEmittingSystems.Count >= 1)
			{
				SensorEcho sensorEcho = createSensorEchoForAntiRadiationSeeker(item, num2, item.transform.position);
				_sensorEchoesInRange.Add(sensorEcho);
				_strongestSensorEcho = sensorEcho;
				if (sensorEcho._signalValue > num)
				{
					num = sensorEcho._signalValue;
					_lockedOnTarget = item;
					_targetLastSeenTime = GameTime.time;
					_strongestSensorEcho = sensorEcho;
				}
			}
		}
		CheckMemorizedTarget();
		if (_strongestSensorEcho == null && GameTime.time - _targetLastSeenTime < 2f)
		{
			_strongestSensorEcho = strongestSensorEcho;
		}
	}

	protected SensorEcho createSensorEchoForAntiRadiationSeeker(ObjectBase obj, double signalAtSensor, Vector3 targetRadarPosition)
	{
		SensorEcho freeInstance = Globals._sensorEchoes.getFreeInstance();
		freeInstance._objectUID = obj.UniqueID;
		freeInstance._objectBase = obj;
		freeInstance._unityPosition = obj.getUnityPosition();
		freeInstance._unitySpeedVector = obj.UnityVelocityVector;
		freeInstance._directionToTarget = freeInstance._unityPosition - _ammoObject.transform.position;
		freeInstance._distanceToSensorInUnity = freeInstance._directionToTarget.magnitude;
		freeInstance._distanceToSensorInM = freeInstance._distanceToSensorInUnity * 67.200066f;
		freeInstance._toTargetAngleRad = Utils.atan2ToMinusPiToPlusPi(Mathf.Atan2(freeInstance._directionToTarget.z, freeInstance._directionToTarget.x));
		freeInstance._rcs = 0f;
		freeInstance._signalValue = signalAtSensor;
		freeInstance._unityPosition = targetRadarPosition;
		return freeInstance;
	}

	public void CheckMemorizedTarget()
	{
		if (_lockedOnCountermeasure != null)
		{
			return;
		}
		if (_firstLockedOnTarget != null && (_firstLockedOnTarget._type == ObjectBase.ObjectType.Noisemaker || _firstLockedOnTarget._type == ObjectBase.ObjectType.Noisemaker))
		{
			_firstLockedOnTarget = null;
		}
		bool flag = _ammoObject is Torpedo torpedo && torpedo._onWire;
		if (_ammoObject is Missile && !_ammoObject.ConnectionLost.Value && (_guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming || _ammoObject._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand))
		{
			flag = true;
		}
		if (((!flag && !_keepTarget) || !(_ammoObject.CurrentIntendedTargetObject != null)) && !(_firstLockedOnTarget != null))
		{
			return;
		}
		if ((flag || _keepTarget) && _ammoObject.CurrentIntendedTargetObject != null)
		{
			foreach (SensorEcho item in _sensorEchoesInRange)
			{
				if (item._objectBase == _ammoObject.CurrentIntendedTargetObject)
				{
					_strongestSensorEcho = item;
					return;
				}
			}
		}
		if (_firstLockedOnTarget != null)
		{
			foreach (SensorEcho item2 in _sensorEchoesInRange)
			{
				if (item2._objectBase == _firstLockedOnTarget)
				{
					_strongestSensorEcho = item2;
					return;
				}
			}
		}
		if (flag)
		{
			_strongestSensorEcho = null;
		}
	}

	internal bool isWakeHomingOnAimpoint(GeoPosition aimpointGeo)
	{
		bool result = true;
		if (_guidanceType == AmmunitionParameters.GuidanceType.WakeHoming && Mathf.Abs(Utils.calculateYawToTarget(_ammoObject.transform.forward, _ammoObject.Position.Value, aimpointGeo)) > 2.5f)
		{
			result = false;
		}
		return result;
	}

	internal bool isWakeHomingNearAimpoint(GeoPosition aimpointGeo)
	{
		bool result = true;
		if (_guidanceType == AmmunitionParameters.GuidanceType.WakeHoming)
		{
			if (_ammoObject._geoPosition.GetDistance(aimpointGeo) > 27.55949592590332)
			{
				result = false;
			}
		}
		else
		{
			result = false;
		}
		return result;
	}
}
