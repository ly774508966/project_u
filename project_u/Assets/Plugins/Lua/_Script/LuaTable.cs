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
	public class LuaTable
	{
		Lua L_;
		int tableRef = Api.LUA_NOREF;

		LuaTable(Lua L, int idx)
		{
			L_ = L;
			tableRef = L.MakeRefAt(idx);
		}

		public bool valid
		{
			get
			{
				return L_.valid;
			}
		}

		internal static LuaTable ReferenceTo(Lua L, int idx)
		{
			Debug.Assert(Api.lua_istable(L, idx));
			return new LuaTable(L, idx);
		}

		~LuaTable()
		{
			if (L_.valid && tableRef != Api.LUA_NOREF)
			{
				L_.Unref(tableRef);
			}
		}

		Lua CheckValid()
		{
			if (!L_.valid)
				throw new System.InvalidOperationException("Lua vm already destroyed.");
			return L_;
		}

		public int Length
		{
			get
			{
				var L = CheckValid();
				var ret = 0;
				L.PushRef(tableRef);
				ret = Api.lua_len(L, -1);
				Api.lua_pop(L, 2);
				return ret;
			}
		}

		public object this[int index]
		{
			get
			{
				var L = CheckValid();
				L.PushRef(tableRef);
				Api.lua_geti(L, -1, index);
				var ret = L.ValueAt(-1);
				Api.lua_pop(L, 2);
				return ret;
			}
			set
			{
				var L = CheckValid();
				L.PushRef(tableRef);
				L.PushValue(value);
				Api.lua_seti(L, -2, index);
				Api.lua_pop(L, 1);
			}
		}

		public object this[string index]
		{
			get
			{
				var L = CheckValid();
				L.PushRef(tableRef);
				Api.lua_getfield(L, -1, index);
				var ret = L.ValueAt(-1);
				Api.lua_pop(L, 2);
				return ret;
			}
			set
			{
				var L = CheckValid();
				L.PushRef(tableRef);
				L.PushValue(value);
				Api.lua_setfield(L, -2, index);
				Api.lua_pop(L, 1);
			}
		}


		public lua.LuaTable Invoke(string name, params object[] args)
		{
			var L = CheckValid();

			LuaTable ret = null;

			L.PushRef(tableRef);
			var top = Api.lua_gettop(L);
			if (Api.lua_getfield(L, -1, name) == Api.LUA_TFUNCTION)
			{
				Api.lua_pushvalue(L, -2);
				for (int i = 0; i < args.Length; ++i)
				{
					L.PushValue(args[i]);
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
					ret = ReferenceTo(L, -1);
					Api.lua_pop(L, 1);
				}
			}
			Api.lua_settop(L, top - 1); // left nothing on stack
			return ret;
		}



	}
}