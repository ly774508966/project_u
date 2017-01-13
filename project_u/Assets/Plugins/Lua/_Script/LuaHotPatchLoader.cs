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
using System;
using System.Reflection;

namespace lua.hotpatch
{
	public class LuaHotPatchLoader
	{
		// https://www.codeproject.com/articles/438868/inline-msil-in-csharp-vb-net-and-generic-pointers

		[LuaHotPatchHub]
		public static bool Hub(
			MethodInfo method, 
			object target,
			out	object retval, 
			params object[] args)
		{
#if UNITY_EDITOR
			if (method == null)
			{
				retval = null;
				return false;
			}
			Config.Log("Invoking " + method.ToString());
#endif
			try
			{
				if (findPatch != null)
				{
					using (var patch = (LuaFunction)findPatch.Invoke1(null, method.ToString()))
					{
						if (patch == null)
						{
							retval = Lua.GetDefaultValue(method.ReturnType);
							return false; // dont break return
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
						var nrets = Api.lua_gettop(L) - top;
						var shouldBreakReturn = (bool)L.ValueAt(-1);
						Api.lua_pop(L, 1);
						--nrets;
						if (method.ReturnType != typeof(void))
						{
							retval = System.Convert.ChangeType(L.ValueAt(-1), method.ReturnType);
							Api.lua_pop(L, 1);
							--nrets;
						}
						else
						{
							retval = null; // whatever will not be executed
						}
						// out or ref parameters
						var parameters = method.GetParameters();
						// out/ref object
						for (int i = parameters.Length - 1; i >= 0; --i)
						{
							var param = parameters[i];
							if (param.IsOut || param.ParameterType.IsByRef)
							{
								if (nrets <= 0) throw new Exception("number of return value dosen't match count of out/ref parameter.");
								args[i] = L.ValueAt(-1);
								Api.lua_pop(L, 1);
								--nrets;
							}
						}
						return shouldBreakReturn;
					}
				}
			}
			catch (Exception e)
			{
				Config.LogError(string.Format("patch for {0} was executed failed. {1}", method.ToString(), e.Message));
				// dont let exception escape
			}
			retval = Lua.GetDefaultValue(method.ReturnType);
			return true; // dont break return
		}


		const string kLuaStub_Patch = 
			"local patches = {}\n" +
			"return\n" +
			"function(sig, func)\n" +  // add a patch
			"  local old = patches[sig]\n" +
			"  patches[sig] = func\n" + 
			"  return old\n" +
			"end,\n" +
			"function(sig)\n" +  // remove path
			"  local old = patches[sig]\n" +
			"  patches[sig] = nil\n" +
			"  return old\n" +
			"end,\n" + 
			"function(sig)\n" +  // find patch
			"  return patches[sig]\n" +
			"end";


		static LuaFunction findPatch;

		internal static void Open(Lua L)
		{
			L.DoString(kLuaStub_Patch, 3, "hotpatch");
			findPatch = (LuaFunction)L.ValueAt(-1);

			var removePatch = (LuaFunction)L.ValueAt(-2);
			var addPatch = (LuaFunction)L.ValueAt(-3);

			var hotpatch = new LuaTable(L);
			hotpatch["patch"] = addPatch;
			addPatch.Dispose();
			hotpatch["remove"] = removePatch;
			removePatch.Dispose();
			hotpatch["find"] = findPatch; // still need it

			hotpatch.Push();
			Api.lua_setglobal(L, "hotpatch");
			hotpatch.Dispose();

			Api.lua_pop(L, 3);
		}

		internal static void Close()
		{
			findPatch.Dispose();
		}





	}
}
