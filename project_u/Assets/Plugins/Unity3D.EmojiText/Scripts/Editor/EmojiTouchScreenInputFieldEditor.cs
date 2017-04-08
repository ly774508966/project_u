using UnityEngine;
using System.Collections;
using UnityEditor;

namespace ui
{
	[CustomEditor(typeof(EmojiTouchScreenInputField))]
	public class EmojiTouchScreenInputFieldEditor : Editor
	{
		SerializedProperty propHideMobileInput;
		SerializedProperty propTextComponent;
		SerializedProperty propPlaceholder;
		SerializedProperty propText;
		SerializedProperty propKeyboardType;
		SerializedProperty propOnEndEdit;

		GUIContent lbHideMobileInput;
		GUIContent lbTextComponent;
		GUIContent lbPlaceholder;
		GUIContent lbText;
		GUIContent lbKeyboardType;




		void OnEnable()
		{
			lbHideMobileInput = new GUIContent("Hide Mobile Input");
			propHideMobileInput = serializedObject.FindProperty("m_HideMobileInput");

			lbTextComponent = new GUIContent("Text Component");
			propTextComponent = serializedObject.FindProperty("m_TextComponent");

			lbPlaceholder = new GUIContent("Placeholder");
			propPlaceholder = serializedObject.FindProperty("m_Placeholder");

			lbText = new GUIContent("Text");
			propText = serializedObject.FindProperty("m_Text");

			lbKeyboardType = new GUIContent("Keyboard Type");
			propKeyboardType = serializedObject.FindProperty("m_KeyboardType");

			propOnEndEdit = serializedObject.FindProperty("onEndEdit");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUILayout.PropertyField(propHideMobileInput, lbHideMobileInput);
			EditorGUILayout.PropertyField(propTextComponent, lbTextComponent);
			EditorGUILayout.PropertyField(propPlaceholder, lbPlaceholder);
			EditorGUILayout.PropertyField(propKeyboardType, lbKeyboardType);
			EditorGUILayout.PropertyField(propOnEndEdit);
			serializedObject.ApplyModifiedProperties();
		}

	}


}
