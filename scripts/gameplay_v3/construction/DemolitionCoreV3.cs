using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameplayV3.Mercenary;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using Godot;
using WorldV2;

namespace GameplayV3.Construction;

public enum StructureDemolitionStatusV3{Designated,UnderDemolition,Completed,Cancelled,Failed}

public sealed class StructureDemolitionStateV3
{
    internal StructureDemolitionStateV3(string structureId,string companyId,DateTime designatedUtc){StructureId=structureId;CompanyId=companyId;DesignatedUtc=designatedUtc.ToUniversalTime();}
    public string StructureId{get;}public string CompanyId{get;}public StructureDemolitionStatusV3 Status{get;private set;}=StructureDemolitionStatusV3.Designated;public float ProgressSeconds{get;private set;}public DateTime DesignatedUtc{get;}public int Revision{get;private set;}
    public bool TryBegin(out string reason){if(Status is StructureDemolitionStatusV3.Completed or StructureDemolitionStatusV3.Cancelled){reason="InvalidDemolitionState";return false;}Status=StructureDemolitionStatusV3.UnderDemolition;Revision++;reason=string.Empty;return true;}
    public bool TryAddProgress(float seconds,out string reason){if(Status!=StructureDemolitionStatusV3.UnderDemolition||!float.IsFinite(seconds)||seconds<0){reason="InvalidDemolitionProgress";return false;}ProgressSeconds+=seconds;Revision++;reason=string.Empty;return true;}
    public void SetDesignated(){if(Status is not (StructureDemolitionStatusV3.Completed or StructureDemolitionStatusV3.Cancelled)){Status=StructureDemolitionStatusV3.Designated;Revision++;}}
    internal void MarkCompleted(){Status=StructureDemolitionStatusV3.Completed;Revision++;}
}

public sealed class StructureDemolitionRegistryV3
{
    private readonly Dictionary<string,StructureDemolitionStateV3> _byStructure=new(StringComparer.Ordinal);
    public int Count=>_byStructure.Count;public long Revision{get;private set;}
    public bool TryDesignate(StructureStateV3 structure,StructureDefinitionV3 definition,string companyId,out StructureDemolitionStateV3? state,out bool reused,out string reason)
    {state=null;reused=false;if(structure.CompanyId!=companyId){reason="OwnershipDenied";return false;}if(!definition.CanBeDemolished){reason="DemolitionNotAllowed";return false;}if(_byStructure.TryGetValue(structure.StructureId,out state)){reused=true;reason=string.Empty;return true;}state=new(structure.StructureId,companyId,DateTime.UtcNow);_byStructure.Add(structure.StructureId,state);Revision++;reason=string.Empty;return true;}
    public bool TryGet(string structureId,out StructureDemolitionStateV3? state)=>_byStructure.TryGetValue(structureId,out state);public bool Contains(string id)=>_byStructure.ContainsKey(id);public bool IsDesignated(string id)=>_byStructure.ContainsKey(id);
    public bool TryRemoveDesignation(string id,out StructureDemolitionStateV3? state){if(!_byStructure.Remove(id,out state))return false;Revision++;return true;}public bool TryRemove(string id,out StructureDemolitionStateV3? state)=>TryRemoveDesignation(id,out state);
    public bool TryBeginDemolition(string id,out string reason){if(!_byStructure.TryGetValue(id,out var state)){reason="NotDesignated";return false;}bool ok=state.TryBegin(out reason);if(ok)Revision++;return ok;}
    public bool TryAddProgress(string id,float seconds,out string reason){if(!_byStructure.TryGetValue(id,out var state)){reason="NotDesignated";return false;}bool ok=state.TryAddProgress(seconds,out reason);if(ok)Revision++;return ok;}
    public bool TrySetDesignated(string id){if(!_byStructure.TryGetValue(id,out var state))return false;state.SetDesignated();Revision++;return true;}
    public bool TryMarkCompleted(string id){if(!_byStructure.TryGetValue(id,out var state))return false;state.MarkCompleted();Revision++;return true;}
    public IReadOnlyList<StructureDemolitionStateV3> GetByCompany(string company)=>_byStructure.Values.Where(x=>x.CompanyId==company).OrderBy(x=>x.StructureId,StringComparer.Ordinal).ToList().AsReadOnly();public IReadOnlyList<string> GetAllStructureIds()=>_byStructure.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToList().AsReadOnly();public void Clear(){_byStructure.Clear();Revision++;}
}

