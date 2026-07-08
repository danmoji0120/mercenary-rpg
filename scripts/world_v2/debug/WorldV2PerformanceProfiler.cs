using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace WorldV2;

public sealed class WorldV2PerformanceProfiler
{
    public const string GenerateChunk = "GenerateChunk";
    public const string FlatlandSample = "FlatlandSample";
    public const string FlatlandSampleTotal = "FlatlandSampleTotal";
    public const string FlatlandBaseNoise = "FlatlandBaseNoise";
    public const string FlatlandRiverSample = "FlatlandRiverSample";
    public const string FlatlandForestSample = "FlatlandForestSample";
    public const string FlatlandRoadSample = "FlatlandRoadSample";
    public const string FlatlandSiteSample = "FlatlandSiteSample";
    public const string FlatlandQuarrySample = "FlatlandQuarrySample";
    public const string FlatlandTileResolve = "FlatlandTileResolve";
    public const string FlatlandChunkContextBuild = "FlatlandChunkContextBuild";
    public const string ContextVillageQuery = "ContextVillageQuery";
    public const string ContextLandmarkQuery = "ContextLandmarkQuery";
    public const string ContextRoadQuery = "ContextRoadQuery";
    public const string ContextForestQuery = "ContextForestQuery";
    public const string ContextRiverQuery = "ContextRiverQuery";
    public const string ContextRoadCacheEnsure = "ContextRoadCacheEnsure";
    public const string ContextRegionPlanEnsure = "ContextRegionPlanEnsure";
    public const string ContextSettlementPlanEnsure = "ContextSettlementPlanEnsure";
    public const string FlatlandChunkRasterBuild = "FlatlandChunkRasterBuild";
    public const string ChunkContext = "ChunkContext";
    public const string ChunkDataFill = "ChunkDataFill";
    public const string ChunkDataStore = "ChunkDataStore";
    public const string CacheHit = "CacheHit";
    public const string CacheMissEnqueue = "CacheMissEnqueue";
    public const string RendererAttach = "RendererAttach";
    public const string RendererRebuild = "RendererRebuild";
    public const string RendererDetach = "RendererDetach";
    public const string F11Rebuild = "F11Rebuild";
    public const string F12Reset = "F12Reset";

    public static WorldV2PerformanceProfiler Instance { get; } = new();

    private readonly Dictionary<string, MetricStats> _metricsByName = new();
    private readonly object _lock = new();
    private Vector2I _slowestChunkCoord;
    private double _slowestChunkMs;
    private string _slowestChunkContextInfo = string.Empty;

    private WorldV2PerformanceProfiler()
    {
    }

    public int GeneratedChunksThisFrame { get; private set; }
    public int RendererAttachesThisFrame { get; private set; }
    public int RendererDetachesThisFrame { get; private set; }
    public int CacheHitsThisFrame { get; private set; }
    public int CacheMissesThisFrame { get; private set; }

    public long BeginSample()
    {
        return Stopwatch.GetTimestamp();
    }

    public double EndSample(string metricName, long startTimestamp, Vector2I? chunkCoord = null)
    {
        double elapsedMs = ElapsedMilliseconds(startTimestamp);
        RecordSample(metricName, elapsedMs, chunkCoord);
        return elapsedMs;
    }

    public double EndGenerateChunkSample(long startTimestamp, Vector2I chunkCoord, string contextInfo)
    {
        double elapsedMs = ElapsedMilliseconds(startTimestamp);
        RecordSample(GenerateChunk, elapsedMs, chunkCoord, contextInfo);
        return elapsedMs;
    }

    public void RecordSample(string metricName, double elapsedMs, Vector2I? chunkCoord = null, string contextInfo = "")
    {
        lock (_lock)
        {
            MetricStats metric = GetOrCreateMetric(metricName);
            metric.Record(elapsedMs, chunkCoord);

            if (metricName == GenerateChunk && elapsedMs > _slowestChunkMs)
            {
                _slowestChunkMs = elapsedMs;
                _slowestChunkCoord = chunkCoord ?? Vector2I.Zero;
                _slowestChunkContextInfo = contextInfo;
            }
        }
    }

    public void ResetFrameCounters()
    {
        GeneratedChunksThisFrame = 0;
        RendererAttachesThisFrame = 0;
        RendererDetachesThisFrame = 0;
        CacheHitsThisFrame = 0;
        CacheMissesThisFrame = 0;
    }

    public void IncrementGeneratedChunksThisFrame()
    {
        GeneratedChunksThisFrame++;
    }

    public void IncrementRendererAttachesThisFrame()
    {
        RendererAttachesThisFrame++;
    }

    public void IncrementRendererDetachesThisFrame()
    {
        RendererDetachesThisFrame++;
    }

    public void IncrementCacheHitsThisFrame()
    {
        CacheHitsThisFrame++;
    }

    public void IncrementCacheMissesThisFrame()
    {
        CacheMissesThisFrame++;
    }

    public WorldV2PerformanceSummary GetSummary()
    {
        lock (_lock)
        {
            return new WorldV2PerformanceSummary(
                GetMetricSummaryUnlocked(GenerateChunk),
                GetMetricSummaryUnlocked(FlatlandSample),
                GetMetricSummaryUnlocked(ChunkContext),
                GetMetricSummaryUnlocked(ChunkDataFill),
                GetMetricSummaryUnlocked(ChunkDataStore),
                GetMetricSummaryUnlocked(CacheHit),
                GetMetricSummaryUnlocked(CacheMissEnqueue),
                GetMetricSummaryUnlocked(RendererAttach),
                GetMetricSummaryUnlocked(RendererRebuild),
                GetMetricSummaryUnlocked(RendererDetach),
                GetMetricSummaryUnlocked(F11Rebuild),
                GetMetricSummaryUnlocked(F12Reset),
                GeneratedChunksThisFrame,
                RendererAttachesThisFrame,
                RendererDetachesThisFrame,
                CacheHitsThisFrame,
                CacheMissesThisFrame,
                _slowestChunkCoord,
                _slowestChunkMs,
                _slowestChunkContextInfo);
        }
    }

