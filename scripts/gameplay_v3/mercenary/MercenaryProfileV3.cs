using System;

namespace GameplayV3.Mercenary;

public sealed class MercenaryProfileV3
{
    private MercenaryProfileV3(
        string mercenaryId,
        string displayName,
        string appearanceKey,
        MercenaryAttributeSetV3 attributes,
        MercenaryWorkSkillSetV3 workSkills,
        DateTime createdUtc,
        bool isInitialSquadMember,
        int? initialSquadSlotIndex)
    {
        MercenaryId = mercenaryId;
        DisplayName = displayName;
        AppearanceKey = appearanceKey;
        Attributes = attributes;
        WorkSkills = workSkills;
        CreatedUtc = createdUtc.Kind == DateTimeKind.Utc ? createdUtc : createdUtc.ToUniversalTime();
        IsInitialSquadMember = isInitialSquadMember;
        InitialSquadSlotIndex = initialSquadSlotIndex;
    }

    public string MercenaryId { get; }
    public string DisplayName { get; }
    public string AppearanceKey { get; }
    public MercenaryAttributeSetV3 Attributes { get; }
    public MercenaryWorkSkillSetV3 WorkSkills { get; }
    public DateTime CreatedUtc { get; }
    public bool IsInitialSquadMember { get; }
    public int? InitialSquadSlotIndex { get; }

    public static bool TryCreate(
        string mercenaryId,
        string displayName,
        string appearanceKey,
        MercenaryAttributeSetV3? attributes,
        MercenaryWorkSkillSetV3? workSkills,
        DateTime createdUtc,
        out MercenaryProfileV3? profile,
        out string reason,
        bool isInitialSquadMember = false,
        int? initialSquadSlotIndex = null)
    {
        if (!MercenaryIdFactoryV3.IsValidMercenaryId(mercenaryId))
        {
            profile = null;
            reason = "MercenaryId is not canonical.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName) || attributes == null || workSkills == null)
        {
            profile = null;
            reason = "DisplayName, attributes, and work skills are required.";
            return false;
        }

        if (isInitialSquadMember != initialSquadSlotIndex.HasValue
            || (initialSquadSlotIndex.HasValue && initialSquadSlotIndex.Value is < 0 or > 2))
        {
            profile = null;
            reason = "Initial squad metadata must use slot 0..2 only for an initial squad member.";
            return false;
        }

        profile = new MercenaryProfileV3(
            mercenaryId,
            displayName.Trim(),
            string.IsNullOrWhiteSpace(appearanceKey) ? "placeholder" : appearanceKey.Trim(),
            attributes,
            workSkills,
            createdUtc,
            isInitialSquadMember,
            initialSquadSlotIndex);
        reason = string.Empty;
        return true;
    }
}
