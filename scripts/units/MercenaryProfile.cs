using System.Collections.Generic;

public sealed class MercenaryProfile
{
    public string MercenaryId { get; set; } = "mercenary_default";
    public string DisplayName { get; set; } = "Mercenary";
    public MercenaryRank Rank { get; set; } = MercenaryRank.N;
    public MercenaryRace Race { get; set; } = MercenaryRace.Human;
    public MercenaryRole PrimaryRole { get; set; } = MercenaryRole.Worker;
    public MercenaryRole SecondaryRole { get; set; } = MercenaryRole.Hauler;
    public MercenaryStats Stats { get; set; } = new();
    public MercenaryCondition Condition { get; set; } = new();
    public MercenaryWorkSettings WorkSettings { get; set; } = MercenaryWorkSettings.CreateDefault();
    public List<string> Traits { get; } = new();
    public string ShortDescription { get; set; } = "";

    public int GetCarryCapacityFallback(int fallback)
    {
        return Stats.MaxCarryWeight > 0 ? Stats.MaxCarryWeight : fallback;
    }

    public MercenaryWorkSettings GetWorkSettings()
    {
        WorkSettings ??= MercenaryWorkSettings.CreateForRoles(PrimaryRole, SecondaryRole);
        return WorkSettings;
    }
}
