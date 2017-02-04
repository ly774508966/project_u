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
using UnityEngine;
using System;
using System.Collections;

namespace lua
{

	/*
	 * 
	 *  -- A LuaBehaviour script, MyLuaBehaviour.lua
	 *  
	 * 	local MyLuaBehaviour = {}
	 * 	
	 *  -- _Init function for new behaviour instance
	 *  function MyLuaBehaviour._Init(instance) -- notice, it use dot `.' to define _Init function (`static' function)
	 * 		instance.value0 = 32
	 * 		instance.value1 = 'abc'
	 * 
	 * 		local Vector3 = csharp.import('UnityEngine.Vector3, UnityEngine') -- import a type from C#
	 * 		instance.value2 = Vector3(1, 2, 3)
	 *  end
	 * 
	 *  -- When a new GameObject which has LuaBehaviour component with MyLuaBehaviour.lua attached Awake
	 *  -- from deserialized data, it will create an empty table as instance of this Lua component (aka behaviour table).
	 *  -- The instance (behaviour table) is passed to _Init function for initialization. 
	 *  -- All values set to instance *in _Init function* can be serialized with GameObject. In fact, the function it self is serialized.
	 *  -- And those values will show in Inspector. Any tweaking on those values also can be serialized as new _Init
	 *  -- function.
	 *  -- When Awake from deserialized data, _Init function is loaded and 'hides' the original _Init function. 
	 *	-- The your version of _Init function is executed to restore values serialized.
	 *	-- And behaviour table all so can be used to save local value at run-time.
	 * 
	 * 
	 *  -- called at the end of host LuaBehaviour.Awake. 
	 *  function MyLuaBehaviour:Awake(instance) -- behaviour table
	 * 		-- awake
	 * 		instance.some_value = self:FunctionFromCsharp() -- calling function defined in C# side and store return value to behaviour table.
	 *  end
	 * 
	 * 	-- called at LuaBehaviour.Update
	 *  function MyLuaBehaviour:Update(instance)
	 * 		-- update
	 *  end
	 * 
	 *  -- You can also have other messages which defined in enum LuaBehaviour.Message.
	 *  -- For performance reason, those *Update messages are combined in different components, LuaInstanceBehaviour*
	 * 
	 * 	return MyLuaBehaviour -- Important! Return the `class' to host, and becoming the `meta-class' of instance.
	 */

	public class LuaBehaviour : MonoBehaviour
	{
		static Lua L;
		public static void SetLua(Lua luaVm)
		{
			if (L != null)
			{
				Debug.LogWarning("Lua state chagned, LuaBehaviour will run in new state.");
			}
			L = luaVm;
		}


		public string scriptName;
#if UNITY_EDITOR
		[NonSerialized]
		public string scriptPath;
#endif

		[SerializeField]
		[HideInInspector]
		string[] keys;
		[SerializeField]
		[HideInInspector]
		GameObject[] gameObjects;

		LuaInstanceBehaviour0 instanceBehaviour;

		public enum Message
		{
			Awake = 0,
			Start,
			OnDestroy,

			OnEnable,
			OnDisable,

			Update,
			FixedUpdate,
			LateUpdate,

			_Count
		}
		int[] messageRef_ = null;
		int[] messageRef
		{
			get
			{
				if (messageRef_ == null)
				{
					messageRef_ = new int[(int)Message._Count];
					for (int i = 0; i < messageRef_.Length; ++i)
					{
						messageRef_[i] = Api.LUA_NOREF;
					}
				}
				return messageRef_;
			}
		}

		int messageFlag = 0;
		static int MakeFlag(Message m)
		{
			return 1 << (int)m;
		}

		int handleToThis = Api.LUA_NOREF;
		bool scriptLoaded_ = false;
		bool scriptLoaded
		{
			get
			{
				return L != null && scriptLoaded_;
			}
		}
		int luaBehaviourRef = Api.LUA_NOREF;

