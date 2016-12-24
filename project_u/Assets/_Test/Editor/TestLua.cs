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
﻿using UnityEngine;
using System.Collections;
using NUnit.Framework;

namespace lua.test
{
	struct TestStruct
	{
		public float floatValue;
		public int intValue;
		public string stringValue;
	}

	class TestRetValue
	{
		public string value = "A Test String";

		~TestRetValue()
		{
			Debug.Log("TestRetValue destroyed.");
		}
	}

	class TestClass
	{
		public int test = 42;
		public int TestMethod()
		{
			return test;
		}
		public static int TestStaticMethod()
		{
			return 43;
		}

		public string TestMethodWithParam(int p0, string p1)
		{
			return p1 + p0;
		}

		public TestRetValue GetRetValue()
		{
			return new TestRetValue();
		}

		public int TestOutParam(int k, out int t1, out Vector3 t2, ref int t3)
		{
			t1 = 11;
			t2 = new Vector3(1, 2, 3);
			t3 = t1 + t3;
			return k+10;
		}

		public int TestOverloading(int a)
		{
			return test;
		}
		public int TestOverloading(int a, string b)
		{
			return test;
		}
	}

	class TestDerivedClass : TestClass
	{

	}


	public class TestLua
	{
		Lua L;
		TestClass obj;
		object objRef;



		[TestFixtureSetUp]
		public void SetUp()
		{
			L = new Lua();
			obj = new TestClass();
			objRef = Lua.MakeRef(L.luaState, obj);
		}

		[TestFixtureTearDown]
		public void TearDown()
		{
			Lua.Unref(L.luaState, objRef);
			Lua.CleanMethodCache();
		}

		[Test]
		public void PreTest()
		{
			var type = typeof(System.Int32);
			double t = 10.0;
			var converted = System.Convert.ChangeType(t, type);
			Assert.True(converted is int);
			Assert.AreEqual(10, (int)converted);
		}

		[Test]
		public void TestAccessFieldFromLua()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState, 
				"function Test(obj)\n" + 
				"  return obj.test\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L.luaState, -1));
			Lua.PushRef(L.luaState, objRef);
			Lua.Call(L.luaState, 1, 1);
			Assert.AreEqual(Api.LUA_TNUMBER, Api.lua_type(L.luaState, -1));
			Assert.AreEqual(obj.test, Api.lua_tointeger(L.luaState, -1));
			Api.lua_pop(L.luaState, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}

		[Test]
		public void TestCallMethodFromLua()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState, 
				"function Test(obj)\n" + 
				"  return obj:TestMethod()\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L.luaState, -1));
			Lua.PushRef(L.luaState, objRef);
			Lua.Call(L.luaState, 1, 1);
			Assert.AreEqual(Api.LUA_TNUMBER, Api.lua_type(L.luaState, -1));
			Assert.AreEqual(obj.test, Api.lua_tointeger(L.luaState, -1));
			Api.lua_pop(L.luaState, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}

		[Test]
		[ExpectedException(typeof(lua.LuaException))]
		public void TestCallMethodFromLua_IncorrectSyntax()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState, 
				"function Test(obj)\n" + 
				"  return obj.TestMethod()\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L.luaState, -1));
			Lua.PushRef(L.luaState, objRef);
			try
			{
				Lua.Call(L.luaState, 1, 1);
			}
			catch (System.Exception e)
			{
				Api.lua_settop(L.luaState, stackTop);
				throw e;
			}
			Assert.Fail("never get here");
		}

		[Test]
		public void TestCallStaticMethodFromLua()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState, 
				"function Test(obj)\n" + 
				"  return obj.TestStaticMethod()\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L.luaState, -1));
			Lua.PushRef(L.luaState, objRef);
			Lua.Call(L.luaState, 1, 1);
			Assert.AreEqual(Api.LUA_TNUMBER, Api.lua_type(L.luaState, -1));
			Assert.AreEqual(43, Api.lua_tointeger(L.luaState, -1));
			Api.lua_pop(L.luaState, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}

