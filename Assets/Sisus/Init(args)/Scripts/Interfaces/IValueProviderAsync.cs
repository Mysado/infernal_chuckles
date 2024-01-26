using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sisus.Init
{
	/// <summary>
	/// Represents an object that can asynchronously retrieve a value of type <typeparamref name="TValue"/> for a client <see cref="Component"/>.
	/// <para>
	/// If a class derives from <see cref="Object"/> and implements <see cref="IValueProviderAsync{T}"/> then
	/// <see cref="Any{T}"/> can wrap an instance of this class and return its value when <see cref="Any{T}.GetValueAsync"/> is called.
	/// </para>
	/// </summary>
	/// <typeparam name="TValue"> Type of the provided value. </typeparam>
	/// <seealso cref="IValueProvider{TValue}"/>
	/// <seealso cref="IValueByTypeProvider"/>
	/// <seealso cref="IValueByTypeProviderAsync"/>
	[RequireImplementors]
	public interface IValueProviderAsync<TValue> : IValueProviderAsync
	{
		/// <summary>
		/// Gets the value of type <typeparamref name="TValue"/> for the <paramref name="client"/>.
		/// </summary>
		/// <param name="client">
		/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <returns>
		/// Value of type <typeparamref name="TValue"/>, if available;
		/// otherwise, the default value of <typeparamref name="TValue"/>.
		/// </returns>
		new ValueTask<TValue> GetForAsync([AllowNull] Component client);
	}

	/// <summary>
	/// Represents an object that can asynchronously retrieve a value for a client <see cref="Component"/>.
	/// <para>
	/// <para>
	/// Base interface of <see cref="IValueProvider{TValue}"/>.
	/// </para>
	/// </summary>
	[RequireImplementors]
	public interface IValueProviderAsync
	{
		/// <summary>
		/// Gets the value of type <typeparamref name="TValue"/> for the <paramref name="client"/>.
		/// </summary>
		/// <param name="client">
		/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <returns>
		/// An awaitable task for retriving the value for the <paramref name="client"/>, if available;
		/// otherwise, a completed task with the result of <see langword="null"/>.
		/// </returns>
		ValueTask<object> GetForAsync([AllowNull] Component client);
	}
}
