using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class InventoryBoardBridge : MonoBehaviour
    {
        [SerializeField] InventoryController inventory;
        [SerializeField] BoardController boardController;
        [SerializeField] InventoryView inventoryView;
        [SerializeField] InventoryFlightPresenter flightPresenter;

        public void Configure(
            InventoryController inventoryController,
            BoardController board,
            InventoryView view,
            InventoryFlightPresenter flight)
        {
            inventory = inventoryController;
            boardController = board;
            inventoryView = view;
            flightPresenter = flight;
        }

        public void TrySendSlotToBoard(int slotIndex, InventorySlotView slotView)
        {
            if (inventory == null)
            {
                inventory = InventoryController.Current;
            }

            if (boardController == null)
            {
                boardController = GetComponent<BoardController>();
            }

            if (inventory == null || boardController == null)
            {
                return;
            }

            var slot = inventory.State.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                return;
            }

            var pointer = slotView != null ? (Vector2)RectTransformUtility.WorldToScreenPoint(null, slotView.transform.position) : Vector2.zero;
            var emptyIndex = boardController.FindRandomEmptyOpenCell();
            if (emptyIndex == BoardController.NoSelectionIndex)
            {
                boardController.EnsureMessagesReady();
                boardController.MessagePresenter?.ShowBoardFull(pointer);
                return;
            }

            if (!inventory.TryTake(slotIndex, out var itemId, out var generatorInstanceId))
            {
                return;
            }

            if (!boardController.TryPlaceInventoryItem(itemId, emptyIndex, out var lockToken, generatorInstanceId))
            {
                inventory.TryAdd(itemId, generatorInstanceId);
                boardController.EnsureMessagesReady();
                boardController.MessagePresenter?.ShowBoardFull(pointer);
                return;
            }

            var closeInventory = inventory.State.OccupiedCount == 0;
            if (flightPresenter == null)
            {
                flightPresenter = GetComponent<InventoryFlightPresenter>();
            }

            Sprite sprite = null;
            var size = new Vector2(170f, 170f);
            if (inventory.ItemDatabase != null && inventory.ItemDatabase.TryGetById(itemId, out var data) && data != null)
            {
                sprite = data.Icon;
            }

            var start = Vector2.zero;
            if (boardController.DragView != null && slotView != null)
            {
                start = boardController.DragView.WorldToLayer(slotView.transform.position);
            }

            var window = inventoryView != null ? inventoryView.Window : null;
            if (flightPresenter != null)
            {
                flightPresenter.Play(sprite, size, start, emptyIndex, lockToken, closeInventory, window);
                return;
            }

            boardController.ReleaseGeneratorSpawnLock(lockToken);
            if (closeInventory && window != null && UIManager.Instance != null)
            {
                UIManager.Instance.CloseWindow(window);
            }
        }
    }
}
