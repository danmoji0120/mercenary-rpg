using System;
using System.Collections.Generic;

namespace GameplayV3.Company;

public sealed class CompanyCoreSelfCheckResultV3
{
    public CompanyCoreSelfCheckResultV3(bool passed, string summary)
    {
        Passed = passed;
        Summary = summary;
    }

    public bool Passed { get; }
    public string Summary { get; }
}

public static class CompanyCoreSelfCheckV3
{
    public static CompanyCoreSelfCheckResultV3 Run()
    {
        List<string> failures = new();
        CompanyRegistryV3 registry = new();

        PlayerIdentityV3 playerA = CreatePlayer("Player A", isLocal: true);
        PlayerIdentityV3 playerB = CreatePlayer("Player B", isLocal: false);
        PlayerIdentityV3 playerC = CreatePlayer("Player C", isLocal: false);

        Require(registry.TryCreateCompany(playerA.PlayerId, "Company A", out CompanyStateV3? companyA, out string reasonA), reasonA, failures);
        Require(registry.TryCreateCompany(playerB.PlayerId, "Company B", out CompanyStateV3? companyB, out string reasonB), reasonB, failures);
        Require(registry.TryCreateCompany(playerC.PlayerId, "Company C", out CompanyStateV3? companyC, out string reasonC), reasonC, failures);

        if (companyA == null || companyB == null || companyC == null)
        {
            failures.Add("Failed to create all three baseline companies.");
            return BuildResult(failures);
        }

        Require(registry.Count == 3, "Registry did not contain three companies.", failures);
        Require(IsOwnerLookupCorrect(registry, playerA.PlayerId, companyA.CompanyId), "Player A owner lookup failed.", failures);
        Require(IsOwnerLookupCorrect(registry, playerB.PlayerId, companyB.CompanyId), "Player B owner lookup failed.", failures);
        Require(IsOwnerLookupCorrect(registry, playerC.PlayerId, companyC.CompanyId), "Player C owner lookup failed.", failures);
        Require(CompanyOwnershipRulesV3.CanPlayerControlCompany(playerA.PlayerId, companyA.CompanyId, registry), "Player A could not control Company A.", failures);
        Require(!CompanyOwnershipRulesV3.CanPlayerControlCompany(playerA.PlayerId, companyB.CompanyId, registry), "Player A incorrectly controlled Company B.", failures);

        bool duplicateOwnerAccepted = registry.TryCreateCompany(
            playerA.PlayerId,
            "Duplicate Owner Company",
            out _,
            out _);
        Require(!duplicateOwnerAccepted, "Duplicate owner registration was accepted.", failures);

        PlayerIdentityV3 duplicateIdOwner = CreatePlayer("Duplicate ID Owner", isLocal: false);
        CompanyStateV3 duplicateIdCompany = new(
            companyA.CompanyId,
            duplicateIdOwner.PlayerId,
            "Duplicate ID Company",
            DateTime.UtcNow);
        Require(!registry.TryRegisterCompany(duplicateIdCompany, out _), "Duplicate CompanyId registration was accepted.", failures);

        LocalPlayerCompanyContextV3 localContext = new(registry);
        Require(localContext.InitializeLocalPlayer(playerA, out string localPlayerReason), localPlayerReason, failures);
        Require(localContext.AssignLocalCompany(companyA.CompanyId, out string localCompanyReason), localCompanyReason, failures);
        Require(localContext.IsLocalCompany(companyA.CompanyId), "Local Company A check failed.", failures);
        Require(!localContext.IsLocalCompany(companyB.CompanyId), "Company B was incorrectly treated as local.", failures);
        Require(localContext.CanLocalPlayerControl(companyA.CompanyId), "Local player could not control Company A.", failures);
        Require(!localContext.CanLocalPlayerControl(companyB.CompanyId), "Local player incorrectly controlled Company B.", failures);

        Require(registry.TryRemoveCompany(companyC.CompanyId, out string removeReason), removeReason, failures);
        Require(!registry.TryGetCompany(companyC.CompanyId, out _), "Removed Company C was still retrievable.", failures);
        Require(!registry.TryRemoveCompany(companyC.CompanyId, out _), "Missing Company C removal was reported as successful.", failures);

        return BuildResult(failures);
    }

    private static PlayerIdentityV3 CreatePlayer(string displayName, bool isLocal)
    {
        return new PlayerIdentityV3(
            CompanyIdFactoryV3.CreatePlayerId(),
            displayName,
            DateTime.UtcNow,
            isLocal);
    }

    private static bool IsOwnerLookupCorrect(CompanyRegistryV3 registry, string playerId, string companyId)
    {
        return registry.TryGetCompanyByOwner(playerId, out CompanyStateV3? company)
            && company?.CompanyId == companyId;
    }

    private static void Require(bool condition, string failure, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(string.IsNullOrWhiteSpace(failure) ? "Unspecified self-check failure." : failure);
        }
    }

    private static CompanyCoreSelfCheckResultV3 BuildResult(IReadOnlyCollection<string> failures)
    {
        if (failures.Count == 0)
        {
            return new CompanyCoreSelfCheckResultV3(
                true,
                "PASS: 3 players/companies, owner lookup, ownership isolation, duplicate rejection, removal, and local-company checks.");
        }

        return new CompanyCoreSelfCheckResultV3(false, "FAIL: " + string.Join(" | ", failures));
    }
}
