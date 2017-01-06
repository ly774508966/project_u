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

// ALL MonoPInvokeCallback SHOULD BE NO THROW
// ALL MonoPInvokeCallback SHOULD BE NO THROW
// ALL MonoPInvokeCallback SHOULD BE NO THROW

// NEVER throw Lua exception in a C# native function
// NEVER throw C# exception in lua_CFunction
// catch all C# exception in any lua_CFunction and use PushErrorObject and return 1 result if error happens
// CHECK above when you write down any thing.
// if Panic (LuaFatalException) thrown, there is no way to resume execution after it (stack frame changed)


namespace lua
{
	public class Config
	{
		// if array.Length > value, then array is passed as C# object.
		// except byte[], bytes always copied to Lua state.
		public static int PassAsObjectIfArrayLengthGreatThan = 30;

	}

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

	public class LuaFatalException : Exception
	{
		public LuaFatalException(string errorMessage)
			: base(errorMessage)
		{
			Debug.LogError("LUA FATAL: " + errorMessage);
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
		static IntPtr Alloc(IntPtr ud, IntPtr ptr, UIntPtr osize, UIntPtr nsize)
		{
			try
			{
				if (nsize == UIntPtr.Zero)
				{
					if (ptr != IntPtr.Zero)
						Marshal.FreeHGlobal(ptr);
					return IntPtr.Zero;
				}
				else
				{
					if (ptr != IntPtr.Zero)
						return Marshal.ReAllocHGlobal(ptr, new IntPtr((long)nsize.ToUInt64()));
					else
						return Marshal.AllocHGlobal(new IntPtr((long)nsize.ToUInt64()));
				}
			}
			catch (Exception e)
			{
				Debug.LogErrorFormat("Alloc nsize = {0} failed: {1}", nsize, e.Message);
				return IntPtr.Zero;
			}
		}

		LuaTable csharp;

		const string kLuaStub_SetupSearcher = 
			"return function(s)\n" + 
#if UNITY_EDITOR
			"  table.insert(package.searchers, 2, s)\n" + // after preload
#else
			"  package.searchers = { package.searchers[1], s }\n" + // keep only preload
#endif
			"end";

		// http://lua-users.org/wiki/HexDump
		const string kLuaStub_HexDump =
			"function (buf)\n" +
			"  local s = { '' }\n" + 
			"  for byte=1, #buf, 16 do\n" +
			"    local chunk = buf:sub(byte, byte+15)\n" +
			"    s[1] = s[1] .. string.format('%08X\t',byte-1)\n" +
			"    chunk:gsub('.', function (c) s[1] = s[1] .. string.format('%02X ', string.byte(c)) end)\n" +
			"    s[1] = s[1] .. string.rep('\t',3*(16-#chunk))\n" +
			"    s[1] = s[1] .. chunk:gsub('[^%a%d]','.') .. '\\n'\n"+
			"  end\n" +
			"  return s[1]\n" +
			"end";

		const string kLuaStub_ErrorObject =
			"local error_meta = { __tostring = function(e) return e.message end }\n" +
			"local table_pack, setmetatable, getmetatable, assert = table.pack, setmetatable, getmetatable, assert\n" +
			"return function(message) -- push error object\n" + 
			"  local errObj = { message = message }\n" +
			"  setmetatable(errObj, error_meta)\n" +
			"  return errObj\n" +
			"end,\n" +
			"function(...) -- check error object, assert if got error, returns all value got if no error\n" +
			"  local r = table_pack(...)\n" + 
			"  if #r > 0 then\n" +
			"    local e = r[1] \n" +
			"    local m = getmetatable(e)\n" +
			"    if m == error_meta then assert(false, 'invoking native function failed: ' .. e.message) end\n" +
			"  end\n" +
			"  return ... -- nothing to check\n" +
			"end";

		LuaFunction pushError;
		LuaFunction checkError;

		const string kLuaStub_Bytes =
			"local hex_dump = csharp.hex_dump\n" +
			"local bytes_meta = { __tostring = function(t) return hex_dump(t[1]) end}\n" +
			"local assert, type = assert, type\n" +
			"return function(s) -- as_bytes, convert string to bytes object\n" +
			"  assert(type(s) == 'string', 'expected string, but '.. type(s) .. ' got')\n" +
			"  return setmetatable({s}, bytes_meta)\n" +
			"end,\n" +
			"function(b) -- return string if is a bytes object, or nil\n" +
			"  return getmetatable(b) == bytes_meta and b[1] or nil\n" +
			"end";
		LuaFunction testBytes;



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
			csharp = LuaTable.MakeRefTo(this, -1);
			Api.lua_pop(L, 1); // pop csharp

			// Helpers
			try
			{
				// csharp.test_error && csharp.push_error
				DoString(kLuaStub_ErrorObject, 2);
				// -1 check, -2 push
				checkError = LuaFunction.MakeRefTo(this, -1);
				pushError = LuaFunction.MakeRefTo(this, -2);
				// also set to csharp table
				csharp["check_error"] = checkError;
				Api.lua_pop(L, 2); // pop

				// ------ BEFORE THIS LINE, ERROR OBJECET is not prepared, so all errors pushed as string

				// hex dump
				var f = LuaFunction.NewFunction(this, kLuaStub_HexDump);
				csharp["hex_dump"] = f;
				f.Dispose();

				// addPath (C#)
				addPath = LuaFunction.NewFunction(this, kLuaStub_AddPath);



				// csharp.to_bytes (lua) and test_bytes (C#)
				DoString(kLuaStub_Bytes, 2);
				// -1 test_bytes (C#), -2 as_bytes (Lua)
				testBytes = LuaFunction.MakeRefTo(this, -1);
				var asBytes = LuaFunction.MakeRefTo(this, -2);
				csharp["as_bytes"] = asBytes;
				asBytes.Dispose();

				Api.lua_pop(L, 2); // pop those
			}
			catch (Exception e)
			{
				Debug.LogError(e.Message);
				throw e;
			}


			// override searcher (insert in the second place, and also set path and cpath)
			// Searcher provided here will load all script/modules except modules/cmodules running only
			// in editor. Lua will handle those modules itself.
			try
			{
				DoString(kLuaStub_SetupSearcher, 1);
				Api.lua_pushcclosure(L, Searcher, 0);
				Call(1, 0);
			}
			catch (Exception e)
			{
				Debug.LogError("Replace searchers failed." + e.Message);
			}


			// set default path
#if UNITY_EDITOR
			// path
			var path = Application.dataPath;
			path = System.IO.Path.Combine(path, "Plugins");
			path = System.IO.Path.Combine(path, "Lua");
			path = System.IO.Path.Combine(path, "Modules");
			path = path.Replace('\\', '/');
			var luaPath = path + "/?.lua;" + path +"/?/init.lua";
			AddPath(luaPath);

			// cpath
			path = Application.dataPath;
			path = System.IO.Path.Combine(path, "Plugins");
			path = System.IO.Path.Combine(path, "Lua");
			path = System.IO.Path.Combine(path, "Windows");
			if (IntPtr.Size > 4)
				path = System.IO.Path.Combine(path, "x86_64");
			else
				path = System.IO.Path.Combine(path, "x86");
			path = path.Replace('\\', '/');
			var luaCPath = path + "/?.dll;"+path + "/?/init.dll";
			AddCPath(luaCPath);
#endif
		}

