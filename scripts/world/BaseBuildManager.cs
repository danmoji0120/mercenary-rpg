using System.Collections.Generic;
using Godot;

public partial class BaseBuildManager : Node2D
{
    [Export]
    public bool ShowBlockedLineDebug { get; set; } = false;

    [Export]
    public int InitialFoodCount { get; set; } = 20;

    [Export]
    public int InitialWood { get; set; } = 100;

    [Export]
    public int InitialStone { get; set; } = 80;

    [Export]
    public int InitialMetal { get; set; } = 30;

    [Export]
    public int FoodLowThreshold { get; set; } = 5;

    [Export]
    public int StorageCapacityPerResource { get; set; } = 100;

    [Export]
    public bool DebugStorageInventory { get; set; } = false;

    [Export]
    public bool DebugResourcePile { get; set; } = false;

    [Export]
    public bool DebugFarmZone { get; set; } = false;

    public int FoodCount => GetResourceAmount(BaseResourceType.Food);
    public TileBuildType CurrentBuildMode { get; private set; } = TileBuildType.None;
    public bool HasHoverCell => _hoverCell.HasValue;
    public bool HoverCellInWorld => _hoverCellInWorld;
    public bool CanPlaceOnHoverCell => _canPlaceOnHoverCell;

    private readonly Dictionary<Vector2I, BuildableTileState> _buildings = new();
    private readonly Dictionary<Vector2I, MercenaryController> _facilityReservations = new();
    private readonly Dictionary<Vector2I, MercenaryController> _facilityOccupants = new();
    private readonly Dictionary<BaseResourceType, int> _resources = new();
    private readonly Dictionary<Vector2I, Dictionary<BaseResourceType, int>> _storageInventories = new();
    private readonly HashSet<Vector2I> _farmZoneCells = new();
    private static readonly BaseResourceType[] BuildCostResourceOrder =
    {
        BaseResourceType.Wood,
        BaseResourceType.Stone,
        BaseResourceType.Metal,
        BaseResourceType.Food
    };

    private static readonly Dictionary<TileBuildType, BuildCost> BuildCosts = new()
    {
        { TileBuildType.Floor, CreateBuildCost((BaseResourceType.Wood, 1)) },
        { TileBuildType.Wall, CreateBuildCost((BaseResourceType.Stone, 2)) },
        { TileBuildType.Door, CreateBuildCost((BaseResourceType.Wood, 2), (BaseResourceType.Metal, 1)) },
        { TileBuildType.Bed, CreateBuildCost((BaseResourceType.Wood, 5)) },
        { TileBuildType.Storage, CreateBuildCost((BaseResourceType.Wood, 4)) },
        { TileBuildType.GuardPost, CreateBuildCost((BaseResourceType.Wood, 3), (BaseResourceType.Stone, 2)) },
        { TileBuildType.Erase, CreateBuildCost() }
    };
    private WorldGridRenderer? _worldGrid;
    private Node2D? _resourcePileLayer;
    private Node2D? _cropPlantLayer;
    private Vector2I? _hoverCell;
    private bool _hoverCellInWorld;
    private bool _canPlaceOnHoverCell;
    private bool _hasBlockedLineDebug;
    private Vector2 _blockedLineDebugFrom;
    private Vector2 _blockedLineDebugTo;
    private Vector2I _blockedLineDebugCell;

    public override void _Ready()
    {
        _worldGrid = GetNodeOrNull<WorldGridRenderer>("../TerrainLayer");
        _resourcePileLayer = GetNodeOrNull<Node2D>("../ResourcePileLayer");
        _cropPlantLayer = GetNodeOrNull<Node2D>("../CropPlantLayer");
        InitializeResources();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        UpdateHoverPreview(GetGlobalMousePosition());
    }

    public void ToggleBuildMode()
    {
        SetBuildMode(CurrentBuildMode == TileBuildType.None ? TileBuildType.Floor : TileBuildType.None);
    }

    public void SetBuildMode(TileBuildType buildType)
    {
        CurrentBuildMode = buildType;
        UpdateHoverPreview(GetGlobalMousePosition());
    }

    public bool TryApplyBuildAtWorldPosition(Vector2 worldPosition)
    {
        return TryApplyBuildAtCell(CurrentBuildMode, WorldToCell(worldPosition));
    }

    public bool TryApplyBuildAtCell(TileBuildType buildType, Vector2I cell)
    {
        if (!CanBuild(buildType, cell))
        {
            if (buildType != TileBuildType.None && CanPlaceAt(cell, buildType) && !CanAffordBuild(buildType))
            {
                GD.Print($"Build blocked: {GetBuildAffordLabel(buildType)}");
            }

            return false;
        }

        if (buildType == TileBuildType.Erase)
        {
            // TODO: Add erase refund, partial refund, dismantle jobs, and resource drops.
            bool removed = TryRemoveDepletedResourceNodeAt(cell) || EraseAt(cell);
            QueueRedraw();
            return removed;
        }

        if (!TryConsumeBuildCost(buildType))
        {
            GD.Print($"Build blocked: {GetBuildAffordLabel(buildType)}");
            return false;
        }

        ApplyBuildAt(cell, buildType);
        QueueRedraw();
        return true;
    }

    public bool CanBuild(TileBuildType buildType, Vector2I cell)
    {
        return CanPlaceAt(cell, buildType) && CanAffordBuild(buildType);
    }

    public string GetBuildBlockReason(TileBuildType buildType, Vector2I cell)
    {
        string placementReason = GetPlacementBlockReason(buildType, cell);

        if (!string.IsNullOrEmpty(placementReason))
        {
            return placementReason;
        }

        if (!CanAffordBuild(buildType))
        {
            return GetBuildAffordLabel(buildType);
        }

        return "";
    }

    public string GetPlacementBlockReason(TileBuildType buildType, Vector2I cell)
    {
        if (buildType == TileBuildType.None)
        {
            return "Cannot place here";
        }

        if (!IsCellInWorld(cell))
        {
            return "Out of bounds";
        }

        bool hasExistingState = _buildings.TryGetValue(cell, out BuildableTileState existingState);

        if (buildType == TileBuildType.Erase)
        {
            if (IsCropPlantCell(cell))
            {
                return "Crop exists";
            }

            ResourceNode? resourceNode = GetResourceNodeAtCell(cell);

            if (resourceNode != null)
            {
                return resourceNode.CanBeRemoved ? "" : "Resource is not depleted";
            }

            if (hasExistingState && existingState.ObjectType == TileBuildType.Storage && !IsStorageInventoryEmpty(cell))
            {
                return "Storage not empty";
            }

            return hasExistingState && (existingState.HasObject || existingState.HasFloor)
                ? ""
                : "Nothing to erase";
        }

        // TODO: Revisit respawn, drops, hauling, or manual clear commands if the design needs persistent resource remains.
        if (IsResourceNodeCell(cell))
        {
            return "Resource node exists";
        }

        if (IsCropPlantCell(cell))
        {
            return "Crop exists";
        }

        if (buildType == TileBuildType.Floor)
        {
            return !hasExistingState || !existingState.HasObject
                ? ""
                : "Object already exists";
        }

        if (buildType == TileBuildType.Wall)
        {
            if (IsCellOccupied(cell))
            {
                return "Occupied by unit";
            }

            return !hasExistingState || !existingState.IsFacility
                ? ""
                : "Object already exists";
        }

        if (buildType == TileBuildType.Door)
        {
            return hasExistingState && existingState.ObjectType == TileBuildType.Wall
                ? ""
                : "Door requires Wall";
        }

        if (IsFacilityBuildType(buildType))
        {
            if (IsCellOccupied(cell))
            {
                return "Occupied by unit";
            }

            if (!hasExistingState || !existingState.HasFloor)
            {
                return "Need Floor";
            }

            return existingState.HasObject ? "Object already exists" : "";
        }

        return CanPlaceAt(cell, buildType) ? "" : "Cannot place here";
    }

