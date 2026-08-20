/// <summary>Shared fog settings for GTA shaders and editor Scene View.</summary>
public static class GTAWorldFogSettings
{
    public const string EditorSceneViewPrefKey = "GTA.SceneView.FogEnabled";

#if UNITY_EDITOR
    public static bool IsFogEnabledInEditorSceneView()
    {
        if (UnityEngine.Application.isPlaying)
            return true;
        return UnityEditor.EditorPrefs.GetBool(EditorSceneViewPrefKey, true);
    }
#endif
}
