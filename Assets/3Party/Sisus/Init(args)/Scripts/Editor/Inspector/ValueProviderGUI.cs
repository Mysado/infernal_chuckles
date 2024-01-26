using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sisus.Init.Internal;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sisus.Init.EditorOnly
{
	public sealed class ValueProviderGUI : IDisposable
	{
		private readonly Editor editor;
		private readonly GUIContent prefixLabel = new GUIContent("");
		private readonly GUIContent valueProviderLabel = new GUIContent("");
		private readonly SerializedProperty anyProperty;
		private readonly SerializedProperty referenceProperty;
		private readonly Type valueType;
		private readonly bool isControlless;
		private readonly Action onDiscardButtonPressed;

		public ValueProviderGUI(Editor editor, GUIContent prefixLabel, SerializedProperty anyProperty, SerializedProperty referenceProperty, Type valueType, Action onDiscardButtonPressed)
		{
			this.editor = editor;
			this.prefixLabel = prefixLabel;
			this.anyProperty = anyProperty;
			this.referenceProperty = referenceProperty;
			this.valueType = valueType;
			this.onDiscardButtonPressed = onDiscardButtonPressed;

			var valueProvider = editor.target;
			if(valueProvider?.GetType() is Type valueProviderType)
			{
				valueProviderLabel = new GUIContent("");

				if(valueProviderType.GetCustomAttribute<ValueProviderMenuAttribute>() is ValueProviderMenuAttribute attribute)
				{
					valueProviderLabel.text = attribute.ItemName ?? "";
					
					string tooltip = attribute.Tooltip ?? "";
					valueProviderLabel.tooltip = tooltip;
					if(prefixLabel.text.Length > 0)
					{
						prefixLabel.tooltip = tooltip;
					}
				}
				
				if(valueProviderLabel.text.Length == 0)
				{ 
					valueProviderLabel.text = ObjectNames.NicifyVariableName(valueProviderType.Name);
				}
			}
			else
			{
				valueProviderLabel = GUIContent.none;
			}

			if(editor is ValueProviderDrawer || !CustomEditorUtility.IsGenericInspectorType(editor.GetType()))
			{
				isControlless = false;
			}
			else
			{
				var firstProperty = editor.serializedObject.GetIterator();
				firstProperty.NextVisible(true);
				isControlless = !firstProperty.NextVisible(false);
			}
		}

		public void OnInspectorGUI()
		{
			if(editor is ValueProviderDrawer customDrawer)
			{
				editor.serializedObject.Update();
				GUILayout.Space(-2f);
				GUILayout.BeginHorizontal();
				customDrawer.Draw(prefixLabel, anyProperty, valueType);
				GUILayout.Space(1f);
				bool discard = GUILayout.Button(GUIContent.none, EditorStyles.label, GUILayout.Width(10f));
				var discardRect = GUILayoutUtility.GetLastRect();
				discardRect.x -= 3f;
				discardRect.y += 1f;
				discardRect.width = 15f;
				GUI.Label(discardRect, GUIContent.none, Styles.Discard);
				GUILayout.EndHorizontal();
				GUILayout.Space(2f);

				if(discard)
				{
					onDiscardButtonPressed();
				}
				else
				{
					editor.serializedObject.ApplyModifiedProperties();
				}

				return;
			}

			if(isControlless)
			{
				editor.serializedObject.Update();
				var fullRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, EditorStyles.foldout);
				var remainingRect = EditorGUI.PrefixLabel(fullRect, prefixLabel);
				remainingRect.y += 3f;
				DrawTagGUI(remainingRect);
				GUILayout.Space(2f);

				// editor can be destroyed by DrawTagGUI if the discard button is pressed
				if(editor != null)
				{
					editor.serializedObject.ApplyModifiedProperties();
				}

				return;
			}

			bool isExpanded = InternalEditorUtility.GetIsInspectorExpanded(editor);
			Rect foldoutRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, EditorStyles.foldout);
			bool setExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, prefixLabel);
			if(isExpanded != setExpanded)
			{
				InternalEditorUtility.SetIsInspectorExpanded(editor, setExpanded);
			}

			if(isExpanded)
			{
				editor.serializedObject.Update();
				EditorGUI.indentLevel++;
				editor.OnInspectorGUI();
				EditorGUI.indentLevel--;
				editor.serializedObject.ApplyModifiedProperties();
			}

			var valueProviderLabelRect = foldoutRect;
			valueProviderLabelRect.x += EditorGUIUtility.labelWidth;
			valueProviderLabelRect.width -= EditorGUIUtility.labelWidth;
			DrawTagGUI(valueProviderLabelRect);
		}

		public void DrawTagGUI(Rect valueProviderLabelRect)
		{
			int indentLevelWas = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			var valueProviderLabelClickableRect = valueProviderLabelRect;
			valueProviderLabelClickableRect.width -= EditorGUIUtility.singleLineHeight;

			float clearValueButtonWidth = EditorGUIUtility.singleLineHeight;
			valueProviderLabelRect.width = Mathf.Min(Styles.ValueProvider.CalcSize(valueProviderLabel).x + clearValueButtonWidth, valueProviderLabelRect.width);

			var clearValueButtonRect = valueProviderLabelRect;
			clearValueButtonRect.x += valueProviderLabelRect.width - clearValueButtonWidth;
			clearValueButtonRect.width = EditorGUIUtility.singleLineHeight;

			if(GUI.Button(valueProviderLabelClickableRect, GUIContent.none, EditorStyles.label))
			{
				if(clearValueButtonRect.Contains(Event.current.mousePosition))
				{
					onDiscardButtonPressed.Invoke();
				}
				else if(editor.target is IValueProvider valueProvider)
				{
					var value = valueProvider.Value as Object;
					if(value != null)
					{
						if(value is Component component)
						{
							EditorGUIUtility.PingObject(component.gameObject);
						}
						else
						{
							EditorGUIUtility.PingObject(value);
						}
					}
					else
					{
						Debug.Log($"{valueProvider.GetType().Name} could not locate value of type {TypeUtility.ToString(valueType)} at this time.", referenceProperty.serializedObject.targetObject);
					}
				}
				else if(editor.target is IValueByTypeProvider valueByTypeProvider && valueType != null)
				{
					var args = new object[] { referenceProperty.serializedObject.targetObject, null };
					bool found = (bool)valueByTypeProvider.GetType()
						.GetMethod(nameof(IValueByTypeProvider.TryGetFor))
						.MakeGenericMethod(valueType)
						.Invoke(valueByTypeProvider, args);

					if(found && args[1] is Object value && value != null)
					{
						if(value is Component component)
						{
							EditorGUIUtility.PingObject(component.gameObject);
						}
						else if(value is Object obj)
						{
							EditorGUIUtility.PingObject(value);
						}
						else if(value is IEnumerable<Object> enumerable)
						{
							switch(enumerable.Count())
							{
								case 0:
									break;
								case 1:
									EditorGUIUtility.PingObject(enumerable.First());
									break;
								default:
									if(enumerable.First() is Component)
									{
										Selection.objects = enumerable.Select(x => (x as Component)?.gameObject).ToArray();
									}
									else
									{
										Selection.objects = enumerable.ToArray();
									}
									break;
							}
						}
					}
					else
					{
						Debug.Log($"{valueByTypeProvider.GetType().Name} could not locate value of type {TypeUtility.ToString(valueType)} at this time.", referenceProperty.serializedObject.targetObject);
					}
				}
			}

			var nullGuardResult = referenceProperty.objectReferenceValue is INullGuard nullGuard ? nullGuard.EvaluateNullGuard(referenceProperty.serializedObject.targetObject as Component)
								: referenceProperty.objectReferenceValue is INullGuardByType ? (NullGuardResult)typeof(INullGuardByType).GetMethod(nameof(INullGuardByType.EvaluateNullGuard))
																							   .MakeGenericMethod(valueType)
																							   .Invoke(referenceProperty.objectReferenceValue, new object[] { referenceProperty.serializedObject.targetObject as Component })
								: NullGuardResult.ClientNotSupported;

			// Tint label green if value exists at this moment
			var backgroundColorWas = GUI.backgroundColor;
			var guiColorWas = GUI.color;
			if(nullGuardResult == NullGuardResult.Passed)
			{
				GUI.backgroundColor = new Color(1f, 1f, 0f);
			}
			else if(nullGuardResult != NullGuardResult.ValueProviderValueNullInEditMode)
			{
				GUI.backgroundColor = new Color(1f, 1f, 0.5f);
				GUI.color = new Color(1f, 0.15f, 0.15f);
			}

			GUI.Label(valueProviderLabelRect, valueProviderLabel, Styles.ValueProvider);

			GUI.backgroundColor = backgroundColorWas;
			GUI.color = guiColorWas;

			EditorGUI.indentLevel = indentLevelWas;

			if(GUI.Button(clearValueButtonRect, GUIContent.none, Styles.Discard))
			{
				onDiscardButtonPressed.Invoke();
			}
		}

		public void Dispose()
		{
			if(editor != null)
			{
				Object.DestroyImmediate(editor);
			}
		}
	}
}