using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    public static class MergeBoardTask17Setup
    {
        [MenuItem("Tools/San Island/Setup Inventory (Existing UI)")]
        public static void SetupFromMenu()
        {
            var controller = Object.FindAnyObjectByType<BoardController>();
            if (controller == null)
            {
                Debug.LogError("[TASK17C] BoardController not found in the open scene.");
                return;
            }

            WireExisting(controller, controller.ItemInfoView);
        }

        public static void WireExisting(BoardController controller, ItemInfoView itemInfoView)
        {
            InventoryExistingUiBinder.Bind(controller, itemInfoView, force: true);
            if (controller != null)
            {
                EditorUtility.SetDirty(controller);
            }

            if (itemInfoView != null)
            {
                EditorUtility.SetDirty(itemInfoView);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
}
