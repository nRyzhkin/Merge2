using System.Collections.Generic;

namespace SanIsland.Merge
{
    /// <summary>
    /// Shared merge / cobweb / box-reveal rules used by gameplay and editor simulation.
    /// </summary>
    public static class BoardProgressionRules
    {
        public static bool TryGetMergeResultItemId(
            BoardState state,
            MergeItemDatabase itemDatabase,
            int fromIndex,
            int toIndex,
            out int resultItemId)
        {
            resultItemId = BoardCellState.EmptyItemId;
            if (state == null || itemDatabase == null || fromIndex == toIndex)
            {
                return false;
            }

            if (!state.IsValidIndex(fromIndex) || !state.IsValidIndex(toIndex))
            {
                return false;
            }

            var source = state.GetCell(fromIndex);
            var target = state.GetCell(toIndex);
            if (source == null || target == null || !source.HasItem || !target.HasItem)
            {
                return false;
            }

            if (source.IsBox || target.IsBox || source.ItemLocked)
            {
                return false;
            }

            if (source.ItemId != target.ItemId)
            {
                return false;
            }

            if (!itemDatabase.TryGetById(source.ItemId, out var data) || data == null)
            {
                return false;
            }

            if (data.NextItemId == MergeItemIdUtility.NoNextItemId)
            {
                return false;
            }

            resultItemId = data.NextItemId;
            return true;
        }

        public static bool TryValidateCobwebUnlock(BoardState state, int fromIndex, int lockedTargetIndex, out int itemId)
        {
            itemId = BoardCellState.EmptyItemId;
            if (state == null || fromIndex == lockedTargetIndex)
            {
                return false;
            }

            if (!state.IsValidIndex(fromIndex) || !state.IsValidIndex(lockedTargetIndex))
            {
                return false;
            }

            var source = state.GetCell(fromIndex);
            var target = state.GetCell(lockedTargetIndex);
            if (source == null || target == null || !source.HasItem || !target.HasItem)
            {
                return false;
            }

            if (source.IsBox || target.IsBox || source.ItemLocked || !target.ItemLocked)
            {
                return false;
            }

            if (source.ItemId != target.ItemId)
            {
                return false;
            }

            itemId = source.ItemId;
            return true;
        }

        public static void RevealOrthogonalBoxes(
            BoardState state,
            int mergeResultIndex,
            List<int> neighborScratch,
            List<BoxRevealResult> results)
        {
            results?.Clear();
            if (state == null || neighborScratch == null || results == null || !state.IsValidIndex(mergeResultIndex))
            {
                return;
            }

            state.GetOrthogonalNeighborIndices(mergeResultIndex, neighborScratch);
            for (var i = 0; i < neighborScratch.Count; i++)
            {
                var index = neighborScratch[i];
                var cell = state.GetCell(index);
                if (cell == null || !cell.IsBox)
                {
                    continue;
                }

                var revealedId = cell.RevealBox();
                results.Add(new BoxRevealResult
                {
                    CellIndex = index,
                    RevealedItemId = revealedId
                });
            }
        }

        public static bool TryApplyMerge(
            BoardState state,
            MergeItemDatabase itemDatabase,
            int fromIndex,
            int toIndex,
            List<int> neighborScratch,
            List<BoxRevealResult> reveals,
            out int resultItemId)
        {
            if (!TryGetMergeResultItemId(state, itemDatabase, fromIndex, toIndex, out resultItemId))
            {
                return false;
            }

            var source = state.GetCell(fromIndex);
            var target = state.GetCell(toIndex);
            source.Clear();
            target.SetItem(resultItemId, locked: false);
            RevealOrthogonalBoxes(state, toIndex, neighborScratch, reveals);
            return true;
        }
    }
}
