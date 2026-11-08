using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Noesis;
using ObservableComputations;
using SeaPower;
using SeaPower.SelectionManagement;
using Seapower.SceneEditor;
using SeapowerUI.ViewModels;
using UniRx;
using UnityEngine;

namespace SeapowerUI;

public class MapUnitViewModel : IContextMenuObject, IUnitViewModel, INotifyPropertyChanged, IDisposable, IMapSelectable, IMapObject, ISnapPoint, SeaPower.SelectionManagement.ISelectable
{
	private MapVisualType _visualType;

	private MapVisualPosture _visualPosture;

	private IDisposable _objectViewModel;

	private NotifyDerivedPropertyChangedStorage _screenPosXChanged = new NotifyDerivedPropertyChangedStorage(HandlePropertyChanged, "ScreenPosX");

	private NotifyDerivedPropertyChangedStorage _screenPosYChanged = new NotifyDerivedPropertyChangedStorage(HandlePropertyChanged, "ScreenPosY");

	private NotifyDerivedPropertyChangedStorage _screenVisibleChanged = new NotifyDerivedPropertyChangedStorage(HandlePropertyChanged, "ScreenVisible");

	private CompositeDisposable _disposer = new CompositeDisposable();

	private OcConsumer _consumer = new OcConsumer();

	private TrulyObservableCollection<ContextMenuItem> _activeContextMenuItems;

	private ReactiveProperty<ISelectableObject> _selectedUnit;

	private GeoPosition _location;

	private string _name;

	private bool _selected;

	private Point _velocityPoint;

	private NotifyDerivedPropertyChangedStorage _locationChanged = new NotifyDerivedPropertyChangedStorage(HandlePropertyChanged, "Location");

	private NotifyDerivedPropertyChangedStorage _latitudeChanged = new NotifyDerivedPropertyChangedStorage(HandlePropertyChanged, "Longitude");

	private NotifyDerivedPropertyChangedStorage _longitudeChanged = new NotifyDerivedPropertyChangedStorage(HandlePropertyChanged, "Latitude");

	private SolidColorBrush _colorBrush;

	private int _layer;

	private bool _dormant;

	private int _damageState;

	private System.Random random = new System.Random();

	private readonly List<WeaponArcSegmentViewModel> _firingArcSegments = new List<WeaponArcSegmentViewModel>();

	private RallyPointsViewModel rallyPointsViewModel;

	private string _contactInfoLine1;

	private StringBuilder _line1Builder = new StringBuilder();

	private StringBuilder _line3Builder = new StringBuilder();

	private bool _addSpace;

	private string _contactInfoLine2;

	private string _contactInfoLine3;

	private string _contactInfoLine4;

	private string _contactMouseoverInfo = "";

	private MapLayer _infoTypeLayer;

	private MapLayer _infoClassLayer;

	private MapLayer _infoEmissionsLayer;

	private MapLayer _infoRangeLayer;

	public IMapShowable Showable => Unit;

	public IDisposable ObjectViewModel
	{
		get
		{
			return _objectViewModel;
		}
		set
		{
			_objectViewModel = value;
			OnPropertyChanged("ObjectViewModel");
		}
	}

	public MapVisualType VisualType
	{
		get
		{
			return _visualType;
		}
		private set
		{
			MapVisualType visualType = _visualType;
			_visualType = value;
			if (visualType != value)
			{
				OnPropertyChanged("VisualType");
			}
		}
	}

	public MapVisualPosture VisualPosture
	{
		get
		{
			return _visualPosture;
		}
		private set
		{
			MapVisualPosture visualPosture = _visualPosture;
			_visualPosture = value;
			if (visualPosture != value)
			{
				OnPropertyChanged("VisualPosture");
			}
		}
	}

	public ReadOnlyReactiveProperty<BehavioursViewModel> BehaviourViewModel { get; }

	public float ScreenPosX
	{
		get
		{
			if (ContactInfoLine1.Contains("7001"))
			{
				Debug.Log("Contact 7001 Geo Position == " + Location.ToString());
			}
			Vector3 vector = Utils.longLatToLocalV3(Location, Globals._currentCenterTile);
			if (ContactInfoLine1.Contains("7001"))
			{
				Debug.Log($"Contact 7001 World Position == {vector}");
			}
			Vector3 vector2 = Singleton<CameraManager>.Instance.getCurrentCamera().getCamera().WorldToScreenPoint(vector);
			if (ContactInfoLine1.Contains("7001"))
			{
				Debug.Log($"Contact 7001 Screen Position == {vector2}");
			}
			return vector2.x;
		}
	}

	public float ScreenPosY
	{
		get
		{
			Vector3 position = Utils.longLatToLocalV3(Location, Globals._currentCenterTile);
			return Singleton<CameraManager>.Instance.getCurrentCamera().getCamera().WorldToScreenPoint(position)
				.y;
		}
	}

	public bool ScreenVisible
	{
		get
		{
			Vector3 position = Utils.longLatToLocalV3(Location, Globals._currentCenterTile);
			Vector3 vector = Singleton<CameraManager>.Instance.getCurrentCamera().getCamera().WorldToScreenPoint(position);
			if (vector.x <= 0f || vector.x > (float)Screen.width || vector.y <= 0f || vector.y > (float)Screen.height || vector.z <= 0f)
			{
				return false;
			}
			return true;
		}
	}

	public Vehicle Unit { get; }

	public Taskforce OurTaskforce { get; }

	public ErrorEllipseViewModel ErrorEllipse => Unit.ErrorEllipse;

	public BearingFanViewModel BearingFan => Unit.BearingFan;

	public ReadOnlyReactiveProperty<RangeCircleViewModel> RangeCircles { get; }

	public GeoPosition Location
	{
		get
		{
			return _location;
		}
		private set
		{
			_ = _location;
			_location = value;
			_locationChanged.Changed(this);
			_screenPosXChanged.Changed(this);
			_screenPosYChanged.Changed(this);
			_screenVisibleChanged.Changed(this);
			_latitudeChanged.Changed(this);
			_longitudeChanged.Changed(this);
		}
	}

	GeoPosition ISnapPoint.SnapPosition => Location;

