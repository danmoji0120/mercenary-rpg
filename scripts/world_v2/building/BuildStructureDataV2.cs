using Godot;

namespace WorldV2;

public sealed class BuildStructureDataV2
{
    public string WorldId { get; init; } = string.Empty;
    public string OwnerId { get; init; } = string.Empty;
    public Vector2I GlobalCellCoord { get; init; }
    public Vector2I GlobalChunkCoord { get; init; }
    public Vector2I SectorCoord { get; init; }
    public Vector2I LocalCellCoord { get; init; }
    public Vector2I ChunkCoord { get; init; }
    public BuildStructureTypeV2 StructureType { get; init; }
}
