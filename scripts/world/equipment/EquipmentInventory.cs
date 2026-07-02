using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class EquipmentInventory
{
    private readonly List<EquipmentInstance> _items = new();
    private readonly Dictionary<string, EquipmentInstance> _itemsById = new();

    public IReadOnlyList<EquipmentInstance> Items => new ReadOnlyCollection<EquipmentInstance>(_items);
    public int Count => _items.Count;

    public bool TryAdd(EquipmentInstance instance)
    {
        if (!CanAdd(instance))
        {
            return false;
        }

        _items.Add(instance);
        _itemsById[instance.InstanceId] = instance;
        return true;
    }

    public bool CanAdd(EquipmentInstance instance)
    {
        return instance != null
            && !string.IsNullOrWhiteSpace(instance.InstanceId)
            && !_itemsById.ContainsKey(instance.InstanceId);
    }

    public bool TryRemove(string instanceId, out EquipmentInstance? instance)
    {
        instance = null;

        if (string.IsNullOrWhiteSpace(instanceId)
            || !_itemsById.TryGetValue(instanceId, out EquipmentInstance? foundInstance))
        {
            return false;
        }

        _itemsById.Remove(instanceId);
        _items.Remove(foundInstance);
        instance = foundInstance;
        return true;
    }

    public bool TryGet(string instanceId, out EquipmentInstance? instance)
    {
        instance = null;

        return !string.IsNullOrWhiteSpace(instanceId)
            && _itemsById.TryGetValue(instanceId, out instance);
    }

    public IReadOnlyList<EquipmentInstance> GetByDefinition(string definitionId)
    {
        List<EquipmentInstance> items = new();

        if (string.IsNullOrWhiteSpace(definitionId))
        {
            return items;
        }

        foreach (EquipmentInstance item in _items)
        {
            if (item.DefinitionId == definitionId)
            {
                items.Add(item);
            }
        }

        return items;
    }

    public IReadOnlyList<EquipmentInstance> GetBySlot(EquipmentSlotType slotType)
    {
        List<EquipmentInstance> items = new();

        foreach (EquipmentInstance item in _items)
        {
            if (EquipmentDefinitionDatabase.TryGet(item.DefinitionId, out EquipmentDefinitionEntry? definition)
                && definition.SlotType == slotType)
            {
                items.Add(item);
            }
        }

        return items;
    }
}
