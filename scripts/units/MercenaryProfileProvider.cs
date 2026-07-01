using System.Collections.Generic;

public static class MercenaryProfileProvider
{
    private static readonly MercenaryProfile[] StartingProfiles =
    {
        Create(
            "nora",
            "\uB178\uB77C",
            MercenaryRank.N,
            MercenaryRace.Human,
            MercenaryRole.Scout,
            MercenaryRole.Hauler,
            new MercenaryStats
            {
                MaxHealth = 92,
                Strength = 6,
                Dexterity = 10,
                Endurance = 6,
                Intelligence = 8,
                Willpower = 7,
                CombatSkill = 2,
                HaulingSkill = 5,
                FarmingSkill = 2,
                CraftingSkill = 1,
                MedicalSkill = 0,
                CookingSkill = 1,
                MaxCarryWeight = 28,
                MoveSpeedMultiplier = 1.12f
            },
            "\uBC1C\uC774 \uBE60\uB978 \uC778\uAC04 \uCC99\uD6C4\uBCD1. \uC544\uC9C1 \uC804\uD22C\uB825\uC740 \uB0AE\uC9C0\uB9CC \uC7A1\uC77C\uC5D0 \uAC15\uD569\uB2C8\uB2E4.",
            "\uB208\uCE58 \uBE60\uB984",
            "\uD5C8\uC57D\uD568"),
        Create(
            "marta",
            "\uB9C8\uB974\uD0C0",
            MercenaryRank.R,
            MercenaryRace.Human,
            MercenaryRole.Tank,
            MercenaryRole.Guard,
            new MercenaryStats
            {
                MaxHealth = 132,
                Strength = 12,
                Dexterity = 8,
                Endurance = 14,
                Intelligence = 8,
                Willpower = 12,
                CombatSkill = 7,
                HaulingSkill = 4,
                FarmingSkill = 1,
                CraftingSkill = 2,
                MedicalSkill = 1,
                CookingSkill = 1,
                MaxCarryWeight = 42,
                MoveSpeedMultiplier = 0.95f
            },
            "\uBC29\uD328\uB97C \uB4E0 \uACBD\uBE44\uBC18\uC7A5. \uAE30\uC9C0 \uBC29\uC5B4\uC640 \uC804\uC5F4 \uC720\uC9C0\uC5D0 \uAC15\uD569\uB2C8\uB2E4.",
            "\uCC45\uC784\uAC10",
            "\uACE0\uC9D1"),
        Create(
            "rina",
            "\uB9AC\uB098",
            MercenaryRank.N,
            MercenaryRace.Human,
            MercenaryRole.Fighter,
            MercenaryRole.Guard,
            new MercenaryStats
            {
                MaxHealth = 104,
                Strength = 9,
                Dexterity = 8,
                Endurance = 9,
                Intelligence = 6,
                Willpower = 7,
                CombatSkill = 5,
                HaulingSkill = 3,
                FarmingSkill = 2,
                CraftingSkill = 1,
                MedicalSkill = 0,
                CookingSkill = 0,
                MaxCarryWeight = 32,
                MoveSpeedMultiplier = 1.0f
            },
            "\uCC3D\uACFC \uBC29\uD328\uB97C \uB2E4\uB8E8\uB294 \uACAC\uC2B5 \uBCD1\uC0AC.",
            "\uC131\uC2E4\uD568"),
        Create(
            "evelyn",
            "\uC774\uBE0C\uB9B0",
            MercenaryRank.R,
            MercenaryRace.Human,
            MercenaryRole.Medic,
            MercenaryRole.Worker,
            new MercenaryStats
            {
                MaxHealth = 108,
                Strength = 7,
                Dexterity = 11,
                Endurance = 8,
                Intelligence = 14,
                Willpower = 11,
                CombatSkill = 3,
                HaulingSkill = 3,
                FarmingSkill = 2,
                CraftingSkill = 4,
                MedicalSkill = 8,
                CookingSkill = 3,
                MaxCarryWeight = 30,
                MoveSpeedMultiplier = 1.0f
            },
            "\uC678\uC0C1 \uBD09\uD569\uC5D0 \uC775\uC219\uD55C \uC57C\uC804 \uCE58\uB8CC\uC0AC.",
            "\uCE68\uCC29\uD568",
            "\uACB0\uBCBD"),
        Create(
            "darmel",
            "\uB2E4\uB974\uBA5C",
            MercenaryRank.N,
            MercenaryRace.Human,
            MercenaryRole.Hauler,
            MercenaryRole.Worker,
            new MercenaryStats
            {
                MaxHealth = 110,
                Strength = 10,
                Dexterity = 5,
                Endurance = 10,
                Intelligence = 5,
                Willpower = 8,
                CombatSkill = 1,
                HaulingSkill = 5,
                FarmingSkill = 3,
                CraftingSkill = 1,
                MedicalSkill = 0,
                CookingSkill = 1,
                MaxCarryWeight = 35,
                MoveSpeedMultiplier = 0.88f
            },
            "\uBD09\uC778\uB048\uACFC \uC9D0\uC744 \uB098\uB974\uB294 \uC6B4\uBC18\uC790.",
            "\uD2BC\uD2BC\uD568",
            "\uB290\uB9BC"),
        Create(
            "sion",
            "\uC2DC\uC628",
            MercenaryRank.R,
            MercenaryRace.Human,
            MercenaryRole.Fighter,
            MercenaryRole.Scout,
            new MercenaryStats
            {
                MaxHealth = 118,
                Strength = 13,
                Dexterity = 14,
                Endurance = 9,
                Intelligence = 7,
                Willpower = 8,
                CombatSkill = 8,
                HaulingSkill = 2,
                FarmingSkill = 0,
                CraftingSkill = 1,
                MedicalSkill = 0,
                CookingSkill = 0,
                MaxCarryWeight = 31,
                MoveSpeedMultiplier = 1.08f
            },
            "\uACC4\uC57D \uBB38\uC81C\uB97C \uC790\uC8FC \uC77C\uC73C\uD0A4\uB294 \uBD88\uB7C9 \uACB0\uD22C\uC0AC.",
            "\uC804\uD22C\uAD11",
            "\uC190\uBC84\uB987 \uB098\uC068"),
        Create(
            "amelia",
            "\uC544\uBA5C\uB9AC\uC544",
            MercenaryRank.SR,
            MercenaryRace.Human,
            MercenaryRole.Medic,
            MercenaryRole.Fighter,
            new MercenaryStats
            {
                MaxHealth = 156,
                Strength = 15,
                Dexterity = 13,
                Endurance = 15,
                Intelligence = 18,
                Willpower = 20,
                CombatSkill = 10,
                HaulingSkill = 4,
                FarmingSkill = 3,
                CraftingSkill = 5,
                MedicalSkill = 14,
                CookingSkill = 4,
                MaxCarryWeight = 44,
                MoveSpeedMultiplier = 1.02f
            },
            "\uC2E0\uC131\uC220\uACFC \uADFC\uC811\uC804\uC744 \uD568\uAED8 \uB2E4\uB8E8\uB294 \uC804\uD22C \uC0AC\uC81C.",
            "\uC2E0\uC559\uC2EC",
            "\uC644\uACE0\uD568"),
        Create(
            "eda",
            "\uC5D0\uB2E4",
            MercenaryRank.SR,
            MercenaryRace.Human,
            MercenaryRole.Crafter,
            MercenaryRole.Medic,
            new MercenaryStats
            {
                MaxHealth = 138,
                Strength = 12,
                Dexterity = 16,
                Endurance = 12,
                Intelligence = 20,
                Willpower = 17,
                CombatSkill = 6,
                HaulingSkill = 3,
                FarmingSkill = 4,
                CraftingSkill = 14,
                MedicalSkill = 9,
                CookingSkill = 5,
                MaxCarryWeight = 38,
                MoveSpeedMultiplier = 1.0f
            },
            "\uC800\uC8FC\uC640 \uBB3C\uAC74\uC758 \uAC10\uC815\uC744 \uB9E1\uB294 \uAD34\uC9DC \uAC10\uC815\uC0AC.",
            "\uC218\uC0C1\uD568",
            "\uC9D1\uC911\uB825")
    };

