using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Noesis;
using SeaPower;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

internal sealed class GuidancePanel
{
	private sealed class Row
	{
		internal ToggleButton View;

		internal GuidanceFrame Last;

		internal bool Selected;
	}

	private static readonly ConditionalWeakTable<MainGameViewModel, GuidancePanel> panels = new ConditionalWeakTable<MainGameViewModel, GuidancePanel>();

	private readonly List<Missile> choices = new List<Missile>();

	private readonly List<Row> rowViews = new List<Row>();

	private Grid host;

	private Grid root;

	private StackPanel rows;

	private ScrollViewer viewport;

	private TextBlock title;

	private TextBlock context;

	private TextBlock count;

	private TextBlock capabilityText;

	private Button fold;

	private Button capabilities;

	private ScrollViewer capabilityDetails;

	private DataTemplate rowTemplate;

	private Style rowStyle;

	private Path foldIcon;

	private ObjectBase lastSelection;

	private Missile expandedWeapon;

	private ObjectMissileControlViewModel lastFooter;

	private ObjectBase lastFooterTarget;

	private ObjectBase lastFooterOwner;

	private MethodInfo getFooterState;

	private string lastEquipmentSummary;

	private float nextRefresh;

	private bool disabled;

	private bool collapsed;

	private bool footerUnavailable;

