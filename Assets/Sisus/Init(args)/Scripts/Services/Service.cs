using System;
using System.Diagnostics.CodeAnalysis;
using Sisus.Init.Internal;
using UnityEngine;
using static Sisus.NullExtensions;

namespace Sisus.Init
{
	/// <summary>
	/// Utility that clients can use to retrieve services that are accessible to them.
	/// <para>
	/// A service can be located from <see cref="Services"/> components found in the active scenes
	/// which are accessible to the client or from the globally shared <see cref="Service{TService}.Instance"/>
	/// which can be accessed by any client.
	/// </para>
	/// </summary>
	public static class Service
	{
		internal static Type nowSettingInstance;

		/// <summary>
		/// Determines whether or not service of type <typeparamref name="TService"/>
		/// is available for the <paramref name="client"/>.
		/// <para>
		/// The service can be located from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns>
		/// <see langword="true"/> if service exists for the client; otherwise, <see langword="false"/>.
		/// </returns>
		public static bool ExistsFor<TService>([DisallowNull] object client)
			=> ServiceInjector.CanProvideService<TService>()
			|| (TryGetGameObject(client, out GameObject gameObject)
				? TryGetFor<TService>(gameObject, out _)
				: TryGet<TService>(out _));

		/// <summary>
		/// Gets a value indicating whether or not service of type <typeparamref name="TService"/> is available for the <paramref name="client"/>.
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns>
		/// <see langword="true"/> if service is available for the client; otherwise, <see langword="false"/>.
		/// </returns>
		public static bool ExistsFor<TService>([DisallowNull] Component client) => ServiceInjector.CanProvideService<TService>() || TryGetFor<TService>(client, out _);

		/// <summary>
		/// Determines whether or not service of type <typeparamref name="TService"/> is available
		/// for the <paramref name="client"/>.
		/// <para>
		/// The service can be located from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns>
		/// <see langword="true"/> if service exists for the client; otherwise, <see langword="false"/>.
		/// </returns>
		public static bool ExistsFor<TService>([DisallowNull] GameObject client) => ServiceInjector.CanProvideService<TService>() || TryGetFor<TService>(client, out _);

		/// <summary>
		/// Determines whether or not service of type <typeparamref name="TService"/>
		/// is available and globally accessible by any client.
		/// <para>
		/// The service can be located from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns>
		/// <see langword="true"/> if service exists for the client; otherwise, <see langword="false"/>.
		/// </returns>
		public static bool Exists<TService>() => ServiceInjector.CanProvideService<TService>() || TryGet<TService>(out _);

		/// <summary>
		/// Determines whether or not the given <paramref name="object"/> is a service accessible by the <paramref name="client"/>.
		/// <para>
		/// Services are components that have the <see cref="ServiceTag"/> attached to them,
		/// have been defined as a service in a <see cref="Services"/> component,
		/// have the <see cref="ServiceAttribute"/> on their class,
		/// or have been <see cref="SetInstance">manually registered</see> as a service in code.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that has to be able to access the service. </param>
		/// <param name="object"> The object to test. </param>
		/// <returns>
		/// <see langword="true"/> if the <paramref name="object"/> is a service accessible by the <paramref name="client"/>;
		/// otherwise, <see langword="false"/>.
		/// </returns>
		public static bool ForEquals<TService>([DisallowNull] GameObject client, TService @object)
			=> TryGetFor(client, out TService service) && ReferenceEquals(service, @object);

		public static bool TryGetClients<TService>([AllowNull] TService service, out Clients clients)
		{
			if(service is null)
			{
				if(ServiceAttributeUtility.definingTypes.ContainsKey(typeof(TService)))
				{
					clients = Clients.Everywhere;
					return true;
				}

				clients = default;
				return false;
			}

			if(!typeof(TService).IsValueType && ReferenceEquals(Service<TService>.Instance, service))
			{
				clients = Clients.Everywhere;
				return true;
			}

			foreach(var instance in ScopedService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(instance.serviceProvider == null)
				{
					continue;
				}
				#endif

				if(ReferenceEquals(instance.service, service))
				{
					clients = instance.clients;
					return true;
				}
			}

			clients = default;
			return false;
		}

		/// <summary>
		/// Tries to get <paramref name="service"/> of type <typeparamref name="TService"/>
		/// for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>,
		/// if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		public static bool TryGetFor<TService>([DisallowNull] object client, [NotNullWhen(true), MaybeNullWhen(false)] out TService service)
			=> TryGetGameObject(client, out GameObject gameObject)
				? TryGetFor(gameObject.transform, out service)
				: TryGet(out service);

