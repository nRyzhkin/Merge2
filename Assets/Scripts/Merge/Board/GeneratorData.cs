using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [Serializable]
    public class GeneratorData
    {
        [SerializeField] int generatorId = BoardCellState.EmptyItemId;
        [SerializeField] List<GeneratorDropEntry> dropTable = new List<GeneratorDropEntry>();
        [SerializeField] int capacityDrops = 16;
        [SerializeField] float cooldownSeconds = 60f;

        public int GeneratorId
        {
            get => generatorId;
            set => generatorId = value;
        }

        public List<GeneratorDropEntry> DropTable => dropTable;

        public int CapacityDrops
        {
            get => capacityDrops;
            set => capacityDrops = value;
        }

        public float CooldownSeconds
        {
            get => cooldownSeconds;
            set => cooldownSeconds = value;
        }
    }
}
