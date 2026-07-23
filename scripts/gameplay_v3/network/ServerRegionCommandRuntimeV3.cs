using System;
using System.Collections.Generic;
using GameplayV3.Control;
using GameplayV3.Mercenary;
using GameplayV3.Movement;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using GameplayV3.Session;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Network;

internal sealed class ServerRegionNavigationQueryV3:IMercenaryNavigationWorldQueryV3,INavigationOccupancyRevisionV3
{
    private readonly ActiveRegionSessionV3 _active;
    private readonly Rect2I _bounds;
    public ServerRegionNavigationQueryV3(ActiveRegionSessionV3 active,Rect2I bounds){_active=active;_bounds=bounds;}
    public long OccupancyRevision=>_active.Construction.Structures.OccupancyRevision;
    public bool IsInsideWorld(Vector2I cell)=>_bounds.HasPoint(cell);
    public bool IsWalkable(Vector2I cell)=>IsInsideWorld(cell)&&!_active.Construction.Structures.IsMovementBlocked(new(cell));
    public float GetTraversalMultiplier(Vector2I cell)=>1f;
    public MercenaryNavigationCellInfoV3 GetCellInfo(Vector2I cell)=>new(IsInsideWorld(cell),IsWalkable(cell),1f,TileType.Grass,BiomeKindV3.Plains);
}

internal sealed class ServerRegionCommandRuntimeV3
{
    private readonly ActiveRegionSessionV3 _active;
    private readonly ServerRegionNavigationQueryV3 _query;
    private readonly MercenaryPathfindingSchedulerV3 _scheduler=new(new(){MaxExpansionsPerTick=1024,MaxMillisecondsPerTick=8});
    private readonly MercenaryMovementCoordinatorV3 _movement=new(new());
    private readonly GatheringWorkSettingsV3 _gatheringSettings=new();
    private readonly Dictionary<string,Vector2I> _lastMercenaryCells=new(StringComparer.Ordinal);

    public ServerRegionCommandRuntimeV3(ActiveRegionSessionV3 active,Rect2I bounds)
    {
        _active=active;_query=new(active,bounds);
        foreach(string id in active.ActiveMercenaryIds)if(active.CompanyState.MercenaryProfiles.TryGetState(id,out MercenaryStateV3? state)&&state!=null)_lastMercenaryCells[id]=state.CurrentCell.Value;
    }

    public IMercenaryNavigationWorldQueryV3 NavigationQuery=>_query;
    public event Action<string>? MercenaryChanged;
    public event Action<string>? MercenaryOrderChanged;
    public event Action<string>? ResourceNodeChanged;
    public event Action<string>? GroundStackChanged;
    internal void TrackMercenary(string id)
    {
        if(_active.CompanyState.MercenaryProfiles.TryGetState(id,out MercenaryStateV3? state)&&state!=null)_lastMercenaryCells[id]=state.CurrentCell.Value;
    }
    internal void UntrackMercenary(string id)
    {
        _active.Control.CancelCurrentActivity(id);
        _lastMercenaryCells.Remove(id);
    }

    public void Tick(float delta)
    {
        if(_active.IsDisposed)return;
        TickGatheringExecutions(delta);
        long occupancy=_query.OccupancyRevision;
        foreach(MercenaryMoveOrderV3 order in _active.Control.Commands.GetPendingOrders())Enqueue(order.PathRequest,occupancy);
        foreach(MercenaryMovementRequestV3 request in _active.Control.ExternalMovements.GetPending())Enqueue(request.PathRequest,occupancy);
        foreach(MercenaryPathResultV3 result in _scheduler.Tick(_query,IsCurrent,out _))ApplyPathResult(result);
        StartReadyMovements();
        List<string> movingIds=new(_active.Control.Movements.GetActiveIds());
        _movement.Tick(delta,_active.Control,_query);
        foreach(string id in _movement.CompletedThisTick)
            if(_active.Control.ExternalMovements.TryGetActive(id,out MercenaryMovementRequestV3? request)&&request!=null&&request.Status==MercenaryMovementRequestStatusV3.Moving)
                _active.Control.ExternalMovements.Complete(request,true,string.Empty);
        foreach(string id in _movement.BlockedThisTick)
            if(_active.Control.ExternalMovements.TryGetActive(id,out MercenaryMovementRequestV3? request)&&request!=null)_active.Control.ExternalMovements.Complete(request,false,"DynamicObstacle");
        foreach(string id in movingIds)PublishCellIfChanged(id);
        DrainWorkMovementResults();
        TickGatheringExecutions(delta);
    }

