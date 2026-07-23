using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GameplayV3.Control;
using GameplayV3.Control.Runtime;
using GameplayV3.Mercenary;
using GameplayV3.Mercenary.Runtime;
using GameplayV3.Mercenary.UI;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using GameplayV3.Resources.Runtime;
using GameplayV3.Resources.Ecology;
using GameplayV3.Resources.Ecology.Runtime;
using GameplayV3.Company;
using GameplayV3.Work;
using GameplayV3.Work.Runtime;
using GameplayV3.Stockpile;
using GameplayV3.Stockpile.Runtime;
using GameplayV3.Construction;
using GameplayV3.Construction.Runtime;
using GameplayV3.Needs;
using GameplayV3.Needs.Runtime;
using GameplayV3.Session;
using GameplayV3.Farming;
using GameplayV3.Farming.Runtime;
using GameplayV3.Jobs;
using GameplayV3.Jobs.Runtime;
using GameplayV3.Rooms;
using GameplayV3.Rooms.Runtime;
using GameplayV3.Bases;
using GameplayV3.Bases.Runtime;
using GameplayV3.Time;
using GameplayV3.Time.Runtime;
using GameplayV3.Time.UI;
using GameplayV3.Bases.UI;
using GameplayV3.Objectives;
using GameplayV3.Objectives.UI;
using GameplayV3.Production.UI;
using GameplayV3.Production;
using GameplayV3.Production.Runtime;
using GameplayV3.Selection;
using GameplayV3.Equipment;

namespace WorldV2;

public partial class WorldV2Root : Node2D
{
	private WorldManagerV2? _worldManager;
	private WorldV2GridRenderer? _gridRenderer;
	private WorldV2BuildManager? _buildManager;
	private WorldV2CameraController? _camera;
	private WorldStreamManagerV2? _streamManager;
	private WorldV2LoadingOverlay? _loadingOverlay;
	private WorldMapOverlayV2? _worldMapOverlay;
	private Node2D? _mercenariesContainer;
	private Node2D? _resourceNodesContainer;
	private Node2D? _groundResourcesContainer;
	private Node2D? _stockpileContainer;
	private readonly MercenaryViewRegistryV3 _mercenaryViewRegistry = new();
	private readonly MercenaryMaterializationCoordinatorV3 _mercenaryMaterializationCoordinator = new();
	private MercenaryInputControllerV3? _mercenaryInputController;
	private MercenaryMovementRuntimeV3? _mercenaryMovementRuntime;
	private DoorPassageRuntimeV3? _doorPassageRuntime;
	private MercenaryDragSelectionOverlayV3? _mercenaryDragOverlay;
	private MercenaryCommandMarkerV3? _mercenaryCommandMarker;
	private MercenaryWorkRuntimeV3? _mercenaryWorkRuntime;
	private readonly ResourceNodeViewRegistryV3 _resourceNodeViews=new();
	private readonly GroundResourceStackViewRegistryV3 _groundStackViews=new();
	private ResourceEcologyRuntimeV3? _resourceEcologyRuntime;
	private readonly Dictionary<Vector2I,ChunkDataV2> _ecologyChunks=new();
	private StockpileOverlayV3? _stockpileOverlay;
	private StockpileDesignationControllerV3? _stockpileDesignation;
	private ConstructionUiV3? _constructionUi;
	private ConstructionWorldOverlayV3? _constructionOverlay;
	private ConstructionPlacementControllerV3? _constructionPlacement;
	private ConstructionWorkCoordinatorV3? _constructionWork;
	private DemolitionDesignationControllerV3? _demolitionDesignation;
	private DemolitionWorkCoordinatorV3? _demolitionWork;
	private FloorConstructionRuntimeV3? _floorRuntime;
	private RoomRuntimeV3? _roomRuntime;
	private RoomOverlayV3? _roomOverlay;
	private BaseAreaRuntimeV3? _baseAreaRuntime;
	private BaseAreaOverlayV3? _baseAreaOverlay;
	private MercenaryInspectHudV3? _mercenaryInspectHud;
	private BaseManagementPanelV3? _baseManagementPanel;
	private MercenarySchedulePanelV3? _mercenarySchedulePanel;
	private FrontierSurvivalPanelV3? _frontierSurvivalPanel;
	private ProductionPanelV3? _productionPanel;
	private ProductionWorkCoordinatorV3? _productionWork;
	private SelectionInspectPanelV3? _selectionInspectPanel;
	private WorldHoverTooltipV3? _worldHoverTooltip;
	private MercenaryNeedsRuntimeV3? _mercenaryNeedsRuntime;
	private RestWorkCoordinatorV3? _restWork;
	private EatingWorkCoordinatorV3? _eatingWork;
	private RestAssignmentOverlayV3? _restAssignmentOverlay;
	private FarmWorldOverlayV3? _farmOverlay;
	private FarmDesignationControllerV3? _farmDesignation;
	private FarmGrowthRuntimeV3? _farmGrowthRuntime;
	private FarmingWorkCoordinatorV3? _farmingWork;
	private JobManagerRuntimeV3? _jobManagerRuntime;
	private IMercenaryNavigationWorldQueryV3? _navigationQuery;
	private MercenaryWorkPriorityPanelV3? _workPriorityPanel;
	private string _pendingBedAssignmentMercenaryId=string.Empty;
	private bool _cameraInputWasLocked;
	private SimulationClockSessionV3? _simulationClock;
	private SimulationClockHudV3? _simulationClockHud;
	private CanvasModulate? _dayNightCanvasModulate;
	private DayNightVisualRuntimeV3? _dayNightVisualRuntime;
	private long _lastSimulationStepFrame=-1;

	public override void _EnterTree()
	{
		// Test-only preset selection for the bounded activity-range runtime fixture.
		// It is intentionally evaluated before WorldManagerV2._Ready consumes the request.
		string presetName=OS.GetEnvironment("ACTIVITY_RANGE_FIXTURE_PRESET");
		if(!string.IsNullOrWhiteSpace(presetName)
			&& Enum.TryParse(presetName,true,out WorldMapSizePresetV2 preset))
		{
			int seed=205904+(int)preset;
			WorldGenerationSessionV2.SetPendingRequest(new WorldGenerationRequestV2(
				preset,seed,WorldPlanVersionV2.V3,
				WorldMapSizeDefinitionV2.FromPreset(preset).CenterCell,
				$"activity_range_fixture_{preset.ToString().ToLowerInvariant()}"));
		}
	}

	public override void _Ready()
	{
		_worldManager = GetNodeOrNull<WorldManagerV2>("WorldManagerV2");
		_gridRenderer = GetNodeOrNull<WorldV2GridRenderer>("GridLayer");
		_buildManager = GetNodeOrNull<WorldV2BuildManager>("BuildingLayer");
		_camera = GetNodeOrNull<WorldV2CameraController>("Camera2D");
		_streamManager = GetNodeOrNull<WorldStreamManagerV2>("WorldStreamManagerV2");
		if(_streamManager!=null){_streamManager.ChunkDataReady+=OnChunkDataReady;_streamManager.ChunkRendererAttached+=OnChunkRendererAttached;_streamManager.ChunkRendererDetached+=OnChunkRendererDetached;}
		_mercenariesContainer = GetNodeOrNull<Node2D>("GameplayEntitiesV3/MercenariesV3");
		_resourceNodesContainer = GetNodeOrNull<Node2D>("GameplayEntitiesV3/ResourceNodesV3");
		_groundResourcesContainer = GetNodeOrNull<Node2D>("GameplayEntitiesV3/GroundResourcesV3");
		_stockpileContainer = GetNodeOrNull<Node2D>("GameplayEntitiesV3/StockpileZonesV3");
		if(_mercenariesContainer!=null)_mercenariesContainer.ZIndex=2;
		if(_resourceNodesContainer!=null)_resourceNodesContainer.ZIndex=2;
		if(_groundResourcesContainer!=null)_groundResourcesContainer.ZIndex=2;
		CreateLoadingOverlay();
		CreateWorldMapOverlay();
		CreateSimulationClockHud();
		CreateDayNightVisual();
		BindCurrentSimulationClock();
		GameplaySessionV3.SessionBegan+=OnGameplaySessionBeganForClock;
		MaterializeLocalMercenaries();
		MaterializeResources();
		InitializeMercenaryControlRuntime();
		if(OS.GetEnvironment("BASE_AREA_FIXTURE")=="1")_ = RunBaseAreaFixture();
		if(OS.GetEnvironment("BASE_ROLE_FIXTURE")=="1")_ = RunBaseRoleFixture();
		if(OS.GetEnvironment("BASE_AREA_LIFECYCLE_FIXTURE")=="1")_ = RunBaseAreaLifecycleFixture();
		if(OS.GetEnvironment("BASE_AREA_PERF_FIXTURE")=="1")_ = RunBaseAreaPerformanceFixture();
		if(OS.GetEnvironment("FACILITY_AFFILIATION_FIXTURE")=="1")_ = RunFacilityAffiliationFixture();
		if(OS.GetEnvironment("MERCENARY_BASE_AFFILIATION_FIXTURE")=="1")_ = RunMercenaryBaseAffiliationFixture();
		if(OS.GetEnvironment("JOB_ACTIVITY_RANGE_FIXTURE")=="1")_ = RunJobActivityRangeFixture();
		if(OS.GetEnvironment("ACTIVITY_RANGE_VERIFICATION_FIXTURE")=="1")_ = RunJobActivityRangeFixture();
		if(OS.GetEnvironment("RESOURCE_ECOLOGY_FIXTURE")=="1")_ = RunResourceEcologyFixture();
		if(OS.GetEnvironment("RESOURCE_ECOLOGY_SAFETY_FIXTURE")=="1")_ = RunResourceEcologySafetyFixture();
		if(OS.GetEnvironment("RESOURCE_ECOLOGY_LIFECYCLE_FIXTURE")=="1")_ = RunResourceEcologyLifecycleFixture();
		if(OS.GetEnvironment("RESOURCE_ECOLOGY_NEW_SESSION_FIXTURE")=="1")_ = RunResourceEcologyNewSessionFixture();
		if(OS.GetEnvironment("BIOME_RESOURCE_LIFECYCLE_FIXTURE")=="1")_ = RunResourceLifecycleFixture();

		if (_camera != null)
		{
			CenterCameraOnInitialDeploymentOrPlayerStart();
		}
	}

	public override void _Process(double delta)
	{
		bool loading = _streamManager != null && !_streamManager.IsInitialLoadingComplete;
		SetCameraInputLocked(loading || IsWorldMapOverlayOpen());
		_loadingOverlay?.Refresh(_streamManager);
		_simulationClockHud?.SetGameplayReady(!loading);
		_simulationClockHud?.AdvanceDisplay(delta);
		_dayNightVisualRuntime?.AdvanceDisplay(delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		if(_simulationClock==null||!GameplaySessionV3.IsCurrentSimulationClock(_simulationClock))return;
		long frame=(long)Engine.GetPhysicsFrames();SimulationDeltaRoutingDiagnosticsV3 diagnostics=_simulationClock.RoutingDiagnostics;
		if(frame==_lastSimulationStepFrame){diagnostics.DuplicateSimulationStepCount++;return;}
		_lastSimulationStepFrame=frame;
		SimulationClockAdvanceResultV3 result=_simulationClock.AdvanceFrame(delta,frame);double simulationDelta=result.ScaledGameplayDeltaSeconds;
		diagnostics.SimulationStepFrameCount++;diagnostics.LastRealDelta=result.AcceptedRealDeltaSeconds;diagnostics.LastScaledGameplayDelta=simulationDelta;diagnostics.LastWorldSecondsAdvanced=result.WorldSecondsAdvanced;
		if(result.WasPaused&&simulationDelta>0)diagnostics.PausedSimulationAdvanceViolationCount++;
		if(simulationDelta>0)
		{
			if(_mercenaryMovementRuntime!=null){_mercenaryMovementRuntime.AdvanceSimulation(simulationDelta);diagnostics.MovementAdvanceCount++;}
			if(_mercenaryWorkRuntime!=null){_mercenaryWorkRuntime.AdvanceSimulation(simulationDelta);diagnostics.WorkAdvanceCount++;}
			if(_mercenaryNeedsRuntime!=null){_mercenaryNeedsRuntime.AdvanceSimulation(simulationDelta);diagnostics.NeedsAdvanceCount++;}
			if(_farmGrowthRuntime!=null){_farmGrowthRuntime.AdvanceSimulation(simulationDelta);diagnostics.FarmingAdvanceCount++;}
			if(_resourceEcologyRuntime!=null){_resourceEcologyRuntime.AdvanceSimulation(simulationDelta);diagnostics.EcologyAdvanceCount++;}
		}
		GameplaySessionV3.FlushFrontierSurvivalDirty();
		diagnostics.NeedsPendingTickCredit=_mercenaryNeedsRuntime?.PendingTickCredit??0;diagnostics.FarmingPendingTickCredit=_farmGrowthRuntime?.PendingTickCredit??0;diagnostics.EcologyPendingTickCredit=GameplaySessionV3.TryGetResourceEcologySession(out ResourceEcologySessionV3? ecology)&&ecology!=null?ecology.PendingTickCredit:0;
	}

	public override void _ExitTree()
	{
		GameplaySessionV3.SessionBegan-=OnGameplaySessionBeganForClock;
		if (_mercenaryInspectHud != null)
		{
			_mercenaryInspectHud.ScheduleRequested -= OnScheduleRequested;
		}
		if (_baseManagementPanel != null)
		{
			_baseManagementPanel.PanelOpened -= OnBaseManagementOpened;
		}
		if (_productionPanel != null)
		{
			_productionPanel.PanelOpened -= OnProductionPanelOpened;
		}
		if (_constructionUi != null)
		{
			_constructionUi.TrayVisibilityChanged -= OnConstructionTrayVisibilityChanged;
		}
		_dayNightVisualRuntime?.Dispose();
		_dayNightVisualRuntime=null;
		_dayNightCanvasModulate=null;
		if(_streamManager!=null){_streamManager.ChunkDataReady-=OnChunkDataReady;_streamManager.ChunkRendererAttached-=OnChunkRendererAttached;_streamManager.ChunkRendererDetached-=OnChunkRendererDetached;}
		_mercenaryViewRegistry.Clear();
		_resourceNodeViews.Clear();
		_groundStackViews.Clear();
	}

	private void CreateSimulationClockHud()
	{
		CanvasLayer? canvas=GetNodeOrNull<CanvasLayer>("CanvasLayer");if(canvas==null)return;
		_simulationClockHud=canvas.GetNodeOrNull<SimulationClockHudV3>("SimulationClockHudV3");
		if(_simulationClockHud==null){_simulationClockHud=new SimulationClockHudV3();canvas.AddChild(_simulationClockHud);}
		_simulationClockHud.SetGameplayReady(false);
	}

	private void CreateDayNightVisual()
	{
		_dayNightCanvasModulate=GetNodeOrNull<CanvasModulate>("DayNightCanvasModulate");
		if(_dayNightCanvasModulate==null)
		{
			_dayNightCanvasModulate=new CanvasModulate{Name="DayNightCanvasModulate",Color=Colors.White};
			AddChild(_dayNightCanvasModulate);
		}
		_dayNightVisualRuntime??=new DayNightVisualRuntimeV3();
	}

	private void BindCurrentSimulationClock()
	{
		_simulationClock=GameplaySessionV3.GetSimulationClock();
		_simulationClockHud?.Bind(_simulationClock);
		if(_dayNightVisualRuntime!=null&&_dayNightCanvasModulate!=null)_dayNightVisualRuntime.Bind(_simulationClock,_dayNightCanvasModulate);
	}
	private void OnGameplaySessionBeganForClock(){BindCurrentSimulationClock();}

	private void OnScheduleRequested(string mercenaryId)
	{
		_constructionUi?.CloseTray("schedule");
		_baseManagementPanel?.ClosePanel("Schedule");
		_productionPanel?.Close();
		_mercenarySchedulePanel?.ToggleForSelectedMercenary(mercenaryId);
	}

	private void OnBaseManagementOpened()
	{
		_constructionUi?.CloseTray("base-management");
		_mercenarySchedulePanel?.ClosePanel("BaseManagement");
		_productionPanel?.Close();
	}

	private void OnProductionPanelOpened()
	{
		_constructionUi?.CloseTray("production");
		_baseManagementPanel?.ClosePanel("Production");
		_mercenarySchedulePanel?.ClosePanel("Production");
	}

	private void OnChunkDataReady(ChunkDataV2 chunkData)
	{
		if(_worldManager==null||_gridRenderer==null||_resourceNodesContainer==null||!_worldManager.TryGetResourceSession(out ResourceSessionV3? resources)||resources==null)return;
		_worldManager.RegisterChunkNaturalResources(chunkData);
		_ecologyChunks[chunkData.GlobalChunkCoord]=chunkData;
		_resourceEcologyRuntime?.OnChunkDataReady(chunkData);
	}

	private void OnChunkRendererAttached(ChunkDataV2 chunkData)
	{
		if(_worldManager==null||_gridRenderer==null||_resourceNodesContainer==null||!_worldManager.TryGetResourceSession(out ResourceSessionV3? resources)||resources==null)return;
		_resourceEcologyRuntime?.OnChunkAttached(chunkData);
		List<string> ids=new(resources.Nodes.GetNodeIdsInChunk(chunkData.GlobalChunkCoord));
		ResourceMaterializationCoordinatorV3.MaterializeNodes(ids,resources,_resourceNodesContainer,_gridRenderer,_resourceNodeViews);
		_jobManagerRuntime?.OnResourceChunkAttached(chunkData.GlobalChunkCoord);
		UpdateResourceViewDiagnostics();
	}

	private void OnChunkRendererDetached(Vector2I chunkCoord)
	{
		_resourceEcologyRuntime?.OnChunkDetached(chunkCoord);
		_jobManagerRuntime?.OnResourceChunkDetached(chunkCoord);
		foreach(string id in _resourceNodeViews.GetIds())if(_worldManager?.TryGetResourceSession(out ResourceSessionV3? resources)==true&&resources?.Nodes.TryGet(id,out ResourceNodeStateV3? state)==true&&state!=null&&WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(state.Cell.Value)==chunkCoord)_resourceNodeViews.TryRemove(id);
		UpdateResourceViewDiagnostics();
	}

	private async Task RunResourceLifecycleFixture()
	{
		for(int i=0;i<180;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(_worldManager==null)return;
		if(!_worldManager.TryGetResourceSession(out ResourceSessionV3? resources)||resources==null)return;var baseline=resources.Nodes.GetAllNodeIds();_worldManager.RegenerateVisibleChunks(false);for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);_worldManager.RegenerateVisibleChunks(true);for(int i=0;i<240;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);bool preserved=true;foreach(string id in baseline)if(!resources.Nodes.Contains(id)){preserved=false;break;}GD.Print($"[ResourceDistributionV3] lifecycle F11/F12 preserved={preserved} baseline={baseline.Count} current={resources.Nodes.Count} views={_resourceNodeViews.Count} duplicate={_worldManager.ResourceDuplicateDescriptorCount} mainThreadPlacement={_worldManager.MainThreadResourcePlacementCount}");
	}

	private async Task RunBaseAreaFixture()
	{
		for(int i=0;i<600&&_baseAreaRuntime==null;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);if(_baseAreaRuntime==null||_worldManager==null||!GameplaySessionV3.TryGetBaseAreaSession(out BaseAreaSessionV3? bases)||bases==null||!GameplaySessionV3.TryGetConstructionSession(out ConstructionSessionV3? construction)||construction==null||!GameplaySessionV3.TryGetStockpileSession(out StockpileSessionV3? stockpiles)||stockpiles==null){GD.PushError("[BaseAreaV3] fixture setup failed");GetTree().Quit(2);return;}string company=_worldManager.LocalCompanyId;Vector2I origin=_worldManager.PlayerStartGlobalCell;bool startEmpty=bases.Areas.Count==0;StructureStateV3 MakeBed(string id,Vector2I cell){construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.BasicBedId,out var definition);ResolvedStructureFootprintV3 footprint=StructureFootprintResolverV3.Resolve(definition!,new(cell),StructureOrientationV3.Deg0);return new(id,company,StructureDefinitionCatalogV3.BasicBedId,new(cell),StructureOrientationV3.Deg0,footprint.Cells,Array.Empty<StructureMaterialRequirementV3>(),false,DateTime.UtcNow,StructureMovementKindV3.NonBlocking);}StructureStateV3 left=MakeBed(StructureIdFactoryV3.Create(),origin);construction.Structures.TryRegister(left,construction.Blueprints,_worldManager.WorldBounds,out _);for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool first=bases.Areas.GetForCompany(company).Count==1;string firstId=first?bases.Areas.GetForCompany(company)[0].BaseAreaId:string.Empty;GlobalCellCoord[] zoneCells={new(origin+new Vector2I(2,0)),new(origin+new Vector2I(3,0)),new(origin+new Vector2I(2,1))};stockpiles.Zones.TryCreateZone(company,zoneCells,_worldManager.WorldBounds,out _,out _);for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool expanded=bases.Areas.GetForCompany(company).Count==1&&bases.Areas.GetForCompany(company)[0].MemberSourceCount>=2;StructureStateV3 right=MakeBed(StructureIdFactoryV3.Create(),origin+new Vector2I(16,0));construction.Structures.TryRegister(right,construction.Blueprints,_worldManager.WorldBounds,out _);for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);int separated=bases.Areas.GetForCompany(company).Count;StructureStateV3 bridge=MakeBed(StructureIdFactoryV3.Create(),origin+new Vector2I(8,0));construction.Structures.TryRegister(bridge,construction.Blueprints,_worldManager.WorldBounds,out _);for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool merged=bases.Areas.GetForCompany(company).Count==1&&bases.Areas.GetForCompany(company)[0].BaseAreaId==firstId;construction.Structures.TryRemove(bridge.StructureId,out _);for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool split=bases.Areas.GetForCompany(company).Count==2;bool removedLeft=construction.Structures.TryRemove(left.StructureId,out _),removedRight=construction.Structures.TryRemove(right.StructureId,out _);bool zonesRemoved=true;foreach(string id in stockpiles.Zones.GetAllZoneIds())zonesRemoved&=stockpiles.Zones.TryRemoveZone(id,company,out _);for(int i=0;i<12;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool removed=bases.Areas.GetForCompany(company).Count==0;string remaining=string.Join(',',bases.Sources.GetByCompany(company).Where(s=>s.IsAnchor).Select(s=>s.SourceId));var d=bases.Diagnostics;bool pass=startEmpty&&first&&expanded&&separated==2&&merged&&split&&removed&&d.FullWorldBaseScanCount==0&&d.AnchorCartesianPairCount==0&&(_streamManager?.MainThreadChunkGenerationCount??0)==0;GD.Print($"[BaseAreaV3] fixture PASS={pass} startEmpty={startEmpty} first={first} expanded={expanded} separated={separated} merged={merged} split={split} removed={removed} removals={removedLeft}/{removedRight}/{zonesRemoved} remainingAnchors={remaining} pending={_baseAreaRuntime.PendingEventCount} created/merge/split/removed={d.BaseCreatedCount}/{d.BaseMergedCount}/{d.BaseSplitCount}/{d.BaseRemovedCount} scans/cartesian/chunkgen={d.FullWorldBaseScanCount}/{d.AnchorCartesianPairCount}/{d.BaseTriggeredChunkGenerationCount} mainThreadChunkGeneration={_streamManager?.MainThreadChunkGenerationCount??-1}");GetTree().Quit(pass?0:3);
	}

