using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardCobwebPresenter : MonoBehaviour
    {
        const int InitialPool = 3;

        enum Phase
        {
            Idle,
            Approach,
            Break,
            Landing
        }

        sealed class UnlockSequence
        {
            public Phase Phase = Phase.Idle;
            public float Elapsed;
            public int SourceIndex = BoardController.NoSelectionIndex;
            public int TargetIndex = BoardController.NoSelectionIndex;
            public int DestinationIndex = BoardController.NoSelectionIndex;
            public int SelectionRevision;
            public int LockToken;
            public bool Mutated;
            public bool VisualOwnershipReleased;
            public BoardCellView TargetView;
            public BoardItemAnimator TargetAnimator;
            public BoardMergeFlightView Flight;
            public CobwebBreakFxView Fx;
        }

        [SerializeField] BoardController boardController;
        [SerializeField] BoardDragView dragView;
        [SerializeField] BoardCobwebAnimationConfig config;
        [SerializeField] BoardDragAnimationConfig dragConfig;

        readonly List<UnlockSequence> _active = new List<UnlockSequence>(4);
        readonly List<UnlockSequence> _pool = new List<UnlockSequence>(4);
        readonly List<BoardMergeFlightView> _flightPool = new List<BoardMergeFlightView>(4);
        readonly List<CobwebBreakFxView> _fxPool = new List<CobwebBreakFxView>(4);
        readonly List<UnlockSequence> _scratch = new List<UnlockSequence>(4);
        RectTransform _poolRoot;
        bool _abortingAll;

        public bool HasActiveSequences => _active.Count > 0;

        public void Configure(
            BoardController controller,
            BoardDragView view,
            BoardCobwebAnimationConfig cobwebConfig,
            BoardDragAnimationConfig animationConfig)
        {
            boardController = controller;
            dragView = view;
            config = cobwebConfig;
            dragConfig = animationConfig;
            EnsurePools();
        }

        public void Play(
            int sourceIndex,
            int targetIndex,
            int destinationIndex,
            int selectionRevision,
            Sprite sprite,
            Vector2 size,
            bool preserveAspect,
            Color color,
            Vector2 startPos,
            Vector2 startScale)
        {
            if (boardController == null || boardController.BoardView == null || config == null)
            {
                Debug.LogError(
                    $"[Cobweb] Presenter.Play aborted controller={boardController != null} " +
                    $"boardView={(boardController != null && boardController.BoardView != null)} config={config != null}");
                return;
            }

            Debug.Log(
                $"[Cobweb] Presenter.Play source={sourceIndex} target={targetIndex} dest={destinationIndex} " +
                $"sprite={sprite != null} start={startPos}");

            EnsurePools();
            var sequence = RentSequence();
            sequence.SourceIndex = sourceIndex;
            sequence.TargetIndex = targetIndex;
            sequence.DestinationIndex = destinationIndex;
            sequence.SelectionRevision = selectionRevision;
            sequence.Mutated = false;
            sequence.VisualOwnershipReleased = false;
            sequence.Elapsed = 0f;
            sequence.Phase = Phase.Approach;
            sequence.TargetView = boardController.BoardView.GetCellView(targetIndex);
            sequence.TargetAnimator = sequence.TargetView != null ? sequence.TargetView.ItemAnimator : null;
            sequence.LockToken = boardController.InteractionLocks.Acquire(sourceIndex, targetIndex, destinationIndex);
            sequence.Flight = RentFlight();
            sequence.Fx = null;

            var land = GetCellCenter(targetIndex);
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
                    config.ApproachDragScale,
                    config.ImpactDuration,
                    config.ImpactCurve);
            }

            if (sequence.TargetView != null)
            {
                sequence.TargetView.SetCobwebUnlockHover(false, config);
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
                AbortSequence(_scratch[i]);
            }

            _scratch.Clear();
            _abortingAll = false;
            enabled = false;
        }

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

                if (sequence.SourceIndex == cellIndex
                    || sequence.TargetIndex == cellIndex
                    || sequence.DestinationIndex == cellIndex)
                {
                    sequence.VisualOwnershipReleased = true;
                }
            }
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

        void TickSequence(UnlockSequence sequence, float dt)
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

            if (sequence.Phase == Phase.Approach)
            {
                if (sequence.Elapsed < config.ImpactDuration)
                {
                    return;
                }

                BeginBreak(sequence);
                return;
            }

            if (sequence.Phase == Phase.Break)
            {
                if (sequence.Elapsed < config.WebBreakDuration * 0.45f)
                {
                    return;
                }

                if (!sequence.Mutated)
                {
                    Commit(sequence);
                }

                if (sequence.Elapsed < config.WebBreakDuration)
                {
                    return;
                }

                BeginLanding(sequence);
                return;
            }

            if (sequence.Phase == Phase.Landing)
            {
                if (sequence.Flight != null && sequence.Flight.IsLanding)
                {
                    return;
                }

                Finish(sequence);
            }
        }

        void BeginBreak(UnlockSequence sequence)
        {
            sequence.Elapsed = 0f;
            sequence.Phase = Phase.Break;
            if (sequence.TargetView != null)
            {
                sequence.TargetView.PlayCobwebBreak(config);
            }

            sequence.Fx = RentFx();
            if (sequence.Fx != null && sequence.TargetView != null)
            {
                var parent = sequence.TargetView.FxRoot != null
                    ? sequence.TargetView.FxRoot
                    : sequence.TargetView.transform as RectTransform;
                sequence.Fx.Play(parent, config);
            }
        }

        void Commit(UnlockSequence sequence)
        {
            var result = boardController.TryUnlockLockedItem(
                sequence.SourceIndex,
                sequence.TargetIndex,
                sequence.DestinationIndex);
            sequence.Mutated = result.Success;
            Debug.Log(
                $"[Cobweb] Commit success={result.Success} source={sequence.SourceIndex} " +
                $"target={sequence.TargetIndex} dest={sequence.DestinationIndex} itemId={result.ItemId}");
            if (!sequence.Mutated)
            {
                AbortSequence(sequence);
                return;
            }

            // Do not ResetCobwebVisualImmediate here — that restored full-alpha overlay after
            // RefreshCell skipped the unlocked branch while _cobwebBreaking was still true.
            if (boardController.BoardView != null)
            {
                boardController.BoardView.RefreshCell(sequence.TargetIndex);
            }

            if (sequence.TargetAnimator != null)
            {
                sequence.TargetAnimator.PlayFreedBounce(config);
            }

            // Critical phase done — free cells for parallel play while landing continues.
            ReleaseLocks(sequence);

            if (boardController.SelectionRevision == sequence.SelectionRevision)
            {
                boardController.SelectCell(sequence.TargetIndex);
            }
        }

        void BeginLanding(UnlockSequence sequence)
        {
            sequence.Elapsed = 0f;
            sequence.Phase = Phase.Landing;
            var landPos = GetCellCenter(sequence.DestinationIndex);
            var flightHeight = dragConfig != null ? dragConfig.DragFlightHeight : 16f;
            if (sequence.Flight != null)
            {
                sequence.Flight.BeginLanding(landPos, dragConfig, flightHeight);
            }
            else
            {
                Finish(sequence);
            }
        }

        void Finish(UnlockSequence sequence)
        {
            if (sequence.Flight != null)
            {
                sequence.Flight.HideImmediate();
                ReturnFlight(sequence.Flight);
                sequence.Flight = null;
            }

            if (!sequence.VisualOwnershipReleased
                && boardController != null
                && boardController.BoardView != null)
            {
                var destView = boardController.BoardView.GetCellView(sequence.DestinationIndex);
                if (destView != null && !destView.IsItemPresentationSuppressed)
                {
                    destView.SetHideItemForDrag(false);
                }

                if (destView == null || !destView.IsItemPresentationSuppressed)
                {
                    boardController.BoardView.RefreshCell(sequence.DestinationIndex);
                }

                boardController.BoardView.RefreshCell(sequence.SourceIndex);
                boardController.BoardView.RefreshCell(sequence.TargetIndex);
            }

            ReleaseSequence(sequence);
        }

        void AbortSequence(UnlockSequence sequence)
        {
            if (sequence.Phase == Phase.Idle)
            {
                return;
            }

            if (sequence.Fx != null)
            {
                sequence.Fx.Hide();
                sequence.Fx = null;
            }

            if (sequence.Flight != null)
            {
                sequence.Flight.HideImmediate();
                ReturnFlight(sequence.Flight);
                sequence.Flight = null;
            }

            var canTouchVisuals = !sequence.VisualOwnershipReleased;

            if (canTouchVisuals && sequence.TargetView != null)
            {
                sequence.TargetView.ResetCobwebVisualImmediate();
                if (!sequence.TargetView.IsItemPresentationSuppressed)
                {
                    sequence.TargetView.SetHideItemForDrag(false);
                }
            }

            if (canTouchVisuals && sequence.TargetAnimator != null)
            {
                sequence.TargetAnimator.ClearMergeTargetImmediate();
            }

            if (canTouchVisuals && boardController != null && boardController.BoardView != null)
            {
                var sourceView = boardController.BoardView.GetCellView(sequence.SourceIndex);
                if (sourceView != null && !sourceView.IsItemPresentationSuppressed)
                {
                    sourceView.SetHideItemForDrag(false);
                }

                var destView = boardController.BoardView.GetCellView(sequence.DestinationIndex);
                if (destView != null && !destView.IsItemPresentationSuppressed)
                {
                    destView.SetHideItemForDrag(false);
                }

                boardController.BoardView.RefreshCell(sequence.SourceIndex);
                boardController.BoardView.RefreshCell(sequence.TargetIndex);
                boardController.BoardView.RefreshCell(sequence.DestinationIndex);
            }

            ReleaseLocks(sequence);
            ReleaseSequence(sequence);
        }

        void ReleaseLocks(UnlockSequence sequence)
        {
            if (boardController == null || sequence.LockToken == 0)
            {
                return;
            }

            boardController.InteractionLocks.Release(sequence.LockToken);
            sequence.LockToken = 0;
        }

        void ReleaseSequence(UnlockSequence sequence)
        {
            _active.Remove(sequence);
            ReleaseLocks(sequence);
            sequence.Phase = Phase.Idle;
            sequence.TargetView = null;
            sequence.TargetAnimator = null;
            sequence.Flight = null;
            sequence.Fx = null;
            sequence.Elapsed = 0f;
            sequence.LockToken = 0;
            sequence.VisualOwnershipReleased = false;
            _pool.Add(sequence);
        }

        UnlockSequence RentSequence()
        {
            if (_pool.Count > 0)
            {
                var last = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                return last;
            }

            return new UnlockSequence();
        }

        BoardMergeFlightView RentFlight()
        {
            for (var i = 0; i < _flightPool.Count; i++)
            {
                if (_flightPool[i] != null && !_flightPool[i].IsActive)
                {
                    return _flightPool[i];
                }
            }

            return CreateFlight();
        }

        void ReturnFlight(BoardMergeFlightView flight)
        {
            flight?.HideImmediate();
        }

        CobwebBreakFxView RentFx()
        {
            for (var i = 0; i < _fxPool.Count; i++)
            {
                if (_fxPool[i] != null && !_fxPool[i].IsPlaying)
                {
                    return _fxPool[i];
                }
            }

            return CreateFx();
        }

        Vector2 GetCellCenter(int index)
        {
            if (dragView == null || boardController == null || boardController.BoardView == null)
            {
                return Vector2.zero;
            }

            var cell = boardController.BoardView.GetCellView(index);
            if (cell == null)
            {
                return Vector2.zero;
            }

            var rect = cell.ItemImage != null
                ? cell.ItemImage.rectTransform
                : cell.transform as RectTransform;
            return dragView.WorldToLayer(rect.TransformPoint(rect.rect.center));
        }

        void EnsurePools()
        {
            if (_poolRoot == null)
            {
                var parent = dragView != null ? dragView.transform : transform;
                var existing = parent.Find("CobwebPresentationPool") as RectTransform;
                if (existing != null)
                {
                    _poolRoot = existing;
                }
                else
                {
                    var go = new GameObject("CobwebPresentationPool", typeof(RectTransform));
                    go.layer = parent.gameObject.layer;
                    _poolRoot = go.GetComponent<RectTransform>();
                    _poolRoot.SetParent(parent, false);
                    _poolRoot.anchorMin = Vector2.zero;
                    _poolRoot.anchorMax = Vector2.one;
                    _poolRoot.offsetMin = Vector2.zero;
                    _poolRoot.offsetMax = Vector2.zero;
                }
            }

            while (_flightPool.Count < InitialPool)
            {
                CreateFlight();
            }

            while (_fxPool.Count < InitialPool)
            {
                CreateFx();
            }

            while (_pool.Count < InitialPool)
            {
                _pool.Add(new UnlockSequence());
            }
        }

        BoardMergeFlightView CreateFlight()
        {
            var go = new GameObject($"CobwebFlight_{_flightPool.Count}", typeof(RectTransform));
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

        CobwebBreakFxView CreateFx()
        {
            var go = new GameObject($"CobwebBreakFx_{_fxPool.Count}", typeof(RectTransform));
            go.layer = _poolRoot.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_poolRoot, false);
            var fx = go.AddComponent<CobwebBreakFxView>();
            fx.EnsureChildren();
            fx.Hide();
            _fxPool.Add(fx);
            return fx;
        }
    }
}
