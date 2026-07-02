public static class CombatStatBuilder
{
    private const int CurrentDefenseCombatBaseAttackPower = 1;
    private const int CurrentDefenseCombatBaseDefensePower = 0;

    public static CombatStatSnapshot BuildForMercenary(
        MercenaryController mercenary,
        EquipmentInventoryManager? equipmentInventory,
        MercenaryEquipmentLoadoutManager? loadoutManager)
    {
        if (mercenary == null)
        {
            return BuildEmpty();
        }

        string mercenaryId = GetMercenaryId(mercenary);
        return BuildForProfile(
            mercenary.Profile,
            mercenaryId,
            equipmentInventory,
            loadoutManager);
    }

    public static CombatStatSnapshot BuildForProfile(
        MercenaryProfile profile,
        string fallbackMercenaryId,
        EquipmentInventoryManager? equipmentInventory,
        MercenaryEquipmentLoadoutManager? loadoutManager)
    {
        if (profile == null)
        {
            return BuildEmpty();
        }

        string mercenaryId = string.IsNullOrWhiteSpace(profile.MercenaryId)
            ? fallbackMercenaryId?.Trim() ?? string.Empty
            : profile.MercenaryId.Trim();
        MercenaryStats stats = profile.Stats ?? new MercenaryStats();
        EquipmentStatSummary equipmentStats = EquipmentStatCalculator.Calculate(
            mercenaryId,
            loadoutManager,
            equipmentInventory);

        return new CombatStatSnapshot(
            mercenaryId,
            profile.DisplayName,
            CurrentDefenseCombatBaseAttackPower,
            CurrentDefenseCombatBaseDefensePower,
            equipmentStats.AttackBonus,
            equipmentStats.DefenseBonus,
            equipmentStats.EquippedCount,
            stats.CombatSkill,
            stats.Strength,
            stats.Dexterity,
            stats.Endurance,
            stats.MaxHealth);
    }

    private static CombatStatSnapshot BuildEmpty()
    {
        return new CombatStatSnapshot(
            string.Empty,
            string.Empty,
            CurrentDefenseCombatBaseAttackPower,
            CurrentDefenseCombatBaseDefensePower,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
    }

    private static string GetMercenaryId(MercenaryController mercenary)
    {
        // The fallback is runtime-only and should not be used as a persisted combat identity.
        return string.IsNullOrWhiteSpace(mercenary.Profile.MercenaryId)
            ? mercenary.GetInstanceId().ToString()
            : mercenary.Profile.MercenaryId;
    }
}
