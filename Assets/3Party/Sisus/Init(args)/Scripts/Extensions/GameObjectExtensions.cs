using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Sisus.Init
{
	/// <summary>
	/// Extensions methods for <see cref="GameObject"/> that can be used to <see cref="AddComponent">add components</see>
	/// that implement one of the <see cref="IArgs{TArgument}">IArgs</see> interfaces
	/// with the required dependencies passed to the component's <see cref="IInitializable{TArgument}.Init">Init</see> function.
	/// </summary>
	public static class GameObjectExtensions
	{
		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TArgument}.Init">initializes</see> the component using the provided <paramref name="argument"/>.
		/// <para>
		/// The argument should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TArgument}"/> interface the argument can be
		/// provided using the <see cref="IInitializable{TArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TArgument"> Type of the argument passed to the component's <see cref="IArgs{TArgument}">Init</see> function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="argument"> The argument passed to the component's <see cref="IArgs{TArgument}">Init</see> function. </param>
		/// <returns> The added component. </returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TArgument}"/> and did not manually handle receiving the provided argument. 
		/// </exception>
		public static TComponent AddComponent<TComponent, TArgument>([DisallowNull] this GameObject gameObject, TArgument argument)
			where TComponent : MonoBehaviour, IArgs<TArgument>
		{
			#if DEBUG
			if(gameObject == null)
			{
				throw new ArgumentNullException($"The GameObject to which you want to add the component {nameof(TComponent)} is null.");
			}
			#endif

			InitArgs.Set<TComponent, TArgument>(argument);
			var client = gameObject.AddComponent<TComponent>();

			if(!InitArgs.Clear<TComponent, TArgument>())
			{
				return client;
			}

			if(client is IInitializable<TArgument> initializable)
			{
				initializable.Init(argument);
				return client;
			}

			throw new InitArgumentsNotReceivedException(nameof(AddComponent), typeof(TComponent));
		}

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TArgument}.Init">initializes</see> the component using a <see cref="ServiceAttribute">service</see>
		/// of type <typeparamref name="TArgument"/>.
		/// <para>
		/// The argument should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TArgument}"/> interface the argument can be
		/// provided using the <see cref="IInitializable{TArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TArgument"> Type of the argument passed to the component's <see cref="IArgs{TArgument}">Init</see> function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <returns> The added component. </returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="MissingInitArgumentsException">
		/// Thrown if no service of type <typeparamref name="TArgument"/> was found that is accessible to the <paramref name="gameObject"/>.
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TArgument}"/> and did not manually handle receiving the provided argument. 
		/// </exception>
		public static TComponent AddComponent<TComponent, TArgument>([DisallowNull] this GameObject gameObject)
			where TComponent : MonoBehaviour, IArgs<TArgument>
		{
			#if DEBUG
			if(gameObject == null)
			{
				throw new ArgumentNullException(nameof(gameObject), $"The game object to which you tried to attach the component {nameof(TComponent)} is null.");
			}
			#endif

			if(!Service.TryGetFor(gameObject, out TArgument argument))
			{
				if(typeof(TArgument).IsInterface)
				{
					throw new MissingInitArgumentsException($"Attempted to attach a component of type {typeof(TComponent).Name} to '{gameObject.name}' but failed to locate service of type {typeof(TArgument).Name}. Add the [Service(typeof({typeof(TArgument).Name}))] attribute to a class that implements {typeof(TArgument).Name}, or use GameObject.AddComponent<{typeof(TComponent).Name}, {typeof(TArgument).Name}> to manually pass in the service instance.");
				}
				
				if(typeof(TArgument).IsAbstract)
				{
					throw new MissingInitArgumentsException($"Attempted to attach  a component of type {typeof(TComponent).Name} to '{gameObject.name}' but failed to locate service of type {typeof(TArgument).Name}. Add the [Service(typeof({typeof(TArgument).Name}))] attribute to a class that derives from {typeof(TArgument).Name}, or use GameObject.AddComponent<{typeof(TComponent).Name}, {typeof(TArgument).Name}> to manually pass in the service instance.");
				}

				throw new MissingInitArgumentsException($"Attempted to attach a component of type {typeof(TComponent).Name} to '{gameObject.name}' but failed to locate service of type {typeof(TArgument).Name}. Add the [Service(typeof({typeof(TArgument).Name}))] attribute to the {typeof(TArgument).Name} class, or use GameObject.AddComponent<{typeof(TComponent).Name}, {typeof(TArgument).Name}> to manually pass in the service instance.");
			}

			InitArgs.Set<TComponent, TArgument>(argument);
			var client = gameObject.AddComponent<TComponent>();

			if(!InitArgs.Clear<TComponent, TArgument>())
			{
				return client;
			}

			if(client is IInitializable<TArgument> initializable)
			{
				initializable.Init(argument);
				return client;
			}

			throw new InitArgumentsNotReceivedException(nameof(AddComponent), typeof(TComponent));
		}

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TFirstArgument, TSecondArgument}.Init">initializes</see> the component using the provided arguments.
		/// <para>
		/// Arguments should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TFirstArgument, TSecondArgument}"/> interface the arguments can be
		/// provided using the <see cref="IInitializable{TFirstArgument, TSecondArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TFirstArgument, TSecondArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TFirstArgument"> Type of the first argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TSecondArgument"> Type of the second argument passed to the component's Init function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="firstArgument"> The first argument passed to the component's Init function. </param>
		/// <param name="secondArgument"> The second argument passed to the component's Init function. </param>
		/// <returns> The added component. </returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TFirstArgument, TSecondArgument}"/> and did not manually handle receiving the provided arguments. 
		/// </exception>
		public static TComponent AddComponent<TComponent, TFirstArgument, TSecondArgument>
			([DisallowNull] this GameObject gameObject, TFirstArgument firstArgument, TSecondArgument secondArgument)
				where TComponent : MonoBehaviour, IArgs<TFirstArgument, TSecondArgument>
		{
			#if DEBUG
			if(gameObject == null)
			{
				throw new ArgumentNullException($"The GameObject to which you want to add the component {nameof(TComponent)} is null.");
			}
			#endif

			InitArgs.Set<TComponent, TFirstArgument, TSecondArgument>(firstArgument, secondArgument);

			var client =
			#if UNITY_EDITOR
				Application.isPlaying
				? gameObject.AddComponent<TComponent>()
				: UnityEditor.Undo.AddComponent<TComponent>(gameObject);
			#else
				gameObject.AddComponent<TComponent>();
			#endif

			if(!InitArgs.Clear<TComponent, TFirstArgument, TSecondArgument>())
			{
				return client;
			}

			if(client is IInitializable<TFirstArgument, TSecondArgument> initializable)
			{
				initializable.Init(firstArgument, secondArgument);
				return client;
			}

			throw new InitArgumentsNotReceivedException(nameof(AddComponent), typeof(TComponent));
		}

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument}.Init">initializes</see> the component using the provided arguments.
		/// <para>
		/// Arguments should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument}"/> interface the arguments can be
		/// provided using the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TFirstArgument, TSecondArgument, TThirdArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TFirstArgument"> Type of the first argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TSecondArgument"> Type of the second argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TThirdArgument"> Type of third argument which is passed to the component's Init function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="firstArgument"> The first argument passed to the component's Init function. </param>
		/// <param name="secondArgument"> The second argument passed to the component's Init function. </param>
		/// <param name="thirdArgument"> The third argument passed to the component's Init function. </param>
		/// <returns> The added component. </returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument}"/>
		/// and did receive the arguments during initialization. 
		/// </exception>
		public static TComponent AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument>
			([DisallowNull] this GameObject gameObject, TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument)
				where TComponent : MonoBehaviour, IArgs<TFirstArgument, TSecondArgument, TThirdArgument>
		{
			#if DEBUG
			if(gameObject == null)
			{
				throw new ArgumentNullException($"The GameObject to which you want to add the component {nameof(TComponent)} is null.");
			}
			#endif

			InitArgs.Set<TComponent, TFirstArgument, TSecondArgument, TThirdArgument>(firstArgument, secondArgument, thirdArgument);
			var client = gameObject.AddComponent<TComponent>();

			if(!InitArgs.Clear<TComponent, TFirstArgument, TSecondArgument, TThirdArgument>())
			{
				return client;
			}

			if(client is IInitializable<TFirstArgument, TSecondArgument, TThirdArgument> initializable)
			{
				initializable.Init(firstArgument, secondArgument, thirdArgument);
				return client;
			}

			throw new InitArgumentsNotReceivedException(nameof(AddComponent), typeof(TComponent));
		}

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}.Init">initializes</see> the component using the provided arguments.
		/// <para>
		/// Arguments should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}"/> interface the arguments can be
		/// provided using the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TFirstArgument"> Type of the first argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TSecondArgument"> Type of the second argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TThirdArgument"> Type of third argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TFourthArgument"> Type of fourth argument which is passed to the component's Init function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="firstArgument"> The first argument passed to the component's Init function. </param>
		/// <param name="secondArgument"> The second argument passed to the component's Init function. </param>
		/// <param name="thirdArgument"> The third argument passed to the component's Init function. </param>
		/// <param name="fourthArgument"> The fourth argument passed to the component's Init function. </param>
		/// <returns> The added component. </returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}"/> and did not manually handle receiving the provided arguments. 
		/// </exception>
		public static TComponent AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>
			([DisallowNull] this GameObject gameObject, TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument)
				where TComponent : MonoBehaviour, IArgs<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>
		{
			#if DEBUG
			if(gameObject == null)
			{
				throw new ArgumentNullException($"The GameObject to which you want to add the component {nameof(TComponent)} is null.");
			}
			#endif

			InitArgs.Set<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>(firstArgument, secondArgument, thirdArgument, fourthArgument);
			var client = gameObject.AddComponent<TComponent>();

			if(!InitArgs.Clear<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>())
			{
				return client;
			}

			if(client is IInitializable<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument> initializable)
			{
				initializable.Init(firstArgument, secondArgument, thirdArgument, fourthArgument);
				return client;
			}

			throw new InitArgumentsNotReceivedException(nameof(AddComponent), typeof(TComponent));
		}

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}.Init">initializes</see> the component using the provided arguments.
		/// <para>
		/// Arguments should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}"/> interface the arguments can be
		/// provided using the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TFirstArgument"> Type of the first argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TSecondArgument"> Type of the second argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TThirdArgument"> Type of third argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TFourthArgument"> Type of fourth argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TFifthArgument"> Type of fifth argument which is passed to the component's Init function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="firstArgument"> The first argument passed to the component's Init function. </param>
		/// <param name="secondArgument"> The second argument passed to the component's Init function. </param>
		/// <param name="thirdArgument"> The third argument passed to the component's Init function. </param>
		/// <param name="fourthArgument"> The fourth argument passed to the component's Init function. </param>
		/// <param name="fifthArgument"> The fifth argument passed to the component's Init function. </param>
		/// <returns> The added component. </returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}"/> and did not manually handle receiving the provided arguments. 
		/// </exception>
		public static TComponent AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument>
			([DisallowNull] this GameObject gameObject, TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument)
				where TComponent : MonoBehaviour, IArgs<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument>
		{
			#if DEBUG
			if(gameObject == null)
			{
				throw new ArgumentNullException($"The GameObject to which you want to add the component {nameof(TComponent)} is null.");
			}
			#endif

			InitArgs.Set<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument>(firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument);
			var client = gameObject.AddComponent<TComponent>();

			if(!InitArgs.Clear<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument>())
			{
				return client;
			}

			if(client is IInitializable<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument> initializable)
			{
				initializable.Init(firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument);
				return client;
			}

			throw new InitArgumentsNotReceivedException(nameof(AddComponent), typeof(TComponent));
		}

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument}.Init">initializes</see> the component using the provided arguments.
		/// <para>
		/// Arguments should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument}"/> interface the arguments can be
		/// provided using the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TFirstArgument"> Type of the first argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TSecondArgument"> Type of the second argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TThirdArgument"> Type of third argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TFourthArgument"> Type of fourth argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TFifthArgument"> Type of fifth argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TSixthArgument"> Type of sixth argument which is passed to the component's Init function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="firstArgument"> The first argument passed to the component's Init function. </param>
		/// <param name="secondArgument"> The second argument passed to the component's Init function. </param>
		/// <param name="thirdArgument"> The third argument passed to the component's Init function. </param>
		/// <param name="fourthArgument"> The fourth argument passed to the component's Init function. </param>
		/// <param name="fifthArgument"> The fifth argument passed to the component's Init function. </param>
		/// <param name="sixthArgument"> The sixth argument passed to the component's Init function. </param>
		/// <returns> The added component. </returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument}"/> and did not manually handle receiving the provided arguments. 
		/// </exception>
		public static TComponent AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>
			([DisallowNull] this GameObject gameObject, TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument, TSixthArgument sixthArgument)
				where TComponent : MonoBehaviour, IArgs<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>
		{
			#if DEBUG
			if(gameObject == null)
			{
				throw new ArgumentNullException($"The GameObject to which you want to add the component {nameof(TComponent)} is null.");
			}
			#endif

			InitArgs.Set<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>(firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument);
			var client = gameObject.AddComponent<TComponent>();

			if(!InitArgs.Clear<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>())
			{
				return client;
			}

			if(client is IInitializable<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument> initializable)
			{
				initializable.Init(firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument);
				return client;
			}

			throw new InitArgumentsNotReceivedException(nameof(AddComponent), typeof(TComponent));
		}

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TArgument}.Init">initializes</see> the component using the provided argument.
		/// <para>
		/// The argument should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TArgument}"/> interface the argument can be
		/// provided using the <see cref="IInitializable{TArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the argument will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TArgument"> Type of the argument passed to the component's Init function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="component">
		/// When this method returns, contains the component of type <typeparamref name="TComponent"/> that was added to the <paramref name="gameObject"/>.
		/// This parameter is passed uninitialized.
		/// </param>
		/// <param name="argument"> The argument passed to the component's Init function. </param>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TArgument}"/> and did not manually handle receiving the provided arguments. 
		/// </exception>
		public static void AddComponent<TComponent, TArgument>
			([DisallowNull] this GameObject gameObject, out TComponent component, TArgument argument)
				where TComponent : MonoBehaviour, IArgs<TArgument>
				 => component = gameObject.AddComponent<TComponent, TArgument>(argument);

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TFirstArgument, TSecondArgument}.Init">initializes</see> the component using the provided arguments.
		/// <para>
		/// Arguments should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TFirstArgument, TSecondArgument}"/> interface the arguments can be
		/// provided using the <see cref="IInitializable{TFirstArgument, TSecondArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TFirstArgument, TSecondArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TFirstArgument"> Type of the first argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TSecondArgument"> Type of the second argument passed to the component's Init function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="component">
		/// When this method returns, contains the component of type <typeparamref name="TComponent"/> that was added to the <paramref name="gameObject"/>.
		/// This parameter is passed uninitialized.
		/// </param>
		/// <param name="firstArgument"> The first argument passed to the component's Init function. </param>
		/// <param name="secondArgument"> The second argument passed to the component's Init function. </param>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TFirstArgument, TSecondArgument}"/> and did not manually handle receiving the provided arguments. 
		/// </exception>
		public static void AddComponent<TComponent, TFirstArgument, TSecondArgument>
			([DisallowNull] this GameObject gameObject, out TComponent component, TFirstArgument firstArgument, TSecondArgument secondArgument)
				where TComponent : MonoBehaviour, IArgs<TFirstArgument, TSecondArgument>
				 => component = gameObject.AddComponent<TComponent, TFirstArgument, TSecondArgument>(firstArgument, secondArgument);

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument}.Init">initializes</see> the component using the provided arguments.
		/// <para>
		/// Arguments should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument}"/> interface the arguments can be
		/// provided using the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TFirstArgument, TSecondArgument, TThirdArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TFirstArgument"> Type of the first argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TSecondArgument"> Type of the second argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TThirdArgument"> Type of third argument which is passed to the component's Init function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="component">
		/// When this method returns, contains the component of type <typeparamref name="TComponent"/> that was added to the <paramref name="gameObject"/>.
		/// This parameter is passed uninitialized.
		/// </param>
		/// <param name="firstArgument"> The first argument passed to the component's Init function. </param>
		/// <param name="secondArgument"> The second argument passed to the component's Init function. </param>
		/// <param name="thirdArgument"> The third argument passed to the component's Init function. </param>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument}"/> and did not manually handle receiving the provided arguments. 
		/// </exception>
		public static void AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument>
			([DisallowNull] this GameObject gameObject, out TComponent component, TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument)
				where TComponent : MonoBehaviour, IArgs<TFirstArgument, TSecondArgument, TThirdArgument>
				 => component = gameObject.AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument>(firstArgument, secondArgument, thirdArgument);

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}.Init">initializes</see> the component using the provided arguments.
		/// <para>
		/// Arguments should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}"/> interface the arguments can be
		/// provided using the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TFirstArgument"> Type of the first argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TSecondArgument"> Type of the second argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TThirdArgument"> Type of third argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TFourthArgument"> Type of fourth argument which is passed to the component's Init function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="component">
		/// When this method returns, contains the component of type <typeparamref name="TComponent"/> that was added to the <paramref name="gameObject"/>.
		/// This parameter is passed uninitialized.
		/// </param>
		/// <param name="firstArgument"> The first argument passed to the component's Init function. </param>
		/// <param name="secondArgument"> The second argument passed to the component's Init function. </param>
		/// <param name="thirdArgument"> The third argument passed to the component's Init function. </param>
		/// <param name="fourthArgument"> The fourth argument passed to the component's Init function. </param>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}"/> and did not manually handle receiving the provided arguments. 
		/// </exception>
		public static void AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>
			([DisallowNull] this GameObject gameObject, out TComponent component, TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument)
				where TComponent : MonoBehaviour, IArgs<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>
				 => component = gameObject.AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>(firstArgument, secondArgument, thirdArgument, fourthArgument);

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}.Init">initializes</see> the component using the provided arguments.
		/// <para>
		/// Arguments should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}"/> interface the arguments can be
		/// provided using the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TFirstArgument"> Type of the first argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TSecondArgument"> Type of the second argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TThirdArgument"> Type of third argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TFourthArgument"> Type of fourth argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TFifthArgument"> Type of fifth argument which is passed to the component's Init function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="component">
		/// When this method returns, contains the component of type <typeparamref name="TComponent"/> that was added to the <paramref name="gameObject"/>.
		/// This parameter is passed uninitialized.
		/// </param>
		/// <param name="firstArgument"> The first argument passed to the component's Init function. </param>
		/// <param name="secondArgument"> The second argument passed to the component's Init function. </param>
		/// <param name="thirdArgument"> The third argument passed to the component's Init function. </param>
		/// <param name="fourthArgument"> The fourth argument passed to the component's Init function. </param>
		/// <param name="fifthArgument"> The fifth argument passed to the component's Init function. </param>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}"/> and did not manually handle receiving the provided arguments. 
		/// </exception>
		public static void AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument>
			([DisallowNull] this GameObject gameObject, out TComponent component, TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument)
				where TComponent : MonoBehaviour, IArgs<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument>
				 => component = gameObject.AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument>(firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument);

		/// <summary>
		/// Adds a component of type <typeparamref name="TComponent"/> to the <paramref name="gameObject"/>
		/// and <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}.Init">initializes</see> the component using the provided arguments.
		/// <para>
		/// Arguments should either be received by the added component during its initialization (such during the Awake event function or in the constructor)
		/// or if the component class implements the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}"/> interface the arguments can be
		/// provided using the <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}.Init">Init</see> function
		/// immediately after initialization has finished (before the Start event function).
		/// </para>
		/// <para>
		/// For classes deriving from <see cref="MonoBehaviour{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument}"/> the latter method will be used in cases
		/// where the <paramref name="gameObject"/> is <see cref="GameObject.activeSelf">inactive</see>, while if the the <paramref name="gameObject"/> is
		/// inactive the arguments will be received during the Awake event function.
		/// </para>
		/// </summary>
		/// <typeparam name="TComponent"> Type of the component to add. </typeparam>
		/// <typeparam name="TFirstArgument"> Type of the first argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TSecondArgument"> Type of the second argument passed to the component's Init function. </typeparam>
		/// <typeparam name="TThirdArgument"> Type of third argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TFourthArgument"> Type of fourth argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TFifthArgument"> Type of fifth argument which is passed to the component's Init function. </typeparam>
		/// <typeparam name="TSixthArgument"> Type of sixth argument which is passed to the component's Init function. </typeparam>
		/// <param name="gameObject"> The GameObject to which the component is added. </param>
		/// <param name="component">
		/// When this method returns, contains the component of type <typeparamref name="TComponent"/> that was added to the <paramref name="gameObject"/>.
		/// This parameter is passed uninitialized.
		/// </param>
		/// <param name="firstArgument"> The first argument passed to the component's Init function. </param>
		/// <param name="secondArgument"> The second argument passed to the component's Init function. </param>
		/// <param name="thirdArgument"> The third argument passed to the component's Init function. </param>
		/// <param name="fourthArgument"> The fourth argument passed to the component's Init function. </param>
		/// <param name="fifthArgument"> The fifth argument passed to the component's Init function. </param>
		/// <param name="sixthArgument"> The sixth argument passed to the component's Init function. </param>
		/// <exception cref="ArgumentNullException">
		/// Thrown if <see cref="this"/> <see cref="GameObject"/> is <see langword="null"/>. 
		/// </exception>
		/// <exception cref="InitArgumentsNotReceivedException">
		/// Thrown if <typeparamref name="TComponent"/> class does not implement <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument}"/> and did not manually handle receiving the provided arguments. 
		/// </exception>
		public static void AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>
			([DisallowNull] this GameObject gameObject, out TComponent component, TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument, TSixthArgument sixthArgument)
				where TComponent : MonoBehaviour, IArgs<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>
				 => component = gameObject.AddComponent<TComponent, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>(firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument);

        /// <summary>
        /// Returns the object of Type <typeparamref name="T"/> if the game object has one attached,
        /// <see langword="null"/> if it doesn't.
        /// <para>
        /// This will return the first object that is found and the order is undefined.
        /// If you expect there to be more than one component of the same type you can
        /// use gameObject.GetComponents instead, and cycle through the returned components
        /// testing for some unique property.
        /// </para>
        /// </summary>
        /// <typeparam name="T">
        /// The type of object to retrieve.
        /// <para>
        /// This can be the exact type of the object's class, a type of a class that the
        /// object's class derives from, or the type of an interface that the object's class implements.
        /// </para>
        /// <para>
        /// It is also possible to get objects wrapped by <see cref="IWrapper">wrappers</see>
        /// using the type of the wrapped object or using any interface implemented by the wrapped object.
        /// </para>
        /// </typeparam>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        [return: MaybeNull]
        public static T Get<T>([DisallowNull] this GameObject gameObject) => Find.In<T>(gameObject);

        [return: MaybeNull]
        public static T Get<T>([DisallowNull] this Component component) => Find.In<T>(component.gameObject);

        public static bool TryGet<T>([DisallowNull] this GameObject gameObject, out T result) => Find.In(gameObject, out result);

        [return: MaybeNull]
        public static T[] GetAll<T>([DisallowNull] this GameObject gameObject) => Find.AllIn<T>(gameObject);

        [return: MaybeNull]
        public static T GetInChildren<T>([DisallowNull] this GameObject gameObject, bool includeInactive = false) => Find.InChildren<T>(gameObject, includeInactive);

        [return: MaybeNull]
        public static T[] GetAllInChildren<T>([DisallowNull] this GameObject gameObject, bool includeInactive = false) => Find.AllInChildren<T>(gameObject, includeInactive);

        [return: MaybeNull]
        public static T GetInParents<T>([DisallowNull] this GameObject gameObject, bool includeInactive = false) => Find.InParents<T>(gameObject, includeInactive);

        [return: MaybeNull]
        public static T[] GetAllInParents<T>([DisallowNull] this GameObject gameObject, bool includeInactive = false) => Find.AllInParents<T>(gameObject, includeInactive);

        [return: MaybeNull]
        public static TWrapped GetWrappedInChildren<TWrapped>([DisallowNull] this Component component, bool includeInactive = false) => Find.InChildren<TWrapped>(component.gameObject, includeInactive);

        [return: MaybeNull]
        public static TWrapped GetWrappedInParents<TWrapped>([DisallowNull] this Component component, bool includeInactive = false) => Find.InParents<TWrapped>(component.gameObject, includeInactive);

		#if UNITY_EDITOR
		internal static bool IsPartOfPrefabAssetOrOpenInPrefabStage([DisallowNull] this GameObject gameObject) => gameObject.IsPartOfPrefabAsset() || gameObject.IsOpenInPrefabStage();

		internal static bool IsPartOfPrefabAsset([DisallowNull] this GameObject gameObject) => !gameObject.scene.IsValid();

		internal static bool IsOpenInPrefabStage([DisallowNull] this GameObject gameObject)
		    #if UNITY_2020_1_OR_NEWER
		    => UnityEditor.SceneManagement.StageUtility.GetStage(gameObject) != null;
		    #else
		    => UnityEditor.SceneManagement.StageUtility.GetStageHandle(gameObject).IsValid();
		    #endif
		#endif
    }
}