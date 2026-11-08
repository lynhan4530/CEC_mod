using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch]
internal static class DirectorLookup
{
	[CompilerGenerated]
	private sealed class _003CTargetMethods_003Ed__0 : IEnumerable<MethodBase>, IEnumerable, IEnumerator<MethodBase>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private MethodBase _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private string[] _003C_003E7__wrap1;

		private int _003C_003E7__wrap2;

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
			_003C_003E7__wrap1 = null;
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
				_003C_003E2__current = AccessTools.Method(typeof(Missile), "CheckRadioConnection", (Type[])null, (Type[])null);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				_003C_003E7__wrap1 = new string[3] { "GoodConnection", "BadConnection", "LostConnection" };
				_003C_003E7__wrap2 = 0;
				break;
			case 2:
				_003C_003E1__state = -1;
				_003C_003E7__wrap2++;
				break;
			}
			if (_003C_003E7__wrap2 < _003C_003E7__wrap1.Length)
			{
				string text = _003C_003E7__wrap1[_003C_003E7__wrap2];
				_003C_003E2__current = AccessTools.Method(typeof(ObjectMissileControlViewModel), text, (Type[])null, (Type[])null);
				_003C_003E1__state = 2;
				return true;
			}
			_003C_003E7__wrap1 = null;
			return false;
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

		private IEnumerable<CodeInstruction> instructions;

		public IEnumerable<CodeInstruction> _003C_003E3__instructions;

		private MethodBase __originalMethod;

		public MethodBase _003C_003E3____originalMethod;

		private FieldInfo _003Cfield_003E5__2;

		private MethodInfo _003Clookup_003E5__3;

		private MethodInfo _003Cintended_003E5__4;

		private int _003Creplaced_003E5__5;

		private int _003Ctargets_003E5__6;

		private IEnumerator<CodeInstruction> _003C_003E7__wrap6;

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
			_003Clookup_003E5__3 = null;
			_003Cintended_003E5__4 = null;
			_003C_003E7__wrap6 = null;
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
					_003Cfield_003E5__2 = AccessTools.Field(typeof(WeaponBase), "_launchPlatform");
					_003Clookup_003E5__3 = AccessTools.Method(typeof(Relay), "RadioOwner", (Type[])null, (Type[])null);
					_003Cintended_003E5__4 = AccessTools.PropertyGetter(typeof(WeaponBase), "CurrentIntendedTargetObject");
					_003Creplaced_003E5__5 = 0;
					_003Ctargets_003E5__6 = 0;
					_003C_003E7__wrap6 = instructions.GetEnumerator();
					_003C_003E1__state = -3;
					break;
				case 1:
					_003C_003E1__state = -3;
					break;
				}
				if (_003C_003E7__wrap6.MoveNext())
				{
					CodeInstruction current = _003C_003E7__wrap6.Current;
					if (CodeInstructionExtensions.LoadsField(current, _003Cfield_003E5__2, false))
					{
						current.opcode = OpCodes.Call;
						current.operand = _003Clookup_003E5__3;
						_003Creplaced_003E5__5++;
					}
					else if (__originalMethod.Name == "GoodConnection" && CodeInstructionExtensions.Calls(current, _003Cintended_003E5__4))
					{
						current.opcode = OpCodes.Call;
						current.operand = AccessTools.Method(typeof(TargetGuard), "DisplayTarget", (Type[])null, (Type[])null);
						_003Ctargets_003E5__6++;
					}
					_003C_003E2__current = current;
					_003C_003E1__state = 1;
					return true;
				}
				_003C_003Em__Finally1();
				_003C_003E7__wrap6 = null;
				if (_003Creplaced_003E5__5 == 0)
				{
					throw new InvalidOperationException("Native radio owner lookup changed");
				}
				if (__originalMethod.Name == "GoodConnection" && _003Ctargets_003E5__6 != 2)
				{
					throw new InvalidOperationException("Native displayed target lookup changed");
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
			if (_003C_003E7__wrap6 != null)
			{
				_003C_003E7__wrap6.Dispose();
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
