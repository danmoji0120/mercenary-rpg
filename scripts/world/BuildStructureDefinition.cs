using System.Collections.Generic;
using Godot;

public readonly struct BuildStructureDefinition
{
    public BuildStructureDefinition(
        TileBuildType buildType,
        BuildMaterialType materialType,
        string displayName,
        IReadOnlyDictionary<BaseResourceType, int> cost,
        int durability,
        float requiredWorkMultiplier,
        float roomQualityBonus,
        float hygieneModifier,
        float moveSpeedMultiplier,
        string description,
        bool isAvailable = true)
    {
        BuildType = buildType;
        MaterialType = materialType;
        DisplayName = displayName;
        Cost = cost;
        Durability = durability;
        RequiredWorkMultiplier = requiredWorkMultiplier;
        RoomQualityBonus = roomQualityBonus;
        HygieneModifier = hygieneModifier;
        MoveSpeedMultiplier = moveSpeedMultiplier;
        Description = description;
        IsAvailable = isAvailable;
    }

    public TileBuildType BuildType { get; }
    public BuildMaterialType MaterialType { get; }
    public string DisplayName { get; }
    public IReadOnlyDictionary<BaseResourceType, int> Cost { get; }
    public int Durability { get; }
    public float RequiredWorkMultiplier { get; }
    public float RoomQualityBonus { get; }
    public float HygieneModifier { get; }
    public float MoveSpeedMultiplier { get; }
    public string Description { get; }
    public bool IsAvailable { get; }
}

public static class BuildStructureDefinitions
{
    private static readonly BuildMaterialType[] MaterialOrder =
    {
        BuildMaterialType.Wood,
        BuildMaterialType.Stone,
        BuildMaterialType.Metal
    };

    public static IReadOnlyList<BuildMaterialType> GetMaterialOptions(TileBuildType buildType)
    {
        return IsMaterialSensitiveBuildType(buildType) ? MaterialOrder : System.Array.Empty<BuildMaterialType>();
    }

    public static bool IsMaterialSensitiveBuildType(TileBuildType buildType)
    {
        return buildType == TileBuildType.Floor
            || buildType == TileBuildType.Wall
            || buildType == TileBuildType.Door;
    }

    public static bool CanUseMaterial(TileBuildType buildType, BuildMaterialType materialType)
    {
        if (!IsMaterialSensitiveBuildType(buildType))
        {
            return false;
        }

        if (buildType == TileBuildType.Door && materialType == BuildMaterialType.Stone)
        {
            return false;
        }

        return materialType == BuildMaterialType.Wood
            || materialType == BuildMaterialType.Stone
            || materialType == BuildMaterialType.Metal;
    }

    public static BuildMaterialType NormalizeMaterial(TileBuildType buildType, BuildMaterialType materialType)
    {
        if (CanUseMaterial(buildType, materialType))
        {
            return materialType;
        }

        return BuildMaterialType.Wood;
    }

    public static BuildStructureDefinition Get(TileBuildType buildType, BuildMaterialType materialType)
    {
        BuildMaterialType normalizedMaterial = NormalizeMaterial(buildType, materialType);

        return buildType switch
        {
            TileBuildType.Floor => GetFloorDefinition(normalizedMaterial),
            TileBuildType.Wall => GetWallDefinition(normalizedMaterial),
            TileBuildType.Door => GetDoorDefinition(normalizedMaterial),
            _ => new BuildStructureDefinition(
                buildType,
                BuildMaterialType.Basic,
                BaseBuildManager.GetBuildDisplayName(buildType),
                new Dictionary<BaseResourceType, int>(),
                0,
                1.0f,
                0.0f,
                0.0f,
                1.0f,
                "")
        };
    }

    public static string GetMaterialDisplayName(BuildMaterialType materialType)
    {
        return materialType switch
        {
            BuildMaterialType.Wood => "\uB098\uBB34",
            BuildMaterialType.Stone => "\uB3CC",
            BuildMaterialType.Metal => "\uAE08\uC18D",
            _ => "\uAE30\uBCF8"
        };
    }

