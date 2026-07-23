using System.Collections.Generic;
using Godot;

namespace WorldV2;

public sealed class WorldChunkCacheV2
{
    private readonly Dictionary<Vector2I, CacheEntry> _entriesByCoord = new();
    private int _evictedCount;

    public int MaxCachedChunkDataCount { get; set; } = 1024;
    public int MinKeepRadiusChunks { get; set; } = 6;
    public bool NeverEvictVisibleChunks { get; set; } = true;
    public double NeverEvictRecentlyVisitedSeconds { get; set; } = 20.0;
    public long CacheHitCount { get; private set; }
    public long CacheMissCount { get; private set; }
    public long StoreCount { get; private set; }
    public long CacheEvictionCount { get; private set; }
    public long GenerationStartedTotal{get;private set;}public long GenerationCompletedTotal{get;private set;}public long GenerationCancelledTotal{get;private set;}
    public int CachedChunkDataCount => CountEntriesWithData();
    public int EntryCount => _entriesByCoord.Count;

    public bool TryGetChunkData(Vector2I coord, out ChunkDataV2? data)
    {
        if (_entriesByCoord.TryGetValue(coord, out CacheEntry? entry) && entry.Data != null)
        {
            TouchEntry(entry);
            CacheHitCount++;
            data = entry.Data;
            return true;
        }

        CacheMissCount++;
        data = null;
        return false;
    }

    public bool TryPeekChunkData(Vector2I coord, out ChunkDataV2? data)
    {
        data = null;
        return _entriesByCoord.TryGetValue(coord, out CacheEntry? entry)
            && entry.Data != null
            && (data = entry.Data) != null;
    }

    public void StoreChunkData(Vector2I coord, ChunkDataV2 data)
    {
        long storeStart = WorldV2PerformanceProfiler.Instance.BeginSample();
        CacheEntry entry = GetOrCreateEntry(coord);
        if(entry.State==WorldChunkLifecycleStateV2.Generating)GenerationCompletedTotal++;
        entry.Data = data;
        entry.State = entry.IsRendered ? WorldChunkLifecycleStateV2.Rendered : WorldChunkLifecycleStateV2.DataCached;
        TouchEntry(entry);
        StoreCount++;
        WorldV2PerformanceProfiler.Instance.EndSample(WorldV2PerformanceProfiler.ChunkDataStore, storeStart, coord);
    }

    public bool Contains(Vector2I coord)
    {
        return _entriesByCoord.TryGetValue(coord, out CacheEntry? entry) && entry.Data != null;
    }

    public void Touch(Vector2I coord)
    {
        if (_entriesByCoord.TryGetValue(coord, out CacheEntry? entry))
        {
            TouchEntry(entry);
        }
    }

    public void MarkGenerating(Vector2I coord)
    {
        CacheEntry entry = GetOrCreateEntry(coord);
        if (entry.Data == null)
        {
            if(entry.State!=WorldChunkLifecycleStateV2.Generating)GenerationStartedTotal++;
            entry.State = WorldChunkLifecycleStateV2.Generating;
        }

        TouchEntry(entry);
    }

    public bool CancelGenerating(Vector2I coord)
    {
        if(!_entriesByCoord.TryGetValue(coord,out CacheEntry? entry)||entry.State!=WorldChunkLifecycleStateV2.Generating||entry.Data!=null)return false;
        _entriesByCoord.Remove(coord);GenerationCancelledTotal++;return true;
    }

    public bool IsGenerating(Vector2I coord)
    {
        return _entriesByCoord.TryGetValue(coord, out CacheEntry? entry)
            && entry.State == WorldChunkLifecycleStateV2.Generating;
    }

    public void MarkRendered(Vector2I coord)
    {
        CacheEntry entry = GetOrCreateEntry(coord);
        entry.IsRendered = true;
        if (entry.Data != null)
        {
            entry.State = entry.IsDirty ? WorldChunkLifecycleStateV2.Dirty : WorldChunkLifecycleStateV2.Rendered;
        }

        TouchEntry(entry);
    }

    public void MarkDataCached(Vector2I coord)
    {
        if (!_entriesByCoord.TryGetValue(coord, out CacheEntry? entry))
        {
            return;
        }

        entry.IsRendered = false;
        if (entry.Data != null)
        {
            entry.State = entry.IsDirty ? WorldChunkLifecycleStateV2.Dirty : WorldChunkLifecycleStateV2.DataCached;
        }

        TouchEntry(entry);
    }

    public void MarkDirty(Vector2I coord)
    {
        CacheEntry entry = GetOrCreateEntry(coord);
        entry.IsDirty = true;
        if (entry.Data != null)
        {
            entry.State = WorldChunkLifecycleStateV2.Dirty;
        }

        TouchEntry(entry);
    }

