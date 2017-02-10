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
using NUnit.Framework;

namespace lua.test
{
	[TestFixture]
	class TestLuaHotPatch
	{

		Lua L;
		[TestFixtureSetUp]
		public void SetUp()
		{
			L = new Lua();
		}

		[TestFixtureTearDown]
		public void TearDown()
		{
			L.Dispose();
			L = null;
		}

		[Test]
		public void TestPatchMe()
		{
			lua.hotpatch.LuaHotPatchLoader.Patch(
				"System.Void ToBePatched::PatchMe()",
				"function() return true end");
			ToBePatched.PatchMe();
		}

		[Test]
		public void TestPatchOutParam_Boolean()
		{
			lua.hotpatch.LuaHotPatchLoader.Patch(
				"System.Void ToBePatched::PatchOutParam_Boolean(System.Boolean&)",
				"function(this) return true, true end"); // first true is breakReturn, the second is out boolean
			var t = new ToBePatched();
			bool b;
			t.PatchOutParam_Boolean(out b);
			Assert.True(b);
		}

		[Test]
		public void TestPatchOutParam_Int()
		{
			lua.hotpatch.LuaHotPatchLoader.Patch(
				"System.Void ToBePatched::PatchOutParam_Int(System.Int32&)",
				"function(this) return true, 42 end"); // first true is breakReturn, the second is out boolean
			var t = new ToBePatched();
			int b;
			t.PatchOutParam_Int(out b);
			Assert.AreEqual(42, b);
		}

		[Test]
		public void TestPatchOutParam_Float()
		{
			lua.hotpatch.LuaHotPatchLoader.Patch(
				"System.Void ToBePatched::PatchOutParam_Float(System.Single&)",
				"function(this) return true, 42 end"); // first true is breakReturn, the second is out boolean
			var t = new ToBePatched();
			float b;
			t.PatchOutParam_Float(out b);
			Assert.AreEqual(42f, b);
		}

		[Test]
		public void TestPatchOutParam_Decimal()
		{
			lua.hotpatch.LuaHotPatchLoader.Patch(
				"System.Void ToBePatched::PatchOutParam_Decimal(System.Decimal&)",
				"function(this) return true, 42 end"); // first true is breakReturn, the second is out boolean
			var t = new ToBePatched();
			decimal b;
			t.PatchOutParam_Decimal(out b);
			Assert.AreEqual(42, b);
		}

		[Test]
		public void TestPatchRefParam_Double()
		{
			lua.hotpatch.LuaHotPatchLoader.Patch(
				"System.Void ToBePatched::PatchRefParam_Double(System.Double&)",
				"function(this, p) return true, 22 + p end"); // first true is breakReturn, the second is out boolean
			var t = new ToBePatched();
			double b = 20;
			t.PatchRefParam_Double(ref b);
			Assert.AreEqual(42, b);
		}


		[Test]
		public void TestPatchRefParam_Struct()
		{
			lua.hotpatch.LuaHotPatchLoader.Patch(
				"System.Void ToBePatched::PatchRefParam_Struct(ToBePatched/StructToChange&)",
				"function(this, p)\n" +
				"  p.str = 'hello'\n" +
				"  p.value = 42\n" +
				"  return true, p\n" +
				"end"); 
			var t = new ToBePatched();
			ToBePatched.StructToChange st = new ToBePatched.StructToChange();
			t.PatchRefParam_Struct(ref st);
			Assert.AreEqual("hello", st.str);
			Assert.AreEqual(42, st.value);
		}



		[Test]
		public void TestPatchRefParam_Class()
		{
			lua.hotpatch.LuaHotPatchLoader.Patch(
				"System.Void ToBePatched::PatchRefParam_Class(ToBePatched/ClassToChange&)",
				"function(this, p)\n" +
				"  p.str = 'hello'\n" +
				"  p.value = 42\n" +
				"  return true, p\n" +
				"end"); 
			var t = new ToBePatched();
			ToBePatched.ClassToChange st = new ToBePatched.ClassToChange();
			t.PatchRefParam_Class(ref st);
			Assert.AreEqual("hello", st.str);
			Assert.AreEqual(42, st.value);
		}

		[Test]
		public void TestPatchOrNotPatch()
		{
			var t = new ToBePatched();
			var r = t.SubOrAdd(10, 20);
			Assert.AreEqual(30, r);
			lua.hotpatch.LuaHotPatchLoader.Patch(
				"System.Int32 ToBePatched::InnerSubOrAdd(System.Int32,System.Int32)",
				"function(this, a, b)\n" +
				"  return true, a - b\n" +
				"end"); 
			r = t.SubOrAdd(10, 20);
			Assert.AreEqual(-10, r);
			lua.hotpatch.LuaHotPatchLoader.Remove(
				"System.Int32 ToBePatched::InnerSubOrAdd(System.Int32,System.Int32)");
			r = t.SubOrAdd(10, 20);
			Assert.AreEqual(30, r);
		}

		[Test]
		public void TestClassToBePached_Constructor()
		{
			lua.hotpatch.LuaHotPatchLoader.Patch(
				"System.Void ClassToBePatched::.ctor(System.Int32)",
				"function(this, value)\n" +
				"  return true, 12345\n" + 
				"end");

			var t = new ClassToBePatched(10);
			Assert.AreEqual(12345, t.intValue);
		}
	}

}