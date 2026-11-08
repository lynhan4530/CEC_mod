using System;
using UniRx;
using Unity.Entities;
using UnityEngine;

namespace SeaPower;

public class ActiveRadarSeeker : SeekerBase, ISensor, IObservable<Entity>
{
	protected Subject<Entity> _subscribersImpl = new Subject<Entity>();

	private bool passiveGuidanceMode;

	private ObjectBase _lastTrackedObject;

	private double _lastTrackTime;

	private const float ANTENNA_COAST_TIME = 5f;

	private bool _coneLossLogged;

	public ObjectBase Host => _ammoObject;

	public GeoPosition PlatformPosition => _ammoObject.Position.Value;

	public float AngularResolutionInDegrees => 0f;

	public float MaxRangeInMeters => _ap._seekerActiveRange;

	public float MaxDistanceError => 1f;

	public float MaxTrackAge => 1f;

	public ActiveRadarSeeker()
	{
		base.ActiveMode.Value = true;
		_guidanceType = AmmunitionParameters.GuidanceType.ActiveRadarHoming;
	}

	public IDisposable Subscribe(IObserver<Entity> observer)
	{
		return _subscribersImpl.Subscribe(observer);
	}

	public override bool isInRange(Vector3 targetPosition)
	{
		Vector3 unityPosition = _ammoObject.getUnityPosition();
		float magnitude = (targetPosition - unityPosition).magnitude;
		if (_ap._secondaryPassiveRadarGuidanceType != 0 && magnitude < _ap._seekerPassiveRange)
		{
			return true;
		}
		if (magnitude < _ap._seekerActiveRange)
		{
			return true;
		}
		return false;
	}