		/// <summary>
		/// Tries to get <paramref name="service"/> of type <typeparamref name="TService"/>
		/// for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client <see cref="Component"/> that needs the service. </param>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>, if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		public static bool TryGetFor<TService>([DisallowNull] Component client, [NotNullWhen(true), MaybeNullWhen(false)] out TService service)
		{
			Debug.Assert(client != null);

			bool foundResult = false;
			ScopedService<TService>.Instance nearest = default;

			foreach(var instance in ScopedService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(instance.serviceProvider == null)
				{
					continue;
				}
				#endif

				if(!IsAccessibleTo(instance, client.transform))
				{
					continue;
				}

				var someService = instance.service;
				if(someService is null)
				{
					continue;
				}

				var nearestService = nearest.service;
				if(nearestService is null)
				{
					nearest = instance;
					foundResult = true;
					continue;
				}

				if(ReferenceEquals(someService, nearestService))
				{
					continue;
				}

				var clientScene = client.gameObject.scene;
				if(instance.Scene != clientScene)
				{
					#if DEBUG
					if(nearest.Scene != clientScene)
					{
						Debug.LogWarning($"AmbiguousMatchWarning: Client on GameObject \"{client.name}\" has access to both services {TypeUtility.ToString(instance.service.GetType())} and {TypeUtility.ToString(nearest.service.GetType())} via {nameof(Services)}s and unable to determine which one should be prioritized.", client);
					}
					#endif

					continue;
				}

				if(nearest.Scene != clientScene)
				{
					nearest = instance;
					continue;
				}

				var instanceTransform = instance.Transform;
				var nearestTransform = nearest.Transform;

				#if DEBUG
				bool betterMatchFound = false;
				#endif

				for(var clientParent = client.transform; clientParent != null; clientParent = clientParent.parent)
				{
					if(clientParent == instanceTransform)
					{
						#if DEBUG
						if(clientParent == nearestTransform)
						{
							break;
						}

						betterMatchFound = true;
						#endif

						nearest = instance;
						break;
					}

					if(clientParent == nearestTransform)
					{
						#if DEBUG
						betterMatchFound = true;
						#endif
						break;
					}
				}

				#if DEBUG
				if(!betterMatchFound)
				{
					Debug.LogWarning($"AmbiguousMatchWarning: Client on GameObject \"{client.name}\" has access to both services \"{instance.Transform.name}\"/{TypeUtility.ToString(instance.service.GetType())} and \"{nearest.Transform.name}\"/{TypeUtility.ToString(nearest.service.GetType())} via {nameof(Services)} and unable to determine which one should be prioritized.", client);
				}
				#endif
			}

			service = nearest.service;
			if(foundResult && service != Null)
			{
				return true;
			}

			if(typeof(TService).IsValueType)
			{
				service = default;
				return false;
			}

			#if UNITY_EDITOR && !INIT_ARGS_DISABLE_SERVICE_INJECTION
			// Usually we can rely on static constructor of Service<T> to handle lazily initializing the service when necessary.
			// However with Enter Play Mode Options enabled this might not work, so we should handle that here instead.
			if(ServiceInjector.uninitializedServices.TryGetValue(typeof(TService), out var definition) && EditorOnly.ThreadSafe.Application.IsPlaying)
			{
				_ = ServiceInjector.LazyInit(definition, typeof(TService));
			}
			#endif

			var globalService = Service<TService>.Instance;
			if(globalService != Null)
			{
				service = globalService;
				return true;
			}

			#if UNITY_EDITOR
			if(ServiceAttributeUtility.definingTypes.TryGetValue(typeof(TService), out ServiceInfo info) && info.FindFromScene)
			{
				service = Find.Any<TService>();
				return Find.Any(out service);
			}
			#endif

			service = default;
			return false;
		}

		/// <summary>
		/// Tries to get <paramref name="service"/> of type <typeparamref name="TService"/>
		/// for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client <see cref="GameObject"/> that needs the service. </param>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>, if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		public static bool TryGetFor<TService>([DisallowNull] GameObject client, [NotNullWhen(true), MaybeNullWhen(false)] out TService service)
			=> TryGetFor(client.transform, out service);

