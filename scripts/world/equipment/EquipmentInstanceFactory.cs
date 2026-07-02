using System;
using System.Diagnostics.CodeAnalysis;

public static class EquipmentInstanceFactory
{
    public static EquipmentInstance CreateFromDefinition(string definitionId)
    {
        if (TryCreateFromDefinition(definitionId, out EquipmentInstance? instance))
        {
            return instance;
        }

        throw new ArgumentException($"Unknown equipment definition id: {definitionId}", nameof(definitionId));
    }

    public static bool TryCreateFromDefinition(string definitionId, [NotNullWhen(true)] out EquipmentInstance? instance)
    {
        instance = null;

        if (!EquipmentDefinitionDatabase.TryGet(definitionId, out EquipmentDefinitionEntry? definition))
        {
            return false;
        }

        string instanceId = Guid.NewGuid().ToString("N");
        instance = new EquipmentInstance(
            instanceId,
            definition.DefinitionId,
            definition.MaxDurability,
            definition.MaxDurability);
        return true;
    }
}
