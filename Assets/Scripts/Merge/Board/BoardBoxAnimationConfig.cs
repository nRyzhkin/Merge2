using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "BoardBoxAnimationConfig", menuName = "San Island/Board Box Animation Config")]
    public class BoardBoxAnimationConfig : ScriptableObject
    {
        [Header("Anticipation")]
        [SerializeField] Vector2 anticipationScale = new Vector2(1.04f, 0.96f);
        [SerializeField] [Range(0.02f, 0.05f)] float anticipationOffsetY = 0.03f;
        [SerializeField] [Range(0.06f, 0.09f)] float anticipationDuration = 0.08f;
        [SerializeField] AnimationCurve anticipationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Break")]
        [SerializeField] [Range(1.08f, 1.12f)] float breakScale = 1.10f;
        [SerializeField] [Range(2f, 4f)] float breakRotationDegrees = 3f;
        [SerializeField] [Range(0.12f, 0.18f)] float breakDuration = 0.16f;
        [SerializeField] AnimationCurve breakCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("FX")]
        [SerializeField] [Range(1f, 1.3f)] float fxScale = 1.15f;
        [SerializeField] [Range(0.12f, 0.22f)] float fxDuration = 0.16f;
        [SerializeField] Color fxColor = new Color(0.82f, 0.68f, 0.48f, 0.7f);

        [Header("Revealed Item")]
        [SerializeField] [Range(0.65f, 0.75f)] float revealedItemStartScale = 0.70f;
        [SerializeField] [Range(1.07f, 1.10f)] float revealedItemOvershootScale = 1.08f;
        [SerializeField] [Range(0.96f, 1f)] float revealedItemReboundScale = 0.98f;
        [SerializeField] [Range(0.03f, 0.05f)] float revealedItemOffsetY = 0.04f;
        [SerializeField] [Range(0.18f, 0.24f)] float revealedItemDuration = 0.20f;
        [SerializeField] AnimationCurve revealedItemCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField] bool useUnscaledTime = true;

        public Vector2 AnticipationScale => anticipationScale;
        public float AnticipationOffsetY => anticipationOffsetY;
        public float AnticipationDuration => anticipationDuration;
        public AnimationCurve AnticipationCurve => anticipationCurve;
        public float BreakScale => breakScale;
        public float BreakRotationDegrees => breakRotationDegrees;
        public float BreakDuration => breakDuration;
        public AnimationCurve BreakCurve => breakCurve;
        public float FxScale => fxScale;
        public float FxDuration => fxDuration;
        public Color FxColor => fxColor;
        public float RevealedItemStartScale => revealedItemStartScale;
        public float RevealedItemOvershootScale => revealedItemOvershootScale;
        public float RevealedItemReboundScale => revealedItemReboundScale;
        public float RevealedItemOffsetY => revealedItemOffsetY;
        public float RevealedItemDuration => revealedItemDuration;
        public AnimationCurve RevealedItemCurve => revealedItemCurve;
        public bool UseUnscaledTime => useUnscaledTime;

        public float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
