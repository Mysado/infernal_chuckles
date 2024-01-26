using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Sisus.Init.Internal;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sisus.Init.EditorOnly.Internal
{
    using static InitializerEditorUtility;

    [CanEditMultipleObjects]
	internal class InitializerEditor : Editor
	{
        protected internal const string InitArgumentMetadataClassName = InitializerUtility.InitArgumentMetadataClassName;

        private SerializedProperty client;
        private SerializedProperty nullArgumentGuard;
        private GUIContent clientLabel;
        private bool clientIsInitializable;
        private bool hasServiceArguments;
        private InitializerGUI ownedDrawer;
        private InitializerGUI externalDrawer;
        [NonSerialized]
        private AnyGUI[] propertyDrawerData = new AnyGUI[0];
        private Type clientType;
        private bool drawNullArgumentGuard;
        private ServiceChangedListener[] serviceChangedListeners = Array.Empty<ServiceChangedListener>();

        protected virtual Type[] GetGenericArguments() => target.GetType().BaseType.GetGenericArguments();
        protected virtual Type GetClientType(Type[] genericArguments) => genericArguments[0];
        protected virtual Type[] GetInitArgumentTypes(Type[] genericArguments) => genericArguments.Skip(1).ToArray();

        private static bool ServicesShown => InitializerGUI.ServicesShown;

        protected virtual bool HasUserDefinedInitArgumentFields { get; }

        private bool IsNullAllowed
        {
            get
            {
                if(!drawNullArgumentGuard)
				{
                    return true;
				}

                var nullGuard = (NullArgumentGuard)nullArgumentGuard.intValue;
                return !nullGuard.IsEnabled(Application.isPlaying ? NullArgumentGuard.RuntimeException : NullArgumentGuard.EditModeWarning)
                   || (!nullGuard.IsEnabled(NullArgumentGuard.EnabledForPrefabs) && PrefabUtility.IsPartOfPrefabAsset(target));
            }
        }

        internal void Setup([AllowNull] InitializerGUI externalDrawer)
        {
            this.externalDrawer = externalDrawer;
            client = serializedObject.FindProperty("target");
            nullArgumentGuard = serializedObject.FindProperty(nameof(nullArgumentGuard));
            drawNullArgumentGuard = nullArgumentGuard != null;

            var genericArguments = GetGenericArguments();
            clientType = GetClientType(genericArguments);
            clientIsInitializable = IsInitializable(clientType);
            clientLabel = GetClientLabel();
            var initializers = targets;
            int count = targets.Length;
            var clients = new Object[count];
            for(int i = 0; i < count; i++)
			{
                var initializer = initializers[i] as IInitializer;
                clients[i] = initializer.Target;

                if(i == 0 && initializer is IInitializerEditorOnly initializerEditorOnly && !initializerEditorOnly.ShowNullArgumentGuard)
				{
                    drawNullArgumentGuard = false;
				}
            }

            var initArguments = GetInitArgumentTypes(genericArguments);
            if(externalDrawer is null)
            {
                ownedDrawer = new InitializerGUI(clientType, clients, initArguments, this);
            }

            Setup(initArguments, externalDrawer ?? ownedDrawer);
            hasServiceArguments = Array.Exists(propertyDrawerData, d => d.isService);

            int initArgumentCount = initArguments.Length;
            Array.Resize(ref serviceChangedListeners, initArgumentCount);
            for(int i = 0; i < initArgumentCount; i++)
			{
                serviceChangedListeners[i] = ServiceChangedListener.Create(genericArguments[i], OnInitArgumentServiceChanged);
            }

            GUIContent GetClientLabel()
			{
                var result = GetLabel(clientType);
                if(typeof(StateMachineBehaviour).IsAssignableFrom(clientType))
				{
                    result.text = "None (Animator → " + result.text + ")";
                }
                else if(typeof(ScriptableObject).IsAssignableFrom(clientType))
				{
                    result.text = "None (" + result.text + ")";
                }
                else
                {
                    result.text = "New Instance (" + result.text + ")";
                }

                return result;
            }
        }

        // This can get called during deserialization, which could result in errors
        private void OnInitArgumentServiceChanged() => EditorApplication.delayCall += ()=>
		{
            if(this == null)
            {
                return;
            }

            OnDisable();
            Setup(externalDrawer);
		};

        /// <param name="genericArguments">
        /// Types of the init arguments injected to the client.
        /// <para>
        /// In addition to the argument types, this can include the client type etc.
        /// </para>
        /// </param>
        /// <param name="internalOrExternalDrawer">
        /// Either <see cref="ownedDrawer"/> (lifetime managed by this editor) or <see cref="externalDrawer"/> (life time managed by an external object).
        /// </param>
        protected virtual void Setup(Type[] argumentTypes, InitializerGUI internalOrExternalDrawer)
        {
            DisposePropertyDrawerData();
            propertyDrawerData = HasUserDefinedInitArgumentFields ? Array.Empty<AnyGUI>() : GetPropertyDrawerData(clientType, argumentTypes, internalOrExternalDrawer);
            
            AnyPropertyDrawer.UserSelectedTypeChanged -= OnInitArgUserSelectedTypeChanged;
            if(propertyDrawerData.Length > 0)
            {
                AnyPropertyDrawer.UserSelectedTypeChanged += OnInitArgUserSelectedTypeChanged;
            }
        }

		private void OnInitArgUserSelectedTypeChanged(SerializedProperty changedProperty, Type userSelectedType)
		{
			for(int i = propertyDrawerData.Length - 1; i >= 0; i--)
			{
                propertyDrawerData[i].Refresh();
			}
		}

        private AnyGUI[] GetPropertyDrawerData(Type clientType, Type[] argumentTypes, InitializerGUI internalOrExternalDrawer)
            => InitializerEditorUtility.GetPropertyDrawerData(serializedObject, clientType, argumentTypes, internalOrExternalDrawer);

        public override void OnInspectorGUI()
		{
            bool hierarchyModeWas = EditorGUIUtility.hierarchyMode;
            EditorGUIUtility.hierarchyMode = true;

            serializedObject.Update();

			if(client == null)
			{
				Setup(null);
			}

			if(ownedDrawer == null)
			{
                EditorGUIUtility.hierarchyMode = hierarchyModeWas;
				return;
			}

			var rect = EditorGUILayout.GetControlRect();
            rect.y -= 2f;

            // Tooltip for icon must be drawn before drawer.OnInspectorGUI for it to
            // take precedence over Init header tooltip.
            var iconRect = rect;
            iconRect.x = EditorGUIUtility.labelWidth - 1f;
            iconRect.y += 5f;
            iconRect.width = 20f;
            iconRect.height = 20f;
            GUI.Label(iconRect, GetReferenceTooltip(client.serializedObject.targetObject, client.objectReferenceValue, clientIsInitializable));

            GUILayout.Space(-EditorGUIUtility.singleLineHeight - 2f);
			ownedDrawer.OnInspectorGUI();

            if(client.objectReferenceValue is Component)
            {
			    DrawClientField(rect, client, clientLabel, clientIsInitializable, hasServiceArguments);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUIUtility.hierarchyMode = hierarchyModeWas;
		}

		public void DrawArgumentFields()
        {
            if(client == null)
            {
                Setup(null);
            }

            int count = propertyDrawerData.Length;
            if(count == 0)
			{
                var serializedProperty = serializedObject.GetIterator();
                serializedProperty.NextVisible(true);
                while(serializedProperty.NextVisible(false))
				{
                    EditorGUILayout.PropertyField(serializedProperty);
				}

                return;
			}

			for(int i = 0; i < count; i++)
			{
				AnyGUI drawer = propertyDrawerData[i];
				drawer.DrawArgumentField(IsNullAllowed, ServicesShown);
			}
        }

        private void OnDisable()
		{
            AnyPropertyDrawer.UserSelectedTypeChanged -= OnInitArgUserSelectedTypeChanged;
            Array.ForEach(serviceChangedListeners, x => x.Dispose());
            serviceChangedListeners = Array.Empty<ServiceChangedListener>();
			DisposePropertyDrawerData();
			DisposeInitializerDrawer();
		}

		private void DisposeInitializerDrawer()
		{
			if(ownedDrawer is null)
			{
				return;
			}

			ownedDrawer.Dispose();
			ownedDrawer = null;
		}

		private void DisposePropertyDrawerData()
		{
			for(int i = 0, count = propertyDrawerData.Length; i < count; i++)
			{
				propertyDrawerData[i].Dispose();
			}

            propertyDrawerData = Array.Empty<AnyGUI>();
		}

        private abstract class ServiceChangedListener : IDisposable
		{
			public static ServiceChangedListener Create(Type argumentType, Action onChangedCallback)
                => (ServiceChangedListener)typeof(ServiceChangedListener<>).MakeGenericType(argumentType).GetConstructor(new Type[] { typeof(Action) }).Invoke(new object[] { onChangedCallback });

			public abstract void Dispose();
		}

        private sealed class ServiceChangedListener<TService> : ServiceChangedListener
		{
            private readonly Action onChangedCallback;

			public ServiceChangedListener(Action onChangedCallback)
            {
                Service.AddInstanceChangedListener<TService>(OnServiceChanged);
                this.onChangedCallback = onChangedCallback;
            }

			public override void Dispose() => Service.RemoveInstanceChangedListener<TService>(OnServiceChanged);
			public void OnServiceChanged([AllowNull] TService oldInstance, [AllowNull] TService newInstance) => onChangedCallback();
		}
	}
}