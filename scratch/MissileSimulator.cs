using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace SeaPower;

public static class MissileSimulator
{
	public class KinematicRangeResult : IComparable<KinematicRangeResult>
	{
		public float MaxRange;

		public float DistPercent;

		public float InterceptSpeed;

		public float InterceptTime;

		public bool LoftTooHigh;

		public bool TargetTooClose;

		public bool InRange
		{
			get
			{
				if (InterceptTime >= 0f)
				{
					return !LoftTooHigh;
				}
				return false;
			}
		}

		public int CompareTo(KinematicRangeResult other)
		{
			bool inRange = InRange;
			bool inRange2 = other.InRange;
			if (inRange != inRange2)
			{
				if (!inRange)
				{
					return -1;
				}
				return 1;
			}
			return other.InterceptTime.CompareTo(InterceptTime);
		}
	}

	public class FlyoutProfile
	{
		public readonly List<float> Times = new List<float>();

		public readonly List<float> FlatDists = new List<float>();

		public readonly List<float> SpeedsUnity = new List<float>();

		public readonly List<float> Alts = new List<float>();

		public readonly List<float> SegmentCos = new List<float>();

		public bool Stalled;

		public bool LoftTooHigh;

		public float EndTime
		{
			get
			{
				if (Times.Count <= 0)
				{
					return 0f;
				}
				return Times[Times.Count - 1];
			}
		}

		public float EndFlatDist
		{
			get
			{
				if (FlatDists.Count <= 0)
				{
					return 0f;
				}
				return FlatDists[FlatDists.Count - 1];
			}
		}

		public void AddNode(float t, float flat, float speedUnity, float alt, float segCos)
		{
			Times.Add(t);
			FlatDists.Add(flat);
			SpeedsUnity.Add(speedUnity);
			Alts.Add(alt);
			SegmentCos.Add(segCos);
		}

		private int SegmentIndexFor(float t)
		{
			int num = 0;
			int num2 = Times.Count - 1;
			while (num2 - num > 1)
			{
				int num3 = (num + num2) / 2;
				if (Times[num3] <= t)
				{
					num = num3;
				}
				else
				{
					num2 = num3;
				}
			}
			return num;
		}

		public float FlatDistAt(float t)
		{
			if (Times.Count == 0)
			{
				return 0f;
			}
			if (t <= Times[0])
			{
				return FlatDists[0];
			}
			if (t >= EndTime)
			{
				return EndFlatDist;
			}
			int num = SegmentIndexFor(t);
			float num2 = Times[num];
			float num3 = Times[num + 1] - num2;
			if (num3 < 1E-06f)
			{
				return FlatDists[num + 1];
			}
			float num4 = (t - num2) / num3;
			float num5 = SegmentCos[num + 1];
			float num6 = FlatDists[num];
			float num7 = FlatDists[num + 1];
			float num8 = SpeedsUnity[num] * num5 * num3;
			float num9 = SpeedsUnity[num + 1] * num5 * num3;
			float num10 = num4 * num4;
			float num11 = num10 * num4;
			return (2f * num11 - 3f * num10 + 1f) * num6 + (num11 - 2f * num10 + num4) * num8 + (-2f * num11 + 3f * num10) * num7 + (num11 - num10) * num9;
		}

		public float SpeedUnityAt(float t)
		{
			if (Times.Count == 0)
			{
				return 0f;
			}
			if (t <= Times[0])
			{
				return SpeedsUnity[0];
			}
			if (t >= EndTime)
			{
				return SpeedsUnity[SpeedsUnity.Count - 1];
			}
			int num = SegmentIndexFor(t);
			float num2 = Times[num + 1] - Times[num];
			if (num2 < 1E-06f)
			{
				return SpeedsUnity[num + 1];
			}
			float t2 = (t - Times[num]) / num2;
			return Mathf.Lerp(SpeedsUnity[num], SpeedsUnity[num + 1], t2);
		}

		public float AltAt(float t)
		{
			if (Times.Count == 0)
			{
				return 0f;
			}
			if (t <= Times[0])
			{
				return Alts[0];
			}
			if (t >= EndTime)
			{
				return Alts[Alts.Count - 1];
			}
			int num = SegmentIndexFor(t);
			float num2 = Times[num + 1] - Times[num];
			if (num2 < 1E-06f)
			{
				return Alts[num + 1];
			}
			float num3 = FlatDists[num + 1] - FlatDists[num];
			if (num3 < 1E-06f)
			{
				return Mathf.Lerp(Alts[num], Alts[num + 1], (t - Times[num]) / num2);
			}
			float t2 = Mathf.Clamp01((FlatDistAt(t) - FlatDists[num]) / num3);
			return Mathf.Lerp(Alts[num], Alts[num + 1], t2);
		}
	}

	public struct ChebyshevSeries
	{
		public float[] C;

		public float TMin;

		public float TMax;

		public float Eval(float t)
		{
			float num = (TMax - TMin) * 0.5f;
			if (num < 1E-09f)
			{
				return C[0] * 0.5f;
			}
			float num2 = Mathf.Clamp((t - TMin) / num - 1f, -1f, 1f);
			float num3 = 0f;
			float num4 = 0f;
			for (int num5 = C.Length - 1; num5 >= 1; num5--)
			{
				float num6 = 2f * num2 * num3 - num4 + C[num5];
				num4 = num3;
				num3 = num6;
			}
			return num2 * num3 - num4 + C[0] * 0.5f;
		}
	}

	public struct FleePerformance
	{
		public float MaxSpeedKnots;

		public float MaxGs;

		public float VelocityGain;

		public float EscapeAltUnity;

		public float DiveAngleDeg;
	}

	public class FleeingTargetPath
	{
		private readonly Vector2 _launchFlat;

		private readonly Vector2 _center;

		private readonly Vector2 _fromCenter;

		private readonly float _angSpeedSigned;

		private readonly float _turnTime;

		private readonly Vector2 _runStart;

		private readonly Vector2 _runDir;

		private readonly float _v0;

		private readonly float _vMax;

		private readonly float _gain;

		private readonly float _startAlt;

		private readonly float _sinDive;

		private readonly float _cosDive;

		private readonly float _divePathLen;

		public float TurnTime => _turnTime;

		public FleeingTargetPath(Vector3 launchPos, Vector3 targetPos, Vector3 targetVelocity, float maxSpeedUnity, float maxGs, float velocityGain, float escapeAltUnity, float diveAngleDeg)
		{
			_launchFlat = new Vector2(launchPos.x, launchPos.z);
			Vector2 vector = new Vector2(targetPos.x, targetPos.z);
			Vector2 vector2 = new Vector2(targetVelocity.x, targetVelocity.z);
			_v0 = vector2.magnitude;
			_vMax = Mathf.Max(maxSpeedUnity, 0.1f);
			_gain = Mathf.Max(velocityGain, 0.001f);
			_startAlt = Mathf.Max(targetPos.y, 0f);
			float num = Mathf.Clamp(escapeAltUnity, 0f, _startAlt);
			float f = Mathf.Clamp(diveAngleDeg, 1f, 89f) * (MathF.PI / 180f);
			_sinDive = Mathf.Sin(f);
			_cosDive = Mathf.Cos(f);
			_divePathLen = (_startAlt - num) / _sinDive;
			Vector2 vector3 = vector - _launchFlat;
			vector3 = ((vector3.sqrMagnitude > 1E-06f) ? vector3.normalized : ((_v0 > 0.0001f) ? (vector2 / _v0) : Vector2.up));
			float num2 = 0.14598154f * Mathf.Sqrt(Mathf.Max(maxGs * maxGs - 1f, 0.25f));
			Vector2 lhs = ((_v0 > 0.0001f) ? (vector2 / _v0) : vector3);
			float f2 = Mathf.Atan2(lhs.x * vector3.y - lhs.y * vector3.x, Vector2.Dot(lhs, vector3));
			if (_v0 > 0.0001f && Mathf.Abs(f2) > 0.001f)
			{
				float num3 = _v0 * _v0 / num2;
				float num4 = Mathf.Sign(f2);
				_center = vector + new Vector2(0f - lhs.y, lhs.x) * (num3 * num4);
				_fromCenter = vector - _center;
				_angSpeedSigned = num4 * _v0 / num3;
				_turnTime = Mathf.Abs(f2) * num3 / _v0;
				_runStart = _center + Rotate(_fromCenter, _angSpeedSigned * _turnTime);
			}
			else
			{
				_turnTime = 0f;
				_runStart = vector;
			}
			_runDir = vector3;
		}

		private static Vector2 Rotate(Vector2 v, float rad)
		{
			float num = Mathf.Cos(rad);
			float num2 = Mathf.Sin(rad);
			return new Vector2(num * v.x - num2 * v.y, num2 * v.x + num * v.y);
		}

		private float RunPathDistAt(float tr)
		{
			return _vMax * tr - (_vMax - _v0) * (1f - Mathf.Exp((0f - _gain) * tr)) / _gain;
		}

		public Vector2 FlatPositionAt(float t)
		{
			if (t < _turnTime)
			{
				return _center + Rotate(_fromCenter, _angSpeedSigned * t);
			}
			float num = RunPathDistAt(t - _turnTime);
			float num2 = ((num <= _divePathLen) ? (num * _cosDive) : (_divePathLen * _cosDive + (num - _divePathLen)));
			return _runStart + _runDir * num2;
		}

		public float AltAt(float t)
		{
			if (t < _turnTime || _divePathLen <= 0f)
			{
				return _startAlt;
			}
			float num = Mathf.Min(RunPathDistAt(t - _turnTime), _divePathLen);
			return _startAlt - num * _sinDive;
		}

		public float DistFromLaunchAt(float t)
		{
			return (FlatPositionAt(t) - _launchFlat).magnitude;
		}
	}

