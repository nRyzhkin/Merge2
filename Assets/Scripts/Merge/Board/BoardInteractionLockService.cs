using System.Collections.Generic;

namespace SanIsland.Merge
{
    /// <summary>
    /// Token-based interaction locks for cells occupied by an in-flight merge critical phase.
    /// </summary>
    public class BoardInteractionLockService
    {
        readonly Dictionary<int, int> _cellRefCount = new Dictionary<int, int>(8);
        readonly Dictionary<int, List<int>> _tokenCells = new Dictionary<int, List<int>>(8);
        int _nextToken;

        public bool IsLocked(int cellIndex)
        {
            return _cellRefCount.TryGetValue(cellIndex, out var count) && count > 0;
        }

        public int Acquire(int cellA, int cellB)
        {
            return Acquire(cellA, cellB, BoardController.NoSelectionIndex);
        }

        public int Acquire(int cellA, int cellB, int cellC)
        {
            var token = ++_nextToken;
            var cells = new List<int>(3);
            AddCell(token, cells, cellA);
            if (cellB != cellA)
            {
                AddCell(token, cells, cellB);
            }

            if (cellC != cellA && cellC != cellB)
            {
                AddCell(token, cells, cellC);
            }

            _tokenCells[token] = cells;
            return token;
        }

        public void ReleaseCell(int token, int cellIndex)
        {
            if (!_tokenCells.TryGetValue(token, out var cells))
            {
                return;
            }

            for (var i = cells.Count - 1; i >= 0; i--)
            {
                if (cells[i] != cellIndex)
                {
                    continue;
                }

                cells.RemoveAt(i);
                Decrement(cellIndex);
            }

            if (cells.Count == 0)
            {
                _tokenCells.Remove(token);
            }
        }

        public void Release(int token)
        {
            if (!_tokenCells.TryGetValue(token, out var cells))
            {
                return;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                Decrement(cells[i]);
            }

            _tokenCells.Remove(token);
        }

        public void ReleaseAll()
        {
            _cellRefCount.Clear();
            _tokenCells.Clear();
        }

        void AddCell(int token, List<int> cells, int cellIndex)
        {
            if (cellIndex < 0)
            {
                return;
            }

            cells.Add(cellIndex);
            if (_cellRefCount.TryGetValue(cellIndex, out var count))
            {
                _cellRefCount[cellIndex] = count + 1;
            }
            else
            {
                _cellRefCount[cellIndex] = 1;
            }
        }

        void Decrement(int cellIndex)
        {
            if (!_cellRefCount.TryGetValue(cellIndex, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _cellRefCount.Remove(cellIndex);
            }
            else
            {
                _cellRefCount[cellIndex] = count - 1;
            }
        }
    }
}
