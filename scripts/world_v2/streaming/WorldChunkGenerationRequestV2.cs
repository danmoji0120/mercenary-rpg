using Godot;

namespace WorldV2;

public readonly struct WorldChunkGenerationRequestV2
{
    public WorldChunkGenerationRequestV2(Vector2I globalChunkCoord, WorldChunkGenerationRequestTypeV2 requestType, int priority)
    {
        GlobalChunkCoord = globalChunkCoord;
        RequestType = requestType;
        Priority = priority;
    }

    public Vector2I GlobalChunkCoord { get; }
    public WorldChunkGenerationRequestTypeV2 RequestType { get; }
    public int Priority { get; }
}
