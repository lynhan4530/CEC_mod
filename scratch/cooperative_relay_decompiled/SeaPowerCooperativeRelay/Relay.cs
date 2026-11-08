using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SeaPower;
using UniRx;
using Unity.Entities;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

internal static class Relay
{
	private sealed class Switch
	{
		internal bool On = true;

		internal long Changed;
	}

	private sealed class Flight
	{
		internal Taskforce Force;

		internal ObjectBase Tracker;

		internal double NextScan;

		internal double LostSince = -1.0;

		internal bool NoUsableSource;

		internal bool AwaitConfirmation;

		internal bool RadioChecked;

		internal SensorSystemRadar Remote;

		internal SensorSystemRadar VerifiedRadar;

		internal SensorSystemRadar CheckedRadar;

		internal GuidanceNotice Notice;

		internal readonly Dictionary<SensorSystemRadar, double> RetryAfter = new Dictionary<SensorSystemRadar, double>();
	}

	private struct Candidate : IComparable<Candidate>
	{
		internal SensorSystemRadar Radar;

		internal float Age;

		internal float Distance;

		internal bool Local => !float.IsPositiveInfinity(Age);

		public int CompareTo(Candidate other)
		{
			int num = Age.CompareTo(other.Age);
			if (num != 0)
			{
				return num;
			}
			num = Distance.CompareTo(other.Distance);
			if (num != 0)
			{
				return num;
			}
			num = ((BaseSystem)Radar)._baseObject.UniqueID.CompareTo(((BaseSystem)other.Radar)._baseObject.UniqueID);
			if (num != 0)
			{
				return num;
			}
			return string.CompareOrdinal(((BaseSystem)Radar)._sectionName, ((BaseSystem)other.Radar)._sectionName);
		}
	}

	private static readonly HashSet<string> ammunition = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> sensors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly ConditionalWeakTable<ObjectBase, Switch> switches = new ConditionalWeakTable<ObjectBase, Switch>();

	private static readonly ConditionalWeakTable<Missile, Flight> flights = new ConditionalWeakTable<Missile, Flight>();

	private static readonly ConditionalWeakTable<Missile, object> restoredLaunches = new ConditionalWeakTable<Missile, object>();

	internal static long ParticipationRevision { get; private set; }

	internal static void Load(string path)
	{
		ammunition.Clear();
		sensors.Clear();
		NativeCapabilities.Load(Path.Combine(Path.GetDirectoryName(path), "NativeCapabilities.ini"));
		RemoteGuidance.ResetConfig();
		ReadConfig(path);
		if (ammunition.Count == 0 || sensors.Count == 0)
		{
			throw new InvalidDataException("Missing ammunition or radar declarations: " + path);
		}
		string path2 = Path.Combine(Path.GetDirectoryName(path), "CooperativeRelay.user.ini");
		if (File.Exists(path2) && UserConfigEnabled(path2))
		{
			ReadConfig(path2);
		}
		Debug.Log((object)("[SPCR] CONFIG ammunition=" + ammunition.Count + " radars=" + sensors.Count + " native-profiles=" + NativeCapabilities.WeaponCount + "/" + NativeCapabilities.RadarCount));
	}

	private static bool UserConfigEnabled(string path)
	{
		bool flag = false;
		bool result = false;
		foreach (string item in File.ReadLines(path))
		{
			string text = item.Trim();
			if (text.Length == 0 || text.StartsWith(";") || text.StartsWith("#"))
			{
				continue;
			}
			if (text.StartsWith("[") && text.EndsWith("]"))
			{
				flag = text.Substring(1, text.Length - 2).Trim().Equals("Options", StringComparison.OrdinalIgnoreCase);
				continue;
			}
			int num = text.IndexOf('=');
			if (flag && num > 0 && text.Substring(0, num).Trim().Equals("Enabled", StringComparison.OrdinalIgnoreCase) && !bool.TryParse(text.Substring(num + 1).Trim(), out result))
			{
				throw new FormatException("Expected True or False: " + text);
			}
		}
		return result;
	}

