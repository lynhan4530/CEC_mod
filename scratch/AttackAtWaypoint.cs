using System.Collections.Generic;
using NodeCanvas.Framework;
using SeapowerUI;
using UnityEngine;

namespace SeaPower;

public class AttackAtWaypoint : GoToWaypointTask
{
	public ObjectBase _targetObject;

	public bool _hasTargetObject;

	public GeoPosition _targetGeoPosition;

	public int _salvo;

	public string _ammunitionName;

	public Ammunition _ammo;

	public float _altOffset = 0.1814284f;

	public BBParameter<float> _minAltitude;

	public BBParameter<float> _maxAltitude;

	public float _triggerRange;

	public BBParameter<float> _targetAltitude;

	public bool _isSonobouyDrop;

	private bool _isEngageTriggered;

	private bool _isCompleted;

	public bool _engageAtWaypointPosition;

	public EngageTask.SalvoType _salvoType;

	public float _areaRadius = -1f;

	public bool _formationAttack = true;

	public bool _attackOnlyDetected;

	public bool _engageTaskCanAttackUndetectedTargets;

	protected override void OnUpdate()
	{
		if (_isCompleted || GameTime.IsPaused())
		{
			return;
		}
		if (_hasTargetObject && _targetObject == null)
		{
			OnFinished();
			EndAction(success: true);
			_isCompleted = true;
			return;
		}
		if (_baseObject.IsAirUnit)
		{
			if (_baseObject.DesiredAltitude.Value < _minAltitude.value)
			{
				_baseObject.DesiredAltitude.Value = _minAltitude.value + Globals._weaponLaunchAltitudeOffset;
			}
			else if (_baseObject.DesiredAltitude.Value > _maxAltitude.value)
			{
				_baseObject.DesiredAltitude.Value = _maxAltitude.value + Globals._weaponLaunchAltitudeOffset;
			}
			if (_ammo._ap._type == Ammunition.Type.Bomb)
			{
				float num = (float)(_baseObject.Position.Value._height - (double)_targetAltitude.value) * 67.200066f;
				if (num < 0f)
				{
					num = 0f;
				}
				_triggerRange = 12f + _baseObject._velocityInKnots * 0.0076554087f * Mathf.Sqrt(2f * num / 9.8f);
				if (_ammo._ap._guidanceType != AmmunitionParameters.GuidanceType.None && !_ammo._ap._midCourseCorrection.AllowsLauncherGuidance)
				{
					ObjectBase.EngagementRange maxEngagementRanges = _baseObject.GetMaxEngagementRanges(_targetObject, _ammo, requireOpenSensorChannel: false, shortCircuitTerrainCheck: true, usePreciseKinematics: false, _waypointGeoPos.value.ToUnity());
					float b = Utils.SlantToHorizontalRange(maxEngagementRanges.SensorRange, (float)_baseObject.Position.Value._height - Mathf.Max(_targetAltitude.value, 0f));
					float num2 = 12f + Mathf.Min(maxEngagementRanges.KinematicRange, b);
					if (_triggerRange > num2)
					{
						_triggerRange = num2;
					}
				}
				if (GameTime.TimeCompression > 25f)
				{
					_triggerRange += 20f;
				}
				if (GameTime.TimeCompression > 50f)
				{
					_triggerRange += 120f;
				}
			}
		}
		if (_baseObject.Position.Value.UnityDistance(_waypointGeoPos.value) < _triggerRange && !_isEngageTriggered)
		{
			_isEngageTriggered = true;
			AttackCalculations();
			OnFinished();
			EndAction(success: true);
			_isCompleted = true;
		}
		else
		{
			base.OnUpdate();
		}
	}

