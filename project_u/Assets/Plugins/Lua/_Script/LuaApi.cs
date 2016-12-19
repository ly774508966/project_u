using System;
using System.Runtime.InteropServices;

namespace lua
{
	public class Api
	{

		public const string LIBNAME = "lua";


		public static bool is64Bit
		{
			get
			{
				return (IntPtr.Size == 8);
			}
		}

		public static int LUAI_MAXSTACK
		{
			get
			{
				if (is64Bit)
					return 1000000;
				return 15000;
			}
		}

		public const int LUA_MULTRET = -1;

		/*
		** Pseudo-indices
		** (-LUAI_MAXSTACK is the minimum valid	index; we keep some	free empty
		** space after that	to help	overflow detection)
		*/
		public static int LUA_REGISTRYINDEX = (-LUAI_MAXSTACK - 1000);
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

		public delegate int lua_CFunction(IntPtr L);
		public delegate int lua_KFunction(IntPtr L, int status, long ctx);

		/*
		** Type	for	functions that read/write blocks when loading/dumping Lua chunks
		*/
		public delegate IntPtr lua_Reader(IntPtr L, IntPtr ud, out long sz);
		public delegate int lua_Writer(IntPtr L, IntPtr p, long sz, IntPtr ud);

		/*
		** state manipulation
		*/
		[DllImport(LIBNAME)]
		public static extern void lua_close(IntPtr L);
		[DllImport(LIBNAME)]
		public static extern IntPtr lua_newthread(IntPtr L);
		[DllImport(LIBNAME)]
		public static extern lua_CFunction lua_atpanic(IntPtr L, lua_CFunction panicf);

		/*
		** basic stack manipulation
		*/
		[DllImport(LIBNAME)]
		public static extern int lua_absindex(IntPtr L, int idx);
		[DllImport(LIBNAME)]
		public static extern int lua_gettop(IntPtr L);
		[DllImport(LIBNAME)]
		public static extern void lua_settop(IntPtr L, int idx);
		[DllImport(LIBNAME)]
		public static extern void lua_pushvalue(IntPtr L, int idx);
		[DllImport(LIBNAME)]
		public static extern void lua_rotate(IntPtr L, int idx, int n);
		[DllImport(LIBNAME)]
		public static extern void lua_copy(IntPtr L, int fromidx, int toidx);
		[DllImport(LIBNAME)]
		public static extern int lua_checkstack(IntPtr L, int n);
		[DllImport(LIBNAME)]
		public static extern void lua_xmove(IntPtr from, IntPtr to, int n);



		/*
		** access functions (stack -> C)
		*/
		[DllImport(LIBNAME, EntryPoint = "lua_isnumber")]
		static extern int lua_isnumber_(IntPtr L, int idx);
		public static bool lua_isnumber(IntPtr L, int idx)
		{
			return lua_isnumber_(L, idx) == 1;
		}


		[DllImport(LIBNAME, EntryPoint = "lua_isstring")]
		static extern int lua_isstring_(IntPtr L, int idx);
		public static bool lua_isstring(IntPtr L, int idx)
		{
			return lua_isstring_(L, idx) == 1;
		}

		[DllImport(LIBNAME, EntryPoint = "lua_iscfunction")]
		static extern int lua_iscfunction_(IntPtr L, int idx);
		public static bool lua_iscfunction(IntPtr L, int idx)
		{
			return lua_iscfunction_(L, idx) == 1;
		}

		[DllImport(LIBNAME, EntryPoint = "lua_isinteger")]
		static extern int lua_isinteger_(IntPtr L, int idx);
		public static bool lua_isinteger(IntPtr L, int idx)
		{
			return lua_isinteger_(L, idx) == 1;
		}


		[DllImport(LIBNAME, EntryPoint = "lua_isuserdata")]
		static extern int lua_isuserdata_(IntPtr L, int idx);
		public static bool lua_isuserdata(IntPtr L, int idx)
		{
			return lua_isuserdata_(L, idx) == 1;
		}

