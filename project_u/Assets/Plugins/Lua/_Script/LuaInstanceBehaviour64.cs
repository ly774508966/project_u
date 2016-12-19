using UnityEngine;

namespace lua
{
	public class LuaInstanceBehaviour64 : LuaInstanceBehaviour
	{
		void FixedUpdate()
		{
			luaBehaviour.SendLuaMessage(LuaBehaviour.Message.FixedUpdate);
		}
	}
}
