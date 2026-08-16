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

        public IReadOnlyList<MergeItemData> Items => items;

        void OnEnable()
        {
            RebuildLookups();
        }

        public void RebuildLookups()
        {
            _byId = new Dictionary<int, MergeItemData>(items.Count);
            _byKey = new Dictionary<string, MergeItemData>(items.Count);

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
            }
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

        void EnsureLookups()
        {
            if (_byId == null || _byKey == null)
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