	private static void ReadConfig(string path)
	{
		string text = "";
		HashSet<string> hashSet = null;
		foreach (string item in File.ReadLines(path))
		{
			string text2 = item.Trim();
			if (text2.Length == 0 || text2.StartsWith(";") || text2.StartsWith("#"))
			{
				continue;
			}
			if (text2.StartsWith("[") && text2.EndsWith("]"))
			{
				text = text2.Substring(1, text2.Length - 2).Trim();
				hashSet = (text.Equals("Ammunition", StringComparison.OrdinalIgnoreCase) ? ammunition : (text.Equals("Sensors", StringComparison.OrdinalIgnoreCase) ? sensors : null));
				continue;
			}
			int num = text2.IndexOf('=');
			if (num <= 0)
			{
				throw new FormatException("Invalid declaration: " + text2);
			}
			string text3 = text2.Substring(0, num).Trim();
			string text4 = text2.Substring(num + 1).Trim();
			if (text.Equals("Uplinks", StringComparison.OrdinalIgnoreCase))
			{
				RemoteGuidance.SetTransmitter(text3, text4.Equals("False", StringComparison.OrdinalIgnoreCase) ? "" : text4);
			}
			else if (text.Equals("RelayModel", StringComparison.OrdinalIgnoreCase) && text3.Equals("MaxHopRangeNM", StringComparison.OrdinalIgnoreCase))
			{
				RemoteGuidance.SetRange(text4);
			}
			else if (text.Equals("RelayModel", StringComparison.OrdinalIgnoreCase) && text3.Equals("NativePhoenixCooperation", StringComparison.OrdinalIgnoreCase))
			{
				if (!bool.TryParse(text4, out var result))
				{
					throw new FormatException("Expected True or False: " + text2);
				}
				if (result)
				{
					Debug.Log((object)"[SPCR] NativePhoenixCooperation ignored: the 1.4.3 demonstration pairing has no verified offboard-control authority.");
				}
			}
			else if (hashSet != null)
			{
				if (!bool.TryParse(text4, out var result2))
				{
					throw new FormatException("Expected True or False: " + text2);
				}
				if (result2)
				{
					hashSet.Add(text3);
				}
				else
				{
					hashSet.Remove(text3);
				}
			}
		}
	}

	internal static bool SensorAllowed(string name)
	{
		if (name != null)
		{
			return sensors.Contains(name);
		}
		return false;
	}

	internal static bool AmmoConfigured(string id)
	{
		if (id != null)
		{
			return ammunition.Contains(id);
		}
		return false;
	}

