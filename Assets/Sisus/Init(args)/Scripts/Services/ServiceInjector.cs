//#define DEBUG_INIT_SERVICES
//#define DEBUG_CREATE_SERVICES
//#define DEBUG_LAZY_INIT

#if !INIT_ARGS_DISABLE_SERVICE_INJECTION
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
using UnityEngine.AddressableAssets;
#endif

#if UNITY_EDITOR
using UnityEditor;
using Sisus.Init.EditorOnly;
using Debug = UnityEngine.Debug;
using TypeCollection = UnityEditor.TypeCache.TypeCollection;
#else
using TypeCollection = System.Collections.Generic.IEnumerable<System.Type>;
#endif

namespace Sisus.Init.Internal
{
	/// <summary>
	/// Class responsible for caching instances of all classes that have the <see cref="ServiceAttribute"/>,
	/// injecting dependencies for services that implement an <see cref="IInitializable{}"/>
	/// interface targeting only other services,
	/// and using <see cref="InitArgs.Set"/> to assign references to services ready to be retrieved
	/// for any other classes that implement an <see cref="IArgs{}"/> interface targeting only services.
	/// </summary>
    internal static class ServiceInjector
    {
        /// <summary>
        /// <see langword="true"/> if all services  have been created, initialized and are ready
        /// to be used by clients; otherwise, <see langword="false"/>.
        /// <para>
        /// This only takes into consideration services that are initialized synchronously and non-lazily
        /// during game initialization. To determine if all asynchronously initialized services are also
        /// ready to be used, use <see cref="AsyncServicesAreReady"/> instead.
        /// </para>
        /// <para>
        /// This only takes into consideration services defined using the <see cref="ServiceAttribute"/>.
        /// Services set up in scenes and prefabs using Service tags and <see cref="Services"/> components
        /// are not guaranteed to be yet loaded even if this is <see langword="true"/>.
        /// Services that are <see cref="Service.SetInstance">registered manually</see> are also not
        /// guaranteed to be loaded even if this is <see langword="true"/>.
        /// </para>
        /// </summary>
        public static bool ServicesAreReady { get; private set; }

        /// <summary>
        /// <see langword="true"/> if all services that are initialized asynchrously
        /// have been created, initialized and are ready to be used by clients.
        /// </para>
        /// This only takes into consideration services defined using the <see cref="ServiceAttribute"/>.
        /// Services set up in scenes and prefabs using Service tags and <see cref="Services"/> components
        /// are not guaranteed to be yet loaded even if this is <see langword="true"/>.
        /// Services that are <see cref="Service.SetInstance">registered manually</see> are also not
        /// guaranteed to be loaded even if this is <see langword="true"/>.
        /// </para>
        /// </summary>
        public static bool AsyncServicesAreReady { get; private set; }

        /// <summary>
        /// Called when all services have been created,
        /// initialized and are ready to be used by clients.
        /// <para>
        /// This only takes into consideration services that are initialized synchronously and non-lazily
        /// during game initialization. To get a callback when all asynchronously initialized services are also
        /// ready to be used, use <see cref="AsyncServicesBecameReady"/> instead.
        /// </para>
        /// </summary>
        public static event Action ServicesBecameReady;

        /// <summary>
        /// Called when all services that are initialized asynchrously have been created,
        /// initialized and are ready to be used by clients.
        /// </summary>
        public static event Action AsyncServicesBecameReady;
        internal static Dictionary<Type, object> services = new Dictionary<Type, object>();
        internal static readonly Dictionary<Type, ServiceInfo> uninitializedServices = new Dictionary<Type, ServiceInfo>();
        private static GameObject container;

        #if UNITY_EDITOR
        /// <summary>
        /// Reset state when entering play mode in the editor to support Enter Play Mode Settings.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnEnterPlayMode()
		{
            ServicesAreReady = false;
            AsyncServicesAreReady = false;
            services.Clear();
            uninitializedServices.Clear();

            static void OnExitingPlayMode() => ServicesAreReady = false;
            ThreadSafe.Application.ExitingPlayMode -= OnExitingPlayMode;
            ThreadSafe.Application.ExitingPlayMode += OnExitingPlayMode;
		}
        #endif

        /// <summary>
        /// Creates instances of all services,
        /// injects dependencies for servives that implement an <see cref="IInitializable{}"/>
        /// interface targeting only other services,
        /// and uses <see cref="InitArgs.Set"/> to assign references to services ready to be retrieved
        /// for any other classes that implement an <see cref="IArgs{}"/> interface targeting only services.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static async void CreateAndInjectServices()
        {
            #if DEV_MODE
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            #endif

            CreateInstancesOfAllServices();

            ServicesAreReady = true;

            #if UNITY_EDITOR
            var scriptableObjects = Resources.FindObjectsOfTypeAll<ScriptableObject>();
            var uninitializedScriptableObjects = new Dictionary<Type, List<ScriptableObject>>(scriptableObjects.Length);
            foreach(var uninitializedScriptableObject in scriptableObjects)
			{
                var type = uninitializedScriptableObject.GetType();
                if(!uninitializedScriptableObjects.TryGetValue(type, out List<ScriptableObject> instances))
				{
                    instances = new List<ScriptableObject>(1);
                    uninitializedScriptableObjects.Add(type, instances);
				}

                instances.Add(uninitializedScriptableObject);
			}
            #endif

            InjectServiceDependenciesForTypesThatRequireOnlyThem(out List<Task> injectAsyncServices
            #if UNITY_EDITOR
            , uninitializedScriptableObjects    
            #endif
            );

            #if UNITY_EDITOR
            InitializeAlreadyLoadedScriptableObjectsInTheEditor(uninitializedScriptableObjects);
            #endif

            ServicesBecameReady?.Invoke();
            ServicesBecameReady = null;

            #if DEV_MODE
            Debug.Log($"Initialization of {services.Count} service took {timer.Elapsed.TotalSeconds} seconds.");
            #endif

            await Task.WhenAll(injectAsyncServices);

            #if DEV_MODE
            timer.Stop();
            Debug.Log($"Injection of {injectAsyncServices.Count} async service took {timer.Elapsed.TotalSeconds} seconds.");
            #endif

            AsyncServicesAreReady = true;
            AsyncServicesBecameReady?.Invoke();
            AsyncServicesBecameReady = null;
        }

        private static void CreateInstancesOfAllServices()
		{
			var serviceInfos = GetServiceDefinitions();
			int definitionCount = serviceInfos.Count;
			if(definitionCount == 0)
			{
				services = null;
				return;
			}

			services = new Dictionary<Type, object>(definitionCount);

			// List of concrete service types that have already been initialized (instance created / retrieved)
			HashSet<Type> initialized = new HashSet<Type>();

			Dictionary<Type, ScopedServiceInfo> scopedServiceInfos = new(0);

			CreateServices(serviceInfos, initialized, scopedServiceInfos);

			InjectCrossServiceDependencies(serviceInfos, initialized, scopedServiceInfos);

            #if UNITY_EDITOR
			_ = CreateServicesDebugger();
            #endif

			if(container != null)
			{
				container.SetActive(true);
			}

			HandleExecutingEventFunctions(initialized);

			static void HandleExecutingEventFunctions(HashSet<Type> concreteServiceTypes)
			{
				foreach(var concreteType in concreteServiceTypes)
				{
					if(services.TryGetValue(concreteType, out object instance))
					{
						SubscribeToUpdateEvents(instance);
						ExecuteAwake(instance);
						ExecuteOnEnable(instance);
						ExecuteStartAtEndOfFrame(instance);
					}
				}
			}
		}

        private static Dictionary<Type, ScopedServiceInfo> FillIfEmpty(Dictionary<Type, ScopedServiceInfo> scopedServiceInfos)
		{
            if(scopedServiceInfos.Count > 0)
			{
                return scopedServiceInfos;
			}

            var servicesComponents = 
            #if UNITY_2022_2_OR_NEWER
			Object.FindObjectsByType<Services>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            #else
            Object.FindObjectsOfType<Services>(true);
            #endif

			foreach(var servicesComponent in servicesComponents)
			{
				var toClients = servicesComponent.toClients;
				foreach(var definition in servicesComponent.providesServices)
				{
                    if(definition.definingType?.Value is Type definingType && definition.service is Object service && service)
                    {
					    scopedServiceInfos[definingType] = new(definingType, service, toClients);
                    }
				}
			}

			var serviceTags =
            #if UNITY_2022_2_OR_NEWER
			Object.FindObjectsByType<ServiceTag>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            #else
            Object.FindObjectsOfType<ServiceTag>(true);
            #endif
            
			foreach(var serviceTag in serviceTags)
			{
                if(serviceTag.DefiningType is Type definingType && serviceTag.Service is Component service)
                {
				    scopedServiceInfos[definingType] = new(definingType, service, serviceTag.ToClients);
                }
			}

            // To avoid wasting resources try to rebuild the dictionary again and again, make sure it contains at least one entry
            if(scopedServiceInfos.Count == 0)
			{
                scopedServiceInfos.Add(typeof(void), new(typeof(void), null, Clients.Everywhere));
			}

			return scopedServiceInfos;
		}

        #if UNITY_EDITOR
		private static async Task CreateServicesDebugger()
        {
            if(!Application.isPlaying)
            {
                return;
            }

            if(container == null)
            {
                CreateServicesContainer();
            }

            var debugger = container.AddComponent<ServicesDebugger>();
            await debugger.SetServices(services.Values.Distinct());
        }
        #endif

        private static void ForEachService(Action<object> action)
		{
            foreach(var service in services.Values.Distinct())
			{
                if(service is Task task)
				{
                    task.ContinueWith(InvokeOnResult);
                }
                else
                {
                    action(service);
                }
			}

            void InvokeOnResult(Task task) => action(task.GetResult());
		}

		private static void CreateServicesContainer()
        {
            container = new GameObject("Services");
            container.SetActive(false);
            container.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(container);
        }

        private static void CreateServices(List<ServiceInfo> serviceInfos, HashSet<Type> initialized, [AllowNull] Dictionary<Type, ScopedServiceInfo> scopedServiceInfos)
        {
            foreach(var serviceInfo in serviceInfos)
			{
				if(serviceInfo.LazyInit || serviceInfo.ConcreteOrDefiningType.IsGenericTypeDefinition)
				{
                    #if DEV_MODE && DEBUG_LAZY_INIT
                    Debug.Log($"Will not initialize {(serviceInfo.concreteType ?? serviceInfo.definingTypes[0]).FullName} yet because it has LazyInit set to true");
                    #endif

                    if(serviceInfo.concreteType is Type concreteType)
                    {
                        uninitializedServices[concreteType] = serviceInfo;
                    }

                    foreach(var definingType in serviceInfo.definingTypes)
					{
                        uninitializedServices[definingType] = serviceInfo;
					}

					continue;
				}

				_ = GetOrCreateInstance(serviceInfo, initialized, scopedServiceInfos, null);
			}
		}

        /// <param name="requestedServiceType"> The type of the initialization argument being requested for the client. Could be abstract. </param>
        /// <returns></returns>
        internal static async Task LazyInit([DisallowNull] ServiceInfo serviceInfo, [DisallowNull] Type requestedServiceType)
        {
            #if DEV_MODE || DEBUG || SAFE_MODE
            if(requestedServiceType.IsGenericTypeDefinition)
			{
                Debug.LogError($"LazyInit called with {nameof(requestedServiceType)} {TypeUtility.ToString(requestedServiceType)} that was a generic type definition. This should not happen.");
                return;
			}
            #endif

            if(GetConcreteAndClosedType(serviceInfo, requestedServiceType) is not Type concreteType)
			{
                #if DEV_MODE
                Debug.LogWarning($"LazyInit({TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} as {TypeUtility.ToString(requestedServiceType)}) called but could not determine {nameof(concreteType)} for service. Should this ever happen? Should the method be renamed to TryLazyInit?");
                #endif
                return;
			}

            // If service has already been initialized, no need to do anything.
            if(services.TryGetValue(concreteType, out object service))
            {
                return;
            }

            if(serviceInfo.ConcreteOrDefiningType is Type concreteOrDefiningType)
            {
                if(concreteOrDefiningType.IsGenericTypeDefinition ?
                  !uninitializedServices.ContainsKey(concreteOrDefiningType)
                : !uninitializedServices.Remove(concreteOrDefiningType))
			    {
                    return;
			    }
            }

            var definingTypes = serviceInfo.definingTypes;
            foreach(var definingType in definingTypes)
            {
                uninitializedServices.Remove(definingType);
            }

            #if DEV_MODE && DEBUG_LAZY_INIT
            Debug.Log($"LazyInit({(serviceInfo.concreteType ?? serviceInfo.definingTypes[0]).FullName})");
            #endif

            var initialized = new HashSet<Type>();
            service = await GetOrCreateInstance(serviceInfo, initialized, null, concreteType);

            #if UNITY_EDITOR
            if(container != null && container.TryGetComponent(out ServicesDebugger debugger))
            {
                _ = debugger.SetServices(services.Values.Distinct());
            }
            #endif

            await InjectCrossServiceDependencies(service, initialized, null, definingTypes);

            [return: MaybeNull]
			static Type GetConcreteAndClosedType([DisallowNull] ServiceInfo serviceInfo, [DisallowNull] Type requestedServiceType)
			{
				Type concreteType = serviceInfo.concreteType;
				if(concreteType is null)
				{
					if(!requestedServiceType.IsAbstract)
					{
						return requestedServiceType;
					}

					concreteType = Array.Find(serviceInfo.definingTypes, t => !t.IsAbstract);
                    if(concreteType is null)
					{
                        return null;
					}
				}

				if(!concreteType.IsGenericTypeDefinition)
				{
					return concreteType;
				}

				if(!requestedServiceType.IsAbstract
				&& requestedServiceType.GetGenericTypeDefinition().IsAssignableFrom(concreteType))
				{
					return requestedServiceType;
				}
				
                if(requestedServiceType.IsGenericType)
				{
					var requestedServiceTypeGenericArguments = requestedServiceType.GetGenericArguments();
					if(requestedServiceTypeGenericArguments.Length == concreteType.GetGenericArguments().Length)
					{
						concreteType = concreteType.MakeGenericType(requestedServiceTypeGenericArguments);
						if(requestedServiceType.IsAssignableFrom(concreteType))
						{
                            return concreteType;
						}
					}
				}

				return Array.Find(serviceInfo.definingTypes, t => !t.IsAbstract);
			}
        }

