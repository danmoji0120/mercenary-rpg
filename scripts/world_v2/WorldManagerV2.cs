using System.Collections.Generic;
using Godot;

namespace WorldV2;

public partial class WorldManagerV2 : Node
{
    [Export]
    public string WorldId { get; set; } = "world_v2_local";

    [Export]
    public int WorldSeed { get; set; } = 20260707;

    [Export]
    public bool UseRandomSeed { get; set; } = false;

    [Export]
    public Vector2I CurrentSectorCoord { get; set; } = Vector2I.Zero;

    [Export]
    public int ActiveSectorWidthCells { get; set; } = 128;

    [Export]
    public int ActiveSectorHeightCells { get; set; } = 128;

    [Export]
    public NodePath GridRendererPath { get; set; } = "../GridLayer";

    [Export]
    public NodePath BuildManagerPath { get; set; } = "../BuildingLayer";

    [Export]
    public NodePath DebugHudPath { get; set; } = "../CanvasLayer/WorldV2DebugHud";

    [Export]
    public NodePath StreamManagerPath { get; set; } = "../WorldStreamManagerV2";

    [Export]
    public NodePath GenerationSettingsPath { get; set; } = "../WorldGenerationSettingsV2";

    public SectorData? ActiveSector { get; private set; }
    public SectorRuntimeState? ActiveRuntimeState { get; private set; }
    public SectorMetadata? ActiveSectorMetadata { get; private set; }
    public Vector2I PlayerStartGlobalCell { get; private set; } = new(64, 64);
    public WorldGenerationRequestV2 GenerationRequest { get; private set; } = WorldGenerationRequestV2.CreateDevDefault(20260707);
    public WorldMapSizeDefinitionV2 WorldMapSize => GenerationRequest.MapSize;
    public WorldMapSizePresetV2 MapSizePreset => GenerationRequest.MapSizePreset;
    public WorldPlanVersionV2 PlanVersion => GenerationRequest.PlanVersion;
    public Rect2I WorldBounds => WorldMapSize.CellBounds;
    public string GeneratedPlanType => _generator.GeneratedPlanType;
    public int V3VillageCount => _generator.V3VillageCount;
    public int V3StartingVillageId => _generator.V3StartingVillageId;
    public Vector2I V3StartingVillageCenter => _generator.V3StartingVillageCenter;
    public Vector2I V3PlayerSpawnCell => _generator.PlayerSpawnCell;
    public float V3NearestToWorldCenterDistance => _generator.V3NearestToWorldCenterDistance;
    public string V3VillageDebugSummary => _generator.V3VillageDebugSummary;
    public int V3RoadCount => _generator.V3RoadCount;
    public int V3PrimaryRoadCount => _generator.V3PrimaryRoadCount;
    public int V3SecondaryRoadCount => _generator.V3SecondaryRoadCount;
    public int V3ExtraRoadCount => _generator.V3ExtraRoadCount;
    public int V3BranchRoadCount => _generator.V3BranchRoadCount;
    public int V3RoadTargetAnchorCount => _generator.V3RoadTargetAnchorCount;
    public int V3RoadTargetQuarryCount => _generator.V3RoadTargetQuarryCount;
    public int V3RoadTargetRuinCount => _generator.V3RoadTargetRuinCount;
    public int V3RoadTargetForestEdgeCount => _generator.V3RoadTargetForestEdgeCount;
    public int V3RoadTargetWorldEdgeExitCount => _generator.V3RoadTargetWorldEdgeExitCount;
    public int V3FutureRoadTargetCount => _generator.V3FutureRoadTargetCount;
    public int V3RejectedRoadTargetCount => _generator.V3RejectedRoadTargetCount;
    public int V3RejectedBranchRoadCount => _generator.V3RejectedBranchRoadCount;
    public int V3RoadNodeCount => _generator.V3RoadNodeCount;
    public int V3RoadJunctionCount => _generator.V3RoadJunctionCount;
    public int V3SharedTrunkCount => _generator.V3SharedTrunkCount;
    public int V3MergedRoadCandidateCount => _generator.V3MergedRoadCandidateCount;
    public int V3RejectedRoadJunctionCount => _generator.V3RejectedRoadJunctionCount;
    public int V3MaxRoadJunctionDegree => _generator.V3MaxRoadJunctionDegree;
    public int V3RejectedHighDegreeJunctionCount => _generator.V3RejectedHighDegreeJunctionCount;
    public int V3RejectedRoadCrossingCount => _generator.V3RejectedRoadCrossingCount;
    public int V3RejectedRoadTooLongCount => _generator.V3RejectedRoadTooLongCount;
    public bool V3RoadLayerEnabled => _generator.V3RoadLayerEnabled;
    public int V3ForestClusterCount => _generator.V3ForestClusterCount;
    public int V3ForestRegionCount => _generator.V3ForestRegionCount;
    public int V3MajorForestRegionCount => _generator.V3MajorForestRegionCount;
    public int V3MinorForestPatchCount => _generator.V3MinorForestPatchCount;
    public int V3LargeForestClusterCount => _generator.V3LargeForestClusterCount;
    public bool V3ForestLayerEnabled => _generator.V3ForestLayerEnabled;
    public int V3QuarryClusterCount => _generator.V3QuarryClusterCount;
    public int V3QuarryRegionCount => _generator.V3QuarryRegionCount;
    public int V3MajorQuarryCount => _generator.V3MajorQuarryCount;
    public int V3MinorQuarryCount => _generator.V3MinorQuarryCount;
    public int V3RejectedQuarryPlacementCount => _generator.V3RejectedQuarryPlacementCount;
    public bool V3QuarryLayerEnabled => _generator.V3QuarryLayerEnabled;
    public int V3RuinSiteCount => _generator.V3RuinSiteCount;
    public int V3RoadLinkedRuinCount => _generator.V3RoadLinkedRuinCount;
    public int V3RejectedRuinPlacementCount => _generator.V3RejectedRuinPlacementCount;
    public bool V3RuinLayerEnabled => _generator.V3RuinLayerEnabled;
    public bool WorldMapOverlayVisible { get; private set; }
    public Vector2I WorldMapTextureSize { get; private set; }
    public double WorldMapBuildMs { get; private set; }
    public bool WorldMapCached { get; private set; }
    public string WorldMapLastBuildReason { get; private set; } = "not built";