		/// <summary>
		/// Tries to get <paramref name="service"/> of type <typeparamref name="TService"/>
		/// for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can be called from any thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>, if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		public static bool TryGet<TService>([NotNullWhen(true), MaybeNullWhen(false)] out TService service)
		{
			bool foundResult = false;
			ScopedService<TService>.Instance nearest = default;

			foreach(var instance in ScopedService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(instance.serviceProvider == null)
				{
					continue;
				}
				#endif

				if(instance.clients != Clients.Everywhere)
				{
					continue;
				}

				#if DEBUG && !INIT_ARGS_DISABLE_WARNINGS
				if(nearest.service != Null && !ReferenceEquals(nearest.service, instance))
				{
					Debug.LogWarning($"AmbiguousMatchWarning: All clients have access to both services \"{nearest.Transform.name}\"/{TypeUtility.ToString(nearest.service.GetType())} and \"{instance.Transform.name}\"/{TypeUtility.ToString(instance.service.GetType())} and unable to determine which one should be prioritized.");
				}
				#endif

				nearest = instance;
				foundResult = true;

				#if !DEBUG
                break;
				#endif
			}

			service = nearest.service;
			if(foundResult && service != Null)
			{
				return true;
			}

			if(typeof(TService).IsValueType)
			{
				service = default;
				return false;
			}

			#if UNITY_EDITOR && !INIT_ARGS_DISABLE_SERVICE_INJECTION
			// Usually we can rely on static constructor of Service<T> to handle lazily initializing the service when necessary.
			// However with Enter Play Mode Options enabled this might not work, so we should handle that here instead.
			if(ServiceInjector.uninitializedServices.TryGetValue(typeof(TService), out var definition) && EditorOnly.ThreadSafe.Application.IsPlaying)
			{
				_ = ServiceInjector.LazyInit(definition, typeof(TService));
			}
			#endif

			var globalService = Service<TService>.Instance;
			if(globalService != Null)
			{
				service = globalService;
				return true;
			}

			#if UNITY_EDITOR
			if(ServiceAttributeUtility.definingTypes.TryGetValue(typeof(TService), out ServiceInfo info) && info.FindFromScene)
			{
				service = Find.Any<TService>();
				return Find.Any(out service);
			}
			#endif

			service = default;
			return false;
		}

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <exception cref="NullReferenceException"> Thrown if no service of type <typeparamref name="TService"/> is found that is accessible to the <paramref name="client"/>. </exception>
		[return: NotNull]
		public static TService GetFor<TService>([DisallowNull] Component client)
			=> TryGetFor(client.gameObject, out TService result)
			? result
			: throw new NullReferenceException($"No service of type {typeof(TService).Name} was found that was accessible to client {TypeUtility.ToString(client.GetType())}.");

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client <see cref="Component"/> that needs the service. </param>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <exception cref="NullReferenceException"> Thrown if no service of type <typeparamref name="TService"/> is found that is accessible to the <paramref name="client"/>. </exception>
		[return: NotNull]
		public static TService Get<TService>([DisallowNull] Component client) => TryGetFor(client.gameObject, out TService service) ? service : throw new NullReferenceException($"No service of type {typeof(TService).Name} was found that was accessible to client {client.GetType().Name}.");

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes, or failing that,
		/// from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client <see cref="GameObject"/> that needs the service. </param>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <exception cref="NullReferenceException"> Thrown if no service of type <typeparamref name="TService"/> is found that is accessible to the <paramref name="client"/>. </exception>
		[return: NotNull]
		public static TService Get<TService>([DisallowNull] GameObject client) => TryGetFor(client, out TService service) ? service : throw new NullReferenceException($"No service of type {typeof(TService).Name} was found that was accessible to client {client.GetType().Name}.");

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> for any client.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes, or failing that,
		/// from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client <see cref="GameObject"/> that needs the service. </param>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <exception cref="NullReferenceException"> Thrown if no service of type <typeparamref name="TService"/> is found that is globally accessible to any client. </exception>
		[return: NotNull]
		public static TService Get<TService>() => TryGet(out TService service) ? service : throw new NullReferenceException($"No globally accessible Service of type {typeof(TService).Name} was found.");

		private static bool TryGetGameObject(object client, out GameObject gameObject)
		{
			var component = client as Component;
			if(component != null)
			{
				gameObject = component.gameObject;
				return true;
			}

			gameObject = client as GameObject;
			if(gameObject != null)
			{
				return true;
			}

			if(!Find.WrapperOf(client, out var wrapper))
			{
				gameObject = null;
				return false;
			}

			component = wrapper as Component;
			if(component != null)
			{
				gameObject = component.gameObject;
				return true;
			}

			gameObject = null;
			return false;
		}

