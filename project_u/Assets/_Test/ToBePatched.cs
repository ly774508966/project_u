using UnityEngine;
using System.Collections;
using lua.hotpatch;

public class ToBePatched
{
	[LuaHotPatch]
	public static void PatchMe()
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public void PatchThisOne()
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public void PatchThisOneWithParam(int a, int b)
	{
		throw new System.Exception("not patched");
	}

	public class A { }
	[LuaHotPatch]
	public A PatchThisOneWithParamAndReturn(int a, int b)
	{
		throw new System.Exception("not patched");
	}

	[LuaHotPatch]
	public int PatchThisOneWithParamAndReturnPrimitive(A a, int b)
	{
		throw new System.Exception("not patched");
	}

	public A CallTest(int a, int b)
	{
		System.Reflection.MethodBase k = null;
		object retval;
		LuaHotPatchLoader.Hub(k, this, out retval, a, b);
		return (A)retval;
	}

	public int CallTest2(A a, int b)
	{
		System.Reflection.MethodBase k = null;
		object retval;
		LuaHotPatchLoader.Hub(k, this, out retval, a, b);
		if (retval != null)
		{
			var type = retval.GetType();
			if (type.IsPrimitive)
			{
				return (int)retval;
			}
		}
		return 0;
	}
}

