using MapControl;
using Noesis;
using SeaPower;
using UnityEngine;

namespace SeapowerUI.ViewModels;

internal class PlaceFDInitialWaypointState : InteractionState
{
	private readonly InteractionStateManager _manager;

	private VehicleType _waypointType;

	public ObjectBase Source { get; }

	public PlaceFDInitialWaypointState(InteractionStateManager manager, ObjectBase source, VehicleType waypointType)
	{
		base.CursorName = "attacking";
		_manager = manager;
		Source = source;
		_waypointType = waypointType;
		ClearStates(_manager);
	}

	public override void OnExit()
	{
		base.OnExit();
	}

	public override HitTestResultBehavior MouseAction(object v, RoutedEventArgs args, GeoPosition mousePosition, MapBase map, ref bool handled)
	{
		if (args is MouseButtonEventArgs e)
		{
			if (e.RightButton == MouseButtonState.Pressed)
			{
				if (!(v is MapUnitViewModel))
				{
					Vector3 position = mousePosition.ToUnity();
					position = Source.transform.InverseTransformPoint(position);
					if (_waypointType == VehicleType.Plane)
					{
						float num = Source._obp._flightDeck._planeInitialWaypoint.y;
						if (num < 0.2267855f)
						{
							num = 9.07142f;
						}
						Source._obp._flightDeck._planeInitialWaypoint = position;
						Source._obp._flightDeck._planeInitialWaypoint.y = num;
					}
					else if (_waypointType == VehicleType.Helicopter)
					{
						float num = Source._obp._flightDeck._heliInitialWaypoint.y;
						if (num < 0.0907142f)
						{
							num = 0.907142f;
						}
						Source._obp._flightDeck._heliInitialWaypoint = position;
						Source._obp._flightDeck._heliInitialWaypoint.y = num;
					}
					else if (_waypointType == VehicleType.VTOL || _waypointType == VehicleType.STOL)
					{
						float num = Source._obp._flightDeck._vtolInitialWaypoint.y;
						if (num < 0.2267855f)
						{
							num = 9.07142f;
						}
						Source._obp._flightDeck._vtolInitialWaypoint = position;
						Source._obp._flightDeck._vtolInitialWaypoint.y = num;
					}
					_manager.PopState<PlaceFDInitialWaypointState>();
				}
				handled = true;
				return HitTestResultBehavior.Stop;
			}
			if (e.MiddleButton == MouseButtonState.Pressed)
			{
				_manager.PopState<PlaceFDInitialWaypointState>();
				handled = true;
				return HitTestResultBehavior.Stop;
			}
		}
		else if (v is MapUnitViewModel)
		{
			base.CursorName = "unavailable";
		}
		else if (v is MapViewModel)
		{
			base.CursorName = "attacking";
		}
		return base.MouseAction(v, args, mousePosition, map, ref handled);
	}
}
