using Godot;

namespace WorldV2;

public sealed class BiomeRegionV3
{
    public int Id { get; init; }
    public BiomeKindV3 Kind { get; init; } = BiomeKindV3.Plains;
    public Vector2 Center { get; init; }
    public float ApproxRadius { get; init; }
    public Rect2 Bounds { get; init; }
    public int Seed { get; init; }
    public float Threshold { get; init; } = 0.46f;
    public float NoiseScale { get; init; } = 0.003f;
    public float WarpStrength { get; init; } = 120.0f;
    public float Weight { get; init; } = 1.0f;
    public bool IsMajorRegion { get; init; }
}
