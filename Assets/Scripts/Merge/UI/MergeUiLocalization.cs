using UnityEngine.Localization;

namespace SanIsland.Merge
{
    public static class MergeUiLocalization
    {
        public const string TableName = "UI";
        public const string LevelShortKey = "ui.item.level_short";
        public const string BoardFullKey = "ui.message.board_full";
        public const string GeneratorRechargingKey = "ui.message.generator_recharging";
        public const string NotEnoughEnergyKey = "ui.message.not_enough_energy";
        public const string SellActionKey = "ui.action.sell";
        public const string UndoActionKey = "ui.action.undo";
        public const string UndoNoSpaceKey = "ui.message.undo_no_space";
        public const string OrderItemsMissingKey = "ui.message.order_items_missing";

        public static string GetLevelLabel(int level)
        {
            var localized = new LocalizedString(TableName, LevelShortKey)
            {
                Arguments = new object[] { level }
            };
            return localized.GetLocalizedString();
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            return new LocalizedString(TableName, key).GetLocalizedString();
        }
    }
}
