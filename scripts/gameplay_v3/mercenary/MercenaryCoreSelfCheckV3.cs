using System;
using System.Collections.Generic;
using GameplayV3.Company;
using GameplayV3.Deployment;
using Godot;
using WorldV2;

namespace GameplayV3.Mercenary;

public sealed class MercenaryCoreSelfCheckResultV3
{
    public MercenaryCoreSelfCheckResultV3(bool passed, string summary)
    {
        Passed = passed;
        Summary = summary;
    }

    public bool Passed { get; }
    public string Summary { get; }
}

public static class MercenaryCoreSelfCheckV3
{
    public static MercenaryCoreSelfCheckResultV3 Run()
    {
        List<string> failures = new();
        TestIds(failures);
        TestValueSetsAndDerivedStats(failures);
        TestRegistryAndInitialSquads(failures);

        return failures.Count == 0
            ? new MercenaryCoreSelfCheckResultV3(
                true,
                "PASS: IDs, value ranges, multipliers, registry atomicity, initial-squad idempotency, ownership, and unrestricted company rosters.")
            : new MercenaryCoreSelfCheckResultV3(false, "FAIL: " + string.Join(" | ", failures));
    }

    private static void TestIds(ICollection<string> failures)
    {
        string mercenaryId = MercenaryIdFactoryV3.CreateMercenaryId();
        Require(MercenaryIdFactoryV3.IsValidMercenaryId(mercenaryId), "Generated MercenaryId was invalid.", failures);
        Require(!MercenaryIdFactoryV3.IsValidMercenaryId("cmp_00000000000000000000000000000001"), "Wrong MercenaryId prefix was accepted.", failures);
        Require(!MercenaryIdFactoryV3.IsValidMercenaryId("merc_"), "Empty MercenaryId body was accepted.", failures);
    }

    private static void TestValueSetsAndDerivedStats(ICollection<string> failures)
    {
        Require(MercenaryAttributeSetV3.TryCreate(0, 20, 10, 10, 10, out MercenaryAttributeSetV3? attributes, out string attributeReason), attributeReason, failures);
        Require(attributes?.GetValue(MercenaryAttributeTypeV3.Strength) == 0
            && attributes.GetValue(MercenaryAttributeTypeV3.Agility) == 20
            && attributes.GetValue(MercenaryAttributeTypeV3.Endurance) == 10
            && attributes.GetValue(MercenaryAttributeTypeV3.Intelligence) == 10
            && attributes.GetValue(MercenaryAttributeTypeV3.Mental) == 10,
            "Attribute lookup failed.", failures);
        Require(!MercenaryAttributeSetV3.TryCreate(-1, 10, 10, 10, 10, out _, out _), "Attribute -1 was accepted.", failures);
        Require(!MercenaryAttributeSetV3.TryCreate(21, 10, 10, 10, 10, out _, out _), "Attribute 21 was accepted.", failures);

        Require(MercenaryWorkSkillSetV3.TryCreate(0, 20, 10, 9, 8, 7, 6, out MercenaryWorkSkillSetV3? skills, out string skillReason), skillReason, failures);
        Require(skills?.GetValue(MercenaryWorkSkillTypeV3.Hauling) == 0
            && skills.GetValue(MercenaryWorkSkillTypeV3.Construction) == 20
            && skills.GetTopSkills(2)[0].Type == MercenaryWorkSkillTypeV3.Construction,
            "Work skill lookup/top-skill ordering failed.", failures);
        Require(!MercenaryWorkSkillSetV3.TryCreate(-1, 0, 0, 0, 0, 0, 0, out _, out _), "Work skill -1 was accepted.", failures);
        Require(!MercenaryWorkSkillSetV3.TryCreate(21, 0, 0, 0, 0, 0, 0, out _, out _), "Work skill 21 was accepted.", failures);

        MercenaryProfileV3 low = CreateProfile(1, "Low", 0, 0, 0, 0, 0, failures);
        MercenaryProfileV3 average = CreateProfile(2, "Average", 10, 10, 10, 10, 10, failures);
        MercenaryProfileV3 high = CreateProfile(3, "High", 20, 20, 20, 20, 20, failures);
        MercenaryDerivedStatsV3 lowStats = MercenaryDerivedStatsCalculatorV3.Calculate(low);
        MercenaryDerivedStatsV3 averageStats = MercenaryDerivedStatsCalculatorV3.Calculate(average);
        MercenaryDerivedStatsV3 highStats = MercenaryDerivedStatsCalculatorV3.Calculate(high);
        Require(lowStats.MoveSpeedMultiplier >= 0.90f && highStats.MoveSpeedMultiplier <= 1.35f, "Move multiplier range was unreasonable.", failures);
        Require(lowStats.CarryCapacity < averageStats.CarryCapacity && averageStats.CarryCapacity < highStats.CarryCapacity, "Carry capacity did not increase with Strength/Endurance.", failures);
        Require(MercenaryDerivedStatsCalculatorV3.Calculate(average).WorkSpeedMultiplier == averageStats.WorkSpeedMultiplier, "Derived stats were not deterministic.", failures);
        Require(MathF.Abs(averageStats.MoveSpeedMultiplier - 1.10f) < 0.0001f
            && MathF.Abs(averageStats.WorkSpeedMultiplier - 1.00f) < 0.0001f
            && MathF.Abs(averageStats.CarryCapacity - 13.0f) < 0.0001f,
            "Multiplier formulas changed.", failures);
    }

