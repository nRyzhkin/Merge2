using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Central baker for up to 4 light clusters. Put on the same object as <see cref="GTAWorldController"/>.
/// Prefab lights choose a <see cref="GTALightClusterId"/>; this component bakes volumes and exposes opacity.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("GTA/Light Cluster Controller")]
[DefaultExecutionOrder(40)]
public sealed class GTALightClusterController : MonoBehaviour
{
    public const int MaxClusters = 4;
    public const int MaxResolution = 96;
    public const int PreviewResolution = 8;
    public const float DefaultCpuBudgetMs = 1.5f;

    enum BakePhase : byte
    {
        Idle,
        Clear,
        Splat,
        Upload
    }

    enum BakePass : byte
    {
        Preview, // fast 8³ for immediate look
        HQ       // target resolution, lights near→far from camera
    }

    [Serializable]
    public sealed class ClusterSettings
    {
        public string label = "Night";
        [Tooltip("0 = off, 1 = full. Fade only — use Local Light intensity for brightness.")]
        [Range(0f, 1f)] public float opacity = 1f;
        [Tooltip("Extra bake multiplier. Keep ≤ 1; prefer raising lamp intensity (max 2).")]
        [Range(0f, 1f)] public float bakeBoost = 1f;
        [Tooltip("Final volume resolution after preview (8). Bakes near-camera lights first.")]
        [Range(PreviewResolution, MaxResolution)] public int resolution = 96;
        public bool manualBounds;
        public Bounds manualWorldBounds = new(Vector3.zero, Vector3.one * 80f);
        public float boundsPadding = 4f;
    }

    sealed class ClusterRuntime
    {
        public readonly List<GTALocalLight> Lights = new(64);
        public Texture3D Volume;
        public Color[] Voxels;
        public Bounds Bounds;
        public Bounds PublishedBounds;
        public Vector3 VoxelSize;
        public int Res;
        public int TargetRes;
        public int VoxelIndex;
        public int LightIndex;
        public BakePhase Phase = BakePhase.Idle;
        public BakePass Pass = BakePass.Preview;
        public bool Dirty = true;
        public bool Ready;
        public bool HasPreview;
        public bool HasHQ;
    }

    static GTALightClusterController _instance;

    [SerializeField] ClusterSettings[] clusters =
    {
        new() { label = "Night", opacity = 0f },
        new() { label = "Interior", opacity = 1f },
        new() { label = "Effects", opacity = 1f },
        new() { label = "Extra", opacity = 1f },
    };

    [Header("Budget")]
    [SerializeField, Range(0.25f, 4f)] float cpuBudgetMs = DefaultCpuBudgetMs;
    [SerializeField, Range(8, 4096)] int maxVoxelsPerFrame = 512;
    [SerializeField] bool rebuildOnEnable = true;
    [Tooltip("Edit Mode: progressive bake while idle + Scene View preview.")]
    [SerializeField] bool previewInEditMode = true;
    [SerializeField] bool logBake;

    readonly ClusterRuntime[] _rt = new ClusterRuntime[MaxClusters];
    readonly Stopwatch _clock = new();
    int _activeBakeSlot = -1;
    int _bakeSettingsHash;
    static Texture3D _blackVolume;

    static readonly int[] IdVolume =
    {
        Shader.PropertyToID("_GTA_LC0"),
        Shader.PropertyToID("_GTA_LC1"),
        Shader.PropertyToID("_GTA_LC2"),
        Shader.PropertyToID("_GTA_LC3"),
    };
    static readonly int[] IdMin =
    {
        Shader.PropertyToID("_GTA_LC0_Min"),
        Shader.PropertyToID("_GTA_LC1_Min"),
        Shader.PropertyToID("_GTA_LC2_Min"),
        Shader.PropertyToID("_GTA_LC3_Min"),
    };
    static readonly int[] IdInvSize =
    {
        Shader.PropertyToID("_GTA_LC0_InvSize"),
        Shader.PropertyToID("_GTA_LC1_InvSize"),
        Shader.PropertyToID("_GTA_LC2_InvSize"),
        Shader.PropertyToID("_GTA_LC3_InvSize"),
    };
    static readonly int IdOpacity = Shader.PropertyToID("_GTA_LCOpacity");
    static readonly int IdCount = Shader.PropertyToID("_GTA_LCCount");

    public static GTALightClusterController Instance => _instance;

    public float GetOpacity(GTALightClusterId id) =>
        clusters != null && (int)id < clusters.Length ? clusters[(int)id].opacity : 0f;

