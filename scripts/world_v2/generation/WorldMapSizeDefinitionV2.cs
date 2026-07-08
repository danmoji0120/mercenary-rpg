using Godot;

namespace WorldV2;

public readonly struct WorldMapSizeDefinitionV2
{
    public WorldMapSizeDefinitionV2(WorldMapSizePresetV2 preset, int widthCells, int heightCells)
    {
        Preset = preset;
        WidthCells = widthCells;
        HeightCells = heightCells;
        ChunkWidth = Mathf.CeilToInt(widthCells / (float)WorldV2CoordinateUtility.ChunkSizeCells);
        ChunkHeight = Mathf.CeilToInt(heightCells / (float)WorldV2CoordinateUtility.ChunkSizeCells);
        TotalChunkCount = ChunkWidth * ChunkHeight;
        CenterCell = new Vector2I(widthCells / 2, heightCells / 2);
    }

    public WorldMapSizePresetV2 Preset { get; }
    public int WidthCells { get; }
    public int HeightCells { get; }
    public int ChunkWidth { get; }
    public int ChunkHeight { get; }
    public int TotalChunkCount { get; }
    public Vector2I CenterCell { get; }
    public Rect2I CellBounds => new(Vector2I.Zero, new Vector2I(WidthCells, HeightCells));
    public Rect2I ChunkBounds => new(Vector2I.Zero, new Vector2I(ChunkWidth, ChunkHeight));

    public bool ContainsCell(Vector2I globalCell)
    {
        return globalCell.X >= 0
            && globalCell.Y >= 0
            && globalCell.X < WidthCells
            && globalCell.Y < HeightCells;
    }

    public bool ContainsChunk(Vector2I globalChunk)
    {
        return globalChunk.X >= 0
            && globalChunk.Y >= 0
            && globalChunk.X < ChunkWidth
            && globalChunk.Y < ChunkHeight;
    }

    public Vector2I ClampCell(Vector2I globalCell)
    {
        return new Vector2I(
            Mathf.Clamp(globalCell.X, 0, Mathf.Max(0, WidthCells - 1)),
            Mathf.Clamp(globalCell.Y, 0, Mathf.Max(0, HeightCells - 1)));
    }

    public static WorldMapSizeDefinitionV2 FromPreset(WorldMapSizePresetV2 preset)
    {
        return preset switch
        {
            WorldMapSizePresetV2.Small => new WorldMapSizeDefinitionV2(preset, 2048, 2048),
            WorldMapSizePresetV2.Medium => new WorldMapSizeDefinitionV2(preset, 4096, 4096),
            WorldMapSizePresetV2.Large => new WorldMapSizeDefinitionV2(preset, 8192, 8192),
            _ => new WorldMapSizeDefinitionV2(WorldMapSizePresetV2.Small, 2048, 2048)
        };
    }
}
