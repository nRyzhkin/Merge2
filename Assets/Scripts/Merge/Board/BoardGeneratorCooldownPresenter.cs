using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardGeneratorCooldownPresenter : MonoBehaviour
    {
        BoardController _controller;
        BoardView _boardView;
        GeneratorInstanceService _instances;
        GeneratorProductionDatabase _database;
        MergeItemDatabase _itemDatabase;
        IGameTimeProvider _timeProvider;

        public void Configure(
            BoardController controller,
            BoardView boardView,
            GeneratorInstanceService instances,
            GeneratorProductionDatabase database,
            MergeItemDatabase itemDatabase,
            IGameTimeProvider timeProvider)
        {
            _controller = controller;
            _boardView = boardView;
            _instances = instances;
            _database = database;
            _itemDatabase = itemDatabase;
            _timeProvider = timeProvider;
        }

        void LateUpdate()
        {
            if (_controller == null || _boardView == null || _instances == null || _database == null || _itemDatabase == null || _timeProvider == null)
            {
                return;
            }

            var cells = _boardView.Cells;
            if (cells == null)
            {
                return;
            }

            var now = _timeProvider.UnixTimeNow;
            for (var i = 0; i < cells.Count; i++)
            {
                var cellView = cells[i];
                if (cellView == null)
                {
                    continue;
                }

                var state = _controller.State;
                if (state == null || !state.IsValidIndex(cellView.Index))
                {
                    continue;
                }

                var cell = state.GetCell(cellView.Index);
                if (cell == null || !cell.HasItem || cell.IsBox || cell.ItemLocked)
                {
                    cellView.ApplyGeneratorPresentation(false, false, 0f);
                    continue;
                }

                if (!_itemDatabase.TryGetById(cell.ItemId, out var itemData) ||
                    itemData == null ||
                    itemData.Kind != MergeItemKind.Generator)
                {
                    cellView.ApplyGeneratorPresentation(false, false, 0f);
                    continue;
                }

                if (!_database.TryGetGeneratorData(cell.ItemId, out var generatorData))
                {
                    cellView.ApplyGeneratorPresentation(false, false, 0f);
                    continue;
                }

                if (!_instances.TryGetRuntime(cell.GeneratorInstanceId, out var runtime))
                {
                    cellView.ApplyGeneratorPresentation(false, false, 0f);
                    continue;
                }

                _instances.ResolveCooldown(runtime, generatorData, now);

                if (runtime.AvailableDrops > 0)
                {
                    cellView.ApplyGeneratorPresentation(false, false, 0f);
                    continue;
                }

                if (!_instances.IsOnCooldown(runtime, now))
                {
                    cellView.ApplyGeneratorPresentation(false, true, 0f);
                    continue;
                }

                var progress = _instances.GetCooldownProgress(runtime, now);
                cellView.ApplyGeneratorPresentation(true, true, progress);
            }
        }
    }
}