		void Awake()
		{
			if (L == null)
			{
				Debug.LogError("Call LuaBehaviour.SetLua first.");
				return;
			}

			if (string.IsNullOrEmpty(scriptName))
			{
				Debug.LogWarning("LuaBehaviour with empty scriptName.");
				return;
			}

			// make	instance
			handleToThis = L.MakeRefTo(this);

			Api.lua_newtable(L); // instance behaviour table

			// ref instance behaviour table
			luaBehaviourRef = L.MakeRefAt(-1);

		
			// meta
			Api.lua_createtable(L, 0, 1);
			// load	class
			Api.luaL_requiref(L, scriptName, Lua.LoadScript1, 0);

			if (Api.lua_istable(L, -1)) // set metatable and bind messages
			{
				// stack: behaviour table, meta, script
				Api.lua_setfield(L, -2, "__index"); // meta.__index = script
													// stack: behaviour table, meta
				Api.lua_setmetatable(L, -2);

				// check message
				for (Message i = Message.Awake; i < Message._Count; ++i)
				{
					Api.lua_getfield(L, -1, i.ToString());
					if (Api.lua_isfunction(L, -1))
					{
						messageFlag = messageFlag | MakeFlag(i);
						messageRef[(int)i] = Api.luaL_ref(L, -2); // func pops, and make ref in behaviour table
					}
					else
					{
						Api.lua_pop(L, 1); // pop field
					}
				}

				// choose script
				int flag = messageFlag & (MakeFlag(Message.Update) | MakeFlag(Message.FixedUpdate) | MakeFlag(Message.LateUpdate));
				var componentType = Type.GetType("lua.LuaInstanceBehaviour" + flag.ToString());
				instanceBehaviour = gameObject.AddComponent(componentType) as LuaInstanceBehaviour0;

				Api.lua_pop(L, 1); // pop behaviour table

				scriptLoaded_ = true;

			}
			else
			{
				Api.lua_pop(L, 3); // pop behaviour table, meta and result of requiref
			}


			if (scriptLoaded_)
			{
				// load	_Init from serialized version
				LoadInitFuncToBehaviourTable(L);
				RunInitFuncOnBehaviourTable(L);

				// Awake Lua script
				instanceBehaviour.SetLuaBehaviour(this);
			}
			else
			{
				Debug.LogWarningFormat("No Lua script running with {0}.", gameObject.name);
			}
		}

		void RunInitFuncOnBehaviourTable(Lua L)
		{
			Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, luaBehaviourRef);
			// Behaviour._Init hides Script._Init
			Api.lua_getfield(L, -1, "_Init");
			if (Api.lua_isfunction(L, -1))
			{
				Api.lua_pushvalue(L, -2);
				try
				{
					L.Call(1, 0);
				}
				catch (Exception e)
				{
					Debug.LogErrorFormat("{0}._Init failed: {1}.", scriptName, e.Message);
				}
			}
			else
			{
				Api.lua_pop(L, 1); // pop non-function
			}
			Api.lua_pop(L, 1); // pop behaviour table
		}

		void Start()
		{
			SendLuaMessage(Message.Start);
		}

		void OnEnable()
		{
			if (instanceBehaviour != null)
				instanceBehaviour.enabled = true;
			SendLuaMessage(Message.OnEnable);
		}

		void OnDisable()
		{
			SendLuaMessage(Message.OnDisable);
			if (instanceBehaviour != null)
				instanceBehaviour.enabled = false;
		}

		void OnDestroy()
		{
			SendLuaMessage(Message.OnDestroy);
			if (L.valid)
			{
				for (int i = 0; i < messageRef.Length; ++i)
				{
					var r = messageRef[i];
					if (r != Api.LUA_NOREF)
					{
						Api.luaL_unref(L, Api.LUA_REGISTRYINDEX, r);
					}
					messageRef[i] = Api.LUA_NOREF;
				}
				messageFlag = 0;

				Api.luaL_unref(L, Api.LUA_REGISTRYINDEX, luaBehaviourRef);
				if (handleToThis != Api.LUA_NOREF)
					L.Unref(handleToThis);
				luaBehaviourRef = Api.LUA_NOREF;
				handleToThis = Api.LUA_NOREF;
				scriptLoaded_ = false;
			}
		}

		public object InvokeLuaMethod(string method, params object[] args)
		{
			if (!scriptLoaded) return null;

