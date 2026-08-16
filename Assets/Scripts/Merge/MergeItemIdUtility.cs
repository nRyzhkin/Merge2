using System;

namespace SanIsland.Merge
{
    /// <summary>
    /// Deterministic, human-readable IDs:
    /// family * 1000 + kindOffset + level
    /// Tools Normal 1001-1099, Tools Generator 1101-1199,
    /// Cleaning Normal 2001-2099, Cleaning Generator 2101-2199,
    /// Coffee Normal 3001-3099, Coffee Generator 3101-3199.
    /// </summary>
    public static class MergeItemIdUtility
    {
        public const int FamilyStride = 1000;
        public const int GeneratorOffset = 100;
        public const int MaxLevel = 99;
        public const int NoNextItemId = -1;

        public static int ComputeStableId(MergeItemFamily family, MergeItemKind kind, int level)
        {
            if (level < 1 || level > MaxLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Level must be between 1 and 99.");
            }

            var familyBase = (int)family * FamilyStride;
            var kindBase = kind == MergeItemKind.Generator ? GeneratorOffset : 0;
            return familyBase + kindBase + level;
        }
    }
}