    public void PrintSummary()
    {
        WorldV2PerformanceSummary summary = GetSummary();
        GD.Print("WorldV2 performance summary:");
        PrintMetric(summary.GenerateChunk);
        PrintMetric(summary.FlatlandSample);
        PrintMetric(GetMetricSummary(FlatlandSampleTotal));
        PrintMetric(GetMetricSummary(FlatlandBaseNoise));
        PrintMetric(GetMetricSummary(FlatlandRiverSample));
        PrintMetric(GetMetricSummary(FlatlandForestSample));
        PrintMetric(GetMetricSummary(FlatlandRoadSample));
        PrintMetric(GetMetricSummary(FlatlandSiteSample));
        PrintMetric(GetMetricSummary(FlatlandQuarrySample));
        PrintMetric(GetMetricSummary(FlatlandTileResolve));
        PrintMetric(GetMetricSummary(FlatlandChunkContextBuild));
        PrintMetric(GetMetricSummary(ContextRegionPlanEnsure));
        PrintMetric(GetMetricSummary(ContextSettlementPlanEnsure));
        PrintMetric(GetMetricSummary(ContextVillageQuery));
        PrintMetric(GetMetricSummary(ContextLandmarkQuery));
        PrintMetric(GetMetricSummary(ContextRoadCacheEnsure));
        PrintMetric(GetMetricSummary(ContextRoadQuery));
        PrintMetric(GetMetricSummary(ContextForestQuery));
        PrintMetric(GetMetricSummary(ContextRiverQuery));
        PrintMetric(GetMetricSummary(FlatlandChunkRasterBuild));
        PrintMetric(summary.ChunkContext);
        PrintMetric(summary.ChunkDataFill);
        PrintMetric(summary.ChunkDataStore);
        PrintMetric(summary.CacheHit);
        PrintMetric(summary.CacheMissEnqueue);
        PrintMetric(summary.RendererAttach);
        PrintMetric(summary.RendererRebuild);
        PrintMetric(summary.RendererDetach);
        PrintMetric(summary.F11Rebuild);
        PrintMetric(summary.F12Reset);
        GD.Print($"  frame: generated={summary.GeneratedChunksThisFrame} attaches={summary.RendererAttachesThisFrame} detaches={summary.RendererDetachesThisFrame} cacheHits={summary.CacheHitsThisFrame} cacheMisses={summary.CacheMissesThisFrame}");
        GD.Print($"  slowestChunk={summary.SlowestChunkCoord} slowestChunkMs={summary.SlowestChunkMs:0.000}");
        GD.Print($"  slowestChunkInfo={summary.SlowestChunkContextInfo}");
        GD.Print($"  {FlatlandWorldPlanV2.GetDebugToggleSummary()}");
        GD.Print($"  {WorldGenerationLayerSettingsV2.GetSummary()}");
    }

    public static double ElapsedMilliseconds(long startTimestamp)
    {
        long elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        return elapsed * 1000.0 / Stopwatch.Frequency;
    }

    public WorldV2MetricSummary GetMetricSummary(string metricName)
    {
        lock (_lock)
        {
            return GetMetricSummaryUnlocked(metricName);
        }
    }

    private WorldV2MetricSummary GetMetricSummaryUnlocked(string metricName)
    {
        return _metricsByName.TryGetValue(metricName, out MetricStats? metric)
            ? metric.ToSummary(metricName)
            : new WorldV2MetricSummary(metricName, 0.0, 0.0, 0.0, 0, Vector2I.Zero);
    }

    private MetricStats GetOrCreateMetric(string metricName)
    {
        if (_metricsByName.TryGetValue(metricName, out MetricStats? metric))
        {
            return metric;
        }

        metric = new MetricStats();
        _metricsByName[metricName] = metric;
        return metric;
    }

    private static void PrintMetric(WorldV2MetricSummary metric)
    {
        GD.Print($"  {metric.Name}: last={metric.LastMs:0.000}ms avg={metric.AverageMs:0.000}ms max={metric.MaxMs:0.000}ms samples={metric.SampleCount} slowestChunk={metric.SlowestChunkCoord}");
    }

    private sealed class MetricStats
    {
        private double _totalMs;

        public double LastMs { get; private set; }
        public double MaxMs { get; private set; }
        public long Count { get; private set; }
        public Vector2I SlowestChunkCoord { get; private set; }

        public void Record(double elapsedMs, Vector2I? chunkCoord)
        {
            LastMs = elapsedMs;
            _totalMs += elapsedMs;
            Count++;

            if (elapsedMs > MaxMs)
            {
                MaxMs = elapsedMs;
                SlowestChunkCoord = chunkCoord ?? Vector2I.Zero;
            }
        }

        public WorldV2MetricSummary ToSummary(string name)
        {
            double average = Count > 0 ? _totalMs / Count : 0.0;
            return new WorldV2MetricSummary(name, LastMs, average, MaxMs, Count, SlowestChunkCoord);
        }
    }
}
