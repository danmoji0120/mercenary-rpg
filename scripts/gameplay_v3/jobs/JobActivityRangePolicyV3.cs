using System;
using System.Collections.Generic;
using GameplayV3.Bases;
using Godot;
using WorldV2;

namespace GameplayV3.Jobs;

public enum JobActivityRangeReasonV3
{
    SameBaseFacility,InsideActivityRange,UnassignedFallback,NoCompanyBaseFallback,DirectOrderOverride,NeedsOverride,
    OtherBaseFacility,OutsideActivityRange,CrossBaseHauling,BasePendingRebuild,FacilityAffiliationAmbiguous
}

public readonly record struct JobActivityRangeDecisionV3(bool Allowed,JobActivityRangeReasonV3 Reason,string? MercenaryBaseAreaId,string? JobBaseAreaId);
public sealed record JobActivityRangeEventV3(long Sequence,string MercenaryId,string CompanyId,JobTypeV3 JobType,JobSourceKindV3 SourceKind,string SourceId,GlobalCellCoord TargetCell,JobCommandSourceV3 CommandSource,bool Allowed,JobActivityRangeReasonV3 Reason,string? MercenaryBaseAreaId,string? JobBaseAreaId);

public sealed class JobActivityRangeDiagnosticsV3
{
    public long EvaluatedCount{get;internal set;}public long AllowedSameBaseCount{get;internal set;}public long AllowedRangeCount{get;internal set;}public long FallbackCount{get;internal set;}public long DirectOverrideCount{get;internal set;}public long NeedsOverrideCount{get;internal set;}public long RejectedOtherBaseCount{get;internal set;}public long RejectedOutsideRangeCount{get;internal set;}public long RejectedCrossBaseHaulingCount{get;internal set;}public long RejectedPendingCount{get;internal set;}public long RejectedAmbiguousCount{get;internal set;}public int DirtyJobSourceCount{get;internal set;}public JobActivityRangeReasonV3 LastReason{get;internal set;}=JobActivityRangeReasonV3.UnassignedFallback;public long RejectCount=>RejectedOtherBaseCount+RejectedOutsideRangeCount+RejectedCrossBaseHaulingCount+RejectedPendingCount+RejectedAmbiguousCount;public int MercenaryJobCartesianScanCount=>0;public int MercenaryBaseCartesianScanCount=>0;public int FullJobAffiliationRebuildCount=>0;
}

public sealed class JobActivityRangePolicyV3
{
    private readonly BaseAreaSessionV3 _bases;private readonly FacilityAffiliationSessionV3 _facilities;private readonly MercenaryBaseAffiliationSessionV3 _mercenaries;
    private readonly Queue<JobActivityRangeEventV3> _recent=new();private (string Company,JobTypeV3 Type,JobSourceKindV3 Kind,string Source,GlobalCellCoord Cell,string Mercenary,JobCommandSourceV3 Command) _context;private long _eventSequence;
    public JobActivityRangePolicyV3(BaseAreaSessionV3 bases,FacilityAffiliationSessionV3 facilities,MercenaryBaseAffiliationSessionV3 mercenaries){_bases=bases;_facilities=facilities;_mercenaries=mercenaries;}
    public JobActivityRangeDiagnosticsV3 Diagnostics{get;}=new();
    public event Action<JobActivityRangeEventV3>? DiagnosticEventRecorded;
    public IReadOnlyList<JobActivityRangeEventV3> GetRecentEvents()=>new List<JobActivityRangeEventV3>(_recent).AsReadOnly();

    public JobActivityRangeDecisionV3 Evaluate(JobRecordV3 job,string mercenaryId,JobCommandSourceV3 commandSource=JobCommandSourceV3.Automatic)
        =>Evaluate(job.CompanyId,job.JobType,job.SourceKind,job.SourceId,new(job.TargetCell),mercenaryId,commandSource);

