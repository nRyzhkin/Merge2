using System.Collections.Generic;
using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    public static class GeneratorProductionDatabaseTools
    {
        public const string DatabasePath = MergeBoardSetupTool.GeneratorProductionDatabasePath;

        static readonly int[] FastDropsPerChargeByLevel = { 10, 12, 14, 16 };

        [MenuItem("Tools/San Island/Rebuild Generator Production Database")]
        public static void RebuildFromMenu()
        {
            Rebuild();
        }

        public static GeneratorProductionDatabase Rebuild()
        {
            var database = AssetDatabase.LoadAssetAtPath<GeneratorProductionDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<GeneratorProductionDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            var itemDatabase = AssetDatabase.LoadAssetAtPath<MergeItemDatabase>(MergeBoardSetupTool.DatabasePath);
            if (itemDatabase == null)
            {
                Debug.LogError("[GeneratorProduction] MergeItemDatabase not found.");
                return database;
            }

            var entries = BuildStarterEntries(itemDatabase);
            database.EditorReplace(entries);
            database.Validate(itemDatabase);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GeneratorProduction] Rebuilt {entries.Count} generator entries.");
            return database;
        }

        static List<GeneratorData> BuildStarterEntries(MergeItemDatabase itemDatabase)
        {
            var entries = new List<GeneratorData>(25);
            AddToolsFamily(entries, itemDatabase);
            AddUtilityFourTierFamily(entries, itemDatabase, MergeItemFamily.Cleaning);
            AddFastFamily(entries, itemDatabase, MergeItemFamily.Coffee);
            AddFastFamily(entries, itemDatabase, MergeItemFamily.Bakery);
            AddFastFamily(entries, itemDatabase, MergeItemFamily.Beach);
            AddFastFamily(entries, itemDatabase, MergeItemFamily.Cocktails);
            return entries;
        }

        static void AddToolsFamily(List<GeneratorData> entries, MergeItemDatabase itemDatabase)
        {
            AddEntry(entries, itemDatabase, MergeItemFamily.Tools, 1, 16, 1, 60f, Drop(100));
            AddEntry(entries, itemDatabase, MergeItemFamily.Tools, 2, 20, 1, 60f, Drop(82), Drop(18, 2));
            AddEntry(entries, itemDatabase, MergeItemFamily.Tools, 3, 24, 1, 60f, Drop(68), Drop(28, 2), Drop(4, 3));
            AddEntry(entries, itemDatabase, MergeItemFamily.Tools, 4, 28, 1, 60f, Drop(55), Drop(35, 2), Drop(9, 3), Drop(1, 4));
            AddEntry(entries, itemDatabase, MergeItemFamily.Tools, 5, 32, 1, 60f, Drop(48), Drop(36, 2), Drop(13, 3), Drop(3, 4));
        }

        static void AddUtilityFourTierFamily(List<GeneratorData> entries, MergeItemDatabase itemDatabase, MergeItemFamily family)
        {
            AddEntry(entries, itemDatabase, family, 1, 16, 1, 60f, Drop(100));
            AddEntry(entries, itemDatabase, family, 2, 20, 1, 60f, Drop(82), Drop(18, 2));
            AddEntry(entries, itemDatabase, family, 3, 24, 1, 60f, Drop(68), Drop(28, 2), Drop(4, 3));
            AddEntry(entries, itemDatabase, family, 4, 28, 1, 60f, Drop(55), Drop(35, 2), Drop(9, 3), Drop(1, 4));
        }

        static void AddFastFamily(List<GeneratorData> entries, MergeItemDatabase itemDatabase, MergeItemFamily family)
        {
            AddEntry(entries, itemDatabase, family, 1, FastDropsPerChargeByLevel[0], 4, 120f, Drop(100));
            AddEntry(entries, itemDatabase, family, 2, FastDropsPerChargeByLevel[1], 4, 120f, Drop(82), Drop(18, 2));
            AddEntry(entries, itemDatabase, family, 3, FastDropsPerChargeByLevel[2], 4, 120f, Drop(68), Drop(28, 2), Drop(4, 3));
            AddEntry(entries, itemDatabase, family, 4, FastDropsPerChargeByLevel[3], 4, 120f, Drop(55), Drop(35, 2), Drop(9, 3), Drop(1, 4));
        }

        static (int weight, int level) Drop(int weight, int level = 1) => (weight, level);

        static void AddEntry(
            List<GeneratorData> entries,
            MergeItemDatabase itemDatabase,
            MergeItemFamily family,
            int generatorLevel,
            int dropsPerCharge,
            int maxStoredCharges,
            float rechargeSecondsPerCharge,
            params (int weight, int level)[] drops)
        {
            var generatorId = MergeItemIdUtility.ComputeStableId(family, MergeItemKind.Generator, generatorLevel);
            if (!itemDatabase.TryGetById(generatorId, out var generatorItem) || generatorItem == null)
            {
                Debug.LogWarning($"[GeneratorProduction] Missing generator id {generatorId} for {family} G{generatorLevel}.");
                return;
            }

            var entry = new GeneratorData
            {
                GeneratorId = generatorId,
                DropsPerCharge = dropsPerCharge,
                MaxStoredCharges = maxStoredCharges,
                RechargeSecondsPerCharge = rechargeSecondsPerCharge
            };

            for (var i = 0; i < drops.Length; i++)
            {
                var outputId = MergeItemIdUtility.ComputeStableId(family, MergeItemKind.Normal, drops[i].level);
                entry.DropTable.Add(new GeneratorDropEntry
                {
                    OutputItemId = outputId,
                    Weight = drops[i].weight
                });
            }

            entries.Add(entry);
        }
    }
}
