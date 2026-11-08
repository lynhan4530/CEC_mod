using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using SeaPower;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

internal static class NativeCapabilities
{
	private sealed class WeaponProfile
	{
		internal int Seeker;

		internal int Correction;

		internal string Policy;
	}

	private sealed class RadarProfile
	{
		internal string Role;

		internal bool SharesTracks;

		internal string Policy;
	}

	private sealed class Policy
	{
		internal string Evidence;

		internal string Badge;

		internal string Restriction;

		internal string Sources;
	}

	private static readonly Dictionary<string, Policy> policies = new Dictionary<string, Policy>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, WeaponProfile> weapons = new Dictionary<string, WeaponProfile>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, RadarProfile> radars = new Dictionary<string, RadarProfile>(StringComparer.OrdinalIgnoreCase);

	internal static int WeaponCount => weapons.Count;

	internal static int RadarCount => radars.Count;

	internal static void Load(string path)
	{
		weapons.Clear();
		radars.Clear();
		policies.Clear();
		sources.Clear();
		string text = "";
		foreach (string item in File.ReadLines(path))
		{
			string text2 = item.Trim();
			if (text2.Length == 0 || text2.StartsWith(";") || text2.StartsWith("#"))
			{
				continue;
			}
			if (text2.StartsWith("[") && text2.EndsWith("]"))
			{
				text = text2.Substring(1, text2.Length - 2);
				continue;
			}
			int num = text2.IndexOf('=');
			if (num <= 0)
			{
				throw new FormatException("Invalid native capability: " + text2);
			}
			string text3 = text2.Substring(0, num).Trim();
			if (text == "Profiles")
			{
				string[] array = text2.Substring(num + 1).Split('|', StringSplitOptions.None);
				if (array.Length != 4 || Array.IndexOf(new string[6] { "Variant", "Family", "Conditional", "ModelOnly", "Unverified", "Generic" }, array[0]) < 0 || string.IsNullOrWhiteSpace(array[1]) || string.IsNullOrWhiteSpace(array[2]) || string.IsNullOrWhiteSpace(array[3]))
				{
					throw new FormatException("Invalid native evidence profile: " + text2);
				}
				policies.Add(text3, new Policy
				{
					Evidence = array[0],
					Badge = array[1],
					Restriction = array[2],
					Sources = array[3]
				});
				continue;
			}
			if (text == "Sources")
			{
				string[] array2 = text2.Substring(num + 1).Split('|', StringSplitOptions.None);
				if (array2.Length == 3 && array2[0].Length != 0 && array2[1].Length != 0 && array2[2].Length != 0 && sources.Add(text3))
				{
					continue;
				}
				throw new FormatException("Invalid native source: " + text2);
			}
			string[] array3 = text2.Substring(num + 1).Split(',', StringSplitOptions.None);
			if (array3.Length != 4 || array3[3].Trim() != "NativeOnly")
			{
				throw new FormatException("Invalid native capability: " + text2);
			}
			if (text == "Ammunition")
			{
				if (!int.TryParse(array3[0], out var result) || result < 0 || result > 6 || !int.TryParse(array3[1], out var result2) || result2 < 0 || result2 > 3)
				{
					throw new FormatException("Invalid native weapon profile: " + text2);
				}
				weapons.Add(text3, new WeaponProfile
				{
					Seeker = result,
					Correction = result2,
					Policy = array3[2].Trim()
				});
				continue;
			}
			if (text == "Radars")
			{
				string text4 = array3[0].Trim();
				if (Array.IndexOf(new string[12]
				{
					"LocalUplink", "LocalCommand", "LocalFireControl", "DirectedRadar", "SearchRadar", "SearchAndUplink", "TestRadar", "GenericRadar", "NavigationRadar", "EarlyWarningRadar",
					"GunFireControl", "HeightFinder"
				}, text4) < 0 || !bool.TryParse(array3[1], out var result3))
				{
					throw new FormatException("Invalid native radar profile: " + text2);
				}
				radars.Add(text3, new RadarProfile
				{
					Role = text4,
					SharesTracks = result3,
					Policy = array3[2].Trim()
				});
				continue;
			}
			throw new FormatException("Unknown native capability section: " + text);
		}
		if (weapons.Count == 0 || radars.Count == 0)
		{
			throw new InvalidDataException("Empty native capability catalog");
		}
		foreach (WeaponProfile value in weapons.Values)
		{
			RequirePolicy(value.Policy);
		}
		foreach (RadarProfile value2 in radars.Values)
		{
			RequirePolicy(value2.Policy);
		}
		foreach (Policy value3 in policies.Values)
		{
			string[] array4 = value3.Sources.Split(';', StringSplitOptions.None);
			foreach (string text5 in array4)
			{
				if (!sources.Contains(text5))
				{
					throw new InvalidDataException("Unknown native evidence source: " + text5);
				}
			}
		}
	}

