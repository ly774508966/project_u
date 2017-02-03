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
using System;
using System.Reflection;
using System.IO;

namespace lua.hotpatch
{
	// hotpatch, https://github.com/xiaobin83/Unity3D.HotPatchEnabler

	public class LuaHotPatchEditor
	{
		[MenuItem("Lua/Active Hot Patch (Mod Test)", priority = 100)]
		static void ActiveHotPatchModeTest()
		{
			global::hotpatch.HotPatchEditor.Log = Debug.Log;

			var csharp_dll = Assembly.Load("Assembly-CSharp").Location;
			var csharp_firstpass_dll = Assembly.Load("Assembly-CSharp-firstpass").Location;
			global::hotpatch.HotPatchEditor.Active(
				new	[] { csharp_dll, csharp_firstpass_dll },
				(path) => path + ".mod.dll");
		}

		[MenuItem("Lua/Active Hot Patch", priority = 100)]
		public static void ActiveHotPatch()
		{
			global::hotpatch.HotPatchEditor.Log = Debug.Log;

			var csharp_dll = Assembly.Load("Assembly-CSharp").Location;
			var csharp_firstpass_dll = Assembly.Load("Assembly-CSharp-firstpass").Location;
			global::hotpatch.HotPatchEditor.Active(
				new	[] { csharp_dll, csharp_firstpass_dll });

			UnityEditorInternal.InternalEditorUtility.RequestScriptReload();
		}

		[MenuItem("Lua/Deactive Hot Patch", priority = 101)]
		static void DeactiveHotPatch()
		{
			var csharp_dll = Assembly.Load("Assembly-CSharp").Location;
			var csharp_firstpass_dll = Assembly.Load("Assembly-CSharp-firstpass").Location;
			File.Delete(csharp_dll);
			File.Delete(csharp_dll + ".mdb");
			File.Delete(csharp_firstpass_dll);
			File.Delete(csharp_firstpass_dll + ".mdb");
			AssetDatabase.Refresh();

			// trigger a script	copmile
			File.WriteAllText(
				Application.dataPath + "/_Dev_EmptyForRefresh.cs",
				"//	generated for recompiling "	+ DateTime.Now.ToString() +	"\n");
			File.WriteAllText(
				Application.dataPath + "/Plugins/_Dev_EmptyForRefresh-firstpass.cs",
				"//	generated for recompiling "	+ DateTime.Now.ToString() +	"\n");
			AssetDatabase.Refresh();
		}
	}
}