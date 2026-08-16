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

        public BoardDragPhase Phase => _phase;
        public int ActivePointerId => _pointerId;
        public bool IsBusy =>
            _phase == BoardDragPhase.Dragging ||
            _phase == BoardDragPhase.Dropping;
        public bool IsDragInteractionActive => IsBusy;

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

            PrepareCellForPossiblePickup(cell);

            if (boardController.IsCellInteractionLocked(cell.Index))
            {
                return false;
            }

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

            if (cell.IsTransientAnimationRunning() || cell.IsCobwebBreakPlaying)
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

            if (dragView != null)
            {
                dragView.HideDropTargetImmediate();
            }

            // Keep last valid preview — pointer often leaves the cell by a few px on release.
            var stickyMergeIndex = _previewMergeIndex;
            var stickyCobwebIndex = _previewCobwebIndex;
            ClearInteractionPreviews();
            var underIndex = FindCellIndexUnderPointer();
            var intentTarget = stickyMergeIndex != BoardController.NoSelectionIndex
                ? stickyMergeIndex
                : stickyCobwebIndex != BoardController.NoSelectionIndex
                    ? stickyCobwebIndex
                    : underIndex;
            var canMerge = boardController != null && boardController.CanMerge(_sourceIndex, intentTarget);
            var canUnlock = !canMerge && boardController != null &&
                            boardController.CanUnlockCobweb(_sourceIndex, intentTarget);
            Debug.Log(
                $"[Cobweb] BeginDropping source={FormatCell(_sourceIndex)} under={FormatCell(underIndex)} " +
                $"stickyMerge={FormatCell(stickyMergeIndex)} stickyCobweb={FormatCell(stickyCobwebIndex)} " +
                $"intent={FormatCell(intentTarget)} canMerge={canMerge} canUnlock={canUnlock} " +
                $"presenter={(boardController != null && boardController.CobwebPresenter != null)} " +
                $"config={(boardController != null && boardController.CobwebAnimationConfig != null)}");

            if (canMerge)
            {
                BeginMerging(intentTarget);
                return;
            }

            if (canUnlock)
            {
                BeginUnlocking(intentTarget);
                return;
            }

            var targetIndex = ResolveDropIndex();
            Debug.Log($"[Cobweb] fallback drop ResolveDropIndex={FormatCell(targetIndex)} (under was {FormatCell(underIndex)})");
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
            var landingIndex = _dropIndex != BoardController.NoSelectionIndex ? _dropIndex : _sourceIndex;
            if (dragView != null)
            {
                dragView.HideImmediate();
            }

            RevealCellItem(landingIndex);
            RevealCellItem(_sourceIndex);

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
                Debug.LogWarning($"[Cobweb] BeginUnlocking CanUnlock became false source={_sourceIndex} target={lockedTargetIndex}");
                _dropIndex = _sourceIndex;
                _phase = BoardDragPhase.Dropping;
                dragView.BeginDrop(GetCellItemLayerPosition(_sourceIndex), true);
                return;
            }

            var destination = boardController.FindUnlockDestination(_sourceIndex, lockedTargetIndex);
            Debug.Log(
                $"[Cobweb] BeginUnlocking OK source={_sourceIndex} locked={lockedTargetIndex} dest={destination}");

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
            Debug.Log($"[Cobweb] hover enter locked target={index} from source={_sourceIndex}");
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
