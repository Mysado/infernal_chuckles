using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Sisus.Init.Internal;
using Sisus.Init.ValueProviders;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#endif

namespace Sisus.Init.EditorOnly.Internal
{
	internal sealed class AnyGUI : IDisposable
	{
		public static bool CurrentlyDrawnItemFailedNullGuard { get; private set; }

		private static int lastDraggedObjectCount = 0;

		public GUIContent label;
		public readonly SerializedProperty serializedProperty;
		public readonly SerializedProperty referenceProperty;
		public readonly Type valueType;
		public readonly bool isService;

		/// <summary>
		/// Property drawer based on attribute the field has or on field type.
		/// </summary>
		[MaybeNull]
		public readonly PropertyDrawer anyPropertyDrawer;

		private ValueProviderGUI valueProviderGUI;
		private readonly MethodInfo getHasValueMethod;
		private readonly object[] getHasValueArgs = new object[] { null, Context.MainThread };
		
		public PropertyDrawer PropertyDrawer => valueProviderGUI == null ? anyPropertyDrawer : null;

		#if ODIN_INSPECTOR
		private readonly InspectorProperty odinDrawer;
		#endif

		public bool SkipWhenDrawing
		{
			get
			{
				#if ODIN_INSPECTOR
				if(odinDrawer?.GetActiveDrawerChain()?.Current?.SkipWhenDrawing ?? false)
				{
					return true;
				}
				#endif

				return false;
			}
		}

		#if UNITY_LOCALIZATION
		private bool anyPropertyUsesCustomPropertyDrawer => referenceProperty.objectReferenceValue is Init.Internal.LocalizedString;
		#else
		private bool anyPropertyUsesCustomPropertyDrawer => false;
		#endif

		public AnyGUI(GUIContent label, SerializedProperty anyProperty, Type valueType, InitializerGUI initializerDrawer, PropertyDrawer propertyDrawer = null, Attribute[] attributes = null)
		{
			this.label = label;
			this.serializedProperty = anyProperty;
			this.valueType = valueType;
			anyPropertyDrawer = propertyDrawer;
			isService = ServiceUtility.TryGetFor(anyProperty.serializedObject.targetObject, valueType, out _, Context.MainThread);
			referenceProperty = anyProperty.FindPropertyRelative("reference");

			var anyType = anyProperty.GetValue().GetType();
			getHasValueMethod = anyType.GetMethod(nameof(Any<object>.GetHasValue));
			UpdateValueProviderEditor();

			#if ODIN_INSPECTOR
			odinDrawer = GetOdinInspectorProperty(label, anyProperty, valueType, initializerDrawer.OdinPropertyTree, attributes);

			[return: MaybeNull]
			static InspectorProperty GetOdinInspectorProperty(GUIContent label, [DisallowNull] SerializedProperty anyProperty, [DisallowNull] Type valueType, PropertyTree odinPropertyTree, [AllowNull] Attribute[] attributes)
			{
				if(attributes is null)
				{
					return null;
				}

				const int None = 0;
				const int Reference = 1;
				const int Value = 2;
				int drawAs = None;

				foreach(var attribute in attributes)
				{
					if(attribute.GetType().Namespace.StartsWith("Sirenix."))
					{
						drawAs = typeof(Object).IsAssignableFrom(valueType) || valueType.IsInterface ? Reference : Value;
						break;
					}
				}

				if(drawAs == None)
				{
					return null;
				}

				string propertyPath;
				if(drawAs == Reference)
				{
					propertyPath = anyProperty.propertyPath + "." + nameof(Any<object>.reference);
				}
				else
				{
					propertyPath = anyProperty.propertyPath + "." + nameof(Any<object>.value);
				}
				
				var result = odinPropertyTree.GetPropertyAtUnityPath(propertyPath);
				if(result is null)
				{
					#if DEV_MODE
					Debug.LogWarning($"Failed to get InspectorProperty from {odinPropertyTree.TargetType.Name} path {propertyPath}.");
					#endif
					return null;
				}

				result.Label = label;
				result.AnimateVisibility = false;
		  		return result;
			}
			#endif
		}

