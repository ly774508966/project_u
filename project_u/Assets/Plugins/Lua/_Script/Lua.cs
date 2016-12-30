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
﻿using System;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;

// NEVER throw Lua exception in a C# native function
// NEVER throw C# exception in lua_CFunction
// catch all C# exception in any lua_CFunction and ThrowLuaException instead
// CHECK above when you write down any thing.
// if Panic (LuaFatalException) thrown, there is no way to resume execution after it (stack frame changed)

namespace lua
{
	public class LuaException : Exception
	{
		public LuaException(string errorMessage)
			: base(errorMessage)
		{
		}
		public LuaException(string errorMessage, int code)
			: base("["+code+"]"+errorMessage)
		{
		}
	}

	public class LuaDestroyedException : LuaException
	{
		public LuaDestroyedException(string message)
			: base(message + " Lua state already destroyed.")
		{
		}
	}

	public class LuaFatalException : Exception
	{
		public LuaFatalException(string errorMessage)
			: base(errorMessage)
		{
		}
	}




	public class Lua : IDisposable
	{
		IntPtr L;

		public bool valid
		{
			get
			{
				return L != IntPtr.Zero;
			}
		}

		[MonoPInvokeCallback(typeof(Api.lua_Alloc))]
		static IntPtr Alloc(IntPtr ud, IntPtr ptr, uint osize, uint nsize)
		{
			try
			{
				if (nsize == 0)
				{
					if (ptr != IntPtr.Zero)
						Marshal.FreeHGlobal(ptr);
					return IntPtr.Zero;
				}
				else
				{
					if (ptr != IntPtr.Zero)
						return Marshal.ReAllocHGlobal(ptr, new IntPtr(nsize));
					else
						return Marshal.AllocHGlobal(new IntPtr(nsize));
				}
			}
			catch (Exception e)
			{
				Debug.LogErrorFormat("Alloc nsize = {0} failed: {1}", nsize, e.Message);
				throw e;
			}
		}

		const string kLuaStub_ReplaceSearcher = 
			"return function(s)\n" + 
			"  package.searchers = { package.searchers[1], s }\n" +
			"end\n";



		public Lua()
		{
#if ALLOC_FROM_CSHARP
			L = Api.lua_newstate(Alloc, IntPtr.Zero);
#else
			L = Api.luaL_newstate();
#endif
			Api.luaL_openlibs(L);
			Api.lua_atpanic(L, Panic);

			// put an anchor at _G['__host']
			SetHost();

			Api.luaL_requiref(L, "csharp", OpenCsharpLib, 1);
			Api.lua_pop(L, 1); // pop csharp

			// override searcher (insert in the second place, and remove all rest)
			try
			{
				Api.luaL_dostring(L, kLuaStub_ReplaceSearcher);
				Debug.Assert(Api.lua_isfunction(L, -1));
				Api.lua_pushcclosure(L, Searcher, 0);
				Call(L, 1, 0);
			}
			catch (Exception e)
			{
				Debug.LogError("Replace searchers failed." + e.Message);
			}
		}

		public void Dispose()
		{
			Api.lua_close(L);
			L = IntPtr.Zero;
		}

		public static implicit operator IntPtr(Lua l)
		{
			if (l != null)
				return l.L;
			return IntPtr.Zero;
		}

		public void RunScript(string scriptName)
		{
			string scriptPath;
			LoadChunkFromFile(L, scriptName, out scriptPath);
			Call(L, 0, 0);
		}

		public object RunScript1(string scriptName)
		{
			string scriptPath;
			var top = Api.lua_gettop(L);
			LoadChunkFromFile(L, scriptName, out scriptPath);
			Call(L, 0, 1);
			var ret = ValueAt(-1);
			Api.lua_settop(L, top);	// should left nothing on stack
			return ret;
		}

		public object Require(string scriptName)
		{
			Api.luaL_requiref(L, scriptName, LoadScript1, 0);
			var ret = ValueAt(-1);
			Api.lua_pop(L, 1);
			return ret;
		}

		const string kHost = "__host";

		void SetHost()
		{
			PushObject(this);
			Api.lua_setglobal(L, kHost);
		}

		static Lua CheckHost(IntPtr L)
		{
			Lua host = null;
			var top = Api.lua_gettop(L);
			if (Api.lua_getglobal(L, kHost) == Api.LUA_TUSERDATA)
			{
				host = ObjectAtInternal(L, -1) as Lua;
			}
			Api.lua_settop(L, top);
			if (host == null || host.L != L)
			{
				ThrowLuaException(L, "__host not found or mismatch.");
			}
			return host;
		}

