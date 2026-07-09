using Godot;

namespace WorldV2;

public sealed class VillageSiteV2
{
    public int Id { get; init; }
    public Vector2I Center { get; init; }
    public float Radius { get; set; }
    public float OccupiedRadius { get; set; }
    public float AvoidRadius { get; set; }
    public VillageScaleV2 Scale { get; set; } = VillageScaleV2.Village;
    public SettlementRoleV3 Role { get; set; } = SettlementRoleV3.Common;
    public bool IsStartingVillage { get; set; }
    public bool ShouldConnectRoad { get; init; } = true;
}
