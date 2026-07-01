using Godot;

public readonly struct BuildableTileState
{
    public BuildableTileState(Vector2I cell, TileBuildType floorType, TileBuildType objectType, bool isOpen = true)
        : this(cell, floorType, objectType, cell, Vector2I.One, isOpen)
    {
    }

    public BuildableTileState(
        Vector2I cell,
        TileBuildType floorType,
        TileBuildType objectType,
        Vector2I objectOriginCell,
        Vector2I objectSize,
        bool isOpen = true)
        : this(
            cell,
            floorType,
            objectType,
            objectOriginCell,
            objectSize,
            isOpen,
            BuildMaterialType.Basic,
            BuildMaterialType.Basic,
            0,
            0,
            0,
            0,
            0.0f,
            0.0f)
    {
    }

    public BuildableTileState(
        Vector2I cell,
        TileBuildType floorType,
        TileBuildType objectType,
        Vector2I objectOriginCell,
        Vector2I objectSize,
        bool isOpen,
        BuildMaterialType floorMaterialType,
        BuildMaterialType objectMaterialType,
        int floorDurability,
        int maxFloorDurability,
        int objectDurability,
        int maxObjectDurability,
        float floorRoomQualityBonus,
        float objectRoomQualityBonus)
    {
        Cell = cell;
        FloorType = floorType;
        ObjectType = objectType;
        ObjectOriginCell = objectOriginCell;
        ObjectSize = objectSize;
        IsOpen = isOpen;
        FloorMaterialType = floorMaterialType;
        ObjectMaterialType = objectMaterialType;
        FloorDurability = floorDurability;
        MaxFloorDurability = maxFloorDurability;
        ObjectDurability = objectDurability;
        MaxObjectDurability = maxObjectDurability;
        FloorRoomQualityBonus = floorRoomQualityBonus;
        ObjectRoomQualityBonus = objectRoomQualityBonus;
    }

    public Vector2I Cell { get; }
    public TileBuildType FloorType { get; }
    public TileBuildType ObjectType { get; }
    public Vector2I ObjectOriginCell { get; }
    public Vector2I ObjectSize { get; }
    public bool IsOpen { get; }
    public BuildMaterialType FloorMaterialType { get; }
    public BuildMaterialType ObjectMaterialType { get; }
    public int FloorDurability { get; }
    public int MaxFloorDurability { get; }
    public int ObjectDurability { get; }
    public int MaxObjectDurability { get; }
    public float FloorRoomQualityBonus { get; }
    public float ObjectRoomQualityBonus { get; }
    public bool HasFloor => FloorType == TileBuildType.Floor;
    public bool HasObject => ObjectType != TileBuildType.None;
    public bool IsObjectOrigin => HasObject && Cell == ObjectOriginCell;
    public bool IsWall => ObjectType == TileBuildType.Wall;
    public bool IsDoor => ObjectType == TileBuildType.Door;
    public bool IsFacility => ObjectType == TileBuildType.Bed
        || ObjectType == TileBuildType.ImprovisedBed
        || ObjectType == TileBuildType.LuxuryBed
        || ObjectType == TileBuildType.Storage
        || ObjectType == TileBuildType.SmallChest
        || ObjectType == TileBuildType.LargeStorage
        || ObjectType == TileBuildType.GuardPost;
    public bool BlocksMovement => IsWall || (IsDoor && !IsOpen);
    public TileBuildType BuildType => HasObject ? ObjectType : FloorType;

    public bool IsEmpty()
    {
        return !HasFloor && !HasObject;
    }

    public BuildableTileState WithFloor(TileBuildType floorType)
    {
        return WithFloor(floorType, BuildMaterialType.Basic, 0, 0.0f);
    }

    public BuildableTileState WithFloor(TileBuildType floorType, BuildMaterialType materialType, int durability, float roomQualityBonus)
    {
        BuildMaterialType nextMaterialType = floorType == TileBuildType.None ? BuildMaterialType.Basic : materialType;
        int nextDurability = floorType == TileBuildType.None ? 0 : durability;
        float nextRoomQualityBonus = floorType == TileBuildType.None ? 0.0f : roomQualityBonus;
        return new BuildableTileState(
            Cell,
            floorType,
            ObjectType,
            ObjectOriginCell,
            ObjectSize,
            IsOpen,
            nextMaterialType,
            ObjectMaterialType,
            nextDurability,
            nextDurability,
            ObjectDurability,
            MaxObjectDurability,
            nextRoomQualityBonus,
            ObjectRoomQualityBonus);
    }

    public BuildableTileState WithObject(TileBuildType objectType, bool isOpen = true)
    {
        return WithObject(objectType, ObjectOriginCell, ObjectSize, isOpen, BuildMaterialType.Basic, 0, 0.0f);
    }

    public BuildableTileState WithObject(TileBuildType objectType, Vector2I objectOriginCell, Vector2I objectSize, bool isOpen = true)
    {
        return WithObject(objectType, objectOriginCell, objectSize, isOpen, BuildMaterialType.Basic, 0, 0.0f);
    }

    public BuildableTileState WithObject(
        TileBuildType objectType,
        Vector2I objectOriginCell,
        Vector2I objectSize,
        bool isOpen,
        BuildMaterialType materialType,
        int durability,
        float roomQualityBonus)
    {
        bool hasObject = objectType != TileBuildType.None;
        return new BuildableTileState(
            Cell,
            FloorType,
            objectType,
            hasObject ? objectOriginCell : Cell,
            hasObject ? objectSize : Vector2I.One,
            isOpen,
            FloorMaterialType,
            hasObject ? materialType : BuildMaterialType.Basic,
            FloorDurability,
            MaxFloorDurability,
            hasObject ? durability : 0,
            hasObject ? durability : 0,
            FloorRoomQualityBonus,
            hasObject ? roomQualityBonus : 0.0f);
    }
}
