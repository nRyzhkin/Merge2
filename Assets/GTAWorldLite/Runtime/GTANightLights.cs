using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Turns the Night light cluster on/off (opacity fade).
/// Hang next to <see cref="GTALightClusterController"/> / World Controller.
/// Manual toggle, optional hotkey, or auto from <see cref="GTAWorldController.TimeOfDay"/>.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("GTA/Night Lights")]
[DefaultExecutionOrder(45)]
public sealed class GTANightLights : MonoBehaviour
{
    public enum DriveMode
    {
        Manual,
        AutoTimeOfDay,
    }

    [SerializeField] GTALightClusterController clusters;
    [SerializeField] GTAWorldController world;

    [Header("Drive")]
    [SerializeField] DriveMode mode = DriveMode.AutoTimeOfDay;
    [Tooltip("Manual / hotkey target. Ignored in AutoTimeOfDay.")]
    [SerializeField] bool lightsOn = false;

    [Header("Auto (Time of Day 0..1)")]
    [Tooltip("Night lights fade in after this time (evening).")]
    [SerializeField, Range(0f, 1f)] float eveningOn = 0.78f;
    [Tooltip("Fully on by this time.")]
    [SerializeField, Range(0f, 1f)] float eveningFull = 0.86f;
    [Tooltip("Start fading out toward morning.")]
    [SerializeField, Range(0f, 1f)] float morningOffStart = 0.22f;
    [Tooltip("Fully off by this time.")]
    [SerializeField, Range(0f, 1f)] float morningOffEnd = 0.30f;

    [Header("Fade")]
    [SerializeField, Min(0.01f)] float fadeSeconds = 1.5f;
    [SerializeField, Range(0f, 1f)] float onOpacity = 1f;

    [Header("Input (play mode)")]
    [SerializeField] bool hotkeyToggle = true;
    [SerializeField] Key toggleKey = Key.L;

    float _opacity;
    float _target;

    public bool LightsOn => _target > 0.5f;
    public float Opacity => _opacity;

    void Awake()
    {
        ResolveRefs();
    }

    void OnEnable()
    {
        ResolveRefs();
        _target = EvaluateTargetOpacity();
        _opacity = _target;
        Apply(_opacity);
    }

    void Update()
    {
        if (clusters == null)
            ResolveRefs();
        if (clusters == null)
            return;

        if (hotkeyToggle && Application.isPlaying)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame)
            {
                mode = DriveMode.Manual;
                lightsOn = !lightsOn;
            }
        }

        _target = EvaluateTargetOpacity();

        float speed = 1f / Mathf.Max(0.01f, fadeSeconds);
        _opacity = Mathf.MoveTowards(_opacity, _target, speed * Time.deltaTime);
        Apply(_opacity);
    }

    public void SetLightsOn(bool on)
    {
        mode = DriveMode.Manual;
        lightsOn = on;
    }

    public void EnableLights() => SetLightsOn(true);

    public void DisableLights() => SetLightsOn(false);

    public void ToggleLights() => SetLightsOn(!lightsOn);

    public void SetMode(DriveMode driveMode) => mode = driveMode;

    float EvaluateTargetOpacity()
    {
        float full = Mathf.Clamp01(onOpacity);

        if (mode == DriveMode.Manual)
            return lightsOn ? full : 0f;

        if (world == null)
            ResolveRefs();
        if (world == null)
            return lightsOn ? full : 0f;

        float t = Mathf.Repeat(world.TimeOfDay, 1f);
        float night = NightBlend(t);
        return night * full;
    }

    /// <summary>
    /// 0 by day, 1 at night. Handles wrap across midnight.
    /// Evening window turns on; morning window turns off.
    /// </summary>
    float NightBlend(float t)
    {
        float on0 = eveningOn;
        float on1 = Mathf.Max(eveningOn + 1e-4f, eveningFull);
        float off0 = morningOffStart;
        float off1 = Mathf.Max(morningOffStart + 1e-4f, morningOffEnd);

        // Typical: evening ~0.78–0.86, morning ~0.22–0.30
        if (on0 <= off1)
        {
            // Degenerate / unusual ordering — treat as simple night band.
            return t >= on0 || t < off1 ? 1f : 0f;
        }

        if (t >= on1 || t < off0)
            return 1f;
        if (t >= on0 && t < on1)
            return Mathf.InverseLerp(on0, on1, t);
        if (t >= off0 && t < off1)
            return 1f - Mathf.InverseLerp(off0, off1, t);
        return 0f;
    }

    void Apply(float opacity)
    {
        clusters.SetOpacity(GTALightClusterId.Night, opacity);
    }

    void ResolveRefs()
    {
        if (clusters == null)
            clusters = GTALightClusterController.Instance != null
                ? GTALightClusterController.Instance
                : FindAnyObjectByType<GTALightClusterController>();

        if (world == null)
            world = FindAnyObjectByType<GTAWorldController>();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        fadeSeconds = Mathf.Max(0.01f, fadeSeconds);
        onOpacity = Mathf.Clamp01(onOpacity);
        eveningFull = Mathf.Max(eveningOn, eveningFull);
        morningOffEnd = Mathf.Max(morningOffStart, morningOffEnd);
    }
#endif
}
