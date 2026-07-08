using Godot;

namespace WorldV2;

public sealed class VillageSiteV2
{
    public int Id { get; init; }
    public Vector2I Center { get; init; }
    public float Radius { get; init; }
    public float OccupiedRadius { get; init; }
    public float AvoidRadius { get; init; }
    public VillageScaleV2 Scale { get; init; } = VillageScaleV2.Village;
    public bool IsStartingVillage { get; set; }
    public bool ShouldConnectRoad { get; init; } = true;
}