	private async Task RunBaseRoleFixture()
	{
		for(int i=0;i<600&&_baseAreaRuntime==null;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
		if(_baseAreaRuntime==null||_worldManager==null||!GameplaySessionV3.TryGetBaseRoleSession(out BaseRoleSessionV3? before)||before==null){GD.PushError("[BaseRoleV3] fixture setup failed");GetTree().Quit(2);return;}
		try
		{
			if(!BaseRoleSelfCheckV3.TryValidate(out string reason))throw new InvalidOperationException(reason);
			if(!BaseManagementUiSelfCheckV3.TryValidate(out string uiReason))throw new InvalidOperationException(uiReason);
			string small=BaseRoleSelfCheckV3.RunSmallFixture();
			bool disposedSubscription=BaseRoleSelfCheckV3.ValidateDisposedSubscription();
			Node? entities=GetNodeOrNull("GameplayEntitiesV3");int overlayBefore=entities?.GetChildren().OfType<BaseAreaOverlayV3>().Count()??0;int panelBefore=GetNodeOrNull<CanvasLayer>("CanvasLayer")?.GetChildren().OfType<BaseManagementPanelV3>().Count()??0;string selectedBefore=_baseManagementPanel?.SelectedManagementBaseId??string.Empty;
			_worldManager.RegenerateVisibleChunks(false);for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
			_worldManager.RegenerateVisibleChunks(true);for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
			bool f11f12=GameplaySessionV3.TryGetBaseRoleSession(out BaseRoleSessionV3? after)&&ReferenceEquals(before,after)&&(entities?.GetChildren().OfType<BaseAreaOverlayV3>().Count()??0)==overlayBefore;
			bool panelF11F12=panelBefore==1&&((GetNodeOrNull<CanvasLayer>("CanvasLayer")?.GetChildren().OfType<BaseManagementPanelV3>().Count()??0)==1)&&(_baseManagementPanel?.SelectedManagementBaseId??string.Empty)==selectedBefore;
			GameplaySessionV3.BeginNewSession();for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
			bool newSession=before.IsDisposed&&GameplaySessionV3.TryGetBaseRoleSession(out BaseRoleSessionV3? replacement)&&replacement!=null&&!ReferenceEquals(before,replacement)&&replacement.Count==0&&replacement.HeadquartersCount==0&&replacement.DirtyRoleCount==0;
			bool panelNewSession=(_baseManagementPanel?.BaseManagementPanelNodeCount??0)==1&&(_baseManagementPanel?.BaseManagementRowCount??-1)==0&&string.IsNullOrEmpty(_baseManagementPanel?.SelectedManagementBaseId);
			bool pass=disposedSubscription&&f11f12&&panelF11F12&&newSession&&panelNewSession&&(_streamManager?.MainThreadChunkGenerationCount??0)==0;
			GD.Print($"[BaseManagementUiV3] fixture PASS={pass} pure={uiReason==string.Empty} panelF11F12={panelF11F12} panelNewSession={panelNewSession} rows={_baseManagementPanel?.BaseManagementRowCount??-1} selected={_baseManagementPanel?.SelectedManagementBaseId} panelNodes={_baseManagementPanel?.BaseManagementPanelNodeCount??-1}");
			GD.Print($"[BaseRoleV3] fixture PASS={pass} pure=({BaseRoleSelfCheckV3.LastValidationSummary}) small=({small}) disposedSubscription={disposedSubscription} f11f12={f11f12} newSession={newSession} overlays={overlayBefore} mainThreadChunkGeneration={_streamManager?.MainThreadChunkGenerationCount??-1}");
			GetTree().Quit(pass?0:3);
		}
		catch(Exception exception){GD.PushError($"[BaseRoleV3] fixture FAIL {exception.Message}");GetTree().Quit(3);}
	}

	private static void PrintResourceEcologyDiagnostics()
	{
		if(!GameplaySessionV3.TryGetResourceEcologySession(out ResourceEcologySessionV3? ecology)||ecology==null){GD.Print("[ResourceEcologyV3] inactive");return;}var d=ecology.Diagnostics;GD.Print($"[ResourceEcologyV3] algorithm={ResourceEcologySessionV3.AlgorithmVersion} time={ecology.SimulationTimeSeconds:0.0} chunks={ecology.ChunkStateCount} states={ecology.StateCount} active={ecology.ActiveStateCount} due={ecology.DueCount} eligible={ecology.ActiveEligibleCount} credits={ecology.PendingCredits:0.00} suppression={ecology.SuppressionCount} negative={ecology.NegativeCacheCount} attempts={d.RenewalAttemptsThisTick} candidates={d.CandidateCellsEvaluatedThisTick} spawns={d.SuccessfulSpawnsThisTick} tree={d.TreeRegrowthThisTick} seedBank={d.TreeSeedBankRecoveryThisTick} stone={d.StoneReplenishmentThisTick} rejected density/max/static/runtime/room/spacing/parent/suppressed/transient/conflict={d.DensityRejectedCount}/{d.MaximumDensityRejectedCount}/{d.StaticExclusionRejectedCount}/{d.RuntimeExclusionRejectedCount}/{d.IndoorRoomRejectedCount}/{d.SpacingRejectedCount}/{d.ParentRejectedCount}/{d.SuppressionRejectedCount}/{d.TransientRejectedCount}/{d.RegistrationConflictCount} stale={d.StaleScheduleDiscardCount} tickMs={d.EcologyTickMs:0.000}/{d.MaxEcologyTickMs:0.000} fullScans={d.FullResourceRegistryScanCount}/{d.FullWorldEcologyScanCount} inactiveCells={d.InactiveCellScanCount} neighborGeneration={d.NeighborChunkGenerationCount} perNodeTimer/process/chunkProcess={d.PerResourceTimerCount}/{d.PerResourceProcessCount}/{d.PerChunkEcologyProcessCount}");foreach(var attempt in ecology.GetRecentAttempts().TakeLast(8))GD.Print($"[ResourceEcologyV3] attempt key={attempt.Key.ChunkCoord}:{attempt.Key.ResourceDefinitionId} seq={attempt.AttemptSequence} cell={attempt.Cell} failure={attempt.Failure} origin={attempt.Origin} id={attempt.SpawnedResourceId} cpu={attempt.CpuMilliseconds:0.000}ms");
	}

	private void PrintSimulationClockDiagnostics()
	{
		if(!GameplaySessionV3.TryGetSimulationClock(out SimulationClockSessionV3? clock)||clock==null){GD.Print("[SimulationClockV3] inactive");return;}
		SimulationClockSnapshotV3 value=clock.GetSnapshot();SimulationClockDiagnosticsV3 d=clock.Diagnostics;
		int hudCount=GetTree().GetNodesInGroup("simulation_clock_hud_v3").Count;
		SimulationDeltaRoutingDiagnosticsV3 routing=clock.RoutingDiagnostics;
		GD.Print($"[SimulationClockV3] day/time={value.DayIndex}/{value.Hour:00}:{value.Minute:00} phase={value.DayPhase} scale={value.TimeScale} paused={value.IsPaused} elapsed={value.ElapsedSimulationSeconds:0.000} tick/revision={value.SimulationTick}/{value.Revision} advance/duplicate/invalid={d.ClockAdvanceCallCount}/{d.ClockDuplicateAdvanceFrameCount}/{d.InvalidDeltaCount} boundaries hour/day/phase={d.HourBoundaryCount}/{d.DayBoundaryCount}/{d.PhaseBoundaryCount} delta real/scaled/world={routing.LastRealDelta:0.0000}/{routing.LastScaledGameplayDelta:0.0000}/{routing.LastWorldSecondsAdvanced:0.000} steps/duplicate/paused/raw={routing.SimulationStepFrameCount}/{routing.DuplicateSimulationStepCount}/{routing.PausedSimulationAdvanceViolationCount}/{routing.RawDeltaBypassCount} advances M/W/N/F/E={routing.MovementAdvanceCount}/{routing.WorkAdvanceCount}/{routing.NeedsAdvanceCount}/{routing.FarmingAdvanceCount}/{routing.EcologyAdvanceCount} pending N/F/E={routing.NeedsPendingTickCredit:0.000}/{routing.FarmingPendingTickCredit:0.000}/{routing.EcologyPendingTickCredit:0.000} instance/hud=1/{hudCount}");
		if(GameplaySessionV3.TryGetMercenarySchedule(out MercenaryScheduleSessionV3? schedules)&&schedules!=null){var sd=schedules.Diagnostics;GD.Print($"[MercenaryScheduleV3] states/dirty/transitions/events={schedules.Count}/{schedules.DirtyCount}/{schedules.TransitionIndexEntryCount}/{sd.ScheduleEventCount} subscriptions clock/registry={schedules.ClockSubscriptionCount}/{schedules.RegistrySubscriptionCount} blocked/delayed/fullScan={sd.BlockedAutoAssignmentCount}/{sd.DelayedScheduleReleaseCount}/{sd.FullMercenaryScanCount}");if(GameplaySessionV3.TryGetControlSession(out MercenaryControlSessionV3? scheduleControl)&&scheduleControl!=null)foreach(string id in scheduleControl.Selection.GetSelectedIds())if(schedules.TryGetSchedule(id,out MercenaryScheduleSnapshotV3? schedule)&&schedule!=null){MercenarySchedulePolicyV3 policy=schedules.GetCurrentPolicy(id);GD.Print($"[MercenaryScheduleV3] merc={id} preset={schedule.Preset} state={schedule.CurrentState} hour/next={schedule.CurrentHour}/{schedule.NextTransitionHour} rev/stateRev={schedule.Revision}/{schedule.CurrentStateRevision} auto/rest/recreation={policy.AutomaticJobEligible}/{policy.WantsScheduledRest}/{policy.RecreationIntent} reason={schedule.LastChangedReason}");}}
		if(GameplaySessionV3.TryGetFrontierSurvivalSession(out FrontierSurvivalSessionV3? objective)&&objective!=null){FrontierSurvivalSnapshotV3 snapshot=objective.GetSnapshot();string progress=string.Join(',',snapshot.Milestones.Select(m=>$"{m.MilestoneId}:{m.CurrentValue}/{m.TargetValue}:{m.CompletedOnce}"));GD.Print($"[FrontierSurvivalV3] active=True company={snapshot.CompanyId} completed={snapshot.CompletedMilestoneCount}/{snapshot.TotalMilestoneCount} hours={snapshot.SurvivedHours} final={snapshot.IsCompleted} progress={progress} dirty/events/fullScan={objective.Diagnostics.ObjectiveDirtyCount}/{objective.Diagnostics.ObjectiveEventCount}/{objective.Diagnostics.ObjectiveFullWorldScanCount} panel/rows={GetTree().GetNodesInGroup("frontier_survival_panel_v3").Count}/{_frontierSurvivalPanel?.CreatedRowCount??0}");}
	}

	private void PrintDayNightVisualDiagnostics()
	{
		if(_dayNightVisualRuntime==null){GD.Print("[DayNightVisualV3] inactive");return;}
		Color tint=_dayNightVisualRuntime.CurrentTint;
		GD.Print($"[DayNightVisualV3] DayNightVisualActive={_dayNightVisualRuntime.IsActive} MinuteOfDay={_dayNightVisualRuntime.MinuteOfDay:0.000} CurrentTint={tint.R:0.000},{tint.G:0.000},{tint.B:0.000},{tint.A:0.000} VisualUpdateCount={_dayNightVisualRuntime.VisualUpdateCount} SkippedUnchangedTintCount={_dayNightVisualRuntime.SkippedUnchangedTintCount} ImmediateRefreshCount={_dayNightVisualRuntime.ImmediateRefreshCount} CanvasModulateNodeCount={_dayNightVisualRuntime.CanvasModulateNodeCount} DuplicateVisualRuntimeCount={_dayNightVisualRuntime.DuplicateVisualRuntimeCount} DuplicateClockSubscriptionCount={_dayNightVisualRuntime.DuplicateClockSubscriptionCount}");
	}

	private void PrintResourceShortageDiagnostics()
	{
		if(!GameplaySessionV3.TryGetResourceEcologySession(out ResourceEcologySessionV3? ecology)||ecology==null)return;foreach(var state in ecology.GetShortageStates())GD.Print($"[ResourceEcologyV3] safety company={state.CompanyId} resource={state.ResourceDefinitionId} anchor={state.AnchorCell} source={state.AnchorSource} accessible/alive={state.AccessibleNodeCount}/{state.AliveNodeCountInRadius} reachable={state.ReachableCellCount} limit={state.ReachabilityLimitHit} tier={state.Tier} affected={state.AffectedChunkCount} status={state.EvaluationStatus} failure={state.LastFailureReason}");
		if(_resourceEcologyRuntime!=null&&_gridRenderer!=null){Vector2I cell=_gridRenderer.WorldToCell(GetViewport().GetCanvasTransform().AffineInverse()*GetViewport().GetMousePosition());var exclusion=_resourceEcologyRuntime.Evaluate(new(cell,FloorRegistryV3.ChunkOf(cell),NaturalResourceDefinitionCatalogV3.TreeId,NaturalResourceRenewalClassV3.VegetativeSpread,_worldManager?.LocalCompanyId,ecology.SimulationTimeSeconds,0));FlatlandCellSampleV2? sample=_worldManager?.SampleV3PlanCellForNavigation(cell);GD.Print($"[ResourceEcologyV3] cursor cell={cell} biome={sample?.BiomeKind} walkable={sample?.IsWalkable} road={sample?.IsRoad} settlement={sample?.IsVillage==true||sample?.IsStartingVillage==true} renewalAllowed={exclusion.IsAllowed} exclusion={exclusion.Kind} transient={exclusion.IsTransient} blocker={exclusion.BlockingSourceId} occupancyRevision={exclusion.Revision}");}
	}
	private void PrintBaseAreaDiagnostics()
	{
		if(!GameplaySessionV3.TryGetBaseAreaSession(out BaseAreaSessionV3? session)||session==null){GD.Print("[BaseAreaV3] inactive");return;}
		Vector2I cursor=_gridRenderer?.WorldToCell(GetViewport().GetCanvasTransform().AffineInverse()*GetViewport().GetMousePosition())??Vector2I.Zero;GlobalCellCoord cursorCell=new(cursor);IReadOnlyList<BaseAreaV3> cores=session.Areas.GetAtCell(cursorCell);
		GameplaySessionV3.TryGetBaseRoleSession(out BaseRoleSessionV3? roles);GameplaySessionV3.TryGetFacilityAffiliationSession(out FacilityAffiliationSessionV3? facilities);GameplaySessionV3.TryGetMercenaryBaseAffiliationSession(out MercenaryBaseAffiliationSessionV3? mercenaryBases);GameplaySessionV3.TryGetJobActivityRangePolicy(out JobActivityRangePolicyV3? policy);GameplaySessionV3.TryGetJobManager(out JobManagerV3? jobs);GameplaySessionV3.TryGetMercenarySession(out MercenarySessionV3? mercenaries);GameplaySessionV3.TryGetControlSession(out MercenaryControlSessionV3? control);
		IReadOnlyList<BaseAreaV3> ranges=facilities?.GetBaseAreasWhoseActivityRangeContains(_worldManager?.LocalCompanyId??string.Empty,cursorCell)??Array.Empty<BaseAreaV3>();IReadOnlyList<FacilityAffiliationSnapshotV3> cursorFacilities=facilities?.GetFacilitiesInChunk(BaseSpatialSourceV3.ChunkOf(cursor)).Where(f=>f.Bounds.HasPoint(cursor)).Take(16).ToList()??new List<FacilityAffiliationSnapshotV3>();IReadOnlyList<JobRecordV3> cursorJobs=jobs?.GetJobsAtCell(_worldManager?.LocalCompanyId??string.Empty,cursor,16)??Array.Empty<JobRecordV3>();
		GD.Print($"[BaseActivityV3] cursor={cursor} core={string.Join(',',cores.Select(a=>a.BaseAreaId))} ranges={string.Join(',',ranges.Select(a=>a.BaseAreaId))} facilities={string.Join(',',cursorFacilities.Select(f=>$"{f.FacilityKey}:{f.AffiliationState}:{f.BaseAreaId}"))} jobs={string.Join(',',cursorJobs.Select(j=>$"{j.JobType}:{j.SourceId}:{j.State}"))}");
		foreach(BaseAreaV3 area in cores)if(roles?.TryGetRole(area.BaseAreaId,out BaseRoleStateV3? role)==true&&role!=null)GD.Print($"[BaseRoleV3] cursor base={area.BaseAreaId} company={area.CompanyId} role={role.Role} headquarters={role.Role==BaseRoleV3.Headquarters} source={role.AssignmentSource} revision={role.Revision}");
		foreach(string id in control?.Selection.GetSelectedIds()??Array.Empty<string>())if(mercenaryBases?.TryGetMercenaryBase(id,out var affiliation)==true&&affiliation!=null&&mercenaries?.Registry.TryGetState(id,out var state)==true&&state!=null){JobRecordV3? currentJob=null;jobs?.TryGetAssignedJob(id,out currentJob);JobActivityRangeDecisionV3? decision=currentJob!=null&&policy!=null?policy.Evaluate(currentJob,id,JobCommandSourceV3.Automatic):null;JobActivityRangeEventV3? last=policy?.GetRecentEvents().LastOrDefault(e=>e.MercenaryId==id);bool insideCore=affiliation.BaseAreaId!=null&&facilities?.IsCellInsideBaseCore(affiliation.BaseAreaId,state.CurrentCell)==true,insideRange=affiliation.BaseAreaId!=null&&facilities?.IsCellInsideActivityRange(affiliation.BaseAreaId,state.CurrentCell)==true;GD.Print($"[BaseActivityV3] merc={id} company={state.CompanyId} base={affiliation.BaseAreaId} source={affiliation.AssignmentSource} cell={state.CurrentCell} core/range={insideCore}/{insideRange} job={currentJob?.JobId}:{currentJob?.JobType} jobBase={decision?.JobBaseAreaId} policy={decision?.Reason} direct={last?.Reason==JobActivityRangeReasonV3.DirectOrderOverride} needs={last?.Reason==JobActivityRangeReasonV3.NeedsOverride} affiliationChange={affiliation.LastChangedReason} lastRange={last?.Reason}");}
		List<string> recent=new();foreach(var e in session.GetRecentEvents())recent.Add($"Base:{e.Kind}:{e.BaseAreaId}");if(roles!=null)foreach(var e in roles.GetRecentEvents())recent.Add($"Role:{e.Kind}:{e.BaseAreaId}:{e.OldRole}->{e.NewRole}:{e.Reason}");if(facilities!=null){foreach(var e in facilities.GetRecentFacilityEvents())recent.Add($"Facility:{e.FacilityKey}:{e.OldBaseAreaId}->{e.NewBaseAreaId}:{e.NewState}");foreach(var e in facilities.GetRecentRangeEvents())recent.Add($"Range:{e.BaseAreaId}:{(e.Removed?"Removed":"Changed")}");}if(mercenaryBases!=null)foreach(var e in mercenaryBases.GetRecentEvents())recent.Add($"Merc:{e.MercenaryId}:{e.OldBaseAreaId}->{e.NewBaseAreaId}:{e.Reason}");if(policy!=null)foreach(var e in policy.GetRecentEvents())recent.Add($"Policy:{e.MercenaryId}:{e.Reason}:{e.TargetCell}");foreach(string e in recent.TakeLast(16))GD.Print($"[BaseActivityV3] recent {e}");
	}
	private async Task RunBaseAreaLifecycleFixture()
	{
		for(int i=0;i<600&&_baseAreaRuntime==null;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);if(_baseAreaRuntime==null||_worldManager==null||!GameplaySessionV3.TryGetBaseAreaSession(out BaseAreaSessionV3? before)||before==null||!GameplaySessionV3.TryGetConstructionSession(out ConstructionSessionV3? construction)||construction==null){GD.PushError("[BaseAreaV3] lifecycle fixture setup failed");GetTree().Quit(2);return;}string company=_worldManager.LocalCompanyId;Vector2I cell=_worldManager.PlayerStartGlobalCell;construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.BasicBedId,out var definition);ResolvedStructureFootprintV3 footprint=StructureFootprintResolverV3.Resolve(definition!,new(cell),StructureOrientationV3.Deg0);StructureStateV3 bed=new(StructureIdFactoryV3.Create(),company,StructureDefinitionCatalogV3.BasicBedId,new(cell),StructureOrientationV3.Deg0,footprint.Cells,Array.Empty<StructureMaterialRequirementV3>(),false,DateTime.UtcNow,StructureMovementKindV3.NonBlocking);construction.Structures.TryRegister(bed,construction.Blueprints,_worldManager.WorldBounds,out _);for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);string id=before.Areas.GetForCompany(company).Single().BaseAreaId;_worldManager.RegenerateVisibleChunks(false);for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);_worldManager.RegenerateVisibleChunks(true);for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool f11f12=GameplaySessionV3.TryGetBaseAreaSession(out BaseAreaSessionV3? after)&&ReferenceEquals(before,after)&&after.Areas.GetForCompany(company).Count==1&&after.Areas.GetForCompany(company)[0].BaseAreaId==id;Node? entities=GetNodeOrNull("GameplayEntitiesV3");int runtimeNodes=entities?.GetChildren().OfType<BaseAreaRuntimeV3>().Count()??0,overlayNodes=entities?.GetChildren().OfType<BaseAreaOverlayV3>().Count()??0;GameplaySessionV3.BeginNewSession();for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool replacementOk=GameplaySessionV3.TryGetBaseAreaSession(out BaseAreaSessionV3? replacement)&&replacement!=null&&!ReferenceEquals(before,replacement)&&replacement.Areas.Count==0&&replacement.Sources.Count==0&&!_baseAreaRuntime.IsPhysicsProcessing();bool pass=f11f12&&runtimeNodes==1&&overlayNodes==1&&replacementOk;GD.Print($"[BaseAreaV3] lifecycle PASS={pass} f11f12={f11f12} id={id} nodes runtime/overlay={runtimeNodes}/{overlayNodes} replacementEmpty={replacementOk} mainThreadChunkGeneration={_streamManager?.MainThreadChunkGenerationCount??-1}");GetTree().Quit(pass?0:3);
	}

	private async Task RunBaseAreaPerformanceFixture()
	{
		for(int i=0;i<600&&_baseAreaRuntime==null;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);if(_baseAreaRuntime==null){GD.PushError("[BaseAreaV3] performance fixture setup failed");GetTree().Quit(2);return;}try{string summary=BaseAreaSelfCheckV3.RunLargePerformanceFixtures();GD.Print($"[BaseAreaV3] performance PASS {summary} mainThreadChunkGeneration={_streamManager?.MainThreadChunkGenerationCount??-1}");GetTree().Quit(0);}catch(Exception exception){GD.PushError($"[BaseAreaV3] performance FAIL {exception.Message}");GetTree().Quit(3);}
	}

	private async Task RunFacilityAffiliationFixture()
	{
		for(int i=0;i<600&&_baseAreaRuntime==null;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);if(_baseAreaRuntime==null||_worldManager==null||!GameplaySessionV3.TryGetFacilityAffiliationSession(out FacilityAffiliationSessionV3? before)||before==null){GD.PushError("[FacilityAffiliationV3] fixture setup failed");GetTree().Quit(2);return;}try{if(!FacilityAffiliationSelfCheckV3.TryValidate(out string reason))throw new InvalidOperationException(reason);string performance=FacilityAffiliationSelfCheckV3.RunPerformanceFixture();_worldManager.RegenerateVisibleChunks(false);for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);_worldManager.RegenerateVisibleChunks(true);for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool f11f12=GameplaySessionV3.TryGetFacilityAffiliationSession(out FacilityAffiliationSessionV3? after)&&ReferenceEquals(before,after);GameplaySessionV3.BeginNewSession();bool replaced=GameplaySessionV3.TryGetFacilityAffiliationSession(out FacilityAffiliationSessionV3? replacement)&&replacement!=null&&!ReferenceEquals(before,replacement)&&replacement.Count==0&&replacement.ActivityRangeCount==0&&before.ProcessDirty()==0;bool pass=f11f12&&replaced;GD.Print($"[FacilityAffiliationV3] fixture PASS={pass} {performance} f11f12={f11f12} beginNewSession={replaced} forbidden={before.Diagnostics.FullWorldFacilityScanCount}/{before.Diagnostics.FullWorldActivityCellBuildCount}/{before.Diagnostics.PerFacilityNodeCount}/{before.Diagnostics.PerBaseActivityNodeCount}/{before.Diagnostics.ActivityRangeTriggeredChunkGenerationCount} mainThreadChunkGeneration={_streamManager?.MainThreadChunkGenerationCount??-1}");GetTree().Quit(pass?0:3);}catch(Exception exception){GD.PushError($"[FacilityAffiliationV3] fixture FAIL {exception.Message}");GetTree().Quit(3);}
	}

	private async Task RunMercenaryBaseAffiliationFixture()
	{
		for(int i=0;i<600&&_baseAreaRuntime==null;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);if(_baseAreaRuntime==null||_worldManager==null||!GameplaySessionV3.TryGetMercenaryBaseAffiliationSession(out MercenaryBaseAffiliationSessionV3? before)||before==null){GD.PushError("[MercenaryBaseAffiliationV3] fixture setup failed");GetTree().Quit(2);return;}try{if(!MercenaryBaseAffiliationSelfCheckV3.TryValidate(out string reason))throw new InvalidOperationException(reason);string performance=MercenaryBaseAffiliationSelfCheckV3.RunPerformanceFixture();_worldManager.RegenerateVisibleChunks(false);for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);_worldManager.RegenerateVisibleChunks(true);for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool f11f12=GameplaySessionV3.TryGetMercenaryBaseAffiliationSession(out MercenaryBaseAffiliationSessionV3? after)&&ReferenceEquals(before,after);GameplaySessionV3.BeginNewSession();bool replaced=GameplaySessionV3.TryGetMercenaryBaseAffiliationSession(out MercenaryBaseAffiliationSessionV3? replacement)&&replacement!=null&&!ReferenceEquals(before,replacement)&&replacement.Count==0&&replacement.DirtyMercenaryCount==0&&before.ProcessDirty()==0;bool pass=f11f12&&replaced;var d=before.Diagnostics;GD.Print($"[MercenaryBaseAffiliationV3] fixture PASS={pass} {performance} f11f12={f11f12} beginNewSession={replaced} forbidden={d.MercenaryBaseCartesianComparisonCount}/{d.PerMercenaryBaseNodeCount}/{d.PerMercenaryBaseProcessCount}/{d.MercenaryBaseTriggeredChunkGenerationCount} mainThreadChunkGeneration={_streamManager?.MainThreadChunkGenerationCount??-1}");GetTree().Quit(pass?0:3);}catch(Exception exception){GD.PushError($"[MercenaryBaseAffiliationV3] fixture FAIL {exception.Message}");GetTree().Quit(3);}
	}

	private async Task RunJobActivityRangeFixture()
	{
		for(int warmup=0;warmup<8;warmup++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
		for(int i=0;i<600&&(_baseAreaRuntime==null||_jobManagerRuntime==null);i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);if(_baseAreaRuntime==null||_jobManagerRuntime==null||_worldManager==null||!GameplaySessionV3.TryGetJobActivityRangePolicy(out JobActivityRangePolicyV3? before)||before==null||!GameplaySessionV3.TryGetBaseAreaSession(out BaseAreaSessionV3? basesBefore)||basesBefore==null||!GameplaySessionV3.TryGetFacilityAffiliationSession(out FacilityAffiliationSessionV3? facilitiesBefore)||facilitiesBefore==null||!GameplaySessionV3.TryGetMercenaryBaseAffiliationSession(out MercenaryBaseAffiliationSessionV3? mercenaryBasesBefore)||mercenaryBasesBefore==null){GD.PushError("[JobActivityRangeV3] fixture setup failed");GetTree().Quit(2);return;}try{string runtimeVertical=await RunActivityRangeRuntimeVertical();if(!JobActivityRangeSelfCheckV3.TryValidate(out string reason))throw new InvalidOperationException(reason);string validation=JobActivityRangeSelfCheckV3.LastValidationSummary,performance=JobActivityRangeSelfCheckV3.RunPerformanceFixture(),scale=OS.GetEnvironment("ACTIVITY_RANGE_VERIFICATION_FIXTURE")=="1"?JobActivityRangeSelfCheckV3.RunScaleFixtures():"not requested";for(int passIndex=0;passIndex<3;passIndex++){_worldManager.RegenerateVisibleChunks(false);for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);_worldManager.RegenerateVisibleChunks(true);for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}Node? entities=GetNodeOrNull("GameplayEntitiesV3");int baseRuntimeNodes=entities?.GetChildren().OfType<BaseAreaRuntimeV3>().Count()??0,overlayNodes=entities?.GetChildren().OfType<BaseAreaOverlayV3>().Count()??0,jobRuntimeNodes=entities?.GetChildren().OfType<JobManagerRuntimeV3>().Count()??0;bool f11f12=GameplaySessionV3.TryGetJobActivityRangePolicy(out JobActivityRangePolicyV3? after)&&ReferenceEquals(before,after)&&GameplaySessionV3.TryGetBaseAreaSession(out BaseAreaSessionV3? basesAfter)&&ReferenceEquals(basesBefore,basesAfter)&&GameplaySessionV3.TryGetFacilityAffiliationSession(out FacilityAffiliationSessionV3? facilitiesAfter)&&ReferenceEquals(facilitiesBefore,facilitiesAfter)&&GameplaySessionV3.TryGetMercenaryBaseAffiliationSession(out MercenaryBaseAffiliationSessionV3? mercenaryBasesAfter)&&ReferenceEquals(mercenaryBasesBefore,mercenaryBasesAfter)&&baseRuntimeNodes==1&&overlayNodes==1&&jobRuntimeNodes==1;int oldRuntimeTicks=_jobManagerRuntime.RuntimeTickCount;GameplaySessionV3.BeginNewSession();for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool replaced=GameplaySessionV3.TryGetJobActivityRangePolicy(out JobActivityRangePolicyV3? replacement)&&replacement!=null&&!ReferenceEquals(before,replacement)&&GameplaySessionV3.TryGetBaseAreaSession(out BaseAreaSessionV3? newBases)&&newBases?.Areas.Count==0&&newBases.Sources.Count==0&&GameplaySessionV3.TryGetFacilityAffiliationSession(out FacilityAffiliationSessionV3? newFacilities)&&newFacilities?.Count==0&&newFacilities.ActivityRangeCount==0&&GameplaySessionV3.TryGetMercenaryBaseAffiliationSession(out MercenaryBaseAffiliationSessionV3? newMercenaryBases)&&newMercenaryBases?.Count==0&&newMercenaryBases.DirtyMercenaryCount==0&&_jobManagerRuntime.RuntimeTickCount==oldRuntimeTicks;_baseAreaOverlay?.QueueRedraw();var d=before.Diagnostics;bool pass=f11f12&&replaced&&runtimeVertical.StartsWith("PASS",StringComparison.Ordinal)&&d.MercenaryJobCartesianScanCount==0&&d.MercenaryBaseCartesianScanCount==0&&_baseAreaOverlay?.PerObjectNodeCount==0;GD.Print($"[ActivityRangeVerificationV3] fixture preset={_worldManager.MapSizePreset} PASS={pass} runtime=({runtimeVertical}) vertical=({validation}) performance=({performance}) scale=({scale}) f11f12={f11f12} nodes={baseRuntimeNodes}/{overlayNodes}/{jobRuntimeNodes} beginNewSession={replaced} forbidden={d.MercenaryJobCartesianScanCount}/{d.MercenaryBaseCartesianScanCount}/{d.FullJobAffiliationRebuildCount}/{_baseAreaOverlay?.PerObjectNodeCount} mainThreadChunkGeneration={_streamManager?.MainThreadChunkGenerationCount??-1}");GetTree().Quit(pass?0:3);}catch(Exception exception){GD.PushError($"[ActivityRangeVerificationV3] fixture FAIL {exception.Message}");GetTree().Quit(3);}
	}

	private async Task<string> RunActivityRangeRuntimeVertical()
	{
		if(_worldManager==null||!GameplaySessionV3.TryGetBaseAreaSession(out BaseAreaSessionV3? bases)||bases==null||!GameplaySessionV3.TryGetFacilityAffiliationSession(out FacilityAffiliationSessionV3? facilities)||facilities==null||!GameplaySessionV3.TryGetMercenaryBaseAffiliationSession(out MercenaryBaseAffiliationSessionV3? affiliations)||affiliations==null||!GameplaySessionV3.TryGetJobActivityRangePolicy(out JobActivityRangePolicyV3? policy)||policy==null||!GameplaySessionV3.TryGetConstructionSession(out ConstructionSessionV3? construction)||construction==null||!GameplaySessionV3.TryGetMercenarySession(out MercenarySessionV3? mercenaries)||mercenaries==null)return "FAIL setup";string company=_worldManager.LocalCompanyId;Vector2I origin=_worldManager.PlayerStartGlobalCell;bool startEmpty=bases.Areas.Count==0&&affiliations.GetStateCount(MercenaryBaseAffiliationStateV3.Unassigned)==mercenaries.Registry.Count;construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.BasicBedId,out var definition);StructureStateV3 MakeBed(string id,Vector2I cell){ResolvedStructureFootprintV3 footprint=StructureFootprintResolverV3.Resolve(definition!,new(cell),StructureOrientationV3.Deg0);return new(id,company,StructureDefinitionCatalogV3.BasicBedId,new(cell),StructureOrientationV3.Deg0,footprint.Cells,Array.Empty<StructureMaterialRequirementV3>(),false,DateTime.UtcNow,StructureMovementKindV3.NonBlocking);}string leftId=StructureIdFactoryV3.Create();construction.Structures.TryRegister(MakeBed(leftId,origin),construction.Blueprints,_worldManager.WorldBounds,out _);for(int i=0;i<10;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);string mercenary=mercenaries.Registry.GetMercenariesByCompany(company)[0];MercenaryBaseAffiliationV3? mercBase=null;bool first=bases.Areas.GetForCompany(company).Count==1&&facilities.TryGetFacility(leftId,out var leftFacility)&&leftFacility?.AffiliationState==FacilityAffiliationStateV3.Affiliated&&affiliations.TryGetMercenaryBase(mercenary,out mercBase)&&mercBase?.State==MercenaryBaseAffiliationStateV3.Assigned&&facilities.ActivityRangeCount==1;string firstBase=mercBase?.BaseAreaId??string.Empty;var inside=policy.Evaluate(company,JobTypeV3.Gathering,JobSourceKindV3.ResourceNode,"runtime_in",new(origin+new Vector2I(40,0)),mercenary,JobCommandSourceV3.Automatic);var outside=policy.Evaluate(company,JobTypeV3.Gathering,JobSourceKindV3.ResourceNode,"runtime_out",new(origin+new Vector2I(70,0)),mercenary,JobCommandSourceV3.Automatic);var direct=policy.Evaluate(company,JobTypeV3.Gathering,JobSourceKindV3.ResourceNode,"runtime_out",new(origin+new Vector2I(70,0)),mercenary,JobCommandSourceV3.DirectOrder);_baseAreaOverlay?.Toggle();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);bool overlay=_baseAreaOverlay?.DrawnBaseCount>0&&_baseAreaOverlay.DrawnActivityRangeCount>0&&_baseAreaOverlay.DrawnFacilityCount>0&&_baseAreaOverlay.PerObjectNodeCount==0;string rightId=StructureIdFactoryV3.Create();construction.Structures.TryRegister(MakeBed(rightId,origin+new Vector2I(140,0)),construction.Blueprints,_worldManager.WorldBounds,out _);for(int i=0;i<10;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool two=bases.Areas.GetForCompany(company).Count==2&&policy.Evaluate(company,JobTypeV3.Demolition,JobSourceKindV3.Structure,rightId,new(origin+new Vector2I(140,0)),mercenary,JobCommandSourceV3.Automatic).Reason==JobActivityRangeReasonV3.OtherBaseFacility;List<string> bridges=new();for(int x=8;x<140;x+=8){string id=StructureIdFactoryV3.Create();bridges.Add(id);construction.Structures.TryRegister(MakeBed(id,origin+new Vector2I(x,0)),construction.Blueprints,_worldManager.WorldBounds,out _);}for(int i=0;i<16;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool merged=bases.Areas.GetForCompany(company).Count==1&&bases.Areas.GetForCompany(company)[0].BaseAreaId==firstBase;foreach(string id in bridges)construction.Structures.TryRemove(id,out _);for(int i=0;i<16;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool split=bases.Areas.GetForCompany(company).Count==2;construction.Structures.TryRemove(leftId,out _);construction.Structures.TryRemove(rightId,out _);for(int i=0;i<16;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);bool removed=bases.Areas.Count==0&&affiliations.GetStateCount(MercenaryBaseAffiliationStateV3.Unassigned)==mercenaries.Registry.Count;_baseAreaOverlay?.Toggle();bool pass=startEmpty&&first&&inside.Allowed&&!outside.Allowed&&direct.Reason==JobActivityRangeReasonV3.DirectOrderOverride&&affiliations.TryGetMercenaryBase(mercenary,out var finalAffiliation)&&finalAffiliation?.BaseAreaId==null&&overlay&&two&&merged&&split&&removed;return $"{(pass?"PASS":"FAIL")} start/first/overlay/two/merge/split/remove={startEmpty}/{first}/{overlay}/{two}/{merged}/{split}/{removed} range={inside.Reason}/{outside.Reason}/{direct.Reason}";
	}

	private async Task RunResourceEcologySafetyFixture()
	{
		for(int i=0;i<600&&(_resourceEcologyRuntime==null||_ecologyChunks.Count==0);i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);if(_resourceEcologyRuntime==null||_worldManager==null||!_worldManager.TryGetResourceSession(out ResourceSessionV3? resources)||resources==null||!_worldManager.TryGetMercenarySession(out MercenarySessionV3? mercenaries)||mercenaries==null||!_worldManager.TryGetConstructionSession(out ConstructionSessionV3? construction)||construction==null||!GameplaySessionV3.TryGetResourceEcologySession(out ResourceEcologySessionV3? ecology)||ecology==null){GD.PushError("[ResourceEcologyV3] safety fixture setup failed");return;}string company=_worldManager.LocalCompanyId;var ids=mercenaries.Registry.GetMercenariesByCompany(company);if(ids.Count==0||!mercenaries.Registry.TryGetState(ids[0],out var mercenary)||mercenary==null){GD.PushError("[ResourceEcologyV3] safety fixture has no local mercenary");return;}Vector2I anchor=mercenary.CurrentCell.Value;var occupied=_resourceEcologyRuntime.Evaluate(new(anchor,FloorRegistryV3.ChunkOf(anchor),NaturalResourceDefinitionCatalogV3.TreeId,NaturalResourceRenewalClassV3.VegetativeSpread,company,ecology.SimulationTimeSeconds,1));Vector2I free=default;bool found=false;foreach(var chunk in _ecologyChunks.Values){for(int y=0;y<ChunkDataV2.ChunkSize&&!found;y++)for(int x=0;x<ChunkDataV2.ChunkSize&&!found;x++){Vector2I cell=chunk.OriginGlobalCell+new Vector2I(x,y);var e=_resourceEcologyRuntime.Evaluate(new(cell,chunk.GlobalChunkCoord,NaturalResourceDefinitionCatalogV3.TreeId,NaturalResourceRenewalClassV3.VegetativeSpread,company,ecology.SimulationTimeSeconds,2));if(e.IsAllowed){free=cell;found=true;}}if(found)break;}bool structureExcluded=false,structureReleased=false;if(found){var s=new StructureStateV3(StructureIdFactoryV3.Create(),company,StructureDefinitionCatalogV3.WoodenWallId,new(free),StructureOrientationV3.Deg0,new[]{new GlobalCellCoord(free)},Array.Empty<StructureMaterialRequirementV3>(),true,DateTime.UtcNow);if(construction.Structures.TryRegister(s,construction.Blueprints,_worldManager.WorldBounds,out _)){structureExcluded=!_resourceEcologyRuntime.Evaluate(new(free,FloorRegistryV3.ChunkOf(free),NaturalResourceDefinitionCatalogV3.TreeId,NaturalResourceRenewalClassV3.VegetativeSpread,company,ecology.SimulationTimeSeconds,3)).IsAllowed;construction.Structures.TryRemove(s.StructureId,out _);structureReleased=_resourceEcologyRuntime.Evaluate(new(free,FloorRegistryV3.ChunkOf(free),NaturalResourceDefinitionCatalogV3.TreeId,NaturalResourceRenewalClassV3.VegetativeSpread,company,ecology.SimulationTimeSeconds,4)).IsAllowed;}}
		int depleted=0;foreach(var chunk in _ecologyChunks.Values)foreach(string id in resources.Nodes.GetNodeIdsInChunk(chunk.GlobalChunkCoord))if(resources.Nodes.TryGet(id,out var node)&&node!=null&&!node.IsDepleted&&Math.Max(Math.Abs(node.Cell.Value.X-anchor.X),Math.Abs(node.Cell.Value.Y-anchor.Y))<=56){while(!node.IsDepleted)node.TryHarvest(out _,out _);resources.Nodes.NotifyChanged(id);depleted++;}_resourceEcologyRuntime.RunShortageEvaluationNow();ecology.TryGetShortageState(company,NaturalResourceDefinitionCatalogV3.TreeId,out var tree);ecology.TryGetShortageState(company,NaturalResourceDefinitionCatalogV3.StoneId,out var stone);for(int i=0;i<120;i++)ecology.Advance(.5,_resourceEcologyRuntime);var d=ecology.Diagnostics;GD.Print($"[ResourceEcologyV3] safety fixture anchor={anchor} mercenaryExcluded={!occupied.IsAllowed&&occupied.Kind==ResourceRenewalExclusionKindV3.Mercenary} structureExcluded/released={structureExcluded}/{structureReleased} depleted={depleted} tree={tree?.Tier}:{tree?.AccessibleNodeCount}/{tree?.AliveNodeCountInRadius} stone={stone?.Tier}:{stone?.AccessibleNodeCount}/{stone?.AliveNodeCountInRadius} pressure={ecology.ActiveShortagePressureCount} emergency={d.EmergencyDueSchedulesThisTick} accelerated={d.ShortageAcceleratedAttemptsThisTick}/{d.ShortageAcceleratedSpawnsThisTick} reach={tree?.ReachableCellCount} limit={tree?.ReachabilityLimitHit} checks={d.ResourceNodesCheckedThisTick} fullScan/A*/generation={d.FullWorldShortageScanCount}/{d.ResourcePerNodePathRequestCount}/{d.ShortageTriggeredChunkGenerationCount} staleNegative={d.StaleNegativeEntryDiscardCount} mainThreadChunkGeneration={_streamManager?.MainThreadChunkGenerationCount??-1}");
	}
	private async Task RunResourceEcologyFixture()
	{
		for(int i=0;i<600&&(_resourceEcologyRuntime==null||_ecologyChunks.Count==0);i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);if(_resourceEcologyRuntime==null||_worldManager==null||!_worldManager.TryGetResourceSession(out ResourceSessionV3? resources)||resources==null||!GameplaySessionV3.TryGetResourceEcologySession(out ResourceEcologySessionV3? ecology)||ecology==null){GD.PushError("[ResourceEcologyV3] runtime fixture setup failed");return;}ChunkDataV2? chosen=null;foreach(var chunk in _ecologyChunks.Values)if(_resourceEcologyRuntime.TryGetActiveChunk(chunk.GlobalChunkCoord,out _)&&chunk.ResourceEcologyCapacities.Any(c=>c.ResourceDefinitionId==NaturalResourceDefinitionCatalogV3.TreeId&&c.Biome==BiomeKindV3.ForestLand&&c.InitialTargetNodeCount>=2)){chosen=chunk;break;}chosen??=_ecologyChunks.Values.FirstOrDefault(chunk=>_resourceEcologyRuntime.TryGetActiveChunk(chunk.GlobalChunkCoord,out _));if(chosen==null){GD.PushError("[ResourceEcologyV3] runtime fixture found no chunk");return;}Vector2I coord=chosen.GlobalChunkCoord;var treeIds=resources.Nodes.GetNodeIdsInChunk(coord).Where(id=>resources.Nodes.TryGet(id,out var n)&&n!=null&&!n.IsDepleted&&n.NodeType==ResourceNodeTypeV3.Tree).ToList();int leave=treeIds.Count>1?1:0;for(int i=0;i<treeIds.Count-leave;i++)if(resources.Nodes.TryGet(treeIds[i],out var node)&&node!=null){while(!node.IsDepleted)node.TryHarvest(out _,out _);resources.Nodes.NotifyChanged(node.ResourceNodeId);resources.Nodes.NotifyChanged(node.ResourceNodeId);}int before=resources.Nodes.CountAliveInChunk(coord,NaturalResourceDefinitionCatalogV3.TreeId);string spawned=string.Empty;for(int i=0;i<80&&string.IsNullOrEmpty(spawned);i++){ecology.Advance(.5,_resourceEcologyRuntime);foreach(string id in resources.Nodes.GetNodeIdsInChunk(coord))if(resources.Nodes.TryGet(id,out var node)&&node!=null&&!node.IsDepleted&&node.OriginKind is NaturalResourceOriginKindV3.VegetativeSpread or NaturalResourceOriginKindV3.SeedBankRecovery){spawned=id;break;}}int after=resources.Nodes.CountAliveInChunk(coord,NaturalResourceDefinitionCatalogV3.TreeId);bool regrew=!string.IsNullOrEmpty(spawned)&&after>before;if(regrew&&resources.Nodes.TryGet(spawned,out var renewal)&&renewal!=null){while(!renewal.IsDepleted)renewal.TryHarvest(out _,out _);resources.Nodes.NotifyChanged(spawned);resources.Nodes.NotifyChanged(spawned);}double creditBefore=ecology.PendingCredits;_resourceEcologyRuntime.OnChunkDetached(coord);ecology.Advance(60,_resourceEcologyRuntime);double creditAfter=ecology.PendingCredits;int countBeforeAttach=resources.Nodes.CountAliveInChunk(coord,NaturalResourceDefinitionCatalogV3.TreeId);_resourceEcologyRuntime.OnChunkAttached(chosen);for(int i=0;i<20;i++)ecology.Advance(.5,_resourceEcologyRuntime);int countAfterAttach=resources.Nodes.CountAliveInChunk(coord,NaturalResourceDefinitionCatalogV3.TreeId);var d=ecology.Diagnostics;GD.Print($"[ResourceEcologyV3] runtime fixture chunk={coord} depleted={treeIds.Count-leave} before={before} after={after} regrew={regrew} renewedId={spawned} redepleted={(regrew?1:0)} credit={creditBefore:0.00}->{creditAfter:0.00} reactivation={countBeforeAttach}->{countAfterAttach} due={ecology.DueCount} suppression={ecology.SuppressionCount} maxSpawnTick={ResourceEcologySessionV3.MaxSuccessfulSpawnsPerTick} chunkSpawnLimit={ResourceEcologySessionV3.MaxSuccessfulSpawnsPerChunkPerTick} fullScans={d.FullResourceRegistryScanCount}/{d.FullWorldEcologyScanCount} inactiveCells={d.InactiveCellScanCount} neighborGeneration={d.NeighborChunkGenerationCount} mainThreadChunkGeneration={_streamManager?.MainThreadChunkGenerationCount??-1}");
	}

	private async Task RunResourceEcologyLifecycleFixture()
	{
		for(int i=0;i<240;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(_worldManager==null||!GameplaySessionV3.TryGetResourceEcologySession(out ResourceEcologySessionV3? before)||before==null)return;int states=before.StateCount,due=before.DueCount;double credits=before.PendingCredits,time=before.SimulationTimeSeconds;_worldManager.RegenerateVisibleChunks(false);for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);_worldManager.RegenerateVisibleChunks(true);for(int i=0;i<240;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);GameplaySessionV3.TryGetResourceEcologySession(out ResourceEcologySessionV3? after);bool same=ReferenceEquals(before,after);GD.Print($"[ResourceEcologyV3] lifecycle F11/F12 sameSession={same} states={states}->{after?.StateCount??-1} due={due}->{after?.DueCount??-1} credits={credits:0.00}->{after?.PendingCredits??-1:0.00} simulationAdvanced={(after?.SimulationTimeSeconds??0)>time} duplicateViews={_resourceNodeViews.DuplicateRejectedCount} mainThreadChunkGeneration={_streamManager?.MainThreadChunkGenerationCount??-1}");
	}

	private async Task RunResourceEcologyNewSessionFixture()
	{
		for(int i=0;i<600&&_resourceEcologyRuntime==null;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);if(_resourceEcologyRuntime==null||_worldManager==null||!GameplaySessionV3.TryGetResourceEcologySession(out ResourceEcologySessionV3? old)||old==null)return;double before=old.SimulationTimeSeconds;GameplaySessionV3.BeginNewSession();_resourceEcologyRuntime.AdvanceSimulation(.5);bool oldStopped=Math.Abs(old.SimulationTimeSeconds-before)<.0001&&!_resourceEcologyRuntime.IsPhysicsProcessing();ResourceEcologySessionV3 replacement=GameplaySessionV3.EnsureResourceEcologySession(_worldManager.WorldSeed,_worldManager.WorldBounds);GD.Print($"[ResourceEcologyV3] new-session fixture replaced={!ReferenceEquals(old,replacement)} oldStopped={oldStopped} new states/shortage/pressure={replacement.StateCount}/{replacement.CompanyShortageStateCount}/{replacement.ActiveShortagePressureCount} old states={old.StateCount} mainThreadChunkGeneration={_streamManager?.MainThreadChunkGenerationCount??-1}");GetTree().Quit();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (IsWorldMapOverlayOpen())
			{
				if (keyEvent.Keycode is Key.M or Key.Escape)
				{
					_worldMapOverlay?.HideMap();
					_constructionUi?.SetWorldMapBlocked(false);
					_mercenaryInspectHud?.SetWorldMapBlocked(false);
					_workPriorityPanel?.SetWorldMapBlocked(false);
					_baseManagementPanel?.SetWorldMapBlocked(false);
					_mercenarySchedulePanel?.SetWorldMapBlocked(false);
					_frontierSurvivalPanel?.SetWorldMapBlocked(false);
					_productionPanel?.SetWorldMapBlocked(false);
					GetViewport().SetInputAsHandled();
				}

				return;
			}

			if (keyEvent.Keycode == Key.M && !IsWorldInputLocked())
			{
				ToggleWorldMapOverlay();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (keyEvent.Keycode == Key.Escape)
			{
				if (_selectionInspectPanel?.HandleEscape() == true)
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (_productionPanel?.HandleEscape() == true)
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (_frontierSurvivalPanel?.HandleEscape() == true)
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (_baseManagementPanel?.HandleEscape() == true)
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (_mercenarySchedulePanel?.HandleEscape() == true)
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				_constructionUi?.HandleEscape();
				_workPriorityPanel?.Close("Escape");
			}

			if (IsWorldInputLocked() && !IsDebugKeyAllowedWhileLoading(keyEvent.Keycode))
			{
				return;
			}

			if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
				&& _farmDesignation?.TryHandleInput(@event) == true)
			{
				GetViewport().SetInputAsHandled();
				return;
			}

			if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
				&& _floorRuntime?.TryHandleInput(@event) == true)
			{
				GetViewport().SetInputAsHandled();
				return;
			}

			if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
				&& _constructionPlacement?.TryHandleInput(@event) == true)
			{
				GetViewport().SetInputAsHandled();
				return;
			}

			if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
				&& _demolitionDesignation?.TryHandleInput(@event) == true)
			{
				GetViewport().SetInputAsHandled();
				return;
			}

			if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
				&& _mercenaryInputController?.TryHandleUnhandledInput(@event) == true)
			{
				GetViewport().SetInputAsHandled();
				return;
			}

			HandleKey(keyEvent);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
			&& !IsWorldInputLocked()
			&& !IsWorldMapOverlayOpen()
			&& TryHandleBedAssignment(@event))
		{
			GetViewport().SetInputAsHandled();return;
		}

		if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
			&& !IsWorldInputLocked()
			&& !IsWorldMapOverlayOpen()
			&& ((_farmDesignation?.TryHandleInput(@event) == true)||TryHandleFarmingAction(@event)))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
			&& !IsWorldInputLocked()
			&& !IsWorldMapOverlayOpen()
			&& _floorRuntime?.TryHandleInput(@event) == true)
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
			&& !IsWorldInputLocked()
			&& !IsWorldMapOverlayOpen()
			&& TryHandleDirectEquipment(@event))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
			&& !IsWorldInputLocked()
			&& !IsWorldMapOverlayOpen()
			&& TryHandleDirectProduction(@event))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
			&& !IsWorldInputLocked()
			&& !IsWorldMapOverlayOpen()
			&& _productionPanel?.TryOpenAt(@event,_gridRenderer!) == true)
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
			&& !IsWorldInputLocked()
			&& !IsWorldMapOverlayOpen()
			&& ((_constructionPlacement?.TryHandleInput(@event) == true)
				|| (_demolitionDesignation?.TryHandleInput(@event) == true)
				|| (_constructionPlacement?.TryHandleBlueprintAction(@event,_constructionWork!,_worldManager.TryGetMercenaryControlSession(out MercenaryControlSessionV3? constructionControl)&&constructionControl!=null?constructionControl.Selection.GetSelectedIds():Array.Empty<string>()) == true)
				|| (_demolitionDesignation?.TryHandleStructureAction(@event,_worldManager.TryGetMercenaryControlSession(out MercenaryControlSessionV3? demolitionControl)&&demolitionControl!=null?demolitionControl.Selection.GetSelectedIds():Array.Empty<string>()) == true)))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
			&& !IsWorldInputLocked()
			&& !IsWorldMapOverlayOpen()
			&& @event is InputEventMouseMotion hoverMotion
			&& _gridRenderer != null)
		{
			_worldHoverTooltip?.UpdateHover(hoverMotion.Position,_gridRenderer,GameplaySessionV3.SessionRevision,_constructionPlacement?.IsPlacementActive==true);
		}

		if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
			&& !IsWorldInputLocked()
			&& !IsWorldMapOverlayOpen()
			&& _mercenaryInputController?.TryHandleUnhandledInput(@event) == true)
		{
			if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
			{
				_selectionInspectPanel?.ClearSelection();
			}
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
			&& !IsWorldInputLocked()
			&& !IsWorldMapOverlayOpen()
			&& _constructionPlacement?.IsPlacementActive!=true
			&& _gridRenderer != null
			&& _selectionInspectPanel?.TryHandleWorldInput(@event,_gridRenderer,GameplaySessionV3.SessionRevision)==true)
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_worldManager?.PlanVersion == WorldPlanVersionV2.V3
			&& @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
		{
			_mercenaryInputController?.ClearSelection();
		}

		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
		{
			if (IsWorldInputLocked() || IsWorldMapOverlayOpen())
			{
				return;
			}

			if (mouseButton.ButtonIndex == MouseButton.WheelUp || mouseButton.ButtonIndex == MouseButton.WheelDown)
			{
				return;
			}

			if (_worldManager?.PlanVersion != WorldPlanVersionV2.V3)
			{
				HandleMouseButton(mouseButton);
			}
		}
	}

	private void HandleKey(InputEventKey keyEvent)
	{
		if (_worldManager == null || _buildManager == null)
		{
			return;
		}

		if (keyEvent.CtrlPressed && keyEvent.ShiftPressed && TryHandleGenerationLayerToggle(keyEvent.Keycode))
		{
			return;
		}

		if (keyEvent.CtrlPressed && TryHandleFlatlandRasterToggle(keyEvent.Keycode))
		{
			return;
		}

		if(keyEvent.CtrlPressed&&keyEvent.Keycode==Key.B)
		{
			_baseAreaOverlay?.Toggle();
			_worldManager.UpdateDebugHud(_baseAreaOverlay?.OverlayEnabled==true?"Base area overlay enabled.":"Base area overlay disabled.");
			return;
		}

		switch (keyEvent.Keycode)
		{
			case Key.F1:
				_worldManager.PrintDebugHelp();
				_worldManager.UpdateDebugHud("Printed debug help.");
				break;
			case Key.F2:
				_worldManager.ToggleDebugHudPage();
				break;
			case Key.Key1:
				_buildManager.SelectedBuildType = BuildStructureTypeV2.Floor;
				_worldManager.UpdateDebugHud("Selected Floor.");
				break;
			case Key.Key2:
				_buildManager.SelectedBuildType = BuildStructureTypeV2.Wall;
				_worldManager.UpdateDebugHud("Selected Wall.");
				break;
			case Key.F3:
				_worldManager.CycleOverlayMode();
				break;
			case Key.F4:
				_worldManager.ToggleChunkDebugDisplay();
				break;
			case Key.F5:
				_roomOverlay?.Toggle();
				_worldManager.UpdateDebugHud(_roomOverlay?.OverlayEnabled==true?"Room overlay enabled.":"Room overlay disabled.");
				break;
			case Key.F6:
				_worldManager.PrintSurroundingSectorMetadata();
				_worldManager.UpdateDebugHud("Printed surrounding metadata.");
				break;
			case Key.F7:
				_worldManager.PrintLoadedChunkSummary();
				_worldManager.UpdateDebugHud("Printed loaded chunk summary.");
				break;
			case Key.F8:
				_worldManager.PrintChunkCacheSummary();
				_worldManager.UpdateDebugHud("Printed chunk cache summary.");
				break;
			case Key.F9:
				_worldManager.PrintFlatlandPlanCacheSummary();
				_worldManager.UpdateDebugHud("Printed flatland plan cache summary.");
				break;
			case Key.F10:
				_worldManager.PrintPerformanceSummary();
				_constructionPlacement?.PrintDebugDiagnostics();
				_baseManagementPanel?.PrintDebugDiagnostics();
				PrintResourceEcologyDiagnostics();
				PrintResourceShortageDiagnostics();
				PrintBaseAreaDiagnostics();
				PrintSimulationClockDiagnostics();
				_mercenarySchedulePanel?.PrintDebugDiagnostics();
				PrintDayNightVisualDiagnostics();
				_worldManager.UpdateDebugHud("Printed performance summary.");
				_worldManager.PrintDebugHudDiagnostics();
				break;
			case Key.F11:
				_worldManager.RegenerateVisibleChunks(clearRuntimeStructures: false);
				_stockpileOverlay?.Refresh();
				_constructionOverlay?.Refresh();
				_farmOverlay?.Refresh();
				_roomOverlay?.Refresh();
				if(_baseAreaOverlay?.OverlayEnabled==true)_baseAreaOverlay.QueueRedraw();
				_constructionPlacement?.RefreshPreview();
				break;
			case Key.F12:
				_worldManager.RegenerateVisibleChunks(clearRuntimeStructures: true);
				_stockpileOverlay?.Refresh();
				_constructionOverlay?.Refresh();
				_farmOverlay?.Refresh();
				_roomOverlay?.Refresh();
				if(_baseAreaOverlay?.OverlayEnabled==true)_baseAreaOverlay.QueueRedraw();
				_constructionPlacement?.RefreshPreview();
				break;
			case Key.Home:
				CenterCameraOnPlayerStart();
				break;
		}
	}

	private bool TryHandleGenerationLayerToggle(Key key)
	{
		int number = key switch
		{
			Key.Key1 => 1,
			Key.Key2 => 2,
			Key.Key3 => 3,
			Key.Key4 => 4,
			Key.Key5 => 5,
			Key.Key6 => 6,
			Key.Key7 => 7,
			Key.Key8 => 8,
			Key.Key9 => 9,
			Key.Key0 => 0,
			_ => -1
		};

		if (!WorldGenerationLayerSettingsV2.ToggleByNumber(number, out string label))
		{
			return false;
		}

		_worldManager?.RegenerateVisibleChunks(clearRuntimeStructures: true);
		_worldManager?.UpdateDebugHud($"Toggled generation layer {label}. {WorldGenerationLayerSettingsV2.GetSummary()}");
		return true;
	}

	private bool TryHandleFlatlandRasterToggle(Key key)
	{
		string label;
		switch (key)
		{
			case Key.Key1:
				FlatlandWorldPlanV2.DisableVillageRaster = !FlatlandWorldPlanV2.DisableVillageRaster;
				label = "village raster";
				break;
			case Key.Key2:
				FlatlandWorldPlanV2.DisableSiteRaster = !FlatlandWorldPlanV2.DisableSiteRaster;
				label = "site raster";
				break;
			case Key.Key3:
				FlatlandWorldPlanV2.DisableRoadRaster = !FlatlandWorldPlanV2.DisableRoadRaster;
				label = "road raster";
				break;
			case Key.Key4:
				FlatlandWorldPlanV2.DisableForestRaster = !FlatlandWorldPlanV2.DisableForestRaster;
				label = "forest raster";
				break;
			case Key.Key5:
				FlatlandWorldPlanV2.DisableVillageContext = !FlatlandWorldPlanV2.DisableVillageContext;
				label = "village context";
				break;
			case Key.Key6:
				FlatlandWorldPlanV2.DisableSiteContext = !FlatlandWorldPlanV2.DisableSiteContext;
				label = "site context";
				break;
			case Key.Key7:
				FlatlandWorldPlanV2.DisableRoadContext = !FlatlandWorldPlanV2.DisableRoadContext;
				label = "road context";
				break;
			case Key.Key8:
				FlatlandWorldPlanV2.DisableForestContext = !FlatlandWorldPlanV2.DisableForestContext;
				label = "forest context";
				break;
			default:
				return false;
		}

		_worldManager?.RegenerateVisibleChunks(clearRuntimeStructures: false);
		_worldManager?.UpdateDebugHud($"Toggled {label}. {FlatlandWorldPlanV2.GetDebugToggleSummary()}");
		return true;
	}

	private void HandleMouseButton(InputEventMouseButton mouseButton)
	{
		if (_camera == null || _gridRenderer == null || _buildManager == null || _worldManager == null)
		{
			return;
		}

		Vector2 worldPosition = GetGlobalMousePosition();
		Vector2I cell = _gridRenderer.WorldToCell(worldPosition);

		if (mouseButton.ButtonIndex == MouseButton.Left)
		{
			bool placed = _buildManager.TryPlaceStructure(cell, _buildManager.SelectedBuildType, _buildManager.DefaultOwnerId, out string reason);
			_worldManager.UpdateDebugHud(placed ? $"Placed {_buildManager.SelectedBuildType} at {cell}." : reason);
		}
		else if (mouseButton.ButtonIndex == MouseButton.Right)
		{
			bool removed = _buildManager.TryRemoveStructure(cell);
			_worldManager.UpdateDebugHud(removed ? $"Removed structure at {cell}." : $"No structure at {cell}.");
		}
	}

	private void CenterCameraOnPlayerStart()
	{
		if (_camera == null || _gridRenderer == null || _worldManager == null)
		{
			return;
		}

		_camera.CenterOnGlobalCell(_worldManager.PlayerStartGlobalCell);
	}

	private void MaterializeLocalMercenaries()
	{
		if (_worldManager == null
			|| _gridRenderer == null
			|| _mercenariesContainer == null
			|| !_worldManager.TryGetMercenarySession(out MercenarySessionV3? mercenarySession)
			|| mercenarySession == null
			|| !_worldManager.TryGetLocalDeployment(out GameplayV3.Deployment.CompanyDeploymentStateV3? deployment)
			|| deployment == null)
		{
			return;
		}

		MercenaryMaterializationResultV3 result = _mercenaryMaterializationCoordinator.MaterializeCompany(
			_worldManager.LocalCompanyId,
			deployment,
			mercenarySession,
			_mercenariesContainer,
			_gridRenderer,
			_mercenaryViewRegistry);
		_worldManager.SetMercenaryRuntimeDiagnostics(
			_mercenaryViewRegistry.GetAllViewIds(),
			_mercenaryViewRegistry.Count,
			_mercenaryViewRegistry.DuplicateViewRejectedCount,
			result.DeploymentMismatchCount);

		foreach (string mercenaryId in result.CreatedMercenaryIds)
		{
			if (_mercenaryViewRegistry.TryGetView(mercenaryId, out MercenaryEntityV3? view) && view != null)
			{
				GD.Print($"[MercenaryCoreV3]\nMercenary materialized:\nMercenaryId={mercenaryId}\nWorldPosition={view.Position}");
			}
		}

		if (!result.Succeeded)
		{
			GD.PushWarning($"[MercenaryCoreV3] Materialization failed: {result.FailureReason}");
			return;
		}

#if DEBUG
		if (!MercenaryRuntimeSelfCheckV3.TryValidate(
				_worldManager.LocalPlayerId,
				_worldManager.LocalCompanyId,
				deployment,
				mercenarySession,
				_mercenaryViewRegistry,
				_gridRenderer,
				out string runtimeReason))
		{
			GD.PushError($"[MercenaryCoreV3] Runtime self-check FAIL: {runtimeReason}");
		}
		else
		{
			GD.Print("[MercenaryCoreV3] Runtime self-check PASS");
		}
#endif
	}

	private void InitializeMercenaryControlRuntime()
	{
		if (_worldManager?.PlanVersion != WorldPlanVersionV2.V3
			|| _gridRenderer == null
			|| _mercenariesContainer == null
			|| !_worldManager.TryGetMercenaryControlSession(out MercenaryControlSessionV3? controlSession)
			|| controlSession == null)
		{
			return;
		}

		CanvasLayer? canvasLayer = GetNodeOrNull<CanvasLayer>("CanvasLayer");
		Node2D? gameplayEntities = GetNodeOrNull<Node2D>("GameplayEntitiesV3");
		if (canvasLayer == null || gameplayEntities == null)
		{
			return;
		}

		IMercenaryNavigationWorldQueryV3 navigationQuery = new MercenaryNavigationWorldQueryV3(
			_worldManager.WorldBounds,
			_worldManager.SampleV3PlanCellForNavigation);
		if(_worldManager.TryGetConstructionSession(out ConstructionSessionV3? navigationConstruction)&&navigationConstruction!=null)
			navigationQuery=new FloorNavigationQueryV3(new DynamicStructureNavigationQueryV3(navigationQuery,navigationConstruction.Structures),navigationConstruction.Floors,navigationConstruction.FloorDefinitions);
		_navigationQuery=navigationQuery;

		_mercenaryDragOverlay = new MercenaryDragSelectionOverlayV3 { Name = "MercenaryDragSelectionOverlayV3" };
		canvasLayer.AddChild(_mercenaryDragOverlay);

		_mercenaryCommandMarker = new MercenaryCommandMarkerV3 { Name = "MercenaryCommandMarkerV3", Visible = false };
		gameplayEntities.AddChild(_mercenaryCommandMarker);

		_mercenaryInputController = new MercenaryInputControllerV3 { Name = "MercenaryInputControllerV3" };
		gameplayEntities.AddChild(_mercenaryInputController);
		_mercenaryInputController.Initialize(
			controlSession,
			_mercenaryViewRegistry,
			_gridRenderer,
			_worldManager,
			navigationQuery,
			_mercenaryDragOverlay,
			_mercenaryCommandMarker);

		_mercenaryMovementRuntime = new MercenaryMovementRuntimeV3 { Name = "MercenaryMovementRuntimeV3" };
		gameplayEntities.AddChild(_mercenaryMovementRuntime);
		_mercenaryMovementRuntime.Initialize(
			controlSession,
			_mercenaryViewRegistry,
			_gridRenderer,
			navigationQuery,
			_worldManager);
		MercenaryNeedsSessionV3? needsSession=null;
		if(GameplaySessionV3.TryGetNeedsSession(out needsSession)&&needsSession!=null&&_worldManager.TryGetMercenarySession(out MercenarySessionV3? needsMercenaries)&&needsMercenaries!=null)
		{
			_mercenaryMovementRuntime.AttachRuntimeSpeedMultiplier(needsSession.MovementMultiplier);
			_mercenaryNeedsRuntime=new MercenaryNeedsRuntimeV3{Name="MercenaryNeedsRuntimeV3"};gameplayEntities.AddChild(_mercenaryNeedsRuntime);_mercenaryNeedsRuntime.Initialize(needsSession,needsMercenaries);
			if(MercenaryNeedsSelfCheckV3.TryValidate(out string needsReason))GD.Print("[MercenaryNeedsV3] self-check PASS");else GD.PushError($"[MercenaryNeedsV3] self-check FAIL: {needsReason}");
		}

		if (_worldManager.TryGetResourceSession(out ResourceSessionV3? resources) && resources != null
			&& _worldManager.TryGetMercenaryWorkSession(out MercenaryWorkSessionV3? work) && work != null
			&& _worldManager.TryGetStockpileSession(out StockpileSessionV3? stockpiles) && stockpiles != null
			&& _worldManager.TryGetMercenarySession(out MercenarySessionV3? mercenaries) && mercenaries != null
			&& _worldManager.TryGetConstructionSession(out ConstructionSessionV3? construction) && construction != null
			&& _groundResourcesContainer != null && _stockpileContainer != null)
		{
			_mercenaryInputController.AttachGathering(resources,_resourceNodeViews,work);
			if(needsSession!=null)work.AttachStartPolicy((id,type)=>(needsSession.CanStartWork(id,type,out string reason),reason));
			_stockpileOverlay=new StockpileOverlayV3{Name="StockpileOverlayV3"};_stockpileContainer.AddChild(_stockpileOverlay);_stockpileOverlay.Initialize(stockpiles,_gridRenderer,_worldManager.LocalCompanyId);
			_stockpileDesignation=new StockpileDesignationControllerV3{Name="StockpileDesignationControllerV3"};gameplayEntities.AddChild(_stockpileDesignation);_stockpileDesignation.Initialize(stockpiles,resources,navigationQuery,_gridRenderer,_worldManager,_stockpileOverlay,canvasLayer);
			_constructionUi=new ConstructionUiV3{Name="ConstructionUiV3"};canvasLayer.AddChild(_constructionUi);_constructionUi.Initialize(_stockpileDesignation,_worldManager);
			_mercenaryInputController.AttachStockpileAndHauling(_groundStackViews,_stockpileDesignation);
			_mercenaryWorkRuntime=new MercenaryWorkRuntimeV3{Name="MercenaryWorkRuntimeV3"};
			gameplayEntities.AddChild(_mercenaryWorkRuntime);
			_mercenaryWorkRuntime.Initialize(work,controlSession,resources,mercenaries,navigationQuery,_resourceNodeViews,_groundStackViews,_mercenaryViewRegistry,_groundResourcesContainer,_gridRenderer,_worldManager,needsSession);
			_mercenaryMovementRuntime?.AttachDoorPassages(construction.DoorPassages);
			_doorPassageRuntime=new DoorPassageRuntimeV3{Name="DoorPassageRuntimeV3"};gameplayEntities.AddChild(_doorPassageRuntime);_doorPassageRuntime.Initialize(construction);
			_constructionOverlay=new ConstructionWorldOverlayV3{Name="ConstructionWorldOverlayV3"};gameplayEntities.AddChild(_constructionOverlay);_constructionOverlay.Initialize(construction,_gridRenderer);
			GameplaySessionV3.TryGetFarmSession(out FarmSessionV3? floorFarm);
			_floorRuntime=new FloorConstructionRuntimeV3{Name="FloorConstructionRuntimeV3"};gameplayEntities.AddChild(_floorRuntime);_floorRuntime.Initialize(construction,resources,floorFarm,navigationQuery,_gridRenderer,_worldManager);
			if(GameplaySessionV3.TryGetRoomSession(out RoomSessionV3? rooms)&&rooms!=null)
			{
				_roomRuntime=new RoomRuntimeV3{Name="RoomRuntimeV3"};gameplayEntities.AddChild(_roomRuntime);_roomRuntime.Initialize(rooms,construction,stockpiles,needsSession,_worldManager.WorldBounds);_roomRuntime.AttachStockpileEvents();
				_roomOverlay=new RoomOverlayV3{Name="RoomOverlayV3"};gameplayEntities.AddChild(_roomOverlay);_roomOverlay.Initialize(rooms,_gridRenderer);
#if DEBUG
				if(RoomSelfCheckV3.TryValidate(out string roomReason)&&RoomSelfCheckV3.TryValidateRuntime(rooms,_worldManager.WorldBounds,out roomReason))GD.Print($"[RoomV3] self-check PASS {RoomSelfCheckV3.LastPerformanceSummary}");else GD.PushError($"[RoomV3] self-check FAIL: {roomReason}");
#endif
			}
#if DEBUG
			if(FloorSelfCheckV3.TryValidate(out string floorReason))GD.Print($"[FloorV3] self-check PASS {FloorSelfCheckV3.LastPerformanceSummary}");else GD.PushError($"[FloorV3] self-check FAIL: {floorReason}");
#endif
			_constructionPlacement=new ConstructionPlacementControllerV3{Name="ConstructionPlacementControllerV3"};gameplayEntities.AddChild(_constructionPlacement);_constructionPlacement.Initialize(construction,resources,stockpiles,mercenaries,navigationQuery,_gridRenderer,_worldManager,_constructionOverlay);
			if(GameplaySessionV3.TryGetFarmSession(out FarmSessionV3? farm)&&farm!=null)
			{
				_farmOverlay=new FarmWorldOverlayV3{Name="FarmWorldOverlayV3"};gameplayEntities.AddChild(_farmOverlay);_farmOverlay.Initialize(farm,_gridRenderer);
				_farmDesignation=new FarmDesignationControllerV3{Name="FarmDesignationControllerV3"};gameplayEntities.AddChild(_farmDesignation);_farmDesignation.Initialize(farm,construction,resources,stockpiles,navigationQuery,_gridRenderer,_worldManager,_farmOverlay);
				_farmGrowthRuntime=new FarmGrowthRuntimeV3{Name="FarmGrowthRuntimeV3"};gameplayEntities.AddChild(_farmGrowthRuntime);_farmGrowthRuntime.Initialize(farm);
				_farmingWork=new FarmingWorkCoordinatorV3(farm,resources,work,controlSession,mercenaries,navigationQuery,_worldManager.LocalPlayerId,_worldManager.LocalCompanyId);
				if(needsSession!=null)_farmingWork.AttachNeedsMultiplier(needsSession.WorkMultiplier);
				_farmingWork.Changed+=()=>_farmOverlay?.Refresh();
				_farmingWork.ResourcesChanged+=MaterializeResources;
			}
			if(GameplaySessionV3.TryGetBaseAreaSession(out BaseAreaSessionV3? baseAreas)&&baseAreas!=null&&GameplaySessionV3.TryGetCompanySession(out CompanySessionV3? baseCompanies)&&baseCompanies!=null&&GameplaySessionV3.TryGetFarmSession(out FarmSessionV3? baseFarm)&&baseFarm!=null&&GameplaySessionV3.TryGetRoomSession(out RoomSessionV3? baseRooms)&&baseRooms!=null)
			{
				_baseAreaRuntime=new BaseAreaRuntimeV3{Name="BaseAreaRuntimeV3"};gameplayEntities.AddChild(_baseAreaRuntime);_baseAreaRuntime.Initialize(baseAreas,baseCompanies,construction,stockpiles,baseFarm,baseRooms,_worldManager.WorldBounds);
				_baseAreaOverlay=new BaseAreaOverlayV3{Name="BaseAreaOverlayV3"};gameplayEntities.AddChild(_baseAreaOverlay);_baseAreaOverlay.Initialize(baseAreas,_gridRenderer);
#if DEBUG
				if(BaseAreaSelfCheckV3.TryValidate(out string baseReason))GD.Print($"[BaseAreaV3] self-check PASS {BaseAreaSelfCheckV3.LastPerformanceSummary}");else GD.PushError($"[BaseAreaV3] self-check FAIL: {baseReason}");
				if(BaseRoleSelfCheckV3.TryValidate(out string roleReason))GD.Print($"[BaseRoleV3] self-check PASS {BaseRoleSelfCheckV3.LastValidationSummary}");else GD.PushError($"[BaseRoleV3] self-check FAIL: {roleReason}");
				if(FacilityAffiliationSelfCheckV3.TryValidate(out string facilityReason))GD.Print("[FacilityAffiliationV3] self-check PASS");else GD.PushError($"[FacilityAffiliationV3] self-check FAIL: {facilityReason}");
				if(MercenaryBaseAffiliationSelfCheckV3.TryValidate(out string mercenaryBaseReason))GD.Print("[MercenaryBaseAffiliationV3] self-check PASS");else GD.PushError($"[MercenaryBaseAffiliationV3] self-check FAIL: {mercenaryBaseReason}");
#endif
			}
			if(GameplaySessionV3.TryGetFarmSession(out FarmSessionV3? ecologyFarm)&&ecologyFarm!=null&&GameplaySessionV3.TryGetRoomSession(out RoomSessionV3? ecologyRooms)&&ecologyRooms!=null&&GameplaySessionV3.TryGetCompanySession(out CompanySessionV3? ecologyCompanies)&&ecologyCompanies!=null)
			{
				ResourceEcologySessionV3 ecology=GameplaySessionV3.EnsureResourceEcologySession(_worldManager.WorldSeed,_worldManager.WorldBounds,OS.GetEnvironment("RESOURCE_ECOLOGY_TEST_SPEED")=="1");
				_resourceEcologyRuntime=new ResourceEcologyRuntimeV3{Name="ResourceEcologyRuntimeV3"};gameplayEntities.AddChild(_resourceEcologyRuntime);_resourceEcologyRuntime.Initialize(ecology,resources,construction,ecologyFarm,stockpiles,ecologyRooms,mercenaries,navigationQuery,ecologyCompanies);
				foreach(ChunkDataV2 knownChunk in _ecologyChunks.Values)_resourceEcologyRuntime.OnChunkDataReady(knownChunk);if(_streamManager!=null)foreach(Vector2I loadedCoord in _streamManager.GetLoadedChunkCoords())if(_ecologyChunks.TryGetValue(loadedCoord,out ChunkDataV2? loadedChunk))_resourceEcologyRuntime.OnChunkAttached(loadedChunk);
				_resourceEcologyRuntime.ResourceSpawned+=id=>{if(_resourceNodesContainer==null||_gridRenderer==null)return;ResourceMaterializationCoordinatorV3.MaterializeNodes(new[]{id},resources,_resourceNodesContainer,_gridRenderer,_resourceNodeViews);_worldManager.SetResourceRuntimeDiagnostics(_resourceNodeViews.GetIds(),_groundStackViews.GetIds());};
#if DEBUG
				if(ResourceEcologySelfCheckV3.TryValidate(out string ecologyReason)&&ResourceEcologySafetySelfCheckV3.TryValidate(out ecologyReason)&&ResourceRenewalExclusionSelfCheckV3.TryValidate(out ecologyReason))GD.Print("[ResourceEcologyV3] self-check PASS");else GD.PushError($"[ResourceEcologyV3] self-check FAIL: {ecologyReason}");
				if(OS.GetEnvironment("RESOURCE_ECOLOGY_PERF_FIXTURE")=="1"){GD.Print($"[ResourceEcologyV3] performance fixture {ResourceEcologyPerformanceFixtureV3.Run()}");GD.Print($"[ResourceEcologyV3] safety performance fixture {ResourceEcologySafetyPerformanceFixtureV3.Run()}");}
#endif
			}
			_constructionWork=new ConstructionWorkCoordinatorV3(construction,resources,stockpiles,work,controlSession,mercenaries,navigationQuery,_worldManager);if(GameplaySessionV3.TryGetWorkToolReservations(out var tools)&&tools!=null)_constructionWork.AttachToolReservations(tools);_constructionWork.Changed+=()=>{_constructionOverlay?.Refresh();_restAssignmentOverlay?.Refresh();};_constructionWork.ResourcesChanged+=MaterializeResources;
			if(!GameplaySessionV3.TryGetProductionSession(out ProductionSessionV3? production)||production==null)return;
			if(GameplaySessionV3.TryGetEquipmentRuntime(out EquipmentRuntimeV3? equipment)&&equipment!=null)production.AttachEquipmentRuntime(equipment,_worldManager.WorldSeed,out _);
			_productionWork=new ProductionWorkCoordinatorV3(production,construction,resources,stockpiles,work,controlSession,mercenaries,navigationQuery,_worldManager.LocalPlayerId,_worldManager.LocalCompanyId);_productionWork.ResourcesChanged+=MaterializeResources;
			if(needsSession!=null)_constructionWork.AttachWorkMultiplier(needsSession.WorkMultiplier);
			_demolitionWork=new DemolitionWorkCoordinatorV3(construction,resources,work,controlSession,mercenaries,navigationQuery,_worldManager.LocalPlayerId,_worldManager.LocalCompanyId,_worldManager.WorldBounds,id=>{_constructionWork.CancelForDirectMove(id);_eatingWork?.Cancel(id,"SupersededByNewWork");},true);_demolitionWork.Changed+=()=>{_constructionOverlay?.Refresh();_restAssignmentOverlay?.Refresh();};_demolitionWork.ResourcesChanged+=MaterializeResources;
			if(GameplaySessionV3.TryGetEquipmentRuntime(out var equipmentRuntime)&&equipmentRuntime!=null)_demolitionWork.AttachEquipmentOutputBlocker(id=>equipmentRuntime.GetEquipmentOutputCount(id)>0);
			_floorRuntime.AttachCoordinators(_constructionWork,_demolitionWork);
			if(needsSession!=null)_demolitionWork.AttachWorkMultiplier(needsSession.WorkMultiplier);
			if(needsSession!=null){_restWork=new RestWorkCoordinatorV3(needsSession,mercenaries,controlSession,work,construction,navigationQuery);_eatingWork=new EatingWorkCoordinatorV3(needsSession,resources,mercenaries,controlSession,work,navigationQuery,_worldManager.LocalPlayerId,_worldManager.LocalCompanyId);if(GameplaySessionV3.TryGetJobActivityRangePolicy(out JobActivityRangePolicyV3? needPolicy)&&needPolicy!=null)_eatingWork.AttachBasePreference((id,cell)=>needPolicy.GetNeedPreferenceRank(id,_worldManager.LocalCompanyId,cell));_eatingWork.ResourcesChanged+=MaterializeResources;_restWork.AttachEatingCancellation(id=>_eatingWork.Cancel(id,"SupersededByRest"));_mercenaryNeedsRuntime?.AttachRestCoordinator(_restWork);_mercenaryNeedsRuntime?.AttachEatingCoordinator(_eatingWork);_demolitionWork.AttachRestLifecycle(_restWork.OnStructureDemolitionStarted,_restWork.OnStructureDemolitionEnded);_restAssignmentOverlay=new RestAssignmentOverlayV3{Name="RestAssignmentOverlayV3"};gameplayEntities.AddChild(_restAssignmentOverlay);_restAssignmentOverlay.Initialize(construction,needsSession,mercenaries,_gridRenderer,_worldManager.LocalCompanyId);construction.Structures.StructureRemoved+=removed=>{if(construction.Definitions.TryGetDefinition(removed.DefinitionId,out var removedDefinition)&&removedDefinition!=null)needsSession.RemoveStructure(removed.StructureId,removedDefinition,removed);_restAssignmentOverlay?.Refresh();};controlSession.Selection.SelectionChanged+=CancelBedAssignmentMode;}
			_demolitionDesignation=new DemolitionDesignationControllerV3{Name="DemolitionDesignationControllerV3"};gameplayEntities.AddChild(_demolitionDesignation);_demolitionDesignation.Initialize(construction,_gridRenderer,_worldManager,_constructionOverlay,_demolitionWork);
			controlSession.AttachConstructionCancellation(id=>{bool changed=_constructionWork.CancelForDirectMove(id)|_demolitionWork.CancelForDirectMove(id)|_productionWork.CancelForDirectMove(id);if(_restWork?.Cancel(id,"CancelledByDirectMove")==true)changed=true;if(_eatingWork?.Cancel(id,"CancelledByDirectMove")==true)changed=true;if(_farmingWork?.Cancel(id,"CancelledByDirectMove")==true)changed=true;return changed;});
			work.AttachExternalWorkSupersede(id=>{_demolitionWork.CancelForNewWork(id);_restWork?.Cancel(id,"SupersededByNewWork");_eatingWork?.Cancel(id,"SupersededByNewWork");_farmingWork?.Cancel(id,"SupersededByNewWork");});
			_constructionUi.ConstructionToolChanged+=HandleConstructionToolChanged;
			_constructionUi.TrayVisibilityChanged+=OnConstructionTrayVisibilityChanged;
			_constructionUi.ConstructionMaterialChanged+=material=>{if(_constructionPlacement==null)return;if(!_constructionPlacement.TrySetMaterial(material,out string reason))_worldManager.UpdateDebugHud(reason);};
			_constructionUi.DemolitionToolChanged+=active=>{if(active){_constructionPlacement?.ClearActivePlacementTool();_farmDesignation?.SetActive(false);}_demolitionDesignation?.SetActive(active);};
			_constructionUi.FarmToolChanged+=active=>{if(active){_constructionPlacement?.ClearActivePlacementTool();_demolitionDesignation?.SetActive(false);_stockpileDesignation?.SetMode(StockpileDesignationModeV3.None);}_farmDesignation?.SetActive(active);};
			if(_farmDesignation!=null)_farmDesignation.ActiveChanged+=active=>{if(!active&&_constructionUi?.ActiveConstructionTool=="PotatoFarm")_constructionUi.SetFarmTool(false);};
			_constructionPlacement.ActiveChanged+=active=>{if(active||_constructionUi==null)return;if(_constructionUi.ActiveConstructionTool=="WoodenWall")_constructionUi.SetWallTool(false);else if(_constructionUi.ActiveConstructionTool=="WoodenDoor")_constructionUi.SetDoorTool(false);else if(_constructionUi.ActiveConstructionTool=="BasicBed")_constructionUi.SetBedTool(false);};
			_floorRuntime.ActiveChanged+=active=>{if(active||_constructionUi==null)return;if(_constructionUi.ActiveConstructionTool=="WoodenFloor")_constructionUi.SetFloorTool(false);else if(_constructionUi.ActiveConstructionTool=="FloorRemoval")_constructionUi.SetFloorRemovalTool(false);};
			_demolitionDesignation.ActiveChanged+=active=>{if(!active&&_constructionUi?.ActiveConstructionTool=="Demolition")_constructionUi.SetDemolitionTool(false);};
			_mercenaryWorkRuntime.AttachConstruction(_constructionWork);
			_mercenaryWorkRuntime.AttachProduction(_productionWork);
			_mercenaryWorkRuntime.AttachDemolition(_demolitionWork);
			if(_restWork!=null)_mercenaryWorkRuntime.AttachRest(_restWork);
			if(_eatingWork!=null){_mercenaryWorkRuntime.AttachEating(_eatingWork);_worldManager.BindEatingCoordinator(_eatingWork);}
			if(_farmingWork!=null)_mercenaryWorkRuntime.AttachFarming(_farmingWork);
			if(_farmingWork!=null&&GameplaySessionV3.TryGetFarmSession(out FarmSessionV3? jobFarm)&&jobFarm!=null&&GameplaySessionV3.TryGetJobManager(out JobManagerV3? jobManager)&&jobManager!=null)
			{
				_jobManagerRuntime=new JobManagerRuntimeV3{Name="JobManagerRuntimeV3"};gameplayEntities.AddChild(_jobManagerRuntime);
				_jobManagerRuntime.Initialize(jobManager,resources,stockpiles,construction,jobFarm,mercenaries,controlSession,work,needsSession,_constructionWork,_demolitionWork,_farmingWork,production,_productionWork,navigationQuery,_worldManager,_streamManager?.GetLoadedChunkCoords());
				if(_baseAreaOverlay!=null&&GameplaySessionV3.TryGetFacilityAffiliationSession(out FacilityAffiliationSessionV3? debugFacilities)&&debugFacilities!=null&&GameplaySessionV3.TryGetMercenaryBaseAffiliationSession(out MercenaryBaseAffiliationSessionV3? debugMercenaries)&&debugMercenaries!=null&&GameplaySessionV3.TryGetJobActivityRangePolicy(out JobActivityRangePolicyV3? debugPolicy)&&debugPolicy!=null)_baseAreaOverlay.BindActivityDebug(debugFacilities,debugMercenaries,mercenaries,_mercenaryViewRegistry,jobManager,debugPolicy,_worldManager.LocalCompanyId);
				if(JobManagerSelfCheckV3.TryValidate(out string jobReason))GD.Print($"[JobManagerV3] self-check PASS 100x1000={JobManagerSelfCheckV3.Last100WorkerCandidateEvaluations} candidates/{JobManagerSelfCheckV3.Last100WorkerMilliseconds:0.00}ms 300x5000={JobManagerSelfCheckV3.Last300WorkerCandidateEvaluations} candidates/{JobManagerSelfCheckV3.Last300WorkerMilliseconds:0.00}ms mainThreadChunkGenerationCount={_streamManager?.MainThreadChunkGenerationCount??0}");else GD.PushError($"[JobManagerV3] self-check FAIL: {jobReason}");
				if(JobActivityRangeSelfCheckV3.TryValidate(out string activityReason))GD.Print("[JobActivityRangeV3] self-check PASS");else GD.PushError($"[JobActivityRangeV3] self-check FAIL: {activityReason}");
			}
			_mercenaryInspectHud=new MercenaryInspectHudV3{Name="MercenaryInspectHudV3"};
			canvasLayer.AddChild(_mercenaryInspectHud);
			_mercenaryInspectHud.Initialize(controlSession,mercenaries,work,_worldManager,_constructionWork,_demolitionWork,needsSession,_restWork,_eatingWork,_farmingWork);
			_mercenaryInspectHud.ScheduleRequested+=OnScheduleRequested;
			_baseManagementPanel=new BaseManagementPanelV3{Name="BaseManagementPanelV3"};
			canvasLayer.AddChild(_baseManagementPanel);
			_baseManagementPanel.Initialize(_worldManager,_camera,_streamManager);
			_baseManagementPanel.PanelOpened+=OnBaseManagementOpened;
			_mercenarySchedulePanel=new MercenarySchedulePanelV3{Name="MercenarySchedulePanelV3"};
			canvasLayer.AddChild(_mercenarySchedulePanel);
			_mercenarySchedulePanel.Initialize(_worldManager);
			GameplaySessionV3.EnsureFrontierSurvivalSession(_worldManager.LocalCompanyId);
			_frontierSurvivalPanel=new FrontierSurvivalPanelV3{Name="FrontierSurvivalPanelV3"};canvasLayer.AddChild(_frontierSurvivalPanel);_frontierSurvivalPanel.Initialize(_worldManager);
			_productionPanel=new ProductionPanelV3{Name="ProductionPanelV3"};canvasLayer.AddChild(_productionPanel);_productionPanel.Initialize(_worldManager);_productionPanel.PanelOpened+=OnProductionPanelOpened;
			_selectionInspectPanel=new SelectionInspectPanelV3{Name="SelectionInspectPanelV3"};canvasLayer.AddChild(_selectionInspectPanel);_selectionInspectPanel.Initialize(resources,construction,work,id=>_productionPanel?.Open(id),production,id=>TryIssueDirectProduction(id),()=>controlSession.Selection.Count>0,id=>_jobManagerRuntime?.GetProductionBlockReasonText(id)??string.Empty,equipment,id=>TryIssueDirectEquipment(id),id=>TryIssueEquipmentHauling(id));
			_worldHoverTooltip=new WorldHoverTooltipV3{Name="WorldHoverTooltipV3"};canvasLayer.AddChild(_worldHoverTooltip);_worldHoverTooltip.Initialize(resources,construction,work);
#if DEBUG
			if(StarterProductionUiSelfCheckV3.TryValidate(out string productionUiReason))GD.Print("[ProductionV3] UI self-check PASS");else GD.PushError($"[ProductionV3] UI self-check FAIL: {productionUiReason}");
#endif
			if(GameplaySessionV3.TryGetJobManager(out JobManagerV3? priorityJobs)&&priorityJobs!=null)
			{
				_workPriorityPanel=new MercenaryWorkPriorityPanelV3{Name="MercenaryWorkPriorityPanelV3"};canvasLayer.AddChild(_workPriorityPanel);_workPriorityPanel.Initialize(priorityJobs,controlSession);
				_mercenaryInspectHud.WorkPriorityRequested+=id=>_workPriorityPanel.Toggle(id);
			}
			_mercenaryInspectHud.BedAssignmentRequested+=id=>{_pendingBedAssignmentMercenaryId=id;_restAssignmentOverlay?.SetAssignmentMode(true,id);_constructionPlacement?.ClearActivePlacementTool();_demolitionDesignation?.SetActive(false);_stockpileDesignation?.SetMode(StockpileDesignationModeV3.None);_worldManager.UpdateDebugHud("배정할 간이 침대를 클릭하세요.");};
		}
	}

	private void HandleConstructionToolChanged(ConstructionPlacementToolKindV3 toolKind,bool active)
	{
		if(_constructionPlacement==null)return;
		if(toolKind is ConstructionPlacementToolKindV3.WoodenFloor or ConstructionPlacementToolKindV3.FloorRemoval)
		{
			if(active){_constructionPlacement.ClearActivePlacementTool();_farmDesignation?.SetActive(false);_demolitionDesignation?.SetActive(false);_stockpileDesignation?.SetMode(StockpileDesignationModeV3.None);_floorRuntime?.SetTool(toolKind==ConstructionPlacementToolKindV3.WoodenFloor?FloorToolModeV3.Build:FloorToolModeV3.Remove);}
			else if((_floorRuntime?.Mode==FloorToolModeV3.Build&&toolKind==ConstructionPlacementToolKindV3.WoodenFloor)||(_floorRuntime?.Mode==FloorToolModeV3.Remove&&toolKind==ConstructionPlacementToolKindV3.FloorRemoval))_floorRuntime.SetTool(FloorToolModeV3.None);
			return;
		}
		if(active)_floorRuntime?.SetTool(FloorToolModeV3.None);
		if(toolKind==ConstructionPlacementToolKindV3.None){if(active)_constructionPlacement.ClearActivePlacementTool();return;}
		if(active)
		{
			_farmDesignation?.SetActive(false);
			string definitionId=toolKind switch{ConstructionPlacementToolKindV3.WoodenWall=>StructureDefinitionCatalogV3.WoodenWallId,ConstructionPlacementToolKindV3.WoodenDoor=>StructureDefinitionCatalogV3.WoodenDoorId,ConstructionPlacementToolKindV3.BasicBed=>StructureDefinitionCatalogV3.BasicBedId,ConstructionPlacementToolKindV3.ProcessingWorkbench=>StructureDefinitionCatalogV3.ProcessingWorkbenchId,ConstructionPlacementToolKindV3.BasicFurnace=>StructureDefinitionCatalogV3.BasicFurnaceId,ConstructionPlacementToolKindV3.FieldKitchen=>StructureDefinitionCatalogV3.FieldKitchenId,ConstructionPlacementToolKindV3.ApothecaryTable=>StructureDefinitionCatalogV3.ApothecaryTableId,_=>string.Empty};
			_constructionPlacement.SetActivePlacementTool(toolKind,definitionId);
		}
		else if(_constructionPlacement.ActiveToolKind==toolKind)
		{
			_constructionPlacement.ClearActivePlacementTool();
		}
	}

	private void OnConstructionTrayVisibilityChanged(bool open)
	{
		_selectionInspectPanel?.SetConstructionUiBlocked(open);
		_mercenaryInspectHud?.SetConstructionUiBlocked(open);
		if(open)_worldHoverTooltip?.Clear();
	}

	private bool TryHandleDirectEquipment(InputEvent e)
	{
		if(e is not InputEventMouseButton { ButtonIndex:MouseButton.Right,Pressed:true } button||_gridRenderer==null||_navigationQuery==null||_worldManager==null)return false;
		if(!GameplaySessionV3.TryGetEquipmentRuntime(out EquipmentRuntimeV3? equipment)||equipment==null)return false;
		GlobalCellCoord cell=new(_gridRenderer.WorldToCell(GetViewport().GetCanvasTransform().AffineInverse()*button.Position));IReadOnlyList<string> ids=equipment.GetGroundEquipmentAtCell(cell.Value);if(ids.Count==0)ids=equipment.GetStoredEquipmentAtCell(cell.Value);if(ids.Count==0)return false;
		TryIssueDirectEquipment(ids[0]);return true;
	}

	private void TryIssueDirectEquipment(string instanceId)
	{
		if(_worldManager==null||_navigationQuery==null||!_worldManager.TryGetMercenaryControlSession(out MercenaryControlSessionV3? control)||control==null||!_worldManager.TryGetMercenaryWorkSession(out MercenaryWorkSessionV3? work)||work==null)return;
		IReadOnlyList<string> selected=control.Selection.GetSelectedIds();if(selected.Count==0){_worldManager.UpdateDebugHud("\uba3c\uc800 \uc6a9\ubcd1\uc744 \uc120\ud0dd\ud558\uc138\uc694.");return;}
		bool issued=work.TryIssueDirectEquipmentEquip(_worldManager.LocalPlayerId,_worldManager.LocalCompanyId,selected[0],instanceId,_navigationQuery,GameplaySessionV3.SessionRevision,out WorkRequestV3? request,out string reason);_worldManager.UpdateDebugHud(issued?$"\uc7a5\ube44 \uc7a5\ucc29 \uba85\ub839: {request!.AssignedMercenaryId}":EquipmentCommandReasonText(reason));
	}

	private void TryIssueEquipmentHauling(string instanceId)
	{
		if(_worldManager==null||_navigationQuery==null||!_worldManager.TryGetMercenaryControlSession(out MercenaryControlSessionV3? control)||control==null||!_worldManager.TryGetMercenaryWorkSession(out MercenaryWorkSessionV3? work)||work==null)return;
		IReadOnlyList<string> selected=control.Selection.GetSelectedIds();if(selected.Count==0){_worldManager.UpdateDebugHud("\uba3c\uc800 \uc6a9\ubcd1\uc744 \uc120\ud0dd\ud558\uc138\uc694.");return;}
		bool issued=work.TryIssueEquipmentHauling(_worldManager.LocalPlayerId,_worldManager.LocalCompanyId,selected,instanceId,_navigationQuery,GameplaySessionV3.SessionRevision,out WorkRequestV3? request,out string reason);_worldManager.UpdateDebugHud(issued?$"\uc7a5\ube44 \uc6b4\ubc18 \uba85\ub839: {request!.AssignedMercenaryId}":EquipmentCommandReasonText(reason));
	}

	private static string EquipmentCommandReasonText(string reason)=>reason switch
	{
		"EquipmentNotFound"=>"\uc7a5\ube44\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.",
		"InvalidLocation"=>"\uc7a5\ube44 \uc704\uce58\uac00 \uc62c\ubc14\ub974\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4.",
		"WrongOwner"=>"\ub2e4\ub978 \ud68c\uc0ac\uc758 \uc7a5\ube44\uc785\ub2c8\ub2e4.",
		"NoAllowedStorage"=>"\uc7a5\ube44\ub97c \ubcf4\uad00\ud560 \ube44\ucd95 \uad6c\uc5ed\uc774 \uc5c6\uc2b5\ub2c8\ub2e4.",
		"NoPath"=>"\uc7a5\ube44\uae4c\uc9c0 \uc774\ub3d9\ud560 \uacbd\ub85c\uac00 \uc5c6\uc2b5\ub2c8\ub2e4.",
		"Reserved"=>"\ub2e4\ub978 \uc6a9\ubcd1\uc774 \uc7a5\ube44\ub97c \uc608\uc57d\ud588\uc2b5\ub2c8\ub2e4.",
		"InvalidSession"=>"\uc138\uc158\uc774 \ubcc0\uacbd\ub418\uc5b4 \uba85\ub839\uc744 \uc2e4\ud589\ud560 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.",
		_=>reason
	};

	private bool TryHandleDirectProduction(InputEvent e)
	{
		if(e is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } button||_gridRenderer==null||_worldManager==null||_productionWork==null)return false;
		if(!_worldManager.TryGetMercenaryControlSession(out MercenaryControlSessionV3? control)||control==null||control.Selection.Count==0)return false;
		if(!_worldManager.TryGetConstructionSession(out ConstructionSessionV3? construction)||construction==null)return false;
		GlobalCellCoord cell=new(_gridRenderer.WorldToCell(GetViewport().GetCanvasTransform().AffineInverse()*button.Position));
		if(!construction.Structures.TryGetStructureAtCell(cell,out StructureStateV3? structure)||structure==null||!GameplaySessionV3.TryGetProductionSession(out ProductionSessionV3? production)||production==null||!production.TryGetFacility(structure.StructureId,out _))return false;
		TryIssueDirectProduction(structure.StructureId);
		return true;
	}

	private void TryIssueDirectProduction(string facilityId)
	{
		if(_worldManager==null||_productionWork==null||!_worldManager.TryGetMercenaryControlSession(out MercenaryControlSessionV3? control)||control==null)return;
		IReadOnlyList<string> selected=control.Selection.GetSelectedIds();
		if(selected.Count==0){_worldManager.UpdateDebugHud(ProductionCommandReasonText("NoEligibleWorker"));return;}
		if(GameplaySessionV3.TryGetProductionSession(out ProductionSessionV3? production)&&production?.TryGetFacility(facilityId,out ProductionFacilitySnapshotV3? facility)==true&&facility!=null&&GameplaySessionV3.TryGetJobActivityRangePolicy(out JobActivityRangePolicyV3? policy)&&policy!=null)policy.Evaluate(_worldManager.LocalCompanyId,JobTypeV3.Production,JobSourceKindV3.ProductionFacility,facilityId,facility.AnchorCell,selected[0],JobCommandSourceV3.DirectOrder);
		bool issued=_productionWork.TryIssueDirect(facilityId,selected,GameplaySessionV3.SessionRevision,out ProductionWorkHudSnapshotV3 snapshot,out string reason);
		_worldManager.UpdateDebugHud(issued?$"Direct production assigned: {snapshot.MercenaryId}":ProductionCommandReasonText(reason));
	}

	private static string ProductionCommandReasonText(string reason)=>reason switch
	{
		"NoOrder"=>"\uC81C\uC791 \uBA85\uB839\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.",
		"WaitingMaterials" or "NoStoredMaterials"=>"\uC7AC\uB8CC\uB97C \uAE30\uB2E4\uB9AC\uB294 \uC911\uC785\uB2C8\uB2E4.",
		"FacilityReserved"=>"\uB2E4\uB978 \uC6A9\uBCD1\uC774 \uC2DC\uC124\uC744 \uC0AC\uC6A9 \uC911\uC785\uB2C8\uB2E4.",
		"NoPath" or "NoApproachCells"=>"\uC2DC\uC124\uAE4C\uC9C0 \uC774\uB3D9\uD560 \uACBD\uB85C\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",
		"OutputBlocked"=>"\uACB0\uACFC\uBB3C\uC744 \uB193\uC744 \uACF5\uAC04\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.",
		"InvalidSession"=>"\uC138\uC158\uC774 \uBCC0\uACBD\uB418\uC5B4 \uBA85\uB839\uC744 \uAC31\uC2E0\uD588\uC2B5\uB2C8\uB2E4.",
		"OwnershipDenied"=>"\uB2E4\uB978 \uD68C\uC0AC\uC758 \uC2DC\uC124\uC740 \uC870\uC791\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.",
		_=>"\uC81C\uC791 \uAC00\uB2A5\uD55C \uC6A9\uBCD1\uC774 \uC5C6\uC2B5\uB2C8\uB2E4."
	};

	private bool TryHandleFarmingAction(InputEvent e)
	{
		if(_farmDesignation?.Active==true||_farmingWork==null||_gridRenderer==null||_worldManager==null)return false;
		if(e is not InputEventMouseButton click||!click.Pressed||click.ButtonIndex!=MouseButton.Right)return false;
		if(!GameplaySessionV3.TryGetFarmSession(out FarmSessionV3? farm)||farm==null)return false;
		GlobalCellCoord cell=new(_gridRenderer.WorldToCell(GetViewport().GetCanvasTransform().AffineInverse()*click.Position));
		if(!farm.Plots.TryGetCrop(cell,out var crop)||crop==null)return false;
		if(crop.Stage==CropStageV3.Empty&&_worldManager.TryGetResourceSession(out ResourceSessionV3? farmResources)&&farmResources?.GroundStacks.GetStacksAtCell(cell).Count>0)return false;
		IReadOnlyList<string> selected=_worldManager.TryGetMercenaryControlSession(out MercenaryControlSessionV3? control)&&control!=null?control.Selection.GetSelectedIds():Array.Empty<string>();
		if(selected.Count>0&&GameplaySessionV3.TryGetJobActivityRangePolicy(out JobActivityRangePolicyV3? policy)&&policy!=null)policy.Evaluate(_worldManager.LocalCompanyId,JobTypeV3.Sowing,JobSourceKindV3.FarmCell,$"direct:{cell.Value.X}:{cell.Value.Y}",cell,selected[0],JobCommandSourceV3.DirectOrder);if(_farmingWork.TryIssue(cell,selected,out string reason)){_worldManager.UpdateDebugHud("농사 작업 시작");_farmOverlay?.Refresh();}else _worldManager.UpdateDebugHud(reason);
		return true;
	}

	private bool TryHandleBedAssignment(InputEvent e)
	{
		if(string.IsNullOrEmpty(_pendingBedAssignmentMercenaryId))return false;if(e is InputEventKey key&&key.Pressed&&key.Keycode==Key.Escape||e is InputEventMouseButton cancel&&cancel.Pressed&&cancel.ButtonIndex==MouseButton.Right){CancelBedAssignmentMode();return true;}if(e is not InputEventMouseButton click||!click.Pressed||click.ButtonIndex!=MouseButton.Left||_gridRenderer==null||_worldManager==null||_restWork==null)return e is InputEventMouseMotion;GlobalCellCoord cell=new(_gridRenderer.WorldToCell(GetViewport().GetCanvasTransform().AffineInverse()*click.Position));if(!_worldManager.TryGetConstructionSession(out ConstructionSessionV3? construction)||construction==null||!construction.Structures.TryGetStructureAtCell(cell,out var structure)||structure==null){_worldManager.UpdateDebugHud("휴식 시설이 아닙니다.");return true;}if(_restWork.TryAssign(_pendingBedAssignmentMercenaryId,structure.StructureId,out string reason)){_worldManager.UpdateDebugHud("침대가 배정되었습니다.");CancelBedAssignmentMode();}else{_worldManager.UpdateDebugHud(reason);_restAssignmentOverlay?.Refresh();}return true;
	}
	private void CancelBedAssignmentMode(){_pendingBedAssignmentMercenaryId=string.Empty;_restAssignmentOverlay?.SetAssignmentMode(false);}

	private void MaterializeResources()
	{
		if(_worldManager==null||_gridRenderer==null||_resourceNodesContainer==null||_groundResourcesContainer==null||!_worldManager.TryGetResourceSession(out ResourceSessionV3? resources)||resources==null)return;
		foreach(string stale in _groundStackViews.GetIds())if(!resources.GroundStacks.TryGet(stale,out _))_groundStackViews.TryRemove(stale);
		int created=0;if(_streamManager!=null)foreach(Vector2I chunk in _streamManager.GetLoadedChunkCoords())created+=ResourceMaterializationCoordinatorV3.MaterializeNodes(resources.Nodes.GetNodeIdsInChunk(chunk),resources,_resourceNodesContainer,_gridRenderer,_resourceNodeViews);
		foreach(string id in resources.GroundStacks.GetAllStackIds())if(resources.GroundStacks.TryGet(id,out GroundResourceStackV3? stack)&&stack!=null)ResourceMaterializationCoordinatorV3.MaterializeOrRefreshStack(stack,resources,_groundResourcesContainer,_gridRenderer,_groundStackViews)?.SetReserved(resources.AmountReservations.IsReserved(id));
		UpdateResourceViewDiagnostics();
		GD.Print($"[ResourceCoreV3] views nodes={_resourceNodeViews.Count} stacks={_groundStackViews.Count} created={created}");
	}

	private void UpdateResourceViewDiagnostics()
	{
		if(_worldManager==null)return;IReadOnlyList<string> ids=_resourceNodeViews.GetIds();HashSet<Vector2I> rendered=_streamManager==null?new():new(_streamManager.GetLoadedChunkCoords());HashSet<Vector2I> attached=new();int outside=0;
		if(_worldManager.TryGetResourceSession(out ResourceSessionV3? resources)&&resources!=null)foreach(string id in ids)if(resources.Nodes.TryGet(id,out ResourceNodeStateV3? node)&&node!=null){Vector2I chunk=WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(node.Cell.Value);attached.Add(chunk);if(!rendered.Contains(chunk))outside++;}
		_worldManager.SetResourceRuntimeDiagnostics(ids,_groundStackViews.GetIds(),_resourceNodeViews.CreatedTotal,attached.Count,outside);
	}

	private void CenterCameraOnInitialDeploymentOrPlayerStart()
	{
		if (_camera == null || _worldManager == null)
		{
			return;
		}

		if (_worldManager.TryGetLocalDeployment(out GameplayV3.Deployment.CompanyDeploymentStateV3? deployment)
			&& deployment != null)
		{
			_camera.CenterOnGlobalCell(deployment.DeploymentAnchorCell.Value);
			return;
		}

		CenterCameraOnPlayerStart();
	}

	private void CreateLoadingOverlay()
	{
		CanvasLayer? canvasLayer = GetNodeOrNull<CanvasLayer>("CanvasLayer");
		if (canvasLayer == null)
		{
			return;
		}

		_loadingOverlay = new WorldV2LoadingOverlay
		{
			Name = "WorldV2LoadingOverlay"
		};
		canvasLayer.AddChild(_loadingOverlay);
	}

	private void CreateWorldMapOverlay()
	{
		CanvasLayer? canvasLayer = GetNodeOrNull<CanvasLayer>("CanvasLayer");
		if (canvasLayer == null)
		{
			return;
		}

		_worldMapOverlay = new WorldMapOverlayV2
		{
			Name = "WorldMapOverlayV2"
		};
		canvasLayer.AddChild(_worldMapOverlay);
	}

	private void ToggleWorldMapOverlay()
	{
		if (_worldManager == null || _worldMapOverlay == null)
		{
			return;
		}

		_worldMapOverlay.Toggle(_worldManager);
		if(_worldMapOverlay.IsOpen){CancelBedAssignmentMode();_constructionPlacement?.ClearActivePlacementTool();_farmDesignation?.SetActive(false);_floorRuntime?.SetTool(FloorToolModeV3.None);_roomOverlay?.HideOverlay();}
		_baseAreaOverlay?.SetSuppressed(_worldMapOverlay.IsOpen);
		_constructionUi?.SetWorldMapBlocked(_worldMapOverlay.IsOpen);
		_mercenaryInspectHud?.SetWorldMapBlocked(_worldMapOverlay.IsOpen);
		_workPriorityPanel?.SetWorldMapBlocked(_worldMapOverlay.IsOpen);
		_baseManagementPanel?.SetWorldMapBlocked(_worldMapOverlay.IsOpen);
		_mercenarySchedulePanel?.SetWorldMapBlocked(_worldMapOverlay.IsOpen);
		_frontierSurvivalPanel?.SetWorldMapBlocked(_worldMapOverlay.IsOpen);
		_productionPanel?.SetWorldMapBlocked(_worldMapOverlay.IsOpen);
	}

	private bool IsWorldMapOverlayOpen()
	{
		return _worldMapOverlay?.IsOpen == true;
	}

	private bool IsWorldInputLocked()
	{
		return _streamManager != null && !_streamManager.IsInitialLoadingComplete;
	}

	private void SetCameraInputLocked(bool locked)
	{
		if (_camera == null || _cameraInputWasLocked == locked)
		{
			return;
		}

		_camera.ProcessMode = locked ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
		_cameraInputWasLocked = locked;
	}

	private static bool IsDebugKeyAllowedWhileLoading(Key key)
	{
		return key is Key.F1 or Key.F7 or Key.F8 or Key.F10;
	}
}
