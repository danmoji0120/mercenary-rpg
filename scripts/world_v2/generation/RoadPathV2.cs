using System.Collections.Generic;
using Godot;

namespace WorldV2;

public enum RoadKindV3
{
    Primary,
    Secondary,
    Branch
}

public sealed class RoadPathV2
{
    public int Id { get; init; }
    public int FromSiteId { get; init; }
    public int ToSiteId { get; init; }
    public int FromNodeId { get; init; } = -1;
    public int ToNodeId { get; init; } = -1;
    public IReadOnlyList<Vector2> PathPointsWorld { get; init; } = new List<Vector2>();
    public Rect2 Bounds { get; init; }
    public float Width { get; init; }
    public RoadKindV3 Kind { get; init; } = RoadKindV3.Primary;
    public bool IsMainVillageRoad { get; init; }
    public float VisualStrength { get; init; } = 1.0f;
    public int VisualWearSeed { get; init; }
    public Vector2 BranchOrigin { get; init; } = Vector2.Zero;
    public int TargetAnchorId { get; init; } = -1;
    public RoadTargetKindV3? TargetAnchorKind { get; init; }
    public IReadOnlyList<Vector2I> BridgeCandidates { get; init; } = new List<Vector2I>();
    public int RoadId => Id;
    public int FromVillageId => FromSiteId;
    public int ToVillageId => ToSiteId;
    public IReadOnlyList<Vector2> Points => PathPointsWorld;
    public bool IsPrimaryRoad => IsMainVillageRoad;
    public bool IsBranchRoad => Kind == RoadKindV3.Branch;

    public float DistanceToPath(Vector2I cell)
    {
        return DistanceToPath(new Vector2(cell.X + 0.5f, cell.Y + 0.5f));
    }

    public float DistanceToPath(Vector2 point)
    {
        if (PathPointsWorld.Count == 0)
        {
            return float.MaxValue;
        }

        if (PathPointsWorld.Count == 1)
        {
            return point.DistanceTo(PathPointsWorld[0]);
        }

        float best = float.MaxValue;
        for (int i = 1; i < PathPointsWorld.Count; i++)
        {
            best = Mathf.Min(best, DistanceToSegment(point, PathPointsWorld[i - 1], PathPointsWorld[i]));
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
