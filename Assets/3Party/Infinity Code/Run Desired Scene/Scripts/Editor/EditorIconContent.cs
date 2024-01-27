/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEditor;
using UnityEngine;

namespace InfinityCode.RunDesiredScene
{
    public static class EditorIconContent
    {
        private static GUIContent _playButton;
        private static GUIContent _sceneLoadIn;
        
        public static GUIContent playButton
        {
            get
            {
                if (_playButton == null) _playButton = EditorGUIUtility.IconContent("PlayButton");
                return _playButton;
            }
        }
        
        public static GUIContent sceneLoadIn
        {
            get
            {
                if (_sceneLoadIn == null) _sceneLoadIn = EditorGUIUtility.IconContent("SceneLoadIn");
                return _sceneLoadIn;
            }
        }
    }
}