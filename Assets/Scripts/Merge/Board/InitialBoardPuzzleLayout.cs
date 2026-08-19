namespace SanIsland.Merge
{
    /// <summary>
    /// Authored V3 progression map: center Bakery onboarding, sparse families, cost grows to the edges.
    /// </summary>
    public static class InitialBoardPuzzleLayout
    {
        public const int StartLeft = 24;
        public const int StartRight = 25;
        public const int EarlyCobweb = 26;
        public const int GeneratorCell = 45;

        public struct HiddenSpec
        {
            public int Index;
            public string Key;
            public bool Generator;
        }

        public struct ScriptedStep
        {
            public bool Produce;
            public int FromIndex;
            public int ToIndex;
            public string ResultKey;
            public string Note;
        }

        public static readonly HiddenSpec[] VisibleCobwebs =
        {
            C(26, "bakery_l02")
        };

        public static readonly HiddenSpec[] HiddenCells =
        {
            H(0, "coffee_l12"), H(1, "coffee_l11"), H(2, "beach_l09"), H(3, "cleaning_l08"),
            H(4, "cleaning_l07"), H(5, "coffee_l08"), H(6, "beach_l08"), H(7, "beach_l11"),
            H(8, "cocktails_l11"), H(9, "cocktails_l12"),
            H(10, "beach_l12"), H(11, "coffee_l09"), H(12, "cocktails_l08"), H(13, "cleaning_l05"),
            H(14, "cleaning_l03"), H(15, "cleaning_l01"), H(16, "coffee_l07"), H(17, "coffee_l09"),
            H(18, "beach_l10"), H(19, "coffee_l12"),
            H(20, "cocktails_l10"), H(21, "bakery_l10"), H(22, "bakery_l07"), H(23, "cocktails_l06"),
            H(27, "bakery_l05"), H(28, "bakery_l12"), H(29, "coffee_l11"),
            H(30, "beach_l10"), H(31, "cocktails_l08"), H(32, "cocktails_l07"), H(33, "beach_l06"),
            H(34, "bakery_l03"), H(35, "bakery_l03"), G(45, "bakery_g01"), H(36, "bakery_l05"),
            H(37, "bakery_l07"), H(38, "coffee_l11"), H(39, "coffee_l12"),
            H(40, "beach_l12"), H(41, "beach_l07"), H(42, "beach_l09"), H(43, "tools_l06"),
            H(44, "tools_l04"), H(46, "tools_l01"), H(47, "cocktails_l10"),
            H(48, "bakery_l11"), H(49, "coffee_l12"),
            H(50, "beach_l12"), H(51, "beach_l11"), H(52, "cocktails_l10"), H(53, "cocktails_l11"),
            H(54, "tools_l08"), H(55, "beach_l08"), H(56, "coffee_l10"), H(57, "cocktails_l11"),
            H(58, "coffee_l11"), H(59, "cocktails_l12")
        };

        public static readonly ScriptedStep[] EarlySteps =
        {
            Merge(24, 25, "bakery_l02", "Center L1+L1. Opens Cleaning L1 (up) and Bakery L3 (hub)."),
            Merge(25, 26, "bakery_l03", "L2 + web L2. Opens 2x Bakery L5 goals (27, 36) — no match yet."),
            Merge(26, 35, "bakery_l04", "L3 + web L3 → L4. Opens Bakery G1 (45) and extra Bakery L3 (34)."),
            Produce(45, 24, "bakery_l01", "First Bakery G1 tap. L5/L7 still require production (no L6 on board).")
        };

        public static void Apply(InitialBoardDefinition definition, MergeItemDatabase database)
        {
            if (definition == null)
            {
                return;
            }

            definition.ClearAll();
            if (database == null)
            {
                UnityEngine.Debug.LogError("[InitialBoard] Cannot apply puzzle layout without MergeItemDatabase.");
                return;
            }

            SetVisibleItem(definition, database, StartLeft, "bakery_l01");
            SetVisibleItem(definition, database, StartRight, "bakery_l01");
            for (var i = 0; i < VisibleCobwebs.Length; i++)
            {
                SetCobweb(definition, database, VisibleCobwebs[i]);
            }

            for (var i = 0; i < HiddenCells.Length; i++)
            {
                SetBox(definition, database, HiddenCells[i]);
            }
        }

        static ScriptedStep Merge(int from, int to, string resultKey, string note)
        {
            return new ScriptedStep
            {
                Produce = false,
                FromIndex = from,
                ToIndex = to,
                ResultKey = resultKey,
                Note = note
            };
        }

        static ScriptedStep Produce(int generatorIndex, int spawnIndex, string resultKey, string note)
        {
            return new ScriptedStep
            {
                Produce = true,
                FromIndex = generatorIndex,
                ToIndex = spawnIndex,
                ResultKey = resultKey,
                Note = note
            };
        }

        static HiddenSpec H(int index, string key)
        {
            return new HiddenSpec { Index = index, Key = key, Generator = false };
        }

        static HiddenSpec G(int index, string key)
        {
            return new HiddenSpec { Index = index, Key = key, Generator = true };
        }

        static HiddenSpec C(int index, string key)
        {
            return new HiddenSpec { Index = index, Key = key, Generator = false };
        }

        static void SetVisibleItem(InitialBoardDefinition definition, MergeItemDatabase database, int index, string key)
        {
            if (!TryId(database, key, out var id))
            {
                return;
            }

            var cell = definition.GetCell(index);
            cell.state = CellInitialState.Item;
            cell.itemId = id;
        }

        static void SetCobweb(InitialBoardDefinition definition, MergeItemDatabase database, HiddenSpec spec)
        {
            if (!TryId(database, spec.Key, out var id))
            {
                return;
            }

            var cell = definition.GetCell(spec.Index);
            cell.state = CellInitialState.CobwebItem;
            cell.itemId = id;
        }

        static void SetBox(InitialBoardDefinition definition, MergeItemDatabase database, HiddenSpec spec)
        {
            if (!TryId(database, spec.Key, out var id))
            {
                return;
            }

            var cell = definition.GetCell(spec.Index);
            cell.state = CellInitialState.Box;
            cell.itemId = BoardCellState.EmptyItemId;
            cell.Hidden.type = spec.Generator ? BoxRevealType.Generator : BoxRevealType.CobwebItem;
            cell.Hidden.itemId = id;
        }

        static bool TryId(MergeItemDatabase database, string key, out int id)
        {
            id = BoardCellState.EmptyItemId;
            if (database.TryGetByKey(key, out var item) && item != null)
            {
                id = item.Id;
                return true;
            }

            UnityEngine.Debug.LogError($"[InitialBoard] Puzzle item '{key}' was not found.");
            return false;
        }
    }
}
