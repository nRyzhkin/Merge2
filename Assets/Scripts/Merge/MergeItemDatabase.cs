using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "MergeItemDatabase", menuName = "San Island/Merge Item Database")]
    public class MergeItemDatabase : ScriptableObject
    {
        [SerializeField] List<MergeItemData> items = new List<MergeItemData>();

        Dictionary<int, MergeItemData> _byId;
        Dictionary<string, MergeItemData> _byKey;
        readonly Dictionary<(MergeItemFamily, MergeItemKind), List<MergeItemData>> _chains = new Dictionary<(MergeItemFamily, MergeItemKind), List<MergeItemData>>();
        readonly Dictionary<MergeItemFamily, MergeItemData> _lowestGenerator = new Dictionary<MergeItemFamily, MergeItemData>();

        public IReadOnlyList<MergeItemData> Items => items;

        void OnEnable()
        {
            RebuildLookups();
        }

        public void RebuildLookups()
        {
            _byId = new Dictionary<int, MergeItemData>(items.Count);
            _byKey = new Dictionary<string, MergeItemData>(items.Count);
            _chains.Clear();
            _lowestGenerator.Clear();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (!_byId.ContainsKey(item.Id))
                {
                    _byId.Add(item.Id, item);
                }

                if (!string.IsNullOrEmpty(item.InternalKey) && !_byKey.ContainsKey(item.InternalKey))
                {
                    _byKey.Add(item.InternalKey, item);
                }

                var chainKey = (item.Family, item.Kind);
                if (!_chains.TryGetValue(chainKey, out var chain))
                {
                    chain = new List<MergeItemData>();
                    _chains.Add(chainKey, chain);
                }

                chain.Add(item);

                if (item.Kind == MergeItemKind.Generator)
                {
                    if (!_lowestGenerator.TryGetValue(item.Family, out var current) || item.Level < current.Level)
                    {
                        _lowestGenerator[item.Family] = item;
                    }
                }
            }

            foreach (var pair in _chains)
            {
                pair.Value.Sort(CompareByLevel);
            }
        }

        static int CompareByLevel(MergeItemData a, MergeItemData b)
        {
            return a.Level.CompareTo(b.Level);
        }

        public MergeItemData GetById(int id)
        {
            if (TryGetById(id, out var item))
            {
                return item;
            }

            throw new KeyNotFoundException($"Merge item with id {id} was not found.");
        }

        public MergeItemData GetByKey(string key)
        {
            if (TryGetByKey(key, out var item))
            {
                return item;
            }

            throw new KeyNotFoundException($"Merge item with key '{key}' was not found.");
        }

        public bool TryGetById(int id, out MergeItemData item)
        {
            EnsureLookups();
            return _byId.TryGetValue(id, out item);
        }

        public bool TryGetByKey(string key, out MergeItemData item)
        {
            EnsureLookups();
            if (string.IsNullOrEmpty(key))
            {
                item = null;
                return false;
            }

            return _byKey.TryGetValue(key, out item);
        }

        public IReadOnlyList<MergeItemData> GetNormalChain(MergeItemFamily family)
        {
            return GetChain(family, MergeItemKind.Normal);
        }

        public IReadOnlyList<MergeItemData> GetChain(MergeItemFamily family, MergeItemKind kind)
        {
            EnsureLookups();
            if (_chains.TryGetValue((family, kind), out var chain))
            {
                return chain;
            }

            return System.Array.Empty<MergeItemData>();
        }

        public MergeItemData GetLowestGenerator(MergeItemFamily family)
        {
            if (TryGetLowestGenerator(family, out var item))
            {
                return item;
            }

            return null;
        }

        public bool TryGetLowestGenerator(MergeItemFamily family, out MergeItemData item)
        {
            EnsureLookups();
            return _lowestGenerator.TryGetValue(family, out item);
        }

        void EnsureLookups()
        {
            if (_byId == null || _byKey == null || _chains.Count == 0)
            {
                RebuildLookups();
            }
        }

#if UNITY_EDITOR
        public void EditorReplaceItems(List<MergeItemData> newItems)
        {
            items = newItems ?? new List<MergeItemData>();
            RebuildLookups();
        }
#endif
    }
}
