using System.Collections.Generic;
using Godot;

public partial class MainWorldController : Node2D
{
    [Export]
    public float MoveSpeed { get; set; } = 600.0f;

    [Export]
    public float ZoomSpeed { get; set; } = 0.1f;

    [Export]
    public float MinZoom { get; set; } = 0.25f;

    [Export]
    public float MaxZoom { get; set; } = 4.0f;

    [Export]
    public bool ClampCameraToWorld { get; set; } = true;

    [Export]
    public float SelectionDragThreshold { get; set; } = 8.0f;

    [Export]
    public bool ShowPathDebug { get; set; } = false;

    [Export]
    public bool ShowFacilityDebug { get; set; } = false;

    [Export]
    public bool ShowHudDebugText { get; set; } = false;

    [Export]
    public bool DebugHarvestDesignation { get; set; } = false;

    [Export]
    public bool DebugFarmZone { get; set; } = false;

    private Camera2D? _camera;
    private WorldGridRenderer? _worldGrid;
    private BaseBuildManager? _baseBuildManager;
    private BaseAlertState? _baseAlertState;
    private Marker2D? _rallyPoint;
    private SelectedMercenaryHud? _selectedMercenaryHud;
    private BuildHud? _buildHud;
    private WorldBuildPanel? _worldBuildPanel;
    private BaseRoomManager? _baseRoomManager;
    private SelectionOverlay? _selectionOverlay;
    private PathDebugOverlay? _pathDebugOverlay;
    private FacilityDebugOverlay? _facilityDebugOverlay;
    private readonly List<MercenaryController> _selectedMercenaries = new();
    private bool _isLeftMouseDown;
    private bool _isDraggingSelection;
    private bool _dragKeepExistingSelection;
    private bool _isBuildDragging;
    private bool _dragChangedNavigation;
    private readonly HashSet<Vector2I> _dragBuiltCells = new();
    private TileBuildType _dragBuildType = TileBuildType.None;
    private bool _isHarvestDesignationMode;
    private bool _isHarvestDesignationDragging;
    private bool _harvestDesignationRemoveMode;
    private int _harvestDesignationChangedCount;
    private readonly HashSet<Vector2I> _harvestDesignationDragCells = new();
    private bool _isFarmZoneMode;
    private bool _isFarmZoneDragging;
    private bool _farmZoneRemoveMode;
    private int _farmZoneChangedCount;
    private readonly HashSet<Vector2I> _farmZoneDragCells = new();
    private bool _isRoomDesignationMode;
    private bool _isRoomDesignationDragging;
    private bool _roomDesignationRemoveMode;
    private bool _roomDesignationDragRemoveMode;
    private RoomType _roomDesignationType = RoomType.None;
    private readonly HashSet<Vector2I> _roomDesignationCells = new();
    private Vector2I? _lastRoomDesignationCell;
    private bool _isStockpileZoneMode;
    private bool _isStockpileZoneDragging;
    private bool _stockpileZoneRemoveMode;
    private bool _stockpileZoneDragRemoveMode;
    private readonly HashSet<Vector2I> _stockpileZoneCells = new();
    private Vector2I? _lastStockpileZoneCell;
    private Vector2 _dragStartWorld;
    private Vector2 _dragCurrentWorld;
    private Vector2 _dragStartScreen;

    public override void _Ready()
    {
        _camera = GetNodeOrNull<Camera2D>("Camera2D");
        _worldGrid = GetNodeOrNull<WorldGridRenderer>("TerrainLayer");
        _baseBuildManager = GetNodeOrNull<BaseBuildManager>("BuildingLayer");
        _baseAlertState = GetNodeOrNull<BaseAlertState>("BaseAlertState");
        _rallyPoint = GetNodeOrNull<Marker2D>("RallyPointLayer/RallyPoint");
        _selectionOverlay = GetNodeOrNull<SelectionOverlay>("CanvasLayer/SelectionOverlay");
        _selectedMercenaryHud = GetNodeOrNull<SelectedMercenaryHud>("CanvasLayer/HUD");
        _buildHud = GetNodeOrNull<BuildHud>("CanvasLayer/HUD/BuildHud");
        _worldBuildPanel = GetNodeOrNull<WorldBuildPanel>("CanvasLayer/HUD/WorldBuildPanel");
        _pathDebugOverlay = GetNodeOrNull<PathDebugOverlay>("EffectLayer/PathDebugOverlay");
        _facilityDebugOverlay = GetNodeOrNull<FacilityDebugOverlay>("EffectLayer/FacilityDebugOverlay");
        _baseRoomManager = GetNodeOrNull<BaseRoomManager>("EffectLayer/BaseRoomManager")
            ?? GetNodeOrNull<BaseRoomManager>("BaseRoomManager");

        if (_baseBuildManager != null)
        {
            _facilityDebugOverlay?.SetBuildManager(_baseBuildManager);
        }

        if (_baseRoomManager == null)
        {
            _baseRoomManager = new BaseRoomManager
            {
                Name = "BaseRoomManager"
            };

            Node? effectLayer = GetNodeOrNull<Node>("EffectLayer");
            if (effectLayer != null)
            {
                effectLayer.AddChild(_baseRoomManager);
            }
            else
            {
                AddChild(_baseRoomManager);
            }
        }

        _baseRoomManager.SetBuildManager(_baseBuildManager);

        _selectedMercenaryHud?.SetDebugStatusVisible(ShowHudDebugText);

        if (_worldBuildPanel != null)
        {
            _worldBuildPanel.SetBuildManager(_baseBuildManager);
            _worldBuildPanel.BuildModeSelected += HandleBuildPanelModeSelected;
            _worldBuildPanel.BuildMaterialSelected += HandleBuildPanelMaterialSelected;
            _worldBuildPanel.RoomDesignationSelected += HandleRoomDesignationSelected;
            _worldBuildPanel.MercenarySelected += HandleMercenaryPanelSelected;
            _worldBuildPanel.MercenaryWorkSettingsChanged += HandleMercenaryWorkSettingsChanged;
            _worldBuildPanel.StockpileZoneDesignationSelected += HandleStockpileZoneDesignationSelected;
        }

        UpdateBaseAlertState();
        UpdateSelectedMercenaryHud();
        UpdateBuildHud();
        UpdatePathDebugOverlayVisibility();
        UpdateFacilityDebugOverlayVisibility();

        if (_camera == null)
        {
            GD.PushError("MainWorldController requires a Camera2D child named Camera2D.");
            return;
        }

        _camera.MakeCurrent();
        ClampCameraZoom();
        ClampCameraPosition();
    }

