using Godot;

namespace WorldV2;

public enum WorldGenerationPresetV2
{
    Balanced,
    MoreRivers,
    FewerRoads,
    SmallerBiomes,
    SaferStart
}

public partial class WorldGenerationSettingsV2 : Node
{
    public static readonly WorldGenerationSettingsV2 Default = new();

    [ExportGroup("Biome Scale")]
    [Export] public float ElevationMacroFrequency { get; set; } = 0.001584f;
    [Export] public float ElevationMidFrequency { get; set; } = 0.0068f;
    [Export] public float ElevationDetailFrequency { get; set; } = 0.0180f;
    [Export] public float TemperatureFrequency { get; set; } = 0.0022f;
    [Export] public float HumidityFrequency { get; set; } = 0.00253f;
    [Export] public float DangerFrequency { get; set; } = 0.0022f;
    [Export] public float RuinFrequency { get; set; } = 0.00209f;
    [Export] public float CivilizationFrequency { get; set; } = 0.001716f;
    [Export] public float ResourceFrequency { get; set; } = 0.00231f;

    [ExportGroup("River")]
    [Export] public float RiverWidth { get; set; } = 3.2f;
    [Export] public float RiverWarpStrength { get; set; } = 135.0f;
    [Export] public float RiverFrequency { get; set; } = 0.0022f;
    [Export] public float RiverHumidityRequirement { get; set; } = 0.30f;
    [Export] public float RiverOceanMargin { get; set; } = 0.04f;
    [Export] public float RiverMountainCutoff { get; set; } = 0.74f;

    [ExportGroup("Road")]
    [Export] public float RoadBandWidth { get; set; } = 0.026f;
    [Export] public float RoadWarpStrength { get; set; } = 96.0f;
    [Export] public float RoadCivilizationThreshold { get; set; } = 0.62f;
    [Export] public float RoadFrequency { get; set; } = 0.0046f;
    [Export] public float TradeRoadZoneThreshold { get; set; } = 0.70f;
    [Export] public float MaxGeneralRoadCoverageHint { get; set; } = 0.08f;

    [ExportGroup("Starting Area")]
    [Export] public int StartCenterX { get; set; } = 64;
    [Export] public int StartCenterY { get; set; } = 64;
    [Export] public float StartCoreRadius { get; set; } = 45.0f;
    [Export] public float StartInnerRadius { get; set; } = 80.0f;
    [Export] public float StartBlendRadius { get; set; } = 140.0f;
    [Export] public float StartSafeRadius { get; set; } = 180.0f;
    [Export] public float StartRoadExitRadius { get; set; } = 260.0f;
    [Export] public int StartRoadExitCount { get; set; } = 4;
    [Export] public float StartRoadWarpStrength { get; set; } = 26.0f;
    [Export] public float StartDangerSuppression { get; set; } = 0.25f;
    [Export] public float StartRiverSuppression { get; set; } = 0.18f;

    [ExportGroup("Thresholds")]
    [Export] public float OceanThreshold { get; set; } = 0.285f;
    [Export] public float CoastThreshold { get; set; } = 0.335f;
    [Export] public float HillThreshold { get; set; } = 0.71f;
    [Export] public float MountainThreshold { get; set; } = 0.84f;
    [Export] public float ToxicDangerThreshold { get; set; } = 0.72f;
    [Export] public float BanditDangerThreshold { get; set; } = 0.78f;
    [Export] public float MonsterDangerThreshold { get; set; } = 0.78f;
    [Export] public float RuinResidentialThreshold { get; set; } = 0.68f;
    [Export] public float RuinFactoryThreshold { get; set; } = 0.58f;

