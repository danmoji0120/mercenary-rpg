using Godot;

namespace WorldV2;

public partial class ChunkRendererV2 : Node2D
{
    public int TileSize { get; private set; } = 24;
    public bool ShowGrid { get; private set; } = false;
    public WorldV2OverlayMode OverlayMode { get; private set; } = WorldV2OverlayMode.Normal;
    public ChunkDataV2? ChunkData { get; private set; }

    public void Initialize(ChunkDataV2 chunkData, int tileSize, bool showGrid, WorldV2OverlayMode overlayMode)
    {
        ChunkData = chunkData;
        TileSize = Mathf.Max(1, tileSize);
        ShowGrid = showGrid;
        OverlayMode = overlayMode;
        Position = new Vector2(
            chunkData.OriginGlobalCell.X * TileSize,
            chunkData.OriginGlobalCell.Y * TileSize);
        QueueRedraw();
    }

    public void SetShowGrid(bool showGrid)
    {
        ShowGrid = showGrid;
        QueueRedraw();
    }

    public void SetOverlayMode(WorldV2OverlayMode overlayMode)
    {
        OverlayMode = overlayMode;
        QueueRedraw();
    }

    public void ClearChunk()
    {
        ChunkData = null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (ChunkData == null)
        {
            return;
        }

        long rebuildStart = WorldV2PerformanceProfiler.Instance.BeginSample();
        for (int localY = 0; localY < ChunkDataV2.ChunkSize; localY++)
        {
            for (int localX = 0; localX < ChunkDataV2.ChunkSize; localX++)
            {
                if (!ChunkData.TryGetCellLocal(localX, localY, out CellData? data) || data == null)
                {
                    continue;
                }

                Rect2 rect = new(localX * TileSize, localY * TileSize, TileSize, TileSize);
                DrawRect(rect, WorldV2GridRenderer.GetTileColor(data, OverlayMode));

                if (data.IsBuildRestricted && ((data.GlobalCellCoord.X + data.GlobalCellCoord.Y) % 5 == 0))
                {
                    DrawRect(rect.Grow(-6.0f), new Color(0.95f, 0.70f, 0.24f, 0.16f));
                }

                if (data.ResourceType != WorldResourceTypeV2.None)
                {
                    DrawRect(rect.Grow(-7.0f), WorldV2GridRenderer.GetResourceColor(data.ResourceType));
                }

                if (data.HasOreSpot)
                {
                    DrawRect(rect.Grow(-8.0f), new Color(0.86f, 0.78f, 0.44f, 0.95f));
                }
            }
        }

        if (ShowGrid)
        {
            DrawGrid();
        }

        WorldV2PerformanceProfiler.Instance.EndSample(WorldV2PerformanceProfiler.RendererRebuild, rebuildStart, ChunkData.GlobalChunkCoord);
    }

    private void DrawGrid()
    {
        Color gridColor = new(0.05f, 0.08f, 0.07f, 0.34f);
        float pixelSize = WorldV2CoordinateUtility.ChunkSizeCells * TileSize;

        for (int i = 0; i <= WorldV2CoordinateUtility.ChunkSizeCells; i++)
        {
            float pixel = i * TileSize;
            DrawLine(new Vector2(pixel, 0.0f), new Vector2(pixel, pixelSize), gridColor);
            DrawLine(new Vector2(0.0f, pixel), new Vector2(pixelSize, pixel), gridColor);
        }
    }
}
