using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    public sealed class InitialBoardValidationResult
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsValid => Errors.Count == 0;
    }

    public static class InitialBoardValidator
    {
        const int EmptyWarningThreshold = 40;
        const int GeneratorWarningThreshold = 4;
            const int MaxTierWarningThreshold = 16;

        public static InitialBoardValidationResult Validate(InitialBoardDefinition definition, MergeItemDatabase database)
        {
            var result = new InitialBoardValidationResult();
            if (definition == null)
            {
                result.Errors.Add("InitialBoardDefinition is missing.");
                return result;
            }

            definition.EnsureCells();
            var cells = definition.Cells;
            if (cells.Count != BoardState.CellCount)
            {
                result.Errors.Add($"Expected {BoardState.CellCount} cells, found {cells.Count}.");
            }

            var seen = new HashSet<int>();
            var emptyCount = 0;
            var generatorCount = 0;
            var maxTierCount = 0;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null)
                {
                    result.Errors.Add($"Cell {i} is null.");
                    continue;
                }

                if (!seen.Add(cell.index))
                {
                    result.Errors.Add($"Duplicate cell index {cell.index}.");
                }

                if (cell.index != i)
                {
                    result.Errors.Add($"Cell slot {i} has index {cell.index}.");
                }

                ValidateCell(cell, database, result, ref emptyCount, ref generatorCount, ref maxTierCount);
            }

            if (generatorCount != 1)
            {
                result.Errors.Add($"Initial board must contain exactly one generator (bakery_g01), found {generatorCount}.");
            }

            if (emptyCount >= EmptyWarningThreshold)
            {
                result.Warnings.Add($"Many empty cells ({emptyCount}/{BoardState.CellCount}).");
            }

            if (generatorCount >= GeneratorWarningThreshold)
            {
                result.Warnings.Add($"Many generators on the starting board ({generatorCount}).");
            }

            if (maxTierCount >= MaxTierWarningThreshold)
            {
                result.Warnings.Add($"Many max-tier items on the starting board ({maxTierCount}).");
            }

            return result;
        }

        static void ValidateCell(
            InitialBoardCellData cell,
            MergeItemDatabase database,
            InitialBoardValidationResult result,
            ref int emptyCount,
            ref int generatorCount,
            ref int maxTierCount)
        {
            switch (cell.state)
            {
                case CellInitialState.Empty:
                    emptyCount++;
                    break;
                case CellInitialState.Item:
                    ValidateVisibleItem(cell, database, result, MergeItemKind.Normal, "item", ref generatorCount, ref maxTierCount);
                    break;
                case CellInitialState.Generator:
                    ValidateVisibleItem(cell, database, result, MergeItemKind.Generator, "generator", ref generatorCount, ref maxTierCount);
                    break;
                case CellInitialState.CobwebItem:
                    ValidateCobwebContent(cell, cell.itemId, database, result, "cobweb item", ref generatorCount, ref maxTierCount);
                    break;
                case CellInitialState.Box:
                    ValidateBox(cell, database, result, ref generatorCount, ref maxTierCount);
                    break;
            }
        }

        static void ValidateVisibleItem(
            InitialBoardCellData cell,
            MergeItemDatabase database,
            InitialBoardValidationResult result,
            MergeItemKind expectedKind,
            string role,
            ref int generatorCount,
            ref int maxTierCount)
        {
            if (!TryGetItem(cell.itemId, database, cell.index, role, result, out var item))
            {
                return;
            }

            if (item.Kind != expectedKind)
            {
                result.Errors.Add($"Cell {cell.index} {role} '{item.InternalKey}' is {item.Kind}, expected {expectedKind}.");
            }

            if (item.Icon == null)
            {
                result.Errors.Add($"Cell {cell.index} {role} '{item.InternalKey}' has no sprite.");
            }

            if (item.Kind == MergeItemKind.Generator)
            {
                NoteGenerator(item, cell.index, result, ref generatorCount);
            }

            if (IsMaxTier(item, database))
            {
                maxTierCount++;
            }
        }

        static void ValidateBox(
            InitialBoardCellData cell,
            MergeItemDatabase database,
            InitialBoardValidationResult result,
            ref int generatorCount,
            ref int maxTierCount)
        {
            var hidden = cell.Hidden;
            switch (hidden.type)
            {
                case BoxRevealType.Empty:
                    result.Warnings.Add($"Cell {cell.index} box reveals empty. Early puzzle boards should hide a cobweb item.");
                    return;
                case BoxRevealType.GeneratorPartPlaceholder:
                    result.Warnings.Add($"Cell {cell.index} box hides a generator-part placeholder (reveals empty until that feature exists).");
                    return;
                case BoxRevealType.Item:
                    ValidateHiddenItem(cell, hidden.itemId, database, result, MergeItemKind.Normal, "box item", ref generatorCount, ref maxTierCount);
                    break;
                case BoxRevealType.CobwebItem:
                    ValidateCobwebContent(cell, hidden.itemId, database, result, "box cobweb item", ref generatorCount, ref maxTierCount);
                    break;
                case BoxRevealType.Generator:
                    ValidateHiddenItem(cell, hidden.itemId, database, result, MergeItemKind.Generator, "box generator", ref generatorCount, ref maxTierCount);
                    break;
            }
        }

        static void ValidateCobwebContent(
            InitialBoardCellData cell,
            int itemId,
            MergeItemDatabase database,
            InitialBoardValidationResult result,
            string role,
            ref int generatorCount,
            ref int maxTierCount)
        {
            if (!TryGetItem(itemId, database, cell.index, role, result, out var item))
            {
                return;
            }

            if (item.Kind != MergeItemKind.Normal && item.Kind != MergeItemKind.Generator)
            {
                result.Errors.Add($"Cell {cell.index} {role} '{item.InternalKey}' is {item.Kind}, expected Normal or Generator.");
            }

            if (item.Icon == null)
            {
                result.Errors.Add($"Cell {cell.index} {role} '{item.InternalKey}' has no sprite.");
            }

            if (item.Kind == MergeItemKind.Generator)
            {
                NoteGenerator(item, cell.index, result, ref generatorCount);
            }

            if (IsMaxTier(item, database))
            {
                maxTierCount++;
            }
        }

        static void ValidateHiddenItem(
            InitialBoardCellData cell,
            int itemId,
            MergeItemDatabase database,
            InitialBoardValidationResult result,
            MergeItemKind expectedKind,
            string role,
            ref int generatorCount,
            ref int maxTierCount)
        {
            if (!TryGetItem(itemId, database, cell.index, role, result, out var item))
            {
                return;
            }

            if (item.Kind != expectedKind)
            {
                result.Errors.Add($"Cell {cell.index} {role} '{item.InternalKey}' is {item.Kind}, expected {expectedKind}.");
            }

            if (item.Icon == null)
            {
                result.Errors.Add($"Cell {cell.index} {role} '{item.InternalKey}' has no sprite.");
            }

            if (item.Kind == MergeItemKind.Generator)
            {
                NoteGenerator(item, cell.index, result, ref generatorCount);
            }

            if (IsMaxTier(item, database))
            {
                maxTierCount++;
            }
        }

        static void NoteGenerator(
            MergeItemData item,
            int cellIndex,
            InitialBoardValidationResult result,
            ref int generatorCount)
        {
            generatorCount++;
            if (item != null && item.InternalKey != "bakery_g01")
            {
                result.Errors.Add($"Cell {cellIndex} has generator '{item.InternalKey}'. Initial board may only contain bakery_g01.");
            }
        }

        static bool TryGetItem(
            int itemId,
            MergeItemDatabase database,
            int cellIndex,
            string role,
            InitialBoardValidationResult result,
            out MergeItemData item)
        {
            item = null;
            if (itemId == BoardCellState.EmptyItemId)
            {
                result.Errors.Add($"Cell {cellIndex} {role} has no item id.");
                return false;
            }

            if (database == null)
            {
                result.Errors.Add("MergeItemDatabase is missing.");
                return false;
            }

            if (!database.TryGetById(itemId, out item) || item == null)
            {
                result.Errors.Add($"Cell {cellIndex} {role} id {itemId} is not in MergeItemDatabase.");
                return false;
            }

            return true;
        }

        static bool IsMaxTier(MergeItemData item, MergeItemDatabase database)
        {
            if (item == null || database == null || item.Kind != MergeItemKind.Normal)
            {
                return false;
            }

            var chain = database.GetNormalChain(item.Family);
            return chain != null && chain.Count > 0 && chain[chain.Count - 1].Id == item.Id;
        }
    }
}
