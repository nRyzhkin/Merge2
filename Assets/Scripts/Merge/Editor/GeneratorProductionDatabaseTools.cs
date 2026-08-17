using System.Collections.Generic;
using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    public static class GeneratorProductionDatabaseTools
    {
        public const string DatabasePath = MergeBoardSetupTool.GeneratorProductionDatabasePath;

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
            var entries = new List<GeneratorData>(13);
            AddToolsFamily(entries, itemDatabase);
            AddFourTierFamily(entries, itemDatabase, MergeItemFamily.Cleaning);
            AddFourTierFamily(entries, itemDatabase, MergeItemFamily.Coffee);
            return entries;
        }

        static void AddToolsFamily(List<GeneratorData> entries, MergeItemDatabase itemDatabase)
        {
            AddEntry(entries, itemDatabase, MergeItemFamily.Tools, 1, 16, Drop(100));
            AddEntry(entries, itemDatabase, MergeItemFamily.Tools, 2, 20, Drop(82), Drop(18, 2));
            AddEntry(entries, itemDatabase, MergeItemFamily.Tools, 3, 24, Drop(68), Drop(28, 2), Drop(4, 3));
            AddEntry(entries, itemDatabase, MergeItemFamily.Tools, 4, 28, Drop(55), Drop(35, 2), Drop(9, 3), Drop(1, 4));
            AddEntry(entries, itemDatabase, MergeItemFamily.Tools, 5, 32, Drop(48), Drop(36, 2), Drop(13, 3), Drop(3, 4));
        }

        static void AddFourTierFamily(List<GeneratorData> entries, MergeItemDatabase itemDatabase, MergeItemFamily family)
        {
            AddEntry(entries, itemDatabase, family, 1, 16, Drop(100));
            AddEntry(entries, itemDatabase, family, 2, 20, Drop(82), Drop(18, 2));
            AddEntry(entries, itemDatabase, family, 3, 24, Drop(68), Drop(28, 2), Drop(4, 3));
            AddEntry(entries, itemDatabase, family, 4, 28, Drop(55), Drop(35, 2), Drop(9, 3), Drop(1, 4));
        }

        static (int weight, int level) Drop(int weight, int level = 1) => (weight, level);

        static void AddEntry(
            List<GeneratorData> entries,
            MergeItemDatabase itemDatabase,
            MergeItemFamily family,
            int generatorLevel,
            int capacityDrops,
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
                CapacityDrops = capacityDrops,
                CooldownSeconds = 60f
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
