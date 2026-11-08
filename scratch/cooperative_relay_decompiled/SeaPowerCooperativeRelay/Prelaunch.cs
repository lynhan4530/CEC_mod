using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch]
internal static class Prelaunch
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
				_003C_003E2__current = AccessTools.Method(typeof(WeaponSystemHardpoint), "HandlePreLaunchAlignment", (Type[])null, (Type[])null);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				_003C_003E2__current = AccessTools.Method(typeof(WeaponSystem), "hasFreeWeaponChannel", (Type[])null, (Type[])null);
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

	private static bool Prefix(WeaponSystem __instance, ref bool __result)
	{
		if (!(__instance is WeaponSystemHardpoint) || !Network.Designated(((BaseSystem)__instance)._baseObject, __instance._ammoForEngage?._ap, __instance._targetObject))
		{
			return true;
		}
		__result = Network.Ready(__instance);
		__instance._sensorPreLaunchAligned = __result;
		return false;
	}
}
