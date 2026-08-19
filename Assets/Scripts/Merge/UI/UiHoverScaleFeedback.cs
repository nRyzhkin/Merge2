using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class UiHoverScaleFeedback : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] UiInteractionFeedbackConfig config;
        [SerializeField] RectTransform target;
        [SerializeField] Selectable selectable;

        Vector3 _baseScale = Vector3.one;
        bool _baseCaptured;
        bool _hovered;
        bool _pressed;
        bool _transitioning;
        bool _bounceReturning;
        RectTransform _bounceTarget;
        Vector3 _fromScale;
        Vector3 _toScale;
        float _elapsed;
        float _duration;
        AnimationCurve _curve;

        public void PlayBounce()
        {
            _bounceTarget = FindBagIcon();
            var bounce = BounceTarget;
            if (bounce == null)
            {
                return;
            }

            _baseScale = bounce.localScale.x < 0.01f ? Vector3.one : bounce.localScale;
            _bounceReturning = false;
            BeginTransition(_baseScale * (config != null ? config.HoverScale * 1.12f : 1.18f), Motion.HoverEnter);
        }

        RectTransform FindBagIcon()
        {
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i) as RectTransform;
                if (child != null && child.GetComponent<Image>() != null)
                {
                    return child;
                }
            }

            return Target;
        }

        public void Configure(UiInteractionFeedbackConfig feedbackConfig, RectTransform visualRoot = null)
        {
            config = feedbackConfig;
            if (visualRoot != null && target == null)
            {
                target = visualRoot;
            }

            if (selectable == null)
            {
                selectable = GetComponent<Selectable>();
            }

            CaptureBase();
            if (!_hovered && !_pressed && !_transitioning)
            {
                ApplyScale(_baseScale);
            }
        }

        void Awake()
        {
            if (selectable == null)
            {
                selectable = GetComponent<Selectable>();
            }

            CaptureBase();
        }

        void OnEnable()
        {
            if (!_hovered && !_pressed && !_transitioning)
            {
                CaptureBase();
            }
        }

        void OnDisable()
        {
            _hovered = false;
            _pressed = false;
            _transitioning = false;
            _bounceReturning = false;
            if (_baseCaptured)
            {
                ApplyScale(_baseScale);
            }

            _bounceTarget = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!UiPointerUtility.SupportsHover(eventData) || !IsInteractable())
            {
                return;
            }

            _hovered = true;
            if (!_pressed)
            {
                BeginTransition(ScaledHover(), Motion.HoverEnter);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            if (!_pressed)
            {
                BeginTransition(_baseScale, Motion.HoverExit);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable())
            {
                return;
            }

            _pressed = true;
            BeginTransition(ScaledPress(), Motion.Press);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            if (!IsInteractable())
            {
                BeginTransition(_baseScale, Motion.HoverExit);
                return;
            }

            if (_hovered)
            {
                BeginTransition(ScaledHover(), Motion.Release);
            }
            else
            {
                BeginTransition(_baseScale, Motion.Release);
            }
        }

        void Update()
        {
            if ((_hovered || _pressed) && !IsInteractable())
            {
                _hovered = false;
                _pressed = false;
                BeginTransition(_baseScale, Motion.HoverExit);
            }

            if (!_transitioning || config == null)
            {
                return;
            }

            _elapsed += config.GetDeltaTime();
            var t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            var curved = _curve != null ? _curve.Evaluate(t) : t;
            ApplyScale(Vector3.LerpUnclamped(_fromScale, _toScale, curved));

            if (t >= 1f)
            {
                ApplyScale(_toScale);
                if (_bounceTarget != null && !_bounceReturning)
                {
                    _bounceReturning = true;
                    BeginTransition(_baseScale, Motion.HoverExit);
                    return;
                }

                _bounceTarget = null;
                _bounceReturning = false;
                _transitioning = false;
            }
        }

        enum Motion
        {
            HoverEnter,
            HoverExit,
            Press,
            Release
        }

        void BeginTransition(Vector3 toScale, Motion motion)
        {
            if (config == null || BounceTarget == null)
            {
                return;
            }

            _fromScale = BounceTarget.localScale;
            _toScale = toScale;
            _elapsed = 0f;
            switch (motion)
            {
                case Motion.Press:
                    _duration = Mathf.Max(0.01f, config.PressDuration);
                    _curve = config.PressCurve;
                    break;
                case Motion.Release:
                    _duration = Mathf.Max(0.01f, config.ReleaseDuration);
                    _curve = config.ReleaseCurve;
                    break;
                case Motion.HoverEnter:
                    _duration = Mathf.Max(0.01f, config.HoverEnterDuration);
                    _curve = config.HoverEnterCurve;
                    break;
                default:
                    _duration = Mathf.Max(0.01f, config.HoverExitDuration);
                    _curve = config.HoverExitCurve;
                    break;
            }

            _transitioning = true;
        }

        bool IsInteractable()
        {
            return selectable == null || selectable.IsInteractable();
        }

        void CaptureBase()
        {
            if (Target == null)
            {
                return;
            }

            _baseScale = Target.localScale;
            _baseCaptured = true;
        }

        Vector3 ScaledHover()
        {
            var scale = config != null ? config.HoverScale : 1.05f;
            return _baseScale * scale;
        }

        Vector3 ScaledPress()
        {
            var scale = config != null ? config.PressScale : 0.98f;
            return _baseScale * scale;
        }

        RectTransform BounceTarget => _bounceTarget != null ? _bounceTarget : Target;

        RectTransform Target
        {
            get
            {
                if (target == null)
                {
                    target = transform as RectTransform;
                }

                return target;
            }
        }

        void ApplyScale(Vector3 scale)
        {
            if (BounceTarget != null)
            {
                BounceTarget.localScale = scale;
            }
        }
    }
}
