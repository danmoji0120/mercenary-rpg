using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class MercenaryEquipmentLoadout
{
    private readonly Dictionary<EquipmentSlotType, string> _equippedInstanceIds = new();

    public MercenaryEquipmentLoadout(string mercenaryId)
    {
        MercenaryId = mercenaryId?.Trim() ?? string.Empty;
    }

    public string MercenaryId { get; }
    public IReadOnlyDictionary<EquipmentSlotType, string> EquippedInstanceIds =>
        new ReadOnlyDictionary<EquipmentSlotType, string>(_equippedInstanceIds);

    public bool TryGetEquipped(EquipmentSlotType slot, out string? instanceId)
    {
        instanceId = null;

        return IsSupportedSlot(slot)
            && _equippedInstanceIds.TryGetValue(slot, out instanceId)
            && !string.IsNullOrWhiteSpace(instanceId);
    }

    public bool IsSlotOccupied(EquipmentSlotType slot)
    {
        return TryGetEquipped(slot, out _);
    }

    public bool TrySetEquipped(EquipmentSlotType slot, string instanceId)
    {
        if (!IsSupportedSlot(slot) || string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        string normalizedInstanceId = instanceId.Trim();

        if (ContainsInstance(normalizedInstanceId)
            && (!_equippedInstanceIds.TryGetValue(slot, out string? existingInstanceId)
                || existingInstanceId != normalizedInstanceId))
        {
            return false;
        }

        _equippedInstanceIds[slot] = normalizedInstanceId;
        return true;
    }

    public bool TryClearSlot(EquipmentSlotType slot, out string? removedInstanceId)
    {
        removedInstanceId = null;

        if (!IsSupportedSlot(slot)
            || !_equippedInstanceIds.TryGetValue(slot, out string? existingInstanceId))
        {
            return false;
        }

        _equippedInstanceIds.Remove(slot);
        removedInstanceId = existingInstanceId;
        return true;
    }

    public bool ContainsInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        string normalizedInstanceId = instanceId.Trim();

        foreach (string equippedInstanceId in _equippedInstanceIds.Values)
        {
            if (equippedInstanceId == normalizedInstanceId)
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<string> GetAllEquippedInstanceIds()
    {
        List<string> instanceIds = new();

        foreach (string instanceId in _equippedInstanceIds.Values)
        {
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                instanceIds.Add(instanceId);
            }
        }

        return instanceIds;
    }

    private static bool IsSupportedSlot(EquipmentSlotType slot)
    {
        return slot == EquipmentSlotType.Weapon
            || slot == EquipmentSlotType.Armor
            || slot == EquipmentSlotType.Shield
            || slot == EquipmentSlotType.Accessory
            || slot == EquipmentSlotType.Tool;
    }
}
