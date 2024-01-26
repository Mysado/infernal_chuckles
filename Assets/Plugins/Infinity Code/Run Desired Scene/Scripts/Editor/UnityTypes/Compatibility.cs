/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEngine;
using UnityEngine.UIElements;

namespace InfinityCode.RunDesiredScene.UnityTypes
{
    [HideInIntegrity]
    public static class Compatibility
    {
        public static VisualElement GetVisualTree(ScriptableObject scriptableObject)
        {
#if UNITY_2020_1_OR_NEWER
            object backend = GUIViewRef.windowBackendProp.GetValue(scriptableObject, null);
            return (VisualElement)IWindowBackendRef.visualTreeProp.GetValue(backend, null);
#else
            return GUIViewRef.visualTreeProp.GetValue(scriptableObject, null) as VisualElement;
#endif
        }
    }
}