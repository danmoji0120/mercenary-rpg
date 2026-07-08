using Godot;

namespace WorldV2;

public sealed class SectorMetadata
{
    public string WorldId { get; set; } = string.Empty;
    public int WorldSeed { get; set; }
    public Vector2I SectorCoord { get; set; }
    public int SectorSeed { get; set; }
    public SectorType Type { get; set; } = SectorType.Wasteland;
    public bool IsDiscovered { get; set; }
    public bool IsVisited { get; set; }
    public bool IsCentralTown { get; set; }
    public bool IsBuildRestricted { get; set; }
    public float DangerLevel { get; set; }
    public float ResourceRichness { get; set; }
    public BiomeTypeV2 DominantBiome { get; set; } = BiomeTypeV2.Plains;
    public BiomeTypeV2 SecondaryBiome { get; set; } = BiomeTypeV2.Plains;
    public float BiomeDiversityScore { get; set; }
    public bool HasStartingArea { get; set; }
    public float AverageStartInfluence { get; set; }
    public float AverageDanger { get; set; }
    public float AverageResourceRichness { get; set; }
    public float AverageRuinDensity { get; set; }
    public float RoadCoverage { get; set; }
    public int RoadPathCount { get; set; }
    public int VillageCount { get; set; }
    public int LandmarkCount { get; set; }
    public int RoadConnectedLandmarkCount { get; set; }
    public int QuarryCount { get; set; }
    public bool HasOcean { get; set; }
    public bool HasRiver { get; set; }
    public bool HasRoad { get; set; }
}
