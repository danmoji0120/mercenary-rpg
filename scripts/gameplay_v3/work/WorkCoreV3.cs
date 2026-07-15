using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GameplayV3.Company;
using GameplayV3.Control;
using GameplayV3.Mercenary;
using GameplayV3.Movement;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using Godot;
using WorldV2;

namespace GameplayV3.Work;

public static class WorkRequestIdFactoryV3{private const string Prefix="work_";public static string Create()=>Prefix+Guid.NewGuid().ToString("N");public static bool IsValid(string? value)=>ResourceIdValidationV3.IsCanonical(value,Prefix);}
public static class WorkAssignmentIdFactoryV3{private const string Prefix="assign_";public static string Create()=>Prefix+Guid.NewGuid().ToString("N");public static bool IsValid(string? value)=>ResourceIdValidationV3.IsCanonical(value,Prefix);}
public enum WorkTypeV3{Gathering,Hauling,Construction,Demolition}
public enum WorkTargetKindV3{ResourceNode,GroundResourceStack,ConstructionBlueprint,Structure}
public enum WorkRequestStatusV3{Created,Assigned,MovingToTarget,Working,Completed,Failed,Cancelled,Superseded}
public enum WorkExecutionPhaseV3{SelectingApproach,WaitingForPath,MovingToApproach,Working,Completed,Failed,Cancelled}

public sealed class WorkRequestV3
{
    internal WorkRequestV3(string id,string company,string node,GlobalCellCoord target,string worker,long revision,DateTime created):this(id,WorkTypeV3.Gathering,WorkTargetKindV3.ResourceNode,company,node,target,worker,revision,created){}
    internal WorkRequestV3(string id,WorkTypeV3 type,WorkTargetKindV3 targetKind,string company,string targetId,GlobalCellCoord target,string worker,long revision,DateTime created){WorkRequestId=id;WorkType=type;TargetKind=targetKind;CompanyId=company;TargetId=targetId;TargetCell=target;AssignedMercenaryId=worker;Revision=revision;CreatedUtc=created;}
    public string WorkRequestId{get;}public WorkTypeV3 WorkType{get;}public WorkTargetKindV3 TargetKind{get;}public string CompanyId{get;}public string TargetId{get;}public string ResourceNodeId=>TargetKind==WorkTargetKindV3.ResourceNode?TargetId:string.Empty;public GlobalCellCoord TargetCell{get;}public string AssignedMercenaryId{get;}public WorkRequestStatusV3 Status{get;internal set;}=WorkRequestStatusV3.Created;public string FailureReason{get;internal set;}=string.Empty;public DateTime CreatedUtc{get;}public long Revision{get;}
}

public sealed class MercenaryWorkAssignmentV3
{
    internal MercenaryWorkAssignmentV3(string id,WorkRequestV3 request,DateTime created){AssignmentId=id;WorkRequestId=request.WorkRequestId;MercenaryId=request.AssignedMercenaryId;CompanyId=request.CompanyId;ResourceNodeId=request.ResourceNodeId;Revision=request.Revision;CreatedUtc=created;}
    public string AssignmentId{get;}public string WorkRequestId{get;}public string MercenaryId{get;}public string CompanyId{get;}public string ResourceNodeId{get;}public WorkRequestStatusV3 Status{get;internal set;}=WorkRequestStatusV3.Assigned;public DateTime CreatedUtc{get;}public long Revision{get;}
}

public sealed class ResourceReservationV3
{
    internal ResourceReservationV3(string node,string request,string mercenary,string company,long revision,DateTime created){ResourceNodeId=node;WorkRequestId=request;MercenaryId=mercenary;CompanyId=company;Revision=revision;CreatedUtc=created;}
    public string ResourceNodeId{get;}public string WorkRequestId{get;}public string MercenaryId{get;}public string CompanyId{get;}public long Revision{get;}public DateTime CreatedUtc{get;}
}

public sealed class ResourceReservationRegistryV3
{
    private readonly Dictionary<string,ResourceReservationV3> _byNode=new(StringComparer.Ordinal);public int Count=>_byNode.Count;
    public bool TryReserve(ResourceReservationV3 reservation,out string reason){if(_byNode.TryGetValue(reservation.ResourceNodeId,out ResourceReservationV3? old)){if(old.WorkRequestId==reservation.WorkRequestId){reason=string.Empty;return true;}reason="Resource node is already reserved.";return false;}_byNode.Add(reservation.ResourceNodeId,reservation);reason=string.Empty;return true;}
    public bool TryGetReservation(string node,out ResourceReservationV3? reservation)=>_byNode.TryGetValue(node,out reservation);public bool IsReserved(string node)=>_byNode.ContainsKey(node);public bool IsReservedBy(string node,string request)=>_byNode.TryGetValue(node,out ResourceReservationV3? value)&&value.WorkRequestId==request;
    public bool TryRelease(string node,string request){return _byNode.TryGetValue(node,out ResourceReservationV3? value)&&value.WorkRequestId==request&&_byNode.Remove(node);}
    public int ReleaseByWorkRequest(string request){List<string> ids=new();foreach(var pair in _byNode)if(pair.Value.WorkRequestId==request)ids.Add(pair.Key);foreach(string id in ids)_byNode.Remove(id);return ids.Count;}
    public int ReleaseByMercenary(string mercenary){List<string> ids=new();foreach(var pair in _byNode)if(pair.Value.MercenaryId==mercenary)ids.Add(pair.Key);foreach(string id in ids)_byNode.Remove(id);return ids.Count;}
    public void Clear()=>_byNode.Clear();
}