	public double Longitude => Location._longitude;

	public double Latitude => Location._latitude;

	public GeoPosition Zero
	{
		get
		{
			GeoPosition result = default(GeoPosition);
			result._latitude = 0.0;
			result._longitude = 0.0;
			return result;
		}
	}

	public SolidColorBrush UnitColorBrush
	{
		get
		{
			return _colorBrush;
		}
		private set
		{
			SolidColorBrush colorBrush = _colorBrush;
			_colorBrush = value;
			if (colorBrush != value)
			{
				OnPropertyChanged("UnitColorBrush");
			}
		}
	}

	public Point VelocityPoint
	{
		get
		{
			return _velocityPoint;
		}
		private set
		{
			if (!value.Equals(_velocityPoint))
			{
				_velocityPoint = value;
				OnPropertyChanged("VelocityPoint");
			}
		}
	}

	bool SeaPower.SelectionManagement.ISelectable.Selected
	{
		get
		{
			return _selected;
		}
		set
		{
			_selected = value;
			OnPropertyChanged("Selected");
		}
	}

	public bool Selected
	{
		get
		{
			return _selected;
		}
		set
		{
			using SelectionGroupManager.SelectionTransaction selectionTransaction = Singleton<SelectionGroupManager>.Instance.BeginTransaction();
			selectionTransaction[this] = value;
		}
	}

	public int Layer
	{
		get
		{
			return _layer;
		}
		set
		{
			_layer = value;
			OnPropertyChanged("Layer");
		}
	}

	public bool Dormant
	{
		get
		{
			return _dormant;
		}
		set
		{
			_dormant = value;
			OnPropertyChanged("Dormant");
		}
	}

	public int DamageState
	{
		get
		{
			return _damageState;
		}
		private set
		{
			if (_damageState != value)
			{
				_damageState = value;
				OnPropertyChanged("DamageState");
			}
		}
	}

	public ObservableCollection<IOverlay> Overlay { get; } = new ObservableCollection<IOverlay>();


	public string ContactInfoLine1
	{
		get
		{
			return _contactInfoLine1;
		}
		set
		{
			string contactInfoLine = _contactInfoLine1;
			_contactInfoLine1 = value;
			if (contactInfoLine != value)
			{
				OnPropertyChanged("ContactInfoLine1");
			}
		}
	}

	public string ContactInfoLine2
	{
		get
		{
			return _contactInfoLine2;
		}
		set
		{
			string contactInfoLine = _contactInfoLine2;
			_contactInfoLine2 = value;
			if (contactInfoLine != value)
			{
				OnPropertyChanged("ContactInfoLine2");
			}
		}
	}

	public string ContactInfoLine3
	{
		get
		{
			return _contactInfoLine3;
		}
		set
		{
			string contactInfoLine = _contactInfoLine3;
			_contactInfoLine3 = value;
			if (contactInfoLine != value)
			{
				OnPropertyChanged("ContactInfoLine3");
			}
		}
	}

	public string ContactInfoLine4
	{
		get
		{
			return _contactInfoLine4;
		}
		set
		{
			string contactInfoLine = _contactInfoLine4;
			_contactInfoLine4 = value;
			if (contactInfoLine != value)
			{
				OnPropertyChanged("ContactInfoLine4");
			}
		}
	}

	public string ContactMouseoverInfo
	{
		get
		{
			return _contactMouseoverInfo;
		}
		set
		{
			string contactMouseoverInfo = _contactMouseoverInfo;
			_contactMouseoverInfo = value;
			if (contactMouseoverInfo != value)
			{
				OnPropertyChanged("ContactMouseoverInfo");
				if (string.IsNullOrEmpty(contactMouseoverInfo) != string.IsNullOrEmpty(value))
				{
					OnPropertyChanged("HasContactMouseoverInfo");
				}
			}
		}
	}

	public bool HasContactMouseoverInfo => !string.IsNullOrEmpty(_contactMouseoverInfo);

	public event PropertyChangedEventHandler PropertyChanged;

	private static void HandlePropertyChanged(object sender, PropertyChangedEventArgs args)
	{
		(sender as MapUnitViewModel).PropertyChanged?.Invoke(sender, args);
	}

