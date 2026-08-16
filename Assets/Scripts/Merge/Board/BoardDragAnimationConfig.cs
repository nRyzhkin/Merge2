using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "BoardDragAnimationConfig", menuName = "San Island/Board Drag Animation Config")]
    public class BoardDragAnimationConfig : ScriptableObject
    {
        [Header("Threshold")]
        [SerializeField] [Range(8f, 16f)] float dragThreshold = 12f;

        [Header("Pickup")]
        [SerializeField] Vector2 pickupStretch = new Vector2(0.91f, 1.16f);
        [SerializeField] [Range(0.08f, 0.12f)] float pickupDuration = 0.10f;
        [SerializeField] AnimationCurve pickupCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] [Range(0.08f, 0.12f)] float pickupLiftDuration = 0.10f;
        [SerializeField] AnimationCurve pickupLiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Drag")]
        [SerializeField] [Range(1.08f, 1.12f)] float dragScale = 1.10f;
        [SerializeField] [Range(0.04f, 0.10f)] float dragScaleDuration = 0.06f;
        [SerializeField] AnimationCurve dragScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] Vector2 mousePointerOffset = new Vector2(0f, 18f);
        [SerializeField] Vector2 touchPointerOffset = new Vector2(0f, 64f);
        [SerializeField] [Range(12f, 20f)] float dragFlightHeight = 16f;
        [SerializeField] [Range(0f, 0.08f)] float followSmoothTime = 0.03f;

        [Header("Landing")]
        [SerializeField] Vector2 dropApproachScale = new Vector2(1.03f, 1.03f);
        [SerializeField] [Range(0.08f, 0.16f)] float landingMoveDuration = 0.12f;
        [SerializeField] AnimationCurve landingMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] Vector2 landingImpactScale = new Vector2(1.055f, 0.94f);
        [SerializeField] Vector2 landingReboundScale = new Vector2(0.98f, 1.03f);
        [SerializeField] [Range(0.04f, 0.10f)] float landingSquashDuration = 0.06f;
        [SerializeField] [Range(0.04f, 0.10f)] float landingReboundDuration = 0.06f;
        [SerializeField] [Range(0.04f, 0.10f)] float landingSettleDuration = 0.05f;
        [SerializeField] AnimationCurve landingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] [Range(0.7f, 1f)] float sourceReturnDurationScale = 0.88f;

        [Header("Invalid Drop")]
        [SerializeField] float invalidDropSnapMaxDistance = 340f;

        [Header("Directional Merge Assist")]
        [SerializeField] bool directionalMergeAssistEnabled = true;
        [SerializeField] [Min(0f)] float directionalMergeMinSpeed = 650f;
        [SerializeField] [Range(1f, 2.5f)] float directionalMergeSearchDistanceInCells = 1.75f;
        [SerializeField] [Range(0.5f, 0.95f)] float directionalMergeMinDot = 0.70f;
        [SerializeField] [Range(0.5f, 2f)] float directionalMergeDirectionWeight = 1.15f;
        [SerializeField] [Range(0.1f, 1.5f)] float directionalMergeDistanceWeight = 0.55f;
        [SerializeField] [Range(3, 5)] int directionalMergeVelocitySampleCount = 5;
        [SerializeField] [Range(0.06f, 0.2f)] float directionalMergeVelocitySampleWindow = 0.12f;
        [SerializeField] bool directionalMergeIncludeCobwebTargets = true;
        [SerializeField] bool showDirectionalAssistDebug;

        [Header("Occupied Displace")]
        [SerializeField] [Range(0.03f, 0.06f)] float displaceLeadTime = 0.045f;
        [SerializeField] [Range(0.12f, 0.22f)] float displaceFlightDuration = 0.16f;
        [SerializeField] [Range(0.6f, 1f)] float displaceFlightHeightScale = 0.75f;
        [SerializeField] [Range(1.02f, 1.12f)] float displacePeakScale = 1.06f;

        [Header("Drop Target")]
        [SerializeField] Color dropTargetColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] [Range(0f, 0.08f)] float dropTargetPulseAmplitude = 0.03f;
        [SerializeField] [Range(0.4f, 1.5f)] float dropTargetPulsePeriod = 0.85f;

        [SerializeField] bool useUnscaledTime = true;

        public float DragThreshold => dragThreshold;
        public Vector2 PickupStretch => pickupStretch;
        public float PickupDuration => pickupDuration;
        public AnimationCurve PickupCurve => pickupCurve;
        public float PickupLiftDuration => pickupLiftDuration;
        public AnimationCurve PickupLiftCurve => pickupLiftCurve;
        public float DragScale => dragScale;
        public float DragScaleDuration => dragScaleDuration;
        public AnimationCurve DragScaleCurve => dragScaleCurve;
        public Vector2 MousePointerOffset => mousePointerOffset;
        public Vector2 TouchPointerOffset => touchPointerOffset;
        public float DragFlightHeight => dragFlightHeight;
        public float FollowSmoothTime => followSmoothTime;
        public Vector2 DropApproachScale => dropApproachScale;
        public float LandingMoveDuration => landingMoveDuration;
        public AnimationCurve LandingMoveCurve => landingMoveCurve;
        public Vector2 LandingImpactScale => landingImpactScale;
        public Vector2 LandingReboundScale => landingReboundScale;
        public float LandingSquashDuration => landingSquashDuration;
        public float LandingReboundDuration => landingReboundDuration;
        public float LandingSettleDuration => landingSettleDuration;
        public AnimationCurve LandingCurve => landingCurve;
        public float SourceReturnDurationScale => sourceReturnDurationScale;
        public float InvalidDropSnapMaxDistance => invalidDropSnapMaxDistance;
        public bool DirectionalMergeAssistEnabled => directionalMergeAssistEnabled;
        public float DirectionalMergeMinSpeed => directionalMergeMinSpeed;
        public float DirectionalMergeSearchDistanceInCells => directionalMergeSearchDistanceInCells;
        public float DirectionalMergeMinDot => directionalMergeMinDot;
        public float DirectionalMergeDirectionWeight => directionalMergeDirectionWeight;
        public float DirectionalMergeDistanceWeight => directionalMergeDistanceWeight;
        public int DirectionalMergeVelocitySampleCount => Mathf.Clamp(directionalMergeVelocitySampleCount, 3, 5);
        public float DirectionalMergeVelocitySampleWindow => directionalMergeVelocitySampleWindow;
        public bool DirectionalMergeIncludeCobwebTargets => directionalMergeIncludeCobwebTargets;
        public bool ShowDirectionalAssistDebug => showDirectionalAssistDebug;
        public float DisplaceLeadTime => displaceLeadTime;
        public float DisplaceFlightDuration => displaceFlightDuration;
        public float DisplaceFlightHeightScale => displaceFlightHeightScale;
        public float DisplacePeakScale => displacePeakScale;
        public Color DropTargetColor => dropTargetColor;
        public float DropTargetPulseAmplitude => dropTargetPulseAmplitude;
        public float DropTargetPulsePeriod => dropTargetPulsePeriod;
        public bool UseUnscaledTime => useUnscaledTime;

        public float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        public float GetDragThresholdScreen(float canvasScaleFactor)
        {
            return Mathf.Clamp(dragThreshold * Mathf.Max(0.75f, canvasScaleFactor), 8f, 24f);
        }

        public Vector2 GetPointerOffset(bool touch)
        {
            return touch ? touchPointerOffset : mousePointerOffset;
        }
    }
}
