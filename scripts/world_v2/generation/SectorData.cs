using System.Collections.Generic;
using Godot;

namespace WorldV2;

public sealed class SectorData
{
    public SectorData(SectorMetadata metadata, int widthCells, int heightCells)
    {
        Metadata = metadata;
        WidthCells = Mathf.Max(1, widthCells);
        HeightCells = Mathf.Max(1, heightCells);
    }

    public SectorMetadata Metadata { get; }
    public int WidthCells { get; }
    public int HeightCells { get; }
    public Dictionary<Vector2I, CellData> Cells { get; } = new();

    public bool TryGetCell(Vector2I localCellCoord, out CellData? cell)
    {
        return Cells.TryGetValue(localCellCoord, out cell);
    }

    public bool ContainsCell(Vector2I localCellCoord)
    {
        return localCellCoord.X >= 0
            && localCellCoord.Y >= 0
            && localCellCoord.X < WidthCells
            && localCellCoord.Y < HeightCells;
    }
}
