using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SeaPower;

public class Waypoint
{
	public enum HeightType
	{
		Absolute,
		RelativeToTarget,
		PercentageOfTarget,
		AboveTerrain,
		AboveSeaLevel,
		AboveLayer,
		PercentFromLastWaypoint,
		OnLineToTarget
	}

	public enum ToHeightType
	{
		Immediate,
		AggressiveArc,
		SlowArc,
		Linear,
		Loft,
		KappaLoft
	}

	private struct LoftParams
	{
		public float HPeak;

		public float MidpointPercent;

		public float StartHeight;

		public float EndHeight;

		public float StartSlopeNormalized;

		public bool Ballistic;
	}

	public struct Context
	{
		public GeoPosition GeoPosition;

		public Vector3 UnityPosition;

		public Vector3 VelocityVector;

		public float FlatVelocity;

		public float StallSpeedKnots;

		public bool MotorBurning;

		public GeoPosition LastWaypoint;

		public static Context FromObjectBase(ObjectBase obj)
		{
			Missile missile = obj as Missile;
			(missile?.CurrentIntendedTargetObject?._velocityVecInUnity.y).GetValueOrDefault();
			return new Context
			{
				GeoPosition = obj._geoPosition,
				UnityPosition = obj.transform.position,
				VelocityVector = obj._velocityVecInUnity,
				FlatVelocity = new Vector2(obj._velocityVecInUnity.x, obj._velocityVecInUnity.z).magnitude,
				StallSpeedKnots = ((missile != null) ? missile._ap.MinVelocity : 0f),
				MotorBurning = (missile?.IsMotorBurning ?? false)
			};
		}
	}

	public class Settings
	{
		public HeightSettings EndHeight;

		public AltitudeLimits HeightLimits;

		public HeightSettings? LoftHeight;

		public AltitudeLimits LoftHeightLimits;

		public ToHeightType PitchMode = ToHeightType.Linear;

		public HeightConstraints Constraints;

		public float TargetSpeedKnots;

		public bool SkipNextWaypointOnComplete;

		public bool AllowBackwardsProgress;

		public bool RequiresTargetToProceed;

		public bool AllowExceedingAngleLimits = true;

		public bool TrueBallistic;
	}

	public struct HeightSettings
	{
		public float Altitude;

		public HeightType HeightType;
	}

	public struct AltitudeLimits
	{
		public HeightSettings[] MinHeight;

		public HeightSettings[] MaxHeight;
	}

	public struct HeightConstraints
	{
		public float ClimbAngle;

		public float DescentAngle;

		public float? StartAngle;

		public float? EndAngle;

		public float ClimbAggressiveness;
	}

	public enum ArcType
	{
		PseudoBallistic
	}

	public struct Status
	{
		public bool Done;

		public GeoPosition DesiredPosition;

		public GeoPosition FuturePosition;
	}

	public class AngularWaypoint : IWaypoint
	{
		public float Angle;

		public float Distance;

		public AngularWaypoint(float angle, float distance)
		{
			Angle = angle;
			Distance = distance;
		}

		public GeoPosition GetPosition(GeoPosition targetPosition, Waypoint wp, GeoPosition lastWaypoint)
		{
			GeoPosition geoPosition = targetPosition.GeoOffsetByDistanceOnHeading(Distance * 0.036285132f, Angle);
			double desiredHeight = wp.GetDesiredHeight(targetPosition, geoPosition);
			geoPosition._height = desiredHeight;
			return geoPosition;
		}
	}

	public class FixedWaypoint : IWaypoint
	{
		public GeoPosition Position;

		public FixedWaypoint(GeoPosition position)
		{
			Position = position;
		}

		public GeoPosition GetPosition(GeoPosition targetPosition, Waypoint wp, GeoPosition lastWaypoint)
		{
			double desiredHeight = wp.GetDesiredHeight(targetPosition, Position);
			return new GeoPosition(Position.Latitude, Position.Longitude, desiredHeight);
		}
	}

	public class TargetRelativeWaypoint : IWaypoint
	{
		public double LatOffset;

		public double LonOffset;

		public TargetRelativeWaypoint(double latOffset, double lonOffset)
		{
			LatOffset = latOffset;
			LonOffset = lonOffset;
		}