		public void Dispose()
		{
			checkError.Dispose();
			checkError = null;

			pushError.Dispose();
			pushError = null;

			testBytes.Dispose();
			testBytes = null;

			csharp.Dispose();
			csharp = null;

			Api.lua_close(L);
			L = IntPtr.Zero;
		}

		public static implicit operator IntPtr(Lua l)
		{
			if (l != null)
				return l.L;
			return IntPtr.Zero;
		}

		const string kLuaStub_AddPath = 
			"function(p, path)\n" + 
#if UNITY_EDITOR
			"  package[p] = path .. ';' .. package[p]\n" +
#endif
			"end";
		LuaFunction addPath;

		public void AddCPath(string cpath)
		{
			addPath.Invoke(null, "cpath", cpath);
		}

		public void AddPath(string path)
		{
			addPath.Invoke(null, "path", path);
		}

		const string kLuaStub_ForbidGlobalVar =
			"return function(forbid)\n" + 
			"  if forbid then\n" +
			"    setmetatable(_G, forbid and { __newindex = function(t, k, v) assert(false, 'set value on global table is forbidden.') end} or {})\n" +
			"  end\n" +
			"end";

		public void ForbidGlobalVar(bool forbid)
		{
			Api.luaL_dostring(L, kLuaStub_ForbidGlobalVar);
			Api.lua_pushboolean(L, forbid);
			Call(1, 0);
		}

		public void SetGlobal(string name, object value)
		{
			PushValue(value);
			Api.lua_setglobal(L, name);
		}

		public void RunScript(string scriptName)
		{
			string scriptPath;
			LoadChunkFromFile(scriptName, out scriptPath);
			Call(0, 0);
		}

		public object RunScript1(string scriptName)
		{
			string scriptPath;
			var top = Api.lua_gettop(L);
			LoadChunkFromFile(scriptName, out scriptPath);
			Call(0, 1);
			var ret = ValueAt(-1);
			Api.lua_settop(L, top);	// should left nothing on stack
			return ret;
		}

