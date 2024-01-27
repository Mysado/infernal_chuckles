/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using System.Reflection;
using UnityEngine;

namespace InfinityCode.RunDesiredScene.UnityTypes
{
    public static class GUIViewRef
    {
        private static Type _type;
        private static PropertyInfo _currentProp;
        private static MethodInfo _sendEventMethod;

        public static Type type
        {
            get
            {
                if (_type == null) _type = ReflectionHelper.GetEditorType("GUIView");
                return _type;
            }
        }


        public static PropertyInfo currentProp
        {
            get
            {
                if (_currentProp == null) _currentProp = type.GetProperty("current", ReflectionHelper.StaticLookup);
                return _currentProp;
            }
        }

        public static MethodInfo sendEventMethod
        {
            get
            {
                if (_sendEventMethod == null) _sendEventMethod = type.GetMethod("SendEvent", ReflectionHelper.InstanceLookup, null, new []{typeof(Event)}, null);
                return _sendEventMethod;
            }
        }


        

#if UNITY_2020_1_OR_NEWER
        private static PropertyInfo _windowBackendProp;
        public static PropertyInfo windowBackendProp
        {
            get
            {
                if (_windowBackendProp == null) _windowBackendProp = type.GetProperty("windowBackend", ReflectionHelper.InstanceLookup);
                return _windowBackendProp;
            }
        }
#else 
        private static PropertyInfo _visualTreeProp;
        public static PropertyInfo visualTreeProp
        {
            get
            {
                if (_visualTreeProp == null) _visualTreeProp = type.GetProperty("visualTree", ReflectionHelper.InstanceLookup);
                return _visualTreeProp;
            }
        }
#endif

        public static object GetCurrent()
        {
            return currentProp.GetValue(null);
        }

        public static void SendEvent(object view, Event e)
        {
            sendEventMethod.Invoke(view, new object[] {e});
        }
    }
}