using UnityEngine;

namespace SeaPower;

public class SemiActiveRadarSeeker : SeekerBase
{
	private SensorSystemRadar _guidingRadar;

	private ObjectBase _targetObject;

	public SemiActiveRadarSeeker()
	{
		_guidanceType = AmmunitionParameters.GuidanceType.SemiActiveRadarHoming;
	}

	public override void setGuidingSystem(SensorSystemRadar guidingRadar)
	{
		_guidingRadar = guidingRadar;
	}

	public override void setCurrentTarget(ObjectBase targetObject)
	{
		_targetObject = targetObject;
	}

	public override bool isInRange(Vector3 targetPosition)
	{
		if (!_ammoObject.isLaunched())
		{
			return false;
		}
		if (_ap._associatedSensors == null)
		{
			Log.Error(_ammoObject.FullIDData() + " has NO associated sensors!", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Guidance\\SemiActiveRadarSeeker.cs", 34, "isInRange");
			return false;
		}
		if (_ap._associatedSensors.Count == 0)
		{
			Log.Error(_ap._ammunitionFileName + " associated sensors count is 0!", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Guidance\\SemiActiveRadarSeeker.cs", 41, "isInRange");
			return false;
		}
		if (_guidingRadar != null)
		{
			if (_guidingRadar.Inoperable.Value)
			{
				return false;
			}
			_mountObject = _guidingRadar.getRadarMountObject();
			if (_mountObject == null)
			{
				return false;
			}
			Vector3 vector = _mountObject.transform.TransformPoint(_mountObject.transform.localPosition);
			float magnitude = (targetPosition - vector).magnitude;
			Vector3 unityPosition = _ammoObject.getUnityPosition();
			float magnitude2 = (targetPosition - unityPosition).magnitude;
			float num = _ap._seekerPassiveRange;
			if (_targetObject != null && _targetObject is Missile missile && Utils.HorizontalAngleToTarget(missile.transform.forward, missile.transform.position, _ammoObject.transform.position) < 30f)
			{
				num += missile._velocityInUnity * _ammoObject._ap._timeToMaxRange;
			}
			if (magnitude < _guidingRadar._radar._rp._maxRange && magnitude2 < num)
			{
				return true;
			}
			return false;
		}
		Log.DebugWarning("Not a radar associated with SARH ammunition", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Guidance\\SemiActiveRadarSeeker.cs", 60, "isInRange");
		return false;
	}

	protected override void createListOfPotentialContacts()
	{
		if (!_ammoObject.ConnectionLost.Value && _targetObject != null && !(_ammoObject == _targetObject) && isValidTarget(_targetObject) && isDetectable(_targetObject) && !_ignoredObjects.Contains(_targetObject) && (_mode != Ammunition.SeekerMode.Default || !_ap._defaultIgnoredTypes.Contains(_targetObject._type)) && (_mode != Ammunition.SeekerMode.LandUnit || _targetObject is LandUnit))
		{
			_potentialContacts.Add(_targetObject);
		}
	}

	protected override void searchForTarget()
	{
		_contactsInRange.Clear();
		SensorEcho strongestSensorEcho = _strongestSensorEcho;
		_lockedOnTarget = null;
		_strongestSensorEcho = null;
		if (_ammoObject.ConnectionLost.Value)
		{
			return;
		}
		if (_potentialContacts.Count > 0)
		{
			TryToLockOnTarget();
		}
		if (_strongestSensorEcho == null)
		{
			if (_ap._secondaryPassiveRadarGuidanceType != AmmunitionParameters.SecondaryPassiveRadarGuidanceType.None)
			{
				searchPassiveRadioEmissionTarget();
			}
			if (_strongestSensorEcho == null && GameTime.time - _targetLastSeenTime < 2f)
			{
				_strongestSensorEcho = strongestSensorEcho;
			}
		}
		else if (strongestSensorEcho == null || _strongestSensorEcho._objectBase != strongestSensorEcho._objectBase)
		{
			OffsetStrongestEchoPosition();
		}
		else if (_strongestSensorEcho._objectBase == strongestSensorEcho._objectBase)
		{
			_strongestSensorEcho._localOffset = strongestSensorEcho._localOffset;
		}
	}

	private void TryToLockOnTarget()
	{
		if (isInSensorFOV(_ammoObject.transform.position, _ammoObject.transform.forward, _targetObject))
		{
			_contactsInRange.Add(_targetObject);
			if (_targetObject._sensorData == null)
			{
				Log.Error($"No sensor data for object {_targetObject.name} type: {_targetObject.GetType()}", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Guidance\\SemiActiveRadarSeeker.cs", 170, "TryToLockOnTarget");
				return;
			}
			SensorEcho freeInstance = Globals._sensorEchoes.getFreeInstance();
			freeInstance._objectUID = _targetObject.UniqueID;
			freeInstance._objectBase = _targetObject;
			freeInstance._unityPosition = _targetObject.transform.position;
			freeInstance._unitySpeedVector = _targetObject.UnityVelocityVector;
			_strongestSensorEcho = freeInstance;
			_lockedOnTarget = _targetObject;
			_targetLastSeenTime = GameTime.time;
		}
	}
}
