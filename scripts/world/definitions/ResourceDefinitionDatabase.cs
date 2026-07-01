using System.Collections.Generic;

public static class ResourceDefinitionDatabase
{
    public static readonly IReadOnlyList<BaseResourceType> AllResourceTypes = new[]
    {
        BaseResourceType.Food,
        BaseResourceType.Wood,
        BaseResourceType.Stone,
        BaseResourceType.Metal,
        BaseResourceType.Plank,
        BaseResourceType.Brick,
        BaseResourceType.IronIngot,
        BaseResourceType.SimpleMeal,
        BaseResourceType.Medicine
    };

    public static readonly IReadOnlyList<BaseResourceType> BuildCostResourceOrder = new[]
    {
        BaseResourceType.Wood,
        BaseResourceType.Stone,
        BaseResourceType.Metal,
        BaseResourceType.Food
    };

    private static readonly IReadOnlyList<BaseResourceType> FoodResources = new[]
    {
        BaseResourceType.Food
    };

    private static readonly IReadOnlyList<BaseResourceType> MealResources = new[]
    {
        BaseResourceType.SimpleMeal
    };

    private static readonly IReadOnlyList<BaseResourceType> RawMaterialResources = new[]
    {
        BaseResourceType.Wood,
        BaseResourceType.Stone,
        BaseResourceType.Metal
    };

    private static readonly IReadOnlyList<BaseResourceType> ProcessedMaterialResources = new[]
    {
        BaseResourceType.Plank,
        BaseResourceType.Brick,
        BaseResourceType.IronIngot
    };

    private static readonly IReadOnlyList<BaseResourceType> MedicalResources = new[]
    {
        BaseResourceType.Medicine
    };

    private static readonly Dictionary<BaseResourceType, ResourceDefinitionEntry> Entries = new()
    {
        { BaseResourceType.Food, Entry(BaseResourceType.Food, "\uC2DD\uB7C9", "\uC2DD", 1, true, StorageResourceCategory.Food) },
        { BaseResourceType.Wood, Entry(BaseResourceType.Wood, "\uB098\uBB34", "\uBAA9", 2, true, StorageResourceCategory.RawMaterial) },
        { BaseResourceType.Stone, Entry(BaseResourceType.Stone, "\uB3CC", "\uC11D", 4, true, StorageResourceCategory.RawMaterial) },
        { BaseResourceType.Metal, Entry(BaseResourceType.Metal, "\uAE08\uC18D", "\uAE08", 5, true, StorageResourceCategory.RawMaterial) },
        { BaseResourceType.Plank, Entry(BaseResourceType.Plank, "\uD310\uC7AC", "\uD310", 2, true, StorageResourceCategory.ProcessedMaterial) },
        { BaseResourceType.Brick, Entry(BaseResourceType.Brick, "\uBCBD\uB3CC", "\uBCBD", 3, true, StorageResourceCategory.ProcessedMaterial) },
        { BaseResourceType.IronIngot, Entry(BaseResourceType.IronIngot, "\uCCA0\uAD34", "\uCCA0", 5, true, StorageResourceCategory.ProcessedMaterial) },
        { BaseResourceType.SimpleMeal, Entry(BaseResourceType.SimpleMeal, "\uAC04\uB2E8 \uC2DD\uC0AC", "\uBC25", 1, true, StorageResourceCategory.Meal) },
        { BaseResourceType.Medicine, Entry(BaseResourceType.Medicine, "\uC57D\uD488", "\uC57D", 1, true, StorageResourceCategory.Medical) }
    };

    public static bool TryGet(BaseResourceType type, out ResourceDefinitionEntry entry)
    {
        return Entries.TryGetValue(type, out entry);
    }

    public static ResourceDefinitionEntry Get(BaseResourceType type)
    {
        return Entries.TryGetValue(type, out ResourceDefinitionEntry entry)
            ? entry
            : Entry(type, type.ToString(), "?", 1, true, StorageResourceCategory.Misc);
    }

    public static string GetDisplayName(BaseResourceType type)
    {
        return Get(type).DisplayName;
    }

    public static string GetMarker(BaseResourceType type)
    {
        return Get(type).Marker;
    }

    public static int GetUnitWeight(BaseResourceType type)
    {
        return Get(type).UnitWeight;
    }

    public static bool IsStoredResource(BaseResourceType type)
    {
        return Get(type).IsStoredResource;
    }

    public static StorageResourceCategory GetCategory(BaseResourceType type)
    {
        return Get(type).Category;
    }

    public static IReadOnlyList<BaseResourceType> GetResourcesInCategory(StorageResourceCategory category)
    {
        return category switch
        {
            StorageResourceCategory.Food => FoodResources,
            StorageResourceCategory.Meal => MealResources,
            StorageResourceCategory.RawMaterial => RawMaterialResources,
            StorageResourceCategory.ProcessedMaterial => ProcessedMaterialResources,
            StorageResourceCategory.Medical => MedicalResources,
            _ => AllResourceTypes
        };
    }

    private static ResourceDefinitionEntry Entry(
        BaseResourceType resourceType,
        string displayName,
        string marker,
        int unitWeight,
        bool isStoredResource,
        StorageResourceCategory category)
    {
        return new ResourceDefinitionEntry(resourceType, displayName, marker, unitWeight, isStoredResource, category);
    }
}
