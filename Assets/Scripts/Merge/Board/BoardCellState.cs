using System;

namespace SanIsland.Merge
{
    [Serializable]
    public class BoardCellState
    {
        public const int EmptyItemId = -1;
        public const int NoGeneratorInstanceId = 0;

        public int Index;
        public int ItemId = EmptyItemId;
        public int GeneratorInstanceId = NoGeneratorInstanceId;
        public CellBlockType BlockType = CellBlockType.None;
        public int ConcealedItemId = EmptyItemId;
        public bool ItemLocked;
        public bool ConcealedItemLocked;
        public int BoxVisualIndex = -1;

        public bool HasItem => ItemId != EmptyItemId;
        public bool IsEmpty => BlockType == CellBlockType.None && !HasItem;
        public bool IsBox => BlockType == CellBlockType.Box;
        public bool HasConcealedItem => ConcealedItemId != EmptyItemId;

        public int ResolveBoxVisualIndex()
        {
            return BoxVisualIndex >= 0 ? BoxVisualIndex : Index;
        }

        public void Clear()
        {
            ItemId = EmptyItemId;
            GeneratorInstanceId = NoGeneratorInstanceId;
            BlockType = CellBlockType.None;
            ConcealedItemId = EmptyItemId;
            ItemLocked = false;
            ConcealedItemLocked = false;
            BoxVisualIndex = -1;
        }

        public void SetItem(int itemId, bool locked = false)
        {
            BlockType = CellBlockType.None;
            ItemId = itemId;
            ConcealedItemId = EmptyItemId;
            ConcealedItemLocked = false;
            ItemLocked = locked;
        }

        public void SetBox(int concealedItemId = EmptyItemId, bool concealedLocked = false)
        {
            ItemId = EmptyItemId;
            ItemLocked = false;
            BlockType = CellBlockType.Box;
            ConcealedItemId = concealedItemId;
            ConcealedItemLocked = concealedItemId != EmptyItemId && concealedLocked;
        }

        public int RevealBox()
        {
            var revealed = ConcealedItemId;
            var locked = ConcealedItemLocked && revealed != EmptyItemId;
            BlockType = CellBlockType.None;
            ConcealedItemId = EmptyItemId;
            ConcealedItemLocked = false;
            ItemLocked = locked;
            ItemId = revealed;
            return revealed;
        }
    }
}
