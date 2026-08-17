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
        }
    }
}