		public object Require(string scriptName)
		{
			Api.luaL_requiref(L, scriptName, LoadScript1, 0);
			string errorMessage;
			if (Lua.TestError(L, -1, out errorMessage))
			{
				Debug.LogError(errorMessage);
				return null;
			}
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

		internal static Lua CheckHost(IntPtr L)
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
				throw new LuaException("__host not found or mismatch.");
			}
			return host;
		}

		internal void Assert(bool condition, string message = "assertion failed.")
		{
			if (!condition) throw new LuaException(message);
		}

		// 

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int Searcher(IntPtr L)
		{
			var top = Api.lua_gettop(L);
			var scriptName = Api.lua_tostring(L, 1);
			try
			{
				var host = CheckHost(L);
				string scriptPath = string.Empty;
				host.LoadChunkFromFile(scriptName, out scriptPath);
				host.PushValue(scriptPath);
				return 2;
			}
			catch (Exception e)
			{
				Api.lua_settop(L, top);
				Api.lua_pushstring(L, string.Format("\n\terror loading module '{0}' {1}", scriptName, e.Message));
				return 1;
			}
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

		void LoadChunkFromFile(string scriptName, out string scriptPath)
		{
			var bytes = loadScriptFromFile(scriptName, out scriptPath);
			if (bytes == null)
			{
				throw new LuaException(string.Format("0 bytes loaded from {0}", scriptName));
			}
			var chunkName = string.Format("@{0}", scriptPath);
#if UNITY_EDITOR
			chunkName = chunkName.Replace('/', '\\');
#endif
			LoadChunk(bytes, chunkName);
		}

		void LoadScriptInternal(string scriptName, int nret, out string scriptPath)
		{
			LoadChunkFromFile(scriptName, out scriptPath);
			Call(0, nret);
		}

		// Run script and adjust the numb of return	value to 1
		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int LoadScript(IntPtr L)
		{
			try
			{
				return LoadScriptInternal(L);
			}
			catch (Exception e)
			{
				PushErrorObject(L, e.Message);
				return 1;
			}
		}

		static int LoadScriptInternal(IntPtr L)
		{
			var host = CheckHost(L);
			var scriptName = Api.luaL_checkstring(L, 1);
			var top = Api.lua_gettop(L);
			try
			{
				string scriptPath;
				host.LoadScriptInternal(scriptName, Api.LUA_MULTRET, out scriptPath);
			}
			catch (Exception e)
			{
				throw new Exception(string.Format("LoadScript \"{0}\" failed: {1}", scriptName), e);
			}
			return Api.lua_gettop(L) - top;
		}

		// Run script and adjust the numb of return	value to 1
		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int LoadScript1(IntPtr L)
		{
			try
			{
				return LoadScript1Internal(L);
			}
			catch (Exception e)
			{
				PushErrorObject(L, e.Message);
				return 1;
			}
		}

		static int LoadScript1Internal(IntPtr L)
		{
			var host = CheckHost(L);
			var scriptName = Api.luaL_checkstring(L, 1);
			string scriptPath = string.Empty;
			try
			{
				host.LoadScriptInternal(scriptName, 1, out scriptPath);
			}
			catch (Exception e)
			{
				throw new Exception(string.Format("LoadScript \"{0}\" from path {1} failed:\n{2}", scriptName, scriptPath, e.Message));
			}
			return 1;
		}


#if UNITY_EDITOR
		// LoadScript, return result, scriptPath , have to public for Editor script
		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		public static int LoadScript1InEditor(IntPtr L)
		{
			try
			{
				return LoadScript1InEditorInternal(L);
			}
			catch (Exception e)
			{
				PushErrorObject(L, e.Message);
				return 1;
			}
		}

		static int LoadScript1InEditorInternal(IntPtr L)
		{
			var host = CheckHost(L);
			var scriptName = Api.luaL_checkstring(L, 1);
			try
			{
				string scriptPath;
				host.LoadScriptInternal(scriptName, 1, out scriptPath);
				host.PushValue(scriptPath);
			}
			catch (Exception e)
			{
				PushErrorObject(L, string.Format("LoadScript2 \"{0}\" failed: {1}", scriptName, e.Message));
				return 1;
			}
			return 2;
		}
#endif

		[MonoPInvokeCallback(typeof(Api.lua_Reader))]
		unsafe static IntPtr ChunkLoader(IntPtr L, IntPtr data, out UIntPtr size)
		{
			try
			{
				return ChunkLoaderInternal(L, data, out size);
			}
			catch (Exception e)
			{
				Debug.LogError(string.Format("ChunkLoader error: {0}", e.Message));
				size = UIntPtr.Zero;
				return IntPtr.Zero;
			}
		}

		unsafe static IntPtr ChunkLoaderInternal(IntPtr L, IntPtr data, out UIntPtr size)
		{
			var handleToBinaryChunk = GCHandle.FromIntPtr(data);
			var chunk = handleToBinaryChunk.Target as Chunk;
			var bytes = chunk.bytes.Target as byte[];
			if (chunk.pos < bytes.Length)
			{
				var curPos = chunk.pos;
				size = new UIntPtr((uint)bytes.Length); // read all at once
				chunk.pos = bytes.Length;
				return Marshal.UnsafeAddrOfPinnedArrayElement(bytes, curPos);
			}
			size = UIntPtr.Zero;
			return IntPtr.Zero;
		}

		class Chunk
		{
			public GCHandle bytes;
			public int pos;
		}

		public void LoadChunk(byte[] bytes, string chunkname, string mode = "bt")
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
		static int ChunkWriter(IntPtr L, IntPtr p, UIntPtr sz, IntPtr ud)
		{
			try
			{
				return ChunkWriterInternal(L, p, sz, ud);
			}
			catch (Exception e)
			{
				Debug.LogError(string.Format("ChunkWriter error: {0}", e.Message));
				return 0;
			}
		}
		static int ChunkWriterInternal(IntPtr L, IntPtr p, UIntPtr sz, IntPtr ud)
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

		// Caution! the dumpped chunk is not portable. you can not save it and run on another device.
		public byte[] DumpChunk(bool strip = true)
		{
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
			Debug.LogError(string.Format("LUA FATAL: {0}", Api.lua_tostring(L, -1)));
			return 0;
		}

		internal const string objectMetaTable = "object_meta";
		internal const string classMetaTable = "class_meta";

		// [-0,	+1,	m]
 		// return 1 if Metatable of object is newly created. 
		internal int PushObject(object obj, string metaTableName = objectMetaTable)
		{
			var handleToObj = GCHandle.Alloc(obj);
			var ptrToObjHandle = GCHandle.ToIntPtr(handleToObj);
			var userdata = Api.lua_newuserdata(L, new UIntPtr((uint)IntPtr.Size));
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
			var userdata = Api.luaL_testudata(L, idx, objectMetaTable);
			if (userdata == IntPtr.Zero) userdata = Api.luaL_testudata(L, idx, classMetaTable);
			if (userdata != IntPtr.Zero)
				return UdataToObject(userdata);
			return null;
		}

		internal static object UdataToObject(IntPtr userdata)
		{
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

		byte[] TestBytes(int idx)
		{
			Api.lua_pushvalue(L, idx);
			testBytes.Push(); // dont use invoke, preventing conversion from Lua -> C# before value is correct
			Api.lua_insert(L, -2);
			Call(1, 1);
			if (Api.lua_isnil(L, -1))
			{
				Api.lua_pop(L, 1);
				return null;
			}
			else // convert to bytes
			{
				var ret = Api.lua_tobytes(L, -1);
				Api.lua_pop(L, 1);
				return ret;
			}
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

					var bytes = TestBytes(idx); // maybe a bytes
					if (bytes != null)
					{
						return bytes;
					}
					return LuaTable.MakeRefTo(this, idx);

				case Api.LUA_TFUNCTION:
					return LuaFunction.MakeRefTo(this, idx);
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
					return (type == typeof(LuaFunction));
				case Api.LUA_TTABLE:
					return (type == typeof(LuaTable));
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

		internal static bool MatchArgs(System.Reflection.ParameterInfo[] args, int[] luaArgTypes)
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

		internal object[] ArgsFrom(System.Reflection.ParameterInfo[] args, int argStart, int numArgs, out IDisposable[] disposableArgs)
		{
			if (args == null || args.Length == 0)
			{
				disposableArgs = null;
				return csharpArgs_NoArgs;
			}
			Assert(args.Length == numArgs - argStart + 1, "Different count of arguments.");
			var actualArgs = new object[numArgs - argStart + 1];
			disposableArgs = new IDisposable[actualArgs.Length];
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
						if (type.IsByRef)
						{
							type = type.GetElementType();
						}
						var converted = System.Convert.ChangeType(nvalue, type);
						actualArgs[idx] = converted;
						break;
					case Api.LUA_TUSERDATA:
						actualArgs[idx] = ObjectAt(i);
						break;
					case Api.LUA_TTABLE:
						var t = LuaTable.MakeRefTo(this, i);
						disposableArgs[idx] = t;
						actualArgs[idx] = t;
						break;
					case Api.LUA_TFUNCTION:
						var f = LuaFunction.MakeRefTo(this, i);
						disposableArgs[idx] = f;
						actualArgs[idx] = f;
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

		internal static readonly int[] luaArgTypes_NoArgs = new int[0];

		internal static string GetLuaInvokingSigniture(string methodName, int[] args)
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

		internal static System.Reflection.MethodBase GetMethodFromCache(Type targetType, string mangledName)
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

		internal static string Mangle(string methodName, int[] luaArgTypes, bool invokingStaticMethod)
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

		internal static void CacheMethod(IntPtr L, Type targetType, string mangledName, System.Reflection.MethodBase method)
		{
			if (!useMethodCache) return;
			var host = CheckHost(L);

			lock (methodCache)
			{
				Dictionary<string, System.Reflection.MethodBase> cachedMethods;
				if (!methodCache.TryGetValue(targetType, out cachedMethods))
				{
					cachedMethods = new Dictionary<string, System.Reflection.MethodBase>();
					methodCache.Add(targetType, cachedMethods);
				}
				host.Assert(!cachedMethods.ContainsKey(mangledName), string.Format("{0} of {1} already cached with mangled name {2}", method.ToString(), targetType.ToString(), mangledName));
				cachedMethods.Add(mangledName, method);
			}
		}

		internal static void PushErrorObject(IntPtr L, string message) // No Throw
		{
			try
			{
				var host = CheckHost(L);
				if (host.pushError != null) // pushError may not be prepared, 
				{
					host.pushError.Push();  // DO NOT use Invoke, or you have infinite recursive call
					Api.lua_pushstring(L, message);
					if (Api.LUA_OK != Api.lua_pcall(L, 1, 1, 0))
					{
						var err = Api.lua_tostring(L, -1);
						Api.lua_pop(L, 1);
						throw new LuaException(err); // err in push err, WTF, catches below 
					}
					// here we got error object on stack
				}
				else // host.pushError not ready
				{
					Api.lua_pushstring(L, message);
				}
			}
			catch (Exception e) // host not ready? host.pushError error? err in test err
			{
				Api.lua_pushstring(L, message + "\n" + e.Message); // have to left message + e.Message on stack, what's happing?
			}
		}

		public static bool TestError(IntPtr L, int idx, out string errorMessage)
		{
			try
			{
				var host = CheckHost(L);
				if (host.checkError != null) // huh, in Lua.Ctor, checkError may no be prepared
				{
					Api.lua_pushvalue(L, idx);
					host.checkError.Push(); // dont use Invoke. host.Call inside of it results an infinite recursive call.
					Api.lua_insert(L, -2);
					if (Api.LUA_OK != Api.lua_pcall(L, 1, 0, 0)) // do not use host.Call, or you have infinite recursive call.
					{
						// has error, test_error calls error() in lua script, catches here
						errorMessage = Api.lua_tostring(L, -1);
						Api.lua_pop(L, 1);
						return true;
					}
					errorMessage = string.Empty; // correct
					return false;
				}
				else
				{
					// if checkError not prepared, there is no error object
					errorMessage = string.Empty;
					return false;
				}
			}
			catch (Exception e) // host not ready? checkError error
			{
				errorMessage = "TestError failed: " + e.Message;
				return true;
			}
		}

		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		internal static int InvokeMethod(IntPtr L)
		{
			try
			{
				return InvokeMethodInternal(L);
			}
			catch (System.Reflection.TargetInvocationException e)
			{
				PushErrorObject(L, e.InnerException.Message);
				return 1;
			}
			catch (Exception e)
			{
				PushErrorObject(L, e.Message);
				return 1;
			}
		}

		static int InvokeMethodInternal(IntPtr L)
		{
			// upvalue 1 --> isInvokingFromClass
			// upvalue 2 --> userdata (host of metatable).
			// upvalue 3 --> member name
			var host = CheckHost(L);

			var isInvokingFromClass = Api.lua_toboolean(L, Api.lua_upvalueindex(1));
			var obj = host.ObjectAt(Api.lua_upvalueindex(2));
			host.Assert(obj != null, "Invoking target not found at upvalueindex(2)");
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
				host.Assert(invokingStaticMethod, string.Format("Invoking static method {0} from class {1} with incorrect syntax", methodName, type.ToString()));
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
					host.Assert(member.MemberType == System.Reflection.MemberTypes.Method, string.Format("{0} is not a Method.", methodName));
					var m = (System.Reflection.MethodInfo)member;
					if (m.IsStatic)
					{
						host.Assert(invokingStaticMethod, string.Format("Invoking static method {0} with incorrect syntax.", m.ToString()));
						target = null;
					}
					else
					{
						host.Assert(!invokingStaticMethod, string.Format("Invoking non-static method {0} with incorrect syntax.", m.ToString()));
					}
					parameters = m.GetParameters();
					if (MatchArgs(parameters, luaArgTypes))
					{
						CacheMethod(L, type, mangledName, m);
						method = m;
						break;  // found one, break
					}
				}
				host.Assert(method != null, string.Format("No corresponding csharp method for {0}", GetLuaInvokingSigniture(methodName, luaArgTypes)));
			}
			else
			{
				parameters = method.GetParameters();
			}

			var top = Api.lua_gettop(L);
			IDisposable[] disposableArgs;
			var actualArgs = host.ArgsFrom(parameters, argStart, numArgs, out disposableArgs);
			host.Assert(top == Api.lua_gettop(L), "stack changed after converted args from lua.");

			var retVal = method.Invoke(target, actualArgs);

			if (disposableArgs != null)
			{
				foreach (var d in disposableArgs)
				{
					if (d != null) d.Dispose();
				}
			}

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

		

		public void Import(Type type, string name)
		{
			Import(type);
			Api.lua_setglobal(L, name);
		}

		// [ 0 | +1 | -]
		internal bool Import(Type type)
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
		static int ImportInternal(IntPtr L)
		{
			try
			{
				return ImportInternal_(L);
			}
			catch (Exception e)
			{
				PushErrorObject(L, e.Message);
				return 1;
			}
		}

		static int ImportInternal_(IntPtr L)
		{
			var host = CheckHost(L);
			var typename = Api.luaL_checkstring(L, 1);
			Debug.LogFormat("{0} imported.", typename);
			var type = Type.GetType(typename);
			if (type == null)
			{
				Api.lua_pushnil(L);
				return 1;
			}
			if (host.PushObject(type, classMetaTable) == 1) // TODO: opt, for type loaded, cache it
			{
				Api.lua_getmetatable(L, -1);

				Api.lua_pushboolean(L, true);
				Api.lua_rawseti(L, -2, 1); // isClassObject = true

				Api.lua_pushcclosure(L, MetaMethod.MetaConstructFunction, 0);
				Api.lua_setfield(L, -2, "__call");

				Api.lua_pop(L, 1);
			}
			return 1;
		}


		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int Import(IntPtr L)
		{
			var typename = Api.luaL_checkstring(L, 1);
			Api.luaL_requiref(L, typename, ImportInternal, 0);
			return 1;
		}

		public void PushArray(object value, bool byRef = false)
		{
			if (value == null)
			{
				Api.lua_pushnil(L);
				return;
			}

			var type = value.GetType();
			Debug.Assert(type.IsArray, "PushArray called with non-array value");
			if (type.IsArray)
			{
				if (type == typeof(byte[]))
				{
					Api.lua_pushbytes(L, (byte[])value);
					return;
				}

				if (byRef)
				{
					PushObject(value);
					return;
				}

				// other primitive array?
				var arr = (System.Array)value;
				if (arr.Length <= Config.PassAsObjectIfArrayLengthGreatThan)
				{
					Api.lua_createtable(L, arr.Length, 0);
					for (int i = 0; i < arr.Length; ++i)
					{
						PushValue(arr.GetValue(i));
						Api.lua_seti(L, -2, i + 1);
					}
					return;
				}

				PushObject(value);
			}
			else
			{
				Api.lua_pushnil(L);
			}
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
				PushArray(value, byRef: true);
				return;
			}

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
			else if (type == typeof(LuaFunction))
			{
				var f = (LuaFunction)value;
				f.Push();
			}
			else if (type == typeof(LuaTable))
			{
				var t = (LuaTable)value;
				t.Push();
			}
			else if (typeof(System.Delegate).IsAssignableFrom(type))
			{
				var f = LuaFunction.CreateDelegate(this, (System.Delegate)value);
				f.Push();
				f.Dispose(); // safely Dispose here
			}
			else
			{
				PushObject(value);
			}
		}

		internal int GetMember(object obj, Type objType, string memberName)
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
						Assert(objType != typeof(object), string.Format("Member {0} not found. {1}", memberName, ae.Message));
						return GetMember(obj, objType.BaseType, memberName);
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
				Assert(objType != typeof(object), string.Format("Member {0} not found.", memberName));
				return GetMember(obj, objType.BaseType, memberName);
			}
		}

