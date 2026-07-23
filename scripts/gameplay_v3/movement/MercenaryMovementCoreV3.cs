using System;
using System.Collections.Generic;
using GameplayV3.Control;
using GameplayV3.Mercenary;
using GameplayV3.Navigation;
using Godot;
using WorldV2;

namespace GameplayV3.Movement;

public enum MovementRequestSourceTypeV3 { DirectCommand, Work }
public enum MercenaryMovementRequestStatusV3 { PendingPath, PathReady, Moving, Completed, Failed, Superseded, Cancelled }

public sealed class MercenaryMovementRequestV3
{
    internal MercenaryMovementRequestV3(string requestId,string mercenaryId,GlobalCellCoord start,GlobalCellCoord destination,MovementRequestSourceTypeV3 sourceType,string sourceId,long sessionRevision,long sourceRevision)
    {MovementRequestId=requestId;MercenaryId=mercenaryId;StartCell=start;DestinationCell=destination;SourceType=sourceType;SourceId=sourceId;SessionRevision=sessionRevision;SourceRevision=sourceRevision;PathRequest=new(requestId,mercenaryId,start,destination,sessionRevision,sourceRevision);}
    public string MovementRequestId{get;}public string MercenaryId{get;}public GlobalCellCoord StartCell{get;}public GlobalCellCoord DestinationCell{get;}public MovementRequestSourceTypeV3 SourceType{get;}public string SourceId{get;}public long SessionRevision{get;}public long SourceRevision{get;}public MercenaryPathRequestV3 PathRequest{get;}public MercenaryPathResultV3? PathResult{get;internal set;}public MercenaryMovementRequestStatusV3 Status{get;internal set;}=MercenaryMovementRequestStatusV3.PendingPath;public string FailureReason{get;internal set;}=string.Empty;
}

public sealed class MercenaryMovementResultV3
{
    public MercenaryMovementResultV3(MercenaryMovementRequestV3 request,bool success,bool superseded,string reason){Request=request;Succeeded=success;Superseded=superseded;FailureReason=reason;}
    public MercenaryMovementRequestV3 Request{get;}public bool Succeeded{get;}public bool Superseded{get;}public string FailureReason{get;}
}

