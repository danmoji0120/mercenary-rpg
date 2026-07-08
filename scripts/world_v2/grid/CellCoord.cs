using Godot;

namespace WorldV2;

public readonly struct CellCoord
{
    public CellCoord(Vector2I sectorCoord, Vector2I localCellCoord)
    {
        SectorCoord = sectorCoord;
        LocalCellCoord = localCellCoord;
        ChunkCoord = WorldV2.ChunkCoord.FromLocalCell(localCellCoord);
    }

    public Vector2I SectorCoord { get; }
    public Vector2I ChunkCoord { get; }
    public Vector2I LocalCellCoord { get; }

    public override string ToString()
    {
        return $"sector={SectorCoord} chunk={ChunkCoord} cell={LocalCellCoord}";
    }
}