    private static void TestRegistryAndInitialSquads(ICollection<string> failures)
    {
        CompanySessionV3 companySession = new();
        for (int companyNumber = 1; companyNumber <= 4; companyNumber++)
        {
            RegisterCompanyAndDeployment(companySession, companyNumber, failures);
        }

        MercenarySessionV3 mercenarySession = new(companySession.CompanyRegistry);
        Rect2I worldBounds = new(0, 0, 512, 512);
        Dictionary<string, IReadOnlyList<string>> initialIdsByCompany = new(StringComparer.Ordinal);
        for (int companyNumber = 1; companyNumber <= 4; companyNumber++)
        {
            string companyId = StableId("cmp_", companyNumber);
            InitialSquadCreationResultV3 result = mercenarySession.CreateOrReuseInitialSquad(
                companyId,
                companySession.DeploymentRegistry,
                worldBounds);
            Require(result.Succeeded && !result.ReusedExisting && result.MercenaryIds.Count == 3, result.FailureReason, failures);
            initialIdsByCompany[companyId] = result.MercenaryIds;
            ValidateSquad(companyId, companySession, mercenarySession, result.MercenaryIds, failures);
        }

        Require(mercenarySession.Registry.Count == 12, "Four companies did not create 12 initial mercenaries.", failures);
        string companyA = StableId("cmp_", 1);
        InitialSquadCreationResultV3 reused = mercenarySession.CreateOrReuseInitialSquad(companyA, companySession.DeploymentRegistry, worldBounds);
        Require(reused.Succeeded && reused.ReusedExisting && SameIds(initialIdsByCompany[companyA], reused.MercenaryIds), "Initial squad idempotency failed.", failures);

        for (int index = 0; index < 10; index++)
        {
            CreateGeneralMercenary(companyA, 100 + index, mercenarySession.Registry, failures);
            Require(mercenarySession.Registry.CountByCompany(companyA) == 4 + index, $"Company roster could not register mercenary {4 + index}.", failures);
        }

        Require(mercenarySession.Registry.CountByCompany(companyA) == 13, "Company could not register ten general mercenaries after the initial squad.", failures);
        foreach (string initialId in initialIdsByCompany[companyA])
        {
            Require(mercenarySession.Registry.TryGetProfile(initialId, out MercenaryProfileV3? initialProfile)
                && initialProfile != null
                && initialProfile.IsInitialSquadMember
                && initialProfile.InitialSquadSlotIndex is >= 0 and <= 2,
                "Initial squad metadata was not retained after general mercenary registration.", failures);
        }

        InitialSquadCreationResultV3 reusedAfterGeneralMembers = mercenarySession.CreateOrReuseInitialSquad(companyA, companySession.DeploymentRegistry, worldBounds);
        Require(reusedAfterGeneralMembers.Succeeded
            && reusedAfterGeneralMembers.ReusedExisting
            && SameIds(initialIdsByCompany[companyA], reusedAfterGeneralMembers.MercenaryIds),
            "Initial squad was not reusable after general mercenary registration.", failures);
        Require(mercenarySession.Registry.Count == 22, "Four initial squads plus ten general mercenaries were not registered.", failures);

        string mercenaryA = initialIdsByCompany[companyA][0];
        Require(mercenarySession.Registry.TryGetMercenary(mercenaryA, out MercenaryProfileV3? profileA, out MercenaryStateV3? stateA)
            && profileA != null && stateA != null, "Registered Profile/State pair lookup failed.", failures);
        Require(!mercenarySession.Registry.TryRegisterMercenary(profileA, stateA, out _), "Duplicate MercenaryId was accepted.", failures);
        Require(mercenarySession.Registry.DuplicateMercenaryRejectedCount > 0, "Duplicate rejection counter did not increment.", failures);
        Require(mercenarySession.CanPlayerControlMercenary(StableId("ply_", 1), mercenaryA), "Owner could not control own mercenary.", failures);
        Require(!mercenarySession.CanPlayerControlMercenary(StableId("ply_", 2), mercenaryA), "Other player controlled foreign mercenary.", failures);

        CompanySessionV3 inconsistentCompanySession = new();
        RegisterCompanyAndDeployment(inconsistentCompanySession, 7, failures);
        MercenarySessionV3 inconsistentMercenarySession = new(inconsistentCompanySession.CompanyRegistry);
        string inconsistentCompanyId = StableId("cmp_", 7);
        CreateSingleMercenary(inconsistentCompanyId, inconsistentMercenarySession.Registry, failures);
        InitialSquadCreationResultV3 inconsistent = inconsistentMercenarySession.CreateOrReuseInitialSquad(
            inconsistentCompanyId,
            inconsistentCompanySession.DeploymentRegistry,
            worldBounds);
        Require(!inconsistent.Succeeded && inconsistentMercenarySession.Registry.Count == 1, "Inconsistent one-member squad was auto-filled or removed.", failures);

        Require(mercenarySession.Registry.TryRemoveMercenary(mercenaryA, out string removeReason), removeReason, failures);
        Require(!mercenarySession.Registry.ContainsMercenary(mercenaryA), "Removed mercenary retained partial Profile/State data.", failures);
        mercenarySession.Registry.Clear();
        Require(mercenarySession.Registry.Count == 0 && mercenarySession.Registry.GetAllMercenaryIds().Count == 0, "MercenaryRegistry Clear failed.", failures);
    }

