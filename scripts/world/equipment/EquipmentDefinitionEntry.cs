public sealed class EquipmentDefinitionEntry
{
    public EquipmentDefinitionEntry(
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
        DefinitionId = definitionId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        SlotType = slotType;
        Description = description ?? string.Empty;
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
        AttackBonus = attackBonus;
        DefenseBonus = defenseBonus;
        MaxDurability = maxDurability > 0 ? maxDurability : 1;
    }

    public string DefinitionId { get; }
    public string DisplayName { get; }
    public EquipmentSlotType SlotType { get; }
    public string Description { get; }
    public int SortOrder { get; }
    public bool IsEnabled { get; }
    public int AttackBonus { get; }
    public int DefenseBonus { get; }
    public int MaxDurability { get; }
}
