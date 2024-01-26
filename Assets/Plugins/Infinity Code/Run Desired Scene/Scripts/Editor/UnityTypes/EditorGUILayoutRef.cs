/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.RunDesiredScene.UnityTypes
{
    public static class EditorGUILayoutRef
    {
        private static MethodInfo _beginVerticalScrollViewMethod;

        private static Type type
        {
            get => typeof(EditorGUILayout);
        }

        private static MethodInfo beginVerticalScrollViewMethod
        {
            get
            {
                if (_beginVerticalScrollViewMethod == null) _beginVerticalScrollViewMethod = type.GetMethod("BeginVerticalScrollView", ReflectionHelper.StaticLookup, null, new []{typeof(Vector2), typeof(GUILayoutOption[])}, null);
                return _beginVerticalScrollViewMethod;
            }
        }
        
        public static Vector2 BeginVerticalScrollView(Vector2 scrollPosition, params GUILayoutOption[] options)
        {
            return (Vector2)beginVerticalScrollViewMethod.Invoke(null, new object[] {scrollPosition, options});
        }
        
    }
}