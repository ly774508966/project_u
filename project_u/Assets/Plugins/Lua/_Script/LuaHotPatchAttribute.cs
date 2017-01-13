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
using System.Linq;

namespace lua.hotpatch
{
	public class LuaHotPatchAttribute : Attribute
	{
		public static MethodInfo[] GetMethods()
		{
			return Assembly.Load("Assembly-CSharp").GetTypes()
				.Union(Assembly.Load("Assembly-CSharp-firstpass").GetTypes())
				.SelectMany(t => t.GetMethods(BindingFlags.Static |	BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
				.Where(m =>	m.GetCustomAttributes(typeof(LuaHotPatchAttribute), false).Length	> 0)
				.ToArray();
		}
	}

	public class LuaHotPatchHubAttribute : Attribute
	{
		public static MethodInfo GetMethod()
		{
			var methods = Assembly.Load("Assembly-CSharp").GetTypes()
				.Union(Assembly.Load("Assembly-CSharp-firstpass").GetTypes())
				.SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public))
				.Where(m => m.GetCustomAttributes(typeof(LuaHotPatchHubAttribute), false).Length > 0)
				.ToArray();
			if (methods.Length > 0)
			{
				return methods[0];
			}
			return null;
		}
	}
}
