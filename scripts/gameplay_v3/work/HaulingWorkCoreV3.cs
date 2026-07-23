using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameplayV3.Mercenary;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using Godot;
using WorldV2;

namespace GameplayV3.Work;

public sealed class GroundStackReservationV3
{
    internal GroundStackReservationV3(string stackId,string requestId,string mercenaryId,string companyId,int requested,long revision,DateTime created){ResourceStackId=stackId;WorkRequestId=requestId;MercenaryId=mercenaryId;CompanyId=companyId;RequestedAmount=requested;Revision=revision;CreatedUtc=created;}
    public string ResourceStackId{get;} public string WorkRequestId{get;} public string MercenaryId{get;} public string CompanyId{get;} public int RequestedAmount{get;} public long Revision{get;} public DateTime CreatedUtc{get;}
}

public sealed class GroundStackReservationRegistryV3
{
    private readonly ResourceAmountReservationRegistryV3 _shared;private readonly GroundResourceStackRegistryV3 _stacks;private readonly Dictionary<string,GroundStackReservationV3> _byStack=new(StringComparer.Ordinal); public GroundStackReservationRegistryV3(ResourceAmountReservationRegistryV3 shared,GroundResourceStackRegistryV3 stacks){_shared=shared;_stacks=stacks;}public int Count=>_byStack.Count;
    public bool TryReserve(GroundStackReservationV3 reservation,out string reason){if(_byStack.TryGetValue(reservation.ResourceStackId,out GroundStackReservationV3? old)){if(old.WorkRequestId==reservation.WorkRequestId){reason=string.Empty;return true;}reason="SourceAlreadyReserved";return false;}if(!_stacks.TryGet(reservation.ResourceStackId,out var stack)||stack==null){reason="InvalidSourceStack";return false;}if(!_shared.TryReserve(reservation.ResourceStackId,reservation.MercenaryId,reservation.WorkRequestId,stack.ResourceType,reservation.RequestedAmount,ResourceAmountReservationPurposeV3.HaulingPickup,out _,out reason))return false;_byStack.Add(reservation.ResourceStackId,reservation);reason=string.Empty;return true;}
    public bool TryGet(string stackId,out GroundStackReservationV3? reservation)=>_byStack.TryGetValue(stackId,out reservation); public bool IsReserved(string id)=>_byStack.ContainsKey(id); public bool IsReservedBy(string id,string request)=>_byStack.TryGetValue(id,out GroundStackReservationV3? r)&&r.WorkRequestId==request;
    public bool TryRelease(string id,string request){if(!_byStack.TryGetValue(id,out GroundStackReservationV3? r)||r.WorkRequestId!=request)return false;_byStack.Remove(id);_shared.ReleaseByWorkRequest(request);return true;}
    public bool TryReduce(string id,string request,int amount){if(!_byStack.TryGetValue(id,out var old)||old.WorkRequestId!=request||amount<1||amount>old.RequestedAmount||!_shared.TryReduceByWork(request,amount))return false;if(amount==old.RequestedAmount)_byStack.Remove(id);else _byStack[id]=new(id,request,old.MercenaryId,old.CompanyId,old.RequestedAmount-amount,old.Revision,old.CreatedUtc);return true;}
    public int ReleaseByWorkRequest(string request){List<string> ids=new();foreach(var pair in _byStack)if(pair.Value.WorkRequestId==request)ids.Add(pair.Key);foreach(string id in ids)_byStack.Remove(id);_shared.ReleaseByWorkRequest(request);return ids.Count;}
    public int ReleaseByMercenary(string mercenary){List<string> ids=new();foreach(var pair in _byStack)if(pair.Value.MercenaryId==mercenary)ids.Add(pair.Key);foreach(string id in ids){string work=_byStack[id].WorkRequestId;_byStack.Remove(id);_shared.ReleaseByWorkRequest(work);}return ids.Count;}
    public int ReleaseByGroundStack(string stackId){int count=_byStack.Remove(stackId)?1:0;_shared.ReleaseByGroundStack(stackId);return count;}
    public void Clear(){foreach(string work in _byStack.Values.Select(x=>x.WorkRequestId).ToList())_shared.ReleaseByWorkRequest(work);_byStack.Clear();}
}

