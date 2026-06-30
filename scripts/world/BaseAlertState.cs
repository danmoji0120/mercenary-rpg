using System.Collections.Generic;
using Godot;

public partial class BaseAlertState : Node
{
    [Export]
    public bool DebugBaseAlert { get; set; } = false;

    public bool EnemySpotted { get; private set; }
    public int SpottedEnemyCount { get; private set; }
    public int SpotterCount { get; private set; }
    public string AlertLabel => EnemySpotted ? "Enemy Spotted" : "Clear";

    public void UpdateFromMercenaries(IEnumerable<Node> mercenaries)
    {
        int nextSpotterCount = 0;
        HashSet<ulong> spottedEnemyIds = new();

        foreach (Node node in mercenaries)
        {
            if (node is not MercenaryController mercenary)
            {
                continue;
            }

            if (!mercenary.TryGetLifeAI(out MercenaryLifeAI? lifeAI) || lifeAI == null || !lifeAI.HasSpottedEnemy)
            {
                continue;
            }

            Node2D? spottedEnemy = lifeAI.SpottedEnemy;

            if (spottedEnemy == null || !GodotObject.IsInstanceValid(spottedEnemy))
            {
                continue;
            }

            if (spottedEnemy is EnemyDummyController enemyDummy && enemyDummy.IsDefeated)
            {
                continue;
            }

            nextSpotterCount++;
            spottedEnemyIds.Add(spottedEnemy.GetInstanceId());
        }

        bool nextEnemySpotted = nextSpotterCount > 0;

        if (DebugBaseAlert && nextEnemySpotted != EnemySpotted)
        {
            GD.Print(nextEnemySpotted ? "Base alert: Enemy Spotted" : "Base alert: Clear");
        }

        SpotterCount = nextSpotterCount;
        SpottedEnemyCount = spottedEnemyIds.Count;
        EnemySpotted = nextEnemySpotted;
    }
}
