using System.Collections.Generic;
using Godot;

namespace WorldV2;

public partial class WorldStreamManagerV2 : Node
{
    [Export]
    public int RenderRadiusChunks { get; set; } = 2;

    [Export]
    public int InitialRenderRadiusChunks { get; set; } = 1;

    [Export]
    public int InitialStreamingWarmupFrames { get; set; } = 45;

    [Export]
    public int InitialRequiredRadiusChunks { get; set; } = 1;

    [Export]
    public int MaxInitialChunksPerFrame { get; set; } = 1;

    [Export]
    public int PreloadMarginChunks { get; set; } = 1;

    [Export]
    public int DataCacheRadiusChunks { get; set; } = 10;

    [Export]
    public int MaxRenderedChunkCountHardLimit { get; set; } = 128;

    [Export]
    public int MaxRequestedChunkCountPerFrame { get; set; } = 8;

    [Export]
    public int MaxRendererAttachPerFrame { get; set; } = 4;

    [Export]
    public int MaxRendererDetachPerFrame { get; set; } = 8;

    [Export]
    public int MaxChunkGenerationsPerFrame { get; set; } = 1;

    [Export]
    public float MaxChunkGenerationMillisecondsPerFrame { get; set; } = 5.0f;

    [Export]
    public bool IdlePrefetchEnabled { get; set; } = true;

    [Export]
    public int IdlePrefetchRadiusChunks { get; set; } = 5;

    [Export]
    public float IdlePrefetchSpeedThreshold { get; set; } = 30.0f;

    [Export]
    public bool DirectionalPrefetchEnabled { get; set; } = false;

    [Export]
    public int DirectionalPrefetchDistanceChunks { get; set; } = 5;

    [Export]
    public int DirectionalPrefetchWidthChunks { get; set; } = 2;

    [Export]
    public float DirectionalPrefetchSpeedThreshold { get; set; } = 900.0f;

    [Export]
    public int MaxPregeneratedChunksPerFrame { get; set; } = 1;

    [Export]
    public float MaxPrefetchFrameBudgetMs { get; set; } = 3.0f;

    [Export]
    public int MaxCompletedChunkApplyPerFrame { get; set; } = 4;

    [Export]
    public int BootstrapDelayFrames { get; set; } = 1;

    [Export]
    public int MaxCachedChunkDataCount { get; set; } = 1024;

    [Export]
    public int MinKeepRadiusChunks { get; set; } = 6;

    [Export]
    public bool NeverEvictVisibleChunks { get; set; } = true;

    [Export]
    public double NeverEvictRecentlyVisitedSeconds { get; set; } = 20.0;

    [Export]
    public bool EnableChunkRendererPooling { get; set; } = true;

    [Export]
    public int MaxPooledChunkRenderers { get; set; } = 128;

    [Export]
    public NodePath WorldManagerPath { get; set; } = "../WorldManagerV2";

    [Export]
    public NodePath CameraPath { get; set; } = "../Camera2D";

    [Export]
    public NodePath ChunkRootPath { get; set; } = "../GridLayer";

    [Export]
    public int TileSize { get; set; } = 24;

    [Export]
    public bool ShowGrid { get; set; } = false;

    [Export]
    public WorldV2OverlayMode OverlayMode { get; set; } = WorldV2OverlayMode.Normal;

    public Vector2I CenterGlobalCellCoord { get; private set; }
    public Vector2I CenterGlobalChunkCoord { get; private set; }
    public Rect2I VisibleChunkRange { get; private set; }
    public Rect2I RequiredChunkRange { get; private set; }
    public int RequiredChunkCount { get; private set; }
    public int ClampedRequiredChunkCount { get; private set; }
    public int DesiredRenderedChunkCount { get; private set; }
    public int GeneratedChunksThisFrame { get; private set; }
    public int LastEvictedChunkCount { get; private set; }
    public Vector2 CameraWorldPosition => _camera?.GlobalPosition ?? Vector2.Zero;
    public Vector2 CameraVelocity => _camera is WorldV2CameraController controller ? controller.Velocity : Vector2.Zero;
    public float CameraSpeed => CameraVelocity.Length();
    public Vector2 CameraZoom => _camera?.Zoom ?? Vector2.One;
    public int LoadedChunkCount => RenderedChunkCount;
    public int RenderedChunkCount => _chunkRenderersByCoord.Count;
    public int PendingGenerationCount => _generationQueue.Count;
    public int RendererPoolCount => _rendererPool.Count;
    public bool LastRequiredRangeWasClamped { get; private set; }
    public bool IsRenderClamped => LastRequiredRangeWasClamped;
    public int AttachQueueCount => _attachQueue.Count;
    public int DetachQueueCount => _detachQueue.Count;
    public int RequestedThisFrame { get; private set; }
    public int AttachedThisFrame { get; private set; }
    public int DetachedThisFrame { get; private set; }
    public int PregeneratedThisFrame { get; private set; }
    public int WorkerGeneratedThisFrame { get; private set; }
    public int CompletedAppliedThisFrame { get; private set; }
    public int MainThreadChunkGenerationCount { get; private set; }
    public WorldStreamingModeV2 StreamingMode { get; private set; } = WorldStreamingModeV2.InitialLoading;
    public bool IsInitialLoadingComplete { get; private set; }
    public int InitialLoadingTargetChunks => _initialRequiredChunks.Count;
    public int InitialLoadingLoadedChunks { get; private set; }
    public float InitialLoadingProgress => !_streamingStarted || InitialLoadingTargetChunks <= 0 ? 0.0f : InitialLoadingLoadedChunks / (float)InitialLoadingTargetChunks;
    public int RequiredQueueCount => _generationQueue.Count;
    public int WarmQueueCount => _warmGenerationQueue.Count;
    public int PrefetchQueueCount => _directionalPrefetchQueue.Count + _idlePrefetchQueue.Count;
    public int CachedChunkCount => _chunkCache.CachedChunkDataCount;
    public int WorkerPendingCount => _generationWorker?.PendingCount ?? 0;
    public int WorkerCompletedCount => _generationWorker?.CompletedCount ?? 0;
    public double WorkerAverageMs => _generationWorker?.AverageMs ?? 0.0;
    public double WorkerMaxMs => _generationWorker?.MaxMs ?? 0.0;