	public MapUnitViewModel(Taskforce ourTaskforce, Vehicle unit, ReactiveProperty<ISelectableObject> selectedUnit, bool everyUpdate = false)
	{
		MapUnitViewModel mapUnitViewModel = this;
		Layer = 3;
		OurTaskforce = ourTaskforce;
		Unit = unit;
		_selectedUnit = selectedUnit;
		bool initialized = false;
		VisualType = unit.GetMapType();
		if (unit.BaseObject is Bomb bomb && bomb._ap._subType == Ammunition.Type.Sonobuoy)
		{
			Layer = 23;
		}
		Unit.ViewModel = this;
		_disposer.Add(unit.Position.Subscribe(delegate(GeoPosition v)
		{
			if (!everyUpdate && initialized)
			{
				double num = mapUnitViewModel.random.NextDouble();
				float num2 = 1f / 15f;
				if (num > (double)num2)
				{
					return;
				}
			}
			mapUnitViewModel.Location = v;
			Vector3 unityVelocityVector = unit.UnityVelocityVector;
			Vector2 vector = new Vector2(unityVelocityVector.x, 0f - unityVelocityVector.z).normalized * 12f;
			mapUnitViewModel.VelocityPoint = new Point((float)(11.25 + (double)vector.x), (float)(11.25 + (double)vector.y));
			initialized = true;
		}));
		_disposer.Add(unit.IsDormant.Subscribe(delegate(bool v)
		{
			mapUnitViewModel.Dormant = v;
		}));
		BehaviourViewModel = this.OnPropertyChanges((MapUnitViewModel muvm) => muvm.Selected).CombineLatest(Globals.AllowControlOfEnemyUnits, Globals.ShowAllWaypoints, (bool selected, bool controlEnemyUnits, bool showAllWaypoints) => (unit.BaseObject._userRoot == null || !(unit.BaseObject._taskforce.Side == Taskforce.TfType.Player || controlEnemyUnits) || !(selected || showAllWaypoints)) ? null : new BehavioursViewModel(unit.BaseObject._userRoot)).DoOnError(delegate(Exception e)
		{
			throw e;
		})
			.ToReadOnlyReactiveProperty();
		_disposer.Add(BehaviourViewModel);
		ObjectViewModel = unit.BaseObject.CreateContactViewModel(unit, this);
		if (ObjectViewModel != null)
		{
			_disposer.Add(ObjectViewModel);
		}
		RangeCircles = this.OnPropertyChanges((MapUnitViewModel muvm) => muvm.Selected).CombineLatest(GizmoLayerManager.Instance.LayersByName["CONTACT_RANGE_CIRCLES"].OnPropertyChanges((MapLayer l) => l.Visible), (bool selected, bool visible) => selected && visible).Select((bool sel, int sc) => (!sel) ? null : new RangeCircleViewModel(unit))
			.DoOnError(delegate(Exception e)
			{
				throw e;
			})
			.ToReadOnlyReactiveProperty();
		_disposer.Add(Unit.OnPropertyChanges((Vehicle u) => u.ErrorEllipse).Subscribe(delegate
		{
			mapUnitViewModel.OnPropertyChanged("ErrorEllipse");
		}, delegate(Exception e)
		{
			throw e;
		}, delegate
		{
		}));
		_disposer.Add(Unit.OnPropertyChanges((Vehicle u) => u.BearingFan).Subscribe(delegate
		{
			mapUnitViewModel.OnPropertyChanged("BearingFan");
		}, delegate(Exception e)
		{
			throw e;
		}, delegate
		{
		}));
		_disposer.Add(RangeCircles);
		Overlay.Add(new RadarIlluminationWarning(this));
		Overlay.Add(new JammingWarning(this));
		if (Unit.BaseObject._obp?._flightDeck != null)
		{
			rallyPointsViewModel = new RallyPointsViewModel(this, Unit.BaseObject, Unit.BaseObject._obp?._flightDeck);
		}
		new MapCircleViewModel().BaseObject = Unit.BaseObject;
		_disposer.Add(this.OnPropertyChanges((MapUnitViewModel muvm) => muvm.Selected).CombineLatest(Globals.AllowControlOfEnemyUnits, Unit.BaseObject.UnitTaskforce, (bool selected, bool controlEnemyUnits, Taskforce taskforce) => false).Subscribe());
		SensorSystemECM sensorSystemECM = null;
		if (Unit.BaseObject._obp != null)
		{
			sensorSystemECM = Unit.BaseObject._obp._ecmSystems.FirstOrDefault((SensorSystemECM x) => x._ecm._ep._type == ECMParameters.ECMType.Offensive || x._ecm._ep._type == ECMParameters.ECMType.Universal);
		}
		else if (Unit.BaseObject is WeaponBase weaponBase)
		{
			foreach (SensorSystem sensor in weaponBase._sensors)
			{
				if (sensor is SensorSystemECM sensorSystemECM2 && (sensorSystemECM2._ecm._ep._type == ECMParameters.ECMType.Offensive || sensorSystemECM2._ecm._ep._type == ECMParameters.ECMType.Universal))
				{
					sensorSystemECM = sensorSystemECM2;
					break;
				}
			}
		}
		if (sensorSystemECM != null)
		{
			JamConeViewModel jamConeViewModel = null;
			jamConeViewModel = new JamConeViewModel(sensorSystemECM, Unit.BaseObject.Position);
			_disposer.Add(this.OnPropertyChanges((MapUnitViewModel muvm) => muvm.Selected).CombineLatest(Globals.AllowControlOfEnemyUnits, Unit.BaseObject.UnitTaskforce, sensorSystemECM.Jams, delegate(bool selected, bool controlEnemyUnits, Taskforce taskforce, bool jams)
			{
				if (jams)
				{
					if (mapUnitViewModel.Selected)
					{
						if (taskforce == Globals._playerTaskforce || controlEnemyUnits)
						{
							if (!mapUnitViewModel.Overlay.Contains(jamConeViewModel))
							{
								mapUnitViewModel.Overlay.Add(jamConeViewModel);
							}
						}
						else
						{
							mapUnitViewModel.Overlay.Remove(jamConeViewModel);
						}
					}
					else
					{
						mapUnitViewModel.Overlay.Remove(jamConeViewModel);
					}
				}
				else
				{
					mapUnitViewModel.Overlay.Remove(jamConeViewModel);
				}
				return false;
			}).Subscribe());
		}
		RangeOnMapViewModel rangeOnMapViewModel = new RangeOnMapViewModel
		{
			UnitObjBase = Unit.BaseObject
		};
		_disposer.Add(this.OnPropertyChanges((MapUnitViewModel muvm) => muvm.Selected).CombineLatest(Globals.AllowControlOfEnemyUnits, Unit.BaseObject.UnitTaskforce, delegate(bool selected, bool controlEnemyUnits, Taskforce taskforce)
		{
			if (selected && (taskforce == Globals._playerTaskforce || controlEnemyUnits))
			{
				if (!mapUnitViewModel.Overlay.Contains(rangeOnMapViewModel))
				{
					mapUnitViewModel.Overlay.Add(rangeOnMapViewModel);
				}
				if (!mapUnitViewModel.Overlay.Contains(mapUnitViewModel.rallyPointsViewModel) && mapUnitViewModel.rallyPointsViewModel != null)
				{
					mapUnitViewModel.Overlay.Add(mapUnitViewModel.rallyPointsViewModel);
				}
			}
			else
			{
				mapUnitViewModel.Overlay.Remove(rangeOnMapViewModel);
				mapUnitViewModel.Overlay.Remove(mapUnitViewModel.rallyPointsViewModel);
			}
			return false;
		}).Subscribe());
		_disposer.Add(WeaponArcHoverManager.Hovered.Subscribe(RebuildFiringArcs, delegate(Exception e)
		{
			throw e;
		}, delegate
		{
		}));
	}

