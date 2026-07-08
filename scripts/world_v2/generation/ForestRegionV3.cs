using Godot;

namespace WorldV2;

public sealed class ForestRegionV3
{
    public int RegionId { get; init; }
    public Vector2 Center { get; init; }
    public float ApproxRadius { get; init; }
    public Rect2 Bounds { get; init; }
    public int Seed { get; init; }
    public float Threshold { get; init; } = 0.50f;
    public float NoiseScale { get; init; } = 0.015f;
    public float WarpStrength { get; init; } = 38.0f;
    public float Density { get; init; } = 1.0f;
    public bool IsMajorForest { get; init; }
}