    private readonly Dictionary<Vector2I, ChunkRendererV2> _chunkRenderersByCoord = new();
    private readonly Stack<ChunkRendererV2> _rendererPool = new();
    private readonly List<Vector2I> _generationQueue = new();
    private readonly List<Vector2I> _warmGenerationQueue = new();
    private readonly List<Vector2I> _directionalPrefetchQueue = new();
    private readonly List<Vector2I> _idlePrefetchQueue = new();
    private readonly HashSet<Vector2I> _pendingGeneration = new();
    private readonly HashSet<Vector2I> _pendingWarmGeneration = new();
    private readonly HashSet<Vector2I> _pendingDirectionalPrefetch = new();
    private readonly HashSet<Vector2I> _pendingIdlePrefetch = new();
    private readonly HashSet<Vector2I> _desiredRenderChunks = new();
    private readonly List<Vector2I> _desiredRenderOrder = new();
    private readonly List<Vector2I> _attachQueue = new();
    private readonly List<Vector2I> _detachQueue = new();
    private readonly HashSet<Vector2I> _pendingAttach = new();
    private readonly HashSet<Vector2I> _pendingDetach = new();
    private readonly WorldChunkCacheV2 _chunkCache = new();
    private WorldManagerV2? _worldManager;
    private Camera2D? _camera;
    private Node2D? _chunkRoot;
    private WorldChunkGenerationWorkerV2? _generationWorker;
    private double _lastClampLogSeconds = -999.0;
    private bool _lastClampLoggedState;
    private Vector2I _lastDesiredCenterChunk = new(int.MinValue, int.MinValue);
    private Rect2I _lastDesiredRequiredRange = new(Vector2I.Zero, Vector2I.Zero);
    private int _lastDesiredZoomBucket = int.MinValue;
    private Vector2I _lastDesiredViewportBucket = new(int.MinValue, int.MinValue);
    private int _streamingFrameCount;
    private int _bootstrapFrameCount;
    private bool _streamingStarted;
    private bool _initialRequiredChunksInitialized;
    private readonly HashSet<Vector2I> _initialRequiredChunks = new();

    public override void _Ready()
    {
        ResolveReferences();
    }

    public override void _ExitTree()
    {
        StopGenerationWorker();
    }

    public override void _Process(double delta)
    {
        WorldV2PerformanceProfiler.Instance.ResetFrameCounters();
        GeneratedChunksThisFrame = 0;
        LastEvictedChunkCount = 0;
        RequestedThisFrame = 0;
        AttachedThisFrame = 0;
        DetachedThisFrame = 0;
        PregeneratedThisFrame = 0;
        WorkerGeneratedThisFrame = 0;
        CompletedAppliedThisFrame = 0;
        if (!EnsureStreamingStarted())
        {
            _worldManager?.UpdateDebugHud();
            return;
        }

        ProcessCompletedChunkResults();
        RefreshStreaming(force: false);
        ProcessGenerationQueue();
        ProcessAttachQueue();
        ProcessPrefetchQueues();
        UpdateInitialLoadingState();
        UpdateStreamingMode();
        EvictColdChunkData();
        _worldManager?.UpdateDebugHud();
        _streamingFrameCount++;
    }

    public void RefreshStreaming(bool force)
    {
        ResolveReferences();

        if (!_streamingStarted || _worldManager == null || _camera == null || _chunkRoot == null)
        {
            return;
        }

        SyncCacheSettings();

        if (_camera is WorldV2CameraController cameraController)
        {
            cameraController.ClampToWorldBounds(_worldManager.WorldBounds);
        }

        Vector2I centerGlobalCell = WorldToGlobalCell(_camera.GlobalPosition);
        Vector2I centerGlobalChunk = WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(centerGlobalCell);
        Rect2I requiredRange = !IsInitialLoadingComplete
            ? CalculateInitialChunkRange(centerGlobalChunk)
            : CalculateRequiredChunkRange(centerGlobalCell, centerGlobalChunk);
        int zoomBucket = GetZoomBucket(CameraZoom);
        Vector2I viewportBucket = GetViewportBucket();

        CenterGlobalCellCoord = centerGlobalCell;
        CenterGlobalChunkCoord = centerGlobalChunk;
        RequiredChunkRange = requiredRange;
        RequiredChunkCount = Mathf.Max(0, requiredRange.Size.X) * Mathf.Max(0, requiredRange.Size.Y);
        EnsureInitialRequiredChunks(centerGlobalChunk);

        bool shouldRecomputeDesired = force
            || centerGlobalChunk != _lastDesiredCenterChunk
            || zoomBucket != _lastDesiredZoomBucket
            || viewportBucket != _lastDesiredViewportBucket
            || requiredRange != _lastDesiredRequiredRange;

        if (shouldRecomputeDesired)
        {
            RebuildDesiredRenderSet(requiredRange, centerGlobalChunk, zoomBucket, viewportBucket);
            QueueRenderersOutsideDesiredSet();
        }

        ProcessDetachQueue();

        int requestLimit = LastRequiredRangeWasClamped
            ? Mathf.Max(1, MaxRequestedChunkCountPerFrame / 2)
            : Mathf.Max(1, MaxRequestedChunkCountPerFrame);
        foreach (Vector2I chunkCoord in _desiredRenderOrder)
        {
            if (_chunkRenderersByCoord.ContainsKey(chunkCoord))
            {
                _chunkCache.Touch(chunkCoord);
                continue;
            }

            if (_pendingAttach.Contains(chunkCoord) || _pendingGeneration.Contains(chunkCoord))
            {
                continue;
            }

            if (PromoteQueuedChunkToRequired(chunkCoord))
            {
                continue;
            }

            long cacheHitStart = WorldV2PerformanceProfiler.Instance.BeginSample();
            bool cacheHit = _chunkCache.TryGetChunkData(chunkCoord, out ChunkDataV2? cachedData) && cachedData != null;
            if (cacheHit && cachedData != null)
            {
                WorldV2PerformanceProfiler.Instance.EndSample(WorldV2PerformanceProfiler.CacheHit, cacheHitStart, chunkCoord);
                WorldV2PerformanceProfiler.Instance.IncrementCacheHitsThisFrame();
                QueueRendererAttach(chunkCoord);
                continue;
            }

            if (RequestedThisFrame >= requestLimit)
            {
                continue;
            }

            long enqueueStart = WorldV2PerformanceProfiler.Instance.BeginSample();
            bool queued = QueueChunkGeneration(chunkCoord);
            WorldV2PerformanceProfiler.Instance.EndSample(WorldV2PerformanceProfiler.CacheMissEnqueue, enqueueStart, chunkCoord);
            if (queued)
            {
                WorldV2PerformanceProfiler.Instance.IncrementCacheMissesThisFrame();
                RequestedThisFrame++;
            }
        }

        if (IsInitialLoadingComplete)
        {
            QueueWarmChunks(requiredRange, centerGlobalChunk);
            QueueDirectionalPrefetchChunks(centerGlobalChunk);
            QueueIdlePrefetchChunks(centerGlobalChunk);
        }

        ProcessAttachQueue();
        _worldManager.UpdateStreamingCenter(centerGlobalCell, centerGlobalChunk, updateHud: false);
    }