    public static IReadOnlyList<MercenaryProfile> GetStartingProfiles()
    {
        return StartingProfiles;
    }

    public static MercenaryProfile GetStartingProfile(int index)
    {
        return index >= 0 && index < StartingProfiles.Length
            ? CloneProfile(StartingProfiles[index])
            : CreateFallbackProfile(index + 1);
    }

    public static MercenaryProfile CreateFallbackProfile(int index = 0)
    {
        MercenaryProfile profile = Create(
            $"mercenary_{index}",
            index > 0 ? $"Mercenary {index}" : "Mercenary",
            MercenaryRank.N,
            MercenaryRace.Unknown,
            MercenaryRole.Worker,
            MercenaryRole.Hauler,
            new MercenaryStats(),
            "Fallback mercenary profile.",
            "Reliable");
        return profile;
    }

    private static MercenaryProfile Create(
        string id,
        string displayName,
        MercenaryRank rank,
        MercenaryRace race,
        MercenaryRole primaryRole,
        MercenaryRole secondaryRole,
        MercenaryStats stats,
        string description,
        params string[] traits)
    {
        MercenaryProfile profile = new()
        {
            MercenaryId = id,
            DisplayName = displayName,
            Rank = rank,
            Race = race,
            PrimaryRole = primaryRole,
            SecondaryRole = secondaryRole,
            Stats = stats,
            Condition = new MercenaryCondition
            {
                Health = stats.MaxHealth,
                Mood = 50,
                Stress = 0,
                Hygiene = 100,
                InjurySeverity = 0
            },
            WorkSettings = MercenaryWorkSettings.CreateForRoles(primaryRole, secondaryRole),
            ShortDescription = description
        };

        profile.Traits.AddRange(traits);
        return profile;
    }

