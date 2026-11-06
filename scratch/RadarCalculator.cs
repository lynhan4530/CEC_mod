using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SeaPower;

public static class RadarCalculator
{
	public static float GetCommPosition(ObjectBase obj)
	{
		if (obj is LandUnit)
		{
			return obj.transform.position.y + 0.44642812f;
		}
		if (obj is Vessel)
		{
			return obj.transform.position.y + 0.29761875f;
		}
		if (obj is Submarine)
		{
			return obj.transform.position.y + 0.14880937f;
		}
		return obj.transform.position.y;
	}

	public static float getRadarHorizon(float radarHeight, float targetHeight, bool visual = false, float terrainHeight = 0f)
	{
		radarHeight -= terrainHeight;
		targetHeight -= terrainHeight;
		radarHeight *= 0.067200065f;
		targetHeight *= 0.067200065f;
		if (targetHeight < 0f)
		{
			targetHeight = 0f;
		}
		if (radarHeight < 0f)
		{
			radarHeight = 0f;
		}
		float num = (visual ? (Mathf.Sqrt(12742.018f * radarHeight) + Mathf.Sqrt(12742.018f * targetHeight)) : (Mathf.Sqrt(16989.357f * radarHeight) + Mathf.Sqrt(16989.357f * targetHeight)));
		return num * 14.880938f;
	}

