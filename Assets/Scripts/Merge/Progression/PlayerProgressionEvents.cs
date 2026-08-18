using System;

namespace SanIsland.Merge
{
    public readonly struct RankContentUnlock
    {
        public RankContentUnlock(RankUnlockType type, MergeItemFamily mergeFamily, string contentId)
        {
            Type = type;
            MergeFamily = mergeFamily;
            ContentId = contentId ?? string.Empty;
        }

        public RankUnlockType Type { get; }
        public MergeItemFamily MergeFamily { get; }
        public string ContentId { get; }
    }

    public readonly struct RankRewardClaim
    {
        public RankRewardClaim(int rank, RankRewardData reward)
        {
            Rank = rank;
            Reward = reward;
        }

        public int Rank { get; }
        public RankRewardData Reward { get; }
    }
}
