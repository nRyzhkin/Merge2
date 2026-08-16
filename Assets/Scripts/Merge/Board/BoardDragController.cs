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

        public BoardDragPhase Phase => _phase;
        public int ActivePointerId => _pointerId;
        public bool IsBusy => _phase == BoardDragPhase.Dragging || _phase == BoardDragPhase.Dropping;
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

                dragView.TickFollowOrPickup(dt, GetFollowTarget());
                UpdateDropTarget();
                return;
            }

            if (_phase == BoardDragPhase.Dropping)
            {
                if (dragView.TickDrop(dt))
                {
                    return;
                }

                FinishDrop();
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

            if (sourceView.ItemAnimator != null)
            {
                sourceView.ItemAnimator.BeginDrag();
            }

            sourceView.SetHideItemForDrag(true);
            if (boardController != null)
            {
                boardController.PrepareDragPresentation(_sourceIndex);
            }

            dragView.EnsureChildren();
            dragView.BeginPickup(item.sprite, size, item.preserveAspect, item.color, startPos, startScale);
            _phase = BoardDragPhase.Dragging;
            _dropIndex = BoardController.NoSelectionIndex;
        }

        void BeginDropping()
        {
            if (_phase != BoardDragPhase.Dragging)
            {
                return;
            }

            if (dragView != null)
            {
                dragView.HideDropTarget();
            }

            var targetIndex = ResolveDropIndex();
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
                dragView.HideDropTarget();
                return;
            }

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

        bool IsEmptyOpenCell(int index)
        {
            if (boardController == null || boardController.State == null || !boardController.State.IsValidIndex(index))
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

            return local;
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
