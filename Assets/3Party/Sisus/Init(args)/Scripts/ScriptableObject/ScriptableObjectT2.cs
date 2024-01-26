﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using static Sisus.Init.Reflection.InjectionUtility;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
using Sisus.Init.EditorOnly;
#endif

namespace Sisus.Init
{
	/// <summary>
	/// A base class for scriptable objects that can be
	/// <see cref="Create.Instance{TScriptableObject, TFirstArgument, TSecondArgument}">created</see>
	/// or <see cref="ObjectExtensions.Instantiate{TScriptableObject, TFirstArgument, TSecondArgument}">instantiated</see>
	/// with two arguments passed to the <see cref="Init"/> function of the created instance.
	/// <para>
	/// If the object depends exclusively on classes that have the <see cref="ServiceAttribute"/> then
	/// it will receive them in its <see cref="Init"/> function automatically during initialization.
	/// </para>
	/// <para>
	/// If the object depends on any classes that don't have the <see cref="ServiceAttribute"/>,
	/// then an <see cref="ScriptableObjectInitializer{TScriptableObject, TFirstArgument, TSecondArgument}"/>
	/// can be used to specify its initialization arguments.
	/// </para>
	/// <para>
	/// Instances of classes inheriting from <see cref="ScriptableObject{TFirstArgument, TSecondArgument}"/> receive the arguments
	/// via the <see cref="Init"/> method where they can be assigned to member fields or properties.
	/// </para>
	/// <typeparam name="TFirstArgument"> Type of the first argument received in the <see cref="Init"/> function. </typeparam>
	/// <typeparam name="TSecondArgument"> Type of the second argument received in the <see cref="Init"/> function. </typeparam>
	public abstract class ScriptableObject<TFirstArgument, TSecondArgument> : ScriptableObject, IInitializable<TFirstArgument, TSecondArgument>, IInitializable
		#if UNITY_EDITOR
		, IInitializableEditorOnly
		#endif
	{
		[SerializeField, HideInInspector, MaybeNull]
		private ScriptableObject initializer = null;

		#if DEBUG
		/// <summary>
		/// <see langword="true"/> if object is currently in the process of being initialized with an argument; otherwise, <see langword="false"/>.
		/// </summary>
		[NonSerialized]
		private bool initializing;
		#endif

		#if UNITY_EDITOR
		IInitializer IInitializableEditorOnly.Initializer { get => initializer as IInitializer; set => initializer = value as ScriptableObject; }
		#endif

		/// <summary>
		/// Provides the <see cref="ScriptableObject"/> with the objects that it depends on.
		/// <para>
		/// You can think of the <see cref="Init"/> function as a parameterized constructor alternative for the <see cref="ScriptableObject"/>.
		/// </para>
		/// <para>
		/// <see cref="Init"/> is called at the beginning of the <see cref="Awake"/> event function when the script is being loaded,
		/// or when services become ready (whichever occurs later).
		/// </para>
		/// </summary>
		/// <param name="firstArgument"> The first object that this <see cref="ScriptableObject"/> depends on. </param>
		/// <param name="secondArgument"> The second object that this <see cref="ScriptableObject"/> depends on. </param>
		protected abstract void Init(TFirstArgument firstArgument, TSecondArgument secondArgument);

		/// <summary>
		/// Assigns an argument received during initialization to a field or property by the <paramref name="memberName">given name</paramref>.
		/// <para>
		/// Because reflection is used to set the value it is possible to use this to assign to init only fields and properties.
		/// Properties that do not have a set accessor and are not auto-implemented are not supported however.
		/// </para>
		/// </summary>
		/// <param name="memberName"> Name of the field or property to which to assign the value. </param>
		/// <exception cref="InvalidOperationException">
		/// Thrown if this method is called outside of the context of the client object being <see cref="Init">initialized</see>.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		/// Thrown if the provided <paramref name="memberName"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="MissingMemberException">
		/// Thrown if no field or property by the provided name is found or if property by given name is not auto-implemented
		/// and does not have a set accessor.
		/// </exception>
		protected object this[[DisallowNull] string memberName]
        {
			set
			{
				#if DEBUG
				if(!initializing)
                {
					throw new InvalidOperationException($"Unable to assign to member {GetType().Name}.{memberName}: Values can only be injected during initialization.");
				}
				#endif

				Inject<ScriptableObject<TFirstArgument, TSecondArgument>, TFirstArgument, TSecondArgument>(this, memberName, value);
			}
        }

		/// <summary>
		/// A value against which any <see cref="object"/> can be compared to determine whether or not it is
		/// <see langword="null"/> or an <see cref="Object"/> which has been <see cref="Object.Destroy">destroyed</see>.
		/// <example>
		/// <code>
		/// private IEvent trigger;
		/// 
		///	private void OnDisable()
		///	{
		///		if(trigger != Null)
		///		{
		///			trigger.RemoveListener(this);
		///		}
		///	}
		/// </code>
		/// </example>
		/// </summary>
		[NotNull]
		protected static NullExtensions.NullComparer Null => NullExtensions.Null;

		/// <summary>
		/// A value against which any <see cref="object"/> can be compared to determine whether or not it is
		/// <see langword="null"/> or an <see cref="Object"/> which is <see cref="GameObject.activeInHierarchy">inactive</see>
		/// or has been <see cref="Object.Destroy">destroyed</see>.
		/// <example>
		/// <code>
		/// private ITrackable target;
		/// 
		/// private void Update()
		/// {
		/// 	if(target != NullOrInactive)
		/// 	{
		/// 		transform.LookAt(target.Position);
		/// 	}
		/// }
		/// </code>
		/// </example>
		[NotNull]
		protected static NullExtensions.NullOrInactiveComparer NullOrInactive => NullExtensions.NullOrInactive;

		/// <summary>
		/// Reset state to default values.
		/// <para>
		/// <see cref="OnReset"/> is called when the user selects Reset in the Inspector context menu or creating an instance for the first time.
		/// </para>
		/// <para>
		/// This function is only called in edit mode.
		/// </para>
		/// </summary>
		protected virtual void OnReset() { }

		/// <summary>
		/// <see cref="OnAwake"/> is called when the script instance is being loaded during the <see cref="Awake"/> event after the <see cref="Init"/> function has finished.
		/// This happens as the game is launched and is similar to MonoBehavior.Awake.
		/// <para>
		/// Use <see cref="OnAwake"/> to initialize variables or states before the application starts.
		/// </para>
		/// <para>
		/// Unity calls <see cref="OnAwake"/> only once during the lifetime of the script instance.
		/// A script's lifetime lasts until it is manually destroyed using <see cref="UnityEngine.Object.Destroy"/>.
		/// </para>
		/// <para>
		/// <see cref="OnAwake"/> is always called before any Start functions. This allows you to order initialization of scripts.
		/// <see cref="OnAwake"/> can not act as a coroutine.
		/// </para>
		/// <para>
		/// Note: Use <see cref="OnAwake"/> instead of the constructor for initialization, as the serialized state of the <see cref="ScriptableObject"/> is undefined at construction time.
		/// </para>
		/// </summary>
		protected virtual void OnAwake() { }

		bool IInitializable.HasInitializer => initializer != null;

		bool IInitializable.Init(Context context)
		{
			#if UNITY_EDITOR
			if(context == Context.EditMode && initializer is IInitializable initializable)
			{
				return initializable.Init(context);
			}
			#endif
			
			if(initializer is IInitializer iinitializer)
			{
				iinitializer.InitTarget();
				return true;
			}

			return false;
		}

		#if UNITY_EDITOR
		private void Reset()
        {
			if(InitArgs.TryGet(Context.Reset, this, out TFirstArgument firstArgument, out TSecondArgument secondArgument))
			{
				#if DEBUG
				initializing = true;
				#endif

                Init(firstArgument, secondArgument);

				#if DEBUG
				initializing = false;
				#endif
			}

			OnReset();
        }
		#endif

        private void Awake()
		{
			#if !UNITY_EDITOR
			if(!Internal.ServiceInjector.ServicesAreReady)
			{
				Internal.ServiceInjector.ServicesBecameReady += Awake;
				return;
			}
			#endif

			#if !UNITY_EDITOR
			if(initializer is IInitializer iinitializer)
			#else
			if(initializer is IInitializer iinitializer && Application.isPlaying)
			#endif
			{
				iinitializer.InitTarget();

				#if !UNITY_EDITOR
				Destroy(initializer);
				initializer = null;
				#endif
			}
			else if(InitArgs.TryGet(Context.Awake, this, out TFirstArgument firstArgument, out TSecondArgument secondArgument))
			{
				#if DEBUG
				initializing = true;
				ValidateArguments(firstArgument, secondArgument);
				#endif

                Init(firstArgument, secondArgument);

				#if DEBUG
				initializing = false;
				#endif
			}

			OnAwake();
		}

		#if UNITY_EDITOR
		private void OnValidate()
		{
			Validate();

			if(initializer == null)
			{
				return;
			}

			var initializersGenericArguments = initializer.GetType().GetInterfaces().Where(interfaceType => interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IInitializer<,,>)).Select(t => t.GetGenericArguments()).ToList();
			if(!initializersGenericArguments.Any())
			{
				Debug.LogWarning($"Initializer {initializer.GetType().Name} is attached to {GetType().Name} at '{AssetDatabase.GetAssetOrScenePath(this)}' but it does not implement IInitializer<{GetType().Name}, {typeof(TFirstArgument).Name}, {typeof(TSecondArgument)}>.", this);
				return;
			}

			if(!initializersGenericArguments.Any(args => args[0].IsInstanceOfType(this)))
			{
				Debug.LogWarning($"Initializer {initializer.GetType().Name} of {GetType().Name} at '{AssetDatabase.GetAssetOrScenePath(this)}' invalid client type {initializersGenericArguments.First()[0].Name}: not assignable from {GetType().Name}.", this);
				return;
			}

			if(!initializersGenericArguments.Any(args => typeof(TFirstArgument).IsAssignableFrom(args[1])))
			{
				Debug.LogWarning($"Initializer {initializer.GetType().Name} of {GetType().Name} at '{AssetDatabase.GetAssetOrScenePath(this)}' invalid argument type {initializersGenericArguments.First()[1].Name}: not assignable to {typeof(TFirstArgument).Name}.", this);
			}

			if(!initializersGenericArguments.Any(args => typeof(TSecondArgument).IsAssignableFrom(args[2])))
			{
				Debug.LogWarning($"Initializer {initializer.GetType().Name} of {GetType().Name} at '{AssetDatabase.GetAssetOrScenePath(this)}' invalid argument type {initializersGenericArguments.First()[2].Name}: not assignable to {typeof(TSecondArgument).Name}.", this);
			}
		}
		#endif

