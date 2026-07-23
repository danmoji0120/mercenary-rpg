using System;
using System.Collections.Generic;
using GameplayV3.Jobs;
using Godot;
using WorldV2;

namespace GameplayV3.Resources.Runtime;

public sealed class GatheringJobMaterializerSettingsV3
{
    public const int MinimumBudget=128;
    public const int PerMercenaryBudget=32;
    public const int MaximumBudget=512;
    public const int MaxCandidateChecksPerStep=256;
    public const int MaxMaterializedPerStep=32;
    public const int MaxRetiredPerStep=32;
    public const float LowWaterRatio=.60f;
}

public sealed class GatheringJobMaterializerV3
{
    private readonly JobManagerV3 _jobs;
    private readonly ResourceNodeRegistryV3 _nodes;
    private readonly string _companyId;
    private readonly Func<IReadOnlyList<Vector2I>> _getMercenaryCells;
    private readonly HashSet<Vector2I> _activeChunks=new();
    private readonly HashSet<string> _materialized=new(StringComparer.Ordinal);
    private readonly HashSet<string> _candidateSet=new(StringComparer.Ordinal);
    private readonly Queue<string> _candidates=new();
    private readonly HashSet<string> _retireSet=new(StringComparer.Ordinal);
    private readonly Queue<string> _retireQueue=new();
    private readonly List<ResourceNodeRegistryChangeV3> _changes=new();
    private long _sourceRevision;

    public GatheringJobMaterializerV3(JobManagerV3 jobs,ResourceNodeRegistryV3 nodes,string companyId,Func<IReadOnlyList<Vector2I>> getMercenaryCells,IEnumerable<Vector2I>? activeChunks=null)
    {
        _jobs=jobs;_nodes=nodes;_companyId=companyId;_getMercenaryCells=getMercenaryCells;_sourceRevision=nodes.Revision;
        if(activeChunks!=null)foreach(Vector2I chunk in activeChunks)_activeChunks.Add(chunk);
        RequestRefill();
    }

    public int Budget=>Math.Clamp(_getMercenaryCells().Count*GatheringJobMaterializerSettingsV3.PerMercenaryBudget,GatheringJobMaterializerSettingsV3.MinimumBudget,GatheringJobMaterializerSettingsV3.MaximumBudget);
    public int MaterializedCount=>_materialized.Count;
    public int CandidateIndexedCount=>_candidateSet.Count;
    public bool RefillRequested{get;private set;}=true;
    public int RefillProcessedLastStep{get;private set;}

    public void OnChunkAttached(Vector2I chunk){if(_activeChunks.Add(chunk)){EnqueueChunk(chunk);RequestRefill();}}
    public void OnChunkDetached(Vector2I chunk){if(_activeChunks.Remove(chunk)){_candidates.Clear();_candidateSet.Clear();RequestRefill();QueueRetireChunk(chunk);}}
    public void RequestRefill(){RefillRequested=true;EnqueueLocalChunks();EnqueueActiveChunks();}

    public int Synchronize()
    {
        RefillProcessedLastStep=0;
        ProcessRetireQueue();
        _changes.Clear();_nodes.DrainChanges(GatheringJobMaterializerSettingsV3.MaxCandidateChecksPerStep,_changes);
        foreach(ResourceNodeRegistryChangeV3 change in _changes)
        {
            if(!change.IsValid){Retire(change.ResourceNodeId,"ResourceUnavailable");continue;}
            Vector2I chunk=WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(change.Cell);
            if(_activeChunks.Contains(chunk)||IsNearLocalMercenaryChunk(chunk))Enqueue(change.ResourceNodeId);
        }
        if(_materialized.Count<(int)Math.Ceiling(Budget*GatheringJobMaterializerSettingsV3.LowWaterRatio))RequestRefill();
        if(RefillRequested&&_candidates.Count==0){EnqueueLocalChunks();EnqueueActiveChunks();}
        int checkedCount=0,created=0;
        while(RefillRequested&&checkedCount<GatheringJobMaterializerSettingsV3.MaxCandidateChecksPerStep&&created<GatheringJobMaterializerSettingsV3.MaxMaterializedPerStep&&_materialized.Count<Budget&&_candidates.Count>0)
        {
            string id=_candidates.Dequeue();_candidateSet.Remove(id);checkedCount++;
            if(_materialized.Contains(id)||!_nodes.TryGet(id,out ResourceNodeStateV3? node)||node==null||node.IsDepleted){_jobs.Diagnostics.InvalidGatheringCandidateCount++;continue;}
            Vector2I chunk=WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(node.Cell.Value);
            if(!_activeChunks.Contains(chunk)&&!IsNearLocalMercenaryChunk(chunk)){_jobs.Diagnostics.InvalidGatheringCandidateCount++;continue;}
            if(Upsert(node)){created++;_jobs.Diagnostics.GatheringJobMaterializedTotal++;}
        }
        if(_materialized.Count>=Budget||_candidates.Count==0)RefillRequested=false;
        RefillProcessedLastStep=checkedCount;
        UpdateDiagnostics();
        return created;
    }

