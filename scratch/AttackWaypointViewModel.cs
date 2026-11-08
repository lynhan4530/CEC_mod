using SeaPower;

namespace SeapowerUI;

public class AttackWaypointViewModel : WaypointViewModel
{
	public enum MapVisualType
	{
		Invisible,
		Weapon,
		ActiveSonobouy,
		PassiveSonobouy
	}

	private MapVisualType _visualType;

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

	public AttackWaypointViewModel(AttackAtWaypoint task)
		: base(task)
	{
		VisualType = MapVisualType.Weapon;
		if (task._ammo._ap._subType == Ammunition.Type.Sonobuoy)
		{
			if (task._ammo._ap._isActiveSonobuoy)
			{
				VisualType = MapVisualType.ActiveSonobouy;
			}
			else
			{
				VisualType = MapVisualType.PassiveSonobouy;
			}
		}
	}
}
