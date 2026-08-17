namespace SanIsland.Merge
{
    public sealed class GeneratorRandomService : IGeneratorRandom
    {
        readonly System.Random _random = new System.Random();

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }
    }
}
