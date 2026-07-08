using System.Collections.Generic;
using Godot;

public partial class TestResourceNodeSpawner : Node2D
{
    [Export]
    public int Seed { get; set; } = 12345;

    [Export]
    public bool UseRandomSeed { get; set; } = false;

    [Export]
    public bool UseFixedTestNodes { get; set; } = false;

    [Export]
    public bool DisableWhenProceduralWorldGeneratorPresent { get; set; } = true;

    [Export]
    public bool DebugResourceGeneration { get; set; } = true;

    [Export]
    public bool SpawnFallbackNodeIfEmpty { get; set; } = true;

    [Export]
    public bool SpawnDebugNodeNearStart { get; set; } = true;

    [Export]
    public Vector2I DebugNodeCell { get; set; } = new(16, 8);

    [Export]
    public int WorldWidth { get; set; } = 64;

    [Export]
    public int WorldHeight { get; set; } = 64;

    [Export]
    public int MapMargin { get; set; } = 3;

    [Export]
    public Vector2I SafeCenterCell { get; set; } = new(8, 8);

    [Export]
    public int SafeRadius { get; set; } = 6;

    [Export]
    public int WoodClusterCount { get; set; } = 5;

    [Export]
    public int WoodNodesPerClusterMin { get; set; } = 3;

    [Export]
    public int WoodNodesPerClusterMax { get; set; } = 7;

    [Export]
    public int WoodClusterRadius { get; set; } = 4;

    [Export]
    public int WoodAmountMin { get; set; } = 35;

    [Export]
    public int WoodAmountMax { get; set; } = 70;

    [Export]
    public int StoneClusterCount { get; set; } = 4;

    [Export]
    public int StoneNodesPerClusterMin { get; set; } = 2;

    [Export]
    public int StoneNodesPerClusterMax { get; set; } = 5;

    [Export]
    public int StoneClusterRadius { get; set; } = 3;

    [Export]
    public int StoneAmountMin { get; set; } = 25;

    [Export]
    public int StoneAmountMax { get; set; } = 55;

    [Export]
    public int MetalClusterCount { get; set; } = 0;

    [Export]
    public int MetalNodesPerClusterMin { get; set; } = 2;

    [Export]
    public int MetalNodesPerClusterMax { get; set; } = 4;

    [Export]
    public int MetalClusterRadius { get; set; } = 2;

    [Export]
    public int MetalAmountMin { get; set; } = 15;

    [Export]
    public int MetalAmountMax { get; set; } = 35;

    [Export]
    public int IronOreClusterCount { get; set; } = 2;

    [Export]
    public int IronOreNodesPerClusterMin { get; set; } = 2;

    [Export]
    public int IronOreNodesPerClusterMax { get; set; } = 4;

    [Export]
    public int IronOreClusterRadius { get; set; } = 2;

    [Export]
    public int IronOreAmountMin { get; set; } = 15;

    [Export]
    public int IronOreAmountMax { get; set; } = 35;

    [Export]
    public int CoalClusterCount { get; set; } = 2;

    [Export]
    public int CoalNodesPerClusterMin { get; set; } = 2;

    [Export]
    public int CoalNodesPerClusterMax { get; set; } = 4;

    [Export]
    public int CoalClusterRadius { get; set; } = 2;

    [Export]
    public int CoalAmountMin { get; set; } = 15;

    [Export]
    public int CoalAmountMax { get; set; } = 35;

    [Export]
    public int HerbClusterCount { get; set; } = 2;

    [Export]
    public int HerbNodesPerClusterMin { get; set; } = 2;

    [Export]
    public int HerbNodesPerClusterMax { get; set; } = 4;

    [Export]
    public int HerbClusterRadius { get; set; } = 3;

    [Export]
    public int HerbAmountMin { get; set; } = 8;

    [Export]
    public int HerbAmountMax { get; set; } = 18;

    [Export]
    public Vector2I DebugHerbNodeCell { get; set; } = new(15, 9);

