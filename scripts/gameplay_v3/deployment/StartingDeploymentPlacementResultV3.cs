using System.Collections.Generic;
using System.Collections.ObjectModel;
using WorldV2;

namespace GameplayV3.Deployment;

public sealed class StartingDeploymentPlacementResultV3
{
    public StartingDeploymentPlacementResultV3(
        bool isInitialized,
        int startingSettlementId,
        GlobalCellCoord settlementCenterCell,
        GlobalCellCoord? arrivalAnchorCell,
        float distanceToNearestRoad,
        int placementAttempts,
        int rejectedBounds,
        int rejectedFeature,
        int rejectedFormation,
        int assignedCompanyCount,
        int unassignedCompanyCount,
        IReadOnlyList<string> newlyAssignedCompanyIds,
        string failureReason)
    {
        IsInitialized = isInitialized;
        StartingSettlementId = startingSettlementId;
        SettlementCenterCell = settlementCenterCell;
        ArrivalAnchorCell = arrivalAnchorCell;
        DistanceToNearestRoad = distanceToNearestRoad;
        PlacementAttempts = placementAttempts;
        RejectedBounds = rejectedBounds;
        RejectedFeature = rejectedFeature;
        RejectedFormation = rejectedFormation;
        AssignedCompanyCount = assignedCompanyCount;
        UnassignedCompanyCount = unassignedCompanyCount;
        NewlyAssignedCompanyIds = new ReadOnlyCollection<string>(new List<string>(newlyAssignedCompanyIds));
        FailureReason = failureReason;
    }

    public bool IsInitialized { get; }
    public bool HasArrivalAnchor => ArrivalAnchorCell.HasValue;
    public bool IsSuccessful => HasArrivalAnchor && string.IsNullOrEmpty(FailureReason);
    public int StartingSettlementId { get; }
    public GlobalCellCoord SettlementCenterCell { get; }
    public GlobalCellCoord? ArrivalAnchorCell { get; }
    public float DistanceToNearestRoad { get; }
    public int PlacementAttempts { get; }
    public int RejectedBounds { get; }
    public int RejectedFeature { get; }
    public int RejectedFormation { get; }
    public int AssignedCompanyCount { get; }
    public int UnassignedCompanyCount { get; }
    public IReadOnlyList<string> NewlyAssignedCompanyIds { get; }
    public string FailureReason { get; }
}
