#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Exports the self-contained GTA World Lite folder as a .unitypackage.</summary>
public static class GTAWorldLitePackageExporter
{
    const string PackageFolder = "Assets/GTAWorldLite";

    [MenuItem("GTA/World Lite/Export .unitypackage...", false, 300)]
    public static void ExportUnityPackage()
    {
        if (!AssetDatabase.IsValidFolder(PackageFolder))
        {
            Debug.LogError($"GTA World Lite: folder not found at {PackageFolder}");
            return;
        }

        string defaultName = "GTAWorldLite.unitypackage";
        string path = EditorUtility.SaveFilePanel(
            "Export GTA World Lite",
            Directory.GetCurrentDirectory(),
            defaultName,
            "unitypackage");

        if (string.IsNullOrEmpty(path))
            return;

        AssetDatabase.ExportPackage(
            new[] { PackageFolder },
            path,
            ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

        Debug.Log($"GTA World Lite exported to {path}");
        EditorUtility.RevealInFinder(path);
    }

    [MenuItem("GTA/World Lite/Open Package Folder", false, 301)]
    public static void PingPackageFolder()
    {
        var folder = AssetDatabase.LoadAssetAtPath<Object>(PackageFolder);
        if (folder != null)
            EditorGUIUtility.PingObject(folder);
    }
}
#endif
