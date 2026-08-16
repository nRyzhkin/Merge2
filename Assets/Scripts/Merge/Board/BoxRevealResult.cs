namespace SanIsland.Merge
{
    public struct BoxRevealResult
    {
        public int CellIndex;
        public int RevealedItemId;

        public bool RevealedItem => RevealedItemId != BoardCellState.EmptyItemId;
    }
}
