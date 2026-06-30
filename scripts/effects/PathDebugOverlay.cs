using System.Collections.Generic;
using Godot;

public partial class PathDebugOverlay : Node2D
{
    private readonly List<Vector2> _worldPath = new();

    public void SetPath(IReadOnlyList<Vector2> worldPath)
    {
        _worldPath.Clear();
        _worldPath.AddRange(worldPath);
        QueueRedraw();
    }

    public void ClearPath()
    {
        if (_worldPath.Count == 0)
        {
            return;
        }

        _worldPath.Clear();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_worldPath.Count == 0)
        {
            return;
        }

        Color lineColor = new Color(0.18f, 0.78f, 1.0f, 0.86f);
        Color pointColor = new Color(0.72f, 0.95f, 1.0f, 0.95f);
        Color startColor = new Color(0.28f, 1.0f, 0.42f, 0.95f);
        Color goalColor = new Color(1.0f, 0.84f, 0.24f, 0.95f);

        for (int i = 1; i < _worldPath.Count; i++)
        {
            DrawLine(_worldPath[i - 1], _worldPath[i], lineColor, 2.0f);
        }

        for (int i = 0; i < _worldPath.Count; i++)
        {
            float radius = 4.0f;
            Color color = pointColor;

            if (i == 0)
            {
                radius = 6.0f;
                color = startColor;
            }
            else if (i == _worldPath.Count - 1)
            {
                radius = 6.0f;
                color = goalColor;
            }

            DrawCircle(_worldPath[i], radius, color);
            DrawArc(_worldPath[i], radius + 2.0f, 0.0f, Mathf.Tau, 24, new Color(0.02f, 0.08f, 0.1f, 0.8f), 1.0f);
        }
    }
}
