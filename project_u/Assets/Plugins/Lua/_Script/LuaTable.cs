using System.Collections;
using System;

namespace lua
{
	class LuaTable
	{
		IntPtr L;
		int refToTable;

		LuaTable(IntPtr L, int refToTable)
		{
			this.L = L;
			this.refToTable = refToTable;
		}

		public static LuaTable Attach(IntPtr L, int idx)
		{
			Api.lua_pushvalue(L, idx);
			var refToTable = Api.luaL_ref(L, -1);
			return new LuaTable(L, refToTable);
		}


	}
}