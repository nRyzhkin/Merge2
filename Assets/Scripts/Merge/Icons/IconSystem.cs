using UnityEngine;

namespace SanIsland.Merge
{
    [DefaultExecutionOrder(-210)]
    [DisallowMultipleComponent]
    public class IconSystem : MonoBehaviour
    {
        public static IconSystem Current { get; private set; }

        [SerializeField] IconCatalog catalog;

        public IconCatalog Catalog => catalog;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[IconSystem] Duplicate IconSystem in the scene. Keeping the first instance.");
            }
            else
            {
                Current = this;
            }

            BindCatalog();
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
                Icons.Bind(null);
            }
        }

        public void Configure(IconCatalog iconCatalog)
        {
            catalog = iconCatalog;
            BindCatalog();
        }

        void BindCatalog()
        {
            Icons.Bind(catalog);
        }
    }
}
