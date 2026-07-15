using System;
using System.Collections.Generic;
using GameplayV3.Company;
using GameplayV3.Deployment;
using Godot;
using WorldV2;

namespace GameplayV3.Mercenary;

public static class InitialSquadCreationServiceV3
{
    private const int InitialSquadSize = 3;

    public static InitialSquadCreationResultV3 CreateOrReuseInitialSquad(
        string companyId,
        CompanyRegistryV3 companyRegistry,
        CompanyDeploymentRegistryV3 deploymentRegistry,
        MercenaryRegistryV3 mercenaryRegistry,
        Rect2I worldBounds)
    {
        if (!companyRegistry.ContainsCompany(companyId))
        {
            return Failure(companyId, "Company is not registered.");
        }

        if (!deploymentRegistry.TryGetDeployment(companyId, out CompanyDeploymentStateV3? deployment) || deployment == null)
        {
            return Failure(companyId, "Company deployment is missing.");
        }

        if (!TryValidateFormation(deployment, worldBounds, out string formationReason))
        {
            return Failure(companyId, formationReason);
        }

        int existingCount = mercenaryRegistry.CountByCompany(companyId);
        if (existingCount == 0)
        {
            return CreateInitialSquad(companyId, deployment, mercenaryRegistry);
        }

        return ValidateExistingSquad(companyId, deployment, mercenaryRegistry);
    }

    private static InitialSquadCreationResultV3 CreateInitialSquad(
        string companyId,
        CompanyDeploymentStateV3 deployment,
        MercenaryRegistryV3 mercenaryRegistry)
    {
        DateTime createdUtc = DateTime.UtcNow;
        List<MercenaryProfileV3> profiles = new(InitialSquadSize);
        List<MercenaryStateV3> states = new(InitialSquadSize);
        for (int slotIndex = 0; slotIndex < InitialSquadSize; slotIndex++)
        {
            string mercenaryId = MercenaryIdFactoryV3.CreateMercenaryId();
            if (!TryCreateInitialProfile(mercenaryId, slotIndex, createdUtc, out MercenaryProfileV3? profile, out string reason)
                || profile == null
                || !MercenaryStateV3.TryCreate(
                    mercenaryId,
                    companyId,
                    deployment.FormationCells[slotIndex],
                    MercenaryActivityStateV3.Idle,
                    createdUtc,
                    out MercenaryStateV3? state,
                    out reason)
                || state == null)
            {
                return Failure(companyId, reason);
            }

            profiles.Add(profile);
            states.Add(state);
        }

        HashSet<string> preparedIds = new(StringComparer.Ordinal);
        HashSet<int> preparedInitialSlots = new();
        for (int index = 0; index < InitialSquadSize; index++)
        {
            if (!preparedIds.Add(profiles[index].MercenaryId)
                || !profiles[index].InitialSquadSlotIndex.HasValue
                || !preparedInitialSlots.Add(profiles[index].InitialSquadSlotIndex!.Value))
            {
                return Failure(companyId, "Prepared initial squad contains duplicates.");
            }

            if (!mercenaryRegistry.CanRegisterMercenary(profiles[index], states[index], out string reason))
            {
                return Failure(companyId, reason);
            }
        }

        List<string> registeredIds = new(InitialSquadSize);
        for (int index = 0; index < InitialSquadSize; index++)
        {
            if (mercenaryRegistry.TryRegisterMercenary(profiles[index], states[index], out string reason))
            {
                registeredIds.Add(profiles[index].MercenaryId);
                continue;
            }

            int rollbackCount = Rollback(registeredIds, mercenaryRegistry);
            return new InitialSquadCreationResultV3(false, false, companyId, Array.Empty<string>(), rollbackCount, reason);
        }

        return new InitialSquadCreationResultV3(true, false, companyId, registeredIds, 0, string.Empty);
    }

