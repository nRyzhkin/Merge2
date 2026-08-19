using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class BoardCellView : MonoBehaviour
    {
        public const string ItemImageName = "ItemImage";
        public const string BlockerImageName = "BlockerImage";
        public const string LockOverlayImageName = "LockOverlayImage";
        public const string FxRootName = "FxRoot";

        [SerializeField] int index = -1;
        [SerializeField] Image itemImage;
        [SerializeField] Image blockerImage;
        [SerializeField] Image lockOverlayImage;
        [SerializeField] RectTransform fxRoot;
        [SerializeField] BoardItemAnimator itemAnimator;
        [SerializeField] GeneratorChargeIndicator generatorChargeIndicator;
        bool _hideItemForDrag;
        bool _itemPresentationSuppressed;
        bool _generatorExhaustedVisual;
        Color _itemBaseColor = Color.white;
        bool _itemBaseColorCaptured;

        Color _cobwebBaseColor = Color.white;
        Vector3 _cobwebBaseScale = Vector3.one;
        bool _cobwebBaseCaptured;
        bool _cobwebHover;
        bool _cobwebBreaking;
        float _cobwebScale = 1f;
        float _cobwebAlpha = 1f;
        float _cobwebScaleFrom = 1f;
        float _cobwebScaleTo = 1f;
        float _cobwebAlphaFrom = 1f;
        float _cobwebAlphaTo = 1f;
        float _cobwebElapsed;
        float _cobwebDuration = 0.1f;
        AnimationCurve _cobwebCurve;
        BoardCobwebAnimationConfig _cobwebConfig;

        bool _boxRevealing;
        BoardBoxAnimationConfig _boxConfig;
        Vector3 _boxBaseScale = Vector3.one;
        Quaternion _boxBaseRotation = Quaternion.identity;
        Color _boxBaseColor = Color.white;
        Vector2 _boxBaseOffsetMin;
        Vector2 _boxBaseOffsetMax;
        bool _boxBaseCaptured;
        float _boxElapsed;
        float _boxDuration = 0.08f;
        float _boxScaleFrom = 1f;
        float _boxScaleTo = 1f;
        float _boxScaleYFrom = 1f;
        float _boxScaleYTo = 1f;
        float _boxOffsetYFrom;
        float _boxOffsetYTo;
        float _boxAlphaFrom = 1f;
        float _boxAlphaTo = 1f;
        float _boxRotationFrom;
        float _boxRotationTo;
        AnimationCurve _boxCurve;
        bool _boxBreaking;

        public int Index => index;
        public bool IsBoxRevealPlaying => _boxRevealing;
        public Image ItemImage => itemImage;
        public Image BlockerImage => blockerImage;
        public Image LockOverlayImage => lockOverlayImage;
        public RectTransform FxRoot => fxRoot;
        public BoardItemAnimator ItemAnimator => itemAnimator;
        public GeneratorChargeIndicator GeneratorChargeIndicator => generatorChargeIndicator;
        public bool IsItemPresentationSuppressed => _itemPresentationSuppressed;
        public bool IsItemHiddenForDrag => _hideItemForDrag;

        public string DebugDescribePresentation()
        {
            var blockerOn = blockerImage != null && blockerImage.gameObject.activeSelf;
            var itemOn = itemImage != null && itemImage.gameObject.activeSelf;
            var cobwebOn = lockOverlayImage != null && lockOverlayImage.gameObject.activeSelf;
            return
                $"boxReveal={_boxRevealing} boxBreak={_boxBreaking} cobwebBreak={_cobwebBreaking} " +
                $"suppressed={_itemPresentationSuppressed} hideDrag={_hideItemForDrag} " +
                $"transient={IsTransientAnimationRunning()} itemOn={itemOn} blockerOn={blockerOn} cobwebOn={cobwebOn}";
        }

        public void SetIndex(int cellIndex)
        {
            index = cellIndex;
        }

        public void BindLayers(Image item, Image blocker, Image lockOverlay, RectTransform fx)
        {
            itemImage = item;
            blockerImage = blocker;
            lockOverlayImage = lockOverlay;
            fxRoot = fx;
        }

        public void BindAnimator(BoardItemAnimator animator)
        {
            itemAnimator = animator;
        }

        public void BindGeneratorChargeIndicator(GeneratorChargeIndicator indicator)
        {
            generatorChargeIndicator = indicator;
        }

        public void ApplyGeneratorPresentation(bool showRechargeSlider, bool exhausted, float rechargeProgress)
        {
            if (generatorChargeIndicator != null)
            {
                generatorChargeIndicator.SetVisible(showRechargeSlider);
                if (showRechargeSlider)
                {
                    generatorChargeIndicator.SetProgress(rechargeProgress);
                }
            }

            if (_generatorExhaustedVisual == exhausted)
            {
                return;
            }

            _generatorExhaustedVisual = exhausted;
            ApplyItemBrightness();
        }

        public void SetItemPresentationSuppressed(bool suppressed)
        {
            _itemPresentationSuppressed = suppressed;
            if (suppressed)
            {
                SetActiveSafe(itemImage, false);
                HideCobwebOverlayImmediate();
            }
        }

        public void SetHideItemForDrag(bool hide)
        {
            _hideItemForDrag = hide;
            if (hide || _itemPresentationSuppressed)
            {
                SetActiveSafe(itemImage, false);
                return;
            }

            if (itemImage != null && itemImage.sprite != null)
            {
                SetActiveSafe(itemImage, true);
            }
        }

        public void RevealItemAfterMerge()
        {
            _hideItemForDrag = false;
            if (_itemPresentationSuppressed)
            {
                SetActiveSafe(itemImage, false);
                return;
            }

            if (itemImage != null && itemImage.sprite != null)
            {
                SetActiveSafe(itemImage, true);
            }
        }

        public bool IsTransientAnimationRunning()
        {
            return itemAnimator != null && itemAnimator.IsTransientAnimationRunning();
        }

        public bool IsCobwebBreakPlaying => _cobwebBreaking;

        public void CancelTransientPresentationForPickup()
        {
            if (itemAnimator != null)
            {
                itemAnimator.CancelTransientAnimationAndAdoptCurrentVisualState();
            }

            HideCobwebOverlayImmediate();
            if (_boxRevealing)
            {
                ClearBoxRevealPresentation();
            }

            _hideItemForDrag = false;
            if (_itemPresentationSuppressed)
            {
                SetActiveSafe(itemImage, false);
            }
        }

        public void SetCobwebUnlockHover(bool active, BoardCobwebAnimationConfig config)
        {
            if (lockOverlayImage == null)
            {
                return;
            }

            CaptureCobwebBase();
            _cobwebConfig = config;
            _cobwebBreaking = false;
            _cobwebHover = active;
            _cobwebScaleFrom = _cobwebScale;
            _cobwebAlphaFrom = _cobwebAlpha;
            _cobwebScaleTo = active ? (config != null ? config.WebHoverScale : 1.03f) : 1f;
            _cobwebAlphaTo = active ? (config != null ? config.WebHoverAlpha : 0.88f) : _cobwebBaseColor.a;
            _cobwebElapsed = 0f;
            _cobwebDuration = config != null ? Mathf.Max(0.01f, config.WebHoverDuration) : 0.10f;
            _cobwebCurve = null;
            ApplyCobwebVisual();
        }

        public void PlayCobwebBreak(BoardCobwebAnimationConfig config)
        {
            if (lockOverlayImage == null)
            {
                return;
            }

            CaptureCobwebBase();
            _cobwebConfig = config;
            _cobwebHover = false;
            _cobwebBreaking = true;
            _cobwebScaleFrom = _cobwebScale;
            _cobwebAlphaFrom = _cobwebAlpha;
            _cobwebScaleTo = config != null ? config.WebBreakScale : 1.12f;
            _cobwebAlphaTo = 0f;
            _cobwebElapsed = 0f;
            _cobwebDuration = config != null ? Mathf.Max(0.01f, config.WebBreakDuration) : 0.18f;
            _cobwebCurve = config != null ? config.WebBreakCurve : null;
            if (_itemPresentationSuppressed)
            {
                HideCobwebOverlayImmediate();
                return;
            }

            SetActiveSafe(lockOverlayImage, true);
        }

        public void ResetCobwebVisualImmediate()
        {
            _cobwebHover = false;
            _cobwebBreaking = false;
            _cobwebElapsed = 0f;
            if (_cobwebBaseCaptured)
            {
                _cobwebScale = 1f;
                _cobwebAlpha = _cobwebBaseColor.a;
                ApplyCobwebVisual();
            }
        }

        public void HideCobwebOverlayImmediate()
        {
            ResetCobwebVisualImmediate();
            SetActiveSafe(lockOverlayImage, false);
        }

        public void BeginBoxRevealPresentation(BoardBoxAnimationConfig config)
        {
            _boxConfig = config;
            _boxRevealing = true;
            CaptureBoxBase();
            SetActiveSafe(lockOverlayImage, false);
            SetActiveSafe(blockerImage, true);
        }

        public void PlayBoxAnticipation(BoardBoxAnimationConfig config)
        {
            if (blockerImage == null)
            {
                return;
            }

            BeginBoxRevealPresentation(config);
            _boxBreaking = false;
            _boxElapsed = 0f;
            _boxDuration = config != null ? Mathf.Max(0.01f, config.AnticipationDuration) : 0.08f;
            _boxCurve = config != null ? config.AnticipationCurve : null;
            _boxScaleFrom = 1f;
            _boxScaleYFrom = 1f;
            _boxScaleTo = config != null ? config.AnticipationScale.x : 1.04f;
            _boxScaleYTo = config != null ? config.AnticipationScale.y : 0.96f;
            _boxOffsetYFrom = 0f;
            var cellHeight = ((RectTransform)transform).rect.height;
            _boxOffsetYTo = cellHeight * (config != null ? config.AnticipationOffsetY : 0.03f);
            _boxAlphaFrom = _boxBaseColor.a;
            _boxAlphaTo = _boxBaseColor.a;
            _boxRotationFrom = 0f;
            _boxRotationTo = 0f;
            ApplyBoxVisual();
        }

        public void PlayBoxBreak(BoardBoxAnimationConfig config)
        {
            if (blockerImage == null)
            {
                return;
            }

            _boxConfig = config;
            _boxBreaking = true;
            _boxElapsed = 0f;
            _boxDuration = config != null ? Mathf.Max(0.01f, config.BreakDuration) : 0.16f;
            _boxCurve = config != null ? config.BreakCurve : null;
            _boxScaleFrom = _boxScaleTo;
            _boxScaleYFrom = _boxScaleYTo;
            var peak = config != null ? config.BreakScale : 1.1f;
            _boxScaleTo = peak;
            _boxScaleYTo = peak;
            _boxOffsetYFrom = _boxOffsetYTo;
            _boxOffsetYTo = _boxOffsetYFrom;
            _boxAlphaFrom = blockerImage.color.a;
            _boxAlphaTo = 0f;
            _boxRotationFrom = 0f;
            _boxRotationTo = (config != null ? config.BreakRotationDegrees : 3f) * (index % 2 == 0 ? 1f : -1f);
            ApplyBoxVisual();
        }

        public void ClearBoxRevealPresentation()
        {
            _boxRevealing = false;
            _boxBreaking = false;
            _boxElapsed = 0f;
            RestoreBoxBasePose();
            SetActiveSafe(blockerImage, false);
        }

        void Update()
        {
            TickBoxReveal();
            if (!_cobwebHover && !_cobwebBreaking)
            {
                return;
            }

            var dt = _cobwebConfig != null ? _cobwebConfig.GetDeltaTime() : Time.unscaledDeltaTime;
            _cobwebElapsed += dt;
            var t = _cobwebDuration <= 0f ? 1f : Mathf.Clamp01(_cobwebElapsed / _cobwebDuration);
            var curved = _cobwebCurve != null ? _cobwebCurve.Evaluate(t) : EaseInOut(t);
            _cobwebScale = Mathf.LerpUnclamped(_cobwebScaleFrom, _cobwebScaleTo, curved);
            _cobwebAlpha = Mathf.LerpUnclamped(_cobwebAlphaFrom, _cobwebAlphaTo, curved);
            ApplyCobwebVisual();

            if (t < 1f)
            {
                return;
            }

            if (_cobwebBreaking)
            {
                _cobwebBreaking = false;
                SetActiveSafe(lockOverlayImage, false);
                ResetCobwebVisualImmediate();
            }
        }

        void CaptureCobwebBase()
        {
            if (_cobwebBaseCaptured || lockOverlayImage == null)
            {
                return;
            }

            _cobwebBaseColor = lockOverlayImage.color;
            _cobwebBaseScale = lockOverlayImage.rectTransform.localScale;
            _cobwebScale = 1f;
            _cobwebAlpha = _cobwebBaseColor.a;
            _cobwebBaseCaptured = true;
        }

        void ApplyCobwebVisual()
        {
            if (lockOverlayImage == null)
            {
                return;
            }

            var rect = lockOverlayImage.rectTransform;
            rect.localScale = _cobwebBaseScale * _cobwebScale;
            var color = _cobwebBaseColor;
            color.a = _cobwebAlpha;
            lockOverlayImage.color = color;
        }

        void CaptureBoxBase()
        {
            if (_boxBaseCaptured || blockerImage == null)
            {
                return;
            }

            var rect = blockerImage.rectTransform;
            _boxBaseScale = rect.localScale;
            _boxBaseRotation = rect.localRotation;
            _boxBaseColor = blockerImage.color;
            _boxBaseOffsetMin = rect.offsetMin;
            _boxBaseOffsetMax = rect.offsetMax;
            _boxBaseCaptured = true;
        }

        void RestoreBoxBasePose()
        {
            if (!_boxBaseCaptured || blockerImage == null)
            {
                return;
            }

            var rect = blockerImage.rectTransform;
            rect.localScale = _boxBaseScale;
            rect.localRotation = _boxBaseRotation;
            rect.offsetMin = _boxBaseOffsetMin;
            rect.offsetMax = _boxBaseOffsetMax;
            blockerImage.color = _boxBaseColor;
        }

        void TickBoxReveal()
        {
            if (!_boxRevealing)
            {
                return;
            }

            var dt = _boxConfig != null ? _boxConfig.GetDeltaTime() : Time.unscaledDeltaTime;
            _boxElapsed += dt;
            var t = _boxDuration <= 0f ? 1f : Mathf.Clamp01(_boxElapsed / _boxDuration);
            var curved = _boxCurve != null ? _boxCurve.Evaluate(t) : EaseInOut(t);
            ApplyBoxVisual(curved);
            if (t < 1f || !_boxBreaking)
            {
                return;
            }

            RestoreBoxBasePose();
            SetActiveSafe(blockerImage, false);
        }

        void ApplyBoxVisual()
        {
            ApplyBoxVisual(0f);
        }

        void ApplyBoxVisual(float curved)
        {
            if (blockerImage == null)
            {
                return;
            }

            CaptureBoxBase();
            var sx = Mathf.LerpUnclamped(_boxScaleFrom, _boxScaleTo, curved);
            var sy = Mathf.LerpUnclamped(_boxScaleYFrom, _boxScaleYTo, curved);
            var offsetY = Mathf.LerpUnclamped(_boxOffsetYFrom, _boxOffsetYTo, curved);
            var rotation = Mathf.LerpUnclamped(_boxRotationFrom, _boxRotationTo, curved);
            var alpha = Mathf.LerpUnclamped(_boxAlphaFrom, _boxAlphaTo, curved);
            var rect = blockerImage.rectTransform;
            rect.localScale = new Vector3(_boxBaseScale.x * sx, _boxBaseScale.y * sy, 1f);
            rect.localRotation = _boxBaseRotation * Quaternion.Euler(0f, 0f, rotation);
            rect.offsetMin = new Vector2(_boxBaseOffsetMin.x, _boxBaseOffsetMin.y + offsetY);
            rect.offsetMax = new Vector2(_boxBaseOffsetMax.x, _boxBaseOffsetMax.y + offsetY);
            var color = _boxBaseColor;
            color.a = alpha;
            blockerImage.color = color;
        }

        static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }

        public void ApplyContentSiblingOrder(
            RectTransform selectionBack,
            RectTransform selectionFront,
            RectTransform orderedHighlight = null,
            RectTransform orderedMark = null)
        {
            var order = 0;
            SetSibling(selectionBack, ref order);
            SetSibling(orderedHighlight, ref order);
            SetSibling(itemImage != null ? itemImage.rectTransform : null, ref order);
            SetSibling(orderedMark, ref order);
            SetSibling(blockerImage != null ? blockerImage.rectTransform : null, ref order);
            SetSibling(lockOverlayImage != null ? lockOverlayImage.rectTransform : null, ref order);
            if (generatorChargeIndicator != null && generatorChargeIndicator.RechargeSlider != null)
            {
                SetSibling(generatorChargeIndicator.RechargeSlider.transform as RectTransform, ref order);
            }

            SetSibling(selectionFront, ref order);
            SetSibling(fxRoot, ref order);
        }

        public void Render(BoardCellState state, MergeItemDatabase itemDatabase, BoardVisualConfig visuals)
        {
            if (state == null)
            {
                SetEmpty();
                return;
            }

            if (_boxRevealing)
            {
                SetActiveSafe(blockerImage, true);
                SetActiveSafe(lockOverlayImage, false);
                SetActiveSafe(itemImage, false);
                return;
            }

            if (state.IsBox)
            {
                SetActiveSafe(itemImage, false);
                SetActiveSafe(blockerImage, true);
                SetActiveSafe(lockOverlayImage, false);

                if (blockerImage != null)
                {
                    blockerImage.sprite = visuals != null ? visuals.GetBoxSprite(state.ResolveBoxVisualIndex()) : null;
                    blockerImage.enabled = blockerImage.sprite != null;
                }

                if (itemAnimator != null)
                {
                    itemAnimator.SetHovered(false);
                    itemAnimator.SnapActionToIdle();
                }

                return;
            }

            SetActiveSafe(blockerImage, false);

            if (state.HasItem)
            {
                Sprite icon = null;
                if (itemDatabase != null && itemDatabase.TryGetById(state.ItemId, out var data))
                {
                    icon = data.Icon;
                }
                else
                {
                    Debug.LogError($"[BoardCellView] Missing item definition for id {state.ItemId} at cell {index}.");
                }

                SetActiveSafe(itemImage, !_hideItemForDrag && !_itemPresentationSuppressed);
                if (itemImage != null)
                {
                    itemImage.sprite = icon;
                    itemImage.enabled = icon != null;
                    CaptureItemBaseColor();
                    ApplyItemBrightness();
                }

                if (state.ItemLocked)
                {
                    SetActiveSafe(lockOverlayImage, true);
                    if (lockOverlayImage != null)
                    {
                        lockOverlayImage.sprite = visuals != null ? visuals.CobwebSprite : null;
                        lockOverlayImage.enabled = lockOverlayImage.sprite != null;
                        if (!_cobwebHover && !_cobwebBreaking)
                        {
                            ResetCobwebVisualImmediate();
                            CaptureCobwebBase();
                        }
                    }
                }
                else if (_cobwebBreaking)
                {
                    // Break animation owns the overlay until it fades out.
                }
                else
                {
                    ResetCobwebVisualImmediate();
                    SetActiveSafe(lockOverlayImage, false);
                }
            }
            else
            {
                SetEmpty();
            }
        }

        void SetEmpty()
        {
            _hideItemForDrag = false;
            _itemPresentationSuppressed = false;
            _generatorExhaustedVisual = false;
            SetActiveSafe(itemImage, false);
            SetActiveSafe(blockerImage, false);
            SetActiveSafe(lockOverlayImage, false);
            if (generatorChargeIndicator != null)
            {
                generatorChargeIndicator.SetVisible(false);
            }
            if (itemAnimator != null)
            {
                itemAnimator.SetHovered(false);
                itemAnimator.SnapActionToIdle();
            }
        }

        void CaptureItemBaseColor()
        {
            if (_itemBaseColorCaptured || itemImage == null)
            {
                return;
            }

            _itemBaseColor = itemImage.color;
            _itemBaseColorCaptured = true;
        }

        void ApplyItemBrightness()
        {
            if (itemImage == null)
            {
                return;
            }

            CaptureItemBaseColor();
            var brightness = _generatorExhaustedVisual ? 0.8f : 1f;
            var color = _itemBaseColor;
            color.r *= brightness;
            color.g *= brightness;
            color.b *= brightness;
            itemImage.color = color;
        }

        static void SetSibling(RectTransform rect, ref int order)
        {
            if (rect == null)
            {
                return;
            }

            rect.SetSiblingIndex(order++);
        }

        static void SetActiveSafe(Behaviour behaviour, bool active)
        {
            if (behaviour == null)
            {
                return;
            }

            if (behaviour.gameObject.activeSelf != active)
            {
                behaviour.gameObject.SetActive(active);
            }
        }
    }
}