public sealed class MercenaryWorkExecutionStateV3
{
    private readonly IReadOnlyList<GlobalCellCoord> _candidates;
    internal MercenaryWorkExecutionStateV3(WorkRequestV3 request,IReadOnlyList<GlobalCellCoord> candidates,float required){WorkRequestId=request.WorkRequestId;MercenaryId=request.AssignedMercenaryId;ResourceNodeId=request.ResourceNodeId;Revision=request.Revision;_candidates=new ReadOnlyCollection<GlobalCellCoord>(new List<GlobalCellCoord>(candidates));RequiredWorkSeconds=required;StartedUtc=DateTime.UtcNow;}
    public string WorkRequestId{get;}public string MercenaryId{get;}public string ResourceNodeId{get;}public WorkExecutionPhaseV3 Phase{get;internal set;}=WorkExecutionPhaseV3.SelectingApproach;public IReadOnlyList<GlobalCellCoord> CandidateApproachCells=>_candidates;public int CurrentCandidateIndex{get;internal set;}public GlobalCellCoord? SelectedApproachCell{get;internal set;}public string MovementRequestId{get;internal set;}=string.Empty;public float WorkProgressSeconds{get;internal set;}public float RequiredWorkSeconds{get;}public int CompletedCycleCount{get;internal set;}public long Revision{get;}public string FailureReason{get;internal set;}=string.Empty;public DateTime StartedUtc{get;}
}

public sealed class GatheringWorkSettingsV3{public float BaseGatheringDurationSeconds{get;init;}=4f;public float MinimumEffectiveSpeed{get;init;}=0.25f;public float MaximumEffectiveSpeed{get;init;}=3f;public int MaxCyclesPerTick{get;init;}=4;}
public readonly struct GatheringWorkCalculationV3{public GatheringWorkCalculationV3(float score,float skillMultiplier,float speed,float seconds){GatheringScore=score;GatheringSkillMultiplier=skillMultiplier;EffectiveGatheringSpeed=speed;RequiredWorkSeconds=seconds;}public float GatheringScore{get;}public float GatheringSkillMultiplier{get;}public float EffectiveGatheringSpeed{get;}public float RequiredWorkSeconds{get;}}
public static class GatheringWorkCalculatorV3
{
    public static GatheringWorkCalculationV3 Calculate(MercenaryProfileV3 profile,GatheringWorkSettingsV3? settings=null){settings??=new();float score=MercenaryDerivedStatsCalculatorV3.GetWorkScore(profile,MercenaryWorkSkillTypeV3.Gathering);float skill=0.75f+score*0.025f;float work=MercenaryDerivedStatsCalculatorV3.Calculate(profile).WorkSpeedMultiplier;float speed=Mathf.Clamp(work*skill,settings.MinimumEffectiveSpeed,settings.MaximumEffectiveSpeed);return new(score,skill,speed,settings.BaseGatheringDurationSeconds/speed);}
}

public static class GatheringApproachCellServiceV3
{
    private static readonly Vector2I[] Cardinals={Vector2I.Up,Vector2I.Right,Vector2I.Down,Vector2I.Left};private static readonly Vector2I[] Diagonals={new(1,-1),new(1,1),new(-1,1),new(-1,-1)};
    public static IReadOnlyList<GlobalCellCoord> GetCandidates(GlobalCellCoord worker,GlobalCellCoord node,IMercenaryNavigationWorldQueryV3 query){List<GlobalCellCoord> result=new();Add(Cardinals);Add(Diagonals);return result.AsReadOnly();void Add(Vector2I[] offsets){List<Vector2I> cells=new();foreach(Vector2I offset in offsets){Vector2I cell=node.Value+offset;if(cell!=node.Value&&query.IsInsideWorld(cell)&&query.IsWalkable(cell))cells.Add(cell);}cells.Sort((a,b)=>{float da=MercenaryMovementCostPolicyV3.Octile(worker.Value,a),db=MercenaryMovementCostPolicyV3.Octile(worker.Value,b);int c=da.CompareTo(db);if(c!=0)return c;c=a.Y.CompareTo(b.Y);return c!=0?c:a.X.CompareTo(b.X);});foreach(Vector2I cell in cells)result.Add(new(cell));}}
}

