using UnityEngine;
using System.Collections;
using hotpatch;

[HotPatch(PatchConstructors = true)]
public class ClassToBePatched
{
	public int intValue;
	public ClassToBePatched(int value)
	{
		intValue = value;
	}

	public void Foo()
	{
		throw new System.Exception("not patched");
	}

}
