using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardBoxRevealPresenter : MonoBehaviour
    {
        const int InitialPool = 6;
        const int InitialFxPool = 8;

        enum Phase
        {
            Idle,
            Anticipation,
            Break
        }

        sealed class RevealSequence
        {
            public Phase Phase = Phase.Idle;
            public float Elapsed;
            public BoxRevealResult Result;
            public int LockToken;
            public bool ItemHandedOff;
            public BoardCellView CellView;
            public BoardItemAnimator Animator;
            public BoxBreakFxView Fx;
        }

        [SerializeField] BoardController boardController;
        [SerializeField] BoardBoxAnimationConfig config;

        readonly List<RevealSequence> _active = new List<RevealSequence>(8);
        readonly List<RevealSequence> _pool = new List<RevealSequence>(8);
        readonly List<BoxBreakFxView> _fxPool = new List<BoxBreakFxView>(8);
        readonly List<RevealSequence> _scratch = new List<RevealSequence>(8);
        RectTransform _poolRoot;
        bool _abortingAll;

        public event Action BoxBreak;
        public event Action BoxItemRevealed;

        public void Configure(BoardController controller, BoardBoxAnimationConfig animationConfig)
        {
            boardController = controller;
            config = animationConfig;
            EnsurePools();
        }

        public void Play(IReadOnlyList<BoxRevealResult> reveals)
        {
            if (boardController == null || boardController.BoardView == null || config == null || reveals == null)
            {
                return;
            }

            EnsurePools();
            for (var i = 0; i < reveals.Count; i++)
            {
                PlayOne(reveals[i]);
            }

            if (_active.Count > 0)
            {
                enabled = true;
            }
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
                if (sequence == null || sequence.Result.CellIndex != cellIndex)
                {
                    continue;
                }

                HandoffItem(sequence);
            }
        }

        void PlayOne(BoxRevealResult result)
        {
            var view = boardController.BoardView.GetCellView(result.CellIndex);
            if (view == null)
            {
                return;
            }

            var sequence = RentSequence();
            sequence.Result = result;
            sequence.Elapsed = 0f;
            sequence.Phase = Phase.Anticipation;
            sequence.ItemHandedOff = false;
            sequence.CellView = view;
            sequence.Animator = view.ItemAnimator;
            sequence.Fx = null;
            sequence.LockToken = boardController.InteractionLocks.Acquire(
                result.CellIndex,
                BoardController.NoSelectionIndex);
            if (result.RevealedItem)
            {
                view.SetHideItemForDrag(true);
            }

            view.BeginBoxRevealPresentation(config);
            view.PlayBoxAnticipation(config);
            _active.Add(sequence);
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

        void TickSequence(RevealSequence sequence, float dt)
        {
            if (sequence.Phase == Phase.Idle)
            {
                return;
            }

            sequence.Elapsed += dt;
            if (sequence.Phase == Phase.Anticipation)
            {
                if (sequence.Elapsed < config.AnticipationDuration)
                {
                    return;
                }

                BeginBreak(sequence);
                return;
            }

            if (sequence.Phase == Phase.Break)
            {
                if (!sequence.ItemHandedOff && sequence.Elapsed >= config.BreakDuration * 0.55f)
                {
                    HandoffItem(sequence);
                }

                if (sequence.Elapsed < config.BreakDuration)
                {
                    return;
                }

                Finish(sequence);
            }
        }

        void BeginBreak(RevealSequence sequence)
        {
            sequence.Elapsed = 0f;
            sequence.Phase = Phase.Break;
            if (sequence.CellView != null)
            {
                sequence.CellView.PlayBoxBreak(config);
            }

            sequence.Fx = RentFx();
            if (sequence.Fx != null && sequence.CellView != null)
            {
                var parent = sequence.CellView.FxRoot != null
                    ? sequence.CellView.FxRoot
                    : sequence.CellView.transform as RectTransform;
                sequence.Fx.Play(parent, config);
            }

            BoxBreak?.Invoke();
            boardController?.NotifyBoxBreak();
        }

        void HandoffItem(RevealSequence sequence)
        {
            if (sequence.ItemHandedOff)
            {
                return;
            }

            sequence.ItemHandedOff = true;
            if (sequence.CellView != null)
            {
                sequence.CellView.ClearBoxRevealPresentation();
            }

            if (boardController != null && boardController.BoardView != null)
            {
                boardController.BoardView.RefreshCell(sequence.Result.CellIndex);
            }

            ReleaseLock(sequence);

            if (sequence.Result.RevealedItem &&
                sequence.CellView != null &&
                !sequence.CellView.IsItemPresentationSuppressed &&
                sequence.Animator != null)
            {
                sequence.Animator.PlayBoxItemReveal(config);
            }

            if (sequence.CellView != null)
            {
                sequence.CellView.SetHideItemForDrag(false);
            }

            if (sequence.Result.RevealedItem &&
                sequence.CellView != null &&
                !sequence.CellView.IsItemPresentationSuppressed)
            {
                BoxItemRevealed?.Invoke();
                boardController?.NotifyBoxItemRevealed();
            }
        }

        void Finish(RevealSequence sequence)
        {
            HandoffItem(sequence);
            if (sequence.Fx != null && !sequence.Fx.IsPlaying)
            {
                ReturnFx(sequence.Fx);
                sequence.Fx = null;
            }

            ReleaseSequence(sequence);
        }

        void AbortSequence(RevealSequence sequence)
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

            if (sequence.CellView != null)
            {
                sequence.CellView.ClearBoxRevealPresentation();
                if (!sequence.CellView.IsItemPresentationSuppressed)
                {
                    sequence.CellView.SetHideItemForDrag(false);
                    sequence.Animator?.CancelTransientAnimationAndAdoptCurrentVisualState();
                }
            }

            if (boardController != null && boardController.BoardView != null &&
                (sequence.CellView == null || !sequence.CellView.IsItemPresentationSuppressed))
            {
                boardController.BoardView.RefreshCell(sequence.Result.CellIndex);
            }

            ReleaseLock(sequence);
            ReleaseSequence(sequence);
        }

        void ReleaseLock(RevealSequence sequence)
        {
            if (boardController == null || sequence.LockToken == 0)
            {
                return;
            }

            boardController.InteractionLocks.Release(sequence.LockToken);
            sequence.LockToken = 0;
        }

        void ReleaseSequence(RevealSequence sequence)
        {
            _active.Remove(sequence);
            ReleaseLock(sequence);
            sequence.Phase = Phase.Idle;
            sequence.CellView = null;
            sequence.Animator = null;
            sequence.Fx = null;
            sequence.LockToken = 0;
            sequence.ItemHandedOff = false;
            _pool.Add(sequence);
        }

        RevealSequence RentSequence()
        {
            if (_pool.Count > 0)
            {
                var last = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                return last;
            }

            return new RevealSequence();
        }

        BoxBreakFxView RentFx()
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

        void ReturnFx(BoxBreakFxView fx)
        {
            fx?.Hide();
        }

        void EnsurePools()
        {
            if (_poolRoot == null)
            {
                var parent = boardController != null && boardController.DragView != null
                    ? boardController.DragView.transform
                    : transform;
                var existing = parent.Find("BoxRevealPresentationPool") as RectTransform;
                if (existing != null)
                {
                    _poolRoot = existing;
                }
                else
                {
                    var go = new GameObject("BoxRevealPresentationPool", typeof(RectTransform));
                    go.layer = parent.gameObject.layer;
                    _poolRoot = go.GetComponent<RectTransform>();
                    _poolRoot.SetParent(parent, false);
                    _poolRoot.anchorMin = Vector2.zero;
                    _poolRoot.anchorMax = Vector2.one;
                    _poolRoot.offsetMin = Vector2.zero;
                    _poolRoot.offsetMax = Vector2.zero;
                }
            }

            while (_fxPool.Count < InitialFxPool)
            {
                CreateFx();
            }

            while (_pool.Count < InitialPool)
            {
                _pool.Add(new RevealSequence());
            }
        }

        BoxBreakFxView CreateFx()
        {
            var go = new GameObject($"BoxBreakFx_{_fxPool.Count}", typeof(RectTransform));
            go.layer = _poolRoot.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_poolRoot, false);
            var fx = go.AddComponent<BoxBreakFxView>();
            fx.EnsureChildren();
            fx.Hide();
            _fxPool.Add(fx);
            return fx;
        }
    }
}
