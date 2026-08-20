#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only fog toggle for Scene View (not Play Mode). Drives _GTA_FogEnabled and RenderSettings.fog.
/// </summary>
[InitializeOnLoad]
public static class GTAEditorSceneFog
{
    static GTAEditorSceneFog()
    {
        EditorApplication.delayCall += ApplyToOpenScene;
    }

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(GTAWorldFogSettings.EditorSceneViewPrefKey, true);
        set => SetEnabled(value);
    }

    public static void SetEnabled(bool enabled)
    {
        if (EditorPrefs.GetBool(GTAWorldFogSettings.EditorSceneViewPrefKey, true) == enabled
            && Shader.GetGlobalFloat("_GTA_FogEnabled") > 0.5f == enabled)
            return;

        EditorPrefs.SetBool(GTAWorldFogSettings.EditorSceneViewPrefKey, enabled);
        ApplyToOpenScene();
    }

    [MenuItem("GTA/World Lite/Scene View/Fog", false, 200)]
    static void ToggleFogMenu()
    {
        SetEnabled(!Enabled);
    }

    [MenuItem("GTA/World Lite/Scene View/Fog", true)]
    static bool ToggleFogMenuValidate()
    {
        Menu.SetChecked("GTA/World Lite/Scene View/Fog", Enabled);
        return true;
    }

    static void ApplyToOpenScene()
    {
        bool fog = Enabled;
        Shader.SetGlobalFloat(Shader.PropertyToID("_GTA_FogEnabled"), fog ? 1f : 0f);
        RenderSettings.fog = fog;

        GTAWorldController[] controllers = Object.FindObjectsByType<GTAWorldController>(FindObjectsInactive.Include);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                controllers[i].EditorRefreshWorld();
        }

        SceneView.RepaintAll();
    }
}
#endif
