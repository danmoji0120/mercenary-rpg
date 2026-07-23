using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using GameplayV3.Control;
using GameplayV3.Mercenary;
using GameplayV3.Work;
using GameplayV3.Needs;
using GameplayV3.Time;
using Godot;

namespace WorldV2;

public partial class WorldV2DebugHud : Control
{
    public const int MaxResourceDetailRows = 12;
    public const int RefreshIntervalMilliseconds = 250;
    private Label? _label;
    private int _page;
    private ulong _lastRefreshTicks;
    private long _refreshStartTicks;

    public long DebugHudRefreshCount { get; private set; }
    public long DebugHudSkippedHiddenCount { get; private set; }
    public long DebugHudSkippedUnchangedCount { get; private set; }
    public double DebugHudLastBuildMs { get; private set; }
    public double DebugHudMaxBuildMs { get; private set; }
    public int DebugHudLastTextLength { get; private set; }
    public int DebugHudMaxTextLength { get; private set; }
    public int DebugHudResourceRowsWritten { get; private set; }
    public int DebugHudResourceRowsOmitted { get; private set; }
    public long DebugHudResourceFullRegistryScanCount { get; private set; }
    public long DebugHudTextAssignmentCount { get; private set; }

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

        if (!Visible)
        {
            DebugHudSkippedHiddenCount++;
            return;
        }

        ulong now = Time.GetTicksMsec();
        if (_lastRefreshTicks != 0 && now - _lastRefreshTicks < RefreshIntervalMilliseconds)
        {
            return;
        }

