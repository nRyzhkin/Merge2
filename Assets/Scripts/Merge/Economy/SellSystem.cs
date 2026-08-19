using System;
using UnityEngine;

namespace SanIsland.Merge
{
    [DefaultExecutionOrder(-160)]
    [DisallowMultipleComponent]
    public class SellSystem : MonoBehaviour
    {
        public static SellSystem Current { get; private set; }

        [SerializeField] BoardController boardController;
        [SerializeField] EconomyConfig config;

        CurrencyService _currency;
        SoldItemSnapshot _lastSale;
        bool _hasLastSale;
        float _lastSaleExpireUnscaled;

        public EconomyConfig Config
        {
            get
            {
                EnsureReady();
                return config;
            }
        }

        public CurrencyService Currency
        {
            get
            {
                EnsureReady();
                return _currency;
            }
        }

        public event Action Changed;
        public event Action<SoldItemSnapshot> Sold;
        public event Action<SoldItemSnapshot, int> Restored;

        public bool CanUndoLastSale => HasValidUndoRecord() && HasRestoreSpace() && Currency.CanSpendCoins(_lastSale.SalePrice);
        public float UndoSecondsRemaining =>
            HasValidUndoRecord() ? Mathf.Max(0f, _lastSaleExpireUnscaled - Time.unscaledTime) : 0f;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[SellSystem] Duplicate SellSystem in the scene. Keeping the first instance.");
            }
            else
            {
                Current = this;
            }

            EnsureReady();
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        public void Configure(BoardController controller, EconomyConfig economyConfig)
        {
            boardController = controller;
            config = economyConfig;
            EnsureReady();
        }

        public void EnsureReady()
        {
            if (boardController == null)
            {
                boardController = GetComponent<BoardController>();
            }

            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EconomyConfig>();
            }

            if (_currency == null)
            {
                var currencySystem = GetComponent<CurrencySystem>();
                if (currencySystem == null)
                {
                    currencySystem = CurrencySystem.Current;
                }

                if (currencySystem == null)
                {
                    currencySystem = gameObject.AddComponent<CurrencySystem>();
                }

                currencySystem.EnsureReady();
                _currency = currencySystem.Service;
            }
        }

        public void ClearLastSale()
        {
            _hasLastSale = false;
            _lastSale = default;
            _lastSaleExpireUnscaled = 0f;
            Changed?.Invoke();
        }

        public bool CanSellSelected()
        {
            EnsureReady();
            return boardController != null && boardController.CanSellSelected();
        }

        public long GetSelectedSellPrice()
        {
            EnsureReady();
            return boardController != null ? boardController.GetSelectedSellPrice(config) : 0;
        }

        public bool TrySellSelected()
        {
            EnsureReady();
            if (boardController == null || !boardController.TrySellSelected(config, out var snapshot, out var sprite, out var size))
            {
                return false;
            }

            Currency.AddCoins(snapshot.SalePrice);
            BeginUndoWindow(snapshot);
            var presenter = GetComponent<BoardSellPresenter>();
            presenter?.PlaySell(snapshot, sprite, size);
            Sold?.Invoke(snapshot);
            Changed?.Invoke();
            return true;
        }

        public bool TrySellCell(int cellIndex, bool ignoreDragBusy = false, Vector2? popupLayerPosition = null)
        {
            EnsureReady();
            if (boardController == null || !boardController.TrySellCell(cellIndex, config, out var snapshot, out var sprite, out var size, ignoreDragBusy))
            {
                return false;
            }

            Currency.AddCoins(snapshot.SalePrice);
            BeginUndoWindow(snapshot);
            var presenter = GetComponent<BoardSellPresenter>();
            presenter?.PlaySell(snapshot, sprite, size, popupLayerPosition);
            Sold?.Invoke(snapshot);
            Changed?.Invoke();
            return true;
        }

        public bool CanSellCell(int cellIndex, bool ignoreDragBusy = false)
        {
            EnsureReady();
            return boardController != null && boardController.CanSellCell(cellIndex, config, ignoreDragBusy);
        }

        public bool TryUndoLastSale()
        {
            EnsureReady();
            if (!HasValidUndoRecord())
            {
                return false;
            }

            if (!HasRestoreSpace())
            {
                ShowUndoNoSpace();
                return false;
            }

            if (!Currency.CanSpendCoins(_lastSale.SalePrice))
            {
                return false;
            }

            var snapshot = _lastSale;
            if (!Currency.TrySpendCoins(snapshot.SalePrice))
            {
                return false;
            }

            if (!boardController.TryRestoreSoldItem(snapshot, out var placedIndex))
            {
                Currency.AddCoins(snapshot.SalePrice);
                ShowUndoNoSpace();
                return false;
            }

            _hasLastSale = false;
            _lastSale = default;
            _lastSaleExpireUnscaled = 0f;
            var presenter = GetComponent<BoardSellPresenter>();
            presenter?.PlayUndoReturn(placedIndex, snapshot);
            Restored?.Invoke(snapshot, placedIndex);
            Changed?.Invoke();
            return true;
        }

        public bool HasValidUndoRecord()
        {
            if (!_hasLastSale)
            {
                return false;
            }

            if (Time.unscaledTime >= _lastSaleExpireUnscaled)
            {
                _hasLastSale = false;
                _lastSale = default;
                Changed?.Invoke();
                return false;
            }

            return true;
        }

        void BeginUndoWindow(SoldItemSnapshot snapshot)
        {
            _lastSale = snapshot;
            _hasLastSale = true;
            _lastSaleExpireUnscaled = Time.unscaledTime + config.UndoWindowSeconds;
        }

        bool HasRestoreSpace()
        {
            return boardController != null &&
                   boardController.FindRestoreCell(_lastSale.OriginalCellIndex) != BoardController.NoSelectionIndex;
        }

        void ShowUndoNoSpace()
        {
            if (boardController == null)
            {
                return;
            }

            boardController.EnsureMessagesReady();
            var screen = boardController.GetCellScreenPosition(_lastSale.OriginalCellIndex);
            boardController.MessagePresenter?.ShowLocalized(
                MessageKind.Info,
                MergeUiLocalization.UndoNoSpaceKey,
                screen);
        }
    }
}
