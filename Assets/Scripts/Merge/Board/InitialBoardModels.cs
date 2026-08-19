using System;
using UnityEngine;

namespace SanIsland.Merge
{
    public enum CellInitialState
    {
        Empty = 0,
        Item = 1,
        Generator = 2,
        CobwebItem = 3,
        Box = 4
    }

    public enum BoxRevealType
    {
        Empty = 0,
        Item = 1,
        CobwebItem = 2,
        Generator = 3,
        GeneratorPartPlaceholder = 4
    }

    public enum InitialBoardPreviewMode
    {
        Initial = 0,
        AfterBoxesRevealed = 1,
        AfterCobwebRemoved = 2
    }

    [Serializable]
    public class BoxRevealContent
    {
        public BoxRevealType type = BoxRevealType.Empty;
        public int itemId = BoardCellState.EmptyItemId;

        public bool HasItemId => itemId != BoardCellState.EmptyItemId;
    }

    [Serializable]
    public class InitialBoardCellData
    {
        public int index;
        public CellInitialState state = CellInitialState.Empty;
        public int itemId = BoardCellState.EmptyItemId;
        public CellBlockType blockType = CellBlockType.None;
        public bool locked;
        public bool cobweb;
        public int boxVisualIndex;
        public BoxRevealContent hidden = new BoxRevealContent();

        public CellBlockType ResolvedBlockType =>
            state == CellInitialState.Box ? CellBlockType.Box : CellBlockType.None;

        public bool IsLocked =>
            locked || state == CellInitialState.CobwebItem || cobweb;

        public bool IsCobweb =>
            cobweb ||
            state == CellInitialState.CobwebItem ||
            (state == CellInitialState.Box && Hidden.type == BoxRevealType.CobwebItem);

        public void SyncAuthoredFlagsFromState()
        {
            blockType = ResolvedBlockType;
            cobweb = state == CellInitialState.CobwebItem ||
                     (state == CellInitialState.Box && Hidden.type == BoxRevealType.CobwebItem);
            locked = cobweb || state == CellInitialState.CobwebItem;
        }

        public void ApplyAuthoredFlagsToState()
        {
            if (blockType == CellBlockType.Box || state == CellInitialState.Box)
            {
                state = CellInitialState.Box;
                blockType = CellBlockType.Box;
                if (cobweb || locked)
                {
                    Hidden.type = Hidden.type == BoxRevealType.Generator
                        ? BoxRevealType.Generator
                        : BoxRevealType.CobwebItem;
                }

                return;
            }

            blockType = CellBlockType.None;
            if (state == CellInitialState.Empty)
            {
                locked = false;
                cobweb = false;
                return;
            }

            if (cobweb || locked)
            {
                if (state != CellInitialState.Generator)
                {
                    state = CellInitialState.CobwebItem;
                }

                locked = true;
                cobweb = true;
                return;
            }

            if (state == CellInitialState.CobwebItem)
            {
                state = CellInitialState.Item;
            }

            locked = false;
            cobweb = false;
        }

        public BoxRevealContent Hidden
        {
            get
            {
                if (hidden == null)
                {
                    hidden = new BoxRevealContent();
                }

                return hidden;
            }
        }
    }

    public struct InitialBoardPreviewCell
    {
        public int Index;
        public bool IsBox;
        public bool IsCobweb;
        public bool IsPlaceholder;
        public int VisibleItemId;
        public int BoxVisualIndex;
    }
}
