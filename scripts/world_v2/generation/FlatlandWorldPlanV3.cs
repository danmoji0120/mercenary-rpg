using Godot;
using System.Collections.Generic;

namespace WorldV2;

public sealed class FlatlandWorldPlanV3
{
    private WorldGenerationRequestV2 _request = WorldGenerationRequestV2.CreateDevDefault(20260707);
    private WorldGenerationSettingsV2 _settings = WorldGenerationSettingsV2.Default;
    private readonly List<VillageSiteV2> _villages = new();
    private readonly List<RoadPathV2> _roads = new();
    private readonly List<ForestClusterSiteV2> _forestClusters = new();
    private readonly List<ForestRegionV3> _forestRegions = new();
    private readonly List<QuarryClusterV3> _quarryClusters = new();
    private readonly List<QuarryRegionV3> _quarryRegions = new();
    private bool _isBuilt;
    private int _startingVillageId = -1;
    private Vector2I _playerSpawnCell;
    private float _nearestToWorldCenterDistance;

    public string GeneratedPlanType => "FlatlandWorldPlanV3";
    public int WorldSeed => _request.Seed;
    public WorldMapSizeDefinitionV2 WorldSize => _request.MapSize;
    public Rect2I Bounds => WorldSize.CellBounds;
    public WorldPlanVersionV2 PlanVersion => _request.PlanVersion;
    public WorldMapSizePresetV2 MapSizePreset => _request.MapSizePreset;
    public Vector2I StartCell => _playerSpawnCell == Vector2I.Zero ? _request.StartCell : _playerSpawnCell;
    public Vector2I PlayerSpawnCell => StartCell;
    public int StartingVillageId => _startingVillageId;
    public Vector2I StartingVillageCenter => GetStartingVillageCenter();
    public float NearestToWorldCenterDistance => _nearestToWorldCenterDistance;
    public int VillageCount => _villages.Count;
    public IReadOnlyList<VillageSiteV2> Villages => _villages;
    public int RoadCount => _roads.Count;
    public int PrimaryRoadCount { get; private set; }
    public int ExtraRoadCount { get; private set; }
    public bool RoadLayerEnabled => WorldGenerationLayerSettingsV2.EnableRoads;
    public IReadOnlyList<RoadPathV2> Roads => _roads;
    public int ForestClusterCount => _forestRegions.Count;
    public int ForestRegionCount => _forestRegions.Count;
    public int MajorForestRegionCount { get; private set; }
    public int MinorForestPatchCount { get; private set; }
    public int LargeForestClusterCount => MajorForestRegionCount;
    public bool ForestLayerEnabled => WorldGenerationLayerSettingsV2.EnableForests;
    public IReadOnlyList<ForestClusterSiteV2> ForestClusters => _forestClusters;
    public IReadOnlyList<ForestRegionV3> ForestRegions => _forestRegions;
    public int QuarryClusterCount => _quarryRegions.Count;
    public int QuarryRegionCount => _quarryRegions.Count;
    public int MajorQuarryCount { get; private set; }
    public int MinorQuarryCount { get; private set; }
    public int RejectedQuarryPlacementCount { get; private set; }
    public bool QuarryLayerEnabled => WorldGenerationLayerSettingsV2.EnableQuarries;
    public IReadOnlyList<QuarryClusterV3> QuarryClusters => _quarryClusters;
    public IReadOnlyList<QuarryRegionV3> QuarryRegions => _quarryRegions;
    public bool IsBuilt => _isBuilt;

    public void Initialize(WorldGenerationRequestV2 request, WorldGenerationSettingsV2? settings)
    {
        WorldGenerationSettingsV2 nextSettings = settings ?? WorldGenerationSettingsV2.Default;
        if (ReferenceEquals(_request, request) && ReferenceEquals(_settings, nextSettings) && _isBuilt)
        {
            return;
        }

        _request = request;
        _settings = nextSettings;
        _isBuilt = false;
    }

    public void BuildPlan()
    {
        GenerateBaseField();
        GenerateRiversPlaceholder();
        GenerateVillagesPlaceholder();
        GenerateRoads();
        GenerateQuarries();
        GenerateForests();
        GenerateLandmarksPlaceholder();
        BuildSpatialIndexPlaceholder();
        _isBuilt = true;
    }

    public void ClearCaches()
    {
        _villages.Clear();
        _roads.Clear();
        _forestClusters.Clear();
        _forestRegions.Clear();
        _quarryClusters.Clear();
        _quarryRegions.Clear();
        PrimaryRoadCount = 0;
        ExtraRoadCount = 0;
        MajorForestRegionCount = 0;
        MinorForestPatchCount = 0;
        MajorQuarryCount = 0;
        MinorQuarryCount = 0;
        RejectedQuarryPlacementCount = 0;
        _startingVillageId = -1;
        _playerSpawnCell = Vector2I.Zero;
        _nearestToWorldCenterDistance = 0.0f;
        _isBuilt = false;
    }

    public FlatlandCellSampleV2 SampleCell(Vector2I globalCell)
    {
        EnsureBuilt();
        if (!WorldSize.ContainsCell(globalCell))
        {
            return CreateOutOfBoundsSample(globalCell);
        }

        FlatlandCellSampleV2 sample = new()
        {
            GlobalCellCoord = globalCell,
            IsBuildRestricted = false,
            IsWalkable = true,
            Biome = BiomeTypeV2.Plains,
            TileType = TileType.Grass
        };

        ApplyVillageSample(sample);
        ApplyRoadSample(sample);
        ApplyForestSample(sample);
        return sample;
    }

    public FlatlandChunkGenerationContextV2 BuildChunkContext(Vector2I globalChunkCoord)
    {
        EnsureBuilt();
        Vector2I originGlobalCell = WorldV2CoordinateUtility.GlobalChunkToOriginGlobalCell(globalChunkCoord);
        FlatlandChunkGenerationContextV2 context = new(globalChunkCoord, originGlobalCell);
        Rect2 chunkRect = new(
            context.GlobalCellBounds.Position,
            context.GlobalCellBounds.Size);

        foreach (VillageSiteV2 village in _villages)
        {
            Rect2 bounds = MakeCircleBounds(village.Center, village.Radius + 2.0f);
            if (bounds.Intersects(chunkRect, includeBorders: true))
            {
                context.AddVillage(village);
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableRoads)
        {
            foreach (RoadPathV2 road in _roads)
            {
                Rect2 bounds = GrowRect(road.Bounds, road.Width + 2.0f);
                if (bounds.Intersects(chunkRect, includeBorders: true))
                {
                    context.AddRoad(road);
                }
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableQuarries)
        {
            foreach (QuarryRegionV3 quarry in _quarryRegions)
            {
                if (quarry.Bounds.Intersects(chunkRect, includeBorders: true))
                {
                    context.AddQuarryRegion(quarry);
                }
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableForests)
        {
            foreach (ForestRegionV3 forest in _forestRegions)
            {
                if (forest.Bounds.Intersects(chunkRect, includeBorders: true))
                {
                    context.AddForestRegion(forest);
                }
            }
        }

        context.NearestVillageDistance = CalculateNearestVillageDistance(context);
        return context;
    }

    public void BuildChunkRaster(FlatlandChunkGenerationContextV2 context)
    {
        RasterVillages(context);

        if (WorldGenerationLayerSettingsV2.EnableRoads)
        {
            RasterRoads(context);
        }

        if (WorldGenerationLayerSettingsV2.EnableQuarries)
        {
            RasterQuarries(context);
        }

        if (WorldGenerationLayerSettingsV2.EnableForests)
        {
            RasterForests(context);
        }
    }

    public FlatlandCellSampleV2 SampleCellFast(Vector2I globalCell, int localX, int localY, FlatlandChunkGenerationContextV2 context)
    {
        if (!WorldSize.ContainsCell(globalCell))
        {
            return CreateOutOfBoundsSample(globalCell);
        }

        int index = FlatlandChunkGenerationContextV2.ToIndex(localX, localY);
        FlatlandCellSampleV2 sample = new()
        {
            GlobalCellCoord = globalCell,
            IsBuildRestricted = false,
            IsWalkable = true,
            Biome = BiomeTypeV2.Plains,
            TileType = TileType.Grass,
            IsRoad = context.IsRoad[index],
            IsVillage = context.IsVillage[index],
            IsStartingVillage = context.IsStartingVillage[index],
            IsLandmark = context.IsLandmark[index],
            IsQuarry = context.IsQuarry[index],
            HasOreSpot = context.HasOreSpot[index],
            LandmarkKind = context.LandmarkKind[index],
            ForestStrength = context.ForestStrength[index],
            IsForest = context.ForestStrength[index] > 0.42f
        };

        ResolveV3Tile(sample, context.SiteDistance[index], context.SiteRadius[index], context.RoadStrength[index]);
        return sample;
    }

    private void EnsureBuilt()
    {
        if (!_isBuilt)
        {
            BuildPlan();
        }
    }

    private void GenerateBaseField()
    {
        // V3 intentionally avoids a full cell array. Base terrain is sampled on demand.
    }

    private void GenerateRiversPlaceholder()
    {
    }

    private void GenerateVillagesPlaceholder()
    {
        _villages.Clear();
        _startingVillageId = -1;
        _playerSpawnCell = WorldSize.CenterCell;
        _nearestToWorldCenterDistance = 0.0f;

        int targetCount = GetTargetVillageCount();
        if (targetCount <= 0)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 3001));
        float edgeMargin = Mathf.Max(_settings.V3VillageEdgeMargin, _settings.V3TownRadius + 48.0f);
        int minX = Mathf.CeilToInt(edgeMargin);
        int minY = Mathf.CeilToInt(edgeMargin);
        int maxX = Mathf.FloorToInt(WorldSize.WidthCells - edgeMargin - 1.0f);
        int maxY = Mathf.FloorToInt(WorldSize.HeightCells - edgeMargin - 1.0f);

        if (minX > maxX || minY > maxY)
        {
            return;
        }

        int maxAttempts = Mathf.Max(targetCount * 8, targetCount * Mathf.Max(1, _settings.V3VillagePlacementMaxAttemptsPerVillage));
        int attempts = 0;
        int nextId = 1;

        while (_villages.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            VillageScaleV2 scale = PickScale(ref random);
            float radius = GetRadiusForScale(scale);
            Vector2I center = new(random.RangeInclusive(minX, maxX), random.RangeInclusive(minY, maxY));
            if (!CanPlaceVillage(center, radius))
            {
                continue;
            }

            _villages.Add(new VillageSiteV2
            {
                Id = nextId++,
                Center = center,
                Radius = radius,
                OccupiedRadius = radius,
                AvoidRadius = Mathf.Max(radius + 24.0f, _settings.V3VillageMinDistance * 0.5f),
                Scale = scale,
                IsStartingVillage = false,
                ShouldConnectRoad = true
            });
        }

        SelectStartingVillage();
    }

    private void GenerateRoads()
    {
        _roads.Clear();
        PrimaryRoadCount = 0;
        ExtraRoadCount = 0;

        if (!WorldGenerationLayerSettingsV2.EnableRoads || _villages.Count <= 1)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 4101));
        HashSet<long> connectedPairs = new();
        List<VillageSiteV2> connected = new();
        List<VillageSiteV2> unconnected = new(_villages);
        VillageSiteV2 root = TryGetStartingVillage(out VillageSiteV2? startVillage) && startVillage != null
            ? startVillage
            : _villages[0];
        connected.Add(root);
        unconnected.Remove(root);

