namespace SanIsland.Merge
{
    public struct MergeResult
    {
        public bool Success;
        public int SourceIndex;
        public int TargetIndex;
        public int ResultingItemId;
        public bool NewlyDiscovered;
    }
}
