using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "BoardGeneratorAnimationConfig", menuName = "San Island/Board Generator Animation Config")]
    public class BoardGeneratorAnimationConfig : ScriptableObject
    {
        [Header("Generator Tap")]
        [SerializeField] Vector2 tapScale = new Vector2(1.05f, 0.95f);
        [SerializeField] [Range(1.04f, 1.12f)] float tapPeakScale = 1.08f;
        [SerializeField] [Range(0.06f, 0.12f)] float tapDuration = 0.08f;
        [SerializeField] AnimationCurve tapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Production Burst")]
        [SerializeField] [Range(0.08f, 0.18f)] float productionBurstDuration = 0.12f;
        [SerializeField] [Range(0.7f, 1.2f)] float productionBurstScale = 0.9f;
        [SerializeField] Color productionBurstColor = new Color(1f, 0.95f, 0.75f, 0.65f);

        [Header("Flight Arc")]
        [SerializeField] [Range(0.55f, 0.75f)] float flightStartScale = 0.65f;
        [SerializeField] [Range(1.0f, 1.12f)] float flightPopScale = 1.05f;
        [SerializeField] [Range(0.04f, 0.12f)] float flightPopDuration = 0.07f;
        [SerializeField] [Range(0.20f, 0.35f)] float flightDuration = 0.25f;
        [SerializeField] [Range(40f, 80f)] float arcHeight = 56f;
        [SerializeField] AnimationCurve flightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] [Range(1.0f, 1.1f)] float flightScale = 1.05f;
        [SerializeField] [Range(0f, 5f)] float flightRotationAmount = 4f;

        [Header("Landing")]
        [SerializeField] [Range(1.02f, 1.08f)] float landingImpactScaleX = 1.05f;
        [SerializeField] [Range(0.90f, 0.98f)] float landingImpactScaleY = 0.94f;
        [SerializeField] [Range(0.94f, 1.0f)] float landingReboundScaleX = 0.98f;
        [SerializeField] [Range(1.0f, 1.06f)] float landingReboundScaleY = 1.03f;
        [SerializeField] [Range(0.10f, 0.22f)] float landingDuration = 0.15f;
        [SerializeField] AnimationCurve landingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Spawn Item (cell settle, legacy)")]
        [SerializeField] [Range(0.5f, 0.7f)] float spawnStartScale = 0.6f;
        [SerializeField] [Range(1.05f, 1.15f)] float spawnOvershootScale = 1.1f;
        [SerializeField] [Range(0.96f, 1f)] float spawnReboundScale = 0.98f;
        [SerializeField] [Range(0.03f, 0.06f)] float spawnOffsetY = 0.04f;
        [SerializeField] [Range(0.14f, 0.24f)] float spawnSettleDuration = 0.18f;
        [SerializeField] AnimationCurve spawnSettleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Message")]
        [SerializeField] [Range(1.2f, 2.2f)] float messageStayDuration = 1.7f;
        [SerializeField] [Range(0.15f, 0.28f)] float messageShowDuration = 0.2f;
        [SerializeField] [Range(0.12f, 0.22f)] float messageHideDuration = 0.16f;
        [SerializeField] float messageRisePixels = 20f;
        [SerializeField] AnimationCurve messageShowCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] AnimationCurve messageHideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField] bool useUnscaledTime = true;

        public Vector2 TapScale => tapScale;
        public float TapPeakScale => tapPeakScale;
        public float TapDuration => tapDuration;
        public AnimationCurve TapCurve => tapCurve;
        public float ProductionBurstDuration => productionBurstDuration;
        public float ProductionBurstScale => productionBurstScale;
        public Color ProductionBurstColor => productionBurstColor;

        public float FlightStartScale => flightStartScale;
        public float FlightPopScale => flightPopScale;
        public float FlightPopDuration => flightPopDuration;
        public float FlightDuration => flightDuration;
        public float ArcHeight => arcHeight;
        public AnimationCurve FlightCurve => flightCurve;
        public float FlightScale => flightScale;
        public float FlightRotationAmount => flightRotationAmount;

        public float LandingImpactScaleX => landingImpactScaleX;
        public float LandingImpactScaleY => landingImpactScaleY;
        public float LandingReboundScaleX => landingReboundScaleX;
        public float LandingReboundScaleY => landingReboundScaleY;
        public float LandingDuration => landingDuration;
        public AnimationCurve LandingCurve => landingCurve;

        public float SpawnStartScale => spawnStartScale;
        public float SpawnOvershootScale => spawnOvershootScale;
        public float SpawnReboundScale => spawnReboundScale;
        public float SpawnOffsetY => spawnOffsetY;
        public float SpawnSettleDuration => spawnSettleDuration;
        public AnimationCurve SpawnSettleCurve => spawnSettleCurve;

        // Backward-compatible aliases used by older call sites.
        public float SpawnFlightDuration => flightDuration;
        public float SpawnFlightHeight => arcHeight;
        public AnimationCurve SpawnFlightCurve => flightCurve;

        public float MessageStayDuration => messageStayDuration;
        public float MessageShowDuration => messageShowDuration;
        public float MessageHideDuration => messageHideDuration;
        public float MessageRisePixels => messageRisePixels;
        public AnimationCurve MessageShowCurve => messageShowCurve;
        public AnimationCurve MessageHideCurve => messageHideCurve;
        public bool UseUnscaledTime => useUnscaledTime;

        public float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