		public GeoPosition GetPosition(GeoPosition targetPosition, Waypoint wp, GeoPosition lastWaypoint)
		{
			double height = wp.GetDesiredHeight(waypointHorizontalPos: new GeoPosition(targetPosition.Latitude + LatOffset, targetPosition.Longitude + LonOffset), targetPos: targetPosition);
			return new GeoPosition(targetPosition.Latitude + LatOffset, targetPosition.Longitude + LonOffset, height);
		}
	}

	public class PercentToWaypointWaypoint : IWaypoint
	{
		public float Percent;

		public Waypoint TargetWaypoint;

		public bool FlatDistance = true;

		public PercentToWaypointWaypoint(float percent, Waypoint targetWaypoint, GeoPosition startPoint, bool flatDistance = true)
		{
			Percent = percent;
			TargetWaypoint = targetWaypoint;
			FlatDistance = flatDistance;
		}

		public GeoPosition GetPosition(GeoPosition targetPosition, Waypoint wp, GeoPosition lastWaypoint)
		{
			Vector3 vector = TargetWaypoint.Wp.GetPosition(targetPosition, TargetWaypoint, lastWaypoint).ToUnity();
			Vector3 vector2 = lastWaypoint.ToUnity();
			Vector3 vector3 = vector - vector2;
			if (FlatDistance)
			{
				vector3.y = 0f;
			}
			Vector3 position = vector2 + vector3 * Percent;
			GeoPosition value = new GeoPosition(position);
			float y = (float)wp.GetDesiredHeight(targetPosition, value);
			position.y = y;
			return new GeoPosition(position);
		}
	}

	public class HeightOnlyWaypoint : IWaypoint
	{
		public GeoPosition GetPosition(GeoPosition targetPosition, Waypoint wp, GeoPosition lastWaypoint)
		{
			return targetPosition;
		}
	}

	public class DistanceToTargetWaypoint : IWaypoint
	{
		public float Distance;

		public bool FlatDistance;

		public DistanceToTargetWaypoint(float distance, bool flatDistance = true)
		{
			Distance = distance;
			FlatDistance = flatDistance;
		}

		public GeoPosition GetPosition(GeoPosition targetPosition, Waypoint wp, GeoPosition lastWaypoint)
		{
			Vector3 vector = targetPosition.ToUnity();
			Vector3 vector2 = wp._context.GeoPosition.ToUnity() - vector;
			if (FlatDistance)
			{
				vector2.y = 0f;
			}
			vector2 = vector + vector2.normalized * Distance;
			vector2.y = (float)wp.GetDesiredHeight(waypointHorizontalPos: new GeoPosition(vector2), targetPos: targetPosition);
			return new GeoPosition(vector2);
		}
	}

	public interface IWaypoint
	{
		GeoPosition GetPosition(GeoPosition targetPosition, Waypoint wp, GeoPosition lastWaypoint);
	}

	public IWaypoint Wp;

	public Settings WpSettings;

	private ObjectBase _objBase;

	public Status WpStatus;

	private double _percentageDone;

	private bool _terminalLock;

	private double _totalDist = 1.0;

	private double _currMinHeight = double.MinValue;

	private double _currMaxHeight = double.MaxValue;

	private Context _context;

	public Waypoint(IWaypoint wp, ObjectBase obj, Settings settings)
	{
		Wp = wp;
		_objBase = obj;
		WpSettings = settings;
		if (obj != null)
		{
			_context = Context.FromObjectBase(obj);
		}
	}

	public Waypoint(IWaypoint wp, Settings settings, Context context)
	{
		Wp = wp;
		_objBase = null;
		WpSettings = settings;
		_context = context;
	}

	public void UpdateContext(Context context)
	{
		_context = context;
	}

	public void InvalidateLoftCache()
	{
	}

	public void UpdateContextFromObject()
	{
		if (_objBase != null)
		{
			_context = Context.FromObjectBase(_objBase);
		}
	}

