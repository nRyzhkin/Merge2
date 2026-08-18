using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SanIsland.Merge.Editor
{
    public static class NunitoCyrillicSdfBuilder
    {
        const string TtfPath = "Assets/Layer Lab/GUI-LifeGame/ResourcesData/Fonts/Nunito/Nunito-ExtraBold.ttf";
        const string SdfPath = "Assets/Layer Lab/GUI-LifeGame/ResourcesData/Fonts/Nunito/Nunito-ExtraBold SDF.asset";
        const string TempPath = "Assets/Layer Lab/GUI-LifeGame/ResourcesData/Fonts/Nunito/_NunitoRebuildTemp.asset";
        const string NanumSdfPath = "Assets/Layer Lab/GUI-LifeGame/ResourcesData/Fonts/NanumSquareRoundEB SDF.asset";
        const int SamplePointSize = 100;
        const int Padding = 5;
        const int AtlasSize = 1024;

        static readonly string[] NunitoMaterials =
        {
            "Assets/Layer Lab/GUI-LifeGame/ResourcesData/Fonts/Nunito/Materials/Nunito SDF Outline White.mat",
            "Assets/Layer Lab/GUI-LifeGame/ResourcesData/Fonts/Nunito/Materials/Nunito SDF Outline Black.mat",
            "Assets/Layer Lab/GUI-LifeGame/ResourcesData/Fonts/Nunito/Materials/Nunito SDF Outline Blue.mat"
        };

        static readonly string[] LiberationPaths =
        {
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat",
            "Assets/TextMesh Pro/Fonts/LiberationSans.ttf",
            "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt"
        };

        [MenuItem("Tools/San Island/Rebuild Nunito Cyrillic SDF")]
        public static void Rebuild()
        {
            RebuildInternal();
        }

        public static void RebuildFromBatch()
        {
            try
            {
                RebuildInternal();
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogError(exception);
                EditorApplication.Exit(1);
            }
        }

        static void RebuildInternal()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            if (sourceFont == null)
            {
                throw new FileNotFoundException("Nunito ExtraBold TTF not found.", TtfPath);
            }

            FontEngine.InitializeFontEngine();
            ShaderUtilities.GetShaderPropertyIDs();
            var created = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                SamplePointSize,
                Padding,
                GlyphRenderMode.SDFAA,
                AtlasSize,
                AtlasSize,
                AtlasPopulationMode.Dynamic,
                false);
            if (created == null)
            {
                throw new System.InvalidOperationException("Failed to create Nunito TMP font asset.");
            }

            var characters = BuildCyrillicSet();
            if (!created.TryAddCharacters(characters, out var missing) && !string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning("[NunitoCyrillicSdf] Missing characters: " + missing);
            }

            created.atlasPopulationMode = AtlasPopulationMode.Static;
            created.name = "Nunito-ExtraBold SDF";
            if (created.material != null)
            {
                created.material.name = "Nunito Cyrillic SDF Material";
                created.material.SetFloat(ShaderUtilities.ID_GradientScale, Padding + 1);
                created.material.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasSize);
                created.material.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasSize);
            }

            if (created.atlasTexture != null)
            {
                created.atlasTexture.name = "Nunito Cyrillic SDF Atlas";
            }

            created.ReadFontAssetDefinition();

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TempPath) != null)
            {
                AssetDatabase.DeleteAsset(TempPath);
            }

            var originalMeta = File.ReadAllText(SdfPath + ".meta");
            AssetDatabase.CreateAsset(created, TempPath);
            if (created.atlasTexture != null)
            {
                AssetDatabase.AddObjectToAsset(created.atlasTexture, created);
            }

            if (created.material != null)
            {
                AssetDatabase.AddObjectToAsset(created.material, created);
            }

            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssets();

            AssetDatabase.DeleteAsset(SdfPath);
            var moveError = AssetDatabase.MoveAsset(TempPath, SdfPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                throw new System.InvalidOperationException(moveError);
            }

            File.WriteAllText(SdfPath + ".meta", originalMeta);
            AssetDatabase.ImportAsset(SdfPath, ImportAssetOptions.ForceUpdate);

            var rebuilt = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath);
            if (rebuilt == null)
            {
                throw new System.InvalidOperationException("Rebuilt Nunito SDF failed to import.");
            }

            BindNunitoMaterials(rebuilt);
            SetDefaultTmpFont();
            DeleteLiberationFonts();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NunitoCyrillicSdf] Rebuilt at " + SamplePointSize + "pt, padding " + Padding + ", atlas " + AtlasSize +
                      ". Glyphs: " + rebuilt.characterTable.Count);
        }

        static void BindNunitoMaterials(TMP_FontAsset fontAsset)
        {
            for (var i = 0; i < NunitoMaterials.Length; i++)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(NunitoMaterials[i]);
                if (material == null)
                {
                    continue;
                }

                material.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
                material.SetFloat(ShaderUtilities.ID_GradientScale, Padding + 1);
                material.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasSize);
                material.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasSize);
                EditorUtility.SetDirty(material);
            }
        }

        static void SetDefaultTmpFont()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            var nanum = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NanumSdfPath);
            if (settings == null || nanum == null)
            {
                return;
            }

            TMP_Settings.defaultFontAsset = nanum;
            EditorUtility.SetDirty(settings);
        }

        static void DeleteLiberationFonts()
        {
            for (var i = 0; i < LiberationPaths.Length; i++)
            {
                if (File.Exists(LiberationPaths[i]) || Directory.Exists(LiberationPaths[i]))
                {
                    AssetDatabase.DeleteAsset(LiberationPaths[i]);
                }
            }
        }

        static string BuildCyrillicSet()
        {
            var text = new StringBuilder(128);
            text.Append(' ');
            text.Append((char)171);
            text.Append((char)187);
            text.Append((char)1025);
            for (var code = 1040; code <= 1103; code++)
            {
                text.Append((char)code);
            }

            text.Append((char)1105);
            text.Append((char)8211);
            text.Append((char)8212);
            text.Append((char)8220);
            text.Append((char)8221);
            text.Append((char)8222);
            text.Append((char)8230);
            text.Append((char)8381);
            text.Append((char)8470);
            return text.ToString();
        }
    }
}
