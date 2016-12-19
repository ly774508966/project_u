using UnityEngine;

namespace lua
{
	public class LuaInstanceBehaviour224 : LuaInstanceBehaviour
	{
		void Update()
		{
			luaBehaviour.SendLuaMessage(LuaBehaviour.Message.Update);
		}

		void FixedUpdate()
		{
			luaBehaviour.SendLuaMessage(LuaBehaviour.Message.FixedUpdate);
		}

		void LateUpdate()
		{
			luaBehaviour.SendLuaMessage(LuaBehaviour.Message.LateUpdate);
		}
	}
}
