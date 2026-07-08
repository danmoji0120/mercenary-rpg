using Godot;

namespace WorldV2;

public partial class WorldV2DebugHud : Control
{
    private Label? _label;
    private int _page;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Position = new Vector2(12.0f, 12.0f);
        CustomMinimumSize = new Vector2(650.0f, 300.0f);

        PanelContainer panel = new()
        {
            Name = "Panel",
            CustomMinimumSize = new Vector2(650.0f, 300.0f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(panel);

        MarginContainer margin = new()
        {
            Name = "Margin",
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        _label = new Label
        {
            Name = "Text",
            Text = "WorldV2 loading...",
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.Off,
            ClipText = true
        };
        margin.AddChild(_label);
    }

    public void Refresh(WorldManagerV2 manager, WorldV2BuildManager? buildManager, string message)
    {
        if (_label == null)
        {
            return;
        }

        SectorMetadata? metadata = manager.ActiveSectorMetadata;
        WorldGenerationSettingsV2 settings = manager.GetGenerationSettings();
        string sectorLine = metadata == null
            ? "sector: none"
            : $"sector: {metadata.SectorCoord} / {metadata.Type}";
        string worldConfigLine = $"map: {manager.MapSizePreset} {manager.WorldMapSize.WidthCells}x{manager.WorldMapSize.HeightCells} cells chunks={manager.WorldMapSize.ChunkWidth}x{manager.WorldMapSize.ChunkHeight} plan={manager.PlanVersion} generated={manager.GeneratedPlanType}";
        string worldBoundsLine = $"bounds: cells={manager.WorldBounds.Position}..{manager.WorldBounds.End - Vector2I.One}";
        string v3VillageLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 villages: count={manager.V3VillageCount} startId={manager.V3StartingVillageId} center={manager.V3StartingVillageCenter} spawn={manager.V3PlayerSpawnCell} nearest={manager.V3NearestToWorldCenterDistance:0.0}"
            : "v3 villages: inactive";
        string v3RoadLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 roads: enabled={manager.V3RoadLayerEnabled} total={manager.V3RoadCount} primary={manager.V3PrimaryRoadCount} extra={manager.V3ExtraRoadCount}"
            : "v3 roads: inactive";
        string v3ForestLine = manager.PlanVersion == WorldPlanVersionV2.V3
            ? $"v3 forests: enabled={manager.V3ForestLayerEnabled} regions={manager.V3ForestRegionCount} major={manager.V3MajorForestRegionCount} minor={manager.V3MinorForestPatchCount}"
            : "v3 forests: inactive";
        string dangerLine = metadata == null
            ? "danger/resources: -"
            : $"danger: {metadata.DangerLevel:0.00}  resources: {metadata.ResourceRichness:0.00}";
        string biomeStatsLine = metadata == null
            ? "biome stats: -"
            : $"sector: roads={metadata.RoadPathCount} villages={metadata.VillageCount} landmarks={metadata.LandmarkCount} connected={metadata.RoadConnectedLandmarkCount} quarries={metadata.QuarryCount}";
        string buildLine = buildManager == null
            ? "build: missing"
            : $"build: {buildManager.SelectedBuildType}  activeSector={buildManager.GetActiveSectorStructureCount()} total={buildManager.GetRuntimeStructureCount()}";
        SectorRuntimeState? runtimeState = manager.ActiveRuntimeState;
        string runtimeLine = runtimeState == null
            ? "runtime: none"
            : $"runtime: yes dirty={runtimeState.IsDirty} dirtySectors={manager.GetDirtyRuntimeSectorCount()} states={manager.GetRuntimeStateCount()}";
        WorldStreamManagerV2? streamManager = manager.GetStreamManager();
        string streamLine = streamManager == null
            ? "stream: missing"
            : $"cameraCell: {streamManager.CenterGlobalCellCoord}  chunk: {streamManager.CenterGlobalChunkCoord} loaded: {streamManager.LoadedChunkCount}";
        string cameraLine = streamManager == null
            ? "camera: -"
            : $"camera: pos={streamManager.CameraWorldPosition.Round()} speed={streamManager.CameraSpeed:0} zoom={streamManager.CameraZoom.X:0.00}";
        string chunkLine = streamManager == null
            ? "chunks: -"
            : $"chunks: range={streamManager.VisibleChunkRange.Position}..{streamManager.VisibleChunkRange.End - Vector2I.One} required={streamManager.RequiredChunkCount} clamped={streamManager.ClampedRequiredChunkCount} actual={streamManager.LoadedChunkCount}";
        string limitLine = streamManager == null
            ? "limits: -"
            : $"overlay={streamManager.OverlayMode} grid={streamManager.ShowGrid} margin={streamManager.PreloadMarginChunks} renderLimit={streamManager.MaxRenderedChunkCountHardLimit} clamped={streamManager.IsRenderClamped}";
        string streamingModeLine = streamManager == null
            ? "mode: -"
            : $"mode: {streamManager.StreamingMode} initial={streamManager.InitialLoadingLoadedChunks}/{streamManager.InitialLoadingTargetChunks} ({streamManager.InitialLoadingProgress:P0}) idlePrefetch={streamManager.IdlePrefetchEnabled}";
        string queueLine = streamManager == null
            ? "queues: -"
            : $"queues: required={streamManager.RequiredQueueCount} warm={streamManager.WarmQueueCount} prefetch={streamManager.PrefetchQueueCount} attach={streamManager.AttachQueueCount} detach={streamManager.DetachQueueCount} pregFrame={streamManager.PregeneratedThisFrame} cached={streamManager.CachedChunkCount}";
        string workerLine = streamManager == null
            ? "worker: -"
            : $"worker: pending={streamManager.WorkerPendingCount} completed={streamManager.WorkerCompletedCount} genFrame={streamManager.WorkerGeneratedThisFrame} applied={streamManager.CompletedAppliedThisFrame} mainGen={streamManager.MainThreadChunkGenerationCount} avg/max={streamManager.WorkerAverageMs:0.00}/{streamManager.WorkerMaxMs:0.00}ms";
        string performanceLine = "perf: -";
        string generationProfileLine = "gen profile: -";
        string renderProfileLine = "render profile: -";
        string cacheLine = "cache: -";
        string slowestChunkLine = "slowest chunk: -";
        string contextProfileLine = "context profile: -";
        string flatlandToggleLine = FlatlandWorldPlanV2.GetDebugToggleSummary();
        string generationLayerLine = WorldGenerationLayerSettingsV2.GetSummary();
        WorldV2PerformanceSummary perf = WorldV2PerformanceProfiler.Instance.GetSummary();
        if (streamManager != null)
        {
            WorldChunkCacheSummaryV2 cache = streamManager.GetChunkCacheSummary();
            WorldV2PerformanceProfiler profiler = WorldV2PerformanceProfiler.Instance;
            WorldV2MetricSummary raster = profiler.GetMetricSummary(WorldV2PerformanceProfiler.FlatlandChunkRasterBuild);
            WorldV2MetricSummary river = profiler.GetMetricSummary(WorldV2PerformanceProfiler.FlatlandRiverSample);
            WorldV2MetricSummary forest = profiler.GetMetricSummary(WorldV2PerformanceProfiler.FlatlandForestSample);
            WorldV2MetricSummary road = profiler.GetMetricSummary(WorldV2PerformanceProfiler.FlatlandRoadSample);
            WorldV2MetricSummary site = profiler.GetMetricSummary(WorldV2PerformanceProfiler.FlatlandSiteSample);
            WorldV2MetricSummary regionEnsure = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextRegionPlanEnsure);
            WorldV2MetricSummary roadEnsure = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextRoadCacheEnsure);
            WorldV2MetricSummary roadQuery = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextRoadQuery);
            WorldV2MetricSummary villageQuery = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextVillageQuery);
            WorldV2MetricSummary landmarkQuery = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextLandmarkQuery);
            WorldV2MetricSummary forestQuery = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextForestQuery);
            WorldV2MetricSummary riverQuery = profiler.GetMetricSummary(WorldV2PerformanceProfiler.ContextRiverQuery);
            performanceLine = $"perf: rendered={streamManager.RenderedChunkCount} pending={streamManager.PendingGenerationCount} genFrame={perf.GeneratedChunksThisFrame} reqFrame={streamManager.RequestedThisFrame} attachFrame={streamManager.AttachedThisFrame} detachFrame={streamManager.DetachedThisFrame} pool={streamManager.RendererPoolCount}";
            generationProfileLine = $"gen ms: last={perf.GenerateChunk.LastMs:0.00} avg={perf.GenerateChunk.AverageMs:0.00} max={perf.GenerateChunk.MaxMs:0.00} sampleAvg={perf.FlatlandSample.AverageMs:0.00} rasterAvg={raster.AverageMs:0.00}";
            renderProfileLine = $"render ms: attach={perf.RendererAttach.LastMs:0.00}/{perf.RendererAttach.AverageMs:0.00} rebuild={perf.RendererRebuild.LastMs:0.00}/{perf.RendererRebuild.AverageMs:0.00} hit={perf.CacheHit.LastMs:0.00}";
            cacheLine = $"cache: data={cache.CachedChunkDataCount}/{cache.MaxCachedChunkDataCount} cells~={cache.ApproxCachedCellCount} hit={cache.CacheHitCount} miss={cache.CacheMissCount} evict={cache.CacheEvictionCount} dirty={cache.DirtyCount}";
            cacheLine += $" | queues a/d={streamManager.AttachQueueCount}/{streamManager.DetachQueueCount} | flatland avg r/f/rd/site={river.AverageMs:0.00}/{forest.AverageMs:0.00}/{road.AverageMs:0.00}/{site.AverageMs:0.00}";
            slowestChunkLine = $"slowest chunk: {perf.SlowestChunkCoord} {perf.SlowestChunkMs:0.00}ms {perf.SlowestChunkContextInfo}";
            contextProfileLine = $"ctx avg: region={regionEnsure.AverageMs:0.00} roadEnsure={roadEnsure.AverageMs:0.00} road={roadQuery.AverageMs:0.00} village={villageQuery.AverageMs:0.00} site={landmarkQuery.AverageMs:0.00} forest={forestQuery.AverageMs:0.00} river={riverQuery.AverageMs:0.00}";
        }
        string sampleLine = "sample: -";
        string featureLine = "features: -";
        string siteLine = "site: -";
        FlatlandCellSampleV2? flatlandSample = null;

        if (streamManager != null)
        {
            flatlandSample = manager.SampleFlatlandAt(streamManager.CenterGlobalCellCoord);
            sampleLine = $"sample: tile={flatlandSample.TileType} biome={flatlandSample.Biome} forest={flatlandSample.ForestStrength:0.00} restricted={flatlandSample.IsBuildRestricted}";
            featureLine = $"features: river={flatlandSample.IsRiver} bank={flatlandSample.IsRiverBank} bridge={flatlandSample.IsBridgeCandidate} road={flatlandSample.IsRoad} ore={flatlandSample.HasOreSpot}";
            siteLine = $"site: village={flatlandSample.IsVillage} starting={flatlandSample.IsStartingVillage} landmark={flatlandSample.LandmarkKind}";
        }

        if (_page == 1)
        {
            _label.Text =
                $"WorldV2 flatland settings  page 2/2\n" +
                $"world: {manager.WorldId}  seed: {manager.WorldSeed}\n" +
                $"rivers: count={settings.RiverCount} width={settings.RiverWidth:0.0} bank={settings.RiverBankWidth:0.0} meander={settings.RiverMeanderStrength:0}\n" +
                $"forest: clusters={settings.ForestClusterCount} length={settings.ForestClusterMinLength:0}-{settings.ForestClusterMaxLength:0} width={settings.ForestClusterMinWidth:0}-{settings.ForestClusterMaxWidth:0}\n" +
                $"v3 forest regions: small={settings.V3SmallMajorForestMinCount}-{settings.V3SmallMajorForestMaxCount}/{settings.V3SmallMinorForestMinCount}-{settings.V3SmallMinorForestMaxCount} medium={settings.V3MediumMajorForestMinCount}-{settings.V3MediumMajorForestMaxCount}/{settings.V3MediumMinorForestMinCount}-{settings.V3MediumMinorForestMaxCount} large={settings.V3LargeMajorForestMinCount}-{settings.V3LargeMajorForestMaxCount}/{settings.V3LargeMinorForestMinCount}-{settings.V3LargeMinorForestMaxCount}\n" +
                $"roads: width={settings.RoadWidth:0.0} forestPenalty={settings.RoadForestPenalty:0} riverPenalty={settings.RoadRiverPenalty:0} villageAttract={settings.RoadVillageAttraction:0.00}\n" +
                $"sites: villageRadius={settings.VillageRadius:0} startRadius={settings.StartVillageRadius:0} landmarks={settings.LandmarkCountPerRegion} quarries={settings.QuarryCountPerRegion}\n" +
                $"road chances: ruin={settings.RuinRoadChance:P0} dungeon={settings.DungeonRoadChance:P0} bandit={settings.BanditRoadChance:P0} faction={settings.FactionRoadChance:P0} quarry={settings.QuarryRoadChance:P0}\n" +
                $"{biomeStatsLine}\n" +
                $"{v3VillageLine}\n" +
                $"{v3RoadLine}\n" +
                $"{v3ForestLine}\n" +
                $"{performanceLine}\n" +
                $"{generationProfileLine}\n" +
                $"{renderProfileLine}\n" +
                $"{cacheLine}\n" +
                $"{slowestChunkLine}\n" +
                $"{contextProfileLine}\n" +
                $"{flatlandToggleLine}\n" +
                $"{generationLayerLine}\n" +
                $"{streamingModeLine}\n" +
                $"{queueLine}\n" +
                $"{workerLine}\n" +
                $"{sampleLine}\n" +
                $"{featureLine}\n" +
                $"{siteLine}\n" +
                $"debug: F1 help, F2 page, F3 overlay, Ctrl+1-4 raster, Ctrl+5-8 context, Ctrl+Shift+1-9 layers, F11 rebuild renderers, F12 full reset\n" +
                $"{message}";
            return;
        }

        _label.Text =
            $"WorldV2  page 1/2\n" +
            $"world: {manager.WorldId}\n" +
            $"seed: {manager.WorldSeed}\n" +
            $"{worldConfigLine}\n" +
            $"{worldBoundsLine}\n" +
            $"{v3VillageLine}\n" +
            $"{v3RoadLine}\n" +
            $"{v3ForestLine}\n" +
            $"{sectorLine}\n" +
            $"{dangerLine}\n" +
            $"{biomeStatsLine}\n" +
            $"{cameraLine}\n" +
            $"{streamLine}\n" +
            $"{chunkLine}\n" +
            $"{limitLine}\n" +
            $"{streamingModeLine}\n" +
            $"{queueLine}\n" +
            $"{workerLine}\n" +
            $"{performanceLine}\n" +
            $"{generationProfileLine}\n" +
            $"{renderProfileLine}\n" +
            $"{cacheLine}\n" +
            $"{slowestChunkLine}\n" +
            $"{contextProfileLine}\n" +
            $"{flatlandToggleLine}\n" +
            $"{generationLayerLine}\n" +
            $"{sampleLine}\n" +
            $"{featureLine}\n" +
            $"{siteLine}\n" +
            $"{runtimeLine}\n" +
            $"{buildLine}\n" +
            $"keys: 1 floor, 2 wall, LMB place, RMB remove\n" +
            $"camera: WASD/arrows move, Shift sprint, wheel zoom\n" +
            $"debug: F1 help, F2 page, F3 overlay, F4 grid, F6 sectors, F7 stream, F8 cache, F9 plan, F10 perf, Ctrl+1-4 raster, Ctrl+5-8 context, Ctrl+Shift+1-9 layers\n" +
            $"regen: F11 renderer rebuild, F12 full reset, Home center\n" +
            $"{message}";
    }

    public void TogglePage()
    {
        _page = (_page + 1) % 2;
    }
}
