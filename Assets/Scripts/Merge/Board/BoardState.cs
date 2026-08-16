using System;
using System.Collections.Generic;

namespace SanIsland.Merge
{
    [Serializable]
    public class BoardState
    {
        public const int Columns = 10;
        public const int Rows = 6;
        public const int CellCount = Columns * Rows;

        readonly BoardCellState[] _cells;

        public int ColumnCount => Columns;
        public int RowCount => Rows;
        public IReadOnlyList<BoardCellState> Cells => _cells;

        public BoardState()
        {
            _cells = new BoardCellState[CellCount];
            for (var i = 0; i < CellCount; i++)
            {
                _cells[i] = new BoardCellState { Index = i };
            }
        }

        public BoardCellState GetCell(int index)
        {
            if (!IsValidIndex(index))
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Board index must be 0..{CellCount - 1}.");
            }

            return _cells[index];
        }

        public BoardCellState GetCell(int row, int column)
        {
            return GetCell(GetIndex(row, column));
        }

        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < CellCount;
        }

        public bool IsValidCoordinate(int row, int column)
        {
            return row >= 0 && row < Rows && column >= 0 && column < Columns;
        }

        public int GetIndex(int row, int column)
        {
            if (!IsValidCoordinate(row, column))
            {
                throw new ArgumentOutOfRangeException($"Invalid board coordinate ({row}, {column}).");
            }

            return row * Columns + column;
        }

        public void GetCoordinates(int index, out int row, out int column)
        {
            if (!IsValidIndex(index))
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Board index must be 0..{CellCount - 1}.");
            }

            row = index / Columns;
            column = index % Columns;
        }

        public void ClearAll()
        {
            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i].Clear();
                _cells[i].Index = i;
            }
        }
    }
}

