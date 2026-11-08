using System;
using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(Missile), "CheckRadioConnection")]
internal static class RadioCheck
{
	[ThreadStatic]
	private static int depth;

	internal static bool InProgress => depth > 0;

	private static bool Prefix(Missile __instance, out bool __state)
	{
		__state = Relay.CanCheck(__instance);
		if (__state)
		{
			depth++;
		}
		return __state;
	}

	private static void Finalizer(bool __state)
	{
		if (__state)
		{
			depth--;
		}
	}
}
