using System;
using System.Collections.Generic;
using Godot;

public partial class BaseRoomManager : Node2D
{
    private const float RoomFillAlpha = 0.16f;
    private const float RoomBorderAlpha = 0.52f;

    private readonly List<BaseRoom> _rooms = new();
    private readonly HashSet<Vector2I> _previewCells = new();
    private BaseBuildManager? _buildManager;
    private RoomType _previewRoomType = RoomType.None;
    private bool _previewRemoveMode;
    private int _nextRoomId = 1;

    public IReadOnlyList<BaseRoom> Rooms => _rooms;

    public override void _Ready()
    {
        ZIndex = 20;
        Visible = true;
    }

    public override void _Draw()
    {
        if (_buildManager == null)
        {
            return;
        }

        int tileSize = _buildManager.TileSize;
        Font font = ThemeDB.FallbackFont;

        foreach (BaseRoom room in _rooms)
        {
            Color color = GetRoomColor(room.RoomType);

            foreach (Vector2I cell in room.Cells)
            {
                Rect2 cellRect = new(
                    new Vector2(cell.X * tileSize, cell.Y * tileSize),
                    new Vector2(tileSize, tileSize));
                DrawRect(cellRect.Grow(-1.0f), new Color(color.R, color.G, color.B, RoomFillAlpha), true);
                DrawRect(cellRect.Grow(-1.0f), new Color(color.R, color.G, color.B, RoomBorderAlpha * 0.55f), false, 1.0f);
            }

            string stateLabel = room.IsValid
                ? $"{room.DisplayName} \u2605{room.QualityScore}"
                : $"{room.DisplayName} \uBBF8\uC644\uC131";
            DrawString(font, GetRoomLabelPosition(room, tileSize), stateLabel, HorizontalAlignment.Left, -1.0f, 13, Colors.White);
        }

        if (_previewCells.Count > 0)
        {
            Color previewColor = _previewRemoveMode ? new Color(1.0f, 0.24f, 0.16f) : GetRoomColor(_previewRoomType);

            foreach (Vector2I cell in _previewCells)
            {
                Rect2 cellRect = new(
                    new Vector2(cell.X * tileSize, cell.Y * tileSize),
                    new Vector2(tileSize, tileSize));
                DrawRect(cellRect.Grow(-2.0f), new Color(previewColor.R, previewColor.G, previewColor.B, 0.24f), true);
                DrawRect(cellRect.Grow(-2.0f), new Color(previewColor.R, previewColor.G, previewColor.B, 0.78f), false, 1.5f);
            }
        }
    }

    public void SetBuildManager(BaseBuildManager? buildManager)
    {
        _buildManager = buildManager;
        RecalculateAllRooms();
        QueueRedraw();
    }

    public bool TryCreateRoom(RoomType roomType, Rect2I bounds, out BaseRoom? room)
    {
        List<Vector2I> cells = new();

        for (int y = bounds.Position.Y; y < bounds.Position.Y + bounds.Size.Y; y++)
        {
            for (int x = bounds.Position.X; x < bounds.Position.X + bounds.Size.X; x++)
            {
                cells.Add(new Vector2I(x, y));
            }
        }

        return TryCreateRoom(roomType, cells, out room);
    }

    public bool TryCreateRoom(RoomType roomType, IEnumerable<Vector2I> requestedCells, out BaseRoom? room)
    {
        room = null;

        if (_buildManager == null || roomType == RoomType.None)
        {
            return false;
        }

        HashSet<Vector2I> uniqueCells = new();

        foreach (Vector2I cell in requestedCells)
        {
            if (_buildManager.IsCellInWorld(cell))
            {
                uniqueCells.Add(cell);
            }
        }

        if (uniqueCells.Count <= 0)
        {
            return false;
        }

        List<Vector2I> cells = new(uniqueCells);
        Rect2I bounds = CreateBounds(cells);
        RemoveRoomsOverlapping(cells);

        room = new BaseRoom(_nextRoomId++, roomType, bounds, cells);
        EvaluateRoom(room);
        _rooms.Add(room);
        QueueRedraw();
        return true;
    }

    public int RemoveRoomsOverlapping(Rect2I bounds)
    {
        List<Vector2I> cells = new();

        for (int y = bounds.Position.Y; y < bounds.Position.Y + bounds.Size.Y; y++)
        {
            for (int x = bounds.Position.X; x < bounds.Position.X + bounds.Size.X; x++)
            {
                cells.Add(new Vector2I(x, y));
            }
        }

        return RemoveRoomsOverlapping(cells);
    }

