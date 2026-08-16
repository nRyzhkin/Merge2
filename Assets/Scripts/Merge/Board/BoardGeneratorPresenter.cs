using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    /// <summary>
    /// Generator production presentation. Flight visuals live on BoardDragLayer
    /// exactly like merge flights / DragItemView — same parent, same WorldToLayer space.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardGeneratorPresenter : MonoBehaviour
    {
        const int InitialPool = 4;

        enum Phase
        {
            Idle,
            Flight
        }

        sealed class ProductionSequence
        {
            public Phase Phase = Phase.Idle;
            public GeneratorSpawnResult Result;
            public int InteractionLockToken;
            public BoardCellView GeneratorView;
            public BoardCellView SpawnView;
            public BoardItemAnimator GeneratorAnimator;
            public GeneratorFlightView Flight;
            public MergeFxView Burst;
            public bool VisualOwnershipReleased;
            public bool LoggedLanding;
        }

        [SerializeField] BoardController boardController;
        [SerializeField] BoardDragView dragView;
        [SerializeField] BoardGeneratorAnimationConfig config;
        [SerializeField] BoardMergeAnimationConfig mergeFxConfig;

        readonly List<ProductionSequence> _active = new List<ProductionSequence>(4);
        readonly List<ProductionSequence> _pool = new List<ProductionSequence>(4);
        readonly List<GeneratorFlightView> _flightPool = new List<GeneratorFlightView>(4);
        readonly List<MergeFxView> _fxPool = new List<MergeFxView>(4);
        readonly List<ProductionSequence> _scratch = new List<ProductionSequence>(4);
        RectTransform _poolRoot;
        bool _abortingAll;

        public void Configure(
            BoardController controller,
            BoardDragView view,
            BoardGeneratorAnimationConfig animationConfig,
            BoardMergeAnimationConfig mergeAnimationConfig,
            RectTransform unusedFlightLayer = null)
        {
            boardController = controller;
            dragView = view;
            config = animationConfig;
            mergeFxConfig = mergeAnimationConfig;
            EnsurePools();
        }

        public void Play(GeneratorSpawnResult result)
        {
            if (!result.Success || boardController == null || boardController.BoardView == null || dragView == null)
            {
                if (result.InteractionLockToken != 0 && boardController != null)
                {
                    boardController.ReleaseGeneratorSpawnLock(result.InteractionLockToken);
                }

                return;
            }

            Debug.Log("Generator production started");

            EnsurePools();
            var sequence = RentSequence();
            sequence.Result = result;
            sequence.InteractionLockToken = result.InteractionLockToken;
            sequence.Phase = Phase.Flight;
            sequence.VisualOwnershipReleased = false;
            sequence.LoggedLanding = false;
            sequence.GeneratorView = boardController.BoardView.GetCellView(result.GeneratorIndex);
            sequence.SpawnView = boardController.BoardView.GetCellView(result.SpawnCellIndex);
            sequence.GeneratorAnimator = sequence.GeneratorView != null ? sequence.GeneratorView.ItemAnimator : null;
            sequence.Flight = null;
            sequence.Burst = null;

            ClaimSpawnVisual(sequence);

            if (sequence.GeneratorAnimator != null && config != null)
            {
                sequence.GeneratorAnimator.PlayGeneratorTap(config);
            }

            sequence.Burst = RentFx();
            if (sequence.Burst != null && sequence.GeneratorView != null)
            {
                var parent = sequence.GeneratorView.FxRoot != null
                    ? sequence.GeneratorView.FxRoot
                    : sequence.GeneratorView.transform as RectTransform;
                PlayBurst(sequence.Burst, parent);
            }

            if (!BeginFlight(sequence))
            {
                Handoff(sequence);
                Finish(sequence);
                return;
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
                if (sequence == null || sequence.Result.SpawnCellIndex != cellIndex)
                {
                    continue;
                }

                sequence.VisualOwnershipReleased = true;
                if (sequence.Flight != null)
                {
                    sequence.Flight.HideImmediate();
                    ReturnFlight(sequence.Flight);
                    sequence.Flight = null;
                }

                ReleaseSpawnVisual(sequence);
            }
        }

        void Update()
        {
            if (_active.Count == 0)
            {
                enabled = false;
                return;
            }

            var dt = config != null ? config.GetDeltaTime() : Time.unscaledDeltaTime;
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

        void TickSequence(ProductionSequence sequence, float dt)
        {
            if (sequence.Phase != Phase.Flight)
            {
                return;
            }

            if (sequence.VisualOwnershipReleased)
            {
                Finish(sequence);
                return;
            }

            var flying = sequence.Flight != null && sequence.Flight.Tick(dt);
            if (flying)
            {
                if (!sequence.LoggedLanding && sequence.Flight != null && sequence.Flight.IsLanding)
                {
                    sequence.LoggedLanding = true;
                    Debug.Log("Generator flight landed");
                    ReleaseInteractionLock(sequence);
                }

                return;
            }

            if (!sequence.LoggedLanding)
            {
                sequence.LoggedLanding = true;
                Debug.Log("Generator flight landed");
            }

            Handoff(sequence);
            Finish(sequence);
        }

        bool BeginFlight(ProductionSequence sequence)
        {
            if (sequence.VisualOwnershipReleased || sequence.SpawnView == null || dragView == null)
            {
                return false;
            }

            Sprite sprite = null;
            if (boardController.ItemDatabase != null &&
                boardController.ItemDatabase.TryGetById(sequence.Result.GeneratedItemId, out var data) &&
                data != null)
            {
                sprite = data.Icon;
            }

            // Same space as DragItemView / merge flights.
            var startPos = GetCellUiCenter(sequence.Result.GeneratorIndex);
            var endPos = GetCellUiCenter(sequence.Result.SpawnCellIndex);

            // Same as BoardDragController: use rect.size, NOT sizeDelta (stretch cells are 0x0).
            var size = new Vector2(170f, 170f);
            var preserveAspect = true;
            if (sequence.SpawnView.ItemImage != null)
            {
                var itemRect = sequence.SpawnView.ItemImage.rectTransform;
                var rectSize = itemRect.rect.size;
                if (rectSize.x > 1f && rectSize.y > 1f)
                {
                    size = rectSize;
                }

                preserveAspect = sequence.SpawnView.ItemImage.preserveAspect;
            }
            else if (sequence.GeneratorView != null && sequence.GeneratorView.ItemImage != null)
            {
                var itemRect = sequence.GeneratorView.ItemImage.rectTransform;
                var rectSize = itemRect.rect.size;
                if (rectSize.x > 1f && rectSize.y > 1f)
                {
                    size = rectSize;
                }

                preserveAspect = sequence.GeneratorView.ItemImage.preserveAspect;
            }

            sequence.Flight = RentFlight();
            if (sequence.Flight == null)
            {
                Debug.LogError("[Generator] Failed to rent GeneratorFlightView.");
                return false;
            }

            sequence.Flight.Begin(
                sprite,
                size,
                preserveAspect,
                Color.white,
                startPos,
                endPos,
                config);

            Debug.Log("GeneratorFlightView spawned");
            return sequence.Flight.IsActive;
        }

        void ClaimSpawnVisual(ProductionSequence sequence)
        {
            if (sequence.SpawnView == null)
            {
                return;
            }

            sequence.SpawnView.SetItemPresentationSuppressed(true);
            sequence.SpawnView.SetHideItemForDrag(true);
            if (boardController != null && boardController.BoardView != null)
            {
                boardController.BoardView.RefreshCell(sequence.Result.SpawnCellIndex);
            }
        }

        void ReleaseSpawnVisual(ProductionSequence sequence)
        {
            if (sequence.SpawnView == null)
            {
                return;
            }

            sequence.SpawnView.SetItemPresentationSuppressed(false);
            sequence.SpawnView.SetHideItemForDrag(false);
            if (boardController != null && boardController.BoardView != null)
            {
                boardController.BoardView.RefreshCell(sequence.Result.SpawnCellIndex);
            }

            sequence.SpawnView.ItemAnimator?.SnapActionToIdle();
        }

        void Handoff(ProductionSequence sequence)
        {
            if (sequence.Flight != null)
            {
                sequence.Flight.HideImmediate();
                ReturnFlight(sequence.Flight);
                sequence.Flight = null;
            }

            if (sequence.VisualOwnershipReleased || sequence.SpawnView == null)
            {
                return;
            }

            ReleaseSpawnVisual(sequence);
            Debug.Log("BoardCellView owns item now");
        }

        void ReleaseInteractionLock(ProductionSequence sequence)
        {
            if (sequence.InteractionLockToken == 0 || boardController == null)
            {
                return;
            }

            boardController.ReleaseGeneratorSpawnLock(sequence.InteractionLockToken);
            sequence.InteractionLockToken = 0;
        }

        void Finish(ProductionSequence sequence)
        {
            if (!sequence.VisualOwnershipReleased)
            {
                EnsureSpawnCellVisible(sequence);
            }

            ReleaseInteractionLock(sequence);

            if (sequence.Burst != null && !sequence.Burst.IsPlaying)
            {
                ReturnFx(sequence.Burst);
                sequence.Burst = null;
            }

            if (sequence.GeneratorAnimator != null)
            {
                sequence.GeneratorAnimator.PlayClickRelease();
            }

            ReleaseSequence(sequence);
        }

        void EnsureSpawnCellVisible(ProductionSequence sequence)
        {
            if (sequence.SpawnView == null)
            {
                return;
            }

            sequence.SpawnView.SetItemPresentationSuppressed(false);
            sequence.SpawnView.SetHideItemForDrag(false);
            if (boardController != null && boardController.BoardView != null)
            {
                boardController.BoardView.RefreshCell(sequence.Result.SpawnCellIndex);
            }
        }

        void AbortSequence(ProductionSequence sequence)
        {
            if (sequence.Phase == Phase.Idle)
            {
                return;
            }

            if (sequence.Flight != null)
            {
                sequence.Flight.HideImmediate();
                ReturnFlight(sequence.Flight);
                sequence.Flight = null;
            }

            if (sequence.Burst != null)
            {
                sequence.Burst.Hide();
                ReturnFx(sequence.Burst);
                sequence.Burst = null;
            }

            EnsureSpawnCellVisible(sequence);
            ReleaseInteractionLock(sequence);
            ReleaseSequence(sequence);
        }

        Vector2 GetCellUiCenter(int index)
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

        void PlayBurst(MergeFxView fx, RectTransform parent)
        {
            if (fx == null)
            {
                return;
            }

            if (mergeFxConfig != null)
            {
                fx.Play(parent, mergeFxConfig);
            }

            var rect = fx.transform as RectTransform;
            if (rect != null)
            {
                var scale = config != null ? config.ProductionBurstScale : 0.9f;
                rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        void ReleaseSequence(ProductionSequence sequence)
        {
            _active.Remove(sequence);
            sequence.Phase = Phase.Idle;
            sequence.GeneratorView = null;
            sequence.SpawnView = null;
            sequence.GeneratorAnimator = null;
            sequence.Flight = null;
            sequence.Burst = null;
            sequence.VisualOwnershipReleased = false;
            sequence.LoggedLanding = false;
            sequence.InteractionLockToken = 0;
            _pool.Add(sequence);
        }

        ProductionSequence RentSequence()
        {
            if (_pool.Count > 0)
            {
                var last = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                return last;
            }

            return new ProductionSequence();
        }

        GeneratorFlightView RentFlight()
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

        void ReturnFlight(GeneratorFlightView flight)
        {
            flight?.HideImmediate();
        }

        MergeFxView RentFx()
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

        void ReturnFx(MergeFxView fx)
        {
            fx?.Hide();
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
                var existing = parent.Find("GeneratorPresentationPool") as RectTransform;
                if (existing != null)
                {
                    _poolRoot = existing;
                }
                else
                {
                    var go = new GameObject("GeneratorPresentationPool", typeof(RectTransform));
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
                _pool.Add(new ProductionSequence());
            }
        }

        GeneratorFlightView CreateFlight()
        {
            if (_poolRoot == null)
            {
                return null;
            }

            var go = new GameObject($"GeneratorFlight_{_flightPool.Count}", typeof(RectTransform));
            go.layer = _poolRoot.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_poolRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var flight = go.AddComponent<GeneratorFlightView>();
            flight.Ensure(_poolRoot);
            flight.HideImmediate();
            _flightPool.Add(flight);
            return flight;
        }

        MergeFxView CreateFx()
        {
            if (_poolRoot == null)
            {
                return null;
            }

            var go = new GameObject($"GeneratorBurst_{_fxPool.Count}", typeof(RectTransform));
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
