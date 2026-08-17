using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    public class BoardController : MonoBehaviour
    {
        public const int NoSelectionIndex = -1;

        [SerializeField] RectTransform boardRoot;
        [SerializeField] BoardView boardView;
        [SerializeField] MergeItemDatabase itemDatabase;
        [SerializeField] BoardVisualConfig visualConfig;
        [SerializeField] BoardItemAnimationConfig animationConfig;
        [SerializeField] UiInteractionFeedbackConfig uiFeedbackConfig;
        [SerializeField] BoardDragAnimationConfig dragAnimationConfig;
        [SerializeField] BoardMergeAnimationConfig mergeAnimationConfig;
        [SerializeField] BoardCobwebAnimationConfig cobwebAnimationConfig;
        [SerializeField] BoardBoxAnimationConfig boxAnimationConfig;
        [SerializeField] BoardGeneratorAnimationConfig generatorAnimationConfig;
        [SerializeField] GeneratorProductionDatabase generatorProductionDatabase;
        [SerializeField] BoardDragView dragView;
        [SerializeField] BoardMergePresenter mergePresenter;
        [SerializeField] BoardCobwebPresenter cobwebPresenter;
        [SerializeField] BoardBoxRevealPresenter boxRevealPresenter;
        [SerializeField] BoardGeneratorPresenter generatorPresenter;
        [SerializeField] BoardDisplacePresenter displacePresenter;
        [SerializeField] MessagePresenter messagePresenter;
        [SerializeField] BoardSelectionView selectionView;
        [SerializeField] ItemInfoView itemInfoView;
        [SerializeField] bool useDevelopmentBoardState = true;

        [Header("Generator Debug")]
        [SerializeField] bool useDebugCooldownOverride;
        [SerializeField] float debugCooldownSeconds = 5f;

        [Header("Debug")]
        [SerializeField] int debugSelectedCellIndex = NoSelectionIndex;
        [SerializeField] int debugSelectedItemId = BoardCellState.EmptyItemId;
        [SerializeField] string debugSelectedInternalKey;
        [SerializeField] int debugDiscoveredCount;

        BoardState _state;
        MergeDiscoveryState _discovery;
        readonly BoardInteractionLockService _interactionLocks = new BoardInteractionLockService();
        readonly List<int> _orthogonalScratch = new List<int>(4);
        readonly List<BoxRevealResult> _boxRevealScratch = new List<BoxRevealResult>(4);
        readonly GeneratorInstanceService _generatorInstances = new GeneratorInstanceService();
        readonly IGameTimeProvider _gameTimeProvider = new GameTimeProvider();
        readonly IGeneratorRandom _generatorRandom = new GeneratorRandomService();
        int _selectedCellIndex = NoSelectionIndex;
        int _selectionRevision;
        BoardDragController _dragController;
        BoardGeneratorCooldownPresenter _generatorCooldownPresenter;

        public RectTransform BoardRoot => boardRoot;
        public BoardView BoardView => boardView;
        public MergeItemDatabase ItemDatabase => itemDatabase;
        public BoardVisualConfig VisualConfig => visualConfig;
        public BoardItemAnimationConfig AnimationConfig => animationConfig;
        public UiInteractionFeedbackConfig UiFeedbackConfig => uiFeedbackConfig;
        public BoardDragAnimationConfig DragAnimationConfig => dragAnimationConfig;
        public BoardMergeAnimationConfig MergeAnimationConfig => mergeAnimationConfig;
        public BoardCobwebAnimationConfig CobwebAnimationConfig => cobwebAnimationConfig;
        public BoardBoxAnimationConfig BoxAnimationConfig => boxAnimationConfig;
        public BoardGeneratorAnimationConfig GeneratorAnimationConfig => generatorAnimationConfig;
        public GeneratorProductionDatabase GeneratorProductionDatabase => generatorProductionDatabase;
        public BoardDragView DragView => dragView;
        public BoardMergePresenter MergePresenter => mergePresenter;
        public BoardCobwebPresenter CobwebPresenter => cobwebPresenter;
        public BoardBoxRevealPresenter BoxRevealPresenter => boxRevealPresenter;
        public BoardGeneratorPresenter GeneratorPresenter => generatorPresenter;
        public BoardDisplacePresenter DisplacePresenter => displacePresenter;
        public MessagePresenter MessagePresenter => messagePresenter;
        public BoardDragController DragController => _dragController;
        public BoardSelectionView SelectionView => selectionView;
        public ItemInfoView ItemInfoView => itemInfoView;
        public BoardState State => _state;
        public MergeDiscoveryState Discovery => _discovery;
        public BoardInteractionLockService InteractionLocks => _interactionLocks;
        public bool UseDevelopmentBoardState => useDevelopmentBoardState;
        public bool UseDebugCooldownOverride => useDebugCooldownOverride;
        public float DebugCooldownSeconds => debugCooldownSeconds;
        public GeneratorInstanceService GeneratorInstances => _generatorInstances;
        public IGameTimeProvider GameTimeProvider => _gameTimeProvider;
        public IGeneratorRandom GeneratorRandom => _generatorRandom;
        public int SelectedCellIndex => _selectedCellIndex;
        public int SelectionRevision => _selectionRevision;
        public bool IsDragInteractionActive => _dragController != null && _dragController.IsBusy;

        public bool IsCellInteractionLocked(int index)
        {
            return _interactionLocks.IsLocked(index);
        }

        public event Action<int> ItemDiscovered;
        public event Action MergeImpact;
        public event Action NewItemAppeared;
        public event Action<int, int> MergeCommitted;
        public event Action BoxBreak;
        public event Action BoxItemRevealed;
        public event Action GeneratorProduced;
        public event Action<int, int> GeneratorRareDrop;

        void Awake()
        {
            _state = null;
            _selectedCellIndex = NoSelectionIndex;
            _discovery = new MergeDiscoveryState();
            if (selectionView != null)
            {
                selectionView.Hide();
            }

            if (itemInfoView != null)
            {
                itemInfoView.Hide();
            }
        }

        void Start()
        {
            if (!ValidateDependencies())
            {
                return;
            }

            boardView.BindInteraction(this, animationConfig, uiFeedbackConfig);
            EnsureDrag();

            if (useDevelopmentBoardState)
            {
                ResetDevelopmentBoard();
            }
            else
            {
                _state = new BoardState();
                _discovery = new MergeDiscoveryState();
                BindAndRefresh();
                ClearSelection();
            }
        }

        public void ResetDevelopmentBoard()
        {
            if (!ValidateDependencies())
            {
                return;
            }

            if (_dragController != null)
            {
                _dragController.AbortImmediate();
            }

            if (mergePresenter != null)
            {
                mergePresenter.AbortAll();
            }

            if (cobwebPresenter != null)
            {
                cobwebPresenter.AbortAll();
            }

            if (boxRevealPresenter != null)
            {
                boxRevealPresenter.AbortAll();
            }

            if (generatorPresenter != null)
            {
                generatorPresenter.AbortAll();
            }

            if (displacePresenter != null)
            {
                displacePresenter.AbortAll();
            }

            _interactionLocks.ReleaseAll();

            if (boardView != null)
            {
                boardView.ClearItemDragHides();
            }

            ClearSelection();
            _state = BoardDevelopmentStateFactory.Create(itemDatabase);
            _discovery = new MergeDiscoveryState();
            _generatorInstances.Clear();
            _discovery.DiscoverFromBoard(_state);
            SyncGeneratorInstancesFromBoard();
            BindAndRefresh();
            UpdateDebug();
            BoardLayoutValidator.Validate(_state, itemDatabase);
        }

        public void SelectCell(int index)
        {
            if (_state == null || !_state.IsValidIndex(index))
            {
                ClearSelection();
                return;
            }

            var cell = _state.GetCell(index);
            if (cell == null || !cell.HasItem || cell.IsBox)
            {
                ClearSelection();
                return;
            }

            _discovery.Discover(cell.ItemId);
            _selectedCellIndex = index;
            _selectionRevision++;
            if (selectionView != null && boardView != null)
            {
                selectionView.ShowOn(boardView.GetCellView(index));
            }

            if (itemDatabase.TryGetById(cell.ItemId, out var data))
            {
                ShowItemInfo(data);
                if (data.Kind == MergeItemKind.Generator &&
                    TryGetGeneratorPresentationInfo(index, out var generatorInfo))
                {
                    itemInfoView?.ApplyGeneratorPresentation(generatorInfo);
                }
            }
            else
            {
                Debug.LogError($"[BoardController] Selected unknown item id {cell.ItemId} at cell {index}.");
            }

            UpdateDebug();
        }

        public void ClearSelection()
        {
            _selectedCellIndex = NoSelectionIndex;
            _selectionRevision++;
            if (selectionView != null)
            {
                selectionView.Hide();
            }

            if (itemInfoView != null)
            {
                itemInfoView.Hide();
            }

            UpdateDebug();
        }

        public BoardCellState GetSelectedCell()
        {
            if (_state == null || _selectedCellIndex == NoSelectionIndex)
            {
                return null;
            }

            return _state.GetCell(_selectedCellIndex);
        }

        public void SetBoardRoot(RectTransform root)
        {
            boardRoot = root;
        }

        public void SetBoardView(BoardView view)
        {
            boardView = view;
        }

        public void SetItemDatabase(MergeItemDatabase database)
        {
            itemDatabase = database;
        }

        public void SetVisualConfig(BoardVisualConfig config)
        {
            visualConfig = config;
        }

        public void SetAnimationConfig(BoardItemAnimationConfig config)
        {
            animationConfig = config;
        }

        public void SetUiFeedbackConfig(UiInteractionFeedbackConfig config)
        {
            uiFeedbackConfig = config;
        }

        public bool TryMoveItemToEmpty(int fromIndex, int toIndex)
        {
            if (_state == null || !_state.IsValidIndex(fromIndex) || !_state.IsValidIndex(toIndex) || fromIndex == toIndex)
            {
                return false;
            }

            var source = _state.GetCell(fromIndex);
            var target = _state.GetCell(toIndex);
            if (source == null || target == null || !source.HasItem || source.IsBox || source.ItemLocked || !target.IsEmpty)
            {
                return false;
            }

            if (IsCellInteractionLocked(fromIndex) || IsCellInteractionLocked(toIndex))
            {
                return false;
            }

            target.SetItem(source.ItemId);
            TransferItemInstanceState(source, target);
            source.Clear();
            if (boardView != null)
            {
                boardView.RefreshCell(fromIndex);
                boardView.RefreshCell(toIndex);
            }

            UpdateDebug();
            return true;
        }

        public bool CanDisplaceOccupiedTarget(int occupiedTargetIndex)
        {
            if (_state == null || !_state.IsValidIndex(occupiedTargetIndex) || IsCellInteractionLocked(occupiedTargetIndex))
            {
                return false;
            }

            return IsDisplaceableOccupant(occupiedTargetIndex);
        }

        bool IsDisplaceableOccupant(int occupiedTargetIndex)
        {
            var cell = _state.GetCell(occupiedTargetIndex);
            if (cell == null || !cell.HasItem || cell.IsBox || cell.ItemLocked || cell.BlockType != CellBlockType.None)
            {
                return false;
            }

            if (boardView != null)
            {
                var view = boardView.GetCellView(occupiedTargetIndex);
                if (view != null && view.IsTransientAnimationRunning())
                {
                    return false;
                }
            }

            return true;
        }

        public int FindDisplaceDestination(int occupiedTargetIndex, int dragSourceIndex)
        {
            if (boardView == null || _state == null || !_state.IsValidIndex(occupiedTargetIndex))
            {
                return NoSelectionIndex;
            }

            var origin = GetCellUiCenter(occupiedTargetIndex);
            var bestDistance = float.MaxValue;
            var bestIndex = NoSelectionIndex;
            var cells = boardView.Cells;
            if (cells == null)
            {
                return NoSelectionIndex;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                var cellView = cells[i];
                if (cellView == null)
                {
                    continue;
                }

                var index = cellView.Index;
                if (index == occupiedTargetIndex)
                {
                    continue;
                }

                if (!IsDisplaceDestinationCandidate(index, dragSourceIndex))
                {
                    continue;
                }

                var distance = Vector2.Distance(origin, GetCellUiCenter(index));
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestIndex = index;
            }

            return bestIndex;
        }

        bool IsDisplaceDestinationCandidate(int index, int dragSourceIndex)
        {
            if (_state == null || !_state.IsValidIndex(index) || IsCellInteractionLocked(index))
            {
                return false;
            }

            // Source still holds the dragged item in BoardState until mutate — allow it.
            if (index == dragSourceIndex)
            {
                return true;
            }

            var cell = _state.GetCell(index);
            return cell != null && cell.IsEmpty && cell.BlockType == CellBlockType.None;
        }

        public DisplaceResult TryDisplaceItem(int sourceIndex, int occupiedTargetIndex, int displacementDestinationIndex)
        {
            if (_state == null ||
                !_state.IsValidIndex(sourceIndex) ||
                !_state.IsValidIndex(occupiedTargetIndex) ||
                !_state.IsValidIndex(displacementDestinationIndex))
            {
                return DisplaceResult.Failed(sourceIndex, occupiedTargetIndex, displacementDestinationIndex);
            }

            if (sourceIndex == occupiedTargetIndex || occupiedTargetIndex == displacementDestinationIndex)
            {
                return DisplaceResult.Failed(sourceIndex, occupiedTargetIndex, displacementDestinationIndex);
            }

            // Interaction locks may already be held by the displace sequence.
            if (!IsDisplaceableOccupant(occupiedTargetIndex))
            {
                return DisplaceResult.Failed(sourceIndex, occupiedTargetIndex, displacementDestinationIndex);
            }

            var source = _state.GetCell(sourceIndex);
            var target = _state.GetCell(occupiedTargetIndex);
            var destination = _state.GetCell(displacementDestinationIndex);
            if (source == null || target == null || destination == null)
            {
                return DisplaceResult.Failed(sourceIndex, occupiedTargetIndex, displacementDestinationIndex);
            }

            if (!source.HasItem || source.IsBox || source.ItemLocked)
            {
                return DisplaceResult.Failed(sourceIndex, occupiedTargetIndex, displacementDestinationIndex);
            }

            if (displacementDestinationIndex != sourceIndex)
            {
                if (!destination.IsEmpty || destination.BlockType != CellBlockType.None)
                {
                    return DisplaceResult.Failed(sourceIndex, occupiedTargetIndex, displacementDestinationIndex);
                }
            }

            var draggedId = source.ItemId;
            var displacedId = target.ItemId;
            var draggedInstanceId = source.GeneratorInstanceId;
            var displacedInstanceId = target.GeneratorInstanceId;

            source.Clear();
            target.SetItem(draggedId, locked: false);
            target.GeneratorInstanceId = draggedInstanceId;
            destination.SetItem(displacedId, locked: false);
            destination.GeneratorInstanceId = displacedInstanceId;

            if (boardView != null)
            {
                boardView.RefreshCell(sourceIndex);
                boardView.RefreshCell(occupiedTargetIndex);
                boardView.RefreshCell(displacementDestinationIndex);
            }

            UpdateDebug();
            return new DisplaceResult
            {
                Success = true,
                SourceIndex = sourceIndex,
                OccupiedTargetIndex = occupiedTargetIndex,
                DestinationIndex = displacementDestinationIndex,
                DraggedItemId = draggedId,
                DisplacedItemId = displacedId
            };
        }

        public bool CanMerge(int fromIndex, int toIndex)
        {
            if (IsCellInteractionLocked(fromIndex) || IsCellInteractionLocked(toIndex))
            {
                return false;
            }

            return TryGetMergeResultItemId(fromIndex, toIndex, out _);
        }

        public bool TryGetMergeResultItemId(int fromIndex, int toIndex, out int resultItemId)
        {
            resultItemId = BoardCellState.EmptyItemId;
            if (_state == null || itemDatabase == null || fromIndex == toIndex)
            {
                return false;
            }

            if (!_state.IsValidIndex(fromIndex) || !_state.IsValidIndex(toIndex))
            {
                return false;
            }

            var source = _state.GetCell(fromIndex);
            var target = _state.GetCell(toIndex);
            if (source == null || target == null || !source.HasItem || !target.HasItem)
            {
                return false;
            }

            if (source.IsBox || target.IsBox || source.ItemLocked)
            {
                return false;
            }

            if (source.ItemId != target.ItemId)
            {
                return false;
            }

            if (!itemDatabase.TryGetById(source.ItemId, out var data) || data == null)
            {
                return false;
            }

            if (data.NextItemId == MergeItemIdUtility.NoNextItemId)
            {
                return false;
            }

            resultItemId = data.NextItemId;
            return true;
        }

        public bool CanUnlockCobweb(int fromIndex, int lockedTargetIndex)
        {
            if (IsCellInteractionLocked(fromIndex) || IsCellInteractionLocked(lockedTargetIndex))
            {
                return false;
            }

            return TryValidateUnlock(fromIndex, lockedTargetIndex, out _);
        }

        public bool TryValidateUnlock(int fromIndex, int lockedTargetIndex, out int itemId)
        {
            itemId = BoardCellState.EmptyItemId;
            if (_state == null || fromIndex == lockedTargetIndex)
            {
                return false;
            }

            if (!_state.IsValidIndex(fromIndex) || !_state.IsValidIndex(lockedTargetIndex))
            {
                return false;
            }

            var source = _state.GetCell(fromIndex);
            var target = _state.GetCell(lockedTargetIndex);
            if (source == null || target == null || !source.HasItem || !target.HasItem)
            {
                return false;
            }

            if (source.IsBox || target.IsBox || source.ItemLocked || !target.ItemLocked)
            {
                return false;
            }

            if (source.ItemId != target.ItemId)
            {
                return false;
            }

            itemId = source.ItemId;
            return true;
        }

        public int FindUnlockDestination(int sourceIndex, int lockedTargetIndex)
        {
            if (boardView == null || _state == null)
            {
                return sourceIndex;
            }

            var origin = GetCellUiCenter(lockedTargetIndex);
            var bestDistance = float.MaxValue;
            var bestIndex = BoardController.NoSelectionIndex;
            var cells = boardView.Cells;
            if (cells == null)
            {
                return sourceIndex;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                var cellView = cells[i];
                if (cellView == null)
                {
                    continue;
                }

                var index = cellView.Index;
                if (index == lockedTargetIndex)
                {
                    continue;
                }

                if (index != sourceIndex && !IsUnlockDestinationCandidate(index))
                {
                    continue;
                }

                if (index != sourceIndex && IsCellInteractionLocked(index))
                {
                    continue;
                }

                var distance = Vector2.Distance(origin, GetCellUiCenter(index));
                // Prefer a real empty cell over returning to source when distances tie.
                var better = distance < bestDistance - 0.01f
                    || (Mathf.Abs(distance - bestDistance) <= 0.01f
                        && bestIndex == sourceIndex
                        && index != sourceIndex);
                if (!better)
                {
                    continue;
                }

                bestDistance = distance;
                bestIndex = index;
            }

            return bestIndex != NoSelectionIndex ? bestIndex : sourceIndex;
        }

        public UnlockResult TryUnlockLockedItem(int sourceIndex, int lockedTargetIndex, int destinationIndex)
        {
            var result = new UnlockResult
            {
                Success = false,
                SourceIndex = sourceIndex,
                LockedTargetIndex = lockedTargetIndex,
                DestinationIndex = destinationIndex,
                ItemId = BoardCellState.EmptyItemId
            };

            if (!TryValidateUnlock(sourceIndex, lockedTargetIndex, out var itemId))
            {
                return result;
            }

            if (!_state.IsValidIndex(destinationIndex) || destinationIndex == lockedTargetIndex)
            {
                return result;
            }

            if (destinationIndex != sourceIndex)
            {
                // Destination may already be interaction-locked by this unlock sequence — only require empty.
                var destCell = _state.GetCell(destinationIndex);
                if (destCell == null || !destCell.IsEmpty)
                {
                    return result;
                }
            }

            var source = _state.GetCell(sourceIndex);
            var target = _state.GetCell(lockedTargetIndex);
            var destination = _state.GetCell(destinationIndex);
            if (source == null || target == null || destination == null)
            {
                return result;
            }

            if (destinationIndex != sourceIndex)
            {
                var sourceInstanceId = source.GeneratorInstanceId;
                source.Clear();
                destination.SetItem(itemId, locked: false);
                destination.GeneratorInstanceId = sourceInstanceId;
            }

            target.ItemLocked = false;
            result.Success = true;
            result.ItemId = itemId;

            if (boardView != null)
            {
                // Destination stays visually hidden until unlock flight lands.
                if (destinationIndex != sourceIndex)
                {
                    var sourceView = boardView.GetCellView(sourceIndex);
                    if (sourceView != null)
                    {
                        sourceView.SetItemPresentationSuppressed(false);
                        sourceView.SetHideItemForDrag(false);
                    }

                    var destView = boardView.GetCellView(destinationIndex);
                    if (destView != null)
                    {
                        destView.SetHideItemForDrag(true);
                    }

                    boardView.RefreshCell(sourceIndex);
                    boardView.RefreshCell(destinationIndex);
                }
                else
                {
                    var sourceView = boardView.GetCellView(sourceIndex);
                    if (sourceView != null)
                    {
                        sourceView.SetHideItemForDrag(true);
                    }

                    boardView.RefreshCell(sourceIndex);
                }

                boardView.RefreshCell(lockedTargetIndex);
            }

            UpdateDebug();
            return result;
        }

        bool IsUnlockDestinationCandidate(int index)
        {
            if (_state == null || !_state.IsValidIndex(index))
            {
                return false;
            }

            var cell = _state.GetCell(index);
            return cell != null && cell.IsEmpty && !IsCellInteractionLocked(index);
        }

        Vector2 GetCellUiCenter(int index)
        {
            if (boardView == null || dragView == null)
            {
                return Vector2.zero;
            }

            var cell = boardView.GetCellView(index);
            if (cell == null)
            {
                return Vector2.zero;
            }

            var rect = cell.ItemImage != null
                ? cell.ItemImage.rectTransform
                : cell.transform as RectTransform;
            return dragView.WorldToLayer(rect.TransformPoint(rect.rect.center));
        }

        public MergeResult TryMerge(int fromIndex, int toIndex)
        {
            var result = new MergeResult
            {
                Success = false,
                SourceIndex = fromIndex,
                TargetIndex = toIndex,
                ResultingItemId = BoardCellState.EmptyItemId
            };

            if (!TryGetMergeResultItemId(fromIndex, toIndex, out var nextItemId))
            {
                return result;
            }

            var source = _state.GetCell(fromIndex);
            var target = _state.GetCell(toIndex);
            var sourceInstanceId = source.GeneratorInstanceId;
            var targetInstanceId = target.GeneratorInstanceId;
            var isGeneratorMerge = itemDatabase.TryGetById(nextItemId, out var mergedItem) &&
                                   mergedItem != null &&
                                   mergedItem.Kind == MergeItemKind.Generator;

            source.Clear();
            target.SetItem(nextItemId, locked: false);
            if (isGeneratorMerge)
            {
                RemoveGeneratorInstance(sourceInstanceId);
                RemoveGeneratorInstance(targetInstanceId);
                InitializeFullGeneratorOnCell(target, nextItemId);
            }
            else
            {
                RemoveGeneratorInstance(sourceInstanceId);
                RemoveGeneratorInstance(targetInstanceId);
                target.GeneratorInstanceId = BoardCellState.NoGeneratorInstanceId;
            }
            result.Success = true;
            result.ResultingItemId = nextItemId;
            result.NewlyDiscovered = _discovery != null && _discovery.Discover(nextItemId);
            if (result.NewlyDiscovered)
            {
                ItemDiscovered?.Invoke(nextItemId);
            }

            if (boardView != null)
            {
                var sourceView = boardView.GetCellView(fromIndex);
                if (sourceView != null)
                {
                    // Drag began with presentation suppressed; merge flight owns visuals now.
                    // Must clear suppressed or the empty source cell stays poisoned for future spawns.
                    sourceView.SetItemPresentationSuppressed(false);
                    sourceView.SetHideItemForDrag(false);
                }

                boardView.RefreshCell(fromIndex);
            }

            UpdateDebug();
            MergeCommitted?.Invoke(toIndex, nextItemId);
            RevealAdjacentBoxes(toIndex);
            return result;
        }

        public IReadOnlyList<BoxRevealResult> RevealAdjacentBoxes(int mergeResultIndex)
        {
            _boxRevealScratch.Clear();
            if (_state == null || !_state.IsValidIndex(mergeResultIndex))
            {
                return _boxRevealScratch;
            }

            _state.GetOrthogonalNeighborIndices(mergeResultIndex, _orthogonalScratch);
            for (var i = 0; i < _orthogonalScratch.Count; i++)
            {
                var index = _orthogonalScratch[i];
                var cell = _state.GetCell(index);
                if (cell == null || !cell.IsBox)
                {
                    continue;
                }

                var revealedId = cell.RevealBox();
                if (revealedId != BoardCellState.EmptyItemId)
                {
                    if (itemDatabase != null &&
                        itemDatabase.TryGetById(revealedId, out var revealedItem) &&
                        revealedItem != null &&
                        revealedItem.Kind == MergeItemKind.Generator)
                    {
                        InitializeFullGeneratorOnCell(cell, revealedId);
                    }

                    if (_discovery != null && _discovery.Discover(revealedId))
                    {
                        ItemDiscovered?.Invoke(revealedId);
                    }
                }

                _boxRevealScratch.Add(new BoxRevealResult
                {
                    CellIndex = index,
                    RevealedItemId = revealedId
                });
            }

            if (_boxRevealScratch.Count > 0)
            {
                EnsureBoxReady();
                boxRevealPresenter?.Play(_boxRevealScratch);
            }

            return _boxRevealScratch;
        }

        public void ShowMergedItemInfo(MergeItemData data)
        {
            if (itemInfoView == null || data == null)
            {
                return;
            }

            itemInfoView.Configure(itemDatabase, _discovery);
            itemInfoView.Show(data);
        }

        public void NotifyMergeImpact()
        {
            MergeImpact?.Invoke();
        }

        public void NotifyNewItemAppeared()
        {
            NewItemAppeared?.Invoke();
        }

        public void NotifyBoxBreak()
        {
            BoxBreak?.Invoke();
        }

        public void NotifyBoxItemRevealed()
        {
            BoxItemRevealed?.Invoke();
        }

        public void NotifyGeneratorProduced()
        {
            GeneratorProduced?.Invoke();
        }

        public bool IsGeneratorCell(int index)
        {
            if (_state == null || itemDatabase == null || !_state.IsValidIndex(index))
            {
                return false;
            }

            var cell = _state.GetCell(index);
            if (cell == null || !cell.HasItem || cell.IsBox || cell.ItemLocked)
            {
                return false;
            }

            return itemDatabase.TryGetById(cell.ItemId, out var data) &&
                   data != null &&
                   data.Kind == MergeItemKind.Generator;
        }

        public GeneratorSpawnResult TryActivateGenerator(int generatorIndex, Vector2 pointerScreenPosition)
        {
            var result = TrySpawnGeneratorItem(generatorIndex);
            if (!result.Success)
            {
                if (result.BoardFull)
                {
                    EnsureMessagesReady();
                    messagePresenter?.ShowBoardFull(pointerScreenPosition);
                }
                else if (result.Recharging)
                {
                    EnsureMessagesReady();
                    messagePresenter?.ShowGeneratorRecharging(pointerScreenPosition);
                }

                return result;
            }

            EnsureGeneratorReady();
            generatorPresenter?.Play(result);
            NotifyGeneratorProduced();
            if (itemDatabase != null &&
                itemDatabase.TryGetById(result.GeneratedItemId, out var outputItem) &&
                outputItem != null &&
                outputItem.Level >= 3)
            {
                GeneratorRareDrop?.Invoke(generatorIndex, result.GeneratedItemId);
            }

            return result;
        }

        public GeneratorSpawnResult TrySpawnGeneratorItem(int generatorIndex)
        {
            if (_state == null || itemDatabase == null || !_state.IsValidIndex(generatorIndex))
            {
                return GeneratorSpawnResult.Failed(generatorIndex);
            }

            if (IsCellInteractionLocked(generatorIndex))
            {
                return GeneratorSpawnResult.Failed(generatorIndex);
            }

            var generatorCell = _state.GetCell(generatorIndex);
            if (generatorCell == null || !generatorCell.HasItem || generatorCell.IsBox || generatorCell.ItemLocked)
            {
                return GeneratorSpawnResult.Failed(generatorIndex);
            }

            if (!itemDatabase.TryGetById(generatorCell.ItemId, out var generatorData) ||
                generatorData == null ||
                generatorData.Kind != MergeItemKind.Generator)
            {
                return GeneratorSpawnResult.Failed(generatorIndex);
            }

            EnsureGeneratorProductionDatabase();
            if (generatorProductionDatabase == null ||
                !generatorProductionDatabase.TryGetGeneratorData(generatorCell.ItemId, out var productionData))
            {
                Debug.LogWarning($"[Generator] No production configured for '{generatorData.InternalKey}'.");
                return GeneratorSpawnResult.Failed(generatorIndex);
            }

            if (!_generatorInstances.TryGetRuntime(generatorCell.GeneratorInstanceId, out var runtime))
            {
                InitializeFullGeneratorOnCell(generatorCell, generatorCell.ItemId);
                if (!_generatorInstances.TryGetRuntime(generatorCell.GeneratorInstanceId, out runtime))
                {
                    return GeneratorSpawnResult.Failed(generatorIndex);
                }
            }

            var now = _gameTimeProvider.UnixTimeNow;
            var cooldownSeconds = GetEffectiveCooldownSeconds(productionData);
            _generatorInstances.ResolveCooldown(runtime, productionData, now);
            if (_generatorInstances.IsOnCooldown(runtime, now))
            {
                return GeneratorSpawnResult.Failed(generatorIndex, recharging: true);
            }

            if (runtime.AvailableDrops <= 0)
            {
                return GeneratorSpawnResult.Failed(generatorIndex, recharging: true);
            }

            var spawnIndex = FindGeneratorSpawnCell(generatorIndex);
            if (spawnIndex == NoSelectionIndex)
            {
                return GeneratorSpawnResult.Failed(generatorIndex, boardFull: true);
            }

            if (!generatorProductionDatabase.TryRollOutputItemId(generatorCell.ItemId, _generatorRandom, out var outputItemId) ||
                outputItemId == BoardCellState.EmptyItemId)
            {
                Debug.LogWarning($"[Generator] Failed to roll output for '{generatorData.InternalKey}'.");
                return GeneratorSpawnResult.Failed(generatorIndex);
            }

            var lockToken = _interactionLocks.Acquire(generatorIndex, spawnIndex);
            var spawnCell = _state.GetCell(spawnIndex);
            if (spawnCell == null || !spawnCell.IsEmpty)
            {
                _interactionLocks.Release(lockToken);
                return GeneratorSpawnResult.Failed(generatorIndex, boardFull: true);
            }

            spawnCell.SetItem(outputItemId, locked: false);
            _generatorInstances.OnDropConsumed(runtime, productionData, now, cooldownSeconds);
            var newlyDiscovered = _discovery != null && _discovery.Discover(outputItemId);
            if (newlyDiscovered)
            {
                ItemDiscovered?.Invoke(outputItemId);
            }

            // Keep spawn cell locked until presentation handoff. Unlock generator immediately
            // so another tap can start a parallel flight.
            _interactionLocks.ReleaseCell(lockToken, generatorIndex);

            // Do NOT RefreshCell here — BoardState owns the item, GeneratorFlightView owns visuals
            // until BoardGeneratorPresenter hands off.
            UpdateDebug();
            return new GeneratorSpawnResult
            {
                Success = true,
                BoardFull = false,
                Recharging = false,
                GeneratorIndex = generatorIndex,
                SpawnCellIndex = spawnIndex,
                GeneratedItemId = outputItemId,
                InteractionLockToken = lockToken
            };
        }

        public void ReleaseGeneratorSpawnLock(int token)
        {
            if (token != 0)
            {
                _interactionLocks.Release(token);
            }
        }

        public int FindGeneratorSpawnCell(int generatorIndex)
        {
            if (_state == null || !_state.IsValidIndex(generatorIndex) || boardView == null)
            {
                return NoSelectionIndex;
            }

            var origin = GetCellUiCenter(generatorIndex);
            _state.GetOrthogonalNeighborIndices(generatorIndex, _orthogonalScratch);
            var bestOrthogonal = NoSelectionIndex;
            var bestOrthogonalDistance = float.MaxValue;
            for (var i = 0; i < _orthogonalScratch.Count; i++)
            {
                var index = _orthogonalScratch[i];
                if (!IsGeneratorSpawnCandidate(index))
                {
                    continue;
                }

                var distance = Vector2.Distance(origin, GetCellUiCenter(index));
                if (distance < bestOrthogonalDistance)
                {
                    bestOrthogonalDistance = distance;
                    bestOrthogonal = index;
                }
            }

            if (bestOrthogonal != NoSelectionIndex)
            {
                return bestOrthogonal;
            }

            var bestIndex = NoSelectionIndex;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < BoardState.CellCount; i++)
            {
                if (i == generatorIndex || !IsGeneratorSpawnCandidate(i))
                {
                    continue;
                }

                var distance = Vector2.Distance(origin, GetCellUiCenter(i));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        bool IsGeneratorSpawnCandidate(int index)
        {
            if (_state == null || !_state.IsValidIndex(index) || IsCellInteractionLocked(index))
            {
                return false;
            }

            var cell = _state.GetCell(index);
            return cell != null && cell.IsEmpty && !cell.IsBox;
        }

        public void PrepareDragPresentation(int sourceIndex)
        {
            if (selectionView != null)
            {
                selectionView.Hide();
            }

            _selectionRevision++;

            if (_state == null || !_state.IsValidIndex(sourceIndex))
            {
                return;
            }

            var cell = _state.GetCell(sourceIndex);
            if (cell == null || !cell.HasItem)
            {
                return;
            }

            _selectedCellIndex = sourceIndex;
            if (itemDatabase != null && itemDatabase.TryGetById(cell.ItemId, out var data))
            {
                ShowItemInfo(data);
            }

            UpdateDebug();
        }

        public void SetDragAnimationConfig(BoardDragAnimationConfig animation)
        {
            dragAnimationConfig = animation;
        }

        public void SetMergeAnimationConfig(BoardMergeAnimationConfig animation)
        {
            mergeAnimationConfig = animation;
        }

        public void SetCobwebAnimationConfig(BoardCobwebAnimationConfig animation)
        {
            cobwebAnimationConfig = animation;
        }

        public void SetBoxAnimationConfig(BoardBoxAnimationConfig animation)
        {
            boxAnimationConfig = animation;
        }

        public void SetGeneratorAnimationConfig(BoardGeneratorAnimationConfig animation)
        {
            generatorAnimationConfig = animation;
        }

        public void SetGeneratorProductionDatabase(GeneratorProductionDatabase database)
        {
            generatorProductionDatabase = database;
        }

        public void SetMessagePresenter(MessagePresenter presenter)
        {
            messagePresenter = presenter;
        }

        public void SetDragView(BoardDragView view)
        {
            dragView = view;
        }

        public void SetSelectionView(BoardSelectionView view)
        {
            selectionView = view;
        }

        public void SetItemInfoView(ItemInfoView view)
        {
            itemInfoView = view;
        }

        void BindAndRefresh()
        {
            boardView.Bind(_state, itemDatabase, visualConfig);
            SyncGeneratorInstancesFromBoard();
            boardView.RefreshAll();
            EnsureGeneratorCooldownPresenter();
            if (_selectedCellIndex != NoSelectionIndex)
            {
                var selected = _state.GetCell(_selectedCellIndex);
                if (selected == null || !selected.HasItem)
                {
                    ClearSelection();
                }
                else if (selectionView != null)
                {
                    selectionView.ShowOn(boardView.GetCellView(_selectedCellIndex));
                }
            }
        }

        void UpdateDebug()
        {
            debugSelectedCellIndex = _selectedCellIndex;
            debugSelectedItemId = BoardCellState.EmptyItemId;
            debugSelectedInternalKey = string.Empty;
            debugDiscoveredCount = _discovery != null ? _discovery.DiscoveredCount : 0;

            var selected = GetSelectedCell();
            if (selected == null || !selected.HasItem)
            {
                return;
            }

            debugSelectedItemId = selected.ItemId;
            if (itemDatabase != null && itemDatabase.TryGetById(selected.ItemId, out var data))
            {
                debugSelectedInternalKey = data.InternalKey;
            }
        }

        void ShowItemInfo(MergeItemData data)
        {
            if (itemInfoView == null || data == null)
            {
                return;
            }

            itemInfoView.Configure(itemDatabase, _discovery);
            if (!itemInfoView.IsShowing(data.Id))
            {
                itemInfoView.Show(data);
            }
        }

        void EnsureDrag()
        {
            _dragController = GetComponent<BoardDragController>();
            if (_dragController == null)
            {
                _dragController = gameObject.AddComponent<BoardDragController>();
            }

            var canvas = boardRoot != null ? boardRoot.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            if (dragView == null)
            {
                dragView = BoardDragView.Ensure(canvas);
            }
            else
            {
                dragView.EnsureChildren();
            }

            if (dragAnimationConfig == null)
            {
                dragAnimationConfig = ScriptableObject.CreateInstance<BoardDragAnimationConfig>();
            }

            _dragController.Configure(this, dragAnimationConfig, dragView);
            EnsureMerge();
            EnsureCobweb();
            EnsureBox();
            EnsureGenerator();
            EnsureDisplace();
            EnsureMessagesReady();
        }

        void EnsureDisplace()
        {
            displacePresenter = GetComponent<BoardDisplacePresenter>();
            if (displacePresenter == null)
            {
                displacePresenter = gameObject.AddComponent<BoardDisplacePresenter>();
            }

            displacePresenter.Configure(this, dragView, dragAnimationConfig);
        }

        public void EnsureDisplaceReady()
        {
            EnsureDisplace();
        }

        void EnsureMerge()
        {
            if (mergeAnimationConfig == null)
            {
                mergeAnimationConfig = ScriptableObject.CreateInstance<BoardMergeAnimationConfig>();
            }

            mergePresenter = GetComponent<BoardMergePresenter>();
            if (mergePresenter == null)
            {
                mergePresenter = gameObject.AddComponent<BoardMergePresenter>();
            }

            mergePresenter.Configure(this, dragView, mergeAnimationConfig);
        }

        public void EnsureCobwebReady()
        {
            EnsureCobweb();
        }

        void EnsureCobweb()
        {
            if (cobwebAnimationConfig == null)
            {
                cobwebAnimationConfig = ScriptableObject.CreateInstance<BoardCobwebAnimationConfig>();
            }

            cobwebPresenter = GetComponent<BoardCobwebPresenter>();
            if (cobwebPresenter == null)
            {
                cobwebPresenter = gameObject.AddComponent<BoardCobwebPresenter>();
            }

            cobwebPresenter.Configure(this, dragView, cobwebAnimationConfig, dragAnimationConfig);
        }

        public void EnsureBoxReady()
        {
            EnsureBox();
        }

        void EnsureBox()
        {
            if (boxAnimationConfig == null)
            {
                boxAnimationConfig = ScriptableObject.CreateInstance<BoardBoxAnimationConfig>();
            }

            boxRevealPresenter = GetComponent<BoardBoxRevealPresenter>();
            if (boxRevealPresenter == null)
            {
                boxRevealPresenter = gameObject.AddComponent<BoardBoxRevealPresenter>();
            }

            boxRevealPresenter.Configure(this, boxAnimationConfig);
        }

        public void EnsureGeneratorReady()
        {
            EnsureGenerator();
        }

        void EnsureGenerator()
        {
            if (generatorAnimationConfig == null)
            {
                generatorAnimationConfig = ScriptableObject.CreateInstance<BoardGeneratorAnimationConfig>();
            }

            EnsureGeneratorProductionDatabase();

            generatorPresenter = GetComponent<BoardGeneratorPresenter>();
            if (generatorPresenter == null)
            {
                generatorPresenter = gameObject.AddComponent<BoardGeneratorPresenter>();
            }

            generatorPresenter.Configure(
                this,
                dragView,
                generatorAnimationConfig,
                mergeAnimationConfig);
            EnsureGeneratorCooldownPresenter();
        }

        void EnsureGeneratorCooldownPresenter()
        {
            if (boardView == null)
            {
                return;
            }

            _generatorCooldownPresenter = GetComponent<BoardGeneratorCooldownPresenter>();
            if (_generatorCooldownPresenter == null)
            {
                _generatorCooldownPresenter = gameObject.AddComponent<BoardGeneratorCooldownPresenter>();
            }

            _generatorCooldownPresenter.Configure(
                this,
                boardView,
                _generatorInstances,
                generatorProductionDatabase,
                itemDatabase,
                _gameTimeProvider);
        }

        public float GetEffectiveCooldownSeconds(GeneratorData definition)
        {
            if (definition == null)
            {
                return 60f;
            }

            if (useDebugCooldownOverride)
            {
                return debugCooldownSeconds;
            }

            return definition.CooldownSeconds;
        }

        public int GetAvailableDrops(int cellIndex)
        {
            return TryGetGeneratorRuntime(cellIndex, out var runtime, out _) ? runtime.AvailableDrops : 0;
        }

        public int GetCapacityDrops(int cellIndex)
        {
            if (!TryGetGeneratorRuntime(cellIndex, out _, out var productionData))
            {
                return 0;
            }

            return productionData.CapacityDrops;
        }

        public float GetCooldownRemaining(int cellIndex)
        {
            if (!TryGetGeneratorRuntime(cellIndex, out var runtime, out var productionData))
            {
                return 0f;
            }

            var now = _gameTimeProvider.UnixTimeNow;
            _generatorInstances.ResolveCooldown(runtime, productionData, now);
            return _generatorInstances.GetCooldownRemaining(runtime, now);
        }

        public bool TryGetGeneratorPresentationInfo(int cellIndex, out GeneratorPresentationInfo info)
        {
            info = GeneratorPresentationInfo.Invalid;
            if (!TryGetGeneratorRuntime(cellIndex, out var runtime, out var productionData))
            {
                return false;
            }

            var now = _gameTimeProvider.UnixTimeNow;
            _generatorInstances.ResolveCooldown(runtime, productionData, now);
            info = new GeneratorPresentationInfo
            {
                IsValid = true,
                AvailableDrops = runtime.AvailableDrops,
                CapacityDrops = productionData.CapacityDrops,
                CooldownRemainingSeconds = _generatorInstances.GetCooldownRemaining(runtime, now)
            };
            return true;
        }

        bool TryGetGeneratorRuntime(int cellIndex, out GeneratorInstanceRuntime runtime, out GeneratorData productionData)
        {
            runtime = null;
            productionData = null;
            if (_state == null || !_state.IsValidIndex(cellIndex) || generatorProductionDatabase == null || itemDatabase == null)
            {
                return false;
            }

            var cell = _state.GetCell(cellIndex);
            if (cell == null || !cell.HasItem || cell.IsBox || cell.ItemLocked)
            {
                return false;
            }

            if (!itemDatabase.TryGetById(cell.ItemId, out var itemData) ||
                itemData == null ||
                itemData.Kind != MergeItemKind.Generator)
            {
                return false;
            }

            if (!generatorProductionDatabase.TryGetGeneratorData(cell.ItemId, out productionData))
            {
                return false;
            }

            if (!_generatorInstances.TryGetRuntime(cell.GeneratorInstanceId, out runtime))
            {
                return false;
            }

            return true;
        }

        void SyncGeneratorInstancesFromBoard()
        {
            if (_state == null || itemDatabase == null)
            {
                return;
            }

            EnsureGeneratorProductionDatabase();
            for (var i = 0; i < BoardState.CellCount; i++)
            {
                var cell = _state.GetCell(i);
                if (cell == null || !cell.HasItem || cell.IsBox)
                {
                    continue;
                }

                if (!itemDatabase.TryGetById(cell.ItemId, out var itemData) ||
                    itemData == null ||
                    itemData.Kind != MergeItemKind.Generator)
                {
                    cell.GeneratorInstanceId = BoardCellState.NoGeneratorInstanceId;
                    continue;
                }

                if (cell.GeneratorInstanceId == BoardCellState.NoGeneratorInstanceId ||
                    !_generatorInstances.TryGetRuntime(cell.GeneratorInstanceId, out _))
                {
                    InitializeFullGeneratorOnCell(cell, cell.ItemId);
                }
            }
        }

        void InitializeFullGeneratorOnCell(BoardCellState cell, int generatorItemId)
        {
            if (cell == null || generatorProductionDatabase == null)
            {
                return;
            }

            RemoveGeneratorInstance(cell.GeneratorInstanceId);
            if (!generatorProductionDatabase.TryGetGeneratorData(generatorItemId, out var productionData))
            {
                cell.GeneratorInstanceId = BoardCellState.NoGeneratorInstanceId;
                return;
            }

            cell.GeneratorInstanceId = _generatorInstances.CreateFullInstance(generatorItemId, productionData.CapacityDrops);
        }

        void RemoveGeneratorInstance(int instanceId)
        {
            _generatorInstances.RemoveInstance(instanceId);
        }

        void TransferItemInstanceState(BoardCellState from, BoardCellState to)
        {
            if (from == null || to == null)
            {
                return;
            }

            to.GeneratorInstanceId = from.GeneratorInstanceId;
            from.GeneratorInstanceId = BoardCellState.NoGeneratorInstanceId;
        }

        void EnsureGeneratorProductionDatabase()
        {
            if (generatorProductionDatabase == null)
            {
                generatorProductionDatabase = ScriptableObject.CreateInstance<GeneratorProductionDatabase>();
            }
        }

        public void EnsureMessagesReady()
        {
            if (generatorAnimationConfig == null)
            {
                generatorAnimationConfig = ScriptableObject.CreateInstance<BoardGeneratorAnimationConfig>();
            }

            if (messagePresenter == null)
            {
                messagePresenter = FindAnyObjectByType<MessagePresenter>();
            }

            if (messagePresenter == null)
            {
                var canvas = boardRoot != null ? boardRoot.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    var layer = canvas.transform.Find("Messages Layer") as RectTransform;
                    if (layer == null)
                    {
                        for (var i = 0; i < canvas.transform.childCount; i++)
                        {
                            var child = canvas.transform.GetChild(i) as RectTransform;
                            if (child != null && child.name == "Messages Layer")
                            {
                                layer = child;
                                break;
                            }
                        }
                    }

                    if (layer != null)
                    {
                        messagePresenter = layer.GetComponent<MessagePresenter>();
                        if (messagePresenter == null)
                        {
                            messagePresenter = layer.gameObject.AddComponent<MessagePresenter>();
                        }

                        var white = layer.Find("ToastMessage_White") as RectTransform;
                        var rose = layer.Find("ToastMessage_Rose") as RectTransform;
                        messagePresenter.Configure(layer, white, rose, generatorAnimationConfig, canvas);
                    }
                }
            }
            else
            {
                messagePresenter.SetConfig(generatorAnimationConfig);
            }
        }

        bool ValidateDependencies()
        {
            if (boardView == null)
            {
                Debug.LogError("[BoardController] BoardView is not assigned.");
                return false;
            }

            if (itemDatabase == null)
            {
                Debug.LogError("[BoardController] MergeItemDatabase is not assigned.");
                return false;
            }

            if (visualConfig == null)
            {
                Debug.LogError("[BoardController] BoardVisualConfig is not assigned.");
                return false;
            }

            return true;
        }
    }
}
