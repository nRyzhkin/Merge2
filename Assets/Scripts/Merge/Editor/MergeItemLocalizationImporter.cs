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