	public Status UpdateWaypoint(GeoPosition pos, GeoPosition lastWaypoint, List<Waypoint> waypoints, float simulationDt = -1f)
	{
		float num = ((simulationDt > 0f) ? simulationDt : GameTime.fixedDeltaTime);
		if (_objBase != null)
		{
			_context = Context.FromObjectBase(_objBase);
			foreach (Waypoint waypoint in waypoints)
			{
				if (waypoint != this && waypoint._objBase != null)
				{
					waypoint._context = Context.FromObjectBase(waypoint._objBase);
				}
				else if (waypoint != this)
				{
					waypoint._context = _context;
				}
			}
		}
		_context.LastWaypoint = lastWaypoint;
		GeoPosition position = Wp.GetPosition(pos, this, lastWaypoint);
		if (Overshot(lastWaypoint, position, pos))
		{
			WpStatus.Done = true;
			return WpStatus;
		}
		HeightType heightType = WpSettings.EndHeight.HeightType;
		double distance;
		double num2;
		if (heightType == HeightType.RelativeToTarget || heightType == HeightType.PercentageOfTarget)
		{
			distance = pos.GetDistance(_context.GeoPosition);
			num2 = position.GetDistance(_context.GeoPosition);
		}
		else if (WpSettings.EndHeight.HeightType == HeightType.OnLineToTarget)
		{
			distance = pos.GetDistance(_context.GeoPosition);
			num2 = distance;
		}
		else
		{
			distance = position.GetDistance(_context.GeoPosition);
			num2 = distance;
		}
		double distance2 = lastWaypoint.GetDistance(_context.GeoPosition);
		double val = distance2 + num2;
		_totalDist = Math.Max(val, 0.001);
		if (num2 <= 0.0 || distance <= 0.0)
		{
			_percentageDone = 1.0;
			WpStatus.Done = true;
		}
		else
		{
			double val2 = distance2 / _totalDist;
			double num3 = 2.5 * (double)_context.FlatVelocity * (double)num / _totalDist;
			_percentageDone = Math.Max(_percentageDone, Math.Min(val2, _percentageDone + num3));
		}
		Vector3 position2 = CalculateFuturePointRecursive(1.5f, distance, num2, position, lastWaypoint, pos, waypoints, 0);
		Vector3 position3 = CalculateFuturePointRecursive(8f, distance, num2, position, lastWaypoint, pos, waypoints, 0);
		WpStatus.FuturePosition = new GeoPosition(position3);
		WpStatus.DesiredPosition = new GeoPosition(position2);
		return WpStatus;
	}

	private Vector3 CalculateFuturePointRecursive(float lookAheadTime, double distToTarget, double remainingDist, GeoPosition currentWaypointPos, GeoPosition lastWaypointPos, GeoPosition targetPos, List<Waypoint> waypoints, int currentWaypointIndex)
	{
		float num = ((_context.FlatVelocity > 0.001f) ? ((float)(remainingDist / (double)_context.FlatVelocity)) : float.MaxValue);
		if (lookAheadTime <= num || currentWaypointIndex >= waypoints.Count - 1)
		{
			Waypoint waypoint = waypoints[currentWaypointIndex];
			Vector3 value = ((currentWaypointIndex == 0) ? _context.UnityPosition : lastWaypointPos.ToUnity());
			if (currentWaypointIndex > 0)
			{
				lastWaypointPos._height = value.y;
			}
			return waypoint.CalculatePointAtTime(MathF.Min(lookAheadTime, num), distToTarget, remainingDist, currentWaypointPos, lastWaypointPos, targetPos, value);
		}
		float lookAheadTime2 = lookAheadTime - num;
		int num2 = currentWaypointIndex + 1;
		Waypoint waypoint2 = waypoints[num2];
		GeoPosition position = waypoint2.Wp.GetPosition(targetPos, waypoint2, currentWaypointPos);
		double distance = currentWaypointPos.GetDistance(position);
		Vector3 vector = currentWaypointPos.ToUnity();
		return CalculateFuturePointRecursive(lookAheadTime2, distance, distance, position, new GeoPosition(new Vector3(vector.x, _context.UnityPosition.y + num * _context.VelocityVector.y, vector.z)), targetPos, waypoints, num2);
	}

	private double GetProgressAtTime(float time, Vector3 targetPos)
	{
		Vector3 vector = (targetPos - _context.UnityPosition).normalized * _context.VelocityVector.magnitude;
		vector.y = 0f;
		float num = vector.magnitude * time;
		return Math.Clamp(_percentageDone + (double)num / _totalDist, 0.0, 1.0);
	}

