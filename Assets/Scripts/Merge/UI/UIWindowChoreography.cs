using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class UIWindowChoreography : MonoBehaviour
    {
        const int PhaseBackground = 0;
        const int PhaseEdge = 1;
        const int PhaseCenter = 2;
        const int PhaseList = 3;
        const int PhaseSecondary = 4;

        enum PlayMode
        {
            None,
            Open,
            Close
        }

        sealed class Unit
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 BasePosition;
            public Vector3 BaseScale = Vector3.one;
            public float BaseAlpha = 1f;
            public bool AnimatePosition;
            public bool AnimateScale;
            public bool Instant;
            public bool IsBackground;
            public UIAnimationRole Role;
            public int Phase;
            public int Order;
            public int IntraIndex;
            public int ListIndex = -1;
            public Vector2 EdgeDir;
            public Vector2 Offset;
            public Vector3 ScaleMul = Vector3.one;
            public float Alpha = 1f;
            public Vector2 FromOffset;
            public Vector2 ToOffset;
            public Vector3 FromScale = Vector3.one;
            public Vector3 ToScale = Vector3.one;
            public float FromAlpha = 1f;
            public float ToAlpha = 1f;
            public float Delay;
            public float Duration = 0.2f;
            public AnimationCurve Curve;
        }

        [SerializeField] RectTransform windowRoot;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] UIWindowChoreographyConfig overrideConfig;

        static UIWindowChoreographyConfig _fallbackConfig;
        readonly List<Unit> _units = new List<Unit>(16);
        readonly HashSet<Transform> _occupied = new HashSet<Transform>();
        UIWindowChoreographyConfig _config;
        PlayMode _mode;
        bool _closing;
        bool _unitsBuilt;
        bool _interactionEnabled;
        float _elapsed;
        float _totalDuration;
        Action _onCloseComplete;

        public bool IsPlaying => _mode != PlayMode.None;
        public bool IsClosing => _closing;
        public bool IsSettledOpen => _mode == PlayMode.None && !_closing && gameObject.activeInHierarchy;

        public void Initialize(RectTransform root, CanvasGroup group, UIWindowChoreographyConfig config)
        {
            if (windowRoot == null)
            {
                windowRoot = root;
            }

            if (canvasGroup == null)
            {
                canvasGroup = group;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            _config = overrideConfig != null ? overrideConfig : config;
        }

        public void SetConfig(UIWindowChoreographyConfig config)
        {
            if (overrideConfig == null)
            {
                _config = config;
            }
        }

        void Awake()
        {
            if (windowRoot == null)
            {
                windowRoot = transform as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        void OnDisable()
        {
            var closing = _closing;
            var callback = _onCloseComplete;
            _mode = PlayMode.None;
            _closing = false;
            _onCloseComplete = null;
            RestoreDesigner();
            if (!closing)
            {
                SetWindowInteractable(true);
            }

            if (closing)
            {
                callback?.Invoke();
            }
        }

        public void SnapOpenStart()
        {
            _closing = false;
            _onCloseComplete = null;
            _mode = PlayMode.None;
            EnsureUnits();
            for (var i = 0; i < _units.Count; i++)
            {
                ApplyOpenStart(_units[i]);
                Write(_units[i]);
            }

            if (IsFullscreen())
            {
                SetWindowAlpha(1f);
            }

            SetWindowInteractable(false);
        }

        public void PlayOpen()
        {
            EnsureUnits();
            _closing = false;
            _onCloseComplete = null;
            _interactionEnabled = false;
            PrepareOpenTracks();
            _elapsed = 0f;
            _mode = PlayMode.Open;
            SetWindowInteractable(false);
            if (IsFullscreen())
            {
                SetWindowAlpha(1f);
            }
        }

        public void PlayClose(Action onComplete)
        {
            EnsureUnits();
            _onCloseComplete = onComplete;
            _closing = true;
            _interactionEnabled = true;
            SetWindowInteractable(false);
            PrepareCloseTracks();
            _elapsed = 0f;
            _mode = PlayMode.Close;
        }

        public void ResetToDesigner()
        {
            _mode = PlayMode.None;
            _closing = false;
            _onCloseComplete = null;
            RestoreDesigner();
            SetWindowInteractable(true);
        }

        public void Invalidate()
        {
            RestoreDesigner();
            _unitsBuilt = false;
            _units.Clear();
            _occupied.Clear();
        }

        void Update()
        {
            if (_mode == PlayMode.None)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            if (_mode == PlayMode.Open && !_interactionEnabled &&
                _elapsed >= _totalDuration * Config.InteractionEnableNormalizedTime)
            {
                _interactionEnabled = true;
                SetWindowInteractable(true);
            }

            var allDone = true;
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit.Instant && _mode == PlayMode.Open)
                {
                    continue;
                }

                var t = unit.Duration <= 0.0001f ? 1f : (_elapsed - unit.Delay) / unit.Duration;
                if (t < 0f)
                {
                    allDone = false;
                    continue;
                }

                if (t < 1f)
                {
                    allDone = false;
                }

                t = Mathf.Clamp01(t);
                var k = unit.Curve != null ? unit.Curve.Evaluate(t) : t;
                unit.Offset = Vector2.LerpUnclamped(unit.FromOffset, unit.ToOffset, k);
                unit.ScaleMul = Vector3.LerpUnclamped(unit.FromScale, unit.ToScale, k);
                unit.Alpha = Mathf.Lerp(unit.FromAlpha, unit.ToAlpha, Mathf.Clamp01(k));
                Write(unit);
            }

            if (!allDone)
            {
                return;
            }

            if (_mode == PlayMode.Open)
            {
                FinishOpen();
                return;
            }

            FinishClose();
        }

        void FinishOpen()
        {
            _mode = PlayMode.None;
            _closing = false;
            for (var i = 0; i < _units.Count; i++)
            {
                ApplyRest(_units[i]);
                Write(_units[i]);
            }

            SetWindowAlpha(1f);
            SetWindowInteractable(true);
            _interactionEnabled = true;
        }

        void FinishClose()
        {
            var callback = _onCloseComplete;
            _onCloseComplete = null;
            _mode = PlayMode.None;
            _closing = false;
            if (callback != null)
            {
                callback.Invoke();
                return;
            }

            SetWindowAlpha(0f);
        }

        void PrepareOpenTracks()
        {
            CaptureCurrentAsFrom();
            var cfg = Config;
            var intra = new int[5];
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                unit.ToOffset = Vector2.zero;
                unit.ToScale = Vector3.one;
                unit.ToAlpha = unit.BaseAlpha;
                unit.Curve = CurveFor(unit, true);
                if (unit.Instant)
                {
                    ApplyRest(unit);
                    Write(unit);
                    unit.Delay = 0f;
                    unit.Duration = 0f;
                    continue;
                }

                unit.Duration = OpenDuration(unit);
                if (unit.ListIndex >= 0)
                {
                    unit.Delay = cfg.ListPhaseDelay + unit.ListIndex * cfg.ListItemStagger;
                }
                else
                {
                    unit.Delay = PhaseDelay(unit.Phase) + intra[unit.Phase] * cfg.IntraPhaseStagger;
                    intra[unit.Phase]++;
                }
            }

            FitTotal(IsFullscreen() ? cfg.MaxFullscreenOpenDuration : cfg.MaxPopupOpenDuration);
            _totalDuration = MeasureTotal();
        }

        void PrepareCloseTracks()
        {
            CaptureCurrentAsFrom();
            var cfg = Config;
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                CloseTarget(unit);
                unit.Curve = CurveFor(unit, false);
                if (unit.Instant)
                {
                    unit.Duration = 0.12f;
                    unit.Delay = 0f;
                    continue;
                }

                unit.Duration = Mathf.Max(0.10f, OpenDuration(unit) * cfg.CloseDurationMultiplier);
                var closePhase = ClosePhase(unit.Phase);
                unit.Delay = closePhase * cfg.CloseStagger;
                if (unit.ListIndex >= 0)
                {
                    unit.Delay += unit.ListIndex * cfg.CloseStagger;
                }
            }

            FitTotal(cfg.MaxCloseDuration);
            _totalDuration = MeasureTotal();
        }

        void CloseTarget(Unit unit)
        {
            unit.ToAlpha = 0f;
            unit.ToOffset = Vector2.zero;
            unit.ToScale = Vector3.one;
            if (unit.IsBackground)
            {
                if (unit.AnimateScale && !IsFullscreen())
                {
                    var s = Config.PopupBackgroundStartScale;
                    unit.ToScale = new Vector3(s, s, 1f);
                }

                return;
            }

            if (IsEdge(unit.Role))
            {
                if (unit.AnimatePosition)
                {
                    unit.ToOffset = unit.EdgeDir * Config.EdgeOffset * Config.CloseEdgeOffsetFactor;
                }

                return;
            }

            if (unit.AnimateScale)
            {
                var s = unit.ListIndex >= 0 ? Config.ListItemStartScale : Config.CenterStartScale;
                unit.ToScale = new Vector3(s, s, 1f);
            }
        }

        float OpenDuration(Unit unit)
        {
            if (unit.IsBackground)
            {
                return Config.BackgroundFadeDuration;
            }

            if (unit.ListIndex >= 0)
            {
                return Config.ListItemDuration;
            }

            if (IsEdge(unit.Role))
            {
                return Config.EdgeDuration;
            }

            if (unit.Phase == PhaseSecondary)
            {
                return Config.CenterDuration * 0.85f;
            }

            return Config.CenterDuration;
        }

        float PhaseDelay(int phase)
        {
            switch (phase)
            {
                case PhaseBackground:
                    return 0f;
                case PhaseEdge:
                    return Config.EdgePhaseDelay;
                case PhaseCenter:
                    return Config.CenterPhaseDelay;
                case PhaseList:
                    return Config.ListPhaseDelay;
                default:
                    return Config.SecondaryPhaseDelay;
            }
        }

        static int ClosePhase(int openPhase)
        {
            switch (openPhase)
            {
                case PhaseList:
                case PhaseSecondary:
                    return 0;
                case PhaseCenter:
                    return 1;
                case PhaseEdge:
                    return 2;
                default:
                    return 3;
            }
        }

        AnimationCurve CurveFor(Unit unit, bool opening)
        {
            if (unit.ListIndex >= 0)
            {
                return Config.ListItemCurve;
            }

            if (IsEdge(unit.Role))
            {
                return Config.EdgeCurve;
            }

            if (unit.IsBackground)
            {
                return UIWindowChoreographyConfig.CreateEaseOut();
            }

            return opening ? Config.CenterCurve : UIWindowChoreographyConfig.CreateEaseIn();
        }

        void FitTotal(float cap)
        {
            var longest = MeasureTotal();
            if (longest <= cap || longest <= 0.0001f)
            {
                return;
            }

            var k = cap / longest;
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                unit.Delay *= k;
                unit.Duration = Mathf.Max(0.10f, unit.Duration * k);
            }
        }

        float MeasureTotal()
        {
            var longest = 0f;
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit.Instant)
                {
                    continue;
                }

                longest = Mathf.Max(longest, unit.Delay + unit.Duration);
            }

            return Mathf.Max(0.12f, longest);
        }

        void CaptureCurrentAsFrom()
        {
            for (var i = 0; i < _units.Count; i++)
            {
                ReadCurrent(_units[i]);
                var unit = _units[i];
                unit.FromOffset = unit.Offset;
                unit.FromScale = unit.ScaleMul;
                unit.FromAlpha = unit.Alpha;
            }
        }

        void EnsureUnits()
        {
            if (_unitsBuilt && _units.Count > 0)
            {
                return;
            }

            _units.Clear();
            _occupied.Clear();
            var root = WindowRoot;
            if (root == null)
            {
                return;
            }

            CollectBackgrounds(root);
            var explicitFound = CollectExplicit(root);
            if (!explicitFound)
            {
                CollectFallback(root);
            }

            if (!IsFullscreen() && !_occupied.Contains(root))
            {
                AddUnit(root, UIAnimationRole.FadeScale, PhaseBackground, 0, false, true, true);
            }

            _units.Sort(CompareUnits);
            _unitsBuilt = true;
            CaptureBases();
        }

        static int CompareUnits(Unit a, Unit b)
        {
            var order = a.Order.CompareTo(b.Order);
            if (order != 0)
            {
                return order;
            }

            var aIndex = a.Rect != null ? a.Rect.GetSiblingIndex() : 0;
            var bIndex = b.Rect != null ? b.Rect.GetSiblingIndex() : 0;
            return aIndex.CompareTo(bIndex);
        }

        bool CollectExplicit(RectTransform root)
        {
            var elements = root.GetComponentsInChildren<UIAnimatedElement>(true);
            var found = false;
            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                if (element == null)
                {
                    continue;
                }

                var role = element.ResolveRole();
                if (role == UIAnimationRole.Ignore)
                {
                    continue;
                }

                if (element.transform == root)
                {
                    continue;
                }

                if (HasBlockingAncestor(element.transform, root))
                {
                    continue;
                }

                if (role == UIAnimationRole.ListContainer)
                {
                    if (element.Rect != null)
                    {
                        _occupied.Add(element.Rect);
                    }

                    CollectListItems(element, root);
                    found = true;
                    continue;
                }

                var rect = element.Rect;
                if (rect == null || _occupied.Contains(rect))
                {
                    continue;
                }

                var phase = PhaseForRole(role);
                var order = element.SequenceOrder != 0 ? element.SequenceOrder : phase * 100 + rect.GetSiblingIndex();
                var edge = UIAnimatedElement.EdgeDirection(role);
                AddUnit(rect, role, phase, order, !IsPositionDrivenByLayout(rect), CanAnimateScale(rect), false);
                _units[_units.Count - 1].EdgeDir = ResolveEdgeDir(rect, role, edge);
                found = true;
            }

            return found;
        }

        void CollectListItems(UIAnimatedElement element, RectTransform window)
        {
            var itemsRoot = element.ResolveListItemsRoot();
            if (itemsRoot == null)
            {
                return;
            }

            var scroll = element.GetComponent<ScrollRect>() ?? itemsRoot.GetComponentInParent<ScrollRect>();
            var viewport = scroll != null ? scroll.viewport : null;
            var candidates = new List<RectTransform>(itemsRoot.childCount);
            for (var i = 0; i < itemsRoot.childCount; i++)
            {
                var child = itemsRoot.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf || ShouldSkipListItem(child))
                {
                    continue;
                }

                candidates.Add(child);
            }

            SortListItems(candidates, scroll);
            var animated = 0;
            var max = Mathf.Max(1, Config.MaxAnimatedListItems);
            for (var i = 0; i < candidates.Count; i++)
            {
                var child = candidates[i];
                if (_occupied.Contains(child))
                {
                    continue;
                }

                var nearVisible = !window.gameObject.activeInHierarchy ||
                                  viewport == null ||
                                  IsNearVisible(child, viewport);
                var instant = animated >= max || !nearVisible;
                var order = PhaseList * 100 + i;
                AddUnit(
                    child,
                    UIAnimationRole.FadeScale,
                    PhaseList,
                    order,
                    !IsPositionDrivenByLayout(child),
                    CanAnimateScale(child),
                    false);
                var unit = _units[_units.Count - 1];
                unit.ListIndex = instant ? -1 : animated;
                unit.Instant = instant;
                if (!instant)
                {
                    animated++;
                }
            }
        }

        void CollectFallback(RectTransform root)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child == null || _occupied.Contains(child) || ShouldSkipFallbackChild(child, root))
                {
                    continue;
                }

                var role = UIAnimatedElement.ResolveAutoRole(child);
                var button = child.GetComponent<Button>();
                var phase = button != null ? PhaseSecondary : PhaseForRole(role);
                var order = phase * 100 + child.GetSiblingIndex();
                AddUnit(child, role, phase, order, !IsPositionDrivenByLayout(child), CanAnimateScale(child), false);
                _units[_units.Count - 1].EdgeDir = ResolveEdgeDir(child, role, UIAnimatedElement.EdgeDirection(role));
            }
        }

        void CollectBackgrounds(RectTransform root)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                {
                    continue;
                }

                var marked = child.GetComponent<UIAnimatedElement>();
                if (marked != null && marked.Role != UIAnimationRole.Ignore && marked.Role != UIAnimationRole.Auto)
                {
                    continue;
                }

                if (!IsStretchFill(child) || child.GetComponent<ScrollRect>() != null)
                {
                    continue;
                }

                if (child.GetComponent<Graphic>() == null)
                {
                    continue;
                }

                AddUnit(child, UIAnimationRole.FadeScale, PhaseBackground, child.GetSiblingIndex(), false, false, true);
            }
        }

        void AddUnit(
            RectTransform rect,
            UIAnimationRole role,
            int phase,
            int order,
            bool animatePosition,
            bool animateScale,
            bool background)
        {
            if (rect == null || _occupied.Contains(rect))
            {
                return;
            }

            _occupied.Add(rect);
            _units.Add(new Unit
            {
                Rect = rect,
                Role = role,
                Phase = phase,
                Order = order,
                AnimatePosition = animatePosition,
                AnimateScale = animateScale,
                IsBackground = background,
                ScaleMul = Vector3.one,
                Alpha = 1f
            });
        }

        void CaptureBases()
        {
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                unit.Group = EnsureCanvasGroup(unit.Rect);
                unit.BasePosition = unit.Rect.anchoredPosition;
                unit.BaseScale = unit.Rect.localScale;
                unit.BaseAlpha = unit.Group != null ? unit.Group.alpha : 1f;
                if (unit.BaseAlpha <= 0.0001f)
                {
                    unit.BaseAlpha = 1f;
                }

                ApplyRest(unit);
            }
        }

        void ApplyOpenStart(Unit unit)
        {
            if (unit.Instant)
            {
                ApplyRest(unit);
                return;
            }

            unit.Alpha = 0f;
            unit.Offset = Vector2.zero;
            unit.ScaleMul = Vector3.one;
            if (unit.IsBackground)
            {
                if (unit.AnimateScale && !IsFullscreen())
                {
                    var s = Config.PopupBackgroundStartScale;
                    unit.ScaleMul = new Vector3(s, s, 1f);
                }

                return;
            }

            if (IsEdge(unit.Role) && unit.AnimatePosition)
            {
                unit.Offset = unit.EdgeDir * Config.EdgeOffset;
                return;
            }

            if (unit.AnimateScale)
            {
                var s = unit.ListIndex >= 0 ? Config.ListItemStartScale : Config.CenterStartScale;
                unit.ScaleMul = new Vector3(s, s, 1f);
            }

            if (unit.ListIndex >= 0 && unit.AnimatePosition)
            {
                unit.Offset = new Vector2(0f, Config.ListItemOffsetY);
            }
        }

        static void ApplyRest(Unit unit)
        {
            unit.Offset = Vector2.zero;
            unit.ScaleMul = Vector3.one;
            unit.Alpha = unit.BaseAlpha;
        }

        void ReadCurrent(Unit unit)
        {
            if (unit.Rect == null)
            {
                return;
            }

            unit.Offset = unit.Rect.anchoredPosition - unit.BasePosition;
            unit.ScaleMul = new Vector3(
                SafeDiv(unit.Rect.localScale.x, unit.BaseScale.x),
                SafeDiv(unit.Rect.localScale.y, unit.BaseScale.y),
                1f);
            unit.Alpha = unit.Group != null ? unit.Group.alpha : unit.BaseAlpha;
        }

        static void Write(Unit unit)
        {
            if (unit.Rect == null)
            {
                return;
            }

            if (unit.AnimatePosition)
            {
                unit.Rect.anchoredPosition = unit.BasePosition + unit.Offset;
            }

            if (unit.AnimateScale)
            {
                unit.Rect.localScale = new Vector3(
                    unit.BaseScale.x * unit.ScaleMul.x,
                    unit.BaseScale.y * unit.ScaleMul.y,
                    unit.BaseScale.z);
            }

            if (unit.Group != null)
            {
                unit.Group.alpha = unit.Alpha;
                unit.Group.blocksRaycasts = true;
            }
        }

        void RestoreDesigner()
        {
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                ApplyRest(unit);
                Write(unit);
            }

            SetWindowAlpha(1f);
        }

        void SetWindowInteractable(bool interactable)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = true;
        }

        void SetWindowAlpha(float alpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = alpha;
            canvasGroup.blocksRaycasts = true;
        }

        static CanvasGroup EnsureCanvasGroup(RectTransform rect)
        {
            var group = rect.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = rect.gameObject.AddComponent<CanvasGroup>();
            }

            return group;
        }

        static bool HasBlockingAncestor(Transform t, Transform root)
        {
            var current = t.parent;
            while (current != null && current != root)
            {
                var element = current.GetComponent<UIAnimatedElement>();
                if (element != null)
                {
                    var role = element.ResolveRole();
                    if (role != UIAnimationRole.Ignore && role != UIAnimationRole.ListContainer)
                    {
                        return true;
                    }
                }

                current = current.parent;
            }

            return false;
        }

        static int PhaseForRole(UIAnimationRole role)
        {
            if (IsEdge(role))
            {
                return PhaseEdge;
            }

            if (role == UIAnimationRole.ListContainer)
            {
                return PhaseList;
            }

            if (role == UIAnimationRole.Center || role == UIAnimationRole.FadeScale)
            {
                return PhaseCenter;
            }

            return PhaseSecondary;
        }

        static bool IsEdge(UIAnimationRole role)
        {
            return role == UIAnimationRole.Top
                || role == UIAnimationRole.Bottom
                || role == UIAnimationRole.Left
                || role == UIAnimationRole.Right;
        }

        static Vector2 ResolveEdgeDir(RectTransform rect, UIAnimationRole role, Vector2 explicitDir)
        {
            if (explicitDir.sqrMagnitude > 0.01f)
            {
                var min = rect.anchorMin;
                var max = rect.anchorMax;
                var mid = (min + max) * 0.5f;
                const float corner = 0.82f;
                var cornerX = mid.x <= 1f - corner ? -1f : mid.x >= corner ? 1f : 0f;
                var cornerY = mid.y <= 1f - corner ? -1f : mid.y >= corner ? 1f : 0f;
                if (IsEdge(role) && cornerX != 0f && cornerY != 0f)
                {
                    if (role == UIAnimationRole.Top || role == UIAnimationRole.Bottom)
                    {
                        return new Vector2(cornerX * 0.35f, explicitDir.y).normalized;
                    }

                    return new Vector2(explicitDir.x, cornerY * 0.35f).normalized;
                }

                return explicitDir;
            }

            return Vector2.zero;
        }

        static bool IsStretchFill(RectTransform rect)
        {
            return rect.anchorMin.x <= 0.02f
                && rect.anchorMin.y <= 0.02f
                && rect.anchorMax.x >= 0.98f
                && rect.anchorMax.y >= 0.98f;
        }

        bool IsFullscreen()
        {
            return WindowRoot != null && IsStretchFill(WindowRoot);
        }

        static bool IsPositionDrivenByLayout(RectTransform rect)
        {
            var parent = rect.parent as RectTransform;
            if (parent == null)
            {
                return false;
            }

            var layout = parent.GetComponent<LayoutGroup>();
            return layout != null && layout.enabled;
        }

        static bool CanAnimateScale(RectTransform rect)
        {
            return rect.GetComponent<Selectable>() == null;
        }

        static bool ShouldSkipFallbackChild(RectTransform child, RectTransform root)
        {
            if (!child.gameObject.activeSelf)
            {
                return true;
            }

            if (child.GetComponent<UIAnimatedElement>() != null)
            {
                return true;
            }

            if (child.GetComponent<Scrollbar>() != null)
            {
                return true;
            }

            var parentScroll = root.GetComponent<ScrollRect>();
            if (parentScroll != null && (child == parentScroll.viewport || child == parentScroll.content))
            {
                return true;
            }

            if (child.GetComponent<Graphic>() == null && child.childCount == 0)
            {
                return true;
            }

            return false;
        }

        static bool ShouldSkipListItem(RectTransform child)
        {
            if (child.GetComponent<Scrollbar>() != null)
            {
                return true;
            }

            var parent = child.parent as RectTransform;
            if (parent != null && child.GetComponent<Mask>() != null && IsStretchFillRelative(child, parent))
            {
                return true;
            }

            return child.GetComponent<Graphic>() == null && child.childCount == 0;
        }

        static bool IsStretchFillRelative(RectTransform child, RectTransform parent)
        {
            return IsStretchFill(child) && child.parent == parent;
        }

        static void SortListItems(List<RectTransform> items, ScrollRect scroll)
        {
            if (items.Count <= 1)
            {
                return;
            }

            var horizontal = scroll != null && scroll.horizontal && !scroll.vertical;
            var vertical = scroll != null && scroll.vertical && !scroll.horizontal;
            items.Sort((a, b) =>
            {
                if (horizontal)
                {
                    var dx = a.anchoredPosition.x.CompareTo(b.anchoredPosition.x);
                    if (dx != 0)
                    {
                        return dx;
                    }
                }
                else if (vertical)
                {
                    var dy = b.anchoredPosition.y.CompareTo(a.anchoredPosition.y);
                    if (dy != 0)
                    {
                        return dy;
                    }
                }

                return a.GetSiblingIndex().CompareTo(b.GetSiblingIndex());
            });
        }

        static bool IsNearVisible(RectTransform item, RectTransform viewport)
        {
            var itemRect = WorldRect(item);
            var viewRect = WorldRect(viewport);
            viewRect.xMin -= viewRect.width * 0.35f;
            viewRect.xMax += viewRect.width * 0.35f;
            viewRect.yMin -= viewRect.height * 0.35f;
            viewRect.yMax += viewRect.height * 0.35f;
            return itemRect.Overlaps(viewRect);
        }

        static readonly Vector3[] WorldCorners = new Vector3[4];

        static Rect WorldRect(RectTransform rect)
        {
            rect.GetWorldCorners(WorldCorners);
            var min = WorldCorners[0];
            var max = WorldCorners[2];
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static float SafeDiv(float value, float basis)
        {
            return Mathf.Abs(basis) > 0.0001f ? value / basis : 1f;
        }

        RectTransform WindowRoot
        {
            get
            {
                if (windowRoot == null)
                {
                    windowRoot = transform as RectTransform;
                }

                return windowRoot;
            }
        }

        UIWindowChoreographyConfig Config
        {
            get
            {
                if (overrideConfig != null)
                {
                    return overrideConfig;
                }

                if (_config != null)
                {
                    return _config;
                }

                if (UIManager.Instance != null && UIManager.Instance.DefaultWindowChoreographyConfig != null)
                {
                    return UIManager.Instance.DefaultWindowChoreographyConfig;
                }

                if (_fallbackConfig == null)
                {
                    _fallbackConfig = ScriptableObject.CreateInstance<UIWindowChoreographyConfig>();
                }

                return _fallbackConfig;
            }
        }
    }
}
