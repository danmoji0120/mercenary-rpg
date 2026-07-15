namespace GameplayV3.Mercenary;

public static class MercenaryDerivedStatsCalculatorV3
{
    public static MercenaryDerivedStatsV3 Calculate(MercenaryProfileV3 profile)
    {
        MercenaryAttributeSetV3 attributes = profile.Attributes;
        return new MercenaryDerivedStatsV3(
            0.90f + attributes.Agility * 0.015f + attributes.Endurance * 0.005f,
            5.0f + attributes.Strength * 0.6f + attributes.Endurance * 0.2f,
            0.80f + attributes.Agility * 0.008f + attributes.Endurance * 0.006f + attributes.Intelligence * 0.006f);
    }

    public static float GetWorkScore(MercenaryProfileV3 profile, MercenaryWorkSkillTypeV3 skillType)
    {
        float skill = profile.WorkSkills.GetValue(skillType) * 0.80f;
        MercenaryAttributeSetV3 attributes = profile.Attributes;
        return skillType switch
        {
            MercenaryWorkSkillTypeV3.Hauling => skill + attributes.Strength * 0.15f + attributes.Endurance * 0.05f,
            MercenaryWorkSkillTypeV3.Construction => skill + attributes.Agility * 0.10f + attributes.Intelligence * 0.10f,
            MercenaryWorkSkillTypeV3.Gathering => skill + attributes.Strength * 0.15f + attributes.Endurance * 0.05f,
            MercenaryWorkSkillTypeV3.Farming => skill + attributes.Agility * 0.10f + attributes.Intelligence * 0.10f,
            MercenaryWorkSkillTypeV3.Production => skill + attributes.Agility * 0.10f + attributes.Intelligence * 0.10f,
            MercenaryWorkSkillTypeV3.Medicine => skill + attributes.Intelligence * 0.15f + attributes.Agility * 0.05f,
            MercenaryWorkSkillTypeV3.Guarding => skill + attributes.Mental * 0.10f + attributes.Agility * 0.10f,
            _ => skill
        };
    }
}
