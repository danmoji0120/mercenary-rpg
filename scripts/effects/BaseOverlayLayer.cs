using System.Collections.Generic;
using Godot;

public partial class BaseOverlayLayer : Node2D
{
    [Export]
    public NodePath BuildManagerPath { get; set; } = "../../BuildingLayer";

    [Export]
    public bool ShowOverlayDiagnostics { get; set; } = true;

    [Export]
    public bool ShowConstructionSites { get; set; } = true;

    [Export]
    public bool ShowStockpileZones { get; set; } = true;

    private BaseBuildManager? _buildManager;
    private CanvasLayer? _diagnosticsCanvasLayer;
    private Label? _diagnosticsLabel;
    private bool _warnedMissingManager;

    public override void _Ready()
    {
        ZIndex = 500;
        EnsureDiagnosticsLabel();
        ResolveBuildManager();

        if (_buildManager == null)
        {
            _warnedMissingManager = true;
            GD.PushWarning("BaseOverlayLayer could not find BaseBuildManager. Check BuildManagerPath.");
        }
    }

    public override void _Process(double delta)
    {
        if (_buildManager == null)
        {
            ResolveBuildManager();
        }

        UpdateDiagnosticsLabel();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_buildManager == null)
        {
            return;
        }

        if (ShowStockpileZones)
        {
            DrawStockpileZones();
        }

