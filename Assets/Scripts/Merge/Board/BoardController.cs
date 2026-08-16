using System;
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
        [SerializeField] BoardDragView dragView;
        [SerializeField] BoardMergePresenter mergePresenter;
        [SerializeField] BoardCobwebPresenter cobwebPresenter;
        [SerializeField] BoardSelectionView selectionView;
        [SerializeField] ItemInfoView itemInfoView;
        [SerializeField] bool useDevelopmentBoardState = true;

        [Header("Debug")]
        [SerializeField] int debugSelectedCellIndex = NoSelectionIndex;
        [SerializeField] int debugSelectedItemId = BoardCellState.EmptyItemId;
        [SerializeField] string debugSelectedInternalKey;
        [SerializeField] int debugDiscoveredCount;

        BoardState _state;
        MergeDiscoveryState _discovery;
        readonly BoardInteractionLockService _interactionLocks = new BoardInteractionLockService();
        int _selectedCellIndex = NoSelectionIndex;
        int _selectionRevision;
        BoardDragController _dragController;

        public RectTransform BoardRoot => boardRoot;
        public BoardView BoardView => boardView;
        public MergeItemDatabase ItemDatabase => itemDatabase;
        public BoardVisualConfig VisualConfig => visualConfig;
        public BoardItemAnimationConfig AnimationConfig => animationConfig;
        public UiInteractionFeedbackConfig UiFeedbackConfig => uiFeedbackConfig;
        public BoardDragAnimationConfig DragAnimationConfig => dragAnimationConfig;
        public BoardMergeAnimationConfig MergeAnimationConfig => mergeAnimationConfig;
        public BoardCobwebAnimationConfig CobwebAnimationConfig => cobwebAnimationConfig;
        public BoardDragView DragView => dragView;
        public BoardMergePresenter MergePresenter => mergePresenter;
        public BoardCobwebPresenter CobwebPresenter => cobwebPresenter;
        public BoardDragController DragController => _dragController;
        public BoardSelectionView SelectionView => selectionView;
        public ItemInfoView ItemInfoView => itemInfoView;
        public BoardState State => _state;
        public MergeDiscoveryState Discovery => _discovery;
        public BoardInteractionLockService InteractionLocks => _interactionLocks;
        public bool UseDevelopmentBoardState => useDevelopmentBoardState;
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

            _interactionLocks.ReleaseAll();

            if (boardView != null)
            {
                boardView.ClearItemDragHides();
            }

            ClearSelection();
            _state = BoardDevelopmentStateFactory.Create(itemDatabase);
            _discovery = new MergeDiscoveryState();
            _discovery.DiscoverFromBoard(_state);
            BindAndRefresh();
            UpdateDebug();
            LogLockedCellsForDebug();
        }

        void LogLockedCellsForDebug()
        {
            if (_state == null)
            {
                return;
            }

            var locked = 0;
            for (var i = 0; i < BoardState.CellCount; i++)
            {
                var cell = _state.GetCell(i);
                if (cell == null || !cell.ItemLocked)
                {
                    continue;
                }

                locked++;
                _state.GetCoordinates(i, out var row, out var col);
                Debug.Log($"[Cobweb] board locked cell={i}[{row},{col}] id={cell.ItemId}");
            }

            Debug.Log($"[Cobweb] board locked total={locked}");
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
            source.Clear();
            if (boardView != null)
            {
                boardView.RefreshCell(fromIndex);
                boardView.RefreshCell(toIndex);
            }

            UpdateDebug();
            return true;
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
                Debug.Log(
                    $"[Cobweb] CanUnlock=false locked cells from={fromIndex}({IsCellInteractionLocked(fromIndex)}) " +
                    $"to={lockedTargetIndex}({IsCellInteractionLocked(lockedTargetIndex)})");
                return false;
            }

            return TryValidateUnlock(fromIndex, lockedTargetIndex, out _);
        }

        public bool TryValidateUnlock(int fromIndex, int lockedTargetIndex, out int itemId)
        {
            itemId = BoardCellState.EmptyItemId;
            if (_state == null || fromIndex == lockedTargetIndex)
            {
                Debug.Log($"[Cobweb] Validate=false stateNull={_state == null} sameIndex={fromIndex == lockedTargetIndex} from={fromIndex} to={lockedTargetIndex}");
                return false;
            }

            if (!_state.IsValidIndex(fromIndex) || !_state.IsValidIndex(lockedTargetIndex))
            {
                Debug.Log($"[Cobweb] Validate=false invalidIndex from={fromIndex} to={lockedTargetIndex}");
                return false;
            }

            var source = _state.GetCell(fromIndex);
            var target = _state.GetCell(lockedTargetIndex);
            if (source == null || target == null || !source.HasItem || !target.HasItem)
            {
                Debug.Log(
                    $"[Cobweb] Validate=false missing items sourceNull={source == null} targetNull={target == null} " +
                    $"sourceHas={source != null && source.HasItem} targetHas={target != null && target.HasItem}");
                return false;
            }

            if (source.IsBox || target.IsBox || source.ItemLocked || !target.ItemLocked)
            {
                Debug.Log(
                    $"[Cobweb] Validate=false box/lock sourceBox={source.IsBox} targetBox={target.IsBox} " +
                    $"sourceLocked={source.ItemLocked} targetLocked={target.ItemLocked} " +
                    $"sourceId={source.ItemId} targetId={target.ItemId}");
                return false;
            }

            if (source.ItemId != target.ItemId)
            {
                Debug.Log($"[Cobweb] Validate=false id mismatch sourceId={source.ItemId} targetId={target.ItemId}");
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
                Debug.LogWarning($"[Cobweb] TryUnlock failed validation source={sourceIndex} target={lockedTargetIndex}");
                return result;
            }

            if (!_state.IsValidIndex(destinationIndex) || destinationIndex == lockedTargetIndex)
            {
                Debug.LogWarning($"[Cobweb] TryUnlock bad destination={destinationIndex} locked={lockedTargetIndex}");
                return result;
            }

            if (destinationIndex != sourceIndex)
            {
                // Destination may already be interaction-locked by this unlock sequence — only require empty.
                var destCell = _state.GetCell(destinationIndex);
                if (destCell == null || !destCell.IsEmpty)
                {
                    Debug.LogWarning($"[Cobweb] TryUnlock destination not empty dest={destinationIndex}");
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
                source.Clear();
                destination.SetItem(itemId, locked: false);
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
            source.Clear();
            target.SetItem(nextItemId, locked: false);
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
                    sourceView.SetHideItemForDrag(false);
                }

                boardView.RefreshCell(fromIndex);
            }

            UpdateDebug();
            return result;
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
            boardView.RefreshAll();
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