public static class GatheringWorkerSelectionServiceV3
{
    public static bool TrySelect(IReadOnlyList<string> selected,GlobalCellCoord node,MercenarySessionV3 session,string playerId,out string worker,out string reason)
    {worker=string.Empty;List<(string Id,float Score,float Distance,int Selection)> candidates=new();for(int index=0;index<selected.Count;index++){string id=selected[index];if(!session.CanPlayerControlMercenary(playerId,id)||!session.Registry.TryGetMercenary(id,out MercenaryProfileV3? profile,out MercenaryStateV3? state)||profile==null||state==null)continue;float score=GatheringWorkCalculatorV3.Calculate(profile).GatheringScore;float distance=MercenaryMovementCostPolicyV3.Octile(state.CurrentCell.Value,node.Value);candidates.Add((id,score,distance,index));}candidates.Sort((a,b)=>{int c=b.Score.CompareTo(a.Score);if(c!=0)return c;c=a.Distance.CompareTo(b.Distance);if(c!=0)return c;c=a.Selection.CompareTo(b.Selection);return c!=0?c:string.CompareOrdinal(a.Id,b.Id);});if(candidates.Count==0){reason="No controllable selected mercenary can gather.";return false;}worker=candidates[0].Id;reason=string.Empty;return true;}
}

public sealed class MercenaryWorkDiagnosticsV3
{public int CompletedWorkCount{get;internal set;}public int FailedWorkCount{get;internal set;}public int CancelledWorkCount{get;internal set;}public int SupersededWorkCount{get;internal set;}public int CompletedCycleCount{get;internal set;}public string LastWorkRequestId{get;internal set;}=string.Empty;public string LastFailureReason{get;internal set;}=string.Empty;}