		internal int IndexObject(object obj, Type type, object[] index)
		{
			Assert(index != null);
			var prop = type.GetProperty("Item");
			if (prop == null)
			{
				Assert(type != typeof(object), string.Format("No indexer found in {0}", obj.GetType()));
				return IndexObject(obj, type.BaseType, index);
			}
			try
			{
				var value = prop.GetValue(obj, index);
				PushValue(value);
				return 1;
			}
			catch (ArgumentException ae)
			{
				Assert(type != typeof(object), string.Format("Incorrect indexer called in {0}: {1}", obj.GetType(), ae.Message));
				return IndexObject(obj, type.BaseType, index);
			}
		}

		internal void SetValueAtIndexOfObject(object obj, Type type, object[] index, object value)
		{
			Assert(index != null);
			var prop = type.GetProperty("Item");
			if (prop == null)
			{
				Assert(type != typeof(object), string.Format("No indexer found in {0}", obj.GetType()));
				SetValueAtIndexOfObject(obj, type.BaseType, index, value);
			}
			try
			{
				var propType = prop.PropertyType;
				object convertedNumber;
				if (ConvertNumber(propType, value, out convertedNumber))
				{
					prop.SetValue(obj, convertedNumber, index);
				}
				else
				{
					var converted = System.Convert.ChangeType(value, propType);
					prop.SetValue(obj, converted, index);
				}
			}
			catch (ArgumentException ae)
			{
				Assert(type != typeof(object), string.Format("Incorrect indexer called in {0}: {1}", obj.GetType(), ae.Message));
				SetValueAtIndexOfObject(obj, type.BaseType, index, value);
			}
		}

