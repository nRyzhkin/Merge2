using System;
using System.Collections.Generic;
using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    public static class MergeItemDatabaseImporter
    {
        public const string DatabasePath = "Assets/Data/MergeItemDatabase.asset";

        [MenuItem("Tools/San Island/Rebuild Merge Item Database")]
        public static void RebuildFromMenu()
        {
            Rebuild();
        }

        public static void RebuildFromCommandLine()
        {
            try
            {
                Rebuild();
                GeneratorProductionDatabaseTools.Rebuild();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MergeItemImporter] Rebuild failed: {exception}");
                EditorApplication.Exit(1);
            }
        }

        public static MergeItemDatabase Rebuild()
        {
            MergeItemLocalizationImporter.EnsureProjectSetup();
            var database = GetOrCreateDatabase();
            var parsed = MergeItemSpriteScanner.Scan(out _, out _);
            var items = MergeRecords(database, parsed);
            AssignNextItemIds(items);
            SortItems(items);
            database.EditorReplaceItems(items);
            MergeItemLocalizationImporter.UpsertEnglishKeys(parsed);
            MergeItemDatabaseValidator.Validate(items);
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MergeItemImporter] Merge Item Database rebuild complete. Items: {items.Count}.");
            return database;
        }

        static MergeItemDatabase GetOrCreateDatabase()
        {
            var guids = AssetDatabase.FindAssets("t:MergeItemDatabase");
            if (guids.Length > 1)
            {
                Debug.LogWarning($"[MergeItemImporter] Found {guids.Length} MergeItemDatabase assets. Using the first and keeping IDs stable.");
            }

            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var existing = AssetDatabase.LoadAssetAtPath<MergeItemDatabase>(path);
                if (existing != null)
                {
                    return existing;
                }
            }

            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }

            var database = ScriptableObject.CreateInstance<MergeItemDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
            return database;
        }

        static List<MergeItemData> MergeRecords(MergeItemDatabase database, List<ParsedMergeSprite> parsedSprites)
        {
            var existingItems = database.Items;
            var byKey = new Dictionary<string, MergeItemData>();
            var byFamilyKindLevel = new Dictionary<(MergeItemFamily, MergeItemKind, int), MergeItemData>();
            var usedIds = new HashSet<int>();

            for (var i = 0; i < existingItems.Count; i++)
            {
                var existing = existingItems[i];
                if (existing == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(existing.InternalKey) && !byKey.ContainsKey(existing.InternalKey))
                {
                    byKey.Add(existing.InternalKey, Clone(existing));
                }

                var tuple = (existing.Family, existing.Kind, existing.Level);
                if (!byFamilyKindLevel.ContainsKey(tuple))
                {
                    byFamilyKindLevel.Add(tuple, byKey.TryGetValue(existing.InternalKey, out var keyed) ? keyed : Clone(existing));
                }

                usedIds.Add(existing.Id);
            }

            var result = new List<MergeItemData>(parsedSprites.Count);
            var consumedKeys = new HashSet<string>();

            for (var i = 0; i < parsedSprites.Count; i++)
            {
                var parsed = parsedSprites[i];
                MergeItemData record = null;
                if (byKey.TryGetValue(parsed.InternalKey, out var byInternalKey))
                {
                    record = byInternalKey;
                }
                else if (byFamilyKindLevel.TryGetValue((parsed.Family, parsed.Kind, parsed.Level), out var byTriple))
                {
                    record = byTriple;
                }

                if (record == null)
                {
                    record = new MergeItemData
                    {
                        Id = AllocateId(parsed, usedIds),
                        InternalKey = parsed.InternalKey,
                        LocalizationKey = parsed.LocalizationKey,
                        Family = parsed.Family,
                        Kind = parsed.Kind,
                        Level = parsed.Level,
                        Icon = parsed.Sprite,
                        NextItemId = MergeItemIdUtility.NoNextItemId
                    };
                    usedIds.Add(record.Id);
                }
                else
                {
                    record.Family = parsed.Family;
                    record.Kind = parsed.Kind;
                    record.Level = parsed.Level;
                    record.Icon = parsed.Sprite;
                    if (string.IsNullOrEmpty(record.InternalKey))
                    {
                        record.InternalKey = parsed.InternalKey;
                    }

                    if (string.IsNullOrEmpty(record.LocalizationKey))
                    {
                        record.LocalizationKey = parsed.LocalizationKey;
                    }
                }

                if (!consumedKeys.Add(record.InternalKey))
                {
                    Debug.LogError($"[MergeItemImporter] Duplicate record for '{record.InternalKey}' from '{parsed.AssetPath}'.");
                    continue;
                }

                result.Add(record);
            }

            for (var i = 0; i < existingItems.Count; i++)
            {
                var existing = existingItems[i];
                if (existing == null || string.IsNullOrEmpty(existing.InternalKey))
                {
                    continue;
                }

                if (consumedKeys.Contains(existing.InternalKey))
                {
                    continue;
                }

                Debug.LogWarning($"[MergeItemImporter] Keeping existing item '{existing.InternalKey}' (ID {existing.Id}) with no matching sprite.");
                if (byKey.TryGetValue(existing.InternalKey, out var orphan))
                {
                    result.Add(orphan);
                }
            }

            return result;
        }

        static int AllocateId(ParsedMergeSprite parsed, HashSet<int> usedIds)
        {
            var computed = MergeItemIdUtility.ComputeStableId(parsed.Family, parsed.Kind, parsed.Level);
            if (!usedIds.Contains(computed))
            {
                return computed;
            }

            Debug.LogError($"[MergeItemImporter] Computed ID {computed} for '{parsed.InternalKey}' is already in use. Existing published IDs were not changed.");
            throw new InvalidOperationException($"Unable to allocate a stable ID for '{parsed.InternalKey}' without colliding with an existing ID {computed}.");
        }

        static void AssignNextItemIds(List<MergeItemData> items)
        {
            var chains = new Dictionary<(MergeItemFamily, MergeItemKind), List<MergeItemData>>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var key = (item.Family, item.Kind);
                if (!chains.TryGetValue(key, out var chain))
                {
                    chain = new List<MergeItemData>();
                    chains.Add(key, chain);
                }

                chain.Add(item);
            }

            foreach (var chain in chains.Values)
            {
                chain.Sort((a, b) => a.Level.CompareTo(b.Level));
                for (var i = 0; i < chain.Count; i++)
                {
                    chain[i].NextItemId = i < chain.Count - 1
                        ? chain[i + 1].Id
                        : MergeItemIdUtility.NoNextItemId;
                }
            }
        }

        static void SortItems(List<MergeItemData> items)
        {
            items.Sort((a, b) =>
            {
                var family = a.Family.CompareTo(b.Family);
                if (family != 0)
                {
                    return family;
                }

                var kind = a.Kind.CompareTo(b.Kind);
                if (kind != 0)
                {
                    return kind;
                }

                return a.Level.CompareTo(b.Level);
            });
        }

        static MergeItemData Clone(MergeItemData source)
        {
            return new MergeItemData
            {
                Id = source.Id,
                InternalKey = source.InternalKey,
                LocalizationKey = source.LocalizationKey,
                Family = source.Family,
                Kind = source.Kind,
                Level = source.Level,
                Icon = source.Icon,
                NextItemId = source.NextItemId
            };
        }
    }
}
