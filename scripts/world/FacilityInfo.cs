using Godot;

public readonly struct FacilityInfo
{
    public FacilityInfo(
        Vector2I cell,
        Vector2 worldPosition,
        FacilityType facilityType,
        TileBuildType objectType,
        bool isUsable = true,
        bool isReserved = false,
        bool isOccupied = false)
    {
        Cell = cell;
        WorldPosition = worldPosition;
        FacilityType = facilityType;
        ObjectType = objectType;
        IsUsable = isUsable;
        IsReserved = isReserved;
        IsOccupied = isOccupied;
    }

    public Vector2I Cell { get; }
    public Vector2 WorldPosition { get; }
    public FacilityType FacilityType { get; }
    public TileBuildType ObjectType { get; }
    public bool IsUsable { get; }
    public bool IsReserved { get; }
    public bool IsOccupied { get; }
}