    [ExportGroup("Flatland")]
    [Export] public int RiverCount { get; set; } = 2;
    [Export] public float RiverBankWidth { get; set; } = 5.5f;
    [Export] public float RiverMeanderStrength { get; set; } = 115.0f;
    [Export] public int ForestClusterCount { get; set; } = 3;
    [Export] public float ForestClusterMinLength { get; set; } = 74.0f;
    [Export] public float ForestClusterMaxLength { get; set; } = 184.0f;
    [Export] public float ForestClusterMinWidth { get; set; } = 26.0f;
    [Export] public float ForestClusterMaxWidth { get; set; } = 70.0f;
    [Export] public int ForestGroveMinCount { get; set; } = 5;
    [Export] public int ForestGroveMaxCount { get; set; } = 12;
    [Export] public float ForestClearingStrength { get; set; } = 0.42f;
    [Export] public float RiverForestBonus { get; set; } = 0.22f;
    [Export] public float VillageRadius { get; set; } = 13.0f;
    [Export] public float StartVillageRadius { get; set; } = 18.0f;
    [Export] public float VillageMinDistance { get; set; } = 72.0f;
    [Export] public int LandmarkCountPerRegion { get; set; } = 3;
    [Export] public int QuarryCountPerRegion { get; set; } = 1;
    [Export] public float RoadWidth { get; set; } = 1.25f;
    [Export] public float RoadMeanderStrength { get; set; } = 58.0f;
    [Export] public float RoadForestPenalty { get; set; } = 12.0f;
    [Export] public float RoadRiverPenalty { get; set; } = 32.0f;
    [Export] public float RoadVillageAttraction { get; set; } = 0.55f;
    [Export] public float RuinRoadChance { get; set; } = 0.48f;
    [Export] public float DungeonRoadChance { get; set; } = 0.30f;
    [Export] public float BanditRoadChance { get; set; } = 0.28f;
    [Export] public float FactionRoadChance { get; set; } = 0.70f;
    [Export] public float QuarryRoadChance { get; set; } = 0.0f;

    [ExportGroup("V3 Villages")]
    [Export] public int V3SmallVillageCount { get; set; } = 9;
    [Export] public int V3MediumVillageCount { get; set; } = 24;
    [Export] public int V3LargeVillageCount { get; set; } = 56;
    [Export] public float V3VillageEdgeMargin { get; set; } = 160.0f;
    [Export] public float V3VillageMinDistance { get; set; } = 210.0f;
    [Export] public int V3VillagePlacementMaxAttemptsPerVillage { get; set; } = 80;
    [Export] public float V3HamletRadius { get; set; } = 18.0f;
    [Export] public float V3VillageRadius { get; set; } = 25.0f;
    [Export] public float V3LargeVillageRadius { get; set; } = 34.0f;
    [Export] public float V3TownRadius { get; set; } = 44.0f;
    [Export] public float V3RoadWidth { get; set; } = 2.25f;
    [Export] public float V3RoadMeanderStrength { get; set; } = 48.0f;
    [Export] public float V3RoadExtraLinkRatioSmall { get; set; } = 0.18f;
    [Export] public float V3RoadExtraLinkRatioMedium { get; set; } = 0.22f;
    [Export] public float V3RoadExtraLinkRatioLarge { get; set; } = 0.26f;
    [Export] public float V3RoadWearFrequency { get; set; } = 0.23f;
    [Export] public int V3SmallForestClusterCount { get; set; } = 28;
    [Export] public int V3MediumForestClusterCount { get; set; } = 72;
    [Export] public int V3LargeForestClusterCount { get; set; } = 156;
    [Export] public float V3ForestClusterMinLength { get; set; } = 240.0f;
    [Export] public float V3ForestClusterMaxLength { get; set; } = 760.0f;
    [Export] public float V3ForestClusterMinWidth { get; set; } = 100.0f;
    [Export] public float V3ForestClusterMaxWidth { get; set; } = 290.0f;
    [Export] public float V3ForestDensity { get; set; } = 0.82f;
    [Export] public float V3ForestEdgeNoiseStrength { get; set; } = 0.28f;
    [Export] public float V3ForestVillageClearRadius { get; set; } = 62.0f;
    [Export] public float V3ForestRoadClearRadius { get; set; } = 6.0f;
    [Export] public int V3ForestMinLobes { get; set; } = 3;
    [Export] public int V3ForestMaxLobes { get; set; } = 7;
    [Export] public float V3LargeForestChance { get; set; } = 0.20f;
    [Export] public int V3SmallMajorForestMinCount { get; set; } = 3;
    [Export] public int V3SmallMajorForestMaxCount { get; set; } = 5;
    [Export] public int V3SmallMinorForestMinCount { get; set; } = 8;
    [Export] public int V3SmallMinorForestMaxCount { get; set; } = 16;
    [Export] public int V3MediumMajorForestMinCount { get; set; } = 8;
    [Export] public int V3MediumMajorForestMaxCount { get; set; } = 14;
    [Export] public int V3MediumMinorForestMinCount { get; set; } = 25;
    [Export] public int V3MediumMinorForestMaxCount { get; set; } = 45;
    [Export] public int V3LargeMajorForestMinCount { get; set; } = 18;
    [Export] public int V3LargeMajorForestMaxCount { get; set; } = 32;
    [Export] public int V3LargeMinorForestMinCount { get; set; } = 70;
    [Export] public int V3LargeMinorForestMaxCount { get; set; } = 120;
    [Export] public float V3MajorForestMinRadius { get; set; } = 180.0f;
    [Export] public float V3MajorForestMaxRadius { get; set; } = 520.0f;
    [Export] public float V3MinorForestMinRadius { get; set; } = 48.0f;
    [Export] public float V3MinorForestMaxRadius { get; set; } = 145.0f;
    [Export] public float V3ForestPotentialThreshold { get; set; } = 0.50f;
    [Export] public float V3MajorForestNoiseScale { get; set; } = 0.010f;
    [Export] public float V3MinorForestNoiseScale { get; set; } = 0.020f;
    [Export] public float V3MajorForestWarpStrength { get; set; } = 64.0f;
    [Export] public float V3MinorForestWarpStrength { get; set; } = 26.0f;

