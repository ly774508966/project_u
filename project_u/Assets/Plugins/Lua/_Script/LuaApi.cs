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
﻿using System;
using System.Runtime.InteropServices;

using lua_KContext = System.IntPtr;
using lua_Integer = System.Int64;
using lua_Number = System.Double;
using size_t = System.UIntPtr;
using lua_State = System.IntPtr;

namespace lua
{
	public class Api
	{
#if UNITY_EDITOR || UNITY_ANDROID
		public const string LIBNAME = "lua";
#elif UNITY_IPHONE
		public const string LIBNAME = "__Internal";
#endif

		public const int LUA_MULTRET = -1;

		/*
		** Pseudo-indices
		*/
		[DllImport(LIBNAME)]
		static extern int lua_const_LUA_REGISTRYINDEX();

		public static int LUA_REGISTRYINDEX
		{
			get
			{
				return lua_const_LUA_REGISTRYINDEX();
			}
		}
		public static int lua_upvalueindex(int i)
		{
			return LUA_REGISTRYINDEX - i;
		}


		/* thread status */
		public const int LUA_OK = 0;
		public const int LUA_YIELD = 1;
		public const int LUA_ERRRUN	= 2;
		public const int LUA_ERRSYNTAX = 3;
		public const int LUA_ERRMEM = 4;
		public const int LUA_ERRGCMM = 5;
		public const int LUA_ERRERR	= 6;

		/*
		** basic types
		*/
		public const int LUA_TNONE = -1;
		public const int LUA_TNIL = 0;
		public const int LUA_TBOOLEAN = 1;
		public const int LUA_TLIGHTUSERDATA = 2;
		public const int LUA_TNUMBER = 3;
		public const int LUA_TSTRING = 4;
		public const int LUA_TTABLE = 5;
		public const int LUA_TFUNCTION = 6;
		public const int LUA_TUSERDATA = 7;
		public const int LUA_TTHREAD = 8;
		public const int LUA_NUMTAGS = 9;

		public delegate int lua_CFunction(lua_State L);
		public delegate int lua_KFunction(lua_State L, int status, lua_KContext ctx);

		/*
		** Type	for	functions that read/write blocks when loading/dumping Lua chunks
		*/
		public delegate IntPtr lua_Reader(lua_State L, IntPtr ud, out size_t sz);
		public delegate int lua_Writer(lua_State L, IntPtr p, size_t sz, IntPtr ud);
		public delegate IntPtr lua_Alloc(IntPtr ud, IntPtr ptr, size_t osize, size_t nsize);

		/*
		** state manipulation
		*/
		[DllImport(LIBNAME)]
		public static extern IntPtr lua_newstate(lua_Alloc f, IntPtr ud);
		[DllImport(LIBNAME)]
		public static extern void lua_close(lua_State L);
		[DllImport(LIBNAME)]
		public static extern IntPtr lua_newthread(lua_State L);
		[DllImport(LIBNAME)]
		public static extern lua_CFunction lua_atpanic(lua_State L, lua_CFunction panicf);

		/*
		** basic stack manipulation
		*/
		[DllImport(LIBNAME)]
		public static extern int lua_absindex(lua_State L, int idx);
		[DllImport(LIBNAME)]
		public static extern int lua_gettop(lua_State L);
		[DllImport(LIBNAME)]
		public static extern void lua_settop(lua_State L, int idx);
		[DllImport(LIBNAME)]
		public static extern void lua_pushvalue(lua_State L, int idx);
		[DllImport(LIBNAME)]
		public static extern void lua_rotate(lua_State L, int idx, int n);
		[DllImport(LIBNAME)]
		public static extern void lua_copy(lua_State L, int fromidx, int toidx);
		[DllImport(LIBNAME)]
		public static extern int lua_checkstack(lua_State L, int n);
		[DllImport(LIBNAME)]
		public static extern void lua_xmove(IntPtr from, IntPtr to, int n);



		/*
		** access functions (stack -> C)
		*/
		[DllImport(LIBNAME, EntryPoint = "lua_isnumber")]
		static extern int lua_isnumber_(lua_State L, int idx);
		public static bool lua_isnumber(lua_State L, int idx)
		{
			return lua_isnumber_(L, idx) == 1;
		}


