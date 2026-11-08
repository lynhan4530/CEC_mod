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

[HarmonyPatch]
internal static class CommandedSteering
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
				_003C_003E2__current = AccessTools.Method(typeof(Missile), "SetMissileCourse", (Type[])null, (Type[])null);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				_003C_003E2__current = AccessTools.Method(typeof(Missile), "CalculateInterceptAimPoint", (Type[])null, (Type[])null);
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

	[CompilerGenerated]
	private sealed class _003CTranspiler_003Ed__1 : IEnumerable<CodeInstruction>, IEnumerable, IEnumerator<CodeInstruction>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private CodeInstruction _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private MethodBase __originalMethod;

		public MethodBase _003C_003E3____originalMethod;

		private IEnumerable<CodeInstruction> instructions;

		public IEnumerable<CodeInstruction> _003C_003E3__instructions;

		private MethodInfo _003Cactive_003E5__2;

		private FieldInfo _003Ccountermeasure_003E5__3;

		private bool _003Ccourse_003E5__4;

		private int _003Ccount_003E5__5;

		private IEnumerator<CodeInstruction> _003C_003E7__wrap5;

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
			_003Cactive_003E5__2 = null;
			_003Ccountermeasure_003E5__3 = null;
			_003C_003E7__wrap5 = null;
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
					_003Cactive_003E5__2 = AccessTools.Method(typeof(SeekerBase), "isActive", (Type[])null, (Type[])null);
					_003Ccountermeasure_003E5__3 = AccessTools.Field(typeof(SeekerBase), "_lockedOnCountermeasure");
					_003Ccourse_003E5__4 = __originalMethod.Name == "SetMissileCourse";
					_003Ccount_003E5__5 = 0;
					_003C_003E7__wrap5 = instructions.GetEnumerator();
					_003C_003E1__state = -3;
					break;
				case 1:
					_003C_003E1__state = -3;
					break;
				}
				if (_003C_003E7__wrap5.MoveNext())
				{
					CodeInstruction current = _003C_003E7__wrap5.Current;
					if (_003Ccourse_003E5__4 ? CodeInstructionExtensions.Calls(current, _003Cactive_003E5__2) : CodeInstructionExtensions.LoadsField(current, _003Ccountermeasure_003E5__3, false))
					{
						current.opcode = OpCodes.Call;
						current.operand = AccessTools.Method(typeof(TargetGuard), _003Ccourse_003E5__4 ? "SteeringSeekerActive" : "SteeringCountermeasure", (Type[])null, (Type[])null);
						_003Ccount_003E5__5++;
					}
					_003C_003E2__current = current;
					_003C_003E1__state = 1;
					return true;
				}
				_003C_003Em__Finally1();
				_003C_003E7__wrap5 = null;
				if (_003Ccount_003E5__5 != (_003Ccourse_003E5__4 ? 1 : 3))
				{
					throw new InvalidOperationException("Native seeker steering gate changed: " + __originalMethod.Name);
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
			if (_003C_003E7__wrap5 != null)
			{
				_003C_003E7__wrap5.Dispose();
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
			_003CTranspiler_003Ed__.__originalMethod = _003C_003E3____originalMethod;
			return _003CTranspiler_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<CodeInstruction>)this).GetEnumerator();
		}
	}

	[IteratorStateMachine(typeof(_003CTargetMethods_003Ed__0))]
	private static IEnumerable<MethodBase> TargetMethods()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CTargetMethods_003Ed__0(-2);
	}

	[IteratorStateMachine(typeof(_003CTranspiler_003Ed__1))]
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CTranspiler_003Ed__1(-2)
		{
			_003C_003E3__instructions = instructions,
			_003C_003E3____originalMethod = __originalMethod
		};
	}
}
