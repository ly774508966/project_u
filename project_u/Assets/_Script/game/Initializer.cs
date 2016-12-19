using UnityEngine;
using System;


public class Initializer : MonoBehaviour
{

	void Awake()
	{
		comext.utils.App.Init();
		var L = lua.Lua.instance.luaState;
		lua.Api.lua_pushcclosure(L, Test, 0);
		lua.Api.lua_setglobal(L, "Test");

		lua.Api.luaL_dostring(L, "return \"my_test_string\"");
		var str = lua.Api.lua_tostring(L, -1);
		Debug.Log(str);

	}

	static int Test(IntPtr L)
	{
		Debug.Log("Test called in Lua");
		return 0;
	}

}
