namespace GameplayV3.Mercenary;

public sealed class MercenaryAttributeSetV3
{
    public const int MinimumValue = 0;
    public const int MaximumValue = 20;

    private MercenaryAttributeSetV3(int strength, int agility, int endurance, int intelligence, int mental)
    {
        Strength = strength;
        Agility = agility;
        Endurance = endurance;
        Intelligence = intelligence;
        Mental = mental;
    }

    public int Strength { get; }
    public int Agility { get; }
    public int Endurance { get; }
    public int Intelligence { get; }
    public int Mental { get; }

    public static bool TryCreate(
        int strength,
        int agility,
        int endurance,
        int intelligence,
        int mental,
        out MercenaryAttributeSetV3? attributes,
        out string reason)
    {
        if (!IsValid(strength) || !IsValid(agility) || !IsValid(endurance) || !IsValid(intelligence) || !IsValid(mental))
        {
            attributes = null;
            reason = $"Mercenary attributes must be between {MinimumValue} and {MaximumValue}.";
            return false;
        }

        attributes = new MercenaryAttributeSetV3(strength, agility, endurance, intelligence, mental);
        reason = string.Empty;
        return true;
    }

    public int GetValue(MercenaryAttributeTypeV3 type)
    {
        return type switch
        {
            MercenaryAttributeTypeV3.Strength => Strength,
            MercenaryAttributeTypeV3.Agility => Agility,
            MercenaryAttributeTypeV3.Endurance => Endurance,
            MercenaryAttributeTypeV3.Intelligence => Intelligence,
            MercenaryAttributeTypeV3.Mental => Mental,
            _ => 0
        };
    }

    private static bool IsValid(int value)
    {
        return value is >= MinimumValue and <= MaximumValue;
    }
}
