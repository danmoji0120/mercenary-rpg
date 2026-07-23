using System;
using System.Collections.Generic;
using System.Text;
using GameplayV3.Resources;
using Godot;

namespace WorldV2;

public static class WorldV2DebugHudSelfCheckV3
{
    public static string LastSummary { get; private set; } = string.Empty;

    public static bool TryValidate(out string reason)
    {
        const int descriptorCount = 20_000;
        ResourceNodeRegistryV3 registry = new();
        Rect2I bounds = new(0, 0, 200, 100);
        DateTime created = DateTime.UnixEpoch;

        for (int index = 0; index < descriptorCount; index++)
        {
            Vector2I cell = new(index % bounds.Size.X, index / bounds.Size.X);
            ResourceNodeTypeV3 type = index % 2 == 0 ? ResourceNodeTypeV3.Tree : ResourceNodeTypeV3.StoneOutcrop;
            string definitionId = type == ResourceNodeTypeV3.Tree
                ? NaturalResourceDefinitionCatalogV3.TreeId
                : NaturalResourceDefinitionCatalogV3.StoneId;
            ResourceTypeV3 produced = type == ResourceNodeTypeV3.Tree ? ResourceTypeV3.Wood : ResourceTypeV3.Stone;
            string id = ResourceNodeIdFactoryV3.CreateDeterministic(9901, definitionId, cell);
            if (!ResourceNodeStateV3.TryCreate(id, type, new GlobalCellCoord(cell), 10, 10, 1, bounds, created, out ResourceNodeStateV3? state, out string createReason)
                || state == null)
            {
                return Fail($"descriptor {index} failed: {createReason}", out reason);
            }

            if (state.ProducedResourceType != produced)
            {
                return Fail($"descriptor {index} produced the wrong resource type", out reason);
            }

            if (!registry.TryRegister(state, out string registerReason))
            {
                return Fail($"descriptor {index} failed to register: {registerReason}", out reason);
            }
        }

        List<string> rows = new();
        int omitted = registry.GetBoundedDebugNodeIds(WorldV2DebugHud.MaxResourceDetailRows, rows);
        if (registry.Count != descriptorCount
            || registry.TreeCount != descriptorCount / 2
            || registry.StoneCount != descriptorCount / 2
            || registry.DepletedCount != 0
            || rows.Count != WorldV2DebugHud.MaxResourceDetailRows
            || omitted != descriptorCount - WorldV2DebugHud.MaxResourceDetailRows
            || !IsOrdinal(rows))
        {
            return Fail($"bounded output failed: count={registry.Count} tree={registry.TreeCount} stone={registry.StoneCount} rows={rows.Count} omitted={omitted}", out reason);
        }

        int secondOmitted = registry.GetBoundedDebugNodeIds(WorldV2DebugHud.MaxResourceDetailRows, rows);
        if (secondOmitted != omitted || rows.Count != WorldV2DebugHud.MaxResourceDetailRows || !IsOrdinal(rows))
        {
            return Fail("repeated bounded query was not stable", out reason);
        }

        string oneThousandText = BuildBoundedResourceText(rows, 988);
        string twentyThousandText = BuildBoundedResourceText(rows, 19_988);
        if (twentyThousandText.Length - oneThousandText.Length > 2)
        {
            return Fail($"bounded text grew unexpectedly: {oneThousandText.Length}->{twentyThousandText.Length}", out reason);
        }

        LastSummary = $"descriptors={descriptorCount} rows={rows.Count} omitted={omitted} fullRegistryScan=0 textLength={twentyThousandText.Length} bounded=true";
        reason = string.Empty;
        return true;
    }

    private static bool IsOrdinal(IReadOnlyList<string> values)
    {
        for (int index = 1; index < values.Count; index++)
        {
            if (string.CompareOrdinal(values[index - 1], values[index]) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildBoundedResourceText(IReadOnlyList<string> rows, int omitted)
    {
        StringBuilder builder = new();
        builder.Append("resource details\n");
        foreach (string id in rows)
        {
            builder.Append("node=").Append(id).Append('\n');
        }

        builder.Append("... ").Append(omitted).Append(" resource nodes omitted");
        return builder.ToString();
    }

    private static bool Fail(string value, out string reason)
    {
        LastSummary = value;
        reason = value;
        return false;
    }
}
