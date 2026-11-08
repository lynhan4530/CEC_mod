using SeaPower;

namespace SeaPowerCooperativeRelay;

internal struct GuidanceFrame
{
	internal Missile Weapon;

	internal ObjectBase Source;

	internal ObjectBase Director;

	internal ObjectBase CommandTarget;

	internal ObjectBase SeekerTarget;

	internal ObjectBase RejectedTarget;

	internal string CommandLabel;

	internal string EchoLabel;

	internal string RejectedLabel;

	internal bool Guarded;

	internal bool SeekerOn;

	internal bool SeekerControl;

	internal GuidancePhase Phase;

	internal RemoteGuidance.Gap Gap;

	internal RemoteGuidance.HopStatus HopStatus;

	internal float HopDistanceNM;

	internal float HopLimitNM;

	internal bool Remote;

	internal bool DirectorEcho;

	internal bool AwaitingNewSource;

	internal bool SourceLive;

	internal bool DirectorReady;

	internal bool UpstreamLive;

	internal bool DirectorSearchOff;

	internal int SourceAge;

	internal int ReportAge;

	internal string Notice;

	internal bool NativeProfile;

	internal string NativeState;

	internal string NativeNote;

	internal string NativeRestriction;

	internal string NativeDirector;
}
