using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class UiHoverScaleFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] UiInteractionFeedbackConfig config;
        [SerializeField] RectTransform target;
        [SerializeField] Selectable selectable;

        Vector3 _baseScale = Vector3.one;
        bool _baseCaptured;
        bool _hovered;
        bool _transitioning;
        Vector3 _fromScale;
        Vector3 _toScale;
        float _elapsed;
        float _duration;
        AnimationCurve _curve;

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
            if (!_hovered && !_transitioning)
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
            if (!_hovered && !_transitioning)
            {
                CaptureBase();
            }
        }

        void OnDisable()
        {
            _hovered = false;
            _transitioning = false;
            if (_baseCaptured)
            {
                ApplyScale(_baseScale);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!UiPointerUtility.SupportsHover(eventData) || !IsInteractable())
            {
                return;
            }

            BeginHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            BeginHover(false);
        }

        void Update()
        {
            if (_hovered && !IsInteractable())
            {
                BeginHover(false);
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
                _transitioning = false;
            }
        }

        void BeginHover(bool hovered)
        {
            if (config == null || Target == null)
            {
                return;
            }

            if (hovered == _hovered && !_transitioning)
            {
                return;
            }

            _hovered = hovered;
            _fromScale = Target.localScale;
            _toScale = hovered ? ScaledHover() : _baseScale;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, hovered ? config.HoverEnterDuration : config.HoverExitDuration);
            _curve = hovered ? config.HoverEnterCurve : config.HoverExitCurve;
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
            if (Target != null)
            {
                Target.localScale = scale;
            }
        }
    }
}
