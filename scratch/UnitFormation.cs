using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Noesis;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UniRx;
using UnityEngine;

namespace SeaPower;

public class UnitFormation : INotifyPropertyChanged, IDragDropCompleted, IContextMenuObject
{
	public enum FormationPattern
	{
		None,
		Loose,
		Circle,
		Vic,
		Echelon,
		LineAbreast,
		LineAstern,
		Box,
		Convoy,
		Battlegroup
	}

	public enum FormationSpacing
	{
		Parade,
		Tight,
		Spread,
		Loose
	}

	public enum ControlMode
	{
		Local,
		FollowLeader,
		Synchronized
	}

	public enum AttackFocus
	{
		Concentrated,
		Distributed
	}

	private string _name;

	private bool _externalNameSet;

	private string _internalName;

	private Station _leaderStation;

	private double _maxSpeedCheckTime;

	private double _airSearchRadarCheckTime;

	private double _surfaceSearchRadarCheckTime;

	private List<Station> _radarCapableStations = new List<Station>();

	private List<Station> _aawStations = new List<Station>();

	private List<Station> _aswStations = new List<Station>();

	private Station _lastRadarStation;

	private ControlMode _selectedControlMode;

	public bool _isOnAlert;

	public Taskforce _taskforce;

	private double _aiCheckTimeShort = -10000.0;

	private double _aiCheckTimeMedium = -10000.0;

	public float _aswHeliPatrolDistance = 275.59497f;

	public float _aswHeliPatrolAngle = 45f;

	public bool _isDisbanded;

	public List<ObjectBase> _objectsToDestroy = new List<ObjectBase>();

	private bool _disabled;

	public bool _checkIfFormationAssembled;

	private string InternalName
	{
		get
		{
			return _internalName;
		}
		set
		{
			_internalName = value;
			NotifyPropertyChanged("InternalName");
			NotifyPropertyChanged("Name");
		}
	}

