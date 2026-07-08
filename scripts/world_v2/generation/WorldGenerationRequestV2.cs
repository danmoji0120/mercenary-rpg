using System;
using Godot;

namespace WorldV2;

public sealed class WorldGenerationRequestV2
{
    public WorldGenerationRequestV2(
        WorldMapSizePresetV2 mapSizePreset,
        int seed,
        WorldPlanVersionV2 planVersion,
        Vector2I? optionalStartCell = null,
        string? worldId = null)
    {
        MapSizePreset = mapSizePreset;
        MapSize = WorldMapSizeDefinitionV2.FromPreset(mapSizePreset);
        Seed = seed;
        PlanVersion = planVersion;
        OptionalStartCell = optionalStartCell;
        WorldId = string.IsNullOrWhiteSpace(worldId)
            ? $"world_{seed}_{mapSizePreset}_{planVersion}"
            : worldId;
        CreatedAtUtc = DateTime.UtcNow;
        DebugId = $"{WorldId}_{CreatedAtUtc:yyyyMMddHHmmss}";
    }

    public WorldMapSizePresetV2 MapSizePreset { get; }
    public WorldMapSizeDefinitionV2 MapSize { get; }
    public int Seed { get; }
    public WorldPlanVersionV2 PlanVersion { get; }
    public Vector2I? OptionalStartCell { get; }
    public string WorldId { get; }
    public DateTime CreatedAtUtc { get; }
    public string DebugId { get; }
    public Vector2I StartCell => OptionalStartCell ?? MapSize.CenterCell;

    public static WorldGenerationRequestV2 CreateDevDefault(int seed)
    {
        WorldMapSizeDefinitionV2 size = WorldMapSizeDefinitionV2.FromPreset(WorldMapSizePresetV2.Small);
        return new WorldGenerationRequestV2(
            WorldMapSizePresetV2.Small,
            seed,
            WorldPlanVersionV2.V3,
            size.CenterCell,
            "world_v2_dev");
    }
}
