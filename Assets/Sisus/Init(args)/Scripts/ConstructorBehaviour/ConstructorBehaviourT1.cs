﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using static Sisus.Init.Internal.InitializerUtility;
using static Sisus.Init.Reflection.InjectionUtility;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#if UNITY_2021_1_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
#endif

namespace Sisus.Init
{
    /// <summary>
    /// A base class for <see cref="MonoBehaviour">MonoBehaviours</see> that depend on receiving one argument in their constructor during initialization.
    /// <para>
    /// Instances can be
    /// <see cref="ObjectExtensions.Instantiate{TArgument}">instantiated</see>
    /// or <see cref="GameObjectExtensions.AddComponent{TComponent, TArgument}">added</see>
    /// to a <see cref="GameObject"/> with one argument passed to the constructor of the created instance.
    /// </para>
    /// <para>
    /// If an object depends exclusively on classes that implement <see cref="IService"/> then it will be able to receive them
    /// in its constructor automatically.
    /// </para>
    /// <para>
    /// Instances of classes inheriting from <see cref="ConstructorBehaviour{TArgument}"/> receive the argument
    /// in their default constructor where it can be assigned to a member field or property - including one that is read-only.
    /// </para>
    /// </summary>
    /// <typeparam name="TArgument"> Type of the object that the <see cref="Component"/> depends on. </typeparam>
	/// <seealso cref="MonoBehaviour{TArgument}"/>
    public abstract class ConstructorBehaviour<TArgument> : MonoBehaviour, IInitializable<TArgument>, ISerializationCallbackReceiver
	{
		private static readonly ConcurrentDictionary<Type, string> initPropertyNamesCache = new ConcurrentDictionary<Type, string>();
		private static readonly ConcurrentDictionary<Type, bool> isAnyInitPropertySerializedCache = new ConcurrentDictionary<Type, bool>();

		/// <summary>
		/// <see langword="true"/> if this object received the argument that it depends on in the constructor
		/// or if it was injected to it through the <see cref="Init"/> function; otherwise, <see langword="false"/>.
		/// <para>
		/// Note that this is set to <see langword="true"/> if it receives the argument regardless of whether or not
		/// the argument is <see langword="null"/> or not.
		/// </para>
		/// </summary>
		private bool dependenciesReceived;

		#if UNITY_EDITOR
		private DateTime dependenciesReceivedTimeStamp = DateTime.MinValue;
		#endif

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
		/// Initializes the new instance of the <see cref="ConstructorBehaviour{TArgument}"/> class and retrieves its dependencies.
		/// <para>
		/// Classes that inherit from <see cref="ConstructorBehaviour{TArgument}"/> should always implement the parameterless
		/// default constructor, chain it to this constructor using the <see langword="base"/> keyword and then in the body of the
		/// constructor assign the value of <paramref name="argument"/> to a read-only field or property.
		/// </para>
		/// <example>
		/// <code>
		/// public class Actor : ConstructorBehaviour<IActorMotor>
		/// {
		///		public readonly IActorMotor actorMotor;
		/// 
		///		public Actor() : base(out var actorMotor)
		///		{
		///			this.actorMotor = actorMotor;
		///		}
		/// 	
		///		private void Update()
		///		{
		///			actorMotor.Update(this);
		///		}
		/// }
		/// </code>
		/// </example>
		/// </summary>
		/// <param name="argument"> The argument passed to this <see cref="Component"/>. </param>
		/// <exception cref="MissingInitArgumentsException">
		/// Thrown if argument was not provided for the object using <see cref="InitArgs.Set{TClient, TArgument}"/> prior to the constructor being called.
		/// </exception>
		protected ConstructorBehaviour(out TArgument argument)
		{
			dependenciesReceived = InitArgs.TryGet(Context.Constructor, this, out argument);

			#if UNITY_EDITOR
			if(dependenciesReceived) dependenciesReceivedTimeStamp = DateTime.UtcNow;
			#endif

			#if DEBUG
			if(!dependenciesReceived)
            {
				return;
			}

			#if UNITY_EDITOR
			if(EditorOnly.ThreadSafe.Application.IsExitingPlayMode)
            {
				return;
            }
			#endif

			ValidateArgument(argument);
			#endif
		}

