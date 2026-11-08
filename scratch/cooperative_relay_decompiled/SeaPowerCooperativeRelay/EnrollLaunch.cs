using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(Missile), "Container_Launch")]
internal static class EnrollLaunch
{
	private static void Prefix(Missile __instance)
	{
		Relay.PrepareLaunch(__instance);
	}

	private static void Postfix(Missile __instance)
	{
		if (!((WeaponBase)__instance).isLaunched() || Relay.Enrolled(__instance))
		{
			return;
		}
		if (Launch.Pending.TryGetValue(__instance, out var value))
		{
			if (Launch.CanBindDirect(__instance))
			{
				((WeaponBase)__instance)._guidingRadarSystem = value.Guide;
				((WeaponBase)__instance)._seeker.setGuidingSystem(value.Guide);
				RemoteGuidance.Start(__instance, value.Target, value.Route);
				value.Linked = true;
			}
		}
		else
		{
			RemoteGuidance.TryStartAutomatic(__instance);
		}
	}
}
