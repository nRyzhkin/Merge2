using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    /// <summary>
    /// Occupied-cell displacement: B arcs to nearest empty while A lands in the freed target.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardDisplacePresenter : MonoBehaviour
    {
        const int InitialPool = 4;

        enum Phase
        {
            Idle,
            Flight
        }

        sealed class DisplaceSequence
        {
            public Phase Phase = Phase.Idle;
            public float Elapsed;
            public int SourceIndex;
            public int TargetIndex;
            public int DestinationIndex;
            public int LockToken;
            public BoardCellView TargetView;
            public BoardCellView DestinationView;
            public BoardItemAnimator TargetAnimator;
            public BoardMergeFlightView Flight;
            public bool Mutated;
            public bool VisualOwnershipReleased;
            public bool ReadyForADropSignaled;
            public Action OnReadyForADrop;
            public Action OnFailed;
            public Sprite DisplacedSprite;
            public Vector2 DisplacedSize;
            public bool PreserveAspect;
            public Color Color;
            public Vector2 StartScale;
        }

        [SerializeField] BoardController boardController;
        [SerializeField] BoardDragView dragView;
        [SerializeField] BoardDragAnimationConfig dragConfig;

        readonly List<DisplaceSequence> _active = new List<DisplaceSequence>(4);
        readonly List<DisplaceSequence> _pool = new List<DisplaceSequence>(4);
        readonly List<BoardMergeFlightView> _flightPool = new List<BoardMergeFlightView>(4);
        readonly List<DisplaceSequence> _scratch = new List<DisplaceSequence>(4);
        RectTransform _poolRoot;
        bool _abortingAll;

        public bool HasActiveSequences => _active.Count > 0;

        public void Configure(BoardController controller, BoardDragView view, BoardDragAnimationConfig animationConfig)
        {
            boardController = controller;
            dragView = view;
            dragConfig = animationConfig;
            EnsurePools();
        }

        public bool Play(
            int sourceIndex,
            int targetIndex,
            int destinationIndex,
            Sprite displacedSprite,
            Vector2 displacedSize,
            bool preserveAspect,
            Color color,
            Action onReadyForADrop,
            Action onFailed = null)
        {
            if (boardController == null || boardController.BoardView == null || dragView == null || dragConfig == null)
            {
                return false;
            }

            if (!boardController.CanDisplaceOccupiedTarget(targetIndex))
            {
                return false;
            }

            EnsurePools();
            var sequence = RentSequence();
            sequence.SourceIndex = sourceIndex;
            sequence.TargetIndex = targetIndex;
            sequence.DestinationIndex = destinationIndex;
            sequence.Elapsed = 0f;
            sequence.Phase = Phase.Flight;
            sequence.Mutated = false;
            sequence.VisualOwnershipReleased = false;
            sequence.ReadyForADropSignaled = false;
            sequence.OnReadyForADrop = onReadyForADrop;
            sequence.OnFailed = onFailed;
            sequence.TargetView = boardController.BoardView.GetCellView(targetIndex);
            sequence.DestinationView = boardController.BoardView.GetCellView(destinationIndex);
            sequence.TargetAnimator = sequence.TargetView != null ? sequence.TargetView.ItemAnimator : null;
            sequence.Flight = null;
            sequence.LockToken = boardController.InteractionLocks.Acquire(sourceIndex, targetIndex, destinationIndex);

            sequence.DisplacedSprite = displacedSprite;
            sequence.DisplacedSize = displacedSize;
            sequence.PreserveAspect = preserveAspect;
            sequence.Color = color;
            sequence.StartScale = Vector2.one;
            if (sequence.TargetView != null && sequence.TargetView.ItemImage != null)
            {
                var scale = sequence.TargetView.ItemImage.rectTransform.localScale;
                sequence.StartScale = new Vector2(scale.x, scale.y);
            }

            if (!BeginFlightAndMutate(sequence))
            {
                ReleaseSequence(sequence);
                return false;
            }

            _active.Add(sequence);
            enabled = true;
            return true;
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

                if (sequence.TargetIndex != cellIndex && sequence.DestinationIndex != cellIndex)
                {
                    continue;
                }

                sequence.VisualOwnershipReleased = true;
                if (sequence.DestinationIndex == cellIndex && sequence.Flight != null)
                {
                    sequence.Flight.HideImmediate();
                    ReturnFlight(sequence.Flight);
                    sequence.Flight = null;
                }

                RevealCell(cellIndex);
            }
        }

        void Update()
        {
            if (_active.Count == 0 || dragConfig == null)
            {
                enabled = false;
                return;
            }

            var dt = dragConfig.GetDeltaTime();
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

        void TickSequence(DisplaceSequence sequence, float dt)
        {
            if (sequence.Phase == Phase.Idle)
            {
                return;
            }

            sequence.Elapsed += dt;

            if (sequence.Phase == Phase.Flight)
            {
                if (!sequence.ReadyForADropSignaled && sequence.Elapsed >= dragConfig.DisplaceLeadTime)
                {
                    sequence.ReadyForADropSignaled = true;
                    sequence.OnReadyForADrop?.Invoke();
                    sequence.OnReadyForADrop = null;
                }

                if (sequence.VisualOwnershipReleased)
                {
                    Finish(sequence);
                    return;
                }

                var flying = sequence.Flight != null && sequence.Flight.Tick(dt);
                if (flying)
                {
                    return;
                }

                RevealCell(sequence.DestinationIndex);
                Finish(sequence);
            }
        }

        bool BeginFlightAndMutate(DisplaceSequence sequence)
        {
            // Suppress both cells BEFORE state refresh so ItemImage never flashes under the flight.
            SuppressCell(sequence.TargetIndex);
            SuppressCell(sequence.DestinationIndex);

            sequence.Flight = RentFlight();
            if (sequence.Flight == null)
            {
                return false;
            }

            var startPos = GetCellCenter(sequence.TargetIndex);
            var endPos = GetCellCenter(sequence.DestinationIndex);
            var flightHeight = dragConfig.DragFlightHeight * dragConfig.DisplaceFlightHeightScale;
            var duration = Mathf.Max(0.08f, dragConfig.DisplaceFlightDuration);
            sequence.Flight.BeginSpawn(
                sequence.DisplacedSprite,
                sequence.DisplacedSize,
                sequence.PreserveAspect,
                sequence.Color,
                startPos,
                endPos,
                sequence.StartScale,
                Vector2.one * dragConfig.DisplacePeakScale,
                flightHeight,
                duration,
                dragConfig.LandingMoveCurve,
                dragConfig);

            var result = boardController.TryDisplaceItem(
                sequence.SourceIndex,
                sequence.TargetIndex,
                sequence.DestinationIndex);
            if (!result.Success)
            {
                sequence.Flight.HideImmediate();
                ReturnFlight(sequence.Flight);
                sequence.Flight = null;
                return false;
            }

            sequence.Mutated = true;
            // Re-assert suppress after RefreshCell inside TryDisplaceItem.
            SuppressCell(sequence.TargetIndex);
            SuppressCell(sequence.DestinationIndex);

            // Source is empty only when B did not land there — never unhide destination early.
            if (sequence.SourceIndex != sequence.DestinationIndex)
            {
                RevealCell(sequence.SourceIndex);
            }

            boardController.InteractionLocks.Release(sequence.LockToken);
            sequence.LockToken = 0;

            sequence.Elapsed = 0f;
            sequence.Phase = Phase.Flight;
            return true;
        }

        public bool IsOwningCellVisual(int cellIndex)
        {
            if (cellIndex == BoardController.NoSelectionIndex)
            {
                return false;
            }

            for (var i = 0; i < _active.Count; i++)
            {
                var sequence = _active[i];
                if (sequence == null || sequence.Phase == Phase.Idle || sequence.VisualOwnershipReleased)
                {
                    continue;
                }

                if (sequence.TargetIndex == cellIndex)
                {
                    // Target stays suppressed until A lands (drag FinishDrop).
                    return true;
                }

                if (sequence.DestinationIndex == cellIndex && sequence.Flight != null && sequence.Flight.IsActive)
                {
                    return true;
                }
            }

            return false;
        }

        void Finish(DisplaceSequence sequence)
        {
            if (!sequence.ReadyForADropSignaled)
            {
                sequence.ReadyForADropSignaled = true;
                sequence.OnReadyForADrop?.Invoke();
                sequence.OnReadyForADrop = null;
            }

            if (sequence.Flight != null)
            {
                sequence.Flight.HideImmediate();
                ReturnFlight(sequence.Flight);
                sequence.Flight = null;
            }

            if (!sequence.VisualOwnershipReleased)
            {
                RevealCell(sequence.DestinationIndex);
            }

            if (sequence.LockToken != 0 && boardController != null)
            {
                boardController.InteractionLocks.Release(sequence.LockToken);
                sequence.LockToken = 0;
            }

            ReleaseSequence(sequence);
        }

        void AbortSequence(DisplaceSequence sequence)
        {
            if (sequence.Phase == Phase.Idle)
            {
                return;
            }

            var failedBeforeMutate = !sequence.Mutated;
            var onFailed = sequence.OnFailed;

            if (sequence.Flight != null)
            {
                sequence.Flight.HideImmediate();
                ReturnFlight(sequence.Flight);
                sequence.Flight = null;
            }

            sequence.OnReadyForADrop = null;
            sequence.OnFailed = null;
            RevealCell(sequence.TargetIndex);
            RevealCell(sequence.DestinationIndex);
            RevealCell(sequence.SourceIndex);

            if (sequence.LockToken != 0 && boardController != null)
            {
                boardController.InteractionLocks.Release(sequence.LockToken);
                sequence.LockToken = 0;
            }

            ReleaseSequence(sequence);

            if (failedBeforeMutate)
            {
                onFailed?.Invoke();
            }
        }

        void SuppressCell(int index)
        {
            if (boardController == null || boardController.BoardView == null)
            {
                return;
            }

            var view = boardController.BoardView.GetCellView(index);
            if (view == null)
            {
                return;
            }

            view.SetItemPresentationSuppressed(true);
            view.SetHideItemForDrag(true);
            boardController.BoardView.RefreshCell(index);
        }

        void RevealCell(int index)
        {
            if (boardController == null || boardController.BoardView == null)
            {
                return;
            }

            var view = boardController.BoardView.GetCellView(index);
            if (view == null)
            {
                return;
            }

            view.SetItemPresentationSuppressed(false);
            view.SetHideItemForDrag(false);
            boardController.BoardView.RefreshCell(index);
            view.ItemAnimator?.SnapActionToIdle();
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

        void ReleaseSequence(DisplaceSequence sequence)
        {
            _active.Remove(sequence);
            sequence.Phase = Phase.Idle;
            sequence.TargetView = null;
            sequence.DestinationView = null;
            sequence.TargetAnimator = null;
            sequence.Flight = null;
            sequence.OnReadyForADrop = null;
            sequence.OnFailed = null;
            sequence.VisualOwnershipReleased = false;
            sequence.ReadyForADropSignaled = false;
            sequence.Mutated = false;
            sequence.LockToken = 0;
            sequence.DisplacedSprite = null;
            _pool.Add(sequence);
        }

        DisplaceSequence RentSequence()
        {
            if (_pool.Count > 0)
            {
                var last = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                return last;
            }

            return new DisplaceSequence();
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

        void EnsurePools()
        {
            if (dragView == null)
            {
                return;
            }

            if (_poolRoot == null)
            {
                var parent = dragView.transform;
                var existing = parent.Find("DisplacePresentationPool") as RectTransform;
                if (existing != null)
                {
                    _poolRoot = existing;
                }
                else
                {
                    var go = new GameObject("DisplacePresentationPool", typeof(RectTransform));
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

            while (_pool.Count < InitialPool)
            {
                _pool.Add(new DisplaceSequence());
            }
        }

        BoardMergeFlightView CreateFlight()
        {
            if (_poolRoot == null)
            {
                return null;
            }

            var go = new GameObject($"DisplaceFlight_{_flightPool.Count}", typeof(RectTransform));
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
    }
}
