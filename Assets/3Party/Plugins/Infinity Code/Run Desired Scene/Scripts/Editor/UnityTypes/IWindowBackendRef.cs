/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using System.Reflection;

namespace InfinityCode.RunDesiredScene.UnityTypes
{
#if UNITY_2020_1_OR_NEWER
    public static class IWindowBackendRef
    {
        private static Type _type;

        public static Type type
        {
            get
            {
                if (_type == null) _type = ReflectionHelper.GetEditorType("IWindowBackend");
                return _type;
            }
        }

        private static PropertyInfo _visualTreeProp;
        public static PropertyInfo visualTreeProp
        {
            get
            {
                if (_visualTreeProp == null) _visualTreeProp = type.GetProperty("visualTree", ReflectionHelper.InstanceLookup);
                return _visualTreeProp;
            }
        }
    }
#endif
}