    private static InitialSquadCreationResultV3 ValidateExistingSquad(
        string companyId,
        CompanyDeploymentStateV3 deployment,
        MercenaryRegistryV3 registry)
    {
        IReadOnlyList<string> companyIds = registry.GetMercenariesByCompany(companyId);
        List<(int SlotIndex, string MercenaryId)> initialSquadMembers = new(InitialSquadSize);
        foreach (string mercenaryId in companyIds)
        {
            if (!registry.TryGetMercenary(mercenaryId, out MercenaryProfileV3? profile, out MercenaryStateV3? state)
                || profile == null
                || state == null)
            {
                return Failure(companyId, "Existing mercenary Profile/State data is inconsistent.");
            }

            if (!profile.IsInitialSquadMember)
            {
                continue;
            }

            if (!profile.InitialSquadSlotIndex.HasValue
                || profile.InitialSquadSlotIndex.Value is < 0 or >= InitialSquadSize
                || state.CurrentCell.Value != deployment.FormationCells[profile.InitialSquadSlotIndex.Value].Value
                || profile.CreatedUtc != state.CreatedUtc
                || !MatchesInitialTemplate(profile, profile.InitialSquadSlotIndex.Value))
            {
                return Failure(companyId, "Existing initial squad Profile/State metadata is inconsistent.");
            }

            initialSquadMembers.Add((profile.InitialSquadSlotIndex.Value, mercenaryId));
        }

        if (initialSquadMembers.Count != InitialSquadSize)
        {
            return Failure(companyId, $"Initial squad is inconsistent: expected three initial members, found {initialSquadMembers.Count}.");
        }

        initialSquadMembers.Sort(static (left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
        for (int index = 0; index < InitialSquadSize; index++)
        {
            if (initialSquadMembers[index].SlotIndex != index)
            {
                return Failure(companyId, "Existing initial squad does not contain InitialSquadSlotIndex 0, 1, and 2.");
            }
        }

        List<string> initialIds = new(InitialSquadSize);
        foreach ((_, string mercenaryId) in initialSquadMembers)
        {
            initialIds.Add(mercenaryId);
        }

        return new InitialSquadCreationResultV3(true, true, companyId, initialIds, 0, string.Empty);
    }

    private static bool TryValidateFormation(CompanyDeploymentStateV3 deployment, Rect2I worldBounds, out string reason)
    {
        if (deployment.FormationCells.Count != InitialSquadSize)
        {
            reason = "Deployment must provide exactly three FormationCells.";
            return false;
        }

        HashSet<Vector2I> uniqueCells = new();
        foreach (GlobalCellCoord cell in deployment.FormationCells)
        {
            if (!worldBounds.HasPoint(cell.Value) || !uniqueCells.Add(cell.Value))
            {
                reason = "Deployment FormationCells must be unique and inside world bounds.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryCreateInitialProfile(
        string mercenaryId,
        int initialSquadSlotIndex,
        DateTime createdUtc,
        out MercenaryProfileV3? profile,
        out string reason)
    {
        InitialProfileTemplate template = GetInitialTemplate(initialSquadSlotIndex);

        profile = null;
        if (!MercenaryAttributeSetV3.TryCreate(
                template.Strength,
                template.Agility,
                template.Endurance,
                template.Intelligence,
                template.Mental,
                out MercenaryAttributeSetV3? attributes,
                out reason)
            || !MercenaryWorkSkillSetV3.TryCreate(
                template.Hauling,
                template.Construction,
                template.Gathering,
                template.Farming,
                template.Production,
                template.Medicine,
                template.Guarding,
                out MercenaryWorkSkillSetV3? skills,
                out reason))
        {
            return false;
        }

        return MercenaryProfileV3.TryCreate(
            mercenaryId,
            template.DisplayName,
            template.AppearanceKey,
            attributes,
            skills,
            createdUtc,
            out profile,
            out reason,
            true,
            initialSquadSlotIndex);
    }

    private static bool MatchesInitialTemplate(MercenaryProfileV3 profile, int initialSquadSlotIndex)
    {
        InitialProfileTemplate template = GetInitialTemplate(initialSquadSlotIndex);
        return profile.DisplayName == template.DisplayName
            && profile.AppearanceKey == template.AppearanceKey
            && profile.Attributes.Strength == template.Strength
            && profile.Attributes.Agility == template.Agility
            && profile.Attributes.Endurance == template.Endurance
            && profile.Attributes.Intelligence == template.Intelligence
            && profile.Attributes.Mental == template.Mental
            && profile.WorkSkills.Hauling == template.Hauling
            && profile.WorkSkills.Construction == template.Construction
            && profile.WorkSkills.Gathering == template.Gathering
            && profile.WorkSkills.Farming == template.Farming
            && profile.WorkSkills.Production == template.Production
            && profile.WorkSkills.Medicine == template.Medicine
            && profile.WorkSkills.Guarding == template.Guarding;
    }

    private static InitialProfileTemplate GetInitialTemplate(int initialSquadSlotIndex)
    {
        return initialSquadSlotIndex switch
        {
            0 => new InitialProfileTemplate("Recruit A", "placeholder_a", 10, 10, 10, 10, 10, 8, 8, 7, 7, 7, 5, 7),
            1 => new InitialProfileTemplate("Recruit B", "placeholder_b", 7, 14, 9, 12, 9, 6, 11, 6, 9, 10, 7, 6),
            2 => new InitialProfileTemplate("Recruit C", "placeholder_c", 15, 7, 13, 7, 12, 12, 6, 11, 5, 6, 3, 9),
            _ => throw new ArgumentOutOfRangeException(nameof(initialSquadSlotIndex))
        };
    }

    private static int Rollback(IReadOnlyList<string> registeredIds, MercenaryRegistryV3 registry)
    {
        int rollbackCount = 0;
        for (int index = registeredIds.Count - 1; index >= 0; index--)
        {
            if (registry.TryRemoveMercenary(registeredIds[index], out _))
            {
                rollbackCount++;
            }
        }

        return rollbackCount;
    }

    private static InitialSquadCreationResultV3 Failure(string companyId, string reason)
    {
        return new InitialSquadCreationResultV3(false, false, companyId, Array.Empty<string>(), 0, reason);
    }

    private readonly struct InitialProfileTemplate
    {
        public InitialProfileTemplate(
            string displayName,
            string appearanceKey,
            int strength,
            int agility,
            int endurance,
            int intelligence,
            int mental,
            int hauling,
            int construction,
            int gathering,
            int farming,
            int production,
            int medicine,
            int guarding)
        {
            DisplayName = displayName;
            AppearanceKey = appearanceKey;
            Strength = strength;
            Agility = agility;
            Endurance = endurance;
            Intelligence = intelligence;
            Mental = mental;
            Hauling = hauling;
            Construction = construction;
            Gathering = gathering;
            Farming = farming;
            Production = production;
            Medicine = medicine;
            Guarding = guarding;
        }

        public string DisplayName { get; }
        public string AppearanceKey { get; }
        public int Strength { get; }
        public int Agility { get; }
        public int Endurance { get; }
        public int Intelligence { get; }
        public int Mental { get; }
        public int Hauling { get; }
        public int Construction { get; }
        public int Gathering { get; }
        public int Farming { get; }
        public int Production { get; }
        public int Medicine { get; }
        public int Guarding { get; }
    }
}
