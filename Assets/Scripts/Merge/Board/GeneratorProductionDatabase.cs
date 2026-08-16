using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "GeneratorProductionDatabase", menuName = "San Island/Generator Production Database")]
    public class GeneratorProductionDatabase : ScriptableObject
    {
        [SerializeField] List<GeneratorData> generators = new List<GeneratorData>();

        Dictionary<int, GeneratorData> _byId;

        public IReadOnlyList<GeneratorData> Generators => generators;

        void OnEnable()
        {
            RebuildLookups();
        }

        public void RebuildLookups()
        {
            _byId = new Dictionary<int, GeneratorData>(generators.Count);
            for (var i = 0; i < generators.Count; i++)
            {
                var entry = generators[i];
                if (entry == null || entry.GeneratorId == BoardCellState.EmptyItemId)
                {
                    continue;
                }

                if (!_byId.ContainsKey(entry.GeneratorId))
                {
                    _byId.Add(entry.GeneratorId, entry);
                }
            }
        }

        public bool TryGetOutputItemId(int generatorItemId, out int outputItemId)
        {
            EnsureLookups();
            outputItemId = BoardCellState.EmptyItemId;
            if (!_byId.TryGetValue(generatorItemId, out var data) || data == null)
            {
                return false;
            }

            return data.TryGetDeterministicOutput(out outputItemId);
        }

        public void EnsureFromItemDatabase(MergeItemDatabase itemDatabase)
        {
            if (itemDatabase == null)
            {
                return;
            }

            var items = itemDatabase.Items;
            if (items == null)
            {
                return;
            }

            var dirty = false;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.Kind != MergeItemKind.Generator)
                {
                    continue;
                }

                if (HasGeneratorEntry(item.Id))
                {
                    continue;
                }

                var chain = itemDatabase.GetNormalChain(item.Family);
                if (chain == null || chain.Count == 0 || chain[0] == null)
                {
                    Debug.LogWarning($"[GeneratorProduction] No normal chain output for generator '{item.InternalKey}'.");
                    continue;
                }

                var entry = new GeneratorData { GeneratorId = item.Id };
                entry.PossibleOutputItems.Add(chain[0].Id);
                generators.Add(entry);
                dirty = true;
            }

            if (dirty)
            {
                RebuildLookups();
            }
        }

        bool HasGeneratorEntry(int generatorId)
        {
            EnsureLookups();
            if (_byId.ContainsKey(generatorId))
            {
                return true;
            }

            for (var i = 0; i < generators.Count; i++)
            {
                if (generators[i] != null && generators[i].GeneratorId == generatorId)
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        public void EditorReplace(List<GeneratorData> entries)
        {
            generators = entries ?? new List<GeneratorData>();
            RebuildLookups();
        }
#endif

        void EnsureLookups()
        {
            if (_byId == null)
            {
                RebuildLookups();
            }
        }
    }
}