		/// <summary>
		/// Default constructor of <see cref="ConstructorBehaviour"/> which should never get called.
		/// <para>
		/// Classes that inherit from <see cref="ConstructorBehaviour{TArgument}"/> should always implement the parameterless
		/// default constructor and chain it to the <see cref="ConstructorBehaviour{TArgument}(out TArgument)"/> constructor using
		/// the <see langword="base"/> keyword.
		/// </para>
		/// </summary>
		/// <exception cref="NotSupportedException"> Always thrown if this constructor is called. </exception>
		private ConstructorBehaviour()
		{
			throw new NotSupportedException(GetType().Name + " invalid constructor called. Classes that derive from ConstructorBehaviour<TArgument> must implement a parameterless default constructor and chain it to the base constructor that contains the out keyword.");
		}

		/// <inheritdoc/>
		void IInitializable<TArgument>.Init(TArgument argument) => Init(argument);

		private void Init(TArgument argument)
		{
			string field = GetInitArgumentClassMemberName();
			Inject(this, field, argument);

			dependenciesReceived = true;
			#if UNITY_EDITOR
			dependenciesReceivedTimeStamp = DateTime.UtcNow;
			#endif

			ValidateArgument(argument);
		}

		/// <summary>
		/// Method that can be overridden and used to validate the initialization argument that was received by this object.
		/// <para>
		/// You can use the <see cref="ThrowIfNull"/> method to throw an <see cref="ArgumentNullException"/>
		/// if the argument is <see cref="Null">null</see>.
		/// <example>
		/// <code>
		/// protected override void ValidateArgument(IInputManager inputManager)
		/// {
		///		ThrowIfNull(inputManager);
		/// }
		/// </code>
		/// </example>
		/// </para>
		/// <para>
		/// You can use the <see cref="AssertNotNull"/> method to log an assertion to the Console
		/// if the argument is <see cref="Null">null</see>.
		/// <example>
		/// <code>
		/// protected override void ValidateArgument(IInputManager inputManager)
		/// {
		///		AssertNotNull(inputManager);
		/// }
		/// </code>
		/// </example>
		/// </para>
		/// <para>
		/// Calls to this method are ignored in non-development builds.
		/// </para>
		/// </summary>
		/// <param name="argument"> The received argument to validate. </param>
		[Conditional("DEBUG")]
		protected virtual void ValidateArgument(TArgument argument) { }

		/// <summary>
		/// Gets the name of the field or property into which the
		/// <see cref="IInitializable{TArgument}.Init">Init</see>
		/// argument accepted by this object is assigned to in the constructor of this object.
		/// <para>
		/// By default uses reflection to figure out the name by searching for fields and properties with a
		/// name matching the one used in the constructor, or failing that, type being an exact match with the argument type.
		/// </para>
		/// <para>
		/// This method can also be overridden to manually specify the name.
		/// <example>
		/// <code>
		/// protected override string GetInitArgumentClassMemberName()
		/// {
		///		return nameof(inputHandler);
		/// }
		/// </code>
		/// </example>
		/// </para>
		/// </summary>
		/// <returns> The names of five instance fields or properties. </returns>
		/// <exception cref="MissingMemberException">
		/// Thrown if no field or property is found with their name closely matching that of the constructor parameter
		/// nor is any field or property found with their type matching the type of the parameter exactly.
		/// </exception>
		/// <exception cref="System.Reflection.AmbiguousMatchException">
		/// Thrown if no field or property is found with their name closely matching that of the constructor parameter
		/// and more than one field or property is found with their type matching the type of the parameter exactly.
		/// </exception>
		protected virtual string GetInitArgumentClassMemberName()
		{
			if(initPropertyNamesCache.TryGetValue(GetType(), out string name))
            {
				return name;
			}

			var type = GetType();

			try
			{
				name = (GetConstructorArgumentTargetFieldName(type, typeof(TArgument)));
				initPropertyNamesCache[type] = name;
			}
			catch
			{
				foreach(var initializerType in GetInitializerTypes(this))
				{
					if(!(initializerType.GetNestedType(InitArgumentMetadataClassName) is Type init))
					{
						continue;
					}

					try
					{
						name = GetConstructorArgumentTargetFieldName(init, typeof(TArgument));
						initPropertyNamesCache[type] = name;
						return name;
					}
					catch
					{
						break;
					}
				}

				throw;
			}

			return name;
		}

