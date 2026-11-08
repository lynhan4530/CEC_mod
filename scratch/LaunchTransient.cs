using UnityEngine;

namespace SeaPower;

public class LaunchTransient : AI_Marker
{
	public void Init(GameObject rootObject, WeaponBase weapon)
	{
		CommonInit(rootObject);
		_aiType = AI_Type.LaunchTransient;
		_masterObject = weapon;
		setName(weapon.getUIDAndName() + " Launch Transient");
		_rootObject.name = weapon.getUIDAndName() + " Launch Transient";
		Vector3 position = _masterObject.transform.position;
		base.transform.position = position;
		setGeoPosition(Utils.worldPositionFromUnityToLongLat(base.transform.position, Globals._currentCenterTile));
		base._taskforce = weapon._taskforce;
		Singleton<ObjectsManager>.Instance.AddObjectToItsTaskforce(this);
		_expirationTime = GameTime.missionElapsedTime + 10.0;
		_3dObject.name = "Debug Sphere";
		float num = base.AltitudeOverSurface * 220.47266f;
		if (num < 0f)
		{
			_detectionKind = DetectionKind.Subsurface;
		}
		else if (num > 20f)
		{
			_detectionKind = DetectionKind.Air;
		}
		else
		{
			_detectionKind = DetectionKind.Surface;
		}
		_sensorData = new SensorData();
		if (num < 5f)
		{
			_sensorData._baseNoise = weapon._sensorData._transientNoise;
			if (weapon is Torpedo && weapon._launchPlatform?._sensorData != null)
			{
				_sensorData._baseNoise -= weapon._launchPlatform._sensorData._launchTransientMitigation;
			}
		}
		if (num > -5f)
		{
			_sensorData._visualIdentificationRange = weapon._sensorData._transientVIDRange;
			_sensorData._size = weapon._sensorData._transientSize;
			_sensorData._maxRCS = weapon._sensorData._transientMaxRCS;
			_sensorData._minRCS = weapon._sensorData._transientMaxRCS;
		}
	}

	public override void OnUpdateEveryFrame()
	{
		if (_isActive)
		{
			base.OnUpdateEveryFrame();
		}
	}

	public override void OnFixedUpdate()
	{
		if (!_isActive)
		{
			return;
		}
		base.OnFixedUpdate();
		if (base.IsDestroyed)
		{
			return;
		}
		if (DM._showTransients)
		{
			if (!_3dObject.activeSelf)
			{
				_3dObject.SetActive(value: true);
			}
		}
		else if (_3dObject.activeSelf)
		{
			_3dObject.SetActive(value: false);
		}
	}
}
