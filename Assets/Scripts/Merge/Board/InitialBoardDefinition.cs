using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "InitialBoardDefinition", menuName = "San Island/Initial Board Definition")]
    public class InitialBoardDefinition : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Data/Boards/InitialBoardDefinition.asset";

        [SerializeField] List<InitialBoardCellData> cells = new List<InitialBoardCellData>(BoardState.CellCount);

        public IReadOnlyList<InitialBoardCellData> Cells => cells;

        public InitialBoardCellData GetCell(int index)
        {
            EnsureCells();
            if (index < 0 || index >= cells.Count)
            {
                return null;
            }

            return cells[index];
        }

        public void EnsureCells()
        {
            if (cells == null)
            {
                cells = new List<InitialBoardCellData>(BoardState.CellCount);
            }

            while (cells.Count < BoardState.CellCount)
            {
                cells.Add(CreateEmpty(cells.Count));
            }

            if (cells.Count > BoardState.CellCount)
            {
                cells.RemoveRange(BoardState.CellCount, cells.Count - BoardState.CellCount);
            }

            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i] == null)
                {
                    cells[i] = CreateEmpty(i);
                }

                cells[i].index = i;
                if (cells[i].hidden == null)
                {
                    cells[i].hidden = new BoxRevealContent();
                }

                cells[i].SyncAuthoredFlagsFromState();
            }
        }

        public void ClearAll()
        {
            EnsureCells();
            for (var i = 0; i < cells.Count; i++)
            {
                cells[i] = CreateEmpty(i);
            }
        }

        static InitialBoardCellData CreateEmpty(int index)
        {
            return new InitialBoardCellData
            {
                index = index,
                state = CellInitialState.Empty,
                itemId = BoardCellState.EmptyItemId,
                boxVisualIndex = 0,
                hidden = new BoxRevealContent
                {
                    type = BoxRevealType.Empty,
                    itemId = BoardCellState.EmptyItemId
                }
            };
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            EnsureCells();
        }
#endif
    }
}
