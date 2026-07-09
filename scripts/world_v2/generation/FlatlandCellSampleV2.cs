using Godot;

namespace WorldV2;

public sealed class FlatlandCellSampleV2
{
    public Vector2I GlobalCellCoord { get; init; }
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
    public bool IsDungeonEntrance { get; set; }
    public DungeonEntranceKindV3 DungeonEntranceKind { get; set; }
    public bool IsBanditCamp { get; set; }
    public BanditCampKindV3 BanditCampKind { get; set; }
    public bool IsFactionOutpost { get; set; }
    public FactionOutpostKindV3 FactionOutpostKind { get; set; }
    public FactionOutpostOwnerV3 FactionOutpostOwner { get; set; }
    public float ForestStrength { get; set; }
    public bool IsForest { get; set; }
    public bool IsDenseForest { get; set; }
    public bool IsBuildRestricted { get; set; }
    public bool IsWalkable { get; set; } = true;
    public BiomeKindV3 BiomeKind { get; set; } = BiomeKindV3.Plains;
    public BiomeTypeV2 Biome { get; set; } = BiomeTypeV2.Plains;
    public TileType TileType { get; set; } = TileType.Grass;
}