	protected override void createListOfPotentialContacts()
	{
		foreach (ObjectBase listOfAllObject in Singleton<ObjectsManager>.Instance._listOfAllObjects)
		{
			if (!(listOfAllObject == null) && !(_ammoObject == listOfAllObject) && (listOfAllObject._obp == null || (!listOfAllObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Deco) && !listOfAllObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.SeaMine))) && !listOfAllObject.isSubmerged() && isValidTarget(listOfAllObject) && isDetectable(listOfAllObject) && !_ignoredObjects.Contains(listOfAllObject) && (_mode != 0 || !_ap._defaultIgnoredTypes.Contains(listOfAllObject._type)) && (_mode != Ammunition.SeekerMode.LandUnit || listOfAllObject is LandUnit))
			{
				_potentialContacts.Add(listOfAllObject);
			}
		}
	}

	protected override void searchForTarget()
	{
		_contactsInRange.Clear();
		Vector3 forward = _ammoObject.transform.forward;
		Vector3 unityPosition = _ammoObject.getUnityPosition();
		SensorEcho strongestSensorEcho = _strongestSensorEcho;
		_lockedOnTarget = null;
		bool flag = _ammoObject is Missile missile && (missile._poppingUp || missile._hasPoppedUp);
		Vector3 from = forward;
		if (flag)
		{
			from.y = 0f;
		}
		if (strongestSensorEcho?._objectBase != null)
		{
			_lastTrackedObject = strongestSensorEcho._objectBase;
			_lastTrackTime = GameTime.missionElapsedTime;
		}
		else if (_lastTrackedObject != null && (_lastTrackedObject.IsDestroyed || GameTime.missionElapsedTime - _lastTrackTime > 5.0 || (_lastTrackedObject._type != ObjectBase.ObjectType.Chaff && _ammoObject.CurrentIntendedTargetObject != null && _ammoObject.CurrentIntendedTargetObject != _lastTrackedObject)))
		{
			_lastTrackedObject = null;
		}
		Vector3 vector = ((strongestSensorEcho?._objectBase != null) ? (strongestSensorEcho._objectBase.transform.position - unityPosition) : ((!(_lastTrackedObject != null)) ? (_ammoObject.AimPointGeoPosition.ToUnity() - unityPosition) : (_lastTrackedObject.transform.position - unityPosition)));
		float num = Mathf.Max(_ap._seekerGimbalFOV, _ap._seekerFOV) * 0.5f;
		Vector3 to = vector;
		if (flag)
		{
			to.y = 0f;
		}
		if (vector.sqrMagnitude < 0.0001f || Vector3.Angle(from, to) > num)
		{
			vector = forward;
		}
		if (strongestSensorEcho?._objectBase != null && !strongestSensorEcho._objectBase.IsDestroyed)
		{
			Vector3 to2 = strongestSensorEcho._objectBase.transform.position - unityPosition;
			if (flag)
			{
				to2.y = 0f;
			}
			Vector3 from2 = vector;
			if (flag)
			{
				from2.y = 0f;
			}
			float num2 = Vector3.Angle(from2, to2);
			float num3 = Vector3.Angle(from, to2);
			bool flag2 = num2 < _ap._seekerFOV * 0.5f && num3 < num + _ap._seekerFOV * 0.5f;
			if (!flag2 && !_coneLossLogged)
			{
				_coneLossLogged = true;
				DM.Log(_ammoObject.FullIDData() + " losing track of " + strongestSensorEcho._objectBase.getUIDAndName() + ": target left seeker cone " + $"(off-head {num2:0.0}° vs FOV ±{_ap._seekerFOV * 0.5f:0.0}°, off-boresight {num3:0.0}° vs gimbal ±{num + _ap._seekerFOV * 0.5f:0.0}°, " + $"missile pitch {Utils.WrapAngle(_ammoObject.transform.localEulerAngles.x):0.0}°, range {to2.magnitude / 27.559496f:0.00} nmi)", DM.EventLogFilters.Debug);
			}
			else if (flag2)
			{
				_coneLossLogged = false;
			}
		}
		foreach (ObjectBase potentialContact in _potentialContacts)
		{
			Vector3 to3 = potentialContact.transform.position - unityPosition;
			if (flag)
			{
				to3.y = 0f;
			}
			if (isInSensorFOV(unityPosition, vector, potentialContact) && Vector3.Angle(from, to3) < num + _ap._seekerFOV * 0.5f)
			{
				_contactsInRange.Add(potentialContact);
			}
		}
		double num4 = 0.0;
		_strongestSensorEcho = null;
		bool flag3 = GameTime.missionElapsedTime - _activationTime > 2.0 && _firstLockedOnTarget == null;
		foreach (ObjectBase item3 in _contactsInRange)
		{
			if (_ignoredObjects.Contains(item3) || !RadarCalculator.createRadarEcho(_ammoObject.transform.position, item3, _waveLength, _ap._gainFactor, _ap._radarSeekerPeakPower, _ammoObject._ECMSystemsTryingToJamMe, _ammoObject._ap._frequency, _ammoObject._crew, out var sensorEcho) || (!flag3 && _ap._sizeGate > 0f && _originalTargetSize > 0f && (item3._sensorData == null || item3._sensorData._maxRCS > _originalTargetSize * _ap._sizeGate || item3._sensorData._maxRCS < _originalTargetSize / _ap._sizeGate)))
			{
				continue;
			}
			if (_ap._rangeGate > 0f)
			{
				float num5 = Mathf.Pow(2f, Mathf.Max(0f, (float)(GameTime.missionElapsedTime - _activationTime)));
				float num6 = MathF.Min(_ap._rangeGate * num5, 1000f);
				Vector3 vector2 = _originalTargetPosition.ToUnity() + _originalTargetVelocity * (float)(GameTime.missionElapsedTime - _ammoObject._launchTime);
				if ((item3.transform.position - vector2).magnitude > num6 * 27.559496f)
				{
					continue;
				}
			}
			if (sensorEcho._distanceToSensorInUnity > _ap._seekerActiveRange)
			{
				continue;
			}
			float f = sensorEcho._distanceToSensorInM / 1000f * 0.5399568f;
			sensorEcho._rcs /= Mathf.Pow(f, 2f);
			sensorEcho._signalValue = sensorEcho._rcs / Mathf.Pow(f, 2f);
			_sensorEchoesInRange.Add(sensorEcho);
			if (_ammoObject._ap._sharedSensorLink && !_ammoObject.ConnectionLost.Value)
			{
				(float heading, float range) tuple = _ammoObject._geoPosition.HeadingRangeMeters(sensorEcho._objectBase._geoPosition);
				float item = tuple.heading;
				float item2 = tuple.range;
				Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
				Entity entity = TrackComponent.MakeEmptyTrack(defaultGameObjectInjectionWorld, _ammoObject, sensorEcho._objectBase, "RadarSeeker", SensorSystem.SensorType.ARS, Entity.Null, typeof(TrackComponent), typeof(TrackBearing), typeof(TrackRangeInMeters), typeof(TrackAltitude));
				defaultGameObjectInjectionWorld.EntityManager.SetComponentData(entity, new TrackBearing
				{
					Estimate = new PointEstimate
					{
						Error = 0f,
						Estimate = item
					}
				});
				defaultGameObjectInjectionWorld.EntityManager.SetComponentData(entity, new TrackRangeInMeters
				{
					Estimate = new PointEstimate
					{
						Error = 0f,
						Estimate = item2
					}
				});
				defaultGameObjectInjectionWorld.EntityManager.SetComponentData(entity, new TrackAltitude
				{
					Estimate = new PointEstimate
					{
						Error = 0f,
						Estimate = (float)sensorEcho._objectBase._geoPosition._height
					}
				});
				DetectedSignature sig = defaultGameObjectInjectionWorld.EntityManager.GetComponentData<DetectedSignature>(entity);
				int? num7 = Singleton<SensorMetadataProvider>.Instance.RCSSignature(RCSValues.ReadRCSValue(sensorEcho._objectBase._sensorData._size));
				if (num7.HasValue)
				{
					int valueOrDefault = num7.GetValueOrDefault();
					ClassInfoUtils.SetBit(ref sig, valueOrDefault);
					defaultGameObjectInjectionWorld.EntityManager.AddComponentData(entity, sig);
				}
				defaultGameObjectInjectionWorld.EntityManager.AddComponentData(entity, new IncomingSignalData
				{
					_sensorType = SensorSystem.SensorType.ARS,
					_contactType = "ARSContact",
					_incomingSignalValue = sensorEcho._signalValue
				});
				_subscribersImpl.OnNext(entity);
			}
			if (CheckCountermeasureLock(item3) && (_lockedOnCountermeasure != null || sensorEcho._signalValue > num4))
			{
				num4 = sensorEcho._signalValue;
				_targetLastSeenTime = GameTime.missionElapsedTime;
				_strongestSensorEcho = sensorEcho;
				if (_lockedOnCountermeasure != null)
				{
					break;
				}
			}
		}
		CheckMemorizedTarget();
		if (_strongestSensorEcho == null)
		{
			if (_ap._secondaryPassiveRadarGuidanceType != 0)
			{
				searchPassiveRadioEmissionTarget();
			}
			if (_strongestSensorEcho == null && GameTime.missionElapsedTime - _targetLastSeenTime < 2.0)
			{
				_strongestSensorEcho = strongestSensorEcho;
			}
		}
		else if (strongestSensorEcho == null || _strongestSensorEcho._objectBase != strongestSensorEcho._objectBase)
		{
			OffsetStrongestEchoPosition();
			_firstLockedOnTarget = _strongestSensorEcho._objectBase;
		}
		else if (_strongestSensorEcho._objectBase == strongestSensorEcho._objectBase)
		{
			_strongestSensorEcho._localOffset = strongestSensorEcho._localOffset;
		}
		if (_strongestSensorEcho != null)
		{
			_lockedOnTarget = _strongestSensorEcho._objectBase;
		}
	}

	public override void init(ObjectBase launcherObject, ObjectBase ammoObject, AmmunitionParameters ap)
	{
		base.init(launcherObject, ammoObject, ap);
		_waveLength = _ap._waveLength;
		if (launcherObject != null && ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
		{
			Subscribe(launcherObject.PlottingTable);
		}
	}
}
