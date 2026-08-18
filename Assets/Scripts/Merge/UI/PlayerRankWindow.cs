using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class PlayerRankWindow : MonoBehaviour, IUIWindowWillOpen
    {
        const float MinFillDuration = 0.4f;
        const float MaxFillDuration = 0.8f;
        const float MultiRankFillDuration = 0.22f;
        const float RankResetPause = 0.05f;
        const float ClaimPopPeak = 1.12f;
        const float ClaimPopDuration = 0.22f;

        [SerializeField] Slider xpSlider;
        [SerializeField] TMP_Text xpText;
        [SerializeField] TMP_Text rankText;
        [SerializeField] ScrollRect roadmapScroll;
        [SerializeField] RectTransform milestoneRoot;
        [SerializeField] PlayerRankMilestoneView milestoneTemplate;
        [SerializeField] RectTransform claimVisual;
        [SerializeField] TMP_Text claimText;
        [SerializeField] GameObject battlePassRoot;
        [SerializeField] Selectable battlePassSelectable;

        readonly List<PlayerRankMilestoneView> _milestones = new List<PlayerRankMilestoneView>(8);
        PlayerProgressionController _progression;
        Coroutine _xpRoutine;
        Coroutine _claimPopRoutine;
        string _claimIdleText;
        bool _claimIdleCaptured;
        bool _roadmapBuilt;
        int _displayedRank = 1;
        float _displayedProgress;
        bool _displaySeeded;
        Vector3 _claimBaseScale = Vector3.one;
        bool _claimScaleCaptured;

        public PlayerRankDefinition LastReachedMilestone { get; private set; }
        public PlayerRankDefinition NextMilestone { get; private set; }

        public void OnWindowWillOpen()
        {
            BindProgression();
            EnsureRoadmap();
            _displaySeeded = false;
            SnapDisplayed();
            RefreshMilestones();
            RefreshRewardAndBattlePass();
            ScrollToCurrentMilestone();
            var window = GetComponent<UIWindow>();
            window?.Choreography.Invalidate();
        }

        void Awake()
        {
            CaptureClaimIdle();
            CaptureClaimScale();
            if (xpSlider != null)
            {
                xpSlider.interactable = false;
                xpSlider.minValue = 0f;
                xpSlider.maxValue = 1f;
            }
        }

        void OnEnable()
        {
            BindProgression();
            EnsureRoadmap();
            RefreshStatic();
            if (!_displaySeeded)
            {
                SnapDisplayed();
            }
        }

        void OnDisable()
        {
            StopXpRoutine();
            StopClaimPop();
            UnbindProgression();
        }

        public void TryClaimFromUi()
        {
            if (_progression == null || !_progression.HasUnclaimedRankRewards())
            {
                return;
            }

            _progression.TryClaimNextReward();
        }

        public void HandleBattlePassClicked()
        {
            if (_progression == null || !_progression.IsBattlePassUnlocked())
            {
                return;
            }

            var message = MergeUiLocalization.Get(MergeUiLocalization.RankBattlePassDevKey);
            if (string.IsNullOrEmpty(message))
            {
                message = "Battle Pass is not implemented yet.";
            }

            var presenter = OrderSystem.Current != null
                ? OrderSystem.Current.GetComponent<BoardController>()?.MessagePresenter
                : null;
            var origin = battlePassRoot != null ? battlePassRoot.transform.position : transform.position;
            if (presenter != null)
            {
                presenter.Show(MessageKind.Info, message, RectTransformUtility.WorldToScreenPoint(null, origin));
            }
            else
            {
                Debug.Log("[PlayerProgression] " + message);
            }
        }

        void BindProgression()
        {
            var current = PlayerProgressionController.Current;
            if (current == _progression)
            {
                return;
            }

            UnbindProgression();
            _progression = current;
            if (_progression == null)
            {
                return;
            }

            _progression.XpChanged += OnXpChanged;
            _progression.RankReached += OnRankReached;
            _progression.RankRewardClaimed += OnRewardClaimed;
            _progression.ContentUnlocked += OnContentUnlocked;
        }

        void UnbindProgression()
        {
            if (_progression == null)
            {
                return;
            }

            _progression.XpChanged -= OnXpChanged;
            _progression.RankReached -= OnRankReached;
            _progression.RankRewardClaimed -= OnRewardClaimed;
            _progression.ContentUnlocked -= OnContentUnlocked;
            _progression = null;
        }

        void OnXpChanged()
        {
            if (!isActiveAndEnabled)
            {
                _displaySeeded = false;
                RefreshStatic();
                return;
            }

            PlayXpToCurrent();
            RefreshRewardAndBattlePass();
            RefreshMilestones();
        }

        void OnRankReached(int rank)
        {
            RefreshMilestones();
            RefreshRewardAndBattlePass();
        }

        void OnRewardClaimed(RankRewardClaim claim)
        {
            PlayClaimPop();
            RefreshRewardAndBattlePass();
        }

        void OnContentUnlocked(RankContentUnlock unlock)
        {
            RefreshMilestones();
            RefreshRewardAndBattlePass();
        }

        void EnsureRoadmap()
        {
            if (_roadmapBuilt)
            {
                RefreshMilestones();
                return;
            }

            if (milestoneTemplate == null || milestoneRoot == null || _progression == null || _progression.Config == null)
            {
                return;
            }

            milestoneTemplate.gameObject.SetActive(false);
            var definitions = _progression.Config.GetMilestoneRanks();
            for (var i = 0; i < definitions.Count; i++)
            {
                var clone = Instantiate(milestoneTemplate, milestoneRoot);
                clone.gameObject.SetActive(true);
                clone.name = milestoneTemplate.name + " Rank " + definitions[i].rank;
                clone.BindLocal();
                _milestones.Add(clone);
            }

            _roadmapBuilt = true;
            RefreshMilestones();
        }

        void RefreshStatic()
        {
            RefreshMilestones();
            RefreshRewardAndBattlePass();
            if (isActiveAndEnabled && !_displaySeeded)
            {
                SnapDisplayed();
            }
            else
            {
                ApplyXpVisual(_displayedRank, _displayedProgress);
            }
        }

        void RefreshMilestones()
        {
            if (_progression == null || _progression.Config == null)
            {
                LastReachedMilestone = null;
                NextMilestone = null;
                return;
            }

            LastReachedMilestone = _progression.GetLastReachedMilestone();
            NextMilestone = _progression.GetNextMilestone();
            var current = _progression.CurrentRank;
            var definitions = _progression.Config.GetMilestoneRanks();
            var count = Mathf.Min(_milestones.Count, definitions.Count);
            for (var i = 0; i < count; i++)
            {
                var definition = definitions[i];
                RankMilestoneCardState state;
                if (definition.rank > current)
                {
                    state = RankMilestoneCardState.Locked;
                }
                else if (LastReachedMilestone != null && definition.rank == LastReachedMilestone.rank)
                {
                    state = RankMilestoneCardState.Current;
                }
                else
                {
                    state = RankMilestoneCardState.Completed;
                }

                _milestones[i].Apply(definition, state);
            }
        }

        void RefreshRewardAndBattlePass()
        {
            CaptureClaimIdle();
            if (claimText != null)
            {
                if (_progression != null &&
                    _progression.TryGetNextUnclaimedReward(out var rank, out var reward))
                {
                    claimText.text = FormatClaimText(rank, reward);
                }
                else if (_claimIdleCaptured)
                {
                    claimText.text = _claimIdleText;
                }
            }

            var unlocked = _progression != null && _progression.IsBattlePassUnlocked();
            if (battlePassRoot != null)
            {
                var group = battlePassRoot.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = battlePassRoot.AddComponent<CanvasGroup>();
                }

                group.alpha = unlocked ? 1f : 0f;
                group.interactable = unlocked;
                group.blocksRaycasts = unlocked;
            }

            if (battlePassSelectable != null)
            {
                battlePassSelectable.interactable = unlocked;
            }
        }

        void ScrollToCurrentMilestone()
        {
            if (roadmapScroll == null || _milestones.Count == 0)
            {
                return;
            }

            var target = NextMilestone != null ? NextMilestone.rank : (LastReachedMilestone != null ? LastReachedMilestone.rank : 0);
            PlayerRankMilestoneView view = null;
            for (var i = 0; i < _milestones.Count; i++)
            {
                if (_milestones[i].Rank == target)
                {
                    view = _milestones[i];
                    break;
                }
            }

            if (view == null)
            {
                view = _milestones[Mathf.Clamp(_milestones.Count / 2, 0, _milestones.Count - 1)];
            }

            Canvas.ForceUpdateCanvases();
            var content = roadmapScroll.content;
            if (content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }

            var viewport = roadmapScroll.viewport != null ? roadmapScroll.viewport : roadmapScroll.transform as RectTransform;
            var item = view.Rect;
            if (content == null || viewport == null || item == null)
            {
                return;
            }

            var contentWidth = content.rect.width;
            var viewportWidth = viewport.rect.width;
            if (contentWidth <= viewportWidth + 1f)
            {
                roadmapScroll.horizontalNormalizedPosition = 0f;
                return;
            }

            var itemCenter = content.InverseTransformPoint(item.TransformPoint(item.rect.center)).x;
            var targetX = itemCenter - viewportWidth * 0.5f;
            var max = contentWidth - viewportWidth;
            roadmapScroll.horizontalNormalizedPosition = Mathf.Clamp01(targetX / max);
        }

        void SnapDisplayed()
        {
            var snapshot = _progression != null ? _progression.GetSnapshot() : null;
            _displayedRank = snapshot != null ? snapshot.CurrentRank : 1;
            _displayedProgress = snapshot != null ? snapshot.Progress01 : 0f;
            _displaySeeded = true;
            ApplyXpVisual(_displayedRank, _displayedProgress);
        }

        void PlayXpToCurrent()
        {
            if (_progression == null)
            {
                return;
            }

            StopXpRoutine();
            _xpRoutine = StartCoroutine(AnimateXpRoutine(_progression.GetSnapshot()));
        }

        IEnumerator AnimateXpRoutine(PlayerProgressionSnapshot target)
        {
            if (!_displaySeeded)
            {
                SnapDisplayed();
            }

            var fromRank = _displayedRank;
            var toRank = target.CurrentRank;
            var multi = toRank > fromRank + 1;

            for (var rank = fromRank; rank < toRank; rank++)
            {
                yield return FillTo(1f, multi ? MultiRankFillDuration : LerpDuration(_displayedProgress, 1f));
                _displayedRank = rank + 1;
                ApplyXpVisual(_displayedRank, 1f);
                yield return new WaitForSecondsRealtime(RankResetPause);
                _displayedProgress = 0f;
                ApplyXpVisual(_displayedRank, 0f);
            }

            var end = target.IsMaxRank ? 1f : target.Progress01;
            yield return FillTo(end, multi ? MultiRankFillDuration : LerpDuration(_displayedProgress, end));
            _displayedRank = toRank;
            _displayedProgress = end;
            ApplyXpVisual(_displayedRank, _displayedProgress);
            _xpRoutine = null;
        }

        IEnumerator FillTo(float target, float duration)
        {
            var from = _displayedProgress;
            duration = Mathf.Max(0.01f, duration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = 1f - (1f - t) * (1f - t);
                _displayedProgress = Mathf.LerpUnclamped(from, target, t);
                ApplyXpVisual(_displayedRank, _displayedProgress);
                yield return null;
            }

            _displayedProgress = target;
            ApplyXpVisual(_displayedRank, _displayedProgress);
        }

        static float LerpDuration(float from, float to)
        {
            var distance = Mathf.Clamp01(Mathf.Abs(to - from));
            return Mathf.Lerp(MinFillDuration, MaxFillDuration, distance);
        }

        void ApplyXpVisual(int rank, float progress01)
        {
            if (xpSlider != null)
            {
                xpSlider.SetValueWithoutNotify(Mathf.Clamp01(progress01));
            }

            if (rankText != null)
            {
                rankText.text = rank.ToString();
            }

            if (xpText != null && _progression != null)
            {
                var snapshot = _progression.GetSnapshot();
                if (snapshot.IsMaxRank)
                {
                    var max = MergeUiLocalization.Get(MergeUiLocalization.RankMaxKey);
                    xpText.text = string.IsNullOrEmpty(max) ? "MAX" : max;
                }
                else
                {
                    var format = MergeUiLocalization.Get(MergeUiLocalization.RankXpProgressKey);
                    xpText.text = string.IsNullOrEmpty(format)
                        ? $"{snapshot.XpIntoCurrentRank}/{snapshot.XpRequiredForNextRank}"
                        : string.Format(format, snapshot.XpIntoCurrentRank, snapshot.XpRequiredForNextRank);
                }
            }
        }

        void PlayClaimPop()
        {
            if (claimVisual == null)
            {
                return;
            }

            CaptureClaimScale();
            StopClaimPop();
            _claimPopRoutine = StartCoroutine(ClaimPopRoutine());
        }

        IEnumerator ClaimPopRoutine()
        {
            var elapsed = 0f;
            while (elapsed < ClaimPopDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / ClaimPopDuration);
                var up = t < 0.45f ? t / 0.45f : 1f - (t - 0.45f) / 0.55f;
                claimVisual.localScale = _claimBaseScale * Mathf.LerpUnclamped(1f, ClaimPopPeak, Mathf.Clamp01(up));
                yield return null;
            }

            claimVisual.localScale = _claimBaseScale;
            _claimPopRoutine = null;
        }

        void CaptureClaimIdle()
        {
            if (_claimIdleCaptured || claimText == null)
            {
                return;
            }

            _claimIdleText = claimText.text;
            _claimIdleCaptured = true;
        }

        void CaptureClaimScale()
        {
            if (_claimScaleCaptured || claimVisual == null)
            {
                return;
            }

            _claimBaseScale = claimVisual.localScale;
            _claimScaleCaptured = true;
        }

        void StopXpRoutine()
        {
            if (_xpRoutine != null)
            {
                StopCoroutine(_xpRoutine);
                _xpRoutine = null;
            }
        }

        void StopClaimPop()
        {
            if (_claimPopRoutine != null)
            {
                StopCoroutine(_claimPopRoutine);
                _claimPopRoutine = null;
            }

            if (claimVisual != null && _claimScaleCaptured)
            {
                claimVisual.localScale = _claimBaseScale;
            }
        }

        static string FormatClaimText(int rank, RankRewardData reward)
        {
            var claim = MergeUiLocalization.Get(MergeUiLocalization.RankRewardClaimKey);
            if (string.IsNullOrEmpty(claim))
            {
                claim = "Claim";
            }

            if (reward == null)
            {
                return claim;
            }

            string detail;
            switch (reward.type)
            {
                case RankRewardType.Energy:
                    detail = string.Format(OrKey(MergeUiLocalization.RankRewardEnergyKey, "Energy +{0}"), reward.amount);
                    break;
                case RankRewardType.Coins:
                    detail = string.Format(OrKey(MergeUiLocalization.RankRewardCoinsKey, "Coins +{0}"), reward.amount);
                    break;
                default:
                    detail = OrKey(MergeUiLocalization.RankRewardGiftKey, "Gift");
                    break;
            }

            return claim + " · " + rank + "\n" + detail;
        }

        static string OrKey(string key, string fallback)
        {
            var value = MergeUiLocalization.Get(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
