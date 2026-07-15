using System;
using Godot;
using GameplayV3.Control;
using GameplayV3.Control.Runtime;
using GameplayV3.Mercenary;
using GameplayV3.Mercenary.Runtime;
using GameplayV3.Mercenary.UI;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using GameplayV3.Resources.Runtime;
using GameplayV3.Work;
using GameplayV3.Work.Runtime;
using GameplayV3.Stockpile;
using GameplayV3.Stockpile.Runtime;
using GameplayV3.Construction;
using GameplayV3.Construction.Runtime;
using GameplayV3.Needs;
using GameplayV3.Needs.Runtime;
using GameplayV3.Session;

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
    private MercenaryDragSelectionOverlayV3? _mercenaryDragOverlay;
    private MercenaryCommandMarkerV3? _mercenaryCommandMarker;
    private MercenaryWorkRuntimeV3? _mercenaryWorkRuntime;
    private readonly ResourceNodeViewRegistryV3 _resourceNodeViews=new();
    private readonly GroundResourceStackViewRegistryV3 _groundStackViews=new();
    private StockpileOverlayV3? _stockpileOverlay;
    private StockpileDesignationControllerV3? _stockpileDesignation;
    private ConstructionUiV3? _constructionUi;
    private ConstructionWorldOverlayV3? _constructionOverlay;
    private ConstructionPlacementControllerV3? _constructionPlacement;
    private ConstructionWorkCoordinatorV3? _constructionWork;
    private DemolitionDesignationControllerV3? _demolitionDesignation;
    private DemolitionWorkCoordinatorV3? _demolitionWork;
    private MercenaryInspectHudV3? _mercenaryInspectHud;
    private MercenaryNeedsRuntimeV3? _mercenaryNeedsRuntime;
    private RestWorkCoordinatorV3? _restWork;
    private RestAssignmentOverlayV3? _restAssignmentOverlay;
    private string _pendingBedAssignmentMercenaryId=string.Empty;
    private bool _cameraInputWasLocked;

    public override void _Ready()
    {
        _worldManager = GetNodeOrNull<WorldManagerV2>("WorldManagerV2");
        _gridRenderer = GetNodeOrNull<WorldV2GridRenderer>("GridLayer");
        _buildManager = GetNodeOrNull<WorldV2BuildManager>("BuildingLayer");
        _camera = GetNodeOrNull<WorldV2CameraController>("Camera2D");
        _streamManager = GetNodeOrNull<WorldStreamManagerV2>("WorldStreamManagerV2");
        _mercenariesContainer = GetNodeOrNull<Node2D>("GameplayEntitiesV3/MercenariesV3");
        _resourceNodesContainer = GetNodeOrNull<Node2D>("GameplayEntitiesV3/ResourceNodesV3");
        _groundResourcesContainer = GetNodeOrNull<Node2D>("GameplayEntitiesV3/GroundResourcesV3");
        _stockpileContainer = GetNodeOrNull<Node2D>("GameplayEntitiesV3/StockpileZonesV3");
        if(_mercenariesContainer!=null)_mercenariesContainer.ZIndex=2;
        if(_resourceNodesContainer!=null)_resourceNodesContainer.ZIndex=2;
        if(_groundResourcesContainer!=null)_groundResourcesContainer.ZIndex=2;
        CreateLoadingOverlay();
        CreateWorldMapOverlay();
        MaterializeLocalMercenaries();
        MaterializeResources();
        InitializeMercenaryControlRuntime();

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
    }

    public override void _ExitTree()
    {
        _mercenaryViewRegistry.Clear();
        _resourceNodeViews.Clear();
        _groundStackViews.Clear();
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
                _constructionUi?.HandleEscape();
            }

            if (IsWorldInputLocked() && !IsDebugKeyAllowedWhileLoading(keyEvent.Keycode))
            {
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
            && _mercenaryInputController?.TryHandleUnhandledInput(@event) == true)
        {
            GetViewport().SetInputAsHandled();
            return;
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
                _worldManager.UpdateDebugHud("Printed performance summary.");
                break;
            case Key.F11:
                _worldManager.RegenerateVisibleChunks(clearRuntimeStructures: false);
                _stockpileOverlay?.Refresh();
                _constructionOverlay?.Refresh();
                _constructionPlacement?.RefreshPreview();
                break;
            case Key.F12:
                _worldManager.RegenerateVisibleChunks(clearRuntimeStructures: true);
                _stockpileOverlay?.Refresh();
                _constructionOverlay?.Refresh();
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
            navigationQuery=new DynamicStructureNavigationQueryV3(navigationQuery,navigationConstruction.Structures);

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
            _constructionOverlay=new ConstructionWorldOverlayV3{Name="ConstructionWorldOverlayV3"};gameplayEntities.AddChild(_constructionOverlay);_constructionOverlay.Initialize(construction,_gridRenderer);
            _constructionPlacement=new ConstructionPlacementControllerV3{Name="ConstructionPlacementControllerV3"};gameplayEntities.AddChild(_constructionPlacement);_constructionPlacement.Initialize(construction,resources,stockpiles,mercenaries,navigationQuery,_gridRenderer,_worldManager,_constructionOverlay);
            _constructionWork=new ConstructionWorkCoordinatorV3(construction,resources,stockpiles,work,controlSession,mercenaries,navigationQuery,_worldManager);_constructionWork.Changed+=()=>{_constructionOverlay?.Refresh();_restAssignmentOverlay?.Refresh();};_constructionWork.ResourcesChanged+=MaterializeResources;
            if(needsSession!=null)_constructionWork.AttachWorkMultiplier(needsSession.WorkMultiplier);
            _demolitionWork=new DemolitionWorkCoordinatorV3(construction,resources,work,controlSession,mercenaries,navigationQuery,_worldManager.LocalPlayerId,_worldManager.LocalCompanyId,_worldManager.WorldBounds,id=>{_constructionWork.CancelForDirectMove(id);},true);_demolitionWork.Changed+=()=>{_constructionOverlay?.Refresh();_restAssignmentOverlay?.Refresh();};_demolitionWork.ResourcesChanged+=MaterializeResources;
            if(needsSession!=null)_demolitionWork.AttachWorkMultiplier(needsSession.WorkMultiplier);
            if(needsSession!=null){_restWork=new RestWorkCoordinatorV3(needsSession,mercenaries,controlSession,work,construction,navigationQuery);_mercenaryNeedsRuntime?.AttachRestCoordinator(_restWork);_demolitionWork.AttachRestLifecycle(_restWork.OnStructureDemolitionStarted,_restWork.OnStructureDemolitionEnded);_restAssignmentOverlay=new RestAssignmentOverlayV3{Name="RestAssignmentOverlayV3"};gameplayEntities.AddChild(_restAssignmentOverlay);_restAssignmentOverlay.Initialize(construction,needsSession,mercenaries,_gridRenderer,_worldManager.LocalCompanyId);construction.Structures.StructureRemoved+=removed=>{if(construction.Definitions.TryGetDefinition(removed.DefinitionId,out var removedDefinition)&&removedDefinition!=null)needsSession.RemoveStructure(removed.StructureId,removedDefinition,removed);_restAssignmentOverlay?.Refresh();};controlSession.Selection.SelectionChanged+=CancelBedAssignmentMode;}
            _demolitionDesignation=new DemolitionDesignationControllerV3{Name="DemolitionDesignationControllerV3"};gameplayEntities.AddChild(_demolitionDesignation);_demolitionDesignation.Initialize(construction,_gridRenderer,_worldManager,_constructionOverlay,_demolitionWork);
            controlSession.AttachConstructionCancellation(id=>{bool changed=_constructionWork.CancelForDirectMove(id)|_demolitionWork.CancelForDirectMove(id);if(_restWork?.Cancel(id,"CancelledByDirectMove")==true)changed=true;return changed;});
            work.AttachExternalWorkSupersede(id=>{_demolitionWork.CancelForNewWork(id);_restWork?.Cancel(id,"SupersededByNewWork");});
            _constructionUi.ConstructionToolChanged+=HandleConstructionToolChanged;
            _constructionUi.DemolitionToolChanged+=active=>{if(active)_constructionPlacement?.ClearActivePlacementTool();_demolitionDesignation?.SetActive(active);};
            _constructionPlacement.ActiveChanged+=active=>{if(active||_constructionUi==null)return;if(_constructionUi.ActiveConstructionTool=="WoodenWall")_constructionUi.SetWallTool(false);else if(_constructionUi.ActiveConstructionTool=="BasicBed")_constructionUi.SetBedTool(false);};
            _demolitionDesignation.ActiveChanged+=active=>{if(!active&&_constructionUi?.ActiveConstructionTool=="Demolition")_constructionUi.SetDemolitionTool(false);};
            _mercenaryWorkRuntime.AttachConstruction(_constructionWork);
            _mercenaryWorkRuntime.AttachDemolition(_demolitionWork);
            if(_restWork!=null)_mercenaryWorkRuntime.AttachRest(_restWork);
            _mercenaryInspectHud=new MercenaryInspectHudV3{Name="MercenaryInspectHudV3"};
            canvasLayer.AddChild(_mercenaryInspectHud);
            _mercenaryInspectHud.Initialize(controlSession,mercenaries,work,_worldManager,_constructionWork,_demolitionWork,needsSession,_restWork);
            _mercenaryInspectHud.BedAssignmentRequested+=id=>{_pendingBedAssignmentMercenaryId=id;_restAssignmentOverlay?.SetAssignmentMode(true,id);_constructionPlacement?.ClearActivePlacementTool();_demolitionDesignation?.SetActive(false);_stockpileDesignation?.SetMode(StockpileDesignationModeV3.None);_worldManager.UpdateDebugHud("배정할 간이 침대를 클릭하세요.");};
        }
    }

    private void HandleConstructionToolChanged(ConstructionPlacementToolKindV3 toolKind,bool active)
    {
        if(_constructionPlacement==null)return;
        if(toolKind==ConstructionPlacementToolKindV3.None){if(active)_constructionPlacement.ClearActivePlacementTool();return;}
        if(active)
        {
            string definitionId=toolKind switch{ConstructionPlacementToolKindV3.WoodenWall=>StructureDefinitionCatalogV3.WoodenWallId,ConstructionPlacementToolKindV3.BasicBed=>StructureDefinitionCatalogV3.BasicBedId,_=>string.Empty};
            _constructionPlacement.SetActivePlacementTool(toolKind,definitionId);
        }
        else if(_constructionPlacement.ActiveToolKind==toolKind)
        {
            _constructionPlacement.ClearActivePlacementTool();
        }
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
        int created=ResourceMaterializationCoordinatorV3.MaterializeNodes(resources,_resourceNodesContainer,_gridRenderer,_resourceNodeViews);
        foreach(string id in resources.GroundStacks.GetAllStackIds())if(resources.GroundStacks.TryGet(id,out GroundResourceStackV3? stack)&&stack!=null)ResourceMaterializationCoordinatorV3.MaterializeOrRefreshStack(stack,resources,_groundResourcesContainer,_gridRenderer,_groundStackViews);
        _worldManager.SetResourceRuntimeDiagnostics(_resourceNodeViews.GetIds(),_groundStackViews.GetIds());
        GD.Print($"[ResourceCoreV3] views nodes={_resourceNodeViews.Count} stacks={_groundStackViews.Count} created={created}");
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
        if(_worldMapOverlay.IsOpen){CancelBedAssignmentMode();_constructionPlacement?.ClearActivePlacementTool();}
        _constructionUi?.SetWorldMapBlocked(_worldMapOverlay.IsOpen);
        _mercenaryInspectHud?.SetWorldMapBlocked(_worldMapOverlay.IsOpen);
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
