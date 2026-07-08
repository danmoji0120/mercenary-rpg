using System.Collections.Generic;
using Godot;

public sealed class SectorGenerationData
{
    public SectorGenerationData(SectorMetadata metadata, int displayWidth, int displayHeight)
    {
        Metadata = metadata;
        DisplayWidth = displayWidth;
        DisplayHeight = displayHeight;
    }

    public SectorMetadata Metadata { get; }
    public int DisplayWidth { get; }
    public int DisplayHeight { get; }
    public Dictionary<Vector2I, Color> TerrainColors { get; } = new();
    public HashSet<Vector2I> BuildRestrictedCells { get; } = new();
    public List<GeneratedResourceNodeData> ResourceNodes { get; } = new();
}
