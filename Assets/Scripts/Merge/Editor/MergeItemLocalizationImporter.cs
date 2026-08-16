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

            UpsertUiKey(collection, "en", "Lvl {0}");
            UpsertUiKey(collection, "ru", "Ур. {0}");
            UpsertUiKey(collection, "de", "St. {0}");
            UpsertUiKey(collection, "es", "Niv. {0}");
            UpsertUiKey(collection, "fr", "Niv. {0}");
            UpsertUiKey(collection, "pt", "Nv. {0}");
            UpsertUiKey(collection, "tr", "Sv. {0}");
            AssetDatabase.SaveAssets();
        }

        static void UpsertUiKey(StringTableCollection collection, string localeCode, string value)
        {
            var table = collection.GetTable(localeCode) as StringTable;
            if (table == null)
            {
                Debug.LogWarning($"[MergeItemImporter] UI String Table for '{localeCode}' is missing.");
                return;
            }

            var key = MergeUiLocalization.LevelShortKey;
            var shared = collection.SharedData.GetEntry(key);
            if (shared == null)
            {
                var entry = table.AddEntry(key, value);
                if (entry != null)
                {
                    entry.IsSmart = true;
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
                    entry.IsSmart = true;
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
