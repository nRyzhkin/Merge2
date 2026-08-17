using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardSellPresenter : MonoBehaviour
    {
        const int InitialPool = 2;

        sealed class SellSequence
        {
            public bool Active;
            public int CellIndex;
            public SellGhostView Ghost;
            public CoinPopupView Popup;
        }

        [SerializeField] BoardController boardController;
        [SerializeField] BoardDragView dragView;
        [SerializeField] EconomyConfig config;
        [SerializeField] CoinPopupView popupPrefab;

        readonly List<SellSequence> _active = new List<SellSequence>(4);
        readonly List<SellGhostView> _ghostPool = new List<SellGhostView>(4);
        readonly List<CoinPopupView> _popupPool = new List<CoinPopupView>(4);
        readonly List<SellSequence> _scratch = new List<SellSequence>(4);
        RectTransform _poolRoot;

        public void Configure(BoardController controller, BoardDragView view, EconomyConfig economyConfig, CoinPopupView coinPopupPrefab = null)
        {
            boardController = controller;
            dragView = view;
            config = economyConfig;
            if (coinPopupPrefab != null)
            {
                popupPrefab = coinPopupPrefab;
            }

            EnsurePools();
        }

        public void PlaySell(SoldItemSnapshot snapshot, Sprite sprite, Vector2 size)
        {
            if (boardController == null || boardController.BoardView == null || dragView == null)
            {
                return;
            }

            EnsurePools();
            var sequence = new SellSequence
            {
                Active = true,
                CellIndex = snapshot.OriginalCellIndex,
                Ghost = RentGhost(),
                Popup = RentPopup()
            };

            var pos = GetCellLayerPosition(snapshot.OriginalCellIndex);
            if (sequence.Ghost != null)
            {
                sequence.Ghost.Play(sprite, size, pos, Vector2.one, Config());
            }

            if (sequence.Popup != null)
            {
                sequence.Popup.Play($"+{snapshot.SalePrice}", pos + new Vector2(0f, 24f), Config());
            }

            _active.Add(sequence);
            enabled = true;
        }

        public void PlayUndoReturn(int cellIndex, SoldItemSnapshot snapshot)
        {
            if (boardController == null || boardController.BoardView == null)
            {
                return;
            }

            var view = boardController.BoardView.GetCellView(cellIndex);
            var animator = view != null ? view.ItemAnimator : null;
            animator?.PlayUndoReturn(Config());
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
                Finish(_scratch[i]);
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

            var dt = Config() != null ? Config().GetDeltaTime() : Time.unscaledDeltaTime;
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

        void Tick(SellSequence sequence, float dt)
        {
            var ghostBusy = sequence.Ghost != null && sequence.Ghost.Tick(dt);
            var popupBusy = sequence.Popup != null && sequence.Popup.Tick(dt);
            if (ghostBusy || popupBusy)
            {
                return;
            }

            Finish(sequence);
        }

        void Finish(SellSequence sequence)
        {
            if (sequence.Ghost != null)
            {
                sequence.Ghost.HideImmediate();
            }

            if (sequence.Popup != null)
            {
                sequence.Popup.HideImmediate();
            }

            sequence.Active = false;
            _active.Remove(sequence);
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

        EconomyConfig Config()
        {
            if (config == null && SellSystem.Current != null)
            {
                config = SellSystem.Current.Config;
            }

            return config;
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
                var existing = parent.Find("SellPresentationPool") as RectTransform;
                if (existing != null)
                {
                    _poolRoot = existing;
                }
                else
                {
                    var go = new GameObject("SellPresentationPool", typeof(RectTransform));
                    go.layer = parent.gameObject.layer;
                    _poolRoot = go.GetComponent<RectTransform>();
                    _poolRoot.SetParent(parent, false);
                    _poolRoot.anchorMin = Vector2.zero;
                    _poolRoot.anchorMax = Vector2.one;
                    _poolRoot.offsetMin = Vector2.zero;
                    _poolRoot.offsetMax = Vector2.zero;
                }
            }

            while (_ghostPool.Count < InitialPool)
            {
                CreateGhost();
            }

            while (_popupPool.Count < InitialPool)
            {
                CreatePopup();
            }
        }

        SellGhostView RentGhost()
        {
            for (var i = 0; i < _ghostPool.Count; i++)
            {
                if (_ghostPool[i] != null && !_ghostPool[i].IsPlaying)
                {
                    return _ghostPool[i];
                }
            }

            return CreateGhost();
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

        SellGhostView CreateGhost()
        {
            if (_poolRoot == null)
            {
                return null;
            }

            var go = new GameObject($"SellGhost_{_ghostPool.Count}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.layer = _poolRoot.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_poolRoot, false);
            var ghost = go.AddComponent<SellGhostView>();
            ghost.Ensure(_poolRoot);
            _ghostPool.Add(ghost);
            return ghost;
        }

        CoinPopupView CreatePopup()
        {
            if (_poolRoot == null || popupPrefab == null)
            {
                return null;
            }

            var instance = Instantiate(popupPrefab, _poolRoot, false);
            instance.name = $"CoinPopup_{_popupPool.Count}";
            instance.gameObject.layer = _poolRoot.gameObject.layer;
            instance.Ensure();
            _popupPool.Add(instance);
            return instance;
        }
    }
}
