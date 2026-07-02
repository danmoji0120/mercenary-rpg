using System.Collections.Generic;
using Godot;

public readonly struct StorageCapability
{
    public StorageCapability(
        string displayName,
        StoragePolicyPreset defaultPreset,
        StoragePriority defaultPriority,
        float capacityMultiplier,
        IReadOnlySet<BaseResourceType>? hardAllowedResources,
        string limitText)
    {
        DisplayName = displayName;
        DefaultPreset = defaultPreset;
        DefaultPriority = defaultPriority;
        CapacityMultiplier = capacityMultiplier;
        HardAllowedResources = hardAllowedResources;
        LimitText = limitText;
    }

    public string DisplayName { get; }
    public StoragePolicyPreset DefaultPreset { get; }
    public StoragePriority DefaultPriority { get; }
    public float CapacityMultiplier { get; }
    public IReadOnlySet<BaseResourceType>? HardAllowedResources { get; }
    public string LimitText { get; }

    public bool Allows(BaseResourceType type)
    {
        return HardAllowedResources == null || HardAllowedResources.Contains(type);
    }

    public bool IsSpecializedMatch(BaseResourceType type)
    {
        return HardAllowedResources != null && HardAllowedResources.Contains(type);
    }

    public static StorageCapability ForBuildType(TileBuildType buildType, bool isStarterSupply = false)
    {
        if (isStarterSupply)
        {
            return new StorageCapability(
                "\uCD08\uAE30 \uBCF4\uAE09 \uC0C1\uC790",
                StoragePolicyPreset.All,
                StoragePriority.Normal,
                1.0f,
                null,
                "\uBAA8\uB4E0 \uC790\uC6D0 \uBCF4\uAD00 \uAC00\uB2A5");
        }

        return buildType switch
        {
            TileBuildType.IngredientCrate => new StorageCapability(
                "\uC2DD\uC7AC\uB8CC \uC0C1\uC790",
                StoragePolicyPreset.FoodAndMeals,
                StoragePriority.Important,
                1.25f,
                Set(BaseResourceType.Food, BaseResourceType.SimpleMeal),
                "\uC2DD\uB7C9/\uC2DD\uC0AC\uB9CC \uBCF4\uAD00 \uAC00\uB2A5"),
            TileBuildType.MedicineShelf => new StorageCapability(
                "\uC57D\uD488 \uC120\uBC18",
                StoragePolicyPreset.Medical,
                StoragePriority.Important,
                1.5f,
                Set(BaseResourceType.Medicine, BaseResourceType.Herb),
                "\uC758\uB8CC\uC6A9\uD488\uB9CC \uBCF4\uAD00 \uAC00\uB2A5"),
            TileBuildType.MaterialShelf => new StorageCapability(
                "\uC790\uC7AC \uC120\uBC18",
                StoragePolicyPreset.ConstructionMaterials,
                StoragePriority.Preferred,
                1.25f,
                Set(BaseResourceType.Wood, BaseResourceType.Stone, BaseResourceType.Metal, BaseResourceType.IronOre, BaseResourceType.Coal, BaseResourceType.Plank, BaseResourceType.Brick, BaseResourceType.IronIngot),
                "\uAC74\uC124 \uC790\uC7AC\uB9CC \uBCF4\uAD00 \uAC00\uB2A5"),
            _ => new StorageCapability(
                BaseBuildManager.GetBuildDisplayName(buildType),
                StoragePolicyPreset.All,
                StoragePriority.Normal,
                1.0f,
                null,
                "\uBAA8\uB4E0 \uC790\uC6D0 \uBCF4\uAD00 \uAC00\uB2A5")
        };
    }

    private static IReadOnlySet<BaseResourceType> Set(params BaseResourceType[] resources)
    {
        return new HashSet<BaseResourceType>(resources);
    }
}
