public sealed class ConstructionRequirement
{
    public ConstructionRequirement(BaseResourceType resourceType, int requiredAmount)
    {
        ResourceType = resourceType;
        RequiredAmount = requiredAmount;
    }

    public BaseResourceType ResourceType { get; }
    public int RequiredAmount { get; }
    public int DeliveredAmount { get; set; }
    public int RemainingAmount => System.Math.Max(0, RequiredAmount - DeliveredAmount);
    public bool IsSatisfied => RemainingAmount <= 0;
}
