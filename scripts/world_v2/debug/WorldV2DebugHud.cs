using System.Collections.Generic;
using System.Text;
using GameplayV3.Control;
using GameplayV3.Mercenary;
using GameplayV3.Work;
using Godot;

namespace WorldV2;

public partial class WorldV2DebugHud : Control
{
    private Label? _label;
    private int _page;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Position = new Vector2(12.0f, 12.0f);
        CustomMinimumSize = new Vector2(650.0f, 300.0f);

        PanelContainer panel = new()
        {
            Name = "Panel",
            CustomMinimumSize = new Vector2(650.0f, 300.0f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(panel);

        MarginContainer margin = new()
        {
            Name = "Margin",
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        _label = new Label
        {
            Name = "Text",
            Text = "WorldV2 loading...",
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.Off,
            ClipText = true
        };
        margin.AddChild(_label);
    }

    public void Refresh(WorldManagerV2 manager, WorldV2BuildManager? buildManager, string message)
    {
        if (_label == null)
        {
            return;
        }

        SectorMetadata? metadata = manager.ActiveSectorMetadata;
        WorldGenerationSettingsV2 settings = manager.GetGenerationSettings();
        string sectorLine = metadata == null
            ? "sector: none"
            : $"sector: {metadata.SectorCoord} / {metadata.Type}";
        string worldConfigLine = $"map: {manager.MapSizePreset} {manager.WorldMapSize.WidthCells}x{manager.WorldMapSize.HeightCells} cells chunks={manager.WorldMapSize.ChunkWidth}x{manager.WorldMapSize.ChunkHeight} plan={manager.PlanVersion} generated={manager.GeneratedPlanType}";
        string worldBoundsLine = $"bounds: cells={manager.WorldBounds.Position}..{manager.WorldBounds.End - Vector2I.One}";
        string worldMapLine = $"world map: visible={manager.WorldMapOverlayVisible} texture={manager.WorldMapTextureSize.X}x{manager.WorldMapTextureSize.Y} build={manager.WorldMapBuildMs:0.0}ms cached={manager.WorldMapCached} reason={manager.WorldMapLastBuildReason}";
        string companyCoreLine =
            $"CompanyCoreInitialized={manager.CompanyCoreInitialized} " +
            $"LocalPlayerId={ShortId(manager.LocalPlayerId)} LocalCompanyId={ShortId(manager.LocalCompanyId)} " +
            $"LocalCompanyName={manager.LocalCompanyName} RegisteredCompanyCount={manager.RegisteredCompanyCount}";
        string companyOwnershipLine = $"LocalCompanyOwnershipValid={manager.LocalCompanyOwnershipValid}";
        bool hasLocalDeployment = manager.TryGetLocalDeployment(out GameplayV3.Deployment.CompanyDeploymentStateV3? localDeployment)
            && localDeployment != null;
        string deploymentLine = manager.TryGetStartingDeploymentResult(out GameplayV3.Deployment.StartingDeploymentPlacementResultV3? deploymentResult)
            && deploymentResult != null
            ? $"StartingDeploymentInitialized={deploymentResult.IsInitialized} StartingSettlementId={deploymentResult.StartingSettlementId} SettlementArrivalAnchorCell={FormatCell(deploymentResult.ArrivalAnchorCell)} RegisteredDeploymentCount={manager.RegisteredDeploymentCount} AssignedCompanyCount={deploymentResult.AssignedCompanyCount} UnassignedCompanyCount={deploymentResult.UnassignedCompanyCount}"
            : "StartingDeploymentInitialized=false";
        string localDeploymentLine = hasLocalDeployment
            ? $"LocalCompanyHasDeployment=true LocalCompanyDeploymentSlot={localDeployment!.DeploymentSlotIndex} LocalCompanyDeploymentAnchorCell={localDeployment.DeploymentAnchorCell} LocalCompanyFormationCells={FormatFormation(localDeployment)}"
            : "LocalCompanyHasDeployment=false";
        string deploymentStatsLine = deploymentResult == null
            ? "DeploymentPlacementAttempts=0 DeploymentRejectedBounds=0 DeploymentRejectedFeature=0 DeploymentRejectedFormation=0"
            : $"DeploymentPlacementAttempts={deploymentResult.PlacementAttempts} DeploymentRejectedBounds={deploymentResult.RejectedBounds} DeploymentRejectedFeature={deploymentResult.RejectedFeature} DeploymentRejectedFormation={deploymentResult.RejectedFormation} DeploymentFailureReason={deploymentResult.FailureReason}";
        string deploymentDistanceLine = hasLocalDeployment
            ? $"DistanceFromDeploymentToSettlementCenter={localDeployment!.DistanceToSettlementCenter:0.0} DistanceFromDeploymentToNearestRoad={localDeployment.DistanceToNearestRoad:0.0} DistanceFromDeploymentToNearestUnsafeFeatureCore={localDeployment.DistanceToNearestUnsafeFeatureCore:0.0}"
            : "DistanceFromDeploymentToSettlementCenter=- DistanceFromDeploymentToNearestRoad=- DistanceFromDeploymentToNearestUnsafeFeatureCore=-";
        string mercenaryCoreLine =
            $"MercenaryCoreInitialized={manager.MercenaryCoreInitialized} RegisteredMercenaryCount={manager.RegisteredMercenaryCount} " +
            $"LocalCompanyMercenaryCount={manager.LocalCompanyMercenaryCount} RuntimeMercenaryViewCount={manager.RuntimeMercenaryViewCount} " +
            $"InitialSquadCreationSucceeded={manager.InitialSquadCreationSucceeded} InitialSquadCreationReusedExisting={manager.InitialSquadCreationReusedExisting}";
        string mercenaryDiagnosticsLine =
            $"InitialSquadCreationFailureReason={manager.InitialSquadCreationFailureReason} InitialSquadRollbackCount={manager.InitialSquadRollbackCount} " +
            $"DuplicateMercenaryRejectedCount={manager.DuplicateMercenaryRejectedCount} DuplicateViewRejectedCount={manager.DuplicateViewRejectedCount} " +
            $"MercenaryDeploymentMismatchCount={manager.MercenaryDeploymentMismatchCount}";
        string mercenarySummaryLines = BuildMercenarySummaryLines(manager);
        string resourceLine=$"ResourceCoreInitialized={manager.ResourceCoreInitialized} nodes/tree/stone/depleted={manager.ResourceNodeCount}/{manager.TreeNodeCount}/{manager.StoneNodeCount}/{manager.DepletedResourceNodeCount} views={manager.RuntimeResourceNodeViewCount} stacks/views={manager.GroundStackCount}/{manager.RuntimeGroundStackViewCount} wood/stone={manager.WoodAmountOnGround}/{manager.StoneAmountOnGround}";
        string workLine=$"WorkCoreInitialized={manager.WorkCoreInitialized} requests={manager.ActiveWorkRequestCount} assignments={manager.ActiveWorkAssignmentCount} reservations={manager.ActiveWorkReservationCount} movingToWork={manager.MovingToWorkCount} working={manager.WorkingMercenaryCount}";
        if(manager.TryGetMercenaryWorkSession(out MercenaryWorkSessionV3? workDiagnosticsSession)&&workDiagnosticsSession!=null){MercenaryWorkDiagnosticsV3 diagnostics=workDiagnosticsSession.Diagnostics;workLine+=$" completed/failed/cancelled/superseded={diagnostics.CompletedWorkCount}/{diagnostics.FailedWorkCount}/{diagnostics.CancelledWorkCount}/{diagnostics.SupersededWorkCount} cycles={diagnostics.CompletedCycleCount} lastFailure={diagnostics.LastFailureReason}";}
        string stockpileLine=$"Stockpile zones/cells/local={manager.StockpileZoneCount}/{manager.StockpileCellCount}/{manager.LocalCompanyZoneCount} mode={manager.StockpileDesignationMode} reserved={manager.ReservedStockpileCellCount} outside={manager.GroundAmountOutsideStockpile} stored W/S={manager.WoodAmountInStockpile}/{manager.StoneAmountInStockpile}";
        string constructionUiLine=$"ConstructionTrayOpen={manager.ConstructionTrayOpen} ActiveConstructionTool={manager.ActiveConstructionTool} StockpileDesignationMode={manager.StockpileDesignationMode} ConstructionUiInputBlockedByWorldMap={manager.ConstructionUiInputBlockedByWorldMap} LastConstructionUiAction={manager.LastConstructionUiAction}";
        string haulingLine=$"Hauling active={manager.ActiveHaulingRequestCount} sourceRes={manager.ReservedSourceStackCount} carrying={manager.CarryingMercenaryCount}";
        string mercenaryControlLine = "MercenaryControlInitialized=false";
        string mercenaryPathLine = "selection=- commands=0 orders=0 movement=0";
        if (manager.TryGetMercenaryControlSession(out MercenaryControlSessionV3? controlSession) && controlSession != null)
        {
            MercenaryControlDiagnosticsV3 controlDiagnostics = controlSession.Diagnostics;
            mercenaryControlLine =
                $"MercenaryControlInitialized=true SelectedMercenaryCount={controlSession.Selection.Count} " +
                $"SelectionRevision={controlSession.Selection.Revision} LastSelectionAction={controlSession.Selection.LastSelectionAction} " +
                $"SelectedIds={FormatIds(controlSession.Selection.GetSelectedIds())}";
            mercenaryPathLine =
                $"commands={controlSession.Commands.ActiveCommandCount} orders={controlSession.Commands.ActiveMoveOrderCount} " +
                $"moving={controlSession.Movements.Count} pathDone/failed={controlDiagnostics.CompletedPathCount}/{controlDiagnostics.FailedPathCount} " +
                $"moveDone/failed={controlDiagnostics.CompletedMovementCount}/{controlDiagnostics.FailedMovementCount} " +
                $"avgMoveMul={GetAverageMoveMultiplier(manager):0.00} " +
                $"lastPath=len:{controlDiagnostics.LastPathLength} cost:{controlDiagnostics.LastPathCost:0.0} expanded:{controlDiagnostics.LastExpandedNodeCount} cpu:{controlDiagnostics.LastSearchDurationMs:0.00}ms " +
                $"stale/limit/peak={controlDiagnostics.StalePathResultDiscardCount}/{controlDiagnostics.SearchLimitExceededCount}/{controlDiagnostics.PeakDiscoveredCellCount}";
        }
        string v3VillageLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 settlements: count={manager.V3VillageCount} ham/v/lg/town/city={manager.V3HamletCount}/{manager.V3VillageTierCount}/{manager.V3LargeVillageCount}/{manager.V3TownCount}/{manager.V3CityCandidateCount} startId={manager.V3StartingVillageId} tier={manager.V3StartingSettlementTier} role={manager.V3StartingSettlementRole} center={manager.V3StartingVillageCenter} spawn={manager.V3PlayerSpawnCell} roles={manager.V3SettlementRoleDistribution}"
            : "v3 villages: inactive";
        string v3BiomeLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 biomes: enabled={manager.V3BiomeLayerEnabled} mode={manager.V3BiomeResolveMode} regions={manager.V3BiomeRegionCount} major={manager.V3MajorBiomeRegionCount} minor={manager.V3MinorBiomeRegionCount} avgR={manager.V3AverageMajorBiomeRadius:0}/{manager.V3AverageMinorBiomeRadius:0} sampleP/F/R/D/W={manager.V3BiomeCellDistribution} featureQuota={manager.V3BiomeFeatureDistributionEnabled} quotaFallback={manager.V3BiomeQuotaFallbackCount}"
            : "v3 biomes: inactive";
        string v3RoadLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 roads: enabled={manager.V3RoadLayerEnabled} total={manager.V3RoadCount} primary={manager.V3PrimaryRoadCount} secondary={manager.V3SecondaryRoadCount} branch={manager.V3BranchRoadCount} nodes={manager.V3RoadNodeCount} junctions={manager.V3RoadJunctionCount} maxDeg={manager.V3MaxRoadJunctionDegree} trunks={manager.V3SharedTrunkCount} merged={manager.V3MergedRoadCandidateCount} rejectJ/deg/x/long={manager.V3RejectedRoadJunctionCount}/{manager.V3RejectedHighDegreeJunctionCount}/{manager.V3RejectedRoadCrossingCount}/{manager.V3RejectedRoadTooLongCount} targets={manager.V3RoadTargetAnchorCount} q/r/d/b/fa/f/e/future={manager.V3RoadTargetQuarryCount}/{manager.V3RoadTargetRuinCount}/{manager.V3RoadTargetDungeonEntranceCount}/{manager.V3RoadTargetBanditCampCount}/{manager.V3RoadTargetFactionOutpostCount}/{manager.V3RoadTargetForestEdgeCount}/{manager.V3RoadTargetWorldEdgeExitCount}/{manager.V3FutureRoadTargetCount} rejectT/B={manager.V3RejectedRoadTargetCount}/{manager.V3RejectedBranchRoadCount}"
            : "v3 roads: inactive";
        string v3ForestLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 forests: enabled={manager.V3ForestLayerEnabled} regions={manager.V3ForestRegionCount} major={manager.V3MajorForestRegionCount} minor={manager.V3MinorForestPatchCount} bonus={manager.V3ForestTotalBonusApplied} rejected={manager.V3RejectedForestPlacementCount} dist={manager.V3ForestBiomeDistribution} majorDist={manager.V3MajorForestBiomeDistribution} minorDist={manager.V3MinorForestBiomeDistribution}"
            : "v3 forests: inactive";
        string v3QuarryLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 quarries: enabled={manager.V3QuarryLayerEnabled} regions={manager.V3QuarryRegionCount} major={manager.V3MajorQuarryCount} minor={manager.V3MinorQuarryCount} rejected={manager.V3RejectedQuarryPlacementCount} dist={manager.V3QuarryBiomeDistribution}"
            : "v3 quarries: inactive";
        string v3RuinLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 ruins: enabled={manager.V3RuinLayerEnabled} sites={manager.V3RuinSiteCount} roadLinked={manager.V3RoadLinkedRuinCount} rejected={manager.V3RejectedRuinPlacementCount} dist={manager.V3RuinBiomeDistribution}"
            : "v3 ruins: inactive";
        string v3DungeonLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 dungeons: enabled={manager.V3DungeonLayerEnabled} entrances={manager.V3DungeonEntranceCount} roadLinked={manager.V3RoadLinkedDungeonEntranceCount} rejected={manager.V3RejectedDungeonEntrancePlacementCount} kinds={manager.V3DungeonEntranceKindDistribution} dist={manager.V3DungeonEntranceBiomeDistribution}"
            : "v3 dungeons: inactive";
        string v3BanditLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 bandits: enabled={manager.V3BanditLayerEnabled} camps={manager.V3BanditCampCount} roadLinked={manager.V3RoadLinkedBanditCampCount} rejected={manager.V3RejectedBanditCampPlacementCount} kinds={manager.V3BanditCampKindDistribution} dist={manager.V3BanditCampBiomeDistribution}"
            : "v3 bandits: inactive";
        string v3FactionLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 faction: enabled={manager.V3FactionOutpostLayerEnabled} outposts={manager.V3FactionOutpostCount} roadLinked={manager.V3RoadLinkedFactionOutpostCount} rejected={manager.V3RejectedFactionOutpostPlacementCount} kinds={manager.V3FactionOutpostKindDistribution} owners={manager.V3FactionOutpostOwnerDistribution} dist={manager.V3FactionOutpostBiomeDistribution}"
            : "v3 faction: inactive";
        string dangerLine = metadata == null
            ? "danger/resources: -"
            : $"danger: {metadata.DangerLevel:0.00}  resources: {metadata.ResourceRichness:0.00}";
        string biomeStatsLine = metadata == null
            ? "biome stats: -"
            : $"sector: roads={metadata.RoadPathCount} villages={metadata.VillageCount} landmarks={metadata.LandmarkCount} connected={metadata.RoadConnectedLandmarkCount} quarries={metadata.QuarryCount}";
        string buildLine = buildManager == null
            ? "build: missing"
            : $"build: {buildManager.SelectedBuildType}  activeSector={buildManager.GetActiveSectorStructureCount()} total={buildManager.GetRuntimeStructureCount()}";
        SectorRuntimeState? runtimeState = manager.ActiveRuntimeState;
        string runtimeLine = runtimeState == null
            ? "runtime: none"
            : $"runtime: yes dirty={runtimeState.IsDirty} dirtySectors={manager.GetDirtyRuntimeSectorCount()} states={manager.GetRuntimeStateCount()}";
        WorldStreamManagerV2? streamManager = manager.GetStreamManager();
        string streamLine = streamManager == null
            ? "stream: missing"
            : $"cameraCell: {streamManager.CenterGlobalCellCoord}  chunk: {streamManager.CenterGlobalChunkCoord} loaded: {streamManager.LoadedChunkCount}";
        string cameraLine = streamManager == null
            ? "camera: -"
            : $"camera: pos={streamManager.CameraWorldPosition.Round()} speed={streamManager.CameraSpeed:0} zoom={streamManager.CameraZoom.X:0.00}";
        string chunkLine = streamManager == null
            ? "chunks: -"
            : $"chunks: range={streamManager.VisibleChunkRange.Position}..{streamManager.VisibleChunkRange.End - Vector2I.One} required={streamManager.RequiredChunkCount} clamped={streamManager.ClampedRequiredChunkCount} actual={streamManager.LoadedChunkCount}";
        string limitLine = streamManager == null
            ? "limits: -"
            : $"overlay={streamManager.OverlayMode} grid={streamManager.ShowGrid} margin={streamManager.PreloadMarginChunks} renderLimit={streamManager.MaxRenderedChunkCountHardLimit} clamped={streamManager.IsRenderClamped}";
        string streamingModeLine = streamManager == null
            ? "mode: -"
            : $"mode: {streamManager.StreamingMode} initial={streamManager.InitialLoadingLoadedChunks}/{streamManager.InitialLoadingTargetChunks} ({streamManager.InitialLoadingProgress:P0}) idlePrefetch={streamManager.IdlePrefetchEnabled}";
        string queueLine = streamManager == null
            ? "queues: -"
            : $"queues: required={streamManager.RequiredQueueCount} warm={streamManager.WarmQueueCount} prefetch={streamManager.PrefetchQueueCount} attach={streamManager.AttachQueueCount} detach={streamManager.DetachQueueCount} pregFrame={streamManager.PregeneratedThisFrame} cached={streamManager.CachedChunkCount}";
        string workerLine = streamManager == null
            ? "worker: -"
            : $"worker: pending={streamManager.WorkerPendingCount} completed={streamManager.WorkerCompletedCount} genFrame={streamManager.WorkerGeneratedThisFrame} applied={streamManager.CompletedAppliedThisFrame} mainGen={streamManager.MainThreadChunkGenerationCount} avg/max={streamManager.WorkerAverageMs:0.00}/{streamManager.WorkerMaxMs:0.00}ms";
        string performanceLine = "perf: -";
        string generationProfileLine = "gen profile: -";
        string renderProfileLine = "render profile: -";
        string cacheLine = "cache: -";
        string slowestChunkLine = "slowest chunk: -";
        string contextProfileLine = "context profile: -";
        string flatlandToggleLine = FlatlandWorldPlanV2.GetDebugToggleSummary();
        string generationLayerLine = WorldGenerationLayerSettingsV2.GetSummary();
        WorldV2PerformanceSummary perf = WorldV2PerformanceProfiler.Instance.GetSummary();
        if (streamManager != null)
        {
            WorldChunkCacheSummaryV2 cache = streamManager.GetChunkCacheSummary();
            WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
            WorldV2MetricSummary raster = profiler.GetMetricSummary(WorldV2PerformanceProfiler.FlatlandChunkRasterBuild);
            WorldV2MetricSummary river = profiler.GetMetricSummary(WorldV2PerformanceProfiler.FlatlandRiverSample);
            WorldV2MetricSummary forest = profiler.GetMetricSummary(WorldV2PerformanceProfiler.FlatlandForestSample);
            WorldV2MetricSummary road = profiler.GetMetricSummary(WorldV2PerformanceProfiler.FlatlandRoadSample);
            WorldV2MetricSummary site = profiler.GetMetricSummary(WorldV2PerformanceProfiler.FlatlandSiteSample);
            WorldV2MetricSummary regionEnsure = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextRegionPlanEnsure);
            WorldV2MetricSummary roadEnsure = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextRoadCacheEnsure);
            WorldV2MetricSummary roadQuery = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextRoadQuery);
            WorldV2MetricSummary villageQuery = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextVillageQuery);
            WorldV2MetricSummary landmarkQuery = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextLandmarkQuery);
            WorldV2MetricSummary forestQuery = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextForestQuery);
            WorldV2MetricSummary riverQuery = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextRiverQuery);
            performanceLine = $"perf: rendered={streamManager.RenderedChunkCount} pending={streamManager.PendingGenerationCount} genFrame={perf.GeneratedChunksThisFrame} reqFrame={streamManager.RequestedThisFrame} attachFrame={streamManager.AttachedThisFrame} detachFrame={streamManager.DetachedThisFrame} pool={streamManager.RendererPoolCount}";
            generationProfileLine = $"gen ms: last={perf.GenerateChunk.LastMs:0.00} avg={perf.GenerateChunk.AverageMs:0.00} max={perf.GenerateChunk.MaxMs:0.00} sampleAvg={perf.FlatlandSample.AverageMs:0.00} rasterAvg={raster.AverageMs:0.00}";
            renderProfileLine = $"render ms: attach={perf.RendererAttach.LastMs:0.00}/{perf.RendererAttach.AverageMs:0.00} rebuild={perf.RendererRebuild.LastMs:0.00}/{perf.RendererRebuild.AverageMs:0.00} hit={perf.CacheHit.LastMs:0.00}";
            cacheLine = $"cache: data={cache.CachedChunkDataCount}/{cache.MaxCachedChunkDataCount} cells~={cache.ApproxCachedCellCount} hit={cache.CacheHitCount} miss={cache.CacheMissCount} evict={cache.CacheEvictionCount} dirty={cache.DirtyCount}";
            cacheLine += $" | queues a/d={streamManager.AttachQueueCount}/{streamManager.DetachQueueCount} | flatland avg r/f/rd/site={river.AverageMs:0.00}/{forest.AverageMs:0.00}/{road.AverageMs:0.00}/{site.AverageMs:0.00}";
            slowestChunkLine = $"slowest chunk: {perf.SlowestChunkCoord} {perf.SlowestChunkMs:0.00}ms {perf.SlowestChunkContextInfo}";
            contextProfileLine = $"ctx avg: region={regionEnsure.AverageMs:0.00} roadEnsure={roadEnsure.AverageMs:0.00} road={roadQuery.AverageMs:0.00} village={villageQuery.AverageMs:0.00} site={landmarkQuery.AverageMs:0.00} forest={forestQuery.AverageMs:0.00} river={riverQuery.AverageMs:0.00}";
        }
        string sampleLine = "sample: -";
        string featureLine = "features: -";
        string siteLine = "site: -";
        FlatlandCellSampleV2? flatlandSample = null;

        if (streamManager != null)
        {
            flatlandSample = manager.SampleFlatlandAt(streamManager.CenterGlobalCellCoord);
            sampleLine = $"sample: tile={flatlandSample.TileType} biome={flatlandSample.Biome} biomeV3={flatlandSample.BiomeKind} forest={flatlandSample.ForestStrength:0.00} restricted={flatlandSample.IsBuildRestricted}";
            featureLine = $"features: river={flatlandSample.IsRiver} bank={flatlandSample.IsRiverBank} bridge={flatlandSample.IsBridgeCandidate} road={flatlandSample.IsRoad} quarry={flatlandSample.IsQuarry} ore={flatlandSample.HasOreSpot} dungeon={flatlandSample.IsDungeonEntrance}:{flatlandSample.DungeonEntranceKind} bandit={flatlandSample.IsBanditCamp}:{flatlandSample.BanditCampKind} faction={flatlandSample.IsFactionOutpost}:{flatlandSample.FactionOutpostKind}/{flatlandSample.FactionOutpostOwner}";
            siteLine = $"site: village={flatlandSample.IsVillage} starting={flatlandSample.IsStartingVillage} landmark={flatlandSample.LandmarkKind}";
        }

        if (_page == 1)
        {
            _label.Text =
                $"WorldV2 flatland settings  page 2/2\n" +
                $"world: {manager.WorldId}  seed: {manager.WorldSeed}\n" +
                $"{companyCoreLine}\n" +
                $"{companyOwnershipLine}\n" +
                $"{deploymentLine}\n" +
                $"{localDeploymentLine}\n" +
                $"{deploymentStatsLine}\n" +
                $"{deploymentDistanceLine}\n" +
                $"{mercenaryCoreLine}\n" +
                $"{mercenaryDiagnosticsLine}\n" +
                $"{mercenaryControlLine}\n" +
                $"{mercenaryPathLine}\n" +
                $"{resourceLine}\n" +
                $"{workLine}\n" +
                $"{stockpileLine}\n" +
                $"{constructionUiLine}\n" +
                $"{haulingLine}\n" +
                $"{mercenarySummaryLines}" +
                $"rivers: count={settings.RiverCount} width={settings.RiverWidth:0.0} bank={settings.RiverBankWidth:0.0} meander={settings.RiverMeanderStrength:0}\n" +
                $"forest: clusters={settings.ForestClusterCount} length={settings.ForestClusterMinLength:0}-{settings.ForestClusterMaxLength:0} width={settings.ForestClusterMinWidth:0}-{settings.ForestClusterMaxWidth:0}\n" +
                $"v3 biome regions: small={settings.V3SmallMajorBiomeMinCount}-{settings.V3SmallMajorBiomeMaxCount}/{settings.V3SmallMinorBiomeMinCount}-{settings.V3SmallMinorBiomeMaxCount} medium={settings.V3MediumMajorBiomeMinCount}-{settings.V3MediumMajorBiomeMaxCount}/{settings.V3MediumMinorBiomeMinCount}-{settings.V3MediumMinorBiomeMaxCount} large={settings.V3LargeMajorBiomeMinCount}-{settings.V3LargeMajorBiomeMaxCount}/{settings.V3LargeMinorBiomeMinCount}-{settings.V3LargeMinorBiomeMaxCount} huge={settings.V3HugeMajorBiomeMinCount}-{settings.V3HugeMajorBiomeMaxCount}/{settings.V3HugeMinorBiomeMinCount}-{settings.V3HugeMinorBiomeMaxCount}\n" +
                $"v3 forest regions: small={settings.V3SmallMajorForestMinCount}-{settings.V3SmallMajorForestMaxCount}/{settings.V3SmallMinorForestMinCount}-{settings.V3SmallMinorForestMaxCount} medium={settings.V3MediumMajorForestMinCount}-{settings.V3MediumMajorForestMaxCount}/{settings.V3MediumMinorForestMinCount}-{settings.V3MediumMinorForestMaxCount} large={settings.V3LargeMajorForestMinCount}-{settings.V3LargeMajorForestMaxCount}/{settings.V3LargeMinorForestMinCount}-{settings.V3LargeMinorForestMaxCount} huge={settings.V3HugeMajorForestMinCount}-{settings.V3HugeMajorForestMaxCount}/{settings.V3HugeMinorForestMinCount}-{settings.V3HugeMinorForestMaxCount}\n" +
                $"v3 quarries: small={settings.V3SmallMajorQuarryMinCount}-{settings.V3SmallMajorQuarryMaxCount}/{settings.V3SmallMinorQuarryMinCount}-{settings.V3SmallMinorQuarryMaxCount} medium={settings.V3MediumMajorQuarryMinCount}-{settings.V3MediumMajorQuarryMaxCount}/{settings.V3MediumMinorQuarryMinCount}-{settings.V3MediumMinorQuarryMaxCount} large={settings.V3LargeMajorQuarryMinCount}-{settings.V3LargeMajorQuarryMaxCount}/{settings.V3LargeMinorQuarryMinCount}-{settings.V3LargeMinorQuarryMaxCount} huge={settings.V3HugeMajorQuarryMinCount}-{settings.V3HugeMajorQuarryMaxCount}/{settings.V3HugeMinorQuarryMinCount}-{settings.V3HugeMinorQuarryMaxCount}\n" +
                $"v3 quarry field: radius major={settings.V3MajorQuarryMinRadius:0}-{settings.V3MajorQuarryMaxRadius:0} minor={settings.V3MinorQuarryMinRadius:0}-{settings.V3MinorQuarryMaxRadius:0} oreChance={settings.V3QuarryOreSpotChance:P1}\n" +
                $"v3 ruins: small={settings.V3SmallRuinMinCount}-{settings.V3SmallRuinMaxCount} medium={settings.V3MediumRuinMinCount}-{settings.V3MediumRuinMaxCount} large={settings.V3LargeRuinMinCount}-{settings.V3LargeRuinMaxCount} huge={settings.V3HugeRuinMinCount}-{settings.V3HugeRuinMaxCount} radius={settings.V3RuinMinRadius:0}-{settings.V3RuinMaxRadius:0}\n" +
                $"v3 dungeons: small={settings.V3SmallDungeonEntranceMinCount}-{settings.V3SmallDungeonEntranceMaxCount} medium={settings.V3MediumDungeonEntranceMinCount}-{settings.V3MediumDungeonEntranceMaxCount} large={settings.V3LargeDungeonEntranceMinCount}-{settings.V3LargeDungeonEntranceMaxCount} huge={settings.V3HugeDungeonEntranceMinCount}-{settings.V3HugeDungeonEntranceMaxCount} radius={settings.V3DungeonEntranceMinRadius:0}-{settings.V3DungeonEntranceMaxRadius:0}\n" +
                $"v3 bandits: small={settings.V3SmallBanditCampMinCount}-{settings.V3SmallBanditCampMaxCount} medium={settings.V3MediumBanditCampMinCount}-{settings.V3MediumBanditCampMaxCount} large={settings.V3LargeBanditCampMinCount}-{settings.V3LargeBanditCampMaxCount} huge={settings.V3HugeBanditCampMinCount}-{settings.V3HugeBanditCampMaxCount} radius={settings.V3BanditCampMinRadius:0}-{settings.V3BanditCampMaxRadius:0}\n" +
                $"v3 faction: small={settings.V3SmallFactionOutpostMinCount}-{settings.V3SmallFactionOutpostMaxCount} medium={settings.V3MediumFactionOutpostMinCount}-{settings.V3MediumFactionOutpostMaxCount} large={settings.V3LargeFactionOutpostMinCount}-{settings.V3LargeFactionOutpostMaxCount} huge={settings.V3HugeFactionOutpostMinCount}-{settings.V3HugeFactionOutpostMaxCount} radius={settings.V3FactionOutpostMinRadius:0}-{settings.V3FactionOutpostMaxRadius:0}\n" +
                $"v3 road graph: nearest={settings.V3RoadNearestNeighborCount} extraRatio={settings.V3RoadExtraEdgeRatio:0.00} sharedExit={settings.V3SharedExitTrunkEnabled} maxDegree={settings.V3MaxRoadJunctionDegree} maxCross={settings.V3MaxRoadCrossingsPerEdge}\n" +
                $"v3 branch roads: small={settings.V3SmallBranchRoadMinCount}-{settings.V3SmallBranchRoadMaxCount} medium={settings.V3MediumBranchRoadMinCount}-{settings.V3MediumBranchRoadMaxCount} large={settings.V3LargeBranchRoadMinCount}-{settings.V3LargeBranchRoadMaxCount} huge={settings.V3HugeBranchRoadMinCount}-{settings.V3HugeBranchRoadMaxCount} length={settings.V3BranchRoadMinLength:0}-{settings.V3BranchRoadMaxLength:0} targets={manager.V3RoadTargetAnchorCount}\n" +
                $"world map texture sizes: small={settings.WorldMapSmallTextureSize} medium={settings.WorldMapMediumTextureSize} large={settings.WorldMapLargeTextureSize} huge={settings.WorldMapHugeTextureSize}\n" +
                $"roads: width={settings.RoadWidth:0.0} forestPenalty={settings.RoadForestPenalty:0} riverPenalty={settings.RoadRiverPenalty:0} villageAttract={settings.RoadVillageAttraction:0.00}\n" +
                $"sites: villageRadius={settings.VillageRadius:0} startRadius={settings.StartVillageRadius:0} landmarks={settings.LandmarkCountPerRegion} quarries={settings.QuarryCountPerRegion}\n" +
                $"road chances: ruin={settings.RuinRoadChance:P0} dungeon={settings.DungeonRoadChance:P0} bandit={settings.BanditRoadChance:P0} faction={settings.FactionRoadChance:P0} quarry={settings.QuarryRoadChance:P0}\n" +
                $"{biomeStatsLine}\n" +
                $"{worldMapLine}\n" +
                $"{v3VillageLine}\n" +
                $"{v3BiomeLine}\n" +
                $"{v3RoadLine}\n" +
                $"{v3ForestLine}\n" +
                $"{v3QuarryLine}\n" +
                $"{v3RuinLine}\n" +
                $"{v3DungeonLine}\n" +
                $"{v3BanditLine}\n" +
                $"{v3FactionLine}\n" +
                $"{performanceLine}\n" +
                $"{generationProfileLine}\n" +
                $"{renderProfileLine}\n" +
                $"{cacheLine}\n" +
                $"{slowestChunkLine}\n" +
                $"{contextProfileLine}\n" +
                $"{flatlandToggleLine}\n" +
                $"{generationLayerLine}\n" +
                $"{streamingModeLine}\n" +
                $"{queueLine}\n" +
                $"{workerLine}\n" +
                $"{sampleLine}\n" +
                $"{featureLine}\n" +
                $"{siteLine}\n" +
                $"debug: F1 help, F2 page, F3 overlay, Ctrl+1-4 raster, Ctrl+5-8 context, Ctrl+Shift+1-9 layers, F11 rebuild renderers, F12 full reset\n" +
                $"{message}";
            return;
        }

        _label.Text =
            $"WorldV2  page 1/2\n" +
            $"world: {manager.WorldId}\n" +
            $"seed: {manager.WorldSeed}\n" +
            $"{companyCoreLine}\n" +
            $"{companyOwnershipLine}\n" +
            $"{deploymentLine}\n" +
            $"{localDeploymentLine}\n" +
            $"{deploymentStatsLine}\n" +
            $"{deploymentDistanceLine}\n" +
            $"{mercenaryCoreLine}\n" +
            $"{mercenaryDiagnosticsLine}\n" +
            $"{mercenaryControlLine}\n" +
            $"{mercenaryPathLine}\n" +
            $"{resourceLine}\n" +
            $"{workLine}\n" +
            $"{stockpileLine}\n" +
            $"{constructionUiLine}\n" +
            $"{haulingLine}\n" +
            $"{mercenarySummaryLines}" +
            $"{worldConfigLine}\n" +
            $"{worldBoundsLine}\n" +
            $"{worldMapLine}\n" +
            $"{v3VillageLine}\n" +
            $"{v3BiomeLine}\n" +
            $"{v3RoadLine}\n" +
            $"{v3ForestLine}\n" +
            $"{v3QuarryLine}\n" +
            $"{v3RuinLine}\n" +
            $"{v3DungeonLine}\n" +
            $"{v3BanditLine}\n" +
            $"{v3FactionLine}\n" +
            $"{sectorLine}\n" +
            $"{dangerLine}\n" +
            $"{biomeStatsLine}\n" +
            $"{cameraLine}\n" +
            $"{streamLine}\n" +
            $"{chunkLine}\n" +
            $"{limitLine}\n" +
            $"{streamingModeLine}\n" +
            $"{queueLine}\n" +
            $"{workerLine}\n" +
            $"{performanceLine}\n" +
            $"{generationProfileLine}\n" +
            $"{renderProfileLine}\n" +
            $"{cacheLine}\n" +
            $"{slowestChunkLine}\n" +
            $"{contextProfileLine}\n" +
            $"{flatlandToggleLine}\n" +
            $"{generationLayerLine}\n" +
            $"{sampleLine}\n" +
            $"{featureLine}\n" +
            $"{siteLine}\n" +
            $"{runtimeLine}\n" +
            $"{buildLine}\n" +
            $"keys: {(manager.PlanVersion == WorldPlanVersionV2.V3 ? "LMB select/drag, Shift add, RMB move" : "1 floor, 2 wall, LMB place, RMB remove")}\n" +
            $"camera: WASD/arrows move, Shift sprint, wheel zoom\n" +
            $"debug: F1 help, F2 page, F3 overlay, F4 grid, F6 sectors, F7 stream, F8 cache, F9 plan, F10 perf, Ctrl+1-4 raster, Ctrl+5-8 context, Ctrl+Shift+1-9 layers\n" +
            $"regen: F11 renderer rebuild, F12 full reset, Home center\n" +
            $"{message}";
    }

    public void TogglePage()
    {
        _page = (_page + 1) % 2;
    }

    private static string ShortId(string id)
    {
        const int visibleLength = 12;
        return string.IsNullOrEmpty(id) || id.Length <= visibleLength
            ? id
            : id[..visibleLength];
    }

    private static string FormatCell(GlobalCellCoord? cell)
    {
        return cell?.ToString() ?? "-";
    }

    private static string FormatFormation(GameplayV3.Deployment.CompanyDeploymentStateV3 deployment)
    {
        return deployment.FormationCells.Count == 3
            ? $"{deployment.FormationCells[0]}/{deployment.FormationCells[1]}/{deployment.FormationCells[2]}"
            : "invalid";
    }

    private static string FormatIds(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
        {
            return "-";
        }

        StringBuilder builder = new();
        for (int index = 0; index < ids.Count; index++)
        {
            if (index > 0) builder.Append(',');
            builder.Append(ShortId(ids[index]));
        }
        return builder.ToString();
    }

    private static string BuildMercenarySummaryLines(WorldManagerV2 manager)
    {
        if (!manager.TryGetMercenarySession(out MercenarySessionV3? session) || session == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        IReadOnlyList<string> mercenaryIds = session.Registry.GetMercenariesByCompany(manager.LocalCompanyId);
        foreach (string mercenaryId in mercenaryIds)
        {
            if (!session.Registry.TryGetMercenary(mercenaryId, out MercenaryProfileV3? profile, out MercenaryStateV3? state)
                || profile == null || state == null)
            {
                continue;
            }

            IReadOnlyList<MercenaryWorkSkillValueV3> topSkills = profile.WorkSkills.GetTopSkills(2);
            MercenaryDerivedStatsV3 derived = MercenaryDerivedStatsCalculatorV3.Calculate(profile);
            string initialSlot = profile.InitialSquadSlotIndex?.ToString() ?? "-";
            string movement = string.Empty;
            if (manager.TryGetMercenaryControlSession(out MercenaryControlSessionV3? control)
                && control != null
                && control.Commands.TryGetActiveOrder(mercenaryId, out MercenaryMoveOrderV3? order)
                && order != null)
            {
                movement = $" dest={order.DestinationCell} status={order.Status}";
                if (control.Movements.TryGet(mercenaryId, out GameplayV3.Movement.MercenaryMovementStateV3? active) && active != null)
                {
                    movement += $" remaining={active.RemainingPathCells}";
                }
            }
            string work = string.Empty;
            if (manager.TryGetMercenaryWorkSession(out MercenaryWorkSessionV3? workSession) && workSession != null)
            {
                GatheringWorkCalculationV3 calculation=GatheringWorkCalculatorV3.Calculate(profile);
                work=$" gather={calculation.GatheringScore:0.0}/{calculation.RequiredWorkSeconds:0.00}s";
                if(workSession.TryGetAssignment(mercenaryId,out MercenaryWorkAssignmentV3? assignment)&&assignment!=null&&workSession.TryGetExecution(assignment.WorkRequestId,out MercenaryWorkExecutionStateV3? execution)&&execution!=null)work+=$" work={ShortId(assignment.WorkRequestId)}:{execution.Phase}";
            }
            builder.Append("merc ")
                .Append(ShortId(mercenaryId)).Append(' ')
                .Append(profile.DisplayName)
                .Append(" initialSlot=").Append(initialSlot)
                .Append(" cell=").Append(state.CurrentCell)
                .Append(' ').Append(state.ActivityState)
                .Append(" top=").Append(topSkills[0]).Append('/').Append(topSkills[1])
                .Append(" moveMul=").Append(derived.MoveSpeedMultiplier.ToString("0.00"))
                .Append(" carry=").Append(derived.CarryCapacity.ToString("0.0"))
                .Append(" view=").Append(manager.IsMercenaryViewMaterialized(mercenaryId))
                .Append(movement)
                .Append(work)
                .Append('\n');
        }

        return builder.ToString();
    }

    private static float GetAverageMoveMultiplier(WorldManagerV2 manager)
    {
        if (!manager.TryGetMercenarySession(out MercenarySessionV3? session) || session == null)
        {
            return 0.0f;
        }

        IReadOnlyList<string> ids = session.Registry.GetMercenariesByCompany(manager.LocalCompanyId);
        if (ids.Count == 0) return 0.0f;
        float total = 0.0f;
        int count = 0;
        foreach (string id in ids)
        {
            if (session.Registry.TryGetProfile(id, out MercenaryProfileV3? profile) && profile != null)
            {
                total += MercenaryDerivedStatsCalculatorV3.Calculate(profile).MoveSpeedMultiplier;
                count++;
            }
        }
        return count == 0 ? 0.0f : total / count;
    }
}
