using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Y-axis billboard for sprites / quads: rotates only horizontally to face the camera.
/// Updates in Play Mode and in the Scene View (edit mode).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("GTA/Sprite Y Billboard")]
public sealed class GTASpriteYBillboard : MonoBehaviour
{
    [Tooltip("If set, always face this camera. Otherwise uses Scene View camera in edit mode, or Main Camera at runtime.")]
    [SerializeField] Camera targetCamera;

    [Tooltip("Flip 180° on Y if the sprite faces the wrong way.")]
    [SerializeField] bool invertFacing;

    [Tooltip("Keep the object's current local Y offset relative to the camera-facing direction.")]
    [SerializeField] float yawOffset;

    void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        FaceCamera(ResolveCamera());
    }

#if UNITY_EDITOR
    void OnEnable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (Application.isPlaying || this == null || !isActiveAndEnabled)
            return;

        Camera cam = targetCamera != null ? targetCamera : sceneView.camera;
        FaceCamera(cam);
    }
#endif

    Camera ResolveCamera()
    {
        if (targetCamera != null)
            return targetCamera;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
                return sceneView.camera;
        }
#endif

        if (Camera.main != null)
            return Camera.main;

        return Camera.current;
    }

    void FaceCamera(Camera cam)
    {
        if (cam == null)
            return;

        Vector3 toCamera = cam.transform.position - transform.position;
        toCamera.y = 0f;

        if (toCamera.sqrMagnitude < 1e-6f)
            return;

        if (invertFacing)
            toCamera = -toCamera;

        Quaternion facing = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        if (Mathf.Abs(yawOffset) > 0.001f)
            facing *= Quaternion.Euler(0f, yawOffset, 0f);

        if (transform.rotation != facing)
            transform.rotation = facing;
    }
}
