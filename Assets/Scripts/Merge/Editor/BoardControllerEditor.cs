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

                if (GUILayout.Button("Reset To Initial Board"))
                {
                    controller.LoadInitialBoard();
                }

                if (GUILayout.Button("Validate Board Layout"))
                {
                    BoardLayoutValidator.Validate(controller.State, controller.ItemDatabase);
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode: Reset Development Board / Reset To Initial Board. Uncheck Use Development Board State to load the authored board on Play.", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generator Instance Debug", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Available Drops", controller.DebugGeneratorAvailableDrops);
                EditorGUILayout.IntField("Max Drops", controller.DebugGeneratorMaxDrops);
                EditorGUILayout.FloatField("Recharge Progress", controller.DebugGeneratorRechargeProgress);
                EditorGUILayout.FloatField("Seconds Until Next Charge", controller.DebugGeneratorSecondsUntilNextCharge);
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Orders Debug", EditorStyles.boldLabel);
            var orders = controller.GetComponent<OrderSystem>();
            using (new EditorGUI.DisabledScope(!Application.isPlaying || orders == null))
            {
                var active = Application.isPlaying && orders != null ? string.Join(",", orders.State.activeOrderIds) : string.Empty;
                var queued = Application.isPlaying && orders != null ? string.Join(",", orders.State.queuedOrderIds) : string.Empty;
                var completed = Application.isPlaying && orders != null ? string.Join(",", orders.State.completedOrderIds) : string.Empty;
                EditorGUILayout.LabelField("Active", active);
                EditorGUILayout.LabelField("Queued", queued);
                EditorGUILayout.LabelField("Completed", completed);
                if (GUILayout.Button("Reset Development Orders"))
                {
                    orders.ResetDevelopment();
                }

                if (GUILayout.Button("Fill Empty Generated Slots"))
                {
                    orders.FillEmptyGeneratedSlots();
                    orders.RecalculateReadiness();
                }

                DrawFamilyUnlockDebug(orders);
            }
        }

        static void DrawFamilyUnlockDebug(OrderSystem orders)
        {
            if (orders == null || orders.Progression == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Family Progression Debug", EditorStyles.boldLabel);
            var families = (MergeItemFamily[])System.Enum.GetValues(typeof(MergeItemFamily));
            for (var i = 0; i < families.Length; i++)
            {
                var family = families[i];
                var unlocked = orders.Progression.IsFamilyUnlocked(family);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(family.ToString(), unlocked ? "Unlocked" : "Locked");
                if (unlocked)
                {
                    if (GUILayout.Button("Lock", GUILayout.Width(70f)))
                    {
                        orders.Progression.LockFamily(family);
                    }
                }
                else if (GUILayout.Button("Unlock", GUILayout.Width(70f)))
                {
                    orders.Progression.UnlockFamily(family);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.HelpBox(
                "Unlock does not replace active orders. Complete a slot to generate a new order from the expanded family pool.",
                MessageType.Info);

            if (GUILayout.Button("Complete First Ready Order"))
            {
                CompleteFirstReadyOrder(orders);
            }

            if (GUILayout.Button("Debug Generate 1000 Orders"))
            {
                orders.DebugGenerateOrders(1000);
            }
        }

        static void CompleteFirstReadyOrder(OrderSystem orders)
        {
            var active = orders.State.activeOrderIds;
            for (var i = 0; i < active.Count; i++)
            {
                if (orders.IsOrderReady(active[i]))
                {
                    orders.TryCompleteOrder(active[i]);
                    return;
                }
            }
        }
    }
}
