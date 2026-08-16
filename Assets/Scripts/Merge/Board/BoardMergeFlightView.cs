using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    /// <summary>
    /// Independent flight visual for one merge sequence. Does not share the player DragItemView.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardMergeFlightView : MonoBehaviour
    {
        enum Phase
        {
            Hidden,
            Collision,
            Absorb
        }

        [SerializeField] DragItemView item;

        Phase _phase = Phase.Hidden;
        float _elapsed;
        float _duration;
        Vector2 _fromPlanar;
        Vector2 _toPlanar;
        Vector2 _fromScale;
        Vector2 _toScale;
        Vector2 _planarPos;
        Vector2 _currentScale = Vector2.one;
        float _flightHeight;
        float _fromFlight;
        float _toFlight;
        AnimationCurve _curve;
        RectTransform _layer;

        public bool IsActive => _phase != Phase.Hidden;
        public DragItemView Item => item;

        public void Bind(DragItemView dragItem, RectTransform layer)
        {
            item = dragItem;
            _layer = layer;
            HideImmediate();
        }

        public void Ensure(RectTransform layer)
        {
            _layer = layer;
            if (item != null)
            {
                return;
            }

            var itemGo = new GameObject("MergeFlightItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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

        public void BeginCollision(
            Sprite sprite,
            Vector2 size,
            bool preserveAspect,
            Color color,
            Vector2 startPos,
            Vector2 startScale,
            Vector2 landPos,
            Vector2 dragScale,
            float duration,
            AnimationCurve curve)
        {
            Ensure(_layer);
            if (item == null)
            {
                return;
            }

            item.Show(sprite, size, preserveAspect, color);
            _planarPos = startPos;
            _flightHeight = 0f;
            _currentScale = startScale;
            _fromPlanar = startPos;
            _toPlanar = landPos;
            _fromFlight = 0f;
            _toFlight = 0f;
            _fromScale = startScale;
            _toScale = dragScale;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, duration);
            _curve = curve;
            _phase = Phase.Collision;
            ApplyPose();
            gameObject.SetActive(true);
        }

        public void BeginAbsorb(float finalScale, float duration, AnimationCurve curve)
        {
            if (_phase == Phase.Hidden)
            {
                return;
            }

            _fromPlanar = _planarPos;
            _toPlanar = _planarPos;
            _fromFlight = _flightHeight;
            _toFlight = 0f;
            _fromScale = _currentScale;
            _toScale = Vector2.one * finalScale;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, duration);
            _curve = curve;
            _phase = Phase.Absorb;
        }

        public void Tick(float dt)
        {
            if (_phase == Phase.Hidden)
            {
                return;
            }

            _elapsed += dt;
            var t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            var curved = _curve != null ? _curve.Evaluate(t) : t;
            _planarPos = Vector2.LerpUnclamped(_fromPlanar, _toPlanar, curved);
            _flightHeight = Mathf.LerpUnclamped(_fromFlight, _toFlight, curved);
            _currentScale = Vector2.LerpUnclamped(_fromScale, _toScale, curved);
            ApplyPose();
        }

        public void HideImmediate()
        {
            if (item != null)
            {
                item.Hide();
            }

            _phase = Phase.Hidden;
            _flightHeight = 0f;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        void ApplyPose()
        {
            if (item != null)
            {
                item.SetPose(_planarPos + new Vector2(0f, _flightHeight), _currentScale);
            }
        }
    }
}
