#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GTALightClusterController))]
public sealed class GTALightClusterControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        var ctrl = (GTALightClusterController)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Preview / Bake", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            for (int i = 0; i < GTALightClusterController.MaxClusters; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{i}] {((GTALightClusterId)i)}", GUILayout.Width(100));
                EditorGUILayout.LabelField(ctrl.GetClusterStatus(i), EditorStyles.miniLabel);
                if (GUILayout.Button("Bake", GUILayout.Width(52)))
                {
                    Undo.RecordObject(ctrl, "Bake Light Cluster");
                    ctrl.BakeClusterNow((GTALightClusterId)i);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space(4);
        using (new EditorGUI.DisabledScope(ctrl.IsBaking && Application.isPlaying))
        {
            if (GUILayout.Button("Bake All Clusters", GUILayout.Height(32)))
            {
                Undo.RecordObject(ctrl, "Bake All Light Clusters");
                ctrl.BakeAllNow();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Edit Mode: сначала preview 8³, потом HQ (до 96³), лампы ближе к камере первыми. " +
                "Opacity = fade 0…1. Яркость — Intensity лампы (макс. 2).",
                MessageType.Info);
        }
    }
}
#endif