	private void RebuildFiringArcs(WeaponArcHoverManager.HoverInfo info)
	{
		foreach (WeaponArcSegmentViewModel firingArcSegment in _firingArcSegments)
		{
			Overlay.Remove(firingArcSegment);
			firingArcSegment.Dispose();
		}
		_firingArcSegments.Clear();
		if (info == null || info.Unit != Unit.BaseObject || info.MapRangeInUnity <= 0f)
		{
			return;
		}
		foreach (WeaponSystem system in info.Systems)
		{
			if (system?._vwp == null)
			{
				continue;
			}
			foreach (Vector2 arcSpan in WeaponArcHoverManager.GetArcSpans(system._vwp._firingArcs))
			{
				WeaponArcSegmentViewModel item = new WeaponArcSegmentViewModel(Unit.BaseObject, arcSpan.x, arcSpan.y, info.MapRangeInUnity);
				_firingArcSegments.Add(item);
				Overlay.Add(item);
			}
		}
	}

	public void Update()
	{
		if (_infoTypeLayer == null)
		{
			_infoTypeLayer = GizmoLayerManager.Instance.LayersByName["CONTACT_INFO_TYPE"];
		}
		if (_infoClassLayer == null)
		{
			_infoClassLayer = GizmoLayerManager.Instance.LayersByName["CONTACT_INFO_CLASS"];
		}
		if (_infoRangeLayer == null)
		{
			_infoRangeLayer = GizmoLayerManager.Instance.LayersByName["CONTACT_INFO_RANGE"];
		}
		if (_infoEmissionsLayer == null)
		{
			_infoEmissionsLayer = GizmoLayerManager.Instance.LayersByName["CONTACT_INFO_EMISSIONS"];
		}
		if (Unit.BaseObject == null || (Unit.BaseObject is WeaponBase weaponBase && !weaponBase.isLaunched()))
		{
			return;
		}
		if (Unit.BaseObject.isUnit() && Unit.UnitTaskforce.Value == Globals._playerTaskforce)
		{
			if (Unit.BaseObject.IsFormationWingman)
			{
				Layer = 5;
			}
			else
			{
				Layer = 3;
			}
		}
		(MapVisualPosture, SolidColorBrush) tuple = UpdateContactDisplay(Unit);
		MapVisualPosture mapVisualPosture = (VisualPosture = tuple.Item1);
		SolidColorBrush solidColorBrush = (UnitColorBrush = tuple.Item2);
		DamageState = ((Unit.BaseObject.IsAirUnit && !Unit.BaseObject.IsDestroyed) ? Unit.BaseObject.GunDamageTier() : 0);
		UpdateContactInfoLine3(_selectedUnit);
		BehaviourViewModel.Value?.Update();
		RangeCircles.Value?.UpdateRadius();
		if (GameTime.IsPaused())
		{
			return;
		}
		foreach (IOverlay item3 in Overlay)
		{
			item3.Update();
		}
	}

