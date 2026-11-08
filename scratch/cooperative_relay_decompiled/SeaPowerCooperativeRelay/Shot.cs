using SeaPower;

namespace SeaPowerCooperativeRelay;

internal sealed class Shot
{
	internal Missile Missile;

	internal SensorSystemRadar Guide;

	internal ObjectBase Target;

	internal RemoteGuidance.Route Route;

	internal bool Linked;

	internal ObjectBase Tracker => ((BaseSystem)Route.Source)._baseObject;
}
