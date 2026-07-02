using System.Collections.Generic;

public static class EquipmentStatCalculator
{
    public static EquipmentStatSummary Calculate(
        string mercenaryId,
        MercenaryEquipmentLoadoutManager? loadoutManager,
        EquipmentInventoryManager? equipmentInventory)
    {
        if (string.IsNullOrWhiteSpace(mercenaryId)
            || loadoutManager == null
            || equipmentInventory == null
            || !loadoutManager.TryGetLoadout(mercenaryId, out MercenaryEquipmentLoadout? loadout)
            || loadout == null)
        {
            return new EquipmentStatSummary(0, 0, 0);
        }

        int attackBonus = 0;
        int defenseBonus = 0;
        int equippedCount = 0;
        List<string> missingInstanceIds = new();
        List<string> missingDefinitionIds = new();

        foreach (string instanceId in loadout.GetAllEquippedInstanceIds())
        {
            if (!equipmentInventory.TryGetEquipment(instanceId, out EquipmentInstance? instance) || instance == null)
            {
                missingInstanceIds.Add(instanceId);
                continue;
            }

            if (!EquipmentDefinitionDatabase.TryGet(instance.DefinitionId, out EquipmentDefinitionEntry? definition)
                || definition == null)
            {
                missingDefinitionIds.Add(instance.DefinitionId);
                continue;
            }

            attackBonus += definition.AttackBonus;
            defenseBonus += definition.DefenseBonus;
            equippedCount++;
        }

        return new EquipmentStatSummary(
            attackBonus,
            defenseBonus,
            equippedCount,
            missingInstanceIds,
            missingDefinitionIds);
    }
}
