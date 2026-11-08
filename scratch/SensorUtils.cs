using System;
using Unity.Entities;
using UnityEngine;

namespace SeaPower;

public static class SensorUtils
{
	public enum VisualSizeClass
	{
		VerySmall,
		Small,
		SemiSmall,
		Meager,
		Medium,
		SemiLarge,
		Large,
		VeryLarge
	}

	public const float NIGHT_OPS_SUNLIGHT_THRESHOLD = 0.25f;

	public const float NEUTRAL_NIGHT_BRIGHTNESS = 0.6f;

	public static bool IsTooDarkForVisualOnlySensors()
	{
		if (Singleton<DynamicSky>.Instance != null)
		{
			return Singleton<DynamicSky>.Instance.getSunlightIntensity() < 0.25f;
		}
		return false;
	}

	public static bool IsBlindingConditions()
	{
		if (!IsTooDarkForVisualOnlySensors())
		{
			if (Singleton<Environment>.InstanceExists())
			{
				if (!Singleton<Environment>.Instance.IsFog)
				{
					return Singleton<Environment>.Instance.IsSnowing;
				}
				return true;
			}
			return false;
		}
		return true;
	}

	public static bool CrewCanSeeTargets(ObjectBase unit)
	{
		if (IsBlindingConditions())
		{
			return HasNightCapableSensors(unit);
		}
		return true;
	}

	public static bool IsNightOrWithinHoursOfNight(float hours)
	{
		if (!Singleton<DynamicSky>.InstanceExists() || !Singleton<Environment>.InstanceExists())
		{
			return false;
		}
		if (IsTooDarkForVisualOnlySensors())
		{
			return true;
		}
		return IsInNightWindow(Singleton<Environment>.Instance._hourMinSec, Singleton<DynamicSky>.Instance.getSunsetTime(), Singleton<DynamicSky>.Instance.getSunriseTime(), hours);
	}

	public static bool IsNightOrWithinHoursOfNight(DateTime utc, GeoPosition position, float hours)
	{
		float num = (float)utc.Hour + (float)utc.Minute / 60f + (float)utc.Second / 3600f;
		DynamicSky.ComputeCivilTwilightTimes(position._latitude, position._longitude, utc.Year, utc.Month, utc.Day, num, out var sunsetTime, out var sunriseTime);
		return IsInNightWindow(num, sunsetTime, sunriseTime, hours);
	}

	private static bool IsInNightWindow(float now, float sunset, float sunrise, float hours)
	{
		if (float.IsNaN(sunset) || float.IsNaN(sunrise))
		{
			return false;
		}
		float num = Mathf.Repeat(now - sunset, 24f);
		float num2 = Mathf.Repeat(sunrise - sunset, 24f);
		if (!(num < num2))
		{
			return 24f - num <= hours;
		}
		return true;
	}

	public static bool HasNightCapableSensors(ObjectBase unit, bool airTargets = false)
	{
		if (unit?._obp?._sensorSystems == null)
		{
			return false;
		}
		foreach (SensorSystem sensorSystem in unit._obp._sensorSystems)
		{
			if (sensorSystem == null || sensorSystem.IsDestroyed)
			{
				continue;
			}
			switch (sensorSystem._systemType)
			{
			case BaseSystem.SystemType.SensorSearchRadar:
			case BaseSystem.SystemType.SensorFCRadar:
				if (!airTargets || !(sensorSystem is SensorSystemRadar { CanSeeAirTargets: false }))
				{
					return true;
				}
				break;
			case BaseSystem.SystemType.SensorInfrared:
				return true;
			case BaseSystem.SystemType.SensorVisual:
				if (sensorSystem is SensorSystemVisual { _nightVisionLevel: >0f })
				{
					return true;
				}
				break;
			}
		}
		return false;
	}