    public bool CanPlaceAt(Vector2I cell, TileBuildType buildType)
    {
        if (buildType == TileBuildType.None)
        {
            return false;
        }

        if (!IsCellInWorld(cell))
        {
            return false;
        }

        if (buildType == TileBuildType.Erase)
        {
            if (IsCropPlantCell(cell))
            {
                return false;
            }

            ResourceNode? resourceNode = GetResourceNodeAtCell(cell);

            if (resourceNode != null)
            {
                return resourceNode.CanBeRemoved;
            }

            if (_buildings.TryGetValue(cell, out BuildableTileState eraseStateForStorage)
                && eraseStateForStorage.ObjectType == TileBuildType.Storage
                && !IsStorageInventoryEmpty(cell))
            {
                return false;
            }

            return _buildings.TryGetValue(cell, out BuildableTileState eraseState)
                && (eraseState.HasObject || eraseState.HasFloor);
        }

        if (IsResourceNodeCell(cell))
        {
            return false;
        }

        if (IsCropPlantCell(cell))
        {
            return false;
        }

        bool hasExistingState = _buildings.TryGetValue(cell, out BuildableTileState existingState);

        if (buildType == TileBuildType.Floor)
        {
            return !hasExistingState || !existingState.HasObject;
        }

        if (buildType == TileBuildType.Wall)
        {
            return !IsCellOccupied(cell)
                && (!hasExistingState
                    || !existingState.IsFacility);
        }

        if (buildType == TileBuildType.Door)
        {
            return hasExistingState && existingState.ObjectType == TileBuildType.Wall;
        }

        if (IsFacilityBuildType(buildType))
        {
            return !IsCellOccupied(cell)
                && hasExistingState
                && existingState.HasFloor
                && !existingState.HasObject;
        }

        return false;
    }

    public Vector2I WorldToCell(Vector2 worldPosition)
    {
        int tileSize = GetTileSize();
        return new Vector2I(Mathf.FloorToInt(worldPosition.X / tileSize), Mathf.FloorToInt(worldPosition.Y / tileSize));
    }

    public Vector2 CellToWorldCenter(Vector2I cell)
    {
        int tileSize = GetTileSize();
        return new Vector2((cell.X + 0.5f) * tileSize, (cell.Y + 0.5f) * tileSize);
    }

    public Vector2 SnapWorldToCellCenter(Vector2 worldPosition)
    {
        return CellToWorldCenter(WorldToCell(worldPosition));
    }

    public bool IsWorldPositionBlocked(Vector2 worldPosition)
    {
        return IsCellBlocked(WorldToCell(worldPosition));
    }

    public bool IsLineBlocked(Vector2 fromWorld, Vector2 toWorld)
    {
        return IsLineBlocked(fromWorld, toWorld, out _);
    }

    public bool IsLineBlocked(Vector2 fromWorld, Vector2 toWorld, out Vector2I blockedCell)
    {
        blockedCell = default;

        int tileSize = GetTileSize();
        float distance = fromWorld.DistanceTo(toWorld);
        float stepLength = Mathf.Max(1.0f, tileSize * 0.25f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / stepLength));
        Vector2I startCell = WorldToCell(fromWorld);
        HashSet<Vector2I> checkedCells = new();

        for (int i = 1; i <= steps; i++)
        {
            float weight = i / (float)steps;
            Vector2 sample = fromWorld.Lerp(toWorld, weight);
            Vector2I sampleCell = WorldToCell(sample);

            if (sampleCell == startCell || !checkedCells.Add(sampleCell))
            {
                continue;
            }

            if (!IsCellInWorld(sampleCell) || IsCellBlocked(sampleCell))
            {
                blockedCell = sampleCell;
                StoreBlockedLineDebug(fromWorld, toWorld, blockedCell);
                return true;
            }
        }

