using System.IO;
using System.Text.RegularExpressions;
using SanIsland.Merge;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    public sealed class ParsedMergeSprite
    {
        public MergeItemFamily Family;
        public MergeItemKind Kind;
        public int Level;
        public Sprite Sprite;
        public string AssetPath;
        public string FileName;
        public string InternalKey;
        public string LocalizationKey;
        public string SuggestedEnglishName;
        public bool HasReliableEnglishName;
    }

    public static class MergeItemSpriteParser
    {
        static readonly Regex KindLevelRegex = new Regex(
            @"(?:^|_|-)(?<kind>[LlGg])(?<level>\d{1,2})(?=$|_|-)",
            RegexOptions.Compiled);

        public static bool TryParse(string assetPath, MergeItemFamily family, Sprite sprite, out ParsedMergeSprite parsed, out string error)
        {
            parsed = null;
            error = null;

            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(fileName))
            {
                error = $"Empty filename at '{assetPath}'.";
                return false;
            }

            var match = KindLevelRegex.Match(fileName);
            if (!match.Success)
            {
                error = $"Could not parse kind/level from '{fileName}'. Expected L# or G# token.";
                return false;
            }

            var kindToken = match.Groups["kind"].Value;
            if (!int.TryParse(match.Groups["level"].Value, out var level) || level < 1)
            {
                error = $"Invalid level in '{fileName}'.";
                return false;
            }

            var kind = char.ToUpperInvariant(kindToken[0]) == 'G'
                ? MergeItemKind.Generator
                : MergeItemKind.Normal;

            parsed = new ParsedMergeSprite
            {
                Family = family,
                Kind = kind,
                Level = level,
                Sprite = sprite,
                AssetPath = assetPath,
                FileName = fileName,
                InternalKey = MergeItemKeys.BuildInternalKey(family, kind, level),
                LocalizationKey = MergeItemKeys.BuildLocalizationKey(family, kind, level),
                HasReliableEnglishName = TryGetEnglishNameFromFileName(fileName, family, out var englishName),
                SuggestedEnglishName = null
            };
            parsed.SuggestedEnglishName = parsed.HasReliableEnglishName ? englishName : null;
            return true;
        }

        public static bool TryGetEnglishNameFromFileName(string fileNameWithoutExtension, MergeItemFamily family, out string englishName)
        {
            englishName = null;
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return false;
            }

            var name = fileNameWithoutExtension;
            var familyPrefix = family.ToString() + "_";
            if (name.StartsWith(familyPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(familyPrefix.Length);
            }

            name = KindLevelRegex.Replace(name, "_");
            name = name.Trim('_', '-', ' ');
            if (string.IsNullOrWhiteSpace(name) || Regex.IsMatch(name, @"^\d+$"))
            {
                return false;
            }

            var parts = name.Split(new[] { '_', '-' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            var words = new System.Collections.Generic.List<string>();
            for (var i = 0; i < parts.Length; i++)
            {
                var camelSplit = Regex.Replace(parts[i], "([a-z])([A-Z])", "$1 $2");
                camelSplit = Regex.Replace(camelSplit, "([A-Z]+)([A-Z][a-z])", "$1 $2");
                var camelParts = camelSplit.Split(' ');
                for (var j = 0; j < camelParts.Length; j++)
                {
                    if (!string.IsNullOrWhiteSpace(camelParts[j]))
                    {
                        words.Add(ToTitleCase(camelParts[j]));
                    }
                }
            }

            if (words.Count == 0)
            {
                return false;
            }

            englishName = string.Join(" ", words);
            return true;
        }

        static string ToTitleCase(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return word;
            }

            if (word.Length == 1)
            {
                return word.ToUpperInvariant();
            }

            return char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
        }
    }
}
