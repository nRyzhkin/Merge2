namespace SanIsland.Merge
{
    public struct GeneratorSpawnResult
    {
        public bool Success;
        public bool BoardFull;
        public bool Recharging;
        public bool InsufficientEnergy;
        public int GeneratorIndex;
        public int SpawnCellIndex;
        public int GeneratedItemId;
        public int InteractionLockToken;

        public static GeneratorSpawnResult Failed(
            int generatorIndex,
            bool boardFull = false,
            bool recharging = false,
            bool insufficientEnergy = false)
        {
            return new GeneratorSpawnResult
            {
                Success = false,
                BoardFull = boardFull,
                Recharging = recharging,
                InsufficientEnergy = insufficientEnergy,
                GeneratorIndex = generatorIndex,
                SpawnCellIndex = BoardController.NoSelectionIndex,
                GeneratedItemId = BoardCellState.EmptyItemId,
                InteractionLockToken = 0
            };
        }
    }
}
