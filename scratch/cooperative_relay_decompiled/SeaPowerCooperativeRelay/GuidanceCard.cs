using System;
using System.Globalization;
using SeaPower;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

public sealed class GuidanceCard
{
	public string WeaponId { get; }

	public string State { get; }

	public string Tone { get; }

	public string Detail { get; }

	public string SourceRole { get; }

	public string SourceName { get; }

	public string DirectorRole { get; }

	public string DirectorName { get; }

	public string WeaponRole { get; }

	public string WeaponName { get; }

	public string Shooter { get; }

	public string Note { get; }

	public string HopDetail { get; }

	public string Notice { get; }

	public string FirstLeg { get; }

	public string LastLeg { get; }

	public bool Remote { get; }

	public bool Seeker { get; }

	public bool ExternalActive { get; }

	public bool SourceActive { get; }

	public bool DirectorActive { get; }

	public string TargetLabel { get; }

	public string TargetBrief { get; }

	public string SeekerLabel { get; }

	public string TargetStatus { get; }

	public bool TargetWarning { get; }

	public bool Selected { get; }

	public string WeaponLabel { get; }

	public string SourceLabel { get; }

	public string DirectorLabel { get; }

	public string CompactState { get; }

	public string Age { get; }

	public string Alert { get; }

	public string GapReason { get; }

	public string ControlHint { get; }