		internal static bool ConvertNumber(Type type, object value, out object converted)
		{
			if (type == typeof(int))
			{
				converted = (int)(long)value;
				return true;
			}
			else if (type == typeof(float))
			{
				converted = (float)(double)value;
				return true;
			}
			else if (type == typeof(long))
			{
				converted = (long)value;
				return true;
			}
			else if (type == typeof(double))
			{
				converted = (double)value;
				return true;
			}
			else if (type == typeof(short))
			{
				converted = (short)(long)value;
				return true;
			}
			else if (type == typeof(uint))
			{
				converted = (uint)(long)value;
				return true;
			}
			else if (type == typeof(ulong))
			{
				converted = (long)value;
				return true;
			}
			else if (type == typeof(ushort))
			{
				converted = (ushort)(long)value;
				return true;
			}
			else if (type == typeof(ushort))
			{
				converted = (ushort)(long)value;
				return true;
			}
			else if (type == typeof(char))
			{
				converted = (char)(long)value;
				return true;
			}
			else if (type == typeof(byte))
			{
				converted = (byte)(long)value;
				return true;
			}
			else if (type == typeof(sbyte))
			{
				converted = (sbyte)(long)value;
				return true;
			}
			converted = null;
			return false;
		}

		internal void SetMember(object thisObject, Type type, string memberName, object value)
		{
			Assert(type.IsClass, string.Format("Setting property {0} of {1} object", memberName, type.ToString()));

			var members = type.GetMember(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance);
			Assert(members.Length > 0, string.Format("Cannot find property with name {0} of type {1}", memberName, type.ToString()));

			if (members.Length > 0)
			{
				var member = members[0];
				if (member.MemberType == System.Reflection.MemberTypes.Field)
				{
					var field = (System.Reflection.FieldInfo)member;
					var fieldType = field.FieldType;
					object convertedNumber;
					if (ConvertNumber(fieldType, value, out convertedNumber))
					{
						field.SetValue(thisObject, convertedNumber);
					}
					else
					{
						var converted = System.Convert.ChangeType(value, fieldType);
						field.SetValue(thisObject, converted);
					}
				}
				else if (member.MemberType == System.Reflection.MemberTypes.Property)
				{
					var prop = (System.Reflection.PropertyInfo)member;
					var propType = prop.PropertyType;
					object convertedNumber;
					if (ConvertNumber(propType, value, out convertedNumber))
					{
						prop.SetValue(thisObject, convertedNumber, null);
					}
					else
					{
						var converted = System.Convert.ChangeType(value, propType);
						prop.SetValue(thisObject, converted, null);
					}
				}
				else
				{
					Assert(false, string.Format("Member type {0} and {1} expected, but {2} got.", 
						System.Reflection.MemberTypes.Field, System.Reflection.MemberTypes.Property, member.MemberType));
				}
			}
		}

