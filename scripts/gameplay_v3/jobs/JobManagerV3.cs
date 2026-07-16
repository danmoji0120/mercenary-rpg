using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace GameplayV3.Jobs;

public sealed class JobManagerV3
{
    private readonly record struct BucketKey(string CompanyId, JobTypeV3 Type, int X, int Y);
    private readonly record struct Candidate(JobRecordV3 Job, int Distance, float Rate);
    private readonly record struct NegativeKey(string MercenaryId,string JobId,long SourceRevision);
    private readonly long _sessionRevision;
    private readonly JobManagerSettingsV3 _settings;
    private readonly Dictionary<string, JobRecordV3> _jobs = new(StringComparer.Ordinal);
    private readonly Dictionary<JobSourceKeyV3, string> _bySource = new();
    private readonly Dictionary<BucketKey, SortedSet<string>> _spatial = new();
    private readonly Dictionary<string, MercenaryWorkPriorityProfileV3> _priorities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _assignedByMercenary = new(StringComparer.Ordinal);
    private readonly Queue<string> _idleQueue = new();
    private readonly HashSet<string> _idleSet = new(StringComparer.Ordinal);
    private readonly Queue<string> _retryQueue = new();
    private readonly HashSet<string> _retrySet = new(StringComparer.Ordinal);
    private readonly Queue<string> _recentTerminal = new();
    private readonly Dictionary<NegativeKey,double> _negativeUntil=new();private readonly Queue<NegativeKey> _negativeOrder=new();
    private long _sequence;

    public JobManagerV3(long sessionRevision, JobManagerSettingsV3? settings = null)
    { _sessionRevision = sessionRevision; _settings = settings ?? new JobManagerSettingsV3(); }

    public long SessionRevision => _sessionRevision;
    public JobManagerSettingsV3 Settings => _settings;
    public JobManagerDiagnosticsV3 Diagnostics { get; } = new();
    public int Count => _jobs.Count;
    public int QueuedCount { get { int count=0; foreach (JobRecordV3 job in _jobs.Values) if (job.State == JobStateV3.Queued) count++; return count; } }
    public int ActiveAssignmentCount => _assignedByMercenary.Count;

    public MercenaryWorkPriorityProfileV3 GetOrCreatePriorityProfile(string mercenaryId)
    {
        if (!_priorities.TryGetValue(mercenaryId, out MercenaryWorkPriorityProfileV3? profile))
        { profile = new MercenaryWorkPriorityProfileV3(); _priorities.Add(mercenaryId, profile); }
        return profile;
    }

    public bool TrySetPriority(string mercenaryId, JobTypeV3 type, int priority, out string reason)
    {
        if (string.IsNullOrWhiteSpace(mercenaryId)) { reason="MercenaryId is required."; return false; }
        if (!GetOrCreatePriorityProfile(mercenaryId).TrySetPriority(type, priority, out reason)) return false;
        QueueIdleMercenary(mercenaryId);
        Diagnostics.LastAction = $"Priority {mercenaryId} {type}={priority}";
        return true;
    }

    public void ResetPriorities(string mercenaryId)
    { GetOrCreatePriorityProfile(mercenaryId).ResetDefaults(); QueueIdleMercenary(mercenaryId); Diagnostics.LastAction=$"Priority defaults {mercenaryId}"; }

    public bool TryUpsertSource(JobSourceKeyV3 key, Vector2I targetCell, long sourceRevision, out JobRecordV3? job, out bool created, out string reason)
    {
        job=null; created=false;
        if (string.IsNullOrWhiteSpace(key.CompanyId) || string.IsNullOrWhiteSpace(key.SourceId) || !Enum.IsDefined(key.JobType) || !Enum.IsDefined(key.SourceKind))
        { reason="Job source identity is invalid."; return false; }
        if (_bySource.TryGetValue(key, out string? existingId) && _jobs.TryGetValue(existingId, out job))
        {
            if (sourceRevision < job.SourceRevision) { reason="Stale source revision."; return false; }
            job.TargetCell=targetCell;
            if (sourceRevision > job.SourceRevision)
            {
                job.SourceRevision=sourceRevision; job.Revision++;
                if (job.State is JobStateV3.Invalidated or JobStateV3.Completed or JobStateV3.Cancelled or JobStateV3.FailedTerminal)
                { job.State=JobStateV3.Queued; job.FailureReason=string.Empty; AddSpatial(job); }
            }
            Diagnostics.DuplicateSourceRejectedCount++;
            reason=string.Empty;
            return true;
        }
        string id=JobIdFactoryV3.Create();
        job=new JobRecordV3(id,key,targetCell,sourceRevision,++_sequence,DateTime.UtcNow);
        _jobs.Add(id,job); _bySource.Add(key,id); AddSpatial(job); created=true;
        Diagnostics.PeakQueuedJobs=Math.Max(Diagnostics.PeakQueuedJobs,QueuedCount);
        Diagnostics.LastAction=$"Source queued {key.JobType}:{key.SourceId}";
        reason=string.Empty;
        return true;
    }

