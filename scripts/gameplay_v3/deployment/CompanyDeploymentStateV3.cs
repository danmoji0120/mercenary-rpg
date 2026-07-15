using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;
using WorldV2;

namespace GameplayV3.Deployment;

public sealed class CompanyDeploymentStateV3
{
    private readonly IReadOnlyList<GlobalCellCoord> _formationCells;

    internal CompanyDeploymentStateV3(
        string companyId,
        int startingSettlementId,
        int deploymentSlotIndex,
        GlobalCellCoord arrivalAnchorCell,
        GlobalCellCoord deploymentAnchorCell,
        IReadOnlyList<GlobalCellCoord> formationCells,
        DateTime assignedUtc,
        int placementAttempts,
        float distanceToSettlementCenter,
        float distanceToNearestRoad,
        float distanceToNearestUnsafeFeatureCore)
    {
        CompanyId = companyId;
        StartingSettlementId = startingSettlementId;
        DeploymentSlotIndex = deploymentSlotIndex;
        ArrivalAnchorCell = arrivalAnchorCell;
        DeploymentAnchorCell = deploymentAnchorCell;
        _formationCells = new ReadOnlyCollection<GlobalCellCoord>(new List<GlobalCellCoord>(formationCells));
        AssignedUtc = assignedUtc.Kind == DateTimeKind.Utc ? assignedUtc : assignedUtc.ToUniversalTime();
        PlacementAttempts = placementAttempts;
        DistanceToSettlementCenter = distanceToSettlementCenter;
        DistanceToNearestRoad = distanceToNearestRoad;
        DistanceToNearestUnsafeFeatureCore = distanceToNearestUnsafeFeatureCore;
    }

    public string CompanyId { get; }
    public int StartingSettlementId { get; }
    public bool HasDeployment => true;
    public int DeploymentSlotIndex { get; }
    public GlobalCellCoord ArrivalAnchorCell { get; }
    public GlobalCellCoord DeploymentAnchorCell { get; }
    public IReadOnlyList<GlobalCellCoord> FormationCells => _formationCells;
    public DateTime AssignedUtc { get; }
    public int PlacementAttempts { get; }
    public float DistanceToSettlementCenter { get; }
    public float DistanceToNearestRoad { get; }
    public float DistanceToNearestUnsafeFeatureCore { get; }

    internal bool Matches(CompanyDeploymentStateV3 other)
    {
        if (CompanyId != other.CompanyId
            || StartingSettlementId != other.StartingSettlementId
            || DeploymentSlotIndex != other.DeploymentSlotIndex
            || ArrivalAnchorCell.Value != other.ArrivalAnchorCell.Value
            || DeploymentAnchorCell.Value != other.DeploymentAnchorCell.Value
            || _formationCells.Count != other._formationCells.Count)
        {
            return false;
        }

        for (int index = 0; index < _formationCells.Count; index++)
        {
            if (_formationCells[index].Value != other._formationCells[index].Value)
            {
                return false;
            }
        }

        return true;
    }
}