public sealed class MercenaryCarryStateV3
{
    internal MercenaryCarryStateV3(string mercenary,string request,ResourceTypeV3 type,int amount,string source,long revision,DateTime updated){MercenaryId=mercenary;WorkRequestId=request;ResourceType=type;Amount=amount;SourceStackId=source;Revision=revision;UpdatedUtc=updated;}
    public string MercenaryId{get;} public string WorkRequestId{get;} public ResourceTypeV3 ResourceType{get;} public int Amount{get;} public string SourceStackId{get;} public DateTime UpdatedUtc{get;} public long Revision{get;}
}

public sealed class MercenaryCarryRegistryV3
{
    private readonly Dictionary<string,MercenaryCarryStateV3> _byMercenary=new(StringComparer.Ordinal); public int Count=>_byMercenary.Count;
    public bool CanBegin(string mercenaryId,int amount,int max,out string reason){if(string.IsNullOrWhiteSpace(mercenaryId)||amount<1||amount>max){reason="Carry amount exceeds capacity.";return false;}if(_byMercenary.ContainsKey(mercenaryId)){reason="CarryStateConflict";return false;}reason=string.Empty;return true;}
    public bool TryBeginCarry(string mercenary,string request,ResourceTypeV3 type,int amount,string source,long revision,int max,out MercenaryCarryStateV3? state,out string reason){state=null;if(!CanBegin(mercenary,amount,max,out reason))return false;state=new(mercenary,request,type,amount,source,revision,DateTime.UtcNow);_byMercenary.Add(mercenary,state);return true;}
    public bool TryGetCarry(string mercenary,out MercenaryCarryStateV3? state)=>_byMercenary.TryGetValue(mercenary,out state); public bool ContainsCarry(string mercenary)=>_byMercenary.ContainsKey(mercenary);
    public bool TryClearCarry(string mercenary,string request,out MercenaryCarryStateV3? state){state=null;return _byMercenary.TryGetValue(mercenary,out MercenaryCarryStateV3? value)&&value.WorkRequestId==request&&_byMercenary.Remove(mercenary,out state);}
    public bool TryDropCarryAtCell(string mercenary,string request,GlobalCellCoord cell,GroundResourceStackRegistryV3 stacks,out GroundResourceStackV3? stack,out string reason){stack=null;if(!_byMercenary.TryGetValue(mercenary,out MercenaryCarryStateV3? carry)||carry.WorkRequestId!=request){reason="CarryStateConflict";return false;}if(!stacks.TryAddOrMerge(carry.ResourceType,carry.Amount,cell,out stack,out _,out reason)||stack==null)return false;_byMercenary.Remove(mercenary);return true;}
    public int GetTotalAmount(ResourceTypeV3? type=null){int total=0;foreach(MercenaryCarryStateV3 state in _byMercenary.Values)if(type==null||state.ResourceType==type.Value)total+=state.Amount;return total;}
    public void Clear()=>_byMercenary.Clear();
}