    public bool InvalidateSource(JobSourceKeyV3 key, string reason="SourceInvalidated")
    {
        if (!_bySource.TryGetValue(key,out string? id)||!_jobs.TryGetValue(id,out JobRecordV3? job)) return false;
        if (job.State is JobStateV3.Assigned or JobStateV3.Running) return false;
        RemoveSpatial(job); job.State=JobStateV3.Invalidated; job.FailureReason=reason; job.Revision++;
        Diagnostics.InvalidatedCount++; Diagnostics.LastAction=$"Invalidated {key.JobType}:{key.SourceId}"; RecordTerminal(id);
        return true;
    }

    public bool TryGet(string jobId,out JobRecordV3? job)=>_jobs.TryGetValue(jobId,out job);
    public bool TryGetBySource(JobSourceKeyV3 key,out JobRecordV3? job)
    { job=null; return _bySource.TryGetValue(key,out string? id)&&_jobs.TryGetValue(id,out job); }
    public bool TryGetAssignedJob(string mercenaryId,out JobRecordV3? job)
    { job=null; return _assignedByMercenary.TryGetValue(mercenaryId,out string? id)&&_jobs.TryGetValue(id,out job); }
    public IReadOnlyList<JobRecordV3> GetJobsSnapshot()
    { List<JobRecordV3> result=new(_jobs.Values); result.Sort((a,b)=>a.Sequence!=b.Sequence?a.Sequence.CompareTo(b.Sequence):StringComparer.Ordinal.Compare(a.JobId,b.JobId)); return result.AsReadOnly(); }

    public void QueueIdleMercenary(string mercenaryId)
    { if (!string.IsNullOrWhiteSpace(mercenaryId)&&!_assignedByMercenary.ContainsKey(mercenaryId)&&_idleSet.Add(mercenaryId)) _idleQueue.Enqueue(mercenaryId); }

    public int TickAssignments(double nowSeconds, Func<string,Vector2I?> getWorkerCell, Func<string,string> getWorkerCompanyId, Func<string,bool> canAcceptAutomaticWork,
        Func<JobRecordV3,string,bool> isCandidateValid, Func<JobRecordV3,string,float> getExecutionRate,
        Func<JobRecordV3,string,JobDispatchResultV3> dispatch)
    {
        Stopwatch watch=Stopwatch.StartNew(); Diagnostics.TickCount++; Diagnostics.LastTickAssignments=0; Diagnostics.LastTickCandidateEvaluations=0;
        AdvanceRetries(nowSeconds);
        int queueBudget=_idleQueue.Count;
        while (queueBudget-->0 && Diagnostics.LastTickAssignments<_settings.MaxAssignmentsPerTick && _idleQueue.Count>0)
        {
            string mercenaryId=_idleQueue.Dequeue(); _idleSet.Remove(mercenaryId);
            if (_assignedByMercenary.ContainsKey(mercenaryId)) continue;
            Vector2I? cell=getWorkerCell(mercenaryId);
            if (cell==null||!canAcceptAutomaticWork(mercenaryId)) { Diagnostics.NeedBlockedCount++; continue; }
            Diagnostics.AssignmentAttempts++;
            string companyId=getWorkerCompanyId(mercenaryId);
            if(string.IsNullOrWhiteSpace(companyId))continue;
            TryAssignOne(mercenaryId,companyId,cell.Value,nowSeconds,isCandidateValid,getExecutionRate,dispatch);
        }
        watch.Stop(); Diagnostics.LastTickMilliseconds=watch.Elapsed.TotalMilliseconds;
        return Diagnostics.LastTickAssignments;
    }

