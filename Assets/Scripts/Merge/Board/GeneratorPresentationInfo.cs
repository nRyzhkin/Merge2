namespace SanIsland.Merge
{
    public struct GeneratorPresentationInfo
    {
        public bool IsValid;
        public int AvailableDrops;
        public int MaxDrops;
        public float RechargeProgress;
        public float SecondsUntilNextCharge;

        public static GeneratorPresentationInfo Invalid => default;
    }
}
