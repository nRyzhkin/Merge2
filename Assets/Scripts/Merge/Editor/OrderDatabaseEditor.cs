using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    [CustomEditor(typeof(OrderDatabase))]
    public class OrderDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var database = (OrderDatabase)target;
            EditorGUILayout.HelpBox(
                "Order IDs are persistent content IDs. Do not renumber published orders. Runtime stores int IDs, not list indices.",
                MessageType.Info);

            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Fill Development Orders If Empty"))
            {
                var items = AssetDatabase.LoadAssetAtPath<MergeItemDatabase>(MergeBoardSetupTool.DatabasePath);
                Undo.RecordObject(database, "Fill Development Orders");
                database.EnsureDevelopmentOrders(items);
                EditorUtility.SetDirty(database);
            }
        }
    }
}
