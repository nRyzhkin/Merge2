using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    public class MergeChainItemView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] GameObject selection;
        [SerializeField] GameObject locked;

        public void BindReferences(Image iconImage, GameObject selectionObject, GameObject lockedObject)
        {
            icon = iconImage;
            selection = selectionObject;
            locked = lockedObject;
        }

        void Awake()
        {
            if (icon == null)
            {
                var iconTransform = transform.Find("Icon");
                icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            }

            if (selection == null)
            {
                var selectionTransform = transform.Find("Selected");
                if (selectionTransform == null)
                {
                    selectionTransform = transform.Find("Selection");
                }

                selection = selectionTransform != null ? selectionTransform.gameObject : null;
            }

            if (locked == null)
            {
                var lockedTransform = transform.Find("Locked");
                locked = lockedTransform != null ? lockedTransform.gameObject : null;
            }
        }

        public void Render(MergeItemData data, bool discovered, bool isCurrent)
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
