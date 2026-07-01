public readonly struct ResourceDefinitionEntry
{
    public ResourceDefinitionEntry(
        BaseResourceType resourceType,
        string displayName,
        string marker,
        int unitWeight,
        bool isStoredResource,
        StorageResourceCategory category)
    {
        ResourceType = resourceType;
        DisplayName = displayName;
        Marker = marker;
        UnitWeight = unitWeight;
        IsStoredResource = isStoredResource;
        Category = category;
    }

    public BaseResourceType ResourceType { get; }
    public string DisplayName { get; }
    public string Marker { get; }
    public int UnitWeight { get; }
    public bool IsStoredResource { get; }
    public StorageResourceCategory Category { get; }
}
