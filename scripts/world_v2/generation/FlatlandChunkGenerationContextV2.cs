using System;
using System.Collections.Generic;
using Godot;

namespace WorldV2;

public sealed class FlatlandChunkGenerationContextV2
{
    private readonly HashSet<int> _villageIds = new();
    private readonly HashSet<int> _landmarkIds = new();
    private readonly HashSet<int> _forestClusterIds = new();
    private readonly HashSet<int> _forestRegionIds = new();
    private readonly HashSet<int> _quarryClusterIds = new();
    private readonly HashSet<int> _quarryRegionIds = new();
    private readonly HashSet<int> _riverIds = new();
    private readonly HashSet<int> _roadIds = new();

    public FlatlandChunkGenerationContextV2(Vector2I chunkCoord, Vector2I originGlobalCell)
    {
        ChunkCoord = chunkCoord;
        OriginGlobalCell = originGlobalCell;
        GlobalCellBounds = new Rect2I(originGlobalCell, new Vector2I(ChunkDataV2.ChunkSize, ChunkDataV2.ChunkSize));
        Array.Fill(RiverDistance, float.MaxValue);
        Array.Fill(SiteDistance, float.MaxValue);
        Array.Fill(LandmarkKind, LandmarkKindV2.None);
    }

    public Vector2I ChunkCoord { get; }
    public Vector2I OriginGlobalCell { get; }
    public Rect2I GlobalCellBounds { get; }

    public List<VillageSiteV2> RelevantVillages { get; } = new();
    public List<LandmarkSiteV2> RelevantLandmarks { get; } = new();
    public List<ForestClusterSiteV2> RelevantForestClusters { get; } = new();
    public List<ForestRegionV3> RelevantForestRegions { get; } = new();
    public List<QuarryClusterV3> RelevantQuarryClusters { get; } = new();
    public List<QuarryRegionV3> RelevantQuarryRegions { get; } = new();
    public List<RiverPathV2> RelevantRivers { get; } = new();
    public List<RoadPathV2> RelevantRoads { get; } = new();

    public bool ContainsVillage => RelevantVillageCount > 0;
    public int RelevantVillageCount => RelevantVillages.Count;
    public int RelevantLandmarkCount => RelevantLandmarks.Count;
    public int RelevantRoadCount => RelevantRoads.Count;
    public int RelevantForestClusterCount => RelevantForestClusters.Count;
    public int RelevantForestRegionCount => RelevantForestRegions.Count;
    public int RelevantForestCandidateCount => RelevantForestClusterCount + RelevantForestRegionCount;
    public int RelevantQuarryClusterCount => RelevantQuarryClusters.Count;
    public int RelevantQuarryRegionCount => RelevantQuarryRegions.Count;
    public int RelevantQuarryCandidateCount => RelevantQuarryClusterCount + RelevantQuarryRegionCount;
    public float NearestVillageDistance { get; set; } = float.MaxValue;
    public bool HasVillageTile { get; set; }
    public bool HasRoadTile { get; set; }
    public bool HasLandmarkTile { get; set; }
    public int DirtTileCount { get; set; }
    public int ForestEdgeClearCount { get; set; }
    public int OreSpotTileCount { get; set; }
    public int SiteOverlapResolvedCount { get; set; }
    public int RejectedSitePlacementCount { get; set; }
    public int RoadCacheReadyRegionCount { get; set; }
    public int RoadCacheMissingRegionCount { get; set; }
    public int QuarryCount => CountLandmarks(LandmarkKindV2.Quarry);
    public int RuinCount => CountLandmarks(LandmarkKindV2.Ruin);
    public int DungeonCount => CountLandmarks(LandmarkKindV2.Dungeon);
    public int BanditCampCount => CountLandmarks(LandmarkKindV2.BanditCamp);
    public int FactionOutpostCount => CountLandmarks(LandmarkKindV2.FactionOutpost);
    public bool HasSlowContextWarning =>
        RelevantRoadCount > 12
        || RelevantLandmarkCount > 12
        || RelevantForestCandidateCount > 12
        || RelevantQuarryCandidateCount > 10
        || RelevantVillageCount > 4;

