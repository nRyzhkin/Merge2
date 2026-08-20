using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>
/// Runtime sun-shadow cache for GTA shaders: orthographic depth atlas around the camera.
/// Heavy work is hard-capped to <see cref="CpuBudgetMs"/> per frame.
/// Published atlas/matrix stay stable while a new one builds; swaps crossfade.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("GTA/Shadow Cache")]
public sealed class GTAShadowCache : MonoBehaviour
{
    public const float DefaultCpuBudgetMs = 1.5f;

    static readonly int IdShadowMap = Shader.PropertyToID("_GTA_ShadowMap");
    static readonly int IdShadowMapPrev = Shader.PropertyToID("_GTA_ShadowMapPrev");
    static readonly int IdWorldToShadow = Shader.PropertyToID("_GTA_WorldToShadow");
    static readonly int IdWorldToShadowPrev = Shader.PropertyToID("_GTA_WorldToShadowPrev");
    static readonly int IdShadowParams = Shader.PropertyToID("_GTA_ShadowParams");
    static readonly int IdShadowSoftParams = Shader.PropertyToID("_GTA_ShadowSoftParams");
    static readonly int IdShadowBlend = Shader.PropertyToID("_GTA_ShadowBlend");
    static readonly int IdShadowCenter = Shader.PropertyToID("_GTA_ShadowCenter");
    static readonly int IdShadowNear = Shader.PropertyToID("_GTA_ShadowNear");
    static readonly int IdShadowFar = Shader.PropertyToID("_GTA_ShadowFar");
    static readonly int IdBaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int IdMainTex = Shader.PropertyToID("_MainTex");
    static readonly int IdCutoff = Shader.PropertyToID("_Cutoff");
    static readonly int IdGtaSunDirection = Shader.PropertyToID("_GTA_SunDirection");
    static readonly int IdGtaWeather = Shader.PropertyToID("_GTA_Weather");

    enum Phase : byte
    {
        Idle,
        Collect,
        Clear,
        Draw,
        Publish
    }

    [Header("Capture")]
    [SerializeField] int resolution = 512;
    [SerializeField] float coverageRadius = 90f;
    [SerializeField] float heightPadding = 80f;
    [SerializeField] float moveRebuildDistance = 28f;
    [SerializeField] LayerMask casterMask = ~0;
    [SerializeField] bool includeInactiveInHierarchy;
    [Tooltip("Foliage casts solid mesh silhouettes (no alpha cutout). Avoids phantom mask blotches in a coarse atlas.")]
    [SerializeField] bool foliageCastOpaque = true;
    [Tooltip("Optional roots to scan. Empty = all loaded scene roots.")]
    [SerializeField] Transform[] collectRoots;

    [Header("Budget")]
    [SerializeField, Range(0.25f, 4f)] float cpuBudgetMs = DefaultCpuBudgetMs;
    [SerializeField, Range(1, 128)] int maxCollectStepsPerFrame = 48;
    [SerializeField, Range(1, 64)] int maxDrawsPerFrame = 16;

    [Header("Look")]
    [SerializeField, Range(0f, 1f)] float shadowStrength = 0.55f;
    [Tooltip("Weather 0 → full strength, Weather ≥ this → strength 0.")]
    [SerializeField, Range(0.1f, 1f)] float weatherKillAt = 0.5f;
    [SerializeField, Range(0.0001f, 0.05f)] float depthBias = 0.0035f;
    [Tooltip("Depth-space contact soften only (acne / peter-panning). Does NOT make silhouette soft. Keep low (~0.008–0.015).")]
    [SerializeField, Range(0.002f, 0.08f)] float softDepthWidth = 0.012f;
    [Tooltip("Penumbra width in shadow-map texels (clear weather). 1.0–2.0 = smooth soft edge. Above ~3 looks blotchy.")]
    [SerializeField, Range(0.5f, 3.5f)] float softFilterRadiusClear = 1.5f;
    [Tooltip("Penumbra width in shadow-map texels when overcast. Keep ≤3.5 — larger values were capped in the shader.")]
    [SerializeField, Range(0.5f, 3.5f)] float softFilterRadiusOvercast = 2.5f;
    [SerializeField, Range(0.05f, 0.45f)] float edgeFade = 0.2f;
    [Tooltip("Crossfade duration when a new atlas is published.")]
    [SerializeField, Range(0.05f, 5f)] float blendDuration = 1f;
    [SerializeField] bool disableUrpSunShadows = true;

    [Header("Debug")]
    [SerializeField] bool logPhaseTransitions;
    [Tooltip("Log caster breakdown each publish (foliage / alpha-clip). Off by default — very noisy.")]
    [SerializeField] bool logCasterDiagnostics;

