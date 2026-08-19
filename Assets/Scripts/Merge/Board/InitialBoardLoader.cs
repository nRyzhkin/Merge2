using UnityEngine;

namespace SanIsland.Merge
{
    public static class InitialBoardLoader
    {
        public static BoardState CreateNewGameBoard(InitialBoardDefinition definition, MergeItemDatabase database)
        {
            var state = new BoardState();
            Apply(definition, database, state);
            return state;
        }

        public static void Apply(InitialBoardDefinition definition, MergeItemDatabase database, BoardState state)
        {
            if (state == null)
            {
                return;
            }

            state.ClearAll();
            if (definition == null)
            {
                Debug.LogError("[InitialBoard] Definition is missing. Board stays empty.");
                return;
            }

            definition.EnsureCells();
            for (var i = 0; i < BoardState.CellCount; i++)
            {
                ApplyCell(state.GetCell(i), definition.GetCell(i), database);
            }
        }

        static void ApplyCell(BoardCellState runtime, InitialBoardCellData authored, MergeItemDatabase database)
        {
            if (runtime == null)
            {
                return;
            }

            runtime.Clear();
            runtime.Index = authored != null ? authored.index : runtime.Index;
            if (authored == null)
            {
                return;
            }

            runtime.BoxVisualIndex = authored.boxVisualIndex;
            switch (authored.state)
            {
                case CellInitialState.Item:
                    runtime.SetItem(ResolveItemId(authored.itemId, database, authored.index, "item"), locked: false);
                    break;
                case CellInitialState.Generator:
                    runtime.SetItem(ResolveItemId(authored.itemId, database, authored.index, "generator"), locked: false);
                    break;
                case CellInitialState.CobwebItem:
                    runtime.SetItem(ResolveItemId(authored.itemId, database, authored.index, "cobweb item"), locked: true);
                    break;
                case CellInitialState.Box:
                    ApplyBox(runtime, authored, database);
                    break;
                default:
                    runtime.Clear();
                    runtime.Index = authored.index;
                    runtime.BoxVisualIndex = authored.boxVisualIndex;
                    break;
            }
        }

        static void ApplyBox(BoardCellState runtime, InitialBoardCellData authored, MergeItemDatabase database)
        {
            var hidden = authored.Hidden;
            var concealedId = BoardCellState.EmptyItemId;
            var concealedLocked = false;
            switch (hidden.type)
            {
                case BoxRevealType.Item:
                    concealedId = ResolveItemId(hidden.itemId, database, authored.index, "box item");
                    break;
                case BoxRevealType.CobwebItem:
                    concealedId = ResolveItemId(hidden.itemId, database, authored.index, "box cobweb item");
                    concealedLocked = concealedId != BoardCellState.EmptyItemId;
                    break;
                case BoxRevealType.Generator:
                    concealedId = ResolveItemId(hidden.itemId, database, authored.index, "box generator");
                    break;
                case BoxRevealType.GeneratorPartPlaceholder:
                    concealedId = BoardCellState.EmptyItemId;
                    break;
            }

            runtime.SetBox(concealedId, concealedLocked);
            runtime.BoxVisualIndex = authored.boxVisualIndex;
        }

        static int ResolveItemId(int itemId, MergeItemDatabase database, int cellIndex, string role)
        {
            if (itemId == BoardCellState.EmptyItemId)
            {
                Debug.LogError($"[InitialBoard] Cell {cellIndex} {role} is missing an item id.");
                return BoardCellState.EmptyItemId;
            }

            if (database != null && !database.TryGetById(itemId, out _))
            {
                Debug.LogError($"[InitialBoard] Cell {cellIndex} {role} id {itemId} is not in MergeItemDatabase.");
                return BoardCellState.EmptyItemId;
            }

            return itemId;
        }
    }
}
