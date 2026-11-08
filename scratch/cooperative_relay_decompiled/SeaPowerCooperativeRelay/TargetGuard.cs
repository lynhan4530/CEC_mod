using System.Runtime.CompilerServices;
using SeaPower;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

internal static class TargetGuard
{
	private sealed class Status
	{
		internal ObjectBase Rejected;

		internal ObjectBase LoggedEcho;

		internal float WarningUntil;

		internal double NextRejectLog;

		internal string LoggedMode;
	}

	private static readonly ConditionalWeakTable<Missile, Status> status = new ConditionalWeakTable<Missile, Status>();

	internal static void Reset(Missile m)
	{
		status.Remove(m);
	}

	internal static bool Managed(Missile m)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Invalid comparison between Unknown and I4
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Invalid comparison between Unknown and I4
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Invalid comparison between Unknown and I4
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Invalid comparison between Unknown and I4
		if (!((Object)(object)m == (Object)null) && RemoteGuidance.Enrolled(m) && ((WeaponBase)m).isLaunched() && !((ObjectBase)m).IsDestroyed && !RemoteGuidance.NativeModeReleased(m) && !RemoteGuidance.NativeTerminal(m))
		{
			SeekerBase seeker = ((WeaponBase)m)._seeker;
			if (seeker != null && (int)seeker._guidanceType == 3 && !RemoteGuidance.GroupControlled(m) && ((WeaponBase)m)._seeker is ActiveRadarSeeker && (!((WeaponBase)m)._launchMalfunction.Value || !Singleton<OptionsManager>.Instance.WeaponFailuresEnabled))
			{
				FlightStage flightStage = ((WeaponBase)m).getFlightStage();
				if ((int)flightStage != 16 && (int)flightStage != 18)
				{
					return (int)flightStage != 17;
				}
				return false;
			}
		}
		return false;
	}

	internal static bool Countermeasure(ObjectBase o)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Invalid comparison between Unknown and I4
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Invalid comparison between Unknown and I4
		if ((Object)(object)o != (Object)null)
		{
			if ((int)o._type != 13)
			{
				return (int)o._type == 15;
			}
			return true;
		}
		return false;
	}

	internal static bool Accepts(Missile m, ObjectBase o)
	{
		if ((Object)(object)o == (Object)null || o.IsDestroyed)
		{
			return false;
		}
		if (Countermeasure(o))
		{
			return true;
		}
		object obj = ((ObjectBase)m)._taskforce;
		if (obj == null)
		{
			ObjectBase launchPlatform = ((WeaponBase)m)._launchPlatform;
			obj = ((launchPlatform != null) ? launchPlatform._taskforce : null);
		}
		Taskforce val = (Taskforce)obj;
		if ((Object)(object)o == (Object)(object)RemoteGuidance.CommandTarget(m))
		{
			if (val != null)
			{
				return o._taskforce != val;
			}
			return true;
		}
		return false;
	}

	internal static bool SeekerOwns(Missile m)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Invalid comparison between Unknown and I4
		if (!Managed(m) || (int)((WeaponBase)m).getFlightStage() != 14 || !((WeaponBase)m)._seeker.isActive() || (((WeaponBase)m)._seekerMalfunction.Value && Singleton<OptionsManager>.Instance.WeaponFailuresEnabled))
		{
			return false;
		}
		SeekerBase seeker = ((WeaponBase)m)._seeker;
		ObjectBase val = seeker._strongestSensorEcho?._objectBase;
		if (Accepts(m, val))
		{
			if (!((Object)(object)seeker._lockedOnTarget == (Object)(object)val))
			{
				return (Object)(object)seeker._lockedOnCountermeasure == (Object)(object)val;
			}
			return true;
		}
		return false;
	}

	internal static ObjectBase FlightTarget(WeaponBase w, ObjectBase native)
	{
		Missile val = (Missile)(object)((w is Missile) ? w : null);
		if (val == null || !Managed(val))
		{
			return native;
		}
		if (SeekerOwns(val))
		{
			return ((WeaponBase)val)._seeker._strongestSensorEcho._objectBase;
		}
		ObjectBase val2 = RemoteGuidance.CommandTarget(val);
		if (!Accepts(val, val2))
		{
			return null;
		}
		return val2;
	}

	internal static SensorEcho FlightEcho(WeaponBase w, SensorEcho native)
	{
		Missile val = (Missile)(object)((w is Missile) ? w : null);
		if (val == null || !Managed(val))
		{
			return native;
		}
		if (!SeekerOwns(val))
		{
			return null;
		}
		return ((WeaponBase)val)._seeker._strongestSensorEcho;
	}

	internal static ObjectBase DisplayTarget(WeaponBase w)
	{
		Missile val = (Missile)(object)((w is Missile) ? w : null);
		if (val == null || !Managed(val))
		{
			return w.CurrentIntendedTargetObject;
		}
		return RemoteGuidance.CommandTarget(val);
	}

	internal static bool SteeringSeekerActive(SeekerBase seeker)
	{
		ActiveRadarSeeker val = (ActiveRadarSeeker)(object)((seeker is ActiveRadarSeeker) ? seeker : null);
		if (val != null)
		{
			ObjectBase host = val.Host;
			Missile val2 = (Missile)(object)((host is Missile) ? host : null);
			if (val2 != null && Managed(val2))
			{
				return SeekerOwns(val2);
			}
		}
		return seeker.isActive();
	}

	internal static ObjectBase SteeringCountermeasure(SeekerBase seeker)
	{
		ActiveRadarSeeker val = (ActiveRadarSeeker)(object)((seeker is ActiveRadarSeeker) ? seeker : null);
		if (val != null)
		{
			ObjectBase host = val.Host;
			Missile val2 = (Missile)(object)((host is Missile) ? host : null);
			if (val2 != null && Managed(val2) && !SeekerOwns(val2))
			{
				return null;
			}
		}
		return seeker._lockedOnCountermeasure;
	}

	private static void Reject(Missile m, ObjectBase rejected)
	{
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)rejected == (Object)null))
		{
			Status value = status.GetValue(m, (Missile _) => new Status());
			if ((Object)(object)value.Rejected != (Object)(object)rejected || GameTime.missionElapsedTime >= value.NextRejectLog)
			{
				string[] obj = new string[8]
				{
					"[SPCR] TARGET-REJECT missile=",
					Network.Name((ObjectBase)(object)m),
					" command=",
					Network.Name(RemoteGuidance.CommandTarget(m)),
					" rejected=",
					Network.Name(rejected),
					" stage=",
					null
				};
				FlightStage flightStage = ((WeaponBase)m).getFlightStage();
				obj[7] = ((object)(FlightStage)(ref flightStage)).ToString();
				Debug.Log((object)string.Concat(obj));
				value.NextRejectLog = GameTime.missionElapsedTime + 5.0;
			}
			value.Rejected = rejected;
			value.WarningUntil = Time.unscaledTime + 7f;
		}
	}

	internal static void Clean(Missile m, SeekerBase seeker)
	{
		ObjectBase val = seeker._strongestSensorEcho?._objectBase;
		if ((Object)(object)val != (Object)null && !Accepts(m, val))
		{
			Reject(m, val);
			seeker._strongestSensorEcho = null;
		}
		if ((Object)(object)seeker._lockedOnTarget != (Object)null && !Accepts(m, seeker._lockedOnTarget))
		{
			Reject(m, seeker._lockedOnTarget);
			seeker._lockedOnTarget = null;
		}
		if ((Object)(object)seeker._firstLockedOnTarget != (Object)null && !Accepts(m, seeker._firstLockedOnTarget))
		{
			seeker._firstLockedOnTarget = null;
		}
		if ((Object)(object)seeker._lockedOnCountermeasure != (Object)null && !Countermeasure(seeker._lockedOnCountermeasure))
		{
			Reject(m, seeker._lockedOnCountermeasure);
			seeker._lockedOnCountermeasure = null;
		}
	}

	internal static void Observe(Missile m)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		if (Managed(m))
		{
			Status value = status.GetValue(m, (Missile _) => new Status());
			ObjectBase val = ((WeaponBase)m)._seeker._strongestSensorEcho?._objectBase;
			string text = ((!SeekerOwns(m)) ? (((int)((WeaponBase)m).getFlightStage() == 14) ? "TERMINAL-SEARCH" : "REPORT") : (Countermeasure(val) ? "COUNTERMEASURE" : "SEEKER"));
			if (!(value.LoggedMode == text) || !((Object)(object)value.LoggedEcho == (Object)(object)val))
			{
				string[] obj = new string[10]
				{
					"[SPCR] TARGET-STATE missile=",
					Network.Name((ObjectBase)(object)m),
					" command=",
					Network.Name(RemoteGuidance.CommandTarget(m)),
					" seeker=",
					Network.Name(val),
					" control=",
					text,
					" stage=",
					null
				};
				FlightStage flightStage = ((WeaponBase)m).getFlightStage();
				obj[9] = ((object)(FlightStage)(ref flightStage)).ToString();
				Debug.Log((object)string.Concat(obj));
				value.LoggedMode = text;
				value.LoggedEcho = val;
			}
		}
	}

	internal static void ReadStatus(Missile m, ref GuidanceFrame view)
	{
		view.Guarded = Managed(m);
		view.CommandTarget = (view.Guarded ? RemoteGuidance.CommandTarget(m) : ((WeaponBase)m).CurrentIntendedTargetObject);
		view.SeekerTarget = ((WeaponBase)m)._seeker?._strongestSensorEcho?._objectBase;
		SeekerBase seeker = ((WeaponBase)m)._seeker;
		view.SeekerOn = seeker != null && seeker.isActive();
		view.SeekerControl = view.Guarded && SeekerOwns(m);
		Status value;
		if (view.Guarded && (Object)(object)view.SeekerTarget != (Object)null && !Accepts(m, view.SeekerTarget))
		{
			view.RejectedTarget = view.SeekerTarget;
		}
		else if (view.Guarded && status.TryGetValue(m, out value) && Time.unscaledTime < value.WarningUntil)
		{
			view.RejectedTarget = value.Rejected;
		}
	}
}
