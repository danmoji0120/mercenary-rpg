using System.Collections.Generic;
using Godot;

namespace WorldV2;

public sealed class ChunkDataV2
{
    public const int ChunkSize = 32;
    public const int CellCount = ChunkSize * ChunkSize;

    private readonly CellData?[] _cells = new CellData?[CellCount];

    public ChunkDataV2(Vector2I globalChunkCoord, Vector2I sectorCoord, Vector2I localChunkCoordInSector)
    {
        GlobalChunkCoord = globalChunkCoord;
        SectorCoord = sectorCoord;
        LocalChunkCoordInSector = localChunkCoordInSector;
        OriginGlobalCell = WorldV2CoordinateUtility.GlobalChunkToOriginGlobalCell(globalChunkCoord);
    }

    public Vector2I GlobalChunkCoord { get; }
    public Vector2I SectorCoord { get; }
    public Vector2I LocalChunkCoordInSector { get; }
    public Vector2I OriginGlobalCell { get; }
    public int FilledCellCount { get; private set; }
    public List<NaturalResourceSpawnDescriptorV3> NaturalResourceSpawns { get; } = new();
    public ResourcePlacementChunkDiagnosticsV3? ResourcePlacementDiagnostics { get; set; }
    public IReadOnlyList<ResourceEcologyCapacitySnapshotV3> ResourceEcologyCapacities => ResourcePlacementDiagnostics?.EcologyCapacities ?? System.Array.Empty<ResourceEcologyCapacitySnapshotV3>();

    public static int ToIndex(int localX, int localY)
    {
        return localY * ChunkSize + localX;
    }

    public static bool IsInBounds(int localX, int localY)
    {
        return localX >= 0 && localX < ChunkSize && localY >= 0 && localY < ChunkSize;
    }

    public CellData? GetCellLocal(int localX, int localY)
    {
        return IsInBounds(localX, localY)
            ? _cells[ToIndex(localX, localY)]
            : null;
    }

    public void SetCellLocal(int localX, int localY, CellData cell)
    {
        if (!IsInBounds(localX, localY))
        {
            return;
        }

        int index = ToIndex(localX, localY);
        if (_cells[index] == null)
        {
            FilledCellCount++;
        }

        _cells[index] = cell;
    }

    public bool TryGetCellLocal(int localX, int localY, out CellData? cell)
    {
        cell = GetCellLocal(localX, localY);
        return cell != null;
    }

    public CellData? GetCellGlobal(Vector2I globalCellCoord)
    {
        Vector2I local = globalCellCoord - OriginGlobalCell;
        return GetCellLocal(local.X, local.Y);
    }

    public void SetCellGlobal(Vector2I globalCellCoord, CellData cell)
    {
        Vector2I local = globalCellCoord - OriginGlobalCell;
        SetCellLocal(local.X, local.Y, cell);
    }

    public bool TryGetCell(Vector2I globalCellCoord, out CellData? cell)
    {
        cell = GetCellGlobal(globalCellCoord);
        return cell != null;
    }

    public IEnumerable<CellData> EnumerateCells()
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] != null)
            {
                yield return _cells[i]!;
            }
        }
    }
}