        private static async Task<object> GetOrCreateInstance(ServiceInfo serviceInfo, HashSet<Type> initialized, [AllowNull] Dictionary<Type, ScopedServiceInfo> scopedServiceInfos, Type overrideConcreteType)
        {
            var concreteType = overrideConcreteType ?? serviceInfo.concreteType;
            if((concreteType ?? serviceInfo.definingTypes.FirstOrDefault()) is not Type concreteOrDefiningType)
			{
                return null;
			}

            // If one class contains multiple Service attributes still create only one shared instance.
            if(services.TryGetValue(concreteOrDefiningType, out var existingInstance))
            {
                if(existingInstance is Task existingTask)
				{
                    existingInstance = await existingTask.GetResult();
				}

                return existingInstance;
            }

            Task<object> task;
            try
			{
				task = LoadInstance(serviceInfo, initialized, scopedServiceInfos);
			}
            catch(MissingMethodException e)
			{
                Debug.LogWarning($"Failed to initialize service {concreteOrDefiningType.Name} with defining types {string.Join(", ", serviceInfo.definingTypes.Select(t => t?.Name))}.\nThis can happen when constructor arguments contain circular references (for example: object A requires object B, but object B also requires object A, so neither object can be constructed).\n{e}");
                return null;
			}
			catch(Exception e)
			{
				Debug.LogWarning($"Failed to initialize service {concreteOrDefiningType.Name} with defining types {string.Join(", ", serviceInfo.definingTypes.Select(t => t?.Name))}.\n{e}");
                return null;
			}
            
            if(concreteType is not null)
            {
                services[concreteType] = task;
            }

            foreach(var definingType in serviceInfo.definingTypes)
			{
                services[definingType] = task;
			}

            object result = await task;
            if(result is Task chainedTask)
            {
                result = await chainedTask.GetResult();
			}

            if(result is null)
            {
                #if DEV_MODE
                Debug.LogWarning($"GetOrCreateInstance(concreteOrDefiningType:{concreteOrDefiningType.Name}, definingTypes:{string.Join(", ", serviceInfo.definingTypes.Select(t => t?.Name))}) returned instance was null.");
                #endif
                return null;
            }

            SetInstanceSync(serviceInfo, result);
            return result;
        }

        private static async Task<object> LoadInstance(ServiceInfo serviceInfo, HashSet<Type> initialized, [AllowNull] Dictionary<Type, ScopedServiceInfo> scopedServiceInfos)
        {
            object result;
            Type concreteType = serviceInfo.concreteType;
            if(typeof(IServiceInitializer).IsAssignableFrom(serviceInfo.classWithAttribute))
			{
                if(initialized.Contains(concreteType))
			    {
                    return null;
			    }

                Type initializerType = serviceInfo.classWithAttribute;
                var serviceInitializer = Activator.CreateInstance(initializerType) as IServiceInitializer;
                var interfaceTypes = initializerType.GetInterfaces();
                for(int i = interfaceTypes.Length - 1; i >= 0; i--)
                {
                    var interfaceType = interfaceTypes[i];
                    if(!interfaceType.IsGenericType)
                    {
                        continue;
                    }

                    var typeDefinition = interfaceType.GetGenericTypeDefinition();

                    if(typeDefinition == typeof(IServiceInitializer<,,,,,,>))
                    {
                        var argumentTypes = interfaceType.GetGenericArguments();
                        var firstArgumentType = argumentTypes[1];
                        var secondArgumentType = argumentTypes[2];
                        var thirdArgumentType = argumentTypes[3];
                        var fourthArgumentType = argumentTypes[4];
                        var fifthArgumentType = argumentTypes[5];
                        var sixthArgumentType = argumentTypes[6];
                        initialized.Add(concreteType);
                        if(TryGetOrCreateService(firstArgumentType, out object firstArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(secondArgumentType, out object secondArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(fourthArgumentType, out object fourthArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(fifthArgumentType, out object fifthArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(sixthArgumentType, out object sixthArgument, initialized, scopedServiceInfos))
                        {
                            Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                            Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                            Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                            Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;
                            Task fifthArgumentTask = !fifthArgumentType.IsInstanceOfType(fifthArgument) ? fifthArgument as Task : null;
                            Task sixthArgumentTask = !sixthArgumentType.IsInstanceOfType(thirdArgument) ? sixthArgument as Task : null;

                            var loadArgumentTasks = Enumerable.Empty<Task>();

                            if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                            if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                            if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                            if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);
                            if(fifthArgumentTask != null) loadArgumentTasks.Append(fifthArgumentTask);
                            if(sixthArgumentTask != null) loadArgumentTasks.Append(sixthArgumentTask);

                            await Task.WhenAll(loadArgumentTasks);

                            firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                            secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                            thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                            fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);
                            fifthArgument = await InjectCrossServiceDependencies(fifthArgument, initialized, scopedServiceInfos);
                            sixthArgument = await InjectCrossServiceDependencies(sixthArgument, initialized, scopedServiceInfos);

                            #if DEBUG_CREATE_SERVICES
                            Debug.Log($"Service {concreteType.Name} created via service initializer {serviceInitializer.GetType().Name} successfully.");
                            #endif

                            result = interfaceType.GetMethod(nameof(IServiceInitializer.InitTarget), new Type[] { firstArgumentType, secondArgumentType, thirdArgumentType, fourthArgumentType, fifthArgumentType, sixthArgumentType }).Invoke(serviceInitializer, new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument });
							return result is Task task ? await task.GetResult() : result;
                        }
                        #if DEBUG_INIT_SERVICES
                        else { Debug.Log($"Service {concreteType.Name} requires 6 arguments but instances not found among {services.Count} services..."); }
                        #endif

                        initialized.Remove(concreteType);
                    }

                    if(typeDefinition == typeof(IServiceInitializer<,,,,,>))
                    {
                        var argumentTypes = interfaceType.GetGenericArguments();
                        var firstArgumentType = argumentTypes[1];
                        var secondArgumentType = argumentTypes[2];
                        var thirdArgumentType = argumentTypes[3];
                        var fourthArgumentType = argumentTypes[4];
                        var fifthArgumentType = argumentTypes[5];
                        initialized.Add(concreteType);
                        if(TryGetOrCreateService(firstArgumentType, out object firstArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(secondArgumentType, out object secondArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(fourthArgumentType, out object fourthArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(fifthArgumentType, out object fifthArgument, initialized, scopedServiceInfos))
                        {
                            Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                            Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                            Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                            Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;
                            Task fifthArgumentTask = !fifthArgumentType.IsInstanceOfType(fifthArgument) ? fifthArgument as Task : null;

                            var loadArgumentTasks = Enumerable.Empty<Task>();

                            if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                            if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                            if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                            if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);
                            if(fifthArgumentTask != null) loadArgumentTasks.Append(fifthArgumentTask);

                            await Task.WhenAll(loadArgumentTasks);

                            firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                            secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                            thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                            fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);
                            fifthArgument = await InjectCrossServiceDependencies(fifthArgument, initialized, scopedServiceInfos);

                            #if DEBUG_CREATE_SERVICES
                            Debug.Log($"Service {concreteType.Name} created via service initializer {serviceInitializer.GetType().Name} successfully.");
                            #endif

                            result = interfaceType.GetMethod(nameof(IServiceInitializer.InitTarget), new Type[] { firstArgumentType, secondArgumentType, thirdArgumentType, fourthArgumentType, fifthArgumentType }).Invoke(serviceInitializer, new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument });
                            return result is Task task ? await task.GetResult() : result;
                        }
                        #if DEBUG_INIT_SERVICES
                        else { Debug.Log($"Service {concreteType.Name} requires 5 arguments but instances not found among {services.Count} services..."); }
                        #endif

                        initialized.Remove(concreteType);
                    }

                    if(typeDefinition == typeof(IServiceInitializer<,,,,>))
                    {
                        var argumentTypes = interfaceType.GetGenericArguments();
                        var firstArgumentType = argumentTypes[1];
                        var secondArgumentType = argumentTypes[2];
                        var thirdArgumentType = argumentTypes[3];
                        var fourthArgumentType = argumentTypes[4];
                        initialized.Add(concreteType);
                        if(TryGetOrCreateService(firstArgumentType, out object firstArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(secondArgumentType, out object secondArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(fourthArgumentType, out object fourthArgument, initialized, scopedServiceInfos))
                        {
                            Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                            Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                            Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                            Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;

                            var loadArgumentTasks = Enumerable.Empty<Task>();

                            if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                            if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                            if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                            if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);

                            await Task.WhenAll(loadArgumentTasks);

                            firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                            secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                            thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                            fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);

                            #if DEBUG_CREATE_SERVICES
                            Debug.Log($"Service {concreteType.Name} created via service initializer {serviceInitializer.GetType().Name} successfully.");
                            #endif

                            result = interfaceType.GetMethod(nameof(IServiceInitializer.InitTarget), new Type[] { firstArgumentType, secondArgumentType, thirdArgumentType, fourthArgumentType }).Invoke(serviceInitializer, new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument });
                            return result is Task task ? await task.GetResult() : result;
                        }
                        #if DEBUG_INIT_SERVICES
                        else { Debug.Log($"Service {concreteType.Name} requires 4 arguments but instances not found among {services.Count} services..."); }
                        #endif

                        initialized.Remove(concreteType);
                    }

                    if(typeDefinition == typeof(IServiceInitializer<,,,>))
                    {
                        var argumentTypes = interfaceType.GetGenericArguments();
                        var firstArgumentType = argumentTypes[1];
                        var secondArgumentType = argumentTypes[2];
                        var thirdArgumentType = argumentTypes[3];
                        initialized.Add(concreteType);
                        if(TryGetOrCreateService(firstArgumentType, out object firstArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(secondArgumentType, out object secondArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos))
                        {
                            Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                            Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                            Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;

                            var loadArgumentTasks = Enumerable.Empty<Task>();

                            if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                            if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                            if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);

                            await Task.WhenAll(loadArgumentTasks);

                            firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                            secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                            thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);

                            #if DEBUG_CREATE_SERVICES
                            Debug.Log($"Service {concreteType.Name} created via service initializer {serviceInitializer.GetType().Name} successfully.");
                            #endif

                            result = interfaceType.GetMethod(nameof(IServiceInitializer.InitTarget), new Type[] { firstArgumentType, secondArgumentType, thirdArgumentType }).Invoke(serviceInitializer, new object[] { firstArgument, secondArgument, thirdArgument });
                            return result is Task task ? await task.GetResult() : result;
                        }
                        #if DEBUG_INIT_SERVICES
                        else { Debug.Log($"Service {concreteType.Name} requires 3 arguments but instances not found among {services.Count} services..."); }
                        #endif

                        initialized.Remove(concreteType);
                    }

                    if(typeDefinition == typeof(IServiceInitializer<,,>))
                    {
                        var argumentTypes = interfaceType.GetGenericArguments();
                        var firstArgumentType = argumentTypes[1];
                        var secondArgumentType = argumentTypes[2];
                        initialized.Add(concreteType);

                        if(TryGetOrCreateService(firstArgumentType, out object firstArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(secondArgumentType, out object secondArgument, initialized, scopedServiceInfos))
                        {
                            Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                            Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;

                            var loadArgumentTasks = Enumerable.Empty<Task>();

                            if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                            if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);

                            await Task.WhenAll(loadArgumentTasks);

                            firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                            secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);

                            #if DEBUG_CREATE_SERVICES
                            Debug.Log($"Service {concreteType.Name} created via service initializer {serviceInitializer.GetType().Name} successfully.");
                            #endif

                            result = interfaceType.GetMethod(nameof(IServiceInitializer.InitTarget), new Type[] { firstArgumentType, secondArgumentType }).Invoke(serviceInitializer, new object[] { firstArgument, secondArgument });
                            return result is Task task ? await task.GetResult() : result;
                        }
                        #if DEBUG_INIT_SERVICES
                        else { Debug.Log($"Service {concreteType.Name} requires 2 arguments but instances not found among {services.Count} services..."); }
                        #endif

                        initialized.Remove(concreteType);
                    }

                    if(typeDefinition == typeof(IServiceInitializer<,>))
                    {
                        var argumentType = interfaceType.GetGenericArguments()[1];
                        initialized.Add(concreteType);

                        if(TryGetOrCreateService(argumentType, out object argument, initialized, scopedServiceInfos))
                        {
                            argument = await InjectCrossServiceDependencies(argument, initialized, scopedServiceInfos);

                            #if DEBUG_CREATE_SERVICES
                            Debug.Log($"Service {concreteType.Name} created via service initializer {serviceInitializer.GetType().Name} successfully.");
                            #endif

                            result = interfaceType.GetMethod(nameof(IServiceInitializer.InitTarget), new Type[] { argumentType }).Invoke(serviceInitializer, new object[] { argument });
							return result is Task task ? await task.GetResult() : result;
						}
                        #if DEBUG_INIT_SERVICES
						else { Debug.Log($"Service {concreteType.Name} requires argument {interfaceType.GetGenericArguments()[0].Name} but instance not found among {services.Count} services..."); }
                        #endif

                        initialized.Remove(concreteType);
                    }
                }

                var instance = serviceInitializer.InitTarget();
                if(instance != null)
				{
                    initialized.Add(concreteType);
                    return instance;
				}
			}

