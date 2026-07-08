using Godot;

public sealed class SectorMetadata
{
    public string WorldId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public int WorldSeed { get; set; }
    public Vector2I SectorCoord { get; set; }
    public int SectorSeed { get; set; }
    public SectorType Type { get; set; } = SectorType.Wasteland;
    public bool IsGenerated { get; set; }
    public bool IsDiscovered { get; set; }
    public bool IsVisited { get; set; }
    public float DangerLevel { get; set; }
    public float ResourceRichness { get; set; }
    public bool IsCentralTownSector { get; set; }
    public bool IsBuildRestricted { get; set; }
}