		[Conditional("UNITY_EDITOR")]
		protected virtual void Validate() { }

		/// <inheritdoc/>
		void IInitializable<TFirstArgument, TSecondArgument>.Init(TFirstArgument firstArgument, TSecondArgument secondArgument)
        {
			#if DEBUG
			initializing = true;
			ValidateArguments(firstArgument, secondArgument);
			#endif

            Init(firstArgument, secondArgument);

			#if DEBUG
			initializing = false;
			#endif
		}

		internal void InitInternal(TFirstArgument firstArgument, TSecondArgument secondArgument)
        {
			#if DEBUG
			initializing = true;
			ValidateArguments(firstArgument, secondArgument);
			#endif

			Init(firstArgument, secondArgument);

			#if DEBUG
			initializing = false;
			#endif
		}

		/// <summary>
		/// Method that can be overridden and used to validate the initialization arguments that were received by this object.
		/// <para>
		/// You can use the <see cref="ThrowIfNull"/> method to throw an <see cref="ArgumentNullException"/>
		/// if an argument is <see cref="Null">null</see>.
		/// <example>
		/// <code>
		/// protected override void ValidateArguments(IInputManager inputManager, Camera camera)
		/// {
		///		ThrowIfNull(inputManager);
		///		ThrowIfNull(camera);
		/// }
		/// </code>
		/// </example>
		/// </para>
		/// <para>
		/// You can use the <see cref="AssertNotNull"/> method to log an assertion to the Console
		/// if an argument is <see cref="Null">null</see>.
		/// <example>
		/// <code>
		/// protected override void ValidateArguments(IInputManager inputManager, Camera camera)
		/// {
		///		AssertNotNull(inputManager);
		///		AssertNotNull(camera);
		/// }
		/// </code>
		/// </example>
		/// </para>
		/// <para>
		/// Calls to this method are ignored in non-development builds.
		/// </para>
		/// </summary>
		/// <param name="firstArgument"> The first received argument to validate. </param>
		/// <param name="secondArgument"> The second received argument to validate. </param>
		[Conditional("DEBUG")]
		protected virtual void ValidateArguments(TFirstArgument firstArgument, TSecondArgument secondArgument) { }

