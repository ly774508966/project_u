/*
MIT License

Copyright (c) 2016 xiaobin83

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
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

		static Dictionary<string, OpCode> _StInd = new Dictionary<string, OpCode>
		{
			{ "System.Boolean", OpCodes.Stind_I1 },
			{ "System.Byte", OpCodes.Stind_I1 },
			{ "System.SByte", OpCodes.Stind_I1 },
			{ "System.Int8", OpCodes.Stind_I1 },
			{ "System.UInt8", OpCodes.Stind_I1 },
			{ "System.Char", OpCodes.Stind_I2 },
			{ "System.Int16", OpCodes.Stind_I2 },
			{ "System.UInt16", OpCodes.Stind_I2 },
			{ "System.Int32", OpCodes.Stind_I4 },
			{ "System.UInt32", OpCodes.Stind_I4 },
			{ "System.Int64", OpCodes.Stind_I8 },
			{ "System.UInt64", OpCodes.Stind_I8 },
			{ "System.Single", OpCodes.Stind_R4 },
			{ "System.Double", OpCodes.Stind_R8 },
			{ "System.IntPtr", OpCodes.Stind_I },
			{ "System.UIntPtr", OpCodes.Stind_I },
		};
		static OpCode GetStindFromType(TypeReference type)
		{
			OpCode op;
			if (_StInd.TryGetValue(type.FullName, out op))
			{
				return op;
			}
			return OpCodes.Nop;
		}

		static Dictionary<string, OpCode> _LdInd = new Dictionary<string, OpCode>
		{
			{ "System.Boolean", OpCodes.Ldind_I1 },
			{ "System.Byte", OpCodes.Ldind_U1 },
			{ "System.SByte", OpCodes.Ldind_I1 },
			{ "System.Int8", OpCodes.Ldind_I1 },
			{ "System.UInt8", OpCodes.Ldind_U1 },
			{ "System.Char", OpCodes.Ldind_I2 },
			{ "System.Int16", OpCodes.Ldind_I2 },
			{ "System.UInt16", OpCodes.Ldind_U2 },
			{ "System.Int32", OpCodes.Ldind_I4 },
			{ "System.UInt32", OpCodes.Ldind_U4 },
			{ "System.Int64", OpCodes.Ldind_I8 },
			{ "System.UInt64", OpCodes.Ldind_I8 },
			{ "System.Single", OpCodes.Ldind_R4 },
			{ "System.Double", OpCodes.Ldind_R8 },
			{ "System.IntPtr", OpCodes.Ldind_I },
			{ "System.UIntPtr", OpCodes.Ldind_I },
		};
		static OpCode GetLdindFromType(TypeReference type)
		{
			OpCode op;
			if (_LdInd.TryGetValue(type.FullName, out op))
			{
				return op;
			}
			return OpCodes.Nop;
		}

		[MenuItem("Lua/Active Hot Patch (Mod Test)", priority = 100)]
		static void ActiveHotPatchModeTest()
		{
			ActiveHotPatch(true);
		}

		[MenuItem("Lua/Active Hot Patch", priority = 100)]
		static void ActiveHotPatch()
		{
			ActiveHotPatch(false);
		}

		static void ActiveHotPatch(bool modTest)
		{
			var allTypes = AssemblyDefinition.ReadAssembly(Application.dataPath + "/../Library/ScriptAssemblies/Assembly-CSharp.dll").Modules
				.Union(AssemblyDefinition.ReadAssembly(Application.dataPath	+ "/../Library/ScriptAssemblies/Assembly-CSharp-firstpass.dll").Modules)
				.SelectMany(m => m.Types).ToArray();

			var allMethods = allTypes
				.Where(t =>	t.HasMethods)
				.SelectMany(t => t.Methods)
				.Where(m =>	m.HasCustomAttributes)
				.ToArray();

			// find	hub	method
			var hubs = allMethods
				.Where(m =>	m.CustomAttributes.FirstOrDefault(a	=> a.AttributeType.FullName	== typeof(LuaHotPatchHubAttribute).FullName) !=	null).ToArray();

			MethodReference hubMethod = null;
			if (hubs.Length > 0)
			{
				hubMethod =  hubs[0];
			}
			if (hubMethod == null)
			{
				Config.Log("LuaHotPatchHub not found. It should be a public static method.");
				return;
			}

			var injectingTargets = allMethods
				.Where(m => m.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == typeof(LuaHotPatchAttribute).ToString()) != null)
				.ToArray();

			var getMethodFromHandleMethod = (MethodInfo)MemberInfo(() => MethodInfo.GetMethodFromHandle(new RuntimeMethodHandle()));


			var pendingAssembly = new HashSet<AssemblyDefinition>();
			foreach (var m in injectingTargets)
			{
				var signature = m.FullName;
				Config.Log(string.Format("Adding patching stub to \"{0}\"", signature));
				var getMethodFromHandleMethodRef = m.Module.ImportReference(getMethodFromHandleMethod);
				var objectTypeRef = m.Module.ImportReference(typeof(object));
				var objectArrayTypeRef = m.Module.ImportReference(typeof(object[]));
				var voidTypeRef = m.Module.ImportReference(typeof(void));

				var ilProcessor = m.Body.GetILProcessor();
				// https://msdn.microsoft.com/en-us/library/system.reflection.emit.opcodes(v=vs.110).aspx
				var hubMethodRef = m.Module.ImportReference(hubMethod);
				var isStatic = m.IsStatic;

				var continueCurrentMethod = ilProcessor.Create(OpCodes.Nop);
				var anchorToArguments = ilProcessor.Create(OpCodes.Ldnull);
				var anchorToRefOrOutArguments = ilProcessor.Create(OpCodes.Nop);
				var anchorToReturn = ilProcessor.Create(OpCodes.Ret);

				if (m.HasParameters)
				{
					// local var, argument array
					m.Body.Variables.Add(new VariableDefinition(objectArrayTypeRef));
				}
				// local val, ret val (last one)
				m.Body.Variables.Add(new VariableDefinition(objectTypeRef));

				var firstInstruction = ilProcessor.Create(OpCodes.Nop);
				ilProcessor.InsertBefore(m.Body.Instructions.First(), firstInstruction); // place holder

				var instructions = new[]
				{
					ilProcessor.Create(OpCodes.Ldstr, signature),
					// http://evain.net/blog/articles/2010/05/05/parameterof-propertyof-methodof/
					ilProcessor.Create(OpCodes.Ldtoken, m),
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
					// ref/out params
					anchorToRefOrOutArguments,
					// return part
					anchorToReturn,
					continueCurrentMethod
				};

				ReplaceInstruction(ilProcessor, firstInstruction, instructions);

				var paramStart = 0;
				if (!isStatic)
				{
					paramStart = 1;
				}

				// process arguments
				bool hasRefOrOutParameter = false;
				if (m.HasParameters)
				{
					var paramsInstructions = new List<Instruction>()
					{
						ilProcessor.Create(OpCodes.Ldc_I4, m.Parameters.Count),
						ilProcessor.Create(OpCodes.Newarr, objectTypeRef),
						ilProcessor.Create(OpCodes.Dup),
						ilProcessor.Create(OpCodes.Stloc, m.Body.Variables.Count - 2)
					};

					for (int i = 0; i < m.Parameters.Count; ++i)
					{
						var param = m.Parameters[i];
						if (param.IsOut)
						{
							// placeholder for outs
							hasRefOrOutParameter = true;
						}
						else
						{
							paramsInstructions.Add(ilProcessor.Create(OpCodes.Dup));
							paramsInstructions.Add(ilProcessor.Create(OpCodes.Ldc_I4, i));
							paramsInstructions.Add(ilProcessor.Create(OpCodes.Ldarg, i + paramStart));
							if (param.ParameterType.IsByReference)
							{
								hasRefOrOutParameter = true;

								var elemType = param.ParameterType.GetElementType();

								if (elemType.IsPrimitive)
								{
									var op = GetLdindFromType(elemType);
									if (op != OpCodes.Nop)
									{
										paramsInstructions.Add(ilProcessor.Create(op));
									}
									else
									{
										paramsInstructions.Add(ilProcessor.Create(OpCodes.Ldobj, elemType));
									}
								}
								else if (elemType.IsValueType)
								{
									paramsInstructions.Add(ilProcessor.Create(OpCodes.Ldobj, elemType));
								}
								else
								{
									paramsInstructions.Add(ilProcessor.Create(OpCodes.Ldind_Ref));
								}

								if (elemType.IsValueType)
								{
									paramsInstructions.Add(ilProcessor.Create(OpCodes.Box, elemType));
								}

								paramsInstructions.Add(ilProcessor.Create(OpCodes.Stelem_Ref));
							}
							else
							{
								if (param.ParameterType.IsPrimitive)
								{
									paramsInstructions.Add(ilProcessor.Create(OpCodes.Box, param.ParameterType));
								}
								paramsInstructions.Add(ilProcessor.Create(OpCodes.Stelem_Ref));
							}
						}
					}
					ReplaceInstruction(ilProcessor, anchorToArguments, paramsInstructions);
				}

				if (hasRefOrOutParameter)
				{
					var refOutInstructions = new List<Instruction>();
					for (int i = 0; i < m.Parameters.Count; ++i)
					{
						var param = m.Parameters[i];
						if (param.IsOut || param.ParameterType.IsByReference)
						{
							// ith_refOutArg = arg[i]


							// ith_refOutArg
							refOutInstructions.Add(ilProcessor.Create(OpCodes.Ldarg, i + paramStart));

							// arg
							refOutInstructions.Add(ilProcessor.Create(OpCodes.Ldloc, m.Body.Variables.Count - 2));

							// arg[i]
							refOutInstructions.Add(ilProcessor.Create(OpCodes.Ldc_I4, i));

							// (type)arg[i]
							refOutInstructions.Add(ilProcessor.Create(OpCodes.Ldelem_Ref));
							TypeReference elemType = param.ParameterType.GetElementType();
							if (elemType.IsValueType)
							{
								refOutInstructions.Add(ilProcessor.Create(OpCodes.Unbox_Any, elemType));
							}
							if (elemType.IsPrimitive)
							{
								var op = GetStindFromType(elemType);
								if (op != OpCodes.Nop)
								{
									refOutInstructions.Add(ilProcessor.Create(op));
								}
								else
								{
									refOutInstructions.Add(ilProcessor.Create(OpCodes.Stobj, elemType)); // just in case
								}
							}
							else if (elemType.IsValueType)
							{
								refOutInstructions.Add(ilProcessor.Create(OpCodes.Stobj, elemType));
							}
							else
							{
								refOutInstructions.Add(ilProcessor.Create(OpCodes.Castclass, elemType));
								// ith_refOutArg = (type)arg[i]
								refOutInstructions.Add(ilProcessor.Create(OpCodes.Stind_Ref));
							}

						}
					}
					ReplaceInstruction(ilProcessor, anchorToRefOrOutArguments, refOutInstructions);
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
				a.Write(Application.dataPath + "/../Library/ScriptAssemblies/" + a.MainModule.Name + (modTest ? ".mod.dll" : ""));
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
			AssetDatabase.Refresh();

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