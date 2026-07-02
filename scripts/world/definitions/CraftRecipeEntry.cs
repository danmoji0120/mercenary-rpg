using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class CraftRecipeEntry
{
    public CraftRecipeEntry(
        string recipeId,
        string displayName,
        IReadOnlyDictionary<BaseResourceType, int> inputs,
        IReadOnlyDictionary<BaseResourceType, int> outputs,
        float requiredWork,
        TileBuildType requiredFacilityType,
        CraftRecipeCategory category,
        int sortOrder,
        bool isEnabled,
        string description)
    {
        RecipeId = recipeId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Inputs = CopyPositiveAmounts(inputs);
        Outputs = CopyPositiveAmounts(outputs);
        OutputEntries = BuildResourceOutputEntries(Outputs);
        RequiredWork = requiredWork > 0.0f ? requiredWork : 0.0f;
        RequiredFacilityType = requiredFacilityType;
        Category = category;
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
        Description = description ?? string.Empty;
    }

    public string RecipeId { get; }
    public string DisplayName { get; }
    public IReadOnlyDictionary<BaseResourceType, int> Inputs { get; }
    // Resource-only legacy-compatible outputs. Equipment crafting should use OutputEntries in later steps.
    public IReadOnlyDictionary<BaseResourceType, int> Outputs { get; }
    public IReadOnlyList<CraftOutputEntry> OutputEntries { get; }
    public float RequiredWork { get; }
    public TileBuildType RequiredFacilityType { get; }
    public CraftRecipeCategory Category { get; }
    public int SortOrder { get; }
    public bool IsEnabled { get; }
    public string Description { get; }

    private static IReadOnlyDictionary<BaseResourceType, int> CopyPositiveAmounts(IReadOnlyDictionary<BaseResourceType, int> source)
    {
        Dictionary<BaseResourceType, int> copy = new();

        if (source != null)
        {
            foreach (KeyValuePair<BaseResourceType, int> pair in source)
            {
                if (pair.Value > 0)
                {
                    copy[pair.Key] = pair.Value;
                }
            }
        }

        return new ReadOnlyDictionary<BaseResourceType, int>(copy);
    }

    private static IReadOnlyList<CraftOutputEntry> BuildResourceOutputEntries(IReadOnlyDictionary<BaseResourceType, int> outputs)
    {
        List<CraftOutputEntry> entries = new();

        foreach (KeyValuePair<BaseResourceType, int> output in outputs)
        {
            if (output.Value > 0)
            {
                entries.Add(CraftOutputEntry.Resource(output.Key, output.Value));
            }
        }

        return new ReadOnlyCollection<CraftOutputEntry>(entries);
    }
}
