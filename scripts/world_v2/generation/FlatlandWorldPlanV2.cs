using System.Collections.Generic;
using Godot;

namespace WorldV2;

public sealed class FlatlandWorldPlanV2
{
    public const int RegionSizeCells = 256;
    private const int RiverBandHeightCells = 1024;
    private const int RoadStepCells = 8;
    private const float RiverSpacingCells = 760.0f;

    public static bool DisableVillageRaster { get; set; }
    public static bool DisableSiteRaster { get; set; }
    public static bool DisableRoadRaster { get; set; }
    public static bool DisableForestRaster { get; set; }
    public static bool DisableVillageContext { get; set; }
    public static bool DisableSiteContext { get; set; }
    public static bool DisableRoadContext { get; set; }
    public static bool DisableForestContext { get; set; }

    private readonly Dictionary<Vector2I, RegionPlan> _regionPlans = new();
    private readonly Dictionary<Vector2I, IReadOnlyList<RiverPathV2>> _riverBands = new();
    private readonly Dictionary<Vector2I, IReadOnlyList<RoadPathV2>> _roadsByRegion = new();
    private readonly HashSet<Vector2I> _roadsGeneratedRegions = new();
    private int _siteOverlapResolvedCount;
    private int _rejectedSitePlacementCount;
    private int _roadCacheMissingRegionCount;

    private int _worldSeed;
    private WorldGenerationSettingsV2 _settings = WorldGenerationSettingsV2.Default;

    public void Initialize(int worldSeed, WorldGenerationSettingsV2? settings)
    {
        settings ??= WorldGenerationSettingsV2.Default;
        if (_worldSeed == worldSeed && ReferenceEquals(_settings, settings))
        {
            return;
        }

        _worldSeed = worldSeed;
        _settings = settings;
        _regionPlans.Clear();
        _riverBands.Clear();
        _roadsByRegion.Clear();
        _roadsGeneratedRegions.Clear();
        _siteOverlapResolvedCount = 0;
        _rejectedSitePlacementCount = 0;
        _roadCacheMissingRegionCount = 0;
    }

    public void ClearCaches()
    {
        _regionPlans.Clear();
        _riverBands.Clear();
        _roadsByRegion.Clear();
        _roadsGeneratedRegions.Clear();
        _siteOverlapResolvedCount = 0;
        _rejectedSitePlacementCount = 0;
        _roadCacheMissingRegionCount = 0;
    }

    public string GetCacheSummary()
    {
        return $"flatland plan cache: regions={_regionPlans.Count} riverBands={_riverBands.Count} roadReadyRegions={_roadsByRegion.Count} generatedRoadRegions={_roadsGeneratedRegions.Count} roadMissing={_roadCacheMissingRegionCount} siteOverlapResolved={_siteOverlapResolvedCount} rejectedSites={_rejectedSitePlacementCount} {WorldGenerationLayerSettingsV2.GetSummary()}";
    }

    public static string GetDebugToggleSummary()
    {
        return $"toggles: villageRaster={DisableVillageRaster} siteRaster={DisableSiteRaster} roadRaster={DisableRoadRaster} forestRaster={DisableForestRaster} villageCtx={DisableVillageContext} siteCtx={DisableSiteContext} roadCtx={DisableRoadContext} forestCtx={DisableForestContext}";
    }

    public void PrewarmRoadCacheForChunk(Vector2I globalChunkCoord)
    {
        if (!WorldGenerationLayerSettingsV2.EnableRoads || !WorldGenerationLayerSettingsV2.EnableVillages)
        {
            return;
        }

        Vector2I originGlobalCell = WorldV2CoordinateUtility.GlobalChunkToOriginGlobalCell(globalChunkCoord);
        Rect2I chunkBounds = new(originGlobalCell, new Vector2I(ChunkDataV2.ChunkSize, ChunkDataV2.ChunkSize));
        foreach (Vector2I region in GetRegionsForRect(chunkBounds, 1))
        {
            GetRoadsForRegion(region);
        }
    }

    public FlatlandCellSampleV2 SampleCell(Vector2I globalCell)
    {
        float riverDistance = WorldGenerationLayerSettingsV2.EnableRivers ? GetRiverDistance(globalCell) : float.MaxValue;
        bool isRiver = riverDistance <= _settings.RiverWidth;
        bool isRiverBank = !isRiver && riverDistance <= _settings.RiverWidth + _settings.RiverBankWidth;
        float forestStrength = WorldGenerationLayerSettingsV2.EnableForests ? GetForestStrength(globalCell, riverDistance) : 0.0f;
        LandmarkHit siteHit = GetLandmarkHit(globalCell);
        RoadHit roadHit = WorldGenerationLayerSettingsV2.EnableRoads ? GetRoadHit(globalCell) : RoadHit.None;
        bool bridgeCandidate = roadHit.IsRoad && isRiver;
        bool hasOreSpot = siteHit.Kind == LandmarkKindV2.Quarry
            && siteHit.Distance <= Mathf.Max(2.0f, siteHit.Radius * 0.72f)
            && HashUnit(globalCell.X, globalCell.Y, 557) > 0.62f;

        FlatlandCellSampleV2 sample = new()
        {
            GlobalCellCoord = globalCell,
            IsRiver = isRiver,
            IsRiverBank = isRiverBank,
            IsBridgeCandidate = bridgeCandidate,
            IsRoad = roadHit.IsRoad && !isRiver,
            IsVillage = siteHit.Kind is LandmarkKindV2.Village or LandmarkKindV2.StartingVillage,
            IsStartingVillage = siteHit.Kind == LandmarkKindV2.StartingVillage,
            IsLandmark = siteHit.Kind is not LandmarkKindV2.None and not LandmarkKindV2.Village and not LandmarkKindV2.StartingVillage,
            LandmarkKind = siteHit.Kind,
            IsQuarry = siteHit.Kind == LandmarkKindV2.Quarry,
            HasOreSpot = hasOreSpot,
            ForestStrength = forestStrength
        };

        ResolveSampleTile(sample, siteHit, forestStrength);
        return sample;
    }

    public FlatlandChunkGenerationContextV2 BuildChunkContext(Vector2I globalChunkCoord)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        Vector2I originGlobalCell = WorldV2CoordinateUtility.GlobalChunkToOriginGlobalCell(globalChunkCoord);
        FlatlandChunkGenerationContextV2 context = new(globalChunkCoord, originGlobalCell);
        int searchMargin = Mathf.CeilToInt(Mathf.Max(96.0f, Mathf.Max(_settings.ForestClusterMaxLength, _settings.ForestClusterMaxWidth) + 48.0f));
        Rect2I searchRect = ExpandRect(context.GlobalCellBounds, searchMargin);
        Rect2 contextRect = ToRect2(context.GlobalCellBounds);

        foreach (Vector2I region in GetRegionsForRect(searchRect, 0))
        {
            long regionEnsureStart = profiler.BeginSample();
            RegionPlan plan = GetRegionPlan(region);
            profiler.EndSample(WorldV2PerformanceProfiler.ContextRegionPlanEnsure, regionEnsureStart, globalChunkCoord);

            if (!DisableVillageContext)
            {
                long villageStart = profiler.BeginSample();
                if (WorldGenerationLayerSettingsV2.EnableVillages)
                {
                    AddVillageIfRelevant(context, plan.Village, contextRect);
                }
                profiler.EndSample(WorldV2PerformanceProfiler.ContextVillageQuery, villageStart, globalChunkCoord);
            }

            if (!DisableSiteContext)
            {
                long landmarkStart = profiler.BeginSample();
                foreach (LandmarkSiteV2 landmark in plan.Landmarks)
                {
                    if (WorldGenerationLayerSettingsV2.IsLandmarkEnabled(landmark.Kind))
                    {
                        AddLandmarkIfRelevant(context, landmark, contextRect);
                    }
                }
                profiler.EndSample(WorldV2PerformanceProfiler.ContextLandmarkQuery, landmarkStart, globalChunkCoord);
            }

            if (!DisableForestContext && WorldGenerationLayerSettingsV2.EnableForests)
            {
                long forestStart = profiler.BeginSample();
                foreach (ForestClusterSiteV2 cluster in plan.ForestClusters)
                {
                    AddForestIfRelevant(context, cluster, contextRect);
                }
                profiler.EndSample(WorldV2PerformanceProfiler.ContextForestQuery, forestStart, globalChunkCoord);
            }
        }

        if (!DisableRoadContext && WorldGenerationLayerSettingsV2.EnableRoads && WorldGenerationLayerSettingsV2.EnableVillages)
        {
            long roadStart = profiler.BeginSample();
            Rect2 roadQueryRect = GrowRect(contextRect, Mathf.Max(3.0f, _settings.RoadWidth + 2.0f));
            foreach (Vector2I region in GetRegionsForRect(context.GlobalCellBounds, 1))
            {
                long roadCacheStart = profiler.BeginSample();
                IReadOnlyList<RoadPathV2> roads = TryGetCachedRoadsForRegion(region);
                profiler.EndSample(WorldV2PerformanceProfiler.ContextRoadCacheEnsure, roadCacheStart, globalChunkCoord);
                if (roads.Count > 0)
                {
                    context.RoadCacheReadyRegionCount++;
                }
                else
                {
                    context.RoadCacheMissingRegionCount++;
                    _roadCacheMissingRegionCount++;
                }

                foreach (RoadPathV2 road in roads)
                {
                    if (GrowRect(road.Bounds, Mathf.Max(2.0f, road.Width + 1.0f)).Intersects(roadQueryRect))
                    {
                        context.AddRoad(road);
                    }
                }
            }
            profiler.EndSample(WorldV2PerformanceProfiler.ContextRoadQuery, roadStart, globalChunkCoord);
        }