    private readonly ResourceNodeSpawnData[] _spawnData =
    {
        new(BaseResourceType.Wood, new Vector2I(10, 10), 50),
        new(BaseResourceType.Wood, new Vector2I(12, 10), 50),
        new(BaseResourceType.Wood, new Vector2I(14, 11), 50),
        new(BaseResourceType.Stone, new Vector2I(18, 12), 40),
        new(BaseResourceType.Stone, new Vector2I(20, 13), 40),
        new(BaseResourceType.IronOre, new Vector2I(26, 16), 25),
        new(BaseResourceType.Coal, new Vector2I(28, 16), 25),
        new(BaseResourceType.Herb, new Vector2I(15, 9), 12)
    };

    private readonly HashSet<Vector2I> _occupiedResourceCells = new();
    private readonly RandomNumberGenerator _random = new();
    private BaseBuildManager? _baseBuildManager;
    private WorldGridRenderer? _worldGrid;

    public override void _Ready()
    {
        if (ShouldSkipForProceduralWorldGenerator())
        {
            if (DebugResourceGeneration)
            {
                GD.Print("Resource node spawner skipped: ProceduralWorldGenerator handles active sector resources.");
            }

            return;
        }

        _baseBuildManager = GetNodeOrNull<BaseBuildManager>("../BuildingLayer");
        _worldGrid = GetNodeOrNull<WorldGridRenderer>("../TerrainLayer");
        SyncWorldSettingsFromGrid();
        ZIndex = 2;
        ConfigureRandom();
        _occupiedResourceCells.Clear();

        if (UseFixedTestNodes)
        {
            SpawnTestResourceNodes();
            return;
        }

        SpawnClusteredResourceNodes();
    }

    private bool ShouldSkipForProceduralWorldGenerator()
    {
        if (!DisableWhenProceduralWorldGeneratorPresent)
        {
            return false;
        }

        ProceduralWorldGenerator? generator = GetNodeOrNull<ProceduralWorldGenerator>("../ProceduralWorldGenerator")
            ?? GetTree().CurrentScene?.GetNodeOrNull<ProceduralWorldGenerator>("ProceduralWorldGenerator");
        return generator?.HandlesActiveSectorResourceSpawning == true;
    }

    private void SpawnTestResourceNodes()
    {
        int woodCount = 0;
        int stoneCount = 0;
        int metalCount = 0;
        int ironOreCount = 0;
        int coalCount = 0;
        int herbCount = 0;

        foreach (ResourceNodeSpawnData spawnData in _spawnData)
        {
            if (!CanSpawnAt(spawnData.Cell, 0))
            {
                continue;
            }

            SpawnResourceNode(spawnData.ResourceType, spawnData.Cell, spawnData.Amount);
            AddToCount(spawnData.ResourceType, ref woodCount, ref stoneCount, ref metalCount, ref ironOreCount, ref coalCount, ref herbCount);
        }

        if (SpawnDebugNodeNearStart)
        {
            TrySpawnDebugNodeNearStart(ref woodCount);
            TrySpawnDebugHerbNearStart(ref herbCount);
        }

        if (SpawnFallbackNodeIfEmpty)
        {
            TrySpawnFallbackNodeIfEmpty(ref woodCount);
        }

        PrintGenerationSummary(woodCount, stoneCount, metalCount, ironOreCount, coalCount, herbCount);
    }

    private void SpawnClusteredResourceNodes()
    {
        int woodCount = SpawnClusters(GetWoodRule());
        int stoneCount = SpawnClusters(GetStoneRule());
        int metalCount = SpawnClusters(GetMetalRule());
        int ironOreCount = SpawnClusters(GetIronOreRule());
        int coalCount = SpawnClusters(GetCoalRule());
        int herbCount = SpawnClusters(GetHerbRule());

        if (SpawnDebugNodeNearStart)
        {
            TrySpawnDebugNodeNearStart(ref woodCount);
            TrySpawnDebugHerbNearStart(ref herbCount);
        }

        if (SpawnFallbackNodeIfEmpty)
        {
            TrySpawnFallbackNodeIfEmpty(ref woodCount);
        }

        PrintGenerationSummary(woodCount, stoneCount, metalCount, ironOreCount, coalCount, herbCount);
    }

