using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "PlayerProgressionConfig", menuName = "San Island/Player Progression Config")]
    public class PlayerProgressionConfig : ScriptableObject
    {
        public const string BattlePassFeatureId = "battle_pass";

        [SerializeField] List<PlayerRankDefinition> ranks = new List<PlayerRankDefinition>();

        public IReadOnlyList<PlayerRankDefinition> Ranks => ranks;

        public bool TryGetRank(int rank, out PlayerRankDefinition definition)
        {
            definition = null;
            if (ranks == null)
            {
                return false;
            }

            for (var i = 0; i < ranks.Count; i++)
            {
                var entry = ranks[i];
                if (entry != null && entry.rank == rank)
                {
                    definition = entry;
                    return true;
                }
            }

            return false;
        }

        public int GetMinRank()
        {
            if (ranks == null || ranks.Count == 0)
            {
                return 1;
            }

            var min = ranks[0].rank;
            for (var i = 1; i < ranks.Count; i++)
            {
                if (ranks[i] != null && ranks[i].rank < min)
                {
                    min = ranks[i].rank;
                }
            }

            return min < 1 ? 1 : min;
        }

        public int GetMaxRank()
        {
            if (ranks == null || ranks.Count == 0)
            {
                return 1;
            }

            var max = ranks[0].rank;
            for (var i = 1; i < ranks.Count; i++)
            {
                if (ranks[i] != null && ranks[i].rank > max)
                {
                    max = ranks[i].rank;
                }
            }

            return max < 1 ? 1 : max;
        }

        public PlayerProgressionSnapshot Evaluate(long totalXp)
        {
            var snapshot = new PlayerProgressionSnapshot
            {
                CurrentRank = GetMinRank(),
                TotalXp = totalXp < 0 ? 0 : totalXp,
                HighestReachedRank = GetMinRank()
            };

            if (ranks == null || ranks.Count == 0)
            {
                snapshot.IsMaxRank = true;
                snapshot.Progress01 = 1f;
                return snapshot;
            }

            var sorted = GetSortedRanks();
            var remaining = snapshot.TotalXp;
            for (var i = 0; i < sorted.Count; i++)
            {
                var definition = sorted[i];
                snapshot.CurrentRank = definition.rank;
                snapshot.HighestReachedRank = definition.rank;
                var required = definition.xpRequiredToReachNextRank;
                var isLast = i == sorted.Count - 1 || required <= 0;
                if (isLast)
                {
                    snapshot.XpIntoCurrentRank = remaining;
                    snapshot.XpRequiredForNextRank = 0;
                    snapshot.IsMaxRank = true;
                    snapshot.Progress01 = 1f;
                    return snapshot;
                }

                if (remaining < required)
                {
                    snapshot.XpIntoCurrentRank = remaining;
                    snapshot.XpRequiredForNextRank = required;
                    snapshot.IsMaxRank = false;
                    snapshot.Progress01 = required <= 0 ? 1f : Mathf.Clamp01((float)remaining / required);
                    return snapshot;
                }

                remaining -= required;
            }

            snapshot.IsMaxRank = true;
            snapshot.Progress01 = 1f;
            return snapshot;
        }

        public long GetTotalXpRequiredToReach(int rank)
        {
            var sorted = GetSortedRanks();
            long total = 0;
            for (var i = 0; i < sorted.Count; i++)
            {
                var definition = sorted[i];
                if (definition.rank >= rank)
                {
                    return total;
                }

                if (definition.xpRequiredToReachNextRank > 0)
                {
                    total += definition.xpRequiredToReachNextRank;
                }
            }

            return total;
        }

        public List<PlayerRankDefinition> GetMilestoneRanks()
        {
            var result = new List<PlayerRankDefinition>(4);
            var sorted = GetSortedRanks();
            for (var i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].showMilestoneCard)
                {
                    result.Add(sorted[i]);
                }
            }

            return result;
        }

        public List<PlayerRankDefinition> GetSortedRanks()
        {
            var result = new List<PlayerRankDefinition>(ranks != null ? ranks.Count : 0);
            if (ranks == null)
            {
                return result;
            }

            for (var i = 0; i < ranks.Count; i++)
            {
                if (ranks[i] != null)
                {
                    result.Add(ranks[i]);
                }
            }

            result.Sort((a, b) => a.rank.CompareTo(b.rank));
            return result;
        }

        void OnValidate()
        {
            if (ranks == null || ranks.Count == 0)
            {
                ranks = CreateDefaultRanks();
            }
        }

        public static List<PlayerRankDefinition> CreateDefaultRanks()
        {
            return new List<PlayerRankDefinition>
            {
                new PlayerRankDefinition
                {
                    rank = 1,
                    xpRequiredToReachNextRank = 100,
                    unlocks = new List<RankUnlockData>
                    {
                        new RankUnlockData { type = RankUnlockType.MergeFamily, mergeFamily = MergeItemFamily.Coffee }
                    }
                },
                new PlayerRankDefinition
                {
                    rank = 2,
                    xpRequiredToReachNextRank = 150,
                    showMilestoneCard = true,
                    milestoneTitleLocalizationKey = "ui.rank.milestone.cleaning.title",
                    milestoneDescriptionLocalizationKey = "ui.rank.milestone.cleaning.description",
                    rewards = new List<RankRewardData>
                    {
                        new RankRewardData { type = RankRewardType.Energy, amount = 20 }
                    },
                    unlocks = new List<RankUnlockData>
                    {
                        new RankUnlockData { type = RankUnlockType.MergeFamily, mergeFamily = MergeItemFamily.Cleaning }
                    }
                },
                new PlayerRankDefinition
                {
                    rank = 3,
                    xpRequiredToReachNextRank = 220,
                    showMilestoneCard = true,
                    milestoneTitleLocalizationKey = "ui.rank.milestone.tools.title",
                    milestoneDescriptionLocalizationKey = "ui.rank.milestone.tools.description",
                    rewards = new List<RankRewardData>
                    {
                        new RankRewardData { type = RankRewardType.Coins, amount = 100 }
                    },
                    unlocks = new List<RankUnlockData>
                    {
                        new RankUnlockData { type = RankUnlockType.MergeFamily, mergeFamily = MergeItemFamily.Tools }
                    }
                },
                new PlayerRankDefinition
                {
                    rank = 4,
                    xpRequiredToReachNextRank = 300,
                    rewards = new List<RankRewardData>
                    {
                        new RankRewardData { type = RankRewardType.Energy, amount = 30 }
                    }
                },
                new PlayerRankDefinition
                {
                    rank = 5,
                    xpRequiredToReachNextRank = 400,
                    showMilestoneCard = true,
                    milestoneTitleLocalizationKey = "ui.rank.milestone.battle_pass.title",
                    milestoneDescriptionLocalizationKey = "ui.rank.milestone.battle_pass.description",
                    rewards = new List<RankRewardData>
                    {
                        new RankRewardData
                        {
                            type = RankRewardType.GiftPlaceholder,
                            amount = 1,
                            rewardKey = "dev_gift_1"
                        }
                    },
                    unlocks = new List<RankUnlockData>
                    {
                        new RankUnlockData { type = RankUnlockType.Feature, contentId = BattlePassFeatureId }
                    }
                },
                new PlayerRankDefinition
                {
                    rank = 6,
                    xpRequiredToReachNextRank = 0,
                    rewards = new List<RankRewardData>
                    {
                        new RankRewardData { type = RankRewardType.Coins, amount = 200 }
                    }
                }
            };
        }
    }
}
