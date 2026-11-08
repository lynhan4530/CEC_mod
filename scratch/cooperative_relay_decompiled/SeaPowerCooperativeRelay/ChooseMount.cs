using System;
using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(WeaponSystem), "HasFreeGuidingSensors", new Type[]
{
	typeof(Ammunition),
	typeof(ObjectBase),
	typeof(bool)
})]
internal static class ChooseMount
{
	private static bool Prefix(WeaponSystem __instance, Ammunition ammo, ObjectBase targetObject, ref bool __result)
	{
		if (!(__instance is WeaponSystemHardpoint) || !Network.Designated(((BaseSystem)__instance)._baseObject, ammo?._ap, targetObject))
		{
			return true;
		}
		__result = Network.Route(((BaseSystem)__instance)._baseObject, ammo._ap, targetObject, out var _, out var _);
		return false;
	}
}
