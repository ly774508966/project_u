using UnityEditor;
using UnityEngine;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;
using System.IO;

namespace lua.hotpatch
{
	public class LuaHotPatchEditor
	{
		// http://stackoverflow.com/a/9469697/84998
		static MemberInfo MemberInfoCore(Expression body, ParameterExpression param)
		{
			if (body.NodeType == ExpressionType.MemberAccess)
			{
				var bodyMemberAccess = (MemberExpression)body;
				return bodyMemberAccess.Member;
			}
			else if (body.NodeType == ExpressionType.Call)
			{
				var bodyMemberAccess = (MethodCallExpression)body;
				return bodyMemberAccess.Method;
			}
			else throw new NotSupportedException();
		}

		static MemberInfo MemberInfo<T1>(Expression<Func<T1>> memberSelectionExpression)
		{
			if (memberSelectionExpression == null) throw new ArgumentNullException("memberSelectionExpression");
			return MemberInfoCore(memberSelectionExpression.Body, null/*param*/);
		}

		[MenuItem("Lua/Active Hot Patch", priority = 100)]
		static void ActiveHotPatch()
		{
			var hubMethod = LuaHotPatchHubAttribute.GetMethod();
			if (hubMethod == null)
			{
				Config.Log("LuaHotPatchHub not found. It should be a public static method.");
				return;
			}


			var injectingMethods = LuaHotPatchAttribute.GetMethods();

			var allTypes = AssemblyDefinition.ReadAssembly(Application.dataPath + "/../Library/ScriptAssemblies/Assembly-CSharp.dll").Modules
				.Union(AssemblyDefinition.ReadAssembly(Application.dataPath	+ "/../Library/ScriptAssemblies/Assembly-CSharp-firstpass.dll").Modules)
				.SelectMany(m => m.Types).ToArray();

			var allMethods = allTypes
				.Where(t =>	t.HasMethods)
				.SelectMany(t => t.Methods)
				.Where(m =>	m.HasCustomAttributes)
				.ToArray();

			var injectingTargets = allMethods
				.Where(m => m.CustomAttributes.FirstOrDefault(a => a.AttributeType.ToString() == typeof(LuaHotPatchAttribute).ToString()) != null)
				.ToArray();

			var getMethodFromHandleMethod = (MethodInfo)MemberInfo(() => MethodBase.GetMethodFromHandle(new RuntimeMethodHandle()));


			var pendingAssembly = new HashSet<AssemblyDefinition>();
			foreach (var m in injectingTargets)
			{
				MethodReference methodRef = null;
				foreach (var im in injectingMethods)
				{
					if (im.Name == m.Name)
					{
						methodRef = m.Module.ImportReference(im);
					}
				}
				if (methodRef == null) continue;

				var getMethodFromHandleMethodRef = m.Module.ImportReference(getMethodFromHandleMethod);
				var objectTypeRef = m.Module.ImportReference(typeof(object));
				var voidTypeRef = m.Module.ImportReference(typeof(void));


				var ilProcessor = m.Body.GetILProcessor();
				// https://msdn.microsoft.com/en-us/library/system.reflection.emit.opcodes(v=vs.110).aspx
				var hubMethodRef = m.Module.ImportReference(hubMethod);
				var isStatic = m.IsStatic;

				var continueCurrentMethod = ilProcessor.Create(OpCodes.Nop);
				var anchorToArguments = ilProcessor.Create(OpCodes.Ldnull);
				var anchorToReturn = ilProcessor.Create(OpCodes.Ret);

				// local val for ret val (last one)
				m.Body.Variables.Add(new VariableDefinition(objectTypeRef));

				var firstInstruction = ilProcessor.Create(OpCodes.Nop);
				ilProcessor.InsertBefore(m.Body.Instructions.First(), firstInstruction); // place holder

				var instructions = new[]
				{
					// http://evain.net/blog/articles/2010/05/05/parameterof-propertyof-methodof/
					ilProcessor.Create(OpCodes.Ldtoken, methodRef),
					ilProcessor.Create(OpCodes.Call, getMethodFromHandleMethodRef),
					// push	null or this
					isStatic ? ilProcessor.Create(OpCodes.Ldnull) : ilProcessor.Create(OpCodes.Ldarg_0),
					// ret value
					ilProcessor.Create(OpCodes.Ldloca_S, (byte)(m.Body.Variables.Count - 1)),
					// copy arguments to params object[]
					anchorToArguments,
					// call
					ilProcessor.Create(OpCodes.Call, hubMethodRef),
					ilProcessor.Create(OpCodes.Brfalse, continueCurrentMethod),
					// return part
					anchorToReturn,
					continueCurrentMethod
				};

				ReplaceInstruction(ilProcessor, firstInstruction, instructions);

				// process arguments
				if (m.HasParameters)
				{
					var paramsInstructions = new List<Instruction>()
					{
						ilProcessor.Create(OpCodes.Ldc_I4, m.Parameters.Count),
						ilProcessor.Create(OpCodes.Newarr, objectTypeRef)
					};

					for (int i = 0; i < m.Parameters.Count; ++i)
					{
						paramsInstructions.Add(ilProcessor.Create(OpCodes.Dup));
						paramsInstructions.Add(ilProcessor.Create(OpCodes.Ldc_I4, i));
						paramsInstructions.Add(ilProcessor.Create(OpCodes.Ldarg, i + 1));
						if (m.Parameters[i].ParameterType.IsPrimitive)
						{
							paramsInstructions.Add(ilProcessor.Create(OpCodes.Box, m.Parameters[i].ParameterType));
						}
						else
						{
							paramsInstructions.Add(ilProcessor.Create(OpCodes.Castclass, objectTypeRef));
						}
						paramsInstructions.Add(ilProcessor.Create(OpCodes.Stelem_Ref));
					}
					ReplaceInstruction(ilProcessor, anchorToArguments, paramsInstructions);
				}

				// process return
				if (m.ReturnType.FullName != voidTypeRef.FullName)
				{
					var retInstructions = new List<Instruction>();
					retInstructions.Add(ilProcessor.Create(OpCodes.Ldloc, m.Body.Variables.Count - 1));
					if (m.ReturnType.IsPrimitive)
					{
						retInstructions.Add(ilProcessor.Create(OpCodes.Unbox_Any, m.ReturnType));
					}
					else
					{
						retInstructions.Add(ilProcessor.Create(OpCodes.Castclass, m.ReturnType));
					}
					retInstructions.Add(ilProcessor.Create(OpCodes.Ret));
					ReplaceInstruction(ilProcessor, anchorToReturn, retInstructions);
				}

				pendingAssembly.Add(m.Module.Assembly);
			}

			foreach (var a in pendingAssembly)
			{
				a.Write(Application.dataPath + "/../Library/ScriptAssemblies/" + a.MainModule.Name);// + ".mod.dll");
			}

			if (pendingAssembly.Count > 0)
			{
				UnityEditorInternal.InternalEditorUtility.RequestScriptReload();
			}



		}

