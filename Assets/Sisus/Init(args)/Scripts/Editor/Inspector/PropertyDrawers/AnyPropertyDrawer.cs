using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sisus.Init.Internal;
using Sisus.Init.Serialization;
using Sisus.Init.ValueProviders;
using Sisus.Shared.EditorOnly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Sisus.Init.EditorOnly.Internal
{
	/// <summary>
	/// Custom property drawer for <see cref="Any{T}"/> and <see cref="AnyGeneric{T}"/>,
	/// that allows assigning any value to the property.
	/// </summary>
	[CustomPropertyDrawer(typeof(IAny), useForChildren: true)]
	//#endif
    internal sealed class AnyPropertyDrawer : PropertyDrawer, IDisposable
    {
		private sealed class State
		{
			public readonly Type anyType;
			public readonly Type valueType;
			public readonly IEqualityComparer equalityComparer;
			public readonly bool valueHasChildProperties;
			public readonly bool canBeUnityObject;
			public readonly bool canBeNonUnityObject;

			public Type objectFieldType;
			public bool isService;
			public bool? draggedObjectIsAssignable;

			/// <summary>
			/// Should the <see cref="Any{T}"/> field be drawn as an Object reference field at this moment?
			/// <para>
			/// True if field currently holds an Object reference value, if only Object reference values can be assigned to the field,
			/// or if the user has selected "Object" from the type dropdown of an interface or object field.
			/// </para>
			/// </summary>
			public bool drawObjectField;
			public bool drawNullOption;
			public bool drawTypeDropdownButton;
			public bool drawDiscardButton;
			public SerializedProperty anyProperty;
			public SerializedProperty referenceProperty;
			public SerializedProperty valueProperty;
			public TypeDropdownButton typeDropdownButton;
			public DiscardButton discardValueButton;
			public object valueLastFrame;

			public State(SerializedProperty anyProperty, FieldInfo fieldInfo)
			{
				this.anyProperty = anyProperty;
				referenceProperty = anyProperty.FindPropertyRelative(nameof(Any<object>.reference));
				valueProperty = anyProperty.FindPropertyRelative(nameof(Any<object>.value));
				bool valuePropertyIsNull = valueProperty is null;
			
				anyType = GetAnyTypeFromField(fieldInfo);
				valueType = GetValueTypeFromAnyType(anyType);
				objectFieldType = typeof(Object);
				
				isService = IsService(anyProperty.serializedObject.targetObject, valueType);
				equalityComparer = typeof(EqualityComparer<>).MakeGenericType(valueType).GetProperty(nameof(EqualityComparer<object>.Default), BindingFlags.Static | BindingFlags.Public).GetValue(null, null) as IEqualityComparer;

				#if DEV_MODE && DEBUG_ENABLED
				Debug.Log($"{anyProperty.propertyPath} value SerializedProperty is null. Probably because Unity can't serialize field of type {valueType} with the SerializeReferenceAttribute.");
				#endif

				valueHasChildProperties = !valuePropertyIsNull && (valueProperty.hasChildren && valueProperty.propertyType != SerializedPropertyType.String);
				canBeUnityObject = CanAssignUnityObjectToField(valueType);
				canBeNonUnityObject = !valuePropertyIsNull && CanAssignNonUnityObjectToField(valueType);

				Update(true);
			}

			public void Update(bool forceDeepUpdate = false)
			{
				if(referenceProperty.objectReferenceValue is _Null nullObject && nullObject != null && valueProperty.propertyType == SerializedPropertyType.ManagedReference)
				{
					valueProperty.SetValue(null);
				}

				bool setIsService = IsService(anyProperty.serializedObject.targetObject, valueType);
				bool shouldRebuildButtons = forceDeepUpdate;

				if(setIsService != isService)
				{
					isService = setIsService;
					shouldRebuildButtons = true;
				}
				
				drawObjectField = GetShouldDrawObjectField();
				drawNullOption = !isService && !valueType.IsValueType && ((canBeUnityObject && canBeNonUnityObject) || TryGetValue(out _, out _) == -1);

				if(shouldRebuildButtons)
				{
					RebuildDefiningTypeAndDiscardButtons(out drawTypeDropdownButton, out drawDiscardButton);
					#if DEV_MODE && DEBUG_ENABLED
					Debug.Log($"{valueType.Name}.drawTypeDropdownButton:{drawTypeDropdownButton}, propertyType:{valueProperty.propertyType}, drawObjectField:{drawObjectField}");
					#endif
				}

				bool dragging = DragAndDrop.objectReferences.Length > 0;
				if(dragging && TryGetAssignableType(DragAndDrop.objectReferences[0], referenceProperty.serializedObject.targetObject, anyType, valueType, out Type assignableType))
				{
					draggedObjectIsAssignable = true;
					objectFieldType = assignableType;
				}
				else
				{
					draggedObjectIsAssignable = dragging ? false : default(bool?);
					var objectReferenceValue = referenceProperty.objectReferenceValue;
					bool hasObjectReferenceValue = objectReferenceValue != null;
					var objectReferenceValueType = hasObjectReferenceValue ? objectReferenceValue.GetType() : null;

					if(typeof(Object).IsAssignableFrom(valueType) || valueType.IsInterface)
					{
						objectFieldType = !hasObjectReferenceValue || valueType.IsAssignableFrom(objectReferenceValueType) ? valueType : typeof(Object);
					}
					else
					{
						var valueProviderType = typeof(IValueProvider<>).MakeGenericType(valueType);
						objectFieldType = !hasObjectReferenceValue || valueProviderType.IsAssignableFrom(objectReferenceValueType) ? valueProviderType : typeof(Object);
					}
				}

				valueLastFrame = TryGetValue(out object value, out Object reference) switch
				{
					0 => value,
					1 => reference,
					_ => null
				};

				bool GetShouldDrawObjectField()
				{
					if(referenceProperty.objectReferenceValue != null)
					{
						return true;
					}

					if(isService)
					{
						return false;
					}

					if(!canBeNonUnityObject)
					{
						return true;
					}

					if(valueProperty is null || valueProperty.propertyType == SerializedPropertyType.ObjectReference)
					{
						return true;
					}
					
					if(valueProperty.GetValue() != null)
					{
						return false;
					}

					if(canBeUnityObject)
					{
						return true;
					}

					return false;
				}
			}

			/// <returns> <see langword="false"/> if menu has no items. </returns>
			private void RebuildDefiningTypeAndDiscardButtons(out bool drawTypeDropdownButton, out bool drawDiscardButton)
			{
				var typeOptions = GetTypeOptions();
				var valueProviderOptions = AnyGUI.GetAllInitArgMenuItemValueProviderTypes(valueType);
				bool hasMoreThanOneNonValueProviderOption = typeOptions.Skip(1).Any();

				if(valueProviderOptions.Any())
				{
					if(typeOptions.Any())
					{
						typeOptions = typeOptions.Append(typeof(Separator));
					}

					typeOptions = typeOptions.Concat(valueProviderOptions);
				}
				else if(!hasMoreThanOneNonValueProviderOption)
				{
					typeDropdownButton = null;
					drawTypeDropdownButton = false;
					drawDiscardButton = false;
					return;
				}

				var prefixLabel = GUIContent.none;
				var value = valueProperty?.GetValue();
				bool isNull = value is null;
				var instanceType = !isNull ? value.GetType()
								 : typeof(Object).IsAssignableFrom(valueType) ? valueType
								 : null;

				string buttonText = GetItemContent(instanceType).fullPath;
				int buttonTextLastPartStart = buttonText.LastIndexOf('/');
				if(buttonTextLastPartStart != -1)
				{
					buttonText = buttonText.Substring(buttonTextLastPartStart + 1);
				}

				IEnumerable<Type> selectedTypes = Enumerable.Repeat(instanceType, 1);
				const string menuTitle = "Select Value";

				if(!hasMoreThanOneNonValueProviderOption || (isNull && drawObjectField))
				{
					typeDropdownButton = new TypeDropdownButton(GUIContent.none, GUIContent.none, typeOptions, selectedTypes, OnSelectedItemChanged, menuTitle, GetItemContent);
					discardValueButton = null;
					drawTypeDropdownButton = true;
					drawDiscardButton = false;
					return;
				}

				if(isNull)
				{
					typeDropdownButton = new TypeDropdownButton(prefixLabel, new GUIContent(buttonText), typeOptions, selectedTypes, OnSelectedItemChanged, menuTitle, GetItemContent);
					discardValueButton = null;
					drawTypeDropdownButton = true;
					drawDiscardButton = false;
					return;
				}
				
				drawTypeDropdownButton = false;
				drawDiscardButton = true;
				typeDropdownButton = null;
				discardValueButton = new DiscardButton($"Click to discard value of type {(TypeUtility.ToString(value.GetType()))}.", () => OnSelectedItemChanged(null));

				void OnSelectedItemChanged(Type setType)
				{
					Undo.RecordObjects(anyProperty.serializedObject.targetObjects, "Set Defining Type");
					SetUserSelectedValueType(setType);
				}

				(string fullPath, Texture icon) GetItemContent(Type type)
				{
					const string noneLabel = "None";

					if(type is null)
					{
						var nullLabel = isService ? ServiceLabel : noneLabel;
						return (nullLabel, null);
					}

					if(type == typeof(_Null))
					{
						var nullLabel = noneLabel;
						return (nullLabel, null);
					}

					if(type == typeof(Object))
					{
						if(type == valueType)
						{
							return ("Object", null);
						}

						return ("Reference", null);
					}

					if(ValueProviderUtility.IsValueProvider(type)
					&& typeof(Object).IsAssignableFrom(type)
					&& type.GetCustomAttribute<ValueProviderMenuAttribute>() is ValueProviderMenuAttribute attribute)
					{ 
						string itemName = !string.IsNullOrEmpty(attribute.ItemName)
										? attribute.ItemName
										: ObjectNames.NicifyVariableName(TypeUtility.ToString(type));

						return (itemName, null);
					}

					return (ObjectNames.NicifyVariableName(TypeUtility.ToString(type)), null);
				}
			}

			private void SetUserSelectedValueType(Type setType)
			{
				if(setType is null)
				{
					valueProperty.managedReferenceValue = null;
					referenceProperty.objectReferenceValue = null;
				}
				else if(typeof(Object).IsAssignableFrom(setType))
				{
					if(valueProperty.propertyType == SerializedPropertyType.ManagedReference)
					{
						valueProperty.managedReferenceValue = null;
					}

					if(setType == typeof(Object))
					{
						if(isService/* || drawNullOption*/)
						{
							var fakeNull = ScriptableObject.CreateInstance<_Null>();
							referenceProperty.objectReferenceValue = fakeNull;
						}
						else if(setType == typeof(Object) && valueType != typeof(Object))
						{
							referenceProperty.objectReferenceValue = null;
						}
					}
					else if(setType.GetCustomAttribute<ValueProviderMenuAttribute>() is ValueProviderMenuAttribute attribute
						 && typeof(ScriptableObject).IsAssignableFrom(setType))
					{
						var instancesInProject = AssetDatabase.FindAssets("t:" + setType.Name)
													.Select(guid => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)))
													.Where(obj => obj.GetType() == setType && obj.name == attribute.ItemName)
													.ToList();

						// if exactly a single asset of the given type exists in the project,
						// and said asset has no serialized fields, then reuse that instance
						// across all clients (the flyweight pattern).
						bool useSingleSharedInstance = false;
						if(instancesInProject.Count == 1)
						{
							using(var serializedObject = new SerializedObject(instancesInProject[0]))
							{
								var firstProperty = serializedObject.GetIterator();
								if(firstProperty.NextVisible(true) && !firstProperty.NextVisible(false))
								{
									useSingleSharedInstance = true;

									#if DEV_MODE
									Debug.Log($"Use single shared instance: {instancesInProject[0]}", instancesInProject[0]);
									#endif
								}
							}
						}

						#if DEV_MODE
						if(!useSingleSharedInstance) Debug.Log($"Creating new instance of type {setType.Name}. Found assets: {instancesInProject.Count}.");
						#endif

						referenceProperty.objectReferenceValue = useSingleSharedInstance
															   ? instancesInProject[0]
															   : ScriptableObject.CreateInstance(setType);
					}
				}
				else if(valueProperty.propertyType != SerializedPropertyType.ManagedReference)
				{
					if(setType == typeof(int))
					{
						valueProperty.intValue = 0;
					}
					else if(setType == typeof(string))
					{
						valueProperty.stringValue = "";
					}
					else if(setType == typeof(float))
					{
						valueProperty.floatValue = 0f;
					}
					else if(setType == typeof(bool))
					{
						valueProperty.boolValue = false;
					}
					else if(setType == typeof(double))
					{
						valueProperty.doubleValue = 0d;
					}
					else if(setType == typeof(Vector2))
					{
						valueProperty.vector2Value = Vector2.zero;
					}
					else if(setType == typeof(Vector3))
					{
						valueProperty.vector3Value = Vector3.zero;
					}
					else if(setType == typeof(Vector2Int))
					{
						valueProperty.vector2IntValue = Vector2Int.zero;
					}
					#if UNITY_LOCALIZATION
					else if(setType == typeof(LocalizedString))
					{
						var localizedString = ScriptableObject.CreateInstance<LocalizedString>();
						localizedString.value.TableReference = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.DefaultTable;
						referenceProperty.objectReferenceValue = localizedString;
					}
					#endif
				}
				else if(typeof(Type).IsAssignableFrom(setType))
				{
					valueProperty.managedReferenceValue = new _Type(setType, null);
					referenceProperty.objectReferenceValue = null;
				}
				else
				{
					valueProperty.managedReferenceValue = InitializerEditorUtility.CreateInstance(setType);
					referenceProperty.objectReferenceValue = null;
				}

				Update(true);
				anyProperty.serializedObject.ApplyModifiedProperties();
				UserSelectedTypeChanged?.Invoke(anyProperty, setType);
				GUI.changed = true;
				InspectorContents.Repaint();
			}

			/// <returns> -1 if no value, 0 if object value, 1 if Object reference.</returns>
			public int TryGetValue(out object value, out Object reference)
			{
				reference = referenceProperty?.objectReferenceValue;
				if(reference != null)
				{
					value = null;
					return 1;
				}
			
				value = valueProperty?.GetValue();
				if(value is not null)
				{
					return 0;
				}

				return -1;
			}

			public bool ValueHasChanged() => TryGetValue(out object value, out Object reference) switch
			{
				0 => valueLastFrame is null || !valueType.IsInstanceOfType(valueLastFrame) || !equalityComparer.Equals(value, valueLastFrame),
				1 => valueLastFrame as Object != reference,
				_ => valueLastFrame is not null
			};

			private IEnumerable<Type> GetTypeOptions()
			{
				if(valueProperty == null || valueProperty.propertyType != SerializedPropertyType.ManagedReference || valueType.IsPrimitive || valueType == typeof(string) || valueType.IsEnum)
				{
					return new Type[] { valueType };
				}

				IEnumerable<Type> typeOptions
					= TypeCache.GetTypesDerivedFrom(valueType)
					.Where(t => !t.IsAbstract && !typeof(Object).IsAssignableFrom(t) && !t.IsGenericTypeDefinition && t.Name.IndexOf('=') == -1 && t.Name.IndexOf('<') == -1)
					.OrderBy(t => t.Name);

				// TypeCache.GetTypesDerivedFrom apparently doesn't include primitive types, even for typeof(object), typeof(IConvertible) etc.
				// Also we want these to be at the top, where they are easier to find.
				if(valueType == typeof(object) || valueType.IsInterface)
				{
					if(valueType.IsAssignableFrom(typeof(bool)))
					{
						typeOptions = typeOptions.Prepend(typeof(_Boolean));
					}

					if(valueType.IsAssignableFrom(typeof(int)))
					{
						typeOptions = typeOptions.Prepend(typeof(_Integer));
					}

					if(valueType.IsAssignableFrom(typeof(float)))
					{
						typeOptions = typeOptions.Prepend(typeof(_Float));
					}

					if(valueType.IsAssignableFrom(typeof(double)))
					{
						typeOptions = typeOptions.Prepend(typeof(_Double));
					}

					if(valueType.IsAssignableFrom(typeof(string)))
					{
						typeOptions = typeOptions.Prepend(typeof(_String));
					}

					typeOptions = typeOptions.Distinct();
				}

				if(!valueType.IsAbstract
					&& !valueType.IsGenericTypeDefinition
					&& !typeof(Object).IsAssignableFrom(valueType)
					// in theory a valid option, but in practice it's highly unlikely anybody
					// will want to inject an instance of it, so I prefer to hide it in the menu,
					// to reduce clutter and avoid potential confusion with UnityEngine.Object.
					&& valueType != typeof(object))
				{
					if(typeOptions.Any())
					{
						typeOptions = typeOptions.Prepend(typeof(Separator));
					}

					typeOptions = typeOptions.Prepend(valueType);
				}

				if(isService || drawNullOption || canBeUnityObject)
				{
					if(typeOptions.Any())
					{
						typeOptions = typeOptions.Prepend(typeof(Separator));
					}

					typeOptions = typeOptions.Prepend(null);
				}

				return typeOptions;
			}
		}

		public const string ServiceLabel = "Service";
		private const float DROPDOWN_BUTTON_WIDTH = DropdownButton.ADD_ICON_WIDTH;

		/// <summary>
		/// Contains the <see cref="SerializedProperty"/> of the Any field that changed
		/// and the new type that the user selected.
		/// </summary>
		public static event Action<SerializedProperty, Type> UserSelectedTypeChanged;

		private const float controlOffset = 3f;
		private static readonly GUIContent valueText = new GUIContent("Value");

		private static readonly Dictionary<Type, Dictionary<Type, bool>> isAssignableCaches = new Dictionary<Type, Dictionary<Type, bool>>();

		private readonly Dictionary<int, State> states = new Dictionary<int, State>();
		private CrossSceneReferenceGUI crossSceneReferenceDrawer;
		private float height = EditorGUIUtility.singleLineHeight;

		public static void OpenDropdown(Rect buttonPosition, SerializedProperty anyProperty) //, Type valueType, bool isService)
		{
			var state = new State(anyProperty, (FieldInfo)anyProperty.GetMemberInfo());
			state.typeDropdownButton.OpenDropdown(buttonPosition);
		}

		public override void OnGUI(Rect position, SerializedProperty anyProperty, GUIContent label)
        {
			int index = anyProperty.GetArrayElementIndex();
			if(index < 0)
			{
				index = 0;
			}

			if(!states.TryGetValue(index, out State state))
			{
				state = new State(anyProperty, fieldInfo);
				states.Add(index, state);
			}
			else if(state.referenceProperty == null || (state.valueProperty != null && state.anyProperty.serializedObject != anyProperty.serializedObject) || state.ValueHasChanged())
			{
				state = new State(anyProperty, fieldInfo);
				states[index] = state;
				UserSelectedTypeChanged?.Invoke(anyProperty, state.valueType);
				InspectorContents.Repaint();
			}
			else
			{
				state.Update();
			}

			height = DrawValueField(position, anyProperty, state, label);

			if(GUI.changed)
			{
				anyProperty.serializedObject.ApplyModifiedProperties();
			}
        }

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => height;

		internal static bool TryGetAssignableType(Object draggedObject, Object targetObject, Type anyType, Type valueType, out Type assignableType)
        {
			if(draggedObject == null)
			{
				assignableType = null;
				return false;
			}

			if(draggedObject is GameObject gameObject && valueType != typeof(GameObject) && valueType != typeof(Object) && valueType != typeof(object))
            {
				foreach(var component in gameObject.GetComponents<Component>())
                {
					if(TryGetAssignableType(component, targetObject, anyType, valueType, out assignableType))
                    {
						return true;
                    }
                }

				assignableType = null;
				return false;
            }

			if(!isAssignableCaches.TryGetValue(anyType, out var isAssignableCache))
			{
				isAssignableCache = new Dictionary<Type, bool>();
				isAssignableCaches.Add(anyType, isAssignableCache);
			}

			var draggedType = draggedObject.GetType();
			if(isAssignableCache.TryGetValue(draggedType, out bool isAssignable))
			{
				assignableType = isAssignable ? draggedObject.GetType() : null;
				return isAssignable;
			}

			MethodInfo isCreatableFromMethod = anyType.GetMethod(nameof(Any<object>.IsCreatableFrom), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			isAssignable = (bool)isCreatableFromMethod.Invoke(null, new object[] { draggedObject, targetObject });
			isAssignableCache.Add(draggedType, isAssignable);
			assignableType = isAssignable ? draggedObject.GetType() : null;
			return isAssignable;
        }

		public static Type GetAnyTypeFromField(FieldInfo fieldInfo)
		{
			var fieldType = fieldInfo.FieldType;
			if(fieldType.IsArray)
            {
				return fieldType.GetElementType();
			}

			// List<Any<T>>, Queue<Any<T>> etc.
			if(fieldType is ICollection && fieldType.IsGenericType)
			{
				return fieldType.GetGenericArguments()[0];
			}

			return fieldType;
		}

		public static bool IsService(Object targetObject, Type valueType) => ServiceUtility.IsServiceDefiningType(valueType) || ServiceUtility.ExistsFor(targetObject, valueType);
		public static Type GetValueTypeFromAnyType(Type anyType) => anyType.GetGenericArguments()[0];
		private bool TryGetAssignableType(State state, Object draggedObject, out Type assignableType) => TryGetAssignableType(draggedObject, state.referenceProperty.serializedObject.targetObject, state.anyType, state.valueType, out assignableType);

		private float DrawValueField(Rect position, SerializedProperty anyProperty, State state, GUIContent label)
        {
			int indentLevelWas = EditorGUI.indentLevel;
			position.height = EditorGUIUtility.singleLineHeight;
			bool draggingAssignableObject = state.draggedObjectIsAssignable.HasValue && state.draggedObjectIsAssignable.Value;
			var referenceProperty = state.referenceProperty;
			var valueProperty = state.valueProperty;
			object managedValue = valueProperty == null || valueProperty.propertyType == SerializedPropertyType.ObjectReference ? null : valueProperty.GetValue();
			bool managedValueIsNull = managedValue is null;
			var referenceValue = referenceProperty.objectReferenceValue;
			bool objectReferenceValueIsNull = referenceValue == null;
			var firstTargetObject = anyProperty.serializedObject.targetObject;
			bool drawAsObjectField = !objectReferenceValueIsNull || (draggingAssignableObject && managedValueIsNull) || (!state.isService && state.drawObjectField);
			bool hasSerializedValue = !managedValueIsNull || !objectReferenceValueIsNull;

			var guiColorWas = GUI.color;
			GUI.color = Color.white;

			if(drawAsObjectField)
            {
				EditorGUI.BeginProperty(position, label, referenceProperty);
				var controlRect = EditorGUI.PrefixLabel(position, label);
				bool drawTypeDropdown = state.drawTypeDropdownButton && objectReferenceValueIsNull && (!draggingAssignableObject || !managedValueIsNull);
				if(drawTypeDropdown)
                {
					controlRect = DrawTypeDropdown(controlRect, state, false);
				}

				if(referenceValue != null)
				{
					if(referenceValue is CrossSceneReference crossSceneReference)
					{
						if(!crossSceneReference.isCrossScene)
						{
							referenceProperty.objectReferenceValue = crossSceneReference.Value;
						}
						else
						{
							EditorGUI.indentLevel = 0;

							if(crossSceneReferenceDrawer is null)
							{
								crossSceneReferenceDrawer = new CrossSceneReferenceGUI(state.objectFieldType);
							}

							if(AnyGUI.CurrentlyDrawnItemFailedNullGuard)
							{
								GUI.color = Color.red;
							}

							crossSceneReferenceDrawer.OnGUI(controlRect, referenceProperty, GUIContent.none);
							GUI.color = guiColorWas;

							EditorGUI.indentLevel = indentLevelWas;

							anyProperty.serializedObject.ApplyModifiedProperties();
							EditorGUI.EndProperty();
							return EditorGUI.GetPropertyHeight(valueProperty, label, true);
						}
					}
					else if(GetScene(referenceValue) is Scene referenceScene && referenceScene.IsValid() && referenceScene != GetScene(firstTargetObject))
					{
						#if DEV_MODE
						Debug.Log($"Cross scene reference detected. {referenceValue.name} scene != {GetScene(firstTargetObject)}");
						#endif

						var referenceGameObject = GetGameObject(referenceValue);
						bool isCrossSceneReferenceable = false;
						foreach(var refTag in referenceGameObject.GetComponents<RefTag>())
						{
							if(refTag.Target == referenceValue)
							{
								isCrossSceneReferenceable = true;
								break;
							}
						}

						if(!isCrossSceneReferenceable)
						{
							var referenceable = referenceGameObject.AddComponent<RefTag, Object>(referenceValue);
							Undo.RegisterCreatedObjectUndo(referenceable, "Create Cross-Scene Reference");
						}

						referenceProperty.objectReferenceValue = Create.Instance<CrossSceneReference, GameObject, Object>(GetGameObject(firstTargetObject), referenceValue);
						#if DEV_MODE
						referenceProperty.objectReferenceValue.name = $"CrossSceneReference {firstTargetObject.name} <- {referenceValue?.name ?? "null"} ({referenceProperty.objectReferenceValue.GetInstanceID()})";
						#endif
					}
				}

				bool preventCrossSceneReferencesWas = EditorSceneManager.preventCrossSceneReferences;
				EditorSceneManager.preventCrossSceneReferences = false;

				EditorGUI.indentLevel = 0;
				if(state.draggedObjectIsAssignable.HasValue && state.draggedObjectIsAssignable.Value
					&& GetScene(DragAndDrop.objectReferences[0]) != GetScene(firstTargetObject))
				{
					if(AnyGUI.CurrentlyDrawnItemFailedNullGuard)
					{
						GUI.color = Color.red;
					}

					Object setReferenceValue = EditorGUI.ObjectField(controlRect, GUIContent.none, referenceValue, state.objectFieldType, true);

					GUI.color = guiColorWas;

					if(setReferenceValue != referenceValue && (setReferenceValue == null || TryGetAssignableType(state, setReferenceValue, out Type referenceType)))
					{
						Scene referencerScene = GetScene(firstTargetObject);
						GameObject referencedGameObject = GetGameObject(setReferenceValue);
						Scene referencedScene = GetScene(setReferenceValue);
						Scene? prefabStage = PrefabStageUtility.GetCurrentPrefabStage()?.scene;
						bool referencedIsSceneObject = referencedScene.IsValid() || (referencedGameObject != null && PrefabStageUtility.GetPrefabStage(referencedGameObject) != null);
						bool referecerIsPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(firstTargetObject)|| PrefabStageUtility.GetPrefabStage(GetGameObject(firstTargetObject)) != null;
						if(referencedIsSceneObject && referecerIsPrefabAsset)
						{
							#if DEV_MODE
							Debug.Log($"Cross scene reference detected. {setReferenceValue.name} scene != {GetScene(firstTargetObject)}");
							#endif

							var referenceGameObject = GetGameObject(setReferenceValue);
							bool isCrossSceneReferenceable = false;
							foreach(var refTag in referenceGameObject.GetComponents<RefTag>()) // Could reuse a list to avoid allocating so much
							{
								if(refTag.Target == setReferenceValue)
								{
									isCrossSceneReferenceable = true;
									break;
								}
							}

							if(!isCrossSceneReferenceable)
							{
								var referenceable = referenceGameObject.AddComponent<RefTag, Object>(setReferenceValue);
								Undo.RegisterCreatedObjectUndo(referenceable, "Create Cross-Scene Reference");
							}

							foreach(var target in referenceProperty.serializedObject.targetObjects)
							{
								using(var editing = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(target)))
								{
									var prefabRootGameObject = editing.prefabContentsRoot;
									using(var prefabSerializedObject = new SerializedObject(prefabRootGameObject))
									{
										var prefabReferenceProperty = prefabSerializedObject.FindProperty(referenceProperty.propertyPath);
										var crossSceneReference = Create.Instance<CrossSceneReference, GameObject, Object>(GetGameObject(firstTargetObject), setReferenceValue);
										prefabReferenceProperty.objectReferenceValue = crossSceneReference;
										#if DEV_MODE
										crossSceneReference.name = $"CrossSceneReference {target.name} <- {setReferenceValue?.name ?? "null"} ({crossSceneReference.GetInstanceID()})";
										#endif
									}
								}
							}
						}
						else
						{
							Undo.RecordObjects(referenceProperty.serializedObject.targetObjects, "Set Object Reference");
							referenceProperty.objectReferenceValue = setReferenceValue;
						}
					}
				}
				else
				{
					if(AnyGUI.CurrentlyDrawnItemFailedNullGuard)
					{
						GUI.color = Color.red;
					}

					DrawObjectField(controlRect, referenceValue, state);

					GUI.color = guiColorWas;
				}
				
				EditorGUI.indentLevel = indentLevelWas;

				EditorSceneManager.preventCrossSceneReferences = preventCrossSceneReferencesWas;

				if(controlRect.Contains(Event.current.mousePosition))
				{
					DragAndDrop.visualMode = state.draggedObjectIsAssignable.HasValue && !state.draggedObjectIsAssignable.Value ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Generic;
				}
				else if(state.draggedObjectIsAssignable.HasValue)
				{
					var tintColor = state.draggedObjectIsAssignable.Value ? new Color(0f, 1f, 0f, 0.05f) : new Color(1f, 0f, 0f, 0.05f);
					EditorGUI.DrawRect(controlRect, tintColor);
				}

				EditorGUI.EndProperty();
				return EditorGUIUtility.singleLineHeight;
            }

			if(state.isService && !hasSerializedValue)
            {
				bool clicked = ServiceTagUtility.Draw(position, label, anyProperty);
				EditorGUI.indentLevel = indentLevelWas;

				if(clicked)
				{
					GUIUtility.ExitGUI();
				}
				return EditorGUIUtility.singleLineHeight;
            }

			bool drawDiscardButton = state.drawDiscardButton;
			if(drawDiscardButton)
            {
				var discardRect = position;
				discardRect.x = position.xMax - DiscardButton.Width;
				state.discardValueButton.Draw(discardRect);

				position.width -= DiscardButton.Width;
			}

			// Elements with foldouts are drawn better, when not drawing PrefixLabel manually,
			// so prefer that, if type dropdown does not need to be drawn.
			if(!state.drawTypeDropdownButton && !draggingAssignableObject && valueProperty.propertyType is not SerializedPropertyType.ObjectReference)
			{
				if(AnyGUI.CurrentlyDrawnItemFailedNullGuard)
				{
					GUI.color = Color.red;
				}

				EditorGUI.PropertyField(position, valueProperty, label, true);

				GUI.color = guiColorWas;

				return EditorGUI.GetPropertyHeight(valueProperty, label, true);
			}

			// Arrays and lists are drawn better, when not drawing PrefixLabel manually.
			if(valueProperty.isArray
				// SerializedProperty.isArray is true for string fields! Skip those.
				&& valueProperty.propertyType != SerializedPropertyType.String)
			{
				var dropdownRect = position;
				float labelWidth = EditorGUIUtility.labelWidth;
				dropdownRect.x += labelWidth;
				const float ARRAY_SIZE_CONTROL_WIDTH = 200f;
				dropdownRect.width -= labelWidth + ARRAY_SIZE_CONTROL_WIDTH;

				if(dropdownRect.width >= EditorGUIUtility.singleLineHeight)
				{
					DrawTypeDropdown(dropdownRect, state, true);
				}

				if(AnyGUI.CurrentlyDrawnItemFailedNullGuard)
				{
					GUI.color = Color.red;
				}

				EditorGUI.PropertyField(position, valueProperty, label, true);

				GUI.color = guiColorWas;

				if(dropdownRect.width >= EditorGUIUtility.singleLineHeight)
				{
					DrawTypeDropdown(dropdownRect, state, true);
				}

				return EditorGUI.GetPropertyHeight(valueProperty, label, true);
			}

			bool drawTypeDropdownButton = state.drawTypeDropdownButton;

			float labelWidthWas = EditorGUIUtility.labelWidth;
			Rect remainingRect;
			if(draggingAssignableObject)
			{
				if(valueProperty.isExpanded && drawTypeDropdownButton)
				{
					EditorGUI.PrefixLabel(position, label);
					remainingRect = position;
				}
				else
				{
					remainingRect = EditorGUI.PrefixLabel(position, label);
				}

				label = GUIContent.none;

				Rect prefixRect = position;
				prefixRect.xMax = remainingRect.x;
				DrawObjectField(prefixRect, referenceValue, state);
			}
			else if(drawTypeDropdownButton && !managedValueIsNull && (state.valueHasChildProperties || valueProperty.isExpanded))
			{
				var dropdownRect = position;
				float labelWidth = EditorGUIUtility.labelWidth;
				dropdownRect.x += labelWidth;
				const float ARRAY_SIZE_CONTROL_WIDTH = 200f;
				dropdownRect.width -= labelWidth + ARRAY_SIZE_CONTROL_WIDTH;

				if(dropdownRect.width >= EditorGUIUtility.singleLineHeight)
				{
					DrawTypeDropdown(dropdownRect, state, true);
				}

				EditorGUIUtility.labelWidth += DROPDOWN_BUTTON_WIDTH;
				drawTypeDropdownButton = false;
				remainingRect = position;
			}
			else
			{
				remainingRect = label.text.Length == 0 ? position : EditorGUI.PrefixLabel(position, label);
				label = GUIContent.none;
			}

			if(valueProperty == null)
            {
				return EditorGUIUtility.singleLineHeight;
			}

			if(valueProperty.propertyType != SerializedPropertyType.ManagedReference)
            {
				EditorGUI.indentLevel = 0;
				if(drawTypeDropdownButton)
				{
					remainingRect = DrawTypeDropdown(remainingRect, state, false);
				}

				if(AnyGUI.CurrentlyDrawnItemFailedNullGuard)
				{
					GUI.color = Color.red;
				}

				EditorGUI.PropertyField(remainingRect, valueProperty, label, true);

				GUI.color = guiColorWas;
				EditorGUI.indentLevel = indentLevelWas;
				EditorGUIUtility.labelWidth = labelWidthWas;
				return EditorGUI.GetPropertyHeight(valueProperty, label, true);
            }

			if(state.valueType == typeof(object))
            {
				switch(valueProperty.type)
                {
					case "managedReference<Int32>":
						SetManagedValue(valueProperty, new _Integer() { value = (int)managedValue }, "Set Int Value");
						valueProperty.serializedObject.ApplyModifiedProperties();
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<_Integer>":
						if(drawTypeDropdownButton)
						{
							remainingRect = DrawTypeDropdown(remainingRect, state, false);
						}

						EditorGUI.BeginProperty(remainingRect, label, valueProperty);
						var intWas = ((_Integer)managedValue).value;
						var setInt = EditorGUI.IntField(remainingRect, label, intWas);
                        if(intWas != setInt)
                        {
                            SetManagedValue(valueProperty, new _Integer() { value = setInt }, "Set Int Value");
                        }
                        EditorGUI.EndProperty();
                        valueProperty.serializedObject.ApplyModifiedProperties();

						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
                        return EditorGUIUtility.singleLineHeight;
					case "managedReference<Type>":
						SetManagedValue(valueProperty, new _Type((Type)managedValue, null), "Set Type Value");
						valueProperty.serializedObject.ApplyModifiedProperties();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
                    case "managedReference<Boolean>":
						SetManagedValue(valueProperty, new _Boolean() { value = (bool)managedValue }, "Set Boolean Value");
						valueProperty.serializedObject.ApplyModifiedProperties();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<_Boolean>":
						if(drawTypeDropdownButton)
						{
							remainingRect = DrawTypeDropdown(remainingRect, state, false);
						}

						EditorGUI.BeginProperty(remainingRect, label, valueProperty);
                        var boolWas = ((_Boolean)managedValue).value;
						var setBool = EditorGUI.Toggle(remainingRect, label, boolWas);
                        if(boolWas != setBool)
                        {
                            SetManagedValue(valueProperty, new _Boolean() { value = setBool }, "Set Boolean Value");
                        }
                        EditorGUI.EndProperty();
                        valueProperty.serializedObject.ApplyModifiedProperties();
                        GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<Single>":
						SetManagedValue(valueProperty, new _Float() { value = (float)managedValue }, "Set Float Value");
						valueProperty.serializedObject.ApplyModifiedProperties();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<_Float>":
						if(drawTypeDropdownButton)
						{
							remainingRect = DrawTypeDropdown(remainingRect, state, false);
						}

						EditorGUI.BeginProperty(remainingRect, label, valueProperty);
                        var floatWas = ((_Float)managedValue).value;
                        var setFloat = EditorGUI.FloatField(remainingRect, label, floatWas);
                        if(floatWas != setFloat)
                        {
                            SetManagedValue(valueProperty, new _Float() { value = setFloat }, "Set Float Value");
                        }

                        EditorGUI.EndProperty();

                        GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<Double>":
						SetManagedValue(valueProperty, new _Double() { value = (double)managedValue }, "Set Double Value");
						valueProperty.serializedObject.ApplyModifiedProperties();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<_Double>":
						if(drawTypeDropdownButton)
						{
							remainingRect = DrawTypeDropdown(remainingRect, state, false);
						}

						EditorGUI.BeginProperty(remainingRect, label, valueProperty);
						var doubleWas = ((_Double)managedValue).value;
						var setDouble = EditorGUI.DoubleField(remainingRect, label, doubleWas);
						if(doubleWas != setDouble)
						{
							SetManagedValue(valueProperty, new _Double() { value = setDouble }, "Set Double Value");
						}
						EditorGUI.EndProperty();

						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<String>":
						SetManagedValue(valueProperty, new _String() { value = (string)managedValue }, "Set String Value");
						valueProperty.serializedObject.ApplyModifiedProperties();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<_String>":
						if(drawTypeDropdownButton)
						{
							remainingRect = DrawTypeDropdown(remainingRect, state, false);
						}

						EditorGUI.BeginProperty(remainingRect, label, valueProperty);
						var stringWas = ((_String)managedValue).value;
						var setString = EditorGUI.TextField(remainingRect, label, stringWas);
						if(stringWas != setString)
						{
							SetManagedValue(valueProperty, new _String() { value = setString }, "Set String Value");
						}
						EditorGUI.EndProperty();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
                    case "managedReference<Color>":
						if(drawTypeDropdownButton)
						{
							remainingRect = DrawTypeDropdown(remainingRect, state, false);
						}

						EditorGUI.BeginProperty(remainingRect, label, valueProperty);
						var colorWas = (Color)managedValue;
						var setColor = EditorGUI.ColorField(remainingRect, label, colorWas);
						if(colorWas != setColor)
						{
							SetManagedValue(valueProperty, setColor, "Set Color Value");
						}

						EditorGUI.EndProperty();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
                    case "managedReference<Vector2>":
						if(drawTypeDropdownButton)
						{
							remainingRect = DrawTypeDropdown(remainingRect, state, false);
						}

						EditorGUI.BeginProperty(remainingRect, label, valueProperty);
						var vector2Was = (Vector2)managedValue;
						var setVector2 = EditorGUI.Vector2Field(remainingRect, label, vector2Was);
						if(vector2Was != setVector2)
						{
							SetManagedValue(valueProperty, setVector2, "Set Vector2 Value");
						}
						EditorGUI.EndProperty();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<Vector3>":
						if(drawTypeDropdownButton)
						{
							remainingRect = DrawTypeDropdown(remainingRect, state, false);
						}

						EditorGUI.BeginProperty(remainingRect, label, valueProperty);
						var vector3Was = (Vector3)managedValue;
						var setVector3 = EditorGUI.Vector3Field(remainingRect, label, vector3Was);
						if(vector3Was != setVector3)
						{
							SetManagedValue(valueProperty, setVector3, "Set Vector3 Value");
						}
						EditorGUI.EndProperty();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<Vector4>":
						if(drawTypeDropdownButton)
						{
							remainingRect = DrawTypeDropdown(remainingRect, state, false);
						}

						EditorGUI.BeginProperty(remainingRect, label, valueProperty);
						var vector4Was = (Vector4)managedValue;
						var setVector4 = EditorGUI.Vector4Field(remainingRect, label, vector4Was);
						if(vector4Was != setVector4)
						{
							SetManagedValue(valueProperty, setVector4, "Set Vector4 Value");
						}
						EditorGUI.EndProperty();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<Vector2Int>":
						if(drawTypeDropdownButton)
						{
							remainingRect = DrawTypeDropdown(remainingRect, state, false);
						}

						EditorGUI.BeginProperty(remainingRect, label, valueProperty);
						var vector2IntWas = (Vector2Int)managedValue;
						var setVector2Int = EditorGUI.Vector2IntField(remainingRect, label, vector2IntWas);
						if(vector2IntWas != setVector2Int)
						{
							SetManagedValue(valueProperty, setVector2Int, "Set Vector2Int Value");
						}
						EditorGUI.EndProperty();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
					case "managedReference<Vector3Int>":
						if(drawTypeDropdownButton)
						{
							remainingRect = DrawTypeDropdown(remainingRect, state, false);
						}

						EditorGUI.BeginProperty(remainingRect, label, valueProperty);
						var vector3IntWas = (Vector3Int)managedValue;
						var setVector3Int = EditorGUI.Vector3IntField(remainingRect, label, vector3IntWas);
						if(vector3IntWas != setVector3Int)
						{
							SetManagedValue(valueProperty, setVector3Int, "Set Vector3Int Value");
						}
						EditorGUI.EndProperty();
						GUI.color = guiColorWas;
						EditorGUI.indentLevel = indentLevelWas;
						EditorGUIUtility.labelWidth = labelWidthWas;
						return EditorGUIUtility.singleLineHeight;
                }
            }

			if(drawTypeDropdownButton)
			{
				if(!state.valueHasChildProperties && label.text.Length > 0)
				{
					remainingRect = EditorGUI.PrefixLabel(remainingRect, label);
					label = GUIContent.none;
				}

				if(AnyGUI.CurrentlyDrawnItemFailedNullGuard && managedValueIsNull)
				{
					GUI.color = Color.red;
				}

				DrawTypeDropdown(remainingRect, state, true);

				GUI.color = guiColorWas;
			}

			if(managedValueIsNull)
            {
				GUI.color = guiColorWas;
				EditorGUI.indentLevel = indentLevelWas;
				EditorGUIUtility.labelWidth = labelWidthWas;
				return EditorGUIUtility.singleLineHeight;
            }

            var assignedInstanceType = managedValue.GetType();
            if(assignedInstanceType is null)
            {
				GUI.color = guiColorWas;
				EditorGUI.indentLevel = indentLevelWas;
				EditorGUIUtility.labelWidth = labelWidthWas;
				return EditorGUIUtility.singleLineHeight;
            }

			bool isSerializableByUnity = TypeUtility.IsSerializableByUnity(assignedInstanceType);

            if(!isSerializableByUnity)
            {
				var boxPosition = position;
                boxPosition.y += position.height;
                EditorGUI.HelpBox(boxPosition, assignedInstanceType.Name + " is missing the [Serializable] attribute.", MessageType.Info);
                GUI.color = guiColorWas;
				EditorGUI.indentLevel = indentLevelWas;
				EditorGUIUtility.labelWidth = labelWidthWas;
				return EditorGUIUtility.singleLineHeight * 2f;
            }

			if(state.valueHasChildProperties)
			{
				EditorGUI.PropertyField(position, valueProperty, label, true);
				GUI.color = guiColorWas;
				EditorGUI.indentLevel = indentLevelWas;
				EditorGUIUtility.labelWidth = labelWidthWas;
				return EditorGUI.GetPropertyHeight(valueProperty, valueText, true);
			}

			var valuePosition = position;
			valuePosition.y += position.height;

			EditorGUI.indentLevel++;
			EditorGUI.PropertyField(valuePosition, valueProperty, valueText, true);
			EditorGUI.indentLevel--;

			GUI.color = guiColorWas;
			EditorGUI.indentLevel = indentLevelWas;
			EditorGUIUtility.labelWidth = labelWidthWas;
			return EditorGUIUtility.singleLineHeight + EditorGUI.GetPropertyHeight(valueProperty, valueText, true);
        }

		void DrawObjectField(Rect controlRect, Object referenceValue, State state)
		{
			EditorGUI.ObjectField(controlRect, state.referenceProperty, state.objectFieldType, GUIContent.none);

			Object newReferenceValue = state.referenceProperty.objectReferenceValue;
			if(newReferenceValue == referenceValue || newReferenceValue == null)
			{
				return;
			}

			if(!state.valueType.IsInstanceOfType(newReferenceValue))
			{
				if(newReferenceValue is GameObject gameObject)
				{
					newReferenceValue = Find.In(gameObject, state.valueType) as Object;
					if(newReferenceValue == null)
					{
						state.referenceProperty.objectReferenceValue = referenceValue;
						return;
					}

					state.referenceProperty.objectReferenceValue = newReferenceValue;
					if(newReferenceValue == referenceValue)
					{
						return;
					}
				}
				else
				{
					MethodInfo isCreatableFromMethod = state.anyType.GetMethod(nameof(Any<object>.IsCreatableFrom), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					bool isCreatableFrom = (bool)isCreatableFromMethod.Invoke(null, new object[] { newReferenceValue, state.referenceProperty.serializedObject.targetObject });
					if(!isCreatableFrom)
					{
						state.referenceProperty.objectReferenceValue = referenceValue;
						return;
					}
				}
			}

			if(!state.valueType.IsValueType && state.valueProperty != null && state.valueProperty.GetValue() is not null)
			{
				state.valueProperty.SetValue(null);
				state.referenceProperty.serializedObject.ApplyModifiedProperties();
				state.Update(true);
				UserSelectedTypeChanged?.Invoke(state.anyProperty, null);
				GUI.changed = true;
				InspectorContents.Repaint();
				GUIUtility.ExitGUI();
			}
		}

        private static void SetManagedValue<T>(SerializedProperty valueProperty, T setValue, string undoText)
        {
			var targets = valueProperty.serializedObject.targetObjects;

			Undo.RecordObjects(targets, undoText);

			valueProperty.managedReferenceValue = setValue;

			foreach(var target in targets)
            {
                EditorUtility.SetDirty(target);
            }
        }

		/// <summary>
		/// Can an <see cref="Object"/> type value be assigned to a field of type <paramref name="valueType"/>?
		/// <para>
		/// Does not consider whether or not any value providers can be assigned to an Any field with the given value type.
		/// </para>
		/// </summary>
		/// <param name="valueType"></param>
		/// <returns> <see langword="true"/> if <paramref name="valueType"/> is <see cref="object"/>, <see cref="Object"/>
		/// or an interface type implemented by any non-abstact <see cref="Object"/>-derived class;
		/// otherwise, <see langword="false"/>. </returns>
        public static bool CanAssignUnityObjectToField(Type valueType)
		{
			if(valueType == typeof(object) || typeof(Object).IsAssignableFrom(valueType))
			{
				return true;
			}

			if(valueType.IsInterface)
			{
				foreach(var derivedType in TypeCache.GetTypesDerivedFrom(valueType))
				{
					if(typeof(Object).IsAssignableFrom(derivedType) && !derivedType.IsAbstract)
					{
						return true;
					}
				}
			}

			return false;
		}

		public static bool CanAssignNonUnityObjectToField(Type valueType)
        {
			if(typeof(Object).IsAssignableFrom(valueType))
			{
				return false;
			}

			if(valueType.IsInterface)
			{
				foreach(var derivedType in TypeCache.GetTypesDerivedFrom(valueType))
				{
					if(!typeof(Object).IsAssignableFrom(derivedType) && !derivedType.IsAbstract)
					{
						return true;
					}
				}

				return false;
			}

			// Skip values that Unity can't serialize nor draw in the inspector
			if(typeof(Delegate).IsAssignableFrom(valueType)
			|| typeof(IDictionary).IsAssignableFrom(valueType)
			|| typeof(MemberInfo).IsAssignableFrom(valueType)
			|| valueType == typeof(DateTime)
			|| valueType == typeof(TimeSpan))
			{
				return false;
			}

			return true;
		}

		private static Rect DrawTypeDropdown(Rect rect, State state, bool fullWidth)
        {
			float totalWidth = rect.width;
			float width = rect.width;
			if(!fullWidth)
            {
				GUIContent buttonLabel = state.typeDropdownButton.buttonLabel;
				if(buttonLabel.text.Length > 0)
				{
					width = EditorStyles.label.CalcSize(buttonLabel).x + DROPDOWN_BUTTON_WIDTH + 3f;
				} 
				else
				{
					width = DROPDOWN_BUTTON_WIDTH;
				}

				rect.width = width;
			}
			
			bool showMixedValueWas = EditorGUI.showMixedValue;
			if(state.valueProperty != null && state.valueProperty.hasMultipleDifferentValues)
			{
				EditorGUI.showMixedValue = true;
			}

			int indentLevelWas = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			// If GUI is tinted red by null argument guard, but value selected in the dropdown is not Null,
			// then we don't tint the type dropdown value red, as that would be misleading, but only the value that follows it.
			if(GUI.color == Color.red && state.typeDropdownButton.buttonLabel.text.Length == 0)
			{
				GUI.color = Color.white;
				state.typeDropdownButton.Draw(rect);
				GUI.color = Color.red;
			}
			else
			{
				state.typeDropdownButton.Draw(rect);
			}

			EditorGUI.indentLevel = indentLevelWas;

			EditorGUI.showMixedValue = showMixedValueWas;

			var remainingRect = rect;
			remainingRect.x += width + controlOffset;
			remainingRect.width = totalWidth - width - controlOffset;

			return remainingRect;
        }

		private static Scene GetScene(Object target) => target is Component component && component != null ? component.gameObject.scene : target is GameObject gameObject && gameObject != null ? gameObject.scene : default;
		private static GameObject GetGameObject(Object target) => target is Component component && component != null ? component.gameObject : target as GameObject;

		 ~AnyPropertyDrawer() => HandleDispose();

		public void Dispose()
		{
			HandleDispose();
			GC.SuppressFinalize(this);
		}

		private void HandleDispose()
		{
			if(crossSceneReferenceDrawer != null)
			{
				crossSceneReferenceDrawer.Dispose();
				crossSceneReferenceDrawer = null;
			}

			states.Clear();
		}
	}
}