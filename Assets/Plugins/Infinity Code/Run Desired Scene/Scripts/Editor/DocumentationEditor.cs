/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEditor;
using UnityEngine;

namespace InfinityCode.RunDesiredScene
{
    [CustomEditor(typeof(Documentation))]
    public class DocumentationEditor : Editor
    {
        private static void DrawDocumentation()
        {
            if (GUILayout.Button("Local Documentation"))
            {
                Links.OpenLocalDocumentation();
            }

            if (GUILayout.Button("Online Documentation"))
            {
                Links.OpenDocumentation();
            }

            GUILayout.Space(10);
        }

        private static void DrawExtra()
        {
            if (GUILayout.Button("Changelog"))
            {
                Links.OpenChangelog();
            }

            GUILayout.Space(10);
        }

        private new static void DrawHeader()
        {
            GUILayout.Label("Run Desired Scene", Styles.centeredLabel);
            GUILayout.Label("version: " + Version.version, Styles.centeredLabel);
            GUILayout.Space(10);
        }

        private void DrawRateAndReview()
        {
            EditorGUILayout.HelpBox("Please don't forget to leave a review on the store page if you liked Run Desired Scene, this helps us a lot!", MessageType.Warning);

            if (GUILayout.Button("Rate & Review"))
            {
                Links.OpenReviews();
            }
        }

        private void DrawSupport()
        {
            if (GUILayout.Button("Support"))
            {
                Links.OpenSupport();
            }

            if (GUILayout.Button("Discord"))
            {
                Links.OpenDiscord();
            }

            if (GUILayout.Button("Forum"))
            {
                Links.OpenForum();
            }

            GUILayout.Space(10);
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            DrawDocumentation();
            DrawExtra();
            DrawSupport();
            DrawRateAndReview();
        }
    }
}