using System;
using UnityEngine;

namespace SanIsland.Merge
{
    [Serializable]
    public class MergeItemData
    {
        [SerializeField] int id;
        [SerializeField] string internalKey;
        [SerializeField] string localizationKey;
        [SerializeField] MergeItemFamily family;
        [SerializeField] MergeItemKind kind;
        [SerializeField] int level;
        [SerializeField] Sprite icon;
        [SerializeField] int nextItemId = MergeItemIdUtility.NoNextItemId;

        public int Id
        {
            get => id;
            set => id = value;
        }

        public string InternalKey
        {
            get => internalKey;
            set => internalKey = value;
        }

        public string LocalizationKey
        {
            get => localizationKey;
            set => localizationKey = value;
        }

        public MergeItemFamily Family
        {
            get => family;
            set => family = value;
        }

        public MergeItemKind Kind
        {
            get => kind;
            set => kind = value;
        }

        public int Level
        {
            get => level;
            set => level = value;
        }

        public Sprite Icon
        {
            get => icon;
            set => icon = value;
        }

        public int NextItemId
        {
            get => nextItemId;
            set => nextItemId = value;
        }
    }
}