		internal enum BinaryOp
		{
			op_Addition = 0,
			op_Subtraction,
			op_Multiply,
			op_Division,
			op_Modulus,

			op_Equality,
			op_LessThan,

			op_LessThanOrEqual,

		}

		static readonly KeyValuePair<string, int>[] binaryOps = new KeyValuePair<string, int>[]
		{
			new KeyValuePair<string, int>("__add", (int)BinaryOp.op_Addition),
			new KeyValuePair<string, int>("__sub", (int)BinaryOp.op_Subtraction),
			new KeyValuePair<string, int>("__mul", (int)BinaryOp.op_Multiply),
			new KeyValuePair<string, int>("__div", (int)BinaryOp.op_Division),
			new KeyValuePair<string, int>("__mod", (int)BinaryOp.op_Modulus),

			new KeyValuePair<string, int>("__eq", (int)BinaryOp.op_Equality),
			new KeyValuePair<string, int>("__lt", (int)BinaryOp.op_LessThan),
			new KeyValuePair<string, int>("__le", (int)BinaryOp.op_LessThanOrEqual),

		};

		internal enum UnaryOp
		{
			op_UnaryNegation = 0,
		}

		static readonly KeyValuePair<string, int>[] unaryOps = new KeyValuePair<string, int>[]
		{
			new KeyValuePair<string, int>("__unm", (int)UnaryOp.op_UnaryNegation),
		};


