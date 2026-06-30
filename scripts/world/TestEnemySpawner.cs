using Godot;

public partial class TestEnemySpawner : Node2D
{
    private readonly Vector2[] _spawnPositions =
    {
        new Vector2(704.0f, 448.0f),
        new Vector2(832.0f, 576.0f),
        new Vector2(960.0f, 416.0f)
    };

    public override void _Ready()
    {
        SpawnTestEnemies();
    }

    private void SpawnTestEnemies()
    {
        for (int i = 0; i < _spawnPositions.Length; i++)
        {
            EnemyDummyController enemy = new EnemyDummyController();
            enemy.Name = $"EnemyDummy{i + 1}";
            AddChild(enemy);
            enemy.Initialize($"Enemy Dummy {i + 1}", _spawnPositions[i]);
        }
    }
}