		// 

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int Searcher(IntPtr L)
		{
			var host = CheckHost(L);

			var scriptName = Api.luaL_checkstring(L, 1);
			string scriptPath = string.Empty;
			try
			{
				LoadChunkFromFile(L, scriptName, out scriptPath);
				host.PushValue(scriptPath);
			}
			catch (Exception e)
			{
				ThrowLuaException(L, e);
			}
			return 2;
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int OpenCsharpLib(IntPtr L)
		{
			var regs = new Api.luaL_Reg[]
			{
				new Api.luaL_Reg("import", Import),
			};
			Api.luaL_newlib(L, regs);
			return 1;
		}

		public static LuaScriptLoaderAttribute.ScriptLoader scriptLoader;
		static LuaScriptLoaderAttribute.ScriptLoader loadScriptFromFile
		{
			get
			{
				if (scriptLoader == null)
					return DefaultScriptLoader;
				return scriptLoader;
			}
		}


		static string GetScriptPath(string scriptName)
		{
			if (string.IsNullOrEmpty(scriptName)) return scriptName;
			var path = "";
			path = System.IO.Path.Combine(Application.streamingAssetsPath, "LuaRoot");
			path = System.IO.Path.Combine(path, scriptName);
			path = path + ".lua";
			if (System.IO.Path.DirectorySeparatorChar != '/')
			{
				path = path.Replace(System.IO.Path.DirectorySeparatorChar, '/');
			}
			return path;
		}

		static byte[] DefaultScriptLoader(string scriptName, out string scriptPath)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				var loader = LuaScriptLoaderAttribute.GetLoader();
				if (loader != null)
				{
					return loader(scriptName, out scriptPath);
				}
			}
#endif

			var path = GetScriptPath(scriptName);
#if !UNITY_EDITOR && UNITY_ANDROID
			var www = new WWW(path);
#else
			var www = new WWW("file:///" + path);
#endif
			while (!www.isDone);

			if (!string.IsNullOrEmpty(www.error))
			{
				throw new Exception(www.error);
			}

			scriptPath = path;

			return www.bytes;
		}

		static void LoadChunkFromFile(IntPtr L, string scriptName, out string scriptPath)
		{
			var bytes = loadScriptFromFile(scriptName, out scriptPath);
			if (bytes == null)
			{
				throw new Exception("0 bytes loaded");
			}
			LoadChunk(L, bytes, scriptName);
		}

		static void LoadScriptInternal(IntPtr L, string scriptName, int nret, out string scriptPath)
		{
			LoadChunkFromFile(L, scriptName, out scriptPath);
			Call(L, 0, nret);
		}

		// Run script and adjust the numb of return	value to 1
		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int LoadScript(IntPtr L)
		{
			var scriptName = Api.luaL_checkstring(L, 1);
			var top = Api.lua_gettop(L);
			try
			{
				string scriptPath;
				LoadScriptInternal(L, scriptName, Api.LUA_MULTRET, out scriptPath);
			}
			catch (Exception e)
			{
				ThrowLuaException(
					L, 
					string.Format("LoadScript \"{0}\" failed: {1}", scriptName, e.Message));
			}
			return Api.lua_gettop(L) - top;
		}

		// Run script and adjust the numb of return	value to 1
		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int LoadScript1(IntPtr L)
		{
			var scriptName = Api.luaL_checkstring(L, 1);
			try
			{
				string scriptPath;
				LoadScriptInternal(L, scriptName, 1, out scriptPath);
			}
			catch (Exception e)
			{
				ThrowLuaException(
					L, 
					string.Format("LoadScript \"{0}\" failed: {1}", scriptName, e.Message));
			}
			return 1;
		}



#if UNITY_EDITOR
		// LoadScript, return result, scriptPath , have to public for Editor script
		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		public static int LoadScript1InEditor(IntPtr L)
		{
			var host = CheckHost(L);

			var scriptName = Api.luaL_checkstring(L, 1);
			try
			{
				string scriptPath;
				LoadScriptInternal(L, scriptName, 1, out scriptPath);
				host.PushValue(scriptPath);
			}
			catch (Exception e)
			{
				ThrowLuaException(
					L, 
					string.Format("LoadScript2 \"{0}\" failed: {1}", scriptName, e.Message));
			}
			return 2;
		}
#endif

		[MonoPInvokeCallback(typeof(Api.lua_Reader))]
		unsafe static IntPtr ChunkLoader(IntPtr L, IntPtr data, out IntPtr size)
		{
			var handleToBinaryChunk = GCHandle.FromIntPtr(data);
			var chunk = handleToBinaryChunk.Target as Chunk;
			var bytes = chunk.bytes.Target as byte[];
			if (chunk.pos < bytes.Length)
			{
				var curPos = chunk.pos;
				size = new IntPtr(bytes.Length); // read all at once
				chunk.pos = bytes.Length;
				return Marshal.UnsafeAddrOfPinnedArrayElement(bytes, curPos);
			}
			size = IntPtr.Zero;
			return IntPtr.Zero;
		}

		class Chunk
		{
			public GCHandle bytes;
			public int pos;
		}

