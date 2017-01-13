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

//	[LuaHotPatch]
	public void PatchThisOne()
	{
		throw new System.Exception("not patched");
	}

//	[LuaHotPatch]
	public void PatchThisOneWithParam(int a, int b)
	{
		throw new System.Exception("not patched");
	}

	public class A { }
//	[LuaHotPatch]
	public A PatchThisOneWithParamAndReturn(int a, int b)
	{
		throw new System.Exception("not patched");
	}

//	[LuaHotPatch]
	public int PatchThisOneWithParamAndReturnPrimitive(A a, int b)
	{
		throw new System.Exception("not patched");
	}

//	[LuaHotPatch]
	public int PatchOutParams(out A a, out int b, ref int c, ref A d)
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public void PatchOutParam_Boolean(out bool a)
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public void PatchOutParam_Int(out int a)
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public void PatchOutParam_Float(out float a)
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public void PatchOutParam_Decimal(out decimal a)
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public void PatchRefParam_Double(ref double a)
	{
		throw new System.Exception("not patched");
	}

	public struct StructToChange
	{
		public string str;
		public double value;
	}
	[LuaHotPatch]
	public void PatchRefParam_Struct(ref StructToChange a)
	{
		throw new System.Exception("not patched");
	}




	// type test below

	public void TestInt(out int a)
	{
		object[] arr = new object[10];
		a = (int)arr[0];
	}

	public void TestUint(out uint a)
	{
		object[] arr = new object[10];
		a = (uint)arr[0];
	}

	public void TestBoolean(out bool a)
	{
		object[] arr = new object[10];
		a = (bool)arr[0];
	}

	public void TestUint(out char a)
	{
		object[] arr = new object[10];
		a = (char)arr[0];
	}

	public void TestDecimal(out decimal a)
	{
		object[] arr = new object[10];
		a = (decimal)arr[0];
	}

	public void TestRefBoolean(ref bool a)
	{
		object[] arr = new object[10];
		a = (bool)arr[0];
	}

	public void TestRefDouble(ref double a)
	{
		object[] arr = new object[] { a };
		a = (double)arr[0];
	}
}

