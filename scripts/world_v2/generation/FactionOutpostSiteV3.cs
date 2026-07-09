using Godot;

namespace WorldV2;

public sealed class FactionOutpostSiteV3
{
    public int Id { get; init; }
    public FactionOutpostKindV3 Kind { get; init; }
    public FactionOutpostOwnerV3 Owner { get; init; }
    public Vector2 Center { get; init; }
    public float ApproxRadius { get; init; }
    public Rect2 Bounds { get; init; }
    public int Seed { get; init; }
    public bool IsRoadLinked { get; set; }
    public int LinkedRoadId { get; set; } = -1;
    public int NearbySettlementId { get; init; } = -1;
    public string InfluenceHint { get; init; } = string.Empty;
}
