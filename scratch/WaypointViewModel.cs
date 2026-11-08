using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NodeCanvas.Framework;
using ObservableComputations;
using SeaPower;
using SeapowerUI.ViewModels;
using UniRx;

namespace SeapowerUI;

public class WaypointViewModel : ITaskViewModel, IDisposable, IMapSelectable, INotifyPropertyChanged, IContextMenuObject
{
	private OcConsumer _consumer = new OcConsumer();

	private CompositeDisposable _disposer = new CompositeDisposable();

	private GeoPosition startPosition;

	private bool _isLive;

	private NotifyPropertyChangedStorage<bool> _selectedStorage;

	private float _startSpeed;

	private string _etaText;

	private List<GeoPosition> _livePath;

	private Swapping<List<GeoPosition>> swapper = new Swapping<List<GeoPosition>>(new List<GeoPosition>(2), new List<GeoPosition>(2));

	public VisualActionTask _task;

	private bool _disposed;

	public static int InstanceCount { get; private set; }

	public GeoHandleViewModel WaypointHandle { get; }

	public bool Selected
	{
		get
		{
			return _selectedStorage.Value;
		}
		set
		{
			if (_selectedStorage.SetField(this.PropertyChanged, this, value, "Selected") && value)
			{
				this.OnSelected?.Invoke(value);
			}
		}
	}

	public GeoPosition StartPosition
	{
		get
		{
			return startPosition;
		}
		private set
		{
			startPosition = value;
			OnPropertyChanged("StartPosition");
		}
	}

	public float StartSpeed
	{
		get
		{
			return _startSpeed;
		}
		private set
		{
			_startSpeed = value;
			OnPropertyChanged("StartSpeed");
		}
	}

	public string ETAText
	{
		get
		{
			return _etaText;
		}
		private set
		{
			_etaText = value;
			OnPropertyChanged("ETAText");
		}
	}

	public List<GeoPosition> LivePath
	{
		get
		{
			return _livePath;
		}
		private set
		{
			_livePath = value;
			OnPropertyChanged("LivePath");
		}
	}

	public float StrokeThickness { get; } = UI_Parameters.TM_WaypointLineThickness;

	public VisualActionTask Task { get; }

	public string DipIcon
	{
		get
		{
			if (_task == null || !_task._setTowedSonars.setSonars || !_task._setTowedSonars.enable)
			{
				return "";
			}
			return "◎";
		}
	}

	public TrulyObservableCollection<ContextMenuItem> ObjectSpeeds { get; } = new TrulyObservableCollection<ContextMenuItem>();

	public event Action StateDirty;

	public event PropertyChangedEventHandler PropertyChanged;

	public event Action<bool> OnSelected;

