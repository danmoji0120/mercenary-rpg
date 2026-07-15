using System;
using System.Collections.Generic;
using GameplayV3.Company;
using Godot;
using WorldV2;

namespace GameplayV3.Deployment;

public sealed class StartingDeploymentSelfCheckResultV3
{
    public StartingDeploymentSelfCheckResultV3(bool passed, string summary)
    {
        Passed = passed;
        Summary = summary;
    }

    public bool Passed { get; }
    public string Summary { get; }
}

public static class StartingDeploymentSelfCheckV3
{
    public static StartingDeploymentSelfCheckResultV3 Run()
    {
        List<string> failures = new();
        StartingDeploymentPlacementSettingsV3 settings = StartingDeploymentPlacementSettingsV3.Default;
        StartingDeploymentWorldQueryV3 worldQuery = CreateTestWorldQuery();
        StartingDeploymentCoordinatorV3 coordinator = new(settings);

        CompanySessionV3 session = CreateSession(new[] { 3, 1, 4, 2 });
        Require(coordinator.TryEnsureDeployments(session, worldQuery, out StartingDeploymentPlacementResultV3 result), result.FailureReason, failures);
        Require(result.IsInitialized && result.HasArrivalAnchor, "Arrival anchor was not initialized.", failures);
        Require(session.DeploymentRegistry.Count == 4, "Four companies were not deployed.", failures);
        ValidateDeployments(session.DeploymentRegistry.GetAllDeployments(), worldQuery.WorldBounds, failures);

        Dictionary<string, DeploymentSignature> beforeAddingFifth = CaptureSignatures(session.DeploymentRegistry.GetAllDeployments());
        RegisterCompany(session.CompanyRegistry, 5);
        Require(!coordinator.TryEnsureDeployments(session, worldQuery, out StartingDeploymentPlacementResultV3 fifthResult), "Fifth company was incorrectly deployed.", failures);
        Require(fifthResult.UnassignedCompanyCount == 1, "Fifth company was not reported as unassigned.", failures);
        Require(SignaturesMatch(beforeAddingFifth, CaptureSignatures(session.DeploymentRegistry.GetAllDeployments())), "Existing deployments changed after adding a fifth company.", failures);

        IReadOnlyList<CompanyDeploymentStateV3> initialDeployments = session.DeploymentRegistry.GetAllDeployments();
        if (initialDeployments.Count == 0)
        {
            failures.Add("No deployment was available for the idempotent registration check.");
        }
        else
        {
            CompanyDeploymentStateV3 firstDeployment = initialDeployments[0];
            Require(session.DeploymentRegistry.TryRegisterDeployment(firstDeployment, out string idempotentReason), idempotentReason, failures);
        }

        CompanySessionV3 collisionSession = CreateSession(new[] { 1, 2 });
        CompanyDeploymentStateV3 firstCollisionDeployment = CreateManualDeployment(1, 0, new Vector2I(100, 100));
        Require(collisionSession.DeploymentRegistry.TryRegisterDeployment(firstCollisionDeployment, out string firstCollisionReason), firstCollisionReason, failures);
        CompanyDeploymentStateV3 duplicateSlotDeployment = CreateManualDeployment(2, 0, new Vector2I(120, 100));
        Require(!collisionSession.DeploymentRegistry.TryRegisterDeployment(duplicateSlotDeployment, out _), "Duplicate deployment slot was accepted.", failures);
        CompanyDeploymentStateV3 duplicateFormationDeployment = CreateManualDeployment(2, 1, new Vector2I(100, 100));
        Require(!collisionSession.DeploymentRegistry.TryRegisterDeployment(duplicateFormationDeployment, out _), "Duplicate formation cell was accepted.", failures);
        CompanyDeploymentStateV3 missingCompanyDeployment = CreateManualDeployment(6, 2, new Vector2I(140, 100));
        Require(!collisionSession.DeploymentRegistry.TryRegisterDeployment(missingCompanyDeployment, out _), "Unregistered company deployment was accepted.", failures);
        collisionSession.DeploymentRegistry.Clear();
        Require(collisionSession.DeploymentRegistry.Count == 0, "Deployment registry Clear did not remove deployments.", failures);

        CompanySessionV3 reverseOrderSession = CreateSession(new[] { 4, 2, 1, 3 });
        Require(coordinator.TryEnsureDeployments(reverseOrderSession, worldQuery, out StartingDeploymentPlacementResultV3 reverseResult), reverseResult.FailureReason, failures);
        Require(SignaturesMatch(
            beforeAddingFifth,
            CaptureSignatures(reverseOrderSession.DeploymentRegistry.GetAllDeployments())),
            "Deployment slot assignment depended on dictionary insertion order.",
            failures);

        return failures.Count == 0
            ? new StartingDeploymentSelfCheckResultV3(
                true,
                "PASS: formation validation, registry rejection, idempotency, four-slot stability, fifth-company rejection, and stable ordering.")
            : new StartingDeploymentSelfCheckResultV3(false, "FAIL: " + string.Join(" | ", failures));
    }

