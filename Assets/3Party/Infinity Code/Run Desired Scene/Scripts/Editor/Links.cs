/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEditor;
using UnityEngine;

namespace InfinityCode.RunDesiredScene
{
    public static class Links
    {
        public const string assetStore = "https://assetstore.unity.com/packages/slug/262762";
        public const string changelog = "https://infinity-code.com/products_update/get-changelog.php?asset=Run%20Desired%20Scene&from=1.0";
        public const string discord = "https://discord.gg/2XRWwPgZK4";
        public const string documentation = "https://infinity-code.com/documentation/run-desired-scene.html";
        public const string forum = "https://forum.infinity-code.com";
        public const string homepage = "https://infinity-code.com/assets/run-desired-scene";
        public const string reviews = assetStore + "/reviews";
        public const string support = "mailto:support@infinity-code.com?subject=Run%20Desired%20Scene";
        public const string youtube = "https://www.youtube.com/channel/UCxCID3jp7RXKGqiCGpjPuOg";
        private const string aid = "?aid=1100liByC";

        public static void Open(string url)
        {
            Application.OpenURL(url);
        }

        public static void OpenAssetStore()
        {
            Open(assetStore + aid);
        }

        public static void OpenChangelog()
        {
            Open(changelog);
        }

        public static void OpenDiscord()
        {
            Open(discord);
        }

        [MenuItem(EditorUtils.MENU_PATH + "Documentation", false, 120)]
        public static void OpenDocumentation()
        {
            OpenDocumentation(null);
        }

        public static void OpenDocumentation(string anchor)
        {
            string url = documentation;
            if (!string.IsNullOrEmpty(anchor)) url += "#" + anchor;
            Open(url);
        }

        public static void OpenForum()
        {
            Open(forum);
        }

        public static void OpenHomepage()
        {
            Open(homepage);
        }

        public static void OpenLocalDocumentation()
        {
            string url = EditorUtils.assetPath + "Documentation/Content/Documentation-Content.html";
            Application.OpenURL(url);
        }

        public static void OpenReviews()
        {
            Open(reviews + aid);
        }

        public static void OpenSupport()
        {
            Open(support);
        }

        public static void OpenYouTube()
        {
            Open(youtube);
        }
    }
}