	public static SensorEcho calculateTargetReflectionValue(Vector3 ownPosition, Vector3 ownTrajectory, ObjectBase targetObject, float minReflection, float maxReflection)
	{
		SensorEcho freeInstance = Globals._sensorEchoes.getFreeInstance();
		freeInstance._objectUID = targetObject.UniqueID;
		freeInstance._objectBase = targetObject;
		freeInstance._unityPosition = targetObject.getUnityPosition();
		freeInstance._unitySpeedVector = targetObject.UnityVelocityVector;
		freeInstance._directionToTarget = freeInstance._unityPosition - ownPosition;
		freeInstance._distanceToSensorInUnity = freeInstance._directionToTarget.magnitude;
		freeInstance._distanceToSensorInM = freeInstance._distanceToSensorInUnity * 67.200066f;
		freeInstance._toTargetAngleRad = Utils.atan2ToMinusPiToPlusPi(Mathf.Atan2(freeInstance._directionToTarget.z, freeInstance._directionToTarget.x));
		float num = Vector3.Angle(ownTrajectory, targetObject.getTransform().forward);
		float t = 1f - Mathf.Abs(num - 90f) / 90f;
		if (targetObject._type != ObjectBase.ObjectType.Chaff && targetObject._type != ObjectBase.ObjectType.Noisemaker)
		{
			freeInstance._rcs = Mathf.Lerp(minReflection, maxReflection, t);
		}
		else
		{
			freeInstance._rcs = maxReflection;
		}
		if (targetObject is Submarine submarine && submarine.isSubmerged())
		{
			int raisedMastsCount = (targetObject as Submarine).getRaisedMastsCount();
			if (raisedMastsCount > 0)
			{
				float num2 = Singleton<Environment>.Instance.SeaState;
				if (num2 < float.Epsilon)
				{
					num2 = 0f;
				}
				float rcs = ((!(num2 > 0f)) ? (0.01f * (float)raisedMastsCount) : (0.01f * (float)raisedMastsCount / Mathf.Pow(10f, num2 / 10f)));
				freeInstance._rcs = rcs;
			}
		}
		freeInstance._signalValue = freeInstance._rcs;
		return freeInstance;
	}

	public static SensorEcho CalculateTargetIRValue(ObjectBase obj, Vector3 ownPosition)
	{
		SensorEcho freeInstance = Globals._sensorEchoes.getFreeInstance();
		freeInstance._objectUID = obj.UniqueID;
		freeInstance._objectBase = obj;
		freeInstance._unityPosition = obj.getUnityPosition();
		freeInstance._unitySpeedVector = obj.UnityVelocityVector;
		float num = obj.getSensorData()._irSignatureValue * CalculateIRScalingFactor(obj, ownPosition);
		float num2 = (obj.getUnityPosition() - ownPosition).magnitude * 0.036285132f;
		float irEmission = num / Mathf.Pow(num2 / 1000f * 0.5399568f, 2f);
		freeInstance._irEmission = irEmission;
		freeInstance._rcs = 1f;
		return freeInstance;
	}

