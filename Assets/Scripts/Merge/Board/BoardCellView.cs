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

        public int Index => index;
        public Image ItemImage => itemImage;
        public Image BlockerImage => blockerImage;
        public Image LockOverlayImage => lockOverlayImage;
        public RectTransform FxRoot => fxRoot;
        public BoardItemAnimator ItemAnimator => itemAnimator;

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

        public void SetHideItemForDrag(bool hide)
        {
            _hideItemForDrag = hide;
            if (hide)
            {
                SetActiveSafe(itemImage, false);
            }
        }

        public void RevealItemAfterMerge()
        {
            _hideItemForDrag = false;
            if (itemImage != null && itemImage.sprite != null)
            {
                SetActiveSafe(itemImage, true);
            }
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

                SetActiveSafe(itemImage, !_hideItemForDrag);
                if (itemImage != null)
                {
                    itemImage.sprite = icon;
                    itemImage.enabled = icon != null;
                }

                var showLock = state.ItemLocked;
                SetActiveSafe(lockOverlayImage, showLock);
                if (showLock && lockOverlayImage != null)
                {
                    lockOverlayImage.sprite = visuals != null ? visuals.CobwebSprite : null;
                    lockOverlayImage.enabled = lockOverlayImage.sprite != null;
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
