using System;
using System.Collections.Generic;
using GameplayV3.Company;
using Godot;
using WorldV2;

namespace GameplayV3.Deployment;

public static class StartingDeploymentPlacementServiceV3
{
    private enum CandidateRejection
    {
        None,
        Bounds,
        Feature,
        Formation
    }

    private static readonly Vector2I[] SlotDirections =
    {
        Vector2I.Left,
        Vector2I.Right,
        Vector2I.Up,
        Vector2I.Down
    };

    private static readonly Vector2I[] CardinalDirections =
    {
        Vector2I.Left,
        Vector2I.Right,
        Vector2I.Up,
        Vector2I.Down,
        new(-1, -1),
        new(1, -1),
        new(1, 1),
        new(-1, 1)
    };

    public static StartingDeploymentPlacementResultV3 AssignMissingDeployments(
        CompanySessionV3 companySession,
        StartingDeploymentWorldQueryV3 worldQuery,
        StartingDeploymentPlacementSettingsV3 settings)
    {
        List<CompanyStateV3> companies = new(companySession.CompanyRegistry.GetAllCompanies());
        companies.Sort(CompareCompanies);

        int assignedBefore = companySession.DeploymentRegistry.Count;
        List<CompanyStateV3> unassignedCompanies = new();
        foreach (CompanyStateV3 company in companies)
        {
            if (!companySession.DeploymentRegistry.ContainsDeployment(company.CompanyId))
            {
                unassignedCompanies.Add(company);
            }
        }

        GlobalCellCoord? arrivalAnchor = GetExistingArrivalAnchor(companySession, worldQuery.StartingSettlement.Id);
        int attempts = 0;
        int rejectedBounds = 0;
        int rejectedFeature = 0;
        int rejectedFormation = 0;
        string failureReason = string.Empty;

        if (!arrivalAnchor.HasValue)
        {
            if (!TryResolveArrivalAnchor(
                    worldQuery,
                    settings,
                    out GlobalCellCoord resolvedAnchor,
                    out attempts,
                    out rejectedBounds,
                    out rejectedFeature,
                    out rejectedFormation))
            {
                failureReason = "No safe arrival anchor was found near the StartingSettlement within the candidate limit.";
                return BuildResult(
                    worldQuery,
                    isInitialized: true,
                    arrivalAnchorCell: null,
                    attempts,
                    rejectedBounds,
                    rejectedFeature,
                    rejectedFormation,
                    assignedBefore,
                    companies.Count - assignedBefore,
                    Array.Empty<string>(),
                    failureReason);
            }

            arrivalAnchor = resolvedAnchor;
        }

        List<string> newlyAssignedCompanyIds = new();
        foreach (CompanyStateV3 company in unassignedCompanies)
        {
            int slotIndex = GetLowestAvailableSlot(companySession.DeploymentRegistry, settings.MaxCompanyCount);
            if (slotIndex < 0)
            {
                failureReason = $"Starting deployment supports at most {settings.MaxCompanyCount} companies.";
                break;
            }

            GlobalCellCoord deploymentAnchor = new(arrivalAnchor.Value.Value + GetSlotOffset(slotIndex, settings));
            IReadOnlyList<GlobalCellCoord> formationCells = CreateFormationCells(deploymentAnchor, slotIndex, settings);
            if (!TryValidateDeploymentCells(
                    worldQuery,
                    settings,
                    deploymentAnchor,
                    formationCells,
                    out CandidateRejection rejection))
            {
                failureReason = $"Deployment slot {slotIndex} is no longer valid near the arrival anchor.";
                IncrementRejection(rejection, ref rejectedBounds, ref rejectedFeature, ref rejectedFormation);
                break;
            }

            float distanceToSettlement = deploymentAnchor.Value.DistanceTo(worldQuery.StartingSettlement.Center);
            float distanceToRoad = GetNearestRoadDistance(deploymentAnchor.Value, worldQuery.Roads);
            float distanceToFeatureCore = GetNearestUnsafeFeatureCoreDistance(
                worldQuery,
                deploymentAnchor.Value,
                settings.FeatureClearance);
            if (!companySession.DeploymentRegistry.TryCreateDeployment(
                    company.CompanyId,
                    worldQuery.StartingSettlement.Id,
                    slotIndex,
                    arrivalAnchor.Value,
                    deploymentAnchor,
                    formationCells,
                    attempts,
                    distanceToSettlement,
                    distanceToRoad,
                    distanceToFeatureCore,
                    out _,
                    out string registerReason))
            {
                failureReason = registerReason;
                break;
            }

            newlyAssignedCompanyIds.Add(company.CompanyId);
        }

        int assignedCompanyCount = companySession.DeploymentRegistry.Count;
        int unassignedCompanyCount = Math.Max(0, companies.Count - assignedCompanyCount);
        return BuildResult(
            worldQuery,
            isInitialized: true,
            arrivalAnchor,
            attempts,
            rejectedBounds,
            rejectedFeature,
            rejectedFormation,
            assignedCompanyCount,
            unassignedCompanyCount,
            newlyAssignedCompanyIds,
            failureReason);
    }