    readonly List<MeshRenderer> _casters = new(256);
    readonly Stack<Transform> _walk = new(128);
    readonly Stopwatch _clock = new();
    readonly List<Material> _matScratch = new(4);
    MaterialPropertyBlock _depthMpb;

    RenderTexture _readRt;
    RenderTexture _writeRt;
    RenderTexture _prevRt;
    Material _depthMat;
    CommandBuffer _cmd;

    Phase _phase = Phase.Idle;
    int _drawIndex;
    bool _hasPublished;
    bool _rebuildRequested = true;
    float _blend = 1f;
    float _lastRealtime;

    // Build-only (must not affect sampling until publish).
    Vector3 _buildCenter;
    Vector3 _buildSunDir = Vector3.down;
    Matrix4x4 _buildWorldToShadow;
    Matrix4x4 _pendingView;
    Matrix4x4 _pendingProj;
    float _buildNear;
    float _buildFar;
    float _buildOrthoHalf;

    // Published sampling state.
    Matrix4x4 _pubWorldToShadow = Matrix4x4.identity;
    Matrix4x4 _prevWorldToShadow = Matrix4x4.identity;
    Vector3 _pubCenter;
    Vector3 _pubSunDir = Vector3.down;
    float _pubNear = 0.5f;
    float _pubFar = 100f;
    float _pubOrthoHalf = 90f;

    Vector3 _lastBuildCameraPos;
    Vector3 _lastBuildSunDir = Vector3.down;
    Light _cachedSun;

    void OnEnable()
    {
        EnsureResources();
        _rebuildRequested = true;
        _phase = Phase.Idle;
        _blend = 1f;
        _lastRealtime = Time.realtimeSinceStartup;
        PushDisabledGlobals();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.update += EditorTick;
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        _rebuildRequested = true;
    }
#endif

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
        PushDisabledGlobals();
        ReleaseResources();
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
        ReleaseResources();
    }

    void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        TickFrame(playMode: true);
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (Application.isPlaying || this == null || !isActiveAndEnabled)
            return;

        TickFrame(playMode: false);
    }
