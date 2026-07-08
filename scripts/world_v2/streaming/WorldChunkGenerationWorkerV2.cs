using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Godot;

namespace WorldV2;

public sealed class WorldChunkGenerationWorkerV2
{
    private readonly object _lock = new();
    private readonly AutoResetEvent _wakeEvent = new(false);
    private readonly List<WorldChunkGenerationRequestV2> _requests = new();
    private readonly Queue<WorldChunkGenerationResultV2> _completed = new();
    private readonly ProceduralWorldGeneratorV2 _generator = new();

    private Thread? _thread;
    private bool _stopRequested;
    private string _worldId = string.Empty;
    private int _worldSeed;
    private WorldGenerationSettingsV2? _settings;
    private WorldGenerationRequestV2? _generationRequest;
    private long _generatedCount;
    private double _totalMs;
    private double _maxMs;

    public int PendingCount
    {
        get
        {
            lock (_lock)
            {
                return _requests.Count;
            }
        }
    }

    public int CompletedCount
    {
        get
        {
            lock (_lock)
            {
                return _completed.Count;
            }
        }
    }

    public long GeneratedCount
    {
        get
        {
            lock (_lock)
            {
                return _generatedCount;
            }
        }
    }

    public double AverageMs
    {
        get
        {
            lock (_lock)
            {
                return _generatedCount > 0 ? _totalMs / _generatedCount : 0.0;
            }
        }
    }

    public double MaxMs
    {
        get
        {
            lock (_lock)
            {
                return _maxMs;
            }
        }
    }

    public void Start(string worldId, int worldSeed, WorldGenerationSettingsV2 settings, WorldGenerationRequestV2 generationRequest)
    {
        lock (_lock)
        {
            if (_thread != null)
            {
                return;
            }

            _worldId = worldId;
            _worldSeed = worldSeed;
            _settings = settings;
            _generationRequest = generationRequest;
            _stopRequested = false;
            _thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "WorldV2 Chunk Generation Worker"
            };
            _thread.Start();
        }
    }

    public void Stop()
    {
        Thread? thread;
        lock (_lock)
        {
            _stopRequested = true;
            _requests.Clear();
            thread = _thread;
        }

        _wakeEvent.Set();
        if (thread != null && thread.IsAlive)
        {
            thread.Join(1000);
        }

        lock (_lock)
        {
            _thread = null;
            _completed.Clear();
        }
    }

    public void Enqueue(WorldChunkGenerationRequestV2 request)
    {
        lock (_lock)
        {
            if (_stopRequested)
            {
                return;
            }

            _requests.Add(request);
            _requests.Sort(CompareRequests);
        }

        _wakeEvent.Set();
    }

    public bool TryDequeueCompleted(out WorldChunkGenerationResultV2? result)
    {
        lock (_lock)
        {
            if (_completed.Count > 0)
            {
                result = _completed.Dequeue();
                return true;
            }
        }

        result = null;
        return false;
    }

    private void WorkerLoop()
    {
        while (true)
        {
            WorldChunkGenerationRequestV2 request;
            bool hasRequest;
            lock (_lock)
            {
                if (_stopRequested)
                {
                    return;
                }

                if (_requests.Count == 0)
                {
                    request = default;
                    hasRequest = false;
                }
                else
                {
                    request = _requests[0];
                    _requests.RemoveAt(0);
                    hasRequest = true;
                }
            }

            if (!hasRequest)
            {
                _wakeEvent.WaitOne(25);
                continue;
            }

            Generate(request);
        }
    }

    private void Generate(WorldChunkGenerationRequestV2 request)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        WorldChunkGenerationResultV2 result;

        try
        {
            if (_settings == null)
            {
                throw new InvalidOperationException("World generation settings are not configured.");
            }

            if (_generationRequest == null)
            {
                throw new InvalidOperationException("World generation request is not configured.");
            }

            _generator.SetGenerationSettings(_settings);
            _generator.SetGenerationRequest(_generationRequest);
            ChunkDataV2 chunkData = _generator.GenerateChunkDataOnly(_worldId, _worldSeed, request.GlobalChunkCoord, _generationRequest);
            stopwatch.Stop();
            result = new WorldChunkGenerationResultV2
            {
                GlobalChunkCoord = request.GlobalChunkCoord,
                RequestType = request.RequestType,
                ChunkData = chunkData,
                ElapsedMs = stopwatch.Elapsed.TotalMilliseconds,
                Success = true
            };
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            result = new WorldChunkGenerationResultV2
            {
                GlobalChunkCoord = request.GlobalChunkCoord,
                RequestType = request.RequestType,
                ElapsedMs = stopwatch.Elapsed.TotalMilliseconds,
                Success = false,
                Error = exception.Message
            };
        }

        lock (_lock)
        {
            _completed.Enqueue(result);
            if (result.Success)
            {
                _generatedCount++;
                _totalMs += result.ElapsedMs;
                _maxMs = Math.Max(_maxMs, result.ElapsedMs);
            }
        }
    }

    private static int CompareRequests(WorldChunkGenerationRequestV2 a, WorldChunkGenerationRequestV2 b)
    {
        int typeCompare = a.RequestType.CompareTo(b.RequestType);
        if (typeCompare != 0)
        {
            return typeCompare;
        }

        int priorityCompare = a.Priority.CompareTo(b.Priority);
        if (priorityCompare != 0)
        {
            return priorityCompare;
        }

        int yCompare = a.GlobalChunkCoord.Y.CompareTo(b.GlobalChunkCoord.Y);
        return yCompare != 0 ? yCompare : a.GlobalChunkCoord.X.CompareTo(b.GlobalChunkCoord.X);
    }
}