    public void SetOpacity(GTALightClusterId id, float value)
    {
        EnsureClusterArray();
        clusters[(int)id].opacity = Mathf.Clamp01(value);
    }

    public void MarkDirty(GTALightClusterId id)
    {
        EnsureRuntimes();
        _rt[(int)id].Dirty = true;
    }

    public void MarkAllDirty()
    {
        EnsureRuntimes();
        for (int i = 0; i < MaxClusters; i++)
            _rt[i].Dirty = true;
    }

    public static void NotifyLightChanged(GTALightClusterId id)
    {
        var ctrl = _instance != null ? _instance : FindController();
        ctrl?.MarkDirty(id);
    }

    static GTALightClusterController FindController() =>
        UnityEngine.Object.FindAnyObjectByType<GTALightClusterController>();

    /// <summary>Editor / tools: bake every dirty cluster to completion and refresh Scene View.</summary>
    public void BakeAllNow()
    {
        EnsureClusterArray();
        EnsureRuntimes();
        AbortActiveBake();
        MarkAllDirty();
        FinishPendingBakes(timeoutSeconds: 30f);
        PublishGlobals();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneView.RepaintAll();
#endif
    }

    public void BakeClusterNow(GTALightClusterId id)
    {
        EnsureClusterArray();
        EnsureRuntimes();
        int slot = (int)id;

        if (_activeBakeSlot >= 0 && _activeBakeSlot != slot)
        {
            // Pause other bake without losing its dirty flag.
            if (_rt[_activeBakeSlot].Phase != BakePhase.Idle)
                _rt[_activeBakeSlot].Dirty = true;
            _rt[_activeBakeSlot].Phase = BakePhase.Idle;
            _activeBakeSlot = -1;
        }

        MarkDirty(id);

        float savedBudget = cpuBudgetMs;
        int savedVoxels = maxVoxelsPerFrame;
        cpuBudgetMs = 50f;
        maxVoxelsPerFrame = 8192;

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 30f)
        {
            ClusterRuntime rt = _rt[slot];
            bool needHq = rt.HasPreview && !rt.HasHQ && rt.TargetRes > PreviewResolution;
            if (!rt.Dirty && rt.Phase == BakePhase.Idle && !needHq)
                break;
            Tick();
        }

        cpuBudgetMs = savedBudget;
        maxVoxelsPerFrame = savedVoxels;
        PublishGlobals();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneView.RepaintAll();
#endif
    }

    public string GetClusterStatus(int slot)
    {
        EnsureRuntimes();
        if (slot < 0 || slot >= MaxClusters || _rt[slot] == null)
            return "—";
        ClusterRuntime rt = _rt[slot];
        if (rt.Phase != BakePhase.Idle)
        {
            string pass = rt.Pass == BakePass.Preview ? "preview 8³" : $"HQ {rt.TargetRes}³";
            return $"{pass} · {rt.Phase}…";
        }

        if (rt.Dirty)
            return "dirty";
        if (rt.HasHQ)
            return $"HQ {rt.Res}³ · {rt.Lights.Count} lights";
        if (rt.HasPreview || rt.Ready)
            return $"preview {rt.Res}³ · refining…";
        return "empty";
    }

    public bool IsBaking => _activeBakeSlot >= 0;

    void AbortActiveBake()
    {
        if (_activeBakeSlot >= 0 && _activeBakeSlot < MaxClusters && _rt[_activeBakeSlot] != null)
        {
            _rt[_activeBakeSlot].Phase = BakePhase.Idle;
            _rt[_activeBakeSlot].Dirty = true;
        }

        _activeBakeSlot = -1;
    }

    void FinishPendingBakes(float timeoutSeconds)
    {
        float savedBudget = cpuBudgetMs;
        int savedVoxels = maxVoxelsPerFrame;
        cpuBudgetMs = 50f;
        maxVoxelsPerFrame = 8192;

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < timeoutSeconds)
        {
            if (!HasPendingBakeWork())
                break;
            Tick();
        }

        cpuBudgetMs = savedBudget;
        maxVoxelsPerFrame = savedVoxels;
    }

    bool HasPendingBakeWork()
    {
        if (_activeBakeSlot >= 0)
            return true;

        for (int i = 0; i < MaxClusters; i++)
        {
            ClusterRuntime rt = _rt[i];
            if (rt == null)
                continue;
            if (rt.Dirty || rt.Phase != BakePhase.Idle)
                return true;
            if (rt.HasPreview && !rt.HasHQ && rt.TargetRes > PreviewResolution)
                return true;
        }

        return false;
    }

    void OnEnable()
    {
        _instance = this;
        EnsureClusterArray();
        EnsureRuntimes();
        _bakeSettingsHash = ComputeBakeSettingsHash();
        if (rebuildOnEnable)
            MarkAllDirty();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
        if (!Application.isPlaying && previewInEditMode)
            UnityEditor.EditorApplication.update += EditorTick;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
        if (_instance == this)
            _instance = null;
        PushDisabledGlobals();
        _activeBakeSlot = -1;
    }

    void OnDestroy()
    {
        for (int i = 0; i < _rt.Length; i++)
            ReleaseVolume(_rt[i]);
    }

    void LateUpdate()
    {
        if (!Application.isPlaying)
            return;
        Tick();
        PublishGlobals();
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (Application.isPlaying || this == null || !isActiveAndEnabled || !previewInEditMode)
            return;
        Tick();
        PublishGlobals();
    }

    void OnValidate()
    {
        EnsureClusterArray();
        for (int i = 0; i < clusters.Length; i++)
        {
            if (clusters[i] == null)
                clusters[i] = new ClusterSettings();
            clusters[i].resolution = Mathf.Clamp(clusters[i].resolution, PreviewResolution, MaxResolution);
            clusters[i].opacity = Mathf.Clamp01(clusters[i].opacity);
            clusters[i].bakeBoost = Mathf.Clamp01(clusters[i].bakeBoost);
        }

        int hash = ComputeBakeSettingsHash();
        if (hash != _bakeSettingsHash)
        {
            _bakeSettingsHash = hash;
            if (isActiveAndEnabled)
                MarkAllDirty();
        }

        // Opacity-only changes: push globals so Scene View updates without rebake.
        if (!Application.isPlaying && isActiveAndEnabled)
        {
            EnsureRuntimes();
            PublishGlobals();
            UnityEditor.SceneView.RepaintAll();
        }
    }
