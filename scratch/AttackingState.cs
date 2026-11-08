using System;
using System.Collections.Generic;
using System.Linq;
using MapControl;
using Noesis;
using SeaPower;
using UniRx;
using UnityEngine;

namespace SeapowerUI.ViewModels;

internal class AttackingState : InteractionState
{
	private readonly InteractionStateManager _manager;

	private Vector2 _ranges;

	private int _salvoSize;

	private bool _isWaypointEdit;

	private VisualActionTask _lastSelectedTask;

	private object _targetItem;

	private double _lastRangeUpdateTime;

	private static readonly List<GeoPosition> EmptyBearingPreviewLine = new List<GeoPosition>();

	private GeoPosition _bearingPreviewMousePosition;

	private bool _bearingPreviewOverMap;

	private RoutedEventArgs _bearingPreviewHitTestEvent;

	private bool _bearingPreviewBlockedForEvent;

	private double _lastBearingTimeUpdate = double.MinValue;

	private float _cachedBearingActivationTime = -1f;

	private double _lastTerminalDelayTravelUpdate = double.MinValue;

	private AmmunitionParameters _cachedTerminalDelayTravelAp;

	private float _cachedTerminalDelayTravel;

	private IDisposable _bearingPreviewRefreshTimer;

	private double _maxRangeInLat;

	private double _maxKinematicRange;

	private double _noEscapeRangeInLat;

	private double _maxSensorRange;

	private double _maxDirectSightRange;

	private List<ObjectBase.EngagementIssue> _engagementIssues = new List<ObjectBase.EngagementIssue>();

	private double _horizonMaxRangeInLat;

	public ObjectBase Source { get; }

	public string Ammunition { get; }

	public Ammunition Ammo { get; }

	private Vector2 ReferenceHorizonRanges { get; }

	public List<GeoPosition> BearingValidTransitLine { get; private set; } = EmptyBearingPreviewLine;


	public List<GeoPosition> BearingInvalidTransitLine { get; private set; } = EmptyBearingPreviewLine;


	public List<GeoPosition> BearingSeekerArc { get; private set; } = EmptyBearingPreviewLine;


	public List<GeoPosition> BearingSeekerSides { get; private set; } = EmptyBearingPreviewLine;


	public List<GeoPosition> BearingPortRail { get; private set; } = EmptyBearingPreviewLine;


	public List<GeoPosition> BearingStarboardRail { get; private set; } = EmptyBearingPreviewLine;


	public List<GeoPosition> BearingCenterLine { get; private set; } = EmptyBearingPreviewLine;


	public List<GeoPosition> BearingSeekerEndArc { get; private set; } = EmptyBearingPreviewLine;


	public GeoPosition BearingActivationPosition { get; private set; }

	public GeoPosition BearingCircleCenter { get; private set; }

	public float BearingCircleRadiusInLat { get; private set; }

	public bool IsBearingCircleVisible { get; private set; }

	public string BearingActivationDistanceText { get; private set; } = "";


	public string BearingActivationTimeText { get; private set; } = "";


	public string BearingWakeDistanceText { get; private set; } = "";


	public string BearingWakeVelocityText { get; private set; } = "";


	public string BearingWakeHitChanceLabel { get; private set; } = "";


	public string BearingWakeHitChanceText { get; private set; } = "";


	public string BearingWakeHitChanceLevel { get; private set; } = "Low";


	public bool IsBearingPreviewVisible { get; private set; }

	public bool IsBearingPreviewInvalid { get; private set; }

	public bool IsBearingActivationTimeVisible { get; private set; }

	public bool IsBearingWakeEstimateVisible { get; private set; }

	public bool IsBearingWakeDistanceVisible { get; private set; }

	public bool IsTargetPreviewValid { get; private set; }

	public bool IsTargetPreviewInvalid { get; private set; }

	public bool IsWakeHomingForCurrentAim { get; private set; }

	public double MaxRangeInLat
	{
		get
		{
			return _maxRangeInLat;
		}
		set
		{
			SetField(ref _maxRangeInLat, value, "MaxRangeInLat");
		}
	}

	public double MaxKinematicRange
	{
		get
		{
			return _maxKinematicRange;
		}
		set
		{
			SetField(ref _maxKinematicRange, value, "MaxKinematicRange");
		}
	}

	public double NoEscapeRangeInLat
	{
		get
		{
			return _noEscapeRangeInLat;
		}
		set
		{
			SetField(ref _noEscapeRangeInLat, value, "NoEscapeRangeInLat");
		}
	}

	public double MaxSensorRange
	{
		get
		{
			return _maxSensorRange;
		}
		set
		{
			SetField(ref _maxSensorRange, value, "MaxSensorRange");
		}
	}

	public double MaxDirectSightRange
	{
		get
		{
			return _maxDirectSightRange;
		}
		set
		{
			SetField(ref _maxDirectSightRange, value, "MaxDirectSightRange");
		}
	}

	public double MinRangeInLat { get; private set; }

	public bool IsMinimumRangeLabelVisible => MinRangeInLat > 0.0;

	public bool IsMaximumRangeLabelVisible => MaxRangeInLat > 0.0;

	public bool IsNoEscapeRangeLabelVisible => NoEscapeRangeInLat > 0.0;

	public bool IsRadarHorizonLabelVisible => HorizonMaxRangeInLat > 0.0;

	public string MinimumRangeLabel => FormatWeaponRangeLabel("WeaponRangeLabelMinimum", MinRangeInLat);

	public string MaximumRangeLabel => FormatWeaponRangeLabel("WeaponRangeLabelMaximum", MaxRangeInLat);

	public string NoEscapeRangeLabel => FormatWeaponRangeLabel("WeaponRangeLabelNoEscape", NoEscapeRangeInLat);

	public string RadarHorizonLabel => FormatRangeLabel("WeaponRangeLabelMaximumRadarHorizon", HorizonMaxRangeInLat);

	public GeoPosition MinimumRangeLabelPosition => GetRangeLabelPosition(MinRangeInLat);

	public GeoPosition MaximumRangeLabelPosition => GetRangeLabelPosition(MaxRangeInLat);

	public GeoPosition NoEscapeRangeLabelPosition => GetRangeLabelPosition(NoEscapeRangeInLat);

	public GeoPosition RadarHorizonLabelPosition => GetRangeLabelPosition(HorizonMaxRangeInLat);

	public float MinimumRangeLabelStackOffset => GetRangeLabelStackOffset(MinRangeInLat, 0);

	public float MaximumRangeLabelStackOffset => GetRangeLabelStackOffset(MaxRangeInLat, 1);

	public float NoEscapeRangeLabelStackOffset => GetRangeLabelStackOffset(NoEscapeRangeInLat, 2);

	public float RadarHorizonLabelStackOffset => GetRangeLabelStackOffset(HorizonMaxRangeInLat, 3);

	public List<ObjectBase.EngagementIssue> EngagementIssues
	{
		get
		{
			return _engagementIssues;
		}
		private set
		{
			SetField(ref _engagementIssues, value, "EngagementIssues");
		}
	}

	public double HorizonMaxRangeInLat
	{
		get
		{
			return _horizonMaxRangeInLat;
		}
		set
		{
			SetField(ref _horizonMaxRangeInLat, value, "HorizonMaxRangeInLat");
		}
	}

	public static bool TryStart(InteractionStateManager manager, ObjectBase source, string ammunition, int salvoSize = -1, bool isWaypointEdit = false)
	{
		if (manager == null || source == null || source.IsDestroyed || source.getAmmunitionByName(ammunition)?._ap == null)
		{
			return false;
		}
		manager.PushSingleState(new AttackingState(manager, source, ammunition, salvoSize, isWaypointEdit));
		Singleton<MouseControlState>.Instance.setActionToPerform(MouseControlState.ActionToPerform.EngageWith);
		return true;
	}

