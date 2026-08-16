using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class MessageView : MonoBehaviour
    {
        enum Phase
        {
            Idle,
            Show,
            Stay,
            Hide
        }

        [SerializeField] RectTransform root;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] TextMeshProUGUI label;

        BoardGeneratorAnimationConfig _config;
        Phase _phase = Phase.Idle;
        float _elapsed;
        Vector2 _basePosition;
        Vector2 _showFromPosition;
        float _showFromScale = 0.8f;
        bool _playing;

        public bool IsPlaying => _playing;
        public MessageKind Kind { get; private set; }

        public void Bind(RectTransform messageRoot, CanvasGroup group, TextMeshProUGUI text)
        {
            root = messageRoot;
            canvasGroup = group;
            label = text;
            HideImmediate();
        }

        public void EnsureBindings()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (label == null)
            {
                label = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            var graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = false;
            }
        }

        public void Play(MessageKind kind, string text, Vector2 anchoredPosition, BoardGeneratorAnimationConfig config)
        {
            EnsureBindings();
            Kind = kind;
            _config = config;
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }

            _basePosition = anchoredPosition;
            var rise = config != null ? config.MessageRisePixels : 20f;
            _showFromPosition = anchoredPosition - new Vector2(0f, rise);
            _showFromScale = 0.8f;
            if (root != null)
            {
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = _showFromPosition;
                root.localScale = Vector3.one * _showFromScale;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            gameObject.SetActive(true);
            _elapsed = 0f;
            _phase = Phase.Show;
            _playing = true;
            enabled = true;
            ApplyShow(0f);
        }

        public void HideImmediate()
        {
            _playing = false;
            _phase = Phase.Idle;
            enabled = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (!_playing || _config == null)
            {
                return;
            }

            var dt = _config.GetDeltaTime();
            _elapsed += dt;
            switch (_phase)
            {
                case Phase.Show:
                {
                    var duration = Mathf.Max(0.01f, _config.MessageShowDuration);
                    var t = Mathf.Clamp01(_elapsed / duration);
                    var curved = _config.MessageShowCurve != null ? _config.MessageShowCurve.Evaluate(t) : t;
                    ApplyShow(curved);
                    if (t >= 1f)
                    {
                        _elapsed = 0f;
                        _phase = Phase.Stay;
                    }

                    break;
                }
                case Phase.Stay:
                {
                    if (_elapsed >= _config.MessageStayDuration)
                    {
                        _elapsed = 0f;
                        _phase = Phase.Hide;
                    }

                    break;
                }
                case Phase.Hide:
                {
                    var duration = Mathf.Max(0.01f, _config.MessageHideDuration);
                    var t = Mathf.Clamp01(_elapsed / duration);
                    var curved = _config.MessageHideCurve != null ? _config.MessageHideCurve.Evaluate(t) : t;
                    ApplyHide(curved);
                    if (t >= 1f)
                    {
                        HideImmediate();
                    }

                    break;
                }
            }
        }

        void ApplyShow(float t)
        {
            if (root != null)
            {
                root.anchoredPosition = Vector2.LerpUnclamped(_showFromPosition, _basePosition, t);
                var scale = t < 0.7f
                    ? Mathf.LerpUnclamped(_showFromScale, 1.05f, t / 0.7f)
                    : Mathf.LerpUnclamped(1.05f, 1f, (t - 0.7f) / 0.3f);
                root.localScale = new Vector3(scale, scale, 1f);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.LerpUnclamped(0f, 1f, t);
            }
        }

        void ApplyHide(float t)
        {
            if (root != null)
            {
                var scale = Mathf.LerpUnclamped(1f, 0.95f, t);
                root.localScale = new Vector3(scale, scale, 1f);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.LerpUnclamped(1f, 0f, t);
            }
        }
    }
}
