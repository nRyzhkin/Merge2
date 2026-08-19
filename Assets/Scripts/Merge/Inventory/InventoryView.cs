using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class InventoryView : MonoBehaviour
    {
        [SerializeField] InventoryController controller;
        [SerializeField] InventorySlotView[] slots;
        [SerializeField] UIWindow window;

        InventoryBoardBridge _bridge;

        public UIWindow Window => window;
        public InventoryController Controller => controller;

        public void Bind(InventoryController inventory, InventorySlotView[] slotViews, UIWindow uiWindow)
        {
            controller = inventory;
            slots = slotViews;
            window = uiWindow;
            if (controller != null)
            {
                _bridge = controller.GetComponent<InventoryBoardBridge>();
            }
        }

        void OnEnable()
        {
            if (controller == null)
            {
                controller = InventoryController.Current;
            }

            if (window == null)
            {
                window = GetComponent<UIWindow>();
            }

            if (controller != null)
            {
                controller.Changed += Refresh;
            }

            Refresh();
        }

        void OnDisable()
        {
            if (controller != null)
            {
                controller.Changed -= Refresh;
            }
        }

        public void Refresh()
        {
            if (slots == null)
            {
                return;
            }

            var state = controller != null ? controller.State : null;
            var database = controller != null ? controller.ItemDatabase : null;
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                slots[i].Render(state != null ? state.GetSlot(i) : null, database);
            }
        }

        public void HandleSlotClicked(int slotIndex, InventorySlotView slotView)
        {
            if (_bridge == null)
            {
                _bridge = controller != null
                    ? controller.GetComponent<InventoryBoardBridge>()
                    : GetComponent<InventoryBoardBridge>();
            }

            _bridge?.TrySendSlotToBoard(slotIndex, slotView);
        }

        public RectTransform GetSlotRect(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
            {
                return null;
            }

            return slots[slotIndex].Rect;
        }
    }
}