		/// <summary>
		/// <see cref="OnAwake"/> is called when the script instance is being loaded during the <see cref="Awake"/> event after the <see cref="Init"/> function has finished.
		/// <para>
		/// <see cref="OnAwake"/> is called either when an active <see cref="GameObject"/> that contains the script is initialized when a <see cref="UnityEngine.SceneManagement.Scene">Scene</see> loads,
		/// or when a previously <see cref="GameObject.activeInHierarchy">inactive</see> <see cref="GameObject"/> is set active, or after a <see cref="GameObject"/> created with <see cref="Object.Instantiate"/>
		/// is initialized.
		/// </para>
		/// <para>
		/// Use <see cref="OnAwake"/> to initialize variables or states before the application starts.
		/// </para>
		/// <para>
		/// Unity calls <see cref="OnAwake"/> only once during the lifetime of the script instance. A script's lifetime lasts until the Scene that contains it is unloaded.
		/// If the Scene is loaded again, Unity loads the script instance again, so <see cref="OnAwake"/> will be called again.
		/// If the Scene is loaded multiple times additively, Unity loads several script instances, so <see cref="OnAwake"/> will be called several times (once on each instance).
		/// </para>
		/// <para>
		/// For active <see cref="GameObject">GameObjects</see> placed in a Scene, Unity calls <see cref="OnAwake"/> after all active <see cref="GameObject">GameObjects</see>
		/// in the Scene are initialized, so you can safely use methods such as <see cref="GameObject.FindWithTag"/> to query other <see cref="GameObject">GameObjects</see>.
		/// </para>
		/// <para>
		/// The order that Unity calls each <see cref="GameObject"/>'s Awake (and by extension <see cref="OnAwake"/>) is not deterministic.
		/// Because of this, you should not rely on one <see cref="GameObject"/>'s Awake being called before or after another
		/// (for example, you should not assume that a reference assigned by one GameObject's <see cref="OnAwake"/> will be usable in another GameObject's <see cref="Awake"/>).
		/// Instead, you should use <see cref="Awake"/>/<see cref="OnAwake"/> to set up references between scripts, and use Start, which is called after all <see cref="Awake"/>
		/// and <see cref="OnAwake"/> calls are finished, to pass any information back and forth.
		/// </para>
		/// <para>
		/// <see cref="OnAwake"/> is always called before any Start functions. This allows you to order initialization of scripts.
		/// <see cref="OnAwake"/> is called even if the script is a disabled component of an active GameObject.
		/// <see cref="OnAwake"/> can not act as a coroutine.
		/// </para>
		/// <para>
		/// Note: Use <see cref="OnAwake"/> instead of the constructor for any initialization logic beyond assigining the received arguments into fields and properties,
		/// as the serialized state of the <see cref="Component"/> is undefined at construction time, and many classes in Unity are not thread safe and as such
		/// are not supported being called from <see cref="Component"/> constructors.
		/// </para>
		/// </summary>
		protected virtual void OnAwake() { }

