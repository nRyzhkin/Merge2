using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class InventorySlotView : MonoBehaviour, IPointerClickHandler
    {
        const string ItemIconName = "ItemIcon";

        [SerializeField] Image icon;
        [SerializeField] Image emptyVisual;
        [SerializeField] GameObject lockedVisual;
        [SerializeField] GameObject offerVisual;
        [SerializeField] int slotIndex;

        InventoryView _owner;

        public int SlotIndex => slotIndex;
        public RectTransform Rect => transform as RectTransform;

        void Awake()
        {
            EnsureVisuals();
        }

        public void Bind(
            InventoryView owner,
            int index,
            Image iconImage,
            GameObject empty,
            GameObject locked = null,
            GameObject offer = null)
        {
            _owner = owner;
            slotIndex = index;
            lockedVisual = locked;
            offerVisual = offer;
            EnsureVisuals();
            if (iconImage != null && iconImage.gameObject.name == ItemIconName)
            {
                icon = iconImage;
            }
        }

        void EnsureVisuals()
        {
            var bgIcon = transform.Find("BgIcon");
            if (emptyVisual == null && bgIcon != null)
            {
                emptyVisual = bgIcon.GetComponent<Image>();
            }

            if (icon == null)
            {
                var existing = transform.Find(ItemIconName);
                icon = existing != null ? existing.GetComponent<Image>() : CreateItemIcon();
            }

            StretchItemIcon();
        }

        Image CreateItemIcon()
        {
            var go = new GameObject(ItemIconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            var bgIcon = transform.Find("BgIcon");
            if (bgIcon != null)
            {
                rect.SetSiblingIndex(bgIcon.GetSiblingIndex() + 1);
            }

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.enabled = false;
            return image;
        }

        void StretchItemIcon()
        {
            if (icon == null)
            {
                return;
            }

            var rect = icon.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            icon.preserveAspect = true;
        }

        public void Render(InventorySlot slot, MergeItemDatabase database)
        {
            var empty = slot == null || slot.IsEmpty;
            if (icon != null)
            {
                if (empty || database == null || !database.TryGetById(slot.itemId, out var data) || data == null)
                {
                    icon.enabled = false;
                    icon.sprite = null;
                }
                else
                {
                    icon.sprite = data.Icon;
                    icon.enabled = data.Icon != null;
                }
            }

            if (emptyVisual != null)
            {
                emptyVisual.enabled = empty;
            }

            if (lockedVisual != null)
            {
                var locked = slot != null && slot.locked;
                if (lockedVisual.activeSelf != locked)
                {
                    lockedVisual.SetActive(locked);
                }
            }

            if (offerVisual != null)
            {
                var offer = slot != null && slot.offer;
                if (offerVisual.activeSelf != offer)
                {
                    offerVisual.SetActive(offer);
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_owner == null)
            {
                _owner = GetComponentInParent<InventoryView>();
            }

            _owner?.HandleSlotClicked(slotIndex, this);
        }
    }
}