public sealed class MercenaryMovementRequestRegistryV3
{
    private readonly Dictionary<string,MercenaryMovementRequestV3> _activeByMercenary=new(StringComparer.Ordinal);private readonly Queue<MercenaryMovementResultV3> _results=new();
    public int Count=>_activeByMercenary.Count;
    public bool TryRequest(string mercenaryId,GlobalCellCoord start,GlobalCellCoord destination,MovementRequestSourceTypeV3 sourceType,string sourceId,long sessionRevision,long sourceRevision,MercenaryMovementRegistryV3 movements,out MercenaryMovementRequestV3? request,out string reason)
    {request=null;if(string.IsNullOrWhiteSpace(mercenaryId)||string.IsNullOrWhiteSpace(sourceId)){reason="Movement source and mercenary are required.";return false;}if(_activeByMercenary.TryGetValue(mercenaryId,out MercenaryMovementRequestV3? old)){old.Status=MercenaryMovementRequestStatusV3.Superseded;_results.Enqueue(new(old,false,true,"Superseded by a newer movement request."));_activeByMercenary.Remove(mercenaryId);}movements.RequestStopAfterCurrentSegment(mercenaryId);string id=$"move:{sourceType}:{sourceId}:{mercenaryId}:{sourceRevision}";request=new(id,mercenaryId,start,destination,sourceType,sourceId,sessionRevision,sourceRevision);_activeByMercenary.Add(mercenaryId,request);reason=string.Empty;return true;}
    public bool TryGetActive(string mercenaryId,out MercenaryMovementRequestV3? request)=>_activeByMercenary.TryGetValue(mercenaryId,out request);
    public bool IsCurrent(MercenaryPathRequestV3 path)=>_activeByMercenary.TryGetValue(path.MercenaryId,out MercenaryMovementRequestV3? request)&&request.MovementRequestId==path.PathRequestId&&request.SessionRevision==path.SessionRevision&&request.SourceRevision==path.OrderRevision;
    public IReadOnlyList<MercenaryMovementRequestV3> GetPending(){List<MercenaryMovementRequestV3> result=new();foreach(var item in _activeByMercenary.Values)if(item.Status==MercenaryMovementRequestStatusV3.PendingPath)result.Add(item);result.Sort((a,b)=>string.CompareOrdinal(a.MercenaryId,b.MercenaryId));return result.AsReadOnly();}
    public IReadOnlyList<MercenaryMovementRequestV3> GetActive(){List<MercenaryMovementRequestV3> result=new(_activeByMercenary.Values);result.Sort((a,b)=>string.CompareOrdinal(a.MercenaryId,b.MercenaryId));return result.AsReadOnly();}
    public void Complete(MercenaryMovementRequestV3 request,bool success,string reason){if(!_activeByMercenary.TryGetValue(request.MercenaryId,out MercenaryMovementRequestV3? current)||!ReferenceEquals(current,request))return;request.Status=success?MercenaryMovementRequestStatusV3.Completed:MercenaryMovementRequestStatusV3.Failed;request.FailureReason=reason;_activeByMercenary.Remove(request.MercenaryId);_results.Enqueue(new(request,success,false,reason));}
    public bool Cancel(string mercenaryId,string reason,MercenaryMovementRegistryV3 movements){if(!_activeByMercenary.Remove(mercenaryId,out MercenaryMovementRequestV3? request))return false;request.Status=MercenaryMovementRequestStatusV3.Cancelled;request.FailureReason=reason;movements.RequestStopAfterCurrentSegment(mercenaryId);_results.Enqueue(new(request,false,true,reason));return true;}
    public bool TryDequeueResult(out MercenaryMovementResultV3? result){if(_results.Count==0){result=null;return false;}result=_results.Dequeue();return true;}
    public void Clear(){_activeByMercenary.Clear();_results.Clear();}
}

public sealed class MercenaryMovementSettingsV3
{
    public float BaseMoveSpeedCellsPerSecond { get; init; }=3f;
    public int MaxSegmentsAdvancedPerTick { get; init; }=16;
}

public sealed class MercenaryMovementStateV3
{
    private readonly List<GlobalCellCoord> _path;
    internal MercenaryMovementStateV3(string mercenaryId,string sourceId,MovementRequestSourceTypeV3 sourceType,long sourceRevision,long sessionRevision,GlobalCellCoord destination,IReadOnlyList<GlobalCellCoord> path,float moveMultiplier)
    { MercenaryId=mercenaryId;SourceId=sourceId;SourceType=sourceType;SourceRevision=sourceRevision;SessionRevision=sessionRevision;DestinationCell=destination;_path=new(path);MoveSpeedMultiplier=moveMultiplier;StartedUtc=DateTime.UtcNow; }
    public string MercenaryId{get;} public string SourceId{get;} public MovementRequestSourceTypeV3 SourceType{get;} public long SourceRevision{get;} public long SessionRevision{get;} public string CommandId=>SourceId; public long OrderRevision=>SourceRevision; public IReadOnlyList<GlobalCellCoord> Path=>_path.AsReadOnly(); public int NextPathIndex{get;internal set;}
    public GlobalCellCoord FromCell{get;internal set;} public GlobalCellCoord ToCell{get;internal set;} public float SegmentElapsed{get;internal set;} public float SegmentDuration{get;internal set;} public float SegmentProgress01=>SegmentDuration<=0?1:Mathf.Clamp(SegmentElapsed/SegmentDuration,0,1);
    public float EnteringTraversalMultiplier{get;internal set;} public float MoveSpeedMultiplier{get;} public GlobalCellCoord DestinationCell{get;} public bool StopAfterCurrentSegment{get;internal set;} public DateTime StartedUtc{get;}
    public int RemainingPathCells=>Math.Max(0,_path.Count-NextPathIndex);
}