		static void ReplaceInstruction(ILProcessor ilProcessor, Instruction anchorInstruction, IEnumerable<Instruction> instructions)
		{
			bool firstOne = true;
			foreach (var ins in instructions)
			{
				if (firstOne)
				{
					ilProcessor.Replace(anchorInstruction, ins);
					firstOne = false;
				}
				else
				{
					ilProcessor.InsertAfter(anchorInstruction, ins);
				}
				anchorInstruction = ins;
			}
		}

		[MenuItem("Lua/Deactive Hot Patch", priority = 101)]
		static void DeactiveHotPatch()
		{
            File.Delete(Application.dataPath + "/../Library/ScriptAssemblies/Assembly-CSharp.dll");
            File.Delete(Application.dataPath + "/../Library/ScriptAssemblies/Assembly-CSharp.dll.mdb");
			File.Delete(Application.dataPath + "/../Library/ScriptAssemblies/Assembly-CSharp-firstpass.dll");
			File.Delete(Application.dataPath + "/../Library/ScriptAssemblies/Assembly-CSharp-firstpass.dll.mdb");

			File.WriteAllText(
				Application.dataPath + "/_Dev_EmptyForRefresh.cs",
				"//	generated for recompiling "	+ DateTime.Now.ToString() +	"\n");
			File.WriteAllText(
				Application.dataPath + "/Plugins/_Dev_EmptyForRefresh-firstpass.cs",
				"//	generated for recompiling "	+ DateTime.Now.ToString() +	"\n");
			AssetDatabase.Refresh();
			Debug.LogWarning("restart to make it take effect.");
		}
		[MenuItem("Lua/Reload C# Scripts", priority = 102)]
		static void Reload()
		{
			UnityEditorInternal.InternalEditorUtility.RequestScriptReload();
		}
    }
}