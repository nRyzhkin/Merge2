using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [Serializable]
    public class OrderRequirement
    {
        public int itemId;
        public int amount = 1;
    }

    [Serializable]
    public class OrderDefinition
    {
        public const int NoNextOrderId = -1;

        public int id;
        public string internalKey;
        public string localizationKey;
        public List<OrderRequirement> requirements = new List<OrderRequirement>();
        public long coinReward;
        public int nextOrderId = NoNextOrderId;
        public bool enabled = true;
    }

    [Serializable]
    public class OrderState
    {
        public List<int> activeOrderIds = new List<int>(4);
        public List<int> queuedOrderIds = new List<int>(8);
        public List<int> completedOrderIds = new List<int>(32);

        public void Clear()
        {
            activeOrderIds.Clear();
            queuedOrderIds.Clear();
            completedOrderIds.Clear();
        }

        public bool Contains(int orderId)
        {
            return activeOrderIds.Contains(orderId) ||
                   queuedOrderIds.Contains(orderId) ||
                   completedOrderIds.Contains(orderId);
        }
    }

    public struct ConsumedBoardItem
    {
        public int CellIndex;
        public int ItemId;
        public int RequirementIndex;
        public Sprite Sprite;
        public Vector2 Size;
    }
}
