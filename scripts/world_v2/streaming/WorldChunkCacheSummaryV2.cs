namespace WorldV2;

public readonly struct WorldChunkCacheSummaryV2
{
    public WorldChunkCacheSummaryV2(
        int cachedChunkDataCount,
        int generatingCount,
        int renderedCount,
        int dirtyCount,
        int evictedCount,
        long cacheHitCount,
        long cacheMissCount,
        long storeCount,
        long cacheEvictionCount,
        int maxCachedChunkDataCount,
        int approxCachedCellCount)
    {
        CachedChunkDataCount = cachedChunkDataCount;
        GeneratingCount = generatingCount;
        RenderedCount = renderedCount;
        DirtyCount = dirtyCount;
        EvictedCount = evictedCount;
        CacheHitCount = cacheHitCount;
        CacheMissCount = cacheMissCount;
        StoreCount = storeCount;
        CacheEvictionCount = cacheEvictionCount;
        MaxCachedChunkDataCount = maxCachedChunkDataCount;
        ApproxCachedCellCount = approxCachedCellCount;
    }

    public int CachedChunkDataCount { get; }
    public int GeneratingCount { get; }
    public int RenderedCount { get; }
    public int DirtyCount { get; }
    public int EvictedCount { get; }
    public long CacheHitCount { get; }
    public long CacheMissCount { get; }
    public long StoreCount { get; }
    public long CacheEvictionCount { get; }
    public int MaxCachedChunkDataCount { get; }
    public int ApproxCachedCellCount { get; }
}
