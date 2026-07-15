using System;
using System.Collections.Generic;
using Godot;
using WorldV2;

namespace GameplayV3.Deployment;

public sealed class StartingDeploymentWorldQueryV3
{
    public StartingDeploymentWorldQueryV3(
        int worldSeed,
        Rect2I worldBounds,
        VillageSiteV2 startingSettlement,
        IReadOnlyList<RoadPathV2> roads,
        IReadOnlyList<RuinSiteV3> ruins,
        IReadOnlyList<QuarryRegionV3> quarries,
        IReadOnlyList<DungeonEntranceSiteV3> dungeons,
        IReadOnlyList<BanditCampSiteV3> banditCamps,
        IReadOnlyList<FactionOutpostSiteV3> factionOutposts,
        Func<Vector2I, FlatlandCellSampleV2> sampleCell)
    {
        WorldSeed = worldSeed;
        WorldBounds = worldBounds;
        StartingSettlement = startingSettlement;
        Roads = roads;
        Ruins = ruins;
        Quarries = quarries;
        Dungeons = dungeons;
        BanditCamps = banditCamps;
        FactionOutposts = factionOutposts;
        SampleCell = sampleCell;
    }

    public int WorldSeed { get; }
    public Rect2I WorldBounds { get; }
    public VillageSiteV2 StartingSettlement { get; }
    public IReadOnlyList<RoadPathV2> Roads { get; }
    public IReadOnlyList<RuinSiteV3> Ruins { get; }
    public IReadOnlyList<QuarryRegionV3> Quarries { get; }
    public IReadOnlyList<DungeonEntranceSiteV3> Dungeons { get; }
    public IReadOnlyList<BanditCampSiteV3> BanditCamps { get; }
    public IReadOnlyList<FactionOutpostSiteV3> FactionOutposts { get; }
    public Func<Vector2I, FlatlandCellSampleV2> SampleCell { get; }
}