		[DllImport(LIBNAME)]
		public static extern int lua_type(IntPtr L, int idx);
		[DllImport(LIBNAME, EntryPoint = "lua_typename")]
		static extern IntPtr lua_typename_(IntPtr L, int tp);
		public static string lua_typename(IntPtr L, int tp)
		{
			var ptr = lua_typename_(L, tp);
			if (ptr != IntPtr.Zero)
				return Marshal.PtrToStringAnsi(ptr);
			return null;
		}
		[DllImport(LIBNAME)]
		public static extern double lua_tonumberx(IntPtr L, int idx, ref int isnum);
		public static double lua_tonumber(IntPtr L, int idx)
		{
			int isnum = 0;
			return lua_tonumberx(L, idx, ref isnum);
		}
		[DllImport(LIBNAME)]
		public static extern long lua_tointegerx(IntPtr L, int idx, ref int isnum);
		public static long lua_tointeger(IntPtr L, int idx)
		{
			int isnum = 0;
			return lua_tointegerx(L, idx, ref isnum);
		}
		[DllImport(LIBNAME, EntryPoint = "lua_toboolean")]
		static extern int lua_toboolean_(IntPtr L, int idx);
		public static bool lua_toboolean(IntPtr L, int idx)
		{
			return lua_toboolean_(L, idx) != 0;
		}

		[DllImport(LIBNAME, EntryPoint = "lua_tolstring")]
		static extern IntPtr lua_tolstring_(IntPtr L, int idx, out long len);
		public static string lua_tostring(IntPtr L, int idx)
		{
			long len;
			var strPtr = lua_tolstring_(L, idx, out len);
			if (strPtr != IntPtr.Zero)
				return Marshal.PtrToStringAnsi(strPtr, (int)len);
			return null;
		}

		/*
		public static extern size_t          (lua_rawlen) (IntPtr L, int idx);
		public static extern lua_CFunction   (lua_tocfunction) (IntPtr L, int idx);
*/
		[DllImport(LIBNAME)]
		public static extern IntPtr lua_touserdata(IntPtr L, int idx);
		/*
		public static extern lua_State		*(lua_tothread)	(IntPtr	L, int idx);
		public static extern const void		*(lua_topointer) (IntPtr L,	int	idx);
		*/


		public const int LUA_OPEQ = 0;
		public const int LUA_OPLT = 1;
		public const int LUA_OPLE = 2;

		[DllImport(LIBNAME, EntryPoint = "lua_rawequal")]
		static extern int lua_rawequal_(IntPtr L, int idx1, int idx2);
		public static bool lua_rawequal(IntPtr L, int idx1, int idx2)
		{
			return lua_rawequal_(L, idx1, idx2) == 1;
		}
		[DllImport(LIBNAME, EntryPoint = "lua_compare")]
		static extern int lua_compare_(IntPtr L, int idx1, int idx2, int op);
		public static bool lua_compare(IntPtr L, int idx1, int idx2, int op)
		{
			return lua_compare_(L, idx1, idx2, op) == 1;
		}

		/*
		** push	functions (C ->	stack)
		*/

		[DllImport(LIBNAME)]
		public static extern void lua_pushnil(IntPtr L);
		[DllImport(LIBNAME)]
		public static extern void lua_pushnumber(IntPtr L, double n);
		[DllImport(LIBNAME)]
		public static extern void lua_pushinteger(IntPtr L, long n);
		[DllImport(LIBNAME)]
		public static extern IntPtr lua_pushstring(IntPtr L, string str);
		[DllImport(LIBNAME)]
		public static extern void lua_pushcclosure(IntPtr L, lua_CFunction fn, int n);
		[DllImport(LIBNAME, EntryPoint = "lua_pushboolean")]
		static extern void lua_pushboolean_(IntPtr L, int b);
		public static void lua_pushboolean(IntPtr L, bool b)
		{
			lua_pushboolean_(L, b ? 1 : 0);
		}

		[DllImport(LIBNAME)]
		public static extern void lua_pushlightuserdata(IntPtr L, IntPtr p);
		/*
		LUA_API	int	  (lua_pushthread) (lua_State *L);
		*/


		public static void lua_newtable(IntPtr L)
		{
			lua_createtable(L, 0, 0);
		}


		/*
		** get functions (Lua -> stack)
		*/
		[DllImport(LIBNAME)]
		public static extern int lua_getglobal(IntPtr L, string name);
		[DllImport(LIBNAME)]
		public static extern int lua_gettable(IntPtr L, int idx);
		[DllImport(LIBNAME)]
		public static extern int lua_getfield(IntPtr L, int idx, string k);
		[DllImport(LIBNAME)]
		public static extern int lua_geti(IntPtr L, int idx, long n);
		[DllImport(LIBNAME)]
		public static extern int lua_rawget(IntPtr L, int idx);
		[DllImport(LIBNAME)]
		public static extern int lua_rawgeti(IntPtr L, int idx, long n);
		[DllImport(LIBNAME)]
		public static extern int lua_rawgetp(IntPtr L, int idx, IntPtr p);
		[DllImport(LIBNAME)]
		public static extern void lua_createtable(IntPtr L, int narr, int nrec);
		[DllImport(LIBNAME)]
		public static extern IntPtr lua_newuserdata(IntPtr L, long sz);
		[DllImport(LIBNAME)]
		public static extern int lua_getmetatable(IntPtr L, int objindex);
		[DllImport(LIBNAME)]
		public static extern int lua_getuservalue(IntPtr L, int idx);


