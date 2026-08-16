using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoxBreakFxView : MonoBehaviour
    {
        const int FragmentCount = 4;
        const int DustCount = 3;

        [SerializeField] Image puff;
        [SerializeField] Image[] fragments;
        [SerializeField] Image[] dust;

        BoardBoxAnimationConfig _config;
        RectTransform _poolParent;
        float _elapsed;
        bool _playing;
        int _rotationSign = 1;
        static Sprite _whiteSprite;

        public bool IsPlaying => _playing;

        public void EnsureChildren()
        {
            if (_poolParent == null)
            {
                _poolParent = transform.parent as RectTransform;
            }

            var sprite = WhiteSprite();
            if (puff == null)
            {
                puff = CreateImage("Puff", sprite, new Vector2(28f, 28f));
            }

            if (fragments == null || fragments.Length != FragmentCount)
            {
                fragments = new Image[FragmentCount];
            }

            for (var i = 0; i < FragmentCount; i++)
            {
                if (fragments[i] == null)
                {
                    fragments[i] = CreateImage($"Fragment_{i}", sprite, new Vector2(14f, 10f));
                }
            }

            if (dust == null || dust.Length != DustCount)
            {
                dust = new Image[DustCount];
            }

            for (var i = 0; i < DustCount; i++)
            {
                if (dust[i] == null)
                {
                    dust[i] = CreateImage($"Dust_{i}", sprite, new Vector2(8f, 8f));
                }
            }

            Hide();
        }

        public void Play(RectTransform parent, BoardBoxAnimationConfig config)
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
            var scale = config != null ? config.FxScale : 1.15f;
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.SetAsLastSibling();
            _rotationSign = Random.value < 0.5f ? -1 : 1;
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
            var color = _config != null ? _config.FxColor : new Color(0.82f, 0.68f, 0.48f, 0.7f);
            var fade = 1f - t;
            if (puff != null)
            {
                var puffScale = Mathf.Lerp(0.35f, 1.15f, t);
                puff.rectTransform.localScale = new Vector3(puffScale, puffScale * 0.85f, 1f);
                puff.color = new Color(color.r, color.g, color.b, fade * color.a * 0.35f);
            }

            var fragmentRadius = Mathf.Lerp(8f, 36f, t);
            for (var i = 0; i < fragments.Length; i++)
            {
                var fragment = fragments[i];
                if (fragment == null)
                {
                    continue;
                }

                var angle = (Mathf.PI * 2f * i) / fragments.Length + 0.4f;
                fragment.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * fragmentRadius;
                fragment.color = new Color(color.r, color.g, color.b, fade * color.a);
                var scale = Mathf.Lerp(0.85f, 0.2f, t);
                fragment.rectTransform.localScale = new Vector3(scale, scale * 0.7f, 1f);
                fragment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (angle * Mathf.Rad2Deg + t * 40f) * _rotationSign);
            }

            var dustRadius = Mathf.Lerp(4f, 22f, t);
            for (var i = 0; i < dust.Length; i++)
            {
                var speck = dust[i];
                if (speck == null)
                {
                    continue;
                }

                var angle = (Mathf.PI * 2f * i) / dust.Length + 1.1f;
                speck.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) + 0.35f) * dustRadius;
                speck.color = new Color(color.r, color.g, color.b, fade * color.a * 0.45f);
                var scale = Mathf.Lerp(0.6f, 0.1f, t);
                speck.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        Image CreateImage(string objectName, Sprite sprite, Vector2 size)
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
            child.sizeDelta = size;
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
