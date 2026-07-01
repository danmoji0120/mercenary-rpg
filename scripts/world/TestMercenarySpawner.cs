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
        new Vector2(384.0f, 384.0f),
        new Vector2(448.0f, 320.0f),
        new Vector2(448.0f, 384.0f),
        new Vector2(320.0f, 448.0f),
        new Vector2(384.0f, 448.0f)
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
            MercenaryProfile profile = MercenaryProfileProvider.GetStartingProfile(i);
            MercenaryController mercenary = new MercenaryController();
            mercenary.Name = $"TestMercenary{i + 1}";
            mercenary.AddToGroup("mercenaries");
            AddChild(mercenary);
            mercenary.Initialize(profile.DisplayName, _spawnPositions[i], MercenaryMoveSpeed, profile);

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