    public override void _Process(double delta)
    {
        if (_camera == null)
        {
            return;
        }

        Vector2 direction = GetCameraMoveDirection();

        if (direction != Vector2.Zero)
        {
            _camera.Position += direction.Normalized() * MoveSpeed * (float)delta;
            ClampCameraPosition();
        }

        if (_isLeftMouseDown)
        {
            _dragCurrentWorld = GetGlobalMousePosition();
            Vector2 currentScreen = GetViewport().GetMousePosition();

            if (!_isDraggingSelection && _dragStartScreen.DistanceTo(currentScreen) >= SelectionDragThreshold)
            {
                _isDraggingSelection = true;
                _selectionOverlay?.ShowSelectionBox(_dragStartScreen, currentScreen);
            }

            if (_isDraggingSelection)
            {
                _selectionOverlay?.UpdateSelectionBox(currentScreen);
            }
        }

        if (_isBuildDragging && _baseBuildManager != null && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            TryApplyBuildDragAtWorldPosition(GetGlobalMousePosition());
        }

        if (_isHarvestDesignationDragging && _baseBuildManager != null && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            TryApplyHarvestDesignationDragAtWorldPosition(GetGlobalMousePosition());
        }

        if (_isFarmZoneDragging && _baseBuildManager != null && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            TryApplyFarmZoneDragAtWorldPosition(GetGlobalMousePosition());
        }

        if (_isRoomDesignationDragging && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            TryAddRoomDesignationCellAtWorldPosition(GetGlobalMousePosition());
        }

        if (_isStockpileZoneDragging && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            TryAddStockpileZoneCellAtWorldPosition(GetGlobalMousePosition());
        }

        UpdateBaseAlertState();
        UpdateSelectedMercenaryHud();
        UpdateBuildHud();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_camera == null)
        {
            return;
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (HandleDebugInput(keyEvent))
            {
                return;
            }

            if (HandleRoomDesignationModeInput(keyEvent))
            {
                return;
            }

            if (HandleHarvestDesignationModeInput(keyEvent))
            {
                return;
            }

            if (HandleFarmZoneModeInput(keyEvent))
            {
                return;
            }

            if (HandleBuildModeInput(keyEvent))
            {
                return;
            }

            HandleControlModeInput(keyEvent);
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            return;
        }

        if (@event is not InputEventMouseButton mouseButton)
        {
            return;
        }