    public int RemoveRoomsOverlapping(IEnumerable<Vector2I> cells)
    {
        int removedCount = 0;
        HashSet<Vector2I> cellSet = new(cells);

        for (int i = _rooms.Count - 1; i >= 0; i--)
        {
            if (RoomOverlapsCells(_rooms[i], cellSet))
            {
                _rooms.RemoveAt(i);
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            QueueRedraw();
        }

        return removedCount;
    }

    public BaseRoom? GetRoomAtCell(Vector2I cell)
    {
        for (int i = _rooms.Count - 1; i >= 0; i--)
        {
            BaseRoom room = _rooms[i];

            foreach (Vector2I roomCell in room.Cells)
            {
                if (roomCell == cell)
                {
                    return room;
                }
            }
        }

        return null;
    }

    public void SetPreviewCells(IEnumerable<Vector2I> cells, RoomType roomType, bool removeMode)
    {
        _previewCells.Clear();
        _previewRoomType = roomType;
        _previewRemoveMode = removeMode;

        if (_buildManager != null)
        {
            foreach (Vector2I cell in cells)
            {
                if (_buildManager.IsCellInWorld(cell))
                {
                    _previewCells.Add(cell);
                }
            }
        }

        QueueRedraw();
    }

    public void ClearPreview()
    {
        if (_previewCells.Count == 0)
        {
            return;
        }

        _previewCells.Clear();
        _previewRoomType = RoomType.None;
        _previewRemoveMode = false;
        QueueRedraw();
    }

    public void RecalculateAllRooms()
    {
        foreach (BaseRoom room in _rooms)
        {
            EvaluateRoom(room);
        }

        QueueRedraw();
    }

    private void EvaluateRoom(BaseRoom room)
    {
        room.FurnitureCounts.Clear();
        room.MissingRequirements.Clear();
        room.QualityScore = 0;
        room.IsValid = false;

        if (_buildManager == null)
        {
            return;
        }

        HashSet<Vector2I> countedOrigins = new();

        foreach (Vector2I cell in room.Cells)
        {
            if (!_buildManager.TryGetObjectInfoAtCell(cell, out TileBuildType objectType, out Vector2I originCell, out _))
            {
                continue;
            }

            if (!countedOrigins.Add(originCell))
            {
                continue;
            }

            foreach (FurnitureTag tag in GetFurnitureTags(objectType))
            {
                room.FurnitureCounts[tag] = GetCount(room, tag) + 1;
            }
        }

        EvaluateRequirements(room);
        ApplyMaterialQualityBonus(room);
    }

    private void ApplyMaterialQualityBonus(BaseRoom room)
    {
        if (_buildManager == null || !room.IsValid || room.Cells.Count == 0)
        {
            return;
        }

        float totalBonus = 0.0f;

        foreach (Vector2I cell in room.Cells)
        {
            totalBonus += _buildManager.GetRoomQualityBonusAtCell(cell);
        }

        float averageBonus = totalBonus / room.Cells.Count;

        if (averageBonus >= 0.25f)
        {
            room.QualityScore = Mathf.Clamp(room.QualityScore + 1, 1, 5);
        }
    }

    private static Rect2I CreateBounds(IReadOnlyCollection<Vector2I> cells)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (Vector2I cell in cells)
        {
            minX = Mathf.Min(minX, cell.X);
            minY = Mathf.Min(minY, cell.Y);
            maxX = Mathf.Max(maxX, cell.X);
            maxY = Mathf.Max(maxY, cell.Y);
        }

        return new Rect2I(new Vector2I(minX, minY), new Vector2I(maxX - minX + 1, maxY - minY + 1));
    }

    private static bool RoomOverlapsCells(BaseRoom room, HashSet<Vector2I> cells)
    {
        foreach (Vector2I cell in room.Cells)
        {
            if (cells.Contains(cell))
            {
                return true;
            }
        }

        return false;
    }