	private void AttackCalculations()
	{
		List<ObjectBase> list = new List<ObjectBase>();
		if (_targetObject != null && !_targetObject.IsDestroyed && _targetObject.HasControl)
		{
			list.Add(_targetObject);
		}
		if (_areaRadius > 0.1f)
		{
			Vector3 vector = _targetGeoPosition.ToUnity();
			if (_targetObject != null)
			{
				vector = _targetObject.transform.position;
			}
			foreach (ObjectBase listOfAllUnit in Singleton<ObjectsManager>.Instance._listOfAllUnits)
			{
				if (!listOfAllUnit.HasControl || listOfAllUnit.IsDestroyed)
				{
					continue;
				}
				if (_targetObject != null)
				{
					if (listOfAllUnit == _targetObject || listOfAllUnit._taskforce != _targetObject._taskforce || (_baseObject.Formation != null && listOfAllUnit.Formation != null && _baseObject.Formation == listOfAllUnit.Formation))
					{
						continue;
					}
				}
				else if (_baseObject._taskforce.RelationshipTo(listOfAllUnit._taskforce) != RelationsState.Hostile)
				{
					continue;
				}
				if ((!_attackOnlyDetected || listOfAllUnit.GetMapVehicle(_baseObject._taskforce) != null) && _baseObject.DoesAmmoMatchTarget(_ammunitionName, listOfAllUnit) && !((vector - listOfAllUnit.transform.position).magnitude > _areaRadius))
				{
					list.Add(listOfAllUnit);
				}
			}
		}
		if (_hasTargetObject && list.Count == 0)
		{
			return;
		}
		int totalShotsToFire = 0;
		int num = 0;
		List<ObjectBase> list2 = new List<ObjectBase>();
		if (_formationAttack && _baseObject.IsFormationLeader && _baseObject.Formation.SelectedControlMode != UnitFormation.ControlMode.Local)
		{
			for (int num2 = _baseObject.Formation.Stations.Count - 1; num2 >= 0; num2--)
			{
				Station station = _baseObject.Formation.Stations[num2];
				if (station != _baseObject.Formation.LeaderStation && !(station.UnitObject == null) && !station.UnitObject.IsDestroyed && station.UnitObject.FollowingFormation && station.UnitObject.getAmmunitionAmountByName(_ammunitionName) >= 1 && !(station.UnitObject is Aircraft { _isInFlight: false }) && !(station.UnitObject is Helicopter { _isInFlight: false }))
				{
					list2.Add(station.UnitObject);
					num += GetAvailableAmmoForAttack(station.UnitObject);
				}
			}
			if (!_baseObject.IsDestroyed && _baseObject.getAmmunitionAmountByName(_ammunitionName) > 0)
			{
				list2.Add(_baseObject);
				num += GetAvailableAmmoForAttack(_baseObject);
			}
		}
		else
		{
			num = GetAvailableAmmoForAttack(_baseObject);
		}
		if (list2.Count == 1 && list2[0] == _baseObject)
		{
			list2.Clear();
		}
		if (list2.Count > 0)
		{
			if (list.Count > 0)
			{
				if (_ammo._ap._type == Ammunition.Type.Projectile || _ammo._ap._type == Ammunition.Type.AerialRocket || _ammo._ap._type == Ammunition.Type.MLRS)
				{
					int num3 = 0;
					if (list.Count == 1)
					{
						foreach (ObjectBase item in list2)
						{
							SingleAttack(item, list[0], 1);
						}
						return;
					}
					{
						foreach (ObjectBase item2 in list2)
						{
							if (num3 < list.Count - 1)
							{
								for (int i = num3; i < list.Count; i++)
								{
									SingleAttack(item2, list[i], 1);
								}
							}
							if (num3 > 0)
							{
								for (int j = 0; j < num3; j++)
								{
									SingleAttack(item2, list[j], 1);
								}
							}
							num3++;
							if (num3 > list.Count - 1)
							{
								num3 = 0;
							}
						}
						return;
					}
				}
				if (_salvoType == EngageTask.SalvoType.PerLauncher)
				{
					totalShotsToFire = _salvo * list2.Count;
				}
				else if (_salvoType == EngageTask.SalvoType.PerTarget)
				{
					totalShotsToFire = _salvo * list.Count;
				}
				else if (_salvoType == EngageTask.SalvoType.InTotal)
				{
					totalShotsToFire = _salvo;
				}
				if (totalShotsToFire > 0)
				{
					if (totalShotsToFire > num)
					{
						totalShotsToFire = num;
					}
					int num4 = totalShotsToFire / list.Count;
					if (num4 == 0)
					{
						num4 = 1;
					}
					DistributeTargets(list2, list, num4, ref totalShotsToFire);
					if (totalShotsToFire > 0)
					{
						DistributeTargets(list2, list, 1, ref totalShotsToFire);
					}
				}
				return;
			}
			if (_salvoType == EngageTask.SalvoType.PerLauncher)
			{
				foreach (ObjectBase item3 in list2)
				{
					SingleAttack(item3, null, _salvo);
				}
				return;
			}
			if (_salvoType != EngageTask.SalvoType.PerTarget && _salvoType != EngageTask.SalvoType.InTotal)
			{
				return;
			}
			int num5 = _salvo;
			{
				foreach (ObjectBase item4 in list2)
				{
					int availableAmmoForAttack = GetAvailableAmmoForAttack(item4);
					int num6 = num5;
					if (num6 > availableAmmoForAttack)
					{
						num6 = availableAmmoForAttack;
					}
					SingleAttack(item4, null, num6);
					num5 -= num6;
					if (num5 < 1)
					{
						break;
					}
				}
				return;
			}
		}
		if (list.Count > 0)
		{
			totalShotsToFire = _salvo;
			if (totalShotsToFire > num)
			{
				totalShotsToFire = num;
			}
			if (totalShotsToFire == 0)
			{
				totalShotsToFire = 1;
			}
			int num4 = list.Count / totalShotsToFire;
			if (num4 == 0)
			{
				num4 = 1;
			}
			if (_salvoType == EngageTask.SalvoType.PerLauncher || _salvoType == EngageTask.SalvoType.PerTarget)
			{
				foreach (ObjectBase item5 in list)
				{
					SingleAttack(_baseObject, item5, _salvo);
				}
				return;
			}
			if (_salvoType != EngageTask.SalvoType.InTotal)
			{
				return;
			}
			foreach (ObjectBase item6 in list)
			{
				SingleAttack(_baseObject, item6, num4);
				totalShotsToFire -= num4;
				if (totalShotsToFire < 1)
				{
					break;
				}
			}
			if (totalShotsToFire <= 0)
			{
				return;
			}
			num4 = list.Count / totalShotsToFire;
			if (num4 == 0)
			{
				num4 = 1;
			}
			foreach (ObjectBase item7 in list)
			{
				SingleAttack(_baseObject, item7, num4);
				totalShotsToFire -= num4;
				if (totalShotsToFire < 1)
				{
					break;
				}
			}
			if (totalShotsToFire > 0)
			{
				SingleAttack(_baseObject, list[0], totalShotsToFire);
			}
		}
		else
		{
			SingleAttack(_baseObject, null, _salvo);
		}
	}

