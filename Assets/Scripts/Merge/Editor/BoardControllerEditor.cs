using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    [CustomEditor(typeof(BoardController))]
    public class BoardControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (BoardController)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selection Debug", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Selected Cell Index", controller.SelectedCellIndex);
                var selected = controller.GetSelectedCell();
                EditorGUILayout.IntField("Selected Item ID", selected != null ? selected.ItemId : BoardCellState.EmptyItemId);
                var key = string.Empty;
                if (selected != null && selected.HasItem && controller.ItemDatabase != null &&
                    controller.ItemDatabase.TryGetById(selected.ItemId, out var data))
                {
                    key = data.InternalKey;
                }

                EditorGUILayout.TextField("Selected Internal Key", key);
                EditorGUILayout.IntField("Discovered Count", controller.Discovery != null ? controller.Discovery.DiscoveredCount : 0);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Setup Merge Board"))
            {
                MergeBoardSetupTool.Setup();
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Reset Development Board"))
                {
                    controller.ResetDevelopmentBoard();
                }

                if (GUILayout.Button("Validate Board Layout"))
                {
                    BoardLayoutValidator.Validate(controller.State, controller.ItemDatabase);
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Reset Development Board is available in Play Mode.", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Energy Debug", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                var current = Application.isPlaying ? controller.GetCurrentEnergy() : EnergyService.DefaultMaxNaturalEnergy;
                var untilNext = Application.isPlaying ? controller.GetSecondsUntilNextEnergy() : 0f;
                EditorGUILayout.LabelField("Current Energy", current.ToString());
                EditorGUILayout.LabelField("Seconds Until Next", untilNext.ToString("0.0"));

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Energy 100"))
                {
                    controller.DebugSetEnergy(100);
                }

                if (GUILayout.Button("Energy 10"))
                {
                    controller.DebugSetEnergy(10);
                }

                if (GUILayout.Button("Energy 1"))
                {
                    controller.DebugSetEnergy(1);
                }

                if (GUILayout.Button("Energy 0"))
                {
                    controller.DebugSetEnergy(0);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Advance Energy Time +120s"))
                {
                    controller.DebugAdvanceEnergyTime(120d);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sell / Coins Debug", EditorStyles.boldLabel);
            var sell = controller.GetComponent<SellSystem>();
            using (new EditorGUI.DisabledScope(!Application.isPlaying || sell == null))
            {
                var coins = Application.isPlaying && sell != null ? sell.Currency.GetCoins() : 0;
                var canUndo = Application.isPlaying && sell != null && sell.CanUndoLastSale;
                var remaining = Application.isPlaying && sell != null ? sell.UndoSecondsRemaining : 0f;
                EditorGUILayout.LabelField("Coins", coins.ToString());
                EditorGUILayout.LabelField("Can Undo Last Sale", canUndo.ToString());
                EditorGUILayout.LabelField("Undo Seconds Remaining", remaining.ToString("0.0"));
                EditorGUILayout.HelpBox("Undo button is not on the scene. This Play Mode control tests TryUndoLastSale(). Place a designer Undo control later and bind it to SellSystem.TryUndoLastSale().", MessageType.Info);
                if (GUILayout.Button("Undo Last Sale"))
                {
                    sell.TryUndoLastSale();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Coins 0"))
                {
                    sell.Currency.DebugSetCoins(0);
                }

                if (GUILayout.Button("Coins +100"))
                {
                    sell.Currency.AddCoins(100);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
