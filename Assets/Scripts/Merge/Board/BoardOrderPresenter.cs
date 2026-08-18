using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardOrderPresenter : MonoBehaviour
    {
        const int InitialPool = 4;

        sealed class CompleteSequence
        {
            public int OrderId;
            public OrderCardView Card;
            public readonly List<OrderItemFlightView> Flights = new List<OrderItemFlightView>(4);
            public CoinPopupView Popup;
            public bool FlightsDone;
            public bool PopupStarted;
            public bool HudRefreshed;
            public float AfterFlightsElapsed;
        }

        [SerializeField] BoardController boardController;
        [SerializeField] BoardDragView dragView;
        [SerializeField] OrderDatabase database;
        [SerializeField] CoinPopupView popupPrefab;
        [SerializeField] OrdersHudView hudView;

        readonly List<CompleteSequence> _active = new List<CompleteSequence>(4);
        readonly List<OrderItemFlightView> _flightPool = new List<OrderItemFlightView>(8);
        readonly List<CoinPopupView> _popupPool = new List<CoinPopupView>(4);
        readonly List<CompleteSequence> _scratch = new List<CompleteSequence>(4);
        RectTransform _poolRoot;

        public OrdersHudView HudView => hudView;
        public bool IsPresenting => _active.Count > 0;

        public void Configure(
            BoardController controller,
            BoardDragView view,
            OrderDatabase orderDatabase,
            CoinPopupView coinPopupPrefab,
            OrdersHudView hud)
        {
            boardController = controller;
            dragView = view;
            database = orderDatabase;
            if (coinPopupPrefab != null)
            {
                popupPrefab = coinPopupPrefab;
            }

            hudView = hud;
            EnsurePools();
        }

        public void PlayComplete(
            int orderId,
            OrderDefinition order,
            IReadOnlyList<ConsumedBoardItem> consumed,
            OrderCardView card,
            int flightOriginCellIndex = BoardController.NoSelectionIndex,
            Vector2 flightOriginLayer = default,
            bool hasFlightOrigin = false)
        {
            if (boardController == null || boardController.BoardView == null || dragView == null || consumed == null)
            {
                return;
            }

            EnsurePools();
            if (card == null && hudView != null)
            {
                hudView.TryGetCard(orderId, out card);
            }

            card?.HoldCompletedVisual();

            var sequence = new CompleteSequence
            {
                OrderId = orderId,
                Card = card
            };

            var stagger = database != null ? database.FlightStagger : 0.08f;
            for (var i = 0; i < consumed.Count; i++)
            {
                var item = consumed[i];
                var flight = RentFlight();
                if (flight == null)
                {
                    continue;
                }

                var start = hasFlightOrigin && item.CellIndex == flightOriginCellIndex
                    ? flightOriginLayer
                    : GetCellLayerPosition(item.CellIndex);
                var end = start;
                if (card == null || !card.TryGetRequirementLayerPosition(item.RequirementIndex, dragView, out end))
                {
                    end = start + new Vector2(180f, 80f);
                }

                flight.Begin(item.Sprite, item.Size, start, end, i * stagger, database);
                sequence.Flights.Add(flight);
            }

            sequence.FlightsDone = sequence.Flights.Count == 0;
            if (sequence.FlightsDone)
            {
                BeginCardCompletion(sequence, order);
            }

            _active.Add(sequence);
            enabled = true;
        }

        public void AbortAll()
        {
            _scratch.Clear();
            for (var i = 0; i < _active.Count; i++)
            {
                _scratch.Add(_active[i]);
            }

            for (var i = 0; i < _scratch.Count; i++)
            {
                Finish(_scratch[i], refreshHud: false);
            }

            _scratch.Clear();
            enabled = false;
        }

        void Update()
        {
            if (_active.Count == 0)
            {
                enabled = false;
                return;
            }

            var dt = database != null ? database.GetDeltaTime() : Time.unscaledDeltaTime;
            _scratch.Clear();
            for (var i = 0; i < _active.Count; i++)
            {
                _scratch.Add(_active[i]);
            }

            for (var i = 0; i < _scratch.Count; i++)
            {
                Tick(_scratch[i], dt);
            }

            _scratch.Clear();
            if (_active.Count == 0)
            {
                enabled = false;
            }
        }

        void Tick(CompleteSequence sequence, float dt)
        {
            if (!sequence.FlightsDone)
            {
                var busy = false;
                for (var i = 0; i < sequence.Flights.Count; i++)
                {
                    if (sequence.Flights[i] != null && sequence.Flights[i].Tick(dt))
                    {
                        busy = true;
                    }
                }

                if (busy)
                {
                    return;
                }

                sequence.FlightsDone = true;
                BeginCardCompletion(sequence, null);
            }

            sequence.AfterFlightsElapsed += dt;
            var popupBusy = sequence.Popup != null && sequence.Popup.Tick(dt);
            if (!sequence.PopupStarted)
            {
                return;
            }

            var cardBusy = sequence.Card != null && sequence.Card.IsCompletionPlaying;
            if (!sequence.HudRefreshed && !cardBusy && !AnySequenceBlockingHud())
            {
                sequence.HudRefreshed = true;
                RefreshHud(sequence);
            }

            if ((popupBusy || !sequence.HudRefreshed) && sequence.AfterFlightsElapsed < 2.5f)
            {
                return;
            }

            Finish(sequence, refreshHud: !sequence.HudRefreshed);
        }

        bool AnySequenceBlockingHud()
        {
            for (var i = 0; i < _active.Count; i++)
            {
                var other = _active[i];
                if (other == null || other.FlightsDone && (other.Card == null || !other.Card.IsCompletionPlaying))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        void BeginCardCompletion(CompleteSequence sequence, OrderDefinition order)
        {
            if (sequence.PopupStarted)
            {
                return;
            }

            sequence.PopupStarted = true;
            if (sequence.Card != null)
            {
                sequence.Card.PlayCompletion();
            }

            var reward = 0L;
            if (order != null)
            {
                reward = order.coinReward;
            }
            else if (OrderSystem.Current != null &&
                     OrderSystem.Current.TryGetOrder(sequence.OrderId, out var found) &&
                     found != null)
            {
                reward = found.coinReward;
            }

            if (reward <= 0)
            {
                return;
            }

            sequence.Popup = RentPopup();
            if (sequence.Popup == null)
            {
                return;
            }

            var pos = sequence.Card != null
                ? sequence.Card.GetRewardLayerPosition(dragView)
                : Vector2.zero;
            var economy = SellSystem.Current != null ? SellSystem.Current.Config : null;
            sequence.Popup.Play($"+{reward}", pos, economy);
        }

        void RefreshHud(CompleteSequence sequence)
        {
            var hud = hudView;
            if (hud == null && sequence != null && sequence.Card != null)
            {
                hud = sequence.Card.GetComponentInParent<OrdersHudView>();
            }

            hud?.RefreshNow();
        }

        void Finish(CompleteSequence sequence, bool refreshHud)
        {
            for (var i = 0; i < sequence.Flights.Count; i++)
            {
                sequence.Flights[i]?.HideImmediate();
            }

            sequence.Flights.Clear();
            if (sequence.Popup != null)
            {
                sequence.Popup.HideImmediate();
            }

            _active.Remove(sequence);
            if (refreshHud && _active.Count == 0)
            {
                RefreshHud(sequence);
            }
        }

        Vector2 GetCellLayerPosition(int index)
        {
            if (dragView == null || boardController == null || boardController.BoardView == null)
            {
                return Vector2.zero;
            }

            var cell = boardController.BoardView.GetCellView(index);
            if (cell == null)
            {
                return Vector2.zero;
            }

            var rect = cell.ItemImage != null
                ? cell.ItemImage.rectTransform
                : cell.transform as RectTransform;
            return dragView.WorldToLayer(rect.TransformPoint(rect.rect.center));
        }

        void EnsurePools()
        {
            if (dragView == null)
            {
                return;
            }

            if (_poolRoot == null)
            {
                var parent = dragView.transform;
                var existing = parent.Find("OrderPresentationPool") as RectTransform;
                if (existing != null)
                {
                    _poolRoot = existing;
                }
                else
                {
                    var go = new GameObject("OrderPresentationPool", typeof(RectTransform));
                    go.layer = parent.gameObject.layer;
                    _poolRoot = go.GetComponent<RectTransform>();
                    _poolRoot.SetParent(parent, false);
                    _poolRoot.anchorMin = Vector2.zero;
                    _poolRoot.anchorMax = Vector2.one;
                    _poolRoot.offsetMin = Vector2.zero;
                    _poolRoot.offsetMax = Vector2.zero;
                }
            }

            while (_flightPool.Count < InitialPool)
            {
                CreateFlight();
            }

            while (_popupPool.Count < 2)
            {
                CreatePopup();
            }
        }

        OrderItemFlightView RentFlight()
        {
            for (var i = 0; i < _flightPool.Count; i++)
            {
                if (_flightPool[i] != null && !_flightPool[i].IsPlaying)
                {
                    return _flightPool[i];
                }
            }

            return CreateFlight();
        }

        CoinPopupView RentPopup()
        {
            for (var i = 0; i < _popupPool.Count; i++)
            {
                if (_popupPool[i] != null && !_popupPool[i].IsPlaying)
                {
                    return _popupPool[i];
                }
            }

            return CreatePopup();
        }

        OrderItemFlightView CreateFlight()
        {
            if (_poolRoot == null)
            {
                return null;
            }

            var go = new GameObject($"OrderFlight_{_flightPool.Count}", typeof(RectTransform), typeof(CanvasGroup));
            go.layer = _poolRoot.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_poolRoot, false);
            var flight = go.AddComponent<OrderItemFlightView>();
            flight.Ensure(_poolRoot);
            _flightPool.Add(flight);
            return flight;
        }

        CoinPopupView CreatePopup()
        {
            if (_poolRoot == null || popupPrefab == null)
            {
                return null;
            }

            var instance = Instantiate(popupPrefab, _poolRoot, false);
            instance.name = $"OrderCoinPopup_{_popupPool.Count}";
            instance.gameObject.layer = _poolRoot.gameObject.layer;
            instance.Ensure();
            _popupPool.Add(instance);
            return instance;
        }
    }
}
