using Godot;

namespace WorldV2;

public sealed class WorldNoiseProfileV2
{
    private int _worldSeed;
    private WorldGenerationSettingsV2 _settings = WorldGenerationSettingsV2.Default;

    public void Initialize(int worldSeed, WorldGenerationSettingsV2? settings)
    {
        _worldSeed = worldSeed;
        _settings = settings ?? WorldGenerationSettingsV2.Default;
    }

    public WorldClimateSampleV2 SampleAll(Vector2I globalCell)
    {
        WorldClimateSampleV2 sample = new()
        {
            WorldSeed = _worldSeed,
            GlobalCell = globalCell,
            Elevation = SampleElevation(globalCell),
            Temperature = SampleTemperature(globalCell),
            Humidity = SampleHumidity(globalCell),
            Danger = SampleDanger(globalCell),
            RuinDensity = SampleRuinDensity(globalCell),
            Civilization = SampleCivilization(globalCell),
            RoadPotential = SampleRoadPotential(globalCell),
            RiverPotential = SampleRiverPotential(globalCell),
            ResourceRichness = SampleResourceRichness(globalCell)
        };

        BiomeResolverV2.Resolve(sample, _settings);
        return sample;
    }

    public float SampleElevation(Vector2I coord)
    {
        float macro = FractalValueNoise(coord, _worldSeed + 1001, _settings.ElevationMacroFrequency, 5, 0.53f);
        float mid = FractalValueNoise(coord, _worldSeed + 1011, _settings.ElevationMidFrequency, 3, 0.48f);
        float detail = FractalValueNoise(coord, _worldSeed + 1021, _settings.ElevationDetailFrequency, 2, 0.42f);
        return Mathf.Clamp(macro * 0.64f + mid * 0.26f + detail * 0.10f, 0.0f, 1.0f);
    }

    public float SampleTemperature(Vector2I coord)
    {
        float latitude = Mathf.Clamp(Mathf.Abs(coord.Y) / 4600.0f, 0.0f, 1.0f);
        float macro = FractalValueNoise(coord, _worldSeed + 2001, _settings.TemperatureFrequency, 4, 0.50f);
        float mid = FractalValueNoise(coord, _worldSeed + 2011, _settings.TemperatureFrequency * 2.3f, 3, 0.48f);
        float detail = FractalValueNoise(coord, _worldSeed + 2021, _settings.TemperatureFrequency * 5.3f, 2, 0.42f);
        return Mathf.Clamp(macro * 0.54f + mid * 0.24f + detail * 0.08f + (1.0f - latitude) * 0.14f, 0.0f, 1.0f);
    }

    public float SampleHumidity(Vector2I coord)
    {
        float macro = FractalValueNoise(coord, _worldSeed + 3001, _settings.HumidityFrequency, 4, 0.52f);
        float mid = FractalValueNoise(coord, _worldSeed + 3011, _settings.HumidityFrequency * 2.8f, 3, 0.50f);
        float detail = FractalValueNoise(coord, _worldSeed + 3021, _settings.HumidityFrequency * 7.1f, 2, 0.44f);
        return Mathf.Clamp(macro * 0.52f + mid * 0.36f + detail * 0.12f, 0.0f, 1.0f);
    }

    public float SampleDanger(Vector2I coord)
    {
        Vector2I startCenter = _settings.StartCenter;
        float distance = new Vector2(coord.X - startCenter.X, coord.Y - startCenter.Y).Length();
        float distanceFactor = Mathf.Clamp(distance / 3600.0f, 0.0f, 1.0f);
        float macro = FractalValueNoise(coord, _worldSeed + 4001, _settings.DangerFrequency, 4, 0.55f);
        float mid = FractalValueNoise(coord, _worldSeed + 4011, _settings.DangerFrequency * 3.1f, 3, 0.47f);
        return Mathf.Clamp(macro * 0.48f + mid * 0.22f + distanceFactor * 0.30f, 0.0f, 1.0f);
    }