    public static IReadOnlyList<GlobalCellCoord> CreateFormationCells(
        GlobalCellCoord deploymentAnchor,
        int deploymentSlotIndex,
        StartingDeploymentPlacementSettingsV3 settings)
    {
        int spacing = settings.FormationCellSpacing;
        Vector2I anchor = deploymentAnchor.Value;
        return deploymentSlotIndex switch
        {
            0 => new[]
            {
                new GlobalCellCoord(anchor + new Vector2I(-spacing, 0)),
                new GlobalCellCoord(anchor + new Vector2I(spacing, -spacing)),
                new GlobalCellCoord(anchor + new Vector2I(spacing, spacing))
            },
            1 => new[]
            {
                new GlobalCellCoord(anchor + new Vector2I(spacing, 0)),
                new GlobalCellCoord(anchor + new Vector2I(-spacing, -spacing)),
                new GlobalCellCoord(anchor + new Vector2I(-spacing, spacing))
            },
            2 => new[]
            {
                new GlobalCellCoord(anchor + new Vector2I(0, -spacing)),
                new GlobalCellCoord(anchor + new Vector2I(-spacing, spacing)),
                new GlobalCellCoord(anchor + new Vector2I(spacing, spacing))
            },
            _ => new[]
            {
                new GlobalCellCoord(anchor + new Vector2I(0, spacing)),
                new GlobalCellCoord(anchor + new Vector2I(-spacing, -spacing)),
                new GlobalCellCoord(anchor + new Vector2I(spacing, -spacing))
            }
        };
    }

    private static bool TryResolveArrivalAnchor(
        StartingDeploymentWorldQueryV3 worldQuery,
        StartingDeploymentPlacementSettingsV3 settings,
        out GlobalCellCoord arrivalAnchor,
        out int attempts,
        out int rejectedBounds,
        out int rejectedFeature,
        out int rejectedFormation)
    {
        attempts = 0;
        rejectedBounds = 0;
        rejectedFeature = 0;
        rejectedFormation = 0;

        foreach (Vector2I candidate in EnumerateArrivalCandidates(worldQuery, settings))
        {
            if (attempts >= settings.ArrivalCandidateAttemptLimit)
            {
                break;
            }

            attempts++;
            if (!TryValidateArrivalAnchor(worldQuery, settings, candidate, out CandidateRejection rejection))
            {
                IncrementRejection(rejection, ref rejectedBounds, ref rejectedFeature, ref rejectedFormation);
                continue;
            }

            arrivalAnchor = new GlobalCellCoord(candidate);
            return true;
        }

        arrivalAnchor = default;
        return false;
    }

    private static IEnumerable<Vector2I> EnumerateArrivalCandidates(
        StartingDeploymentWorldQueryV3 worldQuery,
        StartingDeploymentPlacementSettingsV3 settings)
    {
        VillageSiteV2 settlement = worldQuery.StartingSettlement;
        if (settings.AllowInsideSettlement)
        {
            yield return settlement.Center;
        }

        if (!settings.AllowSettlementEdgeFallback)
        {
            yield break;
        }

        int insideRadius = Math.Max(2, Mathf.RoundToInt(settlement.Radius) - settings.SettlementBoundaryInnerOffset);
        int outsideRadius = Mathf.RoundToInt(settlement.Radius) + settings.SettlementBoundaryOuterOffset;
        int directionOffset = GetStableDirectionOffset(worldQuery.WorldSeed, settlement.Id);
        for (int index = 0; index < CardinalDirections.Length; index++)
        {
            Vector2I direction = CardinalDirections[(index + directionOffset) % CardinalDirections.Length];
            yield return settlement.Center + direction * insideRadius;
        }

        for (int index = 0; index < CardinalDirections.Length; index++)
        {
            Vector2I direction = CardinalDirections[(index + directionOffset) % CardinalDirections.Length];
            yield return settlement.Center + direction * outsideRadius;
        }

        if (!settings.AllowInsideSettlement)
        {
            yield return settlement.Center;
        }
    }

