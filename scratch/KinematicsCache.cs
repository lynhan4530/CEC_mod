using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace SeaPower;

public class KinematicsCache
{
	private static readonly Lazy<KinematicsCache> _lazy = new Lazy<KinematicsCache>(() => new KinematicsCache());

	private readonly ConcurrentDictionary<Tuple<string, bool>, float> _dragFactorCache = new ConcurrentDictionary<Tuple<string, bool>, float>();

	public static KinematicsCache Instance => _lazy.Value;

	public float CalcDragFactor(AmmunitionParameters ap, bool isAirLaunched)
	{
		Tuple<string, bool> key = new Tuple<string, bool>(ap._ammunitionFileName, isAirLaunched);
		return _dragFactorCache.GetOrAdd(key, (Tuple<string, bool> _) => GetDragFactor(ap, isAirLaunched));
	}

	private float GetDragFactor(AmmunitionParameters ap, bool airLaunched)
	{
		float num = 0.05f;
		float num2 = 50f;
		float num3 = 1f;
		float typicalLaunchVelocity = ap.TypicalLaunchVelocity;
		Vector3 launchPosition = new Vector3(0f, ap.TypicalFiringAlt, 0f);
		float typicalTargetAlt = ap.TypicalTargetAlt;
		Vector3 targetVelocityVector = new Vector3(-1f * ap.TypicalTargetVelocity, 0f, 0f);
		for (int i = 0; i < 14; i++)
		{
			num3 = (num + num2) / 2f;
			if (MissileSimulator.EstimateShot(ap, launchPosition, typicalLaunchVelocity / 0.0076554087f, targetVelocityVector, new Vector3(ap._launchRangesInUnity.y, typicalTargetAlt, 0f), airLaunched, num3, ap._timeLimited ? (-1f) : float.MaxValue).DistPercent >= 1f)
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

	public float CalcMaxRange(ObjectBase launcher, float launchVelocity, AmmunitionParameters ap, Vector3 targetVelocity, Vector3 targetPos, bool targetEvasiveManuver = true)
	{
		Vector3 position = launcher.transform.position;
		MissileSimulator.KinematicRangeResult kinematicRangeResult = MissileSimulator.EstimateShot(ap, position, launchVelocity, targetVelocity, targetPos, launcher.IsAirUnit, -1f, -1f, -1f, targetEvasiveManuver);
		if (float.IsNaN(kinematicRangeResult.MaxRange))
		{
			Log.Error("Missile range calculation returned NAN! " + ap._ammunitionFileName + ", " + position.ToString() + ", " + targetPos.ToString(), Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Utils\\KinematicsCache.cs", 53, "CalcMaxRange");
			return 0f;
		}
		return kinematicRangeResult.MaxRange;
	}

	public void OnStart()
	{
		_dragFactorCache.Clear();
		MissileSimulator.ClearLoftSearchCache();
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
