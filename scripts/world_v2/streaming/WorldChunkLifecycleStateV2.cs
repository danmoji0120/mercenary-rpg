namespace WorldV2;

public enum WorldChunkLifecycleStateV2
{
    Unseen,
    Generating,
    DataCached,
    Rendered,
    Dirty,
    Evicted
}
