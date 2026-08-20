using UnityEngine;

/// <summary>
/// Baked world-space depth data for static water surfaces.
/// R channel stores water column depth normalized by <see cref="MaxDepth"/>.
/// </summary>
[CreateAssetMenu(fileName = "WaterSurfaceData", menuName = "GTA/Water Surface Data")]
public sealed class WaterSurfaceData : ScriptableObject
{
    public Texture2D DepthMap;
    public Vector4 Bounds;
    public float WaterLevel;
    public float MaxDepth;
    public int Resolution;
    public long SceneHash;
}
