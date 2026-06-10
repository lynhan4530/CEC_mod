using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;

namespace SeaPower;

public class SensorSystemsLink
{
	private static readonly ProfilerMarker<int> _profVisualSystems = new ProfilerMarker<int>("Visual Systems", "Count");

	private static readonly ProfilerMarker<int> _profMADSystems = new ProfilerMarker<int>("MAD Systems", "Count");

	private static readonly ProfilerMarker<int> _profRadarSystems = new ProfilerMarker<int>("Radar Systems", "Count");

	private static readonly ProfilerMarker<int> _profSonarSystems = new ProfilerMarker<int>("Sonar Systems", "Count");

	private static readonly ProfilerMarker<int> _profESMSystems = new ProfilerMarker<int>("ESM Systems", "Count");

	private static readonly ProfilerMarker<int> _profSonarSystems2ndPass = new ProfilerMarker<int>("Sonar Systems 2nd Pass", "Count");

	private List<Radar> _radarSystems = new List<Radar>();

	private List<Sonar> _sonarSystems = new List<Sonar>();

	private List<ESM> _esmSystems = new List<ESM>();

	private List<SensorSystemVisual> _visualSystems = new List<SensorSystemVisual>();

	private List<SensorSystemMAD> _MADSystems = new List<SensorSystemMAD>();

	private List<SensorSystemLaserDesignator> _laserDesignators = new List<SensorSystemLaserDesignator>();

	private List<WeaponBase> _weaponsList = new List<WeaponBase>();

	private List<int> _idsToRemove = new List<int>();

	private List<ObjectBase> _dpcList = new List<ObjectBase>();

	private HashSet<ObjectBase> _sscSet = new HashSet<ObjectBase>();

	private HashSet<ObjectBase> _vscSet = new HashSet<ObjectBase>();

	public float _updateRate;

	public Dictionary<ObjectBase, float> _detectedPlayerContacts = new Dictionary<ObjectBase, float>();

	public bool _onAlert;

	private Dictionary<object, float> _updateTimes = new Dictionary<object, float>();

	private Dictionary<object, float> _secondUpdateTimes = new Dictionary<object, float>();

	private HashSet<object> _hasFirstUpdate = new HashSet<object>();

	private HashSet<object> _hasSecondUpdate = new HashSet<object>();

	private Taskforce _baseTaskforce;

	private float _revealUpdateTime = -1f;

	public Dictionary<ObjectBase, float> _unitsToRevealOverride = new Dictionary<ObjectBase, float>();

	private List<ObjectBase> _unitsToRevealToRemove = new List<ObjectBase>();

	public SensorSystemsLink(Taskforce baseTaskforce)
	{
		_baseTaskforce = baseTaskforce;
		IniHandler iniHandler = Utils.openVirtualIniFile("systems", "sensors", "sensors");
		_updateRate = iniHandler.readValue("General", "DataLinkUpdateRate", 1f);
	}

	public string GetSensorsCountInfo()
	{
		return $"{_visualSystems.Count + _radarSystems.Count + _sonarSystems.Count + _esmSystems.Count + _MADSystems.Count} ({_visualSystems.Count} V, {_radarSystems.Count} R, {_sonarSystems.Count} S, {_esmSystems.Count} E, {_MADSystems.Count} M)";
	}

	public void SetupUpdate()
	{
		_hasFirstUpdate.Clear();
		_hasSecondUpdate.Clear();
		_updateTimes.Clear();
		_secondUpdateTimes.Clear();
		int num = _visualSystems.Count + _MADSystems.Count + _radarSystems.Count + _sonarSystems.Count + _esmSystems.Count + _sonarSystems.Count;
		int num2 = 0;
		foreach (SensorSystemVisual visualSystem in _visualSystems)
		{
			_updateTimes[visualSystem] = (float)num2++ / (float)num;
		}
		foreach (SensorSystemMAD mADSystem in _MADSystems)
		{
			_updateTimes[mADSystem] = (float)num2++ / (float)num;
		}
		foreach (Radar radarSystem in _radarSystems)
		{
			_updateTimes[radarSystem] = (float)num2++ / (float)num;
		}
		foreach (Sonar sonarSystem in _sonarSystems)
		{
			_updateTimes[sonarSystem] = (float)num2++ / (float)num;
		}
		foreach (ESM esmSystem in _esmSystems)
		{
			_updateTimes[esmSystem] = (float)num2++ / (float)num;
		}
		foreach (Sonar sonarSystem2 in _sonarSystems)
		{
			_secondUpdateTimes[sonarSystem2] = (float)num2++ / (float)num;
		}
	}

	private bool ShouldFirstUpdate(float percentageAlong, object system)
	{
		if (_hasFirstUpdate.Contains(system))
		{
			return false;
		}
		if (_updateTimes.GetValueOrDefault(system, 0f) > percentageAlong)
		{
			return false;
		}
		_hasFirstUpdate.Add(system);
		return true;
	}

