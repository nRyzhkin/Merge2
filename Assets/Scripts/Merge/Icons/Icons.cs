using UnityEngine;

namespace SanIsland.Merge
{
    public static class Icons
    {
        static IconCatalog _catalog;

        public static IconCatalog Catalog => _catalog;

        public static void Bind(IconCatalog catalog)
        {
            _catalog = catalog;
            if (_catalog != null)
            {
                _catalog.RebuildLookups();
            }
        }

        public static Sprite Get(string token)
        {
            TryGet(token, out var sprite);
            return sprite;
        }

        public static bool TryGet(string token, out Sprite sprite)
        {
            sprite = null;
            return _catalog != null && _catalog.TryGet(token, out sprite);
        }
    }

    public static class IconTokenExtensions
    {
        public static Sprite GetSprite(this string token)
        {
            return Icons.Get(token);
        }
    }
}
