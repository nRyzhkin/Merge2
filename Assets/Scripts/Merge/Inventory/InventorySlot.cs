using System;

namespace SanIsland.Merge
{
    [Serializable]
    public class InventorySlot
    {
        public int itemId = BoardCellState.EmptyItemId;
        public int generatorInstanceId = BoardCellState.NoGeneratorInstanceId;
        public bool locked;
        public bool offer;

        public bool IsEmpty => itemId == BoardCellState.EmptyItemId;

        public void Clear()
        {
            itemId = BoardCellState.EmptyItemId;
            generatorInstanceId = BoardCellState.NoGeneratorInstanceId;
            locked = false;
            offer = false;
        }
    }
}
