using UnityEngine;

/// <summary>
/// Static local point light on a prefab (street lamp, sign, interior fixture).
/// Pick a <see cref="GTALightClusterId"/> — baking/opacity are owned by <see cref="GTALightClusterController"/>.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("GTA/Local Light")]
public sealed class GTALocalLight : MonoBehaviour
{
    public const float MaxIntensity = 2f;

    [SerializeField] GTALightClusterId cluster = GTALightClusterId.Night;
    [SerializeField] Color color = new(1f, 0.85f, 0.55f, 1f);
    [Tooltip("Brightness of this lamp. Soft-capped at 2 to avoid blown-out cluster volumes.")]
    [SerializeField, Range(0f, MaxIntensity)] float intensity = 1.2f;
    [SerializeField, Min(0.1f)] float range = 12f;
    [SerializeField] bool includeInBake = true;

    public GTALightClusterId Cluster => cluster;
    public Color Color => color;
    public float Intensity => Mathf.Clamp(intensity, 0f, MaxIntensity);
    public float Range => range;
    public bool IncludeInBake => includeInBake && isActiveAndEnabled;
    public Vector3 PositionWS => transform.position;

    public void SetCluster(GTALightClusterId id)
    {
        cluster = id;
        GTALightClusterController.NotifyLightChanged(id);
    }

    void OnDrawGizmos()
    {
        // Built-in Unity point-light bulb — visible & pickable like a regular Light.
        Gizmos.DrawIcon(transform.position, "PointLight Gizmo", true);
    }

    void OnDrawGizmosSelected()
    {
        Color c = color;
        c.a = 0.35f;
        Gizmos.color = c;
        Gizmos.DrawWireSphere(transform.position, range);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        intensity = Mathf.Clamp(intensity, 0f, MaxIntensity);
        GTALightClusterController.NotifyLightChanged(cluster);
    }

    void OnEnable() => GTALightClusterController.NotifyLightChanged(cluster);
    void OnDisable() => GTALightClusterController.NotifyLightChanged(cluster);
#endif
}