		public static void LoadChunk(IntPtr L, byte[] bytes, string chunkname, string mode = "bt")
		{
			Debug.Assert(bytes != null);

			var c = new Chunk();
			c.bytes = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			c.pos = 0;
			var handleToChunkBytes = GCHandle.Alloc(c);
			var ret = Api.lua_load(L, ChunkLoader, GCHandle.ToIntPtr(handleToChunkBytes), chunkname, mode);
			if (ret != Api.LUA_OK)
			{
				c.bytes.Free();
				handleToChunkBytes.Free();
				var errMsg = Api.lua_tostring(L, -1);
				Api.lua_pop(L, 1);
				throw new LuaException(errMsg, ret);
			}
			c.bytes.Free();
			handleToChunkBytes.Free();
		}

		[MonoPInvokeCallback(typeof(Api.lua_Writer))]
		static int ChunkWriter(IntPtr L, IntPtr p, IntPtr sz, IntPtr ud)
		{
			var handleToOutput = GCHandle.FromIntPtr(ud);
			var output = handleToOutput.Target as System.IO.MemoryStream;
			var toWrite = new byte[(int)sz];
			unsafe
			{
				Marshal.Copy(p, toWrite, 0, toWrite.Length);
			}
			output.Write(toWrite, 0, toWrite.Length);
			return 0;
		}

		public static byte[] DumpChunk(IntPtr L, bool strip = true)
		{
			if (Application.isEditor)
			{
				Debug.LogWarning("Caution! the dumpped chunk is not portable.");
			}

			if (!Api.lua_isfunction(L, -1))
				return null;

			var output = new System.IO.MemoryStream();
			var outputHandle = GCHandle.Alloc(output);
			Api.lua_dump(L, ChunkWriter, GCHandle.ToIntPtr(outputHandle), strip ? 1:0);
			outputHandle.Free();

			output.Flush();
			output.Seek(0, System.IO.SeekOrigin.Begin);

			var bytes = new byte[output.Length];
			output.Read(bytes, 0, bytes.Length);
			return bytes;

		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int Panic(IntPtr L)
		{
			Api.luaL_traceback(L, L, Api.lua_tostring(L, -1), 1);
			throw new LuaFatalException(Api.lua_tostring(L, -1));
		}

		// [-0,	+1,	m]
		int PushObject(object obj, string metaTableName = "object_meta")
		{
			var handleToObj = GCHandle.Alloc(obj, GCHandleType.Pinned);
			var ptrToObjHandle = GCHandle.ToIntPtr(handleToObj);
			var userdata = Api.lua_newuserdata(L, new IntPtr(IntPtr.Size));
			// stack: userdata
			Marshal.WriteIntPtr(userdata, ptrToObjHandle);

			var newMeta = NewObjectMetatable(metaTableName);
			// stack: userdata, meta
			Api.lua_setmetatable(L, -2);
			// stack: userdata
			return newMeta;
		}

		public object ObjectAt(int idx)
		{
			return ObjectAtInternal(L, idx);
		}

		static object ObjectAtInternal(IntPtr L, int idx)
		{
			var userdata = Api.lua_touserdata(L, idx);
			if (userdata == IntPtr.Zero)
			{
				Debug.LogError("userdata is null");
				return null;
			}
			var ptrToObjHandle = Marshal.ReadIntPtr(userdata);
			var handleToObj = GCHandle.FromIntPtr(ptrToObjHandle);
			if (handleToObj.Target == null)
			{
				Debug.LogError("handleToObj is null");
			}
			return handleToObj.Target;
		}


		public int MakeRefTo(object obj)
		{
			Debug.Assert(obj != null);
			var type = obj.GetType();
			Debug.Assert(type.IsClass);
			PushObject(obj);
			var refVal = MakeRefAt(-1);
			Api.lua_pop(L, 1);
			return refVal;
		}


		public int MakeRefAt(int index)
		{
			Api.lua_pushvalue(L, index);
			var refVal = Api.luaL_ref(L, Api.LUA_REGISTRYINDEX);
			return refVal;
		}

		public void PushRef(int objReference)
		{
			Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, objReference);
		}

		public void Unref(int objReference)
		{
			Api.luaL_unref(L, Api.LUA_REGISTRYINDEX, objReference);
		}

		public object ValueAt(int idx)
		{
			var type = Api.lua_type(L, idx);
			switch (type)
			{
				case Api.LUA_TNONE:
				case Api.LUA_TNIL:
					return null;
				case Api.LUA_TBOOLEAN:
					return Api.lua_toboolean(L, idx);
				case Api.LUA_TLIGHTUSERDATA:
					return Api.lua_touserdata(L, idx);
				case Api.LUA_TNUMBER:
					if (Api.lua_isinteger(L, idx))
					{
						return Api.lua_tointeger(L, idx);
					}
					else
					{
						return Api.lua_tonumber(L, idx);
					}
				case Api.LUA_TSTRING:
					return Api.lua_tostring(L, idx);
				case Api.LUA_TTABLE:
					return LuaTable.ReferenceTo(this, idx);
				case Api.LUA_TUSERDATA:
					return ObjectAt(idx);
				default:
					Debug.LogError("Not supported");
					return null;
			}
		}

