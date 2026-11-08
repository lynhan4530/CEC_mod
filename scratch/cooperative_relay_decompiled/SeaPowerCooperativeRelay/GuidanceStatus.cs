using System.Collections.Generic;
using SeaPower;
using UniRx;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

internal static class GuidanceStatus
{
	internal static string Name(ObjectBase unit)
	{
		if ((Object)(object)unit == (Object)null)
		{
			return "—";
		}
		if (string.IsNullOrEmpty(unit.DisplayNameShort))
		{
			return unit.getName();
		}
		return unit.DisplayNameShort;
	}

	internal static string Id(ObjectBase unit)
	{
		if ((Object)(object)unit == (Object)null)
		{
			return "";
		}
		int mapContactID = unit.GetMapContactID();
		return "#" + ((mapContactID > 0) ? mapContactID : unit.UniqueID);
	}

	internal static string Station(ObjectBase unit)
	{
		if (!((Object)(object)unit == (Object)null))
		{
			return Name(unit) + " " + Id(unit);
		}
		return "—";
	}

	private static string ContactLabel(ObjectBase target, Taskforce observer)
	{
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)target == (Object)null)
		{
			return "—";
		}
		if (observer != null && target._taskforce == observer)
		{
			return Id(target) + " · " + Name(target);
		}
		object obj;
		if (observer == null)
		{
			obj = null;
		}
		else
		{
			PlottingTable plottingTable = observer.PlottingTable;
			obj = ((plottingTable != null) ? plottingTable.VehicleForObject(target) : null);
		}
		Vehicle val = (Vehicle)obj;
		if (val == null)
		{
			return "未知接触";
		}
		ReactiveProperty<bool> identified = val.Identified;
		string text = ((identified == null || !identified.Value) ? null : val.Class?.Value);
		if (string.IsNullOrEmpty(text))
		{
			text = "未识别接触";
		}
		if (val.Id <= 0)
		{
			return text;
		}
		return "#" + val.Id + " · " + text;
	}

	internal static GuidancePhase Autonomous(Missile m)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Invalid comparison between Unknown and I4
		OptionsManager instance = Singleton<OptionsManager>.Instance;
		if (instance != null && instance.WeaponFailuresEnabled && (((WeaponBase)m)._launchMalfunction.Value || ((WeaponBase)m)._seekerMalfunction.Value))
		{
			return GuidancePhase.Malfunction;
		}
		if (RemoteGuidance.GroupControlled(m) || (int)((WeaponBase)m).getFlightStage() != 14)
		{
			return GuidancePhase.Native;
		}
		if (((WeaponBase)m)._seeker == null || !((WeaponBase)m)._seeker.isActive())
		{
			return GuidancePhase.Terminal;
		}
		if (TargetGuard.Managed(m))
		{
			if (!TargetGuard.SeekerOwns(m))
			{
				return GuidancePhase.Searching;
			}
			return GuidancePhase.Locked;
		}
		if (!((Object)(object)((WeaponBase)m)._seeker._lockedOnTarget != (Object)null) && !((Object)(object)((WeaponBase)m)._seeker._lockedOnCountermeasure != (Object)null))
		{
			return GuidancePhase.Searching;
		}
		return GuidancePhase.Locked;
	}

	private static bool InFlight(Missile m, Taskforce player)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Invalid comparison between Unknown and I4
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Invalid comparison between Unknown and I4
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Invalid comparison between Unknown and I4
		if ((Object)(object)m == (Object)null || ((ObjectBase)m).IsDestroyed || ((ObjectBase)m)._taskforce != player || !((WeaponBase)m).isLaunched() || (!Relay.Enrolled(m) && !Relay.Ammo(((WeaponBase)m)._ap) && !NativeCapabilities.Matches(((WeaponBase)m)._ap)))
		{
			return false;
		}
		FlightStage flightStage = ((WeaponBase)m).getFlightStage();
		if ((int)flightStage != 16 && (int)flightStage != 18)
		{
			return (int)flightStage != 17;
		}
		return false;
	}

	internal static void ListFor(ObjectBase selected, Taskforce player, List<Missile> result)
	{
		result.Clear();
		if ((Object)(object)selected == (Object)null || selected.IsDestroyed || player == null || selected._taskforce != player)
		{
			return;
		}
		Missile val = (Missile)(object)((selected is Missile) ? selected : null);
		if (val != null)
		{
			if (!InFlight(val, player))
			{
				return;
			}
			result.Add(val);
			selected = ((WeaponBase)val)._launchPlatform;
			if ((Object)(object)selected == (Object)null)
			{
				return;
			}
		}
		if (selected._obp?._weaponSystems == null)
		{
			return;
		}
		foreach (ObjectBase taskforceWeapon in player._taskforceWeapons)
		{
			Missile val2 = (Missile)(object)((taskforceWeapon is Missile) ? taskforceWeapon : null);
			if (val2 != null && (Object)(object)((WeaponBase)val2)._launchPlatform == (Object)(object)selected && InFlight(val2, player) && !result.Contains(val2))
			{
				result.Add(val2);
			}
		}
		result.Sort((Missile a, Missile b) => ((ObjectBase)a).UniqueID.CompareTo(((ObjectBase)b).UniqueID));
	}

	internal static GuidanceFrame Read(Missile m)
	{
		GuidanceFrame guidanceFrame = default(GuidanceFrame);
		guidanceFrame.Weapon = m;
		guidanceFrame.ReportAge = -1;
		GuidanceFrame view = guidanceFrame;
		if (!Relay.Enrolled(m))
		{
			NativeCapabilities.Read(m, ref view);
		}
		Relay.ReadStatus(m, ref view);
		TargetGuard.ReadStatus(m, ref view);
		Taskforce taskforce = ((ObjectBase)m)._taskforce;
		view.CommandLabel = ContactLabel(view.CommandTarget, taskforce);
		view.EchoLabel = (((Object)(object)view.SeekerTarget == (Object)(object)view.CommandTarget) ? view.CommandLabel : ContactLabel(view.SeekerTarget, taskforce));
		view.RejectedLabel = (((Object)(object)view.RejectedTarget == (Object)(object)view.SeekerTarget) ? view.EchoLabel : ContactLabel(view.RejectedTarget, taskforce));
		return view;
	}
}