	private static void RequirePolicy(string key)
	{
		if (!policies.ContainsKey(key))
		{
			throw new InvalidDataException("Unknown native policy: " + key);
		}
	}

	private static string GuidanceLabel(int seeker, int correction)
	{
		string text = seeker switch
		{
			6 => "电视", 
			5 => "激光", 
			4 => "反辐射", 
			3 => "主动雷达", 
			2 => "半主动雷达", 
			1 => "红外", 
			_ => "程序飞行", 
		};
		switch (correction)
		{
		case 2:
			if (seeker != 0)
			{
				return "线导＋" + text;
			}
			return "线导";
		case 1:
			if (seeker != 0)
			{
				return "无线指令＋" + text;
			}
			return "无线指令";
		default:
			return text;
		}
	}

	internal static string WeaponRestriction(string id)
	{
		if (id == null || !weapons.TryGetValue(id, out var value))
		{
			return "制导：未收录；数据链：未收录";
		}
		return "制导：" + GuidanceLabel(value.Seeker, value.Correction) + "；数据链：" + ((value.Seeker == 3 && value.Correction != 2 && Relay.AmmoConfigured(id)) ? "支持" : "不支持");
	}

	internal static string WeaponSummary(AmmunitionParameters ap)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected I4, but got Unknown
		if (ap == null)
		{
			return "制导：未知；数据链：未知";
		}
		if (Relay.Ammo(ap))
		{
			return "制导：主动雷达；数据链：支持";
		}
		if (Matches(ap))
		{
			return WeaponRestriction(ap._ammunitionFileName);
		}
		MidCourseCorrection midCourseCorrection = ap._midCourseCorrection;
		int correction = ((midCourseCorrection != null) ? midCourseCorrection.Index : 0);
		return "制导：" + GuidanceLabel((int)ap._guidanceType, correction) + "；数据链：不支持";
	}

	internal static string RadarRestriction(string id)
	{
		bool num = id != null && RemoteGuidance.TransmitterAllowed(id);
		bool flag = id != null && Relay.SensorAllowed(id);
		if (num)
		{
			return "用途：" + (flag ? "搜索/协同上行" : "协同上行") + "；数据链：支持";
		}
		if (id != null && radars.ContainsKey(id))
		{
			return "用途：" + RadarRole(id) + "；数据链：" + (flag ? "支持" : "不支持");
		}
		return "用途：" + (flag ? "搜索/火控" : "未收录") + "；数据链：" + (flag ? "支持" : "不支持");
	}

	internal static string EquipmentSummary(ObjectBase owner, Taskforce player)
	{
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Invalid comparison between Unknown and I4
		if ((Object)(object)owner == (Object)null || player == null || owner._taskforce != player || owner.IsDestroyed || owner._obp == null)
		{
			return null;
		}
		List<string> list = new List<string>();
		Dictionary<AmmunitionParameters, int> dictionary = new Dictionary<AmmunitionParameters, int>();
		if (owner._obp._weaponSystems != null)
		{
			foreach (WeaponSystem weaponSystem in owner._obp._weaponSystems)
			{
				if (weaponSystem?._loadedAmmunitionCount == null)
				{
					continue;
				}
				foreach (KeyValuePair<string, int> item in weaponSystem._loadedAmmunitionCount)
				{
					if (item.Value > 0)
					{
						AmmunitionParameters val = owner.getAmmunitionByName(item.Key)?._ap;
						if (val != null && (int)val._type == 2)
						{
							dictionary.TryGetValue(val, out var value);
							dictionary[val] = value + item.Value;
						}
					}
				}
			}
		}
		if (dictionary.Count > 0)
		{
			list.Add("挂载导弹");
			foreach (KeyValuePair<AmmunitionParameters, int> item2 in dictionary)
			{
				AmmunitionParameters key = item2.Key;
				string text = key._displayedName;
				if (string.IsNullOrWhiteSpace(text))
				{
					text = key._ammunitionFileName;
				}
				list.Add(text + " ×" + item2.Value + " · " + WeaponSummary(key));
			}
		}
		if (owner._obp._sensorSystems != null)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (SensorSystem item3 in (Collection<SensorSystem>)(object)owner._obp._sensorSystems)
			{
				string text2 = ((BaseSystem)(item3?))._systemName;
				if (item3 is SensorSystemRadar && text2 != null && (radars.ContainsKey(text2) || Relay.SensorAllowed(text2) || RemoteGuidance.TransmitterAllowed(text2)) && hashSet.Add(text2))
				{
					if (hashSet.Count == 1 && dictionary.Count > 0)
					{
						list.Add("\n雷达");
					}
					list.Add(text2 + " · " + RadarRestriction(text2));
				}
			}
		}
		if (list.Count != 0)
		{
			return string.Join("\n", list);
		}
		return null;
	}

	internal static bool Matches(AmmunitionParameters ap)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Invalid comparison between Unknown and I4
		if (ap != null && ap._ammunitionFileName != null && weapons.TryGetValue(ap._ammunitionFileName, out var value) && (int)ap._guidanceType == value.Seeker)
		{
			MidCourseCorrection midCourseCorrection = ap._midCourseCorrection;
			if (((midCourseCorrection != null) ? new int?(midCourseCorrection.Index) : null) != value.Correction)
			{
				if (Relay.Ammo(ap))
				{
					return Relay.OriginalCorrection(ap) == value.Correction;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	internal static bool OriginalRadar(string id)
	{
		if (id != null)
		{
			return radars.ContainsKey(id);
		}
		return false;
	}

	internal static bool OriginalWeapon(string id)
	{
		if (id != null)
		{
			return weapons.ContainsKey(id);
		}
		return false;
	}

	internal static string RadarRole(string id)
	{
		if (id == null || !radars.TryGetValue(id, out var value))
		{
			return "原生制导设备";
		}
		switch (value.Role)
		{
		case "LocalUplink":
			return "本平台中段上行";
		case "SearchAndUplink":
			return "搜索/本平台上行";
		case "LocalCommand":
			return "本平台指令火控";
		case "LocalFireControl":
			return "本平台火控/照射";
		case "DirectedRadar":
			return "定向搜索/火控";
		case "EarlyWarningRadar":
			return "预警/态势共享";
		case "TestRadar":
			return "游戏测试设备";
		case "GenericRadar":
			return "游戏通用设备";
		case "NavigationRadar":
			return "导航/海面搜索";
		case "GunFireControl":
			return "炮瞄火控";
		case "HeightFinder":
			return "测高雷达";
		default:
			if (!value.SharesTracks)
			{
				return "本机搜索";
			}
			return "搜索/游戏态势共享";
		}
	}

	internal static void Read(Missile m, ref GuidanceFrame f)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Expected I4, but got Unknown
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Invalid comparison between Unknown and I4
		bool flag = Relay.Ammo(((WeaponBase)m)._ap);
		if (!flag && !Matches(((WeaponBase)m)._ap))
		{
			return;
		}
		f.NativeProfile = true;
		f.Phase = GuidanceStatus.Autonomous(m);
		int index = ((WeaponBase)m)._ap._midCourseCorrection.Index;
		int num = (int)(((WeaponBase)m)._seeker?._guidanceType ?? ((WeaponBase)m)._ap._guidanceType);
		object obj;
		if (num != 5)
		{
			obj = ((WeaponBase)m)._guidingRadarSystem;
		}
		else
		{
			SeekerBase seeker = ((WeaponBase)m)._seeker;
			SeekerBase obj2 = ((seeker is LaserSeeker) ? seeker : null);
			obj = ((obj2 != null) ? ((LaserSeeker)obj2).GuidingDesignator.Value : null);
		}
		SensorSystem val = (SensorSystem)obj;
		f.Director = ((BaseSystem)(val?))._baseObject;
		f.NativeDirector = ((BaseSystem)(val?))._systemName;
		bool flag2 = (int)((WeaponBase)m).getFlightStage() == 14;
		bool num2 = RemoteGuidance.GroupControlled(m);
		bool flag3 = ((WeaponBase)m)._requiresExternalGuidance && (index != 0 || num == 2);
		bool flag4 = ((WeaponBase)m).ConnectionLost.Value || ((WeaponBase)m).ConnectionLostForever.Value;
		if (num == 2 || num == 5)
		{
			flag3 = true;
		}
		if (num2 || (num != 2 && num != 5 && flag2 && ((WeaponBase)m)._ap._localTerminalOnly))
		{
			flag3 = false;
		}
		if (flag3 && index == 2)
		{
			f.Director = ((WeaponBase)m)._launchPlatform;
			f.NativeDirector = "线导控制台";
		}
		if (flag3)
		{
			f.Phase = ((flag4 || (val == null && index != 2)) ? GuidancePhase.NativeLost : GuidancePhase.NativeLink);
		}
		OptionsManager instance = Singleton<OptionsManager>.Instance;
		if (instance != null && instance.WeaponFailuresEnabled && (((WeaponBase)m)._launchMalfunction.Value || ((WeaponBase)m)._seekerMalfunction.Value))
		{
			f.Phase = GuidancePhase.Malfunction;
		}
		f.NativeNote = (flag ? "未接入三平台协同" : "不支持数据链");
		f.NativeState = GuidanceName(num, index, flag3, ((SensorSystem)(((WeaponBase)m)._guidingRadarSystem?))._isDatalinkGuidance ?? false);
		if (f.NativeState == "本平台中段修正" && (Object)(object)f.Director != (Object)(object)((WeaponBase)m)._launchPlatform)
		{
			f.NativeState = "原生中段修正";
		}
		if (!flag3 && num == 3)
		{
			SeekerBase seeker2 = ((WeaponBase)m)._seeker;
			if (seeker2 == null || !seeker2.isActive())
			{
				f.NativeState = "程序飞行·待开机";
			}
		}
		if (num2)
		{
			f.NativeState = "原生导弹编队";
		}
		f.NativeRestriction = WeaponSummary(((WeaponBase)m)._ap);
	}

	private static string GuidanceName(int seeker, int correction, bool external, bool uplink)
	{
		if (external && correction == 2)
		{
			return "原生线导";
		}
		if (external && correction == 1)
		{
			return "原生无线指令";
		}
		if (external && correction == 3 && (seeker != 2 || uplink))
		{
			return "本平台中段修正";
		}
		return seeker switch
		{
			1 => "红外寻的", 
			2 => "半主动雷达制导", 
			3 => "主动雷达寻的", 
			4 => "反辐射寻的", 
			5 => "原生激光制导", 
			6 => "电视寻的", 
			_ => "原生程序飞行", 
		};
	}
}
