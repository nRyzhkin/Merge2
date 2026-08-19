using System;
using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class InventoryController : MonoBehaviour
    {
        [SerializeField] InventoryState state = new InventoryState();
        [SerializeField] MergeItemDatabase itemDatabase;

        public static InventoryController Current { get; private set; }

        public InventoryState State => state;
        public MergeItemDatabase ItemDatabase => itemDatabase;
        public bool IsFull
        {
            get
            {
                EnsureReady();
                return state.IsFull;
            }
        }

        public event Action Changed;

        public void Configure(MergeItemDatabase database)
        {
            itemDatabase = database;
            EnsureReady();
        }

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[Inventory] Duplicate InventoryController. Keeping the first instance.");
            }
            else
            {
                Current = this;
            }

            EnsureReady();
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        public void EnsureReady()
        {
            if (state == null)
            {
                state = new InventoryState();
            }

            state.EnsureSlots();
        }

        void Start()
        {
            EnsureReady();
            SeedDevFamilyGenerators();
        }

        public bool TryAdd(int itemId, int generatorInstanceId = BoardCellState.NoGeneratorInstanceId)
        {
            EnsureReady();
            if (!IsAcceptedItem(itemId))
            {
                return false;
            }

            var index = state.FindEmptySlotIndex();
            if (index < 0)
            {
                return false;
            }

            var slot = state.GetSlot(index);
            slot.itemId = itemId;
            slot.generatorInstanceId = generatorInstanceId;
            Changed?.Invoke();
            return true;
        }

        public bool TryTake(int slotIndex, out int itemId, out int generatorInstanceId)
        {
            EnsureReady();
            itemId = BoardCellState.EmptyItemId;
            generatorInstanceId = BoardCellState.NoGeneratorInstanceId;
            var slot = state.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty || slot.locked)
            {
                return false;
            }

            itemId = slot.itemId;
            generatorInstanceId = slot.generatorInstanceId;
            slot.Clear();
            Changed?.Invoke();
            return true;
        }

        public bool IsAcceptedItem(int itemId)
        {
            if (itemId == BoardCellState.EmptyItemId)
            {
                return false;
            }

            if (itemDatabase == null || !itemDatabase.TryGetById(itemId, out var data) || data == null)
            {
                return false;
            }

            return data.Kind == MergeItemKind.Normal || data.Kind == MergeItemKind.Generator;
        }

        void SeedDevFamilyGenerators()
        {
            if (state.OccupiedCount > 0 || itemDatabase == null)
            {
                return;
            }

            var families = (MergeItemFamily[])Enum.GetValues(typeof(MergeItemFamily));
            for (var i = 0; i < families.Length; i++)
            {
                if (itemDatabase.TryGetLowestGenerator(families[i], out var item) && item != null)
                {
                    TryAdd(item.Id);
                }
            }
        }

        public void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
