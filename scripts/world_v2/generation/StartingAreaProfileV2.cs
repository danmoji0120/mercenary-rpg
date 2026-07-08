using Godot;

namespace WorldV2;

public sealed class StartingAreaProfileV2
{
    public static readonly StartingAreaProfileV2 Default = new();

    public Vector2I Center { get; init; } = new(64, 64);
    public float CoreRadius { get; init; } = 45.0f;
    public float InnerRadius { get; init; } = 80.0f;
    public float BlendRadius { get; init; } = 140.0f;
    public float SafeRadius { get; init; } = 180.0f;
    public float RoadExitRadius { get; init; } = 260.0f;
    public int RoadExitCount { get; init; } = 4;
    public float RoadExitWarpStrength { get; init; } = 26.0f;
    public float DangerSuppression { get; init; } = 0.25f;
    public float RiverSuppression { get; init; } = 0.18f;

    public float GetDistance(Vector2I globalCell)
    {
        return new Vector2(globalCell.X - Center.X, globalCell.Y - Center.Y).Length();
    }

    public float GetTownInfluence(float distance)
    {
        if (distance <= CoreRadius)
        {
            return 1.0f;
        }

        if (distance >= BlendRadius)
        {
            return 0.0f;
        }

        float t = Mathf.Clamp((distance - CoreRadius) / (BlendRadius - CoreRadius), 0.0f, 1.0f);
        return 1.0f - SmoothStep(t);
    }

    public float GetSafeInfluence(float distance)
    {
        if (distance <= InnerRadius)
        {
            return 1.0f;
        }

        if (distance >= SafeRadius)
        {
            return 0.0f;
        }

        float t = Mathf.Clamp((distance - InnerRadius) / (SafeRadius - InnerRadius), 0.0f, 1.0f);
        return 1.0f - SmoothStep(t);
    }

    private static float SmoothStep(float value)
    {
        return value * value * (3.0f - 2.0f * value);
    }
}
