using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    public class BoardSelectionView : MonoBehaviour
    {
        [SerializeField] RectTransform selectionBack;
        [SerializeField] RectTransform selectionFront;
        [SerializeField] RectTransform parkingParent;

        public RectTransform SelectionBack => selectionBack;
        public RectTransform SelectionFront => selectionFront;

        public void Bind(RectTransform back, RectTransform front)
        {
            selectionBack = back;
            selectionFront = front;
            if (parkingParent == null && back != null)
            {
                parkingParent = back.parent as RectTransform;
            }

            DisableRaycasts(back);
            DisableRaycasts(front);
            Hide();
        }

        public void ShowOn(BoardCellView cell)
        {
            if (cell == null || selectionBack == null || selectionFront == null)
            {
                Hide();
                return;
            }

            Attach(selectionBack, cell.transform, 0);
            Attach(selectionFront, cell.transform, cell.transform.childCount - 1);
            cell.ApplyContentSiblingOrder(selectionBack, selectionFront);
            selectionBack.gameObject.SetActive(true);
            selectionFront.gameObject.SetActive(true);
        }

        public void Hide()
        {
            Park(selectionBack);
            Park(selectionFront);
            if (selectionBack != null)
            {
                selectionBack.gameObject.SetActive(false);
            }

            if (selectionFront != null)
            {
                selectionFront.gameObject.SetActive(false);
            }
        }

        void Park(RectTransform rect)
        {
            if (rect == null || parkingParent == null)
            {
                return;
            }

            if (rect.parent != parkingParent)
            {
                rect.SetParent(parkingParent, false);
            }
        }

        static void Attach(RectTransform rect, Transform parent, int siblingIndex)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));
        }

        static void DisableRaycasts(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            var graphics = root.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }
}