		public static IEnumerable<Type> GetAllInitArgMenuItemValueProviderTypes(Type valueType)
		{
			return ValueProviderEditorUtility.GetAllValueProviderMenuItemTargetTypes()
											 .Where(t => t.GetCustomAttribute<ValueProviderMenuAttribute>() is ValueProviderMenuAttribute attribute
											 && (attribute.IsAny.Length == 0 || Array.Exists(attribute.IsAny, t => t.IsAssignableFrom(valueType)))
											 && (attribute.NotAny.Length == 0 || !Array.Exists(attribute.NotAny, t => t.IsAssignableFrom(valueType)))
											 && MatchesAny(valueType, attribute.WhereAny)
											 && MatchesAll(valueType, attribute.WhereAll)
											 && (attribute.WhereNone == Is.Unconstrained || !MatchesAny(valueType, attribute.WhereNone)))
											 .OrderBy(t => t.Name);

			static bool MatchesAny(Type valueType, Is whereAny)
			{
				if(whereAny == Is.Unconstrained)
				{
					return true;
				}

				if((whereAny.HasFlag(Is.Class)			&& valueType.IsClass) ||
				   (whereAny.HasFlag(Is.ValueType)		&& valueType.IsValueType) ||
				   (whereAny.HasFlag(Is.Concrete)		&& !valueType.IsAbstract) ||
				   (whereAny.HasFlag(Is.Abstract)		&& valueType.IsAbstract) ||
				   (whereAny.HasFlag(Is.BuiltIn)		&& (valueType.IsPrimitive || valueType == typeof(string) || valueType == typeof(object))) ||
				   (whereAny.HasFlag(Is.Interface)		&& valueType.IsInterface) ||
				   (whereAny.HasFlag(Is.Component)		&& Find.typesToComponentTypes.ContainsKey(valueType)) ||
				   (whereAny.HasFlag(Is.WrappedObject)	&& Find.typeToWrapperTypes.ContainsKey(valueType)) ||
				   (whereAny.HasFlag(Is.SceneObject)	&& Find.typesToFindableTypes.ContainsKey(valueType) && (!typeof(Object).IsAssignableFrom(valueType) || typeof(Component).IsAssignableFrom(valueType) || valueType == typeof(GameObject))) ||
				   (whereAny.HasFlag(Is.Asset)			&& Find.typesToFindableTypes.ContainsKey(valueType)) ||
				   (whereAny.HasFlag(Is.Service)		&& ServiceUtility.IsServiceDefiningType(valueType)) ||
				   (whereAny.HasFlag(Is.Collection)		&& TypeUtility.IsCommonCollectionType(valueType)))
				{
					return true;
				}

				return false;
			}

			static bool MatchesAll(Type valueType, Is whereAll)
			{
				if(whereAll == Is.Unconstrained)
				{
					return true;
				}

				if(whereAll.HasFlag(Is.Collection))
				{
					if(!typeof(IEnumerable).IsAssignableFrom(valueType))
					{
						return false;
					}

					valueType = TypeUtility.GetCollectionElementType(valueType);
				}

				if((!whereAll.HasFlag(Is.Class)			|| valueType.IsClass) &&
				   (!whereAll.HasFlag(Is.ValueType)		|| valueType.IsValueType) &&
				   (!whereAll.HasFlag(Is.Concrete)		|| !valueType.IsAbstract) &&
				   (!whereAll.HasFlag(Is.Abstract)		|| valueType.IsAbstract) &&
				   (!whereAll.HasFlag(Is.Interface)		|| valueType.IsInterface) &&
				   (!whereAll.HasFlag(Is.Component)		|| Find.typesToComponentTypes.ContainsKey(valueType)) &&
				   (!whereAll.HasFlag(Is.WrappedObject)	|| Find.typeToWrapperTypes.ContainsKey(valueType)) &&
				   (!whereAll.HasFlag(Is.SceneObject)	|| Find.typesToFindableTypes.ContainsKey(valueType) && (!typeof(Object).IsAssignableFrom(valueType) || typeof(Component).IsAssignableFrom(valueType) || valueType == typeof(GameObject))) &&
				   (!whereAll.HasFlag(Is.Asset)			|| Find.typesToFindableTypes.ContainsKey(valueType)) &&
				   (!whereAll.HasFlag(Is.Service)		|| ServiceUtility.IsServiceDefiningType(valueType)))
				{
					return true;
				}

				return false;
			}
		}

		public void Refresh() => UpdateValueProviderEditor();

