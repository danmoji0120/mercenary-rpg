using System;
using GameplayV3.Company;
using Godot;
using WorldV2;

namespace GameplayV3.Mercenary.UI;

public readonly record struct MercenaryConditionSnapshotV3(
    float HealthNormalized,
    string HealthSummary,
    string InjurySummary,
    string TreatmentSummary,
    float HungerNormalized,
    float FatigueNormalized,
    float MoraleNormalized,
    bool IsPlaceholder,
    string DataSourceName)
{
    public float FullnessNormalized => 1f - HungerNormalized;
    public float RestNormalized => 1f - FatigueNormalized;
    public bool AffectsGameplay => false;
}

public interface IMercenaryConditionSnapshotProviderV3
{
    bool TryGetSnapshot(
        MercenaryProfileV3 profile,
        MercenaryStateV3 state,
        out MercenaryConditionSnapshotV3 snapshot);
}

public sealed class PlaceholderMercenaryConditionSnapshotProviderV3 : IMercenaryConditionSnapshotProviderV3
{
    public bool TryGetSnapshot(
        MercenaryProfileV3 profile,
        MercenaryStateV3 state,
        out MercenaryConditionSnapshotV3 snapshot)
    {
        if (profile == null || state == null || profile.MercenaryId != state.MercenaryId)
        {
            snapshot = default;
            return false;
        }

        uint hash = StableHash(profile.MercenaryId);
        float hunger = 0.05f + (hash & 1023) / 1023f * 0.25f;
        float fatigue = 0.05f + ((hash >> 10) & 1023) / 1023f * 0.20f;
        float morale = 0.55f + ((hash >> 20) & 1023) / 1023f * 0.30f;
        snapshot = new MercenaryConditionSnapshotV3(
            1f,
            "건강함",
            "부상 없음",
            "치료 필요 없음",
            hunger,
            fatigue,
            morale,
            true,
            "Placeholder");
        return true;
    }

    private static uint StableHash(string value)
    {
        uint hash = 2166136261;
        foreach (char character in value)
        {
            hash ^= character;
            hash *= 16777619;
        }

        return hash;
    }
}

public static class MercenaryConditionSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        if (!MercenaryAttributeSetV3.TryCreate(10, 10, 10, 10, 10, out MercenaryAttributeSetV3? attributes, out reason)
            || !MercenaryWorkSkillSetV3.TryCreate(8, 8, 7, 7, 7, 5, 7, out MercenaryWorkSkillSetV3? skills, out reason))
        {
            return false;
        }

        DateTime createdUtc = DateTime.UtcNow;
        string mercenaryId = MercenaryIdFactoryV3.CreateMercenaryId();
        if (!MercenaryProfileV3.TryCreate(mercenaryId, "HUD Test", "placeholder", attributes, skills, createdUtc, out MercenaryProfileV3? profile, out reason)
            || !MercenaryStateV3.TryCreate(mercenaryId, CompanyIdFactoryV3.CreateCompanyId(), new GlobalCellCoord(new Vector2I(1, 1)), MercenaryActivityStateV3.Idle, createdUtc, out MercenaryStateV3? state, out reason)
            || profile == null
            || state == null)
        {
            return false;
        }

        PlaceholderMercenaryConditionSnapshotProviderV3 provider = new();
        if (!provider.TryGetSnapshot(profile, state, out MercenaryConditionSnapshotV3 first)
            || !provider.TryGetSnapshot(profile, state, out MercenaryConditionSnapshotV3 second)
            || first != second)
        {
            reason = "Placeholder condition snapshot is not deterministic.";
            return false;
        }

        if (first.HealthNormalized != 1f
            || first.HungerNormalized is < 0.05f or > 0.30f
            || first.FatigueNormalized is < 0.05f or > 0.25f
            || first.MoraleNormalized is < 0.55f or > 0.85f
            || !first.IsPlaceholder
            || first.AffectsGameplay)
        {
            reason = "Placeholder condition snapshot range or metadata is invalid.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
