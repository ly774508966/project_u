using UnityEngine;

namespace lua
{
	public class LuaInstanceBehaviour192 : LuaInstanceBehaviour
	{

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
