using System;
using System.Collections.Generic;
using System.Linq;
using GameplayV3.Company;
using GameplayV3.Deployment;
using GameplayV3.Mercenary;
using GameplayV3.Movement;
using GameplayV3.Session;
using GameplayV3.Control;
using GameplayV3.Resources;
using GameplayV3.Work;
using GameplayV3.Stockpile;
using GameplayV3.Construction;
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
    public int V3HamletCount => _generator.V3HamletCount;
    public int V3VillageTierCount => _generator.V3VillageTierCount;
    public int V3LargeVillageCount => _generator.V3LargeVillageCount;
    public int V3TownCount => _generator.V3TownCount;
    public int V3CityCandidateCount => _generator.V3CityCandidateCount;
    public string V3SettlementRoleDistribution => _generator.V3SettlementRoleDistribution;
    public int V3StartingVillageId => _generator.V3StartingVillageId;
    public VillageScaleV2 V3StartingSettlementTier => _generator.V3StartingSettlementTier;
    public SettlementRoleV3 V3StartingSettlementRole => _generator.V3StartingSettlementRole;
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
    public int V3RoadTargetDungeonEntranceCount => _generator.V3RoadTargetDungeonEntranceCount;
    public int V3RoadTargetBanditCampCount => _generator.V3RoadTargetBanditCampCount;
    public int V3RoadTargetFactionOutpostCount => _generator.V3RoadTargetFactionOutpostCount;
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
    public int V3RejectedForestPlacementCount => _generator.V3RejectedForestPlacementCount;
    public string V3ForestBiomeDistribution => _generator.V3ForestBiomeDistribution;
    public string V3MajorForestBiomeDistribution => _generator.V3MajorForestBiomeDistribution;
    public string V3MinorForestBiomeDistribution => _generator.V3MinorForestBiomeDistribution;
    public int V3ForestTotalBonusApplied => _generator.V3ForestTotalBonusApplied;
    public int V3BiomeQuotaFallbackCount => _generator.V3BiomeQuotaFallbackCount;
    public bool V3BiomeFeatureDistributionEnabled => _generator.V3BiomeFeatureDistributionEnabled;
    public bool V3ForestLayerEnabled => _generator.V3ForestLayerEnabled;
    public int V3QuarryClusterCount => _generator.V3QuarryClusterCount;
    public int V3QuarryRegionCount => _generator.V3QuarryRegionCount;
    public int V3MajorQuarryCount => _generator.V3MajorQuarryCount;
    public int V3MinorQuarryCount => _generator.V3MinorQuarryCount;
    public int V3RejectedQuarryPlacementCount => _generator.V3RejectedQuarryPlacementCount;
    public string V3QuarryBiomeDistribution => _generator.V3QuarryBiomeDistribution;
    public bool V3QuarryLayerEnabled => _generator.V3QuarryLayerEnabled;
    public int V3RuinSiteCount => _generator.V3RuinSiteCount;
    public int V3RoadLinkedRuinCount => _generator.V3RoadLinkedRuinCount;
    public int V3RejectedRuinPlacementCount => _generator.V3RejectedRuinPlacementCount;
    public string V3RuinBiomeDistribution => _generator.V3RuinBiomeDistribution;
    public bool V3RuinLayerEnabled => _generator.V3RuinLayerEnabled;
    public int V3DungeonEntranceCount => _generator.V3DungeonEntranceCount;
    public int V3RoadLinkedDungeonEntranceCount => _generator.V3RoadLinkedDungeonEntranceCount;
    public int V3RejectedDungeonEntrancePlacementCount => _generator.V3RejectedDungeonEntrancePlacementCount;
    public string V3DungeonEntranceKindDistribution => _generator.V3DungeonEntranceKindDistribution;
    public string V3DungeonEntranceBiomeDistribution => _generator.V3DungeonEntranceBiomeDistribution;
    public bool V3DungeonLayerEnabled => _generator.V3DungeonLayerEnabled;
    public int V3BanditCampCount => _generator.V3BanditCampCount;
    public int V3RoadLinkedBanditCampCount => _generator.V3RoadLinkedBanditCampCount;
    public int V3RejectedBanditCampPlacementCount => _generator.V3RejectedBanditCampPlacementCount;
    public string V3BanditCampKindDistribution => _generator.V3BanditCampKindDistribution;
    public string V3BanditCampBiomeDistribution => _generator.V3BanditCampBiomeDistribution;
    public bool V3BanditLayerEnabled => _generator.V3BanditLayerEnabled;
    public int V3FactionOutpostCount => _generator.V3FactionOutpostCount;
    public int V3RoadLinkedFactionOutpostCount => _generator.V3RoadLinkedFactionOutpostCount;
    public int V3RejectedFactionOutpostPlacementCount => _generator.V3RejectedFactionOutpostPlacementCount;
    public string V3FactionOutpostKindDistribution => _generator.V3FactionOutpostKindDistribution;
    public string V3FactionOutpostOwnerDistribution => _generator.V3FactionOutpostOwnerDistribution;
    public string V3FactionOutpostBiomeDistribution => _generator.V3FactionOutpostBiomeDistribution;
    public bool V3FactionOutpostLayerEnabled => _generator.V3FactionOutpostLayerEnabled;
    public int V3BiomeRegionCount => _generator.V3BiomeRegionCount;
    public int V3MajorBiomeRegionCount => _generator.V3MajorBiomeRegionCount;
    public int V3MinorBiomeRegionCount => _generator.V3MinorBiomeRegionCount;
    public float V3AverageMajorBiomeRadius => _generator.V3AverageMajorBiomeRadius;
    public float V3AverageMinorBiomeRadius => _generator.V3AverageMinorBiomeRadius;
    public int V3BiomeForestLandCount => _generator.V3BiomeForestLandCount;
    public int V3BiomeRockyHillsCount => _generator.V3BiomeRockyHillsCount;
    public int V3BiomeDrylandCount => _generator.V3BiomeDrylandCount;
    public int V3BiomeWastelandCount => _generator.V3BiomeWastelandCount;
    public bool V3BiomeLayerEnabled => _generator.V3BiomeLayerEnabled;
    public string V3BiomeResolveMode => _generator.V3BiomeResolveMode;
    public string V3BiomeCellDistribution => _generator.V3BiomeCellDistribution;
    public bool WorldMapOverlayVisible { get; private set; }
    public Vector2I WorldMapTextureSize { get; private set; }
    public double WorldMapBuildMs { get; private set; }
    public bool WorldMapCached { get; private set; }
    public string WorldMapLastBuildReason { get; private set; } = "not built";
    public bool CompanyCoreInitialized => _companySession?.IsInitialized == true;
    public string LocalPlayerId => _companySession?.LocalContext.LocalPlayerId ?? string.Empty;
    public string LocalCompanyId => _companySession?.LocalContext.LocalCompanyId ?? string.Empty;
    public int RegisteredCompanyCount => _companySession?.CompanyRegistry.Count ?? 0;
    public bool LocalCompanyOwnershipValid => _companySession?.LocalContext.CanLocalPlayerControl(LocalCompanyId) == true;
    public string LocalCompanyName => TryGetLocalCompany(out CompanyStateV3? company)
        ? company!.DisplayName
        : string.Empty;
    public bool StartingDeploymentInitialized => _companySession?.StartingDeploymentResult?.IsInitialized == true;
    public int StartingSettlementId => _companySession?.StartingDeploymentResult?.StartingSettlementId ?? -1;
    public int RegisteredDeploymentCount => _companySession?.DeploymentRegistry.Count ?? 0;
    public int AssignedCompanyCount => _companySession?.StartingDeploymentResult?.AssignedCompanyCount ?? 0;
    public int UnassignedCompanyCount => _companySession?.StartingDeploymentResult?.UnassignedCompanyCount ?? RegisteredCompanyCount;
    public int DeploymentPlacementAttempts => _companySession?.StartingDeploymentResult?.PlacementAttempts ?? 0;
    public int DeploymentRejectedBounds => _companySession?.StartingDeploymentResult?.RejectedBounds ?? 0;
    public int DeploymentRejectedFeature => _companySession?.StartingDeploymentResult?.RejectedFeature ?? 0;
    public int DeploymentRejectedFormation => _companySession?.StartingDeploymentResult?.RejectedFormation ?? 0;
    public string DeploymentFailureReason => _companySession?.StartingDeploymentResult?.FailureReason ?? string.Empty;
    public bool MercenaryCoreInitialized => _mercenarySession?.IsInitialized == true;
    public int RegisteredMercenaryCount => _mercenarySession?.Registry.Count ?? 0;
    public int LocalCompanyMercenaryCount => _mercenarySession?.Registry.CountByCompany(LocalCompanyId) ?? 0;
    public int RuntimeMercenaryViewCount { get; private set; }
    public int DuplicateViewRejectedCount { get; private set; }
    public int MercenaryDeploymentMismatchCount { get; private set; }
    public bool InitialSquadCreationSucceeded => _mercenarySession?.LastInitialSquadCreationResult?.Succeeded == true;
    public bool InitialSquadCreationReusedExisting => _mercenarySession?.LastInitialSquadCreationResult?.ReusedExisting == true;
    public string InitialSquadCreationFailureReason => _mercenarySession?.LastInitialSquadCreationResult?.FailureReason ?? string.Empty;
    public int InitialSquadRollbackCount => _mercenarySession?.LastInitialSquadCreationResult?.RollbackCount ?? 0;
    public int DuplicateMercenaryRejectedCount => _mercenarySession?.Registry.DuplicateMercenaryRejectedCount ?? 0;
    public bool MercenaryControlInitialized => _controlSession != null;
    public int SelectedMercenaryCount => _controlSession?.Selection.Count ?? 0;
    public int ActiveMoveOrderCount => _controlSession?.Commands.ActiveMoveOrderCount ?? 0;
    public int ActiveCommandCount => _controlSession?.Commands.ActiveCommandCount ?? 0;
    public int MovingMercenaryCount => _controlSession?.Movements.Count ?? 0;
    public bool ResourceCoreInitialized => _resourceSession?.InitialPatchResult?.Succeeded == true;
    public bool WorkCoreInitialized => _workSession != null;
    public int ResourceNodeCount => _resourceSession?.Nodes.Count ?? 0;
    public int TreeNodeCount => _resourceSession?.Nodes.GetNodesByType(ResourceNodeTypeV3.Tree).Count ?? 0;
    public int StoneNodeCount => _resourceSession?.Nodes.GetNodesByType(ResourceNodeTypeV3.StoneOutcrop).Count ?? 0;
    public int DepletedResourceNodeCount { get { int count=0;if(_resourceSession!=null)foreach(string id in _resourceSession.Nodes.GetAllNodeIds())if(_resourceSession.Nodes.TryGet(id,out ResourceNodeStateV3? node)&&node?.IsDepleted==true)count++;return count; } }
    public int GroundStackCount => _resourceSession?.GroundStacks.Count ?? 0;
    public int WoodAmountOnGround => _resourceSession?.GroundStacks.GetTotalAmount(ResourceTypeV3.Wood) ?? 0;
    public int StoneAmountOnGround => _resourceSession?.GroundStacks.GetTotalAmount(ResourceTypeV3.Stone) ?? 0;
    public int ActiveWorkRequestCount => _workSession?.ActiveWorkRequestCount ?? 0;
    public int ActiveWorkAssignmentCount => _workSession?.ActiveAssignmentCount ?? 0;
    public int ActiveWorkReservationCount => _workSession?.ActiveReservationCount ?? 0;
    public int RuntimeResourceNodeViewCount{get;private set;}
    public int RuntimeGroundStackViewCount{get;private set;}
    public int MovingToWorkCount { get { int count=0;if(_workSession!=null)foreach(MercenaryWorkExecutionStateV3 execution in _workSession.GetActiveExecutions())if(execution.Phase is WorkExecutionPhaseV3.WaitingForPath or WorkExecutionPhaseV3.MovingToApproach)count++;return count; } }
    public int WorkingMercenaryCount { get { int count=0;if(_workSession!=null)foreach(MercenaryWorkExecutionStateV3 execution in _workSession.GetActiveExecutions())if(execution.Phase==WorkExecutionPhaseV3.Working)count++;return count; } }
    public int StockpileZoneCount=>_stockpileSession?.Zones.Count??0;public int StockpileCellCount=>_stockpileSession?.Zones.CellCount??0;public int LocalCompanyZoneCount=>_stockpileSession?.Zones.GetZonesByCompany(LocalCompanyId).Count??0;public string StockpileDesignationMode=>_stockpileSession?.Diagnostics.DesignationMode.ToString()??"None";public int ReservedStockpileCellCount=>_stockpileSession?.CellReservations.Count??0;
    public int ConstructionBlueprintCount=>_constructionSession?.Blueprints.Count??0;public int ConstructionStructureCount=>_constructionSession?.Structures.Count??0;public int ConstructionBlockingCellCount=>_constructionSession?.Structures.MovementBlockingCellCount??0;public int ConstructionReservationCount=>_constructionSession?.Reservations.Count??0;public long ConstructionOccupancyRevision=>_constructionSession?.Structures.OccupancyRevision??0;
    public int DemolitionDesignationCount=>_constructionSession?.Demolitions.Count??0;public int UnderDemolitionCount=>_constructionSession?.Demolitions.GetAllStructureIds().Count(id=>_constructionSession.Demolitions.TryGet(id,out var state)&&state?.Status==StructureDemolitionStatusV3.UnderDemolition)??0;public int DemolitionReservationCount=>_constructionSession?.DemolitionReservations.Count??0;public int CompletedDemolitionCount=>_constructionSession?.DemolitionDiagnostics.CompletedCount??0;public int FailedDemolitionCount=>_constructionSession?.DemolitionDiagnostics.FailedCount??0;public string LastDemolitionFailureReason=>_constructionSession?.DemolitionDiagnostics.LastFailureReason??string.Empty;public string LastDemolishedStructureId=>_constructionSession?.DemolitionDiagnostics.LastDemolishedStructureId??string.Empty;public string LastDemolitionWorkerId=>_constructionSession?.DemolitionDiagnostics.LastWorkerId??string.Empty;public float LastDemolitionDuration=>_constructionSession?.DemolitionDiagnostics.LastDuration??0;public int LastSalvageTotalAmount=>_constructionSession?.DemolitionDiagnostics.LastSalvageTotalAmount??0;
    public bool ConstructionTrayOpen { get; private set; }
    public string ActiveConstructionTool { get; private set; } = "-";
    public bool ConstructionUiInputBlockedByWorldMap { get; private set; }
    public string LastConstructionUiAction { get; private set; } = string.Empty;
    public void SetConstructionUiState(bool trayOpen,string activeTool,bool blockedByWorldMap,string lastAction){ConstructionTrayOpen=trayOpen;ActiveConstructionTool=activeTool;ConstructionUiInputBlockedByWorldMap=blockedByWorldMap;LastConstructionUiAction=lastAction;}
    public int ReservedSourceStackCount=>_workSession?.SourceStackReservations.Count??0;public int CarryingMercenaryCount=>_workSession?.Carries.Count??0;public int ActiveHaulingRequestCount=>_workSession?.ActiveHaulingRequestCount??0;
    public int WoodAmountInStockpile=>GetStockpileAmount(ResourceTypeV3.Wood);public int StoneAmountInStockpile=>GetStockpileAmount(ResourceTypeV3.Stone);public int GroundAmountOutsideStockpile=>GetOutsideStockpileAmount();

    private readonly ProceduralWorldGeneratorV2 _generator = new();
    private readonly Dictionary<Vector2I, SectorMetadata> _metadataBySector = new();
    private readonly Dictionary<Vector2I, SectorRuntimeState> _runtimeStatesBySector = new();
    private WorldV2GridRenderer? _gridRenderer;
    private WorldV2BuildManager? _buildManager;
    private WorldStreamManagerV2? _streamManager;
    private WorldV2DebugHud? _debugHud;
    private WorldGenerationSettingsV2? _generationSettings;
    private CompanySessionV3? _companySession;
    private MercenarySessionV3? _mercenarySession;
    private MercenaryControlSessionV3? _controlSession;
    private ResourceSessionV3? _resourceSession;
    private MercenaryWorkSessionV3? _workSession;
    private StockpileSessionV3? _stockpileSession;
    private ConstructionSessionV3? _constructionSession;
    private readonly StartingDeploymentCoordinatorV3 _startingDeploymentCoordinator = new();
    private readonly HashSet<string> _materializedMercenaryIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _materializedResourceNodeIds=new(StringComparer.Ordinal);
    private readonly HashSet<string> _materializedGroundStackIds=new(StringComparer.Ordinal);
    private Texture2D? _worldMapTexture;
    private string _worldMapCacheKey = string.Empty;

    public override void _Ready()
    {
        ResolveReferences();
        LoadGenerationRequest();
        InitializeCompanyCore();
        InitializeWorld();
        InitializeStartingDeployment();
        InitializeMercenaryCore();
        InitializeMercenaryControlCore();
        InitializeResourceAndWorkCore();
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

    public bool TryGetV3StartingSettlement(out VillageSiteV2? startingSettlement)
    {
        foreach (VillageSiteV2 village in GetV3MapVillages())
        {
            if (village.IsStartingVillage)
            {
                startingSettlement = village;
                return true;
            }
        }

        startingSettlement = null;
        return false;
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

    public IReadOnlyList<DungeonEntranceSiteV3> GetV3MapDungeonEntrances()
    {
        return _generator.GetV3DungeonEntrances();
    }

    public IReadOnlyList<BanditCampSiteV3> GetV3MapBanditCamps()
    {
        return _generator.GetV3BanditCamps();
    }

    public IReadOnlyList<FactionOutpostSiteV3> GetV3MapFactionOutposts()
    {
        return _generator.GetV3FactionOutposts();
    }

    public IReadOnlyList<BiomeRegionV3> GetV3MapBiomeRegions()
    {
        return _generator.GetV3BiomeRegions();
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
        GD.Print("  Ctrl+Shift+0: toggle biomes, Ctrl+Shift+1..8: toggle generation layers, Ctrl+Shift+9: toggle rivers");
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
        PrintCompanyCoreSummary();
        PrintStartingDeploymentSummary();
        PrintMercenaryCoreSummary(detailed: false);
        PrintMercenaryControlSummary(detailed: false);
        PrintResourceWorkSummary(detailed: false);
        GD.Print($"WorldV2 world: map={MapSizePreset} size={WorldMapSize.WidthCells}x{WorldMapSize.HeightCells} chunks={WorldMapSize.ChunkWidth}x{WorldMapSize.ChunkHeight} plan={PlanVersion} generated={GeneratedPlanType} seed={WorldSeed} bounds={WorldBounds.Position}..{WorldBounds.End - Vector2I.One}");
        GD.Print(V3VillageDebugSummary);
        GD.Print($"V3 biomes: enabled={V3BiomeLayerEnabled} mode={V3BiomeResolveMode} regions={V3BiomeRegionCount} major={V3MajorBiomeRegionCount} minor={V3MinorBiomeRegionCount} avgMajorRadius={V3AverageMajorBiomeRadius:0} avgMinorRadius={V3AverageMinorBiomeRadius:0} forestLand={V3BiomeForestLandCount} rocky={V3BiomeRockyHillsCount} dry={V3BiomeDrylandCount} wasteland={V3BiomeWastelandCount}");
        GD.Print($"V3 roads: enabled={V3RoadLayerEnabled} total={V3RoadCount} primary={V3PrimaryRoadCount} secondary={V3SecondaryRoadCount} extra={V3ExtraRoadCount} branch={V3BranchRoadCount} nodes={V3RoadNodeCount} junctions={V3RoadJunctionCount} maxDegree={V3MaxRoadJunctionDegree} trunks={V3SharedTrunkCount} merged={V3MergedRoadCandidateCount} rejectedJunctions={V3RejectedRoadJunctionCount} rejectedHighDegree={V3RejectedHighDegreeJunctionCount} rejectedCrossings={V3RejectedRoadCrossingCount} rejectedTooLong={V3RejectedRoadTooLongCount} targets={V3RoadTargetAnchorCount} quarryTargets={V3RoadTargetQuarryCount} ruinTargets={V3RoadTargetRuinCount} dungeonTargets={V3RoadTargetDungeonEntranceCount} banditTargets={V3RoadTargetBanditCampCount} factionTargets={V3RoadTargetFactionOutpostCount} forestTargets={V3RoadTargetForestEdgeCount} edgeTargets={V3RoadTargetWorldEdgeExitCount} futureTargets={V3FutureRoadTargetCount} rejectedTargets={V3RejectedRoadTargetCount} rejectedBranches={V3RejectedBranchRoadCount}");
        GD.Print($"V3 biomes: sampleP/F/R/D/W={V3BiomeCellDistribution} quotaFallback={V3BiomeQuotaFallbackCount}");
        GD.Print($"V3 forests: enabled={V3ForestLayerEnabled} regions={V3ForestRegionCount} major={V3MajorForestRegionCount} minor={V3MinorForestPatchCount} bonus={V3ForestTotalBonusApplied} rejected={V3RejectedForestPlacementCount} biomeDist={V3ForestBiomeDistribution} majorDist={V3MajorForestBiomeDistribution} minorDist={V3MinorForestBiomeDistribution}");
        GD.Print($"V3 quarries: enabled={V3QuarryLayerEnabled} regions={V3QuarryRegionCount} major={V3MajorQuarryCount} minor={V3MinorQuarryCount} rejected={V3RejectedQuarryPlacementCount} biomeDist={V3QuarryBiomeDistribution}");
        GD.Print($"V3 ruins: enabled={V3RuinLayerEnabled} sites={V3RuinSiteCount} roadLinked={V3RoadLinkedRuinCount} rejected={V3RejectedRuinPlacementCount} biomeDist={V3RuinBiomeDistribution}");
        GD.Print($"V3 dungeons: enabled={V3DungeonLayerEnabled} entrances={V3DungeonEntranceCount} roadLinked={V3RoadLinkedDungeonEntranceCount} rejected={V3RejectedDungeonEntrancePlacementCount} kinds={V3DungeonEntranceKindDistribution} biomeDist={V3DungeonEntranceBiomeDistribution}");
        GD.Print($"V3 bandits: enabled={V3BanditLayerEnabled} camps={V3BanditCampCount} roadLinked={V3RoadLinkedBanditCampCount} rejected={V3RejectedBanditCampPlacementCount} kinds={V3BanditCampKindDistribution} biomeDist={V3BanditCampBiomeDistribution}");
        GD.Print($"V3 faction outposts: enabled={V3FactionOutpostLayerEnabled} outposts={V3FactionOutpostCount} roadLinked={V3RoadLinkedFactionOutpostCount} rejected={V3RejectedFactionOutpostPlacementCount} kinds={V3FactionOutpostKindDistribution} owners={V3FactionOutpostOwnerDistribution} biomeDist={V3FactionOutpostBiomeDistribution}");
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
        PrintCompanyCoreSummary();
        PrintStartingDeploymentSummary();
        PrintMercenaryCoreSummary(detailed: true);
        PrintMercenaryControlSummary(detailed: true);
        PrintResourceWorkSummary(detailed: true);
        GD.Print($"WorldV2 world: map={MapSizePreset} size={WorldMapSize.WidthCells}x{WorldMapSize.HeightCells} plan={PlanVersion} generated={GeneratedPlanType} seed={WorldSeed}");
        GD.Print(V3VillageDebugSummary);
        GD.Print($"V3 biomes: enabled={V3BiomeLayerEnabled} mode={V3BiomeResolveMode} regions={V3BiomeRegionCount} major={V3MajorBiomeRegionCount} minor={V3MinorBiomeRegionCount} avgMajorRadius={V3AverageMajorBiomeRadius:0} avgMinorRadius={V3AverageMinorBiomeRadius:0} forestLand={V3BiomeForestLandCount} rocky={V3BiomeRockyHillsCount} dry={V3BiomeDrylandCount} wasteland={V3BiomeWastelandCount}");
        GD.Print($"V3 roads: enabled={V3RoadLayerEnabled} total={V3RoadCount} primary={V3PrimaryRoadCount} secondary={V3SecondaryRoadCount} extra={V3ExtraRoadCount} branch={V3BranchRoadCount} nodes={V3RoadNodeCount} junctions={V3RoadJunctionCount} maxDegree={V3MaxRoadJunctionDegree} trunks={V3SharedTrunkCount} merged={V3MergedRoadCandidateCount} rejectedJunctions={V3RejectedRoadJunctionCount} rejectedHighDegree={V3RejectedHighDegreeJunctionCount} rejectedCrossings={V3RejectedRoadCrossingCount} rejectedTooLong={V3RejectedRoadTooLongCount} targets={V3RoadTargetAnchorCount} quarryTargets={V3RoadTargetQuarryCount} ruinTargets={V3RoadTargetRuinCount} dungeonTargets={V3RoadTargetDungeonEntranceCount} banditTargets={V3RoadTargetBanditCampCount} factionTargets={V3RoadTargetFactionOutpostCount} forestTargets={V3RoadTargetForestEdgeCount} edgeTargets={V3RoadTargetWorldEdgeExitCount} futureTargets={V3FutureRoadTargetCount} rejectedTargets={V3RejectedRoadTargetCount} rejectedBranches={V3RejectedBranchRoadCount}");
        GD.Print($"V3 biomes: sampleP/F/R/D/W={V3BiomeCellDistribution} quotaFallback={V3BiomeQuotaFallbackCount}");
        GD.Print($"V3 forests: enabled={V3ForestLayerEnabled} regions={V3ForestRegionCount} major={V3MajorForestRegionCount} minor={V3MinorForestPatchCount} bonus={V3ForestTotalBonusApplied} rejected={V3RejectedForestPlacementCount} biomeDist={V3ForestBiomeDistribution} majorDist={V3MajorForestBiomeDistribution} minorDist={V3MinorForestBiomeDistribution}");
        GD.Print($"V3 quarries: enabled={V3QuarryLayerEnabled} regions={V3QuarryRegionCount} major={V3MajorQuarryCount} minor={V3MinorQuarryCount} rejected={V3RejectedQuarryPlacementCount} biomeDist={V3QuarryBiomeDistribution}");
        GD.Print($"V3 ruins: enabled={V3RuinLayerEnabled} sites={V3RuinSiteCount} roadLinked={V3RoadLinkedRuinCount} rejected={V3RejectedRuinPlacementCount} biomeDist={V3RuinBiomeDistribution}");
        GD.Print($"V3 dungeons: enabled={V3DungeonLayerEnabled} entrances={V3DungeonEntranceCount} roadLinked={V3RoadLinkedDungeonEntranceCount} rejected={V3RejectedDungeonEntrancePlacementCount} kinds={V3DungeonEntranceKindDistribution} biomeDist={V3DungeonEntranceBiomeDistribution}");
        GD.Print($"V3 bandits: enabled={V3BanditLayerEnabled} camps={V3BanditCampCount} roadLinked={V3RoadLinkedBanditCampCount} rejected={V3RejectedBanditCampPlacementCount} kinds={V3BanditCampKindDistribution} biomeDist={V3BanditCampBiomeDistribution}");
        GD.Print($"V3 faction outposts: enabled={V3FactionOutpostLayerEnabled} outposts={V3FactionOutpostCount} roadLinked={V3RoadLinkedFactionOutpostCount} rejected={V3RejectedFactionOutpostPlacementCount} kinds={V3FactionOutpostKindDistribution} owners={V3FactionOutpostOwnerDistribution} biomeDist={V3FactionOutpostBiomeDistribution}");
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

    public bool TryGetLocalCompany(out CompanyStateV3? company)
    {
        company = null;
        return _companySession?.LocalContext.TryGetLocalCompany(out company, out _) == true;
    }

    public bool CanPlayerControlCompany(string playerId, string companyId)
    {
        return _companySession?.CanPlayerControlCompany(playerId, companyId) == true;
    }

    public bool TryGetLocalDeployment(out CompanyDeploymentStateV3? deployment)
    {
        deployment = null;
        return _companySession?.DeploymentRegistry.TryGetDeployment(LocalCompanyId, out deployment) == true;
    }

    public bool TryGetStartingDeploymentResult(out StartingDeploymentPlacementResultV3? result)
    {
        result = _companySession?.StartingDeploymentResult;
        return result != null;
    }

    public IReadOnlyList<CompanyDeploymentStateV3> GetStartingDeployments()
    {
        return _companySession?.DeploymentRegistry.GetAllDeployments()
            ?? System.Array.Empty<CompanyDeploymentStateV3>();
    }

    public bool TryGetMercenarySession(out MercenarySessionV3? mercenarySession)
    {
        mercenarySession = _mercenarySession;
        return mercenarySession != null;
    }

    public bool TryGetMercenaryControlSession(out MercenaryControlSessionV3? controlSession)
    {
        controlSession = _controlSession;
        return controlSession != null;
    }

    public bool TryGetResourceSession(out ResourceSessionV3? resourceSession)
    {
        resourceSession = _resourceSession;
        return resourceSession != null;
    }

    public bool TryGetMercenaryWorkSession(out MercenaryWorkSessionV3? workSession)
    {
        workSession = _workSession;
        return workSession != null;
    }
    public bool TryGetStockpileSession(out StockpileSessionV3? stockpileSession){stockpileSession=_stockpileSession;return stockpileSession!=null;}
    public bool TryGetConstructionSession(out ConstructionSessionV3? constructionSession){constructionSession=_constructionSession;return constructionSession!=null;}

    public bool CanPlayerControlMercenary(string playerId, string mercenaryId)
    {
        return _mercenarySession?.CanPlayerControlMercenary(playerId, mercenaryId) == true;
    }

    public void SetResourceRuntimeDiagnostics(IReadOnlyList<string> nodeIds,IReadOnlyList<string> stackIds)
    {_materializedResourceNodeIds.Clear();foreach(string id in nodeIds)_materializedResourceNodeIds.Add(id);_materializedGroundStackIds.Clear();foreach(string id in stackIds)_materializedGroundStackIds.Add(id);RuntimeResourceNodeViewCount=_materializedResourceNodeIds.Count;RuntimeGroundStackViewCount=_materializedGroundStackIds.Count;}
    public bool IsResourceNodeViewMaterialized(string id)=>_materializedResourceNodeIds.Contains(id);
    public bool IsGroundStackViewMaterialized(string id)=>_materializedGroundStackIds.Contains(id);
    private int GetStockpileAmount(ResourceTypeV3 type){int total=0;if(_resourceSession==null||_stockpileSession==null)return 0;foreach(string id in _resourceSession.GroundStacks.GetAllStackIds())if(_resourceSession.GroundStacks.TryGet(id,out GroundResourceStackV3? stack)&&stack!=null&&stack.ResourceType==type&&_stockpileSession.Zones.IsOwnedStockpileCell(LocalCompanyId,stack.Cell))total+=stack.Amount;return total;}
    private int GetOutsideStockpileAmount(){int total=0;if(_resourceSession==null||_stockpileSession==null)return 0;foreach(string id in _resourceSession.GroundStacks.GetAllStackIds())if(_resourceSession.GroundStacks.TryGet(id,out GroundResourceStackV3? stack)&&stack!=null&&!_stockpileSession.Zones.IsOwnedStockpileCell(LocalCompanyId,stack.Cell))total+=stack.Amount;return total;}

    public bool IsMercenaryViewMaterialized(string mercenaryId)
    {
        return _materializedMercenaryIds.Contains(mercenaryId);
    }

    public void SetMercenaryRuntimeDiagnostics(
        IReadOnlyList<string> materializedMercenaryIds,
        int runtimeViewCount,
        int duplicateViewRejectedCount,
        int deploymentMismatchCount)
    {
        _materializedMercenaryIds.Clear();
        foreach (string mercenaryId in materializedMercenaryIds)
        {
            _materializedMercenaryIds.Add(mercenaryId);
        }

        RuntimeMercenaryViewCount = runtimeViewCount;
        DuplicateViewRejectedCount = duplicateViewRejectedCount;
        MercenaryDeploymentMismatchCount = deploymentMismatchCount;
        UpdateDebugHud();
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

    public FlatlandCellSampleV2 SampleV3PlanCellForNavigation(Vector2I globalCellCoord)
    {
        return _generator.SampleFlatland(WorldSeed, globalCellCoord);
    }

    public WorldGenerationSettingsV2 GetGenerationSettings()
    {
        ResolveReferences();
        return _generationSettings ?? WorldGenerationSettingsV2.Default;
    }

    private string BuildWorldMapCacheKey()
    {
        WorldGenerationSettingsV2 settings = GetGenerationSettings();
        return $"{WorldId}:{WorldSeed}:{MapSizePreset}:{PlanVersion}:{WorldMapSize.WidthCells}x{WorldMapSize.HeightCells}:"
            + $"{V3VillageCount}:{V3HamletCount}:{V3VillageTierCount}:{V3LargeVillageCount}:{V3TownCount}:{V3CityCandidateCount}:{V3SettlementRoleDistribution}:"
            + $"{V3RoadCount}:{V3BiomeRegionCount}:{V3AverageMajorBiomeRadius:0}:{V3AverageMinorBiomeRadius:0}:{V3ForestRegionCount}:{V3QuarryRegionCount}:{V3RuinSiteCount}:{V3DungeonEntranceCount}:{V3BanditCampCount}:{V3FactionOutpostCount}:"
            + $"{V3BiomeFeatureDistributionEnabled}:{V3BiomeQuotaFallbackCount}:{V3BiomeCellDistribution}:{V3ForestBiomeDistribution}:{V3MajorForestBiomeDistribution}:{V3MinorForestBiomeDistribution}:{V3QuarryBiomeDistribution}:{V3RuinBiomeDistribution}:{V3DungeonEntranceKindDistribution}:{V3DungeonEntranceBiomeDistribution}:{V3BanditCampKindDistribution}:{V3BanditCampBiomeDistribution}:{V3FactionOutpostKindDistribution}:{V3FactionOutpostOwnerDistribution}:{V3FactionOutpostBiomeDistribution}:"
            + $"{settings.V3EnableBiomeFeatureDistribution}:{settings.V3BiomeQuotaFallbackEnabled}:{settings.V3BiomePlacementMaxAttempts}:{settings.V3SmallForestBonusRatio}:{settings.V3MediumForestBonusRatio}:{settings.V3LargeForestBonusRatio}:{settings.V3HugeForestBonusRatio}:"
            + $"{settings.V3PlainsForestDistributionWeight}:{settings.V3ForestLandForestDistributionWeight}:{settings.V3RockyHillsForestDistributionWeight}:{settings.V3DrylandForestDistributionWeight}:{settings.V3WastelandForestDistributionWeight}:"
            + $"{settings.V3PlainsQuarryDistributionWeight}:{settings.V3ForestLandQuarryDistributionWeight}:{settings.V3RockyHillsQuarryDistributionWeight}:{settings.V3DrylandQuarryDistributionWeight}:{settings.V3WastelandQuarryDistributionWeight}:"
            + $"{settings.V3PlainsRuinDistributionWeight}:{settings.V3ForestLandRuinDistributionWeight}:{settings.V3RockyHillsRuinDistributionWeight}:{settings.V3DrylandRuinDistributionWeight}:{settings.V3WastelandRuinDistributionWeight}:"
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

    private void InitializeCompanyCore()
    {
        if (PlanVersion != WorldPlanVersionV2.V3)
        {
            return;
        }

        if (!GameplaySessionV3.EnsureCompanyCoreInitialized(
                out CompanySessionV3 companySession,
                out bool createdNow,
                out string reason))
        {
            GD.PushError($"[CompanyCoreV3] Initialization failed: {reason}");
            return;
        }

        _companySession = companySession;
        if (!createdNow)
        {
            return;
        }

        PlayerIdentityV3 localPlayer = companySession.LocalPlayer!;
        companySession.LocalContext.TryGetLocalCompany(out CompanyStateV3? localCompany, out _);
        GD.Print($"[CompanyCoreV3]\nLocal player created:\nPlayerId={localPlayer.PlayerId}");
        GD.Print($"[CompanyCoreV3]\nLocal company created:\nCompanyId={localCompany?.CompanyId}\nOwnerPlayerId={localCompany?.OwnerPlayerId}\nName={localCompany?.DisplayName}");

#if DEBUG
        CompanyCoreSelfCheckResultV3 selfCheck = CompanyCoreSelfCheckV3.Run();
        if (selfCheck.Passed)
        {
            GD.Print($"[CompanyCoreV3] Self-check {selfCheck.Summary}");
        }
        else
        {
            GD.PushError($"[CompanyCoreV3] Self-check {selfCheck.Summary}");
        }
#endif
    }

    private void InitializeStartingDeployment()
    {
        if (PlanVersion != WorldPlanVersionV2.V3 || _companySession?.IsInitialized != true)
        {
            return;
        }

        if (!TryGetV3StartingSettlement(out VillageSiteV2? startingSettlement) || startingSettlement == null)
        {
            GD.PushError("[StartingDeploymentV3] StartingSettlement could not be resolved.");
            return;
        }

        StartingDeploymentWorldQueryV3 worldQuery = new(
            WorldSeed,
            WorldBounds,
            startingSettlement,
            GetV3MapRoads(),
            GetV3MapRuinSites(),
            GetV3MapQuarryRegions(),
            GetV3MapDungeonEntrances(),
            GetV3MapBanditCamps(),
            GetV3MapFactionOutposts(),
            SampleFlatlandAt);

        _startingDeploymentCoordinator.TryEnsureDeployments(_companySession, worldQuery, out StartingDeploymentPlacementResultV3 result);
        if (result.NewlyAssignedCompanyIds.Count > 0 && result.ArrivalAnchorCell.HasValue)
        {
            GD.Print(
                $"[StartingDeploymentV3]\nArrival anchor resolved:\n" +
                $"SettlementId={result.StartingSettlementId}\n" +
                $"Cell={result.ArrivalAnchorCell.Value}\n" +
                $"DistanceToRoad={result.DistanceToNearestRoad:0.0}");

            foreach (string companyId in result.NewlyAssignedCompanyIds)
            {
                if (!_companySession.DeploymentRegistry.TryGetDeployment(companyId, out CompanyDeploymentStateV3? deployment)
                    || deployment == null)
                {
                    continue;
                }

                GD.Print(
                    $"[StartingDeploymentV3]\nCompany deployed:\n" +
                    $"CompanyId={deployment.CompanyId}\n" +
                    $"Slot={deployment.DeploymentSlotIndex}\n" +
                    $"Anchor={deployment.DeploymentAnchorCell}\n" +
                    $"Formation={deployment.FormationCells[0]}, {deployment.FormationCells[1]}, {deployment.FormationCells[2]}\n" +
                    $"Attempts={deployment.PlacementAttempts}\n" +
                    $"DistanceToSettlement={deployment.DistanceToSettlementCenter:0.0}\n" +
                    $"DistanceToRoad={deployment.DistanceToNearestRoad:0.0}\n" +
                    $"DistanceToUnsafeFeatureCore={deployment.DistanceToNearestUnsafeFeatureCore:0.0}");
            }
        }

        if (!string.IsNullOrEmpty(result.FailureReason))
        {
            GD.PushWarning($"[StartingDeploymentV3] {result.FailureReason}");
        }

#if DEBUG
        if (result.NewlyAssignedCompanyIds.Count > 0)
        {
            StartingDeploymentSelfCheckResultV3 selfCheck = StartingDeploymentSelfCheckV3.Run();
            if (selfCheck.Passed)
            {
                GD.Print($"[StartingDeploymentV3] Self-check {selfCheck.Summary}");
            }
            else
            {
                GD.PushError($"[StartingDeploymentV3] Self-check {selfCheck.Summary}");
            }

            if (!StartingDeploymentSelfCheckV3.TryValidateRuntime(_companySession, worldQuery, result, out string runtimeReason))
            {
                GD.PushError($"[StartingDeploymentV3] Runtime self-check FAIL: {runtimeReason}");
            }
            else
            {
                GD.Print("[StartingDeploymentV3] Runtime self-check PASS");
            }
        }
#endif

        UpdateDebugHud();
    }

    private void InitializeMercenaryCore()
    {
        if (PlanVersion != WorldPlanVersionV2.V3 || _companySession?.IsInitialized != true)
        {
            return;
        }

        if (!_companySession.LocalContext.TryGetLocalCompanyId(out string localCompanyId, out string contextReason))
        {
            GD.PushWarning($"[MercenaryCoreV3] {contextReason}");
            return;
        }

        if (!GameplaySessionV3.EnsureMercenarySession(_companySession, out MercenarySessionV3 mercenarySession, out string sessionReason))
        {
            GD.PushError($"[MercenaryCoreV3] {sessionReason}");
            return;
        }

        _mercenarySession = mercenarySession;
        InitialSquadCreationResultV3 result = mercenarySession.CreateOrReuseInitialSquad(
            localCompanyId,
            _companySession.DeploymentRegistry,
            WorldBounds);
        if (!result.Succeeded)
        {
            GD.PushWarning($"[MercenaryCoreV3] Initial squad creation failed: {result.FailureReason}");
            UpdateDebugHud();
            return;
        }

        if (!result.ReusedExisting)
        {
            GD.Print(
                $"[MercenaryCoreV3]\nInitial squad created:\n" +
                $"CompanyId={localCompanyId}\n" +
                $"MercenaryCount={result.MercenaryIds.Count}");
            foreach (string mercenaryId in result.MercenaryIds)
            {
                if (!mercenarySession.Registry.TryGetMercenary(mercenaryId, out MercenaryProfileV3? profile, out MercenaryStateV3? state)
                    || profile == null || state == null)
                {
                    continue;
                }

                IReadOnlyList<MercenaryWorkSkillValueV3> topSkills = profile.WorkSkills.GetTopSkills(2);
                GD.Print(
                    $"[MercenaryCoreV3]\nMercenary created:\n" +
                    $"MercenaryId={mercenaryId}\n" +
                    $"Name={profile.DisplayName}\n" +
                    $"CompanyId={state.CompanyId}\n" +
                    $"Cell={state.CurrentCell}\n" +
                    $"TopSkills={topSkills[0]}, {topSkills[1]}");
            }

#if DEBUG
            MercenaryCoreSelfCheckResultV3 selfCheck = MercenaryCoreSelfCheckV3.Run();
            if (selfCheck.Passed)
            {
                GD.Print($"[MercenaryCoreV3] Self-check {selfCheck.Summary}");
            }
            else
            {
                GD.PushError($"[MercenaryCoreV3] Self-check {selfCheck.Summary}");
            }
#endif
        }

        UpdateDebugHud();
    }

    private void InitializeMercenaryControlCore()
    {
        if (PlanVersion != WorldPlanVersionV2.V3 || _companySession?.IsInitialized != true || _mercenarySession?.IsInitialized != true)
        {
            return;
        }

        if (!GameplaySessionV3.EnsureControlSession(
                _companySession,
                _mercenarySession,
                out MercenaryControlSessionV3 controlSession,
                out string reason))
        {
            GD.PushError($"[MercenaryControlV3] {reason}");
            return;
        }

        _controlSession = controlSession;
#if DEBUG
        MercenaryControlSelfCheckResultV3 selfCheck = MercenaryControlSelfCheckV3.Run();
        if (selfCheck.Passed)
        {
            GD.Print($"[MercenaryControlV3] Self-check {selfCheck.Summary}");
        }
        else
        {
            GD.PushError($"[MercenaryControlV3] Self-check {selfCheck.Summary}");
        }
#endif
        UpdateDebugHud();
    }

    private void InitializeResourceAndWorkCore()
    {
        if (PlanVersion != WorldPlanVersionV2.V3 || _companySession == null || _mercenarySession == null || _controlSession == null
            || !TryGetLocalDeployment(out CompanyDeploymentStateV3? deployment) || deployment == null
            || !TryGetStartingDeploymentResult(out StartingDeploymentPlacementResultV3? placement) || placement == null)
        {
            return;
        }

        if (!GameplaySessionV3.EnsureResourceAndWorkSessions(_companySession,_mercenarySession,_controlSession,out ResourceSessionV3 resources,out MercenaryWorkSessionV3 work,out string reason))
        {
            GD.PushError($"[WorkCoreV3] {reason}");
            return;
        }

        _resourceSession=resources;_workSession=work;_stockpileSession=work.Stockpiles;GameplaySessionV3.TryGetConstructionSession(out _constructionSession);
        InitialGatheringPatchResultV3 result=InitialGatheringPatchPlacementServiceV3.Place(resources,deployment,placement,WorldBounds,SampleV3PlanCellForNavigation);
        if(!result.Succeeded){GD.PushError($"[ResourceCoreV3] Initial patch failed: {result.FailureReason}");return;}
        if(!result.ReusedExisting)
        {
            GD.Print($"[ResourceCoreV3] Initial gathering patch created nodes={result.NodeIds.Count} candidates={result.CandidatesChecked}");
            foreach(string id in result.NodeIds)if(resources.Nodes.TryGet(id,out ResourceNodeStateV3? node)&&node!=null)GD.Print($"[ResourceCoreV3] node={id} type={node.NodeType} cell={node.Cell} amount={node.RemainingAmount}");
        }
#if DEBUG
        WorkResourceSelfCheckResultV3 selfCheck=WorkResourceSelfCheckV3.Run();
        if(selfCheck.Passed)GD.Print($"[WorkCoreV3] Self-check {selfCheck.Summary}");else GD.PushError($"[WorkCoreV3] Self-check {selfCheck.Summary}");
        StockpileHaulingSelfCheckResultV3 stockpileCheck=StockpileHaulingSelfCheckV3.Run();
        if(stockpileCheck.Passed)GD.Print($"[StockpileV3] Self-check {stockpileCheck.Summary}");else GD.PushError($"[StockpileV3] Self-check {stockpileCheck.Summary}");
        ConstructionSelfCheckResultV3 constructionCheck=ConstructionSelfCheckV3.Run();
        if(constructionCheck.Succeeded)GD.Print($"[ConstructionV3] Self-check {constructionCheck.Summary}");else GD.PushError($"[ConstructionV3] Self-check {constructionCheck.Summary}");
        ConstructionRuntimeSelfCheckResultV3 constructionRuntimeCheck=ConstructionRuntimeSelfCheckV3.Run();
        if(constructionRuntimeCheck.Passed)GD.Print($"[ConstructionV3] Runtime self-check {constructionRuntimeCheck.Summary} singleTrips={constructionRuntimeCheck.SingleStackTrips} splitTrips={constructionRuntimeCheck.SplitStackTrips}");else GD.PushError($"[ConstructionV3] Runtime self-check {constructionRuntimeCheck.Summary}");
        DemolitionSelfCheckResultV3 demolitionCheck=DemolitionSelfCheckV3.Run();
        if(demolitionCheck.Passed)GD.Print($"[DemolitionV3] Self-check {demolitionCheck.Summary} scores={demolitionCheck.RecruitAScore:0.00}/{demolitionCheck.RecruitBScore:0.00}/{demolitionCheck.RecruitCScore:0.00} salvage={demolitionCheck.SalvagedWood}");else GD.PushError($"[DemolitionV3] Self-check {demolitionCheck.Summary}");
#endif
        UpdateDebugHud();
    }

    private void PrintCompanyCoreSummary()
    {
        GD.Print(
            $"[CompanyCoreV3] CompanyCoreInitialized={CompanyCoreInitialized} " +
            $"LocalPlayerId={LocalPlayerId} LocalCompanyId={LocalCompanyId} " +
            $"LocalCompanyName={LocalCompanyName} RegisteredCompanyCount={RegisteredCompanyCount} " +
            $"LocalCompanyOwnershipValid={LocalCompanyOwnershipValid}");
    }

    private void PrintStartingDeploymentSummary()
    {
        if (!TryGetStartingDeploymentResult(out StartingDeploymentPlacementResultV3? result) || result == null)
        {
            GD.Print("[StartingDeploymentV3] StartingDeploymentInitialized=false");
            return;
        }

        string arrivalAnchor = result.ArrivalAnchorCell?.ToString() ?? "-";
        string localAnchor = TryGetLocalDeployment(out CompanyDeploymentStateV3? localDeployment)
            ? localDeployment!.DeploymentAnchorCell.ToString()
            : "-";
        string localFormation = localDeployment?.FormationCells.Count == 3
            ? $"{localDeployment.FormationCells[0]}/{localDeployment.FormationCells[1]}/{localDeployment.FormationCells[2]}"
            : "-";
        GD.Print(
            $"[StartingDeploymentV3] StartingDeploymentInitialized={result.IsInitialized} " +
            $"StartingSettlementId={result.StartingSettlementId} SettlementArrivalAnchorCell={arrivalAnchor} " +
            $"LocalCompanyHasDeployment={localDeployment != null} LocalCompanyDeploymentSlot={localDeployment?.DeploymentSlotIndex} " +
            $"LocalCompanyDeploymentAnchorCell={localAnchor} LocalCompanyFormationCells={localFormation} RegisteredDeploymentCount={RegisteredDeploymentCount} " +
            $"AssignedCompanyCount={result.AssignedCompanyCount} UnassignedCompanyCount={result.UnassignedCompanyCount} " +
            $"DeploymentPlacementAttempts={result.PlacementAttempts} DeploymentRejectedBounds={result.RejectedBounds} " +
            $"DeploymentRejectedFeature={result.RejectedFeature} DeploymentRejectedFormation={result.RejectedFormation} " +
            $"DeploymentFailureReason={result.FailureReason} " +
            $"DistanceFromDeploymentToSettlementCenter={(localDeployment?.DistanceToSettlementCenter ?? -1.0f):0.0} " +
            $"DistanceFromDeploymentToNearestRoad={(localDeployment?.DistanceToNearestRoad ?? -1.0f):0.0} " +
            $"DistanceFromDeploymentToNearestUnsafeFeatureCore={(localDeployment?.DistanceToNearestUnsafeFeatureCore ?? -1.0f):0.0}");
    }

    private void PrintMercenaryCoreSummary(bool detailed)
    {
        GD.Print(
            $"[MercenaryCoreV3] MercenaryCoreInitialized={MercenaryCoreInitialized} " +
            $"RegisteredMercenaryCount={RegisteredMercenaryCount} LocalCompanyMercenaryCount={LocalCompanyMercenaryCount} " +
            $"RuntimeMercenaryViewCount={RuntimeMercenaryViewCount} InitialSquadCreationSucceeded={InitialSquadCreationSucceeded} " +
            $"InitialSquadCreationReusedExisting={InitialSquadCreationReusedExisting} " +
            $"InitialSquadCreationFailureReason={InitialSquadCreationFailureReason} InitialSquadRollbackCount={InitialSquadRollbackCount} " +
            $"DuplicateMercenaryRejectedCount={DuplicateMercenaryRejectedCount} DuplicateViewRejectedCount={DuplicateViewRejectedCount} " +
            $"MercenaryDeploymentMismatchCount={MercenaryDeploymentMismatchCount}");

        if (_mercenarySession == null)
        {
            return;
        }

        foreach (string mercenaryId in _mercenarySession.Registry.GetMercenariesByCompany(LocalCompanyId))
        {
            if (!_mercenarySession.Registry.TryGetMercenary(mercenaryId, out MercenaryProfileV3? profile, out MercenaryStateV3? state)
                || profile == null || state == null)
            {
                continue;
            }

            MercenaryDerivedStatsV3 derived = MercenaryDerivedStatsCalculatorV3.Calculate(profile);
            IReadOnlyList<MercenaryWorkSkillValueV3> topSkills = profile.WorkSkills.GetTopSkills(2);
            string initialSlot = profile.InitialSquadSlotIndex?.ToString() ?? "-";
            GD.Print(
                $"[MercenaryCoreV3] {profile.DisplayName} id={ShortMercenaryId(mercenaryId)} initialSlot={initialSlot} " +
                $"cell={state.CurrentCell} activity={state.ActivityState} top={topSkills[0]}/{topSkills[1]} " +
                $"moveMul={derived.MoveSpeedMultiplier:0.00} carry={derived.CarryCapacity:0.0} view={IsMercenaryViewMaterialized(mercenaryId)}");
            if (detailed)
            {
                GD.Print(
                    $"  attributes S/A/E/I/M={profile.Attributes.Strength}/{profile.Attributes.Agility}/{profile.Attributes.Endurance}/{profile.Attributes.Intelligence}/{profile.Attributes.Mental} " +
                    $"skills H/C/G/F/P/M/Gd={profile.WorkSkills.Hauling}/{profile.WorkSkills.Construction}/{profile.WorkSkills.Gathering}/{profile.WorkSkills.Farming}/{profile.WorkSkills.Production}/{profile.WorkSkills.Medicine}/{profile.WorkSkills.Guarding} " +
                    $"workMul={derived.WorkSpeedMultiplier:0.00}");
            }
        }
    }

    private void PrintMercenaryControlSummary(bool detailed)
    {
        if (_controlSession == null)
        {
            GD.Print("[MercenaryControlV3] MercenaryControlInitialized=false");
            return;
        }

        MercenaryControlDiagnosticsV3 diagnostics = _controlSession.Diagnostics;
        GD.Print(
            $"[MercenaryControlV3] MercenaryControlInitialized=true " +
            $"SessionRevision={_controlSession.SessionRevision} Selected={_controlSession.Selection.Count} " +
            $"SelectionRevision={_controlSession.Selection.Revision} LastSelectionAction={_controlSession.Selection.LastSelectionAction} " +
            $"ActiveCommands={_controlSession.Commands.ActiveCommandCount} ActiveOrders={_controlSession.Commands.ActiveMoveOrderCount} " +
            $"Moving={_controlSession.Movements.Count} " +
            $"CompletedPaths={diagnostics.CompletedPathCount} FailedPaths={diagnostics.FailedPathCount} " +
            $"CompletedMoves={diagnostics.CompletedMovementCount} FailedMoves={diagnostics.FailedMovementCount} " +
            $"StalePathResults={diagnostics.StalePathResultDiscardCount} SearchLimitExceeded={diagnostics.SearchLimitExceededCount}");

        if (!detailed)
        {
            return;
        }

        GD.Print($"[MercenaryControlV3] SelectedIds={string.Join(',', _controlSession.Selection.GetSelectedIds())}");
        HashSet<string> printedCommands = new(StringComparer.Ordinal);
        foreach (MercenaryMoveOrderV3 order in _controlSession.Commands.GetActiveOrders())
        {
            if (printedCommands.Add(order.CommandId)
                && _controlSession.Commands.TryGetCommand(order.CommandId, out DirectMoveCommandV3? command)
                && command != null)
            {
                List<string> resolved = new();
                foreach (string mercenaryId in command.RequestedMercenaryIds)
                {
                    if (command.ResolvedDestinationCells.TryGetValue(mercenaryId, out GlobalCellCoord destination))
                    {
                        resolved.Add($"{ShortMercenaryId(mercenaryId)}:{destination}");
                    }
                }
                GD.Print(
                    $"[MercenaryControlV3] command={command.CommandId} revision={command.CommandRevision} status={command.Status} " +
                    $"issuer={command.IssuerPlayerId} company={command.CompanyId} requestedTarget={command.RequestedTargetCell} " +
                    $"requestedIds={string.Join(',', command.RequestedMercenaryIds)} destinations={string.Join(',', resolved)}");
            }
            string pathSummary = order.PathResult == null
                ? "path=-"
                : $"pathLength={order.PathResult.Path.Count} cost={order.PathResult.TotalCost:0.00} expanded={order.PathResult.ExpandedNodeCount} cpuMs={order.PathResult.SearchDurationMs:0.000}";
            string movementSummary = _controlSession.Movements.TryGet(order.MercenaryId, out MercenaryMovementStateV3? movement) && movement != null
                ? $"from={movement.FromCell} to={movement.ToCell} progress={movement.SegmentProgress01:0.00} duration={movement.SegmentDuration:0.000} " +
                  $"effectiveSpeed={3.0f * movement.MoveSpeedMultiplier:0.00} terrainMul={movement.EnteringTraversalMultiplier:0.00} remaining={movement.RemainingPathCells}"
                : "movement=-";
            GD.Print(
                $"[MercenaryControlV3] order={order.MoveOrderId} revision={order.OrderRevision} status={order.Status} " +
                $"start={order.StartCell} destination={order.DestinationCell} {pathSummary} {movementSummary}");
        }
    }

    private void PrintResourceWorkSummary(bool detailed)
    {
        GD.Print($"[ResourceCoreV3] initialized={ResourceCoreInitialized} nodes={ResourceNodeCount} trees={TreeNodeCount} stones={StoneNodeCount} depleted={DepletedResourceNodeCount} stacks={GroundStackCount} wood={WoodAmountOnGround} stone={StoneAmountOnGround} nodeViews={RuntimeResourceNodeViewCount} stackViews={RuntimeGroundStackViewCount}");
        GD.Print($"[StockpileV3] zones={StockpileZoneCount} cells={StockpileCellCount} localZones={LocalCompanyZoneCount} mode={StockpileDesignationMode} reservedCells={ReservedStockpileCellCount} outside={GroundAmountOutsideStockpile} woodStored={WoodAmountInStockpile} stoneStored={StoneAmountInStockpile}");
        GD.Print($"[ConstructionUiV3] trayOpen={ConstructionTrayOpen} activeTool={ActiveConstructionTool} stockpileMode={StockpileDesignationMode} blockedByWorldMap={ConstructionUiInputBlockedByWorldMap} lastAction={LastConstructionUiAction}");
        GD.Print($"[ConstructionV3] blueprints={ConstructionBlueprintCount} structures={ConstructionStructureCount} blockingCells={ConstructionBlockingCellCount} reservations={ConstructionReservationCount} occupancyRevision={ConstructionOccupancyRevision}");
        if(_workSession==null){GD.Print("[WorkCoreV3] initialized=false");return;}
        MercenaryWorkDiagnosticsV3 diagnostics=_workSession.Diagnostics;
        GD.Print($"[WorkCoreV3] initialized=true requests={ActiveWorkRequestCount} assignments={ActiveWorkAssignmentCount} reservations={ActiveWorkReservationCount} movingToWork={MovingToWorkCount} working={WorkingMercenaryCount} completed={diagnostics.CompletedWorkCount} failed={diagnostics.FailedWorkCount} cancelled={diagnostics.CancelledWorkCount} superseded={diagnostics.SupersededWorkCount} cycles={diagnostics.CompletedCycleCount} lastFailure={diagnostics.LastFailureReason}");
        HaulingDiagnosticsV3 hauling=_workSession.HaulingDiagnostics;GD.Print($"[HaulingV3] active={ActiveHaulingRequestCount} sourceReservations={ReservedSourceStackCount} destinationReservations={ReservedStockpileCellCount} carrying={CarryingMercenaryCount} completed={hauling.CompletedCount} failed={hauling.FailedCount} cancelled={hauling.CancelledCount} superseded={hauling.SupersededCount} emergencyDrops={hauling.EmergencyDropCount} conservationMismatch={hauling.ResourceConservationMismatchCount} lastFailure={hauling.LastFailureReason}");
        foreach(MercenaryWorkExecutionStateV3 execution in _workSession.GetActiveExecutions())
        {
            GD.Print($"[WorkCoreV3] request={execution.WorkRequestId} mercenary={execution.MercenaryId} node={execution.ResourceNodeId} phase={execution.Phase} approach={execution.SelectedApproachCell?.ToString()??"-"} candidate={execution.CurrentCandidateIndex}/{execution.CandidateApproachCells.Count} progress={execution.WorkProgressSeconds:0.00}/{execution.RequiredWorkSeconds:0.00} cycles={execution.CompletedCycleCount} failure={execution.FailureReason}");
            if(detailed)GD.Print($"  candidates={string.Join(',',execution.CandidateApproachCells)} movement={execution.MovementRequestId} revision={execution.Revision}");
        }
        foreach(HaulingWorkExecutionStateV3 execution in _workSession.GetActiveHaulingExecutions())
        {HaulingWorkPayloadV3 p=execution.Payload;int carry=_workSession.Carries.TryGetCarry(execution.MercenaryId,out MercenaryCarryStateV3? c)&&c!=null?c.Amount:0;GD.Print($"[HaulingV3] request={execution.WorkRequestId} mercenary={execution.MercenaryId} source={p.SourceStackId}@{p.SourceCell} requested={p.RequestedAmount} remaining={p.RemainingRequestedAmount} carry={carry} destination={p.DestinationCell?.ToString()??"-"} phase={execution.Phase} trips={p.CompletedTripCount} planned={execution.PlannedPickupAmount} handling={execution.HandlingProgressSeconds:0.00}/{execution.RequiredHandlingSeconds:0.00} score={execution.Calculation.HaulingScore:0.00} capacity={execution.Calculation.MaxCarryUnits} revision={execution.Revision}");}
        if(!detailed||_resourceSession==null)return;
        foreach(string id in _resourceSession.Nodes.GetAllNodeIds())if(_resourceSession.Nodes.TryGet(id,out ResourceNodeStateV3? node)&&node!=null)GD.Print($"[ResourceCoreV3] node={id} type={node.NodeType} cell={node.Cell} amount={node.RemainingAmount}/{node.MaxAmount} yield={node.YieldPerCycle} depleted={node.IsDepleted} reserved={_workSession.Reservations.IsReserved(id)} view={IsResourceNodeViewMaterialized(id)}");
        foreach(string id in _resourceSession.GroundStacks.GetAllStackIds())if(_resourceSession.GroundStacks.TryGet(id,out GroundResourceStackV3? stack)&&stack!=null)GD.Print($"[ResourceCoreV3] stack={id} type={stack.ResourceType} amount={stack.Amount} cell={stack.Cell} view={IsGroundStackViewMaterialized(id)}");
    }

    private static string ShortMercenaryId(string mercenaryId)
    {
        return mercenaryId.Length <= 13 ? mercenaryId : mercenaryId[..13];
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
            IsDungeonEntrance = cell.IsDungeonEntrance,
            DungeonEntranceKind = cell.DungeonEntranceKind,
            IsBanditCamp = cell.IsBanditCamp,
            BanditCampKind = cell.BanditCampKind,
            IsFactionOutpost = cell.IsFactionOutpost,
            FactionOutpostKind = cell.FactionOutpostKind,
            FactionOutpostOwner = cell.FactionOutpostOwner,
            ForestStrength = cell.ForestStrength,
            IsForest = cell.ForestStrength > 0.34f,
            IsDenseForest = cell.ForestStrength > 0.62f,
            IsBuildRestricted = cell.IsBuildRestricted,
            IsWalkable = cell.IsWalkable,
            BiomeKind = cell.BiomeKind,
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
