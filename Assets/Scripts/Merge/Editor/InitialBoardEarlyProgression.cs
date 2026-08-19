using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    public static class InitialBoardEarlyProgression
    {
        public static InitialBoardValidationResult Validate(
            InitialBoardDefinition definition,
            MergeItemDatabase database,
            GeneratorProductionDatabase production)
        {
            var result = InitialBoardValidator.Validate(definition, database);
            ValidateStaticLayout(definition, database, result);
            SimulateScriptedSequence(definition, database, production, result);
            return result;
        }

        public static string FormatReport(InitialBoardValidationResult result)
        {
            var lines = new StringBuilder();
            if (result.IsValid && result.Warnings.Count == 0)
            {
                lines.AppendLine("Early progression is valid.");
            }

            for (var i = 0; i < result.Errors.Count; i++)
            {
                lines.AppendLine("Error: " + result.Errors[i]);
            }

            for (var i = 0; i < result.Warnings.Count; i++)
            {
                lines.AppendLine("Warning: " + result.Warnings[i]);
            }

            return lines.ToString();
        }

        static void ValidateStaticLayout(
            InitialBoardDefinition definition,
            MergeItemDatabase database,
            InitialBoardValidationResult result)
        {
            if (definition == null)
            {
                return;
            }

            definition.EnsureCells();
            var empty = 0;
            var movable = 0;
            var generatorBoxes = 0;
            var visibleCobweb = 0;
            var boxes = 0;
            var coffee = 0;
            var bakeryLocked = 0;
            var lockedNonGen = 0;
            var highTier = 0;
            var bakeryG1 = 0;
            var cleaningG1 = 0;
            var toolsG1 = 0;
            for (var i = 0; i < definition.Cells.Count; i++)
            {
                var cell = definition.Cells[i];
                if (cell.state == CellInitialState.Empty)
                {
                    empty++;
                    continue;
                }

                if (cell.state == CellInitialState.Item && cell.itemId != BoardCellState.EmptyItemId)
                {
                    movable++;
                }

                if (cell.state == CellInitialState.CobwebItem)
                {
                    visibleCobweb++;
                    CountLockedItem(database, cell.itemId, result, ref bakeryLocked, ref lockedNonGen, ref highTier, ref coffee);
                }

                if (cell.state == CellInitialState.Box)
                {
                    boxes++;
                    if (cell.Hidden.type == BoxRevealType.Empty ||
                        cell.Hidden.type == BoxRevealType.Item ||
                        cell.Hidden.type == BoxRevealType.GeneratorPartPlaceholder)
                    {
                        result.Errors.Add($"Cell {cell.index} box must hide CobwebItem or a generator.");
                    }

                    if (cell.Hidden.type == BoxRevealType.Generator)
                    {
                        generatorBoxes++;
                        if (database != null &&
                            database.TryGetById(cell.Hidden.itemId, out var gen) &&
                            gen != null)
                        {
                            if (gen.Kind != MergeItemKind.Generator)
                            {
                                result.Errors.Add($"Cell {cell.index} generator box is not a generator.");
                            }
                            else if (gen.InternalKey == "bakery_g01")
                            {
                                bakeryG1++;
                            }
                            else if (gen.InternalKey == "cleaning_g01")
                            {
                                cleaningG1++;
                            }
                            else if (gen.InternalKey == "tools_g01")
                            {
                                toolsG1++;
                            }
                            else
                            {
                                result.Errors.Add($"Cell {cell.index} generator box must be bakery_g01, cleaning_g01, or tools_g01.");
                            }
                        }
                    }
                    else
                    {
                        CountLockedItem(database, cell.Hidden.itemId, result, ref bakeryLocked, ref lockedNonGen, ref highTier, ref coffee);
                    }
                }
            }

            if (empty != 0)
            {
                result.Errors.Add($"Start board has {empty} empty cells; expected 0.");
            }

            if (movable != 2)
            {
                result.Errors.Add($"Start board has {movable} movable items; expected 2.");
            }

            if (bakeryG1 != 1)
            {
                result.Errors.Add($"Expected one Bakery G1 box, found {bakeryG1}.");
            }

            if (cleaningG1 != 0 || toolsG1 != 0)
            {
                result.Errors.Add($"Initial board may not contain Cleaning/Tools generators (cleaning={cleaningG1}, tools={toolsG1}).");
            }

            ValidateDuplicates(definition, database, result);
            ValidateEdgeTiers(definition, database, result);
            ValidateCenterGradient(definition, database, result);

            result.Warnings.Add($"Census: movable={movable} boxes={boxes} visibleCobweb={visibleCobweb} generatorBoxes={generatorBoxes}.");
        }

        static void ValidateDuplicates(
            InitialBoardDefinition definition,
            MergeItemDatabase database,
            InitialBoardValidationResult result)
        {
            var counts = new Dictionary<int, int>();
            for (var i = 0; i < definition.Cells.Count; i++)
            {
                var id = ResolveContentId(definition.Cells[i]);
                if (id == BoardCellState.EmptyItemId)
                {
                    continue;
                }

                if (!counts.ContainsKey(id))
                {
                    counts[id] = 0;
                }

                counts[id]++;
            }

            foreach (var pair in counts)
            {
                if (pair.Value < 2)
                {
                    continue;
                }

                if (database != null &&
                    database.TryGetById(pair.Key, out var item) &&
                    item != null &&
                    item.InternalKey == "bakery_l01" &&
                    pair.Value == 2)
                {
                    continue;
                }

                var name = pair.Key.ToString();
                if (database != null && database.TryGetById(pair.Key, out var dup) && dup != null)
                {
                    name = dup.InternalKey;
                }

                result.Warnings.Add($"Duplicate '{name}' appears {pair.Value} times.");
            }
        }

        static void ValidateEdgeTiers(
            InitialBoardDefinition definition,
            MergeItemDatabase database,
            InitialBoardValidationResult result)
        {
            if (database == null)
            {
                return;
            }

            for (var i = 0; i < definition.Cells.Count; i++)
            {
                if (!IsEdgeIndex(i))
                {
                    continue;
                }

                var id = ResolveContentId(definition.Cells[i]);
                if (!database.TryGetById(id, out var item) || item == null || item.Kind != MergeItemKind.Normal)
                {
                    continue;
                }

                var max = MaxFamilyTier(item.Family);
                var tooLow = max <= 8 ? item.Level <= 3 : item.Level <= 4;
                if (tooLow)
                {
                    result.Warnings.Add($"Edge cell {i} '{item.InternalKey}' L{item.Level} is cheap for an edge/corner.");
                }
            }
        }

        static void ValidateCenterGradient(
            InitialBoardDefinition definition,
            MergeItemDatabase database,
            InitialBoardValidationResult result)
        {
            if (database == null)
            {
                return;
            }

            var bands = new[] { "center", "inner", "middle", "outer", "edge" };
            var sums = new float[5];
            var counts = new int[5];
            var lines = new StringBuilder();
            for (var i = 0; i < definition.Cells.Count; i++)
            {
                var id = ResolveContentId(definition.Cells[i]);
                if (!database.TryGetById(id, out var item) || item == null || item.Kind != MergeItemKind.Normal)
                {
                    continue;
                }

                var distance = DistanceFromHub(i);
                var band = DistanceBand(distance);
                sums[band] += item.Level;
                counts[band]++;
                lines.AppendLine($"  {i} {item.Family} L{item.Level} d={distance} {definition.Cells[i].state}");
            }

            var summary = new StringBuilder();
            summary.Append("Average tier by distance: ");
            for (var b = 0; b < bands.Length; b++)
            {
                if (b > 0)
                {
                    summary.Append(" | ");
                }

                var avg = counts[b] == 0 ? 0f : sums[b] / counts[b];
                summary.Append(bands[b]).Append('=').Append(avg.ToString("0.00"));
            }

            result.Warnings.Add(summary.ToString());
            Debug.Log("[InitialBoard] Cell census\n" + lines);
        }

        static int ResolveContentId(InitialBoardCellData cell)
        {
            if (cell == null)
            {
                return BoardCellState.EmptyItemId;
            }

            if (cell.state == CellInitialState.Box)
            {
                return cell.Hidden.itemId;
            }

            return cell.itemId;
        }

        static int DistanceFromHub(int index)
        {
            var row = index / BoardState.Columns;
            var col = index % BoardState.Columns;
            var best = int.MaxValue;
            for (var r = 2; r <= 3; r++)
            {
                for (var c = 4; c <= 5; c++)
                {
                    var d = Abs(row - r) + Abs(col - c);
                    if (d < best)
                    {
                        best = d;
                    }
                }
            }

            return best;
        }

        static int Abs(int value)
        {
            return value < 0 ? -value : value;
        }

        static int DistanceBand(int distance)
        {
            if (distance <= 0)
            {
                return 0;
            }

            if (distance == 1)
            {
                return 1;
            }

            if (distance == 2)
            {
                return 2;
            }

            if (distance <= 4)
            {
                return 3;
            }

            return 4;
        }

        static bool IsEdgeIndex(int index)
        {
            var row = index / BoardState.Columns;
            var col = index % BoardState.Columns;
            return row == 0 || row == BoardState.Rows - 1 || col == 0 || col == BoardState.Columns - 1;
        }

        static int MaxFamilyTier(MergeItemFamily family)
        {
            switch (family)
            {
                case MergeItemFamily.Cleaning:
                case MergeItemFamily.Tools:
                    return 8;
                default:
                    return 12;
            }
        }

        static void CountLockedItem(
            MergeItemDatabase database,
            int itemId,
            InitialBoardValidationResult result,
            ref int bakeryLocked,
            ref int lockedNonGen,
            ref int highTier,
            ref int coffee)
        {
            lockedNonGen++;
            if (database == null || !database.TryGetById(itemId, out var item) || item == null)
            {
                return;
            }

            if (item.Family == MergeItemFamily.Bakery)
            {
                bakeryLocked++;
            }

            if (item.Family == MergeItemFamily.Coffee)
            {
                coffee++;
            }

            if (item.Kind == MergeItemKind.Normal && item.Level >= 4)
            {
                highTier++;
            }
        }

        static void SimulateScriptedSequence(
            InitialBoardDefinition definition,
            MergeItemDatabase database,
            GeneratorProductionDatabase production,
            InitialBoardValidationResult result)
        {
            if (definition == null || database == null)
            {
                return;
            }

            var state = InitialBoardLoader.CreateNewGameBoard(definition, database);
            var neighbors = new List<int>(4);
            var reveals = new List<BoxRevealResult>(4);
            var mergeCount = 0;
            var g1Merge = -1;
            var steps = InitialBoardPuzzleLayout.EarlySteps;
            if (steps.Length < 3)
            {
                result.Errors.Add($"Scripted sequence has {steps.Length} actions; expected the first-minutes path.");
            }

            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                if (step.Produce)
                {
                    SimulateProduce(state, database, production, step, i, result);
                    continue;
                }

                if (!BoardProgressionRules.TryApplyMerge(
                        state,
                        database,
                        step.FromIndex,
                        step.ToIndex,
                        neighbors,
                        reveals,
                        out var resultId))
                {
                    result.Errors.Add($"Step {i + 1}: merge {step.FromIndex} → {step.ToIndex} is illegal ({step.Note}).");
                    return;
                }

                if (!MatchesKey(database, resultId, step.ResultKey))
                {
                    result.Errors.Add($"Step {i + 1}: merge produced id {resultId}, expected '{step.ResultKey}'.");
                    return;
                }

                mergeCount++;
                if (HasUnlockedGenerator(state, database, "bakery_g01") && g1Merge < 0)
                {
                    g1Merge = mergeCount;
                }

                if (mergeCount == 1 || mergeCount == 3 || mergeCount == 5 || mergeCount == 10)
                {
                    result.Warnings.Add($"After {mergeCount} merge(s): {CountEmpty(state)} empty cells.");
                }
            }

            if (mergeCount < 3)
            {
                result.Errors.Add($"Scripted sequence has {mergeCount} merges; expected the first-minutes Bakery path.");
            }

            var boxesLeft = 0;
            for (var i = 0; i < BoardState.CellCount; i++)
            {
                if (state.GetCell(i).IsBox)
                {
                    boxesLeft++;
                }
            }

            if (boxesLeft < 30)
            {
                result.Errors.Add($"After early sequence {boxesLeft} boxes remain; board cleared too far.");
            }

            if (g1Merge < 2 || g1Merge > 5)
            {
                result.Errors.Add($"Bakery G1 became available after {g1Merge} merges; expected 2–5.");
            }
            else
            {
                result.Warnings.Add($"Bakery G1 available after merge {g1Merge}.");
            }
        }

        static void SimulateProduce(
            BoardState state,
            MergeItemDatabase database,
            GeneratorProductionDatabase production,
            InitialBoardPuzzleLayout.ScriptedStep step,
            int stepIndex,
            InitialBoardValidationResult result)
        {
            var generator = state.GetCell(step.FromIndex);
            if (generator == null ||
                generator.IsBox ||
                generator.ItemLocked ||
                !database.TryGetById(generator.ItemId, out var genItem) ||
                genItem == null ||
                genItem.Kind != MergeItemKind.Generator)
            {
                result.Errors.Add($"Step {stepIndex + 1}: generator at {step.FromIndex} is not usable ({step.Note}).");
                return;
            }

            var spawn = state.GetCell(step.ToIndex);
            if (spawn == null || !spawn.IsEmpty)
            {
                result.Errors.Add($"Step {stepIndex + 1}: spawn cell {step.ToIndex} is not empty ({step.Note}).");
                return;
            }

            var outputId = BoardCellState.EmptyItemId;
            if (production == null || !production.TryGetOutputItemId(generator.ItemId, out outputId))
            {
                result.Errors.Add($"Step {stepIndex + 1}: no deterministic output for generator {step.FromIndex}.");
                return;
            }

            if (!MatchesKey(database, outputId, step.ResultKey))
            {
                result.Errors.Add($"Step {stepIndex + 1}: generator output id {outputId}, expected '{step.ResultKey}'.");
                return;
            }

            spawn.SetItem(outputId, locked: false);
        }

        static bool HasUnlockedGenerator(BoardState state, MergeItemDatabase database, string key)
        {
            for (var i = 0; i < BoardState.CellCount; i++)
            {
                var cell = state.GetCell(i);
                if (cell == null || cell.IsBox || cell.ItemLocked || !cell.HasItem)
                {
                    continue;
                }

                if (database.TryGetById(cell.ItemId, out var item) && item != null && item.InternalKey == key)
                {
                    return true;
                }
            }

            return false;
        }

        static int CountEmpty(BoardState state)
        {
            var count = 0;
            for (var i = 0; i < BoardState.CellCount; i++)
            {
                if (state.GetCell(i).IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }

        static bool MatchesKey(MergeItemDatabase database, int itemId, string key)
        {
            return database.TryGetById(itemId, out var item) && item != null && item.InternalKey == key;
        }

        [MenuItem("Tools/San Island/Validate Early Progression")]
        public static void ValidateMenu()
        {
            var definition = AssetDatabase.LoadAssetAtPath<InitialBoardDefinition>(InitialBoardDefinition.DefaultAssetPath);
            var database = AssetDatabase.LoadAssetAtPath<MergeItemDatabase>(MergeBoardSetupTool.DatabasePath);
            var production = AssetDatabase.LoadAssetAtPath<GeneratorProductionDatabase>(
                MergeBoardSetupTool.GeneratorProductionDatabasePath);
            var result = Validate(definition, database, production);
            EditorUtility.DisplayDialog("Early Progression", FormatReport(result), "OK");
        }
    }
}
