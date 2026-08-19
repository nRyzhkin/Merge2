namespace SanIsland.Merge
{
    public static class InitialBoardPreview
    {
        public static InitialBoardPreviewCell Resolve(InitialBoardCellData cell, InitialBoardPreviewMode mode)
        {
            var preview = new InitialBoardPreviewCell
            {
                Index = cell != null ? cell.index : 0,
                VisibleItemId = BoardCellState.EmptyItemId,
                BoxVisualIndex = cell != null ? cell.boxVisualIndex : 0
            };

            if (cell == null)
            {
                return preview;
            }

            switch (mode)
            {
                case InitialBoardPreviewMode.AfterBoxesRevealed:
                    ResolveAfterBoxes(cell, ref preview, stripCobweb: false);
                    break;
                case InitialBoardPreviewMode.AfterCobwebRemoved:
                    ResolveAfterBoxes(cell, ref preview, stripCobweb: true);
                    break;
                default:
                    ResolveInitial(cell, ref preview);
                    break;
            }

            return preview;
        }

        static void ResolveInitial(InitialBoardCellData cell, ref InitialBoardPreviewCell preview)
        {
            switch (cell.state)
            {
                case CellInitialState.Item:
                    preview.VisibleItemId = cell.itemId;
                    break;
                case CellInitialState.Generator:
                    preview.VisibleItemId = cell.itemId;
                    break;
                case CellInitialState.CobwebItem:
                    preview.VisibleItemId = cell.itemId;
                    preview.IsCobweb = true;
                    break;
                case CellInitialState.Box:
                    preview.IsBox = true;
                    break;
            }
        }

        static void ResolveAfterBoxes(InitialBoardCellData cell, ref InitialBoardPreviewCell preview, bool stripCobweb)
        {
            if (cell.state != CellInitialState.Box)
            {
                ResolveInitial(cell, ref preview);
                if (stripCobweb)
                {
                    preview.IsCobweb = false;
                }

                return;
            }

            var hidden = cell.Hidden;
            switch (hidden.type)
            {
                case BoxRevealType.Item:
                    preview.VisibleItemId = hidden.itemId;
                    break;
                case BoxRevealType.CobwebItem:
                    preview.VisibleItemId = hidden.itemId;
                    preview.IsCobweb = !stripCobweb;
                    break;
                case BoxRevealType.Generator:
                    preview.VisibleItemId = hidden.itemId;
                    break;
                case BoxRevealType.GeneratorPartPlaceholder:
                    preview.IsPlaceholder = true;
                    break;
            }
        }
    }
}