	private void UpdateContactInfoLine3(ReactiveProperty<ISelectableObject> RP_selectedUnit)
	{
		_line3Builder.Clear();
		_line3Builder.EnsureCapacity(100);
		string value = Unit.IncomingEmissionsInfo.Value;
		if (_infoRangeLayer.Visible)
		{
			ISelectableObject value2 = RP_selectedUnit.Value;
			if (value2 != null && value2 != Unit && ((IUnit)value2).BaseObject != null && Unit.BaseObject != null)
			{
				float num;
				if (Globals._testModeActive)
				{
					Vector3 position = Unit.BaseObject.transform.position;
					num = (((IUnit)value2).BaseObject.transform.position - position).magnitude * 0.036285132f;
				}
				else
				{
					num = (float)((IUnit)value2).Position.Value.GetDistance(Location) * 0.036285132f;
				}
				_line3Builder.AppendFormat("{0:F2}", num);
				_line3Builder.Append(" ");
				_line3Builder.Append(Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "NauticalMiles"));
				_line3Builder.Append(" ");
			}
		}
		if (!Unit.BaseObject.IsDestroyed && _infoEmissionsLayer.Visible && !Unit.BaseObject.IsPlayerObject)
		{
			_line3Builder.Append("(");
			_line3Builder.Append(value);
			_line3Builder.Append(")");
		}
		if (Unit.BaseObject.transform.position.y > 0.001f && (Unit.BaseObject.IsAirUnit || Unit.BaseObject.isWeapon()))
		{
			_line3Builder.Append("\n");
			_line3Builder.Append(Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Altitude"));
			_line3Builder.Append(" ");
			_line3Builder.AppendFormat("{0:F0}", (float)Location._height * 220.47266f);
			_line3Builder.Append(" ");
			_line3Builder.Append(Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "Feet"));
		}
		ContactInfoLine3 = _line3Builder.ToString();
	}

	private (MapVisualPosture, SolidColorBrush) UpdateContactDisplay(IUnit unit)
	{
		var (item, item2) = MapContactVisuals.Resolve(unit);
		_line1Builder.Clear();
		_line1Builder.EnsureCapacity(100);
		if (_infoTypeLayer.Visible)
		{
			ObjectBase selectedObject = Singleton<RenderPosition>.Instance.getSelectedObject();
			int num = unit.BaseObject._quickAccessNumber;
			if (num % 10 == 0)
			{
				num += 10;
			}
			if (unit.BaseObject._quickAccessNumber > -1)
			{
				_line1Builder.Append("[").Append(num).Append("] ");
			}
			if (unit.BaseObject is WeaponBase weaponBase)
			{
				_addSpace = false;
				if (weaponBase._targetLost || (Singleton<OptionsManager>.Instance.WeaponFailuresEnabled && (weaponBase._seekerMalfunction.Value || weaponBase._launchMalfunction.Value)))
				{
					_addSpace = true;
					_line1Builder.Append("X");
				}
				if (weaponBase.CurrentTarget is ChaffCloud)
				{
					_addSpace = true;
					_line1Builder.Append("▲");
				}
				if (weaponBase._deviationMagnitudeWithJam > 0f)
				{
					_addSpace = true;
					_line1Builder.Append("▼");
				}
				if (_addSpace)
				{
					_line1Builder.Append(" ");
				}
			}
			if (unit.UnitTaskforce.Value != null && unit.Identified.Value)
			{
				if (Globals._playerTaskforce._spottedObjects.Contains(unit.BaseObject))
				{
					_line1Builder.Append("◘ ");
				}
				else if (selectedObject != null && selectedObject.AcceptsOrdersFromPlayer && selectedObject._spottedObjects.Contains(unit.BaseObject))
				{
					_line1Builder.Append("ƒ ");
				}
				if (unit.BaseObject.IsPlayerObject && unit.BaseObject.Formation != null)
				{
					if (unit.BaseObject.IsFormationLeader)
					{
						_line1Builder.Append("♠");
					}
					else
					{
						_line1Builder.Append("^");
					}
				}
				_line1Builder.Append(unit.Id);
				_line1Builder.Append(" (");
				string value = unit.BaseObject.TypeAbbreviation;
				if (unit is Vehicle vehicle && unit.BaseObject._obp != null && Singleton<UnitMetadataProvider>.Instance.UnitData.TryGetValue((unit.BaseObject._obp._objectIniFolder, vehicle.Class.Value.Value), out var value2) && value2.Item2._variants.ContainsKey("Default"))
				{
					value = Singleton<UnitMetadataProvider>.Instance.UnitData[(unit.BaseObject._obp._objectIniFolder, vehicle.Class.Value.Value)].Item2._variants["Default"].TypeAbbr;
				}
				_line1Builder.Append(value);
				_line1Builder.Append(")");
			}
			else
			{
				_line1Builder.Append(unit.Id);
			}
			if (selectedObject != null && selectedObject.AcceptsOrdersFromPlayer && selectedObject._ai != null)
			{
				if (selectedObject._ai._processedObjects.Contains(unit.BaseObject))
				{
					_line1Builder.Append("  (✓)");
				}
				else
				{
					foreach (var item3 in selectedObject._ai._objectsBeingProcessed)
					{
						if (!(item3.obj != unit.BaseObject))
						{
							if (selectedObject._ai.IsOrientQueued(item3.obj))
							{
								_line1Builder.Append("  (wait)");
							}
							else
							{
								_line1Builder.Append($"  ({(double)selectedObject._ai.GetOrientCost(item3.obj) - GameTime.missionElapsedTime + item3.startTime:0.0})");
							}
							break;
						}
					}
				}
			}
		}
		else
		{
			_line1Builder.Append(unit.Id);
		}
		ContactInfoLine1 = _line1Builder.ToString();
		if (!_infoClassLayer.Visible)
		{
			ContactInfoLine2 = "";
		}
		else if (unit.Identified.Value)
		{
			if (unit.BaseObject.IsPlayerObject)
			{
				ContactInfoLine2 = unit.BaseObject.DisplayNameShort;
			}
			else
			{
				string contactInfoLine = unit.BaseObject.ClassNameShort;
				if (unit is Vehicle vehicle2 && unit.BaseObject._obp != null && Singleton<UnitMetadataProvider>.Instance.UnitData.TryGetValue((unit.BaseObject._obp._objectIniFolder, vehicle2.Class.Value.Value), out var value3) && value3.Item2._variants.ContainsKey("Default"))
				{
					contactInfoLine = Singleton<UnitMetadataProvider>.Instance.UnitData[(unit.BaseObject._obp._objectIniFolder, vehicle2.Class.Value.Value)].Item2._variants["Default"].ClassName;
				}
				ContactInfoLine2 = contactInfoLine;
			}
		}
		else
		{
			ContactInfoLine2 = Unit.IncomingSignalInfo();
		}
		if (!unit.BaseObject.IsPlayerObject && !unit.BaseObject.IsDestroyed && unit.BaseObject._ECMSystemsTryingToJamMe.Count > 0)
		{
			ContactInfoLine4 = ((Singleton<OptionsManager>.Instance.ShowJammedSensorRanges && unit.Identified.Value) ? $"☼ {GetJammedRadarRangeNm(unit.BaseObject):F1}nm" : "☼");
		}
		else
		{
			ContactInfoLine4 = "";
		}
		return (item, item2);
	}

	public void ClearContactMouseoverInfo()
	{
		ContactMouseoverInfo = "";
	}

	public void UpdateContactMouseoverInfo(float hitChance, float kinematicPercent, float sensorPercent, float sightPercent, float timeOfFlight, float rangeToTarget, float kinematicRangeLat = float.MaxValue, float expectedDeviation = 0f, List<ObjectBase.EngagementIssue> engagementIssues = null, float noEscapePercent = -1f)
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (hitChance < 100f)
		{
			if (Singleton<OptionsManager>.Instance.HitChanceDisplay == HitChanceDisplayMode.Rough)
			{
				stringBuilder.Append(GetMissileEffectiveness(hitChance));
			}
			else if (Singleton<OptionsManager>.Instance.HitChanceDisplay == HitChanceDisplayMode.Full)
			{
				stringBuilder.Append(string.Format("{0} <Run Foreground=\"{1}\">{2:F0}%</Run><LineBreak/>", Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "HitChance"), NumberToColorConverter(hitChance, 0f, 80f), hitChance));
			}
		}
		if (expectedDeviation > 0f)
		{
			stringBuilder.Append(string.Format("{0} {1:F0}Ft<LineBreak/>", Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "BombDeviation"), expectedDeviation));
		}
		if (Unit.ErrorEllipse == null || Unit.ErrorEllipse.SemiMajorAxis < kinematicRangeLat * 0.1f)
		{
			if (timeOfFlight > 0f)
			{
				stringBuilder.Append(string.Format("{0} {1:F0}S<LineBreak/>", Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "TimeOfFlight"), timeOfFlight));
			}
			if (kinematicPercent < 100f)
			{
				stringBuilder.Append(Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "KinematicRange") + " <Run Foreground=\"" + NumberToColorConverter(kinematicPercent, 0f, 200f) + "\">" + FormatRangeDisplay(kinematicPercent, rangeToTarget, usePercent: false) + "</Run><LineBreak/>");
			}
			if (noEscapePercent >= 0f)
			{
				stringBuilder.Append(Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "NoEscapeRange") + " <Run Foreground=\"" + NumberToColorConverter(noEscapePercent, 0f, 200f) + "\">" + FormatRangeDisplay(noEscapePercent, rangeToTarget, usePercent: false) + "</Run><LineBreak/>");
			}
			if (sensorPercent < 100f)
			{
				stringBuilder.Append(Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "SensorRange") + " <Run Foreground=\"" + NumberToColorConverter(sensorPercent, 0f, 200f) + "\">" + FormatRangeDisplay(sensorPercent, rangeToTarget, usePercent: false) + "</Run><LineBreak/>");
			}
			if (sightPercent < 100f)
			{
				stringBuilder.Append(Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "SightRange") + " <Run Foreground=\"" + NumberToColorConverter(sightPercent, 0f, 200f) + "\">" + FormatRangeDisplay(sightPercent, rangeToTarget, usePercent: false) + "</Run><LineBreak/>");
			}
		}
		if (engagementIssues != null && engagementIssues.Count > 0)
		{
			foreach (ObjectBase.EngagementIssue engagementIssue in engagementIssues)
			{
				string text = engagementIssue switch
				{
					ObjectBase.EngagementIssue.ObscuredByAltitude => "Issue_ObscuredByAltitude", 
					ObjectBase.EngagementIssue.InLookdown => "Issue_InLookdown", 
					ObjectBase.EngagementIssue.NotEmitting => "Issue_NotEmitting", 
					ObjectBase.EngagementIssue.MissingEmittedBands => "Issue_MissingEmittedBands", 
					ObjectBase.EngagementIssue.Jammed => "Issue_Jammed", 
					ObjectBase.EngagementIssue.BackupTargeting => "Issue_BackupTargeting", 
					ObjectBase.EngagementIssue.OutOfAltitude => "Issue_OutOfAltitude", 
					ObjectBase.EngagementIssue.TargetTooFast => "Issue_TargetTooFast", 
					ObjectBase.EngagementIssue.TargetTooDeep => "Issue_TargetTooDeep", 
					ObjectBase.EngagementIssue.CantSeeTarget => "Issue_CantSeeTarget", 
					ObjectBase.EngagementIssue.DefensiveJamming => "Issue_DefensiveJamming", 
					ObjectBase.EngagementIssue.HomeOnJam => "Issue_HomeOnJam", 
					_ => null, 
				};
				if (text != null)
				{
					string text2 = ((engagementIssue == ObjectBase.EngagementIssue.HomeOnJam) ? "Green" : "Yellow");
					stringBuilder.Append("<Run Foreground=\"" + text2 + "\">" + Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", text) + "</Run><LineBreak/>");
				}
			}
		}
		string text3 = stringBuilder.ToString();
		if (text3.EndsWith("<LineBreak/>"))
		{
			string text4 = text3;
			int length = "<LineBreak/>".Length;
			text3 = text4.Substring(0, text4.Length - length);
		}
		ContactMouseoverInfo = text3;
	}

	private string FormatRangeDisplay(float percent, float total, bool usePercent)
	{
		if (usePercent)
		{
			return $"{percent:F0}%";
		}
		float num = total * 0.036285132f;
		float num2 = num * percent / 100f;
		if (num >= 10f)
		{
			return $"{num2:F0}/{num:F0}Nmi";
		}
		return $"{num2:F1}/{num:F1}Nmi";
	}

	private string GetMissileEffectiveness(float hitChance)
	{
		string text = "";
		string text2 = "";
		if (hitChance < 37f)
		{
			text2 = Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "Bad");
			text = "Red";
		}
		else if (hitChance < 75f)
		{
			text2 = Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "Moderate");
			text = "Yellow";
		}
		else
		{
			text2 = Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "Good");
			text = "Green";
		}
		return Singleton<LanguageResourceHandler>.Instance.getText("Tooltip", "Effectiveness") + " <Run Foreground=\"" + text + "\">" + text2 + "</Run><LineBreak/>";
	}

	private string NumberToColorConverter(float num, float min, float max)
	{
		num = (num - min) * (100f / max);
		if (num > 40f)
		{
			if (!(num > 80f))
			{
				if (num > 60f)
				{
					return "LightGreen";
				}
				return "Yellow";
			}
			return "Green";
		}
		if (num > 20f)
		{
			return "DarkOrange";
		}
		return "Red";
	}

	private float GetJammedRadarRangeNm(ObjectBase obj)
	{
		if (obj._obp == null)
		{
			return 0f;
		}
		SensorSystemRadar sensorSystemRadar = null;
		if (obj._obp._airSearchRadars.Count > 0)
		{
			foreach (SensorSystemRadar airSearchRadar in obj._obp._airSearchRadars)
			{
				if (sensorSystemRadar == null || airSearchRadar._radar._rp._maxRange > sensorSystemRadar._radar._rp._maxRange)
				{
					sensorSystemRadar = airSearchRadar;
				}
			}
		}
		else
		{
			foreach (SensorSystemRadar searchRadarSystem in obj._obp._searchRadarSystems)
			{
				if (sensorSystemRadar == null || searchRadarSystem._radar._rp._maxRange > sensorSystemRadar._radar._rp._maxRange)
				{
					sensorSystemRadar = searchRadarSystem;
				}
			}
		}
		if (sensorSystemRadar == null)
		{
			return 0f;
		}
		float rcs = 1f;
		ObjectBase objectBase = ((_selectedUnit?.Value is IUnit unit) ? unit.BaseObject : null);
		if (objectBase != null && objectBase._sensorData != null)
		{
			RadarParameters.Role role = sensorSystemRadar._radar._rp._role;
			bool num;
			if (!objectBase.isAirTarget())
			{
				if (role == RadarParameters.Role.Surface || role == RadarParameters.Role.SurfaceAndAir)
				{
					goto IL_0160;
				}
				num = role == RadarParameters.Role.Undefined;
			}
			else
			{
				num = role != RadarParameters.Role.Surface;
			}
			if (num)
			{
				goto IL_0160;
			}
		}
		goto IL_016c;
		IL_016c:
		RadarParameters rp = sensorSystemRadar._radar._rp;
		float b = RadarCalculator.GetFCRDetectionRange((sensorSystemRadar._mountObject != null) ? sensorSystemRadar._mountObject.transform.position : obj.transform.position, attackingJammers: new List<SensorSystemECM>(obj._ECMSystemsTryingToJamMe), rcs: rcs, waveLength: rp._waveLength, gainFactor: rp._gainFactor, peakPower: rp._peakPower, frequency: rp._frequency, crew: obj._crew, jamResistance: rp._jamResistance);
		return Mathf.Min(rp._maxRange, b) * 0.036285132f;
		IL_0160:
		rcs = objectBase._sensorData._maxRCS;
		goto IL_016c;
	}

	public MapVisualType GetMapType(ObjectBase obj)
	{
		MapVisualType result;
		switch (obj._type)
		{
		case ObjectBase.ObjectType.Vessel:
			result = MapVisualType.Surface;
			break;
		case ObjectBase.ObjectType.LandUnit:
		{
			LandUnitParameters landUnitParams = (obj as LandUnit).LandUnitParams;
			result = ((landUnitParams._landUnitSubType != 0) ? ((landUnitParams._landUnitSubType != LandUnitParameters.LandUnitSubType.SAM) ? ((landUnitParams._landUnitSubType != LandUnitParameters.LandUnitSubType.Installation) ? ((landUnitParams._landUnitSubType != LandUnitParameters.LandUnitSubType.Bridge) ? ((landUnitParams._landUnitSubType != LandUnitParameters.LandUnitSubType.MobileUnit) ? ((landUnitParams._landUnitSubType != LandUnitParameters.LandUnitSubType.AAA) ? ((landUnitParams._landUnitSubType != LandUnitParameters.LandUnitSubType.Radar) ? ((landUnitParams._landUnitSubType != LandUnitParameters.LandUnitSubType.Port) ? ((landUnitParams._landUnitSubType != LandUnitParameters.LandUnitSubType.OilRig) ? MapVisualType.Installation : MapVisualType.Installation) : MapVisualType.Port) : MapVisualType.Radar) : ((obj as LandUnit)._isMobile ? MapVisualType.MobileAAA : MapVisualType.AAA)) : MapVisualType.MobileUnit) : MapVisualType.Bridge) : MapVisualType.Installation) : MapVisualType.SAM) : MapVisualType.Airbase);
			break;
		}
		case ObjectBase.ObjectType.Submarine:
			result = MapVisualType.Submarine;
			break;
		case ObjectBase.ObjectType.Helicopter:
			result = MapVisualType.Helicopter;
			break;
		case ObjectBase.ObjectType.Aircraft:
			result = MapVisualType.Air;
			break;
		case ObjectBase.ObjectType.Missile:
		case ObjectBase.ObjectType.ASROC:
			result = MapVisualType.Missile;
			break;
		case ObjectBase.ObjectType.Torpedo:
			result = MapVisualType.Torpedo;
			break;
		case ObjectBase.ObjectType.Bomb:
		{
			Bomb bomb = obj as Bomb;
			if (bomb._ap._subType == Ammunition.Type.Sonobuoy)
			{
				result = ((!bomb._ap._isActiveSonobuoy) ? MapVisualType.PassiveSonobouy : MapVisualType.ActiveSonobouy);
				Layer = 23;
			}
			else
			{
				result = ((bomb._seeker._guidanceType != 0) ? MapVisualType.Missile : MapVisualType.Invisible);
			}
			break;
		}
		case ObjectBase.ObjectType.Decoy:
			result = ((OurTaskforce.RelationshipTo(obj._taskforce) != RelationsState.Friendly) ? MapVisualType.Submarine : MapVisualType.Invisible);
			break;
		case ObjectBase.ObjectType.Noisemaker:
			result = ((OurTaskforce.RelationshipTo(obj._taskforce) != RelationsState.Friendly) ? MapVisualType.Submarine : MapVisualType.Invisible);
			break;
		case ObjectBase.ObjectType.Other:
		case ObjectBase.ObjectType.RBU:
		case ObjectBase.ObjectType.AerialRocket:
		case ObjectBase.ObjectType.Chaff:
		case ObjectBase.ObjectType.Projectile:
		case ObjectBase.ObjectType.Wakebubble:
			result = MapVisualType.Invisible;
			break;
		default:
			throw new ArgumentOutOfRangeException();
		}
		if (obj.IsFormationWingman && GizmoLayerManager.Instance.LayersByName["CONTACT_RANGE_CIRCLES"].Visible)
		{
			result = MapVisualType.Invisible;
		}
		return result;
	}

	protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public void Dispose()
	{
		Noesis.ContextMenu contextMenu = MainGameView.Instance?.ContextMenuCanvas?.ContextMenu;
		if (_activeContextMenuItems != null && contextMenu?.ItemsSource == _activeContextMenuItems)
		{
			contextMenu.IsOpen = false;
			contextMenu.ItemsSource = null;
		}
		_activeContextMenuItems = null;
		_disposer.Dispose();
		rallyPointsViewModel?.Dispose();
		Singleton<SelectionGroupManager>.Instance.RemoveSelectable(this);
	}

	public TrulyObservableCollection<ContextMenuItem> GetContextMenuItems()
	{
		TrulyObservableCollection<ContextMenuItem> trulyObservableCollection = (_activeContextMenuItems = new TrulyObservableCollection<ContextMenuItem>());
		if (Unit.Identified.Value)
		{
			trulyObservableCollection.Add(new ContextMenuItem("ContextMenu", "Information", null, new DelegateCommand(delegate
			{
				ObjectBase objectBase2 = Unit?.BaseObject;
				if (!(objectBase2 == null))
				{
					WindowHostCommands.OpenWindowCommand.Execute(new ObjectInformationViewModel(MainGameView.Instance, objectBase2.getObjectIniName(), ObjectInformationViewModel.GetReferenceVariantIndex(objectBase2), showTree: true, ObjectInformationViewModel.GetReferenceLoadoutVariant(objectBase2)), MainGameView.Instance);
				}
			})));
		}
		trulyObservableCollection.Add(new ContextMenuItem("OrderLog", "Friendly", null, new DelegateCommand(delegate
		{
			Unit.OverrideRelationship(RelationsState.Friendly);
		})));
		trulyObservableCollection.Add(new ContextMenuItem("OrderLog", "Neutral", null, new DelegateCommand(delegate
		{
			Unit.OverrideRelationship(RelationsState.Neutral);
		})));
		trulyObservableCollection.Add(new ContextMenuItem("OrderLog", "Hostile", null, new DelegateCommand(delegate
		{
			Unit.OverrideRelationship(RelationsState.Hostile);
		})));
		trulyObservableCollection.Add(new ContextMenuItem("Contact", "Information", null, new DelegateCommand(delegate
		{
			WindowHostCommands.OpenWindowCommand.Execute(new VehicleInfoViewModel(Unit), MainGameView.Instance);
		})));
		if (Unit != null && !Unit.BaseObject.isSubmerged())
		{
			trulyObservableCollection.Add(new ContextMenuItem("Contact", "RequestIdentify", null, new DelegateCommand(delegate
			{
				if (Unit != null)
				{
					ObjectBase baseObject;
					ObjectBase objectBase = CommunicationResult(Unit, "IdentificationRequest", out baseObject);
					if (!(baseObject == null) && objectBase?._ai != null && objectBase._ai.CurrentCompliance == AI.Compliance.Follow)
					{
						Globals._mainGameViewModel.RaiseAlert(baseObject, FormatCommText(Unit, Singleton<LanguageResourceHandler>.Instance.getText("Contact", "FollowRequest")));
						Utils.RevealContactToObject(baseObject, objectBase, "Comms", 3);
					}
				}
			}), new ReactiveProperty<bool>(Singleton<RenderPosition>.Instance.SelectedObject != null && Singleton<RenderPosition>.Instance.SelectedObject.IsPlayerObject).ToReadOnlyReactiveProperty()));
		}
		return trulyObservableCollection;
	}

	public ObjectBase CommunicationResult(Vehicle unit, string requestText, out ObjectBase baseObject)
	{
		baseObject = Singleton<RenderPosition>.Instance.getSelectedObject();
		if (baseObject == null)
		{
			if (Globals._mainGameViewModel.Map.SelectedObject.Value is ObjectBase)
			{
				baseObject = Globals._mainGameViewModel.Map.SelectedObject.Value as ObjectBase;
			}
			else
			{
				if (!(Globals._mainGameViewModel.Map.SelectedObject.Value is Vehicle vehicle))
				{
					return null;
				}
				baseObject = vehicle.BaseObject;
			}
		}
		if (!baseObject.isUnit())
		{
			return null;
		}
		if (baseObject._taskforce != Globals._playerTaskforce)
		{
			return null;
		}
		ObjectBase baseObject2 = unit.BaseObject;
		if (baseObject2 == null)
		{
			return null;
		}
		if (baseObject2.isSubmerged())
		{
			return null;
		}
		if (baseObject.isSubmerged())
		{
			return null;
		}
		if (!TerrainUtils.IsLineOfSightBetween(baseObject, baseObject2, baseObject.VisibleAltitude)._los)
		{
			Globals._mainGameViewModel.RaiseAlert(baseObject, FormatCommText(unit, Singleton<LanguageResourceHandler>.Instance.getText("Contact", "NoLOS")));
			return null;
		}
		float num = 1.2f * RadarCalculator.getRadarHorizon(RadarCalculator.GetCommPosition(baseObject), RadarCalculator.GetCommPosition(baseObject2), visual: false, Utils.TerrainAltForHorizon(baseObject, baseObject2));
		if ((baseObject.transform.position - baseObject2.transform.position).magnitude > num)
		{
			Globals._mainGameViewModel.RaiseAlert(baseObject, FormatCommText(unit, Singleton<LanguageResourceHandler>.Instance.getText("Contact", "NoLOS")));
			return null;
		}
		if (baseObject2.isUnit())
		{
			Globals._mainGameViewModel.RaiseAlert(baseObject, FormatCommText(unit, Singleton<LanguageResourceHandler>.Instance.getText("Contact", requestText)));
			Utils.RevealContactToObject(baseObject2, baseObject, "Comms", 3);
		}
		if (baseObject2 is Biologic || baseObject2._ai == null || baseObject2._ai.CurrentCompliance == AI.Compliance.Ignore)
		{
			Globals._mainGameViewModel.RaiseAlert(baseObject, FormatCommText(unit, Singleton<LanguageResourceHandler>.Instance.getText("Contact", "IgnoreComm")));
			return null;
		}
		if (baseObject2._ai.CurrentCompliance == AI.Compliance.Refuse)
		{
			Globals._mainGameViewModel.RaiseAlert(baseObject, FormatCommText(unit, Singleton<LanguageResourceHandler>.Instance.getText("Contact", "RefuseRequest")));
			return null;
		}
		return baseObject2;
	}

	public static string FormatCommText(Vehicle unit, string text)
	{
		if (unit != null)
		{
			text = text.Replace("${TrackInfo}", Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "TrackInfo") + " ");
			text = text.Replace("${TrackId}", unit.Id.ToString());
			text = ((!unit.Identified.Value) ? text.Replace("${Target}", "") : text.Replace("${Target}", unit.BaseObject.Name.Value));
		}
		else
		{
			text = text.Replace("${TrackInfo}", "");
			text = text.Replace("${TrackId}", "???");
			text.Replace("${Target}", Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "UnknownEntity"));
		}
		if (unit.BaseObject.isUnit())
		{
			string key = unit.BaseObject._type.ToString();
			if (unit.BaseObject._type == ObjectBase.ObjectType.Biologic)
			{
				key = "Submarine";
			}
			text = text.Replace("${Type}", Singleton<LanguageResourceHandler>.Instance.getText("Contact", key));
		}
		else
		{
			text = text.Replace("${Type}", Singleton<LanguageResourceHandler>.Instance.getText("Contact", "Contact"));
		}
		text = text.Replace("${Course}", unit.BaseObject.Heading.Value.ToString("0"));
		text = text.Replace("${GeoPos}", unit.BaseObject._geoPosition.DisplayLatitude + ", " + unit.BaseObject._geoPosition.DisplayLongitude);
		return text;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
