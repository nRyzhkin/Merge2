using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Advances <see cref="GTAWorldController.TimeOfDay"/> for play-mode day/night testing.
/// Hang on any object (or next to the world controller).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("GTA/Day Night Cycle")]
public sealed class GTADayNightCycle : MonoBehaviour
{
    [SerializeField] GTAWorldController world;
    [Tooltip("Real-time seconds for a full 0→1 day loop.")]
    [SerializeField, Min(1f)] float dayLengthSeconds = 120f;
    [SerializeField] bool playOnStart = true;
    [SerializeField] bool loop = true;
    [Tooltip("Optional: Space toggles play/pause in play mode.")]
    [SerializeField] bool spaceTogglesPause = true;

    bool _playing;

    public bool IsPlaying => _playing;

    void Awake()
    {
        if (world == null)
            world = FindAnyObjectByType<GTAWorldController>();
    }

    void OnEnable()
    {
        if (world == null)
            world = FindAnyObjectByType<GTAWorldController>();
        _playing = playOnStart && Application.isPlaying;
    }

    void Update()
    {
        if (!Application.isPlaying || world == null)
            return;

        if (spaceTogglesPause)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
                _playing = !_playing;
        }

        if (!_playing || dayLengthSeconds < 1f)
            return;

        float delta = Time.deltaTime / dayLengthSeconds;
        float next = world.TimeOfDay + delta;

        if (loop)
        {
            world.TimeOfDay = Mathf.Repeat(next, 1f);
        }
        else
        {
            world.TimeOfDay = Mathf.Clamp01(next);
            if (world.TimeOfDay >= 1f - 1e-5f)
                _playing = false;
        }
    }

    public void Play() => _playing = true;

    public void Pause() => _playing = false;

    public void Toggle() => _playing = !_playing;

    public void SetDayLengthSeconds(float seconds) => dayLengthSeconds = Mathf.Max(1f, seconds);
}
