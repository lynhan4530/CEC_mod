using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SeaPower;

public class AircraftLoader : ObjectBaseLoader
{
	public static AircraftParameters loadAircraftFromInternalResources(string aircraftIniName, string squadronReference, string loadoutVariant, int aircraftNumber, GeoPosition geoCenterPosition, GeoPosition geoPosition, Taskforce taskForce, ResourceLoader.ResourceBatch resourceBatch, string nationOverride, bool loadAsync = false)
	{
		Log.Debug("Loading aircraft '" + aircraftIniName + "' from internal resources", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 37, "loadAircraftFromInternalResources");
		AircraftParameters ap = new AircraftParameters();
		ap._objectVariantReference = squadronReference;
		ap._loadoutVariant = loadoutVariant;
		if (!ObjectBaseLoader.loadObjectFromInternalResources(ap, aircraftIniName, "aircraft", aircraftNumber, geoCenterPosition, geoPosition, nationOverride))
		{
			return ap;
		}
		IniHandler iniHandler = Utils.openIniFile("/aircraft", "/" + aircraftIniName + ".ini");
		Aircraft aircraft = ap._rootGameObject.AddComponent<Aircraft>();
		ap._baseObject = aircraft;
		aircraft._taskforce = taskForce;
		if (loadAsync)
		{
			ap._rootGameObject.SetActive(value: false);
			resourceBatch.Completed += delegate
			{
				ap._rootGameObject.SetActive(value: true);
			};
		}
		aircraft.setName(ap._objectName);
		aircraft.SetTypeName(ap._typeAbbr);
		aircraft.setNation(ap._nation);
		aircraft.setGeoPosition(geoPosition);
		aircraft.setSensorData(new SensorData("aircraft", aircraftIniName));
		aircraft.setObjectIniName(aircraftIniName);
		createAircraftRootObject(ap);
		List<ObjectBaseParameters.UnitRoles> list = iniHandler.readValue("AI", "Role", new List<ObjectBaseParameters.UnitRoles>());
		ap._coolDownTime = iniHandler.readValue("Performance", "CoolDownTime", (ap._isLarge ? 2f : 1f) * (list.Contains(ObjectBaseParameters.UnitRoles.HeavyBomber) ? 2f : 1f) * 20f) * 60f;
		ObjectBaseLoader.LoadCommonData(aircraft, ap, iniHandler);
		string sectionName = "General";
		SimpleObjectParameters simple = ap.Simple;
		ap._isVTOL = simple._isVTOL;
		if (simple._isLarge)
		{
			ap._isLarge = true;
		}
		if (simple._isVeryLarge)
		{
			ap._isVeryLarge = true;
		}
		ap._groundedPitchAngle = iniHandler.readValue(sectionName, "GroundedPitchAngle", -5f);
		ap._pilotIniName = iniHandler.readValue(sectionName, "Pilot", "");
		ap._pilotPositions = iniHandler.readValue(sectionName, "PilotPositions", ap._pilotPositions);
		ap._pilotRotations = iniHandler.readValue(sectionName, "PilotRotations", ap._pilotRotations);
		if (ap._pilotRotations.Count == 0)
		{
			ap._pilotRotations.Add(new Vector3(90f, 0f, 0f));
		}
		ap._pilotEjectChance = iniHandler.readValue(sectionName, "EjectChance", 1f);
		ap._pilotEjectDelay = iniHandler.readValue(sectionName, "EjectDelay", 0.3f);
		ap._onGroundPivot = iniHandler.readValue(sectionName, "GroundPivot", new Vector3(0f, -0.027f, 0f));
		ap._landingPivot = iniHandler.readValue(sectionName, "LandingPivot", ap._onGroundPivot);
		ap._landingPitch = iniHandler.readValue(sectionName, "LandingPitch", 0f);
		ap._bombingAccuracy = CombatSystem.ResolveBombingValue(iniHandler.readValue(sectionName, "BombingAccuracy", -1f), ap._combatSystemBombingAccuracy);
		ap._bombingWeatherPenalty = CombatSystem.ResolveBombingValue(iniHandler.readValue(sectionName, "BombingWeatherPenalty", -1f), ap._combatSystemBombingWeatherPenalty);
		ap._bombingMovingTargetPenalty = CombatSystem.ResolveBombingValue(iniHandler.readValue(sectionName, "BombingMovingTargetPenalty", -1f), ap._combatSystemMovingTargetPenalty);
		bool flag = ap._combatSystemBombingAccuracy >= 0f && ap._bombingAccuracy == ap._combatSystemBombingAccuracy;
		ap._bombAimErrorFixedWithAltitude = iniHandler.readValue(sectionName, "AimErrorFixedWithAltitude", flag && ap._combatSystemAimErrorFixedWithAltitude);
		ap._tossBombingCapable = iniHandler.readValue(sectionName, "TossBombing", ap._combatSystemTossBombing);
		ap._preferLevelBombing = iniHandler.readValue(sectionName, "PreferLevelBombing", ap._combatSystemPreferLevelBombing);
		string text = "FlightModel";
		ap._pitchGain = iniHandler.readValue(text, "PitchGain", 0.25f);
		ap._velocityGain = iniHandler.readValue(text, "VelocityGain", 0.1136f);
		ap._headingGain = iniHandler.readValue(text, "HeadingGain", 0.5f);
		ap._bankGain = iniHandler.readValue(text, "BankGain", 1.5f);
		ap._maxRollForHeading = iniHandler.readValue(text, "MaxRollForHeading", ap._isLarge ? 45f : 90f);
		float z = iniHandler.readValue(text, "MaxRollRate", ap._isLarge ? 135f : 270f);
		float x = iniHandler.readValue(text, "MaxPitchRate", ap._isLarge ? 8f : 15f);
		float y = iniHandler.readValue(text, "MaxYawRate", ap._isLarge ? 7.5f : 15f);
		ap._maxTurnRates = new Vector3(x, y, z);
		float z2 = iniHandler.readValue(text, "MaxRollAcceleration", ap._isLarge ? 270f : 540f);
		float x2 = iniHandler.readValue(text, "MaxPitchAcceleration", ap._isLarge ? 30f : 60f);
		float y2 = iniHandler.readValue(text, "MaxYawAcceleration", ap._isLarge ? 15f : 30f);
		ap._maxTurnAccelerations = new Vector3(x2, y2, z2);
		ap._thrustGain = iniHandler.readValue(text, "ThrustGain", 5f);
		ap._speedBrakeFractionGain = iniHandler.readValue(text, "SpeedBrakeFractionGain", 10f);
		ap._phaseDifference = iniHandler.readValue(text, "PhaseDifference", 90f);
		ap._lateralAmplitudeMultiplier = iniHandler.readValue(text, "LateralAmplitudeMultiplier", 2.5f);
		ap._verticalAmplitudeMultiplier = iniHandler.readValue(text, "VerticalAmplitudeMultiplier", 5f);
		ap._phaseRate = iniHandler.readValue(text, "PhaseRate", 10f);
		ap._speedDeltaPerAltitudeChange = iniHandler.readValue(text, "SpeedDeltaPerAltitudeChange", 0.0885f);
		ap._bankAngleCurve = IniHandler.CreateCurveFromIni(iniHandler, text, "BankAngleCurve");
		if (ap._bankAngleCurve == null)
		{
			ap._bankAngleCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f, 0f, 0f), new Keyframe(2.5f, 7.5f, 0f, 0f, 0f, 0f), new Keyframe(30f, ap._maxRollForHeading, 0f, 0f, 0f, 0f));
		}
		string sectionName2 = "Performance";
		ap._wingSpan = iniHandler.readValue(sectionName2, "WingSpan", 1f);
		ap._taxiInterval = 2.5f * ap._wingSpan * 0.0148809375f;
		if (ap._taxiInterval < 0.4f)
		{
			ap._taxiInterval = 0.4f;
		}
		ap._taxiInterval = iniHandler.readValue(sectionName, "TaxiInterval", ap._taxiInterval);
		ap._maxG = iniHandler.readValue(sectionName2, "MaxG", 4f);
		ap._minG = iniHandler.readValue(sectionName2, "MinG", -2f * ap._maxG / 3f);
		ap._maxClimbRate = iniHandler.readValue(sectionName2, "MaxClimbRate", 12.7f);
		ap._machLimit = simple._machLimit;
		ap._maxOutOfCombatClimbPitch = iniHandler.readValue(sectionName2, "MaxClimbPitch", 20f);
		ap._maxCombatClimbPitch = iniHandler.readValue(sectionName2, "MaxCombatClimbPitch", (ap._machLimit > 1f) ? 80f : 45f);
		ap._maxOutOfCombatDescentPitch = iniHandler.readValue(sectionName2, "MaxDescentPitch", -20f);
		ap._maxCombatDescentPitch = iniHandler.readValue(sectionName2, "MaxCombatDescentPitch", (ap._machLimit > 1f) ? (-60f) : (-30f));
		ap._inCombatThresholdAltitude = iniHandler.readValue(sectionName2, "InCombatThresholdAltitude", 10000f) * 0.00453571f;
		ap._outOfCombatThresholdAltitude = iniHandler.readValue(sectionName2, "OutOfCombatThresholdAltitude", 2500f) * 0.00453571f;
		ap._emptyMass = iniHandler.readValue(sectionName2, "EmptyMass", 8669f);
		ap._maxFuel = iniHandler.readValue(sectionName2, "MaxFuel", 0f);
		ap._maxDryThrust = iniHandler.readValue(sectionName2, "PerEngineMaxThrust", 0f);
		ap._maxPower = iniHandler.readValue(sectionName2, "PerEngineMaxPower", 0f);
		ap._maxWetThrust = iniHandler.readValue(sectionName2, "PerEngineMaxAfterburnerThrust", ap._maxDryThrust);
		ap._idleThrust = iniHandler.readValue(sectionName2, "PerEngineIdleThrust", ap._maxDryThrust * 0.1f);
		ap._engineCount = iniHandler.readValue(sectionName2, "EngineCount", 1);
		ap._engineType = (EngineType)Enum.Parse(typeof(EngineType), iniHandler.readValue(sectionName2, "EngineType", iniHandler.doesKeyExist("Engine", "PerEngineMaxPower") ? "Turboprop" : "Jet"));
		ap._engineWarmUpTime = iniHandler.readValue(sectionName2, "EngineWarmUpTime", 20f);
		ap._ceiling = simple._ceiling * 0.00453571f;
		ap._cruiseAltitudeFeet = simple._cruiseAltitudeFeet;
		Vector3 vector = iniHandler.readValue(sectionName2, "AltitudeEfficiency", new Vector3(0.7f, 0.8f, 0.7f));
		ap._maxSpeedSeaLevel = simple._maxSpeedSeaLevel;
		ap._stallSpeed = iniHandler.readValue(sectionName2, "StallSpeed", 114.65f);
		float num = ((iniHandler.readValue(sectionName2, "RangeUnits", "Nmi").ToLower() == "km") ? 1f : 1.852f);
		Vector2 vector2 = iniHandler.readValue(sectionName2, "SpeedAndRange_Cruise", Vector2.zero);
		if (vector2 == Vector2.zero)
		{
			vector2 = new Vector2(0.2f, 40f);
			Log.Error("Incorrect values in section 'SpeedAndRange_Cruise' of '" + ap._objectIniName + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 191, "loadAircraftFromInternalResources");
		}
		ap._cruiseMach = vector2.x;
		if (ap._cruiseMach > 10f)
		{
			ap._cruiseMach = ap._cruiseMach * 0.514444f / Atmosphere.SpeedOfSound(ap._cruiseAltitudeFeet * 0.3048f);
		}
		ap._cruiseRange = vector2.y;
		ap._maxRangeInKm = vector2.y * num;
		Vector2 vector3 = iniHandler.readValue(sectionName2, "SpeedAndRange_Landing", Vector2.zero);
		if (vector3 == Vector2.zero)
		{
			vector3 = new Vector2(0.2f, 40f);
			Log.Error("Incorrect values in section 'SpeedAndRange_Landing' of '" + ap._objectIniName + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 204, "loadAircraftFromInternalResources");
		}
		if (vector3.y > vector2.y)
		{
			vector3.y = vector2.y * 0.5f;
			Log.Error("Incorrect range in section 'SpeedAndRange_Landing' of '" + ap._objectIniName + "'. Not allowed to be more tha cruise range.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 209, "loadAircraftFromInternalResources");
		}
		Vector2 vector4 = iniHandler.readValue(sectionName2, "SpeedAndRange_Loiter", Vector2.zero);
		if (vector4 == Vector2.zero)
		{
			vector4 = new Vector2(0.2f, 60f);
			Log.Error("Incorrect values in section 'SpeedAndRange_Loiter' of '" + ap._objectIniName + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 216, "loadAircraftFromInternalResources");
		}
		vector4.y = vector2.y;
		Vector2 vector5 = iniHandler.readValue(sectionName2, "SpeedAndRange_Max", Vector2.zero);
		if (vector5 == Vector2.zero)
		{
			vector5 = new Vector2(0.2f, 80f);
			Log.Error("Incorrect values in section 'SpeedAndRange_Max' of '" + ap._objectIniName + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 233, "loadAircraftFromInternalResources");
		}
		if (vector5.y > vector2.y)
		{
			vector5.y = vector2.y * 0.8f;
			Log.Error("Incorrect range in section 'SpeedAndRange_Max' of '" + ap._objectIniName + "'. Not allowed to be more tha cruise range.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 238, "loadAircraftFromInternalResources");
		}
		ap._numberOfSpeedValues = 4;
		if (iniHandler.doesKeyExist(sectionName2, "SpeedAndRange_Afterburner"))
		{
			ap._hasAfterburner = true;
			ap._numberOfSpeedValues = 5;
		}
		ap._speedValuesInMach = new float[ap._numberOfSpeedValues];
		ap._speedValueNames = new string[ap._numberOfSpeedValues];
		Vector2[] array = new Vector2[ap._numberOfSpeedValues];
		array[0] = vector3;
		array[1] = vector4;
		array[2] = vector2;
		array[3] = vector5;
		ap._speedValueNames[0] = "Landing";
		ap._speedValueNames[1] = "Loiter";
		ap._speedValueNames[2] = "Cruise";
		ap._speedValueNames[3] = "Max";
		if (ap._numberOfSpeedValues > 4)
		{
			Vector2 vector6 = iniHandler.readValue(sectionName2, "SpeedAndRange_Afterburner", Vector2.zero);
			if (vector6 == Vector2.zero)
			{
				vector6 = new Vector2(0.2f, 80f);
				Log.Error("Incorrect values in section 'SpeedAndRange_Afterburner' of '" + ap._objectIniName + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 271, "loadAircraftFromInternalResources");
			}
			if (vector6.y > vector2.y)
			{
				vector6.y = vector2.y * 0.8f;
				Log.Error("Incorrect range in section 'SpeedAndRange_Afterburner' of '" + ap._objectIniName + "'. Not allowed to be more tha cruise range.", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 276, "loadAircraftFromInternalResources");
			}
			array[4] = vector6;
			ap._speedValueNames[4] = "Afterburner";
		}
		ap._rangeMultiplierCurve = new AnimationCurve();
		for (int num2 = 0; num2 < ap._numberOfSpeedValues; num2++)
		{
			ap._speedValueNames[num2] = iniHandler.readValue(sectionName2, $"SpeedValue{num2 + 1}_Name", ap._speedValueNames[num2]);
			if (array[num2].x > 10f)
			{
				ap._speedValuesInMach[num2] = array[num2].x / (Atmosphere.SpeedOfSound(3048f) * 1.94384f);
			}
			else
			{
				ap._speedValuesInMach[num2] = array[num2].x;
			}
			if (array[num2].y < 1f)
			{
				ap._rangeMultiplierCurve.AddKey(ap._speedValuesInMach[num2], 1f / array[num2].y);
			}
			else
			{
				ap._rangeMultiplierCurve.AddKey(ap._speedValuesInMach[num2], ap._maxRangeInKm / (array[num2].y * num));
			}
		}
		ap._vtolRangeMultiplier = iniHandler.readValue(sectionName2, "VTOL_RangeMultiplier", 0.8f);
		if (ap._vtolRangeMultiplier > 0.99f)
		{
			ap._vtolRangeMultiplier = 0.8f;
		}
		if (iniHandler.doesKeyExist(sectionName2, "ClimbRateCurveType"))
		{
			string text2 = iniHandler.readValue(sectionName2, "ClimbRateCurveType", "EaseInOut");
			Vector2 vector7 = iniHandler.readValue(sectionName2, "ClimbRateCurveStartValue", Vector2.zero);
			Vector2 vector8 = iniHandler.readValue(sectionName2, "ClimbRateCurveEndValue", Vector2.one);
			switch (text2)
			{
			case "Linear":
				ap._climbRateCurve = AnimationCurve.Linear(vector7.x, vector7.y, vector8.x, vector8.y);
				break;
			case "EaseInOut":
				ap._climbRateCurve = AnimationCurve.EaseInOut(vector7.x, vector7.y, vector8.x, vector8.y);
				break;
			case "Custom":
			{
				ap._climbRateCurve = new AnimationCurve();
				int num3 = iniHandler.readValue(sectionName2, "ClimbRateCurveKeys", 2);
				for (int num4 = 1; num4 <= num3; num4++)
				{
					Vector2 vector9 = iniHandler.readValue(sectionName2, "ClimbRateCurveKey" + num4, Vector2.zero);
					ap._climbRateCurve.AddKey(vector9.x, vector9.y);
				}
				break;
			}
			}
		}
		else if ((double)ap._machLimit > 1.5)
		{
			ap._climbRateCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(50f, 0.01f), new Keyframe(100f, 0.05f), new Keyframe(200f, 0.1f), new Keyframe(500f, 0.25f), new Keyframe(1000f, 0.5f), new Keyframe(2000f, 1f));
		}
		else
		{
			ap._climbRateCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(50f, 0.05f), new Keyframe(100f, 0.1f), new Keyframe(200f, 0.25f), new Keyframe(500f, 0.5f), new Keyframe(1000f, 1f));
		}
		List<int> defaultValue = new List<int> { 200, 3000, 10000, 20000, 30000 };
		defaultValue = iniHandler.readValue(sectionName2, "Altitudes", defaultValue);
		ap._presetAltitudes = new float[defaultValue.Count + 2];
		for (int num5 = 0; num5 < defaultValue.Count; num5++)
		{
			ap._presetAltitudes[num5] = (float)defaultValue[num5] * 0.00453571f;
		}
		ap._presetAltitudes[defaultValue.Count] = ap._cruiseAltitudeFeet * 0.00453571f;
		ap._presetAltitudes[defaultValue.Count + 1] = ap._ceiling;
		Array.Sort(ap._presetAltitudes);
		if (ap._engineType == EngineType.Turboprop)
		{
			ap._maxDryThrust = Atmosphere.Density(0f) * ap._maxPower / (ap._stallSpeed * 0.514444f);
		}
		if (ap._cruiseAltitudeFeet < 30000f)
		{
			ap._altitudeEfficiencyCurve = new AnimationCurve(new Keyframe(0f, vector.x), new Keyframe(ap._cruiseAltitudeFeet, 1f), new Keyframe(37000f, vector.y), new Keyframe(65000f, vector.z));
		}
		else if (ap._cruiseAltitudeFeet < 40000f)
		{
			ap._altitudeEfficiencyCurve = new AnimationCurve(new Keyframe(0f, vector.x), new Keyframe(ap._cruiseAltitudeFeet, 1f), new Keyframe(65000f, vector.z));
		}
		else
		{
			ap._altitudeEfficiencyCurve = new AnimationCurve(new Keyframe(0f, vector.x), new Keyframe(37000f, vector.y), new Keyframe(ap._cruiseAltitudeFeet, 1f), new Keyframe(65000f, vector.z));
		}
		ap._machLimitCurve = new AnimationCurve(new Keyframe(0f, ap._maxSpeedSeaLevel * 0.514444f / Atmosphere.SpeedOfSound(0f)), new Keyframe(37000f, ap._machLimit));
		ap._rigidbody.mass = ap._emptyMass;
		ap._rigidbody.linearDamping = 0f;
		string[] array2;
		if (!ap._lockWingSweep && iniHandler.doesSectionExist("WingSweep"))
		{
			string key = iniHandler.readValue("WingSweep", "LeftWingModel", "");
			ap._objectsDictionary.TryGetValue(key, out ap._leftWing);
			string key2 = iniHandler.readValue("WingSweep", "RightWingModel", "");
			ap._objectsDictionary.TryGetValue(key2, out ap._rightWing);
			array2 = iniHandler.readValue("WingSweep", "LeftWingPylons", "").Split(",");
			foreach (string key3 in array2)
			{
				if (ap._objectsDictionary.TryGetValue(key3, out var value))
				{
					ap._leftWingsweepPylons.Add(value);
				}
			}
			array2 = iniHandler.readValue("WingSweep", "RightWingPylons", "").Split(",");
			foreach (string key4 in array2)
			{
				if (ap._objectsDictionary.TryGetValue(key4, out var value2))
				{
					ap._rightWingsweepPylons.Add(value2);
				}
			}
			ap._wingSweepDegreesPerSecond = iniHandler.readValue("WingSweep", "WingSweepDegreesPerSecond", 10f);
			ap._numberOfWingSweepAngles = iniHandler.readValue("WingSweep", "NumberOfAngles", 1);
			ap._wingSweepAngle = new float[ap._numberOfWingSweepAngles];
			ap._wingSweepSpeed = new float[ap._numberOfWingSweepAngles];
			for (int num7 = 0; num7 < ap._numberOfWingSweepAngles; num7++)
			{
				ap._wingSweepAngle[num7] = iniHandler.readValue("WingSweep", "SweepAngle" + (num7 + 1), 0f);
				ap._wingSweepSpeed[num7] = iniHandler.readValue("WingSweep", "SweepSpeed" + (num7 + 1), 0f);
			}
		}
		else
		{
			ap._numberOfWingSweepAngles = 0;
		}
		string key5 = iniHandler.readValue("FlightControls", "AfterBurnerSubModel", "");
		ap._objectsDictionary.TryGetValue(key5, out var value3);
		if (value3 != null)
		{
			ap._afterburnerObject = value3;
			MeshRenderer component = value3.GetComponent<MeshRenderer>();
			if (component != null)
			{
				ap._afterBurnerMaterial = component.material;
			}
			ap._maxAfterBurnerIntensity = iniHandler.readValue("FlightControls", "MaxAfterBurnerIntensity", 0.1f);
			ap._afterBurnerFadeTime = iniHandler.readValue("FlightControls", "AfterBurnerFadeTime", 0.5f);
		}
		GameObject value4 = null;
		array2 = iniHandler.readValue("FlightControls", "PropsIdle", "").Split(',');
		foreach (string text3 in array2)
		{
			if (ap._objectsDictionary.TryGetValue(text3, out value4))
			{
				ap._propsIdle.Add(value4);
				string text4 = iniHandler.readValue(text3, "RotationAxis", "Z").ToLower();
				if (text4 == "x")
				{
					ap._propsIdleForwardAxes.Add(AircraftFlightControlSystem.RotationAxis.X);
				}
				else if (text4 == "y")
				{
					ap._propsIdleForwardAxes.Add(AircraftFlightControlSystem.RotationAxis.Y);
				}
				else
				{
					ap._propsIdleForwardAxes.Add(AircraftFlightControlSystem.RotationAxis.Z);
				}
				if (iniHandler.readValue(text3, "RotationDirection", "Left").ToLower() == "right")
				{
					ap._propClockwise.Add(item: false);
				}
				else
				{
					ap._propClockwise.Add(item: true);
				}
			}
		}
		array2 = iniHandler.readValue("FlightControls", "PropsInFlight", "").Split(',');
		foreach (string key6 in array2)
		{
			if (ap._objectsDictionary.TryGetValue(key6, out value4))
			{
				ap._propsInFlight.Add(value4);
			}
		}
		AircraftFlightControlSystem item = new AircraftFlightControlSystem(aircraft);
		ap._systems.Add(item);
		ap._miscSystems.Add(item);
		createAircraftParticles(iniHandler, ap, resourceBatch, loadAsync);
		ap._engineAudioClipName = iniHandler.readValue("Sounds", "EngineAudioClip", "");
		ap._engineAudioPosition = iniHandler.readValue("Sounds", "EngineAudioPosition", Vector3.zero);
		ap._engineAudioRollOff = iniHandler.readValue("Sounds", "EngineAudioRollOff", "").ToLower();
		ap._engineAudioDistance = iniHandler.readValue("Sounds", "EngineAudioDistance", Vector2.zero);
		ap._engineAudioPitchRange = iniHandler.readValue("Sounds", "EngineAudioPitchRange", Vector2.zero);
		ap._engineAudioVolumeRange = iniHandler.readValue("Sounds", "EngineAudioVolumeRange", Vector2.zero);
		ap._engineAudioAspectAngle = iniHandler.readValue("Sounds", "EngineAudioAspectAngle", 0f);
		ap._engineAudioAspectArcHalfRange = iniHandler.readValue("Sounds", "EngineAudioAspectArcHalfRange", 180f);
		if (ap._engineAudioAspectArcHalfRange < 1f)
		{
			ap._engineAudioAspectArcHalfRange = 1f;
		}
		ap._engineAudioAudibility = iniHandler.readValue("Sounds", "EngineAudioAudibility", "").ToLower();
		ap._exhaustAudioClipName = iniHandler.readValue("Sounds", "ExhaustAudioClip", "");
		ap._exhaustAudioPosition = iniHandler.readValue("Sounds", "ExhaustAudioPosition", Vector3.zero);
		ap._exhaustAudioRollOff = iniHandler.readValue("Sounds", "ExhaustAudioRollOff", "").ToLower();
		ap._exhaustAudioDistance = iniHandler.readValue("Sounds", "ExhaustAudioDistance", Vector2.zero);
		ap._exhaustAudioPitchRange = iniHandler.readValue("Sounds", "ExhaustAudioPitchRange", Vector2.zero);
		ap._exhaustAudioVolumeRange = iniHandler.readValue("Sounds", "ExhaustAudioVolumeRange", Vector2.zero);
		ap._exhaustAudioAspectAngle = iniHandler.readValue("Sounds", "ExhaustAudioAspectAngle", 180f);
		ap._exhaustAudioAspectArcHalfRange = iniHandler.readValue("Sounds", "ExhaustAudioAspectArcHalfRange", 180f);
		if (ap._exhaustAudioAspectArcHalfRange < 1f)
		{
			ap._exhaustAudioAspectArcHalfRange = 1f;
		}
		ap._exhaustAudioAudibility = iniHandler.readValue("Sounds", "ExhaustAudioAudibility", "").ToLower();
		ap._farAudioClipName = iniHandler.readValue("Sounds", "FarAudioClip", "");
		ap._farAudioPosition = iniHandler.readValue("Sounds", "FarAudioPosition", Vector3.zero);
		ap._farAudioRollOff = iniHandler.readValue("Sounds", "FarAudioRollOff", "").ToLower();
		ap._farAudioDistance = iniHandler.readValue("Sounds", "FarAudioDistance", Vector2.zero);
		ap._farAudioPitch = iniHandler.readValue("Sounds", "FarAudioPitch", 1f);
		ap._farAudioVolume = iniHandler.readValue("Sounds", "FarAudioVolume", 1f);
		ap._farAudioFadeRange = iniHandler.readValue("Sounds", "FarAudioFadeRange", Vector2.zero);
		ap._farAudioFadeRange.x *= 27.559496f;
		ap._farAudioFadeRange.y *= 27.559496f;
		ap._farAudioAudibility = iniHandler.readValue("Sounds", "FarAudioAudibility", "").ToLower();
		ap._afterburnerAudioClipName = iniHandler.readValue("Sounds", "AfterburnerAudioClip", "");
		ap._afterburnerAudioPosition = iniHandler.readValue("Sounds", "AfterburnerAudioPosition", Vector3.zero);
		ap._afterburnerAudioRollOff = iniHandler.readValue("Sounds", "AfterburnerAudioRollOff", "").ToLower();
		ap._afterburnerAudioDistance = iniHandler.readValue("Sounds", "AfterburnerAudioDistance", Vector2.zero);
		ap._afterburnerAudioPitch = iniHandler.readValue("Sounds", "AfterburnerAudioPitch", 1f);
		ap._afterburnerAudioVolume = iniHandler.readValue("Sounds", "AfterburnerAudioVolume", 1f);
		ap._afterburnerAudioAudibility = iniHandler.readValue("Sounds", "AfterburnerAudioAudibility", "").ToLower();
		ap._gearsAudioClipName = iniHandler.readValue("Sounds", "GearsAudioClip", "");
		ap._gearsAudioPosition = iniHandler.readValue("Sounds", "GearsAudioPosition", Vector3.zero);
		ap._gearsAudioRollOff = iniHandler.readValue("Sounds", "GearsAudioRollOff", "").ToLower();
		ap._gearsAudioDistance = iniHandler.readValue("Sounds", "GearsAudioDistance", Vector2.zero);
		ap._gearsAudioPitch = iniHandler.readValue("Sounds", "GearsAudioPitch", 1f);
		ap._gearsAudioVolume = iniHandler.readValue("Sounds", "GearsAudioVolume", 1f);
		ap._gearsAudioAudibility = iniHandler.readValue("Sounds", "GearsAudioAudibility", "").ToLower();
		ap._flapsAudioClipName = iniHandler.readValue("Sounds", "FlapsAudioClip", "");
		ap._flapsAudioPosition = iniHandler.readValue("Sounds", "FlapsAudioPosition", Vector3.zero);
		ap._flapsAudioRollOff = iniHandler.readValue("Sounds", "FlapsAudioRollOff", "").ToLower();
		ap._flapsAudioDistance = iniHandler.readValue("Sounds", "FlapsAudioDistance", Vector2.zero);
		ap._flapsAudioPitch = iniHandler.readValue("Sounds", "FlapsAudioPitch", 1f);
		ap._flapsAudioVolume = iniHandler.readValue("Sounds", "FlapsAudioVolume", 1f);
		ap._flapsAudioAudibility = iniHandler.readValue("Sounds", "FlapsAudioAudibility", "").ToLower();
		createAircraftSounds(ap, resourceBatch, loadAsync);
		Vector2 centerTile = Utils.longLatToTile(geoCenterPosition);
		Vector2 vector10 = Utils.longLatToLocal(geoPosition, centerTile);
		if ((bool)ap._rootGameObject)
		{
			ap._rootGameObject.transform.position = new Vector3(vector10.x, (float)geoPosition._height, vector10.y);
		}
		ap._rigidbody.isKinematic = true;
		ApplyLoadoutInfluences(ap);
		resourceBatch.Start();
		return ap;
	}

	private static void ApplyLoadoutInfluences(AircraftParameters ap)
	{
		ap._defaultMaxRangeInKm = ap._maxRangeInKm;
		ap._defaultMaxFuel = ap._maxFuel;
		ap._maxFuel += ap._loadoutFuel;
		ap._maxRangeInKm = ap._maxFuel * ap._defaultMaxRangeInKm / ap._defaultMaxFuel;
	}

	private static void createAircraftRootObject(AircraftParameters aircraftParameters)
	{
		aircraftParameters._rigidbody = aircraftParameters._rootGameObject.AddComponent<Rigidbody>();
		aircraftParameters._rigidbody.useGravity = false;
		aircraftParameters._rigidbody.isKinematic = true;
		aircraftParameters._rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
		aircraftParameters._particlesObject = new GameObject("Particles");
		aircraftParameters._particlesObject.transform.SetParent(aircraftParameters._visibleObjectsParent.transform, worldPositionStays: false);
		aircraftParameters._soundsObject = new GameObject("Sounds");
		aircraftParameters._soundsObject.transform.SetParent(aircraftParameters._rootGameObject.transform, worldPositionStays: false);
	}

	private static void createAircraftParticles(IniHandler ini, AircraftParameters ap, ResourceLoader.ResourceBatch batch, bool async = false)
	{
		if (ini.doesKeyExist("Particles", "ExhaustSmoke"))
		{
			string resourceName = ini.readValue("Particles", "ExhaustSmoke", "", performLogging: false);
			batch.Add(Singleton<ResourceLoader>.Instance.RequestGameObjectResource(resourceName, delegate(GameObject exhaustSmokeObject)
			{
				if (exhaustSmokeObject != null)
				{
					ap._exhaustObject = UnityEngine.Object.Instantiate(exhaustSmokeObject);
					ap._exhaustObject.SetActive(value: true);
					ap._exhaustObject.transform.SetParent(ap._particlesObject.transform, worldPositionStays: false);
					ap._exhaustObject.transform.localPosition = Vector3.zero;
					ap._exhaustObject.transform.localRotation = Quaternion.identity;
					ap._exhaustObject.layer = 0;
					ParticleUtils.SetAutoDestroyAndCustomWorldSpace(ap._exhaustObject, autodestroy: false, ap._trailRenderers);
				}
			}, async));
		}
		GameObject rightWingVortexHolder = null;
		GameObject leftWingVortexHolder = null;
		if (ap._numberOfWingSweepAngles > 0)
		{
			rightWingVortexHolder = new GameObject();
			rightWingVortexHolder.name = "left vortex holder";
			rightWingVortexHolder.transform.SetParent(ap._particlesObject.transform, worldPositionStays: false);
			rightWingVortexHolder.transform.localPosition = ap._rightWing.transform.localPosition;
			rightWingVortexHolder.transform.localRotation = Quaternion.identity;
			leftWingVortexHolder = new GameObject();
			leftWingVortexHolder.name = "right vortex holder";
			leftWingVortexHolder.transform.SetParent(ap._particlesObject.transform, worldPositionStays: false);
			leftWingVortexHolder.transform.localPosition = ap._leftWing.transform.localPosition;
			leftWingVortexHolder.transform.localRotation = Quaternion.identity;
		}
		if (ini.doesKeyExist("Particles", "TipVortex"))
		{
			Vector3 tipVortexOffset = ini.readValue("Particles", "TipVortexPosition", Vector3.zero, performLogging: false);
			string tipVortexName = ini.readValue("Particles", "TipVortex", "");
			batch.Add(Singleton<ResourceLoader>.Instance.RequestGameObjectResource(tipVortexName, delegate(GameObject tipVortexObject)
			{
				if (tipVortexObject != null)
				{
					GameObject gameObject = UnityEngine.Object.Instantiate(tipVortexObject);
					gameObject.SetActive(value: true);
					if (rightWingVortexHolder != null)
					{
						gameObject.transform.SetParent(rightWingVortexHolder.transform, worldPositionStays: false);
					}
					else
					{
						gameObject.transform.SetParent(ap._particlesObject.transform, worldPositionStays: false);
					}
					gameObject.transform.localPosition = tipVortexOffset;
					gameObject.transform.localRotation = Quaternion.identity;
					gameObject.layer = 0;
					ParticleUtils.SetAutoDestroyAndCustomWorldSpace(gameObject, autodestroy: false);
					ap._tipVortexRight = gameObject.GetComponent<ParticleSystem>();
					gameObject = UnityEngine.Object.Instantiate(tipVortexObject);
					gameObject.SetActive(value: true);
					if (leftWingVortexHolder != null)
					{
						gameObject.transform.SetParent(leftWingVortexHolder.transform, worldPositionStays: false);
					}
					else
					{
						gameObject.transform.SetParent(ap._particlesObject.transform, worldPositionStays: false);
					}
					gameObject.transform.localPosition = new Vector3(0f - tipVortexOffset.x, tipVortexOffset.y, tipVortexOffset.z);
					gameObject.transform.localRotation = Quaternion.identity;
					gameObject.layer = 0;
					ParticleUtils.SetAutoDestroyAndCustomWorldSpace(gameObject, autodestroy: false);
					ap._tipVortexLeft = gameObject.GetComponent<ParticleSystem>();
				}
				else
				{
					Log.DebugWarning("WARNING: Unable to load Tip Vortex particles from " + tipVortexName + " for " + ini.getFileName(), Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 733, "createAircraftParticles");
				}
			}, async));
		}
		if (ini.doesKeyExist("Particles", "WingVortex"))
		{
			string vortexName = ini.readValue("Particles", "WingVortex", "");
			batch.Add(Singleton<ResourceLoader>.Instance.RequestGameObjectResource(vortexName, delegate(GameObject vortexObject)
			{
				if (vortexObject != null)
				{
					GameObject gameObject = UnityEngine.Object.Instantiate(vortexObject);
					gameObject.SetActive(value: true);
					gameObject.transform.SetParent(ap._particlesObject.transform, worldPositionStays: false);
					gameObject.transform.localPosition = ini.readValue("Particles", "WingVortexPosition", Vector3.zero, performLogging: false);
					gameObject.transform.localRotation = Quaternion.identity;
					gameObject.layer = 0;
					ParticleUtils.SetAutoDestroyAndCustomWorldSpace(gameObject, autodestroy: false);
					ap._wingVortex = gameObject.GetComponent<ParticleSystem>();
				}
				else
				{
					Log.DebugWarning("WARNING: Unable to load WingVortex Particles from " + vortexName + " for " + ini.getFileName(), Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 760, "createAircraftParticles");
				}
			}, async));
		}
		if (ini.doesKeyExist("Particles", "RightWingVortex"))
		{
			string vortexName2 = ini.readValue("Particles", "RightWingVortex", "");
			batch.Add(Singleton<ResourceLoader>.Instance.RequestGameObjectResource(vortexName2, delegate(GameObject vortexObject)
			{
				if (vortexObject != null)
				{
					GameObject gameObject = UnityEngine.Object.Instantiate(vortexObject);
					gameObject.SetActive(value: true);
					if (rightWingVortexHolder != null)
					{
						gameObject.transform.SetParent(rightWingVortexHolder.transform, worldPositionStays: false);
					}
					else
					{
						gameObject.transform.SetParent(ap._particlesObject.transform, worldPositionStays: false);
					}
					gameObject.transform.localPosition = ini.readValue("Particles", "RightWingVortexPosition", Vector3.zero, performLogging: false);
					gameObject.transform.localRotation = Quaternion.identity;
					gameObject.layer = 0;
					ParticleUtils.SetAutoDestroyAndCustomWorldSpace(gameObject, autodestroy: false);
					ap._wingVortexRight = gameObject.GetComponent<ParticleSystem>();
				}
				else
				{
					Log.DebugWarning("WARNING: Unable to load RightWingVortex Particles from " + vortexName2 + " for " + ini.getFileName(), Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 788, "createAircraftParticles");
				}
			}, async));
		}
		if (ini.doesKeyExist("Particles", "LeftWingVortex"))
		{
			string vortexName3 = ini.readValue("Particles", "LeftWingVortex", "");
			batch.Add(Singleton<ResourceLoader>.Instance.RequestGameObjectResource(vortexName3, delegate(GameObject vortexObject)
			{
				if (vortexObject != null)
				{
					GameObject gameObject = UnityEngine.Object.Instantiate(vortexObject);
					gameObject.SetActive(value: true);
					if (leftWingVortexHolder != null)
					{
						gameObject.transform.SetParent(leftWingVortexHolder.transform, worldPositionStays: false);
					}
					else
					{
						gameObject.transform.SetParent(ap._particlesObject.transform, worldPositionStays: false);
					}
					gameObject.transform.localPosition = ini.readValue("Particles", "LeftWingVortexPosition", Vector3.zero, performLogging: false);
					gameObject.transform.localRotation = Quaternion.identity;
					gameObject.layer = 0;
					ParticleUtils.SetAutoDestroyAndCustomWorldSpace(gameObject, autodestroy: false);
					ap._wingVortexLeft = gameObject.GetComponent<ParticleSystem>();
				}
				else
				{
					Log.DebugWarning("WARNING: Unable to load LeftWingVortex Particles from " + vortexName3 + " for " + ini.getFileName(), Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 816, "createAircraftParticles");
				}
			}, async));
		}
		if (!ini.doesKeyExist("Particles", "ShockCone"))
		{
			return;
		}
		string shockConeName = ini.readValue("Particles", "ShockCone", "");
		batch.Add(Singleton<ResourceLoader>.Instance.RequestGameObjectResource(shockConeName, delegate(GameObject shockConeObject)
		{
			if (shockConeObject != null)
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(shockConeObject);
				gameObject.SetActive(value: true);
				gameObject.transform.SetParent(ap._particlesObject.transform, worldPositionStays: false);
				gameObject.transform.localPosition = ini.readValue("Particles", "ShockConePosition", Vector3.zero, performLogging: false);
				gameObject.transform.localRotation = Quaternion.identity;
				gameObject.layer = 0;
				ParticleUtils.SetAutoDestroyAndCustomWorldSpace(gameObject, autodestroy: false);
				ap._shockCone = gameObject.GetComponent<ParticleSystem>();
			}
			else
			{
				Log.DebugWarning("WARNING: Unable to load ShockCone Particles from " + shockConeName + " for " + ini.getFileName(), Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 843, "createAircraftParticles");
			}
		}, async));
	}

	private static void createAircraftSounds(AircraftParameters ap, ResourceLoader.ResourceBatch batch, bool async = false)
	{
		AudioMixerGroup outputAudioMixerGroup = Singleton<EnvironmentAudioManager>.Instance.getMixer().FindMatchingGroups("Sfx")[0];
		if (!string.IsNullOrEmpty(ap._engineAudioClipName))
		{
			ap._engineAudioObject = new GameObject("Engine");
			ap._engineAudioSource = ap._engineAudioObject.AddComponent<AudioSource>();
			batch.Add(Singleton<ResourceLoader>.Instance.getAudioClipResourceAsync(ap._engineAudioClipName, delegate(AudioClip c)
			{
				ap._engineAudioSource.clip = c;
			}, !async));
			if (ap._engineAudioRollOff == "logarithmic")
			{
				ap._engineAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
			}
			else
			{
				ap._engineAudioSource.rolloffMode = AudioRolloffMode.Linear;
			}
			ap._engineAudioSource.playOnAwake = false;
			ap._engineAudioSource.loop = true;
			ap._engineAudioSource.minDistance = ap._engineAudioDistance.x;
			ap._engineAudioSource.maxDistance = ap._engineAudioDistance.y;
			ap._engineAudioSource.pitch = ap._engineAudioPitchRange.x;
			ap._engineAudioSource.volume = ap._engineAudioVolumeRange.y;
			ap._engineAudioSource.spatialBlend = 1f;
			ap._engineAudioSource.outputAudioMixerGroup = outputAudioMixerGroup;
			ap._engineAudioSource.transform.position = ap._engineAudioPosition;
			ap._engineAudioSource.transform.SetParent(ap._soundsObject.transform, worldPositionStays: false);
		}
		else
		{
			Log.DebugWarning("Aircraft '" + ap._objectMeshName + "' has no engine audio sound", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 883, "createAircraftSounds");
		}
		if (!string.IsNullOrEmpty(ap._exhaustAudioClipName))
		{
			ap._exhaustAudioObject = new GameObject("Exhaust");
			ap._exhaustAudioSource = ap._exhaustAudioObject.AddComponent<AudioSource>();
			batch.Add(Singleton<ResourceLoader>.Instance.getAudioClipResourceAsync(ap._exhaustAudioClipName, delegate(AudioClip c)
			{
				ap._exhaustAudioSource.clip = c;
			}, !async));
			if (ap._exhaustAudioRollOff == "logarithmic")
			{
				ap._exhaustAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
			}
			else
			{
				ap._exhaustAudioSource.rolloffMode = AudioRolloffMode.Linear;
			}
			ap._exhaustAudioSource.playOnAwake = false;
			ap._exhaustAudioSource.loop = true;
			ap._exhaustAudioSource.minDistance = ap._exhaustAudioDistance.x;
			ap._exhaustAudioSource.maxDistance = ap._exhaustAudioDistance.y;
			ap._exhaustAudioSource.pitch = ap._exhaustAudioPitchRange.x;
			ap._exhaustAudioSource.volume = ap._exhaustAudioVolumeRange.y;
			ap._exhaustAudioSource.spatialBlend = 1f;
			ap._exhaustAudioSource.outputAudioMixerGroup = outputAudioMixerGroup;
			ap._exhaustAudioSource.transform.position = ap._exhaustAudioPosition;
			ap._exhaustAudioSource.transform.SetParent(ap._soundsObject.transform, worldPositionStays: false);
		}
		else
		{
			Log.DebugWarning("Aircraft '" + ap._objectMeshName + "' has no exhaust audio sound", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 907, "createAircraftSounds");
		}
		if (!string.IsNullOrEmpty(ap._farAudioClipName))
		{
			ap._farAudioObject = new GameObject("Far");
			ap._farAudioSource = ap._farAudioObject.AddComponent<AudioSource>();
			batch.Add(Singleton<ResourceLoader>.Instance.getAudioClipResourceAsync(ap._farAudioClipName, delegate(AudioClip c)
			{
				ap._farAudioSource.clip = c;
			}, !async));
			if (ap._farAudioRollOff == "logarithmic")
			{
				ap._farAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
			}
			else
			{
				ap._farAudioSource.rolloffMode = AudioRolloffMode.Linear;
			}
			ap._farAudioSource.playOnAwake = false;
			ap._farAudioSource.loop = true;
			ap._farAudioSource.minDistance = ap._farAudioDistance.x;
			ap._farAudioSource.maxDistance = ap._farAudioDistance.y;
			ap._farAudioSource.pitch = ap._farAudioPitch;
			ap._farAudioSource.volume = ap._farAudioVolume;
			ap._farAudioSource.spatialBlend = 1f;
			ap._farAudioSource.outputAudioMixerGroup = outputAudioMixerGroup;
			ap._farAudioSource.transform.position = ap._farAudioPosition;
			ap._farAudioSource.transform.SetParent(ap._soundsObject.transform, worldPositionStays: false);
		}
		else
		{
			Log.DebugWarning("Aircraft '" + ap._objectMeshName + "' has no far audio sound", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 931, "createAircraftSounds");
		}
		if (!string.IsNullOrEmpty(ap._afterburnerAudioClipName))
		{
			ap._afterburnerAudioObject = new GameObject("Afterburner");
			ap._afterburnerAudioSource = ap._afterburnerAudioObject.AddComponent<AudioSource>();
			batch.Add(Singleton<ResourceLoader>.Instance.getAudioClipResourceAsync(ap._afterburnerAudioClipName, delegate(AudioClip c)
			{
				ap._afterburnerAudioSource.clip = c;
			}, !async));
			if (ap._afterburnerAudioRollOff == "logarithmic")
			{
				ap._afterburnerAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
			}
			else
			{
				ap._afterburnerAudioSource.rolloffMode = AudioRolloffMode.Linear;
			}
			ap._afterburnerAudioSource.playOnAwake = false;
			ap._afterburnerAudioSource.loop = true;
			ap._afterburnerAudioSource.minDistance = ap._afterburnerAudioDistance.x;
			ap._afterburnerAudioSource.maxDistance = ap._afterburnerAudioDistance.y;
			ap._afterburnerAudioSource.pitch = ap._afterburnerAudioPitch;
			ap._afterburnerAudioSource.volume = ap._afterburnerAudioVolume;
			ap._afterburnerAudioSource.spatialBlend = 1f;
			ap._afterburnerAudioSource.outputAudioMixerGroup = outputAudioMixerGroup;
			ap._afterburnerAudioSource.transform.position = ap._afterburnerAudioPosition;
			ap._afterburnerAudioSource.transform.SetParent(ap._soundsObject.transform, worldPositionStays: false);
		}
		if (!string.IsNullOrEmpty(ap._gearsAudioClipName))
		{
			ap._gearsAudioObject = new GameObject("Gears");
			ap._gearsAudioSource = ap._gearsAudioObject.AddComponent<AudioSource>();
			batch.Add(Singleton<ResourceLoader>.Instance.getAudioClipResourceAsync(ap._gearsAudioClipName, delegate(AudioClip c)
			{
				ap._gearsAudioSource.clip = c;
			}, !async));
			if (ap._gearsAudioRollOff == "logarithmic")
			{
				ap._gearsAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
			}
			else
			{
				ap._gearsAudioSource.rolloffMode = AudioRolloffMode.Linear;
			}
			ap._gearsAudioSource.playOnAwake = false;
			ap._gearsAudioSource.loop = true;
			ap._gearsAudioSource.minDistance = ap._gearsAudioDistance.x;
			ap._gearsAudioSource.maxDistance = ap._gearsAudioDistance.y;
			ap._gearsAudioSource.pitch = ap._gearsAudioPitch;
			ap._gearsAudioSource.volume = ap._gearsAudioVolume;
			ap._gearsAudioSource.spatialBlend = 1f;
			ap._gearsAudioSource.outputAudioMixerGroup = outputAudioMixerGroup;
			ap._gearsAudioSource.transform.position = ap._gearsAudioPosition;
			ap._gearsAudioSource.transform.SetParent(ap._soundsObject.transform, worldPositionStays: false);
		}
		else
		{
			Log.DebugWarning("Aircraft '" + ap._objectMeshName + "' has no gears audio sound", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 977, "createAircraftSounds");
		}
		if (!string.IsNullOrEmpty(ap._flapsAudioClipName))
		{
			ap._flapsAudioObject = new GameObject("Flaps");
			ap._flapsAudioSource = ap._flapsAudioObject.AddComponent<AudioSource>();
			batch.Add(Singleton<ResourceLoader>.Instance.getAudioClipResourceAsync(ap._flapsAudioClipName, delegate(AudioClip c)
			{
				ap._flapsAudioSource.clip = c;
			}, !async));
			if (ap._flapsAudioRollOff == "logarithmic")
			{
				ap._flapsAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
			}
			else
			{
				ap._flapsAudioSource.rolloffMode = AudioRolloffMode.Linear;
			}
			ap._flapsAudioSource.playOnAwake = false;
			ap._flapsAudioSource.loop = true;
			ap._flapsAudioSource.minDistance = ap._flapsAudioDistance.x;
			ap._flapsAudioSource.maxDistance = ap._flapsAudioDistance.y;
			ap._flapsAudioSource.pitch = ap._flapsAudioPitch;
			ap._flapsAudioSource.volume = ap._flapsAudioVolume;
			ap._flapsAudioSource.spatialBlend = 1f;
			ap._flapsAudioSource.outputAudioMixerGroup = outputAudioMixerGroup;
			ap._flapsAudioSource.transform.position = ap._flapsAudioPosition;
			ap._flapsAudioSource.transform.SetParent(ap._soundsObject.transform, worldPositionStays: false);
		}
		else
		{
			Log.DebugWarning("Aircraft '" + ap._objectMeshName + "' has no flaps audio sound", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\AircraftLoader.cs", 1001, "createAircraftSounds");
		}
	}
}