public sealed record StructureDemolitionReservationV3(string StructureId,string WorkRequestId,string MercenaryId,string CompanyId,DateTime CreatedUtc,long Revision);
public sealed class StructureDemolitionReservationRegistryV3
{
    private readonly Dictionary<string,StructureDemolitionReservationV3> _byStructure=new(StringComparer.Ordinal);public int Count=>_byStructure.Count;
    public bool TryReserve(StructureDemolitionReservationV3 value,out string reason){if(_byStructure.TryGetValue(value.StructureId,out var old)){if(old.WorkRequestId==value.WorkRequestId){reason=string.Empty;return true;}reason="AlreadyReserved";return false;}_byStructure.Add(value.StructureId,value);reason=string.Empty;return true;}
    public bool TryGet(string id,out StructureDemolitionReservationV3? value)=>_byStructure.TryGetValue(id,out value);public bool IsReserved(string id)=>_byStructure.ContainsKey(id);public bool IsReservedBy(string id,string work)=>_byStructure.TryGetValue(id,out var r)&&r.WorkRequestId==work;public bool TryRelease(string id,string work)=>_byStructure.TryGetValue(id,out var r)&&r.WorkRequestId==work&&_byStructure.Remove(id);
    public int ReleaseByWorkRequest(string work)=>RemoveWhere(x=>x.WorkRequestId==work);public int ReleaseByMercenary(string mercenary)=>RemoveWhere(x=>x.MercenaryId==mercenary);public int ReleaseByStructure(string structure)=>_byStructure.Remove(structure)?1:0;public void Clear()=>_byStructure.Clear();private int RemoveWhere(Func<StructureDemolitionReservationV3,bool> predicate){var ids=_byStructure.Where(x=>predicate(x.Value)).Select(x=>x.Key).ToList();foreach(var id in ids)_byStructure.Remove(id);return ids.Count;}
}

public readonly record struct DemolitionWorkCalculationV3(float DemolitionScore,float SkillMultiplier,float EffectiveSpeed,float RequiredSeconds);
public static class DemolitionWorkCalculatorV3
{
    public static DemolitionWorkCalculationV3 Calculate(MercenaryProfileV3 profile,float baseSeconds){float score=profile.WorkSkills.Construction*.55f+profile.Attributes.Strength*.30f+profile.Attributes.Endurance*.15f;float skill=.75f+score*.025f;float speed=MercenaryDerivedStatsCalculatorV3.Calculate(profile).WorkSpeedMultiplier*skill;float required=baseSeconds/speed;if(!float.IsFinite(required)||required<=0)throw new ArgumentOutOfRangeException(nameof(baseSeconds));return new(score,skill,speed,required);}
}

public sealed class DemolitionWorkPayloadV3
{
    internal DemolitionWorkPayloadV3(string structureId,IReadOnlyList<GlobalCellCoord> occupied,IReadOnlyList<StructureMaterialRequirementV3> embedded,float required,float starting,long structureRevision,long occupancyRevision,long workRevision){StructureId=structureId;OccupiedCells=new ReadOnlyCollection<GlobalCellCoord>(occupied.ToList());EmbeddedMaterials=new ReadOnlyCollection<StructureMaterialRequirementV3>(embedded.ToList());RequiredDemolitionSeconds=required;StartingProgressSeconds=starting;CurrentProgressSeconds=starting;StructureRevisionAtStart=structureRevision;OccupancyRevisionAtStart=occupancyRevision;WorkRevision=workRevision;}
    public string StructureId{get;}public GlobalCellCoord? SelectedApproachCell{get;internal set;}public IReadOnlyList<GlobalCellCoord> OccupiedCells{get;}public IReadOnlyList<StructureMaterialRequirementV3> EmbeddedMaterials{get;}public float RequiredDemolitionSeconds{get;}public float StartingProgressSeconds{get;}public float CurrentProgressSeconds{get;internal set;}public long StructureRevisionAtStart{get;}public long OccupancyRevisionAtStart{get;}public long WorkRevision{get;}public string FailureReason{get;internal set;}=string.Empty;
}

