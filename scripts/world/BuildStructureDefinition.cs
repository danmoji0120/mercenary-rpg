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
    public static IReadOnlyList<BuildMaterialType> GetMaterialOptions(TileBuildType buildType)
    {
        return BuildDefinitionDatabase.GetMaterialOptions(buildType);
    }

    public static bool IsMaterialSensitiveBuildType(TileBuildType buildType)
    {
        return BuildDefinitionDatabase.IsMaterialSensitiveBuildType(buildType);
    }

    public static bool CanUseMaterial(TileBuildType buildType, BuildMaterialType materialType)
    {
        return BuildDefinitionDatabase.CanUseMaterial(buildType, materialType);
    }

    public static BuildMaterialType NormalizeMaterial(TileBuildType buildType, BuildMaterialType materialType)
    {
        return BuildDefinitionDatabase.NormalizeMaterial(buildType, materialType);
    }

    public static BuildStructureDefinition Get(TileBuildType buildType, BuildMaterialType materialType)
    {
        if (BuildDefinitionDatabase.IsMaterialSensitiveBuildType(buildType))
        {
            return BuildDefinitionDatabase.Get(buildType, materialType);
        }

        return new BuildStructureDefinition(
            buildType,
            BuildMaterialType.Basic,
            BaseBuildManager.GetBuildDisplayName(buildType),
            new Dictionary<BaseResourceType, int>(),
            0,
            1.0f,
            0.0f,
            0.0f,
            1.0f,
            "");
    }

    public static string GetMaterialDisplayName(BuildMaterialType materialType)
    {
        return BuildDefinitionDatabase.GetMaterialDisplayName(materialType);
    }
}
