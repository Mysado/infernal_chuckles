﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sisus.Init.Internal
{
    public static class TypeUtility
    {
		private static readonly HashSet<Type> baseTypes = new HashSet<Type>()
		{
			typeof(object), typeof(Object),
			typeof(Component), typeof(Behaviour), typeof(MonoBehaviour),
			#if UNITY_UGUI
			typeof(UnityEngine.EventSystems.UIBehaviour),
			#endif
			typeof(ScriptableObject)
		};

		private static readonly HashSet<Type> genericBaseTypes = new HashSet<Type>()
		{
			typeof(MonoBehaviour<>), typeof(MonoBehaviour<,>), typeof(MonoBehaviour<,>),
			typeof(MonoBehaviour<,,>), typeof(MonoBehaviour<,,,>), typeof(MonoBehaviour<,,,,>),
			typeof(ConstructorBehaviour<>), typeof(ConstructorBehaviour<,>),typeof(ConstructorBehaviour<,>),
			typeof(ConstructorBehaviour<,,>), typeof(ConstructorBehaviour<,,,>), typeof(ConstructorBehaviour<,,,,>)
		};

		private static readonly Dictionary<char, Dictionary<Type, string>> toStringCache = new Dictionary<char, Dictionary<Type, string>>(3)
		{
			{ '\0', new Dictionary<Type, string>(4096) {
				{ typeof(Serialization._Integer), "Integer" }, { typeof(int), "Integer" }, { typeof(uint), "UInteger" },
				{ typeof(Serialization._Float), "Float" }, { typeof(float), "Float" },
				{ typeof(Serialization._Double), "Double" }, { typeof(double), "Double" },
				{ typeof(Serialization._Boolean), "Boolean" }, { typeof(bool), "Boolean" },
				{ typeof(Serialization._String), "String" }, { typeof(string), "String" },
				{ typeof(short), "Short" }, { typeof(ushort), "UShort" },
				{ typeof(byte), "Byte" },{ typeof(sbyte), "SByte" },
				{ typeof(long), "Long" }, { typeof(ulong), "ULong" },
				{ typeof(object), "object" },
				{ typeof(decimal), "Decimal" }, { typeof(Serialization._Type), "Type" }
			} },
			{ '/', new Dictionary<Type, string>(4096) {
				{ typeof(Serialization._Integer), "Integer" }, { typeof(int), "Integer" }, { typeof(uint), "UInteger" },
				{ typeof(Serialization._Float), "Float" }, { typeof(float), "Float" },
				{ typeof(Serialization._Double), "Double" }, { typeof(double), "Double" },
				{ typeof(Serialization._Boolean), "Boolean" }, { typeof(bool), "Boolean" },
				{ typeof(Serialization._String), "String" }, { typeof(string), "String" },
				{ typeof(short), "Short" }, { typeof(ushort), "UShort" },
				{ typeof(byte), "Byte" },{ typeof(sbyte), "SByte" },
				{ typeof(long), "Long" }, { typeof(ulong), "ULong" },
				{ typeof(object), "System/Object" },
				{ typeof(decimal), "Decimal" }, { typeof(Serialization._Type), "System/Type" }
			} },
			{ '.', new Dictionary<Type, string>(4096) {
				{ typeof(Serialization._Integer), "Integer" }, { typeof(int), "Integer" }, { typeof(uint), "UInteger" },
				{ typeof(Serialization._Float), "Float" }, { typeof(float), "Float" },
				{ typeof(Serialization._Double), "Double" }, { typeof(double), "Double" },
				{ typeof(Serialization._Boolean), "Boolean" }, { typeof(bool), "Boolean" },
				{ typeof(Serialization._String), "String" }, { typeof(string), "String" },
				{ typeof(short), "Short" }, { typeof(ushort), "UShort" },
				{ typeof(byte), "Byte" },{ typeof(sbyte), "SByte" },
				{ typeof(long), "Long" }, { typeof(ulong), "ULong" },
				{ typeof(object), "System.Object" },
				{ typeof(decimal), "Decimal" }, { typeof(Serialization._Type), "System.Type" }
			} }
		};

		[return: NotNull]
		public static IEnumerable<(Type classWithAttribute, TAttribute attribute)> GetTypesWithAttribute<TAttribute>() where TAttribute : Attribute
		{
			#if UNITY_EDITOR
			foreach(var type in UnityEditor.TypeCache.GetTypesWithAttribute<TAttribute>())
			{
				foreach(var attribute in type.GetCustomAttributes<TAttribute>())
				{
					yield return (type, attribute);
				}
			}
			#else
			foreach(var type in GetAllTypesThreadSafe(typeof(TAttribute).Assembly, null))
			{
				foreach(var attribute in type.GetCustomAttributes<TAttribute>())
				{
					yield return (type, attribute);
				}
			}
			#endif
		}

		[return: NotNull]
		internal static IEnumerable<(Type classWithAttribute, TAttribute[] attributes)> GetTypesWithAttribute<TAttribute>([DisallowNull] Assembly mustReferenceAssembly, [AllowNull] Assembly ignoreAssembly) where TAttribute : Attribute
		{
			#if UNITY_EDITOR
			foreach(var type in UnityEditor.TypeCache.GetTypesWithAttribute<TAttribute>())
			{
				yield return (type, type.GetCustomAttributes<TAttribute>().ToArray());
			}
			#else
			foreach(var type in GetAllTypesThreadSafe(mustReferenceAssembly, ignoreAssembly))
			{
				var attributes = type.GetCustomAttributes<TAttribute>();
				if(attributes.Any())
				{
					yield return (type, attributes.ToArray());
				}
			}
			#endif
		}

		#if UNITY_EDITOR
		public static UnityEditor.TypeCache.TypeCollection GetDerivedTypes<T>()
		#else
		[return: NotNull]
		internal static IEnumerable<Type> GetDerivedTypes<T>()
		#endif
        {
			#if UNITY_EDITOR
			return UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(T));
			#else
			foreach(var type in GetAllTypesThreadSafe(typeof(T).Assembly, null))
			{
				if(typeof(T).IsAssignableFrom(type))
				{
					yield return type;
				}
			}
			#endif
        }

		[return: NotNull]
		internal static IEnumerable<Type> GetDerivedTypes([DisallowNull] Type inheritedType, [DisallowNull] Assembly mustReferenceAssembly, [AllowNull] Assembly ignoreAssembly)
        {
			foreach(var type in GetAllTypesThreadSafe(mustReferenceAssembly, ignoreAssembly))
			{
				if(inheritedType.IsAssignableFrom(type))
				{
					yield return type;
				}
			}
        }

		[return: NotNull]
		internal static IEnumerable<Type> GetImplementingTypes<TInterface>([DisallowNull] Assembly mustReferenceAssembly, [AllowNull] Assembly ignoreAssembly) where TInterface : class
        {
			foreach(var type in GetAllTypesThreadSafe(mustReferenceAssembly, ignoreAssembly))
			{
				if(typeof(TInterface).IsAssignableFrom(type) && !type.IsInterface)
				{
					yield return type;
				}
			}
        }

		[return: NotNull]
		internal static IEnumerable<Type> GetImplementingTypes(Type interfaceType, [DisallowNull] Assembly mustReferenceAssembly, [AllowNull] Assembly ignoreAssembly)
        {
			foreach(var type in GetAllTypesThreadSafe(mustReferenceAssembly, ignoreAssembly))
			{
				if(interfaceType.IsAssignableFrom(type) && !type.IsInterface)
				{
					yield return type;
				}
			}
        }

		[return: NotNull]
		internal static IEnumerable<Type> GetAllTypesThreadSafe(Assembly mustReferenceAssembly, Assembly ignoreAssembly)
		{
            foreach(var assembly in GetAllAssembliesThreadSafe(mustReferenceAssembly, ignoreAssembly))
            {
				var types = assembly.GetLoadableTypes(true);
                for(int i = types.Length - 1; i >= 0; i--)
                {
					yield return types[i];
                }
            }
		}

		[return: NotNull]
		public static IEnumerable<Type> GetAllTypesThreadSafe()
		{
            foreach(var assembly in GetAllAssembliesThreadSafe())
            {
				var types = assembly.GetLoadableTypes(true);
                for(int i = types.Length - 1; i >= 0; i--)
                {
					yield return types[i];
                }
            }
		}

		[return: NotNull]
		private static Type[] GetLoadableTypes([DisallowNull] this Assembly assembly, bool exportedOnly)
		{
			try
			{
				Type[] result;
				if(exportedOnly)
				{
					result = assembly.GetExportedTypes();
				}
				else
				{
					result = assembly.GetTypes();
				}

				return result;
			}
			catch(NotSupportedException) //thrown if GetExportedTypes is called for a dynamic assembly
			{
				#if DEV_MODE
				UnityEngine.Debug.LogWarning(assembly.GetName().Name + ".GetLoadableTypes() NotSupportedException\n" + assembly.FullName);
				#endif
				return Type.EmptyTypes;
			}
			catch(ReflectionTypeLoadException e)
			{
				var TypesList = new List<Type>(100);

				var exceptionTypes = e.Types;
				int count = exceptionTypes.Length;
				for(int n = count - 1; n >= 0; n--)
				{
					var type = exceptionTypes[n];
					if(type != null)
					{
						TypesList.Add(type);
					}
				}

				#if DEV_MODE
				UnityEngine.Debug.LogWarning(assembly.GetName().Name + ".GetLoadableTypes() ReflectionTypeLoadException, salvaged: " + TypesList.Count + "\n" + assembly.FullName);
				#endif

				return TypesList.ToArray();
			}
			#if DEV_MODE
			catch(Exception e)
			{
				UnityEngine.Debug.LogWarning(assembly.GetName().Name + ".GetLoadableTypes() " + e + "\n" + assembly.FullName);
			#else
			catch(Exception)
			{
			#endif
				return Type.EmptyTypes;
			}
		}

		public static IEnumerable<Assembly> GetAllAssembliesThreadSafe([DisallowNull] Assembly mustReferenceAssembly, [AllowNull] Assembly ignoreAssembly)
		{
			var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

			string mustReferenceName = mustReferenceAssembly.GetName().Name;

			for(int n = allAssemblies.Length - 1; n >= 0; n--)
			{
				var assembly = allAssemblies[n];

				// skip dynamic assemblies to prevent NotSupportedException from being thrown when calling GetTypes
				if(assembly.IsDynamic)
				{
					continue;
				}

				if(assembly == ignoreAssembly)
				{
					continue;
				}

				if(assembly == mustReferenceAssembly)
                {
					yield return assembly;
					continue;
                }

				var referencedAssemblies = assembly.GetReferencedAssemblies();
                for(int r = referencedAssemblies.Length - 1; r >= 0; r--)
                {
					if(string.Equals(referencedAssemblies[r].Name, mustReferenceName))
                    {
						yield return assembly;
						break;
					}
                }
			}
		}

		public static IEnumerable<Assembly> GetAllAssembliesThreadSafe()
		{
			var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

			for(int n = allAssemblies.Length - 1; n >= 0; n--)
			{
				var assembly = allAssemblies[n];

				// skip dynamic assemblies to prevent NotSupportedException from being thrown when calling GetTypes
				if(assembly.IsDynamic)
				{
					continue;
				}

				yield return assembly;
			}
		}

		public static string ToString([AllowNull] Type type, char namespaceDelimiter = '\0')
		{
			return type is null ? "Null" : ToString(type, namespaceDelimiter, toStringCache);
		}

		internal static string ToString([DisallowNull] Type type, char namespaceDelimiter, Dictionary<char, Dictionary<Type, string>> cache)
        {
			if(cache[namespaceDelimiter].TryGetValue(type, out string cached))
            {
				return cached;
            }

			var builder = new StringBuilder();
			ToString(type, builder, namespaceDelimiter, cache);
			string result = builder.ToString();
			cache[namespaceDelimiter][type] = result;
			return result;
        }

		internal static bool IsSerializableByUnity(Type type) => type.IsSerializable || (type.Namespace is string namespaceName && namespaceName.Contains("Unity"));

		private static void ToString([DisallowNull] Type type, [DisallowNull] StringBuilder builder, char namespaceDelimiter, Dictionary<char, Dictionary<Type, string>> cache, Type[] genericTypeArguments = null)
		{
			// E.g. List<T> generic parameter is T, in which case we just want to append "T".
			if(type.IsGenericParameter)
			{
				builder.Append(type.Name);
				return;
			}

			if(type.IsArray)
			{
				builder.Append(ToString(type.GetElementType(), namespaceDelimiter, cache));
				int rank = type.GetArrayRank();
				switch(rank)
				{
					case 1:
						builder.Append("[]");
						break;
					case 2:
						builder.Append("[,]");
						break;
					case 3:
						builder.Append("[,,]");
						break;
					default:
						builder.Append('[');
						for(int n = 1; n < rank; n++)
						{
							builder.Append(',');
						}
						builder.Append(']');
						break;
				}
				return;
			}

			if(namespaceDelimiter != '\0' && type.Namespace != null)
			{
				var namespacePart = type.Namespace;
				if(namespaceDelimiter != '.')
				{
					namespacePart = namespacePart.Replace('.', namespaceDelimiter);
				}

				builder.Append(namespacePart);
				builder.Append(namespaceDelimiter);
			}

			#if CSHARP_7_3_OR_NEWER
			// You can create instances of a constructed generic type.
			// E.g. Dictionary<int, string> instead of Dictionary<TKey, TValue>.
			if(type.IsConstructedGenericType)
			{
				genericTypeArguments = type.GenericTypeArguments;
			}
			#endif

			// If this is a nested class type then append containing type(s) before continuing.
			var containingClassType = type.DeclaringType;
			if(containingClassType != null && type != containingClassType)
			{
				// GenericTypeArguments can't be fetched from the containing class type
				// so need to pass them along to the ToString method and use them instead of
				// the results of GetGenericArguments.
				ToString(containingClassType, builder, '\0', cache, genericTypeArguments);
				builder.Append('.');
			}
			
			if(!type.IsGenericType)
			{
				builder.Append(type.Name);
				return;
			}

			var nullableUnderlyingType = Nullable.GetUnderlyingType(type);
			if(nullableUnderlyingType != null)
			{
				// "Int?" instead of "Nullable<Int>"
				builder.Append(ToString(nullableUnderlyingType, '\0', cache));
				builder.Append("?");
				return;
			}
			
			var name = type.Name;

			// If type name doesn't end with `1, `2 etc. then it's not a generic class type
			// but type of a non-generic class nested inside a generic class.
			if(name[name.Length - 2] == '`')
			{
				// E.g. TKey, TValue
				var genericTypes = type.GetGenericArguments();

				builder.Append(name.Substring(0, name.Length - 2));
				builder.Append('<');

				// Prefer using results of GenericTypeArguments over results of GetGenericArguments if available.
				int genericTypeArgumentsLength = genericTypeArguments != null ? genericTypeArguments.Length : 0;
				if(genericTypeArgumentsLength > 0)
				{
					builder.Append(ToString(genericTypeArguments[0], '\0', cache));
				}
				else
				{
					builder.Append(ToString(genericTypes[0], '\0', cache));
				}

				for(int n = 1, count = genericTypes.Length; n < count; n++)
				{
					builder.Append(", ");

					if(genericTypeArgumentsLength > n)
					{
						builder.Append(ToString(genericTypeArguments[n], '\0', cache));
					}
					else
					{
						builder.Append(ToString(genericTypes[n], '\0', cache));
					}
				}

				builder.Append('>');
			}
			else
			{
				builder.Append(name);
			}
		}

		public static bool IsNullOrBaseType([AllowNull] Type type) => type is null || IsBaseType(type);

		public static bool IsBaseType([DisallowNull] Type type) => type.IsGenericType switch
        {
			true when type.IsGenericTypeDefinition => genericBaseTypes.Contains(type),
			true => genericBaseTypes.Contains(type.GetGenericTypeDefinition()),
			_ => baseTypes.Contains(type)
		};

		public static bool DerivesFromGenericBaseType([DisallowNull] Type type)
		{
			while((type = type.BaseType) != null)
			{
				// simply using namespace comparison instead of checking if the type is found in genericBaseTypes,
				// so that base types found inside optional add-on packages will also be detected properly
				// (e.g. SerializedMonoBehaviour<T...> and SerializedScriptableObject<T...>).
				if(type.IsGenericType && type.Namespace == typeof(MonoBehaviour<object>).Namespace)
				{
					return true;
				}
			}

			return false;
		}

		public static bool TryGetGenericBaseType([DisallowNull] Type type, out Type baseType)
		{
			while((type = type.BaseType) != null)
			{
				// simply using namespace comparison instead of checking if the type is found in genericBaseTypes,
				// so that base types found inside optional add-on packages will also be detected properly
				// (e.g. SerializedMonoBehaviour<T...> and SerializedScriptableObject<T...>).
				if(type.IsGenericType && type.Namespace == typeof(MonoBehaviour<object>).Namespace)
				{
					baseType = type;
					return true;
				}
			}

			baseType = null;
			return false;
		}

		public static TCollection ConvertToCollection<TCollection, TElement>(TElement[] source)
		{
			if(source is TCollection result)
			{
				return result;
			}

			if(typeof(TCollection).IsArray)
			{
				var elementType = typeof(TCollection).GetElementType();
				if(elementType == typeof(TElement))
				{
					return (TCollection)(object)source;
				}

				int count = source.Length;
				var array = Array.CreateInstance(elementType, count);
				Array.Copy(source, array, count);
				return (TCollection)(object)array;
			}

			if(typeof(TCollection).IsAbstract)
			{
				if(typeof(TCollection).IsGenericType)
				{
					var typeDefinition = typeof(TCollection).GetGenericTypeDefinition();
					if(typeDefinition == typeof(IEnumerable<>) || typeDefinition == typeof(IReadOnlyList<>) || typeDefinition == typeof(IList<>))
					{
						int count = source.Length;
						var elementType = GetCollectionElementType(typeof(TCollection));
						var array = Array.CreateInstance(elementType, count);
						Array.Copy(source, array, count);
						return (TCollection)(object)array;
					}
				}
			}

			try
			{
				return To<TCollection>.Convert(source);
			}
			catch
			{
				if(!typeof(IEnumerable).IsAssignableFrom(typeof(TCollection)))
				{
					throw new InvalidCastException($"Unable to convert from {ToString(typeof(TElement))}[] to {ToString(typeof(TCollection))}.\n{ToString(typeof(TCollection))} does not seem to be a collection type.");
				}

				throw new InvalidCastException($"Unable to convert from {ToString(typeof(TElement))}[] to {ToString(typeof(TCollection))}.\n{ToString(typeof(TCollection))} must have a public constructor with an IEnumerable<{typeof(TElement).Name}> parameter.");
			}
		}

		public static Type GetCollectionElementType(Type collectionType)
        {
			if(collectionType.IsArray)
			{
				return collectionType.GetElementType();
			}

			if(collectionType.IsGenericType)
			{
				Type typeDefinition = collectionType.GetGenericTypeDefinition();
				if(typeDefinition == typeof(List<>)
				|| typeDefinition == typeof(IEnumerable<>)
				|| typeDefinition == typeof(IList<>)
				|| typeDefinition == typeof(ICollection<>)
				|| typeDefinition == typeof(IReadOnlyCollection<>)
				|| typeDefinition == typeof(IReadOnlyList<>)
				|| typeDefinition == typeof(HashSet<>)
				|| typeDefinition == typeof(Queue<>)
				|| typeDefinition == typeof(Stack<>)
				|| typeDefinition == typeof(NativeArray<>)
				|| typeDefinition == typeof(ReadOnlyCollection<>)
				)
				{
					return collectionType.GetGenericArguments()[0];
				}

				if(typeDefinition == typeof(Dictionary<,>) || typeDefinition == typeof(IDictionary<,>))
				{
					return typeof(KeyValuePair<,>).MakeGenericType(collectionType.GetGenericArguments());
				}
			}

			foreach(var interfaceType in collectionType.GetInterfaces())
            {
				if(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
					return interfaceType.GetGenericArguments()[0];
                }
            }

			return null;
		}

		public static bool IsCommonCollectionType(Type collectionType)
		{
			if(collectionType.IsArray)
			{
				return true;
			}

			if(!collectionType.IsGenericType)
			{
				return false;
			}

			Type typeDefinition = collectionType.GetGenericTypeDefinition();

			return typeDefinition == typeof(List<>)
				|| typeDefinition == typeof(IEnumerable<>)
				|| typeDefinition == typeof(IList<>)
				|| typeDefinition == typeof(IReadOnlyList<>)
				|| typeDefinition == typeof(ICollection<>)
				|| typeDefinition == typeof(IReadOnlyCollection<>)
				|| typeDefinition == typeof(HashSet<>)
				|| typeDefinition == typeof(Queue<>)
				|| typeDefinition == typeof(Stack<>)
				|| typeDefinition == typeof(NativeArray<>)
				|| typeDefinition == typeof(ReadOnlyCollection<>)
				|| typeDefinition == typeof(Dictionary<,>)
				|| typeDefinition == typeof(IDictionary<,>);
		}

		private static class To<TCollection>
		{
			private static readonly ConstructorInfo constructor;
			private static readonly object[] arguments = new object[1];
			
			static To()
			{
				var argumentType = GetCollectionElementType(typeof(TCollection));
				var parameterTypeGeneric = typeof(IEnumerable<>);
				var parameterType = parameterTypeGeneric.MakeGenericType(argumentType);
				var collectionType = !typeof(TCollection).IsAbstract ? typeof(TCollection): typeof(List<>).MakeGenericType(argumentType);
				constructor = collectionType.GetConstructor(new Type[] { parameterType });
			}

			public static TCollection Convert(object sourceArray)
			{
				arguments[0] = sourceArray;
				return (TCollection)constructor.Invoke(arguments);
			}
		}
    }
}
