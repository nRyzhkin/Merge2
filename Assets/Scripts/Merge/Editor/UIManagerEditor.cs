using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    [CustomEditor(typeof(UIManager))]
    public class UIManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var manager = (UIManager)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Window Stack Debug", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Open Windows Count", manager.OpenWindowCount);
                var top = manager.TopWindow;
                EditorGUILayout.TextField("Top Window", top != null ? top.name : string.Empty);
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Close Top Window"))
                {
                    manager.CloseTopWindow();
                }

                if (GUILayout.Button("Close All Windows"))
                {
                    manager.CloseAllWindows();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Window stack debug buttons are available in Play Mode.", MessageType.Info);
            }
        }
    }
}
