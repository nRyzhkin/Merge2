#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click bake of 256x256 water textures for GTA/WaterLite.
/// </summary>
public static class WaterLiteTextureBaker
{
    const int Size = 256;
    const string OutputDir = GTAWorldLitePaths.WaterTexturesLite;

    [MenuItem("GTA/World Lite/Bake Textures (256x256)")]
    public static void BakeAll()
    {
        Directory.CreateDirectory(OutputDir);

        BakeNormal($"{OutputDir}/T_WaterNormal.png");
        BakeFoamNoise($"{OutputDir}/T_FoamNoise.png");
        BakeCaustic($"{OutputDir}/T_Caustic.png");

        AssetDatabase.Refresh();
        ConfigureImport($"{OutputDir}/T_WaterNormal.png", true, false);
        ConfigureImport($"{OutputDir}/T_FoamNoise.png", false, true);
        ConfigureImport($"{OutputDir}/T_Caustic.png", false, true);

        Debug.Log($"WaterLite: baked 3 textures in {OutputDir}");
    }

    static void ConfigureImport(string path, bool normalMap, bool repeat)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normalMap;
        importer.mipmapEnabled = true;
        importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = Size;
        importer.SaveAndReimport();
    }

    static float Hash21(float x, float y)
    {
        Vector3 p = new Vector3(x, y, x) * 0.1031f;
        p = new Vector3(
            Frac(p.x * (p.y + 33.33f)),
            Frac(p.y * (p.z + 33.33f)),
            Frac(p.z * (p.x + 33.33f)));
        return Frac((p.x + p.y) * p.z);
    }

    static float Noise2D(float x, float y)
    {
        float ix = Mathf.Floor(x);
        float iy = Mathf.Floor(y);
        float fx = Frac(x);
        float fy = Frac(y);
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash21(ix, iy);
        float b = Hash21(ix + 1f, iy);
        float c = Hash21(ix, iy + 1f);
        float d = Hash21(ix + 1f, iy + 1f);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Frac(float v) => v - Mathf.Floor(v);

    static void BakeNormal(string path)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size;
                float v = y / (float)Size;
                float h = Noise2D(u * 10f, v * 10f) * 0.6f + Noise2D(u * 22f + 3.7f, v * 22f - 1.2f) * 0.4f;
                float hx = Noise2D((u + 1f / Size) * 10f, v * 10f) - Noise2D((u - 1f / Size) * 10f, v * 10f);
                float hy = Noise2D(u * 10f, (v + 1f / Size) * 10f) - Noise2D(u * 10f, (v - 1f / Size) * 10f);
                var n = new Vector3(-hx * 2.5f, 1f, -hy * 2.5f).normalized;
                tex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
            }
        }
        tex.Apply(true);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void BakeFoamNoise(string path)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size;
                float v = y / (float)Size;
                float n = Noise2D(u * 8f, v * 8f) * 0.55f + Noise2D(u * 19f, v * 19f) * 0.45f;
                tex.SetPixel(x, y, new Color(n, n, n, 1f));
            }
        }
        tex.Apply(true);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void BakeCaustic(string path)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size * 6f;
                float v = y / (float)Size * 6f;
                float c = 0f;
                for (int i = 0; i < 4; i++)
                {
                    var cell = new Vector2(
                        Noise2D(u + i * 1.7f, v + i * 2.3f),
                        Noise2D(u + i * 3.1f + 5f, v + i * 1.9f + 2f));
                    float d = Vector2.Distance(new Vector2(u, v), cell);
                    c = Mathf.Max(c, 1f - Mathf.Clamp01(d * 3.5f));
                }
                tex.SetPixel(x, y, new Color(c, c, c, 1f));
            }
        }
        tex.Apply(true);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
#endif