	public Vector3 CalculatePointAtTime(float lookAheadTime, double distToTarget, double remainingDist, GeoPosition waypointPos, GeoPosition lastWaypoint, GeoPosition targetPos, Vector3? currentPos = null)
	{
		Vector3 vector = currentPos ?? _context.UnityPosition;
		waypointPos.ToUnity();
		float num = lookAheadTime;
		float num2 = _context.FlatVelocity * lookAheadTime;
		if ((double)num2 > remainingDist)
		{
			num = (float)((double)lookAheadTime * remainingDist / (double)num2);
		}
		num2 = _context.FlatVelocity * num;
		double num3 = Math.Min(_percentageDone + (1.0 - _percentageDone) * ((double)num2 / remainingDist), 1.0);
		double heightAtPoint = GetHeightAtPoint(waypointPos, lastWaypoint, targetPos, WpSettings.PitchMode, num3);
		Vector3 vector2 = targetPos.ToUnity();
		Vector3 result = Vector3.Lerp(new Vector3(vector.x, 0f, vector.z), new Vector3(vector2.x, 0f, vector2.z), (float)((double)num2 / remainingDist));
		result.y = (float)heightAtPoint;
		if (!WpSettings.AllowExceedingAngleLimits)
		{
			float y = vector.y;
			float num4 = result.y - y;
			float num5 = Vector2.Distance(new Vector2(vector.x, vector.z), new Vector2(result.x, result.z));
			if (num5 > 0.001f && num3 < 0.949999988079071)
			{
				float num6 = Mathf.Atan2(num4, num5) * 57.29578f;
				if (num4 > 0f && WpSettings.Constraints.ClimbAngle > 0.001f)
				{
					if (num6 > WpSettings.Constraints.ClimbAngle)
					{
						float num7 = num5 * Mathf.Tan(WpSettings.Constraints.ClimbAngle * (MathF.PI / 180f));
						result.y = y + num7;
					}
				}
				else if (num4 < 0f && WpSettings.Constraints.DescentAngle > 0.001f && 0f - num6 > WpSettings.Constraints.DescentAngle)
				{
					float num8 = num5 * Mathf.Tan(WpSettings.Constraints.DescentAngle * (MathF.PI / 180f));
					result.y = y - num8;
				}
			}
		}
		return result;
	}

	private bool Overshot(GeoPosition lastWaypoint, GeoPosition nextPoint, GeoPosition target)
	{
		double distance = lastWaypoint.GetDistance(_context.GeoPosition);
		double distance2 = lastWaypoint.GetDistance(nextPoint);
		if (!WpSettings.AllowBackwardsProgress)
		{
			float num = target.ToUnity().FlatDistance(nextPoint.ToUnity());
			float num2 = target.ToUnity().FlatDistance(_context.UnityPosition);
			if (num > num2)
			{
				return true;
			}
		}
		return distance > distance2;
	}

	private double GetDesiredHeight(GeoPosition targetPos, GeoPosition? waypointHorizontalPos = null)
	{
		HeightSettings heightSettings = WpSettings.EndHeight;
		AltitudeLimits limits = WpSettings.HeightLimits;
		if (WpSettings.LoftHeight.HasValue && WpSettings.PitchMode != ToHeightType.Loft && WpSettings.PitchMode != ToHeightType.KappaLoft)
		{
			limits = WpSettings.LoftHeightLimits;
			heightSettings = WpSettings.LoftHeight.Value;
		}
		double height = GetHeight(targetPos, heightSettings.Altitude, heightSettings.HeightType, waypointHorizontalPos);
		(double, double) heightLimits = GetHeightLimits(targetPos, limits);
		return Math.Min(Math.Max(heightLimits.Item1, height), heightLimits.Item2);
	}

	public (double min, double max) GetHeightLimits(GeoPosition targetPos, AltitudeLimits limits)
	{
		double num = double.MinValue;
		if (limits.MinHeight != null && limits.MinHeight.Any())
		{
			num = limits.MinHeight.Select((HeightSettings x) => GetHeight(targetPos, x.Altitude, x.HeightType)).Max();
		}
		double num2 = double.MaxValue;
		if (limits.MaxHeight != null && limits.MaxHeight.Any())
		{
			num2 = limits.MaxHeight.Select((HeightSettings x) => GetHeight(targetPos, x.Altitude, x.HeightType)).Min();
		}
		_currMinHeight = num;
		_currMaxHeight = num2;
		return (min: num, max: num2);
	}

