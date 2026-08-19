using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class ItemDetailChainNode : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] GameObject selection;
        [SerializeField] GameObject locked;
        [SerializeField] GameObject arrow;

        void Awake()
        {
            ResolveChildren();
        }

        public void Bind(Image iconImage, GameObject selectionObject, GameObject lockedObject, GameObject arrowObject)
        {
            icon = iconImage;
            selection = selectionObject;
            locked = lockedObject;
            arrow = arrowObject;
            ResolveChildren();
        }

        void ResolveChildren()
        {
            if (icon == null)
            {
                var iconTransform = transform.Find("Icon");
                icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            }

            if (selection == null)
            {
                var selected = transform.Find("Selected");
                selection = selected != null ? selected.gameObject : null;
            }

            if (locked == null)
            {
                var lockedTransform = transform.Find("Locked");
                locked = lockedTransform != null ? lockedTransform.gameObject : null;
            }

            if (arrow == null)
            {
                var arrowTransform = transform.Find("arrow") ?? transform.Find("Arrow");
                arrow = arrowTransform != null ? arrowTransform.gameObject : null;
            }
        }

        public void Render(MergeItemData data, bool discovered, bool isCurrent, bool showArrow)
        {
            if (data == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            if (discovered)
            {
                if (icon != null)
                {
                    icon.sprite = data.Icon;
                    icon.enabled = data.Icon != null;
                    icon.gameObject.SetActive(true);
                }

                SetActive(locked, false);
            }
            else
            {
                if (icon != null)
                {
                    icon.gameObject.SetActive(false);
                }

                SetActive(locked, true);
            }

            SetActive(selection, isCurrent);
            if (arrow != null)
            {
                arrow.SetActive(showArrow);
            }
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
    }
}