    private bool TryAssignOne(string mercenaryId,string companyId,Vector2I workerCell,double nowSeconds,Func<JobRecordV3,string,bool> valid,
        Func<JobRecordV3,string,float> rate,Func<JobRecordV3,string,JobDispatchResultV3> dispatch)
    {
        MercenaryWorkPriorityProfileV3 profile=GetOrCreatePriorityProfile(mercenaryId);
        for(int priority=1;priority<=4;priority++)
        {
            List<Candidate> candidates=new();
            foreach(JobTypeV3 type in Enum.GetValues<JobTypeV3>())
            {
                if(profile.GetPriority(type)!=priority)continue;
                CollectCandidates(mercenaryId,companyId,workerCell,type,nowSeconds,valid,rate,candidates);
            }
            if(candidates.Count==0)continue;
            candidates.Sort((a,b)=>CompareCandidate(a,b));
            int attempts=Math.Min(2,candidates.Count);
            for(int i=0;i<attempts;i++)
            {
                JobRecordV3 job=candidates[i].Job;
                RemoveSpatial(job); job.State=JobStateV3.Reserved; job.AssignedMercenaryId=mercenaryId; job.PriorityAtAssignment=priority; job.Revision++;
                JobDispatchResultV3 result=dispatch(job,mercenaryId);
                if(result.Succeeded)
                {
                    job.WorkRequestId=result.WorkRequestId; job.State=JobStateV3.Running; _assignedByMercenary[mercenaryId]=job.JobId;
                    Diagnostics.AssignmentsSucceeded++; Diagnostics.LastTickAssignments++; Diagnostics.LastAction=$"Assigned {job.JobType} to {mercenaryId}";
                    return true;
                }
                Diagnostics.DispatchFailureCount++; ScheduleRetry(job,nowSeconds,result.FailureReason);
                RememberNegative(new(mercenaryId,job.JobId,job.SourceRevision),job.RetryAtSeconds);
            }
            return false;
        }
        return false;
    }

    private void CollectCandidates(string mercenaryId,string companyId,Vector2I origin,JobTypeV3 type,double nowSeconds,Func<JobRecordV3,string,bool> valid,
        Func<JobRecordV3,string,float> rate,List<Candidate> output)
    {
        int before=output.Count; int bx=FloorDiv(origin.X,_settings.SpatialBucketSize); int by=FloorDiv(origin.Y,_settings.SpatialBucketSize);
        for(int ring=0;ring<=_settings.MaxBucketSearchRadius&&output.Count-before<_settings.CandidateLimitPerType;ring++)
        {
            for(int y=by-ring;y<=by+ring&&output.Count-before<_settings.CandidateLimitPerType;y++)
            for(int x=bx-ring;x<=bx+ring&&output.Count-before<_settings.CandidateLimitPerType;x++)
            {
                if(ring>0&&x>bx-ring&&x<bx+ring&&y>by-ring&&y<by+ring)continue;
                if(_spatial.TryGetValue(new BucketKey(companyId,type,x,y),out SortedSet<string>? ids))AddFromBucket(ids);
            }
        }
        void AddFromBucket(SortedSet<string> ids)
        {
            foreach(string id in ids)
            {
                if(output.Count-before>=_settings.CandidateLimitPerType)break;
                if(!_jobs.TryGetValue(id,out JobRecordV3? job)||job.State!=JobStateV3.Queued)continue;
                Diagnostics.CandidateEvaluations++; Diagnostics.LastTickCandidateEvaluations++;
                if(_negativeUntil.TryGetValue(new(mercenaryId,job.JobId,job.SourceRevision),out double until)&&until>nowSeconds){Diagnostics.NegativeCacheHitCount++;continue;}
                if(!valid(job,mercenaryId))continue;
                int distance=Math.Max(Math.Abs(origin.X-job.TargetCell.X),Math.Abs(origin.Y-job.TargetCell.Y));
                output.Add(new Candidate(job,distance,rate(job,mercenaryId)));
            }
        }
    }

    private static int CompareCandidate(Candidate a,Candidate b)
    { int c=a.Distance.CompareTo(b.Distance); if(c!=0)return c; c=b.Rate.CompareTo(a.Rate); if(c!=0)return c; c=a.Job.Sequence.CompareTo(b.Job.Sequence); return c!=0?c:StringComparer.Ordinal.Compare(a.Job.JobId,b.Job.JobId); }

    public bool MarkAssignmentTerminal(string mercenaryId,bool sourceStillValid,double nowSeconds,string reason)
    {
        if(!_assignedByMercenary.Remove(mercenaryId,out string? id)||!_jobs.TryGetValue(id,out JobRecordV3? job))return false;
        job.AssignedMercenaryId=null; job.WorkRequestId=null; job.Revision++;
        if(sourceStillValid){ScheduleRetry(job,nowSeconds,string.IsNullOrWhiteSpace(reason)?"WorkEndedBeforeSourceResolved":reason);RememberNegative(new(mercenaryId,job.JobId,job.SourceRevision),job.RetryAtSeconds);}
        else {job.State=JobStateV3.Completed;job.FailureReason=string.Empty;RecordTerminal(job.JobId);}
        QueueIdleMercenary(mercenaryId);
        return true;
    }

