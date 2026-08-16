using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardMergePresenter : MonoBehaviour
    {
        const int InitialSequencePool = 4;
        const int InitialFxPool = 4;

        enum Phase
        {
            Idle,
            Collision,
            Absorb,
            Result
        }

        sealed class MergeSequence
        {
            public Phase Phase = Phase.Idle;
            public float Elapsed;
            public int SourceIndex = BoardController.NoSelectionIndex;
            public int TargetIndex = BoardController.NoSelectionIndex;
            public int ResultItemId = BoardCellState.EmptyItemId;
            public int SelectionRevision;
            public int LockToken;
            public bool Mutated;
            public bool TargetWasLocked;
            public bool VisualOwnershipReleased;
            public int PresentationRevision;
            public bool SourceUnlocked;
            public bool TargetUnlocked;
            public BoardCellView TargetView;
            public BoardItemAnimator TargetAnimator;
            public BoardMergeFlightView Flight;
            public MergeFxView Fx;
            public Action<bool, int> OnComplete;
        }

        [SerializeField] BoardController boardController;
        [SerializeField] BoardDragView dragView;
        [SerializeField] BoardMergeAnimationConfig config;

        readonly List<MergeSequence> _active = new List<MergeSequence>(8);
        readonly List<MergeSequence> _pool = new List<MergeSequence>(8);
        readonly List<BoardMergeFlightView> _flightPool = new List<BoardMergeFlightView>(8);
        readonly List<MergeFxView> _fxPool = new List<MergeFxView>(8);
        readonly List<MergeSequence> _scratch = new List<MergeSequence>(8);
        RectTransform _poolRoot;
        bool _abortingAll;

        public bool HasActiveSequences => _active.Count > 0;

        public event Action MergeImpact;
        public event Action NewItemAppeared;

        public void Configure(BoardController controller, BoardDragView view, BoardMergeAnimationConfig animationConfig)
        {
            boardController = controller;
            dragView = view;
            config = animationConfig;
            EnsurePools();
        }

        public void Play(
            int sourceIndex,
            int targetIndex,
            int resultItemId,
            int selectionRevision,
            Sprite sprite,
            Vector2 size,
            bool preserveAspect,
            Color color,
            Vector2 startPos,
            Vector2 startScale,
            Action<bool, int> onComplete = null)
        {
            if (boardController == null || boardController.BoardView == null || config == null)
            {
                onComplete?.Invoke(false, sourceIndex);
                return;
            }

            EnsurePools();
            var sequence = RentSequence();
            sequence.SourceIndex = sourceIndex;
            sequence.TargetIndex = targetIndex;
            sequence.ResultItemId = resultItemId;
            sequence.SelectionRevision = selectionRevision;
            sequence.OnComplete = onComplete;
            sequence.Mutated = false;
            sequence.TargetWasLocked = false;
            sequence.VisualOwnershipReleased = false;
            sequence.PresentationRevision = 0;
            sequence.SourceUnlocked = false;
            sequence.TargetUnlocked = false;
            sequence.Elapsed = 0f;
            sequence.Phase = Phase.Collision;
            sequence.TargetView = boardController.BoardView.GetCellView(targetIndex);
            sequence.TargetAnimator = sequence.TargetView != null ? sequence.TargetView.ItemAnimator : null;
            if (boardController.State != null && boardController.State.IsValidIndex(targetIndex))
            {
                var targetState = boardController.State.GetCell(targetIndex);
                sequence.TargetWasLocked = targetState != null && targetState.ItemLocked;
            }

            sequence.LockToken = boardController.InteractionLocks.Acquire(sourceIndex, targetIndex);
            sequence.Flight = RentFlight();
            sequence.Fx = null;

            var land = GetTargetCenter(sequence.TargetView);
            if (sequence.Flight != null)
            {
                sequence.Flight.BeginCollision(
                    sprite,
                    size,
                    preserveAspect,
                    color,
                    startPos,
                    startScale,
                    land,
                    config.CollisionDragScale,
                    config.CollisionDuration,
                    config.CollisionCurve);
            }

            if (sequence.TargetAnimator != null)
            {
                sequence.TargetAnimator.PlayMergeCollision(config);
            }

            _active.Add(sequence);
            enabled = true;
        }

        public void AbortAll()
        {
            if (_abortingAll)
            {
                return;
            }

            _abortingAll = true;
            _scratch.Clear();
            for (var i = 0; i < _active.Count; i++)
            {
                _scratch.Add(_active[i]);
            }

            for (var i = 0; i < _scratch.Count; i++)
            {
                AbortSequence(_scratch[i], invokeComplete: true);
            }

            _scratch.Clear();
            _abortingAll = false;
            enabled = false;
        }

        /// <summary>
        /// Hands visual ownership of a cell from merge presentation back to BoardCell / drag.
        /// Late Finish/Abort must not re-show ItemImage.
        /// </summary>
        public void ReleaseCellVisualOwnership(int cellIndex)
        {
            if (cellIndex == BoardController.NoSelectionIndex)
            {
                return;
            }

            for (var i = 0; i < _active.Count; i++)
            {
                var sequence = _active[i];
                if (sequence == null)
                {
                    continue;
                }

                if (sequence.TargetIndex != cellIndex && sequence.SourceIndex != cellIndex)
                {
                    continue;
                }

                sequence.VisualOwnershipReleased = true;
                if (sequence.TargetAnimator != null)
                {
                    sequence.PresentationRevision = sequence.TargetAnimator.PresentationRevision;
                }

                if (sequence.TargetWasLocked && sequence.TargetView != null &&
                    sequence.TargetIndex == cellIndex)
                {
                    sequence.TargetView.HideCobwebOverlayImmediate();
                }

                // Early pickup must receive a visible cell; Finish will no longer unhide.
                if (sequence.TargetIndex == cellIndex &&
                    sequence.TargetView != null &&
                    !sequence.TargetView.IsItemPresentationSuppressed)
                {
                    sequence.TargetView.SetHideItemForDrag(false);
                }

                if (sequence.SourceIndex == cellIndex &&
                    boardController != null &&
                    boardController.BoardView != null)
                {
                    var sourceView = boardController.BoardView.GetCellView(cellIndex);
                    if (sourceView != null && !sourceView.IsItemPresentationSuppressed)
                    {
                        sourceView.SetHideItemForDrag(false);
                    }
                }
            }
        }

        public bool IsPresentingCell(int cellIndex)
        {
            if (cellIndex == BoardController.NoSelectionIndex)
            {
                return false;
            }

            for (var i = 0; i < _active.Count; i++)
            {
                var sequence = _active[i];
                if (sequence != null && !sequence.VisualOwnershipReleased &&
                    (sequence.TargetIndex == cellIndex || sequence.SourceIndex == cellIndex))
                {
                    return true;
                }
            }

            return false;
        }

        void Update()
        {
            if (_active.Count == 0 || config == null)
            {
                enabled = false;
                return;
            }

            var dt = config.GetDeltaTime();
            _scratch.Clear();
            for (var i = 0; i < _active.Count; i++)
            {
                _scratch.Add(_active[i]);
            }

            for (var i = 0; i < _scratch.Count; i++)
            {
                TickSequence(_scratch[i], dt);
            }

            _scratch.Clear();
            if (_active.Count == 0)
            {
                enabled = false;
            }
        }

        void OnDisable()
        {
            if (_active.Count > 0 && !_abortingAll)
            {
                AbortAll();
            }
        }

        void TickSequence(MergeSequence sequence, float dt)
        {
            if (sequence.Phase == Phase.Idle)
            {
                return;
            }

            sequence.Elapsed += dt;
            if (sequence.Flight != null)
            {
                sequence.Flight.Tick(dt);
            }

            if (sequence.Phase == Phase.Collision)
            {
                if (sequence.Elapsed < config.CollisionDuration)
                {
                    return;
                }

                // Commit + unlock at impact so the result is interactable during spawn/FX.
                // Do not keep gameplay locks through Absorb.
                CommitAndBurst(sequence);
                return;
            }

            if (sequence.Phase == Phase.Absorb)
            {
                // Legacy phase: if any sequence is still here, finish visuals then commit.
                if (sequence.Elapsed < config.AbsorbDuration)
                {
                    return;
                }

                CommitAndBurst(sequence);
                return;
            }

            if (sequence.Elapsed >= config.ResultDuration)
            {
                Finish(sequence);
            }
        }

        void BeginAbsorb(MergeSequence sequence)
        {
            sequence.Elapsed = 0f;
            sequence.Phase = Phase.Absorb;
            if (sequence.TargetWasLocked && sequence.TargetView != null)
            {
                sequence.TargetView.PlayCobwebBreak(boardController != null
                    ? boardController.CobwebAnimationConfig
                    : null);
            }

            if (sequence.Flight != null)
            {
                sequence.Flight.BeginAbsorb(config.AbsorbFinalScale, config.AbsorbDuration, config.AbsorbCurve);
            }

            if (sequence.TargetAnimator != null)
            {
                sequence.TargetAnimator.PlayMergeAbsorb(config);
            }
        }

        void CommitAndBurst(MergeSequence sequence)
        {
            if (sequence.Mutated)
            {
                return;
            }

            if (sequence.TargetWasLocked && sequence.TargetView != null)
            {
                sequence.TargetView.PlayCobwebBreak(boardController != null
                    ? boardController.CobwebAnimationConfig
                    : null);
            }

            if (sequence.Flight != null)
            {
                sequence.Flight.HideImmediate();
                ReturnFlight(sequence.Flight);
                sequence.Flight = null;
            }

            if (sequence.TargetView != null)
            {
                sequence.TargetView.SetHideItemForDrag(true);
            }

            var merge = boardController != null
                ? boardController.TryMerge(sequence.SourceIndex, sequence.TargetIndex)
                : default;
            sequence.Mutated = merge.Success;
            if (!sequence.Mutated)
            {
                if (sequence.TargetView != null)
                {
                    sequence.TargetView.SetHideItemForDrag(false);
                }

                AbortSequence(sequence, invokeComplete: true);
                return;
            }

            if (merge.ResultingItemId != BoardCellState.EmptyItemId)
            {
                sequence.ResultItemId = merge.ResultingItemId;
            }

            // Gameplay ownership ends here — spawn/FX are presentation-only.
            UnlockSource(sequence);
            UnlockTarget(sequence);

            MergeImpact?.Invoke();
            boardController?.NotifyMergeImpact();

            sequence.Fx = RentFx();
            if (sequence.Fx != null && sequence.TargetView != null)
            {
                var fxParent = sequence.TargetView.FxRoot != null
                    ? sequence.TargetView.FxRoot
                    : sequence.TargetView.transform as RectTransform;
                sequence.Fx.Play(fxParent, config);
            }

            Sprite resultSprite = null;
            if (boardController != null && boardController.ItemDatabase != null &&
                boardController.ItemDatabase.TryGetById(sequence.ResultItemId, out var resultData) && resultData != null)
            {
                resultSprite = resultData.Icon;
            }

            if (sequence.TargetView != null && sequence.TargetView.ItemImage != null)
            {
                sequence.TargetView.ItemImage.sprite = resultSprite;
                sequence.TargetView.ItemImage.enabled = resultSprite != null;
            }

            if (sequence.TargetView != null)
            {
                if (!sequence.TargetView.IsItemPresentationSuppressed)
                {
                    sequence.TargetView.RevealItemAfterMerge();
                }
            }

            if (!sequence.VisualOwnershipReleased && sequence.TargetView != null &&
                !sequence.TargetView.IsItemPresentationSuppressed)
            {
                if (sequence.TargetAnimator != null)
                {
                    sequence.TargetAnimator.PlayResultSpawn(config);
                    sequence.PresentationRevision = sequence.TargetAnimator.PresentationRevision;
                }
            }
            else if (sequence.TargetView != null)
            {
                sequence.TargetView.HideCobwebOverlayImmediate();
            }

            if (boardController != null &&
                boardController.SelectionRevision == sequence.SelectionRevision &&
                boardController.ItemDatabase != null &&
                boardController.ItemDatabase.TryGetById(sequence.ResultItemId, out var selectData) &&
                selectData != null)
            {
                boardController.SelectCell(sequence.TargetIndex);
            }

            NewItemAppeared?.Invoke();
            boardController?.NotifyNewItemAppeared();
            sequence.Elapsed = 0f;
            sequence.Phase = Phase.Result;
        }

        void Finish(MergeSequence sequence)
        {
            var complete = sequence.OnComplete;
            var target = sequence.TargetIndex;
            var mutated = sequence.Mutated;
            var canTouchTargetVisual = !sequence.VisualOwnershipReleased
                                       && sequence.TargetView != null
                                       && !sequence.TargetView.IsItemPresentationSuppressed
                                       && (sequence.TargetAnimator == null
                                           || sequence.TargetAnimator.IsPresentationRevisionCurrent(sequence.PresentationRevision)
                                           || sequence.PresentationRevision == 0);

            if (sequence.TargetView != null && !sequence.TargetView.IsItemPresentationSuppressed)
            {
                sequence.TargetView.SetHideItemForDrag(false);
                if (canTouchTargetVisual &&
                    boardController != null &&
                    boardController.BoardView != null)
                {
                    boardController.BoardView.RefreshCell(target);
                }
            }

            ReleaseSequence(sequence);
            complete?.Invoke(mutated, target);
        }

        void AbortSequence(MergeSequence sequence, bool invokeComplete)
        {
            if (sequence.Phase == Phase.Idle)
            {
                return;
            }

            var complete = sequence.OnComplete;
            var mutated = sequence.Mutated;
            var source = sequence.SourceIndex;
            var target = sequence.TargetIndex;
            var canTouchTargetVisual = !sequence.VisualOwnershipReleased
                                       && (sequence.TargetView == null
                                           || !sequence.TargetView.IsItemPresentationSuppressed);

            if (sequence.Fx != null)
            {
                sequence.Fx.Hide();
                ReturnFx(sequence.Fx);
                sequence.Fx = null;
            }

            if (sequence.Flight != null)
            {
                sequence.Flight.HideImmediate();
                ReturnFlight(sequence.Flight);
                sequence.Flight = null;
            }

            if (canTouchTargetVisual && sequence.TargetAnimator != null)
            {
                sequence.TargetAnimator.ClearMergeTargetImmediate();
                sequence.TargetAnimator.CancelTransientAnimationAndAdoptCurrentVisualState();
            }

            if (boardController != null && boardController.BoardView != null)
            {
                var sourceView = boardController.BoardView.GetCellView(source);
                if (sourceView != null && !sourceView.IsItemPresentationSuppressed)
                {
                    sourceView.SetHideItemForDrag(false);
                }

                if (canTouchTargetVisual && sequence.TargetView != null)
                {
                    sequence.TargetView.SetHideItemForDrag(false);
                }

                if (sourceView == null || !sourceView.IsItemPresentationSuppressed)
                {
                    boardController.BoardView.RefreshCell(source);
                }

                if (canTouchTargetVisual)
                {
                    boardController.BoardView.RefreshCell(target);
                }
            }

            ReleaseLocks(sequence);
            ReleaseSequence(sequence);

            if (invokeComplete)
            {
                complete?.Invoke(mutated, mutated ? target : source);
            }
        }

        void UnlockSource(MergeSequence sequence)
        {
            if (sequence.SourceUnlocked || boardController == null)
            {
                return;
            }

            boardController.InteractionLocks.ReleaseCell(sequence.LockToken, sequence.SourceIndex);
            sequence.SourceUnlocked = true;
        }

        void UnlockTarget(MergeSequence sequence)
        {
            if (sequence.TargetUnlocked || boardController == null)
            {
                return;
            }

            boardController.InteractionLocks.ReleaseCell(sequence.LockToken, sequence.TargetIndex);
            sequence.TargetUnlocked = true;
        }

        void ReleaseLocks(MergeSequence sequence)
        {
            if (boardController == null || sequence.LockToken == 0)
            {
                return;
            }

            boardController.InteractionLocks.Release(sequence.LockToken);
            sequence.LockToken = 0;
            sequence.SourceUnlocked = true;
            sequence.TargetUnlocked = true;
        }

        void ReleaseSequence(MergeSequence sequence)
        {
            _active.Remove(sequence);
            ReleaseLocks(sequence);
            sequence.Phase = Phase.Idle;
            sequence.OnComplete = null;
            sequence.TargetView = null;
            sequence.TargetAnimator = null;
            sequence.Flight = null;
            // FX may outlive the sequence; leave it playing and reclaim via pool when inactive.
            sequence.Fx = null;
            sequence.Elapsed = 0f;
            sequence.LockToken = 0;
            sequence.VisualOwnershipReleased = false;
            sequence.PresentationRevision = 0;
            sequence.TargetWasLocked = false;
            _pool.Add(sequence);
        }

        MergeSequence RentSequence()
        {
            if (_pool.Count > 0)
            {
                var last = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                return last;
            }

            return new MergeSequence();
        }

        BoardMergeFlightView RentFlight()
        {
            for (var i = 0; i < _flightPool.Count; i++)
            {
                var flight = _flightPool[i];
                if (flight != null && !flight.IsActive)
                {
                    return flight;
                }
            }

            return CreateFlight();
        }

        void ReturnFlight(BoardMergeFlightView flight)
        {
            if (flight == null)
            {
                return;
            }

            flight.HideImmediate();
        }

        MergeFxView RentFx()
        {
            for (var i = 0; i < _fxPool.Count; i++)
            {
                var fx = _fxPool[i];
                if (fx != null && !fx.IsPlaying)
                {
                    return fx;
                }
            }

            return CreateFx();
        }

        void ReturnFx(MergeFxView fx)
        {
            if (fx == null)
            {
                return;
            }

            fx.Hide();
        }

        Vector2 GetTargetCenter(BoardCellView targetView)
        {
            if (dragView == null || targetView == null)
            {
                return Vector2.zero;
            }

            var rect = targetView.ItemImage != null
                ? targetView.ItemImage.rectTransform
                : targetView.transform as RectTransform;
            return dragView.WorldToLayer(rect.TransformPoint(rect.rect.center));
        }

        void EnsurePools()
        {
            if (_poolRoot == null)
            {
                var parent = dragView != null ? dragView.transform : transform;
                var existing = parent.Find("MergePresentationPool") as RectTransform;
                if (existing != null)
                {
                    _poolRoot = existing;
                }
                else
                {
                    var go = new GameObject("MergePresentationPool", typeof(RectTransform));
                    go.layer = parent.gameObject.layer;
                    _poolRoot = go.GetComponent<RectTransform>();
                    _poolRoot.SetParent(parent, false);
                    _poolRoot.anchorMin = Vector2.zero;
                    _poolRoot.anchorMax = Vector2.one;
                    _poolRoot.offsetMin = Vector2.zero;
                    _poolRoot.offsetMax = Vector2.zero;
                }
            }

            while (_flightPool.Count < InitialSequencePool)
            {
                CreateFlight();
            }

            while (_fxPool.Count < InitialFxPool)
            {
                CreateFx();
            }

            while (_pool.Count < InitialSequencePool)
            {
                _pool.Add(new MergeSequence());
            }
        }

        BoardMergeFlightView CreateFlight()
        {
            var go = new GameObject($"MergeFlight_{_flightPool.Count}", typeof(RectTransform));
            go.layer = _poolRoot.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_poolRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var flight = go.AddComponent<BoardMergeFlightView>();
            flight.Ensure(_poolRoot);
            flight.HideImmediate();
            _flightPool.Add(flight);
            return flight;
        }

        MergeFxView CreateFx()
        {
            var go = new GameObject($"MergeFx_{_fxPool.Count}", typeof(RectTransform));
            go.layer = _poolRoot.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_poolRoot, false);
            var fx = go.AddComponent<MergeFxView>();
            fx.EnsureChildren();
            fx.Hide();
            _fxPool.Add(fx);
            return fx;
        }
    }
}
