#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assigns one shared GTA sprite material to SpriteRenderers.
/// The material must not store a sprite texture — SpriteRenderer supplies it per renderer.
/// </summary>
public static class GTASpriteMaterialSetup
{
    const string MaterialPath = "Assets/Shaders/GTA_SpriteOpaque.mat";

    [MenuItem("GTA/World Lite/Sprites/Assign Shared GTA Sprite Material", false, 300)]
    static void AssignToSceneSprites()
    {
        Material material = LoadOrCreateSharedMaterial();
        if (material == null)
            return;

        SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int assigned = 0;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Assign GTA Sprite Material");

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.sharedMaterial == material)
                continue;

            Undo.RecordObject(renderer, "Assign GTA Sprite Material");
            renderer.sharedMaterial = material;
            assigned++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"GTA sprite material assigned to {assigned} SpriteRenderer(s). Total in scene: {renderers.Length}.");
    }

    [MenuItem("GTA/World Lite/Sprites/Reset Shared GTA Sprite Material", false, 301)]
    static void ResetSharedMaterial()
    {
        Material material = LoadOrCreateSharedMaterial();
        if (material == null)
            return;

        Undo.RecordObject(material, "Reset GTA Sprite Material");
        material.shader = Shader.Find("GTA/Sprite Opaque");
        material.mainTexture = null;

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", null);

        if (material.HasProperty("_AlphaTex"))
            material.SetTexture("_AlphaTex", null);

        if (material.HasProperty("_MainTex_ST"))
            material.SetTextureScale("_MainTex", Vector2.one);

        if (material.HasProperty("_MainTex_ST"))
            material.SetTextureOffset("_MainTex", Vector2.zero);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        Debug.Log("GTA sprite shared material reset: no baked sprite texture, scale/offset = 1/0.");
    }

    static Material LoadOrCreateSharedMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("GTA/Sprite Opaque");
        if (shader == null)
        {
            EditorUtility.DisplayDialog(
                "GTA Sprite Material",
                "Shader 'GTA/Sprite Opaque' not found. Reimport Assets/Shaders/GTA_SpriteOpaque.shader first.",
                "OK");
            return null;
        }

        material = new Material(shader) { name = "GTA_SpriteOpaque" };
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
#endif