		/// <summary>
		/// Checks if the <paramref name="argument"/> is <see langword="null"/> and throws
		/// an <see cref="ArgumentNullException"/> if it is.
		/// <para>
		/// This method call is ignored in non-development builds.
		/// </para>
		/// </summary>
		/// <param name="argument"> The argument to test. </param>
		[Conditional("DEBUG")]
		protected void ThrowIfNull(TFirstArgument argument)
		{
			#if DEBUG
			if(argument != Null)
            {
				return;
            }

			throw new ArgumentNullException(null, $"First argument passed to {GetType().Name} was null.");
			#endif
		}

		/// <summary>
		/// Checks if the <paramref name="argument"/> is <see langword="null"/> and logs
		/// an assertion message to the console if it is.
		/// <para>
		/// This method call is ignored in non-development builds.
		/// </para>
		/// </summary>
		/// <param name="argument"> The argument to test. </param>
		[Conditional("DEBUG")]
		protected void AssertNotNull(TFirstArgument argument)
		{
			#if DEBUG
			if(argument != Null)
            {
				return;
            }

			Debug.LogAssertion($"First argument passed to {GetType().Name} was null.", this);
			#endif
		}

		/// <summary>
		/// Checks if the <paramref name="argument"/> is <see langword="null"/> and throws
		/// an <see cref="ArgumentNullException"/> if it is.
		/// <para>
		/// This method call is ignored in non-development builds.
		/// </para>
		/// </summary>
		/// <param name="argument"> The argument to test. </param>
		[Conditional("DEBUG")]
		protected void ThrowIfNull(TSecondArgument argument)
		{
			#if DEBUG
			if(argument == Null) throw new ArgumentNullException(null, $"Second argument passed to {GetType().Name} was null.");
			#endif
		}

		/// <summary>
		/// Checks if the <paramref name="argument"/> is <see langword="null"/> and logs
		/// an assertion message to the console if it is.
		/// <para>
		/// This method call is ignored in non-development builds.
		/// </para>
		/// </summary>
		/// <param name="argument"> The argument to test. </param>
		[Conditional("DEBUG")]
		protected void AssertNotNull(TSecondArgument argument)
		{
			#if DEBUG
			if(argument == Null) Debug.LogAssertion($"Second argument passed to {GetType().Name} was null.", this);
			#endif
		}
	}
}