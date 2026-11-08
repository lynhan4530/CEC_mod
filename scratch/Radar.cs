using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace SeaPower;

public abstract class Radar : ISensor, IObservable<Entity>
{
	public ObjectBase _homeObject;

	public SensorSystemRadar _parentRadarSystem;

	public RadarParameters _rp;

	public bool _isActive;

	protected List<SensorEcho> _rawSensorEchoes = new List<SensorEcho>();

	protected List<List<SensorEcho>> _intersectedEchoes = new List<List<SensorEcho>>();

	protected DebugSensorContact _debugContactData;

	private ISensor _sensorImplementation;

	public bool IsFireControlRadar
	{
		get
		{
			if (_parentRadarSystem != null)
			{
				return _parentRadarSystem._type == SensorSystemRadar.RadarType.Targeting;
			}
			return false;
		}
	}

	public ObjectBase Host => _homeObject;

	public GeoPosition PlatformPosition => _parentRadarSystem._baseObject.Position.Value;

	public float AngularResolutionInDegrees => _rp._beamWidth / 2f;

	public float MaxRangeInMeters => _rp._maxRange;

	public float MaxDistanceError => _rp._rangeResolution;

	public float MaxTrackAge => 1f;

	public virtual void init(ObjectBase homeObject, RadarParameters rp)
	{
		_homeObject = homeObject;
		_rp = rp;
	}

	public abstract IDisposable Subscribe(IObserver<Entity> observer);

	public virtual void setActive(bool active)
	{
		_isActive = active;
	}

