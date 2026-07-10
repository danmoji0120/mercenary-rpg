using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace WorldV2;

public static class WorldMapTextureBuilderV2
{
    private static readonly Color PlainColor = new(0.24f, 0.43f, 0.25f, 1.0f);
    private static readonly Color ForestLandBiomeColor = new(0.22f, 0.39f, 0.23f, 1.0f);
    private static readonly Color RockyHillsBiomeColor = new(0.39f, 0.40f, 0.31f, 1.0f);
    private static readonly Color DrylandBiomeColor = new(0.45f, 0.41f, 0.27f, 1.0f);
    private static readonly Color WastelandBiomeColor = new(0.34f, 0.30f, 0.28f, 1.0f);
    private static readonly Color ForestColor = new(0.09f, 0.25f, 0.13f, 1.0f);
    private static readonly Color RoadColor = new(0.46f, 0.34f, 0.20f, 1.0f);
    private static readonly Color BranchRoadColor = new(0.38f, 0.31f, 0.20f, 1.0f);
    private static readonly Color VillageColor = new(0.72f, 0.63f, 0.43f, 1.0f);
    private static readonly Color StartingVillageColor = new(0.95f, 0.82f, 0.42f, 1.0f);
    private static readonly Color QuarryColor = new(0.42f, 0.43f, 0.39f, 1.0f);
    private static readonly Color RuinColor = new(0.45f, 0.38f, 0.31f, 1.0f);
    private static readonly Color DungeonEntranceColor = new(0.20f, 0.16f, 0.24f, 1.0f);
    private static readonly Color BanditCampColor = new(0.42f, 0.18f, 0.14f, 1.0f);
    private static readonly Color FactionOutpostColor = new(0.34f, 0.43f, 0.54f, 1.0f);

    public static Texture2D Build(WorldManagerV2 manager, WorldGenerationSettingsV2 settings, out Vector2I textureSize, out double buildMs)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int size = GetTextureSize(manager.MapSizePreset, settings);
        textureSize = new Vector2I(size, size);

        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(PlainColor);

        if (manager.PlanVersion == WorldPlanVersionV2.V3)
        {
            DrawV3Features(image, manager, settings);
        }