        if (ShowConstructionSites)
        {
            DrawConstructionSites();
        }
    }

    private void ResolveBuildManager()
    {
        if (!BuildManagerPath.IsEmpty)
        {
            _buildManager = GetNodeOrNull<BaseBuildManager>(BuildManagerPath);
        }

        _buildManager ??= GetTree().CurrentScene?.GetNodeOrNull<BaseBuildManager>("BuildingLayer");

        if (_buildManager != null)
        {
            _warnedMissingManager = false;
        }
        else if (!_warnedMissingManager)
        {
            _warnedMissingManager = true;
            GD.PushWarning("BaseOverlayLayer could not find BaseBuildManager. Check BuildManagerPath.");
        }
    }

    private void EnsureDiagnosticsLabel()
    {
        if (_diagnosticsCanvasLayer != null && _diagnosticsLabel != null)
        {
            return;
        }

        _diagnosticsCanvasLayer = new CanvasLayer
        {
            Name = "BaseOverlayDiagnosticsCanvas",
            Layer = 90
        };
        AddChild(_diagnosticsCanvasLayer);

        _diagnosticsLabel = new Label
        {
            Name = "BaseOverlayDiagnosticsLabel",
            Position = new Vector2(14.0f, 14.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _diagnosticsLabel.AddThemeFontSizeOverride("font_size", 15);
        _diagnosticsLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.70f, 1.0f));
        _diagnosticsLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.92f));
        _diagnosticsLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _diagnosticsLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _diagnosticsCanvasLayer.AddChild(_diagnosticsLabel);
    }

    private void UpdateDiagnosticsLabel()
    {
        EnsureDiagnosticsLabel();

        if (_diagnosticsLabel == null)
        {
            return;
        }

        _diagnosticsLabel.Visible = ShowOverlayDiagnostics;

        if (!ShowOverlayDiagnostics)
        {
            return;
        }

        if (_buildManager == null)
        {
            _diagnosticsLabel.Text = "BaseOverlay\nmanager missing";
            return;
        }

        int siteCount = _buildManager.GetAllConstructionSites().Count;
        int zoneCount = _buildManager.GetAllStockpileZones().Count;
        _diagnosticsLabel.Text = $"BaseOverlay OK\nsites {siteCount}\nzones {zoneCount}";
    }

    private void DrawConstructionSites()
    {
        Font? font = ThemeDB.FallbackFont;
        int tileSize = _buildManager?.TileSize ?? 32;

        foreach (ConstructionSite site in _buildManager!.GetAllConstructionSites())
        {
            if (site.IsCompleted)
            {
                continue;
            }

            Color baseColor = GetConstructionSiteColor(site);

            foreach (Vector2I cell in site.OccupiedCells)
            {
                Rect2 cellRect = GetCellRect(cell, tileSize);
                DrawRect(cellRect.Grow(-2.0f), new Color(baseColor.R, baseColor.G, baseColor.B, 0.46f));
                DrawRect(cellRect.Grow(-2.0f), new Color(baseColor.R, baseColor.G, baseColor.B, 0.95f), false, 3.0f);
            }

            Rect2 bounds = GetCellsBounds(site.OccupiedCells, tileSize);
            DrawRect(bounds.Grow(-1.0f), new Color(baseColor.R, baseColor.G, baseColor.B, 1.0f), false, 5.0f);

            if (font != null)
            {
                DrawWorldLabel(font, bounds.Position + new Vector2(4.0f, -8.0f), GetConstructionSiteLabel(site), baseColor);
            }
        }
    }

    private void DrawStockpileZones()
    {
        Font? font = ThemeDB.FallbackFont;
        int tileSize = _buildManager?.TileSize ?? 32;
        Color baseColor = new Color(0.20f, 0.56f, 1.0f, 1.0f);

        foreach (StockpileZone zone in _buildManager!.GetAllStockpileZones())
        {
            foreach (Vector2I cell in zone.Cells)
            {
                Rect2 cellRect = GetCellRect(cell, tileSize);
                DrawRect(cellRect.Grow(-2.0f), new Color(baseColor.R, baseColor.G, baseColor.B, 0.36f));
                DrawRect(cellRect.Grow(-2.0f), new Color(baseColor.R, baseColor.G, baseColor.B, 0.90f), false, 2.5f);
            }

            Rect2 bounds = GetCellsBounds(zone.Cells, tileSize);
            DrawRect(bounds.Grow(-1.0f), new Color(baseColor.R, baseColor.G, baseColor.B, 1.0f), false, 4.0f);

            if (font != null)
            {
                DrawWorldLabel(font, bounds.Position + new Vector2(4.0f, -8.0f), GetStockpileZoneLabel(zone), baseColor);
            }
        }
    }

    private void DrawWorldLabel(Font font, Vector2 position, string text, Color accentColor)
    {
        Vector2 textPosition = position;
        string[] lines = text.Split('\n');
        float maxWidth = 0.0f;
        int fontSize = 14;

        foreach (string line in lines)
        {
            maxWidth = Mathf.Max(maxWidth, font.GetStringSize(line, HorizontalAlignment.Left, -1.0f, fontSize).X);
        }

        Rect2 backgroundRect = new Rect2(textPosition + new Vector2(-3.0f, -fontSize - 4.0f), new Vector2(maxWidth + 8.0f, lines.Length * (fontSize + 3.0f) + 7.0f));
        DrawRect(backgroundRect, new Color(0.02f, 0.025f, 0.03f, 0.78f));
        DrawRect(backgroundRect, new Color(accentColor.R, accentColor.G, accentColor.B, 0.85f), false, 1.5f);

        Vector2 linePosition = textPosition;

        foreach (string line in lines)
        {
            DrawString(font, linePosition + new Vector2(1.0f, 1.0f), line, HorizontalAlignment.Left, -1.0f, fontSize, new Color(0.0f, 0.0f, 0.0f, 0.88f));
            DrawString(font, linePosition, line, HorizontalAlignment.Left, -1.0f, fontSize, new Color(1.0f, 0.98f, 0.86f, 1.0f));
            linePosition.Y += fontSize + 3.0f;
        }
    }

    private string GetConstructionSiteLabel(ConstructionSite site)
    {
        return site.State switch
        {
            ConstructionSiteState.WaitingForMaterials => $"{site.DisplayName}\nmaterials {site.GetDeliveredTotal()} / {site.GetRequiredTotal()}",
            ConstructionSiteState.ReadyToBuild => $"{site.DisplayName}\nbuild ready",
            ConstructionSiteState.Building => $"{site.DisplayName}\nbuild {(int)(site.GetProgressRatio() * 100.0f)}%",
            ConstructionSiteState.Cancelled => $"{site.DisplayName}\ncancelled",
            _ => $"{site.DisplayName}\ninvalid"
        };
    }

    private string GetStockpileZoneLabel(StockpileZone zone)
    {
        Vector2I originCell = GetStockpileOriginCell(zone);
        int usedWeight = _buildManager?.GetStorageUsedWeight(originCell) ?? 0;
        int capacity = _buildManager?.GetStockpileZoneCapacity(zone) ?? zone.WeightCapacity;
        string policy = _buildManager?.GetStoragePolicySummary(originCell) ?? "-";
        return $"{zone.DisplayName}\n{usedWeight} / {capacity}\n{policy}";
    }

    private static Color GetConstructionSiteColor(ConstructionSite site)
    {
        return site.State switch
        {
            ConstructionSiteState.WaitingForMaterials => new Color(1.0f, 0.55f, 0.12f, 1.0f),
            ConstructionSiteState.ReadyToBuild => new Color(0.20f, 0.58f, 1.0f, 1.0f),
            ConstructionSiteState.Building => new Color(0.18f, 0.92f, 0.34f, 1.0f),
            ConstructionSiteState.Cancelled => new Color(1.0f, 0.12f, 0.10f, 1.0f),
            _ => new Color(1.0f, 0.18f, 0.16f, 1.0f)
        };
    }

    private static Rect2 GetCellRect(Vector2I cell, int tileSize)
    {
        return new Rect2(new Vector2(cell.X * tileSize, cell.Y * tileSize), new Vector2(tileSize, tileSize));
    }

    private static Rect2 GetCellsBounds(IEnumerable<Vector2I> cells, int tileSize)
    {
        bool hasCell = false;
        int minX = 0;
        int minY = 0;
        int maxX = 0;
        int maxY = 0;

        foreach (Vector2I cell in cells)
        {
            if (!hasCell)
            {
                minX = maxX = cell.X;
                minY = maxY = cell.Y;
                hasCell = true;
                continue;
            }

            minX = Mathf.Min(minX, cell.X);
            minY = Mathf.Min(minY, cell.Y);
            maxX = Mathf.Max(maxX, cell.X);
            maxY = Mathf.Max(maxY, cell.Y);
        }

        return hasCell
            ? new Rect2(new Vector2(minX * tileSize, minY * tileSize), new Vector2((maxX - minX + 1) * tileSize, (maxY - minY + 1) * tileSize))
            : new Rect2();
    }

    private static Vector2I GetStockpileOriginCell(StockpileZone zone)
    {
        Vector2I originCell = default;
        bool hasCell = false;

        foreach (Vector2I cell in zone.Cells)
        {
            if (!hasCell || cell.Y < originCell.Y || (cell.Y == originCell.Y && cell.X < originCell.X))
            {
                originCell = cell;
                hasCell = true;
            }
        }

        return originCell;
    }
}
