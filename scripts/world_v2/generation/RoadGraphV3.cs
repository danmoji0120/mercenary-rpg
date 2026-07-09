using System.Collections.Generic;
using Godot;

namespace WorldV2;

public enum RoadNodeKindV3
{
    Village,
    Junction,
    TrunkPoint,
    BranchTarget
}

public enum RoadEdgeKindV3
{
    Primary,
    Secondary,
    Branch
}

public sealed class RoadNodeV3
{
    public int Id { get; init; }
    public RoadNodeKindV3 Kind { get; init; }
    public Vector2 Position { get; init; }
    public int VillageId { get; init; } = -1;
    public int Seed { get; init; }
}

public sealed class RoadEdgeV3
{
    public int Id { get; init; }
    public int FromNodeId { get; init; }
    public int ToNodeId { get; init; }
    public RoadEdgeKindV3 Kind { get; init; }
    public float Width { get; init; }
    public float VisualStrength { get; init; } = 1.0f;
    public int Seed { get; init; }
    public bool IsExtraLink { get; init; }
}

public sealed class RoadGraphV3
{
    private readonly List<RoadNodeV3> _nodes = new();
    private readonly List<RoadEdgeV3> _edges = new();

    public IReadOnlyList<RoadNodeV3> Nodes => _nodes;
    public IReadOnlyList<RoadEdgeV3> Edges => _edges;
    public int NodeCount => _nodes.Count;
    public int EdgeCount => _edges.Count;

    public void Clear()
    {
        _nodes.Clear();
        _edges.Clear();
    }

    public void AddNode(RoadNodeV3 node)
    {
        _nodes.Add(node);
    }

    public void AddEdge(RoadEdgeV3 edge)
    {
        _edges.Add(edge);
    }

    public bool TryGetNode(int nodeId, out RoadNodeV3? node)
    {
        foreach (RoadNodeV3 candidate in _nodes)
        {
            if (candidate.Id == nodeId)
            {
                node = candidate;
                return true;
            }
        }

        node = null;
        return false;
    }
}