    public float[] RiverDistance { get; } = new float[ChunkDataV2.CellCount];
    public float[] ForestStrength { get; } = new float[ChunkDataV2.CellCount];
    public float[] SiteDistance { get; } = new float[ChunkDataV2.CellCount];
    public float[] SiteRadius { get; } = new float[ChunkDataV2.CellCount];
    public bool[] IsRiver { get; } = new bool[ChunkDataV2.CellCount];
    public bool[] IsRiverBank { get; } = new bool[ChunkDataV2.CellCount];
    public bool[] IsRoad { get; } = new bool[ChunkDataV2.CellCount];
    public float[] RoadStrength { get; } = new float[ChunkDataV2.CellCount];
    public bool[] IsBridgeCandidate { get; } = new bool[ChunkDataV2.CellCount];
    public bool[] IsVillage { get; } = new bool[ChunkDataV2.CellCount];
    public bool[] IsStartingVillage { get; } = new bool[ChunkDataV2.CellCount];
    public bool[] IsLandmark { get; } = new bool[ChunkDataV2.CellCount];
    public bool[] IsQuarry { get; } = new bool[ChunkDataV2.CellCount];
    public bool[] HasOreSpot { get; } = new bool[ChunkDataV2.CellCount];
    public LandmarkKindV2[] LandmarkKind { get; } = new LandmarkKindV2[ChunkDataV2.CellCount];
    public byte[] BaseGroundVariant { get; } = new byte[ChunkDataV2.CellCount];

    public static int ToIndex(int localX, int localY)
    {
        return ChunkDataV2.ToIndex(localX, localY);
    }

    public Vector2I ToGlobalCell(int localX, int localY)
    {
        return OriginGlobalCell + new Vector2I(localX, localY);
    }

    public bool TryGetLocalCell(Vector2I globalCell, out int localX, out int localY)
    {
        localX = globalCell.X - OriginGlobalCell.X;
        localY = globalCell.Y - OriginGlobalCell.Y;
        return ChunkDataV2.IsInBounds(localX, localY);
    }

    public string GetProfilerSummary()
    {
        string nearestVillage = NearestVillageDistance >= float.MaxValue * 0.5f
            ? "-"
            : NearestVillageDistance.ToString("0.0");
        return $"warn={HasSlowContextWarning} villageCtx={ContainsVillage} villages={RelevantVillageCount} roads={RelevantRoadCount} forests={RelevantForestCandidateCount} forestRegions={RelevantForestRegionCount} quarries={RelevantQuarryCandidateCount + QuarryCount} quarryRegions={RelevantQuarryRegionCount} oreSpots={OreSpotTileCount} ruins={RuinCount} dungeons={DungeonCount} bandits={BanditCampCount} faction={FactionOutpostCount} nearestVillage={nearestVillage} hasVillageTile={HasVillageTile} hasRoadTile={HasRoadTile} hasLandmarkTile={HasLandmarkTile} dirt={DirtTileCount} forestClears={ForestEdgeClearCount} overlapResolved={SiteOverlapResolvedCount} rejectedSites={RejectedSitePlacementCount} roadReady={RoadCacheReadyRegionCount} roadMissing={RoadCacheMissingRegionCount}";
    }

    private int CountLandmarks(LandmarkKindV2 kind)
    {
        int count = 0;
        foreach (LandmarkSiteV2 landmark in RelevantLandmarks)
        {
            if (landmark.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    public void AddVillage(VillageSiteV2 village)
    {
        if (_villageIds.Add(village.Id))
        {
            RelevantVillages.Add(village);
        }
    }

    public void AddLandmark(LandmarkSiteV2 landmark)
    {
        if (_landmarkIds.Add(landmark.Id))
        {
            RelevantLandmarks.Add(landmark);
        }
    }

    public void AddForestCluster(ForestClusterSiteV2 cluster)
    {
        if (_forestClusterIds.Add(cluster.Id))
        {
            RelevantForestClusters.Add(cluster);
        }
    }

    public void AddForestRegion(ForestRegionV3 region)
    {
        if (_forestRegionIds.Add(region.RegionId))
        {
            RelevantForestRegions.Add(region);
        }
    }

    public void AddQuarryCluster(QuarryClusterV3 quarry)
    {
        if (_quarryClusterIds.Add(quarry.Id))
        {
            RelevantQuarryClusters.Add(quarry);
        }
    }

    public void AddQuarryRegion(QuarryRegionV3 quarry)
    {
        if (_quarryRegionIds.Add(quarry.Id))
        {
            RelevantQuarryRegions.Add(quarry);
        }
    }

    public void AddRiver(RiverPathV2 river)
    {
        if (_riverIds.Add(river.Id))
        {
            RelevantRivers.Add(river);
        }
    }

    public void AddRoad(RoadPathV2 road)
    {
        if (_roadIds.Add(road.Id))
        {
            RelevantRoads.Add(road);
        }
    }
}
