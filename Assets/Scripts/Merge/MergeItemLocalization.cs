using UnityEngine.Localization.Settings;

namespace SanIsland.Merge
{
    public static class MergeItemLocalization
    {
        public const string TableName = "MergeItems";

        public static string GetName(MergeItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.LocalizationKey))
            {
                return string.Empty;
            }

            return LocalizationSettings.StringDatabase.GetLocalizedString(TableName, item.LocalizationKey);
        }
    }
}
