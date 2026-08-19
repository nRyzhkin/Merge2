using System;
using System.Collections.Generic;

namespace SanIsland.Merge
{
    [Serializable]
    public class InventoryState
    {
        public const int SlotCount = 12;

        public List<InventorySlot> slots = new List<InventorySlot>(SlotCount);

        public int OccupiedCount
        {
            get
            {
                EnsureSlots();
                var count = 0;
                for (var i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && !slots[i].IsEmpty)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsFull
        {
            get
            {
                EnsureSlots();
                return FindEmptySlotIndex() < 0;
            }
        }

        public void EnsureSlots()
        {
            if (slots == null)
            {
                slots = new List<InventorySlot>(SlotCount);
            }

            while (slots.Count < SlotCount)
            {
                slots.Add(new InventorySlot());
            }

            if (slots.Count > SlotCount)
            {
                slots.RemoveRange(SlotCount, slots.Count - SlotCount);
            }

            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = new InventorySlot();
                }
            }
        }

        public InventorySlot GetSlot(int index)
        {
            EnsureSlots();
            if (index < 0 || index >= slots.Count)
            {
                return null;
            }

            return slots[index];
        }

        public int FindEmptySlotIndex()
        {
            EnsureSlots();
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].IsEmpty && !slots[i].locked)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
