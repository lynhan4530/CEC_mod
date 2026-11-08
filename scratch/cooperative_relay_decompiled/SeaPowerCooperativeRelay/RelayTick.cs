using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(Missile), "OnUpdateEveryFrame")]
internal static class RelayTick
{
	[CompilerGenerated]
	private sealed class _003CTranspiler_003Ed__1 : IEnumerable<CodeInstruction>, IEnumerable, IEnumerator<CodeInstruction>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private CodeInstruction _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private IEnumerable<CodeInstruction> instructions;

		public IEnumerable<CodeInstruction> _003C_003E3__instructions;

		private FieldInfo _003Cfield_003E5__2;

		private MethodInfo _003Cmethod_003E5__3;

		private int _003Ccount_003E5__4;

		private IEnumerator<CodeInstruction> _003C_003E7__wrap4;

		CodeInstruction IEnumerator<CodeInstruction>.Current
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
		public _003CTranspiler_003Ed__1(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			int num = _003C_003E1__state;
			if (num == -3 || num == 1)
			{
				try
				{
				}
				finally
				{
					_003C_003Em__Finally1();
				}
			}
			_003Cfield_003E5__2 = null;
			_003Cmethod_003E5__3 = null;
			_003C_003E7__wrap4 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			try
			{
				switch (_003C_003E1__state)
				{
				default:
					return false;
				case 0:
					_003C_003E1__state = -1;
					_003Cfield_003E5__2 = AccessTools.Field(typeof(WeaponBase), "_requiresExternalGuidance");
					_003Cmethod_003E5__3 = AccessTools.Method(typeof(RemoteGuidance), "RequiresNativeRadio", (Type[])null, (Type[])null);
					_003Ccount_003E5__4 = 0;
					_003C_003E7__wrap4 = instructions.GetEnumerator();
					_003C_003E1__state = -3;
					break;
				case 1:
					_003C_003E1__state = -3;
					break;
				}
				if (_003C_003E7__wrap4.MoveNext())
				{
					CodeInstruction current = _003C_003E7__wrap4.Current;
					if (CodeInstructionExtensions.LoadsField(current, _003Cfield_003E5__2, false))
					{
						current.opcode = OpCodes.Call;
						current.operand = _003Cmethod_003E5__3;
						_003Ccount_003E5__4++;
					}
					_003C_003E2__current = current;
					_003C_003E1__state = 1;
					return true;
				}
				_003C_003Em__Finally1();
				_003C_003E7__wrap4 = null;
				if (_003Ccount_003E5__4 != 1)
				{
					throw new InvalidOperationException("Missile frame guidance gate changed");
				}
				return false;
			}
			catch
			{
				//try-fault
				((IDisposable)this).Dispose();
				throw;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		private void _003C_003Em__Finally1()
		{
			_003C_003E1__state = -1;
			if (_003C_003E7__wrap4 != null)
			{
				_003C_003E7__wrap4.Dispose();
			}
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<CodeInstruction> IEnumerable<CodeInstruction>.GetEnumerator()
		{
			_003CTranspiler_003Ed__1 _003CTranspiler_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CTranspiler_003Ed__ = this;
			}
			else
			{
				_003CTranspiler_003Ed__ = new _003CTranspiler_003Ed__1(0);
			}
			_003CTranspiler_003Ed__.instructions = _003C_003E3__instructions;
			return _003CTranspiler_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<CodeInstruction>)this).GetEnumerator();
		}
	}

	private static void Prefix(Missile __instance)
	{
		Relay.Update(__instance);
	}

	[IteratorStateMachine(typeof(_003CTranspiler_003Ed__1))]
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CTranspiler_003Ed__1(-2)
		{
			_003C_003E3__instructions = instructions
		};
	}

	private static void Postfix(Missile __instance)
	{
		Relay.Confirm(__instance);
	}
}
