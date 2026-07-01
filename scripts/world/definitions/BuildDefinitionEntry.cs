using Godot;

public readonly struct BuildDefinitionEntry
{
    public BuildDefinitionEntry(
        TileBuildType buildType,
        string displayName,
        BuildCost cost,
        Vector2I size,
        bool usesDirectConstruction)
    {
        BuildType = buildType;
        DisplayName = displayName;
        Cost = cost;
        Size = size;
        UsesDirectConstruction = usesDirectConstruction;
    }

    public TileBuildType BuildType { get; }
    public string DisplayName { get; }
    public BuildCost Cost { get; }
    public Vector2I Size { get; }
    public bool UsesDirectConstruction { get; }
}
