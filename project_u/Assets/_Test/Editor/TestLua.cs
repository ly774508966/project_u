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
			return "ret_" + p1 + p0;
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

	[TestFixture]
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
			objRef = L.MakeRefTo(obj);
		}

		[TestFixtureTearDown]
		public void TearDown()
		{
			L.Unref(objRef);
			Lua.CleanMethodCache();
			L.Dispose();
		}

		[Test]
		public void PreTest()
		{
			Api.lua_settop(L, 0);

			Debug.Log(Lua.DebugStack(L));
			Api.lua_pushnumber(L, 1);
			Api.lua_pushnumber(L, 2);
			Api.lua_pushnumber(L, 3);
			Api.lua_pushnumber(L, 4);
			var top  = Api.lua_gettop(L) - 3;
			Api.lua_pushnumber(L, 5);
			Api.lua_insert(L, top);
			Debug.Log(Lua.DebugStack(L));
			Api.lua_pop(L, 4);
			Api.lua_pushboolean(L, true);
			Api.lua_remove(L, top);
			Debug.Log(Lua.DebugStack(L));

			Api.lua_pop(L, 1);



			var type = typeof(System.Int32);
			double t = 10.0;
			var converted = System.Convert.ChangeType(t, type);
			Assert.True(converted is int);
			Assert.AreEqual(10, (int)converted);
		}

		[Test]
		public void TestAccessFieldFromLua()
		{
			Api.lua_settop(L, 0);

			Api.luaL_dostring(L, 
				"function Test(obj)\n" + 
				"  return obj.test\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L, -1));
			L.PushRef(objRef);
			Lua.Call(L, 1, 1);
			Assert.AreEqual(Api.LUA_TNUMBER, Api.lua_type(L, -1));
			Assert.AreEqual(obj.test, Api.lua_tointeger(L, -1));
			Api.lua_pop(L, 1);

			Assert.AreEqual(0, Api.lua_gettop(L));

		}

		[Test]
		public void TestCallMethodFromLua()
		{
			Api.lua_settop(L, 0);

			Api.luaL_dostring(L, 
				"function Test(obj)\n" + 
				"  return obj:TestMethod()\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L, -1));
			L.PushRef(objRef);
			Lua.Call(L, 1, 1);
			Assert.AreEqual(Api.LUA_TNUMBER, Api.lua_type(L, -1));
			Assert.AreEqual(obj.test, Api.lua_tointeger(L, -1));
			Api.lua_pop(L, 1);


			Assert.AreEqual(0, Api.lua_gettop(L));
		}

		[Test]
		[ExpectedException(typeof(lua.LuaException))]
		public void TestCallMethodFromLua_IncorrectSyntax()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Api.luaL_dostring(L, 
				"function Test(obj)\n" + 
				"  return obj.TestMethod()\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L, -1));
			L.PushRef(objRef);
			try
			{
				Lua.Call(L, 1, 1);
			}
			catch (System.Exception e)
			{
				Api.lua_settop(L, stackTop);
				throw e;
			}
			Assert.Fail("never get here");
		}

		[Test]
		public void TestCallStaticMethodFromLua()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Api.luaL_dostring(L, 
				"function Test(obj)\n" + 
				"  return obj.TestStaticMethod()\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L, -1));
			L.PushRef(objRef);
			Lua.Call(L, 1, 1);
			Assert.AreEqual(Api.LUA_TNUMBER, Api.lua_type(L, -1));
			Assert.AreEqual(43, Api.lua_tointeger(L, -1));
			Api.lua_pop(L, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L));


			Assert.AreEqual(0, Api.lua_gettop(L));

		}

		[Test]
		[ExpectedException(exceptionType: typeof(lua.LuaException))]
		public void TestCallStaticMethodFromLua_IncorrectSyntax()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Api.luaL_dostring(L, 
				"function Test(obj)\n" + 
				"  return obj:TestStaticMethod()\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L, -1));
			L.PushRef(objRef);
			try
			{
				Lua.Call(L, 1, 1);
			}
			catch (System.Exception e)
			{
				Api.lua_settop(L, stackTop);
				throw e;
			}
			Assert.Fail("never get here");
		}

		[Test]
		public void TestCallStaticMethodFromLua_IncorrectSyntax_PCALL()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Api.luaL_dostring(L, 
				"function Test(obj)\n" + 
				"  return obj:TestStaticMethod()\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L, -1));
			L.PushRef(objRef);
			if (Api.LUA_OK != Api.lua_pcall(L, 1, 1, 0))
			{
				Assert.AreEqual(stackTop + 1, Api.lua_gettop(L));
				Api.lua_pop(L, 1);
			}
			Assert.AreEqual(stackTop, Api.lua_gettop(L));

			Assert.AreEqual(0, Api.lua_gettop(L));
		}


		[Test]
		public void TestCallMethodWithParamFromLua()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Api.luaL_dostring(L, 
				"function Test(obj)\n" + 
				"  return obj:TestMethodWithParam(11, 'TestString')\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L, -1));
			L.PushRef(objRef);
			Lua.Call(L, 1, 1);
			Assert.AreEqual(Api.LUA_TSTRING, Api.lua_type(L, -1));
			Assert.AreEqual("ret_TestString11", Api.lua_tostring(L, -1));
			Api.lua_pop(L, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L));

			Assert.AreEqual(0, Api.lua_gettop(L));
		}

		void SetupTestMethodCache()
		{
			Api.luaL_dostring(L, 
				"function Test(obj)\n" + 
				"  return obj:TestMethodWithParam(10, 'TestString')\n" + 
				"end");
		}

		void MethodCacheTestLoop()
		{
			var top = Api.lua_gettop(L);
			Api.lua_getglobal(L, "Test");
			for (int i = 0; i < 10000; ++i)
			{
				var innerTop = Api.lua_gettop(L);
				Assert.AreEqual(1, Api.lua_gettop(L));

				Api.lua_pushvalue(L, -1); // push function
				if (!Api.lua_isfunction(L, -1))
				{
					Debug.LogError(Lua.DebugStack(L));
					Assert.Fail("Not a func? " + i);
				}
				L.PushRef(objRef); // push obj
				Assert.True(Api.lua_isuserdata(L, -1));
				Lua.Call(L, 1, 1); // call obj:func
				Api.lua_pop(L, 1); // pop ret
				if (innerTop != Api.lua_gettop(L))
				{
					Debug.LogError(Lua.DebugStack(L));
				}
				Assert.AreEqual(innerTop, Api.lua_gettop(L));
			}
			Api.lua_pop(L, 1);
			Assert.AreEqual(top, Api.lua_gettop(L));
		}

		[Test]
		public void TestMethodCache()
		{
			Api.lua_settop(L, 0);

			SetupTestMethodCache();
			Lua.UseMethodCache();
			MethodCacheTestLoop();

			Assert.AreEqual(0, Api.lua_gettop(L));
		}


		[Test]
		public void TestWithoutMethodCache()
		{
			Api.lua_settop(L, 0);

			SetupTestMethodCache();
			Lua.UseMethodCache(false);
			MethodCacheTestLoop();
			Lua.UseMethodCache(); // revert it

			Assert.AreEqual(0, Api.lua_gettop(L));
		}

		[Test]
		public void TestAccessingObjectReturned()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Api.luaL_dostring(L, 
				"function Test(obj)\n" + 
				"  return obj:GetRetValue().value\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L, -1));
			L.PushRef(objRef);
			Lua.Call(L, 1, 1);
			Assert.AreEqual(Api.LUA_TSTRING, Api.lua_type(L, -1));
			var ret = new TestRetValue();
			Assert.AreEqual(ret.value, Api.lua_tostring(L, -1));
			Api.lua_pop(L, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L));

			Assert.AreEqual(0, Api.lua_gettop(L));
		}

		[Test]
		public void TestStructValue()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);

			var value = new TestStruct();
			value.floatValue = 10f;
			value.intValue = 20;
			value.stringValue = "30";

			Api.luaL_dostring(L, 
				"function Test(obj)\n" + 
				"  return obj.floatValue .. obj.intValue .. obj.stringValue\n" + 
				"end");

			Api.lua_getglobal(L, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L, -1));
			L.PushValue(value);
			Lua.Call(L, 1, 1);
			Assert.AreEqual(Api.LUA_TSTRING, Api.lua_type(L, -1));
			Assert.AreEqual("10.020.030", Api.lua_tostring(L, -1));
			Api.lua_pop(L, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L));

			Assert.AreEqual(0, Api.lua_gettop(L));
		}

		[Test]
		public void TestSettingField()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Api.luaL_dostring(L, 
				"function Test(obj)\n" + 
				"  obj.test = 13\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Assert.AreEqual(Api.LUA_TFUNCTION, Api.lua_type(L, -1));
			var obj = new TestClass();
			L.PushValue(obj);
			Lua.Call(L, 1, 0);
			Assert.AreEqual(stackTop, Api.lua_gettop(L));
			Assert.AreEqual(13, obj.test);

			Assert.AreEqual(0, Api.lua_gettop(L));
		}

		[Test]
		public void TestArray_GetElement()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			var testArray = new TestClass[10];
			for (int i = 0; i < testArray.Length; ++i)
			{
				testArray[i] = new TestClass();
				testArray[i].test = i;
			}
			Api.luaL_dostring(L,
				"function Test(obj)\n" + 
				"  return obj[4]\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			L.PushValue(testArray);
			Lua.Call(L, 1, 1);
			Assert.True(Api.lua_isuserdata(L, -1));
			var obj = L.ObjectAt(-1);
			Assert.AreEqual(testArray[4], obj);
			Assert.AreEqual(4, ((TestClass)obj).test);
			Api.lua_pop(L, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L));

			Assert.AreEqual(0, Api.lua_gettop(L));
		}

		[Test]
		public void TestArray_SetElement()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			var testArray = new int[10];
			for (int i = 0; i < testArray.Length; ++i)
			{
				testArray[i] = i;
			}
			Api.luaL_dostring(L,
				"function Test(obj)\n" + 
				"  obj[4] = 42\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			L.PushValue(testArray);
			Lua.Call(L, 1, 0);
			Assert.AreEqual(stackTop, Api.lua_gettop(L));
			Assert.AreEqual(42, testArray[4]);

			Assert.AreEqual(0, Api.lua_gettop(L));
		}

		[Test]
		public void TestCreateCsharpObjectFromLua()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Api.luaL_dostring(L,
				"Vector3 = csharp.import('UnityEngine.Vector3, UnityEngine')\n" +
				"function TestCreateVector3()\n" +
				"  return Vector3(1, 2, 3)\n" + 
				"end");
			Api.lua_getglobal(L, "TestCreateVector3");
			Lua.Call(L, 0, 1);
			var v = (Vector3)L.ObjectAt(-1);
			Assert.AreEqual(new Vector3(1f, 2f, 3f), v);
			Api.lua_pop(L, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L));

			Assert.AreEqual(0, Api.lua_gettop(L));
		}


		[Test]
		public void TestCreateCsharpObjectFromLua_AndAccessIt()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Api.luaL_dostring(L,
				"Vector3 = csharp.import('UnityEngine.Vector3, UnityEngine')\n" +
				"function TestCreateVector3()\n" +
				"  local v = Vector3(1, 2, 3)\n" + 
				"  return v.x + v.y\n" + 
				"end");
			Api.lua_getglobal(L, "TestCreateVector3");
			Lua.Call(L, 0, 1);
			var v = Api.lua_tonumber(L, -1);
			Assert.AreEqual(3.0, v);
			Api.lua_pop(L, 1);
			Assert.AreEqual(stackTop, Api.lua_gettop(L));

			Assert.AreEqual(0, Api.lua_gettop(L));
		}


		public class MyClass
		{
			public static int value = 20;
		}

		[Test]
		public void TestSetStaticFieldOfClass()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Api.luaL_dostring(L,
				"MyClass = csharp.import('lua.test.TestLua+MyClass, Assembly-CSharp-Editor')\n" +
				"function Test()\n" +
				"  MyClass.value = 42\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Lua.Call(L, 0, 0);
			Assert.AreEqual(stackTop, Api.lua_gettop(L));
			Assert.AreEqual(42, MyClass.value);

			Assert.AreEqual(0, Api.lua_gettop(L));
		}


		[Test]
		public void TestSetStaticFieldOfClass_ImportGlobal()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Lua.ImportGlobal(L, typeof(MyClass), "Global_MyClass");
			Api.luaL_dostring(L,
				"function Test()\n" +
				"  Global_MyClass.value = 42\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Lua.Call(L, 0, 0);
			Assert.AreEqual(stackTop, Api.lua_gettop(L));
			Assert.AreEqual(42, MyClass.value);

			Assert.AreEqual(0, Api.lua_gettop(L));
		}


		[Test]
		public void TestDebugLog()
		{
			Api.lua_settop(L, 0);

			Lua.Import(L, typeof(UnityEngine.Debug));
			Api.lua_setglobal(L, "UnityDebug");
			Api.luaL_dostring(L,
				"function Test()\n" +
				"  UnityDebug.Log('Hello UnityDebug')\n" + 
				"  UnityDebug.Log(42)\n" + 
				"end");
			Api.lua_getglobal(L, "Test");
			Lua.Call(L, 0, 0);

			Assert.AreEqual(0, Api.lua_gettop(L));
		}

		[Test]
		public void TestDumpAndLoad()
		{
			Api.lua_settop(L, 0);

			Lua.Import(L, typeof(UnityEngine.Debug));
			Api.lua_setglobal(L, "UnityDebug");
			Api.luaL_dostring(L,
				"function Test()\n" +
				"  UnityDebug.Log('Hello UnityDebug')\n" + 
				"  UnityDebug.Log(42)\n" + 
				"  return 10\n"	+
				"end");
			Api.lua_getglobal(L, "Test");
			var chunk = Lua.DumpChunk(L);
			Assert.True(chunk != null && chunk.Length > 0);
			Api.lua_pop(L, 1);
			Lua.LoadChunk(L, chunk, "Test_LoadFromChunk");
			Assert.True(Api.lua_isfunction(L, -1));
			Lua.Call(L, 0, 1);
			Assert.AreEqual(10, Api.lua_tonumber(L, -1));
			Api.lua_pop(L, 1);

			Assert.AreEqual(0, Api.lua_gettop(L));

		}