    public float SampleRuinDensity(Vector2I coord)
    {
        float cityNoise = FractalValueNoise(coord, _worldSeed + 5001, _settings.RuinFrequency, 5, 0.55f);
        float districtNoise = FractalValueNoise(coord, _worldSeed + 5011, _settings.RuinFrequency * 4.1f, 3, 0.45f);
        float blockNoise = FractalValueNoise(coord, _worldSeed + 5021, _settings.RuinFrequency * 10.3f, 2, 0.40f);
        return Mathf.Clamp(cityNoise * 0.58f + districtNoise * 0.30f + blockNoise * 0.12f, 0.0f, 1.0f);
    }

    public float SampleCivilization(Vector2I coord)
    {
        float oldCity = FractalValueNoise(coord, _worldSeed + 6001, _settings.CivilizationFrequency, 4, 0.52f);
        float district = FractalValueNoise(coord, _worldSeed + 6011, _settings.CivilizationFrequency * 2.8f, 3, 0.48f);
        return Mathf.Clamp(oldCity * 0.68f + district * 0.32f, 0.0f, 1.0f);
    }

    public float SampleRoadPotential(Vector2I coord)
    {
        Vector2 warped = Warp(coord, _worldSeed + 6101, _settings.RoadWarpStrength, 0.0026f);
        return SignedValueNoise(warped.X, warped.Y, _worldSeed + 6001, _settings.RoadFrequency);
    }

    public float SampleRiverPotential(Vector2I coord)
    {
        Vector2 warped = Warp(coord, _worldSeed + 8101, _settings.RiverWarpStrength, 0.0019f);
        return SignedValueNoise(warped.X, warped.Y, _worldSeed + 8001, _settings.RiverFrequency);
    }

    public float SampleResourceRichness(Vector2I coord)
    {
        float broad = FractalValueNoise(coord, _worldSeed + 7001, _settings.ResourceFrequency, 4, 0.52f);
        float mid = FractalValueNoise(coord, _worldSeed + 7011, _settings.ResourceFrequency * 2.9f, 3, 0.45f);
        float detail = FractalValueNoise(coord, _worldSeed + 7021, _settings.ResourceFrequency * 9.0f, 2, 0.40f);
        return Mathf.Clamp(broad * 0.58f + mid * 0.30f + detail * 0.12f, 0.0f, 1.0f);
    }

    private static float FractalValueNoise(Vector2I coord, int seed, float frequency, int octaves, float persistence)
    {
        float total = 0.0f;
        float amplitude = 1.0f;
        float totalAmplitude = 0.0f;
        float currentFrequency = frequency;

        for (int i = 0; i < octaves; i++)
        {
            total += ValueNoise(coord.X * currentFrequency, coord.Y * currentFrequency, seed + i * 131) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= persistence;
            currentFrequency *= 2.0f;
        }

        return totalAmplitude <= 0.0f ? 0.0f : Mathf.Clamp(total / totalAmplitude, 0.0f, 1.0f);
    }

    private static float SignedValueNoise(Vector2I coord, int seed, float frequency)
    {
        return ValueNoise(coord.X * frequency, coord.Y * frequency, seed) * 2.0f - 1.0f;
    }

    private static float SignedValueNoise(float x, float y, int seed, float frequency)
    {
        return ValueNoise(x * frequency, y * frequency, seed) * 2.0f - 1.0f;
    }

    private static Vector2 Warp(Vector2I coord, int seed, float strength, float frequency)
    {
        float offsetX = SignedValueNoise(coord.X, coord.Y, seed, frequency);
        float offsetY = SignedValueNoise(coord.X + 731.0f, coord.Y - 419.0f, seed + 17, frequency);
        return new Vector2(coord.X + offsetX * strength, coord.Y + offsetY * strength);
    }

    private static float ValueNoise(float x, float y, int seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        float tx = SmoothStep(x - x0);
        float ty = SmoothStep(y - y0);
        float a = HashUnit(seed, x0, y0);
        float b = HashUnit(seed, x1, y0);
        float c = HashUnit(seed, x0, y1);
        float d = HashUnit(seed, x1, y1);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float SmoothStep(float value)
    {
        return value * value * (3.0f - 2.0f * value);
    }

    private static float HashUnit(int seed, int x, int y)
    {
        unchecked
        {
            uint hash = 2166136261u;
            Mix(ref hash, seed);
            Mix(ref hash, x);
            Mix(ref hash, y);
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
