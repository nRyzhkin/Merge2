using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class MergeFxView : MonoBehaviour
    {
        const int SparkCount = 6;

        [SerializeField] Image flash;
        [SerializeField] Image[] sparks;

        BoardMergeAnimationConfig _config;
        RectTransform _poolParent;
        float _elapsed;
        bool _playing;
        static Sprite _whiteSprite;

        public bool IsPlaying => _playing;

        public void Bind(Image flashImage, Image[] sparkImages)
        {
            flash = flashImage;
            sparks = sparkImages;
            Hide();
        }

        public void EnsureChildren()
        {
            if (_poolParent == null)
            {
                _poolParent = transform.parent as RectTransform;
            }

            var sprite = WhiteSprite();
            if (flash == null)
            {
                flash = CreateImage("Flash", sprite);
            }

            if (sparks == null || sparks.Length != SparkCount)
            {
                sparks = new Image[SparkCount];
            }

            for (var i = 0; i < SparkCount; i++)
            {
                if (sparks[i] == null)
                {
                    sparks[i] = CreateImage($"Spark_{i}", sprite);
                }
            }

            Hide();
        }

        public void Play(RectTransform parent, BoardMergeAnimationConfig config)
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
            var scale = config != null ? config.MergeFxScaleMultiplier : 1.2f;
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
            var color = _config != null ? _config.FxColor : new Color(1f, 0.92f, 0.72f, 0.9f);
            var flashT = Mathf.Clamp01(t / 0.45f);
            var flashAlpha = (1f - flashT) * color.a * 0.7f;
            if (flash != null)
            {
                flash.color = new Color(color.r, color.g, color.b, flashAlpha);
                var flashScale = Mathf.Lerp(0.25f, 1.25f, flashT);
                flash.rectTransform.localScale = new Vector3(flashScale, flashScale, 1f);
            }

            var radius = Mathf.Lerp(8f, 58f, t);
            var sparkAlpha = (1f - t) * color.a;
            for (var i = 0; i < sparks.Length; i++)
            {
                var spark = sparks[i];
                if (spark == null)
                {
                    continue;
                }

                var angle = (Mathf.PI * 2f * i) / sparks.Length;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                spark.rectTransform.anchoredPosition = offset;
                spark.color = new Color(color.r, color.g, color.b, sparkAlpha);
                var sparkScale = Mathf.Lerp(0.85f, 0.15f, t);
                spark.rectTransform.localScale = new Vector3(sparkScale, sparkScale, 1f);
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
            child.sizeDelta = objectName == "Flash" ? new Vector2(70f, 70f) : new Vector2(18f, 18f);
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