    private static Vector2 GetRoomLabelPosition(BaseRoom room, int tileSize)
    {
        if (room.Cells.Count == 0)
        {
            return new Vector2(room.Bounds.Position.X * tileSize + 6.0f, room.Bounds.Position.Y * tileSize + 16.0f);
        }

        Vector2 sum = Vector2.Zero;

        foreach (Vector2I cell in room.Cells)
        {
            sum += new Vector2((cell.X + 0.5f) * tileSize, (cell.Y + 0.5f) * tileSize);
        }

        Vector2 center = sum / room.Cells.Count;
        return center + new Vector2(-24.0f, 5.0f);
    }

    private static void EvaluateRequirements(BaseRoom room)
    {
        int bedCount = GetCount(room, FurnitureTag.Bed);
        int tableCount = GetCount(room, FurnitureTag.Table);
        int chairCount = GetCount(room, FurnitureTag.Chair);
        int storageCount = GetCount(room, FurnitureTag.Storage);
        int shelfCount = GetCount(room, FurnitureTag.Shelf);
        int workbenchCount = GetCount(room, FurnitureTag.Workbench);
        int advancedWorkshopCount = GetCount(room, FurnitureTag.AdvancedWorkshop);
        int medicalBedCount = GetCount(room, FurnitureTag.MedicalBed);
        int medicalSupportCount = GetCount(room, FurnitureTag.MedicalSupport);
        int trainingCount = GetCount(room, FurnitureTag.Training);
        int decorCount = GetCount(room, FurnitureTag.Decor);

        switch (room.RoomType)
        {
            case RoomType.PrivateRoom:
                Require(room, bedCount >= 1, "bed");
                room.QualityScore = 1 + GetCount(room, FurnitureTag.Cabinet) + GetCount(room, FurnitureTag.Desk) + GetCount(room, FurnitureTag.Lamp);
                break;
            case RoomType.Dormitory:
                Require(room, bedCount >= 2, "beds");
                room.QualityScore = Mathf.Max(1, bedCount);
                break;
            case RoomType.DiningRoom:
                Require(room, tableCount >= 1, "table");
                Require(room, chairCount >= 1, "chair");
                room.QualityScore = 1 + chairCount + GetCount(room, FurnitureTag.ServingCounter);
                break;
            case RoomType.Kitchen:
                Require(room, GetCount(room, FurnitureTag.Kitchen) >= 1, "kitchen");
                room.QualityScore = 1 + GetCount(room, FurnitureTag.IngredientStorage);
                break;
            case RoomType.StorageRoom:
                Require(room, storageCount + shelfCount >= 1, "storage");
                room.QualityScore = 1 + storageCount + shelfCount + GetCount(room, FurnitureTag.LargeStorage);
                break;
            case RoomType.Workshop:
                Require(room, workbenchCount >= 1, "workbench");
                room.QualityScore = 1 + workbenchCount + advancedWorkshopCount;
                break;
            case RoomType.Infirmary:
                Require(room, medicalBedCount >= 1, "medical bed");
                room.QualityScore = 1 + medicalBedCount + medicalSupportCount;
                break;
            case RoomType.TrainingRoom:
                Require(room, trainingCount >= 1, "training furniture");
                room.QualityScore = Mathf.Max(1, trainingCount);
                break;
            case RoomType.BathRoom:
                Require(room, decorCount >= 1, "bath decor");
                room.QualityScore = 1 + decorCount;
                break;
            case RoomType.Lounge:
                Require(room, chairCount + tableCount + decorCount >= 1, "lounge furniture");
                room.QualityScore = 1 + chairCount + tableCount + decorCount;
                break;
        }

        room.IsValid = room.MissingRequirements.Count == 0;
        room.QualityScore = room.IsValid ? Mathf.Clamp(room.QualityScore, 1, 5) : 0;
    }

    private static void Require(BaseRoom room, bool condition, string requirement)
    {
        if (!condition)
        {
            room.MissingRequirements.Add(requirement);
        }
    }

    private static int GetCount(BaseRoom room, FurnitureTag tag)
    {
        return room.FurnitureCounts.TryGetValue(tag, out int count) ? count : 0;
    }

