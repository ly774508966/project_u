using UnityEngine;

namespace lua
{
	public class LuaInstanceBehaviour160 : LuaInstanceBehaviour
	{
		void Update()
		{
			luaBehaviour.SendLuaMessage(LuaBehaviour.Message.Update);
		}

		void LateUpdate()
		{
			luaBehaviour.SendLuaMessage(LuaBehaviour.Message.LateUpdate);
		}
	}
}
