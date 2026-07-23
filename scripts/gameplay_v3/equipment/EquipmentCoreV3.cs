using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

namespace GameplayV3.Equipment;

public enum EquipmentSlotV3
{
    MainHand = 1,
    Armor = 2,
    Tool = 3
}

public enum EquipmentQualityV3
{
    Poor = 0,
    Normal = 1,
    Good = 2,
    Excellent = 3,
    Masterpiece = 4,
    Masterwork = 5
}

public enum EquipmentModifierKindV3
{
    AttackPower = 1,
    DefensePower = 2,
    GatheringWorkSpeed = 3
}

public enum EquipmentLocationKindV3
{
    Unplaced = 0,
    Ground = 1,
    Storage = 2,
    Equipped = 3,
    FacilityOutput = 4,
    CompanyHolding = 5
}

public static class EquipmentDisplayNamesV3
{
    public static string GetSlotDisplayName(EquipmentSlotV3 slot) => slot switch
    {
        EquipmentSlotV3.MainHand => "\uc8fc\ubb34\uae30",
        EquipmentSlotV3.Armor => "\ubc29\uc5b4\uad6c",
        EquipmentSlotV3.Tool => "\ub3c4\uad6c",
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public static string GetQualityDisplayName(EquipmentQualityV3 quality) => quality switch
    {
        EquipmentQualityV3.Poor => "\uc870\uc545",
        EquipmentQualityV3.Normal => "\ubcf4\ud1b5",
        EquipmentQualityV3.Good => "\uc591\uc9c8",
        EquipmentQualityV3.Excellent => "\uc6b0\uc218",
        EquipmentQualityV3.Masterpiece => "\uba85\ud488",
        EquipmentQualityV3.Masterwork => "\uac78\uc791",
        _ => throw new ArgumentOutOfRangeException(nameof(quality))
    };
}

public sealed record EquipmentStatModifierV3(
    EquipmentModifierKindV3 ModifierKind,
    double BaseValue,
    bool ScalesWithQuality);

public sealed class EquipmentDefinitionV3
{
    private EquipmentDefinitionV3(
        string definitionId,
        string displayName,
        string description,
        EquipmentSlotV3 slot,
        IReadOnlyList<EquipmentStatModifierV3> modifiers,
        int baseMarketValue,
        IReadOnlyList<string> tags,
        string iconKey)
    {
        DefinitionId = definitionId;
        DisplayName = displayName;
        Description = description;
        Slot = slot;
        Modifiers = modifiers;
        BaseMarketValue = baseMarketValue;
        Tags = tags;
        IconKey = iconKey;
    }

    public string DefinitionId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public EquipmentSlotV3 Slot { get; }
    public IReadOnlyList<EquipmentStatModifierV3> Modifiers { get; }
    public int BaseMarketValue { get; }
    public IReadOnlyList<string> Tags { get; }
    public string IconKey { get; }

    public static bool TryCreate(
        string definitionId,
        string displayName,
        string description,
        EquipmentSlotV3 slot,
        IEnumerable<EquipmentStatModifierV3>? modifiers,
        int baseMarketValue,
        out EquipmentDefinitionV3? definition,
        out string reason,
        IEnumerable<string>? tags = null,
        string? iconKey = null)
    {
        definition = null;
        if (!EquipmentDefinitionRegistryV3.IsValidDefinitionId(definitionId))
        {
            reason = "Equipment DefinitionId is invalid.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(description))
        {
            reason = "Equipment display name and description are required.";
            return false;
        }
        if (!Enum.IsDefined(slot) || baseMarketValue < 0)
        {
            reason = "Equipment slot or base market value is invalid.";
            return false;
        }

        List<EquipmentStatModifierV3> modifierCopy = new();
        HashSet<EquipmentModifierKindV3> modifierKinds = new();
        if (modifiers != null)
        {
            foreach (EquipmentStatModifierV3 modifier in modifiers)
            {
                if (!Enum.IsDefined(modifier.ModifierKind) || !double.IsFinite(modifier.BaseValue))
                {
                    reason = "Equipment modifier is invalid.";
                    return false;
                }
                if (!modifierKinds.Add(modifier.ModifierKind))
                {
                    reason = "Duplicate equipment modifier kind.";
                    return false;
                }
                modifierCopy.Add(modifier);
            }
        }

        List<string> tagCopy = new();
        HashSet<string> uniqueTags = new(StringComparer.Ordinal);
        if (tags != null)
        {
            foreach (string tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag) || !uniqueTags.Add(tag))
                {
                    reason = "Equipment tags must be non-empty and unique.";
                    return false;
                }
                tagCopy.Add(tag);
            }
        }

