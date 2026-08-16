using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    public class BoardView : MonoBehaviour
    {
        [SerializeField] List<BoardCellView> cells = new List<BoardCellView>(BoardState.CellCount);

        BoardState _state;
        MergeItemDatabase _itemDatabase;
        BoardVisualConfig _visuals;

        public IReadOnlyList<BoardCellView> Cells => cells;

        public BoardCellView GetCellView(int index)
        {
            if (cells == null || index < 0 || index >= cells.Count)
            {
                return null;
            }

            return cells[index];
        }

        public void BindInteraction(BoardController controller, BoardItemAnimationConfig animationConfig, UiInteractionFeedbackConfig hoverConfig = null)
        {
            if (cells == null)
            {
                return;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                var animator = cell.ItemAnimator != null ? cell.ItemAnimator : cell.GetComponent<BoardItemAnimator>();
                if (animator == null)
                {
                    animator = cell.gameObject.AddComponent<BoardItemAnimator>();
                }

                animator.Configure(
                    animationConfig,
                    cell.ItemImage != null ? cell.ItemImage.rectTransform : null,
                    cell.transform as RectTransform,
                    hoverConfig);
                cell.BindAnimator(animator);

                var pointer = cell.GetComponent<BoardCellPointer>();
                if (pointer == null)
                {
                    pointer = cell.gameObject.AddComponent<BoardCellPointer>();
                }

                pointer.Configure(cell, controller, animator);
            }
        }

        public void Bind(BoardState state, MergeItemDatabase itemDatabase, BoardVisualConfig visuals)
        {
            _state = state;
            _itemDatabase = itemDatabase;
            _visuals = visuals;
        }

        public void SetCells(List<BoardCellView> cellViews)
        {
            cells = cellViews ?? new List<BoardCellView>();
        }

        void Awake()
        {
            if (cells != null && cells.Count > 0)
            {
                ValidateCells();
            }
        }

        public void RefreshAll()
        {
            if (_state == null)
            {
                return;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                RefreshCell(i);
            }
        }

        public void RefreshCell(int index)
        {
            if (_state == null || !_state.IsValidIndex(index))
            {
                return;
            }

            if (index < 0 || index >= cells.Count || cells[index] == null)
            {
                Debug.LogError($"[BoardView] Missing BoardCellView for index {index}.");
                return;
            }

            cells[index].Render(_state.GetCell(index), _itemDatabase, _visuals);
        }

        public void ClearItemDragHides()
        {
            if (cells == null)
            {
                return;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i] != null)
                {
                    cells[i].SetHideItemForDrag(false);
                }
            }
        }

        public void ValidateCells()
        {
            if (cells == null || cells.Count != BoardState.CellCount)
            {
                Debug.LogError($"[BoardView] Expected {BoardState.CellCount} cell views, found {(cells == null ? 0 : cells.Count)}.");
                return;
            }

            var seen = new HashSet<int>();
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null)
                {
                    Debug.LogError($"[BoardView] Null cell view at list index {i}.");
                    continue;
                }

                if (cell.Index != i)
                {
                    Debug.LogError($"[BoardView] Cell list index {i} has BoardCellView.Index={cell.Index}.");
                }

                if (!seen.Add(cell.Index))
                {
                    Debug.LogError($"[BoardView] Duplicate cell index {cell.Index}.");
                }
            }

            for (var expected = 0; expected < BoardState.CellCount; expected++)
            {
                if (!seen.Contains(expected))
                {
                    Debug.LogError($"[BoardView] Missing cell index {expected}.");
                }
            }
        }
    }
}
