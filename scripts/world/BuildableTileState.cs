using Godot;

public readonly struct BuildableTileState
{
    public BuildableTileState(Vector2I cell, TileBuildType floorType, TileBuildType objectType, bool isOpen = true)
    {
        Cell = cell;
        FloorType = floorType;
        ObjectType = objectType;
        IsOpen = isOpen;
    }

    public Vector2I Cell { get; }
    public TileBuildType FloorType { get; }
    public TileBuildType ObjectType { get; }
    public bool IsOpen { get; }
    public bool HasFloor => FloorType == TileBuildType.Floor;
    public bool HasObject => ObjectType != TileBuildType.None;
    public bool IsWall => ObjectType == TileBuildType.Wall;
    public bool IsDoor => ObjectType == TileBuildType.Door;
    public bool IsFacility => ObjectType == TileBuildType.Bed || ObjectType == TileBuildType.Storage || ObjectType == TileBuildType.GuardPost;
    public bool BlocksMovement => IsWall || (IsDoor && !IsOpen);
    public TileBuildType BuildType => HasObject ? ObjectType : FloorType;

    public bool IsEmpty()
    {
        return !HasFloor && !HasObject;
    }

    public BuildableTileState WithFloor(TileBuildType floorType)
    {
        return new BuildableTileState(Cell, floorType, ObjectType, IsOpen);
    }

    public BuildableTileState WithObject(TileBuildType objectType, bool isOpen = true)
    {
        return new BuildableTileState(Cell, FloorType, objectType, isOpen);
    }
}