    private void TickGatheringExecutions(float delta)
    {
        foreach(MercenaryWorkExecutionStateV3 execution in _active.Work.GetActiveExecutions())
        {
            if(!_active.Work.TryGetRequest(execution.WorkRequestId,out WorkRequestV3? request)||request==null)continue;
            if(!_active.Resources.Nodes.TryGet(execution.ResourceNodeId,out ResourceNodeStateV3? node)||node==null){_active.Work.Fail(execution.WorkRequestId,"TargetRemoved");MercenaryOrderChanged?.Invoke(execution.MercenaryId);continue;}
            if(execution.Phase==WorkExecutionPhaseV3.SelectingApproach)
            {
                if(execution.CurrentCandidateIndex>=execution.CandidateApproachCells.Count||!_active.CompanyState.MercenaryProfiles.TryGetState(execution.MercenaryId,out MercenaryStateV3? state)||state==null){_active.Work.Fail(execution.WorkRequestId,"NoReachableApproachCell");continue;}
                GlobalCellCoord candidate=execution.CandidateApproachCells[execution.CurrentCandidateIndex];
                if(!_active.Control.ExternalMovements.TryRequest(execution.MercenaryId,state.CurrentCell,candidate,MovementRequestSourceTypeV3.Work,execution.WorkRequestId,_active.SessionRevision,execution.Revision,_active.Control.Movements,out MercenaryMovementRequestV3? movement,out string reason)||movement==null){_active.Work.Fail(execution.WorkRequestId,reason);continue;}
                execution.SelectedApproachCell=candidate;execution.MovementRequestId=movement.MovementRequestId;execution.Phase=WorkExecutionPhaseV3.WaitingForPath;request.Status=WorkRequestStatusV3.MovingToTarget;MercenaryOrderChanged?.Invoke(execution.MercenaryId);continue;
            }
            if(execution.Phase==WorkExecutionPhaseV3.WaitingForPath&&_active.Control.ExternalMovements.TryGetActive(execution.MercenaryId,out MercenaryMovementRequestV3? moving)&&moving?.Status==MercenaryMovementRequestStatusV3.Moving){execution.Phase=WorkExecutionPhaseV3.MovingToApproach;request.Status=WorkRequestStatusV3.MovingToTarget;}
            if(execution.Phase!=WorkExecutionPhaseV3.Working)continue;
            if(!_active.CompanyState.MercenaryProfiles.TryGetState(execution.MercenaryId,out MercenaryStateV3? worker)||worker==null||execution.SelectedApproachCell==null||worker.CurrentCell.Value!=execution.SelectedApproachCell.Value.Value){_active.Work.Fail(execution.WorkRequestId,"InvalidWorkState");continue;}
            execution.WorkProgressSeconds+=Math.Max(0,delta);
            if(execution.WorkProgressSeconds<execution.RequiredWorkSeconds)continue;
            execution.WorkProgressSeconds-=execution.RequiredWorkSeconds;
            if(!node.TryHarvest(out int amount,out string harvestReason)){_active.Work.Fail(execution.WorkRequestId,harvestReason);continue;}
            execution.CompletedCycleCount++;_active.Work.Diagnostics.CompletedCycleCount++;
            if(!_active.Resources.GroundStacks.TryAddStack(node.ProducedResourceType,amount,worker.CurrentCell,out GroundResourceStackV3? stack,out _,out string stackReason)||stack==null){_active.Work.Fail(execution.WorkRequestId,stackReason);continue;}
            if(!_active.Resources.GenerationLedger.TryRecord(node.ProducedResourceType,amount,"Gathering",execution.MercenaryId,execution.WorkRequestId,node.ResourceNodeId,worker.CurrentCell,$"{execution.WorkRequestId}:{execution.CompletedCycleCount}")){_active.Work.Fail(execution.WorkRequestId,"GenerationLedgerFailed");continue;}
            _active.Resources.Nodes.NotifyChanged(node.ResourceNodeId);_active.PersistentState.MarkGameplayChanged();
            ResourceNodeChanged?.Invoke(node.ResourceNodeId);GroundStackChanged?.Invoke(stack.ResourceStackId);
            if(node.IsDepleted){_active.Work.Complete(execution.WorkRequestId);worker.TrySetActivityState(MercenaryActivityStateV3.Idle,out _);MercenaryOrderChanged?.Invoke(execution.MercenaryId);}
        }
    }

