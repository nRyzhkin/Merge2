using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardItemAnimator : MonoBehaviour
    {
        enum Phase
        {
            Idle,
            Press,
            Release,
            Return
        }

        [SerializeField] BoardItemAnimationConfig config;
        [SerializeField] UiInteractionFeedbackConfig hoverConfig;
        [SerializeField] RectTransform itemRect;
        [SerializeField] RectTransform cellRect;

        Phase _phase = Phase.Idle;
        float _elapsed;
        float _duration;
        Vector2 _fromScale;
        Vector2 _toScale;
        Vector2 _actionScale = Vector2.one;
        float _fromDown;
        float _toDown;
        float _currentDown;
        Vector2 _idleOffsetMin;
        Vector2 _idleOffsetMax;
        bool _idleCaptured;
        AnimationCurve _curve;

        bool _hovered;
        bool _hoverTransitioning;
        float _hoverMultiplier = 1f;
        float _hoverFrom = 1f;
        float _hoverTo = 1f;
        float _hoverElapsed;
        float _hoverDuration;
        AnimationCurve _hoverCurve;

        public bool IsPlaying => _phase != Phase.Idle;
        public bool IsHovered => _hovered;

        // Hover is an independent multiplier. Future drag/drop is another action phase
        // and must compose with hover the same way press/release already do.

        public void Configure(
            BoardItemAnimationConfig animationConfig,
            RectTransform item,
            RectTransform cell,
            UiInteractionFeedbackConfig interactionConfig = null)
        {
            config = animationConfig;
            hoverConfig = interactionConfig;
            itemRect = item;
            cellRect = cell;
            CaptureIdle();
            _hovered = false;
            _hoverMultiplier = 1f;
            _hoverTransitioning = false;
            SnapActionToIdle();
            enabled = false;
        }

        public void SetHovered(bool hovered)
        {
            if (hoverConfig == null)
            {
                _hovered = hovered;
                _hoverMultiplier = 1f;
                _hoverTransitioning = false;
                ApplyPose();
                return;
            }

            var target = hovered ? hoverConfig.HoverScale : 1f;
            if (_hovered == hovered && !_hoverTransitioning && Mathf.Approximately(_hoverMultiplier, target))
            {
                return;
            }

            _hovered = hovered;
            _hoverFrom = _hoverMultiplier;
            _hoverTo = target;
            _hoverElapsed = 0f;
            _hoverDuration = Mathf.Max(0.01f, hovered ? hoverConfig.HoverEnterDuration : hoverConfig.HoverExitDuration);
            _hoverCurve = hovered ? hoverConfig.HoverEnterCurve : hoverConfig.HoverExitCurve;
            _hoverTransitioning = true;
            enabled = true;
        }

        public void PlayPressAnticipation()
        {
            if (!CanAnimate())
            {
                return;
            }

            CaptureIdleIfNeeded();
            BeginPhase(Phase.Press, config.PressDuration, config.PressCurve, config.PressScale, CellHeight() * config.PressDownNormalized);
        }

        public void PlayClickRelease()
        {
            if (!CanAnimate())
            {
                return;
            }

            CaptureIdleIfNeeded();
            BeginPhase(Phase.Release, config.ReleaseDuration, config.ReleaseCurve, config.ReleaseScale, 0f);
        }

        public void BeginDrag()
        {
            SetHovered(false);
            SnapActionToIdle();
        }

        public void Drop()
        {
            SnapActionToIdle();
        }

        public void SnapActionToIdle()
        {
            _phase = Phase.Idle;
            _elapsed = 0f;
            _actionScale = Vector2.one;
            _currentDown = 0f;
            ApplyPose();
            RefreshEnabled();
        }

        void OnDisable()
        {
            _hovered = false;
            _hoverTransitioning = false;
            _hoverMultiplier = 1f;
            _phase = Phase.Idle;
            _actionScale = Vector2.one;
            _currentDown = 0f;
            ApplyPose();
        }

        void Update()
        {
            var dt = hoverConfig != null ? hoverConfig.GetDeltaTime() : Time.unscaledDeltaTime;
            var actionRunning = TickAction(dt);
            var hoverRunning = TickHover(dt);
            ApplyPose();

            if (!actionRunning && !hoverRunning && !_hovered)
            {
                enabled = false;
            }
        }

        bool TickAction(float dt)
        {
            if (_phase == Phase.Idle || config == null)
            {
                return false;
            }

            _elapsed += dt;
            var t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            var curved = _curve != null ? _curve.Evaluate(t) : t;
            _actionScale = Vector2.LerpUnclamped(_fromScale, _toScale, curved);
            _currentDown = Mathf.LerpUnclamped(_fromDown, _toDown, curved);

            if (t < 1f)
            {
                return true;
            }

            _actionScale = _toScale;
            _currentDown = _toDown;
            if (_phase == Phase.Press)
            {
                return false;
            }

            if (_phase == Phase.Release)
            {
                BeginPhase(Phase.Return, config.ReturnDuration, config.ReturnCurve, Vector2.one, 0f);
                return true;
            }

            _phase = Phase.Idle;
            _actionScale = Vector2.one;
            _currentDown = 0f;
            return false;
        }

        bool TickHover(float dt)
        {
            if (!_hoverTransitioning)
            {
                return false;
            }

            _hoverElapsed += dt;
            var t = _hoverDuration <= 0f ? 1f : Mathf.Clamp01(_hoverElapsed / _hoverDuration);
            var curved = _hoverCurve != null ? _hoverCurve.Evaluate(t) : t;
            _hoverMultiplier = Mathf.LerpUnclamped(_hoverFrom, _hoverTo, curved);
            if (t < 1f)
            {
                return true;
            }

            _hoverMultiplier = _hoverTo;
            _hoverTransitioning = false;
            return false;
        }

        void BeginPhase(Phase phase, float duration, AnimationCurve curve, Vector2 toScale, float toDown)
        {
            if (itemRect == null)
            {
                return;
            }

            _phase = phase;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, duration);
            _curve = curve;
            _fromScale = _actionScale;
            _toScale = toScale;
            _fromDown = _currentDown;
            _toDown = toDown;
            enabled = true;
        }

        bool CanAnimate()
        {
            return config != null && itemRect != null && itemRect.gameObject.activeInHierarchy;
        }

        void CaptureIdleIfNeeded()
        {
            if (!_idleCaptured)
            {
                CaptureIdle();
            }
        }

        void CaptureIdle()
        {
            if (itemRect == null)
            {
                return;
            }

            _idleOffsetMin = itemRect.offsetMin;
            _idleOffsetMax = itemRect.offsetMax;
            _idleCaptured = true;
        }

        void RefreshEnabled()
        {
            enabled = _phase != Phase.Idle || _hoverTransitioning || _hovered;
        }

        float CellHeight()
        {
            if (cellRect != null)
            {
                return Mathf.Max(1f, cellRect.rect.height);
            }

            return 170f;
        }

        void ApplyPose()
        {
            if (itemRect == null)
            {
                return;
            }

            itemRect.localScale = new Vector3(_actionScale.x * _hoverMultiplier, _actionScale.y * _hoverMultiplier, 1f);
            itemRect.offsetMin = new Vector2(_idleOffsetMin.x, _idleOffsetMin.y - _currentDown);
            itemRect.offsetMax = new Vector2(_idleOffsetMax.x, _idleOffsetMax.y - _currentDown);
        }
    }
}
