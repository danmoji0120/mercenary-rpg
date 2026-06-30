using Godot;

public partial class AttackDebugEffect : Node2D
{
    [Export]
    public double LifeTime { get; set; } = 0.2;

    public Vector2 StartWorldPosition { get; private set; }
    public Vector2 EndWorldPosition { get; private set; }

    private double _remainingLifeTime;

    public override void _Ready()
    {
        _remainingLifeTime = LifeTime;
    }

    public override void _Process(double delta)
    {
        _remainingLifeTime -= delta;

        if (_remainingLifeTime <= 0.0)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float alpha = LifeTime <= 0.0 ? 1.0f : Mathf.Clamp((float)(_remainingLifeTime / LifeTime), 0.0f, 1.0f);
        Color lineColor = new Color(1.0f, 0.92f, 0.28f, alpha);
        Color hitColor = new Color(1.0f, 0.35f, 0.18f, alpha);

        DrawLine(StartWorldPosition, EndWorldPosition, lineColor, 3.0f);
        DrawCircle(EndWorldPosition, 6.0f, new Color(hitColor.R, hitColor.G, hitColor.B, 0.28f * alpha));
        DrawLine(EndWorldPosition + Vector2.Left * 8.0f, EndWorldPosition + Vector2.Right * 8.0f, hitColor, 2.0f);
        DrawLine(EndWorldPosition + Vector2.Up * 8.0f, EndWorldPosition + Vector2.Down * 8.0f, hitColor, 2.0f);
    }

    public void Setup(Vector2 start, Vector2 end)
    {
        StartWorldPosition = start;
        EndWorldPosition = end;
        _remainingLifeTime = LifeTime;
        QueueRedraw();
    }
}
