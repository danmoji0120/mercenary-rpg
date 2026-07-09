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

    [ExportGroup("V3 Biomes")]
    [Export] public int V3SmallMajorBiomeMinCount { get; set; } = 2;
    [Export] public int V3SmallMajorBiomeMaxCount { get; set; } = 3;
    [Export] public int V3SmallMinorBiomeMinCount { get; set; } = 1;
    [Export] public int V3SmallMinorBiomeMaxCount { get; set; } = 2;
    [Export] public int V3MediumMajorBiomeMinCount { get; set; } = 3;
    [Export] public int V3MediumMajorBiomeMaxCount { get; set; } = 5;
    [Export] public int V3MediumMinorBiomeMinCount { get; set; } = 2;
    [Export] public int V3MediumMinorBiomeMaxCount { get; set; } = 4;
    [Export] public int V3LargeMajorBiomeMinCount { get; set; } = 5;
    [Export] public int V3LargeMajorBiomeMaxCount { get; set; } = 7;
    [Export] public int V3LargeMinorBiomeMinCount { get; set; } = 3;
    [Export] public int V3LargeMinorBiomeMaxCount { get; set; } = 6;
    [Export] public int V3HugeMajorBiomeMinCount { get; set; } = 6;
    [Export] public int V3HugeMajorBiomeMaxCount { get; set; } = 9;
    [Export] public int V3HugeMinorBiomeMinCount { get; set; } = 4;
    [Export] public int V3HugeMinorBiomeMaxCount { get; set; } = 8;
    [Export] public float V3MajorBiomeMinRadiusRatio { get; set; } = 0.14f;
    [Export] public float V3MajorBiomeMaxRadiusRatio { get; set; } = 0.27f;
    [Export] public float V3MinorBiomeMinRadiusRatio { get; set; } = 0.05f;
    [Export] public float V3MinorBiomeMaxRadiusRatio { get; set; } = 0.105f;
    [Export] public float V3BiomePotentialThreshold { get; set; } = 0.43f;
    [Export] public float V3MajorBiomeNoiseScale { get; set; } = 0.00115f;
    [Export] public float V3MinorBiomeNoiseScale { get; set; } = 0.00185f;
    [Export] public float V3MajorBiomeWarpStrength { get; set; } = 140.0f;
    [Export] public float V3MinorBiomeWarpStrength { get; set; } = 55.0f;

    [ExportGroup("V3 Biome Feature Weights")]
    [Export] public float V3PlainsForestWeightMultiplier { get; set; } = 1.00f;
    [Export] public float V3PlainsQuarryWeightMultiplier { get; set; } = 1.00f;
    [Export] public float V3PlainsRuinWeightMultiplier { get; set; } = 1.00f;
    [Export] public float V3ForestLandForestWeightMultiplier { get; set; } = 1.45f;
    [Export] public float V3ForestLandQuarryWeightMultiplier { get; set; } = 0.85f;
    [Export] public float V3ForestLandRuinWeightMultiplier { get; set; } = 0.85f;
    [Export] public float V3RockyHillsForestWeightMultiplier { get; set; } = 0.85f;
    [Export] public float V3RockyHillsQuarryWeightMultiplier { get; set; } = 1.55f;
    [Export] public float V3RockyHillsRuinWeightMultiplier { get; set; } = 1.05f;
    [Export] public float V3DrylandForestWeightMultiplier { get; set; } = 0.65f;
    [Export] public float V3DrylandQuarryWeightMultiplier { get; set; } = 1.05f;
    [Export] public float V3DrylandRuinWeightMultiplier { get; set; } = 1.15f;
    [Export] public float V3WastelandForestWeightMultiplier { get; set; } = 0.45f;
    [Export] public float V3WastelandQuarryWeightMultiplier { get; set; } = 1.10f;
    [Export] public float V3WastelandRuinWeightMultiplier { get; set; } = 1.60f;

    [ExportGroup("V3 Villages")]
    [Export] public int V3SmallVillageCount { get; set; } = 9;
    [Export] public int V3MediumVillageCount { get; set; } = 24;
    [Export] public int V3LargeVillageCount { get; set; } = 56;
    [Export] public int V3HugeVillageCount { get; set; } = 140;
    [Export] public float V3VillageEdgeMargin { get; set; } = 160.0f;
    [Export] public float V3VillageMinDistance { get; set; } = 210.0f;
    [Export] public int V3VillagePlacementMaxAttemptsPerVillage { get; set; } = 80;
    [Export] public float V3HamletRadius { get; set; } = 18.0f;
    [Export] public float V3VillageRadius { get; set; } = 25.0f;
    [Export] public float V3LargeVillageRadius { get; set; } = 34.0f;
    [Export] public float V3TownRadius { get; set; } = 44.0f;
    [Export] public float V3CityCandidateRadius { get; set; } = 56.0f;
    [Export] public int V3SmallCityCandidateCount { get; set; } = 0;
    [Export] public int V3SmallTownCount { get; set; } = 1;
    [Export] public int V3SmallLargeVillageCount { get; set; } = 1;
    [Export] public int V3MediumCityCandidateCount { get; set; } = 1;
    [Export] public int V3MediumTownCount { get; set; } = 2;
    [Export] public int V3MediumLargeVillageCount { get; set; } = 5;
    [Export] public int V3LargeCityCandidateCount { get; set; } = 1;
    [Export] public int V3LargeTownCount { get; set; } = 5;
    [Export] public int V3LargeLargeVillageCount { get; set; } = 12;
    [Export] public int V3HugeCityCandidateCount { get; set; } = 3;
    [Export] public int V3HugeTownCount { get; set; } = 12;
    [Export] public int V3HugeLargeVillageCount { get; set; } = 30;
    [Export] public float V3RoadWidth { get; set; } = 2.25f;
    [Export] public float V3RoadMeanderStrength { get; set; } = 48.0f;
    [Export] public float V3RoadExtraLinkRatioSmall { get; set; } = 0.18f;
    [Export] public float V3RoadExtraLinkRatioMedium { get; set; } = 0.22f;
    [Export] public float V3RoadExtraLinkRatioLarge { get; set; } = 0.26f;
    [Export] public float V3RoadExtraLinkRatioHuge { get; set; } = 0.08f;
    [Export] public float V3RoadWearFrequency { get; set; } = 0.23f;
    [Export] public float V3RoadDirectionSectorDegrees { get; set; } = 45.0f;
    [Export] public float V3RoadJunctionDistanceRatio { get; set; } = 0.58f;
    [Export] public float V3RoadJunctionMergeRadius { get; set; } = 96.0f;
    [Export] public float V3RoadJunctionVillageClearance { get; set; } = 72.0f;
    [Export] public int V3RoadNearestNeighborCount { get; set; } = 3;
    [Export] public float V3RoadExtraEdgeRatio { get; set; } = 0.08f;
    [Export] public bool V3SharedExitTrunkEnabled { get; set; } = true;
    [Export] public float V3SharedExitTrunkMaxLengthSmall { get; set; } = 90.0f;
    [Export] public float V3SharedExitTrunkMaxLengthMedium { get; set; } = 130.0f;
    [Export] public float V3SharedExitTrunkMaxLengthLarge { get; set; } = 180.0f;
    [Export] public float V3SharedExitTrunkMaxLengthHuge { get; set; } = 220.0f;
    [Export] public int V3MaxRoadJunctionDegree { get; set; } = 3;
    [Export] public int V3MaxRoadCrossingsPerEdge { get; set; } = 1;
    [Export] public int V3SmallBranchRoadMinCount { get; set; } = 0;
    [Export] public int V3SmallBranchRoadMaxCount { get; set; } = 2;
    [Export] public int V3MediumBranchRoadMinCount { get; set; } = 4;
    [Export] public int V3MediumBranchRoadMaxCount { get; set; } = 8;
    [Export] public int V3LargeBranchRoadMinCount { get; set; } = 12;
    [Export] public int V3LargeBranchRoadMaxCount { get; set; } = 24;
    [Export] public int V3HugeBranchRoadMinCount { get; set; } = 24;
    [Export] public int V3HugeBranchRoadMaxCount { get; set; } = 48;
    [Export] public float V3BranchRoadMinLength { get; set; } = 120.0f;
    [Export] public float V3BranchRoadMaxLength { get; set; } = 420.0f;
    [Export] public float V3BranchRoadWidthMultiplier { get; set; } = 0.62f;
    [Export] public float V3BranchRoadMeanderMultiplier { get; set; } = 1.45f;
    [Export] public int V3SmallForestClusterCount { get; set; } = 28;
    [Export] public int V3MediumForestClusterCount { get; set; } = 72;
    [Export] public int V3LargeForestClusterCount { get; set; } = 156;
    [Export] public int V3HugeForestClusterCount { get; set; } = 280;
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
    [Export] public int V3HugeMajorForestMinCount { get; set; } = 44;
    [Export] public int V3HugeMajorForestMaxCount { get; set; } = 56;
    [Export] public int V3HugeMinorForestMinCount { get; set; } = 200;
    [Export] public int V3HugeMinorForestMaxCount { get; set; } = 240;
    [Export] public float V3MajorForestMinRadius { get; set; } = 180.0f;
    [Export] public float V3MajorForestMaxRadius { get; set; } = 520.0f;
    [Export] public float V3MinorForestMinRadius { get; set; } = 48.0f;
    [Export] public float V3MinorForestMaxRadius { get; set; } = 145.0f;
    [Export] public float V3ForestPotentialThreshold { get; set; } = 0.50f;
    [Export] public float V3MajorForestNoiseScale { get; set; } = 0.010f;
    [Export] public float V3MinorForestNoiseScale { get; set; } = 0.020f;
    [Export] public float V3MajorForestWarpStrength { get; set; } = 64.0f;
    [Export] public float V3MinorForestWarpStrength { get; set; } = 26.0f;
    [Export] public int V3SmallMajorQuarryMinCount { get; set; } = 1;
    [Export] public int V3SmallMajorQuarryMaxCount { get; set; } = 2;
    [Export] public int V3SmallMinorQuarryMinCount { get; set; } = 4;
    [Export] public int V3SmallMinorQuarryMaxCount { get; set; } = 6;
    [Export] public int V3MediumMajorQuarryMinCount { get; set; } = 3;
    [Export] public int V3MediumMajorQuarryMaxCount { get; set; } = 5;
    [Export] public int V3MediumMinorQuarryMinCount { get; set; } = 10;
    [Export] public int V3MediumMinorQuarryMaxCount { get; set; } = 16;
    [Export] public int V3LargeMajorQuarryMinCount { get; set; } = 8;
    [Export] public int V3LargeMajorQuarryMaxCount { get; set; } = 12;
    [Export] public int V3LargeMinorQuarryMinCount { get; set; } = 28;
    [Export] public int V3LargeMinorQuarryMaxCount { get; set; } = 45;
    [Export] public int V3HugeMajorQuarryMinCount { get; set; } = 18;
    [Export] public int V3HugeMajorQuarryMaxCount { get; set; } = 24;
    [Export] public int V3HugeMinorQuarryMinCount { get; set; } = 70;
    [Export] public int V3HugeMinorQuarryMaxCount { get; set; } = 90;
    [Export] public float V3MajorQuarryMinRadius { get; set; } = 70.0f;
    [Export] public float V3MajorQuarryMaxRadius { get; set; } = 145.0f;
    [Export] public float V3MinorQuarryMinRadius { get; set; } = 30.0f;
    [Export] public float V3MinorQuarryMaxRadius { get; set; } = 72.0f;
    [Export] public int V3MajorQuarryMinPatchCount { get; set; } = 5;
    [Export] public int V3MajorQuarryMaxPatchCount { get; set; } = 10;
    [Export] public int V3MinorQuarryMinPatchCount { get; set; } = 2;
    [Export] public int V3MinorQuarryMaxPatchCount { get; set; } = 5;
    [Export] public int V3QuarryPlacementMaxAttemptsPerCluster { get; set; } = 80;
    [Export] public float V3QuarryPotentialThreshold { get; set; } = 0.49f;
    [Export] public float V3MajorQuarryNoiseScale { get; set; } = 0.021f;
    [Export] public float V3MinorQuarryNoiseScale { get; set; } = 0.035f;
    [Export] public float V3MajorQuarryWarpStrength { get; set; } = 24.0f;
    [Export] public float V3MinorQuarryWarpStrength { get; set; } = 10.0f;
    [Export] public float V3QuarryOreSpotChance { get; set; } = 0.018f;
    [Export] public int V3SmallRuinMinCount { get; set; } = 3;
    [Export] public int V3SmallRuinMaxCount { get; set; } = 6;
    [Export] public int V3MediumRuinMinCount { get; set; } = 10;
    [Export] public int V3MediumRuinMaxCount { get; set; } = 18;
    [Export] public int V3LargeRuinMinCount { get; set; } = 28;
    [Export] public int V3LargeRuinMaxCount { get; set; } = 48;
    [Export] public int V3HugeRuinMinCount { get; set; } = 90;
    [Export] public int V3HugeRuinMaxCount { get; set; } = 110;
    [Export] public float V3RuinMinRadius { get; set; } = 13.0f;
    [Export] public float V3RuinMaxRadius { get; set; } = 34.0f;
    [Export] public int V3RuinPlacementMaxAttemptsPerSite { get; set; } = 90;
    [Export] public float V3RuinNoiseScale { get; set; } = 0.075f;
    [Export] public float V3RuinWarpStrength { get; set; } = 7.5f;

    [ExportGroup("V3 Dungeon Entrances")]
    [Export] public int V3SmallDungeonEntranceMinCount { get; set; } = 2;
    [Export] public int V3SmallDungeonEntranceMaxCount { get; set; } = 3;
    [Export] public int V3MediumDungeonEntranceMinCount { get; set; } = 5;
    [Export] public int V3MediumDungeonEntranceMaxCount { get; set; } = 8;
    [Export] public int V3LargeDungeonEntranceMinCount { get; set; } = 10;
    [Export] public int V3LargeDungeonEntranceMaxCount { get; set; } = 16;
    [Export] public int V3HugeDungeonEntranceMinCount { get; set; } = 28;
    [Export] public int V3HugeDungeonEntranceMaxCount { get; set; } = 32;
    [Export] public float V3DungeonEntranceMinRadius { get; set; } = 9.0f;
    [Export] public float V3DungeonEntranceMaxRadius { get; set; } = 18.0f;
    [Export] public float V3DungeonEntranceMinDistance { get; set; } = 280.0f;
    [Export] public int V3DungeonEntrancePlacementMaxAttemptsPerSite { get; set; } = 90;
    [Export] public float V3PlainsDungeonEntranceWeightMultiplier { get; set; } = 0.45f;
    [Export] public float V3ForestLandDungeonEntranceWeightMultiplier { get; set; } = 0.95f;
    [Export] public float V3RockyHillsDungeonEntranceWeightMultiplier { get; set; } = 1.55f;
    [Export] public float V3DrylandDungeonEntranceWeightMultiplier { get; set; } = 0.90f;
    [Export] public float V3WastelandDungeonEntranceWeightMultiplier { get; set; } = 1.45f;

    [ExportGroup("World Map Overlay")]
    [Export] public int WorldMapSmallTextureSize { get; set; } = 512;
    [Export] public int WorldMapMediumTextureSize { get; set; } = 768;
    [Export] public int WorldMapLargeTextureSize { get; set; } = 1024;
    [Export] public int WorldMapHugeTextureSize { get; set; } = 1536;

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
        V3SmallMajorBiomeMinCount = 2;
        V3SmallMajorBiomeMaxCount = 3;
        V3SmallMinorBiomeMinCount = 1;
        V3SmallMinorBiomeMaxCount = 2;
        V3MediumMajorBiomeMinCount = 3;
        V3MediumMajorBiomeMaxCount = 5;
        V3MediumMinorBiomeMinCount = 2;
        V3MediumMinorBiomeMaxCount = 4;
        V3LargeMajorBiomeMinCount = 5;
        V3LargeMajorBiomeMaxCount = 7;
        V3LargeMinorBiomeMinCount = 3;
        V3LargeMinorBiomeMaxCount = 6;
        V3HugeMajorBiomeMinCount = 6;
        V3HugeMajorBiomeMaxCount = 9;
        V3HugeMinorBiomeMinCount = 4;
        V3HugeMinorBiomeMaxCount = 8;
        V3MajorBiomeMinRadiusRatio = 0.14f;
        V3MajorBiomeMaxRadiusRatio = 0.27f;
        V3MinorBiomeMinRadiusRatio = 0.05f;
        V3MinorBiomeMaxRadiusRatio = 0.105f;
        V3BiomePotentialThreshold = 0.43f;
        V3MajorBiomeNoiseScale = 0.00115f;
        V3MinorBiomeNoiseScale = 0.00185f;
        V3MajorBiomeWarpStrength = 140.0f;
        V3MinorBiomeWarpStrength = 55.0f;
        V3PlainsForestWeightMultiplier = 1.00f;
        V3PlainsQuarryWeightMultiplier = 1.00f;
        V3PlainsRuinWeightMultiplier = 1.00f;
        V3ForestLandForestWeightMultiplier = 1.45f;
        V3ForestLandQuarryWeightMultiplier = 0.85f;
        V3ForestLandRuinWeightMultiplier = 0.85f;
        V3RockyHillsForestWeightMultiplier = 0.85f;
        V3RockyHillsQuarryWeightMultiplier = 1.55f;
        V3RockyHillsRuinWeightMultiplier = 1.05f;
        V3DrylandForestWeightMultiplier = 0.65f;
        V3DrylandQuarryWeightMultiplier = 1.05f;
        V3DrylandRuinWeightMultiplier = 1.15f;
        V3WastelandForestWeightMultiplier = 0.45f;
        V3WastelandQuarryWeightMultiplier = 1.10f;
        V3WastelandRuinWeightMultiplier = 1.60f;
        V3SmallVillageCount = 9;
        V3MediumVillageCount = 24;
        V3LargeVillageCount = 56;
        V3HugeVillageCount = 140;
        V3VillageEdgeMargin = 160.0f;
        V3VillageMinDistance = 210.0f;
        V3VillagePlacementMaxAttemptsPerVillage = 80;
        V3HamletRadius = 18.0f;
        V3VillageRadius = 25.0f;
        V3LargeVillageRadius = 34.0f;
        V3TownRadius = 44.0f;
        V3CityCandidateRadius = 56.0f;
        V3SmallCityCandidateCount = 0;
        V3SmallTownCount = 1;
        V3SmallLargeVillageCount = 1;
        V3MediumCityCandidateCount = 1;
        V3MediumTownCount = 2;
        V3MediumLargeVillageCount = 5;
        V3LargeCityCandidateCount = 1;
        V3LargeTownCount = 5;
        V3LargeLargeVillageCount = 12;
        V3HugeCityCandidateCount = 3;
        V3HugeTownCount = 12;
        V3HugeLargeVillageCount = 30;
        V3RoadWidth = 2.25f;
        V3RoadMeanderStrength = 48.0f;
        V3RoadExtraLinkRatioSmall = 0.18f;
        V3RoadExtraLinkRatioMedium = 0.22f;
        V3RoadExtraLinkRatioLarge = 0.26f;
        V3RoadExtraLinkRatioHuge = 0.08f;
        V3RoadWearFrequency = 0.23f;
        V3RoadDirectionSectorDegrees = 45.0f;
        V3RoadJunctionDistanceRatio = 0.58f;
        V3RoadJunctionMergeRadius = 96.0f;
        V3RoadJunctionVillageClearance = 72.0f;
        V3RoadNearestNeighborCount = 3;
        V3RoadExtraEdgeRatio = 0.08f;
        V3SharedExitTrunkEnabled = true;
        V3SharedExitTrunkMaxLengthSmall = 90.0f;
        V3SharedExitTrunkMaxLengthMedium = 130.0f;
        V3SharedExitTrunkMaxLengthLarge = 180.0f;
        V3SharedExitTrunkMaxLengthHuge = 220.0f;
        V3MaxRoadJunctionDegree = 3;
        V3MaxRoadCrossingsPerEdge = 1;
        V3SmallBranchRoadMinCount = 0;
        V3SmallBranchRoadMaxCount = 2;
        V3MediumBranchRoadMinCount = 4;
        V3MediumBranchRoadMaxCount = 8;
        V3LargeBranchRoadMinCount = 12;
        V3LargeBranchRoadMaxCount = 24;
        V3HugeBranchRoadMinCount = 24;
        V3HugeBranchRoadMaxCount = 48;
        V3BranchRoadMinLength = 120.0f;
        V3BranchRoadMaxLength = 420.0f;
        V3BranchRoadWidthMultiplier = 0.62f;
        V3BranchRoadMeanderMultiplier = 1.45f;
        V3SmallForestClusterCount = 28;
        V3MediumForestClusterCount = 72;
        V3LargeForestClusterCount = 156;
        V3HugeForestClusterCount = 280;
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
        V3HugeMajorForestMinCount = 44;
        V3HugeMajorForestMaxCount = 56;
        V3HugeMinorForestMinCount = 200;
        V3HugeMinorForestMaxCount = 240;
        V3MajorForestMinRadius = 180.0f;
        V3MajorForestMaxRadius = 520.0f;
        V3MinorForestMinRadius = 48.0f;
        V3MinorForestMaxRadius = 145.0f;
        V3ForestPotentialThreshold = 0.50f;
        V3MajorForestNoiseScale = 0.010f;
        V3MinorForestNoiseScale = 0.020f;
        V3MajorForestWarpStrength = 64.0f;
        V3MinorForestWarpStrength = 26.0f;
        V3SmallMajorQuarryMinCount = 1;
        V3SmallMajorQuarryMaxCount = 2;
        V3SmallMinorQuarryMinCount = 4;
        V3SmallMinorQuarryMaxCount = 6;
        V3MediumMajorQuarryMinCount = 3;
        V3MediumMajorQuarryMaxCount = 5;
        V3MediumMinorQuarryMinCount = 10;
        V3MediumMinorQuarryMaxCount = 16;
        V3LargeMajorQuarryMinCount = 8;
        V3LargeMajorQuarryMaxCount = 12;
        V3LargeMinorQuarryMinCount = 28;
        V3LargeMinorQuarryMaxCount = 45;
        V3HugeMajorQuarryMinCount = 18;
        V3HugeMajorQuarryMaxCount = 24;
        V3HugeMinorQuarryMinCount = 70;
        V3HugeMinorQuarryMaxCount = 90;
        V3MajorQuarryMinRadius = 70.0f;
        V3MajorQuarryMaxRadius = 145.0f;
        V3MinorQuarryMinRadius = 30.0f;
        V3MinorQuarryMaxRadius = 72.0f;
        V3MajorQuarryMinPatchCount = 5;
        V3MajorQuarryMaxPatchCount = 10;
        V3MinorQuarryMinPatchCount = 2;
        V3MinorQuarryMaxPatchCount = 5;
        V3QuarryPlacementMaxAttemptsPerCluster = 80;
        V3QuarryPotentialThreshold = 0.49f;
        V3MajorQuarryNoiseScale = 0.021f;
        V3MinorQuarryNoiseScale = 0.035f;
        V3MajorQuarryWarpStrength = 24.0f;
        V3MinorQuarryWarpStrength = 10.0f;
        V3QuarryOreSpotChance = 0.018f;
        V3HugeRuinMinCount = 90;
        V3HugeRuinMaxCount = 110;
        V3RuinMinRadius = 13.0f;
        V3RuinMaxRadius = 34.0f;
        V3RuinPlacementMaxAttemptsPerSite = 90;
        V3RuinNoiseScale = 0.075f;
        V3RuinWarpStrength = 7.5f;
        V3SmallDungeonEntranceMinCount = 2;
        V3SmallDungeonEntranceMaxCount = 3;
        V3MediumDungeonEntranceMinCount = 5;
        V3MediumDungeonEntranceMaxCount = 8;
        V3LargeDungeonEntranceMinCount = 10;
        V3LargeDungeonEntranceMaxCount = 16;
        V3HugeDungeonEntranceMinCount = 28;
        V3HugeDungeonEntranceMaxCount = 32;
        V3DungeonEntranceMinRadius = 9.0f;
        V3DungeonEntranceMaxRadius = 18.0f;
        V3DungeonEntranceMinDistance = 280.0f;
        V3DungeonEntrancePlacementMaxAttemptsPerSite = 90;
        V3PlainsDungeonEntranceWeightMultiplier = 0.45f;
        V3ForestLandDungeonEntranceWeightMultiplier = 0.95f;
        V3RockyHillsDungeonEntranceWeightMultiplier = 1.55f;
        V3DrylandDungeonEntranceWeightMultiplier = 0.90f;
        V3WastelandDungeonEntranceWeightMultiplier = 1.45f;
        WorldMapHugeTextureSize = 1536;
    }
}
