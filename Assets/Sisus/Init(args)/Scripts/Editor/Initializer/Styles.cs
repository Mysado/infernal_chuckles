using UnityEngine;

namespace Sisus.Init.EditorOnly
{
	internal static class Styles
	{
		internal readonly static GUIStyle RefTag;
		internal readonly static GUIStyle ServiceTag;
		internal readonly static GUIStyle ValueProvider;
		internal readonly static GUIStyle Discard;

		static Styles()
		{
			ServiceTag = new GUIStyle("AssetLabel");
			ServiceTag.contentOffset = new Vector2(0f, -1f);
			ValueProvider = ServiceTag;
			RefTag = ServiceTag;
			Discard = new GUIStyle("SearchCancelButton");
			Discard.alignment = TextAnchor.MiddleRight;
		}
	}
}