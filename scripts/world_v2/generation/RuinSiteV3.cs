using Godot;

namespace WorldV2;

public enum RuinKindV3
{
    OldFoundation,
    BrokenCamp,
    FallenTower,
    AbandonedYard
}

public sealed class RuinSiteV3
{
    public int Id { get; init; }
    public RuinKindV3 Kind { get; init; }
    public Vector2 Center { get; init; }
    public float ApproxRadius { get; init; }
    public Rect2 Bounds { get; init; }
    public int Seed { get; init; }
    public float Density { get; init; } = 1.0f;
    public bool IsRoadLinked { get; set; }
    public int LinkedRoadId { get; set; } = -1;
}
