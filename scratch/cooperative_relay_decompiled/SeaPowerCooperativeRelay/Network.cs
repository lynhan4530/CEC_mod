using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SeaPower;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

internal static class Network
{
	private sealed class LaunchMode
	{
		internal string LastWait;
	}

	private static readonly ConditionalWeakTable<ObjectBase, LaunchMode> launchModes = new ConditionalWeakTable<ObjectBase, LaunchMode>();

	internal static bool NoLock(ObjectBase unit)
	{
		LaunchMode value;
		if ((Object)(object)unit != (Object)null)
		{
			return launchModes.TryGetValue(unit, out value);
		}
		return false;
	}

	private static bool Supported(AmmunitionParameters ap)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Invalid comparison between Unknown and I4
		if (Relay.Ammo(ap) && (int)ap._guidanceType == 3)
		{
			if ((int)ap._targetType != 1)
			{
				if (ap._hasSecondaryTargetType)
				{
					return (int)ap._secondaryTargetType == 1;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	internal static bool CanToggleNoLock(ObjectBase unit)
	{
		if (unit is Aircraft)
		{
			if (!NoLock(unit))
			{
				ObjectBaseParameters obp = unit._obp;
				if (obp == null)
				{
					return false;
				}
				return (obp._weaponSystems?.OfType<WeaponSystemHardpoint>().Any((WeaponSystemHardpoint mount) => mount._weapons.Any((WeaponBase w) => w is Missile && Supported(w._ap)))).GetValueOrDefault();
			}
			return true;
		}
		return false;
	}

	internal static void ToggleNoLock(ObjectBase unit)
	{
		if (PlayerUnit(unit) && CanToggleNoLock(unit))
		{
			if (!launchModes.Remove(unit))
			{
				launchModes.Add(unit, new LaunchMode());
			}
			Debug.Log((object)("[SPCR] NO-LOCK B=" + Name(unit) + " on=" + NoLock(unit)));
		}
	}

	internal static bool Platform(ObjectBase unit)
	{
		if (!(unit is Aircraft) && !(unit is Vessel) && !(unit is LandUnit))
		{
			return unit is Submarine;
		}
		return true;
	}

	internal static bool PlayerUnit(ObjectBase unit)
	{
		if ((Object)(object)unit != (Object)null && !unit.IsDestroyed && unit._taskforce != null && unit.IsPlayerObject)
		{
			return Platform(unit);
		}
		return false;
	}

	internal static bool RadarsOff(ObjectBase unit)
	{
		if (unit._obp?._sensorSystems == null)
		{
			return true;
		}
		foreach (SensorSystem item in (Collection<SensorSystem>)(object)unit._obp._sensorSystems)
		{
			SensorSystemRadar val = (SensorSystemRadar)(object)((item is SensorSystemRadar) ? item : null);
			if (val == null)
			{
				continue;
			}
			if (!((BaseSystem)val).IsOn.Value)
			{
				Radar radar = val._radar;
				if (radar == null || !radar._isActive)
				{
					continue;
				}
			}
			return false;
		}
		return true;
	}

	internal static bool Designated(ObjectBase unit, AmmunitionParameters ap, ObjectBase target)
	{
		if (NoLock(unit) && PlayerUnit(unit) && (Object)(object)target != (Object)null && !target.IsDestroyed && target.isAirTarget() && target._taskforce != unit._taskforce)
		{
			return Supported(ap);
		}
		return false;
	}

	internal static bool Route(ObjectBase unit, AmmunitionParameters ap, ObjectBase target, out ObjectBase tracker, out SensorSystemRadar guide, Missile reserveFor = null)
	{
		RemoteGuidance.Route route;
		bool result = FindRoute(unit, ap, target, reserveFor, out route);
		tracker = ((BaseSystem)route?.Source)._baseObject;
		guide = route?.Guide;
		return result;
	}

	internal static bool FindRoute(ObjectBase unit, AmmunitionParameters ap, ObjectBase target, Missile weapon, out RemoteGuidance.Route route)
	{
		route = null;
		if (!Designated(unit, ap, target))
		{
			return false;
		}
		LaunchMode value = launchModes.GetValue(unit, (ObjectBase _) => new LaunchMode());
		string text = "B datalink disabled";
		if (Relay.Participates(unit))
		{
			route = RemoteGuidance.Select(unit._taskforce, target, unit, unit, ap, weapon, null, null, out var gap);
			text = ((gap == RemoteGuidance.Gap.Track) ? "no measured external radar track" : ("no compatible A-C-receiver path or weapon channel (" + gap.ToString() + ")"));
		}
		if (route != null)
		{
			value.LastWait = null;
			return true;
		}
		if (value.LastWait != text)
		{
			Debug.Log((object)("[SPCR] WAIT B=" + Name(unit) + " target=" + Name(target) + " reason=" + text));
			value.LastWait = text;
		}
		return false;
	}

	internal static bool Ready(WeaponSystem mount)
	{
		ObjectBase tracker;
		SensorSystemRadar guide;
		return Route(((BaseSystem)mount)._baseObject, mount._ammoForEngage?._ap, mount._targetObject, out tracker, out guide);
	}

	internal static bool HasExternalTrack(ObjectBase unit, AmmunitionParameters ap, ObjectBase target)
	{
		if (Designated(unit, ap, target) && Relay.Participates(unit))
		{
			return RemoteGuidance.HasMeasuredTrack(unit, target);
		}
		return false;
	}

	internal static string Name(ObjectBase unit)
	{
		if (!((Object)(object)unit == (Object)null))
		{
			return unit.getUIDAndName();
		}
		return "-";
	}
}
