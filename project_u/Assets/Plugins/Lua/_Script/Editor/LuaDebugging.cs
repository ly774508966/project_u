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
					if (!Api.luaL_dostring(L,
						"  local json =	require	'json'\n" +
						"  local debuggee =	require	'vscode-debuggee'\n" +
						"  local startType,	startResult	= debuggee.start(json)\n" +
						"  local Debug = csharp.import('UnityEngine.Debug, UnityEngine')\n"	+
						"  Debug.LogWarning('start lua debugging: '.. tostring(startType) .. ',	' .. tostring(startResult))\n" +
						"  return debuggee.poll"))
					{
						debuggeePoll = LuaFunction.MakeRefTo(L, -1);
						LuaBehaviour.debuggeePoll = delegate() {
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
							}
						};
					}
					else
					{
						Debug.LogError("Cannot start debuggee");
					}
				}
				catch (Exception e)
				{
					Debug.LogError(e.Message);
				}

				Api.lua_settop(L, top);
			}
		}
	}
}
