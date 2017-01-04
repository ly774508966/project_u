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
using System.Runtime.InteropServices;
using AOT;

using lua_State = System.IntPtr;

namespace lua
{

	internal class MetaMethod
	{
		static bool IsIndexingClassObject(lua_State L)
		{
			var isIndexingClassObject = false;
			var top = Api.lua_gettop(L);
			Api.lua_getmetatable(L, 1);
			if (Api.lua_istable(L, -1))
			{
				Api.lua_rawgeti(L, -1, 1);
				isIndexingClassObject = Api.lua_toboolean(L, -1);
			}
			Api.lua_settop(L, top);
			return isIndexingClassObject;
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int MetaConstructFunction(lua_State L)
		{
			try
			{
				return MetaConstructFunctionInternal(L);
			}
			catch (Exception e)
			{
				Lua.PushErrorObject(L, e.Message);
				return 1;
			}
		}
		static int MetaConstructFunctionInternal(lua_State L)
		{
			Lua host = Lua.CheckHost(L);

			var typeObj = host.ObjectAt(1);
			host.Assert((typeObj != null && (typeObj is System.Type)), "Constructor needs type object.");

			var numArgs = Api.lua_gettop(L);
			int[] luaArgTypes = Lua.luaArgTypes_NoArgs;
			if (numArgs > 1) // the first arg is class itself
			{
				luaArgTypes = new int[numArgs - 1];
				for (var i = 2; i <= numArgs; ++i)
				{
					luaArgTypes[i-2] = Api.lua_type(L, i);
				}
			}

			var type = (System.Type)typeObj;
			var mangledName = Lua.Mangle("__ctor", luaArgTypes, invokingStaticMethod: true);
			var method = Lua.GetMethodFromCache(type, mangledName);
			System.Reflection.ParameterInfo[] parameters = null;
			if (method == null)
			{
				var constructors = type.GetConstructors();
				for (var i = 0; i < constructors.Length; ++i)
				{
					method = constructors[i];
					parameters = method.GetParameters();
					if (Lua.MatchArgs(parameters, luaArgTypes))
					{
						Lua.CacheMethod(L, type, mangledName, method);
						break;
					}
				}
			}
			else
			{
				parameters = method.GetParameters();
			}

			if (method != null)
			{
				var ctor = (System.Reflection.ConstructorInfo)method;

				IDisposable[] disposableArgs;
				var args = host.ArgsFrom(parameters, 2, numArgs, out disposableArgs);
				host.PushObject(ctor.Invoke(args));
				if (disposableArgs != null)
				{
					foreach (var d in disposableArgs)
					{
						if (d != null) d.Dispose();
					}
				}
				return 1;
			}
			host.Assert(false, string.Format("No proper constructor available, calling {0}", Lua.GetLuaInvokingSigniture("ctor", luaArgTypes)));
			return 1; // never get here , avoiding compile error
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int MetaIndexFunction(lua_State L)
		{
			try
			{
				return MetaIndexFunctionInternal(L);
			}
			catch (Exception e)
			{
				Lua.PushErrorObject(L, e.Message);
				return 1;
			}
		}

		static int MetaIndexFunctionInternal(lua_State L)
		{
			var host = Lua.CheckHost(L);

			var isIndexingClassObject = IsIndexingClassObject(L);

			System.Type typeObject = null;
			if (isIndexingClassObject)
			{
				typeObject = (System.Type)host.ObjectAt(1);
			}

			object thisObject = null;
			if (!isIndexingClassObject)
			{
				thisObject = host.ObjectAt(1);
				typeObject = thisObject.GetType();
			}

			host.Assert(typeObject != null, "Should have a type");

			if (Api.lua_isinteger(L, 2))
			{
				if (typeObject != null && typeObject.IsArray)
				{
					var array = (System.Array)thisObject;
					host.PushValue(array.GetValue((int)Api.lua_tointeger(L, 2)));
					return 1;
				}
				else
				{
					return host.IndexObject(thisObject, typeObject, new object[] { (int)Api.lua_tointeger(L, 2) });
				}
			}
			else if (Api.lua_isstring(L, 2))
			{
				return host.GetMember(thisObject, typeObject, Api.lua_tostring(L, 2));
			}
			else
			{
				return host.IndexObject(thisObject, typeObject, new object[] { host.ValueAt(2) });
			}
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int MetaNewIndexFunction(lua_State L)
		{
			try
			{
				return MetaNewIndexFunctionInternal(L);
			}
			catch (Exception e)
			{
				Lua.PushErrorObject(L, e.Message);
				return 1;
			}
		}

		static int MetaNewIndexFunctionInternal(lua_State L)
		{
			var host = Lua.CheckHost(L);

			var isIndexingClassObject = IsIndexingClassObject(L);

			System.Type typeObject = null;
			if (isIndexingClassObject)
			{
				typeObject = (System.Type)host.ObjectAt(1);
			}

			object thisObject = null;
			if (!isIndexingClassObject)
			{
				thisObject = host.ObjectAt(1);
				typeObject = thisObject.GetType();
			}

			host.Assert(typeObject != null, "Should has a type.");

			if (Api.lua_isnumber(L, 2))
			{
				if (typeObject != null && typeObject.IsArray)
				{
					var array = (System.Array)thisObject;
					var value = host.ValueAt(3);
					var index = (int)Api.lua_tointeger(L, 2);
					object convertedNumber;
					if (Lua.ConvertNumber(typeObject.GetElementType(), value, out convertedNumber))
					{
						array.SetValue(convertedNumber, index);
					}
					else
					{
						var converted = System.Convert.ChangeType(value, typeObject.GetElementType());
						array.SetValue(converted, index);
					}
				}
				else
				{
					host.SetValueAtIndexOfObject(thisObject, typeObject, new object[] { (int)Api.lua_tointeger(L, 2) }, host.ValueAt(3));
				}
			}
			else if (Api.lua_isstring(L, 2))
			{
				host.SetMember(thisObject, typeObject, Api.lua_tostring(L, 2), host.ValueAt(3));
			}
			else
			{
				host.SetValueAtIndexOfObject(thisObject, typeObject, new object[] { host.ValueAt(2) }, host.ValueAt(3));
			}
			return 0;
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int MetaToStringFunction(lua_State L)
		{
			try
			{
				return MetaToStringFunctionInternal(L);
			}
			catch (Exception e)
			{
				Lua.PushErrorObject(L, e.Message);
				return 1;
			}
		}

		static int MetaToStringFunctionInternal(lua_State L)
		{
			var host = Lua.CheckHost(L);
			var thisObject = host.ValueAt(1);
			Api.lua_pushstring(L, thisObject.ToString());
			return 1;
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int MetaGcFunction(lua_State L)
		{
			try
			{
				return MetaGcFunctionInternal(L);
			}
			catch (Exception)
			{
				return 0;
			}
		}

		static int MetaGcFunctionInternal(lua_State L)
		{
			var userdata = Api.lua_touserdata(L, 1);
			var ptrToObjHandle = Marshal.ReadIntPtr(userdata);
			var handleToObj = GCHandle.FromIntPtr(ptrToObjHandle);
			handleToObj.Free();
			return 0;
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int MetaBinaryOpFunction(lua_State L)
		{
			try
			{
				return MetaBinaryOpFunctionInternal(L);
			}
			catch (Exception e)
			{
				Lua.PushErrorObject(L, e.Message);
				return 1;
			}
		}

		static int MetaBinaryOpFunctionInternal(lua_State L)
		{
			var host = Lua.CheckHost(L);
			var opValue = Api.lua_tointeger(L, Api.lua_upvalueindex(1));
			var op = (Lua.BinaryOp)opValue;
			var objectArg = Api.luaL_testudata(L, 1, Lua.objectMetaTable); // test first one
			if (objectArg == IntPtr.Zero)
			{
				objectArg = Api.luaL_testudata(L, 2, Lua.objectMetaTable);
				host.Assert(objectArg != IntPtr.Zero, string.Format("Binary op {0} called on unexpected values.", op));
			}
			var obj = Lua.UdataToObject(objectArg);
			var type = obj.GetType();

			// upvalue 1 --> isInvokingFromClass
			// upvalue 2 --> userdata (host of metatable).
			// upvalue 3 --> member name
			Api.lua_pushboolean(L, true);
			host.PushObject(type);
			Api.lua_pushstring(L, op.ToString());
			Api.lua_pushcclosure(L, Lua.InvokeMethod, 3);
			Api.lua_pushvalue(L, 1);
			Api.lua_pushvalue(L, 2);
			host.Call(2, 1);

			return 1;
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int MetaUnaryOpFunction(lua_State L)
		{
			try
			{
				return MetaUnaryOpFunctionInternal(L);
			}
			catch (Exception e)
			{
				Lua.PushErrorObject(L, e.Message);
				return 1;
			}
		}

		static int MetaUnaryOpFunctionInternal(lua_State L)
		{
			var host = Lua.CheckHost(L);
			var op = (Lua.UnaryOp)Api.lua_tointeger(L, Api.lua_upvalueindex(1));
			var objectArg = Api.luaL_testudata(L, 1, Lua.objectMetaTable); // test first one
			host.Assert(objectArg != IntPtr.Zero, string.Format("Binary op {0} called on unexpected values.", op));
			var obj = Lua.UdataToObject(objectArg);
			var type = obj.GetType();

			// upvalue 1 --> isInvokingFromClass
			// upvalue 2 --> userdata (host of metatable).
			// upvalue 3 --> member name
			Api.lua_pushboolean(L, true);
			host.PushObject(type);
			Api.lua_pushstring(L, op.ToString());
			Api.lua_pushcclosure(L, Lua.InvokeMethod, 3);
			Api.lua_pushvalue(L, 1);
			host.Call(1, 1);
			return 1;
		}
	}


}
