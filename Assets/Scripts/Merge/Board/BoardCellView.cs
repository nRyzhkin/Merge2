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
        bool _hideItemForDrag;
        bool _itemPresentationSuppressed;

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

        public int Index => index;
        public Image ItemImage => itemImage;
        public Image BlockerImage => blockerImage;
        public Image LockOverlayImage => lockOverlayImage;
        public RectTransform FxRoot => fxRoot;
        public BoardItemAnimator ItemAnimator => itemAnimator;
        public bool IsItemPresentationSuppressed => _itemPresentationSuppressed;
        public bool IsItemHiddenForDrag => _hideItemForDrag;

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
            if (!_itemPresentationSuppressed && !_hideItemForDrag)
            {
                return;
            }

            SetActiveSafe(itemImage, false);
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

        void Update()
        {
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

        static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }

        public void ApplyContentSiblingOrder(RectTransform selectionBack, RectTransform selectionFront)
        {
            var order = 0;
            SetSibling(selectionBack, ref order);
            SetSibling(itemImage != null ? itemImage.rectTransform : null, ref order);
            SetSibling(blockerImage != null ? blockerImage.rectTransform : null, ref order);
            SetSibling(lockOverlayImage != null ? lockOverlayImage.rectTransform : null, ref order);
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

            if (state.IsBox)
            {
                SetActiveSafe(itemImage, false);
                SetActiveSafe(blockerImage, true);
                SetActiveSafe(lockOverlayImage, false);

                if (blockerImage != null)
                {
                    blockerImage.sprite = visuals != null ? visuals.GetBoxSprite(state.Index) : null;
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
            SetActiveSafe(itemImage, false);
            SetActiveSafe(blockerImage, false);
            SetActiveSafe(lockOverlayImage, false);
            if (itemAnimator != null)
            {
                itemAnimator.SetHovered(false);
                itemAnimator.SnapActionToIdle();
            }
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
