using System.Collections.Generic;

namespace SanIsland.Merge
{
    public class MergeDiscoveryState
    {
        readonly HashSet<int> _discoveredItemIds = new HashSet<int>();

        public int DiscoveredCount => _discoveredItemIds.Count;

        public bool IsDiscovered(int itemId)
        {
            return itemId != BoardCellState.EmptyItemId && _discoveredItemIds.Contains(itemId);
        }

        public bool Discover(int itemId)
        {
            if (itemId == BoardCellState.EmptyItemId)
            {
                return false;
            }

            return _discoveredItemIds.Add(itemId);
        }

        public void DiscoverFromBoard(BoardState state)
        {
            if (state == null)
            {
                return;
            }

            for (var i = 0; i < state.Cells.Count; i++)
            {
                var cell = state.Cells[i];
                if (cell != null && cell.HasItem)
                {
                    Discover(cell.ItemId);
                }
            }
        }

        public void Clear()
        {
            _discoveredItemIds.Clear();
        }
    }
}
