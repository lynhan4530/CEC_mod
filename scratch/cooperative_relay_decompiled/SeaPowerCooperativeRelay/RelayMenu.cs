using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;
using UniRx;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(ObjectBaseViewModel), "GetContextMenuItems")]
internal static class RelayMenu
{
	private static void Postfix(ObjectBaseViewModel __instance, TrulyObservableCollection<ContextMenuItem> __result)
	{
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Expected O, but got Unknown
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Expected O, but got Unknown
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Expected O, but got Unknown
		ObjectBase unit = __instance._objectBase;
		if (__result == null || !Network.PlayerUnit(unit))
		{
			return;
		}
		bool flag = Network.CanToggleNoLock(unit);
		if (!Relay.CanToggle(unit))
		{
			return;
		}
		((Collection<ContextMenuItem>)(object)__result).Add(new ContextMenuItem(Relay.Participates(unit) ? "数据链参与：开" : "数据链参与：关", (TrulyObservableCollection<ContextMenuItem>)null, (ICommand)new DelegateCommand((Action<object>)delegate
		{
			Relay.Toggle(unit);
		}), (ReadOnlyReactiveProperty<bool>)null, (ReadOnlyReactiveProperty<bool>)null, (string)null, (string)null, (string)null, (string)null, (string)null));
		if (flag)
		{
			((Collection<ContextMenuItem>)(object)__result).Add(new ContextMenuItem(Network.NoLock(unit) ? "免锁定发射：开" : "免锁定发射：关", (TrulyObservableCollection<ContextMenuItem>)null, (ICommand)new DelegateCommand((Action<object>)delegate
			{
				Network.ToggleNoLock(unit);
			}), (ReadOnlyReactiveProperty<bool>)null, (ReadOnlyReactiveProperty<bool>)null, (string)null, (string)null, (string)null, (string)null, (string)null));
		}
	}
}
