namespace SanIsland.Merge
{
    public struct DisplaceResult
    {
        public bool Success;
        public int SourceIndex;
        public int OccupiedTargetIndex;
        public int DestinationIndex;
        public int DraggedItemId;
        public int DisplacedItemId;

        public static DisplaceResult Failed(
            int sourceIndex,
            int occupiedTargetIndex,
            int destinationIndex = BoardController.NoSelectionIndex)
        {
            return new DisplaceResult
            {
                Success = false,
                SourceIndex = sourceIndex,
                OccupiedTargetIndex = occupiedTargetIndex,
                DestinationIndex = destinationIndex,
                DraggedItemId = BoardCellState.EmptyItemId,
                DisplacedItemId = BoardCellState.EmptyItemId
            };
        }
    }
}
