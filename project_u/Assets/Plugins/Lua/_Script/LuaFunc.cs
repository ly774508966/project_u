/*
MIT License

Copyright (c) 2016 xiaobin83

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using UnityEngine;
using System.Collections;

namespace lua
{
	public class FuncTools
	{
		public class FuncBase
		{
			protected System.IntPtr L;
			protected int funcRef;

			protected FuncBase(System.IntPtr L)
			{
				this.L = L;
				if (this.L != System.IntPtr.Zero)
					this.funcRef = Api.luaL_ref(L, Api.LUA_REGISTRYINDEX);
			}

			~FuncBase()
			{
				if (L != System.IntPtr.Zero)
					Api.luaL_unref(L, Api.LUA_REGISTRYINDEX, funcRef);
			}

			protected void Invoke(System.Type retType, object[] args)
			{
				if (L == System.IntPtr.Zero)
					return;
					
				if (Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, funcRef)
					== Api.LUA_TFUNCTION)
				{

					for (int i = 0; i < args.Length; ++i)
					{	
						Lua.PushCsharpValue(L, args[i]);
					}
					Lua.Call(L, args.Length, retType != null ? 1 : 0);
				}
			}

		}


		public class Action : FuncBase
		{
			public Action(System.IntPtr L)
				: base(L)
			{
			}

			public virtual void Invoke(params object[] args)
			{
				try
				{
					base.Invoke(null, args);
				}
				catch (System.Exception e)
				{
					Debug.LogError(e.Message);
				}
			}

		}
	

		public class Func<TRet> : FuncBase
		{
			public Func(System.IntPtr L)
				: base(L)
			{
			}

			public virtual TRet Invoke(params object[] args)
			{
				var retType = typeof(TRet);
				try 
				{
					base.Invoke(retType, args);
				}
				catch (System.Exception e)
				{
					Debug.LogError(e.Message);
					return default(TRet);
				}
				var retValue = Lua.CsharpValueFromInternal(L, -1);
				return (TRet)System.Convert.ChangeType(retValue, retType);
			}
		}

		class ActionWrapper : Action
		{
			System.Action action;

			public ActionWrapper(System.Action action)
				: base(System.IntPtr.Zero)
			{
				this.action = action;
			}
			public override void Invoke(params object[] args)
			{
				action();
			}
		}

		class ActionWrapper<T1> : Action
		{
			System.Action<T1> action;

			public ActionWrapper(System.Action<T1> action)
				: base(System.IntPtr.Zero)
			{
				this.action = action;
			}
			public override void Invoke(params object[] args)
			{
				action((T1)args[0]);
			}
		}

		class ActionWrapper<T1, T2> : Action
		{
			System.Action<T1, T2> action;

			public ActionWrapper(System.Action<T1, T2> action)
				: base(System.IntPtr.Zero)
			{
				this.action = action;
			}
			public override void Invoke(params object[] args)
			{
				action((T1)args[0], (T2)args[1]);
			}
		}

		class ActionWrapper<T1, T2, T3> : Action
		{
			System.Action<T1, T2, T3> action;

			public ActionWrapper(System.Action<T1, T2, T3> action)
				: base(System.IntPtr.Zero)
			{
				this.action = action;
			}
			public override void Invoke(params object[] args)
			{
				action((T1)args[0], (T2)args[1], (T3)args[3]);
			}
		}

		class ActionWrapper<T1, T2, T3, T4> : Action
		{
			System.Action<T1, T2, T3, T4> action;

			public ActionWrapper(System.Action<T1, T2, T3, T4> action)
				: base(System.IntPtr.Zero)
			{
				this.action = action;
			}
			public override void Invoke(params object[] args)
			{
				action((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]);
			}
		}

		public static Action Wrap(System.Action action)
		{
			return new ActionWrapper(action);
		}

		public static Action Wrap<T1>(System.Action<T1> action)
		{
			return new ActionWrapper<T1>(action);
		}

		public static Action Wrap<T1, T2>(System.Action<T1, T2> action)
		{
			return new ActionWrapper<T1, T2>(action);
		}

		public static Action Wrap<T1, T2, T3>(System.Action<T1, T2, T3> action)
		{
			return new ActionWrapper<T1, T2, T3>(action);
		}

		public static Action Wrap<T1, T2, T3, T4>(System.Action<T1, T2, T3, T4> action)
		{
			return new ActionWrapper<T1, T2, T3, T4>(action);
		}


		class FuncWrapper<TRet> : Func<TRet>
		{
			System.Func<TRet> func;

			public FuncWrapper(System.Func<TRet> func)
				: base(System.IntPtr.Zero)
			{
				this.func = func;
			}
			public override TRet Invoke(params object[] args)
			{
				return func();
			}
		}

		class FuncWrapper<T1, TRet> : Func<TRet>
		{
			System.Func<T1, TRet> func;

			public FuncWrapper(System.Func<T1, TRet> func)
				: base(System.IntPtr.Zero)
			{
				this.func = func;
			}
			public override TRet Invoke(params object[] args)
			{
				return func((T1)args[0]);
			}
		}

		class FuncWrapper<T1, T2, TRet> : Func<TRet>
		{
			System.Func<T1, T2, TRet> func;

			public FuncWrapper(System.Func<T1, T2, TRet> func)
				: base(System.IntPtr.Zero)
			{
				this.func = func;
			}
			public override TRet Invoke(params object[] args)
			{
				return func((T1)args[0], (T2)args[1]);
			}
		}

		class FuncWrapper<T1, T2, T3, TRet> : Func<TRet>
		{
			System.Func<T1, T2, T3, TRet> func;

			public FuncWrapper(System.Func<T1, T2, T3, TRet> func)
				: base(System.IntPtr.Zero)
			{
				this.func = func;
			}
			public override TRet Invoke(params object[] args)
			{
				return func((T1)args[0], (T2)args[1], (T3)args[2] );
			}
		}

		class FuncWrapper<T1, T2, T3, T4, TRet> : Func<TRet>
		{
			System.Func<T1, T2, T3, T4, TRet> func;

			public FuncWrapper(System.Func<T1, T2, T3, T4, TRet> func)
				: base(System.IntPtr.Zero)
			{
				this.func = func;
			}
			public override TRet Invoke(params object[] args)
			{
				return func((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]);
			}
		}

		public static Func<TRet> Wrap<TRet>(System.Func<TRet> func)
		{
			return new FuncWrapper<TRet>(func);
		}

		public static Func<TRet> Wrap<T1, TRet>(System.Func<T1, TRet> func)
		{
			return new FuncWrapper<T1, TRet>(func);
		}

		public static Func<TRet> Wrap<T1, T2, TRet>(System.Func<T1, T2, TRet> func)
		{
			return new FuncWrapper<T1, T2, TRet>(func);
		}

		public static Func<TRet> Wrap<T1, T2, T3, TRet>(System.Func<T1, T2, T3, TRet> func)
		{
			return new FuncWrapper<T1, T2, T3, TRet>(func);
		}

		public static Func<TRet> Wrap<T1, T2, T3, T4, TRet>(System.Func<T1, T2, T3, T4, TRet> func)
		{
			return new FuncWrapper<T1, T2, T3, T4, TRet>(func);
		}


		// create Lua func for invocation in C# [-1|0|-]
		public static object CreateFuncObject(System.Type delegateType, System.IntPtr L)
		{
			var ctors = delegateType.GetConstructors();
			var c = ctors[0];
			return c.Invoke(null, new object[] { L });
		}
	}


}
