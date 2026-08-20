using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Baked world-space AO volume (not screen-space). Preview 8³ then HQ up to 96³.
/// Uses Physics raycasts — static geometry needs colliders. Put next to GTAWorldController.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("GTA/Ambient Occlusion")]
[DefaultExecutionOrder(45)]
public sealed class GTAAmbientOcclusion : MonoBehaviour
{
    public const int MaxResolution = 96;
    public const int PreviewResolution = 8;
    public const float DefaultCpuBudgetMs = 1.5f;
    public const int DefaultRayCount = 16;

    enum BakePhase : byte
    {
        Idle,
        Clear,
        Trace,
        Upload
    }

    enum BakePass : byte
    {
        Preview,
        HQ
    }

    [Header("Look")]
    [SerializeField, Range(0f, 1f)] float strength = 0.65f;
    [Tooltip("How much AO also darkens baked local lights (0 = ambient only).")]
    [SerializeField, Range(0f, 1f)] float localLightMul = 0.35f;
    [SerializeField, Range(0.05f, 0.95f)] float minOcclusion = 0.35f;

    [Header("Volume")]
    [SerializeField, Range(PreviewResolution, MaxResolution)] int resolution = 64;
    [SerializeField] bool manualBounds = true;
    [SerializeField] Bounds manualWorldBounds = new(Vector3.zero, new Vector3(200f, 80f, 200f));
    [SerializeField] float boundsPadding = 2f;

    [Header("Trace")]
    [SerializeField, Range(4, 32)] int rayCount = DefaultRayCount;
    [SerializeField, Min(0.5f)] float maxRayDistance = 6f;
    [SerializeField, Min(0.01f)] float rayBias = 0.08f;
    [SerializeField] LayerMask rayMask = ~0;
    [SerializeField] QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Budget")]
    [SerializeField, Range(0.25f, 8f)] float cpuBudgetMs = DefaultCpuBudgetMs;
    [SerializeField, Range(8, 2048)] int maxVoxelsPerFrame = 256;
    [SerializeField] bool rebuildOnEnable = true;
    [SerializeField] bool previewInEditMode = true;
    [SerializeField] bool logBake;

    static readonly int IdVolume = Shader.PropertyToID("_GTA_AOVolume");
    static readonly int IdMin = Shader.PropertyToID("_GTA_AOMin");
    static readonly int IdInvSize = Shader.PropertyToID("_GTA_AOInvSize");
    static readonly int IdParams = Shader.PropertyToID("_GTA_AOParams"); // x=strength, y=localMul, z=minAo, w=enabled

    static GTAAmbientOcclusion _instance;
    static Texture3D _whiteVolume;
    static Vector3[] _rayDirs;

    readonly Stopwatch _clock = new();
    readonly RaycastHit[] _hit = new RaycastHit[1];
    readonly List<int> _voxelOrder = new(512);

    Texture3D _volume;
    Color[] _voxels;
    Bounds _bounds;
    Bounds _publishedBounds;
    Vector3 _voxelSize;
    int _res;
    int _targetRes;
    int _voxelCursor;
    BakePhase _phase = BakePhase.Idle;
    BakePass _pass = BakePass.Preview;
    bool _dirty = true;
    bool _ready;
    bool _hasPreview;
    bool _hasHQ;
    int _settingsHash;

    public static GTAAmbientOcclusion Instance => _instance;

    public float Strength
    {
        get => strength;
        set => strength = Mathf.Clamp01(value);
    }

    public bool IsReady => _ready && _volume != null;
    public bool IsBaking => _phase != BakePhase.Idle;
    public string Status
    {
        get
        {
            if (_phase != BakePhase.Idle)
                return _pass == BakePass.Preview ? $"preview 8³ · {_phase}…" : $"HQ {_targetRes}³ · {_phase}…";
            if (_dirty)
                return "dirty";
            if (_hasHQ)
                return $"HQ {_res}³";
            if (_hasPreview)
                return $"preview {_res}³ · refining…";
            return "empty";
        }
    }

    public void MarkDirty() => _dirty = true;

