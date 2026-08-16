using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class DragItemView : MonoBehaviour
    {
        [SerializeField] Image image;
        [SerializeField] RectTransform fxRoot;

        RectTransform _rect;

        public RectTransform Rect
        {
            get
            {
                if (_rect == null)
                {
                    _rect = transform as RectTransform;
                }

                return _rect;
            }
        }

        public void Bind(Image itemImage, RectTransform fx)
        {
            image = itemImage;
            fxRoot = fx;
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        public void Show(Sprite sprite, Vector2 size, bool preserveAspect, Color color)
        {
            gameObject.SetActive(true);
            var rect = Rect;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            if (image != null)
            {
                image.sprite = sprite;
                image.enabled = sprite != null;
                image.preserveAspect = preserveAspect;
                image.color = color;
                image.raycastTarget = false;
                image.maskable = true;
            }

            if (fxRoot != null)
            {
                fxRoot.gameObject.SetActive(true);
            }
        }

        public void SetPose(Vector2 anchoredPosition, Vector2 scale)
        {
            Rect.anchoredPosition = anchoredPosition;
            Rect.localScale = new Vector3(scale.x, scale.y, 1f);
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