#endif

    int ComputeBakeSettingsHash()
    {
        unchecked
        {
            int h = 17;
            if (clusters == null)
                return h;
            for (int i = 0; i < clusters.Length; i++)
            {
                ClusterSettings c = clusters[i];
                if (c == null)
                    continue;
                h = h * 31 + c.resolution;
                h = h * 31 + c.bakeBoost.GetHashCode();
                h = h * 31 + (c.manualBounds ? 1 : 0);
                h = h * 31 + c.boundsPadding.GetHashCode();
                h = h * 31 + c.manualWorldBounds.GetHashCode();
            }

            return h;
        }
    }

    void Tick()
    {
        EnsureRuntimes();

        if (_activeBakeSlot < 0)
        {
            // Prefer starting dirty clusters (preview). Then continue HQ refine on ready previews.
            for (int i = 0; i < MaxClusters; i++)
            {
                if (_rt[i].Dirty && _rt[i].Phase == BakePhase.Idle)
                {
                    BeginBake(i, BakePass.Preview);
                    break;
                }
            }

            if (_activeBakeSlot < 0)
            {
                for (int i = 0; i < MaxClusters; i++)
                {
                    ClusterRuntime rt = _rt[i];
                    if (rt.Phase != BakePhase.Idle || rt.Dirty)
                        continue;
                    if (rt.HasPreview && !rt.HasHQ && rt.TargetRes > PreviewResolution)
                    {
                        BeginBake(i, BakePass.HQ);
                        break;
                    }
                }
            }
        }

        if (_activeBakeSlot >= 0)
            PumpBake(_activeBakeSlot);
    }

    void BeginBake(int slot, BakePass pass)
    {
        ClusterSettings settings = clusters[slot];
        ClusterRuntime rt = _rt[slot];
        rt.Dirty = false;
        rt.Pass = pass;
        rt.TargetRes = Mathf.Clamp(settings.resolution, PreviewResolution, MaxResolution);
        if (rt.TargetRes < PreviewResolution)
            rt.TargetRes = PreviewResolution;

        if (pass == BakePass.Preview)
        {
            rt.HasPreview = false;
            rt.HasHQ = false;
            rt.Ready = false;
            rt.Lights.Clear();
            CollectLightsForSlot(slot, rt.Lights);
            BuildBounds(settings, rt);
            rt.Res = PreviewResolution;
        }
        else
        {
            // HQ reuses lights/bounds from preview; refresh list in case lights moved.
            rt.Lights.Clear();
            CollectLightsForSlot(slot, rt.Lights);
            BuildBounds(settings, rt);
            rt.Res = rt.TargetRes;
            SortLightsNearToFar(rt.Lights);
        }

        int count = rt.Res * rt.Res * rt.Res;
        if (rt.Voxels == null || rt.Voxels.Length != count)
            rt.Voxels = new Color[count];

        // Preview: create volume immediately. HQ: keep showing preview Texture3D until Upload swaps it.
        if (pass == BakePass.Preview)
            EnsureVolume(rt, rt.Res, settings.label);

        rt.VoxelSize = new Vector3(
            rt.Bounds.size.x / rt.Res,
            rt.Bounds.size.y / rt.Res,
            rt.Bounds.size.z / rt.Res);
        rt.VoxelIndex = 0;
        rt.LightIndex = 0;
        rt.Phase = BakePhase.Clear;
        _activeBakeSlot = slot;

        if (logBake)
            Debug.Log($"[GTALightCluster] Bake '{settings.label}' pass={pass} lights={rt.Lights.Count} res={rt.Res}");
    }

    void PumpBake(int slot)
    {
        ClusterRuntime rt = _rt[slot];
        ClusterSettings settings = clusters[slot];
        _clock.Restart();
        double budget = Mathf.Max(0.2f, cpuBudgetMs);

        while (_clock.Elapsed.TotalMilliseconds < budget)
        {
            switch (rt.Phase)
            {
                case BakePhase.Clear:
                    StepClear(rt, budget);
                    break;
                case BakePhase.Splat:
                    StepSplat(rt, settings, budget);
                    break;
                case BakePhase.Upload:
                    StepUpload(rt, settings);
                    return;
                default:
                    _activeBakeSlot = -1;
                    return;
            }

            if (rt.Phase == BakePhase.Idle)
            {
                _activeBakeSlot = -1;
                return;
            }
        }
    }

    void StepClear(ClusterRuntime rt, double budget)
    {
        int count = rt.Voxels.Length;
        while (rt.VoxelIndex < count && _clock.Elapsed.TotalMilliseconds < budget)
        {
            int end = Mathf.Min(count, rt.VoxelIndex + maxVoxelsPerFrame);
            for (int i = rt.VoxelIndex; i < end; i++)
                rt.Voxels[i] = Color.clear;
            rt.VoxelIndex = end;
        }

        if (rt.VoxelIndex >= count)
        {
            rt.VoxelIndex = 0;
            rt.LightIndex = 0;
            rt.Phase = rt.Lights.Count > 0 ? BakePhase.Splat : BakePhase.Upload;
        }
    }

    void StepSplat(ClusterRuntime rt, ClusterSettings settings, double budget)
    {
        while (rt.LightIndex < rt.Lights.Count && _clock.Elapsed.TotalMilliseconds < budget)
        {
            GTALocalLight light = rt.Lights[rt.LightIndex];
            if (light != null && light.IncludeInBake && light.Intensity > 1e-5f)
                SplatLight(rt, light, settings.bakeBoost);
            rt.LightIndex++;
            if (_clock.Elapsed.TotalMilliseconds >= budget)
                break;
        }

        if (rt.LightIndex >= rt.Lights.Count)
            rt.Phase = BakePhase.Upload;
    }

    static void SplatLight(ClusterRuntime rt, GTALocalLight light, float bakeBoost)
    {
        Vector3 pos = light.PositionWS;
        float range = Mathf.Max(0.1f, light.Range);
        float rangeSq = range * range;
        float intensity = Mathf.Clamp(light.Intensity, 0f, GTALocalLight.MaxIntensity);
        float boost = Mathf.Clamp01(bakeBoost);
        Color rgb = light.Color * (intensity * boost);
        rgb.a = 0f;

        Vector3Int i0 = WorldToVoxelClamped(rt, pos - Vector3.one * range);
        Vector3Int i1 = WorldToVoxelClamped(rt, pos + Vector3.one * range);
        const float voxelCap = GTALocalLight.MaxIntensity;

        for (int z = i0.z; z <= i1.z; z++)
        for (int y = i0.y; y <= i1.y; y++)
        for (int x = i0.x; x <= i1.x; x++)
        {
            Vector3 samplePos = VoxelCenter(rt, x, y, z);
            float distSq = (samplePos - pos).sqrMagnitude;
            if (distSq > rangeSq)
                continue;

            float dist = Mathf.Sqrt(distSq);
            float nd = 1f - dist / range;
            float atten = nd * nd;
            int idx = x + y * rt.Res + z * rt.Res * rt.Res;
            Color v = rt.Voxels[idx] + rgb * atten;
            v.r = Mathf.Min(v.r, voxelCap);
            v.g = Mathf.Min(v.g, voxelCap);
            v.b = Mathf.Min(v.b, voxelCap);
            rt.Voxels[idx] = v;
        }
    }

    void StepUpload(ClusterRuntime rt, ClusterSettings settings)
    {
        EnsureVolume(rt, rt.Res, settings.label);
        if (rt.Volume == null)
        {
            rt.Phase = BakePhase.Idle;
            _activeBakeSlot = -1;
            return;
        }

        rt.Volume.SetPixels(rt.Voxels);
        rt.Volume.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        rt.PublishedBounds = rt.Bounds;
        rt.Ready = true;
        rt.Phase = BakePhase.Idle;

        if (rt.Pass == BakePass.Preview)
        {
            rt.HasPreview = true;
            rt.HasHQ = rt.TargetRes <= PreviewResolution;
            if (logBake)
                Debug.Log($"[GTALightCluster] Preview ready '{settings.label}' 8³");
        }
        else
        {
            rt.HasHQ = true;
            if (logBake)
                Debug.Log($"[GTALightCluster] HQ ready '{settings.label}' {rt.Res}³");
        }

        _activeBakeSlot = -1;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneView.RepaintAll();
#endif
    }

    static void SortLightsNearToFar(List<GTALocalLight> lights)
    {
        Vector3 cam = ResolveBakeCameraPosition();
        lights.Sort((a, b) =>
        {
            float da = a == null ? float.MaxValue : (a.PositionWS - cam).sqrMagnitude;
            float db = b == null ? float.MaxValue : (b.PositionWS - cam).sqrMagnitude;
            return da.CompareTo(db);
        });
    }

    static Vector3 ResolveBakeCameraPosition()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var sv = UnityEditor.SceneView.lastActiveSceneView;
            if (sv != null && sv.camera != null)
                return sv.camera.transform.position;
        }
