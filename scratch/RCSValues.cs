using System;

namespace SeaPower;

public class RCSValues
{
	public float _veryTiny;

	public float _tiny;

	public float _verySmall;

	public float _small;

	public float _semiSmall;

	public float _meager;

	public float _medium;

	public float _semiLarge;

	public float _large;

	public float _veryLarge;

	public float _frontalToBroadsideFactor;

	public float _jamToSignalBurnThroughRatio;

	public double _minDetectableReturnPowerRadar;

	public double _minDetectableReturnPowerESM;

	public RCSValues()
	{
		IniHandler iniHandler = Utils.openVirtualIniFile("systems", "sensors", "sensors");
		_veryTiny = iniHandler.readValue("RCS", "VeryTiny", 0.001f);
		_tiny = iniHandler.readValue("RCS", "Tiny", 0.01f);
		_verySmall = iniHandler.readValue("RCS", "VerySmall", 0.1f);
		_small = iniHandler.readValue("RCS", "Small", 1f);
		_semiSmall = iniHandler.readValue("RCS", "SemiSmall", 10f);
		_meager = iniHandler.readValue("RCS", "Meager", 50f);
		_medium = iniHandler.readValue("RCS", "Medium", 100f);
		_semiLarge = iniHandler.readValue("RCS", "SemiLarge", 1000f);
		_large = iniHandler.readValue("RCS", "Large", 10000f);
		_veryLarge = iniHandler.readValue("RCS", "VeryLarge", 100000f);
		_frontalToBroadsideFactor = iniHandler.readValue("RCS", "FrontalToBroadsideFactor", 0.5f);
		_jamToSignalBurnThroughRatio = iniHandler.readValue("RCS", "JamToSignalBurnThroughRatio", 3f);
		_minDetectableReturnPowerRadar = iniHandler.readValue("RCS", "MinDetectableReturnPowerRadar", 2E-14);
		_minDetectableReturnPowerESM = iniHandler.readValue("RCS", "MinDetectableReturnPowerESM", 4E-10);
	}

	public static EnumRCSValues ReadRCSValue(string value)
	{
		if (float.TryParse(value, out var result))
		{
			return Enum.Parse<EnumRCSValues>(Globals.RcsValues.GetSizeFromRCS(result), ignoreCase: true);
		}
		return Enum.Parse<EnumRCSValues>(value, ignoreCase: true);
	}

	public float getRCSValue(string value)
	{
		float result = 1f;
		if (float.TryParse(value, out result))
		{
			return result;
		}
		switch (value)
		{
		case "verytiny":
			result = _veryTiny;
			break;
		case "tiny":
			result = _tiny;
			break;
		case "verysmall":
			result = _verySmall;
			break;
		case "small":
			result = _small;
			break;
		case "semismall":
			result = _semiSmall;
			break;
		case "meager":
			result = _meager;
			break;
		case "medium":
			result = _medium;
			break;
		case "semilarge":
			result = _semiLarge;
			break;
		case "large":
			result = _large;
			break;
		case "verylarge":
			result = _veryLarge;
			break;
		default:
			result = _medium;
			Log.DebugWarning("Unknown RCS value: '" + value + "', using MEDIUM as fallback", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Systems\\Sensors\\RCSValues.cs", 87, "getRCSValue");
			break;
		}
		return result;
	}

	public string GetSizeFromRCS(float value)
	{
		if (value < _tiny)
		{
			return "VeryTiny";
		}
		if (value < _verySmall)
		{
			return "Tiny";
		}
		if (value < _small)
		{
			return "VerySmall";
		}
		if (value < _semiSmall)
		{
			return "Small";
		}
		if (value < _meager)
		{
			return "SemiSmall";
		}
		if (value < _medium)
		{
			return "Meager";
		}
		if (value < _semiLarge)
		{
			return "Medium";
		}
		if (value < _large)
		{
			return "SemiLarge";
		}
		if (value < _veryLarge)
		{
			return "Large";
		}
		return "VeryLarge";
	}
}
