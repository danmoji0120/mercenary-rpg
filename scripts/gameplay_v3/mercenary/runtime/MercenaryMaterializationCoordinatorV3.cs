using System;
using System.Collections.Generic;
using GameplayV3.Deployment;
using Godot;
using WorldV2;

namespace GameplayV3.Mercenary.Runtime;

public sealed class MercenaryMaterializationResultV3
{
    public MercenaryMaterializationResultV3(
        bool succeeded,
        int createdViewCount,
        int reusedViewCount,
        int deploymentMismatchCount,
        IReadOnlyList<string> createdMercenaryIds,
        string failureReason)
    {
        Succeeded = succeeded;
        CreatedViewCount = createdViewCount;
        ReusedViewCount = reusedViewCount;
        DeploymentMismatchCount = deploymentMismatchCount;
        CreatedMercenaryIds = new List<string>(createdMercenaryIds).AsReadOnly();
        FailureReason = failureReason;
    }

    public bool Succeeded { get; }
    public int CreatedViewCount { get; }
    public int ReusedViewCount { get; }
    public int DeploymentMismatchCount { get; }
    public IReadOnlyList<string> CreatedMercenaryIds { get; }
    public string FailureReason { get; }
}

public sealed class MercenaryMaterializationCoordinatorV3
{
    public MercenaryMaterializationResultV3 MaterializeCompany(
        string companyId,
        CompanyDeploymentStateV3 deployment,
        MercenarySessionV3 mercenarySession,
        Node2D container,
        WorldV2GridRenderer gridRenderer,
        MercenaryViewRegistryV3 viewRegistry)
    {
        viewRegistry.ClearInvalidViews();
        IReadOnlyList<string> mercenaryIds = mercenarySession.Registry.GetMercenariesByCompany(companyId);
        List<string> createdIds = new();
        int reusedCount = 0;
        int mismatchCount = 0;
        string failureReason = string.Empty;

        foreach (string mercenaryId in mercenaryIds)
        {
            if (!mercenarySession.Registry.TryGetState(mercenaryId, out MercenaryStateV3? state) || state == null)
            {
                failureReason = $"Mercenary state is missing: {mercenaryId}";
                break;
            }

            if (!mercenarySession.Registry.TryGetProfile(mercenaryId, out MercenaryProfileV3? profile)
                || profile == null
                || state.CompanyId != companyId
                || (profile.IsInitialSquadMember
                    && (!profile.InitialSquadSlotIndex.HasValue
                        || deployment.FormationCells[profile.InitialSquadSlotIndex.Value].Value != state.CurrentCell.Value)))
            {
                mismatchCount++;
                failureReason = $"Mercenary deployment mismatch: {mercenaryId}";
                continue;
            }

            if (viewRegistry.TryGetView(mercenaryId, out MercenaryEntityV3? existingView) && existingView != null)
            {
                if (!existingView.TryInitialize(mercenaryId, mercenarySession.Registry, gridRenderer, out failureReason))
                {
                    break;
                }

                reusedCount++;
                continue;
            }

            MercenaryEntityV3 view = new()
            {
                Name = $"Mercenary_{ShortId(mercenaryId)}"
            };
            container.AddChild(view);
            if (!view.TryInitialize(mercenaryId, mercenarySession.Registry, gridRenderer, out failureReason)
                || !viewRegistry.TryRegisterView(mercenaryId, view, out failureReason))
            {
                view.QueueFree();
                break;
            }

            createdIds.Add(mercenaryId);
        }

        int materializedCompanyViewCount = 0;
        foreach (string mercenaryId in mercenaryIds)
        {
            if (viewRegistry.ContainsView(mercenaryId))
            {
                materializedCompanyViewCount++;
            }
        }

        bool succeeded = string.IsNullOrEmpty(failureReason)
            && mismatchCount == 0
            && materializedCompanyViewCount == mercenaryIds.Count;
        return new MercenaryMaterializationResultV3(
            succeeded,
            createdIds.Count,
            reusedCount,
            mismatchCount,
            createdIds,
            failureReason);
    }

    private static string ShortId(string mercenaryId)
    {
        return mercenaryId.Length <= 13 ? mercenaryId : mercenaryId[..13];
    }
}
