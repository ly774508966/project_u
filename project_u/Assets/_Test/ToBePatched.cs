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
using lua.hotpatch;

public class ToBePatched
{
	[LuaHotPatch]
	public static void PatchMe()
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public void PatchThisOne()
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public void PatchThisOneWithParam(int a, int b)
	{
		throw new System.Exception("not patched");
	}

	public class A { }
	[LuaHotPatch]
	public A PatchThisOneWithParamAndReturn(int a, int b)
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public int PatchThisOneWithParamAndReturnPrimitive(A a, int b)
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public int PatchOutParams(out A a, out int b)
	{
		a = new A();
		b = 10;
		return 10;
	}

	public A CallTest(int a, int b)
	{
		System.Reflection.MethodInfo k = null;
		object retval;
		LuaHotPatchLoader.Hub(k, this, out retval, a, b);
		return (A)retval;
	}

	public A CallTest1(out A a, out int b)
	{
		a = null;
		b = 10;
		System.Reflection.MethodInfo k = null;
		object retval;
		LuaHotPatchLoader.Hub(k, this, out retval, a, b);
		return (A)retval;
	}

	public int CallTest2(A a, int b)
	{
		System.Reflection.MethodInfo k = null;
		object retval;
		LuaHotPatchLoader.Hub(k, this, out retval, a, b);
		if (retval != null)
		{
			var type = retval.GetType();
			if (type.IsPrimitive)
			{
				return (int)retval;
			}
		}
		return 0;
	}
}

