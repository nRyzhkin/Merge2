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
        [SerializeField] bool useUnscaledTime = true;

        public float HoverScale => hoverScale;
        public float HoverEnterDuration => hoverEnterDuration;
        public float HoverExitDuration => hoverExitDuration;
        public AnimationCurve HoverEnterCurve => hoverEnterCurve;
        public AnimationCurve HoverExitCurve => hoverExitCurve;
        public bool UseUnscaledTime => useUnscaledTime;

        public float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