    private readonly ProceduralWorldGeneratorV2 _generator = new();
    private readonly Dictionary<Vector2I, SectorMetadata> _metadataBySector = new();
    private readonly Dictionary<Vector2I, SectorRuntimeState> _runtimeStatesBySector = new();
    private WorldV2GridRenderer? _gridRenderer;
    private WorldV2BuildManager? _buildManager;
    private WorldStreamManagerV2? _streamManager;
    private WorldV2DebugHud? _debugHud;
    private WorldGenerationSettingsV2? _generationSettings;
    private Texture2D? _worldMapTexture;
    private string _worldMapCacheKey = string.Empty;

    public override void _Ready()
    {
        ResolveReferences();
        LoadGenerationRequest();
        InitializeWorld();
    }

    public void InitializeWorld()
    {
        if (UseRandomSeed && WorldGenerationSessionV2.ActiveRequest == null)
        {
            RandomNumberGenerator random = new();
            random.Randomize();
            WorldSeed = (int)(random.Randi() & 0x7fffffffu);
            GenerationRequest = WorldGenerationRequestV2.CreateDevDefault(WorldSeed);
        }

        WorldId = GenerationRequest.WorldId;
        WorldSeed = GenerationRequest.Seed;
        ApplyGenerationSettings();
        UpdateStreamingCenter(PlayerStartGlobalCell, WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(PlayerStartGlobalCell));
    }

    public SectorMetadata GetOrCreateSectorMetadata(Vector2I sectorCoord)
    {
        ApplyGenerationSettings();

        if (_metadataBySector.TryGetValue(sectorCoord, out SectorMetadata? metadata))
        {
            return metadata;
        }

        metadata = _generator.CreateMetadata(WorldId, WorldSeed, sectorCoord);
        _metadataBySector[sectorCoord] = metadata;
        return metadata;
    }

