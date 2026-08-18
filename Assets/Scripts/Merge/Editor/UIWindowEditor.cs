using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    [CustomEditor(typeof(UIWindow))]
    public class UIWindowEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var window = (UIWindow)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Choreography Preview", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Preview Open"))
                {
                    var choreography = window.Choreography;
                    if (UIManager.Instance != null)
                    {
                        window.Configure(UIManager.Instance.DefaultWindowChoreographyConfig);
                    }

                    choreography.SnapOpenStart();
                    if (!window.gameObject.activeSelf)
                    {
                        window.gameObject.SetActive(true);
                    }

                    choreography.PlayOpen();
                }

                if (GUILayout.Button("Preview Close"))
                {
                    window.Choreography.PlayClose(null);
                }

                if (GUILayout.Button("Reset Visual State"))
                {
                    window.Choreography.ResetToDesigner();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Preview buttons are available in Play Mode and do not change the window stack.", MessageType.Info);
            }
        }
    }
}