        int nextRoadId = 1;
        while (unconnected.Count > 0)
        {
            VillageSiteV2 bestFrom = connected[0];
            VillageSiteV2 bestTo = unconnected[0];
            float bestScore = float.MaxValue;

            foreach (VillageSiteV2 from in connected)
            {
                foreach (VillageSiteV2 to in unconnected)
                {
                    float score = GetConnectionScore(from, to);
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestFrom = from;
                    bestTo = to;
                }
            }

            AddRoad(bestFrom, bestTo, true, nextRoadId++, connectedPairs, ref random);
            connected.Add(bestTo);
            unconnected.Remove(bestTo);
        }

        int extraTarget = Mathf.RoundToInt(_villages.Count * GetExtraRoadRatio());
        int extraAttempts = Mathf.Max(extraTarget * 6, _villages.Count);
        for (int attempt = 0; attempt < extraAttempts && ExtraRoadCount < extraTarget; attempt++)
        {
            if (!TryPickExtraRoadPair(connectedPairs, ref random, out VillageSiteV2? from, out VillageSiteV2? to)
                || from == null
                || to == null)
            {
                continue;
            }

            AddRoad(from, to, false, nextRoadId++, connectedPairs, ref random);
        }
    }

    private void GenerateForests()
    {
        _forestClusters.Clear();
        _forestRegions.Clear();
        MajorForestRegionCount = 0;
        MinorForestPatchCount = 0;

        if (!WorldGenerationLayerSettingsV2.EnableForests)
        {
            return;
        }

        GetTargetForestRegionCounts(out int majorTarget, out int minorTarget);
        int targetCount = majorTarget + minorTarget;
        if (targetCount <= 0)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 5101));
        float edgeMargin = Mathf.Max(96.0f, _settings.V3MajorForestMaxRadius * 0.55f);
        int minX = Mathf.CeilToInt(edgeMargin);
        int minY = Mathf.CeilToInt(edgeMargin);
        int maxX = Mathf.FloorToInt(WorldSize.WidthCells - edgeMargin - 1.0f);
        int maxY = Mathf.FloorToInt(WorldSize.HeightCells - edgeMargin - 1.0f);
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        int nextId = 1;
        for (int i = 0; i < majorTarget; i++)
        {
            _forestRegions.Add(CreateForestRegion(nextId++, true, minX, minY, maxX, maxY, ref random));
            MajorForestRegionCount++;
        }

        for (int i = 0; i < minorTarget; i++)
        {
            _forestRegions.Add(CreateForestRegion(nextId++, false, minX, minY, maxX, maxY, ref random));
            MinorForestPatchCount++;
        }
    }

    private void GenerateQuarriesPlaceholder()
    {
    }

    private void GenerateQuarries()
    {
        _quarryClusters.Clear();
        _quarryRegions.Clear();
        MajorQuarryCount = 0;
        MinorQuarryCount = 0;
        RejectedQuarryPlacementCount = 0;

        if (!WorldGenerationLayerSettingsV2.EnableQuarries)
        {
            return;
        }

        GetTargetQuarryCounts(out int majorTarget, out int minorTarget);
        if (majorTarget + minorTarget <= 0)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 7101));
        float edgeMargin = Mathf.Max(96.0f, _settings.V3MajorQuarryMaxRadius + 48.0f);
        int minX = Mathf.CeilToInt(edgeMargin);
        int minY = Mathf.CeilToInt(edgeMargin);
        int maxX = Mathf.FloorToInt(WorldSize.WidthCells - edgeMargin - 1.0f);
        int maxY = Mathf.FloorToInt(WorldSize.HeightCells - edgeMargin - 1.0f);
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        int nextId = 1;
        PlaceQuarryClusters(majorTarget, true, minX, minY, maxX, maxY, ref nextId, ref random);
        PlaceQuarryClusters(minorTarget, false, minX, minY, maxX, maxY, ref nextId, ref random);
    }

    private void GenerateLandmarksPlaceholder()
    {
    }

    private void BuildSpatialIndexPlaceholder()
    {
    }

    private void ApplyVillageSample(FlatlandCellSampleV2 sample)
    {
        VillageSiteV2? bestVillage = null;
        float bestDistanceSquared = float.MaxValue;

        foreach (VillageSiteV2 village in _villages)
        {
            Vector2I delta = sample.GlobalCellCoord - village.Center;
            float distanceSquared = delta.X * delta.X + delta.Y * delta.Y;
            float radiusSquared = village.Radius * village.Radius;
            if (distanceSquared > radiusSquared || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestVillage = village;
            bestDistanceSquared = distanceSquared;
        }

        if (bestVillage == null)
        {
            return;
        }

        float distance = Mathf.Sqrt(bestDistanceSquared);
        sample.IsVillage = true;
        sample.IsStartingVillage = bestVillage.IsStartingVillage;
        sample.LandmarkKind = bestVillage.IsStartingVillage ? LandmarkKindV2.StartingVillage : LandmarkKindV2.Village;
        sample.TileType = ResolveVillageTile(bestVillage, distance);

        if (bestVillage.IsStartingVillage)
        {
            sample.Biome = BiomeTypeV2.CentralTown;
            sample.IsBuildRestricted = true;
        }
        else
        {
            sample.Biome = BiomeTypeV2.Plains;
        }
    }

    private void ApplyRoadSample(FlatlandCellSampleV2 sample)
    {
        if (!WorldGenerationLayerSettingsV2.EnableRoads || sample.IsStartingVillage)
        {
            return;
        }

        float bestDistance = float.MaxValue;
        foreach (RoadPathV2 road in _roads)
        {
            if (!GrowRect(road.Bounds, road.Width + 1.5f).HasPoint(new Vector2(sample.GlobalCellCoord.X + 0.5f, sample.GlobalCellCoord.Y + 0.5f)))
            {
                continue;
            }

            bestDistance = Mathf.Min(bestDistance, road.DistanceToPath(sample.GlobalCellCoord));
        }

        if (bestDistance > _settings.V3RoadWidth)
        {
            return;
        }

        sample.IsRoad = true;
        if (!sample.IsVillage)
        {
            sample.TileType = TileType.Road;
        }
    }

    private void ApplyForestSample(FlatlandCellSampleV2 sample)
    {
        if (!WorldGenerationLayerSettingsV2.EnableForests || sample.IsVillage || sample.IsRoad)
        {
            return;
        }

        float bestStrength = 0.0f;
        foreach (ForestRegionV3 forest in _forestRegions)
        {
            bestStrength = Mathf.Max(bestStrength, GetForestPotentialAt(forest, sample.GlobalCellCoord));
        }

        if (bestStrength <= 0.0f)
        {
            return;
        }

        sample.ForestStrength = bestStrength;
        sample.IsForest = true;
        sample.Biome = BiomeTypeV2.Forest;
        sample.TileType = TileType.ForestGround;
    }

    private void RasterVillages(FlatlandChunkGenerationContextV2 context)
    {
        foreach (VillageSiteV2 village in context.RelevantVillages)
        {
            RasterVillage(context, village);
        }
    }

    private void RasterVillage(FlatlandChunkGenerationContextV2 context, VillageSiteV2 village)
    {
        if (!TryGetLocalCircleBounds(context, village.Center, village.Radius, out int minX, out int minY, out int maxX, out int maxY))
        {
            return;
        }

        float radiusSquared = village.Radius * village.Radius;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2I cell = context.ToGlobalCell(x, y);
                Vector2I delta = cell - village.Center;
                float distanceSquared = delta.X * delta.X + delta.Y * delta.Y;
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                float distance = Mathf.Sqrt(distanceSquared);
                context.IsVillage[index] = true;
                context.IsStartingVillage[index] = village.IsStartingVillage;
                context.LandmarkKind[index] = village.IsStartingVillage ? LandmarkKindV2.StartingVillage : LandmarkKindV2.Village;
                context.SiteDistance[index] = distance;
                context.SiteRadius[index] = village.Radius;
                context.HasVillageTile = true;
            }
        }
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
                RasterRoadSegment(context, road, points[i - 1], points[i]);
            }
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandRoadSample, start, context.ChunkCoord);
    }

    private void RasterRoadSegment(FlatlandChunkGenerationContextV2 context, RoadPathV2 road, Vector2 a, Vector2 b)
    {
        float width = road.Width;
        float maxDistance = width + 0.75f;
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
                if (context.IsStartingVillage[index])
                {
                    continue;
                }

                float distance = Mathf.Sqrt(DistanceToSegmentSquared(point, a, b));
                float core = Mathf.Clamp(1.0f - distance / Mathf.Max(0.1f, width), 0.0f, 1.0f);
                float wear = GetRoadWear(cell, road.VisualWearSeed, context.ChunkCoord);
                float strength = Mathf.Clamp(core * road.VisualStrength * (0.66f + wear * 0.42f), 0.0f, 1.0f);
                context.IsRoad[index] = true;
                context.RoadStrength[index] = Mathf.Max(context.RoadStrength[index], strength);
                context.HasRoadTile = true;
            }
        }
    }

    private void RasterQuarries(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        foreach (QuarryRegionV3 quarry in context.RelevantQuarryRegions)
        {
            RasterQuarryRegion(context, quarry);
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandQuarrySample, start, context.ChunkCoord);
    }

    private void RasterQuarryRegion(FlatlandChunkGenerationContextV2 context, QuarryRegionV3 quarry)
    {
        int minGlobalX = Mathf.FloorToInt(quarry.Bounds.Position.X);
        int minGlobalY = Mathf.FloorToInt(quarry.Bounds.Position.Y);
        int maxGlobalX = Mathf.CeilToInt(quarry.Bounds.End.X);
        int maxGlobalY = Mathf.CeilToInt(quarry.Bounds.End.Y);
        if (!TryGetLocalBounds(context, minGlobalX, minGlobalY, maxGlobalX, maxGlobalY, out int minX, out int minY, out int maxX, out int maxY))
        {
            return;
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                if (context.IsVillage[index] || context.IsStartingVillage[index] || context.IsRoad[index])
                {
                    continue;
                }

                Vector2I cell = context.ToGlobalCell(x, y);
                float strength = GetRockPotentialAt(quarry, cell);
                if (strength <= 0.0f)
                {
                    continue;
                }

                context.IsQuarry[index] = true;
                context.IsLandmark[index] = true;
                context.LandmarkKind[index] = LandmarkKindV2.Quarry;
                context.HasLandmarkTile = true;
                float oreRoll = StableUnitFloat(cell.X + quarry.Id * 11, cell.Y - quarry.Id * 17, quarry.Seed + 73);
                if (strength > 0.84f && oreRoll < Mathf.Clamp(_settings.V3QuarryOreSpotChance, 0.0f, 0.12f))
                {
                    if (!context.HasOreSpot[index])
                    {
                        context.OreSpotTileCount++;
                    }

                    context.HasOreSpot[index] = true;
                }
            }
        }
    }

    private void RasterForests(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        foreach (ForestRegionV3 forest in context.RelevantForestRegions)
        {
            RasterForestRegion(context, forest);
        }

        for (int i = 0; i < ChunkDataV2.CellCount; i++)
        {
            if (context.ForestStrength[i] <= 0.0f)
            {
                continue;
            }

            if (context.IsVillage[i] || context.IsStartingVillage[i])
            {
                context.ForestStrength[i] = 0.0f;
                continue;
            }

            if (context.IsRoad[i])
            {
                context.ForestStrength[i] *= Mathf.Lerp(0.18f, 0.55f, 1.0f - context.RoadStrength[i]);
                continue;
            }

            int localX = i % ChunkDataV2.ChunkSize;
            int localY = i / ChunkDataV2.ChunkSize;
            Vector2I cell = context.ToGlobalCell(localX, localY);
            float clear = GetVillageClearingAt(context, cell) * 0.70f + GetRoadClearingAt(context, cell) * 0.55f;
            if (clear > 0.0f)
            {
                context.ForestStrength[i] *= Mathf.Clamp(1.0f - clear, 0.0f, 1.0f);
            }
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandForestSample, start, context.ChunkCoord);
    }

    private void RasterForestRegion(FlatlandChunkGenerationContextV2 context, ForestRegionV3 forest)
    {
        Rect2I bounds = new(
            new Vector2I(Mathf.FloorToInt(forest.Bounds.Position.X), Mathf.FloorToInt(forest.Bounds.Position.Y)),
            new Vector2I(Mathf.CeilToInt(forest.Bounds.Size.X), Mathf.CeilToInt(forest.Bounds.Size.Y)));
        int minGlobalX = bounds.Position.X;
        int minGlobalY = bounds.Position.Y;
        int maxGlobalX = bounds.End.X;
        int maxGlobalY = bounds.End.Y;
        if (!TryGetLocalBounds(context, minGlobalX, minGlobalY, maxGlobalX, maxGlobalY, out int minX, out int minY, out int maxX, out int maxY))
        {
            return;
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2I cell = context.ToGlobalCell(x, y);
                float strength = GetForestPotentialAt(forest, cell);
                if (strength <= 0.0f)
                {
                    continue;
                }

                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                context.ForestStrength[index] = Mathf.Max(context.ForestStrength[index], strength);
            }
        }
    }

    private void RasterForestCluster(FlatlandChunkGenerationContextV2 context, ForestClusterSiteV2 forest)
    {
        if (forest.Lobes.Count == 0)
        {
            RasterForestFallbackEllipse(context, forest);
            return;
        }

        foreach (ForestLobeSiteV2 lobe in forest.Lobes)
        {
            float range = GetLobeBoundsRadius(lobe) + 8.0f;
            Vector2I center = new(Mathf.RoundToInt(lobe.Center.X), Mathf.RoundToInt(lobe.Center.Y));
            if (!TryGetLocalCircleBounds(context, center, range, out int minX, out int minY, out int maxX, out int maxY))
            {
                continue;
            }

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2I cell = context.ToGlobalCell(x, y);
                    float strength = GetForestLobeStrengthAt(forest, lobe, cell);
                    if (strength <= 0.0f)
                    {
                        continue;
                    }

                    int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                    context.ForestStrength[index] = Mathf.Max(context.ForestStrength[index], strength);
                }
            }
        }
    }

    private static float GetForestStrengthAt(ForestClusterSiteV2 forest, Vector2I cell)
    {
        if (forest.Lobes.Count > 0)
        {
            float best = 0.0f;
            foreach (ForestLobeSiteV2 lobe in forest.Lobes)
            {
                best = Mathf.Max(best, GetForestLobeStrengthAt(forest, lobe, cell));
            }

            return best;
        }

        float cos = Mathf.Cos(forest.Angle);
        float sin = Mathf.Sin(forest.Angle);
        return GetForestStrengthAt(forest, cell, cos, sin);
    }

    private static float GetForestStrengthAt(ForestClusterSiteV2 forest, Vector2I cell, float cos, float sin)
    {
        Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
        Vector2 delta = point - forest.Center;
        float localX = delta.X * cos + delta.Y * sin;
        float localY = -delta.X * sin + delta.Y * cos;
        float nx = localX / Mathf.Max(1.0f, forest.Length * 0.50f);
        float ny = localY / Mathf.Max(1.0f, forest.Width * 0.56f);
        float distance = Mathf.Sqrt(nx * nx + ny * ny);
        float edgeNoise = (StableUnitFloat(cell.X + forest.Id * 37, cell.Y - forest.Id * 17, 5201) - 0.5f) * forest.EdgeNoiseStrength;
        float edge = 1.0f + edgeNoise;
        if (distance >= edge)
        {
            return 0.0f;
        }

        float t = Mathf.Clamp(1.0f - distance / Mathf.Max(0.01f, edge), 0.0f, 1.0f);
        return Mathf.Pow(t, 0.74f) * forest.Density;
    }

    private static float GetForestLobeStrengthAt(ForestClusterSiteV2 forest, ForestLobeSiteV2 lobe, Vector2I cell)
    {
        Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
        Vector2 delta = point - lobe.Center;
        float cos = Mathf.Cos(lobe.Angle);
        float sin = Mathf.Sin(lobe.Angle);
        float localX = delta.X * cos + delta.Y * sin;
        float localY = -delta.X * sin + delta.Y * cos;
        float aspect = Mathf.Clamp(lobe.Aspect, 0.45f, 1.85f);
        float radiusX = Mathf.Max(4.0f, lobe.Radius * aspect);
        float radiusY = Mathf.Max(4.0f, lobe.Radius / aspect);
        float nx = localX / radiusX;
        float ny = localY / radiusY;
        float distance = Mathf.Sqrt(nx * nx + ny * ny);
        float edgeNoise = (StableUnitFloat(cell.X + forest.Id * 41 + lobe.Id * 17, cell.Y - forest.Id * 23 + lobe.Id * 29, 5209) - 0.5f)
            * forest.EdgeNoiseStrength;
        float edge = 1.0f + edgeNoise;
        if (distance >= edge)
        {
            return 0.0f;
        }

        float t = Mathf.Clamp(1.0f - distance / Mathf.Max(0.01f, edge), 0.0f, 1.0f);
        float strength = Mathf.Pow(t, 0.68f) * forest.Density * lobe.Density;
        float clearingNoise = StableUnitFloat(cell.X / 6 + forest.Id * 31, cell.Y / 6 - lobe.Id * 19, 5231);
        if (clearingNoise > 0.955f && strength < 0.72f)
        {
            strength *= 0.55f;
        }

        return strength;
    }

    private static float GetForestPotentialAt(ForestRegionV3 forest, Vector2I cell)
    {
        Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
        float warpScale = forest.NoiseScale * 0.42f;
        float warpX = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, forest.Seed + 101, 2) - 0.5f) * 2.0f;
        float warpY = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, forest.Seed + 211, 2) - 0.5f) * 2.0f;
        Vector2 warped = point + new Vector2(warpX, warpY) * forest.WarpStrength;
        float distance = warped.DistanceTo(forest.Center);
        float radius = Mathf.Max(8.0f, forest.ApproxRadius);
        float normalizedDistance = distance / radius;
        if (normalizedDistance > 1.28f)
        {
            return 0.0f;
        }

        float low = FractalValueNoise(warped.X * forest.NoiseScale, warped.Y * forest.NoiseScale, forest.Seed + 307, 3);
        float edge = FractalValueNoise(warped.X * forest.NoiseScale * 2.35f, warped.Y * forest.NoiseScale * 2.35f, forest.Seed + 409, 2);
        float boundaryShift = (low - 0.5f) * 0.42f + (edge - 0.5f) * 0.14f;
        float potential = 1.0f - normalizedDistance + boundaryShift;
        if (normalizedDistance < 0.42f)
        {
            potential = Mathf.Max(potential, 0.62f + low * 0.18f);
        }

        if (potential < forest.Threshold)
        {
            return 0.0f;
        }

        float t = Mathf.Clamp((potential - forest.Threshold) / Mathf.Max(0.05f, 1.05f - forest.Threshold), 0.0f, 1.0f);
        return Mathf.Clamp(Mathf.Lerp(0.46f, 1.0f, t) * forest.Density, 0.0f, 1.0f);
    }

    private static float GetQuarryPatchStrength(QuarryClusterV3 quarry, QuarryPatchV3 patch, Vector2I cell)
    {
        Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
        Vector2 delta = point - patch.Center;
        float cos = Mathf.Cos(patch.Angle);
        float sin = Mathf.Sin(patch.Angle);
        float localX = delta.X * cos + delta.Y * sin;
        float localY = -delta.X * sin + delta.Y * cos;
        float aspect = Mathf.Clamp(patch.Aspect, 0.50f, 1.70f);
        float radiusX = Mathf.Max(3.0f, patch.Radius * aspect);
        float radiusY = Mathf.Max(3.0f, patch.Radius / aspect);
        float nx = localX / radiusX;
        float ny = localY / radiusY;
        float distance = Mathf.Sqrt(nx * nx + ny * ny);
        float edgeNoise = (StableUnitFloat(cell.X + quarry.Id * 19, cell.Y - patch.Id * 23, quarry.Seed + 41) - 0.5f) * 0.42f;
        float edge = 1.0f + edgeNoise;
        if (distance >= edge)
        {
            return 0.0f;
        }

        float scatter = StableUnitFloat(cell.X / 2 + quarry.Id * 31, cell.Y / 2 - patch.Id * 17, quarry.Seed + 83);
        float core = 1.0f - distance / Mathf.Max(0.01f, edge);
        float strength = Mathf.Pow(Mathf.Clamp(core, 0.0f, 1.0f), 0.52f) * quarry.Density * patch.Density;
        float keepThreshold = Mathf.Lerp(0.62f, 0.34f, Mathf.Clamp(strength, 0.0f, 1.0f));
        return scatter >= keepThreshold ? strength : 0.0f;
    }

    private static float GetRockPotentialAt(QuarryRegionV3 quarry, Vector2I cell)
    {
        Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
        float warpScale = quarry.NoiseScale * 0.52f;
        float warpX = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, quarry.Seed + 101, 2) - 0.5f) * 2.0f;
        float warpY = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, quarry.Seed + 211, 2) - 0.5f) * 2.0f;
        Vector2 warped = point + new Vector2(warpX, warpY) * quarry.WarpStrength;
        float radius = Mathf.Max(6.0f, quarry.ApproxRadius);
        float normalizedDistance = warped.DistanceTo(quarry.Center) / radius;
        if (normalizedDistance > 1.26f)
        {
            return 0.0f;
        }

        float low = FractalValueNoise(warped.X * quarry.NoiseScale, warped.Y * quarry.NoiseScale, quarry.Seed + 307, 3);
        float edge = FractalValueNoise(warped.X * quarry.NoiseScale * 2.15f, warped.Y * quarry.NoiseScale * 2.15f, quarry.Seed + 409, 2);
        float boundaryShift = (low - 0.5f) * 0.30f + (edge - 0.5f) * 0.12f;
        float potential = 1.0f - normalizedDistance + boundaryShift;
        if (normalizedDistance < 0.44f)
        {
            potential = Mathf.Max(potential, 0.68f + low * 0.15f);
        }

        if (potential < quarry.Threshold)
        {
            return 0.0f;
        }

        float t = Mathf.Clamp((potential - quarry.Threshold) / Mathf.Max(0.05f, 1.05f - quarry.Threshold), 0.0f, 1.0f);
        return Mathf.Clamp(Mathf.Lerp(0.52f, 1.0f, t) * quarry.Density, 0.0f, 1.0f);
    }

    private static void RasterForestFallbackEllipse(FlatlandChunkGenerationContextV2 context, ForestClusterSiteV2 forest)
    {
        float range = Mathf.Max(forest.Length, forest.Width) * 0.62f + 18.0f;
        Vector2I center = new(Mathf.RoundToInt(forest.Center.X), Mathf.RoundToInt(forest.Center.Y));
        if (!TryGetLocalCircleBounds(context, center, range, out int minX, out int minY, out int maxX, out int maxY))
        {
            return;
        }

        float cos = Mathf.Cos(forest.Angle);
        float sin = Mathf.Sin(forest.Angle);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2I cell = context.ToGlobalCell(x, y);
                float strength = GetForestStrengthAt(forest, cell, cos, sin);
                if (strength <= 0.0f)
                {
                    continue;
                }

                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                context.ForestStrength[index] = Mathf.Max(context.ForestStrength[index], strength);
            }
        }
    }

    private void ResolveV3Tile(FlatlandCellSampleV2 sample, float siteDistance, float siteRadius, float roadStrength)
    {
        if (sample.IsStartingVillage)
        {
            sample.Biome = BiomeTypeV2.CentralTown;
            sample.TileType = siteDistance <= siteRadius * 0.42f ? TileType.Plaza : TileType.TownPavement;
            sample.IsBuildRestricted = true;
            return;
        }

        if (sample.IsVillage)
        {
            sample.TileType = siteDistance <= siteRadius * 0.78f ? TileType.Village : TileType.VillageEdge;
            return;
        }

        if (sample.IsRoad)
        {
            sample.TileType = roadStrength >= 0.46f ? TileType.Road : TileType.Dirt;
            return;
        }

        if (sample.IsQuarry)
        {
            sample.Biome = BiomeTypeV2.QuarryField;
            sample.LandmarkKind = LandmarkKindV2.Quarry;
            sample.TileType = sample.HasOreSpot ? TileType.OreSpot : TileType.StoneField;
            return;
        }

        if (sample.ForestStrength > 0.42f)
        {
            sample.Biome = BiomeTypeV2.Forest;
            sample.TileType = TileType.ForestGround;
        }
    }

    private int GetTargetVillageCount()
    {
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Small => Mathf.Max(1, _settings.V3SmallVillageCount),
            WorldMapSizePresetV2.Medium => Mathf.Max(1, _settings.V3MediumVillageCount),
            WorldMapSizePresetV2.Large => Mathf.Max(1, _settings.V3LargeVillageCount),
            _ => Mathf.Max(1, _settings.V3SmallVillageCount)
        };
    }

    private void GetTargetQuarryCounts(out int majorCount, out int minorCount)
    {
        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 7113));
        switch (MapSizePreset)
        {
            case WorldMapSizePresetV2.Medium:
                majorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3MediumMajorQuarryMinCount), Mathf.Max(0, _settings.V3MediumMajorQuarryMaxCount));
                minorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3MediumMinorQuarryMinCount), Mathf.Max(0, _settings.V3MediumMinorQuarryMaxCount));
                break;
            case WorldMapSizePresetV2.Large:
                majorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3LargeMajorQuarryMinCount), Mathf.Max(0, _settings.V3LargeMajorQuarryMaxCount));
                minorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3LargeMinorQuarryMinCount), Mathf.Max(0, _settings.V3LargeMinorQuarryMaxCount));
                break;
            case WorldMapSizePresetV2.Small:
            default:
                majorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3SmallMajorQuarryMinCount), Mathf.Max(0, _settings.V3SmallMajorQuarryMaxCount));
                minorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3SmallMinorQuarryMinCount), Mathf.Max(0, _settings.V3SmallMinorQuarryMaxCount));
                break;
        }
    }

    private void PlaceQuarryClusters(
        int targetCount,
        bool major,
        int minX,
        int minY,
        int maxX,
        int maxY,
        ref int nextId,
        ref DeterministicRandom random)
    {
        int placed = 0;
        int maxAttempts = Mathf.Max(targetCount * 10, targetCount * Mathf.Max(1, _settings.V3QuarryPlacementMaxAttemptsPerCluster));
        for (int attempt = 0; placed < targetCount && attempt < maxAttempts; attempt++)
        {
            float radius = PickQuarryRadius(major, ref random);
            Vector2 center = new(random.RangeInclusive(minX, maxX), random.RangeInclusive(minY, maxY));
            if (!CanPlaceQuarry(center, radius, major))
            {
                RejectedQuarryPlacementCount++;
                continue;
            }

            _quarryRegions.Add(CreateQuarryRegion(nextId++, center, radius, major, ref random));
            placed++;
            if (major)
            {
                MajorQuarryCount++;
            }
            else
            {
                MinorQuarryCount++;
            }
        }
    }

    private float PickQuarryRadius(bool major, ref DeterministicRandom random)
    {
        float min = major ? Mathf.Max(12.0f, _settings.V3MajorQuarryMinRadius) : Mathf.Max(8.0f, _settings.V3MinorQuarryMinRadius);
        float max = major ? Mathf.Max(min, _settings.V3MajorQuarryMaxRadius) : Mathf.Max(min, _settings.V3MinorQuarryMaxRadius);
        return major
            ? Mathf.Lerp(min, max, random.Range(0.35f, 1.0f))
            : Mathf.Lerp(min, max, random.Range(0.0f, 1.0f));
    }

    private bool CanPlaceQuarry(Vector2 center, float radius, bool major)
    {
        foreach (VillageSiteV2 village in _villages)
        {
            float required = village.Radius + radius + (village.IsStartingVillage && major ? 130.0f : 44.0f);
            if (center.DistanceTo(village.Center) < required)
            {
                return false;
            }
        }

        foreach (RoadPathV2 road in _roads)
        {
            float required = road.Width + radius * 0.42f + 8.0f;
            if (road.DistanceToPath(new Vector2I(Mathf.RoundToInt(center.X), Mathf.RoundToInt(center.Y))) < required)
            {
                return false;
            }
        }

        foreach (QuarryRegionV3 quarry in _quarryRegions)
        {
            float required = (radius + quarry.ApproxRadius) * 0.82f + 48.0f;
            if (center.DistanceTo(quarry.Center) < required)
            {
                return false;
            }
        }

        return true;
    }

    private QuarryRegionV3 CreateQuarryRegion(int id, Vector2 center, float radius, bool major, ref DeterministicRandom random)
    {
        float warp = major
            ? Mathf.Max(0.0f, _settings.V3MajorQuarryWarpStrength) * random.Range(0.78f, 1.22f)
            : Mathf.Max(0.0f, _settings.V3MinorQuarryWarpStrength) * random.Range(0.70f, 1.18f);
        float noiseScale = major
            ? Mathf.Max(0.001f, _settings.V3MajorQuarryNoiseScale) * random.Range(0.84f, 1.18f)
            : Mathf.Max(0.001f, _settings.V3MinorQuarryNoiseScale) * random.Range(0.84f, 1.25f);
        float threshold = Mathf.Clamp(_settings.V3QuarryPotentialThreshold + (random.NextUnit() - 0.5f) * 0.06f + (major ? -0.02f : 0.03f), 0.34f, 0.70f);
        float boundsRadius = radius * 1.32f + warp + 10.0f;
        return new QuarryRegionV3
        {
            Id = id,
            Center = center,
            ApproxRadius = radius,
            Bounds = new Rect2(center - new Vector2(boundsRadius, boundsRadius), new Vector2(boundsRadius * 2.0f, boundsRadius * 2.0f)),
            Seed = HashIntId(id, WorldSeed, major ? 7301 : 7401),
            Threshold = threshold,
            NoiseScale = noiseScale,
            WarpStrength = warp,
            Density = major ? random.Range(0.86f, 1.0f) : random.Range(0.72f, 0.94f),
            IsMajorQuarry = major
        };
    }

    private QuarryClusterV3 CreateQuarryCluster(int id, Vector2 center, float radius, bool major, ref DeterministicRandom random)
    {
        int minPatch = major ? Mathf.Max(1, _settings.V3MajorQuarryMinPatchCount) : Mathf.Max(1, _settings.V3MinorQuarryMinPatchCount);
        int maxPatch = major ? Mathf.Max(minPatch, _settings.V3MajorQuarryMaxPatchCount) : Mathf.Max(minPatch, _settings.V3MinorQuarryMaxPatchCount);
        int patchCount = random.RangeInclusive(minPatch, maxPatch);
        List<QuarryPatchV3> patches = BuildQuarryPatches(id, center, radius, patchCount, ref random);
        Rect2 bounds = CalculateQuarryBounds(center, radius, patches);
        return new QuarryClusterV3
        {
            Id = id,
            Center = center,
            ApproxRadius = radius,
            Bounds = bounds,
            Seed = HashIntId(id, WorldSeed, major ? 7301 : 7401),
            Density = major ? random.Range(0.82f, 1.0f) : random.Range(0.68f, 0.92f),
            PatchCount = patchCount,
            SizeClass = major ? QuarrySizeClassV3.Major : QuarrySizeClassV3.Minor,
            Patches = patches
        };
    }

    private static List<QuarryPatchV3> BuildQuarryPatches(int clusterId, Vector2 center, float radius, int patchCount, ref DeterministicRandom random)
    {
        List<QuarryPatchV3> patches = new(patchCount);
        for (int i = 0; i < patchCount; i++)
        {
            float angle = random.NextUnit() * Mathf.Tau;
            float distance = i == 0 ? 0.0f : radius * random.Range(0.10f, 0.78f);
            Vector2 offset = new(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
            float patchRadius = radius * random.Range(i == 0 ? 0.34f : 0.18f, i == 0 ? 0.62f : 0.46f);
            patches.Add(new QuarryPatchV3
            {
                Id = i + 1,
                Center = center + offset,
                Radius = Mathf.Max(5.0f, patchRadius),
                Aspect = random.Range(0.72f, 1.38f),
                Angle = angle + (random.NextUnit() - 0.5f) * 1.4f + clusterId * 0.013f,
                Density = random.Range(0.74f, 1.05f)
            });
        }

        return patches;
    }

    private void GetTargetForestRegionCounts(out int majorCount, out int minorCount)
    {
        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 5113));
        switch (MapSizePreset)
        {
            case WorldMapSizePresetV2.Medium:
                majorCount = random.RangeInclusive(
                    Mathf.Max(0, _settings.V3MediumMajorForestMinCount),
                    Mathf.Max(0, _settings.V3MediumMajorForestMaxCount));
                minorCount = random.RangeInclusive(
                    Mathf.Max(0, _settings.V3MediumMinorForestMinCount),
                    Mathf.Max(0, _settings.V3MediumMinorForestMaxCount));
                break;
            case WorldMapSizePresetV2.Large:
                majorCount = random.RangeInclusive(
                    Mathf.Max(0, _settings.V3LargeMajorForestMinCount),
                    Mathf.Max(0, _settings.V3LargeMajorForestMaxCount));
                minorCount = random.RangeInclusive(
                    Mathf.Max(0, _settings.V3LargeMinorForestMinCount),
                    Mathf.Max(0, _settings.V3LargeMinorForestMaxCount));
                break;
            case WorldMapSizePresetV2.Small:
            default:
                majorCount = random.RangeInclusive(
                    Mathf.Max(0, _settings.V3SmallMajorForestMinCount),
                    Mathf.Max(0, _settings.V3SmallMajorForestMaxCount));
                minorCount = random.RangeInclusive(
                    Mathf.Max(0, _settings.V3SmallMinorForestMinCount),
                    Mathf.Max(0, _settings.V3SmallMinorForestMaxCount));
                break;
        }
    }

    private ForestRegionV3 CreateForestRegion(
        int regionId,
        bool major,
        int minX,
        int minY,
        int maxX,
        int maxY,
        ref DeterministicRandom random)
    {
        float minRadius = major
            ? Mathf.Max(48.0f, _settings.V3MajorForestMinRadius)
            : Mathf.Max(16.0f, _settings.V3MinorForestMinRadius);
        float maxRadius = major
            ? Mathf.Max(minRadius, _settings.V3MajorForestMaxRadius)
            : Mathf.Max(minRadius, _settings.V3MinorForestMaxRadius);
        float radius = major
            ? Mathf.Lerp(minRadius, maxRadius, random.Range(0.35f, 1.0f))
            : Mathf.Lerp(minRadius, maxRadius, random.Range(0.0f, 1.0f));
        Vector2 center = new(random.RangeInclusive(minX, maxX), random.RangeInclusive(minY, maxY));
        float warp = major
            ? Mathf.Max(0.0f, _settings.V3MajorForestWarpStrength) * random.Range(0.78f, 1.25f)
            : Mathf.Max(0.0f, _settings.V3MinorForestWarpStrength) * random.Range(0.70f, 1.18f);
        float noiseScale = major
            ? Mathf.Max(0.001f, _settings.V3MajorForestNoiseScale) * random.Range(0.82f, 1.20f)
            : Mathf.Max(0.001f, _settings.V3MinorForestNoiseScale) * random.Range(0.82f, 1.25f);
        float threshold = Mathf.Clamp(_settings.V3ForestPotentialThreshold + (random.NextUnit() - 0.5f) * 0.08f + (major ? -0.02f : 0.03f), 0.36f, 0.72f);
        float boundsRadius = radius * 1.34f + warp + 18.0f;
        return new ForestRegionV3
        {
            RegionId = regionId,
            Center = center,
            ApproxRadius = radius,
            Bounds = new Rect2(center - new Vector2(boundsRadius, boundsRadius), new Vector2(boundsRadius * 2.0f, boundsRadius * 2.0f)),
            Seed = HashIntId(regionId, WorldSeed, major ? 5301 : 5401),
            Threshold = threshold,
            NoiseScale = noiseScale,
            WarpStrength = warp,
            Density = Mathf.Clamp(_settings.V3ForestDensity + (random.NextUnit() - 0.5f) * 0.12f, 0.50f, 1.0f),
            IsMajorForest = major
        };
    }

    private int GetTargetForestClusterCount()
    {
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Small => Mathf.Max(0, _settings.V3SmallForestClusterCount),
            WorldMapSizePresetV2.Medium => Mathf.Max(0, _settings.V3MediumForestClusterCount),
            WorldMapSizePresetV2.Large => Mathf.Max(0, _settings.V3LargeForestClusterCount),
            _ => Mathf.Max(0, _settings.V3SmallForestClusterCount)
        };
    }

    private float GetLargeForestChance()
    {
        float baseChance = Mathf.Clamp(_settings.V3LargeForestChance, 0.0f, 0.75f);
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Large => Mathf.Clamp(baseChance + 0.08f, 0.0f, 0.80f),
            WorldMapSizePresetV2.Medium => baseChance,
            _ => Mathf.Clamp(baseChance - 0.05f, 0.0f, 0.80f)
        };
    }

    private float PickForestLength(bool isLarge, ref DeterministicRandom random)
    {
        float min = Mathf.Max(32.0f, _settings.V3ForestClusterMinLength);
        float max = Mathf.Max(min, _settings.V3ForestClusterMaxLength);
        if (isLarge)
        {
            return Mathf.Lerp(min, max, random.Range(0.64f, 1.0f));
        }

        float t = random.NextUnit() < 0.42f
            ? random.Range(0.0f, 0.30f)
            : random.Range(0.22f, 0.72f);
        return Mathf.Lerp(min, max, t);
    }

    private float PickForestWidth(bool isLarge, ref DeterministicRandom random)
    {
        float min = Mathf.Max(18.0f, _settings.V3ForestClusterMinWidth);
        float max = Mathf.Max(min, _settings.V3ForestClusterMaxWidth);
        if (isLarge)
        {
            return Mathf.Lerp(min, max, random.Range(0.58f, 1.0f));
        }

        float t = random.NextUnit() < 0.50f
            ? random.Range(0.0f, 0.34f)
            : random.Range(0.28f, 0.74f);
        return Mathf.Lerp(min, max, t);
    }

    private List<ForestLobeSiteV2> BuildForestLobes(
        int forestId,
        Vector2 center,
        float length,
        float width,
        float angle,
        ref DeterministicRandom random)
    {
        int minLobes = Mathf.Max(3, _settings.V3ForestMinLobes);
        int maxLobes = Mathf.Max(minLobes, _settings.V3ForestMaxLobes);
        int lobeCount = random.RangeInclusive(minLobes, maxLobes);
        Vector2 forward = new(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 perpendicular = new(-forward.Y, forward.X);
        float baseRadius = Mathf.Max(14.0f, Mathf.Min(length, width) * 0.34f);
        List<ForestLobeSiteV2> lobes = new(lobeCount);

        for (int i = 0; i < lobeCount; i++)
        {
            float t = lobeCount == 1 ? 0.0f : i / (lobeCount - 1.0f);
            float along = (t - 0.5f) * length * random.Range(0.46f, 0.82f);
            float side = (random.NextUnit() - 0.5f) * width * 0.72f;
            if (i == 0)
            {
                along = 0.0f;
                side = 0.0f;
            }

            float wobble = Mathf.Sin((forestId + i * 1.7f) * 1.31f) * width * 0.12f;
            Vector2 lobeCenter = center + forward * along + perpendicular * (side + wobble);
            float radius = baseRadius * random.Range(0.72f, 1.38f);
            if (i == 0)
            {
                radius *= 1.18f;
            }

            lobes.Add(new ForestLobeSiteV2
            {
                Id = i + 1,
                Center = lobeCenter,
                Radius = radius,
                Aspect = random.Range(0.68f, 1.42f),
                Angle = angle + (random.NextUnit() - 0.5f) * 1.25f,
                Density = random.Range(0.84f, 1.08f)
            });
        }

        return lobes;
    }

    private bool CanPlaceVillage(Vector2I center, float radius)
    {
        float minDistance = Mathf.Max(_settings.V3VillageMinDistance, radius * 2.0f + 18.0f);
        foreach (VillageSiteV2 existing in _villages)
        {
            Vector2I delta = center - existing.Center;
            float required = Mathf.Max(minDistance, radius + existing.Radius + 32.0f);
            if (delta.LengthSquared() < required * required)
            {
                return false;
            }
        }

        return true;
    }

    private void SelectStartingVillage()
    {
        if (_villages.Count == 0)
        {
            _startingVillageId = -1;
            _playerSpawnCell = WorldSize.CenterCell;
            _nearestToWorldCenterDistance = 0.0f;
            return;
        }

        Vector2I worldCenter = WorldSize.CenterCell;
        VillageSiteV2? bestVillage = null;
        float bestDistanceSquared = float.MaxValue;
        foreach (VillageSiteV2 village in _villages)
        {
            Vector2I delta = village.Center - worldCenter;
            float distanceSquared = delta.LengthSquared();
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestVillage = village;
        }

        if (bestVillage == null)
        {
            return;
        }

        bestVillage.IsStartingVillage = true;
        _startingVillageId = bestVillage.Id;
        _nearestToWorldCenterDistance = Mathf.Sqrt(bestDistanceSquared);
        _playerSpawnCell = PickPlayerSpawnCell(bestVillage);
    }

    private Vector2I PickPlayerSpawnCell(VillageSiteV2 village)
    {
        float angle = StableUnitFloat(WorldSeed, village.Id, 3311) * Mathf.Tau;
        float distance = Mathf.Max(2.0f, village.Radius * 0.28f);
        Vector2 offset = new(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
        Vector2I spawn = village.Center + new Vector2I(Mathf.RoundToInt(offset.X), Mathf.RoundToInt(offset.Y));
        return WorldSize.ClampCell(spawn);
    }

    private static VillageScaleV2 PickScale(ref DeterministicRandom random)
    {
        float roll = random.NextUnit();
        if (roll < 0.10f)
        {
            return VillageScaleV2.Town;
        }

        if (roll < 0.26f)
        {
            return VillageScaleV2.LargeVillage;
        }

        return roll < 0.58f ? VillageScaleV2.Village : VillageScaleV2.Hamlet;
    }

    private float GetRadiusForScale(VillageScaleV2 scale)
    {
        return scale switch
        {
            VillageScaleV2.Hamlet => Mathf.Max(6.0f, _settings.V3HamletRadius),
            VillageScaleV2.LargeVillage => Mathf.Max(8.0f, _settings.V3LargeVillageRadius),
            VillageScaleV2.Town => Mathf.Max(10.0f, _settings.V3TownRadius),
            _ => Mathf.Max(7.0f, _settings.V3VillageRadius)
        };
    }

    private static TileType ResolveVillageTile(VillageSiteV2 village, float distance)
    {
        float coreRadius = village.Radius * (village.IsStartingVillage ? 0.42f : 0.34f);
        float edgeRadius = village.Radius * 0.78f;

        if (village.IsStartingVillage && distance <= coreRadius)
        {
            return TileType.Plaza;
        }

        if (village.IsStartingVillage)
        {
            return distance <= edgeRadius ? TileType.TownPavement : TileType.Village;
        }

        return distance <= edgeRadius ? TileType.Village : TileType.VillageEdge;
    }

    private void AddRoad(
        VillageSiteV2 from,
        VillageSiteV2 to,
        bool primary,
        int roadId,
        HashSet<long> connectedPairs,
        ref DeterministicRandom random)
    {
        long key = MakePairKey(from.Id, to.Id);
        if (!connectedPairs.Add(key))
        {
            return;
        }

        RoadPathV2 road = BuildRoadPath(from, to, primary, roadId, ref random);
        _roads.Add(road);
        if (primary)
        {
            PrimaryRoadCount++;
        }
        else
        {
            ExtraRoadCount++;
        }
    }

    private RoadPathV2 BuildRoadPath(VillageSiteV2 from, VillageSiteV2 to, bool primary, int roadId, ref DeterministicRandom random)
    {
        List<Vector2> points = BuildMeanderingPoints(from.Center, to.Center, roadId, primary, ref random);
        float width = Mathf.Max(0.75f, _settings.V3RoadWidth) * (primary ? 1.0f : 0.82f);
        return new RoadPathV2
        {
            Id = roadId,
            FromSiteId = from.Id,
            ToSiteId = to.Id,
            PathPointsWorld = points,
            Bounds = GrowRect(CalculatePathBounds(points), width + 3.0f),
            Width = width,
            IsMainVillageRoad = primary,
            VisualStrength = primary ? 1.0f : 0.78f,
            VisualWearSeed = HashIntId(from.Id, to.Id, roadId + 6201)
        };
    }

    private List<Vector2> BuildMeanderingPoints(Vector2I from, Vector2I to, int roadId, bool primary, ref DeterministicRandom random)
    {
        Vector2 start = new(from.X + 0.5f, from.Y + 0.5f);
        Vector2 end = new(to.X + 0.5f, to.Y + 0.5f);
        Vector2 delta = end - start;
        float distance = Mathf.Max(1.0f, delta.Length());
        Vector2 forward = delta / distance;
        Vector2 perpendicular = new(-forward.Y, forward.X);
        int middlePointCount = distance switch
        {
            < 420.0f => 2,
            < 850.0f => 3,
            < 1350.0f => 5,
            _ => 7
        };
        float meander = Mathf.Min(_settings.V3RoadMeanderStrength, distance * 0.16f) * (primary ? 1.0f : 1.22f);
        float phase = StableUnitFloat(WorldSeed, roadId, 4201) * Mathf.Tau;

        List<Vector2> controls = new(middlePointCount + 2)
        {
            start
        };

        for (int i = 1; i <= middlePointCount; i++)
        {
            float t = i / (middlePointCount + 1.0f);
            Vector2 basePoint = start.Lerp(end, t);
            float wave = Mathf.Sin(t * Mathf.Tau * 1.18f + phase) * 0.52f
                + Mathf.Sin(t * Mathf.Tau * 0.43f + phase * 1.7f) * 0.34f
                + Mathf.Sin(t * Mathf.Tau * 2.05f + phase * 0.31f) * 0.14f;
            float jitter = (random.NextUnit() - 0.5f) * 0.28f;
            float falloff = Mathf.Sin(t * Mathf.Pi);
            Vector2 point = basePoint + perpendicular * (wave + jitter) * meander * falloff;
            controls.Add(point);
        }

        controls.Add(end);
        return SampleCatmullRom(SmoothRoadPoints(controls), 5);
    }

    private static List<Vector2> SampleCatmullRom(IReadOnlyList<Vector2> controls, int samplesPerSegment)
    {
        if (controls.Count <= 2)
        {
            return new List<Vector2>(controls);
        }

        List<Vector2> result = new((controls.Count - 1) * samplesPerSegment + 1)
        {
            controls[0]
        };

        for (int i = 0; i < controls.Count - 1; i++)
        {
            Vector2 p0 = controls[Mathf.Max(0, i - 1)];
            Vector2 p1 = controls[i];
            Vector2 p2 = controls[i + 1];
            Vector2 p3 = controls[Mathf.Min(controls.Count - 1, i + 2)];

            for (int sample = 1; sample <= samplesPerSegment; sample++)
            {
                float t = sample / (float)samplesPerSegment;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        return result;
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            p1 * 2.0f
            + (p2 - p0) * t
            + (p0 * 2.0f - p1 * 5.0f + p2 * 4.0f - p3) * t2
            + (-p0 + p1 * 3.0f - p2 * 3.0f + p3) * t3);
    }

    private static List<Vector2> SmoothRoadPoints(List<Vector2> points)
    {
        if (points.Count <= 3)
        {
            return points;
        }

        List<Vector2> smoothed = new(points.Count)
        {
            points[0]
        };

        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector2 averaged = points[i - 1] * 0.18f + points[i] * 0.64f + points[i + 1] * 0.18f;
            smoothed.Add(averaged);
        }

        smoothed.Add(points[^1]);
        return smoothed;
    }

    private bool TryPickExtraRoadPair(HashSet<long> connectedPairs, ref DeterministicRandom random, out VillageSiteV2? from, out VillageSiteV2? to)
    {
        from = null;
        to = null;
        float bestScore = float.MaxValue;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            VillageSiteV2 a = _villages[random.RangeInclusive(0, _villages.Count - 1)];
            VillageSiteV2 b = _villages[random.RangeInclusive(0, _villages.Count - 1)];
            if (a.Id == b.Id || connectedPairs.Contains(MakePairKey(a.Id, b.Id)))
            {
                continue;
            }

            float distance = a.Center.DistanceTo(b.Center);
            float maxReasonableDistance = MapSizePreset switch
            {
                WorldMapSizePresetV2.Small => 720.0f,
                WorldMapSizePresetV2.Medium => 980.0f,
                WorldMapSizePresetV2.Large => 1280.0f,
                _ => 720.0f
            };

            if (distance > maxReasonableDistance)
            {
                continue;
            }

            float score = GetConnectionScore(a, b) + random.NextUnit() * 80.0f;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            from = a;
            to = b;
        }

        return from != null && to != null;
    }

    private static float GetConnectionScore(VillageSiteV2 from, VillageSiteV2 to)
    {
        float distance = from.Center.DistanceTo(to.Center);
        float priority = GetVillageConnectionPriority(from) + GetVillageConnectionPriority(to);
        return distance / Mathf.Max(0.5f, priority);
    }

    private static float GetVillageConnectionPriority(VillageSiteV2 village)
    {
        return village.Scale switch
        {
            VillageScaleV2.Town => 1.45f,
            VillageScaleV2.LargeVillage => 1.22f,
            VillageScaleV2.Hamlet => 0.92f,
            _ => 1.0f
        };
    }

    private float GetExtraRoadRatio()
    {
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Small => Mathf.Max(0.0f, _settings.V3RoadExtraLinkRatioSmall),
            WorldMapSizePresetV2.Medium => Mathf.Max(0.0f, _settings.V3RoadExtraLinkRatioMedium),
            WorldMapSizePresetV2.Large => Mathf.Max(0.0f, _settings.V3RoadExtraLinkRatioLarge),
            _ => Mathf.Max(0.0f, _settings.V3RoadExtraLinkRatioSmall)
        };
    }

    private static long MakePairKey(int a, int b)
    {
        int min = Mathf.Min(a, b);
        int max = Mathf.Max(a, b);
        return ((long)min << 32) | (uint)max;
    }

    private static int HashIntId(int a, int b, int salt)
    {
        uint hash = 2166136261u;
        Mix(ref hash, a);
        Mix(ref hash, b);
        Mix(ref hash, salt);
        return unchecked((int)(hash & 0x7fffffffu));
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

    private static Rect2 MakeCircleBounds(Vector2I center, float radius)
    {
        return new Rect2(center.X - radius, center.Y - radius, radius * 2.0f, radius * 2.0f);
    }

    private static Rect2 GrowRect(Rect2 rect, float amount)
    {
        return new Rect2(
            rect.Position - new Vector2(amount, amount),
            rect.Size + new Vector2(amount * 2.0f, amount * 2.0f));
    }

    private static Rect2 GetForestBounds(ForestClusterSiteV2 forest)
    {
        if (forest.Lobes.Count == 0)
        {
            float radius = Mathf.Max(forest.Length, forest.Width) * 0.62f + 24.0f;
            return new Rect2(forest.Center - new Vector2(radius, radius), new Vector2(radius * 2.0f, radius * 2.0f));
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        foreach (ForestLobeSiteV2 lobe in forest.Lobes)
        {
            float radius = GetLobeBoundsRadius(lobe) + 12.0f;
            minX = Mathf.Min(minX, lobe.Center.X - radius);
            minY = Mathf.Min(minY, lobe.Center.Y - radius);
            maxX = Mathf.Max(maxX, lobe.Center.X + radius);
            maxY = Mathf.Max(maxY, lobe.Center.Y + radius);
        }

        return new Rect2(minX, minY, Mathf.Max(0.0f, maxX - minX), Mathf.Max(0.0f, maxY - minY));
    }

    private static float GetLobeBoundsRadius(ForestLobeSiteV2 lobe)
    {
        float aspect = Mathf.Clamp(lobe.Aspect, 0.45f, 1.85f);
        return lobe.Radius * Mathf.Max(aspect, 1.0f / aspect);
    }

    private static Rect2 CalculateQuarryBounds(Vector2 center, float radius, IReadOnlyList<QuarryPatchV3> patches)
    {
        float minX = center.X - radius;
        float minY = center.Y - radius;
        float maxX = center.X + radius;
        float maxY = center.Y + radius;
        foreach (QuarryPatchV3 patch in patches)
        {
            float patchRadius = GetQuarryPatchBoundsRadius(patch) + 8.0f;
            minX = Mathf.Min(minX, patch.Center.X - patchRadius);
            minY = Mathf.Min(minY, patch.Center.Y - patchRadius);
            maxX = Mathf.Max(maxX, patch.Center.X + patchRadius);
            maxY = Mathf.Max(maxY, patch.Center.Y + patchRadius);
        }

        return new Rect2(minX, minY, Mathf.Max(0.0f, maxX - minX), Mathf.Max(0.0f, maxY - minY));
    }

    private static float GetQuarryPatchBoundsRadius(QuarryPatchV3 patch)
    {
        float aspect = Mathf.Clamp(patch.Aspect, 0.50f, 1.70f);
        return patch.Radius * Mathf.Max(aspect, 1.0f / aspect);
    }

    private static bool TryGetLocalCircleBounds(
        FlatlandChunkGenerationContextV2 context,
        Vector2I center,
        float radius,
        out int minX,
        out int minY,
        out int maxX,
        out int maxY)
    {
        int minGlobalX = Mathf.FloorToInt(center.X - radius);
        int minGlobalY = Mathf.FloorToInt(center.Y - radius);
        int maxGlobalX = Mathf.CeilToInt(center.X + radius);
        int maxGlobalY = Mathf.CeilToInt(center.Y + radius);
        return TryGetLocalBounds(context, minGlobalX, minGlobalY, maxGlobalX, maxGlobalY, out minX, out minY, out maxX, out maxY);
    }

    private static bool TryGetLocalSegmentBounds(
        FlatlandChunkGenerationContextV2 context,
        Vector2 a,
        Vector2 b,
        float margin,
        out int minX,
        out int minY,
        out int maxX,
        out int maxY)
    {
        int minGlobalX = Mathf.FloorToInt(Mathf.Min(a.X, b.X) - margin);
        int minGlobalY = Mathf.FloorToInt(Mathf.Min(a.Y, b.Y) - margin);
        int maxGlobalX = Mathf.CeilToInt(Mathf.Max(a.X, b.X) + margin);
        int maxGlobalY = Mathf.CeilToInt(Mathf.Max(a.Y, b.Y) + margin);
        return TryGetLocalBounds(context, minGlobalX, minGlobalY, maxGlobalX, maxGlobalY, out minX, out minY, out maxX, out maxY);
    }

    private static bool TryGetLocalBounds(
        FlatlandChunkGenerationContextV2 context,
        int minGlobalX,
        int minGlobalY,
        int maxGlobalX,
        int maxGlobalY,
        out int minX,
        out int minY,
        out int maxX,
        out int maxY)
    {
        minX = Mathf.Clamp(minGlobalX - context.OriginGlobalCell.X, 0, ChunkDataV2.ChunkSize - 1);
        minY = Mathf.Clamp(minGlobalY - context.OriginGlobalCell.Y, 0, ChunkDataV2.ChunkSize - 1);
        maxX = Mathf.Clamp(maxGlobalX - context.OriginGlobalCell.X, 0, ChunkDataV2.ChunkSize - 1);
        maxY = Mathf.Clamp(maxGlobalY - context.OriginGlobalCell.Y, 0, ChunkDataV2.ChunkSize - 1);
        return maxGlobalX >= context.OriginGlobalCell.X
            && maxGlobalY >= context.OriginGlobalCell.Y
            && minGlobalX < context.OriginGlobalCell.X + ChunkDataV2.ChunkSize
            && minGlobalY < context.OriginGlobalCell.Y + ChunkDataV2.ChunkSize;
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

    private float GetRoadWear(Vector2I cell, int wearSeed, Vector2I chunkCoord)
    {
        float coarse = StableUnitFloat(cell.X / 5 + chunkCoord.X * 11, cell.Y / 5 + chunkCoord.Y * 13, wearSeed);
        float detail = StableUnitFloat(cell.X, cell.Y, wearSeed + 17);
        float wear = Mathf.Lerp(coarse, detail, Mathf.Clamp(_settings.V3RoadWearFrequency, 0.0f, 1.0f));
        return Mathf.Clamp(wear, 0.0f, 1.0f);
    }

    private float GetVillageClearingAt(FlatlandChunkGenerationContextV2 context, Vector2I cell)
    {
        float strongest = 0.0f;
        foreach (VillageSiteV2 village in context.RelevantVillages)
        {
            float clearRadius = village.Radius + _settings.V3ForestVillageClearRadius;
            float radiusNoise = ValueNoise(cell.X * 0.045f, cell.Y * 0.045f, village.Id + 6111);
            clearRadius *= Mathf.Lerp(0.88f, 1.12f, radiusNoise);
            float distance = cell.DistanceTo(village.Center);
            if (distance >= clearRadius)
            {
                continue;
            }

            float t = 1.0f - distance / Mathf.Max(1.0f, clearRadius);
            strongest = Mathf.Max(strongest, t * t);
        }

        return strongest;
    }

    private float GetRoadClearingAt(FlatlandChunkGenerationContextV2 context, Vector2I cell)
    {
        if (!WorldGenerationLayerSettingsV2.EnableRoads)
        {
            return 0.0f;
        }

        float strongest = 0.0f;
        foreach (RoadPathV2 road in context.RelevantRoads)
        {
            float clearRadius = road.Width + _settings.V3ForestRoadClearRadius;
            float radiusNoise = ValueNoise(cell.X * 0.055f, cell.Y * 0.055f, road.Id + 6211);
            clearRadius *= Mathf.Lerp(0.82f, 1.18f, radiusNoise);
            float distance = road.DistanceToPath(cell);
            if (distance >= clearRadius)
            {
                continue;
            }

            strongest = Mathf.Max(strongest, 1.0f - distance / Mathf.Max(1.0f, clearRadius));
        }

        return strongest;
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
        float best = float.MaxValue;
        foreach (VillageSiteV2 village in context.RelevantVillages)
        {
            best = Mathf.Min(best, chunkCenter.DistanceTo(village.Center));
        }

        return best;
    }

    private bool TryGetStartingVillage(out VillageSiteV2? village)
    {
        foreach (VillageSiteV2 candidate in _villages)
        {
            if (candidate.Id == _startingVillageId)
            {
                village = candidate;
                return true;
            }
        }

        village = null;
        return false;
    }

    private Vector2I GetStartingVillageCenter()
    {
        return TryGetStartingVillage(out VillageSiteV2? village) && village != null
            ? village.Center
            : WorldSize.CenterCell;
    }

    public string GetDebugSummary()
    {
        return $"V3 villages: count={VillageCount} startId={StartingVillageId} startCenter={StartingVillageCenter} spawn={PlayerSpawnCell} nearestCenter={NearestToWorldCenterDistance:0.0} roads={RoadCount} primary={PrimaryRoadCount} extra={ExtraRoadCount} roadLayer={RoadLayerEnabled} forestRegions={ForestRegionCount} majorForests={MajorForestRegionCount} minorForests={MinorForestPatchCount} forestLayer={ForestLayerEnabled} quarryRegions={QuarryRegionCount} majorQuarries={MajorQuarryCount} minorQuarries={MinorQuarryCount} quarryLayer={QuarryLayerEnabled} rejectedQuarries={RejectedQuarryPlacementCount}";
    }

    private static FlatlandCellSampleV2 CreateOutOfBoundsSample(Vector2I globalCell)
    {
        return new FlatlandCellSampleV2
        {
            GlobalCellCoord = globalCell,
            IsBuildRestricted = true,
            IsWalkable = false,
            Biome = BiomeTypeV2.DryWasteland,
            TileType = TileType.Wasteland
        };
    }

    private static uint MakeSeed(int worldSeed, int preset, int salt)
    {
        uint hash = 2166136261u;
        Mix(ref hash, worldSeed);
        Mix(ref hash, preset);
        Mix(ref hash, salt);
        return hash == 0 ? 1u : hash;
    }

    private static float StableUnitFloat(int x, int y, int salt)
    {
        uint hash = 2166136261u;
        Mix(ref hash, x);
        Mix(ref hash, y);
        Mix(ref hash, salt);
        return (hash & 0x00ffffffu) / 16777215.0f;
    }

    private static float FractalValueNoise(float x, float y, int salt, int octaves)
    {
        float value = 0.0f;
        float amplitude = 1.0f;
        float frequency = 1.0f;
        float totalAmplitude = 0.0f;
        for (int i = 0; i < octaves; i++)
        {
            value += ValueNoise(x * frequency, y * frequency, salt + i * 97) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.0f;
        }

        return totalAmplitude <= 0.0f ? 0.0f : value / totalAmplitude;
    }

    private static float ValueNoise(float x, float y, int salt)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        float tx = Fade(x - x0);
        float ty = Fade(y - y0);
        float a = StableUnitFloat(x0, y0, salt);
        float b = StableUnitFloat(x1, y0, salt);
        float c = StableUnitFloat(x0, y1, salt);
        float d = StableUnitFloat(x1, y1, salt);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float Fade(float t)
    {
        return t * t * (3.0f - 2.0f * t);
    }

    private static void Mix(ref uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }
    }

    private struct DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0 ? 1u : seed;
        }

        public float NextUnit()
        {
            _state = unchecked(_state * 1664525u + 1013904223u);
            return ((_state >> 8) & 0x00ffffffu) / 16777215.0f;
        }

        public int RangeInclusive(int minValue, int maxValue)
        {
            if (maxValue <= minValue)
            {
                return minValue;
            }

            int span = maxValue - minValue + 1;
            return minValue + Mathf.FloorToInt(NextUnit() * span) % span;
        }

        public float Range(float minValue, float maxValue)
        {
            return maxValue <= minValue ? minValue : Mathf.Lerp(minValue, maxValue, NextUnit());
        }
    }
}