    private static IEnumerable<FurnitureTag> GetFurnitureTags(TileBuildType buildType)
    {
        switch (buildType)
        {
            case TileBuildType.Bed:
            case TileBuildType.ImprovisedBed:
            case TileBuildType.LuxuryBed:
                yield return FurnitureTag.Bed;
                break;
            case TileBuildType.SmallCabinet:
                yield return FurnitureTag.Cabinet;
                break;
            case TileBuildType.SmallDesk:
                yield return FurnitureTag.Desk;
                yield return FurnitureTag.Table;
                break;
            case TileBuildType.Lamp:
                yield return FurnitureTag.Lamp;
                yield return FurnitureTag.Decor;
                break;
            case TileBuildType.Chair:
                yield return FurnitureTag.Chair;
                break;
            case TileBuildType.SmallDiningTable:
            case TileBuildType.LongDiningTable:
                yield return FurnitureTag.Table;
                break;
            case TileBuildType.ServingCounter:
                yield return FurnitureTag.ServingCounter;
                break;
            case TileBuildType.KitchenCounter:
            case TileBuildType.Hearth:
                yield return FurnitureTag.Kitchen;
                break;
            case TileBuildType.IngredientCrate:
                yield return FurnitureTag.IngredientStorage;
                break;
            case TileBuildType.SmallChest:
            case TileBuildType.Storage:
                yield return FurnitureTag.Storage;
                break;
            case TileBuildType.LargeStorage:
                yield return FurnitureTag.Storage;
                yield return FurnitureTag.LargeStorage;
                break;
            case TileBuildType.MaterialShelf:
            case TileBuildType.WeaponRack:
            case TileBuildType.MedicineShelf:
                yield return FurnitureTag.Shelf;
                break;
            case TileBuildType.Workbench:
            case TileBuildType.LargeWorkbench:
            case TileBuildType.RepairBench:
                yield return FurnitureTag.Workbench;
                break;
            case TileBuildType.Forge:
            case TileBuildType.AlchemyBench:
                yield return FurnitureTag.Workbench;
                yield return FurnitureTag.AdvancedWorkshop;
                break;
            case TileBuildType.ImprovisedMedicalBed:
            case TileBuildType.MedicalBed:
            case TileBuildType.LuxuryMedicalBed:
                yield return FurnitureTag.MedicalBed;
                break;
            case TileBuildType.MedicalTable:
            case TileBuildType.MedicineCabinet:
                yield return FurnitureTag.MedicalSupport;
                break;
            case TileBuildType.TrainingDummy:
            case TileBuildType.Sandbag:
            case TileBuildType.TrainingMat:
            case TileBuildType.WeaponTrainingRack:
            case TileBuildType.TrainingRing:
                yield return FurnitureTag.Training;
                break;
            case TileBuildType.PlantPot:
            case TileBuildType.SmallRug:
            case TileBuildType.LargeRug:
            case TileBuildType.WallBanner:
            case TileBuildType.TrophyDisplay:
                yield return FurnitureTag.Decor;
                break;
        }
    }

