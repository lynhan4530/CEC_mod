using System.Collections.Generic;
using UnityEngine;

namespace SeaPower;

public static class FireControl
{
	public static SensorSystem ResolveBestDirector(List<SensorSystem> sensors, bool requireContacts, bool ignoreDamage = false)
	{
		if (sensors == null)
		{
			return null;
		}
		SensorSystem sensorSystem = null;
		bool flag = false;
		foreach (SensorSystem sensor in sensors)
		{
			if (sensor == null || (!ignoreDamage && (sensor.Inoperable.Value || !sensor.IsOn.Value || (requireContacts && !sensor.CanProduceContacts))))
			{
				continue;
			}
			sensor.ResolveFireControlProfile();
			bool flag2 = sensor is SensorSystemRadar;
			if (sensorSystem == null)
			{
				sensorSystem = sensor;
				flag = flag2;
			}
			else if (flag2 != flag)
			{
				if (flag2)
				{
					sensorSystem = sensor;
					flag = true;
				}
			}
			else if (sensor._fcSolutionGainRate > sensorSystem._fcSolutionGainRate)
			{
				sensorSystem = sensor;
			}
		}
		return sensorSystem;
	}

	public static bool HasAuthoredDirector(List<SensorSystem> sensors)
	{
		if (sensors == null)
		{
			return false;
		}
		foreach (SensorSystem sensor in sensors)
		{
			if (sensor != null)
			{
				return true;
			}
		}
		return false;
	}

	public static float DarknessErrorFactor(SensorSystem fc)
	{
		float num = fc?.GetFireControlLightFactor() ?? ((Singleton<DynamicSky>.Instance != null) ? Mathf.Clamp01(Singleton<DynamicSky>.Instance.getSunlightIntensity()) : 1f);
		return 1f + 2f * (1f - num);
	}

	public static FireControlClass Classify(FireControlMode? authored, List<SensorSystem> sensors, bool ignoreDamage = false)
	{
		SensorSystem sensorSystem = ResolveBestDirector(sensors, requireContacts: true, ignoreDamage);
		bool flag = HasAuthoredDirector(sensors);
		FireControlMode fireControlMode = (authored.HasValue ? authored.Value : ((sensorSystem != null) ? ((!sensorSystem._fcTracksRounds) ? FireControlMode.Director : FireControlMode.ClosedLoop) : ((!flag) ? FireControlMode.Director : FireControlMode.Director)));
		if (!ignoreDamage && flag)
		{
			if (sensorSystem == null)
			{
				fireControlMode = FireControlMode.Manual;
			}
			else if (fireControlMode == FireControlMode.ClosedLoop && !sensorSystem._fcTracksRounds)
			{
				fireControlMode = FireControlMode.Director;
			}
		}
		FireControlClass result = default(FireControlClass);
		result._mode = fireControlMode;
		if (sensorSystem != null)
		{
			result._gain = sensorSystem._fcSolutionGainRate;
		}
		else
		{
			result._gain = ((fireControlMode == FireControlMode.Manual) ? Globals._gunLocalControlGainRate : 1f);
		}
		if (result._gain <= 0.01f)
		{
			result._gain = 1f;
		}
		result._spotFactor = ((fireControlMode == FireControlMode.Manual) ? DarknessErrorFactor(sensorSystem) : 1f);
		return result;
	}

	public static FireControlMode? ParseMode(IniHandler ini, string section, string key = "FireControlMode")
	{
		if (ini == null || !ini.doesKeyExist(section, key))
		{
			return null;
		}
		string text = ini.readValue(section, key, "", performLogging: false);
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		switch (text.Trim().ToLowerInvariant())
		{
		case "manual":
		case "local":
		case "localcontrol":
			return FireControlMode.Manual;
		case "director":
		case "radar":
			return FireControlMode.Director;
		case "closedloop":
		case "closed":
		case "cls":
		case "spotting":
			return FireControlMode.ClosedLoop;
		default:
			Log.Error("Unknown FireControlMode '" + text + "' in [" + section + "] - expected Manual, Director or ClosedLoop", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Weapons\\FireControl.cs", 173, "ParseMode");
			return null;
		}
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
