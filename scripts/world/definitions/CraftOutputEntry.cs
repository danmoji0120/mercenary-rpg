using System;

public sealed class CraftOutputEntry
{
    private CraftOutputEntry(
        CraftOutputKind kind,
        BaseResourceType resourceType,
        string equipmentDefinitionId,
        int count)
    {
        Kind = kind;
        ResourceType = resourceType;
        EquipmentDefinitionId = equipmentDefinitionId ?? string.Empty;
        Count = Math.Max(1, count);
    }

    public CraftOutputKind Kind { get; }
    public BaseResourceType ResourceType { get; }
    public string EquipmentDefinitionId { get; }
    public int Count { get; }

    public static CraftOutputEntry Resource(BaseResourceType resourceType, int count)
    {
        if (!Enum.IsDefined(typeof(BaseResourceType), resourceType))
        {
            throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, "Unknown resource output type.");
        }

        return new CraftOutputEntry(CraftOutputKind.Resource, resourceType, string.Empty, count);
    }

    public static CraftOutputEntry Equipment(string equipmentDefinitionId, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(equipmentDefinitionId))
        {
            throw new ArgumentException("Equipment output requires a definition id.", nameof(equipmentDefinitionId));
        }

        return new CraftOutputEntry(CraftOutputKind.Equipment, default, equipmentDefinitionId.Trim(), count);
    }
}