	internal static void Install()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		Harmony val = new Harmony("local.seapower.cooperative-relay.hud");
		try
		{
			val.Patch((MethodBase)AccessTools.Method(typeof(MainGameViewModel), "OnUpdate", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(GuidancePanel), "UpdateView", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(MainGameViewModel), "OnDestroy", (Type[])null, (Type[])null), new HarmonyMethod(typeof(GuidancePanel), "RemoveView", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
		catch
		{
			val.UnpatchSelf();
			throw;
		}
	}

	private static void UpdateView(MainGameViewModel __instance)
	{
		GuidancePanel value = panels.GetValue(__instance, (MainGameViewModel _) => new GuidancePanel());
		if (value.disabled || Time.unscaledTime < value.nextRefresh)
		{
			return;
		}
		value.nextRefresh = Time.unscaledTime + 0.2f;
		try
		{
			value.Refresh(__instance);
		}
		catch (Exception ex)
		{
			value.disabled = true;
			Debug.LogWarning((object)("[SPCR] HUD disabled for this mission: " + ex));
			value.Detach();
		}
	}

	private static void RemoveView(MainGameViewModel __instance)
	{
		if (panels.TryGetValue(__instance, out var value))
		{
			value.Detach();
		}
		panels.Remove(__instance);
	}

	private void Detach()
	{
		try
		{
			if ((BaseComponent)(object)root != (BaseComponent)null && (BaseComponent)(object)host != (BaseComponent)null && (BaseComponent)(object)((FrameworkElement)root).Parent == (BaseComponent)(object)host)
			{
				((UICollection<UIElement>)(object)((Panel)host).Children).Remove((UIElement)(object)root);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[SPCR] HUD detach: " + ex.Message));
		}
		host = (root = null);
		rows = null;
		viewport = null;
		rowTemplate = null;
		rowStyle = null;
		title = (context = (count = (capabilityText = null)));
		fold = (capabilities = null);
		capabilityDetails = null;
		foldIcon = null;
		rowViews.Clear();
		choices.Clear();
		lastSelection = null;
		expandedWeapon = null;
		lastFooter = null;
		lastFooterTarget = (lastFooterOwner = null);
		lastEquipmentSummary = null;
	}

	private void Refresh(MainGameViewModel vm)
	{
		ISelectableObject val = vm.SelectedObject?.Value;
		ObjectBase val2 = (ObjectBase)(((object)((val is ObjectBase) ? val : null)) ?? ((object)((Vehicle)(((val is Vehicle) ? val : null)?)).Object));
		FrameworkElement rootControl = vm.RootControl;
		MainGameView val3 = (MainGameView)(((object)((rootControl is MainGameView) ? rootControl : null)) ?? ((object)MainGameView.Instance));
		Grid val4 = val3?.OverlayCanvasNew;
		if ((BaseComponent)(object)root != (BaseComponent)null && (BaseComponent)(object)host != (BaseComponent)(object)val4)
		{
			Detach();
		}
		if (!vm._isInitialized || !vm.IsHudVisible || vm.HideHudForOpticalView || (BaseComponent)(object)val3 == (BaseComponent)null || !((FrameworkElement)val3).IsLoaded || ((FrameworkElement)val3).DataContext != vm)
		{
			Hide();
			return;
		}
		GuidanceStatus.ListFor(val2, Globals._playerTaskforce, choices);
		Missile val5 = (Missile)(object)((val2 is Missile) ? val2 : null);
		string text = ((val5 == null) ? NativeCapabilities.EquipmentSummary(val2, Globals._playerTaskforce) : ((Globals._playerTaskforce != null && ((ObjectBase)val5)._taskforce == Globals._playerTaskforce && ((WeaponBase)val5)._ap != null) ? (GuidanceStatus.Name((ObjectBase)(object)val5) + " · " + NativeCapabilities.WeaponSummary(((WeaponBase)val5)._ap)) : null));
		if (choices.Count == 0 && text == null)
		{
			Hide();
			return;
		}
		if ((BaseComponent)(object)val4 == (BaseComponent)null)
		{
			throw new InvalidOperationException("MainGameView.OverlayCanvasNew unavailable");
		}
		if ((BaseComponent)(object)root == (BaseComponent)null)
		{
			Attach(val4);
			Debug.Log((object)"[SPCR] HUD attached");
		}
		Resize(((FrameworkElement)host).ActualWidth, ((FrameworkElement)host).ActualHeight);
		((UIElement)root).Visibility = (Visibility)2;
		bool num = (Object)(object)lastSelection != (Object)(object)val2;
		if (num)
		{
			expandedWeapon = (Missile)(object)((val2 is Missile) ? val2 : null);
			capabilityDetails.ScrollToVerticalOffset(0f);
			((UIElement)capabilityDetails).Visibility = (Visibility)0;
		}
		ShowCapabilities(text);
		UpdateHeader(val2, choices.Count);
		Render(val2);
		RefreshFooter(vm, val2);
		if (num)
		{
			Missile val6 = (Missile)(object)((val2 is Missile) ? val2 : null);
			int num2 = ((val6 != null) ? choices.IndexOf(val6) : (-1));
			if (!collapsed && num2 >= 0)
			{
				Reveal(rowViews[num2]);
			}
			else
			{
				viewport.ScrollToVerticalOffset(0f);
			}
			lastSelection = val2;
		}
	}

	private void Hide()
	{
		if ((BaseComponent)(object)root != (BaseComponent)null)
		{
			((UIElement)root).Visibility = (Visibility)0;
		}
		EnsureRows(0);
		lastSelection = null;
		expandedWeapon = null;
		lastFooter = null;
		lastFooterTarget = (lastFooterOwner = null);
		choices.Clear();
	}

	private void RefreshFooter(MainGameViewModel vm, ObjectBase selected)
	{
		if (footerUnavailable)
		{
			return;
		}
		try
		{
			Missile val = (Missile)(object)((selected is Missile) ? selected : null);
			if (val != null && ((ObjectBase)val)._taskforce == Globals._playerTaskforce && RemoteGuidance.Enrolled(val))
			{
				ISelectableViewModel obj = vm.SelectedViewModel?.Value;
				WeaponBaseViewModel val2 = (WeaponBaseViewModel)(object)((obj is WeaponBaseViewModel) ? obj : null);
				if (val2 != null && !((Object)(object)val2.WeaponObject != (Object)(object)val))
				{
					IWeaponControlViewModel objectWeaponControlViewModel = val2.ObjectWeaponControlViewModel;
					ObjectMissileControlViewModel val3 = (ObjectMissileControlViewModel)(object)((objectWeaponControlViewModel is ObjectMissileControlViewModel) ? objectWeaponControlViewModel : null);
					if (val3 != null)
					{
						ObjectBase val4 = TargetGuard.DisplayTarget((WeaponBase)(object)val);
						ObjectBase val5 = Relay.RadioOwner((WeaponBase)(object)val);
						if (val3 != lastFooter || !((Object)(object)val4 == (Object)(object)lastFooterTarget) || !((Object)(object)val5 == (Object)(object)lastFooterOwner))
						{
							if (getFooterState == null)
							{
								getFooterState = AccessTools.DeclaredMethod(typeof(ObjectMissileControlViewModel), "GetState", new Type[2]
								{
									typeof(Missile),
									typeof(SensorSystemLaserDesignator)
								}, (Type[])null) ?? throw new MissingMethodException("ObjectMissileControlViewModel.GetState");
							}
							MethodInfo methodInfo = getFooterState;
							object[] obj2 = new object[2] { val, null };
							SeekerBase seeker = ((WeaponBase)val)._seeker;
							SeekerBase obj3 = ((seeker is LaserSeeker) ? seeker : null);
							obj2[1] = ((obj3 == null) ? null : ((LaserSeeker)obj3).GuidingDesignator?.Value);
							methodInfo.Invoke(val3, obj2);
							lastFooter = val3;
							lastFooterTarget = val4;
							lastFooterOwner = val5;
						}
						return;
					}
				}
			}
			lastFooter = null;
			lastFooterTarget = (lastFooterOwner = null);
		}
		catch (Exception ex)
		{
			footerUnavailable = true;
			Debug.LogWarning((object)("[SPCR] Native footer refresh unavailable for this mission: " + ex));
		}
	}

	private void Attach(Grid overlay)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected O, but got Unknown
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Expected O, but got Unknown
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected O, but got Unknown
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Expected O, but got Unknown
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Expected O, but got Unknown
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Expected O, but got Unknown
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Expected O, but got Unknown
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Expected O, but got Unknown
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Expected O, but got Unknown
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Expected O, but got Unknown
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Expected O, but got Unknown
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Expected O, but got Unknown
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Expected O, but got Unknown
		using (Stream stream = typeof(GuidancePanel).Assembly.GetManifestResourceStream("SeaPowerCooperativeRelay.GuidancePanel.xaml"))
		{
			using StreamReader streamReader = new StreamReader(stream ?? throw new InvalidDataException("Missing guidance panel XAML"));
			root = (Grid)GUI.ParseXaml(streamReader.ReadToEnd());
		}
		host = overlay;
		rows = (StackPanel)((FrameworkElement)root).FindName("Rows");
		viewport = (ScrollViewer)((FrameworkElement)root).FindName("Viewport");
		title = (TextBlock)((FrameworkElement)root).FindName("HeaderTitle");
		context = (TextBlock)((FrameworkElement)root).FindName("Context");
		count = (TextBlock)((FrameworkElement)root).FindName("Count");
		capabilities = (Button)((FrameworkElement)root).FindName("Capabilities");
		capabilityText = (TextBlock)((FrameworkElement)root).FindName("CapabilityText");
		capabilityDetails = (ScrollViewer)((FrameworkElement)root).FindName("CapabilityDetails");
		((ButtonBase)capabilities).Click += (RoutedEventHandler)delegate(object _, RoutedEventArgs e)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Invalid comparison between Unknown and I4
			((UIElement)capabilityDetails).Visibility = (Visibility)(((int)((UIElement)capabilityDetails).Visibility != 2) ? 2 : 0);
			UpdateWidth();
			e.Handled = true;
		};
		rowTemplate = (DataTemplate)((FrameworkElement)root).FindResource((object)"MissileRow");
		rowStyle = (Style)((FrameworkElement)root).FindResource((object)"MissileButton");
		foldIcon = (Path)((FrameworkElement)root).FindName("FoldIcon");
		fold = (Button)((FrameworkElement)root).FindName("Fold");
		((ButtonBase)fold).Click += (RoutedEventHandler)delegate(object _, RoutedEventArgs e)
		{
			SetFold(!collapsed);
			e.Handled = true;
		};
		SetFold(collapsed);
		Grid.SetRowSpan((DependencyObject)(object)root, Math.Max(1, ((BaseUICollection)host.RowDefinitions).Count));
		Grid.SetColumnSpan((DependencyObject)(object)root, Math.Max(1, ((BaseUICollection)host.ColumnDefinitions).Count));
		Panel.SetZIndex((DependencyObject)(object)root, 4);
		((UICollection<UIElement>)(object)((Panel)host).Children).Add((UIElement)(object)root);
	}

	private void ShowCapabilities(string summary)
	{
		if (!(lastEquipmentSummary == summary))
		{
			lastEquipmentSummary = summary;
			((UIElement)capabilities).Visibility = (Visibility)((summary != null) ? 2 : 0);
			capabilityText.Text = summary ?? "";
			if (summary == null)
			{
				((UIElement)capabilityDetails).Visibility = (Visibility)0;
			}
			UpdateWidth();
		}
	}

	private void UpdateHeader(ObjectBase selected, int inFlight)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051->IL0051: Incompatible stack types: O vs I4
		//IL_0050->IL0051: Incompatible stack types: I4 vs O
		//IL_0050->IL0051: Incompatible stack types: O vs I4
		Visibility val = (Visibility)((inFlight > 0) ? 2 : 0);
		TextBlock obj = title;
		TextBlock obj2 = context;
		TextBlock obj3 = count;
		Visibility val3 = (((UIElement)fold).Visibility = val);
		Visibility val5 = (((UIElement)obj3).Visibility = val3);
		Visibility visibility = (((UIElement)obj2).Visibility = val5);
		((UIElement)obj).Visibility = visibility;
		object obj4 = viewport;
		int num;
		if (!collapsed)
		{
			obj4 = val;
			num = (int)obj4;
		}
		else
		{
			num = 0;
			obj4 = num;
			num = (int)obj4;
		}
		((UIElement)num).Visibility = (Visibility)obj4;
		if (inFlight > 0)
		{
			Missile val7 = (Missile)(object)((selected is Missile) ? selected : null);
			ObjectBase val8 = ((val7 != null) ? ((WeaponBase)val7)._launchPlatform : selected);
			context.Text = (((Object)(object)val8 == (Object)null) ? "发射平台未知" : GuidanceStatus.Name(val8));
			((FrameworkElement)context).ToolTip = (((Object)(object)val8 == (Object)null) ? context.Text : ("发射平台 " + GuidanceStatus.Station(val8)));
			count.Text = inFlight + " 枚";
		}
		UpdateWidth();
	}

