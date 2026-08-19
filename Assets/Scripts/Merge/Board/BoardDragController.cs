using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    public enum BoardDragPhase
    {
        Idle,
        Pressed,
        Dragging,
        Dropping
    }

    [DisallowMultipleComponent]
    public class BoardDragController : MonoBehaviour
    {
        [SerializeField] BoardController boardController;
        [SerializeField] BoardDragAnimationConfig config;
        [SerializeField] BoardDragView dragView;
        [SerializeField] UIDropTargetFeedback sellDropTarget;
        [SerializeField] UIDropTargetFeedback[] inventoryDropTargets;
        [SerializeField] OrdersHudView ordersHud;
        UiHoverScaleFeedback _bagBounce;

        BoardDragPhase _phase = BoardDragPhase.Idle;
        int _pointerId = int.MinValue;
        int _sourceIndex = BoardController.NoSelectionIndex;
        int _itemId = BoardCellState.EmptyItemId;
        int _dropIndex = BoardController.NoSelectionIndex;
        Vector2 _pointerScreen;
        bool _touch;
        bool _canDrag;
        int _touchId;
        Sprite _highlightSprite;
        int _previewMergeIndex = BoardController.NoSelectionIndex;
        int _previewCobwebIndex = BoardController.NoSelectionIndex;
        float _magnetBlend;
        Vector2 _magnetPos;

        const int MaxVelocitySamples = 5;
        readonly Vector2[] _velocityPositions = new Vector2[MaxVelocitySamples];
        readonly float[] _velocityTimes = new float[MaxVelocitySamples];
        int _velocityCount;
        int _velocityWrite;
        bool _waitingDisplaceADrop;
        UIDropTargetFeedback _activeOrderDrop;
        bool _catchingInventory;

        public BoardDragPhase Phase => _phase;
        public int ActivePointerId => _pointerId;
        public int ActiveSourceIndex => _sourceIndex;
        public bool IsBusy =>
            _phase == BoardDragPhase.Dragging ||
            _phase == BoardDragPhase.Dropping;
        public bool IsBusyWithCell(int index) =>
            index != BoardController.NoSelectionIndex && IsBusy && _sourceIndex == index;
        public bool IsDragInteractionActive => IsBusy;

        public void ConfigureDropTargets(UIDropTargetFeedback sell, UIDropTargetFeedback[] inventory, OrdersHudView orders)
        {
            sellDropTarget = sell;
            inventoryDropTargets = inventory;
            ordersHud = orders;
        }

        public void Configure(BoardController controller, BoardDragAnimationConfig animationConfig, BoardDragView view)
        {
            boardController = controller;
            config = animationConfig;
            dragView = view;
            ApplyEventSystemThreshold();
            CacheHighlightSprite();
            if (dragView != null && controller != null)
            {
                var canvas = controller.BoardRoot != null
                    ? controller.BoardRoot.GetComponentInParent<Canvas>()
                    : null;
                dragView.Configure(animationConfig, canvas);
                if (_highlightSprite != null && dragView.DropTarget != null)
                {
                    var image = dragView.DropTarget.GetComponent<Image>();
                    if (image != null && image.sprite == null)
                    {
                        image.sprite = _highlightSprite;
                        image.type = Image.Type.Sliced;
                    }
                }
            }
        }

        public bool HandlePointerDown(BoardCellView cell, PointerEventData eventData)
        {
            if (eventData == null || cell == null || boardController == null || boardController.State == null)
            {
                return false;
            }

            if (_phase == BoardDragPhase.Dragging || _phase == BoardDragPhase.Dropping)
            {
                return false;
            }

            if (_phase == BoardDragPhase.Pressed && eventData.pointerId != _pointerId)
            {
                return false;
            }

            if (!boardController.State.IsValidIndex(cell.Index))
            {
                return false;
            }

            if (boardController.IsCellInteractionLocked(cell.Index))
            {
                return false;
            }

            PrepareCellForPossiblePickup(cell);

            var state = boardController.State.GetCell(cell.Index);
            if (state == null || !state.HasItem || state.IsBox)
            {
                return false;
            }

            _phase = BoardDragPhase.Pressed;
            _pointerId = eventData.pointerId;
            _sourceIndex = cell.Index;
            _itemId = state.ItemId;
            _pointerScreen = eventData.position;
            _touch = !UiPointerUtility.SupportsHover(eventData);
            _canDrag = !state.ItemLocked;
            _dropIndex = BoardController.NoSelectionIndex;
            _touchId = 0;
            if (eventData is ExtendedPointerEventData extended)
            {
                _touchId = extended.touchId;
            }

            ApplyEventSystemThreshold();
            return true;
        }

        public void HandleBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _pointerId || _phase != BoardDragPhase.Pressed)
            {
                return;
            }

            _pointerScreen = eventData.position;
            if (!_canDrag)
            {
                return;
            }

            BeginDragging();
        }

        public void HandleDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _pointerId)
            {
                return;
            }

            if (_phase != BoardDragPhase.Pressed && _phase != BoardDragPhase.Dragging)
            {
                return;
            }

            _pointerScreen = eventData.position;
        }

        public void HandleEndDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _pointerId)
            {
                return;
            }

            _pointerScreen = eventData.position;
            if (_phase == BoardDragPhase.Dragging)
            {
                BeginDropping();
            }
        }

        public bool HandlePointerUp(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _pointerId)
            {
                return _phase == BoardDragPhase.Dragging || _phase == BoardDragPhase.Dropping;
            }

            _pointerScreen = eventData.position;
            if (_phase == BoardDragPhase.Dragging)
            {
                BeginDropping();
                return true;
            }

            if (_phase == BoardDragPhase.Dropping)
            {
                return true;
            }

            if (_phase == BoardDragPhase.Pressed)
            {
                ResetToIdle();
                return false;
            }

            return false;
        }

        public void HandleCancel()
        {
            if (_phase == BoardDragPhase.Idle)
            {
                return;
            }

            AbortImmediate();
        }

        public void AbortImmediate()
        {
            ClearInteractionPreviews();
            _waitingDisplaceADrop = false;
            if (boardController != null && boardController.DisplacePresenter != null)
            {
                boardController.DisplacePresenter.AbortAll();
            }

            var restoreIndex = _phase == BoardDragPhase.Dropping && _dropIndex != BoardController.NoSelectionIndex
                ? _dropIndex
                : _sourceIndex;
            RevealCellItem(_sourceIndex);
            RevealCellItem(_dropIndex);
            if (boardController != null && boardController.BoardView != null)
            {
                boardController.BoardView.ClearItemDragHides();
            }

            if (dragView != null)
            {
                dragView.HideImmediate();
            }

            ResetToIdle();
            if (boardController != null && boardController.isActiveAndEnabled && restoreIndex != BoardController.NoSelectionIndex)
            {
                var cell = boardController.State != null && boardController.State.IsValidIndex(restoreIndex)
                    ? boardController.State.GetCell(restoreIndex)
                    : null;
                if (cell != null && cell.HasItem && !cell.IsBox)
                {
                    boardController.SelectCell(restoreIndex);
                }
            }
        }

        public void CompleteDropAsConsumed()
        {
            ClearInteractionPreviews();
            _waitingDisplaceADrop = false;
            if (dragView != null)
            {
                dragView.HideImmediate();
            }

            ResetToIdle();
        }

        bool TryCompleteReadyOrderAtPointer()
        {
            if (_itemId == BoardCellState.EmptyItemId)
            {
                return false;
            }

            var orders = OrderSystem.Current;
            if (orders == null || !orders.TryGetReadyOrderIdForItem(_itemId, out var orderId))
            {
                return false;
            }

            if (!IsPointerOverOrders(orderId))
            {
                return false;
            }

            return orders.TryCompleteOrder(orderId);
        }

        bool IsPointerOverOrders(int preferredOrderId)
        {
            if (ordersHud != null && ordersHud.TryGetCard(preferredOrderId, out var card) && card != null && ContainsPointer(card.Rect))
            {
                return true;
            }

            if (ordersHud != null && ContainsPointer(ordersHud.transform as RectTransform))
            {
                return true;
            }

            return IsPointerRightOfBoard();
        }

        bool TryDropOnSell()
        {
            if (sellDropTarget == null || !ContainsPointer(sellDropTarget.Rect))
            {
                return false;
            }

            var sell = SellSystem.Current;
            var source = _sourceIndex;
            if (sell == null || !sell.CanSellCell(source, ignoreDragBusy: true))
            {
                return false;
            }

            var popupPos = dragView != null ? dragView.ScreenToLayer(_pointerScreen) : (Vector2?)null;
            CompleteDropAsConsumed();
            return sell.TrySellCell(source, ignoreDragBusy: true, popupPos);
        }

        bool TryDropOnInventory()
        {
            if (!IsPointerOverInventory())
            {
                return false;
            }

            var inventory = InventoryController.Current;
            if (inventory == null || !inventory.IsAcceptedItem(_itemId))
            {
                return false;
            }

            if (inventory.IsFull)
            {
                if (boardController != null)
                {
                    boardController.EnsureMessagesReady();
                    boardController.MessagePresenter?.ShowInventoryFull(_pointerScreen);
                }

                BeginMoveOrReturn(_sourceIndex);
                return true;
            }

            if (boardController == null || !boardController.TryExtractItem(_sourceIndex, out var extracted, out var instanceId) || extracted != _itemId)
            {
                BeginMoveOrReturn(_sourceIndex);
                return true;
            }

            if (!inventory.TryAdd(extracted, instanceId))
            {
                boardController.TryPlaceInventoryItem(extracted, _sourceIndex, out var token, instanceId);
                boardController.ReleaseGeneratorSpawnLock(token);
                if (boardController.BoardView != null)
                {
                    boardController.BoardView.RefreshCell(_sourceIndex);
                }

                boardController.EnsureMessagesReady();
                boardController.MessagePresenter?.ShowInventoryFull(_pointerScreen);
                CompleteDropAsConsumed();
                return true;
            }

            BeginInventoryCatch();
            return true;
        }

        void BeginInventoryCatch()
        {
            _catchingInventory = true;
            _dropIndex = BoardController.NoSelectionIndex;
            _phase = BoardDragPhase.Dropping;
            if (dragView == null)
            {
                PlayInventoryBagBounce();
                FinishDrop();
                return;
            }

            var bag = ResolveInventoryBagRect();
            var land = bag != null
                ? dragView.WorldToLayer(bag.TransformPoint(bag.rect.center))
                : dragView.CurrentPlanarPosition;
            dragView.BeginDrop(land, false);
            PlayInventoryBagBounce();
        }

        RectTransform ResolveInventoryBagRect()
        {
            if (inventoryDropTargets == null)
            {
                return null;
            }

            for (var i = 0; i < inventoryDropTargets.Length; i++)
            {
                if (inventoryDropTargets[i] != null)
                {
                    return inventoryDropTargets[i].Rect;
                }
            }

            return null;
        }

        void PlayInventoryBagBounce()
        {
            var bag = ResolveInventoryBagRect();
            if (bag == null)
            {
                return;
            }

            if (_bagBounce == null)
            {
                _bagBounce = bag.GetComponent<UiHoverScaleFeedback>();
                if (_bagBounce == null)
                {
                    _bagBounce = bag.GetComponentInChildren<UiHoverScaleFeedback>();
                }
            }

            _bagBounce?.PlayBounce();
        }

        bool IsPointerOverInventory()
        {
            if (inventoryDropTargets == null)
            {
                return false;
            }

            for (var i = 0; i < inventoryDropTargets.Length; i++)
            {
                if (inventoryDropTargets[i] != null && ContainsPointer(inventoryDropTargets[i].Rect))
                {
                    return true;
                }
            }

            return false;
        }

        bool ContainsPointer(RectTransform rect)
        {
            if (rect == null)
            {
                return false;
            }

            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, _pointerScreen, camera);
        }

        bool IsPointerRightOfBoard()
        {
            if (boardController == null || boardController.BoardRoot == null)
            {
                return false;
            }

            var root = boardController.BoardRoot;
            var canvas = root.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var corners = new Vector3[4];
            root.GetWorldCorners(corners);
            var right = Mathf.Max(
                RectTransformUtility.WorldToScreenPoint(camera, corners[2]).x,
                RectTransformUtility.WorldToScreenPoint(camera, corners[3]).x);
            return _pointerScreen.x > right;
        }

        void Update()
        {
            if (_phase == BoardDragPhase.Idle || config == null || dragView == null)
            {
                return;
            }

            var dt = config.GetDeltaTime();
            if (_phase == BoardDragPhase.Pressed)
            {
                if (!IsTrackedPointerHeld())
                {
                    ResetToIdle();
                }

                return;
            }

            if (_phase == BoardDragPhase.Dragging)
            {
                if (!IsSourceValid())
                {
                    AbortImmediate();
                    return;
                }

                if (!IsTrackedPointerHeld())
                {
                    BeginDropping();
                    return;
                }

                SampleDragVelocity();
                UpdateDropTarget();
                var magnetIndex = _previewMergeIndex != BoardController.NoSelectionIndex
                    ? _previewMergeIndex
                    : _previewCobwebIndex;
                TickMagnet(dt, magnetIndex);
                dragView.TickFollowOrPickup(dt, GetFollowTarget());
                return;
            }

            if (_phase == BoardDragPhase.Dropping)
            {
                if (_waitingDisplaceADrop)
                {
                    // Hold A in place until displace presenter starts B flight + lead.
                    dragView.TickFollowOrPickup(dt, dragView.CurrentPlanarPosition);
                    return;
                }

                if (dragView.TickDrop(dt))
                {
                    return;
                }

                FinishDrop();
                return;
            }
        }

        void OnDisable()
        {
            if (_phase != BoardDragPhase.Idle)
            {
                AbortImmediate();
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                HandleCancel();
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                HandleCancel();
            }
        }

        void BeginDragging()
        {
            if (boardController == null || boardController.BoardView == null || config == null || dragView == null)
            {
                ResetToIdle();
                return;
            }

            var sourceView = boardController.BoardView.GetCellView(_sourceIndex);
            if (sourceView == null || sourceView.ItemImage == null || !IsSourceValid())
            {
                ResetToIdle();
                return;
            }

            var item = sourceView.ItemImage;
            var itemRect = item.rectTransform;
            var startPos = dragView.WorldToLayer(itemRect.TransformPoint(itemRect.rect.center));
            var startScale = new Vector2(itemRect.localScale.x, itemRect.localScale.y);
            var size = itemRect.rect.size;
            var sprite = item.sprite;
            var preserveAspect = item.preserveAspect;
            var color = item.color;

            // Capture first, then interrupt presenters — DragItemView owns the only visible copy.
            PrepareCellForPossiblePickup(sourceView);
            if (sourceView.ItemAnimator != null)
            {
                sourceView.ItemAnimator.BeginDrag();
            }

            sourceView.SetItemPresentationSuppressed(true);
            sourceView.SetHideItemForDrag(true);
            if (boardController != null)
            {
                boardController.PrepareDragPresentation(_sourceIndex);
            }

            dragView.EnsureChildren();
            dragView.BeginPickup(sprite, size, preserveAspect, color, startPos, startScale);
            _phase = BoardDragPhase.Dragging;
            _dropIndex = BoardController.NoSelectionIndex;
            ClearVelocitySamples();
            SampleDragVelocity();
        }

        public void PrepareCellForPossiblePickup(BoardCellView cell)
        {
            if (cell == null || boardController == null)
            {
                return;
            }

            if (boardController.MergePresenter != null)
            {
                boardController.MergePresenter.ReleaseCellVisualOwnership(cell.Index);
            }

            if (boardController.CobwebPresenter != null)
            {
                boardController.CobwebPresenter.ReleaseCellVisualOwnership(cell.Index);
            }

            if (boardController.BoxRevealPresenter != null)
            {
                boardController.BoxRevealPresenter.ReleaseCellVisualOwnership(cell.Index);
            }

            if (boardController.GeneratorPresenter != null)
            {
                boardController.GeneratorPresenter.ReleaseCellVisualOwnership(cell.Index);
            }

            if (boardController.DisplacePresenter != null)
            {
                boardController.DisplacePresenter.ReleaseCellVisualOwnership(cell.Index);
            }

            if (cell.IsTransientAnimationRunning() || cell.IsCobwebBreakPlaying || cell.IsBoxRevealPlaying)
            {
                cell.CancelTransientPresentationForPickup();
            }
        }

        void BeginDropping()
        {
            if (_phase != BoardDragPhase.Dragging)
            {
                return;
            }

            SampleDragVelocity();

            if (dragView != null)
            {
                dragView.HideDropTargetImmediate();
            }

            // Keep last valid preview — pointer often leaves the cell by a few px on release.
            var stickyMergeIndex = _previewMergeIndex;
            var stickyCobwebIndex = _previewCobwebIndex;
            ClearInteractionPreviews();
            var underIndex = FindCellIndexUnderPointer();
            var intentMerge = stickyMergeIndex != BoardController.NoSelectionIndex ? stickyMergeIndex : underIndex;
            var intentCobweb = stickyCobwebIndex != BoardController.NoSelectionIndex ? stickyCobwebIndex : underIndex;

            // 1) Direct merge / cobweb under pointer (or sticky preview).
            // Important: locked cobweb targets can still be valid merge targets.
            // Sticky preview for locked cells is stored in _previewCobwebIndex,
            // so we must check merge against that index before falling back to unlock.
            if (boardController != null && boardController.CanMerge(_sourceIndex, intentMerge))
            {
                BeginMerging(intentMerge);
                return;
            }

            if (boardController != null &&
                intentCobweb != intentMerge &&
                boardController.CanMerge(_sourceIndex, intentCobweb))
            {
                BeginMerging(intentCobweb);
                return;
            }

            if (boardController != null && boardController.CanUnlockCobweb(_sourceIndex, intentCobweb))
            {
                BeginUnlocking(intentCobweb);
                return;
            }

            // 2) Direct empty under pointer — never steal with throw-assist.
            if (IsEmptyOpenCell(underIndex))
            {
                BeginMoveOrReturn(underIndex);
                return;
            }

            if (TryDropOnSell())
            {
                return;
            }

            if (TryDropOnInventory())
            {
                return;
            }

            // 3) Drop onto a ready order that needs this item.
            if (TryCompleteReadyOrderAtPointer())
            {
                return;
            }

            // 4) Directional throw-to-merge assist (priority over displacement).
            if (TryFindDirectionalMergeAssist(out var assistIndex, out var assistCobweb))
            {
                if (assistCobweb)
                {
                    BeginUnlocking(assistIndex);
                }
                else
                {
                    BeginMerging(assistIndex);
                }

                return;
            }

            // 5) Occupied movable displacement — A keeps the cell, B yields.
            if (underIndex != BoardController.NoSelectionIndex &&
                boardController != null &&
                boardController.CanDisplaceOccupiedTarget(underIndex) &&
                !boardController.CanMerge(_sourceIndex, underIndex) &&
                !boardController.CanUnlockCobweb(_sourceIndex, underIndex))
            {
                var destination = boardController.FindDisplaceDestination(underIndex, _sourceIndex);
                if (destination != BoardController.NoSelectionIndex)
                {
                    BeginDisplacing(underIndex, destination);
                    return;
                }
            }

            // 6) Source return / nearest-empty fallback.
            BeginMoveOrReturn(ResolveDropIndex());
        }

        void BeginMoveOrReturn(int targetIndex)
        {
            var landingIndex = targetIndex;
            var moved = false;
            if (targetIndex != _sourceIndex && IsEmptyOpenCell(targetIndex))
            {
                var targetView = boardController.BoardView != null
                    ? boardController.BoardView.GetCellView(targetIndex)
                    : null;
                if (targetView != null)
                {
                    targetView.SetHideItemForDrag(true);
                }

                moved = boardController.TryMoveItemToEmpty(_sourceIndex, targetIndex);
                if (moved)
                {
                    RevealCellItem(_sourceIndex);
                    landingIndex = targetIndex;
                }
                else if (targetView != null)
                {
                    targetView.SetHideItemForDrag(false);
                }
            }

            if (!moved)
            {
                landingIndex = _sourceIndex;
            }

            _dropIndex = landingIndex;
            var landPos = GetCellItemLayerPosition(landingIndex);
            _phase = BoardDragPhase.Dropping;
            if (dragView != null)
            {
                dragView.BeginDrop(landPos, !moved);
            }
            else
            {
                FinishDrop();
            }
        }

        void FinishDrop()
        {
            if (_catchingInventory)
            {
                if (dragView != null)
                {
                    dragView.HideImmediate();
                }

                ResetToIdle();
                return;
            }

            var landingIndex = _dropIndex != BoardController.NoSelectionIndex ? _dropIndex : _sourceIndex;
            if (dragView != null)
            {
                dragView.HideImmediate();
            }

            RevealCellItem(landingIndex);
            // Do not unhide a cell still owned by displace flight (B → destination).
            if (boardController == null ||
                boardController.DisplacePresenter == null ||
                !boardController.DisplacePresenter.IsOwningCellVisual(_sourceIndex))
            {
                RevealCellItem(_sourceIndex);
            }

            var hoverIndex = landingIndex;
            var allowHover = !_touch;
            var pointerScreen = _pointerScreen;
            ResetToIdle();
            if (boardController != null)
            {
                boardController.SelectCell(hoverIndex);
            }

            if (allowHover)
            {
                TryRestoreHover(hoverIndex, pointerScreen);
            }
        }

        void UpdateDropTarget()
        {
            UpdateUiDropTargets();
            var index = FindCellIndexUnderPointer();
            if (index == BoardController.NoSelectionIndex)
            {
                ClearInteractionPreviewsSoft();
                dragView.HideDropTarget();
                return;
            }

            if (boardController != null && boardController.CanMerge(_sourceIndex, index))
            {
                if (IsLockedItemCell(index))
                {
                    ClearMergePreviewSoft();
                    SetCobwebPreview(index);
                    dragView.HideDropTarget();
                    return;
                }

                ClearCobwebPreviewSoft();
                SetMergePreview(index);
                var cell = boardController.BoardView != null ? boardController.BoardView.GetCellView(index) : null;
                var mergeConfig = boardController.MergeAnimationConfig;
                dragView.ShowMergeDropTarget(cell, mergeConfig);
                UpdateMergeTargetAttraction(index);
                return;
            }

            if (boardController != null && boardController.CanUnlockCobweb(_sourceIndex, index))
            {
                ClearMergePreviewSoft();
                SetCobwebPreview(index);
                dragView.HideDropTarget();
                return;
            }

            ClearInteractionPreviewsSoft();
            if (index == _sourceIndex || IsEmptyOpenCell(index))
            {
                var cell = boardController.BoardView.GetCellView(index);
                dragView.ShowDropTarget(cell);
                return;
            }

            dragView.HideDropTarget();
        }

        int ResolveDropIndex()
        {
            var index = FindCellIndexUnderPointer();
            if (boardController != null && boardController.CanMerge(_sourceIndex, index))
            {
                return index;
            }

            if (boardController != null && boardController.CanUnlockCobweb(_sourceIndex, index))
            {
                return index;
            }

            if (IsEmptyOpenCell(index))
            {
                return index;
            }

            if (index == _sourceIndex)
            {
                return _sourceIndex;
            }

            var nearest = FindNearestEmptyCell(GetPointerCanvasPosition());
            return nearest != BoardController.NoSelectionIndex ? nearest : _sourceIndex;
        }

        int FindNearestEmptyCell(Vector2 pointerCanvas)
        {
            if (boardController == null || boardController.BoardView == null || config == null)
            {
                return BoardController.NoSelectionIndex;
            }

            var cells = boardController.BoardView.Cells;
            if (cells == null)
            {
                return BoardController.NoSelectionIndex;
            }

            var bestDistance = float.MaxValue;
            var bestIndex = BoardController.NoSelectionIndex;
            for (var i = 0; i < cells.Count; i++)
            {
                var cellView = cells[i];
                if (cellView == null || !IsEmptyCandidate(cellView.Index))
                {
                    continue;
                }

                var center = GetCellItemLayerPosition(cellView.Index);
                var distance = Vector2.Distance(pointerCanvas, center);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestIndex = cellView.Index;
            }

            if (bestIndex == BoardController.NoSelectionIndex || bestDistance > config.InvalidDropSnapMaxDistance)
            {
                return BoardController.NoSelectionIndex;
            }

            return bestIndex;
        }

        bool IsEmptyCandidate(int index)
        {
            return index == _sourceIndex || IsEmptyOpenCell(index);
        }

        Vector2 GetPointerCanvasPosition()
        {
            return dragView != null ? dragView.ScreenToLayer(_pointerScreen) : Vector2.zero;
        }

        int FindCellIndexUnderPointer()
        {
            if (boardController == null || boardController.BoardView == null)
            {
                return BoardController.NoSelectionIndex;
            }

            var cells = boardController.BoardView.Cells;
            if (cells == null)
            {
                return BoardController.NoSelectionIndex;
            }

            var camera = dragView != null ? dragView.EventCamera : null;
            var found = BoardController.NoSelectionIndex;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                var rect = cell.transform as RectTransform;
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, _pointerScreen, camera))
                {
                    found = cell.Index;
                }
            }

            return found;
        }

        string FormatCell(int index)
        {
            if (boardController == null || boardController.State == null ||
                index == BoardController.NoSelectionIndex || !boardController.State.IsValidIndex(index))
            {
                return $"{index}(invalid)";
            }

            boardController.State.GetCoordinates(index, out var row, out var col);
            var cell = boardController.State.GetCell(index);
            if (cell == null)
            {
                return $"{index}[{row},{col}](null)";
            }

            return $"{index}[{row},{col}] id={cell.ItemId} locked={cell.ItemLocked} box={cell.IsBox} empty={cell.IsEmpty}";
        }

        bool IsLockedItemCell(int index)
        {
            if (boardController == null || boardController.State == null || !boardController.State.IsValidIndex(index))
            {
                return false;
            }

            var cell = boardController.State.GetCell(index);
            return cell != null && cell.HasItem && cell.ItemLocked;
        }

        bool IsEmptyOpenCell(int index)
        {
            if (boardController == null || boardController.State == null || !boardController.State.IsValidIndex(index))
            {
                return false;
            }

            if (boardController.IsCellInteractionLocked(index))
            {
                return false;
            }

            var cell = boardController.State.GetCell(index);
            return cell != null && cell.IsEmpty;
        }

        bool IsSourceValid()
        {
            if (boardController == null || boardController.State == null || !boardController.State.IsValidIndex(_sourceIndex))
            {
                return false;
            }

            var cell = boardController.State.GetCell(_sourceIndex);
            var view = boardController.BoardView != null ? boardController.BoardView.GetCellView(_sourceIndex) : null;
            if (view == null || !view.gameObject.activeInHierarchy)
            {
                return false;
            }

            return cell != null && cell.HasItem && !cell.IsBox && !cell.ItemLocked && cell.ItemId == _itemId;
        }

        Vector2 GetFollowTarget()
        {
            var local = dragView.ScreenToLayer(_pointerScreen);
            if (config != null)
            {
                local += config.GetPointerOffset(_touch);
            }

            if (_magnetBlend <= 0.001f)
            {
                return local;
            }

            var strength = 0.26f;
            if (_previewCobwebIndex != BoardController.NoSelectionIndex)
            {
                strength = boardController != null && boardController.CobwebAnimationConfig != null
                    ? boardController.CobwebAnimationConfig.UnlockMagnetStrength
                    : 0.18f;
            }
            else if (boardController != null && boardController.MergeAnimationConfig != null)
            {
                strength = boardController.MergeAnimationConfig.MergeMagnetStrength;
            }

            return Vector2.Lerp(local, _magnetPos, strength * _magnetBlend);
        }

        Vector2 GetCellItemLayerPosition(int index)
        {
            var cell = boardController != null && boardController.BoardView != null
                ? boardController.BoardView.GetCellView(index)
                : null;
            RectTransform rect = null;
            if (cell != null && cell.ItemImage != null)
            {
                rect = cell.ItemImage.rectTransform;
            }
            else if (cell != null)
            {
                rect = cell.transform as RectTransform;
            }

            if (rect == null)
            {
                return GetFollowTarget();
            }

            return dragView.WorldToLayer(rect.TransformPoint(rect.rect.center));
        }

        void RevealCellItem(int index)
        {
            if (boardController == null || boardController.BoardView == null || index == BoardController.NoSelectionIndex)
            {
                return;
            }

            var cell = boardController.BoardView.GetCellView(index);
            if (cell != null)
            {
                cell.SetItemPresentationSuppressed(false);
                cell.SetHideItemForDrag(false);
            }

            boardController.BoardView.RefreshCell(index);
        }

        void TryRestoreHover(int index, Vector2 pointerScreen)
        {
            if (boardController == null || boardController.BoardView == null)
            {
                return;
            }

            var cell = boardController.BoardView.GetCellView(index);
            if (cell == null || cell.ItemAnimator == null)
            {
                return;
            }

            var camera = dragView != null ? dragView.EventCamera : null;
            var rect = cell.transform as RectTransform;
            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, pointerScreen, camera))
            {
                cell.ItemAnimator.SetHovered(true);
            }
        }

        void ResetToIdle()
        {
            _phase = BoardDragPhase.Idle;
            _pointerId = int.MinValue;
            _sourceIndex = BoardController.NoSelectionIndex;
            _itemId = BoardCellState.EmptyItemId;
            _dropIndex = BoardController.NoSelectionIndex;
            _canDrag = false;
            _touch = false;
            _touchId = 0;
            _previewMergeIndex = BoardController.NoSelectionIndex;
            _previewCobwebIndex = BoardController.NoSelectionIndex;
            _magnetBlend = 0f;
            _waitingDisplaceADrop = false;
            _catchingInventory = false;
            ClearVelocitySamples();
            ClearUiDropTargets();
        }

        void UpdateUiDropTargets()
        {
            var sellActive = sellDropTarget != null &&
                             ContainsPointer(sellDropTarget.Rect) &&
                             SellSystem.Current != null &&
                             SellSystem.Current.CanSellCell(_sourceIndex, ignoreDragBusy: true);
            if (sellDropTarget != null)
            {
                sellDropTarget.SetHighlighted(sellActive);
            }

            if (inventoryDropTargets != null)
            {
                var accepted = InventoryController.Current != null && InventoryController.Current.IsAcceptedItem(_itemId);
                for (var i = 0; i < inventoryDropTargets.Length; i++)
                {
                    var target = inventoryDropTargets[i];
                    if (target == null)
                    {
                        continue;
                    }

                    target.SetHighlighted(accepted && ContainsPointer(target.Rect));
                }
            }

            OrderCardView highlightCard = null;
            var orders = OrderSystem.Current;
            if (ordersHud != null &&
                orders != null &&
                orders.TryGetReadyOrderIdForItem(_itemId, out var orderId) &&
                ordersHud.TryGetCard(orderId, out var card) &&
                card != null)
            {
                if (ContainsPointer(card.Rect))
                {
                    highlightCard = card;
                }
            }

            var orderFeedback = highlightCard != null ? highlightCard.GetComponent<UIDropTargetFeedback>() : null;
            if (_activeOrderDrop != orderFeedback)
            {
                if (_activeOrderDrop != null)
                {
                    _activeOrderDrop.SetHighlighted(false, true);
                }

                _activeOrderDrop = orderFeedback;
            }

            if (_activeOrderDrop != null)
            {
                _activeOrderDrop.SetHighlighted(true);
            }
        }

        void ClearUiDropTargets()
        {
            if (sellDropTarget != null)
            {
                sellDropTarget.SetHighlighted(false, true);
            }

            if (inventoryDropTargets != null)
            {
                for (var i = 0; i < inventoryDropTargets.Length; i++)
                {
                    if (inventoryDropTargets[i] != null)
                    {
                        inventoryDropTargets[i].SetHighlighted(false, true);
                    }
                }
            }

            if (_activeOrderDrop != null)
            {
                _activeOrderDrop.SetHighlighted(false, true);
                _activeOrderDrop = null;
            }
        }

        void ClearVelocitySamples()
        {
            _velocityCount = 0;
            _velocityWrite = 0;
        }

        void SampleDragVelocity()
        {
            if (dragView == null || config == null)
            {
                return;
            }

            // Pointer movement only — ignore DragItemView visual offset.
            var position = GetPointerCanvasPosition();
            var time = Time.unscaledTime;
            var capacity = Mathf.Min(MaxVelocitySamples, config.DirectionalMergeVelocitySampleCount);
            if (capacity < 2)
            {
                capacity = 2;
            }

            if (_velocityCount > 0)
            {
                var lastIndex = (_velocityWrite - 1 + MaxVelocitySamples) % MaxVelocitySamples;
                if ((_velocityPositions[lastIndex] - position).sqrMagnitude < 0.01f &&
                    time - _velocityTimes[lastIndex] < 0.001f)
                {
                    return;
                }
            }

            _velocityPositions[_velocityWrite] = position;
            _velocityTimes[_velocityWrite] = time;
            _velocityWrite = (_velocityWrite + 1) % MaxVelocitySamples;
            if (_velocityCount < capacity)
            {
                _velocityCount++;
            }
        }

        bool TryGetReleaseVelocity(out Vector2 velocity, out float speed)
        {
            velocity = Vector2.zero;
            speed = 0f;
            if (config == null || _velocityCount < 2)
            {
                return false;
            }

            var newestIndex = (_velocityWrite - 1 + MaxVelocitySamples) % MaxVelocitySamples;
            var newestPos = _velocityPositions[newestIndex];
            var newestTime = _velocityTimes[newestIndex];
            var window = Mathf.Max(0.04f, config.DirectionalMergeVelocitySampleWindow);

            var oldestPos = newestPos;
            var oldestTime = newestTime;
            for (var i = 0; i < _velocityCount; i++)
            {
                var index = (_velocityWrite - 1 - i + MaxVelocitySamples * 2) % MaxVelocitySamples;
                var sampleTime = _velocityTimes[index];
                if (newestTime - sampleTime > window && i > 0)
                {
                    break;
                }

                oldestPos = _velocityPositions[index];
                oldestTime = sampleTime;
            }

            var dt = newestTime - oldestTime;
            if (dt < 0.001f)
            {
                return false;
            }

            velocity = (newestPos - oldestPos) / dt;
            speed = velocity.magnitude;
            return speed > 0.01f;
        }

        bool TryFindDirectionalMergeAssist(out int targetIndex, out bool cobwebUnlock)
        {
            targetIndex = BoardController.NoSelectionIndex;
            cobwebUnlock = false;
            if (config == null || !config.DirectionalMergeAssistEnabled || boardController == null ||
                boardController.BoardView == null)
            {
                return false;
            }

            if (!TryGetReleaseVelocity(out var velocity, out var speed))
            {
                return false;
            }

            if (speed < config.DirectionalMergeMinSpeed)
            {
                if (config.ShowDirectionalAssistDebug)
                {
                    Debug.Log($"[ThrowAssist] skip slow speed={speed:F0} min={config.DirectionalMergeMinSpeed:F0}");
                }

                return false;
            }

            var throwDirection = velocity.normalized;
            var releasePos = GetPointerCanvasPosition();
            var cellSize = EstimateCellSize();
            var searchDistance = cellSize * config.DirectionalMergeSearchDistanceInCells;
            var searchDistanceSq = searchDistance * searchDistance;
            var minDot = config.DirectionalMergeMinDot;
            var directionWeight = config.DirectionalMergeDirectionWeight;
            var distanceWeight = config.DirectionalMergeDistanceWeight;

            var bestScore = float.NegativeInfinity;
            var bestIndex = BoardController.NoSelectionIndex;
            var bestIsCobweb = false;
            var bestDot = 0f;
            var bestDistance = 0f;

            var cells = boardController.BoardView.Cells;
            if (cells == null)
            {
                return false;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                var cellView = cells[i];
                if (cellView == null)
                {
                    continue;
                }

                var index = cellView.Index;
                if (index == _sourceIndex)
                {
                    continue;
                }

                var canMerge = boardController.CanMerge(_sourceIndex, index);
                var canCobweb = !canMerge && config.DirectionalMergeIncludeCobwebTargets &&
                                boardController.CanUnlockCobweb(_sourceIndex, index);
                if (!canMerge && !canCobweb)
                {
                    continue;
                }

                var candidateCenter = GetCellItemLayerPosition(index);
                var toCandidate = candidateCenter - releasePos;
                var distanceSq = toCandidate.sqrMagnitude;
                if (distanceSq > searchDistanceSq || distanceSq < 0.0001f)
                {
                    continue;
                }

                var distance = Mathf.Sqrt(distanceSq);
                var directionScore = Vector2.Dot(toCandidate / distance, throwDirection);
                if (directionScore < minDot)
                {
                    continue;
                }

                var normalizedDistance = distance / Mathf.Max(1f, searchDistance);
                var score = directionScore * directionWeight - normalizedDistance * distanceWeight;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestIndex = index;
                bestIsCobweb = canCobweb;
                bestDot = directionScore;
                bestDistance = distance;
            }

            if (bestIndex == BoardController.NoSelectionIndex)
            {
                if (config.ShowDirectionalAssistDebug)
                {
                    Debug.Log(
                        $"[ThrowAssist] no candidate speed={speed:F0} dir={throwDirection} " +
                        $"search={searchDistance:F0}");
                }

                return false;
            }

            if (config.ShowDirectionalAssistDebug)
            {
                Debug.Log(
                    $"[ThrowAssist] choose={bestIndex} cobweb={bestIsCobweb} speed={speed:F0} " +
                    $"dot={bestDot:F2} dist={bestDistance:F0}/{searchDistance:F0} score={bestScore:F2}");
            }

            targetIndex = bestIndex;
            cobwebUnlock = bestIsCobweb;
            return true;
        }

        float EstimateCellSize()
        {
            if (boardController == null || boardController.BoardView == null)
            {
                return 170f;
            }

            var cells = boardController.BoardView.Cells;
            if (cells == null || cells.Count == 0 || cells[0] == null)
            {
                return 170f;
            }

            var rect = cells[0].transform as RectTransform;
            if (rect == null)
            {
                return 170f;
            }

            var size = rect.rect.size;
            return Mathf.Max(1f, Mathf.Min(size.x, size.y));
        }

        void BeginMerging(int targetIndex)
        {
            if (boardController == null || boardController.MergePresenter == null || dragView == null)
            {
                _dropIndex = _sourceIndex;
                _phase = BoardDragPhase.Dropping;
                dragView?.BeginDrop(GetCellItemLayerPosition(_sourceIndex), true);
                return;
            }

            if (boardController.IsCellInteractionLocked(targetIndex) ||
                boardController.IsCellInteractionLocked(_sourceIndex) ||
                !boardController.TryGetMergeResultItemId(_sourceIndex, targetIndex, out var resultItemId))
            {
                _dropIndex = _sourceIndex;
                _phase = BoardDragPhase.Dropping;
                dragView.BeginDrop(GetCellItemLayerPosition(_sourceIndex), true);
                return;
            }

            if (!dragView.TryCaptureVisualSnapshot(
                    out var sprite,
                    out var size,
                    out var preserveAspect,
                    out var color,
                    out var startPos,
                    out var startScale))
            {
                _dropIndex = _sourceIndex;
                _phase = BoardDragPhase.Dropping;
                dragView.BeginDrop(GetCellItemLayerPosition(_sourceIndex), true);
                return;
            }

            ClearInteractionPreviews();
            dragView.HideDropTargetImmediate();

            var selectionRevision = boardController.SelectionRevision;
            var sourceIndex = _sourceIndex;
            boardController.MergePresenter.Play(
                sourceIndex,
                targetIndex,
                resultItemId,
                selectionRevision,
                sprite,
                size,
                preserveAspect,
                color,
                startPos,
                startScale);

            // Hand off to independent merge presentation; free the shared drag view immediately.
            dragView.HideImmediate();
            ResetToIdle();
        }

        void BeginUnlocking(int lockedTargetIndex)
        {
            if (boardController != null)
            {
                boardController.EnsureCobwebReady();
            }

            if (boardController == null || boardController.CobwebPresenter == null || dragView == null)
            {
                Debug.LogError(
                    $"[Cobweb] BeginUnlocking abort: controller={boardController != null} " +
                    $"presenter={(boardController != null && boardController.CobwebPresenter != null)} " +
                    $"dragView={dragView != null}");
                _dropIndex = _sourceIndex;
                _phase = BoardDragPhase.Dropping;
                dragView?.BeginDrop(GetCellItemLayerPosition(_sourceIndex), true);
                return;
            }

            if (!boardController.CanUnlockCobweb(_sourceIndex, lockedTargetIndex))
            {
                _dropIndex = _sourceIndex;
                _phase = BoardDragPhase.Dropping;
                dragView.BeginDrop(GetCellItemLayerPosition(_sourceIndex), true);
                return;
            }

            var destination = boardController.FindUnlockDestination(_sourceIndex, lockedTargetIndex);

            if (!dragView.TryCaptureVisualSnapshot(
                    out var sprite,
                    out var size,
                    out var preserveAspect,
                    out var color,
                    out var startPos,
                    out var startScale))
            {
                Debug.LogError("[Cobweb] BeginUnlocking snapshot failed — returning to source");
                _dropIndex = _sourceIndex;
                _phase = BoardDragPhase.Dropping;
                dragView.BeginDrop(GetCellItemLayerPosition(_sourceIndex), true);
                return;
            }

            ClearInteractionPreviews();
            dragView.HideDropTargetImmediate();

            var selectionRevision = boardController.SelectionRevision;
            var presenter = boardController.CobwebPresenter;
            presenter.Play(
                _sourceIndex,
                lockedTargetIndex,
                destination,
                selectionRevision,
                sprite,
                size,
                preserveAspect,
                color,
                startPos,
                startScale);

            if (!presenter.HasActiveSequences)
            {
                Debug.LogError("[Cobweb] Presenter.Play did not start a sequence — returning to source");
                _dropIndex = _sourceIndex;
                _phase = BoardDragPhase.Dropping;
                dragView.BeginDrop(GetCellItemLayerPosition(_sourceIndex), true);
                return;
            }

            dragView.HideImmediate();
            ResetToIdle();
        }

        void BeginDisplacing(int occupiedTargetIndex, int destinationIndex)
        {
            if (boardController != null)
            {
                boardController.EnsureDisplaceReady();
            }

            if (boardController == null || boardController.DisplacePresenter == null || dragView == null || config == null)
            {
                BeginMoveOrReturn(ResolveDropIndex());
                return;
            }

            var targetView = boardController.BoardView != null
                ? boardController.BoardView.GetCellView(occupiedTargetIndex)
                : null;
            if (targetView == null || targetView.ItemImage == null)
            {
                BeginMoveOrReturn(ResolveDropIndex());
                return;
            }

            var item = targetView.ItemImage;
            var itemRect = item.rectTransform;
            var displacedSprite = item.sprite;
            var displacedSize = itemRect.rect.size;
            if (displacedSize.x <= 1f || displacedSize.y <= 1f)
            {
                displacedSize = new Vector2(170f, 170f);
            }

            var preserveAspect = item.preserveAspect;
            var color = item.color;
            var sourceIndex = _sourceIndex;
            var landingIndex = occupiedTargetIndex;

            ClearInteractionPreviews();
            dragView.HideDropTargetImmediate();

            var started = boardController.DisplacePresenter.Play(
                sourceIndex,
                occupiedTargetIndex,
                destinationIndex,
                displacedSprite,
                displacedSize,
                preserveAspect,
                color,
                () =>
                {
                    if (dragView == null)
                    {
                        return;
                    }

                    // A lands into the freed target while B is already in flight.
                    _waitingDisplaceADrop = false;
                    _dropIndex = landingIndex;
                    _phase = BoardDragPhase.Dropping;
                    dragView.BeginDrop(GetCellItemLayerPosition(landingIndex), false);
                },
                () =>
                {
                    _waitingDisplaceADrop = false;
                    BeginMoveOrReturn(_sourceIndex);
                });

            if (!started)
            {
                BeginMoveOrReturn(ResolveDropIndex());
                return;
            }

            // Hold A in drag view until presenter signals drop; suppress source already done.
            _dropIndex = landingIndex;
            _phase = BoardDragPhase.Dropping;
            // Keep current pose until BeginDrop — TickDrop no-ops while still in Follow/Pickup.
            // Force a stable hold: treat as dropping but delay BeginDrop via presenter callback.
            // If TickDrop runs before BeginDrop, it returns false and FinishDrop early — prevent that.
            _waitingDisplaceADrop = true;
        }

        void SetMergePreview(int index)
        {
            if (_previewMergeIndex == index)
            {
                return;
            }

            ClearMergePreviewSoft();
            _previewMergeIndex = index;
            var mergeConfig = boardController != null ? boardController.MergeAnimationConfig : null;
            var cell = boardController != null && boardController.BoardView != null
                ? boardController.BoardView.GetCellView(index)
                : null;
            if (cell != null && cell.ItemAnimator != null)
            {
                cell.ItemAnimator.SetMergeTarget(true, mergeConfig);
            }
        }

        void ClearMergePreview()
        {
            ClearMergePreviewImmediate();
        }

        void ClearMergePreviewSoft()
        {
            if (_previewMergeIndex == BoardController.NoSelectionIndex)
            {
                return;
            }

            var cell = boardController != null && boardController.BoardView != null
                ? boardController.BoardView.GetCellView(_previewMergeIndex)
                : null;
            if (cell != null && cell.ItemAnimator != null)
            {
                cell.ItemAnimator.SetMergeTarget(false);
            }

            _previewMergeIndex = BoardController.NoSelectionIndex;
        }

        void ClearMergePreviewImmediate()
        {
            if (_previewMergeIndex == BoardController.NoSelectionIndex)
            {
                return;
            }

            var cell = boardController != null && boardController.BoardView != null
                ? boardController.BoardView.GetCellView(_previewMergeIndex)
                : null;
            if (cell != null && cell.ItemAnimator != null)
            {
                cell.ItemAnimator.ClearMergeTargetImmediate();
            }

            _previewMergeIndex = BoardController.NoSelectionIndex;
        }

        void UpdateMergeTargetAttraction(int mergeIndex)
        {
            if (mergeIndex == BoardController.NoSelectionIndex || dragView == null || boardController == null ||
                boardController.BoardView == null)
            {
                return;
            }

            var cell = boardController.BoardView.GetCellView(mergeIndex);
            if (cell == null || cell.ItemAnimator == null)
            {
                return;
            }

            var cellCenter = GetCellItemLayerPosition(mergeIndex);
            cell.ItemAnimator.UpdateMergeAttraction(dragView.CurrentDragPosition, cellCenter);
        }

        void TickMagnet(float dt, int magnetIndex)
        {
            var targetBlend = magnetIndex != BoardController.NoSelectionIndex ? 1f : 0f;
            if (magnetIndex != BoardController.NoSelectionIndex)
            {
                _magnetPos = GetCellItemLayerPosition(magnetIndex);
                if (_previewMergeIndex == magnetIndex)
                {
                    UpdateMergeTargetAttraction(magnetIndex);
                }
            }

            _magnetBlend = Mathf.MoveTowards(_magnetBlend, targetBlend, dt / 0.08f);
        }

        void SetCobwebPreview(int index)
        {
            if (_previewCobwebIndex == index)
            {
                return;
            }

            ClearCobwebPreviewSoft();
            _previewCobwebIndex = index;
            var cobwebConfig = boardController != null ? boardController.CobwebAnimationConfig : null;
            var cell = boardController != null && boardController.BoardView != null
                ? boardController.BoardView.GetCellView(index)
                : null;
            if (cell != null)
            {
                if (cell.ItemAnimator != null)
                {
                    cell.ItemAnimator.SetCobwebUnlockTarget(true, cobwebConfig);
                }

                cell.SetCobwebUnlockHover(true, cobwebConfig);
            }
        }

        void ClearCobwebPreviewSoft()
        {
            if (_previewCobwebIndex == BoardController.NoSelectionIndex)
            {
                return;
            }

            var cell = boardController != null && boardController.BoardView != null
                ? boardController.BoardView.GetCellView(_previewCobwebIndex)
                : null;
            if (cell != null)
            {
                if (cell.ItemAnimator != null)
                {
                    cell.ItemAnimator.SetCobwebUnlockTarget(false);
                }

                cell.SetCobwebUnlockHover(false, boardController != null ? boardController.CobwebAnimationConfig : null);
            }

            _previewCobwebIndex = BoardController.NoSelectionIndex;
        }

        void ClearCobwebPreviewImmediate()
        {
            if (_previewCobwebIndex == BoardController.NoSelectionIndex)
            {
                return;
            }

            var cell = boardController != null && boardController.BoardView != null
                ? boardController.BoardView.GetCellView(_previewCobwebIndex)
                : null;
            if (cell != null)
            {
                if (cell.ItemAnimator != null)
                {
                    cell.ItemAnimator.ClearMergeTargetImmediate();
                }

                cell.ResetCobwebVisualImmediate();
            }

            _previewCobwebIndex = BoardController.NoSelectionIndex;
        }

        void ClearInteractionPreviews()
        {
            ClearMergePreviewImmediate();
            ClearCobwebPreviewImmediate();
            ClearUiDropTargets();
        }

        void ClearInteractionPreviewsSoft()
        {
            ClearMergePreviewSoft();
            ClearCobwebPreviewSoft();
        }

        bool IsTrackedPointerHeld()
        {
            if (_touch)
            {
                var touchscreen = Touchscreen.current;
                if (touchscreen != null)
                {
                    var touches = touchscreen.touches;
                    for (var i = 0; i < touches.Count; i++)
                    {
                        var touch = touches[i];
                        if (_touchId != 0 && touch.touchId.ReadValue() != _touchId)
                        {
                            continue;
                        }

                        if (touch.isInProgress)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }

        void ApplyEventSystemThreshold()
        {
            if (EventSystem.current == null || config == null)
            {
                return;
            }

            var canvas = boardController != null && boardController.BoardRoot != null
                ? boardController.BoardRoot.GetComponentInParent<Canvas>()
                : null;
            var scale = canvas != null ? canvas.scaleFactor : 1f;
            EventSystem.current.pixelDragThreshold = Mathf.RoundToInt(config.GetDragThresholdScreen(scale));
        }

        void CacheHighlightSprite()
        {
            if (boardController == null || boardController.SelectionView == null)
            {
                return;
            }

            var back = boardController.SelectionView.SelectionBack;
            if (back == null)
            {
                return;
            }

            var image = back.GetComponent<Image>();
            if (image != null)
            {
                _highlightSprite = image.sprite;
            }
        }
    }
}
