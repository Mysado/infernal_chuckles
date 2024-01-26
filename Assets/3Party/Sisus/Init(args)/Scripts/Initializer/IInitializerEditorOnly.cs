#if UNITY_EDITOR
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("InitArgs.Editor")]
[assembly: InternalsVisibleTo("Tests.EditMode")]
[assembly: InternalsVisibleTo("Tests.PlayMode")]

namespace Sisus.Init.EditorOnly
{
	internal interface IInitializerEditorOnly : IInitializer
	{
		bool WasJustReset { get; set; }
		bool ShowNullArgumentGuard { get; }
		NullArgumentGuard NullArgumentGuard { get; set; }
		string NullGuardFailedMessage { get; set; }
		NullGuardResult EvaluateNullGuard();
		bool MultipleInitializersPerTargetAllowed { get; }
		void SetReleaseArgumentOnDestroy(Arguments argument, bool shouldRelease);
		void SetIsArgumentAsyncValueProvider(Arguments argument, bool isAsyncValueProvider);
	}

	internal interface IInitializerEditorOnly<TClient> : IInitializerEditorOnly { }

	internal interface IInitializerEditorOnly<TClient, TArgument> : IInitializerEditorOnly<TClient>
	{
		TArgument Argument { get; set; }
		void OnReset(ref TArgument argument);
	}

	internal interface IInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument> : IInitializerEditorOnly<TClient>
	{
		TFirstArgument FirstArgument { get; set; }
		TSecondArgument SecondArgument { get; set; }
		void OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument);
	}

	internal interface IInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument> : IInitializerEditorOnly<TClient>
	{
		TFirstArgument FirstArgument { get; set; }
		TSecondArgument SecondArgument { get; set; }
		TThirdArgument ThirdArgument { get; set; }
		void OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument, ref TThirdArgument thirdArgument);
	}

	internal interface IInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument> : IInitializerEditorOnly<TClient>
	{
		TFirstArgument FirstArgument { get; set; }
		TSecondArgument SecondArgument { get; set; }
		TThirdArgument ThirdArgument { get; set; }
		TFourthArgument FourthArgument { get; set; }
		void OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument, ref TThirdArgument thirdArgument, ref TFourthArgument fourthArgument);
	}

	internal interface IInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument> : IInitializerEditorOnly<TClient>
	{
		TFirstArgument FirstArgument { get; set; }
		TSecondArgument SecondArgument { get; set; }
		TThirdArgument ThirdArgument { get; set; }
		TFourthArgument FourthArgument { get; set; }
		TFifthArgument FifthArgument { get; set; }
		void OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument, ref TThirdArgument thirdArgument, ref TFourthArgument fourthArgument, ref TFifthArgument fifthArgument);
	}

	internal interface IInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument> : IInitializerEditorOnly<TClient>
	{
		TFirstArgument FirstArgument { get; set; }
		TSecondArgument SecondArgument { get; set; }
		TThirdArgument ThirdArgument { get; set; }
		TFourthArgument FourthArgument { get; set; }
		TFifthArgument FifthArgument { get; set; }
		TSixthArgument SixthArgument { get; set; }
		void OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument, ref TThirdArgument thirdArgument, ref TFourthArgument fourthArgument, ref TFifthArgument fifthArgument, ref TSixthArgument sixthArgument);
	}

	internal interface IAnyInitializerEditorOnly<TClient, TArgument> : IInitializerEditorOnly<TClient> where TClient : Object
	{
		Any<TArgument> Argument { get; set; }
		void OnReset(ref TArgument argument);
	}

	internal interface IAnyInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument> : IInitializerEditorOnly<TClient> where TClient : Object
	{
		Any<TFirstArgument> FirstArgument { get; set; }
		Any<TSecondArgument> SecondArgument { get; set; }
		void OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument);
	}

	internal interface IAnyInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument> : IInitializerEditorOnly<TClient> where TClient : Object
	{
		Any<TFirstArgument> FirstArgument { get; set; }
		Any<TSecondArgument> SecondArgument { get; set; }
		Any<TThirdArgument> ThirdArgument { get; set; }
		void OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument, ref TThirdArgument thirdArgument);
	}

	internal interface IAnyInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument> : IInitializerEditorOnly<TClient> where TClient : Object
	{
		Any<TFirstArgument> FirstArgument { get; set; }
		Any<TSecondArgument> SecondArgument { get; set; }
		Any<TThirdArgument> ThirdArgument { get; set; }
		Any<TFourthArgument> FourthArgument { get; set; }
		void OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument, ref TThirdArgument thirdArgument, ref TFourthArgument fourthArgument);
	}

	internal interface IAnyInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument> : IInitializerEditorOnly<TClient> where TClient : Object
	{
		Any<TFirstArgument> FirstArgument { get; set; }
		Any<TSecondArgument> SecondArgument { get; set; }
		Any<TThirdArgument> ThirdArgument { get; set; }
		Any<TFourthArgument> FourthArgument { get; set; }
		Any<TFifthArgument> FifthArgument { get; set; }
		void OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument, ref TThirdArgument thirdArgument, ref TFourthArgument fourthArgument, ref TFifthArgument fifthArgument);
	}

	internal interface IAnyInitializerEditorOnly<TClient, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument> : IInitializerEditorOnly<TClient> where TClient : Object
	{
		Any<TFirstArgument> FirstArgument { get; set; }
		Any<TSecondArgument> SecondArgument { get; set; }
		Any<TThirdArgument> ThirdArgument { get; set; }
		Any<TFourthArgument> FourthArgument { get; set; }
		Any<TFifthArgument> FifthArgument { get; set; }
		Any<TSixthArgument> SixthArgument { get; set; }
		void OnReset(ref TFirstArgument firstArgument, ref TSecondArgument secondArgument, ref TThirdArgument thirdArgument, ref TFourthArgument fourthArgument, ref TFifthArgument fifthArgument, ref TSixthArgument sixthArgument);
	}
}
#endif