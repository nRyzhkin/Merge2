using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Single world control point: TimeOfDay + Weather drive lighting, fog, sky and shader globals.
/// Curves/gradients are pre-baked for an artistic open-world look (not physically based).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("GTA/World Controller")]
public sealed class GTAWorldController : MonoBehaviour
{
    public const string SkyShaderName = "GTA/Sky";

    // Linear fog visibility (world units). X = start, Y = end.
    public static readonly Vector2 FogDistanceClear = new Vector2(100f, 500f);
    public static readonly Vector2 FogDistanceStorm = new Vector2(50f, 150f);

    [Range(0f, 1f)]
    [Tooltip("0 = midnight, 0.5 ≈ noon, 1 = next midnight")]
    public float TimeOfDay = 0.45f;

    [Range(0f, 1f)]
    [Tooltip("0 = clear hot day, 1 = heavy rain")]
    public float Weather = 0f;

    [Range(0.2f, 0.75f)]
    [Tooltip("Max material smoothness at Weather wetness = 1. Keep below ~0.55 to preserve normal-map detail.")]
    public float WetMaxSmoothness = 0.52f;

    // ------------------------------------------------------------------
    // Hidden artistic data (auto-filled, not shown in inspector)
    // ------------------------------------------------------------------

    [HideInInspector] [SerializeField] AnimationCurve _sunElevation;
    [HideInInspector] [SerializeField] AnimationCurve _sunIntensity;
    [HideInInspector] [SerializeField] AnimationCurve _fogDensity;
    [HideInInspector] [SerializeField] AnimationCurve _reflectionIntensity;
    [HideInInspector] [SerializeField] AnimationCurve _exposure;
    [HideInInspector] [SerializeField] AnimationCurve _horizonHazeAmount;

    // Weather response curves (evaluate weather 0..1 → multiplier / blend)
    [HideInInspector] [SerializeField] AnimationCurve _weatherWetness;
    [HideInInspector] [SerializeField] AnimationCurve _weatherSunMul;
    [HideInInspector] [SerializeField] AnimationCurve _weatherFogMul;
    [HideInInspector] [SerializeField] AnimationCurve _weatherSkyGrey;
    [HideInInspector] [SerializeField] AnimationCurve _weatherSatMul;
    [HideInInspector] [SerializeField] AnimationCurve _weatherHazeMul;

    [HideInInspector] [SerializeField] Light _sun;
    [HideInInspector] [SerializeField] Material _skyMaterial;
    [HideInInspector] [SerializeField] bool _initialized;

    float _lastAppliedTimeOfDay = -1f;
    float _lastAppliedWeather = -1f;
    float _lastAppliedWetMaxSmoothness = -1f;

    // Shader property IDs
    static readonly int IdTimeOfDay = Shader.PropertyToID("_GTA_TimeOfDay");
    static readonly int IdWeather = Shader.PropertyToID("_GTA_Weather");
    static readonly int IdWetness = Shader.PropertyToID("_GTA_Wetness");
    static readonly int IdWetMaxSmoothness = Shader.PropertyToID("_GTA_WetMaxSmoothness");
    static readonly int IdAmbient = Shader.PropertyToID("_GTA_AmbientColor");
    static readonly int IdSunColor = Shader.PropertyToID("_GTA_SunColor");
    static readonly int IdSunDirection = Shader.PropertyToID("_GTA_SunDirection");
    static readonly int IdHaloColor = Shader.PropertyToID("_GTA_HaloColor");
    static readonly int IdHaloIntensity = Shader.PropertyToID("_GTA_HaloIntensity");
    static readonly int IdGtaSkyExposure = Shader.PropertyToID("_GTA_SkyExposure");
    static readonly int IdSunSize = Shader.PropertyToID("_GTA_SunSize");
    static readonly int IdHaloSize = Shader.PropertyToID("_GTA_HaloSize");
    static readonly int IdFogColor = Shader.PropertyToID("_GTA_FogColor");
    static readonly int IdFogDensity = Shader.PropertyToID("_GTA_FogDensity");
    static readonly int IdFogStart = Shader.PropertyToID("_GTA_FogStart");
    static readonly int IdFogEnd = Shader.PropertyToID("_GTA_FogEnd");
    static readonly int IdFogEnabled = Shader.PropertyToID("_GTA_FogEnabled");
    static readonly int IdReflection = Shader.PropertyToID("_GTA_ReflectionIntensity");
    static readonly int IdGtaSkyZenith = Shader.PropertyToID("_GTA_SkyZenith");
    static readonly int IdGtaSkyHorizon = Shader.PropertyToID("_GTA_SkyHorizon");
    static readonly int IdGtaSkyGround = Shader.PropertyToID("_GTA_SkyGround");
    static readonly int IdGtaSkyHaze = Shader.PropertyToID("_GTA_SkyHaze");
    static readonly int IdGtaHorizonHaze = Shader.PropertyToID("_GTA_HorizonHaze");
    static readonly int IdGtaWeatherGrey = Shader.PropertyToID("_GTA_WeatherGrey");
    static readonly int IdGtaSunVisibility = Shader.PropertyToID("_GTA_SunVisibility");
    static readonly int IdGtaHaloVisibility = Shader.PropertyToID("_GTA_HaloVisibility");

    static readonly int IdSkySunVisibility = Shader.PropertyToID("_SunVisibility");
    static readonly int IdSkyHaloVisibility = Shader.PropertyToID("_HaloVisibility");

