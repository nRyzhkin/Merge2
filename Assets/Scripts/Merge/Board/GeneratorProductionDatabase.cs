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

        public bool TryGetGeneratorData(int generatorItemId, out GeneratorData data)
        {
            EnsureLookups();
            return _byId.TryGetValue(generatorItemId, out data) && data != null;
        }

        public bool TryRollOutputItemId(int generatorItemId, IGeneratorRandom random, out int outputItemId)
        {
            outputItemId = BoardCellState.EmptyItemId;
            if (random == null || !TryGetGeneratorData(generatorItemId, out var data))
            {
                return false;
            }

            var table = data.DropTable;
            if (table == null || table.Count == 0)
            {
                return false;
            }

            var totalWeight = 0;
            for (var i = 0; i < table.Count; i++)
            {
                var entry = table[i];
                if (entry == null || entry.Weight <= 0)
                {
                    continue;
                }

                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
            {
                return false;
            }

            var roll = random.NextInt(0, totalWeight);
            var cursor = 0;
            for (var i = 0; i < table.Count; i++)
            {
                var entry = table[i];
                if (entry == null || entry.Weight <= 0)
                {
                    continue;
                }

                cursor += entry.Weight;
                if (roll < cursor)
                {
                    outputItemId = entry.OutputItemId;
                    return outputItemId != BoardCellState.EmptyItemId;
                }
            }

            return false;
        }

        public bool TryGetOutputItemId(int generatorItemId, out int outputItemId)
        {
            outputItemId = BoardCellState.EmptyItemId;
            if (!TryGetGeneratorData(generatorItemId, out var data) || data.DropTable == null)
            {
                return false;
            }

            for (var i = 0; i < data.DropTable.Count; i++)
            {
                var entry = data.DropTable[i];
                if (entry == null || entry.Weight <= 0 || entry.OutputItemId == BoardCellState.EmptyItemId)
                {
                    continue;
                }

                outputItemId = entry.OutputItemId;
                return true;
            }

            return false;
        }

        public void Validate(MergeItemDatabase itemDatabase)
        {
            if (itemDatabase == null)
            {
                Debug.LogError("[GeneratorProduction] MergeItemDatabase is null.");
                return;
            }

            EnsureLookups();
            var seen = new HashSet<int>();
            for (var i = 0; i < generators.Count; i++)
            {
                var entry = generators[i];
                if (entry == null)
                {
                    Debug.LogError($"[GeneratorProduction] Null generator entry at index {i}.");
                    continue;
                }

                if (entry.GeneratorId == BoardCellState.EmptyItemId)
                {
                    Debug.LogError($"[GeneratorProduction] Generator entry at index {i} has empty id.");
                    continue;
                }

                if (!seen.Add(entry.GeneratorId))
                {
                    Debug.LogError($"[GeneratorProduction] Duplicate generator id {entry.GeneratorId}.");
                }

                if (entry.CapacityDrops <= 0)
                {
                    Debug.LogError($"[GeneratorProduction] Generator {entry.GeneratorId}: capacityDrops must be > 0.");
                }

                if (entry.CooldownSeconds <= 0f)
                {
                    Debug.LogError($"[GeneratorProduction] Generator {entry.GeneratorId}: cooldownSeconds must be > 0.");
                }

                if (entry.DropTable == null || entry.DropTable.Count == 0)
                {
                    Debug.LogError($"[GeneratorProduction] Generator {entry.GeneratorId}: dropTable is empty.");
                    continue;
                }

                if (!itemDatabase.TryGetById(entry.GeneratorId, out var generatorItem) ||
                    generatorItem == null ||
                    generatorItem.Kind != MergeItemKind.Generator)
                {
                    Debug.LogError($"[GeneratorProduction] Generator {entry.GeneratorId} is missing or not a generator item.");
                    continue;
                }

                for (var j = 0; j < entry.DropTable.Count; j++)
                {
                    var drop = entry.DropTable[j];
                    if (drop == null)
                    {
                        Debug.LogError($"[GeneratorProduction] Generator {entry.GeneratorId}: null drop entry at {j}.");
                        continue;
                    }

                    if (drop.Weight <= 0)
                    {
                        Debug.LogError($"[GeneratorProduction] Generator {entry.GeneratorId}: drop {j} weight must be > 0.");
                    }

                    if (!itemDatabase.TryGetById(drop.OutputItemId, out var outputItem) || outputItem == null)
                    {
                        Debug.LogError($"[GeneratorProduction] Generator {entry.GeneratorId}: output item {drop.OutputItemId} not found.");
                        continue;
                    }

                    if (outputItem.Kind != MergeItemKind.Normal)
                    {
                        Debug.LogError($"[GeneratorProduction] Generator {entry.GeneratorId}: output {outputItem.InternalKey} must be Normal.");
                    }

                    if (outputItem.Family != generatorItem.Family)
                    {
                        Debug.LogError($"[GeneratorProduction] Generator {entry.GeneratorId}: output {outputItem.InternalKey} family mismatch.");
                    }
                }
            }
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
