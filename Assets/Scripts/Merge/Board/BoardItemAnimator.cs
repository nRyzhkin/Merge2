using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardItemAnimator : MonoBehaviour
    {
        enum Phase
        {
            Idle,
            Press,
            Release,
            Return,
            MergeCollision,
            MergeAbsorb,
            ResultGrow,
            ResultRebound,
            ResultSettle,
            FreedGrow,
            FreedSettle
        }

        enum MergePreviewPhase
        {
            Off,
            Accept,
            Hold,
            Exit
        }

        [SerializeField] BoardItemAnimationConfig config;
        [SerializeField] UiInteractionFeedbackConfig hoverConfig;
        [SerializeField] RectTransform itemRect;
        [SerializeField] RectTransform cellRect;

        Phase _phase = Phase.Idle;
        float _elapsed;
        float _duration;
        Vector2 _fromScale;
        Vector2 _toScale;
        Vector2 _actionScale = Vector2.one;
        float _fromDown;
        float _toDown;
        float _currentDown;
        Vector2 _idleOffsetMin;
        Vector2 _idleOffsetMax;
        bool _idleCaptured;
        AnimationCurve _curve;

        bool _hovered;
        bool _hoverTransitioning;
        float _hoverMultiplier = 1f;
        float _hoverFrom = 1f;
        float _hoverTo = 1f;
        float _hoverElapsed;
        float _hoverDuration;
        AnimationCurve _hoverCurve;

        MergePreviewPhase _mergePreviewPhase = MergePreviewPhase.Off;
        float _mergeTargetMultiplier = 1f;
        float _mergeTargetFrom = 1f;
        float _mergeTargetTo = 1f;
        float _mergeTargetElapsed;
        float _mergeTargetDuration = 0.10f;
        float _mergeBaseScale = 1.075f;
        float _mergeBreathAmount = 0.012f;
        float _mergeBreathDuration = 0.55f;
        float _mergeAcceptOvershoot = 0.015f;
        float _mergeBreathTime;
        float _mergeBreathWave = 1f;
        float _mergeAttractionMax;
        float _mergeAttractionSmooth = 0.08f;
        Vector2 _attractionOffset;
        Vector2 _attractionTarget;
        Vector2 _absorbPeak = Vector2.one;
        bool _absorbThroughPeak;
        float _resultReboundScale = 0.97f;
        float _resultDuration = 0.22f;
        float _cobwebMultiplier = 1f;
        float _cobwebFrom = 1f;
        float _cobwebTo = 1f;
        float _cobwebElapsed;
        float _cobwebDuration = 0.1f;
        bool _cobwebTransitioning;
        bool _cobwebActive;
        int _presentationRevision;
        int _actionRevision;

        public bool IsPlaying => _phase != Phase.Idle;
        public bool IsHovered => _hovered;
        public bool IsMergeTarget => _mergePreviewPhase == MergePreviewPhase.Accept || _mergePreviewPhase == MergePreviewPhase.Hold;
        public int PresentationRevision => _presentationRevision;

        public bool IsTransientAnimationRunning()
        {
            return _phase == Phase.MergeCollision
                   || _phase == Phase.MergeAbsorb
                   || _phase == Phase.ResultGrow
                   || _phase == Phase.ResultRebound
                   || _phase == Phase.ResultSettle
                   || _phase == Phase.FreedGrow
                   || _phase == Phase.FreedSettle;
        }

        public void Configure(
            BoardItemAnimationConfig animationConfig,
            RectTransform item,
            RectTransform cell,
            UiInteractionFeedbackConfig interactionConfig = null)
        {
            config = animationConfig;
            hoverConfig = interactionConfig;
            itemRect = item;
            cellRect = cell;
            CaptureIdle();
            ClearMergePreviewImmediate();
            ClearCobwebPreviewImmediate();
            _hovered = false;
            _hoverMultiplier = 1f;
            _hoverTransitioning = false;
            SnapActionToIdle();
            enabled = false;
        }

        public void SetHovered(bool hovered)
        {
            if (hoverConfig == null)
            {
                _hovered = hovered;
                _hoverMultiplier = 1f;
                _hoverTransitioning = false;
                ApplyPose();
                return;
            }

            var target = hovered ? hoverConfig.HoverScale : 1f;
            if (_hovered == hovered && !_hoverTransitioning && Mathf.Approximately(_hoverMultiplier, target))
            {
                return;
            }

            _hovered = hovered;
            _hoverFrom = _hoverMultiplier;
            _hoverTo = target;
            _hoverElapsed = 0f;
            _hoverDuration = Mathf.Max(0.01f, hovered ? hoverConfig.HoverEnterDuration : hoverConfig.HoverExitDuration);
            _hoverCurve = hovered ? hoverConfig.HoverEnterCurve : hoverConfig.HoverExitCurve;
            _hoverTransitioning = true;
            enabled = true;
        }

        public void PlayPressAnticipation()
        {
            if (IsTransientAnimationRunning())
            {
                CancelTransientAnimationAndAdoptCurrentVisualState();
            }

            if (!CanAnimate())
            {
                return;
            }

            CaptureIdleIfNeeded();
            BeginPhase(Phase.Press, config.PressDuration, config.PressCurve, config.PressScale, CellHeight() * config.PressDownNormalized);
        }

        public void PlayClickRelease()
        {
            if (!CanAnimate())
            {
                return;
            }

            CaptureIdleIfNeeded();
            BeginPhase(Phase.Release, config.ReleaseDuration, config.ReleaseCurve, config.ReleaseScale, 0f);
        }

        public void BeginDrag()
        {
            CancelTransientAnimationAndAdoptCurrentVisualState();
        }

        public void CancelTransientAnimationAndAdoptCurrentVisualState()
        {
            _presentationRevision++;
            ClearMergePreviewImmediate();
            ClearCobwebPreviewImmediate();
            _hovered = false;
            _hoverTransitioning = false;
            _hoverMultiplier = 1f;
            _absorbThroughPeak = false;
            _phase = Phase.Idle;
            _elapsed = 0f;
            _actionScale = Vector2.one;
            _currentDown = 0f;
            ApplyPose();
            RefreshEnabled();
        }

        public int BeginTransientPresentation()
        {
            _presentationRevision++;
            return _presentationRevision;
        }

        public bool IsPresentationRevisionCurrent(int revision)
        {
            return revision == _presentationRevision;
        }

        public void SetMergeTarget(bool active, BoardMergeAnimationConfig mergeConfig = null)
        {
            if (active)
            {
                SetHovered(false);
                ApplyMergeConfig(mergeConfig);
                if (_mergePreviewPhase == MergePreviewPhase.Accept || _mergePreviewPhase == MergePreviewPhase.Hold)
                {
                    enabled = true;
                    return;
                }

                CaptureIdleIfNeeded();
                _mergeTargetFrom = _mergeTargetMultiplier;
                _mergeTargetTo = _mergeBaseScale + _mergeAcceptOvershoot;
                _mergeTargetElapsed = 0f;
                _mergeTargetDuration = mergeConfig != null
                    ? Mathf.Max(0.08f, mergeConfig.MergeTargetAcceptDuration)
                    : 0.10f;
                _mergeBreathTime = 0f;
                _mergeBreathWave = 1f;
                _attractionTarget = Vector2.zero;
                _mergePreviewPhase = MergePreviewPhase.Accept;
                enabled = true;
                return;
            }

            if (_mergePreviewPhase == MergePreviewPhase.Off &&
                Mathf.Approximately(_mergeTargetMultiplier, 1f) &&
                _attractionOffset.sqrMagnitude < 0.0001f)
            {
                return;
            }

            _mergeTargetFrom = _mergeTargetMultiplier * _mergeBreathWave;
            _mergeTargetTo = 1f;
            _mergeTargetElapsed = 0f;
            _mergeTargetDuration = 0.09f;
            _attractionTarget = Vector2.zero;
            _mergePreviewPhase = MergePreviewPhase.Exit;
            enabled = true;
        }

        // Legacy overload used by older call sites.
        public void SetMergeTarget(bool active, float scale, float pulseDuration)
        {
            if (active)
            {
                _mergeBaseScale = Mathf.Clamp(scale, 1.06f, 1.09f);
                _mergeBreathDuration = Mathf.Clamp(pulseDuration, 0.45f, 0.70f);
            }

            SetMergeTarget(active, null);
        }

        public void UpdateMergeAttraction(Vector2 dragLayerPosition, Vector2 cellLayerCenter)
        {
            if (_mergePreviewPhase != MergePreviewPhase.Accept && _mergePreviewPhase != MergePreviewPhase.Hold)
            {
                _attractionTarget = Vector2.zero;
                return;
            }

            var delta = dragLayerPosition - cellLayerCenter;
            var deadZone = Mathf.Max(4f, CellSize() * 0.08f);
            if (delta.sqrMagnitude <= deadZone * deadZone)
            {
                _attractionTarget = Vector2.zero;
                return;
            }

            var maxOffset = CellSize() * _mergeAttractionMax;
            _attractionTarget = delta.normalized * maxOffset;
        }

        public void ClearMergeTargetImmediate()
        {
            ClearMergePreviewImmediate();
            ClearCobwebPreviewImmediate();
            ApplyPose();
            RefreshEnabled();
        }

        public void SetCobwebUnlockTarget(bool active, BoardCobwebAnimationConfig cobwebConfig = null)
        {
            SetMergeTarget(false);
            SetHovered(false);
            var target = active
                ? (cobwebConfig != null ? cobwebConfig.ItemHoverScale : 1.04f)
                : 1f;
            if (_cobwebActive == active && !_cobwebTransitioning && Mathf.Approximately(_cobwebMultiplier, target))
            {
                return;
            }

            _cobwebActive = active;
            _cobwebFrom = _cobwebMultiplier;
            _cobwebTo = target;
            _cobwebElapsed = 0f;
            _cobwebDuration = cobwebConfig != null ? Mathf.Max(0.01f, cobwebConfig.WebHoverDuration) : 0.10f;
            _cobwebTransitioning = true;
            enabled = true;
        }

        public void PlayFreedBounce(BoardCobwebAnimationConfig cobwebConfig)
        {
            if (itemRect == null || cobwebConfig == null)
            {
                return;
            }

            ClearCobwebPreviewImmediate();
            ClearMergePreviewImmediate();
            SetHovered(false);
            BeginTransientPresentation();
            CaptureIdleIfNeeded();
            _resultDuration = cobwebConfig.FreedItemDuration;
            var start = Vector2.one * 0.95f;
            var grow = Mathf.Max(0.04f, cobwebConfig.FreedItemDuration * 0.55f);
            var up = CellHeight() * cobwebConfig.FreedItemOffsetY;
            _actionScale = start;
            _currentDown = 0f;
            BeginPhase(Phase.FreedGrow, grow, cobwebConfig.FreedItemCurve, Vector2.one * cobwebConfig.FreedItemOvershootScale, -up);
            _fromScale = start;
            ApplyPose();
        }

        public void PlayMergeCollision(BoardMergeAnimationConfig mergeConfig)
        {
            if (itemRect == null || mergeConfig == null)
            {
                return;
            }

            ClearMergePreviewImmediate();
            CaptureIdleIfNeeded();
            BeginPhase(Phase.MergeCollision, mergeConfig.CollisionDuration, mergeConfig.CollisionCurve, mergeConfig.CollisionTargetScale, 0f);
        }

        public void PlayMergeAbsorb(BoardMergeAnimationConfig mergeConfig)
        {
            if (itemRect == null || mergeConfig == null)
            {
                return;
            }

            CaptureIdleIfNeeded();
            _absorbPeak = mergeConfig.AbsorbTargetScale;
            _absorbThroughPeak = true;
            BeginPhase(Phase.MergeAbsorb, mergeConfig.AbsorbDuration, mergeConfig.AbsorbCurve, Vector2.one * mergeConfig.AbsorbFinalScale, 0f);
        }

        public void PlayResultSpawn(BoardMergeAnimationConfig mergeConfig)
        {
            if (itemRect == null || mergeConfig == null)
            {
                return;
            }

            BeginTransientPresentation();
            ClearMergePreviewImmediate();
            SetHovered(false);
            CaptureIdleIfNeeded();
            _resultReboundScale = mergeConfig.ResultReboundScale;
            _resultDuration = mergeConfig.ResultDuration;
            var start = Vector2.one * mergeConfig.ResultStartScale;
            var grow = Mathf.Max(0.04f, mergeConfig.ResultDuration * 0.45f);
            var up = CellHeight() * mergeConfig.ResultSpawnOffsetY;
            _actionScale = start;
            _currentDown = 0f;
            BeginPhase(Phase.ResultGrow, grow, mergeConfig.ResultCurve, Vector2.one * mergeConfig.ResultOvershootScale, -up);
            _fromScale = start;
            ApplyPose();
        }

        public void PlayBoxItemReveal(BoardBoxAnimationConfig boxConfig)
        {
            if (itemRect == null || boxConfig == null)
            {
                return;
            }

            BeginTransientPresentation();
            ClearMergePreviewImmediate();
            SetHovered(false);
            CaptureIdleIfNeeded();
            _resultReboundScale = boxConfig.RevealedItemReboundScale;
            _resultDuration = boxConfig.RevealedItemDuration;
            var start = Vector2.one * boxConfig.RevealedItemStartScale;
            var grow = Mathf.Max(0.04f, boxConfig.RevealedItemDuration * 0.45f);
            var up = CellHeight() * boxConfig.RevealedItemOffsetY;
            _actionScale = start;
            _currentDown = 0f;
            BeginPhase(Phase.ResultGrow, grow, boxConfig.RevealedItemCurve, Vector2.one * boxConfig.RevealedItemOvershootScale, -up);
            _fromScale = start;
            ApplyPose();
        }

        public void PlayUndoReturn(EconomyConfig economy)
        {
            if (itemRect == null || economy == null)
            {
                return;
            }

            BeginTransientPresentation();
            ClearMergePreviewImmediate();
            SetHovered(false);
            CaptureIdleIfNeeded();
            _resultReboundScale = 1f;
            _resultDuration = economy.UndoReturnDuration;
            var start = Vector2.one * economy.UndoStartScale;
            var grow = Mathf.Max(0.04f, economy.UndoReturnDuration * 0.45f);
            _actionScale = start;
            _currentDown = 0f;
            BeginPhase(Phase.ResultGrow, grow, economy.UndoReturnCurve, Vector2.one * economy.UndoOvershootScale, 0f);
            _fromScale = start;
            ApplyPose();
        }

        public void PlayGeneratorTap(BoardGeneratorAnimationConfig generatorConfig)
        {
            if (itemRect == null || generatorConfig == null)
            {
                return;
            }

            if (IsTransientAnimationRunning())
            {
                CancelTransientAnimationAndAdoptCurrentVisualState();
            }

            CaptureIdleIfNeeded();
            SetHovered(false);
            var peak = Vector2.one * generatorConfig.TapPeakScale;
            var squash = generatorConfig.TapScale;
            BeginPhase(
                Phase.Press,
                generatorConfig.TapDuration * 0.45f,
                generatorConfig.TapCurve,
                new Vector2(peak.x * squash.x, peak.y * squash.y),
                0f);
        }

        public void PlayGeneratorItemSpawn(BoardGeneratorAnimationConfig generatorConfig)
        {
            if (itemRect == null || generatorConfig == null)
            {
                return;
            }

            BeginTransientPresentation();
            ClearMergePreviewImmediate();
            SetHovered(false);
            CaptureIdleIfNeeded();
            _resultReboundScale = generatorConfig.SpawnReboundScale;
            _resultDuration = generatorConfig.SpawnSettleDuration;
            var start = Vector2.one * generatorConfig.SpawnStartScale;
            var grow = Mathf.Max(0.04f, generatorConfig.SpawnSettleDuration * 0.45f);
            var up = CellHeight() * generatorConfig.SpawnOffsetY;
            _actionScale = start;
            _currentDown = 0f;
            BeginPhase(
                Phase.ResultGrow,
                grow,
                generatorConfig.SpawnSettleCurve,
                Vector2.one * generatorConfig.SpawnOvershootScale,
                -up);
            _fromScale = start;
            ApplyPose();
        }

        public void Drop()
        {
            SnapActionToIdle();
        }

        public void SnapActionToIdle()
        {
            _phase = Phase.Idle;
            _elapsed = 0f;
            _actionScale = Vector2.one;
            _currentDown = 0f;
            ApplyPose();
            RefreshEnabled();
        }

        void OnDisable()
        {
            _hovered = false;
            _hoverTransitioning = false;
            _hoverMultiplier = 1f;
            ClearMergePreviewImmediate();
            ClearCobwebPreviewImmediate();
            _phase = Phase.Idle;
            _actionScale = Vector2.one;
            _currentDown = 0f;
            ApplyPose();
        }

        void Update()
        {
            var dt = hoverConfig != null ? hoverConfig.GetDeltaTime() : Time.unscaledDeltaTime;
            var actionRunning = TickAction(dt);
            var hoverRunning = TickHover(dt);
            var mergeRunning = TickMergePreview(dt);
            var cobwebRunning = TickCobwebPreview(dt);
            ApplyPose();

            if (!actionRunning && !hoverRunning && !mergeRunning && !cobwebRunning && !_hovered &&
                _mergePreviewPhase == MergePreviewPhase.Off && !_cobwebActive)
            {
                enabled = false;
            }
        }

        bool TickAction(float dt)
        {
            if (_phase == Phase.Idle)
            {
                return false;
            }

            if (_actionRevision != _presentationRevision)
            {
                _phase = Phase.Idle;
                _elapsed = 0f;
                _actionScale = Vector2.one;
                _currentDown = 0f;
                return false;
            }

            _elapsed += dt;
            var t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            var curved = _curve != null ? _curve.Evaluate(t) : t;
            if (_phase == Phase.MergeAbsorb && _absorbThroughPeak)
            {
                TickAbsorb(t, curved);
            }
            else
            {
                _actionScale = Vector2.LerpUnclamped(_fromScale, _toScale, curved);
                _currentDown = Mathf.LerpUnclamped(_fromDown, _toDown, curved);
            }

            if (t < 1f)
            {
                return true;
            }

            _actionScale = _toScale;
            _currentDown = _toDown;
            if (_phase == Phase.Press)
            {
                return false;
            }

            if (_phase == Phase.Release)
            {
                if (config == null)
                {
                    _phase = Phase.Idle;
                    _actionScale = Vector2.one;
                    _currentDown = 0f;
                    return false;
                }

                BeginPhase(Phase.Return, config.ReturnDuration, config.ReturnCurve, Vector2.one, 0f);
                return true;
            }

            if (_phase == Phase.MergeCollision || _phase == Phase.MergeAbsorb)
            {
                _phase = Phase.Idle;
                return false;
            }

            if (_phase == Phase.ResultGrow)
            {
                BeginPhase(Phase.ResultRebound, Mathf.Max(0.04f, _resultDuration * 0.30f), _curve, Vector2.one * _resultReboundScale, 0f);
                return true;
            }

            if (_phase == Phase.ResultRebound)
            {
                BeginPhase(Phase.ResultSettle, Mathf.Max(0.04f, _resultDuration * 0.25f), _curve, Vector2.one, 0f);
                return true;
            }

            if (_phase == Phase.FreedGrow)
            {
                BeginPhase(Phase.FreedSettle, Mathf.Max(0.04f, _resultDuration * 0.45f), _curve, Vector2.one, 0f);
                return true;
            }

            _phase = Phase.Idle;
            _actionScale = Vector2.one;
            _currentDown = 0f;
            return false;
        }

        bool TickMergePreview(float dt)
        {
            var running = false;
            if (_mergePreviewPhase == MergePreviewPhase.Accept)
            {
                running = true;
                _mergeTargetElapsed += dt;
                var t = _mergeTargetDuration <= 0f ? 1f : Mathf.Clamp01(_mergeTargetElapsed / _mergeTargetDuration);
                var curved = EaseInOut(t);
                _mergeTargetMultiplier = Mathf.LerpUnclamped(_mergeTargetFrom, _mergeTargetTo, curved);
                _mergeBreathWave = 1f;
                if (t >= 1f)
                {
                    _mergeTargetFrom = _mergeTargetMultiplier;
                    _mergeTargetTo = _mergeBaseScale;
                    _mergeTargetElapsed = 0f;
                    _mergeTargetDuration = Mathf.Max(0.04f, _mergeTargetDuration * 0.55f);
                    _mergePreviewPhase = MergePreviewPhase.Hold;
                }
            }
            else if (_mergePreviewPhase == MergePreviewPhase.Hold)
            {
                running = true;
                if (_mergeTargetElapsed < _mergeTargetDuration)
                {
                    _mergeTargetElapsed += dt;
                    var settleT = _mergeTargetDuration <= 0f ? 1f : Mathf.Clamp01(_mergeTargetElapsed / _mergeTargetDuration);
                    _mergeTargetMultiplier = Mathf.LerpUnclamped(_mergeTargetFrom, _mergeTargetTo, EaseInOut(settleT));
                }
                else
                {
                    _mergeTargetMultiplier = _mergeBaseScale;
                    _mergeBreathTime += dt;
                    var period = Mathf.Max(0.45f, _mergeBreathDuration);
                    var wave = 0.5f + 0.5f * Mathf.Sin(_mergeBreathTime * (Mathf.PI * 2f) / period);
                    var eased = EaseInOut(wave);
                    _mergeBreathWave = 1f + Mathf.Lerp(-_mergeBreathAmount, _mergeBreathAmount, eased);
                }
            }
            else if (_mergePreviewPhase == MergePreviewPhase.Exit)
            {
                running = true;
                _mergeTargetElapsed += dt;
                var t = _mergeTargetDuration <= 0f ? 1f : Mathf.Clamp01(_mergeTargetElapsed / _mergeTargetDuration);
                _mergeTargetMultiplier = Mathf.LerpUnclamped(_mergeTargetFrom, 1f, EaseInOut(t));
                _mergeBreathWave = 1f;
                if (t >= 1f && _attractionOffset.sqrMagnitude < 0.01f)
                {
                    ClearMergePreviewImmediate();
                    return false;
                }
            }

            var smooth = Mathf.Max(0.01f, _mergeAttractionSmooth);
            _attractionOffset = Vector2.Lerp(_attractionOffset, _attractionTarget, 1f - Mathf.Exp(-dt / smooth));
            if (_attractionOffset.sqrMagnitude > 0.0001f || _attractionTarget.sqrMagnitude > 0.0001f)
            {
                running = true;
            }

            return running;
        }

        void ClearMergePreviewImmediate()
        {
            _mergePreviewPhase = MergePreviewPhase.Off;
            _mergeTargetMultiplier = 1f;
            _mergeBreathWave = 1f;
            _mergeBreathTime = 0f;
            _mergeTargetElapsed = 0f;
            _attractionOffset = Vector2.zero;
            _attractionTarget = Vector2.zero;
        }

        void ClearCobwebPreviewImmediate()
        {
            _cobwebActive = false;
            _cobwebTransitioning = false;
            _cobwebMultiplier = 1f;
            _cobwebFrom = 1f;
            _cobwebTo = 1f;
        }

        bool TickCobwebPreview(float dt)
        {
            if (!_cobwebTransitioning)
            {
                return _cobwebActive;
            }

            _cobwebElapsed += dt;
            var t = _cobwebDuration <= 0f ? 1f : Mathf.Clamp01(_cobwebElapsed / _cobwebDuration);
            _cobwebMultiplier = Mathf.LerpUnclamped(_cobwebFrom, _cobwebTo, EaseInOut(t));
            if (t < 1f)
            {
                return true;
            }

            _cobwebMultiplier = _cobwebTo;
            _cobwebTransitioning = false;
            return _cobwebActive;
        }

        void ApplyMergeConfig(BoardMergeAnimationConfig mergeConfig)
        {
            if (mergeConfig == null)
            {
                return;
            }

            _mergeBaseScale = mergeConfig.MergeTargetBaseScale;
            _mergeBreathAmount = mergeConfig.MergeTargetBreathingAmount;
            _mergeBreathDuration = mergeConfig.MergeTargetBreathingDuration;
            _mergeAcceptOvershoot = mergeConfig.MergeTargetAcceptOvershoot;
            _mergeAttractionMax = mergeConfig.MergeTargetAttractionOffset;
            _mergeAttractionSmooth = mergeConfig.MergeTargetAttractionSmoothing;
        }

        void TickAbsorb(float t, float curved)
        {
            if (t < 0.4f)
            {
                var u = t / 0.4f;
                var peakCurved = _curve != null ? _curve.Evaluate(u) : u;
                _actionScale = Vector2.LerpUnclamped(_fromScale, _absorbPeak, peakCurved);
            }
            else
            {
                var u = (t - 0.4f) / 0.6f;
                var shrinkCurved = _curve != null ? _curve.Evaluate(u) : u;
                _actionScale = Vector2.LerpUnclamped(_absorbPeak, _toScale, shrinkCurved);
            }

            _currentDown = Mathf.LerpUnclamped(_fromDown, _toDown, curved);
        }

        bool TickHover(float dt)
        {
            if (!_hoverTransitioning)
            {
                return false;
            }

            _hoverElapsed += dt;
            var t = _hoverDuration <= 0f ? 1f : Mathf.Clamp01(_hoverElapsed / _hoverDuration);
            var curved = _hoverCurve != null ? _hoverCurve.Evaluate(t) : t;
            _hoverMultiplier = Mathf.LerpUnclamped(_hoverFrom, _hoverTo, curved);
            if (t < 1f)
            {
                return true;
            }

            _hoverMultiplier = _hoverTo;
            _hoverTransitioning = false;
            return false;
        }

        void BeginPhase(Phase phase, float duration, AnimationCurve curve, Vector2 toScale, float toDown)
        {
            if (itemRect == null)
            {
                return;
            }

            _phase = phase;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, duration);
            _curve = curve;
            _fromScale = _actionScale;
            _toScale = toScale;
            _fromDown = _currentDown;
            _toDown = toDown;
            _actionRevision = _presentationRevision;
            enabled = true;
        }

        bool CanAnimate()
        {
            return config != null && itemRect != null && itemRect.gameObject.activeInHierarchy;
        }

        void CaptureIdleIfNeeded()
        {
            if (!_idleCaptured)
            {
                CaptureIdle();
            }
        }

        void CaptureIdle()
        {
            if (itemRect == null)
            {
                return;
            }

            _idleOffsetMin = itemRect.offsetMin;
            _idleOffsetMax = itemRect.offsetMax;
            _idleCaptured = true;
        }

        void RefreshEnabled()
        {
            enabled = _phase != Phase.Idle ||
                      _hoverTransitioning ||
                      _hovered ||
                      _mergePreviewPhase != MergePreviewPhase.Off ||
                      _cobwebActive ||
                      _cobwebTransitioning;
        }

        float CellHeight()
        {
            if (cellRect != null)
            {
                return Mathf.Max(1f, cellRect.rect.height);
            }

            return 170f;
        }

        float CellSize()
        {
            if (cellRect != null)
            {
                var size = cellRect.rect.size;
                return Mathf.Max(1f, Mathf.Min(size.x, size.y));
            }

            return 170f;
        }

        static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }

        void ApplyPose()
        {
            if (itemRect == null)
            {
                return;
            }

            var mergeMultiplier = _mergeTargetMultiplier * _mergeBreathWave;
            itemRect.localScale = new Vector3(
                _actionScale.x * _hoverMultiplier * mergeMultiplier * _cobwebMultiplier,
                _actionScale.y * _hoverMultiplier * mergeMultiplier * _cobwebMultiplier,
                1f);
            itemRect.offsetMin = new Vector2(
                _idleOffsetMin.x + _attractionOffset.x,
                _idleOffsetMin.y + _attractionOffset.y - _currentDown);
            itemRect.offsetMax = new Vector2(
                _idleOffsetMax.x + _attractionOffset.x,
                _idleOffsetMax.y + _attractionOffset.y - _currentDown);
        }
    }
}