		[DllImport(LIBNAME, EntryPoint = "lua_isstring")]
		static extern int lua_isstring_(lua_State L, int idx);
		public static bool lua_isstring(lua_State L, int idx)
		{
			return lua_isstring_(L, idx) == 1;
		}

		[DllImport(LIBNAME, EntryPoint = "lua_iscfunction")]
		static extern int lua_iscfunction_(lua_State L, int idx);
		public static bool lua_iscfunction(lua_State L, int idx)
		{
			return lua_iscfunction_(L, idx) == 1;
		}

		[DllImport(LIBNAME, EntryPoint = "lua_isinteger")]
		static extern int lua_isinteger_(lua_State L, int idx);
		public static bool lua_isinteger(lua_State L, int idx)
		{
			return lua_isinteger_(L, idx) == 1;
		}


		[DllImport(LIBNAME, EntryPoint = "lua_isuserdata")]
		static extern int lua_isuserdata_(lua_State L, int idx);
		public static bool lua_isuserdata(lua_State L, int idx)
		{
			return lua_isuserdata_(L, idx) == 1;
		}
		
		[DllImport(LIBNAME)]
		public static extern int lua_type(lua_State L, int idx);
		[DllImport(LIBNAME, EntryPoint = "lua_typename")]
		static extern IntPtr lua_typename_(lua_State L, int tp);
		public static string lua_typename(lua_State L, int tp)
		{
			var ptr = lua_typename_(L, tp);
			if (ptr != IntPtr.Zero)
				return Marshal.PtrToStringAnsi(ptr);
			return null;
		}
		[DllImport(LIBNAME)]
		public static extern lua_Number lua_tonumberx(lua_State L, int idx, ref int isnum);
		public static lua_Number lua_tonumber(lua_State L, int idx)
		{
			int isnum = 0;
			return lua_tonumberx(L, idx, ref isnum);
		}
		[DllImport(LIBNAME)]
		public static extern lua_Integer lua_tointegerx(lua_State L, int idx, ref int isnum);
		public static lua_Integer lua_tointeger(lua_State L, int idx)
		{
			int isnum = 0;
			return lua_tointegerx(L, idx, ref isnum);
		}
		[DllImport(LIBNAME, EntryPoint = "lua_toboolean")]
		static extern int lua_toboolean_(lua_State L, int idx);
		public static bool lua_toboolean(lua_State L, int idx)
		{
			return lua_toboolean_(L, idx) != 0;
		}

		[DllImport(LIBNAME, EntryPoint = "lua_tolstring")]
		public static extern IntPtr lua_tolstring(lua_State L, int idx, out IntPtr len);

		public static void lua_pushbytes(lua_State L, byte[] bytes)
		{
			var h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			var ptr = h.AddrOfPinnedObject();
			lua_pushlstring(L,	ptr, new size_t((uint)bytes.Length));
			h.Free();
		}

		public static byte[] lua_tobytes(lua_State L, int idx)
		{
			if (lua_isstring(L, idx))
			{
				IntPtr len;
				var ptr = lua_tolstring(L, idx, out len);
				if ((int)len > 0 && IntPtr.Zero != ptr)
				{
					var bytes = new byte[(int)len];
					Marshal.Copy(ptr, bytes, 0, (int)len);
					return bytes;
				}
			}
			return null;
		}

		public static string lua_tostring(lua_State L, int idx)
		{
			IntPtr len;
			var strPtr = lua_tolstring(L, idx, out len);
			if (strPtr != IntPtr.Zero)
				return Marshal.PtrToStringAnsi(strPtr, (int)len);
			return null;
		}

		[DllImport(LIBNAME)]
		public static extern IntPtr lua_touserdata(lua_State L, int idx);


		public const int LUA_OPEQ = 0;
		public const int LUA_OPLT = 1;
		public const int LUA_OPLE = 2;

