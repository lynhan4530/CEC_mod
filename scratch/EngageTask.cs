using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeaPower;

public class EngageTask
{
	public enum SalvoType
	{
		PerLauncher,
		PerTarget,
		InTotal
	}

	public string _ammoId;

	public bool _isAutoAttack;

	public Ammunition _ammunition;

	public ObjectBase _targetObject;

	public ObjectBase _baseObject;

	public GeoPosition _targetGeoPosition;

	public LandUnitParameters.TargetPoint _curentTargetPoint;

	public float MaxRange;

	public int _shotsToFire;

	public int _priority;

	private static int _UID = 1;

	public int _uid = _UID++;

	private double _creationTime;

	public double _solutionStartTime = -1.0;

	public SensorSystem _solutionDirector;

	public WeaponSystem _solutionChannelOwner;

	public double _nextSolutionClaimTime = -1.0;

	public Queue<Vector3> _spreadPositions = new Queue<Vector3>();

	private RectTransform _rectTransformSymbol;

	private Image _symbol;

	private GameObject _symbolGO;

	public bool _isFormationAttack;

	public bool _ignoreIfUndetected;

	public bool _isTargetObjectRequired;

	public bool _requiresDatalink;

	public Vector3 TargetPosition => _targetGeoPosition.ToUnity();

	public double CreationTime => _creationTime;

	public EngageTask(string ammoId, ObjectBase targetObject, ObjectBase baseObject, int shotsToFire, int priority = 1000, bool autoAttack = false, bool isFormationAttack = false)
	{
		_isFormationAttack = isFormationAttack;
		_baseObject = baseObject;
		_ammoId = ammoId;
		_ammunition = _baseObject.getAmmunitionByName(ammoId);
		_targetObject = targetObject;
		Vector3 position = Vector3.zero;
		if (targetObject._type == ObjectBase.ObjectType.LandUnit && _ammunition._ap.GuidanceTypeForTarget(targetObject) != AmmunitionParameters.GuidanceType.AntiRadiationHoming)
		{
			bool excludeDestroyedSystems = false;
			Vehicle mapVehicle = targetObject.GetMapVehicle(_baseObject._taskforce);
			if (mapVehicle != null && mapVehicle.IsObservedByVisual)
			{
				excludeDestroyedSystems = true;
			}
			_curentTargetPoint = Utils.AssignLandUnitTargetPoint(targetObject, excludeDestroyedSystems, _baseObject);
			_baseObject._currentTargetPointDebug = _curentTargetPoint;
			position = _curentTargetPoint._position;
		}
		_targetGeoPosition.FromUnity(targetObject.transform.TransformPoint(position));
		_shotsToFire = shotsToFire;
		_creationTime = GameTime.missionElapsedTime;
		_priority = priority;
		_isAutoAttack = autoAttack;
		_isTargetObjectRequired = true;
		DM.Log($"[{baseObject.getUIDAndName()}]: <color=yellow>Engage Task</color>: {targetObject.getUIDAndName()}, {ammoId}, salvo = {shotsToFire}, priority = {priority}, auto = {autoAttack}", DM.EventLogFilters.EngageTask);
	}