        stopwatch.Stop();
        buildMs = stopwatch.Elapsed.TotalMilliseconds;
        return ImageTexture.CreateFromImage(image);
    }

    private static int GetTextureSize(WorldMapSizePresetV2 preset, WorldGenerationSettingsV2 settings)
    {
        int configured = preset switch
        {
            WorldMapSizePresetV2.Medium => settings.WorldMapMediumTextureSize,
            WorldMapSizePresetV2.Large => settings.WorldMapLargeTextureSize,
            WorldMapSizePresetV2.Huge => settings.WorldMapHugeTextureSize,
            _ => settings.WorldMapSmallTextureSize
        };

        return Mathf.Clamp(configured, 256, 1536);
    }

    private static void DrawV3Features(Image image, WorldManagerV2 manager, WorldGenerationSettingsV2 settings)
    {
        if (WorldGenerationLayerSettingsV2.EnableBiomes)
        {
            DrawBiomeBackground(image, manager.WorldMapSize, manager.GetV3MapBiomeRegions());
        }

        if (WorldGenerationLayerSettingsV2.EnableForests)
        {
            foreach (ForestRegionV3 forest in manager.GetV3MapForestRegions())
            {
                DrawForestRegion(image, manager.WorldMapSize, forest);
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableQuarries)
        {
            foreach (QuarryRegionV3 quarry in manager.GetV3MapQuarryRegions())
            {
                DrawQuarryRegion(image, manager.WorldMapSize, quarry);
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableRuins)
        {
            foreach (RuinSiteV3 ruin in manager.GetV3MapRuinSites())
            {
                DrawRuinSite(image, manager.WorldMapSize, ruin, settings);
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableDungeons)
        {
            foreach (DungeonEntranceSiteV3 dungeon in manager.GetV3MapDungeonEntrances())
            {
                DrawDungeonEntrance(image, manager.WorldMapSize, dungeon);
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableBanditCamps)
        {
            foreach (BanditCampSiteV3 camp in manager.GetV3MapBanditCamps())
            {
                DrawBanditCamp(image, manager.WorldMapSize, camp);
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableFactionOutposts)
        {
            foreach (FactionOutpostSiteV3 outpost in manager.GetV3MapFactionOutposts())
            {
                DrawFactionOutpost(image, manager.WorldMapSize, outpost);
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableRoads)
        {
            foreach (RoadPathV2 road in manager.GetV3MapRoads())
            {
                DrawRoad(image, manager.WorldMapSize, road);
            }
        }

        if (WorldGenerationLayerSettingsV2.EnableVillages)
        {
            foreach (VillageSiteV2 village in manager.GetV3MapVillages())
            {
                DrawVillage(image, manager.WorldMapSize, village);
            }
        }
    }

    private static void DrawBiomeBackground(Image image, WorldMapSizeDefinitionV2 worldSize, IReadOnlyList<BiomeRegionV3> biomes)
    {
        int step = image.GetWidth() >= 1024 ? 2 : 1;
        for (int y = 0; y < image.GetHeight(); y += step)
        {
            for (int x = 0; x < image.GetWidth(); x += step)
            {
                int sampleX = Mathf.Min(x + step / 2, image.GetWidth() - 1);
                int sampleY = Mathf.Min(y + step / 2, image.GetHeight() - 1);
                Vector2 world = PixelToWorld(image, worldSize, sampleX, sampleY);
                ResolveMapBiome(world, biomes, out BiomeKindV3 bestKind, out BiomeKindV3 secondKind, out float margin);
                Color color = GetBiomeColor(bestKind);
                if (secondKind != bestKind && margin < 0.12f)
                {
                    float transition = Mathf.Clamp((0.12f - margin) / 0.12f, 0.0f, 1.0f) * 0.22f;
                    color = color.Lerp(GetBiomeColor(secondKind), transition);
                }

                FillPixelBlock(image, x, y, step, color);
            }
        }
    }

    private static Color GetBiomeColor(BiomeKindV3 biomeKind)
    {
        return biomeKind switch
        {
            BiomeKindV3.ForestLand => ForestLandBiomeColor,
            BiomeKindV3.RockyHills => RockyHillsBiomeColor,
            BiomeKindV3.Dryland => DrylandBiomeColor,
            BiomeKindV3.Wasteland => WastelandBiomeColor,
            _ => PlainColor
        };
    }

    private static void DrawForestRegion(Image image, WorldMapSizeDefinitionV2 worldSize, ForestRegionV3 forest)
    {
        GetPixelBounds(image, worldSize, forest.Bounds, out int minX, out int minY, out int maxX, out int maxY);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 world = PixelToWorld(image, worldSize, x, y);
                float strength = GetRegionPotential(world, forest.Center, forest.ApproxRadius, forest.NoiseScale, forest.WarpStrength, forest.Threshold, forest.Density, forest.Seed);
                if (strength > 0.0f)
                {
                    SetPixelSafe(image, x, y, ForestColor.Lerp(PlainColor, Mathf.Clamp(0.28f - strength * 0.12f, 0.0f, 0.22f)));
                }
            }
        }
    }

    private static void DrawQuarryRegion(Image image, WorldMapSizeDefinitionV2 worldSize, QuarryRegionV3 quarry)
    {
        GetPixelBounds(image, worldSize, quarry.Bounds, out int minX, out int minY, out int maxX, out int maxY);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 world = PixelToWorld(image, worldSize, x, y);
                float strength = GetRegionPotential(world, quarry.Center, quarry.ApproxRadius, quarry.NoiseScale, quarry.WarpStrength, quarry.Threshold, quarry.Density, quarry.Seed);
                if (strength > 0.0f)
                {
                    SetPixelSafe(image, x, y, QuarryColor.Lerp(PlainColor, Mathf.Clamp(0.26f - strength * 0.10f, 0.0f, 0.20f)));
                }
            }
        }
    }

    private static void DrawRuinSite(Image image, WorldMapSizeDefinitionV2 worldSize, RuinSiteV3 ruin, WorldGenerationSettingsV2 settings)
    {
        GetPixelBounds(image, worldSize, ruin.Bounds, out int minX, out int minY, out int maxX, out int maxY);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 world = PixelToWorld(image, worldSize, x, y);
                float strength = GetRegionPotential(world, ruin.Center, ruin.ApproxRadius, settings.V3RuinNoiseScale, settings.V3RuinWarpStrength, 0.24f, ruin.Density, ruin.Seed);
                if (strength > 0.0f)
                {
                    SetPixelSafe(image, x, y, RuinColor.Lerp(PlainColor, Mathf.Clamp(0.34f - strength * 0.16f, 0.0f, 0.26f)));
                }
            }
        }
    }

    private static void DrawDungeonEntrance(Image image, WorldMapSizeDefinitionV2 worldSize, DungeonEntranceSiteV3 dungeon)
    {
        Vector2I center = WorldToPixel(image, worldSize, dungeon.Center);
        float scale = image.GetWidth() / (float)worldSize.WidthCells;
        int radius = Mathf.Clamp(Mathf.RoundToInt(dungeon.ApproxRadius * scale), dungeon.IsRoadLinked ? 3 : 2, dungeon.IsRoadLinked ? 6 : 5);
        DrawFilledCircle(image, center, radius, DungeonEntranceColor);
        DrawCircleOutline(image, center, radius + 1, dungeon.IsRoadLinked
            ? new Color(0.68f, 0.58f, 0.38f, 0.85f)
            : new Color(0.08f, 0.07f, 0.10f, 0.70f));
    }

    private static void DrawBanditCamp(Image image, WorldMapSizeDefinitionV2 worldSize, BanditCampSiteV3 camp)
    {
        Vector2I center = WorldToPixel(image, worldSize, camp.Center);
        float scale = image.GetWidth() / (float)worldSize.WidthCells;
        int radius = Mathf.Clamp(Mathf.RoundToInt(camp.ApproxRadius * scale), camp.IsRoadLinked ? 3 : 2, camp.IsRoadLinked ? 6 : 5);
        DrawFilledCircle(image, center, radius, BanditCampColor);
        DrawCircleOutline(image, center, radius + 1, new Color(0.12f, 0.05f, 0.04f, camp.IsRoadLinked ? 0.90f : 0.72f));
    }

    private static void DrawFactionOutpost(Image image, WorldMapSizeDefinitionV2 worldSize, FactionOutpostSiteV3 outpost)
    {
        Vector2I center = WorldToPixel(image, worldSize, outpost.Center);
        float scale = image.GetWidth() / (float)worldSize.WidthCells;
        int radius = Mathf.Clamp(Mathf.RoundToInt(outpost.ApproxRadius * scale), outpost.IsRoadLinked ? 3 : 2, outpost.IsRoadLinked ? 6 : 5);
        DrawFilledSquare(image, center, radius, FactionOutpostColor);
        DrawSquareOutline(image, center, radius + 1, new Color(0.12f, 0.16f, 0.20f, outpost.IsRoadLinked ? 0.90f : 0.72f));
    }

    private static void DrawRoad(Image image, WorldMapSizeDefinitionV2 worldSize, RoadPathV2 road)
    {
        Color color = road.Kind == RoadKindV3.Branch ? BranchRoadColor : RoadColor;
        int thickness = road.Kind == RoadKindV3.Primary ? 2 : 1;
        for (int i = 1; i < road.PathPointsWorld.Count; i++)
        {
            Vector2I from = WorldToPixel(image, worldSize, road.PathPointsWorld[i - 1]);
            Vector2I to = WorldToPixel(image, worldSize, road.PathPointsWorld[i]);
            DrawLine(image, from, to, color, thickness);
        }
    }

    private static void DrawVillage(Image image, WorldMapSizeDefinitionV2 worldSize, VillageSiteV2 village)
    {
        Vector2I center = WorldToPixel(image, worldSize, village.Center);
        float scale = image.GetWidth() / (float)worldSize.WidthCells;
        int minRadius = village.Scale switch
        {
            VillageScaleV2.CityCandidate => 7,
            VillageScaleV2.Town => 6,
            VillageScaleV2.LargeVillage => 5,
            VillageScaleV2.Hamlet => 3,
            _ => 4
        };
        int maxRadius = village.Scale switch
        {
            VillageScaleV2.CityCandidate => 13,
            VillageScaleV2.Town => 11,
            VillageScaleV2.LargeVillage => 9,
            VillageScaleV2.Hamlet => 5,
            _ => 7
        };
        int radius = Mathf.Clamp(Mathf.RoundToInt(village.Radius * scale), village.IsStartingVillage ? 5 : minRadius, village.IsStartingVillage ? 12 : maxRadius);
        DrawFilledCircle(image, center, radius, village.IsStartingVillage ? StartingVillageColor : VillageColor);
        DrawCircleOutline(image, center, radius + 1, new Color(0.10f, 0.09f, 0.07f, 0.85f));
        if (village.Scale is VillageScaleV2.Town or VillageScaleV2.CityCandidate)
        {
            DrawCircleOutline(image, center, radius + 3, new Color(0.18f, 0.14f, 0.08f, village.Scale == VillageScaleV2.CityCandidate ? 0.85f : 0.62f));
        }
    }

    private static float GetRegionPotential(Vector2 point, Vector2 center, float radius, float noiseScale, float warpStrength, float threshold, float density, int seed)
    {
        float safeNoiseScale = Mathf.Max(0.001f, noiseScale);
        float warpScale = safeNoiseScale * 0.42f;
        float warpX = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, seed + 101, 2) - 0.5f) * 2.0f;
        float warpY = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, seed + 211, 2) - 0.5f) * 2.0f;
        Vector2 warped = point + new Vector2(warpX, warpY) * Mathf.Max(0.0f, warpStrength);
        float normalizedDistance = warped.DistanceTo(center) / Mathf.Max(4.0f, radius);
        if (normalizedDistance > 1.28f)
        {
            return 0.0f;
        }

        float low = FractalValueNoise(warped.X * safeNoiseScale, warped.Y * safeNoiseScale, seed + 307, 2);
        float edge = FractalValueNoise(warped.X * safeNoiseScale * 2.0f, warped.Y * safeNoiseScale * 2.0f, seed + 409, 2);
        float potential = 1.0f - normalizedDistance + (low - 0.5f) * 0.30f + (edge - 0.5f) * 0.10f;
        if (normalizedDistance < 0.42f)
        {
            potential = Mathf.Max(potential, 0.62f + low * 0.14f);
        }

        return potential > threshold
            ? Mathf.Clamp((potential - threshold) / Mathf.Max(0.05f, 1.05f - threshold) * density, 0.0f, 1.0f)
            : 0.0f;
    }

    private static void ResolveMapBiome(Vector2 point, IReadOnlyList<BiomeRegionV3> biomes, out BiomeKindV3 bestKind, out BiomeKindV3 secondKind, out float margin)
    {
        bestKind = BiomeKindV3.Plains;
        secondKind = BiomeKindV3.Plains;
        float bestScore = 0.0f;
        float secondScore = -0.20f;

        foreach (BiomeRegionV3 biome in biomes)
        {
            if (!biome.IsMajorRegion && !biome.Bounds.HasPoint(point))
            {
                continue;
            }

            float score = GetBiomeRegionScore(point, biome);
            if (score > bestScore)
            {
                secondScore = bestScore;
                secondKind = bestKind;
                bestScore = score;
                bestKind = biome.Kind;
            }
            else if (score > secondScore)
            {
                secondScore = score;
                secondKind = biome.Kind;
            }
        }

        margin = Mathf.Clamp(bestScore - secondScore, 0.0f, 1.0f);
    }

    private static float GetBiomeRegionScore(Vector2 point, BiomeRegionV3 biome)
    {
        float safeNoiseScale = Mathf.Max(0.0002f, biome.NoiseScale);
        float warpScale = safeNoiseScale * 0.22f;
        float warpX = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, biome.Seed + 101, 1) - 0.5f) * 2.0f;
        float warpY = (FractalValueNoise(point.X * warpScale, point.Y * warpScale, biome.Seed + 211, 1) - 0.5f) * 2.0f;
        Vector2 warped = point + new Vector2(warpX, warpY) * Mathf.Max(0.0f, biome.WarpStrength);
        float normalizedDistance = warped.DistanceTo(biome.Center) / Mathf.Max(4.0f, biome.ApproxRadius);
        float low = FractalValueNoise(warped.X * safeNoiseScale, warped.Y * safeNoiseScale, biome.Seed + 307, 1);
        float edge = FractalValueNoise(warped.X * safeNoiseScale * 1.25f, warped.Y * safeNoiseScale * 1.25f, biome.Seed + 409, 1);
        float boundaryShift = (low - 0.5f) * 0.10f + (edge - 0.5f) * 0.04f;

        if (biome.IsMajorRegion)
        {
            float plainsBias = biome.Kind == BiomeKindV3.Plains ? 0.08f : 0.0f;
            return biome.Weight + plainsBias - normalizedDistance + boundaryShift;
        }

        if (normalizedDistance > 1.16f)
        {
            return -1000.0f;
        }

        float potential = 1.0f - normalizedDistance + boundaryShift;
        if (normalizedDistance < 0.55f)
        {
            potential = Mathf.Max(potential, 0.72f + (low - 0.5f) * 0.04f);
        }

        return potential > biome.Threshold
            ? Mathf.Lerp(0.18f, 0.54f, Mathf.Clamp((potential - biome.Threshold) / Mathf.Max(0.05f, 1.04f - biome.Threshold), 0.0f, 1.0f)) * biome.Weight
            : -1000.0f;
    }

    private static void DrawLine(Image image, Vector2I from, Vector2I to, Color color, int thickness)
    {
        Vector2 delta = to - from;
        int steps = Mathf.Max(Mathf.Abs(to.X - from.X), Mathf.Abs(to.Y - from.Y));
        if (steps <= 0)
        {
            SetPixelSafe(image, from.X, from.Y, color);
            return;
        }

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 p = new Vector2(from.X, from.Y) + delta * t;
            Vector2I pixel = new(Mathf.RoundToInt(p.X), Mathf.RoundToInt(p.Y));
            DrawFilledCircle(image, pixel, thickness, color);
        }
    }

    private static void DrawFilledCircle(Image image, Vector2I center, int radius, Color color)
    {
        int radiusSquared = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radiusSquared)
                {
                    SetPixelSafe(image, center.X + x, center.Y + y, color);
                }
            }
        }
    }

    private static void DrawCircleOutline(Image image, Vector2I center, int radius, Color color)
    {
        int inner = Mathf.Max(0, radius - 1);
        int outerSquared = radius * radius;
        int innerSquared = inner * inner;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int distanceSquared = x * x + y * y;
                if (distanceSquared <= outerSquared && distanceSquared >= innerSquared)
                {
                    SetPixelSafe(image, center.X + x, center.Y + y, color);
                }
            }
        }
    }

    private static void DrawFilledSquare(Image image, Vector2I center, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                SetPixelSafe(image, center.X + x, center.Y + y, color);
            }
        }
    }

    private static void DrawSquareOutline(Image image, Vector2I center, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Abs(x) == radius || Mathf.Abs(y) == radius)
                {
                    SetPixelSafe(image, center.X + x, center.Y + y, color);
                }
            }
        }
    }

    private static void GetPixelBounds(Image image, WorldMapSizeDefinitionV2 worldSize, Rect2 worldBounds, out int minX, out int minY, out int maxX, out int maxY)
    {
        Vector2I a = WorldToPixel(image, worldSize, worldBounds.Position);
        Vector2I b = WorldToPixel(image, worldSize, worldBounds.End);
        minX = Mathf.Clamp(Mathf.Min(a.X, b.X) - 2, 0, image.GetWidth() - 1);
        minY = Mathf.Clamp(Mathf.Min(a.Y, b.Y) - 2, 0, image.GetHeight() - 1);
        maxX = Mathf.Clamp(Mathf.Max(a.X, b.X) + 2, 0, image.GetWidth() - 1);
        maxY = Mathf.Clamp(Mathf.Max(a.Y, b.Y) + 2, 0, image.GetHeight() - 1);
    }

    private static Vector2 PixelToWorld(Image image, WorldMapSizeDefinitionV2 worldSize, int x, int y)
    {
        return new Vector2(
            (x + 0.5f) / image.GetWidth() * worldSize.WidthCells,
            (y + 0.5f) / image.GetHeight() * worldSize.HeightCells);
    }

    private static Vector2I WorldToPixel(Image image, WorldMapSizeDefinitionV2 worldSize, Vector2 world)
    {
        return new Vector2I(
            Mathf.Clamp(Mathf.RoundToInt(world.X / Mathf.Max(1, worldSize.WidthCells) * (image.GetWidth() - 1)), 0, image.GetWidth() - 1),
            Mathf.Clamp(Mathf.RoundToInt(world.Y / Mathf.Max(1, worldSize.HeightCells) * (image.GetHeight() - 1)), 0, image.GetHeight() - 1));
    }

    private static void SetPixelSafe(Image image, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight())
        {
            return;
        }

        image.SetPixel(x, y, color);
    }

    private static void FillPixelBlock(Image image, int x, int y, int size, Color color)
    {
        int maxY = Mathf.Min(y + size, image.GetHeight());
        int maxX = Mathf.Min(x + size, image.GetWidth());
        for (int py = y; py < maxY; py++)
        {
            for (int px = x; px < maxX; px++)
            {
                image.SetPixel(px, py, color);
            }
        }
    }

    private static float FractalValueNoise(float x, float y, int salt, int octaves)
    {
        float value = 0.0f;
        float amplitude = 1.0f;
        float frequency = 1.0f;
        float totalAmplitude = 0.0f;
        for (int i = 0; i < octaves; i++)
        {
            value += ValueNoise(x * frequency, y * frequency, salt + i * 97) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.0f;
        }

        return totalAmplitude <= 0.0f ? 0.0f : value / totalAmplitude;
    }

    private static float ValueNoise(float x, float y, int salt)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        float tx = SmoothStep(x - x0);
        float ty = SmoothStep(y - y0);
        float a = StableUnitFloat(x0, y0, salt);
        float b = StableUnitFloat(x1, y0, salt);
        float c = StableUnitFloat(x0, y1, salt);
        float d = StableUnitFloat(x1, y1, salt);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3.0f - 2.0f * t);
    }

    private static float StableUnitFloat(int x, int y, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)x) * 16777619u;
            hash = (hash ^ (uint)y) * 16777619u;
            hash = (hash ^ (uint)salt) * 16777619u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            hash ^= hash >> 16;
            return (hash & 0x00ffffffu) / (float)0x01000000u;
        }
    }
}
