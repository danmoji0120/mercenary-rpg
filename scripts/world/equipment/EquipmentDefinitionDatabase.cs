using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public static class EquipmentDefinitionDatabase
{
    private static readonly IReadOnlyList<EquipmentDefinitionEntry> AllDefinitions = new[]
    {
        Entry(
            "crude_sword",
            "\uC870\uC7A1\uD55C \uAC80",
            EquipmentSlotType.Weapon,
            "\uC870\uC7A1\uD558\uC9C0\uB9CC \uC2E4\uC804\uC5D0 \uC4F8 \uC218 \uC788\uB294 \uAE30\uBCF8 \uAC80\uC785\uB2C8\uB2E4.",
            10,
            true,
            2,
            0,
            40),
        Entry(
            "crude_spear",
            "\uC870\uC7A1\uD55C \uCC3D",
            EquipmentSlotType.Weapon,
            "\uAE38\uAC8C \uB2E4\uB4EC\uC740 \uB098\uBB34 \uC790\uB8E8\uC640 \uAE08\uC18D \uB0A0\uB85C \uB9CC\uB4E0 \uCC3D\uC785\uB2C8\uB2E4.",
            20,
            true,
            2,
            0,
            35),
        Entry(
            "wooden_shield",
            "\uB098\uBB34 \uBC29\uD328",
            EquipmentSlotType.Shield,
            "\uC801\uC758 \uACF5\uACA9\uC744 \uB9C9\uAE30 \uC704\uD55C \uAC04\uB2E8\uD55C \uB098\uBB34 \uBC29\uD328\uC785\uB2C8\uB2E4.",
            30,
            true,
            0,
            2,
            35),
        Entry(
            "crude_breastplate",
            "\uC870\uC7A1\uD55C \uD749\uAC11",
            EquipmentSlotType.Armor,
            "\uBAB8\uD1B5\uC744 \uBCF4\uD638\uD558\uB294 \uC870\uC7A1\uD55C \uAE08\uC18D \uAC11\uC637\uC785\uB2C8\uB2E4.",
            40,
            true,
            0,
            3,
            50)
    };

    private static readonly Dictionary<string, EquipmentDefinitionEntry> DefinitionsById = BuildDefinitionLookup();

    public static IReadOnlyList<EquipmentDefinitionEntry> GetAll()
    {
        return AllDefinitions;
    }

    public static IReadOnlyList<EquipmentDefinitionEntry> GetEnabledDefinitions()
    {
        List<EquipmentDefinitionEntry> definitions = new();

        foreach (EquipmentDefinitionEntry definition in AllDefinitions)
        {
            if (definition.IsEnabled)
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));
        return definitions;
    }

    public static bool TryGet(string definitionId, [NotNullWhen(true)] out EquipmentDefinitionEntry? definition)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            definition = null;
            return false;
        }

        return DefinitionsById.TryGetValue(definitionId, out definition);
    }

    public static EquipmentDefinitionEntry Get(string definitionId)
    {
        if (TryGet(definitionId, out EquipmentDefinitionEntry? definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"Unknown equipment definition id: {definitionId}");
    }

    public static string GetDisplayName(string definitionId)
    {
        return TryGet(definitionId, out EquipmentDefinitionEntry? definition)
            ? definition.DisplayName
            : definitionId;
    }

    private static Dictionary<string, EquipmentDefinitionEntry> BuildDefinitionLookup()
    {
        Dictionary<string, EquipmentDefinitionEntry> definitionsById = new();

        foreach (EquipmentDefinitionEntry definition in AllDefinitions)
        {
            if (!string.IsNullOrWhiteSpace(definition.DefinitionId))
            {
                definitionsById[definition.DefinitionId] = definition;
            }
        }

        return definitionsById;
    }

    private static EquipmentDefinitionEntry Entry(
        string definitionId,
        string displayName,
        EquipmentSlotType slotType,
        string description,
        int sortOrder,
        bool isEnabled,
        int attackBonus,
        int defenseBonus,
        int maxDurability)
    {
        return new EquipmentDefinitionEntry(
            definitionId,
            displayName,
            slotType,
            description,
            sortOrder,
            isEnabled,
            attackBonus,
            defenseBonus,
            maxDurability);
    }
}