	private readonly struct LoftSearchKey : IEquatable<LoftSearchKey>
	{
		private readonly string _ammo;

		private readonly bool _air;

		private readonly int _drag;

		private readonly int _d0;

		private readonly int _launchAlt;

		private readonly int _targetAlt;

		private readonly int _altMin;

		private readonly int _rec;

		private readonly int _spd;

		private readonly int _altMax;

		public LoftSearchKey(string ammo, bool air, int drag, int d0, int launchAlt, int targetAlt, int altMin, int rec, int spd, int altMax)
		{
			_ammo = ammo;
			_air = air;
			_drag = drag;
			_d0 = d0;
			_launchAlt = launchAlt;
			_targetAlt = targetAlt;
			_altMin = altMin;
			_rec = rec;
			_spd = spd;
			_altMax = altMax;
		}

		public bool Equals(LoftSearchKey o)
		{
			if (_air == o._air && _drag == o._drag && _d0 == o._d0 && _launchAlt == o._launchAlt && _targetAlt == o._targetAlt && _altMin == o._altMin && _rec == o._rec && _spd == o._spd && _altMax == o._altMax)
			{
				return _ammo == o._ammo;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is LoftSearchKey o)
			{
				return Equals(o);
			}
			return false;
		}

		public override int GetHashCode()
		{
			HashCode hashCode = default(HashCode);
			hashCode.Add(_ammo);
			hashCode.Add(_air);
			hashCode.Add(_drag);
			hashCode.Add(_d0);
			hashCode.Add(_launchAlt);
			hashCode.Add(_targetAlt);
			hashCode.Add(_altMin);
			hashCode.Add(_rec);
			hashCode.Add(_spd);
			hashCode.Add(_altMax);
			return hashCode.ToHashCode();
		}
	}

	private const int CHEB_DEGREE = 12;

	private const int CHEB_SAMPLES = 33;

	private const float SUBDIVIDE_ALT_STEP = 25f;

	private const float LOW_SPEED_UNITY = 5f;

	private const float ALT_EPSILON = 0.01f;

	private static readonly ConcurrentDictionary<LoftSearchKey, float> _loftSearchCache = new ConcurrentDictionary<LoftSearchKey, float>();

	public const float INDUCED_POLAR = 0.7f;

	public const float INDUCED_POLAR_VREF = 2f;

	public const float ARRIVAL_RANGE_RESERVE = 1.1f;

	public const float INDUCED_POLAR_DENSITY = 0.4f;

	public static float LoftCap(AmmunitionParameters ap, float launchAltUnity, float targetAltUnity)
	{
		switch (ap._loftAltMode)
		{
		case AmmunitionParameters.LoftMode.Maintain:
		case AmmunitionParameters.LoftMode.Launcher:
			return Mathf.Max(launchAltUnity, 0f);
		case AmmunitionParameters.LoftMode.Target:
			return Mathf.Max(targetAltUnity, 0f);
		default:
			return ap._maxLoftAltUnity;
		}
	}

	public static ChebyshevSeries FitChebyshev(Func<float, float> f, float t0, float t1, int degree = 12, int samples = 33)
	{
		float num = (t0 + t1) * 0.5f;
		float num2 = (t1 - t0) * 0.5f;
		float[] array = new float[samples];
		for (int i = 0; i < samples; i++)
		{
			float num3 = Mathf.Cos(MathF.PI * ((float)i + 0.5f) / (float)samples);
			array[i] = f(num + num2 * num3);
		}
		float[] array2 = new float[degree + 1];
		for (int j = 0; j <= degree; j++)
		{
			float num4 = 0f;
			for (int k = 0; k < samples; k++)
			{
				num4 += array[k] * Mathf.Cos(MathF.PI * (float)j * ((float)k + 0.5f) / (float)samples);
			}
			array2[j] = 2f / (float)samples * num4;
		}
		ChebyshevSeries result = default(ChebyshevSeries);
		result.C = array2;
		result.TMin = t0;
		result.TMax = t1;
		return result;
	}

	private static float SpeedAtDist(float v0, float A, float k, float s)
	{
		double val;
		if (k < 1E-09f)
		{
			val = (double)v0 * (double)v0 + 2.0 * (double)A * (double)s;
		}
		else
		{
			double num = 2.0 * (double)k * (double)s;
			double num2 = Math.Exp(0.0 - num);
			double num3 = ((num < 1E-05) ? (num * (1.0 - 0.5 * num)) : (1.0 - num2));
			val = (double)v0 * (double)v0 * num2 + (double)A / (double)k * num3;
		}
		return (float)Math.Sqrt(Math.Max(val, 0.0001));
	}

	private static float TimeOverDist(float v0, float A, float k, float s)
	{
		if (s <= 0f)
		{
			return 0f;
		}
		if (k < 1E-09f && Mathf.Abs(A) < 1E-06f)
		{
			return s / Mathf.Max(v0, 0.01f);
		}
		float a = SpeedAtDist(v0, A, k, s * 0.5f);
		float num = 1f / Mathf.Max(v0, 0.01f) + 4f / Mathf.Max(SpeedAtDist(v0, A, k, s * 0.125f), 0.01f) + 2f / Mathf.Max(SpeedAtDist(v0, A, k, s * 0.25f), 0.01f) + 4f / Mathf.Max(SpeedAtDist(v0, A, k, s * 0.375f), 0.01f) + 1f / Mathf.Max(a, 0.01f);
		float num2 = s * 0.5f / 12f * num;
		float num3 = 1f / Mathf.Max(a, 0.01f) + 4f / Mathf.Max(SpeedAtDist(v0, A, k, s * 0.75f), 0.01f) + 1f / Mathf.Max(SpeedAtDist(v0, A, k, s), 0.01f);
		float num4 = s * 0.5f / 6f * num3;
		return num2 + num4;
	}

	private static float DistForTime(float v0, float A, float k, float tau, float sMax)
	{
		if (tau <= 0f)
		{
			return 0f;
		}
		float num = 0f;
		float num2 = sMax;
		for (int i = 0; i < 30; i++)
		{
			float num3 = (num + num2) * 0.5f;
			if (TimeOverDist(v0, A, k, num3) < tau)
			{
				num = num3;
			}
			else
			{
				num2 = num3;
			}
		}
		return (num + num2) * 0.5f;
	}

	private static float DistToSpeed(float v0, float A, float k, float vLimit)
	{
		double num = (double)vLimit * (double)vLimit;
		double num2 = (double)v0 * (double)v0;
		if (num2 <= num)
		{
			return 0f;
		}
		if (k < 1E-09f)
		{
			if (!(A >= 0f))
			{
				return (float)((num2 - num) / (-2.0 * (double)A));
			}
			return -1f;
		}
		double num3 = (double)A / (double)k;
		double num4 = num - num3;
		double num5 = num2 - num3;
		if (num4 <= 0.0 || num5 <= 0.0)
		{
			return -1f;
		}
		double num6 = num4 / num5;
		if (num6 >= 1.0)
		{
			return 0f;
		}
		return (float)((0.0 - Math.Log(num6)) / (2.0 * (double)k));
	}

