using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(Missile), "CalcEstimatedTargetPosition")]
internal static class ExternalTargetEstimate
{
	private static bool Prefix(Missile __instance)
	{
		return RemoteGuidance.Estimate(__instance);
	}
}