    static readonly int IdSkyZenith = Shader.PropertyToID("_ZenithColor");
    static readonly int IdSkyHorizon = Shader.PropertyToID("_HorizonColor");
    static readonly int IdSkyGround = Shader.PropertyToID("_GroundColor");
    static readonly int IdSkySunColor = Shader.PropertyToID("_SunColor");
    static readonly int IdSkyHaloColor = Shader.PropertyToID("_HaloColor");
    static readonly int IdSkyHazeColor = Shader.PropertyToID("_HazeColor");
    static readonly int IdSkySunDir = Shader.PropertyToID("_SunDirection");
    static readonly int IdSkyExposure = Shader.PropertyToID("_Exposure");
    static readonly int IdSkyHaze = Shader.PropertyToID("_HorizonHaze");
    static readonly int IdSkyWeatherGrey = Shader.PropertyToID("_WeatherGrey");
    static readonly int IdSkyHaloIntensity = Shader.PropertyToID("_HaloIntensity");

    void Reset()
    {
        EnsureInitialized(force: true);
    }

    void OnEnable()
    {
        EnsureInitialized(force: false);
        ApplyWorld();
    }

    void OnValidate()
    {
        EnsureInitialized(force: false);
        TimeOfDay = Mathf.Clamp01(TimeOfDay);
        Weather = Mathf.Clamp01(Weather);
        WetMaxSmoothness = Mathf.Clamp(WetMaxSmoothness, 0.2f, 0.75f);
        ApplyWorld();
    }

    void Update()
    {
        if (!NeedsApply())
            return;
        ApplyWorld();
    }

    bool NeedsApply()
    {
        return Mathf.Abs(TimeOfDay - _lastAppliedTimeOfDay) > 1e-5f
            || Mathf.Abs(Weather - _lastAppliedWeather) > 1e-5f
            || Mathf.Abs(WetMaxSmoothness - _lastAppliedWetMaxSmoothness) > 1e-5f;
    }

    void MarkApplied()
    {
        _lastAppliedTimeOfDay = TimeOfDay;
        _lastAppliedWeather = Weather;
        _lastAppliedWetMaxSmoothness = WetMaxSmoothness;
    }

    // ------------------------------------------------------------------
    // Apply
    // ------------------------------------------------------------------