	public double GetHeight(GeoPosition targetPosition, float height, HeightType type)
	{
		return GetHeight(targetPosition, height, type, null);
	}

	public double GetHeight(GeoPosition targetPosition, float height, HeightType type, GeoPosition? waypointHorizontalPosition)
	{
		return type switch
		{
			HeightType.Absolute => height, 
			HeightType.RelativeToTarget => targetPosition._height + (double)height, 
			HeightType.PercentageOfTarget => targetPosition._height * (double)height, 
			HeightType.AboveTerrain => (_objBase != null) ? TerrainUtils.GetSafeAltitudeOverSurface(_objBase, height, height) : height, 
			HeightType.AboveSeaLevel => (_objBase != null) ? TerrainUtils.GetSafeAltitudeOverSea(_objBase, height, height) : height, 
			HeightType.AboveLayer => (Singleton<Environment>.Instance != null) ? (Singleton<Environment>.Instance.getLayerHeight(_context.GeoPosition) + height) : height, 
			HeightType.OnLineToTarget => CalculateHeightOnLineToTarget(targetPosition, waypointHorizontalPosition), 
			_ => throw new ArgumentOutOfRangeException("type", type, null), 
		};
	}

	private double CalculateHeightOnLineToTarget(GeoPosition targetPosition, GeoPosition? waypointPosition)
	{
		return targetPosition._height;
	}

	private LoftParams GetOrComputeLoftParams(GeoPosition aimpoint, GeoPosition lastWaypoint)
	{
		HeightConstraints constraints = WpSettings.Constraints;
		float num = (float)lastWaypoint._height;
		float num2 = (float)aimpoint._height;
		(double, double) heightLimits = GetHeightLimits(aimpoint, WpSettings.LoftHeightLimits);
		float num3 = _context.VelocityVector.magnitude / 0.0076554087f;
		if (!_context.MotorBurning && _context.StallSpeedKnots > 0f && num3 < _context.StallSpeedKnots + 200f && num2 < _context.GeoPosition.y)
		{
			heightLimits.Item2 = _context.GeoPosition.y;
			heightLimits.Item1 = Math.Min(heightLimits.Item2, heightLimits.Item1);
		}
		float num4 = (float)Math.Clamp(GetHeight(aimpoint, WpSettings.LoftHeight.Value.Altitude, WpSettings.LoftHeight.Value.HeightType), heightLimits.Item1, heightLimits.Item2);
		Vector3 vector = lastWaypoint.ToUnity();
		Vector3 vector2 = aimpoint.ToUnity();
		float magnitude = new Vector2(vector2.x - vector.x, vector2.z - vector.z).magnitude;
		float num5 = MathF.Tan(Mathf.Clamp(constraints.ClimbAngle, 1E-06f, 89.9999f) * (MathF.PI / 180f));
		float num6 = MathF.Tan(Mathf.Clamp(constraints.DescentAngle, 1E-06f, 89.9999f) * (MathF.PI / 180f));
		float num7 = 1f / num5;
		float num8 = 1f / num6;
		if (WpSettings.TrueBallistic)
		{
			float num9 = Mathf.Max(num4, num, num2);
			float num10 = MathF.Sqrt(Mathf.Max(num9 - num, 0f));
			float num11 = MathF.Sqrt(Mathf.Max(num9 - num2, 0f));
			float midpointPercent = ((num10 + num11 > 0.0001f) ? Mathf.Clamp(num10 / (num10 + num11), 0f, 0.95f) : 0.5f);
			return new LoftParams
			{
				HPeak = num9,
				MidpointPercent = midpointPercent,
				StartHeight = num,
				EndHeight = num2,
				StartSlopeNormalized = 0f,
				Ballistic = true
			};
		}
		float b = (magnitude + num * num7 + num2 * num8) / (num7 + num8);
		float num12 = Mathf.Max(Mathf.Min(num4, b), num, num2);
		float num13 = (num12 - num) * num7;
		float num14 = (num12 - num2) * num8;
		float num15 = MathF.Min(0.95f, num13 / magnitude);
		float a = MathF.Min(0.95f, num13 / MathF.Max(0.1f, num13 + num14));
		float midpointPercent2;
		if (num4 > vector2.y)
		{
			midpointPercent2 = num15;
		}
		else
		{
			float num16 = vector2.y - num;
			float t = ((num16 > 0.001f) ? Mathf.Clamp01((num4 - num) / num16) : 1f);
			midpointPercent2 = Mathf.Lerp(a, num15, t);
		}
		float value = 0f;
		float num17 = Mathf.Max(0f, Mathf.Min(constraints.StartAngle.GetValueOrDefault(), constraints.ClimbAngle));
		if (num17 > 0.001f && constraints.ClimbAngle > 0.001f)
		{
			value = MathF.Tan(num17 * (MathF.PI / 180f)) / MathF.Tan(Mathf.Clamp(constraints.ClimbAngle, 0.001f, 89f) * (MathF.PI / 180f));
		}
		return new LoftParams
		{
			HPeak = num12,
			MidpointPercent = midpointPercent2,
			StartHeight = num,
			EndHeight = num2,
			StartSlopeNormalized = Mathf.Clamp01(value)
		};
	}