public sealed class HaulingWorkSettingsV3{public float BasePickupDurationSeconds{get;init;}=0.75f;public float BaseDropDurationSeconds{get;init;}=0.75f;public int MaxDestinationCandidates{get;init;}=64;}
public readonly struct HaulingWorkCalculationV3
{public HaulingWorkCalculationV3(float score,float skill,float speed,float pickup,float drop,int capacity){HaulingScore=score;HaulingSkillMultiplier=skill;EffectiveHandlingSpeed=speed;PickupDurationSeconds=pickup;DropDurationSeconds=drop;MaxCarryUnits=capacity;}public float HaulingScore{get;}public float HaulingSkillMultiplier{get;}public float EffectiveHandlingSpeed{get;}public float PickupDurationSeconds{get;}public float DropDurationSeconds{get;}public int MaxCarryUnits{get;}}
public static class HaulingWorkCalculatorV3
{
    public static int GetMaxCarryUnits(MercenaryProfileV3 profile)=>Math.Max(1,(int)Math.Floor(MercenaryDerivedStatsCalculatorV3.Calculate(profile).CarryCapacity));
    public static HaulingWorkCalculationV3 Calculate(MercenaryProfileV3 profile,HaulingWorkSettingsV3? settings=null){settings??=new();float score=profile.WorkSkills.Hauling*0.80f+profile.Attributes.Strength*0.15f+profile.Attributes.Endurance*0.05f;float skill=0.75f+score*0.025f;float speed=Math.Max(0.25f,MercenaryDerivedStatsCalculatorV3.Calculate(profile).WorkSpeedMultiplier*skill);return new(score,skill,speed,settings.BasePickupDurationSeconds/speed,settings.BaseDropDurationSeconds/speed,GetMaxCarryUnits(profile));}
}

public static class HaulingWorkerSelectionServiceV3
{
    public static bool TrySelect(IReadOnlyList<string> selected,GlobalCellCoord source,MercenarySessionV3 session,string playerId,MercenaryCarryRegistryV3 carries,out string worker,out string reason)
    {worker=string.Empty;List<(string Id,float Score,int Capacity,float Distance,int Index)> candidates=new();for(int i=0;i<selected.Count;i++){string id=selected[i];if(!session.CanPlayerControlMercenary(playerId,id)||!session.Registry.TryGetMercenary(id,out MercenaryProfileV3? profile,out MercenaryStateV3? state)||profile==null||state==null)continue;HaulingWorkCalculationV3 c=HaulingWorkCalculatorV3.Calculate(profile);candidates.Add((id,c.HaulingScore,c.MaxCarryUnits,MercenaryMovementCostPolicyV3.Octile(state.CurrentCell.Value,source.Value),i));}candidates.Sort((a,b)=>{int c=b.Score.CompareTo(a.Score);if(c!=0)return c;c=b.Capacity.CompareTo(a.Capacity);if(c!=0)return c;c=a.Distance.CompareTo(b.Distance);if(c!=0)return c;c=a.Index.CompareTo(b.Index);return c!=0?c:string.CompareOrdinal(a.Id,b.Id);});if(candidates.Count==0){reason="InvalidMercenary";return false;}worker=candidates[0].Id;reason=string.Empty;return true;}
}

public static class StockpileDestinationSelectionServiceV3
{
    public static IReadOnlyList<(StockpileZoneStateV3 Zone,GlobalCellCoord Cell)> GetCandidates(string company,ResourceTypeV3 type,GlobalCellCoord origin,StockpileSessionV3 stockpiles,GroundResourceStackRegistryV3 stacks,IMercenaryNavigationWorldQueryV3 query,int max=64,string? replaceableWorkRequestId=null)
    {List<(StockpileZoneStateV3 Zone,GlobalCellCoord Cell,int Existing,float Distance)> result=new();foreach(StockpileZoneStateV3 zone in stockpiles.Zones.GetZonesByCompany(company)){if(!zone.Allows(type))continue;foreach(GlobalCellCoord cell in zone.Cells){if(result.Count>=max*4)break;if(stockpiles.CellReservations.TryGet(cell,out StockpileCellReservationV3? held)&&held?.WorkRequestId!=replaceableWorkRequestId)continue;if(!query.IsInsideWorld(cell.Value)||!query.IsWalkable(cell.Value))continue;IReadOnlyList<GroundResourceStackV3> at=stacks.GetStacksAtCell(cell);bool conflict=false,existing=false;foreach(GroundResourceStackV3 stack in at){if(stack.ResourceType!=type){conflict=true;break;}existing=true;}if(!conflict)result.Add((zone,cell,existing?0:1,MercenaryMovementCostPolicyV3.Octile(origin.Value,cell.Value)));}}
        result.Sort((a,b)=>{int c=a.Existing.CompareTo(b.Existing);if(c!=0)return c;c=a.Distance.CompareTo(b.Distance);if(c!=0)return c;c=a.Zone.CreatedUtc.CompareTo(b.Zone.CreatedUtc);if(c!=0)return c;c=string.CompareOrdinal(a.Zone.StockpileZoneId,b.Zone.StockpileZoneId);if(c!=0)return c;c=a.Cell.Value.Y.CompareTo(b.Cell.Value.Y);return c!=0?c:a.Cell.Value.X.CompareTo(b.Cell.Value.X);});List<(StockpileZoneStateV3,GlobalCellCoord)> trimmed=new();for(int i=0;i<result.Count&&i<max;i++)trimmed.Add((result[i].Zone,result[i].Cell));return new ReadOnlyCollection<(StockpileZoneStateV3,GlobalCellCoord)>(trimmed);}
}

