using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace SeaPower;

public static class WeaponArcHoverManager
{
	public class HoverInfo
	{
		public ObjectBase Unit;

		public List<WeaponSystem> Systems;

		public float MapRangeInUnity;
	}

	private static object _candidateOwner;

	private static HoverInfo _candidate;

	private static IDisposable _tick;

	public static ReactiveProperty<HoverInfo> Hovered { get; } = new ReactiveProperty<HoverInfo>();


	public static void SetCandidate(object owner, ObjectBase unit, List<WeaponSystem> systems, float mapRangeInUnity)
	{
		if (unit == null || systems == null || systems.Count == 0)
		{
			ClearCandidate(owner);
			return;
		}
		_candidateOwner = owner;
		_candidate = new HoverInfo
		{
			Unit = unit,
			Systems = systems,
			MapRangeInUnity = mapRangeInUnity
		};
		EnsureTicking();
	}

	public static void ClearCandidate(object owner)
	{
		if (owner == null || owner == _candidateOwner)
		{
			_candidateOwner = null;
			_candidate = null;
		}
	}

	public static void ReleaseMissionReferences()
	{
		_candidateOwner = null;
		_candidate = null;
		_tick?.Dispose();
		_tick = null;
		if (Hovered.Value != null)
		{
			Hovered.Value = null;
		}
	}

	private static void EnsureTicking()
	{
		if (_tick == null)
		{
			_tick = Observable.EveryUpdate().Subscribe((Action<long>)delegate
			{
				Reconcile();
			}, (Action<Exception>)delegate
			{
			});
		}
	}

	private static void Reconcile()
	{
		HoverInfo hoverInfo = ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) ? _candidate : null);
		if (hoverInfo != Hovered.Value)
		{
			Hovered.Value = hoverInfo;
			if (hoverInfo != null)
			{
				WeaponArc3DRenderer.Show(hoverInfo);
			}
			else
			{
				WeaponArc3DRenderer.Hide();
			}
		}
		if (_candidate == null && Hovered.Value == null)
		{
			_tick?.Dispose();
			_tick = null;
		}
	}

	public static List<Vector2> GetArcSpans(List<Vector2> firingArcs)
	{
		List<Vector2> list = new List<Vector2>();
		if (firingArcs == null || firingArcs.Count == 0)
		{
			list.Add(new Vector2(0f, 360f));
			return list;
		}
		foreach (Vector2 firingArc in firingArcs)
		{
			if (firingArc == Vector2.zero)
			{
				list.Clear();
				list.Add(new Vector2(0f, 360f));
				return list;
			}
			float num = firingArc.y - firingArc.x;
			if (num <= 0f)
			{
				num += 360f;
			}
			float x = firingArc.x + num * 0.5f;
			list.Add(new Vector2(x, num));
		}
		return list;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