	internal GuidanceCard(GuidanceFrame f, bool selected = false)
	{
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Invalid comparison between Unknown and I4
		//IL_0348: Unknown result type (might be due to invalid IL or missing references)
		//IL_034f: Invalid comparison between Unknown and I4
		Missile weapon = f.Weapon;
		Selected = selected;
		TargetLabel = "指令  " + f.CommandLabel;
		if (!string.IsNullOrEmpty(f.CommandLabel) && f.CommandLabel != "—")
		{
			int num = f.CommandLabel.IndexOf(" · ", StringComparison.Ordinal);
			TargetBrief = "→ " + ((num < 0) ? f.CommandLabel : f.CommandLabel.Substring(0, num));
		}
		SeekerLabel = "导引头  " + (((Object)(object)f.SeekerTarget != (Object)null) ? f.EchoLabel : (f.SeekerOn ? "搜索中" : "未开机"));
		if (f.Guarded)
		{
			TargetStatus = (f.SeekerControl ? "回波匹配 · 已接管" : (((int)((WeaponBase)weapon).getFlightStage() == 14) ? "按末次航迹搜索" : ((f.Phase == GuidancePhase.Coasting) ? "按末次航迹续飞" : ((f.Phase == GuidancePhase.NoReport) ? "等待可用航迹" : ((f.Phase == GuidancePhase.Verifying || f.Phase == GuidancePhase.PendingRouteCheck) ? "等待链路确认" : "按中继航迹飞行")))));
			if (f.SeekerControl && TargetGuard.Countermeasure(f.SeekerTarget))
			{
				TargetStatus = "导引头跟踪非指令回波";
				TargetWarning = true;
			}
			if ((Object)(object)f.RejectedTarget != (Object)null)
			{
				TargetWarning = true;
				TargetStatus = "已拦截不匹配目标  " + f.RejectedLabel;
				if ((Object)(object)f.SeekerTarget == (Object)null)
				{
					SeekerLabel = "已拦截  " + f.RejectedLabel;
				}
			}
		}
		WeaponId = GuidanceStatus.Id((ObjectBase)(object)weapon);
		Remote = f.Remote;
		SourceRole = (Remote ? "A  跟踪" : "B  发射");
		SourceName = GuidanceStatus.Name(Remote ? f.Source : ((WeaponBase)weapon)._launchPlatform);
		DirectorRole = (Remote ? "C  中继" : "制导雷达");
		DirectorName = GuidanceStatus.Name(f.Director);
		WeaponName = GuidanceStatus.Name((ObjectBase)(object)weapon);
		Shooter = (Remote ? ("B 发射 · " + GuidanceStatus.Station(((WeaponBase)weapon)._launchPlatform)) : "常规发射");
		WeaponRole = "导弹";
		FirstLeg = (Remote ? "idle" : "hidden");
		LastLeg = "idle";
		Tone = "neutral";
		if (Remote && f.ReportAge >= 0)
		{
			Detail = "观测 " + f.ReportAge + " 秒前";
		}
		Notice = f.Notice;
		switch (f.Phase)
		{
		case GuidancePhase.Uplink:
			State = "中继制导";
			Tone = "live";
			ExternalActive = true;
			FirstLeg = (LastLeg = "live");
			if ((int)((WeaponBase)weapon).getFlightStage() == 14 && ((WeaponBase)weapon)._seeker.isActive())
			{
				WeaponRole = "导弹 · 搜索中";
			}
			Note = (f.AwaitingNewSource ? "待新来源更新" : (f.DirectorSearchOff ? "C 关雷达上行" : (((Object)(object)f.Source == (Object)(object)f.Director) ? "A、C 同平台" : (f.DirectorEcho ? "C 有本机回波" : "C 无本机回波"))));
			break;
		case GuidancePhase.NativeLink:
			ExternalActive = true;
			Tone = "live";
			LastLeg = "live";
			State = (((Object)(object)f.Director == (Object)(object)((WeaponBase)weapon)._launchPlatform) ? "发射平台制导" : "友军接替制导");
			break;
		case GuidancePhase.Coasting:
		case GuidancePhase.NoReport:
			State = ((f.Phase == GuidancePhase.Coasting) ? "断链续飞" : "等待航迹");
			Tone = "warn";
			Notice = null;
			switch (f.Gap)
			{
			case RemoteGuidance.Gap.Track:
				Note = ((f.Phase == GuidancePhase.Coasting) ? "目标航迹丢失" : "等待首份航迹");
				break;
			case RemoteGuidance.Gap.Transmitter:
				Note = "无可用中继";
				break;
			case RemoteGuidance.Gap.Channel:
				Note = "中继通道不可用";
				break;
			case RemoteGuidance.Gap.Upstream:
			case RemoteGuidance.Gap.Downstream:
			{
				bool flag = f.Gap == RemoteGuidance.Gap.Downstream;
				string text = (flag ? "C→导弹 " : "A→C ");
				Note = text + "待确认";
				if (flag && f.UpstreamLive)
				{
					FirstLeg = "live";
				}
				if (f.HopStatus == RemoteGuidance.HopStatus.OutOfRange)
				{
					Note = text + "超距";
					HopDetail = (((Object)(object)f.Director == (Object)null) ? "最近候选链路" : "") + RangeDetail(f.HopDistanceNM, f.HopLimitNM);
				}
				else
				{
					if (f.HopStatus != RemoteGuidance.HopStatus.Blocked)
					{
						break;
					}
					Note = text + "视距受阻";
				}
				if ((Object)(object)f.Source != (Object)null && (Object)(object)f.Director != (Object)null)
				{
					if (flag)
					{
						LastLeg = "broken";
					}
					else
					{
						FirstLeg = "broken";
					}
				}
				break;
			}
			default:
				Note = "无完整通路";
				break;
			}
			DirectorRole = (f.DirectorReady ? "C 上行就绪" : "C 上次中继");
			SourceRole = (f.SourceLive ? "A 跟踪正常" : "A 上次跟踪");
			break;
		case GuidancePhase.NativeLost:
			State = "制导链路中断";
			Tone = "warn";
			LastLeg = "broken";
			break;
		case GuidancePhase.Verifying:
			State = "等待链路确认";
			Note = "等待在途校验";
			break;
		case GuidancePhase.PendingRouteCheck:
			State = "链路待复核";
			Note = "参与设置已更改";
			Notice = null;
			break;
		case GuidancePhase.Terminal:
		case GuidancePhase.Searching:
		case GuidancePhase.Locked:
			Seeker = true;
			Tone = "seeker";
			WeaponRole = "导引头";
			State = ((f.Phase == GuidancePhase.Locked) ? "末段自寻 · 已锁定" : ((f.Phase == GuidancePhase.Searching) ? "末段自寻 · 搜索中" : "末段自主"));
			if (Remote)
			{
				SourceRole = "A 上次跟踪";
				DirectorRole = "C 上次中继";
			}
			Note = "外部制导结束";
			Detail = null;
			Notice = null;
			break;
		case GuidancePhase.Malfunction:
			State = "武器故障";
			Tone = "warn";
			Detail = null;
			Notice = null;
			break;
		default:
			State = "游戏原生制导";
			Detail = null;
			Notice = null;
			break;
		}
		if (f.NativeProfile)
		{
			State = ((f.Phase == GuidancePhase.Malfunction) ? "武器故障" : ((f.Phase == GuidancePhase.NativeLost) ? "原生制导链路中断" : f.NativeState));
			Note = f.NativeNote;
			TargetStatus = f.NativeRestriction;
			Seeker = ((WeaponBase)weapon)._seeker != null && ((WeaponBase)weapon)._seeker.isActive();
			ExternalActive = f.Phase == GuidancePhase.NativeLink;
			FirstLeg = "hidden";
			LastLeg = (ExternalActive ? "live" : ((f.Phase == GuidancePhase.NativeLost) ? "broken" : "hidden"));
			DirectorRole = "原生设备";
			DirectorName = GuidanceStatus.Name(f.Director);
			if (!ExternalActive && f.Phase != GuidancePhase.NativeLost)
			{
				f.Director = null;
			}
			if (!ExternalActive && f.Phase != GuidancePhase.NativeLost && f.Phase != GuidancePhase.Malfunction)
			{
				Tone = (Seeker ? "seeker" : "neutral");
			}
			Detail = null;
			Notice = null;
		}
		ObjectBase val = (Remote ? f.Source : ((WeaponBase)weapon)._launchPlatform);
		if ((Object)(object)val != (Object)null)
		{
			SourceRole = SourceRole + "  " + GuidanceStatus.Id(val);
		}
		if ((Object)(object)f.Director != (Object)null)
		{
			DirectorRole = DirectorRole + "  " + GuidanceStatus.Id(f.Director);
		}
		if (TargetWarning)
		{
			Tone = "warn";
		}
		WeaponLabel = WeaponId + " · " + WeaponName;
		bool flag2 = Seeker || f.Phase == GuidancePhase.Coasting || f.Phase == GuidancePhase.NoReport;
		SourceActive = (Remote ? f.SourceLive : ExternalActive);
		DirectorActive = (Remote ? f.DirectorReady : ExternalActive);
		SourceLabel = ((!Remote) ? "B " : ((flag2 && !SourceActive) ? "上次A " : "A ")) + TargetText(val);
		DirectorLabel = ((!Remote) ? "制导 " : ((flag2 && !DirectorActive) ? "上次C " : "C ")) + TargetText(f.Director);
		if (f.NativeProfile)
		{
			DirectorLabel = ((LastLeg == "hidden") ? "外部支持 —" : ((f.NativeDirector ?? "原生设备") + " " + GuidanceStatus.Id(f.Director)));
		}
		CompactState = (f.NativeProfile ? State : ((f.Phase == GuidancePhase.Locked) ? "末段锁定" : ((f.Phase == GuidancePhase.Searching) ? "末段搜索" : State)));
		Age = ((Detail == null) ? null : (f.ReportAge + "秒前"));
		Alert = (TargetWarning ? TargetStatus : Notice);
		if (f.Phase == GuidancePhase.Coasting || f.Phase == GuidancePhase.NoReport)
		{
			GapReason = Note;
		}
		ControlHint = Shooter + "\n" + SourceRole + " " + SourceName + ((Remote && SourceActive) ? (" · 当前航迹 " + f.SourceAge + " 秒前") : "") + "\n" + DirectorRole + " " + DirectorName + ((Remote && DirectorActive && f.DirectorSearchOff) ? " · 搜索雷达关闭，数据链开启" : "") + "\n" + ((Remote && flag2 && !Seeker) ? (Note + ((HopDetail == null) ? "" : (" · " + HopDetail)) + "\n") : "") + ((Remote && f.ReportAge >= 0) ? ("导弹最后收到更新：" + f.ReportAge + " 秒前\n") : "") + TargetStatus;
	}

	private static string RangeDetail(float distance, float limit)
	{
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		string text = limit.ToString("R", invariantCulture);
		if (distance <= limit)
		{
			return "距离约 " + text + " 海里，略超上限 " + text + " 海里";
		}
		double num = Math.Round(distance, 1);
		string text2 = ((num > (double)limit) ? num.ToString("0.#", invariantCulture) : distance.ToString("R", invariantCulture));
		return "距离 " + text2 + " 海里 · 上限 " + text + " 海里";
	}

	private static string TargetText(ObjectBase target)
	{
		if (!((Object)(object)target == (Object)null))
		{
			return GuidanceStatus.Id(target) + " · " + GuidanceStatus.Name(target);
		}
		return "—";
	}
}