    private static bool TryValidateArrivalAnchor(
        StartingDeploymentWorldQueryV3 worldQuery,
        StartingDeploymentPlacementSettingsV3 settings,
        Vector2I candidate,
        out CandidateRejection rejection)
    {
        if (!TryValidateCell(worldQuery, settings, candidate, out rejection))
        {
            return false;
        }

        HashSet<Vector2I> allFormationCells = new();
        for (int slotIndex = 0; slotIndex < settings.MaxCompanyCount; slotIndex++)
        {
            GlobalCellCoord deploymentAnchor = new(candidate + GetSlotOffset(slotIndex, settings));
            IReadOnlyList<GlobalCellCoord> formationCells = CreateFormationCells(deploymentAnchor, slotIndex, settings);
            if (!TryValidateDeploymentCells(worldQuery, settings, deploymentAnchor, formationCells, out rejection))
            {
                return false;
            }

            foreach (GlobalCellCoord formationCell in formationCells)
            {
                if (!allFormationCells.Add(formationCell.Value))
                {
                    rejection = CandidateRejection.Formation;
                    return false;
                }
            }
        }

        rejection = CandidateRejection.None;
        return true;
    }

    private static bool TryValidateDeploymentCells(
        StartingDeploymentWorldQueryV3 worldQuery,
        StartingDeploymentPlacementSettingsV3 settings,
        GlobalCellCoord deploymentAnchor,
        IReadOnlyList<GlobalCellCoord> formationCells,
        out CandidateRejection rejection)
    {
        if (!TryValidateCell(worldQuery, settings, deploymentAnchor.Value, out rejection))
        {
            return false;
        }

        if (formationCells.Count != settings.MercenaryCountPerCompany)
        {
            rejection = CandidateRejection.Formation;
            return false;
        }

        HashSet<Vector2I> uniqueCells = new();
        foreach (GlobalCellCoord formationCell in formationCells)
        {
            if (!uniqueCells.Add(formationCell.Value)
                || !TryValidateCell(worldQuery, settings, formationCell.Value, out rejection))
            {
                if (rejection == CandidateRejection.None)
                {
                    rejection = CandidateRejection.Formation;
                }

                return false;
            }
        }

        rejection = CandidateRejection.None;
        return true;
    }

    private static bool TryValidateCell(
        StartingDeploymentWorldQueryV3 worldQuery,
        StartingDeploymentPlacementSettingsV3 settings,
        Vector2I cell,
        out CandidateRejection rejection)
    {
        if (!IsWithinSafeWorldBounds(worldQuery.WorldBounds, cell, settings.WorldEdgeMargin))
        {
            rejection = CandidateRejection.Bounds;
            return false;
        }

        FlatlandCellSampleV2 sample = worldQuery.SampleCell(cell);
        if (!sample.IsWalkable || (sample.IsRiver && !sample.IsBridgeCandidate))
        {
            rejection = CandidateRejection.Formation;
            return false;
        }

        if (sample.IsDungeonEntrance
            || sample.IsBanditCamp
            || sample.IsFactionOutpost
            || sample.IsQuarry
            || sample.LandmarkKind == LandmarkKindV2.Ruin
            || sample.IsDenseForest
            || sample.ForestStrength >= 0.70f
            || IsInsideUnsafeFeatureCore(worldQuery, cell, settings.FeatureClearance))
        {
            rejection = CandidateRejection.Feature;
            return false;
        }

        rejection = CandidateRejection.None;
        return true;
    }

    private static bool IsInsideUnsafeFeatureCore(
        StartingDeploymentWorldQueryV3 worldQuery,
        Vector2I cell,
        int clearance)
    {
        float distance = GetNearestUnsafeFeatureCoreDistance(worldQuery, cell, clearance);
        return distance >= 0.0f && distance <= 0.0f;
    }

    public static float GetNearestUnsafeFeatureCoreDistance(
        StartingDeploymentWorldQueryV3 worldQuery,
        Vector2I cell,
        int clearance)
    {
        float bestDistance = float.MaxValue;
        Vector2 point = CellCenter(cell);
        foreach (RuinSiteV3 ruin in worldQuery.Ruins)
        {
            bestDistance = Mathf.Min(bestDistance, point.DistanceTo(ruin.Center) - (ruin.ApproxRadius * 0.60f + clearance));
        }

        foreach (QuarryRegionV3 quarry in worldQuery.Quarries)
        {
            bestDistance = Mathf.Min(bestDistance, point.DistanceTo(quarry.Center) - (quarry.ApproxRadius * 0.52f + clearance));
        }

        foreach (DungeonEntranceSiteV3 dungeon in worldQuery.Dungeons)
        {
            bestDistance = Mathf.Min(bestDistance, point.DistanceTo(dungeon.Center) - (dungeon.ApproxRadius * 0.50f + clearance));
        }

        foreach (BanditCampSiteV3 camp in worldQuery.BanditCamps)
        {
            bestDistance = Mathf.Min(bestDistance, point.DistanceTo(camp.Center) - (camp.ApproxRadius * 0.56f + clearance));
        }

        foreach (FactionOutpostSiteV3 outpost in worldQuery.FactionOutposts)
        {
            bestDistance = Mathf.Min(bestDistance, point.DistanceTo(outpost.Center) - (outpost.ApproxRadius * 0.54f + clearance));
        }

        return bestDistance == float.MaxValue ? -1.0f : bestDistance;
    }

