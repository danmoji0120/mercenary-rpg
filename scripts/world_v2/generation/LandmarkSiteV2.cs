using Godot;

namespace WorldV2;

public sealed class LandmarkSiteV2
{
    public int Id { get; init; }
    public LandmarkKindV2 Kind { get; init; }
    public Vector2I Center { get; init; }
    public float Radius { get; init; }
    public float OccupiedRadius { get; init; }
    public float AvoidRadius { get; init; }
    public bool CanHaveRoad { get; init; }
    public float RoadConnectionChance { get; init; }
    public bool ShouldConnectRoad { get; init; }
}