		/// <summary>
		/// Sets the <typeparamref name="TService"/> service instance
		/// shared across clients to the given value.
		/// <para>
		/// If the provided instance is not equal to the old <see cref="Instance"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="newInstance"> The new instance of the service. </param>
		public static void SetInstance<TService>([DisallowNull] TService newInstance)
		{
			Debug.Assert(newInstance != null);

			nowSettingInstance = typeof(TService);

			var oldInstance = Service<TService>.Instance;

			if(ReferenceEquals(oldInstance, newInstance))
			{
				nowSettingInstance = null;
				return;
			}

			ServiceInjector.uninitializedServices.Remove(typeof(TService));
			ServiceInjector.services[typeof(TService)] = newInstance;
			Service<TService>.Instance = newInstance;

			nowSettingInstance = null;

			ServiceChanged<TService>.listeners?.Invoke(oldInstance, newInstance);
		}

		/// <summary>
		/// Registers a service with the defining type <typeparamref name="TService"/>
		/// available to a limited set of clients.
		/// <para>
		/// If the provided instance is available to clients <see cref="Clients.Everywhere"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </para>
		/// </summary>
		/// <typeparam name="TService">
		/// The defining type of the service; the class or interface type that uniquely defines
		/// the service and can be used to retrieve an instance of it.
		/// <para>
		/// This must be an interface that the service implement, a base type that the service derives from,
		/// or the exact type of the service.
		/// </para>
		/// </typeparam>
		/// <param name="clients">
		/// Specifies which client objects can receive the service instance in their Init function
		/// during their initialization.
		/// </param>
		/// <param name="service"> The service instance to add. </param>
		/// <param name="container">
		/// Component that is registering the service. This can also be the service itself, if it is a component.
		/// <para>
		/// This same argument should be passed when <see cref="RemoveFrom">removing the instance</see>.
		/// </para>
		/// </param>
		public static void AddFor<TService>(Clients clients, [DisallowNull] TService service, [DisallowNull] Component container)
		{
			Debug.Assert(service != null);

			if(ScopedService<TService>.Add(service, clients, container) && clients == Clients.Everywhere)
			{
				ServiceChanged<TService>.listeners?.Invoke(Service<TService>.Instance, service);
			}
		}

		/// <summary>
		/// Deregisters a service with the defining type <typeparamref name="TService"/>
		/// that has been available to a limited set of clients.
		/// <para>
		/// If the provided instance is available to clients <see cref="Clients.Everywhere"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </para>
		/// </summary>
		/// <typeparam name="TService">
		/// The defining type of the service; the class or interface type that uniquely defines
		/// the service and can be used to retrieve an instance of it.
		/// <para>
		/// This must be an interface that the service implement, a base type that the service derives from,
		/// or the exact type of the service.
		/// </para>
		/// </typeparam>
		/// <param name="clients"> The availability of the service being removed. </param>
		/// <param name="service"> The service instance to remove. </param>
		/// <param name="container"> Component that registered the service. </param>
		public static void RemoveFrom<TService>(Clients clients, [DisallowNull] TService service, [DisallowNull] Component container)
		{
			if(ScopedService<TService>.Remove(service, clients, container) && clients == Clients.Everywhere)
			{
				ServiceChanged<TService>.listeners?.Invoke(service, Service<TService>.Instance);
			}
		}

		/// <summary>
		/// Subscribes the provided <paramref name="method"/> to listen for changes made to the shared instance of service of type <typeparamref name="TService"/>.
		/// <para>
		/// The method will only be called when in reaction to services that are accesible by all <see cref="Clients"/> changing.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="method">
		/// Method to call when the shared instance of service of type <typeparamref name="TService"/> has changed to a different one.
		/// </param>
		public static void AddInstanceChangedListener<TService>(ServiceChangedHandler<TService> method) => ServiceChanged<TService>.listeners += method;

		/// <summary>
		/// Unsubscribes the provided <paramref name="method"/> from listening for changes made to the shared instance of service of type <typeparamref name="TService"/>.
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="method">
		/// Method that should no longer be called when the shared instance of service of type <typeparamref name="TService"/> has changed to a different one.
		/// </param>
		public static void RemoveInstanceChangedListener<TService>(ServiceChangedHandler<TService> method) => ServiceChanged<TService>.listeners -= method;

