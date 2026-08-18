using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "OrderDatabase", menuName = "San Island/Order Database")]
    public class OrderDatabase : ScriptableObject
    {
        public const int DefaultMaxActiveOrders = 6;

        [SerializeField] List<OrderDefinition> orders = new List<OrderDefinition>();
        [SerializeField] [Min(1)] int maxActiveOrders = DefaultMaxActiveOrders;

        [Header("Completion Presentation")]
        [SerializeField] [Range(0.5f, 1f)] float flightDuration = 0.76f;
        [SerializeField] [Range(0.04f, 0.16f)] float flightStagger = 0.1f;
        [SerializeField] [Range(80f, 220f)] float flightArcHeight = 118f;
        [SerializeField] [Range(16f, 56f)] float flightLift = 26f;
        [SerializeField] AnimationCurve flightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] [Range(1.02f, 1.08f)] float completePunchScale = 1.03f;
        [SerializeField] [Range(0.18f, 0.4f)] float completePunchDuration = 0.3f;
        [SerializeField] [Range(1.01f, 1.04f)] float readyPulseScale = 1.025f;
        [SerializeField] [Range(0.8f, 1.8f)] float readyPulseDuration = 1.2f;
        [SerializeField] [Range(0.18f, 0.45f)] float cardAppearDuration = 0.32f;
        [SerializeField] [Range(0.32f, 0.64f)] float cardHideDuration = 0.48f;
        [SerializeField] [Range(0.24f, 0.55f)] float cardSlideDuration = 0.4f;
        [SerializeField] [Range(0.12f, 0.4f)] float cardSlideStartDelay = 0.22f;
        [SerializeField] [Range(0.04f, 0.2f)] float cardSlideStagger = 0.08f;
        [SerializeField] [Range(0.84f, 0.96f)] float cardAppearScale = 0.9f;
        [SerializeField] [Range(0.12f, 0.32f)] float completeOverlayDuration = 0.22f;
        [SerializeField] bool useUnscaledTime = true;

        Dictionary<int, OrderDefinition> _byId;

        public IReadOnlyList<OrderDefinition> Orders => orders;
        public int MaxActiveOrders => maxActiveOrders > 0 ? maxActiveOrders : DefaultMaxActiveOrders;
        public float FlightDuration => flightDuration > 0f ? flightDuration : 0.76f;
        public float FlightStagger => flightStagger;
        public float FlightArcHeight => flightArcHeight;
        public float FlightLift => flightLift;
        public AnimationCurve FlightCurve => flightCurve;
        public float CompletePunchScale => completePunchScale;
        public float CompletePunchDuration => completePunchDuration;
        public float ReadyPulseScale => readyPulseScale;
        public float ReadyPulseDuration => readyPulseDuration;
        public float CardAppearDuration => cardAppearDuration > 0f ? cardAppearDuration : 0.32f;
        public float CardHideDuration => cardHideDuration > 0f ? cardHideDuration : 0.48f;
        public float CardSlideDuration => cardSlideDuration > 0f ? cardSlideDuration : 0.4f;
        public float CardSlideStartDelay => cardSlideStartDelay > 0f ? cardSlideStartDelay : 0.22f;
        public float CardSlideStagger => cardSlideStagger > 0f ? cardSlideStagger : 0.08f;
        public float CardAppearScale => cardAppearScale > 0f ? cardAppearScale : 0.9f;
        public float CompleteOverlayDuration => completeOverlayDuration > 0f ? completeOverlayDuration : 0.22f;
        public bool UseUnscaledTime => useUnscaledTime;

        void OnEnable()
        {
            RebuildLookups();
        }

        public void RebuildLookups()
        {
            _byId = new Dictionary<int, OrderDefinition>(orders != null ? orders.Count : 0);
            if (orders == null)
            {
                return;
            }

            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null || order.id == 0 || _byId.ContainsKey(order.id))
                {
                    continue;
                }

                _byId.Add(order.id, order);
            }
        }

        public bool TryGetById(int id, out OrderDefinition order)
        {
            EnsureLookups();
            return _byId.TryGetValue(id, out order);
        }

        public float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        public void EnsureDevelopmentOrders(MergeItemDatabase itemDatabase)
        {
            if (orders != null && orders.Count > 0)
            {
                RebuildLookups();
                return;
            }

            orders = new List<OrderDefinition>(5);
            AddDevelopmentOrder(10001, "order_001", 100, 10003, itemDatabase, "tools_l02", 1);
            AddDevelopmentOrder(10002, "order_002", 120, 10004, itemDatabase, "cleaning_l02", 1);
            AddDevelopmentOrder(10003, "order_003", 180, 10005, itemDatabase, "coffee_l03", 1);
            AddDevelopmentOrder(10004, "order_004", 250, OrderDefinition.NoNextOrderId, itemDatabase, "tools_l03", 1, "cleaning_l02", 1);
            AddDevelopmentOrder(10005, "order_005", 300, OrderDefinition.NoNextOrderId, itemDatabase, "coffee_l04", 1);
            RebuildLookups();
        }

        void AddDevelopmentOrder(
            int id,
            string internalKey,
            long reward,
            int nextOrderId,
            MergeItemDatabase itemDatabase,
            string itemKeyA,
            int amountA,
            string itemKeyB = null,
            int amountB = 0)
        {
            var definition = new OrderDefinition
            {
                id = id,
                internalKey = internalKey,
                localizationKey = string.Empty,
                coinReward = reward,
                nextOrderId = nextOrderId,
                enabled = true,
                requirements = new List<OrderRequirement>(2)
            };
            definition.requirements.Add(MakeRequirement(itemDatabase, itemKeyA, amountA));
            if (!string.IsNullOrEmpty(itemKeyB) && amountB > 0)
            {
                definition.requirements.Add(MakeRequirement(itemDatabase, itemKeyB, amountB));
            }

            orders.Add(definition);
        }

        static OrderRequirement MakeRequirement(MergeItemDatabase itemDatabase, string itemKey, int amount)
        {
            return new OrderRequirement
            {
                itemId = ResolveItemId(itemDatabase, itemKey),
                amount = amount < 1 ? 1 : amount
            };
        }

        static int ResolveItemId(MergeItemDatabase itemDatabase, string itemKey)
        {
            if (itemDatabase != null &&
                !string.IsNullOrEmpty(itemKey) &&
                itemDatabase.TryGetByKey(itemKey, out var item) &&
                item != null)
            {
                return item.Id;
            }

            if (itemKey == "tools_l02")
            {
                return MergeItemIdUtility.ComputeStableId(MergeItemFamily.Tools, MergeItemKind.Normal, 2);
            }

            if (itemKey == "tools_l03")
            {
                return MergeItemIdUtility.ComputeStableId(MergeItemFamily.Tools, MergeItemKind.Normal, 3);
            }

            if (itemKey == "cleaning_l02")
            {
                return MergeItemIdUtility.ComputeStableId(MergeItemFamily.Cleaning, MergeItemKind.Normal, 2);
            }

            if (itemKey == "coffee_l03")
            {
                return MergeItemIdUtility.ComputeStableId(MergeItemFamily.Coffee, MergeItemKind.Normal, 3);
            }

            if (itemKey == "coffee_l04")
            {
                return MergeItemIdUtility.ComputeStableId(MergeItemFamily.Coffee, MergeItemKind.Normal, 4);
            }

            return 0;
        }

        void EnsureLookups()
        {
            if (_byId == null)
            {
                RebuildLookups();
            }
        }
    }
}
