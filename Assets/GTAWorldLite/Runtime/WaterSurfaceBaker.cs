using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Bakes a world-space water depth map from static ground colliders
/// and feeds it into the water material so foam/shore logic does not need SceneDepth.
/// Bake once via the Inspector button (or GTA → Bake Water Surface Depth).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
[DefaultExecutionOrder(-50)]
public sealed class WaterSurfaceBaker : MonoBehaviour
{
    const string GeneratedFolder = GTAWorldLitePaths.WaterGenerated;
    const float PlaneMeshSize = 10f;

    [SerializeField] WaterSurfaceData _data;
    [SerializeField] int _resolution = 1024;
    [SerializeField] [Range(0, 8)] int _blurRadius = 3;
    [SerializeField] LayerMask _groundMask = ~0;
    [SerializeField] float _raycastHeight = 500f;
    [SerializeField] float _defaultDeepDepth = 100f;

    public WaterSurfaceData Data => _data;

    void OnEnable()
    {
#if UNITY_EDITOR
        TryLoadExistingData();
#endif
        ApplyToMaterial();
    }

    void OnValidate()
    {
        ApplyToMaterial();
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying)
            ApplyToMaterial();
    }
#endif

    void Awake()
    {
        ApplyToMaterial();
    }

#if UNITY_EDITOR
    [ContextMenu("Bake Water Surface Depth")]
    public void BakeNow()
    {
        float start = Time.realtimeSinceStartup;
        Bake();
        float seconds = Time.realtimeSinceStartup - start;
        Debug.Log($"WaterSurfaceBaker: baked {_resolution}x{_resolution} depth map (blur {_blurRadius}) in {seconds:0.00}s → {_data}", this);
    }

    void TryLoadExistingData()
    {
        if (_data != null)
            return;

        string sceneName = EditorSceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName))
            sceneName = "Untitled";

        string dataPath = $"{GeneratedFolder}/{sceneName}_WaterSurface.asset";
        _data = AssetDatabase.LoadAssetAtPath<WaterSurfaceData>(dataPath);
        if (_data != null)
        {
            EditorUtility.SetDirty(this);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }

    void Bake()
    {
        if (!TryBakeDepthGrid(out Texture2D texture, out Vector4 bounds, out float waterY, out float maxDepth, out int resolution))
            return;

        SaveOrUpdateAsset(texture, bounds, waterY, maxDepth, resolution);
        ApplyToMaterial();

        EditorUtility.SetDirty(this);
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    void SaveOrUpdateAsset(Texture2D texture, Vector4 bounds, float waterY, float maxDepth, int resolution)
    {
        Directory.CreateDirectory(GeneratedFolder);

        string sceneName = EditorSceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName))
            sceneName = "Untitled";

        string assetName = $"{sceneName}_WaterSurface";
        string dataPath = $"{GeneratedFolder}/{assetName}.asset";
        string texturePath = $"{GeneratedFolder}/{assetName}_Depth.png";

        File.WriteAllBytes(texturePath, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(texturePath);

        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            // Uncompressed: DXT block compression causes cubic shore artifacts.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        _data = AssetDatabase.LoadAssetAtPath<WaterSurfaceData>(dataPath);
        if (_data == null)
        {
            _data = ScriptableObject.CreateInstance<WaterSurfaceData>();
            AssetDatabase.CreateAsset(_data, dataPath);
        }

        _data.DepthMap = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        _data.Bounds = bounds;
        _data.WaterLevel = waterY;
        _data.MaxDepth = maxDepth;
        _data.Resolution = resolution;
        _data.SceneHash = ComputeSceneHash();

        EditorUtility.SetDirty(_data);
        AssetDatabase.SaveAssets();
        DestroyImmediate(texture);
    }
