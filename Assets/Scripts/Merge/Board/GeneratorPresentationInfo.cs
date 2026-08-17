namespace SanIsland.Merge
{
    public struct GeneratorPresentationInfo
    {
        public bool IsValid;
        public int AvailableDrops;
        public int CapacityDrops;
        public float CooldownRemainingSeconds;

        public static GeneratorPresentationInfo Invalid => default;
    }
}
