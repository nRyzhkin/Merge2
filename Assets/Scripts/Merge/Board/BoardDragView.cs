using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardDragView : MonoBehaviour
    {
        enum VisualPhase
        {
            Hidden,
            Pickup,
            Follow,
            DropMove,
            Squash,
            Rebound,
            Settle,
            MergeCollision,
            MergeAbsorb
        }

        [SerializeField] RectTransform layer;
        [SerializeField] DragItemView dragItem;
        [SerializeField] BoardDropTargetView dropTarget;
        [SerializeField] Canvas layerCanvas;

        BoardDragAnimationConfig _config;
        Canvas _rootCanvas;
        VisualPhase _phase = VisualPhase.Hidden;
        float _elapsed;
        float _duration;
        Vector2 _fromPos;
        Vector2 _toPos;
        Vector2 _fromScale;
        Vector2 _toScale;
        Vector2 _currentPos;
        Vector2 _currentScale = Vector2.one;
        Vector2 _planarPos;
        Vector2 _fromPlanar;
        Vector2 _toPlanar;
        Vector2 _followVelocity;
        AnimationCurve _curve;
        bool _scaleSettling;
        float _scaleElapsed;
        float _flightHeight;
        float _fromFlight;
        float _toFlight;

        public DragItemView DragItem => dragItem;
        public BoardDropTargetView DropTarget => dropTarget;
        public RectTransform Layer => layer;
        public bool IsDropFinished => _phase == VisualPhase.Hidden;
        public Vector2 CurrentDragPosition => _currentPos;
        public Vector2 CurrentPlanarPosition => _planarPos;
        public Vector2 CurrentScale => _currentScale;

        public bool TryCaptureVisualSnapshot(out Sprite sprite, out Vector2 size, out bool preserveAspect, out Color color, out Vector2 position, out Vector2 scale)
        {
            sprite = null;
            size = Vector2.zero;
            preserveAspect = true;
            color = Color.white;
            position = _currentPos;
            scale = _currentScale;
            if (dragItem == null || !dragItem.gameObject.activeSelf)
            {
                return false;
            }

            var image = dragItem.GetComponent<Image>();
            if (image == null)
            {
                return false;
            }

            sprite = image.sprite;
            size = dragItem.Rect.sizeDelta;
            preserveAspect = image.preserveAspect;
            color = image.color;
            return sprite != null;
        }

        public void Bind(RectTransform layerRect, DragItemView item, BoardDropTargetView target, Canvas nestedCanvas)
        {
            layer = layerRect;
            dragItem = item;
            dropTarget = target;
            layerCanvas = nestedCanvas;
        }

        public void Configure(BoardDragAnimationConfig config, Canvas rootCanvas)
        {
            _config = config;
            _rootCanvas = rootCanvas;
            if (dropTarget != null)
            {
                dropTarget.Configure(config);
            }
        }

        public static BoardDragView Ensure(Canvas canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            var existing = canvas.GetComponentInChildren<BoardDragView>(true);
            if (existing != null)
            {
                existing.EnsureChildren();
                return existing;
            }

            var layerGo = new GameObject("BoardDragLayer", typeof(RectTransform), typeof(Canvas));
            layerGo.layer = canvas.gameObject.layer;
            var layerRect = layerGo.GetComponent<RectTransform>();
            layerRect.SetParent(canvas.transform, false);
            Stretch(layerRect);
            layerRect.SetAsLastSibling();

            var nested = layerGo.GetComponent<Canvas>();
            nested.overrideSorting = true;
            nested.sortingOrder = 20;
            nested.renderMode = canvas.renderMode;
            nested.worldCamera = canvas.worldCamera;

            var view = layerGo.AddComponent<BoardDragView>();
            view.EnsureChildren();
            return view;
        }

        public Camera EventCamera
        {
            get
            {
                var canvas = _rootCanvas != null ? _rootCanvas : layerCanvas;
                if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return null;
                }

                return canvas.worldCamera;
            }
        }

        public Vector2 ScreenToLayer(Vector2 screenPosition)
        {
            if (layer == null)
            {
                return Vector2.zero;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPosition, EventCamera, out var local);
            return local;
        }

        public Vector2 WorldToLayer(Vector3 worldPosition)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(EventCamera, worldPosition);
            return ScreenToLayer(screen);
        }

        public void BeginPickup(Sprite sprite, Vector2 size, bool preserveAspect, Color color, Vector2 startPos, Vector2 startScale)
        {
            if (dragItem == null || _config == null)
            {
                return;
            }

            dragItem.Show(sprite, size, preserveAspect, color);
            _planarPos = startPos;
            _flightHeight = 0f;
            _currentPos = startPos;
            _currentScale = startScale;
            _fromPos = startPos;
            _fromPlanar = startPos;
            _fromScale = startScale;
            _fromFlight = 0f;
            _toFlight = _config.DragFlightHeight;
            _toScale = _config.PickupStretch;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, Mathf.Max(_config.PickupDuration, _config.PickupLiftDuration));
            _curve = _config.PickupCurve;
            _followVelocity = Vector2.zero;
            _scaleSettling = false;
            _scaleElapsed = 0f;
            _phase = VisualPhase.Pickup;
            ApplyPose();
            gameObject.SetActive(true);
        }

        public void BeginMergeCollision(Vector2 landPos, Vector2 dragScale, float duration, AnimationCurve curve)
        {
            if (_phase == VisualPhase.Hidden)
            {
                return;
            }

            HideDropTargetImmediate();
            _fromPlanar = _planarPos;
            _toPlanar = landPos;
            _fromPos = _currentPos;
            _toPos = landPos;
            _fromFlight = _flightHeight;
            _toFlight = 0f;
            _fromScale = _currentScale;
            _toScale = dragScale;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, duration);
            _curve = curve;
            _scaleSettling = false;
            _phase = VisualPhase.MergeCollision;
        }

        public void BeginMergeAbsorb(float finalScale, float duration, AnimationCurve curve)
        {
            if (_phase == VisualPhase.Hidden)
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
            _phase = VisualPhase.MergeAbsorb;
        }

        public void TickMerge(float dt)
        {
            if (_phase != VisualPhase.MergeCollision && _phase != VisualPhase.MergeAbsorb)
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

        public void BeginDrop(Vector2 landPos, bool returnToSource)
        {
            if (_phase == VisualPhase.Hidden)
            {
                return;
            }

            HideDropTargetImmediate();
            _fromPlanar = _planarPos;
            _toPlanar = landPos;
            _fromPos = _currentPos;
            _toPos = landPos;
            _fromFlight = _flightHeight;
            _toFlight = 0f;
            _fromScale = _currentScale;
            _toScale = _config != null ? _config.DropApproachScale : Vector2.one;
            _elapsed = 0f;
            var moveDuration = _config != null ? _config.LandingMoveDuration : 0.12f;
            if (returnToSource && _config != null)
            {
                moveDuration *= _config.SourceReturnDurationScale;
            }

            _duration = Mathf.Max(0.01f, moveDuration);
            _curve = _config != null ? _config.LandingMoveCurve : null;
            _phase = VisualPhase.DropMove;
        }

        public void TickFollowOrPickup(float dt, Vector2 followTarget)
        {
            if (_phase == VisualPhase.Pickup)
            {
                TickPickup(dt, followTarget);
                return;
            }

            if (_phase != VisualPhase.Follow)
            {
                return;
            }

            TickFollow(dt, followTarget);
            TickDragScale(dt);
            ApplyPose();
        }

        public bool TickDrop(float dt)
        {
            if (_phase != VisualPhase.DropMove && _phase != VisualPhase.Squash &&
                _phase != VisualPhase.Rebound && _phase != VisualPhase.Settle)
            {
                return false;
            }

            _elapsed += dt;
            var t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            var curved = _curve != null ? _curve.Evaluate(t) : t;

            if (_phase == VisualPhase.DropMove)
            {
                _planarPos = Vector2.LerpUnclamped(_fromPlanar, _toPlanar, curved);
                _flightHeight = Mathf.LerpUnclamped(_fromFlight, _toFlight, curved);
                _currentScale = Vector2.LerpUnclamped(_fromScale, _toScale, curved);
                ApplyPose();
            }
            else
            {
                _planarPos = _toPlanar;
                _flightHeight = 0f;
                _currentScale = Vector2.LerpUnclamped(_fromScale, _toScale, curved);
                ApplyPose();
            }

            if (t < 1f)
            {
                return true;
            }

            AdvanceDropPhase();
            return _phase != VisualPhase.Hidden;
        }

        public void HideImmediate()
        {
            HideDropTargetImmediate();
            if (dragItem != null)
            {
                dragItem.Hide();
            }

            _phase = VisualPhase.Hidden;
            _followVelocity = Vector2.zero;
            _flightHeight = 0f;
        }

        public void ShowDropTarget(BoardCellView cell)
        {
            if (dropTarget == null)
            {
                return;
            }

            dropTarget.ShowOn(cell);
        }

        public void ShowMergeDropTarget(BoardCellView cell, BoardMergeAnimationConfig mergeConfig)
        {
            if (dropTarget == null)
            {
                return;
            }

            dropTarget.ShowMergeOn(cell, mergeConfig);
        }

        public void HideDropTarget()
        {
            if (dropTarget != null)
            {
                dropTarget.Hide();
            }
        }

        public void HideDropTargetImmediate()
        {
            if (dropTarget != null)
            {
                dropTarget.HideImmediate();
            }
        }

        void TickPickup(float dt, Vector2 followTarget)
        {
            _elapsed += dt;
            var moveDuration = Mathf.Max(0.01f, _config != null ? _config.PickupDuration : 0.10f);
            var liftDuration = Mathf.Max(0.01f, _config != null ? _config.PickupLiftDuration : 0.10f);
            var moveT = Mathf.Clamp01(_elapsed / moveDuration);
            var liftT = Mathf.Clamp01(_elapsed / liftDuration);
            var moveCurved = _config != null && _config.PickupCurve != null ? _config.PickupCurve.Evaluate(moveT) : moveT;
            var liftCurved = _config != null && _config.PickupLiftCurve != null ? _config.PickupLiftCurve.Evaluate(liftT) : liftT;
            _planarPos = Vector2.LerpUnclamped(_fromPlanar, followTarget, moveCurved);
            _flightHeight = Mathf.LerpUnclamped(_fromFlight, _toFlight, liftCurved);
            _currentScale = Vector2.LerpUnclamped(_fromScale, _toScale, moveCurved);
            ApplyPose();
            if (moveT < 1f || liftT < 1f)
            {
                return;
            }

            _phase = VisualPhase.Follow;
            _scaleSettling = true;
            _scaleElapsed = 0f;
            _fromScale = _currentScale;
            _followVelocity = Vector2.zero;
            _flightHeight = _config != null ? _config.DragFlightHeight : _flightHeight;
        }

        void TickFollow(float dt, Vector2 followTarget)
        {
            var smooth = _config != null ? _config.FollowSmoothTime : 0f;
            if (smooth <= 0.0001f)
            {
                _planarPos = followTarget;
                _followVelocity = Vector2.zero;
            }
            else
            {
                _planarPos = Vector2.SmoothDamp(_planarPos, followTarget, ref _followVelocity, smooth, Mathf.Infinity, dt);
            }

            _flightHeight = _config != null ? _config.DragFlightHeight : _flightHeight;
        }

        void TickDragScale(float dt)
        {
            if (!_scaleSettling || _config == null)
            {
                return;
            }

            _scaleElapsed += dt;
            var duration = Mathf.Max(0.01f, _config.DragScaleDuration);
            var t = Mathf.Clamp01(_scaleElapsed / duration);
            var curved = _config.DragScaleCurve != null ? _config.DragScaleCurve.Evaluate(t) : t;
            var target = Vector2.one * _config.DragScale;
            _currentScale = Vector2.LerpUnclamped(_fromScale, target, curved);
            if (t >= 1f)
            {
                _currentScale = target;
                _scaleSettling = false;
            }
        }

        void AdvanceDropPhase()
        {
            if (_config == null)
            {
                HideImmediate();
                return;
            }

            if (_phase == VisualPhase.DropMove)
            {
                BeginScalePhase(VisualPhase.Squash, _config.LandingImpactScale, _config.LandingSquashDuration);
                return;
            }

            if (_phase == VisualPhase.Squash)
            {
                BeginScalePhase(VisualPhase.Rebound, _config.LandingReboundScale, _config.LandingReboundDuration);
                return;
            }

            if (_phase == VisualPhase.Rebound)
            {
                BeginScalePhase(VisualPhase.Settle, Vector2.one, _config.LandingSettleDuration);
                return;
            }

            HideImmediate();
        }

        void BeginScalePhase(VisualPhase phase, Vector2 toScale, float duration)
        {
            _phase = phase;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, duration);
            _curve = _config != null ? _config.LandingCurve : null;
            _fromScale = _currentScale;
            _toScale = toScale;
            _fromPlanar = _planarPos;
            _toPlanar = _planarPos;
            _fromPos = _currentPos;
            _toPos = _currentPos;
            _fromFlight = 0f;
            _toFlight = 0f;
            _flightHeight = 0f;
        }

        void ApplyPose()
        {
            _currentPos = _planarPos + new Vector2(0f, _flightHeight);
            if (dragItem != null)
            {
                dragItem.SetPose(_currentPos, _currentScale);
            }
        }

        public void EnsureChildren()
        {
            if (layer == null)
            {
                layer = transform as RectTransform;
            }

            if (layerCanvas == null)
            {
                layerCanvas = GetComponent<Canvas>();
            }

            if (dragItem == null)
            {
                dragItem = GetComponentInChildren<DragItemView>(true);
            }

            if (dragItem == null)
            {
                var itemGo = new GameObject("DragItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                itemGo.layer = gameObject.layer;
                var itemRect = itemGo.GetComponent<RectTransform>();
                itemRect.SetParent(layer, false);
                itemRect.anchorMin = itemRect.anchorMax = new Vector2(0.5f, 0.5f);
                itemRect.pivot = new Vector2(0.5f, 0.5f);
                itemRect.sizeDelta = new Vector2(170f, 170f);
                var image = itemGo.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                dragItem = itemGo.AddComponent<DragItemView>();
                var fxGo = new GameObject("FxRoot", typeof(RectTransform));
                var fxRect = fxGo.GetComponent<RectTransform>();
                fxRect.SetParent(itemRect, false);
                Stretch(fxRect);
                dragItem.Bind(image, fxRect);
            }

            if (dropTarget == null)
            {
                dropTarget = GetComponentInChildren<BoardDropTargetView>(true);
            }

            if (dropTarget == null)
            {
                var highlightGo = new GameObject("DropTargetHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                highlightGo.layer = gameObject.layer;
                var highlightRect = highlightGo.GetComponent<RectTransform>();
                highlightRect.SetParent(layer, false);
                Stretch(highlightRect);
                var highlightImage = highlightGo.GetComponent<Image>();
                highlightImage.raycastTarget = false;
                highlightImage.color = new Color(1f, 1f, 1f, 0.18f);
                dropTarget = highlightGo.AddComponent<BoardDropTargetView>();
                dropTarget.Bind(highlightRect, highlightImage);
            }

            if (dragItem != null)
            {
                dragItem.Hide();
            }

            if (dropTarget != null)
            {
                dropTarget.Hide();
            }
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }
}
