using Godot;

namespace WorldV2;

public enum RoadTargetKindV3
{
    Quarry,
    Ruin,
    DungeonEntrance,
    ForestEdge,
    WorldEdgeExit,
    FutureRuinSite,
    FutureDungeonSite,
    FutureBanditCampSite,
    FutureFactionOutpostSite
}

public sealed class RoadTargetAnchorV3
{
    public int Id { get; init; }
    public RoadTargetKindV3 Kind { get; init; }
    public Vector2 Position { get; init; }
    public float Radius { get; init; }
    public Rect2 Bounds { get; init; }
    public int Seed { get; init; }
    public bool IsImplementedPoi { get; init; }
    public int LinkedFeatureId { get; init; }
}
