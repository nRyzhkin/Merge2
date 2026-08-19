using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class ItemDetailChainView : MonoBehaviour
    {
        [SerializeField] RectTransform chainRoot;
        [SerializeField] ItemDetailChainNode nodeTemplate;
        [SerializeField] GridLayoutGroup layoutGroup;

        readonly List<ItemDetailChainNode> _nodes = new List<ItemDetailChainNode>(12);

        public void Bind(RectTransform root, ItemDetailChainNode template, GridLayoutGroup layout)
        {
            chainRoot = root;
            nodeTemplate = template;
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
            CollectExisting();
            var visible = Mathf.Min(chain.Count, _nodes.Count);
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (i >= visible)
                {
                    _nodes[i].gameObject.SetActive(false);
                    continue;
                }

                var data = chain[i];
                var discovered = discovery != null && discovery.IsDiscovered(data.Id);
                _nodes[i].Render(data, discovered, data.Id == currentItemId, i < visible - 1);
            }
        }

        public void Hide()
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].gameObject.SetActive(false);
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
                layoutGroup = GetComponent<GridLayoutGroup>();
            }

            CollectExisting();
        }

        void CollectExisting()
        {
            if (chainRoot == null)
            {
                return;
            }

            _nodes.Clear();
            for (var i = 0; i < chainRoot.childCount; i++)
            {
                var node = chainRoot.GetChild(i).GetComponent<ItemDetailChainNode>();
                if (node == null)
                {
                    continue;
                }

                if (nodeTemplate == null)
                {
                    nodeTemplate = node;
                }

                _nodes.Add(node);
            }
        }
    }
}