public sealed class HaulingWorkPayloadV3
{
    internal HaulingWorkPayloadV3(string source,GlobalCellCoord cell,ResourceTypeV3 type,int amount,long revision){SourceStackId=source;SourceCell=cell;ResourceType=type;RequestedAmount=amount;RemainingRequestedAmount=amount;Revision=revision;}
    public string SourceStackId{get;}public GlobalCellCoord SourceCell{get;}public ResourceTypeV3 ResourceType{get;}public int RequestedAmount{get;}public int RemainingRequestedAmount{get;internal set;}public string? StockpileZoneId{get;internal set;}public GlobalCellCoord? DestinationCell{get;internal set;}public int CompletedTripCount{get;internal set;}public int TotalPickedUp{get;internal set;}public int TotalDroppedOff{get;internal set;}public long Revision{get;}
}

public enum HaulingExecutionPhaseV3{PlanningTrip,WaitingForSourcePath,MovingToSource,PickingUp,WaitingForDestinationPath,MovingToDestination,DroppingOff,Completed,Failed,Cancelled}
public sealed class HaulingWorkExecutionStateV3
{
    private readonly List<(StockpileZoneStateV3 Zone,GlobalCellCoord Cell)> _destinations;
    internal HaulingWorkExecutionStateV3(WorkRequestV3 request,HaulingWorkPayloadV3 payload,IReadOnlyList<(StockpileZoneStateV3 Zone,GlobalCellCoord Cell)> destinations,HaulingWorkCalculationV3 calculation){WorkRequestId=request.WorkRequestId;MercenaryId=request.AssignedMercenaryId;Payload=payload;_destinations=new(destinations);Calculation=calculation;Revision=request.Revision;}
    public string WorkRequestId{get;}public string MercenaryId{get;}public HaulingWorkPayloadV3 Payload{get;}public IReadOnlyList<(StockpileZoneStateV3 Zone,GlobalCellCoord Cell)> DestinationCandidates=>_destinations.AsReadOnly();public HaulingWorkCalculationV3 Calculation{get;}public HaulingExecutionPhaseV3 Phase{get;internal set;}=HaulingExecutionPhaseV3.PlanningTrip;public int DestinationCandidateIndex{get;internal set;}public int PlannedPickupAmount{get;internal set;}public float HandlingProgressSeconds{get;internal set;}public float RequiredHandlingSeconds{get;internal set;}public string MovementRequestId{get;internal set;}=string.Empty;public long Revision{get;}
}

public sealed class HaulingDiagnosticsV3
{public int CompletedCount{get;internal set;}public int FailedCount{get;internal set;}public int CancelledCount{get;internal set;}public int SupersededCount{get;internal set;}public int EmergencyDropCount{get;internal set;}public int ResourceConservationMismatchCount{get;internal set;}public int SourceStackEmptyRejectCount{get;internal set;}public string LastRequestId{get;internal set;}=string.Empty;public string LastFailureReason{get;internal set;}=string.Empty;}

