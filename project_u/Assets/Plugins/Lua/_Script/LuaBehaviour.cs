using UnityEngine;
using System;
using System.Collections.Generic;

namespace lua
{
	public class LuaBehaviour : MonoBehaviour
	{
		public string scriptName;


		MonoBehaviour instanceBehaviour;

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
		int[] messageRef = new int[(int)Message._Count];

		int messageFlag = 0;
		static int MakeFlag(Message m)
		{
			return 1 << (int)m;
		}

		object handleToThis;
		bool scriptLoaded = false;
		int luaBehaviourRef = Api.LUA_NOREF;

		void Awake()
		{
			if (string.IsNullOrEmpty(scriptName))
			{
				Debug.LogWarning("LuaBehaviour with empty scriptName.");
				return;
			}
			var L = lua.Lua.instance.luaState;

			// make	instance
			handleToThis = Lua.MakeRef(L, this);

			Api.lua_newtable(L); // instance behaviour table

			// ref instance behaviour table
			Api.lua_pushvalue(L, -1);
			luaBehaviourRef = Api.luaL_ref(L, Api.LUA_REGISTRYINDEX);

			// load	_Init from serialized version
			LoadInitFunc(L);

			// meta
			Api.lua_createtable(L, 0, 1);
			// load	class
			Api.luaL_requiref(L, scriptName, Lua.LoadScript, 0);

			// Behaviour._Init hides Script._Init
			Api.lua_getfield(L, -1, "_Init");
			if (Api.lua_isfunction(L, -1))
			{
				Api.lua_pushvalue(L, -2);
				try
				{
					Lua.Call(L, 1, 0);
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

			if (Api.lua_istable(L, -1))
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
				instanceBehaviour = gameObject.AddComponent(componentType) as MonoBehaviour;

				Api.lua_pop(L, 1); // pop behaviour table


				scriptLoaded = true;

				// Awake Lua script
				instanceBehaviour.SendMessage("SetLuaBehaviour", this);
			}
			else
			{
				Api.lua_pop(L, 3); // pop behaviour table, meta and result of requiref
			}
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

			Api.luaL_unref(Lua.instance.luaState, Api.LUA_REGISTRYINDEX, luaBehaviourRef);
			Lua.Unref(Lua.instance.luaState, handleToThis);
		}

		public void SendLuaMessage(Message message)
		{
			if (!scriptLoaded) return;

			if ((messageFlag & MakeFlag(message)) == 0) return; // no message defined

			var L = lua.Lua.instance.luaState;

			Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, luaBehaviourRef);
			// get message func	from instance table
			Api.lua_rawgeti(L, -1, messageRef[(int)message]); // stack: func, instance table
			if (Api.lua_isfunction(L, -1))
			{
				Lua.PushRef(L, handleToThis); // this csharp object
				Api.lua_pushvalue(L, -3); // behaviour table
				try
				{
					Lua.Call(L, 2, 0);
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
		string _Init;
		bool LoadInitFunc(IntPtr L)
		{
			if (string.IsNullOrEmpty(_Init)) return false;
			try
			{
				Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, luaBehaviourRef);
				Lua.LoadChunk(L, _Init, scriptName+"_Init");
				Api.lua_setfield(L, -2, "_Init");
				Api.lua_pop(L, 1);
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError(e.Message);
			}
			return false;
		}

#if UNITY_EDITOR
		public bool IsInitFuncDumped()
		{
			return !string.IsNullOrEmpty(scriptName) && !string.IsNullOrEmpty(_Init);
		}
		public string GetInitChunk()
		{
			return _Init;
		}
		public void SetInitChunk(string chunk)
		{
			_Init = chunk;
		}
#endif
	}
}