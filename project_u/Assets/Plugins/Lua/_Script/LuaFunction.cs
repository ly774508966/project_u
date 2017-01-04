using UnityEngine;
using System;

namespace lua
{
	public class LuaFunction : IDisposable 
	{
		Lua L_;
		int funcRef = Api.LUA_NOREF;

		LuaFunction(Lua L, int index)
		{
			L_ = L;
			funcRef = L.MakeRefAt(index);
		}

		public void Dispose()
		{
			if (L_.valid && funcRef != Api.LUA_NOREF)
			{
				L_.Unref(funcRef);
			}
		}

		Lua CheckValid()
		{
			if (L_.valid)
				return L_;
			throw new System.InvalidOperationException("Lua vm already destroyed.");
		}

		internal void Push()
		{
			var L = CheckValid();
			L.PushRef(funcRef);
		}

		public void Invoke(LuaTable target = null, params object[] args)
		{
			InvokeInternal(target, 0, args);
		}

		public object Invoke1(LuaTable target = null, params object[] args)
		{
			return InvokeInternal(target, 1, args);
		}

		public LuaTable InvokeMultiRet(LuaTable target = null, params object[] args)
		{
			return (LuaTable)InvokeInternal(target, Api.LUA_MULTRET, args);
		}

		object InvokeInternal(LuaTable target, int nrets, params object[] args)
		{
			var L = CheckValid();
			var top = Api.lua_gettop(L);
			try
			{
				Push();
				int self = 0;
				if (target != null)
				{
					target.Push();
					self = 1;
				}
				for (int i = 0; i < args.Length; ++i)
				{
					L.PushValue(args[i]);
				}
				L.Call(self + args.Length, nrets);
				if (nrets == 0)
				{
					return null;
				}
				else if (nrets == 1)
				{
					var ret = L.ValueAt(-1);
					Api.lua_settop(L, top);
					return ret;
				}
				else
				{
					nrets = Api.lua_gettop(L) - top;
					LuaTable ret = null;
					
					Api.lua_createtable(L, nrets, 0);
					for (int i = 0; i < nrets; ++i)
					{
						Api.lua_pushvalue(L, top + i + 1);
						Api.lua_seti(L, -2, i + 1);
					}
					ret = LuaTable.MakeRefTo(L, -1);
					Api.lua_settop(L, top);
					return ret;
				}
			}
			catch (Exception e)
			{
				Api.lua_settop(L, top);
				throw e;
			}
		}

		public static LuaFunction MakeRefTo(Lua L, int idx)
		{
			Debug.Assert(Api.lua_isfunction(L, idx));
			return new LuaFunction(L, idx);
		}
	
	}
}
