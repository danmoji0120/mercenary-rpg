using System.Collections.Generic;
using Godot;

namespace WorldV2;

public partial class WorldV2BuildManager : Node2D
{
    [Export]
    public string DefaultOwnerId { get; set; } = "local_player";

    [Export]
    public NodePath GridRendererPath { get; set; } = "../GridLayer";

    [Export]
    public NodePath WorldManagerPath { get; set; } = "../WorldManagerV2";

    [Export]
    public NodePath StreamManagerPath { get; set; } = "../WorldStreamManagerV2";

    public BuildStructureTypeV2 SelectedBuildType { get; set; } = BuildStructureTypeV2.Floor;

    private readonly Dictionary<Vector2I, BuildStructureDataV2> _structuresByGlobalCell = new();
    private WorldV2GridRenderer? _gridRenderer;
    private WorldManagerV2? _worldManager;
    private WorldStreamManagerV2? _streamManager;
    private Vector2I _activeSectorCoord = Vector2I.Zero;
    private SectorRuntimeState? _activeRuntimeState;

    public override void _Ready()
    {
        _gridRenderer = GetNodeOrNull<WorldV2GridRenderer>(GridRendererPath);
        _worldManager = GetNodeOrNull<WorldManagerV2>(WorldManagerPath);
        _streamManager = GetNodeOrNull<WorldStreamManagerV2>(StreamManagerPath);
    }

    public void SetActiveSector(Vector2I sectorCoord)
    {
        _activeSectorCoord = sectorCoord;
        _activeRuntimeState = new SectorRuntimeState(sectorCoord);
        QueueRedraw();
    }

    public void SetActiveSector(Vector2I sectorCoord, SectorRuntimeState runtimeState)
    {
        _activeSectorCoord = sectorCoord;
        _activeRuntimeState = runtimeState;
        QueueRedraw();
    }

    public bool TryPlaceStructure(Vector2I localCellCoord, BuildStructureTypeV2 structureType, string ownerId, out string reason)
    {
        reason = string.Empty;
        _gridRenderer ??= GetNodeOrNull<WorldV2GridRenderer>(GridRendererPath);
        _worldManager ??= GetNodeOrNull<WorldManagerV2>(WorldManagerPath);
        _streamManager ??= GetNodeOrNull<WorldStreamManagerV2>(StreamManagerPath);

        if (_gridRenderer == null || _worldManager == null || _streamManager == null)
        {
            reason = "WorldV2 references are missing.";
            return false;
        }

        Vector2I globalCellCoord = localCellCoord;
        if (!_streamManager.TryGetLoadedCell(globalCellCoord, out CellData? cell) || cell == null)
        {
            reason = "Cell is outside the streamed area.";
            return false;
        }

        if (cell.IsBuildRestricted)
        {
            reason = "Build restricted cell.";
            return false;
        }

        if (_structuresByGlobalCell.ContainsKey(globalCellCoord))
        {
            reason = "A structure already exists on this cell.";
            return false;
        }

        Vector2I sectorCoord = WorldV2CoordinateUtility.GlobalCellToSectorCoord(globalCellCoord);
        Vector2I localCellInSector = WorldV2CoordinateUtility.GlobalCellToLocalCellInSector(globalCellCoord);
        Vector2I localChunkInSector = WorldV2CoordinateUtility.GlobalChunkToLocalChunkInSector(WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(globalCellCoord));
        SectorRuntimeState runtimeState = _worldManager.GetOrCreateSectorRuntimeState(sectorCoord);
        if (runtimeState.StructuresByCell.ContainsKey(localCellInSector))
        {
            reason = "A structure already exists on this cell.";
            return false;
        }

        string resolvedOwnerId = string.IsNullOrWhiteSpace(ownerId) ? DefaultOwnerId : ownerId;
        runtimeState.StructuresByCell[localCellInSector] = new BuildStructureDataV2
        {
            WorldId = _worldManager.WorldId,
            OwnerId = resolvedOwnerId,
            GlobalCellCoord = globalCellCoord,
            GlobalChunkCoord = WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(globalCellCoord),
            SectorCoord = sectorCoord,
            LocalCellCoord = localCellInSector,
            ChunkCoord = localChunkInSector,
            StructureType = structureType
        };
        cell.OwnerId = resolvedOwnerId;
        _structuresByGlobalCell[globalCellCoord] = runtimeState.StructuresByCell[localCellInSector];
        runtimeState.MarkDirty();
        _streamManager.MarkChunkDirty(WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(globalCellCoord));

        QueueRedraw();
        return true;
    }

    public bool TryRemoveStructure(Vector2I globalCellCoord)
    {
        Vector2I sectorCoord = WorldV2CoordinateUtility.GlobalCellToSectorCoord(globalCellCoord);
        Vector2I localCellInSector = WorldV2CoordinateUtility.GlobalCellToLocalCellInSector(globalCellCoord);
        SectorRuntimeState runtimeState = _worldManager?.GetOrCreateSectorRuntimeState(sectorCoord)
            ?? GetActiveRuntimeState();
        bool removed = _structuresByGlobalCell.Remove(globalCellCoord);
        removed = runtimeState.StructuresByCell.Remove(localCellInSector) || removed;

        if (removed)
        {
            if (_streamManager?.TryGetLoadedCell(globalCellCoord, out CellData? cell) == true && cell != null)
            {
                cell.OwnerId = string.Empty;
            }

            runtimeState.MarkDirty();
            _streamManager?.MarkChunkDirty(WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(globalCellCoord));
            QueueRedraw();
        }

        return removed;
    }

    public IReadOnlyDictionary<Vector2I, BuildStructureDataV2> GetActiveSectorStructures()
    {
        return GetActiveRuntimeState().StructuresByCell;
    }

    public int GetActiveSectorStructureCount()
    {
        return GetActiveRuntimeState().StructureCount;
    }

    public int GetRuntimeStructureCount()
    {
        return _structuresByGlobalCell.Count;
    }

    public void ClearAllRuntimeStructures()
    {
        _structuresByGlobalCell.Clear();
        QueueRedraw();
    }

    public override void _Draw()
    {
        _gridRenderer ??= GetNodeOrNull<WorldV2GridRenderer>(GridRendererPath);
        if (_gridRenderer == null)
        {
            return;
        }

        foreach (BuildStructureDataV2 structure in _structuresByGlobalCell.Values)
        {
            Rect2 rect = new(
                structure.GlobalCellCoord.X * _gridRenderer.TileSize,
                structure.GlobalCellCoord.Y * _gridRenderer.TileSize,
                _gridRenderer.TileSize,
                _gridRenderer.TileSize);

            Color color = structure.StructureType == BuildStructureTypeV2.Wall
                ? new Color(0.36f, 0.33f, 0.29f)
                : new Color(0.55f, 0.48f, 0.35f);

            DrawRect(rect.Grow(-2.0f), color);
        }
    }

    private SectorRuntimeState GetActiveRuntimeState()
    {
        if (_activeRuntimeState != null)
        {
            return _activeRuntimeState;
        }

        _activeRuntimeState = _worldManager?.GetOrCreateSectorRuntimeState(_activeSectorCoord)
            ?? new SectorRuntimeState(_activeSectorCoord);
        return _activeRuntimeState;
    }

}
