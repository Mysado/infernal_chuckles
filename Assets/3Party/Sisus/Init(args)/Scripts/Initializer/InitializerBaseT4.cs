﻿#pragma warning disable CS0414

using System.Diagnostics.CodeAnalysis;
using Sisus.Init.Internal;
using UnityEngine;
using static Sisus.Init.Internal.InitializerUtility;
#if UNITY_EDITOR
using Sisus.Init.EditorOnly;
#endif

namespace Sisus.Init
{
	/// <summary>
	/// A base class for a component that can specify the four arguments used to
	/// initialize an object that implements
	/// <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}"/>.
	/// <para>
	/// The arguments can be assigned using the inspector and are serialized as part of the client's scene or prefab asset.
	/// </para>
	/// <para>
	/// The arguments get injected to the <typeparamref name="TClient">client</typeparamref>
	/// during the <see cref="Awake"/> event.
	/// </para>
	/// <para>
	/// The client receives the arguments via the
	/// <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument}.Init">Init</see>
	/// method where it can assigned them to member fields or properties.
	/// </para>
	/// <para>
	/// After the arguments have been injected the initializer is removed from the <see cref="GameObject"/> that holds it.
	/// </para>
	/// <para>
	/// When you derive your Initializer class from <see cref="InitializerBase{,,,,}"/>
	/// you are responsible for implementing the argument properties and serializing their values.
	/// This means you will need to write a little bit more code, but it also grants you more options
	/// in how to handle the serialization, making it possible to support types that Unity can't serialize
	/// automatically. If you derive from <see cref="Initializer{,,,,}"/> instead, then these things will be handled for you.
	/// </para>
	/// </summary>
	/// <typeparam name="TClient"> Type of the initialized client component. </typeparam>
	/// <typeparam name="TFirstArgument"> Type of the first argument to pass to the client component's Init function. </typeparam>
	/// <typeparam name="TSecondArgument"> Type of the second argument to pass to the client component's Init function. </typeparam>
	/// <typeparam name="TThirdArgument"> Type of the third argument to pass to the client component's Init function. </typeparam>
	/// <typeparam name="TFourthArgument"> Type of the fourth argument to pass to the client component's Init function. </typeparam>
	public abstract class InitializerBase<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>
		: InitializerBaseInternal<TClient>, IInitializer<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>, IInitializable
		#if UNITY_EDITOR
		, IInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>
		#endif
		where TClient : MonoBehaviour, IInitializable<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>
	{
		/// <summary>
		/// The first argument passed to the <typeparamref name="TClient">client</typeparamref>'s Init function.
		/// </summary>
		protected abstract TFirstArgument FirstArgument { get; set; }

		/// <summary>
		/// The second argument passed to the <typeparamref name="TClient">client</typeparamref>'s Init function.
		/// </summary>
		protected abstract TSecondArgument SecondArgument { get; set; }

		/// <summary>
		/// The third argument passed to the <typeparamref name="TClient">client</typeparamref>'s Init function.
		/// </summary>
		protected abstract TThirdArgument ThirdArgument { get; set; }

		/// <summary>
		/// The fourth argument passed to the <typeparamref name="TClient">client</typeparamref>'s Init function.
		/// </summary>
		protected abstract TFourthArgument FourthArgument { get; set; }

		/// <inheritdoc/>
		[return: NotNull]
		private protected override TClient InitTarget([AllowNull] TClient target)
        {
			var firstArgument = FirstArgument;
			var secondArgument = SecondArgument;
			var thirdArgument = ThirdArgument;
			var fourthArgument = FourthArgument;

			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(IsRuntimeNullGuardActive) ValidateArgumentsAtRuntime(firstArgument, secondArgument, thirdArgument, fourthArgument);
			#endif

            #if UNITY_EDITOR
			if(target == null)
			#else
			if(target is null)
			#endif
            {
				gameObject.AddComponent(out TClient result, firstArgument, secondArgument, thirdArgument, fourthArgument);
                return result;
            }

			if(target.gameObject != gameObject)
			{
				return target.Instantiate(firstArgument, secondArgument, thirdArgument, fourthArgument);
            }

			if(target is MonoBehaviour<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument> monoBehaviourT)
			{
				monoBehaviourT.InitInternal(firstArgument, secondArgument, thirdArgument, fourthArgument);
			}
			else
			{
				target.Init(firstArgument, secondArgument, thirdArgument, fourthArgument);
			}

			return target;
        }

		bool IInitializable.HasInitializer => false;

		bool IInitializable.Init(Context context)
		{
			#if UNITY_EDITOR
			if(context is Context.EditMode)
			{
				AutoInitInEditMode<InitializerBase<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>, TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>(this);
			}
			#endif

			InitTarget();
			return true;
		}

		/// <summary>
		/// Resets the Init arguments to their default values.
		/// <para>
		/// <see cref="OnReset"/> is called when the user hits the Reset button in the Inspector's
		/// context menu or when adding the component to a GameObject the first time.
		/// <para>
		/// This function is only called in the editor in edit mode.
		/// </summary>
		/// <param name="firstArgument"> The first argument to reset. </param>
		/// <param name="secondArgument"> The second argument to reset. </param>
		/// <param name="thirdArgument"> The third argument to reset. </param>
		/// <param name="fourthArgument"> The fourth argument to reset. </param>
		protected virtual void OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument, ref TThirdArgument thirdArgument, ref TFourthArgument fourthArgument) { }

		#if DEBUG || INIT_ARGS_SAFE_MODE
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		private protected void ValidateArgumentsAtRuntime(TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument)
		{
			ThrowIfMissing(firstArgument); ThrowIfMissing(secondArgument); ThrowIfMissing(thirdArgument); ThrowIfMissing(fourthArgument);
		}
		#endif

		#if UNITY_EDITOR
		private protected override NullGuardResult EvaluateNullGuard() => IsNull(FirstArgument) || IsNull(SecondArgument) || IsNull(ThirdArgument) || IsNull(FourthArgument) ? NullGuardResult.ValueMissing : NullGuardResult.Passed;
		TFirstArgument IInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>.FirstArgument { get => FirstArgument; set => FirstArgument = value; }
		TSecondArgument IInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>.SecondArgument { get => SecondArgument; set => SecondArgument = value; }
		TThirdArgument IInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>.ThirdArgument { get => ThirdArgument; set => ThirdArgument = value; }
		TFourthArgument IInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>.FourthArgument { get => FourthArgument; set => FourthArgument = value; }
		void IInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>.OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument, ref TThirdArgument thirdArgument, ref TFourthArgument fourthArgument) => OnReset(ref firstArgument, ref secondArgument, ref thirdArgument, ref fourthArgument);
		private protected sealed override void Reset() => Reset<InitializerBase<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>, TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument>(this, gameObject);
		private protected override void OnValidate() => Validate(this, gameObject, FirstArgument, SecondArgument, ThirdArgument, FourthArgument);
		#endif
    }
}