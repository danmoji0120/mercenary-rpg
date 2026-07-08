using System.Collections.Generic;
using Godot;

namespace WorldV2;

public enum QuarrySizeClassV3
{
    Minor,
    Major
}

public sealed class QuarryClusterV3
{
    public int Id { get; init; }
    public Vector2 Center { get; init; }
    public float ApproxRadius { get; init; }
    public Rect2 Bounds { get; init; }
    public int Seed { get; init; }
    public float Density { get; init; } = 1.0f;
    public int PatchCount { get; init; }
    public QuarrySizeClassV3 SizeClass { get; init; } = QuarrySizeClassV3.Minor;
    public bool IsMajorQuarry => SizeClass == QuarrySizeClassV3.Major;
    public IReadOnlyList<QuarryPatchV3> Patches { get; init; } = new List<QuarryPatchV3>();
}

public sealed class QuarryPatchV3
{
    public int Id { get; init; }
    public Vector2 Center { get; init; }
    public float Radius { get; init; }
    public float Aspect { get; init; } = 1.0f;
    public float Angle { get; init; }
    public float Density { get; init; } = 1.0f;
}