#endif
        Camera cam = Camera.main;
        if (cam != null)
            return cam.transform.position;
        return Vector3.zero;
    }

    void CollectLightsForSlot(int slot, List<GTALocalLight> dst)
    {
        var found = FindObjectsByType<GTALocalLight>(FindObjectsInactive.Exclude);
        for (int i = 0; i < found.Length; i++)
        {
            GTALocalLight l = found[i];
            if (l != null && (int)l.Cluster == slot && l.IncludeInBake)
                dst.Add(l);
        }
    }

    static void BuildBounds(ClusterSettings settings, ClusterRuntime rt)
    {
        if (settings.manualBounds)
        {
            rt.Bounds = settings.manualWorldBounds;
            if (rt.Bounds.size.sqrMagnitude < 1e-4f)
                rt.Bounds = new Bounds(Vector3.zero, Vector3.one * 40f);
            return;
        }

        if (rt.Lights.Count == 0)
        {
            rt.Bounds = new Bounds(Vector3.zero, Vector3.one * 40f);
            return;
        }

        bool init = false;
        Bounds b = default;
        for (int i = 0; i < rt.Lights.Count; i++)
        {
            GTALocalLight l = rt.Lights[i];
            if (l == null)
                continue;
            var lb = new Bounds(l.PositionWS, Vector3.one * (l.Range * 2f));
            if (!init)
            {
                b = lb;
                init = true;
            }
            else
                b.Encapsulate(lb);
        }

        if (!init)
            b = new Bounds(Vector3.zero, Vector3.one * 40f);

        b.Expand(settings.boundsPadding * 2f);
        rt.Bounds = b;
    }

    static Vector3Int WorldToVoxelClamped(ClusterRuntime rt, Vector3 world)
    {
        Vector3 local = world - rt.Bounds.min;
        int x = Mathf.Clamp(Mathf.FloorToInt(local.x / rt.VoxelSize.x), 0, rt.Res - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(local.y / rt.VoxelSize.y), 0, rt.Res - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(local.z / rt.VoxelSize.z), 0, rt.Res - 1);
        return new Vector3Int(x, y, z);
    }

    static Vector3 VoxelCenter(ClusterRuntime rt, int x, int y, int z)
    {
        return rt.Bounds.min + new Vector3(
            (x + 0.5f) * rt.VoxelSize.x,
            (y + 0.5f) * rt.VoxelSize.y,
            (z + 0.5f) * rt.VoxelSize.z);
    }

    static void EnsureVolume(ClusterRuntime rt, int res, string label)
    {
        if (rt.Volume != null && rt.Volume.width == res)
            return;

        ReleaseVolume(rt);
        rt.Volume = new Texture3D(res, res, res, TextureFormat.RGBAHalf, false)
        {
            name = $"GTA LC '{label}'",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    static void ReleaseVolume(ClusterRuntime rt)
    {
        if (rt == null || rt.Volume == null)
            return;
        DestroyImmediate(rt.Volume);
        rt.Volume = null;
        rt.Ready = false;
    }

    void PublishGlobals()
    {
        EnsureBlackVolume();
        EnsureRuntimes();

        int count = 0;
        var opacity = Vector4.zero;

        for (int i = 0; i < MaxClusters; i++)
        {
            ClusterRuntime rt = _rt[i];
            ClusterSettings settings = clusters[i];
            bool ready = rt.Ready && rt.Volume != null;
            float op = ready ? Mathf.Clamp01(settings.opacity) : 0f;
            Texture3D vol = ready ? rt.Volume : _blackVolume;
            Bounds b = rt.PublishedBounds;
            if (b.size.sqrMagnitude < 1e-6f)
                b = rt.Bounds;
            Vector3 size = b.size;
            Vector3 inv = new(
                size.x > 1e-4f ? 1f / size.x : 0f,
                size.y > 1e-4f ? 1f / size.y : 0f,
                size.z > 1e-4f ? 1f / size.z : 0f);

            Shader.SetGlobalTexture(IdVolume[i], vol);
            Shader.SetGlobalVector(IdMin[i], ready ? (Vector4)b.min : Vector4.zero);
            Shader.SetGlobalVector(IdInvSize[i], ready ? (Vector4)inv : Vector4.zero);

            if (i == 0) opacity.x = op;
            else if (i == 1) opacity.y = op;
            else if (i == 2) opacity.z = op;
            else opacity.w = op;

            if (ready && op > 1e-4f)
                count = i + 1;
        }

        Shader.SetGlobalVector(IdOpacity, opacity);
        Shader.SetGlobalFloat(IdCount, count);
    }

    static void PushDisabledGlobals()
    {
        EnsureBlackVolume();
        for (int i = 0; i < MaxClusters; i++)
        {
            Shader.SetGlobalTexture(IdVolume[i], _blackVolume);
            Shader.SetGlobalVector(IdMin[i], Vector4.zero);
            Shader.SetGlobalVector(IdInvSize[i], Vector4.zero);
        }

        Shader.SetGlobalVector(IdOpacity, Vector4.zero);
        Shader.SetGlobalFloat(IdCount, 0f);
    }

    static void EnsureBlackVolume()
    {
        if (_blackVolume != null)
            return;

        _blackVolume = new Texture3D(1, 1, 1, TextureFormat.RGBAHalf, false)
        {
            name = "GTA LC Black",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        _blackVolume.SetPixel(0, 0, 0, Color.clear);
        _blackVolume.Apply(false, true);
    }

    void EnsureClusterArray()
    {
        if (clusters != null && clusters.Length == MaxClusters)
        {
            for (int i = 0; i < MaxClusters; i++)
                if (clusters[i] == null)
                    clusters[i] = DefaultCluster(i);
            return;
        }

        var next = new ClusterSettings[MaxClusters];
        for (int i = 0; i < MaxClusters; i++)
        {
            if (clusters != null && i < clusters.Length && clusters[i] != null)
                next[i] = clusters[i];
            else
                next[i] = DefaultCluster(i);
        }

        clusters = next;
    }

    static ClusterSettings DefaultCluster(int i) => i switch
    {
        0 => new ClusterSettings { label = "Night", opacity = 0f, resolution = 96 },
        1 => new ClusterSettings { label = "Interior", opacity = 1f, resolution = 48 },
        2 => new ClusterSettings { label = "Effects", opacity = 1f, resolution = 32 },
        _ => new ClusterSettings { label = "Extra", opacity = 1f, resolution = 48 },
    };

    void EnsureRuntimes()
    {
        for (int i = 0; i < MaxClusters; i++)
        {
            if (_rt[i] == null)
                _rt[i] = new ClusterRuntime();
        }
    }
}
