using System;
using hotpatch;

namespace lua.test
{
	[HotPatch(PatchConstructors = true)]
	public class ToBePatched_InPlugins
	{
		public void Foo()
		{
			throw new Exception("not patched");
		}
	}
}
