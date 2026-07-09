using System.Collections.Generic;
using Godot;

namespace WorldV2;

public partial class WorldV2GridRenderer : Node2D
{
    [Export]
    public int TileSize { get; set; } = 24;

    [Export]
    public bool ShowGrid { get; set; } = false;

    [Export]
    public WorldV2OverlayMode OverlayMode { get; set; } = WorldV2OverlayMode.Normal;

    private SectorData? _activeSector;

    public SectorData? ActiveSector => _activeSector;

    public Rect2 GetWorldRect()
    {
        if (_activeSector == null)
        {
            return new Rect2(new Vector2(-1000000.0f, -1000000.0f), new Vector2(2000000.0f, 2000000.0f));
        }

        return new Rect2(Vector2.Zero, new Vector2(_activeSector.WidthCells * TileSize, _activeSector.HeightCells * TileSize));
    }

    public void ApplySector(SectorData sector)
    {
        _activeSector = sector;
        QueueRedraw();
    }

    public void ClearSector()
    {
        _activeSector = null;
        QueueRedraw();
    }

    public bool TryGetCell(Vector2I localCell, out CellData? cell)
    {
        cell = null;
        return _activeSector?.TryGetCell(localCell, out cell) == true;
    }

    public Vector2I WorldToCell(Vector2 worldPosition)
    {
        return new Vector2I(
            Mathf.FloorToInt(worldPosition.X / TileSize),
            Mathf.FloorToInt(worldPosition.Y / TileSize));
    }

    public Vector2 CellToWorldCenter(Vector2I localCell)
    {
        return new Vector2(
            localCell.X * TileSize + TileSize * 0.5f,
            localCell.Y * TileSize + TileSize * 0.5f);
    }

    public override void _Draw()
    {
        if (_activeSector == null)
        {
            return;
        }

        foreach (KeyValuePair<Vector2I, CellData> entry in _activeSector.Cells)
        {
            Vector2I cell = entry.Key;
            CellData data = entry.Value;
            Rect2 rect = new(cell.X * TileSize, cell.Y * TileSize, TileSize, TileSize);
            DrawRect(rect, GetTileColor(data, OverlayMode));

            if (data.IsBuildRestricted && ((cell.X + cell.Y) % 5 == 0))
            {
                DrawRect(rect.Grow(-6.0f), new Color(0.95f, 0.70f, 0.24f, 0.16f));
            }

            if (data.HasOreSpot)
            {
                DrawRect(rect.Grow(-8.0f), new Color(0.78f, 0.72f, 0.48f));
            }
            else if (data.ResourceType != WorldResourceTypeV2.None)
            {
                DrawRect(rect.Grow(-7.0f), GetResourceColor(data.ResourceType));
            }
        }

        if (ShowGrid)
        {
            DrawGrid(_activeSector.WidthCells, _activeSector.HeightCells);
        }
    }

    private void DrawGrid(int widthCells, int heightCells)
    {
        Color gridColor = new(0.05f, 0.08f, 0.07f, 0.38f);
        float pixelWidth = widthCells * TileSize;
        float pixelHeight = heightCells * TileSize;

        for (int x = 0; x <= widthCells; x++)
        {
            float pixelX = x * TileSize;
            DrawLine(new Vector2(pixelX, 0.0f), new Vector2(pixelX, pixelHeight), gridColor);
        }

        for (int y = 0; y <= heightCells; y++)
        {
            float pixelY = y * TileSize;
            DrawLine(new Vector2(0.0f, pixelY), new Vector2(pixelWidth, pixelY), gridColor);
        }
    }

    public static Color GetTileColor(CellData cell)
    {
        return GetTileColor(cell, WorldV2OverlayMode.Normal);
    }

    public static Color GetTileColor(CellData cell, WorldV2OverlayMode overlayMode)
    {
        if (overlayMode != WorldV2OverlayMode.Normal)
        {
            return GetOverlayColor(cell, overlayMode);
        }

        return cell.TileType switch
        {
            TileType.Grass => GetBiomeGroundColor(cell.BiomeKind),
            TileType.TownPavement => new Color(0.38f, 0.39f, 0.34f),
            TileType.Plaza => new Color(0.45f, 0.43f, 0.35f),
            TileType.Road => new Color(0.56f, 0.46f, 0.27f),
            TileType.BridgeCandidate => new Color(0.76f, 0.65f, 0.39f),
            TileType.ForestGround => new Color(0.22f, 0.42f, 0.24f),
            TileType.LightForest => new Color(0.28f, 0.50f, 0.29f),
            TileType.DenseForest => new Color(0.13f, 0.30f, 0.20f),
            TileType.QuarryGround or TileType.StoneField => new Color(0.55f, 0.54f, 0.47f),
            TileType.OreSpot => new Color(0.72f, 0.68f, 0.50f),
            TileType.Rubble or TileType.Ruin => new Color(0.52f, 0.49f, 0.43f),
            TileType.FactoryFloor => new Color(0.34f, 0.36f, 0.36f),
            TileType.Dungeon => new Color(0.34f, 0.27f, 0.40f),
            TileType.BanditCamp => new Color(0.45f, 0.25f, 0.21f),
            TileType.FactionOutpost => new Color(0.34f, 0.43f, 0.52f),
            TileType.Village => new Color(0.70f, 0.60f, 0.39f),
            TileType.VillageEdge => new Color(0.58f, 0.48f, 0.31f),
            TileType.Wasteland => new Color(0.42f, 0.38f, 0.31f),
            TileType.Dirt => new Color(0.44f, 0.39f, 0.25f),
            TileType.Water => new Color(0.17f, 0.44f, 0.53f),
            TileType.RiverBank => new Color(0.55f, 0.48f, 0.30f),
            TileType.WetGrass => new Color(0.43f, 0.56f, 0.33f),
            TileType.Coast => new Color(0.58f, 0.53f, 0.34f),
            TileType.Swamp => new Color(0.22f, 0.35f, 0.26f),
            TileType.Desert => new Color(0.64f, 0.55f, 0.31f),
            TileType.Snow => new Color(0.72f, 0.78f, 0.80f),
            TileType.Hills => new Color(0.40f, 0.48f, 0.35f),
            TileType.Mountain => new Color(0.46f, 0.46f, 0.44f),
            TileType.Toxic => new Color(0.30f, 0.35f, 0.21f),
            _ => new Color(0.37f, 0.53f, 0.31f)
        };
    }

