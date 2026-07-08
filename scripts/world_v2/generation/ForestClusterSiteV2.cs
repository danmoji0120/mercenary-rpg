using System.Collections.Generic;
using Godot;

namespace WorldV2;

public sealed class ForestClusterSiteV2
{
    public int Id { get; init; }
    public Vector2 Center { get; init; }
    public float Length { get; init; }
    public float Width { get; init; }
    public float Angle { get; init; }
    public float Density { get; init; }
    public float EdgeNoiseStrength { get; init; }
    public float ClearingStrength { get; init; }
    public IReadOnlyList<ForestLobeSiteV2> Lobes { get; init; } = new List<ForestLobeSiteV2>();
    public IReadOnlyList<ForestGroveSiteV2> Groves { get; init; } = new List<ForestGroveSiteV2>();
}

public sealed class ForestLobeSiteV2
{
    public int Id { get; init; }
    public Vector2 Center { get; init; }
    public float Radius { get; init; }
    public float Aspect { get; init; } = 1.0f;
    public float Angle { get; init; }
    public float Density { get; init; } = 1.0f;
}