    private static MercenaryProfile CloneProfile(MercenaryProfile source)
    {
        MercenaryProfile clone = new()
        {
            MercenaryId = source.MercenaryId,
            DisplayName = source.DisplayName,
            Rank = source.Rank,
            Race = source.Race,
            PrimaryRole = source.PrimaryRole,
            SecondaryRole = source.SecondaryRole,
            Stats = new MercenaryStats
            {
                MaxHealth = source.Stats.MaxHealth,
                Strength = source.Stats.Strength,
                Dexterity = source.Stats.Dexterity,
                Endurance = source.Stats.Endurance,
                Intelligence = source.Stats.Intelligence,
                Willpower = source.Stats.Willpower,
                CombatSkill = source.Stats.CombatSkill,
                HaulingSkill = source.Stats.HaulingSkill,
                FarmingSkill = source.Stats.FarmingSkill,
                CraftingSkill = source.Stats.CraftingSkill,
                MedicalSkill = source.Stats.MedicalSkill,
                CookingSkill = source.Stats.CookingSkill,
                MaxCarryWeight = source.Stats.MaxCarryWeight,
                MoveSpeedMultiplier = source.Stats.MoveSpeedMultiplier
            },
            Condition = new MercenaryCondition
            {
                Health = source.Condition.Health,
                Hunger = source.Condition.Hunger,
                Sleepiness = source.Condition.Sleepiness,
                Mood = source.Condition.Mood,
                Stress = source.Condition.Stress,
                Hygiene = source.Condition.Hygiene,
                InjurySeverity = source.Condition.InjurySeverity
            },
            WorkSettings = source.GetWorkSettings().Clone(),
            ShortDescription = source.ShortDescription
        };

        clone.Traits.AddRange(source.Traits);
        return clone;
    }
}