	public static float CalculateIRScalingFactor(ObjectBase obj, Vector3 ownPosition)
	{
		float result = 1f;
		if (obj.isAirTarget())
		{
			result = Mathf.Pow(obj.UnityVelocityVector.magnitude, 2.5f) / 140f * ((120000f - obj.transform.position.y * 220.47266f) / 120000f);
			if (obj._type == ObjectBase.ObjectType.Aircraft)
			{
				float num = 3f;
				Aircraft aircraft = (Aircraft)obj;
				num *= (float)aircraft.Ap._engineCount / 80000f;
				switch (aircraft._telegraph)
				{
				case 0:
					num *= 0.5f * aircraft.Ap._maxDryThrust;
					break;
				case 1:
					num *= 0.8f * aircraft.Ap._maxDryThrust;
					break;
				case 2:
					num *= 0.9f * aircraft.Ap._maxDryThrust;
					break;
				case 3:
					num *= aircraft.Ap._maxDryThrust;
					break;
				case 4:
					num *= 1.5f * aircraft.Ap._maxWetThrust;
					break;
				}
				float num2 = Utils.GetShotAspect(ownPosition, obj);
				if (num2 > 180f)
				{
					num2 = 360f - num2;
				}
				float num3;
				if (num2 >= 160f)
				{
					num3 = 1f;
				}
				else
				{
					float f = num2 * (MathF.PI / 180f);
					float num4 = 0.28f * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(f)), 1.5f);
					float num5 = Mathf.Pow(num2 / 180f, 3f);
					num3 = 0.04f + num4 + num5;
				}
				result += num * num3;
			}
			else
			{
				result += 1f;
			}
		}
		return result;
	}

	public static float CalculateVisualRange(ObjectBase baseObject, ObjectBase targetObject, float nightVisionLevel)
	{
		float rcs;
		return CalculateVisualRange(baseObject, targetObject, nightVisionLevel, out rcs);
	}

	public static VisualSizeClass VisualSizeFor(float detectionRange)
	{
		detectionRange *= 0.036285132f;
		if (detectionRange < 5f)
		{
			return VisualSizeClass.VerySmall;
		}
		if (detectionRange < 6f)
		{
			return VisualSizeClass.Small;
		}
		if ((double)detectionRange < 7.5)
		{
			return VisualSizeClass.SemiSmall;
		}
		if (detectionRange < 9f)
		{
			return VisualSizeClass.Meager;
		}
		if (detectionRange < 11f)
		{
			return VisualSizeClass.Medium;
		}
		if (detectionRange < 12f)
		{
			return VisualSizeClass.SemiLarge;
		}
		if (detectionRange < 15f)
		{
			return VisualSizeClass.Large;
		}
		return VisualSizeClass.VeryLarge;
	}

	public static float CalculateMaxVisualRange(VisualSizeClass size, float nightVisionLevel, bool useIrLevels = false)
	{
		float num = size switch
		{
			VisualSizeClass.VerySmall => 82.67849f, 
			VisualSizeClass.Small => 137.79749f, 
			VisualSizeClass.SemiSmall => 165.35698f, 
			VisualSizeClass.Meager => 206.69621f, 
			VisualSizeClass.Medium => 165.35698f, 
			VisualSizeClass.SemiLarge => 303.15445f, 
			VisualSizeClass.Large => 330.71396f, 
			_ => 413.39243f, 
		};
		if (Singleton<Environment>.Instance.IsRaining)
		{
			num *= 0.75f;
		}
		if (Singleton<Environment>.Instance.IsSnowing)
		{
			num *= 0.5f;
		}
		if (Singleton<Environment>.Instance.IsFog)
		{
			num *= 0.33f;
		}
		float sunlightIntensity = Singleton<DynamicSky>.Instance.getSunlightIntensity();
		float num2 = sunlightIntensity + Singleton<DynamicSky>.Instance.getMoonlightIntensityVisualModifier();
		if (sunlightIntensity < 0.1f)
		{
			num2 += nightVisionLevel;
		}
		else if (sunlightIntensity < 0.25f)
		{
			num2 += nightVisionLevel / 2f;
		}
		else if (sunlightIntensity < 0.5f)
		{
			num2 += nightVisionLevel / 3f;
		}
		return num *= num2;
	}

	public static float CalculateVisualRange(ObjectBase baseObject, ObjectBase targetObject, float nightVisionLevel, out float rcs, bool useIrLevels = false, bool ignoreClouds = false, bool frontAspect = false)
	{
		rcs = 0f;
		if (targetObject._type == ObjectBase.ObjectType.Submarine && targetObject.isSubmerged() && (targetObject as Submarine).getRaisedMastsCount() > 0)
		{
			SensorEcho echo = Globals._sensorEchoes.getFreeInstance();
			float visualRangeForRaisedMasts = getVisualRangeForRaisedMasts(baseObject, targetObject, nightVisionLevel, out echo);
			rcs = echo._rcs;
			return visualRangeForRaisedMasts;
		}
		Vector3 unityPosition = baseObject.getUnityPosition();
		Vector3 unityPosition2 = targetObject.getUnityPosition();
		float magnitude = (unityPosition2 - unityPosition).magnitude;
		if (targetObject.isSubmerged() && targetObject.transform.position.y < -0.453571f)
		{
			return -1f;
		}
		if (targetObject.isSubmerged())
		{
			if (!baseObject.IsAirUnit)
			{
				return -1f;
			}
			if (180f / MathF.PI * Mathf.Asin(Mathf.Abs(unityPosition.y - unityPosition2.y) / magnitude) < 20f)
			{
				return -1f;
			}
		}
		return CalculateVisualRangeFromPosition(unityPosition, targetObject, nightVisionLevel, out rcs, useIrLevels, ignoreClouds, frontAspect);
	}

	public static float CalculateVisualRange(Vector3 ownPosition, ObjectBase targetObject, float nightVisionLevel)
	{
		float rcs;
		return CalculateVisualRangeFromPosition(ownPosition, targetObject, nightVisionLevel, out rcs);
	}

	private static float CalculateVisualRangeFromPosition(Vector3 ownPosition, ObjectBase targetObject, float nightVisionLevel, out float rcs, bool useIrLevels = false, bool ignoreClouds = false, bool frontAspect = false)
	{
		rcs = 0f;
		Vector3 unityPosition = targetObject.getUnityPosition();
		Vector3 ownTrajectory = unityPosition - ownPosition;
		float num = 248.03546f;
		if (targetObject.isWeapon())
		{
			float maxRCS = targetObject._sensorData._maxRCS;
			num = (((double)maxRCS < 0.2) ? 82.67849f : ((maxRCS < 2f) ? 137.79749f : ((maxRCS < 11f) ? 192.91647f : ((maxRCS < 101f) ? 248.03546f : ((maxRCS < 1001f) ? 303.15445f : ((maxRCS < 10001f) ? 358.27344f : ((!(maxRCS < 100001f)) ? 248.03546f : 413.39243f)))))));
		}
		float num2 = 0.5f;
		if (targetObject._sensorData != null)
		{
			num = targetObject._sensorData.VisualIdentificationRange;
			num2 = targetObject._sensorData._visibilityFromFront;
		}
		if (targetObject is LaunchTransient)
		{
			num2 = 1f;
		}
		if (num < 3f)
		{
			num = 3f;
		}
		if (targetObject.isSubmerged())
		{
			num /= 100f;
		}
		float num3 = 1f;
		num *= num3;
		num = ModifyVisualIdentificationRangeByEnvironmentalConditions(targetObject, nightVisionLevel, ownPosition, unityPosition, num, submarineMastsDetection: false, useIrLevels, ignoreClouds);
		SensorEcho sensorEcho = ((!useIrLevels) ? calculateTargetReflectionValue(ownPosition, ownTrajectory, targetObject, num2, 1f) : CalculateTargetIRValue(targetObject, ownPosition));
		rcs = sensorEcho._rcs;
		if (frontAspect && !useIrLevels && !targetObject.isSubmerged())
		{
			rcs = num2;
		}
		return num * rcs;
	}

	public static float ModifyVisualIdentificationRangeByEnvironmentalConditions(ObjectBase targetObject, float nightVisionLevel, Vector3 ownPosition, Vector3 targetPosition, float visualIdentificationRange, bool submarineMastsDetection = false, bool useIrLevels = false, bool ignoreClouds = false)
	{
		float num = 1f;
		float num2 = 15.874985f;
		float num3 = 17.235699f;
		if (!ignoreClouds && Singleton<Environment>.Instance.CloudsCoverage != "clear")
		{
			if (Singleton<Environment>.Instance.CloudsCoverage == "clear")
			{
				num = 1f;
			}
			else if (Singleton<Environment>.Instance.CloudsCoverage.Contains("scattered"))
			{
				num3 = 43.089245f;
				num = 0.5f;
			}
			else if (Singleton<Environment>.Instance.CloudsCoverage.Contains("broken"))
			{
				num3 = 65.76779f;
				num = 0.25f;
			}
			else if (Singleton<Environment>.Instance.CloudsCoverage.Contains("overcast"))
			{
				CloudParameters cloudParameters = Singleton<CloudsManager>.Instance.getCloudParameters();
				num2 = cloudParameters._cloudLayerBlendStartHeight;
				num3 = cloudParameters._cloudLayerBlendEndHeight;
				num = 0.001f;
			}
			if (targetObject != null && targetObject.isSubmerged() && !submarineMastsDetection)
			{
				visualIdentificationRange *= num;
			}
			if ((ownPosition.y > num2 && ownPosition.y < num3) || (targetPosition.y > num2 && targetPosition.y < num3))
			{
				visualIdentificationRange *= num;
			}
			else if ((ownPosition.y > num3 && targetPosition.y < num2) || (ownPosition.y < num2 && targetPosition.y > num3))
			{
				visualIdentificationRange *= num;
			}
		}
		if (ownPosition.y < num2 || targetPosition.y < num2)
		{
			if (Singleton<Environment>.Instance.IsRaining)
			{
				visualIdentificationRange *= 0.75f;
			}
			if (Singleton<Environment>.Instance.IsSnowing)
			{
				visualIdentificationRange *= 0.5f;
			}
			if (Singleton<Environment>.Instance.IsFog)
			{
				visualIdentificationRange *= 0.33f;
			}
		}
		if (targetObject != null && targetObject.isSubmerged())
		{
			float num4;
			switch (Singleton<Environment>.Instance.SeaState)
			{
			default:
				num4 = 1f;
				break;
			case 2:
				num4 = 0.9f;
				break;
			case 3:
				num4 = 0.75f;
				break;
			case 4:
				num4 = 0.5f;
				break;
			case 5:
				num4 = 0.25f;
				break;
			case 6:
			case 7:
			case 8:
			case 9:
			case 10:
			case 11:
			case 12:
				num4 = 0f;
				break;
			}
			visualIdentificationRange *= num4;
		}
		if (!useIrLevels)
		{
			float sunlightIntensity = Singleton<DynamicSky>.Instance.getSunlightIntensity();
			float num5 = sunlightIntensity;
			if (num5 < 0.025f)
			{
				num5 = 0.09f;
			}
			float moonlightIntensityVisualModifier = Singleton<DynamicSky>.Instance.getMoonlightIntensityVisualModifier();
			float num6 = num5 + moonlightIntensityVisualModifier;
			if (sunlightIntensity < 0.1f)
			{
				num6 += nightVisionLevel;
			}
			else if (sunlightIntensity < 0.25f)
			{
				num6 += nightVisionLevel / 2f;
			}
			else if (sunlightIntensity < 0.5f)
			{
				num6 += nightVisionLevel / 3f;
			}
			bool flag = targetObject != null && targetObject._taskforce == Globals._neutralTaskforce;
			if (sunlightIntensity < 0.37f && !flag && targetObject != null && targetObject is Vessel && !targetObject.Emcon.Value)
			{
				visualIdentificationRange *= 1.5f;
			}
			if (flag)
			{
				num6 = Mathf.Max(num6, 0.6f);
			}
			if (num6 > 1f)
			{
				num6 = 1f;
			}
			visualIdentificationRange *= num6;
		}
		return visualIdentificationRange;
	}

	private static float getVisualRangeForRaisedMasts(ObjectBase baseObject, ObjectBase targetObject, float nightVisionLevel, out SensorEcho echo)
	{
		Vector3 unityPosition = baseObject.getUnityPosition();
		Vector3 unityPosition2 = targetObject.getUnityPosition();
		Vector3 ownTrajectory = unityPosition2 - unityPosition;
		float visualIdentificationRange = 688.9874f;
		visualIdentificationRange = ModifyVisualIdentificationRangeByEnvironmentalConditions(targetObject, nightVisionLevel, unityPosition, unityPosition2, visualIdentificationRange, submarineMastsDetection: true);
		echo = calculateTargetReflectionValue(unityPosition, ownTrajectory, targetObject, 1f, 1f);
		return visualIdentificationRange * echo._rcs;
	}

	public static void CheckVisualIdentification(Entity track, ObjectBase targetObject, float distance, float thruthDistance = -1f)
	{
		Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
		if (distance > thruthDistance && targetObject._obp != null && targetObject._obp._disguisedAs != "" && targetObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Spy))
		{
			int? num = Singleton<UnitMetadataProvider>.Instance.IdForUnit(targetObject._obp._disguisedAs, targetObject._obp._objectIniFolder);
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				ClassInformation sig = new ClassInformation
				{
					_preventTaskforceAnalysis = true
				};
				sig.SetBit(valueOrDefault);
				defaultGameObjectInjectionWorld.EntityManager.AddComponentData(track, sig);
			}
			defaultGameObjectInjectionWorld.EntityManager.AddComponentData(track, new DetectedSide(Globals._neutralTaskforce.Entity));
			return;
		}
		if (targetObject.isWeapon())
		{
			defaultGameObjectInjectionWorld.EntityManager.AddComponentData(track, default(ClassInformation));
		}
		else
		{
			int? num2 = Singleton<UnitMetadataProvider>.Instance.IdForUnit(targetObject);
			if (num2.HasValue)
			{
				int valueOrDefault2 = num2.GetValueOrDefault();
				ClassInformation sig2 = default(ClassInformation);
				sig2.SetBit(valueOrDefault2);
				defaultGameObjectInjectionWorld.EntityManager.AddComponentData(track, sig2);
			}
		}
		defaultGameObjectInjectionWorld.EntityManager.AddComponentData(track, new DetectedSide(targetObject._taskforce.Entity));
	}

	public static void CheckClassificationOrIdentification(Entity track, ObjectBase baseObject, ObjectBase targetObject, bool wasIdentified, float distance = -1f, float thruthDistance = 0f, bool canRevealDisguise = true)
	{
		Unity.Entities.World defaultGameObjectInjectionWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
		if (targetObject._obp != null && targetObject._obp._disguisedAs != "" && targetObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Spy) && !baseObject._taskforce._identifiedSpyUnits.Contains(targetObject) && (!canRevealDisguise || distance > thruthDistance))
		{
			if (wasIdentified)
			{
				int? num = Singleton<UnitMetadataProvider>.Instance.IdForUnit(targetObject._obp._disguisedAs, targetObject._obp._objectIniFolder);
				if (num.HasValue)
				{
					int valueOrDefault = num.GetValueOrDefault();
					ClassInformation sig = new ClassInformation
					{
						_preventTaskforceAnalysis = true
					};
					sig.SetBit(valueOrDefault);
					defaultGameObjectInjectionWorld.EntityManager.AddComponentData(track, sig);
				}
			}
			defaultGameObjectInjectionWorld.EntityManager.AddComponentData(track, new DetectedSide(Globals._neutralTaskforce.Entity));
			return;
		}
		if (canRevealDisguise && !baseObject._taskforce._identifiedSpyUnits.Contains(targetObject))
		{
			baseObject._taskforce._identifiedSpyUnits.Add(targetObject);
		}
		if (wasIdentified)
		{
			int? num2 = Singleton<UnitMetadataProvider>.Instance.IdForUnit(targetObject);
			if (num2.HasValue)
			{
				int valueOrDefault2 = num2.GetValueOrDefault();
				defaultGameObjectInjectionWorld.EntityManager.AddComponentData(track, ClassInformation.ForceID(valueOrDefault2));
			}
			else
			{
				defaultGameObjectInjectionWorld.EntityManager.AddComponentData(track, new ClassInformation
				{
					overrideIdentification = true
				});
			}
		}
		defaultGameObjectInjectionWorld.EntityManager.AddComponentData(track, new DetectedSide(targetObject._taskforce.Entity));
	}

	public static bool CheckForLikelyHostile(ObjectBase targetObject, SensorSystem sys)
	{
		ObjectBase baseObject = sys._baseObject;
		if (baseObject.IsPlayerObject)
		{
			return false;
		}
		if (!baseObject.isUnit())
		{
			return false;
		}
		if (baseObject._taskforce.RelationshipTo(targetObject._taskforce) != RelationsState.Hostile)
		{
			return false;
		}
		Vehicle mapVehicle = targetObject.GetMapVehicle(baseObject._taskforce);
		if (mapVehicle == null)
		{
			return false;
		}
		if (baseObject._taskforce.RelationshipTo(mapVehicle.UnitTaskforce.Value) == RelationsState.Hostile)
		{
			return true;
		}
		if (targetObject.IsAirUnit && mapVehicle.Position.Value.getDistanceInMiles(baseObject.Position.Value) < 250.0)
		{
			float num = Vector3.Angle(baseObject.transform.position - targetObject.transform.position, targetObject.transform.forward);
			bool flag = targetObject.transform.position.y > 136.0713f && targetObject.transform.position.y < 181.4284f;
			if (num < 60f)
			{
				if (flag)
				{
					if (targetObject._velocityInKnots > 500f)
					{
						return true;
					}
				}
				else if (targetObject._velocityInKnots > 300f)
				{
					return true;
				}
			}
		}
		int num2 = 0;
		foreach (Vehicle vehicle in baseObject.PlottingTable.Vehicles)
		{
			if (targetObject == vehicle.BaseObject || targetObject._type != vehicle.BaseObject._type || !targetObject.isUnit() || baseObject._taskforce.RelationshipTo(vehicle.BaseObject._taskforce) != RelationsState.Hostile || Mathf.Abs(targetObject.Heading.Value - vehicle.BaseObject.Heading.Value) > 10f)
			{
				continue;
			}
			double distanceInMiles = mapVehicle.Position.Value.getDistanceInMiles(vehicle.Position.Value);
			if (distanceInMiles < 0.0010000000474974513)
			{
				continue;
			}
			if (targetObject is LandUnit landUnit)
			{
				if (!landUnit._isMobile || distanceInMiles > 5.0)
				{
					continue;
				}
			}
			else if (targetObject.IsAirUnit)
			{
				if (distanceInMiles > 10.0)
				{
					continue;
				}
			}
			else if (targetObject.Formation != null && vehicle.Object.Formation != null && targetObject.Formation == vehicle.Object.Formation)
			{
				if (distanceInMiles > 60.0)
				{
					continue;
				}
			}
			else if (distanceInMiles > 20.0)
			{
				continue;
			}
			if (baseObject._taskforce.RelationshipTo(vehicle.UnitTaskforce.Value) == RelationsState.Hostile)
			{
				num2++;
			}
			num2++;
		}
		if (num2 > 2)
		{
			return true;
		}
		if (targetObject.IsAirUnit && num2 > 0)
		{
			return true;
		}
		return false;
	}

	public static void BuildSolution(ref float _solution, ref float _prevTargetHeading, ref float _prevTargetVelocityInKnots, ref bool _firstCheckDone, ObjectBase _targetObject, GameObject _mountObject, float multiplier = 1f)
	{
		if (_targetObject == null || _mountObject == null)
		{
			return;
		}
		float magnitude = (_targetObject.transform.position - _mountObject.transform.position).magnitude;
		float num = Utils.WrapAngle(_targetObject.transform.eulerAngles.y);
		float a = magnitude * 67.200066f;
		float num2 = 0.075f / (1f + Mathf.Pow(Mathf.Max(a, 0f) / 6000f, 1.5f)) * multiplier * Globals._solutionCheckInterval;
		_solution += num2;
		if (_solution > 1f)
		{
			_solution = 1f;
		}
		if (!_firstCheckDone)
		{
			_firstCheckDone = true;
		}
		else
		{
			float num3 = 0.1f / Mathf.Max(multiplier, 1f);
			if (Mathf.Abs(Mathf.DeltaAngle(_prevTargetHeading, num)) > 4f)
			{
				_solution -= num3;
			}
			if (Mathf.Abs(_prevTargetVelocityInKnots - _targetObject._velocityInKnots) > 4f)
			{
				_solution -= num3;
			}
		}
		_prevTargetHeading = num;
		_prevTargetVelocityInKnots = _targetObject._velocityInKnots;
		if (_solution < 0f)
		{
			_solution = 0f;
		}
		else if (_solution > 1f)
		{
			_solution = 1f;
		}
	}

	public static void ReadAquisitionTimeValues()
	{
		Globals._aquisitionTimeWordTonumber.Clear();
		IniHandler iniHandler = Utils.openVirtualIniFile("systems", "sensors", "sensors");
		foreach (string item in iniHandler.GetAllKeysOfSection("TargetAquisitionTime"))
		{
			float result = 30f;
			float.TryParse(iniHandler.readValue("TargetAquisitionTime", item, ""), out result);
			if (!Globals._aquisitionTimeWordTonumber.ContainsKey(item))
			{
				Globals._aquisitionTimeWordTonumber.Add(item, result);
			}
		}
	}

	public static float ReadAquisitionTime(string value)
	{
		if (float.TryParse(value, out var result))
		{
			return result;
		}
		if (Globals._aquisitionTimeWordTonumber.ContainsKey(value))
		{
			return Globals._aquisitionTimeWordTonumber[value];
		}
		return 0f;
	}
}
