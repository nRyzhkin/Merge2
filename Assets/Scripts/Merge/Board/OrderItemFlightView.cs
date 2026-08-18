using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class OrderItemFlightView : MonoBehaviour
    {
        [SerializeField] DragItemView item;
        [SerializeField] CanvasGroup canvasGroup;

        float _delay;
        float _elapsed;
        float _duration;
        float _lift;
        Vector2 _start;
        Vector2 _control;
        Vector2 _end;
        AnimationCurve _curve;
        bool _playing;

        public bool IsPlaying => _playing;

        public void Ensure(RectTransform layer)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }

                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (item != null)
            {
                return;
            }

            var itemGo = new GameObject("OrderFlightItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            itemGo.layer = gameObject.layer;
            var itemRect = itemGo.GetComponent<RectTransform>();
            itemRect.SetParent(transform, false);
            itemRect.anchorMin = itemRect.anchorMax = new Vector2(0.5f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = new Vector2(170f, 170f);
            var image = itemGo.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            item = itemGo.AddComponent<DragItemView>();
            var fxGo = new GameObject("FxRoot", typeof(RectTransform));
            var fxRect = fxGo.GetComponent<RectTransform>();
            fxRect.SetParent(itemRect, false);
            fxRect.anchorMin = Vector2.zero;
            fxRect.anchorMax = Vector2.one;
            fxRect.offsetMin = Vector2.zero;
            fxRect.offsetMax = Vector2.zero;
            item.Bind(image, fxRect);
            HideImmediate();
        }

        public void Begin(
            Sprite sprite,
            Vector2 size,
            Vector2 startPos,
            Vector2 endPos,
            float delay,
            OrderDatabase database)
        {
            Ensure(null);
            if (item == null)
            {
                return;
            }

            item.Show(sprite, size, true, Color.white);
            _start = startPos;
            _end = endPos;
            _delay = Mathf.Max(0f, delay);
            _duration = database != null ? Mathf.Max(0.55f, database.FlightDuration) : 0.76f;
            var arcHeight = database != null ? database.FlightArcHeight : 140f;
            _lift = database != null ? database.FlightLift : 36f;
            _curve = database != null ? database.FlightCurve : null;
            _control = EvaluateArcControl(startPos, endPos, arcHeight);
            _elapsed = 0f;
            _playing = true;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            item.SetPose(startPos, Vector2.one);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public bool Tick(float dt)
        {
            if (!_playing)
            {
                return false;
            }

            if (_delay > 0f)
            {
                _delay -= dt;
                if (_delay > 0f)
                {
                    item.SetPose(_start, Vector2.one);
                    return true;
                }

                _delay = 0f;
            }

            _elapsed += dt;
            var t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            var curved = _curve != null ? _curve.Evaluate(t) : SmoothStep(t);
            var pos = EvaluateQuadraticBezier(_start, _control, _end, curved);
            var liftT = SmoothStep(Mathf.Clamp01(t / 0.4f));
            pos.y += _lift * liftT * (1f - SmoothStep(t));

            var scale = t < 0.5f
                ? Mathf.Lerp(1f, 1.05f, SmoothStep(t / 0.5f))
                : Mathf.Lerp(1.05f, 0.88f, SmoothStep((t - 0.5f) / 0.5f));
            item.SetPose(pos, Vector2.one * scale);
            if (canvasGroup != null)
            {
                var fade = SmoothStep(Mathf.InverseLerp(0.55f, 1f, t));
                canvasGroup.alpha = 1f - fade;
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
            if (item != null)
            {
                item.Hide();
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        static Vector2 EvaluateArcControl(Vector2 start, Vector2 end, float height)
        {
            var mid = (start + end) * 0.5f;
            mid.y += height;
            return mid;
        }

        static Vector2 EvaluateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            var oneMinus = 1f - t;
            return oneMinus * oneMinus * start + 2f * oneMinus * t * control + t * t * end;
        }

        static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
