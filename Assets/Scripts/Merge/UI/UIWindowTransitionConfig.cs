using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "UIWindowTransitionConfig", menuName = "San Island/UI Window Transition Config")]
    public class UIWindowTransitionConfig : ScriptableObject
    {
        [Header("Open Enter")]
        [SerializeField] float openStartScaleX = 0.92f;
        [SerializeField] float openStartScaleY = 0.88f;
        [SerializeField] float openStartOffsetY = 28f;
        [SerializeField] [Range(0.06f, 0.22f)] float openEnterDuration = 0.12f;
        [SerializeField] AnimationCurve openEnterCurve = CreateEaseOut();

        [Header("Open Overshoot / Settle")]
        [SerializeField] float openOvershootScaleX = 1.018f;
        [SerializeField] float openOvershootScaleY = 1.03f;
        [SerializeField] float openOvershootOffsetY = -3f;
        [SerializeField] [Range(0.06f, 0.24f)] float openSettleDuration = 0.13f;
        [SerializeField] AnimationCurve openSettleCurve = CreateEaseOut();

        [Header("Open Fade")]
        [SerializeField] [Range(0.04f, 0.20f)] float fadeInDuration = 0.08f;

        [Header("Close Anticipation")]
        [SerializeField] float closeAnticipationScale = 1.012f;
        [SerializeField] [Range(0.02f, 0.10f)] float closeAnticipationDuration = 0.045f;
        [SerializeField] AnimationCurve closeAnticipationCurve = CreateEaseOut();

        [Header("Close Exit")]
        [SerializeField] float closeEndScaleX = 0.93f;
        [SerializeField] float closeEndScaleY = 0.90f;
        [SerializeField] float closeEndOffsetY = 22f;
        [SerializeField] [Range(0.08f, 0.28f)] float closeExitDuration = 0.14f;
        [SerializeField] AnimationCurve closeExitCurve = CreateEaseIn();
        [SerializeField] [Range(0.06f, 0.24f)] float fadeOutDuration = 0.12f;

        [Header("Content (optional)")]
        [SerializeField] [Range(0f, 0.12f)] float contentDelay = 0.04f;
        [SerializeField] [Range(0f, 1f)] float contentOffsetYFactor = 0.30f;

        [Header("Modal Background")]
        [SerializeField] [Range(0f, 1f)] float optionalModalBackgroundAlpha = 0.45f;
        [SerializeField] [Range(0.06f, 0.30f)] float modalBackgroundDuration = 0.16f;

        public float OpenStartScaleX => openStartScaleX;
        public float OpenStartScaleY => openStartScaleY;
        public float OpenStartOffsetY => openStartOffsetY;
        public float OpenEnterDuration => openEnterDuration;
        public AnimationCurve OpenEnterCurve => openEnterCurve;
        public float OpenOvershootScaleX => openOvershootScaleX;
        public float OpenOvershootScaleY => openOvershootScaleY;
        public float OpenOvershootOffsetY => openOvershootOffsetY;
        public float OpenSettleDuration => openSettleDuration;
        public AnimationCurve OpenSettleCurve => openSettleCurve;
        public float FadeInDuration => fadeInDuration;
        public float CloseAnticipationScale => closeAnticipationScale;
        public float CloseAnticipationDuration => closeAnticipationDuration;
        public AnimationCurve CloseAnticipationCurve => closeAnticipationCurve;
        public float CloseEndScaleX => closeEndScaleX;
        public float CloseEndScaleY => closeEndScaleY;
        public float CloseEndOffsetY => closeEndOffsetY;
        public float CloseExitDuration => closeExitDuration;
        public AnimationCurve CloseExitCurve => closeExitCurve;
        public float FadeOutDuration => fadeOutDuration;
        public float ContentDelay => contentDelay;
        public float ContentOffsetYFactor => contentOffsetYFactor;
        public float OptionalModalBackgroundAlpha => optionalModalBackgroundAlpha;
        public float ModalBackgroundDuration => modalBackgroundDuration;

        public static AnimationCurve CreateEaseOut()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 2.2f), new Keyframe(1f, 1f, 0f, 0f));
        }

        public static AnimationCurve CreateEaseIn()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.2f, 0f));
        }

        public static AnimationCurve CreateEaseInOut()
        {
            return AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }
}
