/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System.IO;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.RunDesiredScene
{
    public static class EditorUtils
    {
        public const string MENU_PATH = "Tools/Run Desired Scene/";
        public const string PREFS_PREFIX = "RunDesiredScene_";
        
        private static string _assetPath;
        
        public static string assetPath
        {
            get
            {
                if (_assetPath == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:asmdef RunDesiredScene-Editor");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        FileInfo info = new FileInfo(path);
                        _assetPath = info.Directory.Parent.Parent.FullName.Substring(Application.dataPath.Length - 6) + "/";
                    }
                    else
                    {
                        _assetPath = "Assets/Plugins/Infinity Code/Run Desired Scene/";
                    }
                }

                return _assetPath;
            }
        }

        public static Texture2D LoadIcon(string name)
        {
            return LoadTexture("Icons/" + name);
        }

        public static Texture2D LoadTexture(string name, string extension = "png")
        {
            string path = Path.Combine(assetPath, "Textures", name + "." + extension);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}