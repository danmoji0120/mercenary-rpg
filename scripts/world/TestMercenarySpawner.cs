using Godot;

public partial class TestMercenarySpawner : Node2D
{
    [Export]
    public float MercenaryMoveSpeed { get; set; } = 180.0f;

    private readonly Vector2[] _spawnPositions =
    {
        new Vector2(320.0f, 320.0f),
        new Vector2(384.0f, 320.0f),
        new Vector2(320.0f, 384.0f),
        new Vector2(384.0f, 384.0f)
    };

    private Node2D? _lifePointLayer;
    private Marker2D? _rallyPoint;

    public override void _Ready()
    {
        _lifePointLayer = GetNodeOrNull<Node2D>("../LifePointLayer");
        _rallyPoint = GetNodeOrNull<Marker2D>("../RallyPointLayer/RallyPoint");
        SpawnTestMercenaries();
    }

    private void SpawnTestMercenaries()
    {
        for (int i = 0; i < _spawnPositions.Length; i++)
        {
            MercenaryController mercenary = new MercenaryController();
            mercenary.Name = $"TestMercenary{i + 1}";
            mercenary.AddToGroup("mercenaries");
            AddChild(mercenary);
            mercenary.Initialize($"Test Mercenary {i + 1}", _spawnPositions[i], MercenaryMoveSpeed);

            if (_lifePointLayer != null)
            {
                mercenary.SetLifePoints(_lifePointLayer);
            }

            if (_rallyPoint != null)
            {
                mercenary.SetRallyPoint(_rallyPoint);
            }
        }
    }
}
