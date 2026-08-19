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
        [SerializeField] GameObject amountRoot;

        int _itemId;
        bool _pointerEnabled;
        Graphic[] _graphics;

        public int ItemId => _itemId;
        public event Action<OrderRequirementSlotView> Clicked;

        public void Bind(Image iconImage, TMP_Text amount, GameObject amountHolder)
        {
            icon = iconImage;
            amountText = amount;
            amountRoot = amountHolder;
        }

        public void Show(Sprite sprite, int amount, int itemId)
        {
            _itemId = itemId;
            gameObject.SetActive(true);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.preserveAspect = true;
            }

            var showAmount = amount > 1;
            if (amountRoot != null && amountRoot.activeSelf != showAmount)
            {
                amountRoot.SetActive(showAmount);
            }

            if (amountText != null)
            {
                if (showAmount)
                {
                    amountText.text = amount.ToString();
                }

                if (amountRoot == null && amountText.gameObject.activeSelf != showAmount)
                {
                    amountText.gameObject.SetActive(showAmount);
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
            if (_graphics == null)
            {
                _graphics = GetComponentsInChildren<Graphic>(true);
            }

            for (var i = 0; i < _graphics.Length; i++)
            {
                var graphic = _graphics[i];
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
