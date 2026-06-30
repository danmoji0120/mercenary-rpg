using System.Collections.Generic;
using Godot;

public static class GridPathfinder
{
    private static readonly Vector2I[] Directions =
    {
        Vector2I.Up,
        Vector2I.Down,
        Vector2I.Left,
        Vector2I.Right
    };

    public static List<Vector2I> FindPath(Vector2I startCell, Vector2I goalCell, BaseBuildManager buildManager)
    {
        List<Vector2I> emptyPath = new();

        if (!buildManager.IsCellInWorld(goalCell) || buildManager.IsCellBlocked(goalCell))
        {
            return emptyPath;
        }

        if (startCell == goalCell)
        {
            return emptyPath;
        }

        Queue<Vector2I> frontier = new();
        Dictionary<Vector2I, Vector2I> cameFrom = new();
        HashSet<Vector2I> visited = new();

        frontier.Enqueue(startCell);
        visited.Add(startCell);

        while (frontier.Count > 0)
        {
            Vector2I current = frontier.Dequeue();

            if (current == goalCell)
            {
                return ReconstructPath(startCell, goalCell, cameFrom);
            }

            foreach (Vector2I direction in Directions)
            {
                Vector2I next = current + direction;

                if (visited.Contains(next))
                {
                    continue;
                }

                if (!buildManager.IsCellInWorld(next))
                {
                    continue;
                }

                // Door tiles are currently open and report not blocked, so BFS treats them as passable corridors.
                if (next != startCell && buildManager.IsCellBlocked(next))
                {
                    continue;
                }

                visited.Add(next);
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        return emptyPath;
    }

    private static List<Vector2I> ReconstructPath(Vector2I startCell, Vector2I goalCell, Dictionary<Vector2I, Vector2I> cameFrom)
    {
        List<Vector2I> path = new();
        Vector2I current = goalCell;

        while (current != startCell)
        {
            path.Add(current);

            if (!cameFrom.TryGetValue(current, out current))
            {
                path.Clear();
                return path;
            }
        }

        path.Reverse();
        return path;
    }
}
