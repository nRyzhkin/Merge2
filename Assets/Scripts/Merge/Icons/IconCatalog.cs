using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [Serializable]
    public class IconEntry
    {
        public string token;
        public Sprite sprite;
    }

    [CreateAssetMenu(fileName = "IconCatalog", menuName = "San Island/Icon Catalog")]
    public class IconCatalog : ScriptableObject
    {
        [SerializeField] List<IconEntry> entries = new List<IconEntry>();

        Dictionary<string, Sprite> _byToken;

        public IReadOnlyList<IconEntry> Entries => entries;

        void OnEnable()
        {
            RebuildLookups();
        }

        public void RebuildLookups()
        {
            var count = entries != null ? entries.Count : 0;
            _byToken = new Dictionary<string, Sprite>(count);
            if (entries == null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.token) || entry.sprite == null)
                {
                    continue;
                }

                if (!_byToken.ContainsKey(entry.token))
                {
                    _byToken.Add(entry.token, entry.sprite);
                }
            }
        }

        public bool TryGet(string token, out Sprite sprite)
        {
            EnsureLookups();
            sprite = null;
            if (string.IsNullOrEmpty(token) || _byToken == null)
            {
                return false;
            }

            return _byToken.TryGetValue(token, out sprite) && sprite != null;
        }

        public Sprite Get(string token)
        {
            TryGet(token, out var sprite);
            return sprite;
        }

        void EnsureLookups()
        {
            if (_byToken == null)
            {
                RebuildLookups();
            }
        }
    }
}