    public SectorData LoadSector(Vector2I sectorCoord)
    {
        ResolveReferences();
        ApplyGenerationSettings();

        SectorMetadata metadata = GetOrCreateSectorMetadata(sectorCoord);
        metadata.IsVisited = true;
        metadata.IsDiscovered = true;
        CurrentSectorCoord = sectorCoord;
        ActiveSectorMetadata = metadata;
        ActiveSector = _generator.GenerateSector(metadata, ActiveSectorWidthCells, ActiveSectorHeightCells);
        ActiveRuntimeState = GetOrCreateSectorRuntimeState(sectorCoord);
        ApplyRuntimeState(ActiveSector, ActiveRuntimeState);

        _gridRenderer?.ApplySector(ActiveSector);
        _buildManager?.SetActiveSector(sectorCoord, ActiveRuntimeState);
        CreateNeighborMetadata(sectorCoord);
        UpdateDebugHud("Sector loaded.");

        return ActiveSector;
    }

    public ChunkDataV2 GenerateChunk(Vector2I globalChunkCoord)
    {
        ApplyGenerationSettings();
        return _generator.GenerateChunk(WorldId, WorldSeed, globalChunkCoord, GetOrCreateSectorMetadata);
    }

    public bool IsChunkWithinWorldBounds(Vector2I globalChunkCoord)
    {
        return WorldMapSize.ContainsChunk(globalChunkCoord);
    }

    public bool IsCellWithinWorldBounds(Vector2I globalCellCoord)
    {
        return WorldMapSize.ContainsCell(globalCellCoord);
    }

    public Vector2I ClampGlobalCellToWorld(Vector2I globalCellCoord)
    {
        return WorldMapSize.ClampCell(globalCellCoord);
    }

    public WorldGenerationRequestV2 GetGenerationRequest()
    {
        return GenerationRequest;
    }

    public IReadOnlyList<VillageSiteV2> GetV3MapVillages()
    {
        return _generator.GetV3Villages();
    }

    public IReadOnlyList<RoadPathV2> GetV3MapRoads()
    {
        return _generator.GetV3Roads();
    }

    public IReadOnlyList<ForestRegionV3> GetV3MapForestRegions()
    {
        return _generator.GetV3ForestRegions();
    }

    public IReadOnlyList<QuarryRegionV3> GetV3MapQuarryRegions()
    {
        return _generator.GetV3QuarryRegions();
    }

    public IReadOnlyList<RuinSiteV3> GetV3MapRuinSites()
    {
        return _generator.GetV3RuinSites();
    }

    public Texture2D GetOrBuildWorldMapTexture(string reason)
    {
        ApplyGenerationSettings();
        string cacheKey = BuildWorldMapCacheKey();
        if (_worldMapTexture != null && _worldMapCacheKey == cacheKey)
        {
            WorldMapCached = true;
            WorldMapLastBuildReason = "cached";
            return _worldMapTexture;
        }

        _worldMapTexture = WorldMapTextureBuilderV2.Build(this, GetGenerationSettings(), out Vector2I textureSize, out double buildMs);
        WorldMapTextureSize = textureSize;
        WorldMapBuildMs = buildMs;
        WorldMapCached = false;
        WorldMapLastBuildReason = reason;
        _worldMapCacheKey = cacheKey;
        return _worldMapTexture;
    }

    public void SetWorldMapOverlayVisible(bool visible)
    {
        WorldMapOverlayVisible = visible;
        UpdateDebugHud();
    }

    public void UpdateStreamingCenter(Vector2I centerGlobalCellCoord, Vector2I centerGlobalChunkCoord, bool updateHud = true)
    {
        Vector2I sectorCoord = WorldV2CoordinateUtility.GlobalCellToSectorCoord(centerGlobalCellCoord);
        SectorMetadata metadata = GetOrCreateSectorMetadata(sectorCoord);
        metadata.IsVisited = true;
        metadata.IsDiscovered = true;
        CurrentSectorCoord = sectorCoord;
        ActiveSectorMetadata = metadata;
        ActiveRuntimeState = GetOrCreateSectorRuntimeState(sectorCoord);
        _buildManager?.SetActiveSector(sectorCoord, ActiveRuntimeState);
        CreateNeighborMetadata(sectorCoord);
        if (updateHud)
        {
            UpdateDebugHud();
        }
    }

    public void MoveSector(Vector2I delta)
    {
        LoadSector(CurrentSectorCoord + delta);
    }