        private void Awake()
        {
			if(!dependenciesReceived)
			{
				// Retry fetching argument in Awake because scene services defined in Services components
                // can't always be fetched in the constructor or in OnAfterDeserialize because examining
                // the parent chains can only be done from the main thread.
				if(InitArgs.TryGet(Context.Awake, this, out TArgument argument))
				{
					Init(argument);
				}
				else
				{
					#if UNITY_EDITOR
					if(!Application.isPlaying || PrefabUtility.IsPartOfPrefabAsset(this) || PrefabStageUtility.GetPrefabStage(gameObject) != null)
					{
						return;
					}
					#endif

					DestroyImmediate(this);
					throw new MissingInitArgumentsException(this);
				}
			}

			OnAwake();
		}

        void ISerializationCallbackReceiver.OnBeforeSerialize() => OnBeforeSerialize();

        protected virtual void OnBeforeSerialize() { }

		void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
			if(dependenciesReceived && GetIsInitPropertySerialized() && InitArgs.TryGet(Context.OnAfterDeserialize, this, out TArgument argument))
            {
				OnAfterDeserialize(new Arg<TArgument>(argument));
			}
			else
            {
				OnAfterDeserialize(Arg<TArgument>.None);
			}
        }

		protected virtual void OnAfterDeserialize(Arg<TArgument> argument)
		{
			if(argument.provided)
            {
				Init(argument.argument);
			}
		}

		/// <summary>
		/// Reset state to default values.
		/// <para>
		/// <see cref="OnReset"/> is called when the user selects Reset in the Inspector context menu or when adding the component the first time.
		/// </para>
		/// <para>
		/// This function is only called in edit mode.
		/// </para>
		/// </summary>
		protected virtual void OnReset() { }

		#if UNITY_EDITOR
		private void Reset()
		{
			// Skip InitOnReset handling if a new instance was just created in edit mode, and it already received
			// its dependencies via InitArgs.TryGet in the constructor. Without this, Reset would always overwrite
			// any dependencies that were manually passed via AddComponent or Instantiate, when the component has
			// the InitOnResetAttribute.
			if((!dependenciesReceived || (DateTime.UtcNow - dependenciesReceivedTimeStamp).TotalMilliseconds > 1000)
				&& InitArgs.TryGet(Context.Reset, this, out TArgument argument))
			{
				Init(argument);
			}

			OnInitializableReset(this);
			OnReset();
		}
		#endif

		private bool GetIsInitPropertySerialized()
        {
			if(isAnyInitPropertySerializedCache.TryGetValue(GetType(), out bool isAnyInitPropertySerialized))
            {
				return isAnyInitPropertySerialized;
            }

			string field = GetInitArgumentClassMemberName();
			var type = GetType();
			isAnyInitPropertySerialized = IsClassMemberSerialized(type, field);
			isAnyInitPropertySerializedCache[type] = isAnyInitPropertySerialized;
			return isAnyInitPropertySerialized;
		}

		private bool TryGetConstructorParameterName(out string parameterName)
        {
			try
			{
				parameterName = GetConstructorArgumentName(GetInitArgumentClassMemberName());
				return true;
			}
			catch
            {
				parameterName = null;
				return false;
            }
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
		protected void ThrowIfNull(TArgument argument)
		{
			#if DEBUG
			if(!dependenciesReceived || argument != Null)
            {
				return;
            }

			TryGetConstructorParameterName(out string parameterName);
			throw new ArgumentNullException(parameterName, $"Argument passed to {GetType().Name} was null.");
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
		protected void AssertNotNull(TArgument argument)
		{
			#if DEBUG
			if(!dependenciesReceived || argument != Null)
            {
				return;
            }

			string suffix = TryGetConstructorParameterName(out string parameterName) ? "\nParameter name: " + parameterName : "";
			Debug.LogAssertion($"Argument passed to {GetType().Name} was null.{suffix}", this);
			#endif
		}
	}
}