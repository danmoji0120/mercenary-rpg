namespace GameplayV3.Mercenary;

public sealed class MercenaryDerivedStatsV3
{
    public MercenaryDerivedStatsV3(float moveSpeedMultiplier, float carryCapacity, float workSpeedMultiplier)
    {
        MoveSpeedMultiplier = moveSpeedMultiplier;
        CarryCapacity = carryCapacity;
        WorkSpeedMultiplier = workSpeedMultiplier;
    }

    // Actual movement is intentionally not implemented here; this value composes with global and terrain multipliers later.
    public float MoveSpeedMultiplier { get; }
    public float CarryCapacity { get; }
    public float WorkSpeedMultiplier { get; }
}
