using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "BoardItemAnimationConfig", menuName = "San Island/Board Item Animation Config")]
    public class BoardItemAnimationConfig : ScriptableObject
    {
        [Header("Press Anticipation")]
        [SerializeField] Vector2 pressScale = new Vector2(1.10f, 0.85f);
        [SerializeField] [Range(0.04f, 0.08f)] float pressDownNormalized = 0.06f;
        [SerializeField] [Range(0.07f, 0.11f)] float pressDuration = 0.09f;
        [SerializeField] AnimationCurve pressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Click Release")]
        [SerializeField] Vector2 releaseScale = new Vector2(0.955f, 1.075f);
        [SerializeField] [Range(0.06f, 0.09f)] float releaseDuration = 0.075f;
        [SerializeField] AnimationCurve releaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Return To Idle")]
        [SerializeField] [Range(0.08f, 0.16f)] float returnDuration = 0.12f;
        [SerializeField] AnimationCurve returnCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.2f),
            new Keyframe(0.65f, 1.08f, 0f, 0f),
            new Keyframe(1f, 1f, -0.8f, 0f));

        public Vector2 PressScale => pressScale;
        public float PressDownNormalized => pressDownNormalized;
        public float PressDuration => pressDuration;
        public AnimationCurve PressCurve => pressCurve;
        public Vector2 ReleaseScale => releaseScale;
        public float ReleaseDuration => releaseDuration;
        public AnimationCurve ReleaseCurve => releaseCurve;
        public float ReturnDuration => returnDuration;
        public AnimationCurve ReturnCurve => returnCurve;
    }
}
