using System.Collections.Generic;
using Godot;

public partial class BaseBuildManager : Node2D
{
	[Export]
	public bool ShowBlockedLineDebug { get; set; } = false;

	[Export]
	public int InitialFoodCount { get; set; } = 0;

	[Export]
	public int InitialWood { get; set; } = 0;

	[Export]
	public int InitialStone { get; set; } = 0;

	[Export]
	public int InitialMetal { get; set; } = 0;

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

	[Export]
	public bool EnableLogisticsValidation { get; set; } = true;

	[Export]
	public bool EnableVerboseLogisticsLogs { get; set; } = false;

	public int FoodCount => GetResourceAmount(BaseResourceType.Food);
	public int TileSize => GetTileSize();
	public TileBuildType CurrentBuildMode { get; private set; } = TileBuildType.None;
	public BuildMaterialType CurrentBuildMaterialType { get; private set; } = BuildMaterialType.Wood;
	public bool HasHoverCell => _hoverCell.HasValue;
	public bool HoverCellInWorld => _hoverCellInWorld;
	public bool CanPlaceOnHoverCell => _canPlaceOnHoverCell;

	private readonly Dictionary<Vector2I, BuildableTileState> _buildings = new();
	private readonly Dictionary<Vector2I, MercenaryController> _facilityReservations = new();
	private readonly Dictionary<Vector2I, MercenaryController> _facilityOccupants = new();
	private readonly Dictionary<Vector2I, MercenaryController> _furnitureReservations = new();
	private readonly Dictionary<Vector2I, MercenaryController> _storageInteractionReservations = new();
	private readonly Dictionary<BaseResourceType, int> _resources = new();
	private readonly Dictionary<Vector2I, Dictionary<BaseResourceType, int>> _storageInventories = new();
	private readonly Dictionary<Vector2I, StoragePolicy> _storagePolicies = new();
	private readonly List<ConstructionSite> _constructionSites = new();
	private readonly List<StockpileZone> _stockpileZones = new();
	private readonly HashSet<Vector2I> _farmZoneCells = new();
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
	private float _logisticsValidationTimer;
	private string _lastLogisticsValidationWarning = "";
	private bool _isRunningLogisticsValidation;
	private int _nextConstructionSiteId = 1;
	private int _nextStockpileZoneId = 1;
	private Vector2I? _starterSupplyChestCell;

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
		CleanInvalidFurnitureReservations();
		UpdateLogisticsValidation((float)delta);
		UpdateHoverPreview(GetGlobalMousePosition());
	}

	private void UpdateLogisticsValidation(float delta)
	{
		if (!EnableLogisticsValidation)
		{
			_lastLogisticsValidationWarning = "";
			return;
		}

		_logisticsValidationTimer -= delta;

		if (_logisticsValidationTimer > 0.0f)
		{
			return;
		}

		_logisticsValidationTimer = 3.0f;
		ValidateLogisticsWorld("periodic");
	}

	public void ValidateLogisticsWorld(string context = "manual")
	{
		if (!EnableLogisticsValidation || _isRunningLogisticsValidation)
		{
			return;
		}

		_isRunningLogisticsValidation = true;

		try
		{
			List<string> warnings = new();
			ValidateStorageInventories(warnings);
			ValidateStorageInteractions(warnings);
			ValidateConstructionSites(warnings);
			ValidateStockpileZones(warnings);
			ValidateStarterSupplyChest(warnings);
			ValidateResourcePiles(warnings);
			ValidateMercenaryInventories(warnings);

			int reservationCountBefore = _furnitureReservations.Count;
			CleanInvalidFurnitureReservations();

			if (reservationCountBefore != _furnitureReservations.Count)
			{
				warnings.Add("Stale furniture reservation cleaned");
			}

			ReportLogisticsWarnings(context, warnings);
		}
		finally
		{
			_isRunningLogisticsValidation = false;
		}
	}

	private void ValidateStorageInventories(List<string> warnings)
	{
		foreach (Vector2I storageCell in new List<Vector2I>(_storageInventories.Keys))
		{
			if (!_storageInventories.TryGetValue(storageCell, out Dictionary<BaseResourceType, int>? inventory))
			{
				continue;
			}

			if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell))
			{
				if (IsInventoryEmpty(inventory))
				{
					_storageInventories.Remove(storageCell);
					warnings.Add($"Empty orphan storage inventory removed at {storageCell}");
				}
				else
				{
					warnings.Add($"Orphan storage inventory at {storageCell}");
				}

				continue;
			}

			if (originCell != storageCell)
			{
				RegisterStorage(originCell);

				foreach (KeyValuePair<BaseResourceType, int> entry in inventory)
				{
					if (entry.Value > 0)
					{
						_storageInventories[originCell][entry.Key] = GetStoredAmountAt(originCell, entry.Key) + entry.Value;
					}
				}

				_storageInventories.Remove(storageCell);
				warnings.Add($"Child storage inventory merged into origin {originCell}");
				continue;
			}

			foreach (BaseResourceType resourceType in ResourceDefinitionDatabase.AllResourceTypes)
			{
				inventory.TryAdd(resourceType, 0);
			}

			foreach (BaseResourceType resourceType in new List<BaseResourceType>(inventory.Keys))
			{
				if (inventory[resourceType] < 0)
				{
					inventory[resourceType] = 0;
					warnings.Add($"Negative storage amount clamped at {storageCell}");
				}

				if (inventory[resourceType] > 0 && !CanStoragePolicyAllow(storageCell, resourceType))
				{
					warnings.Add($"Storage policy mismatch at {storageCell}: {resourceType}");
				}
			}

			if (GetStorageUsedWeight(storageCell) > GetStorageWeightCapacity(storageCell))
			{
				warnings.Add($"Storage overweight at {storageCell}");
			}
		}
	}

	private void ValidateStorageInteractions(List<string> warnings)
	{
		foreach (KeyValuePair<Vector2I, MercenaryController> reservation in new Dictionary<Vector2I, MercenaryController>(_storageInteractionReservations))
		{
			Vector2I originCell = reservation.Key;
			MercenaryController user = reservation.Value;

			if (!TryGetStorageOriginCell(originCell, out Vector2I resolvedOrigin) || resolvedOrigin != originCell)
			{
				_storageInteractionReservations.Remove(originCell);
				warnings.Add($"Stale storage interaction removed at {originCell}");
				continue;
			}

			if (!GodotObject.IsInstanceValid(user) || user.IsQueuedForDeletion())
			{
				_storageInteractionReservations.Remove(originCell);
				warnings.Add($"Invalid storage interaction user removed at {originCell}");
			}
		}
	}

	private void ValidateConstructionSites(List<string> warnings)
	{
		HashSet<Vector2I> occupiedSiteCells = new();
		List<ConstructionSite> completedSites = new();

		foreach (ConstructionSite site in _constructionSites)
		{
			site.PruneReservations();

			if (site.IsCompleted || site.IsCancelled)
			{
				completedSites.Add(site);
				continue;
			}

			foreach (Vector2I cell in site.OccupiedCells)
			{
				if (!IsCellInWorld(cell))
				{
					warnings.Add($"Construction site outside world at {cell}");
				}

				if (!occupiedSiteCells.Add(cell))
				{
					warnings.Add($"Construction site overlap at {cell}");
				}

				if (_buildings.TryGetValue(cell, out BuildableTileState state) && state.HasObject)
				{
					bool allowedDoorReplacement = site.TargetBuildType == TileBuildType.Door && state.ObjectType == TileBuildType.Wall;

					if (!allowedDoorReplacement)
					{
						warnings.Add($"Construction site overlaps object at {cell}");
					}
				}
			}

			foreach (ConstructionRequirement requirement in site.Requirements.Values)
			{
				if (requirement.DeliveredAmount < 0)
				{
					requirement.DeliveredAmount = 0;
					warnings.Add($"Negative construction delivery clamped at site {site.SiteId}");
				}

				if (requirement.DeliveredAmount > requirement.RequiredAmount)
				{
					requirement.DeliveredAmount = requirement.RequiredAmount;
					warnings.Add($"Over-delivered construction material clamped at site {site.SiteId}");
				}
			}
		}

		foreach (ConstructionSite site in completedSites)
		{
			_constructionSites.Remove(site);
			warnings.Add($"Completed/cancelled construction site removed from active list: {site.SiteId}");
		}
	}

	private void ValidateStockpileZones(List<string> warnings)
	{
		HashSet<Vector2I> occupiedZoneCells = new();

		foreach (StockpileZone zone in _stockpileZones)
		{
			if (zone.Cells.Count == 0)
			{
				warnings.Add($"Empty stockpile zone {zone.ZoneId}");
				continue;
			}

			foreach (Vector2I cell in zone.Cells)
			{
				if (!IsCellInWorld(cell))
				{
					warnings.Add($"Stockpile zone outside world at {cell}");
				}

				if (!occupiedZoneCells.Add(cell))
				{
					warnings.Add($"Stockpile zone overlap at {cell}");
				}
			}

			Vector2I originCell = GetStockpileOriginCell(zone);
			RegisterStorage(originCell);
			zone.Bounds = CalculateBounds(zone.Cells);

			if (GetStorageUsedWeight(originCell) > zone.WeightCapacity)
			{
				warnings.Add($"Stockpile zone overweight: {zone.DisplayName}");
			}
		}
	}

	private void ValidateStarterSupplyChest(List<string> warnings)
	{
		if (!_starterSupplyChestCell.HasValue)
		{
			warnings.Add("Starter supply chest is not registered");
			return;
		}

		if (!TryGetObjectOriginState(_starterSupplyChestCell.Value, out BuildableTileState state)
			|| state.ObjectType != TileBuildType.SmallChest
			|| !TryGetStorageOriginCell(_starterSupplyChestCell.Value, out Vector2I originCell)
			|| originCell != _starterSupplyChestCell.Value)
		{
			warnings.Add("Starter supply chest is missing");
		}
	}

	private void ValidateResourcePiles(List<string> warnings)
	{
		SceneTree? tree = GetTree();

		if (tree == null)
		{
			return;
		}

		foreach (Node node in tree.GetNodesInGroup("resource_piles"))
		{
			if (node is not ResourcePile pile
				|| !GodotObject.IsInstanceValid(pile)
				|| pile.IsQueuedForDeletion())
			{
				continue;
			}

			if (!pile.ValidateLogisticsState(out string pileWarning))
			{
				warnings.Add(pileWarning);
			}
		}
	}

	private void ValidateMercenaryInventories(List<string> warnings)
	{
		Node? root = GetTree()?.CurrentScene;

		if (root == null)
		{
			return;
		}

		List<MercenaryController> mercenaries = new();
		CollectMercenaries(root, mercenaries);

		foreach (MercenaryController mercenary in mercenaries)
		{
			mercenary.ValidateLogisticsState(EnableVerboseLogisticsLogs);
			string warning = mercenary.CurrentLogisticsValidationWarning;

			if (!string.IsNullOrWhiteSpace(warning))
			{
				warnings.Add($"{mercenary.MercenaryName}: {warning}");
			}
		}
	}

	private static void CollectMercenaries(Node node, List<MercenaryController> mercenaries)
	{
		if (node is MercenaryController mercenary
			&& GodotObject.IsInstanceValid(mercenary)
			&& !mercenary.IsQueuedForDeletion())
		{
			mercenaries.Add(mercenary);
		}

		foreach (Node child in node.GetChildren())
		{
			CollectMercenaries(child, mercenaries);
		}
	}

	private static bool IsInventoryEmpty(Dictionary<BaseResourceType, int> inventory)
	{
		foreach (int amount in inventory.Values)
		{
			if (amount > 0)
			{
				return false;
			}
		}

		return true;
	}

	private void ReportLogisticsWarnings(string context, List<string> warnings)
	{
		if (warnings.Count == 0)
		{
			_lastLogisticsValidationWarning = "";
			return;
		}

		string warning = string.Join(" / ", warnings);

		if (!EnableVerboseLogisticsLogs && warning == _lastLogisticsValidationWarning)
		{
			return;
		}

		_lastLogisticsValidationWarning = warning;
		GD.PushWarning($"Logistics validation [{context}]: {warning}");
	}

	public void ToggleBuildMode()
	{
		SetBuildMode(CurrentBuildMode == TileBuildType.None ? TileBuildType.Floor : TileBuildType.None);
	}

	public void SetBuildMode(TileBuildType buildType)
	{
		CurrentBuildMode = buildType;
		CurrentBuildMaterialType = BuildStructureDefinitions.NormalizeMaterial(buildType, CurrentBuildMaterialType);
		UpdateHoverPreview(GetGlobalMousePosition());
	}

	public void SetBuildMaterialType(BuildMaterialType materialType)
	{
		CurrentBuildMaterialType = BuildStructureDefinitions.NormalizeMaterial(CurrentBuildMode, materialType);
		UpdateHoverPreview(GetGlobalMousePosition());
		QueueRedraw();
	}

	public bool TryApplyBuildAtWorldPosition(Vector2 worldPosition)
	{
		return TryApplyBuildAtCell(CurrentBuildMode, WorldToCell(worldPosition));
	}

	public bool TryApplyBuildAtCell(TileBuildType buildType, Vector2I cell)
	{
		if (UsesDirectConstruction(buildType))
		{
			if (!CanPlaceAt(cell, buildType))
			{
				return false;
			}

			bool created = TryCreateConstructionSite(buildType, cell, CurrentBuildMaterialType);
			QueueRedraw();
			return created;
		}

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
			bool removed = TryCancelConstructionSiteAt(cell) || TryRemoveDepletedResourceNodeAt(cell) || EraseAt(cell);
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

			if (TryGetConstructionSiteAtCell(cell, out _))
			{
				return "";
			}

			if (hasExistingState && IsStorageBuildType(existingState.ObjectType) && !IsStorageInventoryEmpty(cell))
			{
				return "Storage not empty";
			}

			return hasExistingState && (existingState.HasObject || existingState.HasFloor)
				? ""
				: "Nothing to erase";
		}

		if (buildType == TileBuildType.Floor)
		{
			if (IsResourceNodeCell(cell))
			{
				return "Resource node exists";
			}

			if (IsCropPlantCell(cell))
			{
				return "Crop exists";
			}

			return !hasExistingState || !existingState.HasObject
				? ""
				: "Object already exists";
		}

		if (buildType == TileBuildType.Wall)
		{
			if (IsResourceNodeCell(cell))
			{
				return "Resource node exists";
			}

			if (IsCropPlantCell(cell))
			{
				return "Crop exists";
			}

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
			if (IsResourceNodeCell(cell))
			{
				return "Resource node exists";
			}

			if (IsCropPlantCell(cell))
			{
				return "Crop exists";
			}

			return hasExistingState && existingState.ObjectType == TileBuildType.Wall
				? ""
				: "Door requires Wall";
		}

		if (IsPlaceableObjectBuildType(buildType))
		{
			return GetMultiCellObjectBlockReason(cell, buildType);
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

			if (TryGetConstructionSiteAtCell(cell, out _))
			{
				return true;
			}

			if (_buildings.TryGetValue(cell, out BuildableTileState eraseStateForStorage)
				&& IsStorageBuildType(eraseStateForStorage.ObjectType)
				&& !IsStorageInventoryEmpty(cell))
			{
				return false;
			}

			return _buildings.TryGetValue(cell, out BuildableTileState eraseState)
				&& (eraseState.HasObject || eraseState.HasFloor);
		}

		bool hasExistingState = _buildings.TryGetValue(cell, out BuildableTileState existingState);

		if (buildType == TileBuildType.Floor)
		{
			return !IsResourceNodeCell(cell)
				&& !IsCropPlantCell(cell)
				&& (!hasExistingState || !existingState.HasObject);
		}

		if (buildType == TileBuildType.Wall)
		{
			return !IsResourceNodeCell(cell)
				&& !IsCropPlantCell(cell)
				&& !IsCellOccupied(cell)
				&& (!hasExistingState
					|| !existingState.IsFacility);
		}

		if (buildType == TileBuildType.Door)
		{
			return !IsResourceNodeCell(cell)
				&& !IsCropPlantCell(cell)
				&& hasExistingState
				&& existingState.ObjectType == TileBuildType.Wall;
		}

		if (IsPlaceableObjectBuildType(buildType))
		{
			return string.IsNullOrEmpty(GetMultiCellObjectBlockReason(cell, buildType));
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
		return TryGetObjectOriginState(cell, out BuildableTileState state) && state.IsFacility;
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
		if (amount <= 0 || !IsStoredResource(type))
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

			ValidateLogisticsWorld("resource pile merge");
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

		ValidateLogisticsWorld("resource pile spawn");
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
		return TryGetObjectOriginState(cell, out BuildableTileState state)
			&& IsStorageBuildType(state.ObjectType);
	}

	public bool IsStorageOrigin(Vector2I cell)
	{
		return TryGetStorageOriginCell(cell, out Vector2I originCell) && originCell == cell;
	}

	public Vector2I ResolveStorageOrigin(Vector2I cell)
	{
		return TryGetStorageOriginCell(cell, out Vector2I originCell) ? originCell : cell;
	}

	public int GetStoredAmountAt(Vector2I storageCell, BaseResourceType type)
	{
		Vector2I originCell = TryGetStorageOriginCell(storageCell, out Vector2I resolvedCell) ? resolvedCell : storageCell;
		return _storageInventories.TryGetValue(originCell, out Dictionary<BaseResourceType, int>? inventory)
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

	public IReadOnlyDictionary<BaseResourceType, int> GetAllStoredResourceTotals()
	{
		Dictionary<BaseResourceType, int> totals = new();

		foreach (BaseResourceType resourceType in ResourceDefinitionDatabase.AllResourceTypes)
		{
			int amount = GetTotalStoredAmount(resourceType);

			if (amount > 0)
			{
				totals[resourceType] = amount;
			}
		}

		return totals;
	}

	public int GetStorageFreeSpace(Vector2I storageCell, BaseResourceType type)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell)
			|| !IsStoredResource(type)
			|| !CanStorageAcceptResource(originCell, type, false))
		{
			return 0;
		}

		int unitWeight = GetResourceUnitWeight(type);
		return unitWeight <= 0 ? 0 : GetStorageFreeWeight(originCell) / unitWeight;
	}

	public int GetStorageUsedWeight(Vector2I storageCell)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell)
			|| !_storageInventories.TryGetValue(originCell, out Dictionary<BaseResourceType, int>? inventory))
		{
			return 0;
		}

		int usedWeight = 0;

		foreach (KeyValuePair<BaseResourceType, int> entry in inventory)
		{
			if (entry.Value > 0)
			{
				usedWeight += entry.Value * GetResourceUnitWeight(entry.Key);
			}
		}

		return usedWeight;
	}

	public int GetStorageWeightCapacity(Vector2I storageCell)
	{
		if (TryGetStorageOriginCell(storageCell, out Vector2I originCell))
		{
			storageCell = originCell;
		}

		if (IsStarterSupplyChest(storageCell))
		{
			return 150;
		}

		if (TryGetStockpileZoneByOrigin(storageCell, out StockpileZone? zone) && zone != null)
		{
			return zone.WeightCapacity;
		}

		if (!TryGetObjectOriginState(storageCell, out BuildableTileState state))
		{
			return StorageCapacityPerResource * 8;
		}

		int baseCapacity = state.ObjectType switch
		{
			TileBuildType.SmallChest => 100,
			TileBuildType.IngredientCrate => 100,
			TileBuildType.MaterialShelf => 200,
			TileBuildType.MedicineShelf => 100,
			TileBuildType.LargeStorage => 1800,
			TileBuildType.Storage => 800,
			_ => StorageCapacityPerResource * 8
		};

		return Mathf.RoundToInt(baseCapacity * GetStorageCapability(storageCell).CapacityMultiplier);
	}

	public int GetStorageCapacity(Vector2I storageCell)
	{
		return GetStorageWeightCapacity(storageCell);
	}

	public int GetStorageFreeWeight(Vector2I storageCell)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell))
		{
			return 0;
		}

		return Mathf.Max(0, GetStorageWeightCapacity(originCell) - GetStorageUsedWeight(originCell));
	}

	public bool TryGetStorageContents(Vector2I storageCell, out Dictionary<BaseResourceType, int> contents)
	{
		contents = new Dictionary<BaseResourceType, int>();

		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell))
		{
			return false;
		}

		RegisterStorage(originCell);

		foreach (BaseResourceType resourceType in ResourceDefinitionDatabase.AllResourceTypes)
		{
			int amount = GetStoredAmountAt(originCell, resourceType);

			if (amount > 0)
			{
				contents[resourceType] = amount;
			}
		}

		return true;
	}

	public IReadOnlyDictionary<BaseResourceType, int> GetStorageContents(Vector2I storageCell)
	{
		return TryGetStorageContents(storageCell, out Dictionary<BaseResourceType, int> contents)
			? contents
			: new Dictionary<BaseResourceType, int>();
	}

	public IReadOnlyList<Vector2I> GetStorageOriginCells()
	{
		List<Vector2I> storageCells = new(_storageInventories.Keys);
		storageCells.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
		return storageCells;
	}

	public bool TryResolveStorageOriginCell(Vector2I cell, out Vector2I originCell)
	{
		return TryGetStorageOriginCell(cell, out originCell);
	}

	public string GetStorageDisplayName(Vector2I storageCell)
	{
		if (TryGetStorageOriginCell(storageCell, out Vector2I originCell))
		{
			storageCell = originCell;
		}

		if (IsStarterSupplyChest(storageCell))
		{
			return "\uCD08\uAE30 \uBCF4\uAE09 \uC0C1\uC790";
		}

		if (TryGetStockpileZoneByOrigin(storageCell, out StockpileZone? zone) && zone != null)
		{
			return zone.DisplayName;
		}

		if (!TryGetObjectOriginState(storageCell, out BuildableTileState state))
		{
			return "\uBCF4\uAD00\uD568";
		}

		return state.ObjectType switch
		{
			TileBuildType.SmallChest => "\uC791\uC740 \uC0C1\uC790",
			TileBuildType.IngredientCrate => "\uC2DD\uC7AC\uB8CC \uC0C1\uC790",
			TileBuildType.MaterialShelf => "\uC790\uC7AC \uC120\uBC18",
			TileBuildType.MedicineShelf => "\uC57D\uD488 \uC120\uBC18",
			TileBuildType.Storage => "\uAE30\uBCF8 \uCC3D\uACE0",
			TileBuildType.LargeStorage => "\uB300\uD615 \uCC3D\uACE0",
			_ => "\uBCF4\uAD00\uD568"
		};
	}

	public bool TryAddResourceToStorage(Vector2I storageCell, BaseResourceType type, int amount, out int storedAmount, out int leftoverAmount)
	{
		storedAmount = 0;
		leftoverAmount = Mathf.Max(0, amount);

		if (amount <= 0
			|| !TryGetStorageOriginCell(storageCell, out Vector2I originCell)
			|| !IsStoredResource(type)
			|| !CanStorageAcceptResource(originCell, type, false))
		{
			return false;
		}

		RegisterStorage(originCell);
		int freeSpace = GetStorageFreeSpace(originCell, type);
		storedAmount = Mathf.Min(amount, freeSpace);
		leftoverAmount = amount - storedAmount;

		if (storedAmount <= 0)
		{
			return false;
		}

		_storageInventories[originCell][type] = GetStoredAmountAt(originCell, type) + storedAmount;

		if (DebugStorageInventory)
		{
			GD.Print($"Stored {type} x{storedAmount} at Storage {originCell}");
		}

		ValidateLogisticsWorld("storage add");
		return true;
	}

	public int GetStorageAvailableAmount(Vector2I storageCell, BaseResourceType type)
	{
		return TryGetStorageOriginCell(storageCell, out Vector2I originCell)
			? GetStoredAmountAt(originCell, type)
			: 0;
	}

	public bool TryRemoveResourceFromStorage(Vector2I storageCell, BaseResourceType type, int requestedAmount, out int removedAmount)
	{
		removedAmount = 0;

		if (requestedAmount <= 0
			|| !TryGetStorageOriginCell(storageCell, out Vector2I originCell)
			|| !_storageInventories.TryGetValue(originCell, out Dictionary<BaseResourceType, int>? inventory))
		{
			return false;
		}

		int storedAmount = GetStoredAmountAt(originCell, type);
		removedAmount = Mathf.Min(requestedAmount, storedAmount);

		if (removedAmount <= 0)
		{
			return false;
		}

		inventory[type] = storedAmount - removedAmount;
		ValidateLogisticsWorld("storage withdraw");
		return true;
	}

	public float GetStorageDepositDuration(Vector2I storageOriginCell, BaseResourceType type, int amount)
	{
		return GetStorageInteractionDuration(storageOriginCell, type, amount, true);
	}

	public float GetStorageWithdrawDuration(Vector2I storageOriginCell, BaseResourceType type, int amount)
	{
		return GetStorageInteractionDuration(storageOriginCell, type, amount, false);
	}

	private float GetStorageInteractionDuration(Vector2I storageOriginCell, BaseResourceType type, int amount, bool isDeposit)
	{
		float baseDuration = GetStorageBaseInteractionDuration(storageOriginCell, isDeposit);
		int totalWeight = Mathf.Max(0, amount) * GetResourceUnitWeight(type);
		return Mathf.Clamp(baseDuration + totalWeight * 0.02f, 0.5f, 4.0f);
	}

	private float GetStorageBaseInteractionDuration(Vector2I storageOriginCell, bool isDeposit)
	{
		if (TryGetObjectOriginState(storageOriginCell, out BuildableTileState state))
		{
			return state.ObjectType switch
			{
				TileBuildType.SmallChest or TileBuildType.IngredientCrate or TileBuildType.MedicineShelf => 0.8f,
				TileBuildType.LargeStorage => 2.0f,
				TileBuildType.Storage => 1.3f,
				TileBuildType.MaterialShelf => 1.0f,
				_ => 1.3f
			};
		}

		return 1.3f;
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
				ValidateLogisticsWorld("storage remove");
				return true;
			}
		}

		ValidateLogisticsWorld("storage remove");
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
		int bestPriority = int.MinValue;
		bool bestSpecializedMatch = false;

		foreach (Vector2I candidateStorageCell in GetStorageCellsWithSpace(type))
		{
			StoragePolicy candidatePolicy = GetStoragePolicy(candidateStorageCell);
			StorageCapability candidateCapability = GetStorageCapability(candidateStorageCell);
			int candidatePriority = (int)candidatePolicy.Priority;
			bool candidateSpecializedMatch = candidateCapability.IsSpecializedMatch(type);

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

				bool isBetter = candidatePriority > bestPriority
					|| candidatePriority == bestPriority && candidateSpecializedMatch && !bestSpecializedMatch
					|| candidatePriority == bestPriority && candidateSpecializedMatch == bestSpecializedMatch && pathLength < nearestPathLength;

				if (!isBetter)
				{
					continue;
				}

				bestPriority = candidatePriority;
				bestSpecializedMatch = candidateSpecializedMatch;
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

	public bool TryFindNearestStorageAccessForResource(Vector2I fromCell, BaseResourceType type, out Vector2I storageCell, out Vector2I accessCell)
	{
		return TryFindNearestStorageAccessWithSpace(fromCell, type, out storageCell, out accessCell);
	}

	public bool TryFindNearestStorageAccessWithResource(Vector2I fromCell, BaseResourceType type, out Vector2I storageCell, out Vector2I accessCell)
	{
		Vector2I? nearestStorageCell = null;
		Vector2I? nearestAccessCell = null;
		int nearestPathLength = int.MaxValue;

		foreach (Vector2I candidateStorageCell in GetStorageOriginCells())
		{
			if (GetStoredAmountAt(candidateStorageCell, type) <= 0)
			{
				continue;
			}

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
			if (CanStorageAcceptResource(storageCell, type, true))
			{
				storageCells.Add(storageCell);
			}
		}

		return storageCells;
	}

	public bool TryReserveStorageInteraction(Vector2I originCell, MercenaryController user)
	{
		if (!TryGetStorageOriginCell(originCell, out Vector2I storageOriginCell))
		{
			return false;
		}

		if (_storageInteractionReservations.TryGetValue(storageOriginCell, out MercenaryController? reservedBy)
			&& GodotObject.IsInstanceValid(reservedBy)
			&& !reservedBy.IsQueuedForDeletion()
			&& reservedBy != user)
		{
			return false;
		}

		_storageInteractionReservations[storageOriginCell] = user;
		ValidateLogisticsWorld("storage interaction reserve");
		return true;
	}

	public void ReleaseStorageInteraction(Vector2I originCell, MercenaryController user)
	{
		if (!TryGetStorageOriginCell(originCell, out Vector2I storageOriginCell))
		{
			return;
		}

		if (_storageInteractionReservations.TryGetValue(storageOriginCell, out MercenaryController? reservedBy)
			&& (!GodotObject.IsInstanceValid(reservedBy) || reservedBy.IsQueuedForDeletion() || reservedBy == user))
		{
			_storageInteractionReservations.Remove(storageOriginCell);
			ValidateLogisticsWorld("storage interaction release");
		}
	}

	public bool IsStorageInteractionReservedByOther(Vector2I originCell, MercenaryController user)
	{
		if (!TryGetStorageOriginCell(originCell, out Vector2I storageOriginCell))
		{
			return false;
		}

		if (!_storageInteractionReservations.TryGetValue(storageOriginCell, out MercenaryController? reservedBy))
		{
			return false;
		}

		if (!GodotObject.IsInstanceValid(reservedBy) || reservedBy.IsQueuedForDeletion())
		{
			_storageInteractionReservations.Remove(storageOriginCell);
			return false;
		}

		return reservedBy != user;
	}

	public string GetStorageInteractionUserLabel(Vector2I storageCell)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell)
			|| !_storageInteractionReservations.TryGetValue(originCell, out MercenaryController? user)
			|| !GodotObject.IsInstanceValid(user)
			|| user.IsQueuedForDeletion())
		{
			return "\uC5C6\uC74C";
		}

		return user.MercenaryName;
	}

	public string GetStorageInteractionSummary(Vector2I storageCell)
	{
		string userLabel = GetStorageInteractionUserLabel(storageCell);
		return userLabel == "\uC5C6\uC74C"
			? "\uC0AC\uC6A9 \uC911: \uC5C6\uC74C"
			: $"\uC0AC\uC6A9 \uC911: {userLabel}";
	}

	public StoragePolicy GetStoragePolicy(Vector2I storageCell)
	{
		Vector2I originCell = TryGetStorageOriginCell(storageCell, out Vector2I resolvedCell) ? resolvedCell : storageCell;
		return EnsureStoragePolicy(originCell);
	}

	public StorageCapability GetStorageCapability(Vector2I storageCell)
	{
		Vector2I originCell = TryGetStorageOriginCell(storageCell, out Vector2I resolvedCell) ? resolvedCell : storageCell;

		if (IsStarterSupplyChest(originCell))
		{
			return StorageCapability.ForBuildType(TileBuildType.SmallChest, true);
		}

		if (TryGetStockpileZoneByOrigin(originCell, out StockpileZone? zone) && zone != null)
		{
			return new StorageCapability(
				zone.DisplayName,
				StoragePolicyPreset.All,
				StoragePriority.Low,
				1.0f,
				null,
				"\uC784\uC2DC \uC800\uC7A5 \uAD6C\uC5ED: \uBAA8\uB4E0 \uC790\uC6D0 \uBCF4\uAD00 \uAC00\uB2A5");
		}

		TileBuildType buildType = TryGetObjectOriginState(originCell, out BuildableTileState state)
			? state.ObjectType
			: TileBuildType.Storage;
		return StorageCapability.ForBuildType(buildType);
	}

	public string GetStoragePolicySummary(Vector2I storageCell)
	{
		StoragePolicy policy = GetStoragePolicy(storageCell);
		return $"{StoragePolicyHelpers.GetPresetDisplayName(policy.Preset)} / {StoragePolicyHelpers.GetPriorityDisplayName(policy.Priority)}";
	}

	public StoragePriority GetStoragePriority(Vector2I storageCell)
	{
		return GetStoragePolicy(storageCell).Priority;
	}

	public string GetStorageLimitText(Vector2I storageCell)
	{
		StorageCapability capability = GetStorageCapability(storageCell);
		int efficiency = Mathf.RoundToInt(capability.CapacityMultiplier * 100.0f);
		return $"{capability.LimitText}\n\uC6A9\uB7C9 \uD6A8\uC728: {efficiency}%";
	}

	public bool CanStorageCapabilityAllow(Vector2I storageCell, BaseResourceType type)
	{
		return GetStorageCapability(storageCell).Allows(type);
	}

	public bool CanStoragePolicyAllow(Vector2I storageCell, BaseResourceType type)
	{
		return CanStorageCapabilityAllow(storageCell, type) && GetStoragePolicy(storageCell).UserAllows(type);
	}

	public bool CanStorageAcceptResource(Vector2I storageCell, BaseResourceType type, bool requireCapacity)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell) || !IsStoredResource(type))
		{
			return false;
		}

		if (!CanStoragePolicyAllow(originCell, type))
		{
			return false;
		}

		return !requireCapacity || GetStorageFreeWeight(originCell) >= GetResourceUnitWeight(type);
	}

	public void CycleStoragePriority(Vector2I storageCell)
	{
		StoragePriority current = GetStoragePolicy(storageCell).Priority;
		StoragePriority next = current switch
		{
			StoragePriority.Low => StoragePriority.Normal,
			StoragePriority.Normal => StoragePriority.Preferred,
			StoragePriority.Preferred => StoragePriority.Important,
			StoragePriority.Important => StoragePriority.Critical,
			_ => StoragePriority.Low
		};
		SetStoragePriority(storageCell, next);
	}

	public void SetStoragePriority(Vector2I storageCell, StoragePriority priority)
	{
		if (TryGetStorageOriginCell(storageCell, out Vector2I originCell))
		{
			EnsureStoragePolicy(originCell).Priority = priority;
		}
	}

	public void ApplyStoragePolicyPreset(Vector2I storageCell, StoragePolicyPreset preset)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell))
		{
			return;
		}

		EnsureStoragePolicy(originCell).ApplyPreset(preset);
		EjectDisallowedStorageContents(originCell);
	}

	public void ToggleStorageResourceAllowed(Vector2I storageCell, BaseResourceType type)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell)
			|| !CanStorageCapabilityAllow(originCell, type))
		{
			return;
		}

		StoragePolicy policy = EnsureStoragePolicy(originCell);
		policy.SetAllowed(type, !policy.UserAllows(type));
		EjectDisallowedStorageContents(originCell);
	}

	public void SetStorageResourceAllowed(Vector2I storageCell, BaseResourceType type, bool allowed)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell)
			|| !CanStorageCapabilityAllow(originCell, type))
		{
			return;
		}

		StoragePolicy policy = EnsureStoragePolicy(originCell);
		policy.SetAllowed(type, allowed);
		EjectDisallowedStorageContents(originCell);
	}

	public void ToggleStorageCategoryAllowed(Vector2I storageCell, StorageResourceCategory category)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell))
		{
			return;
		}

		StoragePolicy policy = EnsureStoragePolicy(originCell);
		bool allow = !policy.IsCategoryFullyAllowed(category);

		foreach (BaseResourceType resourceType in StoragePolicyHelpers.GetResourcesInCategory(category))
		{
			if (CanStorageCapabilityAllow(originCell, resourceType))
			{
				policy.SetAllowed(resourceType, allow);
			}
		}

		EjectDisallowedStorageContents(originCell);
	}

	public void SetStorageCategoryAllowed(Vector2I storageCell, StorageResourceCategory category, bool allowed)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell))
		{
			return;
		}

		StoragePolicy policy = EnsureStoragePolicy(originCell);

		foreach (BaseResourceType resourceType in StoragePolicyHelpers.GetResourcesInCategory(category))
		{
			if (CanStorageCapabilityAllow(originCell, resourceType))
			{
				policy.SetAllowed(resourceType, allowed);
			}
		}

		EjectDisallowedStorageContents(originCell);
	}

	public bool EjectDisallowedStorageContents(Vector2I storageCell)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell)
			|| !_storageInventories.TryGetValue(originCell, out Dictionary<BaseResourceType, int>? inventory))
		{
			return false;
		}

		bool changed = false;

		foreach (BaseResourceType resourceType in ResourceDefinitionDatabase.AllResourceTypes)
		{
			int amount = inventory.TryGetValue(resourceType, out int storedAmount) ? storedAmount : 0;

			if (amount <= 0 || CanStoragePolicyAllow(originCell, resourceType))
			{
				continue;
			}

			inventory[resourceType] = 0;

			if (!TrySpawnOrMergeResourcePile(resourceType, originCell, amount))
			{
				inventory[resourceType] = amount;
				GD.PushWarning($"Storage policy cleanup failed to drop {resourceType} x{amount} near {originCell}");
				continue;
			}

			changed = true;
		}

		if (changed)
		{
			ValidateLogisticsWorld("storage policy cleanup");
		}

		return changed;
	}

	public bool CleanupStorageContentsByPolicy(Vector2I storageCell)
	{
		return EjectDisallowedStorageContents(storageCell);
	}

	private bool EjectAllStorageContents(Vector2I storageCell)
	{
		if (!TryGetStorageOriginCell(storageCell, out Vector2I originCell)
			|| !_storageInventories.TryGetValue(originCell, out Dictionary<BaseResourceType, int>? inventory))
		{
			return true;
		}

		bool success = true;

		foreach (BaseResourceType resourceType in ResourceDefinitionDatabase.AllResourceTypes)
		{
			int amount = inventory.TryGetValue(resourceType, out int storedAmount) ? storedAmount : 0;

			if (amount <= 0)
			{
				continue;
			}

			inventory[resourceType] = 0;

			if (!TrySpawnOrMergeResourcePile(resourceType, originCell, amount))
			{
				inventory[resourceType] = amount;
				GD.PushWarning($"Storage eject failed to drop {resourceType} x{amount} near {originCell}");
				success = false;
			}
		}

		return success;
	}

	private StoragePolicy EnsureStoragePolicy(Vector2I storageCell)
	{
		if (_storagePolicies.TryGetValue(storageCell, out StoragePolicy? policy))
		{
			policy.EnsureAllResources();
			return policy;
		}

		StorageCapability capability = GetStorageCapability(storageCell);
		policy = new StoragePolicy(capability.DefaultPreset, capability.DefaultPriority)
		{
			PolicyName = capability.DisplayName
		};
		_storagePolicies[storageCell] = policy;
		return policy;
	}

	public IReadOnlyList<StockpileZone> GetStockpileZones()
	{
		return _stockpileZones;
	}

	public IReadOnlyList<StockpileZone> GetAllStockpileZones()
	{
		return _stockpileZones;
	}

	public bool TryGetStockpileZoneAtCell(Vector2I cell, out StockpileZone? zone)
	{
		foreach (StockpileZone candidate in _stockpileZones)
		{
			if (candidate.Cells.Contains(cell))
			{
				zone = candidate;
				return true;
			}
		}

		zone = null;
		return false;
	}

	public bool TryCreateStockpileZone(IEnumerable<Vector2I> cells, out StockpileZone? zone)
	{
		zone = null;
		HashSet<Vector2I> validCells = new();

		foreach (Vector2I cell in cells)
		{
			if (IsCellInWorld(cell)
				&& _buildings.TryGetValue(cell, out BuildableTileState state)
				&& state.HasFloor
				&& !state.HasObject
				&& !IsConstructionSiteCell(cell))
			{
				validCells.Add(cell);
			}
		}

		if (validCells.Count == 0)
		{
			return false;
		}

		RemoveStockpileZonesOverlapping(validCells);

		foreach (Vector2I cell in validCells)
		{
			if (TryGetStockpileZoneAtCell(cell, out _))
			{
				return false;
			}
		}

		zone = new StockpileZone
		{
			ZoneId = _nextStockpileZoneId++,
			DisplayName = $"\uC800\uC7A5 \uAD6C\uC5ED {_nextStockpileZoneId - 1}",
			Bounds = CalculateBounds(validCells)
		};

		foreach (Vector2I cell in validCells)
		{
			zone.Cells.Add(cell);
		}

		Vector2I originCell = GetStockpileOriginCell(zone);
		_stockpileZones.Add(zone);
		RegisterStorage(originCell);
		_storagePolicies[originCell] = zone.Policy;
		QueueRedraw();
		ValidateLogisticsWorld("stockpile zone create");
		return true;
	}

	public int RemoveStockpileZonesOverlapping(IEnumerable<Vector2I> cells)
	{
		HashSet<Vector2I> selectedCells = new(cells);
		List<StockpileZone> zonesToRemove = new();

		foreach (StockpileZone zone in _stockpileZones)
		{
			foreach (Vector2I cell in selectedCells)
			{
				if (zone.Cells.Contains(cell))
				{
					zonesToRemove.Add(zone);
					break;
				}
			}
		}

		int removedCount = 0;

		foreach (StockpileZone zone in zonesToRemove)
		{
			Vector2I originCell = GetStockpileOriginCell(zone);
			if (!EjectAllStorageContents(originCell))
			{
				GD.PushWarning($"Stockpile zone removal skipped because contents could not be dropped: {zone.DisplayName}");
				continue;
			}

			UnregisterStorage(originCell);
			_stockpileZones.Remove(zone);
			removedCount++;
		}

		if (removedCount > 0)
		{
			QueueRedraw();
			ValidateLogisticsWorld("stockpile zone remove");
		}

		return removedCount;
	}

	public bool DeleteStockpileZone(StockpileZone zone)
	{
		if (!_stockpileZones.Contains(zone))
		{
			return false;
		}

		Vector2I originCell = GetStockpileOriginCell(zone);
		if (!EjectAllStorageContents(originCell))
		{
			GD.PushWarning($"Stockpile zone delete skipped because contents could not be dropped: {zone.DisplayName}");
			return false;
		}

		UnregisterStorage(originCell);
		_stockpileZones.Remove(zone);
		QueueRedraw();
		ValidateLogisticsWorld("stockpile zone delete");
		return true;
	}

	public bool TryAddResourceToStockpileZone(StockpileZone zone, BaseResourceType type, int amount, out int storedAmount, out int leftoverAmount)
	{
		return TryAddResourceToStorage(GetStockpileOriginCell(zone), type, amount, out storedAmount, out leftoverAmount);
	}

	public bool TryRemoveResourceFromStockpileZone(StockpileZone zone, BaseResourceType type, int requestedAmount, out int removedAmount)
	{
		return TryRemoveResourceFromStorage(GetStockpileOriginCell(zone), type, requestedAmount, out removedAmount);
	}

	public bool TryGetStockpileZoneContents(StockpileZone zone, out Dictionary<BaseResourceType, int> contents)
	{
		return TryGetStorageContents(GetStockpileOriginCell(zone), out contents);
	}

	public int GetStockpileZoneCapacity(StockpileZone zone)
	{
		return zone.WeightCapacity;
	}

	public bool CleanupStockpileZoneContentsByPolicy(StockpileZone zone)
	{
		return EjectDisallowedStorageContents(GetStockpileOriginCell(zone));
	}

	private bool TryGetStockpileZoneByOrigin(Vector2I originCell, out StockpileZone? zone)
	{
		foreach (StockpileZone candidate in _stockpileZones)
		{
			if (GetStockpileOriginCell(candidate) == originCell)
			{
				zone = candidate;
				return true;
			}
		}

		zone = null;
		return false;
	}

	private static Vector2I GetStockpileOriginCell(StockpileZone zone)
	{
		Vector2I originCell = default;
		bool hasCell = false;

		foreach (Vector2I cell in zone.Cells)
		{
			if (!hasCell || cell.Y < originCell.Y || (cell.Y == originCell.Y && cell.X < originCell.X))
			{
				originCell = cell;
				hasCell = true;
			}
		}

		return originCell;
	}

	private static Rect2I CalculateBounds(IEnumerable<Vector2I> cells)
	{
		bool hasCell = false;
		int minX = 0;
		int minY = 0;
		int maxX = 0;
		int maxY = 0;

		foreach (Vector2I cell in cells)
		{
			if (!hasCell)
			{
				minX = maxX = cell.X;
				minY = maxY = cell.Y;
				hasCell = true;
				continue;
			}

			minX = Mathf.Min(minX, cell.X);
			minY = Mathf.Min(minY, cell.Y);
			maxX = Mathf.Max(maxX, cell.X);
			maxY = Mathf.Max(maxY, cell.Y);
		}

		return hasCell
			? new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1)
			: new Rect2I();
	}

	public bool TryCreateConstructionSite(TileBuildType buildType, Vector2I originCell)
	{
		return TryCreateConstructionSite(buildType, originCell, CurrentBuildMaterialType);
	}

	public bool TryCreateConstructionSite(TileBuildType buildType, Vector2I originCell, BuildMaterialType materialType)
	{
		if (!UsesDirectConstruction(buildType) || !CanPlaceAt(originCell, buildType))
		{
			return false;
		}

		Vector2I size = GetBuildObjectSize(buildType);
		List<Vector2I> occupiedCells = new(GetObjectCells(originCell, size));

		foreach (Vector2I occupiedCell in occupiedCells)
		{
			if (IsConstructionSiteCell(occupiedCell)
				|| IsResourcePileCell(occupiedCell)
				|| IsResourceNodeCell(occupiedCell)
				|| IsCropPlantCell(occupiedCell))
			{
				return false;
			}
		}

		BuildStructureDefinition definition = GetBuildDefinition(buildType, materialType);
		ConstructionSite site = new()
		{
			SiteId = _nextConstructionSiteId++,
			TargetBuildType = buildType,
			MaterialType = definition.MaterialType,
			OriginCell = originCell,
			Size = size,
			DisplayName = definition.DisplayName,
			RequiredWork = GetConstructionRequiredWork(buildType, definition)
		};

		site.OccupiedCells.AddRange(occupiedCells);
		site.SetRequirements(definition.Cost);
		_constructionSites.Add(site);
		QueueRedraw();
		ValidateLogisticsWorld("construction site create");
		return true;
	}

	public bool TryGetConstructionSiteAtCell(Vector2I cell, out ConstructionSite site)
	{
		foreach (ConstructionSite candidate in _constructionSites)
		{
			if (!candidate.IsCompleted && !candidate.IsCancelled && candidate.OccupiedCells.Contains(cell))
			{
				site = candidate;
				return true;
			}
		}

		site = null!;
		return false;
	}

	public IReadOnlyList<ConstructionSite> GetAllConstructionSites()
	{
		return _constructionSites;
	}

	public bool TryFindConstructionBuildWork(Vector2I fromCell, MercenaryController worker, out ConstructionSite site, out Vector2I accessCell)
	{
		site = null!;
		accessCell = default;
		int bestPathLength = int.MaxValue;

		foreach (ConstructionSite candidate in _constructionSites)
		{
			candidate.PruneReservations();

			if (candidate.IsCompleted
				|| candidate.IsCancelled
				|| !candidate.HasAllMaterials
				|| candidate.ReservedBuildWorker is { } reserved && reserved != worker)
			{
				continue;
			}

			if (!TryFindNearestConstructionAccessCell(candidate, fromCell, out Vector2I candidateAccessCell, out int pathLength))
			{
				continue;
			}

			if (pathLength >= bestPathLength)
			{
				continue;
			}

			bestPathLength = pathLength;
			site = candidate;
			accessCell = candidateAccessCell;
		}

		return site != null;
	}

	public bool TryFindConstructionMaterialDelivery(
		Vector2I fromCell,
		MercenaryController worker,
		out ConstructionSite site,
		out BaseResourceType resourceType,
		out int amount,
		out Vector2I storageCell,
		out Vector2I storageAccessCell,
		out Vector2I siteAccessCell)
	{
		site = null!;
		resourceType = BaseResourceType.Wood;
		amount = 0;
		storageCell = default;
		storageAccessCell = default;
		siteAccessCell = default;
		int bestPathLength = int.MaxValue;

		foreach (ConstructionSite candidate in _constructionSites)
		{
			candidate.PruneReservations();
			ConstructionRequirement? requirement = candidate.GetFirstMissingRequirement();

			if (candidate.IsCompleted
				|| candidate.IsCancelled
				|| requirement == null
				|| candidate.ReservedDeliveryWorker is { } reserved && reserved != worker)
			{
				continue;
			}

			if (!TryFindNearestStorageAccessWithResource(fromCell, requirement.ResourceType, out Vector2I candidateStorageCell, out Vector2I candidateStorageAccessCell))
			{
				continue;
			}

			if (!TryFindNearestConstructionAccessCell(candidate, candidateStorageAccessCell, out Vector2I candidateSiteAccessCell, out int sitePathLength))
			{
				continue;
			}

			List<Vector2I> storagePath = GridPathfinder.FindPath(fromCell, candidateStorageAccessCell, this);

			if (fromCell != candidateStorageAccessCell && storagePath.Count == 0)
			{
				continue;
			}

			int pathLength = (fromCell == candidateStorageAccessCell ? 0 : storagePath.Count) + sitePathLength;

			if (pathLength >= bestPathLength)
			{
				continue;
			}

			bestPathLength = pathLength;
			site = candidate;
			resourceType = requirement.ResourceType;
			amount = requirement.RemainingAmount;
			storageCell = candidateStorageCell;
			storageAccessCell = candidateStorageAccessCell;
			siteAccessCell = candidateSiteAccessCell;
		}

		return site != null && amount > 0;
	}

	public bool TryReserveSiteMaterialDelivery(ConstructionSite site, MercenaryController worker)
	{
		return _constructionSites.Contains(site) && site.TryReserveMaterialDelivery(worker);
	}

	public void ReleaseSiteMaterialDelivery(ConstructionSite site, MercenaryController worker)
	{
		if (_constructionSites.Contains(site))
		{
			site.ReleaseMaterialDelivery(worker);
		}
	}

	public bool TryReserveBuildWork(ConstructionSite site, MercenaryController worker)
	{
		return _constructionSites.Contains(site) && site.TryReserveBuildWork(worker);
	}

	public void ReleaseBuildWork(ConstructionSite site, MercenaryController worker)
	{
		if (_constructionSites.Contains(site))
		{
			site.ReleaseBuildWork(worker);
		}
	}

	public bool TryDeliverMaterial(ConstructionSite site, BaseResourceType type, int amount, out int acceptedAmount)
	{
		acceptedAmount = 0;

		if (!_constructionSites.Contains(site) || amount <= 0)
		{
			return false;
		}

		acceptedAmount = site.AcceptMaterial(type, amount);
		QueueRedraw();
		ValidateLogisticsWorld("construction material deliver");
		return acceptedAmount > 0;
	}

	public bool AddBuildProgress(ConstructionSite site, float amount)
	{
		if (!_constructionSites.Contains(site) || site.IsCancelled)
		{
			return false;
		}

		site.AddBuildProgress(amount);

		if (site.IsCompleted)
		{
			CompleteConstructionSite(site);
		}

		QueueRedraw();
		return true;
	}

	public bool AddBuildProgress(ConstructionSite site, MercenaryController worker, float amount)
	{
		if (site.ReservedBuildWorker != null && site.ReservedBuildWorker != worker)
		{
			return false;
		}

		return AddBuildProgress(site, amount);
	}

	public bool CompleteSite(ConstructionSite site)
	{
		if (!_constructionSites.Contains(site) || site.IsCancelled)
		{
			return false;
		}

		CompleteConstructionSite(site);
		QueueRedraw();
		return true;
	}

	public bool CancelSite(ConstructionSite site)
	{
		if (!_constructionSites.Contains(site) || site.IsCompleted)
		{
			return false;
		}

		DropDeliveredConstructionMaterials(site);
		site.Cancel();
		_constructionSites.Remove(site);
		QueueRedraw();
		ValidateLogisticsWorld("construction cancel");
		return true;
	}

	private void CompleteConstructionSite(ConstructionSite site)
	{
		ApplyBuildAt(site.OriginCell, site.TargetBuildType, site.MaterialType);
		site.Complete();
		_constructionSites.Remove(site);
		ValidateLogisticsWorld("construction complete");
	}

	private bool TryCancelConstructionSiteAt(Vector2I cell)
	{
		if (!TryGetConstructionSiteAtCell(cell, out ConstructionSite site))
		{
			return false;
		}

		return CancelSite(site);
	}

	private void DropDeliveredConstructionMaterials(ConstructionSite site)
	{
		foreach (ConstructionRequirement requirement in site.Requirements.Values)
		{
			if (requirement.DeliveredAmount <= 0)
			{
				continue;
			}

			if (!TrySpawnOrMergeResourcePile(requirement.ResourceType, site.OriginCell, requirement.DeliveredAmount))
			{
				GD.PushWarning($"Construction cancel failed to drop {requirement.ResourceType} x{requirement.DeliveredAmount} at {site.OriginCell}");
			}
		}
	}

	private bool IsConstructionSiteCell(Vector2I cell)
	{
		return TryGetConstructionSiteAtCell(cell, out _);
	}

	private bool TryFindNearestConstructionAccessCell(ConstructionSite site, Vector2I fromCell, out Vector2I accessCell, out int pathLength)
	{
		accessCell = default;
		pathLength = int.MaxValue;

		foreach (Vector2I candidate in GetConstructionAccessCells(site))
		{
			if (!IsCellInWorld(candidate) || IsCellBlocked(candidate))
			{
				continue;
			}

			List<Vector2I> path = GridPathfinder.FindPath(fromCell, candidate, this);

			if (fromCell != candidate && path.Count == 0)
			{
				continue;
			}

			int candidateLength = fromCell == candidate ? 0 : path.Count;

			if (candidateLength >= pathLength)
			{
				continue;
			}

			pathLength = candidateLength;
			accessCell = candidate;
		}

		return pathLength != int.MaxValue;
	}

	private IEnumerable<Vector2I> GetConstructionAccessCells(ConstructionSite site)
	{
		Vector2I[] offsets =
		{
			new(0, 0),
			new(1, 0),
			new(-1, 0),
			new(0, 1),
			new(0, -1)
		};

		HashSet<Vector2I> yielded = new();

		foreach (Vector2I occupiedCell in site.OccupiedCells)
		{
			foreach (Vector2I offset in offsets)
			{
				Vector2I candidate = occupiedCell + offset;

				if (yielded.Add(candidate))
				{
					yield return candidate;
				}
			}
		}
	}

	private float GetConstructionRequiredWork(TileBuildType buildType, BuildStructureDefinition definition)
	{
		int area = Mathf.Max(1, GetBuildObjectSize(buildType).X * GetBuildObjectSize(buildType).Y);
		int totalWeight = 0;

		foreach (KeyValuePair<BaseResourceType, int> cost in definition.Cost)
		{
			totalWeight += cost.Value * GetResourceUnitWeight(cost.Key);
		}

		return Mathf.Clamp((3.0f + area * 2.0f + totalWeight * 0.1f) * definition.RequiredWorkMultiplier, 3.0f, 40.0f);
	}

	public bool TryFindNearestUsableFurnitureAccess(
		Vector2I fromCell,
		FurnitureUseType useType,
		MercenaryController user,
		out Vector2I furnitureOriginCell,
		out Vector2I accessCell,
		out TileBuildType furnitureType)
	{
		furnitureOriginCell = default;
		accessCell = default;
		furnitureType = TileBuildType.None;
		int nearestPathLength = int.MaxValue;
		CleanInvalidFurnitureReservations();

		foreach (BuildableTileState state in _buildings.Values)
		{
			if (!state.HasObject
				|| !state.IsObjectOrigin
				|| !CanUseFurnitureFor(state.ObjectType, useType)
				|| IsFurnitureReservedByOther(state.ObjectOriginCell, user))
			{
				continue;
			}

			foreach (Vector2I candidateAccessCell in GetObjectAccessCells(state.ObjectOriginCell))
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
				furnitureOriginCell = state.ObjectOriginCell;
				accessCell = candidateAccessCell;
				furnitureType = state.ObjectType;
			}
		}

		return furnitureType != TileBuildType.None;
	}

	public bool TryReserveFurniture(Vector2I originCell, MercenaryController user)
	{
		originCell = GetObjectOriginCell(originCell);
		CleanInvalidFurnitureReservations();

		if (_furnitureReservations.TryGetValue(originCell, out MercenaryController? reservedBy)
			&& GodotObject.IsInstanceValid(reservedBy)
			&& !reservedBy.IsQueuedForDeletion()
			&& reservedBy != user)
		{
			return false;
		}

		_furnitureReservations[originCell] = user;
		QueueRedraw();
		ValidateLogisticsWorld("furniture reserve");
		return true;
	}

	public void ReleaseFurnitureReservation(Vector2I originCell, MercenaryController user)
	{
		originCell = GetObjectOriginCell(originCell);

		if (_furnitureReservations.TryGetValue(originCell, out MercenaryController? reservedBy)
			&& (!GodotObject.IsInstanceValid(reservedBy) || reservedBy.IsQueuedForDeletion() || reservedBy == user))
		{
			_furnitureReservations.Remove(originCell);
			QueueRedraw();
			ValidateLogisticsWorld("furniture release");
		}
	}

	public bool IsFurnitureReservedByOther(Vector2I originCell, MercenaryController user)
	{
		originCell = GetObjectOriginCell(originCell);

		if (!_furnitureReservations.TryGetValue(originCell, out MercenaryController? reservedBy))
		{
			return false;
		}

		if (!GodotObject.IsInstanceValid(reservedBy) || reservedBy.IsQueuedForDeletion())
		{
			_furnitureReservations.Remove(originCell);
			QueueRedraw();
			return false;
		}

		return reservedBy != user;
	}

	private void CleanInvalidFurnitureReservations()
	{
		if (_furnitureReservations.Count == 0)
		{
			return;
		}

		List<Vector2I> staleReservations = new();

		foreach (KeyValuePair<Vector2I, MercenaryController> reservation in _furnitureReservations)
		{
			MercenaryController reservedBy = reservation.Value;

			if (!GodotObject.IsInstanceValid(reservedBy)
				|| reservedBy.IsQueuedForDeletion()
				|| !TryGetObjectOriginState(reservation.Key, out BuildableTileState state)
				|| state.ObjectOriginCell != reservation.Key)
			{
				staleReservations.Add(reservation.Key);
			}
		}

		foreach (Vector2I originCell in staleReservations)
		{
			_furnitureReservations.Remove(originCell);
		}

		if (staleReservations.Count > 0)
		{
			QueueRedraw();
		}
	}

	private bool IsFurnitureReserved(Vector2I originCell)
	{
		originCell = GetObjectOriginCell(originCell);

		if (!_furnitureReservations.TryGetValue(originCell, out MercenaryController? reservedBy))
		{
			return false;
		}

		if (!GodotObject.IsInstanceValid(reservedBy) || reservedBy.IsQueuedForDeletion())
		{
			_furnitureReservations.Remove(originCell);
			QueueRedraw();
			return false;
		}

		return true;
	}

	public FacilityType GetFacilityTypeAt(Vector2I cell)
	{
		return TryGetObjectOriginState(cell, out BuildableTileState state)
			? GetFacilityTypeForObject(state.ObjectType)
			: FacilityType.None;
	}

	public float GetRoomQualityBonusAtCell(Vector2I cell)
	{
		float bonus = 0.0f;

		if (_buildings.TryGetValue(cell, out BuildableTileState state))
		{
			bonus += state.FloorRoomQualityBonus;
		}

		if (TryGetObjectOriginState(cell, out BuildableTileState originState) && originState.IsObjectOrigin)
		{
			bonus += originState.ObjectRoomQualityBonus;
		}

		return bonus;
	}

	public List<FacilityInfo> GetFacilities()
	{
		List<FacilityInfo> facilities = new();

		foreach (BuildableTileState state in _buildings.Values)
		{
			if (state.HasObject && !state.IsObjectOrigin)
			{
				continue;
			}

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
			if (state.HasObject && !state.IsObjectOrigin)
			{
				continue;
			}

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
		if (!TryGetFacilityOriginCell(cell, out Vector2I originCell))
		{
			return false;
		}

		PruneInvalidFacilityReservation(originCell);
		return _facilityReservations.ContainsKey(originCell);
	}

	public bool IsFacilityReservedBy(Vector2I cell, MercenaryController mercenary)
	{
		if (!TryGetFacilityOriginCell(cell, out Vector2I originCell))
		{
			return false;
		}

		PruneInvalidFacilityReservation(originCell);
		return _facilityReservations.TryGetValue(originCell, out MercenaryController? reservedBy) && reservedBy == mercenary;
	}

	public bool TryReserveFacility(Vector2I cell, MercenaryController mercenary)
	{
		if (!TryGetFacilityOriginCell(cell, out Vector2I originCell))
		{
			return false;
		}

		PruneInvalidFacilityReservation(originCell);
		PruneInvalidFacilityOccupancy(originCell);

		if (_facilityOccupants.TryGetValue(originCell, out MercenaryController? occupant) && occupant != mercenary)
		{
			return false;
		}

		if (_facilityReservations.TryGetValue(originCell, out MercenaryController? reservedBy))
		{
			return reservedBy == mercenary;
		}

		_facilityReservations[originCell] = mercenary;
		return true;
	}

	public void ReleaseFacilityReservation(Vector2I cell, MercenaryController mercenary)
	{
		if (!TryGetFacilityOriginCell(cell, out Vector2I originCell))
		{
			return;
		}

		PruneInvalidFacilityReservation(originCell);

		if (_facilityReservations.TryGetValue(originCell, out MercenaryController? reservedBy) && reservedBy == mercenary)
		{
			_facilityReservations.Remove(originCell);
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
		if (!TryGetFacilityOriginCell(cell, out Vector2I originCell))
		{
			return false;
		}

		PruneInvalidFacilityOccupancy(originCell);
		return _facilityOccupants.ContainsKey(originCell);
	}

	public bool IsFacilityOccupiedBy(Vector2I cell, MercenaryController mercenary)
	{
		if (!TryGetFacilityOriginCell(cell, out Vector2I originCell))
		{
			return false;
		}

		PruneInvalidFacilityOccupancy(originCell);
		return _facilityOccupants.TryGetValue(originCell, out MercenaryController? occupiedBy) && occupiedBy == mercenary;
	}

	public bool TryOccupyFacility(Vector2I cell, MercenaryController mercenary)
	{
		if (!TryGetFacilityOriginCell(cell, out Vector2I originCell))
		{
			return false;
		}

		PruneInvalidFacilityReservation(originCell);
		PruneInvalidFacilityOccupancy(originCell);

		if (_facilityReservations.TryGetValue(originCell, out MercenaryController? reservedBy) && reservedBy != mercenary)
		{
			return false;
		}

		if (_facilityOccupants.TryGetValue(originCell, out MercenaryController? occupiedBy))
		{
			return occupiedBy == mercenary;
		}

		_facilityOccupants[originCell] = mercenary;
		_facilityReservations.Remove(originCell);
		return true;
	}

	public void ReleaseFacilityOccupancy(Vector2I cell, MercenaryController mercenary)
	{
		if (!TryGetFacilityOriginCell(cell, out Vector2I originCell))
		{
			return;
		}

		PruneInvalidFacilityOccupancy(originCell);

		if (_facilityOccupants.TryGetValue(originCell, out MercenaryController? occupiedBy) && occupiedBy == mercenary)
		{
			_facilityOccupants.Remove(originCell);
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

			Vector2I targetCell = GetNearestObjectAccessCell(startCell, candidate.Cell);
			List<Vector2I> path = GridPathfinder.FindPath(startCell, targetCell, this);

			if (startCell != targetCell && path.Count == 0)
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
			DrawBuildTileFloor(state);
		}

		foreach (BuildableTileState state in _buildings.Values)
		{
			DrawBuildObject(state);
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
		if (BuildStructureDefinitions.IsMaterialSensitiveBuildType(buildType))
		{
			return GetBuildDefinition(buildType, CurrentBuildMaterialType).Cost;
		}

		return BuildDefinitionDatabase.GetCost(buildType).Resources;
	}

	public IReadOnlyDictionary<BaseResourceType, int> GetBuildCost(TileBuildType buildType, BuildMaterialType materialType)
	{
		return BuildStructureDefinitions.IsMaterialSensitiveBuildType(buildType)
			? GetBuildDefinition(buildType, materialType).Cost
			: GetBuildCost(buildType);
	}

	public BuildStructureDefinition GetCurrentBuildDefinition(TileBuildType buildType)
	{
		return GetBuildDefinition(buildType, CurrentBuildMaterialType);
	}

	public bool UsesDirectConstructionSite(TileBuildType buildType)
	{
		return UsesDirectConstruction(buildType);
	}

	private static bool UsesDirectConstruction(TileBuildType buildType)
	{
		return BuildDefinitionDatabase.UsesDirectConstruction(buildType);
	}

	public static string GetBuildDisplayName(TileBuildType buildType)
	{
		return BuildDefinitionDatabase.GetDisplayName(buildType);
	}

	private BuildStructureDefinition GetBuildDefinition(TileBuildType buildType, BuildMaterialType materialType)
	{
		return BuildDefinitionDatabase.Get(buildType, materialType);
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

		foreach (BaseResourceType resourceType in ResourceDefinitionDatabase.BuildCostResourceOrder)
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

		foreach (BaseResourceType resourceType in ResourceDefinitionDatabase.BuildCostResourceOrder)
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
		if (IsStoredResource(type))
		{
			return GetTotalStoredAmount(type);
		}

		return _resources.TryGetValue(type, out int amount) ? amount : 0;
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
			return TryRemoveResourceFromStorages(type, amount);
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

		if (IsStoredResource(type))
		{
			AddResourceToAvailableStorage(type, amount);
			return;
		}

		_resources[type] = (_resources.TryGetValue(type, out int legacyAmount) ? legacyAmount : 0) + amount;
	}

	public void SetResourceAmount(BaseResourceType type, int amount)
	{
		if (IsStoredResource(type))
		{
			ClearStoredResource(type);
			AddResourceToAvailableStorage(type, Mathf.Max(0, amount));
			return;
		}

		_resources[type] = Mathf.Max(0, amount);
	}

	private void AddResourceToAvailableStorage(BaseResourceType type, int amount)
	{
		int remaining = amount;
		List<Vector2I> storageCells = new(GetStorageOriginCells());
		storageCells.Sort((a, b) =>
		{
			int priorityCompare = GetStoragePolicy(b).Priority.CompareTo(GetStoragePolicy(a).Priority);

			if (priorityCompare != 0)
			{
				return priorityCompare;
			}

			bool aSpecialized = GetStorageCapability(a).IsSpecializedMatch(type);
			bool bSpecialized = GetStorageCapability(b).IsSpecializedMatch(type);

			if (aSpecialized != bSpecialized)
			{
				return bSpecialized.CompareTo(aSpecialized);
			}

			return a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y);
		});

		foreach (Vector2I storageCell in storageCells)
		{
			if (remaining <= 0)
			{
				break;
			}

			if (TryAddResourceToStorage(storageCell, type, remaining, out int storedAmount, out int leftoverAmount))
			{
				remaining = leftoverAmount;

				if (storedAmount <= 0)
				{
					break;
				}
			}
		}

		if (remaining > 0)
		{
			GD.PushWarning($"No storage space for debug resource {type} x{remaining}");
		}
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
		foreach (BaseResourceType resourceType in ResourceDefinitionDatabase.AllResourceTypes)
		{
			_resources[resourceType] = 0;
		}

		EnsureStarterSupplyChest();
	}

	private void EnsureStarterSupplyChest()
	{
		if (_starterSupplyChestCell.HasValue
			&& TryGetObjectOriginState(_starterSupplyChestCell.Value, out BuildableTileState existingStarter)
			&& existingStarter.ObjectType == TileBuildType.SmallChest)
		{
			RegisterStorage(_starterSupplyChestCell.Value);
			EnsureStarterSupplyContents(_starterSupplyChestCell.Value);
			return;
		}

		if (!FindStarterSupplyChestCell(out Vector2I chestCell))
		{
			GD.PushWarning("Could not place starter supply chest.");
			return;
		}

		if (!_buildings.TryGetValue(chestCell, out BuildableTileState state) || !state.HasFloor)
		{
			ApplyBuildAt(chestCell, TileBuildType.Floor, BuildMaterialType.Wood);
		}

		_starterSupplyChestCell = chestCell;
		ApplyBuildAt(chestCell, TileBuildType.SmallChest);
		RegisterStorage(chestCell);
		_storagePolicies[chestCell] = new StoragePolicy(StoragePolicyPreset.All, StoragePriority.Normal)
		{
			PolicyName = "\uCD08\uAE30 \uBCF4\uAE09 \uC0C1\uC790"
		};
		EnsureStarterSupplyContents(chestCell);
	}

	private bool FindStarterSupplyChestCell(out Vector2I chestCell)
	{
		Vector2I preferredCell = new(8, 8);

		if (CanUseStarterSupplyChestCell(preferredCell))
		{
			chestCell = preferredCell;
			return true;
		}

		for (int radius = 1; radius <= 12; radius++)
		{
			for (int y = -radius; y <= radius; y++)
			{
				for (int x = -radius; x <= radius; x++)
				{
					if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
					{
						continue;
					}

					Vector2I candidate = preferredCell + new Vector2I(x, y);

					if (CanUseStarterSupplyChestCell(candidate))
					{
						chestCell = candidate;
						return true;
					}
				}
			}
		}

		chestCell = default;
		return false;
	}

	private bool CanUseStarterSupplyChestCell(Vector2I cell)
	{
		if (!IsCellInWorld(cell)
			|| IsResourceNodeCell(cell)
			|| IsCropPlantCell(cell)
			|| IsResourcePileCell(cell)
			|| IsConstructionSiteCell(cell)
			|| IsCellOccupied(cell))
		{
			return false;
		}

		return !_buildings.TryGetValue(cell, out BuildableTileState state) || !state.HasObject;
	}

	private void EnsureStarterSupplyContents(Vector2I chestCell)
	{
		RegisterStorage(chestCell);

		if (!_storageInventories.TryGetValue(chestCell, out Dictionary<BaseResourceType, int>? inventory))
		{
			return;
		}

		if (!IsInventoryEmpty(inventory))
		{
			return;
		}

		inventory[BaseResourceType.Wood] = 30;
		inventory[BaseResourceType.Stone] = 10;
		inventory[BaseResourceType.Food] = 20;
		inventory[BaseResourceType.IronOre] = 4;
		inventory[BaseResourceType.Coal] = 2;
	}

	private bool IsStarterSupplyChest(Vector2I storageCell)
	{
		return _starterSupplyChestCell.HasValue && _starterSupplyChestCell.Value == storageCell;
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

		if (existingPile != null)
		{
			return existingPile.ResourceType == type;
		}

		if (IsResourceNodeCell(cell)
			|| IsCropPlantCell(cell)
			|| IsConstructionSiteCell(cell)
			|| IsCellBlocked(cell))
		{
			return false;
		}

		return !_buildings.TryGetValue(cell, out BuildableTileState state) || !state.HasObject;
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

	private IEnumerable<Vector2I> GetStorageAccessCells(Vector2I storageCell)
	{
		if (TryGetStockpileZoneByOrigin(storageCell, out StockpileZone? zone) && zone != null)
		{
			foreach (Vector2I cell in zone.Cells)
			{
				yield return cell;
			}

			yield break;
		}

		foreach (Vector2I accessCell in GetObjectAccessCells(storageCell))
		{
			yield return accessCell;
		}
	}

	private void RegisterStorage(Vector2I cell)
	{
		if (_storageInventories.ContainsKey(cell))
		{
			foreach (BaseResourceType resourceType in ResourceDefinitionDatabase.AllResourceTypes)
			{
				_storageInventories[cell].TryAdd(resourceType, 0);
			}

			EnsureStoragePolicy(cell);
			return;
		}

		Dictionary<BaseResourceType, int> inventory = new();

		foreach (BaseResourceType resourceType in ResourceDefinitionDatabase.AllResourceTypes)
		{
			inventory[resourceType] = 0;
		}

		_storageInventories[cell] = inventory;
		EnsureStoragePolicy(cell);
	}

	private void UnregisterStorage(Vector2I cell)
	{
		_storageInventories.Remove(cell);
		_storagePolicies.Remove(cell);
		_storageInteractionReservations.Remove(cell);
	}

	private bool IsStorageInventoryEmpty(Vector2I cell)
	{
		Vector2I originCell = TryGetStorageOriginCell(cell, out Vector2I resolvedCell) ? resolvedCell : cell;

		if (!_storageInventories.TryGetValue(originCell, out Dictionary<BaseResourceType, int>? inventory))
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

	private int GetStorageCapacityPerResource(Vector2I storageCell)
	{
		if (!TryGetObjectOriginState(storageCell, out BuildableTileState state))
		{
			return StorageCapacityPerResource;
		}

		return state.ObjectType switch
		{
			TileBuildType.SmallChest => 40,
			TileBuildType.LargeStorage => 220,
			_ => StorageCapacityPerResource
		};
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
			|| type == BaseResourceType.IronOre
			|| type == BaseResourceType.Coal
			|| type == BaseResourceType.IronIngot;
	}

	private static bool IsStoredResource(BaseResourceType type)
	{
		return ResourceDefinitionDatabase.IsStoredResource(type);
	}

	public static IReadOnlyList<BaseResourceType> GetAllResourceTypes()
	{
		return ResourceDefinitionDatabase.AllResourceTypes;
	}

	public static int GetResourceUnitWeight(BaseResourceType type)
	{
		return ResourceDefinitionDatabase.GetUnitWeight(type);
	}

	public static string GetResourceDisplayName(BaseResourceType type)
	{
		return ResourceDefinitionDatabase.GetDisplayName(type);
	}

	public static string GetResourceMarker(BaseResourceType type)
	{
		return ResourceDefinitionDatabase.GetMarker(type);
	}

	private static bool IsFacilityBuildType(TileBuildType buildType)
	{
		return IsBedBuildType(buildType)
			|| IsStorageBuildType(buildType)
			|| buildType == TileBuildType.GuardPost;
	}

	private static bool IsPlaceableObjectBuildType(TileBuildType buildType)
	{
		return IsFacilityBuildType(buildType)
			|| buildType == TileBuildType.SmallCabinet
			|| buildType == TileBuildType.SmallDesk
			|| buildType == TileBuildType.Lamp
			|| buildType == TileBuildType.Chair
			|| buildType == TileBuildType.SmallDiningTable
			|| buildType == TileBuildType.LongDiningTable
			|| buildType == TileBuildType.ServingCounter
			|| buildType == TileBuildType.KitchenCounter
			|| buildType == TileBuildType.Hearth
			|| buildType == TileBuildType.IngredientCrate
			|| buildType == TileBuildType.MaterialShelf
			|| buildType == TileBuildType.WeaponRack
			|| buildType == TileBuildType.MedicineShelf
			|| buildType == TileBuildType.Workbench
			|| buildType == TileBuildType.LargeWorkbench
			|| buildType == TileBuildType.RepairBench
			|| buildType == TileBuildType.Forge
			|| buildType == TileBuildType.AlchemyBench
			|| buildType == TileBuildType.TrainingDummy
			|| buildType == TileBuildType.Sandbag
			|| buildType == TileBuildType.TrainingMat
			|| buildType == TileBuildType.WeaponTrainingRack
			|| buildType == TileBuildType.TrainingRing
			|| buildType == TileBuildType.ImprovisedMedicalBed
			|| buildType == TileBuildType.MedicalBed
			|| buildType == TileBuildType.LuxuryMedicalBed
			|| buildType == TileBuildType.MedicalTable
			|| buildType == TileBuildType.MedicineCabinet
			|| buildType == TileBuildType.PlantPot
			|| buildType == TileBuildType.SmallRug
			|| buildType == TileBuildType.LargeRug
			|| buildType == TileBuildType.WallBanner
			|| buildType == TileBuildType.TrophyDisplay;
	}

	private static bool IsBedBuildType(TileBuildType buildType)
	{
		return buildType == TileBuildType.ImprovisedBed
			|| buildType == TileBuildType.Bed
			|| buildType == TileBuildType.LuxuryBed;
	}

	private static bool IsStorageBuildType(TileBuildType buildType)
	{
		return buildType == TileBuildType.SmallChest
			|| buildType == TileBuildType.Storage
			|| buildType == TileBuildType.LargeStorage
			|| buildType == TileBuildType.IngredientCrate
			|| buildType == TileBuildType.MaterialShelf
			|| buildType == TileBuildType.MedicineShelf;
	}

	private static bool IsTableLikeBuildType(TileBuildType buildType)
	{
		return buildType == TileBuildType.SmallCabinet
			|| buildType == TileBuildType.SmallDesk
			|| buildType == TileBuildType.Chair
			|| buildType == TileBuildType.SmallDiningTable
			|| buildType == TileBuildType.LongDiningTable
			|| buildType == TileBuildType.ServingCounter
			|| buildType == TileBuildType.KitchenCounter
			|| buildType == TileBuildType.IngredientCrate
			|| buildType == TileBuildType.Workbench
			|| buildType == TileBuildType.LargeWorkbench
			|| buildType == TileBuildType.RepairBench
			|| buildType == TileBuildType.AlchemyBench;
	}

	private static bool IsKitchenFireBuildType(TileBuildType buildType)
	{
		return buildType == TileBuildType.Hearth || buildType == TileBuildType.Forge;
	}

	private static bool IsShelfRackBuildType(TileBuildType buildType)
	{
		return buildType == TileBuildType.MaterialShelf
			|| buildType == TileBuildType.WeaponRack
			|| buildType == TileBuildType.MedicineShelf
			|| buildType == TileBuildType.MedicineCabinet;
	}

	private static bool IsTrainingBuildType(TileBuildType buildType)
	{
		return buildType == TileBuildType.TrainingDummy
			|| buildType == TileBuildType.Sandbag
			|| buildType == TileBuildType.TrainingMat
			|| buildType == TileBuildType.WeaponTrainingRack
			|| buildType == TileBuildType.TrainingRing;
	}

	private static bool IsMedicalBuildType(TileBuildType buildType)
	{
		return buildType == TileBuildType.ImprovisedMedicalBed
			|| buildType == TileBuildType.MedicalBed
			|| buildType == TileBuildType.LuxuryMedicalBed
			|| buildType == TileBuildType.MedicalTable;
	}

	private static bool IsDecorBuildType(TileBuildType buildType)
	{
		return buildType == TileBuildType.Lamp
			|| buildType == TileBuildType.PlantPot
			|| buildType == TileBuildType.SmallRug
			|| buildType == TileBuildType.LargeRug
			|| buildType == TileBuildType.WallBanner
			|| buildType == TileBuildType.TrophyDisplay;
	}

	private static bool CanUseFurnitureFor(TileBuildType buildType, FurnitureUseType useType)
	{
		return useType switch
		{
			FurnitureUseType.Sleep => IsBedBuildType(buildType),
			FurnitureUseType.Eat => buildType == TileBuildType.Chair
				|| buildType == TileBuildType.SmallDiningTable
				|| buildType == TileBuildType.LongDiningTable
				|| buildType == TileBuildType.ServingCounter
				|| buildType == TileBuildType.KitchenCounter
				|| buildType == TileBuildType.Hearth
				|| buildType == TileBuildType.IngredientCrate
				|| IsStorageBuildType(buildType),
			FurnitureUseType.Relax => buildType == TileBuildType.Chair
				|| buildType == TileBuildType.SmallDesk
				|| buildType == TileBuildType.SmallDiningTable
				|| buildType == TileBuildType.LongDiningTable
				|| buildType == TileBuildType.SmallRug
				|| buildType == TileBuildType.LargeRug
				|| IsDecorBuildType(buildType),
			_ => false
		};
	}

	private static FacilityType GetFacilityTypeForObject(TileBuildType objectType)
	{
		if (IsBedBuildType(objectType))
		{
			return FacilityType.Bed;
		}

		if (IsStorageBuildType(objectType))
		{
			return FacilityType.Storage;
		}

		return objectType switch
		{
			TileBuildType.GuardPost => FacilityType.GuardPost,
			_ => FacilityType.None
		};
	}

	private FacilityInfo CreateFacilityInfo(BuildableTileState state, FacilityType facilityType)
	{
		// TODO: Add ownership, damage, and richer usability checks when jobs need them.
		return new FacilityInfo(
			state.ObjectOriginCell,
			GetObjectWorldCenter(state.ObjectOriginCell, state.ObjectSize),
			facilityType,
			state.ObjectType,
			true,
			IsFacilityReserved(state.ObjectOriginCell),
			IsFacilityOccupied(state.ObjectOriginCell));
	}

	private void ApplyBuildAt(Vector2I cell, TileBuildType buildType)
	{
		ApplyBuildAt(cell, buildType, BuildMaterialType.Basic);
	}

	private void ApplyBuildAt(Vector2I cell, TileBuildType buildType, BuildMaterialType materialType)
	{
		Vector2I objectSize = GetBuildObjectSize(buildType);
		BuildStructureDefinition definition = GetBuildDefinition(buildType, materialType);
		BuildMaterialType appliedMaterial = BuildStructureDefinitions.IsMaterialSensitiveBuildType(buildType)
			? definition.MaterialType
			: BuildMaterialType.Basic;

		if (objectSize != Vector2I.One && IsPlaceableObjectBuildType(buildType))
		{
			foreach (Vector2I objectCell in GetObjectCells(cell, objectSize))
			{
				bool hasObjectState = _buildings.TryGetValue(objectCell, out BuildableTileState objectState);

				if (!hasObjectState)
				{
					objectState = new BuildableTileState(objectCell, TileBuildType.None, TileBuildType.None);
				}

				ClearFarmZoneAt(objectCell);
				_buildings[objectCell] = objectState.WithObject(
					buildType,
					cell,
					objectSize,
					true,
					appliedMaterial,
					definition.Durability,
					definition.RoomQualityBonus);
			}

			if (IsStorageBuildType(buildType))
			{
				RegisterStorage(cell);
			}

			return;
		}

		bool hasState = _buildings.TryGetValue(cell, out BuildableTileState state);

		if (!hasState)
		{
			state = new BuildableTileState(cell, TileBuildType.None, TileBuildType.None);
		}

		if (buildType == TileBuildType.Floor)
		{
			state = state.WithFloor(TileBuildType.Floor, appliedMaterial, definition.Durability, definition.RoomQualityBonus);
		}
		else if (buildType == TileBuildType.Wall)
		{
			ClearFarmZoneAt(cell);
			state = state.WithObject(
				TileBuildType.Wall,
				cell,
				Vector2I.One,
				true,
				appliedMaterial,
				definition.Durability,
				definition.RoomQualityBonus);
		}
		else if (buildType == TileBuildType.Door)
		{
			ClearFarmZoneAt(cell);
			state = state.WithObject(
				TileBuildType.Door,
				cell,
				Vector2I.One,
				true,
				appliedMaterial,
				definition.Durability,
				definition.RoomQualityBonus);
		}
		else if (IsPlaceableObjectBuildType(buildType))
		{
			ClearFarmZoneAt(cell);
			state = state.WithObject(
				buildType,
				cell,
				objectSize,
				true,
				appliedMaterial,
				definition.Durability,
				definition.RoomQualityBonus);
		}

		_buildings[cell] = state;

		if (IsStorageBuildType(buildType))
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
			return EraseObjectAt(cell, state);
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

	private bool EraseObjectAt(Vector2I cell, BuildableTileState state)
	{
		Vector2I originCell = state.ObjectOriginCell;
		Vector2I objectSize = state.ObjectSize;

		if (state.IsFacility)
		{
			_facilityReservations.Remove(originCell);
			_facilityOccupants.Remove(originCell);
		}

		if (IsStorageBuildType(state.ObjectType))
		{
			if (!IsStorageInventoryEmpty(originCell))
			{
				return false;
			}

			UnregisterStorage(originCell);
		}

		_furnitureReservations.Remove(originCell);

		foreach (Vector2I objectCell in GetObjectCells(originCell, objectSize))
		{
			if (!_buildings.TryGetValue(objectCell, out BuildableTileState objectState)
				|| objectState.ObjectOriginCell != originCell
				|| objectState.ObjectType != state.ObjectType)
			{
				continue;
			}

			objectState = objectState.WithObject(TileBuildType.None);

			if (objectState.IsEmpty())
			{
				_buildings.Remove(objectCell);
			}
			else
			{
				_buildings[objectCell] = objectState;
			}
		}

		ValidateLogisticsWorld("object erase");
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

	public Vector2I GetBuildObjectSize(TileBuildType buildType)
	{
		return BuildDefinitionDatabase.GetSize(buildType);
	}

	public Vector2I GetObjectOriginCell(Vector2I cell)
	{
		return _buildings.TryGetValue(cell, out BuildableTileState state) && state.HasObject
			? state.ObjectOriginCell
			: cell;
	}

	public bool TryGetObjectInfoAtCell(Vector2I cell, out TileBuildType objectType, out Vector2I originCell, out Vector2I objectSize)
	{
		objectType = TileBuildType.None;
		originCell = cell;
		objectSize = Vector2I.One;

		if (!_buildings.TryGetValue(cell, out BuildableTileState state) || !state.HasObject)
		{
			return false;
		}

		objectType = state.ObjectType;
		originCell = state.ObjectOriginCell;
		objectSize = state.ObjectSize;
		return true;
	}

	public bool TryFindObjectAccessCell(Vector2I fromCell, Vector2I objectOriginCell, out Vector2I accessCell, out int pathLength)
	{
		accessCell = default;
		pathLength = int.MaxValue;

		if (!TryGetObjectOriginState(objectOriginCell, out BuildableTileState originState)
			|| originState.ObjectOriginCell != objectOriginCell)
		{
			return false;
		}

		foreach (Vector2I candidateAccessCell in GetObjectAccessCells(objectOriginCell))
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

			int candidatePathLength = fromCell == candidateAccessCell ? 0 : path.Count;

			if (candidatePathLength >= pathLength)
			{
				continue;
			}

			accessCell = candidateAccessCell;
			pathLength = candidatePathLength;
		}

		return pathLength != int.MaxValue;
	}

	private string GetMultiCellObjectBlockReason(Vector2I originCell, TileBuildType buildType)
	{
		foreach (Vector2I objectCell in GetObjectCells(originCell, GetBuildObjectSize(buildType)))
		{
			if (!IsCellInWorld(objectCell))
			{
				return "Out of bounds";
			}

			if (IsResourceNodeCell(objectCell))
			{
				return "Resource node exists";
			}

			if (IsCropPlantCell(objectCell))
			{
				return "Crop exists";
			}

			if (IsResourcePileCell(objectCell))
			{
				return "Resource pile exists";
			}

			if (IsCellOccupied(objectCell))
			{
				return "Occupied by unit";
			}

			if (!_buildings.TryGetValue(objectCell, out BuildableTileState state) || !state.HasFloor)
			{
				return "Need Floor";
			}

			if (state.HasObject)
			{
				return "Object already exists";
			}
		}

		return "";
	}

	private static IEnumerable<Vector2I> GetObjectCells(Vector2I originCell, Vector2I size)
	{
		for (int y = 0; y < size.Y; y++)
		{
			for (int x = 0; x < size.X; x++)
			{
				yield return originCell + new Vector2I(x, y);
			}
		}
	}

	private bool TryGetObjectOriginState(Vector2I cell, out BuildableTileState originState)
	{
		originState = default;

		if (!_buildings.TryGetValue(cell, out BuildableTileState state) || !state.HasObject)
		{
			return false;
		}

		return _buildings.TryGetValue(state.ObjectOriginCell, out originState)
			&& originState.HasObject
			&& originState.ObjectType == state.ObjectType
			&& originState.ObjectOriginCell == state.ObjectOriginCell;
	}

	private bool TryGetStorageOriginCell(Vector2I cell, out Vector2I originCell)
	{
		originCell = default;

		if (TryGetObjectOriginState(cell, out BuildableTileState state)
			&& IsStorageBuildType(state.ObjectType))
		{
			originCell = state.ObjectOriginCell;
			return true;
		}

		if (TryGetStockpileZoneAtCell(cell, out StockpileZone? zone) && zone != null)
		{
			originCell = GetStockpileOriginCell(zone);
			return true;
		}

		return false;
	}

	private bool TryGetFacilityOriginCell(Vector2I cell, out Vector2I originCell)
	{
		originCell = default;

		if (!TryGetObjectOriginState(cell, out BuildableTileState state) || !state.IsFacility)
		{
			return false;
		}

		originCell = state.ObjectOriginCell;
		return true;
	}

	private Vector2I GetNearestObjectAccessCell(Vector2I fromCell, Vector2I objectOriginCell)
	{
		Vector2I nearestCell = objectOriginCell;
		int nearestDistance = int.MaxValue;

		foreach (Vector2I accessCell in GetObjectAccessCells(objectOriginCell))
		{
			if (!IsCellInWorld(accessCell) || IsCellBlocked(accessCell))
			{
				continue;
			}

			int distance = Mathf.Abs(fromCell.X - accessCell.X) + Mathf.Abs(fromCell.Y - accessCell.Y);

			if (distance >= nearestDistance)
			{
				continue;
			}

			nearestDistance = distance;
			nearestCell = accessCell;
		}

		return nearestCell;
	}

	private IEnumerable<Vector2I> GetObjectAccessCells(Vector2I objectOriginCell)
	{
		Vector2I size = _buildings.TryGetValue(objectOriginCell, out BuildableTileState state)
			? state.ObjectSize
			: Vector2I.One;
		HashSet<Vector2I> objectCells = new(GetObjectCells(objectOriginCell, size));
		HashSet<Vector2I> accessCells = new();

		foreach (Vector2I objectCell in objectCells)
		{
			Vector2I right = objectCell + new Vector2I(1, 0);
			Vector2I left = objectCell + new Vector2I(-1, 0);
			Vector2I down = objectCell + new Vector2I(0, 1);
			Vector2I up = objectCell + new Vector2I(0, -1);

			if (!objectCells.Contains(right))
			{
				accessCells.Add(right);
			}

			if (!objectCells.Contains(left))
			{
				accessCells.Add(left);
			}

			if (!objectCells.Contains(down))
			{
				accessCells.Add(down);
			}

			if (!objectCells.Contains(up))
			{
				accessCells.Add(up);
			}
		}

		return accessCells;
	}

	private Vector2 GetObjectWorldCenter(Vector2I originCell, Vector2I size)
	{
		int tileSize = GetTileSize();
		return new Vector2(
			(originCell.X + size.X * 0.5f) * tileSize,
			(originCell.Y + size.Y * 0.5f) * tileSize);
	}

	private static Color GetFloorColor(BuildMaterialType materialType)
	{
		return materialType switch
		{
			BuildMaterialType.Stone => new Color(0.54f, 0.56f, 0.56f, 0.62f),
			BuildMaterialType.Metal => new Color(0.38f, 0.42f, 0.46f, 0.66f),
			_ => new Color(0.72f, 0.60f, 0.40f, 0.58f)
		};
	}

	private static Color GetWallColor(BuildMaterialType materialType)
	{
		return materialType switch
		{
			BuildMaterialType.Stone => new Color(0.46f, 0.46f, 0.48f, 0.95f),
			BuildMaterialType.Metal => new Color(0.24f, 0.28f, 0.32f, 0.96f),
			_ => new Color(0.46f, 0.28f, 0.12f, 0.95f)
		};
	}

	private static Color GetDoorColor(BuildMaterialType materialType)
	{
		return materialType == BuildMaterialType.Metal
			? new Color(0.42f, 0.44f, 0.48f, 0.96f)
			: new Color(0.58f, 0.36f, 0.16f, 0.95f);
	}

	private void DrawBuildTileFloor(BuildableTileState state)
	{
		int tileSize = GetTileSize();
		Vector2 position = new Vector2(state.Cell.X * tileSize, state.Cell.Y * tileSize);
		Rect2 tileRect = new Rect2(position, new Vector2(tileSize, tileSize));

		if (state.HasFloor)
		{
			Color floorColor = GetFloorColor(state.FloorMaterialType);
			DrawRect(tileRect.Grow(-2.0f), floorColor);
			DrawRect(tileRect.Grow(-2.0f), floorColor.Lightened(0.22f), false, 1.0f);
		}
	}

	private void DrawBuildObject(BuildableTileState state)
	{
		if (!state.HasObject || !state.IsObjectOrigin)
		{
			return;
		}

		int tileSize = GetTileSize();
		Vector2 position = new Vector2(state.ObjectOriginCell.X * tileSize, state.ObjectOriginCell.Y * tileSize);
		Rect2 tileRect = new Rect2(position, new Vector2(state.ObjectSize.X * tileSize, state.ObjectSize.Y * tileSize));
		Rect2 innerRect = tileRect.Grow(-4.0f);

		if (state.ObjectType == TileBuildType.Wall)
		{
			Color wallColor = GetWallColor(state.ObjectMaterialType);
			DrawRect(tileRect.Grow(-1.0f), wallColor.Darkened(0.24f));
			DrawRect(tileRect.Grow(-6.0f), wallColor);
		}
		else if (state.ObjectType == TileBuildType.Door)
		{
			Color doorColor = GetDoorColor(state.ObjectMaterialType);
			DrawRect(tileRect.Grow(-1.0f), doorColor.Darkened(0.35f));
			DrawRect(new Rect2(position + new Vector2(5.0f, tileSize * 0.36f), new Vector2(tileSize - 10.0f, tileSize * 0.28f)), doorColor);
			DrawLine(position + new Vector2(8.0f, tileSize * 0.5f), position + new Vector2(tileSize - 8.0f, tileSize * 0.5f), new Color(0.98f, 0.78f, 0.36f), 2.0f);
			DrawCircle(tileRect.GetCenter() + new Vector2(tileSize * 0.2f, -2.0f), 2.0f, new Color(1.0f, 0.86f, 0.4f));
		}
		else if (IsBedBuildType(state.ObjectType))
		{
			Color bedColor = state.ObjectType == TileBuildType.LuxuryBed
				? new Color(0.30f, 0.24f, 0.58f, 0.96f)
				: state.ObjectType == TileBuildType.ImprovisedBed
					? new Color(0.28f, 0.36f, 0.46f, 0.9f)
					: new Color(0.20f, 0.36f, 0.62f, 0.95f);
			DrawRect(innerRect, bedColor);
			DrawRect(innerRect, new Color(0.07f, 0.12f, 0.22f, 0.95f), false, 2.0f);

			Rect2 pillowRect = new Rect2(
				innerRect.Position + new Vector2(5.0f, 5.0f),
				new Vector2(innerRect.Size.X - 10.0f, Mathf.Min(16.0f, innerRect.Size.Y * 0.25f)));
			DrawRect(pillowRect, new Color(0.86f, 0.90f, 0.96f, 0.96f));
			DrawRect(pillowRect, new Color(0.36f, 0.45f, 0.58f, 0.9f), false, 1.0f);

			Rect2 blanketRect = new Rect2(
				innerRect.Position + new Vector2(5.0f, pillowRect.Size.Y + 10.0f),
				new Vector2(innerRect.Size.X - 10.0f, Mathf.Max(6.0f, innerRect.Size.Y - pillowRect.Size.Y - 15.0f)));
			DrawRect(blanketRect, new Color(0.12f, 0.26f, 0.52f, 0.86f));

			if (state.ObjectType == TileBuildType.LuxuryBed)
			{
				DrawRect(innerRect.Grow(-4.0f), new Color(0.92f, 0.78f, 0.38f, 0.82f), false, 1.5f);
			}
		}
		else if (IsStorageBuildType(state.ObjectType))
		{
			Color woodColor = state.ObjectType == TileBuildType.LargeStorage
				? new Color(0.50f, 0.30f, 0.12f, 0.96f)
				: state.ObjectType == TileBuildType.SmallChest
					? new Color(0.66f, 0.42f, 0.18f, 0.94f)
					: new Color(0.58f, 0.35f, 0.15f, 0.94f);
			Color darkWoodColor = new Color(0.23f, 0.12f, 0.04f, 0.95f);

			DrawRect(innerRect, woodColor);
			DrawRect(innerRect, darkWoodColor, false, 2.0f);

			float plankStep = innerRect.Size.Y / 4.0f;

			for (int i = 1; i < 4; i++)
			{
				float y = innerRect.Position.Y + plankStep * i;
				DrawLine(new Vector2(innerRect.Position.X + 4.0f, y), new Vector2(innerRect.End.X - 4.0f, y), new Color(0.35f, 0.20f, 0.08f, 0.75f), 1.5f);
			}

			DrawLine(innerRect.Position + new Vector2(8.0f, 8.0f), innerRect.End - new Vector2(8.0f, 8.0f), darkWoodColor, 2.0f);
			DrawLine(new Vector2(innerRect.End.X - 8.0f, innerRect.Position.Y + 8.0f), new Vector2(innerRect.Position.X + 8.0f, innerRect.End.Y - 8.0f), darkWoodColor, 2.0f);
		}
		else if (IsTableLikeBuildType(state.ObjectType))
		{
			DrawRect(innerRect, new Color(0.48f, 0.30f, 0.13f, 0.92f));
			DrawRect(innerRect, new Color(0.20f, 0.11f, 0.04f, 0.88f), false, 2.0f);
			DrawLine(innerRect.Position + new Vector2(4.0f, innerRect.Size.Y * 0.5f), new Vector2(innerRect.End.X - 4.0f, innerRect.Position.Y + innerRect.Size.Y * 0.5f), new Color(0.30f, 0.18f, 0.08f, 0.8f), 1.5f);
		}
		else if (IsKitchenFireBuildType(state.ObjectType))
		{
			DrawRect(innerRect, new Color(0.30f, 0.30f, 0.30f, 0.94f));
			DrawRect(innerRect, new Color(0.10f, 0.10f, 0.10f, 0.94f), false, 2.0f);
			DrawCircle(innerRect.GetCenter(), Mathf.Min(innerRect.Size.X, innerRect.Size.Y) * 0.22f, new Color(0.96f, 0.32f, 0.08f, 0.88f));
			DrawCircle(innerRect.GetCenter() + new Vector2(0.0f, -3.0f), Mathf.Min(innerRect.Size.X, innerRect.Size.Y) * 0.12f, new Color(1.0f, 0.76f, 0.18f, 0.86f));
		}
		else if (IsShelfRackBuildType(state.ObjectType))
		{
			DrawRect(innerRect, new Color(0.42f, 0.28f, 0.13f, 0.86f));
			DrawRect(innerRect, new Color(0.16f, 0.10f, 0.04f, 0.9f), false, 2.0f);
			DrawLine(new Vector2(innerRect.Position.X + 4.0f, innerRect.Position.Y + innerRect.Size.Y * 0.35f), new Vector2(innerRect.End.X - 4.0f, innerRect.Position.Y + innerRect.Size.Y * 0.35f), new Color(0.82f, 0.68f, 0.42f, 0.7f), 1.5f);
			DrawLine(new Vector2(innerRect.Position.X + 4.0f, innerRect.Position.Y + innerRect.Size.Y * 0.68f), new Vector2(innerRect.End.X - 4.0f, innerRect.Position.Y + innerRect.Size.Y * 0.68f), new Color(0.82f, 0.68f, 0.42f, 0.7f), 1.5f);
		}
		else if (IsTrainingBuildType(state.ObjectType))
		{
			DrawRect(innerRect, new Color(0.34f, 0.30f, 0.22f, 0.82f));
			DrawRect(innerRect, new Color(0.12f, 0.10f, 0.08f, 0.88f), false, 2.0f);
			DrawCircle(innerRect.GetCenter(), Mathf.Min(innerRect.Size.X, innerRect.Size.Y) * 0.22f, new Color(0.74f, 0.56f, 0.30f, 0.9f));
			DrawLine(innerRect.GetCenter() + new Vector2(-8.0f, 0.0f), innerRect.GetCenter() + new Vector2(8.0f, 0.0f), new Color(0.96f, 0.86f, 0.62f, 0.9f), 2.0f);
		}
		else if (IsMedicalBuildType(state.ObjectType))
		{
			DrawRect(innerRect, new Color(0.78f, 0.84f, 0.82f, 0.92f));
			DrawRect(innerRect, new Color(0.26f, 0.38f, 0.42f, 0.9f), false, 2.0f);
			Vector2 center = innerRect.GetCenter();
			DrawLine(center + new Vector2(-7.0f, 0.0f), center + new Vector2(7.0f, 0.0f), new Color(0.82f, 0.10f, 0.12f, 0.9f), 2.5f);
			DrawLine(center + new Vector2(0.0f, -7.0f), center + new Vector2(0.0f, 7.0f), new Color(0.82f, 0.10f, 0.12f, 0.9f), 2.5f);
		}
		else if (IsDecorBuildType(state.ObjectType))
		{
			Color decorColor = state.ObjectType == TileBuildType.PlantPot
				? new Color(0.18f, 0.58f, 0.26f, 0.86f)
				: new Color(0.62f, 0.30f, 0.42f, 0.82f);
			DrawRect(innerRect, decorColor);
			DrawRect(innerRect, new Color(0.16f, 0.10f, 0.12f, 0.84f), false, 1.5f);
			DrawCircle(innerRect.GetCenter(), Mathf.Min(innerRect.Size.X, innerRect.Size.Y) * 0.18f, decorColor.Lightened(0.25f));
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

		if (IsFurnitureReserved(state.ObjectOriginCell))
		{
			Rect2 highlightRect = tileRect.Grow(-2.0f);
			DrawRect(highlightRect, new Color(1.0f, 0.86f, 0.24f, 0.16f));
			DrawRect(highlightRect, new Color(1.0f, 0.86f, 0.24f, 0.86f), false, 2.5f);
		}
	}

	private void DrawBuildPreview()
	{
		if (CurrentBuildMode == TileBuildType.None || !_hoverCell.HasValue || !_hoverCellInWorld)
		{
			return;
		}

		int tileSize = GetTileSize();
		Color validColor = new Color(0.34f, 0.9f, 0.42f, 0.32f);
		Color invalidColor = new Color(1.0f, 0.18f, 0.12f, 0.32f);
		bool canPreviewBuild = _canPlaceOnHoverCell && CanAffordBuild(CurrentBuildMode);
		Color outlineColor = canPreviewBuild
			? new Color(0.52f, 1.0f, 0.56f, 0.9f)
			: new Color(1.0f, 0.2f, 0.16f, 0.95f);

		if (CurrentBuildMode == TileBuildType.Erase)
		{
			Vector2 position = new Vector2(_hoverCell.Value.X * tileSize, _hoverCell.Value.Y * tileSize);
			Rect2 tileRect = new Rect2(position, new Vector2(tileSize, tileSize));
			DrawRect(tileRect.Grow(-1.0f), _canPlaceOnHoverCell ? new Color(1.0f, 0.78f, 0.18f, 0.32f) : invalidColor);
			DrawRect(tileRect.Grow(-1.0f), outlineColor, false, 2.0f);
			DrawLine(tileRect.Position + new Vector2(6.0f, 6.0f), tileRect.End - new Vector2(6.0f, 6.0f), outlineColor, 2.0f);
			DrawLine(new Vector2(tileRect.End.X - 6.0f, tileRect.Position.Y + 6.0f), new Vector2(tileRect.Position.X + 6.0f, tileRect.End.Y - 6.0f), outlineColor, 2.0f);
			return;
		}

		foreach (Vector2I previewCell in GetObjectCells(_hoverCell.Value, GetBuildObjectSize(CurrentBuildMode)))
		{
			Vector2 position = new Vector2(previewCell.X * tileSize, previewCell.Y * tileSize);
			Rect2 tileRect = new Rect2(position, new Vector2(tileSize, tileSize));

			DrawRect(tileRect.Grow(-2.0f), canPreviewBuild ? validColor : invalidColor);
			DrawPreviewShape(tileRect, CurrentBuildMode, canPreviewBuild);
			DrawRect(tileRect.Grow(-1.0f), outlineColor, false, 2.0f);

			if (!canPreviewBuild)
			{
				DrawLine(tileRect.Position + new Vector2(5.0f, 5.0f), tileRect.End - new Vector2(5.0f, 5.0f), outlineColor, 2.5f);
				DrawLine(new Vector2(tileRect.End.X - 5.0f, tileRect.Position.Y + 5.0f), new Vector2(tileRect.Position.X + 5.0f, tileRect.End.Y - 5.0f), outlineColor, 2.5f);
			}
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
