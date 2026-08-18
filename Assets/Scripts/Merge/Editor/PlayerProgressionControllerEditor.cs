using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    [CustomEditor(typeof(PlayerProgressionController))]
    public class PlayerProgressionControllerEditor : UnityEditor.Editor
    {
        int _setRank = 1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (PlayerProgressionController)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Progression Debug", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                var snapshot = Application.isPlaying ? controller.GetSnapshot() : null;
                EditorGUILayout.LabelField("Rank", snapshot != null ? snapshot.CurrentRank.ToString() : "-");
                EditorGUILayout.LabelField("Total XP", snapshot != null ? snapshot.TotalXp.ToString() : "-");
                EditorGUILayout.LabelField("XP Into Rank", snapshot != null ? snapshot.XpIntoCurrentRank.ToString() : "-");
                EditorGUILayout.LabelField("XP Required", snapshot != null ? snapshot.XpRequiredForNextRank.ToString() : "-");
                EditorGUILayout.LabelField("Unclaimed Rewards", snapshot != null && controller.HasUnclaimedRankRewards() ? "Yes" : "No");

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add 10 XP"))
                {
                    controller.DebugAddXp(10);
                }

                if (GUILayout.Button("Add 100 XP"))
                {
                    controller.DebugAddXp(100);
                }

                if (GUILayout.Button("Add 500 XP"))
                {
                    controller.DebugAddXp(500);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                _setRank = EditorGUILayout.IntField("Set Rank", _setRank);
                if (GUILayout.Button("Apply Rank"))
                {
                    controller.DebugSetRank(_setRank);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Claim Next Reward"))
                {
                    controller.DebugClaimNextReward();
                }

                if (GUILayout.Button("Reset Progression"))
                {
                    controller.DebugResetProgression();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Debug XP/Rank/Claim buttons are available in Play Mode. Progress resets on Stop because there is no save yet.", MessageType.Info);
            }
        }
    }
}
