using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    [CustomPropertyDrawer(typeof(OrderRequirement))]
    public class OrderRequirementDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var itemId = property.FindPropertyRelative("itemId");
            var amount = property.FindPropertyRelative("amount");
            var labels = CollectItemLabels(out var ids);
            var currentId = itemId != null ? itemId.intValue : 0;
            var index = 0;
            for (var i = 0; i < ids.Length; i++)
            {
                if (ids[i] == currentId)
                {
                    index = i;
                    break;
                }
            }

            EditorGUI.BeginProperty(position, label, property);
            var itemRect = new Rect(position.x, position.y, position.width - 72f, position.height);
            var amountRect = new Rect(position.xMax - 68f, position.y, 68f, position.height);
            var next = EditorGUI.Popup(itemRect, index, labels);
            if (next != index && next >= 0 && next < ids.Length && itemId != null)
            {
                itemId.intValue = ids[next];
            }

            if (amount != null)
            {
                EditorGUI.BeginChangeCheck();
                var value = EditorGUI.IntField(amountRect, amount.intValue);
                if (EditorGUI.EndChangeCheck())
                {
                    amount.intValue = Mathf.Max(1, value);
                }
            }

            EditorGUI.EndProperty();
        }

        static string[] CollectItemLabels(out int[] ids)
        {
            var database = AssetDatabase.LoadAssetAtPath<MergeItemDatabase>(MergeBoardSetupTool.DatabasePath);
            if (database == null || database.Items == null || database.Items.Count == 0)
            {
                ids = new[] { 0 };
                return new[] { "(no MergeItemDatabase)" };
            }

            var count = database.Items.Count + 1;
            var labels = new string[count];
            ids = new int[count];
            labels[0] = "(none)";
            ids[0] = 0;
            for (var i = 0; i < database.Items.Count; i++)
            {
                var item = database.Items[i];
                if (item == null)
                {
                    labels[i + 1] = "(null)";
                    ids[i + 1] = 0;
                    continue;
                }

                labels[i + 1] = $"{item.InternalKey} ({item.Id})";
                ids[i + 1] = item.Id;
            }

            return labels;
        }
    }
}
