using Godot;

namespace WorldV2;

public sealed class CellData
{
    public Vector2I GlobalCellCoord { get; init; }
    public Vector2I GlobalChunkCoord { get; init; }
    public Vector2I LocalCellCoord { get; init; }
    public Vector2I SectorCoord { get; init; }
    public Vector2I ChunkCoord { get; init; }
    public TileType TileType { get; set; }
    public BiomeKindV3 BiomeKind { get; set; } = BiomeKindV3.Plains;
    public BiomeTypeV2 Biome { get; set; } = BiomeTypeV2.Plains;
    public WorldResourceTypeV2 ResourceType { get; set; } = WorldResourceTypeV2.None;
    public bool IsWater { get; set; }
    public bool IsOcean { get; set; }
    public bool IsRiver { get; set; }
    public bool IsRiverBank { get; set; }
    public bool IsBridgeCandidate { get; set; }
    public bool IsRoad { get; set; }
    public bool IsVillage { get; set; }
    public bool IsStartingVillage { get; set; }
    public bool IsLandmark { get; set; }
    public LandmarkKindV2 LandmarkKind { get; set; } = LandmarkKindV2.None;
    public bool IsQuarry { get; set; }
    public bool HasOreSpot { get; set; }
    public float ForestStrength { get; set; }
    public bool IsBuildRestricted { get; set; }
    public bool IsWalkable { get; set; } = true;
    public string OwnerId { get; set; } = string.Empty;

    public CellCoord GetCoordinate()
    {
        return new CellCoord(SectorCoord, LocalCellCoord);
    }
}
