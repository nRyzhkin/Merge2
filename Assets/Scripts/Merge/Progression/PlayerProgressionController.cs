using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [DefaultExecutionOrder(-160)]
    [DisallowMultipleComponent]
    public class PlayerProgressionController : MonoBehaviour
    {
        public static PlayerProgressionController Current { get; private set; }

        [SerializeField] PlayerProgressionConfig config;

        readonly PlayerProgressionState _state = new PlayerProgressionState();

        public PlayerProgressionConfig Config => config;
        public PlayerProgressionState State => _state;
        public long TotalXp => _state.TotalXp;
        public int CurrentRank => GetSnapshot().CurrentRank;
        public int HighestReachedRank => _state.HighestReachedRank;
        public float Progress01 => GetSnapshot().Progress01;
        public bool IsMaxRank => GetSnapshot().IsMaxRank;

        public event Action XpChanged;
        public event Action<int> RankReached;
        public event Action<RankRewardClaim> RankRewardClaimed;
        public event Action<RankContentUnlock> ContentUnlocked;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[PlayerProgression] Duplicate controller in the scene. Keeping the first instance.");
            }
            else
            {
                Current = this;
            }

            ResetRuntimeState();
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        public PlayerProgressionSnapshot GetSnapshot()
        {
            if (config == null)
            {
                return new PlayerProgressionSnapshot
                {
                    CurrentRank = 1,
                    HighestReachedRank = Mathf.Max(1, _state.HighestReachedRank),
                    TotalXp = _state.TotalXp,
                    IsMaxRank = true,
                    Progress01 = 1f
                };
            }

            var snapshot = config.Evaluate(_state.TotalXp);
            snapshot.HighestReachedRank = Mathf.Max(snapshot.CurrentRank, _state.HighestReachedRank);
            return snapshot;
        }

        public void AddXp(long amount)
        {
            if (amount <= 0)
            {
                return;
            }

            var before = GetSnapshot();
            _state.TotalXp += amount;
            var after = GetSnapshot();
            if (after.CurrentRank > _state.HighestReachedRank)
            {
                _state.HighestReachedRank = after.CurrentRank;
            }

            for (var rank = before.CurrentRank + 1; rank <= after.CurrentRank; rank++)
            {
                ApplyUnlocksForRank(rank);
                RankReached?.Invoke(rank);
            }

            XpChanged?.Invoke();
        }

        public bool HasUnclaimedRankRewards()
        {
            return TryGetNextUnclaimedRank(out _);
        }

        public bool TryGetNextUnclaimedRank(out int rank)
        {
            rank = 0;
            if (config == null)
            {
                return false;
            }

            var highest = _state.HighestReachedRank;
            var sorted = config.GetSortedRanks();
            for (var i = 0; i < sorted.Count; i++)
            {
                var definition = sorted[i];
                if (definition.rank <= 1 || definition.rank > highest)
                {
                    continue;
                }

                if (!HasClaimableRewards(definition) || _state.ClaimedRewardRanks.Contains(definition.rank))
                {
                    continue;
                }

                rank = definition.rank;
                return true;
            }

            return false;
        }

        public bool TryGetNextUnclaimedReward(out int rank, out RankRewardData reward)
        {
            reward = null;
            if (!TryGetNextUnclaimedRank(out rank) || !config.TryGetRank(rank, out var definition))
            {
                return false;
            }

            if (definition.rewards == null || definition.rewards.Count == 0)
            {
                return false;
            }

            reward = definition.rewards[0];
            return reward != null;
        }

        public bool TryClaimNextReward()
        {
            if (!TryGetNextUnclaimedRank(out var rank) || !config.TryGetRank(rank, out var definition))
            {
                return false;
            }

            var rewards = definition.rewards;
            if (rewards == null || rewards.Count == 0)
            {
                return false;
            }

            _state.ClaimedRewardRanks.Add(rank);
            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null)
                {
                    continue;
                }

                GrantReward(reward);
                RankRewardClaimed?.Invoke(new RankRewardClaim(rank, reward));
            }

            return true;
        }

        public bool IsLocationUnlocked(string id)
        {
            return !string.IsNullOrEmpty(id) && _state.UnlockedLocationIds.Contains(id);
        }

        public bool IsFeatureUnlocked(string id)
        {
            return !string.IsNullOrEmpty(id) && _state.UnlockedFeatureIds.Contains(id);
        }

        public bool IsEventUnlocked(string id)
        {
            return !string.IsNullOrEmpty(id) && _state.UnlockedEventIds.Contains(id);
        }

        public bool IsBattlePassUnlocked()
        {
            return IsFeatureUnlocked(PlayerProgressionConfig.BattlePassFeatureId);
        }

        public PlayerRankDefinition GetLastReachedMilestone()
        {
            return FindMilestone(true);
        }

        public PlayerRankDefinition GetNextMilestone()
        {
            return FindMilestone(false);
        }

        public void ApplyReachedUnlocks()
        {
            ApplyReachedUnlocks(ResolveFamilyProgression());
        }

        public void ApplyReachedUnlocks(GameProgressionState families)
        {
            if (config == null)
            {
                return;
            }

            var highest = Mathf.Max(1, _state.HighestReachedRank);
            var sorted = config.GetSortedRanks();
            for (var i = 0; i < sorted.Count; i++)
            {
                var definition = sorted[i];
                if (definition.rank > highest)
                {
                    break;
                }

                ApplyUnlocksForRank(definition.rank, families);
            }
        }

        public void DebugAddXp(long amount)
        {
            AddXp(amount);
        }

        public void DebugSetRank(int rank)
        {
            if (config == null)
            {
                return;
            }

            var min = config.GetMinRank();
            var max = config.GetMaxRank();
            rank = Mathf.Clamp(rank, min, max);
            ResetRuntimeState(false);
            _state.TotalXp = config.GetTotalXpRequiredToReach(rank);
            var snapshot = config.Evaluate(_state.TotalXp);
            _state.HighestReachedRank = snapshot.CurrentRank;
            ApplyReachedUnlocks();
            XpChanged?.Invoke();
        }

        public void DebugResetProgression()
        {
            ResetRuntimeState();
            XpChanged?.Invoke();
        }

        public void DebugClaimNextReward()
        {
            TryClaimNextReward();
        }

        void ResetRuntimeState(bool applyUnlocks = true)
        {
            _state.Reset();
            ResolveFamilyProgression()?.ResetDevelopment();
            if (config != null)
            {
                _state.HighestReachedRank = config.GetMinRank();
            }

            if (applyUnlocks)
            {
                ApplyReachedUnlocks();
            }
        }

        void ApplyUnlocksForRank(int rank)
        {
            ApplyUnlocksForRank(rank, ResolveFamilyProgression());
        }

        void ApplyUnlocksForRank(int rank, GameProgressionState families)
        {
            if (config == null || !config.TryGetRank(rank, out var definition) || definition.unlocks == null)
            {
                return;
            }

            for (var i = 0; i < definition.unlocks.Count; i++)
            {
                ApplyUnlock(definition.unlocks[i], families);
            }
        }

        void ApplyUnlock(RankUnlockData unlock, GameProgressionState families)
        {
            if (unlock == null)
            {
                return;
            }

            switch (unlock.type)
            {
                case RankUnlockType.MergeFamily:
                    if (families != null && families.UnlockFamily(unlock.mergeFamily))
                    {
                        ContentUnlocked?.Invoke(new RankContentUnlock(
                            RankUnlockType.MergeFamily,
                            unlock.mergeFamily,
                            unlock.mergeFamily.ToString()));
                    }

                    return;
                case RankUnlockType.Location:
                    if (TryAddContentId(_state.UnlockedLocationIds, unlock.contentId))
                    {
                        ContentUnlocked?.Invoke(new RankContentUnlock(RankUnlockType.Location, default, unlock.contentId));
                    }

                    return;
                case RankUnlockType.Feature:
                    if (TryAddContentId(_state.UnlockedFeatureIds, unlock.contentId))
                    {
                        ContentUnlocked?.Invoke(new RankContentUnlock(RankUnlockType.Feature, default, unlock.contentId));
                    }

                    return;
                case RankUnlockType.Event:
                    if (TryAddContentId(_state.UnlockedEventIds, unlock.contentId))
                    {
                        ContentUnlocked?.Invoke(new RankContentUnlock(RankUnlockType.Event, default, unlock.contentId));
                    }

                    return;
            }
        }

        static bool TryAddContentId(HashSet<string> set, string id)
        {
            return !string.IsNullOrEmpty(id) && set.Add(id);
        }

        static bool HasClaimableRewards(PlayerRankDefinition definition)
        {
            if (definition == null || definition.rewards == null)
            {
                return false;
            }

            for (var i = 0; i < definition.rewards.Count; i++)
            {
                if (definition.rewards[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        static void GrantReward(RankRewardData reward)
        {
            switch (reward.type)
            {
                case RankRewardType.Energy:
                    EnergySystem.Current?.Service.AddEnergy(ToIntAmount(reward.amount));
                    return;
                case RankRewardType.Coins:
                    CurrencySystem.Current?.Service.AddCoins(reward.amount);
                    return;
                case RankRewardType.GiftPlaceholder:
                    Debug.Log($"[PlayerProgression] Claimed gift placeholder '{reward.rewardKey}'.");
                    return;
            }
        }

        static int ToIntAmount(long amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            return amount > int.MaxValue ? int.MaxValue : (int)amount;
        }

        GameProgressionState ResolveFamilyProgression()
        {
            return OrderSystem.Current != null ? OrderSystem.Current.Progression : null;
        }

        PlayerRankDefinition FindMilestone(bool reached)
        {
            if (config == null)
            {
                return null;
            }

            var current = CurrentRank;
            var milestones = config.GetMilestoneRanks();
            PlayerRankDefinition lastReached = null;
            for (var i = 0; i < milestones.Count; i++)
            {
                var milestone = milestones[i];
                if (milestone.rank <= current)
                {
                    lastReached = milestone;
                    continue;
                }

                return reached ? lastReached : milestone;
            }

            return reached ? lastReached : null;
        }
    }
}
