using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch]
internal static class RemoteVisibility
{
	[CompilerGenerated]
	private sealed class _003CTargetMethods_003Ed__0 : IEnumerable<MethodBase>, IEnumerable, IEnumerator<MethodBase>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private MethodBase _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		MethodBase IEnumerator<MethodBase>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CTargetMethods_003Ed__0(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			switch (_003C_003E1__state)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E2__current = AccessTools.Method(typeof(ObjectBase), "DoesAmmoMatchTarget", new Type[4]
				{
					typeof(AmmunitionParameters),
					typeof(ObjectBase),
					typeof(bool).MakeByRefType(),
					typeof(bool)
				}, (Type[])null);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				_003C_003E2__current = AccessTools.Method(typeof(ObjectBase), "HandleEngageTasks", (Type[])null, (Type[])null);
				_003C_003E1__state = 2;
				return true;
			case 2:
				_003C_003E1__state = -1;
				return false;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<MethodBase> IEnumerable<MethodBase>.GetEnumerator()
		{
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				return this;
			}
			return new _003CTargetMethods_003Ed__0(0);
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<MethodBase>)this).GetEnumerator();
		}
	}

	[IteratorStateMachine(typeof(_003CTargetMethods_003Ed__0))]
	private static IEnumerable<MethodBase> TargetMethods()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CTargetMethods_003Ed__0(-2);
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
	{
		List<CodeInstruction> list = instructions.ToList();
		bool flag = __originalMethod.Name == "HandleEngageTasks";
		ReplaceQuery(list, "CantSeeAirTarget", "CantSee", flag);
		if (flag)
		{
			ReplaceQuery(list, "GetLocalAirTargetVisibilityRange", "VisibilityForRadarWake", task: true);
		}
		return list;
	}

	private static void ReplaceQuery(List<CodeInstruction> code, string source, string replacement, bool task)
	{
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Expected O, but got Unknown
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Expected O, but got Unknown
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Expected O, but got Unknown
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Expected O, but got Unknown
		MethodInfo check = AccessTools.Method(typeof(ObjectBase), source, (Type[])null, (Type[])null);
		int num = code.FindIndex((CodeInstruction i) => CodeInstructionExtensions.Calls(i, check));
		if (num < 0 || code.Count((CodeInstruction i) => CodeInstructionExtensions.Calls(i, check)) != 1)
		{
			throw new InvalidOperationException("Visibility call changed");
		}
		List<CodeInstruction> list = new List<CodeInstruction>();
		if (task)
		{
			FieldInfo target = AccessTools.Field(typeof(EngageTask), "_targetObject");
			int num2 = code.FindLastIndex(num, (CodeInstruction i) => CodeInstructionExtensions.LoadsField(i, target, false));
			if (num2 < 1 || num - num2 > 8 || !CodeInstructionExtensions.IsLdloc(code[num2 - 1], (LocalBuilder)null))
			{
				throw new InvalidOperationException("EngageTask local changed");
			}
			list.Add(new CodeInstruction(code[num2 - 1].opcode, code[num2 - 1].operand));
			list.Add(new CodeInstruction(OpCodes.Ldfld, (object)AccessTools.Field(typeof(EngageTask), "_ammunition")));
			list.Add(new CodeInstruction(OpCodes.Ldfld, (object)AccessTools.Field(typeof(Ammunition), "_ap")));
		}
		else
		{
			list.Add(new CodeInstruction(OpCodes.Ldarg_1, (object)null));
		}
		list[0].labels.AddRange(code[num].labels);
		code[num].labels.Clear();
		code[num].opcode = OpCodes.Call;
		code[num].operand = AccessTools.Method(typeof(RemoteVisibility), replacement, (Type[])null, (Type[])null);
		code.InsertRange(num, list);
	}

	private static float VisibilityForRadarWake(ObjectBase unit, ObjectBase target, SensorRangeOptions options, bool excludeRadars, AmmunitionParameters ap)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		if (!Network.Designated(unit, ap, target))
		{
			return unit.GetLocalAirTargetVisibilityRange(target, options, excludeRadars);
		}
		return float.MaxValue;
	}

	private static bool CantSee(ObjectBase unit, ObjectBase target, SensorRangeOptions options, AmmunitionParameters ap)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		if (!Network.HasExternalTrack(unit, ap, target))
		{
			return unit.CantSeeAirTarget(target, options);
		}
		return false;
	}
}
