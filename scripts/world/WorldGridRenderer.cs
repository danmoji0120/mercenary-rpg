using System;
using Godot;

public partial class WorldGridRenderer : Node2D
{
    private int _worldWidth = 64;
    private int _worldHeight = 64;
    private int _tileSize = 32;
    private bool _showGrid = true;

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
                DrawRect(tileRect, ((x + y) & 1) == 0 ? grass : grassAlt);
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
