using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    public enum RankRewardType
    {
        Energy = 0,
        Coins = 1,
        GiftPlaceholder = 2
    }

    public enum RankUnlockType
    {
        MergeFamily = 0,
        Location = 1,
        Feature = 2,
        Event = 3
    }

    public enum RankMilestoneCardState
    {
        Locked = 0,
        Current = 1,
        Completed = 2
    }

    [Serializable]
    public class RankRewardData
    {
        public RankRewardType type = RankRewardType.Energy;
        public long amount = 10;
        public string rewardKey;
    }

    [Serializable]
    public class RankUnlockData
    {
        public RankUnlockType type = RankUnlockType.MergeFamily;
        public MergeItemFamily mergeFamily = MergeItemFamily.Coffee;
        public string contentId;
    }

    [Serializable]
    public class PlayerRankDefinition
    {
        public int rank = 1;
        public long xpRequiredToReachNextRank = 100;
        public List<RankRewardData> rewards = new List<RankRewardData>();
        public List<RankUnlockData> unlocks = new List<RankUnlockData>();
        public bool showMilestoneCard;
        public Sprite milestoneSprite;
        public string milestoneTitleLocalizationKey;
        public string milestoneDescriptionLocalizationKey;
    }

    public sealed class PlayerProgressionSnapshot
    {
        public int CurrentRank;
        public long TotalXp;
        public long XpIntoCurrentRank;
        public long XpRequiredForNextRank;
        public float Progress01;
        public bool IsMaxRank;
        public int HighestReachedRank;
    }

    public sealed class PlayerProgressionState
    {
        public long TotalXp;
        public int HighestReachedRank = 1;
        public readonly HashSet<int> ClaimedRewardRanks = new HashSet<int>();
        public readonly HashSet<string> UnlockedLocationIds = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> UnlockedFeatureIds = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> UnlockedEventIds = new HashSet<string>(StringComparer.Ordinal);

        public void Reset()
        {
            TotalXp = 0;
            HighestReachedRank = 1;
            ClaimedRewardRanks.Clear();
            UnlockedLocationIds.Clear();
            UnlockedFeatureIds.Clear();
            UnlockedEventIds.Clear();
        }
    }
}
