/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using System.Reflection;

namespace InfinityCode.RunDesiredScene
{
    public static class ReflectionHelper
    {
        public const BindingFlags InstanceLookup = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public const BindingFlags StaticLookup = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        
        private static Assembly _editorAssembly;

        public static Assembly editorAssembly
        {
            get
            {
                if (_editorAssembly == null) _editorAssembly = Assembly.Load("UnityEditor");
                return _editorAssembly;
            }
        }
        
        public static Assembly GetAssembly(string name)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly a = assemblies[i];
                if (a.GetName().Name == name)
                {
                    return a;
                }
            }

            return null;
        }
        
        public static Type GetEditorType(string name, string @namespace = "UnityEditor")
        {
            return editorAssembly.GetType(@namespace + "." + name);
        }
    }
}