using TMPro;
using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class CoinHudView : MonoBehaviour
    {
        public const string CoinRootName = "Resource_Coin";
        public const string ValueTextName = "Text_Value";

        [SerializeField] TMP_Text valueText;

        CurrencyService _currency;
        long _displayedCoins = long.MinValue;

        public void BindLocal()
        {
            if (valueText == null)
            {
                var child = transform.Find(ValueTextName);
                if (child != null)
                {
                    valueText = child.GetComponent<TMP_Text>();
                }
            }
        }

        void Awake()
        {
            BindLocal();
        }

        void OnEnable()
        {
            BindLocal();
            BindCurrency(CurrencySystem.Current);
        }

        void OnDisable()
        {
            UnbindCurrency();
        }

        void LateUpdate()
        {
            if (_currency == null)
            {
                BindCurrency(CurrencySystem.Current);
                if (_currency == null)
                {
                    return;
                }
            }

            ApplyCoins(_currency.GetCoins());
        }

        void BindCurrency(CurrencySystem system)
        {
            var service = system != null ? system.Service : null;
            if (service == _currency && _currency != null)
            {
                return;
            }

            UnbindCurrency();
            _currency = service;
            if (_currency == null)
            {
                return;
            }

            _currency.Changed += OnChanged;
            ApplyCoins(_currency.GetCoins());
        }

        void UnbindCurrency()
        {
            if (_currency != null)
            {
                _currency.Changed -= OnChanged;
            }

            _currency = null;
        }

        void OnChanged()
        {
            if (_currency != null)
            {
                ApplyCoins(_currency.GetCoins());
            }
        }

        void ApplyCoins(long coins)
        {
            if (valueText == null || coins == _displayedCoins)
            {
                return;
            }

            _displayedCoins = coins;
            valueText.text = coins.ToString();
        }
    }
}
