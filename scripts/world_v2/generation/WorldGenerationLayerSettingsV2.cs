namespace WorldV2;

public static class WorldGenerationLayerSettingsV2
{
    public static bool EnableBiomes { get; set; } = true;
    public static bool EnableVillages { get; set; } = true;
    public static bool EnableRoads { get; set; } = true;
    public static bool EnableForests { get; set; } = true;
    public static bool EnableQuarries { get; set; } = true;
    public static bool EnableRuins { get; set; } = true;
    public static bool EnableDungeons { get; set; } = true;
    public static bool EnableBanditCamps { get; set; } = true;
    public static bool EnableFactionOutposts { get; set; } = true;
    public static bool EnableRivers { get; set; } = true;

    public static bool HasAnyEnabledLayer =>
        EnableBiomes
        || EnableVillages
        || EnableRoads
        || EnableForests
        || EnableQuarries
        || EnableRuins
        || EnableDungeons
        || EnableBanditCamps
        || EnableFactionOutposts
        || EnableRivers;

    public static bool IsLandmarkEnabled(LandmarkKindV2 kind)
    {
        return kind switch
        {
            LandmarkKindV2.Ruin => EnableRuins,
            LandmarkKindV2.Dungeon => EnableDungeons,
            LandmarkKindV2.BanditCamp => EnableBanditCamps,
            LandmarkKindV2.FactionOutpost => EnableFactionOutposts,
            LandmarkKindV2.Quarry => EnableQuarries,
            LandmarkKindV2.Village or LandmarkKindV2.StartingVillage => EnableVillages,
            LandmarkKindV2.None => false,
            _ => false
        };
    }

    public static string GetSummary()
    {
        return $"layers: biomes={EnableBiomes} villages={EnableVillages} roads={EnableRoads} forests={EnableForests} quarries={EnableQuarries} ruins={EnableRuins} dungeons={EnableDungeons} bandits={EnableBanditCamps} faction={EnableFactionOutposts} rivers={EnableRivers}";
    }

    public static bool ToggleByNumber(int number, out string label)
    {
        switch (number)
        {
            case 0:
                EnableBiomes = !EnableBiomes;
                label = "Biomes";
                return true;
            case 1:
                EnableVillages = !EnableVillages;
                label = "Villages";
                return true;
            case 2:
                EnableRoads = !EnableRoads;
                label = "Roads";
                return true;
            case 3:
                EnableForests = !EnableForests;
                label = "Forests";
                return true;
            case 4:
                EnableQuarries = !EnableQuarries;
                label = "Quarries";
                return true;
            case 5:
                EnableRuins = !EnableRuins;
                label = "Ruins";
                return true;
            case 6:
                EnableDungeons = !EnableDungeons;
                label = "Dungeons";
                return true;
            case 7:
                EnableBanditCamps = !EnableBanditCamps;
                label = "BanditCamps";
                return true;
            case 8:
                EnableFactionOutposts = !EnableFactionOutposts;
                label = "FactionOutposts";
                return true;
            case 9:
                EnableRivers = !EnableRivers;
                label = "Rivers";
                return true;
            default:
                label = string.Empty;
                return false;
        }
    }
}
