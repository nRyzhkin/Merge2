using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "BoardCobwebAnimationConfig", menuName = "San Island/Board Cobweb Animation Config")]
    public class BoardCobwebAnimationConfig : ScriptableObject
    {
        [Header("Magnet")]
        [SerializeField] [Range(0.15f, 0.20f)] float unlockMagnetStrength = 0.17f;

        [Header("Hover")]
        [SerializeField] [Range(1.03f, 1.05f)] float itemHoverScale = 1.04f;
        [SerializeField] [Range(1.02f, 1.05f)] float webHoverScale = 1.03f;
        [SerializeField] [Range(0.80f, 0.95f)] float webHoverAlpha = 0.88f;
        [SerializeField] [Range(0.08f, 0.16f)] float webHoverDuration = 0.10f;

        [Header("Approach / Impact")]
        [SerializeField] Vector2 approachDragScale = new Vector2(0.94f, 1.08f);
        [SerializeField] [Range(0.08f, 0.14f)] float impactDuration = 0.10f;
        [SerializeField] AnimationCurve impactCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Web Break")]
        [SerializeField] [Range(1.08f, 1.15f)] float webBreakScale = 1.12f;
        [SerializeField] [Range(0.15f, 0.25f)] float webBreakDuration = 0.18f;
        [SerializeField] AnimationCurve webBreakCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Freed Item")]
        [SerializeField] [Range(1.04f, 1.10f)] float freedItemOvershootScale = 1.07f;
        [SerializeField] [Range(0.03f, 0.05f)] float freedItemOffsetY = 0.04f;
        [SerializeField] [Range(0.12f, 0.22f)] float freedItemDuration = 0.16f;
        [SerializeField] AnimationCurve freedItemCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("FX")]
        [SerializeField] [Range(1.0f, 1.25f)] float fxScale = 1.1f;
        [SerializeField] [Range(0.15f, 0.25f)] float fxDuration = 0.20f;
        [SerializeField] Color fxColor = new Color(1f, 1f, 1f, 0.85f);

        [SerializeField] bool useUnscaledTime = true;

        public float UnlockMagnetStrength => unlockMagnetStrength;
        public float ItemHoverScale => itemHoverScale;
        public float WebHoverScale => webHoverScale;
        public float WebHoverAlpha => webHoverAlpha;
        public float WebHoverDuration => webHoverDuration;
        public Vector2 ApproachDragScale => approachDragScale;
        public float ImpactDuration => impactDuration;
        public AnimationCurve ImpactCurve => impactCurve;
        public float WebBreakScale => webBreakScale;
        public float WebBreakDuration => webBreakDuration;
        public AnimationCurve WebBreakCurve => webBreakCurve;
        public float FreedItemOvershootScale => freedItemOvershootScale;
        public float FreedItemOffsetY => freedItemOffsetY;
        public float FreedItemDuration => freedItemDuration;
        public AnimationCurve FreedItemCurve => freedItemCurve;
        public float FxScale => fxScale;
        public float FxDuration => fxDuration;
        public Color FxColor => fxColor;
        public bool UseUnscaledTime => useUnscaledTime;

        public float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
