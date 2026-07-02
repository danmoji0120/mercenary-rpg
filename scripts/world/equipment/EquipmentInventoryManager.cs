using System.Collections.Generic;
using Godot;

public partial class EquipmentInventoryManager : Node
{
    public EquipmentInventory Inventory { get; } = new();
    public int Count => Inventory.Count;

    public bool TryAddEquipment(EquipmentInstance instance)
    {
        return Inventory.TryAdd(instance);
    }

    public bool CanAddEquipment(EquipmentInstance instance)
    {
        return Inventory.CanAdd(instance);
    }

    public bool CanAddAllEquipment(IEnumerable<EquipmentInstance> instances)
    {
        if (instances == null)
        {
            return false;
        }

        HashSet<string> seenIds = new();

        foreach (EquipmentInstance instance in instances)
        {
            if (instance == null
                || string.IsNullOrWhiteSpace(instance.InstanceId)
                || !seenIds.Add(instance.InstanceId)
                || !CanAddEquipment(instance))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryAddAllEquipment(IEnumerable<EquipmentInstance> instances)
    {
        if (!CanAddAllEquipment(instances))
        {
            return false;
        }

        List<string> addedIds = new();

        foreach (EquipmentInstance instance in instances)
        {
            if (!TryAddEquipment(instance))
            {
                foreach (string addedId in addedIds)
                {
                    TryRemoveEquipment(addedId, out _);
                }

                return false;
            }

            addedIds.Add(instance.InstanceId);
        }

        return true;
    }

    public bool TryRemoveEquipment(string instanceId, out EquipmentInstance? instance)
    {
        return Inventory.TryRemove(instanceId, out instance);
    }

    public bool TryGetEquipment(string instanceId, out EquipmentInstance? instance)
    {
        return Inventory.TryGet(instanceId, out instance);
    }

    public IReadOnlyList<EquipmentInstance> GetAllEquipment()
    {
        return Inventory.Items;
    }

    public IReadOnlyList<EquipmentInstance> GetAvailableEquipment()
    {
        List<EquipmentInstance> items = new();

        foreach (EquipmentInstance item in Inventory.Items)
        {
            if (!item.IsEquipped)
            {
                items.Add(item);
            }
        }

        return items;
    }

    public static string GetDisplayName(EquipmentInstance instance)
    {
        return instance == null
            ? string.Empty
            : EquipmentDefinitionDatabase.GetDisplayName(instance.DefinitionId);
    }

    public static string GetSummary(EquipmentInstance instance)
    {
        if (instance == null)
        {
            return "-";
        }

        string displayName = GetDisplayName(instance);
        return $"{displayName} / Durability {instance.Durability}/{instance.MaxDurability}";
    }
}
