namespace SanIsland.Merge
{
    public struct GeneratorSpawnResult
    {
        public bool Success;
        public bool BoardFull;
        public int GeneratorIndex;
        public int SpawnCellIndex;
        public int GeneratedItemId;
        public int InteractionLockToken;

        public static GeneratorSpawnResult Failed(int generatorIndex, bool boardFull = false)
        {
            return new GeneratorSpawnResult
            {
                Success = false,
                BoardFull = boardFull,
                GeneratorIndex = generatorIndex,
                SpawnCellIndex = BoardController.NoSelectionIndex,
                GeneratedItemId = BoardCellState.EmptyItemId,
                InteractionLockToken = 0
            };
        }
    }
}
