namespace SanIsland.Merge
{
    public interface IGeneratorRandom
    {
        /// <summary>Returns value in [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);
    }
}
