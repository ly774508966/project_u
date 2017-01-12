using System.Reflection;

namespace lua.hotpatch
{
	public class LuaHotPatchLoader
	{
		[LuaHotPatchHub]
		public static bool Hub(MethodBase method, 
			object target,
			out	object retval, 
			params object[] args)
		{
			retval = null;
			return true;
		}
	}
}
