using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "BoardMergeAnimationConfig", menuName = "San Island/Board Merge Animation Config")]
    public class BoardMergeAnimationConfig : ScriptableObject
    {
        [Header("Drag Magnet")]
        [SerializeField] [Range(0.15f, 0.40f)] float mergeMagnetStrength = 0.26f;

        [Header("Target Scale")]
        [SerializeField] [Range(1.06f, 1.09f)] float mergeTargetBaseScale = 1.075f;
        [SerializeField] [Range(0.005f, 0.025f)] float mergeTargetBreathingAmount = 0.012f;
        [SerializeField] [Range(0.45f, 0.70f)] float mergeTargetBreathingDuration = 0.55f;
        [SerializeField] [Range(0.08f, 0.12f)] float mergeTargetAcceptDuration = 0.10f;
        [SerializeField] [Range(0.008f, 0.025f)] float mergeTargetAcceptOvershoot = 0.015f;

        [Header("Target Attraction")]
        [SerializeField] [Range(0.02f, 0.04f)] float mergeTargetAttractionOffset = 0.03f;
        [SerializeField] [Range(0.04f, 0.16f)] float mergeTargetAttractionSmoothing = 0.08f;

        [Header("Target Highlight")]
        [SerializeField] [Range(0.28f, 0.55f)] float mergeTargetHighlightAlpha = 0.42f;
        [SerializeField] [Range(1.00f, 1.08f)] float mergeTargetHighlightScale = 1.035f;
        [SerializeField] [Range(0.06f, 0.14f)] float mergeTargetHighlightEnterDuration = 0.10f;
        [SerializeField] [Range(0.06f, 0.14f)] float mergeTargetHighlightExitDuration = 0.09f;
        [SerializeField] Color mergeTargetHighlightColor = new Color(1f, 0.90f, 0.55f, 1f);

        [Header("Collision")]
        [SerializeField] Vector2 collisionDragScale = new Vector2(0.90f, 1.14f);
        [SerializeField] Vector2 collisionTargetScale = new Vector2(0.94f, 0.90f);
        [SerializeField] [Range(0.08f, 0.12f)] float collisionDuration = 0.10f;
        [SerializeField] AnimationCurve collisionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Absorb")]
        [SerializeField] Vector2 absorbTargetScale = new Vector2(1.12f, 0.88f);
        [SerializeField] [Range(0.60f, 0.80f)] float absorbFinalScale = 0.72f;
        [SerializeField] [Range(0.06f, 0.10f)] float absorbDuration = 0.08f;
        [SerializeField] AnimationCurve absorbCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Result")]
        [SerializeField] [Range(0.55f, 0.70f)] float resultStartScale = 0.62f;
        [SerializeField] [Range(1.10f, 1.16f)] float resultOvershootScale = 1.13f;
        [SerializeField] [Range(0.94f, 0.99f)] float resultReboundScale = 0.97f;
        [SerializeField] [Range(0.03f, 0.06f)] float resultSpawnOffsetY = 0.045f;
        [SerializeField] [Range(0.18f, 0.28f)] float resultDuration = 0.22f;
        [SerializeField] AnimationCurve resultCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("FX")]
        [SerializeField] [Range(0.20f, 0.35f)] float fxDuration = 0.28f;
        [SerializeField] Color fxColor = new Color(1f, 0.92f, 0.72f, 0.9f);
        [SerializeField] [Range(1.10f, 1.30f)] float mergeFxScaleMultiplier = 1.20f;

        [SerializeField] bool useUnscaledTime = true;

        // Legacy aliases kept so existing callers / serialized assets remain readable.
        public float MergeMagnetStrength => mergeMagnetStrength;
        public float MergeTargetScale => mergeTargetBaseScale;
        public float MergeTargetPulseDuration => mergeTargetBreathingDuration;
        public float MergeTargetBaseScale => mergeTargetBaseScale;
        public float MergeTargetBreathingAmount => mergeTargetBreathingAmount;
        public float MergeTargetBreathingDuration => mergeTargetBreathingDuration;
        public float MergeTargetAcceptDuration => mergeTargetAcceptDuration;
        public float MergeTargetAcceptOvershoot => mergeTargetAcceptOvershoot;
        public float MergeTargetAttractionOffset => mergeTargetAttractionOffset;
        public float MergeTargetAttractionSmoothing => mergeTargetAttractionSmoothing;
        public float MergeTargetHighlightAlpha => mergeTargetHighlightAlpha;
        public float MergeTargetHighlightScale => mergeTargetHighlightScale;
        public float MergeTargetHighlightEnterDuration => mergeTargetHighlightEnterDuration;
        public float MergeTargetHighlightExitDuration => mergeTargetHighlightExitDuration;
        public Color MergeTargetHighlightColor => mergeTargetHighlightColor;
        public Vector2 CollisionDragScale => collisionDragScale;
        public Vector2 CollisionTargetScale => collisionTargetScale;
        public float CollisionDuration => collisionDuration;
        public AnimationCurve CollisionCurve => collisionCurve;
        public Vector2 AbsorbTargetScale => absorbTargetScale;
        public float AbsorbFinalScale => absorbFinalScale;
        public float AbsorbDuration => absorbDuration;
        public AnimationCurve AbsorbCurve => absorbCurve;
        public float ResultStartScale => resultStartScale;
        public float ResultOvershootScale => resultOvershootScale;
        public float ResultReboundScale => resultReboundScale;
        public float ResultSpawnOffsetY => resultSpawnOffsetY;
        public float ResultDuration => resultDuration;
        public AnimationCurve ResultCurve => resultCurve;
        public float FxDuration => fxDuration;
        public Color FxColor => fxColor;
        public float MergeFxScaleMultiplier => mergeFxScaleMultiplier;
        public bool UseUnscaledTime => useUnscaledTime;

        public float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
