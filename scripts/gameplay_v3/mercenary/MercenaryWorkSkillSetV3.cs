using System;
using System.Collections.Generic;

namespace GameplayV3.Mercenary;

public readonly struct MercenaryWorkSkillValueV3
{
    public MercenaryWorkSkillValueV3(MercenaryWorkSkillTypeV3 type, int value)
    {
        Type = type;
        Value = value;
    }

    public MercenaryWorkSkillTypeV3 Type { get; }
    public int Value { get; }
    public override string ToString() => $"{Type} {Value}";
}

public sealed class MercenaryWorkSkillSetV3
{
    public const int MinimumValue = 0;
    public const int MaximumValue = 20;

    private MercenaryWorkSkillSetV3(
        int hauling,
        int construction,
        int gathering,
        int farming,
        int production,
        int medicine,
        int guarding)
    {
        Hauling = hauling;
        Construction = construction;
        Gathering = gathering;
        Farming = farming;
        Production = production;
        Medicine = medicine;
        Guarding = guarding;
    }

    public int Hauling { get; }
    public int Construction { get; }
    public int Gathering { get; }
    public int Farming { get; }
    public int Production { get; }
    public int Medicine { get; }
    public int Guarding { get; }

    public static bool TryCreate(
        int hauling,
        int construction,
        int gathering,
        int farming,
        int production,
        int medicine,
        int guarding,
        out MercenaryWorkSkillSetV3? skills,
        out string reason)
    {
        if (!IsValid(hauling) || !IsValid(construction) || !IsValid(gathering) || !IsValid(farming)
            || !IsValid(production) || !IsValid(medicine) || !IsValid(guarding))
        {
            skills = null;
            reason = $"Mercenary work skills must be between {MinimumValue} and {MaximumValue}.";
            return false;
        }

        skills = new MercenaryWorkSkillSetV3(hauling, construction, gathering, farming, production, medicine, guarding);
        reason = string.Empty;
        return true;
    }

    public int GetValue(MercenaryWorkSkillTypeV3 type)
    {
        return type switch
        {
            MercenaryWorkSkillTypeV3.Hauling => Hauling,
            MercenaryWorkSkillTypeV3.Construction => Construction,
            MercenaryWorkSkillTypeV3.Gathering => Gathering,
            MercenaryWorkSkillTypeV3.Farming => Farming,
            MercenaryWorkSkillTypeV3.Production => Production,
            MercenaryWorkSkillTypeV3.Medicine => Medicine,
            MercenaryWorkSkillTypeV3.Guarding => Guarding,
            _ => 0
        };
    }

    public MercenaryWorkSkillValueV3 GetHighestSkill()
    {
        return GetTopSkills(1)[0];
    }

    public IReadOnlyList<MercenaryWorkSkillValueV3> GetTopSkills(int count)
    {
        if (count <= 0)
        {
            return Array.Empty<MercenaryWorkSkillValueV3>();
        }

        List<MercenaryWorkSkillValueV3> values = new(7);
        foreach (MercenaryWorkSkillTypeV3 type in Enum.GetValues<MercenaryWorkSkillTypeV3>())
        {
            values.Add(new MercenaryWorkSkillValueV3(type, GetValue(type)));
        }

        values.Sort(static (left, right) =>
        {
            int valueComparison = right.Value.CompareTo(left.Value);
            return valueComparison != 0 ? valueComparison : left.Type.CompareTo(right.Type);
        });

        if (count < values.Count)
        {
            values.RemoveRange(count, values.Count - count);
        }

        return values.AsReadOnly();
    }

    private static bool IsValid(int value)
    {
        return value is >= MinimumValue and <= MaximumValue;
    }
}