    public bool CancelAssignment(string mercenaryId,string reason)
    {
        if(!_assignedByMercenary.Remove(mercenaryId,out string? id)||!_jobs.TryGetValue(id,out JobRecordV3? job))return false;
        job.AssignedMercenaryId=null;job.WorkRequestId=null;job.State=JobStateV3.Cancelled;job.FailureReason=reason;job.Revision++;RecordTerminal(job.JobId);return true;
    }

    private void ScheduleRetry(JobRecordV3 job,double nowSeconds,string reason)
    {
        job.State=JobStateV3.RetryWaiting;job.FailureReason=reason;job.FailureCount++;
        job.RetryAtSeconds=nowSeconds+Math.Min(30.0,0.5*Math.Pow(2,Math.Min(job.FailureCount-1,6)));
        job.AssignedMercenaryId=null;job.WorkRequestId=null;
        if(_retrySet.Add(job.JobId))_retryQueue.Enqueue(job.JobId);
        Diagnostics.RetryScheduledCount++;Diagnostics.LastAction=$"Retry {job.JobType}: {reason}";
    }

    private void AdvanceRetries(double nowSeconds)
    {
        int checks=Math.Min(_settings.MaxRetryChecksPerTick,_retryQueue.Count);
        while(checks-->0)
        {
            string id=_retryQueue.Dequeue();_retrySet.Remove(id);
            if(!_jobs.TryGetValue(id,out JobRecordV3? job)||job.State!=JobStateV3.RetryWaiting)continue;
            if(job.RetryAtSeconds>nowSeconds){if(_retrySet.Add(id))_retryQueue.Enqueue(id);continue;}
            job.State=JobStateV3.Queued;job.Revision++;AddSpatial(job);
        }
    }

    private void AddSpatial(JobRecordV3 job)
    {
        int x=FloorDiv(job.TargetCell.X,_settings.SpatialBucketSize),y=FloorDiv(job.TargetCell.Y,_settings.SpatialBucketSize);
        var key=new BucketKey(job.CompanyId,job.JobType,x,y);
        if(!_spatial.TryGetValue(key,out SortedSet<string>? ids)){ids=new(StringComparer.Ordinal);_spatial.Add(key,ids);}ids.Add(job.JobId);
    }
    private void RemoveSpatial(JobRecordV3 job)
    {
        int x=FloorDiv(job.TargetCell.X,_settings.SpatialBucketSize),y=FloorDiv(job.TargetCell.Y,_settings.SpatialBucketSize);
        var key=new BucketKey(job.CompanyId,job.JobType,x,y);
        if(_spatial.TryGetValue(key,out SortedSet<string>? ids)){ids.Remove(job.JobId);if(ids.Count==0)_spatial.Remove(key);}
    }
    private void RecordTerminal(string id)
    {
        _recentTerminal.Enqueue(id);
        while(_recentTerminal.Count>_settings.RecentTerminalCapacity)
        {
            string expired=_recentTerminal.Dequeue();
            if(!_jobs.TryGetValue(expired,out JobRecordV3? old)||old.State is JobStateV3.Queued or JobStateV3.Reserved or JobStateV3.Assigned or JobStateV3.Running or JobStateV3.RetryWaiting)continue;
            RemoveSpatial(old);_jobs.Remove(expired);_bySource.Remove(old.SourceKey);
        }
    }
    private void RememberNegative(NegativeKey key,double until){if(!_negativeUntil.ContainsKey(key))_negativeOrder.Enqueue(key);_negativeUntil[key]=until;while(_negativeOrder.Count>_settings.NegativeReachabilityCacheCapacity){NegativeKey old=_negativeOrder.Dequeue();_negativeUntil.Remove(old);}}
    private static int FloorDiv(int value,int divisor){int q=value/divisor,r=value%divisor;return r!=0&&value<0?q-1:q;}

    public void Clear()
    { _jobs.Clear();_bySource.Clear();_spatial.Clear();_priorities.Clear();_assignedByMercenary.Clear();_idleQueue.Clear();_idleSet.Clear();_retryQueue.Clear();_retrySet.Clear();_recentTerminal.Clear();_negativeUntil.Clear();_negativeOrder.Clear(); }
}
