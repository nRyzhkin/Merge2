using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    [CustomEditor(typeof(MergeItemDatabase))]
    public class MergeItemDatabaseEditor : UnityEditor.Editor
    {
        Vector2 _scroll;

        public override void OnInspectorGUI()
        {
            var database = (MergeItemDatabase)target;
            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Merge Item Database"))
            {
                MergeItemDatabaseImporter.Rebuild();
                serializedObject.Update();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Records", database.Items.Count.ToString());
            DrawHeaderRow();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(240f));
            EditorGUI.BeginDisabledGroup(true);
            for (var i = 0; i < database.Items.Count; i++)
            {
                var item = database.Items[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.IntField(item.Id, GUILayout.Width(56));
                EditorGUILayout.TextField(item.InternalKey ?? string.Empty, GUILayout.Width(140));
                EditorGUILayout.EnumPopup(item.Family, GUILayout.Width(90));
                EditorGUILayout.EnumPopup(item.Kind, GUILayout.Width(90));
                EditorGUILayout.IntField(item.Level, GUILayout.Width(36));
                EditorGUILayout.ObjectField(item.Icon, typeof(Sprite), false, GUILayout.Width(120));
                EditorGUILayout.TextField(item.LocalizationKey ?? string.Empty, GUILayout.MinWidth(160));
                EditorGUILayout.IntField(item.NextItemId, GUILayout.Width(56));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndScrollView();
        }

        static void DrawHeaderRow()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID", GUILayout.Width(56));
            EditorGUILayout.LabelField("Key", GUILayout.Width(140));
            EditorGUILayout.LabelField("Family", GUILayout.Width(90));
            EditorGUILayout.LabelField("Kind", GUILayout.Width(90));
            EditorGUILayout.LabelField("Lv", GUILayout.Width(36));
            EditorGUILayout.LabelField("Sprite", GUILayout.Width(120));
            EditorGUILayout.LabelField("Localization Key", GUILayout.MinWidth(160));
            EditorGUILayout.LabelField("Next", GUILayout.Width(56));
            EditorGUILayout.EndHorizontal();
        }
    }
}
