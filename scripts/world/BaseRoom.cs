using System.Collections.Generic;
using Godot;

public sealed class BaseRoom
{
    public BaseRoom(int roomId, RoomType roomType, Rect2I bounds, List<Vector2I> cells)
    {
        RoomId = roomId;
        RoomType = roomType;
        Bounds = bounds;
        Cells = cells;
        DisplayName = BaseRoomManager.GetRoomDisplayName(roomType);
    }

    public int RoomId { get; }
    public RoomType RoomType { get; }
    public Rect2I Bounds { get; }
    public IReadOnlyList<Vector2I> Cells { get; }
    public string DisplayName { get; }
    public int QualityScore { get; set; }
    public bool IsValid { get; set; }
    public List<string> MissingRequirements { get; } = new();
    public Dictionary<FurnitureTag, int> FurnitureCounts { get; } = new();
}

public enum FurnitureTag
{
    Bed,
    Cabinet,
    Desk,
    Lamp,
    Table,
    Chair,
    ServingCounter,
    Kitchen,
    IngredientStorage,
    Storage,
    LargeStorage,
    Shelf,
    Workbench,
    AdvancedWorkshop,
    MedicalBed,
    MedicalSupport,
    Training,
    Decor
}
