using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics;
using Noesis;
using SeaPower;
using UnityEngine;

namespace SeapowerUI.ViewModels;

public class MissileRangeChartViewModel : INotifyPropertyChanged
{
	public const float CHART_WIDTH = 280f;

	public const float CHART_HEIGHT = 150f;

	private readonly AmmunitionParameters _ap;

	private float _firingSpeedKnots;

	private float _targetSpeedKnots;

	private PathGeometry _chartGeometry = new PathGeometry();

	private string _maxRangeLabel = "";

	private string _midRangeLabel = "";

	private string _maxAltLabel = "";

	private string _midAltLabel = "";

	private float _storedMaxRangeNm;

	private float _storedMaxAltFeet;

	private List<(float cx, float cy, float rangeNm, float altFeet)> _chartPoints = new List<(float, float, float, float)>();

	private bool _isCursorVisible;

	private float _cursorX;

	private float _cursorY;

	private float _cursorLabelX;

	private float _cursorLabelY = 5f;

	private string _hoverRangeLabel = "";

	private string _hoverAltLabel = "";

	public bool IsAirLaunched => _ap._airLaunched;

	public float FiringSpeedKnots
	{
		get
		{
			return _firingSpeedKnots;
		}
		set
		{
			_firingSpeedKnots = value;
			OnPropertyChanged("FiringSpeedKnots");
			Recompute();
		}
	}

	public float TargetSpeedKnots
	{
		get
		{
			return _targetSpeedKnots;
		}
		set
		{
			_targetSpeedKnots = value;
			OnPropertyChanged("TargetSpeedKnots");
			Recompute();
		}
	}

	public PathGeometry ChartGeometry
	{
		get
		{
			return _chartGeometry;
		}
		private set
		{
			_chartGeometry = value;
			OnPropertyChanged("ChartGeometry");
		}
	}

	public string MaxRangeLabel
	{
		get
		{
			return _maxRangeLabel;
		}
		private set
		{
			_maxRangeLabel = value;
			OnPropertyChanged("MaxRangeLabel");
		}
	}

	public string MidRangeLabel
	{
		get
		{
			return _midRangeLabel;
		}
		private set
		{
			_midRangeLabel = value;
			OnPropertyChanged("MidRangeLabel");
		}
	}

	public string MaxAltLabel
	{
		get
		{
			return _maxAltLabel;
		}
		private set
		{
			_maxAltLabel = value;
			OnPropertyChanged("MaxAltLabel");
		}
	}

	public string MidAltLabel
	{
		get
		{
			return _midAltLabel;
		}
		private set
		{
			_midAltLabel = value;
			OnPropertyChanged("MidAltLabel");
		}
	}

	public bool IsCursorVisible
	{
		get
		{
			return _isCursorVisible;
		}
		set
		{
			_isCursorVisible = value;
			OnPropertyChanged("IsCursorVisible");
		}
	}

	public float CursorX
	{
		get
		{
			return _cursorX;
		}
		set
		{
			_cursorX = value;
			OnPropertyChanged("CursorX");
		}
	}

	public float CursorY
	{
		get
		{
			return _cursorY;
		}
		set
		{
			_cursorY = value;
			OnPropertyChanged("CursorY");
		}
	}

	public float CursorLabelX
	{
		get
		{
			return _cursorLabelX;
		}
		set
		{
			_cursorLabelX = value;
			OnPropertyChanged("CursorLabelX");
		}
	}

	public float CursorLabelY
	{
		get
		{
			return _cursorLabelY;
		}
		set
		{
			_cursorLabelY = value;
			OnPropertyChanged("CursorLabelY");
		}
	}

	public string HoverRangeLabel
	{
		get
		{
			return _hoverRangeLabel;
		}
		set
		{
			_hoverRangeLabel = value;
			OnPropertyChanged("HoverRangeLabel");
		}
	}

