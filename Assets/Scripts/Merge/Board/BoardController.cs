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
        [SerializeField] InitialBoardDefinition initialBoard;
        [SerializeField] bool useDevelopmentBoardState = true;

        [Header("Generator Debug")]
        [SerializeField] bool useDebugCooldownOverride;
        [SerializeField] float debugCooldownSeconds = 5f;
        [HideInInspector] [SerializeField] int debugGeneratorAvailableDrops;
        [HideInInspector] [SerializeField] int debugGeneratorMaxDrops;
        [HideInInspector] [SerializeField] float debugGeneratorRechargeProgress;
        [HideInInspector] [SerializeField] float debugGeneratorSecondsUntilNextCharge;

        [Header("Debug")]
        [SerializeField] int debugSelectedCellIndex = NoSelectionIndex;
        [SerializeField] int debugSelectedItemId = BoardCellState.EmptyItemId;
        [SerializeField] string debugSelectedInternalKey;
        [SerializeField] int debugDiscoveredCount;
        [SerializeField] int debugCurrentEnergy;
        [SerializeField] float debugSecondsUntilNextEnergy;

        BoardState _state;
        MergeDiscoveryState _discovery;
        readonly BoardInteractionLockService _interactionLocks = new BoardInteractionLockService();
        readonly List<int> _orthogonalScratch = new List<int>(4);
        readonly List<BoxRevealResult> _boxRevealScratch = new List<BoxRevealResult>(4);
        readonly bool[] _orderCellUsed = new bool[BoardState.CellCount];
        readonly List<ConsumedBoardItem> _orderCollectScratch = new List<ConsumedBoardItem>(8);
        readonly GeneratorInstanceService _generatorInstances = new GeneratorInstanceService();
        readonly GameTimeProvider _gameTimeProvider = new GameTimeProvider();
        readonly IGeneratorRandom _generatorRandom = new GeneratorRandomService();
        EnergySystem _energySystem;
        EnergyService _energy;
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
        public InitialBoardDefinition InitialBoard => initialBoard;
        public EnergyService Energy => _energy;
        public BoardState State => _state;
        public MergeDiscoveryState Discovery => _discovery;
        public BoardInteractionLockService InteractionLocks => _interactionLocks;
        public bool UseDevelopmentBoardState => useDevelopmentBoardState;
        public bool UseDebugCooldownOverride => useDebugCooldownOverride;
        public float DebugCooldownSeconds => debugCooldownSeconds;
        public int DebugGeneratorAvailableDrops => debugGeneratorAvailableDrops;
        public int DebugGeneratorMaxDrops => debugGeneratorMaxDrops;
        public float DebugGeneratorRechargeProgress => debugGeneratorRechargeProgress;
        public float DebugGeneratorSecondsUntilNextCharge => debugGeneratorSecondsUntilNextCharge;
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

        public string DebugDescribeCell(int index)
        {
            var row = -1;
            var col = -1;
            var stateText = "invalid";
            if (_state != null && _state.IsValidIndex(index))
            {
                _state.GetCoordinates(index, out row, out col);
                var cell = _state.GetCell(index);
                stateText = cell == null
                    ? "null"
                    : $"id={cell.ItemId} empty={cell.IsEmpty} box={cell.IsBox} itemLocked={cell.ItemLocked} block={cell.BlockType} concealed={cell.ConcealedItemId}";
            }

            var view = boardView != null ? boardView.GetCellView(index) : null;
            var viewText = view != null ? view.DebugDescribePresentation() : "view=null";
            var lockedCells = _interactionLocks.DebugDescribeLockedCells();
            return
                $"cell={index}[{row},{col}] interactionLocked={IsCellInteractionLocked(index)} {stateText} {viewText} lockedCells={lockedCells}";
        }

        public event Action<int> ItemDiscovered;
        public event Action MergeImpact;
        public event Action NewItemAppeared;
        public event Action<int, int> MergeCommitted;
        public event Action BoxBreak;
        public event Action BoxItemRevealed;
        public event Action GeneratorProduced;
        public event Action<int, int> GeneratorRareDrop;
        public event Action BoardContentsChanged;

        void Awake()
        {
            _state = null;
            _selectedCellIndex = NoSelectionIndex;
            _discovery = new MergeDiscoveryState();
            EnsureEnergySystem();
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
            EnsureEnergySystem();
            EnsureIcons();
            EnsureSell();
            EnsureOrders();

            if (useDevelopmentBoardState)
            {
                ResetDevelopmentBoard();
            }
            else
            {
                LoadInitialBoard();
            }
        }

        void LateUpdate()
        {
            if (_selectedCellIndex == NoSelectionIndex)
            {
                return;
            }

            var selected = GetSelectedCell();
            if (selected == null || !selected.HasItem || itemDatabase == null)
            {
                return;
            }

            if (itemDatabase.TryGetById(selected.ItemId, out var data) &&
                data != null &&
                data.Kind == MergeItemKind.Generator)
            {
                UpdateDebug();
            }
        }

        public void ResetDevelopmentBoard()
        {
            if (!ValidateDependencies())
            {
                return;
            }

            AbortBoardActivity();
            ClearSelection();
            _state = BoardDevelopmentStateFactory.Create(itemDatabase);
            _discovery = new MergeDiscoveryState();
            _generatorInstances.Clear();
            _discovery.DiscoverFromBoard(_state);
            SyncGeneratorInstancesFromBoard();
            BindAndRefresh();
            GetComponent<OrderSystem>()?.ResetDevelopment();
            UpdateDebug();
            BoardLayoutValidator.Validate(_state, itemDatabase);
            NotifyBoardContentsChanged();
        }

        public void LoadInitialBoard()
        {
            if (!ValidateDependencies())
            {
                return;
            }

            AbortBoardActivity();
            ClearSelection();
            if (initialBoard == null)
            {
                Debug.LogError("[BoardController] InitialBoardDefinition is not assigned.");
                _state = new BoardState();
            }
            else
            {
                _state = InitialBoardLoader.CreateNewGameBoard(initialBoard, itemDatabase);
            }

            _discovery = new MergeDiscoveryState();
            _generatorInstances.Clear();
            _discovery.DiscoverFromBoard(_state);
            SyncGeneratorInstancesFromBoard();
            BindAndRefresh();
            UpdateDebug();
            BoardLayoutValidator.Validate(_state, itemDatabase);
            NotifyBoardContentsChanged();
        }

        public void SetInitialBoard(InitialBoardDefinition definition)
        {
            initialBoard = definition;
        }

        void AbortBoardActivity()
        {
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

            var sellPresenter = GetComponent<BoardSellPresenter>();
            sellPresenter?.AbortAll();
            var sellSystem = GetComponent<SellSystem>();
            sellSystem?.ClearLastSale();
            var orderPresenter = GetComponent<BoardOrderPresenter>();
            orderPresenter?.AbortAll();
            _interactionLocks.ReleaseAll();
            if (boardView != null)
            {
                boardView.ClearItemDragHides();
            }
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
                ShowItemInfo(data, allowSell: data.Kind == MergeItemKind.Normal && CanSellSelected());
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

        public void InspectItem(int itemId)
        {
            if (itemDatabase == null || !itemDatabase.TryGetById(itemId, out var data) || data == null)
            {
                return;
            }

            _selectedCellIndex = NoSelectionIndex;
            _selectionRevision++;
            if (selectionView != null)
            {
                selectionView.Hide();
            }

            ShowItemInfo(data, allowSell: false, force: true);
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

        public bool CanSellSelected()
        {
            var economy = SellSystem.Current != null ? SellSystem.Current.Config : null;
            return TryGetSellableSelection(economy, _selectedCellIndex, out _, out _, out _);
        }

        public long GetSelectedSellPrice(EconomyConfig economy)
        {
            if (!TryGetSellableSelection(economy, _selectedCellIndex, out _, out var price, out _))
            {
                return 0;
            }

            return price;
        }

        public long GetSellPriceForCell(int cellIndex, EconomyConfig economy, bool ignoreDragBusy = false)
        {
            if (!TryGetSellableSelection(economy, cellIndex, out _, out var price, out _, ignoreDragBusy))
            {
                return 0;
            }

            return price;
        }

        public bool TrySellSelected(EconomyConfig economy, out SoldItemSnapshot snapshot, out Sprite sprite, out Vector2 size)
        {
            snapshot = default;
            sprite = null;
            size = Vector2.zero;
            if (!TryGetSellableSelection(economy, _selectedCellIndex, out var index, out var price, out var data))
            {
                return false;
            }

            var view = boardView != null ? boardView.GetCellView(index) : null;
            if (view != null && _dragController != null)
            {
                _dragController.PrepareCellForPossiblePickup(view);
            }

            if (!TryGetSellableSelection(economy, _selectedCellIndex, out index, out price, out data))
            {
                return false;
            }

            view = boardView != null ? boardView.GetCellView(index) : null;
            if (view != null && view.ItemImage != null)
            {
                sprite = view.ItemImage.sprite;
                var rectSize = view.ItemImage.rectTransform.rect.size;
                size = rectSize.x > 1f && rectSize.y > 1f ? rectSize : new Vector2(170f, 170f);
            }

            var cell = _state.GetCell(index);
            snapshot = new SoldItemSnapshot
            {
                ItemId = cell.ItemId,
                OriginalCellIndex = index,
                SalePrice = price,
                Level = data.Level,
                ItemLocked = cell.ItemLocked,
                GeneratorInstanceId = cell.GeneratorInstanceId
            };

            cell.Clear();
            if (boardView != null)
            {
                boardView.RefreshCell(index);
            }

            ClearSelection();
            UpdateDebug();
            NotifyBoardContentsChanged();
            return true;
        }

        public bool TryRestoreSoldItem(SoldItemSnapshot snapshot, out int placedIndex)
        {
            placedIndex = NoSelectionIndex;
            if (_state == null || snapshot.ItemId == BoardCellState.EmptyItemId)
            {
                return false;
            }

            placedIndex = FindRestoreCell(snapshot.OriginalCellIndex);
            if (placedIndex == NoSelectionIndex)
            {
                return false;
            }

            var cell = _state.GetCell(placedIndex);
            if (cell == null || !cell.IsEmpty)
            {
                placedIndex = NoSelectionIndex;
                return false;
            }

            cell.SetItem(snapshot.ItemId, snapshot.ItemLocked);
            cell.GeneratorInstanceId = snapshot.GeneratorInstanceId;
            if (boardView != null)
            {
                boardView.RefreshCell(placedIndex);
            }

            SelectCell(placedIndex);
            UpdateDebug();
            NotifyBoardContentsChanged();
            return true;
        }

        public int FindRestoreCell(int originIndex)
        {
            if (IsEmptyOpenCell(originIndex))
            {
                return originIndex;
            }

            if (_state == null)
            {
                return NoSelectionIndex;
            }

            var origin = originIndex;
            if (!_state.IsValidIndex(origin))
            {
                origin = 0;
            }

            var originPos = GetCellUiCenter(origin);
            var bestIndex = NoSelectionIndex;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < BoardState.CellCount; i++)
            {
                if (i == origin || !IsEmptyOpenCell(i))
                {
                    continue;
                }

                var distance = Vector2.Distance(originPos, GetCellUiCenter(i));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        public Vector2 GetCellScreenPosition(int index)
        {
            if (boardView == null)
            {
                return Vector2.zero;
            }

            var cell = boardView.GetCellView(index);
            if (cell == null)
            {
                return Vector2.zero;
            }

            var rect = cell.transform as RectTransform;
            if (rect == null)
            {
                return Vector2.zero;
            }

            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }

        bool TryGetSellableSelection(EconomyConfig economy, int cellIndex, out int index, out long price, out MergeItemData data, bool ignoreDragBusy = false)
        {
            index = NoSelectionIndex;
            price = 0;
            data = null;
            if (_state == null || economy == null || !_state.IsValidIndex(cellIndex))
            {
                return false;
            }

            if (IsCellInteractionLocked(cellIndex))
            {
                return false;
            }

            if (!ignoreDragBusy && _dragController != null && _dragController.IsBusyWithCell(cellIndex))
            {
                return false;
            }

            var cell = _state.GetCell(cellIndex);
            if (cell == null || !cell.HasItem || cell.IsBox || cell.ItemLocked)
            {
                return false;
            }

            if (itemDatabase == null || !itemDatabase.TryGetById(cell.ItemId, out data) || data == null)
            {
                return false;
            }

            if (data.Kind != MergeItemKind.Normal)
            {
                return false;
            }

            if (!economy.TryGetSellPrice(data.Level, out price) || price <= 0)
            {
                return false;
            }

            index = cellIndex;
            return true;
        }

        public bool CanSellCell(int cellIndex, EconomyConfig economy, bool ignoreDragBusy = false)
        {
            return TryGetSellableSelection(economy, cellIndex, out _, out _, out _, ignoreDragBusy);
        }

        public bool TrySellCell(int cellIndex, EconomyConfig economy, out SoldItemSnapshot snapshot, out Sprite sprite, out Vector2 size, bool ignoreDragBusy = false)
        {
            snapshot = default;
            sprite = null;
            size = Vector2.zero;
            if (!TryGetSellableSelection(economy, cellIndex, out var index, out var price, out var data, ignoreDragBusy))
            {
                return false;
            }

            var view = boardView != null ? boardView.GetCellView(index) : null;
            if (view != null && view.ItemImage != null)
            {
                sprite = view.ItemImage.sprite;
                var rectSize = view.ItemImage.rectTransform.rect.size;
                size = rectSize.x > 1f && rectSize.y > 1f ? rectSize : new Vector2(170f, 170f);
            }

            var cell = _state.GetCell(index);
            snapshot = new SoldItemSnapshot
            {
                ItemId = cell.ItemId,
                OriginalCellIndex = index,
                SalePrice = price,
                Level = data.Level,
                ItemLocked = cell.ItemLocked,
                GeneratorInstanceId = cell.GeneratorInstanceId
            };

            cell.Clear();
            if (boardView != null)
            {
                boardView.RefreshCell(index);
            }

            if (_selectedCellIndex == index)
            {
                ClearSelection();
            }

            UpdateDebug();
            NotifyBoardContentsChanged();
            return true;
        }

        public int FindRandomEmptyOpenCell()
        {
            if (_state == null)
            {
                return NoSelectionIndex;
            }

            var count = 0;
            var first = NoSelectionIndex;
            for (var i = 0; i < BoardState.CellCount; i++)
            {
                if (!IsEmptyOpenCell(i))
                {
                    continue;
                }

                count++;
                if (first == NoSelectionIndex)
                {
                    first = i;
                }
            }

            if (count == 0)
            {
                return NoSelectionIndex;
            }

            if (count == 1)
            {
                return first;
            }

            var pick = UnityEngine.Random.Range(0, count);
            for (var i = 0; i < BoardState.CellCount; i++)
            {
                if (!IsEmptyOpenCell(i))
                {
                    continue;
                }

                if (pick == 0)
                {
                    return i;
                }

                pick--;
            }

            return first;
        }

        public bool TryPlaceInventoryItem(int itemId, int cellIndex, out int lockToken, int generatorInstanceId = BoardCellState.NoGeneratorInstanceId)
        {
            lockToken = 0;
            if (_state == null || !IsEmptyOpenCell(cellIndex) || itemId == BoardCellState.EmptyItemId)
            {
                return false;
            }

            if (itemDatabase == null || !itemDatabase.TryGetById(itemId, out var data) || data == null)
            {
                return false;
            }

            if (data.Kind != MergeItemKind.Normal && data.Kind != MergeItemKind.Generator)
            {
                return false;
            }

            var cell = _state.GetCell(cellIndex);
            if (cell == null || !cell.IsEmpty)
            {
                return false;
            }

            lockToken = _interactionLocks.Acquire(cellIndex, NoSelectionIndex);
            cell.SetItem(itemId, locked: false);
            cell.GeneratorInstanceId = generatorInstanceId;
            if (data.Kind == MergeItemKind.Generator)
            {
                if (cell.GeneratorInstanceId == BoardCellState.NoGeneratorInstanceId ||
                    !_generatorInstances.TryGetRuntime(cell.GeneratorInstanceId, out _))
                {
                    InitializeFullGeneratorOnCell(cell, itemId);
                }
            }

            if (_discovery != null && _discovery.Discover(itemId))
            {
                ItemDiscovered?.Invoke(itemId);
            }

            UpdateDebug();
            NotifyBoardContentsChanged();
            return true;
        }

        public bool TryExtractItem(int cellIndex, out int itemId, out int generatorInstanceId)
        {
            itemId = BoardCellState.EmptyItemId;
            generatorInstanceId = BoardCellState.NoGeneratorInstanceId;
            if (_state == null || !_state.IsValidIndex(cellIndex) || IsCellInteractionLocked(cellIndex))
            {
                return false;
            }

            var cell = _state.GetCell(cellIndex);
            if (cell == null || !cell.HasItem || cell.IsBox || cell.ItemLocked)
            {
                return false;
            }

            if (itemDatabase != null && itemDatabase.TryGetById(cell.ItemId, out var data) && data != null &&
                data.Kind != MergeItemKind.Normal && data.Kind != MergeItemKind.Generator)
            {
                return false;
            }

            itemId = cell.ItemId;
            generatorInstanceId = cell.GeneratorInstanceId;
            cell.GeneratorInstanceId = BoardCellState.NoGeneratorInstanceId;
            cell.Clear();
            if (boardView != null)
            {
                boardView.RefreshCell(cellIndex);
            }

            if (_selectedCellIndex == cellIndex)
            {
                ClearSelection();
            }

            UpdateDebug();
            NotifyBoardContentsChanged();
            return true;
        }

        bool IsEmptyOpenCell(int index)
        {
            if (_state == null || !_state.IsValidIndex(index) || IsCellInteractionLocked(index))
            {
                return false;
            }

            var cell = _state.GetCell(index);
            return cell != null && cell.IsEmpty && !cell.IsBox;
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
            NotifyBoardContentsChanged();
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
            NotifyBoardContentsChanged();
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
            return BoardProgressionRules.TryGetMergeResultItemId(_state, itemDatabase, fromIndex, toIndex, out resultItemId);
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
            return BoardProgressionRules.TryValidateCobwebUnlock(_state, fromIndex, lockedTargetIndex, out itemId);
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
            NotifyBoardContentsChanged();
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
            NotifyBoardContentsChanged();
            return result;
        }

        public IReadOnlyList<BoxRevealResult> RevealAdjacentBoxes(int mergeResultIndex)
        {
            BoardProgressionRules.RevealOrthogonalBoxes(_state, mergeResultIndex, _orthogonalScratch, _boxRevealScratch);
            for (var i = 0; i < _boxRevealScratch.Count; i++)
            {
                var revealedId = _boxRevealScratch[i].RevealedItemId;
                if (revealedId == BoardCellState.EmptyItemId)
                {
                    continue;
                }

                var cell = _state.GetCell(_boxRevealScratch[i].CellIndex);
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

            if (_boxRevealScratch.Count > 0)
            {
                EnsureBoxReady();
                boxRevealPresenter?.Play(_boxRevealScratch);
                NotifyBoardContentsChanged();
            }

            return _boxRevealScratch;
        }

        public void ShowMergedItemInfo(MergeItemData data)
        {
            ShowItemInfo(data, allowSell: CanSellSelected(), force: true);
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

                else if (result.InsufficientEnergy)
                {
                    EnsureMessagesReady();
                    messagePresenter?.ShowNotEnoughEnergy(pointerScreenPosition);
                    EnsureEnergySystem();
                    _energySystem?.NotifySpendRejected();
                }

                return result;
            }

            EnsureGeneratorReady();
            generatorPresenter?.Play(result);
            NotifyGeneratorProduced();
            NotifyBoardContentsChanged();
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
            var rechargeSeconds = GetEffectiveRechargeSeconds(productionData);
            _generatorInstances.ApplyRecharge(runtime, productionData, now, rechargeSeconds);
            if (runtime.AvailableDrops <= 0)
            {
                return GeneratorSpawnResult.Failed(generatorIndex, recharging: true);
            }

            if (_energy == null || !_energy.CanSpend(EnergyService.GeneratorProductionCost))
            {
                return GeneratorSpawnResult.Failed(generatorIndex, insufficientEnergy: true);
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

            if (_energy == null || !_energy.TrySpend(EnergyService.GeneratorProductionCost))
            {
                _interactionLocks.Release(lockToken);
                return GeneratorSpawnResult.Failed(generatorIndex, insufficientEnergy: true);
            }

            spawnCell.SetItem(outputItemId, locked: false);
            _generatorInstances.OnDropConsumed(runtime, productionData, now, rechargeSeconds);
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
            NotifyBoardContentsChanged();
            return new GeneratorSpawnResult
            {
                Success = true,
                BoardFull = false,
                Recharging = false,
                InsufficientEnergy = false,
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
                ShowItemInfo(data, allowSell: data.Kind == MergeItemKind.Normal && CanSellSelected());
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

        public bool CanSpend(int amount)
        {
            EnsureEnergySystem();
            return _energy != null && _energy.CanSpend(amount);
        }

        public bool TrySpend(int amount)
        {
            EnsureEnergySystem();
            return _energy != null && _energy.TrySpend(amount);
        }

        public void AddEnergy(int amount)
        {
            EnsureEnergySystem();
            _energy?.AddEnergy(amount);
        }

        public void ResolveRegeneration()
        {
            EnsureEnergySystem();
            _energy?.ResolveRegeneration();
        }

        public int GetCurrentEnergy()
        {
            EnsureEnergySystem();
            return _energy != null ? _energy.GetCurrentEnergy() : 0;
        }

        public int GetMaxNaturalEnergy()
        {
            EnsureEnergySystem();
            return _energy != null ? _energy.GetMaxNaturalEnergy() : EnergyService.DefaultMaxNaturalEnergy;
        }

        public float GetSecondsUntilNextEnergy()
        {
            EnsureEnergySystem();
            return _energy != null ? _energy.GetSecondsUntilNextEnergy() : 0f;
        }

        public float GetNextEnergyProgress()
        {
            EnsureEnergySystem();
            return _energy != null ? _energy.GetNextEnergyProgress() : 0f;
        }

        public void DebugSetEnergy(int value)
        {
            EnsureEnergySystem();
            _energySystem?.DebugSetEnergy(value);
            UpdateDebug();
        }

        public void DebugAdvanceEnergyTime(double seconds)
        {
            EnsureEnergySystem();
            _energySystem?.DebugAdvanceEnergyTime(seconds);
            UpdateDebug();
        }

        void EnsureEnergySystem()
        {
            if (_energySystem == null)
            {
                _energySystem = GetComponent<EnergySystem>();
            }

            if (_energySystem == null)
            {
                _energySystem = EnergySystem.Current;
            }

            if (_energySystem == null)
            {
                _energySystem = gameObject.AddComponent<EnergySystem>();
            }

            _energySystem.EnsureReady();
            _energy = _energySystem.Service;
        }

        void EnsureIcons()
        {
            var icons = GetComponent<IconSystem>();
            if (icons == null)
            {
                icons = IconSystem.Current;
            }

            if (icons == null)
            {
                icons = gameObject.AddComponent<IconSystem>();
            }
        }

        void EnsureSell()
        {
            EnsureDrag();
            var currency = GetComponent<CurrencySystem>();
            if (currency == null)
            {
                currency = CurrencySystem.Current;
            }

            if (currency == null)
            {
                currency = gameObject.AddComponent<CurrencySystem>();
            }

            currency.EnsureReady();

            var sell = GetComponent<SellSystem>();
            if (sell == null)
            {
                sell = gameObject.AddComponent<SellSystem>();
            }

            sell.Configure(this, sell.Config);
            var presenter = GetComponent<BoardSellPresenter>();
            if (presenter == null)
            {
                presenter = gameObject.AddComponent<BoardSellPresenter>();
            }

            presenter.Configure(this, dragView, sell.Config);
        }

        void EnsureOrders()
        {
            EnsureSell();
            var orders = GetComponent<OrderSystem>();
            if (orders == null)
            {
                orders = OrderSystem.Current;
            }

            if (orders == null)
            {
                orders = gameObject.AddComponent<OrderSystem>();
            }

            orders.Configure(this, orders.Database);
            var presenter = GetComponent<BoardOrderPresenter>();
            if (presenter == null)
            {
                presenter = gameObject.AddComponent<BoardOrderPresenter>();
            }

            presenter.Configure(this, dragView, orders.Database, null, presenter.HudView);
            EnsureOrderMarkers();
        }

        void EnsureOrderMarkers()
        {
            var markers = GetComponent<BoardOrderMarkerView>();
            if (markers == null)
            {
                markers = gameObject.AddComponent<BoardOrderMarkerView>();
            }

            if (markers.HasTemplate)
            {
                return;
            }

            var selection = selectionView != null ? selectionView : GetComponent<BoardSelectionView>();
            var searchParent = selection != null && selection.SelectionBack != null
                ? selection.SelectionBack.parent
                : null;
            if (searchParent == null)
            {
                return;
            }

            for (var i = 0; i < searchParent.childCount; i++)
            {
                var child = searchParent.GetChild(i) as RectTransform;
                if (child != null && child.name == BoardOrderMarkerView.MarkerName)
                {
                    markers.Bind(child);
                    return;
                }
            }
        }

        void NotifyBoardContentsChanged()
        {
            BoardContentsChanged?.Invoke();
        }

        public bool CanFulfillOrderRequirements(IReadOnlyList<OrderRequirement> requirements)
        {
            return TryCollectOrderItems(requirements, _orderCollectScratch, NoSelectionIndex, resetUsed: true);
        }

        public int CountAvailableOrderItems(int itemId)
        {
            var count = 0;
            if (_state == null || itemId == BoardCellState.EmptyItemId)
            {
                return 0;
            }

            for (var i = 0; i < BoardState.CellCount; i++)
            {
                if (CanCountCellForOrder(i) && _state.GetCell(i).ItemId == itemId)
                {
                    count++;
                }
            }

            return count;
        }

        public bool TryConsumeOrderItems(
            IReadOnlyList<OrderRequirement> requirements,
            List<ConsumedBoardItem> consumed,
            int preferredCellIndex = NoSelectionIndex)
        {
            if (consumed == null)
            {
                return false;
            }

            consumed.Clear();
            if (!TryCollectOrderItems(requirements, consumed, preferredCellIndex, resetUsed: true))
            {
                return false;
            }

            var consumedSelected = false;
            for (var i = 0; i < consumed.Count; i++)
            {
                var entry = consumed[i];
                var view = boardView != null ? boardView.GetCellView(entry.CellIndex) : null;
                if (view != null && _dragController != null)
                {
                    _dragController.PrepareCellForPossiblePickup(view);
                }

                if (!CanCountCellForOrder(entry.CellIndex) ||
                    _state.GetCell(entry.CellIndex).ItemId != entry.ItemId)
                {
                    consumed.Clear();
                    return false;
                }

                view = boardView != null ? boardView.GetCellView(entry.CellIndex) : null;
                if (view != null && view.ItemImage != null)
                {
                    entry.Sprite = view.ItemImage.sprite;
                    var rectSize = view.ItemImage.rectTransform.rect.size;
                    entry.Size = rectSize.x > 1f && rectSize.y > 1f ? rectSize : new Vector2(170f, 170f);
                }
                else if (itemDatabase != null && itemDatabase.TryGetById(entry.ItemId, out var data) && data != null)
                {
                    entry.Sprite = data.Icon;
                    entry.Size = new Vector2(170f, 170f);
                }

                if (entry.Sprite == null &&
                    _dragController != null &&
                    _dragController.IsBusyWithCell(entry.CellIndex) &&
                    dragView != null &&
                    dragView.TryCaptureVisualSnapshot(out var dragSprite, out var dragSize, out _, out _, out _, out _))
                {
                    entry.Sprite = dragSprite;
                    entry.Size = dragSize.x > 1f && dragSize.y > 1f ? dragSize : new Vector2(170f, 170f);
                }

                consumed[i] = entry;
                if (_selectedCellIndex == entry.CellIndex)
                {
                    consumedSelected = true;
                }
            }

            for (var i = 0; i < consumed.Count; i++)
            {
                _state.GetCell(consumed[i].CellIndex).Clear();
                if (boardView != null)
                {
                    boardView.RefreshCell(consumed[i].CellIndex);
                }
            }

            if (consumedSelected)
            {
                ClearSelection();
            }

            UpdateDebug();
            NotifyBoardContentsChanged();
            return true;
        }

        public void CollectReadyOrderHighlightCells(
            IReadOnlyList<OrderDefinition> readyOrders,
            List<int> cells)
        {
            if (cells == null)
            {
                return;
            }

            cells.Clear();
            if (readyOrders == null)
            {
                return;
            }

            for (var i = 0; i < _orderCellUsed.Length; i++)
            {
                _orderCellUsed[i] = false;
            }

            for (var i = 0; i < readyOrders.Count; i++)
            {
                var order = readyOrders[i];
                if (order == null || order.requirements == null)
                {
                    continue;
                }

                if (!TryCollectOrderItems(order.requirements, _orderCollectScratch, NoSelectionIndex, resetUsed: false))
                {
                    continue;
                }

                for (var c = 0; c < _orderCollectScratch.Count; c++)
                {
                    var index = _orderCollectScratch[c].CellIndex;
                    if (!cells.Contains(index))
                    {
                        cells.Add(index);
                    }
                }
            }
        }

        bool TryCollectOrderItems(
            IReadOnlyList<OrderRequirement> requirements,
            List<ConsumedBoardItem> collected,
            int preferredCellIndex,
            bool resetUsed)
        {
            collected.Clear();
            if (requirements == null || _state == null || requirements.Count == 0)
            {
                return false;
            }

            if (resetUsed)
            {
                for (var i = 0; i < _orderCellUsed.Length; i++)
                {
                    _orderCellUsed[i] = false;
                }
            }

            for (var r = 0; r < requirements.Count; r++)
            {
                var requirement = requirements[r];
                if (requirement == null || requirement.itemId == BoardCellState.EmptyItemId || requirement.amount < 1)
                {
                    collected.Clear();
                    return false;
                }

                var remaining = requirement.amount;
                if (TryTakeOrderCell(preferredCellIndex, requirement.itemId, r, collected))
                {
                    remaining--;
                }

                for (var i = 0; i < BoardState.CellCount && remaining > 0; i++)
                {
                    if (TryTakeOrderCell(i, requirement.itemId, r, collected))
                    {
                        remaining--;
                    }
                }

                if (remaining > 0)
                {
                    collected.Clear();
                    return false;
                }
            }

            return collected.Count > 0;
        }

        bool TryTakeOrderCell(int index, int itemId, int requirementIndex, List<ConsumedBoardItem> collected)
        {
            if (index == NoSelectionIndex ||
                _state == null ||
                !_state.IsValidIndex(index) ||
                _orderCellUsed[index] ||
                !CanCountCellForOrder(index))
            {
                return false;
            }

            if (_state.GetCell(index).ItemId != itemId)
            {
                return false;
            }

            _orderCellUsed[index] = true;
            collected.Add(new ConsumedBoardItem
            {
                CellIndex = index,
                ItemId = itemId,
                RequirementIndex = requirementIndex
            });
            return true;
        }

        bool CanCountCellForOrder(int index)
        {
            if (_state == null || !_state.IsValidIndex(index))
            {
                return false;
            }

            if (IsCellInteractionLocked(index))
            {
                return false;
            }

            var cell = _state.GetCell(index);
            if (cell == null || !cell.HasItem || cell.IsBox || cell.ItemLocked)
            {
                return false;
            }

            if (itemDatabase == null || !itemDatabase.TryGetById(cell.ItemId, out var data) || data == null)
            {
                return false;
            }

            return data.Kind == MergeItemKind.Normal;
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
            debugGeneratorAvailableDrops = 0;
            debugGeneratorMaxDrops = 0;
            debugGeneratorRechargeProgress = 0f;
            debugGeneratorSecondsUntilNextCharge = 0f;
            if (_energy != null)
            {
                debugCurrentEnergy = _energy.GetCurrentEnergy();
                debugSecondsUntilNextEnergy = _energy.GetSecondsUntilNextEnergy();
            }
            else
            {
                debugCurrentEnergy = 0;
                debugSecondsUntilNextEnergy = 0f;
            }

            var selected = GetSelectedCell();
            if (selected == null || !selected.HasItem)
            {
                return;
            }

            debugSelectedItemId = selected.ItemId;
            if (itemDatabase != null && itemDatabase.TryGetById(selected.ItemId, out var data))
            {
                debugSelectedInternalKey = data.InternalKey;
                if (data.Kind == MergeItemKind.Generator &&
                    TryGetGeneratorPresentationInfo(_selectedCellIndex, out var generatorInfo))
                {
                    debugGeneratorAvailableDrops = generatorInfo.AvailableDrops;
                    debugGeneratorMaxDrops = generatorInfo.MaxDrops;
                    debugGeneratorRechargeProgress = generatorInfo.RechargeProgress;
                    debugGeneratorSecondsUntilNextCharge = generatorInfo.SecondsUntilNextCharge;
                }
            }
        }

        void ShowItemInfo(MergeItemData data, bool allowSell, bool force = false)
        {
            if (itemInfoView == null || data == null)
            {
                return;
            }

            itemInfoView.Configure(itemDatabase, _discovery, this);
            if (force || !itemInfoView.IsShowing(data.Id))
            {
                itemInfoView.Show(data, _selectedCellIndex);
            }

            itemInfoView.SetSellVisible(allowSell);
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
            EnsureInventoryRuntime();
        }

        void EnsureInventoryRuntime()
        {
            var inventory = GetComponent<InventoryController>();
            if (inventory != null)
            {
                inventory.Configure(itemDatabase);
            }

            var flight = GetComponent<InventoryFlightPresenter>();
            if (flight != null)
            {
                flight.Configure(this, dragView, generatorAnimationConfig, generatorPresenter);
            }
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
            return GetEffectiveRechargeSeconds(definition);
        }

        public float GetEffectiveRechargeSeconds(GeneratorData definition)
        {
            if (definition == null)
            {
                return 120f;
            }

            if (useDebugCooldownOverride)
            {
                return debugCooldownSeconds;
            }

            return definition.RechargeSecondsPerCharge;
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

            return productionData.MaxAvailableDrops;
        }

        public float GetCooldownRemaining(int cellIndex)
        {
            if (!TryGetGeneratorRuntime(cellIndex, out var runtime, out var productionData))
            {
                return 0f;
            }

            var now = _gameTimeProvider.UnixTimeNow;
            _generatorInstances.ApplyRecharge(runtime, productionData, now, GetEffectiveRechargeSeconds(productionData));
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
            _generatorInstances.ApplyRecharge(runtime, productionData, now, GetEffectiveRechargeSeconds(productionData));
            info = new GeneratorPresentationInfo
            {
                IsValid = true,
                AvailableDrops = runtime.AvailableDrops,
                MaxDrops = productionData.MaxAvailableDrops,
                RechargeProgress = _generatorInstances.GetNextChargeProgress(runtime, now),
                SecondsUntilNextCharge = _generatorInstances.GetSecondsUntilNextCharge(runtime, now)
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

            cell.GeneratorInstanceId = _generatorInstances.CreateFullInstance(generatorItemId, productionData.MaxAvailableDrops);
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