        definition = new EquipmentDefinitionV3(
            definitionId,
            displayName,
            description,
            slot,
            new ReadOnlyCollection<EquipmentStatModifierV3>(modifierCopy),
            baseMarketValue,
            new ReadOnlyCollection<string>(tagCopy),
            iconKey ?? string.Empty);
        reason = string.Empty;
        return true;
    }
}

public sealed class EquipmentInstanceV3
{
    internal EquipmentInstanceV3(
        string equipmentInstanceId,
        string equipmentDefinitionId,
        EquipmentQualityV3 quality,
        int qualityScore,
        string crafterMercenaryId,
        int crafterProductionSkillSnapshot,
        string ownerCompanyId,
        long createdSessionRevision)
    {
        EquipmentInstanceId = equipmentInstanceId;
        EquipmentDefinitionId = equipmentDefinitionId;
        Quality = quality;
        QualityScore = qualityScore;
        CrafterMercenaryId = crafterMercenaryId;
        CrafterProductionSkillSnapshot = crafterProductionSkillSnapshot;
        OwnerCompanyId = ownerCompanyId;
        CreatedSessionRevision = createdSessionRevision;
        LocationKind = EquipmentLocationKindV3.Unplaced;
    }

    public string EquipmentInstanceId { get; }
    public string EquipmentDefinitionId { get; }
    public EquipmentQualityV3 Quality { get; }
    public int QualityScore { get; }
    public string CrafterMercenaryId { get; }
    public int CrafterProductionSkillSnapshot { get; }
    public string OwnerCompanyId { get; }
    public EquipmentLocationKindV3 LocationKind { get; private set; }
    public Vector2I? GroundCell { get; private set; }
    public string? StorageId { get; private set; }
    public Vector2I? StorageCell { get; private set; }
    public string? EquippedMercenaryId { get; private set; }
    public EquipmentSlotV3? EquippedSlot { get; private set; }
    public string? FacilityId { get; private set; }
    public string? RegionId { get; private set; }
    public long CreatedSessionRevision { get; }

    internal bool TryPlaceInFacilityOutput(string facilityId,string regionId)
    {
        if (LocationKind != EquipmentLocationKindV3.Unplaced || string.IsNullOrWhiteSpace(facilityId)||string.IsNullOrWhiteSpace(regionId)) return false;
        LocationKind = EquipmentLocationKindV3.FacilityOutput;
        FacilityId = facilityId;
        RegionId=regionId;
        GroundCell = null;
        StorageId = null;
        StorageCell = null;
        EquippedMercenaryId = null;
        EquippedSlot = null;
        return true;
    }

    internal bool TryReleaseFromFacilityOutput(string facilityId)
    {
        if (LocationKind != EquipmentLocationKindV3.FacilityOutput || FacilityId != facilityId) return false;
        LocationKind = EquipmentLocationKindV3.Unplaced;
        FacilityId = null;
        RegionId=null;
        return true;
    }

    internal void MoveToGround(Vector2I cell,string regionId)
    {
        LocationKind = EquipmentLocationKindV3.Ground;
        RegionId=regionId;
        GroundCell = cell;
        StorageId = null;
        StorageCell = null;
        FacilityId = null;
        EquippedMercenaryId = null;
        EquippedSlot = null;
    }

    internal void MoveToStorage(string storageId,Vector2I cell,string regionId)
    {
        LocationKind = EquipmentLocationKindV3.Storage;
        RegionId=regionId;
        StorageId = storageId;
        StorageCell = cell;
        GroundCell = null;
        FacilityId = null;
        EquippedMercenaryId = null;
        EquippedSlot = null;
    }

    internal void MoveToCompanyHolding()
    {
        LocationKind = EquipmentLocationKindV3.CompanyHolding;
        RegionId=null;
        FacilityId = null;
        GroundCell = null;
        StorageId = null;
        StorageCell = null;
        EquippedMercenaryId = null;
        EquippedSlot = null;
    }

    internal void MoveToEquipped(string mercenaryId,EquipmentSlotV3 slot,string regionId)
    {
        LocationKind = EquipmentLocationKindV3.Equipped;
        RegionId = regionId;
        FacilityId = null;
        GroundCell = null;
        StorageId = null;
        StorageCell = null;
        EquippedMercenaryId = mercenaryId;
        EquippedSlot = slot;
    }
    internal bool TrySetEquippedRegion(string mercenaryId,string? regionId)
    {
        if(LocationKind!=EquipmentLocationKindV3.Equipped||EquippedMercenaryId!=mercenaryId)return false;
        RegionId=regionId;return true;
    }
}