        ClearBlockedLineDebug();
        return false;
    }

    public bool IsCellBlocked(Vector2I cell)
    {
        // Door is currently always open, so it keeps BlocksMovement false and BFS can pass through it.
        // TODO: When closed/locked doors exist, treat door && !IsOpen as blocked here.
        return _buildings.TryGetValue(cell, out BuildableTileState state) && state.BlocksMovement;
    }

    public bool IsFacilityCell(Vector2I cell)
    {
        return _buildings.TryGetValue(cell, out BuildableTileState state) && state.IsFacility;
    }

    public bool IsResourceNodeCell(Vector2I cell)
    {
        return GetResourceNodeAtCell(cell) != null;
    }

    public ResourceNode? GetResourceNodeAtCell(Vector2I cell)
    {
        SceneTree? tree = GetTree();

        if (tree == null)
        {
            return null;
        }

        foreach (Node node in tree.GetNodesInGroup("resource_nodes"))
        {
            if (node is ResourceNode resourceNode
                && GodotObject.IsInstanceValid(resourceNode)
                && !resourceNode.IsQueuedForDeletion()
                && !resourceNode.IsRemoving
                && resourceNode.Cell == cell)
            {
                return resourceNode;
            }
        }

        return null;
    }

    public bool CanRemoveResourceNodeAt(Vector2I cell)
    {
        ResourceNode? resourceNode = GetResourceNodeAtCell(cell);
        return resourceNode != null && resourceNode.CanBeRemoved;
    }

    public bool TryRemoveDepletedResourceNodeAt(Vector2I cell)
    {
        ResourceNode? resourceNode = GetResourceNodeAtCell(cell);
        return resourceNode != null && resourceNode.TryRemoveDepleted();
    }

    public ResourceNode? GetDesignatableResourceNodeAtCell(Vector2I cell)
    {
        ResourceNode? resourceNode = GetResourceNodeAtCell(cell);
        return resourceNode != null && resourceNode.CanBeHarvestDesignated ? resourceNode : null;
    }

    public bool CanSetHarvestDesignationAt(Vector2I cell, bool designated)
    {
        ResourceNode? resourceNode = GetResourceNodeAtCell(cell);

        if (resourceNode == null)
        {
            return false;
        }

        return !designated || resourceNode.CanBeHarvestDesignated;
    }

    public bool TrySetHarvestDesignationAt(Vector2I cell, bool designated)
    {
        ResourceNode? resourceNode = GetResourceNodeAtCell(cell);
        return resourceNode != null && resourceNode.TrySetHarvestDesignated(designated);
    }

    public bool IsResourcePileCell(Vector2I cell)
    {
        return GetResourcePileAtCell(cell) != null;
    }

    public bool IsFarmZoneCell(Vector2I cell)
    {
        return _farmZoneCells.Contains(cell);
    }

    public IReadOnlyCollection<Vector2I> GetFarmZoneCells()
    {
        return _farmZoneCells;
    }

    public bool CanMarkFarmZoneAt(Vector2I cell)
    {
        return string.IsNullOrEmpty(GetFarmZoneBlockReason(cell));
    }

    public string GetFarmZoneBlockReason(Vector2I cell)
    {
        if (!IsCellInWorld(cell))
        {
            return "Out of bounds";
        }

        if (IsResourceNodeCell(cell))
        {
            return "Resource node exists";
        }

        if (!_buildings.TryGetValue(cell, out BuildableTileState state) || !state.HasFloor)
        {
            return "Need Floor";
        }

        if (state.HasObject)
        {
            return "Object exists";
        }

        return "";
    }

    public bool TrySetFarmZoneAt(Vector2I cell, bool enabled)
    {
        if (!enabled)
        {
            bool removed = _farmZoneCells.Remove(cell);

            if (removed)
            {
                QueueRedraw();
            }

            return removed;
        }

        if (!CanMarkFarmZoneAt(cell))
        {
            return false;
        }

        bool added = _farmZoneCells.Add(cell);

        if (added && DebugFarmZone)
        {
            GD.Print($"Farm zone marked at {cell}");
        }

        QueueRedraw();
        return added || IsFarmZoneCell(cell);
    }

    public void ClearFarmZoneAt(Vector2I cell)
    {
        if (_farmZoneCells.Remove(cell))
        {
            QueueRedraw();
        }
    }

    public ResourcePile? GetResourcePileAtCell(Vector2I cell, BaseResourceType? type = null)
    {
        foreach (ResourcePile pile in GetAllResourcePiles())
        {
            if (pile.Cell == cell && (!type.HasValue || pile.ResourceType == type.Value))
            {
                return pile;
            }
        }

        return null;
    }

    public IReadOnlyList<ResourcePile> GetAllResourcePiles()
    {
        List<ResourcePile> piles = new();
        SceneTree? tree = GetTree();

        if (tree == null)
        {
            return piles;
        }

        foreach (Node node in tree.GetNodesInGroup("resource_piles"))
        {
            if (node is ResourcePile pile
                && GodotObject.IsInstanceValid(pile)
                && !pile.IsQueuedForDeletion()
                && !pile.IsRemoving
                && !pile.IsEmpty)
            {
                piles.Add(pile);
            }
        }

        return piles;
    }

    public bool TrySpawnOrMergeResourcePile(BaseResourceType type, Vector2I cell, int amount)
    {
        if (amount <= 0 || !IsConstructionResource(type))
        {
            return false;
        }

        Vector2I pileCell = FindResourcePileSpawnCell(type, cell);

        if (!IsCellInWorld(pileCell))
        {
            return false;
        }

        ResourcePile? existingPile = GetResourcePileAtCell(pileCell, type);

        if (existingPile != null)
        {
            existingPile.AddAmount(amount);

            if (DebugResourcePile)
            {
                GD.Print($"Merged resource pile {type} +{amount} at {pileCell}");
            }

            return true;
        }

        if (_resourcePileLayer == null)
        {
            GD.PushWarning("ResourcePileLayer not found. Cannot spawn resource pile.");
            return false;
        }

        ResourcePile pile = new();
        pile.Initialize(type, pileCell, amount);
        pile.Position = CellToWorldCenter(pileCell);
        _resourcePileLayer.AddChild(pile);

        if (DebugResourcePile)
        {
            GD.Print($"Spawned resource pile {type} x{amount} at {pileCell}");
        }

        return true;
    }

    public ResourcePile? FindNearestHaulablePile(Vector2I fromCell, MercenaryLifeAI lifeAI)
    {
        ResourcePile? nearestPile = null;
        int nearestDistance = int.MaxValue;

        foreach (ResourcePile pile in GetAllResourcePiles())
        {
            if (pile.IsReservedForHaul && !pile.IsReservedBy(lifeAI))
            {
                continue;
            }

            if (!TryFindNearestStorageAccessWithSpace(pile.Cell, pile.ResourceType, out _, out _))
            {
                continue;
            }

            int distance = Mathf.Abs(fromCell.X - pile.Cell.X) + Mathf.Abs(fromCell.Y - pile.Cell.Y);

            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearestPile = pile;
        }

        return nearestPile;
    }

    public bool IsStorageCell(Vector2I cell)
    {
        return _buildings.TryGetValue(cell, out BuildableTileState state)
            && state.ObjectType == TileBuildType.Storage;
    }

    public int GetStoredAmountAt(Vector2I storageCell, BaseResourceType type)
    {
        return _storageInventories.TryGetValue(storageCell, out Dictionary<BaseResourceType, int>? inventory)
            && inventory.TryGetValue(type, out int amount)
                ? amount
                : 0;
    }

    public int GetTotalStoredAmount(BaseResourceType type)
    {
        int total = 0;

        foreach (Dictionary<BaseResourceType, int> inventory in _storageInventories.Values)
        {
            if (inventory.TryGetValue(type, out int amount))
            {
                total += amount;
            }
        }

        return total;
    }

    public int GetStorageFreeSpace(Vector2I storageCell, BaseResourceType type)
    {
        if (!IsStorageCell(storageCell) || !IsStoredResource(type))
        {
            return 0;
        }

        return Mathf.Max(0, StorageCapacityPerResource - GetStoredAmountAt(storageCell, type));
    }

    public bool TryAddResourceToStorage(Vector2I storageCell, BaseResourceType type, int amount, out int storedAmount, out int leftoverAmount)
    {
        storedAmount = 0;
        leftoverAmount = Mathf.Max(0, amount);

        if (amount <= 0 || !IsStorageCell(storageCell) || !IsStoredResource(type))
        {
            return false;
        }

        RegisterStorage(storageCell);
        int freeSpace = GetStorageFreeSpace(storageCell, type);
        storedAmount = Mathf.Min(amount, freeSpace);
        leftoverAmount = amount - storedAmount;

        if (storedAmount <= 0)
        {
            return false;
        }

        _storageInventories[storageCell][type] = GetStoredAmountAt(storageCell, type) + storedAmount;

        if (DebugStorageInventory)
        {
            GD.Print($"Stored {type} x{storedAmount} at Storage {storageCell}");
        }

        return true;
    }

    public bool TryRemoveResourceFromStorages(BaseResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (GetTotalStoredAmount(type) < amount)
        {
            return false;
        }

        int remaining = amount;
        List<Vector2I> storageCells = new(_storageInventories.Keys);

        foreach (Vector2I storageCell in storageCells)
        {
            int storedAmount = GetStoredAmountAt(storageCell, type);
            int removedAmount = Mathf.Min(storedAmount, remaining);

            if (removedAmount <= 0)
            {
                continue;
            }

            _storageInventories[storageCell][type] = storedAmount - removedAmount;
            remaining -= removedAmount;

            if (remaining <= 0)
            {
                return true;
            }
        }

        return remaining <= 0;
    }

    public Vector2I? FindNearestStorageWithSpace(Vector2I fromCell, BaseResourceType type)
    {
        return TryFindNearestStorageAccessWithSpace(fromCell, type, out Vector2I storageCell, out _)
            ? storageCell
            : null;
    }

    public bool TryFindNearestStorageAccessWithSpace(Vector2I fromCell, BaseResourceType type, out Vector2I storageCell, out Vector2I accessCell)
    {
        Vector2I? nearestStorageCell = null;
        Vector2I? nearestAccessCell = null;
        int nearestPathLength = int.MaxValue;

        foreach (Vector2I candidateStorageCell in GetStorageCellsWithSpace(type))
        {
            foreach (Vector2I candidateAccessCell in GetStorageAccessCells(candidateStorageCell))
            {
                if (!IsCellInWorld(candidateAccessCell) || IsCellBlocked(candidateAccessCell))
                {
                    continue;
                }

                List<Vector2I> path = GridPathfinder.FindPath(fromCell, candidateAccessCell, this);

                if (fromCell != candidateAccessCell && path.Count == 0)
                {
                    continue;
                }

                int pathLength = fromCell == candidateAccessCell ? 0 : path.Count;

                if (pathLength >= nearestPathLength)
                {
                    continue;
                }

                nearestPathLength = pathLength;
                nearestStorageCell = candidateStorageCell;
                nearestAccessCell = candidateAccessCell;
            }
        }

        if (!nearestStorageCell.HasValue || !nearestAccessCell.HasValue)
        {
            storageCell = default;
            accessCell = default;
            return false;
        }

        storageCell = nearestStorageCell.Value;
        accessCell = nearestAccessCell.Value;
        return true;
    }

    public IReadOnlyList<Vector2I> GetStorageCellsWithSpace(BaseResourceType type)
    {
        List<Vector2I> storageCells = new();

        foreach (Vector2I storageCell in _storageInventories.Keys)
        {
            if (GetStorageFreeSpace(storageCell, type) > 0)
            {
                storageCells.Add(storageCell);
            }
        }

        return storageCells;
    }

    public FacilityType GetFacilityTypeAt(Vector2I cell)
    {
        return _buildings.TryGetValue(cell, out BuildableTileState state)
            ? GetFacilityTypeForObject(state.ObjectType)
            : FacilityType.None;
    }

    public List<FacilityInfo> GetFacilities()
    {
        List<FacilityInfo> facilities = new();

        foreach (BuildableTileState state in _buildings.Values)
        {
            FacilityType facilityType = GetFacilityTypeForObject(state.ObjectType);

            if (facilityType == FacilityType.None)
            {
                continue;
            }

            facilities.Add(CreateFacilityInfo(state, facilityType));
        }

        return facilities;
    }

    public List<FacilityInfo> GetFacilitiesByType(FacilityType type)
    {
        List<FacilityInfo> facilities = new();

        if (type == FacilityType.None)
        {
            return facilities;
        }

        foreach (BuildableTileState state in _buildings.Values)
        {
            FacilityType facilityType = GetFacilityTypeForObject(state.ObjectType);

            if (facilityType != type)
            {
                continue;
            }

            facilities.Add(CreateFacilityInfo(state, facilityType));
        }

        return facilities;
    }

    public bool IsFacilityReserved(Vector2I cell)
    {
        PruneInvalidFacilityReservation(cell);
        return _facilityReservations.ContainsKey(cell);
    }

    public bool IsFacilityReservedBy(Vector2I cell, MercenaryController mercenary)
    {
        PruneInvalidFacilityReservation(cell);
        return _facilityReservations.TryGetValue(cell, out MercenaryController? reservedBy) && reservedBy == mercenary;
    }

    public bool TryReserveFacility(Vector2I cell, MercenaryController mercenary)
    {
        if (!IsFacilityCell(cell))
        {
            return false;
        }

        PruneInvalidFacilityReservation(cell);
        PruneInvalidFacilityOccupancy(cell);

        if (_facilityOccupants.TryGetValue(cell, out MercenaryController? occupant) && occupant != mercenary)
        {
            return false;
        }

        if (_facilityReservations.TryGetValue(cell, out MercenaryController? reservedBy))
        {
            return reservedBy == mercenary;
        }

        _facilityReservations[cell] = mercenary;
        return true;
    }

    public void ReleaseFacilityReservation(Vector2I cell, MercenaryController mercenary)
    {
        PruneInvalidFacilityReservation(cell);

        if (_facilityReservations.TryGetValue(cell, out MercenaryController? reservedBy) && reservedBy == mercenary)
        {
            _facilityReservations.Remove(cell);
        }
    }

    public void ReleaseReservationsFor(MercenaryController mercenary)
    {
        List<Vector2I> cellsToRelease = new();

        foreach (KeyValuePair<Vector2I, MercenaryController> reservation in _facilityReservations)
        {
            if (reservation.Value == mercenary || !IsValidMercenaryFacilityUse(reservation.Value))
            {
                cellsToRelease.Add(reservation.Key);
            }
        }

        foreach (Vector2I cell in cellsToRelease)
        {
            _facilityReservations.Remove(cell);
        }
    }

    public bool IsFacilityOccupied(Vector2I cell)
    {
        PruneInvalidFacilityOccupancy(cell);
        return _facilityOccupants.ContainsKey(cell);
    }

    public bool IsFacilityOccupiedBy(Vector2I cell, MercenaryController mercenary)
    {
        PruneInvalidFacilityOccupancy(cell);
        return _facilityOccupants.TryGetValue(cell, out MercenaryController? occupiedBy) && occupiedBy == mercenary;
    }

    public bool TryOccupyFacility(Vector2I cell, MercenaryController mercenary)
    {
        if (!IsFacilityCell(cell))
        {
            return false;
        }

        PruneInvalidFacilityReservation(cell);
        PruneInvalidFacilityOccupancy(cell);

        if (_facilityReservations.TryGetValue(cell, out MercenaryController? reservedBy) && reservedBy != mercenary)
        {
            return false;
        }

        if (_facilityOccupants.TryGetValue(cell, out MercenaryController? occupiedBy))
        {
            return occupiedBy == mercenary;
        }

        _facilityOccupants[cell] = mercenary;
        _facilityReservations.Remove(cell);
        return true;
    }

    public void ReleaseFacilityOccupancy(Vector2I cell, MercenaryController mercenary)
    {
        PruneInvalidFacilityOccupancy(cell);

        if (_facilityOccupants.TryGetValue(cell, out MercenaryController? occupiedBy) && occupiedBy == mercenary)
        {
            _facilityOccupants.Remove(cell);
        }
    }

    public void ReleaseOccupanciesFor(MercenaryController mercenary)
    {
        List<Vector2I> cellsToRelease = new();

        foreach (KeyValuePair<Vector2I, MercenaryController> occupancy in _facilityOccupants)
        {
            if (occupancy.Value == mercenary || !IsValidMercenaryFacilityUse(occupancy.Value))
            {
                cellsToRelease.Add(occupancy.Key);
            }
        }

        foreach (Vector2I cell in cellsToRelease)
        {
            _facilityOccupants.Remove(cell);
        }
    }

    public void ReleaseFacilityUseFor(MercenaryController mercenary)
    {
        ReleaseReservationsFor(mercenary);
        ReleaseOccupanciesFor(mercenary);
    }

    public bool TryGetNearestFacility(Vector2 fromWorld, FacilityType type, out FacilityInfo facility)
    {
        return TryGetNearestFacilityInternal(fromWorld, type, null, false, out facility);
    }

    public bool TryGetNearestAvailableFacility(Vector2 fromWorld, FacilityType type, MercenaryController requester, out FacilityInfo facility)
    {
        return TryGetNearestFacilityInternal(fromWorld, type, requester, true, out facility);
    }

    private bool TryGetNearestFacilityInternal(Vector2 fromWorld, FacilityType type, MercenaryController? requester, bool requireAvailable, out FacilityInfo facility)
    {
        facility = default;

        if (type == FacilityType.None)
        {
            return false;
        }

        Vector2I startCell = WorldToCell(fromWorld);
        float bestDistanceSquared = float.MaxValue;
        bool foundFacility = false;

        foreach (FacilityInfo candidate in GetFacilitiesByType(type))
        {
            bool reservedByOther = candidate.IsReserved && (requester == null || !IsFacilityReservedBy(candidate.Cell, requester));
            bool occupiedByOther = candidate.IsOccupied && (requester == null || !IsFacilityOccupiedBy(candidate.Cell, requester));

            if (requireAvailable && (!candidate.IsUsable || reservedByOther || occupiedByOther))
            {
                continue;
            }

            List<Vector2I> path = GridPathfinder.FindPath(startCell, candidate.Cell, this);

            if (startCell != candidate.Cell && path.Count == 0)
            {
                continue;
            }

            float distanceSquared = fromWorld.DistanceSquaredTo(candidate.WorldPosition);

            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            facility = candidate;
            bestDistanceSquared = distanceSquared;
            foundFacility = true;
        }

        return foundFacility;
    }

    public bool IsCellInWorld(Vector2I cell)
    {
        if (_worldGrid == null)
        {
            return false;
        }

        return cell.X >= 0 && cell.Y >= 0 && cell.X < _worldGrid.WorldWidth && cell.Y < _worldGrid.WorldHeight;
    }

    public override void _Draw()
    {
        DrawFarmZones();

        foreach (BuildableTileState state in _buildings.Values)
        {
            DrawBuildTile(state);
        }

        DrawBuildPreview();
        DrawBlockedLineDebug();
    }

    private void DrawFarmZones()
    {
        int tileSize = GetTileSize();
        Color fillColor = new Color(0.18f, 0.72f, 0.26f, 0.22f);
        Color outlineColor = new Color(0.36f, 0.95f, 0.42f, 0.55f);

        foreach (Vector2I cell in _farmZoneCells)
        {
            Rect2 tileRect = new Rect2(new Vector2(cell.X * tileSize, cell.Y * tileSize), new Vector2(tileSize, tileSize));
            DrawRect(tileRect.Grow(-3.0f), fillColor);
            DrawRect(tileRect.Grow(-3.0f), outlineColor, false, 1.0f);
        }
    }

    public string GetBuildStatusText()
    {
        if (CurrentBuildMode == TileBuildType.None)
        {
            return "Build: Off";
        }

        string placementStatus;

        bool canAffordCurrentBuild = CanAffordBuild(CurrentBuildMode);

        if (!HasHoverCell || !HoverCellInWorld)
        {
            placementStatus = $"Build: {CurrentBuildMode} | Blocked";
        }
        else if (CurrentBuildMode == TileBuildType.Erase)
        {
            placementStatus = CanPlaceOnHoverCell ? "Build: Erase | Target" : "Build: Erase | No Target";
        }
        else if (CanPlaceOnHoverCell && !canAffordCurrentBuild)
        {
            placementStatus = $"Build: {CurrentBuildMode} | Blocked";
        }
        else
        {
            placementStatus = CanPlaceOnHoverCell ? $"Build: {CurrentBuildMode} | Can Place" : $"Build: {CurrentBuildMode} | Blocked";
        }

        return $"{placementStatus}\n{GetBuildCostLabel(CurrentBuildMode)}\n{GetBuildAffordLabel(CurrentBuildMode)}";
    }

    public IReadOnlyDictionary<BaseResourceType, int> GetBuildCost(TileBuildType buildType)
    {
        return BuildCosts.TryGetValue(buildType, out BuildCost cost)
            ? cost.Resources
            : CreateBuildCost().Resources;
    }

    public bool CanAffordBuild(TileBuildType buildType)
    {
        foreach (KeyValuePair<BaseResourceType, int> cost in GetBuildCost(buildType))
        {
            if (!HasResource(cost.Key, cost.Value))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryConsumeBuildCost(TileBuildType buildType)
    {
        IReadOnlyDictionary<BaseResourceType, int> cost = GetBuildCost(buildType);

        if (cost.Count == 0)
        {
            return true;
        }

        if (!CanAffordBuild(buildType))
        {
            return false;
        }

        foreach (KeyValuePair<BaseResourceType, int> resourceCost in cost)
        {
            TryConsumeResource(resourceCost.Key, resourceCost.Value);
        }

        return true;
    }

    public string GetBuildCostLabel(TileBuildType buildType)
    {
        IReadOnlyDictionary<BaseResourceType, int> cost = GetBuildCost(buildType);

        if (cost.Count == 0)
        {
            return "Cost: -";
        }

        List<string> parts = new();

        foreach (BaseResourceType resourceType in BuildCostResourceOrder)
        {
            if (cost.TryGetValue(resourceType, out int amount) && amount > 0)
            {
                parts.Add($"{resourceType} {amount}");
            }
        }

        return parts.Count == 0 ? "Cost: -" : $"Cost: {string.Join(", ", parts)}";
    }

    public string GetBuildAffordLabel(TileBuildType buildType)
    {
        List<string> missingResources = new();

        foreach (BaseResourceType resourceType in BuildCostResourceOrder)
        {
            IReadOnlyDictionary<BaseResourceType, int> cost = GetBuildCost(buildType);

            if (cost.TryGetValue(resourceType, out int amount) && amount > 0 && !HasResource(resourceType, amount))
            {
                missingResources.Add(resourceType.ToString());
            }
        }

        return missingResources.Count == 0 ? "Can Afford" : $"Need {string.Join(", ", missingResources)}";
    }

    public bool HasFood()
    {
        return HasResource(BaseResourceType.Food, 1);
    }

    public bool IsFoodEmpty => GetResourceAmount(BaseResourceType.Food) <= 0;
    public bool IsFoodLow => GetResourceAmount(BaseResourceType.Food) > 0 && GetResourceAmount(BaseResourceType.Food) <= FoodLowThreshold;

    public int GetResourceAmount(BaseResourceType type)
    {
        int legacyAmount = _resources.TryGetValue(type, out int amount) ? amount : 0;
        return IsStoredResource(type) ? legacyAmount + GetTotalStoredAmount(type) : legacyAmount;
    }

    public bool HasResource(BaseResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        return GetResourceAmount(type) >= amount;
    }

    public bool TryConsumeResource(BaseResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (!HasResource(type, amount))
        {
            return false;
        }

        if (IsStoredResource(type))
        {
            int remainingAmount = amount;
            int storedAmount = GetTotalStoredAmount(type);
            int removeFromStorage = Mathf.Min(storedAmount, remainingAmount);

            if (removeFromStorage > 0)
            {
                TryRemoveResourceFromStorages(type, removeFromStorage);
                remainingAmount -= removeFromStorage;
            }

            if (remainingAmount > 0)
            {
                _resources[type] = Mathf.Max(0, (_resources.TryGetValue(type, out int legacyAmount) ? legacyAmount : 0) - remainingAmount);
            }

            return true;
        }

        SetResourceAmount(type, GetResourceAmount(type) - amount);
        return true;
    }

    public void AddResource(BaseResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        // TODO: Route production and hauling through storage policies once jobs and stockpile filters exist.
        _resources[type] = (_resources.TryGetValue(type, out int legacyAmount) ? legacyAmount : 0) + amount;
    }

    public void SetResourceAmount(BaseResourceType type, int amount)
    {
        if (IsStoredResource(type))
        {
            ClearStoredResource(type);
        }

        _resources[type] = Mathf.Max(0, amount);
    }

    public bool TryConsumeFood(int amount = 1)
    {
        return TryConsumeResource(BaseResourceType.Food, amount);
    }

    public bool TryRemoveFood(int amount = 1)
    {
        return TryConsumeResource(BaseResourceType.Food, amount);
    }

    public void AddFood(int amount)
    {
        AddResource(BaseResourceType.Food, amount);
    }

    public void SetFoodCount(int value)
    {
        SetResourceAmount(BaseResourceType.Food, value);
    }

    public int GetFoodCount()
    {
        return GetResourceAmount(BaseResourceType.Food);
    }

    public string GetFoodStatusLabel()
    {
        if (IsFoodEmpty)
        {
            return "EMPTY";
        }

        if (IsFoodLow)
        {
            return "LOW";
        }

        return "";
    }

    private void InitializeResources()
    {
        SetResourceAmount(BaseResourceType.Food, InitialFoodCount);
        SetResourceAmount(BaseResourceType.Wood, InitialWood);
        SetResourceAmount(BaseResourceType.Stone, InitialStone);
        SetResourceAmount(BaseResourceType.Metal, InitialMetal);
    }

    private static BuildCost CreateBuildCost(params (BaseResourceType ResourceType, int Amount)[] resources)
    {
        Dictionary<BaseResourceType, int> cost = new();

        foreach ((BaseResourceType resourceType, int amount) in resources)
        {
            if (amount <= 0)
            {
                continue;
            }

            cost[resourceType] = amount;
        }

        // TODO: Tune these temporary 0.1 build costs when construction time, hauling, and gathering exist.
        return new BuildCost(cost);
    }

    private Vector2I FindResourcePileSpawnCell(BaseResourceType type, Vector2I preferredCell)
    {
        if (CanUseResourcePileCell(preferredCell, type))
        {
            return preferredCell;
        }

        Vector2I[] offsets =
        {
            new Vector2I(1, 0),
            new Vector2I(-1, 0),
            new Vector2I(0, 1),
            new Vector2I(0, -1),
            new Vector2I(1, 1),
            new Vector2I(-1, 1),
            new Vector2I(1, -1),
            new Vector2I(-1, -1)
        };

        foreach (Vector2I offset in offsets)
        {
            Vector2I candidate = preferredCell + offset;

            if (CanUseResourcePileCell(candidate, type))
            {
                return candidate;
            }
        }

        return preferredCell;
    }

    private bool CanUseResourcePileCell(Vector2I cell, BaseResourceType type)
    {
        if (!IsCellInWorld(cell))
        {
            return false;
        }

        ResourcePile? existingPile = GetResourcePileAtCell(cell);
        return existingPile == null || existingPile.ResourceType == type;
    }

    private bool CanPlantCropAt(Vector2I cell)
    {
        if (!IsFarmZoneCell(cell)
            || !CanMarkFarmZoneAt(cell)
            || IsResourcePileCell(cell)
            || IsCropPlantCell(cell)
            || IsCellBlocked(cell))
        {
            return false;
        }

        return true;
    }

    private static IEnumerable<Vector2I> GetStorageAccessCells(Vector2I storageCell)
    {
        yield return storageCell + new Vector2I(1, 0);
        yield return storageCell + new Vector2I(-1, 0);
        yield return storageCell + new Vector2I(0, 1);
        yield return storageCell + new Vector2I(0, -1);
    }

    private void RegisterStorage(Vector2I cell)
    {
        if (_storageInventories.ContainsKey(cell))
        {
            return;
        }

        _storageInventories[cell] = new Dictionary<BaseResourceType, int>
        {
            { BaseResourceType.Food, 0 },
            { BaseResourceType.Wood, 0 },
            { BaseResourceType.Stone, 0 },
            { BaseResourceType.Metal, 0 }
        };
    }

    private void UnregisterStorage(Vector2I cell)
    {
        _storageInventories.Remove(cell);
    }

    private bool IsStorageInventoryEmpty(Vector2I cell)
    {
        if (!_storageInventories.TryGetValue(cell, out Dictionary<BaseResourceType, int>? inventory))
        {
            return true;
        }

        foreach (int amount in inventory.Values)
        {
            if (amount > 0)
            {
                return false;
            }
        }

        return true;
    }

    private void ClearStoredResource(BaseResourceType type)
    {
        foreach (Dictionary<BaseResourceType, int> inventory in _storageInventories.Values)
        {
            if (inventory.ContainsKey(type))
            {
                inventory[type] = 0;
            }
        }
    }

    private static bool IsConstructionResource(BaseResourceType type)
    {
        return type == BaseResourceType.Wood
            || type == BaseResourceType.Stone
            || type == BaseResourceType.Metal;
    }

    private static bool IsStoredResource(BaseResourceType type)
    {
        return type == BaseResourceType.Food || IsConstructionResource(type);
    }

    private static bool IsFacilityBuildType(TileBuildType buildType)
    {
        return buildType == TileBuildType.Bed
            || buildType == TileBuildType.Storage
            || buildType == TileBuildType.GuardPost;
    }

    private static FacilityType GetFacilityTypeForObject(TileBuildType objectType)
    {
        return objectType switch
        {
            TileBuildType.Bed => FacilityType.Bed,
            TileBuildType.Storage => FacilityType.Storage,
            TileBuildType.GuardPost => FacilityType.GuardPost,
            _ => FacilityType.None
        };
    }

    private FacilityInfo CreateFacilityInfo(BuildableTileState state, FacilityType facilityType)
    {
        // TODO: Add ownership, damage, and richer usability checks when jobs need them.
        return new FacilityInfo(
            state.Cell,
            CellToWorldCenter(state.Cell),
            facilityType,
            state.ObjectType,
            true,
            IsFacilityReserved(state.Cell),
            IsFacilityOccupied(state.Cell));
    }

    private void ApplyBuildAt(Vector2I cell, TileBuildType buildType)
    {
        bool hasState = _buildings.TryGetValue(cell, out BuildableTileState state);

        if (!hasState)
        {
            state = new BuildableTileState(cell, TileBuildType.None, TileBuildType.None);
        }

        if (buildType == TileBuildType.Floor)
        {
            state = state.WithFloor(TileBuildType.Floor);
        }
        else if (buildType == TileBuildType.Wall)
        {
            ClearFarmZoneAt(cell);
            state = state.WithObject(TileBuildType.Wall);
        }
        else if (buildType == TileBuildType.Door)
        {
            ClearFarmZoneAt(cell);
            state = state.WithObject(TileBuildType.Door, true);
        }
        else if (IsFacilityBuildType(buildType))
        {
            ClearFarmZoneAt(cell);
            state = state.WithObject(buildType);
        }

        _buildings[cell] = state;

        if (buildType == TileBuildType.Storage)
        {
            RegisterStorage(cell);
        }
    }

    private bool EraseAt(Vector2I cell)
    {
        if (!_buildings.TryGetValue(cell, out BuildableTileState state))
        {
            return false;
        }

        if (state.HasObject)
        {
            if (state.IsFacility)
            {
                _facilityReservations.Remove(cell);
                _facilityOccupants.Remove(cell);
            }

            if (state.ObjectType == TileBuildType.Storage)
            {
                if (!IsStorageInventoryEmpty(cell))
                {
                    return false;
                }

                UnregisterStorage(cell);
            }

            state = state.WithObject(TileBuildType.None);
        }
        else if (state.HasFloor)
        {
            state = state.WithFloor(TileBuildType.None);
            ClearFarmZoneAt(cell);
        }

        if (state.IsEmpty())
        {
            _buildings.Remove(cell);
        }
        else
        {
            _buildings[cell] = state;
        }

        return true;
    }

    private bool IsCellOccupied(Vector2I cell)
    {
        return IsCellOccupiedByGroup(cell, "mercenaries") || IsCellOccupiedByGroup(cell, "enemies");
    }

    private bool IsCellOccupiedByGroup(Vector2I cell, string groupName)
    {
        SceneTree? tree = GetTree();

        if (tree == null)
        {
            return false;
        }

        foreach (Node node in tree.GetNodesInGroup(groupName))
        {
            if (node is Node2D node2D && WorldToCell(node2D.GlobalPosition) == cell)
            {
                return true;
            }
        }

        return false;
    }

    public CropPlant? GetCropPlantAtCell(Vector2I cell)
    {
        foreach (CropPlant cropPlant in GetAllCropPlants())
        {
            if (cropPlant.Cell == cell)
            {
                return cropPlant;
            }
        }

        return null;
    }

    public bool IsCropPlantCell(Vector2I cell)
    {
        return GetCropPlantAtCell(cell) != null;
    }

    public IReadOnlyList<CropPlant> GetAllCropPlants()
    {
        List<CropPlant> cropPlants = new();
        SceneTree? tree = GetTree();

        if (tree == null)
        {
            return cropPlants;
        }

        foreach (Node node in tree.GetNodesInGroup("crop_plants"))
        {
            if (node is CropPlant cropPlant
                && GodotObject.IsInstanceValid(cropPlant)
                && !cropPlant.IsQueuedForDeletion()
                && !cropPlant.IsRemoving)
            {
                cropPlants.Add(cropPlant);
            }
        }

        return cropPlants;
    }

    public bool HasCropPlantAtCell(Vector2I cell)
    {
        return IsCropPlantCell(cell);
    }

    public bool TrySpawnCropPlantAt(Vector2I cell)
    {
        if (!CanPlantCropAt(cell))
        {
            return false;
        }

        if (_cropPlantLayer == null)
        {
            GD.PushWarning("CropPlantLayer not found. Cannot spawn crop plant.");
            return false;
        }

        CropPlant cropPlant = new();
        cropPlant.Initialize(cell);
        cropPlant.Position = CellToWorldCenter(cell);
        _cropPlantLayer.AddChild(cropPlant);
        return true;
    }

    public bool TryFindNearestPlantableFarmCell(Vector2I fromCell, out Vector2I targetCell)
    {
        targetCell = default;
        int bestPathLength = int.MaxValue;
        bool foundCell = false;

        foreach (Vector2I farmCell in _farmZoneCells)
        {
            if (!CanPlantCropAt(farmCell))
            {
                continue;
            }

            List<Vector2I> path = GridPathfinder.FindPath(fromCell, farmCell, this);

            if (fromCell != farmCell && path.Count == 0)
            {
                continue;
            }

            int pathLength = fromCell == farmCell ? 0 : path.Count;

            if (pathLength >= bestPathLength)
            {
                continue;
            }

            bestPathLength = pathLength;
            targetCell = farmCell;
            foundCell = true;
        }

        return foundCell;
    }

    public CropPlant? FindNearestHarvestableCrop(Vector2I fromCell, MercenaryLifeAI lifeAI)
    {
        CropPlant? nearestCrop = null;
        int bestPathLength = int.MaxValue;

        foreach (CropPlant cropPlant in GetAllCropPlants())
        {
            if (!cropPlant.IsMature
                || (cropPlant.IsReservedForHarvest && !cropPlant.IsReservedBy(lifeAI)))
            {
                continue;
            }

            List<Vector2I> path = GridPathfinder.FindPath(fromCell, cropPlant.Cell, this);

            if (fromCell != cropPlant.Cell && path.Count == 0)
            {
                continue;
            }

            int pathLength = fromCell == cropPlant.Cell ? 0 : path.Count;

            if (pathLength >= bestPathLength)
            {
                continue;
            }

            bestPathLength = pathLength;
            nearestCrop = cropPlant;
        }

        return nearestCrop;
    }

    private void PruneInvalidFacilityReservation(Vector2I cell)
    {
        if (!_facilityReservations.TryGetValue(cell, out MercenaryController? mercenary))
        {
            return;
        }

        if (!IsFacilityCell(cell) || !IsValidMercenaryFacilityUse(mercenary))
        {
            _facilityReservations.Remove(cell);
        }
    }

    private void PruneInvalidFacilityOccupancy(Vector2I cell)
    {
        if (!_facilityOccupants.TryGetValue(cell, out MercenaryController? mercenary))
        {
            return;
        }

        if (!IsFacilityCell(cell) || !IsValidMercenaryFacilityUse(mercenary))
        {
            _facilityOccupants.Remove(cell);
        }
    }

    private static bool IsValidMercenaryFacilityUse(MercenaryController mercenary)
    {
        return GodotObject.IsInstanceValid(mercenary);
    }

    private void DrawBuildTile(BuildableTileState state)
    {
        int tileSize = GetTileSize();
        Vector2 position = new Vector2(state.Cell.X * tileSize, state.Cell.Y * tileSize);
        Rect2 tileRect = new Rect2(position, new Vector2(tileSize, tileSize));
        Rect2 innerRect = tileRect.Grow(-4.0f);

        if (state.HasFloor)
        {
            DrawRect(tileRect.Grow(-2.0f), new Color(0.72f, 0.68f, 0.56f, 0.55f));
            DrawRect(tileRect.Grow(-2.0f), new Color(0.92f, 0.86f, 0.68f, 0.7f), false, 1.0f);
        }

        if (state.ObjectType == TileBuildType.Wall)
        {
            DrawRect(tileRect.Grow(-1.0f), new Color(0.24f, 0.24f, 0.26f, 0.92f));
            DrawRect(tileRect.Grow(-6.0f), new Color(0.38f, 0.38f, 0.42f, 0.95f));
        }
        else if (state.ObjectType == TileBuildType.Door)
        {
            DrawRect(tileRect.Grow(-1.0f), new Color(0.18f, 0.18f, 0.2f, 0.72f));
            DrawRect(new Rect2(position + new Vector2(5.0f, tileSize * 0.36f), new Vector2(tileSize - 10.0f, tileSize * 0.28f)), new Color(0.58f, 0.36f, 0.16f, 0.95f));
            DrawLine(position + new Vector2(8.0f, tileSize * 0.5f), position + new Vector2(tileSize - 8.0f, tileSize * 0.5f), new Color(0.98f, 0.78f, 0.36f), 2.0f);
            DrawCircle(tileRect.GetCenter() + new Vector2(tileSize * 0.2f, -2.0f), 2.0f, new Color(1.0f, 0.86f, 0.4f));
        }
        else if (state.ObjectType == TileBuildType.Bed)
        {
            DrawRect(innerRect, new Color(0.24f, 0.42f, 0.68f, 0.9f));
            DrawRect(new Rect2(innerRect.Position, new Vector2(innerRect.Size.X, 8.0f)), new Color(0.84f, 0.88f, 0.92f, 0.95f));
        }
        else if (state.ObjectType == TileBuildType.Storage)
        {
            DrawRect(innerRect, new Color(0.58f, 0.36f, 0.16f, 0.9f));
            DrawLine(innerRect.Position, innerRect.End, new Color(0.24f, 0.14f, 0.06f), 2.0f);
            DrawLine(new Vector2(innerRect.End.X, innerRect.Position.Y), new Vector2(innerRect.Position.X, innerRect.End.Y), new Color(0.24f, 0.14f, 0.06f), 2.0f);
        }
        else if (state.ObjectType == TileBuildType.GuardPost)
        {
            Vector2 center = tileRect.GetCenter();
            DrawCircle(center, tileSize * 0.28f, new Color(0.22f, 0.22f, 0.2f, 0.8f));
            DrawLine(center + new Vector2(-4.0f, 8.0f), center + new Vector2(-4.0f, -10.0f), new Color(0.96f, 0.92f, 0.72f), 2.0f);
            DrawColoredPolygon(new[]
            {
                center + new Vector2(-4.0f, -10.0f),
                center + new Vector2(10.0f, -6.0f),
                center + new Vector2(-4.0f, -2.0f)
            }, new Color(0.86f, 0.18f, 0.18f, 0.95f));
        }
    }

    private void DrawBuildPreview()
    {
        if (CurrentBuildMode == TileBuildType.None || !_hoverCell.HasValue || !_hoverCellInWorld)
        {
            return;
        }

        int tileSize = GetTileSize();
        Vector2 position = new Vector2(_hoverCell.Value.X * tileSize, _hoverCell.Value.Y * tileSize);
        Rect2 tileRect = new Rect2(position, new Vector2(tileSize, tileSize));
        Color validColor = new Color(0.34f, 0.9f, 0.42f, 0.32f);
        Color invalidColor = new Color(1.0f, 0.18f, 0.12f, 0.32f);
        bool canPreviewBuild = _canPlaceOnHoverCell && CanAffordBuild(CurrentBuildMode);
        Color outlineColor = canPreviewBuild
            ? new Color(0.52f, 1.0f, 0.56f, 0.9f)
            : new Color(1.0f, 0.2f, 0.16f, 0.95f);

        if (CurrentBuildMode == TileBuildType.Erase)
        {
            DrawRect(tileRect.Grow(-1.0f), _canPlaceOnHoverCell ? new Color(1.0f, 0.78f, 0.18f, 0.32f) : invalidColor);
            DrawRect(tileRect.Grow(-1.0f), outlineColor, false, 2.0f);
            DrawLine(tileRect.Position + new Vector2(6.0f, 6.0f), tileRect.End - new Vector2(6.0f, 6.0f), outlineColor, 2.0f);
            DrawLine(new Vector2(tileRect.End.X - 6.0f, tileRect.Position.Y + 6.0f), new Vector2(tileRect.Position.X + 6.0f, tileRect.End.Y - 6.0f), outlineColor, 2.0f);
            return;
        }

        DrawRect(tileRect.Grow(-2.0f), canPreviewBuild ? validColor : invalidColor);
        DrawPreviewShape(tileRect, CurrentBuildMode, canPreviewBuild);
        DrawRect(tileRect.Grow(-1.0f), outlineColor, false, 2.0f);

        if (!canPreviewBuild)
        {
            DrawLine(tileRect.Position + new Vector2(5.0f, 5.0f), tileRect.End - new Vector2(5.0f, 5.0f), outlineColor, 2.5f);
            DrawLine(new Vector2(tileRect.End.X - 5.0f, tileRect.Position.Y + 5.0f), new Vector2(tileRect.Position.X + 5.0f, tileRect.End.Y - 5.0f), outlineColor, 2.5f);
        }
    }

    private void DrawBlockedLineDebug()
    {
        if (!ShowBlockedLineDebug || !_hasBlockedLineDebug)
        {
            return;
        }

        int tileSize = GetTileSize();
        Rect2 blockedRect = new Rect2(new Vector2(_blockedLineDebugCell.X * tileSize, _blockedLineDebugCell.Y * tileSize), new Vector2(tileSize, tileSize));
        Color lineColor = new Color(1.0f, 0.12f, 0.08f, 0.8f);
        DrawLine(_blockedLineDebugFrom, _blockedLineDebugTo, lineColor, 2.0f);
        DrawRect(blockedRect.Grow(-1.0f), new Color(1.0f, 0.08f, 0.06f, 0.24f));
        DrawRect(blockedRect.Grow(-1.0f), lineColor, false, 2.0f);
    }

    private void DrawPreviewShape(Rect2 tileRect, TileBuildType buildType, bool isValid)
    {
        Color shapeColor = isValid ? new Color(0.82f, 1.0f, 0.82f, 0.58f) : new Color(1.0f, 0.58f, 0.52f, 0.58f);
        Rect2 innerRect = tileRect.Grow(-7.0f);

        if (buildType == TileBuildType.Floor)
        {
            DrawRect(tileRect.Grow(-6.0f), shapeColor, false, 2.0f);
        }
        else if (buildType == TileBuildType.Wall)
        {
            DrawRect(innerRect, shapeColor);
        }
        else if (buildType == TileBuildType.Door)
        {
            DrawRect(innerRect, shapeColor, false, 3.0f);
            DrawLine(tileRect.GetCenter() + Vector2.Up * 9.0f, tileRect.GetCenter() + Vector2.Down * 9.0f, shapeColor, 2.0f);
        }
        else if (buildType == TileBuildType.Bed)
        {
            DrawRect(innerRect, shapeColor);
            DrawRect(new Rect2(innerRect.Position, new Vector2(innerRect.Size.X, 6.0f)), new Color(1.0f, 1.0f, 1.0f, 0.62f));
        }
        else if (buildType == TileBuildType.Storage)
        {
            DrawRect(innerRect, shapeColor, false, 2.0f);
            DrawLine(innerRect.Position, innerRect.End, shapeColor, 2.0f);
            DrawLine(new Vector2(innerRect.End.X, innerRect.Position.Y), new Vector2(innerRect.Position.X, innerRect.End.Y), shapeColor, 2.0f);
        }
        else if (buildType == TileBuildType.GuardPost)
        {
            Vector2 center = tileRect.GetCenter();
            DrawCircle(center, tileRect.Size.X * 0.2f, shapeColor);
            DrawLine(center + new Vector2(-3.0f, 8.0f), center + new Vector2(-3.0f, -10.0f), shapeColor, 2.0f);
            DrawLine(center + new Vector2(-3.0f, -10.0f), center + new Vector2(9.0f, -5.0f), shapeColor, 2.0f);
        }
    }

    private void UpdateHoverPreview(Vector2 worldPosition)
    {
        Vector2I nextHoverCell = WorldToCell(worldPosition);
        Vector2I? nextStoredHoverCell = CurrentBuildMode == TileBuildType.None ? null : nextHoverCell;
        bool nextHoverCellInWorld = CurrentBuildMode != TileBuildType.None && IsCellInWorld(nextHoverCell);
        bool nextCanPlaceOnHoverCell = nextHoverCellInWorld && CanPlaceAt(nextHoverCell, CurrentBuildMode);
        bool changed = _hoverCell != nextStoredHoverCell
            || _hoverCellInWorld != nextHoverCellInWorld
            || _canPlaceOnHoverCell != nextCanPlaceOnHoverCell;

        _hoverCell = nextStoredHoverCell;
        _hoverCellInWorld = nextHoverCellInWorld;
        _canPlaceOnHoverCell = nextCanPlaceOnHoverCell;

        if (changed)
        {
            QueueRedraw();
        }
    }

    private int GetTileSize()
    {
        return _worldGrid?.TileSize ?? 32;
    }

    private void StoreBlockedLineDebug(Vector2 fromWorld, Vector2 toWorld, Vector2I blockedCell)
    {
        if (!ShowBlockedLineDebug)
        {
            return;
        }

        _hasBlockedLineDebug = true;
        _blockedLineDebugFrom = fromWorld;
        _blockedLineDebugTo = toWorld;
        _blockedLineDebugCell = blockedCell;
        QueueRedraw();
    }

    private void ClearBlockedLineDebug()
    {
        if (!_hasBlockedLineDebug)
        {
            return;
        }

        _hasBlockedLineDebug = false;
        QueueRedraw();
    }
}
