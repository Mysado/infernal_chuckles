/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEditor;
using UnityEngine;

namespace InfinityCode.RunDesiredScene
{
    public static class Icons
    {
        private static Texture2D _help;
        private static Texture2D _hidden;
        private static Texture2D _starBlack;
        private static Texture2D _starWhite;
        private static Texture2D _starYellow;
        private static Texture2D _updateAvailable;

        public static Texture2D help
        {
            get
            {
                if (_help == null) _help = EditorUtils.LoadIcon("Help");
                return _help;
            }
        }

        public static Texture2D hidden
        {
            get
            {
                if (_hidden == null) _hidden = EditorUtils.LoadIcon(EditorGUIUtility.isProSkin?  "Hidden-White": "Hidden-Black");
                return _hidden;
            }
        }

        public static Texture2D starBlack
        {
            get
            {
                if (_starBlack == null) _starBlack = EditorUtils.LoadIcon("Star-Black");
                return _starBlack;
            }
        }

        public static Texture2D starWhite
        {
            get
            {
                if (_starWhite == null) _starWhite = EditorUtils.LoadIcon("Star-White");
                return _starWhite;
            }
        }

        public static Texture2D starYellow
        {
            get
            {
                if (_starYellow == null) _starYellow = EditorUtils.LoadIcon("Star-Yellow");
                return _starYellow;
            }
        }

        public static Texture updateAvailable
        {
            get
            {
                if (_updateAvailable == null) _updateAvailable = EditorUtils.LoadIcon("Update-Available");
                return _updateAvailable;
            }
        }
    }
}