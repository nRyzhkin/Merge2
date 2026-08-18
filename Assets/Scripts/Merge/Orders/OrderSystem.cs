using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public class OrderSystem : MonoBehaviour
    {
        public static OrderSystem Current { get; private set; }

        [SerializeField] BoardController boardController;
        [SerializeField] OrderDatabase database;

        readonly OrderState _state = new OrderState();
        readonly HashSet<int> _readyOrderIds = new HashSet<int>();
        readonly List<ConsumedBoardItem> _consumeScratch = new List<ConsumedBoardItem>(8);
        bool _wasDragBusy;

        public OrderState State => _state;
        public OrderDatabase Database
        {
            get
            {
                EnsureReady();
                return database;
            }
        }

        public event Action Changed;
        public event Action<int> Completed;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[OrderSystem] Duplicate OrderSystem in the scene. Keeping the first instance.");
            }
            else
            {
                Current = this;
            }

            EnsureReady();
            if (_state.activeOrderIds.Count == 0 &&
                _state.queuedOrderIds.Count == 0 &&
                _state.completedOrderIds.Count == 0)
            {
                ResetDevelopment();
            }
        }

        void OnEnable()
        {
            SubscribeBoard();
            RecalculateReadiness();
        }

        void OnDisable()
        {
            UnsubscribeBoard();
        }

        void OnDestroy()
        {
            UnsubscribeBoard();
            if (Current == this)
            {
                Current = null;
            }
        }

        void LateUpdate()
        {
            var dragBusy = boardController != null && boardController.IsDragInteractionActive;
            if (dragBusy == _wasDragBusy)
            {
                return;
            }

            _wasDragBusy = dragBusy;
            RecalculateReadiness();
        }

        public void Configure(BoardController controller, OrderDatabase orderDatabase)
        {
            UnsubscribeBoard();
            boardController = controller;
            database = orderDatabase;
            EnsureReady();
            SubscribeBoard();
            if (_state.activeOrderIds.Count == 0 &&
                _state.queuedOrderIds.Count == 0 &&
                _state.completedOrderIds.Count == 0)
            {
                ResetDevelopment();
                return;
            }

            RecalculateReadiness();
        }

        public void EnsureReady()
        {
            if (boardController == null)
            {
                boardController = GetComponent<BoardController>();
            }

            if (database == null)
            {
                return;
            }

            database.EnsureDevelopmentOrders(boardController != null ? boardController.ItemDatabase : null);
        }

        public void ResetDevelopment()
        {
            if (boardController == null)
            {
                boardController = GetComponent<BoardController>();
            }

            if (database != null)
            {
                database.EnsureDevelopmentOrders(boardController != null ? boardController.ItemDatabase : null);
            }

            _state.Clear();
            _readyOrderIds.Clear();
            if (database == null)
            {
                Changed?.Invoke();
                return;
            }

            var maxActive = GetMaxActiveOrders();
            var orders = database.Orders;
            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null || !order.enabled || order.id == 0)
                {
                    continue;
                }

                if (_state.activeOrderIds.Count < maxActive)
                {
                    _state.activeOrderIds.Add(order.id);
                }
                else
                {
                    _state.queuedOrderIds.Add(order.id);
                }
            }

            RecalculateReadiness();
        }

        public bool IsOrderReady(int orderId)
        {
            return _readyOrderIds.Contains(orderId);
        }

        public bool CanCompleteOrder(int orderId)
        {
            return IsOrderReady(orderId);
        }

        public bool TryGetReadyOrderIdForItem(int itemId, out int orderId)
        {
            orderId = 0;
            if (itemId == BoardCellState.EmptyItemId || database == null)
            {
                return false;
            }

            for (var i = 0; i < _state.activeOrderIds.Count; i++)
            {
                var candidateId = _state.activeOrderIds[i];
                if (!_readyOrderIds.Contains(candidateId) ||
                    !database.TryGetById(candidateId, out var order) ||
                    order == null ||
                    !OrderRequiresItem(order, itemId))
                {
                    continue;
                }

                orderId = candidateId;
                return true;
            }

            return false;
        }

        public void CopyReadyHighlightCells(List<int> cells)
        {
            if (cells == null)
            {
                return;
            }

            cells.Clear();
            if (boardController == null || database == null)
            {
                return;
            }

            boardController.CollectReadyOrderHighlightCells(_state.activeOrderIds, _readyOrderIds, database, cells);
        }

        public bool TryActivateOrder(int orderId)
        {
            EnsureReady();
            if (database == null || !database.TryGetById(orderId, out var order) || order == null || !order.enabled)
            {
                return false;
            }

            if (_state.Contains(orderId))
            {
                return false;
            }

            if (_state.activeOrderIds.Count < GetMaxActiveOrders())
            {
                _state.activeOrderIds.Add(orderId);
            }
            else
            {
                _state.queuedOrderIds.Add(orderId);
            }

            RecalculateReadiness();
            return true;
        }

        public bool TryCompleteOrder(int orderId)
        {
            EnsureReady();
            if (boardController == null || database == null || !database.TryGetById(orderId, out var order) || order == null)
            {
                return false;
            }

            if (!_state.activeOrderIds.Contains(orderId) || !EvaluateReady(order))
            {
                ShowItemsMissing();
                RecalculateReadiness();
                return false;
            }

            var drag = boardController.DragController;
            var dragSource = drag != null && drag.Phase == BoardDragPhase.Dragging
                ? drag.ActiveSourceIndex
                : BoardController.NoSelectionIndex;
            var hasFlightOrigin = false;
            var flightOrigin = Vector2.zero;
            if (dragSource != BoardController.NoSelectionIndex && boardController.DragView != null)
            {
                flightOrigin = boardController.DragView.CurrentDragPosition;
                hasFlightOrigin = true;
            }

            var presenter = GetComponent<BoardOrderPresenter>();
            OrderCardView card = null;
            presenter?.HudView?.TryGetCard(orderId, out card);
            card?.HoldCompletedVisual();

            if (!boardController.TryConsumeOrderItems(order.requirements, _consumeScratch, dragSource))
            {
                card?.ReleaseCompletedHold();
                ShowItemsMissing();
                RecalculateReadiness();
                return false;
            }

            var consumedDragSource = false;
            for (var i = 0; i < _consumeScratch.Count; i++)
            {
                if (_consumeScratch[i].CellIndex == dragSource)
                {
                    consumedDragSource = true;
                    break;
                }
            }

            var currency = GetCurrency();
            if (currency != null && order.coinReward > 0)
            {
                currency.AddCoins(order.coinReward);
            }

            presenter?.PlayComplete(
                orderId,
                order,
                _consumeScratch,
                card,
                consumedDragSource && hasFlightOrigin ? dragSource : BoardController.NoSelectionIndex,
                flightOrigin,
                consumedDragSource && hasFlightOrigin);

            if (consumedDragSource && drag != null && drag.Phase == BoardDragPhase.Dragging)
            {
                drag.CompleteDropAsConsumed();
            }

            _state.activeOrderIds.Remove(orderId);
            if (!_state.completedOrderIds.Contains(orderId))
            {
                _state.completedOrderIds.Add(orderId);
            }

            EnqueueNext(order.nextOrderId);
            DrainQueue();
            RecalculateReadiness();
            Completed?.Invoke(orderId);
            return true;
        }

        public void RecalculateReadiness()
        {
            _readyOrderIds.Clear();
            if (database == null || boardController == null)
            {
                Changed?.Invoke();
                return;
            }

            for (var i = 0; i < _state.activeOrderIds.Count; i++)
            {
                var orderId = _state.activeOrderIds[i];
                if (database.TryGetById(orderId, out var order) && EvaluateReady(order))
                {
                    _readyOrderIds.Add(orderId);
                }
            }

            Changed?.Invoke();
        }

        bool EvaluateReady(OrderDefinition order)
        {
            if (order == null || order.requirements == null || order.requirements.Count == 0 || boardController == null)
            {
                return false;
            }

            return boardController.CanFulfillOrderRequirements(order.requirements);
        }

        void EnqueueNext(int nextOrderId)
        {
            if (nextOrderId == OrderDefinition.NoNextOrderId || database == null)
            {
                return;
            }

            if (!database.TryGetById(nextOrderId, out var next) || next == null || !next.enabled)
            {
                return;
            }

            if (_state.Contains(nextOrderId))
            {
                return;
            }

            _state.queuedOrderIds.Insert(0, nextOrderId);
        }

        void DrainQueue()
        {
            if (database == null)
            {
                return;
            }

            var maxActive = GetMaxActiveOrders();
            while (_state.activeOrderIds.Count < maxActive && _state.queuedOrderIds.Count > 0)
            {
                var nextId = _state.queuedOrderIds[0];
                _state.queuedOrderIds.RemoveAt(0);
                if (_state.activeOrderIds.Contains(nextId) || _state.completedOrderIds.Contains(nextId))
                {
                    continue;
                }

                if (!database.TryGetById(nextId, out var next) || next == null || !next.enabled)
                {
                    continue;
                }

                _state.activeOrderIds.Add(nextId);
            }
        }

        int GetMaxActiveOrders()
        {
            return database != null ? database.MaxActiveOrders : OrderDatabase.DefaultMaxActiveOrders;
        }

        static bool OrderRequiresItem(OrderDefinition order, int itemId)
        {
            if (order == null || order.requirements == null)
            {
                return false;
            }

            for (var i = 0; i < order.requirements.Count; i++)
            {
                var requirement = order.requirements[i];
                if (requirement != null && requirement.itemId == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        CurrencyService GetCurrency()
        {
            var currencySystem = GetComponent<CurrencySystem>();
            if (currencySystem == null)
            {
                currencySystem = CurrencySystem.Current;
            }

            return currencySystem != null ? currencySystem.Service : null;
        }

        void ShowItemsMissing()
        {
            if (boardController == null)
            {
                return;
            }

            boardController.EnsureMessagesReady();
            var screen = boardController.SelectedCellIndex != BoardController.NoSelectionIndex
                ? boardController.GetCellScreenPosition(boardController.SelectedCellIndex)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            boardController.MessagePresenter?.ShowLocalized(
                MessageKind.Warning,
                MergeUiLocalization.OrderItemsMissingKey,
                screen);
        }

        void SubscribeBoard()
        {
            if (boardController == null)
            {
                return;
            }

            boardController.BoardContentsChanged += RecalculateReadiness;
            boardController.InteractionLocks.Changed += RecalculateReadiness;
        }

        void UnsubscribeBoard()
        {
            if (boardController == null)
            {
                return;
            }

            boardController.BoardContentsChanged -= RecalculateReadiness;
            boardController.InteractionLocks.Changed -= RecalculateReadiness;
        }
    }
}