    void ApplyWorld()
    {
        EnsureInitialized(force: false);
        EnsureSun();
        EnsureSkyMaterial();

        float t = Mathf.Repeat(TimeOfDay, 1f);
        float w = Mathf.Clamp01(Weather);

        float wetness = EvaluateSafe(_weatherWetness, w);
        float sunMul = EvaluateSafe(_weatherSunMul, w);
        float skyGrey = EvaluateSafe(_weatherSkyGrey, w);
        float satMul = EvaluateSafe(_weatherSatMul, w);
        float weatherHazeMul = EvaluateSafe(_weatherHazeMul, w);

        float golden = ComputeGoldenHour(t);
        float sunSatMul = satMul * Mathf.Lerp(1f, 0.78f, golden);

        // --- Time samples ---
        float elevationDeg = EvaluateSafe(_sunElevation, t);
        float baseSunIntensity = EvaluateSafe(_sunIntensity, t);
        float reflection = EvaluateSafe(_reflectionIntensity, t) * Mathf.Lerp(1f, 0.55f, skyGrey);
        float exposure = EvaluateSafe(_exposure, t) * Mathf.Lerp(1f, 0.82f, skyGrey);
        float hazeAmount = EvaluateSafe(_horizonHazeAmount, t) * weatherHazeMul;

        Color sunCol = AdjustSaturation(EvaluateRamp(SunColorKeys, t), sunSatMul);
        // No fill light — sun + local clusters only (Gamma look stays punchy without pastel ambient).
        Color ambient = Color.black;
        Color zenith = AdjustSaturation(EvaluateRamp(SkyZenithKeys, t), satMul);
        Color horizon = AdjustSaturation(EvaluateRamp(SkyHorizonKeys, t), satMul);
        Color ground = AdjustSaturation(EvaluateRamp(SkyGroundKeys, t), satMul);
        Color haze = AdjustSaturation(EvaluateRamp(HorizonHazeKeys, t), satMul);
        Color atmosphere = AdjustSaturation(EvaluateRamp(FogColorKeys, t), satMul);
        Color halo = AdjustSaturation(EvaluateRamp(SunHaloKeys, t), satMul);

        // Golden hour: soften direct sun tint only — sky palette stays on the ramps.
        if (golden > 1e-4f)
        {
            Color neutralSun = Hex(0xFFF6EE) * 1.05f;
            float preserve = Mathf.Clamp01(golden * 0.28f);
            sunCol = PreserveSurfaceTint(sunCol, neutralSun, preserve, 1.42f);
        }

        // Afternoon / sunset: gently pull horizon toward zenith (was a strong wash toward pastel).
        {
            float u = 0f;
            if (t >= 0.5f && t <= 0.66f)
                u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.66f, t)) * 0.35f;
            horizon = Color.Lerp(horizon, zenith, u);
            haze = Color.Lerp(haze, zenith, u * 0.5f);
        }

        float sunVisibility = WeatherSunVisibility(w);
        float haloVisibility = WeatherHaloVisibility(w);

        // Weather pushes sky toward grey; atmosphere tint greys the horizon band too.
        Color weatherFogTint = Color.Lerp(
            new Color(0.75f, 0.8f, 0.88f),
            new Color(0.35f, 0.38f, 0.42f),
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, w)));
        atmosphere = Color.Lerp(atmosphere, weatherFogTint, skyGrey * 0.85f);
        zenith = Color.Lerp(zenith, atmosphere * 0.9f, skyGrey);
        horizon = Color.Lerp(horizon, atmosphere, skyGrey * 0.75f);
        haze = Color.Lerp(haze, Color.white * 0.85f, skyGrey * 0.6f);

        float fogStart = Mathf.Lerp(FogDistanceClear.x, FogDistanceStorm.x, w);
        float fogEnd = Mathf.Lerp(FogDistanceClear.y, FogDistanceStorm.y, w);
        fogEnd = Mathf.Max(fogEnd, fogStart + 1f);

        // Tight, colored corona — not a huge white bloom.
        float haloIntensity = Mathf.Lerp(0.65f, 0.2f, skyGrey) * haloVisibility;
        Vector3 sunDir = SunDirectionFromTime(t, elevationDeg);
        Color fogDisplay = EvaluateHorizonFogColor(
            horizon, zenith, ground, haze, halo, sunDir, exposure, hazeAmount, skyGrey, haloIntensity, haloVisibility,
            HorizonSampleDirection(sunDir));

        float sunIntensity = baseSunIntensity * sunMul * sunVisibility;
        sunIntensity *= Mathf.Lerp(1f, 0.89f, golden);
        // Below horizon: fade the hard sun, keep soft moonlight (ambient is off).
        if (elevationDeg < 0f)
        {
            float elevFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-6f, 0f, elevationDeg));
            float moonFloor = 0.10f * sunMul * Mathf.Max(sunVisibility, 0.4f);
            sunIntensity = Mathf.Max(sunIntensity * elevFade, moonFloor);
        }

        // --- Directional light ---
        if (_sun != null)
        {
            _sun.transform.rotation = Quaternion.LookRotation(-sunDir);
            _sun.color = sunCol;
            // Allow true zero below horizon — even 0.001 intensity makes a mirror specular.
            _sun.intensity = Mathf.Max(0f, sunIntensity);
            // GTAShadowCache owns sun contact; keep URP cascades off for WebGL cost.
            if (_sun.shadows != LightShadows.None)
                _sun.shadows = LightShadows.None;
            _sun.shadowStrength = Mathf.Lerp(1f, 0.55f, skyGrey);
        }

        // --- Ambient / reflections ---
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = Mathf.Clamp01(reflection);

        // --- Fog (linear distances; color matched in shaders via GTASkyHorizon) ---
        bool fogActive = IsFogActiveForCurrentView();
        Shader.SetGlobalFloat(IdFogEnabled, fogActive ? 1f : 0f);

        if (fogActive)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(
                Mathf.Clamp01(fogDisplay.r),
                Mathf.Clamp01(fogDisplay.g),
                Mathf.Clamp01(fogDisplay.b),
                1f);
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }
        else
        {
            RenderSettings.fog = false;
        }

        // --- Sky material ---
        if (_skyMaterial != null)
        {
            _skyMaterial.SetColor(IdSkyZenith, zenith);
            _skyMaterial.SetColor(IdSkyHorizon, horizon);
            _skyMaterial.SetColor(IdSkyGround, ground);
            _skyMaterial.SetColor(IdSkySunColor, sunCol * 1.1f);
            _skyMaterial.SetColor(IdSkyHaloColor, halo);
            _skyMaterial.SetColor(IdSkyHazeColor, haze);
            _skyMaterial.SetVector(IdSkySunDir, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
            _skyMaterial.SetFloat(IdSkyExposure, exposure);
            _skyMaterial.SetFloat(IdSkyHaze, hazeAmount);
            _skyMaterial.SetFloat(IdSkyWeatherGrey, skyGrey);
            _skyMaterial.SetFloat(IdSkyHaloIntensity, haloIntensity);
            _skyMaterial.SetFloat(IdSkySunVisibility, sunVisibility);
            _skyMaterial.SetFloat(IdSkyHaloVisibility, haloVisibility);
            // Keep corona compact even if an old material asset still has a large Halo Size.
            if (_skyMaterial.HasProperty("_HaloSize"))
                _skyMaterial.SetFloat("_HaloSize", Mathf.Min(_skyMaterial.GetFloat("_HaloSize"), 0.08f));

            if (RenderSettings.skybox != _skyMaterial)
                RenderSettings.skybox = _skyMaterial;
        }

        // --- Shader globals ---
        Shader.SetGlobalFloat(IdTimeOfDay, t);
        Shader.SetGlobalFloat(IdWeather, w);
        Shader.SetGlobalFloat(IdWetness, wetness);
        Shader.SetGlobalFloat(IdWetMaxSmoothness, Mathf.Clamp(WetMaxSmoothness, 0.2f, 0.75f));
        Shader.SetGlobalColor(IdAmbient, ambient);
        Shader.SetGlobalColor(IdSunColor, sunCol);
        Shader.SetGlobalVector(IdSunDirection, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
        Shader.SetGlobalColor(IdHaloColor, halo);
        Shader.SetGlobalFloat(IdHaloIntensity, haloIntensity);
        Shader.SetGlobalFloat(IdGtaSkyExposure, exposure);
        float sunSize = 0.02f;
        float haloSize = 0.07f;
        if (_skyMaterial != null)
        {
            if (_skyMaterial.HasProperty("_SunSize"))
                sunSize = _skyMaterial.GetFloat("_SunSize");
            if (_skyMaterial.HasProperty("_HaloSize"))
                haloSize = _skyMaterial.GetFloat("_HaloSize");
        }
        Shader.SetGlobalFloat(IdSunSize, sunSize);
        Shader.SetGlobalFloat(IdHaloSize, haloSize);
        Shader.SetGlobalColor(IdFogColor, fogDisplay);
        Shader.SetGlobalFloat(IdFogStart, fogStart);
        Shader.SetGlobalFloat(IdFogEnd, fogEnd);
        Shader.SetGlobalFloat(IdFogDensity, 1f / (fogEnd - fogStart));
        Shader.SetGlobalFloat(IdReflection, reflection);
        Shader.SetGlobalColor(IdGtaSkyZenith, zenith);
        Shader.SetGlobalColor(IdGtaSkyHorizon, horizon);
        Shader.SetGlobalColor(IdGtaSkyGround, ground);
        Shader.SetGlobalColor(IdGtaSkyHaze, haze);
        Shader.SetGlobalFloat(IdGtaHorizonHaze, hazeAmount);
        Shader.SetGlobalFloat(IdGtaWeatherGrey, skyGrey);
        Shader.SetGlobalFloat(IdGtaSunVisibility, sunVisibility);
        Shader.SetGlobalFloat(IdGtaHaloVisibility, haloVisibility);

        MarkApplied();
    }

    bool IsFogActiveForCurrentView()
    {
#if UNITY_EDITOR
        return GTAWorldFogSettings.IsFogEnabledInEditorSceneView();
#else
        return true;
#endif
    }

#if UNITY_EDITOR
    /// <summary>Re-apply lighting/fog after editor-only display toggles change.</summary>
    public void EditorRefreshWorld()
    {
        _lastAppliedTimeOfDay = -1f;
        ApplyWorld();
    }
#endif

    /// <summary>Force-rebuild all artistic curves and gradients from code defaults.</summary>
    public void RebuildArtisticCurves()
    {
        EnsureInitialized(force: true);
        ApplyWorld();
    }

    static float WeatherSunVisibility(float weather)
    {
        if (weather <= 0.5f)
            return 1f;
        return 1f - Mathf.SmoothStep(0f, 1f, (weather - 0.5f) / 0.5f);
    }

    static float WeatherHaloVisibility(float weather)
    {
        if (weather <= 0.5f)
            return 1f;
        if (weather >= 0.8f)
            return 0f;
        return 1f - Mathf.SmoothStep(0f, 1f, (weather - 0.5f) / 0.3f);
    }

    static Vector3 SunDirectionFromTime(float t, float elevationDeg)
    {
        // Azimuth across the day; elevation is degrees above the horizon.
        float azimuth = Mathf.Lerp(-90f, 270f, t);
        return (Quaternion.Euler(-elevationDeg, azimuth, 0f) * Vector3.forward).normalized;
    }

    static float ComputeGoldenHour(float t)
    {
        float dawn = Mathf.SmoothStep(0.22f, 0.26f, t) * (1f - Mathf.SmoothStep(0.30f, 0.36f, t));
        float dusk = Mathf.SmoothStep(0.70f, 0.75f, t) * (1f - Mathf.SmoothStep(0.82f, 0.88f, t));
        return Mathf.Max(dawn, dusk);
    }

    /// <summary>
    /// Lerp warm HDR tint toward neutral daylight and cap peak HDR so albedo stays readable.
    /// </summary>
    static Color PreserveSurfaceTint(Color warm, Color neutral, float blend, float maxHdr)
    {
        if (blend <= 1e-4f)
            return warm;

        Color c = Color.Lerp(warm, neutral, blend);
        float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (peak > maxHdr)
            c *= maxHdr / peak;
        return c;
    }

    /// <summary>
    /// satMul 0 = grey, 1 = original chroma, &gt;1 punches saturation without raising luma.
    /// (Old Desaturate clamped satMul to 0..1 so clear-day boost never applied.)
    /// </summary>
    static Color AdjustSaturation(Color c, float satMul)
    {
        float g = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        float s = Mathf.Max(0f, satMul);
        return new Color(
            Mathf.Max(0f, g + (c.r - g) * s),
            Mathf.Max(0f, g + (c.g - g) * s),
            Mathf.Max(0f, g + (c.b - g) * s),
            c.a);
    }

    /// <summary>
    /// Matches GTAEvaluateHorizonSky in GTASkyHorizon.hlsl for a given view direction.
    /// </summary>
    static Color EvaluateHorizonFogColor(
        Color horizonHdr,
        Color zenithHdr,
        Color groundHdr,
        Color hazeHdr,
        Color haloHdr,
        Vector3 sunDir,
        float exposure,
        float horizonHaze,
        float weatherGrey,
        float haloIntensity,
        float haloVisibility,
        Vector3 viewDir)
    {
        Vector3 V = viewDir.normalized;
        Vector3 S = sunDir.normalized;

        float y = V.y;
        float belowHorizon = Mathf.Clamp01(-y * 3f);
        float horizonBlend = 1f - Mathf.Clamp01(Mathf.Abs(y) * 4.5f);
        float zenithBlend = Mathf.Clamp01(y * 2.35f);

        Vector3 sky = Vector3.Lerp(
            ToVec3(horizonHdr),
            ToVec3(zenithHdr),
            zenithBlend);
        sky = Vector3.Lerp(sky, ToVec3(horizonHdr), belowHorizon);

        float sunDot = Vector3.Dot(V, S);
        float towardSun = Mathf.Clamp01(sunDot);
        float antiSun = Mathf.Clamp01(-sunDot);

        float sunsetStrength = Mathf.Clamp01(1f - Mathf.Abs(S.y) * 2.2f);
        sunsetStrength *= Mathf.Clamp01(S.y * 6f + 0.85f);

        Vector3 coolSky = Vector3.Scale(ToVec3(zenithHdr), new Vector3(0.55f, 0.65f, 0.95f));
        sky = Vector3.Lerp(sky, coolSky, antiSun * antiSun * Mathf.Lerp(0.25f, 0.55f, sunsetStrength));

        float sunAzimuthWeight = towardSun * towardSun * 0.65f + 0.35f;
        float haze = Mathf.Pow(horizonBlend, 1.25f) * horizonHaze * sunAzimuthWeight;
        Vector3 hazeCol = Vector3.Lerp(ToVec3(hazeHdr), ToVec3(horizonHdr), sunsetStrength * 0.65f);
        sky = Vector3.Lerp(sky, hazeCol, Mathf.Clamp01(haze));

        float sunsetCore = Mathf.Pow(towardSun, 3.8f);
        Vector3 warm = ToVec3(haloHdr);
        float warmVis = haloVisibility;
        sky = Vector3.Lerp(sky, warm, sunsetCore * sunsetStrength * 0.35f * warmVis);
        sky += warm * (sunsetCore * sunsetStrength * 0.08f * warmVis);

        float glow = Mathf.Pow(towardSun, 4.5f);
        sky += warm * (glow * haloIntensity * 0.12f * Mathf.Clamp01(S.y * 3f + 0.4f) * warmVis);

        float grey = sky.x * 0.299f + sky.y * 0.587f + sky.z * 0.114f;
        float greyLift = Mathf.Lerp(0.85f, 1.15f, y * 0.5f + 0.5f);
        sky = Vector3.Lerp(sky, new Vector3(grey, grey, grey) * greyLift, weatherGrey);

        sky *= Mathf.Max(0.01f, exposure);
        return new Color(sky.x, sky.y, sky.z, 1f);
    }

    /// <summary>Horizon-band view along sun azimuth (matches in-shader fog direction).</summary>
    static Vector3 HorizonSampleDirection(Vector3 sunDir)
    {
        Vector3 sunFlat = new Vector3(sunDir.x, 0f, sunDir.z);
        if (sunFlat.sqrMagnitude < 1e-6f)
            return new Vector3(0f, 0.04f, 1f).normalized;
        sunFlat.Normalize();
        return new Vector3(sunFlat.x, 0.04f, sunFlat.z).normalized;
    }

    static Vector3 ToVec3(Color c) => new Vector3(c.r, c.g, c.b);

    static float EvaluateSafe(AnimationCurve curve, float t)
    {
        if (curve == null || curve.length == 0)
            return 0f;
        return curve.Evaluate(t);
    }

    // ------------------------------------------------------------------
    // References
    // ------------------------------------------------------------------

    void EnsureSun()
    {
        if (_sun != null)
            return;

        var lights = FindObjectsByType<Light>();
        Light best = null;
        foreach (var l in lights)
        {
            if (l == null || l.type != LightType.Directional)
                continue;
            if (best == null || l.intensity > best.intensity)
                best = l;
        }

        if (best == null)
        {
            var go = new GameObject("Directional Light (GTA)");
            go.transform.SetParent(transform, false);
            best = go.AddComponent<Light>();
            best.type = LightType.Directional;
            best.shadows = LightShadows.Soft;
        }

        _sun = best;
        RenderSettings.sun = _sun;
    }

    void EnsureSkyMaterial()
    {
        if (_skyMaterial != null && _skyMaterial.shader != null && _skyMaterial.shader.name == SkyShaderName)
            return;

        Shader skyShader = Shader.Find(SkyShaderName);
        if (skyShader == null)
            return;

#if UNITY_EDITOR
        const string path = GTAWorldLitePaths.SkyMaterial;
        _skyMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (_skyMaterial == null)
        {
            EnsureFolder(GTAWorldLitePaths.PackageRoot + "/Materials");
            _skyMaterial = new Material(skyShader) { name = "GTA_Sky" };
            AssetDatabase.CreateAsset(_skyMaterial, path);
            AssetDatabase.SaveAssets();
        }
#else
        _skyMaterial = new Material(skyShader) { name = "GTA_Sky_Runtime" };
#endif
        RenderSettings.skybox = _skyMaterial;
    }

#if UNITY_EDITOR
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = "Assets";
        string[] parts = path.Split('/');
        for (int i = 1; i < parts.Length; i++)
        {
            string next = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(parent, parts[i]);
            parent = next;
        }
    }