	public bool IsDetectableTarget_Unconditional(ObjectBase obj, bool isSearchRadar = false, bool ignoreTaskforce = false)
	{
		if (obj == null)
		{
			return false;
		}
		if (_homeObject == null)
		{
			Log.Error("!!!! Radar's homeObject = null", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Sensors\\Radar.cs", 60, "IsDetectableTarget_Unconditional");
			return false;
		}
		if (obj == _homeObject)
		{
			return false;
		}
		if (obj._type == ObjectBase.ObjectType.Other)
		{
			return false;
		}
		if (obj is WakeBubble)
		{
			return false;
		}
		if (obj is Noisemaker)
		{
			return false;
		}
		if (obj._isUndetectable)
		{
			return false;
		}
		if (obj._sensorData == null)
		{
			return false;
		}
		if (obj._sensorData._maxRCS < 0f)
		{
			return false;
		}
		if (!obj._hasControl)
		{
			return false;
		}
		if (isSearchRadar)
		{
			if (!ignoreTaskforce && obj._taskforce == _homeObject._taskforce)
			{
				return false;
			}
			if (obj.IsDestroyed)
			{
				return false;
			}
		}
		if (obj._obp != null)
		{
			if (obj._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Deco))
			{
				return false;
			}
			if (obj._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SeaMine))
			{
				return false;
			}
		}
		if (obj.transform.position.y < 0.01f && !(obj is Vessel) && !(obj is Submarine))
		{
			return false;
		}
		if (obj is Submarine submarine && submarine.isSubmerged())
		{
			if (submarine.getRaisedMastsCount() == 0)
			{
				return false;
			}
			if (!_rp._canDetectPeriscope)
			{
				return false;
			}
		}
		if (!_rp._canDetectAirToAirMissiles && obj is Missile missile && missile._launchPlatform != null && missile._launchPlatform.IsAirUnit)
		{
			ObjectBase objectBase = missile.CurrentIntendedTargetObject ?? missile.CurrentTarget;
			if (objectBase != null && objectBase.isAirTarget())
			{
				return false;
			}
		}
		if (isSearchRadar)
		{
			if (obj.isAirTarget())
			{
				if (_rp._role == RadarParameters.Role.Surface)
				{
					return false;
				}
			}
			else
			{
				if (!(obj is Vessel) && !(obj is LandUnit) && !(obj is Submarine))
				{
					return false;
				}
				if (_rp._role != RadarParameters.Role.Surface && _rp._role != RadarParameters.Role.SurfaceAndAir)
				{
					return false;
				}
				if (obj is LandUnit && !_rp._canDetectLandTargets && isSearchRadar)
				{
					return false;
				}
			}
		}
		else if (obj is LaunchTransient)
		{
			return false;
		}
		return true;
	}

	public bool IsDetectableTarget_Conditional(ObjectBase obj, bool debugMode = false)
	{
		if (obj.isAirTarget() && _rp._role != RadarParameters.Role.Surface && !IsFireControlRadar && (obj.AltitudeOverClutter < _rp._minAltitude || obj.transform.position.y > _rp._maxAltitude))
		{
			if (debugMode)
			{
				_debugContactData._outOfAltitudeBrackets = true;
			}
			else if (!debugMode)
			{
				return false;
			}
		}
		Vector3 sensorPosition = _parentRadarSystem.SensorPosition;
		float magnitude = (obj.transform.position - sensorPosition).magnitude;
		if ((magnitude < _rp._minRange || magnitude > _rp._maxRange) && !debugMode)
		{
			return false;
		}
		LineOfSightResult lineOfSightResult = Singleton<UnitSensorCache>.Instance.UnitsHaveRadarlLOS(_homeObject, obj, _parentRadarSystem.SensorPosition.y, magnitude);
		if (!lineOfSightResult._los || !lineOfSightResult._isValid)
		{
			if (!debugMode)
			{
				return false;
			}
			_debugContactData._noLOS = true;
		}
		float horizon = lineOfSightResult._horizon;
		if (debugMode)
		{
			_debugContactData._currentHorizonNM = horizon * 0.036285132f;
		}
		if (magnitude > horizon)
		{
			if (!debugMode)
			{
				return false;
			}
			_debugContactData._beyoundHorizon = true;
		}
		Vector3 forward = _parentRadarSystem._mountObject.transform.forward;
		if (!_parentRadarSystem.IsObjectInFOV(sensorPosition, forward, obj.transform.position))
		{
			if (!debugMode)
			{
				return false;
			}
			_debugContactData._notInFOV = true;
		}
		if ((double)_parentRadarSystem._lookDownMultiplier < 1.0 && _parentRadarSystem.IsInLookDown(_parentRadarSystem.SensorPosition, obj, horizon))
		{
			if (debugMode)
			{
				_debugContactData._inLookDown = true;
			}
			if (magnitude > _parentRadarSystem._lookDownRange)
			{
				if (!debugMode)
				{
					return false;
				}
				_debugContactData._beyondLookdownRange = true;
			}
		}
		if (debugMode && !_debugContactData._inLookDown && _parentRadarSystem.IsInLookDown(_parentRadarSystem.SensorPosition, obj, horizon))
		{
			_debugContactData._inLookDown = true;
		}
		return true;
	}

	protected void ClearDebugData()
	{
		_debugContactData._outOfAltitudeBrackets = false;
		_debugContactData._noLOS = false;
		_debugContactData._currentHorizonNM = -1f;
		_debugContactData._beyoundHorizon = false;
		_debugContactData._notInFOV = false;
		_debugContactData._inLookDown = false;
		_debugContactData._beyondLookdownRange = false;
	}

	public virtual void CalculateRawSensorContacts()
	{
		Log.Error("call of calculateRawSensorContacts in base class", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Sensors\\Radar.cs", 214, "CalculateRawSensorContacts");
	}

	public virtual void calculateClusteredSensorContacts()
	{
		Log.Error("call of calculateClusteredSensorContacts in base class", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Sensors\\Radar.cs", 219, "calculateClusteredSensorContacts");
	}

	protected Entity CreateRadarTrack(SensorSystem.SensorType sensorType, ObjectBase target, Vector3 knownPosition, float rangeErrorInMeters, float bearingError)
	{
		_parentRadarSystem._detectedObjects.Add(target);
		Vector3 vector = _homeObject.transform.position - knownPosition;
		Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
		Entity entity = TrackComponent.MakeEmptyTrack(defaultGameObjectInjectionWorld, _homeObject, target, _parentRadarSystem.DisplayName.Value, sensorType, _parentRadarSystem.InstanceEntity, typeof(TrackComponent), typeof(TrackBearing), typeof(TrackRangeInMeters), typeof(TrackAltitude));
		if (_parentRadarSystem._type == SensorSystemRadar.RadarType.Search)
		{
			float num = ReactionTime.ObserveScanPeriod(_parentRadarSystem.ScanPeriodSeconds, _homeObject);
			if (num > 0f)
			{
				TrackComponent componentData = defaultGameObjectInjectionWorld.EntityManager.GetComponentData<TrackComponent>(entity);
				float num2 = num * 1.5f;
				if (num2 > componentData.MaxAge)
				{
					componentData.MaxAge = num2;
					defaultGameObjectInjectionWorld.EntityManager.SetComponentData(entity, componentData);
				}
			}
		}
		defaultGameObjectInjectionWorld.EntityManager.SetComponentData(entity, new TrackBearing
		{
			Estimate = new PointEstimate
			{
				Error = bearingError * bearingError,
				Estimate = Mathf.Atan2(0f - vector.x, 0f - vector.z) * 180f / MathF.PI
			}
		});
		defaultGameObjectInjectionWorld.EntityManager.SetComponentData(entity, new TrackRangeInMeters
		{
			Estimate = new PointEstimate
			{
				Error = rangeErrorInMeters * rangeErrorInMeters,
				Estimate = new Vector3(vector.x, 0f, vector.z).magnitude * 67.200066f
			}
		});
		defaultGameObjectInjectionWorld.EntityManager.SetComponentData(entity, new TrackAltitude
		{
			Estimate = new PointEstimate
			{
				Error = 0f,
				Estimate = knownPosition.y
			}
		});
		if (target.isWeapon())
		{
			defaultGameObjectInjectionWorld.EntityManager.AddComponentData(entity, new DetectedSide(target._taskforce.Entity));
		}
		DetectedSignature sig = defaultGameObjectInjectionWorld.EntityManager.GetComponentData<DetectedSignature>(entity);
		if (!(target is AI_Marker))
		{
			int? num3 = Singleton<SensorMetadataProvider>.Instance.RCSSignature(RCSValues.ReadRCSValue(target._sensorData._size));
			if (num3.HasValue)
			{
				int valueOrDefault = num3.GetValueOrDefault();
				ClassInfoUtils.SetBit(ref sig, valueOrDefault);
				defaultGameObjectInjectionWorld.EntityManager.AddComponentData(entity, sig);
			}
		}
		return entity;
	}
}
