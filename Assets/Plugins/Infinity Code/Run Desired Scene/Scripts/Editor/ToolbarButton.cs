/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEditor;
using UnityEngine;

namespace InfinityCode.RunDesiredScene
{
    [InitializeOnLoad]
    public static class ToolbarButton
    {
        static ToolbarButton()
        {
            ToolbarManager.AddLeftToolbar("RunDesiredScene", DrawButton, 1000);
        }

        private static void DrawButton()
        {
            if (EditorApplication.isPlaying) return;
            
            GUIContent content = TempContent.Get(SceneManager.GetStartSceneName());
            if (GUILayoutUtils.Button(content, EditorStyles.toolbarDropDown) == ButtonEvent.click)
            {
                Rect rect = GUILayoutUtils.lastRect;
                SelectSceneWindow.ShowWindow(rect.position + new Vector2(0, rect.height + 5));
            }
        }
    }
}