		static bool IsNumericType(System.Type type)
		{
			if (type.IsPrimitive)
			{
				return (type == typeof(System.Byte)
					|| type	== typeof(System.SByte)
					|| type	== typeof(System.Char)
					|| type == typeof(System.Int16)
					|| type	== typeof(System.UInt16)
					|| type	== typeof(System.Int32)
					|| type	== typeof(System.UInt32)
					|| type	== typeof(System.Int64)
					|| type	== typeof(System.UInt64)
					|| type	== typeof(System.Single)
					|| type	== typeof(System.Double)
					|| type	== typeof(System.Decimal));
			}
			return false;
		}


		static bool IsConvertable(int luaType, System.Reflection.ParameterInfo arg)
		{
			if (arg.ParameterType == typeof(System.Object))
			{
				// everything can be converted to object
				return true;
			}
			if (luaType == Api.LUA_TUSERDATA)
			{
				// test	at convertion part
				return true;
			}

			var type = arg.ParameterType;
			if (type.IsByRef)
			{
				type = type.GetElementType(); // strip byref
			}
			switch (luaType)
			{
				case Api.LUA_TNUMBER:
					return IsNumericType(type);
				case Api.LUA_TSTRING:
					return (type == typeof(string));
				case Api.LUA_TBOOLEAN:
					return (type == typeof(System.Boolean));
				case Api.LUA_TLIGHTUSERDATA:
					return (type == typeof(System.IntPtr) || type == typeof(System.UIntPtr));
				case Api.LUA_TFUNCTION:
					return typeof(lua.FuncTools.FuncBase).IsAssignableFrom(type);
				case Api.LUA_TNIL:
					if (arg.IsOut) // if is out, we can pass nil in
					{
						return true;
					}
					return (type == typeof(object));
				case Api.LUA_TNONE:
					return (type == typeof(object));
			}
			Debug.LogWarningFormat("Argument type {0} is not supported", Api.ttypename(luaType));
			return false;
		}

		static bool MatchArgs(System.Reflection.ParameterInfo[] args, int[] luaArgTypes)
		{
			if (args.Length == luaArgTypes.Length)
			{
				if (args.Length == 0) return true;
				for (var i = 0; i < args.Length; ++i)
				{
					var arg = args[i];
					var luaArgType = luaArgTypes[i];
					if (!IsConvertable(luaArgType, arg))
					{
						return false;
					}
				}
				return true;
			}
			return false;
		}

		static readonly object[] csharpArgs_NoArgs = null;

		static object GetDefaultValue(Type type)
		{
			if (type.IsValueType)
			{
				return Activator.CreateInstance(type);
			}
			return null;
		}

		object[] ArgsFrom(System.Reflection.ParameterInfo[] args, int argStart, int numArgs)
		{
			if (args == null || args.Length == 0)
			{
				return csharpArgs_NoArgs;
			}
			Api.Assert(L, args.Length == numArgs - argStart + 1, "Different count of arguments.");
			var actualArgs = new object[numArgs - argStart + 1];
			for (int i = argStart; i <= numArgs; ++i)
			{
				var idx = i - argStart;
				var arg = args[idx];
				var type = arg.ParameterType;
				var luaType = Api.lua_type(L, i);

				switch (luaType)
				{
					case Api.LUA_TNUMBER:
						var nvalue = Api.lua_tonumber(L, i);
						try
						{
							if (type.IsByRef)
							{
								type = type.GetElementType();
							}
							var converted = System.Convert.ChangeType(nvalue, type);
							actualArgs[idx] = converted;
						}
						catch (Exception e)
						{
							ThrowLuaException(L, e);
						}
						break;
					case Api.LUA_TUSERDATA:
						actualArgs[idx] = ObjectAt(i);
						break;
					case Api.LUA_TFUNCTION:
						var methodObj = FuncTools.CreateFuncObject(type, this, i);
						actualArgs[idx] = methodObj;
						break;	
					case Api.LUA_TNIL:
						if (arg.IsOut)
						{
							actualArgs[idx] = GetDefaultValue(type);
						}
						break;
					default:
						if (type != typeof(string) && type != typeof(System.Object))
						{
							Debug.LogWarningFormat("Convert lua type {0} to string, wanted to fit {1}", Api.ttypename(luaType), type.ToString());
						}
						actualArgs[idx] = Api.lua_tostring(L, i);
						break;
				}

			}
			return actualArgs;
		}

		static readonly int[] luaArgTypes_NoArgs = new int[0];

