/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InfinityCode.RunDesiredScene.UnityTypes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace InfinityCode.RunDesiredScene
{
    public class SelectSceneWindow : EditorWindow
    {
        public static bool isDirty;
        
        private static GUIContent additiveContent = new GUIContent("Additive", "Load Scene Additively When Play");
        private static GUIContent setStartSceneButton = new GUIContent("", "Set Start Scene");
        
        private static GUIContent openContent;
        private static GUIContent showHiddenContent;
        private static GUIContent playButton;
        private static GUIContent starActiveContent;
        private static GUIContent starInactiveContent;
        
        private static double lastClickTime;

        [NonSerialized]
        private SceneAsset[] buildScenes;

        [NonSerialized]
        private SceneAsset[] favoriteScenes;

        [NonSerialized]
        private SceneAsset[] otherScenes;
        
        [NonSerialized]
        private int adjustSize = 2; // 0 Not Adjust, 1 Adjust Only Height, 2 Adjust Height and Width

        [NonSerialized]
        private string filter;

        [NonSerialized]
        private SceneAsset[] filteredScenes;

        [NonSerialized]
        private bool focusOnTextField = true;

        [NonSerialized]
        private Vector2 scrollPosition;

        [FormerlySerializedAs("showHidden")] [SerializeField]
        private bool showHiddenScenes;
        
        public static bool AskForSave(params Scene[] scenes)
        {
            if (scenes.Length == 0) return true;

            List<string> paths = new List<string>();

            for (int i = 0; i < scenes.Length; i++)
            {
                Scene scene = scenes[i];

                if (scene.isDirty) paths.Add(scene.path);
            }

            if (paths.Count > 0)
            {
                string pathStr = String.Join("\n", paths);
                if (pathStr.Length == 0) pathStr = "Untitled";

                int result = EditorUtility.DisplayDialogComplex("Scene(s) Have Been Modified", "Do you want to save the changes you made in the scenes:\n" + pathStr + "\n\nYour changes will be lost if you don't save them.", "Save", "Don't Save", "Cancel");
                if (result == 2) return false;

                if (result == 0)
                {
                    for (int i = 0; i < scenes.Length; i++)
                    {
                        Scene scene = scenes[i];
                        if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                    }
                }
            }

            return true;
        }

        private void DrawCurrentItem()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button(playButton, EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                EditorSceneManager.playModeStartScene = null;
                EditorApplication.isPlaying = true;
                Close();
            }
            
            bool isStartScene = SceneManager.IsStartScene(null);
            EditorGUI.BeginChangeCheck();
            GUILayout.Toggle(isStartScene, setStartSceneButton, EditorStyles.toggle, GUILayout.Width(20));
            if (EditorGUI.EndChangeCheck())
            {
                SceneManager.SetStartScene(null);
                isDirty = true;
                GUI.changed = true;
            }

            GUILayout.Label("Current Scene", EditorStyles.label);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHelpMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Welcome"), false, Welcome.OpenWindow);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Product Page"), false, Links.OpenHomepage);
            menu.AddItem(new GUIContent("Documentation"), false, Links.OpenDocumentation);
            menu.AddItem(new GUIContent("Videos"), false, Links.OpenYouTube);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Support"), false, Links.OpenSupport);
            menu.AddItem(new GUIContent("Discord"), false, Links.OpenDiscord);
            menu.AddItem(new GUIContent("Forum"), false, Links.OpenForum);
            menu.AddItem(new GUIContent("Check Updates"), false, Updater.OpenWindow);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Rate and Review"), false, Welcome.RateAndReview);
            menu.AddItem(new GUIContent("About"), false, About.OpenWindow);

            menu.ShowAsContext();
        }

        private void DrawItem(SceneAsset scene, string label = null)
        {
            if (scene == null) return;
            if (string.IsNullOrEmpty(label)) label = scene.name;
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button(playButton, EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                EditorSceneManager.playModeStartScene = scene;
                EditorApplication.isPlaying = true;
                Close();
            }

            bool isStartScene = SceneManager.IsStartScene(scene);
            EditorGUI.BeginChangeCheck();
            isStartScene = GUILayout.Toggle(isStartScene, setStartSceneButton, EditorStyles.toggle, GUILayout.Width(20));
            if (EditorGUI.EndChangeCheck())
            {
                if (isStartScene) SceneManager.SetStartScene(scene);
                else SceneManager.SetStartScene(null);
                isDirty = true;
                GUI.changed = true;
            }

            string scenePath = AssetDatabase.GetAssetPath(scene);
            GUIContent content;

            if (SceneManager.IsHidden(scene))
            {
                content = TempContent.Get(Icons.hidden, "Make Unhidden");
                if (GUILayout.Button(content, EditorStyles.label, GUILayout.Width(16)))
                {
                    SceneManager.ToggleHidden(scene);
                }
            }
            
            StringBuilder sb = StaticStringBuilder.Start();
            sb.Append(scenePath);
            sb.Append("\n\n(Click to Ping)\n(Double Click to Open)\n(Shift+Double Click to Open Additive)");
            content = TempContent.Get(label, sb.ToString());

            ButtonEvent buttonEvent = GUILayoutUtils.Button(content, EditorStyles.label);
            Event e = Event.current;

            if (buttonEvent == ButtonEvent.click)
            {
                if (e.button == 0)
                {
                    if (EditorApplication.timeSinceStartup - lastClickTime < 0.3)
                    {
                        if (!e.shift)
                        {
                            if (AskForSave(EditorSceneManager.GetActiveScene()))
                            {
                                EditorSceneManager.OpenScene(scenePath);
                            }
                        }
                        else EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    }
                    else
                    {
                        lastClickTime = EditorApplication.timeSinceStartup;
                        EditorGUIUtility.PingObject(scene);
                    }
                }
                else if (e.button == 1) ShowSceneContextMenu(scene);
            }
            GUILayout.Space(5);
            
            EditorGUI.BeginChangeCheck();
            bool isFavorite = SceneManager.IsFavorite(scene);
            GUIContent starContent = isFavorite ? starActiveContent : starInactiveContent;
            bool v = GUILayout.Toggle(isFavorite, starContent, EditorStyles.toolbarButton, GUILayout.Width(25));
            if (EditorGUI.EndChangeCheck())
            {
                if (v) SceneManager.AddFavorite(scene);
                else SceneManager.RemoveFavorite(scene);
                isDirty = true;
            }
            
            bool isAdditive = SceneManager.IsAdditive(scene);
            EditorGUI.BeginChangeCheck();
            isAdditive = GUILayout.Toggle(isAdditive, "A", EditorStyles.toolbarButton, GUILayout.Width(20));
            if (EditorGUI.EndChangeCheck())
            {
                if (isAdditive) SceneManager.AddAdditive(scene);
                else SceneManager.RemoveAdditive(scene);
                isDirty = true;
            }

            if (GUILayoutUtils.Button(openContent, EditorStyles.toolbarButton, GUILayout.Width(25)) == ButtonEvent.click)
            {
                if (e.modifiers == EventModifiers.None)
                {
                    if (AskForSave(EditorSceneManager.GetActiveScene()))
                    {
                        EditorSceneManager.OpenScene(scenePath);
                    }
                }
                else
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawItems(SceneAsset[] scenes, string label = null)
        {
            if (scenes == null || scenes.Length == 0) return;
            if (!string.IsNullOrEmpty(label))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                Rect rect = EditorGUILayout.GetControlRect(false, 1);
                rect.xMin -= 14;
                rect.xMax += 14;
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color32(32, 32, 32, 255) : new Color32(127, 127, 127, 255));
            }
            
            foreach (SceneAsset scene in scenes)
            {
                DrawItem(scene);
            }
        }

        private static void  DrawNewVersionNotification(ref int heightOffset)
        {
            if (!Updater.hasNewVersion) return;
            
            Color color = GUI.backgroundColor;
            GUI.backgroundColor = Color.red;
            GUIContent updateContent = TempContent.Get(Icons.updateAvailable, "Update Available. Click to open the updater.");
            updateContent.text = "Update Available. Click to open the updater.";
            if (GUILayout.Button(updateContent, EditorStyles.toolbarButton)) Updater.OpenWindow();
            GUI.backgroundColor = color;
            heightOffset += 20;
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName("SSFilterTextField");
            filter = EditorGUILayout.TextField(filter, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck()) UpdateFilteredScenes();

            if (focusOnTextField && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("SSFilterTextField");
                focusOnTextField = false;
            }

            if (SceneManager.HasHidden())
            {
                EditorGUI.BeginChangeCheck();
                showHiddenScenes = GUILayout.Toggle(showHiddenScenes, showHiddenContent, EditorStyles.toolbarButton, GUILayout.Width(20));
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateScenes();
                    if (!string.IsNullOrEmpty(filter)) UpdateFilteredScenes();
                }
            }

            if (GUILayout.Button("?", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                DrawHelpMenu();
            }

            GUILayout.EndHorizontal();
        }

        private void OnDisable()
        {
            buildScenes = null;
            favoriteScenes = null;
            otherScenes = null;
        }

        private void OnEnable()
        {
            Updater.CheckNewVersionAvailable();
            UpdateScenes();
        }

        private void OnGUI()
        {
            if (focusedWindow != this)
            {
                Close();
                return;
            }
            
            if (playButton == null) playButton = new GUIContent(EditorIconContent.playButton.image, "Play Scene");
            if (starActiveContent == null)  starActiveContent = new GUIContent(Icons.starYellow, "Remove from Favorites");
            if (starInactiveContent == null) starInactiveContent = new GUIContent(EditorGUIUtility.isProSkin? Icons.starWhite: Icons.starBlack, "Add to Favorites");
            if (openContent == null) openContent = new GUIContent(EditorIconContent.sceneLoadIn.image, "Click - Open Scene\nShift+Click - Open Additive");
            if (showHiddenContent == null) showHiddenContent = new GUIContent(Icons.hidden, "Show Hidden Scenes");
            showHiddenContent.tooltip = showHiddenScenes ? "Hide Hidden Scenes" : "Show Hidden Scenes";
            
            if (isDirty) UpdateScenes();

            int heightOffset = 25;
            DrawNewVersionNotification(ref heightOffset);
            DrawToolbar();

            scrollPosition = EditorGUILayoutRef.BeginVerticalScrollView(scrollPosition);

            if (filteredScenes != null)
            {
                DrawItems(filteredScenes);
            }
            else
            {
                DrawCurrentItem();
                DrawItems(favoriteScenes, "Favorites");
                DrawItems(buildScenes, "In Build");
                DrawItems(otherScenes, "Other Scenes");
            }

            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(0));
            float w = rect.width;
            float h = rect.yMin + heightOffset;
            if (adjustSize > 0 && Event.current.type == EventType.Repaint)
            {
                int widthOffset = h > 400 ? 20 : 0;
                h = Mathf.Min(h, 400);
                w = Mathf.Max(w + widthOffset, 300);
                if (Math.Abs(position.height - h) > float.Epsilon || Math.Abs(position.width - w) > float.Epsilon)
                {
                    Rect r = position;
                    if (adjustSize == 2)
                    {
                        r.width = w;
                        adjustSize = 1;
                    }
                    r.height = h;
                    position = r;
                }
            }
            
            EditorGUILayout.EndScrollView();
            
            if (isDirty) UpdateScenes();
        }

        private void ShowSceneContextMenu(SceneAsset scene)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Open"), false, () =>
            {
                if (AskForSave(EditorSceneManager.GetActiveScene()))
                {
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(scene));
                }
            });
            menu.AddItem(new GUIContent("Open Additive"), false, () => EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(scene), OpenSceneMode.Additive));
            menu.AddItem(new GUIContent("Select"), false, () => Selection.activeObject = scene);
            menu.AddItem(new GUIContent("Hide"), SceneManager.IsHidden(scene), () => ToggleHidden(scene));
            menu.ShowAsContext();
        }

        public static void ShowWindow(Vector2 position)
        {
            SelectSceneWindow wnd = CreateInstance<SelectSceneWindow>();
            wnd.titleContent = new GUIContent("Select Scene");
            position = GUIUtility.GUIToScreenPoint(position);
            Vector2 size = new Vector2(300, 400);
            Rect rect = new Rect(position, size);
            wnd.minSize = new Vector2(1, 1);
            wnd.position = rect;
            wnd.ShowPopup();
            wnd.Focus();
        }

        private void ToggleHidden(SceneAsset scene)
        {
            SceneManager.ToggleHidden(scene);
            isDirty = true;
        }

        private void UpdateFilteredScenes()
        {
            adjustSize = 2;
            
            if (string.IsNullOrEmpty(filter))
            {
                filteredScenes = null;
                return;
            }

            string pattern = SearchableItem.GetPattern(filter);

            filteredScenes = buildScenes.Where(s => s != null && SearchableItem.Match(pattern, s.name))
                .Concat(favoriteScenes.Where(s => s != null && SearchableItem.Match(pattern, s.name)))
                .Concat(otherScenes.Where(s => s != null && SearchableItem.Match(pattern, s.name)))
                .ToArray();
        }

        private void UpdateScenes()
        {
            SceneManager.ClearCache();
            
            favoriteScenes = SceneManager.GetFavoriteScenes();
            buildScenes = SceneManager.GetBuildScenes();
            otherScenes = SceneManager.GetOtherScenes();
            
            if (!showHiddenScenes)
            {
                favoriteScenes = favoriteScenes.Where(s => !SceneManager.IsHidden(s)).ToArray();
                buildScenes = buildScenes.Where(s => !SceneManager.IsHidden(s)).ToArray();
                otherScenes = otherScenes.Where(s => !SceneManager.IsHidden(s)).ToArray();
            }
            
            isDirty = false;
            adjustSize = 2;
        }
    }
}
