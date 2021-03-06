﻿/*
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
using System;
using System.Reflection;
using hotpatch;

namespace lua.hotpatch
{
	public class LuaHotPatchLoader
	{
		[HotPatchHub]
		public static bool Hub(
			string signature,
			MethodBase method, 
			object target,
			out	object retval, 
			params object[] args)
		{
			try
			{
				if (find != null)
				{
					using (var patch = (LuaFunction)find.Invoke1(null, signature))
					{
						if (patch == null)
						{
							if (!method.IsConstructor)
								retval = Lua.GetDefaultValue(((MethodInfo)method).ReturnType);
							else
								retval = null;
							return false; // dont break return or change parameter
						}

						var L = patch.CheckValid();
						var top = Api.lua_gettop(L);
						patch.Push();
						var numArgs = 0;
						if (target != null)
						{
							L.PushValue(target);
							++numArgs;
						}
						if (args != null) // args can be null here
						{
							for (int i = 0; i < args.Length; ++i)
							{
								L.PushValue(args[i]);
							}
							numArgs += args.Length;
						}
						L.Call(numArgs, Api.LUA_MULTRET);
						var end = Api.lua_gettop(L);
						var retIdx = top + 1;
						var shouldBreakReturnOrChangeParamOfConstructor = (bool)L.ValueAt(retIdx);
						++retIdx;
						if (!method.IsConstructor)
						{
							var mi = (MethodInfo)method;
							if (mi.ReturnType != typeof(void))
							{
								retval = System.Convert.ChangeType(L.ValueAt(retIdx), mi.ReturnType);
								++retIdx;
							}
							else
							{
								retval = null; // wantever won't be executed
							}
						}
						else
						{
							retval = null; // wantever won't be executed
						}

						// out or ref parameters
						var parameters = method.GetParameters();
						// out/ref object
						for (int i = retIdx; i <= end; ++i)
						{
							int argIdx = i - retIdx;
							var param = parameters[argIdx];
							if (param.IsOut || param.ParameterType.IsByRef || method.IsConstructor)
							{
								if (param.IsOut || param.ParameterType.IsByRef)
									args[argIdx] = System.Convert.ChangeType(L.ValueAt(retIdx), param.ParameterType.GetElementType());
								else
									args[argIdx] = System.Convert.ChangeType(L.ValueAt(retIdx), param.ParameterType);
							}
						}
						Api.lua_settop(L, top);
						return shouldBreakReturnOrChangeParamOfConstructor;
					}
				}
			}
			catch (Exception e)
			{
				Config.LogError(string.Format("patch for {0} was executed failed. {1}", method.ToString(), e.Message));
				// dont let exception escape
			}

			// nothing will	happen
			if (!method.IsConstructor)
			{
				retval = Lua.GetDefaultValue(((MethodInfo)method).ReturnType);
			}
			else
			{
				retval = null; // wantever won't be executed
			}
			return false; // dont do anything
		}


		const string kLuaStub_Patch = 
			"local patches = {}\n" +
			"return\n" +
			"function(sig, func)\n" +  // add a patch
			"  local old = patches[sig]\n" +
			"  patches[sig] = func\n" + 
			"  return old\n" +
			"end,\n" +
			"function(sig)\n" +  // remove patch
			"  local old = patches[sig]\n" +
			"  patches[sig] = nil\n" +
			"  return old\n" +
			"end,\n" + 
			"function(sig)\n" +  // find patch
			"  return patches[sig]\n" +
			"end";


		static LuaFunction find;
		static LuaFunction patch;
		static LuaFunction remove;

		internal static void Open(Lua L)
		{
			L.DoString(kLuaStub_Patch, 3, "hotpatch");
			find = (LuaFunction)L.ValueAt(-1);
			remove = (LuaFunction)L.ValueAt(-2);
			patch = (LuaFunction)L.ValueAt(-3);

			var hotpatch = new LuaTable(L);
			hotpatch["patch"] = patch;
			hotpatch["remove"] = remove;
			hotpatch["find"] = find;

			hotpatch.Push();
			Api.lua_setglobal(L, "hotpatch");
			hotpatch.Dispose();

			Api.lua_pop(L, 3);
		}

		internal static void Close()
		{
			if (find != null)
				find.Dispose();
			if (remove != null)
				remove.Dispose();
			if (patch != null)
				patch.Dispose();

			find = null;
			remove = null;
			patch = null;
		}

		public static void Patch(string signature, string patchScript)
		{
			var L = patch.CheckValid();
			using (var f = LuaFunction.NewFunction(L, patchScript, "patch " + signature))
			{
				patch.Invoke(null, signature, f);
			}
		}

		public static void Remove(string signature)
		{
			remove.Invoke(null, signature);
		}



	}
}
