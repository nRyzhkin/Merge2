using System.Collections.Generic;
using SanIsland.Merge;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace SanIsland.Merge.Editor
{
    public static class MergeItemLocalizationImporter
    {
        public const string SettingsPath = "Assets/Localization/Localization Settings.asset";
        public const string LocalesFolder = "Assets/Localization/Locales";
        public const string TablesFolder = "Assets/Localization/Tables";

        static readonly string[] LocaleCodes =
        {
            "en", "ru", "de", "es", "fr", "pt", "tr"
        };

        public static void EnsureProjectSetup()
        {
            EnsureAddressables();
            EnsureFolder("Assets/Localization");
            EnsureFolder(LocalesFolder);
            EnsureFolder(TablesFolder);
            EnsureLocalizationSettings();
            EnsureLocales();
            EnsureStringTableCollection();
        }

        public static void UpsertEnglishKeys(IReadOnlyList<ParsedMergeSprite> parsedSprites)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(MergeItemLocalization.TableName);
            if (collection == null)
            {
                Debug.LogError($"[MergeItemImporter] String Table '{MergeItemLocalization.TableName}' was not created. Check Localization Settings.");
                return;
            }

            var english = collection.GetTable("en") as StringTable;
            if (english == null)
            {
                Debug.LogError("[MergeItemImporter] English (en) String Table is missing from MergeItems.");
                return;
            }

            var dirty = false;
            for (var i = 0; i < parsedSprites.Count; i++)
            {
                var parsed = parsedSprites[i];
                var key = parsed.LocalizationKey;
                var existing = collection.SharedData.GetEntry(key);
                if (existing != null)
                {
                    continue;
                }

                if (!parsed.HasReliableEnglishName)
                {
                    Debug.LogWarning($"[MergeItemImporter] Created localization key '{key}' with empty English value. Filename '{parsed.FileName}' has no reliable display name.");
                    english.AddEntry(key, string.Empty);
                }
                else
                {
                    english.AddEntry(key, parsed.SuggestedEnglishName);
                }

                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(english);
                EditorUtility.SetDirty(english.SharedData);
            }
        }

        public static void EnsureUiStringTable()
        {
            EnsureProjectSetup();
            var collection = LocalizationEditorSettings.GetStringTableCollection(MergeUiLocalization.TableName);
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(MergeUiLocalization.TableName, TablesFolder);
            }

            if (collection == null)
            {
                Debug.LogError("[MergeItemImporter] Failed to create UI String Table Collection.");
                return;
            }

            var locales = LocalizationEditorSettings.GetLocales();
            for (var i = 0; i < locales.Count; i++)
            {
                var locale = locales[i];
                if (collection.GetTable(locale.Identifier) == null)
                {
                    collection.AddNewTable(locale.Identifier);
                }
            }

            UpsertUiKey(collection, MergeUiLocalization.LevelShortKey, "en", "Lvl {0}", smart: true);
            UpsertUiKey(collection, MergeUiLocalization.LevelShortKey, "ru", "Ур. {0}", smart: true);
            UpsertUiKey(collection, MergeUiLocalization.LevelShortKey, "de", "St. {0}", smart: true);
            UpsertUiKey(collection, MergeUiLocalization.LevelShortKey, "es", "Niv. {0}", smart: true);
            UpsertUiKey(collection, MergeUiLocalization.LevelShortKey, "fr", "Niv. {0}", smart: true);
            UpsertUiKey(collection, MergeUiLocalization.LevelShortKey, "pt", "Nv. {0}", smart: true);
            UpsertUiKey(collection, MergeUiLocalization.LevelShortKey, "tr", "Sv. {0}", smart: true);

            UpsertUiKey(collection, MergeUiLocalization.LevelWordKey, "en", "Level");
            UpsertUiKey(collection, MergeUiLocalization.LevelWordKey, "ru", "Уровень");
            UpsertUiKey(collection, MergeUiLocalization.LevelWordKey, "de", "Stufe");
            UpsertUiKey(collection, MergeUiLocalization.LevelWordKey, "es", "Nivel");
            UpsertUiKey(collection, MergeUiLocalization.LevelWordKey, "fr", "Niveau");
            UpsertUiKey(collection, MergeUiLocalization.LevelWordKey, "pt", "Nível");
            UpsertUiKey(collection, MergeUiLocalization.LevelWordKey, "tr", "Seviye");

            UpsertUiKey(collection, MergeUiLocalization.BoardFullKey, "en", "Board is full");
            UpsertUiKey(collection, MergeUiLocalization.BoardFullKey, "ru", "Поле заполнено");
            UpsertUiKey(collection, MergeUiLocalization.BoardFullKey, "de", "Spielfeld ist voll");
            UpsertUiKey(collection, MergeUiLocalization.BoardFullKey, "es", "El tablero está lleno");
            UpsertUiKey(collection, MergeUiLocalization.BoardFullKey, "fr", "Plateau plein");
            UpsertUiKey(collection, MergeUiLocalization.BoardFullKey, "pt", "Tabuleiro cheio");
            UpsertUiKey(collection, MergeUiLocalization.BoardFullKey, "tr", "Tahta dolu");

            UpsertUiKey(collection, MergeUiLocalization.GeneratorRechargingKey, "en", "Generator is recharging");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorRechargingKey, "ru", "Генератор заряжается");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorRechargingKey, "de", "Generator lädt auf");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorRechargingKey, "es", "El generador se está recargando");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorRechargingKey, "fr", "Le générateur se recharge");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorRechargingKey, "pt", "Gerador recarregando");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorRechargingKey, "tr", "Jeneratör şarj oluyor");

            UpsertUiKey(collection, MergeUiLocalization.NotEnoughEnergyKey, "en", "Not enough energy");
            UpsertUiKey(collection, MergeUiLocalization.NotEnoughEnergyKey, "ru", "Недостаточно энергии");
            UpsertUiKey(collection, MergeUiLocalization.NotEnoughEnergyKey, "de", "Nicht genug Energie");
            UpsertUiKey(collection, MergeUiLocalization.NotEnoughEnergyKey, "es", "Energía insuficiente");
            UpsertUiKey(collection, MergeUiLocalization.NotEnoughEnergyKey, "fr", "Pas assez d'énergie");
            UpsertUiKey(collection, MergeUiLocalization.NotEnoughEnergyKey, "pt", "Energia insuficiente");
            UpsertUiKey(collection, MergeUiLocalization.NotEnoughEnergyKey, "tr", "Yeterli enerji yok");

            UpsertUiKey(collection, MergeUiLocalization.SellActionKey, "en", "Sell");
            UpsertUiKey(collection, MergeUiLocalization.SellActionKey, "ru", "Продать");
            UpsertUiKey(collection, MergeUiLocalization.SellActionKey, "de", "Verkaufen");
            UpsertUiKey(collection, MergeUiLocalization.SellActionKey, "es", "Vender");
            UpsertUiKey(collection, MergeUiLocalization.SellActionKey, "fr", "Vendre");
            UpsertUiKey(collection, MergeUiLocalization.SellActionKey, "pt", "Vender");
            UpsertUiKey(collection, MergeUiLocalization.SellActionKey, "tr", "Sat");

            UpsertUiKey(collection, MergeUiLocalization.UndoActionKey, "en", "Undo");
            UpsertUiKey(collection, MergeUiLocalization.UndoActionKey, "ru", "Отмена");
            UpsertUiKey(collection, MergeUiLocalization.UndoActionKey, "de", "Rückgängig");
            UpsertUiKey(collection, MergeUiLocalization.UndoActionKey, "es", "Deshacer");
            UpsertUiKey(collection, MergeUiLocalization.UndoActionKey, "fr", "Annuler");
            UpsertUiKey(collection, MergeUiLocalization.UndoActionKey, "pt", "Desfazer");
            UpsertUiKey(collection, MergeUiLocalization.UndoActionKey, "tr", "Geri Al");

            UpsertUiKey(collection, MergeUiLocalization.UndoNoSpaceKey, "en", "No space to restore item");
            UpsertUiKey(collection, MergeUiLocalization.UndoNoSpaceKey, "ru", "Нет места, чтобы вернуть предмет");
            UpsertUiKey(collection, MergeUiLocalization.UndoNoSpaceKey, "de", "Kein Platz zum Zurücklegen");
            UpsertUiKey(collection, MergeUiLocalization.UndoNoSpaceKey, "es", "No hay espacio para restaurar");
            UpsertUiKey(collection, MergeUiLocalization.UndoNoSpaceKey, "fr", "Pas de place pour restaurer");
            UpsertUiKey(collection, MergeUiLocalization.UndoNoSpaceKey, "pt", "Sem espaço para restaurar");
            UpsertUiKey(collection, MergeUiLocalization.UndoNoSpaceKey, "tr", "Geri koymak için yer yok");

            UpsertUiKey(collection, MergeUiLocalization.OrderItemsMissingKey, "en", "Required items are missing");
            UpsertUiKey(collection, MergeUiLocalization.OrderItemsMissingKey, "ru", "Нужные предметы отсутствуют");
            UpsertUiKey(collection, MergeUiLocalization.OrderItemsMissingKey, "de", "Benötigte Gegenstände fehlen");
            UpsertUiKey(collection, MergeUiLocalization.OrderItemsMissingKey, "es", "Faltan los objetos requeridos");
            UpsertUiKey(collection, MergeUiLocalization.OrderItemsMissingKey, "fr", "Objets requis manquants");
            UpsertUiKey(collection, MergeUiLocalization.OrderItemsMissingKey, "pt", "Itens necessários ausentes");
            UpsertUiKey(collection, MergeUiLocalization.OrderItemsMissingKey, "tr", "Gerekli eşyalar eksik");

            UpsertUiKey(collection, MergeUiLocalization.TimeMinutesSecondsKey, "en", "{0}m {1}s");
            UpsertUiKey(collection, MergeUiLocalization.TimeMinutesSecondsKey, "ru", "{0}м {1}с");
            UpsertUiKey(collection, MergeUiLocalization.TimeMinutesSecondsKey, "de", "{0} Min. {1} Sek.");
            UpsertUiKey(collection, MergeUiLocalization.TimeMinutesSecondsKey, "es", "{0} min {1} s");
            UpsertUiKey(collection, MergeUiLocalization.TimeMinutesSecondsKey, "fr", "{0} min {1} s");
            UpsertUiKey(collection, MergeUiLocalization.TimeMinutesSecondsKey, "pt", "{0} min {1} s");
            UpsertUiKey(collection, MergeUiLocalization.TimeMinutesSecondsKey, "tr", "{0} dk {1} sn");

            UpsertUiKey(collection, MergeUiLocalization.TimeSecondsKey, "en", "{0}s");
            UpsertUiKey(collection, MergeUiLocalization.TimeSecondsKey, "ru", "{0}с");
            UpsertUiKey(collection, MergeUiLocalization.TimeSecondsKey, "de", "{0} Sek.");
            UpsertUiKey(collection, MergeUiLocalization.TimeSecondsKey, "es", "{0} s");
            UpsertUiKey(collection, MergeUiLocalization.TimeSecondsKey, "fr", "{0} s");
            UpsertUiKey(collection, MergeUiLocalization.TimeSecondsKey, "pt", "{0} s");
            UpsertUiKey(collection, MergeUiLocalization.TimeSecondsKey, "tr", "{0} sn");

            UpsertUiKey(collection, MergeUiLocalization.GeneratorDescriptionKey, "en", "This generator contains items. It recharges after running out.");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorDescriptionKey, "ru", "В этом генераторе есть предметы. Он перезаряжается, когда они заканчиваются.");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorDescriptionKey, "de", "Dieser Generator enthält Gegenstände. Er lädt sich auf, wenn sie aufgebraucht sind.");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorDescriptionKey, "es", "Este generador contiene objetos. Se recarga cuando se agotan.");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorDescriptionKey, "fr", "Ce générateur contient des objets. Il se recharge une fois vide.");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorDescriptionKey, "pt", "Este gerador contém itens. Ele recarrega quando acaba.");
            UpsertUiKey(collection, MergeUiLocalization.GeneratorDescriptionKey, "tr", "Bu jeneratör eşya içerir. Bitince yeniden şarj olur.");
            AssetDatabase.SaveAssets();
        }

        static void UpsertUiKey(StringTableCollection collection, string key, string localeCode, string value, bool smart = false)
        {
            var table = collection.GetTable(localeCode) as StringTable;
            if (table == null)
            {
                Debug.LogWarning($"[MergeItemImporter] UI String Table for '{localeCode}' is missing.");
                return;
            }

            var shared = collection.SharedData.GetEntry(key);
            if (shared == null)
            {
                var entry = table.AddEntry(key, value);
                if (entry != null)
                {
                    entry.IsSmart = smart;
                }
            }
            else
            {
                var entry = table.GetEntry(shared.Id);
                if (entry == null)
                {
                    entry = table.AddEntry(shared.Id, value);
                }
                else if (string.IsNullOrEmpty(entry.Value))
                {
                    entry.Value = value;
                }

                if (entry != null)
                {
                    entry.IsSmart = smart;
                }
            }

            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(table.SharedData);
        }

        static void EnsureAddressables()
        {
            AddressableAssetSettingsDefaultObject.GetSettings(true);
        }

        static void EnsureLocalizationSettings()
        {
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null)
            {
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<LocalizationSettings>();
                existing.name = "Localization Settings";
                AssetDatabase.CreateAsset(existing, SettingsPath);
            }

            LocalizationEditorSettings.ActiveLocalizationSettings = existing;
            EditorUtility.SetDirty(existing);
        }

        static void EnsureLocales()
        {
            for (var i = 0; i < LocaleCodes.Length; i++)
            {
                var code = LocaleCodes[i];
                if (LocalizationEditorSettings.GetLocale(code) != null)
                {
                    continue;
                }

                var locale = Locale.CreateLocale(new LocaleIdentifier(code));
                var path = $"{LocalesFolder}/{code}.asset";
                AssetDatabase.CreateAsset(locale, path);
                LocalizationEditorSettings.AddLocale(locale);
            }
        }

        static void EnsureStringTableCollection()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(MergeItemLocalization.TableName);
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(MergeItemLocalization.TableName, TablesFolder);
            }

            if (collection == null)
            {
                Debug.LogError("[MergeItemImporter] Failed to create MergeItems String Table Collection.");
                return;
            }

            var locales = LocalizationEditorSettings.GetLocales();
            for (var i = 0; i < locales.Count; i++)
            {
                var locale = locales[i];
                if (collection.GetTable(locale.Identifier) == null)
                {
                    collection.AddNewTable(locale.Identifier);
                }
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                var grandParent = System.IO.Path.GetDirectoryName(parent)?.Replace('\\', '/');
                var parentName = System.IO.Path.GetFileName(parent);
                AssetDatabase.CreateFolder(grandParent, parentName);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
