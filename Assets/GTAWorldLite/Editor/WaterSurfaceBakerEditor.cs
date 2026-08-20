#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WaterSurfaceBaker))]
public sealed class WaterSurfaceBakerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var baker = (WaterSurfaceBaker)target;

        EditorGUILayout.Space(8);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Bake Water Surface Depth", GUILayout.Height(32)))
                baker.BakeNow();
        }

        if (Application.isPlaying)
            EditorGUILayout.HelpBox("Bake only in Edit Mode — Play Mode uses the already baked asset.", MessageType.Info);
        else if (baker.Data == null || baker.Data.DepthMap == null)
            EditorGUILayout.HelpBox("No baked depth map yet. Press the button above once after the island is in the scene.", MessageType.Warning);
        else
            EditorGUILayout.HelpBox($"Using {baker.Data.DepthMap.name} ({baker.Data.Resolution}²). Rebake after moving water or changing the island.", MessageType.None);
    }
}

public static class WaterSurfaceBakerMenu
{
    [MenuItem("GTA/World Lite/Bake Water Surface Depth")]
    public static void BakeAllInScene()
    {
        var bakers = Object.FindObjectsByType<WaterSurfaceBaker>();
        if (bakers.Length == 0)
        {
            Debug.LogWarning("WaterSurfaceBaker: no WaterSurfaceBaker found in the open scene.");
            return;
        }

        for (int i = 0; i < bakers.Length; i++)
            bakers[i].BakeNow();
    }
}
#endif
