#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GTALocalLight))]
[CanEditMultipleObjects]
public sealed class GTALocalLightEditor : Editor
{
    static Texture2D _lightIcon;

    void OnEnable()
    {
        EnsureIcon();
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                EditorGUIUtility.SetIconForObject(targets[i], _lightIcon);
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }

    static void EnsureIcon()
    {
        if (_lightIcon != null)
            return;

        // Same hierarchy/scene icon family as Unity's Light component.
        _lightIcon = EditorGUIUtility.IconContent("Light Icon").image as Texture2D
                     ?? EditorGUIUtility.IconContent("d_Light Icon").image as Texture2D
                     ?? EditorGUIUtility.IconContent("PointLight Gizmo").image as Texture2D;
    }

    [InitializeOnLoadMethod]
    static void AssignIconsToExisting()
    {
        EditorApplication.delayCall += () =>
        {
            EnsureIcon();
            if (_lightIcon == null)
                return;

            var lights = Object.FindObjectsByType<GTALocalLight>(FindObjectsInactive.Include);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                    EditorGUIUtility.SetIconForObject(lights[i], _lightIcon);
            }
        };
    }
}
#endif
