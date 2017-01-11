using UnityEditor;
using UnityEngine;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;

namespace lua.hotpatch
{
	public class LuaHotPatchEditor
	{


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

		[MenuItem("Lua/Active Hot Patch")]
		static void ActiveHotPatch()
		{
			var hubMethod = LuaHotPatchHubAttribute.GetMethod();
			if (hubMethod == null) return;

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

			var getTypeFromHandleMethodInfo = (MethodInfo)MemberInfo(() => Type.GetTypeFromHandle(new RuntimeTypeHandle()));
			var createInstanceMethodInfo = (MethodInfo)MemberInfo(() => Activator.CreateInstance(typeof(int)));


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

				var getTypeFromHandleMethodRef = m.Module.ImportReference(getTypeFromHandleMethodInfo);
//				var createInstanceMethodRef = m.Module.ImportReference(createInstanceMethodInfo);

				var ilProcessor = m.Body.GetILProcessor();
				// https://msdn.microsoft.com/en-us/library/system.reflection.emit.opcodes(v=vs.110).aspx
				// https://stackoverflow.com/questions/9623797/c-sharp-call-and-return-an-object-from-a-static-method-in-il
				var hubMethodRef = m.Module.ImportReference(hubMethod);

				var instructions = new List<Instruction>
				{
//					ilProcessor.Create(OpCodes.Nop),
//					ilProcessor.Create(OpCodes.Ldtoken, methodRef),
//					ilProcessor.Create(OpCodes.Pop),
					ilProcessor.Create(OpCodes.Ldc_I4_0),
//					ilProcessor.Create(OpCodes.Box, Type),
//					ilProcessor.Create(OpCodes.Call, getTypeFromHandleMethodRef),
//					ilProcessor.Create(OpCodes.Call, createInstanceMethodRef),
                    ilProcessor.Create(OpCodes.Call, hubMethodRef)
				};
				for (int i = instructions.Count - 1; i >= 0; --i)
				{
					ilProcessor.InsertBefore(m.Body.Instructions.First(), instructions[i]);
				}

/*

				if (m.HasParameters)
				{
					// loads all parameters	onto stack
					for (int i = 0; i < m.Parameters.Count; ++i)
					{
						ilProcessor.InsertBefore(m.Body.Instructions[0], ilProcessor.Create(OpCodes.Call, hubMethod));
					}
				}
*/

				pendingAssembly.Add(m.Module.Assembly);
			}

			foreach (var a in pendingAssembly)
			{
				a.Write(Application.dataPath + "/../Library/ScriptAssemblies/" + a.MainModule.Name + ".mod.dll");
			}

			if (pendingAssembly.Count > 0)
			{
				UnityEditorInternal.InternalEditorUtility.RequestScriptReload();
			}








		}
	}
}