using Godot;

public readonly struct GeneratedResourceNodeData
{
    public GeneratedResourceNodeData(BaseResourceType resourceType, Vector2I cell, int amount)
    {
        ResourceType = resourceType;
        Cell = cell;
        Amount = amount;
    }

    public BaseResourceType ResourceType { get; }
    public Vector2I Cell { get; }
    public int Amount { get; }
}
