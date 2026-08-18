using System.Collections.Generic;

namespace SanIsland.Merge
{
    public sealed class GameProgressionState
    {
        readonly HashSet<MergeItemFamily> _unlockedFamilies = new HashSet<MergeItemFamily>();

        public bool IsFamilyUnlocked(MergeItemFamily family)
        {
            return _unlockedFamilies.Contains(family);
        }

        public bool UnlockFamily(MergeItemFamily family)
        {
            return _unlockedFamilies.Add(family);
        }

        public void LockFamily(MergeItemFamily family)
        {
            _unlockedFamilies.Remove(family);
        }

        public IReadOnlyCollection<MergeItemFamily> GetUnlockedFamilies()
        {
            return _unlockedFamilies;
        }

        public void ResetDevelopment()
        {
            _unlockedFamilies.Clear();
        }
    }
}