    private bool EnsureStreamingStarted()
    {
        if (_streamingStarted)
        {
            return true;
        }

        ResolveReferences();
        if (_worldManager == null)
        {
            return false;
        }

        if (_bootstrapFrameCount++ < Mathf.Max(0, BootstrapDelayFrames))
        {
            return false;
        }

        StartGenerationWorker();
        _streamingStarted = true;
        RefreshStreaming(force: true);
        return true;
    }

    private void StartGenerationWorker()
    {
        if (_worldManager == null || _generationWorker != null)
        {
            return;
        }

        _generationWorker = new WorldChunkGenerationWorkerV2();
        _generationWorker.Start(_worldManager.WorldId, _worldManager.WorldSeed, _worldManager.GetGenerationSettings(), _worldManager.GetGenerationRequest());
    }

    public bool TryGetLoadedCell(Vector2I globalCellCoord, out CellData? cell)
    {
        Vector2I globalChunkCoord = WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(globalCellCoord);
        cell = null;
        return _chunkCache.TryPeekChunkData(globalChunkCoord, out ChunkDataV2? chunk)
            && chunk != null
            && chunk.TryGetCell(globalCellCoord, out cell);
    }

    public bool IsChunkLoaded(Vector2I globalChunkCoord)
    {
        return _chunkCache.Contains(globalChunkCoord);
    }

    public IReadOnlyCollection<Vector2I> GetLoadedChunkCoords()
    {
        return _chunkRenderersByCoord.Keys;
    }

    public WorldChunkCacheSummaryV2 GetChunkCacheSummary()
    {
        return _chunkCache.GetSummary();
    }

    public void PrintLoadedChunks()
    {
        WorldChunkCacheSummaryV2 summary = _chunkCache.GetSummary();
        GD.Print($"WorldV2 streaming: mode={StreamingMode} initial={InitialLoadingLoadedChunks}/{InitialLoadingTargetChunks} rendered={RenderedChunkCount} desired={DesiredRenderedChunkCount} required={RequiredChunkCount} clamped={ClampedRequiredChunkCount} isClamped={IsRenderClamped} requiredQueue={RequiredQueueCount} warmQueue={WarmQueueCount} prefetchQueue={PrefetchQueueCount} generatedFrame={GeneratedChunksThisFrame} pregeneratedFrame={PregeneratedThisFrame} requestedFrame={RequestedThisFrame} attachFrame={AttachedThisFrame} detachFrame={DetachedThisFrame} attachQueue={AttachQueueCount} detachQueue={DetachQueueCount} pool={RendererPoolCount} center={CenterGlobalChunkCoord} range={VisibleChunkRange.Position}..{VisibleChunkRange.End - Vector2I.One}");
        GD.Print($"WorldV2 worker: pending={WorkerPendingCount} completed={WorkerCompletedCount} generatedFrame={WorkerGeneratedThisFrame} appliedFrame={CompletedAppliedThisFrame} mainThreadGeneration={MainThreadChunkGenerationCount} avgMs={WorkerAverageMs:0.000} maxMs={WorkerMaxMs:0.000}");
        GD.Print($"WorldV2 chunk cache: cached={summary.CachedChunkDataCount}/{summary.MaxCachedChunkDataCount} generating={summary.GeneratingCount} dirty={summary.DirtyCount} hit={summary.CacheHitCount} miss={summary.CacheMissCount} evicted={summary.CacheEvictionCount}");
        WorldV2PerformanceProfiler.Instance.PrintSummary();

        foreach (Vector2I chunkCoord in _chunkRenderersByCoord.Keys)
        {
            Vector2I sectorCoord = WorldV2CoordinateUtility.GlobalChunkToSectorCoord(chunkCoord);
            Vector2I localChunkCoord = WorldV2CoordinateUtility.GlobalChunkToLocalChunkInSector(chunkCoord);
            GD.Print($"  rendered chunk={chunkCoord} sector={sectorCoord} localChunk={localChunkCoord}");
        }
    }

    public void PrintChunkCacheSummary()
    {
        WorldChunkCacheSummaryV2 summary = _chunkCache.GetSummary();
        GD.Print($"WorldV2 chunk cache summary: cached={summary.CachedChunkDataCount}/{summary.MaxCachedChunkDataCount} rendered={summary.RenderedCount} generating={summary.GeneratingCount} dirty={summary.DirtyCount} evictedEntries={summary.EvictedCount} hit={summary.CacheHitCount} miss={summary.CacheMissCount} stores={summary.StoreCount} evictions={summary.CacheEvictionCount}");
        GD.Print($"  worker pending={WorkerPendingCount} completed={WorkerCompletedCount} generatedFrame={WorkerGeneratedThisFrame} appliedFrame={CompletedAppliedThisFrame} avgMs={WorkerAverageMs:0.000} maxMs={WorkerMaxMs:0.000} mainThreadGeneration={MainThreadChunkGenerationCount}");
        GD.Print($"  queue required={RequiredQueueCount} warm={WarmQueueCount} prefetch={PrefetchQueueCount} pregeneratedThisFrame={PregeneratedThisFrame} pool={RendererPoolCount} dataRadius={DataCacheRadiusChunks} minKeep={MinKeepRadiusChunks} recentKeep={NeverEvictRecentlyVisitedSeconds:0.0}s idlePrefetch={IdlePrefetchEnabled}");
    }

    public void ToggleChunkDebugDisplay()
    {
        ShowGrid = !ShowGrid;

        foreach (ChunkRendererV2 renderer in _chunkRenderersByCoord.Values)
        {
            renderer.SetShowGrid(ShowGrid);
        }
    }

    public void CycleOverlayMode()
    {
        int modeCount = System.Enum.GetValues(typeof(WorldV2OverlayMode)).Length;
        OverlayMode = (WorldV2OverlayMode)(((int)OverlayMode + 1) % modeCount);

        foreach (ChunkRendererV2 renderer in _chunkRenderersByCoord.Values)
        {
            renderer.SetOverlayMode(OverlayMode);
        }
    }

    public void RebuildVisibleRenderersFromCache()
    {
        long rebuildStart = WorldV2PerformanceProfiler.Instance.BeginSample();
        foreach (KeyValuePair<Vector2I, ChunkRendererV2> entry in _chunkRenderersByCoord)
        {
            if (_chunkCache.TryPeekChunkData(entry.Key, out ChunkDataV2? data) && data != null)
            {
                entry.Value.Initialize(data, TileSize, ShowGrid, OverlayMode);
            }
        }

        RefreshStreaming(force: true);
        WorldV2PerformanceProfiler.Instance.EndSample(WorldV2PerformanceProfiler.F11Rebuild, rebuildStart, CenterGlobalChunkCoord);
    }