	public static bool createRadarEcho(Vector3 sourcePosition, ObjectBase potentialContact, float waveLength, float gainFactor, float peakPower, List<SensorSystemECM> attackingJammers, Globals.Frequency frequency, Crew crew, out SensorEcho sensorEcho, bool ignoreAspect = false)
	{
		if (potentialContact == null)
		{
			sensorEcho = null;
			return false;
		}
		SensorData sensorData = potentialContact.getSensorData();
		if (sensorData == null)
		{
			Log.Error($"No sensor data for object {potentialContact.name} type: {potentialContact.GetType()}", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Systems\\Sensors\\RadarCalculator.cs", 61, "createRadarEcho");
			sensorEcho = null;
			return false;
		}
		for (int num = attackingJammers.Count - 1; num >= 0; num--)
		{
			if (attackingJammers[num] == null || attackingJammers[num].Inoperable.Value || !attackingJammers[num].IsOn.Value || attackingJammers[num]._mountObject == null)
			{
				attackingJammers.Remove(attackingJammers[num]);
			}
		}
		Vector3 ownTrajectory = potentialContact.transform.position - sourcePosition;
		float minReflection = (ignoreAspect ? sensorData._maxRCS : sensorData._minRCS);
		sensorEcho = SensorUtils.calculateTargetReflectionValue(sourcePosition, ownTrajectory, potentialContact, minReflection, sensorData._maxRCS);
		float num2 = (float)Math.Pow((double)(peakPower * 1000f * Mathf.Pow(gainFactor, 2f) * Mathf.Pow(waveLength, 2f) * sensorEcho._rcs) / (Globals.RcsValues._minDetectableReturnPowerRadar * 1000.0 * (double)Mathf.Pow(MathF.PI * 4f, 3f)), 0.25);
		sensorEcho._maxDetectionRange = num2 * 0.0148809375f;
		if (Singleton<Environment>.Instance.SeaState > 1 && (potentialContact is Vessel || (potentialContact is Submarine && !(potentialContact as Submarine).IsSubmerged.Value)))
		{
			float num3 = 0.05f;
			float num4 = 1f - num3 * (float)Singleton<Environment>.Instance.SeaState;
			num2 *= num4;
		}
		if (Globals._useCombinedJammers)
		{
			num2 = ECM.CombinedJamUnit(from x in attackingJammers
				where x.IsOn.Value && !x.Inoperable.Value
				select x._ecm, sensorEcho._rcs, sourcePosition, frequency, peakPower, gainFactor, num2, crew);
		}
		else
		{
			foreach (SensorSystemECM attackingJammer in attackingJammers)
			{
				if (attackingJammer != null && attackingJammer.IsOn.Value && !attackingJammer.Inoperable.Value)
				{
					num2 = attackingJammer._ecm.JamUnit(sensorEcho._rcs, sourcePosition, frequency, peakPower, gainFactor, num2, crew);
				}
			}
		}
		sensorEcho._minBurnThroughRange = num2 * 0.0148809375f;
		if (sensorEcho._distanceToSensorInM > num2)
		{
			return false;
		}
		return true;
	}

	public static float GetFCRDetectionRange(Vector3 sourcePosition, float rcs, float waveLength, float gainFactor, float peakPower, List<SensorSystemECM> attackingJammers, Globals.Frequency frequency, Crew crew)
	{
		for (int num = attackingJammers.Count - 1; num >= 0; num--)
		{
			if (attackingJammers[num] == null || attackingJammers[num].Inoperable.Value || !attackingJammers[num].IsOn.Value || attackingJammers[num]._mountObject == null)
			{
				attackingJammers.Remove(attackingJammers[num]);
			}
		}
		float num2 = (float)Math.Pow((double)(peakPower * 1000f * Mathf.Pow(gainFactor, 2f) * Mathf.Pow(waveLength, 2f) * rcs) / (Globals.RcsValues._minDetectableReturnPowerRadar * 1000.0 * (double)Mathf.Pow(MathF.PI * 4f, 3f)), 0.25);
		if (Globals._useCombinedJammers)
		{
			num2 = ECM.CombinedJamUnit(from x in attackingJammers
				where x.IsOn.Value && !x.Inoperable.Value
				select x._ecm, rcs, sourcePosition, frequency, peakPower, gainFactor, num2, crew);
		}
		else
		{
			foreach (SensorSystemECM attackingJammer in attackingJammers)
			{
				if (attackingJammer != null && attackingJammer.IsOn.Value && !attackingJammer.Inoperable.Value)
				{
					num2 = attackingJammer._ecm.JamUnit(rcs, sourcePosition, frequency, peakPower, gainFactor, num2, crew);
				}
			}
		}
		return num2 * 0.0148809375f;
	}

	public static double getSignalStrength(ObjectBase targetObject, ObjectBase ownObj, Vector3 ownPosition, float gainFactor, List<Globals.Frequency> frequencies)
	{
		List<SensorSystem> detectedEmittingSensorSystems;
		return getSignalStrength(targetObject, ownObj, ownPosition, gainFactor, frequencies, out detectedEmittingSensorSystems);
	}

	public static double getSignalStrengthFromWeapon(ObjectBase targetObject, ObjectBase ownObj, Vector3 ownPosition, float gainFactor, List<Globals.Frequency> frequencies, out WeaponBase detectedEmittingWeapon, float overHorizonMultiplier = 1f)
	{
		detectedEmittingWeapon = null;
		double num = 0.0;
		if (targetObject.isWeapon())
		{
			WeaponBase weaponBase = targetObject as WeaponBase;
			AmmunitionParameters ap = weaponBase._ap;
			if (ap == null)
			{
				return 0.0;
			}
			if (ap.GuidanceTypeForTarget(targetObject) != AmmunitionParameters.GuidanceType.ActiveRadarHoming || !weaponBase._seeker.isActive())
			{
				return 0.0;
			}
			if (!frequencies.Contains(ap._frequency))
			{
				return 0.0;
			}
			Vector3 direction = ownObj.transform.position - targetObject.transform.position;
			float magnitude = direction.magnitude;
			float num2 = overHorizonMultiplier * getRadarHorizon(targetObject.transform.position.y, ownPosition.y, visual: false, Utils.TerrainAltForHorizon(targetObject, ownObj));
			if (magnitude > num2)
			{
				return 0.0;
			}
			Vector3 direction2 = targetObject.transform.InverseTransformDirection(direction);
			direction2.y = 0f;
			if (Quaternion.Angle(Quaternion.LookRotation(targetObject.transform.TransformDirection(direction2), targetObject.transform.up), targetObject.transform.rotation) > 5f)
			{
				return 0.0;
			}
			float num3 = magnitude * 67.200066f;
			num = ap._radarSeekerPeakPower * 1000f * ap._gainFactor * gainFactor * Mathf.Pow(ap._waveLength, 2f) / Mathf.Pow(MathF.PI * 4f * num3, 2f) / 1000f;
			if (num > 0.0)
			{
				detectedEmittingWeapon = weaponBase;
			}
		}
		return num;
	}

	public static double getSignalStrength(ObjectBase targetObject, ObjectBase ownObj, Vector3 ownPosition, float gainFactor, List<Globals.Frequency> frequencies, out List<SensorSystem> detectedEmittingSensorSystems, float overHorizonMultiplier = 1.3f, bool homeOnRadar = true)
	{
		detectedEmittingSensorSystems = Globals._detectedEmittingSensorSystems.getFreeInstance();
		if (targetObject == null)
		{
			return 0.0;
		}
		float magnitude = (targetObject.transform.position - ownPosition).magnitude;
		double num = 0.0;
		if (targetObject.isUnit())
		{
			if (targetObject._obp == null)
			{
				return 0.0;
			}
			foreach (SensorSystem sensorSystem in targetObject._obp._sensorSystems)
			{
				if (!sensorSystem.CanProduceContacts)
				{
					continue;
				}
				if (sensorSystem is SensorSystemRadar sensorSystemRadar)
				{
					if (!homeOnRadar || (!sensorSystemRadar._radar._isActive && !sensorSystemRadar._isGuiding) || !frequencies.Contains(sensorSystemRadar._radar._rp._frequency))
					{
						continue;
					}
					float num2 = overHorizonMultiplier * getRadarHorizon(sensorSystemRadar._radar.Altitude, ownPosition.y, visual: false, Utils.TerrainAltForHorizon(targetObject, ownObj));
					if (magnitude > num2)
					{
						continue;
					}
					if (sensorSystemRadar._type == SensorSystemRadar.RadarType.DirectedSearch || sensorSystemRadar._type == SensorSystemRadar.RadarType.Targeting)
					{
						if (sensorSystemRadar._type == SensorSystemRadar.RadarType.Targeting && !sensorSystemRadar._isGuiding)
						{
							continue;
						}
						float num3 = 0f - Utils.calculateYawToTarget(sensorSystemRadar._mountObject.transform.forward, sensorSystemRadar._mountObject.transform.position, ownPosition);
						if (num3 < sensorSystemRadar._horizontalViewArc.x || num3 > sensorSystemRadar._horizontalViewArc.y)
						{
							continue;
						}
					}
					RadarParameters rp = sensorSystemRadar._radar._rp;
					float num4 = magnitude * 67.200066f;
					double num5 = rp._peakPower * 1000f * rp._gainFactor * gainFactor * Mathf.Pow(rp._waveLength, 2f) / Mathf.Pow(MathF.PI * 4f * num4, 2f) / 1000f;
					if (num5 > num)
					{
						num = num5;
					}
					if (num5 > 0.0)
					{
						detectedEmittingSensorSystems.Add(sensorSystemRadar);
					}
				}
				else
				{
					if (!(sensorSystem is SensorSystemECM sensorSystemECM) || (sensorSystemECM._ecm._ep._type != ECMParameters.ECMType.Offensive && sensorSystemECM._ecm._ep._type != ECMParameters.ECMType.Universal) || !sensorSystemECM.Jams.Value)
					{
						continue;
					}
					ECMParameters ep = sensorSystemECM._ecm._ep;
					bool flag = false;
					float f = ep._waveLengths[0];
					for (int i = 0; i < ep._frequencies.Count; i++)
					{
						if (frequencies.Contains(ep._frequencies[i]))
						{
							flag = true;
							f = ep._waveLengths[i];
							break;
						}
					}
					if (!flag)
					{
						continue;
					}
					float num6 = overHorizonMultiplier * getRadarHorizon(sensorSystemECM._mountObject.transform.position.y, ownPosition.y, visual: false, Utils.TerrainAltForHorizon(targetObject, ownObj));
					if (!(magnitude > num6))
					{
						float num7 = magnitude * 67.200066f;
						double num8 = ep._peakPower * 1000f * ep._gainFactor * gainFactor * Mathf.Pow(f, 2f) / Mathf.Pow(MathF.PI * 4f * num7, 2f) / 1000f;
						if (num8 > num)
						{
							num = num8;
						}
						if (num8 > 0.0)
						{
							detectedEmittingSensorSystems.Add(sensorSystemECM);
						}
					}
				}
			}
		}
		return num;
	}
}
