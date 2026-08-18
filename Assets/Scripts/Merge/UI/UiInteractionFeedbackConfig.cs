using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "UiInteractionFeedbackConfig", menuName = "San Island/UI Interaction Feedback Config")]
    public class UiInteractionFeedbackConfig : ScriptableObject
    {
        [Header("Hover")]
        [SerializeField] float hoverScale = 1.05f;
        [SerializeField] [Range(0.05f, 0.30f)] float hoverEnterDuration = 0.12f;
        [SerializeField] [Range(0.05f, 0.30f)] float hoverExitDuration = 0.12f;
        [SerializeField] AnimationCurve hoverEnterCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] AnimationCurve hoverExitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Press")]
        [SerializeField] float pressScale = 0.98f;
        [SerializeField] [Range(0.03f, 0.16f)] float pressDuration = 0.07f;
        [SerializeField] [Range(0.03f, 0.16f)] float releaseDuration = 0.07f;
        [SerializeField] AnimationCurve pressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] AnimationCurve releaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField] bool useUnscaledTime = true;

        public float HoverScale => hoverScale;
        public float HoverEnterDuration => hoverEnterDuration;
        public float HoverExitDuration => hoverExitDuration;
        public AnimationCurve HoverEnterCurve => hoverEnterCurve;
        public AnimationCurve HoverExitCurve => hoverExitCurve;
        public float PressScale => pressScale;
        public float PressDuration => pressDuration;
        public float ReleaseDuration => releaseDuration;
        public AnimationCurve PressCurve => pressCurve;
        public AnimationCurve ReleaseCurve => releaseCurve;
        public bool UseUnscaledTime => useUnscaledTime;

        public float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