public sealed class MercenaryMovementRegistryV3
{
    private readonly Dictionary<string,MercenaryMovementStateV3> _states=new(StringComparer.Ordinal);
    public int Count=>_states.Count;
    public bool TryGet(string id,out MercenaryMovementStateV3? state)=>_states.TryGetValue(id,out state);
    public IReadOnlyList<string> GetActiveIds(){List<string> ids=new(_states.Keys);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    internal void Set(MercenaryMovementStateV3 state)=>_states[state.MercenaryId]=state;
    internal bool Remove(string id)=>_states.Remove(id);
    public void RequestStopAfterCurrentSegment(string id){if(_states.TryGetValue(id,out MercenaryMovementStateV3? state))state.StopAfterCurrentSegment=true;}
    public void Clear()=>_states.Clear();
}

public sealed class MercenaryMovementCoordinatorV3
{
    private readonly MercenaryMovementSettingsV3 _settings;
    private Func<string,float>? _runtimeSpeedMultiplier;
    private Action<string,GlobalCellCoord,GlobalCellCoord>? _segmentStarting;
    private Action<string>? _movementEnded;
    private readonly List<string> _completedThisTick = new();
    private readonly List<string> _blockedThisTick = new();
    public MercenaryMovementCoordinatorV3(MercenaryMovementSettingsV3 settings){_settings=settings;}
    public void AttachRuntimeSpeedMultiplier(Func<string,float>? resolver)=>_runtimeSpeedMultiplier=resolver;
    public void AttachPassageCallbacks(Action<string,GlobalCellCoord,GlobalCellCoord>? segmentStarting,Action<string>? movementEnded){_segmentStarting=segmentStarting;_movementEnded=movementEnded;}
    public IReadOnlyList<string> CompletedThisTick => _completedThisTick;
    public IReadOnlyList<string> BlockedThisTick => _blockedThisTick;
    public bool TryStart(MercenaryMoveOrderV3 order,MercenaryPathResultV3 result,MercenarySessionV3 mercenary,IMercenaryNavigationWorldQueryV3 query,MercenaryMovementRegistryV3 registry,out string reason)
    {
        if(registry.Contains(order.MercenaryId)){reason="Previous segment is still active.";return false;}
        if(!mercenary.Registry.TryGetMercenary(order.MercenaryId,out MercenaryProfileV3? profile,out MercenaryStateV3? state)||profile==null||state==null){reason="Mercenary data missing.";return false;}
        if(result.Path.Count==0){state.TrySetCurrentCell(order.DestinationCell,out _);state.TrySetActivityState(MercenaryActivityStateV3.Idle,out _);reason=string.Empty;return true;}
        MercenaryMovementStateV3 moving=new(order.MercenaryId,order.CommandId,MovementRequestSourceTypeV3.DirectCommand,order.OrderRevision,order.SessionRevision,order.DestinationCell,result.Path,MercenaryDerivedStatsCalculatorV3.Calculate(profile).MoveSpeedMultiplier);moving.FromCell=state.CurrentCell;moving.ToCell=result.Path[0];moving.NextPathIndex=1;PrepareSegment(moving,query);registry.Set(moving);state.TrySetActivityState(MercenaryActivityStateV3.Moving,out _);reason=string.Empty;return true;
    }
    public bool TryStart(MercenaryMovementRequestV3 request,MercenaryPathResultV3 result,MercenarySessionV3 mercenary,IMercenaryNavigationWorldQueryV3 query,MercenaryMovementRegistryV3 registry,out string reason)
    {if(registry.Contains(request.MercenaryId)){reason="Previous segment is still active.";return false;}if(!mercenary.Registry.TryGetMercenary(request.MercenaryId,out MercenaryProfileV3? profile,out MercenaryStateV3? state)||profile==null||state==null){reason="Mercenary data missing.";return false;}if(result.Path.Count==0){state.TrySetCurrentCell(request.DestinationCell,out _);state.TrySetActivityState(MercenaryActivityStateV3.Idle,out _);reason=string.Empty;return true;}MercenaryMovementStateV3 moving=new(request.MercenaryId,request.SourceId,request.SourceType,request.SourceRevision,request.SessionRevision,request.DestinationCell,result.Path,MercenaryDerivedStatsCalculatorV3.Calculate(profile).MoveSpeedMultiplier);moving.FromCell=state.CurrentCell;moving.ToCell=result.Path[0];moving.NextPathIndex=1;PrepareSegment(moving,query);registry.Set(moving);state.TrySetActivityState(MercenaryActivityStateV3.Moving,out _);reason=string.Empty;return true;}
    public void Tick(float delta,MercenaryControlSessionV3 control,IMercenaryNavigationWorldQueryV3 query)
    {
        _completedThisTick.Clear();_blockedThisTick.Clear();
        foreach(string id in control.Movements.GetActiveIds())
        {
            if(!control.Movements.TryGet(id,out MercenaryMovementStateV3? moving)||moving==null)continue;
            if(!control.MercenarySession.Registry.TryGetState(id,out MercenaryStateV3? state)||state==null){control.Movements.Remove(id);_movementEnded?.Invoke(id);_blockedThisTick.Add(id);continue;}
            float remainingDelta=Math.Max(0,delta);int advanced=0;bool finished=false;
            while(remainingDelta>0&&advanced<_settings.MaxSegmentsAdvancedPerTick)
            {
                if(!_querySafe(query,moving.ToCell.Value)){finished=true;_blockedThisTick.Add(id);break;}float left=Math.Max(0,moving.SegmentDuration-moving.SegmentElapsed);float consume=Math.Min(left,remainingDelta);moving.SegmentElapsed+=consume;remainingDelta-=consume;
                if(moving.SegmentElapsed+0.000001f<moving.SegmentDuration)break;
                state.TrySetCurrentCell(moving.ToCell,out _);advanced++;
                if(moving.StopAfterCurrentSegment||moving.NextPathIndex>=moving.Path.Count){finished=true;break;}
                moving.FromCell=state.CurrentCell;moving.ToCell=moving.Path[moving.NextPathIndex++];PrepareSegment(moving,query);
            }
            if(!finished)continue;
            control.Movements.Remove(id);_movementEnded?.Invoke(id);state.TrySetActivityState(MercenaryActivityStateV3.Idle,out _);
            if(!_blockedThisTick.Contains(id))_completedThisTick.Add(id);
            if(control.Commands.TryGetActiveOrder(id,out MercenaryMoveOrderV3? order)&&order!=null&&order.CommandId==moving.CommandId&&order.OrderRevision==moving.OrderRevision)
            { control.Commands.FinishOrder(order,true,string.Empty);control.Diagnostics.CompletedMovementCount++; }
        }
    }
    private static bool _querySafe(IMercenaryNavigationWorldQueryV3 query,Vector2I cell)=>query.IsWalkable(cell);
    private void PrepareSegment(MercenaryMovementStateV3 state,IMercenaryNavigationWorldQueryV3 query)
    { _segmentStarting?.Invoke(state.MercenaryId,state.FromCell,state.ToCell);state.SegmentElapsed=0;state.EnteringTraversalMultiplier=query.GetTraversalMultiplier(state.ToCell.Value);float distance=MercenaryMovementCostPolicyV3.DirectionDistance(state.FromCell.Value,state.ToCell.Value);float fatigue=Math.Clamp(_runtimeSpeedMultiplier?.Invoke(state.MercenaryId)??1f,.1f,2f);float speed=_settings.BaseMoveSpeedCellsPerSecond*state.MoveSpeedMultiplier*fatigue;state.SegmentDuration=distance*state.EnteringTraversalMultiplier/Math.Max(0.001f,speed); }
}

internal static class MovementRegistryExtensionsV3
{
    public static bool Contains(this MercenaryMovementRegistryV3 registry,string id)=>registry.TryGet(id,out _);
}