	public string Name
	{
		get
		{
			if (_externalNameSet)
			{
				if (!(_name != string.Empty))
				{
					return Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "UnnamedFormation");
				}
				return _name;
			}
			if (!(_internalName != string.Empty))
			{
				return Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "UnnamedFormation");
			}
			return _internalName;
		}
		set
		{
			_name = value;
			_externalNameSet = true;
			NotifyPropertyChanged("Name");
		}
	}

	public Station LeaderStation
	{
		get
		{
			return _leaderStation;
		}
		private set
		{
			_leaderStation = value;
			NotifyPropertyChanged("LeaderStation");
			if (_leaderStation != null && LeaderStation.UnitObject != null && LeaderStation.UnitObject.IsAirUnit)
			{
				InternalName = Singleton<LanguageResourceHandler>.Instance.getText("Formation", "AirFormationName").Replace("${leaderName}", LeaderStation.UnitObject._obp._objectName);
			}
		}
	}

	public List<(GeoPosition wpGP, WaypointData.WaypointHeightState heightState)> OldLeaderWaypointsGeoPositions { get; private set; }

	public float OldLeaderDesiredAltitude { get; private set; }

	public float OldLeaderDemandedSpeedInKnots { get; private set; }

	public bool RotateStationsAroundLeader { get; set; }

	public ReactiveProperty<float> MaxFormationSpeed { get; private set; } = new ReactiveProperty<float>();

	public TrulyObservableCollection<Station> Stations { get; private set; } = new TrulyObservableCollection<Station>();

	public string InFormationSummary
	{
		get
		{
			string[] uniqueTypeAbbr = (from y in Stations
				where y.UnitObject != null && !y.UnitObject.Disabled
				select y.UnitObject._obp._typeAbbr).Distinct().ToArray();
			string[] array = new string[uniqueTypeAbbr.Length];
			int i;
			for (i = 0; i < uniqueTypeAbbr.Length; i++)
			{
				array[i] = $"{Stations.Where((Station x) => x.UnitObject != null && !x.UnitObject.Disabled).Count((Station x) => x.UnitObject._obp._typeAbbr == uniqueTypeAbbr[i])} x {uniqueTypeAbbr[i]}";
			}
			return string.Join(", ", array);
		}
	}

	public float Spacing { get; private set; }

	public bool SetHeight { get; private set; }

	public FormationPattern Pattern { get; private set; }

	public ControlMode SelectedControlMode
	{
		get
		{
			return _selectedControlMode;
		}
		set
		{
			_selectedControlMode = value;
			NotifyPropertyChanged("SelectedControlMode");
		}
	}

	public IEnumerable<ControlMode> ControlModes { get; } = Enum.GetValues(typeof(ControlMode)).Cast<ControlMode>();

	public ReactiveProperty<ObjectBase.WeaponStatus> ObjectWeaponsState { get; }

	public DragDropCompletedCallback OnDragDropCompleted { get; }

	public bool Disabled
	{
		get
		{
			return _disabled;
		}
		set
		{
			_disabled = value;
			NotifyPropertyChanged("Disabled");
		}
	}

	public TacticalDoctrine TacticalDoctrine { get; private set; }

	public event PropertyChangedEventHandler PropertyChanged;

	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public UnitFormation(UnitFormationParameters formationParams)
	{
		Name = formationParams._name;
		RotateStationsAroundLeader = formationParams._rotateStationsAroundLeader;
		MaxFormationSpeed.Value = float.MaxValue;
		Spacing = formationParams._spacing;
		SetHeight = formationParams._leaderObject._type == ObjectBase.ObjectType.Aircraft || formationParams._setHeight;
		Pattern = formationParams._pattern;
		if (Pattern == FormationPattern.Battlegroup || Pattern == FormationPattern.LineAstern || Pattern == FormationPattern.LineAbreast)
		{
			RotateStationsAroundLeader = true;
		}
		AddUnit(formationParams._leaderObject, isLeader: true);
		LeaderStation = Stations[0];
		formationParams._leaderObject._taskforce.Formations.Add(this);
		_taskforce = formationParams._leaderObject._taskforce;
		TacticalDoctrine = new TacticalDoctrine(this);
		Stations.CollectionChanged += Stations_CollectionChanged;
		OnDragDropCompleted = delegate(DependencyObject source, IDataObject data, UIElement target, Point dropPoint, DragDropEffects effects)
		{
			if (data != null)
			{
				if (target is FrameworkElement frameworkElement)
				{
					if (data.GetDataPresent(typeof(UnitFormation)) && data.GetData(typeof(UnitFormation)) is UnitFormation { _isDisbanded: false } unitFormation && !(unitFormation.LeaderStation?.UnitObject == null))
					{
						object dataContext = frameworkElement.DataContext;
						if (dataContext is ObjectBase objectBase && objectBase != null)
						{
							unitFormation.AddUnitFromMenu(objectBase, isLeader: true, ".ctor");
						}
						else if (dataContext is UnitFormation unitFormation2)
						{
							if (unitFormation2 != unitFormation && !unitFormation2._isDisbanded && !(unitFormation2.LeaderStation?.UnitObject == null))
							{
								unitFormation2.AddUnitFromMenu(unitFormation.LeaderStation.UnitObject, isLeader: false, ".ctor");
							}
						}
						else if (dataContext is Station { UnitObject: var unitObject } && !(unitObject == null))
						{
							UnitFormation formation = unitObject.Formation;
							if (formation != null && formation != unitFormation && !formation._isDisbanded && !(formation.LeaderStation?.UnitObject == null))
							{
								foreach (ObjectBase item in (from x in unitFormation.Stations
									select x.UnitObject into y
									where y != null
									select y).ToList())
								{
									formation.AddUnitFromMenu(item, isLeader: false, ".ctor");
								}
							}
						}
					}
				}
				else if (target == null && data.GetDataPresent(typeof(Station)))
				{
					ObjectBase objectBase2 = (data.GetData(typeof(Station)) as Station)?.UnitObject;
					if (objectBase2 != null)
					{
						objectBase2.Formation?.DetachUnit(objectBase2);
					}
				}
			}
		};
		NotifyPropertyChanged("InFormationSummary");
		DM.D("Created new formation " + Name + " of " + LeaderStation.UnitObject.getUIDAndName() + " as leader");
	}

	private void Stations_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		bool flag = false;
		if (LeaderStation?.UnitObject != null)
		{
			flag = LeaderStation.UnitObject.IsAirUnit;
		}
		else
		{
			foreach (Station station in Stations)
			{
				if (!(station.UnitObject == null))
				{
					flag = station.UnitObject.IsAirUnit;
					break;
				}
			}
		}
		if (flag)
		{
			CalculateAirUnitNames("Stations_CollectionChanged");
		}
		if (Stations.Count((Station x) => x.UnitObject != null && x.UnitObject.Disabled) == Stations.Count((Station x) => x.UnitObject != null))
		{
			Disabled = true;
		}
		else
		{
			Disabled = false;
		}
		NotifyPropertyChanged("InFormationSummary");
	}

	public void CalculateAirUnitNames([CallerMemberName] string propertyName = "")
	{
		LeaderStation.UnitObject.Name.Value = LeaderStation.UnitObject._obp._objectName + "-1";
		int num = 2;
		foreach (Station station in Stations)
		{
			if (station != LeaderStation && !(station.UnitObject == null) && station.UnitObject.IsAirUnit)
			{
				station.UnitObject.Name.Value = $"{LeaderStation.UnitObject._obp._objectName}-{num}";
				num++;
			}
		}
	}

	public void Disband()
	{
		if (LeaderStation?.UnitObject is Vessel vessel)
		{
			vessel.setTelegraph(vessel._telegraph);
		}
		else if (LeaderStation?.UnitObject is Submarine submarine)
		{
			submarine.setTelegraph(submarine._telegraph);
		}
		DM.I("Formation " + Name + " of " + LeaderStation?.UnitObject?.getUIDAndName() + " disbanded!");
		foreach (Station station in Stations)
		{
			if (station != null && station.UnitObject != null)
			{
				station.UnitObject.Formation = null;
				if (station.UnitObject.IsCurrentActiveWaypointRelative())
				{
					List<(GeoPosition wpGP, WaypointData.WaypointHeightState heightState)> allWaypointsGeoPositions = station.UnitObject.GetAllWaypointsGeoPositions();
					station.UnitObject.RemoveWaypoints();
					foreach (var item in allWaypointsGeoPositions)
					{
						station.UnitObject.setWaypointTask(item.wpGP, "wp", item.heightState);
					}
				}
				if (station.UnitObject is Aircraft aircraft && aircraft.Motioncontroller is FormationFlightPhysics)
				{
					aircraft.SetFlightPhysicsModel(isStartingAirborne: true);
				}
				if (station.UnitObject.IsAirUnit)
				{
					station.UnitObject.Name.Value = station.UnitObject._obp._objectName;
				}
			}
			Stations.CollectionChanged -= Stations_CollectionChanged;
		}
		_taskforce.Formations.Remove(this);
		LeaderStation = null;
		_isDisbanded = true;
	}

	public void AddUnitFromMenu(ObjectBase obj, bool isLeader = false, [CallerMemberName] string callerMemberName = "")
	{
		Debug.Log($"UnitFormation.AddUnitFromMenu({obj}, {isLeader}) called from {callerMemberName}");
		if ((isLeader && !CheckFormationJoin(obj, LeaderStation.UnitObject)) || !CheckFormationJoin(LeaderStation.UnitObject, obj) || Stations.Any((Station x) => x.UnitObject == obj))
		{
			return;
		}
		if (obj.Formation?.LeaderStation?.UnitObject != null && obj.Formation?.LeaderStation?.UnitObject == obj)
		{
			IEnumerable<ObjectBase> otherUnits = from x in obj.Formation.Stations
				select x.UnitObject into y
				where y != obj
				select y;
			MessageBoxViewModel messageMergeFormation = new MessageBoxViewModel();
			messageMergeFormation.Message = Singleton<LanguageResourceHandler>.Instance.getText("Formation", "MergeFormation").Replace("${formationOneName}", obj.Formation.Name).Replace("${formationTwoName}", Name);
			messageMergeFormation.AcknowledgeCommand = new DelegateCommand(delegate
			{
				AddUnit(obj, isLeader);
				foreach (ObjectBase item in otherUnits)
				{
					AddUnit(item);
				}
				WindowHostCommands.CloseWindowCommand.Execute(messageMergeFormation, MainGameView.Instance);
			});
			messageMergeFormation.CancelCommand = new DelegateCommand(delegate
			{
				WindowHostCommands.CloseWindowCommand.Execute(messageMergeFormation, MainGameView.Instance);
			});
			WindowHostCommands.OpenWindowCommand.Execute(messageMergeFormation, MainGameView.Instance);
		}
		else
		{
			AddUnit(obj);
		}
	}

	public static bool CheckFormationJoin(ObjectBase objInFormation, ObjectBase objectJoiningFormation)
	{
		if (objInFormation == null || objectJoiningFormation == null)
		{
			return false;
		}
		if (objectJoiningFormation.IsAirUnit && objectJoiningFormation.StateMachine?.CurrentStateName == "BingoFuel")
		{
			return false;
		}
		if (objInFormation.IsAirUnit && objInFormation.StateMachine?.CurrentStateName == "BingoFuel")
		{
			return false;
		}
		bool result = false;
		if ((objInFormation._type == ObjectBase.ObjectType.Vessel || objInFormation._type == ObjectBase.ObjectType.Submarine) && (objectJoiningFormation._type == ObjectBase.ObjectType.Vessel || objectJoiningFormation._type == ObjectBase.ObjectType.Submarine))
		{
			result = true;
		}
		else if (objInFormation._type == ObjectBase.ObjectType.Aircraft && objectJoiningFormation._type == ObjectBase.ObjectType.Aircraft)
		{
			result = true;
		}
		else if (objInFormation._type == ObjectBase.ObjectType.Helicopter && objectJoiningFormation._type == ObjectBase.ObjectType.Helicopter)
		{
			result = true;
		}
		else if (objInFormation is LandUnit landUnit && objectJoiningFormation is LandUnit landUnit2 && landUnit._isMobile && landUnit2._isMobile)
		{
			result = true;
		}
		return result;
	}

	public void AddUnit(ObjectBase obj, bool isLeader = false)
	{
		if (Stations.Any((Station x) => x.UnitObject == obj))
		{
			return;
		}
		if (obj.Formation?.LeaderStation?.UnitObject != null && obj.Formation?.LeaderStation?.UnitObject == obj)
		{
			obj.Formation.Disband();
		}
		else if (obj.Formation != null)
		{
			obj.Formation.DetachUnit(obj);
		}
		if (isLeader)
		{
			if (obj.IsAirUnit)
			{
				SelectedControlMode = ControlMode.FollowLeader;
			}
			else
			{
				SelectedControlMode = ControlMode.Local;
			}
		}
		Station station = null;
		Vector3 vector = Vector3.zero;
		if (isLeader && LeaderStation != null)
		{
			LeaderStation.UnitObject = obj;
			obj.Formation = this;
			station = LeaderStation;
			vector = station.StationPosition;
		}
		Station station2 = Stations.FirstOrDefault((Station s) => s.UnitObject == null);
		if (station2 != null)
		{
			station2.UnitObject = obj;
			obj.Formation = this;
			station = station2;
			vector = station.StationPosition;
		}
		if (station == null)
		{
			if (isLeader)
			{
				vector = Vector3.zero;
			}
			else if (Pattern == FormationPattern.Loose)
			{
				vector = LeaderStation.UnitObject.transform.InverseTransformPoint(obj.transform.position + obj.transform.forward * 0.1f * 27.559496f);
			}
			else if (Pattern == FormationPattern.Vic)
			{
				vector = GetNewStationPositionInVicFormation(Spacing, Stations.Count);
			}
			else if (Pattern == FormationPattern.Circle)
			{
				vector = GetNewStationPositionInCircularFormation(Spacing, Stations.Count, SetHeight);
			}
			else if (Pattern == FormationPattern.LineAbreast)
			{
				vector = GetNewStationPositionInLineAbreastFormation(Spacing, Stations.Count);
			}
			else if (Pattern == FormationPattern.LineAstern)
			{
				vector = GetNewStationPositionInLineAsternFormation(Spacing, Stations.Count, SetHeight);
			}
			else if (Pattern == FormationPattern.Echelon)
			{
				vector = GetNewStationPositionInEchelonFormation(Spacing, Stations.Count);
			}
			else if (Pattern == FormationPattern.Box)
			{
				vector = GetNewStationPositionInBoxFormation(Spacing, Stations.Count, SetHeight);
			}
			else if (Pattern == FormationPattern.Battlegroup)
			{
				vector = LeaderStation.UnitObject.transform.InverseTransformPoint(obj.transform.position + obj.transform.forward * 0.1f * 27.559496f);
			}
			if (!RotateStationsAroundLeader && !isLeader)
			{
				vector = Matrix4x4.Rotate(Quaternion.AngleAxis(LeaderStation.UnitObject.transform.eulerAngles.y, Vector3.up)) * vector;
			}
			station = new Station(obj, vector, this);
			if (LeaderStation == null && Stations.Count == 0)
			{
				LeaderStation = station;
			}
			if (isLeader)
			{
				LeaderStation = station;
			}
			Stations.Add(station);
			station.UnitObject.Formation = this;
			station.Tag = StationTag.Default;
		}
		if (!isLeader)
		{
			if (obj.CurrentOrder.Value.OrderType == Order.Type.ReturnToBase)
			{
				obj.setOrder(Order.Type.None, null, displayOrderText: false);
			}
			station.UnitObject.RemoveWaypoints();
			station.UnitObject.SetRelativeWaypointTask(vector, LeaderStation.UnitObject, "wp", reachable: false, SetHeight, RotateStationsAroundLeader);
		}
		if (obj != LeaderStation.UnitObject)
		{
			DM.D(obj.getUIDAndName() + " joins formation " + Name + " of " + LeaderStation.UnitObject.getUIDAndName());
		}
	}

	public void DetachUnit(ObjectBase obj)
	{
		if (Stations.Count == 0 || !(obj != null))
		{
			return;
		}
		Station station = Stations.FirstOrDefault((Station s) => s.UnitObject == obj);
		if (LeaderStation?.UnitObject == obj)
		{
			if (LeaderStation?.UnitObject is Vessel vessel)
			{
				vessel.setTelegraph(vessel._telegraph);
			}
			else if (LeaderStation?.UnitObject is Submarine submarine)
			{
				submarine.setTelegraph(submarine._telegraph);
			}
			AssignNewLeaderFromAvailableUnits();
		}
		CleanUpStation(obj, station);
		Stations.Remove(station);
		if (obj is Aircraft aircraft && aircraft.Motioncontroller is FormationFlightPhysics)
		{
			aircraft.SetFlightPhysicsModel(isStartingAirborne: true);
		}
	}

	public void ReturnToFormation(ObjectBase obj, bool message = false)
	{
		if (obj == null || LeaderStation?.UnitObject == null)
		{
			return;
		}
		Station station = Stations.FirstOrDefault((Station x) => x.UnitObject == obj);
		if (station != null)
		{
			obj.RemoveWaypoints();
			obj.SetRelativeWaypointTask(station.StationPosition, LeaderStation.UnitObject, "wp", reachable: false, SetHeight, RotateStationsAroundLeader);
			station.UnitActsIndependently.Value = false;
			if (message && obj.IsPlayerObject)
			{
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("ReturningToFormation", obj);
			}
		}
	}

	public void Reform(FormationPattern pattern, float spacing, bool teleportToPositions = false)
	{
		if (pattern == FormationPattern.Convoy)
		{
			FormUpConvoy(teleportToPositions);
		}
		else
		{
			for (int i = 0; i < Stations.Count; i++)
			{
				if (Stations[i] == LeaderStation)
				{
					continue;
				}
				ObjectBase unitObject = Stations[i].UnitObject;
				if (unitObject != null)
				{
					Vector3 vector = Vector3.zero;
					switch (pattern)
					{
					case FormationPattern.Vic:
						vector = GetNewStationPositionInVicFormation(spacing, i);
						break;
					case FormationPattern.LineAbreast:
						vector = GetNewStationPositionInLineAbreastFormation(Spacing, i);
						break;
					case FormationPattern.LineAstern:
						vector = GetNewStationPositionInLineAsternFormation(Spacing, i, SetHeight);
						break;
					case FormationPattern.Echelon:
						vector = GetNewStationPositionInEchelonFormation(Spacing, i);
						break;
					case FormationPattern.Box:
						vector = GetNewStationPositionInBoxFormation(Spacing, i, SetHeight);
						break;
					case FormationPattern.Circle:
						vector = GetNewStationPositionInCircularFormation(spacing, i, SetHeight);
						break;
					}
					Vector3 vector2 = Matrix4x4.Rotate(Quaternion.AngleAxis(LeaderStation.UnitObject.transform.eulerAngles.y, Vector3.up)) * vector;
					if (!RotateStationsAroundLeader)
					{
						vector = vector2;
					}
					ChangeStationPosition(Stations[i], vector, SetHeight);
					if (teleportToPositions)
					{
						unitObject.transform.position = LeaderStation.UnitObject.transform.position + vector2 - 5.5118995f * LeaderStation.UnitObject.transform.forward;
						unitObject.SetTeleportProtection();
					}
				}
			}
		}
		Pattern = pattern;
		Spacing = spacing;
	}

	public void CeaseFire(bool recall = false, bool message = true)
	{
		_objectsToDestroy.Clear();
		foreach (Station station in Stations)
		{
			if (station == null)
			{
				continue;
			}
			ObjectBase unitObject = station.UnitObject;
			if (unitObject == null || unitObject.IsDestroyed || !unitObject.IsControllable || unitObject._ai == null)
			{
				continue;
			}
			if (!recall)
			{
				if (!unitObject.FollowingFormation)
				{
					continue;
				}
				bool flag = false;
				foreach (AlignUnitToTarget alignmentTask in unitObject._alignmentTasks)
				{
					if (alignmentTask._isGuidingWeapon)
					{
						unitObject._ai._objectToDestroy = null;
						unitObject._ai._objectsToDestroyList.Clear();
						unitObject._currentEngageTasks.Clear();
						unitObject.ClearAttackTasks();
						flag = true;
						break;
					}
				}
				if (flag)
				{
					continue;
				}
			}
			unitObject.CeaseFire(report: false);
			if (recall && !unitObject.IsFormationLeader)
			{
				ReturnToFormation(unitObject);
			}
		}
		if (message && Globals._mainGameViewModel != null && LeaderStation?.UnitObject != null && LeaderStation.UnitObject.AcceptsOrdersFromPlayer)
		{
			string text = (recall ? Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "FormationCeaseFireAndReturn") : Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "FormationCeaseFire"));
			text = text.Replace("${FormationName}", Name);
			Globals._mainGameViewModel.RaiseAlert(text);
		}
	}

	public void AllReturnToFormation(bool ceaseFire = false, bool message = true)
	{
		foreach (Station station in Stations)
		{
			if (station == null || station == LeaderStation)
			{
				continue;
			}
			ObjectBase unitObject = station.UnitObject;
			if (!(unitObject == null) && !unitObject.IsDestroyed && unitObject.IsControllable)
			{
				unitObject.RemoveWaypoints();
				unitObject.SetRelativeWaypointTask(station.StationPosition, LeaderStation.UnitObject, "wp", reachable: false, SetHeight, RotateStationsAroundLeader);
				station.UnitActsIndependently.Value = false;
				if (ceaseFire)
				{
					unitObject.CeaseFire();
				}
			}
		}
		if (message && Globals._mainGameViewModel != null && LeaderStation?.UnitObject != null && LeaderStation.UnitObject.AcceptsOrdersFromPlayer)
		{
			string text = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "FormationRecall");
			text = text.Replace("${FormationName}", Name);
			Globals._mainGameViewModel.RaiseAlert(text);
		}
	}

	private void CleanUpStation(ObjectBase obj, Station station)
	{
		if (station != null)
		{
			if (Stations.Count == 1)
			{
				obj._taskforce.Formations.Remove(this);
			}
			station.UnitObject = null;
			obj.Formation = null;
			if (station != LeaderStation && !station.UnitActsIndependently.Value)
			{
				obj.RemoveWaypoints();
			}
		}
	}

	private void AssignNewLeaderFromAvailableUnits()
	{
		if (Stations.Count < 1 || LeaderStation == null)
		{
			return;
		}
		ObjectBase objectBase = null;
		Station leaderStation = LeaderStation;
		if (LeaderStation.UnitObject != null)
		{
			objectBase = LeaderStation.UnitObject;
		}
		int num = 5;
		if (objectBase != null)
		{
			if (objectBase is LandUnit)
			{
				num = 0;
			}
			else if (objectBase is Vessel)
			{
				num = 1;
			}
			else if (objectBase is Submarine)
			{
				num = 2;
			}
			else if (objectBase is Helicopter)
			{
				num = 3;
			}
			else if (objectBase is Aircraft)
			{
				num = 4;
			}
		}
		else
		{
			foreach (Station station in Stations)
			{
				if (station.UnitObject is LandUnit)
				{
					num = 0;
				}
				else if (num > 0)
				{
					if (station.UnitObject is Vessel)
					{
						num = 1;
					}
				}
				else if (num > 1)
				{
					if (station.UnitObject is Submarine)
					{
						num = 2;
					}
				}
				else if (num > 2)
				{
					if (station.UnitObject is Helicopter)
					{
						num = 3;
					}
				}
				else if (num > 3 && station.UnitObject is Aircraft)
				{
					num = 4;
				}
			}
		}
		foreach (Station station2 in Stations)
		{
			if (station2.UnitObject is Aircraft aircraft)
			{
				aircraft._resetFormation = true;
			}
		}
		bool flag = false;
		for (int i = 0; i < Stations.Count; i++)
		{
			if (Stations[i] == LeaderStation || UnitOnStationNotValid(Stations[i]) || (num == 0 && !(Stations[i].UnitObject is LandUnit)) || (num == 1 && !(Stations[i].UnitObject is Vessel)) || (num == 2 && !(Stations[i].UnitObject is Submarine)) || (num == 3 && !(Stations[i].UnitObject is Helicopter)))
			{
				continue;
			}
			LeaderStation = Stations[i];
			LeaderStation.UnitObject.RemoveWaypoints();
			LeaderStation.UnitObject.SetSpeedCommand(new ConstantSpeed(OldLeaderDemandedSpeedInKnots, LeaderStation.UnitObject));
			LeaderStation.UnitObject.DesiredAltitude.Value = OldLeaderDesiredAltitude;
			if (objectBase != null)
			{
				LeaderStation.UnitObject.CopyWaypointsFrom(objectBase);
				objectBase.RemoveWaypoints();
				if (!objectBase.IsDestroyed)
				{
					objectBase.SetRelativeWaypointTask(leaderStation.StationPosition + objectBase.transform.forward * 0.05f * 27.559496f, LeaderStation.UnitObject, "wp", reachable: false, SetHeight, RotateStationsAroundLeader);
				}
			}
			else if (OldLeaderWaypointsGeoPositions != null)
			{
				foreach (var oldLeaderWaypointsGeoPosition in OldLeaderWaypointsGeoPositions)
				{
					LeaderStation.UnitObject.setWaypointTask(oldLeaderWaypointsGeoPosition.wpGP, "wp", oldLeaderWaypointsGeoPosition.heightState);
				}
			}
			LeaderStation.UnitObject.CurrentOrderText.Value = Singleton<LanguageResourceHandler>.Instance.getText("Status", "FormationLeader");
			LeaderStation.StationPosition = Vector3.zero;
			DM.I(LeaderStation.UnitObject.getUIDAndName() + " assigned as new leader of formation " + Name);
			flag = true;
			break;
		}
		if (!flag)
		{
			Disband();
			return;
		}
		foreach (Station station3 in Stations)
		{
			if (station3 != LeaderStation && station3.UnitObject != null && station3.UnitObject.CommandPosition.HasValue)
			{
				Vector3 vector = LeaderStation.UnitObject.transform.InverseTransformPoint(Utils.longLatToLocalV3(station3.UnitObject.CommandPosition.Value, Globals._currentCenterTile));
				vector = Matrix4x4.Rotate(Quaternion.AngleAxis(LeaderStation.UnitObject.transform.eulerAngles.y, Vector3.up)) * vector;
				ChangeStationPosition(station3, vector);
			}
			else
			{
				LeaderStation.StationPosition = Vector3.zero;
			}
		}
		if (!(objectBase != null))
		{
			return;
		}
		for (int j = 0; j < Singleton<ObjectsManager>.Instance._quickAcessObjects.Length; j++)
		{
			if (objectBase == Singleton<ObjectsManager>.Instance._quickAcessObjects[j])
			{
				Singleton<ObjectsManager>.Instance._quickAcessObjects[j] = LeaderStation.UnitObject;
				objectBase._quickAccessNumber = -1;
				LeaderStation.UnitObject._quickAccessNumber = j;
				break;
			}
		}
	}

	public void OffsetStationPosition(Station station, Vector3 offset, bool setStationHeight = false, bool reachable = true)
	{
		if (Stations.Count != 0 && station != null)
		{
			station.UnitObject.RemoveWaypoints();
			station.UnitObject.SetRelativeToStationWaypointTask(offset, station, "offset", reachable, setStationHeight);
		}
	}

	public void ChangeStationPosition(Station station, Vector3 newStationPosition, bool setStationHeight = false)
	{
		if (Stations.Count == 0 || station == null)
		{
			return;
		}
		station.StationPosition = newStationPosition;
		if (station.UnitObject != null)
		{
			station.UnitObject.RemoveWaypoints();
			bool rotatesWithTarget = RotateStationsAroundLeader;
			if (station.rotationOverride.enabled)
			{
				rotatesWithTarget = station.rotationOverride.rotates;
			}
			station.UnitObject.SetRelativeWaypointTask(newStationPosition, LeaderStation.UnitObject, "wp", reachable: false, setStationHeight, rotatesWithTarget);
		}
	}

	public void ChangeStationPosition(ObjectBase obj, GeoPosition geoPos, bool setStationHeight = false)
	{
		if (Stations.Count != 0)
		{
			Vector3 vector = LeaderStation.UnitObject.transform.InverseTransformPoint(Utils.longLatToLocalV3(geoPos, Globals._currentCenterTile));
			if (!RotateStationsAroundLeader)
			{
				vector = Matrix4x4.Rotate(Quaternion.AngleAxis(LeaderStation.UnitObject.transform.eulerAngles.y, Vector3.up)) * vector;
			}
			ChangeStationPosition(GetStationForUnit(obj), vector);
		}
	}

	public void SwapLeader(ObjectBase obj)
	{
		if (Stations.Count == 0 || LeaderStation == null || obj == null || obj.IsDestroyed)
		{
			return;
		}
		ObjectBase unitObject = LeaderStation.UnitObject;
		Station leaderStation = LeaderStation;
		if (unitObject != null)
		{
			obj.RemoveWaypoints();
			obj.CopyWaypointsFrom(unitObject);
			unitObject.RemoveWaypoints();
			unitObject.SetRelativeWaypointTask(leaderStation.StationPosition + unitObject.transform.forward * 0.05f * 27.559496f, LeaderStation.UnitObject, "wp", reachable: false, SetHeight, RotateStationsAroundLeader);
		}
		LeaderStation = GetStationForUnit(obj);
		LeaderStation.UnitObject.SetSpeedCommand(new ConstantSpeed(OldLeaderDemandedSpeedInKnots, LeaderStation.UnitObject));
		LeaderStation.UnitObject.DesiredAltitude.Value = OldLeaderDesiredAltitude;
		LeaderStation.UnitObject.CurrentOrderText.Value = Singleton<LanguageResourceHandler>.Instance.getText("Status", "FormationLeader");
		LeaderStation.StationPosition = Vector3.zero;
		if (Pattern != FormationPattern.None && Pattern != FormationPattern.Battlegroup)
		{
			Reform(Pattern, Spacing);
		}
		else
		{
			foreach (Station station in Stations)
			{
				if (station != LeaderStation && station.UnitObject != null && station.UnitObject.CommandPosition.HasValue)
				{
					Vector3 vector = LeaderStation.UnitObject.transform.InverseTransformPoint(Utils.longLatToLocalV3(station.UnitObject.CommandPosition.Value, Globals._currentCenterTile));
					vector = Matrix4x4.Rotate(Quaternion.AngleAxis(LeaderStation.UnitObject.transform.eulerAngles.y, Vector3.up)) * vector;
					ChangeStationPosition(station, vector);
				}
				else
				{
					LeaderStation.StationPosition = Vector3.zero;
				}
			}
		}
		foreach (Station station2 in Stations)
		{
			if (station2.UnitObject is Aircraft aircraft)
			{
				aircraft._resetFormation = true;
			}
		}
		if (!(unitObject != null))
		{
			return;
		}
		for (int i = 0; i < Singleton<ObjectsManager>.Instance._quickAcessObjects.Length; i++)
		{
			if (unitObject == Singleton<ObjectsManager>.Instance._quickAcessObjects[i])
			{
				Singleton<ObjectsManager>.Instance._quickAcessObjects[i] = LeaderStation.UnitObject;
				unitObject._quickAccessNumber = -1;
				LeaderStation.UnitObject._quickAccessNumber = i;
				break;
			}
		}
	}

	public void MakeUnitActIndependently(ObjectBase obj)
	{
		if (Stations.Count != 0)
		{
			Station station = Stations.FirstOrDefault((Station x) => x.UnitObject == obj);
			if (station != null && station != LeaderStation)
			{
				station.UnitActsIndependently.Value = true;
			}
		}
	}

	public Station GetStationForUnit(ObjectBase obj)
	{
		Station result = null;
		if (Stations.Count == 0)
		{
			return result;
		}
		foreach (Station station in Stations)
		{
			if (station.UnitObject == obj)
			{
				return station;
			}
		}
		return null;
	}

	public int GetStationIndex(ObjectBase obj)
	{
		for (int i = 0; i < Stations.Count; i++)
		{
			if (Stations[i].UnitObject == obj)
			{
				return i;
			}
		}
		return -1;
	}

	public float GetDistanceToStation(ObjectBase obj)
	{
		if (Stations.Count == 0)
		{
			return -1f;
		}
		Station stationForUnit = GetStationForUnit(obj);
		Vector3 vector = stationForUnit.StationPosition;
		if (RotateStationsAroundLeader)
		{
			vector = Quaternion.Euler(0f, LeaderStation.UnitObject.transform.eulerAngles.y, 0f) * stationForUnit.StationPosition;
		}
		return (LeaderStation.UnitObject.transform.position + vector - obj.transform.position).magnitude;
	}

	public static Vector3 GetNewStationPositionInVicFormation(float spacing, int stationIndex)
	{
		Vector3 result = new Vector3(spacing, 0f, (0f - spacing) / 2f);
		result *= 27.559496f * (float)((stationIndex - 1) / 2 + 1);
		if (stationIndex % 2 == 0)
		{
			result.x = 0f - result.x;
		}
		return result;
	}

	public static Vector3 GetNewStationPositionInLineAbreastFormation(float spacing, int stationIndex)
	{
		stationIndex--;
		Vector3 result = new Vector3(spacing * (float)(stationIndex / 2 + 1), 0f, 0f);
		result *= 27.559496f;
		if (stationIndex % 2 == 0)
		{
			result.x = 0f - result.x;
		}
		return result;
	}

	public static Vector3 GetNewStationPositionInLineAsternFormation(float spacing, int stationIndex, bool setHeight)
	{
		Vector3 result = new Vector3(0f, 0f, 0f - spacing);
		result *= 27.559496f * (float)stationIndex;
		if (setHeight)
		{
			if (stationIndex % 2 == 0)
			{
				result.y += 0.453571f;
			}
			if (stationIndex % 3 == 0)
			{
				result.y -= 0.453571f;
			}
		}
		return result;
	}

	public static Vector3 GetNewStationPositionInEchelonFormation(float spacing, int stationIndex)
	{
		return new Vector3(spacing, 0f, 0f - spacing) * (27.559496f * (float)stationIndex);
	}

	public static Vector3 GetNewStationPositionInBoxFormation(float spacing, int stationIndex, bool setHeight)
	{
		spacing *= (float)((stationIndex - 1) / 3 + 1);
		Vector3 result = new Vector3(spacing, 0f, 0f - spacing);
		result *= 27.559496f;
		if (stationIndex % 3 == 0)
		{
			result.x = 0f - result.x;
		}
		else if (stationIndex % 2 == 0)
		{
			result.x = 0f;
		}
		if (stationIndex % 2 == 0 && setHeight)
		{
			if (stationIndex % 2 == 0)
			{
				result.y -= 0.453571f;
			}
			if (stationIndex % 5 == 0)
			{
				result.y += 0.453571f;
			}
		}
		return result;
	}

	public static Vector3 GetNewStationPositionInCircularFormation(float spacing, int stationIndex, bool setHeight)
	{
		spacing *= (float)((stationIndex - 1) / 8 + 1);
		float num = 45f;
		float num2 = (float)stationIndex * num;
		if (num2 > 360f)
		{
			stationIndex %= 8;
			num2 = (float)stationIndex * num;
		}
		Vector3 result = new Vector3
		{
			y = 0f,
			x = spacing * Mathf.Cos(MathF.PI / 180f * num2),
			z = spacing * Mathf.Sin(MathF.PI / 180f * num2)
		};
		result.x *= 27.559496f;
		result.z *= 27.559496f;
		if (setHeight)
		{
			result.y = 0f;
		}
		return result;
	}

	private bool UnitOnStationNotValid(Station station)
	{
		bool result = false;
		if (station == null)
		{
			return true;
		}
		if (station?.UnitObject == null)
		{
			result = true;
		}
		else if (station.UnitObject.IsDestroyed)
		{
			result = true;
		}
		else if (station.UnitObject is Vessel vessel && vessel._compartments._isSinking)
		{
			result = true;
		}
		else if (station.UnitObject is Submarine submarine && submarine._compartments._isSinking)
		{
			result = true;
		}
		return result;
	}

	public void OnUpdate()
	{
		if (UnitOnStationNotValid(LeaderStation))
		{
			AssignNewLeaderFromAvailableUnits();
		}
		if (LeaderStation?.UnitObject != null)
		{
			if (LeaderStation?.UnitObject is Aircraft)
			{
				if ((double)Time.time > _maxSpeedCheckTime + 1.0)
				{
					_maxSpeedCheckTime = Time.time;
					if (LeaderStation.UnitObject.SpeedCommand != null)
					{
						OldLeaderDemandedSpeedInKnots = LeaderStation.UnitObject.SpeedCommand.Value.SpeedInKnots;
					}
					OldLeaderWaypointsGeoPositions = LeaderStation.UnitObject.GetAllWaypointsGeoPositions();
					OldLeaderDesiredAltitude = LeaderStation.UnitObject.DesiredAltitude.Value;
					float num = float.MaxValue;
					foreach (Station station in Stations)
					{
						if (!(station.UnitObject == null) && station.UnitObject is Aircraft)
						{
							float maxForwardSpeedInKnots = (station.UnitObject as Aircraft).MaxForwardSpeedInKnots;
							if (maxForwardSpeedInKnots < num)
							{
								num = maxForwardSpeedInKnots;
							}
						}
					}
					MaxFormationSpeed.Value = num;
				}
				if (_checkIfFormationAssembled)
				{
					if (LeaderStation.UnitObject._speedSetByPlayer)
					{
						_checkIfFormationAssembled = false;
					}
					if (Stations.Count > 1)
					{
						bool flag = true;
						foreach (Station station2 in Stations)
						{
							if (station2 != LeaderStation && !(station2.UnitObject == null) && !station2.UnitObject.IsDestroyed && station2.UnitObject is Aircraft aircraft && !(aircraft.Motioncontroller is FormationFlightPhysics))
							{
								flag = false;
								break;
							}
						}
						if (flag)
						{
							(LeaderStation.UnitObject as Aircraft).setTelegraph(3);
							_checkIfFormationAssembled = false;
						}
					}
				}
			}
			else
			{
				if (_checkIfFormationAssembled)
				{
					_checkIfFormationAssembled = false;
				}
				if ((double)Time.time > _maxSpeedCheckTime + 5.0)
				{
					_maxSpeedCheckTime = Time.time;
					if (LeaderStation?.UnitObject?.SpeedCommand?.Value != null)
					{
						OldLeaderDemandedSpeedInKnots = LeaderStation.UnitObject.SpeedCommand.Value.SpeedInKnots;
					}
					OldLeaderWaypointsGeoPositions = LeaderStation.UnitObject.GetAllWaypointsGeoPositions();
					OldLeaderDesiredAltitude = LeaderStation.UnitObject.DesiredAltitude.Value;
					float num2 = float.MaxValue;
					foreach (Station station3 in Stations)
					{
						if (station3.UnitObject == null)
						{
							continue;
						}
						float num3 = 0f;
						float num4 = 0f;
						if (station3.UnitObject is Vessel vessel)
						{
							if (Singleton<Environment>.Instance.SeaState > 3)
							{
								num4 = vessel.VP._maxForwardVelocityInKnots - ((float)Singleton<Environment>.Instance.SeaState - 3f) * vessel.VP._displacementCoeff;
								num3 = ((!(num4 > 5f)) ? 5f : num4);
							}
							else
							{
								num3 = station3.UnitObject.MaxForwardSpeedInKnots;
							}
						}
						else if (station3.UnitObject is Submarine submarine)
						{
							if (submarine.IsSubmerged.Value)
							{
								num3 = submarine.MaxForwardSpeedSubmergedInKnots;
							}
							else if (Singleton<Environment>.Instance.SeaState > 3)
							{
								num4 = submarine.SP._maxForwardVelocitySurfacedInKnots - ((float)Singleton<Environment>.Instance.SeaState - 3f) * submarine.SP._displacementCoeff;
								num3 = ((!(num4 > 5f)) ? 5f : num4);
							}
							else
							{
								num3 = submarine.MaxForwardSpeedInKnots;
							}
						}
						else if (station3.UnitObject is Biologic biologic)
						{
							num3 = biologic.MaxForwardSpeedInKnots;
						}
						else if (station3.UnitObject is Helicopter helicopter)
						{
							num3 = helicopter._hp._maxSpeedSeaLevel;
						}
						else if (station3.UnitObject is Aircraft aircraft2)
						{
							num3 = aircraft2.Ap._maxSpeedSeaLevel;
						}
						else if (station3.UnitObject is LandUnit landUnit)
						{
							if (!landUnit._isMobile)
							{
								continue;
							}
							num3 = landUnit.MaxForwardSpeedInKnots;
						}
						if (num3 < num2)
						{
							num2 = num3;
						}
					}
					MaxFormationSpeed.Value = num2;
				}
			}
		}
		if (GameTime.missionElapsedTime > _aiCheckTimeMedium)
		{
			CheckAI();
		}
		if (GameTime.missionElapsedTime > _aiCheckTimeShort)
		{
			CheckAutomation();
		}
		for (int i = 0; i < Stations.Count; i++)
		{
			if (Stations[i] != LeaderStation && !Stations[i].UnitActsIndependently.Value && Stations[i].Role == StationRole.Default && Stations[i].UnitObject != null && Stations[i].UnitObject._hasControl && !Stations[i].UnitObject.HasWaypoints())
			{
				ReturnToFormation(Stations[i].UnitObject);
			}
			Stations[i].OnUpdate();
		}
	}

	public void CheckAutomation()
	{
		_aiCheckTimeShort = GameTime.missionElapsedTime + 1.0;
		if (_taskforce != Globals._playerTaskforce)
		{
			if (GameTime.missionElapsedTime > _airSearchRadarCheckTime)
			{
				_airSearchRadarCheckTime = GameTime.missionElapsedTime + 600.0;
				CheckForRadarScan(RadarParameters.Role.Air);
			}
			if (GameTime.missionElapsedTime > _surfaceSearchRadarCheckTime)
			{
				_surfaceSearchRadarCheckTime = GameTime.missionElapsedTime + 300.0;
				CheckForRadarScan(RadarParameters.Role.Surface);
			}
		}
		if (_objectsToDestroy.Count <= 0)
		{
			return;
		}
		for (int num = _objectsToDestroy.Count - 1; num >= 0; num--)
		{
			if (_objectsToDestroy[num] == null || _objectsToDestroy[num].IsDestroyed || !_objectsToDestroy[num]._hasControl)
			{
				_objectsToDestroy.RemoveAt(num);
			}
		}
		if (_objectsToDestroy.Count <= 0)
		{
			return;
		}
		bool flag = false;
		foreach (Station station in Stations)
		{
			if (station.UnitObject?._ai != null && !station.UnitObject.IsDestroyed && station.UnitObject.AcceptsOrdersFromAI && !station.UnitActsIndependently.Value)
			{
				if (station.UnitObject._currentLoadoutCapabilities._hasASWTorpedoes)
				{
					flag = true;
				}
				if (!(station.UnitObject._ai._objectToAttack != null) && !(station.UnitObject._ai._objectToDestroy != null) && station.UnitObject._ai._objectsToDestroyList.Count <= 0)
				{
					ObjectBase item = _objectsToDestroy[0];
					station.UnitObject._ai._objectsToDestroyList.Add(_objectsToDestroy[0]);
					_objectsToDestroy.RemoveAt(0);
					_objectsToDestroy.Add(item);
				}
			}
		}
		if (flag)
		{
			return;
		}
		for (int num2 = _objectsToDestroy.Count - 1; num2 >= 0; num2--)
		{
			if (_objectsToDestroy[num2] is Submarine && _objectsToDestroy[num2].isSubmerged())
			{
				_objectsToDestroy.RemoveAt(num2);
			}
		}
	}

	public void CheckAI()
	{
		_aiCheckTimeMedium = GameTime.missionElapsedTime + 10.0;
		if (_taskforce != Globals._playerTaskforce && Globals._allowEnemyUnitsAttackPlayer && _taskforce != Globals._neutralTaskforce)
		{
			CheckForUnitLaunch(ObjectBase.ObjectType.Helicopter, 5, "asw", ObjectBaseParameters.UnitRoles.ASW, FlightDeckTask.MissionType.MPA, countThoseWhoLeftFormation: false);
			CheckASWAssignment();
		}
	}

	private void CheckForUnitLaunch(ObjectBase.ObjectType type, int maxAllowed, string loadoutName = "recon", ObjectBaseParameters.UnitRoles role = ObjectBaseParameters.UnitRoles.None, FlightDeckTask.MissionType missionType = FlightDeckTask.MissionType.None, bool countThoseWhoLeftFormation = true)
	{
		maxAllowed--;
		StationRole stationRole = StationRole.Default;
		switch (missionType)
		{
		case FlightDeckTask.MissionType.AEW:
			stationRole = StationRole.AEW;
			break;
		case FlightDeckTask.MissionType.MPA:
		case FlightDeckTask.MissionType.SearchAndDestroyASW:
			stationRole = ((type == ObjectBase.ObjectType.Helicopter) ? StationRole.HeloASW : StationRole.ASW);
			break;
		}
		int num = 0;
		foreach (Station station in Stations)
		{
			ObjectBase unitObject = station.UnitObject;
			if (unitObject?._obp == null)
			{
				continue;
			}
			if (unitObject._type == type)
			{
				if ((type == ObjectBase.ObjectType.Aircraft && !((Aircraft)unitObject)._isInFlight) || (type == ObjectBase.ObjectType.Helicopter && !((Helicopter)unitObject)._isInFlight) || station.Role != stationRole || (role != ObjectBaseParameters.UnitRoles.None && !unitObject._obp._unitRoles.Contains(role)))
				{
					continue;
				}
				num++;
			}
			if (unitObject._obp._flightDeck != null)
			{
				if (countThoseWhoLeftFormation)
				{
					foreach (ObjectBase item in unitObject._childAircraft)
					{
						if (item._type == type && item.Formation != this && item._obp._unitRoles.Contains(role))
						{
							num++;
						}
					}
				}
				foreach (FlightDeckTask flightDeckTask in unitObject._obp._flightDeck.FlightDeckTasks)
				{
					if (flightDeckTask.VehicleType != null && flightDeckTask.VehicleType._objectType == type && flightDeckTask.VehicleType._unitRoles.Contains(role))
					{
						num++;
					}
				}
			}
			if (num > maxAllowed)
			{
				return;
			}
		}
		foreach (Station station2 in Stations)
		{
			ObjectBase unitObject2 = station2.UnitObject;
			if (unitObject2?._obp?._flightDeck != null && !unitObject2._obp._flightDeck._aiDisabled && (type != ObjectBase.ObjectType.Helicopter || role != ObjectBaseParameters.UnitRoles.ASW || !unitObject2._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Carrier)) && unitObject2._hasControl && unitObject2.FlightDeckHasUnit(type, role) && unitObject2 is Vessel)
			{
				unitObject2.FlightDeckLaunchUnit(type, loadoutName, role, missionType);
				return;
			}
		}
		if (type != ObjectBase.ObjectType.Helicopter || role != ObjectBaseParameters.UnitRoles.ASW)
		{
			return;
		}
		foreach (Station station3 in Stations)
		{
			ObjectBase unitObject3 = station3.UnitObject;
			if (unitObject3?._obp?._flightDeck != null && !unitObject3._obp._flightDeck._aiDisabled && unitObject3._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Carrier) && unitObject3._hasControl && unitObject3.FlightDeckHasUnit(type, role))
			{
				unitObject3.FlightDeckLaunchUnit(type, loadoutName, role, missionType);
				break;
			}
		}
	}

	public void CheckForStationDemands()
	{
		foreach (Station station in Stations)
		{
			if (station.DemandsUnit)
			{
				switch (station.Role)
				{
				case StationRole.HeloASW:
					DemandAswHelo(station);
					break;
				}
			}
		}
	}

	public void DemandAswHelo(Station station)
	{
		Debug.Log($"DemandAswHelo({station})");
		IEnumerable<ObjectBase> source = from st in Stations
			select st.UnitObject into x
			where x != null && x._obp?._flightDeck != null && x.FlightDeckHasUnit(ObjectBase.ObjectType.Helicopter, ObjectBaseParameters.UnitRoles.ASW)
			select x;
		if (source.Count() >= 1)
		{
			source.OrderBy((ObjectBase x) => station.GeoPosition.GetDistance(x.getGeoPosition()) / (double)x.FlightDeckGetNumberOfUnits(ObjectBase.ObjectType.Helicopter, ObjectBaseParameters.UnitRoles.ASW)).FirstOrDefault().FlightDeckLaunchUnit(ObjectBase.ObjectType.Helicopter, "ASW", ObjectBaseParameters.UnitRoles.ASW);
			station.DemandsUnit = false;
		}
	}

	public void CheckASWAssignment()
	{
		foreach (KeyValuePair<ObjectBase, Vehicle> localVehicle in _taskforce.PlottingTable.LocalVehicles)
		{
			ObjectBase key = localVehicle.Key;
			_ = localVehicle.Value;
			if (!(key is Submarine) || key._ai._enemiesInvestigatingThisUnit.Count > 0)
			{
				continue;
			}
			ObjectBase objectBase = null;
			float num = float.MaxValue;
			float num2 = 826.78485f;
			num2 *= num2;
			foreach (Station item in Stations.Where((Station x) => x.UnitObject is Aircraft))
			{
				_ = item.UnitObject;
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
				continue;
			}
			num = float.MaxValue;
			foreach (Station item2 in Stations.Where((Station x) => x.UnitObject is Helicopter))
			{
				_ = item2.UnitObject;
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
				continue;
			}
			num = float.MaxValue;
			foreach (Station item3 in Stations.Where((Station x) => x.UnitObject is Vessel))
			{
				if (item3.Role != StationRole.ASW || item3.UnitObject._ai._contactsToInvestigate.Count > 0)
				{
					continue;
				}
				ObjectBase unitObject = item3.UnitObject;
				if (unitObject._obp._sonarSystems.Count != 0 && !unitObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Carrier) && !unitObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.CVS) && !unitObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.CVL) && !unitObject._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.ASuW_VLR))
				{
					float sqrMagnitude = (unitObject.transform.position - key.transform.position).sqrMagnitude;
					if (!(sqrMagnitude > num2) && num > sqrMagnitude)
					{
						num = sqrMagnitude;
						objectBase = unitObject;
					}
				}
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
				continue;
			}
			foreach (Station item4 in Stations.Where((Station x) => x.UnitObject is Submarine))
			{
				_ = item4.UnitObject;
			}
			if (objectBase != null)
			{
				objectBase._ai._contactsToInvestigate.Add(key);
				key._ai._enemiesInvestigatingThisUnit.Add(objectBase);
			}
		}
	}

	public void FormUpConvoy(bool teleportToPositions = false, bool rotateStationsAroundLeader = true)
	{
		RotateStationsAroundLeader = rotateStationsAroundLeader;
		List<ObjectBase> list = new List<ObjectBase>();
		List<ObjectBase> list2 = new List<ObjectBase>();
		List<ObjectBase> list3 = new List<ObjectBase>();
		foreach (Station station in Stations)
		{
			if (station != LeaderStation && !(station.UnitObject == null) && station.UnitObject is Vessel vessel)
			{
				if (vessel._obp._unitRoles[0] == ObjectBaseParameters.UnitRoles.RAS || vessel._obp._unitRoles[0] == ObjectBaseParameters.UnitRoles.Merchant || vessel._obp._unitRoles[0] == ObjectBaseParameters.UnitRoles.Landing || vessel._obp._unitRoles[0] == ObjectBaseParameters.UnitRoles.Transport || vessel._obp._unitRoles[0] == ObjectBaseParameters.UnitRoles.Passenger || vessel._obp._unitRoles[0] == ObjectBaseParameters.UnitRoles.SmallCivilian)
				{
					list.Add(vessel);
					continue;
				}
				if (vessel._obp._unitRoles[0] == ObjectBaseParameters.UnitRoles.ASuW || vessel._obp._unitRoles[0] == ObjectBaseParameters.UnitRoles.ASuW_VLR || vessel._obp._unitRoles[0] == ObjectBaseParameters.UnitRoles.ASW || vessel._obp._unitRoles[0] == ObjectBaseParameters.UnitRoles.AAW)
				{
					list2.Add(vessel);
					continue;
				}
				DM.I($"Adding `{vessel.getUIDAndName()}` to type Other. Role = `{vessel._obp._unitRoles[0]}`");
				list3.Add(vessel);
			}
		}
		if (list.Count < 1)
		{
			Debug.LogError("Can not form convoy - no transports found.");
			return;
		}
		float num = Spacing * 27.559496f;
		float num2 = 0.5f * num;
		int num3 = 2;
		int num4 = 3;
		if (list.Count != 7)
		{
			num4 = (int)Math.Ceiling((Mathf.Sqrt(num3 * num3 + 4 * list.Count) - (float)num3) / 2f);
		}
		int[] array = new int[num4];
		int num5 = list.Count / num4;
		int num6 = list.Count % num4;
		for (int i = 0; i < num4; i++)
		{
			array[i] = ((i < num6) ? (num5 + 1) : num5);
		}
		int num7 = 0;
		bool flag = false;
		float num8 = 0f;
		float num9 = ((num4 % 2 == 0) ? (num2 / 2f) : 0f);
		float num10;
		for (int j = 0; j < num4; j++)
		{
			if (j > 0)
			{
				num8 = num2 * (float)((j - 1) / 2 + 1);
				if ((j - 1) % 2 == 0)
				{
					num8 = 0f - num8;
				}
			}
			num8 += num9;
			for (int k = 0; k < array[j]; k++)
			{
				num10 = (float)(-(k + 1)) * num;
				if (num6 > 0 && array[j] == num5)
				{
					num10 -= num / 2f;
				}
				Vector3 vector = new Vector3(num8, 0f, num10);
				if (teleportToPositions)
				{
					list[num7].transform.position = LeaderStation.UnitObject.transform.position + Quaternion.Euler(0f, LeaderStation.UnitObject.transform.eulerAngles.y, 0f) * vector;
					list[num7].SetTeleportProtection();
				}
				vector.z += 5.5118995f;
				if (!RotateStationsAroundLeader)
				{
					vector = Quaternion.Euler(0f, LeaderStation.UnitObject.transform.eulerAngles.y, 0f) * vector;
				}
				ChangeStationPosition(GetStationForUnit(list[num7]), vector, SetHeight);
				num7++;
				if (num7 == list.Count)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				break;
			}
		}
		num10 = 0f - num;
		for (int l = 0; l < list2.Count; l++)
		{
			if (list2.Count > 1)
			{
				num8 = num2 * (float)(num4 / 2 + 1);
				if (num4 % 2 == 0)
				{
					num8 += num2 * 0.5f;
				}
				num10 = (float)(-(array[0] / 2 + 1)) * num;
			}
			else
			{
				num8 = num2 * (float)(num4 / 2 + 1);
				if (num4 % 2 == 0)
				{
					num8 += num2 * 0.5f;
				}
				num8 = 0f - num8;
				num10 = (float)(-(array[0] / 2 + 1)) * num;
			}
			Vector3 vector = new Vector3(num8, 0f, num10);
			DM.I($"[{list2[l].getUIDAndName()}]: offset = ({vector.x} | {vector.y} | {vector.z})");
			if (teleportToPositions)
			{
				list2[l].transform.position = LeaderStation.UnitObject.transform.position + Quaternion.Euler(0f, LeaderStation.UnitObject.transform.eulerAngles.y, 0f) * vector;
				list2[l].SetTeleportProtection();
			}
			vector.z += 5.5118995f;
			if (!RotateStationsAroundLeader)
			{
				vector = Quaternion.Euler(0f, LeaderStation.UnitObject.transform.eulerAngles.y, 0f) * vector;
			}
			ChangeStationPosition(GetStationForUnit(list2[l]), vector, SetHeight);
		}
		DM.I($"Convoy consists of {Stations.Count} units: 1 x Leader, {list.Count} x transport, {list2.Count} x escort and {list3.Count} x other vessels");
	}

	public void PerformConsequetiveTurn()
	{
		if (!RotateStationsAroundLeader || LeaderStation.UnitObject is Aircraft)
		{
			return;
		}
		Vector3 targetPosition = Utils.longLatToLocalV3(LeaderStation.UnitObject.CommandPosition.Value, Globals._currentCenterTile);
		float f = 0f - Utils.calculateYawToTarget(LeaderStation.UnitObject.transform.forward, LeaderStation.UnitObject.transform.position, targetPosition);
		foreach (Station station in Stations)
		{
			if (station != LeaderStation && !(station.UnitObject == null) && !station.UnitActsIndependently.Value && !station.UnitObject.IsAirUnit)
			{
				Vector3 vector = LeaderStation.UnitObject.transform.position + LeaderStation.UnitObject.transform.right * station.StationPosition.x;
				if (Mathf.Sign(station.StationPosition.x) != Mathf.Sign(f))
				{
					vector += LeaderStation.UnitObject.transform.forward * Math.Abs(station.StationPosition.x);
					station.UnitObject._performingOuterTurn = true;
				}
				else
				{
					vector -= LeaderStation.UnitObject.transform.forward * Math.Abs(station.StationPosition.x);
				}
				station.UnitObject._performingFormationTurn = true;
				station.UnitObject.RemoveWaypoints();
				if (Mathf.Abs(Utils.calculateYawToTarget(station.UnitObject.transform.forward, station.UnitObject.transform.position, vector)) < 90f)
				{
					station.UnitObject.setWaypointTask(Utils.worldPositionFromUnityToLongLat(vector, Globals._currentCenterTile));
				}
				station.UnitObject.SetRelativeWaypointTask(station.StationPosition, LeaderStation.UnitObject, "wp", reachable: false, SetHeight);
			}
		}
	}

	public void RaiseAlert()
	{
		if (_isOnAlert)
		{
			return;
		}
		_isOnAlert = true;
		foreach (Station station in Stations)
		{
			station.UnitObject._ai.CheckForRaiseAlert(forceAlert: true);
		}
	}

	public void CancelAlert(bool panic = false)
	{
		if (!_isOnAlert)
		{
			return;
		}
		_isOnAlert = false;
		foreach (Station station in Stations)
		{
			station.UnitObject._ai.CancelAlert();
		}
	}

	public void CheckForRadarScan(RadarParameters.Role radarRole)
	{
		_radarCapableStations.Clear();
		_aawStations.Clear();
		_aswStations.Clear();
		foreach (Station station2 in Stations)
		{
			if (!(station2.UnitObject == null) && !station2.UnitObject.IsDestroyed && station2.UnitObject is Vessel && !station2.UnitObject._ai._excludeFromAIRadarRoutine && station2.UnitObject.HasRadar(radarRole))
			{
				_radarCapableStations.Add(station2);
				if (station2.Role == StationRole.AAW)
				{
					_aawStations.Add(station2);
				}
				else if (station2.Role == StationRole.ASW)
				{
					_aswStations.Add(station2);
				}
			}
		}
		if (_radarCapableStations.Count == 0)
		{
			return;
		}
		Station station = null;
		station = ((_aawStations.Count > 1) ? GetStationForRadar(_aawStations) : ((_aswStations.Count <= 0) ? GetStationForRadar(_radarCapableStations) : GetStationForRadar(_aawStations.Concat(_aswStations).ToList())));
		if (station != null)
		{
			station.UnitObject.EnableRadars(radarRole);
			switch (radarRole)
			{
			case RadarParameters.Role.Air:
				station.UnitObject._ai._turnOffAirSearchRadarsTime = 3.0;
				break;
			case RadarParameters.Role.SurfaceAndAir:
				station.UnitObject._ai._turnOffAirSearchRadarsTime = GameTime.missionElapsedTime + 3.0;
				station.UnitObject._ai._turnOffSurfaceSearchRadarsTime = GameTime.missionElapsedTime + 3.0;
				break;
			default:
				station.UnitObject._ai._turnOffSurfaceSearchRadarsTime = GameTime.missionElapsedTime + 20.0;
				break;
			}
		}
	}

	private Station GetStationForRadar(List<Station> stations)
	{
		Station station = stations[Globals._rnd.Next(0, stations.Count)];
		if (station == _lastRadarStation)
		{
			station = stations[Globals._rnd.Next(0, stations.Count)];
		}
		if (station == _lastRadarStation)
		{
			station = stations[Globals._rnd.Next(0, stations.Count)];
		}
		return station;
	}

	public int GetUnassignedASWStationsCount()
	{
		int num = 0;
		foreach (Station station in Stations)
		{
			if (station.Role == StationRole.ASW && station.UnitObject._ai._contactsToInvestigate.Count == 0)
			{
				num++;
			}
		}
		return num;
	}

	public TrulyObservableCollection<ContextMenuItem> GetContextMenuItems()
	{
		TrulyObservableCollection<ContextMenuItem> trulyObservableCollection = new TrulyObservableCollection<ContextMenuItem>();
		TrulyObservableCollection<ContextMenuItem> trulyObservableCollection2 = new TrulyObservableCollection<ContextMenuItem>();
		trulyObservableCollection2.Add(new ContextMenuItem("ContextMenu", "Navigate_Remove_Waypoints", null, new DelegateCommand(delegate
		{
			if (!(LeaderStation.UnitObject == null) && !(LeaderStation.UnitObject is Aircraft { _isInFlight: false }) && !(LeaderStation.UnitObject is Helicopter { _isInFlight: false }))
			{
				LeaderStation.UnitObject.RemoveWaypoints();
			}
		})));
		trulyObservableCollection2.Add(new ContextMenuItem("ContextMenu", "Navigate_Resume_Course", null, new DelegateCommand(delegate
		{
			if (!(LeaderStation.UnitObject == null))
			{
				LeaderStation.UnitObject._playerCommandOverride = false;
				LeaderStation.UnitObject.CheckForPlayerAbort();
			}
		})));
		if (LeaderStation != null && LeaderStation.UnitObject != null && LeaderStation.UnitObject.ViewModel is ObjectBaseViewModel objectBaseViewModel)
		{
			if (objectBaseViewModel.ObjectZVelocityViewModel != null && objectBaseViewModel.ObjectZVelocityViewModel.ObjectSpeeds != null && objectBaseViewModel.ObjectZVelocityViewModel.ObjectSpeeds.Count > 0)
			{
				trulyObservableCollection2.Add(new ContextMenuItem("ContextMenu", "Navigate_Change_Speed", objectBaseViewModel.ObjectZVelocityViewModel.ObjectSpeeds));
			}
			if (objectBaseViewModel._objectBase._type == ObjectBase.ObjectType.Submarine && objectBaseViewModel.ObjectYPositionViewModel != null && objectBaseViewModel.ObjectYPositionViewModel.ObjectYPositions.Count > 0)
			{
				trulyObservableCollection2.Add(new ContextMenuItem("ContextMenu", "SetDepth", objectBaseViewModel.ObjectYPositionViewModel.ObjectYPositions));
			}
			else if ((objectBaseViewModel._objectBase._type == ObjectBase.ObjectType.Helicopter || objectBaseViewModel._objectBase._type == ObjectBase.ObjectType.Aircraft) && objectBaseViewModel.ObjectYPositionViewModel != null && objectBaseViewModel.ObjectYPositionViewModel.ObjectYPositions.Count > 0)
			{
				trulyObservableCollection2.Add(new ContextMenuItem("ContextMenu", "SetAltitude", objectBaseViewModel.ObjectYPositionViewModel.ObjectYPositions));
			}
		}
		trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "Navigate", trulyObservableCollection2));
		TrulyObservableCollection<ContextMenuItem> items = new TrulyObservableCollection<ContextMenuItem>
		{
			new ContextMenuItem("BottomRow", "EMCON", null, new DelegateCommand(delegate
			{
				foreach (Station item2 in Stations.Where((Station x) => x.UnitObject != null))
				{
					item2.UnitObject.setEMCON(emcon: true);
				}
			})),
			new ContextMenuItem("ContextMenu", "Sensors_AirSearch", null, new DelegateCommand(delegate
			{
				foreach (Station item3 in Stations.Where((Station x) => x.UnitObject != null))
				{
					item3.UnitObject.EnableAirSearchRadars();
				}
			})),
			new ContextMenuItem("ContextMenu", "Sensors_SurfaceSearch", null, new DelegateCommand(delegate
			{
				foreach (Station item4 in Stations.Where((Station x) => x.UnitObject != null))
				{
					item4.UnitObject.EnableSurfaceSearchRadars();
				}
			})),
			new ContextMenuItem("ContextMenu", "Sensors_ActiveSonar", null, new DelegateCommand(delegate
			{
				foreach (Station item5 in Stations.Where((Station x) => x.UnitObject != null))
				{
					item5.UnitObject.EnableActiveSonars();
				}
			}))
		};
		trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "FormationSensorOrders", items));
		if (LeaderStation != null && LeaderStation.UnitObject != null && LeaderStation.UnitObject.IsAirUnit)
		{
			TrulyObservableCollection<ContextMenuItem> items2 = new TrulyObservableCollection<ContextMenuItem>
			{
				new ContextMenuItem("ContextMenu", "FormationControlMode.FollowLeader", null, isChecked: GetControlModeMatch(ControlMode.FollowLeader), command: new DelegateCommand(delegate
				{
					SelectedControlMode = ControlMode.FollowLeader;
				})),
				new ContextMenuItem("ContextMenu", "FormationControlMode.Synchronized", null, isChecked: GetControlModeMatch(ControlMode.Synchronized), command: new DelegateCommand(delegate
				{
					SelectedControlMode = ControlMode.Synchronized;
				})),
				new ContextMenuItem("ContextMenu", "FormationControlMode.Local", null, isChecked: GetControlModeMatch(ControlMode.Local), command: new DelegateCommand(delegate
				{
					SelectedControlMode = ControlMode.Local;
				}))
			};
			trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "FormationControlMode", items2));
			trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "RenameFormation", null, new DelegateCommand(delegate
			{
				InputBoxViewModel _formationNameEditor = new InputBoxViewModel();
				_formationNameEditor.AcknowledgeCommand = new DelegateCommand(delegate(object x)
				{
					string text = (string)x;
					if (!string.IsNullOrEmpty(text))
					{
						Name = text;
					}
					WindowHostCommands.CloseWindowCommand.Execute(_formationNameEditor, MainGameView.Instance);
					_formationNameEditor.AcknowledgeCommand = null;
					_formationNameEditor = null;
				});
				_formationNameEditor.Message = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "RenameFormationInstruction");
				_formationNameEditor.Input = Name;
				WindowHostCommands.OpenWindowCommand.Execute(_formationNameEditor, MainGameView.Instance);
			})));
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection3 = new TrulyObservableCollection<ContextMenuItem>();
			foreach (FormationPattern item in Enum.GetValues(typeof(FormationPattern)))
			{
				if (item != FormationPattern.Battlegroup && item != FormationPattern.None && item != FormationPattern.Convoy && (!(LeaderStation.UnitObject != null) || !LeaderStation.UnitObject.IsAirUnit || item != FormationPattern.Loose))
				{
					trulyObservableCollection3.Add(new ContextMenuItem("ContextMenu", $"FormationPattern.{item}", null, new DelegateCommand(delegate
					{
						Reform(item, Spacing);
					})));
				}
			}
			trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "FormationPattern", trulyObservableCollection3));
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection4 = new TrulyObservableCollection<ContextMenuItem>();
			foreach (FormationSpacing value in Enum.GetValues(typeof(FormationSpacing)))
			{
				float spacing = 0.1f;
				switch (value)
				{
				case FormationSpacing.Parade:
					spacing = 0.05f;
					break;
				case FormationSpacing.Tight:
					spacing = 0.1f;
					break;
				case FormationSpacing.Spread:
					spacing = 0.5f;
					break;
				case FormationSpacing.Loose:
					spacing = 1f;
					break;
				}
				trulyObservableCollection4.Add(new ContextMenuItem("ContextMenu", $"FormationSpacing.{value}", null, new DelegateCommand(delegate
				{
					Reform(Pattern, spacing);
				})));
			}
			trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "FormationSpacing", trulyObservableCollection4));
		}
		else if (LeaderStation != null && LeaderStation.UnitObject != null)
		{
			trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "EditFormation", null, new DelegateCommand(delegate
			{
				WindowHostCommands.OpenWindowCommand.Execute((LeaderStation.UnitObject.ViewModel as ObjectBaseViewModel).FormationEdit, MainGameView.Instance);
			})));
		}
		if (LeaderStation != null && LeaderStation.UnitObject != null && (LeaderStation.UnitObject._type == ObjectBase.ObjectType.Aircraft || LeaderStation.UnitObject._type == ObjectBase.ObjectType.Helicopter))
		{
			trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "Orders_Rebase", null, new DelegateCommand(delegate
			{
				if (!(LeaderStation.UnitObject == null) && LeaderStation.UnitObject.AcceptsOrdersFromPlayer)
				{
					Log.Info("Context Menu: Orders -> Rebase selected for '" + LeaderStation.UnitObject.getUIDAndName() + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Taskforce\\UnitFormation.cs", 2295, "GetContextMenuItems");
					Singleton<MouseControlState>.Instance.setActionToPerform(MouseControlState.ActionToPerform.Rebase);
					Globals._mainGameViewModel.StateManager.PushSingleState(new RebaseState(Globals._mainGameViewModel.StateManager, LeaderStation.UnitObject));
				}
			})));
			trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "Orders_ReturnToBase", null, new DelegateCommand(delegate
			{
				if (!(LeaderStation.UnitObject == null) && LeaderStation.UnitObject.AcceptsOrdersFromPlayer)
				{
					ObjectBase homeBase = LeaderStation.UnitObject.getHomeBase();
					if (!(homeBase == null))
					{
						Log.Info("Context Menu: Orders -> RTB selected for '" + LeaderStation.UnitObject.getUIDAndName() + "' returning to '" + homeBase.getUIDAndName() + "'", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Taskforce\\UnitFormation.cs", 2309, "GetContextMenuItems");
						if (homeBase != null)
						{
							DM.I(LeaderStation.UnitObject.getUIDAndName() + " returns to base: " + homeBase.getUIDAndName());
							LeaderStation.UnitObject.setOrder(Order.Type.ReturnToBase, homeBase, displayOrderText: true);
						}
						else
						{
							DM.I(LeaderStation.UnitObject.getUIDAndName() + " base is null!");
						}
					}
				}
			})));
		}
		TrulyObservableCollection<ContextMenuItem> items3 = new TrulyObservableCollection<ContextMenuItem>
		{
			new ContextMenuItem("BottomRow", "WeaponStatusFree", null, new DelegateCommand(delegate
			{
				foreach (Station item6 in Stations.Where((Station x) => x.UnitObject != null))
				{
					item6.UnitObject._weaponStatus = ObjectBase.WeaponStatus.Free;
				}
			})),
			new ContextMenuItem("BottomRow", "WeaponStatusTight", null, new DelegateCommand(delegate
			{
				foreach (Station item7 in Stations.Where((Station x) => x.UnitObject != null))
				{
					item7.UnitObject._weaponStatus = ObjectBase.WeaponStatus.Tight;
				}
			})),
			new ContextMenuItem("BottomRow", "WeaponStatusHold", null, new DelegateCommand(delegate
			{
				foreach (Station item8 in Stations.Where((Station x) => x.UnitObject != null))
				{
					item8.UnitObject.CeaseFire(report: false);
					item8.UnitObject._weaponStatus = ObjectBase.WeaponStatus.Hold;
				}
			}))
		};
		trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "FormationAirDefenceOrders", items3));
		trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "FormationCeaseFireOrder", null, new DelegateCommand(delegate
		{
			CeaseFire();
		})));
		trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "FormationRecallAllOrder", null, new DelegateCommand(delegate
		{
			AllReturnToFormation();
		})));
		trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "DisbandFormation", null, new DelegateCommand(delegate
		{
			Disband();
		})));
		return trulyObservableCollection;
		ReadOnlyReactiveProperty<bool> GetControlModeMatch(ControlMode controlMode)
		{
			return new ReadOnlyReactiveProperty<bool>(new ReactiveProperty<bool>(SelectedControlMode == controlMode));
		}
	}
}
