using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class OrderRequirementSlotView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text amountText;

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

        public void Show(Sprite sprite, int amount)
        {
            BindLocal();
            gameObject.SetActive(true);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.preserveAspect = true;
            }

            if (amountText == null)
            {
                return;
            }

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

        public void Hide()
        {
            gameObject.SetActive(false);
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
    }
}
