namespace Sisus.Init
{
	/// <summary>
	/// Specifies the different possible states that <see cref="Any{T}"/> or an initializer have
	/// in terms of having one or more missing initialization arguments for its clients.
	/// </summary>
	public enum NullGuardResult
	{
		/// <summary>
		/// No arguments are null.
		/// </summary>
		Passed,

		/// <summary>
		/// One or more arguments are null.
		/// </summary>
		ValueMissing,

		/// <summary>
		/// One or more value providers have invalid state that needs to be fixed, or they
		/// will not be able to provide a value at runtime.
		/// </summary>
		InvalidValueProviderState,

		/// <summary>
		/// No arguments are null, but one or more arguments are a value provider which will
		/// not be able to provide a value at runtime.
		/// </summary>
		ValueProviderValueMissing,

		/// <summary>
		/// An exception was encountered while trying to retrieve one or more arguments.
		/// </summary>
		ExceptionOccurred,

		/// <summary>
		/// No arguments are null, but one or more arguments are a value provider which has
		/// a null return value at this time. They might still be able to provide a value
		/// at runtime.
		/// </summary>
		ValueProviderValueNullInEditMode,

		/// <summary>
		/// The value provider does not offer services to the initializer
		/// </summary>
		ClientNotSupported,

		/// <summary>
		/// The value provider does not support providing a service matching the argument type.
		/// </summary>
		TypeNotSupported
	}

	public static class NullGuardResultExtensions
	{
		public static NullGuardResult Join(this NullGuardResult previous, NullGuardResult next) => previous switch
		{
			NullGuardResult.Passed => next,
			NullGuardResult.ValueProviderValueNullInEditMode => next is NullGuardResult.Passed ? previous : next,
			NullGuardResult.ValueProviderValueMissing => next is NullGuardResult.Passed or NullGuardResult.ValueProviderValueNullInEditMode ? previous : next,
			_ => previous
		};
	}
}