        _lastRefreshTicks = now;
        _refreshStartTicks = Stopwatch.GetTimestamp();
        DebugHudRefreshCount++;

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
        DebugHudResourceRowsWritten = 0;
        DebugHudResourceRowsOmitted = manager.ResourceNodeCount;
        DebugHudResourceFullRegistryScanCount = 0;
        string resourceLine=$"ResourceCoreInitialized={manager.ResourceCoreInitialized} registry nodes/tree/stone/depleted={manager.ResourceNodeCount}/{manager.TreeNodeCount}/{manager.StoneNodeCount}/{manager.DepletedResourceNodeCount} views active/pooled/createdTotal/chunks/outside={manager.RuntimeResourceNodeViewCount}/{manager.RuntimeResourceNodeViewPooledCount}/{manager.RuntimeResourceNodeViewCreatedTotal}/{manager.RuntimeResourceNodeViewAttachedChunkCount}/{manager.RuntimeResourceNodeViewOutsideRenderedChunkCount} stacks/views={manager.GroundStackCount}/{manager.RuntimeGroundStackViewCount} wood/stone={manager.WoodAmountOnGround}/{manager.StoneAmountOnGround} detailRows=0 omitted={DebugHudResourceRowsOmitted}\nStarterResources registry[{manager.StarterResourceRegistryCounts}] loaded[{manager.StarterResourceLoadedCounts}] exhausted[{manager.StarterResourceExhaustedCounts}] gathered[{manager.StarterResourceGatheredAmounts}] unknown definition/visual={manager.UnknownResourceDefinitionCount}/{manager.UnknownResourceVisualCount}\nResourceDistribution v={manager.ResourcePlacementAlgorithmVersion} profiles/rules={manager.BiomeResourceProfileCount}/{manager.BiomeResourceSpawnRuleCount} distributed T/S={manager.DistributedTreeCount}/{manager.DistributedStoneCount} candidates/eligible={manager.ResourceCandidateCellsEvaluated}/{manager.ResourceEligibleCandidateCount} reject static/terrain/spacing/conflict/cap={manager.ResourceStaticExclusionRejectedCount}/{manager.ResourceTerrainRejectedCount}/{manager.ResourceSpacingRejectedCount}/{manager.ResourceConflictRejectedCount}/{manager.ResourceChunkCapRejectedCount} fallback/duplicate/main={manager.ResourceFallbackProfileUseCount}/{manager.ResourceDuplicateDescriptorCount}/{manager.MainThreadResourcePlacementCount} workerMs total/max={manager.ResourcePlacementWorkerMilliseconds:0.0}/{manager.MaxResourcePlacementWorkerMilliseconds:0.0}";
        if(GameplayV3.Session.GameplaySessionV3.TryGetResourceEcologySession(out GameplayV3.Resources.Ecology.ResourceEcologySessionV3? ecology)&&ecology!=null){var d=ecology.Diagnostics;resourceLine+=$"\nEcology v={GameplayV3.Resources.Ecology.ResourceEcologySessionV3.AlgorithmVersion} time={ecology.SimulationTimeSeconds:0.0} chunks/states/active={ecology.ChunkStateCount}/{ecology.StateCount}/{ecology.ActiveStateCount} due/eligible/credit={ecology.DueCount}/{ecology.ActiveEligibleCount}/{ecology.PendingCredits:0.0} suppression/negative={ecology.SuppressionCount}/{ecology.NegativeCacheCount} unsupportedNonRenewable={d.UnsupportedResourceIgnoredCount} tick due/keys/attempt/candidates/spawn={d.DueEntriesProcessedThisTick}/{d.ActiveKeysAdvancedThisTick}/{d.RenewalAttemptsThisTick}/{d.CandidateCellsEvaluatedThisTick}/{d.SuccessfulSpawnsThisTick} T/SB/S={d.TreeRegrowthThisTick}/{d.TreeSeedBankRecoveryThisTick}/{d.StoneReplenishmentThisTick} forbidden scans/nodes/process/chunks={d.FullWorldEcologyScanCount}/{d.PerResourceTimerCount}/{d.PerResourceProcessCount}/{d.PerChunkEcologyProcessCount} cpu={d.EcologyTickMs:0.000}/{d.MaxEcologyTickMs:0.000}ms";}
        if(GameplayV3.Session.GameplaySessionV3.TryGetResourceEcologySession(out GameplayV3.Resources.Ecology.ResourceEcologySessionV3? safety)&&safety!=null){var d=safety.Diagnostics;resourceLine+=$"\nEcologySafety company/pressure={safety.CompanyShortageStateCount}/{safety.ActiveShortagePressureCount} eval/reach/nodes={d.CompaniesEvaluatedThisTick}/{d.ReachabilityCellsVisitedThisTick}/{d.ResourceNodesCheckedThisTick} shortage T N/L/C={d.TreeShortageNoneCount}/{d.TreeShortageLowCount}/{d.TreeShortageCriticalCount} S N/L/C={d.StoneShortageNoneCount}/{d.StoneShortageLowCount}/{d.StoneShortageCriticalCount} emergency/attempt/spawn={d.EmergencyDueSchedulesThisTick}/{d.ShortageAcceleratedAttemptsThisTick}/{d.ShortageAcceleratedSpawnsThisTick} exclusion q/p/t={d.RuntimeExclusionQueryCount}/{d.PersistentExclusionCount}/{d.TransientExclusionCount} stale={d.StaleNegativeEntryDiscardCount} forbidden scans/A*/generation={d.FullWorldShortageScanCount}/{d.ResourcePerNodePathRequestCount}/{d.ShortageTriggeredChunkGenerationCount} cpu={d.ShortageEvaluationMs:0.000}/{d.MaxShortageEvaluationMs:0.000}ms";}
        string workLine=$"WorkCoreInitialized={manager.WorkCoreInitialized} requests={manager.ActiveWorkRequestCount} assignments={manager.ActiveWorkAssignmentCount} reservations={manager.ActiveWorkReservationCount} movingToWork={manager.MovingToWorkCount} working={manager.WorkingMercenaryCount}";
        if(manager.TryGetMercenaryWorkSession(out MercenaryWorkSessionV3? workDiagnosticsSession)&&workDiagnosticsSession!=null){MercenaryWorkDiagnosticsV3 diagnostics=workDiagnosticsSession.Diagnostics;workLine+=$" completed/failed/cancelled/superseded={diagnostics.CompletedWorkCount}/{diagnostics.FailedWorkCount}/{diagnostics.CancelledWorkCount}/{diagnostics.SupersededWorkCount} cycles={diagnostics.CompletedCycleCount} lastFailure={diagnostics.LastFailureReason}";}
        string stockpileLine=$"Stockpile zones/cells/local={manager.StockpileZoneCount}/{manager.StockpileCellCount}/{manager.LocalCompanyZoneCount} mode={manager.StockpileDesignationMode} reserved={manager.ReservedStockpileCellCount} outside={manager.GroundAmountOutsideStockpile} stored W/S={manager.WoodAmountInStockpile}/{manager.StoneAmountInStockpile}";
        string constructionUiLine=$"ConstructionTrayOpen={manager.ConstructionTrayOpen} ActiveConstructionTool={manager.ActiveConstructionTool} StockpileDesignationMode={manager.StockpileDesignationMode} ConstructionUiInputBlockedByWorldMap={manager.ConstructionUiInputBlockedByWorldMap} LastConstructionUiAction={manager.LastConstructionUiAction}";
        string foodLine=$"Food defs=2 ration/potato ground={manager.GroundRationAmount}/{manager.GroundPotatoAmount} consumed={manager.ConsumedRationAmount}/{manager.ConsumedPotatoAmount} generatedPotato={manager.GeneratedPotatoAmount}";
        string farmingLine=$"Farm plots/cells={manager.FarmPlotCount}/{manager.FarmCellCount} empty/growing/mature={manager.EmptyFarmCellCount}/{manager.GrowingCropCount}/{manager.MatureCropCount} work/reserved={manager.ActiveFarmingWorkCount}/{manager.FarmReservationCount} growthTicks={manager.FarmGrowthTickCount} failure={manager.LastFarmFailureReason}";
        string centralJobLine=$"Jobs total/queued/active={manager.CentralJobCount}/{manager.QueuedCentralJobCount}/{manager.ActiveCentralJobCount} assigned/candidates={manager.CentralJobAssignmentCount}/{manager.CentralJobCandidateEvaluationCount} priorities={manager.SelectedMercenaryWorkPriorities} last={manager.LastCentralJobAction}";if(GameplayV3.Session.GameplaySessionV3.TryGetJobManager(out GameplayV3.Jobs.JobManagerV3? scaleJobs)&&scaleJobs!=null){var jd=scaleJobs.Diagnostics;centralJobLine+=$" gathering budget/materialized/indexed={jd.GatheringJobMaterializationBudget}/{jd.GatheringJobMaterializedCount}/{jd.GatheringCandidateIndexedCount} refill={jd.GatheringRefillRequested}:{jd.GatheringRefillProcessedLastFrame} total+/retired/direct={jd.GatheringJobMaterializedTotal}/{jd.GatheringJobRetiredTotal}/{jd.GatheringDirectWorkBypassCount} invalid/duplicate/fullScan={jd.InvalidGatheringCandidateCount}/{jd.DuplicateGatheringJobRejectedCount}/{jd.GatheringFullRegistryScanCount}";}
        string clockLine="Clock inactive";
        if(GameplayV3.Session.GameplaySessionV3.TryGetSimulationClock(out SimulationClockSessionV3? clock)&&clock!=null){SimulationClockSnapshotV3 value=clock.GetSnapshot();SimulationClockDiagnosticsV3 d=clock.Diagnostics;SimulationDeltaRoutingDiagnosticsV3 r=clock.RoutingDiagnostics;int clockHudCount=GetTree().GetNodesInGroup("simulation_clock_hud_v3").Count;clockLine=$"Clock {value.DayIndex}d {value.Hour:00}:{value.Minute:00} phase={value.DayPhase} scale={value.TimeScale}x paused={value.IsPaused} rev={value.Revision} advance/hour/day/phase={d.ClockAdvanceCallCount}/{d.HourBoundaryCount}/{d.DayBoundaryCount}/{d.PhaseBoundaryCount} delta real/scaled/world={r.LastRealDelta:0.000}/{r.LastScaledGameplayDelta:0.000}/{r.LastWorldSecondsAdvanced:0.00} steps/dup/paused/raw={r.SimulationStepFrameCount}/{r.DuplicateSimulationStepCount}/{r.PausedSimulationAdvanceViolationCount}/{r.RawDeltaBypassCount} pending N/F/E={r.NeedsPendingTickCredit:0.00}/{r.FarmingPendingTickCredit:0.00}/{r.EcologyPendingTickCredit:0.00} instances/hud={1}/{clockHudCount}";if(GameplayV3.Session.GameplaySessionV3.TryGetMercenarySchedule(out MercenaryScheduleSessionV3? schedules)&&schedules!=null){var sd=schedules.Diagnostics;clockLine+=$" schedule states/dirty/transitions/events={schedules.Count}/{schedules.DirtyCount}/{schedules.TransitionIndexEntryCount}/{sd.ScheduleEventCount} blocked/delayed/fullScan={sd.BlockedAutoAssignmentCount}/{sd.DelayedScheduleReleaseCount}/{sd.FullMercenaryScanCount}";}}
        if(GameplayV3.Session.GameplaySessionV3.TryGetFrontierSurvivalSession(out GameplayV3.Objectives.FrontierSurvivalSessionV3? frontier)&&frontier!=null){GameplayV3.Objectives.FrontierSurvivalSnapshotV3 objective=frontier.GetSnapshot();clockLine+=$" objective={objective.CompletedMilestoneCount}/{objective.TotalMilestoneCount} wood/stone={objective.Milestones[0].CurrentValue}/{objective.Milestones[1].CurrentValue} stock/bed/farm/room/hq={objective.Milestones[2].CurrentValue}/{objective.Milestones[3].CurrentValue}/{objective.Milestones[4].CurrentValue}/{objective.Milestones[5].CurrentValue}/{objective.Milestones[6].CurrentValue} survived={objective.SurvivedHours}h completed={objective.IsCompleted} events/fullScan={frontier.Diagnostics.ObjectiveEventCount}/{frontier.Diagnostics.ObjectiveFullWorldScanCount}";}
        string mercenaryInspectLine=$"InspectHud visible/mode/selected={manager.MercenaryInspectHudVisible}/{manager.MercenaryInspectHudMode}/{manager.MercenaryInspectHudSelectedCount} id={ShortId(manager.MercenaryInspectHudDisplayedId)} work={manager.MercenaryInspectHudWorkType}:{manager.MercenaryInspectHudWorkPhase} carry={manager.MercenaryInspectHudCarry} progress={manager.MercenaryInspectHudProgress:0.00} refresh={manager.MercenaryInspectHudRefreshCount}:{manager.MercenaryInspectHudLastRefreshReason} mapBlocked={manager.MercenaryInspectHudInputBlockedByWorldMap} rect={manager.MercenaryInspectHudGlobalRect} trayRect={manager.ConstructionUiGlobalRect} overlap={manager.MercenaryInspectHudOverlapsConstructionTray}";
        string mercenaryConditionLine=$"InspectCondition source={manager.MercenaryConditionDataSource} placeholder={manager.MercenaryConditionSnapshotIsPlaceholder} affectsGameplay={manager.MercenaryConditionAffectsGameplay} health/fullness/rest/morale={manager.MercenaryInspectHealth:0.00}/{manager.MercenaryInspectFullness:0.00}/{manager.MercenaryInspectRest:0.00}/{manager.MercenaryInspectMorale:0.00} action={manager.MercenaryInspectHudLastAction}";
        if(manager.TryGetNeedsSession(out MercenaryNeedsSessionV3? needs)&&needs!=null){manager.TryGetMercenarySession(out MercenarySessionV3? fatigueMercenaries);float average=needs.Fatigue.Count==0?0:fatigueMercenaries!=null?fatigueMercenaries.Registry.GetAllMercenaryIds().Select(needs.Fatigue.GetValue).DefaultIfEmpty().Average():0;float hungerAverage=needs.Hunger.Count==0?0:fatigueMercenaries!=null?fatigueMercenaries.Registry.GetAllMercenaryIds().Select(needs.Hunger.GetHunger).DefaultIfEmpty().Average():0;mercenaryConditionLine+=$" fatigueCount/avg={needs.Fatigue.Count}/{average:0.000} hungerCount/avg={needs.Hunger.Count}/{hungerAverage:0.000} hungerTicks={needs.HungerTickCount} assigned/reserved/resting={needs.Assignments.Count}/{needs.Reservations.Count}/{needs.ActiveRestCount} rest completed/cancelled={needs.Diagnostics.CompletedRestCount}/{needs.Diagnostics.CancelledRestCount} blockedWork={needs.Diagnostics.BlockedWorkCount}";}
        if(manager.TryGetResourceSession(out GameplayV3.Resources.ResourceSessionV3? hungerResources)&&hungerResources!=null){int groundRations=hungerResources.GroundStacks.GetTotalAmount(GameplayV3.Resources.ResourceTypeV3.Ration),reservedEating=hungerResources.AmountReservations.GetReservedAmount(hungerResources.InitialRationStackId,GameplayV3.Resources.ResourceAmountReservationPurposeV3.FoodConsumption);mercenaryConditionLine+=$" ration ground/reserved/consumed={groundRations}/{reservedEating}/{hungerResources.ConsumptionLedger.GetConsumedAmount(GameplayV3.Resources.ResourceTypeV3.Ration)}";}
        string constructionLine=$"Construction Blueprint/Structure/Blocked={manager.ConstructionBlueprintCount}/{manager.ConstructionStructureCount}/{manager.ConstructionBlockingCellCount} reservations={manager.ConstructionReservationCount} occupancyRev={manager.ConstructionOccupancyRevision}";
        constructionLine+=$" Demolition designated/working/reserved/done/failed={manager.DemolitionDesignationCount}/{manager.UnderDemolitionCount}/{manager.DemolitionReservationCount}/{manager.CompletedDemolitionCount}/{manager.FailedDemolitionCount} last={manager.LastDemolishedStructureId} worker={manager.LastDemolitionWorkerId} duration={manager.LastDemolitionDuration:0.00}s salvage={manager.LastSalvageTotalAmount} failure={manager.LastDemolitionFailureReason}";
        constructionLine+=$" Door init/total={manager.DoorRuntimeInitialized}/{manager.DoorCount} C/Og/O/Cg={manager.ClosedDoorCount}/{manager.OpeningDoorCount}/{manager.OpenDoorCount}/{manager.ClosingDoorCount} users={manager.DoorPassageUserCount} scheduled/stale={manager.DoorScheduledTransitionCount}/{manager.DoorStaleScheduleCount} A/R/T={manager.DoorAcquireCount}/{manager.DoorReleaseCount}/{manager.DoorTransitionCount} cpu={manager.DoorLastTickCpuMilliseconds:0.000}ms";
        constructionLine+=$" Floor complete/blueprint/mark/chunks={manager.CompletedFloorCellCount}/{manager.FloorBlueprintCount}/{manager.FloorDemolitionMarkCount}/{manager.FloorChunkIndexCount} dirty={manager.DirtyFloorChunkCount} moveRev={manager.FloorMovementRevision} nodes/process/scans={manager.FloorDiagnostics?.PerCellNodeCount??0}/{manager.FloorDiagnostics?.PerCellProcessCount??0}/{manager.FloorDiagnostics?.FullFloorRegistryScanCount??0}";
        constructionLine+=$" Rooms stable/cells/chunks/portals={manager.StableRoomCount}/{manager.RoomCellCount}/{manager.RoomChunkIndexCount}/{manager.RoomPortalCount} flood/max/commits={manager.RoomDiagnostics?.FloodCellsProcessedThisTick??0}/{manager.RoomDiagnostics?.MaxFloodCellsProcessedInTick??0}/{manager.RoomDiagnostics?.TopologyCommitsThisTick??0} metadata={manager.RoomDiagnostics?.MetadataCommitsThisTick??0} outdoor/tooLarge={manager.RoomDiagnostics?.OutdoorCandidateCount??0}/{manager.RoomDiagnostics?.TooLargeCandidateCount??0} forbidden scans/nodes/process/doorRebuild={manager.RoomDiagnostics?.FullWorldRoomScanCount??0}/{manager.RoomDiagnostics?.PerCellRoomNodeCount??0}/{manager.RoomDiagnostics?.PerRoomProcessCount??0}/{manager.RoomDiagnostics?.DoorStateTriggeredRebuildCount??0}";
        string baseAreaLine="BaseArea session=none";
        if(GameplayV3.Session.GameplaySessionV3.TryGetBaseAreaSession(out GameplayV3.Bases.BaseAreaSessionV3? baseAreas)&&baseAreas!=null){GameplayV3.Bases.BaseAreaDiagnosticsV3 d=baseAreas.Diagnostics;int local=string.IsNullOrWhiteSpace(manager.LocalCompanyId)?0:baseAreas.Areas.GetForCompany(manager.LocalCompanyId).Count;IReadOnlyList<GameplayV3.Bases.BaseSpatialSourceV3> baseSources=baseAreas.Sources.GetAll();baseAreaLine=$"BaseArea total/local/sources={baseAreas.Areas.Count}/{local}/{baseSources.Count} anchors/attachments={baseSources.Count(s=>s.IsAnchor)}/{baseSources.Count(s=>!s.IsAnchor)} dirty={baseAreas.DirtyCompanyCount} cells/chunks={baseAreas.Areas.CellIndexEntryCount}/{baseAreas.Areas.ChunkIndexCount} created/updated/merge/split/removed/remap={d.BaseCreatedCount}/{d.BaseUpdatedCount}/{d.BaseMergedCount}/{d.BaseSplitCount}/{d.BaseRemovedCount}/{d.BaseRemapCount} sync={d.InitialSyncProcessed}/{d.InitialSyncRemaining} pair/attach/writes={d.AnchorPairChecksThisTick}/{d.AttachmentChecksThisTick}/{d.CellIndexWritesThisTick} forbidden world/company/cartesian/chunkgen/nodes/process={d.FullWorldBaseScanCount}/{d.FullCompanySourceScanCount}/{d.AnchorCartesianPairCount}/{d.BaseTriggeredChunkGenerationCount}/{d.PerBaseNodeCount}/{d.PerBaseProcessCount} cpu={d.RuntimeTickMs:0.000}/{d.MaxRuntimeMs:0.000}ms";}
        if(GameplayV3.Session.GameplaySessionV3.TryGetBaseRoleSession(out GameplayV3.Bases.BaseRoleSessionV3? baseRoles)&&baseRoles!=null)baseAreaLine+=$" BaseRoles state/hq/base/outpost={baseRoles.Count}/{baseRoles.HeadquartersCount}/{baseRoles.BaseCount}/{baseRoles.OutpostCount} companiesWithoutHq={baseRoles.CompanyWithoutHeadquartersCount} dirty/events={baseRoles.DirtyRoleCount}/{baseRoles.RecentEventCount}";
        if(GameplayV3.Session.GameplaySessionV3.TryGetFacilityAffiliationSession(out GameplayV3.Bases.FacilityAffiliationSessionV3? affiliations)&&affiliations!=null){GameplayV3.Bases.FacilityAffiliationDiagnosticsV3 d=affiliations.Diagnostics;baseAreaLine+=$" FacilityAffiliationCount={affiliations.Count} UnaffiliatedFacilityCount={affiliations.GetFacilityStateCount(GameplayV3.Bases.FacilityAffiliationStateV3.Unaffiliated)} AmbiguousFacilityCount={affiliations.GetFacilityStateCount(GameplayV3.Bases.FacilityAffiliationStateV3.Ambiguous)} ActivityRangeCount={affiliations.ActivityRangeCount} DirtyFacilityCount={affiliations.DirtyFacilityCount} ActivityQueryCount={d.ActivityQueryCount} ActivityQueryCandidateCount={d.ActivityChunkCandidateCount} ActivityRuntimeMs={d.LastTickMilliseconds:0.000} MaxActivityRuntimeMs={d.MaxTickMilliseconds:0.000} forbidden FullWorldFacilityScanCount={d.FullWorldFacilityScanCount} FullWorldActivityCellBuildCount={d.FullWorldActivityCellBuildCount} PerFacilityNodeCount={d.PerFacilityNodeCount} PerBaseActivityNodeCount={d.PerBaseActivityNodeCount} ActivityRangeTriggeredChunkGenerationCount={d.ActivityRangeTriggeredChunkGenerationCount}";}
        if(GameplayV3.Session.GameplaySessionV3.TryGetMercenaryBaseAffiliationSession(out GameplayV3.Bases.MercenaryBaseAffiliationSessionV3? mercenaryBases)&&mercenaryBases!=null){GameplayV3.Bases.MercenaryBaseAffiliationDiagnosticsV3 d=mercenaryBases.Diagnostics;baseAreaLine+=$" AssignedMercenaryCount={mercenaryBases.GetStateCount(GameplayV3.Bases.MercenaryBaseAffiliationStateV3.Assigned)} UnassignedMercenaryCount={mercenaryBases.GetStateCount(GameplayV3.Bases.MercenaryBaseAffiliationStateV3.Unassigned)} PendingReassignmentCount={mercenaryBases.GetStateCount(GameplayV3.Bases.MercenaryBaseAffiliationStateV3.PendingReassignment)+mercenaryBases.GetStateCount(GameplayV3.Bases.MercenaryBaseAffiliationStateV3.BaseRemoved)} DirtyMercenaryCount={mercenaryBases.DirtyMercenaryCount} changed/remap/stable={d.ChangedCount}/{d.RemappedCount}/{d.StabilityKeepCount} forbidden MercenaryBaseCartesianCount={d.MercenaryBaseCartesianComparisonCount} perMercNodes/process={d.PerMercenaryBaseNodeCount}/{d.PerMercenaryBaseProcessCount}";}
        if(GameplayV3.Session.GameplaySessionV3.TryGetJobActivityRangePolicy(out GameplayV3.Jobs.JobActivityRangePolicyV3? jobRange)&&jobRange!=null){GameplayV3.Jobs.JobActivityRangeDiagnosticsV3 d=jobRange.Diagnostics;baseAreaLine+=$" ActivityRangeRejectCount={d.RejectCount} DirectOrderOverrideCount={d.DirectOverrideCount} NeedsOverrideCount={d.NeedsOverrideCount} CrossBaseHaulingRejectCount={d.RejectedCrossBaseHaulingCount} DirtyJobSourceCount={d.DirtyJobSourceCount} last={d.LastReason} forbidden MercenaryJobCartesianCount={d.MercenaryJobCartesianScanCount}";}
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
            cacheLine = $"cache: data={cache.CachedChunkDataCount}/{cache.MaxCachedChunkDataCount} currentGenerating={streamManager.CurrentGeneratingChunkCount} started/completed/cancelled={streamManager.ChunkGenerationStartedTotal}/{streamManager.ChunkGenerationCompletedTotal}/{streamManager.ChunkGenerationCancelledTotal} orphan={streamManager.OrphanGeneratingEntryCount} cells~={cache.ApproxCachedCellCount} hit={cache.CacheHitCount} miss={cache.CacheMissCount} evict={cache.CacheEvictionCount} dirty={cache.DirtyCount}";
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
            ApplyLabelText(
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
                $"{mercenaryInspectLine}\n{mercenaryConditionLine}\n" +
                $"{mercenaryPathLine}\n" +
                $"{resourceLine}\n" +
                $"{workLine}\n" +
                $"{stockpileLine}\n{foodLine}\n{farmingLine}\n{centralJobLine}\n{clockLine}\n" +
                $"{constructionUiLine}\n" +
                $"{constructionLine}\n" +
                $"{baseAreaLine}\n" +
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
                $"{message}");
            return;
        }

        ApplyLabelText(
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
            $"{mercenaryInspectLine}\n{mercenaryConditionLine}\n" +
            $"{mercenaryPathLine}\n" +
            $"{resourceLine}\n" +
            $"{workLine}\n" +
            $"{stockpileLine}\n{foodLine}\n{farmingLine}\n{centralJobLine}\n" +
            $"{constructionUiLine}\n" +
            $"{constructionLine}\n" +
            $"{baseAreaLine}\n" +
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
            $"{message}");
    }

    private void ApplyLabelText(string text)
    {
        if (_label == null || _label.Text == text)
        {
            DebugHudSkippedUnchangedCount++;
            return;
        }

        _label.Text = text;
        DebugHudTextAssignmentCount++;
        DebugHudLastTextLength = text.Length;
        DebugHudMaxTextLength = Math.Max(DebugHudMaxTextLength, text.Length);
        double elapsedMs = (Stopwatch.GetTimestamp() - _refreshStartTicks) * 1000.0 / Stopwatch.Frequency;
        DebugHudLastBuildMs = elapsedMs;
        DebugHudMaxBuildMs = Math.Max(DebugHudMaxBuildMs, elapsedMs);
    }

    public void PrintDiagnostics()
    {
        GD.Print($"[WorldV2DebugHud] refresh={DebugHudRefreshCount} hiddenSkip={DebugHudSkippedHiddenCount} unchangedSkip={DebugHudSkippedUnchangedCount} lastBuildMs={DebugHudLastBuildMs:0.000} maxBuildMs={DebugHudMaxBuildMs:0.000} lastTextLength={DebugHudLastTextLength} maxTextLength={DebugHudMaxTextLength} resourceRows={DebugHudResourceRowsWritten}/{DebugHudResourceRowsOmitted} fullRegistryScan={DebugHudResourceFullRegistryScanCount} textAssignments={DebugHudTextAssignmentCount}");
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
