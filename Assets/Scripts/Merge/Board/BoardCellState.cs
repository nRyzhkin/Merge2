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
        public bool ItemLocked;

        public bool HasItem => ItemId != EmptyItemId;
        public bool IsEmpty => BlockType == CellBlockType.None && !HasItem;
        public bool IsBox => BlockType == CellBlockType.Box;

        public void Clear()
        {
            ItemId = EmptyItemId;
            BlockType = CellBlockType.None;
            ItemLocked = false;
        }

        public void SetItem(int itemId, bool locked = false)
        {
            BlockType = CellBlockType.None;
            ItemId = itemId;
            ItemLocked = locked;
        }

        public void SetBox()
        {
            ItemId = EmptyItemId;
            ItemLocked = false;
            BlockType = CellBlockType.Box;
        }
    }
}
