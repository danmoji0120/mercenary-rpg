using Godot;

namespace WorldV2;

public static class WorldV2CoordinateUtility
{
    public const int ChunkSizeCells = 32;
    public const int SectorSizeChunks = 16;
    public const int SectorSizeCells = ChunkSizeCells * SectorSizeChunks;

    public static Vector2I GlobalCellToSectorCoord(Vector2I globalCellCoord)
    {
        return new Vector2I(
            FloorDiv(globalCellCoord.X, SectorSizeCells),
            FloorDiv(globalCellCoord.Y, SectorSizeCells));
    }

    public static Vector2I GlobalCellToLocalCellInSector(Vector2I globalCellCoord)
    {
        return new Vector2I(
            FloorMod(globalCellCoord.X, SectorSizeCells),
            FloorMod(globalCellCoord.Y, SectorSizeCells));
    }

    public static Vector2I GlobalCellToGlobalChunkCoord(Vector2I globalCellCoord)
    {
        return new Vector2I(
            FloorDiv(globalCellCoord.X, ChunkSizeCells),
            FloorDiv(globalCellCoord.Y, ChunkSizeCells));
    }

    public static Vector2I GlobalChunkToSectorCoord(Vector2I globalChunkCoord)
    {
        return new Vector2I(
            FloorDiv(globalChunkCoord.X, SectorSizeChunks),
            FloorDiv(globalChunkCoord.Y, SectorSizeChunks));
    }

    public static Vector2I GlobalChunkToLocalChunkInSector(Vector2I globalChunkCoord)
    {
        return new Vector2I(
            FloorMod(globalChunkCoord.X, SectorSizeChunks),
            FloorMod(globalChunkCoord.Y, SectorSizeChunks));
    }

    public static Vector2I GlobalChunkToOriginGlobalCell(Vector2I globalChunkCoord)
    {
        return globalChunkCoord * ChunkSizeCells;
    }

    public static Vector2I SectorAndLocalCellToGlobalCell(Vector2I sectorCoord, Vector2I localCellCoord)
    {
        return sectorCoord * SectorSizeCells + localCellCoord;
    }

    public static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;

        if (remainder != 0 && ((remainder < 0) != (divisor < 0)))
        {
            quotient--;
        }

        return quotient;
    }

    public static int FloorMod(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + Mathf.Abs(divisor) : result;
    }
}
