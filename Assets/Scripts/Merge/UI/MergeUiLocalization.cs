using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace SanIsland.Merge
{
    public static class MergeUiLocalization
    {
        public const string TableName = "UI";
        public const string LevelShortKey = "ui.item.level_short";

        public static string GetLevelLabel(int level)
        {
            var localized = new LocalizedString(TableName, LevelShortKey)
            {
                Arguments = new object[] { level }
            };
            return localized.GetLocalizedString();
        }
    }
}
