using System;

namespace GameplayV3.Equipment;

public readonly record struct EquipmentQualityResolutionV3(
    int ProductionSkill,
    double VariationRoll,
    int QualityScore,
    EquipmentQualityV3 Quality);

public static class EquipmentQualityResolverV3
{
    public const int MinimumProductionSkill = 0;
    public const int MaximumProductionSkill = 20;
    public const double MinimumVariationRoll = -4.0;
    public const double MaximumVariationRoll = 4.0;
    public const int MaximumQualityScore = 24;

    /// <summary>
    /// Resolves quality deterministically from the production skill captured at completion and an externally supplied roll.
    /// The caller owns deterministic random generation; the resolved result is stored once and is never rerolled by view,
    /// location, equipment, or persistence transitions.
    /// </summary>
    public static EquipmentQualityResolutionV3 Resolve(int productionSkill, double variationRoll)
    {
        int clampedSkill = Math.Clamp(productionSkill, MinimumProductionSkill, MaximumProductionSkill);
        double finiteRoll = double.IsFinite(variationRoll) ? variationRoll : 0.0;
        double clampedRoll = Math.Clamp(finiteRoll, MinimumVariationRoll, MaximumVariationRoll);
        int roundedRoll = (int)Math.Round(clampedRoll, MidpointRounding.AwayFromZero);
        int score = Math.Clamp(clampedSkill + roundedRoll, 0, MaximumQualityScore);
        return new(clampedSkill, clampedRoll, score, GetQualityForScore(score));
    }

    public static EquipmentQualityV3 GetQualityForScore(int qualityScore)
    {
        if (qualityScore < 0 || qualityScore > MaximumQualityScore)
            throw new ArgumentOutOfRangeException(nameof(qualityScore));
        return qualityScore switch
        {
            <= 3 => EquipmentQualityV3.Poor,
            <= 7 => EquipmentQualityV3.Normal,
            <= 11 => EquipmentQualityV3.Good,
            <= 15 => EquipmentQualityV3.Excellent,
            <= 18 => EquipmentQualityV3.Masterpiece,
            _ => EquipmentQualityV3.Masterwork
        };
    }

    public static double GetQualityMultiplier(EquipmentQualityV3 quality) => quality switch
    {
        EquipmentQualityV3.Poor => 0.75,
        EquipmentQualityV3.Normal => 1.00,
        EquipmentQualityV3.Good => 1.10,
        EquipmentQualityV3.Excellent => 1.22,
        EquipmentQualityV3.Masterpiece => 1.38,
        EquipmentQualityV3.Masterwork => 1.60,
        _ => throw new ArgumentOutOfRangeException(nameof(quality))
    };

    public static bool TryEvaluateModifier(
        EquipmentDefinitionV3 definition,
        EquipmentQualityV3 quality,
        EquipmentModifierKindV3 modifierKind,
        out double value)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Enum.IsDefined(quality) || !Enum.IsDefined(modifierKind))
        {
            value = 0.0;
            return false;
        }
        foreach (EquipmentStatModifierV3 modifier in definition.Modifiers)
        {
            if (modifier.ModifierKind != modifierKind) continue;
            value = modifier.ScalesWithQuality
                ? modifier.BaseValue * GetQualityMultiplier(quality)
                : modifier.BaseValue;
            return true;
        }
        value = 0.0;
        return false;
    }
}
