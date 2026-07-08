using System;
using System.Collections.Generic;
using Godot;

public partial class WorldGridRenderer : Node2D
{
    private int _worldWidth = 64;
    private int _worldHeight = 64;
    private int _tileSize = 32;
    private bool _showGrid = true;
    private readonly Dictionary<Vector2I, Color> _generatedTerrainColors = new();
    private readonly HashSet<Vector2I> _buildRestrictedCells = new();

    [Export]
    public int WorldWidth
    {
        get => _worldWidth;
        set
        {
            _worldWidth = Math.Max(1, value);
            QueueRedraw();
        }
    }

    [Export]
    public int WorldHeight
    {
        get => _worldHeight;
        set
        {
            _worldHeight = Math.Max(1, value);
            QueueRedraw();
        }
    }

    [Export]
    public int TileSize
    {
        get => _tileSize;
        set
        {
            _tileSize = Math.Max(1, value);
            QueueRedraw();
        }
    }

    [Export]
    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            _showGrid = value;
            QueueRedraw();
        }
    }

    public Rect2 GetWorldRect()
    {
        return new Rect2(Vector2.Zero, new Vector2(WorldWidth * TileSize, WorldHeight * TileSize));
    }

    public void ApplySectorGeneration(SectorGenerationData sectorData)
    {
        if (sectorData == null)
        {
            return;
        }

        WorldWidth = sectorData.DisplayWidth;
        WorldHeight = sectorData.DisplayHeight;
        _generatedTerrainColors.Clear();
        _buildRestrictedCells.Clear();

        foreach (KeyValuePair<Vector2I, Color> entry in sectorData.TerrainColors)
        {
            _generatedTerrainColors[entry.Key] = entry.Value;
        }

        foreach (Vector2I cell in sectorData.BuildRestrictedCells)
        {
            _buildRestrictedCells.Add(cell);
        }

        QueueRedraw();
    }

    public void ClearSectorGeneration()
    {
        _generatedTerrainColors.Clear();
        _buildRestrictedCells.Clear();
        QueueRedraw();
    }

    public bool IsBuildRestrictedCell(Vector2I cell)
    {
        return _buildRestrictedCells.Contains(cell);
    }

    public override void _Draw()
    {
        Color grass = new Color(0.28f, 0.47f, 0.28f);
        Color grassAlt = new Color(0.24f, 0.42f, 0.25f);
        Color grid = new Color(0.08f, 0.13f, 0.1f, 0.42f);

        for (int y = 0; y < WorldHeight; y++)
        {
            for (int x = 0; x < WorldWidth; x++)
            {
                Rect2 tileRect = new Rect2(x * TileSize, y * TileSize, TileSize, TileSize);
                Vector2I cell = new(x, y);
                Color tileColor = _generatedTerrainColors.TryGetValue(cell, out Color generatedColor)
                    ? generatedColor
                    : ((x + y) & 1) == 0 ? grass : grassAlt;
                DrawRect(tileRect, tileColor);

                if (_buildRestrictedCells.Contains(cell) && (x + y) % 7 == 0)
                {
                    DrawRect(tileRect.Grow(-8.0f), new Color(0.85f, 0.62f, 0.28f, 0.10f));
                }
            }
        }

        if (!ShowGrid)
        {
            return;
        }

        float worldPixelWidth = WorldWidth * TileSize;
        float worldPixelHeight = WorldHeight * TileSize;

        for (int x = 0; x <= WorldWidth; x++)
        {
            float pixelX = x * TileSize;
            DrawLine(new Vector2(pixelX, 0.0f), new Vector2(pixelX, worldPixelHeight), grid);
        }

        for (int y = 0; y <= WorldHeight; y++)
        {
            float pixelY = y * TileSize;
            DrawLine(new Vector2(0.0f, pixelY), new Vector2(worldPixelWidth, pixelY), grid);
        }
    }
}
