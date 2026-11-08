using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using SeaPower;
using UniRx;
using UnityEngine;

namespace SeapowerUI.ViewModels;

public class WeaponEntry : ContextMenuItem
{
	public class LauncherBoxStatus : INotifyPropertyChanged
	{
		private string _status;

		public string Name { get; }

		public string Status
		{
			get
			{
				return _status;
			}
			private set
			{
				_status = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Status"));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public LauncherBoxStatus(string name, ReactiveProperty<string> source)
		{
			Name = name;
			_status = source.Value;
			source.Subscribe(delegate(string v)
			{
				Status = v;
			});
		}
	}

	public ObjectBase objectBase;

	public GroupOfUnits sourceGroup;

	private string _ammo;

	private int ammoAmount;

	private bool _isFuelTankEntry;

	private float _fittedFuelTankCapacity;

	private (bool DaySeeker, bool CrewAimed, bool UnguidedBombBehindDaySight, bool AirToAir) _nightFacts;

	private static readonly List<WeakReference<WeaponEntry>> _nightWarningEntries = new List<WeakReference<WeaponEntry>>();

	private static IDisposable _nightWarningTicker;

	private DelegateCommand _showFiringArcsCommand;

	private DelegateCommand _hideFiringArcsCommand;

	private TacticalDoctrineOptionViewModel<TacticalDoctrine.PlayerAASalvoSize> _aawSalvoSize;

	public int AmmoAmount
	{
		get
		{
			return ammoAmount;
		}
		set
		{
			ammoAmount = value;
			OnPropertyChanged("AmmoAmount");
			OnPropertyChanged("AmmoAmountText");
		}
	}

	public string AmmoAmountText
	{
		get
		{
			if (!_isFuelTankEntry)
			{
				return AmmoAmount.ToString();
			}
			return GetFuelTankPercentageText();
		}
	}

	public bool IsAmmoEntry { get; private set; }

	public bool WasInInitialLoadout { get; private set; }

	public bool NightWarningDayOnly { get; private set; }

	public bool NightWarningDaySeeker { get; private set; }

	public bool NightWarningBombs { get; private set; }

	public bool NightWarningAirToAir { get; private set; }

	public bool HasNightWarning
	{
		get
		{
			if (!NightWarningDayOnly && !NightWarningDaySeeker && !NightWarningBombs)
			{
				return NightWarningAirToAir;
			}
			return true;
		}
	}

	public ICommand OpenReferenceCommand { get; }

	public ObservableCollection<BaseSystem> WeaponSystems { get; } = new ObservableCollection<BaseSystem>();


	public ObservableCollection<LauncherBoxStatus> LauncherBoxes { get; } = new ObservableCollection<LauncherBoxStatus>();


	public ICommand ShowFiringArcsCommand => _showFiringArcsCommand ?? (_showFiringArcsCommand = new DelegateCommand(delegate
	{
		MarkFiringArcCandidate();
	}));

	public ICommand HideFiringArcsCommand => _hideFiringArcsCommand ?? (_hideFiringArcsCommand = new DelegateCommand(delegate
	{
		WeaponArcHoverManager.ClearCandidate(this);
	}));

	public string Name { get; }

	public string Nickname { get; }

	public string Type { get; }

	public string AawSalvoSizeText
	{
		get
		{
			if (_aawSalvoSize == null)
			{
				return string.Empty;
			}
			string text = _aawSalvoSize.ResolvedValue switch
			{
				TacticalDoctrine.PlayerAASalvoSize.One => "1", 
				TacticalDoctrine.PlayerAASalvoSize.Two => "2", 
				_ => _aawSalvoSize.DisplayValue, 
			};
			return "- " + _aawSalvoSize.Label + ": " + text;
		}
	}

	public string WeaponSlotShortcut { get; private set; } = string.Empty;


	public double FooterToolTipVerticalOffset { get; private set; }

	public ReactiveProperty<string> Mode { get; set; } = new ReactiveProperty<string>();


	public ReactiveProperty<string> ChannelInfo { get; } = new ReactiveProperty<string>();


	private void InitNightWarnings(IniHandler unitIni)
	{
		_nightFacts = LoadoutSummary.GetAmmoNightFacts(unitIni, _ammo);
		if (!_nightFacts.DaySeeker && !_nightFacts.CrewAimed && !_nightFacts.UnguidedBombBehindDaySight && !_nightFacts.AirToAir)
		{
			return;
		}
		RefreshNightWarnings();
		_nightWarningEntries.Add(new WeakReference<WeaponEntry>(this));
		if (_nightWarningTicker == null)
		{
			_nightWarningTicker = Observable.Interval(TimeSpan.FromSeconds(5.0)).ObserveOnMainThread().Subscribe(delegate
			{
				TickNightWarnings();
			});
		}
	}

	private static void TickNightWarnings()
	{
		_nightWarningEntries.RemoveAll((WeakReference<WeaponEntry> r) => !r.TryGetTarget(out var target2) || target2.objectBase == null);
		foreach (WeakReference<WeaponEntry> nightWarningEntry in _nightWarningEntries)
		{
			if (nightWarningEntry.TryGetTarget(out var target))
			{
				target.RefreshNightWarnings();
			}
		}
	}

	private void RefreshNightWarnings()
	{
		bool num = LoadoutSummary.IsInNightWarningWindow();
		bool flag = SensorUtils.HasNightCapableSensors(objectBase);
		bool flag2 = _nightFacts.AirToAir && SensorUtils.HasNightCapableSensors(objectBase, airTargets: true);
		bool flag3 = num && _nightFacts.CrewAimed && !flag;
		bool flag4 = num && _nightFacts.DaySeeker;
		bool flag5 = num && !flag3 && _nightFacts.UnguidedBombBehindDaySight;
		bool flag6 = num && _nightFacts.AirToAir && !flag2;
		if (flag3 != NightWarningDayOnly || flag4 != NightWarningDaySeeker || flag5 != NightWarningBombs || flag6 != NightWarningAirToAir)
		{
			NightWarningDayOnly = flag3;
			NightWarningDaySeeker = flag4;
			NightWarningBombs = flag5;
			NightWarningAirToAir = flag6;
			OnPropertyChanged("NightWarningDayOnly");
			OnPropertyChanged("NightWarningDaySeeker");
			OnPropertyChanged("NightWarningBombs");
			OnPropertyChanged("NightWarningAirToAir");
			OnPropertyChanged("HasNightWarning");
		}
	}

	private void MarkFiringArcCandidate()
	{
		if (objectBase == null)
		{
			return;
		}
		List<WeaponSystem> list = WeaponSystems.OfType<WeaponSystem>().ToList();
		if (list.Count == 0)
		{
			return;
		}
		float mapRangeInUnity = 0f;
		if (!string.IsNullOrEmpty(_ammo))
		{
			Ammunition ammunitionByName = objectBase.getAmmunitionByName(_ammo);
			if (ammunitionByName != null)
			{
				mapRangeInUnity = ammunitionByName._ap._launchRangesInUnity.y;
			}
		}
		WeaponArcHoverManager.SetCandidate(this, objectBase, list, mapRangeInUnity);
	}

	public WeaponEntry(ObjectBase obj, AmmunitionGroup ammoGroup, bool isWaypointEdit = false)
	{
		WeaponEntry weaponEntry = this;
		objectBase = obj;
		IsAmmoEntry = true;
		base.Label = ammoGroup.getDisplayedName();
		base.Items = GetSalvoCommands(obj, ammoGroup.Ap, Mode, isWaypointEdit);
		OpenReferenceCommand = new DelegateCommand(delegate
		{
			WindowHostCommands.OpenWindowCommand.Execute(new ObjectWeaponInformationViewModel(MainGameView.Instance, Singleton<EncyclopedicDataManager>.Instance.WeaponMuseumEntries.FirstOrDefault((MuseumWeaponEntry x) => x.IniRef == ammoGroup.Ap._ammunitionFileName)), MainGameView.Instance);
		});
		Name = ammoGroup.Ap._displayedName;
		Nickname = ammoGroup.Ap._displayedNickname;
		Type = ammoGroup.Ap._displayedType;
		Mode.Value = string.Empty;
		_ammo = ammoGroup.Ap._ammunitionFileName;
		InitNightWarnings(obj._obp?.Simple?._ini);
		foreach (WeaponSystem item4 in obj.GetWeaponSystemsForAmmunition(_ammo))
		{
			WeaponSystems.Add(item4);
			item4.PropertyChanged += delegate(object _, PropertyChangedEventArgs e)
			{
				if (e.PropertyName == "DisplayState")
				{
					weaponEntry.UpdateChannelInfo();
				}
			};
			UpdateChannelInfo();
		}
		foreach (BaseSystem weaponSystem in WeaponSystems)
		{
			if (!(weaponSystem is WeaponSystemLauncher weaponSystemLauncher) || !weaponSystemLauncher._vwp._perContainerReload || string.Equals(weaponSystemLauncher._vwp._moduleName, "VLS", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			int num = 0;
			foreach (WeaponContainer container in weaponSystemLauncher._containers)
			{
				container.EnsureSlotStatus();
				for (int i = 0; i < container.SlotStatus.Count; i++)
				{
					num++;
					LauncherBoxes.Add(new LauncherBoxStatus($"Box {num}", container.SlotStatus[i]));
				}
			}
		}
		List<(ContextMenuItem item, WeaponSystem ws, string baseLabel)> reloadEntries;
		if (!isWaypointEdit)
		{
			List<WeaponSystem> list = (from ws in WeaponSystems.OfType<WeaponSystem>()
				where ws.hasAMagazine() && !(ws is WeaponSystemLauncher)
				select ws).ToList();
			if (list.Count > 0)
			{
				if (base.Items == null)
				{
					base.Items = new TrulyObservableCollection<ContextMenuItem>();
				}
				string text = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "ForceReload");
				reloadEntries = new List<(ContextMenuItem, WeaponSystem, string)>();
				if (list.Count == 1)
				{
					WeaponSystem single = list[0];
					ContextMenuItem contextMenuItem = new ContextMenuItem(text, null, new DelegateCommand(delegate
					{
						single.ForceReload();
					}));
					base.Items.Add(contextMenuItem);
					reloadEntries.Add((contextMenuItem, single, text));
				}
				else
				{
					TrulyObservableCollection<ContextMenuItem> trulyObservableCollection = new TrulyObservableCollection<ContextMenuItem>();
					for (int j = 0; j < list.Count; j++)
					{
						WeaponSystem ws2 = list[j];
						string text2 = $"{ws2.DisplayName.Value} #{j + 1}";
						ContextMenuItem contextMenuItem2 = new ContextMenuItem(text2, null, new DelegateCommand(delegate
						{
							ws2.ForceReload();
						}));
						trulyObservableCollection.Add(contextMenuItem2);
						reloadEntries.Add((contextMenuItem2, ws2, text2));
					}
					base.Items.Add(new ContextMenuItem(text, trulyObservableCollection));
				}
				UpdateReloadLabels();
				foreach (var item5 in reloadEntries)
				{
					item5.ws.PropertyChanged += delegate(object _, PropertyChangedEventArgs e)
					{
						if (e.PropertyName == "DisplayState")
						{
							UpdateReloadLabels();
						}
					};
				}
			}
		}
		AmmunitionParameters ap = obj.getAmmunitionByName(_ammo)._ap;
		_isFuelTankEntry = ap._subType == Ammunition.Type.Fueltank;
		if (_isFuelTankEntry)
		{
			_fittedFuelTankCapacity = Mathf.Max(0f, obj._obp._maxFuel - obj._obp._defaultMaxFuel);
			obj.RangeInKm.Subscribe(delegate
			{
				weaponEntry.OnPropertyChanged("AmmoAmountText");
			});
		}
		if (obj.IsPlayerObject && obj is Vessel && ap._type == Ammunition.Type.Missile && (ap._targetType == Ammunition.Target.AAW || ap._secondaryTargetType == Ammunition.Target.AAW))
		{
			_aawSalvoSize = obj.TacticalDoctrine?.AASalvoSize;
			if (_aawSalvoSize != null)
			{
				_aawSalvoSize.PropertyChanged += AawSalvoSize_PropertyChanged;
			}
		}
		if (isWaypointEdit)
		{
			if (ap._type == Ammunition.Type.Noisemaker || ap._type == Ammunition.Type.Chaff || ap._type == Ammunition.Type.Fueltank)
			{
				base.Command = new DelegateCommand(delegate
				{
					VisualActionTask selectedTask = obj._userRoot.SelectedTask;
					if (selectedTask != null)
					{
						obj.SetAttackAtWaypointTask(ap._ammunitionFileName, null, selectedTask.WaypointPosition, selectedTask.WaypointPosition, 1, selectedTask);
					}
				});
			}
			else
			{
				base.Command = new DelegateCommand(delegate
				{
					AttackingState.TryStart(Globals._mainGameViewModel?.StateManager, obj, weaponEntry._ammo, -1, isWaypointEdit);
				});
			}
		}
		else if (ap._type == Ammunition.Type.Noisemaker)
		{
			base.Command = new DelegateCommand(delegate
			{
				obj.LaunchNoisemaker(weaponEntry._ammo);
			});
		}
		else if (ap._type == Ammunition.Type.Chaff)
		{
			base.Command = new DelegateCommand(delegate
			{
				obj.LaunchChaff(message: true);
			});
		}
		else if (ap._subType == Ammunition.Type.Fueltank)
		{
			base.Command = new DelegateCommand(delegate
			{
				obj.DropFueltanks();
			});
		}
		else if (ap._subType == Ammunition.Type.Sonobuoy)
		{
			base.Command = new DelegateCommand(delegate
			{
				AttackingState.TryStart(Globals._mainGameViewModel?.StateManager, obj, weaponEntry._ammo, -1, isWaypointEdit);
			});
		}
		else
		{
			base.Command = new DelegateCommand(delegate
			{
				AttackingState.TryStart(Globals._mainGameViewModel?.StateManager, obj, weaponEntry._ammo, -1, isWaypointEdit);
			});
		}
		if (ap._subType == Ammunition.Type.Fueltank)
		{
			List<string> list2 = new List<string>();
			AmmoAmount = 0;
			foreach (Ammunition ammunition in ammoGroup.getAmmunitionList())
			{
				if (!list2.Contains(ammunition._ap._ammunitionFileName))
				{
					list2.Add(ammunition._ap._ammunitionFileName);
					AmmoAmount += obj.getAmmunitionAmountByName(ammunition._ap._ammunitionFileName);
				}
			}
		}
		else
		{
			AmmoAmount = obj.getAmmunitionAmountByName(_ammo);
		}
		obj.AmmunitionAmountDictionary.ObserveReplace().Subscribe(delegate(DictionaryReplaceEvent<string, int> x)
		{
			if (x.Key == weaponEntry._ammo)
			{
				weaponEntry.AmmoAmount = obj.getAmmunitionAmountByName(weaponEntry._ammo);
			}
		});
		if (ap._subType == Ammunition.Type.Fueltank)
		{
			WasInInitialLoadout = false;
			{
				foreach (Ammunition ammunition2 in ammoGroup.getAmmunitionList())
				{
					if (obj.WasInStartingLoadout(ammunition2._ap._ammunitionFileName))
					{
						WasInInitialLoadout = true;
						break;
					}
				}
				return;
			}
		}
		WasInInitialLoadout = obj.WasInStartingLoadout(_ammo);
		void UpdateReloadLabels()
		{
			foreach (var item6 in reloadEntries)
			{
				ContextMenuItem item = item6.item;
				WeaponSystem item2 = item6.ws;
				string item3 = item6.baseLabel;
				(int, int)? loadedAmmoReadout = item2.GetLoadedAmmoReadout();
				item.Label = (loadedAmmoReadout.HasValue ? $"{item3}  ({loadedAmmoReadout.Value.Item1}/{loadedAmmoReadout.Value.Item2})" : item3);
			}
		}
	}

	private string GetFuelTankPercentageText()
	{
		if (objectBase?._obp == null || objectBase._fuelTanksDropped || AmmoAmount <= 0 || _fittedFuelTankCapacity <= 1E-05f || objectBase._obp._maxRangeInKm <= 1E-05f)
		{
			return "0%";
		}
		float num = Mathf.Clamp(objectBase._obp._maxFuel * objectBase.RangeInKm.Value / objectBase._obp._maxRangeInKm - objectBase._obp._defaultMaxFuel, 0f, _fittedFuelTankCapacity);
		return $"{Mathf.RoundToInt(100f * num / _fittedFuelTankCapacity)}%";
	}

	private void AawSalvoSize_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		OnPropertyChanged("AawSalvoSizeText");
	}

	private void UpdateChannelInfo()
	{
		string text = null;
		AmmunitionParameters ap = objectBase.getAmmunitionByName(_ammo)._ap;
		if (ap._requiresGuidance)
		{
			(int totalW, int usedW, int totalT, int usedT) tuple = (from s in WeaponSystems.OfType<WeaponSystem>().SelectMany((WeaponSystem x) => x._vwp._associatedSensors).Distinct()
				where s.CanGuideAmmo(ap)
				select s).Aggregate((0, 0, 0, 0), ((int totalW, int usedW, int totalT, int usedT) acc, SensorSystem s) => (acc.totalW + s.EffectiveWeaponChannels, acc.usedW + s._associatedWeapons.Count, acc.totalT + s._targetChannels, acc.usedT + s._associatedTargets.Count));
			int item = tuple.totalW;
			int item2 = tuple.usedW;
			int item3 = tuple.totalT;
			int item4 = tuple.usedT;
			text = ((item <= 1 && item3 <= 1) ? null : $"W:{item2}/{item}, T:{item4}/{item3}");
		}
		else if (ap._midCourseCorrection.IsWireGuided && objectBase._maxWireCount < int.MaxValue)
		{
			text = string.Format("{0}:{1}/{2}", Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Wires"), objectBase.WiresInUse(), objectBase._maxWireCount);
		}
		if (ChannelInfo.Value != text)
		{
			ChannelInfo.Value = text;
		}
	}

	private static TrulyObservableCollection<ContextMenuItem> GetSalvoCommands(ObjectBase obj, AmmunitionParameters ap, ReactiveProperty<string> modeProperty = null, bool isWaypointEdit = false)
	{
		switch (ap._type)
		{
		case Ammunition.Type.Missile:
		case Ammunition.Type.ASROC:
		{
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection5 = new TrulyObservableCollection<ContextMenuItem>();
			string ammo2 = ap._ammunitionFileName;
			trulyObservableCollection5.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "SingleShot"), null, new DelegateCommand(delegate
			{
				AttackingState.TryStart(Globals._mainGameViewModel?.StateManager, obj, ammo2, -1, isWaypointEdit);
			})));
			int[] array = new int[5] { 2, 4, 8, 16, 20 };
			for (int i = 0; i < array.Length; i++)
			{
				int item2 = array[i];
				trulyObservableCollection5.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "SalvoFire").Replace("${salvoSize}", item2.ToString()), null, new DelegateCommand(delegate
				{
					AttackingState.TryStart(Globals._mainGameViewModel?.StateManager, obj, ammo2, item2, isWaypointEdit);
				})));
			}
			return trulyObservableCollection5;
		}
		case Ammunition.Type.Torpedo:
		{
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection6 = new TrulyObservableCollection<ContextMenuItem>();
			ReadOnlyReactiveProperty<bool> enabled = GetLayerExists(obj);
			trulyObservableCollection6.Add(new ContextMenuItem("ContextMenu", "SearchDepth", new TrulyObservableCollection<ContextMenuItem>
			{
				new ContextMenuItem("Generic", "Auto", null, isChecked: GetSearchDepthMatch(obj, ap, AmmunitionParameters.SearchDepth.None), command: GetSetSearchDepthCommand(obj, ap, AmmunitionParameters.SearchDepth.None)),
				new ContextMenuItem("ContextMenu", "SetDepth_Shallow", null, isChecked: GetSearchDepthMatch(obj, ap, AmmunitionParameters.SearchDepth.Shallow), command: GetSetSearchDepthCommand(obj, ap, AmmunitionParameters.SearchDepth.Shallow)),
				new ContextMenuItem("ContextMenu", "SetDepth_AboveLayer", null, isChecked: GetSearchDepthMatch(obj, ap, AmmunitionParameters.SearchDepth.AboveLayer), command: GetSetSearchDepthCommand(obj, ap, AmmunitionParameters.SearchDepth.AboveLayer), enabled: enabled),
				new ContextMenuItem("ContextMenu", "SetDepth_BelowLayer", null, isChecked: GetSearchDepthMatch(obj, ap, AmmunitionParameters.SearchDepth.BelowLayer), command: GetSetSearchDepthCommand(obj, ap, AmmunitionParameters.SearchDepth.BelowLayer), enabled: enabled),
				new ContextMenuItem("ContextMenu", "SetDepth_Deep", null, isChecked: GetSearchDepthMatch(obj, ap, AmmunitionParameters.SearchDepth.Deep), command: GetSetSearchDepthCommand(obj, ap, AmmunitionParameters.SearchDepth.Deep))
			}, null, new ReadOnlyReactiveProperty<bool>(new ReactiveProperty<bool>(ap._guidanceType != 0 && ap._guidanceType != AmmunitionParameters.GuidanceType.WakeHoming && (ap._targetType == Ammunition.Target.ASW || ap._secondaryTargetType == Ammunition.Target.ASW))), null, Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "SearchDepthTooltip")));
			if (ap._torpedoSpeedSettings.Count > 1)
			{
				string text = Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "Knots");
				string text2 = Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "NauticalMiles");
				TrulyObservableCollection<ContextMenuItem> trulyObservableCollection7 = new TrulyObservableCollection<ContextMenuItem>
				{
					new ContextMenuItem("Generic", "Auto", null, isChecked: GetLaunchSpeedMatch(obj, ap, -1), command: GetSetLaunchSpeedCommand(obj, ap, -1))
				};
				for (int num = ap._torpedoSpeedSettings.Count - 1; num >= 0; num--)
				{
					int speedIndex2 = num;
					Vector2 vector = ap._torpedoSpeedSettings[num];
					string text3 = $"{vector.x:0} {text} ({vector.y * 0.036285132f:0.0} {text2})";
					if (ap._midCourseCorrection.IsWireGuided && vector.x > ap._maxSpeedOnWireInKnots)
					{
						text3 = text3 + " " + Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "BreaksWire");
					}
					trulyObservableCollection7.Add(new ContextMenuItem(text3, (TrulyObservableCollection<ContextMenuItem>)null, isChecked: GetLaunchSpeedMatch(obj, ap, speedIndex2), command: GetSetLaunchSpeedCommand(obj, ap, speedIndex2), enabled: (ReadOnlyReactiveProperty<bool>)null, tooltip: (string)null, shortcut: (string)null, shortcutLabel: (string)null, secondaryShortcut: (string)null, secondaryShortcutLabel: (string)null));
				}
				trulyObservableCollection6.Add(new ContextMenuItem("ContextMenu", "LaunchSpeed", trulyObservableCollection7, null, null, null, Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "LaunchSpeedTooltip")));
			}
			if (ap._hasVesselAttackGuidance && ap.PrimaryLaunchGuidanceType != ap._vesselAttackGuidanceType)
			{
				TrulyObservableCollection<ContextMenuItem> items11 = new TrulyObservableCollection<ContextMenuItem>
				{
					new ContextMenuItem("Generic", "Auto", null, isChecked: GetLaunchSeekerModeMatch(obj, ap, AmmunitionParameters.LaunchSeekerMode.Auto), command: GetSetLaunchSeekerModeCommand(obj, ap, AmmunitionParameters.LaunchSeekerMode.Auto)),
					new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("GuidanceType", ap.PrimaryLaunchGuidanceType.ToString()), (TrulyObservableCollection<ContextMenuItem>)null, isChecked: GetLaunchSeekerModeMatch(obj, ap, AmmunitionParameters.LaunchSeekerMode.Primary), command: GetSetLaunchSeekerModeCommand(obj, ap, AmmunitionParameters.LaunchSeekerMode.Primary), enabled: (ReadOnlyReactiveProperty<bool>)null, tooltip: (string)null, shortcut: (string)null, shortcutLabel: (string)null, secondaryShortcut: (string)null, secondaryShortcutLabel: (string)null),
					new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("GuidanceType", ap._vesselAttackGuidanceType.ToString()), (TrulyObservableCollection<ContextMenuItem>)null, isChecked: GetLaunchSeekerModeMatch(obj, ap, AmmunitionParameters.LaunchSeekerMode.Secondary), command: GetSetLaunchSeekerModeCommand(obj, ap, AmmunitionParameters.LaunchSeekerMode.Secondary), enabled: (ReadOnlyReactiveProperty<bool>)null, tooltip: (string)null, shortcut: (string)null, shortcutLabel: (string)null, secondaryShortcut: (string)null, secondaryShortcutLabel: (string)null)
				};
				trulyObservableCollection6.Add(new ContextMenuItem("ContextMenu", "SeekerMode", items11, null, null, null, Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "SeekerModeTooltip")));
			}
			string ammo = ap._ammunitionFileName;
			trulyObservableCollection6.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "SingleShot"), null, new DelegateCommand(delegate
			{
				AttackingState.TryStart(Globals._mainGameViewModel?.StateManager, obj, ammo, -1, isWaypointEdit);
			})));
			int[] array = new int[5] { 2, 4, 8, 16, 20 };
			for (int i = 0; i < array.Length; i++)
			{
				int item = array[i];
				trulyObservableCollection6.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "SalvoFire").Replace("${salvoSize}", item.ToString()), null, new DelegateCommand(delegate
				{
					AttackingState.TryStart(Globals._mainGameViewModel?.StateManager, obj, ammo, item, isWaypointEdit);
				})));
			}
			return trulyObservableCollection6;
		}
		case Ammunition.Type.Bomb:
			if (ap._subType == Ammunition.Type.Sonobuoy)
			{
				TrulyObservableCollection<ContextMenuItem> trulyObservableCollection = new TrulyObservableCollection<ContextMenuItem>();
				string above = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "AboveLayer");
				string below = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "BelowLayer");
				string intermitent = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Intermittent");
				string mode2 = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Mode");
				switch (obj.SonoDeployState.Value)
				{
				case Bomb.SonobuoyDeployState.BelowLayer:
					modeProperty.Value = mode2.Replace("${DeployMode}", below);
					break;
				case Bomb.SonobuoyDeployState.Intermittent:
					modeProperty.Value = mode2.Replace("${DeployMode}", intermitent);
					break;
				case Bomb.SonobuoyDeployState.AboveLayer:
					modeProperty.Value = mode2.Replace("${DeployMode}", above);
					break;
				}
				trulyObservableCollection.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "AboveLayer"), null, new DelegateCommand(delegate
				{
					obj.SonoDeployState.Value = Bomb.SonobuoyDeployState.AboveLayer;
					modeProperty.Value = mode2.Replace("${DeployMode}", above);
				}), null, obj.SonoDeployState.Select((Bomb.SonobuoyDeployState x) => x == Bomb.SonobuoyDeployState.AboveLayer).ToReadOnlyReactiveProperty()));
				trulyObservableCollection.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "BelowLayer"), null, new DelegateCommand(delegate
				{
					obj.SonoDeployState.Value = Bomb.SonobuoyDeployState.BelowLayer;
					modeProperty.Value = mode2.Replace("${DeployMode}", below);
				}), null, obj.SonoDeployState.Select((Bomb.SonobuoyDeployState x) => x == Bomb.SonobuoyDeployState.BelowLayer).ToReadOnlyReactiveProperty()));
				trulyObservableCollection.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "Intermittent"), null, new DelegateCommand(delegate
				{
					obj.SonoDeployState.Value = Bomb.SonobuoyDeployState.Intermittent;
					modeProperty.Value = mode2.Replace("${DeployMode}", intermitent);
				}), null, obj.SonoDeployState.Select((Bomb.SonobuoyDeployState x) => x == Bomb.SonobuoyDeployState.Intermittent).ToReadOnlyReactiveProperty()));
				TrulyObservableCollection<ContextMenuItem> trulyObservableCollection2 = new TrulyObservableCollection<ContextMenuItem>();
				int[] array = new int[6] { 1, 2, 4, 5, 8, 10 };
				foreach (int dropIntervalInMiles in array)
				{
					trulyObservableCollection2.Add(new ContextMenuItem(string.Format("{0} {1}", dropIntervalInMiles, Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "NauticalMiles")), null, new DelegateCommand(delegate
					{
						Globals._mainGameViewModel.StateManager.PushSingleState(new SonobuoyLineState(Globals._mainGameViewModel.StateManager, obj, ap._ammunitionFileName, dropIntervalInMiles, isWaypointEdit));
						Singleton<MouseControlState>.Instance.setActionToPerform(MouseControlState.ActionToPerform.EngageWith);
					})));
				}
				trulyObservableCollection.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "SonoBuoyLine"), trulyObservableCollection2));
				return trulyObservableCollection;
			}
			if (ap._subType != Ammunition.Type.AirDepthCharge && obj is Aircraft aircraft)
			{
				TrulyObservableCollection<ContextMenuItem> trulyObservableCollection3 = new TrulyObservableCollection<ContextMenuItem>();
				TrulyObservableCollection<ContextMenuItem> trulyObservableCollection4 = new TrulyObservableCollection<ContextMenuItem>
				{
					GetDropTypeItem("Generic", "Auto", TacticalDoctrine.AircraftBombAttackProfile.Automatic),
					GetDropTypeItem("AircraftBombAttackProfile", "LevelAttack", TacticalDoctrine.AircraftBombAttackProfile.LevelAttack)
				};
				if (aircraft.CanDiveBomb())
				{
					trulyObservableCollection4.Add(GetDropTypeItem("AircraftBombAttackProfile", "DiveAttack", TacticalDoctrine.AircraftBombAttackProfile.DiveAttack));
				}
				if (aircraft.CanTossBomb())
				{
					trulyObservableCollection4.Add(GetDropTypeItem("AircraftBombAttackProfile", "Toss", TacticalDoctrine.AircraftBombAttackProfile.Toss));
				}
				trulyObservableCollection3.Add(new ContextMenuItem("ContextMenu", "DropType", trulyObservableCollection4));
				return trulyObservableCollection3;
			}
			break;
		}
		return null;
		ContextMenuItem GetDropTypeItem(string labelSection, string labelKey, TacticalDoctrine.AircraftBombAttackProfile profile)
		{
			return new ContextMenuItem(labelSection, labelKey, null, isChecked: obj.AircraftBombAttackProfileDoctrine.Select((TacticalDoctrine.AircraftBombAttackProfile x) => x == profile).ToReadOnlyReactiveProperty(), command: new DelegateCommand(delegate
			{
				obj.AircraftBombAttackProfileDoctrine.Value = profile;
				if (!obj.FollowingFormation || obj.Formation.SelectedControlMode == UnitFormation.ControlMode.Local || (obj.Formation.SelectedControlMode == UnitFormation.ControlMode.FollowLeader && obj.IsFormationWingman))
				{
					return;
				}
				foreach (Station station in obj.Formation.Stations)
				{
					ObjectBase unitObject = station.UnitObject;
					if (!(unitObject == null) && !(unitObject == obj) && unitObject.FollowingFormation)
					{
						unitObject.AircraftBombAttackProfileDoctrine.Value = profile;
					}
				}
			}));
		}
		static ReadOnlyReactiveProperty<bool> GetLaunchSeekerModeMatch(ObjectBase obj, AmmunitionParameters ap, AmmunitionParameters.LaunchSeekerMode mode)
		{
			return GetLaunchSeekerModeSetting(obj, ap).ObserveEveryValueChanged((ReactiveProperty<AmmunitionParameters.LaunchSeekerMode> x) => x.Value == mode).ToReadOnlyReactiveProperty();
		}
		static ReactiveProperty<AmmunitionParameters.LaunchSeekerMode> GetLaunchSeekerModeSetting(ObjectBase obj, AmmunitionParameters ap)
		{
			if (!obj._launchSeekerModeSettings.TryGetValue(ap._ammunitionFileName, out var value))
			{
				value = new ReactiveProperty<AmmunitionParameters.LaunchSeekerMode>(AmmunitionParameters.LaunchSeekerMode.Auto);
				obj._launchSeekerModeSettings[ap._ammunitionFileName] = value;
			}
			return value;
		}
		static ReadOnlyReactiveProperty<bool> GetLaunchSpeedMatch(ObjectBase obj, AmmunitionParameters ap, int speedIndex)
		{
			if (!obj._launchSpeedSettings.ContainsKey(ap._ammunitionFileName))
			{
				obj._launchSpeedSettings[ap._ammunitionFileName] = new ReactiveProperty<int>(-1);
			}
			return obj._launchSpeedSettings[ap._ammunitionFileName].ObserveEveryValueChanged((ReactiveProperty<int> x) => x.Value == speedIndex).ToReadOnlyReactiveProperty();
		}
		static ReadOnlyReactiveProperty<bool> GetLayerExists(ObjectBase objBase)
		{
			return objBase.Position.ObserveEveryValueChanged((ReactiveProperty<GeoPosition> x) => Singleton<SeaPower.Environment>.Instance.getLayerHeight(x.Value) < 0f).ToReadOnlyReactiveProperty();
		}
		static ReadOnlyReactiveProperty<bool> GetSearchDepthMatch(ObjectBase obj, AmmunitionParameters ap, AmmunitionParameters.SearchDepth searchDepth)
		{
			if (!obj._searchDepthSettings.ContainsKey(ap._ammunitionFileName))
			{
				obj._searchDepthSettings[ap._ammunitionFileName] = new ReactiveProperty<AmmunitionParameters.SearchDepth>(AmmunitionParameters.SearchDepth.None);
			}
			return obj._searchDepthSettings[ap._ammunitionFileName].ObserveEveryValueChanged((ReactiveProperty<AmmunitionParameters.SearchDepth> x) => x.Value == searchDepth).ToReadOnlyReactiveProperty();
		}
		static ICommand GetSetLaunchSeekerModeCommand(ObjectBase obj, AmmunitionParameters ap, AmmunitionParameters.LaunchSeekerMode mode)
		{
			return new DelegateCommand(delegate
			{
				GetLaunchSeekerModeSetting(obj, ap).Value = mode;
			});
		}
		static ICommand GetSetLaunchSpeedCommand(ObjectBase obj, AmmunitionParameters ap, int speedIndex)
		{
			return new DelegateCommand(delegate
			{
				if (!obj._launchSpeedSettings.ContainsKey(ap._ammunitionFileName))
				{
					obj._launchSpeedSettings[ap._ammunitionFileName] = new ReactiveProperty<int>(-1);
				}
				obj._launchSpeedSettings[ap._ammunitionFileName].Value = speedIndex;
			});
		}
		static ICommand GetSetSearchDepthCommand(ObjectBase obj, AmmunitionParameters ap, AmmunitionParameters.SearchDepth searchDepth)
		{
			return new DelegateCommand(delegate
			{
				if (!obj._searchDepthSettings.ContainsKey(ap._ammunitionFileName))
				{
					obj._searchDepthSettings[ap._ammunitionFileName] = new ReactiveProperty<AmmunitionParameters.SearchDepth>(AmmunitionParameters.SearchDepth.None);
				}
				obj._searchDepthSettings[ap._ammunitionFileName].Value = searchDepth;
				Debug.Log($"SetSearchDepthCommand({searchDepth}) results in value {obj._searchDepthSettings[ap._ammunitionFileName].Value}");
			});
		}
	}

	public void SetWeaponSlotShortcut(int slotIndex)
	{
		KeyAction action = (KeyAction)(20 + slotIndex);
		WeaponSlotShortcut = (Singleton<InputHandler>.InstanceExists() ? Singleton<InputHandler>.Instance.GetBindingDisplayString(action) : string.Empty);
		OnPropertyChanged("WeaponSlotShortcut");
		FooterToolTipVerticalOffset = ((slotIndex % 2 != 0) ? (-16) : 0);
		OnPropertyChanged("FooterToolTipVerticalOffset");
	}

	public WeaponEntry(ObjectBase obj, BaseSystem.SystemType type, bool isWaypointEdit = false)
	{
		WeaponEntry weaponEntry = this;
		if (type == BaseSystem.SystemType.SensorECM)
		{
			base.Label = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "EngageWith_ECM");
			AmmoAmount = 1;
			obj.NumberOfFreeJamChannels.Subscribe(delegate
			{
				weaponEntry.AmmoAmount = obj.NumberOfFreeJamChannels.Value;
			});
			SensorSystemECM item = obj._obp._ecmSystems.FirstOrDefault((SensorSystemECM x) => (x != null && x.IsOn.Value && !x.Inoperable.Value && x._ecm._ep._type == ECMParameters.ECMType.Offensive) || x._ecm._ep._type == ECMParameters.ECMType.Universal);
			WeaponSystems.Add(item);
			base.Command = new DelegateCommand(delegate
			{
				Globals._mainGameViewModel.StateManager.PushSingleState(new AttackingWithSystemState(Globals._mainGameViewModel.StateManager, obj, type, isWaypointEdit));
				Singleton<MouseControlState>.Instance.setActionToPerform(MouseControlState.ActionToPerform.EngageWithECM);
			});
		}
		else
		{
			if (type != BaseSystem.SystemType.SensorLaserDesignator)
			{
				return;
			}
			base.Label = Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "HighlightByLaser");
			AmmoAmount = obj.NumberOfFreeLaserChannels.Value;
			obj.NumberOfFreeLaserChannels.Subscribe(delegate
			{
				weaponEntry.AmmoAmount = obj.NumberOfFreeLaserChannels.Value;
			});
			foreach (SensorSystem sensorSystem in obj.getSensorSystems(typeof(SensorSystemLaserDesignator)))
			{
				WeaponSystems.Add(sensorSystem);
			}
			base.Command = new DelegateCommand(delegate
			{
				Globals._mainGameViewModel.StateManager.PushSingleState(new AttackingWithSystemState(Globals._mainGameViewModel.StateManager, obj, type, isWaypointEdit));
				Singleton<MouseControlState>.Instance.setActionToPerform(MouseControlState.ActionToPerform.HighlightByLaser);
			});
		}
	}

	public WeaponEntry(AmmunitionSimpleParameters ammoData, GroupOfUnits source, GroupWaypoint wp)
	{
		WeaponEntry weaponEntry = this;
		sourceGroup = source;
		Mode.Value = string.Empty;
		base.Items = GetSalvoCommands(ammoData, source, wp);
		IniHandler iniHandler = LocalizationLoader.OpenVirtualLocalizationFile("language_" + Singleton<LanguageResourceHandler>.Instance.Language, "ammunition_names", "ammunition_names");
		if (iniHandler != null)
		{
			Name = iniHandler.readValue("AmmunitionNames", ammoData.Name, "MISSING TEXT");
		}
		else
		{
			iniHandler = LocalizationLoader.OpenVirtualLocalizationFile("language_en", "ammunition_names", "ammunition_names");
			if (iniHandler != null)
			{
				Name = iniHandler.readValue("AmmunitionNames", ammoData.Name, "MISSING TEXT");
			}
			else
			{
				Log.Error("Localization file '/language_en/ammunition_names.ini' does not exist!", Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\UI\\Game\\ViewModels\\ObjectEffectorsViewModel.cs", 914, ".ctor");
			}
		}
		if (Name != "MISSING TEXT")
		{
			string[] array = LocalizationLoader.ApplyNatoWeaponName(iniHandler, ammoData.Name, Name.Split(','));
			Name = array[0];
			if (array.Length > 1)
			{
				Nickname = array[1];
			}
			if (array.Length > 2)
			{
				Type = array[2];
			}
		}
		base.Label = Name;
		_ammo = ammoData.Name;
		if (ammoData._type == Ammunition.Type.Noisemaker || ammoData._type == Ammunition.Type.Chaff || ammoData._type == Ammunition.Type.Fueltank)
		{
			base.Command = new DelegateCommand(delegate
			{
				MissionEditorViewModel.Instance.StateManager.PushSingleState(new MissionEditorViewModel.MissionEditorAttackingState(MissionEditorViewModel.Instance.StateManager, source, weaponEntry._ammo, weaponEntry.Name, ammoData, wp, 1));
			});
		}
		else
		{
			base.Command = new DelegateCommand(delegate
			{
				MissionEditorViewModel.Instance.StateManager.PushSingleState(new MissionEditorViewModel.MissionEditorAttackingState(MissionEditorViewModel.Instance.StateManager, source, weaponEntry._ammo, weaponEntry.Name, ammoData, wp, 1));
			});
		}
	}

	private TrulyObservableCollection<ContextMenuItem> GetSalvoCommands(AmmunitionSimpleParameters ammoData, GroupOfUnits source, GroupWaypoint wp)
	{
		switch (ammoData._type)
		{
		case Ammunition.Type.Missile:
		case Ammunition.Type.ASROC:
		{
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection2 = new TrulyObservableCollection<ContextMenuItem>();
			string ammo = ammoData.Name;
			trulyObservableCollection2.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "SingleShot"), null, new DelegateCommand(delegate
			{
				MissionEditorViewModel.Instance.StateManager.PushSingleState(new MissionEditorViewModel.MissionEditorAttackingState(MissionEditorViewModel.Instance.StateManager, source, ammo, Name, ammoData, wp, 1));
			})));
			int[] array = new int[5] { 2, 4, 8, 16, 20 };
			for (int i = 0; i < array.Length; i++)
			{
				int item = array[i];
				trulyObservableCollection2.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "SalvoFire").Replace("${salvoSize}", item.ToString()), null, new DelegateCommand(delegate
				{
					MissionEditorViewModel.Instance.StateManager.PushSingleState(new MissionEditorViewModel.MissionEditorAttackingState(MissionEditorViewModel.Instance.StateManager, source, ammo, Name, ammoData, wp, item));
				})));
			}
			return trulyObservableCollection2;
		}
		case Ammunition.Type.Torpedo:
		{
			TrulyObservableCollection<ContextMenuItem> trulyObservableCollection = new TrulyObservableCollection<ContextMenuItem>();
			string ammo2 = ammoData.Name;
			trulyObservableCollection.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "SingleShot"), null, new DelegateCommand(delegate
			{
				MissionEditorViewModel.Instance.StateManager.PushSingleState(new MissionEditorViewModel.MissionEditorAttackingState(MissionEditorViewModel.Instance.StateManager, source, ammo2, Name, ammoData, wp, 1));
			})));
			int[] array = new int[5] { 2, 4, 8, 16, 20 };
			for (int i = 0; i < array.Length; i++)
			{
				int item2 = array[i];
				trulyObservableCollection.Add(new ContextMenuItem(Singleton<LanguageResourceHandler>.Instance.getText("ContextMenu", "SalvoFire").Replace("${salvoSize}", item2.ToString()), null, new DelegateCommand(delegate
				{
					MissionEditorViewModel.Instance.StateManager.PushSingleState(new MissionEditorViewModel.MissionEditorAttackingState(MissionEditorViewModel.Instance.StateManager, source, ammo2, Name, ammoData, wp, item2));
				})));
			}
			return trulyObservableCollection;
		}
		default:
			return null;
		}
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
