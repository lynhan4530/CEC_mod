using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(WeaponBase), "SetGuidanceSystem")]
internal static class InitialDirector
{
	private static void Prefix(WeaponBase __instance, ref bool requiredToActivateNonLaserGuidanceSystem)
	{
		Missile val = (Missile)(object)((__instance is Missile) ? __instance : null);
		if (val != null)
		{
			Shot value;
			if (RemoteGuidance.Enrolled(val) && !RemoteGuidance.NativeModeReleased(val))
			{
				requiredToActivateNonLaserGuidanceSystem = false;
			}
			else if (Launch.Pending.TryGetValue(val, out value) && Launch.CanBindDirect(val))
			{
				((WeaponBase)val)._guidingRadarSystem = value.Guide;
				requiredToActivateNonLaserGuidanceSystem = false;
			}
		}
	}

	private static void Postfix(WeaponBase __instance)
	{
		Missile val = (Missile)(object)((__instance is Missile) ? __instance : null);
		if (val != null && Launch.Pending.TryGetValue(val, out var value) && !value.Linked && Launch.CanBindDirect(val))
		{
			RemoteGuidance.Start(val, value.Target, value.Route);
			value.Linked = true;
		}
	}
}
