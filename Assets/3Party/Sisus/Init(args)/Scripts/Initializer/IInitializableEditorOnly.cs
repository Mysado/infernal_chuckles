#if UNITY_EDITOR
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("InitArgs")]

namespace Sisus.Init.EditorOnly
{
	internal interface IInitializableEditorOnly
	{
		[MaybeNull]
		IInitializer Initializer { get; set; }
	}
}
#endif