//*
		[Test]
		public void TestCallFunctionOutParams()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);
			Api.luaL_dostring(L,
				"function TestOutParam(obj)\n" +
				"  ret, t1, t2, t3 = obj:TestOutParam(10, nil, nil, 10)\n" + 
				"  return ret, t1, t2, t3\n" +
				"end");

			Api.lua_getglobal(L, "TestOutParam");
			L.PushRef(objRef);
			Lua.Call(L, 1, Api.LUA_MULTRET);

			Assert.AreEqual(20.0, Api.lua_tonumber(L, -4));
			Assert.AreEqual(11.0, Api.lua_tonumber(L, -3));
			var value = (Vector3)L.ValueAt(-2);
			Assert.AreEqual(1f, value.x);
			Assert.AreEqual(2f, value.y);
			Assert.AreEqual(3f, value.z);
			// t3 =	t1 + t3
			Assert.AreEqual(21.0, Api.lua_tonumber(L, -1));
			Api.lua_pop(L, 4);

			Assert.AreEqual(stackTop, Api.lua_gettop(L));
		}
//*/

		public class SomeClass
		{
			public int MeCallYou(lua.FuncTools.Func<int> complete)
			{
				return complete.Invoke();
			}

			public int MeCallYou2(lua.FuncTools.Func<int> complete)
			{
				return complete.Invoke("called in MeCallYou2");
			}
		}