public sealed record DemolitionCompletionResultV3(bool Succeeded,string FailureReason,string StructureId,IReadOnlyList<string> SalvageStackIds,int SalvageAmount,long OccupancyRevisionBefore,long OccupancyRevisionAfter);
public static class DemolitionCompletionServiceV3
{
    public static DemolitionCompletionResultV3 TryComplete(ConstructionSessionV3 construction,ResourceSessionV3 resources,string structureId,string workRequestId,Rect2I bounds,float requiredProgressSeconds)
    {
        if(!construction.Structures.TryGet(structureId,out var structure)||structure==null)return Fail("StructureMissing");
        if(!construction.Demolitions.TryGet(structureId,out var demolition)||demolition==null||demolition.Status!=StructureDemolitionStatusV3.UnderDemolition||!float.IsFinite(requiredProgressSeconds)||requiredProgressSeconds<=0||demolition.ProgressSeconds<requiredProgressSeconds)return Fail("InvalidDemolitionState");
        if(!construction.DemolitionReservations.IsReservedBy(structureId,workRequestId))return Fail("ReservationLost");
        foreach(var cell in structure.OccupiedCells)if(!construction.Structures.TryGetStructureAtCell(cell,out var indexed)||!ReferenceEquals(indexed,structure))return Fail("StructureIndexCorrupted");
        if(structure.EmbeddedMaterials.Any(x=>x.RequiredAmount<1))return Fail("InvalidEmbeddedMaterials");
        GlobalCellCoord salvageCell=structure.AnchorCell;if(!bounds.HasPoint(salvageCell.Value))return Fail("SalvagePlacementFailed");
        List<(ResourceTypeV3 Type,int Amount,GlobalCellCoord Cell)> salvage=structure.EmbeddedMaterials.Select(x=>(x.ResourceType,x.RequiredAmount,salvageCell)).ToList();long before=construction.Structures.OccupancyRevision;
        if(!construction.Structures.TryRemove(structureId,out _))return Fail("StructureMissing");
        if(!resources.GroundStacks.TryAddBatchAtomic(salvage,out IReadOnlyList<string> stackIds,out string reason))
        {if(!construction.Structures.TryRegister(structure,construction.Blueprints,bounds,out _))return Fail("CompletionRollback");return Fail(reason.Length==0?"SalvageCommitFailed":reason);}
        construction.Demolitions.TryMarkCompleted(structureId);construction.Demolitions.TryRemove(structureId,out _);construction.DemolitionReservations.ReleaseByWorkRequest(workRequestId);int amount=salvage.Sum(x=>x.Amount);return new(true,string.Empty,structureId,stackIds,amount,before,construction.Structures.OccupancyRevision);
        DemolitionCompletionResultV3 Fail(string reason)=>new(false,reason,structureId,Array.Empty<string>(),0,construction.Structures.OccupancyRevision,construction.Structures.OccupancyRevision);
    }
}

public sealed class DemolitionDiagnosticsV3
{
    public int CompletedCount{get;internal set;}public int FailedCount{get;internal set;}public string LastFailureReason{get;internal set;}=string.Empty;public string LastDemolishedStructureId{get;internal set;}=string.Empty;public string LastWorkerId{get;internal set;}=string.Empty;public float LastDuration{get;internal set;}public int LastSalvageTotalAmount{get;internal set;}public string LastSalvageResourceTypes{get;internal set;}=string.Empty;public long LastOccupancyRevisionBefore{get;internal set;}public long LastOccupancyRevisionAfter{get;internal set;}
}
