using Godot;

namespace WorldV2;

public sealed class QuarryRegionV3
{
    public int Id { get; init; }
    public Vector2 Center { get; init; }
    public float ApproxRadius { get; init; }
    public Rect2 Bounds { get; init; }
    public int Seed { get; init; }
    public float Threshold { get; init; } = 0.50f;
    public float NoiseScale { get; init; } = 0.030f;
    public float WarpStrength { get; init; } = 12.0f;
    public float Density { get; init; } = 1.0f;
    public bool IsMajorQuarry { get; init; }
}