    public Vector2I StartCenter => new(StartCenterX, StartCenterY);

    public StartingAreaProfileV2 CreateStartingAreaProfile()
    {
        return new StartingAreaProfileV2
        {
            Center = StartCenter,
            CoreRadius = Mathf.Max(1.0f, StartCoreRadius),
            InnerRadius = Mathf.Max(StartCoreRadius, StartInnerRadius),
            BlendRadius = Mathf.Max(StartInnerRadius, StartBlendRadius),
            SafeRadius = Mathf.Max(StartBlendRadius, StartSafeRadius),
            RoadExitRadius = Mathf.Max(StartInnerRadius, StartRoadExitRadius),
            RoadExitCount = Mathf.Max(3, StartRoadExitCount),
            RoadExitWarpStrength = Mathf.Max(0.0f, StartRoadWarpStrength),
            DangerSuppression = Mathf.Clamp(StartDangerSuppression, 0.0f, 1.0f),
            RiverSuppression = Mathf.Clamp(StartRiverSuppression, 0.0f, 1.0f)
        };
    }

    public void ApplyPreset(WorldGenerationPresetV2 preset)
    {
        switch (preset)
        {
            case WorldGenerationPresetV2.MoreRivers:
                RiverCount = 3;
                RiverWidth = 4.0f;
                RiverHumidityRequirement = 0.22f;
                RiverWarpStrength = 160.0f;
                break;
            case WorldGenerationPresetV2.FewerRoads:
                RoadWidth = 1.05f;
                RuinRoadChance = 0.34f;
                DungeonRoadChance = 0.20f;
                BanditRoadChance = 0.18f;
                FactionRoadChance = 0.56f;
                RoadBandWidth = 0.016f;
                RoadCivilizationThreshold = 0.70f;
                TradeRoadZoneThreshold = 0.76f;
                MaxGeneralRoadCoverageHint = 0.04f;
                break;
            case WorldGenerationPresetV2.SmallerBiomes:
                ForestClusterCount = 4;
                ForestClusterMinLength = 58.0f;
                ForestClusterMaxLength = 132.0f;
                ForestClusterMinWidth = 22.0f;
                ForestClusterMaxWidth = 54.0f;
                ElevationMidFrequency = 0.0092f;
                HumidityFrequency = 0.0034f;
                TemperatureFrequency = 0.0030f;
                RuinFrequency = 0.0030f;
                break;
            case WorldGenerationPresetV2.SaferStart:
                StartSafeRadius = 240.0f;
                StartBlendRadius = 170.0f;
                StartDangerSuppression = 0.12f;
                StartRiverSuppression = 0.08f;
                break;
            case WorldGenerationPresetV2.Balanced:
            default:
                ResetToBalanced();
                break;
        }
    }

