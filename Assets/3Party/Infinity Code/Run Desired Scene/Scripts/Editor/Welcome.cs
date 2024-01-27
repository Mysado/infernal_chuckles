/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.RunDesiredScene
{
    [InitializeOnLoad]
    public class Welcome : EditorWindow
    {
        private const string ShowAtStartupPrefs = EditorUtils.PREFS_PREFIX + "ShowWelcomeScreen";

        private string copyright = "Infinity Code " + DateTime.Now.Year;
        private static bool showAtStartup = true;
        private Vector2 scrollPosition;
        private static bool inited;
        public static GUIStyle headerStyle;
        private static Texture2D discordTexture;
        private static Texture2D docTexture;
        private static Texture2D forumTexture;
        private static Texture2D rateTexture;
        private static Texture2D supportTexture;
        private static Texture2D updateTexture;
        private static Texture2D urlTexture;
        private static Texture2D videoTexture;
        private static GUIStyle copyrightStyle;
        private static Welcome wnd;

        static Welcome()
        {
            EditorApplication.update -= GetShowAtStartup;
            EditorApplication.update += GetShowAtStartup;
        }

        public static bool DrawButton(Texture2D texture, string title, string body = "", int space = 10)
        {
            try
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(34);
                GUILayout.Box(texture, GUIStyle.none, GUILayout.MaxWidth(48), GUILayout.MaxHeight(48));
                GUILayout.Space(10);

                GUILayout.BeginVertical();
                GUILayout.Space(1);
                GUILayout.Label(title, EditorStyles.boldLabel);
                GUILayout.Label(body, EditorStyles.wordWrappedLabel);
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();

                Rect rect = GUILayoutUtility.GetLastRect();
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

                bool returnValue = Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition);

                GUILayout.Space(space);

                return returnValue;
            }
            catch
            {

            }

            return false;
        }

        private void DrawContent()
        {
            GUI.Box(new Rect(0, 0, 500, 58), "v" + Version.version, headerStyle);
            GUILayoutUtility.GetRect(position.width, 58);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(10);

           if (DrawButton(urlTexture, "Product Page", "Visit the official asset page"))
            {
                Links.OpenHomepage();
            }

            if (DrawButton(docTexture, "Documentation", "Online version of the documentation"))
            {
                Links.OpenDocumentation();
            }

            if (DrawButton(supportTexture, "Support", "If you have any problems feel free to contact us"))
            {
                Links.OpenSupport();
            }
            
            if (DrawButton(discordTexture, "Discord", "Join our Discord server"))
            {
                Links.OpenDiscord();
            }

            if (DrawButton(forumTexture, "Forum", "Official forum of Run Desired Scene"))
            {
                Links.OpenForum();
            }

            if (DrawButton(videoTexture, "Videos", "Check out new videos about asset"))
            {
                Links.OpenYouTube();
            }

            if (DrawButton(rateTexture, "Rate and Review", "Share your impression about the asset"))
            {
                Links.OpenReviews();
            }

            if (DrawButton(updateTexture, "Check Updates", "Perhaps a new version is already waiting for you. Check it!"))
            {
                Updater.OpenWindow();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField(copyright, copyrightStyle);
        }

        private static void GetShowAtStartup()
        {
            EditorApplication.update -= GetShowAtStartup;
            showAtStartup = EditorPrefs.GetBool(ShowAtStartupPrefs, true);

            if (showAtStartup)
            {
                EditorApplication.update -= OpenAtStartup;
                EditorApplication.update += OpenAtStartup;
            }
        }

        private void Init()
        {
            headerStyle = new GUIStyle();
            headerStyle.normal.background = EditorUtils.LoadTexture("Welcome/Logo");
            headerStyle.padding = new RectOffset(435, 0, 18, 0);
            headerStyle.normal.textColor = Color.white;
            headerStyle.margin = new RectOffset(0, 0, 0, 0);

            copyrightStyle = new GUIStyle();
            copyrightStyle.alignment = TextAnchor.MiddleRight;
            copyrightStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;

            discordTexture = EditorUtils.LoadTexture("Welcome/Discord");
            docTexture = EditorUtils.LoadTexture("Welcome/Docs");
            forumTexture = EditorUtils.LoadTexture("Welcome/Forum");
            rateTexture = EditorUtils.LoadTexture("Welcome/Rate");
            supportTexture = EditorUtils.LoadTexture("Welcome/Support");
            updateTexture = EditorUtils.LoadTexture("Welcome/Update");
            urlTexture = EditorUtils.LoadTexture("Welcome/URL");
            videoTexture = EditorUtils.LoadTexture("Welcome/Video");

            inited = true;
        }

        private void OnDestroy()
        {
            wnd = null;
            EditorPrefs.SetBool(ShowAtStartupPrefs, false);
            
            Resources.UnloadAsset(discordTexture);
            Resources.UnloadAsset(docTexture);
            Resources.UnloadAsset(forumTexture);
            Resources.UnloadAsset(supportTexture);
            Resources.UnloadAsset(updateTexture);
            Resources.UnloadAsset(urlTexture);
            Resources.UnloadAsset(videoTexture);
        }

        private void OnEnable()
        {
            wnd = this;
            wnd.minSize = new Vector2(500, 300);
            wnd.maxSize = new Vector2(500, 300);
        }

        private void OnGUI()
        {
            if (!inited) Init();

            try
            {
                DrawContent();
            }
            catch
            {

            }
        }

        private static void OpenAtStartup()
        {
            EditorApplication.update -= OpenAtStartup;
            OpenWindow();
        }

        [MenuItem(EditorUtils.MENU_PATH + "Welcome", false, 120)]
        public static void OpenWindow()
        {
            if (wnd != null) return;

            wnd = GetWindow<Welcome>(true, "Welcome to Run Desired Scene", true);
            wnd.maxSize = wnd.minSize = new Vector2(500, 440);
            wnd.Focus();
        }

        [MenuItem(EditorUtils.MENU_PATH + "Rate and Review", false, 125)]
        public static void RateAndReview()
        {
            Links.OpenReviews();
        }
    }
}