//*
		[Test]
		public void TestCallNativeFuncWithLuaCallback()
		{
			Api.lua_settop(L, 0);
			var inst = new SomeClass();
			var re = L.MakeRefTo(inst);
			Api.luaL_dostring(
				L,
				"function Test(obj)\n" +
				" return obj:MeCallYou(function() \n" +
				"	  return 10\n" +
				"	end)\n"	+
				"end");
			Assert.AreEqual(0, Api.lua_gettop(L));
			Api.lua_getglobal(L, "Test");
			for (int i = 0; i < 1000; ++i)
			{
				Api.lua_pushvalue(L, -1);
				L.PushRef(re);
				Lua.Call(L, 1, 1);
				Assert.AreEqual(10.0, Api.lua_tonumber(L, -1));
				Api.lua_pop(L, 1);
			}

			Api.lua_pop(L, 1);
			Assert.AreEqual(0, Api.lua_gettop(L));
		}
//*/
/*
		[Test]
		public void TestWrapperToLuaFuncToolsFunc()
		{
			Api.lua_settop(L, 0);

			var a = new SomeClass();
			var val = a.MeCallYou2(lua.FuncTools.Wrap<string, int>(
				(str) => {
					Debug.Log(str);
					return 10;
				}));
			Assert.AreEqual(10, val);

			Assert.AreEqual(0, Api.lua_gettop(L));

		}
//*/