    private static bool IsWithinSafeWorldBounds(Rect2I worldBounds, Vector2I cell, int margin)
    {
        return cell.X >= worldBounds.Position.X + margin
            && cell.Y >= worldBounds.Position.Y + margin
            && cell.X < worldBounds.End.X - margin
            && cell.Y < worldBounds.End.Y - margin;
    }

    private static Vector2 CellCenter(Vector2I cell)
    {
        return new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
    }

    private static GlobalCellCoord? GetExistingArrivalAnchor(CompanySessionV3 companySession, int startingSettlementId)
    {
        StartingDeploymentPlacementResultV3? existingResult = companySession.StartingDeploymentResult;
        if (existingResult?.HasArrivalAnchor == true
            && existingResult.StartingSettlementId == startingSettlementId)
        {
            return existingResult.ArrivalAnchorCell;
        }

        foreach (CompanyDeploymentStateV3 deployment in companySession.DeploymentRegistry.GetAllDeployments())
        {
            if (deployment.StartingSettlementId == startingSettlementId)
            {
                return deployment.ArrivalAnchorCell;
            }
        }

        return null;
    }

    private static int GetLowestAvailableSlot(CompanyDeploymentRegistryV3 registry, int maxCompanyCount)
    {
        for (int slotIndex = 0; slotIndex < maxCompanyCount; slotIndex++)
        {
            if (!registry.ContainsSlot(slotIndex))
            {
                return slotIndex;
            }
        }

        return -1;
    }

    private static Vector2I GetSlotOffset(int slotIndex, StartingDeploymentPlacementSettingsV3 settings)
    {
        return SlotDirections[slotIndex % SlotDirections.Length] * settings.CompanySlotSpacing;
    }

    private static float GetNearestRoadDistance(Vector2I cell, IReadOnlyList<RoadPathV2> roads)
    {
        float bestDistance = float.MaxValue;
        foreach (RoadPathV2 road in roads)
        {
            bestDistance = Mathf.Min(bestDistance, road.DistanceToPath(cell));
        }

        return bestDistance == float.MaxValue ? -1.0f : bestDistance;
    }

    private static int CompareCompanies(CompanyStateV3 left, CompanyStateV3 right)
    {
        int createdComparison = left.CreatedUtc.CompareTo(right.CreatedUtc);
        return createdComparison != 0
            ? createdComparison
            : string.CompareOrdinal(left.CompanyId, right.CompanyId);
    }

    private static int GetStableDirectionOffset(int worldSeed, int settlementId)
    {
        uint hash = 2166136261u;
        hash = (hash ^ (uint)worldSeed) * 16777619u;
        hash = (hash ^ (uint)settlementId) * 16777619u;
        return (int)(hash % CardinalDirections.Length);
    }

    private static void IncrementRejection(
        CandidateRejection rejection,
        ref int rejectedBounds,
        ref int rejectedFeature,
        ref int rejectedFormation)
    {
        switch (rejection)
        {
            case CandidateRejection.Bounds:
                rejectedBounds++;
                break;
            case CandidateRejection.Feature:
                rejectedFeature++;
                break;
            case CandidateRejection.Formation:
                rejectedFormation++;
                break;
        }
    }

    private static StartingDeploymentPlacementResultV3 BuildResult(
        StartingDeploymentWorldQueryV3 worldQuery,
        bool isInitialized,
        GlobalCellCoord? arrivalAnchorCell,
        int attempts,
        int rejectedBounds,
        int rejectedFeature,
        int rejectedFormation,
        int assignedCompanyCount,
        int unassignedCompanyCount,
        IReadOnlyList<string> newlyAssignedCompanyIds,
        string failureReason)
    {
        float distanceToNearestRoad = arrivalAnchorCell.HasValue
            ? GetNearestRoadDistance(arrivalAnchorCell.Value.Value, worldQuery.Roads)
            : float.MaxValue;
        return new StartingDeploymentPlacementResultV3(
            isInitialized,
            worldQuery.StartingSettlement.Id,
            new GlobalCellCoord(worldQuery.StartingSettlement.Center),
            arrivalAnchorCell,
            distanceToNearestRoad,
            attempts,
            rejectedBounds,
            rejectedFeature,
            rejectedFormation,
            assignedCompanyCount,
            unassignedCompanyCount,
            newlyAssignedCompanyIds,
            failureReason);
    }
}
