using UnityEngine;

namespace SanIsland.Merge
{
    public static class BoardLayoutValidator
    {
        public static void Validate(BoardState state, MergeItemDatabase database)
        {
            if (state == null)
            {
                return;
            }

            for (var i = 0; i < BoardState.CellCount; i++)
            {
                var cell = state.GetCell(i);
                if (cell == null)
                {
                    continue;
                }

                if (cell.IsBox)
                {
                    if (cell.HasItem)
                    {
                        Debug.LogWarning($"[Board] Box at {i} also has itemId={cell.ItemId}.");
                    }

                    if (cell.ItemLocked)
                    {
                        Debug.LogWarning($"[Board] Box at {i} also has itemLocked. Boxes cannot be cobwebbed.");
                    }

                    if (cell.HasConcealedItem && database != null &&
                        !database.TryGetById(cell.ConcealedItemId, out _))
                    {
                        Debug.LogError($"[Board] Box at {i} concealedItemId={cell.ConcealedItemId} is missing from MergeItemDatabase.");
                    }
                }
                else if (cell.HasConcealedItem)
                {
                    Debug.LogWarning($"[Board] Non-box cell {i} has concealedItemId={cell.ConcealedItemId}.");
                }
            }
        }
    }
}