#endif

    // ------------------------------------------------------------------
    // Curve / gradient bootstrap
    // Time keys (fraction of 24h):
    //   00:00=0.000  04:30≈0.1875  07:00≈0.2917  12:00=0.500
    //   16:00≈0.6667 19:30=0.8125  22:00≈0.9167  24:00=1.000
    // ------------------------------------------------------------------

    void EnsureInitialized(bool force)
    {
        if (_initialized && !force && CurvesValid())
            return;

        BuildAllCurves();
        _initialized = true;
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    bool CurvesValid()
    {
        // Detect legacy wetness curve (materials were too wet by ~0.7).
        if (_weatherWetness == null || _weatherWetness.length < 2)
            return false;
        if (EvaluateSafe(_weatherWetness, 0.70f) > 0.12f)
            return false;

        // Detect gamma night crush (ambient off): midnight exposure must stay readable.
        if (_exposure == null || EvaluateSafe(_exposure, 0f) < 0.82f)
            return false;

        // Detect pre–rich-chroma midday (exposure peak ~1.05 washed the sky white).
        if (EvaluateSafe(_exposure, 0.5f) > 0.96f)
            return false;

        // Detect old clear-day sat curve that couldn't punch chroma (peak ≤ 1.15).
        if (_weatherSatMul == null || EvaluateSafe(_weatherSatMul, 0f) < 1.25f)
            return false;

        // Weather curves must Clamp — Loop makes Weather=1 evaluate as clear (t=0).
        if (_weatherSkyGrey != null && _weatherSkyGrey.postWrapMode == WrapMode.Loop)
            return false;

        // Soft moonlight floor for gamma nights.
        if (_sunIntensity == null || EvaluateSafe(_sunIntensity, 0f) < 0.08f)
            return false;

        return _sunElevation != null && _sunElevation.length > 1;
    }

    void BuildAllCurves()
    {
        _sunElevation = BuildSunElevation();
        _sunIntensity = BuildSunIntensity();
        _fogDensity = BuildFogDensity();
        _reflectionIntensity = BuildReflection();
        _exposure = BuildExposure();
        _horizonHazeAmount = BuildHorizonHazeAmount();

        _weatherWetness = BuildWeatherWetness();
        _weatherSunMul = BuildWeatherSunMul();
        _weatherFogMul = BuildWeatherFogMul();
        _weatherSkyGrey = BuildWeatherSkyGrey();
        _weatherSatMul = BuildWeatherSatMul();
        _weatherHazeMul = BuildWeatherHazeMul();
    }

    // ---- Time-of-day curves ----

    static AnimationCurve BuildSunElevation()
    {
        // Degrees above horizon. Deep night below, fast rise/set around dawn/dusk, flat day.
        var c = new AnimationCurve(
            Ease(-20f, 0.000f),
            Ease(-18f, 0.1875f),
            Ease(-2f, 0.229f),   // ~05:30
            Ease(12f, 0.2708f),  // ~06:30
            Ease(28f, 0.2917f),  // 07:00
            Ease(62f, 0.500f),   // noon
            Ease(38f, 0.6667f),  // 16:00
            Ease(18f, 0.740f),   // ~17:45
            Ease(4f, 0.8125f),   // 19:30
            Ease(-8f, 0.875f),   // 21:00
            Ease(-18f, 0.9167f),
            Ease(-20f, 1.000f)
        );
        FlattenTangents(c);
        return c;
    }

    static AnimationCurve BuildSunIntensity()
    {
        // Soft moonlight floor so Gamma nights stay readable without ambient.
        var c = new AnimationCurve(
            Ease(0.12f, 0.000f),
            Ease(0.14f, 0.1875f),
            Ease(0.35f, 0.240f),
            Ease(0.95f, 0.280f),
            Ease(1.25f, 0.2917f),
            Ease(1.35f, 0.500f),
            Ease(1.25f, 0.6667f),
            Ease(1.15f, 0.740f),
            Ease(0.55f, 0.8125f),
            Ease(0.18f, 0.875f),
            Ease(0.14f, 0.9167f),
            Ease(0.12f, 1.000f)
        );
        FlattenTangents(c);
        return c;
    }

    static AnimationCurve BuildFogDensity()
    {
        // Slightly denser at night / dawn / dusk for mood; clear mid-day.
        var c = new AnimationCurve(
            Ease(0.0045f, 0.000f),
            Ease(0.0040f, 0.1875f),
            Ease(0.0032f, 0.2708f),
            Ease(0.0016f, 0.350f),
            Ease(0.0012f, 0.500f),
            Ease(0.0014f, 0.6667f),
            Ease(0.0024f, 0.780f),
            Ease(0.0035f, 0.850f),
            Ease(0.0045f, 1.000f)
        );
        FlattenTangents(c);
        return c;
    }

    static AnimationCurve BuildReflection()
    {
        var c = new AnimationCurve(
            Ease(0.55f, 0.000f),
            Ease(0.6f, 0.250f),
            Ease(1.0f, 0.350f),
            Ease(1.0f, 0.650f),
            Ease(0.75f, 0.800f),
            Ease(0.6f, 0.900f),
            Ease(0.55f, 1.000f)
        );
        FlattenTangents(c);
        return c;
    }

    static AnimationCurve BuildExposure()
    {
        // Gamma + no ambient: nights need headroom; midday stays under 1 for blue sky.
        var c = new AnimationCurve(
            Ease(0.88f, 0.000f),
            Ease(0.90f, 0.200f),
            Ease(0.92f, 0.290f),
            Ease(0.94f, 0.500f),
            Ease(0.92f, 0.700f),
            Ease(0.90f, 0.810f),
            Ease(0.88f, 0.900f),
            Ease(0.88f, 1.000f)
        );
        FlattenTangents(c);
        return c;
    }

    static AnimationCurve BuildHorizonHazeAmount()
    {
        var c = new AnimationCurve(
            Ease(0.28f, 0.000f),
            Ease(0.70f, 0.250f), // dawn haze
            Ease(0.38f, 0.400f),
            Ease(0.28f, 0.550f),
            Ease(0.62f, 0.760f), // sunset haze
            Ease(0.42f, 0.850f),
            Ease(0.28f, 1.000f)
        );
        FlattenTangents(c);
        return c;
    }

    // ---- Weather curves ----

    static AnimationCurve BuildWeatherWetness()
    {
        // Materials stay dry until ~0.75; ramp to max only in the last fifth.
        var c = new AnimationCurve(
            Ease(0.00f, 0.00f),
            Ease(0.00f, 0.60f),
            Ease(0.03f, 0.75f), // air damp — materials still nearly dry
            Ease(0.40f, 0.90f),
            Ease(1.00f, 1.00f)
        );
        FlattenTangents(c, WrapMode.ClampForever);
        return c;
    }

    static AnimationCurve BuildWeatherSunMul()
    {
        var c = new AnimationCurve(
            Ease(1.00f, 0.00f),
            Ease(1.00f, 0.30f),
            Ease(0.95f, 0.50f),
            Ease(0.80f, 0.60f),
            Ease(0.55f, 0.70f),
            Ease(0.30f, 0.80f),
            Ease(0.08f, 1.00f)
        );
        FlattenTangents(c, WrapMode.ClampForever);
        return c;
    }

    static AnimationCurve BuildWeatherFogMul()
    {
        // Mild air haze 0.6–0.75 without heavy storm fog yet.
        var c = new AnimationCurve(
            Ease(0.85f, 0.00f),
            Ease(1.00f, 0.30f),
            Ease(1.15f, 0.50f),
            Ease(1.45f, 0.60f),
            Ease(1.80f, 0.75f),
            Ease(2.60f, 0.90f),
            Ease(4.20f, 1.00f)
        );
        FlattenTangents(c, WrapMode.ClampForever);
        return c;
    }

    static AnimationCurve BuildWeatherSkyGrey()
    {
        var c = new AnimationCurve(
            Ease(0.00f, 0.00f),
            Ease(0.00f, 0.30f),
            Ease(0.05f, 0.50f),
            Ease(0.12f, 0.60f),
            Ease(0.22f, 0.75f),
            Ease(0.55f, 0.90f),
            Ease(1.00f, 1.00f)
        );
        FlattenTangents(c, WrapMode.ClampForever);
        return c;
    }

    static AnimationCurve BuildWeatherSatMul()
    {
        // Clear day punches chroma; storms still grey out.
        var c = new AnimationCurve(
            Ease(1.35f, 0.00f),
            Ease(1.30f, 0.30f),
            Ease(1.15f, 0.50f),
            Ease(1.00f, 0.60f),
            Ease(0.85f, 0.75f),
            Ease(0.55f, 0.90f),
            Ease(0.30f, 1.00f)
        );
        FlattenTangents(c, WrapMode.ClampForever);
        return c;
    }

    static AnimationCurve BuildWeatherHazeMul()
    {
        var c = new AnimationCurve(
            Ease(0.70f, 0.00f),
            Ease(0.85f, 0.30f),
            Ease(1.20f, 0.50f),
            Ease(1.40f, 0.60f),
            Ease(1.15f, 0.75f), // clouds kill warm haze
            Ease(0.80f, 1.00f)
        );
        FlattenTangents(c, WrapMode.ClampForever);
        return c;
    }

    // ---- Color ramps (Unity Gradient caps at 8 keys — use custom evaluator) ----

    static readonly GradKey[] SunColorKeys =
    {
        K(0.0000f, Hex(0x5A78C8), 0.40f),
        K(0.1875f, Hex(0x6A82D8), 0.50f),
        K(0.2290f, Hex(0xB070E0), 0.95f),
        K(0.2500f, Hex(0xFF70B0), 1.28f),
        K(0.2700f, Hex(0xFFA250), 1.45f),
        K(0.2917f, Hex(0xFFCC70), 1.30f),
        K(0.3400f, Hex(0xFFE8C8), 1.12f), // leave golden dawn
        K(0.4200f, Hex(0xFFF6EE), 1.08f), // morning → neutral daylight
        K(0.5000f, Hex(0xFFF8F2), 1.05f), // noon: near-white, tiny warmth
        K(0.5800f, Hex(0xFFF4EC), 1.06f),
        K(0.6667f, Hex(0xFFE4C0), 1.10f), // late afternoon warms again
        K(0.7400f, Hex(0xFF893C), 1.65f),
        K(0.8000f, Hex(0xFF4C28), 1.50f),
        K(0.8500f, Hex(0x6070C8), 0.75f),
        K(0.9167f, Hex(0x5A78C8), 0.45f),
        K(1.0000f, Hex(0x5A78C8), 0.40f)
    };

    static readonly GradKey[] AmbientColorKeys =
    {
        K(0.0000f, Hex(0x1C2C58), 0.80f),
        K(0.1875f, Hex(0x223868), 0.85f),
        K(0.2500f, Hex(0x6850A8), 0.90f),
        K(0.2800f, Hex(0xC07058), 0.90f),
        K(0.3200f, Hex(0x5A98E8), 0.95f),
        K(0.5000f, Hex(0x4A9AE8), 1.05f), // richer noon fill, not pastel
        K(0.7000f, Hex(0x6890D8), 1.00f),
        K(0.7800f, Hex(0xE07840), 0.95f),
        K(0.8300f, Hex(0x405898), 0.85f),
        K(0.9167f, Hex(0x1C2848), 0.80f),
        K(1.0000f, Hex(0x1C2C58), 0.80f)
    };

    static readonly GradKey[] SkyZenithKeys =
    {
        K(0.0000f, Hex(0x0C1C38), 0.85f),
        K(0.1875f, Hex(0x0E2040), 0.88f),
        K(0.2400f, Hex(0x142868), 0.90f),
        K(0.2700f, Hex(0x2858C8), 1.00f),
        K(0.3200f, Hex(0x1E6FE8), 1.20f),
        K(0.5000f, Hex(0x1870F0), 1.25f),
        K(0.7000f, Hex(0x2868E0), 1.15f),
        K(0.7600f, Hex(0x3858B0), 0.95f),
        K(0.8200f, Hex(0x1C3C78), 0.85f),
        K(0.9000f, Hex(0x0E2040), 0.82f),
        K(1.0000f, Hex(0x0C1C38), 0.85f)
    };

    static readonly GradKey[] SkyHorizonKeys =
    {
        K(0.0000f, Hex(0x182E4A), 0.90f),
        K(0.1875f, Hex(0x1A3450), 0.92f),
        K(0.2290f, Hex(0x5840B0), 0.90f),
        K(0.2500f, Hex(0xE058A8), 1.25f),
        K(0.2700f, Hex(0xFF8A40), 1.55f),
        K(0.2917f, Hex(0xFFC070), 1.35f),
        K(0.3600f, Hex(0x5AA8F0), 1.15f),
        K(0.5000f, Hex(0x4AA0F0), 1.10f),
        K(0.6667f, Hex(0x78B0E8), 1.10f),
        K(0.7400f, Hex(0xFF9A38), 1.75f),
        K(0.8000f, Hex(0xFF4A20), 1.65f),
        K(0.8500f, Hex(0x5060B8), 0.95f),
        K(0.9167f, Hex(0x1A3858), 0.92f),
        K(1.0000f, Hex(0x182E4A), 0.90f)
    };

    static readonly GradKey[] SkyGroundKeys =
    {
        K(0.0000f, Hex(0x0A121C), 0.38f),
        K(0.3000f, Hex(0x14161C), 0.28f),
        K(0.5000f, Hex(0x181A20), 0.28f),
        K(0.8000f, Hex(0x10141C), 0.30f),
        K(1.0000f, Hex(0x0A121C), 0.38f)
    };

    static readonly GradKey[] HorizonHazeKeys =
    {
        K(0.0000f, Hex(0x386888), 0.50f),
        K(0.2500f, Hex(0xFFB888), 1.10f),
        K(0.5000f, Hex(0x88C0F0), 0.75f), // less white midday haze
        K(0.7600f, Hex(0xFF9A58), 1.30f),
        K(0.9000f, Hex(0x3A6888), 0.50f),
        K(1.0000f, Hex(0x386888), 0.50f)
    };

    static readonly GradKey[] FogColorKeys =
    {
        K(0.0000f, Hex(0x163458), 0.80f),
        K(0.2500f, Hex(0xC08070), 0.75f),
        K(0.3500f, Hex(0x70B0E8), 0.90f),
        K(0.5000f, Hex(0x78B8E8), 0.95f),
        K(0.7400f, Hex(0xE09050), 1.00f),
        K(0.8500f, Hex(0x3A5878), 0.70f),
        K(1.0000f, Hex(0x163458), 0.80f)
    };

    static readonly GradKey[] SunHaloKeys =
    {
        K(0.0000f, Hex(0x3858B0), 0.25f),
        K(0.2500f, Hex(0xFF6098), 1.00f),
        K(0.2800f, Hex(0xFF9A40), 1.25f),
        K(0.4200f, Hex(0xFFE8D0), 0.35f),
        K(0.5000f, Hex(0xFFF0E4), 0.28f), // noon corona: soft, not yellow
        K(0.6200f, Hex(0xFFE0C0), 0.40f),
        K(0.7400f, Hex(0xFF7018), 1.55f),
        K(0.8200f, Hex(0xFF3010), 1.10f),
        K(1.0000f, Hex(0x3858B0), 0.25f)
    };

    // ---- Helpers ----

    struct GradKey
    {
        public float t;
        public Color color;
    }

    static GradKey K(float t, Color c, float intensity)
    {
        return new GradKey { t = t, color = c * intensity };
    }

    static Color Hex(int rgb)
    {
        float r = ((rgb >> 16) & 0xFF) / 255f;
        float g = ((rgb >> 8) & 0xFF) / 255f;
        float b = (rgb & 0xFF) / 255f;
        return new Color(r, g, b, 1f);
    }

    static Color EvaluateRamp(GradKey[] keys, float t)
    {
        if (keys == null || keys.Length == 0)
            return Color.black;
        if (keys.Length == 1)
            return keys[0].color;

        t = Mathf.Repeat(t, 1f);

        for (int i = 0; i < keys.Length; i++)
        {
            int j = (i + 1) % keys.Length;
            float t0 = keys[i].t;
            float t1 = keys[j].t + (j == 0 ? 1f : 0f);
            float tt = t;
            if (j == 0 && t < keys[0].t)
                tt = t + 1f;

            if (tt < t0 || tt > t1)
                continue;

            float local = Mathf.InverseLerp(t0, t1, tt);
            local = local * local * (3f - 2f * local);
            return Color.Lerp(keys[i].color, keys[j].color, local);
        }

        return keys[keys.Length - 1].color;
    }

    static Keyframe Ease(float value, float time)
    {
        // Zero tangents = smooth Hermite ease between keys (Clamp forever).
        return new Keyframe(time, value, 0f, 0f, 0.33f, 0.33f);
    }

    static void FlattenTangents(AnimationCurve c, WrapMode wrap = WrapMode.Loop)
    {
        // Weather must Clamp — Loop makes Evaluate(1) wrap to t=0 (clear sky flash).
        c.preWrapMode = wrap;
        c.postWrapMode = wrap;
#if UNITY_EDITOR
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Auto);
        }
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GTAWorldController))]
sealed class GTAWorldControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("GTA World", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Only Time Of Day and Weather are artist-facing.\n" +
            "Linear fog matches the displayed sky horizon.\n" +
            "Fog distance: 100–500 m (clear) → 50–150 m (storm). Camera far clip is not modified.",
            MessageType.None);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("TimeOfDay"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Weather"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("WetMaxSmoothness"),
            new GUIContent("Wet Max Smoothness", "Smoothness target at full wetness. ~0.45–0.55 keeps normals readable."));

        if (!Application.isPlaying)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Scene View (Edit Mode)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool fog = EditorGUILayout.Toggle(
                "Fog In Scene View",
                EditorPrefs.GetBool(GTAWorldFogSettings.EditorSceneViewPrefKey, true));
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(GTAWorldFogSettings.EditorSceneViewPrefKey, fog);
                ((GTAWorldController)target).EditorRefreshWorld();
                SceneView.RepaintAll();
            }
        }

        serializedObject.ApplyModifiedProperties();

        if (GUILayout.Button("Rebuild Artistic Curves"))
        {
            var ctrl = (GTAWorldController)target;
            Undo.RecordObject(ctrl, "Rebuild GTA World Curves");
            ctrl.RebuildArtisticCurves();
            EditorUtility.SetDirty(ctrl);
        }
    }
}
#endif
