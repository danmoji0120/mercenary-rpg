using System.Collections.Generic;

public readonly struct BuildCost
{
    private static readonly IReadOnlyDictionary<BaseResourceType, int> EmptyResources = new Dictionary<BaseResourceType, int>();
    private readonly IReadOnlyDictionary<BaseResourceType, int>? _resources;

    public BuildCost(IReadOnlyDictionary<BaseResourceType, int> resources)
    {
        _resources = resources;
    }

    public IReadOnlyDictionary<BaseResourceType, int> Resources => _resources ?? EmptyResources;
    public bool IsEmpty => Resources.Count == 0;
}
