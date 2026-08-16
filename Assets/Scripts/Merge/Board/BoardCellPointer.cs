using UnityEngine;
using UnityEngine.EventSystems;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardCellPointer : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        ICancelHandler
    {
        [SerializeField] BoardCellView cellView;
        [SerializeField] BoardController boardController;
        [SerializeField] BoardItemAnimator itemAnimator;

        bool _pressedItem;
        bool _pointerInside;

        public void Configure(BoardCellView view, BoardController controller, BoardItemAnimator animator)
        {
            cellView = view;
            boardController = controller;
            itemAnimator = animator;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            if (boardController != null && boardController.IsDragInteractionActive)
            {
                return;
            }

            if (!UiPointerUtility.SupportsHover(eventData) || !TryGetItemCell(out _))
            {
                return;
            }

            if (itemAnimator != null)
            {
                itemAnimator.SetHovered(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            if (boardController != null && boardController.IsDragInteractionActive)
            {
                return;
            }

            if (itemAnimator != null)
            {
                itemAnimator.SetHovered(false);
            }
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData != null)
            {
                eventData.useDragThreshold = true;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressedItem = false;
            if (boardController != null && boardController.DragController != null &&
                boardController.DragController.Phase != BoardDragPhase.Idle &&
                boardController.DragController.ActivePointerId != eventData.pointerId)
            {
                return;
            }

            if (!TryGetItemCell(out _))
            {
                return;
            }

            if (cellView != null && boardController != null && boardController.IsCellInteractionLocked(cellView.Index))
            {
                return;
            }

            if (cellView != null && cellView.IsTransientAnimationRunning())
            {
                if (boardController != null && boardController.DragController != null)
                {
                    boardController.DragController.PrepareCellForPossiblePickup(cellView);
                }
                else
                {
                    cellView.CancelTransientPresentationForPickup();
                }
            }

            if (boardController != null && boardController.DragController != null &&
                !boardController.DragController.HandlePointerDown(cellView, eventData))
            {
                return;
            }

            _pressedItem = true;
            if (itemAnimator != null)
            {
                itemAnimator.PlayPressAnticipation();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (boardController != null && boardController.DragController != null)
            {
                boardController.DragController.HandleBeginDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (boardController != null && boardController.DragController != null)
            {
                boardController.DragController.HandleDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (boardController != null && boardController.DragController != null)
            {
                boardController.DragController.HandleEndDrag(eventData);
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (boardController != null && boardController.DragController != null)
            {
                boardController.DragController.HandleCancel();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (boardController != null && boardController.DragController != null &&
                boardController.DragController.HandlePointerUp(eventData))
            {
                _pressedItem = false;
                return;
            }

            if (_pressedItem)
            {
                if (itemAnimator != null)
                {
                    itemAnimator.PlayClickRelease();
                    if (!_pointerInside)
                    {
                        itemAnimator.SetHovered(false);
                    }
                }

                if (boardController != null && cellView != null)
                {
                    if (!boardController.IsCellInteractionLocked(cellView.Index))
                    {
                        boardController.SelectCell(cellView.Index);
                        if (boardController.IsGeneratorCell(cellView.Index))
                        {
                            boardController.TryActivateGenerator(cellView.Index, eventData.position);
                        }
                    }
                }
            }
            else if (boardController != null && cellView != null)
            {
                boardController.ClearSelection();
            }

            _pressedItem = false;
        }

        bool TryGetItemCell(out BoardCellState cell)
        {
            cell = null;
            if (boardController == null || boardController.State == null || cellView == null)
            {
                return false;
            }

            if (!boardController.State.IsValidIndex(cellView.Index))
            {
                return false;
            }

            cell = boardController.State.GetCell(cellView.Index);
            return cell != null && cell.HasItem && !cell.IsBox;
        }
    }
}
