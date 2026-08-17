using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class SellButtonView : MonoBehaviour
    {
        public const string SellRootName = "Sell";

        [SerializeField] Button button;
        [SerializeField] TMP_Text actionLabel;
        [SerializeField] TMP_Text priceLabel;

        bool _boundClick;
        long _displayedPrice = long.MinValue;

        public void BindLocal()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (actionLabel == null)
            {
                var title = transform.Find("Text (TMP)");
                if (title != null)
                {
                    actionLabel = title.GetComponent<TMP_Text>();
                }
            }

            if (priceLabel == null)
            {
                for (var i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    if (actionLabel != null && child == actionLabel.transform)
                    {
                        continue;
                    }

                    priceLabel = child.GetComponentInChildren<TMP_Text>(true);
                    if (priceLabel != null)
                    {
                        break;
                    }
                }
            }

            EnsureClickBound();
        }

        void Awake()
        {
            BindLocal();
        }

        void OnEnable()
        {
            BindLocal();
            ApplyActionLabel();
            Refresh();
        }

        void OnDestroy()
        {
            if (button != null && _boundClick)
            {
                button.onClick.RemoveListener(OnClicked);
            }
        }

        void LateUpdate()
        {
            Refresh();
        }

        void EnsureClickBound()
        {
            if (button == null || _boundClick)
            {
                return;
            }

            button.onClick.AddListener(OnClicked);
            _boundClick = true;
        }

        void OnClicked()
        {
            var system = SellSystem.Current;
            system?.TrySellSelected();
        }

        void Refresh()
        {
            var system = SellSystem.Current;
            var canSell = system != null && system.CanSellSelected();
            var price = canSell && system != null ? system.GetSelectedSellPrice() : 0L;
            if (button != null && button.interactable != canSell)
            {
                button.interactable = canSell;
            }

            if (priceLabel == null)
            {
                return;
            }

            if (price == _displayedPrice)
            {
                return;
            }

            _displayedPrice = price;
            priceLabel.text = price > 0 ? price.ToString() : string.Empty;
        }

        void ApplyActionLabel()
        {
            if (actionLabel == null)
            {
                return;
            }

            var localized = MergeUiLocalization.Get(MergeUiLocalization.SellActionKey);
            if (!string.IsNullOrEmpty(localized))
            {
                actionLabel.text = localized;
            }
        }
    }
}
