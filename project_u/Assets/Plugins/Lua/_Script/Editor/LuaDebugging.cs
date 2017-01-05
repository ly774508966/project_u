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
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System;

namespace lua
{
	public class LuaDebugging
	{
		static LuaFunction debuggeePoll;
		static LuaFunction debuggee_Debug;

		[MenuItem("Lua/Start Debugging ...")]
		static void StartDebugging()
		{
			if (Application.isPlaying)
			{
				var type = Type.GetType("lua.LuaBehaviour, Assembly-CSharp-firstpass");
				Debug.Assert(type != null);
				var field = type.GetField("L", BindingFlags.Static | BindingFlags.NonPublic);
				var L = (Lua)field.GetValue(null);
				Debug.Assert(L != null);
				var top = Api.lua_gettop(L);

				try
				{
					Api.luaL_dostring(L,
						"return function()" +
						"  local Debug = csharp.import('UnityEngine.Debug, UnityEngine')\n"	+
						"  local json =	require 'json'\n" +
						"  local debuggee =	require	'vscode-debuggee'\n" +
						"  local startType,	startResult	= debuggee.start(json)\n" +
						"  Debug.LogWarning('start lua debugging: '.. tostring(startType) .. ',	' .. tostring(startResult))\n" +
						"  return debuggee.poll, debuggee._debug\n" + 
						"end");
					L.Call(0, 2);
					debuggeePoll = LuaFunction.MakeRefTo(L, -2);
					debuggee_Debug = LuaFunction.MakeRefTo(L, -1);
					LuaBehaviour.debuggeePoll = delegate ()
					{
						try
						{
							debuggeePoll.Invoke();
						}
						catch (Exception e)
						{
							Debug.LogError(e.Message);
							LuaBehaviour.debuggeePoll = null;
							debuggeePoll.Dispose();
							debuggeePoll = null;
							debuggee_Debug.Dispose();
							debuggee_Debug = null;
						}
					};
				}
				catch (Exception e)
				{
					Debug.LogErrorFormat("Cannot start debuggee {0}", e.Message);
				}
				Api.lua_settop(L, top);
			}
		}

		[MenuItem("Lua/Which Line")]
		static void WhichLine()
		{
			if (debuggee_Debug != null)
			{
				debuggee_Debug.Invoke(null, "which_line");
			}
		}
	}
}
