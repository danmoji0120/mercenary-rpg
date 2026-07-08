namespace WorldV2;

public readonly struct FlatlandWorldPlanSummaryV2
{
    public FlatlandWorldPlanSummaryV2(
        int roadPathCount,
        int villageCount,
        int landmarkCount,
        int roadConnectedLandmarkCount,
        int quarryCount)
    {
        RoadPathCount = roadPathCount;
        VillageCount = villageCount;
        LandmarkCount = landmarkCount;
        RoadConnectedLandmarkCount = roadConnectedLandmarkCount;
        QuarryCount = quarryCount;
    }

    public int RoadPathCount { get; }
    public int VillageCount { get; }
    public int LandmarkCount { get; }
    public int RoadConnectedLandmarkCount { get; }
    public int QuarryCount { get; }
}
