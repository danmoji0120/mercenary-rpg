using Godot;

namespace WorldV2;

public readonly struct WorldV2MetricSummary
{
    public WorldV2MetricSummary(string name, double lastMs, double averageMs, double maxMs, long sampleCount, Vector2I slowestChunkCoord)
    {
        Name = name;
        LastMs = lastMs;
        AverageMs = averageMs;
        MaxMs = maxMs;
        SampleCount = sampleCount;
        SlowestChunkCoord = slowestChunkCoord;
    }

    public string Name { get; }
    public double LastMs { get; }
    public double AverageMs { get; }
    public double MaxMs { get; }
    public long SampleCount { get; }
    public Vector2I SlowestChunkCoord { get; }
}

public readonly struct WorldV2PerformanceSummary
{
    public WorldV2PerformanceSummary(
        WorldV2MetricSummary generateChunk,
        WorldV2MetricSummary flatlandSample,
        WorldV2MetricSummary chunkContext,
        WorldV2MetricSummary chunkDataFill,
        WorldV2MetricSummary chunkDataStore,
        WorldV2MetricSummary cacheHit,
        WorldV2MetricSummary cacheMissEnqueue,
        WorldV2MetricSummary rendererAttach,
        WorldV2MetricSummary rendererRebuild,
        WorldV2MetricSummary rendererDetach,
        WorldV2MetricSummary f11Rebuild,
        WorldV2MetricSummary f12Reset,
        int generatedChunksThisFrame,
        int rendererAttachesThisFrame,
        int rendererDetachesThisFrame,
        int cacheHitsThisFrame,
        int cacheMissesThisFrame,
        Vector2I slowestChunkCoord,
        double slowestChunkMs,
        string slowestChunkContextInfo)
    {
        GenerateChunk = generateChunk;
        FlatlandSample = flatlandSample;
        ChunkContext = chunkContext;
        ChunkDataFill = chunkDataFill;
        ChunkDataStore = chunkDataStore;
        CacheHit = cacheHit;
        CacheMissEnqueue = cacheMissEnqueue;
        RendererAttach = rendererAttach;
        RendererRebuild = rendererRebuild;
        RendererDetach = rendererDetach;
        F11Rebuild = f11Rebuild;
        F12Reset = f12Reset;
        GeneratedChunksThisFrame = generatedChunksThisFrame;
        RendererAttachesThisFrame = rendererAttachesThisFrame;
        RendererDetachesThisFrame = rendererDetachesThisFrame;
        CacheHitsThisFrame = cacheHitsThisFrame;
        CacheMissesThisFrame = cacheMissesThisFrame;
        SlowestChunkCoord = slowestChunkCoord;
        SlowestChunkMs = slowestChunkMs;
        SlowestChunkContextInfo = slowestChunkContextInfo;
    }

    public WorldV2MetricSummary GenerateChunk { get; }
    public WorldV2MetricSummary FlatlandSample { get; }
    public WorldV2MetricSummary ChunkContext { get; }
    public WorldV2MetricSummary ChunkDataFill { get; }
    public WorldV2MetricSummary ChunkDataStore { get; }
    public WorldV2MetricSummary CacheHit { get; }
    public WorldV2MetricSummary CacheMissEnqueue { get; }
    public WorldV2MetricSummary RendererAttach { get; }
    public WorldV2MetricSummary RendererRebuild { get; }
    public WorldV2MetricSummary RendererDetach { get; }
    public WorldV2MetricSummary F11Rebuild { get; }
    public WorldV2MetricSummary F12Reset { get; }
    public int GeneratedChunksThisFrame { get; }
    public int RendererAttachesThisFrame { get; }
    public int RendererDetachesThisFrame { get; }
    public int CacheHitsThisFrame { get; }
    public int CacheMissesThisFrame { get; }
    public Vector2I SlowestChunkCoord { get; }
    public double SlowestChunkMs { get; }
    public string SlowestChunkContextInfo { get; }
}
