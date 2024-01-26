﻿using System.Threading.Tasks;
using UnityEngine;
using static Sisus.Init.Internal.InitializerUtility;
using static Sisus.Init.ValueProviders.ValueProviderUtility;

namespace Sisus.Init
{
	/// <summary>
	/// A base class for a component that can be used to specify the argument used to
	/// initialize a component that implements <see cref="IInitializable{TArgument}"/>.
	/// <para>
	/// The argument can be assigned using the inspector and is serialized as part of the client's scene or prefab asset.
	/// </para>
	/// <para>
	/// The argument gets injected to the <typeparamref name="TClient">client</typeparamref>
	/// during the <see cref="Awake"/> event.
	/// </para>
	/// <para>
	/// The client receives the argument via the <see cref="IInitializable{TArgument}.Init">Init</see>
	/// method where it can be assigned to a member field or property.
	/// </para>
	/// <para>
	/// After the argument has been injected the <see cref="Initializer{,}"/> is removed from the
	/// <see cref="GameObject"/> that holds it.
	/// </para>
	/// </summary>
	/// <typeparam name="TClient"> Type of the initialized client component. </typeparam>
	/// <typeparam name="TArgument"> Type of the argument to pass to the client component's Init function. </typeparam>
	public abstract class Initializer<TClient, TArgument> : InitializerBase<TClient, TArgument> where TClient : MonoBehaviour, IInitializable<TArgument>
	{
		[SerializeField] private Any<TArgument> argument = default;

		[SerializeField, HideInInspector] private Arguments disposeArgumentsOnDestroy = Arguments.None;
		[SerializeField, HideInInspector] private Arguments asyncValueProviderArguments = Arguments.None;

		protected override TArgument Argument { get => argument.GetValue(this, Context.MainThread); set => argument = value; }

		protected override bool IsRemovedAfterTargetInitialized => disposeArgumentsOnDestroy == Arguments.None;
		private protected override bool IsAsync => asyncValueProviderArguments != Arguments.None;
		
		private protected sealed override async ValueTask<TClient> InitTargetAsync(TClient target)
		{
			var argument = await this.argument.GetValueAsync(this, Context.MainThread);

			#if DEBUG
			if(disposeArgumentsOnDestroy == Arguments.First) OptimizeValueProviderNameForDebugging(this, this.argument);
			#endif

			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(IsRuntimeNullGuardActive) ValidateArgumentAtRuntime(argument);
			#endif

            #if UNITY_EDITOR
			if(target == null)
			#else
			if(target is null)
			#endif
            {
				gameObject.AddComponent(out target, argument);
                return target;
            }

			if(target.gameObject != gameObject)
			{
				return target.Instantiate(argument);
            }

			if(target is MonoBehaviour<TArgument> monoBehaviourT)
			{
				monoBehaviourT.InitInternal(argument);
			}
			else
			{
				target.Init(argument);
			}

			return target;
		}

		private void OnDestroy()
		{
			if(disposeArgumentsOnDestroy == Arguments.First)
			{
				HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.First, ref argument);
			}
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

		private protected override NullGuardResult EvaluateNullGuard() => argument.EvaluateNullGuard(this);
		private protected override void OnValidate() => Validate(this, gameObject, argument);
		#endif
	}
}