using UnityEngine;
using System.Collections;
using lua.hotpatch;

public class ToBePatched
{
	[LuaHotPatch]
	public static void PathMe()
	{
		LuaHotPatchLoader.Hub(0);
		throw new System.Exception("not patched1");
	}
}