		[DllImport(LIBNAME, EntryPoint = "lua_rawequal")]
		static extern int lua_rawequal_(lua_State L, int idx1, int idx2);
		public static bool lua_rawequal(lua_State L, int idx1, int idx2)
		{
			return lua_rawequal_(L, idx1, idx2) == 1;
		}
		[DllImport(LIBNAME, EntryPoint = "lua_compare")]
		static extern int lua_compare_(lua_State L, int idx1, int idx2, int op);
		public static bool lua_compare(lua_State L, int idx1, int idx2, int op)
		{
			return lua_compare_(L, idx1, idx2, op) == 1;
		}

		/*
		** push	functions (C ->	stack)
		*/

		[DllImport(LIBNAME)]
		public static extern void lua_pushnil(lua_State L);
		[DllImport(LIBNAME)]
		public static extern void lua_pushnumber(lua_State L, lua_Number n);
		[DllImport(LIBNAME)]
		public static extern void lua_pushinteger(lua_State L, lua_Integer n);
		[DllImport(LIBNAME)]
		public static extern IntPtr lua_pushlstring(lua_State L, IntPtr s, size_t len);
		[DllImport(LIBNAME)]
		public static extern IntPtr lua_pushstring(lua_State L, string str);
		[DllImport(LIBNAME)]
		public static extern void lua_pushcclosure(lua_State L, lua_CFunction fn, int n);
		[DllImport(LIBNAME, EntryPoint = "lua_pushboolean")]
		static extern void lua_pushboolean_(lua_State L, int b);
		public static void lua_pushboolean(lua_State L, bool b)
		{
			lua_pushboolean_(L, b ? 1 : 0);
		}

		[DllImport(LIBNAME)]
		public static extern void lua_pushlightuserdata(lua_State L, IntPtr p);
		/*
		LUA_API	int	  (lua_pushthread) (lua_State *L);
		*/


		public static void lua_newtable(lua_State L)
		{
			lua_createtable(L, 0, 0);
		}


		/*
		** get functions (Lua -> stack)
		*/
		[DllImport(LIBNAME)]
		public static extern int lua_getglobal(lua_State L, string name);
		[DllImport(LIBNAME)]
		public static extern int lua_gettable(lua_State L, int idx);
		[DllImport(LIBNAME)]
		public static extern int lua_getfield(lua_State L, int idx, string k);
		[DllImport(LIBNAME)]
		public static extern int lua_geti(lua_State L, int idx, lua_Integer n);
		[DllImport(LIBNAME)]
		public static extern int lua_rawget(lua_State L, int idx);
		[DllImport(LIBNAME)]
		public static extern int lua_rawgeti(lua_State L, int idx, lua_Integer n);
		[DllImport(LIBNAME)]
		public static extern int lua_rawgetp(lua_State L, int idx, IntPtr p);
		[DllImport(LIBNAME)]
		public static extern void lua_createtable(lua_State L, int narr, int nrec);
		[DllImport(LIBNAME)]
		public static extern IntPtr lua_newuserdata(lua_State L, size_t sz);
		[DllImport(LIBNAME)]
		public static extern int lua_getmetatable(lua_State L, int objindex);
		[DllImport(LIBNAME)]
		public static extern int lua_getuservalue(lua_State L, int idx);


		/*
		** set functions (stack	-> Lua)
		*/
		[DllImport(LIBNAME)]
		public static extern void lua_setglobal(lua_State L, string name);
		[DllImport(LIBNAME)]
		public static extern void lua_settable(lua_State L, int idx);
		[DllImport(LIBNAME)]
		public static extern void lua_setfield(lua_State L, int idx, string k);
		[DllImport(LIBNAME)]
		public static extern void lua_seti(lua_State L, int idx, int n);
		[DllImport(LIBNAME)]
		public static extern void lua_rawset(lua_State L, int idx);
		[DllImport(LIBNAME)]
		public static extern void lua_rawseti(lua_State L, int idx, int n);
		[DllImport(LIBNAME)]
		public static extern void lua_rawsetp(lua_State L, int idx, IntPtr p);
		[DllImport(LIBNAME)]
		public static extern int lua_setmetatable(lua_State L, int objindex);
		[DllImport(LIBNAME)]
		public static extern void lua_setuservalue(lua_State L, int idx);



		static int HandleError(lua_State L)
		{
			var errMessage = lua_tostring(L, -1);
			lua_pop(L, 1); // pop error object
			luaL_traceback(L, L, errMessage, 1); // push stack trace
			return 1;
		}