    public static bool TryValidateRuntime(
        CompanySessionV3 companySession,
        StartingDeploymentWorldQueryV3 worldQuery,
        StartingDeploymentPlacementResultV3 result,
        out string reason)
    {
        if (!result.HasArrivalAnchor || !result.IsInitialized)
        {
            reason = "Arrival anchor was not initialized.";
            return false;
        }

        if (!companySession.LocalContext.TryGetLocalCompanyId(out string localCompanyId, out reason)
            || !companySession.DeploymentRegistry.TryGetDeployment(localCompanyId, out CompanyDeploymentStateV3? deployment)
            || deployment == null)
        {
            reason = string.IsNullOrEmpty(reason) ? "Local company deployment is missing." : reason;
            return false;
        }

        List<string> failures = new();
        ValidateDeployments(new[] { deployment }, worldQuery.WorldBounds, failures);
        foreach (GlobalCellCoord formationCell in deployment.FormationCells)
        {
            FlatlandCellSampleV2 sample = worldQuery.SampleCell(formationCell.Value);
            if (!sample.IsWalkable
                || sample.IsDungeonEntrance
                || sample.IsBanditCamp
                || sample.IsFactionOutpost
                || sample.IsQuarry
                || sample.LandmarkKind == LandmarkKindV2.Ruin
                || sample.IsDenseForest)
            {
                failures.Add($"Unsafe runtime formation cell: {formationCell}.");
            }
        }

        reason = failures.Count == 0 ? string.Empty : string.Join(" | ", failures);
        return failures.Count == 0;
    }

    private static CompanySessionV3 CreateSession(IReadOnlyList<int> companyNumbers)
    {
        CompanySessionV3 session = new();
        foreach (int companyNumber in companyNumbers)
        {
            RegisterCompany(session.CompanyRegistry, companyNumber);
        }

        return session;
    }

