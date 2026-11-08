using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using SeaPower;
using Unity.Entities;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

internal static class RemoteGuidance
{
	private sealed class Retry
	{
		internal double Next;
	}

	internal struct Report
	{
		internal GeoPosition Position;

		internal double Time;

		internal Vector3 Velocity;

		internal bool HasVelocity;
	}

	internal sealed class Route
	{
		internal SensorSystemRadar Source;

		internal SensorSystemRadar Guide;

		internal Report Report;
	}

	internal sealed class Assessment
	{
		internal SensorSystemRadar Source;

		internal double SourceTime;

		internal bool DirectorReady;

		internal bool Upstream;

		internal HopStatus Hop;

		internal float HopDistance;
	}

	internal enum HopStatus
	{
		Unavailable,
		Connected,
		OutOfRange,
		Blocked
	}

	[Flags]
	internal enum Gap
	{
		None = 0,
		Track = 1,
		Transmitter = 2,
		Channel = 4,
		Upstream = 8,
		Downstream = 0x10
	}

	private sealed class Flight
	{
		internal Taskforce Force;

		internal ObjectBase Target;

		internal int TargetId;

		internal SensorSystemRadar Source;

		internal SensorSystemRadar Guide;

		internal Report Last;

		internal SensorSystemRadar LastSource;

		internal SensorSystemRadar DisplayGuide;

		internal Assessment Status;

		internal bool Checked;

		internal bool GuideHadEcho;

		internal long CheckedParticipation;

		internal Gap Gap;

		internal GuidanceNotice Notice;

		internal bool HasReport;

		internal bool Connected;

		internal bool Finished;

		internal bool Announced;

		internal bool Restoring;

		internal bool Automatic;

		internal bool NativeExternalGuidance;

		internal bool TerminalReleased;

		internal bool ModeReleased;

		internal double NextScan;

		internal double NextReportLog;
	}

	internal sealed class Observations
	{
		internal Report Latest;

		internal Report Previous;

		internal Entity Owner;

		internal bool HasLatest;

		internal bool HasPrevious;

		internal void Add(Report report)
		{
			if (!HasLatest || report.Time > Latest.Time)
			{
				Previous = Latest;
				HasPrevious = HasLatest;
				Latest = report;
				HasLatest = true;
			}
			else if (report.Time < Latest.Time && (!HasPrevious || report.Time > Previous.Time))
			{
				Previous = report;
				HasPrevious = true;
			}
		}

		internal Report LatestReport()
		{
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			Report latest = Latest;
			if (HasPrevious && latest.Time - Previous.Time >= 0.05)
			{
				latest.Velocity = (((GeoPosition)(ref latest.Position)).ToUnity() - ((GeoPosition)(ref Previous.Position)).ToUnity()) / (float)(latest.Time - Previous.Time);
				latest.HasVelocity = true;
			}
			return latest;
		}
	}

	[CompilerGenerated]
	private sealed class _003CExternalRadars_003Ed__21 : IEnumerable<SensorSystemRadar>, IEnumerable, IEnumerator<SensorSystemRadar>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private SensorSystemRadar _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private Taskforce force;

		public Taskforce _003C_003E3__force;

		private ObjectBase launcher;

		public ObjectBase _003C_003E3__launcher;

		private IEnumerator<ObjectBase> _003C_003E7__wrap1;

		private ObjectBase _003Cunit_003E5__3;

		private IEnumerator<SensorSystem> _003C_003E7__wrap3;

