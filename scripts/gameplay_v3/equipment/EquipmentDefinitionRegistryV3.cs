using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameplayV3.Equipment;

public sealed class EquipmentDefinitionRegistryV3
{
    private readonly Dictionary<string, EquipmentDefinitionV3> _byId = new(StringComparer.Ordinal);
    private readonly List<EquipmentDefinitionV3> _ordered = new();
    private readonly ReadOnlyCollection<EquipmentDefinitionV3> _readOnlyDefinitions;

    public EquipmentDefinitionRegistryV3()
    {
        _readOnlyDefinitions = _ordered.AsReadOnly();
    }

    public int Count => _byId.Count;
    public bool IsSealed { get; private set; }

    public bool TryRegister(EquipmentDefinitionV3? definition, out string reason)
    {
        if (IsSealed)
        {
            reason = "Equipment definition registry is sealed.";
            return false;
        }
        if (definition == null || !IsValidDefinitionId(definition.DefinitionId))
        {
            reason = "Equipment definition is invalid.";
            return false;
        }
        if (_byId.ContainsKey(definition.DefinitionId))
        {
            reason = "Duplicate equipment DefinitionId.";
            return false;
        }
        if (!Enum.IsDefined(definition.Slot))
        {
            reason = "Equipment slot is invalid.";
            return false;
        }
        _byId.Add(definition.DefinitionId, definition);
        _ordered.Add(definition);
        reason = string.Empty;
        return true;
    }

    public void Seal() => IsSealed = true;
    public bool TryGetDefinition(string definitionId, out EquipmentDefinitionV3? definition) =>
        _byId.TryGetValue(definitionId, out definition);
    public bool Contains(string definitionId) => _byId.ContainsKey(definitionId);
    public IReadOnlyList<EquipmentDefinitionV3> GetAllDefinitions() => _readOnlyDefinitions;

    internal static bool IsValidDefinitionId(string? definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId)) return false;
        foreach (char character in definitionId)
        {
            if ((character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9') ||
                character == '_') continue;
            return false;
        }
        return true;
    }
}

public static class StarterEquipmentContentV3
{
    public const string IronSwordDefinitionId = "iron_sword";
    public const string PaddedArmorDefinitionId = "padded_armor";
    public const string IronPickaxeDefinitionId = "iron_pickaxe";

    public static EquipmentDefinitionRegistryV3 CreateRegistry()
    {
        EquipmentDefinitionRegistryV3 registry = new();
        Register(
            IronSwordDefinitionId,
            "\ucca0\uac80",
            "\ucca0\ub85c \ub9cc\ub4e0 \uae30\ubcf8\uc801\uc778 \ud55c\uc190 \uac80\uc785\ub2c8\ub2e4.",
            EquipmentSlotV3.MainHand,
            120,
            new(EquipmentModifierKindV3.AttackPower, 10.0, true));
        Register(
            PaddedArmorDefinitionId,
            "\ub204\ube44\uac11\uc637",
            "\uc5ec\ub7ec \uacb9\uc758 \ucc9c\uc744 \ub367\ub300\uc5b4 \ub9cc\ub4e0 \uae30\ubcf8 \ubc29\uc5b4\uad6c\uc785\ub2c8\ub2e4.",
            EquipmentSlotV3.Armor,
            100,
            new(EquipmentModifierKindV3.DefensePower, 8.0, true));
        Register(
            IronPickaxeDefinitionId,
            "\ucca0 \uace1\uad2d\uc774",
            "\uad11\ubb3c \ucc44\uc9d1\uc5d0 \uc4f0\ub294 \uae30\ubcf8\uc801\uc778 \ucca0\uc81c \ub3c4\uad6c\uc785\ub2c8\ub2e4.",
            EquipmentSlotV3.Tool,
            90,
            new(EquipmentModifierKindV3.GatheringWorkSpeed, 0.20, true));
        registry.Seal();
        return registry;

        void Register(
            string id,
            string name,
            string description,
            EquipmentSlotV3 slot,
            int value,
            EquipmentStatModifierV3 modifier)
        {
            if (!EquipmentDefinitionV3.TryCreate(id, name, description, slot, new[] { modifier }, value, out EquipmentDefinitionV3? definition, out string reason) ||
                definition == null || !registry.TryRegister(definition, out reason))
                throw new InvalidOperationException($"Invalid starter equipment definition '{id}': {reason}");
        }
    }
}
