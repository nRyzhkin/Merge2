using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class OrdersHudView : MonoBehaviour
    {
        public const string OrdersRootName = "Group_Orders";

        [SerializeField] OrderCardView[] cards;

        OrderSystem _orders;
        MergeItemDatabase _itemDatabase;
        LayoutGroup _layout;
        bool _hasPresented;
        bool _layoutFrozen;
        bool _subscribedCards;
        bool _fillAfterMotion;

        public int CardCount => cards != null ? cards.Length : 0;

        public void BindLocal()
        {
            CollectCardsFromChildren();
            if (_layout == null)
            {
                _layout = GetComponent<LayoutGroup>();
            }

            if (cards == null)
            {
                return;
            }

            for (var i = 0; i < cards.Length; i++)
            {
                cards[i]?.BindLocal();
            }

            SubscribeCards();
        }

        public void RefreshNow()
        {
            Refresh(skipIfPresenting: false, animate: true, force: true);
        }

        public bool TryGetCard(int orderId, out OrderCardView card)
        {
            card = null;
            if (cards == null)
            {
                return false;
            }

            for (var i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null && cards[i].BoundOrderId == orderId)
                {
                    card = cards[i];
                    return true;
                }
            }

            return false;
        }

        void Awake()
        {
            BindLocal();
        }

        void OnEnable()
        {
            BindLocal();
            BindOrders(OrderSystem.Current);
            Refresh(skipIfPresenting: false, animate: true, force: false);
        }

        void OnDisable()
        {
            UnbindOrders();
            UnsubscribeCards();
            _fillAfterMotion = false;
            if (_layout != null)
            {
                _layout.enabled = true;
            }

            _layoutFrozen = false;
        }

        void LateUpdate()
        {
            if (_orders == null)
            {
                BindOrders(OrderSystem.Current);
                if (_orders == null)
                {
                    return;
                }

                Refresh(skipIfPresenting: false, animate: true, force: false);
            }

            if (_layoutFrozen && !AnyCardAnimatingLayout())
            {
                UnfreezeLayout();
            }

            if (_fillAfterMotion && !AnyCardAnimatingLayout() && !IsPresenterBusy())
            {
                _fillAfterMotion = false;
                Refresh(skipIfPresenting: true, animate: true, force: true);
            }
        }

        void BindOrders(OrderSystem system)
        {
            if (system == _orders && _orders != null)
            {
                return;
            }

            UnbindOrders();
            _orders = system;
            if (_orders == null)
            {
                return;
            }

            _orders.Changed += OnOrdersChanged;
            var board = _orders.GetComponent<BoardController>();
            _itemDatabase = board != null ? board.ItemDatabase : null;
        }

        void UnbindOrders()
        {
            if (_orders != null)
            {
                _orders.Changed -= OnOrdersChanged;
            }

            _orders = null;
        }

        void OnOrdersChanged()
        {
            Refresh(skipIfPresenting: true, animate: true, force: false);
        }

        void OnCardExitFinished(OrderCardView card)
        {
            if (AnyCardAnimatingLayout() || IsPresenterBusy())
            {
                _fillAfterMotion = true;
                return;
            }

            _fillAfterMotion = false;
            Refresh(skipIfPresenting: true, animate: true, force: true);
        }

        void Refresh(bool skipIfPresenting, bool animate, bool force)
        {
            if (skipIfPresenting && IsPresenterBusy())
            {
                return;
            }

            if (cards == null || cards.Length == 0)
            {
                BindLocal();
            }

            if (cards == null)
            {
                return;
            }

            SubscribeCards();
            var active = _orders != null ? _orders.State.activeOrderIds : null;
            var database = _orders != null ? _orders.Database : null;
            var assigned = new int[cards.Length];
            var taken = new HashSet<int>();
            var wasActive = new bool[cards.Length];
            var from = new Vector2[cards.Length];

            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null)
                {
                    continue;
                }

                wasActive[i] = card.gameObject.activeSelf && !card.IsExiting;
                if (card.Rect != null)
                {
                    from[i] = card.Rect.anchoredPosition;
                }

                var boundId = card.BoundOrderId;
                if (boundId != 0 && wasActive[i] && ContainsId(active, boundId) && taken.Add(boundId))
                {
                    assigned[i] = boundId;
                }
            }

            if (active != null)
            {
                for (var a = 0; a < active.Count; a++)
                {
                    var orderId = active[a];
                    if (taken.Contains(orderId))
                    {
                        continue;
                    }

                    var slot = FindFreeCard(assigned, wasActive, preferEmpty: true);
                    if (slot < 0)
                    {
                        slot = FindFreeCard(assigned, wasActive, preferEmpty: false);
                    }

                    if (slot < 0)
                    {
                        continue;
                    }

                    assigned[slot] = orderId;
                    taken.Add(orderId);
                }
            }

            if (!force && _hasPresented && animate && IdsUnchanged(assigned, active))
            {
                for (var i = 0; i < cards.Length; i++)
                {
                    var card = cards[i];
                    if (card == null || assigned[i] == 0)
                    {
                        continue;
                    }

                    card.SetReady(_orders != null && _orders.IsOrderReady(assigned[i]), animate: true);
                }

                return;
            }

            var firstPass = !_hasPresented;
            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null)
                {
                    continue;
                }

                if (assigned[i] == 0)
                {
                    if (wasActive[i] && animate && !firstPass)
                    {
                        _fillAfterMotion = true;
                        card.PlayExit();
                    }
                    else if (!card.IsExiting)
                    {
                        card.ShowEmptyImmediate();
                    }

                    continue;
                }

                OrderDefinition order = null;
                database?.TryGetById(assigned[i], out order);
                var ready = _orders != null && _orders.IsOrderReady(assigned[i]);
                var isNew = card.BoundOrderId != assigned[i] || !wasActive[i];
                if (card.HoldsCompletedVisual && card.BoundOrderId == assigned[i])
                {
                    continue;
                }

                if (!isNew)
                {
                    card.SetReady(ready, animate);
                    continue;
                }

                card.Present(assigned[i], order, _itemDatabase, ready);
                if (animate && (firstPass || isNew))
                {
                    card.PlayAppear(firstPass ? i * 0.05f : 0f);
                }
            }

            var sibling = 0;
            if (active != null)
            {
                for (var a = 0; a < active.Count; a++)
                {
                    for (var i = 0; i < cards.Length; i++)
                    {
                        if (assigned[i] == active[a] && cards[i] != null)
                        {
                            cards[i].transform.SetSiblingIndex(sibling++);
                        }
                    }
                }
            }

            for (var i = 0; i < cards.Length; i++)
            {
                if (assigned[i] == 0 && cards[i] != null)
                {
                    cards[i].SetLayoutIgnored(cards[i].IsExiting);
                    cards[i].transform.SetSiblingIndex(sibling++);
                }
            }

            if (_layout != null)
            {
                _layout.enabled = true;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);

            if (animate && !firstPass)
            {
                var slideCount = 0;
                var slideIndices = new int[cards.Length];
                for (var i = 0; i < cards.Length; i++)
                {
                    var card = cards[i];
                    if (card == null || assigned[i] == 0 || !wasActive[i] || card.IsExiting)
                    {
                        continue;
                    }

                    var to = card.Rect.anchoredPosition;
                    if ((to - from[i]).sqrMagnitude <= 1f)
                    {
                        continue;
                    }

                    slideIndices[slideCount++] = i;
                }

                for (var a = 0; a < slideCount - 1; a++)
                {
                    for (var b = a + 1; b < slideCount; b++)
                    {
                        if (from[slideIndices[a]].y >= from[slideIndices[b]].y)
                        {
                            continue;
                        }

                        var swap = slideIndices[a];
                        slideIndices[a] = slideIndices[b];
                        slideIndices[b] = swap;
                    }
                }

                var startDelay = database != null ? database.CardSlideStartDelay : 0.2f;
                var stagger = database != null ? database.CardSlideStagger : 0.1f;
                for (var s = 0; s < slideCount; s++)
                {
                    var i = slideIndices[s];
                    cards[i].PlaySlide(from[i], cards[i].Rect.anchoredPosition, startDelay + s * stagger);
                }

                if (slideCount > 0)
                {
                    FreezeLayout();
                }
            }

            _hasPresented = true;
        }

        bool IsPresenterBusy()
        {
            if (_orders == null)
            {
                return false;
            }

            var presenter = _orders.GetComponent<BoardOrderPresenter>();
            return presenter != null && presenter.IsPresenting;
        }

        int FindFreeCard(int[] assigned, bool[] wasActive, bool preferEmpty)
        {
            for (var i = 0; i < assigned.Length; i++)
            {
                if (assigned[i] != 0 || cards[i] == null || cards[i].IsExiting || cards[i].HoldsCompletedVisual)
                {
                    continue;
                }

                var empty = !wasActive[i];
                if (preferEmpty == empty)
                {
                    return i;
                }
            }

            return -1;
        }

        bool IdsUnchanged(int[] assigned, IReadOnlyList<int> active)
        {
            if (active == null)
            {
                return false;
            }

            var shown = 0;
            for (var i = 0; i < assigned.Length; i++)
            {
                var card = cards[i];
                if (card == null)
                {
                    continue;
                }

                if (assigned[i] == 0)
                {
                    if (card.gameObject.activeSelf && card.BoundOrderId != 0 && !card.IsExiting)
                    {
                        return false;
                    }

                    continue;
                }

                shown++;
                if (card.BoundOrderId != assigned[i])
                {
                    return false;
                }
            }

            return shown == active.Count;
        }

        static bool ContainsId(IReadOnlyList<int> ids, int orderId)
        {
            if (ids == null)
            {
                return false;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == orderId)
                {
                    return true;
                }
            }

            return false;
        }

        void FreezeLayout()
        {
            if (_layout == null)
            {
                _layout = GetComponent<LayoutGroup>();
            }

            if (_layout != null)
            {
                _layout.enabled = false;
            }

            _layoutFrozen = true;
        }

        void UnfreezeLayout()
        {
            _layoutFrozen = false;
            if (_layout != null)
            {
                _layout.enabled = true;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }

        bool AnyCardAnimatingLayout()
        {
            if (cards == null)
            {
                return false;
            }

            for (var i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null && cards[i].IsLayoutAnimating)
                {
                    return true;
                }
            }

            return false;
        }

        void SubscribeCards()
        {
            if (_subscribedCards || cards == null)
            {
                return;
            }

            for (var i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].ExitFinished += OnCardExitFinished;
                }
            }

            _subscribedCards = true;
        }

        void UnsubscribeCards()
        {
            if (!_subscribedCards || cards == null)
            {
                return;
            }

            for (var i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].ExitFinished -= OnCardExitFinished;
                }
            }

            _subscribedCards = false;
        }

        void CollectCardsFromChildren()
        {
            var found = new OrderCardView[transform.childCount];
            var count = 0;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null || !child.name.StartsWith("Order"))
                {
                    continue;
                }

                var card = child.GetComponent<OrderCardView>();
                if (card == null)
                {
                    card = child.gameObject.AddComponent<OrderCardView>();
                }

                found[count] = card;
                count++;
            }

            if (count == 0)
            {
                return;
            }

            cards = new OrderCardView[count];
            for (var i = 0; i < count; i++)
            {
                cards[i] = found[i];
            }
        }
    }
}