		static string GetLuaInvokingSigniture(string methodName, int[] args)
		{
			var sb = new System.Text.StringBuilder();
			sb.Append(methodName);
			sb.Append("(");
			if (args != null && args.Length > 0)
			{
				for (var i = 0; i < args.Length; ++i)
				{
					sb.Append(Api.ttypename(args[i]));
					if (i < args.Length - 1)
					{
						sb.Append(",");
					}
				}
			}
			sb.Append(")");
			return sb.ToString();
		}

		static Dictionary<Type, Dictionary<string, System.Reflection.MethodBase>> methodCache = new Dictionary<Type, Dictionary<string, System.Reflection.MethodBase>>();
		static bool useMethodCache = true;

		public static bool UseMethodCache(bool useMethodCache_ = true)
		{
			var wasUsingMethodCache = useMethodCache;
			useMethodCache = useMethodCache_;
			return wasUsingMethodCache;
		}
		public static void CleanMethodCache()
		{
			lock (methodCache)
			{
				methodCache.Clear();
			}
		}

		static System.Reflection.MethodBase GetMethodFromCache(Type targetType, string mangledName)
		{
			if (!useMethodCache) return null;

			System.Reflection.MethodBase method = null;
			lock (methodCache)
			{
				Dictionary<string, System.Reflection.MethodBase> cachedMethods;
				if (methodCache.TryGetValue(targetType, out cachedMethods))
				{
					cachedMethods.TryGetValue(mangledName, out method);
				}
			}
			return method;
		}

		static string Mangle(string methodName, int[] luaArgTypes, bool invokingStaticMethod)
		{
			var sb = new System.Text.StringBuilder();
			if (invokingStaticMethod)
			{
				sb.Append("_s_");
			}
			else
			{
				sb.Append("_");
			}
			sb.Append(methodName);
			sb.Append(luaArgTypes.Length);
			for (int i = 0; i < luaArgTypes.Length; ++i)
			{
				sb.Append(luaArgTypes[i]);
			}
			return sb.ToString();
		}



