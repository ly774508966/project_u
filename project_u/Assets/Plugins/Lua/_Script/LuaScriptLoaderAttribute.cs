using System;
using System.Reflection;
using System.Linq;

namespace lua
{
	public class LuaScriptLoaderAttribute : Attribute
	{
		public delegate byte[] ScriptLoader(string scriptName, out string scriptPath);

		public static ScriptLoader GetLoader()
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			var methods = assemblies
				.SelectMany(a => a.GetTypes())
				.SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public))
				.Where(m => m.GetCustomAttributes(typeof(LuaScriptLoaderAttribute), false).Length > 0)
				.ToArray();
			if (methods.Length > 0)
			{
				var m = methods[0];
				return (ScriptLoader)Delegate.CreateDelegate(typeof(ScriptLoader), m);
			}
			return null;
		}
	}
}