    public void ResetToBalanced()
    {
        ElevationMacroFrequency = 0.001584f;
        ElevationMidFrequency = 0.0068f;
        ElevationDetailFrequency = 0.0180f;
        TemperatureFrequency = 0.0022f;
        HumidityFrequency = 0.00253f;
        DangerFrequency = 0.0022f;
        RuinFrequency = 0.00209f;
        CivilizationFrequency = 0.001716f;
        ResourceFrequency = 0.00231f;
        RiverWidth = 3.2f;
        RiverWarpStrength = 135.0f;
        RiverFrequency = 0.0022f;
        RiverHumidityRequirement = 0.30f;
        RiverOceanMargin = 0.04f;
        RiverMountainCutoff = 0.74f;
        RoadBandWidth = 0.026f;
        RoadWarpStrength = 96.0f;
        RoadCivilizationThreshold = 0.62f;
        RoadFrequency = 0.0046f;
        TradeRoadZoneThreshold = 0.70f;
        MaxGeneralRoadCoverageHint = 0.08f;
        StartCenterX = 64;
        StartCenterY = 64;
        StartCoreRadius = 45.0f;
        StartInnerRadius = 80.0f;
        StartBlendRadius = 140.0f;
        StartSafeRadius = 180.0f;
        StartRoadExitRadius = 260.0f;
        StartRoadExitCount = 4;
        StartRoadWarpStrength = 26.0f;
        StartDangerSuppression = 0.25f;
        StartRiverSuppression = 0.18f;
        OceanThreshold = 0.285f;
        CoastThreshold = 0.335f;
        HillThreshold = 0.71f;
        MountainThreshold = 0.84f;
        ToxicDangerThreshold = 0.72f;
        BanditDangerThreshold = 0.78f;
        MonsterDangerThreshold = 0.78f;
        RuinResidentialThreshold = 0.68f;
        RuinFactoryThreshold = 0.58f;
        RiverCount = 2;
        RiverBankWidth = 5.5f;
        RiverMeanderStrength = 115.0f;
        ForestClusterCount = 3;
        ForestClusterMinLength = 74.0f;
        ForestClusterMaxLength = 184.0f;
        ForestClusterMinWidth = 26.0f;
        ForestClusterMaxWidth = 70.0f;
        ForestGroveMinCount = 5;
        ForestGroveMaxCount = 12;
        ForestClearingStrength = 0.42f;
        RiverForestBonus = 0.22f;
        VillageRadius = 13.0f;
        StartVillageRadius = 18.0f;
        VillageMinDistance = 72.0f;
        LandmarkCountPerRegion = 3;
        QuarryCountPerRegion = 1;
        RoadWidth = 1.25f;
        RoadMeanderStrength = 58.0f;
        RoadForestPenalty = 12.0f;
        RoadRiverPenalty = 32.0f;
        RoadVillageAttraction = 0.55f;
        RuinRoadChance = 0.48f;
        DungeonRoadChance = 0.30f;
        BanditRoadChance = 0.28f;
        FactionRoadChance = 0.70f;
        QuarryRoadChance = 0.0f;
        V3SmallVillageCount = 9;
        V3MediumVillageCount = 24;
        V3LargeVillageCount = 56;
        V3VillageEdgeMargin = 160.0f;
        V3VillageMinDistance = 210.0f;
        V3VillagePlacementMaxAttemptsPerVillage = 80;
        V3HamletRadius = 18.0f;
        V3VillageRadius = 25.0f;
        V3LargeVillageRadius = 34.0f;
        V3TownRadius = 44.0f;
        V3RoadWidth = 2.25f;
        V3RoadMeanderStrength = 48.0f;
        V3RoadExtraLinkRatioSmall = 0.18f;
        V3RoadExtraLinkRatioMedium = 0.22f;
        V3RoadExtraLinkRatioLarge = 0.26f;
        V3RoadWearFrequency = 0.23f;
        V3SmallForestClusterCount = 28;
        V3MediumForestClusterCount = 72;
        V3LargeForestClusterCount = 156;
        V3ForestClusterMinLength = 240.0f;
        V3ForestClusterMaxLength = 760.0f;
        V3ForestClusterMinWidth = 100.0f;
        V3ForestClusterMaxWidth = 290.0f;
        V3ForestDensity = 0.82f;
        V3ForestEdgeNoiseStrength = 0.28f;
        V3ForestVillageClearRadius = 62.0f;
        V3ForestRoadClearRadius = 6.0f;
        V3ForestMinLobes = 3;
        V3ForestMaxLobes = 7;
        V3LargeForestChance = 0.20f;
        V3SmallMajorForestMinCount = 3;
        V3SmallMajorForestMaxCount = 5;
        V3SmallMinorForestMinCount = 8;
        V3SmallMinorForestMaxCount = 16;
        V3MediumMajorForestMinCount = 8;
        V3MediumMajorForestMaxCount = 14;
        V3MediumMinorForestMinCount = 25;
        V3MediumMinorForestMaxCount = 45;
        V3LargeMajorForestMinCount = 18;
        V3LargeMajorForestMaxCount = 32;
        V3LargeMinorForestMinCount = 70;
        V3LargeMinorForestMaxCount = 120;
        V3MajorForestMinRadius = 180.0f;
        V3MajorForestMaxRadius = 520.0f;
        V3MinorForestMinRadius = 48.0f;
        V3MinorForestMaxRadius = 145.0f;
        V3ForestPotentialThreshold = 0.50f;
        V3MajorForestNoiseScale = 0.010f;
        V3MinorForestNoiseScale = 0.020f;
        V3MajorForestWarpStrength = 64.0f;
        V3MinorForestWarpStrength = 26.0f;
    }
}
