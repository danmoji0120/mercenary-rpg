using System.Collections.Generic;
using Godot;

public partial class ProceduralWorldGenerator : Node
{
    public const int ChunkSizeCells = 32;
    public const int SectorSizeChunks = 16;
    public const int SectorSizeCells = ChunkSizeCells * SectorSizeChunks;

    [Export]
    public string WorldId { get; set; } = "local_world";

    [Export]
    public int WorldSeed { get; set; } = 20260707;

    [Export]
    public bool UseRandomSeed { get; set; } = false;

    [Export]
    public Vector2I ActiveSectorCoord { get; set; } = Vector2I.Zero;

    [Export]
    public bool GenerateActiveSectorOnReady { get; set; } = false;

    [Export]
    public bool DebugGeneration { get; set; } = true;

    public bool HandlesActiveSectorResourceSpawning => true;
    public SectorMetadata? ActiveSectorMetadata { get; private set; }

    private readonly Dictionary<Vector2I, SectorMetadata> _sectorMetadataByCoord = new();
    private WorldGridRenderer? _worldGrid;
    private Node2D? _resourceNodeLayer;
    private BaseBuildManager? _baseBuildManager;

    public override void _Ready()
    {
        ResolveSceneReferences();

        if (GenerateActiveSectorOnReady)
        {
            Initialize(WorldSeed);
        }
    }

    public void Initialize(int worldSeed)
    {
        if (UseRandomSeed)
        {
            RandomNumberGenerator random = new();
            random.Randomize();
            WorldSeed = (int)(random.Randi() & 0x7fffffff);
        }
        else
        {
            WorldSeed = worldSeed;
        }

        LoadActiveSector(ActiveSectorCoord);
    }

    public SectorMetadata GetOrCreateSectorMetadata(Vector2I sectorCoord)
    {
        if (_sectorMetadataByCoord.TryGetValue(sectorCoord, out SectorMetadata? metadata))
        {
            return metadata;
        }

        int sectorSeed = MakeSectorSeed(WorldSeed, sectorCoord);
        bool isCentralTown = sectorCoord == Vector2I.Zero;
        float distanceFromCenter = Mathf.Abs(sectorCoord.X) + Mathf.Abs(sectorCoord.Y);
        float dangerLevel = isCentralTown
            ? 0.0f
            : Mathf.Clamp(distanceFromCenter * 0.14f + StableUnitFloat(sectorSeed, 11) * 0.20f, 0.05f, 1.0f);
        float resourceRichness = isCentralTown
            ? 0.0f
            : Mathf.Clamp(0.25f + dangerLevel * 0.48f + StableUnitFloat(sectorSeed, 17) * 0.22f, 0.1f, 1.0f);

        metadata = new SectorMetadata
        {
            WorldId = WorldId,
            OwnerId = string.Empty,
            WorldSeed = WorldSeed,
            SectorCoord = sectorCoord,
            SectorSeed = sectorSeed,
            Type = isCentralTown ? SectorType.CentralTown : PickSectorType(sectorSeed, distanceFromCenter),
            IsGenerated = false,
            IsDiscovered = isCentralTown,
            IsVisited = isCentralTown,
            DangerLevel = dangerLevel,
            ResourceRichness = resourceRichness,
            IsCentralTownSector = isCentralTown,
            IsBuildRestricted = isCentralTown
        };

        _sectorMetadataByCoord[sectorCoord] = metadata;
        return metadata;
    }

