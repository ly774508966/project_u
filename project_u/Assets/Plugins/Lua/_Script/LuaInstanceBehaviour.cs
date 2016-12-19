using UnityEngine;

namespace lua
{
	public class LuaInstanceBehaviour : MonoBehaviour
	{
		protected LuaBehaviour luaBehaviour;
		void SetLuaBehaviour(LuaBehaviour behaviour)
		{
			luaBehaviour = behaviour;
			luaBehaviour.SendLuaMessage(LuaBehaviour.Message.Awake); // Awake Lua Script
		}
	}
}
