using UnityEngine;
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

		void Reload()
		{
			initChunkLoadFailed = false;
			reason = string.Empty;

			var L = lua.luaState;

			var lb = target as LuaBehaviour;

			Api.lua_newtable(L);
			try
			{
				if (lb.IsInitFuncDumped())
				{
					Lua.LoadChunk(L, lb.GetInitChunk(), lb.scriptName + "_Init_Editor");
				}
				else
				{
					Api.luaL_requiref(L, lb.scriptName, Lua.LoadScript, 0);
					Api.lua_getfield(L, -1, "_Init");
					Api.lua_remove(L, -2); // keep _Init, remove table
				}

				Api.lua_pushvalue(L, -2);
				Lua.Call(L, 1, 0);
			}
			catch (Exception e)
			{
				Api.lua_pop(L, 1);
				initChunkLoadFailed = true;
				reason = e.Message;
			}

			if (initChunkLoadFailed) 
			{
				return;
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
			Api.lua_pop(L, 1);

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
		}

		void HandleUndoRedo()
		{
			var groupName = Undo.GetCurrentGroupName();
			Debug.Log(groupName);
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
			base.OnInspectorGUI();

			errorTextFieldStyle = new GUIStyle(EditorStyles.textField);
			errorTextFieldStyle.normal.textColor = Color.red;
			normalTextFieldStyle = new GUIStyle(EditorStyles.textField);


			OnInspectorGUI_GameObjectMap();
			

			if (initChunkLoadFailed) 
			{
				EditorGUILayout.HelpBox ("_Init function error: " + reason, MessageType.Error);
			}

			var lb = target as LuaBehaviour;

			EditorGUILayout.Separator();
			EditorGUILayout.LabelField("Init Values");
			EditorGUILayout.HelpBox("Init values are set in Script._Init function, and can be deserialized from asset.", MessageType.Info);

			if (GUILayout.Button("Reset"))
			{
				// reset original _Init function defined in script
				Undo.RecordObject(lb, "LuaBehaviour.ChangeInitChunk");
				lb.SetInitChunk(string.Empty);
				Reload();
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
					else if (type == typeof(Vector3))
					{
						values[i] = EditorGUILayout.Vector3Field(key, (Vector3)value);
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

			EditorGUILayout.HelpBox("Serialized: " + lb.GetInitChunk(), MessageType.None);
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

		void DumpInitValues()
		{
			Debug.Assert(keys != null);
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("function _Init(instance)");
			for (int i = 0; i < keys.Count; ++i)
			{
				var key = keys[i];
				var value = values[i];
				string importLiteral, typeConstructionLiteral;
				GetLuaTypeLiterial(i, value, out importLiteral, out typeConstructionLiteral);
				if (!string.IsNullOrEmpty(importLiteral))
				{
					sb.AppendLine(importLiteral);
				}
				sb.AppendLine(string.Format("instance.{0} = {1}", key, GetLuaValueLiterial(typeConstructionLiteral, value)));
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
			else if (type == typeof(Vector3))
			{
				return typeConstructionLiteral + value.ToString();
			}
			return value.ToString();
		}
	}


}
