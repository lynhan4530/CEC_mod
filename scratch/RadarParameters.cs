using UnityEngine;

namespace SeaPower;

public class RadarParameters
{
	public enum Role
	{
		Undefined,
		Surface,
		Air,
		SurfaceAndAir
	}

	public string _systemName;

	public bool _mountRotatable;

	public bool _radarRotatable;

	public bool _hasDataLink;

	public float _minRange;

	public float _maxRange;

	public float _minAltitude;

	public float _maxAltitude;

	public float _rangeResolution;

	public float _beamWidth;

	public float _gain;

	public float _gainFactor;

	public bool _canDetectPeriscope;

	public bool _canDetectLandTargets;

	public float _peakPower;

	public Globals.Frequency _frequency;

	public float _waveLength;

	public Role _role;

	public RadarParameters(string systemName)
	{
		_systemName = systemName;
		IniHandler iniHandler = Utils.openVirtualIniFile("systems", "sensors", "sensors");
		_mountRotatable = iniHandler.readValue(systemName, "MountRotatable", defaultValue: true);
		_radarRotatable = iniHandler.readValue(systemName, "RadarRotatable", defaultValue: true);
		_hasDataLink = iniHandler.readValue(systemName, "HasDataLink", defaultValue: false);
		_minRange = iniHandler.readValue(systemName, "MinRange", 0f) * 1000f * 0.0148809375f;
		_maxRange = iniHandler.readValue(systemName, "MaxRange", 25f) * 1000f * 0.0148809375f;
		_minAltitude = iniHandler.readValue(systemName, "MinAltitude", 0f) * 0.0148809375f;
		_maxAltitude = iniHandler.readValue(systemName, "MaxAltitude", 30480f) * 0.0148809375f;
		_rangeResolution = iniHandler.readValue(systemName, "RangeResolution", 100f);
		_beamWidth = iniHandler.readValue(systemName, "BeamWidth", 1f);
		_gain = iniHandler.readValue(systemName, "Gain", 0f);
		_gainFactor = Mathf.Pow(10f, _gain / 10f);
		_peakPower = iniHandler.readValue(systemName, "PeakPower", 100f);
		_canDetectPeriscope = iniHandler.readValue(systemName, "CanDetectPeriscope", defaultValue: false);
		_canDetectLandTargets = iniHandler.readValue(systemName, "CanDetectLandTargets", defaultValue: false);
		switch (iniHandler.readValue(systemName, "Frequency", "").ToLower())
		{
		case "vhf-band":
			_frequency = Globals.Frequency.VHF_Band;
			break;
		case "uhf-band":
			_frequency = Globals.Frequency.UHF_Band;
			break;
		case "l-band":
			_frequency = Globals.Frequency.L_Band;
			break;
		case "s-band":
			_frequency = Globals.Frequency.S_Band;
			break;
		case "c-band":
			_frequency = Globals.Frequency.C_Band;
			break;
		case "x-band":
			_frequency = Globals.Frequency.X_Band;
			break;
		case "ku-band":
			_frequency = Globals.Frequency.Ku_Band;
			break;
		case "k-band":
			_frequency = Globals.Frequency.K_Band;
			break;
		case "ka-band":
			_frequency = Globals.Frequency.Ka_Band;
			break;
		default:
			_frequency = Globals.Frequency.Undefined;
			break;
		}
		Globals._waveLengthsForFrequencyBands.TryGetValue(_frequency, out var value);
		_waveLength = value;
		switch (iniHandler.readValue(systemName, "Role", "").ToLower())
		{
		case "surface":
			_role = Role.Surface;
			break;
		case "air":
			_role = Role.Air;
			break;
		case "airandsurface":
			_role = Role.SurfaceAndAir;
			break;
		default:
			_role = Role.Undefined;
			break;
		}
	}
}
