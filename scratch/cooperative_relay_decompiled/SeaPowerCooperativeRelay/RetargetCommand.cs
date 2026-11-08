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

[HarmonyPatch(typeof(Missile), "SetNewTarget")]
internal static class RetargetCommand
{
	[CompilerGenerated]
	private sealed class _003CTranspiler_003Ed__0 : IEnumerable<CodeInstruction>, IEnumerable, IEnumerator<CodeInstruction>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private CodeInstruction _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private IEnumerable<CodeInstruction> instructions;

		public IEnumerable<CodeInstruction> _003C_003E3__instructions;

		private MethodInfo _003Csetter_003E5__2;

		private MethodInfo _003Cassign_003E5__3;

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
		public _003CTranspiler_003Ed__0(int _003C_003E1__state)
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
			_003Csetter_003E5__2 = null;
			_003Cassign_003E5__3 = null;
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
					_003Csetter_003E5__2 = AccessTools.PropertySetter(typeof(WeaponBase), "CurrentIntendedTargetObject");
					_003Cassign_003E5__3 = AccessTools.Method(typeof(RemoteGuidance), "AssignRetarget", (Type[])null, (Type[])null);
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
					if (CodeInstructionExtensions.Calls(current, _003Csetter_003E5__2))
					{
						current.opcode = OpCodes.Call;
						current.operand = _003Cassign_003E5__3;
						_003Ccount_003E5__4++;
					}
					_003C_003E2__current = current;
					_003C_003E1__state = 1;
					return true;
				}
				_003C_003Em__Finally1();
				_003C_003E7__wrap4 = null;
				if (_003Ccount_003E5__4 != 5)
				{
					throw new InvalidOperationException("Native missile retarget assignments changed");
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
			_003CTranspiler_003Ed__0 _003CTranspiler_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CTranspiler_003Ed__ = this;
			}
			else
			{
				_003CTranspiler_003Ed__ = new _003CTranspiler_003Ed__0(0);
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

	[IteratorStateMachine(typeof(_003CTranspiler_003Ed__0))]
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CTranspiler_003Ed__0(-2)
		{
			_003C_003E3__instructions = instructions
		};
	}
}