    public SectorGenerationData GenerateSector(Vector2I sectorCoord)
    {
        ResolveSceneReferences();
        SectorMetadata metadata = GetOrCreateSectorMetadata(sectorCoord);
        int displayWidth = Mathf.Max(1, _worldGrid?.WorldWidth ?? 128);
        int displayHeight = Mathf.Max(1, _worldGrid?.WorldHeight ?? 128);
        SectorGenerationData sectorData = new(metadata, displayWidth, displayHeight);
        int chunkColumns = Mathf.CeilToInt(displayWidth / (float)ChunkSizeCells);
        int chunkRows = Mathf.CeilToInt(displayHeight / (float)ChunkSizeCells);

        for (int chunkY = 0; chunkY < chunkRows; chunkY++)
        {
            for (int chunkX = 0; chunkX < chunkColumns; chunkX++)
            {
                WorldChunkData chunkData = GenerateChunk(sectorCoord, new Vector2I(chunkX, chunkY));
                MergeChunkIntoSector(sectorData, chunkData);
            }
        }

        metadata.IsGenerated = true;
        return sectorData;
    }

    public WorldChunkData GenerateChunk(Vector2I sectorCoord, Vector2I chunkCoord)
    {
        SectorMetadata metadata = GetOrCreateSectorMetadata(sectorCoord);
        int chunkSeed = MakeChunkSeed(metadata.SectorSeed, chunkCoord);
        WorldChunkData chunkData = new(WorldId, sectorCoord, chunkCoord, chunkSeed);
        Vector2I chunkOrigin = chunkCoord * ChunkSizeCells;

        for (int y = 0; y < ChunkSizeCells; y++)
        {
            for (int x = 0; x < ChunkSizeCells; x++)
            {
                Vector2I cell = chunkOrigin + new Vector2I(x, y);
                chunkData.TerrainColors[cell] = GetTerrainColor(metadata, cell);

                if (metadata.IsBuildRestricted)
                {
                    chunkData.BuildRestrictedCells.Add(cell);
                }
            }
        }

        AddChunkResourceNodes(metadata, chunkData);
        return chunkData;
    }

    public SectorGenerationData LoadActiveSector(Vector2I sectorCoord)
    {
        SectorGenerationData sectorData = GenerateSector(sectorCoord);
        ActiveSectorCoord = sectorCoord;
        ActiveSectorMetadata = sectorData.Metadata;
        ActiveSectorMetadata.IsVisited = true;
        ActiveSectorMetadata.IsDiscovered = true;

        ApplySectorData(sectorData);
        CreateNeighborMetadata(sectorCoord);

        if (DebugGeneration)
        {
            PrintActiveSectorSummary();
        }

        return sectorData;
    }

    public void PrintActiveSectorSummary()
    {
        SectorMetadata metadata = ActiveSectorMetadata ?? GetOrCreateSectorMetadata(ActiveSectorCoord);
        GD.Print($"World {WorldId} seed={WorldSeed} active sector={metadata.SectorCoord} type={metadata.Type} danger={metadata.DangerLevel:0.00} resources={metadata.ResourceRichness:0.00} buildRestricted={metadata.IsBuildRestricted}");
    }