    private static Color GetBiomeGroundColor(BiomeKindV3 biomeKind)
    {
        return biomeKind switch
        {
            BiomeKindV3.ForestLand => new Color(0.33f, 0.49f, 0.31f),
            BiomeKindV3.RockyHills => new Color(0.41f, 0.46f, 0.34f),
            BiomeKindV3.Dryland => new Color(0.49f, 0.48f, 0.31f),
            BiomeKindV3.Wasteland => new Color(0.39f, 0.38f, 0.32f),
            _ => new Color(0.37f, 0.53f, 0.31f)
        };
    }

    private static Color GetOverlayColor(CellData cell, WorldV2OverlayMode overlayMode)
    {
        return overlayMode switch
        {
            WorldV2OverlayMode.RoadNetwork => cell.IsBridgeCandidate
                ? new Color(0.91f, 0.80f, 0.47f)
                : cell.IsRoad
                    ? new Color(0.76f, 0.60f, 0.33f)
                    : cell.IsRiver
                        ? new Color(0.16f, 0.35f, 0.43f)
                        : cell.ForestStrength > 0.45f
                            ? new Color(0.16f, 0.30f, 0.20f)
                            : new Color(0.32f, 0.43f, 0.28f),
            WorldV2OverlayMode.ForestCost => new Color(0.18f, Mathf.Lerp(0.34f, 0.88f, cell.ForestStrength), 0.22f),
            WorldV2OverlayMode.RiverBridge => cell.IsBridgeCandidate
                ? new Color(0.91f, 0.82f, 0.49f)
                : cell.IsRiver
                    ? new Color(0.16f, 0.54f, 0.66f)
                    : cell.IsRiverBank
                        ? new Color(0.58f, 0.49f, 0.31f)
                        : new Color(0.35f, 0.46f, 0.31f),
            WorldV2OverlayMode.Sites => cell.IsStartingVillage
                ? new Color(0.86f, 0.74f, 0.45f)
                : cell.IsVillage
                    ? new Color(0.73f, 0.62f, 0.40f)
                    : cell.LandmarkKind switch
                    {
                        LandmarkKindV2.Ruin => new Color(0.55f, 0.51f, 0.45f),
                        LandmarkKindV2.Dungeon => new Color(0.35f, 0.27f, 0.43f),
                        LandmarkKindV2.BanditCamp => new Color(0.50f, 0.25f, 0.21f),
                        LandmarkKindV2.FactionOutpost => new Color(0.33f, 0.43f, 0.54f),
                        LandmarkKindV2.Quarry => new Color(0.62f, 0.60f, 0.50f),
                        _ => cell.IsRoad ? new Color(0.66f, 0.52f, 0.30f) : new Color(0.34f, 0.45f, 0.30f)
                    },
            WorldV2OverlayMode.BuildRestricted => cell.IsBuildRestricted
                ? new Color(0.86f, 0.53f, 0.25f)
                : new Color(0.30f, 0.44f, 0.29f),
            _ => GetTileColor(cell)
        };
    }

    public static Color GetResourceColor(WorldResourceTypeV2 type)
    {
        return type switch
        {
            WorldResourceTypeV2.Wood => new Color(0.24f, 0.55f, 0.22f),
            WorldResourceTypeV2.Stone => new Color(0.68f, 0.68f, 0.64f),
            WorldResourceTypeV2.IronOre => new Color(0.55f, 0.42f, 0.36f),
            WorldResourceTypeV2.Coal => new Color(0.10f, 0.11f, 0.12f),
            WorldResourceTypeV2.Herb => new Color(0.30f, 0.82f, 0.36f),
            WorldResourceTypeV2.Food => new Color(0.92f, 0.58f, 0.22f),
            WorldResourceTypeV2.Scrap => new Color(0.58f, 0.62f, 0.65f),
            _ => Colors.Transparent
        };
    }
}
