using System.Collections.Generic;
using Godot;

namespace WorldV2;

public sealed class ProceduralWorldGeneratorV2
{
    public const int ChunkSizeCells = 32;
    public const int SectorSizeChunks = 16;
    public const int SectorSizeCells = ChunkSizeCells * SectorSizeChunks;

    private readonly WorldNoiseProfileV2 _noiseProfile = new();
    private readonly FlatlandWorldPlanV2 _flatlandPlan = new();
    private readonly FlatlandWorldPlanV3 _flatlandPlanV3 = new();
    private int _initializedSeed = int.MinValue;
    private WorldGenerationSettingsV2 _settings = WorldGenerationSettingsV2.Default;
    private WorldGenerationRequestV2 _generationRequest = WorldGenerationRequestV2.CreateDevDefault(20260707);

    public void SetGenerationSettings(WorldGenerationSettingsV2? settings)
    {
        WorldGenerationSettingsV2 nextSettings = settings ?? WorldGenerationSettingsV2.Default;
        if (!ReferenceEquals(_settings, nextSettings))
        {
            _settings = nextSettings;
            _initializedSeed = int.MinValue;
        }
    }

    public void SetGenerationRequest(WorldGenerationRequestV2 request)
    {
        if (ReferenceEquals(_generationRequest, request))
        {
            return;
        }

        _generationRequest = request;
        _initializedSeed = int.MinValue;
    }

    public void ClearGeneratedPlanCache()
    {
        _flatlandPlan.ClearCaches();
        _flatlandPlanV3.ClearCaches();
        _initializedSeed = int.MinValue;
    }

    public string GetPlanCacheSummary()
    {
        if (_generationRequest.PlanVersion == WorldPlanVersionV2.V3)
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _flatlandPlanV3.GetDebugSummary();
        }