    private static void ValidateSquad(
        string companyId,
        CompanySessionV3 companySession,
        MercenarySessionV3 mercenarySession,
        IReadOnlyList<string> mercenaryIds,
        ICollection<string> failures)
    {
        companySession.DeploymentRegistry.TryGetDeployment(companyId, out CompanyDeploymentStateV3? deployment);
        bool[] initialSlots = new bool[3];
        HashSet<string> uniqueIds = new(StringComparer.Ordinal);
        foreach (string mercenaryId in mercenaryIds)
        {
            if (!mercenarySession.Registry.TryGetMercenary(mercenaryId, out MercenaryProfileV3? profile, out MercenaryStateV3? state)
                || profile == null || state == null || deployment == null)
            {
                failures.Add("Squad Profile/State/deployment lookup failed.");
                continue;
            }

            Require(uniqueIds.Add(mercenaryId), "Initial squad MercenaryIds were not unique.", failures);
            Require(state.CompanyId == companyId && state.ActivityState == MercenaryActivityStateV3.Idle, "Initial squad ownership/activity was invalid.", failures);
            Require(profile.IsInitialSquadMember && profile.InitialSquadSlotIndex is >= 0 and <= 2, "Initial squad slot metadata was invalid.", failures);
            if (profile.InitialSquadSlotIndex.HasValue)
            {
                initialSlots[profile.InitialSquadSlotIndex.Value] = true;
                Require(state.CurrentCell.Value == deployment.FormationCells[profile.InitialSquadSlotIndex.Value].Value, "CurrentCell did not match FormationCell.", failures);
            }

            Require(profile.CreatedUtc == state.CreatedUtc, "Initial Profile and State CreatedUtc were not atomic.", failures);
        }

        Require(initialSlots[0] && initialSlots[1] && initialSlots[2], "InitialSquadSlotIndex 0/1/2 were not all present.", failures);
    }

