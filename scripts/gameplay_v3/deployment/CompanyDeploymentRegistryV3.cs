using System;
using System.Collections.Generic;
using GameplayV3.Company;
using Godot;
using WorldV2;

namespace GameplayV3.Deployment;

public sealed class CompanyDeploymentRegistryV3
{
    private readonly CompanyRegistryV3 _companyRegistry;
    private readonly Dictionary<string, CompanyDeploymentStateV3> _deploymentsByCompanyId = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _companyIdsBySlot = new();
    private readonly Dictionary<Vector2I, string> _companyIdsByFormationCell = new();
    private readonly Dictionary<Vector2I, string> _companyIdsByAnchorCell = new();

    public CompanyDeploymentRegistryV3(CompanyRegistryV3 companyRegistry)
    {
        _companyRegistry = companyRegistry;
    }

    public int Count => _deploymentsByCompanyId.Count;

    public bool TryRegisterDeployment(CompanyDeploymentStateV3? deployment, out string reason)
    {
        if (deployment == null)
        {
            reason = "Deployment cannot be null.";
            return false;
        }

        if (!_companyRegistry.ContainsCompany(deployment.CompanyId))
        {
            reason = $"Deployment company is not registered: {deployment.CompanyId}";
            return false;
        }

        if (deployment.StartingSettlementId < 0 || deployment.DeploymentSlotIndex < 0)
        {
            reason = "StartingSettlementId and DeploymentSlotIndex must be non-negative.";
            return false;
        }

        if (deployment.FormationCells.Count != 3)
        {
            reason = "A deployment must contain exactly three formation cells.";
            return false;
        }

        if (_deploymentsByCompanyId.TryGetValue(deployment.CompanyId, out CompanyDeploymentStateV3? existing))
        {
            if (existing.Matches(deployment))
            {
                reason = string.Empty;
                return true;
            }

            reason = $"Company already has an immutable deployment: {deployment.CompanyId}";
            return false;
        }

        if (_companyIdsBySlot.TryGetValue(deployment.DeploymentSlotIndex, out string? slotOwner))
        {
            reason = $"Deployment slot is already in use by {slotOwner}.";
            return false;
        }

        if (_companyIdsByAnchorCell.TryGetValue(deployment.DeploymentAnchorCell.Value, out string? anchorOwner))
        {
            reason = $"Deployment anchor cell is already in use by {anchorOwner}.";
            return false;
        }

        HashSet<Vector2I> uniqueFormationCells = new();
        foreach (GlobalCellCoord formationCell in deployment.FormationCells)
        {
            if (!uniqueFormationCells.Add(formationCell.Value))
            {
                reason = "Deployment formation contains duplicate cells.";
                return false;
            }

            if (_companyIdsByFormationCell.TryGetValue(formationCell.Value, out string? formationOwner))
            {
                reason = $"Deployment formation cell is already in use by {formationOwner}.";
                return false;
            }
        }

        _deploymentsByCompanyId.Add(deployment.CompanyId, deployment);
        _companyIdsBySlot.Add(deployment.DeploymentSlotIndex, deployment.CompanyId);
        _companyIdsByAnchorCell.Add(deployment.DeploymentAnchorCell.Value, deployment.CompanyId);
        foreach (GlobalCellCoord formationCell in deployment.FormationCells)
        {
            _companyIdsByFormationCell.Add(formationCell.Value, deployment.CompanyId);
        }

        reason = string.Empty;
        return true;
    }

    public bool TryCreateDeployment(
        string companyId,
        int startingSettlementId,
        int deploymentSlotIndex,
        GlobalCellCoord arrivalAnchorCell,
        GlobalCellCoord deploymentAnchorCell,
        IReadOnlyList<GlobalCellCoord> formationCells,
        int placementAttempts,
        float distanceToSettlementCenter,
        float distanceToNearestRoad,
        float distanceToNearestUnsafeFeatureCore,
        out CompanyDeploymentStateV3? deployment,
        out string reason)
    {
        deployment = new CompanyDeploymentStateV3(
            companyId,
            startingSettlementId,
            deploymentSlotIndex,
            arrivalAnchorCell,
            deploymentAnchorCell,
            formationCells,
            DateTime.UtcNow,
            placementAttempts,
            distanceToSettlementCenter,
            distanceToNearestRoad,
            distanceToNearestUnsafeFeatureCore);

        if (TryRegisterDeployment(deployment, out reason))
        {
            return true;
        }

        deployment = null;
        return false;
    }

    public bool TryGetDeployment(string companyId, out CompanyDeploymentStateV3? deployment)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            deployment = null;
            return false;
        }

        return _deploymentsByCompanyId.TryGetValue(companyId, out deployment);
    }

    public bool ContainsDeployment(string companyId)
    {
        return !string.IsNullOrWhiteSpace(companyId) && _deploymentsByCompanyId.ContainsKey(companyId);
    }

    public bool ContainsSlot(int deploymentSlotIndex)
    {
        return _companyIdsBySlot.ContainsKey(deploymentSlotIndex);
    }

    public bool TryRemoveDeployment(string companyId, out string reason)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            reason = "CompanyId cannot be empty.";
            return false;
        }

        if (!_deploymentsByCompanyId.Remove(companyId, out CompanyDeploymentStateV3? deployment) || deployment == null)
        {
            reason = $"Company deployment is not registered: {companyId}";
            return false;
        }

        _companyIdsBySlot.Remove(deployment.DeploymentSlotIndex);
        _companyIdsByAnchorCell.Remove(deployment.DeploymentAnchorCell.Value);
        foreach (GlobalCellCoord formationCell in deployment.FormationCells)
        {
            _companyIdsByFormationCell.Remove(formationCell.Value);
        }

        reason = string.Empty;
        return true;
    }

    public IReadOnlyList<CompanyDeploymentStateV3> GetAllDeployments()
    {
        return new List<CompanyDeploymentStateV3>(_deploymentsByCompanyId.Values).AsReadOnly();
    }

    public void Clear()
    {
        _deploymentsByCompanyId.Clear();
        _companyIdsBySlot.Clear();
        _companyIdsByFormationCell.Clear();
        _companyIdsByAnchorCell.Clear();
    }
}
