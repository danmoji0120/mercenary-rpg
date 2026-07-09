using Godot;

namespace WorldV2;

public partial class WorldMapOverlayV2 : Control
{
    [Export]
    public bool ShowDebugInfo { get; set; } = false;

    private WorldManagerV2? _manager;
    private Texture2D? _texture;
    private Label? _titleLabel;
    private Label? _hintLabel;
    private Rect2 _mapRect;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 200;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _titleLabel = new Label
        {
            Text = "World Map",
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        AddChild(_titleLabel);

        _hintLabel = new Label
        {
            Text = "M / ESC: Close Map",
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        AddChild(_hintLabel);
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        UpdateLabelLayout();
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 viewportSize = GetOverlaySize();
        DrawRect(new Rect2(Vector2.Zero, viewportSize), new Color(0.0f, 0.0f, 0.0f, 0.66f));

        if (_texture == null || _manager == null)
        {
            return;
        }

        _mapRect = CalculateMapRect(_texture);
        Rect2 panelRect = _mapRect.Grow(20.0f);
        DrawRect(panelRect, new Color(0.055f, 0.060f, 0.052f, 0.98f));
        DrawRect(panelRect, new Color(0.50f, 0.46f, 0.35f, 0.90f), filled: false, width: 2.0f);
        DrawTextureRect(_texture, _mapRect, tile: false);

        DrawCurrentPositionMarker();
    }

    public void ShowMap(WorldManagerV2 manager)
    {
        _manager = manager;
        _texture = manager.GetOrBuildWorldMapTexture("opened overlay");
        Visible = true;
        manager.SetWorldMapOverlayVisible(true);
        UpdateLabelLayout();
        QueueRedraw();
    }

    public void HideMap()
    {
        Visible = false;
        _manager?.SetWorldMapOverlayVisible(false);
    }

    public void Toggle(WorldManagerV2 manager)
    {
        if (Visible)
        {
            HideMap();
            return;
        }

        ShowMap(manager);
    }

    private void DrawCurrentPositionMarker()
    {
        if (_manager == null)
        {
            return;
        }

        WorldStreamManagerV2? streamManager = _manager.GetStreamManager();
        Vector2I currentCell = streamManager?.CenterGlobalCellCoord ?? _manager.PlayerStartGlobalCell;
        Vector2 currentPosition = CellToMapPosition(currentCell);
        DrawCircle(currentPosition, 6.0f, new Color(1.0f, 0.22f, 0.18f, 0.95f));
        DrawCircle(currentPosition, 9.0f, new Color(1.0f, 0.95f, 0.72f, 0.35f));

        if (_manager.PlanVersion == WorldPlanVersionV2.V3)
        {
            Vector2 startPosition = CellToMapPosition(_manager.V3StartingVillageCenter);
            DrawCircle(startPosition, 5.0f, new Color(1.0f, 0.88f, 0.30f, 0.82f));
        }
    }

    private Vector2 CellToMapPosition(Vector2I cell)
    {
        if (_manager == null)
        {
            return _mapRect.GetCenter();
        }

        float x = _mapRect.Position.X + cell.X / Mathf.Max(1.0f, _manager.WorldMapSize.WidthCells - 1.0f) * _mapRect.Size.X;
        float y = _mapRect.Position.Y + cell.Y / Mathf.Max(1.0f, _manager.WorldMapSize.HeightCells - 1.0f) * _mapRect.Size.Y;
        return new Vector2(
            Mathf.Clamp(x, _mapRect.Position.X, _mapRect.End.X),
            Mathf.Clamp(y, _mapRect.Position.Y, _mapRect.End.Y));
    }

    private Rect2 CalculateMapRect(Texture2D texture)
    {
        Vector2 viewportSize = GetOverlaySize();
        Vector2 available = new(
            Mathf.Max(280.0f, viewportSize.X * 0.88f),
            Mathf.Max(220.0f, viewportSize.Y * 0.80f));
        float aspect = texture.GetWidth() / (float)Mathf.Max(1, texture.GetHeight());
        Vector2 mapSize = available;
        if (mapSize.X / mapSize.Y > aspect)
        {
            mapSize.X = mapSize.Y * aspect;
        }
        else
        {
            mapSize.Y = mapSize.X / aspect;
        }

        return new Rect2((viewportSize - mapSize) * 0.5f, mapSize);
    }

    private void UpdateLabelLayout()
    {
        if (_titleLabel == null || _hintLabel == null || _texture == null)
        {
            return;
        }

        _mapRect = CalculateMapRect(_texture);
        float labelWidth = Mathf.Max(280.0f, _mapRect.Size.X);
        _titleLabel.Size = new Vector2(labelWidth, 28.0f);
        _hintLabel.Size = new Vector2(labelWidth, 24.0f);
        _titleLabel.Position = new Vector2(_mapRect.Position.X, Mathf.Max(12.0f, _mapRect.Position.Y - 42.0f));
        _hintLabel.Position = new Vector2(_mapRect.Position.X, Mathf.Min(GetOverlaySize().Y - 32.0f, _mapRect.End.Y + 14.0f));

        if (_manager != null)
        {
            _titleLabel.Text = $"World Map  {_manager.MapSizePreset}";
            _hintLabel.Text = ShowDebugInfo
                ? $"M / ESC: Close Map    texture={_manager.WorldMapTextureSize.X}x{_manager.WorldMapTextureSize.Y} build={_manager.WorldMapBuildMs:0.0}ms cached={_manager.WorldMapCached}"
                : "M / ESC: Close Map";
        }
    }

    private Vector2 GetOverlaySize()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        if (viewportSize.X > 1.0f && viewportSize.Y > 1.0f)
        {
            return viewportSize;
        }

        return new Vector2(Mathf.Max(1.0f, Size.X), Mathf.Max(1.0f, Size.Y));
    }
}