public sealed partial class MercenaryWorkSessionV3
{
    private readonly CompanySessionV3 _companies;private readonly MercenarySessionV3 _mercenaries;private readonly ResourceSessionV3 _resources;private readonly MercenaryControlSessionV3 _control;private readonly Dictionary<string,WorkRequestV3> _requests=new(StringComparer.Ordinal);private readonly Dictionary<string,MercenaryWorkAssignmentV3> _assignmentsByMercenary=new(StringComparer.Ordinal);private readonly Dictionary<string,MercenaryWorkExecutionStateV3> _executionsByRequest=new(StringComparer.Ordinal);private readonly Queue<string> _history=new();private long _revision;
    public MercenaryWorkSessionV3(long sessionRevision,CompanySessionV3 companies,MercenarySessionV3 mercenaries,ResourceSessionV3 resources,StockpileSessionV3 stockpiles,MercenaryControlSessionV3 control){SessionRevision=sessionRevision;_companies=companies;_mercenaries=mercenaries;_resources=resources;Stockpiles=stockpiles;_control=control;}
    private Action<string>? _externalWorkSupersede;public long SessionRevision{get;}public StockpileSessionV3 Stockpiles{get;}public ResourceReservationRegistryV3 Reservations{get;}=new();public MercenaryWorkDiagnosticsV3 Diagnostics{get;}=new();public int ActiveWorkRequestCount=>_executionsByRequest.Count+_haulingExecutionsByRequest.Count;public int ActiveAssignmentCount=>_assignmentsByMercenary.Count;public int ActiveReservationCount=>Reservations.Count+SourceStackReservations.Count+Stockpiles.CellReservations.Count;public void AttachExternalWorkSupersede(Action<string> callback)=>_externalWorkSupersede=callback;public void NotifyExternalWorkSupersede(string mercenaryId)=>_externalWorkSupersede?.Invoke(mercenaryId);
    public bool TryGetRequest(string id,out WorkRequestV3? request)=>_requests.TryGetValue(id,out request);public bool TryGetAssignment(string mercenary,out MercenaryWorkAssignmentV3? assignment)=>_assignmentsByMercenary.TryGetValue(mercenary,out assignment);public bool TryGetExecution(string request,out MercenaryWorkExecutionStateV3? execution)=>_executionsByRequest.TryGetValue(request,out execution);public IReadOnlyList<MercenaryWorkExecutionStateV3> GetActiveExecutions(){List<MercenaryWorkExecutionStateV3> values=new(_executionsByRequest.Values);values.Sort((a,b)=>string.CompareOrdinal(a.WorkRequestId,b.WorkRequestId));return values.AsReadOnly();}
    public bool IsCurrentExecution(string request,long revision)=>_executionsByRequest.TryGetValue(request,out MercenaryWorkExecutionStateV3? execution)&&execution.Revision==revision;
    public bool TryIssueGathering(string issuer,string companyId,IReadOnlyList<string> selected,string nodeId,IMercenaryNavigationWorldQueryV3 query,long currentSession,out WorkRequestV3? request,out string reason)
    {request=null;if(currentSession!=SessionRevision){reason="InvalidSession";return false;}if(_companies.LocalPlayer?.PlayerId!=issuer||!_companies.CanPlayerControlCompany(issuer,companyId)){reason="OwnershipDenied";return false;}if(!_resources.Nodes.TryGet(nodeId,out ResourceNodeStateV3? node)||node==null){reason="InvalidResourceNode";return false;}if(node.IsDepleted){reason="ResourceDepleted";return false;}if(Reservations.IsReserved(nodeId)){reason="ResourceAlreadyReserved";return false;}if(!GatheringWorkerSelectionServiceV3.TrySelect(selected,node.Cell,_mercenaries,issuer,out string worker,out reason))return false;if(!_mercenaries.Registry.TryGetMercenary(worker,out MercenaryProfileV3? profile,out MercenaryStateV3? state)||profile==null||state==null||state.CompanyId!=companyId){reason="InvalidMercenary";return false;}IReadOnlyList<GlobalCellCoord> approaches=GatheringApproachCellServiceV3.GetCandidates(state.CurrentCell,node.Cell,query);if(approaches.Count==0){reason="NoApproachCells";return false;}
        NotifyExternalWorkSupersede(worker);if(_assignmentsByMercenary.TryGetValue(worker,out MercenaryWorkAssignmentV3? old))Terminal(old.WorkRequestId,WorkRequestStatusV3.Superseded,"SupersededByNewWork");long revision=++_revision;DateTime now=DateTime.UtcNow;WorkRequestV3 created=new(WorkRequestIdFactoryV3.Create(),companyId,nodeId,node.Cell,worker,revision,now);ResourceReservationV3 reservation=new(nodeId,created.WorkRequestId,worker,companyId,revision,now);if(!Reservations.TryReserve(reservation,out reason))return false;_control.SupersedeDirectMovementForWork(worker);MercenaryWorkAssignmentV3 assignment=new(WorkAssignmentIdFactoryV3.Create(),created,now);GatheringWorkCalculationV3 calculation=GatheringWorkCalculatorV3.Calculate(profile);MercenaryWorkExecutionStateV3 execution=new(created,approaches,calculation.RequiredWorkSeconds);created.Status=WorkRequestStatusV3.Assigned;_requests.Add(created.WorkRequestId,created);_assignmentsByMercenary.Add(worker,assignment);_executionsByRequest.Add(created.WorkRequestId,execution);Diagnostics.LastWorkRequestId=created.WorkRequestId;request=created;reason=string.Empty;return true;}
    public bool CancelForDirectMove(string mercenaryId){if(!_assignmentsByMercenary.TryGetValue(mercenaryId,out MercenaryWorkAssignmentV3? assignment))return false;return Terminal(assignment.WorkRequestId,WorkRequestStatusV3.Cancelled,"CancelledByDirectMove");}
    public bool Complete(string request)=>Terminal(request,WorkRequestStatusV3.Completed,string.Empty);public bool Fail(string request,string reason)=>Terminal(request,WorkRequestStatusV3.Failed,reason);
    private bool Terminal(string requestId,WorkRequestStatusV3 status,string reason)
    {if(!_requests.TryGetValue(requestId,out WorkRequestV3? request))return false;request.Status=status;request.FailureReason=reason;Reservations.ReleaseByWorkRequest(requestId);CleanupHaulingTerminal(request,status,reason);_executionsByRequest.Remove(requestId);_assignmentsByMercenary.Remove(request.AssignedMercenaryId);_control.ExternalMovements.Cancel(request.AssignedMercenaryId,reason,_control.Movements);if(_mercenaries.Registry.TryGetState(request.AssignedMercenaryId,out MercenaryStateV3? state)&&state!=null&&!_control.Movements.TryGet(request.AssignedMercenaryId,out _))state.TrySetActivityState(MercenaryActivityStateV3.Idle,out _);if(status==WorkRequestStatusV3.Completed)Diagnostics.CompletedWorkCount++;else if(status==WorkRequestStatusV3.Failed)Diagnostics.FailedWorkCount++;else if(status==WorkRequestStatusV3.Cancelled)Diagnostics.CancelledWorkCount++;else if(status==WorkRequestStatusV3.Superseded)Diagnostics.SupersededWorkCount++;Diagnostics.LastFailureReason=reason;_history.Enqueue(requestId);while(_history.Count>64){string remove=_history.Dequeue();_requests.Remove(remove);TrimHaulingHistory(remove);}return true;}
}