	private void FirstUpdate(float percentageAlong, object system, Action update)
	{
		if (!_hasFirstUpdate.Contains(system) && !(_updateTimes.GetValueOrDefault(system, 0f) > percentageAlong))
		{
			_hasFirstUpdate.Add(system);
			update();
		}
	}

	private bool ShouldSecondUpdate(float percentageAlong, object system)
	{
		if (GameTime.time - Singleton<SceneCreator>.Instance._missionStartTime < 3f)
		{
			return false;
		}
		if (_hasSecondUpdate.Contains(system))
		{
			return false;
		}
		if (_secondUpdateTimes.GetValueOrDefault(system, 0f) > percentageAlong)
		{
			return false;
		}
		_hasSecondUpdate.Add(system);
		return true;
	}

	private void SecondUpdate(float percentageAlong, object system, Action update)
	{
		if (!(GameTime.time - Singleton<SceneCreator>.Instance._missionStartTime < 3f) && !_hasSecondUpdate.Contains(system) && !(_secondUpdateTimes.GetValueOrDefault(system, 0f) > percentageAlong))
		{
			_hasSecondUpdate.Add(system);
			update();
		}
	}

	public void OnUpdate(float percentageAlong)
	{
		if (GameTime.time - Singleton<SceneCreator>.Instance._missionStartTime < 3f)
		{
			return;
		}
		if (_baseTaskforce._taskforceUnits.Count > 0 && GameTime.time > _revealUpdateTime)
		{
			_revealUpdateTime = GameTime.time + Utils.RandomFloat(1f, 2f);
			_unitsToRevealToRemove.Clear();
			foreach (KeyValuePair<ObjectBase, float> item in _unitsToRevealOverride)
			{
				Utils.RevealContactToObject(_baseTaskforce._taskforceUnits[0], item.Key, "None", 0);
				if (item.Value > -0.1f && item.Value < GameTime.time)
				{
					_unitsToRevealToRemove.Add(item.Key);
				}
			}
			foreach (ObjectBase item2 in _unitsToRevealToRemove)
			{
				_unitsToRevealOverride.Remove(item2);
			}
		}
		foreach (SensorSystemVisual visualSystem in _visualSystems)
		{
			if (ShouldFirstUpdate(percentageAlong, visualSystem) && visualSystem.CanProduceContacts)
			{
				visualSystem.runVisualScan();
			}
		}
		foreach (SensorSystemMAD mADSystem in _MADSystems)
		{
			if (ShouldFirstUpdate(percentageAlong, mADSystem) && mADSystem.CanProduceContacts)
			{
				mADSystem.seekMADContacts();
			}
		}
		foreach (Radar radarSystem in _radarSystems)
		{
			if (radarSystem._parentRadarSystem.CanProduceContacts && ShouldFirstUpdate(percentageAlong, radarSystem))
			{
				radarSystem.CalculateRawSensorContacts();
				radarSystem.calculateClusteredSensorContacts();
			}
		}
		foreach (Sonar sonarSystem in _sonarSystems)
		{
			if (sonarSystem._parentSonarSystem.CanProduceContacts && !(sonarSystem._parentSonarSystem._mountObject.transform.position.y > 0f) && (!Globals._useDatalinks || sonarSystem._sp._hasDataLink) && ShouldFirstUpdate(percentageAlong, sonarSystem))
			{
				if (!sonarSystem._parentSonarSystem.IsWithinTransmitterRange())
				{
					return;
				}
				sonarSystem.calculateActiveContacts();
			}
		}
		foreach (ESM esmSystem in _esmSystems)
		{
			if (!ShouldFirstUpdate(percentageAlong, esmSystem))
			{
				continue;
			}
			bool flag = true;
			if (!esmSystem._parentESMSystem.CanProduceContacts)
			{
				flag = false;
			}
			else if (Globals._useDatalinks && !esmSystem._ep._hasDataLink)
			{
				flag = false;
			}
			if (flag)
			{
				esmSystem.createPassiveESMContacts(_updateRate);
			}
			foreach (KeyValuePair<SensorSystem, ESM.ESMDataForSystem> esmObservationDatum in esmSystem._esmObservationData)
			{
				esmObservationDatum.Value._observationTime -= _updateRate;
			}
			SensorSystem[] array = esmSystem._esmObservationData.Keys.ToArray();
			foreach (SensorSystem sensorSystem in array)
			{
				if (sensorSystem._baseObject == null || sensorSystem._baseObject.IsDestroyed || esmSystem._esmObservationData[sensorSystem]._observationTime < 0f)
				{
					esmSystem._esmObservationData.Remove(sensorSystem);
				}
			}
		}
		foreach (Sonar sonarSystem2 in _sonarSystems)
		{
			if (!sonarSystem2._parentSonarSystem.CanProduceContacts || sonarSystem2._parentSonarSystem._mountObject.transform.position.y > 0f || sonarSystem2._sp._isActiveOnly || (Globals._useDatalinks && !sonarSystem2._sp._hasDataLink) || !ShouldSecondUpdate(percentageAlong, sonarSystem2))
			{
				continue;
			}
			if (!sonarSystem2._parentSonarSystem.IsWithinTransmitterRange())
			{
				break;
			}
			sonarSystem2.calculatePassiveContacts();
			sonarSystem2._parentSonarSystem._detectedObjects.Clear();
			foreach (List<SensorEcho> intersectedEcho in sonarSystem2.IntersectedEchoes)
			{
				if (!sonarSystem2._parentSonarSystem._detectedObjects.Contains(intersectedEcho[0]._objectBase))
				{
					sonarSystem2._parentSonarSystem._detectedObjects.Add(intersectedEcho[0]._objectBase);
				}
			}
			foreach (SensorEcho rawPassiveSonarReading in sonarSystem2.RawPassiveSonarReadings)
			{
				if (!sonarSystem2._parentSonarSystem._detectedObjects.Contains(rawPassiveSonarReading._objectBase))
				{
					sonarSystem2._parentSonarSystem._detectedObjects.Add(rawPassiveSonarReading._objectBase);
				}
			}
		}
	}