		[Test]
		[ExpectedException(exceptionType: typeof(lua.LuaException))]
		public void TestCallStaticMethodFromLua_IncorrectSyntax()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState, 
				"function Test(obj)\n" + 
				"  return obj:TestStaticMethod()\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L.luaState, -1));
			Lua.PushRef(L.luaState, objRef);
			try
			{
				Lua.Call(L.luaState, 1, 1);
			}
			catch (System.Exception e)
			{
				Api.lua_settop(L.luaState, stackTop);
				throw e;
			}
			Assert.Fail("never get here");
		}

		[Test]
		public void TestCallStaticMethodFromLua_IncorrectSyntax_PCALL()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState, 
				"function Test(obj)\n" + 
				"  return obj:TestStaticMethod()\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L.luaState, -1));
			Lua.PushRef(L.luaState, objRef);
			if (Api.LUA_OK != Api.lua_pcall(L.luaState, 1, 1, 0))
			{
				Assert.AreEqual(stackTop + 1, Api.lua_gettop(L.luaState));
				Api.lua_pop(L.luaState, 1);
			}
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}


		[Test]
		public void TestCallMethodWithParamFromLua()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState, 
				"function Test(obj)\n" + 
				"  return obj:TestMethodWithParam(10, 'TestString')\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L.luaState, -1));
			Lua.PushRef(L.luaState, objRef);
			Lua.Call(L.luaState, 1, 1);
			Assert.AreEqual(Api.LUA_TSTRING, Api.lua_type(L.luaState, -1));
			Assert.AreEqual("TestString10", Api.lua_tostring(L.luaState, -1));
			Api.lua_pop(L.luaState, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}

		void SetupTestMethodCache()
		{
			Api.luaL_dostring(L.luaState, 
				"function Test(obj)\n" + 
				"  return obj:TestMethodWithParam(10, 'TestString')\n" + 
				"end");
		}

		void MethodCacheTestLoop()
		{
			Api.lua_getglobal(L.luaState, "Test");
			for (int i = 0; i < 10000; ++i)
			{
				Api.lua_pushvalue(L.luaState, -1);
				Lua.PushRef(L.luaState, objRef);
				Lua.Call(L.luaState, 1, 1);
				Api.lua_pop(L.luaState, 1);
			}
			Api.lua_pop(L.luaState, 1);
		}

		[Test]
		public void TestMethodCache()
		{
			SetupTestMethodCache();
			Lua.UseMethodCache();
			var stackTop = Api.lua_gettop(L.luaState);
			MethodCacheTestLoop();
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}


		[Test]
		public void TestWithoutMethodCache()
		{
			SetupTestMethodCache();
			Lua.UseMethodCache(false);
			var stackTop = Api.lua_gettop(L.luaState);
			MethodCacheTestLoop();
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
			Lua.UseMethodCache(); // revert it
		}

		[Test]
		public void TestAccessingObjectReturned()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState, 
				"function Test(obj)\n" + 
				"  return obj:GetRetValue().value\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L.luaState, -1));
			Lua.PushRef(L.luaState, objRef);
			Lua.Call(L.luaState, 1, 1);
			Assert.AreEqual(Api.LUA_TSTRING, Api.lua_type(L.luaState, -1));
			var ret = new TestRetValue();
			Assert.AreEqual(ret.value, Api.lua_tostring(L.luaState, -1));
			Api.lua_pop(L.luaState, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}

		[Test]
		public void TestStructValue()
		{
			var stackTop = Api.lua_gettop(L.luaState);

			var value = new TestStruct();
			value.floatValue = 10f;
			value.intValue = 20;
			value.stringValue = "30";

			Api.luaL_dostring(L.luaState, 
				"function Test(obj)\n" + 
				"  return obj.floatValue .. obj.intValue .. obj.stringValue\n" + 
				"end");

			Api.lua_getglobal(L.luaState, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L.luaState, -1));
			Lua.PushCsharpValue(L.luaState, value);
			Lua.Call(L.luaState, 1, 1);
			Assert.AreEqual(Api.LUA_TSTRING, Api.lua_type(L.luaState, -1));
			Assert.AreEqual("10.020.030", Api.lua_tostring(L.luaState, -1));
			Api.lua_pop(L.luaState, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}

		[Test]
		public void TestSettingField()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState, 
				"function Test(obj)\n" + 
				"  obj.test = 13\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L.luaState, -1));
			var obj = new TestClass();
			Lua.PushCsharpValue(L.luaState, obj);
			Lua.Call(L.luaState, 1, 0);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
			Assert.AreEqual(13, obj.test);
		}

		[Test]
		public void TestArray_GetElement()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			var testArray = new TestClass[10];
			for (int i = 0; i < testArray.Length; ++i)
			{
				testArray[i] = new TestClass();
				testArray[i].test = i;
			}
			Api.luaL_dostring(L.luaState,
				"function Test(obj)\n" + 
				"  return obj[4]\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Lua.PushCsharpValue(L.luaState, testArray);
			Lua.Call(L.luaState, 1, 1);
			Assert.True(Api.lua_isuserdata(L.luaState, -1));
			var obj = Lua.ToCsharpObject(L.luaState, -1);
			Assert.AreEqual(testArray[4], obj);
			Assert.AreEqual(4, ((TestClass)obj).test);
			Api.lua_pop(L.luaState, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}

		[Test]
		public void TestArray_SetElement()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			var testArray = new int[10];
			for (int i = 0; i < testArray.Length; ++i)
			{
				testArray[i] = i;
			}
			Api.luaL_dostring(L.luaState,
				"function Test(obj)\n" + 
				"  obj[4] = 42\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Lua.PushCsharpValue(L.luaState, testArray);
			Lua.Call(L.luaState, 1, 0);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
			Assert.AreEqual(42, testArray[4]);
		}

		[Test]
		public void TestCreateCsharpObjectFromLua()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState,
				"Vector3 = csharp.import('UnityEngine.Vector3, UnityEngine')\n" +
				"function TestCreateVector3()\n" +
				"  return Vector3(1, 2, 3)\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "TestCreateVector3");
			Lua.Call(L.luaState, 0, 1);
			var v = (Vector3)Lua.ToCsharpObject(L.luaState, -1);
			Assert.AreEqual(new Vector3(1f, 2f, 3f), v);
			Api.lua_pop(L.luaState, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}


		[Test]
		public void TestCreateCsharpObjectFromLua_AndAccessIt()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState,
				"Vector3 = csharp.import('UnityEngine.Vector3, UnityEngine')\n" +
				"function TestCreateVector3()\n" +
				"  local v = Vector3(1, 2, 3)\n" + 
				"  return v.x + v.y\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "TestCreateVector3");
			Lua.Call(L.luaState, 0, 1);
			var v = Api.lua_tonumber(L.luaState, -1);
			Assert.AreEqual(3.0, v);
			Api.lua_pop(L.luaState, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}


		public class MyClass
		{
			public static int value = 20;
		}

		[Test]
		public void TestSetStaticFieldOfClass()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState,
				"MyClass = csharp.import('lua.test.TestLua+MyClass, Assembly-CSharp-Editor')\n" +
				"function Test()\n" +
				"  MyClass.value = 42\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Lua.Call(L.luaState, 0, 0);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
			Assert.AreEqual(42, MyClass.value);
		}


		[Test]
		public void TestSetStaticFieldOfClass_ImportGlobal()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Lua.ImportGlobal(L.luaState, typeof(MyClass), "Global_MyClass");
			Api.luaL_dostring(L.luaState,
				"function Test()\n" +
				"  Global_MyClass.value = 42\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Lua.Call(L.luaState, 0, 0);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
			Assert.AreEqual(42, MyClass.value);
		}


		[Test]
		public void TestDebugLog()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Lua.Import(L.luaState, typeof(UnityEngine.Debug));
			Api.lua_setglobal(L.luaState, "UnityDebug");
			Api.luaL_dostring(L.luaState,
				"function Test()\n" +
				"  UnityDebug.Log('Hello UnityDebug')\n" + 
				"  UnityDebug.Log(42)\n" + 
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Lua.Call(L.luaState, 0, 0);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}

		[Test]
		public void TestDumpAndLoad()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Lua.Import(L.luaState, typeof(UnityEngine.Debug));
			Api.lua_setglobal(L.luaState, "UnityDebug");
			Api.luaL_dostring(L.luaState,
				"function Test()\n" +
				"  UnityDebug.Log('Hello UnityDebug')\n" + 
				"  UnityDebug.Log(42)\n" + 
				"  return 10\n"	+
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			var chunk = Lua.DumpChunk(L.luaState);
			Assert.True(chunk != null && chunk.Length > 0);
			Api.lua_pop(L.luaState, 1);
			Lua.LoadChunk(L.luaState, chunk, "Test_LoadFromChunk");
			Assert.True(Api.lua_isfunction(L.luaState, -1));
			Lua.Call(L.luaState, 0, 1);
			Assert.AreEqual(10, Api.lua_tonumber(L.luaState, -1));
			Api.lua_pop(L.luaState, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}


		[Test]
		public void TestCallFunctionOutParams()
		{
			var stackTop = Api.lua_gettop(L.luaState);
			Api.luaL_dostring(L.luaState,
				"function TestOutParam(obj)\n" +
				"  ret, t1, t2, t3 = obj:TestOutParam(10, nil, nil, 10)\n" + 
				"  return ret, t1, t2, t3\n" +
				"end");

			Api.lua_getglobal(L.luaState, "TestOutParam");
			Lua.PushRef(L.luaState, objRef);
			Lua.Call(L.luaState, 1, Api.LUA_MULTRET);

			Assert.AreEqual(20.0, Api.lua_tonumber(L.luaState, -4));
			Assert.AreEqual(11.0, Api.lua_tonumber(L.luaState, -3));
			var value = (Vector3)Lua.CsharpValueFrom(L.luaState, -2);
			Assert.AreEqual(1f, value.x);
			Assert.AreEqual(2f, value.y);
			Assert.AreEqual(3f, value.z);
			// t3 =	t1 + t3
			Assert.AreEqual(21.0, Api.lua_tonumber(L.luaState, -1));
			Api.lua_pop(L.luaState, 4);

			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}


		public class SomeClass
		{
			public int MeCallYou(System.Func<int> complete)
			{
				return complete();
			}
		}

		[Test]
		public void TestCallNativeFuncWithLuaCallback()
		{
			var stackTop = Api.lua_gettop(L.luaState);

			var inst = new SomeClass();

			Api.luaL_dostring(
				L.luaState,
				"function Test(obj)\n" +
				" obj:MeCallYou(function() \n" +
				"     Debug.Log('being called')\n" +
				"     return 10\n" +
				"   end)\n" +
				"end");
			Api.lua_getglobal(L.luaState, "Test");
			Lua.PushCsharpValue(L.luaState, inst);
			Lua.Call(L.luaState, 1, 1);
			Assert.AreEqual(10.0, Api.lua_tonumber(L.luaState, -1));

			Assert.AreEqual(stackTop, Api.lua_gettop(L.luaState));
		}

	}
}