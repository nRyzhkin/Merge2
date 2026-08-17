using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "San Island/Economy Config")]
    public class EconomyConfig : ScriptableObject
    {
        [Header("Sell Prices By Level (L1 = index 0)")]
        [SerializeField] long[] sellPricesByLevel =
        {
            10, 20, 40, 60, 120, 250, 510, 1020, 2050, 4100, 8200, 16400, 32800, 65600
        };

        [Header("Undo")]
        [SerializeField] [Min(0.5f)] float undoWindowSeconds = 5f;

        [Header("Sell Animation")]
        [SerializeField] [Range(1.04f, 1.12f)] float sellAnticipationScale = 1.08f;
        [SerializeField] [Range(0.05f, 0.12f)] float sellAnticipationDuration = 0.08f;
        [SerializeField] [Range(0.5f, 0.7f)] float sellShrinkScale = 0.6f;
        [SerializeField] [Range(0.08f, 0.16f)] float sellShrinkDuration = 0.12f;
        [SerializeField] AnimationCurve sellCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] [Range(8f, 24f)] float sellRisePixels = 14f;

        [Header("Coin Feedback")]
        [SerializeField] [Range(24f, 64f)] float coinPopupRisePixels = 42f;
        [SerializeField] [Range(0.35f, 0.7f)] float coinPopupDuration = 0.5f;
        [SerializeField] AnimationCurve coinPopupCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Undo Return")]
        [SerializeField] [Range(0.6f, 0.8f)] float undoStartScale = 0.7f;
        [SerializeField] [Range(1.04f, 1.1f)] float undoOvershootScale = 1.07f;
        [SerializeField] [Range(0.16f, 0.28f)] float undoReturnDuration = 0.2f;
        [SerializeField] AnimationCurve undoReturnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField] bool useUnscaledTime = true;

        public float UndoWindowSeconds => undoWindowSeconds > 0f ? undoWindowSeconds : 5f;
        public float SellAnticipationScale => sellAnticipationScale;
        public float SellAnticipationDuration => sellAnticipationDuration;
        public float SellShrinkScale => sellShrinkScale;
        public float SellShrinkDuration => sellShrinkDuration;
        public AnimationCurve SellCurve => sellCurve;
        public float SellRisePixels => sellRisePixels;
        public float CoinPopupRisePixels => coinPopupRisePixels;
        public float CoinPopupDuration => coinPopupDuration;
        public AnimationCurve CoinPopupCurve => coinPopupCurve;
        public float UndoStartScale => undoStartScale;
        public float UndoOvershootScale => undoOvershootScale;
        public float UndoReturnDuration => undoReturnDuration;
        public AnimationCurve UndoReturnCurve => undoReturnCurve;
        public bool UseUnscaledTime => useUnscaledTime;

        public bool TryGetSellPrice(int level, out long price)
        {
            price = 0;
            if (sellPricesByLevel == null || level < 1 || level > sellPricesByLevel.Length)
            {
                return false;
            }

            price = sellPricesByLevel[level - 1];
            return price > 0;
        }

        public float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