			var top = Api.lua_gettop(L);
			try
			{

				Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, luaBehaviourRef);
				if (Api.lua_getfield(L, -1, method) == Api.LUA_TFUNCTION)
				{
					L.PushRef(handleToThis);
					Api.lua_pushvalue(L, -3);
					int argsLength = 0;
					if (args != null && args.Length > 0)
					{
						L.PushArray(args);
						argsLength = args.Length;
					}
					L.Call(2 + argsLength, 1);
					Api.lua_settop(L, top);
					return L.ValueAt(-1);
				}
				Api.lua_settop(L, top);
			}
			catch (Exception e)
			{
				Api.lua_settop(L, top);
				Debug.LogErrorFormat("Invoke {0}.{1} failed: {2}", scriptName, method, e.Message);
			}
			return null;
		}

		public void SendLuaMessage(string message)
		{
			if (!scriptLoaded) return;

			Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, luaBehaviourRef);
			if (Api.lua_getfield(L, -1, message) == Api.LUA_TFUNCTION)
			{
				L.PushRef(handleToThis);
				Api.lua_pushvalue(L, -3);
				try
				{
					L.Call(2, 0);
				}
				catch (Exception e)
				{
					Debug.LogErrorFormat("Invoke {0}.{1} failed: {2}", scriptName, message, e.Message);
				}
			}
			Api.lua_pop(L, 1); // pop behaviour table
		}

		IEnumerator LuaCoroutine(LuaThread thread)
		{
			if (thread.Resume())
			{
				if (thread.hasYields)
				{
					var stuff = thread.current[1];
					if (stuff is YieldInstruction
						|| stuff is CustomYieldInstruction)
					{
						yield return stuff;
					}
				}
				yield return null;
			}
			thread.Dispose();
		}

		public void StartLuaCoroutine(LuaThread thread)
		{
			StartCoroutine(LuaCoroutine(thread.Retain()));
		}


		public void SendLuaMessage(Message message)
		{
			if (!scriptLoaded) return;

			if ((messageFlag & MakeFlag(message)) == 0) return; // no message defined

			Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, luaBehaviourRef);
			// get message func	from instance table
			if (Api.lua_rawgeti(L, -1, messageRef[(int)message]) == Api.LUA_TFUNCTION)
			{
				// stack: func, instance table
				L.PushRef(handleToThis); // this csharp object
				Api.lua_pushvalue(L, -3); // behaviour table
				try
				{
					L.Call(2, 0);
				}
				catch (Exception e)
				{
					Debug.LogErrorFormat("Invoke {0}.{1} failed: {2}", scriptName, message, e.Message);
				}
			}
			Api.lua_pop(L, 1); // pop behaviour table
		}

		[SerializeField]
		[HideInInspector]
		byte[] _Init;
		bool LoadInitFuncToBehaviourTable(Lua L)
		{
			if (_Init == null || _Init.Length == 0) return false;
			try
			{
				Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, luaBehaviourRef);
				L.LoadChunk(_Init, scriptName + "_Init");
				L.Call(0, 1); // run loaded chunk
				Api.lua_setfield(L, -2, "_Init");
				Api.lua_pop(L, 1);  // pop behaviour table
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError(e.Message);
			}
			return false;
		}

		// non-throw
		public GameObject FindGameObject(string key)
		{
			var index = System.Array.FindIndex(keys, (k) => k == key);
			if (index != -1)
				return GetGameObjectAtIndex(index);
			return null;
		}

		// non-throw
		public GameObject GetGameObjectAtIndex(int index)
		{
			if (index >= 0 && index < gameObjects.Length)
			{
				return gameObjects[index];
			}
			return null;
		}


#if UNITY_EDITOR
		public static System.Action debuggeePoll;
		static int debuggeeUpdatedFrameCount = 0;

		void LateUpdate()
		{
			if (debuggeeUpdatedFrameCount != Time.frameCount)
			{
				if (debuggeePoll != null)
					debuggeePoll();
				debuggeeUpdatedFrameCount = Time.frameCount;
			}
		}

		public bool IsInitFuncDumped()
		{
			return !string.IsNullOrEmpty(scriptName) && _Init != null && _Init.Length > 0;
		}

		public byte[] GetInitChunk()
		{
			return _Init;
		}

		public void SetInitChunk(byte[] chunk)
		{
			_Init = chunk;
			if (Application.isPlaying)
			{
				if (scriptLoaded)
				{
					LoadInitFuncToBehaviourTable(L);
					RunInitFuncOnBehaviourTable(L);
				}
			}
		}

		public void Reload()
		{
			if (Application.isPlaying)
			{
				using (var removeLoaded = LuaFunction.NewFunction(
					L,
					"function()\n" +
					" package.loaded['" + scriptName + "'] = nil\n" +
					"end"))
				{
					removeLoaded.Invoke();
					// https://docs.unity3d.com/Manual/ExecutionOrder.html
					OnDisable();
					OnDestroy();
					Destroy(instanceBehaviour);
					instanceBehaviour = null;
					Awake();
					OnEnable();
					Start();
				}
			}
		}
#endif
	}
}