    private int SpawnClusters(ResourceClusterRule rule)
    {
        int spawnedCount = 0;
        int safeRadius = GetSafeRadiusFor(rule.ResourceType);

        for (int clusterIndex = 0; clusterIndex < rule.ClusterCount; clusterIndex++)
        {
            if (!TryPickClusterCenter(safeRadius, out Vector2I centerCell))
            {
                continue;
            }

            int targetNodeCount = _random.RandiRange(
                Mathf.Min(rule.NodesPerClusterMin, rule.NodesPerClusterMax),
                Mathf.Max(rule.NodesPerClusterMin, rule.NodesPerClusterMax));

            for (int nodeIndex = 0; nodeIndex < targetNodeCount; nodeIndex++)
            {
                if (!TryPickNodeCell(centerCell, rule.ClusterRadius, safeRadius, out Vector2I nodeCell))
                {
                    continue;
                }

                int amount = _random.RandiRange(
                    Mathf.Min(rule.AmountMin, rule.AmountMax),
                    Mathf.Max(rule.AmountMin, rule.AmountMax));

                SpawnResourceNode(rule.ResourceType, nodeCell, amount);
                spawnedCount++;
            }
        }

        return spawnedCount;
    }

    private bool TryPickClusterCenter(int safeRadius, out Vector2I centerCell)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Vector2I candidate = new(
                _random.RandiRange(MapMargin, WorldWidth - MapMargin - 1),
                _random.RandiRange(MapMargin, WorldHeight - MapMargin - 1));