	public string HoverAltLabel
	{
		get
		{
			return _hoverAltLabel;
		}
		set
		{
			_hoverAltLabel = value;
			OnPropertyChanged("HoverAltLabel");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string name = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public void UpdateHover(float canvasX, float canvasY)
	{
		if (_chartPoints.Count == 0)
		{
			IsCursorVisible = false;
			return;
		}
		float num = float.MaxValue;
		float num2 = 0f;
		float num3 = 0f;
		float num4 = 0f;
		float num5 = 0f;
		if (_chartPoints.Count == 1)
		{
			(num2, num3, num4, num5) = _chartPoints[0];
		}
		else
		{
			for (int i = 0; i + 1 < _chartPoints.Count; i++)
			{
				(float, float, float, float) tuple2 = _chartPoints[i];
				(float, float, float, float) tuple3 = _chartPoints[i + 1];
				float num6 = tuple3.Item1 - tuple2.Item1;
				float num7 = tuple3.Item2 - tuple2.Item2;
				float num8 = num6 * num6 + num7 * num7;
				float num9 = ((num8 > 0f) ? Math.Clamp(((canvasX - tuple2.Item1) * num6 + (canvasY - tuple2.Item2) * num7) / num8, 0f, 1f) : 0f);
				float num10 = tuple2.Item1 + num9 * num6;
				float num11 = tuple2.Item2 + num9 * num7;
				float num12 = (canvasX - num10) * (canvasX - num10) + (canvasY - num11) * (canvasY - num11);
				if (num12 < num)
				{
					num = num12;
					num2 = num10;
					num3 = num11;
					num4 = tuple2.Item3 + num9 * (tuple3.Item3 - tuple2.Item3);
					num5 = tuple2.Item4 + num9 * (tuple3.Item4 - tuple2.Item4);
				}
			}
		}
		CursorX = num2;
		CursorY = num3;
		string text = Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "NauticalMiles");
		string text2 = Singleton<LanguageResourceHandler>.Instance.getText("MeasurmentUnits", "Feet");
		HoverRangeLabel = $"{num4:F1} {text}";
		HoverAltLabel = $"{num5:F0} {text2}";
		CursorLabelX = ((num2 + 6f < 218f) ? (num2 + 6f) : (num2 - 62f - 6f));
		CursorLabelY = Math.Max(5f, num3 - 24f);
		IsCursorVisible = true;
	}

	public void HideHover()
	{
		IsCursorVisible = false;
	}

	public MissileRangeChartViewModel(AmmunitionParameters ap)
	{
		_ap = ap;
		_firingSpeedKnots = ap.TypicalLaunchVelocity / 0.0076554087f;
		_targetSpeedKnots = ap.TypicalTargetVelocity / 0.0076554087f;
		Recompute();
	}

	private void Recompute()
	{
		float num = MathF.Min(_ap._attackAltitudesInUnity.y * 220.47266f, 100000f);
		if (num < 1000f)
		{
			num = 50000f;
		}
		if (_ap._airLaunched)
		{
			num = MathF.Min(num, _ap._launchAltitudesInUnity.y * 220.47266f);
		}
		List<float> altSamples = new List<float>();
		for (float num2 = 0f; num2 <= num + 1f; num2 += 1250f)
		{
			altSamples.Add(Math.Min(num2, num));
		}
		Vector3 targetVelocity = new Vector3((0f - _targetSpeedKnots) * 0.0076554087f, 0f, 0f);
		MissileSimulator.KinematicRangeResult[] rangeResults = new MissileSimulator.KinematicRangeResult[altSamples.Count];
		Parallel.For(0, altSamples.Count, delegate(int i)
		{
			float num14 = altSamples[i];
			float y = ((_ap._targetType == Ammunition.Target.AAW) ? (num14 * 0.00453571f) : 0f);
			float y2 = (_ap._airLaunched ? (num14 * 0.00453571f) : 0f);
			Vector3 launchPos = new Vector3(0f, y2, 0f);
			Vector3 targetPos = new Vector3(_ap._launchRangesInUnity.y, y, 0f);
			MissileSimulator.KinematicRangeResult kinematicRangeResult = _ap.MaxRangePrecise(launchPos, targetPos, targetVelocity, _firingSpeedKnots, _ap._airLaunched, 18);
			rangeResults[i] = kinematicRangeResult;
		});
		List<(float, float)> list = new List<(float, float)>(altSamples.Count);
		for (int j = 0; j < altSamples.Count; j++)
		{
			float num3 = rangeResults[j].MaxRange / rangeResults[j].DistPercent;
			list.Add((num3 * 0.036285132f, altSamples[j]));
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine($"[RangeChartDebug] {_ap._ammunitionFileName} f={_ap.GetDragFactor(_ap._airLaunched):F3} speed={_firingSpeedKnots:F0}kt tgt={_targetSpeedKnots:F0}kt maxAlt={num:F0}ft");
		for (int k = 0; k < altSamples.Count; k++)
		{
			stringBuilder.AppendLine($"  alt={altSamples[k],7:F0}ft range={list[k].Item1,7:F2}nm maxRange={rangeResults[k].MaxRange * 0.036285132f,7:F2} distPct={rangeResults[k].DistPercent:F3} iSpd={rangeResults[k].InterceptSpeed:F0} iT={rangeResults[k].InterceptTime:F0}");
		}
		MissileSimulator.FlyoutProfile flyoutProfile = MissileSimulator.BuildFlyout(_ap, 294.82114f, 0f, 2204.7598f, 900f, isAirUnit: true, _ap.GetDragFactor(_ap._airLaunched), -1f, 200f);
		stringBuilder.AppendLine($"[ProfileDebug] 65000ft/900kt/80nm: EndTime={flyoutProfile.EndTime:F1}s EndFlat={flyoutProfile.EndFlatDist * 0.036285132f:F1}nm stalled={flyoutProfile.Stalled}");
		for (int l = 0; l < flyoutProfile.Times.Count; l += Math.Max(1, flyoutProfile.Times.Count / 25))
		{
			stringBuilder.AppendLine($"  t={flyoutProfile.Times[l],6:F1}s flat={flyoutProfile.FlatDists[l] * 0.036285132f,6:F1}nm alt={flyoutProfile.Alts[l] * 220.47266f,7:F0}ft v={flyoutProfile.SpeedsUnity[l] / 0.0076554087f,6:F0}kt");
		}
		Debug.Log(stringBuilder.ToString());
		float num4 = 0f;
		foreach (var item6 in list)
		{
			if (item6.Item1 > num4)
			{
				(num4, _) = item6;
			}
		}
		if (list.Count >= 5)
		{
			float[] array = new float[5];
			float[] array2 = new float[list.Count];
			for (int m = 0; m < list.Count; m++)
			{
				array2[m] = list[m].Item1;
			}
			for (int n = 2; n < list.Count - 2; n++)
			{
				for (int num5 = 0; num5 < 5; num5++)
				{
					array[num5] = list[n - 2 + num5].Item1;
				}
				Array.Sort(array);
				if (MathF.Abs(list[n].Item1 - array[2]) > 0.02f * num4)
				{
					array2[n] = array[2];
				}
			}
			for (int num6 = 0; num6 < list.Count; num6++)
			{
				list[num6] = (array2[num6], list[num6].Item2);
			}
		}
		float num7 = 0f;
		foreach (var item7 in list)
		{
			if (item7.Item1 > num7)
			{
				(num7, _) = item7;
			}
		}
		if (num7 < 0.01f)
		{
			_chartPoints = new List<(float, float, float, float)>();
			ChartGeometry = new PathGeometry();
			return;
		}
		_storedMaxAltFeet = num;
		num7 = (_storedMaxRangeNm = (num7 * 1.2f).Round(1));
		MaxRangeLabel = $"{num7:F0}";
		MidRangeLabel = $"{num7 / 2f:F0}";
		MaxAltLabel = $"{num:F0}";
		MidAltLabel = $"{num / 2f:F0}";
		List<(float, float, float, float)> list2 = new List<(float, float, float, float)>(list.Count);
		foreach (var item8 in list)
		{
			float item = item8.Item1;
			float item2 = item8.Item2;
			float item3 = item / num7 * 280f;
			float item4 = 150f - ((num > 0f) ? (item2 / num * 150f) : 0f);
			list2.Add((item3, item4, item, item2));
		}
		for (int num8 = 1; num8 < list2.Count - 1; num8++)
		{
			float num9 = (list2[num8 - 1].Item1 + list2[num8 + 1].Item1) * 0.5f;
			if (!(num9 < 0.001f))
			{
				float num10 = MathF.Abs(list2[num8].Item1 - num9) / num9;
				if (num10 > 0.2f)
				{
					float num11 = MathF.Min((num10 - 0.2f) / 0.8f, 1f);
					float num12 = list2[num8].Item1 + num11 * (num9 - list2[num8].Item1);
					list2[num8] = (num12, list2[num8].Item2, num12 / 280f * num7, list2[num8].Item4);
				}
			}
		}
		List<(float, float, float, float)> list3 = new List<(float, float, float, float)>(list2.Count);
		PathFigure pathFigure = new PathFigure();
		if (list2.Count > 0)
		{
			pathFigure.StartPoint = new Point(list2[0].Item1, list2[0].Item2);
			list3.Add(list2[0]);
			for (int num13 = 0; num13 < list2.Count - 1; num13++)
			{
				(float, float, float, float) tuple3 = list2[Math.Max(num13 - 1, 0)];
				(float, float, float, float) tuple4 = list2[num13];
				(float, float, float, float) item5 = list2[num13 + 1];
				(float, float, float, float) tuple5 = list2[Math.Min(num13 + 2, list2.Count - 1)];
				Point point = new Point(tuple4.Item1 + (item5.Item1 - tuple3.Item1) / 6f, tuple4.Item2 + (item5.Item2 - tuple3.Item2) / 6f);
				Point point2 = new Point(item5.Item1 - (tuple5.Item1 - tuple4.Item1) / 6f, item5.Item2 - (tuple5.Item2 - tuple4.Item2) / 6f);
				pathFigure.Segments.Add(new BezierSegment(point, point2, new Point(item5.Item1, item5.Item2), isStroked: true));
				list3.Add(item5);
			}
		}
		_chartPoints = list3;
		PathGeometry pathGeometry = new PathGeometry();
		pathGeometry.Figures.Add(pathFigure);
		ChartGeometry = pathGeometry;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050'
