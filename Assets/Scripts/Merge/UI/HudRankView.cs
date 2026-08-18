using TMPro;
using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class HudRankView : MonoBehaviour
    {
        const float RankPopPeak = 1.12f;
        const float RankPopDuration = 0.22f;

        [SerializeField] TMP_Text rankText;
        [SerializeField] GameObject unclaimedBadge;

        PlayerProgressionController _progression;
        int _displayedRank = int.MinValue;
        Vector3 _baseScale = Vector3.one;
        bool _baseCaptured;
        float _popElapsed = -1f;

        public bool HasUnclaimedRankRewards =>
            _progression != null && _progression.HasUnclaimedRankRewards();

        void Awake()
        {
            CaptureBaseScale();
        }

        void OnEnable()
        {
            BindProgression();
            Refresh(false);
        }

        void OnDisable()
        {
            UnbindProgression();
            RestoreScale();
            _popElapsed = -1f;
        }

        void LateUpdate()
        {
            if (_progression == null)
            {
                BindProgression();
            }

            TickPop();
        }

        void BindProgression()
        {
            var current = PlayerProgressionController.Current;
            if (current == _progression && _progression != null)
            {
                return;
            }

            UnbindProgression();
            _progression = current;
            if (_progression == null)
            {
                return;
            }

            _progression.XpChanged += OnProgressionChanged;
            _progression.RankReached += OnRankReached;
            _progression.RankRewardClaimed += OnRewardClaimed;
            Refresh(false);
        }

        void UnbindProgression()
        {
            if (_progression == null)
            {
                return;
            }

            _progression.XpChanged -= OnProgressionChanged;
            _progression.RankReached -= OnRankReached;
            _progression.RankRewardClaimed -= OnRewardClaimed;
            _progression = null;
        }

        void OnProgressionChanged()
        {
            Refresh(false);
        }

        void OnRankReached(int rank)
        {
            Refresh(true);
        }

        void OnRewardClaimed(RankRewardClaim claim)
        {
            Refresh(false);
        }

        void Refresh(bool pop)
        {
            if (_progression == null)
            {
                return;
            }

            var rank = _progression.CurrentRank;
            if (rankText != null && rank != _displayedRank)
            {
                rankText.text = rank.ToString();
                _displayedRank = rank;
            }

            if (unclaimedBadge != null)
            {
                var show = _progression.HasUnclaimedRankRewards();
                if (unclaimedBadge.activeSelf != show)
                {
                    unclaimedBadge.SetActive(show);
                }
            }

            if (pop)
            {
                StartPop();
            }
        }

        void StartPop()
        {
            CaptureBaseScale();
            _popElapsed = 0f;
        }

        void TickPop()
        {
            if (_popElapsed < 0f || rankText == null)
            {
                return;
            }

            _popElapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_popElapsed / RankPopDuration);
            var up = t < 0.45f ? t / 0.45f : 1f - (t - 0.45f) / 0.55f;
            rankText.rectTransform.localScale = _baseScale * Mathf.LerpUnclamped(1f, RankPopPeak, Mathf.Clamp01(up));
            if (t >= 1f)
            {
                RestoreScale();
                _popElapsed = -1f;
            }
        }

        void CaptureBaseScale()
        {
            if (_baseCaptured || rankText == null)
            {
                return;
            }

            _baseScale = rankText.rectTransform.localScale;
            _baseCaptured = true;
        }

        void RestoreScale()
        {
            if (rankText != null && _baseCaptured)
            {
                rankText.rectTransform.localScale = _baseScale;
            }
        }
    }
}