            if (CanSpawnAt(candidate, safeRadius))
            {
                centerCell = candidate;
                return true;
            }
        }

        centerCell = default;
        return false;
    }

    private bool TryPickNodeCell(Vector2I centerCell, int clusterRadius, int safeRadius, out Vector2I nodeCell)
    {
        int radius = Mathf.Max(0, clusterRadius);

        for (int attempt = 0; attempt < 50; attempt++)
        {
            Vector2I offset = new(
                _random.RandiRange(-radius, radius),
                _random.RandiRange(-radius, radius));

            if (Mathf.Abs(offset.X) + Mathf.Abs(offset.Y) > radius)
            {
                continue;
            }

            Vector2I candidate = centerCell + offset;

            if (CanSpawnAt(candidate, safeRadius))
            {
                nodeCell = candidate;
                return true;
            }
        }

        nodeCell = default;
        return false;
    }

    private void SpawnResourceNode(BaseResourceType resourceType, Vector2I cell, int amount)
    {
        _occupiedResourceCells.Add(cell);
        Vector2 worldPosition = GetWorldPosition(cell);

        ResourceNode resourceNode = new()
        {
            Name = $"{resourceType}Node_{cell.X}_{cell.Y}",
            Position = worldPosition
        };

        AddChild(resourceNode);
        resourceNode.Initialize(resourceType, cell, amount);

        if (DebugResourceGeneration)
        {
            GD.Print($"ResourceNode: {resourceType} cell={cell} pos={worldPosition} amount={amount}");
        }
    }

    private bool CanSpawnAt(Vector2I cell, int safeRadius)
    {
        return IsInSpawnBounds(cell)
            && IsOutsideSafeZone(cell, safeRadius)
            && !_occupiedResourceCells.Contains(cell)
            && IsCellInsideWorld(cell);
    }

    private bool IsInSpawnBounds(Vector2I cell)
    {
        int margin = Mathf.Max(0, MapMargin);
        return cell.X >= margin
            && cell.Y >= margin
            && cell.X < WorldWidth - margin
            && cell.Y < WorldHeight - margin;
    }

    private bool IsOutsideSafeZone(Vector2I cell, int safeRadius)
    {
        int radius = Mathf.Max(0, safeRadius);
        int manhattanDistance = Mathf.Abs(cell.X - SafeCenterCell.X) + Mathf.Abs(cell.Y - SafeCenterCell.Y);
        return manhattanDistance > radius;
    }

    private int GetSafeRadiusFor(BaseResourceType resourceType)
    {
        return resourceType == BaseResourceType.Metal
            || resourceType == BaseResourceType.IronOre
            || resourceType == BaseResourceType.Coal
            ? SafeRadius + 4
            : SafeRadius;
    }

    private void ConfigureRandom()
    {
        if (UseRandomSeed)
        {
            _random.Randomize();
            return;
        }

        _random.Seed = unchecked((ulong)Seed);
    }

    private void SyncWorldSettingsFromGrid()
    {
        if (_worldGrid == null)
        {
            GD.Print("WARNING: Resource node spawner could not find TerrainLayer WorldGridRenderer. Using exported world size and 32px fallback positions.");
            return;
        }

        WorldWidth = _worldGrid.WorldWidth;
        WorldHeight = _worldGrid.WorldHeight;
    }

    private bool IsCellInsideWorld(Vector2I cell)
    {
        if (_worldGrid == null)
        {
            return cell.X >= 0 && cell.Y >= 0 && cell.X < WorldWidth && cell.Y < WorldHeight;
        }

        return cell.X >= 0 && cell.Y >= 0 && cell.X < _worldGrid.WorldWidth && cell.Y < _worldGrid.WorldHeight;
    }

    private ResourceClusterRule GetWoodRule()
    {
        return new ResourceClusterRule(
            BaseResourceType.Wood,
            WoodClusterCount,
            WoodNodesPerClusterMin,
            WoodNodesPerClusterMax,
            WoodClusterRadius,
            WoodAmountMin,
            WoodAmountMax);
    }

    private ResourceClusterRule GetStoneRule()
    {
        return new ResourceClusterRule(
            BaseResourceType.Stone,
            StoneClusterCount,
            StoneNodesPerClusterMin,
            StoneNodesPerClusterMax,
            StoneClusterRadius,
            StoneAmountMin,
            StoneAmountMax);
    }

    private ResourceClusterRule GetMetalRule()
    {
        return new ResourceClusterRule(
            BaseResourceType.Metal,
            MetalClusterCount,
            MetalNodesPerClusterMin,
            MetalNodesPerClusterMax,
            MetalClusterRadius,
            MetalAmountMin,
            MetalAmountMax);
    }

    private ResourceClusterRule GetIronOreRule()
    {
        return new ResourceClusterRule(
            BaseResourceType.IronOre,
            IronOreClusterCount,
            IronOreNodesPerClusterMin,
            IronOreNodesPerClusterMax,
            IronOreClusterRadius,
            IronOreAmountMin,
            IronOreAmountMax);
    }

    private ResourceClusterRule GetCoalRule()
    {
        return new ResourceClusterRule(
            BaseResourceType.Coal,
            CoalClusterCount,
            CoalNodesPerClusterMin,
            CoalNodesPerClusterMax,
            CoalClusterRadius,
            CoalAmountMin,
            CoalAmountMax);
    }

    private ResourceClusterRule GetHerbRule()
    {
        return new ResourceClusterRule(
            BaseResourceType.Herb,
            HerbClusterCount,
            HerbNodesPerClusterMin,
            HerbNodesPerClusterMax,
            HerbClusterRadius,
            HerbAmountMin,
            HerbAmountMax);
    }

    private void PrintGenerationSummary(int woodCount, int stoneCount, int metalCount, int ironOreCount, int coalCount, int herbCount)
    {
        int totalCount = woodCount + stoneCount + metalCount + ironOreCount + coalCount + herbCount;
        string seedLabel = UseRandomSeed ? "random" : Seed.ToString();
        GD.Print($"Resource node generation complete. Seed={seedLabel}, UseRandomSeed={UseRandomSeed}, UseFixedTestNodes={UseFixedTestNodes}, Wood={woodCount}, Stone={stoneCount}, Metal={metalCount}, IronOre={ironOreCount}, Coal={coalCount}, Herb={herbCount}, Total={totalCount}");

        if (totalCount == 0)
        {
            GD.Print("WARNING: No resource nodes generated. Check generation bounds, safe zone, cluster rules.");
        }

        GD.Print($"ResourceNodeLayer child count: {GetChildCount()}");
        CallDeferred(MethodName.PrintDeferredGroupCount);
    }

    private void TrySpawnDebugNodeNearStart(ref int woodCount)
    {
        if (!CanSpawnAt(DebugNodeCell, SafeRadius))
        {
            return;
        }

        SpawnResourceNode(BaseResourceType.Wood, DebugNodeCell, 50);
        woodCount++;
    }

    private void TrySpawnDebugHerbNearStart(ref int herbCount)
    {
        if (!CanSpawnAt(DebugHerbNodeCell, SafeRadius))
        {
            return;
        }

        SpawnResourceNode(BaseResourceType.Herb, DebugHerbNodeCell, 12);
        herbCount++;
    }

    private void TrySpawnFallbackNodeIfEmpty(ref int woodCount)
    {
        if (GetChildCount() > 0)
        {
            return;
        }

        Vector2I fallbackCell = SafeCenterCell + new Vector2I(8, 0);

        if (!CanSpawnAt(fallbackCell, 0))
        {
            fallbackCell = new Vector2I(
                Mathf.Clamp(SafeCenterCell.X + 8, MapMargin, WorldWidth - MapMargin - 1),
                Mathf.Clamp(SafeCenterCell.Y, MapMargin, WorldHeight - MapMargin - 1));
        }

        if (!CanSpawnAt(fallbackCell, 0))
        {
            GD.Print("WARNING: Resource fallback node could not be spawned.");
            return;
        }

        SpawnResourceNode(BaseResourceType.Wood, fallbackCell, 50);
        woodCount++;
    }

    private void PrintDeferredGroupCount()
    {
        GD.Print($"resource_nodes group count: {GetTree().GetNodesInGroup("resource_nodes").Count}");
    }

    private static void AddToCount(BaseResourceType resourceType, ref int woodCount, ref int stoneCount, ref int metalCount, ref int ironOreCount, ref int coalCount, ref int herbCount)
    {
        if (resourceType == BaseResourceType.Wood)
        {
            woodCount++;
        }
        else if (resourceType == BaseResourceType.Stone)
        {
            stoneCount++;
        }
        else if (resourceType == BaseResourceType.Metal)
        {
            metalCount++;
        }
        else if (resourceType == BaseResourceType.IronOre)
        {
            ironOreCount++;
        }
        else if (resourceType == BaseResourceType.Coal)
        {
            coalCount++;
        }
        else if (resourceType == BaseResourceType.Herb)
        {
            herbCount++;
        }
    }

    private Vector2 GetWorldPosition(Vector2I cell)
    {
        if (_baseBuildManager != null)
        {
            return _baseBuildManager.CellToWorldCenter(cell);
        }

        int tileSize = _worldGrid?.TileSize ?? 32;
        return new Vector2((cell.X + 0.5f) * tileSize, (cell.Y + 0.5f) * tileSize);
    }

    private readonly struct ResourceNodeSpawnData
    {
        public ResourceNodeSpawnData(BaseResourceType resourceType, Vector2I cell, int amount)
        {
            ResourceType = resourceType;
            Cell = cell;
            Amount = amount;
        }

        public BaseResourceType ResourceType { get; }
        public Vector2I Cell { get; }
        public int Amount { get; }
    }

    private readonly struct ResourceClusterRule
    {
        public ResourceClusterRule(
            BaseResourceType resourceType,
            int clusterCount,
            int nodesPerClusterMin,
            int nodesPerClusterMax,
            int clusterRadius,
            int amountMin,
            int amountMax)
        {
            ResourceType = resourceType;
            ClusterCount = clusterCount;
            NodesPerClusterMin = nodesPerClusterMin;
            NodesPerClusterMax = nodesPerClusterMax;
            ClusterRadius = clusterRadius;
            AmountMin = amountMin;
            AmountMax = amountMax;
        }

        public BaseResourceType ResourceType { get; }
        public int ClusterCount { get; }
        public int NodesPerClusterMin { get; }
        public int NodesPerClusterMax { get; }
        public int ClusterRadius { get; }
        public int AmountMin { get; }
        public int AmountMax { get; }
    }
}