    private void DrainWorkMovementResults()
    {
        while(_active.Control.ExternalMovements.TryDequeueResult(out MercenaryMovementResultV3? result))
        {
            if(result==null||result.Request.SourceType!=MovementRequestSourceTypeV3.Work||!_active.Work.IsCurrentExecution(result.Request.SourceId,result.Request.SourceRevision)||!_active.Work.TryGetExecution(result.Request.SourceId,out MercenaryWorkExecutionStateV3? execution)||execution==null)continue;
            if(!result.Succeeded){execution.CurrentCandidateIndex++;execution.SelectedApproachCell=null;execution.Phase=WorkExecutionPhaseV3.SelectingApproach;continue;}
            if(!_active.CompanyState.MercenaryProfiles.TryGetState(execution.MercenaryId,out MercenaryStateV3? state)||state==null||execution.SelectedApproachCell==null||state.CurrentCell.Value!=execution.SelectedApproachCell.Value.Value){_active.Work.Fail(execution.WorkRequestId,"MovementFailed");continue;}
            execution.Phase=WorkExecutionPhaseV3.Working;if(_active.Work.TryGetRequest(execution.WorkRequestId,out WorkRequestV3? request)&&request!=null)request.Status=WorkRequestStatusV3.Working;
            state.TrySetActivityState(MercenaryActivityStateV3.Working,out _);MercenaryOrderChanged?.Invoke(execution.MercenaryId);
        }
    }

    private void Enqueue(MercenaryPathRequestV3 request,long occupancy){if(_scheduler.IsKnown(request.PathRequestId))return;request.NavigationOccupancyRevision=occupancy;_scheduler.Enqueue(request);}
    private bool IsCurrent(MercenaryPathRequestV3 request)=>(_active.Control.Commands.TryGetActiveOrder(request.MercenaryId,out MercenaryMoveOrderV3? order)&&order?.MoveOrderId==request.PathRequestId&&order.OrderRevision==request.OrderRevision)||_active.Control.ExternalMovements.IsCurrent(request);
    private void ApplyPathResult(MercenaryPathResultV3 result)
    {
        if(_active.Control.ExternalMovements.TryGetActive(result.Request.MercenaryId,out MercenaryMovementRequestV3? external)&&external?.MovementRequestId==result.Request.PathRequestId)
        {if(!result.Success)_active.Control.ExternalMovements.Complete(external,false,result.Failure.ToString());else{external.PathResult=result;external.Status=MercenaryMovementRequestStatusV3.PathReady;}return;}
        if(!_active.Control.Commands.TryGetActiveOrder(result.Request.MercenaryId,out MercenaryMoveOrderV3? order)||order?.MoveOrderId!=result.Request.PathRequestId)return;
        if(!result.Success){_active.Control.Commands.FinishOrder(order,false,result.Failure.ToString());MercenaryOrderChanged?.Invoke(order.MercenaryId);}
        else{order.PathResult=result;order.Status=MercenaryMoveOrderStatusV3.PathReady;}
    }
    private void StartReadyMovements()
    {
        foreach(MercenaryMoveOrderV3 order in _active.Control.Commands.GetActiveOrders())if(order.Status==MercenaryMoveOrderStatusV3.PathReady&&order.PathResult!=null&&!_active.Control.Movements.TryGet(order.MercenaryId,out _))
        {if(_movement.TryStart(order,order.PathResult,_active.Control.MercenarySession,_query,_active.Control.Movements,out _)){if(order.PathResult.Path.Count==0)_active.Control.Commands.FinishOrder(order,true,string.Empty);else order.Status=MercenaryMoveOrderStatusV3.Moving;MercenaryOrderChanged?.Invoke(order.MercenaryId);}}
        foreach(MercenaryMovementRequestV3 request in _active.Control.ExternalMovements.GetActive())if(request.Status==MercenaryMovementRequestStatusV3.PathReady&&request.PathResult!=null&&!_active.Control.Movements.TryGet(request.MercenaryId,out _))
        {if(_movement.TryStart(request,request.PathResult,_active.Control.MercenarySession,_query,_active.Control.Movements,out _)){if(request.PathResult.Path.Count==0)_active.Control.ExternalMovements.Complete(request,true,string.Empty);else request.Status=MercenaryMovementRequestStatusV3.Moving;}}
    }
    private void PublishCellIfChanged(string id)
    {
        if(!_active.CompanyState.MercenaryProfiles.TryGetState(id,out MercenaryStateV3? state)||state==null)return;
        if(_lastMercenaryCells.TryGetValue(id,out Vector2I old)&&old==state.CurrentCell.Value)return;
        _lastMercenaryCells[id]=state.CurrentCell.Value;MercenaryChanged?.Invoke(id);
    }
}
