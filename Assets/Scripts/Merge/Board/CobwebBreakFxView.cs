using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class CobwebBreakFxView : MonoBehaviour
    {
        const int FragmentCount = 5;

        [SerializeField] Image[] fragments;

        BoardCobwebAnimationConfig _config;
        RectTransform _poolParent;
        float _elapsed;
        bool _playing;
        static Sprite _whiteSprite;

        public bool IsPlaying => _playing;

        public void EnsureChildren()
        {
            if (_poolParent == null)
            {
                _poolParent = transform.parent as RectTransform;
            }

            var sprite = WhiteSprite();
            if (fragments == null || fragments.Length != FragmentCount)
            {
                fragments = new Image[FragmentCount];
            }

            for (var i = 0; i < FragmentCount; i++)
            {
                if (fragments[i] == null)
                {
                    fragments[i] = CreateImage($"Fragment_{i}", sprite);
                }
            }

            Hide();
        }

        public void Play(RectTransform parent, BoardCobwebAnimationConfig config)
        {
            EnsureChildren();
            _config = config;
            if (parent != null && transform.parent != parent)
            {
                transform.SetParent(parent, false);
            }

            var rect = transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var scale = config != null ? config.FxScale : 1.1f;
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.SetAsLastSibling();
            gameObject.SetActive(true);
            _elapsed = 0f;
            _playing = true;
            enabled = true;
            Apply(0f);
        }

        public void Hide()
        {
            _playing = false;
            enabled = false;
            if (_poolParent != null && transform.parent != _poolParent)
            {
                transform.SetParent(_poolParent, false);
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

            _elapsed += _config.GetDeltaTime();
            var duration = Mathf.Max(0.05f, _config.FxDuration);
            var t = Mathf.Clamp01(_elapsed / duration);
            Apply(t);
            if (t >= 1f)
            {
                Hide();
            }
        }

        void Apply(float t)
        {
            var color = _config != null ? _config.FxColor : Color.white;
            var radius = Mathf.Lerp(6f, 42f, t);
            var alpha = (1f - t) * color.a;
            for (var i = 0; i < fragments.Length; i++)
            {
                var fragment = fragments[i];
                if (fragment == null)
                {
                    continue;
                }

                var angle = (Mathf.PI * 2f * i) / fragments.Length + 0.35f;
                fragment.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                fragment.color = new Color(color.r, color.g, color.b, alpha);
                var scale = Mathf.Lerp(0.7f, 0.15f, t);
                fragment.rectTransform.localScale = new Vector3(scale, scale * 0.55f, 1f);
                fragment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            }
        }

        Image CreateImage(string objectName, Sprite sprite)
        {
            var child = transform.Find(objectName) as RectTransform;
            Image image;
            if (child == null)
            {
                var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = gameObject.layer;
                child = go.GetComponent<RectTransform>();
                child.SetParent(transform, false);
                image = go.GetComponent<Image>();
            }
            else
            {
                image = child.GetComponent<Image>();
                if (image == null)
                {
                    image = child.gameObject.AddComponent<Image>();
                }
            }

            child.anchorMin = child.anchorMax = new Vector2(0.5f, 0.5f);
            child.pivot = new Vector2(0.5f, 0.5f);
            child.sizeDelta = new Vector2(16f, 10f);
            child.anchoredPosition = Vector2.zero;
            image.sprite = sprite;
            image.raycastTarget = false;
            image.maskable = false;
            return image;
        }

        static Sprite WhiteSprite()
        {
            if (_whiteSprite != null)
            {
                return _whiteSprite;
            }

            var texture = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 4f);
            return _whiteSprite;
        }
    }
}
