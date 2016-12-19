using UnityEngine;

namespace lua
{
	public class LuaInstanceBehaviour128 : LuaInstanceBehaviour
	{
		void LateUpdate()
		{
			luaBehaviour.SendLuaMessage(LuaBehaviour.Message.LateUpdate);
		}
	}
}
