using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnchorChain;
using HarmonyLib;
using SeaPower;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

[ACPlugin("local.seapower.cooperative-relay", "海权三平台协同制导", "1.5.3", null, null)]
public sealed class Plugin : IAnchorChainMod
{
	public void TriggerEntryPoint()
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		string folder = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
		if (!((IEnumerable<SearchDirectory>)Singleton<FileManager>.Instance.Directories).Any((SearchDirectory d) => d.IsChecked && string.Equals(d.DirectoryInfo.FullName, folder, StringComparison.OrdinalIgnoreCase)))
		{
			return;
		}
		Harmony val = new Harmony("local.seapower.cooperative-relay");
		try
		{
			Relay.Load(Path.Combine(folder, "CooperativeRelay.ini"));
			val.PatchAll(typeof(Plugin).Assembly);
			Debug.Log((object)"[SeaPowerCooperativeRelay] Ready 1.5.3");
		}
		catch (Exception ex)
		{
			val.UnpatchSelf();
			Debug.LogError((object)("[SeaPowerCooperativeRelay] Disabled: " + ex));
			return;
		}
		try
		{
			GuidancePanel.Install();
		}
		catch (Exception ex2)
		{
			Debug.LogWarning((object)("[SPCR] HUD unavailable: " + ex2));
		}
	}
}
