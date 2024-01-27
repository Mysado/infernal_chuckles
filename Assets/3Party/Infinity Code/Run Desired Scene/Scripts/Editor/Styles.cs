/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEditor;
using UnityEngine;

namespace InfinityCode.RunDesiredScene
{
    public static class Styles
    {
        private static GUIStyle _centeredLabel;
        
        public static GUIStyle centeredLabel
        {
            get
            {
                if (_centeredLabel == null)
                {
                    _centeredLabel = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                }

                return _centeredLabel;
            }
        }
    }
}