using Godot;

namespace WorldV2;

public static class BiomeResolverV2
{
    public static readonly Vector2I CentralTownCenter = StartingAreaProfileV2.Default.Center;

    public static void Resolve(WorldClimateSampleV2 sample, WorldGenerationSettingsV2? settings)
    {
        settings ??= WorldGenerationSettingsV2.Default;
        StartingAreaProfileV2 startingArea = settings.CreateStartingAreaProfile();
        float centralDistance = startingArea.GetDistance(sample.GlobalCell);
        sample.DistanceFromStart = centralDistance;
        sample.TownInfluence = startingArea.GetTownInfluence(centralDistance);
        sample.StartSafeInfluence = startingArea.GetSafeInfluence(centralDistance);
        sample.IsInsideTownCore = centralDistance <= startingArea.CoreRadius;
        sample.IsInsideTownInner = centralDistance <= startingArea.InnerRadius;
        sample.IsInsideTownBlend = centralDistance <= startingArea.BlendRadius;
        sample.IsStartSafeZone = centralDistance <= startingArea.SafeRadius;

        if (sample.StartSafeInfluence > 0.0f)
        {
            sample.Danger = Mathf.Lerp(sample.Danger, sample.Danger * startingArea.DangerSuppression, sample.StartSafeInfluence);
        }

        sample.IsOcean = sample.Elevation < settings.OceanThreshold;
        float riverWidth = settings.RiverWidth * Mathf.Lerp(0.55f, 1.25f, sample.Humidity);
        if (sample.TownInfluence > 0.0f)
        {
            riverWidth *= Mathf.Lerp(1.0f, startingArea.RiverSuppression, sample.TownInfluence);
        }

        bool lowlandRiverBand = sample.Elevation > settings.OceanThreshold + settings.RiverOceanMargin
            && sample.Elevation < settings.RiverMountainCutoff;
        bool humidEnough = sample.Humidity > settings.RiverHumidityRequirement || sample.Elevation < 0.46f;
        sample.IsRiver = !sample.IsOcean
            && lowlandRiverBand
            && humidEnough
            && Mathf.Abs(sample.RiverPotential) < riverWidth;
        if (sample.TownInfluence > 0.75f)
        {
            sample.IsOcean = false;
            sample.IsRiver = false;
        }

        if (sample.IsInsideTownInner)
        {
            sample.IsOcean = false;
            sample.IsRiver = false;
        }

        bool isCoast = !sample.IsOcean && !sample.IsRiver && sample.Elevation < settings.CoastThreshold;
        bool startRoad = IsStartingAreaRoad(sample, centralDistance, startingArea);
        bool noiseRoad = sample.TownInfluence < 0.45f && IsNoiseRoad(sample, settings);
        sample.IsRoad = !sample.IsOcean && !sample.IsRiver && (startRoad || noiseRoad);
        sample.IsWater = sample.IsOcean || sample.IsRiver;
        sample.IsBuildRestricted = sample.IsOcean || sample.IsRiver || sample.IsInsideTownInner;
        sample.IsWalkable = !sample.IsWater;

        if (sample.IsInsideTownCore)
        {
            sample.Biome = BiomeTypeV2.CentralTown;
            return;
        }

        if (sample.IsInsideTownInner && StableUnitFloat(sample.GlobalCell.X, sample.GlobalCell.Y, 2101) < sample.TownInfluence * 0.54f)
        {
            sample.Biome = BiomeTypeV2.CentralTown;
            return;
        }

        if (sample.IsOcean)
        {
            sample.Biome = BiomeTypeV2.Ocean;
            return;
        }

        if (sample.IsRiver)
        {
            sample.Biome = BiomeTypeV2.River;
            return;
        }

        if (isCoast)
        {
            sample.Biome = BiomeTypeV2.Coast;
            return;
        }

        if (!sample.IsStartSafeZone && sample.Danger > settings.MonsterDangerThreshold && sample.RuinDensity > 0.56f)
        {
            sample.Biome = BiomeTypeV2.MonsterNestArea;
            return;
        }

        if (!sample.IsStartSafeZone && sample.Danger > settings.BanditDangerThreshold && sample.RuinDensity > 0.56f)
        {
            sample.Biome = BiomeTypeV2.BanditTerritory;
            return;
        }

        if (!sample.IsStartSafeZone && sample.Danger > settings.ToxicDangerThreshold && sample.Humidity < 0.34f)
        {
            sample.Biome = BiomeTypeV2.ToxicWasteland;
            return;
        }

        if (sample.RuinDensity > settings.RuinResidentialThreshold && sample.Civilization > 0.55f)
        {
            sample.Biome = sample.ResourceRichness > settings.RuinFactoryThreshold ? BiomeTypeV2.RuinedFactory : BiomeTypeV2.RuinedResidential;
            return;
        }

        if (sample.Elevation > settings.MountainThreshold)
        {
            sample.Biome = sample.Temperature < 0.34f ? BiomeTypeV2.Snowfield : BiomeTypeV2.Mountain;
            return;
        }

        if (sample.Elevation > settings.HillThreshold)
        {
            sample.Biome = sample.ResourceRichness > 0.62f ? BiomeTypeV2.QuarryField : BiomeTypeV2.Hills;
            return;
        }

        if (sample.Temperature < 0.24f)
        {
            sample.Biome = sample.Humidity > 0.55f ? BiomeTypeV2.Snowfield : BiomeTypeV2.ColdWasteland;
            return;
        }

        if (sample.Humidity > 0.74f && sample.Elevation < 0.50f)
        {
            sample.Biome = BiomeTypeV2.Swamp;
            return;
        }

        if (sample.Humidity > 0.68f)
        {
            sample.Biome = sample.Temperature > 0.36f ? BiomeTypeV2.DenseForest : BiomeTypeV2.Forest;
            return;
        }

        if (sample.Humidity > 0.52f)
        {
            sample.Biome = BiomeTypeV2.Forest;
            return;
        }

        if (sample.Humidity < 0.24f && sample.Temperature > 0.62f)
        {
            sample.Biome = BiomeTypeV2.Desert;
            return;
        }

        if (sample.Humidity < 0.30f)
        {
            sample.Biome = BiomeTypeV2.DryWasteland;
            return;
        }

        if (sample.Civilization > settings.TradeRoadZoneThreshold && Mathf.Abs(sample.RoadPotential) < settings.RoadBandWidth * 3.0f)
        {
            sample.Biome = BiomeTypeV2.TradeRoadZone;
            return;
        }

        sample.Biome = BiomeTypeV2.Plains;
    }

