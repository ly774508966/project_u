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
using System;

namespace lua
{
	public class FuncTools
	{
		public class FuncBase
		{
			protected IntPtr L;
			protected int funcRef;

			protected FuncBase(IntPtr L)
			{
				this.L = L;
				this.funcRef = Api.luaL_ref(L, Api.LUA_REGISTRYINDEX);
			}

			~FuncBase()
			{
				Api.luaL_unref(L, Api.LUA_REGISTRYINDEX, funcRef);
			}

			protected void Invoke(Type retType, object[] args)
			{
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
			public Action(IntPtr L)
				: base(L)
			{
			}

			public void Invoke(params object[] args)
			{
				try
				{
					base.Invoke(null, args);
				}
				catch (Exception e)
				{
					Debug.LogError(e.Message);
				}
			}

		}
	

		public class Func<TRet> : FuncBase
		{
			public Func(IntPtr L)
				: base(L)
			{
			}

			public TRet Invoke(params object[] args)
			{
				var retType = typeof(TRet);
				try 
				{
					base.Invoke(retType, args);
				}
				catch (Exception e)
				{
					Debug.LogError(e.Message);
					return default(TRet);
				}
				var retValue = Lua.CsharpValueFrom(L, -1);
				return (TRet)System.Convert.ChangeType(retValue, retType);
			}
		}


		// create Lua func for invocation in C# [-1|0|-]
		public static object CreateFuncObject(Type delegateType, IntPtr L)
		{
			var ctors = delegateType.GetConstructors();
			var c = ctors[0];
			return c.Invoke(null, new object[] { L });
		}
	}


}
