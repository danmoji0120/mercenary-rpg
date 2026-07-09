using Godot;

namespace WorldV2;

public sealed class BanditCampSiteV3
{
    public int Id { get; init; }
    public BanditCampKindV3 Kind { get; init; }
    public Vector2 Center { get; init; }
    public float ApproxRadius { get; init; }
    public Rect2 Bounds { get; init; }
    public int Seed { get; init; }
    public bool IsRoadLinked { get; set; }
    public int LinkedRoadId { get; set; } = -1;
    public int ThreatTier { get; init; }
    public string NearbyFeatureHint { get; init; } = string.Empty;
}
