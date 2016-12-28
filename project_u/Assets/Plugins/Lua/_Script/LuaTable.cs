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
﻿using System.Collections;
using System;
using UnityEngine;

namespace lua
{
	public class LuaTable : ILuaValueConverter
	{
		Lua L;
		int refToTable = Api.LUA_NOREF;

		public LuaTable()
		{
		}

		public LuaTable(Lua L)
		{
			Api.lua_newtable(L);
			Convert(L, -1);
			Api.lua_pop(L, 1);
		}

		public bool valid
		{
			get
			{
				return L != null && refToTable >= 0;
			}
		}

		public void Convert(Lua L, int idx)
		{
			Debug.Assert(Api.lua_istable(L, idx));
			Api.lua_pushvalue(L, idx);
			refToTable = Api.luaL_ref(L, Api.LUA_REGISTRYINDEX);
			this.L = L;
		}

		public bool IsConvertable(Lua L, int idx)
		{
			return Api.lua_type(L, idx) == Api.LUA_TTABLE;
		}

		~LuaTable()
		{
			if (L != null)
			{
				Api.luaL_unref(L, Api.LUA_REGISTRYINDEX, refToTable);
			}
		}

		public int Length
		{
			get
			{
				if (!valid)
					throw new System.InvalidOperationException("Invalid LuaTable");
				var ret = 0;
				Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, refToTable);
				ret = Api.lua_len(L, -1);
				Api.lua_pop(L, 1);
				return ret;
			}
		}

		public object this[int index]
		{
			get
			{
				if (!valid)
					throw new System.InvalidOperationException("Invalid LuaTable");
				Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, refToTable);
				Api.lua_geti(L, -1, index);
				var ret = Lua.CsharpValueFrom(L, -1);
				Api.lua_pop(L, 1);
				return ret;

			}
		}

		public object this[string index]
		{
			get
			{
				if (!valid)
					throw new System.InvalidOperationException("Invalid LuaTable");
				Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, refToTable);
				Api.lua_getfield(L, -1, index);
				var ret = Lua.CsharpValueFrom(L, -1);
				Api.lua_pop(L, 1);
				return ret;
			}
		}


		public lua.LuaTable Invoke(string name, params object[] args)
		{
			if (!valid)
				throw new System.InvalidOperationException("Invalid LuaTable");

			LuaTable ret = null;

			Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, refToTable);
			var top = Api.lua_gettop(L);
			if (Api.lua_getfield(L, -1, name) == Api.LUA_TFUNCTION)
			{
				Api.lua_pushvalue(L, -2);
				for (int i = 0; i < args.Length; ++i)
				{
					Lua.PushCsharpValue(L, args[i]);
				}
				Lua.Call(L, 1 + args.Length, Api.LUA_MULTRET);
				var nrets = Api.lua_gettop(L) - top;
				if (nrets > 0)
				{
					Api.lua_createtable(L, nrets, 0);
					for (int i = nrets; i >= 1; i--)
					{
						Api.lua_pushvalue(L, -i - 1); // value at -i - 1
						Api.lua_seti(L, -2, nrets - i + 1);
					}
					ret = new LuaTable();
					ret.Convert(L, -1);
					Api.lua_pop(L, 1);
				}
			}
			Api.lua_settop(L, top); // left nothing on stack
			return ret;
		}



	}
}