        if (mouseButton.Pressed && IsPointerOverUi())
        {
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
        {
            ApplyZoom(1.0f + ZoomSpeed);
        }
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
        {
            ApplyZoom(1.0f / (1.0f + ZoomSpeed));
        }
        else if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (_isRoomDesignationMode)
            {
                if (mouseButton.Pressed)
                {
                    HandleRoomDesignationLeftMousePressed(mouseButton);
                }
                else
                {
                    EndRoomDesignationDrag(true);
                }

                return;
            }

            if (_isStockpileZoneMode)
            {
                if (mouseButton.Pressed)
                {
                    HandleStockpileZoneLeftMousePressed(mouseButton);
                }
                else
                {
                    EndStockpileZoneDrag(true);
                }

                return;
            }

            if (_isFarmZoneMode)
            {
                if (mouseButton.Pressed)
                {
                    HandleFarmZoneLeftMousePressed(mouseButton);
                }
                else
                {
                    EndFarmZoneDrag(true);
                }

                return;
            }

            if (_isHarvestDesignationMode)
            {
                if (mouseButton.Pressed)
                {
                    HandleHarvestDesignationLeftMousePressed(mouseButton);
                }
                else
                {
                    EndHarvestDesignationDrag(true);
                }

                return;
            }

            if (IsBuildModeActive())
            {
                if (mouseButton.Pressed)
                {
                    HandleBuildLeftMousePressed(GetGlobalMousePosition());
                }
                else
                {
                    EndBuildDrag();
                }

                if (!mouseButton.Pressed)
                {
                    UpdateBuildHud();
                }

                return;
            }

            HandleLeftMouseButton(mouseButton);
        }
        else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
        {
            if (_isRoomDesignationMode)
            {
                SetRoomDesignationMode(RoomType.None, false);
                ClearBuildUiSelection();
                CancelSelectionDrag();
                UpdateBuildHud();
                return;
            }

            if (_isStockpileZoneMode)
            {
                SetStockpileZoneMode(false, false);
                ClearBuildUiSelection();
                CancelSelectionDrag();
                UpdateBuildHud();
                return;
            }

            if (_isFarmZoneMode)
            {
                SetFarmZoneMode(false);
                CancelSelectionDrag();
                UpdateBuildHud();
                return;
            }

            if (_isHarvestDesignationMode)
            {
                SetHarvestDesignationMode(false);
                CancelSelectionDrag();
                UpdateBuildHud();
                return;
            }

            if (IsBuildModeActive())
            {
                EndBuildDrag();
                _baseBuildManager?.SetBuildMode(TileBuildType.None);
                _buildHud?.ClearFeedback();
                _worldBuildPanel?.SetSelectedBuildType(TileBuildType.None);
                CancelSelectionDrag();
                UpdateBuildHud();
                return;
            }

            MoveSelectedSquadMercenaries(GetGlobalMousePosition());
        }
    }

    private static Vector2 GetCameraMoveDirection()
    {
        Vector2 direction = Vector2.Zero;

        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
        {
            direction.X -= 1.0f;
        }

        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
        {
            direction.X += 1.0f;
        }

        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
        {
            direction.Y -= 1.0f;
        }

        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
        {
            direction.Y += 1.0f;
        }

        return direction;
    }

    private void ApplyZoom(float factor)
    {
        float nextZoom = Mathf.Clamp(_camera!.Zoom.X * factor, MinZoom, MaxZoom);
        _camera.Zoom = Vector2.One * nextZoom;
        ClampCameraPosition();
    }

    private void ClampCameraZoom()
    {
        float clampedZoom = Mathf.Clamp(_camera!.Zoom.X, MinZoom, MaxZoom);
        _camera.Zoom = Vector2.One * clampedZoom;
    }

    private void ClampCameraPosition()
    {
        if (!ClampCameraToWorld || _camera == null || _worldGrid == null)
        {
            return;
        }

        Rect2 worldRect = _worldGrid.GetWorldRect();
        Vector2 viewportSize = GetViewportRect().Size / _camera.Zoom;
        Vector2 halfViewport = viewportSize * 0.5f;
        Vector2 minPosition = worldRect.Position + halfViewport;
        Vector2 maxPosition = worldRect.End - halfViewport;
        Vector2 worldCenter = worldRect.GetCenter();

        float clampedX = minPosition.X > maxPosition.X
            ? worldCenter.X
            : Mathf.Clamp(_camera.Position.X, minPosition.X, maxPosition.X);

        float clampedY = minPosition.Y > maxPosition.Y
            ? worldCenter.Y
            : Mathf.Clamp(_camera.Position.Y, minPosition.Y, maxPosition.Y);

        _camera.Position = new Vector2(clampedX, clampedY);
    }

    private void SelectOnly(MercenaryController mercenary)
    {
        ClearSelection();
        _worldBuildPanel?.ClearInfoPanel();
        AddToSelection(mercenary);
    }

    private void ToggleSelection(MercenaryController mercenary)
    {
        if (_selectedMercenaries.Contains(mercenary))
        {
            _selectedMercenaries.Remove(mercenary);
            mercenary.SetSelected(false);
        }
        else
        {
            AddToSelection(mercenary);
        }

        UpdateSelectedMercenaryHud();
    }

    private MercenaryController? FindMercenaryAt(Vector2 worldPosition)
    {
        PhysicsPointQueryParameters2D query = new PhysicsPointQueryParameters2D
        {
            Position = worldPosition,
            CollideWithAreas = true,
            CollideWithBodies = false,
            CollisionMask = uint.MaxValue
        };

        var results = GetWorld2D().DirectSpaceState.IntersectPoint(query, 16);

        foreach (var result in results)
        {
            GodotObject? collider = result["collider"].AsGodotObject();

            if (collider is Area2D area && area.GetParent() is MercenaryController mercenary)
            {
                return mercenary;
            }
        }

        return null;
    }

    private bool IsPointerOverUi()
    {
        return GetViewport().GuiGetHoveredControl() != null;
    }

    private bool HandleDebugInput(InputEventKey keyEvent)
    {
        if (keyEvent.Keycode == Key.F3)
        {
            ShowPathDebug = !ShowPathDebug;
            UpdatePathDebugOverlayVisibility();
            GD.Print($"Path debug overlay: {(ShowPathDebug ? "On" : "Off")}");
            return true;
        }

        if (keyEvent.Keycode == Key.F4)
        {
            ShowFacilityDebug = !ShowFacilityDebug;
            UpdateFacilityDebugOverlayVisibility();
            GD.Print($"Facility debug overlay: {(ShowFacilityDebug ? "On" : "Off")}");
            return true;
        }

        if (keyEvent.Keycode == Key.F6)
        {
            AdjustFoodDebug(5, "Food debug: +5");
            return true;
        }

        if (keyEvent.Keycode == Key.F7)
        {
            AdjustFoodDebug(-5, "Food debug: -5");
            return true;
        }

        if (keyEvent.Keycode == Key.F8)
        {
            SetFoodDebug(0, "Food debug: set 0");
            return true;
        }

        if (keyEvent.Keycode == Key.F9)
        {
            AdjustResourceDebug(BaseResourceType.Wood, 10, "Resource debug: Wood +10");
            return true;
        }

        if (keyEvent.Keycode == Key.F10)
        {
            AdjustResourceDebug(BaseResourceType.Stone, 10, "Resource debug: Stone +10");
            return true;
        }

        if (keyEvent.Keycode == Key.F11)
        {
            AdjustResourceDebug(BaseResourceType.Metal, 5, "Resource debug: Metal +5");
            return true;
        }

        if (keyEvent.Keycode == Key.F12)
        {
            // TODO: If F12 is intercepted by the desktop environment, add a small resource debug panel or alternate key chord.
            SetConstructionResourcesDebug(0);
            return true;
        }

        return false;
    }

    private void AdjustFoodDebug(int amount, string messagePrefix)
    {
        if (_baseBuildManager == null)
        {
            return;
        }

        _baseBuildManager.SetFoodCount(_baseBuildManager.GetFoodCount() + amount);
        UpdateSelectedMercenaryHud();
        GD.Print($"{messagePrefix} => {_baseBuildManager.GetFoodCount()}");
    }

    private void SetFoodDebug(int value, string message)
    {
        if (_baseBuildManager == null)
        {
            return;
        }

        _baseBuildManager.SetFoodCount(value);
        UpdateSelectedMercenaryHud();
        GD.Print($"{message} => {_baseBuildManager.GetFoodCount()}");
    }

    private void AdjustResourceDebug(BaseResourceType resourceType, int amount, string messagePrefix)
    {
        if (_baseBuildManager == null)
        {
            return;
        }

        _baseBuildManager.AddResource(resourceType, amount);
        UpdateSelectedMercenaryHud();
        UpdateBuildHud();
        GD.Print($"{messagePrefix} => {_baseBuildManager.GetResourceAmount(resourceType)}");
    }

    private void SetConstructionResourcesDebug(int value)
    {
        if (_baseBuildManager == null)
        {
            return;
        }

        _baseBuildManager.SetResourceAmount(BaseResourceType.Wood, value);
        _baseBuildManager.SetResourceAmount(BaseResourceType.Stone, value);
        _baseBuildManager.SetResourceAmount(BaseResourceType.Metal, value);
        UpdateSelectedMercenaryHud();
        UpdateBuildHud();
        GD.Print("Resource debug: construction resources set 0");
    }

    private bool HandleBuildModeInput(InputEventKey keyEvent)
    {
        if (_baseBuildManager == null)
        {
            return false;
        }

        if (keyEvent.Keycode == Key.Escape)
        {
            SetRoomDesignationMode(RoomType.None, false);
            SetStockpileZoneMode(false, false);
            SetHarvestDesignationMode(false);
            SetFarmZoneMode(false);
            ClearBuildUiSelection();
            CancelSelectionDrag();
            UpdateBuildHud();
            return true;
        }

        if (keyEvent.Keycode == Key.G)
        {
            SetRoomDesignationMode(RoomType.None, false);
            SetStockpileZoneMode(false, false);
            SetHarvestDesignationMode(false);
            SetFarmZoneMode(false);
            _baseBuildManager.ToggleBuildMode();
            _worldBuildPanel?.SetSelectedBuildType(_baseBuildManager.CurrentBuildMode);
            if (!IsBuildModeActive())
            {
                EndBuildDrag();
                _buildHud?.ClearFeedback();
            }

            CancelSelectionDrag();
            UpdateBuildHud();
            return true;
        }

        TileBuildType buildType = keyEvent.Keycode switch
        {
            Key.Key1 => TileBuildType.Floor,
            Key.Key2 => TileBuildType.Wall,
            Key.Key3 => TileBuildType.Door,
            Key.Key4 => TileBuildType.Bed,
            Key.Key5 => TileBuildType.Storage,
            Key.Key6 => TileBuildType.GuardPost,
            Key.Key0 => TileBuildType.Erase,
            Key.Delete => TileBuildType.Erase,
            _ => TileBuildType.None
        };

        bool handled = keyEvent.Keycode == Key.Key1
            || keyEvent.Keycode == Key.Key2
            || keyEvent.Keycode == Key.Key3
            || keyEvent.Keycode == Key.Key4
            || keyEvent.Keycode == Key.Key5
            || keyEvent.Keycode == Key.Key6
            || keyEvent.Keycode == Key.Key0
            || keyEvent.Keycode == Key.Delete;

        if (!handled)
        {
            return IsBuildModeActive();
        }

        SetRoomDesignationMode(RoomType.None, false);
        SetStockpileZoneMode(false, false);
        SetHarvestDesignationMode(false);
        SetFarmZoneMode(false);
        _baseBuildManager.SetBuildMode(buildType);
        if (buildType == TileBuildType.None)
        {
            EndBuildDrag();
            _buildHud?.ClearFeedback();
        }

        _worldBuildPanel?.SetSelectedBuildType(buildType);
        CancelSelectionDrag();
        UpdateBuildHud();
        return true;
    }

    private void HandleBuildPanelModeSelected(TileBuildType buildType)
    {
        if (_baseBuildManager == null)
        {
            return;
        }

        SetRoomDesignationMode(RoomType.None, false);
        SetStockpileZoneMode(false, false);
        SetHarvestDesignationMode(false);
        SetFarmZoneMode(false);
        EndBuildDrag();
        _baseBuildManager.SetBuildMode(buildType);
        _buildHud?.ClearFeedback();
        _worldBuildPanel?.SetSelectedBuildType(buildType);
        CancelSelectionDrag();
        UpdateBuildHud();
    }

    private void HandleBuildPanelMaterialSelected(BuildMaterialType materialType)
    {
        _baseBuildManager?.SetBuildMaterialType(materialType);
        _worldBuildPanel?.SetSelectedBuildMaterialType(_baseBuildManager?.CurrentBuildMaterialType ?? materialType);
        UpdateBuildHud();
    }

    private void HandleRoomDesignationSelected(RoomType roomType, bool removeMode)
    {
        SetRoomDesignationMode(roomType, removeMode);
        _worldBuildPanel?.SetSelectedRoomDesignation(roomType, removeMode);
        CancelSelectionDrag();
        UpdateBuildHud();
    }

    private void HandleStockpileZoneDesignationSelected(bool removeMode)
    {
        SetStockpileZoneMode(true, removeMode);
        CancelSelectionDrag();
        UpdateBuildHud();
    }

    private void HandleMercenaryPanelSelected(MercenaryController mercenary)
    {
        if (!GodotObject.IsInstanceValid(mercenary) || mercenary.IsQueuedForDeletion())
        {
            return;
        }

        SetRoomDesignationMode(RoomType.None, false);
        SetHarvestDesignationMode(false);
        SetFarmZoneMode(false);
        SetStockpileZoneMode(false, false);
        EndBuildDrag();
        _baseBuildManager?.SetBuildMode(TileBuildType.None);
        _buildHud?.ClearFeedback();
        SelectMercenaryFromManagement(mercenary, true);
        CancelSelectionDrag();
        UpdateBuildHud();
    }

    private void HandleMercenaryWorkSettingsChanged(MercenaryController mercenary)
    {
        if (!GodotObject.IsInstanceValid(mercenary) || mercenary.IsQueuedForDeletion())
        {
            return;
        }

        UpdateSelectedMercenaryHud();
    }

    public void SelectMercenaryFromManagement(MercenaryController mercenary, bool focusCamera)
    {
        SelectOnly(mercenary);

        if (focusCamera && _camera != null)
        {
            _camera.GlobalPosition = mercenary.GlobalPosition;
            ClampCameraPosition();
        }
    }

    private bool HandleRoomDesignationModeInput(InputEventKey keyEvent)
    {
        if (keyEvent.Keycode == Key.Escape && _isRoomDesignationMode)
        {
            SetRoomDesignationMode(RoomType.None, false);
            ClearBuildUiSelection();
            CancelSelectionDrag();
            UpdateBuildHud();
            return true;
        }

        return false;
    }

    private void SetRoomDesignationMode(RoomType roomType, bool removeMode)
    {
        bool enabled = removeMode || roomType != RoomType.None;
        _isRoomDesignationMode = enabled;
        _roomDesignationType = enabled ? roomType : RoomType.None;
        _roomDesignationRemoveMode = enabled && removeMode;

        if (!enabled)
        {
            EndRoomDesignationDrag(false);
            _roomDesignationCells.Clear();
            _lastRoomDesignationCell = null;
            _baseRoomManager?.ClearPreview();
            return;
        }

        SetHarvestDesignationMode(false);
        SetFarmZoneMode(false);
        EndBuildDrag();
        _baseBuildManager?.SetBuildMode(TileBuildType.None);
        _buildHud?.ClearFeedback();
    }

    private void HandleRoomDesignationLeftMousePressed(InputEventMouseButton mouseButton)
    {
        if (_baseBuildManager == null)
        {
            return;
        }

        _isRoomDesignationDragging = true;
        _roomDesignationDragRemoveMode = _roomDesignationRemoveMode || mouseButton.ShiftPressed;
        _roomDesignationCells.Clear();
        _lastRoomDesignationCell = null;
        TryAddRoomDesignationCellAtWorldPosition(GetGlobalMousePosition());
    }

    private void TryAddRoomDesignationCellAtWorldPosition(Vector2 worldPosition)
    {
        if (_baseBuildManager == null)
        {
            return;
        }

        Vector2I cell = _baseBuildManager.WorldToCell(worldPosition);

        if (!_baseBuildManager.IsCellInWorld(cell))
        {
            return;
        }

        if (_lastRoomDesignationCell.HasValue)
        {
            AddRoomDesignationLine(_lastRoomDesignationCell.Value, cell);
        }
        else
        {
            _roomDesignationCells.Add(cell);
        }

        _lastRoomDesignationCell = cell;
        _baseRoomManager?.SetPreviewCells(_roomDesignationCells, _roomDesignationType, _roomDesignationDragRemoveMode);
    }

    private void AddRoomDesignationLine(Vector2I fromCell, Vector2I toCell)
    {
        if (_baseBuildManager == null)
        {
            return;
        }

        int steps = Mathf.Max(Mathf.Abs(toCell.X - fromCell.X), Mathf.Abs(toCell.Y - fromCell.Y));

        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0.0f : i / (float)steps;
            Vector2I cell = new(
                Mathf.RoundToInt(Mathf.Lerp(fromCell.X, toCell.X, t)),
                Mathf.RoundToInt(Mathf.Lerp(fromCell.Y, toCell.Y, t)));

            if (_baseBuildManager.IsCellInWorld(cell))
            {
                _roomDesignationCells.Add(cell);
            }
        }
    }

    private void EndRoomDesignationDrag(bool apply)
    {
        if (!_isRoomDesignationDragging)
        {
            return;
        }

        _isRoomDesignationDragging = false;
        bool removeMode = _roomDesignationDragRemoveMode;
        _roomDesignationDragRemoveMode = false;
        _lastRoomDesignationCell = null;
        List<Vector2I> selectedCells = new(_roomDesignationCells);
        _roomDesignationCells.Clear();
        _baseRoomManager?.ClearPreview();

        if (!apply || _baseBuildManager == null || _baseRoomManager == null)
        {
            return;
        }

        if (removeMode)
        {
            int removedCount = _baseRoomManager.RemoveRoomsOverlapping(selectedCells);
            _buildHud?.ShowFeedback(removedCount > 0 ? $"Removed {removedCount} room(s)" : "No room here");
            return;
        }

        if (_roomDesignationType == RoomType.None || selectedCells.Count <= 0)
        {
            return;
        }

        if (_baseRoomManager.TryCreateRoom(_roomDesignationType, selectedCells, out BaseRoom? room) && room != null)
        {
            _buildHud?.ShowFeedback($"Marked room: {room.DisplayName}");
        }
        else
        {
            _buildHud?.ShowFeedback("Cannot mark room");
        }
    }

    private void SetStockpileZoneMode(bool enabled, bool removeMode)
    {
        _isStockpileZoneMode = enabled;
        _stockpileZoneRemoveMode = enabled && removeMode;

        if (!enabled)
        {
            EndStockpileZoneDrag(false);
            _stockpileZoneCells.Clear();
            _lastStockpileZoneCell = null;
            return;
        }

        SetRoomDesignationMode(RoomType.None, false);
        SetHarvestDesignationMode(false);
        SetFarmZoneMode(false);
        EndBuildDrag();
        _baseBuildManager?.SetBuildMode(TileBuildType.None);
        _buildHud?.ClearFeedback();
    }

    private void HandleStockpileZoneLeftMousePressed(InputEventMouseButton mouseButton)
    {
        _isStockpileZoneDragging = true;
        _stockpileZoneDragRemoveMode = _stockpileZoneRemoveMode || mouseButton.ShiftPressed;
        _stockpileZoneCells.Clear();
        _lastStockpileZoneCell = null;
        TryAddStockpileZoneCellAtWorldPosition(GetGlobalMousePosition());
    }

    private void TryAddStockpileZoneCellAtWorldPosition(Vector2 worldPosition)
    {
        if (_baseBuildManager == null)
        {
            return;
        }

        Vector2I cell = _baseBuildManager.WorldToCell(worldPosition);

        if (!_baseBuildManager.IsCellInWorld(cell))
        {
            return;
        }

        if (_lastStockpileZoneCell.HasValue)
        {
            AddStockpileZoneLine(_lastStockpileZoneCell.Value, cell);
        }
        else
        {
            _stockpileZoneCells.Add(cell);
        }

        _lastStockpileZoneCell = cell;
    }

    private void AddStockpileZoneLine(Vector2I fromCell, Vector2I toCell)
    {
        if (_baseBuildManager == null)
        {
            return;
        }

        int steps = Mathf.Max(Mathf.Abs(toCell.X - fromCell.X), Mathf.Abs(toCell.Y - fromCell.Y));

        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0.0f : i / (float)steps;
            Vector2I cell = new(
                Mathf.RoundToInt(Mathf.Lerp(fromCell.X, toCell.X, t)),
                Mathf.RoundToInt(Mathf.Lerp(fromCell.Y, toCell.Y, t)));

            if (_baseBuildManager.IsCellInWorld(cell))
            {
                _stockpileZoneCells.Add(cell);
            }
        }
    }

    private void EndStockpileZoneDrag(bool apply)
    {
        if (!_isStockpileZoneDragging)
        {
            return;
        }

        _isStockpileZoneDragging = false;
        bool removeMode = _stockpileZoneDragRemoveMode;
        _stockpileZoneDragRemoveMode = false;
        _lastStockpileZoneCell = null;
        List<Vector2I> selectedCells = new(_stockpileZoneCells);
        _stockpileZoneCells.Clear();

        if (!apply || _baseBuildManager == null)
        {
            return;
        }

        if (removeMode)
        {
            int removedCount = _baseBuildManager.RemoveStockpileZonesOverlapping(selectedCells);
            _buildHud?.ShowFeedback(removedCount > 0 ? $"Removed {removedCount} stockpile zone(s)" : "No stockpile zone here");
            return;
        }

        if (_baseBuildManager.TryCreateStockpileZone(selectedCells, out StockpileZone? zone) && zone != null)
        {
            _buildHud?.ShowFeedback($"Marked stockpile zone: {zone.DisplayName}");
        }
        else
        {
            _buildHud?.ShowFeedback("Cannot mark stockpile zone");
        }
    }

    private bool TryShowRoomInfoAtMousePosition()
    {
        if (_baseBuildManager == null
            || _baseRoomManager == null
            || _worldBuildPanel == null
            || !_worldBuildPanel.IsBaseManagementPanelActive)
        {
            return false;
        }

        Vector2I cell = _baseBuildManager.WorldToCell(GetGlobalMousePosition());
        BaseRoom? room = _baseRoomManager.GetRoomAtCell(cell);

        if (room == null)
        {
            return false;
        }

        _worldBuildPanel.ShowRoomInfo(room);
        return true;
    }

    private bool TryShowStorageInfoAtMousePosition()
    {
        if (_baseBuildManager == null || _worldBuildPanel == null)
        {
            return false;
        }

        Vector2I cell = _baseBuildManager.WorldToCell(GetGlobalMousePosition());

        if (!_baseBuildManager.TryResolveStorageOriginCell(cell, out Vector2I originCell))
        {
            return false;
        }

        _worldBuildPanel.ShowStorageInfo(originCell);
        return true;
    }

    private bool TryShowConstructionSiteInfoAtMousePosition()
    {
        if (_baseBuildManager == null || _worldBuildPanel == null)
        {
            return false;
        }

        Vector2I cell = _baseBuildManager.WorldToCell(GetGlobalMousePosition());

        if (!_baseBuildManager.TryGetConstructionSiteAtCell(cell, out ConstructionSite site))
        {
            return false;
        }

        _worldBuildPanel.ShowConstructionSiteInfo(site);
        return true;
    }

    private bool HandleHarvestDesignationModeInput(InputEventKey keyEvent)
    {
        if (keyEvent.Keycode == Key.V)
        {
            SetHarvestDesignationMode(!_isHarvestDesignationMode);
            CancelSelectionDrag();
            UpdateBuildHud();
            return true;
        }

        if (keyEvent.Keycode == Key.Escape && _isHarvestDesignationMode)
        {
            SetHarvestDesignationMode(false);
            ClearBuildUiSelection();
            CancelSelectionDrag();
            UpdateBuildHud();
            return true;
        }

        return false;
    }

    private void SetHarvestDesignationMode(bool enabled)
    {
        if (_isHarvestDesignationMode == enabled)
        {
            return;
        }

        _isHarvestDesignationMode = enabled;
        EndHarvestDesignationDrag(false);

        if (enabled)
        {
            SetRoomDesignationMode(RoomType.None, false);
            SetStockpileZoneMode(false, false);
            SetFarmZoneMode(false);
            EndBuildDrag();
            _baseBuildManager?.SetBuildMode(TileBuildType.None);
        }

        _buildHud?.ClearFeedback();

        if (DebugHarvestDesignation)
        {
            GD.Print($"Harvest designation mode: {(enabled ? "On" : "Off")}");
        }
    }

    private bool HandleFarmZoneModeInput(InputEventKey keyEvent)
    {
        if (keyEvent.Keycode == Key.Z)
        {
            SetFarmZoneMode(!_isFarmZoneMode);
            CancelSelectionDrag();
            UpdateBuildHud();
            return true;
        }

        if (keyEvent.Keycode == Key.Escape && _isFarmZoneMode)
        {
            SetFarmZoneMode(false);
            ClearBuildUiSelection();
            CancelSelectionDrag();
            UpdateBuildHud();
            return true;
        }

        return false;
    }

    private void SetFarmZoneMode(bool enabled)
    {
        if (_isFarmZoneMode == enabled)
        {
            return;
        }

        _isFarmZoneMode = enabled;
        EndFarmZoneDrag(false);

        if (enabled)
        {
            SetRoomDesignationMode(RoomType.None, false);
            SetStockpileZoneMode(false, false);
            SetHarvestDesignationMode(false);
            EndBuildDrag();
            _baseBuildManager?.SetBuildMode(TileBuildType.None);
        }

        _buildHud?.ClearFeedback();

        if (DebugFarmZone)
        {
            GD.Print($"Farm zone mode: {(enabled ? "On" : "Off")}");
        }
    }

    private void ClearBuildUiSelection()
    {
        SetRoomDesignationMode(RoomType.None, false);
        SetStockpileZoneMode(false, false);
        EndBuildDrag();
        _baseBuildManager?.SetBuildMode(TileBuildType.None);
        _buildHud?.ClearFeedback();
        _worldBuildPanel?.ClearUiSelection();
    }

    private void HandleFarmZoneLeftMousePressed(InputEventMouseButton mouseButton)
    {
        _isFarmZoneDragging = true;
        _farmZoneRemoveMode = mouseButton.ShiftPressed;
        _farmZoneChangedCount = 0;
        _farmZoneDragCells.Clear();
        TryApplyFarmZoneDragAtWorldPosition(GetGlobalMousePosition(), true);
    }

    private void TryApplyFarmZoneDragAtWorldPosition(Vector2 worldPosition, bool showFeedback = false)
    {
        if (_baseBuildManager == null || !_isFarmZoneDragging)
        {
            return;
        }

        Vector2I cell = _baseBuildManager.WorldToCell(worldPosition);

        if (_farmZoneDragCells.Contains(cell))
        {
            return;
        }

        _farmZoneDragCells.Add(cell);
        bool shouldMark = !_farmZoneRemoveMode;

        if (!_baseBuildManager.TrySetFarmZoneAt(cell, shouldMark))
        {
            if (showFeedback)
            {
                string reason = shouldMark ? _baseBuildManager.GetFarmZoneBlockReason(cell) : "No farm zone here";
                _buildHud?.ShowFeedback(shouldMark ? $"Cannot mark farm: {reason}" : reason);
            }

            return;
        }

        _farmZoneChangedCount++;

        if (DebugFarmZone)
        {
            GD.Print($"{(shouldMark ? "Marked" : "Removed")} farm zone at {cell}");
        }

        if (showFeedback)
        {
            _buildHud?.ShowFeedback(shouldMark ? "Marked farm zone" : "Removed farm zone");
        }
    }

    private void EndFarmZoneDrag(bool showSummary)
    {
        if (!_isFarmZoneDragging)
        {
            return;
        }

        bool removeMode = _farmZoneRemoveMode;
        int changedCount = _farmZoneChangedCount;
        _isFarmZoneDragging = false;
        _farmZoneRemoveMode = false;
        _farmZoneChangedCount = 0;
        _farmZoneDragCells.Clear();

        if (showSummary && changedCount > 1)
        {
            _buildHud?.ShowFeedback(removeMode ? $"Removed {changedCount} farm cells" : $"Marked {changedCount} farm cells");
        }
    }

    private void HandleHarvestDesignationLeftMousePressed(InputEventMouseButton mouseButton)
    {
        _isHarvestDesignationDragging = true;
        _harvestDesignationRemoveMode = mouseButton.ShiftPressed;
        _harvestDesignationChangedCount = 0;
        _harvestDesignationDragCells.Clear();
        TryApplyHarvestDesignationDragAtWorldPosition(GetGlobalMousePosition(), true);
    }

    private void TryApplyHarvestDesignationDragAtWorldPosition(Vector2 worldPosition, bool showFeedback = false)
    {
        if (_baseBuildManager == null || !_isHarvestDesignationDragging)
        {
            return;
        }

        Vector2I cell = _baseBuildManager.WorldToCell(worldPosition);

        if (_harvestDesignationDragCells.Contains(cell))
        {
            return;
        }

        _harvestDesignationDragCells.Add(cell);
        bool shouldDesignate = !_harvestDesignationRemoveMode;
        ResourceNode? resourceNode = _baseBuildManager.GetResourceNodeAtCell(cell);

        if (resourceNode == null)
        {
            if (showFeedback)
            {
                _buildHud?.ShowFeedback("No resource here");
            }

            return;
        }

        if (!_baseBuildManager.TrySetHarvestDesignationAt(cell, shouldDesignate))
        {
            if (showFeedback)
            {
                _buildHud?.ShowFeedback(shouldDesignate ? "Cannot mark depleted resource" : "No resource here");
            }

            return;
        }

        _harvestDesignationChangedCount++;

        if (DebugHarvestDesignation)
        {
            GD.Print($"{(shouldDesignate ? "Marked" : "Unmarked")} resource {resourceNode.ResourceType} at {cell}");
        }

        if (showFeedback)
        {
            _buildHud?.ShowFeedback(shouldDesignate ? "Marked resource" : "Unmarked resource");
        }
    }

    private void EndHarvestDesignationDrag(bool showSummary)
    {
        if (!_isHarvestDesignationDragging)
        {
            return;
        }

        bool removeMode = _harvestDesignationRemoveMode;
        int changedCount = _harvestDesignationChangedCount;
        _isHarvestDesignationDragging = false;
        _harvestDesignationRemoveMode = false;
        _harvestDesignationChangedCount = 0;
        _harvestDesignationDragCells.Clear();

        if (showSummary && changedCount > 1)
        {
            _buildHud?.ShowFeedback(removeMode ? $"Unmarked {changedCount} resources" : $"Marked {changedCount} resources");
        }
    }

    private void HandleBuildLeftMousePressed(Vector2 worldPosition)
    {
        if (_baseBuildManager == null)
        {
            return;
        }

        TileBuildType buildType = _baseBuildManager.CurrentBuildMode;

        if (!CanDragBuildType(buildType))
        {
            if (TryApplyBuildAtWorldPosition(worldPosition, buildType, true))
            {
                RevalidateAllMercenaryPaths();
            }

            return;
        }

        _isBuildDragging = true;
        _dragChangedNavigation = false;
        _dragBuildType = buildType;
        _dragBuiltCells.Clear();
        TryApplyBuildDragAtWorldPosition(worldPosition, true);
    }

    private void TryApplyBuildDragAtWorldPosition(Vector2 worldPosition, bool showFeedback = false)
    {
        if (_baseBuildManager == null || !_isBuildDragging)
        {
            return;
        }

        Vector2I cell = _baseBuildManager.WorldToCell(worldPosition);

        if (_dragBuiltCells.Contains(cell))
        {
            return;
        }

        _dragBuiltCells.Add(cell);

        if (TryApplyBuildAtCell(cell, _dragBuildType, showFeedback))
        {
            _dragChangedNavigation = true;
        }
    }

    private bool TryApplyBuildAtWorldPosition(Vector2 worldPosition, TileBuildType buildType, bool showFeedback)
    {
        if (_baseBuildManager == null)
        {
            return false;
        }

        return TryApplyBuildAtCell(_baseBuildManager.WorldToCell(worldPosition), buildType, showFeedback);
    }

    private bool TryApplyBuildAtCell(Vector2I cell, TileBuildType buildType, bool showFeedback)
    {
        if (_baseBuildManager == null)
        {
            return false;
        }

        bool clearingDepletedResourceNode = buildType == TileBuildType.Erase
            && _baseBuildManager.CanRemoveResourceNodeAt(cell);

        if (_baseBuildManager.TryApplyBuildAtCell(buildType, cell))
        {
            _baseRoomManager?.RecalculateAllRooms();

            if (clearingDepletedResourceNode)
            {
                if (showFeedback)
                {
                    _buildHud?.ShowFeedback("Cleared depleted resource node");
                }

                return true;
            }

            if (showFeedback)
            {
                _buildHud?.ClearFeedback();
            }

            return true;
        }

        if (showFeedback)
        {
            string blockReason = _baseBuildManager.GetBuildBlockReason(buildType, cell);
            _buildHud?.ShowFeedback(string.IsNullOrEmpty(blockReason) ? "Build blocked" : $"Build blocked: {blockReason}");
        }

        return false;
    }

    private void EndBuildDrag()
    {
        if (!_isBuildDragging)
        {
            return;
        }

        bool shouldRevalidate = _dragChangedNavigation;
        _isBuildDragging = false;
        _dragChangedNavigation = false;
        _dragBuildType = TileBuildType.None;
        _dragBuiltCells.Clear();

        if (shouldRevalidate)
        {
            RevalidateAllMercenaryPaths();
        }
    }

    private static bool CanDragBuildType(TileBuildType buildType)
    {
        return buildType == TileBuildType.Floor
            || buildType == TileBuildType.Wall
            || buildType == TileBuildType.Erase;
    }

    private void RevalidateAllMercenaryPaths()
    {
        foreach (MercenaryController mercenary in GetAllMercenaries())
        {
            mercenary.RevalidateCurrentPath();
        }
    }

    private bool IsBuildModeActive()
    {
        return _baseBuildManager != null && _baseBuildManager.CurrentBuildMode != TileBuildType.None;
    }

    private void HandleControlModeInput(InputEventKey keyEvent)
    {
        if (_selectedMercenaries.Count == 0)
        {
            return;
        }

        if (keyEvent.Keycode == Key.R)
        {
            ToggleRallyOrDisband();
        }
        else if (keyEvent.Keycode == Key.X)
        {
            ApplyStopCommandToSelectedSquad();
        }
        else if (keyEvent.Keycode == Key.B)
        {
            ApplyDefendCommandToSelectedSquad();
        }
    }

    private void HandleLeftMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.Pressed)
        {
            _isLeftMouseDown = true;
            _isDraggingSelection = false;
            _dragKeepExistingSelection = mouseButton.ShiftPressed;
            _dragStartWorld = GetGlobalMousePosition();
            _dragCurrentWorld = _dragStartWorld;
            _dragStartScreen = GetViewport().GetMousePosition();
            return;
        }

        if (!_isLeftMouseDown)
        {
            return;
        }

        _isLeftMouseDown = false;

        if (_isDraggingSelection)
        {
            SelectMercenariesInDragRect(_dragKeepExistingSelection);
            _isDraggingSelection = false;
            _selectionOverlay?.HideSelectionBox();
            return;
        }

        if (TryShowRoomInfoAtMousePosition())
        {
            return;
        }

        if (TryShowConstructionSiteInfoAtMousePosition())
        {
            return;
        }

        if (TryShowStorageInfoAtMousePosition())
        {
            return;
        }

        MercenaryController? clickedMercenary = FindMercenaryAt(GetGlobalMousePosition());

        if (clickedMercenary == null)
        {
            if (!mouseButton.ShiftPressed)
            {
                ClearSelection();
            }

            return;
        }

        if (mouseButton.ShiftPressed)
        {
            ToggleSelection(clickedMercenary);
        }
        else
        {
            SelectOnly(clickedMercenary);
        }
    }

    private void SelectMercenariesInDragRect(bool keepExistingSelection)
    {
        if (!keepExistingSelection)
        {
            ClearSelection();
        }

        Rect2 selectionRect = GetDragSelectionRect().Abs();

        foreach (MercenaryController mercenary in GetAllMercenaries())
        {
            if (selectionRect.HasPoint(mercenary.GlobalPosition))
            {
                AddToSelection(mercenary);
            }
        }

        UpdateSelectedMercenaryHud();
    }

    private Rect2 GetDragSelectionRect()
    {
        return new Rect2(_dragStartWorld, _dragCurrentWorld - _dragStartWorld);
    }

    private void AddToSelection(MercenaryController mercenary)
    {
        if (_selectedMercenaries.Contains(mercenary))
        {
            return;
        }

        _selectedMercenaries.Add(mercenary);
        mercenary.SetSelected(true);
        UpdateSelectedMercenaryHud();
    }

    private void ClearSelection()
    {
        foreach (MercenaryController mercenary in _selectedMercenaries)
        {
            mercenary.SetSelected(false);
        }

        _selectedMercenaries.Clear();
        _worldBuildPanel?.ClearInfoPanel();
        UpdateSelectedMercenaryHud();
    }

    private void ToggleRallyOrDisband()
    {
        List<MercenaryController> lifeMercenaries = new();

        foreach (MercenaryController mercenary in _selectedMercenaries)
        {
            if (mercenary.ControlMode == MercenaryControlMode.Life)
            {
                lifeMercenaries.Add(mercenary);
            }
            else if (mercenary.ControlMode == MercenaryControlMode.Rallying || mercenary.ControlMode == MercenaryControlMode.Squad)
            {
                mercenary.ReturnToLife();
            }
        }

        Vector2I rallyCenterCell = GetCellForWorldPosition(_rallyPoint?.GlobalPosition ?? Vector2.Zero);
        List<Vector2I> offsets = BuildFormationCellOffsets(lifeMercenaries.Count);

        for (int i = 0; i < lifeMercenaries.Count; i++)
        {
            Vector2I targetCell = rallyCenterCell + offsets[i];

            if (_baseBuildManager != null && (!_baseBuildManager.IsCellInWorld(targetCell) || _baseBuildManager.IsCellBlocked(targetCell)))
            {
                continue;
            }

            lifeMercenaries[i].ToggleRallyState(GetCellCenter(targetCell));
        }
    }

    private void MoveSelectedSquadMercenaries(Vector2 targetCenter)
    {
        List<MercenaryController> squadMercenaries = new();

        foreach (MercenaryController mercenary in _selectedMercenaries)
        {
            if (mercenary.ControlMode == MercenaryControlMode.Squad)
            {
                squadMercenaries.Add(mercenary);
            }
        }

        Vector2I targetCenterCell = GetCellForWorldPosition(targetCenter);
        List<Vector2I> offsets = BuildFormationCellOffsets(squadMercenaries.Count);
        bool debugPathUpdated = false;

        for (int i = 0; i < squadMercenaries.Count; i++)
        {
            MercenaryController mercenary = squadMercenaries[i];
            Vector2I targetCell = targetCenterCell + offsets[i];

            if (_baseBuildManager != null
                && (!_baseBuildManager.IsCellInWorld(targetCell)
                    || _baseBuildManager.IsCellBlocked(targetCell)))
            {
                continue;
            }

            if (_baseBuildManager == null)
            {
                mercenary.MoveTo(GetCellCenter(targetCell));
                continue;
            }

            Vector2I startCell = _baseBuildManager.WorldToCell(mercenary.GlobalPosition);
            List<Vector2I> pathCells = GridPathfinder.FindPath(startCell, targetCell, _baseBuildManager);

            if (startCell != targetCell && pathCells.Count == 0)
            {
                continue;
            }

            List<Vector2> worldPath = BuildWorldPath(pathCells, targetCell);
            mercenary.MoveAlongWorldPath(worldPath);

            if (ShowPathDebug && !debugPathUpdated)
            {
                _pathDebugOverlay?.SetPath(BuildDebugWorldPath(mercenary.GlobalPosition, worldPath));
                debugPathUpdated = true;
            }
        }

        if (ShowPathDebug && !debugPathUpdated)
        {
            _pathDebugOverlay?.ClearPath();
        }
    }

    private void ApplyStopCommandToSelectedSquad()
    {
        foreach (MercenaryController mercenary in _selectedMercenaries)
        {
            if (mercenary.ControlMode == MercenaryControlMode.Squad)
            {
                mercenary.StopSquadCommand();
            }
        }
    }

    private void ApplyDefendCommandToSelectedSquad()
    {
        foreach (MercenaryController mercenary in _selectedMercenaries)
        {
            if (mercenary.ControlMode == MercenaryControlMode.Squad)
            {
                mercenary.SetSquadDefending();
            }
        }
    }

    private List<MercenaryController> GetAllMercenaries()
    {
        List<MercenaryController> mercenaries = new();
        Node? unitLayer = GetNodeOrNull("UnitLayer");

        if (unitLayer == null)
        {
            return mercenaries;
        }

        foreach (Node child in unitLayer.GetChildren())
        {
            if (child is MercenaryController mercenary)
            {
                mercenaries.Add(mercenary);
            }
        }

        return mercenaries;
    }

    private List<Vector2I> BuildFormationCellOffsets(int count)
    {
        List<Vector2I> offsets = new();

        if (count <= 0)
        {
            return offsets;
        }

        offsets.Add(Vector2I.Zero);

        int ring = 1;
        while (offsets.Count < count)
        {
            for (int y = -ring; y <= ring && offsets.Count < count; y++)
            {
                for (int x = -ring; x <= ring && offsets.Count < count; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != ring)
                    {
                        continue;
                    }

                    offsets.Add(new Vector2I(x, y));
                }
            }

            ring++;
        }

        return offsets;
    }

    private Vector2I GetCellForWorldPosition(Vector2 worldPosition)
    {
        if (_baseBuildManager != null)
        {
            return _baseBuildManager.WorldToCell(worldPosition);
        }

        int tileSize = _worldGrid?.TileSize ?? 32;
        return new Vector2I(Mathf.FloorToInt(worldPosition.X / tileSize), Mathf.FloorToInt(worldPosition.Y / tileSize));
    }

    private Vector2 GetCellCenter(Vector2I cell)
    {
        if (_baseBuildManager != null)
        {
            return _baseBuildManager.CellToWorldCenter(cell);
        }

        int tileSize = _worldGrid?.TileSize ?? 32;
        return new Vector2((cell.X + 0.5f) * tileSize, (cell.Y + 0.5f) * tileSize);
    }

    private List<Vector2> BuildWorldPath(IReadOnlyList<Vector2I> pathCells, Vector2I fallbackTargetCell)
    {
        List<Vector2> worldPath = new();

        if (pathCells.Count == 0)
        {
            worldPath.Add(GetCellCenter(fallbackTargetCell));
            return worldPath;
        }

        foreach (Vector2I pathCell in pathCells)
        {
            worldPath.Add(GetCellCenter(pathCell));
        }

        return worldPath;
    }

    private static List<Vector2> BuildDebugWorldPath(Vector2 startPosition, IReadOnlyList<Vector2> worldPath)
    {
        List<Vector2> debugPath = new() { startPosition };
        debugPath.AddRange(worldPath);
        return debugPath;
    }

    private void UpdateSelectedMercenaryHud()
    {
        _selectedMercenaryHud?.UpdateSelectionSummary(_selectedMercenaries);
    }

    private void UpdateBaseAlertState()
    {
        _baseAlertState?.UpdateFromMercenaries(GetTree().GetNodesInGroup("mercenaries"));
    }

    private void UpdateBuildHud()
    {
        if (_buildHud == null)
        {
            return;
        }

        if (_baseBuildManager == null)
        {
            _buildHud.UpdateBuildMode(TileBuildType.None);
            return;
        }

        if (_isHarvestDesignationMode)
        {
            string actionText = _isHarvestDesignationDragging && _harvestDesignationRemoveMode
                ? "Mode: Harvest Designation\nShift Drag: Unmark Resources\nRight Click / Esc: Cancel"
                : "Mode: Harvest Designation\nLeft Drag: Mark Resources\nShift Drag: Unmark\nRight Click / Esc: Cancel";
            _buildHud.UpdateBuildStatus(actionText);
            return;
        }

        if (_isFarmZoneMode)
        {
            string actionText = _isFarmZoneDragging && _farmZoneRemoveMode
                ? "Mode: Farm Zone\nShift Drag: Remove Farm Area\nRight Click / Esc: Cancel"
                : "Mode: Farm Zone\nLeft Drag: Mark Farm Area\nShift Drag: Remove\nRight Click / Esc: Cancel";
            _buildHud.UpdateBuildStatus(actionText);
            return;
        }

        _buildHud.UpdateBuildStatus(_baseBuildManager.GetBuildStatusText());
    }

    private void CancelSelectionDrag()
    {
        _isLeftMouseDown = false;
        _isDraggingSelection = false;
        _selectionOverlay?.HideSelectionBox();
    }

    private void UpdatePathDebugOverlayVisibility()
    {
        if (!ShowPathDebug)
        {
            _pathDebugOverlay?.ClearPath();
        }
    }

    private void UpdateFacilityDebugOverlayVisibility()
    {
        _facilityDebugOverlay?.SetEnabled(ShowFacilityDebug);
    }
}