    public void ClearAllChunkCaches()
    {
        long resetStart = WorldV2PerformanceProfiler.Instance.BeginSample();
        foreach (ChunkRendererV2 renderer in _chunkRenderersByCoord.Values)
        {
            renderer.QueueFree();
        }

        _chunkRenderersByCoord.Clear();

        foreach (ChunkRendererV2 renderer in _rendererPool)
        {
            renderer.QueueFree();
        }

        _rendererPool.Clear();
        _generationQueue.Clear();
        _warmGenerationQueue.Clear();
        _directionalPrefetchQueue.Clear();
        _idlePrefetchQueue.Clear();
        _pendingGeneration.Clear();
        _pendingWarmGeneration.Clear();
        _pendingDirectionalPrefetch.Clear();
        _pendingIdlePrefetch.Clear();
        _desiredRenderChunks.Clear();
        _desiredRenderOrder.Clear();
        _attachQueue.Clear();
        _detachQueue.Clear();
        _pendingAttach.Clear();
        _pendingDetach.Clear();
        _lastDesiredCenterChunk = new Vector2I(int.MinValue, int.MinValue);
        _lastDesiredRequiredRange = new Rect2I(Vector2I.Zero, Vector2I.Zero);
        _lastDesiredZoomBucket = int.MinValue;
        _lastDesiredViewportBucket = new Vector2I(int.MinValue, int.MinValue);
        _streamingFrameCount = 0;
        _initialRequiredChunksInitialized = false;
        _initialRequiredChunks.Clear();
        InitialLoadingLoadedChunks = 0;
        IsInitialLoadingComplete = false;
        StreamingMode = WorldStreamingModeV2.InitialLoading;
        _chunkCache.ClearAll();
        StopGenerationWorker();
        _streamingStarted = false;
        _bootstrapFrameCount = 0;
        WorldV2PerformanceProfiler.Instance.EndSample(WorldV2PerformanceProfiler.F12Reset, resetStart, CenterGlobalChunkCoord);
    }

    private void StopGenerationWorker()
    {
        _generationWorker?.Stop();
        _generationWorker = null;
    }

    public void MarkChunkDirty(Vector2I globalChunkCoord)
    {
        _chunkCache.MarkDirty(globalChunkCoord);
        if (_chunkRenderersByCoord.TryGetValue(globalChunkCoord, out ChunkRendererV2? renderer))
        {
            renderer.QueueRedraw();
        }
    }

    public Vector2I WorldToGlobalCell(Vector2 worldPosition)
    {
        Vector2I cell = new(
            Mathf.FloorToInt(worldPosition.X / TileSize),
            Mathf.FloorToInt(worldPosition.Y / TileSize));
        return _worldManager?.ClampGlobalCellToWorld(cell) ?? cell;
    }

    public Vector2 GlobalCellToWorldCenter(Vector2I globalCellCoord)
    {
        return new Vector2(
            globalCellCoord.X * TileSize + TileSize * 0.5f,
            globalCellCoord.Y * TileSize + TileSize * 0.5f);
    }

    private void ProcessGenerationQueue()
    {
        ResolveReferences();
        if (_worldManager == null || _generationWorker == null)
        {
            return;
        }

        PrioritizeGenerationQueue();
        int maxGenerations = IsInitialLoadingComplete
            ? Mathf.Max(1, MaxChunkGenerationsPerFrame)
            : Mathf.Max(1, MaxInitialChunksPerFrame);

        int submitted = 0;
        while (_generationQueue.Count > 0 && submitted < maxGenerations)
        {
            Vector2I chunkCoord = _generationQueue[0];
            _generationQueue.RemoveAt(0);

            if (_chunkCache.Contains(chunkCoord))
            {
                _pendingGeneration.Remove(chunkCoord);
                continue;
            }

            if (!_desiredRenderChunks.Contains(chunkCoord))
            {
                _pendingGeneration.Remove(chunkCoord);
                continue;
            }

            _generationWorker.Enqueue(new WorldChunkGenerationRequestV2(
                chunkCoord,
                WorldChunkGenerationRequestTypeV2.Required,
                GetChunkPriorityScore(chunkCoord, CenterGlobalChunkCoord, includeVelocityBias: true)));
            submitted++;
        }
    }

    private bool QueueChunkGeneration(Vector2I chunkCoord)
    {
        if (!IsChunkWithinWorldBounds(chunkCoord) || _chunkCache.Contains(chunkCoord) || IsQueuedForAnyGeneration(chunkCoord))
        {
            return false;
        }

        _chunkCache.MarkGenerating(chunkCoord);
        _pendingGeneration.Add(chunkCoord);
        _generationQueue.Add(chunkCoord);
        return true;
    }

    private void ProcessCompletedChunkResults()
    {
        if (_generationWorker == null)
        {
            return;
        }

        int applied = 0;
        int maxApply = Mathf.Max(1, MaxCompletedChunkApplyPerFrame);
        while (applied < maxApply && _generationWorker.TryDequeueCompleted(out WorldChunkGenerationResultV2? result) && result != null)
        {
            WorkerGeneratedThisFrame++;
            ClearPendingForChunk(result.GlobalChunkCoord);

            if (!result.Success || result.ChunkData == null)
            {
                GD.PushWarning($"WorldV2 chunk generation failed at {result.GlobalChunkCoord}: {result.Error}");
                continue;
            }

            if (!_chunkCache.Contains(result.GlobalChunkCoord))
            {
                _chunkCache.StoreChunkData(result.GlobalChunkCoord, result.ChunkData);
            }

            if (result.RequestType is WorldChunkGenerationRequestTypeV2.DirectionalPrefetch or WorldChunkGenerationRequestTypeV2.IdlePrefetch or WorldChunkGenerationRequestTypeV2.Warm)
            {
                PregeneratedThisFrame++;
            }

            if (_desiredRenderChunks.Contains(result.GlobalChunkCoord))
            {
                QueueRendererAttach(result.GlobalChunkCoord);
            }

            GeneratedChunksThisFrame++;
            CompletedAppliedThisFrame++;
            WorldV2PerformanceProfiler.Instance.IncrementGeneratedChunksThisFrame();
            applied++;
        }
    }

    private void ClearPendingForChunk(Vector2I chunkCoord)
    {
        _pendingGeneration.Remove(chunkCoord);
        _pendingWarmGeneration.Remove(chunkCoord);
        _pendingDirectionalPrefetch.Remove(chunkCoord);
        _pendingIdlePrefetch.Remove(chunkCoord);
    }

