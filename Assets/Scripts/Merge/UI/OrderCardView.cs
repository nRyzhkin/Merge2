using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class OrderCardView : MonoBehaviour
    {
        enum Motion
        {
            None,
            Appear,
            Exit,
            Slide
        }

        [SerializeField] Button cardButton;
        [SerializeField] GameObject completeVisual;
        [SerializeField] Image completeImage;
        [SerializeField] Outline completeOutline;
        [SerializeField] Image rewardIcon;
        [SerializeField] TMP_Text rewardText;
        [SerializeField] GameObject notifyBadge;
        [SerializeField] OrderRequirementSlotView[] slots;

        RectTransform _rect;
        CanvasGroup _group;
        CanvasGroup _completeGroup;
        LayoutElement _layoutElement;
        Vector3 _baseScale = Vector3.one;
        bool _baseScaleCaptured;
        int _boundOrderId;
        bool _ready;
        bool _boundClick;
        bool _holdCompleteVisual;
        float _readyPulseElapsed;
        float _completeElapsed = -1f;
        float _completeOverlay = 1f;
        float _completeOverlayFrom;
        float _completeOverlayTo = 1f;
        float _completeOverlayElapsed = -1f;
        Motion _motion = Motion.None;
        float _motionElapsed;
        float _motionDelay;
        Vector2 _slideFrom;
        Vector2 _slideTo;
        Vector2 _exitFrom;

        public int BoundOrderId => _boundOrderId;
        public bool HoldsCompletedVisual => _holdCompleteVisual;
        public bool IsCompletionPlaying => _completeElapsed >= 0f;
        public bool IsLayoutAnimating => _motion == Motion.Appear || _motion == Motion.Exit || _motion == Motion.Slide;
        public bool IsExiting => _motion == Motion.Exit;
        public event Action<OrderCardView> ExitFinished;
        public RectTransform Rect
        {
            get
            {
                if (_rect == null)
                {
                    _rect = transform as RectTransform;
                }

                return _rect;
            }
        }

        public void BindLocal()
        {
            if (_rect == null)
            {
                _rect = transform as RectTransform;
            }

            CaptureBaseScaleOnce();
            EnsureCanvasGroup();
            EnsureCardButton();
            BindCompleteVisual();
            BindReward();
            DisableDescendantRaycasts();

            if (notifyBadge == null)
            {
                var notify = transform.Find("Notify_New");
                if (notify != null)
                {
                    notifyBadge = notify.gameObject;
                }
            }

            if (notifyBadge != null)
            {
                notifyBadge.SetActive(false);
            }

            EnsureSlots();
            EnsureSlotClicks();
            EnsureClickBound();
        }

        public void Present(int orderId, OrderDefinition order, MergeItemDatabase itemDatabase, bool ready)
        {
            BindLocal();
            ApplyBoundContent(orderId, order, itemDatabase, ready);
            if (_group != null)
            {
                _group.alpha = 1f;
                _group.blocksRaycasts = true;
            }

            RestoreBaseScale();
            _motion = Motion.None;
        }

        public void ShowEmptyImmediate()
        {
            BindLocal();
            _boundOrderId = 0;
            _ready = false;
            _holdCompleteVisual = false;
            _motion = Motion.None;
            _completeElapsed = -1f;
            _completeOverlayElapsed = -1f;
            RestoreBaseScale();
            SetLayoutIgnored(false);
            if (_group != null)
            {
                _group.alpha = 1f;
                _group.blocksRaycasts = false;
            }

            ApplyReadyState(false, animate: false);
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public void PlayAppear(float delay)
        {
            BindLocal();
            _motion = Motion.Appear;
            _motionDelay = Mathf.Max(0f, delay);
            _motionElapsed = -_motionDelay;
            if (_group != null)
            {
                _group.alpha = 1f;
                _group.blocksRaycasts = false;
            }

            SetScaleMul(0.9f);
            gameObject.SetActive(true);
            enabled = true;
        }

        public void PlayExit()
        {
            BindLocal();
            _holdCompleteVisual = true;
            _ready = false;
            if (cardButton != null)
            {
                cardButton.interactable = false;
            }

            SetSlotPointerEnabled(false);
            SetCompleteVisual(true, animate: false);
            SetLayoutIgnored(true);
            _motion = Motion.Exit;
            _motionElapsed = 0f;
            _motionDelay = 0f;
            _exitFrom = Rect != null ? Rect.anchoredPosition : Vector2.zero;
            if (_group != null)
            {
                _group.alpha = 1f;
                _group.blocksRaycasts = false;
            }

            gameObject.SetActive(true);
            enabled = true;
        }

        public void PlaySlide(Vector2 from, Vector2 to, float delay = 0f)
        {
            if (Rect == null)
            {
                return;
            }

            _motion = Motion.Slide;
            _motionDelay = Mathf.Max(0f, delay);
            _motionElapsed = -_motionDelay;
            _slideFrom = from;
            _slideTo = to;
            Rect.anchoredPosition = from;
            if (_group != null)
            {
                _group.alpha = 1f;
            }

            enabled = true;
        }

        public void SetLayoutIgnored(bool ignore)
        {
            if (_layoutElement == null)
            {
                _layoutElement = GetComponent<LayoutElement>();
                if (_layoutElement == null)
                {
                    _layoutElement = gameObject.AddComponent<LayoutElement>();
                }
            }

            _layoutElement.ignoreLayout = ignore;
        }

        public bool TryGetRequirementLayerPosition(int requirementIndex, BoardDragView dragView, out Vector2 layerPosition)
        {
            layerPosition = Vector2.zero;
            if (dragView == null || slots == null || requirementIndex < 0 || requirementIndex >= slots.Length)
            {
                return false;
            }

            var slot = slots[requirementIndex];
            if (slot == null)
            {
                return false;
            }

            layerPosition = dragView.WorldToLayer(slot.GetWorldCenter());
            return true;
        }

        public Vector2 GetRewardLayerPosition(BoardDragView dragView)
        {
            if (dragView == null)
            {
                return Vector2.zero;
            }

            var target = rewardText != null
                ? rewardText.rectTransform
                : rewardIcon != null
                    ? rewardIcon.rectTransform
                    : Rect;
            if (target == null)
            {
                return Vector2.zero;
            }

            return dragView.WorldToLayer(target.TransformPoint(target.rect.center));
        }

        public void HoldCompletedVisual()
        {
            BindLocal();
            _holdCompleteVisual = true;
            if (cardButton != null)
            {
                cardButton.interactable = false;
            }

            SetSlotPointerEnabled(false);
            SetCompleteVisual(true, animate: false);
            enabled = true;
        }

        public void ReleaseCompletedHold()
        {
            if (!_holdCompleteVisual)
            {
                return;
            }

            _holdCompleteVisual = false;
            ApplyReadyState(_ready, false);
        }

        public void PlayCompletion()
        {
            BindLocal();
            _completeElapsed = 0f;
            _holdCompleteVisual = true;
            if (cardButton != null)
            {
                cardButton.interactable = false;
            }

            SetCompleteVisual(true, animate: false);
            enabled = true;
        }

        public void SetReady(bool ready, bool animate)
        {
            if (_holdCompleteVisual)
            {
                return;
            }

            if (_ready == ready && !animate)
            {
                ApplyReadyState(ready, false);
                return;
            }

            if (_ready == ready)
            {
                return;
            }

            _ready = ready;
            ApplyReadyState(ready, animate);
        }

        public bool Tick(float dt, OrderDatabase database)
        {
            var busy = false;
            if (_completeElapsed >= 0f)
            {
                _completeElapsed += dt;
                var duration = database != null ? Mathf.Max(0.16f, database.CompletePunchDuration) : 0.3f;
                var peak = database != null ? database.CompletePunchScale : 1.03f;
                var t = duration <= 0f ? 1f : Mathf.Clamp01(_completeElapsed / duration);
                var punch = 1f + (peak - 1f) * Mathf.Sin(t * Mathf.PI);
                if (Rect != null)
                {
                    Rect.localScale = _baseScale * punch;
                }

                if (t < 1f)
                {
                    busy = true;
                }
                else
                {
                    RestoreBaseScale();
                    _completeElapsed = -1f;
                }
            }

            if (TickOverlay(dt, database))
            {
                busy = true;
            }

            if (TickMotion(dt, database))
            {
                busy = true;
            }

            if (_completeElapsed >= 0f || _motion != Motion.None || _holdCompleteVisual)
            {
                return busy;
            }

            if (!_ready || Rect == null || !gameObject.activeInHierarchy)
            {
                RestoreBaseScale();
                return busy;
            }

            var pulseDuration = database != null ? Mathf.Max(0.4f, database.ReadyPulseDuration) : 1.2f;
            var pulseScale = database != null ? database.ReadyPulseScale : 1.025f;
            _readyPulseElapsed += dt;
            var wave = 0.5f + 0.5f * Mathf.Sin((_readyPulseElapsed / pulseDuration) * Mathf.PI * 2f);
            Rect.localScale = _baseScale * Mathf.Lerp(1f, pulseScale, wave);
            return true;
        }

        void Awake()
        {
            BindLocal();
        }

        void LateUpdate()
        {
            var db = OrderSystem.Current != null ? OrderSystem.Current.Database : null;
            Tick(db != null ? db.GetDeltaTime() : Time.unscaledDeltaTime, db);
        }

        void OnDestroy()
        {
            UnsubscribeSlotClicks();
            if (cardButton != null && _boundClick)
            {
                cardButton.onClick.RemoveListener(OnCompleteClicked);
            }
        }

        void EnsureClickBound()
        {
            if (cardButton == null || _boundClick)
            {
                return;
            }

            cardButton.onClick.AddListener(OnCompleteClicked);
            _boundClick = true;
        }

        void OnCompleteClicked()
        {
            if (_boundOrderId == 0 || !_ready || _holdCompleteVisual || _motion == Motion.Exit)
            {
                return;
            }

            OrderSystem.Current?.TryCompleteOrder(_boundOrderId);
        }

        void ApplyBoundContent(int orderId, OrderDefinition order, MergeItemDatabase itemDatabase, bool ready)
        {
            _boundOrderId = orderId;
            _ready = ready;
            _holdCompleteVisual = false;
            gameObject.SetActive(true);
            RefreshBoundContent(order, itemDatabase);
            ApplyReadyState(ready, animate: false);
        }

        void RefreshBoundContent(OrderDefinition order, MergeItemDatabase itemDatabase)
        {
            var requirementCount = order != null && order.requirements != null ? order.requirements.Count : 0;
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (i >= requirementCount)
                {
                    slot.Hide();
                    continue;
                }

                var requirement = order.requirements[i];
                Sprite icon = null;
                if (itemDatabase != null &&
                    itemDatabase.TryGetById(requirement.itemId, out var item) &&
                    item != null)
                {
                    icon = item.Icon;
                }

                slot.Show(icon, requirement.amount, requirement.itemId);
            }

            if (rewardText != null)
            {
                rewardText.text = order != null && order.coinReward > 0
                    ? order.coinReward.ToString()
                    : string.Empty;
            }

            ApplyRewardIcon();
        }

        void ApplyRewardIcon()
        {
            if (rewardIcon == null)
            {
                return;
            }

            var sprite = Icons.Get(IconTokens.Coin);
            if (sprite == null)
            {
                return;
            }

            rewardIcon.sprite = sprite;
            rewardIcon.enabled = true;
            rewardIcon.preserveAspect = true;
        }

        void ApplyReadyState(bool ready, bool animate)
        {
            if (cardButton != null)
            {
                cardButton.interactable = ready && !_holdCompleteVisual;
            }

            SetSlotPointerEnabled(!ready && !_holdCompleteVisual);

            if (completeOutline != null)
            {
                completeOutline.enabled = ready;
            }

            SetCompleteVisual(ready, animate);
            if (!ready)
            {
                _readyPulseElapsed = 0f;
                if (_motion == Motion.None && _completeElapsed < 0f)
                {
                    RestoreBaseScale();
                }
            }
        }

        void SetCompleteVisual(bool visible, bool animate)
        {
            if (completeVisual == null)
            {
                return;
            }

            EnsureCompleteGroup();
            _completeOverlayTo = visible ? 1f : 0f;
            if (!completeVisual.activeSelf && (visible || animate))
            {
                completeVisual.SetActive(true);
            }

            if (!animate)
            {
                _completeOverlay = _completeOverlayTo;
                _completeOverlayElapsed = -1f;
                ApplyCompleteOverlayVisual(_completeOverlay);
                if (completeVisual.activeSelf != visible)
                {
                    completeVisual.SetActive(visible);
                }

                return;
            }

            _completeOverlayFrom = _completeOverlay;
            _completeOverlayElapsed = 0f;
        }

        bool TickOverlay(float dt, OrderDatabase database)
        {
            if (_completeOverlayElapsed < 0f || _holdCompleteVisual)
            {
                return false;
            }

            _completeOverlayElapsed += dt;
            var duration = database != null ? Mathf.Max(0.08f, database.CompleteOverlayDuration) : 0.22f;
            var t = duration <= 0f ? 1f : Mathf.Clamp01(_completeOverlayElapsed / duration);
            _completeOverlay = Mathf.LerpUnclamped(_completeOverlayFrom, _completeOverlayTo, EaseOutCubic(t));
            ApplyCompleteOverlayVisual(_completeOverlay);
            if (t < 1f)
            {
                return true;
            }

            _completeOverlayElapsed = -1f;
            if (_completeOverlayTo <= 0.01f && completeVisual != null && completeVisual.activeSelf)
            {
                completeVisual.SetActive(false);
            }

            return false;
        }

        void ApplyCompleteOverlayVisual(float t)
        {
            if (_completeGroup != null)
            {
                _completeGroup.alpha = t;
                _completeGroup.blocksRaycasts = false;
            }
        }

        bool TickMotion(float dt, OrderDatabase database)
        {
            if (_motion == Motion.None)
            {
                return false;
            }

            _motionElapsed += dt;
            if (_motionElapsed < 0f)
            {
                if (_motion == Motion.Slide && Rect != null)
                {
                    Rect.anchoredPosition = _slideFrom;
                }

                return true;
            }

            if (_motion == Motion.Slide)
            {
                var duration = database != null ? Mathf.Max(0.16f, database.CardSlideDuration) : 0.4f;
                var t = Mathf.Clamp01(_motionElapsed / duration);
                if (Rect != null)
                {
                    Rect.anchoredPosition = Vector2.LerpUnclamped(_slideFrom, _slideTo, EaseInOutCubic(t));
                }

                if (t < 1f)
                {
                    return true;
                }

                if (Rect != null)
                {
                    Rect.anchoredPosition = _slideTo;
                }

                _motion = Motion.None;
                return false;
            }

            if (_motion == Motion.Exit)
            {
                var duration = database != null ? Mathf.Max(0.2f, database.CardHideDuration) : 0.48f;
                var t = Mathf.Clamp01(_motionElapsed / duration);
                var move = EaseInOutCubic(t);
                var fade = SmoothStep(t);
                SetScaleMul(Mathf.LerpUnclamped(1f, 0.94f, fade));
                if (Rect != null)
                {
                    Rect.anchoredPosition = _exitFrom + new Vector2(72f * move, 0f);
                }

                if (_group != null)
                {
                    _group.alpha = 1f - fade;
                }

                if (t < 1f)
                {
                    return true;
                }

                _motion = Motion.None;
                ShowEmptyImmediate();
                ExitFinished?.Invoke(this);
                return false;
            }

            var appearDuration = database != null ? Mathf.Max(0.08f, database.CardAppearDuration) : 0.32f;
            var appearScale = database != null ? database.CardAppearScale : 0.9f;
            var appearT = Mathf.Clamp01(_motionElapsed / appearDuration);
            SetScaleMul(Mathf.LerpUnclamped(appearScale, 1f, EaseOutBack(appearT)));
            if (appearT < 1f)
            {
                return true;
            }

            RestoreBaseScale();
            if (_group != null)
            {
                _group.alpha = 1f;
                _group.blocksRaycasts = true;
            }

            _motion = Motion.None;
            return false;
        }

        void CaptureBaseScaleOnce()
        {
            if (_baseScaleCaptured || Rect == null)
            {
                return;
            }

            var scale = Rect.localScale;
            _baseScale = scale.x > 0.01f && scale.y > 0.01f ? scale : Vector3.one;
            _baseScaleCaptured = true;
        }

        void RestoreBaseScale()
        {
            if (Rect != null)
            {
                Rect.localScale = _baseScale;
            }
        }

        void SetScaleMul(float mul)
        {
            if (Rect != null)
            {
                Rect.localScale = _baseScale * mul;
            }
        }

        void EnsureCanvasGroup()
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
                if (_group == null)
                {
                    _group = gameObject.AddComponent<CanvasGroup>();
                }
            }

            _group.interactable = true;
        }

        void EnsureCardButton()
        {
            var nested = GetComponentsInChildren<Button>(true);
            for (var i = 0; i < nested.Length; i++)
            {
                if (nested[i] != null && nested[i].gameObject != gameObject)
                {
                    nested[i].onClick.RemoveAllListeners();
                    nested[i].enabled = false;
                    nested[i].interactable = false;
                }
            }

            cardButton = GetComponent<Button>();
            if (cardButton == null)
            {
                cardButton = gameObject.AddComponent<Button>();
            }

            cardButton.transition = Selectable.Transition.None;
            var graphic = GetComponent<Image>();
            if (graphic == null)
            {
                graphic = gameObject.AddComponent<Image>();
                graphic.color = new Color(1f, 1f, 1f, 0f);
            }

            graphic.raycastTarget = true;
            cardButton.targetGraphic = graphic;
        }

        void BindCompleteVisual()
        {
            if (completeVisual == null)
            {
                var complete = FindNamedDescendant(transform, "Complete");
                if (complete != null)
                {
                    completeVisual = complete.gameObject;
                    completeImage = complete.GetComponent<Image>();
                    completeOutline = complete.GetComponent<Outline>();
                }
            }
            else
            {
                if (completeImage == null)
                {
                    completeImage = completeVisual.GetComponent<Image>();
                }

                if (completeOutline == null)
                {
                    completeOutline = completeVisual.GetComponent<Outline>();
                }
            }

            EnsureCompleteGroup();
        }

        void EnsureCompleteGroup()
        {
            if (completeVisual == null)
            {
                return;
            }

            if (_completeGroup == null)
            {
                _completeGroup = completeVisual.GetComponent<CanvasGroup>();
                if (_completeGroup == null)
                {
                    _completeGroup = completeVisual.AddComponent<CanvasGroup>();
                }
            }

            _completeGroup.interactable = false;
            _completeGroup.blocksRaycasts = false;
        }

        void DisableDescendantRaycasts()
        {
            var graphics = GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null || graphic.gameObject == gameObject)
                {
                    continue;
                }

                if (graphic.GetComponentInParent<OrderRequirementSlotView>() != null)
                {
                    continue;
                }

                graphic.raycastTarget = false;
            }
        }

        void BindReward()
        {
            var content = FindContentRoot();
            if (rewardText == null && content != null)
            {
                var reward = content.Find("Reward/Text_Amount");
                if (reward != null)
                {
                    rewardText = reward.GetComponent<TMP_Text>();
                }
            }

            if (rewardIcon == null && content != null)
            {
                var icon = content.Find("Reward/Icon");
                if (icon != null)
                {
                    rewardIcon = icon.GetComponent<Image>();
                }
            }
        }

        Transform FindContentRoot()
        {
            Transform first = null;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null || child.name != "Nomal")
                {
                    continue;
                }

                if (first == null)
                {
                    first = child;
                }

                if (child.Find("OrderItems") != null || child.Find("Reward") != null)
                {
                    return child;
                }
            }

            return first != null ? first : transform.Find("Nomal");
        }

        static Transform FindNamedDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            var direct = root.Find(objectName);
            if (direct != null)
            {
                return direct;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindNamedDescendant(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        void EnsureSlots()
        {
            if (slots != null && slots.Length > 0)
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    slots[i]?.BindLocal();
                }

                return;
            }

            var content = FindContentRoot();
            var itemsRoot = content != null ? content.Find("OrderItems") : null;
            if (itemsRoot == null)
            {
                slots = Array.Empty<OrderRequirementSlotView>();
                return;
            }

            var found = new OrderRequirementSlotView[itemsRoot.childCount];
            var count = 0;
            for (var i = 0; i < itemsRoot.childCount; i++)
            {
                var child = itemsRoot.GetChild(i);
                if (child == null || !child.name.StartsWith("Item"))
                {
                    continue;
                }

                var slot = child.GetComponent<OrderRequirementSlotView>();
                if (slot == null)
                {
                    slot = child.gameObject.AddComponent<OrderRequirementSlotView>();
                }

                slot.BindLocal();
                found[count] = slot;
                count++;
            }

            slots = new OrderRequirementSlotView[count];
            for (var i = 0; i < count; i++)
            {
                slots[i] = found[i];
            }
        }

        void EnsureSlotClicks()
        {
            if (slots == null)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.Clicked -= OnRequirementClicked;
                slot.Clicked += OnRequirementClicked;
            }
        }

        void UnsubscribeSlotClicks()
        {
            if (slots == null)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].Clicked -= OnRequirementClicked;
                }
            }
        }

        void OnRequirementClicked(OrderRequirementSlotView slot)
        {
            if (slot == null || slot.ItemId == 0 || _ready || _holdCompleteVisual || _motion == Motion.Exit)
            {
                return;
            }

            OrderSystem.Current?.InspectRequirement(slot.ItemId);
        }

        void SetSlotPointerEnabled(bool enabled)
        {
            if (slots == null)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                slots[i]?.SetPointerEnabled(enabled);
            }
        }

        static float EaseOutCubic(float t)
        {
            var inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        static float EaseInOutCubic(float t)
        {
            if (t < 0.5f)
            {
                return 4f * t * t * t;
            }

            var inv = -2f * t + 2f;
            return 1f - inv * inv * inv * 0.5f;
        }

        static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static float EaseInCubic(float t)
        {
            return t * t * t;
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.70158f;
            var p = t - 1f;
            return 1f + p * p * ((c + 1f) * p + c);
        }
    }
}
