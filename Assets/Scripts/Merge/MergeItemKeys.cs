namespace SanIsland.Merge
{
    public static class MergeItemKeys
    {
        public static string BuildInternalKey(MergeItemFamily family, MergeItemKind kind, int level)
        {
            return $"{FamilyToken(family)}_{KindToken(kind)}{level:00}";
        }

        public static string BuildLocalizationKey(MergeItemFamily family, MergeItemKind kind, int level)
        {
            return $"item.{FamilyToken(family)}.{KindToken(kind)}{level:00}.name";
        }

        public static string FamilyToken(MergeItemFamily family)
        {
            return family.ToString().ToLowerInvariant();
        }

        public static string KindToken(MergeItemKind kind)
        {
            return kind == MergeItemKind.Generator ? "g" : "l";
        }
    }
}