        if (WorldGenerationLayerSettingsV2.EnableRivers)
        {
            long riverStart = profiler.BeginSample();
            foreach (RiverPathV2 river in GetRiversForRect(ExpandRect(context.GlobalCellBounds, Mathf.CeilToInt(_settings.RiverWidth + _settings.RiverBankWidth + 96.0f))))
            {
                context.AddRiver(river);
            }
            profiler.EndSample(WorldV2PerformanceProfiler.ContextRiverQuery, riverStart, globalChunkCoord);
        }

        context.NearestVillageDistance = CalculateNearestVillageDistance(context);
        context.SiteOverlapResolvedCount = _siteOverlapResolvedCount;
        context.RejectedSitePlacementCount = _rejectedSitePlacementCount;

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandChunkContextBuild, start, globalChunkCoord);
        return context;
    }

    public void BuildChunkRaster(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        RasterBaseGround(context);
        RasterSites(context);
        if (WorldGenerationLayerSettingsV2.EnableRivers)
        {
            RasterRivers(context);
        }
        if (!DisableRoadRaster)
        {
            RasterRoads(context);
        }

        if (!DisableForestRaster)
        {
            RasterForests(context);
        }

        for (int i = 0; i < ChunkDataV2.CellCount; i++)
        {
            context.IsBridgeCandidate[i] = context.IsRoad[i] && context.IsRiver[i];
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandChunkRasterBuild, start, context.ChunkCoord);
    }

    public FlatlandCellSampleV2 SampleCellFast(Vector2I globalCell, int localX, int localY, FlatlandChunkGenerationContextV2 context)
    {
        int index = FlatlandChunkGenerationContextV2.ToIndex(localX, localY);
        LandmarkKindV2 kind = context.LandmarkKind[index];
        LandmarkHit siteHit = kind == LandmarkKindV2.None
            ? LandmarkHit.None
            : new LandmarkHit(0, kind, context.SiteDistance[index], context.SiteRadius[index]);

        FlatlandCellSampleV2 sample = new()
        {
            GlobalCellCoord = globalCell,
            IsRiver = context.IsRiver[index],
            IsRiverBank = context.IsRiverBank[index],
            IsBridgeCandidate = context.IsBridgeCandidate[index],
            IsRoad = context.IsRoad[index] && !context.IsRiver[index],
            IsVillage = context.IsVillage[index],
            IsStartingVillage = context.IsStartingVillage[index],
            IsLandmark = context.IsLandmark[index],
            LandmarkKind = kind,
            IsQuarry = context.IsQuarry[index],
            HasOreSpot = context.HasOreSpot[index],
            ForestStrength = context.ForestStrength[index]
        };

        ResolveSampleTileFast(sample, siteHit, context.ForestStrength[index], context.BaseGroundVariant[index]);
        if (sample.TileType == TileType.Dirt)
        {
            context.DirtTileCount++;
        }

        return sample;
    }

    public FlatlandWorldPlanSummaryV2 GetSummaryForRect(Rect2I rect)
    {
        HashSet<int> villageIds = new();
        HashSet<int> landmarkIds = new();
        HashSet<int> connectedLandmarkIds = new();
        HashSet<int> quarryIds = new();
        HashSet<int> roadIds = new();

        foreach (Vector2I region in GetRegionsForRect(rect, 1))
        {
            RegionPlan plan = GetRegionPlan(region);
            villageIds.Add(plan.Village.Id);

            foreach (LandmarkSiteV2 landmark in plan.Landmarks)
            {
                landmarkIds.Add(landmark.Id);
                if (landmark.Kind == LandmarkKindV2.Quarry)
                {
                    quarryIds.Add(landmark.Id);
                }

                if (landmark.ShouldConnectRoad)
                {
                    connectedLandmarkIds.Add(landmark.Id);
                }
            }

            foreach (RoadPathV2 road in GetRoadsForRegion(region))
            {
                roadIds.Add(road.Id);
            }
        }

        return new FlatlandWorldPlanSummaryV2(
            roadIds.Count,
            villageIds.Count,
            landmarkIds.Count,
            connectedLandmarkIds.Count,
            quarryIds.Count);
    }

    private void ResolveSampleTile(FlatlandCellSampleV2 sample, LandmarkHit siteHit, float forestStrength)
    {
        sample.IsWalkable = true;
        sample.IsBuildRestricted = false;
        sample.Biome = BiomeTypeV2.Plains;
        sample.TileType = TileType.Grass;

        if (sample.IsRiver)
        {
            sample.Biome = BiomeTypeV2.River;
            sample.TileType = sample.IsBridgeCandidate ? TileType.BridgeCandidate : TileType.Water;
            sample.IsWalkable = false;
            sample.IsBuildRestricted = true;
            return;
        }

        if (sample.IsBridgeCandidate)
        {
            sample.TileType = TileType.BridgeCandidate;
            return;
        }

        if (sample.IsStartingVillage)
        {
            sample.Biome = BiomeTypeV2.CentralTown;
            sample.TileType = siteHit.Distance < siteHit.Radius * 0.52f ? TileType.Plaza : TileType.Village;
            sample.IsBuildRestricted = true;
            return;
        }

        if (sample.IsVillage)
        {
            sample.TileType = siteHit.Distance < siteHit.Radius ? TileType.Village : TileType.VillageEdge;
            return;
        }

        if (sample.IsRoad)
        {
            sample.TileType = TileType.Road;
            return;
        }

        if (sample.IsLandmark)
        {
            sample.TileType = sample.LandmarkKind switch
            {
                LandmarkKindV2.Ruin => TileType.Ruin,
                LandmarkKindV2.Dungeon => TileType.Dungeon,
                LandmarkKindV2.BanditCamp => TileType.BanditCamp,
                LandmarkKindV2.FactionOutpost => TileType.FactionOutpost,
                LandmarkKindV2.Quarry => sample.HasOreSpot ? TileType.OreSpot : TileType.StoneField,
                _ => TileType.Rubble
            };
            sample.Biome = sample.LandmarkKind switch
            {
                LandmarkKindV2.Ruin => BiomeTypeV2.RuinedResidential,
                LandmarkKindV2.Dungeon => BiomeTypeV2.RuinedFactory,
                LandmarkKindV2.BanditCamp => BiomeTypeV2.BanditTerritory,
                LandmarkKindV2.FactionOutpost => BiomeTypeV2.TradeRoadZone,
                LandmarkKindV2.Quarry => BiomeTypeV2.QuarryField,
                _ => BiomeTypeV2.Plains
            };
            return;
        }

        if (sample.IsRiverBank)
        {
            sample.TileType = TileType.RiverBank;
        }

        if (siteHit.Kind == LandmarkKindV2.Quarry && siteHit.Distance <= siteHit.Radius + 7.0f)
        {
            sample.TileType = sample.HasOreSpot ? TileType.OreSpot : TileType.StoneField;
            sample.Biome = BiomeTypeV2.QuarryField;
            return;
        }

        sample.IsDenseForest = forestStrength > 0.62f;
        sample.IsForest = forestStrength > 0.34f;
        if (sample.IsDenseForest)
        {
            sample.Biome = BiomeTypeV2.DenseForest;
            sample.TileType = TileType.DenseForest;
            return;
        }

        if (sample.IsForest)
        {
            sample.Biome = BiomeTypeV2.Forest;
            sample.TileType = forestStrength > 0.48f ? TileType.ForestGround : TileType.LightForest;
            return;
        }

        if (sample.IsRiverBank)
        {
            sample.TileType = TileType.WetGrass;
        }
    }

    private void ResolveSampleTileFast(FlatlandCellSampleV2 sample, LandmarkHit siteHit, float forestStrength, byte baseGroundVariant)
    {
        sample.IsWalkable = true;
        sample.IsBuildRestricted = false;
        sample.Biome = BiomeTypeV2.Plains;
        sample.TileType = TileType.Grass;

        if (sample.IsRiver)
        {
            sample.Biome = BiomeTypeV2.River;
            sample.TileType = sample.IsBridgeCandidate ? TileType.BridgeCandidate : TileType.Water;
            sample.IsWalkable = false;
            sample.IsBuildRestricted = true;
            return;
        }

        if (sample.IsBridgeCandidate)
        {
            sample.TileType = TileType.BridgeCandidate;
            return;
        }

        if (sample.IsStartingVillage)
        {
            sample.Biome = BiomeTypeV2.CentralTown;
            sample.TileType = siteHit.Distance < siteHit.Radius * 0.52f ? TileType.Plaza : TileType.Village;
            sample.IsBuildRestricted = true;
            return;
        }

        if (sample.IsVillage)
        {
            sample.TileType = siteHit.Distance < siteHit.Radius ? TileType.Village : TileType.VillageEdge;
            return;
        }

        if (sample.IsRoad)
        {
            sample.TileType = TileType.Road;
            return;
        }

        if (sample.IsLandmark)
        {
            sample.TileType = sample.LandmarkKind switch
            {
                LandmarkKindV2.Ruin => TileType.Ruin,
                LandmarkKindV2.Dungeon => TileType.Dungeon,
                LandmarkKindV2.BanditCamp => TileType.BanditCamp,
                LandmarkKindV2.FactionOutpost => TileType.FactionOutpost,
                LandmarkKindV2.Quarry => sample.HasOreSpot ? TileType.OreSpot : TileType.StoneField,
                _ => TileType.Rubble
            };
            sample.Biome = sample.LandmarkKind switch
            {
                LandmarkKindV2.Ruin => BiomeTypeV2.RuinedResidential,
                LandmarkKindV2.Dungeon => BiomeTypeV2.RuinedFactory,
                LandmarkKindV2.BanditCamp => BiomeTypeV2.BanditTerritory,
                LandmarkKindV2.FactionOutpost => BiomeTypeV2.TradeRoadZone,
                LandmarkKindV2.Quarry => BiomeTypeV2.QuarryField,
                _ => BiomeTypeV2.Plains
            };
            return;
        }

        if (sample.IsRiverBank)
        {
            sample.TileType = TileType.RiverBank;
        }

        if (siteHit.Kind == LandmarkKindV2.Quarry && siteHit.Distance <= siteHit.Radius + 7.0f)
        {
            sample.TileType = sample.HasOreSpot ? TileType.OreSpot : TileType.StoneField;
            sample.Biome = BiomeTypeV2.QuarryField;
            return;
        }

        sample.IsDenseForest = forestStrength > 0.62f;
        sample.IsForest = forestStrength > 0.34f;
        if (sample.IsDenseForest)
        {
            sample.Biome = BiomeTypeV2.DenseForest;
            sample.TileType = TileType.DenseForest;
            return;
        }

        if (sample.IsForest)
        {
            sample.Biome = BiomeTypeV2.Forest;
            sample.TileType = forestStrength > 0.48f ? TileType.ForestGround : TileType.LightForest;
            return;
        }

        if (sample.IsRiverBank)
        {
            sample.TileType = TileType.WetGrass;
        }
    }

    private void RasterBaseGround(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        for (int y = 0; y < ChunkDataV2.ChunkSize; y++)
        {
            for (int x = 0; x < ChunkDataV2.ChunkSize; x++)
            {
                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                context.BaseGroundVariant[index] = 0;
            }
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandBaseNoise, start, context.ChunkCoord);
    }

    private void RasterSites(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        if (!DisableVillageRaster)
        {
            foreach (VillageSiteV2 village in context.RelevantVillages)
            {
                RasterVillage(context, village);
            }
        }

        if (!DisableSiteRaster)
        {
            foreach (LandmarkSiteV2 landmark in context.RelevantLandmarks)
            {
                RasterLandmark(context, landmark);
            }
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandSiteSample, start, context.ChunkCoord);

        start = profiler.BeginSample();
        for (int i = 0; i < ChunkDataV2.CellCount; i++)
        {
            if (context.LandmarkKind[i] == LandmarkKindV2.Quarry)
            {
                context.IsQuarry[i] = true;
                int localX = i % ChunkDataV2.ChunkSize;
                int localY = i / ChunkDataV2.ChunkSize;
                Vector2I cell = context.ToGlobalCell(localX, localY);
                context.HasOreSpot[i] = context.SiteDistance[i] <= Mathf.Max(2.0f, context.SiteRadius[i] * 0.72f)
                    && HashUnit(cell.X, cell.Y, 557) > 0.62f;
            }
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandQuarrySample, start, context.ChunkCoord);
    }

    private void RasterVillage(FlatlandChunkGenerationContextV2 context, VillageSiteV2 village)
    {
        float outerRadius = village.Radius + 5.0f;
        if (!TryGetLocalCircleBounds(context, new Vector2(village.Center.X, village.Center.Y), outerRadius, out int minX, out int minY, out int maxX, out int maxY))
        {
            return;
        }

        LandmarkKindV2 kind = village.IsStartingVillage ? LandmarkKindV2.StartingVillage : LandmarkKindV2.Village;
        float outerRadiusSquared = outerRadius * outerRadius;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2I cell = context.ToGlobalCell(x, y);
                float dx = cell.X - village.Center.X;
                float dy = cell.Y - village.Center.Y;
                float distanceSquared = dx * dx + dy * dy;
                if (distanceSquared > outerRadiusSquared)
                {
                    continue;
                }

                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                ApplyRasterSiteHit(context, index, kind, Mathf.Sqrt(distanceSquared), village.Radius);
            }
        }
    }

    private void RasterLandmark(FlatlandChunkGenerationContextV2 context, LandmarkSiteV2 landmark)
    {
        float outerRadius = landmark.Kind == LandmarkKindV2.Quarry ? landmark.Radius + 7.0f : landmark.Radius + 5.0f;
        if (!TryGetLocalCircleBounds(context, new Vector2(landmark.Center.X, landmark.Center.Y), outerRadius, out int minX, out int minY, out int maxX, out int maxY))
        {
            return;
        }

        float outerRadiusSquared = outerRadius * outerRadius;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2I cell = context.ToGlobalCell(x, y);
                float dx = cell.X - landmark.Center.X;
                float dy = cell.Y - landmark.Center.Y;
                float distanceSquared = dx * dx + dy * dy;
                if (distanceSquared > outerRadiusSquared)
                {
                    continue;
                }

                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                ApplyRasterSiteHit(context, index, landmark.Kind, Mathf.Sqrt(distanceSquared), landmark.Radius);
            }
        }
    }

    private static void ApplyRasterSiteHit(FlatlandChunkGenerationContextV2 context, int index, LandmarkKindV2 kind, float distance, float radius)
    {
        if (distance >= context.SiteDistance[index])
        {
            return;
        }

        context.SiteDistance[index] = distance;
        context.SiteRadius[index] = radius;
        context.LandmarkKind[index] = kind;
        context.IsVillage[index] = kind is LandmarkKindV2.Village or LandmarkKindV2.StartingVillage;
        context.IsStartingVillage[index] = kind == LandmarkKindV2.StartingVillage;
        context.IsLandmark[index] = kind is not LandmarkKindV2.None and not LandmarkKindV2.Village and not LandmarkKindV2.StartingVillage;
        context.IsQuarry[index] = kind == LandmarkKindV2.Quarry;
        context.HasVillageTile |= context.IsVillage[index];
        context.HasLandmarkTile |= context.IsLandmark[index];
    }

    private void RasterRivers(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();
        float maxDistance = _settings.RiverWidth + _settings.RiverBankWidth + 1.5f;

        foreach (RiverPathV2 river in context.RelevantRivers)
        {
            IReadOnlyList<Vector2> points = river.ControlPointsWorld;
            for (int i = 1; i < points.Count; i++)
            {
                RasterDistanceToSegment(context, points[i - 1], points[i], maxDistance, context.RiverDistance);
            }
        }

        for (int i = 0; i < ChunkDataV2.CellCount; i++)
        {
            float distance = context.RiverDistance[i];
            context.IsRiver[i] = distance <= _settings.RiverWidth;
            context.IsRiverBank[i] = !context.IsRiver[i] && distance <= _settings.RiverWidth + _settings.RiverBankWidth;
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandRiverSample, start, context.ChunkCoord);
    }

    private void RasterRoads(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        foreach (RoadPathV2 road in context.RelevantRoads)
        {
            IReadOnlyList<Vector2> points = road.PathPointsWorld;
            for (int i = 1; i < points.Count; i++)
            {
                RasterRoadSegment(context, points[i - 1], points[i], road.Width);
            }
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandRoadSample, start, context.ChunkCoord);
    }

    private void RasterForests(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        foreach (ForestClusterSiteV2 cluster in context.RelevantForestClusters)
        {
            RasterForestCluster(context, cluster);
        }

        for (int i = 0; i < ChunkDataV2.CellCount; i++)
        {
            float strength = context.ForestStrength[i];
            float riverDistance = context.RiverDistance[i];
            if (riverDistance > _settings.RiverWidth + 3.0f
                && riverDistance < _settings.RiverWidth + _settings.RiverBankWidth + 10.0f)
            {
                strength = Mathf.Clamp(strength + _settings.RiverForestBonus, 0.0f, 1.0f);
            }

            if (context.LandmarkKind[i] != LandmarkKindV2.None)
            {
                float clearingRadius = context.SiteRadius[i] + 18.0f;
                if (context.SiteDistance[i] < clearingRadius)
                {
                    float t = Mathf.Clamp(context.SiteDistance[i] / clearingRadius, 0.0f, 1.0f);
                    float softened = Mathf.Lerp(0.28f, 1.0f, t * t * (3.0f - 2.0f * t));
                    if (softened < 0.98f && strength > 0.12f)
                    {
                        context.ForestEdgeClearCount++;
                    }

                    strength *= softened;
                }
            }

            if (strength > 0.12f)
            {
                int localX = i % ChunkDataV2.ChunkSize;
                int localY = i / ChunkDataV2.ChunkSize;
                Vector2I cell = context.ToGlobalCell(localX, localY);
                float clearing = FractalNoise(cell.X, cell.Y, 34.0f, 1900, 2);
                if (clearing > 0.73f)
                {
                    strength *= _settings.ForestClearingStrength;
                }
            }

            context.ForestStrength[i] = Mathf.Clamp(strength, 0.0f, 1.0f);
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandForestSample, start, context.ChunkCoord);
    }

    private void RasterForestCluster(FlatlandChunkGenerationContextV2 context, ForestClusterSiteV2 cluster)
    {
        float range = Mathf.Max(cluster.Length, cluster.Width) * 0.78f + 28.0f;
        if (!TryGetLocalCircleBounds(context, cluster.Center, range, out int minX, out int minY, out int maxX, out int maxY))
        {
            return;
        }

        float cos = Mathf.Cos(cluster.Angle);
        float sin = Mathf.Sin(cluster.Angle);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2I cell = context.ToGlobalCell(x, y);
                Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
                Vector2 delta = point - cluster.Center;
                float localX = delta.X * cos + delta.Y * sin;
                float localY = -delta.X * sin + delta.Y * cos;
                float nx = localX / Mathf.Max(1.0f, cluster.Length * 0.55f);
                float ny = localY / Mathf.Max(1.0f, cluster.Width * 0.62f);
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float edgeNoise = (FractalNoise(cell.X + cluster.Id * 19, cell.Y - cluster.Id * 13, 46.0f, cluster.Id + 400, 2) - 0.5f)
                    * cluster.EdgeNoiseStrength;
                float belt = 1.0f + edgeNoise;
                if (distance >= belt)
                {
                    continue;
                }

                float core = Mathf.Clamp(1.0f - distance / Mathf.Max(0.01f, belt), 0.0f, 1.0f);
                float strength = Mathf.Pow(core, 0.72f) * cluster.Density;
                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                context.ForestStrength[index] = Mathf.Max(context.ForestStrength[index], strength);
            }
        }

        foreach (ForestGroveSiteV2 grove in cluster.Groves)
        {
            RasterForestGrove(context, cluster, grove);
        }
    }

    private void RasterForestGrove(FlatlandChunkGenerationContextV2 context, ForestClusterSiteV2 cluster, ForestGroveSiteV2 grove)
    {
        float maxRadius = grove.Radius + 9.0f;
        if (!TryGetLocalCircleBounds(context, grove.Center, maxRadius, out int minX, out int minY, out int maxX, out int maxY))
        {
            return;
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2I cell = context.ToGlobalCell(x, y);
                Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
                float groveRadius = grove.Radius + (FractalNoise(cell.X - cluster.Id, cell.Y + cluster.Id, 28.0f, cluster.Id + 77, 2) - 0.5f) * 9.0f;
                float distance = point.DistanceTo(grove.Center);
                if (distance >= groveRadius)
                {
                    continue;
                }

                float t = 1.0f - distance / Mathf.Max(1.0f, groveRadius);
                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                context.ForestStrength[index] = Mathf.Max(context.ForestStrength[index], Mathf.Pow(t, 0.85f) * grove.Density);
            }
        }
    }

    private void RasterDistanceToSegment(FlatlandChunkGenerationContextV2 context, Vector2 a, Vector2 b, float maxDistance, float[] destination)
    {
        if (!TryGetLocalSegmentBounds(context, a, b, maxDistance, out int minX, out int minY, out int maxX, out int maxY))
        {
            return;
        }

        float maxDistanceSquared = maxDistance * maxDistance;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2I cell = context.ToGlobalCell(x, y);
                Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
                float distanceSquared = DistanceToSegmentSquared(point, a, b);
                if (distanceSquared > maxDistanceSquared)
                {
                    continue;
                }

                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                float distance = Mathf.Sqrt(distanceSquared);
                if (distance < destination[index])
                {
                    destination[index] = distance;
                }
            }
        }
    }

    private void RasterRoadSegment(FlatlandChunkGenerationContextV2 context, Vector2 a, Vector2 b, float width)
    {
        float maxDistance = width + 0.5f;
        if (!TryGetLocalSegmentBounds(context, a, b, maxDistance, out int minX, out int minY, out int maxX, out int maxY))
        {
            return;
        }

        float maxDistanceSquared = width * width;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2I cell = context.ToGlobalCell(x, y);
                Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
                if (DistanceToSegmentSquared(point, a, b) > maxDistanceSquared)
                {
                    continue;
                }

                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                context.IsRoad[index] = true;
                context.HasRoadTile = true;
            }
        }
    }

    private static float CalculateNearestVillageDistance(FlatlandChunkGenerationContextV2 context)
    {
        if (context.RelevantVillages.Count == 0)
        {
            return float.MaxValue;
        }

        Vector2 chunkCenter = new(
            context.OriginGlobalCell.X + ChunkDataV2.ChunkSize * 0.5f,
            context.OriginGlobalCell.Y + ChunkDataV2.ChunkSize * 0.5f);
        float bestSquared = float.MaxValue;
        foreach (VillageSiteV2 village in context.RelevantVillages)
        {
            float dx = chunkCenter.X - village.Center.X;
            float dy = chunkCenter.Y - village.Center.Y;
            float distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < bestSquared)
            {
                bestSquared = distanceSquared;
            }
        }

        return Mathf.Sqrt(bestSquared);
    }

    private static void AddVillageIfRelevant(FlatlandChunkGenerationContextV2 context, VillageSiteV2 village, Rect2 contextRect)
    {
        float radius = village.Radius + 5.0f;
        if (CircleIntersectsRect(village.Center, radius, contextRect))
        {
            context.AddVillage(village);
        }
    }

    private static void AddLandmarkIfRelevant(FlatlandChunkGenerationContextV2 context, LandmarkSiteV2 landmark, Rect2 contextRect)
    {
        float radius = landmark.Kind == LandmarkKindV2.Quarry ? landmark.Radius + 7.0f : landmark.Radius + 5.0f;
        if (CircleIntersectsRect(landmark.Center, radius, contextRect))
        {
            context.AddLandmark(landmark);
        }
    }

    private static void AddForestIfRelevant(FlatlandChunkGenerationContextV2 context, ForestClusterSiteV2 cluster, Rect2 contextRect)
    {
        float radius = Mathf.Max(cluster.Length, cluster.Width) * 0.78f + 28.0f;
        if (CircleIntersectsRect(cluster.Center, radius, contextRect))
        {
            context.AddForestCluster(cluster);
        }
    }

    private static bool CircleIntersectsRect(Vector2I center, float radius, Rect2 rect)
    {
        return CircleIntersectsRect(new Vector2(center.X, center.Y), radius, rect);
    }

    private static bool CircleIntersectsRect(Vector2 center, float radius, Rect2 rect)
    {
        float closestX = Mathf.Clamp(center.X, rect.Position.X, rect.Position.X + rect.Size.X);
        float closestY = Mathf.Clamp(center.Y, rect.Position.Y, rect.Position.Y + rect.Size.Y);
        float dx = center.X - closestX;
        float dy = center.Y - closestY;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static Rect2 ToRect2(Rect2I rect)
    {
        return new Rect2(rect.Position, rect.Size);
    }

    private static Rect2 GrowRect(Rect2 rect, float amount)
    {
        return new Rect2(
            rect.Position - new Vector2(amount, amount),
            rect.Size + new Vector2(amount * 2.0f, amount * 2.0f));
    }

    private IEnumerable<RiverPathV2> GetRiversForRect(Rect2I rect)
    {
        int minRiverIndex = Mathf.FloorToInt((rect.Position.X - RiverSpacingCells) / RiverSpacingCells) - 1;
        int maxRiverIndex = Mathf.CeilToInt((rect.End.X + RiverSpacingCells) / RiverSpacingCells) + 1;
        int minBand = FloorDiv(rect.Position.Y - RiverBandHeightCells / 2, RiverBandHeightCells) - 1;
        int maxBand = FloorDiv(rect.End.Y + RiverBandHeightCells / 2, RiverBandHeightCells) + 1;

        for (int riverIndex = minRiverIndex; riverIndex <= maxRiverIndex; riverIndex++)
        {
            if (!IsRiverIndexActive(riverIndex))
            {
                continue;
            }

            for (int band = minBand; band <= maxBand; band++)
            {
                foreach (RiverPathV2 river in GetRiverBand(riverIndex, band))
                {
                    yield return river;
                }
            }
        }
    }

    private static Rect2I ExpandRect(Rect2I rect, int margin)
    {
        return new Rect2I(rect.Position - new Vector2I(margin, margin), rect.Size + new Vector2I(margin * 2, margin * 2));
    }

    private static bool TryGetLocalCircleBounds(FlatlandChunkGenerationContextV2 context, Vector2 center, float radius, out int minX, out int minY, out int maxX, out int maxY)
    {
        return TryGetLocalRange(
            context,
            Mathf.FloorToInt(center.X - radius),
            Mathf.FloorToInt(center.Y - radius),
            Mathf.CeilToInt(center.X + radius),
            Mathf.CeilToInt(center.Y + radius),
            out minX,
            out minY,
            out maxX,
            out maxY);
    }

    private static bool TryGetLocalSegmentBounds(FlatlandChunkGenerationContextV2 context, Vector2 a, Vector2 b, float margin, out int minX, out int minY, out int maxX, out int maxY)
    {
        return TryGetLocalRange(
            context,
            Mathf.FloorToInt(Mathf.Min(a.X, b.X) - margin),
            Mathf.FloorToInt(Mathf.Min(a.Y, b.Y) - margin),
            Mathf.CeilToInt(Mathf.Max(a.X, b.X) + margin),
            Mathf.CeilToInt(Mathf.Max(a.Y, b.Y) + margin),
            out minX,
            out minY,
            out maxX,
            out maxY);
    }

    private static bool TryGetLocalRange(FlatlandChunkGenerationContextV2 context, int minGlobalX, int minGlobalY, int maxGlobalX, int maxGlobalY, out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = Mathf.Clamp(minGlobalX - context.OriginGlobalCell.X, 0, ChunkDataV2.ChunkSize - 1);
        minY = Mathf.Clamp(minGlobalY - context.OriginGlobalCell.Y, 0, ChunkDataV2.ChunkSize - 1);
        maxX = Mathf.Clamp(maxGlobalX - context.OriginGlobalCell.X, 0, ChunkDataV2.ChunkSize - 1);
        maxY = Mathf.Clamp(maxGlobalY - context.OriginGlobalCell.Y, 0, ChunkDataV2.ChunkSize - 1);
        return maxGlobalX >= context.OriginGlobalCell.X
            && maxGlobalY >= context.OriginGlobalCell.Y
            && minGlobalX < context.OriginGlobalCell.X + ChunkDataV2.ChunkSize
            && minGlobalY < context.OriginGlobalCell.Y + ChunkDataV2.ChunkSize
            && minX <= maxX
            && minY <= maxY;
    }

    private static float DistanceToSegmentSquared(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return point.DistanceSquaredTo(a);
        }

        float t = Mathf.Clamp((point - a).Dot(ab) / lengthSquared, 0.0f, 1.0f);
        return point.DistanceSquaredTo(a + ab * t);
    }

    private LandmarkHit GetLandmarkHit(Vector2I cell)
    {
        LandmarkHit best = LandmarkHit.None;
        foreach (Vector2I region in GetRegionsAroundCell(cell, 1))
        {
            RegionPlan plan = GetRegionPlan(region);
            if (WorldGenerationLayerSettingsV2.EnableVillages)
            {
                ApplyVillageHit(plan.Village, cell, ref best);
            }

            foreach (LandmarkSiteV2 landmark in plan.Landmarks)
            {
                if (!WorldGenerationLayerSettingsV2.IsLandmarkEnabled(landmark.Kind))
                {
                    continue;
                }

                float distance = Distance(cell, landmark.Center);
                float outerRadius = landmark.Kind == LandmarkKindV2.Quarry
                    ? landmark.Radius + 7.0f
                    : landmark.Radius + 5.0f;
                if (distance > outerRadius || distance >= best.Distance)
                {
                    continue;
                }

                best = new LandmarkHit(landmark.Id, landmark.Kind, distance, landmark.Radius);
            }
        }

        return best;
    }

    private static void ApplyVillageHit(VillageSiteV2 village, Vector2I cell, ref LandmarkHit best)
    {
        float distance = Distance(cell, village.Center);
        if (distance > village.Radius + 5.0f || distance >= best.Distance)
        {
            return;
        }

        LandmarkKindV2 kind = village.IsStartingVillage ? LandmarkKindV2.StartingVillage : LandmarkKindV2.Village;
        best = new LandmarkHit(village.Id, kind, distance, village.Radius);
    }

    private RoadHit GetRoadHit(Vector2I cell)
    {
        if (!WorldGenerationLayerSettingsV2.EnableRoads || !WorldGenerationLayerSettingsV2.EnableVillages)
        {
            return RoadHit.None;
        }

        RoadHit best = RoadHit.None;
        foreach (Vector2I region in GetRegionsAroundCell(cell, 1))
        {
            foreach (RoadPathV2 road in TryGetCachedRoadsForRegion(region))
            {
                float distance = road.DistanceToPath(cell);
                if (distance <= road.Width && distance < best.Distance)
                {
                    best = new RoadHit(true, road.IsMainVillageRoad, distance, road.Width);
                }
            }
        }

        return best;
    }

    private float GetForestStrength(Vector2I cell, float riverDistance)
    {
        if (!WorldGenerationLayerSettingsV2.EnableForests)
        {
            return 0.0f;
        }

        float strength = 0.0f;
        foreach (Vector2I region in GetRegionsAroundCell(cell, 1))
        {
            foreach (ForestClusterSiteV2 cluster in GetRegionPlan(region).ForestClusters)
            {
                strength = Mathf.Max(strength, GetClusterStrength(cluster, cell));
            }
        }

        if (riverDistance > _settings.RiverWidth + 3.0f
            && riverDistance < _settings.RiverWidth + _settings.RiverBankWidth + 10.0f)
        {
            strength = Mathf.Clamp(strength + _settings.RiverForestBonus, 0.0f, 1.0f);
        }

        LandmarkHit siteHit = GetLandmarkHit(cell);
        if (siteHit.Kind != LandmarkKindV2.None)
        {
            float clearingRadius = siteHit.Radius + 18.0f;
            if (siteHit.Distance < clearingRadius)
            {
                strength *= Mathf.Clamp(siteHit.Distance / clearingRadius, 0.0f, 1.0f);
            }
        }

        return Mathf.Clamp(strength, 0.0f, 1.0f);
    }

    private float GetClusterStrength(ForestClusterSiteV2 cluster, Vector2I cell)
    {
        Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
        Vector2 delta = point - cluster.Center;
        float cos = Mathf.Cos(cluster.Angle);
        float sin = Mathf.Sin(cluster.Angle);
        float localX = delta.X * cos + delta.Y * sin;
        float localY = -delta.X * sin + delta.Y * cos;
        float nx = localX / Mathf.Max(1.0f, cluster.Length * 0.55f);
        float ny = localY / Mathf.Max(1.0f, cluster.Width * 0.62f);
        float distance = Mathf.Sqrt(nx * nx + ny * ny);
        float edgeNoise = (FractalNoise(cell.X + cluster.Id * 19, cell.Y - cluster.Id * 13, 46.0f, cluster.Id + 400, 4) - 0.5f)
            * cluster.EdgeNoiseStrength;
        float belt = 1.0f + edgeNoise;
        float strength = 0.0f;

        if (distance < belt)
        {
            float core = Mathf.Clamp(1.0f - distance / Mathf.Max(0.01f, belt), 0.0f, 1.0f);
            strength = Mathf.Pow(core, 0.72f) * cluster.Density;
        }

        foreach (ForestGroveSiteV2 grove in cluster.Groves)
        {
            float groveDistance = point.DistanceTo(grove.Center);
            float groveRadius = grove.Radius + (FractalNoise(cell.X - cluster.Id, cell.Y + cluster.Id, 28.0f, cluster.Id + 77, 3) - 0.5f) * 9.0f;
            if (groveDistance < groveRadius)
            {
                float t = 1.0f - groveDistance / Mathf.Max(1.0f, groveRadius);
                strength = Mathf.Max(strength, Mathf.Pow(t, 0.85f) * grove.Density);
            }
        }

        float clearing = FractalNoise(cell.X, cell.Y, 34.0f, cluster.Id + 900, 3);
        if (clearing > 0.73f)
        {
            strength *= _settings.ForestClearingStrength;
        }

        return strength;
    }

    private float GetRiverDistance(Vector2I cell)
    {
        int approximateIndex = Mathf.RoundToInt(cell.X / RiverSpacingCells);
        int band = FloorDiv(cell.Y, RiverBandHeightCells);
        float best = float.MaxValue;

        for (int riverIndex = approximateIndex - 1; riverIndex <= approximateIndex + 1; riverIndex++)
        {
            if (!IsRiverIndexActive(riverIndex))
            {
                continue;
            }

            for (int bandOffset = -1; bandOffset <= 1; bandOffset++)
            {
                foreach (RiverPathV2 river in GetRiverBand(riverIndex, band + bandOffset))
                {
                    best = Mathf.Min(best, river.DistanceToPath(cell));
                }
            }
        }

        return best;
    }

    private IReadOnlyList<RiverPathV2> GetRiverBand(int riverIndex, int band)
    {
        Vector2I key = new(riverIndex, band);
        if (_riverBands.TryGetValue(key, out IReadOnlyList<RiverPathV2>? cached))
        {
            return cached;
        }

        int minY = band * RiverBandHeightCells - 128;
        int maxY = (band + 1) * RiverBandHeightCells + 128;
        List<Vector2> points = new();
        for (int y = minY; y <= maxY; y += 96)
        {
            points.Add(new Vector2(GetRiverCenterX(riverIndex, y), y));
        }

        RiverPathV2 river = new()
        {
            Id = HashIntId(riverIndex, band, 801),
            ControlPointsWorld = points,
            Width = _settings.RiverWidth,
            BankWidth = _settings.RiverBankWidth,
            MeanderStrength = _settings.RiverMeanderStrength
        };
        List<RiverPathV2> rivers = new() { river };
        _riverBands[key] = rivers;
        return rivers;
    }

    private bool IsRiverIndexActive(int riverIndex)
    {
        int cadence = 4;
        int activeCount = Mathf.Clamp(_settings.RiverCount, 1, 3);
        return PosMod(riverIndex, cadence) < activeCount;
    }

    private float GetRiverCenterX(int riverIndex, float y)
    {
        float offset = (HashUnit(riverIndex, 0, 800) - 0.5f) * 190.0f;
        float sine = Mathf.Sin((y + HashUnit(riverIndex, 0, 801) * 900.0f) / 150.0f) * _settings.RiverMeanderStrength * 0.64f;
        float coarse = (FractalNoise(riverIndex * 1000, Mathf.RoundToInt(y), 380.0f, 802 + riverIndex, 4) - 0.5f)
            * _settings.RiverMeanderStrength * 1.9f;
        return riverIndex * RiverSpacingCells + offset + sine + coarse;
    }

    private IReadOnlyList<RoadPathV2> GetRoadsForRegion(Vector2I region)
    {
        if (!WorldGenerationLayerSettingsV2.EnableRoads || !WorldGenerationLayerSettingsV2.EnableVillages)
        {
            return System.Array.Empty<RoadPathV2>();
        }

        if (_roadsGeneratedRegions.Contains(region))
        {
            return _roadsByRegion.TryGetValue(region, out IReadOnlyList<RoadPathV2>? roads)
                ? roads
                : System.Array.Empty<RoadPathV2>();
        }

        _roadsGeneratedRegions.Add(region);
        RegionPlan plan = GetRegionPlan(region);
        List<RoadPathV2> generated = new();
        generated.Add(BuildRoadPath(plan.Village, GetRegionPlan(region + Vector2I.Right).Village, main: true));
        generated.Add(BuildRoadPath(plan.Village, GetRegionPlan(region + Vector2I.Down).Village, main: true));

        foreach (LandmarkSiteV2 landmark in plan.Landmarks)
        {
            if (!landmark.ShouldConnectRoad)
            {
                continue;
            }

            if (!WorldGenerationLayerSettingsV2.IsLandmarkEnabled(landmark.Kind))
            {
                continue;
            }

            generated.Add(BuildRoadPath(plan.Village, landmark, main: false));
        }

        _roadsByRegion[region] = generated;
        return generated;
    }

    private IReadOnlyList<RoadPathV2> TryGetCachedRoadsForRegion(Vector2I region)
    {
        return _roadsByRegion.TryGetValue(region, out IReadOnlyList<RoadPathV2>? roads)
            ? roads
            : System.Array.Empty<RoadPathV2>();
    }

    private RoadPathV2 BuildRoadPath(VillageSiteV2 from, VillageSiteV2 to, bool main)
    {
        return BuildRoadPath(from.Id, to.Id, from.Center, to.Center, main);
    }

    private RoadPathV2 BuildRoadPath(VillageSiteV2 from, LandmarkSiteV2 to, bool main)
    {
        return BuildRoadPath(from.Id, to.Id, from.Center, to.Center, main);
    }

    private RoadPathV2 BuildRoadPath(int fromId, int toId, Vector2I from, Vector2I to, bool main)
    {
        List<Vector2> path = FindRoadPath(from, to, main);
        path = ApplyRoadMeander(path, fromId, toId, main);
        List<Vector2I> bridges = new();
        foreach (Vector2 point in path)
        {
            Vector2I cell = new(Mathf.RoundToInt(point.X), Mathf.RoundToInt(point.Y));
            if (GetRiverDistance(cell) <= _settings.RiverWidth + 1.0f)
            {
                bridges.Add(cell);
            }
        }

        return new RoadPathV2
        {
            Id = HashIntId(fromId, toId, main ? 901 : 902),
            FromSiteId = fromId,
            ToSiteId = toId,
            PathPointsWorld = path,
            Bounds = CalculatePathBounds(path),
            Width = Mathf.Max(0.7f, _settings.RoadWidth) * (main ? 1.0f : 0.72f),
            IsMainVillageRoad = main,
            BridgeCandidates = bridges
        };
    }

    private static Rect2 CalculatePathBounds(IReadOnlyList<Vector2> path)
    {
        if (path.Count == 0)
        {
            return new Rect2(Vector2.Zero, Vector2.Zero);
        }

        float minX = path[0].X;
        float maxX = path[0].X;
        float minY = path[0].Y;
        float maxY = path[0].Y;
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 point = path[i];
            minX = Mathf.Min(minX, point.X);
            maxX = Mathf.Max(maxX, point.X);
            minY = Mathf.Min(minY, point.Y);
            maxY = Mathf.Max(maxY, point.Y);
        }

        return new Rect2(minX, minY, Mathf.Max(0.0f, maxX - minX), Mathf.Max(0.0f, maxY - minY));
    }

    private List<Vector2> FindRoadPath(Vector2I from, Vector2I to, bool main)
    {
        int minGridX = FloorDiv(Mathf.Min(from.X, to.X) - 88, RoadStepCells);
        int maxGridX = FloorDiv(Mathf.Max(from.X, to.X) + 88, RoadStepCells);
        int minGridY = FloorDiv(Mathf.Min(from.Y, to.Y) - 88, RoadStepCells);
        int maxGridY = FloorDiv(Mathf.Max(from.Y, to.Y) + 88, RoadStepCells);
        int startX = Mathf.RoundToInt(from.X / (float)RoadStepCells);
        int startY = Mathf.RoundToInt(from.Y / (float)RoadStepCells);
        int targetX = Mathf.RoundToInt(to.X / (float)RoadStepCells);
        int targetY = Mathf.RoundToInt(to.Y / (float)RoadStepCells);
        string startId = LocalRoadNodeId(startX, startY, minGridX, minGridY);
        string targetId = LocalRoadNodeId(targetX, targetY, minGridX, minGridY);

        List<RoadOpenNode> open = new() { new RoadOpenNode(startX, startY, 0.0f) };
        Dictionary<string, float> gScore = new() { [startId] = 0.0f };
        Dictionary<string, string> cameFrom = new();
        HashSet<string> closed = new();
        bool found = false;
        int safety = 0;

        while (open.Count > 0 && safety++ < 12000)
        {
            int bestIndex = 0;
            float bestScore = open[0].FScore;
            for (int i = 1; i < open.Count; i++)
            {
                if (open[i].FScore < bestScore)
                {
                    bestScore = open[i].FScore;
                    bestIndex = i;
                }
            }

            RoadOpenNode current = open[bestIndex];
            open.RemoveAt(bestIndex);
            string currentId = LocalRoadNodeId(current.X, current.Y, minGridX, minGridY);
            if (!closed.Add(currentId))
            {
                continue;
            }

            if (currentId == targetId)
            {
                found = true;
                break;
            }

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    int nx = current.X + dx;
                    int ny = current.Y + dy;
                    if (nx < minGridX || nx > maxGridX || ny < minGridY || ny > maxGridY)
                    {
                        continue;
                    }

                    string nextId = LocalRoadNodeId(nx, ny, minGridX, minGridY);
                    if (closed.Contains(nextId))
                    {
                        continue;
                    }

                    float multiplier = dx != 0 && dy != 0 ? 1.42f : 1.0f;
                    float tentative = gScore[currentId] + TerrainRoadCost(new Vector2I(nx * RoadStepCells, ny * RoadStepCells), from, to) * multiplier;
                    if (tentative >= (gScore.TryGetValue(nextId, out float existing) ? existing : float.MaxValue))
                    {
                        continue;
                    }

                    cameFrom[nextId] = currentId;
                    gScore[nextId] = tentative;
                    float heuristic = new Vector2(nx - targetX, ny - targetY).Length() * 1.15f;
                    open.Add(new RoadOpenNode(nx, ny, tentative + heuristic));
                }
            }
        }

        if (!found)
        {
            return BuildFallbackRoadPath(from, to, main);
        }

        List<Vector2> points = new();
        string? cursor = targetId;
        int guard = 0;
        while (!string.IsNullOrEmpty(cursor) && guard++ < 2000)
        {
            string[] parts = cursor.Split(',');
            int gx = int.Parse(parts[0]) + minGridX;
            int gy = int.Parse(parts[1]) + minGridY;
            points.Add(new Vector2(gx * RoadStepCells, gy * RoadStepCells));
            if (cursor == startId)
            {
                break;
            }

            cameFrom.TryGetValue(cursor, out cursor);
        }

        points.Reverse();
        return SmoothPath(points, 2);
    }

    private float TerrainRoadCost(Vector2I point, Vector2I from, Vector2I to)
    {
        float cost = 1.0f;
        cost += GetForestStrength(point, GetRiverDistance(point)) * _settings.RoadForestPenalty;
        float riverDistance = GetRiverDistance(point);
        if (riverDistance < _settings.RiverWidth + 1.0f)
        {
            cost += _settings.RoadRiverPenalty;
        }
        else if (riverDistance < _settings.RiverWidth + _settings.RiverBankWidth + 4.0f)
        {
            cost += 3.2f;
        }

        if (Distance(point, from) < _settings.VillageRadius + 18.0f || Distance(point, to) < _settings.VillageRadius + 18.0f)
        {
            cost *= _settings.RoadVillageAttraction;
        }

        cost += FractalNoise(point.X, point.Y, 96.0f, 777, 3) * 0.6f;
        return cost;
    }

    private List<Vector2> BuildFallbackRoadPath(Vector2I from, Vector2I to, bool main)
    {
        Vector2 a = new(from.X, from.Y);
        Vector2 d = new(to.X - from.X, to.Y - from.Y);
        float length = Mathf.Max(1.0f, d.Length());
        Vector2 normal = new(-d.Y / length, d.X / length);
        float bend = (HashUnit(from.X, to.Y, 910) - 0.5f) * _settings.RoadMeanderStrength * (main ? 1.45f : 0.95f);
        Vector2 b = a + d * 0.33f + normal * bend;
        Vector2 c = a + d * 0.66f - normal * bend * 0.5f;
        Vector2 e = new(to.X, to.Y);
        List<Vector2> points = new();
        for (int i = 0; i <= 36; i++)
        {
            float t = i / 36.0f;
            points.Add(CubicBezier(a, b, c, e, t));
        }

        return points;
    }

    private List<Vector2> ApplyRoadMeander(List<Vector2> points, int fromId, int toId, bool main)
    {
        if (points.Count <= 2 || _settings.RoadMeanderStrength <= 0.0f)
        {
            return points;
        }

        float strength = _settings.RoadMeanderStrength * (main ? 0.16f : 0.11f);
        float phase = HashUnit(fromId, toId, 920) * Mathf.Tau;
        int waveCount = 2 + Mathf.FloorToInt(HashUnit(toId, fromId, 921) * 3.0f);
        List<Vector2> meandered = new() { points[0] };

        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector2 previous = points[i - 1];
            Vector2 next = points[i + 1];
            Vector2 direction = next - previous;
            if (direction.LengthSquared() <= 0.0001f)
            {
                meandered.Add(points[i]);
                continue;
            }

            direction = direction.Normalized();
            Vector2 normal = new(-direction.Y, direction.X);
            float t = i / (float)(points.Count - 1);
            float fade = Mathf.Sin(t * Mathf.Pi);
            float wave = Mathf.Sin(t * Mathf.Tau * waveCount + phase);
            float noise = (FractalNoise(Mathf.RoundToInt(points[i].X), Mathf.RoundToInt(points[i].Y), 118.0f, fromId ^ toId ^ 930, 3) - 0.5f) * 2.0f;
            float offset = (wave * 0.45f + noise * 0.55f) * strength * fade;
            meandered.Add(points[i] + normal * offset);
        }

        meandered.Add(points[^1]);
        return SmoothPath(meandered, 1);
    }

    private static Vector2 CubicBezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float mt = 1.0f - t;
        return mt * mt * mt * a + 3.0f * mt * mt * t * b + 3.0f * mt * t * t * c + t * t * t * d;
    }

    private static List<Vector2> SmoothPath(List<Vector2> points, int passes)
    {
        if (points.Count <= 2)
        {
            return points;
        }

        List<Vector2> smoothed = points;
        for (int pass = 0; pass < passes; pass++)
        {
            List<Vector2> next = new() { smoothed[0] };
            for (int i = 1; i < smoothed.Count - 1; i++)
            {
                next.Add((smoothed[i - 1] + smoothed[i] * 2.0f + smoothed[i + 1]) / 4.0f);
            }
            next.Add(smoothed[^1]);
            smoothed = next;
        }

        return smoothed;
    }

    private RegionPlan GetRegionPlan(Vector2I region)
    {
        if (_regionPlans.TryGetValue(region, out RegionPlan? plan))
        {
            return plan;
        }

        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();
        plan = CreateRegionPlan(region);
        profiler.EndSample(WorldV2PerformanceProfiler.ContextSettlementPlanEnsure, start, region);
        _regionPlans[region] = plan;
        return plan;
    }

    private RegionPlan CreateRegionPlan(Vector2I region)
    {
        Vector2I origin = region * RegionSizeCells;
        bool startRegion = region == Vector2I.Zero;
        Vector2I villageCenter = startRegion
            ? _settings.StartCenter
            : PickVillageCenter(region, origin);
        VillageSiteV2 village = new()
        {
            Id = HashIntId(region.X, region.Y, 100),
            Center = villageCenter,
            Radius = startRegion ? _settings.StartVillageRadius : _settings.VillageRadius,
            OccupiedRadius = startRegion ? _settings.StartVillageRadius : _settings.VillageRadius,
            AvoidRadius = (startRegion ? _settings.StartVillageRadius : _settings.VillageRadius) + 18.0f,
            IsStartingVillage = startRegion,
            ShouldConnectRoad = true
        };

        List<LandmarkSiteV2> landmarks = CreateLandmarks(region, origin, village);
        List<ForestClusterSiteV2> forests = CreateForestClusters(region, origin);
        return new RegionPlan(village, landmarks, forests);
    }

    private Vector2I PickVillageCenter(Vector2I region, Vector2I origin)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            int x = Mathf.RoundToInt(origin.X + RegionSizeCells * (0.28f + HashUnit(region.X, region.Y, 110 + attempt) * 0.44f));
            int y = Mathf.RoundToInt(origin.Y + RegionSizeCells * (0.28f + HashUnit(region.X, region.Y, 130 + attempt) * 0.44f));
            Vector2I candidate = new(x, y);
            if (GetRiverDistance(candidate) > _settings.RiverWidth + _settings.RiverBankWidth + 12.0f)
            {
                return candidate;
            }
        }

        return origin + new Vector2I(RegionSizeCells / 2, RegionSizeCells / 2);
    }

    private List<LandmarkSiteV2> CreateLandmarks(Vector2I region, Vector2I origin, VillageSiteV2 village)
    {
        List<LandmarkSiteV2> landmarks = new();
        List<PlacementCircle> occupied = new()
        {
            new PlacementCircle(village.Center, village.AvoidRadius)
        };
        int count = Mathf.Max(0, _settings.LandmarkCountPerRegion);
        LandmarkKindV2[] kinds =
        {
            LandmarkKindV2.Ruin,
            LandmarkKindV2.Dungeon,
            LandmarkKindV2.BanditCamp,
            LandmarkKindV2.FactionOutpost
        };

        for (int i = 0; i < count; i++)
        {
            LandmarkKindV2 kind = kinds[Mathf.Min(kinds.Length - 1, Mathf.FloorToInt(HashUnit(region.X, region.Y, 220 + i) * kinds.Length))];
            if (!WorldGenerationLayerSettingsV2.IsLandmarkEnabled(kind))
            {
                continue;
            }

            float radius = 7.0f + HashUnit(region.X, region.Y, 250 + i) * 4.0f;
            Vector2I center = PickNonOverlappingLandmarkCenter(region, origin, village.Center, i, 200, radius, GetLandmarkAvoidRadius(kind, radius), occupied);
            float chance = GetRoadChance(kind);
            bool shouldConnect = HashUnit(region.X, region.Y, 240 + i) < chance;
            LandmarkSiteV2 landmark = new()
            {
                Id = HashIntId(region.X, region.Y, 300 + i),
                Kind = kind,
                Center = center,
                Radius = radius,
                OccupiedRadius = radius,
                AvoidRadius = GetLandmarkAvoidRadius(kind, radius),
                CanHaveRoad = chance > 0.0f,
                RoadConnectionChance = chance,
                ShouldConnectRoad = shouldConnect
            };
            landmarks.Add(landmark);
            occupied.Add(new PlacementCircle(center, landmark.AvoidRadius));
        }

        int quarryCount = WorldGenerationLayerSettingsV2.EnableQuarries
            ? Mathf.Max(0, _settings.QuarryCountPerRegion)
            : 0;
        for (int i = 0; i < quarryCount; i++)
        {
            float radius = 6.0f + HashUnit(region.X, region.Y, 480 + i) * 3.0f;
            Vector2I center = PickNonOverlappingLandmarkCenter(region, origin, village.Center, i, 460, radius, GetLandmarkAvoidRadius(LandmarkKindV2.Quarry, radius), occupied);
            LandmarkSiteV2 quarry = new()
            {
                Id = HashIntId(region.X, region.Y, 500 + i),
                Kind = LandmarkKindV2.Quarry,
                Center = center,
                Radius = radius,
                OccupiedRadius = radius,
                AvoidRadius = GetLandmarkAvoidRadius(LandmarkKindV2.Quarry, radius),
                CanHaveRoad = false,
                RoadConnectionChance = _settings.QuarryRoadChance,
                ShouldConnectRoad = false
            };
            landmarks.Add(quarry);
            occupied.Add(new PlacementCircle(center, quarry.AvoidRadius));
        }

        return landmarks;
    }

    private Vector2I PickLandmarkCenter(Vector2I region, Vector2I origin, Vector2I villageCenter, int index, int salt)
    {
        float angle = HashUnit(region.X, region.Y, salt + index * 17) * Mathf.Tau;
        float radius = RegionSizeCells * (0.20f + HashUnit(region.X, region.Y, salt + index * 17 + 1) * 0.30f);
        int x = Mathf.RoundToInt(villageCenter.X + Mathf.Cos(angle) * radius);
        int y = Mathf.RoundToInt(villageCenter.Y + Mathf.Sin(angle) * radius);
        x = Mathf.Clamp(x, origin.X + 22, origin.X + RegionSizeCells - 22);
        y = Mathf.Clamp(y, origin.Y + 22, origin.Y + RegionSizeCells - 22);
        return new Vector2I(x, y);
    }

    private Vector2I PickNonOverlappingLandmarkCenter(
        Vector2I region,
        Vector2I origin,
        Vector2I villageCenter,
        int index,
        int salt,
        float occupiedRadius,
        float avoidRadius,
        IReadOnlyList<PlacementCircle> occupied)
    {
        Vector2I bestFallback = PickLandmarkCenter(region, origin, villageCenter, index, salt);
        float bestClearance = ScorePlacementClearance(bestFallback, occupiedRadius, avoidRadius, occupied);
        for (int attempt = 0; attempt < 32; attempt++)
        {
            Vector2I candidate = PickLandmarkCenter(region, origin, villageCenter, index + attempt * 13, salt + attempt * 29);
            float clearance = ScorePlacementClearance(candidate, occupiedRadius, avoidRadius, occupied);
            if (clearance > bestClearance)
            {
                bestClearance = clearance;
                bestFallback = candidate;
            }

            if (IsPlacementClear(candidate, occupiedRadius, avoidRadius, occupied))
            {
                if (attempt > 0)
                {
                    _siteOverlapResolvedCount++;
                }

                return candidate;
            }
        }

        _rejectedSitePlacementCount++;
        return bestFallback;
    }

    private static bool IsPlacementClear(Vector2I center, float occupiedRadius, float avoidRadius, IReadOnlyList<PlacementCircle> occupied)
    {
        Vector2 candidate = new(center.X, center.Y);
        foreach (PlacementCircle existing in occupied)
        {
            float required = Mathf.Max(occupiedRadius + existing.Radius, avoidRadius);
            if (candidate.DistanceSquaredTo(new Vector2(existing.Center.X, existing.Center.Y)) < required * required)
            {
                return false;
            }
        }

        return true;
    }

    private static float ScorePlacementClearance(Vector2I center, float occupiedRadius, float avoidRadius, IReadOnlyList<PlacementCircle> occupied)
    {
        if (occupied.Count == 0)
        {
            return float.MaxValue;
        }

        Vector2 candidate = new(center.X, center.Y);
        float best = float.MaxValue;
        foreach (PlacementCircle existing in occupied)
        {
            float required = Mathf.Max(occupiedRadius + existing.Radius, avoidRadius);
            float distance = candidate.DistanceTo(new Vector2(existing.Center.X, existing.Center.Y)) - required;
            best = Mathf.Min(best, distance);
        }

        return best;
    }

    private static float GetLandmarkAvoidRadius(LandmarkKindV2 kind, float radius)
    {
        return kind switch
        {
            LandmarkKindV2.Quarry => radius + 12.0f,
            LandmarkKindV2.BanditCamp or LandmarkKindV2.Dungeon => radius + 18.0f,
            LandmarkKindV2.Ruin => radius + 14.0f,
            LandmarkKindV2.FactionOutpost => radius + 16.0f,
            _ => radius + 12.0f
        };
    }

    private List<ForestClusterSiteV2> CreateForestClusters(Vector2I region, Vector2I origin)
    {
        List<ForestClusterSiteV2> clusters = new();
        if (!WorldGenerationLayerSettingsV2.EnableForests)
        {
            return clusters;
        }

        int count = Mathf.Max(0, _settings.ForestClusterCount);
        for (int i = 0; i < count; i++)
        {
            Vector2 center = new(
                origin.X + 28.0f + HashUnit(region.X, region.Y, 310 + i) * (RegionSizeCells - 56.0f),
                origin.Y + 28.0f + HashUnit(region.X, region.Y, 330 + i) * (RegionSizeCells - 56.0f));
            float angle = HashUnit(region.X, region.Y, 340 + i) * Mathf.Tau;
            float length = Mathf.Lerp(_settings.ForestClusterMinLength, _settings.ForestClusterMaxLength, HashUnit(region.X, region.Y, 350 + i));
            float width = Mathf.Lerp(_settings.ForestClusterMinWidth, _settings.ForestClusterMaxWidth, HashUnit(region.X, region.Y, 360 + i));
            float density = 0.78f + HashUnit(region.X, region.Y, 370 + i) * 0.42f;
            int id = HashIntId(region.X, region.Y, 400 + i * 31);
            clusters.Add(new ForestClusterSiteV2
            {
                Id = id,
                Center = center,
                Angle = angle,
                Length = length,
                Width = width,
                Density = density,
                EdgeNoiseStrength = 0.36f,
                ClearingStrength = _settings.ForestClearingStrength,
                Groves = CreateGroves(region, center, angle, length, width, id, i)
            });
        }

        return clusters;
    }

    private List<ForestGroveSiteV2> CreateGroves(Vector2I region, Vector2 center, float angle, float length, float width, int clusterId, int clusterIndex)
    {
        List<ForestGroveSiteV2> groves = new();
        int min = Mathf.Min(_settings.ForestGroveMinCount, _settings.ForestGroveMaxCount);
        int max = Mathf.Max(_settings.ForestGroveMinCount, _settings.ForestGroveMaxCount);
        int count = min + Mathf.FloorToInt(HashUnit(region.X, region.Y, 380 + clusterIndex) * (max - min + 1));
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);

        for (int i = 0; i < count; i++)
        {
            float along = (HashUnit(region.X, region.Y, clusterId + i * 7) - 0.5f) * length * 0.95f;
            float side = (HashUnit(region.X, region.Y, clusterId + i * 7 + 1) - 0.5f) * width * 1.35f;
            Vector2 groveCenter = new(center.X + cos * along - sin * side, center.Y + sin * along + cos * side);
            groves.Add(new ForestGroveSiteV2
            {
                Center = groveCenter,
                Radius = 18.0f + HashUnit(region.X, region.Y, clusterId + i * 7 + 2) * 32.0f,
                Density = 0.45f + HashUnit(region.X, region.Y, clusterId + i * 7 + 3) * 0.55f
            });
        }

        return groves;
    }

    private float GetRoadChance(LandmarkKindV2 kind)
    {
        return kind switch
        {
            LandmarkKindV2.Ruin => _settings.RuinRoadChance,
            LandmarkKindV2.Dungeon => _settings.DungeonRoadChance,
            LandmarkKindV2.BanditCamp => _settings.BanditRoadChance,
            LandmarkKindV2.FactionOutpost => _settings.FactionRoadChance,
            LandmarkKindV2.Quarry => _settings.QuarryRoadChance,
            LandmarkKindV2.Village or LandmarkKindV2.StartingVillage => 1.0f,
            _ => 0.0f
        };
    }

    private IEnumerable<Vector2I> GetRegionsAroundCell(Vector2I cell, int margin)
    {
        Vector2I region = GlobalCellToRegion(cell);
        for (int y = region.Y - margin; y <= region.Y + margin; y++)
        {
            for (int x = region.X - margin; x <= region.X + margin; x++)
            {
                yield return new Vector2I(x, y);
            }
        }
    }

    private IEnumerable<Vector2I> GetRegionsForRect(Rect2I rect, int margin)
    {
        Vector2I min = GlobalCellToRegion(rect.Position);
        Vector2I max = GlobalCellToRegion(rect.End - Vector2I.One);
        for (int y = min.Y - margin; y <= max.Y + margin; y++)
        {
            for (int x = min.X - margin; x <= max.X + margin; x++)
            {
                yield return new Vector2I(x, y);
            }
        }
    }

    private static Vector2I GlobalCellToRegion(Vector2I cell)
    {
        return new Vector2I(FloorDiv(cell.X, RegionSizeCells), FloorDiv(cell.Y, RegionSizeCells));
    }

    private static string LocalRoadNodeId(int gridX, int gridY, int minGridX, int minGridY)
    {
        return $"{gridX - minGridX},{gridY - minGridY}";
    }

    private float FractalNoise(int x, int y, float scale, int salt, int octaves)
    {
        float amplitude = 0.5f;
        float frequency = 1.0f;
        float sum = 0.0f;
        float norm = 0.0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += ValueNoise(x * frequency / scale, y * frequency / scale, salt + i * 101) * amplitude;
            norm += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.0f;
        }

        return norm <= 0.0f ? 0.0f : sum / norm;
    }

    private float ValueNoise(float x, float y, int salt)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        float tx = SmoothStep(x - x0);
        float ty = SmoothStep(y - y0);
        float a = HashUnit(x0, y0, salt);
        float b = HashUnit(x1, y0, salt);
        float c = HashUnit(x0, y1, salt);
        float d = HashUnit(x1, y1, salt);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private float HashUnit(int x, int y, int salt)
    {
        return (HashInt(x, y, salt) & 0x00ffffffu) / 16777215.0f;
    }

    private uint HashInt(int x, int y, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            Mix(ref hash, _worldSeed);
            Mix(ref hash, x);
            Mix(ref hash, y);
            Mix(ref hash, salt);
            return hash;
        }
    }

    private int HashIntId(int x, int y, int salt)
    {
        return unchecked((int)(HashInt(x, y, salt) & 0x7fffffffu));
    }

    private static void Mix(ref uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }
    }

    private static float SmoothStep(float value)
    {
        return value * value * (3.0f - 2.0f * value);
    }

    private static int FloorDiv(int a, int b)
    {
        int quotient = a / b;
        int remainder = a % b;
        return remainder != 0 && ((remainder < 0) != (b < 0)) ? quotient - 1 : quotient;
    }

    private static int PosMod(int value, int divisor)
    {
        int remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }

    private static float Distance(Vector2I a, Vector2I b)
    {
        return new Vector2(a.X - b.X, a.Y - b.Y).Length();
    }

    private sealed class RegionPlan
    {
        public RegionPlan(VillageSiteV2 village, IReadOnlyList<LandmarkSiteV2> landmarks, IReadOnlyList<ForestClusterSiteV2> forestClusters)
        {
            Village = village;
            Landmarks = landmarks;
            ForestClusters = forestClusters;
        }

        public VillageSiteV2 Village { get; }
        public IReadOnlyList<LandmarkSiteV2> Landmarks { get; }
        public IReadOnlyList<ForestClusterSiteV2> ForestClusters { get; }
    }

    private readonly struct LandmarkHit
    {
        public static readonly LandmarkHit None = new(0, LandmarkKindV2.None, float.MaxValue, 0.0f);

        public LandmarkHit(int id, LandmarkKindV2 kind, float distance, float radius)
        {
            Id = id;
            Kind = kind;
            Distance = distance;
            Radius = radius;
        }

        public int Id { get; }
        public LandmarkKindV2 Kind { get; }
        public float Distance { get; }
        public float Radius { get; }
    }

    private readonly struct RoadHit
    {
        public static readonly RoadHit None = new(false, false, float.MaxValue, 0.0f);

        public RoadHit(bool isRoad, bool isMainRoad, float distance, float width)
        {
            IsRoad = isRoad;
            IsMainRoad = isMainRoad;
            Distance = distance;
            Width = width;
        }

        public bool IsRoad { get; }
        public bool IsMainRoad { get; }
        public float Distance { get; }
        public float Width { get; }
    }

    private readonly struct RoadOpenNode
    {
        public RoadOpenNode(int x, int y, float fScore)
        {
            X = x;
            Y = y;
            FScore = fScore;
        }

        public int X { get; }
        public int Y { get; }
        public float FScore { get; }
    }

    private readonly struct PlacementCircle
    {
        public PlacementCircle(Vector2I center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public Vector2I Center { get; }
        public float Radius { get; }
    }
}
