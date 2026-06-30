using Godot;

public partial class LifePointDebugRenderer : Node2D
{
    private bool _showDebugPoints = true;

    [Export]
    public bool ShowDebugPoints
    {
        get => _showDebugPoints;
        set
        {
            _showDebugPoints = value;
            QueueRedraw();
        }
    }

    [Export]
    public float MarkerRadius { get; set; } = 10.0f;

    public override void _Draw()
    {
        if (!ShowDebugPoints)
        {
            return;
        }

        Font font = ThemeDB.FallbackFont;
        Color fillColor = new Color(0.25f, 0.75f, 0.95f, 0.32f);
        Color lineColor = new Color(0.1f, 0.85f, 1.0f, 0.9f);
        Color textColor = new Color(0.88f, 0.97f, 1.0f);

        foreach (Node child in GetChildren())
        {
            if (child is not Marker2D marker)
            {
                continue;
            }

            Vector2 position = marker.Position;
            string label = GetDisplayName(marker.Name);

            DrawCircle(position, MarkerRadius, fillColor);
            DrawArc(position, MarkerRadius, 0.0f, Mathf.Tau, 32, lineColor, 2.0f);
            DrawLine(position + Vector2.Left * 14.0f, position + Vector2.Right * 14.0f, lineColor, 2.0f);
            DrawLine(position + Vector2.Up * 14.0f, position + Vector2.Down * 14.0f, lineColor, 2.0f);
            DrawString(font, position + new Vector2(14.0f, -14.0f), label, HorizontalAlignment.Left, -1.0f, 14, textColor);
        }
    }

    private static string GetDisplayName(StringName pointName)
    {
        string name = pointName.ToString();
        return name.EndsWith("Point") ? name[..^5] : name;
    }
}
