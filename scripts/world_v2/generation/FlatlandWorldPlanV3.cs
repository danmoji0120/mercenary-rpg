using Godot;
using System.Collections.Generic;

namespace WorldV2;

public sealed class FlatlandWorldPlanV3
{
    private WorldGenerationRequestV2 _request = WorldGenerationRequestV2.CreateDevDefault(20260707);
    private WorldGenerationSettingsV2 _settings = WorldGenerationSettingsV2.Default;
    private readonly List<VillageSiteV2> _villages = new();
    private readonly List<RoadPathV2> _roads = new();
    private readonly List<RoadTargetAnchorV3> _roadTargetAnchors = new();
    private readonly RoadGraphV3 _roadGraph = new();
    private readonly List<ForestClusterSiteV2> _forestClusters = new();
    private readonly List<ForestRegionV3> _forestRegions = new();
    private readonly List<QuarryClusterV3> _quarryClusters = new();
    private readonly List<QuarryRegionV3> _quarryRegions = new();
    private readonly List<RuinSiteV3> _ruinSites = new();
    private readonly List<DungeonEntranceSiteV3> _dungeonEntrances = new();
    private readonly List<BanditCampSiteV3> _banditCamps = new();
    private readonly List<FactionOutpostSiteV3> _factionOutposts = new();
    private readonly List<BiomeRegionV3> _biomeRegions = new();
    private readonly int[] _forestBiomeDistribution = new int[5];
    private readonly int[] _quarryBiomeDistribution = new int[5];
    private readonly int[] _ruinBiomeDistribution = new int[5];
    private readonly int[] _dungeonEntranceBiomeDistribution = new int[5];
    private readonly int[] _dungeonEntranceKindDistribution = new int[5];
    private readonly int[] _banditCampBiomeDistribution = new int[5];
    private readonly int[] _banditCampKindDistribution = new int[5];
    private readonly int[] _factionOutpostBiomeDistribution = new int[5];
    private readonly int[] _factionOutpostKindDistribution = new int[5];
    private readonly int[] _factionOutpostOwnerDistribution = new int[5];
    private readonly int[] _settlementRoleDistribution = new int[8];
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
    public int HamletCount => CountSettlementsByScale(VillageScaleV2.Hamlet);
    public int VillageTierCount => CountSettlementsByScale(VillageScaleV2.Village);
    public int LargeVillageCount => CountSettlementsByScale(VillageScaleV2.LargeVillage);
    public int TownCount => CountSettlementsByScale(VillageScaleV2.Town);
    public int CityCandidateCount => CountSettlementsByScale(VillageScaleV2.CityCandidate);
    public string SettlementRoleDistribution => FormatRoleDistribution(_settlementRoleDistribution);
    public VillageScaleV2 StartingSettlementTier => TryGetStartingVillage(out VillageSiteV2? village) && village != null ? village.Scale : VillageScaleV2.Village;
    public SettlementRoleV3 StartingSettlementRole => TryGetStartingVillage(out VillageSiteV2? village) && village != null ? village.Role : SettlementRoleV3.StartingSettlement;
    public IReadOnlyList<VillageSiteV2> Villages => _villages;
    public int RoadCount => _roads.Count;
    public int PrimaryRoadCount { get; private set; }
    public int SecondaryRoadCount { get; private set; }
    public int ExtraRoadCount { get; private set; }
    public int BranchRoadCount { get; private set; }
    public int RoadNodeCount => _roadGraph.NodeCount;
    public int RoadJunctionCount { get; private set; }
    public int SharedTrunkCount { get; private set; }
    public int MergedRoadCandidateCount { get; private set; }
    public int RejectedRoadJunctionCount { get; private set; }
    public int MaxRoadJunctionDegree { get; private set; }
    public int RejectedHighDegreeJunctionCount { get; private set; }
    public int RejectedRoadCrossingCount { get; private set; }
    public int RejectedRoadTooLongCount { get; private set; }
    public int RoadTargetAnchorCount => _roadTargetAnchors.Count;
    public int RoadTargetQuarryCount { get; private set; }
    public int RoadTargetRuinCount { get; private set; }
    public int RoadTargetDungeonEntranceCount { get; private set; }
    public int RoadTargetBanditCampCount { get; private set; }
    public int RoadTargetFactionOutpostCount { get; private set; }
    public int RoadTargetForestEdgeCount { get; private set; }
    public int RoadTargetWorldEdgeExitCount { get; private set; }
    public int FutureRoadTargetCount { get; private set; }
    public int RejectedRoadTargetCount { get; private set; }
    public int RejectedBranchRoadCount { get; private set; }
    public bool RoadLayerEnabled => WorldGenerationLayerSettingsV2.EnableRoads;
    public IReadOnlyList<RoadPathV2> Roads => _roads;
    public IReadOnlyList<RoadTargetAnchorV3> RoadTargetAnchors => _roadTargetAnchors;
    public int ForestClusterCount => _forestRegions.Count;
    public int ForestRegionCount => _forestRegions.Count;
    public int MajorForestRegionCount { get; private set; }
    public int MinorForestPatchCount { get; private set; }
    public int LargeForestClusterCount => MajorForestRegionCount;
    public int RejectedForestPlacementCount { get; private set; }
    public string ForestBiomeDistribution => FormatBiomeDistribution(_forestBiomeDistribution);
    public bool ForestLayerEnabled => WorldGenerationLayerSettingsV2.EnableForests;
    public IReadOnlyList<ForestClusterSiteV2> ForestClusters => _forestClusters;
    public IReadOnlyList<ForestRegionV3> ForestRegions => _forestRegions;
    public int QuarryClusterCount => _quarryRegions.Count;
    public int QuarryRegionCount => _quarryRegions.Count;
    public int MajorQuarryCount { get; private set; }
    public int MinorQuarryCount { get; private set; }
    public int RejectedQuarryPlacementCount { get; private set; }
    public string QuarryBiomeDistribution => FormatBiomeDistribution(_quarryBiomeDistribution);
    public bool QuarryLayerEnabled => WorldGenerationLayerSettingsV2.EnableQuarries;
    public IReadOnlyList<QuarryClusterV3> QuarryClusters => _quarryClusters;
    public IReadOnlyList<QuarryRegionV3> QuarryRegions => _quarryRegions;
    public int RuinSiteCount => _ruinSites.Count;
    public int RoadLinkedRuinCount
    {
        get
        {
            int count = 0;
            foreach (RuinSiteV3 ruin in _ruinSites)
            {
                if (ruin.LinkedRoadId > 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
    public int RejectedRuinPlacementCount { get; private set; }
    public string RuinBiomeDistribution => FormatBiomeDistribution(_ruinBiomeDistribution);
    public bool RuinLayerEnabled => WorldGenerationLayerSettingsV2.EnableRuins;
    public IReadOnlyList<RuinSiteV3> RuinSites => _ruinSites;
    public int DungeonEntranceCount => _dungeonEntrances.Count;
    public int RoadLinkedDungeonEntranceCount
    {
        get
        {
            int count = 0;
            foreach (DungeonEntranceSiteV3 dungeon in _dungeonEntrances)
            {
                if (dungeon.LinkedRoadId > 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
    public int RejectedDungeonEntrancePlacementCount { get; private set; }
    public string DungeonEntranceKindDistribution => FormatDungeonEntranceKindDistribution(_dungeonEntranceKindDistribution);
    public string DungeonEntranceBiomeDistribution => FormatBiomeDistribution(_dungeonEntranceBiomeDistribution);
    public bool DungeonLayerEnabled => WorldGenerationLayerSettingsV2.EnableDungeons;
    public IReadOnlyList<DungeonEntranceSiteV3> DungeonEntrances => _dungeonEntrances;
    public int BanditCampCount => _banditCamps.Count;
    public int RoadLinkedBanditCampCount
    {
        get
        {
            int count = 0;
            foreach (BanditCampSiteV3 camp in _banditCamps)
            {
                if (camp.LinkedRoadId > 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
    public int RejectedBanditCampPlacementCount { get; private set; }
    public string BanditCampKindDistribution => FormatBanditCampKindDistribution(_banditCampKindDistribution);
    public string BanditCampBiomeDistribution => FormatBiomeDistribution(_banditCampBiomeDistribution);
    public bool BanditLayerEnabled => WorldGenerationLayerSettingsV2.EnableBanditCamps;
    public IReadOnlyList<BanditCampSiteV3> BanditCamps => _banditCamps;
    public int FactionOutpostCount => _factionOutposts.Count;
    public int RoadLinkedFactionOutpostCount
    {
        get
        {
            int count = 0;
            foreach (FactionOutpostSiteV3 outpost in _factionOutposts)
            {
                if (outpost.LinkedRoadId > 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
    public int RejectedFactionOutpostPlacementCount { get; private set; }
    public string FactionOutpostKindDistribution => FormatFactionOutpostKindDistribution(_factionOutpostKindDistribution);
    public string FactionOutpostOwnerDistribution => FormatFactionOutpostOwnerDistribution(_factionOutpostOwnerDistribution);
    public string FactionOutpostBiomeDistribution => FormatBiomeDistribution(_factionOutpostBiomeDistribution);
    public bool FactionOutpostLayerEnabled => WorldGenerationLayerSettingsV2.EnableFactionOutposts;
    public IReadOnlyList<FactionOutpostSiteV3> FactionOutposts => _factionOutposts;
    public int BiomeRegionCount => _biomeRegions.Count;
    public int MajorBiomeRegionCount { get; private set; }
    public int MinorBiomeRegionCount { get; private set; }
    public int BiomeForestLandCount { get; private set; }
    public int BiomeRockyHillsCount { get; private set; }
    public int BiomeDrylandCount { get; private set; }
    public int BiomeWastelandCount { get; private set; }
    public float AverageMajorBiomeRadius { get; private set; }
    public float AverageMinorBiomeRadius { get; private set; }
    public string BiomeResolveMode => WorldGenerationLayerSettingsV2.EnableBiomes ? "MacroZone" : "Disabled";
    public bool BiomeLayerEnabled => WorldGenerationLayerSettingsV2.EnableBiomes;
    public IReadOnlyList<BiomeRegionV3> BiomeRegions => _biomeRegions;
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
        GenerateBiomeRegions();
        GenerateRiversPlaceholder();
        GenerateVillagesPlaceholder();
        GenerateRoads();
        GenerateQuarries();
        GenerateForests();
        GenerateRuins();
        GenerateDungeonEntrances();
        GenerateBanditCamps();
        GenerateFactionOutposts();
        AssignSettlementRoles();
        GenerateRoadTargetAnchors();
        GenerateBranchRoads();
        GenerateLandmarksPlaceholder();
        BuildSpatialIndexPlaceholder();
        _isBuilt = true;
    }

    public void ClearCaches()
    {
        _villages.Clear();
        _roads.Clear();
        _roadTargetAnchors.Clear();
        _roadGraph.Clear();
        _forestClusters.Clear();
        _forestRegions.Clear();
        _quarryClusters.Clear();
        _quarryRegions.Clear();
        _ruinSites.Clear();
        _dungeonEntrances.Clear();
        _banditCamps.Clear();
        _factionOutposts.Clear();
        _biomeRegions.Clear();
        PrimaryRoadCount = 0;
        SecondaryRoadCount = 0;
        ExtraRoadCount = 0;
        BranchRoadCount = 0;
        RoadJunctionCount = 0;
        SharedTrunkCount = 0;
        MergedRoadCandidateCount = 0;
        RejectedRoadJunctionCount = 0;
        MaxRoadJunctionDegree = 0;
        RejectedHighDegreeJunctionCount = 0;
        RejectedRoadCrossingCount = 0;
        RejectedRoadTooLongCount = 0;
        RoadTargetQuarryCount = 0;
        RoadTargetRuinCount = 0;
        RoadTargetDungeonEntranceCount = 0;
        RoadTargetBanditCampCount = 0;
        RoadTargetFactionOutpostCount = 0;
        RoadTargetForestEdgeCount = 0;
        RoadTargetWorldEdgeExitCount = 0;
        FutureRoadTargetCount = 0;
        RejectedRoadTargetCount = 0;
        RejectedBranchRoadCount = 0;
        MajorForestRegionCount = 0;
        MinorForestPatchCount = 0;
        RejectedForestPlacementCount = 0;
        MajorQuarryCount = 0;
        MinorQuarryCount = 0;
        RejectedQuarryPlacementCount = 0;
        RejectedRuinPlacementCount = 0;
        RejectedDungeonEntrancePlacementCount = 0;
        RejectedBanditCampPlacementCount = 0;
        RejectedFactionOutpostPlacementCount = 0;
        MajorBiomeRegionCount = 0;
        MinorBiomeRegionCount = 0;
        BiomeForestLandCount = 0;
        BiomeRockyHillsCount = 0;
        BiomeDrylandCount = 0;
        BiomeWastelandCount = 0;
        AverageMajorBiomeRadius = 0.0f;
        AverageMinorBiomeRadius = 0.0f;
        _startingVillageId = -1;
        _playerSpawnCell = Vector2I.Zero;
        _nearestToWorldCenterDistance = 0.0f;
        ClearFeatureBiomeDistributions();
        ClearDistribution(_dungeonEntranceKindDistribution);
        ClearDistribution(_banditCampKindDistribution);
        ClearDistribution(_factionOutpostKindDistribution);
        ClearDistribution(_factionOutpostOwnerDistribution);
        ClearDistribution(_settlementRoleDistribution);
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
            BiomeKind = BiomeKindV3.Plains,
            Biome = BiomeTypeV2.Plains,
            TileType = TileType.Grass
        };

        ApplyBiomeSample(sample);
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

        if (WorldGenerationLayerSettingsV2.EnableBiomes)
        {
            foreach (BiomeRegionV3 biome in _biomeRegions)
            {
                if (biome.IsMajorRegion || biome.Bounds.Intersects(chunkRect, includeBorders: true))
                {
                    context.AddBiomeRegion(biome);
                }
            }
        }

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

        if (WorldGenerationLayerSettingsV2.EnableRuins)
        {
            foreach (RuinSiteV3 ruin in _ruinSites)
            {
                if (ruin.Bounds.Intersects(chunkRect, includeBorders: true))
                {
                    context.AddRuinSite(ruin);
                }
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableDungeons)
        {
            foreach (DungeonEntranceSiteV3 dungeon in _dungeonEntrances)
            {
                if (dungeon.Bounds.Intersects(chunkRect, includeBorders: true))
                {
                    context.AddDungeonEntrance(dungeon);
                }
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableBanditCamps)
        {
            foreach (BanditCampSiteV3 camp in _banditCamps)
            {
                if (camp.Bounds.Intersects(chunkRect, includeBorders: true))
                {
                    context.AddBanditCamp(camp);
                }
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableFactionOutposts)
        {
            foreach (FactionOutpostSiteV3 outpost in _factionOutposts)
            {
                if (outpost.Bounds.Intersects(chunkRect, includeBorders: true))
                {
                    context.AddFactionOutpost(outpost);
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
        if (WorldGenerationLayerSettingsV2.EnableBiomes)
        {
            RasterBiomes(context);
        }

        RasterVillages(context);

        if (WorldGenerationLayerSettingsV2.EnableRoads)
        {
            RasterRoads(context);
        }

        if (WorldGenerationLayerSettingsV2.EnableFactionOutposts)
        {
            RasterFactionOutposts(context);
        }

        if (WorldGenerationLayerSettingsV2.EnableBanditCamps)
        {
            RasterBanditCamps(context);
        }

        if (WorldGenerationLayerSettingsV2.EnableRuins)
        {
            RasterRuins(context);
        }

        if (WorldGenerationLayerSettingsV2.EnableDungeons)
        {
            RasterDungeonEntrances(context);
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
            BiomeKind = context.BiomeKind[index],
            Biome = MapBiomeKindToBiomeType(context.BiomeKind[index]),
            TileType = TileType.Grass,
            IsRoad = context.IsRoad[index],
            IsVillage = context.IsVillage[index],
            IsStartingVillage = context.IsStartingVillage[index],
            IsLandmark = context.IsLandmark[index],
            IsQuarry = context.IsQuarry[index],
            HasOreSpot = context.HasOreSpot[index],
            IsDungeonEntrance = context.IsDungeonEntrance[index],
            DungeonEntranceKind = context.DungeonEntranceKind[index],
            IsBanditCamp = context.IsBanditCamp[index],
            BanditCampKind = context.BanditCampKind[index],
            IsFactionOutpost = context.IsFactionOutpost[index],
            FactionOutpostKind = context.FactionOutpostKind[index],
            FactionOutpostOwner = context.FactionOutpostOwner[index],
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

    private void GenerateBiomeRegions()
    {
        _biomeRegions.Clear();
        MajorBiomeRegionCount = 0;
        MinorBiomeRegionCount = 0;
        BiomeForestLandCount = 0;
        BiomeRockyHillsCount = 0;
        BiomeDrylandCount = 0;
        BiomeWastelandCount = 0;
        AverageMajorBiomeRadius = 0.0f;
        AverageMinorBiomeRadius = 0.0f;

        if (!WorldGenerationLayerSettingsV2.EnableBiomes)
        {
            return;
        }

        GetTargetBiomeRegionCounts(out int majorTarget, out int minorTarget);
        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 2101));
        int nextId = 1;
        PlaceBiomeRegions(majorTarget, major: true, ref nextId, ref random);
        PlaceBiomeRegions(minorTarget, major: false, ref nextId, ref random);
        UpdateBiomeRadiusAverages();
    }

    private void PlaceBiomeRegions(int targetCount, bool major, ref int nextId, ref DeterministicRandom random)
    {
        if (targetCount <= 0)
        {
            return;
        }

        float mapScale = Mathf.Min(WorldSize.WidthCells, WorldSize.HeightCells);
        float minRatio = major
            ? Mathf.Clamp(_settings.V3MajorBiomeMinRadiusRatio, 0.03f, 0.45f)
            : Mathf.Clamp(_settings.V3MinorBiomeMinRadiusRatio, 0.02f, 0.25f);
        float maxRatio = major
            ? Mathf.Clamp(_settings.V3MajorBiomeMaxRadiusRatio, minRatio, 0.55f)
            : Mathf.Clamp(_settings.V3MinorBiomeMaxRadiusRatio, minRatio, 0.35f);
        float minRadius = Mathf.Max(96.0f, mapScale * minRatio);
        float maxRadius = Mathf.Max(minRadius, mapScale * maxRatio);
        float edgeMargin = Mathf.Max(64.0f, minRadius * (major ? 0.18f : 0.28f));
        int minX = Mathf.CeilToInt(edgeMargin);
        int minY = Mathf.CeilToInt(edgeMargin);
        int maxX = Mathf.FloorToInt(WorldSize.WidthCells - edgeMargin - 1.0f);
        int maxY = Mathf.FloorToInt(WorldSize.HeightCells - edgeMargin - 1.0f);
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        int placed = 0;
        int attempts = Mathf.Max(targetCount * 8, 24);
        for (int attempt = 0; placed < targetCount && attempt < attempts; attempt++)
        {
            float radius = Mathf.Lerp(minRadius, maxRadius, major ? random.Range(0.45f, 1.0f) : random.Range(0.12f, 1.0f));
            Vector2 center = new(random.RangeInclusive(minX, maxX), random.RangeInclusive(minY, maxY));
            BiomeKindV3 kind = PickBiomeKind(nextId, placed, major, ref random);
            BiomeRegionV3 region = CreateBiomeRegion(nextId++, kind, center, radius, major, ref random);
            _biomeRegions.Add(region);
            placed++;
            if (major)
            {
                MajorBiomeRegionCount++;
            }
            else
            {
                MinorBiomeRegionCount++;
            }

            IncrementBiomeKindCount(kind);
        }
    }

    private void UpdateBiomeRadiusAverages()
    {
        float majorSum = 0.0f;
        float minorSum = 0.0f;
        int majorCount = 0;
        int minorCount = 0;
        foreach (BiomeRegionV3 region in _biomeRegions)
        {
            if (region.IsMajorRegion)
            {
                majorSum += region.ApproxRadius;
                majorCount++;
            }
            else
            {
                minorSum += region.ApproxRadius;
                minorCount++;
            }
        }

        AverageMajorBiomeRadius = majorCount > 0 ? majorSum / majorCount : 0.0f;
        AverageMinorBiomeRadius = minorCount > 0 ? minorSum / minorCount : 0.0f;
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
        ClearDistribution(_settlementRoleDistribution);

        int targetCount = GetTargetVillageCount();
        if (targetCount <= 0)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 3001));
        float edgeMargin = Mathf.Max(_settings.V3VillageEdgeMargin, Mathf.Max(_settings.V3TownRadius, _settings.V3CityCandidateRadius) + 48.0f);
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
        GetTargetSettlementScaleCounts(
            targetCount,
            out int cityRemaining,
            out int townRemaining,
            out int largeRemaining,
            out int villageRemaining,
            out int hamletRemaining);

        while (_villages.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            VillageScaleV2 scale = PickScaleFromQuota(
                ref cityRemaining,
                ref townRemaining,
                ref largeRemaining,
                ref villageRemaining,
                ref hamletRemaining,
                ref random);
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
            ConsumeSettlementScaleQuota(
                scale,
                ref cityRemaining,
                ref townRemaining,
                ref largeRemaining,
                ref villageRemaining,
                ref hamletRemaining);
        }

        SelectStartingVillage();
    }

    private void GenerateRoads()
    {
        _roads.Clear();
        _roadGraph.Clear();
        PrimaryRoadCount = 0;
        SecondaryRoadCount = 0;
        ExtraRoadCount = 0;
        BranchRoadCount = 0;
        RoadJunctionCount = 0;
        SharedTrunkCount = 0;
        MergedRoadCandidateCount = 0;
        RejectedRoadJunctionCount = 0;
        MaxRoadJunctionDegree = 0;
        RejectedHighDegreeJunctionCount = 0;
        RejectedRoadCrossingCount = 0;
        RejectedRoadTooLongCount = 0;
        RejectedBranchRoadCount = 0;

        if (!WorldGenerationLayerSettingsV2.EnableRoads || _villages.Count <= 1)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 4101));
        BuildVillageRoadGraph(ref random);
        ConvertRoadGraphToPaths(ref random);
    }

    private void BuildVillageRoadGraph(ref DeterministicRandom random)
    {
        Dictionary<int, int> nodeIdByVillageId = new();
        int nextNodeId = 1;
        foreach (VillageSiteV2 village in _villages)
        {
            int nodeId = nextNodeId++;
            nodeIdByVillageId[village.Id] = nodeId;
            _roadGraph.AddNode(new RoadNodeV3
            {
                Id = nodeId,
                Kind = RoadNodeKindV3.Village,
                Position = new Vector2(village.Center.X + 0.5f, village.Center.Y + 0.5f),
                VillageId = village.Id,
                Seed = HashIntId(village.Id, WorldSeed, 8411)
            });
        }

        List<RoadCandidateEdgeV3> selectedEdges = BuildLocalMstRoadCandidates(ref random);
        AddSharedExitRoadGraph(selectedEdges, nodeIdByVillageId, ref nextNodeId, ref random);
        MaxRoadJunctionDegree = CalculateMaxJunctionDegree();
    }

    private List<RoadCandidateEdgeV3> BuildLocalMstRoadCandidates(ref DeterministicRandom random)
    {
        List<RoadCandidateEdgeV3> candidates = BuildNearestNeighborRoadCandidates();
        candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
        DisjointSetV3 disjointSet = new(_villages.Count);
        Dictionary<int, int> villageIndexById = new();
        for (int i = 0; i < _villages.Count; i++)
        {
            villageIndexById[_villages[i].Id] = i;
        }

        List<RoadCandidateEdgeV3> selected = new();
        foreach (RoadCandidateEdgeV3 candidate in candidates)
        {
            int a = villageIndexById[candidate.From.Id];
            int b = villageIndexById[candidate.To.Id];
            if (disjointSet.Find(a) == disjointSet.Find(b))
            {
                continue;
            }

            if (!CanAcceptRoadCandidate(candidate, selected, force: false))
            {
                continue;
            }

            selected.Add(candidate);
            disjointSet.Union(a, b);
        }

        if (selected.Count < _villages.Count - 1)
        {
            List<RoadCandidateEdgeV3> allCandidates = BuildAllVillageRoadCandidates();
            allCandidates.Sort((a, b) => a.Score.CompareTo(b.Score));
            foreach (RoadCandidateEdgeV3 candidate in allCandidates)
            {
                int a = villageIndexById[candidate.From.Id];
                int b = villageIndexById[candidate.To.Id];
                if (disjointSet.Find(a) == disjointSet.Find(b))
                {
                    continue;
                }

                if (!CanAcceptRoadCandidate(candidate, selected, force: false))
                {
                    continue;
                }

                selected.Add(candidate);
                disjointSet.Union(a, b);
                if (selected.Count >= _villages.Count - 1)
                {
                    break;
                }
            }

            foreach (RoadCandidateEdgeV3 candidate in allCandidates)
            {
                if (selected.Count >= _villages.Count - 1)
                {
                    break;
                }

                int a = villageIndexById[candidate.From.Id];
                int b = villageIndexById[candidate.To.Id];
                if (disjointSet.Find(a) == disjointSet.Find(b))
                {
                    continue;
                }

                selected.Add(candidate);
                disjointSet.Union(a, b);
            }
        }

        AddSparseExtraVillageCandidateLinks(selected, ref random);
        return selected;
    }

    private List<RoadCandidateEdgeV3> BuildNearestNeighborRoadCandidates()
    {
        int neighborCount = Mathf.Clamp(_settings.V3RoadNearestNeighborCount, 1, 8);
        if (_villages.Count > 96)
        {
            return BuildNearestNeighborRoadCandidatesSpatial(neighborCount);
        }

        HashSet<long> seenPairs = new();
        List<RoadCandidateEdgeV3> candidates = new();
        foreach (VillageSiteV2 from in _villages)
        {
            List<RoadCandidateEdgeV3> nearest = new();
            foreach (VillageSiteV2 to in _villages)
            {
                if (from.Id == to.Id)
                {
                    continue;
                }

                nearest.Add(CreateRoadCandidate(from, to, false));
            }

            nearest.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            int count = Mathf.Min(neighborCount, nearest.Count);
            for (int i = 0; i < count; i++)
            {
                RoadCandidateEdgeV3 candidate = nearest[i];
                long key = MakePairKey(candidate.From.Id, candidate.To.Id);
                if (seenPairs.Add(key))
                {
                    candidates.Add(candidate);
                }
            }
        }

        return candidates;
    }

    private List<RoadCandidateEdgeV3> BuildNearestNeighborRoadCandidatesSpatial(int neighborCount)
    {
        float bucketSize = Mathf.Max(256.0f, GetMaxLocalRoadLength());
        Dictionary<Vector2I, List<VillageSiteV2>> buckets = BuildVillageBuckets(bucketSize);
        HashSet<long> seenPairs = new();
        List<RoadCandidateEdgeV3> candidates = new();

        foreach (VillageSiteV2 from in _villages)
        {
            Vector2I bucket = GetVillageBucket(from, bucketSize);
            List<RoadCandidateEdgeV3> nearest = new();
            int searchRadius = 1;
            while (nearest.Count < neighborCount && searchRadius <= 3)
            {
                nearest.Clear();
                for (int y = -searchRadius; y <= searchRadius; y++)
                {
                    for (int x = -searchRadius; x <= searchRadius; x++)
                    {
                        Vector2I key = new(bucket.X + x, bucket.Y + y);
                        if (!buckets.TryGetValue(key, out List<VillageSiteV2>? bucketVillages))
                        {
                            continue;
                        }

                        foreach (VillageSiteV2 to in bucketVillages)
                        {
                            if (from.Id == to.Id)
                            {
                                continue;
                            }

                            nearest.Add(CreateRoadCandidate(from, to, false));
                        }
                    }
                }

                searchRadius++;
            }

            nearest.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            int count = Mathf.Min(neighborCount, nearest.Count);
            for (int i = 0; i < count; i++)
            {
                RoadCandidateEdgeV3 candidate = nearest[i];
                long key = MakePairKey(candidate.From.Id, candidate.To.Id);
                if (seenPairs.Add(key))
                {
                    candidates.Add(candidate);
                }
            }
        }

        return candidates;
    }

    private List<RoadCandidateEdgeV3> BuildAllVillageRoadCandidates()
    {
        if (_villages.Count > 96)
        {
            List<RoadCandidateEdgeV3> limitedCandidates = BuildDistanceLimitedVillageRoadCandidates();
            if (CandidateSetConnectsAllVillages(limitedCandidates))
            {
                return limitedCandidates;
            }
        }

        List<RoadCandidateEdgeV3> candidates = new();
        for (int i = 0; i < _villages.Count; i++)
        {
            for (int j = i + 1; j < _villages.Count; j++)
            {
                candidates.Add(CreateRoadCandidate(_villages[i], _villages[j], false));
            }
        }

        return candidates;
    }

    private bool CandidateSetConnectsAllVillages(IReadOnlyList<RoadCandidateEdgeV3> candidates)
    {
        if (_villages.Count <= 1)
        {
            return true;
        }

        Dictionary<int, int> villageIndexById = new();
        for (int i = 0; i < _villages.Count; i++)
        {
            villageIndexById[_villages[i].Id] = i;
        }

        DisjointSetV3 disjointSet = new(_villages.Count);
        foreach (RoadCandidateEdgeV3 candidate in candidates)
        {
            if (!villageIndexById.TryGetValue(candidate.From.Id, out int a)
                || !villageIndexById.TryGetValue(candidate.To.Id, out int b))
            {
                continue;
            }

            disjointSet.Union(a, b);
        }

        int root = disjointSet.Find(0);
        for (int i = 1; i < _villages.Count; i++)
        {
            if (disjointSet.Find(i) != root)
            {
                return false;
            }
        }

        return true;
    }

    private List<RoadCandidateEdgeV3> BuildDistanceLimitedVillageRoadCandidates()
    {
        float maxDistance = GetMaxLocalRoadLength() * 1.35f;
        float bucketSize = Mathf.Max(256.0f, maxDistance);
        Dictionary<Vector2I, List<VillageSiteV2>> buckets = BuildVillageBuckets(bucketSize);
        HashSet<long> seenPairs = new();
        List<RoadCandidateEdgeV3> candidates = new();

        foreach (VillageSiteV2 from in _villages)
        {
            Vector2I bucket = GetVillageBucket(from, bucketSize);
            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    Vector2I key = new(bucket.X + x, bucket.Y + y);
                    if (!buckets.TryGetValue(key, out List<VillageSiteV2>? bucketVillages))
                    {
                        continue;
                    }

                    foreach (VillageSiteV2 to in bucketVillages)
                    {
                        if (from.Id >= to.Id || from.Center.DistanceTo(to.Center) > maxDistance)
                        {
                            continue;
                        }

                        long pairKey = MakePairKey(from.Id, to.Id);
                        if (seenPairs.Add(pairKey))
                        {
                            candidates.Add(CreateRoadCandidate(from, to, false));
                        }
                    }
                }
            }
        }

        return candidates;
    }

    private Dictionary<Vector2I, List<VillageSiteV2>> BuildVillageBuckets(float bucketSize)
    {
        Dictionary<Vector2I, List<VillageSiteV2>> buckets = new();
        foreach (VillageSiteV2 village in _villages)
        {
            Vector2I bucket = GetVillageBucket(village, bucketSize);
            if (!buckets.TryGetValue(bucket, out List<VillageSiteV2>? bucketVillages))
            {
                bucketVillages = new List<VillageSiteV2>();
                buckets[bucket] = bucketVillages;
            }

            bucketVillages.Add(village);
        }

        return buckets;
    }

    private static Vector2I GetVillageBucket(VillageSiteV2 village, float bucketSize)
    {
        return new Vector2I(
            Mathf.FloorToInt(village.Center.X / bucketSize),
            Mathf.FloorToInt(village.Center.Y / bucketSize));
    }

    private RoadCandidateEdgeV3 CreateRoadCandidate(VillageSiteV2 from, VillageSiteV2 to, bool isExtra)
    {
        float distance = from.Center.DistanceTo(to.Center);
        float hubWeight = (GetVillageConnectionPriority(from) + GetVillageConnectionPriority(to)) * 0.5f;
        return new RoadCandidateEdgeV3
        {
            From = from,
            To = to,
            Distance = distance,
            Score = distance / Mathf.Max(0.5f, hubWeight),
            IsExtra = isExtra
        };
    }

    private bool CanAcceptRoadCandidate(RoadCandidateEdgeV3 candidate, IReadOnlyList<RoadCandidateEdgeV3> accepted, bool force)
    {
        if (!force && candidate.Distance > GetMaxLocalRoadLength())
        {
            RejectedRoadTooLongCount++;
            return false;
        }

        int crossings = 0;
        Vector2 a = GetVillagePoint(candidate.From);
        Vector2 b = GetVillagePoint(candidate.To);
        foreach (RoadCandidateEdgeV3 existing in accepted)
        {
            if (candidate.SharesEndpoint(existing))
            {
                continue;
            }

            if (SegmentsIntersect(a, b, GetVillagePoint(existing.From), GetVillagePoint(existing.To)))
            {
                crossings++;
                if (!force && crossings > Mathf.Max(0, _settings.V3MaxRoadCrossingsPerEdge))
                {
                    RejectedRoadCrossingCount++;
                    return false;
                }
            }
        }

        return true;
    }

    private void AddSparseExtraVillageCandidateLinks(List<RoadCandidateEdgeV3> selected, ref DeterministicRandom random)
    {
        int extraTarget = Mathf.RoundToInt(_villages.Count * GetExtraRoadRatio());
        if (extraTarget <= 0)
        {
            return;
        }

        HashSet<long> selectedPairs = new();
        foreach (RoadCandidateEdgeV3 edge in selected)
        {
            selectedPairs.Add(MakePairKey(edge.From.Id, edge.To.Id));
        }

        List<RoadCandidateEdgeV3> candidates = BuildNearestNeighborRoadCandidates();
        candidates.Sort((a, b) =>
        {
            float aScore = a.Score + StableUnitFloat(a.From.Id, a.To.Id, WorldSeed + 8501) * 40.0f;
            float bScore = b.Score + StableUnitFloat(b.From.Id, b.To.Id, WorldSeed + 8501) * 40.0f;
            return aScore.CompareTo(bScore);
        });

        int added = 0;
        foreach (RoadCandidateEdgeV3 candidate in candidates)
        {
            long key = MakePairKey(candidate.From.Id, candidate.To.Id);
            if (selectedPairs.Contains(key))
            {
                continue;
            }

            RoadCandidateEdgeV3 extra = candidate with { IsExtra = true };
            if (!CanAcceptRoadCandidate(extra, selected, force: false))
            {
                continue;
            }

            selected.Add(extra);
            selectedPairs.Add(key);
            added++;
            if (added >= extraTarget)
            {
                return;
            }
        }
    }

    private void AddSharedExitRoadGraph(
        List<RoadCandidateEdgeV3> selectedEdges,
        Dictionary<int, int> nodeIdByVillageId,
        ref int nextNodeId,
        ref DeterministicRandom random)
    {
        HashSet<int> consumedEdges = new();
        int nextEdgeId = 1;

        if (WorldGenerationLayerSettingsV2.EnableRoads && _settings.V3SharedExitTrunkEnabled)
        {
            List<VillageSiteV2> orderedVillages = new(_villages);
            orderedVillages.Sort((a, b) => GetVillageConnectionPriority(b).CompareTo(GetVillageConnectionPriority(a)));
            foreach (VillageSiteV2 village in orderedVillages)
            {
                AddSharedExitsForVillage(village, selectedEdges, consumedEdges, nodeIdByVillageId, ref nextNodeId, ref nextEdgeId, ref random);
            }
        }

        for (int i = 0; i < selectedEdges.Count; i++)
        {
            if (consumedEdges.Contains(i))
            {
                continue;
            }

            RoadCandidateEdgeV3 candidate = selectedEdges[i];
            RoadEdgeKindV3 kind = PickRoadEdgeKind(candidate);
            AddGraphEdge(nextEdgeId++, nodeIdByVillageId[candidate.From.Id], nodeIdByVillageId[candidate.To.Id], kind, candidate.IsExtra);
        }
    }

    private void AddSharedExitsForVillage(
        VillageSiteV2 village,
        IReadOnlyList<RoadCandidateEdgeV3> selectedEdges,
        HashSet<int> consumedEdges,
        Dictionary<int, int> nodeIdByVillageId,
        ref int nextNodeId,
        ref int nextEdgeId,
        ref DeterministicRandom random)
    {
        List<(int EdgeIndex, RoadCandidateEdgeV3 Edge, VillageSiteV2 Other, float Angle)> incident = new();
        for (int i = 0; i < selectedEdges.Count; i++)
        {
            if (consumedEdges.Contains(i))
            {
                continue;
            }

            RoadCandidateEdgeV3 edge = selectedEdges[i];
            VillageSiteV2? other = null;
            if (edge.From.Id == village.Id)
            {
                other = edge.To;
            }
            else if (edge.To.Id == village.Id)
            {
                other = edge.From;
            }

            if (other == null)
            {
                continue;
            }

            Vector2 delta = GetVillagePoint(other) - GetVillagePoint(village);
            float angle = Mathf.PosMod(Mathf.Atan2(delta.Y, delta.X), Mathf.Tau);
            incident.Add((i, edge, other, angle));
        }

        if (incident.Count < 2)
        {
            return;
        }

        incident.Sort((a, b) => a.Angle.CompareTo(b.Angle));
        float sectorRadians = Mathf.DegToRad(Mathf.Clamp(_settings.V3RoadDirectionSectorDegrees, 25.0f, 75.0f));
        for (int i = 0; i < incident.Count - 1; i++)
        {
            if (consumedEdges.Contains(incident[i].EdgeIndex))
            {
                continue;
            }

            for (int j = i + 1; j < incident.Count; j++)
            {
                if (consumedEdges.Contains(incident[j].EdgeIndex))
                {
                    continue;
                }

                float angleDiff = Mathf.Abs(Mathf.AngleDifference(incident[i].Angle, incident[j].Angle));
                if (angleDiff > sectorRadians)
                {
                    break;
                }

                if (!TryAddSharedExit(village, incident[i], incident[j], nodeIdByVillageId, ref nextNodeId, ref nextEdgeId, ref random))
                {
                    continue;
                }

                consumedEdges.Add(incident[i].EdgeIndex);
                consumedEdges.Add(incident[j].EdgeIndex);
                SharedTrunkCount++;
                MergedRoadCandidateCount++;
                break;
            }
        }
    }

    private bool TryAddSharedExit(
        VillageSiteV2 village,
        (int EdgeIndex, RoadCandidateEdgeV3 Edge, VillageSiteV2 Other, float Angle) first,
        (int EdgeIndex, RoadCandidateEdgeV3 Edge, VillageSiteV2 Other, float Angle) second,
        Dictionary<int, int> nodeIdByVillageId,
        ref int nextNodeId,
        ref int nextEdgeId,
        ref DeterministicRandom random)
    {
        int maxDegree = Mathf.Clamp(_settings.V3MaxRoadJunctionDegree, 2, 4);
        if (maxDegree < 3)
        {
            RejectedHighDegreeJunctionCount++;
            return false;
        }

        Vector2 origin = GetVillagePoint(village);
        Vector2 dirA = (GetVillagePoint(first.Other) - origin).Normalized();
        Vector2 dirB = (GetVillagePoint(second.Other) - origin).Normalized();
        Vector2 direction = (dirA + dirB).Normalized();
        if (direction.LengthSquared() <= 0.0001f)
        {
            return false;
        }

        float trunkLength = Mathf.Min(GetSharedExitTrunkMaxLength(), Mathf.Min(first.Edge.Distance, second.Edge.Distance) * 0.30f);
        trunkLength = Mathf.Max(village.Radius + _settings.V3RoadJunctionVillageClearance * 0.42f, trunkLength);
        Vector2 jitter = new Vector2(-direction.Y, direction.X) * random.Range(-12.0f, 12.0f);
        Vector2 junctionPosition = ClampPointToWorld(origin + direction * trunkLength + jitter, 72.0f);
        if (IsPointTooCloseToVillageCore(junctionPosition))
        {
            RejectedRoadJunctionCount++;
            return false;
        }

        int junctionNodeId = nextNodeId++;
        _roadGraph.AddNode(new RoadNodeV3
        {
            Id = junctionNodeId,
            Kind = RoadNodeKindV3.Junction,
            Position = junctionPosition,
            Seed = HashIntId(junctionNodeId, WorldSeed, 8511)
        });
        RoadJunctionCount++;

        AddGraphEdge(nextEdgeId++, nodeIdByVillageId[village.Id], junctionNodeId, RoadEdgeKindV3.Primary);
        AddGraphEdge(nextEdgeId++, junctionNodeId, nodeIdByVillageId[first.Other.Id], RoadEdgeKindV3.Secondary, first.Edge.IsExtra);
        AddGraphEdge(nextEdgeId++, junctionNodeId, nodeIdByVillageId[second.Other.Id], RoadEdgeKindV3.Secondary, second.Edge.IsExtra);
        return true;
    }

    private RoadEdgeKindV3 PickRoadEdgeKind(RoadCandidateEdgeV3 candidate)
    {
        if (!candidate.IsExtra
            && (candidate.From.Scale is VillageScaleV2.CityCandidate or VillageScaleV2.Town or VillageScaleV2.LargeVillage
                || candidate.To.Scale is VillageScaleV2.CityCandidate or VillageScaleV2.Town or VillageScaleV2.LargeVillage))
        {
            return RoadEdgeKindV3.Primary;
        }

        return RoadEdgeKindV3.Secondary;
    }

    private void AddGraphEdge(int edgeId, int fromNodeId, int toNodeId, RoadEdgeKindV3 kind, bool isExtraLink = false)
    {
        RoadKindV3 roadKind = kind switch
        {
            RoadEdgeKindV3.Primary => RoadKindV3.Primary,
            RoadEdgeKindV3.Branch => RoadKindV3.Branch,
            _ => RoadKindV3.Secondary
        };
        float width = Mathf.Max(0.75f, _settings.V3RoadWidth) * (roadKind switch
        {
            RoadKindV3.Primary => 1.0f,
            RoadKindV3.Branch => Mathf.Clamp(_settings.V3BranchRoadWidthMultiplier, 0.35f, 1.0f),
            _ => 0.78f
        });
        float visualStrength = roadKind switch
        {
            RoadKindV3.Primary => 1.0f,
            RoadKindV3.Branch => 0.58f,
            _ => 0.78f
        };

        _roadGraph.AddEdge(new RoadEdgeV3
        {
            Id = edgeId,
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId,
            Kind = kind,
            Width = width,
            VisualStrength = visualStrength,
            Seed = HashIntId(edgeId, WorldSeed, 8421),
            IsExtraLink = isExtraLink
        });
    }

    private void ConvertRoadGraphToPaths(ref DeterministicRandom random)
    {
        foreach (RoadEdgeV3 edge in _roadGraph.Edges)
        {
            if (!_roadGraph.TryGetNode(edge.FromNodeId, out RoadNodeV3? fromNode)
                || !_roadGraph.TryGetNode(edge.ToNodeId, out RoadNodeV3? toNode)
                || fromNode == null
                || toNode == null)
            {
                continue;
            }

            RoadKindV3 roadKind = edge.Kind switch
            {
                RoadEdgeKindV3.Primary => RoadKindV3.Primary,
                RoadEdgeKindV3.Branch => RoadKindV3.Branch,
                _ => RoadKindV3.Secondary
            };
            List<Vector2> points = BuildMeanderingPoints(fromNode.Position, toNode.Position, edge.Id, roadKind, ref random);
            RoadPathV2 road = new()
            {
                Id = edge.Id,
                FromSiteId = fromNode.VillageId,
                ToSiteId = toNode.VillageId,
                FromNodeId = fromNode.Id,
                ToNodeId = toNode.Id,
                PathPointsWorld = points,
                Bounds = GrowRect(CalculatePathBounds(points), edge.Width + 3.0f),
                Width = edge.Width,
                Kind = roadKind,
                IsMainVillageRoad = roadKind == RoadKindV3.Primary,
                VisualStrength = edge.VisualStrength,
                VisualWearSeed = edge.Seed
            };

            _roads.Add(road);
            switch (roadKind)
            {
                case RoadKindV3.Primary:
                    PrimaryRoadCount++;
                    break;
                case RoadKindV3.Branch:
                    BranchRoadCount++;
                    break;
                default:
                    SecondaryRoadCount++;
                    if (edge.IsExtraLink)
                    {
                        ExtraRoadCount++;
                    }
                    break;
            }
        }
    }

    private void GenerateForests()
    {
        _forestClusters.Clear();
        _forestRegions.Clear();
        MajorForestRegionCount = 0;
        MinorForestPatchCount = 0;
        RejectedForestPlacementCount = 0;
        ClearDistribution(_forestBiomeDistribution);

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
        PlaceForestRegions(majorTarget, true, minX, minY, maxX, maxY, ref nextId, ref random);
        PlaceForestRegions(minorTarget, false, minX, minY, maxX, maxY, ref nextId, ref random);
    }

    private void GenerateQuarriesPlaceholder()
    {
    }

    private void PlaceForestRegions(
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
        int maxAttempts = Mathf.Max(targetCount * 10, 24);
        for (int attempt = 0; placed < targetCount && attempt < maxAttempts; attempt++)
        {
            ForestRegionV3 region = CreateForestRegion(nextId, major, minX, minY, maxX, maxY, ref random);
            BiomeKindV3 biome = ResolveBiomeKindForPlacement(region.Center);
            if (!AcceptBiomeWeightedPlacement(biome, GetForestBiomeWeight(biome), ref random))
            {
                RejectedForestPlacementCount++;
                continue;
            }

            _forestRegions.Add(region);
            IncrementDistribution(_forestBiomeDistribution, biome);
            nextId++;
            placed++;
            if (major)
            {
                MajorForestRegionCount++;
            }
            else
            {
                MinorForestPatchCount++;
            }
        }
    }

    private void GenerateRoadTargetAnchors()
    {
        _roadTargetAnchors.Clear();
        RoadTargetQuarryCount = 0;
        RoadTargetRuinCount = 0;
        RoadTargetDungeonEntranceCount = 0;
        RoadTargetBanditCampCount = 0;
        RoadTargetFactionOutpostCount = 0;
        RoadTargetForestEdgeCount = 0;
        RoadTargetWorldEdgeExitCount = 0;
        FutureRoadTargetCount = 0;
        RejectedRoadTargetCount = 0;

        if (!WorldGenerationLayerSettingsV2.EnableRoads)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 8101));
        int nextId = 1;
        AddQuarryRoadTargets(ref nextId, ref random);
        AddRuinRoadTargets(ref nextId, ref random);
        AddDungeonEntranceRoadTargets(ref nextId, ref random);
        AddBanditCampRoadTargets(ref nextId, ref random);
        AddFactionOutpostRoadTargets(ref nextId, ref random);
        AddForestEdgeRoadTargets(ref nextId, ref random);
        AddWorldEdgeExitTargets(ref nextId, ref random);
    }

    private void GenerateQuarries()
    {
        _quarryClusters.Clear();
        _quarryRegions.Clear();
        MajorQuarryCount = 0;
        MinorQuarryCount = 0;
        RejectedQuarryPlacementCount = 0;
        ClearDistribution(_quarryBiomeDistribution);

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

    private void GenerateRuins()
    {
        _ruinSites.Clear();
        RejectedRuinPlacementCount = 0;
        ClearDistribution(_ruinBiomeDistribution);

        if (!WorldGenerationLayerSettingsV2.EnableRuins)
        {
            return;
        }

        int targetCount = GetTargetRuinCount();
        if (targetCount <= 0)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 9101));
        float edgeMargin = Mathf.Max(96.0f, _settings.V3RuinMaxRadius + 52.0f);
        int minX = Mathf.CeilToInt(edgeMargin);
        int minY = Mathf.CeilToInt(edgeMargin);
        int maxX = Mathf.FloorToInt(WorldSize.WidthCells - edgeMargin - 1.0f);
        int maxY = Mathf.FloorToInt(WorldSize.HeightCells - edgeMargin - 1.0f);
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        int maxAttempts = Mathf.Max(targetCount * 10, targetCount * Mathf.Max(1, _settings.V3RuinPlacementMaxAttemptsPerSite));
        int nextId = 1;
        for (int attempt = 0; _ruinSites.Count < targetCount && attempt < maxAttempts; attempt++)
        {
            float radius = random.Range(Mathf.Max(8.0f, _settings.V3RuinMinRadius), Mathf.Max(_settings.V3RuinMinRadius, _settings.V3RuinMaxRadius));
            Vector2 center = new(random.RangeInclusive(minX, maxX), random.RangeInclusive(minY, maxY));
            if (!CanPlaceRuin(center, radius))
            {
                RejectedRuinPlacementCount++;
                continue;
            }

            BiomeKindV3 biome = ResolveBiomeKindForPlacement(center);
            if (!AcceptBiomeWeightedPlacement(biome, GetRuinBiomeWeight(biome), ref random))
            {
                RejectedRuinPlacementCount++;
                continue;
            }

            _ruinSites.Add(CreateRuinSite(nextId++, center, radius, ref random));
            IncrementDistribution(_ruinBiomeDistribution, biome);
        }

        AssignRoadLinkedRuins(ref random);
    }

    private void GenerateDungeonEntrances()
    {
        _dungeonEntrances.Clear();
        RejectedDungeonEntrancePlacementCount = 0;
        ClearDistribution(_dungeonEntranceBiomeDistribution);
        ClearDistribution(_dungeonEntranceKindDistribution);

        if (!WorldGenerationLayerSettingsV2.EnableDungeons)
        {
            return;
        }

        int targetCount = GetTargetDungeonEntranceCount();
        if (targetCount <= 0)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 10101));
        float edgeMargin = Mathf.Max(96.0f, _settings.V3DungeonEntranceMaxRadius + 72.0f);
        int minX = Mathf.CeilToInt(edgeMargin);
        int minY = Mathf.CeilToInt(edgeMargin);
        int maxX = Mathf.FloorToInt(WorldSize.WidthCells - edgeMargin - 1.0f);
        int maxY = Mathf.FloorToInt(WorldSize.HeightCells - edgeMargin - 1.0f);
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        int maxAttempts = Mathf.Max(targetCount * 12, targetCount * Mathf.Max(1, _settings.V3DungeonEntrancePlacementMaxAttemptsPerSite));
        int nextId = 1;
        for (int attempt = 0; _dungeonEntrances.Count < targetCount && attempt < maxAttempts; attempt++)
        {
            float radius = random.Range(Mathf.Max(6.0f, _settings.V3DungeonEntranceMinRadius), Mathf.Max(_settings.V3DungeonEntranceMinRadius, _settings.V3DungeonEntranceMaxRadius));
            Vector2 center = new(random.RangeInclusive(minX, maxX), random.RangeInclusive(minY, maxY));
            if (!CanPlaceDungeonEntrance(center, radius))
            {
                RejectedDungeonEntrancePlacementCount++;
                continue;
            }

            BiomeKindV3 biome = ResolveBiomeKindForPlacement(center);
            float weight = GetDungeonEntranceBiomeWeight(biome) * GetDungeonEntranceFeatureWeight(center, out string nearbyFeatureHint);
            if (!AcceptBiomeWeightedPlacement(biome, weight, ref random))
            {
                RejectedDungeonEntrancePlacementCount++;
                continue;
            }

            DungeonEntranceKindV3 kind = PickDungeonEntranceKind(center, biome, ref random);
            DungeonEntranceSiteV3 site = CreateDungeonEntranceSite(nextId++, kind, center, radius, nearbyFeatureHint, ref random);
            _dungeonEntrances.Add(site);
            IncrementDistribution(_dungeonEntranceBiomeDistribution, biome);
            IncrementDistribution(_dungeonEntranceKindDistribution, (int)kind);
        }

        AssignRoadLinkedDungeonEntrances(ref random);
    }

    private void GenerateBanditCamps()
    {
        _banditCamps.Clear();
        RejectedBanditCampPlacementCount = 0;
        ClearDistribution(_banditCampBiomeDistribution);
        ClearDistribution(_banditCampKindDistribution);

        if (!WorldGenerationLayerSettingsV2.EnableBanditCamps)
        {
            return;
        }

        int targetCount = GetTargetBanditCampCount();
        if (targetCount <= 0)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 11101));
        float edgeMargin = Mathf.Max(96.0f, _settings.V3BanditCampMaxRadius + 72.0f);
        int minX = Mathf.CeilToInt(edgeMargin);
        int minY = Mathf.CeilToInt(edgeMargin);
        int maxX = Mathf.FloorToInt(WorldSize.WidthCells - edgeMargin - 1.0f);
        int maxY = Mathf.FloorToInt(WorldSize.HeightCells - edgeMargin - 1.0f);
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        int maxAttempts = Mathf.Max(targetCount * 12, targetCount * Mathf.Max(1, _settings.V3BanditCampPlacementMaxAttemptsPerSite));
        int nextId = 1;
        for (int attempt = 0; _banditCamps.Count < targetCount && attempt < maxAttempts; attempt++)
        {
            float radius = random.Range(Mathf.Max(7.0f, _settings.V3BanditCampMinRadius), Mathf.Max(_settings.V3BanditCampMinRadius, _settings.V3BanditCampMaxRadius));
            Vector2 center = new(random.RangeInclusive(minX, maxX), random.RangeInclusive(minY, maxY));
            if (!CanPlaceBanditCamp(center, radius))
            {
                RejectedBanditCampPlacementCount++;
                continue;
            }

            BiomeKindV3 biome = ResolveBiomeKindForPlacement(center);
            float weight = GetBanditCampBiomeWeight(biome) * GetBanditCampFeatureWeight(center, out string nearbyFeatureHint);
            if (!AcceptBiomeWeightedPlacement(biome, weight, ref random))
            {
                RejectedBanditCampPlacementCount++;
                continue;
            }

            BanditCampKindV3 kind = PickBanditCampKind(center, biome, nearbyFeatureHint, ref random);
            BanditCampSiteV3 site = CreateBanditCampSite(nextId++, kind, center, radius, nearbyFeatureHint, ref random);
            _banditCamps.Add(site);
            IncrementDistribution(_banditCampBiomeDistribution, biome);
            IncrementDistribution(_banditCampKindDistribution, (int)kind);
        }

        AssignRoadLinkedBanditCamps(ref random);
    }

    private void GenerateFactionOutposts()
    {
        _factionOutposts.Clear();
        RejectedFactionOutpostPlacementCount = 0;
        ClearDistribution(_factionOutpostBiomeDistribution);
        ClearDistribution(_factionOutpostKindDistribution);
        ClearDistribution(_factionOutpostOwnerDistribution);

        if (!WorldGenerationLayerSettingsV2.EnableFactionOutposts)
        {
            return;
        }

        int targetCount = GetTargetFactionOutpostCount();
        if (targetCount <= 0)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 12101));
        float edgeMargin = Mathf.Max(96.0f, _settings.V3FactionOutpostMaxRadius + 80.0f);
        int minX = Mathf.CeilToInt(edgeMargin);
        int minY = Mathf.CeilToInt(edgeMargin);
        int maxX = Mathf.FloorToInt(WorldSize.WidthCells - edgeMargin - 1.0f);
        int maxY = Mathf.FloorToInt(WorldSize.HeightCells - edgeMargin - 1.0f);
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        int maxAttempts = Mathf.Max(targetCount * 12, targetCount * Mathf.Max(1, _settings.V3FactionOutpostPlacementMaxAttemptsPerSite));
        int nextId = 1;
        for (int attempt = 0; _factionOutposts.Count < targetCount && attempt < maxAttempts; attempt++)
        {
            float radius = random.Range(Mathf.Max(8.0f, _settings.V3FactionOutpostMinRadius), Mathf.Max(_settings.V3FactionOutpostMinRadius, _settings.V3FactionOutpostMaxRadius));
            Vector2 center = new(random.RangeInclusive(minX, maxX), random.RangeInclusive(minY, maxY));
            if (!CanPlaceFactionOutpost(center, radius))
            {
                RejectedFactionOutpostPlacementCount++;
                continue;
            }

            BiomeKindV3 biome = ResolveBiomeKindForPlacement(center);
            float weight = GetFactionOutpostBiomeWeight(biome) * GetFactionOutpostFeatureWeight(center, out VillageSiteV2? nearbySettlement, out string influenceHint);
            if (!AcceptBiomeWeightedPlacement(biome, weight, ref random))
            {
                RejectedFactionOutpostPlacementCount++;
                continue;
            }

            FactionOutpostKindV3 kind = PickFactionOutpostKind(biome, nearbySettlement, influenceHint, ref random);
            FactionOutpostOwnerV3 owner = PickFactionOutpostOwner(biome, nearbySettlement, influenceHint, ref random);
            FactionOutpostSiteV3 site = CreateFactionOutpostSite(nextId++, kind, owner, center, radius, nearbySettlement?.Id ?? -1, influenceHint, ref random);
            _factionOutposts.Add(site);
            IncrementDistribution(_factionOutpostBiomeDistribution, biome);
            IncrementDistribution(_factionOutpostKindDistribution, (int)kind);
            IncrementDistribution(_factionOutpostOwnerDistribution, (int)owner);
        }

        AssignRoadLinkedFactionOutposts(ref random);
    }

    private void GenerateLandmarksPlaceholder()
    {
    }

    private void AssignSettlementRoles()
    {
        ClearDistribution(_settlementRoleDistribution);
        Dictionary<int, int> roadDegreeByVillageId = BuildRoadDegreeByVillageId();

        foreach (VillageSiteV2 village in _villages)
        {
            if (village.IsStartingVillage)
            {
                village.Role = SettlementRoleV3.StartingSettlement;
                IncrementDistribution(_settlementRoleDistribution, (int)village.Role);
                continue;
            }

            BiomeKindV3 biome = ResolveBiomeKindForPlacement(village.Center);
            roadDegreeByVillageId.TryGetValue(village.Id, out int roadDegree);
            village.Role = ResolveSettlementRole(village, biome, roadDegree);
            IncrementDistribution(_settlementRoleDistribution, (int)village.Role);
        }
    }

    private Dictionary<int, int> BuildRoadDegreeByVillageId()
    {
        Dictionary<int, int> degreeByVillageId = new();
        foreach (RoadPathV2 road in _roads)
        {
            if (road.FromSiteId > 0)
            {
                degreeByVillageId.TryGetValue(road.FromSiteId, out int degree);
                degreeByVillageId[road.FromSiteId] = degree + 1;
            }

            if (road.ToSiteId > 0 && road.ToSiteId != road.FromSiteId)
            {
                degreeByVillageId.TryGetValue(road.ToSiteId, out int degree);
                degreeByVillageId[road.ToSiteId] = degree + 1;
            }
        }

        return degreeByVillageId;
    }

    private SettlementRoleV3 ResolveSettlementRole(VillageSiteV2 village, BiomeKindV3 biome, int roadDegree)
    {
        float common = 1.0f + StableUnitFloat(village.Id, WorldSeed, 3611) * 0.25f;
        float farming = StableUnitFloat(village.Id, WorldSeed, 3621) * 0.20f;
        float trade = StableUnitFloat(village.Id, WorldSeed, 3631) * 0.22f;
        float mining = StableUnitFloat(village.Id, WorldSeed, 3641) * 0.20f;
        float forest = StableUnitFloat(village.Id, WorldSeed, 3651) * 0.20f;
        float frontier = StableUnitFloat(village.Id, WorldSeed, 3661) * 0.20f;
        float ruin = StableUnitFloat(village.Id, WorldSeed, 3671) * 0.20f;

        switch (biome)
        {
            case BiomeKindV3.ForestLand:
                forest += 1.05f;
                frontier += 0.25f;
                break;
            case BiomeKindV3.RockyHills:
                mining += 1.05f;
                frontier += 0.35f;
                break;
            case BiomeKindV3.Dryland:
                frontier += 0.80f;
                trade += 0.35f;
                break;
            case BiomeKindV3.Wasteland:
                frontier += 0.85f;
                ruin += 0.75f;
                break;
            default:
                farming += 0.82f;
                trade += 0.18f;
                break;
        }

        if (HasNearbyQuarry(village.Center))
        {
            mining += 0.82f;
        }

        if (HasNearbyForest(village.Center))
        {
            forest += 0.72f;
        }

        if (HasNearbyRuin(village.Center))
        {
            ruin += 0.82f;
            frontier += 0.28f;
        }

        if (roadDegree >= 3)
        {
            trade += 0.72f;
        }

        if (village.Scale is VillageScaleV2.Town or VillageScaleV2.CityCandidate)
        {
            trade += 0.45f;
            common += 0.18f;
        }

        SettlementRoleV3 bestRole = SettlementRoleV3.Common;
        float bestScore = common;
        ConsiderRole(SettlementRoleV3.Farming, farming, ref bestRole, ref bestScore);
        ConsiderRole(SettlementRoleV3.TradeHub, trade, ref bestRole, ref bestScore);
        ConsiderRole(SettlementRoleV3.Mining, mining, ref bestRole, ref bestScore);
        ConsiderRole(SettlementRoleV3.ForestEdge, forest, ref bestRole, ref bestScore);
        ConsiderRole(SettlementRoleV3.Frontier, frontier, ref bestRole, ref bestScore);
        ConsiderRole(SettlementRoleV3.RuinNeighbor, ruin, ref bestRole, ref bestScore);
        return bestRole;
    }

    private static void ConsiderRole(SettlementRoleV3 role, float score, ref SettlementRoleV3 bestRole, ref float bestScore)
    {
        if (score > bestScore)
        {
            bestRole = role;
            bestScore = score;
        }
    }

    private bool HasNearbyQuarry(Vector2I center)
    {
        foreach (QuarryRegionV3 quarry in _quarryRegions)
        {
            if (new Vector2(center.X, center.Y).DistanceTo(quarry.Center) <= quarry.ApproxRadius + 240.0f)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasNearbyForest(Vector2I center)
    {
        foreach (ForestRegionV3 forest in _forestRegions)
        {
            if (new Vector2(center.X, center.Y).DistanceTo(forest.Center) <= forest.ApproxRadius + 220.0f)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasNearbyRuin(Vector2I center)
    {
        foreach (RuinSiteV3 ruin in _ruinSites)
        {
            if (new Vector2(center.X, center.Y).DistanceTo(ruin.Center) <= ruin.ApproxRadius + 260.0f)
            {
                return true;
            }
        }

        return false;
    }

    private void BuildSpatialIndexPlaceholder()
    {
    }

    private void ApplyBiomeSample(FlatlandCellSampleV2 sample)
    {
        if (!WorldGenerationLayerSettingsV2.EnableBiomes)
        {
            return;
        }

        sample.BiomeKind = ResolveBiomeKindAt(sample.GlobalCellCoord, _biomeRegions, out _);
        sample.Biome = MapBiomeKindToBiomeType(sample.BiomeKind);
    }

    private BiomeKindV3 ResolveBiomeKindForPlacement(Vector2 position)
    {
        if (!WorldGenerationLayerSettingsV2.EnableBiomes)
        {
            return BiomeKindV3.Plains;
        }

        Vector2I cell = new(Mathf.RoundToInt(position.X), Mathf.RoundToInt(position.Y));
        return ResolveBiomeKindAt(cell, _biomeRegions, out _);
    }

    private static bool AcceptBiomeWeightedPlacement(BiomeKindV3 biome, float multiplier, ref DeterministicRandom random)
    {
        if (!WorldGenerationLayerSettingsV2.EnableBiomes || multiplier >= 0.995f)
        {
            return true;
        }

        float chance = Mathf.Clamp(multiplier, 0.10f, 1.0f);
        return random.NextUnit() <= chance;
    }

    private float GetForestBiomeWeight(BiomeKindV3 biome)
    {
        return biome switch
        {
            BiomeKindV3.ForestLand => _settings.V3ForestLandForestWeightMultiplier,
            BiomeKindV3.RockyHills => _settings.V3RockyHillsForestWeightMultiplier,
            BiomeKindV3.Dryland => _settings.V3DrylandForestWeightMultiplier,
            BiomeKindV3.Wasteland => _settings.V3WastelandForestWeightMultiplier,
            _ => _settings.V3PlainsForestWeightMultiplier
        };
    }

    private float GetQuarryBiomeWeight(BiomeKindV3 biome)
    {
        return biome switch
        {
            BiomeKindV3.ForestLand => _settings.V3ForestLandQuarryWeightMultiplier,
            BiomeKindV3.RockyHills => _settings.V3RockyHillsQuarryWeightMultiplier,
            BiomeKindV3.Dryland => _settings.V3DrylandQuarryWeightMultiplier,
            BiomeKindV3.Wasteland => _settings.V3WastelandQuarryWeightMultiplier,
            _ => _settings.V3PlainsQuarryWeightMultiplier
        };
    }

    private float GetRuinBiomeWeight(BiomeKindV3 biome)
    {
        return biome switch
        {
            BiomeKindV3.ForestLand => _settings.V3ForestLandRuinWeightMultiplier,
            BiomeKindV3.RockyHills => _settings.V3RockyHillsRuinWeightMultiplier,
            BiomeKindV3.Dryland => _settings.V3DrylandRuinWeightMultiplier,
            BiomeKindV3.Wasteland => _settings.V3WastelandRuinWeightMultiplier,
            _ => _settings.V3PlainsRuinWeightMultiplier
        };
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

    private void RasterBiomes(FlatlandChunkGenerationContextV2 context)
    {
        for (int y = 0; y < ChunkDataV2.ChunkSize; y++)
        {
            for (int x = 0; x < ChunkDataV2.ChunkSize; x++)
            {
                Vector2I cell = context.ToGlobalCell(x, y);
                int index = FlatlandChunkGenerationContextV2.ToIndex(x, y);
                context.BiomeKind[index] = ResolveBiomeKindAt(cell, context.RelevantBiomeRegions, out float margin);
                context.BiomeStrength[index] = margin;
            }
        }
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

    private void RasterRuins(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        foreach (RuinSiteV3 ruin in context.RelevantRuinSites)
        {
            RasterRuinSite(context, ruin);
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandSiteSample, start, context.ChunkCoord);
    }

    private void RasterRuinSite(FlatlandChunkGenerationContextV2 context, RuinSiteV3 ruin)
    {
        int minGlobalX = Mathf.FloorToInt(ruin.Bounds.Position.X);
        int minGlobalY = Mathf.FloorToInt(ruin.Bounds.Position.Y);
        int maxGlobalX = Mathf.CeilToInt(ruin.Bounds.End.X);
        int maxGlobalY = Mathf.CeilToInt(ruin.Bounds.End.Y);
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
                float strength = GetRuinPotentialAt(ruin, cell);
                if (strength <= 0.0f)
                {
                    continue;
                }

                float distance = new Vector2(cell.X + 0.5f, cell.Y + 0.5f).DistanceTo(ruin.Center);
                context.IsLandmark[index] = true;
                context.LandmarkKind[index] = LandmarkKindV2.Ruin;
                context.SiteDistance[index] = Mathf.Min(context.SiteDistance[index], distance);
                context.SiteRadius[index] = ruin.ApproxRadius;
                context.HasLandmarkTile = true;
            }
        }
    }

    private void RasterDungeonEntrances(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        foreach (DungeonEntranceSiteV3 dungeon in context.RelevantDungeonEntrances)
        {
            RasterDungeonEntrance(context, dungeon);
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandSiteSample, start, context.ChunkCoord);
    }

    private void RasterDungeonEntrance(FlatlandChunkGenerationContextV2 context, DungeonEntranceSiteV3 dungeon)
    {
        int minGlobalX = Mathf.FloorToInt(dungeon.Bounds.Position.X);
        int minGlobalY = Mathf.FloorToInt(dungeon.Bounds.Position.Y);
        int maxGlobalX = Mathf.CeilToInt(dungeon.Bounds.End.X);
        int maxGlobalY = Mathf.CeilToInt(dungeon.Bounds.End.Y);
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
                float strength = GetDungeonEntrancePotentialAt(dungeon, cell);
                if (strength <= 0.0f)
                {
                    continue;
                }

                float distance = new Vector2(cell.X + 0.5f, cell.Y + 0.5f).DistanceTo(dungeon.Center);
                context.IsDungeonEntrance[index] = true;
                context.DungeonEntranceKind[index] = dungeon.Kind;
                context.IsLandmark[index] = true;
                context.LandmarkKind[index] = LandmarkKindV2.Dungeon;
                context.SiteDistance[index] = Mathf.Min(context.SiteDistance[index], distance);
                context.SiteRadius[index] = dungeon.ApproxRadius;
                context.HasLandmarkTile = true;
            }
        }
    }

    private void RasterBanditCamps(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        foreach (BanditCampSiteV3 camp in context.RelevantBanditCamps)
        {
            RasterBanditCamp(context, camp);
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandSiteSample, start, context.ChunkCoord);
    }

    private void RasterBanditCamp(FlatlandChunkGenerationContextV2 context, BanditCampSiteV3 camp)
    {
        int minGlobalX = Mathf.FloorToInt(camp.Bounds.Position.X);
        int minGlobalY = Mathf.FloorToInt(camp.Bounds.Position.Y);
        int maxGlobalX = Mathf.CeilToInt(camp.Bounds.End.X);
        int maxGlobalY = Mathf.CeilToInt(camp.Bounds.End.Y);
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
                float strength = GetBanditCampPotentialAt(camp, cell);
                if (strength <= 0.0f)
                {
                    continue;
                }

                float distance = new Vector2(cell.X + 0.5f, cell.Y + 0.5f).DistanceTo(camp.Center);
                context.IsBanditCamp[index] = true;
                context.BanditCampKind[index] = camp.Kind;
                context.IsLandmark[index] = true;
                context.LandmarkKind[index] = LandmarkKindV2.BanditCamp;
                context.SiteDistance[index] = Mathf.Min(context.SiteDistance[index], distance);
                context.SiteRadius[index] = camp.ApproxRadius;
                context.HasLandmarkTile = true;
            }
        }
    }

    private void RasterFactionOutposts(FlatlandChunkGenerationContextV2 context)
    {
        WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
        long start = profiler.BeginSample();

        foreach (FactionOutpostSiteV3 outpost in context.RelevantFactionOutposts)
        {
            RasterFactionOutpost(context, outpost);
        }

        profiler.EndSample(WorldV2PerformanceProfiler.FlatlandSiteSample, start, context.ChunkCoord);
    }

    private void RasterFactionOutpost(FlatlandChunkGenerationContextV2 context, FactionOutpostSiteV3 outpost)
    {
        int minGlobalX = Mathf.FloorToInt(outpost.Bounds.Position.X);
        int minGlobalY = Mathf.FloorToInt(outpost.Bounds.Position.Y);
        int maxGlobalX = Mathf.CeilToInt(outpost.Bounds.End.X);
        int maxGlobalY = Mathf.CeilToInt(outpost.Bounds.End.Y);
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
                float strength = GetFactionOutpostPotentialAt(outpost, cell);
                if (strength <= 0.0f)
                {
                    continue;
                }

                float distance = new Vector2(cell.X + 0.5f, cell.Y + 0.5f).DistanceTo(outpost.Center);
                context.IsFactionOutpost[index] = true;
                context.FactionOutpostKind[index] = outpost.Kind;
                context.FactionOutpostOwner[index] = outpost.Owner;
                context.IsLandmark[index] = true;
                context.LandmarkKind[index] = LandmarkKindV2.FactionOutpost;
                context.SiteDistance[index] = Mathf.Min(context.SiteDistance[index], distance);
                context.SiteRadius[index] = outpost.ApproxRadius;
                context.HasLandmarkTile = true;
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

    private static BiomeKindV3 ResolveBiomeKindAt(Vector2I cell, IReadOnlyList<BiomeRegionV3> regions, out float scoreMargin)
    {
        Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
        BiomeKindV3 bestKind = BiomeKindV3.Plains;
        float bestScore = 0.0f;
        float secondScore = -0.20f;

        foreach (BiomeRegionV3 region in regions)
        {
            if (!region.IsMajorRegion && !region.Bounds.HasPoint(point))
            {
                continue;
            }

            float score = GetBiomeScoreAt(region, point);
            if (score > bestScore)
            {
                secondScore = bestScore;
                bestScore = score;
                bestKind = region.Kind;
            }
            else if (score > secondScore)
            {
                secondScore = score;
            }
        }

        scoreMargin = Mathf.Clamp(bestScore - secondScore, 0.0f, 1.0f);
        return bestKind;
    }

    private static float GetBiomeScoreAt(BiomeRegionV3 biome, Vector2 point)
    {
        float warpScale = biome.NoiseScale * 0.22f;
        float warpX = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, biome.Seed + 101, 1) - 0.5f) * 2.0f;
        float warpY = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, biome.Seed + 211, 1) - 0.5f) * 2.0f;
        Vector2 warped = point + new Vector2(warpX, warpY) * biome.WarpStrength;
        float radius = Mathf.Max(16.0f, biome.ApproxRadius);
        float normalizedDistance = warped.DistanceTo(biome.Center) / radius;

        float low = FractalValueNoise(warped.X * biome.NoiseScale, warped.Y * biome.NoiseScale, biome.Seed + 307, 1);
        float edge = FractalValueNoise(warped.X * biome.NoiseScale * 1.25f, warped.Y * biome.NoiseScale * 1.25f, biome.Seed + 409, 1);
        float boundaryShift = (low - 0.5f) * 0.10f + (edge - 0.5f) * 0.04f;

        if (biome.IsMajorRegion)
        {
            float plainsBias = biome.Kind == BiomeKindV3.Plains ? 0.08f : 0.0f;
            return biome.Weight + plainsBias - normalizedDistance + boundaryShift;
        }

        if (normalizedDistance > 1.16f)
        {
            return -1000.0f;
        }

        float potential = 1.0f - normalizedDistance + boundaryShift;
        if (normalizedDistance < 0.55f)
        {
            potential = Mathf.Max(potential, 0.72f + (low - 0.5f) * 0.04f);
        }

        if (potential < biome.Threshold)
        {
            return -1000.0f;
        }

        float t = Mathf.Clamp((potential - biome.Threshold) / Mathf.Max(0.05f, 1.04f - biome.Threshold), 0.0f, 1.0f);
        return Mathf.Lerp(0.18f, 0.54f, t) * biome.Weight;
    }

    private static BiomeTypeV2 MapBiomeKindToBiomeType(BiomeKindV3 kind)
    {
        return kind switch
        {
            BiomeKindV3.ForestLand => BiomeTypeV2.Forest,
            BiomeKindV3.RockyHills => BiomeTypeV2.Hills,
            BiomeKindV3.Dryland => BiomeTypeV2.DryWasteland,
            BiomeKindV3.Wasteland => BiomeTypeV2.DryWasteland,
            _ => BiomeTypeV2.Plains
        };
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

    private float GetRuinPotentialAt(RuinSiteV3 ruin, Vector2I cell)
    {
        Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
        float noiseScale = Mathf.Max(0.001f, _settings.V3RuinNoiseScale);
        float warpScale = noiseScale * 0.42f;
        float warpX = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, ruin.Seed + 101, 2) - 0.5f) * 2.0f;
        float warpY = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, ruin.Seed + 211, 2) - 0.5f) * 2.0f;
        Vector2 warped = point + new Vector2(warpX, warpY) * Mathf.Max(0.0f, _settings.V3RuinWarpStrength);
        float radius = Mathf.Max(4.0f, ruin.ApproxRadius);
        float normalizedDistance = warped.DistanceTo(ruin.Center) / radius;
        if (normalizedDistance > 1.18f)
        {
            return 0.0f;
        }

