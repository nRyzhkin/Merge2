using UnityEngine;

namespace SanIsland.Merge
{
    /// <summary>
    /// Deterministic merge-test layout for Play Mode debugging only.
    /// Not a production new-game / FTUE board.
    /// </summary>
    public static class BoardDevelopmentStateFactory
    {
        public static BoardState Create(MergeItemDatabase database)
        {
            var state = new BoardState();
            Populate(state, database);
            return state;
        }

        public static void Populate(BoardState state, MergeItemDatabase database)
        {
            if (state == null || database == null)
            {
                return;
            }

            state.ClearAll();

            // Row 0 — adjacent L1 pairs + isolated box (move nearby should NOT open it)
            SetItem(state, database, 0, 0, "tools_l01");
            SetItem(state, database, 0, 1, "tools_l01");
            SetItem(state, database, 0, 3, "cleaning_l01");
            SetItem(state, database, 0, 4, "cleaning_l01");
            SetItem(state, database, 0, 6, "coffee_l01");
            SetItem(state, database, 0, 7, "coffee_l01");
            SetBox(state, 0, 9);

            // Row 1 — second L1 pairs for chained merge
            SetItem(state, database, 1, 0, "tools_l01");
            SetItem(state, database, 1, 1, "tools_l01");
            SetItem(state, database, 1, 3, "cleaning_l01");
            SetItem(state, database, 1, 4, "cleaning_l01");
            SetItem(state, database, 1, 6, "coffee_l01");
            SetItem(state, database, 1, 7, "coffee_l01");

            // Row 2 — L2 anchors + cobweb tools merge + cobweb-adjacent box
            SetItem(state, database, 2, 2, "tools_l02");
            SetItem(state, database, 2, 3, "tools_l02");
            SetItem(state, database, 2, 4, "cleaning_l02");
            SetItem(state, database, 2, 5, "tools_l01");
            SetLockedItem(state, database, 2, 6, "tools_l01");
            SetBox(state, 2, 7);

            // Row 3 — cobweb cleaning + 4-box cluster top / diagonal
            SetItem(state, database, 3, 1, "coffee_l01");
            SetItem(state, database, 3, 2, "coffee_l01");
            SetItem(state, database, 3, 4, "tools_l01");
            SetItem(state, database, 3, 6, "cleaning_l01");
            SetLockedItem(state, database, 3, 7, "cleaning_l01");
            SetBox(state, database, 3, 8, "tools_l01");
            SetBox(state, 3, 9);

            // Row 4 — generator pair (mergeable) + empties for production + box cluster
            SetBox(state, 4, 0);
            SetItem(state, database, 4, 2, "tools_g01");
            SetItem(state, database, 4, 3, "tools_g01");
            // (4,1)/(4,4)/(5,2)/(5,3) left empty for tools generator spawn tests

            //            BOX(tools_l01) at (3,8)
            // BOX(empty)=(4,7) - tools_l01=(4,8) - BOX(cleaning_l01)=(4,9)
            //            BOX(empty) at (5,8)
            // source tools_l01 at (4,6); diagonal BOX at (3,9) must NOT open.
            SetItem(state, database, 4, 6, "tools_l01");
            SetBox(state, 4, 7);
            SetItem(state, database, 4, 8, "tools_l01");
            SetBox(state, database, 4, 9, "cleaning_l01");
            SetBox(state, 5, 8);

            // Row 5 — cleaning generator with empty neighbors + sparse boxes
            SetBox(state, 5, 0);
            SetBox(state, 5, 1);
            SetItem(state, database, 5, 4, "cleaning_g01");
            // (5,3)/(5,5)/(4,4) empty around cleaning_g01 for production
        }

        public static void SetItem(BoardState state, MergeItemDatabase database, int row, int column, string internalKey)
        {
            SetItem(state, database, state.GetIndex(row, column), internalKey);
        }

        public static void SetItem(BoardState state, MergeItemDatabase database, int index, string internalKey)
        {
            if (!TryResolveId(database, internalKey, out var itemId))
            {
                return;
            }

            state.GetCell(index).SetItem(itemId, locked: false);
        }

        public static void SetLockedItem(BoardState state, MergeItemDatabase database, int row, int column, string internalKey)
        {
            SetLockedItem(state, database, state.GetIndex(row, column), internalKey);
        }

        public static void SetLockedItem(BoardState state, MergeItemDatabase database, int index, string internalKey)
        {
            if (!TryResolveId(database, internalKey, out var itemId))
            {
                return;
            }

            state.GetCell(index).SetItem(itemId, locked: true);
        }

        public static void SetBox(BoardState state, int row, int column, string concealedInternalKey, MergeItemDatabase database)
        {
            SetBox(state, database, row, column, concealedInternalKey);
        }

        public static void SetBox(BoardState state, MergeItemDatabase database, int row, int column, string concealedInternalKey)
        {
            if (!TryResolveId(database, concealedInternalKey, out var itemId))
            {
                return;
            }

            state.GetCell(state.GetIndex(row, column)).SetBox(itemId);
        }

        public static void SetBox(BoardState state, int row, int column)
        {
            SetBox(state, state.GetIndex(row, column));
        }

        public static void SetBox(BoardState state, int index)
        {
            state.GetCell(index).SetBox();
        }

        static bool TryResolveId(MergeItemDatabase database, string internalKey, out int itemId)
        {
            itemId = BoardCellState.EmptyItemId;
            if (!database.TryGetByKey(internalKey, out var data) || data == null)
            {
                Debug.LogError($"[BoardDevelopmentState] Item key '{internalKey}' was not found in MergeItemDatabase.");
                return false;
            }

            itemId = data.Id;
            return true;
        }
    }
}
