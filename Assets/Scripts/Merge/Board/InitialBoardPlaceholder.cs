namespace SanIsland.Merge
{
    public static class InitialBoardPlaceholder
    {
        public static void Apply(InitialBoardDefinition definition, MergeItemDatabase database)
        {
            InitialBoardPuzzleLayout.Apply(definition, database);
        }
    }
}