    public static string GetRoomDisplayName(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.PrivateRoom => "\uAC1C\uC778\uC2E4",
            RoomType.Dormitory => "\uACF5\uC6A9 \uCE68\uC2E4",
            RoomType.DiningRoom => "\uC2DD\uB2F9",
            RoomType.Kitchen => "\uC8FC\uBC29",
            RoomType.StorageRoom => "\uCC3D\uACE0",
            RoomType.Workshop => "\uC791\uC5C5\uC18C",
            RoomType.Infirmary => "\uC758\uBB34\uC2E4",
            RoomType.TrainingRoom => "\uD6C8\uB828\uC18C",
            RoomType.BathRoom => "\uBAA9\uC695\uD0D5",
            RoomType.Lounge => "\uD734\uAC8C\uC2E4",
            _ => "\uC120\uD0DD \uC5C6\uC74C"
        };
    }

    public static string GetRoomRequirementText(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.PrivateRoom => "\uCE68\uB300 1\uAC1C \uC774\uC0C1",
            RoomType.Dormitory => "\uCE68\uB300 2\uAC1C \uC774\uC0C1",
            RoomType.DiningRoom => "\uC2DD\uD0C1 1\uAC1C, \uC758\uC790 1\uAC1C \uC774\uC0C1",
            RoomType.Kitchen => "\uC870\uB9AC\uB300 \uB610\uB294 \uD654\uB355 1\uAC1C \uC774\uC0C1",
            RoomType.StorageRoom => "\uCC3D\uACE0/\uC0C1\uC790/\uC120\uBC18 1\uAC1C \uC774\uC0C1",
            RoomType.Workshop => "\uC791\uC5C5\uB300/\uD070 \uC791\uC5C5\uB300/\uC218\uB9AC\uB300 1\uAC1C \uC774\uC0C1",
            RoomType.Infirmary => "\uCE58\uB8CC \uCE68\uB300\uB958 1\uAC1C \uC774\uC0C1",
            RoomType.TrainingRoom => "\uD6C8\uB828 \uAC00\uAD6C 1\uAC1C \uC774\uC0C1",
            RoomType.BathRoom => "\uC7A5\uC2DD \uAC00\uAD6C 1\uAC1C \uC774\uC0C1",
            RoomType.Lounge => "\uC758\uC790/\uD0C1\uC790/\uC7A5\uC2DD 1\uAC1C \uC774\uC0C1",
            _ => "-"
        };
    }

    public static string GetRoomDescription(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.PrivateRoom => "\uC6A9\uBCD1 \uD55C \uBA85\uC774 \uC26C\uB294 \uAC1C\uC778 \uACF5\uAC04\uC785\uB2C8\uB2E4.",
            RoomType.Dormitory => "\uC5EC\uB7EC \uC6A9\uBCD1\uC774 \uD568\uAED8 \uC790\uB294 \uACF5\uC6A9 \uCE68\uC2E4\uC785\uB2C8\uB2E4.",
            RoomType.DiningRoom => "\uC6A9\uBCD1\uB4E4\uC774 \uC2DD\uC0AC\uD560 \uC218 \uC788\uB294 \uACF5\uAC04\uC785\uB2C8\uB2E4.",
            RoomType.Kitchen => "\uC74C\uC2DD\uC744 \uC900\uBE44\uD558\uB294 \uC8FC\uBC29 \uACF5\uAC04\uC785\uB2C8\uB2E4.",
            RoomType.StorageRoom => "\uC790\uC6D0\uACFC \uBB3C\uD488\uC744 \uBAA8\uC544 \uB450\uB294 \uBCF4\uAD00 \uACF5\uAC04\uC785\uB2C8\uB2E4.",
            RoomType.Workshop => "\uC81C\uC791\uACFC \uC218\uB9AC\uB97C \uC704\uD55C \uC791\uC5C5 \uACF5\uAC04\uC785\uB2C8\uB2E4.",
            RoomType.Infirmary => "\uBD80\uC0C1\uC790\uB97C \uB3CC\uBCF4\uB294 \uC758\uB8CC \uACF5\uAC04\uC785\uB2C8\uB2E4.",
            RoomType.TrainingRoom => "\uC804\uD22C \uD6C8\uB828\uC744 \uC704\uD55C \uACF5\uAC04\uC785\uB2C8\uB2E4.",
            RoomType.BathRoom => "\uBAA9\uC695\uD0D5 \uAC00\uAD6C\uAC00 \uC5C6\uB294 0.1\uC5D0\uC11C\uB294 \uC7A5\uC2DD \uBC29\uC73C\uB85C \uD310\uC815\uD569\uB2C8\uB2E4.",
            RoomType.Lounge => "\uC26C\uBA70 \uBA38\uBB34\uB97C \uC218 \uC788\uB294 \uD734\uAC8C \uACF5\uAC04\uC785\uB2C8\uB2E4.",
            _ => ""
        };
    }

    public static string GetRoomEffectText(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.PrivateRoom => "\uC218\uBA74 \uD68C\uBCF5 \uC18D\uB3C4, \uC2A4\uD2B8\uB808\uC2A4/\uAE30\uBD84 \uD68C\uBCF5 \uC18C\uD3ED \uBCF4\uB108\uC2A4",
            RoomType.Dormitory => "\uC218\uBA74 \uD68C\uBCF5 \uC18D\uB3C4 \uC18C\uD3ED \uBCF4\uB108\uC2A4",
            RoomType.DiningRoom => "\uC2DD\uC0AC \uC2DC \uD5C8\uAE30/\uAE30\uBD84/\uC2A4\uD2B8\uB808\uC2A4 \uD68C\uBCF5 \uBCF4\uB108\uC2A4",
            RoomType.Kitchen => "\uC2DD\uC0AC \uC2DC \uAE30\uBD84 \uC18C\uD3ED \uBCF4\uB108\uC2A4",
            RoomType.Lounge => "\uD734\uC2DD \uC2DC \uC2A4\uD2B8\uB808\uC2A4/\uAE30\uBD84 \uD68C\uBCF5 \uBCF4\uB108\uC2A4",
            RoomType.StorageRoom => "\uBCF4\uAD00 \uD6A8\uC728 \uBCF4\uB108\uC2A4\uB294 \uBCF4\uB958",
            RoomType.Workshop => "\uC81C\uC791/\uC218\uB9AC \uBCF4\uB108\uC2A4\uB294 \uBCF4\uB958",
            RoomType.Infirmary => "\uCE58\uB8CC/\uD68C\uBCF5 \uBCF4\uB108\uC2A4\uB294 \uBCF4\uB958",
            RoomType.TrainingRoom => "\uD6C8\uB828 \uACBD\uD5D8\uCE58 \uBCF4\uB108\uC2A4\uB294 \uBCF4\uB958",
            RoomType.BathRoom => "\uC704\uC0DD \uD68C\uBCF5 \uBCF4\uB108\uC2A4\uB294 \uBCF4\uB958",
            _ => "\uC5C6\uC74C"
        };
    }

    public static string GetFurnitureTagDisplayName(FurnitureTag tag)
    {
        return tag switch
        {
            FurnitureTag.Bed => "\uCE68\uB300",
            FurnitureTag.Cabinet => "\uC218\uB0A9\uC7A5",
            FurnitureTag.Desk => "\uCC45\uC0C1",
            FurnitureTag.Lamp => "\uB7A8\uD504",
            FurnitureTag.Table => "\uD0C1\uC790",
            FurnitureTag.Chair => "\uC758\uC790",
            FurnitureTag.ServingCounter => "\uBC30\uC2DD\uB300",
            FurnitureTag.Kitchen => "\uC8FC\uBC29 \uAC00\uAD6C",
            FurnitureTag.IngredientStorage => "\uC2DD\uC7AC\uB8CC \uBCF4\uAD00",
            FurnitureTag.Storage => "\uBCF4\uAD00 \uAC00\uAD6C",
            FurnitureTag.LargeStorage => "\uB300\uD615 \uCC3D\uACE0",
            FurnitureTag.Shelf => "\uC120\uBC18",
            FurnitureTag.Workbench => "\uC791\uC5C5\uB300",
            FurnitureTag.AdvancedWorkshop => "\uD2B9\uC218 \uC791\uC5C5\uB300",
            FurnitureTag.MedicalBed => "\uCE58\uB8CC \uCE68\uB300",
            FurnitureTag.MedicalSupport => "\uC758\uB8CC \uBCF4\uC870 \uAC00\uAD6C",
            FurnitureTag.Training => "\uD6C8\uB828 \uAC00\uAD6C",
            FurnitureTag.Decor => "\uC7A5\uC2DD",
            _ => tag.ToString()
        };
    }

    public static string GetRequirementDisplayName(string requirement)
    {
        return requirement switch
        {
            "bed" => "\uCE68\uB300",
            "beds" => "\uCE68\uB300 2\uAC1C",
            "table" => "\uC2DD\uD0C1",
            "chair" => "\uC758\uC790",
            "kitchen" => "\uC870\uB9AC\uB300/\uD654\uB355",
            "storage" => "\uBCF4\uAD00 \uAC00\uAD6C",
            "workbench" => "\uC791\uC5C5\uB300",
            "medical bed" => "\uCE58\uB8CC \uCE68\uB300",
            "training furniture" => "\uD6C8\uB828 \uAC00\uAD6C",
            "bath decor" => "\uBAA9\uC695/\uC7A5\uC2DD \uAC00\uAD6C",
            "lounge furniture" => "\uD734\uAC8C \uAC00\uAD6C",
            _ => requirement
        };
    }

    private static Color GetRoomColor(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.PrivateRoom => new Color(0.38f, 0.68f, 1.0f),
            RoomType.Dormitory => new Color(0.18f, 0.82f, 0.80f),
            RoomType.DiningRoom => new Color(1.0f, 0.72f, 0.18f),
            RoomType.Kitchen => new Color(0.92f, 0.45f, 0.16f),
            RoomType.StorageRoom => new Color(0.58f, 0.36f, 0.16f),
            RoomType.Workshop => new Color(0.58f, 0.58f, 0.62f),
            RoomType.Infirmary => new Color(0.76f, 1.0f, 0.78f),
            RoomType.TrainingRoom => new Color(0.92f, 0.22f, 0.18f),
            RoomType.BathRoom => new Color(0.42f, 0.78f, 1.0f),
            RoomType.Lounge => new Color(0.72f, 0.45f, 1.0f),
            _ => Colors.White
        };
    }

}