		private static ScopedService<TService>.Instance GetNearest<TService>([DisallowNull] GameObject client, ScopedService<TService>.Instance firstOption, ScopedService<TService>.Instance? secondOptionOrNull)
		{
			if(!secondOptionOrNull.HasValue)
			{
				return firstOption;
			}

			var firstService = firstOption.service;
			var secondOption = secondOptionOrNull.Value;
			var secondService = secondOption.service;

			if(firstService is null)
			{
				return secondOption;
			}

			if(secondService is null)
			{
				return firstOption;
			}

			if(ReferenceEquals(firstService, secondService))
			{
				return firstOption;
			}

			var scene = client.gameObject.scene;

			if(firstOption.Scene != scene)
			{
				#if DEBUG
				if(secondOption.Scene != scene)
				{
					Debug.LogWarning($"AmbiguousMatchWarning: Client on GameObject \"{client.name}\" has access to both services {TypeUtility.ToString(firstOption.service.GetType())} and {TypeUtility.ToString(secondOption.service.GetType())} via {nameof(Services)}s and unable to determine which one should be prioritized.", client);
				}
				#endif

				return secondOption;
			}

			if(secondOption.Scene != scene)
			{
				return firstOption;
			}

			var firstTransform = firstOption.Transform;
			var secondTransform = secondOption.Transform;

			for(var parent = client.transform; parent != null; parent = parent.parent)
			{
				if(parent == firstTransform)
				{
					if(parent == secondTransform)
					{
						break;
					}

					return firstOption;
				}

				if(parent == secondTransform)
				{
					return secondOption;
				}
			}

			#if DEBUG
			Debug.LogWarning($"AmbiguousMatchWarning: Client on GameObject \"{client.name}\" has access to both services \"{firstOption.Transform.name}\"/{TypeUtility.ToString(firstOption.service.GetType())} and \"{secondOption.Transform.name}\"/{TypeUtility.ToString(secondOption.service.GetType())} via {nameof(Services)} and unable to determine which one should be prioritized.", client);
			#endif

			return firstOption;
		}

		private static bool IsAccessibleTo<TService>(ScopedService<TService>.Instance instance, [DisallowNull] Transform clientTransform)
		{
			Debug.Assert(clientTransform != null);
			Debug.Assert(instance.serviceProvider != null);

			switch(instance.clients)
			{
				case Clients.InGameObject:
					return instance.Transform == clientTransform;
				case Clients.InChildren:
					for(var parent = clientTransform.transform; parent != null; parent = parent.parent)
					{
						if(parent == instance.Transform)
						{
							return true;
						}
					}
					return false;
				case Clients.InParents:
					for(var parent = instance.Transform; parent != null; parent = parent.parent)
					{
						if(parent == clientTransform)
						{
							return true;
						}
					}
					return false;
				case Clients.InHierarchyRootChildren:
					return instance.Transform.root == clientTransform.root;
				case Clients.InScene:
					return clientTransform.gameObject.scene == instance.Scene;
				case Clients.InAllScenes:
				case Clients.Everywhere:
					return true;
				default:
					Debug.LogError($"Unrecognized {nameof(Clients)} value: {instance.clients}.", instance.Transform);
					return false;
			}
		}

		internal static bool IsAccessibleTo([DisallowNull] Transform serviceTransform, Clients clients, [AllowNull] Transform clientTransform)
		{
			if(clientTransform == null)
			{
				return clients == Clients.Everywhere;
			}

			switch(clients)
			{
				case Clients.InGameObject:
					return serviceTransform == clientTransform;
				case Clients.InChildren:
					for(var parent = clientTransform.transform; parent != null; parent = parent.parent)
					{
						if(parent == serviceTransform)
						{
							return true;
						}
					}
					return false;
				case Clients.InParents:
					for(var parent = serviceTransform; parent != null; parent = parent.parent)
					{
						if(parent == clientTransform)
						{
							return true;
						}
					}
					return false;
				case Clients.InHierarchyRootChildren:
					return serviceTransform.root == clientTransform.root;
				case Clients.InScene:
					return clientTransform.gameObject.scene == serviceTransform.gameObject.scene;
				case Clients.InAllScenes:
				case Clients.Everywhere:
					return true;
				default:
					Debug.LogError($"Unrecognized {nameof(Clients)} value: {clients}.", serviceTransform);
					return false;
			}
		}
	}
}