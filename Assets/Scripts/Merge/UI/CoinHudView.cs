using TMPro;
using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class CoinHudView : MonoBehaviour
    {
        public const string CoinRootName = "Resource_Coin";
        public const string ValueTextName = "Text_Value";
        const float GainPeakScale = 1.08f;
        const float GainDuration = 0.22f;

        [SerializeField] TMP_Text valueText;

        CurrencyService _currency;
        long _displayedCoins = long.MinValue;
        Vector3 _valueBaseScale = Vector3.one;
        bool _valueBaseCaptured;
        float _feedbackElapsed = -1f;

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

            CaptureValueBaseScale();
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
            RestoreValueScale();
            _feedbackElapsed = -1f;
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
            TickFeedback();
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

            if (_displayedCoins != long.MinValue && coins > _displayedCoins)
            {
                CaptureValueBaseScale();
                _feedbackElapsed = 0f;
            }

            _displayedCoins = coins;
            valueText.text = coins.ToString();
        }

        void TickFeedback()
        {
            if (_feedbackElapsed < 0f || valueText == null)
            {
                return;
            }

            _feedbackElapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_feedbackElapsed / GainDuration);
            var up = t < 0.45f ? t / 0.45f : 1f - (t - 0.45f) / 0.55f;
            valueText.rectTransform.localScale = _valueBaseScale * Mathf.LerpUnclamped(1f, GainPeakScale, Mathf.Clamp01(up));
            if (t >= 1f)
            {
                RestoreValueScale();
                _feedbackElapsed = -1f;
            }
        }

        void CaptureValueBaseScale()
        {
            if (_valueBaseCaptured || valueText == null)
            {
                return;
            }

            _valueBaseScale = valueText.rectTransform.localScale;
            _valueBaseCaptured = true;
        }

        void RestoreValueScale()
        {
            if (valueText != null && _valueBaseCaptured)
            {
                valueText.rectTransform.localScale = _valueBaseScale;
            }
        }
    }
}