		/*
		** set functions (stack	-> Lua)
		*/
		[DllImport(LIBNAME)]
		public static extern void lua_setglobal(IntPtr L, string name);
		[DllImport(LIBNAME)]
		public static extern void lua_settable(IntPtr L, int idx);
		[DllImport(LIBNAME)]
		public static extern void lua_setfield(IntPtr L, int idx, string k);
		[DllImport(LIBNAME)]
		public static extern void lua_seti(IntPtr L, int idx, long n);
		[DllImport(LIBNAME)]
		public static extern void lua_rawset(IntPtr L, int idx);
		[DllImport(LIBNAME)]
		public static extern void lua_rawseti(IntPtr L, int idx, long n);
		[DllImport(LIBNAME)]
		public static extern void lua_rawsetp(IntPtr L, int idx, IntPtr p);
		[DllImport(LIBNAME)]
		public static extern int lua_setmetatable(IntPtr L, int objindex);
		[DllImport(LIBNAME)]
		public static extern void lua_setuservalue(IntPtr L, int idx);



		static int HandleError(IntPtr L)
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
		internal static extern void lua_callk(IntPtr L, int nargs, int nresults, long ctx, lua_KFunction k);
		internal static void lua_call(IntPtr L, int nargs, int nresults)
		{
			lua_callk(L, nargs, nresults, 0, null);
		}

		[DllImport(LIBNAME)]
		public static extern int lua_pcallk(IntPtr L, int nargs, int nresults, int errfunc, long ctx, lua_KFunction k);

		public static int lua_pcall(IntPtr L, int nargs, int nresults, int errfunc)
		{
			return lua_pcallk(L, nargs, nresults, errfunc, 0, null);
		}

		[DllImport(LIBNAME)]
		public static extern int lua_len(IntPtr L, int idx);

		[DllImport(LIBNAME)]
		public static extern int lua_load(IntPtr L, lua_Reader reader, IntPtr data, string chunkname, string mode);

		[DllImport(LIBNAME)]
		public static extern int lua_dump(IntPtr L, lua_Writer writer, IntPtr data, int strip);


		/*
		** coroutine functions
		*/
		[DllImport(LIBNAME)]
		public static extern int lua_yieldk(IntPtr L, int nresults, long ctx, lua_KFunction k);
		/*
				LUA_API int  (lua_resume)     (lua_State *L, lua_State *from, int narg);
		LUA_API int  (lua_status)     (lua_State *L);
		LUA_API int (lua_isyieldable) (lua_State *L);
		*/

		public static int lua_yield(IntPtr L, int n)
		{
			return lua_yieldk(L, (n), 0, null);
		}



		/*
		** miscellaneous functions
		*/
		[DllImport(LIBNAME)]
		public static extern int lua_error(IntPtr L);

		[DllImport(LIBNAME)]
		public static extern int lua_next(IntPtr L, int idx);

		/*
		** {==============================================================
		** some useful macros
		** ===============================================================
		*/
		public static void lua_pop(IntPtr L, int n) { lua_settop(L, -(n) - 1); }

		public static bool lua_isfunction(IntPtr L, int n) { return (lua_type(L, (n)) == LUA_TFUNCTION); }
		public static bool lua_istable(IntPtr L, int n) { return (lua_type(L, (n)) == LUA_TTABLE); }
		public static bool lua_islightuserdata(IntPtr L, int n) { return (lua_type(L, (n)) == LUA_TLIGHTUSERDATA); }
		public static bool lua_isnil(IntPtr L, int n) { return (lua_type(L, (n)) == LUA_TNIL); }
		public static bool lua_isboolean(IntPtr L, int n) { return (lua_type(L, (n)) == LUA_TBOOLEAN); }
		public static bool lua_isthread(IntPtr L, int n) { return (lua_type(L, (n)) == LUA_TTHREAD); }
		public static bool lua_isnone(IntPtr L, int n) { return (lua_type(L, (n)) == LUA_TNONE); }
		public static bool lua_isnoneornil(IntPtr L, int n) { return (lua_type(L, (n)) <= 0); }


