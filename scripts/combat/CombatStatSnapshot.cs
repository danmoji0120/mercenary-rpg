public sealed class CombatStatSnapshot
{
    public CombatStatSnapshot(
        string mercenaryId,
        string displayName,
        int baseAttackPower,
        int baseDefensePower,
        int equipmentAttackBonus,
        int equipmentDefenseBonus,
        int equipmentCount,
        int combatSkill,
        int strength,
        int dexterity,
        int endurance,
        int maxHealth)
    {
        MercenaryId = mercenaryId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        BaseAttackPower = baseAttackPower;
        BaseDefensePower = baseDefensePower;
        EquipmentAttackBonus = equipmentAttackBonus;
        EquipmentDefenseBonus = equipmentDefenseBonus;
        AttackPower = baseAttackPower + equipmentAttackBonus;
        DefensePower = baseDefensePower + equipmentDefenseBonus;
        EquipmentCount = equipmentCount;
        CombatSkill = combatSkill;
        Strength = strength;
        Dexterity = dexterity;
        Endurance = endurance;
        MaxHealth = maxHealth;
    }

    public string MercenaryId { get; }
    public string DisplayName { get; }
    public int BaseAttackPower { get; }
    public int BaseDefensePower { get; }
    public int EquipmentAttackBonus { get; }
    public int EquipmentDefenseBonus { get; }
    public int AttackPower { get; }
    public int DefensePower { get; }
    public int EquipmentCount { get; }
    public bool HasEquipmentBonus => EquipmentAttackBonus != 0 || EquipmentDefenseBonus != 0;
    public int CombatSkill { get; }
    public int Strength { get; }
    public int Dexterity { get; }
    public int Endurance { get; }
    public int MaxHealth { get; }
}