    private static bool IsStartingAreaRoad(WorldClimateSampleV2 sample, float distanceFromTown, StartingAreaProfileV2 startingArea)
    {
        if (distanceFromTown < 14.0f || distanceFromTown > startingArea.RoadExitRadius)
        {
            return false;
        }

        Vector2 offset = new(sample.GlobalCell.X - startingArea.Center.X, sample.GlobalCell.Y - startingArea.Center.Y);
        int exitCount = Mathf.Max(3, startingArea.RoadExitCount);
        float baseAngle = StableUnitFloat(sample.WorldSeed, 0, 1801) * Mathf.Tau;

        for (int i = 0; i < exitCount; i++)
        {
            float jitter = (StableUnitFloat(sample.WorldSeed, i, 1811) - 0.5f) * 0.48f;
            float angle = baseAngle + Mathf.Tau * i / exitCount + jitter;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            float forward = offset.Dot(direction);
            if (forward < 8.0f || forward > startingArea.RoadExitRadius)
            {
                continue;
            }

            float side = offset.X * direction.Y - offset.Y * direction.X;
            float phaseA = StableUnitFloat(sample.WorldSeed, i, 1821) * Mathf.Tau;
            float phaseB = StableUnitFloat(sample.WorldSeed, i, 1831) * Mathf.Tau;
            float wobble = Mathf.Sin(forward * 0.030f + phaseA) * startingArea.RoadExitWarpStrength
                + Mathf.Sin(forward * 0.073f + phaseB) * startingArea.RoadExitWarpStrength * 0.35f;
            float distanceToPath = Mathf.Abs(side - wobble);
            float width = Mathf.Lerp(5.0f, 2.0f, Mathf.Clamp(forward / startingArea.RoadExitRadius, 0.0f, 1.0f));
            float falloff = distanceFromTown <= startingArea.InnerRadius + 6.0f
                ? 1.0f
                : Mathf.Clamp(1.0f - (distanceFromTown - startingArea.InnerRadius - 6.0f) / (startingArea.RoadExitRadius - startingArea.InnerRadius - 6.0f), 0.0f, 1.0f);
            float brokenEdge = StableUnitFloat(sample.GlobalCell.X, sample.GlobalCell.Y, 1841);

            if (distanceToPath <= width && brokenEdge < Mathf.Lerp(0.72f, 1.0f, falloff))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNoiseRoad(WorldClimateSampleV2 sample, WorldGenerationSettingsV2 settings)
    {
        float roadBand = Mathf.Abs(sample.RoadPotential);
        float allowedBand = settings.RoadBandWidth * Mathf.Lerp(0.72f, 1.35f, sample.Civilization);
        bool roadFriendlyTerrain = sample.Elevation > settings.OceanThreshold + 0.035f && sample.Elevation < 0.76f;
        bool ruinsHelpRoads = sample.RuinDensity > 0.58f && sample.Civilization > 0.54f;
        return roadFriendlyTerrain
            && sample.Civilization > settings.RoadCivilizationThreshold
            && (roadBand < allowedBand || (ruinsHelpRoads && roadBand < allowedBand * 1.35f));
    }

    private static float StableUnitFloat(int x, int y, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            Mix(ref hash, x);
            Mix(ref hash, y);
            Mix(ref hash, salt);
            return (hash & 0x00ffffffu) / 16777215.0f;
        }
    }

    private static void Mix(ref uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }
    }
}
