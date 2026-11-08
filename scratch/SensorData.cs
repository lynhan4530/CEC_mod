using UnityEngine;

namespace SeaPower;

public class SensorData
{
	public enum PropellerType
	{
		Regular,
		Tandem,
		Skewback,
		PumpJet
	}

	public string _objectName;

	public float _minRCS = -1f;

	public float _maxRCS = -1f;

	public string _size;

	public float _baseNoise = -1f;

	public float _flowNoise;

	public float _cavitationNoise;

	public float _creepSpeedKts;

	public float _belowCreepQuietingPerKt;

	public float _aspectNoiseDeltaDB;

	public float _minTargetReflectionStrength;

	public float _maxTargetReflectionStrength = -1f;

	public float _irSignatureValue;

	public float _visualIdentificationRange = -1f;

	public float _vidIncrease;

	public double _vidIncreaseEndTime = -1.0;

	public float _frontalAreaM2 = -1f;

	public float _sideAreaM2 = -1f;

	public float _bottomAreaM2 = -1f;

	public float _visibilityFromFront = 0.5f;

	public bool _isLowMagnetic;

	public bool _isCamouflaged;

	public int _shaftsCount;

	public int _bladesCount;

	public int _electricFrequency;

	public bool _distinctiveNoise;

	public PropellerType _propellerType;

	public float _propellerTonalDeficitDB;

	public bool _hasPrairie;

	public int _machineryRaftingGeneration;

	public float _machineryTonalDeficitDB;

	public float _tonalRPMModifier;

	public float _displacement;

	public const int NoiseBandAll = 14;

	public int _noiseBandMask = 14;

	public float _transientNoise = -1f;

	public float _transientMaxRCS = -1f;

	public string _transientSize;

	public float _transientVIDRange = -1f;

	public bool _hasTransient;

	public float _launchTransientMitigation;

	public float VisualIdentificationRange => _visualIdentificationRange + _vidIncrease;

	public float NoiseBandMismatchDB(SonarParameters.SonarFrequency band)
	{
		if (band == SonarParameters.SonarFrequency.Undefined || (_noiseBandMask & (1 << (int)band)) != 0)
		{
			return 0f;
		}
		int num = 99;
		for (int i = 1; i <= 3; i++)
		{
			if ((_noiseBandMask & (1 << i)) != 0)
			{
				num = Mathf.Min(num, Mathf.Abs((int)(i - band)));
			}
		}
		if (num < 99)
		{
			return -15f * (float)num;
		}
		return 0f;
	}

	public SensorData()
	{
	}

	public SensorData(string iniFolder, string objectName)
	{
		IniHandler iniHandler = Utils.openIniFile("/" + iniFolder, "/" + objectName + ".ini");
		switch (iniFolder)
		{
		case "aircraft":
			if (iniHandler.readValue("General", "UnitType", "") == "Helicopter")
			{
				_visibilityFromFront = 0.5f;
			}
			else
			{
				_visibilityFromFront = 0.3f;
			}
			break;
		case "vessels":
			if (iniHandler.readValue("General", "UnitType", "") == "Vessel")
			{
				_visibilityFromFront = 0.61f;
			}
			else
			{
				_visibilityFromFront = 0.4f;
			}
			break;
		case "landunits":
			if (iniHandler.readValue("General", "LandUnitSubType", "") == "MobileUnit" || iniHandler.readValue("General", "IsMobile", defaultValue: false))
			{
				_visibilityFromFront = 0.8f;
			}
			else
			{
				_visibilityFromFront = 1f;
			}
			break;
		case "ammunition":
			if (iniHandler.readValue("General", "UnitType", "") == "Missile")
			{
				_visibilityFromFront = 0.2f;
			}
			else if (iniHandler.readValue("General", "UnitType", "") == "Bomb")
			{
				_visibilityFromFront = 0.2f;
			}
			break;
		}
		ReadSensorData(iniHandler, objectName);
	}

	public SensorData(IniHandler ini, string objectName)
	{
		ReadSensorData(ini, objectName);
	}