    public void BakeNow()
    {
        AbortBake(keepDirty: false);
        _dirty = true;
        _hasPreview = false;
        _hasHQ = false;
        float savedBudget = cpuBudgetMs;
        int savedVoxels = maxVoxelsPerFrame;
        cpuBudgetMs = 50f;
        maxVoxelsPerFrame = 2048;

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 60f && HasPendingWork())
            TickBake();

        cpuBudgetMs = savedBudget;
        maxVoxelsPerFrame = savedVoxels;
        PublishGlobals();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneView.RepaintAll();
#endif
    }

    void OnEnable()
    {
        _instance = this;
        EnsureRayDirections();
        _settingsHash = ComputeSettingsHash();
        if (rebuildOnEnable)
            _dirty = true;
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
        AbortBake(keepDirty: true);
    }

    void OnDestroy()
    {
        ReleaseVolume();
    }

    void LateUpdate()
    {
        if (!Application.isPlaying)
            return;
        TickBake();
        PublishGlobals();
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (Application.isPlaying || this == null || !isActiveAndEnabled || !previewInEditMode)
            return;
        TickBake();
        PublishGlobals();
    }

    void OnValidate()
    {
        resolution = Mathf.Clamp(resolution, PreviewResolution, MaxResolution);
        rayCount = Mathf.Clamp(rayCount, 4, 32);
        strength = Mathf.Clamp01(strength);
        localLightMul = Mathf.Clamp01(localLightMul);
        minOcclusion = Mathf.Clamp(minOcclusion, 0.05f, 0.95f);

        int hash = ComputeSettingsHash();
        if (hash != _settingsHash)
        {
            _settingsHash = hash;
            if (isActiveAndEnabled)
                _dirty = true;
        }

        if (!Application.isPlaying && isActiveAndEnabled)
        {
            PublishGlobals();
            UnityEditor.SceneView.RepaintAll();
        }
    }
