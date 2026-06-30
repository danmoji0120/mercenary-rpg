using Godot;

public partial class EnemyDummyController : Node2D
{
    private int _maxHp = 10;

    [Export]
    public int MaxHp
    {
        get => _maxHp;
        set
        {
            _maxHp = Mathf.Max(1, value);
            CurrentHp = Mathf.Clamp(CurrentHp, 0, _maxHp);
            UpdateLabel();
        }
    }

    [Export]
    public float HitboxRadius { get; set; } = 18.0f;

    public string EnemyName { get; private set; } = "Enemy Dummy";
    public int CurrentHp { get; private set; }
    public bool IsDefeated { get; private set; }
    public Area2D? DetectionArea { get; private set; }

    private Label? _nameLabel;

    public override void _Ready()
    {
        AddToGroup("enemies");
        CurrentHp = MaxHp;

        DetectionArea = new Area2D
        {
            Name = "DetectionArea",
            CollisionLayer = 2,
            CollisionMask = 0,
            Monitoring = false,
            Monitorable = true
        };

        CollisionShape2D detectionShape = new CollisionShape2D
        {
            Name = "DetectionShape",
            Shape = new CircleShape2D
            {
                Radius = HitboxRadius
            }
        };

        DetectionArea.AddChild(detectionShape);
        AddChild(DetectionArea);

        _nameLabel = new Label
        {
            Text = EnemyName,
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-80.0f, -44.0f),
            Size = new Vector2(160.0f, 24.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        AddChild(_nameLabel);
        UpdateLabel();
        QueueRedraw();
    }

    public override void _Draw()
    {
        Color bodyColor = IsDefeated
            ? new Color(0.22f, 0.22f, 0.22f, 0.45f)
            : new Color(0.78f, 0.22f, 0.22f);

        Color outlineColor = IsDefeated
            ? new Color(0.08f, 0.08f, 0.08f, 0.6f)
            : new Color(0.2f, 0.05f, 0.05f);

        DrawCircle(Vector2.Zero, HitboxRadius, bodyColor);
        DrawArc(Vector2.Zero, HitboxRadius, 0.0f, Mathf.Tau, 40, outlineColor, 2.0f);
        DrawRect(new Rect2(-8.0f, -8.0f, 16.0f, 16.0f), new Color(0.35f, 0.08f, 0.08f));
    }

    public void Initialize(string enemyName, Vector2 spawnPosition)
    {
        EnemyName = enemyName;
        GlobalPosition = spawnPosition;
        CurrentHp = MaxHp;
        IsDefeated = false;

        UpdateLabel();
    }

    public void TakeDamage(int damage)
    {
        if (IsDefeated || damage <= 0)
        {
            return;
        }

        CurrentHp = Mathf.Max(0, CurrentHp - damage);

        if (CurrentHp <= 0)
        {
            IsDefeated = true;
        }

        UpdateLabel();
        QueueRedraw();
    }

    private void UpdateLabel()
    {
        if (_nameLabel == null)
        {
            return;
        }

        _nameLabel.Text = IsDefeated
            ? $"{EnemyName} [Defeated]"
            : $"{EnemyName} [HP {CurrentHp}/{MaxHp}]";
    }
}
