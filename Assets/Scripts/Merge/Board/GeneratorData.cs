using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [Serializable]
    public class GeneratorData
    {
        [SerializeField] int generatorId = BoardCellState.EmptyItemId;
        [SerializeField] List<int> possibleOutputItems = new List<int>();

        public int GeneratorId
        {
            get => generatorId;
            set => generatorId = value;
        }

        public List<int> PossibleOutputItems => possibleOutputItems;

        public bool TryGetDeterministicOutput(out int itemId)
        {
            itemId = BoardCellState.EmptyItemId;
            if (possibleOutputItems == null || possibleOutputItems.Count == 0)
            {
                return false;
            }

            itemId = possibleOutputItems[0];
            return itemId != BoardCellState.EmptyItemId;
        }
    }
}
