using System.Collections.Generic;
using Godot;

public sealed class WorldChunkData
{
    public WorldChunkData(string worldId, Vector2I sectorCoord, Vector2I chunkCoord, int chunkSeed)
    {
        WorldId = worldId ?? string.Empty;
        SectorCoord = sectorCoord;
        ChunkCoord = chunkCoord;
        ChunkSeed = chunkSeed;
    }

    public string WorldId { get; }
    public Vector2I SectorCoord { get; }
    public Vector2I ChunkCoord { get; }
    public int ChunkSeed { get; }
    public Dictionary<Vector2I, Color> TerrainColors { get; } = new();
    public HashSet<Vector2I> BuildRestrictedCells { get; } = new();
    public List<GeneratedResourceNodeData> ResourceNodes { get; } = new();
}
