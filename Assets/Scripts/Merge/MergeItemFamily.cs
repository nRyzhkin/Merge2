namespace SanIsland.Merge
{
    /// <summary>
    /// Merge item family. Integer values are part of the stable ID scheme and must never be reused or reordered.
    /// Add new families with the next unused value.
    /// </summary>
    public enum MergeItemFamily
    {
        Tools = 1,
        Cleaning = 2,
        Coffee = 3,
        Bakery = 4,
        Beach = 5,
        Cocktails = 6
    }
}