            if(serviceInfo.FindFromScene)
            {
                object instance;
                if(typeof(Component).IsAssignableFrom(concreteType))
                {
                    instance = 
                    #if UNITY_2023_1_OR_NEWER
                    Object.FindAnyObjectByType(concreteType, FindObjectsInactive.Include);
                    #else
                    Object.FindObjectOfType(concreteType, true);
                    #endif
                }
                else if(typeof(Component).IsAssignableFrom(serviceInfo.classWithAttribute))
                {
                    instance = 
                    #if UNITY_2023_1_OR_NEWER
                    Object.FindAnyObjectByType(serviceInfo.classWithAttribute, FindObjectsInactive.Include);
                    #else
                    Object.FindObjectOfType(serviceInfo.classWithAttribute, true);
                    #endif
                }
                else
				{
                    instance = Find.Any(concreteType, true);
				}

                if(instance != null)
                {
                    #if DEBUG_CREATE_SERVICES
                    Debug.Log($"Service {concreteType.Name} retrieved from scene successfully.", instance as Object);
                    #endif

                    if(IsInstanceOf(serviceInfo, instance))
					{
                        return instance;
					}

                    if(instance is IInitializer initializerWithAttribute)
					{
                        instance = initializerWithAttribute.InitTarget();
                        if(IsInstanceOf(serviceInfo, instance))
					    {
                            return instance;
					    }
                    }

                    if(instance is IValueProvider valueProvider && valueProvider.Value is object value)
                    {
                        #if DEBUG || INIT_ARGS_SAFE_MODE
                        if(!IsInstanceOf(serviceInfo, value))
					    {
                            throw new Exception($"Failed to convert service instance from {value.GetType().Name} to {serviceInfo.definingTypes.FirstOrDefault()?.Name}.");
					    }
                        #endif

                        return value;
                    }
                }

                var allInitializerInLoadedScenes = Find.All<IInitializer>();
                foreach(var initializer in allInitializerInLoadedScenes)
                {
                    if(TargetIsAssignableOrConvertibleToType(initializer, serviceInfo) && initializer.InitTarget() is object initializedTarget)
                    {
                        #if DEBUG_CREATE_SERVICES
                        Debug.Log($"Service {initializedTarget.GetType().Name} retrieved from scene successfully.", instance as Object);
                        #endif

                        // Support Initializer -> Initialized
                        if(IsInstanceOf(serviceInfo, instance))
					    {
                            return initializedTarget;
						}

                        // Support WrapperInitializer -> Wrapper -> Wrapped Object
                        if(initializedTarget is IValueProvider valueProvider)
					    {
                            return valueProvider.Value;
                        }
                    }
                }

                foreach(var servicesComponent in Find.All<Services>())
				{
					if(!servicesComponent.AreAvailableToAnyClient())
					{
						continue;
					}

					foreach(var serviceComponent in servicesComponent.providesServices)
					{
						if(serviceInfo.HasDefiningType(serviceComponent.definingType.Value) && TryGetService(serviceComponent.service, serviceInfo, out result))
						{
							return result;
						}
					}
				}

				if(typeof(ScriptableObject).IsAssignableFrom(concreteType))
                {
                    Debug.LogWarning($"Invalid Service Definition: Service '{concreteType.Name}' is a ScriptableObject type but has the {nameof(ServiceAttribute)} with {nameof(ServiceAttribute.FindFromScene)} set to true. ScriptableObjects can not exist in scenes and as such can't be retrived using this method.");
                    return null;
                }

                #if UNITY_EDITOR
                if(!IsFirstSceneInBuildSettingsLoaded()) { return null; }
                #endif

                Debug.LogWarning($"Service Not Found: There is no '{concreteType.Name}' found in the active scene {SceneManager.GetActiveScene().name}, but the service class has the {nameof(ServiceAttribute)} with {nameof(ServiceAttribute.FindFromScene)} set to true. Either add an instance to the scene or don't set {nameof(ServiceAttribute.FindFromScene)} true to have a new instance be created automatically.");
                return null;
            }

            if(serviceInfo.ResourcePath is string resourcePath)
            {
                Object asset;
                if(serviceInfo.LoadAsync)
				{
  					ResourceRequest resourceRequest = Resources.LoadAsync(resourcePath, typeof(Object));
                    #if UNITY_2023_2_OR_NEWER
                    await resourceRequest;
                    #else
                    while(!resourceRequest.isDone)
					{
                        await Task.Yield();
					}
                    #endif
                    
                    asset = resourceRequest.asset;
				}
                else
				{
                    asset = Resources.Load(resourcePath, typeof(Object));
				}

                if(asset == null)
                {
                    Debug.LogWarning($"Service Not Found: There is no '{concreteType.Name}' found at resource path 'Resources/{resourcePath}', but the service class has the {nameof(ServiceAttribute)} {nameof(ServiceAttribute.ResourcePath)} set to '{resourcePath}'. Either make sure an asset exists in the project at the expected path or don't specify a {nameof(ServiceAttribute.ResourcePath)} to have a new instance be created automatically.");
                    return null;
                }

                // If loaded asset is a prefab, instantiate a clone from it
                if(asset is GameObject gameObject && !gameObject.scene.IsValid() && TryGetServiceComponent(gameObject, serviceInfo, out Component component))
                {
                    result = await InstantiateServiceAsync(component, initialized, scopedServiceInfos);
                }
                else if(!TryGetService(asset, serviceInfo, out result))
                {
                    Debug.LogWarning($"Service Not Found: Resource at path 'Resources/{resourcePath}' could not be converted to type {serviceInfo.definingTypes.FirstOrDefault()?.Name}.");
                    return null;
                }

                if(result is IWrapper wrapper && Array.Exists(serviceInfo.definingTypes, t => !t.IsInstanceOfType(result)))
				{
                    result = wrapper.WrappedObject;
				}

                #if DEBUG_CREATE_SERVICES
                Debug.Log($"Service {concreteType.Name} loaded from resources successfully.", asset);
                #endif

                return result;
            }

            #if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
            if(serviceInfo.AddressableKey is string addressableKey)
            {
                var asyncOperation = Addressables.LoadAssetAsync<Object>(addressableKey);
                Object asset;

                if(serviceInfo.LoadAsync)
				{
                    asset = await asyncOperation.Task;
				}
                else
                {
                    asset = asyncOperation.WaitForCompletion();
                }

                if(asset == null)
                {
                    Debug.LogWarning($"Service Not Found: There is no '{concreteType.Name}' found in the Addressable registry under the address {addressableKey}, but the service class has the {nameof(ServiceAttribute)} with {nameof(ServiceAttribute.AddressableKey)} set to '{addressableKey}'. Either make sure an instance with the address exists in the project or don't specify a {nameof(ServiceAttribute.ResourcePath)} to have a new instance be created automatically.");
                    return null;
                }

                // If loaded asset is a prefab, instantiate a clone from it
                if(asset is GameObject gameObject && !gameObject.scene.IsValid() && TryGetServiceComponent(gameObject, serviceInfo, out Component component))
                {
                    result = await InstantiateServiceAsync(component, initialized, scopedServiceInfos);
                }
                else if(!TryGetService(asset, serviceInfo, out result))
                {
                    Debug.LogWarning($"Service Not Found: Addressable in the Addressable registry under the address {addressableKey} could not be converted to type {serviceInfo.definingTypes.FirstOrDefault()?.Name}.");
                    return null;
                }

                if(result is IWrapper wrapper && Array.Exists(serviceInfo.definingTypes, t => !t.IsInstanceOfType(result)))
				{
                    result = wrapper.WrappedObject;
				}

                #if DEBUG_CREATE_SERVICES
                if(result != null) { Debug.Log($"Service {concreteType.Name} loaded using Addressables successfully.", asset); }
                #endif

                return result;
                
            }
            #endif

            if(typeof(Component).IsAssignableFrom(concreteType))
            {
                if(container == null)
                {
                    CreateServicesContainer();
                }

                #if DEBUG_CREATE_SERVICES
                Debug.Log($"Service {concreteType.Name} added to Services container.", container);
                #endif

                return container.AddComponent(concreteType);
            }

            if(typeof(ScriptableObject).IsAssignableFrom(concreteType))
            {
                #if DEBUG_CREATE_SERVICES
                Debug.Log($"Service {concreteType.Name} created successfully.");
                #endif

                return ScriptableObject.CreateInstance(concreteType);
            }

            if(initialized.Contains(concreteType))
			{
                return null;
			}

            var constructors = concreteType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            if(constructors.Length == 0)
            {
                constructors = concreteType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
            }

