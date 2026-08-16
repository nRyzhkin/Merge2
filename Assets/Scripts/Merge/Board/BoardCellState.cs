using System;

namespace SanIsland.Merge
{
    [Serializable]
    public class BoardCellState
    {
        public const int EmptyItemId = -1;

        public int Index;
        public int ItemId = EmptyItemId;
        public CellBlockType BlockType = CellBlockType.None;
        public int ConcealedItemId = EmptyItemId;
        public bool ItemLocked;

        public bool HasItem => ItemId != EmptyItemId;
        public bool IsEmpty => BlockType == CellBlockType.None && !HasItem;
        public bool IsBox => BlockType == CellBlockType.Box;
        public bool HasConcealedItem => ConcealedItemId != EmptyItemId;

        public void Clear()
        {
            ItemId = EmptyItemId;
            BlockType = CellBlockType.None;
            ConcealedItemId = EmptyItemId;
            ItemLocked = false;
        }

        public void SetItem(int itemId, bool locked = false)
        {
            BlockType = CellBlockType.None;
            ItemId = itemId;
            ConcealedItemId = EmptyItemId;
            ItemLocked = locked;
        }

        public void SetBox(int concealedItemId = EmptyItemId)
        {
            ItemId = EmptyItemId;
            ItemLocked = false;
            BlockType = CellBlockType.Box;
            ConcealedItemId = concealedItemId;
        }

        public int RevealBox()
        {
            var revealed = ConcealedItemId;
            BlockType = CellBlockType.None;
            ConcealedItemId = EmptyItemId;
            ItemLocked = false;
            ItemId = revealed;
            return revealed;
        }
    }
}