    private bool QueueWarmGeneration(Vector2I chunkCoord)
    {
        if (!IsChunkWithinWorldBounds(chunkCoord) || _chunkCache.Contains(chunkCoord) || IsQueuedForAnyGeneration(chunkCoord))
        {
            return false;
        }

        _chunkCache.MarkGenerating(chunkCoord);
        _pendingWarmGeneration.Add(chunkCoord);
        _warmGenerationQueue.Add(chunkCoord);
        return true;
    }

    private bool QueueDirectionalPrefetch(Vector2I chunkCoord)
    {
        if (!IsChunkWithinWorldBounds(chunkCoord) || _chunkCache.Contains(chunkCoord) || IsQueuedForAnyGeneration(chunkCoord))
        {
            return false;
        }

        _chunkCache.MarkGenerating(chunkCoord);
        _pendingDirectionalPrefetch.Add(chunkCoord);
        _directionalPrefetchQueue.Add(chunkCoord);
        return true;
    }

    private bool QueueIdlePrefetch(Vector2I chunkCoord)
    {
        if (!IsChunkWithinWorldBounds(chunkCoord) || _chunkCache.Contains(chunkCoord) || IsQueuedForAnyGeneration(chunkCoord))
        {
            return false;
        }

        _chunkCache.MarkGenerating(chunkCoord);
        _pendingIdlePrefetch.Add(chunkCoord);
        _idlePrefetchQueue.Add(chunkCoord);
        return true;
    }

    private bool IsQueuedForAnyGeneration(Vector2I chunkCoord)
    {
        return _pendingGeneration.Contains(chunkCoord)
            || _pendingWarmGeneration.Contains(chunkCoord)
            || _pendingDirectionalPrefetch.Contains(chunkCoord)
            || _pendingIdlePrefetch.Contains(chunkCoord);
    }

    private bool PromoteQueuedChunkToRequired(Vector2I chunkCoord)
    {
        bool removed = RemoveQueuedChunk(_warmGenerationQueue, _pendingWarmGeneration, chunkCoord)
            || RemoveQueuedChunk(_directionalPrefetchQueue, _pendingDirectionalPrefetch, chunkCoord)
            || RemoveQueuedChunk(_idlePrefetchQueue, _pendingIdlePrefetch, chunkCoord);
        if (!removed || _pendingGeneration.Contains(chunkCoord) || _chunkCache.Contains(chunkCoord))
        {
            return false;
        }

        _pendingGeneration.Add(chunkCoord);
        _generationQueue.Add(chunkCoord);
        return true;
    }

    private static bool RemoveQueuedChunk(List<Vector2I> queue, HashSet<Vector2I> pending, Vector2I chunkCoord)
    {
        if (!pending.Remove(chunkCoord))
        {
            return false;
        }

        queue.Remove(chunkCoord);
        return true;
    }

    private void ProcessPrefetchQueues()
    {
        ResolveReferences();
        if (_worldManager == null || _generationWorker == null || !IsInitialLoadingComplete)
        {
            return;
        }

        if (_attachQueue.Count > 0 || _detachQueue.Count > 0 || _generationQueue.Count > 0)
        {
            return;
        }

        int maxCount = Mathf.Max(0, MaxPregeneratedChunksPerFrame);
        int submitted = 0;

        while (submitted < maxCount)
        {
            if (!TryDequeuePrefetchRequest(out WorldChunkGenerationRequestV2 request))
            {
                break;
            }

            if (_chunkCache.Contains(request.GlobalChunkCoord))
            {
                ClearPendingForChunk(request.GlobalChunkCoord);
                continue;
            }

            _generationWorker.Enqueue(request);
            submitted++;
        }
    }

    private bool TryDequeuePrefetchRequest(out WorldChunkGenerationRequestV2 request)
    {
        Vector2I chunkCoord;
        if (TryDequeueFromQueue(_warmGenerationQueue, _pendingWarmGeneration, out chunkCoord))
        {
            request = new WorldChunkGenerationRequestV2(
                chunkCoord,
                WorldChunkGenerationRequestTypeV2.Warm,
                GetChunkPriorityScore(chunkCoord, CenterGlobalChunkCoord, includeVelocityBias: false));
            return true;
        }

        if (TryDequeueFromQueue(_directionalPrefetchQueue, _pendingDirectionalPrefetch, out chunkCoord))
        {
            request = new WorldChunkGenerationRequestV2(
                chunkCoord,
                WorldChunkGenerationRequestTypeV2.DirectionalPrefetch,
                GetChunkPriorityScore(chunkCoord, CenterGlobalChunkCoord, includeVelocityBias: true));
            return true;
        }

        if (TryDequeueFromQueue(_idlePrefetchQueue, _pendingIdlePrefetch, out chunkCoord))
        {
            request = new WorldChunkGenerationRequestV2(
                chunkCoord,
                WorldChunkGenerationRequestTypeV2.IdlePrefetch,
                GetChunkPriorityScore(chunkCoord, CenterGlobalChunkCoord, includeVelocityBias: false));
            return true;
        }

        request = default;
        return false;
    }

    private static bool TryDequeueFromQueue(List<Vector2I> queue, HashSet<Vector2I> pending, out Vector2I chunkCoord)
    {
        while (queue.Count > 0)
        {
            chunkCoord = queue[0];
            queue.RemoveAt(0);
            return true;
        }

        chunkCoord = Vector2I.Zero;
        return false;
    }

    private void QueueRendererAttach(Vector2I chunkCoord)
    {
        if (_chunkRenderersByCoord.ContainsKey(chunkCoord) || _pendingAttach.Contains(chunkCoord))
        {
            return;
        }

        _pendingAttach.Add(chunkCoord);
        _attachQueue.Add(chunkCoord);
    }

    private void ProcessAttachQueue()
    {
        int attachLimit = Mathf.Max(1, MaxRendererAttachPerFrame);
        int index = 0;
        while (index < _attachQueue.Count && AttachedThisFrame < attachLimit)
        {
            Vector2I chunkCoord = _attachQueue[index];
            _attachQueue.RemoveAt(index);
            _pendingAttach.Remove(chunkCoord);

            if (!_desiredRenderChunks.Contains(chunkCoord) || _chunkRenderersByCoord.ContainsKey(chunkCoord))
            {
                continue;
            }

            if (_chunkCache.TryGetChunkData(chunkCoord, out ChunkDataV2? chunkData) && chunkData != null)
            {
                AttachRenderer(chunkCoord, chunkData);
            }
        }
    }

