using SanIsland.Merge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge.Editor
{
    public static class UiInteractionFeedbackSetupTool
    {
        public const string ConfigPath = "Assets/Data/UiInteractionFeedbackConfig.asset";

        [MenuItem("Tools/San Island/Setup UI Interaction Feedback")]
        public static void SetupFromMenu()
        {
            Setup();
        }

        public static void Setup()
        {
            var config = EnsureConfig();
            var added = 0;
            var updated = 0;

            var selectables = Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include);
            for (var i = 0; i < selectables.Length; i++)
            {
                var selectable = selectables[i];
                if (selectable == null || ShouldSkip(selectable.gameObject))
                {
                    continue;
                }

                var feedback = selectable.GetComponent<UiHoverScaleFeedback>();
                if (feedback == null)
                {
                    feedback = Undo.AddComponent<UiHoverScaleFeedback>(selectable.gameObject);
                    added++;
                }
                else
                {
                    updated++;
                }

                feedback.Configure(config, selectable.transform as RectTransform);
                EditorUtility.SetDirty(feedback);
            }

            var controllers = Object.FindObjectsByType<BoardController>(FindObjectsInactive.Include);
            for (var i = 0; i < controllers.Length; i++)
            {
                controllers[i].SetUiFeedbackConfig(config);
                EditorUtility.SetDirty(controllers[i]);
                if (controllers[i].BoardView != null)
                {
                    controllers[i].BoardView.BindInteraction(controllers[i], controllers[i].AnimationConfig, config);
                    EditorUtility.SetDirty(controllers[i].BoardView);
                }
            }

            var animators = Object.FindObjectsByType<BoardItemAnimator>(FindObjectsInactive.Include);
            for (var i = 0; i < animators.Length; i++)
            {
                EditorUtility.SetDirty(animators[i]);
            }

            if (controllers.Length > 0)
            {
                EditorSceneManager.MarkSceneDirty(controllers[0].gameObject.scene);
            }
            else
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            Debug.Log($"[UiInteractionFeedback] Setup complete. Added {added}, updated {updated} hover components. Config: {ConfigPath}");
        }

        public static UiInteractionFeedbackConfig EnsureConfig()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }

            var config = AssetDatabase.LoadAssetAtPath<UiInteractionFeedbackConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<UiInteractionFeedbackConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        static bool ShouldSkip(GameObject go)
        {
            return go.GetComponent<BoardCellView>() != null
                || go.GetComponent<BoardItemAnimator>() != null
                || go.GetComponent<BoardCellPointer>() != null
                || go.GetComponentInParent<BoardCellView>() != null
                || go.GetComponentInParent<BoardItemAnimator>() != null;
        }
    }
}