        return _flatlandPlan.GetCacheSummary();
    }

    public SectorMetadata CreateMetadata(string worldId, int worldSeed, Vector2I sectorCoord)
    {
        EnsureNoiseProfile(worldSeed);

        if (_generationRequest.PlanVersion == WorldPlanVersionV2.V3)
        {
            return CreateV3Metadata(worldId, worldSeed, sectorCoord);
        }

        int sectorSeed = MakeSectorSeed(worldSeed, sectorCoord);
        bool isCentralTownSector = sectorCoord == Vector2I.Zero;
        SectorMetadata metadata = new()
        {
            WorldId = worldId,
            WorldSeed = worldSeed,
            SectorCoord = sectorCoord,
            SectorSeed = sectorSeed,
            Type = isCentralTownSector ? SectorType.CentralTown : SectorType.Wasteland,
            IsDiscovered = isCentralTownSector,
            IsVisited = false,
            IsCentralTown = isCentralTownSector,
            IsBuildRestricted = isCentralTownSector
        };

        PopulateMetadataFromLowResolutionSamples(metadata);
        metadata.Type = MapDominantBiomeToSectorType(metadata.DominantBiome);
        metadata.DangerLevel = metadata.AverageDanger;
        metadata.ResourceRichness = metadata.AverageResourceRichness;
        metadata.IsCentralTown = metadata.DominantBiome == BiomeTypeV2.CentralTown || isCentralTownSector;
        metadata.IsBuildRestricted = metadata.DominantBiome == BiomeTypeV2.CentralTown || metadata.HasOcean;
        return metadata;
    }

    public SectorData GenerateSector(SectorMetadata metadata, int widthCells, int heightCells)
    {
        EnsureNoiseProfile(metadata.WorldSeed);
        SectorData data = new(metadata, widthCells, heightCells);

        for (int y = 0; y < data.HeightCells; y++)
        {
            for (int x = 0; x < data.WidthCells; x++)
            {
                Vector2I localCell = new(x, y);
                Vector2I globalCell = WorldV2CoordinateUtility.SectorAndLocalCellToGlobalCell(metadata.SectorCoord, localCell);
                data.Cells[localCell] = GenerateCell(metadata, globalCell, localCell);
            }
        }

        return data;
    }

    public ChunkDataV2 GenerateChunk(string worldId, int worldSeed, Vector2I globalChunkCoord, System.Func<Vector2I, SectorMetadata> getMetadata)
    {
        if (_generationRequest.PlanVersion == WorldPlanVersionV2.V3)
        {
            return GenerateChunkV3(worldId, worldSeed, globalChunkCoord, getMetadata);
        }

        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long totalStart = profiler.BeginSample();
        long contextStart = profiler.BeginSample();
        EnsureNoiseProfile(worldSeed);

        Vector2I sectorCoord = WorldV2CoordinateUtility.GlobalChunkToSectorCoord(globalChunkCoord);
        Vector2I localChunkCoord = WorldV2CoordinateUtility.GlobalChunkToLocalChunkInSector(globalChunkCoord);
        SectorMetadata metadata = getMetadata(sectorCoord);
        ChunkDataV2 chunkData = new(globalChunkCoord, sectorCoord, localChunkCoord);
        Vector2I originGlobalCell = WorldV2CoordinateUtility.GlobalChunkToOriginGlobalCell(globalChunkCoord);
        _flatlandPlan.PrewarmRoadCacheForChunk(globalChunkCoord);
        FlatlandChunkGenerationContextV2 flatlandContext = _flatlandPlan.BuildChunkContext(globalChunkCoord);
        _flatlandPlan.BuildChunkRaster(flatlandContext);
        profiler.EndSample(WorldV2PerformanceProfiler.ChunkContext, contextStart, globalChunkCoord);

        long fillStart = profiler.BeginSample();
        long sampleStart = profiler.BeginSample();

        for (int y = 0; y < ChunkSizeCells; y++)
        {
            for (int x = 0; x < ChunkSizeCells; x++)
            {
                Vector2I globalCell = originGlobalCell + new Vector2I(x, y);
                Vector2I localCell = WorldV2CoordinateUtility.GlobalCellToLocalCellInSector(globalCell);
                FlatlandCellSampleV2 sample = _flatlandPlan.SampleCellFast(globalCell, x, y, flatlandContext);
                chunkData.SetCellLocal(x, y, CreateCellFromSample(metadata, globalCell, localCell, sample));
            }
        }

        double sampleTotalMs = WorldV2PerformanceProfiler.ElapsedMilliseconds(sampleStart);
        profiler.RecordSample(WorldV2PerformanceProfiler.FlatlandSample, sampleTotalMs, globalChunkCoord);
        profiler.RecordSample(WorldV2PerformanceProfiler.FlatlandSampleTotal, sampleTotalMs, globalChunkCoord);
        profiler.RecordSample(WorldV2PerformanceProfiler.FlatlandTileResolve, sampleTotalMs, globalChunkCoord);
        profiler.EndSample(WorldV2PerformanceProfiler.ChunkDataFill, fillStart, globalChunkCoord);
        profiler.EndGenerateChunkSample(totalStart, globalChunkCoord, flatlandContext.GetProfilerSummary());
        return chunkData;
    }

    public ChunkDataV2 GenerateChunkDataOnly(string worldId, int worldSeed, Vector2I globalChunkCoord)
    {
        return GenerateChunk(worldId, worldSeed, globalChunkCoord, sectorCoord => CreateMinimalChunkMetadata(worldId, worldSeed, sectorCoord));
    }

    public ChunkDataV2 GenerateChunkDataOnly(string worldId, int worldSeed, Vector2I globalChunkCoord, WorldGenerationRequestV2 request)
    {
        SetGenerationRequest(request);
        return GenerateChunk(worldId, worldSeed, globalChunkCoord, sectorCoord => CreateMinimalChunkMetadata(worldId, worldSeed, sectorCoord));
    }

    private ChunkDataV2 GenerateChunkV3(string worldId, int worldSeed, Vector2I globalChunkCoord, System.Func<Vector2I, SectorMetadata> getMetadata)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long totalStart = profiler.BeginSample();
        EnsureNoiseProfile(worldSeed);

        Vector2I sectorCoord = WorldV2CoordinateUtility.GlobalChunkToSectorCoord(globalChunkCoord);
        Vector2I localChunkCoord = WorldV2CoordinateUtility.GlobalChunkToLocalChunkInSector(globalChunkCoord);
        SectorMetadata metadata = CreateV3Metadata(worldId, worldSeed, sectorCoord);
        ChunkDataV2 chunkData = new(globalChunkCoord, sectorCoord, localChunkCoord);
        Vector2I originGlobalCell = WorldV2CoordinateUtility.GlobalChunkToOriginGlobalCell(globalChunkCoord);
        long contextStart = profiler.BeginSample();
        FlatlandChunkGenerationContextV2 flatlandContext = _flatlandPlanV3.BuildChunkContext(globalChunkCoord);
        _flatlandPlanV3.BuildChunkRaster(flatlandContext);
        profiler.EndSample(WorldV2PerformanceProfiler.ChunkContext, contextStart, globalChunkCoord);

        long fillStart = profiler.BeginSample();
        long sampleStart = profiler.BeginSample();
        for (int y = 0; y < ChunkSizeCells; y++)
        {
            for (int x = 0; x < ChunkSizeCells; x++)
            {
                Vector2I globalCell = originGlobalCell + new Vector2I(x, y);
                Vector2I localCell = WorldV2CoordinateUtility.GlobalCellToLocalCellInSector(globalCell);
                FlatlandCellSampleV2 sample = _flatlandPlanV3.SampleCellFast(globalCell, x, y, flatlandContext);
                chunkData.SetCellLocal(x, y, CreateCellFromSample(metadata, globalCell, localCell, sample));
            }
        }

        double sampleTotalMs = WorldV2PerformanceProfiler.ElapsedMilliseconds(sampleStart);
        profiler.RecordSample(WorldV2PerformanceProfiler.FlatlandSample, sampleTotalMs, globalChunkCoord);
        profiler.RecordSample(WorldV2PerformanceProfiler.FlatlandSampleTotal, sampleTotalMs, globalChunkCoord);
        profiler.RecordSample(WorldV2PerformanceProfiler.FlatlandTileResolve, sampleTotalMs, globalChunkCoord);
        profiler.EndSample(WorldV2PerformanceProfiler.ChunkDataFill, fillStart, globalChunkCoord);
        profiler.EndGenerateChunkSample(totalStart, globalChunkCoord, $"plan=V3 size={_generationRequest.MapSizePreset} roads={flatlandContext.RelevantRoadCount}/{_flatlandPlanV3.RoadCount} {flatlandContext.GetProfilerSummary()}");
        return chunkData;
    }

    public WorldClimateSampleV2 SampleClimate(int worldSeed, Vector2I globalCell)
    {
        EnsureNoiseProfile(worldSeed);
        return _noiseProfile.SampleAll(globalCell);
    }

    public FlatlandCellSampleV2 SampleFlatland(int worldSeed, Vector2I globalCell)
    {
        EnsureNoiseProfile(worldSeed);
        if (_generationRequest.PlanVersion == WorldPlanVersionV2.V3)
        {
            return _flatlandPlanV3.SampleCell(globalCell);
        }

        return _flatlandPlan.SampleCell(globalCell);
    }

    public string GeneratedPlanType => _generationRequest.PlanVersion == WorldPlanVersionV2.V3
        ? _flatlandPlanV3.GeneratedPlanType
        : "FlatlandWorldPlanV2";

    public int V3VillageCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.VillageCount : 0;
        }
    }

    public int V3HamletCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.HamletCount : 0;
        }
    }

    public int V3VillageTierCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.VillageTierCount : 0;
        }
    }

    public int V3LargeVillageCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.LargeVillageCount : 0;
        }
    }

    public int V3TownCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.TownCount : 0;
        }
    }

    public int V3CityCandidateCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.CityCandidateCount : 0;
        }
    }

    public string V3SettlementRoleDistribution
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.SettlementRoleDistribution : "common/farm/trade/mining/forest/frontier/ruin/start=0/0/0/0/0/0/0/0";
        }
    }

    public VillageScaleV2 V3StartingSettlementTier
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.StartingSettlementTier : VillageScaleV2.Village;
        }
    }

    public SettlementRoleV3 V3StartingSettlementRole
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.StartingSettlementRole : SettlementRoleV3.StartingSettlement;
        }
    }

    public int V3StartingVillageId
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.StartingVillageId : -1;
        }
    }

    public Vector2I V3StartingVillageCenter
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.StartingVillageCenter : _generationRequest.StartCell;
        }
    }

    public Vector2I PlayerSpawnCell
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.PlayerSpawnCell : _generationRequest.StartCell;
        }
    }

    public float V3NearestToWorldCenterDistance
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.NearestToWorldCenterDistance : 0.0f;
        }
    }

    public string V3VillageDebugSummary
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.GetDebugSummary() : "V3 villages: inactive";
        }
    }

    public int V3RoadCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadCount : 0;
        }
    }

    public int V3PrimaryRoadCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.PrimaryRoadCount : 0;
        }
    }

    public int V3SecondaryRoadCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.SecondaryRoadCount : 0;
        }
    }

    public int V3ExtraRoadCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.ExtraRoadCount : 0;
        }
    }

    public int V3BranchRoadCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.BranchRoadCount : 0;
        }
    }

    public int V3RoadTargetAnchorCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadTargetAnchorCount : 0;
        }
    }

    public int V3RoadTargetQuarryCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadTargetQuarryCount : 0;
        }
    }

    public int V3RoadTargetRuinCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadTargetRuinCount : 0;
        }
    }

    public int V3RoadTargetDungeonEntranceCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadTargetDungeonEntranceCount : 0;
        }
    }

    public int V3RoadTargetBanditCampCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadTargetBanditCampCount : 0;
        }
    }

    public int V3RoadTargetFactionOutpostCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadTargetFactionOutpostCount : 0;
        }
    }

    public int V3RoadTargetForestEdgeCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadTargetForestEdgeCount : 0;
        }
    }

    public int V3RoadTargetWorldEdgeExitCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadTargetWorldEdgeExitCount : 0;
        }
    }

    public int V3FutureRoadTargetCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.FutureRoadTargetCount : 0;
        }
    }

    public int V3RejectedRoadTargetCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedRoadTargetCount : 0;
        }
    }

    public int V3RejectedBranchRoadCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedBranchRoadCount : 0;
        }
    }

    public int V3RoadNodeCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadNodeCount : 0;
        }
    }

    public int V3RoadJunctionCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadJunctionCount : 0;
        }
    }

    public int V3SharedTrunkCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.SharedTrunkCount : 0;
        }
    }

    public int V3MergedRoadCandidateCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.MergedRoadCandidateCount : 0;
        }
    }

    public int V3RejectedRoadJunctionCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedRoadJunctionCount : 0;
        }
    }

    public int V3MaxRoadJunctionDegree
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.MaxRoadJunctionDegree : 0;
        }
    }

    public int V3RejectedHighDegreeJunctionCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedHighDegreeJunctionCount : 0;
        }
    }

    public int V3RejectedRoadCrossingCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedRoadCrossingCount : 0;
        }
    }

    public int V3RejectedRoadTooLongCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedRoadTooLongCount : 0;
        }
    }

    public bool V3RoadLayerEnabled => _generationRequest.PlanVersion == WorldPlanVersionV2.V3 && _flatlandPlanV3.RoadLayerEnabled;

    public int V3ForestClusterCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.ForestClusterCount : 0;
        }
    }

    public int V3ForestRegionCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.ForestRegionCount : 0;
        }
    }

    public int V3MajorForestRegionCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.MajorForestRegionCount : 0;
        }
    }

    public int V3MinorForestPatchCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.MinorForestPatchCount : 0;
        }
    }

    public int V3LargeForestClusterCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.LargeForestClusterCount : 0;
        }
    }

    public int V3RejectedForestPlacementCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedForestPlacementCount : 0;
        }
    }

    public string V3ForestBiomeDistribution
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.ForestBiomeDistribution : "P/F/R/D/W=0/0/0/0/0";
        }
    }

    public bool V3ForestLayerEnabled => _generationRequest.PlanVersion == WorldPlanVersionV2.V3 && _flatlandPlanV3.ForestLayerEnabled;

    public int V3QuarryClusterCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.QuarryClusterCount : 0;
        }
    }

    public int V3QuarryRegionCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.QuarryRegionCount : 0;
        }
    }

    public int V3MajorQuarryCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.MajorQuarryCount : 0;
        }
    }

    public int V3MinorQuarryCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.MinorQuarryCount : 0;
        }
    }

    public int V3RejectedQuarryPlacementCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedQuarryPlacementCount : 0;
        }
    }

    public string V3QuarryBiomeDistribution
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.QuarryBiomeDistribution : "P/F/R/D/W=0/0/0/0/0";
        }
    }

    public bool V3QuarryLayerEnabled => _generationRequest.PlanVersion == WorldPlanVersionV2.V3 && _flatlandPlanV3.QuarryLayerEnabled;

    public int V3RuinSiteCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RuinSiteCount : 0;
        }
    }

    public int V3RoadLinkedRuinCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadLinkedRuinCount : 0;
        }
    }

    public int V3RejectedRuinPlacementCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedRuinPlacementCount : 0;
        }
    }

    public string V3RuinBiomeDistribution
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RuinBiomeDistribution : "P/F/R/D/W=0/0/0/0/0";
        }
    }

    public bool V3RuinLayerEnabled => _generationRequest.PlanVersion == WorldPlanVersionV2.V3 && _flatlandPlanV3.RuinLayerEnabled;

    public int V3DungeonEntranceCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.DungeonEntranceCount : 0;
        }
    }

    public int V3RoadLinkedDungeonEntranceCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadLinkedDungeonEntranceCount : 0;
        }
    }

    public int V3RejectedDungeonEntrancePlacementCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedDungeonEntrancePlacementCount : 0;
        }
    }

    public string V3DungeonEntranceKindDistribution
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.DungeonEntranceKindDistribution : "cave/gate/stair/sink/mine=0/0/0/0/0";
        }
    }

    public string V3DungeonEntranceBiomeDistribution
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.DungeonEntranceBiomeDistribution : "P/F/R/D/W=0/0/0/0/0";
        }
    }

    public bool V3DungeonLayerEnabled => _generationRequest.PlanVersion == WorldPlanVersionV2.V3 && _flatlandPlanV3.DungeonLayerEnabled;

    public int V3BanditCampCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.BanditCampCount : 0;
        }
    }

    public int V3RoadLinkedBanditCampCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadLinkedBanditCampCount : 0;
        }
    }

    public int V3RejectedBanditCampPlacementCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedBanditCampPlacementCount : 0;
        }
    }

    public string V3BanditCampKindDistribution
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.BanditCampKindDistribution : "hidden/road/ruin/forest/waste=0/0/0/0/0";
        }
    }

    public string V3BanditCampBiomeDistribution
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.BanditCampBiomeDistribution : "P/F/R/D/W=0/0/0/0/0";
        }
    }

    public bool V3BanditLayerEnabled => _generationRequest.PlanVersion == WorldPlanVersionV2.V3 && _flatlandPlanV3.BanditLayerEnabled;

    public int V3FactionOutpostCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.FactionOutpostCount : 0;
        }
    }

    public int V3RoadLinkedFactionOutpostCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RoadLinkedFactionOutpostCount : 0;
        }
    }

    public int V3RejectedFactionOutpostPlacementCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.RejectedFactionOutpostPlacementCount : 0;
        }
    }

    public string V3FactionOutpostKindDistribution
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.FactionOutpostKindDistribution : "watch/guard/trade/border/survey=0/0/0/0/0";
        }
    }

    public string V3FactionOutpostOwnerDistribution
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.FactionOutpostOwnerDistribution : "kingdom/merchant/frontier/militia/unknown=0/0/0/0/0";
        }
    }

    public string V3FactionOutpostBiomeDistribution
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.FactionOutpostBiomeDistribution : "P/F/R/D/W=0/0/0/0/0";
        }
    }

    public bool V3FactionOutpostLayerEnabled => _generationRequest.PlanVersion == WorldPlanVersionV2.V3 && _flatlandPlanV3.FactionOutpostLayerEnabled;

    public int V3BiomeRegionCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.BiomeRegionCount : 0;
        }
    }

    public int V3MajorBiomeRegionCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.MajorBiomeRegionCount : 0;
        }
    }

    public int V3MinorBiomeRegionCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.MinorBiomeRegionCount : 0;
        }
    }

    public float V3AverageMajorBiomeRadius
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.AverageMajorBiomeRadius : 0.0f;
        }
    }

    public float V3AverageMinorBiomeRadius
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.AverageMinorBiomeRadius : 0.0f;
        }
    }

    public int V3BiomeForestLandCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.BiomeForestLandCount : 0;
        }
    }

    public int V3BiomeRockyHillsCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.BiomeRockyHillsCount : 0;
        }
    }

    public int V3BiomeDrylandCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.BiomeDrylandCount : 0;
        }
    }

    public int V3BiomeWastelandCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.BiomeWastelandCount : 0;
        }
    }

    public bool V3BiomeLayerEnabled => _generationRequest.PlanVersion == WorldPlanVersionV2.V3 && _flatlandPlanV3.BiomeLayerEnabled;

    public string V3BiomeResolveMode => _generationRequest.PlanVersion == WorldPlanVersionV2.V3
        ? _flatlandPlanV3.BiomeResolveMode
        : "Inactive";

    public IReadOnlyList<VillageSiteV2> GetV3Villages()
    {
        EnsureNoiseProfile(_generationRequest.Seed);
        return _generationRequest.PlanVersion == WorldPlanVersionV2.V3
            ? _flatlandPlanV3.Villages
            : System.Array.Empty<VillageSiteV2>();
    }

    public IReadOnlyList<RoadPathV2> GetV3Roads()
    {
        EnsureNoiseProfile(_generationRequest.Seed);
        return _generationRequest.PlanVersion == WorldPlanVersionV2.V3
            ? _flatlandPlanV3.Roads
            : System.Array.Empty<RoadPathV2>();
    }

    public IReadOnlyList<ForestRegionV3> GetV3ForestRegions()
    {
        EnsureNoiseProfile(_generationRequest.Seed);
        return _generationRequest.PlanVersion == WorldPlanVersionV2.V3
            ? _flatlandPlanV3.ForestRegions
            : System.Array.Empty<ForestRegionV3>();
    }

    public IReadOnlyList<QuarryRegionV3> GetV3QuarryRegions()
    {
        EnsureNoiseProfile(_generationRequest.Seed);
        return _generationRequest.PlanVersion == WorldPlanVersionV2.V3
            ? _flatlandPlanV3.QuarryRegions
            : System.Array.Empty<QuarryRegionV3>();
    }

    public IReadOnlyList<RuinSiteV3> GetV3RuinSites()
    {
        EnsureNoiseProfile(_generationRequest.Seed);
        return _generationRequest.PlanVersion == WorldPlanVersionV2.V3
            ? _flatlandPlanV3.RuinSites
            : System.Array.Empty<RuinSiteV3>();
    }

    public IReadOnlyList<DungeonEntranceSiteV3> GetV3DungeonEntrances()
    {
        EnsureNoiseProfile(_generationRequest.Seed);
        return _generationRequest.PlanVersion == WorldPlanVersionV2.V3
            ? _flatlandPlanV3.DungeonEntrances
            : System.Array.Empty<DungeonEntranceSiteV3>();
    }

    public IReadOnlyList<BanditCampSiteV3> GetV3BanditCamps()
    {
        EnsureNoiseProfile(_generationRequest.Seed);
        return _generationRequest.PlanVersion == WorldPlanVersionV2.V3
            ? _flatlandPlanV3.BanditCamps
            : System.Array.Empty<BanditCampSiteV3>();
    }

    public IReadOnlyList<FactionOutpostSiteV3> GetV3FactionOutposts()
    {
        EnsureNoiseProfile(_generationRequest.Seed);
        return _generationRequest.PlanVersion == WorldPlanVersionV2.V3
            ? _flatlandPlanV3.FactionOutposts
            : System.Array.Empty<FactionOutpostSiteV3>();
    }

    public IReadOnlyList<BiomeRegionV3> GetV3BiomeRegions()
    {
        EnsureNoiseProfile(_generationRequest.Seed);
        return _generationRequest.PlanVersion == WorldPlanVersionV2.V3
            ? _flatlandPlanV3.BiomeRegions
            : System.Array.Empty<BiomeRegionV3>();
    }

    public static int MakeSectorSeed(int worldSeed, Vector2I sectorCoord)
    {
        uint hash = 2166136261u;
        Mix(ref hash, worldSeed);
        Mix(ref hash, sectorCoord.X);
        Mix(ref hash, sectorCoord.Y);
        return unchecked((int)(hash & 0x7fffffffu));
    }

    private CellData GenerateCell(SectorMetadata metadata, Vector2I globalCell, Vector2I localCell)
    {
        FlatlandCellSampleV2 sample = _flatlandPlan.SampleCell(globalCell);
        return CreateCellFromSample(metadata, globalCell, localCell, sample);
    }

    private static CellData CreateCellFromSample(SectorMetadata metadata, Vector2I globalCell, Vector2I localCell, FlatlandCellSampleV2 sample)
    {
        return new CellData
        {
            GlobalCellCoord = globalCell,
            GlobalChunkCoord = WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(globalCell),
            LocalCellCoord = localCell,
            SectorCoord = metadata.SectorCoord,
            ChunkCoord = ChunkCoord.FromLocalCell(localCell),
            TileType = sample.TileType,
            BiomeKind = sample.BiomeKind,
            Biome = sample.Biome,
            ResourceType = WorldResourceTypeV2.None,
            IsWater = sample.IsRiver,
            IsOcean = false,
            IsRiver = sample.IsRiver,
            IsRiverBank = sample.IsRiverBank,
            IsBridgeCandidate = sample.IsBridgeCandidate,
            IsRoad = sample.IsRoad,
            IsVillage = sample.IsVillage,
            IsStartingVillage = sample.IsStartingVillage,
            IsLandmark = sample.IsLandmark,
            LandmarkKind = sample.LandmarkKind,
            IsQuarry = sample.IsQuarry,
            HasOreSpot = sample.HasOreSpot,
            IsDungeonEntrance = sample.IsDungeonEntrance,
            DungeonEntranceKind = sample.DungeonEntranceKind,
            IsBanditCamp = sample.IsBanditCamp,
            BanditCampKind = sample.BanditCampKind,
            IsFactionOutpost = sample.IsFactionOutpost,
            FactionOutpostKind = sample.FactionOutpostKind,
            FactionOutpostOwner = sample.FactionOutpostOwner,
            ForestStrength = sample.ForestStrength,
            IsBuildRestricted = sample.IsBuildRestricted,
            IsWalkable = sample.IsWalkable,
            OwnerId = string.Empty
        };
    }

    private static SectorMetadata CreateMinimalChunkMetadata(string worldId, int worldSeed, Vector2I sectorCoord)
    {
        bool isCentralTownSector = sectorCoord == Vector2I.Zero;
        return new SectorMetadata
        {
            WorldId = worldId,
            WorldSeed = worldSeed,
            SectorCoord = sectorCoord,
            SectorSeed = MakeSectorSeed(worldSeed, sectorCoord),
            Type = isCentralTownSector ? SectorType.CentralTown : SectorType.Wasteland,
            IsDiscovered = isCentralTownSector,
            IsVisited = isCentralTownSector,
            IsCentralTown = isCentralTownSector,
            IsBuildRestricted = isCentralTownSector
        };
    }

    private SectorMetadata CreateV3Metadata(string worldId, int worldSeed, Vector2I sectorCoord)
    {
        bool containsStart = WorldV2CoordinateUtility.GlobalCellToSectorCoord(_flatlandPlanV3.PlayerSpawnCell) == sectorCoord;
        return new SectorMetadata
        {
            WorldId = worldId,
            WorldSeed = worldSeed,
            SectorCoord = sectorCoord,
            SectorSeed = MakeSectorSeed(worldSeed, sectorCoord),
            Type = containsStart ? SectorType.CentralTown : SectorType.Outskirts,
            IsDiscovered = containsStart,
            IsVisited = containsStart,
            IsCentralTown = containsStart,
            IsBuildRestricted = false,
            DominantBiome = containsStart ? BiomeTypeV2.CentralTown : BiomeTypeV2.Plains,
            SecondaryBiome = BiomeTypeV2.Plains,
            BiomeDiversityScore = 0.0f,
            DangerLevel = 0.0f,
            ResourceRichness = 0.0f,
            AverageDanger = 0.0f,
            AverageResourceRichness = 0.0f,
            AverageRuinDensity = 0.0f,
            HasStartingArea = containsStart,
            AverageStartInfluence = containsStart ? 1.0f : 0.0f,
            VillageCount = _flatlandPlanV3.VillageCount,
            RoadPathCount = _flatlandPlanV3.RoadCount
        };
    }

    private void PopulateMetadataFromLowResolutionSamples(SectorMetadata metadata)
    {
        const int sampleCountPerAxis = 8;
        Dictionary<BiomeTypeV2, int> biomeCounts = new();
        float dangerTotal = 0.0f;
        float resourceTotal = 0.0f;
        float ruinTotal = 0.0f;
        float startInfluenceTotal = 0.0f;
        int roadCount = 0;
        int sampleCount = 0;
        Vector2I sectorOrigin = metadata.SectorCoord * SectorSizeCells;

        for (int y = 0; y < sampleCountPerAxis; y++)
        {
            for (int x = 0; x < sampleCountPerAxis; x++)
            {
                Vector2I globalCell = sectorOrigin + new Vector2I(
                    x * SectorSizeCells / sampleCountPerAxis + SectorSizeCells / (sampleCountPerAxis * 2),
                    y * SectorSizeCells / sampleCountPerAxis + SectorSizeCells / (sampleCountPerAxis * 2));
                FlatlandCellSampleV2 sample = _flatlandPlan.SampleCell(globalCell);
                biomeCounts.TryGetValue(sample.Biome, out int count);
                biomeCounts[sample.Biome] = count + 1;
                dangerTotal += sample.LandmarkKind is LandmarkKindV2.BanditCamp or LandmarkKindV2.Dungeon ? 0.55f : 0.08f;
                resourceTotal += sample.IsQuarry || sample.HasOreSpot ? 0.75f : sample.ForestStrength * 0.35f;
                ruinTotal += sample.LandmarkKind is LandmarkKindV2.Ruin or LandmarkKindV2.Dungeon ? 0.65f : 0.0f;
                startInfluenceTotal += sample.IsStartingVillage ? 1.0f : 0.0f;
                metadata.HasOcean = false;
                metadata.HasRiver |= sample.IsRiver;
                metadata.HasRoad |= sample.IsRoad;
                metadata.HasStartingArea |= sample.IsStartingVillage;
                if (sample.IsRoad)
                {
                    roadCount++;
                }

                sampleCount++;
            }
        }

        (BiomeTypeV2 dominantBiome, BiomeTypeV2 secondaryBiome, int dominantCount) = GetBiomeStats(biomeCounts);
        metadata.DominantBiome = dominantBiome;
        metadata.SecondaryBiome = secondaryBiome;
        metadata.BiomeDiversityScore = sampleCount > 0 ? 1.0f - dominantCount / (float)sampleCount : 0.0f;
        metadata.AverageDanger = sampleCount > 0 ? dangerTotal / sampleCount : 0.0f;
        metadata.AverageResourceRichness = sampleCount > 0 ? resourceTotal / sampleCount : 0.0f;
        metadata.AverageRuinDensity = sampleCount > 0 ? ruinTotal / sampleCount : 0.0f;
        metadata.AverageStartInfluence = sampleCount > 0 ? startInfluenceTotal / sampleCount : 0.0f;
        metadata.RoadCoverage = sampleCount > 0 ? roadCount / (float)sampleCount : 0.0f;

        Rect2I sectorRect = new(sectorOrigin, new Vector2I(SectorSizeCells, SectorSizeCells));
        FlatlandWorldPlanSummaryV2 summary = _flatlandPlan.GetSummaryForRect(sectorRect);
        metadata.RoadPathCount = summary.RoadPathCount;
        metadata.VillageCount = summary.VillageCount;
        metadata.LandmarkCount = summary.LandmarkCount;
        metadata.RoadConnectedLandmarkCount = summary.RoadConnectedLandmarkCount;
        metadata.QuarryCount = summary.QuarryCount;
    }

    private static (BiomeTypeV2 Dominant, BiomeTypeV2 Secondary, int DominantCount) GetBiomeStats(Dictionary<BiomeTypeV2, int> biomeCounts)
    {
        BiomeTypeV2 bestBiome = BiomeTypeV2.Plains;
        BiomeTypeV2 secondBiome = BiomeTypeV2.Plains;
        int bestCount = -1;
        int secondCount = -1;

        foreach (KeyValuePair<BiomeTypeV2, int> entry in biomeCounts)
        {
            if (entry.Value > bestCount)
            {
                secondBiome = bestBiome;
                secondCount = bestCount;
                bestBiome = entry.Key;
                bestCount = entry.Value;
                continue;
            }

            if (entry.Value > secondCount)
            {
                secondBiome = entry.Key;
                secondCount = entry.Value;
            }
        }

        return (bestBiome, secondBiome, Mathf.Max(bestCount, 0));
    }

    private static SectorType MapDominantBiomeToSectorType(BiomeTypeV2 biome)
    {
        return biome switch
        {
            BiomeTypeV2.CentralTown => SectorType.CentralTown,
            BiomeTypeV2.RuinedResidential => SectorType.RuinedResidential,
            BiomeTypeV2.RuinedFactory => SectorType.RuinedFactory,
            BiomeTypeV2.Forest or BiomeTypeV2.DenseForest => SectorType.ForestEdge,
            BiomeTypeV2.QuarryField or BiomeTypeV2.Hills or BiomeTypeV2.Mountain => SectorType.Quarry,
            BiomeTypeV2.BanditTerritory => SectorType.BanditTerritory,
            BiomeTypeV2.MonsterNestArea => SectorType.MonsterNestArea,
            BiomeTypeV2.TradeRoadZone => SectorType.TradeRoad,
            BiomeTypeV2.DryWasteland or BiomeTypeV2.ToxicWasteland or BiomeTypeV2.Desert => SectorType.Wasteland,
            _ => SectorType.Outskirts
        };
    }

    private static TileType ResolveTileType(WorldClimateSampleV2 sample)
    {
        if (sample.IsRoad)
        {
            return TileType.Road;
        }

        if (sample.IsInsideTownCore || sample.Biome == BiomeTypeV2.CentralTown)
        {
            return GetStartingTownTileType(sample);
        }

        TileType biomeTile = ResolveBiomeTile(sample.Biome);
        if (sample.TownInfluence > 0.0f)
        {
            return BlendStartingAreaTile(sample, biomeTile);
        }

        return biomeTile;
    }

    private static TileType ResolveBiomeTile(BiomeTypeV2 biome)
    {
        return biome switch
        {
            BiomeTypeV2.Ocean or BiomeTypeV2.River => TileType.Water,
            BiomeTypeV2.Coast => TileType.Coast,
            BiomeTypeV2.Forest => TileType.ForestGround,
            BiomeTypeV2.DenseForest => TileType.DenseForest,
            BiomeTypeV2.Swamp => TileType.Swamp,
            BiomeTypeV2.Desert => TileType.Desert,
            BiomeTypeV2.DryWasteland => TileType.Wasteland,
            BiomeTypeV2.ColdWasteland or BiomeTypeV2.Snowfield => TileType.Snow,
            BiomeTypeV2.Hills => TileType.Hills,
            BiomeTypeV2.Mountain or BiomeTypeV2.QuarryField => TileType.Mountain,
            BiomeTypeV2.RuinedResidential => TileType.Rubble,
            BiomeTypeV2.RuinedFactory => TileType.FactoryFloor,
            BiomeTypeV2.BanditTerritory => TileType.Rubble,
            BiomeTypeV2.MonsterNestArea => TileType.Toxic,
            BiomeTypeV2.ToxicWasteland => TileType.Toxic,
            BiomeTypeV2.TradeRoadZone => TileType.Dirt,
            _ => TileType.Grass
        };
    }

    private static TileType BlendStartingAreaTile(WorldClimateSampleV2 sample, TileType biomeTile)
    {
        float texture = StableUnitFloat(sample.GlobalCell.X, sample.GlobalCell.Y, 2301);

        if (sample.IsInsideTownInner)
        {
            if (texture < sample.TownInfluence * 0.42f)
            {
                return TileType.TownPavement;
            }

            if (texture < sample.TownInfluence * 0.78f)
            {
                return TileType.Dirt;
            }
        }
        else if (texture < sample.TownInfluence * 0.42f)
        {
            return TileType.Dirt;
        }

        return biomeTile;
    }

    private static TileType GetStartingTownTileType(WorldClimateSampleV2 sample)
    {
        float distance = sample.DistanceFromStart;
        float texture = StableUnitFloat(sample.GlobalCell.X, sample.GlobalCell.Y, 2311);

        if (distance < 15.0f)
        {
            return TileType.Plaza;
        }

        if (sample.IsInsideTownCore)
        {
            if (texture < 0.58f)
            {
                return TileType.TownPavement;
            }

            return texture < 0.82f ? TileType.Dirt : TileType.Grass;
        }

        if (texture < sample.TownInfluence * 0.50f)
        {
            return TileType.TownPavement;
        }

        return texture < sample.TownInfluence * 0.82f ? TileType.Dirt : TileType.Grass;
    }

    private static WorldResourceTypeV2 PickResource(WorldClimateSampleV2 sample, Vector2I globalCell)
    {
        if (sample.IsBuildRestricted || sample.IsRoad)
        {
            return WorldResourceTypeV2.None;
        }

        float chance = GetResourceChance(sample) * Mathf.Lerp(0.60f, 1.75f, sample.ResourceRichness);
        if (StableUnitFloat(sample.GlobalCell.X, sample.GlobalCell.Y, 9001) > chance)
        {
            return WorldResourceTypeV2.None;
        }

        float roll = StableUnitFloat(globalCell.X, globalCell.Y, 9011);
        return sample.Biome switch
        {
            BiomeTypeV2.Forest => roll < 0.72f ? WorldResourceTypeV2.Wood : WorldResourceTypeV2.Herb,
            BiomeTypeV2.DenseForest => roll < 0.82f ? WorldResourceTypeV2.Wood : WorldResourceTypeV2.Herb,
            BiomeTypeV2.Swamp => roll < 0.54f ? WorldResourceTypeV2.Herb : WorldResourceTypeV2.Wood,
            BiomeTypeV2.Hills => roll < 0.56f ? WorldResourceTypeV2.Stone : WorldResourceTypeV2.IronOre,
            BiomeTypeV2.Mountain or BiomeTypeV2.QuarryField => roll < 0.50f ? WorldResourceTypeV2.Stone : roll < 0.78f ? WorldResourceTypeV2.IronOre : WorldResourceTypeV2.Coal,
            BiomeTypeV2.RuinedFactory => roll < 0.46f ? WorldResourceTypeV2.Scrap : roll < 0.72f ? WorldResourceTypeV2.IronOre : WorldResourceTypeV2.Coal,
            BiomeTypeV2.RuinedResidential => roll < 0.48f ? WorldResourceTypeV2.Scrap : roll < 0.76f ? WorldResourceTypeV2.Wood : WorldResourceTypeV2.Stone,
            BiomeTypeV2.ToxicWasteland or BiomeTypeV2.MonsterNestArea => roll < 0.38f ? WorldResourceTypeV2.Coal : roll < 0.66f ? WorldResourceTypeV2.Herb : WorldResourceTypeV2.Scrap,
            BiomeTypeV2.Desert or BiomeTypeV2.DryWasteland => roll < 0.54f ? WorldResourceTypeV2.Stone : WorldResourceTypeV2.Scrap,
            _ => roll < 0.45f ? WorldResourceTypeV2.Wood : roll < 0.76f ? WorldResourceTypeV2.Stone : WorldResourceTypeV2.Herb
        };
    }

    private static float GetResourceChance(WorldClimateSampleV2 sample)
    {
        return sample.Biome switch
        {
            BiomeTypeV2.DenseForest => 0.026f,
            BiomeTypeV2.Forest => 0.020f,
            BiomeTypeV2.QuarryField => 0.030f,
            BiomeTypeV2.Mountain => 0.022f,
            BiomeTypeV2.RuinedFactory => 0.020f,
            BiomeTypeV2.RuinedResidential => 0.016f,
            BiomeTypeV2.Swamp => 0.016f,
            BiomeTypeV2.ToxicWasteland => 0.014f,
            _ => 0.010f
        };
    }

    private void EnsureNoiseProfile(int worldSeed)
    {
        if (_initializedSeed == worldSeed)
        {
            return;
        }

        _noiseProfile.Initialize(worldSeed, _settings);
        _flatlandPlan.Initialize(worldSeed, _settings);
        _flatlandPlanV3.Initialize(_generationRequest, _settings);
        _flatlandPlanV3.BuildPlan();
        _initializedSeed = worldSeed;
    }

    private static float StableUnitFloat(int x, int y, int salt)
    {
        uint hash = 2166136261u;
        Mix(ref hash, x);
        Mix(ref hash, y);
        Mix(ref hash, salt);
        return (hash & 0x00ffffffu) / 16777215.0f;
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