	private static bool ConfiguredActiveAmmo(AmmunitionParameters ap)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Invalid comparison between Unknown and I4
		if (ap != null && (int)ap._guidanceType == 3)
		{
			return AmmoConfigured(ap._ammunitionFileName);
		}
		return false;
	}

	internal static bool Ammo(AmmunitionParameters ap)
	{
		if (ConfiguredActiveAmmo(ap) && ap._midCourseCorrection != null)
		{
			if (ap._midCourseCorrection.Index != 0 && ap._midCourseCorrection.Index != 1)
			{
				return ap._midCourseCorrection.Index == 3;
			}
			return true;
		}
		return false;
	}

	internal static int OriginalCorrection(AmmunitionParameters ap)
	{
		int? obj;
		if (ap == null)
		{
			obj = null;
		}
		else
		{
			MidCourseCorrection midCourseCorrection = ap._midCourseCorrection;
			obj = ((midCourseCorrection != null) ? new int?(midCourseCorrection.Index) : null);
		}
		int? num = obj;
		return num.GetValueOrDefault(-1);
	}

	internal static bool Capable(ObjectBase owner)
	{
		if ((Object)(object)owner == (Object)null || owner.IsDestroyed || (!(owner is Aircraft) && !(owner is Vessel) && !(owner is LandUnit)) || owner._obp?._sensorSystems == null)
		{
			return false;
		}
		foreach (SensorSystem item in (Collection<SensorSystem>)(object)owner._obp._sensorSystems)
		{
			if (item is SensorSystemRadar && (SensorAllowed(((BaseSystem)item)._systemName) || RemoteGuidance.TransmitterAllowed(((BaseSystem)item)._systemName)))
			{
				return true;
			}
		}
		return false;
	}

	internal static bool Participates(ObjectBase owner)
	{
		if ((Object)(object)owner != (Object)null)
		{
			if (switches.TryGetValue(owner, out var value))
			{
				return value.On;
			}
			return true;
		}
		return false;
	}

	internal static bool ParticipationChanged(ObjectBase owner, long revision)
	{
		if ((Object)(object)owner != (Object)null && switches.TryGetValue(owner, out var value))
		{
			return value.Changed > revision;
		}
		return false;
	}

	internal static bool Enabled(ObjectBase owner)
	{
		if (Capable(owner))
		{
			return Participates(owner);
		}
		return false;
	}

	internal static void RestoreParticipation(ObjectBase owner, bool on)
	{
		if (on)
		{
			switches.Remove(owner);
			return;
		}
		switches.GetValue(owner, (ObjectBase _) => new Switch()).On = false;
	}

	internal static bool CanToggle(ObjectBase owner)
	{
		if (Network.PlayerUnit(owner))
		{
			if (Participates(owner) && !Capable(owner) && !Network.CanToggleNoLock(owner))
			{
				ReactiveDictionary<string, Ammunition> ammunitionNameToAmmunitionDictionary = owner.AmmunitionNameToAmmunitionDictionary;
				if (ammunitionNameToAmmunitionDictionary == null || !ammunitionNameToAmmunitionDictionary.Values.Any((Ammunition ammo) => Ammo(ammo?._ap)))
				{
					ObjectBaseParameters obp = owner._obp;
					if (obp == null)
					{
						return false;
					}
					return (obp._weaponSystems?.OfType<WeaponSystemHardpoint>().Any((WeaponSystemHardpoint mount) => mount._weapons.Any((WeaponBase w) => w is Missile && Ammo(w._ap)))).GetValueOrDefault();
				}
			}
			return true;
		}
		return false;
	}

	internal static void Toggle(ObjectBase owner)
	{
		if (CanToggle(owner))
		{
			Switch value = switches.GetValue(owner, (ObjectBase _) => new Switch());
			value.On = !value.On;
			value.Changed = ++ParticipationRevision;
			Debug.Log((object)("[SPCR] PARTICIPATION unit=" + Network.Name(owner) + " on=" + value.On));
		}
	}

	private static List<Candidate> Coverage(Taskforce force, ObjectBase target, ObjectBase exclude)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Invalid comparison between Unknown and I4
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Invalid comparison between Unknown and I4
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		List<Candidate> list = new List<Candidate>();
		PlottingTable plottingTable = force.PlottingTable;
		Vehicle val = ((plottingTable != null) ? plottingTable.VehicleForObject(target) : null);
		World defaultGameObjectInjectionWorld = World.DefaultGameObjectInjectionWorld;
		if (val == null || defaultGameObjectInjectionWorld == null || !defaultGameObjectInjectionWorld.IsCreated)
		{
			return list;
		}
		Dictionary<Entity, float> dictionary = new Dictionary<Entity, float>();
		EntityManager entityManager = defaultGameObjectInjectionWorld.EntityManager;
		foreach (Entity item2 in val.CurrentTracks())
		{
			if (!((EntityManager)(ref entityManager)).Exists(item2) || !((EntityManager)(ref entityManager)).HasComponent<TrackComponent>(item2))
			{
				continue;
			}
			TrackComponent componentData = ((EntityManager)(ref entityManager)).GetComponentData<TrackComponent>(item2);
			if (!(componentData.DetectedObject != target.Entity) && ((int)componentData.SensorType == 4 || (int)componentData.SensorType == 5))
			{
				float num = ((Epoch)(ref componentData.Epoch)).AgeFrom(GameTime.missionSessionTime);
				if (!(num < 0f) && (!(componentData.MaxAge > 0f) || !(num > componentData.MaxAge)) && (!dictionary.TryGetValue(componentData.Source, out var value) || num < value))
				{
					dictionary[componentData.Source] = num;
				}
			}
		}
		if (dictionary.Count == 0)
		{
			return list;
		}
		foreach (ObjectBase taskforceObject in force.TaskforceObjects)
		{
			if ((Object)(object)taskforceObject == (Object)(object)exclude || !Enabled(taskforceObject) || taskforceObject._taskforce != force)
			{
				continue;
			}
			foreach (SensorSystem item3 in (Collection<SensorSystem>)(object)taskforceObject._obp._sensorSystems)
			{
				SensorSystemRadar val2 = (SensorSystemRadar)(object)((item3 is SensorSystemRadar) ? item3 : null);
				if (val2 != null && !((BaseSystem)val2).IsDestroyed && ((SensorSystem)val2).CanProduceContacts)
				{
					Radar radar = val2._radar;
					if (radar != null && radar._isActive && SensorAllowed(((BaseSystem)val2)._systemName) && ((SensorSystem)val2)._detectedObjects.Contains(target))
					{
						float value2;
						bool flag = dictionary.TryGetValue(taskforceObject.Entity, out value2) && value2 <= Math.Max(5f, val2.ScanPeriodSeconds * 2f);
						Candidate item = new Candidate
						{
							Radar = val2,
							Age = (flag ? value2 : float.PositiveInfinity)
						};
						Vector3 val3 = ((Component)taskforceObject).transform.position - ((Component)target).transform.position;
						item.Distance = ((Vector3)(ref val3)).sqrMagnitude;
						list.Add(item);
					}
				}
			}
		}
		list.Sort();
		return list;
	}

	private static ObjectBase SelectTracker(List<Candidate> radars, ObjectBase guide, ObjectBase current = null)
	{
		ObjectBase val = null;
		foreach (Candidate radar in radars)
		{
			if (!radar.Local)
			{
				break;
			}
			ObjectBase baseObject = ((BaseSystem)radar.Radar)._baseObject;
			if ((Object)(object)baseObject == (Object)(object)current)
			{
				return baseObject;
			}
			if ((Object)(object)val == (Object)null || ((Object)(object)val == (Object)(object)guide && (Object)(object)baseObject != (Object)(object)guide))
			{
				val = baseObject;
			}
		}
		return val;
	}

	internal static void Register(Missile m)
	{
		if (!((Object)(object)m == (Object)null) && !RemoteGuidance.Enrolled(m) && Ammo(((WeaponBase)m)._ap) && Enabled(((WeaponBase)m)._launchPlatform) && (((WeaponBase)m)._guidingRadarSystem == null || SensorAllowed(((BaseSystem)((WeaponBase)m)._guidingRadarSystem)._systemName) || ((Object)(object)((BaseSystem)((WeaponBase)m)._guidingRadarSystem)._baseObject == (Object)(object)((WeaponBase)m)._launchPlatform && RemoteGuidance.TransmitterCompatible(((WeaponBase)m)._guidingRadarSystem, ((WeaponBase)m)._ap))))
		{
			Flight value = flights.GetValue(m, (Missile _) => new Flight());
			value.Force = ((WeaponBase)m)._launchPlatform._taskforce;
			SensorSystemRadar guidingRadarSystem = ((WeaponBase)m)._guidingRadarSystem;
			if (guidingRadarSystem != null && (Object)(object)((BaseSystem)guidingRadarSystem)._baseObject != (Object)(object)((WeaponBase)m)._launchPlatform && guidingRadarSystem != value.Remote)
			{
				value.Remote = guidingRadarSystem;
				value.AwaitConfirmation = true;
			}
		}
	}

	internal static void Save(Missile m, IniHandler ini, string section)
	{
		Flight value;
		if (RemoteGuidance.Enrolled(m))
		{
			RemoteGuidance.Save(m, ini, section);
		}
		else if (flights.TryGetValue(m, out value))
		{
			ini.writeValue(section, "SPCR_Enrolled", true);
			ini.writeValue(section, "SPCR_ExternalOnly", false);
		}
	}

	internal static void Reset(Missile m)
	{
		restoredLaunches.Remove(m);
		flights.Remove(m);
		RemoteGuidance.Reset(m);
	}

	internal static void PrepareLaunch(Missile m)
	{
		if (!restoredLaunches.Remove(m))
		{
			Reset(m);
		}
	}

	internal static void Restore(Missile m, IniHandler ini, string section)
	{
		Reset(m);
		if (Ammo(((WeaponBase)m)._ap) && ini.readValue(section, "SPCR_Enrolled", false, true))
		{
			if (ini.readValue(section, "SPCR_ExternalOnly", false, true))
			{
				RemoteGuidance.Restore(m, ini, section);
			}
			else
			{
				flights.GetValue(m, (Missile _) => new Flight());
			}
			if (!((WeaponBase)m).isLaunched())
			{
				restoredLaunches.Add(m, new object());
			}
		}
		else if (((WeaponBase)m).isLaunched())
		{
			Register(m);
		}
	}

	internal static bool Midcourse(Missile m)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Invalid comparison between Unknown and I4
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Invalid comparison between Unknown and I4
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Invalid comparison between Unknown and I4
		if ((Object)(object)m == (Object)null || ((ObjectBase)m).IsDestroyed || !((WeaponBase)m).isLaunched() || !((WeaponBase)m)._requiresExternalGuidance || ((WeaponBase)m)._seeker == null || (int)((WeaponBase)m)._seeker._guidanceType != 3 || (((WeaponBase)m)._launchMalfunction.Value && Singleton<OptionsManager>.Instance.WeaponFailuresEnabled) || RemoteGuidance.GroupControlled(m))
		{
			return false;
		}
		FlightStage flightStage = ((WeaponBase)m).getFlightStage();
		if ((int)flightStage != 14)
		{
			if (flightStage - 16 <= 2)
			{
				return false;
			}
			return true;
		}
		if (!((WeaponBase)m)._ap._localTerminalOnly && !((WeaponBase)m).ConnectionLostForever.Value)
		{
			return !(RemoteGuidance.Enrolled(m) ? TargetGuard.SeekerOwns(m) : (((WeaponBase)m)._seeker.isActive() && ((Object)(object)((WeaponBase)m)._seeker._lockedOnTarget != (Object)null || (Object)(object)((WeaponBase)m)._seeker._lockedOnCountermeasure != (Object)null)));
		}
		return false;
	}

	internal static bool Enrolled(Missile m)
	{
		Flight value;
		if (!RemoteGuidance.Enrolled(m))
		{
			return flights.TryGetValue(m, out value);
		}
		return true;
	}

	internal static void ReadStatus(Missile m, ref GuidanceFrame view)
	{
		if (!RemoteGuidance.ReadStatus(m, ref view) && flights.TryGetValue(m, out var value))
		{
			SensorSystemRadar guidingRadarSystem = ((WeaponBase)m)._guidingRadarSystem;
			view.Director = ((BaseSystem)((guidingRadarSystem ?? value.VerifiedRadar)?))._baseObject;
			view.Notice = value.Notice.Current;
			if (!Midcourse(m))
			{
				view.Phase = GuidanceStatus.Autonomous(m);
			}
			else if (value.NoUsableSource || ((WeaponBase)m).ConnectionLostForever.Value)
			{
				view.Phase = GuidancePhase.NativeLost;
			}
			else if (value.AwaitConfirmation && value.CheckedRadar != guidingRadarSystem)
			{
				view.Phase = GuidancePhase.Verifying;
			}
			else if (((WeaponBase)m).ConnectionLost.Value)
			{
				view.Phase = GuidancePhase.NativeLost;
			}
			else if (!value.AwaitConfirmation && guidingRadarSystem != null && guidingRadarSystem == value.VerifiedRadar)
			{
				view.Phase = GuidancePhase.NativeLink;
			}
			else
			{
				view.Phase = GuidancePhase.Verifying;
			}
		}
	}

	internal static void Update(Missile m)
	{
		if (RemoteGuidance.Enrolled(m))
		{
			RemoteGuidance.Tick(m);
		}
		else if (RemoteGuidance.TryStartAutomatic(m))
		{
			RemoteGuidance.Tick(m);
		}
		else
		{
			UpdateNative(m);
		}
	}

	internal static void UpdateNative(Missile m)
	{
		if (RemoteGuidance.Enrolled(m))
		{
			RemoteGuidance.Tick(m);
		}
		else
		{
			if (!flights.TryGetValue(m, out var value))
			{
				return;
			}
			value.RadioChecked = false;
			if (GameTime.IsPaused() || !Midcourse(m))
			{
				return;
			}
			double missionElapsedTime = GameTime.missionElapsedTime;
			if (missionElapsedTime < value.NextScan)
			{
				return;
			}
			value.NextScan = missionElapsedTime + 0.5;
			if (value.Force == null)
			{
				Flight flight = value;
				object obj = ((ObjectBase)m)._taskforce;
				if (obj == null)
				{
					ObjectBase launchPlatform = ((WeaponBase)m)._launchPlatform;
					obj = ((launchPlatform != null) ? launchPlatform._taskforce : null);
				}
				flight.Force = (Taskforce)obj;
				SensorSystemRadar guidingRadarSystem = ((WeaponBase)m)._guidingRadarSystem;
				if (guidingRadarSystem != null && (Object)(object)((BaseSystem)guidingRadarSystem)._baseObject != (Object)(object)((WeaponBase)m)._launchPlatform)
				{
					value.Remote = guidingRadarSystem;
					value.AwaitConfirmation = true;
				}
			}
			ObjectBase currentIntendedTargetObject = ((WeaponBase)m).CurrentIntendedTargetObject;
			if (value.Force == null || (Object)(object)currentIntendedTargetObject == (Object)null || currentIntendedTargetObject.IsDestroyed || currentIntendedTargetObject._taskforce == value.Force)
			{
				return;
			}
			SensorSystemRadar old = ((WeaponBase)m)._guidingRadarSystem;
			List<Candidate> list = Coverage(value.Force, currentIntendedTargetObject, null);
			ObjectBase val = SelectTracker(list, ((BaseSystem)(old?))._baseObject, value.Tracker);
			if ((Object)(object)val != (Object)(object)value.Tracker)
			{
				Debug.Log((object)("[SPCR] TRACK-SOURCE missile=" + Network.Name((ObjectBase)(object)m) + " from=" + Network.Name(value.Tracker) + " to=" + Network.Name(val) + " target=" + Network.Name(currentIntendedTargetObject)));
				value.Tracker = val;
			}
			bool flag = (Object)(object)val != (Object)null && old != null && RemoteGuidance.TransmitterCompatible(old, ((WeaponBase)m)._ap) && list.Exists((Candidate candidate) => candidate.Radar == old);
			bool flag2 = flag || NativeOwnDirectorReady(m, old);
			bool noUsableSource = value.NoUsableSource;
			value.NoUsableSource = !flag2;
			if (flag2 && noUsableSource && !((WeaponBase)m).ConnectionLostForever.Value)
			{
				value.LostSince = -1.0;
				return;
			}
			if (flag2 && !((WeaponBase)m).ConnectionLost.Value && !((WeaponBase)m).ConnectionLostForever.Value)
			{
				value.LostSince = -1.0;
				return;
			}
			if (flag2 && !((WeaponBase)m).ConnectionLostForever.Value)
			{
				if (value.LostSince < 0.0)
				{
					value.LostSince = missionElapsedTime;
				}
				if (missionElapsedTime - value.LostSince < 1.5)
				{
					return;
				}
			}
			((WeaponBase)m).ConnectionLost.Value = true;
			if ((Object)(object)val != (Object)null)
			{
				foreach (Candidate item in list)
				{
					SensorSystemRadar radar = item.Radar;
					if (radar != old && RemoteGuidance.TransmitterCompatible(radar, ((WeaponBase)m)._ap) && (!value.RetryAfter.TryGetValue(radar, out var value2) || !(missionElapsedTime < value2)) && Reserve(radar, currentIntendedTargetObject, m))
					{
						Bind(m, value, old, radar, currentIntendedTargetObject, missionElapsedTime);
						return;
					}
				}
			}
			double value3;
			if (!flag2 && old != null)
			{
				if ((Object)(object)((BaseSystem)old)._baseObject != (Object)null && !((BaseSystem)old)._baseObject.IsDestroyed)
				{
					((SensorSystem)old).FreeChannels(currentIntendedTargetObject, (WeaponBase)(object)m);
				}
				((WeaponBase)m)._guidingRadarSystem = null;
				((WeaponBase)m)._seeker.setGuidingSystem((SensorSystemRadar)null);
				value.Remote = null;
				value.AwaitConfirmation = false;
				Debug.Log((object)("[SPCR] WAIT-SOURCE missile=" + Network.Name((ObjectBase)(object)m) + " target=" + Network.Name(currentIntendedTargetObject)));
			}
			else if (flag && ((WeaponBase)m).ConnectionLostForever.Value && (!value.RetryAfter.TryGetValue(old, out value3) || missionElapsedTime >= value3) && Reserve(old, currentIntendedTargetObject, m))
			{
				value.RetryAfter[old] = missionElapsedTime + 5.0;
				Bind(m, value, old, old, currentIntendedTargetObject, missionElapsedTime);
			}
		}
	}

	private static bool NativeOwnDirectorReady(Missile m, SensorSystemRadar radar)
	{
		if (radar != null && (Object)(object)((BaseSystem)radar)._baseObject == (Object)(object)((WeaponBase)m)._launchPlatform && (Object)(object)((WeaponBase)m)._launchPlatform != (Object)null && !((WeaponBase)m)._launchPlatform.IsDestroyed && ((WeaponBase)m)._launchPlatform._hasControl && Participates(((WeaponBase)m)._launchPlatform) && !((BaseSystem)radar).IsDestroyed && !((BaseSystem)radar).Inoperable.Value && ((BaseSystem)radar).IsOn.Value && ((SensorSystem)radar).CanGuideAmmo(((WeaponBase)m)._ap))
		{
			return ((SensorSystem)radar).IsGuidingWeapon((ObjectBase)(object)m);
		}
		return false;
	}

	private static bool Reserve(SensorSystemRadar radar, ObjectBase target, Missile m)
	{
		bool flag = ((SensorSystem)radar).IsGuidingWeapon((ObjectBase)(object)m);
		if ((!flag && !((SensorSystem)radar).HasFreeWeaponChannels()) || !((SensorSystem)radar).HasFreeTargetChannels(target) || !((SensorSystem)radar).TryToAddTarget(target))
		{
			return false;
		}
		if (flag || ((SensorSystem)radar).TryToAddWeapon((ObjectBase)(object)m))
		{
			return true;
		}
		((SensorSystem)radar).DropTarget(target, (WeaponBase)(object)m);
		return false;
	}

	private static void Bind(Missile m, Flight f, SensorSystemRadar old, SensorSystemRadar radar, ObjectBase target, double now)
	{
		((SensorSystem)radar)._isGuiding = true;
		((SensorSystem)radar)._restPositionReached = false;
		((SensorSystem)radar).setTargetObject(target);
		((WeaponBase)m)._guidingRadarSystem = radar;
		((WeaponBase)m)._seeker.setGuidingSystem(radar);
		if (old != null && old != radar)
		{
			f.RetryAfter[old] = now + 5.0;
			if ((Object)(object)((BaseSystem)old)._baseObject != (Object)null && !((BaseSystem)old)._baseObject.IsDestroyed)
			{
				((SensorSystem)old).FreeChannels(target, (WeaponBase)(object)m);
			}
		}
		f.Remote = (((Object)(object)((BaseSystem)radar)._baseObject == (Object)(object)((WeaponBase)m)._launchPlatform) ? null : radar);
		f.NoUsableSource = false;
		f.LostSince = -1.0;
		f.AwaitConfirmation = true;
		f.CheckedRadar = null;
		((WeaponBase)m).ConnectionLostForever.Value = false;
		((WeaponBase)m).ConnectionLost.Value = true;
		Debug.Log((object)("[SPCR] HANDOFF missile=" + Network.Name((ObjectBase)(object)m) + " from=" + Network.Name(((BaseSystem)(old?))._baseObject) + " to=" + Network.Name(((BaseSystem)radar)._baseObject) + " radar=" + ((BaseSystem)radar)._systemName + " target=" + Network.Name(target)));
	}

	internal static bool CanCheck(Missile m)
	{
		if (!flights.TryGetValue(m, out var value) || !Midcourse(m))
		{
			return true;
		}
		SensorSystemRadar guidingRadarSystem = ((WeaponBase)m)._guidingRadarSystem;
		if (NativeOwnDirectorReady(m, guidingRadarSystem) || (!value.NoUsableSource && guidingRadarSystem != null && (Object)(object)((BaseSystem)guidingRadarSystem)._baseObject != (Object)null && Enabled(((BaseSystem)guidingRadarSystem)._baseObject)))
		{
			value.RadioChecked = true;
			return true;
		}
		((WeaponBase)m).ConnectionLost.Value = true;
		return false;
	}

	internal static ObjectBase RadioOwner(WeaponBase weapon)
	{
		Missile val = (Missile)(object)((weapon is Missile) ? weapon : null);
		if (val != null)
		{
			ObjectBase val2 = RemoteGuidance.Guide(val);
			if (val2 != null)
			{
				return val2;
			}
		}
		Missile val3 = (Missile)(object)((weapon is Missile) ? weapon : null);
		if (val3 != null && flights.TryGetValue(val3, out var value) && value.Remote != null && ((WeaponBase)val3)._guidingRadarSystem == value.Remote && (Object)(object)((BaseSystem)value.Remote)._baseObject != (Object)null)
		{
			return ((BaseSystem)value.Remote)._baseObject;
		}
		return weapon._launchPlatform;
	}

	internal static void Confirm(Missile m)
	{
		if (!flights.TryGetValue(m, out var value) || !value.RadioChecked)
		{
			return;
		}
		SensorSystemRadar val = (value.CheckedRadar = ((WeaponBase)m)._guidingRadarSystem);
		if (!((WeaponBase)m).ConnectionLost.Value && !((WeaponBase)m).ConnectionLostForever.Value && val != null && ((SensorSystem)val).IsGuidingWeapon((ObjectBase)(object)m))
		{
			if (val != value.VerifiedRadar)
			{
				value.Notice.Record(null, null, ((BaseSystem)(value.VerifiedRadar?))._baseObject, ((BaseSystem)val)._baseObject, restored: false);
				value.VerifiedRadar = val;
			}
			if (value.AwaitConfirmation)
			{
				value.AwaitConfirmation = false;
				Debug.Log((object)("[SPCR] LINK-OK missile=" + Network.Name((ObjectBase)(object)m) + " guide=" + Network.Name(((BaseSystem)val)._baseObject)));
			}
		}
	}
}