	public SensorSystemLaserDesignator findLaserDesignatorFromTaskForceLink(ObjectBase targetObject, WeaponBase ammoObject)
	{
		SensorSystemLaserDesignator result = null;
		foreach (SensorSystemLaserDesignator laserDesignator in _laserDesignators)
		{
			if (laserDesignator != null && laserDesignator._associatedTargets.Contains(targetObject) && laserDesignator.rangeTargetWithLaser(targetObject, ammoObject))
			{
				result = laserDesignator;
				ammoObject.CheckAndSetLaserDesignatorOnTheLaunchPlatform(laserDesignator._baseObject);
				break;
			}
		}
		return result;
	}

	public SensorSystemLaserDesignator findLaserDesignatorFromTaskForceLink(ObjectBase targetObject)
	{
		SensorSystemLaserDesignator result = null;
		foreach (SensorSystemLaserDesignator laserDesignator in _laserDesignators)
		{
			if (laserDesignator != null && laserDesignator._associatedTargets.Contains(targetObject) && laserDesignator.rangeTargetWithLaser(targetObject))
			{
				result = laserDesignator;
				break;
			}
		}
		return result;
	}

	public void AddSensor<T>(T sensor)
	{
		if (sensor is SensorSystemVisual item && !_visualSystems.Contains(item))
		{
			_visualSystems.Add(item);
		}
		else if (sensor is SensorSystemRadar sensorSystemRadar && !_radarSystems.Contains(sensorSystemRadar._radar))
		{
			_radarSystems.Add(sensorSystemRadar._radar);
		}
		else if (sensor is SensorSystemSonar sensorSystemSonar && !_sonarSystems.Contains(sensorSystemSonar._sonar))
		{
			_sonarSystems.Add(sensorSystemSonar._sonar);
		}
		else if (sensor is SensorSystemESM sensorSystemESM && !_esmSystems.Contains(sensorSystemESM._esm))
		{
			_esmSystems.Add(sensorSystemESM._esm);
		}
		else if (sensor is SensorSystemMAD item2 && !_MADSystems.Contains(item2))
		{
			_MADSystems.Add(item2);
		}
		else if (sensor is SensorSystemLaserDesignator item3 && !_laserDesignators.Contains(item3))
		{
			_laserDesignators.Add(item3);
		}
		else if (sensor is Radar item4 && !_radarSystems.Contains(item4))
		{
			_radarSystems.Add(item4);
		}
		else if (sensor is Sonar item5 && !_sonarSystems.Contains(item5))
		{
			_sonarSystems.Add(item5);
		}
	}

	public void RemoveSensor<T>(T sensor)
	{
		if (sensor is SensorSystemVisual item && _visualSystems.Contains(item))
		{
			_visualSystems.Remove(item);
		}
		else if (sensor is SensorSystemRadar sensorSystemRadar && _radarSystems.Contains(sensorSystemRadar._radar))
		{
			_radarSystems.Remove(sensorSystemRadar._radar);
		}
		else if (sensor is SensorSystemSonar sensorSystemSonar && _sonarSystems.Contains(sensorSystemSonar._sonar))
		{
			_sonarSystems.Remove(sensorSystemSonar._sonar);
		}
		else if (sensor is SensorSystemESM sensorSystemESM && _esmSystems.Contains(sensorSystemESM._esm))
		{
			_esmSystems.Remove(sensorSystemESM._esm);
		}
		else if (sensor is SensorSystemMAD item2 && _MADSystems.Contains(item2))
		{
			_MADSystems.Remove(item2);
		}
		else if (sensor is SensorSystemLaserDesignator item3 && _laserDesignators.Contains(item3))
		{
			_laserDesignators.Remove(item3);
		}
		else if (sensor is Radar item4 && _radarSystems.Contains(item4))
		{
			_radarSystems.Remove(item4);
		}
		else if (sensor is Sonar item5 && _sonarSystems.Contains(item5))
		{
			_sonarSystems.Remove(item5);
		}
	}
}