	private static float ComputeTotalDistLoft(GeoPosition aimpoint, GeoPosition lastWaypoint)
	{
		Vector3 vector = lastWaypoint.ToUnity();
		Vector3 vector2 = aimpoint.ToUnity();
		return new Vector2(vector2.x - vector.x, vector2.z - vector.z).magnitude;
	}

	private static float BallisticHeightAt(float percent, float startHeight, float hPeak, float endHeight, float midpointPercent, float startSlopeNormalized = 0f, bool trueBallistic = false)
	{
		float num = Mathf.Max(midpointPercent, 1E-05f);
		if (trueBallistic)
		{
			if (percent < midpointPercent)
			{
				float num2 = 1f - percent / num;
				return hPeak - (hPeak - startHeight) * num2 * num2;
			}
			float num3 = (percent - midpointPercent) / Mathf.Max(1f - midpointPercent, 1E-05f);
			return hPeak - (hPeak - endHeight) * num3 * num3;
		}
		if (percent < midpointPercent)
		{
			float num4 = percent / num;
			float num5 = MathF.Max(1f, startSlopeNormalized);
			float num6 = num4 * num4 * (3f - 2f * num4) + num5 * num4 * (1f - num4) * (1f - num4);
			return startHeight + (hPeak - startHeight) * num6;
		}
		float num7 = Mathf.Max(1f - midpointPercent, 1E-05f);
		float num8 = (percent - midpointPercent) / num7;
		float num9 = 3f * (endHeight - hPeak);
		float num10 = num8 * num8;
		float num11 = num10 * num8;
		return (2f * num11 - 3f * num10 + 1f) * hPeak + (-2f * num11 + 3f * num10) * endHeight + (num11 - num10) * num9;
	}

	private float GetBallisticAltitude(GeoPosition aimpoint, GeoPosition lastWaypoint, GeoPosition targetPoint, double progress)
	{
		LoftParams orComputeLoftParams = GetOrComputeLoftParams(aimpoint, lastWaypoint);
		return BallisticHeightAt((float)progress, orComputeLoftParams.StartHeight, orComputeLoftParams.HPeak, orComputeLoftParams.EndHeight, orComputeLoftParams.MidpointPercent, orComputeLoftParams.StartSlopeNormalized, orComputeLoftParams.Ballistic);
	}

