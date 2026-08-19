using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class OrderRequirementSlotView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text amountText;

        int _itemId;
        bool _pointerEnabled;

        public int ItemId => _itemId;
        public event Action<OrderRequirementSlotView> Clicked;

        public void BindLocal()
        {
            if (icon == null)
            {
                var child = transform.Find("Icon");
                if (child != null)
                {
                    icon = child.GetComponent<Image>();
                }
            }

            if (amountText == null)
            {
                var child = transform.Find("Text_Amount");
                if (child != null)
                {
                    amountText = child.GetComponent<TMP_Text>();
                }
            }
        }

        public void Show(Sprite sprite, int amount, int itemId)
        {
            BindLocal();
            _itemId = itemId;
            gameObject.SetActive(true);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.preserveAspect = true;
            }

            if (amountText != null)
            {
                if (amount > 1)
                {
                    amountText.gameObject.SetActive(true);
                    amountText.text = amount.ToString();
                }
                else
                {
                    amountText.gameObject.SetActive(false);
                }
            }

            ApplyPointerTargets();
        }

        public void Hide()
        {
            _itemId = 0;
            SetPointerEnabled(false);
            gameObject.SetActive(false);
        }

        public void SetPointerEnabled(bool enabled)
        {
            _pointerEnabled = enabled && _itemId != 0 && gameObject.activeSelf;
            ApplyPointerTargets();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_pointerEnabled || _itemId == 0 || eventData == null ||
                eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            Clicked?.Invoke(this);
        }

        public Vector3 GetWorldCenter()
        {
            var target = icon != null ? icon.rectTransform : transform as RectTransform;
            if (target == null)
            {
                return transform.position;
            }

            return target.TransformPoint(target.rect.center);
        }

        void ApplyPointerTargets()
        {
            EnsureHitGraphic();
            var graphics = GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                graphic.raycastTarget = _pointerEnabled && graphic.gameObject == gameObject;
            }
        }

        void EnsureHitGraphic()
        {
            var image = GetComponent<Image>();
            if (image != null)
            {
                return;
            }

            image = gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;
        }
    }
}