		/*
		** 'load' and 'call' functions (load and run Lua code)
		*/
		[DllImport(LIBNAME)]
		internal static extern void lua_callk(lua_State L, int nargs, int nresults, lua_KContext ctx, lua_KFunction k);
		internal static void lua_call(lua_State L, int nargs, int nresults)
		{
			lua_callk(L, nargs, nresults, lua_KContext.Zero, null);
		}

		[DllImport(LIBNAME)]
		public static extern int lua_pcallk(lua_State L, int nargs, int nresults, int errfunc, lua_KContext ctx, lua_KFunction k);

		public static int lua_pcall(lua_State L, int nargs, int nresults, int errfunc)
		{
			return lua_pcallk(L, nargs, nresults, errfunc, IntPtr.Zero, null);
		}

		[DllImport(LIBNAME)]
		public static extern int lua_len(lua_State L, int idx);

		[DllImport(LIBNAME)]
		public static extern int lua_load(lua_State L, lua_Reader reader, IntPtr data, string chunkname, string mode);

		[DllImport(LIBNAME)]
		public static extern int lua_dump(lua_State L, lua_Writer writer, IntPtr data, int strip);


		/*
		** coroutine functions
		*/
		[DllImport(LIBNAME)]
		public static extern int lua_yieldk(lua_State L, int nresults, lua_KContext ctx, lua_KFunction k);

		public static int lua_yield(lua_State L, int n)
		{
			return lua_yieldk(L, (n), lua_KContext.Zero, null);
		}



		/*
		** miscellaneous functions
		*/
		[DllImport(LIBNAME)]
		public static extern int lua_error(lua_State L);

		[DllImport(LIBNAME)]
		public static extern int lua_next(lua_State L, int idx);

		/*
		** {==============================================================
		** some useful macros
		** ===============================================================
		*/
		public static void lua_pop(lua_State L, int n) { lua_settop(L, -(n) - 1); }

		public static bool lua_isfunction(lua_State L, int n) { return (lua_type(L, (n)) == LUA_TFUNCTION); }
		public static bool lua_istable(lua_State L, int n) { return (lua_type(L, (n)) == LUA_TTABLE); }
		public static bool lua_islightuserdata(lua_State L, int n) { return (lua_type(L, (n)) == LUA_TLIGHTUSERDATA); }
		public static bool lua_isnil(lua_State L, int n) { return (lua_type(L, (n)) == LUA_TNIL); }
		public static bool lua_isboolean(lua_State L, int n) { return (lua_type(L, (n)) == LUA_TBOOLEAN); }
		public static bool lua_isthread(lua_State L, int n) { return (lua_type(L, (n)) == LUA_TTHREAD); }
		public static bool lua_isnone(lua_State L, int n) { return (lua_type(L, (n)) == LUA_TNONE); }
		public static bool lua_isnoneornil(lua_State L, int n) { return (lua_type(L, (n)) <= 0); }


		public static void lua_insert(lua_State L, int idx) { lua_rotate(L, (idx), 1); }
		public static void lua_remove(lua_State L, int idx) { lua_rotate(L, (idx), -1); lua_pop(L, 1); }
		public static void lua_replace(lua_State L, int idx) { lua_copy(L, -1, (idx)); lua_pop(L, 1); }



		// helpers

		[DllImport(LIBNAME, EntryPoint = "luaL_checklstring")]
		public static extern IntPtr luaL_checklstring(lua_State L, int arg, out size_t length);
		public static string luaL_checkstring(lua_State L, int arg)
		{
			size_t len;
			var strPtr = luaL_checklstring(L, arg, out len);
			if (strPtr != IntPtr.Zero)
				return Marshal.PtrToStringAnsi(strPtr, (int)len);
			return null;
		}

		[DllImport(LIBNAME)]
		public static extern long luaL_checkinteger(lua_State L, int arg);
		[DllImport(LIBNAME)]
		public static extern long luaL_optinteger(lua_State L, int arg, lua_Integer def);


