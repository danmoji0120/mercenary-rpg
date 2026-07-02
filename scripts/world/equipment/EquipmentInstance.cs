using System;
using Godot;

public sealed class EquipmentInstance
{
    public EquipmentInstance(
        string instanceId,
        string definitionId,
        int durability,
        int maxDurability,
        bool isEquipped = false,
        string? equippedMercenaryId = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Equipment instance requires an instance id.", nameof(instanceId));
        }

        if (string.IsNullOrWhiteSpace(definitionId))
        {
            throw new ArgumentException("Equipment instance requires a definition id.", nameof(definitionId));
        }

        InstanceId = instanceId;
        DefinitionId = definitionId;
        MaxDurability = Math.Max(1, maxDurability);
        Durability = Mathf.Clamp(durability, 0, MaxDurability);
        IsEquipped = isEquipped;
        EquippedMercenaryId = string.IsNullOrWhiteSpace(equippedMercenaryId) ? null : equippedMercenaryId;
    }

    // Stable id intended for future save/load and inventory references.
    public string InstanceId { get; }
    public string DefinitionId { get; }
    public int Durability { get; private set; }
    public int MaxDurability { get; }
    public bool IsEquipped { get; private set; }
    public string? EquippedMercenaryId { get; private set; }

    public void SetDurability(int durability)
    {
        Durability = Mathf.Clamp(durability, 0, MaxDurability);
    }

    public void MarkEquipped(string equippedMercenaryId)
    {
        IsEquipped = true;
        EquippedMercenaryId = string.IsNullOrWhiteSpace(equippedMercenaryId) ? null : equippedMercenaryId;
    }

    public void MarkUnequipped()
    {
        IsEquipped = false;
        EquippedMercenaryId = null;
    }
}
