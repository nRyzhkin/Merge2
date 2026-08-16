using System.Collections.Generic;
using System.IO;
using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    public static class MergeItemSpriteScanner
    {
        public const string SpritesRoot = "Assets/Sprites";

        public static List<ParsedMergeSprite> Scan(out int warningCount, out int errorCount)
        {
            warningCount = 0;
            errorCount = 0;
            var results = new List<ParsedMergeSprite>();

            if (!AssetDatabase.IsValidFolder(SpritesRoot))
            {
                Debug.LogError($"[MergeItemImporter] Sprite root folder not found: {SpritesRoot}");
                errorCount++;
                return results;
            }

            var familyFolders = Directory.GetDirectories(SpritesRoot);
            for (var folderIndex = 0; folderIndex < familyFolders.Length; folderIndex++)
            {
                var folderPath = familyFolders[folderIndex].Replace('\\', '/');

                var folderName = Path.GetFileName(folderPath);
                if (folderName.StartsWith("."))
                {
                    continue;
                }

                if (!TryParseFamily(folderName, out var family))
                {
                    Debug.LogWarning($"[MergeItemImporter] Unknown family folder '{folderName}' at '{folderPath}'. Supported: {string.Join(", ", System.Enum.GetNames(typeof(MergeItemFamily)))}.");
                    warningCount++;
                    continue;
                }

                var spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
                var seenPaths = new HashSet<string>();
                foreach (var spriteGuid in spriteGuids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(spriteGuid);
                    if (!seenPaths.Add(assetPath))
                    {
                        continue;
                    }

                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite == null)
                    {
                        Debug.LogError($"[MergeItemImporter] Failed to load Sprite at '{assetPath}'.");
                        errorCount++;
                        continue;
                    }

                    if (!MergeItemSpriteParser.TryParse(assetPath, family, sprite, out var parsed, out var error))
                    {
                        Debug.LogError($"[MergeItemImporter] Invalid sprite filename '{assetPath}': {error}");
                        errorCount++;
                        continue;
                    }

                    results.Add(parsed);
                }
            }

            return results;
        }

        static bool TryParseFamily(string folderName, out MergeItemFamily family)
        {
            return System.Enum.TryParse(folderName, true, out family) && System.Enum.IsDefined(typeof(MergeItemFamily), family);
        }
    }
}