	public void ReadSensorData(IniHandler ini, string objectName)
	{
		_objectName = objectName;
		_size = ini.readValue("SensorData", "RCS", "Medium");
		if (float.TryParse(_size, out _maxRCS))
		{
			_size = Globals.RcsValues.GetSizeFromRCS(_maxRCS);
		}
		else
		{
			_maxRCS = Globals.RcsValues.getRCSValue(_size.ToLower());
		}
		_frontalAreaM2 = ini.readValue("General", "FrontalArea", -1f, performLogging: false);
		_sideAreaM2 = ini.readValue("General", "SideArea", -1f, performLogging: false);
		_bottomAreaM2 = ini.readValue("General", "BottomArea", -1f, performLogging: false);
		_minRCS = Globals.RcsValues._frontalToBroadsideFactor * _maxRCS;
		if (_frontalAreaM2 > 0f && _sideAreaM2 > 0f)
		{
			_minRCS = Mathf.Clamp(_frontalAreaM2 / _sideAreaM2, 0.02f, 1f) * _maxRCS;
		}
		_irSignatureValue = Globals.IrValues.getIRSignatureValue(ini.readValue("SensorData", "IRSignature", "Medium").ToLower());
		if (ini.doesKeyExist("SensorData", "VisualIdentificationRange"))
		{
			_visualIdentificationRange = ini.readValue("SensorData", "VisualIdentificationRange", 10f) * 27.559496f;
		}
		_isCamouflaged = ini.readValue("SensorData", "Camouflaged", defaultValue: false);
		_isLowMagnetic = ini.readValue("SensorData", "LowMagneticHull", defaultValue: false);
		if (ini.doesKeyExist("SensorData", "BaseNoise"))
		{
			_baseNoise = ini.readValue("SensorData", "BaseNoise", 100f);
			_flowNoise = ini.readValue("SensorData", "FlowNoise", 1f);
			_cavitationNoise = ini.readValue("SensorData", "CavitationNoise", 10f);
			_maxTargetReflectionStrength = ini.readValue("SensorData", "TargetReflectionStrength", 25f);
			_minTargetReflectionStrength = Globals.RcsValues._frontalToBroadsideFactor * _maxTargetReflectionStrength;
			if (_frontalAreaM2 > 0f && _sideAreaM2 > 0f)
			{
				_minTargetReflectionStrength = _maxTargetReflectionStrength + 10f * Mathf.Log10(Mathf.Clamp(_frontalAreaM2 / _sideAreaM2, 0.02f, 1f));
			}
			_aspectNoiseDeltaDB = ini.readValue("SensorData", "AspectNoiseDelta", 0f);
			_shaftsCount = ini.readValue("PropulsionSystems", "NumberOfPropulsionSystems", 1);
			_bladesCount = ini.readValue("SensorData", "BladesCount", 5);
			_electricFrequency = ini.readValue("SensorData", "ElectricFrequency", 50);
			_distinctiveNoise = ini.readValue("SensorData", "DistinctiveNoise", defaultValue: false);
			_propellerType = ini.readValue("SensorData", "PropellerType", "regular").ToLower() switch
			{
				"tandem" => PropellerType.Tandem, 
				"skewback" => PropellerType.Skewback, 
				"pumpjet" => PropellerType.PumpJet, 
				_ => PropellerType.Regular, 
			};
			_propellerTonalDeficitDB = _propellerType switch
			{
				PropellerType.Tandem => 3f, 
				PropellerType.Skewback => 10f, 
				PropellerType.PumpJet => 20f, 
				_ => 0f, 
			};
			_hasPrairie = ini.readValue("SensorData", "Prairie", defaultValue: false);
			string text = ini.readValue("SensorData", "NoiseFrequencies", "");
			if (!string.IsNullOrWhiteSpace(text))
			{
				int num = 0;
				string[] array = text.Split(',');
				for (int i = 0; i < array.Length; i++)
				{
					switch (array[i].Trim().ToLowerInvariant())
					{
					case "lf":
						num |= 2;
						break;
					case "mf":
						num |= 4;
						break;
					case "hf":
						num |= 8;
						break;
					}
				}
				if (num != 0)
				{
					_noiseBandMask = num;
				}
			}
			_machineryRaftingGeneration = Mathf.Clamp(ini.readValue("SensorData", "MachineryRaftingGeneration", 0), 0, 5);
			_machineryTonalDeficitDB = (float)_machineryRaftingGeneration * 2f;
			_tonalRPMModifier = ini.readValue("SensorData", "TonalRPMModifier", 100f);
			string text2 = ini.readValue("Physics", "TelegraphVelocities", "");
			if (!string.IsNullOrEmpty(text2))
			{
				string[] array2 = text2.Split(',');
				if (array2.Length >= 3 && float.TryParse(array2[2].Trim(), out var result))
				{
					_creepSpeedKts = result;
				}
			}
			else
			{
				_creepSpeedKts = 0f;
			}
		}
		_transientNoise = ini.readValue("SensorData", "TransientNoise", -1f);
		_transientVIDRange = ini.readValue("SensorData", "TransientVisualIdentificationRange", -1f) * 27.559496f;
		_transientSize = ini.readValue("SensorData", "TransientRCS", "");
		if (!float.TryParse(_transientSize, out _transientMaxRCS))
		{
			_transientMaxRCS = Globals.RcsValues.getRCSValue(_transientSize.ToLower());
		}
		_transientSize = Globals.RcsValues.GetSizeFromRCS(_transientMaxRCS);
		if (_transientNoise > 0f || _transientVIDRange > 0f || _transientMaxRCS > 0f)
		{
			_hasTransient = true;
		}
		_launchTransientMitigation = ini.readValue("SensorData", "LaunchTransientMitigation", 0f);
		_visibilityFromFront = ini.readValue("SensorData", "VisibilityFromFront", _visibilityFromFront);
		if (_visibilityFromFront < 0f)
		{
			_visibilityFromFront = 0.5f;
		}
		_displacement = ini.readValue("Physics", "Displacement", 0f);
	}
}