		public static void lua_insert(IntPtr L, int idx) { lua_rotate(L, (idx), 1); }
		public static void lua_remove(IntPtr L, int idx) { lua_rotate(L, (idx), -1); lua_pop(L, 1); }
		public static void lua_replace(IntPtr L, int idx) { lua_copy(L, -1, (idx)); lua_pop(L, 1); }



		// helpers

		[DllImport(LIBNAME, EntryPoint = "luaL_checklstring")]
		static extern IntPtr luaL_checklstring_(IntPtr L, int arg, out long length);
		public static string luaL_checkstring(IntPtr L, int arg)
		{
			long len;
			var strPtr = luaL_checklstring_(L, arg, out len);
			if (strPtr != IntPtr.Zero)
				return Marshal.PtrToStringAnsi(strPtr, (int)len);
			return null;
		}

		[DllImport(LIBNAME)]
		public static extern long luaL_checkinteger(IntPtr L, int arg);
		[DllImport(LIBNAME)]
		public static extern long luaL_optinteger(IntPtr L, int arg, long def);


		[DllImport(LIBNAME)]
		public static extern void luaL_checkstack(IntPtr L, int sz, string msg);
		[DllImport(LIBNAME)]
		public static extern void luaL_checktype(IntPtr L, int arg, int t);
		[DllImport(LIBNAME)]
		public static extern void luaL_checkany(IntPtr L, int arg);

		[DllImport(LIBNAME)]
		public static extern int luaL_loadstring(IntPtr state, string s);

		[DllImport(LIBNAME)]
		public static extern IntPtr luaL_newstate();

		public static bool luaL_dostring(IntPtr L, string s)
		{
			int r = luaL_loadstring(L, s);
			if (r == LUA_OK)
			{
				r = lua_pcall(L, 0, LUA_MULTRET, 0);
				return r != LUA_OK;
			}
			return true;
		}

		public static int luaL_getmetatable(IntPtr L, string k)
		{
			return lua_getfield(L, LUA_REGISTRYINDEX, k);
		}


		/* predefined references */
		public const int LUA_NOREF = -2;
		public const int LUA_REFNIL = -1;

		[DllImport(LIBNAME)]
		public static extern int luaL_ref(IntPtr L, int t);
		[DllImport(LIBNAME)]
		public static extern void luaL_unref(IntPtr L, int t, int r);


		[DllImport(LIBNAME)]
		public static extern int luaL_loadfilex(IntPtr L, string filename, string mode);

		public static int luaL_loadfile(IntPtr L, string filename)
		{
			return luaL_loadfilex(L, filename, "bt");
		}

		public static bool luaL_dofile(IntPtr L, string filename)
		{
			var r = luaL_loadfile(L, filename);
			if (r == LUA_OK)
			{
				r = lua_pcall(L, 0, LUA_MULTRET, 0);
				return r != LUA_OK;
			}
			return true;
		}

		public static void luaL_setfuncs(IntPtr L, luaL_Reg[] l, int nup)
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
		public static extern void luaL_traceback(IntPtr L, IntPtr L1, string msg, int level);


		[DllImport(LIBNAME)]
		public static extern void luaL_requiref(IntPtr L, string modname, lua_CFunction openf, int glb);





		// lualib
		[DllImport(LIBNAME)]
		public static extern void luaL_openlibs(IntPtr L);




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
		public static extern int luaL_newmetatable(IntPtr L, string tname);
		[DllImport(LIBNAME)]
		public static extern int luaL_setmetatable(IntPtr L, string tname);


		public static void luaL_newlibtable(IntPtr L, luaL_Reg[] l)
		{
			lua_createtable(L, 0, l.Length);
		}

		public static void luaL_newlib(IntPtr L, luaL_Reg[] l)
		{
			luaL_newlibtable(L, l);
			luaL_setfuncs(L, l, 0);
		}

		public static void Assert(IntPtr L, bool condition, string message = "Assertion false!")
		{
			if (!condition)
			{
				lua_pushstring(L, message);
				lua_error(L);
			}
		}

		const string udatatypename = "userdata";
		static readonly string[] luaT_typenames_ = new string [] {
			"no_value",
			"nil", "boolean", udatatypename, "number",
			"string", "table", "function", udatatypename, "thread",
			"proto" /* this last case is used for tests only */
		};
		public static string Typename(int type)
		{
			if (type < luaT_typenames_.Length)
			{
				return luaT_typenames_[type + 1];
			}
			return "invalid_type_" + type;
		}


	}
}