public sealed partial class MercenaryWorkSessionV3
{
    private readonly Dictionary<string,HaulingWorkPayloadV3> _haulingPayloads=new(StringComparer.Ordinal);private readonly Dictionary<string,HaulingWorkExecutionStateV3> _haulingExecutionsByRequest=new(StringComparer.Ordinal);
    public GroundStackReservationRegistryV3 SourceStackReservations{get;}public MercenaryCarryRegistryV3 Carries{get;}=new();public HaulingDiagnosticsV3 HaulingDiagnostics{get;}=new();
    public int ActiveHaulingRequestCount=>_haulingExecutionsByRequest.Count;
    public bool TryGetHaulingPayload(string request,out HaulingWorkPayloadV3? payload)=>_haulingPayloads.TryGetValue(request,out payload);public bool TryGetHaulingExecution(string request,out HaulingWorkExecutionStateV3? execution)=>_haulingExecutionsByRequest.TryGetValue(request,out execution);
    public IReadOnlyList<HaulingWorkExecutionStateV3> GetActiveHaulingExecutions(){List<HaulingWorkExecutionStateV3> result=new(_haulingExecutionsByRequest.Values);result.Sort((a,b)=>string.CompareOrdinal(a.WorkRequestId,b.WorkRequestId));return result.AsReadOnly();}
    public bool TryIssueHauling(string issuer,string companyId,IReadOnlyList<string> selected,string stackId,IMercenaryNavigationWorldQueryV3 query,long currentSession,out WorkRequestV3? request,out string reason)
    {
        request=null;if(currentSession!=SessionRevision){reason="InvalidSession";return false;}if(_companies.LocalPlayer?.PlayerId!=issuer||!_companies.CanPlayerControlCompany(issuer,companyId)){reason="OwnershipDenied";return false;}if(!_resources.GroundStacks.TryGet(stackId,out GroundResourceStackV3? stack)||stack==null){reason="InvalidSourceStack";return false;}int requestedSnapshot=_resources.AmountReservations.GetAvailableAmount(stackId);if(requestedSnapshot<1){reason="SourceStackEmpty";return false;}if(Stockpiles.Zones.IsOwnedStockpileCell(companyId,stack.Cell)){reason="AlreadyStored";return false;}if(!HaulingWorkerSelectionServiceV3.TrySelect(selected,stack.Cell,_mercenaries,issuer,Carries,out string worker,out reason))return false;if(!CanStartFor(worker,WorkTypeV3.Hauling,out reason))return false;if(!_mercenaries.Registry.TryGetMercenary(worker,out MercenaryProfileV3? profile,out MercenaryStateV3? state)||profile==null||state==null||state.CompanyId!=companyId){reason="InvalidMercenary";return false;}string? replaceableRequest=_assignmentsByMercenary.TryGetValue(worker,out MercenaryWorkAssignmentV3? replaceable)?replaceable.WorkRequestId:null;if(Carries.ContainsCarry(worker)&&replaceableRequest==null){reason="CarryStateConflict";return false;}if(SourceStackReservations.TryGet(stackId,out GroundStackReservationV3? heldSource)&&heldSource?.WorkRequestId!=replaceableRequest){reason="SourceAlreadyReserved";return false;}
        IReadOnlyList<(StockpileZoneStateV3 Zone,GlobalCellCoord Cell)> destinations=StockpileDestinationSelectionServiceV3.GetCandidates(companyId,stack.ResourceType,stack.Cell,Stockpiles,_resources.GroundStacks,query,replaceableWorkRequestId:replaceableRequest);if(destinations.Count==0){reason=Stockpiles.Zones.GetZonesByCompany(companyId).Count==0?"NoStockpileZone":"NoStockpileDestination";return false;}
        NotifyExternalWorkSupersede(worker);if(_assignmentsByMercenary.TryGetValue(worker,out MercenaryWorkAssignmentV3? old))Terminal(old.WorkRequestId,WorkRequestStatusV3.Superseded,"SupersededByNewWork");long revision=++_revision;DateTime now=DateTime.UtcNow;WorkRequestV3 created=new(WorkRequestIdFactoryV3.Create(),WorkTypeV3.Hauling,WorkTargetKindV3.GroundResourceStack,companyId,stackId,stack.Cell,worker,revision,now);GroundStackReservationV3 sourceReservation=new(stackId,created.WorkRequestId,worker,companyId,requestedSnapshot,revision,now);if(!SourceStackReservations.TryReserve(sourceReservation,out reason))return false;
        (StockpileZoneStateV3 Zone,GlobalCellCoord Cell) destination=destinations[0];StockpileCellReservationV3 destinationReservation=new(destination.Zone.StockpileZoneId,destination.Cell,created.WorkRequestId,worker,stack.ResourceType,revision,now);if(!Stockpiles.CellReservations.TryReserve(destinationReservation,out reason)){SourceStackReservations.TryRelease(stackId,created.WorkRequestId);return false;}
        _control.SupersedeDirectMovementForWork(worker);MercenaryWorkAssignmentV3 assignment=new(WorkAssignmentIdFactoryV3.Create(),created,now);HaulingWorkPayloadV3 payload=new(stackId,stack.Cell,stack.ResourceType,requestedSnapshot,revision){StockpileZoneId=destination.Zone.StockpileZoneId,DestinationCell=destination.Cell};HaulingWorkExecutionStateV3 execution=new(created,payload,destinations,HaulingWorkCalculatorV3.Calculate(profile)){DestinationCandidateIndex=1};created.Status=WorkRequestStatusV3.Assigned;_requests.Add(created.WorkRequestId,created);_assignmentsByMercenary.Add(worker,assignment);_haulingPayloads.Add(created.WorkRequestId,payload);_haulingExecutionsByRequest.Add(created.WorkRequestId,execution);Diagnostics.LastWorkRequestId=created.WorkRequestId;HaulingDiagnostics.LastRequestId=created.WorkRequestId;request=created;reason=string.Empty;return true;
    }
    private void CleanupHaulingTerminal(WorkRequestV3 request,WorkRequestStatusV3 status,string reason)
    {
        if(request.WorkType!=WorkTypeV3.Hauling)return;SourceStackReservations.ReleaseByWorkRequest(request.WorkRequestId);Stockpiles.CellReservations.ReleaseByWorkRequest(request.WorkRequestId);if(Carries.TryGetCarry(request.AssignedMercenaryId,out MercenaryCarryStateV3? carry)&&carry?.WorkRequestId==request.WorkRequestId&&_mercenaries.Registry.TryGetState(request.AssignedMercenaryId,out MercenaryStateV3? state)&&state!=null){if(Carries.TryDropCarryAtCell(request.AssignedMercenaryId,request.WorkRequestId,state.CurrentCell,_resources.GroundStacks,out _,out _))HaulingDiagnostics.EmergencyDropCount++;}
        _haulingExecutionsByRequest.Remove(request.WorkRequestId);if(status==WorkRequestStatusV3.Completed)HaulingDiagnostics.CompletedCount++;else if(status==WorkRequestStatusV3.Failed)HaulingDiagnostics.FailedCount++;else if(status==WorkRequestStatusV3.Cancelled)HaulingDiagnostics.CancelledCount++;else if(status==WorkRequestStatusV3.Superseded)HaulingDiagnostics.SupersededCount++;HaulingDiagnostics.LastFailureReason=reason;
    }
    private void TrimHaulingHistory(string requestId)=>_haulingPayloads.Remove(requestId);
}
