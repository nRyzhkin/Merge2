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
            Absorb,
            LandingMove,
            LandingSquash,
            LandingRebound,
            LandingSettle
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
        BoardDragAnimationConfig _dragConfig;

        public bool IsActive => _phase != Phase.Hidden;
        public bool IsLanding =>
            _phase == Phase.LandingMove ||
            _phase == Phase.LandingSquash ||
            _phase == Phase.LandingRebound ||
            _phase == Phase.LandingSettle;
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

        public void BeginLanding(Vector2 landPos, BoardDragAnimationConfig dragConfig, float flightHeight)
        {
            if (item == null)
            {
                return;
            }

            _dragConfig = dragConfig;
            _fromPlanar = _planarPos;
            _toPlanar = landPos;
            _fromFlight = Mathf.Max(_flightHeight, flightHeight);
            _toFlight = 0f;
            _fromScale = _currentScale;
            _toScale = dragConfig != null ? dragConfig.DropApproachScale : Vector2.one;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, dragConfig != null ? dragConfig.LandingMoveDuration : 0.12f);
            _curve = dragConfig != null ? dragConfig.LandingMoveCurve : null;
            _phase = Phase.LandingMove;
            gameObject.SetActive(true);
        }

        public bool Tick(float dt)
        {
            if (_phase == Phase.Hidden)
            {
                return false;
            }

            _elapsed += dt;
            var t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            var curved = _curve != null ? _curve.Evaluate(t) : t;

            if (_phase == Phase.Collision || _phase == Phase.Absorb || _phase == Phase.LandingMove)
            {
                _planarPos = Vector2.LerpUnclamped(_fromPlanar, _toPlanar, curved);
                _flightHeight = Mathf.LerpUnclamped(_fromFlight, _toFlight, curved);
                _currentScale = Vector2.LerpUnclamped(_fromScale, _toScale, curved);
            }
            else
            {
                _planarPos = _toPlanar;
                _flightHeight = 0f;
                _currentScale = Vector2.LerpUnclamped(_fromScale, _toScale, curved);
            }

            ApplyPose();
            if (t < 1f)
            {
                return true;
            }

            if (_phase == Phase.LandingMove)
            {
                BeginLandingScale(Phase.LandingSquash,
                    _dragConfig != null ? _dragConfig.LandingImpactScale : new Vector2(1.055f, 0.94f),
                    _dragConfig != null ? _dragConfig.LandingSquashDuration : 0.06f);
                return true;
            }

            if (_phase == Phase.LandingSquash)
            {
                BeginLandingScale(Phase.LandingRebound,
                    _dragConfig != null ? _dragConfig.LandingReboundScale : new Vector2(0.98f, 1.03f),
                    _dragConfig != null ? _dragConfig.LandingReboundDuration : 0.06f);
                return true;
            }

            if (_phase == Phase.LandingRebound)
            {
                BeginLandingScale(Phase.LandingSettle, Vector2.one,
                    _dragConfig != null ? _dragConfig.LandingSettleDuration : 0.05f);
                return true;
            }

            if (_phase == Phase.LandingSettle)
            {
                HideImmediate();
                return false;
            }

            return _phase != Phase.Hidden;
        }

        void BeginLandingScale(Phase phase, Vector2 toScale, float duration)
        {
            _phase = phase;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, duration);
            _curve = _dragConfig != null ? _dragConfig.LandingCurve : null;
            _fromScale = _currentScale;
            _toScale = toScale;
            _fromPlanar = _planarPos;
            _toPlanar = _planarPos;
            _fromFlight = 0f;
            _toFlight = 0f;
            _flightHeight = 0f;
        }

        public void HideImmediate()
        {
            if (item != null)
            {
                item.Hide();
            }

            _phase = Phase.Hidden;
            _flightHeight = 0f;
            _dragConfig = null;
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
