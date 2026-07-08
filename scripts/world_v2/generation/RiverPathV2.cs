using System.Collections.Generic;
using Godot;

namespace WorldV2;

public sealed class RiverPathV2
{
    public int Id { get; init; }
    public IReadOnlyList<Vector2> ControlPointsWorld { get; init; } = new List<Vector2>();
    public float Width { get; init; }
    public float BankWidth { get; init; }
    public float MeanderStrength { get; init; }

    public bool ContainsCell(Vector2I cell)
    {
        return DistanceToPath(cell) <= Width;
    }

    public float DistanceToPath(Vector2I cell)
    {
        return DistanceToPath(new Vector2(cell.X + 0.5f, cell.Y + 0.5f));
    }

    public float DistanceToPath(Vector2 point)
    {
        if (ControlPointsWorld.Count == 0)
        {
            return float.MaxValue;
        }

        if (ControlPointsWorld.Count == 1)
        {
            return point.DistanceTo(ControlPointsWorld[0]);
        }

        float best = float.MaxValue;
        for (int i = 1; i < ControlPointsWorld.Count; i++)
        {
            best = Mathf.Min(best, DistanceToSegment(point, ControlPointsWorld[i - 1], ControlPointsWorld[i]));
        }

        return best;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return point.DistanceTo(a);
        }

        float t = Mathf.Clamp((point - a).Dot(ab) / lengthSquared, 0.0f, 1.0f);
        return point.DistanceTo(a + ab * t);
    }
}