    public JobActivityRangeDecisionV3 Evaluate(string companyId,JobTypeV3 jobType,JobSourceKindV3 sourceKind,string sourceId,GlobalCellCoord targetCell,string mercenaryId,JobCommandSourceV3 commandSource)
    {
        _context=(companyId,jobType,sourceKind,sourceId,targetCell,mercenaryId,commandSource);
        Diagnostics.EvaluatedCount++;
        if(commandSource is JobCommandSourceV3.DirectOrder or JobCommandSourceV3.ForceOrder)return Record(new(true,JobActivityRangeReasonV3.DirectOrderOverride,null,null));
        if(commandSource==JobCommandSourceV3.NeedAutomatic)return Record(new(true,JobActivityRangeReasonV3.NeedsOverride,null,null));
        if(!_mercenaries.TryGetMercenaryBase(mercenaryId,out var affiliation)||affiliation==null||affiliation.CompanyId!=companyId||affiliation.State==MercenaryBaseAffiliationStateV3.Unassigned)
        {bool noBases=!_bases.Areas.HasCompanyAreas(companyId);return Record(new(true,noBases?JobActivityRangeReasonV3.NoCompanyBaseFallback:JobActivityRangeReasonV3.UnassignedFallback,null,null));}
        if(affiliation.State is MercenaryBaseAffiliationStateV3.PendingReassignment or MercenaryBaseAffiliationStateV3.BaseRemoved||affiliation.BaseAreaId==null)return Record(new(false,JobActivityRangeReasonV3.BasePendingRebuild,affiliation.BaseAreaId,null));
        if(affiliation.State==MercenaryBaseAffiliationStateV3.Ambiguous)return Record(new(false,JobActivityRangeReasonV3.FacilityAffiliationAmbiguous,null,null));
        string ownBase=affiliation.BaseAreaId;
        string? facilityKey=FacilityKey(sourceKind,sourceId,targetCell.Value);
        if(facilityKey!=null&&_facilities.TryGetFacility(facilityKey,out var facility)&&facility!=null)
        {
            if(facility.AffiliationState==FacilityAffiliationStateV3.Ambiguous)return Record(new(false,JobActivityRangeReasonV3.FacilityAffiliationAmbiguous,ownBase,null));
            if(facility.AffiliationState==FacilityAffiliationStateV3.PendingBaseRebuild)return Record(new(false,JobActivityRangeReasonV3.BasePendingRebuild,ownBase,facility.BaseAreaId));
            if(facility.AffiliationState==FacilityAffiliationStateV3.Affiliated&&facility.BaseAreaId!=null)return Record(new(facility.BaseAreaId==ownBase,facility.BaseAreaId==ownBase?JobActivityRangeReasonV3.SameBaseFacility:jobType==JobTypeV3.Hauling?JobActivityRangeReasonV3.CrossBaseHauling:JobActivityRangeReasonV3.OtherBaseFacility,ownBase,facility.BaseAreaId));
        }
        if(jobType==JobTypeV3.Hauling)
        {
            if(TryFindCoreBase(companyId,targetCell,out string? sourceBase)&&sourceBase!=ownBase)return Record(new(false,JobActivityRangeReasonV3.CrossBaseHauling,ownBase,sourceBase));
            bool hasOwnDestination=_facilities.HasFacilityKindForBase(ownBase,FacilityKindV3.StockpileZone);if(!hasOwnDestination)return Record(new(false,JobActivityRangeReasonV3.CrossBaseHauling,ownBase,null));
        }
        if(_facilities.IsCellInsideActivityRange(ownBase,targetCell))return Record(new(true,JobActivityRangeReasonV3.InsideActivityRange,ownBase,ownBase));
        return Record(new(false,JobActivityRangeReasonV3.OutsideActivityRange,ownBase,null));
    }

    public int GetNeedPreferenceRank(string mercenaryId,string companyId,GlobalCellCoord cell)
    {
        if(!_mercenaries.TryGetMercenaryBase(mercenaryId,out var affiliation)||affiliation?.State!=MercenaryBaseAffiliationStateV3.Assigned||affiliation.BaseAreaId==null||affiliation.CompanyId!=companyId)return 1;
        string baseId=affiliation.BaseAreaId;if(_facilities.IsCellInsideBaseCore(baseId,cell))return 0;if(_facilities.IsCellInsideActivityRange(baseId,cell))return 1;return 2;
    }

    private bool TryFindCoreBase(string company,GlobalCellCoord cell,out string? baseId){baseId=null;foreach(BaseAreaV3 area in _facilities.GetBaseAreasWhoseActivityRangeContains(company,cell))if(area.Contains(cell.Value)){baseId=area.BaseAreaId;return true;}return false;}
    private static string? FacilityKey(JobSourceKindV3 kind,string sourceId,Vector2I cell)=>kind switch{JobSourceKindV3.Structure or JobSourceKindV3.ProductionFacility=>sourceId,JobSourceKindV3.FarmCell=>sourceId.Split(':',2)[0],JobSourceKindV3.FloorDemolitionMark=>$"basefloor:{cell.X}:{cell.Y}",_=>null};
    private JobActivityRangeDecisionV3 Record(JobActivityRangeDecisionV3 value){Diagnostics.LastReason=value.Reason;switch(value.Reason){case JobActivityRangeReasonV3.SameBaseFacility:Diagnostics.AllowedSameBaseCount++;break;case JobActivityRangeReasonV3.InsideActivityRange:Diagnostics.AllowedRangeCount++;break;case JobActivityRangeReasonV3.UnassignedFallback:case JobActivityRangeReasonV3.NoCompanyBaseFallback:Diagnostics.FallbackCount++;break;case JobActivityRangeReasonV3.DirectOrderOverride:Diagnostics.DirectOverrideCount++;break;case JobActivityRangeReasonV3.NeedsOverride:Diagnostics.NeedsOverrideCount++;break;case JobActivityRangeReasonV3.OtherBaseFacility:Diagnostics.RejectedOtherBaseCount++;break;case JobActivityRangeReasonV3.OutsideActivityRange:Diagnostics.RejectedOutsideRangeCount++;break;case JobActivityRangeReasonV3.CrossBaseHauling:Diagnostics.RejectedCrossBaseHaulingCount++;break;case JobActivityRangeReasonV3.BasePendingRebuild:Diagnostics.RejectedPendingCount++;break;case JobActivityRangeReasonV3.FacilityAffiliationAmbiguous:Diagnostics.RejectedAmbiguousCount++;break;}if(!value.Allowed||value.Reason is JobActivityRangeReasonV3.DirectOrderOverride or JobActivityRangeReasonV3.NeedsOverride){var c=_context;JobActivityRangeEventV3 evt=new(++_eventSequence,c.Mercenary,c.Company,c.Type,c.Kind,c.Source,c.Cell,c.Command,value.Allowed,value.Reason,value.MercenaryBaseAreaId,value.JobBaseAreaId);_recent.Enqueue(evt);while(_recent.Count>16)_recent.Dequeue();DiagnosticEventRecorded?.Invoke(evt);}return value;}
}
