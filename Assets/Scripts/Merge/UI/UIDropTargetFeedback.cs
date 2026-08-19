using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class UIDropTargetFeedback : MonoBehaviour
    {
        [SerializeField] RectTransform target;
        [SerializeField] Graphic highlight;
        [SerializeField] [Range(1f, 1.2f)] float highlightScale = 1.08f;
        [SerializeField] float pulseSpeed = 3.2f;

        Vector3 _baseScale = Vector3.one;
        Color _baseHighlightColor = Color.white;
        bool _baseCaptured;
        bool _highlighted;
        float _pulse;

        public RectTransform Rect
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

        public void Configure(RectTransform visualRoot, Graphic glow)
        {
            if (visualRoot != null)
            {
                target = visualRoot;
            }

            highlight = glow;
            CaptureBase();
        }

        void Awake()
        {
            CaptureBase();
        }

        void OnDisable()
        {
            SetHighlighted(false, immediate: true);
        }

        public void SetHighlighted(bool highlighted, bool immediate = false)
        {
            CaptureBase();
            _highlighted = highlighted;
            if (!highlighted)
            {
                _pulse = 0f;
                Apply(0f, immediate);
            }
        }

        void Update()
        {
            if (!_highlighted)
            {
                return;
            }

            _pulse += Time.unscaledDeltaTime * pulseSpeed;
            var wave = 0.5f + 0.5f * Mathf.Sin(_pulse);
            Apply(wave, false);
        }

        void CaptureBase()
        {
            if (_baseCaptured || Rect == null)
            {
                return;
            }

            _baseScale = Rect.localScale;
            if (_baseScale.x < 0.01f)
            {
                _baseScale = Vector3.one;
            }

            if (highlight != null)
            {
                _baseHighlightColor = highlight.color;
            }

            _baseCaptured = true;
        }

        void Apply(float wave, bool immediate)
        {
            if (Rect != null)
            {
                var scale = Mathf.Lerp(1f, highlightScale, _highlighted ? Mathf.Lerp(0.65f, 1f, wave) : 0f);
                Rect.localScale = immediate || !_highlighted ? _baseScale : _baseScale * scale;
            }

            if (highlight == null)
            {
                return;
            }

            if (highlight.gameObject == gameObject)
            {
                return;
            }

            var color = _baseHighlightColor;
            color.a = _highlighted ? Mathf.Lerp(0.25f, 0.7f, wave) : _baseHighlightColor.a;
            highlight.color = color;
        }
    }
}
