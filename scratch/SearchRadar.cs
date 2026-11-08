using System;
using System.Collections.Generic;
using UniRx;
using Unity.Entities;
using UnityEngine;

namespace SeaPower;

public class SearchRadar : Radar
{
	private Subject<Entity> _subscribersImpl = new Subject<Entity>();

	private double _nextPaintTime = double.NegativeInfinity;

	private bool _skipThisUpdate;

	private bool _sweepGating;

	private readonly HashSet<int> _paintedLastSweep = new HashSet<int>();

	private readonly HashSet<int> _paintedThisSweep = new HashSet<int>();

	private float SweepPeriod()
	{
		if (_parentRadarSystem == null || _parentRadarSystem._type != SensorSystemRadar.RadarType.Search)
		{
			return 0f;
		}
		return ReactionTime.ObserveScanPeriod(_parentRadarSystem.ScanPeriodSeconds, _homeObject);
	}

	public override void CalculateRawSensorContacts()
	{
		float num = SweepPeriod();
		_sweepGating = num > 0f;
		_skipThisUpdate = false;
		if (_sweepGating)
		{
			double missionElapsedTime = GameTime.missionElapsedTime;
			if (double.IsNegativeInfinity(_nextPaintTime))
			{
				_nextPaintTime = missionElapsedTime + Globals._rnd.NextDouble() * (double)num;
				_skipThisUpdate = true;
				return;
			}
			if (missionElapsedTime < _nextPaintTime)
			{
				_skipThisUpdate = true;
				return;
			}
			_nextPaintTime = missionElapsedTime + (double)num;
			_paintedLastSweep.Clear();
			_paintedLastSweep.UnionWith(_paintedThisSweep);
			_paintedThisSweep.Clear();
		}
		_rawSensorEchoes.Clear();
		if (_isActive)
		{
			createRadarEchoes();
		}
	}

	public override void calculateClusteredSensorContacts()
	{
		if (!_skipThisUpdate)
		{
			_intersectedEchoes.Clear();
			if (_isActive)
			{
				createRadarEchoClusters();
			}
		}
	}

	public override IDisposable Subscribe(IObserver<Entity> observer)
	{
		return _subscribersImpl.Subscribe(observer);
	}

	private void createRadarEchoes()
	{
		if (_parentRadarSystem.SensorPosition.y < 0f && (!(_homeObject is Submarine) || ((Submarine)_homeObject).isBelowPeriscopeDepth()))
		{
			return;
		}
		bool flag = DM_Sensors._debugRadar == this;
		if (flag)
		{
			DM_Sensors._debugRadarData.Clear();
		}
		foreach (ObjectBase listOfAllObject in Singleton<ObjectsManager>.Instance._listOfAllObjects)
		{
			if (!IsDetectableTarget_Unconditional(listOfAllObject, isSearchRadar: true))
			{
				continue;
			}
			if (flag)
			{
				ClearDebugData();
			}
			if (!IsDetectableTarget_Conditional(listOfAllObject, flag))
			{
				continue;
			}
			SensorEcho sensorEcho = Globals._sensorEchoes.getFreeInstance();
			if (!RadarCalculator.createRadarEcho(_parentRadarSystem.SensorPosition, listOfAllObject, _rp._waveLength, _rp._gainFactor, _rp._peakPower, _homeObject._ECMSystemsTryingToJamMe, _rp._frequency, _homeObject._crew, out sensorEcho, ignoreAspect: false, _rp._jamResistance, ignoreSelfScreening: true, null, ignoreSupportJammers: false, RadarCalculator.IsAirborneSensor(_homeObject) ? null : _rp, _parentRadarSystem.SensorPosition.y, _parentRadarSystem != null && _parentRadarSystem._lookDownMultiplier < 1f) && !flag)
			{
				continue;
			}
			if (flag)
			{
				float magnitude = (listOfAllObject.transform.position - _parentRadarSystem.SensorPosition).magnitude;
				DebugSensorContact item = new DebugSensorContact
				{
					_outOfAltitudeBrackets = _debugContactData._outOfAltitudeBrackets,
					_noLOS = _debugContactData._noLOS,
					_currentHorizonNM = _debugContactData._currentHorizonNM,
					_beyoundHorizon = _debugContactData._beyoundHorizon,
					_notInFOV = _debugContactData._notInFOV,
					_inLookDown = _debugContactData._inLookDown,
					_beyondLookdownRange = _debugContactData._beyondLookdownRange,
					_contactObject = listOfAllObject,
					_distanceNM = magnitude * 0.036285132f,
					_signalValue = -1f,
					_rcs = -1f,
					_maxDetectionRangeNM = -1f
				};
				if (sensorEcho != null)
				{
					item._signalValue = sensorEcho._rcs;
					item._rcs = sensorEcho._rcs;
					item._bearing = sensorEcho._toTargetAngleRad * 57.29578f;
					item._baseMaxDetectionRangeNM = sensorEcho._maxDetectionRange * 0.036285132f;
					item._maxDetectionRangeNM = sensorEcho._minBurnThroughRange * 0.036285132f;
				}
				else
				{
					item._bearing = Utils.WrapAngle(0f - Utils.calculateYawToTarget(_parentRadarSystem._baseObject.transform.forward, _parentRadarSystem.SensorPosition, listOfAllObject.transform.position));
				}
				DM_Sensors._debugRadarData.Add(item);
				if (item._outOfAltitudeBrackets || item._noLOS || item._beyoundHorizon || item._notInFOV || item._beyondLookdownRange || magnitude * 0.036285132f > item._maxDetectionRangeNM || magnitude < _rp._minRange || magnitude > _rp._maxRange)
				{
					continue;
				}
			}
			if (_sweepGating)
			{
				int uniqueID = listOfAllObject.UniqueID;
				_paintedThisSweep.Add(uniqueID);
				if (!_paintedLastSweep.Contains(uniqueID))
				{
					continue;
				}
			}
			_rawSensorEchoes.Add(sensorEcho);
		}
		if (_homeObject.HasESM())
		{
			return;
		}
		foreach (SensorSystemECM item3 in _homeObject._ECMSystemsTryingToJamMe)
		{
			bool flag2 = true;
			float item2 = _homeObject._geoPosition.HeadingRangeMeters(item3._baseObject._geoPosition).heading;
			float num = 30f;
			foreach (float jamBearing in _homeObject._jamBearings)
			{
				if (MathF.Abs(jamBearing - item2) < num)
				{
					flag2 = false;
					break;
				}
			}
			if (flag2)
			{
				_homeObject._jamBearings.Add(item2);
			}
		}
	}