	private AttackingState(InteractionStateManager manager, ObjectBase source, string ammunition, int salvoSize, bool isWaypointEdit)
	{
		base.CursorName = "attacking";
		_manager = manager;
		Source = source;
		Ammunition = ammunition;
		Ammo = Source.getAmmunitionByName(ammunition);
		_salvoSize = salvoSize;
		_isWaypointEdit = isWaypointEdit;
		_ranges = Ammo?._ap._launchRangesInUnity ?? new Vector2(0f, 0f);
		ReferenceHorizonRanges = ((Ammo != null) ? Source.GetAmmunitionHorizons(Ammunition, 0.0907142f) : new Vector2(0f, 0f));
		if (_ranges.x > 0f)
		{
			MinRangeInLat = (double)_ranges.x * 0.0006047522103382444;
		}
		else
		{
			MinRangeInLat = 0.0;
		}
		ObjectBase.EngagementRange maxEngagementRanges = Source.GetMaxEngagementRanges(null, Ammo, requireOpenSensorChannel: false, shortCircuitTerrainCheck: true, usePreciseKinematics: true);
		float b = ((Ammo._ap._type == SeaPower.Ammunition.Type.Bomb) ? Utils.SlantToHorizontalRange(maxEngagementRanges.SensorRange, Source.transform.position.y) : maxEngagementRanges.SensorRange);
		float num = Mathf.Min(maxEngagementRanges.KinematicRange, b);
		if (Ammo._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
		{
			num = Source.GetMaxRangeForFCR(Ammo, null, num).Range;
			if (num < 0f)
			{
				num = _ranges.y;
			}
		}
		MaxRangeInLat = (double)num * 0.0006047522103382444;
		UpdateReferenceHorizonRing();
		NotifyRangeLabelBindings();
		ClearStates(_manager);
	}

	private string FormatRangeLabel(string labelKey, double rangeInLat)
	{
		float num = (float)(rangeInLat / 0.0006047522103382444 * 0.03628513216972351);
		if (num <= 0f)
		{
			return string.Empty;
		}
		return Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", labelKey).Replace("${Distance}", num.ToString("0.#")).Replace("${NauticalMiles}", Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "NauticalMiles"));
	}

	private string FormatWeaponRangeLabel(string labelKey, double rangeInLat)
	{
		string newValue = Ammo?._ap?._displayedName ?? Ammunition;
		return FormatRangeLabel(labelKey, rangeInLat).Replace("${WeaponName}", newValue);
	}

	private GeoPosition GetRangeLabelPosition(double rangeInLat)
	{
		float dInNmi = (float)(rangeInLat / 0.0006047522103382444 * 0.03628513216972351);
		return Source.UpdatePosition.Value.GeoOffsetByDistanceOnHeading(dInNmi, 0f);
	}

	private void UpdateReferenceHorizonRing()
	{
		HorizonMaxRangeInLat = ((ReferenceHorizonRanges.y < _ranges.y) ? ((double)ReferenceHorizonRanges.y * 0.0006047522103382444) : 0.0);
	}

	private void UpdateTargetHorizonRing(ObjectBase target, float displayedRange)
	{
		if (Ammo?._ap == null || !Ammo._ap.IsFcrHorizonLimited)
		{
			HorizonMaxRangeInLat = 0.0;
			return;
		}
		Vector2 ammunitionHorizons = Source.GetAmmunitionHorizons(Ammunition, Ammo._ap.FcrTrackedAltitude(target));
		HorizonMaxRangeInLat = ((ammunitionHorizons.y < displayedRange) ? ((double)ammunitionHorizons.y * 0.0006047522103382444) : 0.0);
	}

	private float GetRangeLabelStackOffset(double rangeInLat, int stackOrder)
	{
		if (rangeInLat <= 0.0)
		{
			return 0f;
		}
		int num = 0;
		if (stackOrder > 0 && IsRangeLabelStacked(rangeInLat, MinRangeInLat, 0.25))
		{
			num++;
		}
		if (stackOrder > 1 && IsRangeLabelStacked(rangeInLat, MaxRangeInLat, 0.25))
		{
			num++;
		}
		if (stackOrder > 2 && IsRangeLabelStacked(rangeInLat, NoEscapeRangeInLat, 0.25))
		{
			num++;
		}
		return (float)(-num) * 12f;
	}

	private static bool IsRangeLabelStacked(double firstRangeInLat, double secondRangeInLat, double thresholdNm)
	{
		if (secondRangeInLat <= 0.0)
		{
			return false;
		}
		return Math.Abs(firstRangeInLat - secondRangeInLat) / 0.0006047522103382444 * 0.03628513216972351 <= thresholdNm;
	}

	private void NotifyRangeLabelBindings()
	{
		OnPropertyChanged("IsMinimumRangeLabelVisible");
		OnPropertyChanged("IsMaximumRangeLabelVisible");
		OnPropertyChanged("IsNoEscapeRangeLabelVisible");
		OnPropertyChanged("IsRadarHorizonLabelVisible");
		OnPropertyChanged("MinimumRangeLabel");
		OnPropertyChanged("MaximumRangeLabel");
		OnPropertyChanged("NoEscapeRangeLabel");
		OnPropertyChanged("RadarHorizonLabel");
		OnPropertyChanged("MinimumRangeLabelPosition");
		OnPropertyChanged("MaximumRangeLabelPosition");
		OnPropertyChanged("NoEscapeRangeLabelPosition");
		OnPropertyChanged("RadarHorizonLabelPosition");
		OnPropertyChanged("MinimumRangeLabelStackOffset");
		OnPropertyChanged("MaximumRangeLabelStackOffset");
		OnPropertyChanged("NoEscapeRangeLabelStackOffset");
		OnPropertyChanged("RadarHorizonLabelStackOffset");
	}

	public override void OnExit()
	{
		SetTargetItem(null);
		_bearingPreviewRefreshTimer?.Dispose();
		_bearingPreviewRefreshTimer = null;
		ClearBearingPreview();
		base.OnExit();
	}

	private void SetTargetItem(object newItem)
	{
		if (_targetItem is MapUnitViewModel mapUnitViewModel && mapUnitViewModel != newItem)
		{
			mapUnitViewModel.ClearContactMouseoverInfo();
		}
		_targetItem = newItem;
	}

	public override void Update()
	{
		if (GameTime.unscaledTime - _lastRangeUpdateTime >= 1.0)
		{
			_lastRangeUpdateTime = GameTime.unscaledTime;
			UpdateDisplayedRange(_targetItem, Source.getAmmunitionByName(Ammunition));
			if (_bearingPreviewOverMap)
			{
				UpdateBearingPreview(_bearingPreviewMousePosition);
			}
			else if (_targetItem is MapUnitViewModel mapUnitViewModel)
			{
				UpdateTargetPreview(mapUnitViewModel.Unit?.BaseObject);
			}
		}
	}

	public override HitTestResultBehavior MouseAction(object v, RoutedEventArgs args, GeoPosition mousePosition, MapBase map, ref bool handled)
	{
		if (args is MouseWheelEventArgs)
		{
			return base.MouseAction(v, args, mousePosition, map, ref handled);
		}
		if (_bearingPreviewHitTestEvent != args)
		{
			_bearingPreviewHitTestEvent = args;
			_bearingPreviewBlockedForEvent = false;
		}
		if (v is MapUnitViewModel)
		{
			_bearingPreviewBlockedForEvent = true;
			_bearingPreviewOverMap = false;
			ObjectBase target = (v as MapUnitViewModel)?.Unit?.BaseObject;
			UpdateWakeHomingForCurrentAim(target);
			UpdateTargetPreview(target);
		}
		else if (v is MapViewModel && !_bearingPreviewBlockedForEvent)
		{
			_bearingPreviewMousePosition = mousePosition;
			_bearingPreviewOverMap = true;
			UpdateBearingPreview(mousePosition);
			UpdateWakeHomingForCurrentAim(null);
		}
		bool flag = Source.DoesEngageRequiresTarget(Ammunition);
		Ammunition ammunitionByName = Source.getAmmunitionByName(Ammunition);
		int num2;
		int num3;
		if (args is MouseButtonEventArgs mouseButtonEventArgs)
		{
			if (mouseButtonEventArgs.RightButton == MouseButtonState.Pressed)
			{
				bool flag2 = true;
				if (Source.getAmmunitionAmountByName(Ammunition) < 1)
				{
					flag2 = false;
					string text = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "NoSuitableAmmo");
					text = text.Replace("${TrackId} ", "");
					Globals._mainGameViewModel.RaiseAlert(Source, text);
					Singleton<VoiceMessage>.Instance.PlayVoiceMessage("UnableToComply", Source);
				}
				else if (v is MapUnitViewModel mapUnitViewModel)
				{
					ObjectBase baseObject = mapUnitViewModel.Unit.BaseObject;
					bool flag3 = ammunitionByName._ap._guidanceType == AmmunitionParameters.GuidanceType.AntiRadiationHoming;
					if (Source == mapUnitViewModel.Unit.BaseObject)
					{
						flag2 = false;
					}
					else if (!DoesCurrentLaunchSeekerMatchTarget(ammunitionByName._ap, baseObject) || !Source.DoesAmmoMatchTarget(Ammunition, baseObject, _isWaypointEdit || flag3))
					{
						flag2 = false;
						string text2 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "WrongTarget");
						if (ammunitionByName._ap.HasDayOnlySeeker && Source.CrewVisionBlocksAmmo(ammunitionByName._ap, baseObject))
						{
							text2 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "SeekerCantSeeTarget");
						}
						else if (ammunitionByName._ap.GuidanceTypeForTarget(baseObject) == AmmunitionParameters.GuidanceType.TVHoming && ammunitionByName._ap._midCourseCorrection != AmmunitionParameters.MidCourseCorrection.RadioCommand && SensorUtils.CalculateVisualRange(Source, baseObject, ammunitionByName._ap._nightVisionLevel) * ammunitionByName._ap._zoom * 1.5f < ammunitionByName._ap._launchRangesInUnity.x)
						{
							text2 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "SeekerCantSeeTarget");
						}
						text2 = text2.Replace("${TrackId}", mapUnitViewModel.Unit.Id.ToString());
						text2 = text2.Replace("${Ammo}", ammunitionByName._ap._displayedName);
						Globals._mainGameViewModel.RaiseAlert(Source, text2);
						Singleton<VoiceMessage>.Instance.PlayVoiceMessage("WrongTarget", Source);
					}
				}
				else if (flag)
				{
					flag2 = false;
					string text3 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "TargetRequired");
					text3 = text3.Replace("${Ammo}", ammunitionByName._ap._displayedName);
					Globals._mainGameViewModel.RaiseAlert(Source, text3);
					Singleton<VoiceMessage>.Instance.PlayVoiceMessage("TargetRequired", Source);
				}
				else if (!_isWaypointEdit && Source.CrewVisionBlocksAmmo(ammunitionByName._ap, null))
				{
					flag2 = false;
					string text4 = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", ammunitionByName._ap.HasDayOnlySeeker ? "SeekerCantSeeTarget" : "WrongTarget");
					text4 = text4.Replace("Track ${TrackId}", "").Replace("${TrackId}", "").Replace("  ", " ");
					text4 = text4.Replace("${Ammo}", ammunitionByName._ap._displayedName);
					Globals._mainGameViewModel.RaiseAlert(Source, text4);
					Singleton<VoiceMessage>.Instance.PlayVoiceMessage("UnableToComply", Source);
				}
				if (flag2)
				{
					int num = 1;
					if (_salvoSize <= 0)
					{
						if (Input.GetKey("left ctrl") || Input.GetKey("right ctrl"))
						{
							num = ((!Input.GetKey("left alt") && !Input.GetKey("right alt")) ? 2 : 3);
						}
						else if (Input.GetKey("left alt") || Input.GetKey("right alt"))
						{
							num = 4;
						}
					}
					else
					{
						num = _salvoSize;
					}
					if (_isWaypointEdit)
					{
						VisualActionTask selectedTask = Source._userRoot.SelectedTask;
						if (selectedTask != null)
						{
							OffsetAttack(v, mousePosition, selectedTask.WaypointPosition, num, selectedTask, ammunitionByName);
						}
						else
						{
							OffsetAttack(v, mousePosition, mousePosition, num, null, ammunitionByName);
						}
					}
					else if (!(v is MapUnitViewModel))
					{
						if (Source.IsAirUnit)
						{
							if (ammunitionByName._ap._type == SeaPower.Ammunition.Type.Torpedo)
							{
								if (num > 1)
								{
									NormalAttack(v, mousePosition, num);
								}
								else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
								{
									VisualActionTask selectedTask2 = Source._userRoot.SelectedTask;
									if (selectedTask2 != null)
									{
										OffsetAttack(v, mousePosition, mousePosition, num, selectedTask2, ammunitionByName);
									}
									else
									{
										OffsetAttack(v, mousePosition, mousePosition, num, null, ammunitionByName);
									}
								}
								else
								{
									if (Source.CurrentOrder.Value.OrderType != 0)
									{
										Source.setOrder(Order.Type.None, null, displayOrderText: false);
									}
									if (Source.Formation != null && Source.Formation.LeaderStation.UnitObject != Source)
									{
										Source.Formation.MakeUnitActIndependently(Source);
									}
									Source.RemoveWaypoints();
									OffsetAttack(v, mousePosition, mousePosition, num, null, ammunitionByName);
								}
							}
							else if (ammunitionByName._ap._subType == SeaPower.Ammunition.Type.Sonobuoy)
							{
								if (num > 1)
								{
									DropSonobuoys(mousePosition, num, ammunitionByName);
								}
								else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
								{
									VisualActionTask selectedTask3 = Source._userRoot.SelectedTask;
									if (selectedTask3 != null)
									{
										OffsetAttack(v, mousePosition, mousePosition, num, selectedTask3, ammunitionByName);
									}
									else
									{
										OffsetAttack(v, mousePosition, mousePosition, num, null, ammunitionByName);
									}
								}
								else
								{
									if (Source.CurrentOrder.Value.OrderType != 0)
									{
										Source.setOrder(Order.Type.None, null, displayOrderText: false);
									}
									if (Source.Formation != null && Source.Formation.LeaderStation.UnitObject != Source)
									{
										Source.Formation.MakeUnitActIndependently(Source);
									}
									Source.RemoveWaypoints();
									OffsetAttack(v, mousePosition, mousePosition, num, null, ammunitionByName);
								}
							}
							else
							{
								NormalAttack(v, mousePosition, num);
							}
						}
						else
						{
							NormalAttack(v, mousePosition, num);
						}
					}
					else
					{
						NormalAttack(v, mousePosition, num);
					}
				}
				if (!Input.GetKey("left shift") && !Input.GetKey("right shift"))
				{
					if (!_isWaypointEdit && Source.IsAirUnit && ammunitionByName._ap._type != SeaPower.Ammunition.Type.Torpedo && ammunitionByName._ap._subType != SeaPower.Ammunition.Type.Sonobuoy)
					{
						if (Source.IsFormationLeader)
						{
							foreach (Station station in Source.Formation.Stations)
							{
								ObjectBase unitObject = station.UnitObject;
								if (!(unitObject == null) && unitObject.FollowingFormation)
								{
									CheckCeaseFire(unitObject, unitObject != Source);
								}
							}
						}
						else
						{
							CheckCeaseFire(Source);
						}
					}
					_manager.PopState<AttackingState>();
				}
				handled = true;
				return HitTestResultBehavior.Stop;
			}
			if (mouseButtonEventArgs.MiddleButton == MouseButtonState.Pressed)
			{
				_manager.PopState<AttackingState>();
				handled = true;
				Singleton<InputHandler>.Instance._attackStateDroppedThisTick = true;
				return HitTestResultBehavior.Stop;
			}
		}
		else
		{
			if (v is MapUnitViewModel mapUnitViewModel2)
			{
				bool ignoreSituationalRestrictions = ammunitionByName._ap._guidanceType == AmmunitionParameters.GuidanceType.AntiRadiationHoming;
				if (Source != mapUnitViewModel2.Unit.BaseObject)
				{
					num2 = (DoesCurrentLaunchSeekerMatchTarget(ammunitionByName._ap, mapUnitViewModel2.Unit.BaseObject) ? 1 : 0);
					if (num2 != 0)
					{
						num3 = (Source.DoesAmmoMatchTarget(Ammunition, mapUnitViewModel2.Unit.BaseObject, ignoreSituationalRestrictions) ? 1 : 0);
						goto IL_08a8;
					}
				}
				else
				{
					num2 = 0;
				}
				num3 = 0;
				goto IL_08a8;
			}
			if (v is MapViewModel)
			{
				if (!flag && !Source.CrewVisionBlocksAmmo(ammunitionByName?._ap, null))
				{
					base.CursorName = "attacking";
				}
				else
				{
					base.CursorName = "unavailable";
				}
				if (v != _targetItem || GameTime.unscaledTime - _lastRangeUpdateTime >= 0.20000000298023224)
				{
					SetTargetItem(v);
					_lastRangeUpdateTime = GameTime.unscaledTime;
					UpdateDisplayedRange(v, ammunitionByName);
				}
			}
		}
		goto IL_09ce;
		IL_08a8:
		bool flag4 = (byte)num3 != 0;
		bool flag5 = num2 != 0 && !flag4 && Source.DoesAmmoMatchTarget(Ammunition, mapUnitViewModel2.Unit.BaseObject, ignoreSituationalRestrictions: true);
		if (flag4 || flag5)
		{
			base.CursorName = (flag4 ? "attackunit" : "unavailable");
			_ = ammunitionByName.getWeaponSystems().FirstOrDefault() is WeaponSystemHardpoint;
			if (v != _targetItem || GameTime.unscaledTime - _lastRangeUpdateTime >= 0.20000000298023224)
			{
				SetTargetItem(v);
				_lastRangeUpdateTime = GameTime.unscaledTime;
				UpdateDisplayedRange(v, ammunitionByName);
			}
		}
		else
		{
			SetTargetItem(null);
			base.CursorName = "unavailable";
		}
		goto IL_09ce;
		IL_09ce:
		return base.MouseAction(v, args, mousePosition, map, ref handled);
	}

	private void UpdateWakeHomingForCurrentAim(ObjectBase target)
	{
		if (Ammo?._ap == null || Ammo._ap._type != SeaPower.Ammunition.Type.Torpedo)
		{
			IsWakeHomingForCurrentAim = false;
			return;
		}
		AmmunitionParameters.GuidanceType guidanceType = ((target == null) ? GuidanceTypeForCurrentBearingLaunch(Ammo._ap) : GuidanceTypeForCurrentLaunch(Ammo._ap, target._type));
		IsWakeHomingForCurrentAim = guidanceType == AmmunitionParameters.GuidanceType.WakeHoming;
	}

	private void UpdateBearingPreview(GeoPosition mousePosition)
	{
		if (Source == null)
		{
			return;
		}
		Ammunition ammunitionByName = Source.getAmmunitionByName(Ammunition);
		if (ammunitionByName?._ap == null || Source.DoesEngageRequiresTarget(Ammunition) || (ammunitionByName._ap._type != SeaPower.Ammunition.Type.Missile && ammunitionByName._ap._type != SeaPower.Ammunition.Type.Torpedo && ammunitionByName._ap._type != SeaPower.Ammunition.Type.ASROC))
		{
			ClearBearingPreview();
			return;
		}
		Vector3 position = Source.transform.position;
		Vector3 vector = mousePosition.ToUnity();
		vector.y = ammunitionByName._ap.TypicalTargetAlt;
		Vector3 vector2 = vector;
		vector2.y = position.y;
		float num = Vector3.Distance(position, vector2);
		float num2 = 0f;
		float num3;
		if (ammunitionByName._ap._type == SeaPower.Ammunition.Type.Torpedo)
		{
			Vector2 vector3 = BearingLaunchPrediction.TorpedoLaunchProfile(ammunitionByName._ap, Source, num);
			num2 = vector3.x;
			num3 = vector3.y;
		}
		else
		{
			num3 = ((MaxRangeInLat > 0.0) ? ((float)(MaxRangeInLat / 0.0006047522103382444)) : ammunitionByName._ap.EffectiveMaxLaunchRange(Source));
		}
		IsBearingPreviewVisible = true;
		IsTargetPreviewValid = false;
		IsTargetPreviewInvalid = false;
		IsBearingPreviewInvalid = num > num3;
		float num4;
		float num5;
		if (ammunitionByName._ap._type == SeaPower.Ammunition.Type.Missile)
		{
			AmmunitionParameters.GuidanceType guidanceType = GuidanceTypeForCurrentBearingLaunch(ammunitionByName._ap);
			num4 = MissileActivationLead(ammunitionByName._ap, guidanceType, position, vector, num);
			num5 = BearingLaunchPrediction.SeekerRange(ammunitionByName._ap, guidanceType);
		}
		else
		{
			num4 = BearingLaunchPrediction.ActivationLeadDistance(ammunitionByName._ap);
			num5 = BearingLaunchPrediction.EffectiveSeekerRange(ammunitionByName._ap);
		}
		float seekerFovDegrees = ammunitionByName._ap._wideSeekerFOV;
		AmmunitionParameters.SearchMode searchMode = ammunitionByName._ap.SearchModeSetting.Value;
		ReactiveProperty<AmmunitionParameters.SearchMode> value;
		if (ammunitionByName._ap._type == SeaPower.Ammunition.Type.ASROC)
		{
			num5 = ammunitionByName._ap._submunitionSeekerRange;
			seekerFovDegrees = ammunitionByName._ap._submunitionSeekerFOV;
			searchMode = ammunitionByName._ap._submunitionSearchMode;
		}
		else if (Source._searchModeSettings.TryGetValue(ammunitionByName._ap._ammunitionFileName, out value))
		{
			searchMode = value.Value;
		}
		if (ammunitionByName._ap._type == SeaPower.Ammunition.Type.Torpedo && GuidanceTypeForCurrentBearingLaunch(ammunitionByName._ap) == AmmunitionParameters.GuidanceType.WakeHoming)
		{
			searchMode = AmmunitionParameters.SearchMode.None;
		}
		bool flag = searchMode == AmmunitionParameters.SearchMode.Circle || searchMode == AmmunitionParameters.SearchMode.Spiral;
		if (ammunitionByName._ap._type == SeaPower.Ammunition.Type.Torpedo && ammunitionByName._ap._guidanceType == AmmunitionParameters.GuidanceType.None && searchMode == AmmunitionParameters.SearchMode.None)
		{
			num4 = 0f;
		}
		if (IsBearingPreviewInvalid)
		{
			BearingValidTransitLine = EmptyBearingPreviewLine;
			BearingInvalidTransitLine = ToGeoPositions(new List<Vector3> { position, vector2 });
			BearingActivationDistanceText = "";
			BearingActivationTimeText = "";
			BearingWakeDistanceText = "";
			BearingWakeVelocityText = "";
			BearingWakeHitChanceLabel = "";
			BearingWakeHitChanceText = "";
			IsBearingActivationTimeVisible = false;
			IsBearingWakeEstimateVisible = false;
			ClearBearingAcquisitionGeometry();
			NotifyBearingPreviewChanged();
			ScheduleBearingPreviewRefresh();
			return;
		}
		bool drawAcquisitionArea = searchMode == AmmunitionParameters.SearchMode.None || searchMode == AmmunitionParameters.SearchMode.Snake || flag;
		if (searchMode == AmmunitionParameters.SearchMode.Snake)
		{
			seekerFovDegrees = BearingLaunchPrediction.SnakeEnvelopeFov(ammunitionByName._ap, num5, seekerFovDegrees);
			float activationTravel = Mathf.Clamp(num - Mathf.Max(0f, num4), 0f, num3);
			num3 = BearingLaunchPrediction.SnakeProjectedMaximumRange(ammunitionByName._ap, num3, activationTravel);
		}
		float trackEnd = ((ammunitionByName._ap._type == SeaPower.Ammunition.Type.Missile && searchMode != AmmunitionParameters.SearchMode.Snake) ? num : float.PositiveInfinity);
		BearingLaunchPrediction.Geometry geometry = (flag ? BearingLaunchPrediction.BuildCircleGeometry(ammunitionByName._ap, position, vector2, num3, num4, num5, seekerFovDegrees) : BearingLaunchPrediction.BuildGeometry(position, vector2, num3, num4, num5, seekerFovDegrees, drawAcquisitionArea, trackEnd));
		BearingActivationPosition = new GeoPosition(geometry.ActivationPoint);
		BearingValidTransitLine = ToGeoPositions(new List<Vector3> { position, geometry.ActivationPoint });
		BearingInvalidTransitLine = EmptyBearingPreviewLine;
		BearingSeekerArc = ToGeoPositions(geometry.SeekerArc);
		BearingSeekerSides = ToGeoPositions(geometry.SeekerSides);
		BearingPortRail = ToGeoPositions(geometry.PortRail);
		BearingStarboardRail = ToGeoPositions(geometry.StarboardRail);
		BearingCenterLine = ToGeoPositions(geometry.CenterLine);
		BearingSeekerEndArc = ToGeoPositions(geometry.EndArc);
		IsBearingCircleVisible = flag && num5 > 0f;
		BearingCircleCenter = (IsBearingCircleVisible ? new GeoPosition(vector2) : default(GeoPosition));
		BearingCircleRadiusInLat = (IsBearingCircleVisible ? ((float)((double)num5 * 0.0006047522103382444)) : 0f);
		float num6 = position.FlatDistance(geometry.ActivationPoint);
		string text = Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "Distance");
		string text2 = Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "NauticalMiles");
		BearingActivationDistanceText = $"{text}: {num6 * 0.036285132f:F1} {text2}";
		if (ammunitionByName._ap._type == SeaPower.Ammunition.Type.Torpedo && num2 > 0f)
		{
			_cachedBearingActivationTime = num6 / (num2 * 0.0076554087f);
		}
		else if (ammunitionByName._ap._type == SeaPower.Ammunition.Type.Missile && (double)Time.unscaledTime - _lastBearingTimeUpdate >= 0.20000000298023224)
		{
			_lastBearingTimeUpdate = Time.unscaledTime;
			float startVelocityKnots = (Source.IsAirUnit ? Source._velocityInKnots : 0f);
			_cachedBearingActivationTime = MissileSimulator.EstimateTimeToFlatDistance(ammunitionByName._ap, position, startVelocityKnots, vector, Source.IsAirUnit, num6);
		}
		IsBearingActivationTimeVisible = _cachedBearingActivationTime >= 0f && ammunitionByName._ap._type != SeaPower.Ammunition.Type.ASROC;
		BearingActivationTimeText = (IsBearingActivationTimeVisible ? FormatBearingActivationTime(_cachedBearingActivationTime) : "");
		IsBearingWakeEstimateVisible = false;
		IsBearingWakeDistanceVisible = false;
		BearingWakeDistanceText = "";
		BearingWakeVelocityText = "";
		BearingWakeHitChanceLabel = "";
		BearingWakeHitChanceText = "";
		if (ammunitionByName._ap._type == SeaPower.Ammunition.Type.Torpedo && GuidanceTypeForCurrentBearingLaunch(ammunitionByName._ap) == AmmunitionParameters.GuidanceType.WakeHoming && BearingLaunchPrediction.TryEstimateWakeRun(ammunitionByName._ap, num6, num2, out var estimate))
		{
			string text3 = Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "WakeDistance");
			string text4 = Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "WakeVelocity");
			string text5 = Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "Knots");
			BearingWakeDistanceText = $"{text3} {estimate.DistanceAlongWake * 0.036285132f:F1} {text2}";
			BearingWakeVelocityText = $"{text4} {estimate.EffectiveVelocityInKnots:F0} {text5}";
			if (IsBearingActivationTimeVisible && estimate.EffectiveVelocityInKnots > 0f)
			{
				float num7 = estimate.DistanceAlongWake / (estimate.EffectiveVelocityInKnots * 0.0076554087f);
				BearingActivationTimeText = FormatBearingActivationTime(_cachedBearingActivationTime, _cachedBearingActivationTime + num7);
			}
			IsBearingWakeEstimateVisible = true;
			IsBearingWakeDistanceVisible = true;
		}
		NotifyBearingPreviewChanged();
		ScheduleBearingPreviewRefresh();
	}

	private void UpdateTargetPreview(ObjectBase target)
	{
		Ammunition ammunitionByName = Source.getAmmunitionByName(Ammunition);
		if (!CanPreviewAttackTarget(ammunitionByName, target))
		{
			ClearBearingPreview();
			return;
		}
		if (ammunitionByName._ap._type != SeaPower.Ammunition.Type.Torpedo || GuidanceTypeForCurrentLaunch(ammunitionByName._ap, target._type) != AmmunitionParameters.GuidanceType.WakeHoming)
		{
			UpdateConventionalTargetPreview(ammunitionByName, target);
			return;
		}
		Vector3 position = Source.transform.position;
		Vector3 position2 = target.transform.position;
		Vector3 vector = position2;
		vector.y = position.y;
		float num = Vector3.Distance(position, vector);
		Vector2 vector2 = BearingLaunchPrediction.TorpedoLaunchProfile(ammunitionByName._ap, Source, num);
		bool flag = num <= vector2.y;
		BearingLaunchPrediction.WakeTargetSolution solution = default(BearingLaunchPrediction.WakeTargetSolution);
		bool flag2 = flag && BearingLaunchPrediction.TrySolveWakeTarget(ammunitionByName._ap, position, position2, target.UnityVelocityVector, target.transform.forward, vector2.x, vector2.y, out solution);
		IsBearingPreviewVisible = true;
		IsTargetPreviewValid = flag2 && solution.CanReach;
		IsTargetPreviewInvalid = !IsTargetPreviewValid;
		IsBearingPreviewInvalid = IsTargetPreviewInvalid;
		ClearBearingAcquisitionGeometry();
		Vector3 vector3 = (flag2 ? solution.ActivationPoint : vector);
		vector3.y = position.y;
		BearingActivationPosition = new GeoPosition(vector3);
		BearingValidTransitLine = (IsTargetPreviewValid ? ToGeoPositions(new List<Vector3> { position, vector3 }) : EmptyBearingPreviewLine);
		BearingInvalidTransitLine = (IsTargetPreviewInvalid ? ToGeoPositions(new List<Vector3> { position, vector3 }) : EmptyBearingPreviewLine);
		IsBearingActivationTimeVisible = flag && flag2;
		IsBearingWakeEstimateVisible = flag && flag2;
		IsBearingWakeDistanceVisible = false;
		BearingWakeDistanceText = "";
		if (flag && flag2)
		{
			string text = Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "TotalDistance");
			string text2 = Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "TotalTime");
			string text3 = Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "WakeVelocity");
			string text4 = Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "NauticalMiles");
			string text5 = Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "Knots");
			string text6 = Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "SecondShort");
			BearingActivationDistanceText = $"{text} {solution.TransitDistance * 0.036285132f:F1} + {solution.WakeDistance * 0.036285132f:F1} {text4}";
			BearingActivationTimeText = $"{text2} {Mathf.RoundToInt(solution.TotalTime)}{text6}";
			BearingWakeVelocityText = $"{text3} {solution.EffectiveWakeVelocityInKnots:F0} {text5}";
			float num2 = solution.SafetyOffset * 0.036285132f;
			string key;
			if (num2 > 0.75f)
			{
				BearingWakeHitChanceLevel = "High";
				key = "HitChanceHigh";
			}
			else if (num2 > 0.4f)
			{
				BearingWakeHitChanceLevel = "Moderate";
				key = "HitChanceModerate";
			}
			else
			{
				BearingWakeHitChanceLevel = "Low";
				key = "HitChanceLow";
			}
			BearingWakeHitChanceLabel = Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "HitChance");
			BearingWakeHitChanceText = Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", key);
		}
		else
		{
			BearingActivationDistanceText = "";
			BearingActivationTimeText = "";
			BearingWakeVelocityText = "";
			BearingWakeHitChanceLabel = "";
			BearingWakeHitChanceText = "";
		}
		NotifyBearingPreviewChanged();
	}

	private bool CanPreviewAttackTarget(Ammunition ammo, ObjectBase target)
	{
		if (Source == null || target == null || target == Source || ammo?._ap == null)
		{
			return false;
		}
		if (!WakebubbleLayerViewModel.IsLocalizedContact(Globals._playerTaskforce?.PlottingTable?.VehicleForObject(target)))
		{
			return false;
		}
		if (ammo._ap._type != SeaPower.Ammunition.Type.Missile && ammo._ap._type != SeaPower.Ammunition.Type.Torpedo && ammo._ap._type != SeaPower.Ammunition.Type.ASROC)
		{
			return false;
		}
		AmmunitionParameters.GuidanceType guidanceType = GuidanceTypeForCurrentLaunch(ammo._ap, target._type);
		if (guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming || guidanceType == AmmunitionParameters.GuidanceType.LaserHoming)
		{
			return false;
		}
		bool flag = ammo._ap._guidanceType == AmmunitionParameters.GuidanceType.AntiRadiationHoming;
		if (DoesCurrentLaunchSeekerMatchTarget(ammo._ap, target))
		{
			return Source.DoesAmmoMatchTarget(Ammunition, target, _isWaypointEdit || flag);
		}
		return false;
	}

	private void UpdateConventionalTargetPreview(Ammunition ammo, ObjectBase target)
	{
		Vector3 position = Source.transform.position;
		Vector3 position2 = target.transform.position;
		Vector3 unityVelocityVector = target.UnityVelocityVector;
		Vector3 vector = position2;
		float num = -1f;
		bool flag = true;
		if (ammo._ap._type == SeaPower.Ammunition.Type.Missile)
		{
			(num, flag, vector) = Missile.CalculateMissileInterceptTimeAnalytical(ammo._ap, position, Source.IsAirUnit ? Source._velocityInKnots : 0f, position2, unityVelocityVector, ammo._ap.GetDragFactor(Source.IsAirUnit));
		}
		else if (ammo._ap._type == SeaPower.Ammunition.Type.Torpedo)
		{
			(Vector3 interceptPosition, bool canIntercept) tuple2 = Utils.CalculateInterceptPosition(position, ammo._ap._maxVelocityInKnots * 0.0076554087f, position2, unityVelocityVector);
			flag = tuple2.canIntercept;
			(vector, _) = tuple2;
		}
		else if (ammo._ap._type == SeaPower.Ammunition.Type.ASROC)
		{
			float num2 = ((ammo._ap._sinkRate > 0f) ? Mathf.Max(0f, (0f - position2.y) * 67.200066f / ammo._ap._sinkRate) : 0f);
			Vector3 targetPosition = position2 + unityVelocityVector * num2;
			targetPosition.y = 0f;
			(Vector3 interceptPosition, bool canIntercept) tuple4 = Utils.CalculateInterceptPosition(position, ammo._ap._maxVelocityInKnots * 0.0076554087f, targetPosition, unityVelocityVector);
			flag = tuple4.canIntercept;
			(vector, _) = tuple4;
		}
		Vector3 vector2 = vector;
		vector2.y = position.y;
		float num3 = position.FlatDistance(vector2);
		float num4 = 0f;
		float num5;
		if (ammo._ap._type == SeaPower.Ammunition.Type.Torpedo)
		{
			Vector2 vector3 = BearingLaunchPrediction.TorpedoLaunchProfile(ammo._ap, Source, num3);
			num4 = vector3.x;
			num5 = vector3.y;
			if (num4 > 0f)
			{
				num = num3 / (num4 * 0.0076554087f);
			}
		}
		else
		{
			num5 = ammo._ap.EffectiveMaxLaunchRange(Source);
		}
		bool flag2 = flag && num3 <= num5;
		float num6;
		float num7;
		if (ammo._ap._type == SeaPower.Ammunition.Type.Missile)
		{
			AmmunitionParameters.GuidanceType guidanceType = GuidanceTypeForCurrentLaunch(ammo._ap, target._type);
			num6 = ((ammo._ap._guidanceType == AmmunitionParameters.GuidanceType.IRHoming && Vector3.Distance(position, position2) < ammo._ap._terminalApproachDist) ? num3 : MissileActivationLead(ammo._ap, guidanceType, position, vector, num3));
			num7 = BearingLaunchPrediction.SeekerRange(ammo._ap, guidanceType);
		}
		else
		{
			num6 = BearingLaunchPrediction.ActivationLeadDistance(ammo._ap);
			num7 = BearingLaunchPrediction.EffectiveSeekerRange(ammo._ap);
		}
		float seekerFovDegrees = ammo._ap._narrowSeekerFOV;
		AmmunitionParameters.SearchMode searchMode = ammo._ap.SearchModeSetting.Value;
		ReactiveProperty<AmmunitionParameters.SearchMode> value;
		if (ammo._ap._type == SeaPower.Ammunition.Type.ASROC)
		{
			num7 = ammo._ap._submunitionSeekerRange;
			seekerFovDegrees = ammo._ap._submunitionSeekerFOV;
			searchMode = ammo._ap._submunitionSearchMode;
		}
		else if (Source._searchModeSettings.TryGetValue(ammo._ap._ammunitionFileName, out value))
		{
			searchMode = value.Value;
		}
		bool flag3 = searchMode == AmmunitionParameters.SearchMode.Circle || searchMode == AmmunitionParameters.SearchMode.Spiral;
		bool flag4 = flag2 && (searchMode == AmmunitionParameters.SearchMode.None || searchMode == AmmunitionParameters.SearchMode.Snake || flag3);
		if (flag4 && searchMode == AmmunitionParameters.SearchMode.Snake)
		{
			seekerFovDegrees = BearingLaunchPrediction.SnakeEnvelopeFov(ammo._ap, num7, seekerFovDegrees);
			float activationTravel = Mathf.Clamp(num3 - Mathf.Max(0f, num6), 0f, num5);
			num5 = BearingLaunchPrediction.SnakeProjectedMaximumRange(ammo._ap, num5, activationTravel);
			flag2 = num3 <= num5;
			flag4 = flag2;
		}
		float num8 = float.PositiveInfinity;
		if (ammo._ap._type == SeaPower.Ammunition.Type.Missile && searchMode != AmmunitionParameters.SearchMode.Snake)
		{
			num8 = num3;
			if (target.isAirTarget() && num > 0f)
			{
				num8 += num3 / num * (ammo._ap._selfDestructDelay + 2f);
			}
		}
		BearingLaunchPrediction.Geometry geometry = ((flag4 && flag3) ? BearingLaunchPrediction.BuildCircleGeometry(ammo._ap, position, vector2, num5, num6, num7, seekerFovDegrees) : BearingLaunchPrediction.BuildGeometry(position, vector2, num5, num6, num7, seekerFovDegrees, flag4, num8));
		IsBearingPreviewVisible = true;
		IsTargetPreviewValid = flag2;
		IsTargetPreviewInvalid = !flag2;
		IsBearingPreviewInvalid = !flag2;
		BearingActivationPosition = new GeoPosition(vector2);
		BearingValidTransitLine = (flag2 ? ToGeoPositions(new List<Vector3> { position, vector2 }) : EmptyBearingPreviewLine);
		BearingInvalidTransitLine = (flag2 ? EmptyBearingPreviewLine : ToGeoPositions(new List<Vector3> { position, vector2 }));
		BearingSeekerArc = ToGeoPositions(geometry.SeekerArc);
		BearingSeekerSides = ToGeoPositions(geometry.SeekerSides);
		BearingPortRail = ToGeoPositions(geometry.PortRail);
		BearingStarboardRail = ToGeoPositions(geometry.StarboardRail);
		BearingCenterLine = ToGeoPositions(geometry.CenterLine);
		BearingSeekerEndArc = ToGeoPositions(geometry.EndArc);
		IsBearingCircleVisible = flag2 && flag3 && num7 > 0f;
		BearingCircleCenter = (IsBearingCircleVisible ? new GeoPosition(vector2) : default(GeoPosition));
		BearingCircleRadiusInLat = (IsBearingCircleVisible ? ((float)((double)num7 * 0.0006047522103382444)) : 0f);
		string text = Singleton<LanguageResourceHandler>.Instance.getText("MiniMap", "Distance");
		string text2 = Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "NauticalMiles");
		BearingActivationDistanceText = $"{text}: {num3 * 0.036285132f:F1} {text2}";
		IsBearingActivationTimeVisible = flag2 && num >= 0f && ammo._ap._type != SeaPower.Ammunition.Type.ASROC;
		BearingActivationTimeText = (IsBearingActivationTimeVisible ? FormatBearingActivationTime(num) : "");
		IsBearingWakeEstimateVisible = false;
		IsBearingWakeDistanceVisible = false;
		BearingWakeDistanceText = "";
		BearingWakeVelocityText = "";
		BearingWakeHitChanceLabel = "";
		BearingWakeHitChanceText = "";
		NotifyBearingPreviewChanged();
	}

	private float MissileActivationLead(AmmunitionParameters ap, AmmunitionParameters.GuidanceType guidance, Vector3 launch, Vector3 aim, float aimDistance)
	{
		float num = BearingLaunchPrediction.MissileActivationLead(ap, guidance, aimDistance, 0f);
		if (num > Mathf.Min(ap._terminalApproachDist, aimDistance))
		{
			return num;
		}
		float startVelocityKnots = (Source.IsAirUnit ? Source._velocityInKnots : 0f);
		if (aimDistance - ap._terminalApproachDist >= BearingLaunchPrediction.MissileTerminalDelayTravelBound(ap, startVelocityKnots))
		{
			return num;
		}
		if (_cachedTerminalDelayTravelAp != ap || (double)Time.unscaledTime - _lastTerminalDelayTravelUpdate >= 0.20000000298023224)
		{
			_cachedTerminalDelayTravelAp = ap;
			_lastTerminalDelayTravelUpdate = Time.unscaledTime;
			_cachedTerminalDelayTravel = MissileSimulator.EstimateFlatDistanceAtTime(ap, launch, startVelocityKnots, aim, Source.IsAirUnit, BearingLaunchPrediction.MissileTerminalDelay(ap));
		}
		return BearingLaunchPrediction.MissileActivationLead(ap, guidance, aimDistance, _cachedTerminalDelayTravel);
	}

	private AmmunitionParameters.GuidanceType GuidanceTypeForCurrentLaunch(AmmunitionParameters ap, ObjectBase.ObjectType targetType)
	{
		return ap.GuidanceTypeForLaunchSelection(CurrentLaunchSeekerMode(ap), targetType);
	}

	private AmmunitionParameters.GuidanceType GuidanceTypeForCurrentBearingLaunch(AmmunitionParameters ap)
	{
		return CurrentLaunchSeekerMode(ap) switch
		{
			AmmunitionParameters.LaunchSeekerMode.Primary => ap.PrimaryLaunchGuidanceType, 
			AmmunitionParameters.LaunchSeekerMode.Secondary => ap._vesselAttackGuidanceType, 
			_ => ap._guidanceType, 
		};
	}

	private AmmunitionParameters.LaunchSeekerMode CurrentLaunchSeekerMode(AmmunitionParameters ap)
	{
		if (Source != null && Source._launchSeekerModeSettings.TryGetValue(ap._ammunitionFileName, out var value))
		{
			return value.Value;
		}
		return AmmunitionParameters.LaunchSeekerMode.Auto;
	}

	private bool DoesCurrentLaunchSeekerMatchTarget(AmmunitionParameters ap, ObjectBase target)
	{
		if (ap._type != SeaPower.Ammunition.Type.Torpedo || target == null)
		{
			return true;
		}
		return CurrentLaunchSeekerMode(ap) switch
		{
			AmmunitionParameters.LaunchSeekerMode.Primary => !(target is Vessel), 
			AmmunitionParameters.LaunchSeekerMode.Secondary => target is Vessel, 
			_ => true, 
		};
	}

	private void ScheduleBearingPreviewRefresh()
	{
		_bearingPreviewRefreshTimer?.Dispose();
		_bearingPreviewRefreshTimer = Observable.Timer(TimeSpan.FromSeconds(0.25)).ObserveOnMainThread().Subscribe(delegate
		{
			_bearingPreviewRefreshTimer = null;
			if (_bearingPreviewOverMap)
			{
				UpdateBearingPreview(_bearingPreviewMousePosition);
			}
		});
	}

	private static string FormatBearingActivationTime(float seconds, float? completeSeconds = null)
	{
		int num = Mathf.Max(0, Mathf.RoundToInt(seconds));
		string text = Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "TimeOfFlight");
		string text2 = Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "SecondShort");
		string text3 = $"{text} {num}{text2}";
		if (completeSeconds.HasValue)
		{
			text3 += $" ({Mathf.Max(0, Mathf.RoundToInt(completeSeconds.Value))}{text2})";
		}
		return text3;
	}

	private static List<GeoPosition> ToGeoPositions(IReadOnlyList<Vector3> points)
	{
		if (points == null || points.Count == 0)
		{
			return EmptyBearingPreviewLine;
		}
		List<GeoPosition> list = new List<GeoPosition>(points.Count);
		for (int i = 0; i < points.Count; i++)
		{
			list.Add(new GeoPosition(points[i]));
		}
		return list;
	}

	private void ClearBearingAcquisitionGeometry()
	{
		BearingSeekerArc = EmptyBearingPreviewLine;
		BearingSeekerSides = EmptyBearingPreviewLine;
		BearingPortRail = EmptyBearingPreviewLine;
		BearingStarboardRail = EmptyBearingPreviewLine;
		BearingCenterLine = EmptyBearingPreviewLine;
		BearingSeekerEndArc = EmptyBearingPreviewLine;
		IsBearingCircleVisible = false;
		BearingCircleCenter = default(GeoPosition);
		BearingCircleRadiusInLat = 0f;
	}

	private void ClearBearingPreview()
	{
		if (IsBearingPreviewVisible || BearingValidTransitLine.Count != 0 || BearingInvalidTransitLine.Count != 0)
		{
			IsBearingPreviewVisible = false;
			IsBearingPreviewInvalid = false;
			IsBearingActivationTimeVisible = false;
			IsBearingWakeEstimateVisible = false;
			IsBearingWakeDistanceVisible = false;
			IsTargetPreviewValid = false;
			IsTargetPreviewInvalid = false;
			_cachedBearingActivationTime = -1f;
			_cachedTerminalDelayTravelAp = null;
			_bearingPreviewRefreshTimer?.Dispose();
			_bearingPreviewRefreshTimer = null;
			BearingActivationDistanceText = "";
			BearingActivationTimeText = "";
			BearingWakeDistanceText = "";
			BearingWakeVelocityText = "";
			BearingWakeHitChanceLabel = "";
			BearingWakeHitChanceText = "";
			BearingValidTransitLine = EmptyBearingPreviewLine;
			BearingInvalidTransitLine = EmptyBearingPreviewLine;
			ClearBearingAcquisitionGeometry();
			NotifyBearingPreviewChanged();
		}
	}

	private void NotifyBearingPreviewChanged()
	{
		OnPropertyChanged("BearingValidTransitLine");
		OnPropertyChanged("BearingInvalidTransitLine");
		OnPropertyChanged("BearingSeekerArc");
		OnPropertyChanged("BearingSeekerSides");
		OnPropertyChanged("BearingPortRail");
		OnPropertyChanged("BearingStarboardRail");
		OnPropertyChanged("BearingCenterLine");
		OnPropertyChanged("BearingSeekerEndArc");
		OnPropertyChanged("BearingCircleCenter");
		OnPropertyChanged("BearingCircleRadiusInLat");
		OnPropertyChanged("IsBearingCircleVisible");
		OnPropertyChanged("BearingActivationPosition");
		OnPropertyChanged("BearingActivationDistanceText");
		OnPropertyChanged("BearingActivationTimeText");
		OnPropertyChanged("BearingWakeDistanceText");
		OnPropertyChanged("BearingWakeVelocityText");
		OnPropertyChanged("BearingWakeHitChanceLabel");
		OnPropertyChanged("BearingWakeHitChanceText");
		OnPropertyChanged("BearingWakeHitChanceLevel");
		OnPropertyChanged("IsBearingPreviewVisible");
		OnPropertyChanged("IsBearingPreviewInvalid");
		OnPropertyChanged("IsBearingActivationTimeVisible");
		OnPropertyChanged("IsBearingWakeEstimateVisible");
		OnPropertyChanged("IsBearingWakeDistanceVisible");
		OnPropertyChanged("IsTargetPreviewValid");
		OnPropertyChanged("IsTargetPreviewInvalid");
	}

	private void DropSonobuoys(GeoPosition mousePosition, int salvo, Ammunition ammo)
	{
		float num = 2f;
		int num2 = 9;
		switch (salvo)
		{
		case 4:
			num2 = 9;
			num = 5f;
			break;
		case 3:
			num2 = 16;
			num = 5f;
			break;
		}
		float num3 = num * 27.559496f;
		int num4 = (int)Math.Ceiling(Mathf.Sqrt(num2));
		float num5 = 0f - num3;
		GeoPosition geoPosition = mousePosition.OffsetVector(new Vector3(num3 / 2f * (float)(num4 - 1), 0f, num5 / 2f * (float)(num4 - 1)));
		for (int i = 0; i < num4; i++)
		{
			if (i > 0)
			{
				geoPosition = geoPosition.OffsetVector(new Vector3(0f - num3, 0f, 0f));
			}
			num5 = 0f - num5;
			for (int j = 0; j < num4; j++)
			{
				if (j > 0)
				{
					geoPosition = geoPosition.OffsetVector(new Vector3(0f, 0f, num5));
				}
				if (i == 0 && j == 0)
				{
					DM.I($"Sonobouys drop: {Source._selectedAmmunitionForEngage} x {num2}, Spacing: {num} nmi, Square pattern");
					Source.DefaultConfirmationMessageAndVO();
				}
				VisualActionTask selectedTask = Source._userRoot.SelectedTask;
				if (selectedTask != null)
				{
					OffsetAttack(null, geoPosition, geoPosition, 1, selectedTask, ammo);
				}
				else
				{
					OffsetAttack(null, geoPosition, geoPosition, 1, null, ammo);
				}
				num2--;
				if (num2 < 1)
				{
					break;
				}
			}
			if (num2 < 1)
			{
				break;
			}
		}
	}

	private void OffsetAttack(object v, GeoPosition targetGeoPosition, GeoPosition mousePosition, int salvo, VisualActionTask after, Ammunition ammo)
	{
		ObjectBase targetObject = null;
		if (v is MapUnitViewModel mapUnitViewModel)
		{
			targetObject = mapUnitViewModel.Unit.BaseObject;
		}
		if (_lastSelectedTask == null)
		{
			_lastSelectedTask = after;
		}
		_lastSelectedTask = Source.SetAttackAtWaypointTask(ammo._ap._ammunitionFileName, targetObject, targetGeoPosition, mousePosition, salvo, _lastSelectedTask);
	}

	private void NormalAttack(object v, GeoPosition mousePosition, int salvo)
	{
		ObjectBase targetObject = null;
		if (v is MapUnitViewModel mapUnitViewModel)
		{
			targetObject = mapUnitViewModel.Unit.BaseObject;
		}
		bool flag = false;
		if (Source.FollowingFormation)
		{
			if (Source.Formation.SelectedControlMode == UnitFormation.ControlMode.Local)
			{
				flag = true;
			}
			else if (Source.Formation.SelectedControlMode == UnitFormation.ControlMode.FollowLeader && Source.IsFormationWingman)
			{
				flag = true;
			}
		}
		else
		{
			flag = true;
		}
		if (Input.GetKey("f"))
		{
			flag = true;
		}
		if (flag)
		{
			AddAttackTask(Source, targetObject, mousePosition, salvo);
			return;
		}
		foreach (Station station in Source.Formation.Stations)
		{
			ObjectBase unitObject = station.UnitObject;
			if (!(unitObject == null) && unitObject.FollowingFormation && unitObject.getAmmunitionByName(Ammunition) != null && (!(unitObject is Aircraft aircraft) || aircraft._isInFlight) && (!(unitObject is Helicopter helicopter) || helicopter._isInFlight))
			{
				AddAttackTask(unitObject, targetObject, mousePosition, salvo);
			}
		}
	}

	private void NormalAttack_bkp(object v, GeoPosition mousePosition, int salvo)
	{
		ObjectBase objectBase = null;
		if (v is MapUnitViewModel mapUnitViewModel)
		{
			objectBase = mapUnitViewModel.Unit.BaseObject;
		}
		if (Source.IsAirUnit)
		{
			bool flag = false;
			if (Source.FollowingFormation)
			{
				if (Source.Formation.SelectedControlMode == UnitFormation.ControlMode.Local)
				{
					flag = true;
				}
				else if (Source.Formation.SelectedControlMode == UnitFormation.ControlMode.FollowLeader && Source.IsFormationWingman)
				{
					flag = true;
				}
			}
			else
			{
				flag = true;
			}
			if (Input.GetKey("f"))
			{
				flag = true;
			}
			if (flag)
			{
				AddAttackTask(Source, objectBase, mousePosition, salvo);
				return;
			}
			{
				foreach (Station station in Source.Formation.Stations)
				{
					if (station.UnitObject.FollowingFormation && station.UnitObject.getAmmunitionByName(Ammunition) != null && (!(station.UnitObject is Aircraft aircraft) || aircraft._isInFlight) && (!(station.UnitObject is Helicopter helicopter) || helicopter._isInFlight))
					{
						AddAttackTask(station.UnitObject, objectBase, mousePosition, salvo);
					}
				}
				return;
			}
		}
		if (Globals._passOrdersToFormationMembers && Source.FollowingFormation)
		{
			foreach (Station station2 in Source.Formation.Stations)
			{
				if (station2.UnitObject.FollowingFormation && station2.UnitObject.getAmmunitionByName(Ammunition) != null && (!(station2.UnitObject is Aircraft aircraft2) || aircraft2._isInFlight) && (!(station2.UnitObject is Helicopter helicopter2) || helicopter2._isInFlight))
				{
					AttackTask attackTask = station2.UnitObject._userRoot?.AddTaskNow<AttackTask>();
					if ((bool)objectBase)
					{
						attackTask.targetObject.value = objectBase;
					}
					else
					{
						attackTask.bearingPosition.value = mousePosition;
					}
					attackTask.salvo.value = salvo;
					attackTask._displayOrderText = true;
					attackTask.ammunitionForEngage.value = Ammunition;
				}
			}
			return;
		}
		AddAttackTask(Source, objectBase, mousePosition, salvo);
	}

	private void AddAttackTask(ObjectBase attacker, ObjectBase targetObject, GeoPosition mousePosition, int salvo)
	{
		AttackTask attackTask = attacker._userRoot?.AddTaskNow<AttackTask>();
		if ((bool)targetObject)
		{
			attackTask.targetObject.value = targetObject;
		}
		else
		{
			attackTask.bearingPosition.value = mousePosition;
		}
		attackTask.salvo.value = salvo;
		attackTask._displayOrderText = true;
		attackTask.ammunitionForEngage.value = Ammunition;
	}

	private void UpdateDisplayedRange(object v, Ammunition ammo)
	{
		if (v == null)
		{
			return;
		}
		if (v is MapUnitViewModel mapUnitViewModel)
		{
			if (mapUnitViewModel.Unit == null || mapUnitViewModel.Unit.BaseObject == null)
			{
				return;
			}
			ObjectBase.EngagementRange maxEngagementRanges = Source.GetMaxEngagementRanges(mapUnitViewModel.Unit.BaseObject, ammo, requireOpenSensorChannel: false, shortCircuitTerrainCheck: false, usePreciseKinematics: true);
			float num = Vector3.Distance(Source.transform.position, mapUnitViewModel.Unit.BaseObject.transform.position);
			float num2 = Vector3.Distance(Source.transform.position, new Vector3(mapUnitViewModel.Unit.BaseObject.transform.position.x, Source.transform.position.y, mapUnitViewModel.Unit.BaseObject.transform.position.z));
			float num3 = maxEngagementRanges.SensorRange;
			EngagementIssues = maxEngagementRanges.SensorData.EngagementIssues ?? new List<ObjectBase.EngagementIssue>();
			if (maxEngagementRanges.UnscreenedSensorRange > num3 && !mapUnitViewModel.Unit.Identified.Value && num > maxEngagementRanges.UnscreenedSensorRange)
			{
				num3 = maxEngagementRanges.UnscreenedSensorRange;
				EngagementIssues = EngagementIssues.Where((ObjectBase.EngagementIssue x) => x != ObjectBase.EngagementIssue.DefensiveJamming).ToList();
			}
			if (EngagementIssues.Contains(ObjectBase.EngagementIssue.HomeOnJam) && !mapUnitViewModel.Unit.Identified.Value && !Source.IsBeingOffensivelyJammedBy(mapUnitViewModel.Unit.BaseObject, ammo._ap._passiveRadarHomingFrequencies))
			{
				EngagementIssues = EngagementIssues.Where((ObjectBase.EngagementIssue x) => x != ObjectBase.EngagementIssue.HomeOnJam).ToList();
			}
			float num4 = ((ammo._ap._type == SeaPower.Ammunition.Type.Bomb) ? Utils.SlantToHorizontalRange(num3, Source.transform.position.y - mapUnitViewModel.Unit.BaseObject.transform.position.y) : num3);
			float num5 = Mathf.Min(maxEngagementRanges.KinematicRange, num4, maxEngagementRanges.TerrainRange);
			MaxRangeInLat = (double)num5 * 0.0006047522103382444;
			float num6 = ammo._ap.NoEscapeRangePrecise(Source, mapUnitViewModel.Unit.BaseObject, num5);
			NoEscapeRangeInLat = ((num6 > 0f) ? ((double)num6 * 0.0006047522103382444) : 0.0);
			MaxKinematicRange = maxEngagementRanges.KinematicRange / num2 * 100f;
			MaxDirectSightRange = maxEngagementRanges.TerrainRange / num * 100f;
			MaxSensorRange = num3 / num * 100f;
			float hitChance = 100f;
			if (mapUnitViewModel.Unit.IsClassified && mapUnitViewModel.Unit.Identified.Value)
			{
				hitChance = ammo._ap.CalculateHitChance(Source, mapUnitViewModel.Unit.BaseObject) * 100f;
				ObjectBase baseObject = mapUnitViewModel.Unit.BaseObject;
				if ((baseObject.isWeapon() || baseObject.IsAirUnit) && Source._obp != null)
				{
					bool flag = false;
					bool flag2 = false;
					foreach (WeaponSystem item in Source.GetWeaponSystemsForAmmunition(ammo._ap._ammunitionFileName))
					{
						if (item is WeaponSystemCIWS)
						{
							flag = true;
							break;
						}
						if (item is WeaponSystemGun)
						{
							flag2 = true;
						}
					}
					if (flag)
					{
						hitChance = WeaponSystemCIWS.PredictCombinedKillChance(Source, baseObject) * 100f;
					}
					else if (flag2)
					{
						hitChance = WeaponSystemGun.PredictCombinedKillChance(Source, baseObject, ammo) * 100f;
					}
				}
			}
			float expectedDeviation = ammo._ap.CalculateExpectedDeviation(Source, mapUnitViewModel.Unit.BaseObject.transform.position, mapUnitViewModel.Unit.BaseObject);
			if (mapUnitViewModel.Unit.BaseObject != Source)
			{
				mapUnitViewModel.UpdateContactMouseoverInfo(hitChance, (float)MaxKinematicRange, (float)MaxSensorRange, (float)MaxDirectSightRange, maxEngagementRanges.TimeOfFlight, num2, (float)((double)maxEngagementRanges.KinematicRange * 0.0006047522103382444), expectedDeviation, EngagementIssues, (num6 >= 0f) ? (num6 / num2 * 100f) : (-1f));
			}
			UpdateTargetHorizonRing(mapUnitViewModel.Unit.BaseObject, num5);
		}
		else if (v is MapViewModel || v == null)
		{
			Vector3 vector = Source._geoPosition.ToUnity();
			Vector3 firePos = new Vector3(ammo._ap._launchRangesInUnity.y + vector.x, ammo._ap.TypicalTargetAlt, vector.z);
			if (ammo._ap.TypicalTargetAlt > 0f && Source.IsAirUnit)
			{
				firePos.y = vector.y;
			}
			float num7 = Source.GetKinematicRange(null, ammo, firePos, usePreciseKinematics: true, ammo._ap.TypicalTargetVelocity).MaxRange;
			if (ammo._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
			{
				num7 = Source.GetMaxRangeForFCR(ammo, null, num7).Range;
				if (num7 < 0f)
				{
					num7 = _ranges.y;
				}
			}
			MaxRangeInLat = (double)num7 * 0.0006047522103382444;
			NoEscapeRangeInLat = 0.0;
			UpdateReferenceHorizonRing();
		}
		NotifyRangeLabelBindings();
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