    private static void RegisterCompanyAndDeployment(CompanySessionV3 session, int number, ICollection<string> failures)
    {
        CompanyStateV3 company = new(
            StableId("cmp_", number),
            StableId("ply_", number),
            $"Company {number}",
            new DateTime(2026, 1, 1, 0, 0, number, DateTimeKind.Utc));
        Require(session.CompanyRegistry.TryRegisterCompany(company, out string companyReason), companyReason, failures);

        Vector2I anchor = new(80 + number * 50, 100);
        IReadOnlyList<GlobalCellCoord> formationCells = new[]
        {
            new GlobalCellCoord(anchor + new Vector2I(-2, 0)),
            new GlobalCellCoord(anchor + new Vector2I(2, -2)),
            new GlobalCellCoord(anchor + new Vector2I(2, 2))
        };
        Require(session.DeploymentRegistry.TryCreateDeployment(
            company.CompanyId,
            1,
            number - 1,
            new GlobalCellCoord(100, 100),
            new GlobalCellCoord(anchor),
            formationCells,
            1,
            10.0f,
            2.0f,
            50.0f,
            out _,
            out string deploymentReason), deploymentReason, failures);
    }

    private static void CreateSingleMercenary(string companyId, MercenaryRegistryV3 registry, ICollection<string> failures)
    {
        DateTime createdUtc = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        MercenaryProfileV3 profile = CreateProfile(70, "Partial", 10, 10, 10, 10, 10, createdUtc, failures);
        Require(MercenaryStateV3.TryCreate(
            profile.MercenaryId,
            companyId,
            new GlobalCellCoord(100, 100),
            MercenaryActivityStateV3.Idle,
            createdUtc,
            out MercenaryStateV3? state,
            out string stateReason), stateReason, failures);
        Require(registry.TryRegisterMercenary(profile, state, out string registerReason), registerReason, failures);
    }

    private static void CreateGeneralMercenary(string companyId, int idNumber, MercenaryRegistryV3 registry, ICollection<string> failures)
    {
        DateTime createdUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc).AddSeconds(idNumber);
        MercenaryProfileV3 profile = CreateProfile(idNumber, $"General {idNumber}", 10, 10, 10, 10, 10, createdUtc, failures);
        Require(!profile.IsInitialSquadMember && profile.InitialSquadSlotIndex == null, "General mercenary received initial squad metadata.", failures);
        Require(MercenaryStateV3.TryCreate(
            profile.MercenaryId,
            companyId,
            new GlobalCellCoord(200 + idNumber, 200),
            MercenaryActivityStateV3.Idle,
            createdUtc,
            out MercenaryStateV3? state,
            out string stateReason), stateReason, failures);
        Require(state != null && state.CreatedUtc == profile.CreatedUtc, "General Profile and State CreatedUtc were not atomic.", failures);
        Require(registry.TryRegisterMercenary(profile, state, out string registerReason), registerReason, failures);
    }

    private static MercenaryProfileV3 CreateProfile(
        int idNumber,
        string displayName,
        int strength,
        int agility,
        int endurance,
        int intelligence,
        int mental,
        ICollection<string> failures)
    {
        return CreateProfile(idNumber, displayName, strength, agility, endurance, intelligence, mental, DateTime.UtcNow, failures);
    }

    private static MercenaryProfileV3 CreateProfile(
        int idNumber,
        string displayName,
        int strength,
        int agility,
        int endurance,
        int intelligence,
        int mental,
        DateTime createdUtc,
        ICollection<string> failures)
    {
        MercenaryAttributeSetV3.TryCreate(strength, agility, endurance, intelligence, mental, out MercenaryAttributeSetV3? attributes, out string attributeReason);
        Require(attributes != null, attributeReason, failures);
        MercenaryWorkSkillSetV3.TryCreate(10, 10, 10, 10, 10, 10, 10, out MercenaryWorkSkillSetV3? skills, out string skillReason);
        Require(skills != null, skillReason, failures);
        MercenaryProfileV3.TryCreate(
            StableId("merc_", idNumber),
            displayName,
            "placeholder_test",
            attributes,
            skills,
            createdUtc,
            out MercenaryProfileV3? profile,
            out string profileReason);
        Require(profile != null, profileReason, failures);
        return profile!;
    }

    private static bool SameIds(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        HashSet<string> values = new(left, StringComparer.Ordinal);
        foreach (string value in right)
        {
            if (!values.Remove(value))
            {
                return false;
            }
        }

        return values.Count == 0;
    }

    private static string StableId(string prefix, int number)
    {
        return prefix + number.ToString("x32");
    }

    private static void Require(bool condition, string failure, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(string.IsNullOrEmpty(failure) ? "Unspecified mercenary self-check failure." : failure);
        }
    }
}