		SensorSystemRadar IEnumerator<SensorSystemRadar>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CExternalRadars_003Ed__21(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			int num = _003C_003E1__state;
			if ((uint)(num - -4) <= 1u || num == 1)
			{
				try
				{
					if (num == -4 || num == 1)
					{
						try
						{
						}
						finally
						{
							_003C_003Em__Finally2();
						}
					}
				}
				finally
				{
					_003C_003Em__Finally1();
				}
			}
			_003C_003E7__wrap1 = null;
			_003Cunit_003E5__3 = null;
			_003C_003E7__wrap3 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			try
			{
				int num = _003C_003E1__state;
				if (num != 0)
				{
					if (num != 1)
					{
						return false;
					}
					_003C_003E1__state = -4;
					goto IL_00f5;
				}
				_003C_003E1__state = -1;
				_003C_003E7__wrap1 = force.TaskforceObjects.GetEnumerator();
				_003C_003E1__state = -3;
				goto IL_0116;
				IL_00f5:
				while (_003C_003E7__wrap3.MoveNext())
				{
					SensorSystem current = _003C_003E7__wrap3.Current;
					SensorSystemRadar val = (SensorSystemRadar)(object)((current is SensorSystemRadar) ? current : null);
					if (val != null && (Object)(object)((BaseSystem)val)._baseObject == (Object)(object)_003Cunit_003E5__3)
					{
						_003C_003E2__current = val;
						_003C_003E1__state = 1;
						return true;
					}
				}
				_003C_003Em__Finally2();
				_003C_003E7__wrap3 = null;
				_003Cunit_003E5__3 = null;
				goto IL_0116;
				IL_0116:
				do
				{
					if (_003C_003E7__wrap1.MoveNext())
					{
						_003Cunit_003E5__3 = _003C_003E7__wrap1.Current;
						continue;
					}
					_003C_003Em__Finally1();
					_003C_003E7__wrap1 = null;
					return false;
				}
				while ((Object)(object)_003Cunit_003E5__3 == (Object)(object)launcher || !Relay.Enabled(_003Cunit_003E5__3) || _003Cunit_003E5__3._taskforce != force);
				_003C_003E7__wrap3 = ((Collection<SensorSystem>)(object)_003Cunit_003E5__3._obp._sensorSystems).GetEnumerator();
				_003C_003E1__state = -4;
				goto IL_00f5;
			}
			catch
			{
				//try-fault
				((IDisposable)this).Dispose();
				throw;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		private void _003C_003Em__Finally1()
		{
			_003C_003E1__state = -1;
			if (_003C_003E7__wrap1 != null)
			{
				_003C_003E7__wrap1.Dispose();
			}
		}

		private void _003C_003Em__Finally2()
		{
			_003C_003E1__state = -3;
			if (_003C_003E7__wrap3 != null)
			{
				_003C_003E7__wrap3.Dispose();
			}
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<SensorSystemRadar> IEnumerable<SensorSystemRadar>.GetEnumerator()
		{
			_003CExternalRadars_003Ed__21 _003CExternalRadars_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CExternalRadars_003Ed__ = this;
			}
			else
			{
				_003CExternalRadars_003Ed__ = new _003CExternalRadars_003Ed__21(0);
			}
			_003CExternalRadars_003Ed__.force = _003C_003E3__force;
			_003CExternalRadars_003Ed__.launcher = _003C_003E3__launcher;
			return _003CExternalRadars_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<SensorSystemRadar>)this).GetEnumerator();
		}
	}

	private static readonly Dictionary<string, HashSet<string>> transmitters = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

	private static readonly ConditionalWeakTable<Missile, Flight> flights = new ConditionalWeakTable<Missile, Flight>();

	private static readonly ConditionalWeakTable<Missile, Retry> attempts = new ConditionalWeakTable<Missile, Retry>();

	private const float UnityPerNauticalMile = 27.559496f;

	private static float maxHopNM;

	internal static void ResetConfig()
	{
		transmitters.Clear();
		maxHopNM = 180f;
	}

	internal static void SetRange(string value)
	{
		if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !(result > 0f) || float.IsInfinity(result * 27.559496f))
		{
			throw new FormatException("Invalid MaxHopRangeNM");
		}
		maxHopNM = result;
	}

	internal static void SetTransmitter(string radar, string list)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string[] array = list.Split(',', StringSplitOptions.None);
		foreach (string text in array)
		{
			if (text.Trim().Length > 0)
			{
				hashSet.Add(text.Trim());
			}
		}
		transmitters[radar] = hashSet;
	}

	internal static bool TransmitterAllowed(string name)
	{
		if (name != null && transmitters.TryGetValue(name, out var value))
		{
			return value.Count > 0;
		}
		return false;
	}

	internal static Dictionary<Entity, Observations> ReadObservations(Taskforce force, ObjectBase target)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Invalid comparison between Unknown and I4
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Invalid comparison between Unknown and I4
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<Entity, Observations> dictionary = new Dictionary<Entity, Observations>();
		PlottingTable plottingTable = force.PlottingTable;
		Vehicle val = ((plottingTable != null) ? plottingTable.VehicleForObject(target) : null);
		World defaultGameObjectInjectionWorld = World.DefaultGameObjectInjectionWorld;
		if (val == null || defaultGameObjectInjectionWorld == null || !defaultGameObjectInjectionWorld.IsCreated)
		{
			return dictionary;
		}
		EntityManager entityManager = defaultGameObjectInjectionWorld.EntityManager;
		foreach (Entity item in val.CurrentTracks())
		{
			if (!((EntityManager)(ref entityManager)).Exists(item) || !((EntityManager)(ref entityManager)).HasComponent<TrackComponent>(item))
			{
				continue;
			}
			TrackComponent componentData = ((EntityManager)(ref entityManager)).GetComponentData<TrackComponent>(item);
			if (componentData.DetectedObject != target.Entity || ((int)componentData.SensorType != 4 && (int)componentData.SensorType != 5) || !((EntityManager)(ref entityManager)).HasComponent<TrackBearing>(item) || !((EntityManager)(ref entityManager)).HasComponent<TrackRangeInMeters>(item) || !((EntityManager)(ref entityManager)).HasComponent<TrackAltitude>(item))
			{
				continue;
			}
			float num = ((Epoch)(ref componentData.Epoch)).AgeFrom(GameTime.missionSessionTime);
			if (num < 0f || (componentData.MaxAge > 0f && num > componentData.MaxAge))
			{
				continue;
			}
			float estimate = ((EntityManager)(ref entityManager)).GetComponentData<TrackBearing>(item).Estimate.Estimate;
			float estimate2 = ((EntityManager)(ref entityManager)).GetComponentData<TrackRangeInMeters>(item).Estimate.Estimate;
			float estimate3 = ((EntityManager)(ref entityManager)).GetComponentData<TrackAltitude>(item).Estimate.Estimate;
			if (estimate2 < 0f || !Finite(estimate2) || !Finite(estimate) || !Finite(estimate3))
			{
				continue;
			}
			GeoPosition position = ((GeoPosition)(ref componentData.OwnPosition)).OffsetByDistanceOnHeadingUnity(estimate2 / 1000f, (double)estimate);
			((GeoPosition)(ref position)).SetHeightUnity((double)estimate3);
			if (!((GeoPosition)(ref position)).IsInvalid())
			{
				if (!dictionary.TryGetValue(componentData.Sensor, out var value))
				{
					Entity sensor = componentData.Sensor;
					Observations obj = new Observations
					{
						Owner = componentData.Source
					};
					value = obj;
					dictionary[sensor] = obj;
				}
				if (!(value.Owner != componentData.Source))
				{
					value.Add(new Report
					{
						Position = position,
						Time = GameTime.missionElapsedTime - (double)num
					});
				}
			}
		}
		return dictionary;
	}

	private static bool Finite(float v)
	{
		if (!float.IsNaN(v))
		{
			return !float.IsInfinity(v);
		}
		return false;
	}

	private static bool RadioOnline(SensorSystemRadar r)
	{
		if (r != null && !((BaseSystem)r).IsDestroyed && !((BaseSystem)r).Inoperable.Value && (Object)(object)((BaseSystem)r)._baseObject != (Object)null && !((BaseSystem)r)._baseObject.IsDestroyed && ((BaseSystem)r)._baseObject._hasControl)
		{
			return Relay.Participates(((BaseSystem)r)._baseObject);
		}
		return false;
	}

	private static bool Tracking(SensorSystemRadar r)
	{
		if (RadioOnline(r) && Relay.SensorAllowed(((BaseSystem)r)._systemName) && ((SensorSystem)r).CanProduceContacts)
		{
			return r._radar?._isActive ?? false;
		}
		return false;
	}

	[IteratorStateMachine(typeof(_003CExternalRadars_003Ed__21))]
	private static IEnumerable<SensorSystemRadar> ExternalRadars(Taskforce force, ObjectBase launcher)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CExternalRadars_003Ed__21(-2)
		{
			_003C_003E3__force = force,
			_003C_003E3__launcher = launcher
		};
	}

	private static bool Measured(SensorSystemRadar radar, ObjectBase target, Dictionary<Entity, Observations> observations, out Observations series)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		series = null;
		if (Tracking(radar) && ((SensorSystem)radar)._detectedObjects.Contains(target) && observations.TryGetValue(((SensorSystem)radar).InstanceEntity, out series) && series.Owner == ((BaseSystem)radar)._baseObject.Entity)
		{
			return GameTime.missionElapsedTime - series.Latest.Time <= (double)Math.Max(5f, 2f * radar.ScanPeriodSeconds);
		}
		return false;
	}

	internal static bool HasMeasuredTrack(ObjectBase launcher, ObjectBase target)
	{
		Taskforce taskforce = launcher._taskforce;
		Dictionary<Entity, Observations> observations = ReadObservations(taskforce, target);
		foreach (SensorSystemRadar item in ExternalRadars(taskforce, launcher))
		{
			if (Measured(item, target, observations, out var _))
			{
				return true;
			}
		}
		return false;
	}

	internal static bool TransmitterCompatible(SensorSystemRadar r, AmmunitionParameters ap)
	{
		if (ap != null && RadioOnline(r) && Relay.Ammo(ap) && TransmitterAllowed(((BaseSystem)r)._systemName))
		{
			return transmitters[((BaseSystem)r)._systemName].Contains(ap._ammunitionFileName);
		}
		return false;
	}

	private static bool Reserved(SensorSystemRadar r, Missile weapon)
	{
		if ((Object)(object)weapon != (Object)null)
		{
			return ((SensorSystem)r)._associatedWeapons.Contains((ObjectBase)(object)weapon);
		}
		return false;
	}

	private static bool ChannelAvailable(SensorSystemRadar r, Missile weapon)
	{
		if (!Reserved(r, weapon))
		{
			return ((SensorSystem)r).HasFreeWeaponChannels();
		}
		return true;
	}

	private static bool Reserve(SensorSystemRadar r, Missile weapon)
	{
		if ((Object)(object)weapon == (Object)null || Reserved(r, weapon))
		{
			return true;
		}
		if (!((SensorSystem)r).HasFreeWeaponChannels())
		{
			return false;
		}
		if (((BaseSystem)r).IsOn.Value)
		{
			return ((SensorSystem)r).TryToAddWeapon((ObjectBase)(object)weapon);
		}
		((SensorSystem)r)._associatedWeapons.Add((ObjectBase)(object)weapon);
		return true;
	}

	private static HopStatus CheckHop(SensorSystemRadar sender, ObjectBase receiver, out float distance)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		ObjectBase baseObject = ((BaseSystem)sender)._baseObject;
		distance = 0f;
		if ((Object)(object)baseObject == (Object)(object)receiver)
		{
			return HopStatus.Connected;
		}
		distance = Vector3.Distance(((SensorSystem)sender).SensorPosition, ((Component)receiver).transform.position);
		if (distance > maxHopNM * 27.559496f)
		{
			return HopStatus.OutOfRange;
		}
		UnitSensorCache instance = Singleton<UnitSensorCache>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return HopStatus.Unavailable;
		}
		LineOfSightResult val = instance.UnitsHaveRadarlLOS(baseObject, receiver, ((SensorSystem)sender).SensorPosition.y, distance, 1f);
		if (!val._isValid)
		{
			return HopStatus.Unavailable;
		}
		if (!val._withinHorizon || !val._los)
		{
			return HopStatus.Blocked;
		}
		return HopStatus.Connected;
	}

	internal static Route Select(Taskforce force, ObjectBase target, ObjectBase launcher, ObjectBase receiver, AmmunitionParameters ap, Missile weapon, SensorSystemRadar previousSource, SensorSystemRadar previousGuide, out Gap gap, SensorSystemRadar lastDisplayedGuide = null, Assessment status = null)
	{
		status = status ?? new Assessment();
		gap = Gap.Track;
		if (force == null || (Object)(object)target == (Object)null || target.IsDestroyed || target._taskforce == force || (Object)(object)receiver == (Object)null || !Relay.Ammo(ap))
		{
			return null;
		}
		Dictionary<Entity, Observations> observations = ReadObservations(force, target);
		List<Route> list = new List<Route>();
		List<SensorSystemRadar> list2 = new List<SensorSystemRadar>();
		foreach (SensorSystemRadar item in ExternalRadars(force, launcher))
		{
			if (TransmitterCompatible(item, ap))
			{
				list2.Add(item);
			}
			if (Measured(item, target, observations, out var series))
			{
				list.Add(new Route
				{
					Source = item,
					Report = series.LatestReport()
				});
			}
		}
		SensorSystemRadar val = lastDisplayedGuide ?? previousGuide;
		status.DirectorReady = list2.Contains(val) && ChannelAvailable(val, weapon);
		list.Sort(delegate(Route x, Route y)
		{
			int num2 = (y.Source == previousSource).CompareTo(x.Source == previousSource);
			return (num2 == 0) ? y.Report.Time.CompareTo(x.Report.Time) : num2;
		});
		if (list.Count == 0)
		{
			return null;
		}
		status.Source = list[0].Source;
		status.SourceTime = list[0].Report.Time;
		List<Route> list3 = new List<Route>();
		Gap gap2 = Gap.None;
		Gap gap3 = Gap.Transmitter;
		foreach (Route item2 in list)
		{
			SensorSystemRadar source = item2.Source;
			foreach (SensorSystemRadar item3 in list2)
			{
				Gap gap4 = Gap.None;
				HopStatus hopStatus = HopStatus.Unavailable;
				float distance = 0f;
				if (!ChannelAvailable(item3, weapon))
				{
					gap4 = Gap.Channel;
				}
				else if ((hopStatus = CheckHop(source, ((BaseSystem)item3)._baseObject, out distance)) != HopStatus.Connected)
				{
					gap4 = Gap.Upstream;
				}
				else if ((hopStatus = CheckHop(item3, receiver, out distance)) != HopStatus.Connected)
				{
					gap4 = Gap.Downstream;
				}
				if (gap4 != 0)
				{
					if (source == status.Source && item3 == val)
					{
						gap3 = gap4;
						status.Upstream = gap4 == Gap.Downstream;
						status.Hop = hopStatus;
						status.HopDistance = distance;
					}
					else if (val == null)
					{
						if (gap2 == Gap.None || (gap2 == gap4 && status.Hop == hopStatus && distance < status.HopDistance))
						{
							status.Hop = hopStatus;
							status.HopDistance = distance;
						}
						else if (gap2 != gap4 || status.Hop != hopStatus)
						{
							status.Hop = HopStatus.Unavailable;
						}
					}
					gap2 |= gap4;
				}
				else
				{
					list3.Add(new Route
					{
						Source = source,
						Guide = item3,
						Report = item2.Report
					});
				}
			}
		}
		list3.Sort(delegate(Route x, Route y)
		{
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
			int num = (y.Guide == previousGuide).CompareTo(x.Guide == previousGuide);
			if (num == 0)
			{
				num = (y.Source == previousSource).CompareTo(x.Source == previousSource);
			}
			if (num == 0)
			{
				num = ((Object)(object)((BaseSystem)x.Source)._baseObject == (Object)(object)((BaseSystem)x.Guide)._baseObject).CompareTo((Object)(object)((BaseSystem)y.Source)._baseObject == (Object)(object)((BaseSystem)y.Guide)._baseObject);
			}
			if (num == 0)
			{
				num = y.Report.Time.CompareTo(x.Report.Time);
			}
			if (num == 0)
			{
				Vector3 val2 = ((SensorSystem)x.Guide).SensorPosition - ((Component)receiver).transform.position;
				float sqrMagnitude = ((Vector3)(ref val2)).sqrMagnitude;
				val2 = ((SensorSystem)y.Guide).SensorPosition - ((Component)receiver).transform.position;
				num = sqrMagnitude.CompareTo(((Vector3)(ref val2)).sqrMagnitude);
			}
			if (num == 0)
			{
				num = ((BaseSystem)x.Guide)._baseObject.UniqueID.CompareTo(((BaseSystem)y.Guide)._baseObject.UniqueID);
			}
			if (num == 0)
			{
				num = string.CompareOrdinal(((BaseSystem)x.Guide)._sectionName, ((BaseSystem)y.Guide)._sectionName);
			}
			if (num == 0)
			{
				num = ((BaseSystem)x.Source)._baseObject.UniqueID.CompareTo(((BaseSystem)y.Source)._baseObject.UniqueID);
			}
			return (num == 0) ? string.CompareOrdinal(((BaseSystem)x.Source)._sectionName, ((BaseSystem)y.Source)._sectionName) : num;
		});
		foreach (Route item4 in list3)
		{
			if (Reserve(item4.Guide, weapon))
			{
				status.Source = item4.Source;
				status.SourceTime = item4.Report.Time;
				status.DirectorReady = (status.Upstream = true);
				gap = Gap.None;
				return item4;
			}
			gap2 |= Gap.Channel;
			if (item4.Source == status.Source && item4.Guide == val)
			{
				gap3 = Gap.Channel;
				status.DirectorReady = (status.Upstream = false);
			}
		}
		if (val != null)
		{
			gap = gap3;
		}
		else
		{
			gap = ((gap2 == Gap.None) ? Gap.Transmitter : gap2);
		}
		return null;
	}

	internal static bool Enrolled(Missile m)
	{
		Flight value;
		return flights.TryGetValue(m, out value);
	}

	internal static void Reset(Missile m)
	{
		flights.Remove(m);
		attempts.Remove(m);
		TargetGuard.Reset(m);
	}

	internal static bool GroupControlled(Missile m)
	{
		if (!m._inMissileGroup && !m.GroupWingman.Value)
		{
			return m._wasInMissileGroup;
		}
		return true;
	}

	internal static bool NativeModeReleased(Missile m)
	{
		if (flights.TryGetValue(m, out var value))
		{
			return value.ModeReleased;
		}
		return false;
	}

	internal static bool NativeTerminal(Missile m)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Invalid comparison between Unknown and I4
		if (flights.TryGetValue(m, out var value) && value.Automatic && !value.ModeReleased)
		{
			if (!value.TerminalReleased)
			{
				return (int)((WeaponBase)m).getFlightStage() == 14;
			}
			return true;
		}
		return false;
	}

	internal static bool TryStartAutomatic(Missile m)
	{
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Invalid comparison between Unknown and I4
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Invalid comparison between Unknown and I4
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Invalid comparison between Unknown and I4
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Invalid comparison between Unknown and I4
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Invalid comparison between Unknown and I4
		if ((Object)(object)m == (Object)null || GameTime.IsPaused() || Enrolled(m) || !((WeaponBase)m).isLaunched() || ((ObjectBase)m).IsDestroyed || !Relay.Ammo(((WeaponBase)m)._ap) || (Object)(object)((WeaponBase)m)._launchPlatform == (Object)null || !Relay.Participates(((WeaponBase)m)._launchPlatform) || GroupControlled(m) || !(((WeaponBase)m)._seeker is ActiveRadarSeeker) || (int)((WeaponBase)m)._seeker._guidanceType != 3 || (((WeaponBase)m)._launchMalfunction.Value && Singleton<OptionsManager>.Instance.WeaponFailuresEnabled))
		{
			return false;
		}
		FlightStage flightStage = ((WeaponBase)m).getFlightStage();
		if ((int)flightStage == 14 || (int)flightStage == 16 || (int)flightStage == 18 || (int)flightStage == 17)
		{
			return false;
		}
		Retry value = attempts.GetValue(m, (Missile _) => new Retry());
		if (GameTime.missionElapsedTime < value.Next)
		{
			return false;
		}
		value.Next = GameTime.missionElapsedTime + 0.5;
		ObjectBase currentIntendedTargetObject = ((WeaponBase)m).CurrentIntendedTargetObject;
		Gap gap;
		Route route = Select(((ObjectBase)m)._taskforce ?? ((WeaponBase)m)._launchPlatform._taskforce, currentIntendedTargetObject, ((WeaponBase)m)._launchPlatform, (ObjectBase)(object)m, ((WeaponBase)m)._ap, m, null, null, out gap);
		if (route == null)
		{
			return false;
		}
		SensorSystemRadar guidingRadarSystem = ((WeaponBase)m)._guidingRadarSystem;
		Launch.ReleaseLauncherAlignment(m, afterLaunch: true);
		if (guidingRadarSystem != null && guidingRadarSystem != route.Guide && (Object)(object)((BaseSystem)guidingRadarSystem)._baseObject != (Object)null && !((BaseSystem)guidingRadarSystem)._baseObject.IsDestroyed)
		{
			((SensorSystem)guidingRadarSystem).FreeChannels(currentIntendedTargetObject, (WeaponBase)(object)m);
		}
		((WeaponBase)m)._guidingRadarSystem = route.Guide;
		((WeaponBase)m)._seeker.setGuidingSystem(route.Guide);
		Start(m, currentIntendedTargetObject, route, automatic: true);
		Debug.Log((object)("[SPCR] AUTO-COOPERATIVE A=" + Network.Name(((BaseSystem)route.Source)._baseObject) + " B=" + Network.Name(((WeaponBase)m)._launchPlatform) + " C=" + Network.Name(((BaseSystem)route.Guide)._baseObject) + " missile=" + Network.Name((ObjectBase)(object)m)));
		return true;
	}

	internal static ObjectBase CommandTarget(Missile m)
	{
		if (!flights.TryGetValue(m, out var value))
		{
			return null;
		}
		if ((Object)(object)value.Target == (Object)null)
		{
			value.Target = ((value.TargetId > 0) ? Singleton<SceneCreator>.Instance.FindObjectById(value.TargetId) : ((WeaponBase)m).CurrentIntendedTargetObject);
			if ((Object)(object)value.Target != (Object)null)
			{
				value.TargetId = value.Target.UniqueID;
			}
		}
		return value.Target;
	}

	internal static void AssignRetarget(WeaponBase weapon, ObjectBase target)
	{
		ObjectBase currentIntendedTargetObject = weapon.CurrentIntendedTargetObject;
		weapon.CurrentIntendedTargetObject = target;
		Missile val = (Missile)(object)((weapon is Missile) ? weapon : null);
		if (val != null && !((Object)(object)target == (Object)null) && TargetGuard.Managed(val) && flights.TryGetValue(val, out var value) && !((Object)(object)value.Target == (Object)(object)target))
		{
			value.Target = target;
			value.TargetId = target.UniqueID;
			value.Last = default(Report);
			Debug.Log((object)("[SPCR] RETARGET missile=" + Network.Name((ObjectBase)(object)val) + " command-id=" + value.TargetId));
			value.LastSource = (value.Source = null);
			value.HasReport = (value.Connected = (value.Checked = (value.Announced = false)));
			value.Status = null;
			value.Notice = default(GuidanceNotice);
			value.NextScan = 0.0;
			TargetGuard.Reset(val);
			HoldCourse(val);
			if ((Object)(object)currentIntendedTargetObject != (Object)null && (Object)(object)currentIntendedTargetObject != (Object)(object)target && value.Guide == ((WeaponBase)val)._guidingRadarSystem && value.Guide != null)
			{
				((SensorSystem)value.Guide).DropTarget(currentIntendedTargetObject, (WeaponBase)(object)val);
			}
		}
	}

	internal static void Start(Missile m, ObjectBase target, Route route, bool automatic = false)
	{
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		Flight value = flights.GetValue(m, (Missile _) => new Flight());
		value.Automatic = automatic;
		value.NativeExternalGuidance = ((WeaponBase)m)._requiresExternalGuidance;
		((WeaponBase)m)._requiresExternalGuidance = true;
		value.Force = ((WeaponBase)m)._launchPlatform._taskforce;
		value.Target = target;
		value.TargetId = target.UniqueID;
		value.Source = route.Source;
		value.Guide = route.Guide;
		value.DisplayGuide = route.Guide;
		value.Last = route.Report;
		value.LastSource = route.Source;
		value.HasReport = true;
		value.Connected = true;
		if (!automatic)
		{
			GeoPosition initialTargetGeoPosition = (((WeaponBase)m).AimPointGeoPosition = value.Last.Position);
			((WeaponBase)m).InitialTargetGeoPosition = initialTargetGeoPosition;
			((WeaponBase)m)._initialSetTargetPosition = ((GeoPosition)(ref value.Last.Position)).ToUnity();
		}
		WriteEstimate(m, value);
	}

	internal static bool Active(Missile m)
	{
		if (flights.TryGetValue(m, out var value) && !value.Finished)
		{
			return Relay.Midcourse(m);
		}
		return false;
	}

	internal static bool RequiresNativeRadio(WeaponBase w)
	{
		if (w._requiresExternalGuidance)
		{
			Missile val = (Missile)(object)((w is Missile) ? w : null);
			if (val != null)
			{
				if (!Active(val))
				{
					return !NativeTerminal(val);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	internal static void Tick(Missile m)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Invalid comparison between Unknown and I4
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Invalid comparison between Unknown and I4
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Invalid comparison between Unknown and I4
		//IL_0548: Unknown result type (might be due to invalid IL or missing references)
		//IL_054d: Unknown result type (might be due to invalid IL or missing references)
		//IL_056e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0573: Unknown result type (might be due to invalid IL or missing references)
		//IL_048d: Unknown result type (might be due to invalid IL or missing references)
		//IL_049d: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_04aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_04af: Unknown result type (might be due to invalid IL or missing references)
		if (GameTime.IsPaused() || !flights.TryGetValue(m, out var value) || value.Finished || !((WeaponBase)m).isLaunched())
		{
			return;
		}
		int num;
		if (!GroupControlled(m))
		{
			SeekerBase seeker = ((WeaponBase)m)._seeker;
			num = ((seeker == null || (int)seeker._guidanceType != 3) ? 1 : 0);
		}
		else
		{
			num = 1;
		}
		bool flag = (byte)num != 0;
		if (flag || (value.Automatic && (int)((WeaponBase)m).getFlightStage() == 14))
		{
			Release(m, value);
			value.Finished = true;
			value.ModeReleased = flag;
			value.TerminalReleased = !flag;
			((WeaponBase)m)._requiresExternalGuidance = flag && value.NativeExternalGuidance;
			return;
		}
		((WeaponBase)m)._requiresExternalGuidance = true;
		if (!Relay.Midcourse(m))
		{
			if ((int)((WeaponBase)m).getFlightStage() == 14 || ((ObjectBase)m).IsDestroyed || (((WeaponBase)m)._launchMalfunction.Value && Singleton<OptionsManager>.Instance.WeaponFailuresEnabled))
			{
				Release(m, value);
				value.Finished = true;
			}
			return;
		}
		double missionElapsedTime = GameTime.missionElapsedTime;
		if (missionElapsedTime < value.NextScan)
		{
			return;
		}
		value.NextScan = missionElapsedTime + 0.5;
		value.CheckedParticipation = Relay.ParticipationRevision;
		Flight flight = value;
		object obj = value.Force;
		if (obj == null)
		{
			obj = ((ObjectBase)m)._taskforce;
			if (obj == null)
			{
				ObjectBase launchPlatform = ((WeaponBase)m)._launchPlatform;
				obj = ((launchPlatform != null) ? launchPlatform._taskforce : null);
			}
		}
		flight.Force = (Taskforce)obj;
		ObjectBase currentIntendedTargetObject = ((WeaponBase)m).CurrentIntendedTargetObject;
		if (value.Restoring && value.Force != null && (Object)(object)currentIntendedTargetObject != (Object)null)
		{
			value.Guide = ((WeaponBase)m)._guidingRadarSystem;
			value.Restoring = false;
		}
		CommandTarget(m);
		value.Gap = Gap.None;
		value.Status = new Assessment();
		Route route = (((Object)(object)currentIntendedTargetObject != (Object)(object)value.Target) ? null : Select(value.Force, currentIntendedTargetObject, ((WeaponBase)m)._launchPlatform, (ObjectBase)(object)m, ((WeaponBase)m)._ap, m, value.Source, value.Guide, out value.Gap, value.DisplayGuide, value.Status));
		value.Checked = true;
		if (route == null)
		{
			if (value.Connected)
			{
				Debug.Log((object)("[SPCR] UPLINK-LOST missile=" + Network.Name((ObjectBase)(object)m) + " coasting=True"));
			}
			Release(m, value);
			((WeaponBase)m).ConnectionLost.Value = true;
			return;
		}
		bool flag2 = !value.Connected || value.Source != route.Source || value.Guide != route.Guide;
		if (flag2)
		{
			value.Notice.Record(((BaseSystem)(value.Source?))._baseObject, ((BaseSystem)route.Source)._baseObject, ((BaseSystem)(value.DisplayGuide?))._baseObject, ((BaseSystem)route.Guide)._baseObject, !value.Connected && value.Announced);
		}
		value.DisplayGuide = route.Guide;
		Flight flight2 = value;
		int guideHadEcho;
		if (RadioOnline(route.Guide) && ((SensorSystem)route.Guide).CanProduceContacts)
		{
			Radar radar = route.Guide._radar;
			if (radar != null && radar._isActive)
			{
				guideHadEcho = (((SensorSystem)route.Guide)._detectedObjects.Contains(currentIntendedTargetObject) ? 1 : 0);
				goto IL_02ee;
			}
		}
		guideHadEcho = 0;
		goto IL_02ee;
		IL_02ee:
		flight2.GuideHadEcho = (byte)guideHadEcho != 0;
		SensorSystemRadar guide = value.Guide;
		if (guide != route.Guide)
		{
			Release(m, value);
			((WeaponBase)m)._guidingRadarSystem = route.Guide;
			((WeaponBase)m)._seeker.setGuidingSystem(route.Guide);
		}
		if (value.Source != route.Source)
		{
			Debug.Log((object)("[SPCR] TRACK-SOURCE missile=" + Network.Name((ObjectBase)(object)m) + " from=" + Network.Name(((BaseSystem)(value.Source?))._baseObject) + " to=" + Network.Name(((BaseSystem)route.Source)._baseObject)));
		}
		if (guide != route.Guide)
		{
			Debug.Log((object)("[SPCR] HANDOFF missile=" + Network.Name((ObjectBase)(object)m) + " from=" + Network.Name(((BaseSystem)(guide?))._baseObject) + " to=" + Network.Name(((BaseSystem)route.Guide)._baseObject)));
		}
		if (!value.HasReport || route.Report.Time > value.Last.Time + 0.001)
		{
			double num2 = route.Report.Time - value.Last.Time;
			if (!route.Report.HasVelocity && value.HasReport && value.LastSource == route.Source && num2 >= 0.05)
			{
				route.Report.Velocity = (((GeoPosition)(ref route.Report.Position)).ToUnity() - ((GeoPosition)(ref value.Last.Position)).ToUnity()) / (float)num2;
				route.Report.HasVelocity = true;
			}
			value.Last = route.Report;
			value.LastSource = route.Source;
			value.HasReport = true;
			if (missionElapsedTime >= value.NextReportLog)
			{
				string[] obj2 = new string[12]
				{
					"[SPCR] TRACK-UPDATE missile=",
					Network.Name((ObjectBase)(object)m),
					" A=",
					Network.Name(((BaseSystem)route.Source)._baseObject),
					" target=",
					Network.Name(value.Target),
					" measured=",
					null,
					null,
					null,
					null,
					null
				};
				GeoPosition position = value.Last.Position;
				obj2[7] = ((object)(GeoPosition)(ref position)).ToString();
				obj2[8] = " velocity=";
				Vector3 velocity = value.Last.Velocity;
				obj2[9] = ((object)(Vector3)(ref velocity)).ToString();
				obj2[10] = " age=";
				obj2[11] = (missionElapsedTime - value.Last.Time).ToString("F1");
				Debug.Log((object)string.Concat(obj2));
				value.NextReportLog = missionElapsedTime + 5.0;
			}
		}
		if (flag2 || !value.Announced)
		{
			Debug.Log((object)("[SPCR] UPLINK-OK missile=" + Network.Name((ObjectBase)(object)m) + " A=" + Network.Name(((BaseSystem)route.Source)._baseObject) + " C=" + Network.Name(((BaseSystem)route.Guide)._baseObject) + " C-local-echo=" + value.GuideHadEcho + " C-search-on=" + (((SensorSystem)route.Guide).CanProduceContacts && (route.Guide._radar?._isActive ?? false))));
			value.Announced = true;
		}
		value.Connected = true;
		value.Source = route.Source;
		value.Guide = route.Guide;
		((WeaponBase)m).ConnectionLost.Value = false;
		((WeaponBase)m).ConnectionLostForever.Value = false;
		((WeaponBase)m)._lostGuidanceTime = missionElapsedTime;
	}

	private static void Release(Missile m, Flight f)
	{
		SensorSystemRadar guide = f.Guide;
		if (guide != null && (Object)(object)((BaseSystem)guide)._baseObject != (Object)null && !((BaseSystem)guide)._baseObject.IsDestroyed)
		{
			((SensorSystem)guide).FreeChannels(f.Target, (WeaponBase)(object)m);
		}
		if (guide != null && ((WeaponBase)m)._guidingRadarSystem == guide)
		{
			((WeaponBase)m)._guidingRadarSystem = null;
			SeekerBase seeker = ((WeaponBase)m)._seeker;
			if (seeker != null)
			{
				seeker.setGuidingSystem((SensorSystemRadar)null);
			}
		}
		f.Guide = null;
		f.Connected = false;
	}

	internal static ObjectBase Guide(Missile m)
	{
		if (!flights.TryGetValue(m, out var value) || !value.Connected)
		{
			return null;
		}
		return ((BaseSystem)(value.Guide?))._baseObject;
	}

	internal static bool ReadStatus(Missile m, ref GuidanceFrame view)
	{
		if (!flights.TryGetValue(m, out var value))
		{
			return false;
		}
		view.Remote = true;
		view.Source = ((BaseSystem)(value.Source?))._baseObject;
		view.Director = ((BaseSystem)(value.DisplayGuide?))._baseObject;
		view.ReportAge = (value.HasReport ? ((int)Math.Max(0.0, GameTime.missionElapsedTime - value.Last.Time)) : (-1));
		view.Gap = value.Gap;
		view.DirectorEcho = value.GuideHadEcho;
		view.AwaitingNewSource = value.Connected && value.LastSource != value.Source;
		view.Notice = value.Notice.Current;
		if (value.Finished || !Relay.Midcourse(m))
		{
			view.Phase = GuidanceStatus.Autonomous(m);
		}
		else if (!value.Checked)
		{
			view.Phase = GuidancePhase.Verifying;
		}
		else if (value.Connected)
		{
			view.Phase = GuidancePhase.Uplink;
		}
		else
		{
			view.Phase = (value.HasReport ? GuidancePhase.Coasting : GuidancePhase.NoReport);
		}
		OptionsManager instance = Singleton<OptionsManager>.Instance;
		if (instance != null && instance.WeaponFailuresEnabled && (((WeaponBase)m)._launchMalfunction.Value || ((WeaponBase)m)._seekerMalfunction.Value))
		{
			view.Phase = GuidancePhase.Malfunction;
		}
		if (view.Phase == GuidancePhase.Uplink || view.Phase == GuidancePhase.Coasting || view.Phase == GuidancePhase.NoReport)
		{
			view.SourceLive = value.Status?.Source != null;
			if (view.SourceLive)
			{
				view.Source = ((BaseSystem)value.Status.Source)._baseObject;
				view.SourceAge = (int)Math.Max(0.0, GameTime.missionElapsedTime - value.Status.SourceTime);
			}
			view.DirectorReady = value.Status?.DirectorReady ?? false;
			view.UpstreamLive = value.Status?.Upstream ?? false;
			int directorSearchOff;
			if (value.DisplayGuide != null)
			{
				if (((SensorSystem)value.DisplayGuide).CanProduceContacts)
				{
					Radar radar = value.DisplayGuide._radar;
					directorSearchOff = ((radar == null || !radar._isActive) ? 1 : 0);
				}
				else
				{
					directorSearchOff = 1;
				}
			}
			else
			{
				directorSearchOff = 0;
			}
			view.DirectorSearchOff = (byte)directorSearchOff != 0;
			bool flag = Relay.ParticipationChanged(view.Source, value.CheckedParticipation);
			bool flag2 = Relay.ParticipationChanged(view.Director, value.CheckedParticipation);
			if (flag || flag2)
			{
				view.Phase = GuidancePhase.PendingRouteCheck;
				view.SourceLive &= !flag;
				view.DirectorReady &= !flag2;
				view.UpstreamLive = false;
			}
			else if (view.Gap == Gap.Upstream || view.Gap == Gap.Downstream)
			{
				view.HopStatus = value.Status.Hop;
				view.HopDistanceNM = value.Status.HopDistance / 27.559496f;
				view.HopLimitNM = maxHopNM;
			}
		}
		return true;
	}

	internal static bool Estimate(Missile m)
	{
		if (!flights.TryGetValue(m, out var value))
		{
			return true;
		}
		Tick(m);
		TargetGuard.Observe(m);
		if (!TargetGuard.Managed(m) || TargetGuard.SeekerOwns(m))
		{
			return true;
		}
		if (value.HasReport)
		{
			WriteEstimate(m, value);
		}
		else
		{
			HoldCourse(m);
		}
		return false;
	}

	internal static void RememberSeeker(Missile m, double seen)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		if (TargetGuard.SeekerOwns(m) && flights.TryGetValue(m, out var value) && !((Object)(object)((WeaponBase)m)._seeker._strongestSensorEcho._objectBase != (Object)(object)CommandTarget(m)) && (!value.HasReport || !(seen <= value.Last.Time)))
		{
			value.Last = new Report
			{
				Position = new GeoPosition(((WeaponBase)m)._seeker.getEstimatedTargetPosition()),
				Velocity = ((WeaponBase)m)._seeker.getEstimatedTargetVelocityVector(),
				Time = seen,
				HasVelocity = true
			};
			value.HasReport = true;
		}
	}

	private static void HoldCourse(Missile m)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		m._lastKnownTargetVelocity = Vector3.zero;
		((WeaponBase)m)._currentKnownTargetPosition = ((Component)m).transform.position + ((Component)m).transform.forward * Math.Max(1f, ((WeaponBase)m)._ap._launchRangesInUnity.y);
	}

	private static void WriteEstimate(Missile m, Flight f)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		m._lastKnownTargetVelocity = f.Last.Velocity;
		((WeaponBase)m)._currentKnownTargetPosition = ((GeoPosition)(ref f.Last.Position)).ToUnity() + f.Last.Velocity * (float)Math.Max(0.0, GameTime.missionElapsedTime - f.Last.Time);
		if (!f.Automatic)
		{
			((WeaponBase)m).InitialTargetGeoPosition = f.Last.Position;
		}
	}

	internal static void Save(Missile m, IniHandler ini, string section)
	{
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		if (flights.TryGetValue(m, out var value))
		{
			ini.writeValue(section, "SPCR_Enrolled", true);
			ini.writeValue(section, "SPCR_ExternalOnly", true);
			ini.writeValue(section, "SPCR_Automatic", value.Automatic);
			ini.writeValue(section, "SPCR_NativeExternalGuidance", value.NativeExternalGuidance);
			ini.writeValue(section, "SPCR_NativeTerminal", value.TerminalReleased);
			ini.writeValue(section, "SPCR_NativeModeReleased", value.ModeReleased);
			ini.writeValue(section, "SPCR_UplinkFinished", value.Finished);
			ObjectBase val = CommandTarget(m);
			ini.writeValue(section, "SPCR_CommandTargetId", ((Object)(object)val != (Object)null) ? val.UniqueID : value.TargetId);
			ini.writeValue(section, "SPCR_HasReport", value.HasReport);
			if (value.HasReport)
			{
				ini.writeValue(section, "SPCR_Lat", value.Last.Position._latitude);
				ini.writeValue(section, "SPCR_Lon", value.Last.Position._longitude);
				ini.writeValue(section, "SPCR_Height", value.Last.Position._height);
				ini.writeValue(section, "SPCR_Velocity", value.Last.Velocity);
				ini.writeValue(section, "SPCR_Age", (float)Math.Max(0.0, GameTime.missionElapsedTime - value.Last.Time));
			}
		}
	}

	internal static void Restore(Missile m, IniHandler ini, string section)
	{
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		Flight value = flights.GetValue(m, (Missile _) => new Flight());
		value.Restoring = true;
		value.Automatic = ini.readValue(section, "SPCR_Automatic", false, true);
		value.NativeExternalGuidance = ini.readValue(section, "SPCR_NativeExternalGuidance", ((WeaponBase)m)._requiresExternalGuidance, true);
		value.TerminalReleased = ini.readValue(section, "SPCR_NativeTerminal", false, true);
		value.Target = null;
		value.TargetId = ini.readValue(section, "SPCR_CommandTargetId", 0, true);
		value.Finished = ini.readValue(section, "SPCR_UplinkFinished", false, true);
		value.ModeReleased = ini.readValue(section, "SPCR_NativeModeReleased", value.Finished && value.Automatic && !value.TerminalReleased, true);
		if (ini.readValue(section, "SPCR_HasReport", false, true))
		{
			value.Last = new Report
			{
				Position = new GeoPosition(ini.readValue(section, "SPCR_Lat", 0.0, true), ini.readValue(section, "SPCR_Lon", 0.0, true), ini.readValue(section, "SPCR_Height", 0.0, true)),
				Velocity = ini.readValue(section, "SPCR_Velocity", Vector3.zero, true, false),
				Time = GameTime.missionElapsedTime - (double)ini.readValue(section, "SPCR_Age", 0f, true)
			};
			value.HasReport = true;
		}
	}
}
