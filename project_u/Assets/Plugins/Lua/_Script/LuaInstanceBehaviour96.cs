using UnityEngine;

namespace lua
{
	public class LuaInstanceBehaviour96 : LuaInstanceBehaviour
	{
		void Update()
		{
			luaBehaviour.SendLuaMessage(LuaBehaviour.Message.Update);
		}

		void FixedUpdate()
		{
			luaBehaviour.SendLuaMessage(LuaBehaviour.Message.FixedUpdate);
		}
	}
}
