using Godot;

namespace WorldV2;

public sealed class WorldClimateSampleV2
{
    public int WorldSeed { get; init; }
    public Vector2I GlobalCell { get; init; }
    public float Elevation { get; set; }
    public float Temperature { get; set; }
    public float Humidity { get; set; }
    public float Danger { get; set; }
    public float RuinDensity { get; set; }
    public float Civilization { get; set; }
    public float RoadPotential { get; set; }
    public float RiverPotential { get; set; }
    public float ResourceRichness { get; set; }
    public float DistanceFromStart { get; set; }
    public float TownInfluence { get; set; }
    public float StartSafeInfluence { get; set; }
    public BiomeTypeV2 Biome { get; set; } = BiomeTypeV2.Plains;
    public bool IsWater { get; set; }
    public bool IsOcean { get; set; }
    public bool IsRiver { get; set; }
    public bool IsRoad { get; set; }
    public bool IsBuildRestricted { get; set; }
    public bool IsWalkable { get; set; } = true;
    public bool IsInsideTownCore { get; set; }
    public bool IsInsideTownInner { get; set; }
    public bool IsInsideTownBlend { get; set; }
    public bool IsStartSafeZone { get; set; }
}
