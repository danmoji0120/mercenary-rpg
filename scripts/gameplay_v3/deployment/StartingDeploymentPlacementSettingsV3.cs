namespace GameplayV3.Deployment;

public sealed class StartingDeploymentPlacementSettingsV3
{
    public static StartingDeploymentPlacementSettingsV3 Default { get; } = new();

    public int MaxCompanyCount { get; init; } = 4;
    public int MercenaryCountPerCompany { get; init; } = 3;
    public int CompanySlotSpacing { get; init; } = 10;
    public int FormationCellSpacing { get; init; } = 2;
    public int SettlementBoundaryInnerOffset { get; init; } = 6;
    public int SettlementBoundaryOuterOffset { get; init; } = 6;
    public int ArrivalCandidateAttemptLimit { get; init; } = 24;
    public int WorldEdgeMargin { get; init; } = 8;
    public int FeatureClearance { get; init; } = 4;
    public float RoadPreferenceDistance { get; init; } = 18.0f;
    public float MaxRoadSearchDistance { get; init; } = 96.0f;
    public bool AllowInsideSettlement { get; init; } = true;
    public bool AllowSettlementEdgeFallback { get; init; } = true;
}