	private float GetKappaLoftAltitude(GeoPosition aimpoint, GeoPosition lastWaypoint, GeoPosition targetPoint, double progress)
	{
		LoftParams orComputeLoftParams = GetOrComputeLoftParams(aimpoint, lastWaypoint);
		float num = ComputeTotalDistLoft(aimpoint, lastWaypoint);
		float num2 = (float)_percentageDone;
		float num3 = (float)(progress - _percentageDone);
		float num4 = BallisticHeightAt(num2, orComputeLoftParams.StartHeight, orComputeLoftParams.HPeak, orComputeLoftParams.EndHeight, orComputeLoftParams.MidpointPercent, orComputeLoftParams.StartSlopeNormalized, orComputeLoftParams.Ballistic);
		float f = Mathf.Atan2(BallisticHeightAt(num2 + num3, orComputeLoftParams.StartHeight, orComputeLoftParams.HPeak, orComputeLoftParams.EndHeight, orComputeLoftParams.MidpointPercent, orComputeLoftParams.StartSlopeNormalized, orComputeLoftParams.Ballistic) - num4, num3 * num);
		float num5 = Mathf.Max((float)(progress - _percentageDone) * num, 0.001f);
		float a = num4 + num5 * Mathf.Tan(f);
		float num6 = Mathf.Clamp(max: Mathf.Max((1f - num2) * num, 0.001f), value: _context.FlatVelocity * 8f, min: num5);
		float num7 = BallisticHeightAt(Mathf.Min(num2 + num6 / num, 1f), orComputeLoftParams.StartHeight, orComputeLoftParams.HPeak, orComputeLoftParams.EndHeight, orComputeLoftParams.MidpointPercent, orComputeLoftParams.StartSlopeNormalized, orComputeLoftParams.Ballistic);
		float f2 = Mathf.Min(Mathf.Atan2(_context.UnityPosition.y - num7, num6), MathF.PI * 89f / 180f);
		float b = _context.UnityPosition.y - num5 * Mathf.Tan(f2);
		return Mathf.Max(a, b);
	}

	public double GetHeightAtPoint(GeoPosition aimpoint, GeoPosition lastWaypoint, GeoPosition targetPoint, ToHeightType toHeightType, double percentToPoint)
	{
		double num = aimpoint._height - lastWaypoint._height;
		return toHeightType switch
		{
			ToHeightType.Immediate => aimpoint._height, 
			ToHeightType.AggressiveArc => lastWaypoint._height + Math.Sqrt(percentToPoint) * num, 
			ToHeightType.SlowArc => lastWaypoint._height + Math.Pow(percentToPoint, 2.0) * num, 
			ToHeightType.Linear => lastWaypoint._height + percentToPoint * num, 
			ToHeightType.Loft => GetBallisticAltitude(aimpoint, lastWaypoint, targetPoint, percentToPoint), 
			ToHeightType.KappaLoft => GetKappaLoftAltitude(aimpoint, lastWaypoint, targetPoint, percentToPoint), 
			_ => throw new ArgumentOutOfRangeException("toHeightType", toHeightType, null), 
		};
	}

	public static void UpdateAndGetActiveWaypoint(ref List<Waypoint> waypoints, ref GeoPosition lastWaypoint, GeoPosition targetPoint)
	{
		while (waypoints.Count > 0)
		{
			Waypoint waypoint = waypoints[0];
			if (!waypoint.UpdateWaypoint(targetPoint, lastWaypoint, waypoints).Done)
			{
				break;
			}
			bool num = waypoint._percentageDone > 0.01;
			lastWaypoint = waypoint._context.GeoPosition;
			waypoints.RemoveAt(0);
			if (num && waypoint.WpSettings.SkipNextWaypointOnComplete && waypoints.Count > 0)
			{
				waypoints[0].WpSettings = waypoint.WpSettings;
				waypoints[0].WpSettings.SkipNextWaypointOnComplete = false;
			}
		}
	}

	public static void UpdateAndGetActiveWaypoint(ref List<Waypoint> waypoints, ref GeoPosition lastWaypoint, GeoPosition targetPoint, float simulationDt)
	{
		while (waypoints.Count > 0)
		{
			Waypoint waypoint = waypoints[0];
			if (!waypoint.UpdateWaypoint(targetPoint, lastWaypoint, waypoints, simulationDt).Done)
			{
				break;
			}
			bool num = waypoint._percentageDone > 0.01;
			lastWaypoint = waypoint._context.GeoPosition;
			waypoints.RemoveAt(0);
			if (num && waypoint.WpSettings.SkipNextWaypointOnComplete && waypoints.Count > 0)
			{
				waypoints[0].WpSettings = waypoint.WpSettings;
				waypoints[0].WpSettings.SkipNextWaypointOnComplete = false;
			}
		}
	}
}
