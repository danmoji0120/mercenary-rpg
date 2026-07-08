using Godot;

namespace WorldV2;

public sealed class WorldChunkGenerationResultV2
{
    public Vector2I GlobalChunkCoord { get; init; }
    public WorldChunkGenerationRequestTypeV2 RequestType { get; init; }
    public ChunkDataV2? ChunkData { get; init; }
    public double ElapsedMs { get; init; }
    public bool Success { get; init; }
    public string Error { get; init; } = string.Empty;
}