	private static List<Vector2> BuildAltitudeNodes(AmmunitionParameters ap, float launchAlt, float targetAlt, float flatDistTotal, float loftAltOverride, out float loftEndFlatOut)
	{
		bool flag = ap._targetType == Ammunition.Target.AAW;
		List<Vector2> list = new List<Vector2>
		{
			new Vector2(0f, launchAlt)
		};
		loftEndFlatOut = 0f;
		if (ap.Kinematics == AmmunitionParameters.KinematicsLevel.None && !flag)
		{
			AppendNonKinematicStages(ap, list, launchAlt, targetAlt, flatDistTotal, loftAltOverride, out loftEndFlatOut);
			return list;
		}
		List<Vector4> list2 = new List<Vector4>();
		float num = 0f;
		float z = ((ap._finalFlightPhaseMaxAngle > 0.01f) ? ap._finalFlightPhaseMaxAngle : 10f);
		float z2 = ((ap._seaSkimmingMaxDescentAngle > 0.01f) ? ap._seaSkimmingMaxDescentAngle : 10f);
		float num2 = LoftCap(ap, launchAlt, targetAlt);
		bool flag2 = !ap._loftToSkim && num2 > 0f;
		if (ap._terminalDiveDistance > 0f && ap._terminalDiveDistance < ap._terminalApproachDist && !flag && !ap._terminalLoft)
		{
			num = ap._terminalDiveDistance;
		}
		if (ap._terminalPopUpDist > 0f)
		{
			num = ap._terminalPopUpDist;
		}
		if (ap._terminalApproachDist > 0f && !ap._terminalLoft)
		{
			float num3 = (flag ? targetAlt : ((ap._terminalAltUnity > 0f) ? ap._terminalAltUnity : ((ap._finalFlightPhaseDistToTargetUnity > 0f && ap._finalFlightPhaseAltUnity > 0f) ? ap._finalFlightPhaseAltUnity : ((!(ap._seaSkimmingStartDistToTargetUnity > 0f) || !(ap._seaSkimmingAltUnity > 0f)) ? (-1f) : ap._seaSkimmingAltUnity))));
			if (num3 >= 0f)
			{
				list2.Add(new Vector4(flatDistTotal - num, Mathf.Max(num3, 0f), z2, flatDistTotal - ap._terminalApproachDist));
				num = ap._terminalApproachDist;
			}
		}
		if (ap._finalFlightPhaseDistToTargetUnity > 0f)
		{
			list2.Add(new Vector4(flatDistTotal - num, Mathf.Max(ap._finalFlightPhaseAltUnity, 0f), z, flatDistTotal - ap._finalFlightPhaseDistToTargetUnity));
			num = ap._finalFlightPhaseDistToTargetUnity;
		}
		if (ap._seaSkimmingStartDistToTargetUnity > 0f && !flag2)
		{
			list2.Add(new Vector4(flatDistTotal - num, flag ? targetAlt : Mathf.Max(ap._seaSkimmingAltUnity, 0f), z2, flatDistTotal - ap._seaSkimmingStartDistToTargetUnity));
			num = ap._seaSkimmingStartDistToTargetUnity;
		}
		float num4 = flatDistTotal - num;
		loftEndFlatOut = Mathf.Max(num4, 0f);
		float num5 = ((num2 > 0f) ? Mathf.Max((loftAltOverride >= 0f) ? loftAltOverride : num2, launchAlt) : (-1f));
		if (num5 > 0f && num4 > 1f)
		{
			float num6 = Mathf.Max(targetAlt, 0f);
			float num7 = Mathf.Tan(Mathf.Clamp(ap._maxLoftAngle, 0.5f, 89f) * (MathF.PI / 180f));
			bool ballisticLoft = ap._ballisticLoft;
			float num8;
			float num11;
			if (ballisticLoft)
			{
				num8 = Mathf.Max(num5, launchAlt, num6);
				float num9 = Mathf.Sqrt(Mathf.Max(num8 - launchAlt, 0f));
				float num10 = Mathf.Sqrt(Mathf.Max(num8 - num6, 0f));
				num11 = ((num9 + num10 > 0.0001f) ? (num4 * Mathf.Clamp(num9 / (num9 + num10), 0f, 0.95f)) : 0f);
			}
			else
			{
				float num12 = Mathf.Tan(Mathf.Clamp((ap._finalFlightPhaseMaxAngle > 0.01f) ? ap._finalFlightPhaseMaxAngle : 45f, 0.5f, 89f) * (MathF.PI / 180f));
				float num13 = 1f / num7;
				float num14 = 1f / num12;
				float b2 = (num4 + launchAlt * num13 + num6 * num14) / (num13 + num14);
				num8 = Mathf.Max(Mathf.Min(num5, b2), Mathf.Max(launchAlt, num6));
				num11 = Mathf.Clamp((num8 - launchAlt) * num13, 0f, num4);
			}
			if (num11 > 0.5f && num8 > launchAlt + 0.5f)
			{
				int num15 = Mathf.Clamp(Mathf.CeilToInt((num8 - launchAlt) / 25f) + 1, 3, 12);
				for (int i = 1; i <= num15; i++)
				{
					float num16 = (float)i / (float)num15;
					float num17 = (ballisticLoft ? (1f - (1f - num16) * (1f - num16)) : ((0f - num16) * num16 * num16 + num16 * num16 + num16));
					list.Add(new Vector2(num11 * num16, launchAlt + (num8 - launchAlt) * num17));
				}
			}
			float x = list[list.Count - 1].x;
			if (num4 > x + 0.5f)
			{
				float y = list[list.Count - 1].y;
				float num18 = num4 - x;
				int num19 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(y - num6) / 25f) + 1, 3, 12);
				for (int j = 1; j <= num19; j++)
				{
					float num20 = (float)j / (float)num19;
					float num21 = (ballisticLoft ? (num20 * num20) : (num20 * num20 * num20));
					list.Add(new Vector2(x + num18 * num20, y + (num6 - y) * num21));
				}
			}
		}
		list2.Sort((Vector4 a, Vector4 b) => a.x.CompareTo(b.x));
		foreach (Vector4 item in list2)
		{
			Vector2 vector = list[list.Count - 1];
			if (item.x <= vector.x + 0.5f || item.x > flatDistTotal)
			{
				continue;
			}
			float num22 = item.y - vector.y;
			float num23 = ((Mathf.Abs(num22) > 0.01f) ? (Mathf.Abs(num22) / Mathf.Tan(Mathf.Clamp(item.z, 1f, 89f) * (MathF.PI / 180f))) : 0f);
			if (num23 > 0.5f)
			{
				float num24 = Mathf.Clamp(item.w, vector.x, item.x);
				if (num24 > vector.x + 0.5f)
				{
					list.Add(new Vector2(num24, vector.y));
				}
				if (num24 + num23 >= item.x)
				{
					if (flag)
					{
						list.Add(new Vector2(item.x, item.y));
					}
					else
					{
						list.Add(new Vector2(item.x, vector.y + num22 * Mathf.Clamp01((item.x - num24) / num23)));
					}
					continue;
				}
				list.Add(new Vector2(num24 + num23, item.y));
			}
			if (item.x > list[list.Count - 1].x + 0.5f)
			{
				list.Add(new Vector2(item.x, item.y));
			}
		}
		if (flatDistTotal > list[list.Count - 1].x + 0.5f)
		{
			list.Add(new Vector2(flatDistTotal, Mathf.Max(targetAlt, 0f)));
		}
		return list;
	}

	private static void AppendNonKinematicStages(AmmunitionParameters ap, List<Vector2> nodes, float launchAlt, float targetAlt, float flatDistTotal, float loftAltOverride, out float loftEndFlat)
	{
		float item = ((ap._seaSkimmingMaxDescentAngle > 0.01f) ? ap._seaSkimmingMaxDescentAngle : 10f);
		float item2 = ((ap._finalFlightPhaseMaxAngle > 0.01f) ? ap._finalFlightPhaseMaxAngle : 10f);
		float item3 = Mathf.Clamp(ap._maxLoftAngle, 1f, 89f);
		bool item4 = false;
		float a;
		if (ap._terminalAltUnity > 0f)
		{
			a = ap._terminalAltUnity;
		}
		else if (ap._finalFlightPhaseDistToTargetUnity > 0f && ap._finalFlightPhaseAltUnity > 0f)
		{
			a = ap._finalFlightPhaseAltUnity;
		}
		else if (ap._seaSkimmingStartDistToTargetUnity > 0f && ap._seaSkimmingAltUnity > 0f)
		{
			a = ap._seaSkimmingAltUnity;
		}
		else
		{
			a = targetAlt;
			item4 = true;
		}
		List<(float, float, float, bool)> list = new List<(float, float, float, bool)>();
		float num = LoftCap(ap, launchAlt, targetAlt);
		if (num > 0f)
		{
			list.Add((float.MaxValue, Mathf.Max((loftAltOverride >= 0f) ? loftAltOverride : num, 0f), item3, false));
		}
		if (ap._seaSkimmingStartDistToTargetUnity > 0f && (ap._loftToSkim || num <= 0f))
		{
			list.Add((ap._seaSkimmingStartDistToTargetUnity, Mathf.Max(ap._seaSkimmingAltUnity, 0f), item, false));
		}
		if (ap._finalFlightPhaseDistToTargetUnity > 0f)
		{
			list.Add((ap._finalFlightPhaseDistToTargetUnity, Mathf.Max(ap._finalFlightPhaseAltUnity, 0f), item2, false));
		}
		if (ap._terminalApproachDist > 0f && !ap._terminalLoft)
		{
			list.Add((ap._terminalApproachDist, Mathf.Max(a, 0f), item, item4));
		}
		loftEndFlat = 0f;
		bool flag = ap._terminalPopUpDist > 0f && !ap._selectivePopUp;
		float num2 = (flag ? Mathf.Max(flatDistTotal - ap._terminalPopUpDist, 0f) : flatDistTotal);
		float num3 = Mathf.Max(ap._maxVelocityInKnots, 1f) * 0.0076554087f * 3f;
		float num4 = 0f;
		float num5 = launchAlt;
		for (int i = 0; i < list.Count; i++)
		{
			bool flag2 = i == 0;
			float a2 = (flag2 ? 0f : Mathf.Clamp(flatDistTotal - list[i].Item1, 0f, num2));
			a2 = Mathf.Max(a2, num4);
			float a3 = ((i + 1 < list.Count) ? Mathf.Clamp(flatDistTotal - list[i + 1].Item1, 0f, num2) : num2);
			a3 = Mathf.Max(a3, a2);
			float num6 = list[i].Item2;
			if (list[i].Item4)
			{
				float num7 = Mathf.Max(a2, num4);
				if (a3 > num7 + 0.001f)
				{
					nodes.Add(new Vector2(a3, num6));
					num4 = a3;
					num5 = num6;
				}
				continue;
			}
			float num8 = Mathf.Tan(Mathf.Clamp(list[i].Item3, 1f, 89f) * (MathF.PI / 180f));
			bool flag3 = Mathf.Abs(num6 - num5) > 0.01f;
			float num9 = (flag3 ? (Mathf.Abs(num6 - num5) / num8) : 0f);
			if (flag3 && !flag2)
			{
				num9 = Mathf.Max(num9, num3);
			}
			if (flag2 && flag3)
			{
				a3 = Mathf.Clamp(Mathf.Max(a3, a2 + num9), a2, num2);
			}
			if (i == 0 && num > 0f)
			{
				loftEndFlat = a3;
			}
			if (a3 - a2 < 0.001f)
			{
				continue;
			}
			if (flag3)
			{
				if (num9 >= a3 - a2)
				{
					num6 = Mathf.Lerp(num5, num6, Mathf.Clamp01((a3 - a2) / num9));
					nodes.Add(new Vector2(a3, num6));
				}
				else
				{
					nodes.Add(new Vector2(a2 + num9, num6));
					if (a3 > a2 + num9 + 0.5f)
					{
						nodes.Add(new Vector2(a3, num6));
					}
				}
			}
			else
			{
				nodes.Add(new Vector2(a3, num5));
				num6 = num5;
			}
			num4 = a3;
			num5 = num6;
		}
		if (flag && flatDistTotal > num4 + 0.5f)
		{
			float num10 = Mathf.Tan(Mathf.Clamp((ap._terminalPopUpAngle > 0.01f) ? ap._terminalPopUpAngle : 45f, 1f, 89f) * (MathF.PI / 180f));
			float num11 = flatDistTotal - num4;
			float num12 = Mathf.Max(targetAlt, 0f);
			float a4 = num12 + Mathf.Max(ap._terminalPopUpHeight, 0f);
			float b = (num11 * num10 + num5 + num12) * 0.5f;
			float num13 = Mathf.Max(Mathf.Min(a4, b), Mathf.Max(num5, num12));
			float num14 = Mathf.Min(num4 + (num13 - num5) / num10, flatDistTotal);
			float num15 = Mathf.Max(flatDistTotal - (num13 - num12) / num10, num14);
			if (num13 > num5 + 0.01f)
			{
				nodes.Add(new Vector2(num14, num13));
			}
			if (num15 > num14 + 0.5f)
			{
				nodes.Add(new Vector2(num15, num13));
			}
			if (flatDistTotal > nodes[nodes.Count - 1].x + 0.5f)
			{
				nodes.Add(new Vector2(flatDistTotal, num12));
			}
			return;
		}
		float num16 = Mathf.Max(targetAlt, 0f);
		if (num5 - num16 > 0.01f && flatDistTotal > num4 + 0.001f)
		{
			float num17 = ((ap._terminalDiveDistance > 0f) ? ap._terminalDiveDistance : Mathf.Min(num3, flatDistTotal - num4));
			float num18 = Mathf.Clamp(flatDistTotal - num17, num4, flatDistTotal);
			if (num18 > num4 + 0.001f)
			{
				nodes.Add(new Vector2(num18, num5));
			}
			nodes.Add(new Vector2(flatDistTotal, num16));
		}
		else if (flatDistTotal > num4 + 0.5f)
		{
			nodes.Add(new Vector2(flatDistTotal, num5));
		}
	}

	public static FlyoutProfile BuildFlyout(AmmunitionParameters ap, float launchAlt, float targetAlt, float flatDistTotal, float startVelocityKnots, bool isAirUnit, float dragFactor, float loftAltOverride, float lifetime)
	{
		if (ap.Kinematics == AmmunitionParameters.KinematicsLevel.None || dragFactor < 0f)
		{
			return BuildFlyoutNonKinematic(ap, launchAlt, targetAlt, flatDistTotal, startVelocityKnots, loftAltOverride, lifetime);
		}
		float num = 0.0076554087f;
		lifetime = Mathf.Min(lifetime, 7200f);
		float loftEndFlatOut;
		List<Vector2> list = BuildAltitudeNodes(ap, launchAlt, targetAlt, flatDistTotal, loftAltOverride, out loftEndFlatOut);
		FlyoutProfile flyoutProfile = new FlyoutProfile();
		float num2 = ap.MinVelocity * num;
		float num3 = num2 * 2f * num2 * 2f;
		float num4 = 0.080000006f * dragFactor * num;
		float vLimit = (ap.MinVelocity + 200f) * num;
		(float, float) accelerationTimes = GetAccelerationTimes(ap, isAirUnit);
		float num5 = Mathf.Max(accelerationTimes.Item1, 0f);
		float num6 = num5 + Mathf.Max(accelerationTimes.Item2, 0f);
		float num7 = 22.882885f * num;
		float num8 = ap._acceleration * num7;
		float num9 = ((ap.Kinematics == AmmunitionParameters.KinematicsLevel.Full) ? (ap._sustainerBurnAcceleration * num7) : 0f);
		float num10 = 19.06907f * num;
		float num11 = Mathf.Max((float)Utils.CalculateAirDensity(Mathf.Max(targetAlt, 0f)) / 1.225f, 0.001f);
		float num12 = 0f;
		float num13 = Mathf.Max(startVelocityKnots, 0f) * num;
		float num14 = list[0].x;
		float num15 = list[0].y;
		flyoutProfile.AddNode(num12, num14, num13, num15, 1f);
		bool flag = false;
		for (int i = 1; i < list.Count; i++)
		{
			if (flag)
			{
				break;
			}
			float num16 = list[i].x - num14;
			float num17 = list[i].y - num15;
			if (num16 < 0.001f)
			{
				continue;
			}
			int num18 = ((!(num17 > 0.5f) && !(num17 < -0.5f)) ? 1 : Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(num17) / 25f), 1, 16));
			for (int j = 0; j < num18; j++)
			{
				if (flag)
				{
					break;
				}
				float num19 = num16 / (float)num18;
				float num20 = num17 / (float)num18;
				float num21 = Mathf.Sqrt(num19 * num19 + num20 * num20);
				if (num21 < 1E-06f)
				{
					continue;
				}
				float num22 = num19 / num21;
				float num23 = num20 / num21;
				float num24 = (float)Utils.CalculateAirDensity(Mathf.Max(num15 + num20 * 0.5f, 0f));
				float num25 = 0.2f * num24 * dragFactor * num;
				float num26 = num10 * num23;
				float num27 = Mathf.Sqrt(Mathf.Abs(num22)) * dragFactor * ap.LiftFactor * num10 / num11;
				float num28 = num21;
				int num29 = 0;
				while (num28 > 0.001f && !flag && num29++ < 400)
				{
					bool flag2 = num12 < num6 - 0.0001f;
					float num30;
					float num31;
					if (num12 < num5 - 0.0001f)
					{
						num30 = num8 - num26;
						num31 = num5;
					}
					else if (flag2)
					{
						num30 = num9 - num26;
						num31 = num6;
					}
					else
					{
						float num32 = num4 * 0.7f * num3 * Mathf.Min(num3 / Mathf.Max(num13 * num13, 1E-06f), 16f) * num22 * num22;
						num30 = 0f - (num26 + num27 + num32);
						num31 = lifetime;
					}
					if (flag2 && num13 < 5f && num30 > 0.01f)
					{
						float num33 = Mathf.Min((25f - num13 * num13) / (2f * num30), num28);
						float num34 = Mathf.Sqrt(num13 * num13 + 2f * num30 * num33);
						float num35 = (num34 - num13) / num30;
						if (num12 + num35 > num31)
						{
							num35 = num31 - num12;
							num33 = num13 * num35 + 0.5f * num30 * num35 * num35;
							num34 = num13 + num30 * num35;
						}
						num12 += num35;
						num13 = num34;
						num14 += num33 * num22;
						num15 += num33 * num23;
						num28 -= num33;
						flyoutProfile.AddNode(num12, num14, num13, num15, num22);
						continue;
					}
					float num36 = num28;
					bool flag3 = false;
					bool flag4 = false;
					if (!flag2)
					{
						float num37 = DistToSpeed(num13, num30, num25, num2);
						if (num37 >= 0f && num37 < num36)
						{
							num36 = Mathf.Max(num37, 0.01f);
							flag3 = true;
						}
						if (!flyoutProfile.LoftTooHigh && num15 - Mathf.Max(targetAlt, 0f) > 5f)
						{
							float num38 = DistToSpeed(num13, num30, num25, vLimit);
							if (num38 >= 0f && num38 < num36)
							{
								num36 = Mathf.Max(num38, 0.01f);
								flag4 = true;
								flag3 = false;
							}
						}
					}
					if (!flag2 && num25 > 1E-09f)
					{
						float num39 = Mathf.Abs(num30 / num25 - num13 * num13);
						if (num39 > 1E-06f)
						{
							float num40 = 0.25f * num13 * num13 / num39;
							if (num40 < 1f)
							{
								float num41 = (0f - Mathf.Log(1f - num40)) / (2f * num25);
								if (num41 < num36)
								{
									num36 = Mathf.Max(num41, 0.01f);
									flag3 = false;
									flag4 = false;
								}
							}
						}
					}
					float num42 = TimeOverDist(num13, num30, num25, num36);
					if (num12 + num42 > num31 && num31 - num12 > 0.001f && num31 < lifetime)
					{
						num36 = DistForTime(num13, num30, num25, num31 - num12, num36);
						num42 = num31 - num12;
						flag3 = false;
						flag4 = false;
					}
					if (num12 + num42 > lifetime)
					{
						num36 = DistForTime(num13, num30, num25, lifetime - num12, num36);
						num42 = lifetime - num12;
						flag = true;
					}
					float num43 = SpeedAtDist(num13, num30, num25, num36);
					num12 += num42;
					num13 = num43;
					num14 += num36 * num22;
					num15 += num36 * num23;
					num28 -= num36;
					flyoutProfile.AddNode(num12, num14, num13, num15, num22);
					if (flag4)
					{
						flyoutProfile.LoftTooHigh = true;
						int k;
						for (k = i; k < list.Count && list[k].x < loftEndFlatOut - 0.5f; k++)
						{
						}
						float num44 = ((k < list.Count) ? list[k].x : flatDistTotal);
						float num45 = ((k < list.Count) ? list[k].y : Mathf.Max(targetAlt, 0f));
						list.RemoveRange(i, k - i);
						float num46 = num44 - num14;
						if (num46 > 0.5f && num15 > num45 + 0.01f)
						{
							int num47 = Mathf.Clamp(Mathf.CeilToInt((num15 - num45) / 25f) + 1, 3, 12);
							for (int l = 1; l <= num47; l++)
							{
								float num48 = (float)l / (float)num47;
								if (k < list.Count && l == num47)
								{
									break;
								}
								list.Insert(i + l - 1, new Vector2(num14 + num46 * num48, num15 + (num45 - num15) * num48 * num48 * num48));
							}
						}
						num28 = 0f;
						j = num18;
						i--;
					}
					else if (flag3)
					{
						flyoutProfile.Stalled = true;
						flag = true;
					}
				}
			}
		}
		if (!flag)
		{
			float num49 = (float)Utils.CalculateAirDensity(Mathf.Max(num15, 0f));
			float num50 = 0.2f * num49 * dragFactor * num;
			float num51 = dragFactor * ap.LiftFactor * num10 / num11;
			int num52 = 0;
			while (!flag && num12 < lifetime && num52++ < 400)
			{
				bool flag5 = num12 < num6 - 0.0001f;
				float num53 = (flag5 ? 0f : (num4 * 0.7f * num3 * Mathf.Min(num3 / Mathf.Max(num13 * num13, 1E-06f), 16f)));
				float num54 = ((!flag5) ? (0f - (num51 + num53)) : ((num12 < num5 - 0.0001f) ? num8 : num9));
				float num55 = ((num12 < num5 - 0.0001f) ? num5 : (flag5 ? num6 : lifetime));
				float num56 = flatDistTotal * 2f + 1000f;
				bool flag6 = false;
				if (!flag5)
				{
					float num57 = DistToSpeed(num13, num54, num50, num2);
					if (num57 >= 0f && num57 < num56)
					{
						num56 = Mathf.Max(num57, 0.01f);
						flag6 = true;
					}
					if (num50 > 1E-09f)
					{
						float num58 = Mathf.Abs(num54 / num50 - num13 * num13);
						if (num58 > 1E-06f)
						{
							float num59 = 0.25f * num13 * num13 / num58;
							if (num59 < 1f)
							{
								float num60 = (0f - Mathf.Log(1f - num59)) / (2f * num50);
								if (num60 < num56)
								{
									num56 = Mathf.Max(num60, 0.01f);
									flag6 = false;
								}
							}
						}
					}
				}
				float num61 = TimeOverDist(num13, num54, num50, num56);
				if (num12 + num61 > num55 && num55 - num12 > 0.001f && num55 < lifetime)
				{
					num56 = DistForTime(num13, num54, num50, num55 - num12, num56);
					num61 = num55 - num12;
					flag6 = false;
				}
				if (num12 + num61 > lifetime)
				{
					num56 = DistForTime(num13, num54, num50, lifetime - num12, num56);
					num61 = lifetime - num12;
					flag = true;
				}
				if (num56 <= 0.001f && num61 <= 0.001f)
				{
					break;
				}
				num13 = SpeedAtDist(num13, num54, num50, num56);
				num12 += num61;
				num14 += num56;
				flyoutProfile.AddNode(num12, num14, num13, num15, 1f);
				if (flag6)
				{
					flyoutProfile.Stalled = true;
					flag = true;
				}
			}
		}
		return flyoutProfile;
	}

	private static FlyoutProfile BuildFlyoutNonKinematic(AmmunitionParameters ap, float launchAlt, float targetAlt, float flatDistTotal, float startVelocityKnots, float loftAltOverride, float lifetime)
	{
		float num = 0.0076554087f;
		FlyoutProfile flyoutProfile = new FlyoutProfile();
		lifetime = Mathf.Min(lifetime, 7200f);
		float num2 = ((ap._launchRangesInUnity.y > 0f) ? ap._launchRangesInUnity.y : float.MaxValue);
		float num3 = ap._acceleration * 9.81f * 1.94384f * 1.2f * num;
		float num4 = ap._deceleration * 9.8f * 1.94384f * num;
		float maxVelU = Mathf.Max(ap._maxVelocityInKnots, 1f) * num;
		float loftVelU = ((ap._maxLoftVelocityInKnots > 0f) ? (ap._maxLoftVelocityInKnots * num) : maxVelU);
		float termVelU = ((ap._terminalVelocityInKnots > 0f) ? (ap._terminalVelocityInKnots * num) : maxVelU);
		float loftEndFlat;
		List<Vector2> list = BuildAltitudeNodes(ap, launchAlt, targetAlt, flatDistTotal, loftAltOverride, out loftEndFlat);
		float num5 = ((num2 < float.MaxValue) ? num2 : (flatDistTotal + maxVelU * lifetime));
		if (num5 > list[list.Count - 1].x + 0.5f)
		{
			list.Add(new Vector2(num5, list[list.Count - 1].y));
		}
		float num6 = 0f;
		float num7 = list[0].x;
		float num8 = list[0].y;
		float num9 = Mathf.Max(startVelocityKnots, 0f) * num;
		flyoutProfile.AddNode(num6, num7, num9, num8, 1f);
		bool flag = false;
		for (int i = 1; i < list.Count; i++)
		{
			if (flag)
			{
				break;
			}
			float num10 = list[i].x - num7;
			float num11 = list[i].y - num8;
			if (num10 < 0.001f)
			{
				continue;
			}
			float num12 = Mathf.Sqrt(num10 * num10 + num11 * num11);
			float num13 = num10 / num12;
			float num14 = num11 / num12;
			float num15 = TargetSpeedForX(list[i].x);
			float num16 = num12;
			while (num16 > 0.001f && !flag)
			{
				float num17 = ((num9 < num15 - 0.01f) ? num3 : ((num9 > num15 + 0.01f) ? (0f - num4) : 0f));
				float num18 = num16;
				if (num7 + num18 * num13 > num2)
				{
					num18 = Mathf.Max((num2 - num7) / num13, 0.001f);
					flag = true;
				}
				float num21;
				float num22;
				if (Mathf.Abs(num17) > 0.001f)
				{
					float num19 = (num15 - num9) / num17;
					float num20 = num9 * num19 + 0.5f * num17 * num19 * num19;
					if (num20 < num18)
					{
						num18 = Mathf.Max(num20, 0.001f);
						num21 = num19;
						num22 = num15;
						flag = false;
					}
					else
					{
						num21 = (0f - num9 + Mathf.Sqrt(Mathf.Max(num9 * num9 + 2f * num17 * num18, 0f))) / num17;
						num22 = num9 + num17 * num21;
					}
				}
				else
				{
					num21 = num18 / Mathf.Max(num9, 0.01f);
					num22 = num9;
				}
				if (num6 + num21 > lifetime)
				{
					float num23 = lifetime - num6;
					num18 = Mathf.Max(num9 * num23 + 0.5f * num17 * num23 * num23, 0f);
					num21 = num23;
					num22 = num9 + num17 * num23;
					flag = true;
				}
				num6 += num21;
				num9 = num22;
				num7 += num18 * num13;
				num8 += num18 * num14;
				num16 -= num18;
				flyoutProfile.AddNode(num6, num7, num9, num8, num13);
				if (num7 >= num2 - 0.001f)
				{
					flag = true;
				}
			}
		}
		return flyoutProfile;
		float TargetSpeedForX(float x)
		{
			float num24 = flatDistTotal - x;
			if (num24 < 0f)
			{
				return maxVelU;
			}
			if (ap._terminalApproachDist > 0f && num24 <= ap._terminalApproachDist && !ap._terminalLoft)
			{
				return termVelU;
			}
			if (LoftCap(ap, launchAlt, targetAlt) > 0f && x <= loftEndFlat)
			{
				return loftVelU;
			}
			return maxVelU;
		}
	}

	private static FlyoutProfile BuildFlyoutForTarget(AmmunitionParameters ap, Vector3 launchPosition, float startVelocityKnots, Vector3 targetPosition, Vector3 targetVelocityVector, bool isAirUnit, float dragFactor, float lifetime, ref float loftAltOverride, out Vector3 aimPoint)
	{
		float num = launchPosition.FlatDistance(targetPosition);
		float num2 = Missile.CalculateMissileInterceptTimeAnalytical(ap, launchPosition, startVelocityKnots, targetPosition, targetVelocityVector, dragFactor).interceptTime;
		if (num2 < 0f)
		{
			num2 = Missile.CalculateMissileInterceptTimeSimple(ap, launchPosition, targetPosition, targetVelocityVector);
		}
		if (num2 < 0f)
		{
			num2 = num / Mathf.Max(ap._maxVelocityInKnots * 0.0076554087f, 1f);
		}
		num2 = Mathf.Clamp(num2, 0f, Mathf.Min(lifetime, 7200f));
		aimPoint = targetPosition + targetVelocityVector * num2;
		float flatDistTotal = Mathf.Max(launchPosition.FlatDistance(aimPoint), 1f);
		float num3 = Mathf.Max(aimPoint.y, 0f);
		float num4 = LoftCap(ap, Mathf.Max(launchPosition.y, 0f), num3);
		if (loftAltOverride < 0f && dragFactor >= 0f && num4 > 0f && ap.Kinematics != 0)
		{
			float num5 = Mathf.Max(Mathf.Max(launchPosition.y, 0f), num3);
			if (num4 > num5 + 0.5f)
			{
				loftAltOverride = SearchOptimalLoftAltitude(ap, launchPosition, targetPosition, targetVelocityVector, startVelocityKnots, isAirUnit, dragFactor, num5, num4);
			}
		}
		return BuildFlyout(ap, Mathf.Max(launchPosition.y, 0f), num3, flatDistTotal, startVelocityKnots, isAirUnit, dragFactor, loftAltOverride, lifetime);
	}

	public static KinematicRangeResult EstimateShot(AmmunitionParameters ap, Vector3 launchPosition, float startVelocityKnots, Vector3 targetVelocityVector, Vector3 targetPosition, bool isAirUnit, float dragFactorOverride = -1f, float timeoutOverride = -1f, float loftAltOverride = -1f, bool targetEvasiveManuver = true, List<Vector3> trajectoryOut = null, List<float> speedsOut = null, List<float> timesOut = null, bool arrivalMargin = true)
	{
		float KU = 0.0076554087f;
		float magnitude = targetVelocityVector.magnitude;
		if (magnitude > 0f && targetEvasiveManuver)
		{
			Vector3 vector = targetPosition - launchPosition;
			vector.y = 0f;
			targetVelocityVector += vector.normalized * (targetVelocityVector.magnitude * 0.8f);
			float magnitude2 = targetVelocityVector.magnitude;
			targetVelocityVector = targetVelocityVector.normalized * MathF.Min(magnitude2, magnitude);
		}
		float num = launchPosition.FlatDistance(targetPosition);
		if (num < 0.001f)
		{
			return new KinematicRangeResult
			{
				MaxRange = 1f,
				DistPercent = 1f,
				InterceptTime = 0f,
				InterceptSpeed = startVelocityKnots
			};
		}
		if (dragFactorOverride < -0.1f)
		{
			dragFactorOverride = ap.GetDragFactor(isAirUnit);
		}
		float num2 = ((ap._maxFlightTime > 0f) ? ap._maxFlightTime : 600f);
		if (timeoutOverride > 0f)
		{
			num2 = timeoutOverride;
		}
		float num3 = Missile.CalculateMissileInterceptTimeAnalytical(ap, launchPosition, startVelocityKnots, targetPosition, targetVelocityVector, dragFactorOverride).interceptTime;
		if (num3 < 0f)
		{
			num3 = Missile.CalculateMissileInterceptTimeSimple(ap, launchPosition, targetPosition, targetVelocityVector);
		}
		if (num3 < 0f)
		{
			num3 = num / Mathf.Max(ap._maxVelocityInKnots * KU, 1f);
		}
		num3 = Mathf.Clamp(num3, 0f, Mathf.Min(num2, 7200f));
		Vector3 b = targetPosition + targetVelocityVector * num3;
		Mathf.Max(launchPosition.FlatDistance(b), 1f);
		float num4 = Mathf.Max(b.y, 0f);
		float num5 = loftAltOverride;
		float num6 = LoftCap(ap, Mathf.Max(launchPosition.y, 0f), num4);
		if (loftAltOverride < 0f && dragFactorOverride >= 0f && num6 > 0f && ap.Kinematics != 0)
		{
			float num7 = Mathf.Max(Mathf.Max(launchPosition.y, 0f), num4);
			if (num6 > num7 + 0.5f)
			{
				loftAltOverride = SearchOptimalLoftAltitude(ap, launchPosition, targetPosition, targetVelocityVector, startVelocityKnots, isAirUnit, dragFactorOverride, num7, num6);
			}
		}
		loftAltOverride = num5;
		Vector3 aimPoint;
		FlyoutProfile flyoutProfile = BuildFlyoutForTarget(ap, launchPosition, startVelocityKnots, targetPosition, targetVelocityVector, isAirUnit, dragFactorOverride, num2, ref loftAltOverride, out aimPoint);
		if (flyoutProfile.EndTime <= 0.01f)
		{
			return new KinematicRangeResult
			{
				MaxRange = 0f,
				DistPercent = 0f,
				InterceptTime = -1f,
				InterceptSpeed = -1f,
				LoftTooHigh = flyoutProfile.LoftTooHigh
			};
		}
		ChebyshevSeries cheb = FitChebyshev(flyoutProfile.FlatDistAt, 0f, flyoutProfile.EndTime);
		Vector3 relFlat = new Vector3(targetPosition.x - launchPosition.x, 0f, targetPosition.z - launchPosition.z);
		Vector3 velFlat = new Vector3(targetVelocityVector.x, 0f, targetVelocityVector.z);
		Vector3 vector2 = ((relFlat.sqrMagnitude > 0.001f) ? relFlat.normalized : Vector3.forward);
		bool flag = Vector3.Dot(targetVelocityVector, vector2) < 0f;
		float endTime = flyoutProfile.EndTime;
		float num8 = -1f;
		float num9 = 0f;
		if (Gap(0f) <= 0f)
		{
			num8 = 0f;
		}
		else
		{
			for (int i = 1; i <= 48; i++)
			{
				float num10 = endTime * (float)i / 48f;
				float num11 = Gap(num10);
				if (num11 <= 0f)
				{
					float num12 = num9;
					float num13 = num10;
					for (int j = 0; j < 40; j++)
					{
						float num14 = (num12 + num13) * 0.5f;
						if (Gap(num14) > 0f)
						{
							num12 = num14;
						}
						else
						{
							num13 = num14;
						}
					}
					num8 = (num12 + num13) * 0.5f;
					break;
				}
				num9 = num10;
			}
		}
		bool flag2 = false;
		if (num8 >= 0f && ap.Kinematics != 0 && num8 >= BurnEndTime(ap, isAirUnit) && flyoutProfile.SpeedUnityAt(num8) < ap.MinVelocity * 1.1f * KU)
		{
			num8 = -1f;
			flag2 = true;
		}
		if (num8 >= 0f && arrivalMargin && ap.Kinematics != 0)
		{
			Vector3 targetPosition2 = launchPosition + vector2 * (num * 1.1f);
			targetPosition2.y = targetPosition.y;
			if (!(EstimateShot(ap, launchPosition, startVelocityKnots, targetVelocityVector, targetPosition2, isAirUnit, dragFactorOverride, timeoutOverride, num5, targetEvasiveManuver: false, null, null, null, arrivalMargin: false).InterceptSpeed > 0f))
			{
				num8 = -1f;
				flag2 = true;
			}
		}
		Vector3 dirFlat;
		FlyoutProfile plotProfile;
		ChebyshevSeries chebR;
		if (trajectoryOut != null || speedsOut != null || timesOut != null)
		{
			dirFlat = vector2;
			plotProfile = flyoutProfile;
			float num15 = ((num8 >= 0f) ? num8 : flyoutProfile.EndTime);
			if (num8 >= 0f)
			{
				Vector3 b2 = targetPosition + targetVelocityVector * num8;
				float flatDistTotal = Mathf.Max(launchPosition.FlatDistance(b2), 1f);
				FlyoutProfile flyoutProfile2 = BuildFlyout(ap, Mathf.Max(launchPosition.y, 0f), Mathf.Max(b2.y, 0f), flatDistTotal, startVelocityKnots, isAirUnit, dragFactorOverride, loftAltOverride, num2);
				if (flyoutProfile2.EndTime > 0.01f)
				{
					chebR = FitChebyshev(flyoutProfile2.FlatDistAt, 0f, flyoutProfile2.EndTime);
					float num16 = -1f;
					float num17 = 0f;
					if (GapR(0f) <= 0f)
					{
						num16 = 0f;
					}
					else
					{
						for (int k = 1; k <= 48; k++)
						{
							float num18 = flyoutProfile2.EndTime * (float)k / 48f;
							if (GapR(num18) <= 0f)
							{
								float num19 = num17;
								float num20 = num18;
								for (int l = 0; l < 40; l++)
								{
									float num21 = (num19 + num20) * 0.5f;
									if (GapR(num21) > 0f)
									{
										num19 = num21;
									}
									else
									{
										num20 = num21;
									}
								}
								num16 = (num19 + num20) * 0.5f;
								break;
							}
							num17 = num18;
						}
					}
					if (num16 >= 0f)
					{
						plotProfile = flyoutProfile2;
						num15 = num16;
					}
				}
			}
			int num22 = Mathf.Clamp(80 / Mathf.Max(plotProfile.Times.Count, 1), 1, 6);
			EmitSample(0f);
			for (int m = 1; m < plotProfile.Times.Count; m++)
			{
				float num23 = Mathf.Min(plotProfile.Times[m - 1], num15);
				float num24 = Mathf.Min(plotProfile.Times[m], num15);
				if (num24 <= num23 + 1E-06f)
				{
					if (num23 >= num15)
					{
						break;
					}
					continue;
				}
				for (int n = 1; n <= num22; n++)
				{
					EmitSample(Mathf.Lerp(num23, num24, (float)n / (float)num22));
				}
				if (num24 >= num15)
				{
					break;
				}
			}
		}
		if (num8 >= 0f)
		{
			float num25 = cheb.Eval(num8);
			float interceptSpeed = flyoutProfile.SpeedUnityAt(num8) / KU;
			float num26 = Mathf.Max(flyoutProfile.EndFlatDist - num25, 0f);
			Vector3 vector3 = relFlat + velFlat * num8;
			if (vector3.sqrMagnitude > 0.001f)
			{
				float num27 = Vector3.Dot(velFlat, vector3.normalized);
				if (num27 > 0f)
				{
					num26 = Mathf.Max(num26 - num27 * (flyoutProfile.EndTime - num8), 0f);
				}
			}
			bool targetTooClose = flag && num25 > num * 1.05f;
			float num28 = num + num26;
			return new KinematicRangeResult
			{
				MaxRange = num28,
				DistPercent = num28 / num,
				InterceptTime = num8,
				InterceptSpeed = interceptSpeed,
				TargetTooClose = targetTooClose
			};
		}
		float num29 = float.MaxValue;
		float num30 = 0f;
		for (int num31 = 0; num31 <= 48; num31++)
		{
			float num32 = endTime * (float)num31 / 48f;
			float num33 = Gap(num32);
			if (num33 < num29)
			{
				num29 = num33;
				num30 = num32;
			}
		}
		float num34 = Mathf.Max(num30 - endTime / 48f, 0f);
		float num35 = Mathf.Min(num30 + endTime / 48f, endTime);
		for (int num36 = 0; num36 < 20; num36++)
		{
			float num37 = num34 + (num35 - num34) / 3f;
			float num38 = num35 - (num35 - num34) / 3f;
			if (Gap(num37) < Gap(num38))
			{
				num35 = num38;
			}
			else
			{
				num34 = num37;
			}
		}
		num30 = (num34 + num35) * 0.5f;
		num29 = Mathf.Min(num29, Gap(num30));
		bool targetTooClose2 = flag && flyoutProfile.EndFlatDist > num * 1.05f;
		if (num30 < 0.2f)
		{
			return new KinematicRangeResult
			{
				MaxRange = 0f,
				DistPercent = 0f,
				InterceptTime = -1f,
				InterceptSpeed = -1f,
				TargetTooClose = targetTooClose2,
				LoftTooHigh = flyoutProfile.LoftTooHigh
			};
		}
		bool flag3 = ap.Kinematics != 0 && (flyoutProfile.SpeedUnityAt(num30) < ap.MinVelocity * 1.1f * KU || num30 >= endTime - 0.5f);
		float num39 = (flag2 ? (num / 1.1f) : ((num - Mathf.Max(num29, 0f)) / (flag3 ? 1.1f : 1f)));
		return new KinematicRangeResult
		{
			MaxRange = num39,
			DistPercent = num39 / MathF.Max(num, 1f),
			InterceptTime = num30,
			InterceptSpeed = -1f,
			TargetTooClose = targetTooClose2,
			LoftTooHigh = flyoutProfile.LoftTooHigh
		};
		void EmitSample(float ti)
		{
			Vector3 item = launchPosition + dirFlat * plotProfile.FlatDistAt(ti);
			item.y = plotProfile.AltAt(ti);
			trajectoryOut?.Add(item);
			speedsOut?.Add(plotProfile.SpeedUnityAt(ti) / KU);
			timesOut?.Add(ti);
		}
		float Gap(float time)
		{
			return TargetDist(time) - cheb.Eval(time);
		}
		float GapR(float time)
		{
			return TargetDist(time) - chebR.Eval(time);
		}
		float TargetDist(float time)
		{
			return (relFlat + velFlat * time).magnitude;
		}
	}

	public static float EstimateTimeToFlatDistance(AmmunitionParameters ap, Vector3 launchPosition, float startVelocityKnots, Vector3 aimPoint, bool isAirUnit, float flatDistance)
	{
		if (ap == null || flatDistance < 0f)
		{
			return -1f;
		}
		if (flatDistance <= 0.001f)
		{
			return 0f;
		}
		float dragFactor = ap.GetDragFactor(isAirUnit);
		float lifetime = ((ap._maxFlightTime > 0f) ? ap._maxFlightTime : 600f);
		float loftAltOverride = -1f;
		Vector3 aimPoint2;
		FlyoutProfile flyoutProfile = BuildFlyoutForTarget(ap, launchPosition, startVelocityKnots, aimPoint, Vector3.zero, isAirUnit, dragFactor, lifetime, ref loftAltOverride, out aimPoint2);
		if (flyoutProfile.EndTime <= 0.01f || flyoutProfile.EndFlatDist + 0.001f < flatDistance)
		{
			return -1f;
		}
		float num = 0f;
		float num2 = flyoutProfile.EndTime;
		for (int i = 0; i < 40; i++)
		{
			float num3 = (num + num2) * 0.5f;
			if (flyoutProfile.FlatDistAt(num3) < flatDistance)
			{
				num = num3;
			}
			else
			{
				num2 = num3;
			}
		}
		return (num + num2) * 0.5f;
	}

	public static float EstimateFlatDistanceAtTime(AmmunitionParameters ap, Vector3 launchPosition, float startVelocityKnots, Vector3 aimPoint, bool isAirUnit, float time)
	{
		if (ap == null || time <= 0f)
		{
			return 0f;
		}
		float dragFactor = ap.GetDragFactor(isAirUnit);
		float lifetime = ((ap._maxFlightTime > 0f) ? ap._maxFlightTime : 600f);
		float loftAltOverride = -1f;
		Vector3 aimPoint2;
		FlyoutProfile flyoutProfile = BuildFlyoutForTarget(ap, launchPosition, startVelocityKnots, aimPoint, Vector3.zero, isAirUnit, dragFactor, lifetime, ref loftAltOverride, out aimPoint2);
		if (flyoutProfile.EndTime <= 0.01f)
		{
			return 0f;
		}
		return flyoutProfile.FlatDistAt(time);
	}

	public static FleePerformance GetFleePerformance(ObjectBase target)
	{
		FleePerformance result;
		if (target is Aircraft aircraft)
		{
			result = default(FleePerformance);
			result.MaxSpeedKnots = ((aircraft.Ap._maxSpeedSeaLevel > 0f) ? aircraft.Ap._maxSpeedSeaLevel : target._velocityInKnots);
			result.MaxGs = Mathf.Max(aircraft.Ap._maxG, 1.5f);
			result.VelocityGain = ((aircraft.Ap._velocityGain > 0f) ? aircraft.Ap._velocityGain : 0.1136f);
			float[] presetAltitudes = aircraft.Ap._presetAltitudes;
			result.EscapeAltUnity = ((presetAltitudes != null && presetAltitudes.Length > 0) ? aircraft.Ap._presetAltitudes[0] : 0.907142f);
			result.DiveAngleDeg = ((Mathf.Abs(aircraft.Ap._maxCombatDescentPitch) > 1f) ? Mathf.Abs(aircraft.Ap._maxCombatDescentPitch) : 30f);
			return result;
		}
		if (target is Helicopter helicopter)
		{
			result = default(FleePerformance);
			result.MaxSpeedKnots = Mathf.Max(helicopter._maxSpeedSeaLevel, target._velocityInKnots);
			result.MaxGs = 2f;
			result.VelocityGain = 0.1136f;
			result.EscapeAltUnity = 0.453571f;
			result.DiveAngleDeg = 10f;
			return result;
		}
		result = default(FleePerformance);
		result.MaxSpeedKnots = Mathf.Max(target.MaxForwardSpeedInKnots, target._velocityInKnots);
		result.MaxGs = 3f;
		result.VelocityGain = 0.1136f;
		result.EscapeAltUnity = 0.907142f;
		result.DiveAngleDeg = 15f;
		return result;
	}

	public static KinematicRangeResult EstimateShotNoEscape(AmmunitionParameters ap, Vector3 launchPosition, float startVelocityKnots, Vector3 targetPosition, Vector3 targetVelocityVector, float targetMaxSpeedKnots, float targetMaxGs, float targetVelocityGain, float targetEscapeAltUnity, float targetDiveAngleDeg, bool isAirUnit, float dragFactorOverride = -1f, bool arrivalMargin = true)
	{
		float num = 0.0076554087f;
		float num2 = launchPosition.FlatDistance(targetPosition);
		if (num2 < 0.001f)
		{
			return new KinematicRangeResult
			{
				MaxRange = 1f,
				DistPercent = 1f,
				InterceptTime = 0f,
				InterceptSpeed = startVelocityKnots
			};
		}
		if (dragFactorOverride < -0.1f)
		{
			dragFactorOverride = ap.GetDragFactor(isAirUnit);
		}
		float num3 = ((ap._maxFlightTime > 0f) ? ap._maxFlightTime : 600f);
		FleeingTargetPath flee = new FleeingTargetPath(launchPosition, targetPosition, targetVelocityVector, targetMaxSpeedKnots * num, targetMaxGs, targetVelocityGain, targetEscapeAltUnity, targetDiveAngleDeg);
		Vector3 normalized = new Vector3(targetPosition.x - launchPosition.x, 0f, targetPosition.z - launchPosition.z).normalized;
		Vector3 vector = normalized * (targetMaxSpeedKnots * num);
		float num4 = Missile.CalculateMissileInterceptTimeAnalytical(ap, launchPosition, startVelocityKnots, targetPosition, vector, dragFactorOverride).interceptTime;
		if (num4 < 0f)
		{
			num4 = Missile.CalculateMissileInterceptTimeSimple(ap, launchPosition, targetPosition, vector);
		}
		if (num4 < 0f)
		{
			num4 = num2 / Mathf.Max(ap._maxVelocityInKnots * num, 1f);
		}
		num4 = Mathf.Clamp(num4, 0f, Mathf.Min(num3, 7200f));
		float num5 = Mathf.Max(flee.DistFromLaunchAt(num4), 1f);
		float num6 = Mathf.Max(flee.AltAt(num4), 0f);
		float loftAltOverride = -1f;
		float num7 = LoftCap(ap, Mathf.Max(launchPosition.y, 0f), num6);
		if (dragFactorOverride >= 0f && num7 > 0f && ap.Kinematics != 0)
		{
			float num8 = Mathf.Max(Mathf.Max(launchPosition.y, 0f), num6);
			if (num7 > num8 + 0.5f)
			{
				Vector3 targetPos = targetPosition;
				targetPos.y = Mathf.Clamp(targetEscapeAltUnity, 0f, Mathf.Max(targetPosition.y, 0f));
				loftAltOverride = SearchOptimalLoftAltitude(ap, launchPosition, targetPos, vector, startVelocityKnots, isAirUnit, dragFactorOverride, num8, num7);
			}
		}
		int num9 = 0;
		FlyoutProfile flyoutProfile;
		float num16;
		while (true)
		{
			flyoutProfile = BuildFlyout(ap, Mathf.Max(launchPosition.y, 0f), num6, num5, startVelocityKnots, isAirUnit, dragFactorOverride, loftAltOverride, num3);
			if (flyoutProfile.EndTime <= 0.01f)
			{
				return new KinematicRangeResult
				{
					MaxRange = 0f,
					DistPercent = 0f,
					InterceptTime = -1f,
					InterceptSpeed = -1f,
					LoftTooHigh = flyoutProfile.LoftTooHigh
				};
			}
			ChebyshevSeries cheb = FitChebyshev(flyoutProfile.FlatDistAt, 0f, flyoutProfile.EndTime);
			float endTime = flyoutProfile.EndTime;
			float num10 = -1f;
			float num11 = 0f;
			for (int i = 1; i <= 48; i++)
			{
				float num12 = endTime * (float)i / 48f;
				if (Gap(num12) <= 0f)
				{
					float num13 = num11;
					float num14 = num12;
					for (int j = 0; j < 40; j++)
					{
						float num15 = (num13 + num14) * 0.5f;
						if (Gap(num15) > 0f)
						{
							num13 = num15;
						}
						else
						{
							num14 = num15;
						}
					}
					num10 = (num13 + num14) * 0.5f;
					break;
				}
				num11 = num12;
			}
			if (num10 >= 0f && ap.Kinematics != 0 && num10 >= BurnEndTime(ap, isAirUnit) && flyoutProfile.SpeedUnityAt(num10) < ap.MinVelocity * 1.1f * num)
			{
				num10 = -1f;
			}
			if (num10 >= 0f)
			{
				if (arrivalMargin && ap.Kinematics != 0)
				{
					Vector3 targetPosition2 = launchPosition + normalized * (num2 * 1.1f);
					targetPosition2.y = targetPosition.y;
					KinematicRangeResult kinematicRangeResult = EstimateShotNoEscape(ap, launchPosition, startVelocityKnots, targetPosition2, targetVelocityVector, targetMaxSpeedKnots, targetMaxGs, targetVelocityGain, targetEscapeAltUnity, targetDiveAngleDeg, isAirUnit, dragFactorOverride, arrivalMargin: false);
					if (!(kinematicRangeResult.InterceptTime >= 0f) || !(kinematicRangeResult.InterceptSpeed > 0f))
					{
						return new KinematicRangeResult
						{
							MaxRange = num2 / 1.1f,
							DistPercent = 0.9090909f,
							InterceptTime = -1f,
							InterceptSpeed = -1f,
							LoftTooHigh = flyoutProfile.LoftTooHigh
						};
					}
				}
				return new KinematicRangeResult
				{
					MaxRange = num2,
					DistPercent = 1f,
					InterceptTime = num10,
					InterceptSpeed = flyoutProfile.SpeedUnityAt(num10) / num
				};
			}
			num16 = float.MaxValue;
			float t = 0f;
			for (int k = 0; k <= 48; k++)
			{
				float num17 = endTime * (float)k / 48f;
				float num18 = Gap(num17);
				if (num18 < num16)
				{
					num16 = num18;
					t = num17;
				}
			}
			float num19 = Mathf.Max(flee.DistFromLaunchAt(t), 1f);
			if (num9 >= 2 || Mathf.Abs(num19 - num5) < 1f)
			{
				break;
			}
			num5 = num19;
			num6 = Mathf.Max(flee.AltAt(t), 0f);
			num9++;
			continue;
			float Gap(float time)
			{
				return flee.DistFromLaunchAt(time) - cheb.Eval(time);
			}
			float Gap(float time)
			{
				return flee.DistFromLaunchAt(time) - cheb.Eval(time);
			}
		}
		float num20 = num2 - Mathf.Max(num16, 0f);
		return new KinematicRangeResult
		{
			MaxRange = Mathf.Max(num20, 0f),
			DistPercent = num20 / MathF.Max(num2, 1f),
			InterceptTime = -1f,
			InterceptSpeed = -1f,
			LoftTooHigh = flyoutProfile.LoftTooHigh
		};
	}

	public static float CalculateThrustOverTime(AmmunitionParameters ap, bool isAirLaunched, float timeSinceLaunch, float timeWindow)
	{
		float num = -1f;
		float num2 = ap._acceleration * 9.81f * 1.94384f * 1.2f;
		(float, float) accelerationTimes = GetAccelerationTimes(ap, isAirLaunched);
		if (ap.Kinematics == AmmunitionParameters.KinematicsLevel.None && ap._accelerationTime == 6f)
		{
			accelerationTimes.Item1 = 9999999f;
		}
		if (ap.Kinematics == AmmunitionParameters.KinematicsLevel.Full)
		{
			num = ap._sustainerBurnAcceleration * 9.81f * 1.94384f * 1.2f;
		}
		float num3 = accelerationTimes.Item1 - timeSinceLaunch;
		float num4 = accelerationTimes.Item2 + accelerationTimes.Item1 - timeSinceLaunch;
		if (num3 < 1E-05f && num4 <= 0.0001f)
		{
			return 0f;
		}
		if (num3 < 0f && num4 > 0f)
		{
			if (num4 < timeWindow)
			{
				return num4 * timeWindow;
			}
			return timeWindow * num;
		}
		if (num3 < timeWindow)
		{
			float num5 = (timeWindow - num3) * num;
			float num6 = num2 * num3;
			if (num5 > 0f)
			{
				num6 += num5;
			}
			return num6;
		}
		return num2 * timeWindow;
	}

	public static float BurnEndTime(AmmunitionParameters ap, bool isAirLaunched)
	{
		(float, float) accelerationTimes = GetAccelerationTimes(ap, isAirLaunched);
		return Mathf.Max(accelerationTimes.Item1, 0f) + Mathf.Max(accelerationTimes.Item2, 0f);
	}

	public static (float primaryTime, float secondaryTIme) GetAccelerationTimes(AmmunitionParameters ap, bool isAirLaunched)
	{
		float item = -1f;
		float item2;
		if (ap.Kinematics == AmmunitionParameters.KinematicsLevel.Full)
		{
			item2 = ap._accelerationTime;
			item = ap._sustainerBurnTime;
		}
		else
		{
			float num = (isAirLaunched ? 1000f : 0f);
			item2 = (ap._maxVelocityInKnots - num) / (ap._acceleration * 9.81f * 1.94384f * 1.2f);
		}
		return (item2, item);
	}

	public static float EffectiveChaseRange(FlyoutProfile p, float recessionSpeedUnity)
	{
		if (recessionSpeedUnity <= 0.001f)
		{
			return p.EndFlatDist;
		}
		float num = 0f;
		List<float> times = p.Times;
		List<float> flatDists = p.FlatDists;
		List<float> speedsUnity = p.SpeedsUnity;
		List<float> segmentCos = p.SegmentCos;
		for (int i = 1; i < times.Count; i++)
		{
			float num2 = flatDists[i] - recessionSpeedUnity * times[i];
			if (num2 > num)
			{
				num = num2;
			}
			float num3 = speedsUnity[i - 1] * segmentCos[i];
			float num4 = speedsUnity[i] * segmentCos[i];
			if ((num3 - recessionSpeedUnity) * (num4 - recessionSpeedUnity) < 0f && Mathf.Abs(num4 - num3) > 0.0001f)
			{
				float t = (recessionSpeedUnity - num3) / (num4 - num3);
				float num5 = Mathf.Lerp(times[i - 1], times[i], t);
				float num6 = p.FlatDistAt(num5) - recessionSpeedUnity * num5;
				if (num6 > num)
				{
					num = num6;
				}
			}
		}
		return num;
	}

	public static float SearchOptimalLoftAltitude(AmmunitionParameters ap, Vector3 launchPos, Vector3 targetPos, Vector3 targetVel, float platformSpeedKnots, bool isAirUnit, float dragFactor, float altMin, float altMax)
	{
		if (altMax <= altMin + 0.5f)
		{
			return altMax;
		}
		if (!ap._optimizeLoftAlt)
		{
			return altMax;
		}
		Vector3 vector = new Vector3(targetPos.x - launchPos.x, 0f, targetPos.z - launchPos.z);
		float magnitude = vector.magnitude;
		Vector3 rhs = ((magnitude > 0.001f) ? (vector / magnitude) : Vector3.forward);
		float num = Vector3.Dot(new Vector3(targetVel.x, 0f, targetVel.z), rhs);
		float num2 = Mathf.Max(launchPos.y, 0f);
		float num3 = Mathf.Max(targetPos.y, 0f);
		int num4 = Mathf.RoundToInt(Mathf.Log(Mathf.Max(magnitude, 1f)) * 32f);
		int num5 = Mathf.RoundToInt(num2 * 0.5f);
		int num6 = Mathf.RoundToInt(num3 * 0.5f);
		int num7 = Mathf.RoundToInt(altMin * 0.5f);
		int num8 = Mathf.RoundToInt(num * 20f);
		int num9 = Mathf.RoundToInt(platformSpeedKnots * 0.04f);
		LoftSearchKey key = new LoftSearchKey(ap._ammunitionFileName, isAirUnit, Mathf.RoundToInt(dragFactor * 1000f), num4, num5, num6, num7, num8, num9, Mathf.RoundToInt(altMax * 4f));
		if (_loftSearchCache.TryGetValue(key, out var value))
		{
			return Mathf.Clamp(value, altMin, altMax);
		}
		if (_loftSearchCache.Count > 20000)
		{
			_loftSearchCache.Clear();
		}
		float altMin2 = Mathf.Min((float)num7 * 2f, altMax);
		float value2 = SearchOptimalLoftAltitudeCore(ap, (float)num5 * 2f, (float)num6 * 2f, Mathf.Exp((float)num4 / 32f), (float)num8 * 0.05f, (float)num9 * 25f, isAirUnit, dragFactor, altMin2, altMax);
		_loftSearchCache[key] = value2;
		return Mathf.Clamp(value2, altMin, altMax);
	}

	public static void ClearLoftSearchCache()
	{
		_loftSearchCache.Clear();
	}

	private static float SearchOptimalLoftAltitudeCore(AmmunitionParameters ap, float launchAlt, float targetAlt, float d0, float recession, float platformSpeedKnots, bool isAirUnit, float dragFactor, float altMin, float altMax)
	{
		if (altMax <= altMin + 0.5f)
		{
			return altMax;
		}
		float lifetime = ((ap._maxFlightTime > 0f) ? ap._maxFlightTime : 600f);
		float aimDist = Mathf.Max(d0, 1f);
		int num = 0;
		float num2 = float.MinValue;
		float num3 = 0f;
		for (int i = 0; i <= 8; i++)
		{
			float num4 = REff(Mathf.Lerp(altMin, altMax, (float)i / 8f));
			if (i == 0)
			{
				num3 = num4;
			}
			if (num4 > num2)
			{
				num2 = num4;
				num = i;
			}
		}
		float num5 = Mathf.Lerp(altMin, altMax, (float)Mathf.Max(num - 1, 0) / 8f);
		float num6 = Mathf.Lerp(altMin, altMax, (float)Mathf.Min(num + 1, 8) / 8f);
		float num7 = num6 - (num6 - num5) * 0.618034f;
		float num8 = num5 + (num6 - num5) * 0.618034f;
		float num9 = REff(num7);
		float num10 = REff(num8);
		for (int j = 0; j < 8; j++)
		{
			if (num9 >= num10)
			{
				num6 = num8;
				num8 = num7;
				num10 = num9;
				num7 = num6 - (num6 - num5) * 0.618034f;
				num9 = REff(num7);
			}
			else
			{
				num5 = num7;
				num7 = num8;
				num9 = num10;
				num8 = num5 + (num6 - num5) * 0.618034f;
				num10 = REff(num8);
			}
		}
		float num11 = ((num9 >= num10) ? num7 : num8);
		float num12 = Mathf.Max(num9, num10);
		float value = targetAlt + d0 * 0.5f * 0.176f;
		float num13 = d0 * 1.1f;
		if (num12 >= num13 && num11 > altMin + 0.5f)
		{
			float num14 = altMin;
			if (num3 < num13)
			{
				float num15 = altMin;
				float num16 = num11;
				for (int k = 0; k < 8; k++)
				{
					float num17 = (num15 + num16) * 0.5f;
					if (REff(num17) >= num13)
					{
						num16 = num17;
					}
					else
					{
						num15 = num17;
					}
				}
				num14 = num16;
			}
			float num18 = Mathf.Clamp(value, num14, num11);
			if (num18 > num14 + 0.5f && num18 < num11 - 0.5f && REff(num18) < num13)
			{
				num18 = num11;
			}
			return num18;
		}
		return num11;
		float REff(float loftAlt)
		{
			return EffectiveChaseRange(BuildFlyout(ap, launchAlt, targetAlt, aimDist, platformSpeedKnots, isAirUnit, dragFactor, loftAlt, lifetime), recession);
		}
	}

	public static Vector3 ComputePN(Vector3 missileVel, Vector3 relPos, Vector3 relVel, float N)
	{
		float magnitude = relPos.magnitude;
		if (magnitude < 0.001f)
		{
			return Vector3.zero;
		}
		Vector3 vector = relPos / magnitude;
		Vector3 vector2 = (relVel - Vector3.Dot(relVel, vector) * vector) / magnitude;
		float num = 0f - Vector3.Dot(relVel, vector);
		return N * num * vector2;
	}

	private static float LiftLoadFactor(float pitchDeg, float pitchRateDegPerSec, float velocityUnity)
	{
		float num = Mathf.Abs(Mathf.Cos(pitchDeg * (MathF.PI / 180f)));
		float num2 = Mathf.Max(pitchRateDegPerSec, 0f) * (MathF.PI / 180f);
		float num3 = velocityUnity * 67.200066f;
		return Mathf.Clamp01(num - num3 * num2 / 9.81f);
	}

	public static float CalculateDrag(float altitude, float velocity, float time, float pitch, float dragFactor, bool motorBurning, float targetAltitude = 0f, float liftFactor = 0.005f, float stallSpeedKnots = 0f, float pitchRateDegPerSec = 0f)
	{
		float aeroDragKnots;
		float inducedDragKnots;
		float parallelGKnots;
		return CalculateDrag(altitude, velocity, time, pitch, dragFactor, liftFactor, motorBurning, targetAltitude, out aeroDragKnots, out inducedDragKnots, out parallelGKnots, stallSpeedKnots, pitchRateDegPerSec);
	}

	public static float CalculateDrag(float altitude, float velocity, float time, float pitch, float dragFactor, float liftFactor, bool motorBurning, float targetAltitude, out float aeroDragKnots, out float inducedDragKnots, out float parallelGKnots, float stallSpeedKnots = 0f, float pitchRateDegPerSec = 0f)
	{
		float num = (float)Utils.CalculateAirDensity(altitude) * velocity * velocity * 0.2f * time * dragFactor;
		if (!motorBurning && stallSpeedKnots > 0f && velocity > 0.001f)
		{
			float num2 = stallSpeedKnots * 2f * 0.0076554087f;
			float num3 = num2 * num2;
			float num4 = Mathf.Min(num3 / (velocity * velocity), 16f);
			float num5 = LiftLoadFactor(pitch, pitchRateDegPerSec, velocity);
			num += 0.4f * num3 * num4 * 0.2f * time * dragFactor * 0.7f * num5 * num5;
		}
		float num6 = MathF.PI / 180f * pitch;
		float num7 = 9.81f;
		float num8 = num7 * Mathf.Sin(0f - num6);
		float num9 = 0f;
		if (!motorBurning)
		{
			float num10 = Mathf.Max((float)Utils.CalculateAirDensity(targetAltitude) / 1.225f, 0.001f);
			num9 = MathF.Sqrt(MathF.Abs(Mathf.Cos(num6))) * dragFactor * liftFactor * num7 / num10;
		}
		float num11 = (num9 + num8) * 1.94384f * time;
		aeroDragKnots = num;
		inducedDragKnots = num9 * 1.94384f * time;
		parallelGKnots = num8 * 1.94384f * time;
		return num + num11;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