		[DllImport(LIBNAME)]
		public static extern void luaL_checkstack(lua_State L, int sz, string msg);
		[DllImport(LIBNAME)]
		public static extern void luaL_checktype(lua_State L, int arg, int t);
		[DllImport(LIBNAME)]
		public static extern void luaL_checkany(lua_State L, int arg);

		[DllImport(LIBNAME)]
		public static extern int luaL_loadstring(IntPtr state, string s);

		[DllImport(LIBNAME)]
		public static extern lua_State luaL_newstate();

		public static bool luaL_dostring(lua_State L, string s)
		{
			int r = luaL_loadstring(L, s);
			if (r == LUA_OK)
			{
				r = lua_pcall(L, 0, LUA_MULTRET, 0);
				return r != LUA_OK;
			}
			return true;
		}

		public static int luaL_getmetatable(lua_State L, string k)
		{
			return lua_getfield(L, LUA_REGISTRYINDEX, k);
		}


		/* predefined references */
		public const int LUA_NOREF = -2;
		public const int LUA_REFNIL = -1;

		[DllImport(LIBNAME)]
		public static extern int luaL_ref(lua_State L, int t);
		[DllImport(LIBNAME)]
		public static extern void luaL_unref(lua_State L, int t, int r);


		[DllImport(LIBNAME)]
		public static extern int luaL_loadfilex(lua_State L, string filename, string mode);

		public static int luaL_loadfile(lua_State L, string filename)
		{
			return luaL_loadfilex(L, filename, "bt");
		}

		public static bool luaL_dofile(lua_State L, string filename)
		{
			var r = luaL_loadfile(L, filename);
			if (r == LUA_OK)
			{
				r = lua_pcall(L, 0, LUA_MULTRET, 0);
				return r != LUA_OK;
			}
			return true;
		}

		public static void luaL_setfuncs(lua_State L, luaL_Reg[] l, int nup)
		{
			luaL_checkstack(L, nup, "too many upvalues");
			for (int i = 0; i < l.Length; ++i)
			{
				for (int u = 0; u < nup; ++u)  /* copy upvalues to the top */
					lua_pushvalue(L, -nup);
				lua_pushcclosure(L, l[i].func, nup);  /* closure with those upvalues */
				lua_setfield(L, -(nup + 2), l[i].name);
			}
			lua_pop(L, nup);  /* remove upvalues */
		}

		[DllImport(LIBNAME)]
		public static extern void luaL_traceback(lua_State L, lua_State L1, string msg, int level);


		[DllImport(LIBNAME)]
		public static extern void luaL_requiref(lua_State L, string modname, lua_CFunction openf, int glb);





		// lualib
		[DllImport(LIBNAME)]
		public static extern void luaL_openlibs(lua_State L);




		public struct luaL_Reg
		{
			public string name;
			public lua_CFunction func;

			public luaL_Reg(string n, lua_CFunction f)
			{
				name = n;
				func = f;
			}
		}

		[DllImport(LIBNAME)]
		public static extern int luaL_newmetatable(lua_State L, string tname);
		[DllImport(LIBNAME)]
		public static extern int luaL_setmetatable(lua_State L, string tname);
		[DllImport(LIBNAME)]
		public static extern IntPtr luaL_testudata(lua_State L, int ud, string tname);
		[DllImport(LIBNAME)]
		public static extern IntPtr luaL_checkudata(lua_State L, int ud, string tname);

		public static void luaL_newlibtable(lua_State L, luaL_Reg[] l)
		{
			lua_createtable(L, 0, l.Length);
		}

		public static void luaL_newlib(lua_State L, luaL_Reg[] l)
		{
			luaL_newlibtable(L, l);
			luaL_setfuncs(L, l, 0);
		}

		public static void Assert(lua_State L, bool condition, string message = "Assertion failed!")
		{
			if (!condition)
			{
				lua_pushstring(L, message);
				lua_error(L);
			}
		}

		[DllImport(LIBNAME)]
		static extern IntPtr lua_const_ttypename(int type);
		public static string ttypename(int type)
		{
			var strPtr = lua_const_ttypename(type);
			if (strPtr != IntPtr.Zero)
				return Marshal.PtrToStringAnsi(strPtr);
			return "invalid_type_bad_ttypename";
		}


	}
}