#endif

    bool HasPendingWork()
    {
        if (_phase != BakePhase.Idle || _dirty)
            return true;
        return _hasPreview && !_hasHQ && _targetRes > PreviewResolution;
    }

    void AbortBake(bool keepDirty)
    {
        if (_phase != BakePhase.Idle && keepDirty)
            _dirty = true;
        _phase = BakePhase.Idle;
    }

    void TickBake()
    {
        if (_phase == BakePhase.Idle)
        {
            if (_dirty)
                BeginPass(BakePass.Preview);
            else if (_hasPreview && !_hasHQ && _targetRes > PreviewResolution)
                BeginPass(BakePass.HQ);
        }

        if (_phase != BakePhase.Idle)
            Pump();
    }

    void BeginPass(BakePass pass)
    {
        _dirty = false;
        _pass = pass;
        _targetRes = Mathf.Clamp(resolution, PreviewResolution, MaxResolution);
        BuildBounds();

        _res = pass == BakePass.Preview ? PreviewResolution : _targetRes;
        int count = _res * _res * _res;
        if (_voxels == null || _voxels.Length != count)
            _voxels = new Color[count];

        _voxelSize = new Vector3(
            _bounds.size.x / _res,
            _bounds.size.y / _res,
            _bounds.size.z / _res);

        BuildVoxelOrderNearToFar();
        _voxelCursor = 0;
        _phase = BakePhase.Clear;

        if (pass == BakePass.Preview)
        {
            _hasPreview = false;
            _hasHQ = false;
            _ready = false;
            EnsureVolume(_res);
        }

        if (logBake)
            Debug.Log($"[GTA AO] Bake pass={pass} res={_res} rays={rayCount} bounds={_bounds}");
    }

    void Pump()
    {
        _clock.Restart();
        double budget = Mathf.Max(0.2f, cpuBudgetMs);

        while (_clock.Elapsed.TotalMilliseconds < budget)
        {
            switch (_phase)
            {
                case BakePhase.Clear:
                    StepClear(budget);
                    break;
                case BakePhase.Trace:
                    StepTrace(budget);
                    break;
                case BakePhase.Upload:
                    StepUpload();
                    return;
                default:
                    return;
            }

            if (_phase == BakePhase.Idle)
                return;
        }
    }

    void StepClear(double budget)
    {
        int count = _voxels.Length;
        while (_voxelCursor < count && _clock.Elapsed.TotalMilliseconds < budget)
        {
            int end = Mathf.Min(count, _voxelCursor + maxVoxelsPerFrame);
            for (int i = _voxelCursor; i < end; i++)
                _voxels[i] = Color.white; // 1 = unoccluded
            _voxelCursor = end;
        }

        if (_voxelCursor >= count)
        {
            _voxelCursor = 0;
            _phase = BakePhase.Trace;
        }
    }

    void StepTrace(double budget)
    {
        EnsureRayDirections();
        int dirCount = Mathf.Min(rayCount, _rayDirs.Length);

        while (_voxelCursor < _voxelOrder.Count && _clock.Elapsed.TotalMilliseconds < budget)
        {
            int end = Mathf.Min(_voxelOrder.Count, _voxelCursor + maxVoxelsPerFrame);
            for (int i = _voxelCursor; i < end; i++)
            {
                int idx = _voxelOrder[i];
                DecodeVoxel(idx, out int x, out int y, out int z);
                Vector3 pos = VoxelCenter(x, y, z);
                _voxels[idx] = new Color(TraceAo(pos, dirCount), 0f, 0f, 1f);
            }

            _voxelCursor = end;
        }

        if (_voxelCursor >= _voxelOrder.Count)
            _phase = BakePhase.Upload;
    }

    float TraceAo(Vector3 pos, int dirCount)
    {
        float occluded = 0f;
        float maxDist = Mathf.Max(0.5f, maxRayDistance);
        Vector3 origin = pos;

        for (int i = 0; i < dirCount; i++)
        {
            Vector3 dir = _rayDirs[i];
            Vector3 o = origin + dir * rayBias;
            int hits = Physics.RaycastNonAlloc(o, dir, _hit, maxDist, rayMask, triggerInteraction);
            if (hits > 0)
            {
                float t = Mathf.Clamp01(_hit[0].distance / maxDist);
                // Closer hits occlude more.
                occluded += 1f - t * t;
            }
        }

        float open = 1f - occluded / dirCount;
        return Mathf.Clamp(open, minOcclusion, 1f);
    }

    void StepUpload()
    {
        EnsureVolume(_res);
        if (_volume == null)
        {
            _phase = BakePhase.Idle;
            return;
        }

        _volume.SetPixels(_voxels);
        _volume.Apply(false, false);
        _publishedBounds = _bounds;
        _ready = true;
        _phase = BakePhase.Idle;

        if (_pass == BakePass.Preview)
        {
            _hasPreview = true;
            _hasHQ = _targetRes <= PreviewResolution;
        }
        else
            _hasHQ = true;

        if (logBake)
            Debug.Log($"[GTA AO] Upload {_pass} res={_res}");

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneView.RepaintAll();
#endif
    }

    void BuildBounds()
    {
        if (manualBounds)
        {
            _bounds = manualWorldBounds;
            if (_bounds.size.sqrMagnitude < 1e-4f)
                _bounds = new Bounds(transform.position, new Vector3(100f, 40f, 100f));
            return;
        }

        var renderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
        if (renderers.Length == 0)
        {
            _bounds = new Bounds(transform.position, new Vector3(100f, 40f, 100f));
            return;
        }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        b.Expand(boundsPadding * 2f);
        _bounds = b;
    }

    void BuildVoxelOrderNearToFar()
    {
        _voxelOrder.Clear();
        int count = _res * _res * _res;
        for (int i = 0; i < count; i++)
            _voxelOrder.Add(i);

        Vector3 cam = ResolveCameraPosition();
        _voxelOrder.Sort((a, b) =>
        {
            DecodeVoxel(a, out int ax, out int ay, out int az);
            DecodeVoxel(b, out int bx, out int by, out int bz);
            float da = (VoxelCenter(ax, ay, az) - cam).sqrMagnitude;
            float db = (VoxelCenter(bx, by, bz) - cam).sqrMagnitude;
            return da.CompareTo(db);
        });
    }

    void DecodeVoxel(int idx, out int x, out int y, out int z)
    {
        int area = _res * _res;
        z = idx / area;
        int rem = idx - z * area;
        y = rem / _res;
        x = rem - y * _res;
    }

    Vector3 VoxelCenter(int x, int y, int z)
    {
        return _bounds.min + new Vector3(
            (x + 0.5f) * _voxelSize.x,
            (y + 0.5f) * _voxelSize.y,
            (z + 0.5f) * _voxelSize.z);
    }

    static Vector3 ResolveCameraPosition()
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
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    void EnsureVolume(int res)
    {
        if (_volume != null && _volume.width == res)
            return;

        ReleaseVolume();
        _volume = new Texture3D(res, res, res, TextureFormat.RHalf, false)
        {
            name = "GTA AO Volume",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    void ReleaseVolume()
    {
        if (_volume == null)
            return;
        DestroyImmediate(_volume);
        _volume = null;
        _ready = false;
    }

    void PublishGlobals()
    {
        EnsureWhiteVolume();
        bool on = isActiveAndEnabled && _ready && _volume != null && strength > 1e-4f;
        Bounds b = _publishedBounds.size.sqrMagnitude > 1e-6f ? _publishedBounds : _bounds;
        Vector3 size = b.size;
        Vector3 inv = new(
            size.x > 1e-4f ? 1f / size.x : 0f,
            size.y > 1e-4f ? 1f / size.y : 0f,
            size.z > 1e-4f ? 1f / size.z : 0f);

        Shader.SetGlobalTexture(IdVolume, on ? _volume : _whiteVolume);
        Shader.SetGlobalVector(IdMin, on ? (Vector4)b.min : Vector4.zero);
        Shader.SetGlobalVector(IdInvSize, on ? (Vector4)inv : Vector4.zero);
        Shader.SetGlobalVector(IdParams, new Vector4(strength, localLightMul, minOcclusion, on ? 1f : 0f));
    }

    static void PushDisabledGlobals()
    {
        EnsureWhiteVolume();
        Shader.SetGlobalTexture(IdVolume, _whiteVolume);
        Shader.SetGlobalVector(IdMin, Vector4.zero);
        Shader.SetGlobalVector(IdInvSize, Vector4.zero);
        Shader.SetGlobalVector(IdParams, new Vector4(0f, 0f, 1f, 0f));
    }

    static void EnsureWhiteVolume()
    {
        if (_whiteVolume != null)
            return;
        _whiteVolume = new Texture3D(1, 1, 1, TextureFormat.RHalf, false)
        {
            name = "GTA AO White",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        _whiteVolume.SetPixel(0, 0, 0, Color.white);
        _whiteVolume.Apply(false, true);
    }

    void EnsureRayDirections()
    {
        int want = Mathf.Clamp(rayCount, 4, 32);
        if (_rayDirs != null && _rayDirs.Length >= want)
            return;
        _rayDirs = BuildHemisphereDirections(32);
    }

    static Vector3[] BuildHemisphereDirections(int count)
    {
        // Upper-hemisphere + some downward for cavities (GTA-ish contact AO).
        var dirs = new Vector3[count];
        int i = 0;
        // Fibonacci sphere, keep mostly y>=-0.2
        const float golden = 2.399963229728653f;
        for (int n = 0; n < count * 3 && i < count; n++)
        {
            float t = (n + 0.5f) / (count * 3f);
            float y = 1f - 2f * t;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float phi = n * golden;
            var d = new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r);
            if (d.y < -0.25f)
                continue;
            dirs[i++] = d.normalized;
        }

        while (i < count)
        {
            dirs[i] = Vector3.up;
            i++;
        }

        return dirs;
    }

    int ComputeSettingsHash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + resolution;
            h = h * 31 + rayCount;
            h = h * 31 + maxRayDistance.GetHashCode();
            h = h * 31 + rayBias.GetHashCode();
            h = h * 31 + minOcclusion.GetHashCode();
            h = h * 31 + (manualBounds ? 1 : 0);
            h = h * 31 + manualWorldBounds.GetHashCode();
            h = h * 31 + boundsPadding.GetHashCode();
            h = h * 31 + rayMask.value;
            return h;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.25f);
        Bounds b = manualBounds ? manualWorldBounds : _bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
