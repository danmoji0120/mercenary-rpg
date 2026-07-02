using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public static class CraftRecipeDatabase
{
    private static readonly IReadOnlyList<CraftRecipeEntry> AllRecipes = new[]
    {
        Entry(
            "process_wood_plank",
            "\uB098\uBB34 \uAC00\uACF5",
            Amounts((BaseResourceType.Wood, 2)),
            Amounts((BaseResourceType.Plank, 1)),
            4.0f,
            TileBuildType.Workbench,
            CraftRecipeCategory.Processing,
            10,
            true,
            "\uB098\uBB34\uB97C \uAC00\uACF5\uD574 \uD310\uC7AC\uB85C \uB9CC\uB4ED\uB2C8\uB2E4."),
        Entry(
            "process_stone_brick",
            "\uB3CC \uAC00\uACF5",
            Amounts((BaseResourceType.Stone, 2)),
            Amounts((BaseResourceType.Brick, 1)),
            5.0f,
            TileBuildType.Workbench,
            CraftRecipeCategory.Processing,
            20,
            true,
            "\uB3CC\uC744 \uB2E4\uB4EC\uC5B4 \uBCBD\uB3CC\uB85C \uB9CC\uB4DC\uB294 \uD6C4\uBCF4 \uB808\uC2DC\uD53C\uC785\uB2C8\uB2E4."),
        Entry(
            "smelt_metal_ingot",
            "\uAE08\uC18D \uC81C\uB828",
            Amounts((BaseResourceType.Metal, 2)),
            Amounts((BaseResourceType.IronIngot, 1)),
            6.0f,
            TileBuildType.Forge,
            CraftRecipeCategory.Smithing,
            30,
            false,
            "Legacy compatibility recipe. New iron ingot smelting uses iron ore and coal."),
        Entry(
            "smelt_iron_ore_ingot",
            "\uCCA0\uAD34 \uC81C\uB828",
            Amounts((BaseResourceType.IronOre, 2), (BaseResourceType.Coal, 1)),
            Amounts((BaseResourceType.IronIngot, 1)),
            7.0f,
            TileBuildType.Forge,
            CraftRecipeCategory.Smithing,
            31,
            true,
            "\uCCA0\uAD11\uC11D\uACFC \uC11D\uD0C4\uC744 \uC81C\uB828\uD574 \uCCA0\uAD34\uB85C \uB9CC\uB4ED\uB2C8\uB2E4."),
        Entry(
            "craft_crude_sword",
            "\uC870\uC7A1\uD55C \uAC80 \uC81C\uC791",
            Amounts((BaseResourceType.IronIngot, 1), (BaseResourceType.Plank, 1)),
            new[] { CraftOutputEntry.Equipment("crude_sword", 1) },
            8.0f,
            TileBuildType.Forge,
            CraftRecipeCategory.Smithing,
            50,
            true,
            "Forge a crude sword from an iron ingot and plank."),
        Entry(
            "craft_crude_spear",
            "\uC870\uC7A1\uD55C \uCC3D \uC81C\uC791",
            Amounts((BaseResourceType.IronIngot, 1), (BaseResourceType.Wood, 2)),
            new[] { CraftOutputEntry.Equipment("crude_spear", 1) },
            7.0f,
            TileBuildType.Forge,
            CraftRecipeCategory.Smithing,
            60,
            true,
            "Forge a crude spear from an iron ingot and wood."),
        Entry(
            "craft_wooden_shield",
            "\uB098\uBB34 \uBC29\uD328 \uC81C\uC791",
            Amounts((BaseResourceType.Plank, 2), (BaseResourceType.IronOre, 1)),
            new[] { CraftOutputEntry.Equipment("wooden_shield", 1) },
            6.0f,
            TileBuildType.Workbench,
            CraftRecipeCategory.Smithing,
            70,
            true,
            "Build a wooden shield from planks and iron ore."),
        Entry(
            "cook_simple_meal",
            "\uAC04\uB2E8 \uC2DD\uC0AC \uC870\uB9AC",
            Amounts((BaseResourceType.Food, 2)),
            Amounts((BaseResourceType.SimpleMeal, 1)),
            4.0f,
            TileBuildType.Hearth,
            CraftRecipeCategory.Cooking,
            40,
            false,
            "\uC2DD\uB7C9\uC744 \uC870\uB9AC\uD574 \uAC04\uB2E8 \uC2DD\uC0AC\uB85C \uB9CC\uB4DC\uB294 \uD6C4\uBCF4 \uB808\uC2DC\uD53C\uC785\uB2C8\uB2E4.")
    };

    private static readonly Dictionary<string, CraftRecipeEntry> RecipesById = BuildRecipeLookup();

    public static IReadOnlyList<CraftRecipeEntry> GetAll()
    {
        return AllRecipes;
    }

    public static IReadOnlyList<CraftRecipeEntry> GetEnabledRecipes()
    {
        List<CraftRecipeEntry> recipes = new();

        foreach (CraftRecipeEntry recipe in AllRecipes)
        {
            if (recipe.IsEnabled)
            {
                recipes.Add(recipe);
            }
        }

        return recipes;
    }

    public static IReadOnlyList<CraftRecipeEntry> GetRecipesForFacility(TileBuildType facilityType)
    {
        List<CraftRecipeEntry> recipes = new();

        foreach (CraftRecipeEntry recipe in AllRecipes)
        {
            if (recipe.RequiredFacilityType == facilityType)
            {
                recipes.Add(recipe);
            }
        }

        return recipes;
    }

    public static bool TryGet(string recipeId, [NotNullWhen(true)] out CraftRecipeEntry recipe)
    {
        if (string.IsNullOrEmpty(recipeId))
        {
            recipe = default!;
            return false;
        }

        CraftRecipeEntry? foundRecipe;
        if (RecipesById.TryGetValue(recipeId, out foundRecipe) && foundRecipe != null)
        {
            recipe = foundRecipe;
            return true;
        }

        recipe = default!;
        return false;
    }

    public static CraftRecipeEntry Get(string recipeId)
    {
        if (TryGet(recipeId, out CraftRecipeEntry recipe))
        {
            return recipe;
        }

        throw new KeyNotFoundException($"Unknown craft recipe id: {recipeId}");
    }

    public static string GetDisplayName(string recipeId)
    {
        return Get(recipeId).DisplayName;
    }

    private static Dictionary<string, CraftRecipeEntry> BuildRecipeLookup()
    {
        Dictionary<string, CraftRecipeEntry> recipesById = new();

        foreach (CraftRecipeEntry recipe in AllRecipes)
        {
            if (!string.IsNullOrEmpty(recipe.RecipeId))
            {
                recipesById[recipe.RecipeId] = recipe;
            }
        }

        return recipesById;
    }

    private static CraftRecipeEntry Entry(
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
        return new CraftRecipeEntry(
            recipeId,
            displayName,
            inputs,
            outputs,
            requiredWork,
            requiredFacilityType,
            category,
            sortOrder,
            isEnabled,
            description);
    }

    private static CraftRecipeEntry Entry(
        string recipeId,
        string displayName,
        IReadOnlyDictionary<BaseResourceType, int> inputs,
        IReadOnlyList<CraftOutputEntry> outputEntries,
        float requiredWork,
        TileBuildType requiredFacilityType,
        CraftRecipeCategory category,
        int sortOrder,
        bool isEnabled,
        string description)
    {
        return new CraftRecipeEntry(
            recipeId,
            displayName,
            inputs,
            outputEntries,
            requiredWork,
            requiredFacilityType,
            category,
            sortOrder,
            isEnabled,
            description);
    }

    private static IReadOnlyDictionary<BaseResourceType, int> Amounts(params (BaseResourceType Type, int Amount)[] amounts)
    {
        Dictionary<BaseResourceType, int> result = new();

        foreach ((BaseResourceType type, int amount) in amounts)
        {
            if (amount > 0)
            {
                result[type] = amount;
            }
        }

        return result;
    }
}