            IEnumerable<ConstructorInfo> constructorsByParameterCount = constructors.Length <= 1 ? constructors : constructors.OrderByDescending(c => c.GetParameters().Length);
            foreach(var constructor in constructorsByParameterCount)
            {
                var parameters = constructor.GetParameters();

                switch(parameters.Length)
				{
                    case 1:
                        initialized.Add(concreteType);
                        Type firstArgumentType = parameters[0].ParameterType;
                        if(TryGetOrCreateService(firstArgumentType, out object firstArgument, initialized, scopedServiceInfos))
                        {
                            firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                            return constructor.Invoke(new object[] { firstArgument });
                        }
                        initialized.Remove(concreteType);
                        break;
                    case 2:
                        initialized.Add(concreteType);
                        firstArgumentType = parameters[0].ParameterType;
                        Type secondArgumentType = parameters[1].ParameterType;
                        if(TryGetOrCreateService(firstArgumentType, out firstArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(secondArgumentType, out object secondArgument, initialized, scopedServiceInfos))
                        {
                            Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                            Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;

                            var loadArgumentTasks = Enumerable.Empty<Task>();

                            if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                            if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);

                            await Task.WhenAll(loadArgumentTasks);

                            firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                            secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                            return constructor.Invoke(new object[] { firstArgument, secondArgument });
                        }
                        initialized.Remove(concreteType);
                        break;
                    case 3:
                        initialized.Add(concreteType);
                        firstArgumentType = parameters[0].ParameterType;
                        secondArgumentType = parameters[1].ParameterType;
                        Type thirdArgumentType = parameters[2].ParameterType;
                        if(TryGetOrCreateService(firstArgumentType, out firstArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(secondArgumentType, out secondArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos))
                        {
                            Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                            Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                            Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;

                            var loadArgumentTasks = Enumerable.Empty<Task>();

                            if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                            if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                            if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);

                            await Task.WhenAll(loadArgumentTasks);

                            firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                            secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                            thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                            return constructor.Invoke(new object[] { firstArgument, secondArgument, thirdArgument });
                        }
                        initialized.Remove(concreteType);
                        break;
                    case 4:
                        initialized.Add(concreteType);
                        firstArgumentType = parameters[0].ParameterType;
                        secondArgumentType = parameters[1].ParameterType;
                        thirdArgumentType = parameters[2].ParameterType;
                        Type fourthArgumentType = parameters[3].ParameterType;
                        if(TryGetOrCreateService(firstArgumentType, out firstArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(secondArgumentType, out secondArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(thirdArgumentType, out thirdArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(fourthArgumentType, out object fourthArgument, initialized, scopedServiceInfos))
                        {
                            Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                            Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                            Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                            Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;

                            var loadArgumentTasks = Enumerable.Empty<Task>();

                            if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                            if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                            if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                            if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);

                            await Task.WhenAll(loadArgumentTasks);

                            firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                            secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                            thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                            fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);
                            return constructor.Invoke(new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument });
                        }
                        initialized.Remove(concreteType);
                        break;
                    case 5:
                        initialized.Add(concreteType);
                        firstArgumentType = parameters[0].ParameterType;
                        secondArgumentType = parameters[1].ParameterType;
                        thirdArgumentType = parameters[2].ParameterType;
                        fourthArgumentType = parameters[3].ParameterType;
                        Type fifthArgumentType = parameters[4].ParameterType;
                        if(TryGetOrCreateService(firstArgumentType, out firstArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(secondArgumentType, out secondArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(thirdArgumentType, out thirdArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(fourthArgumentType, out fourthArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(fifthArgumentType, out object fifthArgument, initialized, scopedServiceInfos))
                        {
                            Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                            Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                            Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                            Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;
                            Task fifthArgumentTask = !fifthArgumentType.IsInstanceOfType(fifthArgument) ? fifthArgument as Task : null;
                            
                            var loadArgumentTasks = Enumerable.Empty<Task>();

                            if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                            if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                            if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                            if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);
                            if(fifthArgumentTask != null) loadArgumentTasks.Append(fifthArgumentTask);

                            await Task.WhenAll(loadArgumentTasks);

                            firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                            secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                            thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                            fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);
                            fifthArgument = await InjectCrossServiceDependencies(fifthArgument, initialized, scopedServiceInfos);
                            return constructor.Invoke(new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument });
                        }
                        initialized.Remove(concreteType);
                        break;
                    case 6:
                        initialized.Add(concreteType);
                        firstArgumentType = parameters[0].ParameterType;
                        secondArgumentType = parameters[1].ParameterType;
                        thirdArgumentType = parameters[2].ParameterType;
                        fourthArgumentType = parameters[3].ParameterType;
                        fifthArgumentType = parameters[4].ParameterType;
                        Type sixthArgumentType = parameters[5].ParameterType;
                        if(TryGetOrCreateService(firstArgumentType, out firstArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(secondArgumentType, out secondArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(thirdArgumentType, out thirdArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(fourthArgumentType, out fourthArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(fifthArgumentType, out fifthArgument, initialized, scopedServiceInfos)
                        && TryGetOrCreateService(sixthArgumentType, out object sixthArgument, initialized, scopedServiceInfos))
                        {
                            Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                            Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                            Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                            Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;
                            Task fifthArgumentTask = !fifthArgumentType.IsInstanceOfType(fifthArgument) ? fifthArgument as Task : null;
                            Task sixthArgumentTask = !sixthArgumentType.IsInstanceOfType(thirdArgument) ? sixthArgument as Task : null;

                            var loadArgumentTasks = Enumerable.Empty<Task>();

                            if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                            if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                            if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                            if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);
                            if(fifthArgumentTask != null) loadArgumentTasks.Append(fifthArgumentTask);
                            if(sixthArgumentTask != null) loadArgumentTasks.Append(sixthArgumentTask);

                            await Task.WhenAll(loadArgumentTasks);

                            firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                            secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                            thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                            fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);
                            fifthArgument = await InjectCrossServiceDependencies(fifthArgument, initialized, scopedServiceInfos);
                            sixthArgument = await InjectCrossServiceDependencies(sixthArgument, initialized, scopedServiceInfos);
                            return constructor.Invoke(new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument });
                        }
                        initialized.Remove(concreteType);
                        break;
                }
			}

            if(Array.Exists(constructors, c => c.GetParameters().Length == 0))
            {
                result = Activator.CreateInstance(concreteType);
            }
            else
			{
                result = null;
			}

            #if DEBUG || DEBUG_CREATE_SERVICES
            if(result == null)
            {
                Debug.Log($"Failed to create instance of Service {concreteType}. This could happen if some of the types it depends on are not services, are at least services that are accessible to every client and at this particular time.");
            }
            #endif
            #if DEBUG_CREATE_SERVICES
            else
            {
                Debug.Log($"Service {concreteType} created successfully via default constructor.");
            }
            #endif

            return result;
        }

        private static bool TargetIsAssignableOrConvertibleToType(IInitializer initializer, ServiceInfo serviceInfo)
		{
            foreach(var definingType in serviceInfo.definingTypes)
			{
                if(initializer.TargetIsAssignableOrConvertibleToType(definingType))
				{
                    return true;
				}
			}

            return false;
		}

        private static bool TryGetOrCreateService(Type definingType, out object service, HashSet<Type> initialized, [AllowNull] Dictionary<Type, ScopedServiceInfo> scopedServiceInfos, object client = null)
        {
            if(TryGetServiceFor(client, definingType, out service, initialized, scopedServiceInfos))
			{
                return true;
			}

            if(TryGetServiceInfo(definingType, out var serviceInfo))
			{
                service = GetOrCreateInstance(serviceInfo, initialized, scopedServiceInfos, serviceInfo.concreteType is null && !definingType.IsAbstract ? definingType : null);
                return service != null;
			}

            return false;
        }

        private static bool TryGetService([DisallowNull] Object unityObject, [DisallowNull] ServiceInfo serviceInfo, out object result)
        {
            if(unityObject is GameObject gameObject)
            {
                return TryGetService(gameObject, serviceInfo, out result);
            }

            if(IsInstanceOf(serviceInfo, unityObject))
            {
                result = unityObject;
                return true;
            }

            if(unityObject is IWrapper wrapper && wrapper.WrappedObject is object wrappedObject && IsInstanceOf(serviceInfo, wrappedObject))
            {
                result = wrappedObject;
                return true;
            }

            if(unityObject is IInitializer initializer && TargetIsAssignableOrConvertibleToType(initializer, serviceInfo))
            {
                result = initializer.InitTarget();
                return result != null;
            }

            result = null;
            return false;
        }

        private static bool TryGetService([DisallowNull] GameObject gameObject, ServiceInfo serviceInfo, [NotNullWhen(true), MaybeNullWhen(false)] out object result)
        {
            if(Find.In(gameObject, serviceInfo.ConcreteOrDefiningType, out result))
			{
                return true;
			}

            if(typeof(Component).IsAssignableFrom(serviceInfo.classWithAttribute) && gameObject.TryGetComponent(serviceInfo.classWithAttribute, out var componentWithAttribute))
            {
                if(IsInstanceOf(serviceInfo, componentWithAttribute))
                {
                    result = componentWithAttribute;
                    return true;
                }

                if(componentWithAttribute is IValueProvider valueProvider && valueProvider.Value is object value && IsInstanceOf(serviceInfo, value))
                {
                    result = value;
                    return true;
                }

                if(componentWithAttribute is IInitializer initializer && TargetIsAssignableOrConvertibleToType(initializer, serviceInfo))
                {
                    result = initializer.InitTarget();
                    if(IsInstanceOf(serviceInfo, result))
					{
                        return true;
					}

                    valueProvider = result as IValueProvider;
                    if(valueProvider != null)
					{
                        result = valueProvider.Value;

                        if(result is Component resultComponent && resultComponent.gameObject == gameObject)
						{
                            result = Object.Instantiate(componentWithAttribute);
						}
                    }

                    return result is not null;
                }

                if(componentWithAttribute is IValueByTypeProvider valueByTypeProvider)
                {
                    var method = typeof(IValueByTypeProvider).GetMethod(nameof(IValueByTypeProvider.TryGetFor));
                    var args = new object[] { componentWithAttribute, null };
                    if((bool)method.Invoke(valueByTypeProvider, args))
					{
                        result = args[1];

                        if(result is Component resultComponent && resultComponent.gameObject == gameObject)
						{
                            result = Object.Instantiate(componentWithAttribute);
						}

                        return true;
					}
                }

                if(componentWithAttribute is IValueProviderAsync valueProviderAsync)
                {
                    var method = typeof(IValueProviderAsync).GetMethod(nameof(IValueProviderAsync.GetForAsync));
                    var task = (ValueTask<object>)method.Invoke(valueProviderAsync, new object[] { componentWithAttribute });
                    result = task;
                    return !task.IsFaulted;
                }

                if(componentWithAttribute is IValueByTypeProviderAsync valueByTypeProviderAsync)
                {
                    var method = typeof(IValueByTypeProviderAsync).GetMethod(nameof(IValueByTypeProviderAsync.GetForAsync));
                    var task = method.Invoke(valueByTypeProviderAsync, new object[] { componentWithAttribute });
                    result = task;
                    return true;
                }

                result = null;
                return false;
            }

            var valueProviders = gameObject.GetComponents<IValueProvider>();
            foreach(var valueProvider in valueProviders)
            {
                var value = valueProvider.Value;
                if(value != null && IsInstanceOf(serviceInfo, value))
                {
                    result = value;
                    return true;
                }
            }

            foreach(var valueProvider in valueProviders)
            {
                if(valueProvider is IInitializer initializer && TargetIsAssignableOrConvertibleToType(initializer, serviceInfo))
                {
                    result = initializer.InitTarget();
                    return result != null;
                }
            }

            result = null;
            return false;
        }

        private static bool TryGetServiceComponent([DisallowNull] GameObject gameObject, ServiceInfo serviceInfo, [NotNullWhen(true), MaybeNullWhen(false)] out Component result)
        {
            if(Find.typesToFindableTypes.TryGetValue(serviceInfo.ConcreteOrDefiningType, out var findableTypes))
            {
                for(int i = findableTypes.Length - 1; i >= 0; i--)
                {
                    Type findableType = findableTypes[i];
                    if(typeof(Component).IsAssignableFrom(findableType) && gameObject.TryGetComponent(findableType, out result))
                    {
                        return true;
                    }
                }
            }

            if(Find.typesToFindableTypes.TryGetValue(serviceInfo.classWithAttribute, out findableTypes))
            {
                for(int i = findableTypes.Length - 1; i >= 0; i--)
                {
                    Type findableType = findableTypes[i];
                    if(typeof(Component).IsAssignableFrom(findableType) && gameObject.TryGetComponent(findableType, out result))
                    {
                        return true;
                    }
                }
            }

            var valueProviders = gameObject.GetComponents<IValueProvider>();
            foreach(var valueProvider in valueProviders)
            {
                var value = valueProvider.Value;
                if(value != null && IsInstanceOf(serviceInfo, value))
                {
                    if(value is Component component)
					{
                        result = component;
                        return true;
					}

                    if(Find.WrapperOf(value, out IWrapper wrapper))
					{
                        result = wrapper as Component;
                        return result != null;
					}
                }
            }

            foreach(var valueProvider in valueProviders)
            {
                if(valueProvider is IInitializer initializer && TargetIsAssignableOrConvertibleToType(initializer, serviceInfo))
                {
                    result = initializer as Component;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static bool IsInstanceOf(ServiceInfo serviceInfo, object instance)
		{
            if(serviceInfo.concreteType != null)
			{
                return serviceInfo.concreteType.IsInstanceOfType(instance);
			}

            foreach(var definingType in serviceInfo.definingTypes)
			{
                if(!definingType.IsInstanceOfType(instance))
                {
                    return false;
                }
			}

            return true;
		}

        private static bool TryGetServiceFor([AllowNull] object client, Type definingType, out object service, HashSet<Type> initialized, [AllowNull] Dictionary<Type, ScopedServiceInfo> scopedServiceInfos)
        {
            if(services.TryGetValue(definingType, out service))
            {
                return true;
            }

            if(!uninitializedServices.TryGetValue(definingType, out var definition)
                || !ServiceAttributeUtility.definingTypes.TryGetValue(definingType, out var serviceInfo))
            {
                // Also try to find scene from ServiceTag and Services components in the scene.
				if(FillIfEmpty(scopedServiceInfos).TryGetValue(definingType, out var scopedServiceInfo)
                && (scopedServiceInfo.toClients == Clients.Everywhere
			    || (Find.In(scopedServiceInfo.service, out Transform serviceTransform)
				&& Service.IsAccessibleTo(serviceTransform, scopedServiceInfo.toClients, client as Transform))))
				{
					service = scopedServiceInfo.service;
					return true;
				}

				return false;
            }

            #if DEV_MODE && DEBUG_LAZY_INIT
            Debug.Log($"Initializing service {definingType.Name} with LazyInit=true because it is a dependency of another service.");
            #endif

            uninitializedServices.Remove(serviceInfo.concreteType);
			foreach(var singleDefiningType in serviceInfo.definingTypes)
			{
                uninitializedServices.Remove(singleDefiningType);
			}

            service = LoadInstance(serviceInfo, initialized, scopedServiceInfos);
            if(service is null)
            {
                return false;
            }

            if(service is Task task)
			{
                services[serviceInfo.concreteType] = task;
                foreach(var singleDefiningType in serviceInfo.definingTypes)
				{
                    services[singleDefiningType] = task;
				}
                
                _ = SetInstanceAsync(serviceInfo, task);
                return true;
			}

            SetInstanceSync(serviceInfo, service);
            return service != null;
        }

        private static async Task SetInstanceAsync(ServiceInfo serviceInfo, Task loadServiceTask)
		{
			var service = await loadServiceTask.GetResult();

			SetInstanceSync(serviceInfo, service);

            SubscribeToUpdateEvents(service);
            ExecuteAwake(service);
            ExecuteOnEnable(service);
            ExecuteStartAtEndOfFrame(service);
		}

		private static void SetInstanceSync(ServiceInfo serviceInfo, object service) => SetInstanceSync(serviceInfo.definingTypes, service);

        private static void SetInstanceSync(Type[] definingTypes, object service)
		{
			services[service.GetType()] = service;

            foreach(var definingType in definingTypes)
            {
                services[definingType] = service;
                ServiceUtility.SetInstance(definingType, service);
            }
		}

        #if UNITY_EDITOR
		/// <summary>
		/// Warnings about missing Services should be suppressed when entering Play Mode from a scene
		/// which is not the first enabled one in build settings.
		/// </summary>
		/// <returns></returns>
		private static bool IsFirstSceneInBuildSettingsLoaded()
        {
            string firstSceneInBuildsPath = Array.Find(EditorBuildSettings.scenes, s => s.enabled)?.path ?? "";
            Scene firstSceneInBuilds = SceneManager.GetSceneByPath(firstSceneInBuildsPath);
            return firstSceneInBuilds.IsValid() && firstSceneInBuilds.isLoaded;
        }
        #endif

        private static void InjectCrossServiceDependencies(List<ServiceInfo> serviceInfos, HashSet<Type> initialized, [AllowNull] Dictionary<Type, ScopedServiceInfo> scopedServiceInfos)
        {
            foreach(var serviceInfo in serviceInfos)
            {
                var concreteOrDefiningType = serviceInfo.ConcreteOrDefiningType;
                if(!uninitializedServices.ContainsKey(concreteOrDefiningType)
                    && services.TryGetValue(concreteOrDefiningType, out var client))
                {
                    _ = InjectCrossServiceDependencies(client, initialized, scopedServiceInfos, serviceInfo.definingTypes);
                }
            }

            foreach(var scopedServiceInfo in FillIfEmpty(scopedServiceInfos).Values)
            {
                if(scopedServiceInfo.service is Object service && service)
                {
                    _ = InjectCrossServiceDependencies(scopedServiceInfo.service, initialized, scopedServiceInfos, scopedServiceInfo.definingType);
                }
            }
        }

        private static async Task<object> InjectCrossServiceDependencies(object client, HashSet<Type> initialized, [AllowNull] Dictionary<Type, ScopedServiceInfo> scopedServiceInfos, params Type[] definingTypes)
        {
            if(client is Task clientTask)
			{
                client = await clientTask.GetResult();
			}

            var concreteType = client.GetType();
            if(!initialized.Add(concreteType))
            {
                return client;
            }

            if(CanSelfInitialize(client, concreteType))
			{
                return client;
			}

            var interfaceTypes = concreteType.GetInterfaces();
            for(int i = interfaceTypes.Length - 1; i >= 0; i--)
            {
                var interfaceType = interfaceTypes[i];

                if(!interfaceType.IsGenericType)
                {
                    continue;
                }

                var typeDefinition = interfaceType.GetGenericTypeDefinition();
                if(typeDefinition == typeof(IInitializable<,,,,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();
                    var firstArgumentType = argumentTypes[0];
                    if(!TryGetServiceFor(client, firstArgumentType, out object firstArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(firstArgumentType);
                        return client;
					}

                    var secondArgumentType = argumentTypes[1];
                    if(!TryGetServiceFor(client, secondArgumentType, out object secondArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(secondArgumentType);
                        return client;
					}

                    var thirdArgumentType = argumentTypes[2];
                    if(!TryGetServiceFor(client, thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(thirdArgumentType);
                        return client;
					}

                    var fourthArgumentType = argumentTypes[3];
                    if(!TryGetServiceFor(client, fourthArgumentType, out object fourthArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(fourthArgumentType);
                        return client;
					}

                    var fifthArgumentType = argumentTypes[4];
                    if(!TryGetServiceFor(client, fifthArgumentType, out object fifthArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(fifthArgumentType);
                        return client;
					}

                    var sixthArgumentType = argumentTypes[5];
                    if(!TryGetServiceFor(client, sixthArgumentType, out object sixthArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(sixthArgumentType);
                        return client;
					}

                    Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                    Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                    Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                    Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;
                    Task fifthArgumentTask = !fifthArgumentType.IsInstanceOfType(fifthArgument) ? fifthArgument as Task : null;
                    Task sixthArgumentTask = !sixthArgumentType.IsInstanceOfType(thirdArgument) ? sixthArgument as Task : null;

                    var loadArgumentTasks = Enumerable.Empty<Task>();

                    if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                    if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                    if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                    if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);
                    if(fifthArgumentTask != null) loadArgumentTasks.Append(fifthArgumentTask);
                    if(sixthArgumentTask != null) loadArgumentTasks.Append(sixthArgumentTask);

                    await Task.WhenAll(loadArgumentTasks);

                    firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                    secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                    thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                    fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);
                    fifthArgument = await InjectCrossServiceDependencies(fifthArgument, initialized, scopedServiceInfos);
                    sixthArgument = await InjectCrossServiceDependencies(sixthArgument, initialized, scopedServiceInfos);

                    interfaceType.GetMethod(nameof(IInitializable<object>.Init)).Invoke(client, new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument });
                    return client;
                }

                if(typeDefinition == typeof(IInitializable<,,,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();
                    
                    var firstArgumentType = argumentTypes[0];
                    if(!TryGetServiceFor(client, firstArgumentType, out object firstArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(firstArgumentType);
                        return client;
					}

                    var secondArgumentType = argumentTypes[1];
                    if(!TryGetServiceFor(client, secondArgumentType, out object secondArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(secondArgumentType);
                        return client;
					}

                    var thirdArgumentType = argumentTypes[2];
                    if(!TryGetServiceFor(client, thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(thirdArgumentType);
                        return client;
					}

                    var fourthArgumentType = argumentTypes[3];
                    if(!TryGetServiceFor(client, fourthArgumentType, out object fourthArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(fourthArgumentType);
                        return client;
					}

                    var fifthArgumentType = argumentTypes[4];
                    if(!TryGetServiceFor(client, fifthArgumentType, out object fifthArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(fifthArgumentType);
                        return client;
					}

                    Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                    Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                    Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                    Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;
                    Task fifthArgumentTask = !fifthArgumentType.IsInstanceOfType(fifthArgument) ? fifthArgument as Task : null;

                    var loadArgumentTasks = Enumerable.Empty<Task>();

                    if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                    if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                    if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                    if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);
                    if(fifthArgumentTask != null) loadArgumentTasks.Append(fifthArgumentTask);

                    await Task.WhenAll(loadArgumentTasks);

                    firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                    secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                    thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                    fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);
                    fifthArgument = await InjectCrossServiceDependencies(fifthArgument, initialized, scopedServiceInfos);

                    interfaceType.GetMethod(nameof(IInitializable<object>.Init)).Invoke(client, new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument });
                    return client;
                }

                if(typeDefinition == typeof(IInitializable<,,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();

                    var firstArgumentType = argumentTypes[0];
                    if(!TryGetServiceFor(client, firstArgumentType, out object firstArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(firstArgumentType);
                        return client;
					}

                    var secondArgumentType = argumentTypes[1];
                    if(!TryGetServiceFor(client, secondArgumentType, out object secondArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(secondArgumentType);
                        return client;
					}

                    var thirdArgumentType = argumentTypes[2];
                    if(!TryGetServiceFor(client, thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(thirdArgumentType);
                        return client;
					}

                    var fourthArgumentType = argumentTypes[3];
                    if(!TryGetServiceFor(client, fourthArgumentType, out object fourthArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(fourthArgumentType);
                        return client;
					}

                    Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                    Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                    Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                    Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;

                    var loadArgumentTasks = Enumerable.Empty<Task>();

                    if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                    if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                    if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                    if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);

                    await Task.WhenAll(loadArgumentTasks);

                    firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                    secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                    thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                    fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);

                    interfaceType.GetMethod(nameof(IInitializable<object>.Init)).Invoke(client, new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument });
                    return client;
                }

                if(typeDefinition == typeof(IInitializable<,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();

                    var firstArgumentType = argumentTypes[0];
                    if(!TryGetServiceFor(client, firstArgumentType, out object firstArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(firstArgumentType);
                        return client;
					}

                    var secondArgumentType = argumentTypes[1];
                    if(!TryGetServiceFor(client, secondArgumentType, out object secondArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(secondArgumentType);
                        return client;
					}

                    var thirdArgumentType = argumentTypes[2];
                    if(!TryGetServiceFor(client, thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(thirdArgumentType);
                        return client;
					}

                    Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                    Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                    Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;

                    var loadArgumentTasks = Enumerable.Empty<Task>();

                    if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                    if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                    if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);

                    await Task.WhenAll(loadArgumentTasks);

                    firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                    secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                    thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);

                    interfaceType.GetMethod(nameof(IInitializable<object>.Init)).Invoke(client, new object[] { firstArgument, secondArgument, thirdArgument });
                    return client;
                }

                if(typeDefinition == typeof(IInitializable<,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();

                    var firstArgumentType = argumentTypes[0];
                    if(!TryGetServiceFor(client, firstArgumentType, out object firstArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(firstArgumentType);
                        return client;
					}

                    var secondArgumentType = argumentTypes[1];
                    if(!TryGetServiceFor(client, secondArgumentType, out object secondArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(secondArgumentType);
                        return client;
					}

                    Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                    Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;

                    var loadArgumentTasks = Enumerable.Empty<Task>();

                    if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                    if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);

                    await Task.WhenAll(loadArgumentTasks);

                    firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                    secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);

                    interfaceType.GetMethod(nameof(IInitializable<object>.Init)).Invoke(client, new object[] { firstArgument, secondArgument });
                    return client;
                }

                if(typeDefinition == typeof(IInitializable<>))
				{
					var argumentType = interfaceType.GetGenericArguments()[0];
					if(!TryGetServiceFor(client, argumentType, out object argument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(argumentType);
                        return client;
					}

					argument = await InjectCrossServiceDependencies(argument, initialized, scopedServiceInfos);

                    interfaceType.GetMethod(nameof(IInitializable<object>.Init)).Invoke(client, new object[] { argument });
					return client;
				}

				void LogMissingDependecyWarning(Type dependencyType)
                {
                    if(FillIfEmpty(scopedServiceInfos).TryGetValue(dependencyType, out var serviceInfo))
					{
                        if(serviceInfo.service == null)
						{
                            Debug.LogWarning($"Service {TypeUtility.ToString(concreteType)} requires argument {TypeUtility.ToString(dependencyType)} but reference to the service seems to be broken in the scene component.", client as Component);
						}
                        else
						{
                            Debug.LogWarning($"Service {TypeUtility.ToString(concreteType)} requires argument {TypeUtility.ToString(dependencyType)} but the service is only accessible to clients {serviceInfo.toClients}.", client as Component);
						}
					}
                    else
                    {
                        Debug.LogWarning($"Service {TypeUtility.ToString(concreteType)} requires argument {TypeUtility.ToString(dependencyType)} but instance not found among {services.Count + scopedServiceInfos.Count + uninitializedServices.Count} services:\n{string.Join("\n", services.Keys.Select(t => TypeUtility.ToString(t)).Concat(uninitializedServices.Keys.Select(t => TypeUtility.ToString(t))).Concat(scopedServiceInfos.Values.Select(i => TypeUtility.ToString(i.service?.GetType()))))}", client as Component);
                    }
                }
            }

            return client;
        }

        private static async Task<object> InstantiateServiceAsync(Component prefab, HashSet<Type> initialized, [AllowNull] Dictionary<Type, ScopedServiceInfo> scopedServiceInfos)
        {
            var concreteType = prefab.GetType();
            if(!initialized.Add(concreteType))
            {
                return null;
            }

            if(CanSelfInitialize(prefab, concreteType))
			{
                return Object.Instantiate(prefab);
			}

            var interfaceTypes = concreteType.GetInterfaces();
            for(int i = interfaceTypes.Length - 1; i >= 0; i--)
            {
                var interfaceType = interfaceTypes[i];
                if(!interfaceType.IsGenericType)
                {
                    continue;
                }

                var typeDefinition = interfaceType.GetGenericTypeDefinition();
                if(typeDefinition == typeof(IArgs<,,,,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();
                    var firstArgumentType = argumentTypes[0];
                    initialized.Add(concreteType);
                    if(!TryGetOrCreateService(firstArgumentType, out object firstArgument, initialized, scopedServiceInfos, prefab))
					{
                        LogMissingDependecyWarning(firstArgumentType);
                        return null;
					}

                    var secondArgumentType = argumentTypes[1];
                    if(!TryGetOrCreateService(secondArgumentType, out object secondArgument, initialized, scopedServiceInfos, prefab))
					{
                        LogMissingDependecyWarning(secondArgumentType);
                        return null;
					}

                    var thirdArgumentType = argumentTypes[2];
                    if(!TryGetOrCreateService(thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos, prefab))
					{
						LogMissingDependecyWarning(thirdArgumentType);
                        return null;
					}

                    var fourthArgumentType = argumentTypes[3];
                    if(!TryGetOrCreateService(fourthArgumentType, out object fourthArgument, initialized, scopedServiceInfos, prefab))
					{
						LogMissingDependecyWarning(fourthArgumentType);
                        return null;
					}

                    var fifthArgumentType = argumentTypes[4];
                    if(!TryGetOrCreateService(fifthArgumentType, out object fifthArgument, initialized, scopedServiceInfos, prefab))
					{
						LogMissingDependecyWarning(fifthArgumentType);
                        return null;
					}

                    var sixthArgumentType = argumentTypes[5];
                    if(!TryGetOrCreateService(sixthArgumentType, out object sixthArgument, initialized, scopedServiceInfos, prefab))
					{
						LogMissingDependecyWarning(sixthArgumentType);
                        return null;
					}

                    Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                    Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                    Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                    Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;
                    Task fifthArgumentTask = !fifthArgumentType.IsInstanceOfType(fifthArgument) ? fifthArgument as Task : null;
                    Task sixthArgumentTask = !sixthArgumentType.IsInstanceOfType(thirdArgument) ? sixthArgument as Task : null;

                    var loadArgumentTasks = Enumerable.Empty<Task>();

                    if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                    if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                    if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                    if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);
                    if(fifthArgumentTask != null) loadArgumentTasks.Append(fifthArgumentTask);
                    if(sixthArgumentTask != null) loadArgumentTasks.Append(sixthArgumentTask);

                    await Task.WhenAll(loadArgumentTasks);

                    firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                    secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                    thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                    fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);
                    fifthArgument = await InjectCrossServiceDependencies(fifthArgument, initialized, scopedServiceInfos);
                    sixthArgument = await InjectCrossServiceDependencies(sixthArgument, initialized, scopedServiceInfos);

                    MethodInfo instantiateMethod = typeof(ObjectExtensions).GetMember(nameof(ObjectExtensions.Instantiate), BindingFlags.Static | BindingFlags.Public)
                                                                .Select(member => (MethodInfo)member)
                                                                .FirstOrDefault(method => method.GetGenericArguments().Length == 7)
                                                                .MakeGenericMethod(concreteType, firstArgumentType, secondArgumentType, thirdArgumentType, fourthArgumentType, fifthArgumentType, sixthArgumentType);
                    return instantiateMethod.Invoke(null, new object[] { prefab, firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument });
                }

                if(typeDefinition == typeof(IArgs<,,,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();
                    
                    var firstArgumentType = argumentTypes[0];
                    if(!TryGetServiceFor(prefab, firstArgumentType, out object firstArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(firstArgumentType);
                        return null;
					}

                    var secondArgumentType = argumentTypes[1];
                    if(!TryGetServiceFor(prefab, secondArgumentType, out object secondArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(secondArgumentType);
                        return null;
					}

                    var thirdArgumentType = argumentTypes[2];
                    if(!TryGetServiceFor(prefab, thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(thirdArgumentType);
                        return null;
					}

                    var fourthArgumentType = argumentTypes[3];
                    if(!TryGetServiceFor(prefab, fourthArgumentType, out object fourthArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(fourthArgumentType);
                        return null;
					}

                    var fifthArgumentType = argumentTypes[4];
                    if(!TryGetServiceFor(prefab, fifthArgumentType, out object fifthArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(fifthArgumentType);
                        return null;
					}

                    Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                    Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                    Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                    Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;
                    Task fifthArgumentTask = !fifthArgumentType.IsInstanceOfType(fifthArgument) ? fifthArgument as Task : null;

                    var loadArgumentTasks = Enumerable.Empty<Task>();

                    if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                    if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                    if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                    if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);
                    if(fifthArgumentTask != null) loadArgumentTasks.Append(fifthArgumentTask);

                    await Task.WhenAll(loadArgumentTasks);

                    firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                    secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                    thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                    fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);
                    fifthArgument = await InjectCrossServiceDependencies(fifthArgument, initialized, scopedServiceInfos);

                    MethodInfo instantiateMethod = typeof(ObjectExtensions).GetMember(nameof(ObjectExtensions.Instantiate), BindingFlags.Static | BindingFlags.Public)
                                                                .Select(member => (MethodInfo)member)
                                                                .FirstOrDefault(method => method.GetGenericArguments().Length == 6)
                                                                .MakeGenericMethod(concreteType, firstArgumentType, secondArgumentType, thirdArgumentType, fourthArgumentType, fifthArgumentType);
                    return instantiateMethod.Invoke(null, new object[] { prefab, firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument });
                }

                if(typeDefinition == typeof(IArgs<,,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();

                    var firstArgumentType = argumentTypes[0];
                    if(!TryGetServiceFor(prefab, firstArgumentType, out object firstArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(firstArgumentType);
                        return null;
					}

                    var secondArgumentType = argumentTypes[1];
                    if(!TryGetServiceFor(prefab, secondArgumentType, out object secondArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(secondArgumentType);
                        return null;
					}

                    var thirdArgumentType = argumentTypes[2];
                    if(!TryGetServiceFor(prefab, thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(thirdArgumentType);
                        return null;
					}

                    var fourthArgumentType = argumentTypes[3];
                    if(!TryGetServiceFor(prefab, fourthArgumentType, out object fourthArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(fourthArgumentType);
                        return null;
					}

                    Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                    Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                    Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                    Task fourthArgumentTask = !fourthArgumentType.IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;

                    var loadArgumentTasks = Enumerable.Empty<Task>();

                    if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                    if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                    if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                    if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);

                    await Task.WhenAll(loadArgumentTasks);

                    firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                    secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                    thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);
                    fourthArgument = await InjectCrossServiceDependencies(fourthArgument, initialized, scopedServiceInfos);

                    MethodInfo instantiateMethod = typeof(ObjectExtensions).GetMember(nameof(ObjectExtensions.Instantiate), BindingFlags.Static | BindingFlags.Public)
                                                                .Select(member => (MethodInfo)member)
                                                                .FirstOrDefault(method => method.GetGenericArguments().Length == 5)
                                                                .MakeGenericMethod(concreteType, firstArgumentType, secondArgumentType, thirdArgumentType, fourthArgumentType);
                    return instantiateMethod.Invoke(null, new object[] { prefab, firstArgument, secondArgument, thirdArgument, fourthArgument });
                }

                if(typeDefinition == typeof(IArgs<,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();

                    var firstArgumentType = argumentTypes[0];
                    if(!TryGetServiceFor(prefab, firstArgumentType, out object firstArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(firstArgumentType);
                        return null;
					}

                    var secondArgumentType = argumentTypes[1];
                    if(!TryGetServiceFor(prefab, secondArgumentType, out object secondArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(secondArgumentType);
                        return null;
					}

                    var thirdArgumentType = argumentTypes[2];
                    if(!TryGetServiceFor(prefab, thirdArgumentType, out object thirdArgument, initialized, scopedServiceInfos))
					{
						LogMissingDependecyWarning(thirdArgumentType);
                        return null;
					}

                    Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                    Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                    Task thirdArgumentTask = !thirdArgumentType.IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;

                    var loadArgumentTasks = Enumerable.Empty<Task>();

                    if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                    if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                    if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);

                    await Task.WhenAll(loadArgumentTasks);

                    firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                    secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);
                    thirdArgument = await InjectCrossServiceDependencies(thirdArgument, initialized, scopedServiceInfos);

                    MethodInfo instantiateMethod = typeof(ObjectExtensions).GetMember(nameof(ObjectExtensions.Instantiate), BindingFlags.Static | BindingFlags.Public)
                                                                .Select(member => (MethodInfo)member)
                                                                .FirstOrDefault(method => method.GetGenericArguments().Length == 4)
                                                                .MakeGenericMethod(concreteType, firstArgumentType, secondArgumentType, thirdArgumentType);
                    return instantiateMethod.Invoke(null, new object[] { prefab, firstArgument, secondArgument, thirdArgument });
                }

                if(typeDefinition == typeof(IArgs<,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();

                    var firstArgumentType = argumentTypes[0];
                    if(!TryGetServiceFor(prefab, firstArgumentType, out object firstArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(firstArgumentType);
                        return null;
					}

                    var secondArgumentType = argumentTypes[1];
                    if(!TryGetServiceFor(prefab, secondArgumentType, out object secondArgument, initialized, scopedServiceInfos))
					{
                        LogMissingDependecyWarning(secondArgumentType);
                        return null;
					}

                    Task firstArgumentTask = !firstArgumentType.IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                    Task secondArgumentTask = !secondArgumentType.IsInstanceOfType(secondArgument) ? secondArgument as Task : null;

                    var loadArgumentTasks = Enumerable.Empty<Task>();

                    if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                    if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);

                    await Task.WhenAll(loadArgumentTasks);

                    firstArgument = await InjectCrossServiceDependencies(firstArgument, initialized, scopedServiceInfos);
                    secondArgument = await InjectCrossServiceDependencies(secondArgument, initialized, scopedServiceInfos);

                    MethodInfo instantiateMethod = typeof(ObjectExtensions).GetMember(nameof(ObjectExtensions.Instantiate), BindingFlags.Static | BindingFlags.Public)
                                                                .Select(member => (MethodInfo)member)
                                                                .FirstOrDefault(method => method.GetGenericArguments().Length == 3)
                                                                .MakeGenericMethod(concreteType, firstArgumentType, secondArgumentType);
                    return instantiateMethod.Invoke(null, new object[] { prefab, firstArgument, secondArgument });
                }

                if(typeDefinition == typeof(IArgs<>))
				{
					var argumentType = interfaceType.GetGenericArguments()[0];
                    if(!TryGetOrCreateService(argumentType, out object argument, initialized, scopedServiceInfos, prefab))
					{
						LogMissingDependecyWarning(argumentType);
                        return null;
					}

					argument = await InjectCrossServiceDependencies(argument, initialized, scopedServiceInfos);

                    MethodInfo instantiateMethod = typeof(ObjectExtensions).GetMember(nameof(ObjectExtensions.Instantiate), BindingFlags.Static | BindingFlags.Public)
                                                                .Select(member => (MethodInfo)member)
                                                                .FirstOrDefault(method => method.GetGenericArguments().Length == 2)
                                                                .MakeGenericMethod(concreteType, argumentType);
                    return instantiateMethod.Invoke(null, new object[] { prefab, argument });
				}

				void LogMissingDependecyWarning(Type dependencyType)
                {
                    if(FillIfEmpty(scopedServiceInfos).TryGetValue(dependencyType, out var serviceInfo))
					{
                        if(serviceInfo.service == null)
						{
                            Debug.LogWarning($"Service {TypeUtility.ToString(concreteType)} requires argument {TypeUtility.ToString(dependencyType)} but reference to the service seems to be broken in the scene component.", prefab);
						}
                        else
						{
                            Debug.LogWarning($"Service {TypeUtility.ToString(concreteType)} requires argument {TypeUtility.ToString(dependencyType)} but the service is only accessible to clients {serviceInfo.toClients}.", prefab);
						}
					}
                    else
                    {
                        Debug.LogWarning($"Service {TypeUtility.ToString(concreteType)} requires argument {TypeUtility.ToString(dependencyType)} but instance not found among {services.Count + scopedServiceInfos.Count + uninitializedServices.Count} services:\n{string.Join("\n", services.Keys.Select(t => TypeUtility.ToString(t)).Concat(uninitializedServices.Keys.Select(t => TypeUtility.ToString(t))).Concat(scopedServiceInfos.Values.Select(i => TypeUtility.ToString(i.service?.GetType()))))}", prefab);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Does the client have an initializer or a base class that can handle automatically initializing itself with all services?
        /// </summary>
        private static bool CanSelfInitialize([DisallowNull] object client, [DisallowNull] Type clientType) => (client is Component && TypeUtility.DerivesFromGenericBaseType(clientType)) || InitializerUtility.HasInitializer(client);
        private static bool CanSelfInitialize([DisallowNull] Component client, [DisallowNull] Type clientType) => TypeUtility.DerivesFromGenericBaseType(clientType) || InitializerUtility.HasInitializer(client);

        private static void InjectServiceDependenciesForTypesThatRequireOnlyThem(out List<Task> injectAsyncServices
        #if UNITY_EDITOR
        , Dictionary<Type, List<ScriptableObject>> uninitializedScriptableObjects    
        #endif
        )
        {
            injectAsyncServices = new List<Task>(32);

            if(services is null)
            {
                return;
            }

            var setMethodsByArgumentCount = GetInitArgsSetMethods();

            var setMethodArgumentTypes = new Type[] { typeof(Type), null, typeof(bool) };
            var setMethodArguments = new object[3];
            foreach(var clientType in GetImplementingTypes<IOneArgument>())
            {
                if(!clientType.IsAbstract)
                {
                    var task = TrySetOneDefaultService(clientType, clientType.GetInterfaces(), setMethodsByArgumentCount[1], setMethodArgumentTypes, setMethodArguments
                    #if UNITY_EDITOR
                    , uninitializedScriptableObjects
                    #endif
                    );
                        
                    if(!task.IsCompleted)
					{
                        injectAsyncServices.Add(task);
					}
                }
            }

            setMethodArgumentTypes = new Type[] { typeof(Type), null, null, typeof(bool) };
            setMethodArguments = new object[4];
            foreach(var clientType in GetImplementingTypes<ITwoArguments>())
            {
                if(!clientType.IsAbstract)
                {
                    var task = TrySetTwoDefaultServices(clientType, clientType.GetInterfaces(), setMethodsByArgumentCount[2], setMethodArgumentTypes, setMethodArguments
                    #if UNITY_EDITOR
                    , uninitializedScriptableObjects
                    #endif
                    );

                    if(!task.IsCompleted)
					{
                        injectAsyncServices.Add(task);
					}
                }
            }

            setMethodArgumentTypes = new Type[] { typeof(Type), null, null, null, typeof(bool) };
            setMethodArguments = new object[5];
            foreach(var clientType in GetImplementingTypes<IThreeArguments>())
            {
                if(!clientType.IsAbstract)
                {
                    var task = TrySetThreeDefaultServices(clientType, clientType.GetInterfaces(), setMethodsByArgumentCount[3], setMethodArgumentTypes, setMethodArguments
                    #if UNITY_EDITOR
                    , uninitializedScriptableObjects
                    #endif
                    );

                    if(!task.IsCompleted)
					{
                        injectAsyncServices.Add(task);
					}
                }
            }

            setMethodArgumentTypes = new Type[] { typeof(Type), null, null, null, null, typeof(bool) };
            setMethodArguments = new object[6];
            foreach(var clientType in GetImplementingTypes<IFourArguments>())
            {
                if(!clientType.IsAbstract)
                {
                    var task = TrySetFourDefaultServices(clientType, clientType.GetInterfaces(), setMethodsByArgumentCount[4], setMethodArgumentTypes, setMethodArguments
                    #if UNITY_EDITOR
                    , uninitializedScriptableObjects
                    #endif
                    );

                    if(!task.IsCompleted)
					{
                        injectAsyncServices.Add(task);
					}
                }
            }

            setMethodArgumentTypes = new Type[] { typeof(Type), null, null, null, null, null, typeof(bool) };
            setMethodArguments = new object[7];
            foreach(var clientType in GetImplementingTypes<IFiveArguments>())
            {
                if(!clientType.IsAbstract)
                {
                    var task = TrySetFiveDefaultServices(clientType, clientType.GetInterfaces(), setMethodsByArgumentCount[5], setMethodArgumentTypes, setMethodArguments
                    #if UNITY_EDITOR
                    , uninitializedScriptableObjects
                    #endif
                    );

                    if(!task.IsCompleted)
					{
                        injectAsyncServices.Add(task);
					}
                }
            }

            setMethodArgumentTypes = new Type[] { typeof(Type), null, null, null, null, null, null, typeof(bool) };
            setMethodArguments = new object[8];
            foreach(var clientType in GetImplementingTypes<ISixArguments>())
            {
                if(!clientType.IsAbstract)
                {
                    var task = TrySetSixDefaultServices(clientType, clientType.GetInterfaces(), setMethodsByArgumentCount[6], setMethodArgumentTypes, setMethodArguments
                    #if UNITY_EDITOR
                    , uninitializedScriptableObjects
                    #endif
                    );

                    if(!task.IsCompleted)
					{
                        injectAsyncServices.Add(task);
					}
                }
            }
        }

        private static Dictionary<int, MethodInfo> GetInitArgsSetMethods()
        {
            const int MAX_INIT_ARGUMENT_COUNT = 6;

            Dictionary<int, MethodInfo> setMethodsByArgumentCount = new Dictionary<int, MethodInfo>(MAX_INIT_ARGUMENT_COUNT);

            foreach(MethodInfo method in typeof(InitArgs).GetMember(nameof(InitArgs.Set), MemberTypes.Method, BindingFlags.Static | BindingFlags.NonPublic))
            {
                var genericArguments = method.GetGenericArguments();
                var parameters = method.GetParameters();
                int genericArgumentCount = genericArguments.Length;
                if(genericArgumentCount == parameters.Length - 1) // in addition to the init argument types, there is clientType
                {
                    setMethodsByArgumentCount.Add(genericArgumentCount, method);
                }
            }

            #if DEV_MODE
            Debug.Assert(setMethodsByArgumentCount.Count == MAX_INIT_ARGUMENT_COUNT);
            #endif

            return setMethodsByArgumentCount;
        }

        private static async Task TrySetOneDefaultService(Type clientType, Type[] interfaceTypes, MethodInfo setMethod, Type[] setMethodArgumentTypes, object[] setMethodArguments
        #if UNITY_EDITOR
        , Dictionary<Type, List<ScriptableObject>> uninitializedScriptableObjects
        #endif
        )
        {
            for(int i = interfaceTypes.Length - 1; i >= 0; i--)
            {
                var interfaceType = interfaceTypes[i];

                if(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IArgs<>))
				{
					var argumentTypes = interfaceType.GetGenericArguments();
					var argumentType = argumentTypes[0];
                    if(services.TryGetValue(argumentType, out object argument))
					{
						if(!argumentType.IsInstanceOfType(argument) && argument is Task task)
						{
                            #if DEBUG || INIT_ARGS_SAFE_MODE
							try
							{
                            #endif

								argument = await task.GetResult();

                            #if DEBUG || INIT_ARGS_SAFE_MODE
							}
							catch(Exception e)
							{
								Debug.LogWarning(e.ToString());
							}
                            #endif
						}

						setMethodArgumentTypes[1] = argumentType;
						setMethodArguments[0] = clientType;
						setMethodArguments[1] = argument;
						setMethodArguments[2] = true;

						setMethod = setMethod.MakeGenericMethod(argumentTypes);

                        #if DEV_MODE && DEBUG_INIT_SERVICES
                        Debug.Log($"Providing 1 service for client {clientType.Name}: {argument.GetType().Name}.");
                        #endif

						setMethod.Invoke(null, setMethodArguments);

                        #if UNITY_EDITOR
						// In builds ScriptableObjects' Awake method gets called at runtime when a scene or prefab with a component referencing said ScriptableObject gets loaded.
						// Because of this we can rely on ServiceInjector to execute via the RuntimeInitializeOnLoadMethod attribute before the Awake methods of ScriptableObjects are executed.
						// In the editor however, ScriptableObject can already get loaded in edit mode, in which case Awake gets executed before service injection has taken place.
						// For this reason we need to manually initialize these ScriptableObjects.
						if(uninitializedScriptableObjects.TryGetValue(clientType, out var scriptableObjects))
						{
							for(int s = scriptableObjects.Count - 1; s >= 0; s--)
							{
								ScriptableObject scriptableObject = scriptableObjects[s];
								if(scriptableObject is IInitializableEditorOnly initializableEditorOnly
									&& initializableEditorOnly.Initializer is IInitializer initializer)
								{
									try
									{
										initializer.InitTarget();
									}
									catch(Exception ex)
									{
										Debug.LogError(ex);
									}
									scriptableObjects.RemoveAt(s);
									continue;
								}

								try
								{
									var initMethod = typeof(IInitializable<>).MakeGenericType(argumentTypes).GetMethod(nameof(IInitializable<object>.Init));
									initMethod.Invoke(scriptableObject, new object[] { argument });
								}
								catch(Exception ex)
								{
									Debug.LogError(ex);
								}
							}
						}
                        #endif
					}

					return;
				}
			}
        }

		private static async Task TrySetTwoDefaultServices(Type clientType, Type[] interfaceTypes, MethodInfo setMethod, Type[] setMethodArgumentTypes, object[] setMethodArguments
        #if UNITY_EDITOR
        , Dictionary<Type, List<ScriptableObject>> uninitializedScriptableObjects
        #endif
        )
        {
            for(int i = interfaceTypes.Length - 1; i >= 0; i--)
            {
                var interfaceType = interfaceTypes[i];
                if(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IArgs<,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();
                    if(services.TryGetValue(argumentTypes[0], out object firstArgument) && services.TryGetValue(argumentTypes[1], out object secondArgument))
                    {
                        Task firstArgumentTask = !argumentTypes[0].IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                        Task secondArgumentTask = !argumentTypes[1].IsInstanceOfType(secondArgument) ? secondArgument as Task : null;

                        if(firstArgumentTask != null || secondArgumentTask != null)
						{
                            var loadArgumentTasks = Enumerable.Empty<Task>();

                            if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                            if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);

                            #if DEBUG || INIT_ARGS_SAFE_MODE
                            try
                            {
                            #endif

                            await Task.WhenAll(loadArgumentTasks);

                            #if DEBUG || INIT_ARGS_SAFE_MODE
                            }
                            catch(Exception e)
							{
                                Debug.LogWarning(e.ToString());
							}
                            #endif

                            if(firstArgumentTask != null) firstArgument = await firstArgumentTask.GetResult();
                            if(secondArgumentTask != null) secondArgument = await secondArgumentTask.GetResult();
                        }

                        setMethodArgumentTypes[1] = argumentTypes[0];
                        setMethodArgumentTypes[2] = argumentTypes[1];
                        setMethodArguments[0] = clientType;
                        setMethodArguments[1] = firstArgument;
                        setMethodArguments[2] = secondArgument;
                        setMethodArguments[3] = true;

                        setMethod = setMethod.MakeGenericMethod(argumentTypes);

                        #if DEV_MODE && DEBUG_INIT_SERVICES
                        Debug.Log($"Providing 2 services for client {clientType.Name}: {firstArgument.GetType().Name}, {secondArgument.GetType().Name}.");
                        #endif

                        setMethod.Invoke(null, setMethodArguments);

                        #if UNITY_EDITOR
                        // In builds ScriptableObjects' Awake method gets called at runtime when a scene or prefab with a component referencing said ScriptableObject gets loaded.
                        // Because of this we can rely on ServiceInjector to execute via the RuntimeInitializeOnLoadMethod attribute before the Awake methods of ScriptableObjects are executed.
                        // In the editor however, ScriptableObject can already get loaded in edit mode, in which case Awake gets executed before service injection has taken place.
                        // For this reason we need to manually initialize these ScriptableObjects.
                        if(uninitializedScriptableObjects.TryGetValue(clientType, out var scriptableObjects))
						{
                            for(int s = scriptableObjects.Count - 1; s >= 0; s--)
		                    {
								ScriptableObject scriptableObject = scriptableObjects[s];
								if(scriptableObject is IInitializableEditorOnly initializableEditorOnly
                                    && initializableEditorOnly.Initializer is IInitializer initializer)
								{
                                    try
                                    {
                                        initializer.InitTarget();
                                    }
                                    catch(Exception ex)
								    {
                                        Debug.LogError(ex);
								    }
                                    scriptableObjects.RemoveAt(s);
                                    continue;
								}

                                try
                                {
                                    var initMethod = typeof(IInitializable<,>).MakeGenericType(argumentTypes).GetMethod(nameof(IInitializable<object>.Init));
                                    initMethod.Invoke(scriptableObject, new object[] { firstArgument, secondArgument });
                                }
                                catch(Exception ex)
								{
                                    Debug.LogError(ex);
								}
		                    }

                            uninitializedScriptableObjects.Remove(clientType);
						}
                        #endif
                    }
                    return;
                }
            }
        }

        private static async Task TrySetThreeDefaultServices(Type clientType, Type[] interfaceTypes, MethodInfo setMethod, Type[] setMethodArgumentTypes, object[] setMethodArguments
        #if UNITY_EDITOR
        , Dictionary<Type, List<ScriptableObject>> uninitializedScriptableObjects
        #endif
        )
        {
            for(int i = interfaceTypes.Length - 1; i >= 0; i--)
            {
                var interfaceType = interfaceTypes[i];
                if(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IArgs<,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();
                    if(services.TryGetValue(argumentTypes[0], out object firstArgument) && services.TryGetValue(argumentTypes[1], out object secondArgument)
                        && services.TryGetValue(argumentTypes[2], out object thirdArgument))
                    {
                        Task firstArgumentTask = !argumentTypes[0].IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                        Task secondArgumentTask = !argumentTypes[1].IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                        Task thirdArgumentTask = !argumentTypes[2].IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                        
                        var loadArgumentTasks = Enumerable.Empty<Task>();

                        if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                        if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                        if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);

                        await Task.WhenAll(loadArgumentTasks);

                        if(firstArgumentTask != null) firstArgument = await firstArgumentTask.GetResult();
                        if(secondArgumentTask != null) secondArgument = await secondArgumentTask.GetResult();
                        if(thirdArgumentTask != null) thirdArgument = await thirdArgumentTask.GetResult();

                        setMethodArgumentTypes[1] = argumentTypes[0];
                        setMethodArgumentTypes[2] = argumentTypes[1];
                        setMethodArgumentTypes[3] = argumentTypes[2];
                        setMethodArguments[0] = clientType;
                        setMethodArguments[1] = firstArgument;
                        setMethodArguments[2] = secondArgument;
                        setMethodArguments[3] = thirdArgument;
                        setMethodArguments[4] = true;

                        setMethod = setMethod.MakeGenericMethod(argumentTypes);

                        #if DEV_MODE && DEBUG_INIT_SERVICES
                        Debug.Log($"Providing 3 services for client {clientType.Name}: {firstArgument.GetType().Name}, {secondArgument.GetType().Name}, {thirdArgument.GetType().Name}.");
                        #endif

                        setMethod.Invoke(null, setMethodArguments);

                        #if UNITY_EDITOR
                        // In builds ScriptableObjects' Awake method gets called at runtime when a scene or prefab with a component referencing said ScriptableObject gets loaded.
                        // Because of this we can rely on ServiceInjector to execute via the RuntimeInitializeOnLoadMethod attribute before the Awake methods of ScriptableObjects are executed.
                        // In the editor however, ScriptableObject can already get loaded in edit mode, in which case Awake gets executed before service injection has taken place.
                        // For this reason we need to manually initialize these ScriptableObjects.
                        if(uninitializedScriptableObjects.TryGetValue(clientType, out var scriptableObjects))
						{
                            for(int s = scriptableObjects.Count - 1; s >= 0; s--)
		                    {
								ScriptableObject scriptableObject = scriptableObjects[s];
								if(scriptableObject is IInitializableEditorOnly initializableEditorOnly
                                    && initializableEditorOnly.Initializer is IInitializer initializer)
								{
                                    try
                                    {
                                        initializer.InitTarget();
                                    }
                                    catch(Exception ex)
								    {
                                        Debug.LogError(ex);
								    }
                                    scriptableObjects.RemoveAt(s);
                                    continue;
								}

                                try
                                {
                                    var initMethod = typeof(IInitializable<,,>).MakeGenericType(argumentTypes).GetMethod(nameof(IInitializable<object>.Init));
                                    initMethod.Invoke(scriptableObject, new object[] { firstArgument, secondArgument, thirdArgument });
                                }
                                catch(Exception ex)
								{
                                    Debug.LogError(ex);
								}
		                    }

                            uninitializedScriptableObjects.Remove(clientType);
						}
                        #endif
                    }
                    return;
                }
            }
        }

        private static async Task TrySetFourDefaultServices(Type clientType, Type[] interfaceTypes, MethodInfo setMethod, Type[] setMethodArgumentTypes, object[] setMethodArguments
        #if UNITY_EDITOR
        , Dictionary<Type, List<ScriptableObject>> uninitializedScriptableObjects
        #endif
        )
        {
            for(int i = interfaceTypes.Length - 1; i >= 0; i--)
            {
                var interfaceType = interfaceTypes[i];
                if(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IArgs<,,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();
                    if(services.TryGetValue(argumentTypes[0], out object firstArgument) && services.TryGetValue(argumentTypes[1], out object secondArgument)
                        && services.TryGetValue(argumentTypes[2], out object thirdArgument) && services.TryGetValue(argumentTypes[3], out object fourthArgument))
                    {
                        Task firstArgumentTask = !argumentTypes[0].IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                        Task secondArgumentTask = !argumentTypes[1].IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                        Task thirdArgumentTask = !argumentTypes[2].IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                        Task fourthArgumentTask = !argumentTypes[3].IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;

                        var loadArgumentTasks = Enumerable.Empty<Task>();

                        if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                        if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                        if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                        if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);

                        await Task.WhenAll(loadArgumentTasks);

                        if(firstArgumentTask != null) firstArgument = await firstArgumentTask.GetResult();
                        if(secondArgumentTask != null) secondArgument = await secondArgumentTask.GetResult();
                        if(thirdArgumentTask != null) thirdArgument = await thirdArgumentTask.GetResult();
                        if(fourthArgumentTask != null) fourthArgument = await fourthArgumentTask.GetResult();

                        setMethodArgumentTypes[1] = argumentTypes[0];
                        setMethodArgumentTypes[2] = argumentTypes[1];
                        setMethodArgumentTypes[3] = argumentTypes[2];
                        setMethodArgumentTypes[4] = argumentTypes[3];
                        setMethodArguments[0] = clientType;
                        setMethodArguments[1] = firstArgument;
                        setMethodArguments[2] = secondArgument;
                        setMethodArguments[3] = thirdArgument;
                        setMethodArguments[4] = fourthArgument;
                        setMethodArguments[5] = true;

                        setMethod = setMethod.MakeGenericMethod(argumentTypes);

                        #if DEV_MODE && DEBUG_INIT_SERVICES
                        Debug.Log($"Providing 4 services for client {clientType.Name}: {firstArgument.GetType().Name}, {secondArgument.GetType().Name}, {thirdArgument.GetType().Name}, {fourthArgument.GetType().Name}.");
                        #endif

                        setMethod.Invoke(null, setMethodArguments);

                        #if UNITY_EDITOR
                        // In builds ScriptableObjects' Awake method gets called at runtime when a scene or prefab with a component referencing said ScriptableObject gets loaded.
                        // Because of this we can rely on ServiceInjector to execute via the RuntimeInitializeOnLoadMethod attribute before the Awake methods of ScriptableObjects are executed.
                        // In the editor however, ScriptableObject can already get loaded in edit mode, in which case Awake gets executed before service injection has taken place.
                        // For this reason we need to manually initialize these ScriptableObjects.
                        if(uninitializedScriptableObjects.TryGetValue(clientType, out var scriptableObjects))
						{
                            for(int s = scriptableObjects.Count - 1; s >= 0; s--)
		                    {
								ScriptableObject scriptableObject = scriptableObjects[s];
								if(scriptableObject is IInitializableEditorOnly initializableEditorOnly
                                    && initializableEditorOnly.Initializer is IInitializer initializer)
								{
                                    try
                                    {
                                        initializer.InitTarget();
                                    }
                                    catch(Exception ex)
								    {
                                        Debug.LogError(ex);
								    }
                                    scriptableObjects.RemoveAt(s);
                                    continue;
								}

                                try
                                {
                                    var initMethod = typeof(IInitializable<,,,>).MakeGenericType(argumentTypes).GetMethod(nameof(IInitializable<object>.Init));
                                    initMethod.Invoke(scriptableObject, new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument });
                                }
                                catch(Exception ex)
								{
                                    Debug.LogError(ex);
								}
		                    }

                            uninitializedScriptableObjects.Remove(clientType);
						}
                        #endif
                    }
                    return;
                }
            }
        }

        private static async Task TrySetFiveDefaultServices(Type clientType, Type[] interfaceTypes, MethodInfo setMethod, Type[] setMethodArgumentTypes, object[] setMethodArguments
        #if UNITY_EDITOR
        , Dictionary<Type, List<ScriptableObject>> uninitializedScriptableObjects
        #endif
        )
        {
            for(int i = interfaceTypes.Length - 1; i >= 0; i--)
            {
                var interfaceType = interfaceTypes[i];
                if(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IArgs<,,,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();
                    if(services.TryGetValue(argumentTypes[0], out object firstArgument) && services.TryGetValue(argumentTypes[1], out object secondArgument)
                        && services.TryGetValue(argumentTypes[2], out object thirdArgument) && services.TryGetValue(argumentTypes[3], out object fourthArgument)
                         && services.TryGetValue(argumentTypes[4], out object fifthArgument))
                    {
                        Task firstArgumentTask = !argumentTypes[0].IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                        Task secondArgumentTask = !argumentTypes[1].IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                        Task thirdArgumentTask = !argumentTypes[2].IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                        Task fourthArgumentTask = !argumentTypes[3].IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;
                        Task fifthArgumentTask = !argumentTypes[4].IsInstanceOfType(fifthArgument) ? fifthArgument as Task : null;

                        var loadArgumentTasks = Enumerable.Empty<Task>();

                        if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                        if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                        if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                        if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);
                        if(fifthArgumentTask != null) loadArgumentTasks.Append(fifthArgumentTask);

                        await Task.WhenAll(loadArgumentTasks);

                        if(firstArgumentTask != null) firstArgument = await firstArgumentTask.GetResult();
                        if(secondArgumentTask != null) secondArgument = await secondArgumentTask.GetResult();
                        if(thirdArgumentTask != null) thirdArgument = await thirdArgumentTask.GetResult();
                        if(fourthArgumentTask != null) fourthArgument = await fourthArgumentTask.GetResult();
                        if(fifthArgumentTask != null) fifthArgument  = await fifthArgumentTask.GetResult();

                        setMethodArgumentTypes[1] = argumentTypes[0];
                        setMethodArgumentTypes[2] = argumentTypes[1];
                        setMethodArgumentTypes[3] = argumentTypes[2];
                        setMethodArgumentTypes[4] = argumentTypes[3];
                        setMethodArgumentTypes[5] = argumentTypes[4];
                        setMethodArguments[0] = clientType;
                        setMethodArguments[1] = firstArgument;
                        setMethodArguments[2] = secondArgument;
                        setMethodArguments[3] = thirdArgument;
                        setMethodArguments[4] = fourthArgument;
                        setMethodArguments[5] = fifthArgument;
                        setMethodArguments[6] = true;

                        setMethod = setMethod.MakeGenericMethod(argumentTypes);

                        #if DEV_MODE && DEBUG_INIT_SERVICES
                        Debug.Log($"Providing 5 services for client {clientType.Name}: {firstArgument.GetType().Name}, {secondArgument.GetType().Name}, {thirdArgument.GetType().Name}, {fourthArgument.GetType().Name}, {fifthArgument.GetType().Name}.");
                        #endif

                        setMethod.Invoke(null, setMethodArguments);

                        #if UNITY_EDITOR
                        // In builds ScriptableObjects' Awake method gets called at runtime when a scene or prefab with a component referencing said ScriptableObject gets loaded.
                        // Because of this we can rely on ServiceInjector to execute via the RuntimeInitializeOnLoadMethod attribute before the Awake methods of ScriptableObjects are executed.
                        // In the editor however, ScriptableObject can already get loaded in edit mode, in which case Awake gets executed before service injection has taken place.
                        // For this reason we need to manually initialize these ScriptableObjects.
                        if(uninitializedScriptableObjects.TryGetValue(clientType, out var scriptableObjects))
						{
                            for(int s = scriptableObjects.Count - 1; s >= 0; s--)
		                    {
								ScriptableObject scriptableObject = scriptableObjects[s];
								if(scriptableObject is IInitializableEditorOnly initializableEditorOnly
                                    && initializableEditorOnly.Initializer is IInitializer initializer)
								{
                                    try
                                    {
                                        initializer.InitTarget();
                                    }
                                    catch(Exception ex)
								    {
                                        Debug.LogError(ex);
								    }
                                    scriptableObjects.RemoveAt(s);
                                    continue;
								}

                                try
                                {
                                    var initMethod = typeof(IInitializable<,,,,>).MakeGenericType(argumentTypes).GetMethod(nameof(IInitializable<object>.Init));
                                    initMethod.Invoke(scriptableObject, new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument });
                                }
                                catch(Exception ex)
								{
                                    Debug.LogError(ex);
								}
		                    }

                            uninitializedScriptableObjects.Remove(clientType);
						}
                        #endif
                    }
                }
            }
        }

        private static async Task TrySetSixDefaultServices(Type clientType, Type[] interfaceTypes, MethodInfo setMethod, Type[] setMethodArgumentTypes, object[] setMethodArguments
        #if UNITY_EDITOR
        , Dictionary<Type, List<ScriptableObject>> uninitializedScriptableObjects
        #endif
        )
        {
            for(int i = interfaceTypes.Length - 1; i >= 0; i--)
            {
                var interfaceType = interfaceTypes[i];
                if(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IArgs<,,,,,>))
                {
                    var argumentTypes = interfaceType.GetGenericArguments();
                    if(services.TryGetValue(argumentTypes[0], out object firstArgument) && services.TryGetValue(argumentTypes[1], out object secondArgument)
                        && services.TryGetValue(argumentTypes[2], out object thirdArgument) && services.TryGetValue(argumentTypes[3], out object fourthArgument)
                         && services.TryGetValue(argumentTypes[4], out object fifthArgument)&& services.TryGetValue(argumentTypes[4], out object sixthArgument))
                    {
                        Task firstArgumentTask = !argumentTypes[0].IsInstanceOfType(firstArgument) ? firstArgument as Task : null;
                        Task secondArgumentTask = !argumentTypes[1].IsInstanceOfType(secondArgument) ? secondArgument as Task : null;
                        Task thirdArgumentTask = !argumentTypes[2].IsInstanceOfType(thirdArgument) ? thirdArgument as Task : null;
                        Task fourthArgumentTask = !argumentTypes[3].IsInstanceOfType(fourthArgument) ? fourthArgument as Task : null;
                        Task fifthArgumentTask = !argumentTypes[4].IsInstanceOfType(fifthArgument) ? fifthArgument as Task : null;
                        Task sixthArgumentTask = !argumentTypes[5].IsInstanceOfType(sixthArgument) ? sixthArgument as Task : null;

                        var loadArgumentTasks = Enumerable.Empty<Task>();

                        if(firstArgumentTask != null) loadArgumentTasks.Append(firstArgumentTask);
                        if(secondArgumentTask != null) loadArgumentTasks.Append(secondArgumentTask);
                        if(thirdArgumentTask != null) loadArgumentTasks.Append(thirdArgumentTask);
                        if(fourthArgumentTask != null) loadArgumentTasks.Append(fourthArgumentTask);
                        if(fifthArgumentTask != null) loadArgumentTasks.Append(fifthArgumentTask);
                        if(sixthArgumentTask != null) loadArgumentTasks.Append(sixthArgumentTask);

                        await Task.WhenAll(loadArgumentTasks);

                        if(firstArgumentTask != null) firstArgument = await firstArgumentTask.GetResult();
                        if(secondArgumentTask != null) secondArgument = await secondArgumentTask.GetResult();
                        if(thirdArgumentTask != null) thirdArgument = await thirdArgumentTask.GetResult();
                        if(fourthArgumentTask != null) fourthArgument = await fourthArgumentTask.GetResult();
                        if(fifthArgumentTask != null) fifthArgument  = await fifthArgumentTask.GetResult();
                        if(sixthArgumentTask != null) sixthArgument  = await sixthArgumentTask.GetResult();

                        setMethodArgumentTypes[1] = argumentTypes[0];
                        setMethodArgumentTypes[2] = argumentTypes[1];
                        setMethodArgumentTypes[3] = argumentTypes[2];
                        setMethodArgumentTypes[4] = argumentTypes[3];
                        setMethodArgumentTypes[5] = argumentTypes[4];
                        setMethodArgumentTypes[6] = argumentTypes[5];
                        setMethodArguments[0] = clientType;
                        setMethodArguments[1] = firstArgument;
                        setMethodArguments[2] = secondArgument;
                        setMethodArguments[3] = thirdArgument;
                        setMethodArguments[4] = fourthArgument;
                        setMethodArguments[5] = fifthArgument;
                        setMethodArguments[6] = sixthArgument;
                        setMethodArguments[7] = true;

                        setMethod = setMethod.MakeGenericMethod(argumentTypes);

                        #if DEV_MODE && DEBUG_INIT_SERVICES
                        Debug.Log($"Providing 6 services for client {clientType.Name}: {firstArgument.GetType().Name}, {secondArgument.GetType().Name}, {thirdArgument.GetType().Name}, {fourthArgument.GetType().Name}, {fifthArgument.GetType().Name}, {sixthArgument.GetType().Name}.");
                        #endif

                        setMethod.Invoke(null, setMethodArguments);

                        #if UNITY_EDITOR
                        // In builds ScriptableObjects' Awake method gets called at runtime when a scene or prefab with a component referencing said ScriptableObject gets loaded.
                        // Because of this we can rely on ServiceInjector to execute via the RuntimeInitializeOnLoadMethod attribute before the Awake methods of ScriptableObjects are executed.
                        // In the editor however, ScriptableObject can already get loaded in edit mode, in which case Awake gets executed before service injection has taken place.
                        // For this reason we need to manually initialize these ScriptableObjects.
                        if(uninitializedScriptableObjects.TryGetValue(clientType, out var scriptableObjects))
						{
                            for(int s = scriptableObjects.Count - 1; s >= 0; s--)
		                    {
								ScriptableObject scriptableObject = scriptableObjects[s];
								if(scriptableObject is IInitializableEditorOnly initializableEditorOnly
                                    && initializableEditorOnly.Initializer is IInitializer initializer)
								{
                                    try
                                    {
                                        initializer.InitTarget();
                                    }
                                    catch(Exception ex)
								    {
                                        Debug.LogError(ex);
								    }
                                    scriptableObjects.RemoveAt(s);
                                    continue;
								}

                                try
                                {
                                    var initMethod = typeof(IInitializable<,,,,,>).MakeGenericType(argumentTypes).GetMethod(nameof(IInitializable<object>.Init));
                                    initMethod.Invoke(scriptableObject, new object[] { firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument });
                                }
                                catch(Exception ex)
								{
                                    Debug.LogError(ex);
								}
		                    }

                            uninitializedScriptableObjects.Remove(clientType);
						}
                        #endif
                    }
                }
            }
        }

        #if UNITY_EDITOR
        // In builds ScriptableObjects' Awake method gets called at runtime when a scene or prefab with a component
        // referencing said ScriptableObject gets loaded.
        // Because of this we can rely on ServiceInjector to execute via the RuntimeInitializeOnLoadMethod attribute
        // before the Awake methods of ScriptableObjects are executed.
        // In the editor however, ScriptableObject can already get loaded in edit mode, in which case Awake gets
        // executed before service injection has taken place.
        // For this reason we need to manually initialize these ScriptableObjects.
		private static void InitializeAlreadyLoadedScriptableObjectsInTheEditor(Dictionary<Type, List<ScriptableObject>> uninitializedScriptableObjects)
		{
            foreach(var scriptableObjects in uninitializedScriptableObjects.Values)
            {
                foreach(var scriptableObject in scriptableObjects)
                {
                    if(scriptableObject is IInitializableEditorOnly initializableEditorOnly
                    && initializableEditorOnly.Initializer is IInitializer initializer)
	                {
                        initializer.InitTarget();
	                }
                }
            }
		}
        #endif

        private static void SubscribeToUpdateEvents(object service)
		{
			if(service is IUpdate update)
			{
				Updater.Subscribe(update);
			}

			if(service is ILateUpdate lateUpdate)
			{
				Updater.Subscribe(lateUpdate);
			}

			if(service is IFixedUpdate fixedUpdate)
			{
				Updater.Subscribe(fixedUpdate);
			}
		}

		private static void ExecuteAwake(object service)
		{
			if(service is IAwake onEnable)
			{
				onEnable.Awake();
			}
		}

		private static void ExecuteOnEnable(object service)
		{
			if(service is IOnEnable onEnable)
			{
				onEnable.OnEnable();
			}
		}

		private static void ExecuteStartAtEndOfFrame(object service)
		{
			if(service is IStart start)
			{
                Updater.InvokeAtEndOfFrame(start.Start);
			}
		}

		[return: MaybeNull]
		internal static Type GetClassWithServiceAttribute(Type definingType)
            => ServiceAttributeUtility.definingTypes.TryGetValue(definingType, out var serviceInfo)
				? serviceInfo.classWithAttribute
				: null;

		[return: MaybeNull]
        internal static bool TryGetServiceInfo(Type definingType, out ServiceInfo serviceInfo) => ServiceAttributeUtility.definingTypes.TryGetValue(definingType, out serviceInfo);

        private static List<ServiceInfo> GetServiceDefinitions() => ServiceAttributeUtility.concreteTypes.Values.Concat(ServiceAttributeUtility.definingTypes.Values.Where(d => d.concreteType is null)).ToList();

		private static TypeCollection GetImplementingTypes<TInterface>() where TInterface : class
        {
            #if UNITY_EDITOR
			return TypeCache.GetTypesDerivedFrom<TInterface>();
            #else
            return TypeUtility.GetImplementingTypes<TInterface>(typeof(object).Assembly, typeof(InitArgs).Assembly);
            #endif
        }

		public static bool CanProvideService<TService>() => services.ContainsKey(typeof(TService)) || uninitializedServices.ContainsKey(typeof(TService));

		private sealed class ScopedServiceInfo
		{
            public readonly Type definingType;
            public readonly Object service;
            public readonly Clients toClients;

			public ScopedServiceInfo(Type definingType, Object service, Clients toClients)
			{
				this.definingType = definingType;
				this.service = service;
				this.toClients = toClients;
			}
		}
    }
}
#endif