		static void CacheMethod(IntPtr L, Type targetType, string mangledName, System.Reflection.MethodBase method)
		{
			if (!useMethodCache) return;

			lock (methodCache)
			{
				Dictionary<string, System.Reflection.MethodBase> cachedMethods;
				if (!methodCache.TryGetValue(targetType, out cachedMethods))
				{
					cachedMethods = new Dictionary<string, System.Reflection.MethodBase>();
					methodCache.Add(targetType, cachedMethods);
				}
				Api.Assert(L, !cachedMethods.ContainsKey(mangledName), string.Format("{0} of {1} already cached with mangled name {2}", method.ToString(), targetType.ToString(), mangledName));
				cachedMethods.Add(mangledName, method);
			}
		}





		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int InvokeMethod(IntPtr L)
		{
			// upvalue 1 --> isInvokingFromClass
			// upvalue 2 --> userdata (host of metatable).
			// upvalue 3 --> member name

			Lua host = CheckHost(L);
			var isInvokingFromClass = Api.lua_toboolean(L, Api.lua_upvalueindex(1));
			var obj = host.ObjectAt(Api.lua_upvalueindex(2));
			Api.Assert(L, obj != null, "Invoking target not found at upvalueindex(2)");
			var methodName = Api.luaL_checkstring(L, Api.lua_upvalueindex(3));

			int[] luaArgTypes = luaArgTypes_NoArgs;
			var argStart = 1;
			var numArgs = Api.lua_gettop(L);
			var invokingStaticMethod = true;
			if (numArgs > 0)
			{
				if (Api.lua_rawequal(L, 1, Api.lua_upvalueindex(2)))
				{
					invokingStaticMethod = false;
					if (numArgs - 1 == 0)
					{
						luaArgTypes = luaArgTypes_NoArgs;
					}
					else
					{
						luaArgTypes = new int[numArgs - 1];
						argStart = 2;
					}
				}
				else
				{
					luaArgTypes = new int[numArgs];
					argStart = 1;
				}
			}

			if (luaArgTypes != luaArgTypes_NoArgs)
			{
				// fill	arg	types
				for (var i = argStart; i <= numArgs; ++i)
				{
					luaArgTypes[i - argStart] = Api.lua_type(L, i);
				}
			}



			object target = null;
			System.Type type = null;
			if (isInvokingFromClass)
			{
				type = (System.Type)obj;
				Api.Assert(L, invokingStaticMethod, string.Format("Invoking static method {0} from class {1} with incorrect syntax", methodName, type.ToString()));
			}
			else
			{
				target = obj;
				type = obj.GetType();
			}

			var mangledName = Mangle(methodName, luaArgTypes, invokingStaticMethod);
			var method = GetMethodFromCache(type, mangledName);
			System.Reflection.ParameterInfo[] parameters = null;

			if (method == null)
			{
				var members = type.GetMember(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance);
				foreach (var member in members)
				{
					Api.Assert(L, member.MemberType == System.Reflection.MemberTypes.Method, string.Format("{0} is not a Method.", methodName));
					var m = (System.Reflection.MethodInfo)member;
					if (m.IsStatic)
					{
						Api.Assert(L, invokingStaticMethod, string.Format("Invoking static method {0} with incorrect syntax.", m.ToString()));
						target = null;
					}
					else
					{
						Api.Assert(L, !invokingStaticMethod, string.Format("Invoking non-static method {0} with incorrect syntax.", m.ToString()));
					}
					parameters = m.GetParameters();
					if (MatchArgs(parameters, luaArgTypes))
					{
						CacheMethod(L, type, mangledName, m);
						method = m;
						break;  // found one, break
					}
				}
				Api.Assert(L, method != null, string.Format("No corresponding csharp method for {0}", GetLuaInvokingSigniture(methodName, luaArgTypes)));
			}
			else
			{
				parameters = method.GetParameters();
			}
			try
			{
				var top = Api.lua_gettop(L);
				var actualArgs = host.ArgsFrom(parameters, argStart, numArgs);
				Api.Assert(L, top == Api.lua_gettop(L), "stack changed after converted args from lua.");

				var retVal = method.Invoke(target, actualArgs);
				int outValues = 0;
				if (retVal != null)
				{
					host.PushValue(retVal);
					++outValues;
				}
				// out and ref parameters
				for (int i = 0; i < parameters.Length; ++i)
				{
					if (parameters[i].IsOut || parameters[i].ParameterType.IsByRef)
					{
						host.PushValue(actualArgs[i]);
						++outValues;
					}
				}
				return outValues;
			}
			catch (Exception e)
			{
				ThrowLuaException(L, e);
			}
			return 0;
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int MetaConstructFunction(IntPtr L)
		{
			Lua host = CheckHost(L);

			var typeObj = host.ObjectAt(1);
			Api.Assert(L, (typeObj != null) && (typeObj is System.Type), "Constructor needs type object.");

			var numArgs = Api.lua_gettop(L);
			int[] luaArgTypes = luaArgTypes_NoArgs;
			if (numArgs > 1) // the first arg is class itself
			{
				luaArgTypes = new int[numArgs - 1];
				for (var i = 2; i <= numArgs; ++i)
				{
					luaArgTypes[i-2] = Api.lua_type(L, i);
				}
			}

			var type = (System.Type)typeObj;
			var mangledName = Mangle("__ctor", luaArgTypes, invokingStaticMethod: true);
			var method = GetMethodFromCache(type, mangledName);
			System.Reflection.ParameterInfo[] parameters = null;
			if (method == null)
			{
				var constructors = type.GetConstructors();
				for (var i = 0; i < constructors.Length; ++i)
				{
					method = constructors[i];
					parameters = method.GetParameters();
					if (MatchArgs(parameters, luaArgTypes))
					{
						CacheMethod(L, type, mangledName, method);
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
				try
				{
					host.PushObject(ctor.Invoke(host.ArgsFrom(parameters, 2, numArgs)));
				}
				catch (Exception e)
				{
					ThrowLuaException(L, e);
				}
			}
			else
			{
				Api.Assert(L, false, string.Format("No proper constructor available, calling {0}", GetLuaInvokingSigniture("ctor", luaArgTypes)));
				Api.lua_pushnil(L);
			}
			return 1;
		}

		public static void ImportGlobal(IntPtr L, Type type, string name)
		{
			Import(L, type);
			Api.lua_setglobal(L, name);
		}

		// [ 0 | +1 | -]
		public static bool Import(IntPtr L, Type type)
		{
			Api.lua_pushcclosure(L, Import, 0);
			Api.lua_pushstring(L, type.AssemblyQualifiedName);
			if (Api.LUA_OK != Api.lua_pcall(L, 1, 1, 0))
			{
				Debug.LogError(Api.lua_tostring(L, -1));
				Api.lua_pop(L, 1);
				Api.lua_pushnil(L);
				return false;
			}
			return true;
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int Import(IntPtr L)
		{
			var host = CheckHost(L);

			var typename = Api.luaL_checkstring(L, 1);
			var type = Type.GetType(typename);
			if (type == null)
			{
				Api.lua_pushnil(L);
				return 1;
			}

			if (host.PushObject(type, "class_meta") == 1) // TODO: opt, for type loaded, cache it
			{
				Api.lua_getmetatable(L, -1);

				Api.lua_pushboolean(L, true);
				Api.lua_rawseti(L, -2, 1); // isClassObject = true

				Api.lua_pushcclosure(L, MetaConstructFunction, 0);
				Api.lua_setfield(L, -2, "__call");

				Api.lua_pop(L, 1);
			}
			return 1;
		}

	
		static void ThrowLuaException(IntPtr L, string err)
		{
			Api.Assert(L, false, err);
		}

		static void ThrowLuaException(IntPtr L, Exception e)
		{
			ThrowLuaException(L, e.Message);
		}

		public void PushValue(object value)
		{
			if (value == null)
			{
				Api.lua_pushnil(L);
				return;
			}

			var type = value.GetType();
			if (type.IsArray)
			{
				if (type == typeof(byte[]))
				{
					Api.lua_pushbytes(L, (byte[])value);
					return;
				}
				// TODO: other primitive array
			}
			// other arrays currently go below, push as an object

			if (type.IsPrimitive)
			{
				if (IsNumericType(type))
				{
					var number = System.Convert.ToDouble(value);
					Api.lua_pushnumber(L, number);
				}
				else if (type == typeof(System.Boolean))
				{
					Api.lua_pushboolean(L, (bool)value);
				}
				else if (type == typeof(System.IntPtr)
					|| type == typeof(System.UIntPtr))
				{
					Api.lua_pushlightuserdata(L, (IntPtr)value);
				}
			}
			else if (type == typeof(string))
			{
				Api.lua_pushstring(L, (string)value);
			}
			else
			{
				PushObject(value);
			}
		}

		int GetMember(object obj, Type objType, string memberName)
		{
			var members = objType.GetMember(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance);
			if (members.Length > 0)
			{
				var member = members[0];
				if (member.MemberType == System.Reflection.MemberTypes.Field)
				{
					var field = (System.Reflection.FieldInfo)member;
					PushValue(field.GetValue(obj));
					return 1;
				}
				else if (member.MemberType == System.Reflection.MemberTypes.Property)
				{
					var prop = (System.Reflection.PropertyInfo)member;
					try
					{
						PushValue(prop.GetValue(obj, null));
						return 1;
					}
					catch (ArgumentException ae)
					{
						// search into base	class of obj
						if (objType == typeof(object))
						{
							ThrowLuaException(L, string.Format("Member {0} not found. {1}", memberName, ae.Message));
						}
						return GetMember(obj, objType.BaseType, memberName);
					}
					catch (Exception e)
					{
						ThrowLuaException(L, e);
					}
				}
				else if (member.MemberType == System.Reflection.MemberTypes.Method)
				{
					bool isInvokingFromClass = (obj == null);
					Api.lua_pushboolean(L, isInvokingFromClass);      // upvalue 1 --> isInvokingFromClass
					Api.lua_pushvalue(L, 1);                          // upvalue 2 --> userdata, first parameter of __index
					Api.lua_pushvalue(L, 2);                          // upvalue 3 --> member name
					Api.lua_pushcclosure(L, InvokeMethod, 3);         // return a wrapped lua_CFunction
					return 1;
				}

				Api.lua_pushnil(L);
				return 1;
			}
			else
			{
				// search into base	class of obj
				if (objType == typeof(object))
				{
					ThrowLuaException(L, string.Format("Member {0} not found.", memberName));
				}
				return GetMember(obj, objType.BaseType, memberName);
			}
		}

		class NoIndexerException : Exception
		{
			public NoIndexerException(Type type, object[] index)
				: base("Object hasn't proper indexer.")
			{
			}
		}

		int IndexObject(object obj, Type type, object[] index)
		{
			Api.Assert(L, index != null);
			var prop = type.GetProperty("Item");
			if (prop == null)
			{
				if (type == typeof(object))
					ThrowLuaException(L, new NoIndexerException(obj.GetType(), index));
				return IndexObject(obj, type.BaseType, index);
			}
			try
			{
				var value = prop.GetValue(obj, index);
				PushValue(value);
				return 1;
			}
			catch (ArgumentException)
			{
				if (type == typeof(object))
					ThrowLuaException(L, new NoIndexerException(obj.GetType(), index));
				return IndexObject(obj, type.BaseType, index);
			}
			catch (Exception e)
			{
				ThrowLuaException(L, e);
			}
			Api.lua_pushnil(L);
			return 1;
		}


		static bool IsIndexingClassObject(IntPtr L)
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
		static int MetaIndexFunction(IntPtr L)
		{
			var host = CheckHost(L);

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

			Api.Assert(L, typeObject != null, "Should has a type.");

			if (Api.lua_isinteger(L, 2))
			{
				if (typeObject != null && typeObject.IsArray)
				{
					var array = (System.Array)thisObject;
					host.PushValue(array.GetValue(Api.lua_tointeger(L, 2)));
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
		static int MetaNewIndexFunction(IntPtr L)
		{
			var host = CheckHost(L);

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

			Api.Assert(L, typeObject != null, "Should has a type.");

			if (Api.lua_isnumber(L, 2))
			{
				if (typeObject != null && typeObject.IsArray)
				{
					var array = (System.Array)thisObject;
					var value = host.ValueAt(3);
					var converted = System.Convert.ChangeType(value, typeObject.GetElementType());
					array.SetValue(converted, Api.lua_tointeger(L, 2));
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

		void SetValueAtIndexOfObject(object obj, Type type, object[] index, object value)
		{
			Api.Assert(L, index != null);
			var prop = type.GetProperty("Item");
			if (prop == null)
			{
				if (type == typeof(object))
					ThrowLuaException(L, new NoIndexerException(obj.GetType(), index));
				SetValueAtIndexOfObject(obj, type.BaseType, index, value);
			}
			try
			{
				var converted = System.Convert.ChangeType(value, prop.PropertyType);
				prop.SetValue(obj, converted, index);
			}
			catch (ArgumentException)
			{
				if (type == typeof(object))
					ThrowLuaException(L, new NoIndexerException(obj.GetType(), index));
				SetValueAtIndexOfObject(obj, type.BaseType, index, value);
			}
			catch (Exception e)
			{
				ThrowLuaException(L, e);
			}
		}

		void SetMember(object thisObject, Type type, string memberName, object value)
		{
			Api.Assert(L, type.IsClass, string.Format("Setting property {0} of {1} object", memberName, type.ToString()));

			var members = type.GetMember(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance);
			Api.Assert(L, members.Length > 0, string.Format("Cannot find property with name {0} of type {1}", memberName, type.ToString()));

			if (members.Length > 0)
			{
				var member = members[0];
				if (member.MemberType == System.Reflection.MemberTypes.Field)
				{
					var field = (System.Reflection.FieldInfo)member;
					var converted = System.Convert.ChangeType(ValueAt(3), field.FieldType);
					field.SetValue(thisObject, converted);
				}
				else if (member.MemberType == System.Reflection.MemberTypes.Property)
				{
					var prop = (System.Reflection.PropertyInfo)member;
					var converted = System.Convert.ChangeType(ValueAt(3), prop.PropertyType);
					prop.SetValue(thisObject, converted, null);
				}
				else
				{
					Api.Assert(L, false, string.Format("Member type {0} and {1} expected, but {2} got.", 
						System.Reflection.MemberTypes.Field, System.Reflection.MemberTypes.Property, member.MemberType));
				}
			}
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int MetaToStringFunction(IntPtr L)
		{
			var host = CheckHost(L);
			var thisObject = host.ValueAt(1);
			Api.lua_pushstring(L, thisObject.ToString());
			return 1;
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int MetaGcFunction(IntPtr L)
		{
			var userdata = Api.lua_touserdata(L, 1);
			var ptrToObjHandle = Marshal.ReadIntPtr(userdata);
			var handleToObj = GCHandle.FromIntPtr(ptrToObjHandle);
			handleToObj.Free();
			return 0;
		}

		// [-0, +1, -]
		int NewObjectMetatable(string metaTableName)
		{
			if (Api.luaL_newmetatable(L, metaTableName) == 1)
			{
				Debug.LogFormat("Registering object meta table {0} ... ", metaTableName);
				Api.lua_pushboolean(L, false);
				Api.lua_rawseti(L, -2, 1); // isClassObject = false

				Api.lua_pushcclosure(L, MetaIndexFunction, 0);
				Api.lua_setfield(L, -2, "__index");

				Api.lua_pushcclosure(L, MetaNewIndexFunction, 0);
				Api.lua_setfield(L, -2, "__newindex");

				Api.lua_pushcclosure(L, MetaToStringFunction, 0);
				Api.lua_setfield(L, -2, "__tostring");

				Api.lua_pushcclosure(L, MetaGcFunction, 0);
				Api.lua_setfield(L, -2, "__gc");

				return 1;
			}
			return 0;
		}


		// Invoking Lua Function
		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int HandleLuaFunctionInvokingError(IntPtr L)
		{
			var err = Api.lua_tostring(L, -1);
			Api.lua_pop(L, 1);
			Api.luaL_traceback(L, L, err, 0);
			return 1;
		}
		public static void Call(IntPtr L, int nargs, int nresults)
		{
			var stackTop = Api.lua_gettop(L) - nargs - 1; // function and args

			Api.lua_pushcclosure(L, HandleLuaFunctionInvokingError, 0);
			Api.lua_insert(L, stackTop + 1); // put err func to stackTop

			var ret = Api.lua_pcall(L, nargs, nresults, stackTop + 1);
			if (ret != Api.LUA_OK)
			{
				var errWithTraceback = Api.lua_tostring(L, -1);
				Api.lua_settop(L, stackTop);
				throw new LuaException(errWithTraceback, ret);
			}
			else
			{
				Api.lua_remove(L, stackTop + 1); // remove err func
			}
		}


		public static string DebugStack(IntPtr L)
		{
			var top = Api.lua_gettop(L);
			var sb = new System.Text.StringBuilder();
			for (int i = top; i > 0; i--)
			{
				sb.Append(i);
				sb.Append(":\t");
				sb.Append(Api.lua_typename(L, Api.lua_type(L, i)));
				sb.Append("\t");
				sb.Append(Api.lua_tostring(L, i));
				sb.AppendLine();
			}
			return sb.ToString();
		}
	}
}
