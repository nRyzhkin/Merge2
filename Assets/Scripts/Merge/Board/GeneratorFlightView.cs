using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    /// <summary>
    /// Same visual stack as BoardMergeFlightView / DragItemView: DragItemView child,
    /// poses in BoardDragLayer space via SetPose.
    /// </summary>
    [DisallowMultipleComponent]
    public class GeneratorFlightView : MonoBehaviour
    {
        enum Phase
        {
            Hidden,
            Arc,
            LandingImpact,
            LandingRebound,
            LandingSettle
        }

        [SerializeField] DragItemView item;

        Phase _phase = Phase.Hidden;
        float _elapsed;
        float _duration;
        float _arcHeight;
        Vector2 _start;
        Vector2 _control;
        Vector2 _end;
        Vector2 _fromScale;
        Vector2 _toScale;
        Vector2 _currentScale = Vector2.one;
        Vector2 _planarPos;
        float _fromRotation;
        float _currentRotation;
        float _rotationAmount;
        AnimationCurve _curve;
        Vector2 _landingImpactScale = new Vector2(1.05f, 0.94f);
        Vector2 _landingReboundScale = new Vector2(0.98f, 1.03f);
        float _landingDuration = 0.15f;

        public bool IsActive => _phase != Phase.Hidden;
        public bool IsLanding =>
            _phase == Phase.LandingImpact ||
            _phase == Phase.LandingRebound ||
            _phase == Phase.LandingSettle;

        public void Ensure(RectTransform layer)
        {
            if (item != null)
            {
                return;
            }

            var itemGo = new GameObject("GeneratorFlightItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
            bool preserveAspect,
            Color color,
            Vector2 startPos,
            Vector2 endPos,
            BoardGeneratorAnimationConfig config)
        {
            Ensure(null);
            if (item == null)
            {
                return;
            }

            var startScale = config != null ? config.FlightStartScale : 0.7f;
            var peakScale = config != null ? config.FlightScale : 1.05f;
            var duration = config != null ? Mathf.Max(0.2f, config.FlightDuration) : 0.25f;
            var arcHeight = config != null ? config.ArcHeight : 56f;
            _rotationAmount = config != null ? config.FlightRotationAmount : 4f;
            _curve = config != null ? config.FlightCurve : null;
            _landingImpactScale = config != null
                ? new Vector2(config.LandingImpactScaleX, config.LandingImpactScaleY)
                : new Vector2(1.05f, 0.94f);
            _landingReboundScale = config != null
                ? new Vector2(config.LandingReboundScaleX, config.LandingReboundScaleY)
                : new Vector2(0.98f, 1.03f);
            _landingDuration = config != null ? Mathf.Max(0.03f, config.LandingDuration) : 0.15f;

            item.Show(sprite, size, preserveAspect, color);

            _start = startPos;
            _end = endPos;
            var distance = Vector2.Distance(startPos, endPos);
            var distanceFactor = Mathf.Clamp01(distance / 420f);
            _arcHeight = Mathf.Lerp(Mathf.Max(24f, arcHeight * 0.45f), Mathf.Max(40f, arcHeight), distanceFactor);
            _control = EvaluateArcControl(startPos, endPos, _arcHeight);

            _planarPos = startPos;
            _currentScale = Vector2.one * startScale;
            _currentRotation = 0f;
            _fromScale = _currentScale;
            _toScale = Vector2.one * peakScale;
            _fromRotation = 0f;
            _elapsed = 0f;
            _duration = duration;
            _phase = Phase.Arc;
            ApplyPose();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
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

            if (_phase == Phase.Arc)
            {
                _planarPos = EvaluateQuadraticBezier(_start, _control, _end, curved);
                _currentScale = Vector2.LerpUnclamped(_fromScale, _toScale, curved);
                _currentRotation = Mathf.Sin(t * Mathf.PI) * _rotationAmount;
            }
            else
            {
                _planarPos = _end;
                _currentScale = Vector2.LerpUnclamped(_fromScale, _toScale, curved);
                _currentRotation = Mathf.LerpUnclamped(_fromRotation, 0f, curved);
            }

            ApplyPose();
            if (t < 1f)
            {
                return true;
            }

            return AdvancePhase();
        }

        public void HideImmediate()
        {
            if (item != null)
            {
                item.Hide();
                item.Rect.localRotation = Quaternion.identity;
            }

            _phase = Phase.Hidden;
            _currentRotation = 0f;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        bool AdvancePhase()
        {
            if (_phase == Phase.Arc)
            {
                BeginLandingScale(Phase.LandingImpact, _landingImpactScale, _landingDuration * 0.38f);
                return true;
            }

            if (_phase == Phase.LandingImpact)
            {
                BeginLandingScale(Phase.LandingRebound, _landingReboundScale, _landingDuration * 0.34f);
                return true;
            }

            if (_phase == Phase.LandingRebound)
            {
                BeginLandingScale(Phase.LandingSettle, Vector2.one, _landingDuration * 0.28f);
                return true;
            }

            HideImmediate();
            return false;
        }

        void BeginLandingScale(Phase phase, Vector2 toScale, float duration)
        {
            _phase = phase;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, duration);
            _fromScale = _currentScale;
            _toScale = toScale;
            _fromRotation = _currentRotation;
            _curve = null;
        }

        void ApplyPose()
        {
            if (item == null)
            {
                return;
            }

            item.SetPose(_planarPos, _currentScale);
            item.Rect.localRotation = Quaternion.Euler(0f, 0f, _currentRotation);
        }

        static Vector2 EvaluateArcControl(Vector2 start, Vector2 end, float arcHeight)
        {
            var mid = (start + end) * 0.5f;
            mid.y += Mathf.Max(8f, arcHeight);
            return mid;
        }

        static Vector2 EvaluateQuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            var u = 1f - t;
            return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
        }
    }
}
