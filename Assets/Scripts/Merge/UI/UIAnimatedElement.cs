using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    public enum UIAnimationRole
    {
        Auto = 0,
        Top = 1,
        Bottom = 2,
        Left = 3,
        Right = 4,
        Center = 5,
        FadeScale = 6,
        ListContainer = 7,
        Ignore = 8
    }

    [DisallowMultipleComponent]
    public class UIAnimatedElement : MonoBehaviour
    {
        [SerializeField] UIAnimationRole role = UIAnimationRole.Auto;
        [SerializeField] int sequenceOrder;
        [SerializeField] RectTransform itemsRoot;

        public UIAnimationRole Role => role;
        public int SequenceOrder => sequenceOrder;
        public RectTransform ItemsRoot => itemsRoot;
        public RectTransform Rect => transform as RectTransform;

        public UIAnimationRole ResolveRole()
        {
            if (role != UIAnimationRole.Auto)
            {
                return role;
            }

            return ResolveAutoRole(Rect);
        }

        public static UIAnimationRole ResolveAutoRole(RectTransform rect)
        {
            if (rect == null)
            {
                return UIAnimationRole.FadeScale;
            }

            var min = rect.anchorMin;
            var max = rect.anchorMax;
            var mid = (min + max) * 0.5f;
            var stretchX = max.x - min.x > 0.85f;
            var stretchY = max.y - min.y > 0.85f;
            if (stretchX && stretchY)
            {
                return UIAnimationRole.FadeScale;
            }

            const float edge = 0.82f;
            var top = mid.y >= edge;
            var bottom = mid.y <= 1f - edge;
            var left = mid.x <= 1f - edge;
            var right = mid.x >= edge;

            if (top && !bottom && !left && !right)
            {
                return UIAnimationRole.Top;
            }

            if (bottom && !top && !left && !right)
            {
                return UIAnimationRole.Bottom;
            }

            if (left && !right && !top && !bottom)
            {
                return UIAnimationRole.Left;
            }

            if (right && !left && !top && !bottom)
            {
                return UIAnimationRole.Right;
            }

            if (top)
            {
                return UIAnimationRole.Top;
            }

            if (bottom)
            {
                return UIAnimationRole.Bottom;
            }

            if (left)
            {
                return UIAnimationRole.Left;
            }

            if (right)
            {
                return UIAnimationRole.Right;
            }

            return UIAnimationRole.FadeScale;
        }

        public static Vector2 EdgeDirection(UIAnimationRole resolved)
        {
            switch (resolved)
            {
                case UIAnimationRole.Top:
                    return Vector2.up;
                case UIAnimationRole.Bottom:
                    return Vector2.down;
                case UIAnimationRole.Left:
                    return Vector2.left;
                case UIAnimationRole.Right:
                    return Vector2.right;
                default:
                    return Vector2.zero;
            }
        }

        public RectTransform ResolveListItemsRoot()
        {
            if (itemsRoot != null)
            {
                return itemsRoot;
            }

            var scroll = GetComponent<ScrollRect>();
            if (scroll != null && scroll.content != null)
            {
                return scroll.content;
            }

            return Rect;
        }
    }
}