    private void QueueRenderersOutsideDesiredSet()
    {
        foreach (Vector2I chunkCoord in _chunkRenderersByCoord.Keys)
        {
            if (_desiredRenderChunks.Contains(chunkCoord) || _pendingDetach.Contains(chunkCoord))
            {
                continue;
            }

            _pendingDetach.Add(chunkCoord);
            _detachQueue.Add(chunkCoord);
        }
    }

    private void ProcessDetachQueue()
    {
        int detachLimit = Mathf.Max(1, MaxRendererDetachPerFrame);
        int index = 0;
        while (index < _detachQueue.Count && DetachedThisFrame < detachLimit)
        {
            Vector2I chunkCoord = _detachQueue[index];
            _detachQueue.RemoveAt(index);
            _pendingDetach.Remove(chunkCoord);

            if (_desiredRenderChunks.Contains(chunkCoord))
            {
                continue;
            }

            DetachRenderer(chunkCoord);
        }
    }

    private void AttachRenderer(Vector2I chunkCoord, ChunkDataV2 chunkData)
    {
        if (_chunkRenderersByCoord.ContainsKey(chunkCoord) || _chunkRoot == null)
        {
            return;
        }

        long attachStart = WorldV2PerformanceProfiler.Instance.BeginSample();
        ChunkRendererV2 renderer = RentRenderer(chunkCoord);
        renderer.Initialize(chunkData, TileSize, ShowGrid, OverlayMode);
        _chunkRenderersByCoord[chunkCoord] = renderer;
        _chunkCache.MarkRendered(chunkCoord);
        AttachedThisFrame++;
        WorldV2PerformanceProfiler.Instance.IncrementRendererAttachesThisFrame();
        WorldV2PerformanceProfiler.Instance.EndSample(WorldV2PerformanceProfiler.RendererAttach, attachStart, chunkCoord);
    }

    private ChunkRendererV2 RentRenderer(Vector2I chunkCoord)
    {
        ChunkRendererV2 renderer;
        if (EnableChunkRendererPooling && _rendererPool.Count > 0)
        {
            renderer = _rendererPool.Pop();
            renderer.Visible = true;
        }
        else
        {
            renderer = new ChunkRendererV2();
            _chunkRoot?.AddChild(renderer);
        }

        renderer.Name = $"Chunk_{chunkCoord.X}_{chunkCoord.Y}";
        return renderer;
    }

    private void DetachRenderersOutsideDesiredSet()
    {
        List<Vector2I> detach = new();
        foreach (Vector2I chunkCoord in _chunkRenderersByCoord.Keys)
        {
            if (!_desiredRenderChunks.Contains(chunkCoord))
            {
                detach.Add(chunkCoord);
            }
        }

        foreach (Vector2I chunkCoord in detach)
        {
            DetachRenderer(chunkCoord);
        }
    }

    private void DetachRenderer(Vector2I chunkCoord)
    {
        long detachStart = WorldV2PerformanceProfiler.Instance.BeginSample();
        if (!_chunkRenderersByCoord.Remove(chunkCoord, out ChunkRendererV2? renderer))
        {
            return;
        }

        _chunkCache.MarkDataCached(chunkCoord);
        if (EnableChunkRendererPooling && _rendererPool.Count < Mathf.Max(0, MaxPooledChunkRenderers))
        {
            renderer.ClearChunk();
            renderer.Visible = false;
            renderer.Name = $"PooledChunk_{_rendererPool.Count}";
            _rendererPool.Push(renderer);
        }
        else
        {
            renderer.QueueFree();
        }

        WorldV2PerformanceProfiler.Instance.IncrementRendererDetachesThisFrame();
        DetachedThisFrame++;
        WorldV2PerformanceProfiler.Instance.EndSample(WorldV2PerformanceProfiler.RendererDetach, detachStart, chunkCoord);
    }

    private void EvictColdChunkData()
    {
        HashSet<Vector2I> protectedCoords = BuildProtectedCacheSet();
        LastEvictedChunkCount = _chunkCache.EvictUntilWithinLimit(protectedCoords);
    }

    private HashSet<Vector2I> BuildProtectedCacheSet()
    {
        HashSet<Vector2I> protectedCoords = new(_desiredRenderChunks);
        int radius = Mathf.Max(MinKeepRadiusChunks, DataCacheRadiusChunks);
        for (int y = CenterGlobalChunkCoord.Y - radius; y <= CenterGlobalChunkCoord.Y + radius; y++)
        {
            for (int x = CenterGlobalChunkCoord.X - radius; x <= CenterGlobalChunkCoord.X + radius; x++)
            {
                protectedCoords.Add(new Vector2I(x, y));
            }
        }

        return protectedCoords;
    }

    private void RebuildDesiredRenderSet(Rect2I requiredRange, Vector2I centerGlobalChunk, int zoomBucket, Vector2I viewportBucket)
    {
        List<Vector2I> sortedRequestedChunks = GetStableSortedChunksInRange(requiredRange, centerGlobalChunk);
        _desiredRenderChunks.Clear();
        _desiredRenderOrder.Clear();

        int renderLimit = Mathf.Max(1, MaxRenderedChunkCountHardLimit);
        LastRequiredRangeWasClamped = sortedRequestedChunks.Count > renderLimit;
        MaybePrintClampWarning(sortedRequestedChunks.Count, renderLimit);

        int take = Mathf.Min(renderLimit, sortedRequestedChunks.Count);
        for (int i = 0; i < take; i++)
        {
            Vector2I chunkCoord = sortedRequestedChunks[i];
            _desiredRenderChunks.Add(chunkCoord);
            _desiredRenderOrder.Add(chunkCoord);
        }

        DesiredRenderedChunkCount = _desiredRenderChunks.Count;
        ClampedRequiredChunkCount = DesiredRenderedChunkCount;
        VisibleChunkRange = GetBoundingRange(_desiredRenderChunks, centerGlobalChunk);
        _lastDesiredCenterChunk = centerGlobalChunk;
        _lastDesiredRequiredRange = requiredRange;
        _lastDesiredZoomBucket = zoomBucket;
        _lastDesiredViewportBucket = viewportBucket;
    }

    private void EnsureInitialRequiredChunks(Vector2I centerGlobalChunk)
    {
        if (_initialRequiredChunksInitialized)
        {
            return;
        }

        _initialRequiredChunksInitialized = true;
        Rect2I initialRange = CalculateInitialChunkRange(centerGlobalChunk);
        foreach (Vector2I chunkCoord in GetStableSortedChunksInRange(initialRange, centerGlobalChunk))
        {
            _initialRequiredChunks.Add(chunkCoord);
        }
    }

