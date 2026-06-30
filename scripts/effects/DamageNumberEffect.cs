using Godot;

public partial class DamageNumberEffect : Node2D
{
    [Export]
    public double LifeTime { get; set; } = 0.6;

    [Export]
    public float FloatSpeed { get; set; } = 32.0f;

    public int Damage { get; private set; }

    private Label? _label;
    private double _remainingLifeTime;

    public override void _Ready()
    {
        _remainingLifeTime = LifeTime;
        EnsureLabel();
        UpdateLabel();
    }

    public override void _Process(double delta)
    {
        _remainingLifeTime -= delta;

        if (_remainingLifeTime <= 0.0)
        {
            QueueFree();
            return;
        }

        Position += Vector2.Up * FloatSpeed * (float)delta;
        UpdateLabelAlpha();
    }

    public void Setup(Vector2 worldPosition, int damage)
    {
        GlobalPosition = worldPosition;
        Damage = damage;
        _remainingLifeTime = LifeTime;
        EnsureLabel();
        UpdateLabel();
    }

    private void EnsureLabel()
    {
        if (_label != null)
        {
            return;
        }

        _label = new Label();
        _label.Size = new Vector2(80.0f, 24.0f);
        _label.Position = new Vector2(-40.0f, -34.0f);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.AddThemeFontSizeOverride("font_size", 18);
        _label.AddThemeColorOverride("font_color", new Color(1.0f, 0.2f, 0.12f));
        _label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.7f));
        _label.AddThemeConstantOverride("shadow_offset_x", 1);
        _label.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(_label);
    }

    private void UpdateLabel()
    {
        if (_label == null)
        {
            return;
        }

        _label.Text = Damage > 0 ? $"-{Damage}" : string.Empty;
        UpdateLabelAlpha();
    }

    private void UpdateLabelAlpha()
    {
        if (_label == null)
        {
            return;
        }

        float alpha = LifeTime <= 0.0 ? 1.0f : Mathf.Clamp((float)(_remainingLifeTime / LifeTime), 0.0f, 1.0f);
        _label.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha);
    }
}