    public void PrintSurroundingSectorMetadata()
    {
        GD.Print($"WorldV2 3x3 metadata around {CurrentSectorCoord}, world={WorldId}, seed={WorldSeed}");

        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                SectorMetadata metadata = GetOrCreateSectorMetadata(CurrentSectorCoord + new Vector2I(x, y));
                bool hasRuntime = _runtimeStatesBySector.TryGetValue(metadata.SectorCoord, out SectorRuntimeState? runtimeState);
                int structures = runtimeState?.StructureCount ?? 0;
                bool dirty = runtimeState?.IsDirty ?? false;
                GD.Print($"  sector={metadata.SectorCoord} type={metadata.Type} biome={metadata.DominantBiome} secondary={metadata.SecondaryBiome} diversity={metadata.BiomeDiversityScore:0.00} roadCoverage={metadata.RoadCoverage:P0} roads={metadata.RoadPathCount} villages={metadata.VillageCount} landmarks={metadata.LandmarkCount} connectedLandmarks={metadata.RoadConnectedLandmarkCount} quarries={metadata.QuarryCount} start={metadata.HasStartingArea} startAvg={metadata.AverageStartInfluence:0.00} seed={metadata.SectorSeed} danger={metadata.AverageDanger:0.00} resources={metadata.AverageResourceRichness:0.00} ruin={metadata.AverageRuinDensity:0.00} river={metadata.HasRiver} road={metadata.HasRoad} central={metadata.IsCentralTown} restricted={metadata.IsBuildRestricted} runtime={hasRuntime} structures={structures} dirty={dirty}");
            }
        }
    }

    public void RegenerateVisibleChunks(bool clearRuntimeStructures)
    {
        ResolveReferences();
        ApplyGenerationSettings();

        if (clearRuntimeStructures)
        {
            long resetStart = WorldV2PerformanceProfiler.Instance.BeginSample();
            _generator.ClearGeneratedPlanCache();
            _metadataBySector.Clear();
            ClearWorldMapTextureCache();
            ClearRuntimeStructures();
            _streamManager?.ClearAllChunkCaches();
            _streamManager?.RefreshStreaming(force: true);
            CreateNeighborMetadata(CurrentSectorCoord);
            WorldV2PerformanceProfiler.Instance.EndSample(WorldV2PerformanceProfiler.F12Reset, resetStart, CurrentSectorCoord);
            UpdateDebugHud("Full regenerate: cleared plan, chunk cache, renderers, and structures.");
            return;
        }

        _streamManager?.RebuildVisibleRenderersFromCache();
        UpdateDebugHud("Rebuilt visible renderers from cached chunk data.");
    }

    private void ClearWorldMapTextureCache()
    {
        _worldMapTexture = null;
        _worldMapCacheKey = string.Empty;
        WorldMapCached = false;
        WorldMapTextureSize = Vector2I.Zero;
        WorldMapBuildMs = 0.0;
        WorldMapLastBuildReason = "cache cleared";
    }

    public void PrintDebugHelp()
    {
        GD.Print("WorldV2 debug keys:");
        GD.Print("  1/2: select floor/wall");
        GD.Print("  LMB/RMB: place/remove test structure");
        GD.Print("  WASD/arrows: camera move, Shift: sprint, wheel: zoom");
        GD.Print("  F2: debug HUD page, F3: overlay mode, F4: chunk grid, F6: sectors");
        GD.Print("  F7: streaming summary, F8: chunk cache summary, F9: flatland plan cache");
        GD.Print("  F10: performance summary, Home: center on start");
        GD.Print("  F11: rebuild visible renderers from cache, F12: full regenerate and clear structures");
        GD.Print("  Ctrl+1/2/3/4: toggle village/site/road/forest raster diagnostics");
        GD.Print("  Ctrl+5/6/7/8: toggle village/site/road/forest context diagnostics");
        GD.Print("  Ctrl+Shift+1..8: toggle generation layers, Ctrl+Shift+9: toggle rivers");
    }

    public void PrintRuntimeStateSummary()
    {
        if (_runtimeStatesBySector.Count == 0)
        {
            GD.Print("WorldV2 runtime states: none");
            return;
        }

        GD.Print($"WorldV2 runtime states: total={_runtimeStatesBySector.Count} dirty={GetDirtyRuntimeSectorCount()}");

        foreach (KeyValuePair<Vector2I, SectorRuntimeState> entry in _runtimeStatesBySector)
        {
            SectorRuntimeState state = entry.Value;
            GD.Print($"  sector={entry.Key} structures={state.StructureCount} tileOverrides={state.TileOverrideCount} removedProcedural={state.RemovedProceduralObjectCount} dirty={state.IsDirty}");
        }
    }

    public void PrintLoadedChunkSummary()
    {
        ResolveReferences();
        GD.Print($"WorldV2 world: map={MapSizePreset} size={WorldMapSize.WidthCells}x{WorldMapSize.HeightCells} chunks={WorldMapSize.ChunkWidth}x{WorldMapSize.ChunkHeight} plan={PlanVersion} generated={GeneratedPlanType} seed={WorldSeed} bounds={WorldBounds.Position}..{WorldBounds.End - Vector2I.One}");
        GD.Print(V3VillageDebugSummary);
        GD.Print($"V3 roads: enabled={V3RoadLayerEnabled} total={V3RoadCount} primary={V3PrimaryRoadCount} secondary={V3SecondaryRoadCount} extra={V3ExtraRoadCount} branch={V3BranchRoadCount} nodes={V3RoadNodeCount} junctions={V3RoadJunctionCount} maxDegree={V3MaxRoadJunctionDegree} trunks={V3SharedTrunkCount} merged={V3MergedRoadCandidateCount} rejectedJunctions={V3RejectedRoadJunctionCount} rejectedHighDegree={V3RejectedHighDegreeJunctionCount} rejectedCrossings={V3RejectedRoadCrossingCount} rejectedTooLong={V3RejectedRoadTooLongCount} targets={V3RoadTargetAnchorCount} quarryTargets={V3RoadTargetQuarryCount} ruinTargets={V3RoadTargetRuinCount} forestTargets={V3RoadTargetForestEdgeCount} edgeTargets={V3RoadTargetWorldEdgeExitCount} futureTargets={V3FutureRoadTargetCount} rejectedTargets={V3RejectedRoadTargetCount} rejectedBranches={V3RejectedBranchRoadCount}");
        GD.Print($"V3 forests: enabled={V3ForestLayerEnabled} regions={V3ForestRegionCount} major={V3MajorForestRegionCount} minor={V3MinorForestPatchCount}");
        GD.Print($"V3 quarries: enabled={V3QuarryLayerEnabled} regions={V3QuarryRegionCount} major={V3MajorQuarryCount} minor={V3MinorQuarryCount} rejected={V3RejectedQuarryPlacementCount}");
        GD.Print($"V3 ruins: enabled={V3RuinLayerEnabled} sites={V3RuinSiteCount} roadLinked={V3RoadLinkedRuinCount} rejected={V3RejectedRuinPlacementCount}");
        _streamManager?.PrintLoadedChunks();
        GD.Print(WorldGenerationLayerSettingsV2.GetSummary());
    }

    public void PrintChunkCacheSummary()
    {
        ResolveReferences();
        _streamManager?.PrintChunkCacheSummary();
    }

    public void PrintFlatlandPlanCacheSummary()
    {
        GD.Print(_generator.GetPlanCacheSummary());
    }

    public void PrintPerformanceSummary()
    {
        GD.Print($"WorldV2 world: map={MapSizePreset} size={WorldMapSize.WidthCells}x{WorldMapSize.HeightCells} plan={PlanVersion} generated={GeneratedPlanType} seed={WorldSeed}");
        GD.Print(V3VillageDebugSummary);
        GD.Print($"V3 roads: enabled={V3RoadLayerEnabled} total={V3RoadCount} primary={V3PrimaryRoadCount} secondary={V3SecondaryRoadCount} extra={V3ExtraRoadCount} branch={V3BranchRoadCount} nodes={V3RoadNodeCount} junctions={V3RoadJunctionCount} maxDegree={V3MaxRoadJunctionDegree} trunks={V3SharedTrunkCount} merged={V3MergedRoadCandidateCount} rejectedJunctions={V3RejectedRoadJunctionCount} rejectedHighDegree={V3RejectedHighDegreeJunctionCount} rejectedCrossings={V3RejectedRoadCrossingCount} rejectedTooLong={V3RejectedRoadTooLongCount} targets={V3RoadTargetAnchorCount} quarryTargets={V3RoadTargetQuarryCount} ruinTargets={V3RoadTargetRuinCount} forestTargets={V3RoadTargetForestEdgeCount} edgeTargets={V3RoadTargetWorldEdgeExitCount} futureTargets={V3FutureRoadTargetCount} rejectedTargets={V3RejectedRoadTargetCount} rejectedBranches={V3RejectedBranchRoadCount}");
        GD.Print($"V3 forests: enabled={V3ForestLayerEnabled} regions={V3ForestRegionCount} major={V3MajorForestRegionCount} minor={V3MinorForestPatchCount}");
        GD.Print($"V3 quarries: enabled={V3QuarryLayerEnabled} regions={V3QuarryRegionCount} major={V3MajorQuarryCount} minor={V3MinorQuarryCount} rejected={V3RejectedQuarryPlacementCount}");
        GD.Print($"V3 ruins: enabled={V3RuinLayerEnabled} sites={V3RuinSiteCount} roadLinked={V3RoadLinkedRuinCount} rejected={V3RejectedRuinPlacementCount}");
        WorldV2PerformanceProfiler.Instance.PrintSummary();
    }

    public void ToggleChunkDebugDisplay()
    {
        ResolveReferences();
        _streamManager?.ToggleChunkDebugDisplay();
        UpdateDebugHud("Toggled chunk grid.");
    }

    public void CycleOverlayMode()
    {
        ResolveReferences();
        _streamManager?.CycleOverlayMode();
        UpdateDebugHud($"Overlay: {_streamManager?.OverlayMode.ToString() ?? "missing"}.");
    }

    public void UpdateDebugHud(string message = "")
    {
        ResolveReferences();
        _debugHud?.Refresh(this, _buildManager, message);
    }

    public void ToggleDebugHudPage()
    {
        ResolveReferences();
        _debugHud?.TogglePage();
        UpdateDebugHud("Toggled debug HUD page.");
    }

    public string GetActiveSectorSummary()
    {
        SectorMetadata? metadata = ActiveSectorMetadata;
        if (metadata == null)
        {
            return "No active sector.";
        }

        return $"seed={WorldSeed} sector={metadata.SectorCoord} type={metadata.Type} danger={metadata.DangerLevel:0.00} resources={metadata.ResourceRichness:0.00}";
    }

    public WorldStreamManagerV2? GetStreamManager()
    {
        ResolveReferences();
        return _streamManager;
    }

    public SectorRuntimeState GetOrCreateSectorRuntimeState(Vector2I sectorCoord)
    {
        if (_runtimeStatesBySector.TryGetValue(sectorCoord, out SectorRuntimeState? runtimeState))
        {
            return runtimeState;
        }

        runtimeState = new SectorRuntimeState(sectorCoord);
        _runtimeStatesBySector[sectorCoord] = runtimeState;
        return runtimeState;
    }

    public bool HasRuntimeState(Vector2I sectorCoord)
    {
        return _runtimeStatesBySector.ContainsKey(sectorCoord);
    }

    public int GetRuntimeStateCount()
    {
        return _runtimeStatesBySector.Count;
    }

    public int GetDirtyRuntimeSectorCount()
    {
        int count = 0;

        foreach (SectorRuntimeState runtimeState in _runtimeStatesBySector.Values)
        {
            if (runtimeState.IsDirty)
            {
                count++;
            }
        }

        return count;
    }

    public int GetRuntimeStructureCount(Vector2I sectorCoord)
    {
        return _runtimeStatesBySector.TryGetValue(sectorCoord, out SectorRuntimeState? state)
            ? state.StructureCount
            : 0;
    }

    public bool TryGetLoadedCell(Vector2I globalCellCoord, out CellData? cell)
    {
        ResolveReferences();
        cell = null;
        return _streamManager?.TryGetLoadedCell(globalCellCoord, out cell) == true;
    }

    public WorldClimateSampleV2 SampleClimateAt(Vector2I globalCellCoord)
    {
        ApplyGenerationSettings();
        return _generator.SampleClimate(WorldSeed, globalCellCoord);
    }

    public FlatlandCellSampleV2 SampleFlatlandAt(Vector2I globalCellCoord)
    {
        ApplyGenerationSettings();
        if (TryGetLoadedCell(globalCellCoord, out CellData? cell) && cell != null)
        {
            return CreateFlatlandSampleFromCell(cell);
        }

        return _generator.SampleFlatland(WorldSeed, globalCellCoord);
    }

    public WorldGenerationSettingsV2 GetGenerationSettings()
    {
        ResolveReferences();
        return _generationSettings ?? WorldGenerationSettingsV2.Default;
    }

    private string BuildWorldMapCacheKey()
    {
        return $"{WorldId}:{WorldSeed}:{MapSizePreset}:{PlanVersion}:{WorldMapSize.WidthCells}x{WorldMapSize.HeightCells}:"
            + $"{V3VillageCount}:{V3RoadCount}:{V3ForestRegionCount}:{V3QuarryRegionCount}:{V3RuinSiteCount}:"
            + WorldGenerationLayerSettingsV2.GetSummary();
    }

    private void ClearRuntimeStructures()
    {
        foreach (SectorRuntimeState runtimeState in _runtimeStatesBySector.Values)
        {
            runtimeState.StructuresByCell.Clear();
            runtimeState.MarkDirty();
        }

        _buildManager?.ClearAllRuntimeStructures();
    }

    private void ApplyGenerationSettings()
    {
        ResolveReferences();
        WorldGenerationSettingsV2 settings = _generationSettings ?? WorldGenerationSettingsV2.Default;
        _generator.SetGenerationSettings(settings);
        _generator.SetGenerationRequest(GenerationRequest);
        PlayerStartGlobalCell = _generator.PlayerSpawnCell;
    }

    private void LoadGenerationRequest()
    {
        int fallbackSeed = UseRandomSeed ? GenerateRandomSeed() : WorldSeed;
        GenerationRequest = WorldGenerationSessionV2.ConsumePendingOrCreateDefault(fallbackSeed);
        WorldId = GenerationRequest.WorldId;
        WorldSeed = GenerationRequest.Seed;
        PlayerStartGlobalCell = GenerationRequest.StartCell;
        _generator.SetGenerationRequest(GenerationRequest);
    }

    private static int GenerateRandomSeed()
    {
        RandomNumberGenerator random = new();
        random.Randomize();
        return (int)(random.Randi() & 0x7fffffffu);
    }

    private static FlatlandCellSampleV2 CreateFlatlandSampleFromCell(CellData cell)
    {
        return new FlatlandCellSampleV2
        {
            GlobalCellCoord = cell.GlobalCellCoord,
            IsRiver = cell.IsRiver,
            IsRiverBank = cell.IsRiverBank,
            IsBridgeCandidate = cell.IsBridgeCandidate,
            IsRoad = cell.IsRoad,
            IsVillage = cell.IsVillage,
            IsStartingVillage = cell.IsStartingVillage,
            IsLandmark = cell.IsLandmark,
            LandmarkKind = cell.LandmarkKind,
            IsQuarry = cell.IsQuarry,
            HasOreSpot = cell.HasOreSpot,
            ForestStrength = cell.ForestStrength,
            IsForest = cell.ForestStrength > 0.34f,
            IsDenseForest = cell.ForestStrength > 0.62f,
            IsBuildRestricted = cell.IsBuildRestricted,
            IsWalkable = cell.IsWalkable,
            Biome = cell.Biome,
            TileType = cell.TileType
        };
    }

    private static void ApplyRuntimeState(SectorData sectorData, SectorRuntimeState runtimeState)
    {
        foreach (KeyValuePair<Vector2I, TileType> tileOverride in runtimeState.TileOverrides)
        {
            if (sectorData.TryGetCell(tileOverride.Key, out CellData? cell) && cell != null)
            {
                cell.TileType = tileOverride.Value;
            }
        }
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

    private void ResolveReferences()
    {
        _gridRenderer ??= GetNodeOrNull<WorldV2GridRenderer>(GridRendererPath);
        _buildManager ??= GetNodeOrNull<WorldV2BuildManager>(BuildManagerPath);
        _streamManager ??= GetNodeOrNull<WorldStreamManagerV2>(StreamManagerPath);
        _debugHud ??= GetNodeOrNull<WorldV2DebugHud>(DebugHudPath);
        _generationSettings ??= GetNodeOrNull<WorldGenerationSettingsV2>(GenerationSettingsPath);
    }
}