		// [-0, +1, -]
		int NewObjectMetatable(string metaTableName)
		{
			if (Api.luaL_newmetatable(L, metaTableName) == 1)
			{
				Debug.LogFormat("Registering object meta table {0} ... ", metaTableName);
				Api.lua_pushboolean(L, false);
				Api.lua_rawseti(L, -2, 1); // isClassObject = false

				foreach (var op in binaryOps)
				{
                    Api.lua_pushinteger(L, op.Value);
					Api.lua_pushcclosure(L, MetaMethod.MetaBinaryOpFunction, 1);
					Api.lua_setfield(L, -2, op.Key);
				}

				foreach(var op in unaryOps)
				{
					Api.lua_pushinteger(L, op.Value);
					Api.lua_pushcclosure(L, MetaMethod.MetaUnaryOpFunction, 1);
					Api.lua_setfield(L, -2, op.Key);
				}

				Api.lua_pushcclosure(L, MetaMethod.MetaIndexFunction, 0);
				Api.lua_setfield(L, -2, "__index");

				Api.lua_pushcclosure(L, MetaMethod.MetaNewIndexFunction, 0);
				Api.lua_setfield(L, -2, "__newindex");

				Api.lua_pushcclosure(L, MetaMethod.MetaToStringFunction, 0);
				Api.lua_setfield(L, -2, "__tostring");

				Api.lua_pushcclosure(L, MetaMethod.MetaGcFunction, 0);
				Api.lua_setfield(L, -2, "__gc");

				return 1;
			}
			return 0;
		}

