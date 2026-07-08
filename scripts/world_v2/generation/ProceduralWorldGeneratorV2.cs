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

    public int V3ExtraRoadCount
    {
        get
        {
            EnsureNoiseProfile(_generationRequest.Seed);
            return _generationRequest.PlanVersion == WorldPlanVersionV2.V3 ? _flatlandPlanV3.ExtraRoadCount : 0;
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

    public bool V3ForestLayerEnabled => _generationRequest.PlanVersion == WorldPlanVersionV2.V3 && _flatlandPlanV3.ForestLayerEnabled;

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