    private static void RegisterCompany(CompanyRegistryV3 registry, int number)
    {
        CompanyStateV3 company = new(
            StableId("cmp_", number),
            StableId("ply_", number),
            $"Company {number}",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        if (!registry.TryRegisterCompany(company, out string reason))
        {
            throw new InvalidOperationException(reason);
        }
    }

    private static CompanyDeploymentStateV3 CreateManualDeployment(int companyNumber, int slotIndex, Vector2I anchor)
    {
        IReadOnlyList<GlobalCellCoord> formation = new[]
        {
            new GlobalCellCoord(anchor + new Vector2I(-1, 0)),
            new GlobalCellCoord(anchor + new Vector2I(1, -1)),
            new GlobalCellCoord(anchor + new Vector2I(1, 1))
        };
        return new CompanyDeploymentStateV3(
            StableId("cmp_", companyNumber),
            77,
            slotIndex,
            new GlobalCellCoord(128, 128),
            new GlobalCellCoord(anchor),
            formation,
            DateTime.UtcNow,
            1,
            10.0f,
            0.0f,
            -1.0f);
    }

    private static StartingDeploymentWorldQueryV3 CreateTestWorldQuery()
    {
        VillageSiteV2 settlement = new()
        {
            Id = 77,
            Center = new Vector2I(128, 128),
            Radius = 25.0f,
            IsStartingVillage = true,
            Role = SettlementRoleV3.StartingSettlement
        };
        return new StartingDeploymentWorldQueryV3(
            20260710,
            new Rect2I(0, 0, 256, 256),
            settlement,
            Array.Empty<RoadPathV2>(),
            Array.Empty<RuinSiteV3>(),
            Array.Empty<QuarryRegionV3>(),
            Array.Empty<DungeonEntranceSiteV3>(),
            Array.Empty<BanditCampSiteV3>(),
            Array.Empty<FactionOutpostSiteV3>(),
            cell => new FlatlandCellSampleV2
            {
                GlobalCellCoord = cell,
                IsWalkable = true,
                IsVillage = true,
                IsStartingVillage = true
            });
    }

    private static void ValidateDeployments(
        IReadOnlyList<CompanyDeploymentStateV3> deployments,
        Rect2I worldBounds,
        ICollection<string> failures)
    {
        HashSet<Vector2I> formationCells = new();
        HashSet<int> slots = new();
        foreach (CompanyDeploymentStateV3 deployment in deployments)
        {
            Require(deployment.HasDeployment, "Deployment state was not marked as assigned.", failures);
            Require(deployment.FormationCells.Count == 3, "Formation did not contain exactly three cells.", failures);
            Require(slots.Add(deployment.DeploymentSlotIndex), "Deployment slots overlapped.", failures);
            foreach (GlobalCellCoord formationCell in deployment.FormationCells)
            {
                Require(worldBounds.HasPoint(formationCell.Value), "Formation cell was outside world bounds.", failures);
                Require(formationCells.Add(formationCell.Value), "Formation cells overlapped.", failures);
            }
        }
    }

    private static Dictionary<string, DeploymentSignature> CaptureSignatures(IReadOnlyList<CompanyDeploymentStateV3> deployments)
    {
        Dictionary<string, DeploymentSignature> signatures = new(StringComparer.Ordinal);
        foreach (CompanyDeploymentStateV3 deployment in deployments)
        {
            signatures.Add(deployment.CompanyId, new DeploymentSignature(
                deployment.DeploymentSlotIndex,
                deployment.DeploymentAnchorCell.Value,
                deployment.FormationCells[0].Value,
                deployment.FormationCells[1].Value,
                deployment.FormationCells[2].Value));
        }

        return signatures;
    }

    private static bool SignaturesMatch(
        IReadOnlyDictionary<string, DeploymentSignature> expected,
        IReadOnlyDictionary<string, DeploymentSignature> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        foreach ((string companyId, DeploymentSignature expectedSignature) in expected)
        {
            if (!actual.TryGetValue(companyId, out DeploymentSignature actualSignature)
                || !expectedSignature.Equals(actualSignature))
            {
                return false;
            }
        }

        return true;
    }

    private static string StableId(string prefix, int number)
    {
        return prefix + number.ToString("x32");
    }

    private static void Require(bool condition, string failure, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(string.IsNullOrEmpty(failure) ? "Unspecified deployment self-check failure." : failure);
        }
    }

    private readonly struct DeploymentSignature : IEquatable<DeploymentSignature>
    {
        public DeploymentSignature(int slot, Vector2I anchor, Vector2I first, Vector2I second, Vector2I third)
        {
            Slot = slot;
            Anchor = anchor;
            First = first;
            Second = second;
            Third = third;
        }

        private int Slot { get; }
        private Vector2I Anchor { get; }
        private Vector2I First { get; }
        private Vector2I Second { get; }
        private Vector2I Third { get; }

        public bool Equals(DeploymentSignature other)
        {
            return Slot == other.Slot
                && Anchor == other.Anchor
                && First == other.First
                && Second == other.Second
                && Third == other.Third;
        }
    }
}