	private void createRadarEchoClusters()
	{
		for (int i = 0; i < _rawSensorEchoes.Count; i++)
		{
			SensorEcho sensorEcho = _rawSensorEchoes[i];
			List<SensorEcho> list = null;
			foreach (List<SensorEcho> intersectedEcho in _intersectedEchoes)
			{
				if (intersectedEcho.Contains(sensorEcho))
				{
					list = intersectedEcho;
					break;
				}
			}
			if (list == null)
			{
				list = new List<SensorEcho>();
				list.Add(sensorEcho);
				_intersectedEchoes.Add(list);
			}
			for (int j = i + 1; j < _rawSensorEchoes.Count; j++)
			{
				if (_rawSensorEchoes[j]._objectBase is LaunchTransient)
				{
					continue;
				}
				SensorEcho sensorEcho2 = _rawSensorEchoes[j];
				Vector3 vector = sensorEcho2._unityPosition - sensorEcho._unityPosition;
				float num = Utils.atan2ToMinusPiToPlusPi(Mathf.Atan2(vector.z, vector.x));
				float f = sensorEcho._toTargetAngleRad - num;
				float num2 = Mathf.Abs(Mathf.Cos(f));
				float num3 = vector.magnitude * 67.200066f;
				if (num3 * num2 < _rp._rangeResolution)
				{
					float num4 = 90f - _rp._beamWidth;
					float num5 = sensorEcho._distanceToSensorInM * Mathf.Abs(Mathf.Cos(MathF.PI / 180f * num4));
					float num6 = Mathf.Abs(Mathf.Sin(f));
					if (num3 * num6 < num5)
					{
						list.Add(sensorEcho2);
					}
				}
			}
		}
		_parentRadarSystem._detectedObjects.Clear();
		foreach (List<SensorEcho> intersectedEcho2 in _intersectedEchoes)
		{
			Vector3 position = intersectedEcho2[0]._objectBase.transform.position;
			float num7 = 0.0001f;
			float bearingError = 0.0001f;
			if (_parentRadarSystem.CurrentDamageType > DamageValues.DamageType.No)
			{
				if (_parentRadarSystem.CurrentDamageType == DamageValues.DamageType.Light)
				{
					num7 = 5f;
					bearingError = 5f;
				}
				else if (_parentRadarSystem.CurrentDamageType == DamageValues.DamageType.Medium)
				{
					num7 = 10f;
					bearingError = 10f;
				}
				else if (_parentRadarSystem.CurrentDamageType == DamageValues.DamageType.Severe)
				{
					num7 = 20f;
					bearingError = 15f;
				}
				else if (_parentRadarSystem.CurrentDamageType == DamageValues.DamageType.Destroyed)
				{
					num7 = 115f;
					bearingError = 20f;
				}
				num7 *= 1851.9999f;
			}
			Entity entity = CreateRadarTrack(SensorSystem.SensorType.SearchRadar, intersectedEcho2[0]._objectBase, position, num7, bearingError);
			if (SensorUtils.CheckForLikelyHostile(intersectedEcho2[0]._objectBase, _parentRadarSystem))
			{
				Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager.AddComponentData(entity, new DetectedSide(intersectedEcho2[0]._objectBase._taskforce.Entity));
			}
			Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager.AddComponentData(entity, new IncomingSignalData
			{
				_sensorType = SensorSystem.SensorType.SearchRadar,
				_contactType = "RadarContact",
				_incomingSignalValue = intersectedEcho2[0]._signalValue
			});
			_subscribersImpl.OnNext(entity);
		}
	}
}