/*
		[Test]
		public void TestRunScript()
		{
			Api.lua_settop(L, 0);

			var runMe = (lua.LuaTable)L.RunScript1("RunMe");

			Assert.AreEqual(0, Api.lua_gettop(L));


			var ret = runMe.Invoke("MyFunc", "Hello");
			Assert.AreEqual(0, Api.lua_gettop(L));

			Assert.AreEqual(4, ret.Length);
			Assert.AreEqual(1, ret[1]);
			Assert.AreEqual(2, ret[2]);
			Assert.AreEqual(3, ret[3]);
			Assert.AreEqual("Hello", (string)ret[4]);

			// set value in runMe, and re RunScript on RunMe, the value should lost
			runMe["TestValue"] = "Test Value";
			runMe[25] = 7788;
			Assert.AreEqual("Test Value", runMe["TestValue"]);
			Assert.AreEqual(7788, runMe[25]);


			runMe = (lua.LuaTable)L.RunScript1("RunMe");

			Assert.AreEqual(0, Api.lua_gettop(L));

			Assert.AreEqual(null, runMe["TestValue"]);
			Assert.AreEqual(null, runMe[25]);

			Assert.AreEqual(0, Api.lua_gettop(L));
		}

		[Test]
		public void TestRequire()
		{
			Api.lua_settop(L, 0);

			var runMe = (lua.LuaTable)L.Require("RunMe");
			Assert.AreEqual(0, Api.lua_gettop(L));

			var ret = runMe.Invoke("MyFunc", "Hello");

			Assert.AreEqual(0, Api.lua_gettop(L));

			Assert.AreEqual(4, ret.Length);

			Assert.AreEqual(0, Api.lua_gettop(L));

			Assert.AreEqual(1, ret[1]);
			Assert.AreEqual(2, ret[2]);
			Assert.AreEqual(3, ret[3]);

			Assert.AreEqual(0, Api.lua_gettop(L));

			Assert.AreEqual("Hello", (string)ret[4]);

			Assert.AreEqual(0, Api.lua_gettop(L));


			// set value in runMe, and re Require on RunMe, the value should be there
			runMe["TestValue"] = "Test Value";
			runMe[25] = 7788;
			Assert.AreEqual("Test Value", runMe["TestValue"]);

			runMe = (lua.LuaTable)L.Require("RunMe");
			Assert.AreEqual("Test Value", runMe["TestValue"]);
			Assert.AreEqual(7788, runMe[25]);

			Assert.AreEqual(0, Api.lua_gettop(L));
		}
//*/

		[Test]
		public void TestPushBytesAsLuaString()
		{
			Api.lua_settop(L, 0);

			var stackTop = Api.lua_gettop(L);

			var bytes = new byte[30];
			for (int i = 0; i < bytes.Length; ++i)
			{
				bytes[i] = (byte)Random.Range(0, 255);
			}
			bytes[0] = 0;

			Api.luaL_dostring(L, 
				"return function(s)\n" +
				"  return s\n" +
				"end");
			Assert.True(Api.lua_isfunction(L, -1));
			L.PushValue(bytes);
			Assert.True(Api.lua_isstring(L, -1));
			Lua.Call(L, 1, 1);
			var outBytes = Api.lua_tobytes(L, -1);
			Assert.AreEqual(30, outBytes.Length);
			for (int i = 0; i < bytes.Length; ++i)
			{
				Assert.AreEqual(bytes[i], outBytes[i]);
			}
			Api.lua_settop(L, stackTop);
		}


	}
}