/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEngine;

namespace InfinityCode.RunDesiredScene
{
    public static class GUILayoutUtils
    {
        public static Rect lastRect;
        public static int buttonHash = "Button".GetHashCode();
        public static int hoveredButtonID;
        
        public static ButtonEvent Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            Rect rect = GUILayoutUtility.GetRect(content, style, options);
            lastRect = rect;
            return Button(rect, content, style);
        }

        public static ButtonEvent Button(Rect rect, GUIContent content, GUIStyle style)
        {
            int id = GUIUtility.GetControlID(buttonHash, FocusType.Passive, rect);

            Event e = Event.current;
            bool isHover = rect.Contains(e.mousePosition);
            bool hasMouseControl = GUIUtility.hotControl == id;

            if (e.type == EventType.Repaint)
            {
                style.Draw(rect, content, id, false, isHover);
                if (isHover) return ButtonEvent.hover;
            }
            else if (e.type == EventType.MouseDrag)
            {
                if (hasMouseControl)
                {
                    GUIUtility.hotControl = 0;
                    return ButtonEvent.drag;
                }
            }
            else if (e.type == EventType.MouseMove)
            {
                if (isHover)
                {
                    hoveredButtonID = id;
                    return ButtonEvent.hover;
                }
            }
            else if (e.type == EventType.MouseDown)
            {
                if (isHover)
                {
                    Debug.unityLogger.logEnabled = false;
                    try
                    {
                        GUIUtility.hotControl = id;
                    }
                    catch
                    {
                    }

                    Debug.unityLogger.logEnabled = true;
                    e.Use();
                    return ButtonEvent.press;
                }
            }
            else if (e.type == EventType.MouseUp)
            {
                if (hasMouseControl)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();

                    if (isHover)
                    {
                        GUI.changed = true;
                        return ButtonEvent.click;
                    }
                }

                return ButtonEvent.release;
            }

            return ButtonEvent.none;
        }
    }
}