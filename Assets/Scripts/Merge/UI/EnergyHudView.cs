using TMPro;
using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class EnergyHudView : MonoBehaviour
    {
        public const string EnergyRootName = "Resource_Energy";
        public const string ValueTextName = "Text_Value";
        public const string TimerContainerName = "TimerContainer";
        public const string TimerTextName = "Text_Timer";

        const float SpendPeakScale = 1.06f;
        const float SpendDuration = 0.16f;
        const float AttentionPeakScale = 1.08f;
        const float AttentionDuration = 0.22f;

        [SerializeField] TMP_Text valueText;
        [SerializeField] GameObject timerContainer;
        [SerializeField] TMP_Text timerText;

        EnergySystem _system;
        EnergyService _energy;
        int _displayedEnergy = int.MinValue;
        int _displayedTimerSeconds = int.MinValue;
        Vector3 _valueBaseScale = Vector3.one;
        bool _valueBaseCaptured;
        float _feedbackElapsed = -1f;
        float _feedbackDuration = SpendDuration;
        float _feedbackPeak = SpendPeakScale;

        public void Bind(TMP_Text value, GameObject timerRoot, TMP_Text timer)
        {
            valueText = value;
            timerContainer = timerRoot;
            timerText = timer;
            CaptureValueBaseScale();
        }

        public void BindLocal()
        {
            if (valueText == null)
            {
                valueText = FindValueText(transform);
            }

            if (timerContainer == null)
            {
                timerContainer = FindTimerContainer(transform);
            }

            if (timerText == null)
            {
                timerText = FindTimerText(transform);
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
            BindToSystem(EnergySystem.Current);
        }

        void OnDisable()
        {
            UnbindSystem();
            RestoreValueScale();
            _feedbackElapsed = -1f;
        }

        void LateUpdate()
        {
            if (_energy == null)
            {
                BindToSystem(EnergySystem.Current);
                if (_energy == null)
                {
                    return;
                }
            }

            ApplyEnergyNumber(_energy.GetCurrentEnergy());
            ApplyTimer(_energy.GetSecondsUntilNextEnergy());
            TickFeedback();
        }

        public void PlaySpendFeedback()
        {
            StartFeedback(SpendPeakScale, SpendDuration);
        }

        public void PlayAttentionPulse()
        {
            StartFeedback(AttentionPeakScale, AttentionDuration);
        }

        public void RefreshImmediate()
        {
            BindLocal();
            if (_energy == null)
            {
                return;
            }

            ApplyEnergyNumber(_energy.GetCurrentEnergy());
            ApplyTimer(_energy.GetSecondsUntilNextEnergy());
        }

        void BindToSystem(EnergySystem system)
        {
            if (system == _system && _energy != null)
            {
                return;
            }

            UnbindSystem();
            _system = system;
            _energy = system != null ? system.Service : null;
            if (_energy == null)
            {
                return;
            }

            _energy.Changed += OnEnergyChanged;
            _energy.Spent += PlaySpendFeedback;
            _system.SpendRejected += PlayAttentionPulse;
            if (isActiveAndEnabled)
            {
                RefreshImmediate();
            }
        }

        void UnbindSystem()
        {
            if (_energy != null)
            {
                _energy.Changed -= OnEnergyChanged;
                _energy.Spent -= PlaySpendFeedback;
            }

            if (_system != null)
            {
                _system.SpendRejected -= PlayAttentionPulse;
            }

            _system = null;
            _energy = null;
        }

        void OnEnergyChanged()
        {
            if (_energy == null)
            {
                return;
            }

            var next = _energy.GetCurrentEnergy();
            if (_displayedEnergy != int.MinValue && next > _displayedEnergy)
            {
                PlayAttentionPulse();
            }

            ApplyEnergyNumber(next);
            ApplyTimer(_energy.GetSecondsUntilNextEnergy());
        }

        void ApplyEnergyNumber(int energy)
        {
            if (valueText == null)
            {
                return;
            }

            if (energy == _displayedEnergy)
            {
                return;
            }

            _displayedEnergy = energy;
            valueText.text = energy.ToString();
        }

        void ApplyTimer(float secondsUntilNext)
        {
            var regenerating = secondsUntilNext > 0f;
            if (timerContainer != null && timerContainer.activeSelf != regenerating)
            {
                timerContainer.SetActive(regenerating);
            }

            if (!regenerating || timerText == null)
            {
                _displayedTimerSeconds = int.MinValue;
                return;
            }

            var totalSeconds = Mathf.Max(1, Mathf.CeilToInt(secondsUntilNext));
            if (totalSeconds == _displayedTimerSeconds)
            {
                return;
            }

            _displayedTimerSeconds = totalSeconds;
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            timerText.text = minutes > 0
                ? $"{minutes}м {seconds}с"
                : $"{seconds}с";
        }

        void StartFeedback(float peak, float duration)
        {
            CaptureValueBaseScale();
            _feedbackPeak = peak;
            _feedbackDuration = Mathf.Max(0.01f, duration);
            _feedbackElapsed = 0f;
        }

        void TickFeedback()
        {
            if (_feedbackElapsed < 0f || valueText == null)
            {
                return;
            }

            _feedbackElapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_feedbackElapsed / _feedbackDuration);
            var up = t < 0.45f ? t / 0.45f : 1f - (t - 0.45f) / 0.55f;
            up = Mathf.Clamp01(up);
            var scale = Mathf.LerpUnclamped(1f, _feedbackPeak, up);
            valueText.rectTransform.localScale = _valueBaseScale * scale;
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
            if (!_valueBaseCaptured || valueText == null)
            {
                return;
            }

            valueText.rectTransform.localScale = _valueBaseScale;
        }

        static TMP_Text FindValueText(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            var child = root.Find(ValueTextName);
            if (child != null)
            {
                return child.GetComponent<TMP_Text>();
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == ValueTextName)
                {
                    return texts[i];
                }
            }

            return null;
        }

        static GameObject FindTimerContainer(Transform root)
        {
            var child = root != null ? root.Find(TimerContainerName) : null;
            return child != null ? child.gameObject : null;
        }

        static TMP_Text FindTimerText(Transform root)
        {
            var container = root != null ? root.Find(TimerContainerName) : null;
            if (container == null)
            {
                return null;
            }

            var text = container.Find(TimerTextName);
            if (text != null)
            {
                return text.GetComponent<TMP_Text>();
            }

            return container.GetComponentInChildren<TMP_Text>(true);
        }
    }
}