    private bool Upsert(ResourceNodeStateV3 node)
    {
        JobSourceKeyV3 key=Key(node.ResourceNodeId);
        long revision=Math.Max(++_sourceRevision,_nodes.Revision);if(!_jobs.TryUpsertSource(key,node.Cell.Value,revision,out _,out bool created,out _))return false;
        _materialized.Add(node.ResourceNodeId);return created;
    }
    public int CountMaterializedInChunk(Vector2I chunk){int count=0;foreach(string id in _materialized)if(_nodes.TryGet(id,out ResourceNodeStateV3? node)&&node!=null&&WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(node.Cell.Value)==chunk)count++;return count;}
    private void QueueRetireChunk(Vector2I chunk)
    {
        foreach(string id in _materialized)if(_nodes.TryGet(id,out ResourceNodeStateV3? node)&&node!=null&&WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(node.Cell.Value)==chunk&&!IsNearLocalMercenaryChunk(chunk)&&_retireSet.Add(id))_retireQueue.Enqueue(id);
    }
    private void ProcessRetireQueue(){int checks=Math.Min(GatheringJobMaterializerSettingsV3.MaxRetiredPerStep,_retireQueue.Count);while(checks-->0){string id=_retireQueue.Dequeue();_retireSet.Remove(id);if(Retire(id,"ResourceChunkDetached"))continue;if(_materialized.Contains(id)&&_retireSet.Add(id))_retireQueue.Enqueue(id);}}
    private bool Retire(string id,string reason)
    {
        if(!_materialized.Contains(id))return false;JobSourceKeyV3 key=Key(id);
        if(_jobs.TryGetBySource(key,out JobRecordV3? job)&&job!=null&&job.State!=JobStateV3.Queued)return false;
        if(!_jobs.InvalidateSource(key,reason))return false;
        _materialized.Remove(id);_jobs.Diagnostics.GatheringJobRetiredTotal++;RequestRefill();return true;
    }
    private void EnqueueLocalChunks(){foreach(Vector2I cell in _getMercenaryCells()){Vector2I center=WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(cell);for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++)EnqueueChunk(center+new Vector2I(x,y));}}
    private void EnqueueActiveChunks(){List<Vector2I> chunks=new(_activeChunks);chunks.Sort((a,b)=>a.Y!=b.Y?a.Y.CompareTo(b.Y):a.X.CompareTo(b.X));foreach(Vector2I chunk in chunks)EnqueueChunk(chunk);}
    private bool IsNearLocalMercenaryChunk(Vector2I chunk){foreach(Vector2I cell in _getMercenaryCells()){Vector2I center=WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(cell);if(Math.Max(Math.Abs(center.X-chunk.X),Math.Abs(center.Y-chunk.Y))<=2)return true;}return false;}
    private void EnqueueChunk(Vector2I chunk){foreach(string id in _nodes.GetNodeIdsInChunk(chunk))Enqueue(id);}
    private void Enqueue(string id){if(!_materialized.Contains(id)&&_candidateSet.Add(id))_candidates.Enqueue(id);}
    private JobSourceKeyV3 Key(string id)=>new(_companyId,JobTypeV3.Gathering,JobSourceKindV3.ResourceNode,id);
    private void UpdateDiagnostics(){JobManagerDiagnosticsV3 d=_jobs.Diagnostics;d.GatheringJobMaterializationBudget=Budget;d.GatheringJobMaterializedCount=_materialized.Count;d.GatheringCandidateIndexedCount=_candidateSet.Count;d.GatheringRefillRequested=RefillRequested;d.GatheringRefillProcessedLastFrame=RefillProcessedLastStep;d.GatheringFullRegistryScanCount=0;}
}
