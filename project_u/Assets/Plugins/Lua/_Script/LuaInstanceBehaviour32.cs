using UnityEngine;

namespace lua
{
	public class LuaInstanceBehaviour32 : LuaInstanceBehaviour
	{
		void Update()
		{
			luaBehaviour.SendLuaMessage(LuaBehaviour.Message.Update);
		}
	}
}
