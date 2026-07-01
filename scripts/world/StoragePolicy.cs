using System.Collections.Generic;

public sealed class StoragePolicy
{
    public StoragePolicy(StoragePolicyPreset preset = StoragePolicyPreset.All, StoragePriority priority = StoragePriority.Normal)
    {
        Priority = priority;
        Preset = preset;
        ApplyPreset(preset);
    }

    public StoragePriority Priority { get; set; }
    public StoragePolicyPreset Preset { get; private set; }
    public bool UseDetailedFilter { get; set; } = true;
    public string PolicyName { get; set; } = "";
    public Dictionary<BaseResourceType, bool> AllowedResources { get; } = new();

    public bool UserAllows(BaseResourceType type)
    {
        EnsureAllResources();
        return AllowedResources.TryGetValue(type, out bool allowed) && allowed;
    }

    public void SetAllowed(BaseResourceType type, bool allowed)
    {
        EnsureAllResources();
        Preset = StoragePolicyPreset.None;
        AllowedResources[type] = allowed;
    }

    public void SetCategoryAllowed(StorageResourceCategory category, bool allowed)
    {
        EnsureAllResources();
        Preset = StoragePolicyPreset.None;

        foreach (BaseResourceType resourceType in StoragePolicyHelpers.GetResourcesInCategory(category))
        {
            AllowedResources[resourceType] = allowed;
        }
    }

    public bool IsCategoryFullyAllowed(StorageResourceCategory category)
    {
        EnsureAllResources();

        foreach (BaseResourceType resourceType in StoragePolicyHelpers.GetResourcesInCategory(category))
        {
            if (!UserAllows(resourceType))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsCategoryPartiallyAllowed(StorageResourceCategory category)
    {
        EnsureAllResources();
        bool hasAllowed = false;
        bool hasBlocked = false;

        foreach (BaseResourceType resourceType in StoragePolicyHelpers.GetResourcesInCategory(category))
        {
            if (UserAllows(resourceType))
            {
                hasAllowed = true;
            }
            else
            {
                hasBlocked = true;
            }
        }

        return hasAllowed && hasBlocked;
    }

    public void ApplyPreset(StoragePolicyPreset preset)
    {
        Preset = preset;
        EnsureAllResources();

        foreach (BaseResourceType resourceType in BaseBuildManager.GetAllResourceTypes())
        {
            AllowedResources[resourceType] = StoragePolicyHelpers.PresetAllows(preset, resourceType);
        }
    }

    public void EnsureAllResources()
    {
        foreach (BaseResourceType resourceType in BaseBuildManager.GetAllResourceTypes())
        {
            AllowedResources.TryAdd(resourceType, false);
        }
    }
}

public static class StoragePolicyHelpers
{
    public static StorageResourceCategory GetResourceCategory(BaseResourceType type)
    {
        return ResourceDefinitionDatabase.GetCategory(type);
    }

    public static IReadOnlyList<BaseResourceType> GetResourcesInCategory(StorageResourceCategory category)
    {
        return ResourceDefinitionDatabase.GetResourcesInCategory(category);
    }

    public static bool PresetAllows(StoragePolicyPreset preset, BaseResourceType type)
    {
        StorageResourceCategory category = GetResourceCategory(type);

        return preset switch
        {
            StoragePolicyPreset.All => true,
            StoragePolicyPreset.None => false,
            StoragePolicyPreset.Food => type == BaseResourceType.Food,
            StoragePolicyPreset.Meals => type == BaseResourceType.SimpleMeal,
            StoragePolicyPreset.FoodAndMeals => type == BaseResourceType.Food || type == BaseResourceType.SimpleMeal,
            StoragePolicyPreset.RawMaterials => category == StorageResourceCategory.RawMaterial,
            StoragePolicyPreset.ProcessedMaterials => category == StorageResourceCategory.ProcessedMaterial,
            StoragePolicyPreset.ConstructionMaterials => category == StorageResourceCategory.RawMaterial || category == StorageResourceCategory.ProcessedMaterial,
            StoragePolicyPreset.Medical => type == BaseResourceType.Medicine,
            _ => false
        };
    }

    public static string GetPriorityDisplayName(StoragePriority priority)
    {
        return priority switch
        {
            StoragePriority.Low => "\uB0AE\uC74C",
            StoragePriority.Preferred => "\uC120\uD638",
            StoragePriority.Important => "\uC911\uC694",
            StoragePriority.Critical => "\uCD5C\uC6B0\uC120",
            _ => "\uBCF4\uD1B5"
        };
    }

    public static string GetPresetDisplayName(StoragePolicyPreset preset)
    {
        return preset switch
        {
            StoragePolicyPreset.None => "\uAE08\uC9C0",
            StoragePolicyPreset.Food => "\uC2DD\uB7C9",
            StoragePolicyPreset.Meals => "\uC2DD\uC0AC",
            StoragePolicyPreset.FoodAndMeals => "\uC2DD\uB7C9+\uC2DD\uC0AC",
            StoragePolicyPreset.RawMaterials => "\uC6D0\uC790\uC7AC",
            StoragePolicyPreset.ProcessedMaterials => "\uAC00\uACF5\uC7AC",
            StoragePolicyPreset.ConstructionMaterials => "\uAC74\uC124\uC7AC",
            StoragePolicyPreset.Medical => "\uC758\uB8CC",
            _ => "\uC804\uCCB4"
        };
    }

    public static string GetCategoryDisplayName(StorageResourceCategory category)
    {
        return category switch
        {
            StorageResourceCategory.Food => "\uC2DD\uB7C9",
            StorageResourceCategory.Meal => "\uC2DD\uC0AC",
            StorageResourceCategory.RawMaterial => "\uC6D0\uC790\uC7AC",
            StorageResourceCategory.ProcessedMaterial => "\uAC00\uACF5\uC7AC",
            StorageResourceCategory.Medical => "\uC758\uB8CC",
            _ => "\uAE30\uD0C0"
        };
    }
}
