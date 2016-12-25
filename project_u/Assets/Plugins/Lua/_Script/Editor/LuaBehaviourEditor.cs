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
﻿using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace lua
{
	[CustomEditor(typeof(lua.LuaBehaviour))]
	public class LuaBehaviourEditor : Editor
	{

		static Lua lua = new Lua();

		bool initChunkLoadFailed = false;
		string reason;

		bool editSerializedChunk;

		List<string> keys;
		List<object> values;

		const string mergeFunction =
			"function merge(orig, cur)\n" +
			// "  Debug = csharp.import('UnityEngine.Debug, UnityEngine')\n" + 
			"  merged = {}\n" +
			"  for k, v in pairs(orig) do\n" +
			// "    Debug.Log(k)\n" +
			"    if cur[k] then\n" +
			"      merged[k] = cur[k]\n" +
			"    else\n" +
			"      merged[k] = v\n" +
			"    end\n" +
			"  end\n" +
			"  return merged\n" +
			"end\n";

		void Reload()
		{
			var lb = target as LuaBehaviour;
			if (string.IsNullOrEmpty(lb.scriptName))
				return;

			initChunkLoadFailed = false;
			reason = string.Empty;

			var L = lua.luaState;


			// load from script
			Api.lua_pushcclosure(L, Lua.LoadScript, 0);
			Api.lua_pushstring(L, lb.scriptName);
			try 
			{
				Lua.Call(L, 1, 1);
			}
			catch (Exception e)
			{
				initChunkLoadFailed = true;
				reason = e.Message;	
				return;
			}

			var luaType = Api.lua_getfield(L, -1, "_Init");
			Api.lua_remove(L, -2); // keep _Init, remove table
			if (luaType != Api.LUA_TFUNCTION)
			{
				Api.lua_pop(L, 1); // pop func
				lb.SetInitChunk(string.Empty); // no _Init chunk anymore
				serializedObject.Update();
				return;
			}

			// stack: func

			// call Script._Init on instance
			Api.lua_newtable(L); // oringal table
			// stack: func, table
			Api.lua_pushvalue(L, -1); // push again, stack: func, table, table
			var refToOriginal = Api.luaL_ref(L, Api.LUA_REGISTRYINDEX); // stack: func, table
			try
			{
				Lua.Call(L, 1, 0);
			}
			catch (Exception e)
			{
				Api.luaL_unref(L, Api.LUA_REGISTRYINDEX, refToOriginal);
				initChunkLoadFailed = true;
				reason = e.Message;
				return;
			}

			// stack: *empty*

			if (lb.IsInitFuncDumped())
			{
				Api.lua_newtable(L); // new table for loading valued from dumpped _Init function

				Lua.LoadChunk(L, lb.GetInitChunk(), lb.scriptName + "_Init_Editor");
				// stack: table, func
				Api.lua_pushvalue(L, -2);
				// stack: table, func, table

				try
				{
					Lua.Call(L, 1, 0);
				}
				catch (Exception e)
				{
					Api.lua_pop(L, 1); // pop table
					Api.luaL_unref(L, Api.LUA_REGISTRYINDEX, refToOriginal);
					initChunkLoadFailed = true;
					reason = e.Message;
					return;	
				}

				// stack: table

				// merge two tables
				// -1: deserialized values

				Api.luaL_dostring(L, mergeFunction);
				luaType = Api.lua_getglobal(L, "merge");
				// stack: table, func
				Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, refToOriginal);
				// stack: table, func, original table
				Api.lua_pushvalue(L, -3);
				// stack: table, func, original table, table
				Api.lua_remove(L, -4);
				// stack: func, original table, table

				try
				{
					Lua.Call(L, 2, 1);
				}
				catch (Exception e)
				{
					Api.luaL_unref(L, Api.LUA_REGISTRYINDEX, refToOriginal);
					initChunkLoadFailed = true;
					reason = e.Message;					
					return;
				}

				// stack: merged table
			}
			else
			{
				Api.lua_rawgeti(L, Api.LUA_REGISTRYINDEX, refToOriginal);
				Api.luaL_unref(L, Api.LUA_REGISTRYINDEX, refToOriginal);
				// stack: orignal table
			}


			// prepare to show on Inspector
			keys = new List<string>();
			values = new List<object>();
			// iterate table on stack
			Api.lua_pushnil(L);
			while (Api.lua_next(L, -2) != 0)
			{
				var key = Api.lua_tostring(L, -2);
				var value = Lua.CsharpValueFrom(L, -1);
				keys.Add(key);
				values.Add(value);
				Api.lua_pop(L, 1);
			}
			Api.lua_pop(L, 1); // pop table
			// stack: *empty*

			int[] sortedIndex = new int[keys.Count];
			for (int i = 0; i < sortedIndex.Length; ++i)
			{
				sortedIndex[i] = i;
			}
			System.Array.Sort(sortedIndex, (a, b) => keys[a].CompareTo(keys[b]));
			keys.Sort();
			var newValues = new List<object>();
			for (int i = 0; i < values.Count; ++i)
			{
				newValues.Add(values[sortedIndex[i]]);
			}
			values = newValues;

			if (lb.IsInitFuncDumped())
			{
				// dump again, in case of Script._Init and Behaviour._Init being merged.
				DumpInitValues(); 
			}
		}

		void HandleUndoRedo()
		{
			var groupName = Undo.GetCurrentGroupName();
			// Debug.Log(groupName);
			if (!string.IsNullOrEmpty(groupName) && groupName.StartsWith("LuaBehaviour."))
			{
				Reload();
				Repaint();
			}
		}


		GUIStyle errorTextFieldStyle, normalTextFieldStyle;
		void OnEnable()
		{
			Undo.undoRedoPerformed += HandleUndoRedo;
			Reload();
		}

		void OnDisable()
		{
			Undo.undoRedoPerformed -= HandleUndoRedo;
		}

		// if _Init func changed and also dumped, merge this two parts and dump again
		void OnInspectorGUI_CheckReimported()
		{
			var lb = target as LuaBehaviour;
			var path = Lua.GetScriptPath(lb.scriptName);
			if (WatchingLuaSources.IsReimported(path))
			{
				Reload();
				WatchingLuaSources.SetProcessed(path);
			}
		}


		HashSet<string> gameObjectNames = new HashSet<string>();
		void OnInspectorGUI_GameObjectMap()
		{
			gameObjectNames.Clear();

			var serializedKeys = serializedObject.FindProperty("keys");
			var serializedGameObjects = serializedObject.FindProperty("gameObjects");
			Debug.Assert(serializedKeys.arraySize == serializedGameObjects.arraySize);

			EditorGUILayout.BeginVertical();
			var idToDelete = -1;
			bool hasDuplicatedName = false;
			for (int i = 0; i < serializedKeys.arraySize; ++i)
			{
				var propKey = serializedKeys.GetArrayElementAtIndex(i);
				var propGameObject = serializedGameObjects.GetArrayElementAtIndex(i);

				EditorGUILayout.BeginHorizontal();

				var nameDuplicated = !gameObjectNames.Add(propKey.stringValue);
				hasDuplicatedName = hasDuplicatedName || nameDuplicated;

				propKey.stringValue = 
					EditorGUILayout.TextField(
						i.ToString() + ".", 
						propKey.stringValue, 
						nameDuplicated ? errorTextFieldStyle : normalTextFieldStyle);
				propGameObject.objectReferenceValue = 
					EditorGUILayout.ObjectField(
						propGameObject.objectReferenceValue,
						typeof(GameObject),
						allowSceneObjects: true);
				if (GUILayout.Button("X"))
				{
					idToDelete = i;
				}
				EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndVertical();

			if (hasDuplicatedName)
			{
				EditorGUILayout.HelpBox(
					"Duplicated name of attached GameObject found! GameObject may not be found correctly in script!",
					MessageType.Error);
			}

			if (idToDelete != -1)
			{
				Undo.RecordObject(target, "LuaBehaviour.RemoveGameObject");
				serializedKeys.DeleteArrayElementAtIndex(idToDelete);
				serializedGameObjects.DeleteArrayElementAtIndex(idToDelete);
			}

			if (GUILayout.Button("Attach New GameObject"))
			{
				Undo.RecordObject(target, "LuaBehaviour.RemoveGameObject");
				++serializedKeys.arraySize;
				++serializedGameObjects.arraySize;
			}

			serializedObject.ApplyModifiedProperties();
		}

		public override void OnInspectorGUI()
		{
			errorTextFieldStyle = new GUIStyle(EditorStyles.textField);
			errorTextFieldStyle.normal.textColor = Color.red;
			normalTextFieldStyle = new GUIStyle(EditorStyles.textField);

			var lb = target as LuaBehaviour;

			EditorGUI.BeginChangeCheck();
			lb.scriptName = EditorGUILayout.TextField("script", lb.scriptName);
			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
				Reload();
				serializedObject.Update();
			}


			OnInspectorGUI_CheckReimported();
			OnInspectorGUI_GameObjectMap();
			

			if (initChunkLoadFailed) 
			{
				EditorGUILayout.HelpBox ("_Init function error: " + reason, MessageType.Error);
			}

	
	

			EditorGUILayout.Separator();
			EditorGUILayout.LabelField("Init Values");
			EditorGUILayout.HelpBox(string.Format("Init values can be specified in _Init function of script {0}.lua.", lb.scriptName), MessageType.Info);

			if (lb.IsInitFuncDumped())
			{
				if (GUILayout.Button("Reset"))
				{
					// reset original _Init function defined in script
					Undo.RecordObject(lb, "LuaBehaviour.ChangeInitChunk");
					lb.SetInitChunk(string.Empty);
					Reload();
				}
			}

			if (initChunkLoadFailed) 
				return;

			if (keys == null)
				return;

			EditorGUI.BeginChangeCheck();

			for (int i = 0; i < keys.Count; ++i)
			{
				var key = keys[i];
				var value = values[i];
				EditorGUILayout.BeginHorizontal();
				{
					var type = value.GetType();
					if (type == typeof(System.Double))
					{
						values[i] = EditorGUILayout.DoubleField(key, (double)value);
					}
					else if (type == typeof(string))
					{
						values[i] = EditorGUILayout.TextField(key, (string)value);
					}
					else if (type == typeof(Vector4))
					{
						values[i] = EditorGUILayout.Vector4Field(key, (Vector4)value);
					}
					else if (type == typeof(Vector3))
					{
						values[i] = EditorGUILayout.Vector3Field(key, (Vector3)value);
					}
					else if (type == typeof(Vector2))
					{
						values[i] = EditorGUILayout.Vector2Field(key, (Vector2)value);
					}
					else if (type == typeof(Color))
					{
						values[i] = EditorGUILayout.ColorField(key, (Color)value);
					}
					else
					{
						EditorGUILayout.LabelField(string.Format("not support type {0} with key {1}", type, key));
					}
				}
				EditorGUILayout.EndHorizontal();
			}
			if (EditorGUI.EndChangeCheck())
			{
				DumpInitValues();
			}

			if (lb.IsInitFuncDumped())
			{
				// EditorGUILayout.HelpBox("Serialized: " + lb.GetInitChunk(), MessageType.None);
				EditorGUILayout.HelpBox("Serialized: " + lb.GetInitChunk().Length + " bytes dumped.", MessageType.None);
				editSerializedChunk = EditorGUILayout.Toggle("Edit Serialized Chunk", editSerializedChunk);
				if (editSerializedChunk)
				{
					var original = lb.GetInitChunk();
					var changed = EditorGUILayout.TextArea(original);
					if (changed != lb.GetInitChunk())
					{
						Undo.RecordObject(lb, "LuaBehaviour.ChangeInitChunk");
						lb.SetInitChunk(changed.Trim());
						Reload();
					}
				}
			}
		}

		struct Pair<T1, T2>
		{
			public T1 p0;
			public T1 p1;
		}

		void DumpInitValues()
		{
			Debug.Assert(keys != null);
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("function _Init(instance)");
			var importedTypes = new Dictionary<Type, Pair<string, string>>();
			for (int i = 0; i < keys.Count; ++i)
			{
				var key = keys[i];
				var value = values[i];
				Pair<string, string> literials;
				if (!importedTypes.TryGetValue(value.GetType(), out literials))
				{
					GetLuaTypeLiterial(i, value, out literials.p0, out literials.p1);
					importedTypes.Add(value.GetType(), literials);
					if (!string.IsNullOrEmpty(literials.p0))
					{						
						sb.AppendLine(literials.p0);
					}
				}
				sb.AppendLine(string.Format("instance.{0} = {1}", key, GetLuaValueLiterial(literials.p1, value)));
			}
			sb.AppendLine("end");

			var chunk = sb.ToString();
			// Debug.Log(chunk);

			try
			{
				Api.luaL_dostring(lua.luaState, chunk);
				Api.lua_getglobal(lua.luaState, "_Init");
				var dumped = Lua.DumpChunk(lua.luaState);
				var lb = target as LuaBehaviour;
				Undo.RecordObject(lb, "LuaBehaviour.ChangeInitValues");
				lb.SetInitChunk(dumped);
				serializedObject.Update();
				Api.lua_pop(lua.luaState, 1);
			}
			catch (Exception e)
			{
				Debug.LogError(e.Message);
			}
		}


		void GetLuaTypeLiterial(int idx, object value, out string importLiteral, out string typeConstructionLiteral)
		{
			importLiteral = string.Empty;
			typeConstructionLiteral = string.Empty;
			var type = value.GetType();
			if (!type.IsPrimitive)
			{
				if (type != typeof(string))
				{
					var typeLiteral = "_" + idx;
					importLiteral = string.Format("local {0} = csharp.import('{1}')", typeLiteral, type.AssemblyQualifiedName);
					typeConstructionLiteral = typeLiteral;
				}
			}
		}

		string GetLuaValueLiterial(string typeConstructionLiteral, object value)
		{
			var type = value.GetType();
			if (type == typeof(string))
			{
				return string.Format("'{0}'", ((string)value).Replace("'", "\\'")); // escape '
			}
			else if (type == typeof(Vector4)
			         || type == typeof(Vector3)
			         || type == typeof(Vector2))
			{
				return typeConstructionLiteral + value.ToString();
			}
			else if (type == typeof(Color))
			{
				var c = (Color)value;
				return string.Format("{0}({1}, {2}, {3}, {4})", 
					typeConstructionLiteral,
					c.r, c.g, c.b, c.a);
			}
			return value.ToString();
		}
	}


}
