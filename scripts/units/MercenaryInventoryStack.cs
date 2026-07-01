public sealed class MercenaryInventoryStack
{
    public MercenaryInventoryStack(BaseResourceType resourceType, int amount)
    {
        ResourceType = resourceType;
        Amount = amount;
    }

    public BaseResourceType ResourceType { get; set; }
    public int Amount { get; set; }
}
