using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(WeaponSystemHardpoint), "launch")]
internal static class Launch
{
	internal static readonly ConditionalWeakTable<Missile, Shot> Pending = new ConditionalWeakTable<Missile, Shot>();

	internal static bool CanBindDirect(Missile missile)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Invalid comparison between Unknown and I4
		if (!RemoteGuidance.GroupControlled(missile) && ((WeaponBase)missile)._seeker is ActiveRadarSeeker && (int)((WeaponBase)missile)._seeker._guidanceType == 3)
		{
			if (((WeaponBase)missile)._launchMalfunction.Value)
			{
				return !Singleton<OptionsManager>.Instance.WeaponFailuresEnabled;
			}
			return true;
		}
		return false;
	}

	private static bool Prefix(WeaponSystemHardpoint __instance, ObjectBase targetObject, out Shot __state)
	{
		__state = null;
		AmmunitionParameters ap = ((WeaponSystem)__instance)._ammoForEngage?._ap;
		if (!Relay.Ammo(ap))
		{
			return true;
		}
		bool flag = Network.Designated(((BaseSystem)__instance)._baseObject, ap, targetObject);
		WeaponBase obj = __instance._weapons.FirstOrDefault((WeaponBase w) => w?._ap?._ammunitionFileName == ap._ammunitionFileName);
		Missile val = (Missile)(object)((obj is Missile) ? obj : null);
		if ((Object)(object)val == (Object)null || ((WeaponBase)val).isLaunched())
		{
			return !flag;
		}
		if (!flag)
		{
			__state = new Shot
			{
				Missile = val
			};
			return true;
		}
		if (Pending.TryGetValue(val, out var _))
		{
			return false;
		}
		if (!Network.FindRoute(((BaseSystem)__instance)._baseObject, ap, targetObject, val, out var route))
		{
			return false;
		}
		__state = new Shot
		{
			Missile = val,
			Guide = route.Guide,
			Target = targetObject,
			Route = route
		};
		Pending.Add(val, __state);
		return true;
	}

	private static void Postfix(Shot __state, ref bool ____endAlignmentAfterLaunch)
	{
		if (__state != null && ((WeaponBase)__state.Missile).isLaunched() && RemoteGuidance.Active(__state.Missile))
		{
			Missile missile = __state.Missile;
			____endAlignmentAfterLaunch = true;
			ReleaseLauncherAlignment(missile, afterLaunch: false);
			if (__state.Linked)
			{
				Debug.Log((object)("[SPCR] DIRECT-LAUNCH A=" + Network.Name(__state.Tracker) + " B=" + Network.Name(((WeaponBase)missile)._launchPlatform) + " C=" + Network.Name(((BaseSystem)__state.Guide)._baseObject) + " radar=" + ((BaseSystem)__state.Guide)._systemName + " missile=" + Network.Name((ObjectBase)(object)missile) + " target=" + Network.Name(__state.Target) + " B-radars-off=" + Network.RadarsOff(((WeaponBase)missile)._launchPlatform)));
			}
		}
	}

	internal static void ReleaseLauncherAlignment(Missile missile, bool afterLaunch)
	{
		ObjectBase launchPlatform = ((WeaponBase)missile)._launchPlatform;
		int uid = ((WeaponBase)missile)._alignmentUID;
		((WeaponBase)missile)._alignmentUID = -1;
		if (uid < 0 || (Object)(object)launchPlatform == (Object)null || launchPlatform.IsDestroyed)
		{
			return;
		}
		foreach (AlignUnitToTarget alignmentTask in launchPlatform._alignmentTasks)
		{
			if (!((Object)(object)alignmentTask._guidedWeapon != (Object)(object)missile))
			{
				alignmentTask._isGuidingWeapon = false;
				alignmentTask._guidedWeapon = null;
				Debug.Log((object)("[SPCR] LAUNCHER-RELEASE B=" + Network.Name(launchPlatform) + " missile=" + Network.Name((ObjectBase)(object)missile) + " uid=" + uid + " phase=" + (afterLaunch ? "late" : "at-launch")));
				if (afterLaunch && alignmentTask._uid == uid && !launchPlatform._obp._weaponSystems.Any((WeaponSystem system) => system._executingEngageTask && system._alignmentUID == uid))
				{
					launchPlatform.DelayedEndAlignment(uid, 0f, "SPCR remote guidance");
				}
				break;
			}
		}
	}

	private static void Finalizer(Shot __state)
	{
		if (__state?.Route == null)
		{
			return;
		}
		Pending.Remove(__state.Missile);
		if (__state.Linked && ((WeaponBase)__state.Missile).isLaunched())
		{
			return;
		}
		((SensorSystem)__state.Guide).FreeChannels(__state.Target, (WeaponBase)(object)__state.Missile);
		if (((WeaponBase)__state.Missile)._guidingRadarSystem == __state.Guide)
		{
			((WeaponBase)__state.Missile)._guidingRadarSystem = null;
			SeekerBase seeker = ((WeaponBase)__state.Missile)._seeker;
			if (seeker != null)
			{
				seeker.setGuidingSystem((SensorSystemRadar)null);
			}
		}
		RemoteGuidance.Reset(__state.Missile);
	}
}