		private void UpdateValueProviderEditor()
		{
			if(valueProviderGUI != null)
			{
				valueProviderGUI.Dispose();
				valueProviderGUI = null;
			}

			if(referenceProperty.objectReferenceValue is ScriptableObject valueProvider
				&& valueProvider != null
				&& ValueProviderUtility.IsValueProvider(valueProvider)
				&& valueProvider.GetType().GetCustomAttribute<ValueProviderMenuAttribute>() != null
				&& !(valueProvider is _Null)
				&& (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(valueProvider))
				|| ValueProviderEditorUtility.IsSingleSharedInstance(valueProvider))
			)
			{
				Editor valueProviderEditor = Editor.CreateEditor(valueProvider, null);
				if(valueProviderEditor != null)
				{
					valueProviderGUI = new ValueProviderGUI(valueProviderEditor, label, serializedProperty, referenceProperty, valueType, DiscardObjectReferenceValue);
				}
			}
		}

		private void DiscardObjectReferenceValue()
		{
			if(valueProviderGUI != null)
			{
				valueProviderGUI.Dispose();
				valueProviderGUI = null;
			}

			if(referenceProperty.objectReferenceValue is ScriptableObject valueProvider
			&& valueProvider != null
			&& string.IsNullOrEmpty(AssetDatabase.GetAssetPath(valueProvider)))
			{
				Undo.DestroyObjectImmediate(valueProvider);
			}

			referenceProperty.objectReferenceValue = null;
			referenceProperty.serializedObject.ApplyModifiedProperties();

			GUI.changed = true;
		}

		private void HandleOpeningCustomContextMenu(Rect rightClickableRect)
		{
			if(Event.current.type == EventType.MouseDown
			&& Event.current.button == 1
			&& rightClickableRect.Contains(Event.current.mousePosition)
			&& valueProviderGUI != null)
			{
				OpenCustomContextMenu(rightClickableRect);
			}
		}

		private void OpenCustomContextMenu(Rect rect)
		{
			var menu = new GenericMenu();
			
			var valueProperty = serializedProperty.FindPropertyRelative("value");
			if(valueProperty.GetValue() is object value && !value.GetType().IsValueType)
			{
				menu.AddItem(new GUIContent("Set Null"), false, ()=>
				{
					valueProperty.SetValue(null);
					referenceProperty.objectReferenceValue = null;
				});
			}
			else if(referenceProperty.objectReferenceValue != null)
			{
				menu.AddItem(new GUIContent("Set Null"), false, ()=>referenceProperty.objectReferenceValue = null);
			}
			else if(isService)
			{
				menu.AddItem(new GUIContent("Set Null"), false, ()=>referenceProperty.objectReferenceValue = ScriptableObject.CreateInstance<_Null>());
			}

			menu.AddItem(new GUIContent("Copy Property Path"), false, ()=> GUIUtility.systemCopyBuffer = valueProperty.propertyPath.Substring(valueProperty.propertyPath.LastIndexOf('.')));

			menu.DropDown(rect);
		}

