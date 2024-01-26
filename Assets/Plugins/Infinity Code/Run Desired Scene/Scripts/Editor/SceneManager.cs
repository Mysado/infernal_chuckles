/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace InfinityCode.RunDesiredScene
{
    [InitializeOnLoad]
    public class SceneManager : ScriptableObject
    {
        private static HashSet<SceneAsset> _additiveHashSet;
        private static HashSet<SceneAsset> _favoriteHashSet;
        private static HashSet<SceneAsset> _hiddenHashSet;
        private static SceneManager _instance;
        
        public SceneAsset startScene;
        public SceneAsset[] additiveScenes;
        public SceneAsset[] favorites;
        public SceneAsset[] hidden;

        private static HashSet<SceneAsset> additiveHashSet
        {
            get
            {
                if (_additiveHashSet == null)
                {
                    if (instance.additiveScenes == null) instance.additiveScenes = new SceneAsset[0];
                    _additiveHashSet = new HashSet<SceneAsset>(instance.additiveScenes);
                }

                return _additiveHashSet;
            }
        }

        private static HashSet<SceneAsset> favoriteHashSet
        {
            get
            {
                if (_favoriteHashSet == null)
                {
                    if (instance.favorites == null) instance.favorites = new SceneAsset[0];
                    _favoriteHashSet = new HashSet<SceneAsset>(instance.favorites);
                }

                return _favoriteHashSet;
            }
        }
        
        private static HashSet<SceneAsset> hiddenHashSet
        {
            get
            {
                if (_hiddenHashSet == null)
                {
                    if (instance.hidden == null) instance.hidden = new SceneAsset[0];
                    _hiddenHashSet = new HashSet<SceneAsset>(instance.hidden);
                }

                return _hiddenHashSet;
            }
        }

        public static SceneManager instance
        {
            get
            {
                if (_instance == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:SceneManager");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        _instance = AssetDatabase.LoadAssetAtPath<SceneManager>(path);
                    }
                    else
                    {
                        _instance = CreateInstance<SceneManager>();
                        AssetDatabase.CreateAsset(_instance, EditorUtils.assetPath + "SceneManager.asset");
                    }
                }

                return _instance;
            }
        }

        static SceneManager()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            
            EditorApplication.delayCall += () => EditorSceneManager.playModeStartScene = instance.startScene;
        }

        public static void AddAdditive(SceneAsset scene)
        {
            SceneAsset[] buildScenes = GetBuildScenes();
            if (!Array.Exists(buildScenes, s => s == scene))
            {
                bool v = EditorUtility.DisplayDialog(
                    "Error", 
                    "Scene is not in build settings. Add a scene in build settings?", 
                    "Yes", "No");
                
                if (!v) return;
                ArrayUtility.Add(ref buildScenes, scene);
                EditorBuildSettings.scenes = buildScenes.Select(
                    s => new EditorBuildSettingsScene(AssetDatabase.GetAssetPath(s), true)
                    ).ToArray();
                SelectSceneWindow.isDirty = true;
            }

            ArrayUtility.Add(ref instance.additiveScenes, scene);
            additiveHashSet.Add(scene);
            Save();
        }

        public static void AddFavorite(SceneAsset scene)
        {
            ArrayUtility.Add(ref instance.favorites, scene);
            favoriteHashSet.Add(scene);
            Save();
        }

        public static void ClearCache()
        {
            _additiveHashSet = null;
            _favoriteHashSet = null;
            _hiddenHashSet = null;
        }

        public static SceneAsset[] GetBuildScenes()
        {
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            List<SceneAsset> scenes = new List<SceneAsset>();
            
            for (int i = 0; i < buildScenes.Length; i++)
            {
                SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScenes[i].path);
                if (scene == null) continue;
                if (!favoriteHashSet.Contains(scene)) scenes.Add(scene);
            }
            
            return scenes.ToArray();
        }

        public static SceneAsset[] GetFavoriteScenes()
        {
            if (instance.favorites == null)
            {
                instance.favorites = new SceneAsset[0];
                Save();
            }
            return instance.favorites;
        }

        public static SceneAsset[] GetOtherScenes()
        {
            SceneAsset[] buildScenes = GetBuildScenes();
            SceneAsset[] favoriteScenes = GetFavoriteScenes();
            
            SceneAsset[] scenes = AssetDatabase.FindAssets("t:SceneAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SceneAsset>)
                .Where(scene => !Array.Exists(buildScenes, s => s == scene))
                .Where(scene => !Array.Exists(favoriteScenes, s => s == scene))
                .ToArray();
            
            return scenes;
        }

        public static string GetStartSceneName()
        {
            StringBuilder sb = StaticStringBuilder.Start();
            if (instance.startScene == null) sb.Append("Current Scene");
            else sb.Append(instance.startScene.name);

            if (instance.additiveScenes != null)
            {
                int countAdditive = instance.additiveScenes.Length;
                if (countAdditive > 0)
                {
                    sb.Append(" (+");
                    sb.Append(countAdditive);
                    sb.Append(")");
                }   
            }

            return sb.ToString();
        }

        public static bool HasHidden()
        {
            return instance.hidden != null && instance.hidden.Length > 0;
        }

        public static bool IsAdditive(SceneAsset scene)
        {
            return additiveHashSet.Contains(scene);
        }

        public static bool IsHidden(SceneAsset scene)
        {
            return hiddenHashSet.Contains(scene);
        }

        public static bool IsFavorite(SceneAsset scene)
        {
            return favoriteHashSet.Contains(scene);
        }

        public static bool IsStartScene(SceneAsset scene)
        {
            return instance.startScene == scene;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (instance.additiveScenes == null && instance.additiveScenes.Length > 0) return;

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            foreach (SceneAsset scene in instance.additiveScenes)
            {
                if (scene == null) continue;
                int index = Array.FindIndex(scenes, s => s.path == AssetDatabase.GetAssetPath(scene));
                if (index != -1) UnityEngine.SceneManagement.SceneManager.LoadScene(index, LoadSceneMode.Additive);
            }
        }

        public static void RemoveAdditive(SceneAsset scene)
        {
            ArrayUtility.Remove(ref instance.additiveScenes, scene);
            additiveHashSet.Remove(scene);
            Save();
        }

        public static void RemoveFavorite(SceneAsset scene)
        {
            ArrayUtility.Remove(ref instance.favorites, scene);
            favoriteHashSet.Remove(scene);
            Save();
        }

        public static void Save()
        {
            EditorUtility.SetDirty(instance);
        }

        public static void SetStartScene(SceneAsset scene)
        {
            instance.startScene = scene;
            EditorSceneManager.playModeStartScene = scene;
            Save();
        }

        public static void ToggleHidden(SceneAsset scene)
        {
            if (hiddenHashSet.Contains(scene))
            {
                ArrayUtility.Remove(ref instance.hidden, scene);
                hiddenHashSet.Remove(scene);
            }
            else
            {
                ArrayUtility.Add(ref instance.hidden, scene);
                hiddenHashSet.Add(scene);
            }
            Save();
        }
    }
}