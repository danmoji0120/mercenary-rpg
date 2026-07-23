using System;
using System.Collections.Generic;
using Godot;

namespace GameplayV3.Jobs;

public enum JobTypeV3 { Hauling, Construction, Demolition, Gathering, Sowing, Harvesting, Production }
public enum JobSourceKindV3 { GroundResourceStack, Blueprint, Structure, ResourceNode, FarmCell, FloorBlueprint, FloorDemolitionMark, ProductionFacility, GroundEquipment }
public enum JobStateV3 { Queued, Reserved, Assigned, Running, RetryWaiting, Completed, Cancelled, Invalidated, FailedTerminal }
public enum JobCommandSourceV3 { Automatic, NeedAutomatic, DirectOrder, ForceOrder }

public static class JobIdFactoryV3
{
    private const string Prefix = "job_";
    public static string Create() => Prefix + Guid.NewGuid().ToString("N");
    public static bool IsValid(string? value)
    {
        if (value == null || value.Length != Prefix.Length + 32 || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        for (int i = Prefix.Length; i < value.Length; i++)
        {
            char c = value[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        }
        return true;
    }
}

public readonly record struct JobSourceKeyV3(string CompanyId, JobTypeV3 JobType, JobSourceKindV3 SourceKind, string SourceId)
{
    public override string ToString() => $"{CompanyId}:{JobType}:{SourceKind}:{SourceId}";
}

public sealed class MercenaryWorkPriorityProfileV3
{
    private readonly byte[] _values = new byte[Enum.GetValues<JobTypeV3>().Length];

    public MercenaryWorkPriorityProfileV3() => ResetDefaults();
    public int GetPriority(JobTypeV3 type) => _values[(int)type];
    public bool IsEnabled(JobTypeV3 type) => GetPriority(type) > 0;
    public bool TrySetPriority(JobTypeV3 type, int priority, out string reason)
    {
        if (!Enum.IsDefined(type) || priority is < 0 or > 4) { reason = "Work priority must be Off(0) or 1-4."; return false; }
        _values[(int)type] = (byte)priority;
        reason = string.Empty;
        return true;
    }
    public void ResetDefaults()
    {
        _values[(int)JobTypeV3.Hauling] = 3;
        _values[(int)JobTypeV3.Construction] = 3;
        _values[(int)JobTypeV3.Demolition] = 4;
        _values[(int)JobTypeV3.Gathering] = 3;
        _values[(int)JobTypeV3.Sowing] = 3;
        _values[(int)JobTypeV3.Harvesting] = 2;
        _values[(int)JobTypeV3.Production] = 3;
    }
}

public sealed class JobRecordV3
{
    internal JobRecordV3(string id, JobSourceKeyV3 sourceKey, Vector2I targetCell, long sourceRevision, long sequence, DateTime createdUtc)
    {
        JobId = id; SourceKey = sourceKey; TargetCell = targetCell; SourceRevision = sourceRevision;
        Sequence = sequence; CreatedUtc = createdUtc.Kind == DateTimeKind.Utc ? createdUtc : createdUtc.ToUniversalTime();
        State = JobStateV3.Queued;
    }

    public string JobId { get; }
    public JobSourceKeyV3 SourceKey { get; }
    public string CompanyId => SourceKey.CompanyId;
    public JobTypeV3 JobType => SourceKey.JobType;
    public JobSourceKindV3 SourceKind => SourceKey.SourceKind;
    public string SourceId => SourceKey.SourceId;
    public Vector2I TargetCell { get; internal set; }
    public long SourceRevision { get; internal set; }
    public long Revision { get; internal set; } = 1;
    public long Sequence { get; }
    public JobStateV3 State { get; internal set; }
    public string? AssignedMercenaryId { get; internal set; }
    public string? WorkRequestId { get; internal set; }
    public JobCommandSourceV3 CommandSource { get; internal set; } = JobCommandSourceV3.Automatic;
    public int PriorityAtAssignment { get; internal set; }
    public string FailureReason { get; internal set; } = string.Empty;
    public int FailureCount { get; internal set; }
    public double RetryAtSeconds { get; internal set; }
    public DateTime CreatedUtc { get; }
}

public sealed class JobManagerSettingsV3
{
    public int SpatialBucketSize { get; init; } = 32;
    public int CandidateLimitPerType { get; init; } = 12;
    public int MaxAssignmentsPerTick { get; init; } = 8;
    public int MaxDirtySourcesPerTick { get; init; } = 64;
    public int MaxRetryChecksPerTick { get; init; } = 32;
    public int MaxBucketSearchRadius { get; init; } = 32;
    public float AssignmentIntervalSeconds { get; init; } = 0.25f;
    public int RecentTerminalCapacity { get; init; } = 128;
    public int NegativeReachabilityCacheCapacity { get; init; } = 2048;
}

public sealed class JobManagerDiagnosticsV3
{
    public long TickCount { get; internal set; }
    public long AssignmentAttempts { get; internal set; }
    public long AssignmentsSucceeded { get; internal set; }
    public long CandidateEvaluations { get; internal set; }
    public long CartesianScanCount { get; internal set; }
    public long DuplicateSourceRejectedCount { get; internal set; }
    public long DispatchFailureCount { get; internal set; }
    public long RetryScheduledCount { get; internal set; }
    public long InvalidatedCount { get; internal set; }
    public long NeedBlockedCount { get; internal set; }
    public long NegativeCacheHitCount { get; internal set; }
    public int LastTickAssignments { get; internal set; }
    public int LastTickCandidateEvaluations { get; internal set; }
    public double LastTickMilliseconds { get; internal set; }
    public int PeakQueuedJobs { get; internal set; }
    public string LastAction { get; internal set; } = "None";
    public int GatheringJobMaterializationBudget{get;internal set;}
    public int GatheringJobMaterializedCount{get;internal set;}
    public int GatheringCandidateIndexedCount{get;internal set;}
    public bool GatheringRefillRequested{get;internal set;}
    public int GatheringRefillProcessedLastFrame{get;internal set;}
    public long GatheringJobMaterializedTotal{get;internal set;}
    public long GatheringJobRetiredTotal{get;internal set;}
    public long GatheringDirectWorkBypassCount{get;internal set;}
    public long InvalidGatheringCandidateCount{get;internal set;}
    public long DuplicateGatheringJobRejectedCount{get;internal set;}
    public long GatheringFullRegistryScanCount{get;internal set;}
}

public readonly record struct JobDispatchResultV3(bool Succeeded, string WorkRequestId, string FailureReason)
{
    public static JobDispatchResultV3 Success(string workRequestId) => new(true, workRequestId, string.Empty);
    public static JobDispatchResultV3 Failure(string reason) => new(false, string.Empty, reason);
}
