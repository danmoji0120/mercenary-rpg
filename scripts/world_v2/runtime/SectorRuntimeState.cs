using System.Collections.Generic;
using Godot;

namespace WorldV2;

public sealed class SectorRuntimeState
{
    public SectorRuntimeState(Vector2I sectorCoord)
    {
        SectorCoord = sectorCoord;
    }

    public Vector2I SectorCoord { get; }
    public Dictionary<Vector2I, BuildStructureDataV2> StructuresByCell { get; } = new();
    public Dictionary<Vector2I, TileType> TileOverrides { get; } = new();
    public HashSet<Vector2I> RemovedProceduralObjects { get; } = new();
    public bool IsDirty { get; private set; }

    public int StructureCount => StructuresByCell.Count;
    public int TileOverrideCount => TileOverrides.Count;
    public int RemovedProceduralObjectCount => RemovedProceduralObjects.Count;

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public void MarkClean()
    {
        IsDirty = false;
    }
}