    public void PrintSurroundingSectorMetadata(Vector2I centerCoord)
    {
        GD.Print($"World sector metadata 3x3 around {centerCoord}, world={WorldId}, seed={WorldSeed}");

        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                SectorMetadata metadata = GetOrCreateSectorMetadata(centerCoord + new Vector2I(x, y));
                GD.Print($"  sector={metadata.SectorCoord} type={metadata.Type} seed={metadata.SectorSeed} danger={metadata.DangerLevel:0.00} resources={metadata.ResourceRichness:0.00} central={metadata.IsCentralTownSector} restricted={metadata.IsBuildRestricted}");
            }
        }
    }

    public static int MakeSectorSeed(int worldSeed, Vector2I sectorCoord)
    {
        uint hash = 2166136261u;
        Mix(ref hash, worldSeed);
        Mix(ref hash, sectorCoord.X);
        Mix(ref hash, sectorCoord.Y);
        return unchecked((int)(hash & 0x7fffffffu));
    }

    private static int MakeChunkSeed(int sectorSeed, Vector2I chunkCoord)
    {
        uint hash = 2166136261u;
        Mix(ref hash, sectorSeed);
        Mix(ref hash, chunkCoord.X);
        Mix(ref hash, chunkCoord.Y);
        return unchecked((int)(hash & 0x7fffffffu));
    }

    private void ApplySectorData(SectorGenerationData sectorData)
    {
        ResolveSceneReferences();
        _worldGrid?.ApplySectorGeneration(sectorData);
        ClearResourceNodes();

        foreach (GeneratedResourceNodeData nodeData in sectorData.ResourceNodes)
        {
            SpawnResourceNode(nodeData);
        }
    }

    private void MergeChunkIntoSector(SectorGenerationData sectorData, WorldChunkData chunkData)
    {
        foreach (KeyValuePair<Vector2I, Color> entry in chunkData.TerrainColors)
        {
            if (IsInsideDisplay(entry.Key, sectorData))
            {
                sectorData.TerrainColors[entry.Key] = entry.Value;
            }
        }

        foreach (Vector2I cell in chunkData.BuildRestrictedCells)
        {
            if (IsInsideDisplay(cell, sectorData))
            {
                sectorData.BuildRestrictedCells.Add(cell);
            }
        }

        foreach (GeneratedResourceNodeData nodeData in chunkData.ResourceNodes)
        {
            if (IsInsideDisplay(nodeData.Cell, sectorData))
            {
                sectorData.ResourceNodes.Add(nodeData);
            }
        }
    }

    private static bool IsInsideDisplay(Vector2I cell, SectorGenerationData sectorData)
    {
        return cell.X >= 0 && cell.Y >= 0 && cell.X < sectorData.DisplayWidth && cell.Y < sectorData.DisplayHeight;
    }

    private void CreateNeighborMetadata(Vector2I centerCoord)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                GetOrCreateSectorMetadata(centerCoord + new Vector2I(x, y));
            }
        }
    }

    private void AddChunkResourceNodes(SectorMetadata metadata, WorldChunkData chunkData)
    {
        if (metadata.IsCentralTownSector)
        {
            return;
        }

        int richnessNodes = Mathf.RoundToInt(metadata.ResourceRichness * 2.0f);
        int nodeCount = GetBaseResourceNodeCount(metadata.Type) + richnessNodes;

        for (int i = 0; i < nodeCount; i++)
        {
            int rollSeed = StableHash(chunkData.ChunkSeed, i * 31 + 7);

            if (StableUnitFloat(rollSeed, 3) > GetResourceSpawnChance(metadata.Type))
            {
                continue;
            }

            BaseResourceType resourceType = PickResourceType(metadata.Type, rollSeed);
            Vector2I cell = chunkData.ChunkCoord * ChunkSizeCells + new Vector2I(
                2 + StablePositiveInt(rollSeed, 13, ChunkSizeCells - 4),
                2 + StablePositiveInt(rollSeed, 29, ChunkSizeCells - 4));

            if (IsRoadCell(metadata, cell))
            {
                continue;
            }

            int amount = 12 + StablePositiveInt(rollSeed, 43, 28) + Mathf.RoundToInt(metadata.ResourceRichness * 18.0f);
            chunkData.ResourceNodes.Add(new GeneratedResourceNodeData(resourceType, cell, amount));
        }
    }

    private void SpawnResourceNode(GeneratedResourceNodeData nodeData)
    {
        if (_resourceNodeLayer == null)
        {
            return;
        }

        ResourceNode resourceNode = new()
        {
            Name = $"{nodeData.ResourceType}Node_{nodeData.Cell.X}_{nodeData.Cell.Y}",
            Position = GetWorldPosition(nodeData.Cell)
        };

        _resourceNodeLayer.AddChild(resourceNode);
        resourceNode.Initialize(nodeData.ResourceType, nodeData.Cell, nodeData.Amount);
    }

    private void ClearResourceNodes()
    {
        if (_resourceNodeLayer == null)
        {
            return;
        }

        foreach (Node child in _resourceNodeLayer.GetChildren())
        {
            if (child is ResourceNode resourceNode)
            {
                resourceNode.QueueFree();
            }
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

    private void ResolveSceneReferences()
    {
        _worldGrid ??= GetTree().CurrentScene?.GetNodeOrNull<WorldGridRenderer>("TerrainLayer");
        _resourceNodeLayer ??= GetTree().CurrentScene?.GetNodeOrNull<Node2D>("ResourceNodeLayer");
        _baseBuildManager ??= GetTree().CurrentScene?.GetNodeOrNull<BaseBuildManager>("BuildingLayer");
    }

    private static SectorType PickSectorType(int sectorSeed, float distanceFromCenter)
    {
        float roll = StableUnitFloat(sectorSeed, 23);

        if (distanceFromCenter <= 1.0f)
        {
            if (roll < 0.36f) return SectorType.Outskirts;
            if (roll < 0.58f) return SectorType.ForestEdge;
            if (roll < 0.78f) return SectorType.RuinedResidential;
            if (roll < 0.90f) return SectorType.TradeRoad;
            return SectorType.Quarry;
        }

        if (distanceFromCenter <= 3.0f)
        {
            if (roll < 0.24f) return SectorType.RuinedResidential;
            if (roll < 0.42f) return SectorType.ForestEdge;
            if (roll < 0.58f) return SectorType.Quarry;
            if (roll < 0.72f) return SectorType.RuinedFactory;
            if (roll < 0.84f) return SectorType.TradeRoad;
            if (roll < 0.94f) return SectorType.BanditTerritory;
            return SectorType.Wasteland;
        }

        if (roll < 0.18f) return SectorType.RuinedFactory;
        if (roll < 0.32f) return SectorType.Quarry;
        if (roll < 0.48f) return SectorType.BanditTerritory;
        if (roll < 0.62f) return SectorType.MonsterNestArea;
        if (roll < 0.76f) return SectorType.Wasteland;
        if (roll < 0.88f) return SectorType.ForestEdge;
        return SectorType.RuinedResidential;
    }

    private Color GetTerrainColor(SectorMetadata metadata, Vector2I cell)
    {
        if (metadata.IsCentralTownSector)
        {
            return GetCentralTownTerrainColor(cell);
        }

        if (IsRoadCell(metadata, cell))
        {
            return new Color(0.27f, 0.27f, 0.25f);
        }

        Color baseColor = metadata.Type switch
        {
            SectorType.Outskirts => new Color(0.28f, 0.43f, 0.24f),
            SectorType.RuinedResidential => new Color(0.31f, 0.32f, 0.30f),
            SectorType.RuinedFactory => new Color(0.25f, 0.27f, 0.28f),
            SectorType.ForestEdge => new Color(0.20f, 0.42f, 0.23f),
            SectorType.Quarry => new Color(0.42f, 0.42f, 0.39f),
            SectorType.BanditTerritory => new Color(0.34f, 0.30f, 0.22f),
            SectorType.MonsterNestArea => new Color(0.30f, 0.24f, 0.31f),
            SectorType.TradeRoad => new Color(0.32f, 0.37f, 0.26f),
            _ => new Color(0.29f, 0.28f, 0.24f)
        };

        float noise = StableUnitFloat(metadata.SectorSeed, cell.X * 73856093 ^ cell.Y * 19349663) - 0.5f;

        if (IsRubbleCell(metadata, cell))
        {
            return baseColor.Darkened(0.22f + Mathf.Abs(noise) * 0.18f);
        }

        return noise >= 0.0f
            ? baseColor.Lightened(noise * 0.12f)
            : baseColor.Darkened(-noise * 0.12f);
    }

    private static Color GetCentralTownTerrainColor(Vector2I cell)
    {
        const int displayCenter = 64;
        int dx = Mathf.Abs(cell.X - displayCenter);
        int dy = Mathf.Abs(cell.Y - displayCenter);

        if (dx <= 7 && dy <= 7)
        {
            return new Color(0.44f, 0.43f, 0.39f);
        }

        if (dx <= 2 || dy <= 2 || cell.X % 24 <= 1 || cell.Y % 24 <= 1)
        {
            return new Color(0.34f, 0.34f, 0.31f);
        }

        return ((cell.X + cell.Y) & 1) == 0
            ? new Color(0.26f, 0.42f, 0.27f)
            : new Color(0.23f, 0.38f, 0.25f);
    }

    private static bool IsRoadCell(SectorMetadata metadata, Vector2I cell)
    {
        if (metadata.Type == SectorType.TradeRoad)
        {
            return Mathf.Abs(cell.Y - 64) <= 2 || Mathf.Abs(cell.X - 64) <= 1;
        }

        if (metadata.Type == SectorType.RuinedResidential || metadata.Type == SectorType.RuinedFactory)
        {
            return cell.X % 28 <= 2 || cell.Y % 31 <= 2;
        }

        return false;
    }

    private static bool IsRubbleCell(SectorMetadata metadata, Vector2I cell)
    {
        if (metadata.Type != SectorType.RuinedResidential && metadata.Type != SectorType.RuinedFactory)
        {
            return false;
        }

        int hash = StableHash(metadata.SectorSeed, cell.X * 31 + cell.Y * 97);
        return hash % 41 == 0 || hash % 53 == 0;
    }

    private static int GetBaseResourceNodeCount(SectorType sectorType)
    {
        return sectorType switch
        {
            SectorType.ForestEdge => 3,
            SectorType.Quarry => 3,
            SectorType.RuinedFactory => 2,
            SectorType.Wasteland => 1,
            SectorType.BanditTerritory => 2,
            SectorType.MonsterNestArea => 2,
            _ => 1
        };
    }

    private static float GetResourceSpawnChance(SectorType sectorType)
    {
        return sectorType switch
        {
            SectorType.ForestEdge => 0.82f,
            SectorType.Quarry => 0.78f,
            SectorType.RuinedFactory => 0.62f,
            SectorType.BanditTerritory => 0.50f,
            SectorType.MonsterNestArea => 0.46f,
            _ => 0.42f
        };
    }

    private static BaseResourceType PickResourceType(SectorType sectorType, int rollSeed)
    {
        float roll = StableUnitFloat(rollSeed, 71);

        return sectorType switch
        {
            SectorType.ForestEdge => roll < 0.72f ? BaseResourceType.Wood : roll < 0.86f ? BaseResourceType.Herb : BaseResourceType.Stone,
            SectorType.Quarry => roll < 0.52f ? BaseResourceType.Stone : roll < 0.80f ? BaseResourceType.IronOre : BaseResourceType.Coal,
            SectorType.RuinedFactory => roll < 0.46f ? BaseResourceType.IronOre : roll < 0.74f ? BaseResourceType.Coal : BaseResourceType.Stone,
            SectorType.MonsterNestArea => roll < 0.44f ? BaseResourceType.Coal : roll < 0.68f ? BaseResourceType.IronOre : BaseResourceType.Herb,
            SectorType.Wasteland => roll < 0.38f ? BaseResourceType.Stone : roll < 0.66f ? BaseResourceType.Coal : BaseResourceType.Herb,
            _ => roll < 0.42f ? BaseResourceType.Wood : roll < 0.70f ? BaseResourceType.Stone : roll < 0.86f ? BaseResourceType.Herb : BaseResourceType.IronOre
        };
    }

    private static int StablePositiveInt(int seed, int salt, int modulo)
    {
        if (modulo <= 0)
        {
            return 0;
        }

        int value = StableHash(seed, salt) & 0x7fffffff;
        return value % modulo;
    }

    private static float StableUnitFloat(int seed, int salt)
    {
        int value = StableHash(seed, salt) & 0x00ffffff;
        return value / (float)0x01000000;
    }

    private static int StableHash(int seed, int salt)
    {
        uint hash = 2166136261u;
        Mix(ref hash, seed);
        Mix(ref hash, salt);
        return unchecked((int)(hash & 0x7fffffffu));
    }

    private static void Mix(ref uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }
    }
}
