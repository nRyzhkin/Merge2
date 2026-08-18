using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class CoinPopupView : MonoBehaviour
    {
        [SerializeField] RectTransform root;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Image icon;
        [SerializeField] TextMeshProUGUI label;
        [SerializeField] string iconToken = IconTokens.Coin;

        EconomyConfig _config;
        float _elapsed;
        Vector2 _from;
        Vector2 _to;
        Vector3 _baseScale = Vector3.one;
        bool _playing;

        public bool IsPlaying => _playing;

        public void Ensure()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (icon == null)
            {
                var child = transform.Find("Icon");
                if (child != null)
                {
                    icon = child.GetComponent<Image>();
                }
            }

            if (label == null)
            {
                var child = transform.Find("Value");
                if (child != null)
                {
                    label = child.GetComponent<TextMeshProUGUI>();
                }
            }

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (root != null)
            {
                _baseScale = root.localScale;
            }

            HideImmediate();
        }

        public void Play(string text, Vector2 layerPosition, EconomyConfig config)
        {
            Ensure();
            _config = config;
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }

            ApplyIcon();

            _from = layerPosition;
            _to = layerPosition + new Vector2(0f, config != null ? config.CoinPopupRisePixels : 42f);
            if (root != null)
            {
                root.anchoredPosition = _from;
                root.localScale = _baseScale;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            gameObject.SetActive(true);
            _elapsed = 0f;
            _playing = true;
            enabled = true;
        }

        public bool Tick(float dt)
        {
            if (!_playing)
            {
                return false;
            }

            if (_config == null)
            {
                HideImmediate();
                return false;
            }

            _elapsed += dt;
            var duration = Mathf.Max(0.01f, _config.CoinPopupDuration);
            var t = Mathf.Clamp01(_elapsed / duration);
            var curved = _config.CoinPopupCurve != null ? _config.CoinPopupCurve.Evaluate(t) : t;
            if (root != null)
            {
                root.anchoredPosition = Vector2.LerpUnclamped(_from, _to, curved);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = t < 0.55f ? 1f : Mathf.LerpUnclamped(1f, 0f, (t - 0.55f) / 0.45f);
            }

            if (t < 1f)
            {
                return true;
            }

            HideImmediate();
            return false;
        }

        public void HideImmediate()
        {
            _playing = false;
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

        void ApplyIcon()
        {
            if (icon == null)
            {
                return;
            }

            var sprite = Icons.Get(iconToken);
            if (sprite == null)
            {
                return;
            }

            icon.sprite = sprite;
            icon.enabled = true;
        }
    }
}
