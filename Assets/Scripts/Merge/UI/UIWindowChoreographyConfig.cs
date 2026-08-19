using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "UIWindowChoreographyConfig", menuName = "San Island/UI Window Choreography Config")]
    public class UIWindowChoreographyConfig : ScriptableObject
    {
        [Header("Background")]
        [SerializeField] [Range(0.12f, 0.40f)] float backgroundFadeDuration = 0.24f;
        [SerializeField] [Range(0.90f, 1f)] float popupBackgroundStartScale = 0.94f;

        [Header("Edge")]
        [SerializeField] float edgeOffset = 70f;
        [SerializeField] [Range(0.22f, 0.50f)] float edgeDuration = 0.36f;
        [SerializeField] float edgeOvershoot = 4f;
        [SerializeField] AnimationCurve edgeCurve = CreateEaseOutBack();

        [Header("Center / FadeScale")]
        [SerializeField] [Range(0.86f, 0.98f)] float centerStartScale = 0.92f;
        [SerializeField] [Range(0.22f, 0.42f)] float centerDuration = 0.32f;
        [SerializeField] [Range(1f, 1.04f)] float centerOvershootScale = 1.012f;
        [SerializeField] AnimationCurve centerCurve = CreateEaseOutBack();

        [Header("Phases")]
        [SerializeField] float edgePhaseDelay = 0.08f;
        [SerializeField] float centerPhaseDelay = 0.16f;
        [SerializeField] float listPhaseDelay = 0.24f;
        [SerializeField] float secondaryPhaseDelay = 0.30f;
        [SerializeField] [Range(0f, 0.08f)] float intraPhaseStagger = 0.025f;

        [Header("List")]
        [SerializeField] [Range(0.86f, 0.98f)] float listItemStartScale = 0.92f;
        [SerializeField] [Range(0.18f, 0.40f)] float listItemDuration = 0.28f;
        [SerializeField] [Range(0.04f, 0.12f)] float listItemStagger = 0.05f;
        [SerializeField] float listItemOffsetY = 12f;
        [SerializeField] [Range(4, 16)] int maxAnimatedListItems = 10;
        [SerializeField] AnimationCurve listItemCurve = CreateEaseOut();

        [Header("Close")]
        [SerializeField] [Range(0.35f, 0.80f)] float closeDurationMultiplier = 0.55f;
        [SerializeField] [Range(0.02f, 0.10f)] float closeStagger = 0.04f;
        [SerializeField] [Range(0.20f, 0.70f)] float closeEdgeOffsetFactor = 0.45f;
        [SerializeField] [Range(0.24f, 0.50f)] float maxCloseDuration = 0.42f;

        [Header("Timing Caps")]
        [SerializeField] [Range(0.55f, 1.10f)] float maxFullscreenOpenDuration = 0.90f;
        [SerializeField] [Range(0.28f, 0.65f)] float maxPopupOpenDuration = 0.50f;
        [SerializeField] [Range(0.40f, 0.90f)] float interactionEnableNormalizedTime = 0.65f;

        [Header("Modal Background")]
        [SerializeField] [Range(0f, 1f)] float optionalModalBackgroundAlpha = 0.45f;
        [SerializeField] [Range(0.06f, 0.30f)] float modalBackgroundDuration = 0.16f;

        public float BackgroundFadeDuration => backgroundFadeDuration;
        public float PopupBackgroundStartScale => popupBackgroundStartScale;
        public float EdgeOffset => edgeOffset;
        public float EdgeDuration => edgeDuration;
        public float EdgeOvershoot => edgeOvershoot;
        public AnimationCurve EdgeCurve => edgeCurve != null && edgeCurve.length > 0 ? edgeCurve : CachedEaseOutBack;
        public float CenterStartScale => centerStartScale;
        public float CenterDuration => centerDuration;
        public float CenterOvershootScale => centerOvershootScale;
        public AnimationCurve CenterCurve => centerCurve != null && centerCurve.length > 0 ? centerCurve : CachedEaseOutBack;
        public float EdgePhaseDelay => edgePhaseDelay;
        public float CenterPhaseDelay => centerPhaseDelay;
        public float ListPhaseDelay => listPhaseDelay;
        public float SecondaryPhaseDelay => secondaryPhaseDelay;
        public float IntraPhaseStagger => intraPhaseStagger;
        public float ListItemStartScale => listItemStartScale;
        public float ListItemDuration => listItemDuration;
        public float ListItemStagger => listItemStagger;
        public float ListItemOffsetY => listItemOffsetY;
        public int MaxAnimatedListItems => maxAnimatedListItems;
        public AnimationCurve ListItemCurve => listItemCurve != null && listItemCurve.length > 0 ? listItemCurve : CachedEaseOut;
        public float CloseDurationMultiplier => closeDurationMultiplier;
        public float CloseStagger => closeStagger;
        public float CloseEdgeOffsetFactor => closeEdgeOffsetFactor;
        public float MaxCloseDuration => maxCloseDuration;
        public float MaxFullscreenOpenDuration => maxFullscreenOpenDuration;
        public float MaxPopupOpenDuration => maxPopupOpenDuration;
        public float InteractionEnableNormalizedTime => interactionEnableNormalizedTime;
        public float OptionalModalBackgroundAlpha => optionalModalBackgroundAlpha;
        public float ModalBackgroundDuration => modalBackgroundDuration;

        public static AnimationCurve CachedEaseOut => _cachedEaseOut ?? (_cachedEaseOut = CreateEaseOut());
        public static AnimationCurve CachedEaseIn => _cachedEaseIn ?? (_cachedEaseIn = CreateEaseIn());
        public static AnimationCurve CachedEaseOutBack => _cachedEaseOutBack ?? (_cachedEaseOutBack = CreateEaseOutBack());

        static AnimationCurve _cachedEaseOut;
        static AnimationCurve _cachedEaseIn;
        static AnimationCurve _cachedEaseOutBack;

        public static AnimationCurve CreateEaseOut()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 2.2f), new Keyframe(1f, 1f, 0f, 0f));
        }

        public static AnimationCurve CreateEaseOutBack()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2.4f),
                new Keyframe(0.78f, 1.06f, 0f, 0f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        public static AnimationCurve CreateEaseIn()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.2f, 0f));
        }
    }
}