    private static BuildStructureDefinition GetFloorDefinition(BuildMaterialType materialType)
    {
        return materialType switch
        {
            BuildMaterialType.Stone => new BuildStructureDefinition(
                TileBuildType.Floor,
                BuildMaterialType.Stone,
                "\uB3CC \uBC14\uB2E5",
                Cost((BaseResourceType.Stone, 1)),
                80,
                1.2f,
                0.3f,
                0.2f,
                0.95f,
                "\uB2E8\uB2E8\uD558\uACE0 \uC704\uC0DD\uC801\uC778 \uB3CC \uBC14\uB2E5\uC785\uB2C8\uB2E4."),
            BuildMaterialType.Metal => new BuildStructureDefinition(
                TileBuildType.Floor,
                BuildMaterialType.Metal,
                "\uCCA0\uD310 \uBC14\uB2E5",
                Cost((BaseResourceType.Metal, 1)),
                120,
                1.5f,
                0.1f,
                0.0f,
                0.9f,
                "\uB0B4\uAD6C\uB3C4\uAC00 \uB192\uC9C0\uB9CC \uCC28\uAC11\uACE0 \uBB34\uAC70\uC6B4 \uCCA0\uD310 \uBC14\uB2E5\uC785\uB2C8\uB2E4."),
            _ => new BuildStructureDefinition(
                TileBuildType.Floor,
                BuildMaterialType.Wood,
                "\uB098\uBB34 \uBC14\uB2E5",
                Cost((BaseResourceType.Wood, 1)),
                40,
                1.0f,
                0.2f,
                0.0f,
                1.0f,
                "\uB530\uB73B\uD558\uACE0 \uBB34\uB09C\uD55C \uB098\uBB34 \uBC14\uB2E5\uC785\uB2C8\uB2E4.")
        };
    }

    private static BuildStructureDefinition GetWallDefinition(BuildMaterialType materialType)
    {
        return materialType switch
        {
            BuildMaterialType.Stone => new BuildStructureDefinition(
                TileBuildType.Wall,
                BuildMaterialType.Stone,
                "\uB3CC \uBCBD",
                Cost((BaseResourceType.Stone, 2)),
                140,
                1.4f,
                0.3f,
                0.0f,
                1.0f,
                "\uB0B4\uAD6C\uB3C4\uAC00 \uB192\uC740 \uB3CC \uBCBD\uC785\uB2C8\uB2E4."),
            BuildMaterialType.Metal => new BuildStructureDefinition(
                TileBuildType.Wall,
                BuildMaterialType.Metal,
                "\uCCA0\uBCBD",
                Cost((BaseResourceType.Metal, 2)),
                220,
                1.8f,
                0.2f,
                0.0f,
                1.0f,
                "\uAC00\uC7A5 \uB2E8\uB2E8\uD55C \uAE08\uC18D \uBCBD\uC785\uB2C8\uB2E4."),
            _ => new BuildStructureDefinition(
                TileBuildType.Wall,
                BuildMaterialType.Wood,
                "\uB098\uBB34 \uBCBD",
                Cost((BaseResourceType.Wood, 2)),
                60,
                1.0f,
                0.1f,
                0.0f,
                1.0f,
                "\uBE60\uB974\uAC8C \uC138\uC6B8 \uC218 \uC788\uB294 \uB098\uBB34 \uBCBD\uC785\uB2C8\uB2E4.")
        };
    }

    private static BuildStructureDefinition GetDoorDefinition(BuildMaterialType materialType)
    {
        return materialType == BuildMaterialType.Metal
            ? new BuildStructureDefinition(
                TileBuildType.Door,
                BuildMaterialType.Metal,
                "\uBCF4\uAC15 \uBB38",
                Cost((BaseResourceType.Wood, 2), (BaseResourceType.Metal, 1)),
                130,
                1.4f,
                0.0f,
                0.0f,
                1.0f,
                "\uAE08\uC18D\uC73C\uB85C \uBCF4\uAC15\uD55C \uD2BC\uD2BC\uD55C \uBB38\uC785\uB2C8\uB2E4.")
            : new BuildStructureDefinition(
                TileBuildType.Door,
                BuildMaterialType.Wood,
                "\uB098\uBB34 \uBB38",
                Cost((BaseResourceType.Wood, 3)),
                50,
                1.0f,
                0.0f,
                0.0f,
                1.0f,
                "\uAE30\uBCF8\uC801\uC778 \uB098\uBB34 \uBB38\uC785\uB2C8\uB2E4.");
    }

    private static IReadOnlyDictionary<BaseResourceType, int> Cost(params (BaseResourceType ResourceType, int Amount)[] entries)
    {
        Dictionary<BaseResourceType, int> cost = new();

        foreach ((BaseResourceType resourceType, int amount) in entries)
        {
            if (amount > 0)
            {
                cost[resourceType] = amount;
            }
        }

        return cost;
    }
}
