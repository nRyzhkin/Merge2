using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class GeneratorChargeIndicator : MonoBehaviour
    {
        public const string SliderObjectName = "Cooldown Indicator";

        [SerializeField] Slider rechargeSlider;

        public Slider RechargeSlider => rechargeSlider;

        public void Bind(Slider slider)
        {
            rechargeSlider = slider;
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (rechargeSlider == null)
            {
                return;
            }

            if (rechargeSlider.gameObject.activeSelf != visible)
            {
                rechargeSlider.gameObject.SetActive(visible);
            }
        }

        public void SetProgress(float normalizedProgress)
        {
            if (rechargeSlider == null)
            {
                return;
            }

            rechargeSlider.value = Mathf.Clamp01(normalizedProgress);
        }
    }
}
