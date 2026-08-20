#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps GTA shader_feature keywords in sync with assigned textures.
/// </summary>
public sealed class GTAShaderGUI : ShaderGUI
{
    static readonly (string property, string keyword)[] TextureKeywords =
    {
        ("_BumpMap", "_NORMALMAP"),
        ("_MetallicGlossMap", "_METALLICMAP"),
        ("_OcclusionMap", "_OCCLUSIONMAP"),
        ("_EmissionMap", "_EMISSION"),
        ("_DetailAlbedoMap", "_DETAIL_ALBEDO"),
        ("_DetailNormalMap", "_DETAIL_NORMAL"),
        ("_MacroMap", "_MACRO_MAP"),
        ("_DirtMap", "_DIRT_MAP"),
        ("_OpacityMap", "_OPACITYMAP"),
        ("_DirtAlbedoMap", "_DIRT_ALBEDO"),
    };

    static readonly string[] RenderFaceLabels = { "Front", "Back", "Both" };
    static readonly int[] RenderFaceCullValues = { 2, 1, 0 };

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        foreach (Object target in materialEditor.targets)
        {
            if (target is Material material)
                SyncKeywords(material);
        }

        base.OnGUI(materialEditor, properties);
        DrawRenderFaces(materialEditor, properties);

        foreach (Object target in materialEditor.targets)
        {
            if (target is Material material)
                SyncKeywords(material);
        }
    }

    static void DrawRenderFaces(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty cullProp = FindProperty("_Cull", properties, false);
        if (cullProp == null)
            return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);

        int currentIndex = CullValueToIndex(cullProp.floatValue);
        EditorGUI.BeginChangeCheck();
        int selectedIndex = EditorGUILayout.Popup("Render Faces", currentIndex, RenderFaceLabels);
        if (EditorGUI.EndChangeCheck())
        {
            float cullValue = RenderFaceCullValues[selectedIndex];
            cullProp.floatValue = cullValue;

            foreach (Object target in materialEditor.targets)
            {
                if (target is not Material material || !material.HasProperty("_Cull"))
                    continue;

                material.SetFloat("_Cull", cullValue);
                material.doubleSidedGI = cullValue == 0f;
            }
        }
    }

    static int CullValueToIndex(float cullValue)
    {
        if (Mathf.Approximately(cullValue, 1f))
            return 1;
        if (Mathf.Approximately(cullValue, 0f))
            return 2;
        return 0;
    }

    public static void SyncKeywords(Material material)
    {
        if (material == null || material.shader == null)
            return;

        foreach (var (property, keyword) in TextureKeywords)
        {
            if (!material.HasProperty(property))
                continue;

            bool enabled = material.GetTexture(property) != null;
            if (keyword == "_EMISSION" && !enabled && material.HasProperty("_EmissionColor"))
            {
                Color e = material.GetColor("_EmissionColor");
                enabled = e.maxColorComponent > 0.01f;
            }

            CoreUtils.SetKeyword(material, keyword, enabled);

            string toggle = ToggleForKeyword(keyword);
            if (!string.IsNullOrEmpty(toggle) && material.HasProperty(toggle))
                material.SetFloat(toggle, enabled ? 1f : 0f);
        }
    }

    static string ToggleForKeyword(string keyword) => keyword switch
    {
        "_NORMALMAP" => "_UseNormalMap",
        "_METALLICMAP" => "_UseMetallicMap",
        "_OCCLUSIONMAP" => "_UseOcclusionMap",
        "_EMISSION" => "_UseEmission",
        "_DETAIL_ALBEDO" => "_UseDetailAlbedo",
        "_DETAIL_NORMAL" => "_UseDetailNormal",
        "_MACRO_MAP" => "_UseMacroMap",
        "_OPACITYMAP" => "_UseOpacityMap",
        "_DIRT_MAP" => "_UseDirtMap",
        "_DIRT_ALBEDO" => "_UseDirtAlbedo",
        _ => null
    };
}
#endif
