﻿using System.Threading.Tasks;
using UnityEngine;
using static Sisus.Init.Internal.InitializerUtility;

namespace Sisus.Init
{
	/// <summary>
	/// A base class for a component that can specify the six arguments used to initialize an object of type <typeparamref name="TClient"/>.
	/// <para>
	/// The arguments can be assigned using the inspector and are serialized as part of the client's scene or prefab asset.
	/// </para>
	/// <para>
	/// The <typeparamref name="TClient">client</typeparamref> does not need to implement the
	/// <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument}"/> interface.
	/// The initialization arguments can instead be injected, for example, directly into properties with public setters.
	/// </para>
	/// <para>
	/// After the arguments have been injected the initializer is removed from the <see cref="GameObject"/> that holds it.
	/// </para>
	/// </summary>
	/// <typeparam name="TClient"> Type of the initialized client component. </typeparam>
	/// <typeparam name="TFirstArgument"> Type of the first argument to inject to the client. </typeparam>
	/// <typeparam name="TSecondArgument"> Type of the second argument to inject to the client. </typeparam>
	/// <typeparam name="TThirdArgument"> Type of the third argument to inject to the client. </typeparam>
	/// <typeparam name="TFourthArgument"> Type of the fourth argument to inject to the client. </typeparam>
	/// <typeparam name="TFifthArgument"> Type of the fifth argument to inject to the client. </typeparam>
	/// <typeparam name="TSixthArgument"> Type of the sixth argument to inject to the client. </typeparam>
	public abstract class CustomInitializer<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>
		: CustomInitializerBase<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>
			where TClient : Component
	{
		[SerializeField] private Any<TFirstArgument> firstArgument = default;
		[SerializeField] private Any<TSecondArgument> secondArgument = default;
		[SerializeField] private Any<TThirdArgument> thirdArgument = default;
		[SerializeField] private Any<TFourthArgument> fourthArgument = default;
		[SerializeField] private Any<TFifthArgument> fifthArgument = default;
		[SerializeField] private Any<TSixthArgument> sixthArgument = default;

		[SerializeField, HideInInspector] private Arguments disposeArgumentsOnDestroy = Arguments.None;
		[SerializeField, HideInInspector] private Arguments asyncValueProviderArguments = Arguments.None;

		/// <inheritdoc/>
		protected override TFirstArgument FirstArgument { get => firstArgument.GetValue(this, Context.MainThread); set => firstArgument = value; }
		/// <inheritdoc/>
		protected override TSecondArgument SecondArgument { get => secondArgument.GetValue(this, Context.MainThread); set => secondArgument = value; }
		/// <inheritdoc/>
		protected override TThirdArgument ThirdArgument { get => thirdArgument.GetValue(this, Context.MainThread); set => thirdArgument = value; }
		/// <inheritdoc/>
		protected override TFourthArgument FourthArgument { get => fourthArgument.GetValue(this, Context.MainThread); set => fourthArgument = value; }
		/// <inheritdoc/>
		protected override TFifthArgument FifthArgument { get => fifthArgument.GetValue(this, Context.MainThread); set => fifthArgument = value; }
		/// <inheritdoc/>
		protected override TSixthArgument SixthArgument { get => sixthArgument.GetValue(this, Context.MainThread); set => sixthArgument = value; }

		protected override bool IsRemovedAfterTargetInitialized => disposeArgumentsOnDestroy == Arguments.None;
		private protected override bool IsAsync => asyncValueProviderArguments != Arguments.None;

		private protected sealed override async ValueTask<TClient> InitTargetAsync(TClient target)
		{
			var firstArgument = await this.firstArgument.GetValueAsync(this, Context.MainThread);
			var secondArgument = await this.secondArgument.GetValueAsync(this, Context.MainThread);
			var thirdArgument = await this.thirdArgument.GetValueAsync(this, Context.MainThread);
			var fourthArgument = await this.fourthArgument.GetValueAsync(this, Context.MainThread);
			var fifthArgument = await this.fifthArgument.GetValueAsync(this, Context.MainThread);
			var sixthArgument = await this.sixthArgument.GetValueAsync(this, Context.MainThread);

			#if DEBUG
			if(disposeArgumentsOnDestroy != Arguments.None)
			{
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.First)) OptimizeValueProviderNameForDebugging(this, this.firstArgument);
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.Second)) OptimizeValueProviderNameForDebugging(this, this.secondArgument);
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.Third)) OptimizeValueProviderNameForDebugging(this, this.thirdArgument);
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.Fourth)) OptimizeValueProviderNameForDebugging(this, this.fourthArgument);
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.Fifth)) OptimizeValueProviderNameForDebugging(this, this.fifthArgument);
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.Sixth)) OptimizeValueProviderNameForDebugging(this, this.sixthArgument);
			}
			#endif

			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(IsRuntimeNullGuardActive) ValidateArgumentsAtRuntime(firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument);
			#endif

			#if UNITY_EDITOR
			if(target == null)
			#else
			if(target is null)
			#endif
            {
                target = gameObject.AddComponent<TClient>();
            }
			else if(target.gameObject != gameObject)
			{
				target = Instantiate(target);
            }

			InitTarget(target, firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument);
			return target;
		}

		private void OnDestroy()
		{
			if(disposeArgumentsOnDestroy == Arguments.None)
			{
				return;
			}

			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.First, ref firstArgument);
			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.Second, ref secondArgument);
			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.Third, ref thirdArgument);
			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.Fourth, ref fourthArgument);
			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.Fifth, ref fifthArgument);
			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.Sixth, ref sixthArgument);
		}

		#if UNITY_EDITOR
		private protected sealed override void SetReleaseArgumentOnDestroy(Arguments argument, bool shouldRelease)
		{
			var setValue = disposeArgumentsOnDestroy.WithFlag(argument, shouldRelease);
			if(disposeArgumentsOnDestroy != setValue)
			{
				disposeArgumentsOnDestroy = setValue;
				UnityEditor.EditorUtility.SetDirty(this);
			}
		}

		private protected sealed override void SetIsArgumentAsyncValueProvider(Arguments argument, bool isAsyncValueProvider)
		{
			var setValue = asyncValueProviderArguments.WithFlag(argument, isAsyncValueProvider);
			if(asyncValueProviderArguments != setValue)
			{
				asyncValueProviderArguments = setValue;
				UnityEditor.EditorUtility.SetDirty(this);
			}
		}

		private protected override NullGuardResult EvaluateNullGuard() => firstArgument.EvaluateNullGuard(this)
															.Join(secondArgument.EvaluateNullGuard(this))
															.Join(thirdArgument.EvaluateNullGuard(this))
															.Join(fourthArgument.EvaluateNullGuard(this))
															.Join(fifthArgument.EvaluateNullGuard(this))
															.Join(sixthArgument.EvaluateNullGuard(this));

		private protected override void OnValidate() => Validate(this, gameObject, firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument);
		#endif
	}
}