        float low = FractalValueNoise(warped.X * noiseScale, warped.Y * noiseScale, ruin.Seed + 307, 2);
        float edge = FractalValueNoise(warped.X * noiseScale * 2.2f, warped.Y * noiseScale * 2.2f, ruin.Seed + 409, 2);
        float boundaryShift = (low - 0.5f) * 0.26f + (edge - 0.5f) * 0.10f;
        float potential = 1.0f - normalizedDistance + boundaryShift;
        if (normalizedDistance < 0.42f)
        {
            potential = Mathf.Max(potential, 0.64f + low * 0.14f);
        }

        return potential > 0.24f
            ? Mathf.Clamp(Mathf.Lerp(0.34f, 1.0f, potential) * ruin.Density, 0.0f, 1.0f)
            : 0.0f;
    }

    private float GetDungeonEntrancePotentialAt(DungeonEntranceSiteV3 dungeon, Vector2I cell)
    {
        Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
        float radius = Mathf.Max(3.0f, dungeon.ApproxRadius);
        float distance = point.DistanceTo(dungeon.Center);
        if (distance > radius * 1.12f)
        {
            return 0.0f;
        }

        float normalizedDistance = distance / radius;
        float edgeNoise = (StableUnitFloat(cell.X + dungeon.Id * 31, cell.Y - dungeon.Id * 19, dungeon.Seed + 47) - 0.5f) * 0.18f;
        float detail = StableUnitFloat(cell.X / 2 + dungeon.Id * 13, cell.Y / 2 - dungeon.Id * 11, dungeon.Seed + 83);
        float potential = (1.0f - normalizedDistance + edgeNoise) * 0.84f + detail * 0.16f;
        return potential > 0.26f ? Mathf.Clamp(potential, 0.0f, 1.0f) : 0.0f;
    }

    private float GetBanditCampPotentialAt(BanditCampSiteV3 camp, Vector2I cell)
    {
        Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
        float radius = Mathf.Max(4.0f, camp.ApproxRadius);
        float distance = point.DistanceTo(camp.Center);
        if (distance > radius * 1.20f)
        {
            return 0.0f;
        }

        float normalizedDistance = distance / radius;
        float edgeNoise = (StableUnitFloat(cell.X + camp.Id * 29, cell.Y - camp.Id * 17, camp.Seed + 61) - 0.5f) * 0.24f;
        float detail = StableUnitFloat(cell.X / 2 + camp.Id * 7, cell.Y / 2 - camp.Id * 13, camp.Seed + 97);
        float potential = (1.0f - normalizedDistance + edgeNoise) * 0.76f + detail * 0.24f;
        return potential > 0.30f ? Mathf.Clamp(potential, 0.0f, 1.0f) : 0.0f;
    }

    private float GetFactionOutpostPotentialAt(FactionOutpostSiteV3 outpost, Vector2I cell)
    {
        Vector2 point = new(cell.X + 0.5f, cell.Y + 0.5f);
        float radius = Mathf.Max(5.0f, outpost.ApproxRadius);
        float distance = point.DistanceTo(outpost.Center);
        if (distance > radius * 1.16f)
        {
            return 0.0f;
        }

        float normalizedDistance = distance / radius;
        float edgeNoise = (StableUnitFloat(cell.X + outpost.Id * 37, cell.Y - outpost.Id * 23, outpost.Seed + 53) - 0.5f) * 0.14f;
        float orderedYard = Mathf.Abs((StableUnitFloat(cell.X / 3 + outpost.Id * 5, cell.Y / 3 - outpost.Id * 3, outpost.Seed + 109) - 0.5f) * 0.12f);
        float potential = 1.0f - normalizedDistance + edgeNoise - orderedYard;
        return potential > 0.26f ? Mathf.Clamp(potential, 0.0f, 1.0f) : 0.0f;
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

        if (sample.IsFactionOutpost)
        {
            sample.Biome = BiomeTypeV2.TradeRoadZone;
            sample.LandmarkKind = LandmarkKindV2.FactionOutpost;
            float coreRatio = siteRadius > 0.0f ? siteDistance / siteRadius : 1.0f;
            sample.TileType = coreRatio <= 0.46f
                ? TileType.FactionOutpost
                : sample.FactionOutpostKind == FactionOutpostKindV3.BorderFort ? TileType.StoneField : TileType.Dirt;
            sample.IsBuildRestricted = true;
            return;
        }

        if (sample.IsBanditCamp)
        {
            sample.Biome = BiomeTypeV2.BanditTerritory;
            sample.LandmarkKind = LandmarkKindV2.BanditCamp;
            float coreRatio = siteRadius > 0.0f ? siteDistance / siteRadius : 1.0f;
            sample.TileType = coreRatio <= 0.46f
                ? TileType.BanditCamp
                : sample.BanditCampKind == BanditCampKindV3.RuinCamp ? TileType.Rubble : TileType.Dirt;
            sample.IsBuildRestricted = true;
            return;
        }

        if (sample.IsDungeonEntrance)
        {
            sample.Biome = BiomeTypeV2.RuinedFactory;
            sample.LandmarkKind = LandmarkKindV2.Dungeon;
            float coreRatio = siteRadius > 0.0f ? siteDistance / siteRadius : 1.0f;
            sample.TileType = coreRatio <= 0.38f
                ? TileType.Dungeon
                : sample.DungeonEntranceKind == DungeonEntranceKindV3.MineShaft ? TileType.StoneField : TileType.Rubble;
            sample.IsBuildRestricted = true;
            return;
        }

        if (sample.IsLandmark && sample.LandmarkKind == LandmarkKindV2.Ruin)
        {
            sample.Biome = BiomeTypeV2.RuinedResidential;
            float coreRatio = siteRadius > 0.0f ? siteDistance / siteRadius : 1.0f;
            sample.TileType = coreRatio <= 0.48f ? TileType.Ruin : TileType.Rubble;
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

    private void GetTargetBiomeRegionCounts(out int majorCount, out int minorCount)
    {
        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 2113));
        switch (MapSizePreset)
        {
            case WorldMapSizePresetV2.Medium:
                majorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3MediumMajorBiomeMinCount), Mathf.Max(0, _settings.V3MediumMajorBiomeMaxCount));
                minorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3MediumMinorBiomeMinCount), Mathf.Max(0, _settings.V3MediumMinorBiomeMaxCount));
                break;
            case WorldMapSizePresetV2.Large:
                majorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3LargeMajorBiomeMinCount), Mathf.Max(0, _settings.V3LargeMajorBiomeMaxCount));
                minorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3LargeMinorBiomeMinCount), Mathf.Max(0, _settings.V3LargeMinorBiomeMaxCount));
                break;
            case WorldMapSizePresetV2.Huge:
                majorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3HugeMajorBiomeMinCount), Mathf.Max(0, _settings.V3HugeMajorBiomeMaxCount));
                minorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3HugeMinorBiomeMinCount), Mathf.Max(0, _settings.V3HugeMinorBiomeMaxCount));
                break;
            case WorldMapSizePresetV2.Small:
            default:
                majorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3SmallMajorBiomeMinCount), Mathf.Max(0, _settings.V3SmallMajorBiomeMaxCount));
                minorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3SmallMinorBiomeMinCount), Mathf.Max(0, _settings.V3SmallMinorBiomeMaxCount));
                break;
        }
    }

    private BiomeKindV3 PickBiomeKind(int nextId, int placedIndex, bool major, ref DeterministicRandom random)
    {
        if (major && placedIndex == 0)
        {
            return BiomeKindV3.Plains;
        }

        if (placedIndex > 0 && placedIndex < 5)
        {
            return (BiomeKindV3)placedIndex;
        }

        float roll = random.NextUnit();
        if (major)
        {
            return roll switch
            {
                < 0.34f => BiomeKindV3.ForestLand,
                < 0.58f => BiomeKindV3.RockyHills,
                < 0.80f => BiomeKindV3.Dryland,
                _ => BiomeKindV3.Wasteland
            };
        }

        float jitter = StableUnitFloat(nextId, WorldSeed, 2129) * 0.10f;
        return (roll + jitter) switch
        {
            < 0.30f => BiomeKindV3.ForestLand,
            < 0.55f => BiomeKindV3.RockyHills,
            < 0.82f => BiomeKindV3.Dryland,
            _ => BiomeKindV3.Wasteland
        };
    }

    private BiomeRegionV3 CreateBiomeRegion(int id, BiomeKindV3 kind, Vector2 center, float radius, bool major, ref DeterministicRandom random)
    {
        float warp = major
            ? Mathf.Max(0.0f, _settings.V3MajorBiomeWarpStrength) * random.Range(0.55f, 0.90f)
            : Mathf.Max(0.0f, _settings.V3MinorBiomeWarpStrength) * random.Range(0.45f, 0.78f);
        float noiseScale = major
            ? Mathf.Max(0.0002f, _settings.V3MajorBiomeNoiseScale) * random.Range(0.78f, 1.06f)
            : Mathf.Max(0.0002f, _settings.V3MinorBiomeNoiseScale) * random.Range(0.74f, 1.12f);
        float threshold = Mathf.Clamp(_settings.V3BiomePotentialThreshold + (random.NextUnit() - 0.5f) * 0.045f + (major ? -0.055f : 0.075f), 0.30f, 0.72f);
        float boundsRadius = radius * 1.24f + warp + 12.0f;
        Rect2 bounds = major
            ? new Rect2(Vector2.Zero, new Vector2(WorldSize.WidthCells, WorldSize.HeightCells))
            : new Rect2(center - new Vector2(boundsRadius, boundsRadius), new Vector2(boundsRadius * 2.0f, boundsRadius * 2.0f));
        return new BiomeRegionV3
        {
            Id = id,
            Kind = kind,
            Center = center,
            ApproxRadius = radius,
            Bounds = bounds,
            Seed = HashIntId(id, WorldSeed, major ? 2201 : 2301),
            Threshold = threshold,
            NoiseScale = noiseScale,
            WarpStrength = warp,
            Weight = kind == BiomeKindV3.Plains ? random.Range(0.95f, 1.05f) : major ? random.Range(0.98f, 1.14f) : random.Range(0.34f, 0.54f),
            IsMajorRegion = major
        };
    }

    private void IncrementBiomeKindCount(BiomeKindV3 kind)
    {
        switch (kind)
        {
            case BiomeKindV3.ForestLand:
                BiomeForestLandCount++;
                break;
            case BiomeKindV3.RockyHills:
                BiomeRockyHillsCount++;
                break;
            case BiomeKindV3.Dryland:
                BiomeDrylandCount++;
                break;
            case BiomeKindV3.Wasteland:
                BiomeWastelandCount++;
                break;
        }
    }

    private int GetTargetVillageCount()
    {
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Small => Mathf.Max(1, _settings.V3SmallVillageCount),
            WorldMapSizePresetV2.Medium => Mathf.Max(1, _settings.V3MediumVillageCount),
            WorldMapSizePresetV2.Large => Mathf.Max(1, _settings.V3LargeVillageCount),
            WorldMapSizePresetV2.Huge => Mathf.Max(1, _settings.V3HugeVillageCount),
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
            case WorldMapSizePresetV2.Huge:
                majorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3HugeMajorQuarryMinCount), Mathf.Max(0, _settings.V3HugeMajorQuarryMaxCount));
                minorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3HugeMinorQuarryMinCount), Mathf.Max(0, _settings.V3HugeMinorQuarryMaxCount));
                break;
            case WorldMapSizePresetV2.Small:
            default:
                majorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3SmallMajorQuarryMinCount), Mathf.Max(0, _settings.V3SmallMajorQuarryMaxCount));
                minorCount = random.RangeInclusive(Mathf.Max(0, _settings.V3SmallMinorQuarryMinCount), Mathf.Max(0, _settings.V3SmallMinorQuarryMaxCount));
                break;
        }
    }

    private int GetTargetRuinCount()
    {
        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 9113));
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Medium => random.RangeInclusive(
                Mathf.Max(0, _settings.V3MediumRuinMinCount),
                Mathf.Max(0, _settings.V3MediumRuinMaxCount)),
            WorldMapSizePresetV2.Large => random.RangeInclusive(
                Mathf.Max(0, _settings.V3LargeRuinMinCount),
                Mathf.Max(0, _settings.V3LargeRuinMaxCount)),
            WorldMapSizePresetV2.Huge => random.RangeInclusive(
                Mathf.Max(0, _settings.V3HugeRuinMinCount),
                Mathf.Max(0, _settings.V3HugeRuinMaxCount)),
            WorldMapSizePresetV2.Small or _ => random.RangeInclusive(
                Mathf.Max(0, _settings.V3SmallRuinMinCount),
                Mathf.Max(0, _settings.V3SmallRuinMaxCount))
        };
    }

    private int GetTargetDungeonEntranceCount()
    {
        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 10113));
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Medium => random.RangeInclusive(
                Mathf.Max(0, _settings.V3MediumDungeonEntranceMinCount),
                Mathf.Max(0, _settings.V3MediumDungeonEntranceMaxCount)),
            WorldMapSizePresetV2.Large => random.RangeInclusive(
                Mathf.Max(0, _settings.V3LargeDungeonEntranceMinCount),
                Mathf.Max(0, _settings.V3LargeDungeonEntranceMaxCount)),
            WorldMapSizePresetV2.Huge => random.RangeInclusive(
                Mathf.Max(0, _settings.V3HugeDungeonEntranceMinCount),
                Mathf.Max(0, _settings.V3HugeDungeonEntranceMaxCount)),
            WorldMapSizePresetV2.Small or _ => random.RangeInclusive(
                Mathf.Max(0, _settings.V3SmallDungeonEntranceMinCount),
                Mathf.Max(0, _settings.V3SmallDungeonEntranceMaxCount))
        };
    }

    private int GetDesiredRoadLinkedRuinCount(ref DeterministicRandom random)
    {
        int ruinCount = _ruinSites.Count;
        if (ruinCount == 0)
        {
            return 0;
        }

        int desired = MapSizePreset switch
        {
            WorldMapSizePresetV2.Medium => Mathf.RoundToInt(ruinCount * random.Range(0.30f, 0.45f)),
            WorldMapSizePresetV2.Large => Mathf.RoundToInt(ruinCount * random.Range(0.25f, 0.40f)),
            WorldMapSizePresetV2.Huge => Mathf.RoundToInt(ruinCount * random.Range(0.25f, 0.35f)),
            WorldMapSizePresetV2.Small or _ => random.RangeInclusive(1, 2)
        };

        return Mathf.Clamp(desired, 0, ruinCount);
    }

    private void AssignRoadLinkedRuins(ref DeterministicRandom random)
    {
        foreach (RuinSiteV3 ruin in _ruinSites)
        {
            ruin.IsRoadLinked = false;
            ruin.LinkedRoadId = -1;
        }

        int desired = GetDesiredRoadLinkedRuinCount(ref random);
        if (desired <= 0 || _roads.Count == 0)
        {
            return;
        }

        List<RuinSiteV3> candidates = new();
        foreach (RuinSiteV3 ruin in _ruinSites)
        {
            float distance = GetNearestNonBranchRoadDistance(ruin.Center);
            if (distance < _settings.V3BranchRoadMinLength * 0.45f || distance > _settings.V3BranchRoadMaxLength * 1.08f)
            {
                continue;
            }

            candidates.Add(ruin);
        }

        candidates.Sort((a, b) =>
        {
            float aScore = GetNearestNonBranchRoadDistance(a.Center) + StableUnitFloat(a.Id, WorldSeed, 9141) * 80.0f;
            float bScore = GetNearestNonBranchRoadDistance(b.Center) + StableUnitFloat(b.Id, WorldSeed, 9141) * 80.0f;
            return aScore.CompareTo(bScore);
        });

        int count = Mathf.Min(desired, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            candidates[i].IsRoadLinked = true;
        }
    }

    private int GetDesiredRoadLinkedDungeonEntranceCount(ref DeterministicRandom random)
    {
        int dungeonCount = _dungeonEntrances.Count;
        if (dungeonCount == 0)
        {
            return 0;
        }

        int desired = MapSizePreset switch
        {
            WorldMapSizePresetV2.Medium => Mathf.RoundToInt(dungeonCount * random.Range(0.30f, 0.45f)),
            WorldMapSizePresetV2.Large => Mathf.RoundToInt(dungeonCount * random.Range(0.25f, 0.40f)),
            WorldMapSizePresetV2.Huge => Mathf.RoundToInt(dungeonCount * random.Range(0.20f, 0.35f)),
            WorldMapSizePresetV2.Small or _ => 1
        };

        return Mathf.Clamp(desired, 0, dungeonCount);
    }

    private void AssignRoadLinkedDungeonEntrances(ref DeterministicRandom random)
    {
        foreach (DungeonEntranceSiteV3 dungeon in _dungeonEntrances)
        {
            dungeon.IsRoadLinked = false;
            dungeon.LinkedRoadId = -1;
        }

        int desired = GetDesiredRoadLinkedDungeonEntranceCount(ref random);
        if (desired <= 0 || _roads.Count == 0)
        {
            return;
        }

        List<DungeonEntranceSiteV3> candidates = new();
        foreach (DungeonEntranceSiteV3 dungeon in _dungeonEntrances)
        {
            float distance = GetNearestNonBranchRoadDistance(dungeon.Center);
            if (distance < _settings.V3BranchRoadMinLength * 0.42f || distance > _settings.V3BranchRoadMaxLength * 1.10f)
            {
                continue;
            }

            candidates.Add(dungeon);
        }

        candidates.Sort((a, b) =>
        {
            float aScore = GetNearestNonBranchRoadDistance(a.Center) + StableUnitFloat(a.Id, WorldSeed, 10141) * 90.0f;
            float bScore = GetNearestNonBranchRoadDistance(b.Center) + StableUnitFloat(b.Id, WorldSeed, 10141) * 90.0f;
            return aScore.CompareTo(bScore);
        });

        int count = Mathf.Min(desired, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            candidates[i].IsRoadLinked = true;
        }
    }

    private int GetTargetBanditCampCount()
    {
        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 11113));
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Medium => random.RangeInclusive(
                Mathf.Max(0, _settings.V3MediumBanditCampMinCount),
                Mathf.Max(0, _settings.V3MediumBanditCampMaxCount)),
            WorldMapSizePresetV2.Large => random.RangeInclusive(
                Mathf.Max(0, _settings.V3LargeBanditCampMinCount),
                Mathf.Max(0, _settings.V3LargeBanditCampMaxCount)),
            WorldMapSizePresetV2.Huge => random.RangeInclusive(
                Mathf.Max(0, _settings.V3HugeBanditCampMinCount),
                Mathf.Max(0, _settings.V3HugeBanditCampMaxCount)),
            WorldMapSizePresetV2.Small or _ => random.RangeInclusive(
                Mathf.Max(0, _settings.V3SmallBanditCampMinCount),
                Mathf.Max(0, _settings.V3SmallBanditCampMaxCount))
        };
    }

    private int GetTargetFactionOutpostCount()
    {
        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 12113));
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Medium => random.RangeInclusive(
                Mathf.Max(0, _settings.V3MediumFactionOutpostMinCount),
                Mathf.Max(0, _settings.V3MediumFactionOutpostMaxCount)),
            WorldMapSizePresetV2.Large => random.RangeInclusive(
                Mathf.Max(0, _settings.V3LargeFactionOutpostMinCount),
                Mathf.Max(0, _settings.V3LargeFactionOutpostMaxCount)),
            WorldMapSizePresetV2.Huge => random.RangeInclusive(
                Mathf.Max(0, _settings.V3HugeFactionOutpostMinCount),
                Mathf.Max(0, _settings.V3HugeFactionOutpostMaxCount)),
            WorldMapSizePresetV2.Small or _ => random.RangeInclusive(
                Mathf.Max(0, _settings.V3SmallFactionOutpostMinCount),
                Mathf.Max(0, _settings.V3SmallFactionOutpostMaxCount))
        };
    }

    private int GetDesiredRoadLinkedBanditCampCount(ref DeterministicRandom random)
    {
        int campCount = _banditCamps.Count;
        if (campCount == 0)
        {
            return 0;
        }

        int desired = MapSizePreset switch
        {
            WorldMapSizePresetV2.Medium => Mathf.RoundToInt(campCount * random.Range(0.25f, 0.40f)),
            WorldMapSizePresetV2.Large => Mathf.RoundToInt(campCount * random.Range(0.20f, 0.35f)),
            WorldMapSizePresetV2.Huge => Mathf.RoundToInt(campCount * random.Range(0.15f, 0.30f)),
            WorldMapSizePresetV2.Small or _ => 1
        };

        return Mathf.Clamp(desired, 0, campCount);
    }

    private void AssignRoadLinkedBanditCamps(ref DeterministicRandom random)
    {
        foreach (BanditCampSiteV3 camp in _banditCamps)
        {
            camp.IsRoadLinked = false;
            camp.LinkedRoadId = -1;
        }

        int desired = GetDesiredRoadLinkedBanditCampCount(ref random);
        if (desired <= 0 || _roads.Count == 0)
        {
            return;
        }

        List<BanditCampSiteV3> candidates = new();
        foreach (BanditCampSiteV3 camp in _banditCamps)
        {
            float distance = GetNearestNonBranchRoadDistance(camp.Center);
            if (distance < _settings.V3BranchRoadMinLength * 0.50f || distance > _settings.V3BranchRoadMaxLength * 1.05f)
            {
                continue;
            }

            candidates.Add(camp);
        }

        candidates.Sort((a, b) =>
        {
            float aScore = GetNearestNonBranchRoadDistance(a.Center) + StableUnitFloat(a.Id, WorldSeed, 11141) * 96.0f;
            float bScore = GetNearestNonBranchRoadDistance(b.Center) + StableUnitFloat(b.Id, WorldSeed, 11141) * 96.0f;
            return aScore.CompareTo(bScore);
        });

        int count = Mathf.Min(desired, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            candidates[i].IsRoadLinked = true;
        }
    }

    private int GetDesiredRoadLinkedFactionOutpostCount(ref DeterministicRandom random)
    {
        int outpostCount = _factionOutposts.Count;
        if (outpostCount == 0)
        {
            return 0;
        }

        int desired = MapSizePreset switch
        {
            WorldMapSizePresetV2.Medium => Mathf.RoundToInt(outpostCount * random.Range(0.60f, 0.80f)),
            WorldMapSizePresetV2.Large => Mathf.RoundToInt(outpostCount * random.Range(0.60f, 0.80f)),
            WorldMapSizePresetV2.Huge => Mathf.RoundToInt(outpostCount * random.Range(0.55f, 0.75f)),
            WorldMapSizePresetV2.Small or _ => 1
        };

        return Mathf.Clamp(desired, 0, outpostCount);
    }

    private void AssignRoadLinkedFactionOutposts(ref DeterministicRandom random)
    {
        foreach (FactionOutpostSiteV3 outpost in _factionOutposts)
        {
            outpost.IsRoadLinked = false;
            outpost.LinkedRoadId = -1;
        }

        int desired = GetDesiredRoadLinkedFactionOutpostCount(ref random);
        if (desired <= 0 || _roads.Count == 0)
        {
            return;
        }

        List<FactionOutpostSiteV3> candidates = new();
        foreach (FactionOutpostSiteV3 outpost in _factionOutposts)
        {
            float distance = GetNearestNonBranchRoadDistance(outpost.Center);
            if (distance < _settings.V3BranchRoadMinLength * 0.42f || distance > _settings.V3BranchRoadMaxLength * 1.12f)
            {
                continue;
            }

            candidates.Add(outpost);
        }

        candidates.Sort((a, b) =>
        {
            float aScore = GetNearestNonBranchRoadDistance(a.Center) + StableUnitFloat(a.Id, WorldSeed, 12141) * 58.0f;
            float bScore = GetNearestNonBranchRoadDistance(b.Center) + StableUnitFloat(b.Id, WorldSeed, 12141) * 58.0f;
            return aScore.CompareTo(bScore);
        });

        int count = Mathf.Min(desired, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            candidates[i].IsRoadLinked = true;
        }
    }

    private bool CanPlaceBanditCamp(Vector2 center, float radius)
    {
        foreach (VillageSiteV2 village in _villages)
        {
            float tierBuffer = village.Scale is VillageScaleV2.Town or VillageScaleV2.CityCandidate ? 124.0f : 74.0f;
            float required = village.Radius + radius + (village.IsStartingVillage ? 220.0f : tierBuffer);
            if (center.DistanceTo(village.Center) < required)
            {
                return false;
            }
        }

        foreach (RoadPathV2 road in _roads)
        {
            float required = road.Width + radius * 0.32f + 6.0f;
            if (road.DistanceToPath(center) < required)
            {
                return false;
            }
        }

        foreach (DungeonEntranceSiteV3 dungeon in _dungeonEntrances)
        {
            float required = radius + dungeon.ApproxRadius + 92.0f;
            if (center.DistanceTo(dungeon.Center) < required)
            {
                return false;
            }
        }

        foreach (BanditCampSiteV3 camp in _banditCamps)
        {
            float required = radius + camp.ApproxRadius + Mathf.Max(100.0f, _settings.V3BanditCampMinDistance);
            if (center.DistanceTo(camp.Center) < required)
            {
                return false;
            }
        }

        return true;
    }

    private bool CanPlaceFactionOutpost(Vector2 center, float radius)
    {
        foreach (VillageSiteV2 village in _villages)
        {
            float tierBuffer = village.Scale is VillageScaleV2.Town or VillageScaleV2.CityCandidate ? 74.0f : 58.0f;
            float required = village.Radius + radius + (village.IsStartingVillage ? 170.0f : tierBuffer);
            if (center.DistanceTo(village.Center) < required)
            {
                return false;
            }
        }

        foreach (RoadPathV2 road in _roads)
        {
            float required = road.Width + radius * 0.28f + 4.0f;
            if (road.DistanceToPath(center) < required)
            {
                return false;
            }
        }

        foreach (BanditCampSiteV3 camp in _banditCamps)
        {
            float required = radius + camp.ApproxRadius + 150.0f;
            if (center.DistanceTo(camp.Center) < required)
            {
                return false;
            }
        }

        foreach (DungeonEntranceSiteV3 dungeon in _dungeonEntrances)
        {
            float required = radius + dungeon.ApproxRadius + 110.0f;
            if (center.DistanceTo(dungeon.Center) < required)
            {
                return false;
            }
        }

        foreach (FactionOutpostSiteV3 outpost in _factionOutposts)
        {
            float required = radius + outpost.ApproxRadius + Mathf.Max(120.0f, _settings.V3FactionOutpostMinDistance);
            if (center.DistanceTo(outpost.Center) < required)
            {
                return false;
            }
        }

        return true;
    }

    private BanditCampSiteV3 CreateBanditCampSite(
        int id,
        BanditCampKindV3 kind,
        Vector2 center,
        float radius,
        string nearbyFeatureHint,
        ref DeterministicRandom random)
    {
        float boundsRadius = radius + 9.0f;
        return new BanditCampSiteV3
        {
            Id = id,
            Kind = kind,
            Center = center,
            ApproxRadius = radius,
            Bounds = new Rect2(center - new Vector2(boundsRadius, boundsRadius), new Vector2(boundsRadius * 2.0f, boundsRadius * 2.0f)),
            Seed = HashIntId(id, WorldSeed, 11201),
            ThreatTier = random.RangeInclusive(1, 3),
            NearbyFeatureHint = nearbyFeatureHint
        };
    }

    private FactionOutpostSiteV3 CreateFactionOutpostSite(
        int id,
        FactionOutpostKindV3 kind,
        FactionOutpostOwnerV3 owner,
        Vector2 center,
        float radius,
        int nearbySettlementId,
        string influenceHint,
        ref DeterministicRandom random)
    {
        float boundsRadius = radius + 10.0f;
        return new FactionOutpostSiteV3
        {
            Id = id,
            Kind = kind,
            Owner = owner,
            Center = center,
            ApproxRadius = radius,
            Bounds = new Rect2(center - new Vector2(boundsRadius, boundsRadius), new Vector2(boundsRadius * 2.0f, boundsRadius * 2.0f)),
            Seed = HashIntId(id, WorldSeed, 12201),
            NearbySettlementId = nearbySettlementId,
            InfluenceHint = influenceHint
        };
    }

    private float GetBanditCampBiomeWeight(BiomeKindV3 biome)
    {
        if (!WorldGenerationLayerSettingsV2.EnableBiomes)
        {
            return 1.0f;
        }

        return biome switch
        {
            BiomeKindV3.ForestLand => _settings.V3ForestLandBanditCampWeightMultiplier,
            BiomeKindV3.RockyHills => _settings.V3RockyHillsBanditCampWeightMultiplier,
            BiomeKindV3.Dryland => _settings.V3DrylandBanditCampWeightMultiplier,
            BiomeKindV3.Wasteland => _settings.V3WastelandBanditCampWeightMultiplier,
            _ => _settings.V3PlainsBanditCampWeightMultiplier
        };
    }

    private float GetFactionOutpostBiomeWeight(BiomeKindV3 biome)
    {
        if (!WorldGenerationLayerSettingsV2.EnableBiomes)
        {
            return 1.0f;
        }

        return biome switch
        {
            BiomeKindV3.ForestLand => _settings.V3ForestLandFactionOutpostWeightMultiplier,
            BiomeKindV3.RockyHills => _settings.V3RockyHillsFactionOutpostWeightMultiplier,
            BiomeKindV3.Dryland => _settings.V3DrylandFactionOutpostWeightMultiplier,
            BiomeKindV3.Wasteland => _settings.V3WastelandFactionOutpostWeightMultiplier,
            _ => _settings.V3PlainsFactionOutpostWeightMultiplier
        };
    }

    private float GetBanditCampFeatureWeight(Vector2 center, out string nearbyFeatureHint)
    {
        nearbyFeatureHint = string.Empty;
        float multiplier = 1.0f;
        float bestDistance = float.MaxValue;

        foreach (RuinSiteV3 ruin in _ruinSites)
        {
            float distance = center.DistanceTo(ruin.Center) - ruin.ApproxRadius;
            if (distance < 300.0f)
            {
                multiplier += 0.34f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearbyFeatureHint = "Ruin";
                }
                break;
            }
        }

        foreach (ForestRegionV3 forest in _forestRegions)
        {
            float distance = Mathf.Abs(center.DistanceTo(forest.Center) - forest.ApproxRadius);
            if (distance < 190.0f)
            {
                multiplier += 0.28f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearbyFeatureHint = "ForestEdge";
                }
                break;
            }
        }

        float roadDistance = GetNearestNonBranchRoadDistance(center);
        if (roadDistance < 230.0f)
        {
            multiplier += 0.22f;
            if (roadDistance < bestDistance)
            {
                nearbyFeatureHint = "Road";
            }
        }

        return Mathf.Clamp(multiplier, 0.70f, 1.82f);
    }

    private float GetFactionOutpostFeatureWeight(Vector2 center, out VillageSiteV2? nearbySettlement, out string influenceHint)
    {
        nearbySettlement = null;
        influenceHint = string.Empty;
        float multiplier = 1.0f;

        float roadDistance = GetNearestNonBranchRoadDistance(center);
        if (roadDistance < 260.0f)
        {
            multiplier += 0.42f;
            influenceHint = "Road";
        }
        else if (roadDistance > 620.0f)
        {
            multiplier -= 0.18f;
            influenceHint = "Isolated";
        }

        if (TryFindNearbySettlementForOutpost(center, out VillageSiteV2? settlement, out float settlementDistance) && settlement != null)
        {
            nearbySettlement = settlement;
            float preferredDistance = settlement.Scale is VillageScaleV2.Town or VillageScaleV2.CityCandidate ? 460.0f : 320.0f;
            if (settlementDistance < preferredDistance)
            {
                multiplier += settlement.Scale is VillageScaleV2.Town or VillageScaleV2.CityCandidate ? 0.38f : 0.18f;
                if (settlement.Scale is VillageScaleV2.Town or VillageScaleV2.CityCandidate)
                {
                    influenceHint = "TradeHub";
                }
                else
                {
                    influenceHint = settlement.Role == SettlementRoleV3.TradeHub ? "TradeHub" : settlement.Role == SettlementRoleV3.Frontier ? "Frontier" : "Settlement";
                }
            }
        }

        return Mathf.Clamp(multiplier, 0.62f, 1.85f);
    }

    private bool TryFindNearbySettlementForOutpost(Vector2 center, out VillageSiteV2? settlement, out float distance)
    {
        settlement = null;
        distance = float.MaxValue;
        foreach (VillageSiteV2 village in _villages)
        {
            float current = center.DistanceTo(village.Center) - village.Radius;
            if (current < distance)
            {
                distance = current;
                settlement = village;
            }
        }

        return settlement != null && distance < 720.0f;
    }

    private BanditCampKindV3 PickBanditCampKind(Vector2 center, BiomeKindV3 biome, string nearbyFeatureHint, ref DeterministicRandom random)
    {
        float hidden = 1.0f + random.NextUnit() * 0.18f;
        float roadside = 0.62f + random.NextUnit() * 0.18f;
        float ruin = 0.58f + random.NextUnit() * 0.18f;
        float forest = 0.66f + random.NextUnit() * 0.18f;
        float wasteland = 0.58f + random.NextUnit() * 0.18f;

        switch (biome)
        {
            case BiomeKindV3.ForestLand:
                forest += 1.02f;
                hidden += 0.24f;
                break;
            case BiomeKindV3.Wasteland:
                wasteland += 1.04f;
                ruin += 0.56f;
                break;
            case BiomeKindV3.Dryland:
                roadside += 0.54f;
                wasteland += 0.42f;
                break;
            case BiomeKindV3.RockyHills:
                hidden += 0.42f;
                break;
        }

        if (nearbyFeatureHint == "Ruin")
        {
            ruin += 0.88f;
        }
        else if (nearbyFeatureHint == "ForestEdge")
        {
            forest += 0.72f;
        }
        else if (nearbyFeatureHint == "Road")
        {
            roadside += 0.80f;
        }

        BanditCampKindV3 bestKind = BanditCampKindV3.HiddenCamp;
        float bestScore = hidden;
        ConsiderBanditKind(BanditCampKindV3.RoadsideAmbush, roadside, ref bestKind, ref bestScore);
        ConsiderBanditKind(BanditCampKindV3.RuinCamp, ruin, ref bestKind, ref bestScore);
        ConsiderBanditKind(BanditCampKindV3.ForestHideout, forest, ref bestKind, ref bestScore);
        ConsiderBanditKind(BanditCampKindV3.WastelandRaidCamp, wasteland, ref bestKind, ref bestScore);
        return bestKind;
    }

    private FactionOutpostKindV3 PickFactionOutpostKind(BiomeKindV3 biome, VillageSiteV2? nearbySettlement, string influenceHint, ref DeterministicRandom random)
    {
        float watch = 1.0f + random.NextUnit() * 0.16f;
        float guard = 0.82f + random.NextUnit() * 0.16f;
        float trade = 0.66f + random.NextUnit() * 0.16f;
        float border = 0.62f + random.NextUnit() * 0.16f;
        float survey = 0.70f + random.NextUnit() * 0.16f;

        switch (biome)
        {
            case BiomeKindV3.Plains:
                watch += 0.32f;
                trade += 0.38f;
                guard += 0.28f;
                break;
            case BiomeKindV3.Dryland:
                border += 0.62f;
                survey += 0.32f;
                break;
            case BiomeKindV3.RockyHills:
                survey += 0.62f;
                guard += 0.30f;
                break;
            case BiomeKindV3.ForestLand:
                watch += 0.30f;
                guard += 0.20f;
                break;
            case BiomeKindV3.Wasteland:
                border += 0.48f;
                survey += 0.36f;
                break;
        }

        if (nearbySettlement?.Role == SettlementRoleV3.TradeHub || influenceHint == "TradeHub")
        {
            trade += 0.74f;
        }

        if (nearbySettlement?.Role == SettlementRoleV3.Frontier || influenceHint == "Frontier")
        {
            border += 0.48f;
            guard += 0.34f;
        }

        if (influenceHint == "Isolated")
        {
            survey += 0.54f;
        }

        FactionOutpostKindV3 bestKind = FactionOutpostKindV3.WatchPost;
        float bestScore = watch;
        ConsiderFactionKind(FactionOutpostKindV3.GuardCamp, guard, ref bestKind, ref bestScore);
        ConsiderFactionKind(FactionOutpostKindV3.TradePost, trade, ref bestKind, ref bestScore);
        ConsiderFactionKind(FactionOutpostKindV3.BorderFort, border, ref bestKind, ref bestScore);
        ConsiderFactionKind(FactionOutpostKindV3.SurveyPost, survey, ref bestKind, ref bestScore);
        return bestKind;
    }

    private FactionOutpostOwnerV3 PickFactionOutpostOwner(BiomeKindV3 biome, VillageSiteV2? nearbySettlement, string influenceHint, ref DeterministicRandom random)
    {
        float kingdom = 0.76f + random.NextUnit() * 0.14f;
        float merchant = 0.48f + random.NextUnit() * 0.14f;
        float frontier = 0.62f + random.NextUnit() * 0.14f;
        float militia = 0.58f + random.NextUnit() * 0.14f;
        float unknown = 0.28f + random.NextUnit() * 0.14f;

        if (nearbySettlement?.Scale is VillageScaleV2.Town or VillageScaleV2.CityCandidate)
        {
            kingdom += 0.46f;
            merchant += 0.24f;
        }

        if (nearbySettlement?.Role == SettlementRoleV3.TradeHub || influenceHint == "TradeHub")
        {
            merchant += 0.76f;
        }

        if (nearbySettlement?.Role == SettlementRoleV3.Frontier || biome is BiomeKindV3.Dryland or BiomeKindV3.Wasteland)
        {
            frontier += 0.44f;
            militia += 0.24f;
        }

        if (biome == BiomeKindV3.Wasteland || influenceHint == "Isolated")
        {
            unknown += 0.42f;
            frontier += 0.22f;
        }

        FactionOutpostOwnerV3 bestOwner = FactionOutpostOwnerV3.Kingdom;
        float bestScore = kingdom;
        ConsiderFactionOwner(FactionOutpostOwnerV3.MerchantGuild, merchant, ref bestOwner, ref bestScore);
        ConsiderFactionOwner(FactionOutpostOwnerV3.NeutralFrontier, frontier, ref bestOwner, ref bestScore);
        ConsiderFactionOwner(FactionOutpostOwnerV3.LocalMilitia, militia, ref bestOwner, ref bestScore);
        ConsiderFactionOwner(FactionOutpostOwnerV3.Unknown, unknown, ref bestOwner, ref bestScore);
        return bestOwner;
    }

    private static void ConsiderBanditKind(BanditCampKindV3 kind, float score, ref BanditCampKindV3 bestKind, ref float bestScore)
    {
        if (score > bestScore)
        {
            bestKind = kind;
            bestScore = score;
        }
    }

    private static void ConsiderFactionKind(FactionOutpostKindV3 kind, float score, ref FactionOutpostKindV3 bestKind, ref float bestScore)
    {
        if (score > bestScore)
        {
            bestKind = kind;
            bestScore = score;
        }
    }

    private static void ConsiderFactionOwner(FactionOutpostOwnerV3 owner, float score, ref FactionOutpostOwnerV3 bestOwner, ref float bestScore)
    {
        if (score > bestScore)
        {
            bestOwner = owner;
            bestScore = score;
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

            BiomeKindV3 biome = ResolveBiomeKindForPlacement(center);
            if (!AcceptBiomeWeightedPlacement(biome, GetQuarryBiomeWeight(biome), ref random))
            {
                RejectedQuarryPlacementCount++;
                continue;
            }

            _quarryRegions.Add(CreateQuarryRegion(nextId++, center, radius, major, ref random));
            IncrementDistribution(_quarryBiomeDistribution, biome);
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

    private bool CanPlaceRuin(Vector2 center, float radius)
    {
        foreach (VillageSiteV2 village in _villages)
        {
            float required = village.Radius + radius + (village.IsStartingVillage ? 138.0f : 46.0f);
            if (center.DistanceTo(village.Center) < required)
            {
                return false;
            }
        }

        foreach (RoadPathV2 road in _roads)
        {
            float required = road.Width + radius * 0.24f + 4.0f;
            if (road.DistanceToPath(center) < required)
            {
                return false;
            }
        }

        foreach (RuinSiteV3 ruin in _ruinSites)
        {
            float required = radius + ruin.ApproxRadius + 42.0f;
            if (center.DistanceTo(ruin.Center) < required)
            {
                return false;
            }
        }

        foreach (QuarryRegionV3 quarry in _quarryRegions)
        {
            float required = radius + quarry.ApproxRadius * 0.26f;
            if (center.DistanceTo(quarry.Center) < required)
            {
                return false;
            }
        }

        return true;
    }

    private RuinSiteV3 CreateRuinSite(int id, Vector2 center, float radius, ref DeterministicRandom random)
    {
        RuinKindV3 kind = (RuinKindV3)random.RangeInclusive(0, 3);
        float boundsRadius = radius + Mathf.Max(0.0f, _settings.V3RuinWarpStrength) + 9.0f;
        return new RuinSiteV3
        {
            Id = id,
            Kind = kind,
            Center = center,
            ApproxRadius = radius,
            Bounds = new Rect2(center - new Vector2(boundsRadius, boundsRadius), new Vector2(boundsRadius * 2.0f, boundsRadius * 2.0f)),
            Seed = HashIntId(id, WorldSeed, 9201),
            Density = random.Range(0.78f, 1.05f)
        };
    }

    private bool CanPlaceDungeonEntrance(Vector2 center, float radius)
    {
        foreach (VillageSiteV2 village in _villages)
        {
            float tierBuffer = village.Scale is VillageScaleV2.Town or VillageScaleV2.CityCandidate ? 70.0f : 42.0f;
            float required = village.Radius + radius + (village.IsStartingVillage ? 180.0f : tierBuffer);
            if (center.DistanceTo(village.Center) < required)
            {
                return false;
            }
        }

        foreach (RoadPathV2 road in _roads)
        {
            float required = road.Width + radius * 0.45f + 5.0f;
            if (road.DistanceToPath(center) < required)
            {
                return false;
            }
        }

        foreach (DungeonEntranceSiteV3 dungeon in _dungeonEntrances)
        {
            float required = radius + dungeon.ApproxRadius + Mathf.Max(80.0f, _settings.V3DungeonEntranceMinDistance);
            if (center.DistanceTo(dungeon.Center) < required)
            {
                return false;
            }
        }

        foreach (RuinSiteV3 ruin in _ruinSites)
        {
            float required = radius + ruin.ApproxRadius * 0.38f;
            if (center.DistanceTo(ruin.Center) < required)
            {
                return false;
            }
        }

        foreach (QuarryRegionV3 quarry in _quarryRegions)
        {
            float required = radius + quarry.ApproxRadius * 0.24f;
            if (center.DistanceTo(quarry.Center) < required)
            {
                return false;
            }
        }

        return true;
    }

    private DungeonEntranceSiteV3 CreateDungeonEntranceSite(
        int id,
        DungeonEntranceKindV3 kind,
        Vector2 center,
        float radius,
        string nearbyFeatureHint,
        ref DeterministicRandom random)
    {
        float boundsRadius = radius + 8.0f;
        return new DungeonEntranceSiteV3
        {
            Id = id,
            Kind = kind,
            Center = center,
            ApproxRadius = radius,
            Bounds = new Rect2(center - new Vector2(boundsRadius, boundsRadius), new Vector2(boundsRadius * 2.0f, boundsRadius * 2.0f)),
            Seed = HashIntId(id, WorldSeed, 10201),
            DangerHint = random.RangeInclusive(1, 3),
            NearbyFeatureHint = nearbyFeatureHint
        };
    }

    private float GetDungeonEntranceBiomeWeight(BiomeKindV3 biome)
    {
        if (!WorldGenerationLayerSettingsV2.EnableBiomes)
        {
            return 1.0f;
        }

        return biome switch
        {
            BiomeKindV3.ForestLand => _settings.V3ForestLandDungeonEntranceWeightMultiplier,
            BiomeKindV3.RockyHills => _settings.V3RockyHillsDungeonEntranceWeightMultiplier,
            BiomeKindV3.Dryland => _settings.V3DrylandDungeonEntranceWeightMultiplier,
            BiomeKindV3.Wasteland => _settings.V3WastelandDungeonEntranceWeightMultiplier,
            _ => _settings.V3PlainsDungeonEntranceWeightMultiplier
        };
    }

    private float GetDungeonEntranceFeatureWeight(Vector2 center, out string nearbyFeatureHint)
    {
        nearbyFeatureHint = string.Empty;
        float multiplier = 1.0f;
        float bestDistance = float.MaxValue;

        foreach (RuinSiteV3 ruin in _ruinSites)
        {
            float distance = center.DistanceTo(ruin.Center) - ruin.ApproxRadius;
            if (distance < 360.0f)
            {
                multiplier += 0.34f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearbyFeatureHint = "Ruin";
                }
                break;
            }
        }

        foreach (QuarryRegionV3 quarry in _quarryRegions)
        {
            float distance = center.DistanceTo(quarry.Center) - quarry.ApproxRadius;
            if (distance < 340.0f)
            {
                multiplier += 0.30f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearbyFeatureHint = "Quarry";
                }
                break;
            }
        }

        foreach (ForestRegionV3 forest in _forestRegions)
        {
            float distance = Mathf.Abs(center.DistanceTo(forest.Center) - forest.ApproxRadius);
            if (distance < 180.0f)
            {
                multiplier += 0.20f;
                if (distance < bestDistance)
                {
                    nearbyFeatureHint = "ForestEdge";
                }
                break;
            }
        }

        return Mathf.Clamp(multiplier, 0.75f, 1.85f);
    }

    private DungeonEntranceKindV3 PickDungeonEntranceKind(Vector2 center, BiomeKindV3 biome, ref DeterministicRandom random)
    {
        float cave = 1.0f + random.NextUnit() * 0.18f;
        float gate = 0.70f + random.NextUnit() * 0.16f;
        float stair = 0.70f + random.NextUnit() * 0.16f;
        float sinkhole = 0.80f + random.NextUnit() * 0.16f;
        float mine = 0.70f + random.NextUnit() * 0.16f;

        switch (biome)
        {
            case BiomeKindV3.RockyHills:
                cave += 0.82f;
                mine += 1.05f;
                break;
            case BiomeKindV3.Wasteland:
                gate += 0.90f;
                stair += 0.92f;
                sinkhole += 0.36f;
                break;
            case BiomeKindV3.ForestLand:
                cave += 0.45f;
                sinkhole += 0.82f;
                break;
            case BiomeKindV3.Dryland:
                gate += 0.48f;
                stair += 0.70f;
                break;
        }

        if (HasNearbyRuin(new Vector2I(Mathf.RoundToInt(center.X), Mathf.RoundToInt(center.Y))))
        {
            gate += 0.58f;
            stair += 0.68f;
        }

        if (HasNearbyQuarry(new Vector2I(Mathf.RoundToInt(center.X), Mathf.RoundToInt(center.Y))))
        {
            mine += 0.72f;
            cave += 0.42f;
        }

        if (HasNearbyForest(new Vector2I(Mathf.RoundToInt(center.X), Mathf.RoundToInt(center.Y))))
        {
            sinkhole += 0.50f;
            cave += 0.35f;
        }

        DungeonEntranceKindV3 bestKind = DungeonEntranceKindV3.CaveMouth;
        float bestScore = cave;
        ConsiderDungeonKind(DungeonEntranceKindV3.AncientGate, gate, ref bestKind, ref bestScore);
        ConsiderDungeonKind(DungeonEntranceKindV3.RuinedStair, stair, ref bestKind, ref bestScore);
        ConsiderDungeonKind(DungeonEntranceKindV3.Sinkhole, sinkhole, ref bestKind, ref bestScore);
        ConsiderDungeonKind(DungeonEntranceKindV3.MineShaft, mine, ref bestKind, ref bestScore);
        return bestKind;
    }

    private static void ConsiderDungeonKind(DungeonEntranceKindV3 kind, float score, ref DungeonEntranceKindV3 bestKind, ref float bestScore)
    {
        if (score > bestScore)
        {
            bestKind = kind;
            bestScore = score;
        }
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
            case WorldMapSizePresetV2.Huge:
                majorCount = random.RangeInclusive(
                    Mathf.Max(0, _settings.V3HugeMajorForestMinCount),
                    Mathf.Max(0, _settings.V3HugeMajorForestMaxCount));
                minorCount = random.RangeInclusive(
                    Mathf.Max(0, _settings.V3HugeMinorForestMinCount),
                    Mathf.Max(0, _settings.V3HugeMinorForestMaxCount));
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
            WorldMapSizePresetV2.Huge => Mathf.Max(0, _settings.V3HugeForestClusterCount),
            _ => Mathf.Max(0, _settings.V3SmallForestClusterCount)
        };
    }

    private float GetLargeForestChance()
    {
        float baseChance = Mathf.Clamp(_settings.V3LargeForestChance, 0.0f, 0.75f);
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Huge => Mathf.Clamp(baseChance + 0.10f, 0.0f, 0.80f),
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
        if (bestVillage.Scale == VillageScaleV2.Hamlet)
        {
            bestVillage.Scale = VillageScaleV2.Village;
            bestVillage.Radius = GetRadiusForScale(VillageScaleV2.Village);
            bestVillage.OccupiedRadius = bestVillage.Radius;
            bestVillage.AvoidRadius = Mathf.Max(bestVillage.Radius + 24.0f, _settings.V3VillageMinDistance * 0.5f);
        }

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

    private void GetTargetSettlementScaleCounts(
        int totalCount,
        out int cityCount,
        out int townCount,
        out int largeCount,
        out int villageCount,
        out int hamletCount)
    {
        switch (MapSizePreset)
        {
            case WorldMapSizePresetV2.Medium:
                cityCount = _settings.V3MediumCityCandidateCount;
                townCount = _settings.V3MediumTownCount;
                largeCount = _settings.V3MediumLargeVillageCount;
                villageCount = Mathf.Min(Mathf.Max(8, Mathf.RoundToInt(totalCount * 0.38f)), totalCount);
                break;
            case WorldMapSizePresetV2.Large:
                cityCount = _settings.V3LargeCityCandidateCount;
                townCount = _settings.V3LargeTownCount;
                largeCount = _settings.V3LargeLargeVillageCount;
                villageCount = Mathf.Min(20, totalCount);
                break;
            case WorldMapSizePresetV2.Huge:
                cityCount = _settings.V3HugeCityCandidateCount;
                townCount = _settings.V3HugeTownCount;
                largeCount = _settings.V3HugeLargeVillageCount;
                villageCount = Mathf.Min(55, totalCount);
                break;
            case WorldMapSizePresetV2.Small:
            default:
                cityCount = _settings.V3SmallCityCandidateCount;
                townCount = _settings.V3SmallTownCount;
                largeCount = _settings.V3SmallLargeVillageCount;
                villageCount = Mathf.Min(4, totalCount);
                break;
        }

        cityCount = Mathf.Clamp(cityCount, 0, totalCount);
        townCount = Mathf.Clamp(townCount, 0, totalCount - cityCount);
        largeCount = Mathf.Clamp(largeCount, 0, totalCount - cityCount - townCount);
        villageCount = Mathf.Clamp(villageCount, 0, totalCount - cityCount - townCount - largeCount);
        hamletCount = Mathf.Max(0, totalCount - cityCount - townCount - largeCount - villageCount);
    }

    private static VillageScaleV2 PickScaleFromQuota(
        ref int cityCount,
        ref int townCount,
        ref int largeCount,
        ref int villageCount,
        ref int hamletCount,
        ref DeterministicRandom random)
    {
        int total = cityCount + townCount + largeCount + villageCount + hamletCount;
        if (total <= 0)
        {
            return VillageScaleV2.Hamlet;
        }

        int roll = random.RangeInclusive(1, total);
        if (roll <= cityCount)
        {
            return VillageScaleV2.CityCandidate;
        }

        roll -= cityCount;
        if (roll <= townCount)
        {
            return VillageScaleV2.Town;
        }

        roll -= townCount;
        if (roll <= largeCount)
        {
            return VillageScaleV2.LargeVillage;
        }

        roll -= largeCount;
        return roll <= villageCount ? VillageScaleV2.Village : VillageScaleV2.Hamlet;
    }

    private static void ConsumeSettlementScaleQuota(
        VillageScaleV2 scale,
        ref int cityCount,
        ref int townCount,
        ref int largeCount,
        ref int villageCount,
        ref int hamletCount)
    {
        switch (scale)
        {
            case VillageScaleV2.CityCandidate when cityCount > 0:
                cityCount--;
                break;
            case VillageScaleV2.Town when townCount > 0:
                townCount--;
                break;
            case VillageScaleV2.LargeVillage when largeCount > 0:
                largeCount--;
                break;
            case VillageScaleV2.Village when villageCount > 0:
                villageCount--;
                break;
            case VillageScaleV2.Hamlet when hamletCount > 0:
                hamletCount--;
                break;
        }
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
            VillageScaleV2.CityCandidate => Mathf.Max(12.0f, _settings.V3CityCandidateRadius),
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

        if (village.Scale is VillageScaleV2.Town or VillageScaleV2.CityCandidate)
        {
            return distance <= coreRadius ? TileType.TownPavement : distance <= edgeRadius ? TileType.Village : TileType.VillageEdge;
        }

        return distance <= edgeRadius ? TileType.Village : TileType.VillageEdge;
    }

    private void AddQuarryRoadTargets(ref int nextId, ref DeterministicRandom random)
    {
        foreach (QuarryRegionV3 quarry in _quarryRegions)
        {
            float chance = quarry.IsMajorQuarry ? 0.72f : 0.42f;
            if (random.NextUnit() > chance)
            {
                continue;
            }

            float angle = random.NextUnit() * Mathf.Tau;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 position = ClampPointToWorld(quarry.Center + direction * quarry.ApproxRadius * random.Range(0.82f, 1.08f), 48.0f);
            RoadTargetAnchorV3 anchor = CreateRoadTarget(nextId, RoadTargetKindV3.Quarry, position, 12.0f, quarry.Id, true);
            if (!TryAddRoadTarget(anchor))
            {
                RejectedRoadTargetCount++;
                continue;
            }

            nextId++;
            RoadTargetQuarryCount++;
        }
    }

    private void AddRuinRoadTargets(ref int nextId, ref DeterministicRandom random)
    {
        if (!WorldGenerationLayerSettingsV2.EnableRuins)
        {
            return;
        }

        foreach (RuinSiteV3 ruin in _ruinSites)
        {
            if (!ruin.IsRoadLinked)
            {
                continue;
            }

            Vector2 direction = Vector2.Right.Rotated(random.NextUnit() * Mathf.Tau);
            if (TryFindNearestNonBranchRoadPoint(ruin.Center, out Vector2 roadPoint))
            {
                Vector2 toRoad = roadPoint - ruin.Center;
                if (toRoad.LengthSquared() > 0.001f)
                {
                    direction = toRoad.Normalized();
                }
            }

            Vector2 position = ClampPointToWorld(ruin.Center + direction * ruin.ApproxRadius * random.Range(0.72f, 0.96f), 48.0f);
            RoadTargetAnchorV3 anchor = CreateRoadTarget(nextId, RoadTargetKindV3.Ruin, position, Mathf.Max(8.0f, ruin.ApproxRadius * 0.34f), ruin.Id, true);
            if (!TryAddRoadTarget(anchor))
            {
                RejectedRoadTargetCount++;
                continue;
            }

            nextId++;
            RoadTargetRuinCount++;
        }
    }

    private void AddDungeonEntranceRoadTargets(ref int nextId, ref DeterministicRandom random)
    {
        if (!WorldGenerationLayerSettingsV2.EnableDungeons)
        {
            return;
        }

        foreach (DungeonEntranceSiteV3 dungeon in _dungeonEntrances)
        {
            if (!dungeon.IsRoadLinked)
            {
                continue;
            }

            Vector2 direction = Vector2.Right.Rotated(random.NextUnit() * Mathf.Tau);
            if (TryFindNearestNonBranchRoadPoint(dungeon.Center, out Vector2 roadPoint))
            {
                Vector2 toRoad = roadPoint - dungeon.Center;
                if (toRoad.LengthSquared() > 0.001f)
                {
                    direction = toRoad.Normalized();
                }
            }

            Vector2 position = ClampPointToWorld(dungeon.Center + direction * dungeon.ApproxRadius * random.Range(0.78f, 1.08f), 48.0f);
            RoadTargetAnchorV3 anchor = CreateRoadTarget(nextId, RoadTargetKindV3.DungeonEntrance, position, Mathf.Max(8.0f, dungeon.ApproxRadius * 0.50f), dungeon.Id, true);
            if (!TryAddRoadTarget(anchor))
            {
                RejectedRoadTargetCount++;
                continue;
            }

            nextId++;
            RoadTargetDungeonEntranceCount++;
        }
    }

    private void AddBanditCampRoadTargets(ref int nextId, ref DeterministicRandom random)
    {
        if (!WorldGenerationLayerSettingsV2.EnableBanditCamps)
        {
            return;
        }

        foreach (BanditCampSiteV3 camp in _banditCamps)
        {
            if (!camp.IsRoadLinked)
            {
                continue;
            }

            Vector2 direction = Vector2.Right.Rotated(random.NextUnit() * Mathf.Tau);
            if (TryFindNearestNonBranchRoadPoint(camp.Center, out Vector2 roadPoint))
            {
                Vector2 toRoad = roadPoint - camp.Center;
                if (toRoad.LengthSquared() > 0.001f)
                {
                    direction = toRoad.Normalized();
                }
            }

            Vector2 position = ClampPointToWorld(camp.Center + direction * camp.ApproxRadius * random.Range(0.78f, 1.08f), 48.0f);
            RoadTargetAnchorV3 anchor = CreateRoadTarget(nextId, RoadTargetKindV3.BanditCamp, position, Mathf.Max(8.0f, camp.ApproxRadius * 0.50f), camp.Id, true);
            if (!TryAddRoadTarget(anchor))
            {
                RejectedRoadTargetCount++;
                continue;
            }

            nextId++;
            RoadTargetBanditCampCount++;
        }
    }

    private void AddFactionOutpostRoadTargets(ref int nextId, ref DeterministicRandom random)
    {
        if (!WorldGenerationLayerSettingsV2.EnableFactionOutposts)
        {
            return;
        }

        foreach (FactionOutpostSiteV3 outpost in _factionOutposts)
        {
            if (!outpost.IsRoadLinked)
            {
                continue;
            }

            Vector2 direction = Vector2.Right.Rotated(random.NextUnit() * Mathf.Tau);
            if (TryFindNearestNonBranchRoadPoint(outpost.Center, out Vector2 roadPoint))
            {
                Vector2 toRoad = roadPoint - outpost.Center;
                if (toRoad.LengthSquared() > 0.001f)
                {
                    direction = toRoad.Normalized();
                }
            }

            Vector2 position = ClampPointToWorld(outpost.Center + direction * outpost.ApproxRadius * random.Range(0.80f, 1.10f), 48.0f);
            RoadTargetAnchorV3 anchor = CreateRoadTarget(nextId, RoadTargetKindV3.FactionOutpost, position, Mathf.Max(8.0f, outpost.ApproxRadius * 0.46f), outpost.Id, true);
            if (!TryAddRoadTarget(anchor))
            {
                RejectedRoadTargetCount++;
                continue;
            }

            nextId++;
            RoadTargetFactionOutpostCount++;
        }
    }

    private void AddForestEdgeRoadTargets(ref int nextId, ref DeterministicRandom random)
    {
        int targetLimit = GetTargetBranchRoadCount(ref random);
        int added = 0;
        foreach (ForestRegionV3 forest in _forestRegions)
        {
            if (added >= targetLimit)
            {
                return;
            }

            float chance = forest.IsMajorForest ? 0.36f : 0.14f;
            if (random.NextUnit() > chance)
            {
                continue;
            }

            float angle = random.NextUnit() * Mathf.Tau;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            float radius = forest.ApproxRadius * random.Range(0.76f, 1.04f);
            Vector2 position = ClampPointToWorld(forest.Center + direction * radius, 48.0f);
            RoadTargetAnchorV3 anchor = CreateRoadTarget(nextId, RoadTargetKindV3.ForestEdge, position, 10.0f, forest.RegionId, false);
            if (!TryAddRoadTarget(anchor))
            {
                RejectedRoadTargetCount++;
                continue;
            }

            nextId++;
            added++;
            RoadTargetForestEdgeCount++;
        }
    }

    private void AddWorldEdgeExitTargets(ref int nextId, ref DeterministicRandom random)
    {
        int targetCount = MapSizePreset switch
        {
            WorldMapSizePresetV2.Small => random.RangeInclusive(0, 1),
            WorldMapSizePresetV2.Medium => random.RangeInclusive(1, 2),
            WorldMapSizePresetV2.Large => random.RangeInclusive(2, 4),
            WorldMapSizePresetV2.Huge => random.RangeInclusive(4, 7),
            _ => 1
        };

        for (int i = 0; i < targetCount; i++)
        {
            int side = random.RangeInclusive(0, 3);
            float margin = 56.0f;
            Vector2 position = side switch
            {
                0 => new Vector2(random.Range(margin, WorldSize.WidthCells - margin), margin),
                1 => new Vector2(WorldSize.WidthCells - margin, random.Range(margin, WorldSize.HeightCells - margin)),
                2 => new Vector2(random.Range(margin, WorldSize.WidthCells - margin), WorldSize.HeightCells - margin),
                _ => new Vector2(margin, random.Range(margin, WorldSize.HeightCells - margin))
            };

            RoadTargetAnchorV3 anchor = CreateRoadTarget(nextId, RoadTargetKindV3.WorldEdgeExit, position, 14.0f, 0, false);
            if (!TryAddRoadTarget(anchor))
            {
                RejectedRoadTargetCount++;
                continue;
            }

            nextId++;
            RoadTargetWorldEdgeExitCount++;
        }
    }

    private RoadTargetAnchorV3 CreateRoadTarget(int id, RoadTargetKindV3 kind, Vector2 position, float radius, int linkedFeatureId, bool implementedPoi)
    {
        float boundsRadius = radius + 3.0f;
        return new RoadTargetAnchorV3
        {
            Id = id,
            Kind = kind,
            Position = position,
            Radius = radius,
            Bounds = new Rect2(position - new Vector2(boundsRadius, boundsRadius), new Vector2(boundsRadius * 2.0f, boundsRadius * 2.0f)),
            Seed = HashIntId(id, WorldSeed, 8123),
            IsImplementedPoi = implementedPoi,
            LinkedFeatureId = linkedFeatureId
        };
    }

    private bool TryAddRoadTarget(RoadTargetAnchorV3 anchor)
    {
        if (!WorldSize.ContainsCell(new Vector2I(Mathf.RoundToInt(anchor.Position.X), Mathf.RoundToInt(anchor.Position.Y))))
        {
            return false;
        }

        if (IsPointTooCloseToVillageCore(anchor.Position))
        {
            return false;
        }

        foreach (RoadTargetAnchorV3 existing in _roadTargetAnchors)
        {
            float required = anchor.Radius + existing.Radius + 92.0f;
            if (anchor.Position.DistanceTo(existing.Position) < required)
            {
                return false;
            }
        }

        foreach (RoadPathV2 road in _roads)
        {
            if (road.Kind == RoadKindV3.Branch)
            {
                continue;
            }

            if (road.DistanceToPath(anchor.Position) < 54.0f)
            {
                return false;
            }
        }

        _roadTargetAnchors.Add(anchor);
        return true;
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
            Kind = primary ? RoadKindV3.Primary : RoadKindV3.Secondary,
            IsMainVillageRoad = primary,
            VisualStrength = primary ? 1.0f : 0.78f,
            VisualWearSeed = HashIntId(from.Id, to.Id, roadId + 6201)
        };
    }

    private void GenerateBranchRoads()
    {
        BranchRoadCount = 0;
        RejectedBranchRoadCount = 0;
        if (!WorldGenerationLayerSettingsV2.EnableRoads || _roadTargetAnchors.Count == 0)
        {
            return;
        }

        DeterministicRandom random = new(MakeSeed(WorldSeed, (int)MapSizePreset, 8201));
        int targetCount = GetTargetBranchRoadCount(ref random);
        if (targetCount <= 0)
        {
            targetCount = Mathf.Min(CountRoadTargets(RoadTargetKindV3.Ruin), 2);
        }

        int ruinTargetCount = CountRoadTargets(RoadTargetKindV3.Ruin);
        if (ruinTargetCount > 0)
        {
            targetCount = Mathf.Max(targetCount, Mathf.Min(ruinTargetCount, 2));
        }

        int dungeonTargetCount = CountRoadTargets(RoadTargetKindV3.DungeonEntrance);
        if (dungeonTargetCount > 0)
        {
            targetCount = Mathf.Max(targetCount, Mathf.Min(dungeonTargetCount, MapSizePreset == WorldMapSizePresetV2.Small ? 1 : 3));
        }

        int banditTargetCount = CountRoadTargets(RoadTargetKindV3.BanditCamp);
        if (banditTargetCount > 0)
        {
            targetCount = Mathf.Max(targetCount, Mathf.Min(banditTargetCount, MapSizePreset == WorldMapSizePresetV2.Small ? 1 : 3));
        }

        int factionTargetCount = CountRoadTargets(RoadTargetKindV3.FactionOutpost);
        if (factionTargetCount > 0)
        {
            targetCount = Mathf.Max(targetCount, Mathf.Min(factionTargetCount, MapSizePreset == WorldMapSizePresetV2.Small ? 1 : 4));
        }

        targetCount = Mathf.Min(targetCount, _roadTargetAnchors.Count);
        if (targetCount <= 0)
        {
            return;
        }

        List<RoadPathV2> primaryRoads = new();
        foreach (RoadPathV2 road in _roads)
        {
            if (road.Kind == RoadKindV3.Primary && road.PathPointsWorld.Count >= 7)
            {
                primaryRoads.Add(road);
            }
        }

        if (primaryRoads.Count == 0)
        {
            return;
        }

        Dictionary<int, int> branchCountByRoad = new();
        HashSet<int> connectedTargetIds = new();
        int maxBranchesPerRoad = MapSizePreset switch
        {
            WorldMapSizePresetV2.Small => 1,
            WorldMapSizePresetV2.Medium => 2,
            WorldMapSizePresetV2.Large => 2,
            WorldMapSizePresetV2.Huge => 2,
            _ => 1
        };
        int nextRoadId = GetNextRoadId();
        int attempts = Mathf.Max(targetCount * 10, 32);

        for (int attempt = 0; attempt < attempts && BranchRoadCount < targetCount; attempt++)
        {
            RoadTargetAnchorV3 target = PickRoadTargetAnchor(connectedTargetIds, ref random);
            if (connectedTargetIds.Contains(target.Id))
            {
                RejectedBranchRoadCount++;
                continue;
            }

            if (!TryFindBranchSource(primaryRoads, target, branchCountByRoad, maxBranchesPerRoad, out RoadPathV2? sourceRoad, out Vector2 branchStart)
                || sourceRoad == null)
            {
                RejectedBranchRoadCount++;
                continue;
            }

            branchCountByRoad.TryGetValue(sourceRoad.Id, out int existingBranchCount);
            if (!TryBuildBranchRoad(sourceRoad, target, branchStart, nextRoadId, ref random, out RoadPathV2? branchRoad) || branchRoad == null)
            {
                RejectedBranchRoadCount++;
                continue;
            }

            _roads.Add(branchRoad);
            branchCountByRoad[sourceRoad.Id] = existingBranchCount + 1;
            connectedTargetIds.Add(target.Id);
            MarkRoadTargetLinked(target, branchRoad.Id);
            BranchRoadCount++;
            nextRoadId++;
        }
    }

    private RoadTargetAnchorV3 PickRoadTargetAnchor(HashSet<int> connectedTargetIds, ref DeterministicRandom random)
    {
        if (random.NextUnit() < 0.70f)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                RoadTargetAnchorV3 target = _roadTargetAnchors[random.RangeInclusive(0, _roadTargetAnchors.Count - 1)];
                if (IsImplementedRoadTargetKind(target.Kind) && !connectedTargetIds.Contains(target.Id))
                {
                    return target;
                }
            }
        }

        for (int attempt = 0; attempt < 12; attempt++)
        {
            RoadTargetAnchorV3 target = _roadTargetAnchors[random.RangeInclusive(0, _roadTargetAnchors.Count - 1)];
            if (!connectedTargetIds.Contains(target.Id))
            {
                return target;
            }
        }

        return _roadTargetAnchors[random.RangeInclusive(0, _roadTargetAnchors.Count - 1)];
    }

    private int CountRoadTargets(RoadTargetKindV3 kind)
    {
        int count = 0;
        foreach (RoadTargetAnchorV3 target in _roadTargetAnchors)
        {
            if (target.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsImplementedRoadTargetKind(RoadTargetKindV3 kind)
    {
        return kind is RoadTargetKindV3.Ruin
            or RoadTargetKindV3.DungeonEntrance
            or RoadTargetKindV3.BanditCamp
            or RoadTargetKindV3.FactionOutpost;
    }

    private void MarkRoadTargetLinked(RoadTargetAnchorV3 target, int roadId)
    {
        if (target.Kind == RoadTargetKindV3.Ruin)
        {
            foreach (RuinSiteV3 ruin in _ruinSites)
            {
                if (ruin.Id == target.LinkedFeatureId)
                {
                    ruin.LinkedRoadId = roadId;
                    return;
                }
            }
        }
        else if (target.Kind == RoadTargetKindV3.DungeonEntrance)
        {
            foreach (DungeonEntranceSiteV3 dungeon in _dungeonEntrances)
            {
                if (dungeon.Id == target.LinkedFeatureId)
                {
                    dungeon.LinkedRoadId = roadId;
                    return;
                }
            }
        }
        else if (target.Kind == RoadTargetKindV3.BanditCamp)
        {
            foreach (BanditCampSiteV3 camp in _banditCamps)
            {
                if (camp.Id == target.LinkedFeatureId)
                {
                    camp.LinkedRoadId = roadId;
                    return;
                }
            }
        }
        else if (target.Kind == RoadTargetKindV3.FactionOutpost)
        {
            foreach (FactionOutpostSiteV3 outpost in _factionOutposts)
            {
                if (outpost.Id == target.LinkedFeatureId)
                {
                    outpost.LinkedRoadId = roadId;
                    return;
                }
            }
        }
    }

    private bool TryFindBranchSource(
        IReadOnlyList<RoadPathV2> primaryRoads,
        RoadTargetAnchorV3 target,
        Dictionary<int, int> branchCountByRoad,
        int maxBranchesPerRoad,
        out RoadPathV2? sourceRoad,
        out Vector2 branchStart)
    {
        sourceRoad = null;
        branchStart = Vector2.Zero;
        float minLength = Mathf.Max(48.0f, _settings.V3BranchRoadMinLength);
        float maxLength = Mathf.Max(minLength, _settings.V3BranchRoadMaxLength);
        float bestScore = float.MaxValue;

        foreach (RoadPathV2 road in primaryRoads)
        {
            branchCountByRoad.TryGetValue(road.Id, out int existingBranchCount);
            if (existingBranchCount >= maxBranchesPerRoad)
            {
                continue;
            }

            IReadOnlyList<Vector2> points = road.PathPointsWorld;
            int minIndex = Mathf.Max(1, Mathf.FloorToInt((points.Count - 1) * 0.25f));
            int maxIndex = Mathf.Min(points.Count - 2, Mathf.CeilToInt((points.Count - 1) * 0.75f));
            for (int i = minIndex; i <= maxIndex; i += 2)
            {
                Vector2 point = points[i];
                float distance = point.DistanceTo(target.Position);
                if (distance < minLength || distance > maxLength)
                {
                    continue;
                }

                float score = distance + StableUnitFloat(road.Id, target.Id, i + 8301) * 28.0f;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                sourceRoad = road;
                branchStart = point;
            }
        }

        return sourceRoad != null;
    }

    private bool TryBuildBranchRoad(RoadPathV2 sourceRoad, RoadTargetAnchorV3 target, Vector2 start, int roadId, ref DeterministicRandom random, out RoadPathV2? branchRoad)
    {
        branchRoad = null;
        Vector2 end = target.Position;
        float minLength = Mathf.Max(48.0f, _settings.V3BranchRoadMinLength);
        float maxLength = Mathf.Max(minLength, _settings.V3BranchRoadMaxLength);
        float actualLength = start.DistanceTo(end);
        if (actualLength < minLength || actualLength > maxLength)
        {
            return false;
        }

        if (sourceRoad.DistanceToPath(end) < Mathf.Min(actualLength * 0.30f, 96.0f))
        {
            return false;
        }

        List<Vector2> points = BuildBranchRoadPoints(start, end, roadId, ref random);
        if (points.Count < 2 || BranchPathHitsProtectedVillage(points) || BranchPathCutsTargetCore(points, target))
        {
            return false;
        }

        float width = Mathf.Max(0.55f, _settings.V3RoadWidth * Mathf.Clamp(_settings.V3BranchRoadWidthMultiplier, 0.35f, 1.0f));
        branchRoad = new RoadPathV2
        {
            Id = roadId,
            FromSiteId = 0,
            ToSiteId = 0,
            PathPointsWorld = points,
            Bounds = GrowRect(CalculatePathBounds(points), width + 3.0f),
            Width = width,
            Kind = RoadKindV3.Branch,
            IsMainVillageRoad = false,
            VisualStrength = 0.58f,
            VisualWearSeed = HashIntId(sourceRoad.Id, roadId, 8801),
            BranchOrigin = start,
            TargetAnchorId = target.Id,
            TargetAnchorKind = target.Kind
        };
        return true;
    }

    private List<Vector2> BuildBranchRoadPoints(Vector2 start, Vector2 end, int roadId, ref DeterministicRandom random)
    {
        Vector2 delta = end - start;
        float distance = Mathf.Max(1.0f, delta.Length());
        Vector2 forward = delta / distance;
        Vector2 perpendicular = new(-forward.Y, forward.X);
        int middlePointCount = distance switch
        {
            < 190.0f => 2,
            < 330.0f => 3,
            _ => 4
        };
        float multiplier = Mathf.Clamp(_settings.V3BranchRoadMeanderMultiplier, 0.8f, 2.4f);
        float meander = Mathf.Min(_settings.V3RoadMeanderStrength * multiplier, distance * 0.24f);
        float phase = StableUnitFloat(WorldSeed, roadId, 8611) * Mathf.Tau;

        List<Vector2> controls = new(middlePointCount + 2)
        {
            start
        };

        for (int i = 1; i <= middlePointCount; i++)
        {
            float t = i / (middlePointCount + 1.0f);
            Vector2 basePoint = start.Lerp(end, t);
            float wave = Mathf.Sin(t * Mathf.Tau * 0.92f + phase) * 0.48f
                + Mathf.Sin(t * Mathf.Tau * 1.73f + phase * 1.31f) * 0.30f
                + Mathf.Sin(t * Mathf.Tau * 2.51f + phase * 0.47f) * 0.12f;
            float jitter = (random.NextUnit() - 0.5f) * 0.24f;
            float falloff = Mathf.Sin(t * Mathf.Pi);
            controls.Add(basePoint + perpendicular * (wave + jitter) * meander * falloff);
        }

        controls.Add(end);
        return SampleCatmullRom(SmoothRoadPoints(controls), 4);
    }

    private List<Vector2> BuildMeanderingPoints(Vector2I from, Vector2I to, int roadId, bool primary, ref DeterministicRandom random)
    {
        Vector2 start = new(from.X + 0.5f, from.Y + 0.5f);
        Vector2 end = new(to.X + 0.5f, to.Y + 0.5f);
        return BuildMeanderingPoints(start, end, roadId, primary ? RoadKindV3.Primary : RoadKindV3.Secondary, ref random);
    }

    private List<Vector2> BuildMeanderingPoints(Vector2 start, Vector2 end, int roadId, RoadKindV3 roadKind, ref DeterministicRandom random)
    {
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
        float kindMeander = roadKind switch
        {
            RoadKindV3.Primary => 1.0f,
            RoadKindV3.Branch => 1.35f,
            _ => 1.18f
        };
        float meander = Mathf.Min(_settings.V3RoadMeanderStrength, distance * 0.16f) * kindMeander;
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

    private int GetTargetBranchRoadCount(ref DeterministicRandom random)
    {
        GetBranchRoadCountRange(out int minCount, out int maxCount);
        minCount = Mathf.Max(0, minCount);
        maxCount = Mathf.Max(minCount, maxCount);
        return random.RangeInclusive(minCount, maxCount);
    }

    private void GetBranchRoadCountRange(out int minCount, out int maxCount)
    {
        switch (MapSizePreset)
        {
            case WorldMapSizePresetV2.Medium:
                minCount = _settings.V3MediumBranchRoadMinCount;
                maxCount = _settings.V3MediumBranchRoadMaxCount;
                return;
            case WorldMapSizePresetV2.Large:
                minCount = _settings.V3LargeBranchRoadMinCount;
                maxCount = _settings.V3LargeBranchRoadMaxCount;
                return;
            case WorldMapSizePresetV2.Huge:
                minCount = _settings.V3HugeBranchRoadMinCount;
                maxCount = _settings.V3HugeBranchRoadMaxCount;
                return;
            case WorldMapSizePresetV2.Small:
            default:
                minCount = _settings.V3SmallBranchRoadMinCount;
                maxCount = _settings.V3SmallBranchRoadMaxCount;
                return;
        }
    }

    private static Vector2 GetRoadTangent(IReadOnlyList<Vector2> points, int index)
    {
        int previous = Mathf.Max(0, index - 1);
        int next = Mathf.Min(points.Count - 1, index + 1);
        Vector2 tangent = points[next] - points[previous];
        return tangent.LengthSquared() <= 0.0001f ? Vector2.Zero : tangent.Normalized();
    }

    private Vector2 ClampPointToWorld(Vector2 point, float margin)
    {
        float maxX = Mathf.Max(margin, WorldSize.WidthCells - margin - 1.0f);
        float maxY = Mathf.Max(margin, WorldSize.HeightCells - margin - 1.0f);
        return new Vector2(
            Mathf.Clamp(point.X, margin, maxX),
            Mathf.Clamp(point.Y, margin, maxY));
    }

    private bool BranchPathHitsProtectedVillage(IReadOnlyList<Vector2> points)
    {
        foreach (Vector2 point in points)
        {
            foreach (VillageSiteV2 village in _villages)
            {
                float protectedRadius = village.IsStartingVillage
                    ? village.Radius + 12.0f
                    : village.Radius * 0.78f;
                if (point.DistanceTo(village.Center) <= protectedRadius)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool BranchPathCutsTargetCore(IReadOnlyList<Vector2> points, RoadTargetAnchorV3 target)
    {
        if (target.Kind == RoadTargetKindV3.Quarry)
        {
            foreach (QuarryRegionV3 quarry in _quarryRegions)
            {
                if (quarry.Id == target.LinkedFeatureId)
                {
                    return BranchPathCutsCore(points, quarry.Center, quarry.ApproxRadius * 0.52f);
                }
            }
        }
        else if (target.Kind == RoadTargetKindV3.BanditCamp)
        {
            foreach (BanditCampSiteV3 camp in _banditCamps)
            {
                if (camp.Id == target.LinkedFeatureId)
                {
                    return BranchPathCutsCore(points, camp.Center, camp.ApproxRadius * 0.56f);
                }
            }
        }
        else if (target.Kind == RoadTargetKindV3.FactionOutpost)
        {
            foreach (FactionOutpostSiteV3 outpost in _factionOutposts)
            {
                if (outpost.Id == target.LinkedFeatureId)
                {
                    return BranchPathCutsCore(points, outpost.Center, outpost.ApproxRadius * 0.54f);
                }
            }
        }

        return false;
    }

    private static bool BranchPathCutsCore(IReadOnlyList<Vector2> points, Vector2 center, float coreRadius)
    {
        for (int i = 0; i < points.Count - 2; i++)
        {
            if (points[i].DistanceTo(center) < coreRadius)
            {
                return true;
            }
        }

        return false;
    }

    private float GetMaxLocalRoadLength()
    {
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Small => 850.0f,
            WorldMapSizePresetV2.Medium => 1150.0f,
            WorldMapSizePresetV2.Large => 1450.0f,
            WorldMapSizePresetV2.Huge => 1750.0f,
            _ => 850.0f
        };
    }

    private float GetSharedExitTrunkMaxLength()
    {
        return MapSizePreset switch
        {
            WorldMapSizePresetV2.Small => Mathf.Max(24.0f, _settings.V3SharedExitTrunkMaxLengthSmall),
            WorldMapSizePresetV2.Medium => Mathf.Max(32.0f, _settings.V3SharedExitTrunkMaxLengthMedium),
            WorldMapSizePresetV2.Large => Mathf.Max(40.0f, _settings.V3SharedExitTrunkMaxLengthLarge),
            WorldMapSizePresetV2.Huge => Mathf.Max(48.0f, _settings.V3SharedExitTrunkMaxLengthHuge),
            _ => Mathf.Max(24.0f, _settings.V3SharedExitTrunkMaxLengthSmall)
        };
    }

    private int CalculateMaxJunctionDegree()
    {
        Dictionary<int, int> degreeByNode = new();
        foreach (RoadEdgeV3 edge in _roadGraph.Edges)
        {
            degreeByNode.TryGetValue(edge.FromNodeId, out int fromDegree);
            degreeByNode[edge.FromNodeId] = fromDegree + 1;
            degreeByNode.TryGetValue(edge.ToNodeId, out int toDegree);
            degreeByNode[edge.ToNodeId] = toDegree + 1;
        }

        int maxDegree = 0;
        foreach (RoadNodeV3 node in _roadGraph.Nodes)
        {
            if (node.Kind != RoadNodeKindV3.Junction)
            {
                continue;
            }

            degreeByNode.TryGetValue(node.Id, out int degree);
            maxDegree = Mathf.Max(maxDegree, degree);
            if (degree > Mathf.Clamp(_settings.V3MaxRoadJunctionDegree, 2, 4))
            {
                RejectedHighDegreeJunctionCount++;
            }
        }

        return maxDegree;
    }

    private bool IsPointTooCloseToVillageCore(Vector2 point)
    {
        foreach (VillageSiteV2 village in _villages)
        {
            float protectedRadius = village.IsStartingVillage
                ? village.Radius + 72.0f
                : village.Radius + 34.0f;
            if (point.DistanceTo(village.Center) < protectedRadius)
            {
                return true;
            }
        }

        return false;
    }

    private float GetNearestNonBranchRoadDistance(Vector2 point)
    {
        float best = float.MaxValue;
        foreach (RoadPathV2 road in _roads)
        {
            if (road.Kind == RoadKindV3.Branch)
            {
                continue;
            }

            best = Mathf.Min(best, road.DistanceToPath(point));
        }

        return best;
    }

    private bool TryFindNearestNonBranchRoadPoint(Vector2 point, out Vector2 roadPoint)
    {
        roadPoint = Vector2.Zero;
        float best = float.MaxValue;
        bool found = false;

        foreach (RoadPathV2 road in _roads)
        {
            if (road.Kind == RoadKindV3.Branch)
            {
                continue;
            }

            IReadOnlyList<Vector2> points = road.PathPointsWorld;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 closest = ClosestPointOnSegment(point, points[i], points[i + 1]);
                float distanceSquared = point.DistanceSquaredTo(closest);
                if (distanceSquared >= best)
                {
                    continue;
                }

                best = distanceSquared;
                roadPoint = closest;
                found = true;
            }
        }

        return found;
    }

    private int GetNextRoadId()
    {
        int nextRoadId = 1;
        foreach (RoadPathV2 road in _roads)
        {
            nextRoadId = Mathf.Max(nextRoadId, road.Id + 1);
        }

        return nextRoadId;
    }

    private static Vector2 GetVillagePoint(VillageSiteV2 village)
    {
        return new Vector2(village.Center.X + 0.5f, village.Center.Y + 0.5f);
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float abC = Cross(b - a, c - a);
        float abD = Cross(b - a, d - a);
        float cdA = Cross(d - c, a - c);
        float cdB = Cross(d - c, b - c);
        if (Mathf.Abs(abC) < 0.001f || Mathf.Abs(abD) < 0.001f || Mathf.Abs(cdA) < 0.001f || Mathf.Abs(cdB) < 0.001f)
        {
            return false;
        }

        return (abC > 0.0f) != (abD > 0.0f) && (cdA > 0.0f) != (cdB > 0.0f);
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    private readonly record struct RoadCandidateEdgeV3(
        VillageSiteV2 From,
        VillageSiteV2 To,
        float Distance,
        float Score,
        bool IsExtra)
    {
        public bool SharesEndpoint(RoadCandidateEdgeV3 other)
        {
            return From.Id == other.From.Id
                || From.Id == other.To.Id
                || To.Id == other.From.Id
                || To.Id == other.To.Id;
        }
    }

    private sealed class DisjointSetV3
    {
        private readonly int[] _parent;
        private readonly int[] _rank;

        public DisjointSetV3(int count)
        {
            _parent = new int[count];
            _rank = new int[count];
            for (int i = 0; i < count; i++)
            {
                _parent[i] = i;
            }
        }

        public int Find(int value)
        {
            if (_parent[value] != value)
            {
                _parent[value] = Find(_parent[value]);
            }

            return _parent[value];
        }

        public void Union(int a, int b)
        {
            int rootA = Find(a);
            int rootB = Find(b);
            if (rootA == rootB)
            {
                return;
            }

            if (_rank[rootA] < _rank[rootB])
            {
                _parent[rootA] = rootB;
            }
            else if (_rank[rootA] > _rank[rootB])
            {
                _parent[rootB] = rootA;
            }
            else
            {
                _parent[rootB] = rootA;
                _rank[rootA]++;
            }
        }
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
                WorldMapSizePresetV2.Huge => 1550.0f,
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
            VillageScaleV2.CityCandidate => 1.72f,
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
            WorldMapSizePresetV2.Huge => Mathf.Max(0.0f, _settings.V3RoadExtraLinkRatioHuge),
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
        Vector2 closest = ClosestPointOnSegment(point, a, b);
        return point.DistanceSquaredTo(closest);
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return a;
        }

        float t = Mathf.Clamp((point - a).Dot(ab) / lengthSquared, 0.0f, 1.0f);
        return a + ab * t;
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
        return $"V3 settlements: count={VillageCount} hamlet={HamletCount} village={VillageTierCount} large={LargeVillageCount} town={TownCount} cityCandidate={CityCandidateCount} startId={StartingVillageId} startTier={StartingSettlementTier} startRole={StartingSettlementRole} startCenter={StartingVillageCenter} spawn={PlayerSpawnCell} nearestCenter={NearestToWorldCenterDistance:0.0} roleDist={SettlementRoleDistribution} biomes={BiomeRegionCount} majorBiomes={MajorBiomeRegionCount} minorBiomes={MinorBiomeRegionCount} avgMajorBiomeRadius={AverageMajorBiomeRadius:0} avgMinorBiomeRadius={AverageMinorBiomeRadius:0} biomeKinds forest/rocky/dry/waste={BiomeForestLandCount}/{BiomeRockyHillsCount}/{BiomeDrylandCount}/{BiomeWastelandCount} biomeLayer={BiomeLayerEnabled} roads={RoadCount} primary={PrimaryRoadCount} secondary={SecondaryRoadCount} extra={ExtraRoadCount} branch={BranchRoadCount} nodes={RoadNodeCount} junctions={RoadJunctionCount} maxDegree={MaxRoadJunctionDegree} trunks={SharedTrunkCount} merged={MergedRoadCandidateCount} rejectedJunctions={RejectedRoadJunctionCount} rejectedHighDegree={RejectedHighDegreeJunctionCount} rejectedCrossings={RejectedRoadCrossingCount} rejectedTooLong={RejectedRoadTooLongCount} targets={RoadTargetAnchorCount} quarryTargets={RoadTargetQuarryCount} ruinTargets={RoadTargetRuinCount} dungeonTargets={RoadTargetDungeonEntranceCount} banditTargets={RoadTargetBanditCampCount} factionTargets={RoadTargetFactionOutpostCount} forestTargets={RoadTargetForestEdgeCount} edgeTargets={RoadTargetWorldEdgeExitCount} futureTargets={FutureRoadTargetCount} rejectedTargets={RejectedRoadTargetCount} rejectedBranches={RejectedBranchRoadCount} roadLayer={RoadLayerEnabled} forestRegions={ForestRegionCount} majorForests={MajorForestRegionCount} minorForests={MinorForestPatchCount} forestLayer={ForestLayerEnabled} rejectedForests={RejectedForestPlacementCount} forestBiomeDist={ForestBiomeDistribution} quarryRegions={QuarryRegionCount} majorQuarries={MajorQuarryCount} minorQuarries={MinorQuarryCount} quarryLayer={QuarryLayerEnabled} rejectedQuarries={RejectedQuarryPlacementCount} quarryBiomeDist={QuarryBiomeDistribution} ruins={RuinSiteCount} roadLinkedRuins={RoadLinkedRuinCount} ruinLayer={RuinLayerEnabled} rejectedRuins={RejectedRuinPlacementCount} ruinBiomeDist={RuinBiomeDistribution} dungeons={DungeonEntranceCount} roadLinkedDungeons={RoadLinkedDungeonEntranceCount} dungeonLayer={DungeonLayerEnabled} rejectedDungeons={RejectedDungeonEntrancePlacementCount} dungeonKinds={DungeonEntranceKindDistribution} dungeonBiomeDist={DungeonEntranceBiomeDistribution} bandits={BanditCampCount} roadLinkedBandits={RoadLinkedBanditCampCount} banditLayer={BanditLayerEnabled} rejectedBandits={RejectedBanditCampPlacementCount} banditKinds={BanditCampKindDistribution} banditBiomeDist={BanditCampBiomeDistribution} factionOutposts={FactionOutpostCount} roadLinkedFaction={RoadLinkedFactionOutpostCount} factionLayer={FactionOutpostLayerEnabled} rejectedFaction={RejectedFactionOutpostPlacementCount} factionKinds={FactionOutpostKindDistribution} factionOwners={FactionOutpostOwnerDistribution} factionBiomeDist={FactionOutpostBiomeDistribution}";
    }

    private void ClearFeatureBiomeDistributions()
    {
        ClearDistribution(_forestBiomeDistribution);
        ClearDistribution(_quarryBiomeDistribution);
        ClearDistribution(_ruinBiomeDistribution);
        ClearDistribution(_dungeonEntranceBiomeDistribution);
        ClearDistribution(_banditCampBiomeDistribution);
        ClearDistribution(_factionOutpostBiomeDistribution);
    }

    private static void ClearDistribution(int[] distribution)
    {
        for (int i = 0; i < distribution.Length; i++)
        {
            distribution[i] = 0;
        }
    }

    private static void IncrementDistribution(int[] distribution, BiomeKindV3 biome)
    {
        int index = Mathf.Clamp((int)biome, 0, distribution.Length - 1);
        distribution[index]++;
    }

    private static void IncrementDistribution(int[] distribution, int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, distribution.Length - 1);
        distribution[safeIndex]++;
    }

    private static string FormatBiomeDistribution(int[] distribution)
    {
        return $"P/F/R/D/W={distribution[(int)BiomeKindV3.Plains]}/{distribution[(int)BiomeKindV3.ForestLand]}/{distribution[(int)BiomeKindV3.RockyHills]}/{distribution[(int)BiomeKindV3.Dryland]}/{distribution[(int)BiomeKindV3.Wasteland]}";
    }

    private static string FormatDungeonEntranceKindDistribution(int[] distribution)
    {
        return $"cave/gate/stair/sink/mine={distribution[(int)DungeonEntranceKindV3.CaveMouth]}/{distribution[(int)DungeonEntranceKindV3.AncientGate]}/{distribution[(int)DungeonEntranceKindV3.RuinedStair]}/{distribution[(int)DungeonEntranceKindV3.Sinkhole]}/{distribution[(int)DungeonEntranceKindV3.MineShaft]}";
    }

    private static string FormatBanditCampKindDistribution(int[] distribution)
    {
        return $"hidden/road/ruin/forest/waste={distribution[(int)BanditCampKindV3.HiddenCamp]}/{distribution[(int)BanditCampKindV3.RoadsideAmbush]}/{distribution[(int)BanditCampKindV3.RuinCamp]}/{distribution[(int)BanditCampKindV3.ForestHideout]}/{distribution[(int)BanditCampKindV3.WastelandRaidCamp]}";
    }

    private static string FormatFactionOutpostKindDistribution(int[] distribution)
    {
        return $"watch/guard/trade/border/survey={distribution[(int)FactionOutpostKindV3.WatchPost]}/{distribution[(int)FactionOutpostKindV3.GuardCamp]}/{distribution[(int)FactionOutpostKindV3.TradePost]}/{distribution[(int)FactionOutpostKindV3.BorderFort]}/{distribution[(int)FactionOutpostKindV3.SurveyPost]}";
    }

    private static string FormatFactionOutpostOwnerDistribution(int[] distribution)
    {
        return $"kingdom/merchant/frontier/militia/unknown={distribution[(int)FactionOutpostOwnerV3.Kingdom]}/{distribution[(int)FactionOutpostOwnerV3.MerchantGuild]}/{distribution[(int)FactionOutpostOwnerV3.NeutralFrontier]}/{distribution[(int)FactionOutpostOwnerV3.LocalMilitia]}/{distribution[(int)FactionOutpostOwnerV3.Unknown]}";
    }

    private static string FormatRoleDistribution(int[] distribution)
    {
        return $"common/farm/trade/mining/forest/frontier/ruin/start={distribution[(int)SettlementRoleV3.Common]}/{distribution[(int)SettlementRoleV3.Farming]}/{distribution[(int)SettlementRoleV3.TradeHub]}/{distribution[(int)SettlementRoleV3.Mining]}/{distribution[(int)SettlementRoleV3.ForestEdge]}/{distribution[(int)SettlementRoleV3.Frontier]}/{distribution[(int)SettlementRoleV3.RuinNeighbor]}/{distribution[(int)SettlementRoleV3.StartingSettlement]}";
    }

    private int CountSettlementsByScale(VillageScaleV2 scale)
    {
        int count = 0;
        foreach (VillageSiteV2 village in _villages)
        {
            if (village.Scale == scale)
            {
                count++;
            }
        }

        return count;
    }

    private static FlatlandCellSampleV2 CreateOutOfBoundsSample(Vector2I globalCell)
    {
        return new FlatlandCellSampleV2
        {
            GlobalCellCoord = globalCell,
            IsBuildRestricted = true,
            IsWalkable = false,
            BiomeKind = BiomeKindV3.Wasteland,
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