    private void UpdateInitialLoadingState()
    {
        if (!_initialRequiredChunksInitialized || IsInitialLoadingComplete)
        {
            return;
        }

        int loaded = 0;
        foreach (Vector2I chunkCoord in _initialRequiredChunks)
        {
            if (_chunkRenderersByCoord.ContainsKey(chunkCoord))
            {
                loaded++;
            }
        }

        InitialLoadingLoadedChunks = loaded;
        IsInitialLoadingComplete = InitialLoadingTargetChunks > 0 && loaded >= InitialLoadingTargetChunks;
    }

    private void QueueWarmChunks(Rect2I requiredRange, Vector2I centerGlobalChunk)
    {
        if (_warmGenerationQueue.Count >= Mathf.Max(1, MaxRequestedChunkCountPerFrame))
        {
            return;
        }

        int queued = 0;
        foreach (Vector2I chunkCoord in GetStableSortedChunksInRange(requiredRange, centerGlobalChunk))
        {
            if (_desiredRenderChunks.Contains(chunkCoord))
            {
                continue;
            }

            if (QueueWarmGeneration(chunkCoord))
            {
                queued++;
                if (queued >= Mathf.Max(1, MaxRequestedChunkCountPerFrame))
                {
                    break;
                }
            }
        }
    }

    private void QueueDirectionalPrefetchChunks(Vector2I centerGlobalChunk)
    {
        if (!DirectionalPrefetchEnabled || CameraSpeed < DirectionalPrefetchSpeedThreshold)
        {
            return;
        }

        if (_directionalPrefetchQueue.Count >= Mathf.Max(1, MaxRequestedChunkCountPerFrame))
        {
            return;
        }

        Vector2 velocity = CameraVelocity;
        if (velocity.LengthSquared() <= 1.0f)
        {
            return;
        }

        Vector2 direction = velocity.Normalized();
        Vector2 perpendicular = new(-direction.Y, direction.X);
        List<Vector2I> candidates = new();
        int distance = Mathf.Max(1, DirectionalPrefetchDistanceChunks);
        int width = Mathf.Max(0, DirectionalPrefetchWidthChunks);

        for (int forward = 1; forward <= distance; forward++)
        {
            for (int side = -width; side <= width; side++)
            {
                Vector2 offset = direction * forward + perpendicular * side;
                Vector2I chunkCoord = centerGlobalChunk + new Vector2I(
                    Mathf.RoundToInt(offset.X),
                    Mathf.RoundToInt(offset.Y));
                if (chunkCoord != centerGlobalChunk)
                {
                    candidates.Add(chunkCoord);
                }
            }
        }

        candidates.Sort((a, b) => CompareChunkPriority(a, b, centerGlobalChunk, includeVelocityBias: true));
        int queued = 0;
        foreach (Vector2I chunkCoord in candidates)
        {
            if (QueueDirectionalPrefetch(chunkCoord))
            {
                queued++;
                if (queued >= Mathf.Max(1, MaxRequestedChunkCountPerFrame))
                {
                    break;
                }
            }
        }
    }

    private void QueueIdlePrefetchChunks(Vector2I centerGlobalChunk)
    {
        if (!IdlePrefetchEnabled || CameraSpeed > IdlePrefetchSpeedThreshold)
        {
            return;
        }

        if (_idlePrefetchQueue.Count >= Mathf.Max(1, MaxRequestedChunkCountPerFrame))
        {
            return;
        }

        int radius = Mathf.Max(RenderRadiusChunks + 3, IdlePrefetchRadiusChunks);
        Rect2I idleRange = new(centerGlobalChunk - new Vector2I(radius, radius), new Vector2I(radius * 2 + 1, radius * 2 + 1));
        int queued = 0;
        foreach (Vector2I chunkCoord in GetStableSortedChunksInRange(idleRange, centerGlobalChunk))
        {
            if (_desiredRenderChunks.Contains(chunkCoord))
            {
                continue;
            }

            if (QueueIdlePrefetch(chunkCoord))
            {
                queued++;
                if (queued >= Mathf.Max(1, MaxRequestedChunkCountPerFrame))
                {
                    break;
                }
            }
        }
    }

    private void UpdateStreamingMode()
    {
        if (!IsInitialLoadingComplete)
        {
            StreamingMode = WorldStreamingModeV2.InitialLoading;
            return;
        }

        if (_generationQueue.Count > 0 || _attachQueue.Count > 0 || _detachQueue.Count > 0)
        {
            StreamingMode = WorldStreamingModeV2.Streaming;
            return;
        }

        StreamingMode = _warmGenerationQueue.Count > 0 || _directionalPrefetchQueue.Count > 0 || _idlePrefetchQueue.Count > 0 || PregeneratedThisFrame > 0
            ? WorldStreamingModeV2.Prefetching
            : WorldStreamingModeV2.Active;
    }

    private void MaybePrintClampWarning(int requiredCount, int renderLimit)
    {
        bool clampChanged = LastRequiredRangeWasClamped != _lastClampLoggedState;
        double now = Time.GetTicksMsec() / 1000.0;
        bool rateLimitPassed = now - _lastClampLogSeconds >= 1.0;

        if (LastRequiredRangeWasClamped && (clampChanged || rateLimitPassed))
        {
            _lastClampLogSeconds = now;
            GD.Print($"WorldV2 render chunk range clamped: required={requiredCount}, limit={renderLimit}");
        }

        _lastClampLoggedState = LastRequiredRangeWasClamped;
    }

    private bool IsInInitialStreamingWarmup()
    {
        return _streamingFrameCount < Mathf.Max(0, InitialStreamingWarmupFrames);
    }

    private Rect2I CalculateInitialChunkRange(Vector2I centerGlobalChunk)
    {
        int radius = Mathf.Max(0, InitialRenderRadiusChunks);
        Vector2I min = centerGlobalChunk - new Vector2I(radius, radius);
        Vector2I max = centerGlobalChunk + new Vector2I(radius, radius);
        return new Rect2I(min, max - min + Vector2I.One);
    }