		/// <summary>
		/// Draws Init argument field of an Initializer.
		/// </summary>
		/// <param name="anyProperty"> The <see cref="Any{T}"/> type field that holds the argument. </param>
		/// <param name="label"> Label for the Init argument field. </param>
		/// <param name="customDrawer">
		/// Custom property drawer that was defined for the field via a PropertyAttribute added to a field
		/// on an Init class nested inside the Initializer.
		/// <para>
		/// <see langword="null"/> if no custom drawer has been defined, in which case <see cref="AnyPropertyDrawer"/>
		/// is used to draw the field instead.
		/// </para>
		/// </param>
		/// <param name="isService">
		/// Is the argument a service?
		/// <para>
		/// If <see langword="true"/> then the field is drawn as a service tag.
		/// </para>
		/// </param>
		/// <param name="canBeNull">
		/// Is the argument allowed to be null or not?
		/// <para>
		/// If <see langword="false"/>, then the field will be tinted red if it has a null value.
		/// </para>
		/// </param>
		public void DrawArgumentField(bool canBeNull, bool servicesShown)
		{
			// Repaint whenever dragged object references change because
			// the controls can change in reaction to objects being dragged.
			if(lastDraggedObjectCount != DragAndDrop.objectReferences.Length)
			{
				GUI.changed = true;
				lastDraggedObjectCount = DragAndDrop.objectReferences.Length;
			}

			bool hasSerializedValue;
			Object targetObject;
			var anyProperty = serializedProperty;
			object any;
			Type anyType;
			if(anyProperty != null)
			{
				any = anyProperty.GetValue();
				targetObject = anyProperty.serializedObject.targetObject;
				anyType = any.GetType();
				hasSerializedValue = (bool)anyType.GetMethod("HasSerializedValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(any, null);
			}
			else
			{
				hasSerializedValue = false;
				targetObject = null;
				any = null;
				anyType = typeof(Any<>).MakeGenericType(valueType);
			}

			if(isService && !hasSerializedValue)
			{
				if(!servicesShown)
				{
					return;
				}

				if(!IsDraggingObjectReferenceThatIsAssignableToProperty(targetObject, anyType, valueType))
				{
					var totalRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight - 2f);
					totalRect.height = EditorGUIUtility.singleLineHeight - 2f;
					totalRect.width = Screen.width - totalRect.x - 5f;
					bool clicked = ServiceTagUtility.Draw(totalRect, label, anyProperty);
					if(clicked)
					{
						GUIUtility.ExitGUI();
					}

					return;
				}
			}

			if(anyProperty == null)
			{
				GUI.color = Color.red;
				EditorGUILayout.LabelField(label.text, "NULL");
				GUI.color = Color.white;
				return;
			}

			if(valueProviderGUI != null)
			{
				valueProviderGUI.OnInspectorGUI();
				return;
			}

			#if ODIN_INSPECTOR
			if(odinDrawer != null)
			{
				DrawUsingOdin();
				return;
			}
			#endif

			var referenceProperty = serializedProperty.FindPropertyRelative("reference");
			Object referenceValueWas = referenceProperty.objectReferenceValue;

			getHasValueArgs[0] = targetObject;
			bool tintValueRed = !canBeNull && !(bool)getHasValueMethod.Invoke(any, getHasValueArgs);
			CurrentlyDrawnItemFailedNullGuard = tintValueRed;
			if(tintValueRed)
			{
				if(PropertyDrawer != null)
				{
					GUI.color = Color.red;
					DrawUsingCustomPropertyDrawer();
					GUI.color = Color.white;
				}
				// when using a custom property drawer, tint the whole thing red, since we don't know how prefab label drawing is handled.
				else if(anyPropertyUsesCustomPropertyDrawer)
				{
					GUI.color = Color.red;
					DrawUsingDefaultDrawer(label);
					GUI.color = Color.white;
				}
				// when not using a custom property drawer, we can tint only the control but not the prefix in AnyPropertyDrawer
				// based on the value of CurrentlyDrawnItemFailedNullGuard. So set GUI.color to white.
				else
				{
					GUI.color = Color.white;
					DrawUsingDefaultDrawer(label);
					GUI.color = Color.white;
				}
			}
			else if(PropertyDrawer != null)
			{
				DrawUsingCustomPropertyDrawer();
			}
			else
			{
				DrawUsingDefaultDrawer(label);
			}

			anyProperty.serializedObject.ApplyModifiedProperties();

			Object referenceValueIs = referenceProperty.objectReferenceValue;
			if(referenceValueIs != referenceValueWas)
			{
				UpdateValueProviderEditor();
			}

			CurrentlyDrawnItemFailedNullGuard = false;

			static bool IsDraggingObjectReferenceThatIsAssignableToProperty(Object targetObject, Type anyType, Type valueType)
			{
				if(DragAndDrop.objectReferences.Length == 0)
				{
					return false;
				}

				return AnyPropertyDrawer.TryGetAssignableType(DragAndDrop.objectReferences[0], targetObject, anyType, valueType, out _);
			}

			// Draw using AnyPropertyDrawer
			void DrawUsingDefaultDrawer(GUIContent label) => EditorGUILayout.PropertyField(anyProperty, label);

			void DrawUsingCustomPropertyDrawer()
			{
				SerializedProperty valueProperty;
				if(typeof(Object).IsAssignableFrom(valueType))
				{
					valueProperty = anyProperty.FindPropertyRelative("reference");
				}
				else
				{
					valueProperty = anyProperty.FindPropertyRelative("value");
					if(valueProperty == null)
					{
						valueProperty = anyProperty.FindPropertyRelative("reference");
					}
				}

				float height = PropertyDrawer.GetPropertyHeight(valueProperty, label);
				if(height < 0f)
				{
					height = EditorGUIUtility.singleLineHeight;
				}

				var rect = EditorGUILayout.GetControlRect(GUILayout.Height(height));
				PropertyDrawer.OnGUI(rect, valueProperty, label);
			}

			#if ODIN_INSPECTOR
			void DrawUsingOdin()
			{
				odinDrawer.Tree.BeginDraw(true);
				odinDrawer.Draw();
				odinDrawer.Tree.EndDraw();
			}
			#endif
		} 

		public void Dispose()
		{
			#if ODIN_INSPECTOR
			odinDrawer?.Dispose();
			#endif

			if(valueProviderGUI != null)
			{
				valueProviderGUI.Dispose();
				valueProviderGUI = null;
			}
		}
	}
}