	private void SingleAttack(ObjectBase baseObject, ObjectBase targetObject, int shotsToFire)
	{
		string text = "null";
		EngageTask engageTask = null;
		if (targetObject != null)
		{
			text = targetObject.getUIDAndName();
			engageTask = baseObject.InsertEngageTask(_ammunitionName, targetObject, Vector3.zero, shotsToFire, baseObject.GetEngagePriority(targetObject));
		}
		else
		{
			Vector3 targetPosition = ((!_engageAtWaypointPosition) ? Utils.longLatToLocalV3(_targetGeoPosition, Globals._currentCenterTile) : Utils.longLatToLocalV3(_waypointGeoPos.value, Globals._currentCenterTile));
			targetPosition.y = _targetAltitude.value;
			engageTask = new EngageTask(_ammunitionName, targetPosition, baseObject, shotsToFire);
			baseObject.AddEngageTask(engageTask);
		}
		if (_engageTaskCanAttackUndetectedTargets)
		{
			engageTask._ignoreIfUndetected = true;
		}
		string text2 = ((shotsToFire > 1) ? $" Salvo = {shotsToFire}" : "");
		DM.I($"Created task: Ammo: {baseObject._selectedAmmunitionForEngage} uid = {engageTask._uid} target = {text}{text2}");
	}

	private void DistributeTargets(List<ObjectBase> formationMembers, List<ObjectBase> targetObjects, int shotsPerTarget, ref int totalShotsToFire)
	{
		Dictionary<ObjectBase, int> dictionary = new Dictionary<ObjectBase, int>();
		foreach (ObjectBase targetObject in targetObjects)
		{
			dictionary.Add(targetObject, shotsPerTarget);
		}
		foreach (ObjectBase formationMember in formationMembers)
		{
			int availableAmmoForAttack = GetAvailableAmmoForAttack(formationMember);
			int num = shotsPerTarget;
			if (num > availableAmmoForAttack)
			{
				num = availableAmmoForAttack;
			}
			if (num > _salvo)
			{
				num = _salvo;
			}
			foreach (ObjectBase targetObject2 in targetObjects)
			{
				if (dictionary[targetObject2] >= 1)
				{
					int num2 = num;
					if (num2 > dictionary[targetObject2])
					{
						num2 = dictionary[targetObject2];
					}
					dictionary[targetObject2] -= num2;
					num -= num2;
					totalShotsToFire -= num2;
					SingleAttack(formationMember, targetObject2, num2);
					if (num < 1 || totalShotsToFire < 1)
					{
						break;
					}
				}
			}
			if (totalShotsToFire < 1)
			{
				break;
			}
		}
	}

	public void UpdateSalvoType(EngageTask.SalvoType salvoType)
	{
		_salvoType = salvoType;
	}

	private bool TreatAmmoAsSingleAttack()
	{
		if (_ammo._ap._type != Ammunition.Type.AerialRocket && _ammo._ap._type != Ammunition.Type.MLRS)
		{
			if (_ammo._ap._type == Ammunition.Type.Bomb)
			{
				return _ammo._ap._subType == Ammunition.Type.Sonobuoy;
			}
			return false;
		}
		return true;
	}

	private int GetAvailableAmmoForAttack(ObjectBase unit)
	{
		if (TreatAmmoAsSingleAttack())
		{
			return 1;
		}
		return unit.getAmmunitionAmountByName(_ammunitionName);
	}

	public void UpdateSalvoAmount(int salvoAmount)
	{
		_salvo = salvoAmount;
	}

	public void UpdateFormationAttack(bool formationAttack)
	{
		_formationAttack = formationAttack;
	}

	public void UpdateAreaRadius(float areaRadius)
	{
		_areaRadius = areaRadius;
	}

	public override ITaskViewModel MakeViewModel()
	{
		return new AttackWaypointViewModel(this);
	}
}