    private Rect2I CalculateRequiredChunkRange(Vector2I centerGlobalCell, Vector2I centerGlobalChunk)
    {
        Vector2 viewportSize = _camera?.GetViewportRect().Size ?? new Vector2(1280.0f, 720.0f);
        Vector2 zoom = _camera?.Zoom ?? Vector2.One;
        zoom.X = Mathf.Max(0.01f, zoom.X);
        zoom.Y = Mathf.Max(0.01f, zoom.Y);

        Vector2 visibleWorldSize = new(viewportSize.X / zoom.X, viewportSize.Y / zoom.Y);
        Vector2 centerWorld = _camera?.GlobalPosition ?? Vector2.Zero;
        Vector2 minWorld = centerWorld - visibleWorldSize * 0.5f;
        Vector2 maxWorld = centerWorld + visibleWorldSize * 0.5f;
        Vector2I minCell = WorldToGlobalCell(minWorld);
        Vector2I maxCell = WorldToGlobalCell(maxWorld);
        Vector2I minChunk = WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(minCell) - new Vector2I(PreloadMarginChunks, PreloadMarginChunks);
        Vector2I maxChunk = WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(maxCell) + new Vector2I(PreloadMarginChunks, PreloadMarginChunks);

        Vector2I fallbackMin = centerGlobalChunk - new Vector2I(RenderRadiusChunks, RenderRadiusChunks);
        Vector2I fallbackMax = centerGlobalChunk + new Vector2I(RenderRadiusChunks, RenderRadiusChunks);

        minChunk = new Vector2I(Mathf.Min(minChunk.X, fallbackMin.X), Mathf.Min(minChunk.Y, fallbackMin.Y));
        maxChunk = new Vector2I(Mathf.Max(maxChunk.X, fallbackMax.X), Mathf.Max(maxChunk.Y, fallbackMax.Y));

        return new Rect2I(minChunk, maxChunk - minChunk + Vector2I.One);
    }

    private List<Vector2I> GetSortedChunksInRange(Rect2I range, Vector2I centerGlobalChunk)
    {
        List<Vector2I> chunks = new();
        for (int y = range.Position.Y; y < range.End.Y; y++)
        {
            for (int x = range.Position.X; x < range.End.X; x++)
            {
                Vector2I chunkCoord = new(x, y);
                if (IsChunkWithinWorldBounds(chunkCoord))
                {
                    chunks.Add(chunkCoord);
                }
            }
        }

        chunks.Sort((a, b) => CompareChunkPriority(a, b, centerGlobalChunk, includeVelocityBias: true));
        return chunks;
    }

    private List<Vector2I> GetStableSortedChunksInRange(Rect2I range, Vector2I centerGlobalChunk)
    {
        List<Vector2I> chunks = new();
        for (int y = range.Position.Y; y < range.End.Y; y++)
        {
            for (int x = range.Position.X; x < range.End.X; x++)
            {
                Vector2I chunkCoord = new(x, y);
                if (IsChunkWithinWorldBounds(chunkCoord))
                {
                    chunks.Add(chunkCoord);
                }
            }
        }

        chunks.Sort((a, b) => CompareChunkPriority(a, b, centerGlobalChunk, includeVelocityBias: false));
        return chunks;
    }

    private int CompareChunkPriority(Vector2I a, Vector2I b, Vector2I centerGlobalChunk, bool includeVelocityBias)
    {
        float priorityA = GetChunkPriority(a, centerGlobalChunk, includeVelocityBias);
        float priorityB = GetChunkPriority(b, centerGlobalChunk, includeVelocityBias);
        int priorityCompare = priorityA.CompareTo(priorityB);
        if (priorityCompare != 0)
        {
            return priorityCompare;
        }

        int yCompare = a.Y.CompareTo(b.Y);
        return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
    }

    private float GetChunkPriority(Vector2I chunkCoord, Vector2I centerGlobalChunk)
    {
        return GetChunkPriority(chunkCoord, centerGlobalChunk, includeVelocityBias: true);
    }

    private float GetChunkPriority(Vector2I chunkCoord, Vector2I centerGlobalChunk, bool includeVelocityBias)
    {
        Vector2 delta = new(chunkCoord.X - centerGlobalChunk.X, chunkCoord.Y - centerGlobalChunk.Y);
        float score = delta.LengthSquared();
        Vector2 velocity = CameraVelocity;
        if (includeVelocityBias && velocity.LengthSquared() > 1.0f && delta.LengthSquared() > 0.01f)
        {
            Vector2 direction = velocity.Normalized();
            Vector2 chunkDirection = delta.Normalized();
            float ahead = direction.Dot(chunkDirection);
            score -= Mathf.Max(0.0f, ahead) * 5.0f;
        }

        return score;
    }

    private int GetChunkPriorityScore(Vector2I chunkCoord, Vector2I centerGlobalChunk, bool includeVelocityBias)
    {
        return Mathf.RoundToInt(GetChunkPriority(chunkCoord, centerGlobalChunk, includeVelocityBias) * 1000.0f);
    }

    private int GetZoomBucket(Vector2 zoom)
    {
        float normalizedZoom = Mathf.Max(0.01f, Mathf.Min(zoom.X, zoom.Y));
        return Mathf.RoundToInt(Mathf.Log(normalizedZoom) * 8.0f);
    }

    private Vector2I GetViewportBucket()
    {
        Vector2 viewportSize = _camera?.GetViewportRect().Size ?? new Vector2(1280.0f, 720.0f);
        return new Vector2I(
            Mathf.RoundToInt(viewportSize.X / 64.0f),
            Mathf.RoundToInt(viewportSize.Y / 64.0f));
    }

    private void PrioritizeGenerationQueue()
    {
        _generationQueue.Sort((a, b) => CompareChunkPriority(a, b, CenterGlobalChunkCoord, includeVelocityBias: true));
    }

    private static Rect2I GetBoundingRange(HashSet<Vector2I> coords, Vector2I fallbackCenter)
    {
        if (coords.Count == 0)
        {
            return new Rect2I(fallbackCenter, Vector2I.One);
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (Vector2I coord in coords)
        {
            minX = Mathf.Min(minX, coord.X);
            minY = Mathf.Min(minY, coord.Y);
            maxX = Mathf.Max(maxX, coord.X);
            maxY = Mathf.Max(maxY, coord.Y);
        }

        return new Rect2I(new Vector2I(minX, minY), new Vector2I(maxX - minX + 1, maxY - minY + 1));
    }

    private void SyncCacheSettings()
    {
        _chunkCache.MaxCachedChunkDataCount = Mathf.Max(0, MaxCachedChunkDataCount);
        _chunkCache.MinKeepRadiusChunks = Mathf.Max(0, MinKeepRadiusChunks);
        _chunkCache.NeverEvictVisibleChunks = NeverEvictVisibleChunks;
        _chunkCache.NeverEvictRecentlyVisitedSeconds = Mathf.Max(0.0, NeverEvictRecentlyVisitedSeconds);
    }

    private bool IsChunkWithinWorldBounds(Vector2I chunkCoord)
    {
        return _worldManager?.IsChunkWithinWorldBounds(chunkCoord) != false;
    }

    private void ResolveReferences()
    {
        _worldManager ??= GetNodeOrNull<WorldManagerV2>(WorldManagerPath);
        _camera ??= GetNodeOrNull<Camera2D>(CameraPath);
        _chunkRoot ??= GetNodeOrNull<Node2D>(ChunkRootPath);
    }
}