	public WaypointViewModel(GoToWaypointTask task)
	{
		WaypointViewModel waypointViewModel = this;
		_task = task;
		InstanceCount++;
		BBParameter<GeoPosition> wp = task._waypointGeoPos;
		Computing<GeoPosition> computing = new Computing<GeoPosition>(() => wp.value)
		{
			SetValueRequestHandler = delegate(GeoPosition p)
			{
				task._waypointGeoPos.value = p;
			}
		};
		computing.For(_consumer).PropertyChanged += delegate(object p, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "Value")
			{
				waypointViewModel.StateDirty?.Invoke();
			}
		};
		WaypointHandle = new GeoHandleViewModel(computing);
		WaypointHandle.ContextMenuOwner = this;
		WaypointHandle.Position.ScalarProcessing(delegate
		{
			waypointViewModel.UpdateLivePath();
		}).For(_consumer);
		new Binding<bool>(new Computing<bool>(() => WaypointHandle.Selected), delegate(bool s, Binding<bool> b)
		{
			waypointViewModel.Selected = s;
		}).For(_consumer);
		new Binding<bool>(new Computing<bool>(() => Selected), delegate(bool s, Binding<bool> b)
		{
			waypointViewModel.WaypointHandle.Selected = s;
		}).For(_consumer);
		Task = task;
		ETAText = "N/A";
		LivePath = swapper.Next();
	}

	public WaypointViewModel(GoToRelativePoint task)
	{
		WaypointViewModel waypointViewModel = this;
		_task = task;
		InstanceCount++;
		Computing<GeoPosition> computing = new Computing<GeoPosition>(() => task.targetPosition.value);
		Computing<GeoPosition> computing2 = computing.For(_consumer);
		computing.SetValueRequestHandler = delegate
		{
		};
		computing2.PropertyChanged += delegate(object p, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "Value")
			{
				waypointViewModel.StateDirty?.Invoke();
			}
		};
		WaypointHandle = new GeoHandleViewModel(computing);
		WaypointHandle.ContextMenuOwner = this;
		new Binding<bool>(new Computing<bool>(() => WaypointHandle.Selected), delegate(bool s, Binding<bool> b)
		{
			waypointViewModel.Selected = s;
		}).For(_consumer);
		new Binding<bool>(new Computing<bool>(() => Selected), delegate(bool s, Binding<bool> b)
		{
			waypointViewModel.WaypointHandle.Selected = s;
		}).For(_consumer);
		Task = task;
		ETAText = "N/A";
	}

	public WaypointViewModel(GoToRelativeToStationPoint task)
	{
		_task = task;
		InstanceCount++;
		Computing<GeoPosition> targetPosition = task.TargetPosition;
		Computing<GeoPosition> computing = targetPosition.For(_consumer);
		targetPosition.SetValueRequestHandler = delegate
		{
		};
		computing.PropertyChanged += delegate(object p, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "Value")
			{
				this.StateDirty?.Invoke();
			}
		};
		WaypointHandle = new GeoHandleViewModel(targetPosition);
		WaypointHandle.ContextMenuOwner = this;
		new Binding<bool>(new Computing<bool>(() => WaypointHandle.Selected), delegate(bool s, Binding<bool> b)
		{
			Selected = s;
		}).For(_consumer);
		new Binding<bool>(new Computing<bool>(() => Selected), delegate(bool s, Binding<bool> b)
		{
			WaypointHandle.Selected = s;
		}).For(_consumer);
		Task = task;
		ETAText = "N/A";
	}

	public TrulyObservableCollection<ContextMenuItem> GetContextMenuItems()
	{
		if (!(_task is GoToWaypointTask) && !(_task is AttackAtWaypoint))
		{
			return null;
		}
		TrulyObservableCollection<ContextMenuItem> trulyObservableCollection = new TrulyObservableCollection<ContextMenuItem>();
		ObjectEffectorsViewModel objectEffectorsViewModel = new ObjectEffectorsViewModel(_task._baseObject, isWaypointEdit: true);
		OnPropertyChanged("ObjectEffectors");
		if (objectEffectorsViewModel != null && objectEffectorsViewModel.WeaponEntries.Count > 0)
		{
			trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "EngageWithAtWaypoint", objectEffectorsViewModel.WeaponEntries));
		}
		if (_task is AttackAtWaypoint attackAtWaypoint && attackAtWaypoint._ammo._ap._type != Ammunition.Type.Chaff && attackAtWaypoint._ammo._ap._type != Ammunition.Type.Noisemaker && attackAtWaypoint._ammo._ap._type != Ammunition.Type.Fueltank && attackAtWaypoint._ammo._ap._type != Ammunition.Type.Sonobuoy)
		{
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection2 = new TrulyObservableCollection<ContextMenuItem>();
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection3 = new TrulyObservableCollection<ContextMenuItem>();
			trulyObservableCollection3.Add(GetSalvoAmountCMItem(1));
			trulyObservableCollection3.Add(GetSalvoAmountCMItem(2));
			trulyObservableCollection3.Add(GetSalvoAmountCMItem(3));
			trulyObservableCollection3.Add(GetSalvoAmountCMItem(4));
			trulyObservableCollection3.Add(GetSalvoAmountCMItem(6));
			trulyObservableCollection3.Add(GetSalvoAmountCMItem(8));
			trulyObservableCollection3.Add(GetSalvoAmountCMItem(10));
			trulyObservableCollection3.Add(GetSalvoAmountCMItem(16));
			trulyObservableCollection3.Add(GetSalvoAmountCMItem(20));
			string text = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "EngageParameters_Salvo");
			text = text.Replace("${Salvo}", $"{attackAtWaypoint._salvo}");
			trulyObservableCollection2.Add(new ContextMenuItem(text, trulyObservableCollection3));
			if (_task._baseObject.IsFormationLeader)
			{
				if (attackAtWaypoint._formationAttack)
				{
					TrulyObservableCollection<ContextMenuItem> trulyObservableCollection4 = new TrulyObservableCollection<ContextMenuItem>();
					trulyObservableCollection4.Add(GetSalvoTypeCMItem(EngageTask.SalvoType.PerLauncher));
					trulyObservableCollection4.Add(GetSalvoTypeCMItem(EngageTask.SalvoType.PerTarget));
					trulyObservableCollection4.Add(GetSalvoTypeCMItem(EngageTask.SalvoType.InTotal));
					trulyObservableCollection2.Add(new ContextMenuItem("ContextMenu", "EngageParameters_SalvoType", trulyObservableCollection4));
				}
				trulyObservableCollection2.Add(GetFormationAttackCMItem());
			}
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection5 = new TrulyObservableCollection<ContextMenuItem>();
			trulyObservableCollection5.Add(GetAreaRadiusCMItem(-1f));
			trulyObservableCollection5.Add(GetAreaRadiusCMItem(0.5f));
			trulyObservableCollection5.Add(GetAreaRadiusCMItem(1f));
			trulyObservableCollection5.Add(GetAreaRadiusCMItem(2f));
			trulyObservableCollection5.Add(GetAreaRadiusCMItem(5f));
			trulyObservableCollection5.Add(GetAreaRadiusCMItem(10f));
			trulyObservableCollection5.Add(GetAreaRadiusCMItem(15f));
			trulyObservableCollection5.Add(GetAreaRadiusCMItem(20f));
			if (_task._baseObject is Aircraft)
			{
				trulyObservableCollection5.Add(GetAreaRadiusCMItem(30f));
				trulyObservableCollection5.Add(GetAreaRadiusCMItem(50f));
			}
			string text2 = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "EngageParameters_AreaRadius");
			string newValue = ((attackAtWaypoint._areaRadius < 0f) ? Singleton<LanguageResourceHandler>.Instance.getText("YesNo", "False") : string.Format("{0:0.#} {1}", attackAtWaypoint._areaRadius * 0.036285132f, Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "NauticalMiles")));
			text2 = text2.Replace("${Radius}", newValue);
			trulyObservableCollection2.Add(new ContextMenuItem(text2, trulyObservableCollection5));
			trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "EngageParameters", trulyObservableCollection2));
		}
		TrulyObservableCollection<ContextMenuItem> trulyObservableCollection6 = new TrulyObservableCollection<ContextMenuItem>();
		trulyObservableCollection6.Add(new ContextMenuItem("ContextMenu", "PrecisePosition", null, new DelegateCommand(delegate
		{
			PreciseWaypointViewModel parameter = new PreciseWaypointViewModel(this);
			WindowHostCommands.OpenWindowCommand.Execute(parameter, MainGameView.Instance);
		})));
		if (_task._baseObject is Vessel || _task._baseObject is Submarine)
		{
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection7 = new TrulyObservableCollection<ContextMenuItem>();
			ObjectSpeed.SpeedSettings[] array = (ObjectSpeed.SpeedSettings[])Enum.GetValues(typeof(ObjectSpeed.SpeedSettings));
			for (int num = array.Length - 1; num >= 0; num--)
			{
				if (array[num] != ObjectSpeed.SpeedSettings.BackFull)
				{
					trulyObservableCollection7.Add(GetVelocityCMItem(array[num]));
				}
			}
			trulyObservableCollection6.Add(new ContextMenuItem("ContextMenu", "Navigate_Change_Speed", trulyObservableCollection7));
		}
		else if (_task._baseObject is Aircraft aircraft)
		{
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection8 = new TrulyObservableCollection<ContextMenuItem>();
			if (aircraft.Ap._speedValuesInMach.Length > 4)
			{
				trulyObservableCollection8.Add(GetVelocityCMItem(ObjectSpeed.SpeedSettings.Ahead_Flank));
			}
			trulyObservableCollection8.Add(GetVelocityCMItem(ObjectSpeed.SpeedSettings.Ahead_Full));
			trulyObservableCollection8.Add(GetVelocityCMItem(ObjectSpeed.SpeedSettings.Ahead_Standard));
			trulyObservableCollection8.Add(GetVelocityCMItem(ObjectSpeed.SpeedSettings.Ahead_2_3));
			trulyObservableCollection6.Add(new ContextMenuItem("ContextMenu", "Navigate_Change_Speed", trulyObservableCollection8));
		}
		if (_task._baseObject is Aircraft || _task._baseObject is Helicopter)
		{
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection9 = new TrulyObservableCollection<ContextMenuItem>();
			float[] array2 = new float[1];
			if (_task._baseObject is Aircraft aircraft2)
			{
				array2 = aircraft2.Ap._presetAltitudes;
			}
			else if (_task._baseObject is Helicopter helicopter)
			{
				array2 = helicopter._hp._presetAltitudes;
			}
			for (int num2 = array2.Length - 1; num2 >= 0; num2--)
			{
				trulyObservableCollection9.Add(GetAltitudeCMItem(array2, num2));
			}
			trulyObservableCollection6.Add(new ContextMenuItem("ContextMenu", "SetAltitude", trulyObservableCollection9));
		}
		else if (_task._baseObject is Submarine || _task._baseObject is Biologic)
		{
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection10 = new TrulyObservableCollection<ContextMenuItem>();
			foreach (ObjectDepth.DepthSettings value in Enum.GetValues(typeof(ObjectDepth.DepthSettings)))
			{
				trulyObservableCollection10.Add(GetDepthCMItem(value));
			}
			trulyObservableCollection6.Add(new ContextMenuItem("ContextMenu", "SetDepth", trulyObservableCollection10));
		}
		trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "Navigate", trulyObservableCollection6));
		TrulyObservableCollection<ContextMenuItem> trulyObservableCollection11 = new TrulyObservableCollection<ContextMenuItem>();
		trulyObservableCollection11.Add(new ContextMenuItem("BottomRow", "EMCON", null, isChecked: new ReactiveProperty<bool>(_task._setEmcon).ToReadOnlyReactiveProperty(), command: new DelegateCommand(delegate
		{
			_task.UpdateEmcon();
		})));
		if (_task._baseObject.HasAirSearchRadar())
		{
			string text3 = (_task._baseObject.HasAirAndSurfaceSurfaceSearchRadar() ? Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_AirAndSurfaceSearch") : Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_AirSearch"));
			if (_task._setAirSearchRadars.setRadars)
			{
				text3 += ": ";
				text3 = ((!_task._setAirSearchRadars.enable) ? (text3 + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off")) : (text3 + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On")));
			}
			trulyObservableCollection11.Add(new ContextMenuItem(text3, (TrulyObservableCollection<ContextMenuItem>)null, isChecked: new ReactiveProperty<bool>(_task._setAirSearchRadars.setRadars).ToReadOnlyReactiveProperty(), command: (ICommand)new DelegateCommand(delegate
			{
				_task.UpdateAirSearchRadars();
			}), enabled: (ReadOnlyReactiveProperty<bool>)null, tooltip: (string)null, shortcut: (string)null, shortcutLabel: (string)null, secondaryShortcut: (string)null, secondaryShortcutLabel: (string)null));
		}
		if (_task._baseObject.HasSurfaceSearchRadar())
		{
			string text3 = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_SurfaceSearch");
			if (_task._setSurfaceSearchRadars.setRadars)
			{
				text3 += ": ";
				text3 = ((!_task._setSurfaceSearchRadars.enable) ? (text3 + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off")) : (text3 + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On")));
			}
			trulyObservableCollection11.Add(new ContextMenuItem(text3, (TrulyObservableCollection<ContextMenuItem>)null, isChecked: new ReactiveProperty<bool>(_task._setSurfaceSearchRadars.setRadars).ToReadOnlyReactiveProperty(), command: (ICommand)new DelegateCommand(delegate
			{
				_task.UpdateSurfaceSearchRadars();
			}), enabled: (ReadOnlyReactiveProperty<bool>)null, tooltip: (string)null, shortcut: (string)null, shortcutLabel: (string)null, secondaryShortcut: (string)null, secondaryShortcutLabel: (string)null));
		}
		if (_task._baseObject.HasActiveSonar())
		{
			string text3 = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_ActiveSonar");
			if (_task._setActiveSonars.setSonars)
			{
				text3 += ": ";
				text3 = ((!_task._setActiveSonars.enable) ? (text3 + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off")) : (text3 + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On")));
			}
			trulyObservableCollection11.Add(new ContextMenuItem(text3, (TrulyObservableCollection<ContextMenuItem>)null, isChecked: new ReactiveProperty<bool>(_task._setActiveSonars.setSonars).ToReadOnlyReactiveProperty(), command: (ICommand)new DelegateCommand(delegate
			{
				_task.UpdateActiveSonars();
			}), enabled: (ReadOnlyReactiveProperty<bool>)null, tooltip: (string)null, shortcut: (string)null, shortcutLabel: (string)null, secondaryShortcut: (string)null, secondaryShortcutLabel: (string)null));
		}
		if (_task._baseObject.HasTowedSonar())
		{
			string text3 = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_TowedSonarGeneralized");
			if (_task._setTowedSonars.setSonars)
			{
				text3 += ": ";
				text3 = ((!_task._setTowedSonars.enable) ? (text3 + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off")) : (text3 + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On")));
			}
			trulyObservableCollection11.Add(new ContextMenuItem(text3, (TrulyObservableCollection<ContextMenuItem>)null, isChecked: new ReactiveProperty<bool>(_task._setTowedSonars.setSonars).ToReadOnlyReactiveProperty(), command: (ICommand)new DelegateCommand(delegate
			{
				if (_task._baseObject is Helicopter && !_task._setTowedSonars.setSonars)
				{
					_task.UpdateTowedSonarsTime(120f);
				}
				_task.UpdateTowedSonars();
				_task._isAutogenerated = false;
			}), enabled: (ReadOnlyReactiveProperty<bool>)null, tooltip: (string)null, shortcut: (string)null, shortcutLabel: (string)null, secondaryShortcut: (string)null, secondaryShortcutLabel: (string)null));
			if (_task._setTowedSonars.enable)
			{
				TrulyObservableCollection<ContextMenuItem> trulyObservableCollection12 = new TrulyObservableCollection<ContextMenuItem>();
				foreach (float duration in Globals._towedSonarDurationsList)
				{
					bool initialValue = MathF.Abs(_task._towedSonarsEnabledTime - duration) < 0.5f;
					string text4 = "";
					if (duration < 0f)
					{
						text4 = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Unlimited");
					}
					else
					{
						float num3 = duration;
						int num4 = (int)(num3 / 3600f);
						num3 -= (float)num4 * 3600f;
						int num5 = (int)(num3 / 60f);
						num3 -= (float)num5 * 60f;
						if (num4 > 0)
						{
							text4 += string.Format("{0}{1} ", num4, Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "HourShort"));
						}
						if (num5 > 0)
						{
							text4 += string.Format("{0}{1} ", num5, Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "MinuteShort"));
						}
						if (num4 == 0 && num3 > 1f)
						{
							text4 += string.Format("{0:0}{1}", num3, Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "SecondShort"));
						}
					}
					trulyObservableCollection12.Add(new ContextMenuItem(text4, null, new DelegateCommand(delegate
					{
						_task.UpdateTowedSonarsTime(duration);
					}), null, new ReactiveProperty<bool>(initialValue).ToReadOnlyReactiveProperty()));
				}
				text3 = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_TowedSonarDuration") + ": ";
				trulyObservableCollection11.Add(new ContextMenuItem(text3, trulyObservableCollection12));
			}
		}
		trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "Sensors", trulyObservableCollection11));
		TrulyObservableCollection<ContextMenuItem> trulyObservableCollection13 = new TrulyObservableCollection<ContextMenuItem>();
		trulyObservableCollection13.Add(new ContextMenuItem("ContextMenu", "Orders_Identify", null, new DelegateCommand(delegate
		{
			Globals._mainGameViewModel.StateManager.PushSingleState(new IdentifyState(Globals._mainGameViewModel.StateManager, _task._baseObject, isWaypointEdit: true));
		})));
		trulyObservableCollection13.Add(new ContextMenuItem("ContextMenu", "Orders_ReturnToFormation", null, isChecked: new ReactiveProperty<bool>(_task._returnToFormation).ToReadOnlyReactiveProperty(), command: new DelegateCommand(delegate
		{
			_task.UpdateReturnToFormation();
		})));
		trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "Orders", trulyObservableCollection13));
		trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "AirDefenceState", GetRuleOfEngagemntItems()));
		if (_task == _task._baseObject.GetLastWaypoint() && _task._baseObject.GetNumberOfWaypoints() > 1)
		{
			trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "Waypoints_CreateLoop", null, isChecked: new ReactiveProperty<bool>(_task._waypointsData.Count > 0).ToReadOnlyReactiveProperty(), command: new DelegateCommand(delegate
			{
				if (_task._waypointsData.Count > 0)
				{
					_task._waypointsData.Clear();
				}
				else
				{
					_task._waypointsData = _task._baseObject.ExportWaypoints();
				}
			})));
		}
		return trulyObservableCollection;
	}

	private ContextMenuItem GetSalvoTypeCMItem(EngageTask.SalvoType salvoType)
	{
		bool initialValue = (_task as AttackAtWaypoint)._salvoType == salvoType;
		return new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", $"SalvoType_{salvoType}") ?? "", null, new DelegateCommand(delegate
		{
			(_task as AttackAtWaypoint).UpdateSalvoType(salvoType);
		}), null, new ReactiveProperty<bool>(initialValue).ToReadOnlyReactiveProperty());
	}

	private ContextMenuItem GetSalvoAmountCMItem(int salvoAmount)
	{
		bool initialValue = (_task as AttackAtWaypoint)._salvo == salvoAmount;
		return new ContextMenuItem($"{salvoAmount}", null, new DelegateCommand(delegate
		{
			(_task as AttackAtWaypoint).UpdateSalvoAmount(salvoAmount);
		}), null, new ReactiveProperty<bool>(initialValue).ToReadOnlyReactiveProperty());
	}

	private ContextMenuItem GetFormationAttackCMItem()
	{
		bool isChecked = (_task as AttackAtWaypoint)._formationAttack;
		return new ContextMenuItem(isChecked ? (Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "EngageParameters_FormationAttackYes") ?? "") : (Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "EngageParameters_FormationAttackNo") ?? ""), null, new DelegateCommand(delegate
		{
			(_task as AttackAtWaypoint).UpdateFormationAttack(!isChecked);
		}));
	}

	private ContextMenuItem GetAreaRadiusCMItem(float radiusNmi)
	{
		float radius = radiusNmi * 27.559496f;
		bool initialValue = MathF.Abs((_task as AttackAtWaypoint)._areaRadius - radius) < 0.1f;
		string label = $"{radiusNmi:0.#}";
		if (radiusNmi < 0f)
		{
			initialValue = (_task as AttackAtWaypoint)._areaRadius < 0f;
			label = Singleton<LanguageResourceHandler>.Instance.getText("YesNo", "False");
		}
		return new ContextMenuItem(label, null, new DelegateCommand(delegate
		{
			(_task as AttackAtWaypoint).UpdateAreaRadius(radius);
		}), null, new ReactiveProperty<bool>(initialValue).ToReadOnlyReactiveProperty());
	}

	private ContextMenuItem GetVelocityCMItem(ObjectSpeed.SpeedSettings telegraph)
	{
		bool initialValue = _task._setSpeed.value && _task._speedState.value == telegraph;
		string key = $"Navigate_Change_Speed_{telegraph}";
		if (_task._baseObject is Aircraft)
		{
			if (telegraph == ObjectSpeed.SpeedSettings.Ahead_Flank)
			{
				key = "Navigate_Change_Speed_Afterburner";
			}
			else if (telegraph == ObjectSpeed.SpeedSettings.Ahead_Full)
			{
				key = "Navigate_Change_Speed_Max";
			}
			else if (telegraph == ObjectSpeed.SpeedSettings.Ahead_Standard)
			{
				key = "Navigate_Change_Speed_Cruise";
			}
			else if (telegraph == ObjectSpeed.SpeedSettings.Ahead_2_3)
			{
				key = "Navigate_Change_Speed_Loiter";
			}
		}
		return new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", key) ?? "", null, new DelegateCommand(delegate
		{
			_task.UpdateSpeed(telegraph);
		}), null, new ReactiveProperty<bool>(initialValue).ToReadOnlyReactiveProperty());
	}

	private ContextMenuItem GetAltitudeCMItem(float[] altitudes, int index)
	{
		bool initialValue = MathF.Abs((float)_task.WaypointPosition._height - altitudes[index]) < 0.2267855f;
		return new ContextMenuItem(string.Format("{0:0} {1}", altitudes[index] * 220.47266f, Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "Feet")), null, new DelegateCommand(delegate
		{
			_task.UpdateAltitude(altitudes[index]);
		}), null, new ReactiveProperty<bool>(initialValue).ToReadOnlyReactiveProperty());
	}

	private ContextMenuItem GetDepthCMItem(ObjectDepth.DepthSettings depth)
	{
		bool initialValue = _task._heightState.value == DepthStateToHeightState(depth);
		return new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", $"SetDepth_{depth}") ?? "", null, new DelegateCommand(delegate
		{
			_task.UpdateDepth(DepthStateToHeightState(depth));
		}), null, new ReactiveProperty<bool>(initialValue).ToReadOnlyReactiveProperty());
	}

	private WaypointData.WaypointHeightState DepthStateToHeightState(ObjectDepth.DepthSettings depth)
	{
		return depth switch
		{
			ObjectDepth.DepthSettings.Periscope => WaypointData.WaypointHeightState.Periscope, 
			ObjectDepth.DepthSettings.Shallow => WaypointData.WaypointHeightState.VeryLow, 
			ObjectDepth.DepthSettings.AboveLayer => WaypointData.WaypointHeightState.Low, 
			ObjectDepth.DepthSettings.BelowLayer => WaypointData.WaypointHeightState.Medium, 
			ObjectDepth.DepthSettings.Deep => WaypointData.WaypointHeightState.High, 
			ObjectDepth.DepthSettings.VeryDeep => WaypointData.WaypointHeightState.VeryHigh, 
			_ => WaypointData.WaypointHeightState.Surface, 
		};
	}

	private ObjectDepth.DepthSettings HeightStateToDepthState(WaypointData.WaypointHeightState height)
	{
		return height switch
		{
			WaypointData.WaypointHeightState.Periscope => ObjectDepth.DepthSettings.Periscope, 
			WaypointData.WaypointHeightState.VeryLow => ObjectDepth.DepthSettings.Shallow, 
			WaypointData.WaypointHeightState.Low => ObjectDepth.DepthSettings.AboveLayer, 
			WaypointData.WaypointHeightState.Medium => ObjectDepth.DepthSettings.BelowLayer, 
			WaypointData.WaypointHeightState.High => ObjectDepth.DepthSettings.Deep, 
			WaypointData.WaypointHeightState.VeryHigh => ObjectDepth.DepthSettings.VeryDeep, 
			_ => ObjectDepth.DepthSettings.Surface, 
		};
	}

	public TrulyObservableCollection<ContextMenuItem> GetRuleOfEngagemntItems()
	{
		return new TrulyObservableCollection<ContextMenuItem>
		{
			new ContextMenuItem("BottomRow", "WeaponStatusFree", null, isChecked: new ReactiveProperty<bool>(_task._setWeaponStatus.setWS && _task._setWeaponStatus.weaponStatus == ObjectBase.WeaponStatus.Free).ToReadOnlyReactiveProperty(), command: new DelegateCommand(delegate
			{
				_task.UpdateWeaponStatus(ObjectBase.WeaponStatus.Free);
			})),
			new ContextMenuItem("BottomRow", "WeaponStatusTight", null, isChecked: new ReactiveProperty<bool>(_task._setWeaponStatus.setWS && _task._setWeaponStatus.weaponStatus == ObjectBase.WeaponStatus.Tight).ToReadOnlyReactiveProperty(), command: new DelegateCommand(delegate
			{
				_task.UpdateWeaponStatus(ObjectBase.WeaponStatus.Tight);
			})),
			new ContextMenuItem("BottomRow", "WeaponStatusHold", null, isChecked: new ReactiveProperty<bool>(_task._setWeaponStatus.setWS && _task._setWeaponStatus.weaponStatus == ObjectBase.WeaponStatus.Hold).ToReadOnlyReactiveProperty(), command: new DelegateCommand(delegate
			{
				_task.UpdateWeaponStatus(ObjectBase.WeaponStatus.Hold);
			}))
		};
	}

	private string RenderETA(float hoursDec)
	{
		if (Singleton.Quitting || !Singleton<SeaPower.Environment>.InstanceExists() || Singleton<SeaPower.Environment>.Instance == null)
		{
			return "";
		}
		DateTime dateTime;
		try
		{
			dateTime = new DateTime(Singleton<SeaPower.Environment>.Instance.Year, Singleton<SeaPower.Environment>.Instance.Month, Singleton<SeaPower.Environment>.Instance.Day, Singleton<SeaPower.Environment>.Instance.Hour, Singleton<SeaPower.Environment>.Instance.Minutes, (int)Singleton<SeaPower.Environment>.Instance.Seconds);
		}
		catch
		{
			return "";
		}
		if (hoursDec < 50f)
		{
			return dateTime.AddHours(hoursDec).ToString("HH:mm");
		}
		return "";
	}

	public SimulatedState SetLive(ObjectBase target, IObservable<GeoPosition> currentLocation, IObservable<ISpeedCommand> targetSpeed)
	{
		if (!_isLive)
		{
			_disposer.Clear();
			_disposer.Add(Observable.EveryUpdate().WithLatestFrom(currentLocation, (long _, GeoPosition r) => r).Subscribe(delegate(GeoPosition p)
			{
				StartPosition = p;
				UpdateLivePath();
				UpdateLiveETA();
			}));
			_disposer.Add(targetSpeed.Subscribe(delegate(ISpeedCommand c)
			{
				StartSpeed = c?.GetDemandedSpeedInKnots(target) ?? 0f;
			}));
			_disposer.Add(Observable.Interval(new TimeSpan(0, 0, 1)).Subscribe(delegate
			{
				UpdateLiveETA();
			}));
			_isLive = true;
		}
		float num = WaypointHandle.Position.Value.UnityDistance(StartPosition) * 0.036285132f;
		float num2 = target.SpeedCommand.Value?.GetDemandedSpeedInKnots(target) ?? target._velocityInKnots;
		return new SimulatedState
		{
			InitialPosition = WaypointHandle.Position.Value,
			speed = num2,
			deltaTime = num / num2,
			_wpHeightState = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._heightState.value,
			_wpHeight = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task.WaypointPosition._height,
			_wpSpeedState = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._speedState.value,
			_setAirSearchRadars = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setAirSearchRadars,
			_setSurfaceSearchRadars = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setSurfaceSearchRadars,
			_setActiveSonars = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setActiveSonars,
			_setTowedSonars = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setTowedSonars,
			_setEmcon = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setEmcon,
			_wpSetWS = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setWeaponStatus.setWS,
			_wpWeaponStatus = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setWeaponStatus.weaponStatus,
			_returnToFormation = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._returnToFormation,
			_isAutogenerated = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._isAutogenerated
		};
		void UpdateLiveETA()
		{
			float num3 = WaypointHandle.Position.Value.UnityDistance(StartPosition) * 0.036285132f;
			if (DM._showETAasRange)
			{
				ETAText = string.Format("{0:0.##} {1}", num3, Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "NauticalMiles"));
			}
			else
			{
				float hoursDec = ((StartSpeed > float.Epsilon) ? (num3 / StartSpeed) : float.PositiveInfinity);
				ETAText = RenderETA(hoursDec);
			}
			if (DM._displayWPChanges && WaypointHandle.ContextMenuOwner is WaypointViewModel)
			{
				VisualActionTask task = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task;
				if (task is AttackAtWaypoint attackAtWaypoint && attackAtWaypoint._ammo._ap._subType != Ammunition.Type.Sonobuoy)
				{
					string newValue = Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "AttackAtWaypointPosition");
					if (attackAtWaypoint._targetObject != null)
					{
						newValue = Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "AttackAtWaypointTarget");
						Vehicle mapVehicle = attackAtWaypoint._targetObject.GetMapVehicle(_task._baseObject._taskforce);
						newValue = ((mapVehicle == null) ? newValue.Replace("${TargetID}", "???") : newValue.Replace("${TargetID}", mapVehicle.Id.ToString()));
					}
					newValue = Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "AttackAtWaypoint").Replace("${Target}", newValue);
					newValue = newValue.Replace("${Weapon}", attackAtWaypoint._ammo._ap._displayedName);
					newValue = newValue.Replace("${Salvo}", attackAtWaypoint._salvo.ToString());
					ETAText = ETAText + "\n" + newValue;
				}
				if (task._setAirSearchRadars.setRadars)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_AirSearch") + ": " + (task._setAirSearchRadars.enable ? Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On") : Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off"));
				}
				if (task._setSurfaceSearchRadars.setRadars)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_SurfaceSearch") + ": " + (task._setSurfaceSearchRadars.enable ? Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On") : Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off"));
				}
				if (task._setActiveSonars.setSonars)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_ActiveSonar") + ": " + (task._setActiveSonars.enable ? Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On") : Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off"));
				}
				if (task._setTowedSonars.setSonars)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_TowedSonarGeneralized") + ": " + (task._setTowedSonars.enable ? Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On") : Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off"));
				}
				if (task._setEmcon)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "EMCON");
				}
				if (task._objectsToVIDList.Count > 0)
				{
					string text = Singleton<LanguageResourceHandler>.Instance.getText("Status", "Status_VID");
					string text2 = "";
					foreach (ObjectBase objectsToVID in task._objectsToVIDList)
					{
						if (!(objectsToVID == null) && !objectsToVID.IsDestroyed)
						{
							Vehicle mapVehicle2 = objectsToVID.GetMapVehicle(_task._baseObject._taskforce);
							text2 = ((mapVehicle2 == null) ? (text2 + "???, ") : (text2 + $"{mapVehicle2.Id}, "));
						}
					}
					if (!string.IsNullOrEmpty(text2))
					{
						text2 = text2.Remove(text2.Length - 2, 2);
						text = text.Replace("${TrackId}", text2);
						ETAText = ETAText + "\n" + text;
					}
				}
				if (task._setWeaponStatus.setWS)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", $"WeaponStatus{task._setWeaponStatus.weaponStatus}");
				}
				if (task._returnToFormation)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Orders_ReturnToFormation");
				}
				if (task._waypointsData.Count > 0)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Waypoints_Loop");
				}
			}
		}
	}

	public SimulatedState SetSimulated(ObjectBase target, SimulatedState initialState)
	{
		if (_isLive)
		{
			_disposer.Clear();
			_isLive = false;
		}
		WaypointHandle.Layer = 15;
		StartPosition = initialState.InitialPosition;
		float num = WaypointHandle.Position.Value.UnityDistance(initialState.InitialPosition) * 0.036285132f;
		float num2 = ((initialState.speed > float.Epsilon) ? (num / initialState.speed) : float.PositiveInfinity);
		if (Singleton<LanguageResourceHandler>.InstanceExists() && !Singleton.Quitting)
		{
			if (DM._showETAasRange)
			{
				ETAText = string.Format("{0:0.##} {1}", num, Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "NauticalMiles"));
			}
			else
			{
				ETAText = RenderETA(initialState.deltaTime + num2);
			}
			if (DM._displayWPChanges && WaypointHandle.ContextMenuOwner is WaypointViewModel)
			{
				VisualActionTask task = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task;
				if (task is AttackAtWaypoint attackAtWaypoint && attackAtWaypoint._ammo._ap._subType != Ammunition.Type.Sonobuoy)
				{
					string newValue = Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "AttackAtWaypointPosition");
					if (attackAtWaypoint._targetObject != null)
					{
						newValue = Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "AttackAtWaypointTarget");
						Vehicle mapVehicle = attackAtWaypoint._targetObject.GetMapVehicle(_task._baseObject._taskforce);
						newValue = ((mapVehicle == null) ? newValue.Replace("${TargetID}", "???") : newValue.Replace("${TargetID}", mapVehicle.Id.ToString()));
					}
					newValue = Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "AttackAtWaypoint").Replace("${Target}", newValue);
					newValue = newValue.Replace("${Weapon}", attackAtWaypoint._ammo._ap._displayedName);
					newValue = newValue.Replace("${Salvo}", attackAtWaypoint._salvo.ToString());
					ETAText = ETAText + "\n" + newValue;
				}
				if (task._setSpeed.value && task._speedState.value != initialState._wpSpeedState)
				{
					string key = $"Navigate_Change_Speed_{task._speedState.value}";
					if (_task._baseObject is Aircraft)
					{
						if (task._speedState.value == ObjectSpeed.SpeedSettings.Ahead_Flank)
						{
							key = "Navigate_Change_Speed_Afterburner";
						}
						else if (task._speedState.value == ObjectSpeed.SpeedSettings.Ahead_Full)
						{
							key = "Navigate_Change_Speed_Max";
						}
						else if (task._speedState.value == ObjectSpeed.SpeedSettings.Ahead_Standard)
						{
							key = "Navigate_Change_Speed_Cruise";
						}
						else if (task._speedState.value == ObjectSpeed.SpeedSettings.Ahead_2_3)
						{
							key = "Navigate_Change_Speed_Loiter";
						}
					}
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Speed") + " " + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", key);
				}
				if (target is Submarine || target is Biologic)
				{
					if (task._heightState.value != initialState._wpHeightState)
					{
						if (task._heightState.value == WaypointData.WaypointHeightState.Number)
						{
							ETAText += string.Format("\n{0} {1:0} {2}", Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Depth"), (0.0 - task.WaypointPosition._height) * 220.47265625, Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "Feet"));
						}
						else
						{
							ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Depth") + " " + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", $"SetDepth_{HeightStateToDepthState(task._heightState.value)}");
						}
					}
					else if (task._heightState.value == WaypointData.WaypointHeightState.Number && MathF.Abs((float)(task.WaypointPosition._height - initialState._wpHeight)) > 0.2267855f)
					{
						ETAText += string.Format("\n{0} {1:0} {2}", Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Depth"), (0.0 - task.WaypointPosition._height) * 220.47265625, Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "Feet"));
					}
				}
				else if ((target is Aircraft || target is Helicopter) && MathF.Abs((float)(task.WaypointPosition._height - initialState._wpHeight)) > 0.2267855f)
				{
					ETAText += string.Format("\n{0} {1:0} {2}", Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Altitude"), task.WaypointPosition._height * 220.47265625, Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "Feet"));
				}
				if (task._setAirSearchRadars.setRadars)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_AirSearch") + ": " + (task._setAirSearchRadars.enable ? Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On") : Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off"));
				}
				if (task._setSurfaceSearchRadars.setRadars)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_SurfaceSearch") + ": " + (task._setSurfaceSearchRadars.enable ? Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On") : Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off"));
				}
				if (task._setActiveSonars.setSonars)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_ActiveSonar") + ": " + (task._setActiveSonars.enable ? Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On") : Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off"));
				}
				if (task._setTowedSonars.setSonars)
				{
					if (task._baseObject is Helicopter && task._isAutogenerated)
					{
						if (!task._setTowedSonars.enable)
						{
							ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_TowedSonarGeneralized") + ": " + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off");
						}
					}
					else
					{
						ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Sensors_TowedSonarGeneralized") + ": " + (task._setTowedSonars.enable ? Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "On") : Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Off"));
					}
				}
				if (task._setEmcon)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "EMCON");
				}
				if (task._objectsToVIDList.Count > 0)
				{
					string text = Singleton<LanguageResourceHandler>.Instance.getText("Status", "Status_VID");
					string text2 = "";
					foreach (ObjectBase objectsToVID in task._objectsToVIDList)
					{
						if (!(objectsToVID == null) && !objectsToVID.IsDestroyed)
						{
							Vehicle mapVehicle2 = objectsToVID.GetMapVehicle(_task._baseObject._taskforce);
							text2 = ((mapVehicle2 == null) ? (text2 + "???, ") : (text2 + $"{mapVehicle2.Id}, "));
						}
					}
					if (!string.IsNullOrEmpty(text2))
					{
						text2 = text2.Remove(text2.Length - 2, 2);
						text = text.Replace("${TrackId}", text2);
						ETAText = ETAText + "\n" + text;
					}
				}
				if (task._setWeaponStatus.setWS)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", $"WeaponStatus{task._setWeaponStatus.weaponStatus}");
				}
				if (task._returnToFormation)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Orders_ReturnToFormation");
				}
				if (task._waypointsData.Count > 0)
				{
					ETAText = ETAText + "\n" + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Waypoints_Loop");
				}
			}
		}
		return new SimulatedState
		{
			InitialPosition = WaypointHandle.Position.Value,
			speed = initialState.speed,
			deltaTime = num2 + initialState.deltaTime,
			_wpHeightState = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._heightState.value,
			_wpHeight = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task.WaypointPosition._height,
			_wpSpeedState = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._speedState.value,
			_setAirSearchRadars = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setAirSearchRadars,
			_setSurfaceSearchRadars = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setSurfaceSearchRadars,
			_setActiveSonars = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setActiveSonars,
			_setTowedSonars = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setTowedSonars,
			_setEmcon = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setEmcon,
			_wpSetWS = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setWeaponStatus.setWS,
			_wpWeaponStatus = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._setWeaponStatus.weaponStatus,
			_returnToFormation = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._returnToFormation,
			_isAutogenerated = (WaypointHandle.ContextMenuOwner as WaypointViewModel)._task._isAutogenerated
		};
	}

	private void UpdateLivePath()
	{
		if (WaypointHandle.Position.Value._latitude != 0.0 || WaypointHandle.Position.Value._longitude != 0.0)
		{
			List<GeoPosition> list = swapper.Next();
			list.Clear();
			list.Add(StartPosition);
			list.Add(WaypointHandle.Position.Value);
			LivePath = list;
		}
	}

	protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public void Dispose()
	{
		_disposer.Dispose();
		_consumer.Dispose();
		WaypointHandle.Dispose();
		if (!_disposed)
		{
			_disposed = true;
			InstanceCount--;
		}
		else
		{
			DM.B("Repeated disposal!");
		}
		DM.B($"Waypoint viewmodel disposal! Current alive instances: {InstanceCount}");
	}
}
