using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class SellGhostView : MonoBehaviour
    {
        enum Phase
        {
            Idle,
            Anticipate,
            Shrink
        }

        [SerializeField] RectTransform root;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] UnityEngine.UI.Image image;

        EconomyConfig _config;
        Phase _phase = Phase.Idle;
        float _elapsed;
        Vector2 _basePosition;
        Vector3 _baseScale = Vector3.one;
        bool _playing;

        public bool IsPlaying => _playing;

        public void Ensure(RectTransform layer)
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

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (image == null)
            {
                image = GetComponent<UnityEngine.UI.Image>();
                if (image == null)
                {
                    image = gameObject.AddComponent<UnityEngine.UI.Image>();
                }
            }

            image.raycastTarget = false;
            image.preserveAspect = true;
            HideImmediate();
        }

        public void Play(Sprite sprite, Vector2 size, Vector2 layerPosition, Vector2 startScale, EconomyConfig config)
        {
            Ensure(null);
            _config = config;
            if (image != null)
            {
                image.sprite = sprite;
                image.enabled = sprite != null;
                image.color = Color.white;
            }

            _basePosition = layerPosition;
            _baseScale = new Vector3(startScale.x, startScale.y, 1f);
            if (root != null)
            {
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.sizeDelta = size.x > 1f && size.y > 1f ? size : new Vector2(170f, 170f);
                root.anchoredPosition = _basePosition;
                root.localScale = _baseScale;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            gameObject.SetActive(true);
            _elapsed = 0f;
            _phase = Phase.Anticipate;
            _playing = true;
            enabled = true;
        }

        public bool Tick(float dt)
        {
            if (!_playing || _config == null)
            {
                return false;
            }

            _elapsed += dt;
            if (_phase == Phase.Anticipate)
            {
                var duration = Mathf.Max(0.01f, _config.SellAnticipationDuration);
                var t = Mathf.Clamp01(_elapsed / duration);
                var curved = Evaluate(_config.SellCurve, t);
                var scale = Mathf.LerpUnclamped(1f, _config.SellAnticipationScale, curved);
                ApplyScale(scale);
                if (t < 1f)
                {
                    return true;
                }

                _elapsed = 0f;
                _phase = Phase.Shrink;
            }

            if (_phase == Phase.Shrink)
            {
                var duration = Mathf.Max(0.01f, _config.SellShrinkDuration);
                var t = Mathf.Clamp01(_elapsed / duration);
                var curved = Evaluate(_config.SellCurve, t);
                var scale = Mathf.LerpUnclamped(_config.SellAnticipationScale, _config.SellShrinkScale, curved);
                ApplyScale(scale);
                if (root != null)
                {
                    root.anchoredPosition = _basePosition + new Vector2(0f, _config.SellRisePixels * curved);
                }

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.LerpUnclamped(1f, 0f, curved);
                }

                if (t < 1f)
                {
                    return true;
                }

                HideImmediate();
                return false;
            }

            return false;
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

        void ApplyScale(float scale)
        {
            if (root == null)
            {
                return;
            }

            root.localScale = new Vector3(_baseScale.x * scale, _baseScale.y * scale, 1f);
        }

        static float Evaluate(AnimationCurve curve, float t)
        {
            return curve != null ? curve.Evaluate(t) : t;
        }
    }
}