#endif

    void ApplyToMaterial()
    {
        var renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
            return;

        var material = renderer.sharedMaterial;
        if (material == null)
            return;

#if UNITY_EDITOR
        if (_data == null)
            TryLoadExistingData();
#endif

        if (_data == null || _data.DepthMap == null)
        {
            if (material.HasProperty("_WaterUseBakedSurface"))
                material.SetFloat("_WaterUseBakedSurface", 0f);
            ApplyShoreFoamGlobals(material);
            return;
        }

        Shader.SetGlobalTexture("_WaterSurfaceMap", _data.DepthMap);
        Shader.SetGlobalVector("_WaterSurfaceBounds", _data.Bounds);
        Shader.SetGlobalFloat("_WaterSurfaceDepthMax", _data.MaxDepth);
        Shader.SetGlobalFloat("_GTA_WaterLevel", _data.WaterLevel);
        Shader.SetGlobalVector(
            "_WaterSurfaceMap_TexelSize",
            new Vector4(
                1f / _data.DepthMap.width,
                1f / _data.DepthMap.height,
                _data.DepthMap.width,
                _data.DepthMap.height));

        if (material.HasProperty("_WaterSurfaceMap"))
            material.SetTexture("_WaterSurfaceMap", _data.DepthMap);
        if (material.HasProperty("_WaterSurfaceBounds"))
            material.SetVector("_WaterSurfaceBounds", _data.Bounds);
        if (material.HasProperty("_WaterSurfaceDepthMax"))
            material.SetFloat("_WaterSurfaceDepthMax", _data.MaxDepth);
        if (material.HasProperty("_WaterUseBakedSurface"))
            material.SetFloat("_WaterUseBakedSurface", 1f);

        ApplyShoreFoamGlobals(material);
    }

    static void ApplyShoreFoamGlobals(Material material)
    {
        if (material == null)
            return;

        CopyGlobalFloat(material, "_WaveFrequency", "_GTA_WaveFrequency");
        CopyGlobalFloat(material, "_WaveSpeed", "_GTA_WaveSpeed");
        CopyGlobalFloat(material, "_WaveDist", "_GTA_WaveDist");
        CopyGlobalFloat(material, "_FoamShoreWobble", "_GTA_FoamShoreWobble");
        CopyGlobalFloat(material, "_FoamShoreScale", "_GTA_FoamShoreScale");
        CopyGlobalFloat(material, "_FoamBeachDepth", "_GTA_FoamBeachDepth");
        CopyGlobalFloat(material, "_FoamCoverage", "_GTA_FoamCoverage");
        CopyGlobalFloat(material, "_FoamThickness", "_GTA_FoamThickness");
        CopyGlobalFloat(material, "_UseFoam", "_GTA_UseFoam");
        CopyGlobalFloat(material, "_FoamDayBoost", "_GTA_FoamDayBoost");
        CopyGlobalColor(material, "_FoamColor", "_GTA_FoamColor");
        CopyGlobalTexture(material, "_FoamNoiseMap", "_GTA_FoamNoiseMap");
        CopyGlobalTextureST(material, "_FoamNoiseMap", "_GTA_FoamNoiseMap_ST");
    }

    static void CopyGlobalFloat(Material material, string property, string globalName)
    {
        if (material.HasProperty(property))
            Shader.SetGlobalFloat(globalName, material.GetFloat(property));
    }

    static void CopyGlobalColor(Material material, string property, string globalName)
    {
        if (material.HasProperty(property))
            Shader.SetGlobalColor(globalName, material.GetColor(property));
    }

    static void CopyGlobalTexture(Material material, string property, string globalName)
    {
        if (!material.HasProperty(property))
            return;

        var texture = material.GetTexture(property);
        if (texture != null)
            Shader.SetGlobalTexture(globalName, texture);
    }

    static void CopyGlobalTextureST(Material material, string property, string globalName)
    {
        if (!material.HasProperty(property))
            return;

        Vector2 scale = material.GetTextureScale(property);
        Vector2 offset = material.GetTextureOffset(property);
        Shader.SetGlobalVector(globalName, new Vector4(scale.x, scale.y, offset.x, offset.y));
    }

    Vector4 ComputeBounds()
    {
        Vector3 scale = transform.lossyScale;
        Vector3 center = transform.position;
        float halfX = PlaneMeshSize * 0.5f * scale.x;
        float halfZ = PlaneMeshSize * 0.5f * scale.z;
        return new Vector4(center.x - halfX, center.z - halfZ, halfX * 2f, halfZ * 2f);
    }

    long ComputeSceneHash()
    {
        unchecked
        {
            long hash = 17;
            hash = hash * 31 + transform.position.GetHashCode();
            hash = hash * 31 + transform.lossyScale.GetHashCode();

            var colliders = Object.FindObjectsByType<MeshCollider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform))
                    continue;

                hash = hash * 31 + collider.bounds.GetHashCode();
            }

            return hash;
        }
    }

    bool TryBakeDepthGrid(out Texture2D texture, out Vector4 bounds, out float waterY, out float maxDepth, out int resolution)
    {
        resolution = Mathf.Clamp(_resolution, 64, 2048);
        waterY = transform.position.y;
        bounds = ComputeBounds();
        float minX = bounds.x;
        float minZ = bounds.y;
        float sizeX = bounds.z;
        float sizeZ = bounds.w;

        var depthValues = new float[resolution * resolution];

        var waterCollider = GetComponent<Collider>();
        bool restoreCollider = waterCollider != null && waterCollider.enabled;
        if (waterCollider != null)
            waterCollider.enabled = false;

        try
        {
            Physics.SyncTransforms();

            for (int y = 0; y < resolution; y++)
            {
                float tz = (y + 0.5f) / resolution;
                float worldZ = minZ + tz * sizeZ;

                for (int x = 0; x < resolution; x++)
                {
                    float tx = (x + 0.5f) / resolution;
                    float worldX = minX + tx * sizeX;
                    depthValues[y * resolution + x] = SampleDepth(worldX, worldZ, waterY);
                }
            }
        }
        finally
        {
            if (waterCollider != null)
                waterCollider.enabled = restoreCollider;
        }

        if (_blurRadius > 0)
            depthValues = BlurDepth(depthValues, resolution, _blurRadius);

        maxDepth = 0.01f;
        for (int i = 0; i < depthValues.Length; i++)
        {
            if (depthValues[i] > maxDepth)
                maxDepth = depthValues[i];
        }

        texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "WaterSurfaceDepth"
        };

        var pixels = new Color32[resolution * resolution];
        for (int i = 0; i < depthValues.Length; i++)
        {
            byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(depthValues[i] / maxDepth) * 255f);
            pixels[i] = new Color32(value, 0, 0, 255);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return true;
    }

    static float[] BlurDepth(float[] source, int resolution, int radius)
    {
        var temp = new float[source.Length];
        var dest = new float[source.Length];
        System.Array.Copy(source, dest, source.Length);

        for (int pass = 0; pass < radius; pass++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float sum = 0f;
                    for (int k = -1; k <= 1; k++)
                    {
                        int xx = Mathf.Clamp(x + k, 0, resolution - 1);
                        sum += dest[y * resolution + xx];
                    }

                    temp[y * resolution + x] = sum / 3f;
                }
            }

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float sum = 0f;
                    for (int k = -1; k <= 1; k++)
                    {
                        int yy = Mathf.Clamp(y + k, 0, resolution - 1);
                        sum += temp[yy * resolution + x];
                    }

                    dest[y * resolution + x] = sum / 3f;
                }
            }
        }

        return dest;
    }

    float SampleDepth(float worldX, float worldZ, float waterY)
    {
        var origin = new Vector3(worldX, waterY + _raycastHeight, worldZ);
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _raycastHeight * 2f, _groundMask, QueryTriggerInteraction.Ignore))
            return _defaultDeepDepth;

        if (hit.collider != null &&
            (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)))
            return _defaultDeepDepth;

        if (hit.point.y >= waterY)
            return 0f;

        return waterY - hit.point.y;
    }
}
