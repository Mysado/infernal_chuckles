using System.Linq;
using Sisus.Init.Internal;
using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly.Internal
{
	internal static class ComponentMenuItems
	{
		private const string AddServiceTagMenuItemName = "CONTEXT/Component/Make Service Of Type...";

		[MenuItem(AddServiceTagMenuItemName, priority = 1500)]
		private static void AddServiceTag(MenuCommand command)
			=> ServiceTagUtility.openSelectTagsMenuFor = command.context as Component;

		[MenuItem(AddServiceTagMenuItemName, validate = true)]
		private static bool ShowAddServiceTag(MenuCommand command)
			// If component has the ServiceAttribute disable opening of the service tags selection menu.
			=> command.context is Component component && ServiceTagUtility.CanAddServiceTag(component);

		private const string GenerateInitializerMenuItemName = "CONTEXT/Component/Generate Initializer...";

		[MenuItem(GenerateInitializerMenuItemName, priority = 1501)]
		private static void GenerateInitializer(MenuCommand command) => ScriptGenerator.CreateInitializer(command.context);

		[MenuItem(GenerateInitializerMenuItemName, validate = true)]
		private static bool ShowGenerateInitializer(MenuCommand command)
		{
			return (command.context is Component
				 || (command.context is ScriptableObject scriptableObject && TypeUtility.DerivesFromGenericBaseType(scriptableObject.GetType())))
				 && !InitializerEditorUtility.GetInitializerTypes(command.context.GetType()).Any();
		}
	}
}