#endif

    void TickFrame(bool playMode)
    {
        EnsureResources();
        TickBlend();

        if (playMode || (_phase == Phase.Idle && _blend >= 1f))
            MaybeRequestRebuild();

        PumpBudgeted();
        PublishSamplingGlobals();
    }

    void TickBlend()
    {
        float now = Time.realtimeSinceStartup;
        float dt = Mathf.Clamp(now - _lastRealtime, 0f, 0.25f);
        _lastRealtime = now;

        if (_blend >= 1f)
        {
            RecyclePrevIfReady();
            return;
        }

        float dur = Mathf.Max(0.05f, blendDuration);
        _blend = Mathf.MoveTowards(_blend, 1f, dt / dur);
        if (_blend >= 1f)
            RecyclePrevIfReady();
    }

    static float BlendToShader(float linearBlend) =>
        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(linearBlend));

    void RecyclePrevIfReady()
    {
        if (_prevRt == null || _blend < 1f)
            return;

        // Prefer recycling the faded-out atlas as the next write target.
        if (_writeRt == null)
        {
            _writeRt = _prevRt;
            _prevRt = null;
            return;
        }

        _prevRt.Release();
        DestroyImmediate(_prevRt);
        _prevRt = null;
    }

    void MaybeRequestRebuild()
    {
        // Don't start a new capture mid-crossfade (keeps RT count at 2 and avoids flicker).
        if (_blend < 1f || _phase != Phase.Idle)
            return;

        Transform cam = ResolveCamera();
        if (cam == null)
            return;

        Vector3 sunDir = ResolveSunDir();
        Vector3 camPos = cam.position;

        if (_rebuildRequested)
            return;

        const float sunAngleRebuildDegrees = 0.3f;
        float moveSq = (camPos - _lastBuildCameraPos).sqrMagnitude;
        float moveThreshold = moveRebuildDistance * moveRebuildDistance;
        float sunDot = Vector3.Dot(sunDir, _lastBuildSunDir);
        float sunDeg = sunDot >= 1f ? 0f : Mathf.Acos(Mathf.Clamp(sunDot, -1f, 1f)) * Mathf.Rad2Deg;

        if (moveSq >= moveThreshold || sunDeg >= sunAngleRebuildDegrees || !_hasPublished)
            _rebuildRequested = true;
    }

    void PumpBudgeted()
    {
        _clock.Restart();
        double budget = Mathf.Max(0.2f, cpuBudgetMs);

        if (_phase == Phase.Idle)
        {
            if (!_rebuildRequested || _blend < 1f)
                return;
            BeginRebuild();
        }

        while (_clock.Elapsed.TotalMilliseconds < budget)
        {
            switch (_phase)
            {
                case Phase.Collect:
                    StepCollect(budget);
                    break;
                case Phase.Clear:
                    StepClear();
                    break;
                case Phase.Draw:
                    StepDraw(budget);
                    break;
                case Phase.Publish:
                    StepPublish();
                    return;
                default:
                    return;
            }

            if (_phase == Phase.Idle)
                return;
        }
    }

    void BeginRebuild()
    {
        _rebuildRequested = false;
        Transform cam = ResolveCamera();
        if (cam == null)
        {
            _phase = Phase.Idle;
            return;
        }

        _buildSunDir = ResolveSunDir();
        _buildCenter = cam.position;
        _lastBuildCameraPos = _buildCenter;
        _lastBuildSunDir = _buildSunDir;

        _buildOrthoHalf = Mathf.Max(8f, coverageRadius);
        float height = Mathf.Max(20f, heightPadding);
        _buildNear = 0.5f;
        _buildFar = height * 2f + _buildOrthoHalf * 2f;

        _casters.Clear();
        _walk.Clear();
        SeedWalkRoots();
        _drawIndex = 0;
        _phase = Phase.Collect;
        if (logPhaseTransitions)
            Debug.Log("[GTAShadowCache] Collect start");
    }

    void SeedWalkRoots()
    {
        if (collectRoots != null && collectRoots.Length > 0)
        {
            for (int i = 0; i < collectRoots.Length; i++)
            {
                if (collectRoots[i] != null)
                    _walk.Push(collectRoots[i]);
            }

            return;
        }

        var scene = gameObject.scene;
        if (!scene.IsValid())
            return;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null)
                _walk.Push(roots[i].transform);
        }
    }

    void StepCollect(double budget)
    {
        int steps = 0;
        Bounds captureBounds = BuildCaptureBounds();

        while (_walk.Count > 0
               && steps < maxCollectStepsPerFrame
               && _clock.Elapsed.TotalMilliseconds < budget)
        {
            steps++;
            Transform t = _walk.Pop();
            if (t == null)
                continue;

            if (!includeInactiveInHierarchy && !t.gameObject.activeInHierarchy)
                continue;

            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null && IsEligibleCaster(mr, captureBounds))
                _casters.Add(mr);

            int childCount = t.childCount;
            for (int i = 0; i < childCount; i++)
                _walk.Push(t.GetChild(i));
        }

        if (_walk.Count == 0)
        {
            _phase = Phase.Clear;
            if (logPhaseTransitions)
                Debug.Log($"[GTAShadowCache] Collect done, casters={_casters.Count}");
        }
    }

    void StepClear()
    {
        EnsureWriteRt();
        BuildLightMatrices(
            _buildCenter,
            _buildSunDir,
            _buildOrthoHalf,
            _buildNear,
            _buildFar,
            out _pendingView,
            out _pendingProj,
            out _buildWorldToShadow);

        // Depth pass needs near/far globals; sampling globals are restored later this frame.
        Shader.SetGlobalFloat(IdShadowNear, _buildNear);
        Shader.SetGlobalFloat(IdShadowFar, _buildFar);

        _cmd.Clear();
        _cmd.SetRenderTarget(_writeRt);
        _cmd.ClearRenderTarget(true, true, Color.white);
        Graphics.ExecuteCommandBuffer(_cmd);
        _drawIndex = 0;
        _phase = Phase.Draw;
        if (logPhaseTransitions)
            Debug.Log("[GTAShadowCache] Draw start");
    }

    void StepDraw(double budget)
    {
        EnsureWriteRt();

        int draws = 0;
        _cmd.Clear();
        _cmd.SetRenderTarget(_writeRt);
        _cmd.SetViewProjectionMatrices(_pendingView, _pendingProj);
        Shader.SetGlobalFloat(IdShadowNear, _buildNear);
        Shader.SetGlobalFloat(IdShadowFar, _buildFar);

        while (_drawIndex < _casters.Count
               && draws < maxDrawsPerFrame
               && _clock.Elapsed.TotalMilliseconds < budget)
        {
            MeshRenderer mr = _casters[_drawIndex++];
            if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy)
                continue;

            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                continue;

            Mesh mesh = mf.sharedMesh;
            int subMeshCount = mesh.subMeshCount;
            Matrix4x4 matrix = mr.localToWorldMatrix;
            bool foliage = foliageCastOpaque && IsFoliageCaster(mr);

            // DrawMesh + MPB: CommandBuffer does NOT snapshot Material property edits between
            // DrawRenderer calls, so multi-material buildings must not share one Prepare().
            if (!mr.isPartOfStaticBatch)
            {
                for (int s = 0; s < subMeshCount; s++)
                {
                    FillDepthPropertyBlock(mr, s, foliage, out _, out _, out _);
                    _cmd.DrawMesh(mesh, matrix, _depthMat, s, 0, _depthMpb);
                }
            }
            else
            {
                // Static-batched meshes need DrawRenderer; flush so each slot's state sticks.
                for (int s = 0; s < subMeshCount; s++)
                {
                    FillDepthPropertyBlock(mr, s, foliage, out Texture map, out Vector4 st, out float cutoff);
                    _depthMat.SetTexture(IdBaseMap, map);
                    _depthMat.SetVector("_BaseMap_ST", st);
                    _depthMat.SetFloat(IdCutoff, cutoff);
                    _cmd.DrawRenderer(mr, _depthMat, s, 0);
                    Graphics.ExecuteCommandBuffer(_cmd);
                    _cmd.Clear();
                    _cmd.SetRenderTarget(_writeRt);
                    _cmd.SetViewProjectionMatrices(_pendingView, _pendingProj);
                }
            }

            draws++;
        }

        if (draws > 0)
            Graphics.ExecuteCommandBuffer(_cmd);

        if (_drawIndex >= _casters.Count)
            _phase = Phase.Publish;
    }

    void StepPublish()
    {
        // Tighter than sunAngleRebuildDegrees — sun-driven rebuilds must crossfade, not swap in place.
        const float sameCaptureSunDotMin = 0.9999985f;
        bool sameCapture = _hasPublished
            && (_buildCenter - _pubCenter).sqrMagnitude < 0.25f
            && Vector3.Dot(_buildSunDir, _pubSunDir) > sameCaptureSunDotMin
            && Mathf.Abs(_buildOrthoHalf - _pubOrthoHalf) < 0.1f;

        if (_hasPublished && _readRt != null)
        {
            if (sameCapture)
            {
                // Near-identical capture: swap in place — crossfade of noisy foliage atlases looks like phantoms.
                RenderTexture old = _readRt;
                _readRt = _writeRt;
                _writeRt = old;
                _blend = 1f;
                if (_prevRt != null && _prevRt != _readRt && _prevRt != _writeRt)
                {
                    _prevRt.Release();
                    DestroyImmediate(_prevRt);
                    _prevRt = null;
                }
            }
            else
            {
                // Crossfade: keep old atlas+matrix as prev, show new as current.
                if (_prevRt != null && _prevRt != _writeRt && _prevRt != _readRt)
                {
                    _prevRt.Release();
                    DestroyImmediate(_prevRt);
                }

                _prevRt = _readRt;
                _prevWorldToShadow = _pubWorldToShadow;
                _readRt = _writeRt;
                _writeRt = null;
                _blend = 0f;
            }
        }
        else
        {
            // First publish — keep the unused buffer as the next write target.
            RenderTexture built = _writeRt;
            RenderTexture spare = _readRt;
            _readRt = built;
            _writeRt = (spare != null && spare != built) ? spare : null;
            _prevRt = null;
            _blend = 1f;
        }

        _pubWorldToShadow = _buildWorldToShadow;
        _pubCenter = _buildCenter;
        _pubSunDir = _buildSunDir;
        _pubNear = _buildNear;
        _pubFar = _buildFar;
        _pubOrthoHalf = _buildOrthoHalf;

        _hasPublished = true;
        _phase = Phase.Idle;
        ApplyUrpSunShadowPolicy();

        if (logPhaseTransitions)
            Debug.Log($"[GTAShadowCache] Published (blend={_blend:0.00}, sameCapture={sameCapture})");

        if (logCasterDiagnostics)
            LogCasterDiagnostics();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneView.RepaintAll();
#endif
    }

    void LogCasterDiagnostics()
    {
        int total = _casters.Count;
        int nullOrDead = 0;
        int foliageNamed = 0;
        int underForiageRoot = 0;
        int alphaClipUsed = 0;       // what depth pass actually clips
        int cutoutNoKeyword = 0;     // looks cutout but PrepareDepthMaterial would draw opaque
        int foliageOpaqueDepth = 0;
        int alphaClipNoTexture = 0;

        var foliageOpaqueSamples = new List<string>(12);
        var cutoutNoKwSamples = new List<string>(12);
        var foliageClipSamples = new List<string>(8);
        var shaderCounts = new Dictionary<string, int>(16);

        for (int i = 0; i < total; i++)
        {
            MeshRenderer mr = _casters[i];
            if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy)
            {
                nullOrDead++;
                continue;
            }

            string path = GetTransformPath(mr.transform);
            bool foliageLike = IsFoliageLikePath(path);
            if (foliageLike)
                foliageNamed++;
            if (path.IndexOf("FORIAGE", System.StringComparison.OrdinalIgnoreCase) >= 0)
                underForiageRoot++;

            InspectCasterMaterials(mr, out bool keywordClip, out bool looksCutout, out bool hasBaseMap, out string shaderName);

            if (!string.IsNullOrEmpty(shaderName))
            {
                shaderCounts.TryGetValue(shaderName, out int c);
                shaderCounts[shaderName] = c + 1;
            }

            bool depthWouldClip = keywordClip && hasBaseMap;
            if (depthWouldClip)
            {
                alphaClipUsed++;
                if (foliageLike && foliageClipSamples.Count < 8)
                    foliageClipSamples.Add($"{path} shader={shaderName}");
            }
            else
            {
                if (keywordClip && !hasBaseMap)
                    alphaClipNoTexture++;

                if (looksCutout && !keywordClip)
                {
                    cutoutNoKeyword++;
                    if (cutoutNoKwSamples.Count < 12)
                        cutoutNoKwSamples.Add($"{path} shader={shaderName}");
                }

                if (foliageLike)
                {
                    foliageOpaqueDepth++;
                    if (foliageOpaqueSamples.Count < 12)
                        foliageOpaqueSamples.Add($"{path} shader={shaderName} kwClip={keywordClip} cutout={looksCutout} tex={hasBaseMap}");
                }
            }
        }

        var sb = new StringBuilder(1024);
        sb.Append("[GTAShadowCache] DIAG publish frame=").Append(Time.frameCount);
        sb.Append(" casters=").Append(total);
        sb.Append(" dead=").Append(nullOrDead);
        sb.Append(" foliageLike=").Append(foliageNamed);
        sb.Append(" underFORIAGE=").Append(underForiageRoot);
        sb.Append(" depthAlphaClip=").Append(alphaClipUsed);
        sb.Append(" cutoutNoKeyword=").Append(cutoutNoKeyword);
        sb.Append(" foliageOpaqueDepth=").Append(foliageOpaqueDepth);
        sb.Append(" alphaNoTex=").Append(alphaClipNoTexture);
        sb.Append(" center=").Append(_buildCenter.ToString("F1"));
        sb.Append(" sun=").Append(_buildSunDir.ToString("F3"));
        sb.Append(" ortho=").Append(_buildOrthoHalf.ToString("F0"));
        sb.Append(" near/far=").Append(_buildNear.ToString("F1")).Append('/').Append(_buildFar.ToString("F1"));
        sb.Append(" res=").Append(_readRt != null ? _readRt.width : 0);
        sb.Append(" blend=").Append(_blend.ToString("F2"));
        Debug.Log(sb.ToString());

        if (foliageOpaqueSamples.Count > 0)
        {
            sb.Clear();
            sb.Append("[GTAShadowCache] DIAG foliage drawn as OPAQUE cards in depth atlas:\n");
            for (int i = 0; i < foliageOpaqueSamples.Count; i++)
                sb.Append("  • ").Append(foliageOpaqueSamples[i]).Append('\n');
            Debug.LogWarning(sb.ToString());
        }

        if (cutoutNoKwSamples.Count > 0)
        {
            sb.Clear();
            sb.Append("[GTAShadowCache] DIAG cutout-looking mats WITHOUT alpha keyword (depth draws solid):\n");
            for (int i = 0; i < cutoutNoKwSamples.Count; i++)
                sb.Append("  • ").Append(cutoutNoKwSamples[i]).Append('\n');
            Debug.LogWarning(sb.ToString());
        }

        if (foliageClipSamples.Count > 0)
        {
            sb.Clear();
            sb.Append("[GTAShadowCache] DIAG foliage with working alpha-clip in depth:\n");
            for (int i = 0; i < foliageClipSamples.Count; i++)
                sb.Append("  • ").Append(foliageClipSamples[i]).Append('\n');
            Debug.Log(sb.ToString());
        }

        if (shaderCounts.Count > 0)
        {
            sb.Clear();
            sb.Append("[GTAShadowCache] DIAG shaders among casters:");
            foreach (var kv in shaderCounts)
                sb.Append("\n  ").Append(kv.Value).Append("× ").Append(kv.Key);
            Debug.Log(sb.ToString());
        }
    }

    static bool IsFoliageLikePath(string path)
    {
        // Match path segments only — "streetwall" must not hit substring "tree".
        int start = 0;
        while (start <= path.Length)
        {
            int slash = path.IndexOf('/', start);
            int len = (slash < 0 ? path.Length : slash) - start;
            if (len > 0)
            {
                string seg = path.Substring(start, len);
                if (seg.IndexOf("foriage", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || seg.IndexOf("foliage", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || seg.StartsWith("grass", System.StringComparison.OrdinalIgnoreCase)
                    || seg.StartsWith("leaf", System.StringComparison.OrdinalIgnoreCase)
                    || seg.StartsWith("plant", System.StringComparison.OrdinalIgnoreCase)
                    || seg.StartsWith("tree", System.StringComparison.OrdinalIgnoreCase)
                    || seg.StartsWith("bush", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (slash < 0)
                break;
            start = slash + 1;
        }

        return false;
    }

    void InspectCasterMaterials(
        MeshRenderer mr,
        out bool keywordClip,
        out bool looksCutout,
        out bool hasBaseMap,
        out string shaderName)
    {
        keywordClip = false;
        looksCutout = false;
        hasBaseMap = false;
        shaderName = "?";

        mr.GetSharedMaterials(_matScratch);
        for (int i = 0; i < _matScratch.Count; i++)
        {
            Material src = _matScratch[i];
            if (src == null)
                continue;

            if (shaderName == "?" && src.shader != null)
                shaderName = src.shader.name;

            if (!hasBaseMap)
            {
                if (src.HasProperty(IdBaseMap) && src.GetTexture(IdBaseMap) != null)
                    hasBaseMap = true;
                else if (src.HasProperty(IdMainTex) && src.GetTexture(IdMainTex) != null)
                    hasBaseMap = true;
            }

            if (src.IsKeywordEnabled("_ALPHATEST_ON")
                || src.IsKeywordEnabled("_ALPHA_CLIP")
                || src.IsKeywordEnabled("_ALPHACLIP_ON"))
                keywordClip = true;

            if (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f)
            {
                looksCutout = true;
                // URP often stores clip in float; keyword may still be missing after conversion.
            }

            int q = src.renderQueue;
            if (q >= 2450 && q < 3000)
                looksCutout = true;

            string sn = src.shader != null ? src.shader.name : string.Empty;
            if (sn.IndexOf("Nature", System.StringComparison.OrdinalIgnoreCase) >= 0
                || sn.IndexOf("Foliage", System.StringComparison.OrdinalIgnoreCase) >= 0
                || sn.IndexOf("Grass", System.StringComparison.OrdinalIgnoreCase) >= 0
                || sn.IndexOf("Vegetation", System.StringComparison.OrdinalIgnoreCase) >= 0)
                looksCutout = true;
        }
    }

    static string GetTransformPath(Transform t)
    {
        if (t == null)
            return "<null>";
        var sb = new StringBuilder(t.name.Length * 4);
        sb.Append(t.name);
        Transform p = t.parent;
        int guard = 0;
        while (p != null && guard++ < 24)
        {
            sb.Insert(0, '/');
            sb.Insert(0, p.name);
            p = p.parent;
        }

        return sb.ToString();
    }

    void FillDepthPropertyBlock(
        MeshRenderer mr,
        int subMeshIndex,
        bool foliage,
        out Texture baseMap,
        out Vector4 st,
        out float cutoff)
    {
        _depthMpb ??= new MaterialPropertyBlock();
        mr.GetSharedMaterials(_matScratch);

        Material src = null;
        if (_matScratch.Count > 0)
        {
            int slot = Mathf.Clamp(subMeshIndex, 0, _matScratch.Count - 1);
            src = _matScratch[slot];
            if (src == null)
            {
                for (int i = _matScratch.Count - 1; i >= 0; i--)
                {
                    if (_matScratch[i] == null)
                        continue;
                    src = _matScratch[i];
                    break;
                }
            }
        }

        baseMap = Texture2D.whiteTexture;
        st = new Vector4(1f, 1f, 0f, 0f);
        cutoff = -0.1f; // opaque: never discard
        bool alphaClip = false;

        if (src != null)
        {
            Texture map = null;
            if (src.HasProperty(IdBaseMap))
                map = src.GetTexture(IdBaseMap);
            if (map == null && src.HasProperty(IdMainTex))
                map = src.GetTexture(IdMainTex);

            if (src.HasProperty(IdBaseMap))
                st = src.GetVector("_BaseMap_ST");
            else if (src.HasProperty("_MainTex_ST"))
                st = src.GetVector("_MainTex_ST");

            if (!foliage && (src.IsKeywordEnabled("_ALPHATEST_ON") || src.IsKeywordEnabled("_ALPHA_CLIP")))
                alphaClip = true;

            if (alphaClip && map != null)
            {
                baseMap = map;
                cutoff = src.HasProperty(IdCutoff) ? src.GetFloat(IdCutoff) : 0.5f;
            }
        }

        _depthMpb.Clear();
        _depthMpb.SetTexture(IdBaseMap, baseMap);
        _depthMpb.SetVector("_BaseMap_ST", st);
        _depthMpb.SetFloat(IdCutoff, cutoff);
    }

    void PrepareDepthMaterial(MeshRenderer mr, int subMeshIndex = 0)
    {
        FillDepthPropertyBlock(mr, subMeshIndex, foliageCastOpaque && IsFoliageCaster(mr), out _, out _, out _);
    }

    bool IsEligibleCaster(MeshRenderer mr, Bounds captureBounds)
    {
        if (!mr.enabled || mr.shadowCastingMode == ShadowCastingMode.Off)
            return false;
        if (((1 << mr.gameObject.layer) & casterMask.value) == 0)
            return false;
        if (!mr.bounds.Intersects(captureBounds))
            return false;
        var mf = mr.GetComponent<MeshFilter>();
        return mf != null && mf.sharedMesh != null;
    }

    bool IsFoliageCaster(MeshRenderer mr)
    {
        for (Transform t = mr.transform; t != null; t = t.parent)
        {
            string n = t.name;
            if (n.IndexOf("foriage", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("foliage", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        mr.GetSharedMaterials(_matScratch);
        for (int i = 0; i < _matScratch.Count; i++)
        {
            Material mat = _matScratch[i];
            if (mat == null || mat.shader == null)
                continue;
            string sn = mat.shader.name;
            if (sn.IndexOf("Foliage", System.StringComparison.OrdinalIgnoreCase) >= 0
                || sn.IndexOf("Nature", System.StringComparison.OrdinalIgnoreCase) >= 0
                || sn.IndexOf("/Grass", System.StringComparison.OrdinalIgnoreCase) >= 0
                || sn.IndexOf("Vegetation", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    Bounds BuildCaptureBounds()
    {
        float ext = _buildOrthoHalf + 4f;
        float hy = Mathf.Max(heightPadding, _buildOrthoHalf);
        return new Bounds(_buildCenter, new Vector3(ext * 2f, hy * 2f, ext * 2f));
    }

    static void BuildLightMatrices(
        Vector3 center,
        Vector3 sunDir,
        float orthoHalf,
        float nearPlane,
        float farPlane,
        out Matrix4x4 view,
        out Matrix4x4 proj,
        out Matrix4x4 worldToShadow)
    {
        Vector3 sun = sunDir.sqrMagnitude > 1e-6f ? sunDir.normalized : new Vector3(0.2f, 0.85f, 0.2f).normalized;
        Vector3 lightForward = -sun;
        Vector3 up = Mathf.Abs(Vector3.Dot(lightForward, Vector3.up)) > 0.94f ? Vector3.forward : Vector3.up;
        Quaternion rot = Quaternion.LookRotation(lightForward, up);
        Vector3 eye = center - lightForward * (farPlane * 0.45f);

        view = Matrix4x4.TRS(eye, rot, new Vector3(1f, 1f, -1f)).inverse;
        proj = Matrix4x4.Ortho(-orthoHalf, orthoHalf, -orthoHalf, orthoHalf, nearPlane, farPlane);

        float invRange = 1f / Mathf.Max(farPlane - nearPlane, 1e-4f);
        float invOrtho = 0.5f / orthoHalf;
        Matrix4x4 scaleBias = Matrix4x4.identity;
        scaleBias.m00 = invOrtho;
        scaleBias.m11 = invOrtho;
        scaleBias.m22 = -invRange;
        scaleBias.m03 = 0.5f;
        scaleBias.m13 = 0.5f;
        scaleBias.m23 = -nearPlane * invRange;
        worldToShadow = scaleBias * view;
    }

    void PublishSamplingGlobals()
    {
        if (!_hasPublished || _readRt == null)
        {
            PushDisabledGlobals();
            return;
        }

        float weather = Shader.GetGlobalFloat(IdGtaWeather);
        float weatherT = Mathf.Clamp01(weather / Mathf.Max(0.01f, weatherKillAt));
        float weatherMul = 1f - weatherT;
        float strength = shadowStrength * weatherMul;
        float filterRadius = Mathf.Lerp(softFilterRadiusClear, softFilterRadiusOvercast, weatherT);
        float texel = 1f / Mathf.Max(1, _readRt.width);
        bool blending = _prevRt != null && _blend < 1f;

        Shader.SetGlobalTexture(IdShadowMap, _readRt);
        Shader.SetGlobalMatrix(IdWorldToShadow, _pubWorldToShadow);
        Shader.SetGlobalTexture(IdShadowMapPrev, blending ? _prevRt : Texture2D.whiteTexture);
        Shader.SetGlobalMatrix(IdWorldToShadowPrev, blending ? _prevWorldToShadow : Matrix4x4.identity);
        Shader.SetGlobalFloat(IdShadowBlend, blending ? BlendToShader(_blend) : 1f);
        Shader.SetGlobalVector(IdShadowParams, new Vector4(depthBias, strength, edgeFade, strength > 1e-4f ? 1f : 0f));
        Shader.SetGlobalVector(IdShadowSoftParams, new Vector4(softDepthWidth, filterRadius, texel, 0f));
        Shader.SetGlobalVector(IdShadowCenter, new Vector4(_pubCenter.x, _pubCenter.y, _pubCenter.z, _pubOrthoHalf));
        Shader.SetGlobalFloat(IdShadowNear, _pubNear);
        Shader.SetGlobalFloat(IdShadowFar, _pubFar);
    }

    void PushDisabledGlobals()
    {
        Shader.SetGlobalVector(IdShadowParams, new Vector4(0f, 0f, 0.2f, 0f));
        Shader.SetGlobalVector(IdShadowSoftParams, new Vector4(0.02f, softFilterRadiusClear, 1f / 512f, 0f));
        Shader.SetGlobalVector(IdShadowCenter, Vector4.zero);
        Shader.SetGlobalMatrix(IdWorldToShadow, Matrix4x4.identity);
        Shader.SetGlobalMatrix(IdWorldToShadowPrev, Matrix4x4.identity);
        Shader.SetGlobalTexture(IdShadowMap, Texture2D.whiteTexture);
        Shader.SetGlobalTexture(IdShadowMapPrev, Texture2D.whiteTexture);
        Shader.SetGlobalFloat(IdShadowBlend, 1f);
        Shader.SetGlobalFloat(IdShadowNear, 0.5f);
        Shader.SetGlobalFloat(IdShadowFar, 100f);
    }

    void ApplyUrpSunShadowPolicy()
    {
        if (!disableUrpSunShadows)
            return;

        if (_cachedSun == null)
            _cachedSun = RenderSettings.sun;
        if (_cachedSun != null)
            _cachedSun.shadows = LightShadows.None;
    }

    static Transform ResolveCamera()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var sv = UnityEditor.SceneView.lastActiveSceneView;
            if (sv != null && sv.camera != null)
                return sv.camera.transform;
        }
#endif
        Camera cam = Camera.main;
        if (cam != null)
            return cam.transform;
        cam = FindAnyObjectByType<Camera>();
        return cam != null ? cam.transform : null;
    }

    static Vector3 ResolveSunDir()
    {
        Vector4 g = Shader.GetGlobalVector(IdGtaSunDirection);
        Vector3 d = new Vector3(g.x, g.y, g.z);
        if (d.sqrMagnitude < 1e-6f)
            d = new Vector3(0.2f, 0.85f, 0.2f);
        return d.normalized;
    }

    void EnsureResources()
    {
        int res = Mathf.ClosestPowerOfTwo(Mathf.Clamp(resolution, 256, 2048));
        if (_readRt != null && _readRt.width != res)
            ReleaseRts();

        if (_readRt == null)
            _readRt = CreateRt(res);
        if (_writeRt == null)
            _writeRt = CreateRt(res);

        if (_readRt != null)
            _readRt.filterMode = FilterMode.Bilinear;
        if (_writeRt != null)
            _writeRt.filterMode = FilterMode.Bilinear;
        if (_prevRt != null)
            _prevRt.filterMode = FilterMode.Bilinear;

        if (_depthMat == null)
        {
            Shader shader = Shader.Find("Hidden/GTA/ShadowCacheDepth");
            if (shader == null)
            {
                Debug.LogError("[GTAShadowCache] Missing shader Hidden/GTA/ShadowCacheDepth");
                enabled = false;
                return;
            }

            _depthMat = new Material(shader) { name = "GTA ShadowCache Depth (Runtime)", hideFlags = HideFlags.HideAndDontSave };
        }

        _cmd ??= new CommandBuffer { name = "GTA Shadow Cache" };
    }

    void EnsureWriteRt()
    {
        if (_writeRt != null)
            return;

        if (_blend >= 1f && _prevRt != null)
        {
            _writeRt = _prevRt;
            _prevRt = null;
            return;
        }

        int res = _readRt != null ? _readRt.width : Mathf.ClosestPowerOfTwo(resolution);
        _writeRt = CreateRt(res);
    }

    static RenderTexture CreateRt(int res)
    {
        var rt = new RenderTexture(res, res, 16, RenderTextureFormat.RHalf)
        {
            name = $"GTAShadowMap_{res}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            autoGenerateMips = false,
            useMipMap = false,
            hideFlags = HideFlags.HideAndDontSave
        };
        rt.Create();
        return rt;
    }

    void ReleaseRts()
    {
        ReleaseRt(ref _readRt);
        ReleaseRt(ref _writeRt);
        ReleaseRt(ref _prevRt);
    }

    static void ReleaseRt(ref RenderTexture rt)
    {
        if (rt == null)
            return;
        rt.Release();
        DestroyImmediate(rt);
        rt = null;
    }

    void ReleaseResources()
    {
        ReleaseRts();
        if (_depthMat != null)
        {
            DestroyImmediate(_depthMat);
            _depthMat = null;
        }

        if (_cmd != null)
        {
            _cmd.Release();
            _cmd = null;
        }
    }
}
