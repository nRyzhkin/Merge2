using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    public class MergeChainInfoView : MonoBehaviour
    {
        [SerializeField] RectTransform chainRoot;
        [SerializeField] MergeChainItemView itemTemplate;
        [SerializeField] RectTransform arrowTemplate;
        [SerializeField] HorizontalLayoutGroup layoutGroup;
        [SerializeField] float itemSize = 65f;
        [SerializeField] float arrowSize = 32f;
        [SerializeField] float spacing = 10f;

        readonly List<MergeChainItemView> _items = new List<MergeChainItemView>(12);
        readonly List<RectTransform> _arrows = new List<RectTransform>(12);

        public void Bind(RectTransform root, MergeChainItemView item, RectTransform arrow, HorizontalLayoutGroup layout)
        {
            chainRoot = root;
            itemTemplate = item;
            arrowTemplate = arrow;
            layoutGroup = layout;
            CollectExisting();
        }

        public void Show(IReadOnlyList<MergeItemData> chain, int currentItemId, MergeDiscoveryState discovery)
        {
            if (chain == null || chain.Count == 0)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            EnsureCapacity(chain.Count);
            ApplyFit(chain.Count);

            var itemIndex = 0;
            var arrowIndex = 0;
            for (var i = 0; i < chain.Count; i++)
            {
                var data = chain[i];
                var view = _items[itemIndex++];
                var discovered = discovery != null && discovery.IsDiscovered(data.Id);
                view.Render(data, discovered, data.Id == currentItemId);
                view.transform.SetSiblingIndex(i * 2);

                if (i < chain.Count - 1)
                {
                    var arrow = _arrows[arrowIndex++];
                    arrow.gameObject.SetActive(true);
                    arrow.SetSiblingIndex(i * 2 + 1);
                }
            }

            for (var i = itemIndex; i < _items.Count; i++)
            {
                _items[i].gameObject.SetActive(false);
            }

            for (var i = arrowIndex; i < _arrows.Count; i++)
            {
                _arrows[i].gameObject.SetActive(false);
            }
        }

        public void Hide()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                _items[i].gameObject.SetActive(false);
            }

            for (var i = 0; i < _arrows.Count; i++)
            {
                _arrows[i].gameObject.SetActive(false);
            }
        }

        void Awake()
        {
            if (chainRoot == null)
            {
                chainRoot = transform as RectTransform;
            }

            if (layoutGroup == null)
            {
                layoutGroup = GetComponent<HorizontalLayoutGroup>();
            }

            CollectExisting();
        }

        void CollectExisting()
        {
            if (chainRoot == null)
            {
                return;
            }

            _items.Clear();
            _arrows.Clear();
            for (var i = 0; i < chainRoot.childCount; i++)
            {
                var child = chainRoot.GetChild(i);
                var item = child.GetComponent<MergeChainItemView>();
                if (item != null)
                {
                    if (itemTemplate == null)
                    {
                        itemTemplate = item;
                    }

                    _items.Add(item);
                    continue;
                }

                var rect = child as RectTransform;
                if (rect != null && child.name.IndexOf("arrow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (arrowTemplate == null)
                    {
                        arrowTemplate = rect;
                    }

                    _arrows.Add(rect);
                }
            }
        }

        void EnsureCapacity(int itemCount)
        {
            while (_items.Count < itemCount)
            {
                var clone = Instantiate(itemTemplate, chainRoot);
                clone.name = itemTemplate.name;
                _items.Add(clone);
            }

            var arrowCount = Mathf.Max(0, itemCount - 1);
            while (_arrows.Count < arrowCount)
            {
                var clone = Instantiate(arrowTemplate, chainRoot);
                clone.name = arrowTemplate.name;
                _arrows.Add(clone);
            }
        }

        void ApplyFit(int itemCount)
        {
            if (layoutGroup != null)
            {
                layoutGroup.spacing = spacing;
            }

            var arrowCount = Mathf.Max(0, itemCount - 1);
            var needed = itemCount * itemSize + arrowCount * arrowSize + Mathf.Max(0, itemCount + arrowCount - 1) * spacing;
            var available = chainRoot != null ? chainRoot.rect.width : needed;
            var scale = needed > available && needed > 0f ? available / needed : 1f;
            var fittedItem = itemSize * scale;
            var fittedArrow = arrowSize * scale;
            if (layoutGroup != null)
            {
                layoutGroup.spacing = spacing * scale;
            }

            for (var i = 0; i < _items.Count; i++)
            {
                SetSize(_items[i].transform as RectTransform, fittedItem, fittedItem);
            }

            for (var i = 0; i < _arrows.Count; i++)
            {
                SetSize(_arrows[i], fittedArrow, fittedItem);
            }
        }

        static void SetSize(RectTransform rect, float width, float height)
        {
            if (rect == null)
            {
                return;
            }

            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }
}
