using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [Serializable]
    public class GeneratorData : ISerializationCallbackReceiver
    {
        [SerializeField] int generatorId = BoardCellState.EmptyItemId;
        [SerializeField] List<GeneratorDropEntry> dropTable = new List<GeneratorDropEntry>();
        [SerializeField] int dropsPerCharge = 10;
        [SerializeField] int maxStoredCharges = 4;
        [SerializeField] float rechargeSecondsPerCharge = 120f;

        [SerializeField] int capacityDrops;
        [SerializeField] float cooldownSeconds;

        public int GeneratorId
        {
            get => generatorId;
            set => generatorId = value;
        }

        public List<GeneratorDropEntry> DropTable => dropTable;

        public int DropsPerCharge
        {
            get => dropsPerCharge;
            set => dropsPerCharge = value;
        }

        public int MaxStoredCharges
        {
            get => maxStoredCharges;
            set => maxStoredCharges = value;
        }

        public float RechargeSecondsPerCharge
        {
            get => rechargeSecondsPerCharge;
            set => rechargeSecondsPerCharge = value;
        }

        public int MaxAvailableDrops => Math.Max(0, dropsPerCharge) * Math.Max(0, maxStoredCharges);

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (dropsPerCharge <= 0 && capacityDrops > 0)
            {
                dropsPerCharge = capacityDrops;
                if (maxStoredCharges <= 0)
                {
                    maxStoredCharges = 1;
                }
            }

            if (rechargeSecondsPerCharge <= 0f && cooldownSeconds > 0f)
            {
                rechargeSecondsPerCharge = cooldownSeconds;
            }
        }
    }
}