	public EngageTask(string ammoId, Vector3 targetPosition, ObjectBase baseObject, int shotsToFire, int priority = 1000, bool autoAttack = false, bool isFormationAttack = false)
	{
		_isFormationAttack = isFormationAttack;
		_baseObject = baseObject;
		_ammoId = ammoId;
		_targetObject = null;
		_targetGeoPosition.FromUnity(targetPosition);
		_shotsToFire = shotsToFire;
		_creationTime = GameTime.missionElapsedTime;
		_ammunition = _baseObject.getAmmunitionByName(ammoId);
		if (_ammunition == null)
		{
			Log.Error("Trying to engage with nonexistent ammunition with name " + ammoId + "!", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\AI\\EngageTask.cs", 101, ".ctor");
		}
		_priority = priority;
		_isAutoAttack = autoAttack;
		DM.Log($"[{baseObject.getUIDAndName()}]: <color=yellow>Engage Task</color>: ({targetPosition}), {ammoId}, salvo = {shotsToFire}, priority = {priority}, auto = {autoAttack}", DM.EventLogFilters.EngageTask);
	}

	public void CalculateTorpedoSpread()
	{
		Vector3 vector = Matrix4x4.Rotate(Quaternion.AngleAxis(90f, Vector3.up)) * (TargetPosition - _baseObject.transform.position).normalized;
		float num2;
		if (_ammunition._ap.GuidanceTypeForTarget(_targetObject) != AmmunitionParameters.GuidanceType.None && _ammunition._ap.GuidanceTypeForTarget(_targetObject) != AmmunitionParameters.GuidanceType.WakeHoming)
		{
			float num = ((_ammunition._ap._seekerActiveRange > _ammunition._ap._seekerPassiveRange) ? _ammunition._ap._seekerActiveRange : _ammunition._ap._seekerPassiveRange);
			num *= 0.7f;
			num2 = 2f * num * Mathf.Atan(MathF.PI / 180f * _ammunition._ap._seekerFOV / 2f);
		}
		else
		{
			num2 = 1.190475f;
		}
		bool flag = _shotsToFire % 2 == 0;
		for (int i = 0; i < _shotsToFire; i++)
		{
			Vector3 item = ((i % 2 == 0) ? vector : (-vector));
			if (flag)
			{
				item *= (float)(i / 2) * num2 + num2 / 2f;
			}
			else if (i > 0)
			{
				item *= (float)((i - 1) / 2 + 1) * num2;
			}
			else
			{
				item = Vector3.zero;
			}
			_spreadPositions.Enqueue(item);
		}
	}

	public void SaveStateToFile(IniHandler ini, string sectionName)
	{
		ini.writeValue(sectionName, "CreationTime", _creationTime);
		ini.writeValue(sectionName, "Ammunition", _ammoId);
		ini.writeValue(sectionName, "Base", _baseObject.UniqueID);
		if (_targetObject != null)
		{
			ini.writeValue(sectionName, "Target", _targetObject.UniqueID);
		}
		else
		{
			GeoPosition geoPosition = default(GeoPosition);
			geoPosition.FromUnity(TargetPosition);
			ini.writeValue(sectionName, "TargetGeoPosition", $"{geoPosition._latitude},{geoPosition._longitude},{geoPosition._height * 220.47265625}");
		}
		ini.writeValue(sectionName, "ShotsToFire", _shotsToFire);
		ini.writeValue(sectionName, "Priority", _priority);
		ini.writeValue(sectionName, "UID", _uid);
		ini.writeValue(sectionName, "IsAutoAttack", _isAutoAttack);
		ini.writeValue(sectionName, "IsFormationAttack", _isFormationAttack);
	}

	public static EngageTask CreateFromFile(IniHandler ini, string sectionName, ObjectBase baseObject)
	{
		string ammoId = ini.readValue(sectionName, "Ammunition", "");
		ObjectBase objectBase = null;
		int defaultValue = ini.readValue(sectionName, "Target", -1);
		defaultValue = ini.readValue(sectionName, "TargetObject", defaultValue);
		if (defaultValue > 0)
		{
			objectBase = Singleton<SceneCreator>.Instance.FindObjectById(defaultValue);
		}
		Vector3 targetPosition = ini.readValue(sectionName, "TargetPosition", Vector3.zero);
		if (ini.doesKeyExist(sectionName, "TargetGeoPosition"))
		{
			double result = 0.0;
			double result2 = 0.0;
			string[] array = ini.readValue(sectionName, "TargetGeoPosition", "").Split(',');
			if (array.Length > 2)
			{
				double.TryParse(array[0], out var result3);
				double.TryParse(array[1], out result);
				double.TryParse(array[2], out result2);
				GeoPosition geoPosition = new GeoPosition
				{
					_latitude = result3,
					_longitude = result,
					_height = result2 * 0.004535709973424673
				};
				targetPosition = geoPosition.ToUnity();
			}
		}
		int shotsToFire = ini.readValue(sectionName, "ShotsToFire", 0);
		int priority = ini.readValue(sectionName, "Priority", 1000);
		bool autoAttack = ini.readValue(sectionName, "IsAutoAttack", defaultValue: false);
		bool isFormationAttack = ini.readValue(sectionName, "IsFormationAttack", defaultValue: false);
		EngageTask engageTask = null;
		engageTask = ((!(objectBase != null)) ? new EngageTask(ammoId, targetPosition, baseObject, shotsToFire, priority, autoAttack, isFormationAttack) : new EngageTask(ammoId, objectBase, baseObject, shotsToFire, priority, autoAttack, isFormationAttack));
		int num = ini.readValue(sectionName, "UID", 0);
		if (num > 0)
		{
			engageTask._uid = num;
			if (num > _UID)
			{
				_UID = num + 1;
			}
		}
		double num2 = ini.readValue(sectionName, "CreationTime", 0.0);
		if (num2 > 0.0)
		{
			engageTask._creationTime = num2;
		}
		return engageTask;
	}
}