	private void UpdateWidth()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Invalid comparison between Unknown and I4
		((FrameworkElement)root).Width = (((int)((UIElement)count).Visibility == 2 || (int)((UIElement)capabilityDetails).Visibility == 2) ? 368f : float.NaN);
	}

	private void SetFold(bool value)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected O, but got Unknown
		collapsed = value;
		((UIElement)viewport).Visibility = (Visibility)((!value) ? ((int)((UIElement)count).Visibility) : 0);
		if (value)
		{
			((UIElement)capabilityDetails).Visibility = (Visibility)0;
		}
		((UIElement)foldIcon).RenderTransform = (Transform)new RotateTransform
		{
			Angle = (value ? 180f : 0f)
		};
		((FrameworkElement)fold).ToolTip = (value ? "展开在途导弹" : "收起在途导弹");
		UpdateWidth();
		if (!value && (Object)(object)expandedWeapon != (Object)null)
		{
			int num = choices.IndexOf(expandedWeapon);
			if (num >= 0 && num < rowViews.Count)
			{
				Reveal(rowViews[num]);
			}
		}
	}

	private void Reveal(Row row)
	{
		((UIElement)root).UpdateLayout();
		((FrameworkElement)row.View).BringIntoView();
	}

	private void Resize(float width, float height)
	{
		if (width > 0f)
		{
			((FrameworkElement)root).MaxWidth = Math.Min(368f, Math.Max(1f, width - 28f));
		}
		if (height > 0f)
		{
			((FrameworkElement)viewport).MaxHeight = Math.Min(300f, height * 0.36f);
			((FrameworkElement)capabilityDetails).MaxHeight = Math.Min(160f, height * 0.25f);
		}
	}

	private void EnsureRows(int wanted)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Expected O, but got Unknown
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Expected O, but got Unknown
		while (rowViews.Count > wanted)
		{
			int index = rowViews.Count - 1;
			((UICollection<UIElement>)(object)((Panel)rows).Children).Remove((UIElement)(object)rowViews[index].View);
			rowViews.RemoveAt(index);
		}
		while (rowViews.Count < wanted)
		{
			Row row = new Row
			{
				View = new ToggleButton
				{
					Style = rowStyle,
					ContentTemplate = rowTemplate
				}
			};
			((ButtonBase)row.View).Click += (RoutedEventHandler)delegate(object _, RoutedEventArgs e)
			{
				expandedWeapon = (row.View.IsChecked.GetValueOrDefault() ? row.Last.Weapon : null);
				foreach (Row rowView in rowViews)
				{
					if (rowView != row && rowView.View.IsChecked.GetValueOrDefault())
					{
						rowView.View.IsChecked = false;
					}
				}
				if (row.View.IsChecked.GetValueOrDefault())
				{
					Reveal(row);
				}
				e.Handled = true;
			};
			((UICollection<UIElement>)(object)((Panel)rows).Children).Add((UIElement)(object)row.View);
			rowViews.Add(row);
		}
	}

	private void Render(ObjectBase selected)
	{
		EnsureRows(choices.Count);
		if ((Object)(object)expandedWeapon != (Object)null && !choices.Contains(expandedWeapon))
		{
			expandedWeapon = null;
		}
		for (int i = 0; i < choices.Count; i++)
		{
			GuidanceFrame guidanceFrame = GuidanceStatus.Read(choices[i]);
			Row row = rowViews[i];
			bool flag = (Object)(object)choices[i] == (Object)(object)selected;
			bool flag2 = (Object)(object)choices[i] == (Object)(object)expandedWeapon;
			if (row.View.IsChecked != flag2)
			{
				row.View.IsChecked = flag2;
			}
			if (!guidanceFrame.Equals(row.Last) || row.Selected != flag)
			{
				((ContentControl)row.View).Content = new GuidanceCard(guidanceFrame, flag);
				row.Last = guidanceFrame;
				row.Selected = flag;
			}
		}
	}
}