		public void DoString(string luaScript, int nrets = 0)
		{
			var ret = Api.luaL_loadstring(L, luaScript);
			if (ret != Api.LUA_OK)
			{
				var err = Api.lua_tostring(L, -1);
				Api.lua_pop(L, 1);
				throw new LuaException(err, ret);
			}
			Call(0, nrets);
		}


		// Invoking Lua Function
		[MonoPInvokeCallback(typeof(Api.lua_CFunction))]
		static int HandleLuaFunctionInvokingError(IntPtr L)
		{
			var err = Api.lua_tostring(L, 1);
			Api.luaL_traceback(L, L, err, 0);
			err = Api.lua_tostring(L, -1);
			Api.lua_pop(L, 1);
			PushErrorObject(L, err);
			return 1;
		}

		public void Call(int nargs, int nresults)
		{
			var stackTop = Api.lua_gettop(L) - nargs - 1; // function and args

			Api.lua_pushcclosure(L, HandleLuaFunctionInvokingError, 0);
			Api.lua_insert(L, stackTop + 1); // put err func to stackTop
			var ret = Api.lua_pcall(L, nargs, nresults, stackTop + 1);
			if (ret != Api.LUA_OK)
			{
				string errorMessage;
				if (!TestError(L, -1, out errorMessage))
				{
					errorMessage = Api.lua_tostring(L, -1);
				}
				Api.lua_settop(L, stackTop);
				throw new LuaException(errorMessage, ret);
			}
			else
			{
				// check error object
				string errorMessage;
				if (TestError(L, -1, out errorMessage))
				{
					Api.lua_settop(L, stackTop);
					throw new LuaException(errorMessage);
				}
				Api.lua_remove(L, stackTop + 1); // remove err func
			}
		}

		public static string DebugStack(IntPtr L)
		{
			var top = Api.lua_gettop(L);
			var sb = new System.Text.StringBuilder();
			for (int i = top; i > 0; i--)
			{
				var type = Api.lua_type(L, i);
				sb.Append(i);
				sb.Append(":\t");
				sb.Append(Api.lua_typename(L, type));
				sb.Append("\t");
				Api.lua_pushvalue(L, i);
				sb.Append(Api.lua_tostring(L, -1));
				Api.lua_pop(L, 1);
				sb.AppendLine();
			}
			return sb.ToString();
		}
	}
}
