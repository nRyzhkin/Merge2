using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    public enum OrderDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    [Serializable]
    public class FamilyOrderSettings
    {
        public MergeItemFamily family = MergeItemFamily.Tools;
        public bool enabledForOrders = true;
        [Min(0)] public int weight = 70;
    }

    [Serializable]
    public class OrderDifficultyBand
    {
        [Min(1)] public int minCost = 2;
        [Min(1)] public int maxCost = 6;
        [Range(1, 2)] public int minRequirements = 1;
        [Range(1, 2)] public int maxRequirements = 1;
        [Range(0f, 1f)] public float multiFamilyChance;
        [Min(1)] public int rewardMultiplier = 16;
    }

    [CreateAssetMenu(fileName = "OrderGenerationConfig", menuName = "San Island/Order Generation Config")]
    public class OrderGenerationConfig : ScriptableObject
    {
        [Tooltip("One active generated order per entry. UI can have more card slots; unused cards stay hidden.")]
        [SerializeField] List<OrderDifficulty> slotDifficulties = new List<OrderDifficulty>
        {
            OrderDifficulty.Easy,
            OrderDifficulty.Medium,
            OrderDifficulty.Medium
        };

        [SerializeField] List<FamilyOrderSettings> families = new List<FamilyOrderSettings>
        {
            new FamilyOrderSettings { family = MergeItemFamily.Tools, enabledForOrders = true, weight = 70 },
            new FamilyOrderSettings { family = MergeItemFamily.Cleaning, enabledForOrders = true, weight = 70 },
            new FamilyOrderSettings { family = MergeItemFamily.Coffee, enabledForOrders = true, weight = 100 }
        };

        [SerializeField] OrderDifficultyBand easy = new OrderDifficultyBand
        {
            minCost = 2,
            maxCost = 6,
            minRequirements = 1,
            maxRequirements = 1,
            multiFamilyChance = 0f,
            rewardMultiplier = 16
        };

        [SerializeField] OrderDifficultyBand medium = new OrderDifficultyBand
        {
            minCost = 7,
            maxCost = 16,
            minRequirements = 1,
            maxRequirements = 2,
            multiFamilyChance = 0.25f,
            rewardMultiplier = 14
        };

        [SerializeField] OrderDifficultyBand hard = new OrderDifficultyBand
        {
            minCost = 17,
            maxCost = 32,
            minRequirements = 1,
            maxRequirements = 2,
            multiFamilyChance = 0.25f,
            rewardMultiplier = 12
        };

        [SerializeField] [Min(1)] int maxGenerationAttempts = 24;
        [SerializeField] [Range(1, 2)] int maxAmountPerRequirement = 2;
        [SerializeField] [Min(1f)] float minimumOrderVsSellMultiplier = 1.25f;

        public int SlotCount => slotDifficulties != null && slotDifficulties.Count > 0 ? slotDifficulties.Count : 3;
        public IReadOnlyList<OrderDifficulty> SlotDifficulties => slotDifficulties;
        public IReadOnlyList<FamilyOrderSettings> Families => families;
        public int MaxGenerationAttempts => maxGenerationAttempts > 0 ? maxGenerationAttempts : 24;
        public int MaxAmountPerRequirement => maxAmountPerRequirement < 1 ? 1 : maxAmountPerRequirement;
        public float MinimumOrderVsSellMultiplier => minimumOrderVsSellMultiplier < 1f ? 1f : minimumOrderVsSellMultiplier;

        public OrderDifficulty GetSlotDifficulty(int slotIndex)
        {
            if (slotDifficulties == null || slotDifficulties.Count == 0)
            {
                return OrderDifficulty.Easy;
            }

            if (slotIndex < 0)
            {
                slotIndex = 0;
            }

            if (slotIndex >= slotDifficulties.Count)
            {
                slotIndex = slotDifficulties.Count - 1;
            }

            return slotDifficulties[slotIndex];
        }

        public OrderDifficultyBand GetBand(OrderDifficulty difficulty)
        {
            switch (difficulty)
            {
                case OrderDifficulty.Hard:
                    return hard;
                case OrderDifficulty.Medium:
                    return medium;
                default:
                    return easy;
            }
        }

        public int GetFamilyWeight(MergeItemFamily family)
        {
            if (families == null)
            {
                return 0;
            }

            for (var i = 0; i < families.Count; i++)
            {
                var settings = families[i];
                if (settings != null && settings.family == family)
                {
                    return settings.enabledForOrders ? settings.weight : 0;
                }
            }

            return 0;
        }
    }
}
