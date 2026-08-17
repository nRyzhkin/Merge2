using System;
using UnityEngine;

namespace SanIsland.Merge
{
    [Serializable]
    public class GeneratorDropEntry
    {
        [SerializeField] int outputItemId = BoardCellState.EmptyItemId;
        [SerializeField] int weight = 1;

        public int OutputItemId
        {
            get => outputItemId;
            set => outputItemId = value;
        }

        public int Weight
        {
            get => weight;
            set => weight = value;
        }
    }
}
