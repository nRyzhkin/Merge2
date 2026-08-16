using System.Collections.Generic;
using SanIsland.Merge;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    public static class MergeItemDatabaseValidator
    {
        public static void Validate(IReadOnlyList<MergeItemData> items)
        {
            var ids = new Dictionary<int, MergeItemData>();
            var keys = new Dictionary<string, MergeItemData>();
            var familyKindLevel = new Dictionary<(MergeItemFamily, MergeItemKind, int), MergeItemData>();
            var chains = new Dictionary<(MergeItemFamily, MergeItemKind), List<MergeItemData>>();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    Debug.LogError("[MergeItemValidator] Null item in database.");
                    continue;
                }

                if (ids.ContainsKey(item.Id))
                {
                    Debug.LogError($"[MergeItemValidator] Duplicate ID {item.Id}: '{ids[item.Id].InternalKey}' and '{item.InternalKey}'.");
                }
                else
                {
                    ids.Add(item.Id, item);
                }

                if (string.IsNullOrEmpty(item.InternalKey))
                {
                    Debug.LogError($"[MergeItemValidator] Missing internalKey for ID {item.Id}.");
                }
                else if (keys.ContainsKey(item.InternalKey))
                {
                    Debug.LogError($"[MergeItemValidator] Duplicate internalKey '{item.InternalKey}' (IDs {keys[item.InternalKey].Id} and {item.Id}).");
                }
                else
                {
                    keys.Add(item.InternalKey, item);
                }

                var tuple = (item.Family, item.Kind, item.Level);
                if (familyKindLevel.ContainsKey(tuple))
                {
                    Debug.LogError($"[MergeItemValidator] Duplicate family/kind/level {item.Family} {item.Kind} L{item.Level}: '{familyKindLevel[tuple].InternalKey}' and '{item.InternalKey}'.");
                }
                else
                {
                    familyKindLevel.Add(tuple, item);
                }

                if (item.Icon == null)
                {
                    Debug.LogError($"[MergeItemValidator] Missing sprite for '{item.InternalKey}' (ID {item.Id}).");
                }

                if (string.IsNullOrEmpty(item.LocalizationKey))
                {
                    Debug.LogError($"[MergeItemValidator] Missing localizationKey for '{item.InternalKey}' (ID {item.Id}).");
                }

                var chainKey = (item.Family, item.Kind);
                if (!chains.TryGetValue(chainKey, out var chain))
                {
                    chain = new List<MergeItemData>();
                    chains.Add(chainKey, chain);
                }

                chain.Add(item);
            }

            foreach (var pair in chains)
            {
                var chain = pair.Value;
                chain.Sort((a, b) => a.Level.CompareTo(b.Level));

                for (var i = 0; i < chain.Count; i++)
                {
                    var item = chain[i];
                    var isMax = i == chain.Count - 1;
                    if (isMax)
                    {
                        if (item.NextItemId != MergeItemIdUtility.NoNextItemId)
                        {
                            Debug.LogError($"[MergeItemValidator] Max-tier '{item.InternalKey}' must have nextItemId = {MergeItemIdUtility.NoNextItemId}, got {item.NextItemId}.");
                        }
                    }
                    else
                    {
                        var next = chain[i + 1];
                        if (item.NextItemId != next.Id)
                        {
                            Debug.LogError($"[MergeItemValidator] '{item.InternalKey}' should point to next level '{next.InternalKey}' (ID {next.Id}), got nextItemId {item.NextItemId}.");
                        }
                    }

                    if (i > 0)
                    {
                        var previous = chain[i - 1];
                        for (var missing = previous.Level + 1; missing < item.Level; missing++)
                        {
                            Debug.LogWarning($"[MergeItemValidator] Gap in {pair.Key.Item1} {pair.Key.Item2} chain: missing level {missing}.");
                        }
                    }
                }
            }
        }
    }
}
