using System.Collections.Generic;
using Godot;

public partial class MercenaryEquipmentLoadoutManager : Node
{
    private readonly Dictionary<string, MercenaryEquipmentLoadout> _loadoutsByMercenaryId = new();

    public int Count => _loadoutsByMercenaryId.Count;

    public MercenaryEquipmentLoadout GetOrCreateLoadout(string mercenaryId)
    {
        string normalizedMercenaryId = NormalizeId(mercenaryId);

        if (!_loadoutsByMercenaryId.TryGetValue(normalizedMercenaryId, out MercenaryEquipmentLoadout? loadout))
        {
            loadout = new MercenaryEquipmentLoadout(normalizedMercenaryId);
            _loadoutsByMercenaryId[normalizedMercenaryId] = loadout;
        }

        return loadout;
    }

    public bool TryGetLoadout(string mercenaryId, out MercenaryEquipmentLoadout? loadout)
    {
        loadout = null;
        string normalizedMercenaryId = NormalizeId(mercenaryId);

        return !string.IsNullOrWhiteSpace(normalizedMercenaryId)
            && _loadoutsByMercenaryId.TryGetValue(normalizedMercenaryId, out loadout);
    }

    public IReadOnlyList<MercenaryEquipmentLoadout> GetAllLoadouts()
    {
        List<MercenaryEquipmentLoadout> loadouts = new();

        foreach (MercenaryEquipmentLoadout loadout in _loadoutsByMercenaryId.Values)
        {
            loadouts.Add(loadout);
        }

        return loadouts;
    }

    public bool TryAssign(string mercenaryId, EquipmentSlotType slot, string instanceId)
    {
        if (string.IsNullOrWhiteSpace(mercenaryId)
            || string.IsNullOrWhiteSpace(instanceId)
            || IsInstanceEquipped(instanceId))
        {
            return false;
        }

        MercenaryEquipmentLoadout loadout = GetOrCreateLoadout(mercenaryId);
        return loadout.TrySetEquipped(slot, instanceId);
    }

    public bool TryUnassign(string mercenaryId, EquipmentSlotType slot, out string? removedInstanceId)
    {
        removedInstanceId = null;

        return TryGetLoadout(mercenaryId, out MercenaryEquipmentLoadout? loadout)
            && loadout != null
            && loadout.TryClearSlot(slot, out removedInstanceId);
    }

    public bool TryEquip(
        string mercenaryId,
        EquipmentSlotType slot,
        string instanceId,
        EquipmentInventoryManager equipmentInventory,
        out string? reason)
    {
        reason = null;
        string normalizedMercenaryId = NormalizeId(mercenaryId);
        string normalizedInstanceId = NormalizeId(instanceId);

        if (string.IsNullOrWhiteSpace(normalizedMercenaryId))
        {
            reason = "Mercenary id is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedInstanceId))
        {
            reason = "Equipment instance id is empty.";
            return false;
        }

        if (equipmentInventory == null)
        {
            reason = "Equipment inventory is missing.";
            return false;
        }

        if (!equipmentInventory.TryGetEquipment(normalizedInstanceId, out EquipmentInstance? instance) || instance == null)
        {
            reason = "Equipment instance was not found.";
            return false;
        }

        if (!CanAssignToSlot(instance, slot))
        {
            reason = "Equipment slot mismatch.";
            return false;
        }

        if (TryGetLoadout(normalizedMercenaryId, out MercenaryEquipmentLoadout? loadout)
            && loadout != null
            && loadout.IsSlotOccupied(slot))
        {
            reason = "Equipment slot is already occupied.";
            return false;
        }

        string? ownerId = GetEquippedOwnerId(normalizedInstanceId);
        if (ownerId != null)
        {
            reason = ownerId == normalizedMercenaryId
                ? "Equipment is already assigned to this mercenary."
                : "Equipment is already assigned to another mercenary.";
            return false;
        }

        if (instance.IsEquipped)
        {
            if (!string.IsNullOrWhiteSpace(instance.EquippedMercenaryId)
                && instance.EquippedMercenaryId != normalizedMercenaryId)
            {
                reason = "Equipment instance is marked equipped by another mercenary.";
                return false;
            }

            reason = "Equipment instance is already marked equipped.";
            return false;
        }

        MercenaryEquipmentLoadout targetLoadout = GetOrCreateLoadout(normalizedMercenaryId);
        if (!targetLoadout.TrySetEquipped(slot, normalizedInstanceId))
        {
            reason = "Failed to assign equipment to loadout.";
            return false;
        }

        instance.MarkEquipped(normalizedMercenaryId);
        return true;
    }

    public bool TryUnequip(
        string mercenaryId,
        EquipmentSlotType slot,
        EquipmentInventoryManager equipmentInventory,
        out string? removedInstanceId,
        out string? reason)
    {
        removedInstanceId = null;
        reason = null;
        string normalizedMercenaryId = NormalizeId(mercenaryId);

        if (string.IsNullOrWhiteSpace(normalizedMercenaryId))
        {
            reason = "Mercenary id is empty.";
            return false;
        }

        if (equipmentInventory == null)
        {
            reason = "Equipment inventory is missing.";
            return false;
        }

        if (!TryGetLoadout(normalizedMercenaryId, out MercenaryEquipmentLoadout? loadout)
            || loadout == null
            || !loadout.TryGetEquipped(slot, out string? equippedInstanceId)
            || string.IsNullOrWhiteSpace(equippedInstanceId))
        {
            reason = "Equipment slot is empty.";
            return false;
        }

        if (!loadout.TryClearSlot(slot, out removedInstanceId))
        {
            reason = "Failed to clear equipment slot.";
            return false;
        }

        if (equipmentInventory.TryGetEquipment(removedInstanceId!, out EquipmentInstance? instance) && instance != null)
        {
            instance.MarkUnequipped();
            return true;
        }

        reason = "Cleared orphaned equipment reference.";
        return true;
    }

    public bool IsInstanceEquipped(string instanceId)
    {
        return GetEquippedOwnerId(instanceId) != null;
    }

    public string? GetEquippedOwnerId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        string normalizedInstanceId = instanceId.Trim();

        foreach (MercenaryEquipmentLoadout loadout in _loadoutsByMercenaryId.Values)
        {
            if (loadout.ContainsInstance(normalizedInstanceId))
            {
                return loadout.MercenaryId;
            }
        }

        return null;
    }

    public bool CanAssignToSlot(EquipmentInstance instance, EquipmentSlotType slot)
    {
        return instance != null
            && CanAssignDefinitionToSlot(instance.DefinitionId, slot);
    }

    public bool CanAssignDefinitionToSlot(string definitionId, EquipmentSlotType slot)
    {
        return EquipmentDefinitionDatabase.TryGet(definitionId, out EquipmentDefinitionEntry? definition)
            && definition.SlotType == slot;
    }

    private static string NormalizeId(string id)
    {
        return id?.Trim() ?? string.Empty;
    }
}
