using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class InventoryFlightPresenter : MonoBehaviour
    {
        [SerializeField] BoardController boardController;
        [SerializeField] BoardDragView dragView;
        [SerializeField] BoardGeneratorAnimationConfig config;
        [SerializeField] BoardGeneratorPresenter generatorPresenter;

        GeneratorFlightView _flight;
        int _cellIndex = BoardController.NoSelectionIndex;
        int _lockToken;
        bool _closeInventory;
        UIWindow _inventoryWindow;
        bool _busy;

        public bool IsBusy => _busy;

        public void Configure(
            BoardController controller,
            BoardDragView view,
            BoardGeneratorAnimationConfig animationConfig,
            BoardGeneratorPresenter presenter)
        {
            boardController = controller;
            dragView = view;
            config = animationConfig;
            generatorPresenter = presenter;
        }

        public void Play(Sprite sprite, Vector2 size, Vector2 startLayer, int cellIndex, int lockToken, bool closeInventory, UIWindow inventoryWindow)
        {
            if (boardController == null || boardController.BoardView == null || dragView == null)
            {
                boardController?.ReleaseGeneratorSpawnLock(lockToken);
                return;
            }

            if (generatorPresenter == null)
            {
                generatorPresenter = boardController.GeneratorPresenter;
            }

            Abort();
            _cellIndex = cellIndex;
            _lockToken = lockToken;
            _closeInventory = closeInventory;
            _inventoryWindow = inventoryWindow;

            var spawnView = boardController.BoardView.GetCellView(cellIndex);
            if (spawnView != null)
            {
                spawnView.SetItemPresentationSuppressed(true);
                spawnView.SetHideItemForDrag(true);
                boardController.BoardView.RefreshCell(cellIndex);
            }

            var end = startLayer;
            if (spawnView != null)
            {
                var rect = spawnView.ItemImage != null
                    ? spawnView.ItemImage.rectTransform
                    : spawnView.transform as RectTransform;
                end = dragView.WorldToLayer(rect.TransformPoint(rect.rect.center));
            }

            _flight = generatorPresenter != null ? generatorPresenter.BorrowFlight() : null;
            if (_flight == null)
            {
                RevealCell();
                boardController.ReleaseGeneratorSpawnLock(lockToken);
                if (spawnView != null && spawnView.ItemAnimator != null && config != null)
                {
                    spawnView.ItemAnimator.PlayGeneratorItemSpawn(config);
                }

                CloseInventoryIfNeeded();
                return;
            }

            _busy = true;
            enabled = true;
            _flight.Begin(sprite, size, true, Color.white, startLayer, end, config);
        }

        void Update()
        {
            if (!_busy)
            {
                enabled = false;
                return;
            }

            var dt = config != null ? config.GetDeltaTime() : Time.unscaledDeltaTime;
            if (_flight != null && _flight.Tick(dt))
            {
                return;
            }

            Finish();
        }

        void OnDisable()
        {
            if (!_busy)
            {
                return;
            }

            _closeInventory = false;
            Finish();
        }

        public void Abort()
        {
            if (!_busy && _flight == null)
            {
                return;
            }

            ReleaseFlight();
            RevealCell();
            boardController?.ReleaseGeneratorSpawnLock(_lockToken);
            _lockToken = 0;
            _busy = false;
            enabled = false;
        }

        void Finish()
        {
            ReleaseFlight();
            RevealCell();
            var spawnView = boardController != null && boardController.BoardView != null
                ? boardController.BoardView.GetCellView(_cellIndex)
                : null;
            if (spawnView != null && spawnView.ItemAnimator != null && config != null)
            {
                spawnView.ItemAnimator.PlayGeneratorItemSpawn(config);
            }

            boardController?.ReleaseGeneratorSpawnLock(_lockToken);
            _lockToken = 0;
            _busy = false;
            enabled = false;
            CloseInventoryIfNeeded();
        }

        void CloseInventoryIfNeeded()
        {
            if (_closeInventory && _inventoryWindow != null && UIManager.Instance != null)
            {
                UIManager.Instance.CloseWindow(_inventoryWindow);
            }
        }

        void ReleaseFlight()
        {
            if (_flight == null)
            {
                return;
            }

            if (generatorPresenter != null)
            {
                generatorPresenter.ReleaseFlight(_flight);
            }
            else
            {
                _flight.HideImmediate();
            }

            _flight = null;
        }

        void RevealCell()
        {
            if (boardController == null || boardController.BoardView == null || _cellIndex == BoardController.NoSelectionIndex)
            {
                return;
            }

            var spawnView = boardController.BoardView.GetCellView(_cellIndex);
            if (spawnView == null)
            {
                return;
            }

            spawnView.SetItemPresentationSuppressed(false);
            spawnView.SetHideItemForDrag(false);
            boardController.BoardView.RefreshCell(_cellIndex);
        }
    }
}
