using System;
using System.Collections.Generic;
using SanIsland.Merge;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace SanIsland.Merge.Editor
{
    [CustomEditor(typeof(PlayerProgressionConfig))]
    public class PlayerProgressionConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Validate"))
            {
                PlayerProgressionConfigValidator.Validate((PlayerProgressionConfig)target, true);
            }
        }
    }

    public static class PlayerProgressionConfigValidator
    {
        public static void Validate(PlayerProgressionConfig config, bool logToConsole)
        {
            if (config == null)
            {
                return;
            }

            var errors = new List<string>();
            var warnings = new List<string>();
            var ranks = config.GetSortedRanks();
            if (ranks.Count == 0)
            {
                errors.Add("No ranks configured.");
            }

            var seen = new HashSet<int>();
            var hasRank1 = false;
            var mergeUnlocks = new HashSet<MergeItemFamily>();
            var locationIds = new HashSet<string>(StringComparer.Ordinal);
            var featureIds = new HashSet<string>(StringComparer.Ordinal);
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            var locKeys = LoadUiKeys();

            for (var i = 0; i < ranks.Count; i++)
            {
                var rank = ranks[i];
                if (rank == null)
                {
                    errors.Add($"Null rank at sorted index {i}.");
                    continue;
                }

                if (!seen.Add(rank.rank))
                {
                    errors.Add($"Duplicate rank {rank.rank}.");
                }

                if (rank.rank == 1)
                {
                    hasRank1 = true;
                }

                if (i < ranks.Count - 1 && rank.xpRequiredToReachNextRank <= 0)
                {
                    errors.Add($"Rank {rank.rank} xpRequiredToReachNextRank must be > 0 except for the final rank.");
                }

                if (i < ranks.Count - 1 && i + 1 < ranks.Count && ranks[i + 1].rank <= rank.rank)
                {
                    errors.Add("Ranks must be sorted ascending without duplicates.");
                }

                if (rank.showMilestoneCard)
                {
                    ValidateKey(rank.milestoneTitleLocalizationKey, $"Rank {rank.rank} milestone title", locKeys, warnings);
                    ValidateKey(rank.milestoneDescriptionLocalizationKey, $"Rank {rank.rank} milestone description", locKeys, warnings);
                }

                ValidateUnlocks(rank, mergeUnlocks, locationIds, featureIds, eventIds, warnings);
            }

            if (!hasRank1)
            {
                errors.Add("Rank 1 is required.");
            }

            if (logToConsole)
            {
                for (var i = 0; i < errors.Count; i++)
                {
                    Debug.LogError("[PlayerProgressionConfig] " + errors[i], config);
                }

                for (var i = 0; i < warnings.Count; i++)
                {
                    Debug.LogWarning("[PlayerProgressionConfig] " + warnings[i], config);
                }

                if (errors.Count == 0 && warnings.Count == 0)
                {
                    Debug.Log("[PlayerProgressionConfig] Validation passed.", config);
                }
            }
        }

        static void ValidateUnlocks(
            PlayerRankDefinition rank,
            HashSet<MergeItemFamily> mergeUnlocks,
            HashSet<string> locationIds,
            HashSet<string> featureIds,
            HashSet<string> eventIds,
            List<string> warnings)
        {
            if (rank.unlocks == null)
            {
                return;
            }

            for (var i = 0; i < rank.unlocks.Count; i++)
            {
                var unlock = rank.unlocks[i];
                if (unlock == null)
                {
                    continue;
                }

                switch (unlock.type)
                {
                    case RankUnlockType.MergeFamily:
                        if (!Enum.IsDefined(typeof(MergeItemFamily), unlock.mergeFamily))
                        {
                            warnings.Add($"Rank {rank.rank} MergeFamily unlock is not a valid family.");
                        }
                        else if (!mergeUnlocks.Add(unlock.mergeFamily))
                        {
                            warnings.Add($"Duplicate MergeFamily unlock '{unlock.mergeFamily}'.");
                        }

                        break;
                    case RankUnlockType.Location:
                        WarnDuplicateId(rank.rank, "Location", unlock.contentId, locationIds, warnings);
                        break;
                    case RankUnlockType.Feature:
                        WarnDuplicateId(rank.rank, "Feature", unlock.contentId, featureIds, warnings);
                        break;
                    case RankUnlockType.Event:
                        WarnDuplicateId(rank.rank, "Event", unlock.contentId, eventIds, warnings);
                        break;
                }
            }
        }

        static void WarnDuplicateId(int rank, string kind, string id, HashSet<string> set, List<string> warnings)
        {
            if (string.IsNullOrEmpty(id))
            {
                warnings.Add($"Rank {rank} {kind} unlock is missing contentId.");
                return;
            }

            if (!set.Add(id))
            {
                warnings.Add($"Duplicate {kind} id '{id}'.");
            }
        }

        static void ValidateKey(string key, string label, HashSet<string> locKeys, List<string> warnings)
        {
            if (string.IsNullOrEmpty(key))
            {
                warnings.Add(label + " localization key is empty.");
                return;
            }

            if (locKeys != null && locKeys.Count > 0 && !locKeys.Contains(key))
            {
                warnings.Add(label + " localization key '" + key + "' is missing from UI table.");
            }
        }

        static HashSet<string> LoadUiKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>("Assets/Localization/Tables/UI Shared Data.asset");
            if (shared == null)
            {
                return keys;
            }

            var entries = shared.Entries;
            for (var e = 0; e < entries.Count; e++)
            {
                if (!string.IsNullOrEmpty(entries[e].Key))
                {
                    keys.Add(entries[e].Key);
                }
            }

            return keys;
        }
    }
}