    public bool IsDirty(Vector2I coord)
    {
        return _entriesByCoord.TryGetValue(coord, out CacheEntry? entry) && entry.IsDirty;
    }

    public int EvictUntilWithinLimit(HashSet<Vector2I> protectedCoords)
    {
        int evicted = 0;
        int target = Mathf.Max(0, MaxCachedChunkDataCount);
        if (CachedChunkDataCount <= target)
        {
            return 0;
        }

        List<CacheEntry> candidates = new();
        double now = NowSeconds();
        foreach (CacheEntry entry in _entriesByCoord.Values)
        {
            if (entry.Data == null || entry.IsDirty)
            {
                continue;
            }

            if (NeverEvictVisibleChunks && protectedCoords.Contains(entry.Coord))
            {
                continue;
            }

            if (NeverEvictRecentlyVisitedSeconds > 0.0
                && now - entry.LastAccessSeconds < NeverEvictRecentlyVisitedSeconds)
            {
                continue;
            }

            candidates.Add(entry);
        }

        candidates.Sort((a, b) => a.LastAccessFrame.CompareTo(b.LastAccessFrame));
        foreach (CacheEntry entry in candidates)
        {
            if (CachedChunkDataCount <= target)
            {
                break;
            }

            entry.Data = null;
            entry.IsRendered = false;
            entry.IsDirty = false;
            entry.State = WorldChunkLifecycleStateV2.Evicted;
            _evictedCount++;
            CacheEvictionCount++;
            evicted++;
        }

        return evicted;
    }

    public WorldChunkCacheSummaryV2 GetSummary()
    {
        int cached = 0;
        int generating = 0;
        int rendered = 0;
        int dirty = 0;

        foreach (CacheEntry entry in _entriesByCoord.Values)
        {
            if (entry.Data != null)
            {
                cached++;
            }

            if (entry.State == WorldChunkLifecycleStateV2.Generating)
            {
                generating++;
            }

            if (entry.IsRendered)
            {
                rendered++;
            }

            if (entry.IsDirty)
            {
                dirty++;
            }
        }

        return new WorldChunkCacheSummaryV2(
            cached,
            generating,
            rendered,
            dirty,
            _evictedCount,
            CacheHitCount,
            CacheMissCount,
            StoreCount,
            CacheEvictionCount,
            MaxCachedChunkDataCount,
            cached * ChunkDataV2.CellCount);
    }

    public void ClearRendererOnlyState()
    {
        foreach (CacheEntry entry in _entriesByCoord.Values)
        {
            entry.IsRendered = false;
            if (entry.Data != null)
            {
                entry.State = entry.IsDirty ? WorldChunkLifecycleStateV2.Dirty : WorldChunkLifecycleStateV2.DataCached;
            }
        }
    }

    public void ClearAll()
    {
        _entriesByCoord.Clear();
        _evictedCount = 0;
        CacheHitCount = 0;
        CacheMissCount = 0;
        StoreCount = 0;
        CacheEvictionCount = 0;
        GenerationStartedTotal=0;GenerationCompletedTotal=0;GenerationCancelledTotal=0;
    }

    private CacheEntry GetOrCreateEntry(Vector2I coord)
    {
        if (_entriesByCoord.TryGetValue(coord, out CacheEntry? entry))
        {
            return entry;
        }

        entry = new CacheEntry(coord);
        _entriesByCoord[coord] = entry;
        return entry;
    }

    private void TouchEntry(CacheEntry entry)
    {
        entry.LastAccessFrame = Engine.GetProcessFrames();
        entry.LastAccessSeconds = NowSeconds();
    }

    private int CountEntriesWithData()
    {
        int count = 0;
        foreach (CacheEntry entry in _entriesByCoord.Values)
        {
            if (entry.Data != null)
            {
                count++;
            }
        }

        return count;
    }

    private static double NowSeconds()
    {
        return Time.GetTicksMsec() / 1000.0;
    }

    private sealed class CacheEntry
    {
        public CacheEntry(Vector2I coord)
        {
            Coord = coord;
            State = WorldChunkLifecycleStateV2.Unseen;
            LastAccessFrame = Engine.GetProcessFrames();
            LastAccessSeconds = NowSeconds();
        }

        public Vector2I Coord { get; }
        public ChunkDataV2? Data { get; set; }
        public WorldChunkLifecycleStateV2 State { get; set; }
        public bool IsDirty { get; set; }
        public bool IsRendered { get; set; }
        public ulong LastAccessFrame { get; set; }
        public double LastAccessSeconds { get; set; }
    }
}
