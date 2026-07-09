using Godot;

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
    private bool _cameraInputWasLocked;

    public override void _Ready()
    {
        _worldManager = GetNodeOrNull<WorldManagerV2>("WorldManagerV2");
        _gridRenderer = GetNodeOrNull<WorldV2GridRenderer>("GridLayer");
        _buildManager = GetNodeOrNull<WorldV2BuildManager>("BuildingLayer");
        _camera = GetNodeOrNull<WorldV2CameraController>("Camera2D");
        _streamManager = GetNodeOrNull<WorldStreamManagerV2>("WorldStreamManagerV2");
        CreateLoadingOverlay();
        CreateWorldMapOverlay();

        if (_camera != null)
        {
            CenterCameraOnPlayerStart();
        }
    }

    public override void _Process(double delta)
    {
        bool loading = _streamManager != null && !_streamManager.IsInitialLoadingComplete;
        SetCameraInputLocked(loading || IsWorldMapOverlayOpen());
        _loadingOverlay?.Refresh(_streamManager);
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
                }

                return;
            }

            if (keyEvent.Keycode == Key.M && !IsWorldInputLocked())
            {
                ToggleWorldMapOverlay();
                return;
            }

            if (IsWorldInputLocked() && !IsDebugKeyAllowedWhileLoading(keyEvent.Keycode))
            {
                return;
            }

            HandleKey(keyEvent);
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

            HandleMouseButton(mouseButton);
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
                _worldManager.UpdateDebugHud("Printed performance summary.");
                break;
            case Key.F11:
                _worldManager.RegenerateVisibleChunks(clearRuntimeStructures: false);
                break;
            case Key.F12:
                _worldManager.RegenerateVisibleChunks(clearRuntimeStructures: true);
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
            _ => 0
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
