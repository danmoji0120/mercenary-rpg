using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class EquipmentStatSummary
{
    private readonly IReadOnlyList<string> _missingInstanceIds;
    private readonly IReadOnlyList<string> _missingDefinitionIds;

    public EquipmentStatSummary(
        int attackBonus,
        int defenseBonus,
        int equippedCount,
        IEnumerable<string>? missingInstanceIds = null,
        IEnumerable<string>? missingDefinitionIds = null)
    {
        AttackBonus = attackBonus;
        DefenseBonus = defenseBonus;
        EquippedCount = equippedCount;
        _missingInstanceIds = CopyIds(missingInstanceIds);
        _missingDefinitionIds = CopyIds(missingDefinitionIds);
    }

    public int AttackBonus { get; }
    public int DefenseBonus { get; }
    public int EquippedCount { get; }
    public IReadOnlyList<string> MissingInstanceIds => _missingInstanceIds;
    public IReadOnlyList<string> MissingDefinitionIds => _missingDefinitionIds;
    public bool HasAnyBonus => AttackBonus != 0 || DefenseBonus != 0;
    public bool HasMissingReferences => MissingInstanceIds.Count > 0 || MissingDefinitionIds.Count > 0;

    public string GetShortSummary()
    {
        if (!HasAnyBonus)
        {
            return "None";
        }

        List<string> parts = new();

        if (AttackBonus != 0)
        {
            parts.Add($"Attack +{AttackBonus}");
        }

        if (DefenseBonus != 0)
        {
            parts.Add($"Defense +{DefenseBonus}");
        }

        return parts.Count == 0 ? "None" : string.Join(" / ", parts);
    }

    private static IReadOnlyList<string> CopyIds(IEnumerable<string>? ids)
    {
        List<string> copiedIds = new();

        if (ids == null)
        {
            return copiedIds;
        }

        foreach (string id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                copiedIds.Add(id.Trim());
            }
        }

        return new ReadOnlyCollection<string>(copiedIds);
    }
}
