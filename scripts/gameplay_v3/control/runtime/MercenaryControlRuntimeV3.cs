using System;
using System.Collections.Generic;
using GameplayV3.Mercenary;
using GameplayV3.Mercenary.Runtime;
using GameplayV3.Movement;
using GameplayV3.Navigation;
using GameplayV3.Session;
using GameplayV3.Resources;
using GameplayV3.Resources.Runtime;
using GameplayV3.Work;
using GameplayV3.Stockpile;
using GameplayV3.Stockpile.Runtime;
using Godot;
using WorldV2;

namespace GameplayV3.Control.Runtime;

public partial class MercenaryDragSelectionOverlayV3 : Godot.Control
{
    private bool _active; private Rect2 _rect;
    public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);ZIndex=150;}
    public void ShowRect(Vector2 start,Vector2 current){_active=true;_rect=Normalize(start,current);QueueRedraw();}
    public void HideRect(){if(!_active)return;_active=false;QueueRedraw();}
    public override void _Draw(){if(!_active)return;DrawRect(_rect,new Color(0.20f,0.95f,0.85f,0.10f));DrawRect(_rect,new Color(0.45f,1f,0.92f,0.85f),false,1f);}
    private static Rect2 Normalize(Vector2 a,Vector2 b)=>new(new Vector2(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y)),new Vector2(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y)));
}

public partial class MercenaryCommandMarkerV3 : Node2D
{
    private long _revision;private bool _gather;
    public void ShowAt(Vector2 position,double seconds=0.75){_gather=false;ShowMarker(position,seconds);}public void ShowGatherAt(Vector2 position,double seconds=0.75){_gather=true;ShowMarker(position,seconds);}private void ShowMarker(Vector2 position,double seconds){Position=position;Visible=true;long revision=++_revision;QueueRedraw();HideLater(revision,seconds);}
    private async void HideLater(long revision,double seconds){await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);if(revision==_revision)Visible=false;}
    public override void _Draw(){Color color=_gather?new Color(1f,0.74f,0.18f):new Color(0.4f,1f,0.9f);DrawCircle(Vector2.Zero,7,new Color(color.R,color.G,color.B,0.15f));DrawArc(Vector2.Zero,7,0,Mathf.Tau,24,color,1.5f);DrawLine(new(-4,0),new(4,0),color,1);DrawLine(new(0,-4),new(0,4),color,1);}
}

public partial class MercenaryInputControllerV3 : Node2D
{
    private const float DragThreshold=6f; private const float MinHitRadius=12f; private const float MaxHitRadius=20f;
    private MercenaryControlSessionV3? _control; private MercenaryViewRegistryV3? _views; private WorldV2GridRenderer? _grid; private WorldManagerV2? _manager; private IMercenaryNavigationWorldQueryV3? _query; private MercenaryDragSelectionOverlayV3? _dragOverlay; private MercenaryCommandMarkerV3? _marker;private ResourceSessionV3? _resources;private ResourceNodeViewRegistryV3? _resourceViews;private GroundResourceStackViewRegistryV3? _stackViews;private MercenaryWorkSessionV3? _work;private StockpileDesignationControllerV3? _stockpileDesignation;
    private bool _leftDown;private bool _dragging;private bool _shiftAtStart;private Vector2 _dragStart;
    public void Initialize(MercenaryControlSessionV3 control,MercenaryViewRegistryV3 views,WorldV2GridRenderer grid,WorldManagerV2 manager,IMercenaryNavigationWorldQueryV3 query,MercenaryDragSelectionOverlayV3 overlay,MercenaryCommandMarkerV3 marker)
    {_control=control;_views=views;_grid=grid;_manager=manager;_query=query;_dragOverlay=overlay;_marker=marker;RefreshSelectionVisuals();}
    public void AttachGathering(ResourceSessionV3 resources,ResourceNodeViewRegistryV3 resourceViews,MercenaryWorkSessionV3 work){_resources=resources;_resourceViews=resourceViews;_work=work;}
    public void AttachStockpileAndHauling(GroundResourceStackViewRegistryV3 stackViews,StockpileDesignationControllerV3 designation){_stackViews=stackViews;_stockpileDesignation=designation;}
    public bool TryHandleUnhandledInput(InputEvent e)
    {
        if(_stockpileDesignation?.TryHandleInput(e)==true)return true;
        if(e is InputEventKey k&&k.Pressed&&!k.Echo&&k.Keycode==Key.Escape){CancelDrag();_control?.Selection.Clear();RefreshSelectionVisuals();_manager?.UpdateDebugHud("Selection cleared.");return true;}
        if(e is InputEventMouseMotion motion)return HandlePointerMotion(motion.Position);
        if(e is InputEventMouseButton button)return HandlePointerButton(button.Position,button.ButtonIndex,button.Pressed,button.ShiftPressed);
        return false;
    }
    public bool HandlePointerButton(Vector2 screen,MouseButton button,bool pressed,bool shift)
    {
        if(_control==null||_views==null||_grid==null||_manager==null||_query==null)return false;
        if(button==MouseButton.Left){if(pressed){_leftDown=true;_dragging=false;_shiftAtStart=shift;_dragStart=screen;return true;}if(!_leftDown)return false;_leftDown=false;if(_dragging){ApplyDrag(screen,_shiftAtStart);CancelDrag();}else ApplyClick(screen,shift);return true;}
        if(button==MouseButton.Right&&pressed&&!_dragging){string? stack=FindStackHit(screen);if(stack!=null)IssueHauling(stack);else{string? resource=FindResourceHit(screen);if(resource!=null)IssueGather(resource);else IssueMove(screen);}return true;}return false;
    }
    public bool HandlePointerMotion(Vector2 screen)
    {if(!_leftDown)return false;if(!_dragging&&screen.DistanceTo(_dragStart)>=DragThreshold)_dragging=true;if(_dragging)_dragOverlay?.ShowRect(_dragStart,screen);return _dragging;}
    public void CancelDrag(){_leftDown=false;_dragging=false;_dragOverlay?.HideRect();}
    public void RefreshSelectionVisuals(){if(_control==null||_views==null)return;foreach(string id in _views.GetAllViewIds())if(_views.TryGetView(id,out MercenaryEntityV3? view)&&view!=null)view.SetSelected(_control.Selection.Contains(id));}
    private void ApplyClick(Vector2 screen,bool shift)
    {string? hit=FindHit(screen);if(hit==null){if(!shift)_control!.Selection.Clear();}else if(shift)_control!.Selection.TryToggle(hit,out _);else _control!.Selection.TrySelectSingle(hit,out _);RefreshSelectionVisuals();_manager?.UpdateDebugHud("Selection updated.");}
    private void ApplyDrag(Vector2 screen,bool shift)
    {Rect2 rect=Normalize(_dragStart,screen);List<string> ids=new();foreach(string id in _views!.GetAllViewIds())if(_views.TryGetView(id,out MercenaryEntityV3? view)&&view!=null&&rect.HasPoint(view.GetGlobalTransformWithCanvas().Origin)&&_manager!.CanPlayerControlMercenary(_manager.LocalPlayerId,id))ids.Add(id);if(shift){foreach(string id in ids)_control!.Selection.TryAdd(id,out _);}else _control!.Selection.TryReplaceSelection(ids,out _);RefreshSelectionVisuals();_manager?.UpdateDebugHud("Drag selection updated.");}
    private string? FindHit(Vector2 screen)
    {string? best=null;float bestDistance=float.MaxValue;Transform2D canvas=GetViewport().GetCanvasTransform();float scale=Math.Abs(canvas.X.Length());float radius=Mathf.Clamp(10f*scale,MinHitRadius,MaxHitRadius);foreach(string id in _views!.GetAllViewIds()){if(!_views.TryGetView(id,out MercenaryEntityV3? view)||view==null||!_manager!.CanPlayerControlMercenary(_manager.LocalPlayerId,id))continue;float d=screen.DistanceTo(view.GetGlobalTransformWithCanvas().Origin);if(d<=radius&&(d<bestDistance-0.001f||(Mathf.IsEqualApprox(d,bestDistance)&&string.CompareOrdinal(id,best)<0))){best=id;bestDistance=d;}}return best;}
    private string? FindResourceHit(Vector2 screen){if(_resourceViews==null)return null;string? best=null;float bestDistance=float.MaxValue;Transform2D canvas=GetViewport().GetCanvasTransform();float radius=Mathf.Clamp(11f*Math.Abs(canvas.X.Length()),12f,22f);foreach(string id in _resourceViews.GetIds()){if(!_resourceViews.TryGet(id,out ResourceNodeEntityV3? view)||view==null)continue;float distance=screen.DistanceTo(view.GetGlobalTransformWithCanvas().Origin);if(distance<=radius&&(distance<bestDistance-0.001f||(Mathf.IsEqualApprox(distance,bestDistance)&&string.CompareOrdinal(id,best)<0))){best=id;bestDistance=distance;}}return best;}
    private string? FindStackHit(Vector2 screen){if(_stackViews==null)return null;string? best=null;float bestDistance=float.MaxValue;Transform2D canvas=GetViewport().GetCanvasTransform();float radius=Mathf.Clamp(9f*Math.Abs(canvas.X.Length()),12f,20f);foreach(string id in _stackViews.GetIds()){if(!_stackViews.TryGet(id,out GroundResourceStackEntityV3? view)||view==null)continue;float distance=screen.DistanceTo(view.GetGlobalTransformWithCanvas().Origin);if(distance<=radius&&(distance<bestDistance-0.001f||(Mathf.IsEqualApprox(distance,bestDistance)&&string.CompareOrdinal(id,best)<0))){best=id;bestDistance=distance;}}return best;}
    private void IssueGather(string nodeId){if(_work==null||_resources==null)return;IReadOnlyList<string> selected=_control!.Selection.GetSelectedIds();if(selected.Count==0){_manager!.UpdateDebugHud("Select a mercenary before gathering.");return;}if(_work.TryIssueGathering(_manager!.LocalPlayerId,_manager.LocalCompanyId,selected,nodeId,_query!,GameplaySessionV3.SessionRevision,out WorkRequestV3? request,out string reason)&&request!=null&&_resources.Nodes.TryGet(nodeId,out ResourceNodeStateV3? node)&&node!=null){_marker?.ShowGatherAt(_grid!.CellToWorldCenter(node.Cell.Value));GD.Print($"[WorkCoreV3] Gathering request={request.WorkRequestId} worker={request.AssignedMercenaryId} node={nodeId}");_manager.UpdateDebugHud("Gathering assigned to one mercenary.");}else{GD.PushWarning($"[WorkCoreV3] Gathering rejected: {reason}");_manager!.UpdateDebugHud(reason);}}
    private void IssueHauling(string stackId){if(_work==null||_resources==null)return;IReadOnlyList<string> selected=_control!.Selection.GetSelectedIds();if(selected.Count==0){_manager!.UpdateDebugHud("Select a mercenary before hauling.");return;}if(_work.TryIssueHauling(_manager!.LocalPlayerId,_manager.LocalCompanyId,selected,stackId,_query!,GameplaySessionV3.SessionRevision,out WorkRequestV3? request,out string reason)&&request!=null&&_resources.GroundStacks.TryGet(stackId,out GroundResourceStackV3? stack)&&stack!=null){_marker?.ShowGatherAt(_grid!.CellToWorldCenter(stack.Cell.Value));GD.Print($"[HaulingV3] request={request.WorkRequestId} worker={request.AssignedMercenaryId} stack={stackId} amount={stack.Amount}");_manager.UpdateDebugHud("Hauling assigned to one mercenary.");}else{GD.PushWarning($"[HaulingV3] rejected: {reason}");_manager!.UpdateDebugHud(reason);}}
    public bool HandleGroundStackCommand(string stackId){int before=_work?.ActiveHaulingRequestCount??0;IssueHauling(stackId);return (_work?.ActiveHaulingRequestCount??0)>before;}
    private void IssueMove(Vector2 screen)
    {IReadOnlyList<string> ids=_control!.Selection.GetSelectedIds();if(ids.Count==0)return;Vector2 world=GetViewport().GetCanvasTransform().AffineInverse()*screen;Vector2I cell=_grid!.WorldToCell(world);if(!_manager!.IsCellWithinWorldBounds(cell)){const string boundsReason="Move target is outside world bounds.";GD.PushWarning($"[MercenaryControlV3] {boundsReason}");_manager.UpdateDebugHud(boundsReason);return;}if(_control.TryIssueDirectMove(_manager.LocalPlayerId,_manager.LocalCompanyId,ids,new GlobalCellCoord(cell),_query!,GameplaySessionV3.SessionRevision,out DirectMoveCommandV3? command,out string reason)&&command!=null){_marker?.ShowAt(_grid.CellToWorldCenter(cell));GD.Print($"[MercenaryControlV3] Command {command.CommandId} target={cell} count={ids.Count}");}else GD.PushWarning($"[MercenaryControlV3] Move rejected: {reason}");_manager.UpdateDebugHud(reason);}
    private static Rect2 Normalize(Vector2 a,Vector2 b)=>new(new Vector2(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y)),new Vector2(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y)));
}

public partial class MercenaryMovementRuntimeV3 : Node
{
    private MercenaryControlSessionV3? _control;private MercenaryViewRegistryV3? _views;private WorldV2GridRenderer? _grid;private IMercenaryNavigationWorldQueryV3? _query;private WorldManagerV2? _manager;
    private readonly MercenaryPathfindingSchedulerV3 _scheduler=new(new());private readonly MercenaryMovementCoordinatorV3 _movement=new(new());
    public void Initialize(MercenaryControlSessionV3 control,MercenaryViewRegistryV3 views,WorldV2GridRenderer grid,IMercenaryNavigationWorldQueryV3 query,WorldManagerV2 manager){_control=control;_views=views;_grid=grid;_query=query;_manager=manager;RestoreViews();}
    public override void _PhysicsProcess(double delta)
    {
        if(_control==null||_views==null||_grid==null||_query==null||!GameplaySessionV3.IsCurrentControlSession(_control))return;
        long occupancyRevision=CurrentOccupancyRevision();
        foreach(MercenaryMoveOrderV3 order in _control.Commands.GetPendingOrders())EnqueueWithOccupancyRevision(order.PathRequest,occupancyRevision);
        foreach(MercenaryMovementRequestV3 request in _control.ExternalMovements.GetPending())EnqueueWithOccupancyRevision(request.PathRequest,occupancyRevision);
        IReadOnlyList<MercenaryPathResultV3> results=_scheduler.Tick(_query,IsCurrentRequest,out int peak);_control.Diagnostics.PeakDiscoveredCellCount=Math.Max(_control.Diagnostics.PeakDiscoveredCellCount,peak);
        foreach(MercenaryPathResultV3 result in results){if(!MercenaryNavigationRevisionPolicyV3.IsCurrent(result.Request,_query))RejectStaleOccupancy(result);else ApplyResult(result);}
        StartReadyOrders();StartReadyExternalMovements();_movement.Tick((float)delta,_control,_query);FailDynamicallyBlockedMovements();CompleteExternalMovements();UpdateMovingViews();SnapCompletedViews();
    }
    public void RestoreViews(){if(_control==null||_views==null||_grid==null)return;foreach(string id in _views.GetAllViewIds()){if(!_views.TryGetView(id,out MercenaryEntityV3? view)||view==null)continue;if(_control.Movements.TryGet(id,out MercenaryMovementStateV3? moving)&&moving!=null)view.Position=Interpolate(moving);else if(_control.MercenarySession.Registry.TryGetState(id,out MercenaryStateV3? state)&&state!=null)view.Position=_grid.CellToWorldCenter(state.CurrentCell.Value);}}
    private bool IsCurrentRequest(MercenaryPathRequestV3 request)=>(_control!.Commands.TryGetActiveOrder(request.MercenaryId,out MercenaryMoveOrderV3? order)&&order!=null&&order.MoveOrderId==request.PathRequestId&&order.OrderRevision==request.OrderRevision&&order.SessionRevision==request.SessionRevision)||_control.ExternalMovements.IsCurrent(request);
    private void ApplyResult(MercenaryPathResultV3 result)
    {if(result.Request.SessionRevision!=_control!.SessionRevision){_control.Diagnostics.StalePathResultDiscardCount++;return;}if(_control.ExternalMovements.TryGetActive(result.Request.MercenaryId,out MercenaryMovementRequestV3? external)&&external!=null&&external.MovementRequestId==result.Request.PathRequestId&&external.SourceRevision==result.Request.OrderRevision){ApplyExternalPathResult(external,result);return;}if(!_control.Commands.TryGetActiveOrder(result.Request.MercenaryId,out MercenaryMoveOrderV3? order)||order==null||order.MoveOrderId!=result.Request.PathRequestId||order.OrderRevision!=result.Request.OrderRevision||order.SessionRevision!=result.Request.SessionRevision){_control.Diagnostics.StalePathResultDiscardCount++;return;}_control.Diagnostics.LastPathLength=result.Path.Count;_control.Diagnostics.LastPathCost=result.TotalCost;_control.Diagnostics.LastExpandedNodeCount=result.ExpandedNodeCount;_control.Diagnostics.LastSearchDurationMs=result.SearchDurationMs;GD.Print($"[MercenaryControlV3] Path {(result.Success?"completed":"failed")} mercenary={result.Request.MercenaryId} length={result.Path.Count} cost={result.TotalCost:0.00} expanded={result.ExpandedNodeCount} cpuMs={result.SearchDurationMs:0.000} failure={result.Failure}");if(!result.Success){_control.Diagnostics.FailedPathCount++;if(result.Failure==MercenaryPathFailureV3.SearchLimitExceeded)_control.Diagnostics.SearchLimitExceededCount++;_control.Commands.FinishOrder(order,false,result.Failure.ToString());_control.MercenarySession.Registry.TryGetState(order.MercenaryId,out MercenaryStateV3? state);state?.TrySetActivityState(MercenaryActivityStateV3.Idle,out _);_manager?.UpdateDebugHud(result.Failure.ToString());return;}_control.Diagnostics.CompletedPathCount++;order.PathResult=result;order.Status=MercenaryMoveOrderStatusV3.PathReady;_manager?.UpdateDebugHud("Path ready.");}
    private void ApplyExternalPathResult(MercenaryMovementRequestV3 request,MercenaryPathResultV3 result){_control!.Diagnostics.LastPathLength=result.Path.Count;_control.Diagnostics.LastPathCost=result.TotalCost;_control.Diagnostics.LastExpandedNodeCount=result.ExpandedNodeCount;_control.Diagnostics.LastSearchDurationMs=result.SearchDurationMs;if(!result.Success){_control.Diagnostics.FailedPathCount++;_control.ExternalMovements.Complete(request,false,result.Failure.ToString());return;}_control.Diagnostics.CompletedPathCount++;request.PathResult=result;request.Status=MercenaryMovementRequestStatusV3.PathReady;}
    private void RejectStaleOccupancy(MercenaryPathResultV3 result){_control!.Diagnostics.StalePathResultDiscardCount++;if(_control.ExternalMovements.TryGetActive(result.Request.MercenaryId,out MercenaryMovementRequestV3? external)&&external?.MovementRequestId==result.Request.PathRequestId)_control.ExternalMovements.Complete(external,false,"StaleNavigationOccupancy");else if(_control.Commands.TryGetActiveOrder(result.Request.MercenaryId,out MercenaryMoveOrderV3? order)&&order?.MoveOrderId==result.Request.PathRequestId)_control.Commands.FinishOrder(order,false,"StaleNavigationOccupancy");}
    private void EnqueueWithOccupancyRevision(MercenaryPathRequestV3 request,long occupancyRevision){if(_scheduler.IsKnown(request.PathRequestId))return;request.NavigationOccupancyRevision=occupancyRevision;_scheduler.Enqueue(request);}
    private void StartReadyOrders(){foreach(MercenaryMoveOrderV3 order in _control!.Commands.GetActiveOrders()){if(order.Status!=MercenaryMoveOrderStatusV3.PathReady||order.PathResult==null||_control.Movements.TryGet(order.MercenaryId,out _))continue;if(_movement.TryStart(order,order.PathResult,_control.MercenarySession,_query!,_control.Movements,out string reason)){if(order.PathResult.Path.Count==0){_control.Commands.FinishOrder(order,true,string.Empty);_control.Diagnostics.CompletedMovementCount++;if(_control.MercenarySession.Registry.TryGetState(order.MercenaryId,out MercenaryStateV3? state)&&state!=null&&_views!.TryGetView(order.MercenaryId,out MercenaryEntityV3? view)&&view!=null)view.Position=_grid!.CellToWorldCenter(state.CurrentCell.Value);}else order.Status=MercenaryMoveOrderStatusV3.Moving;}else if(reason!="Previous segment is still active."){_control.Commands.FinishOrder(order,false,reason);_control.Diagnostics.FailedMovementCount++;}}}
    private void StartReadyExternalMovements(){foreach(MercenaryMovementRequestV3 request in _control!.ExternalMovements.GetActive()){if(request.Status!=MercenaryMovementRequestStatusV3.PathReady||request.PathResult==null||_control.Movements.TryGet(request.MercenaryId,out _))continue;if(_movement.TryStart(request,request.PathResult,_control.MercenarySession,_query!,_control.Movements,out string reason)){if(request.PathResult.Path.Count==0){_control.ExternalMovements.Complete(request,true,string.Empty);_control.Diagnostics.CompletedMovementCount++;}else request.Status=MercenaryMovementRequestStatusV3.Moving;}else if(reason!="Previous segment is still active."){_control.ExternalMovements.Complete(request,false,reason);_control.Diagnostics.FailedMovementCount++;}}}
    private void CompleteExternalMovements(){foreach(string id in _movement.CompletedThisTick){if(_control!.ExternalMovements.TryGetActive(id,out MercenaryMovementRequestV3? request)&&request!=null&&request.Status==MercenaryMovementRequestStatusV3.Moving&&_control.MercenarySession.Registry.TryGetState(id,out MercenaryStateV3? state)&&state!=null&&state.CurrentCell.Value==request.DestinationCell.Value){_control.ExternalMovements.Complete(request,true,string.Empty);_control.Diagnostics.CompletedMovementCount++;}}}
    private void FailDynamicallyBlockedMovements(){foreach(string id in _movement.BlockedThisTick){if(_control!.ExternalMovements.TryGetActive(id,out MercenaryMovementRequestV3? request)&&request!=null)_control.ExternalMovements.Complete(request,false,"DynamicObstacle");if(_control.Commands.TryGetActiveOrder(id,out MercenaryMoveOrderV3? order)&&order!=null)_control.Commands.FinishOrder(order,false,"DynamicObstacle");_control.MercenarySession.Registry.TryGetState(id,out MercenaryStateV3? state);state?.TrySetActivityState(MercenaryActivityStateV3.Idle,out _);_control.Diagnostics.FailedMovementCount++;}}
    private void UpdateMovingViews(){foreach(string id in _control!.Movements.GetActiveIds())if(_control.Movements.TryGet(id,out MercenaryMovementStateV3? moving)&&moving!=null&&_views!.TryGetView(id,out MercenaryEntityV3? view)&&view!=null)view.Position=Interpolate(moving);}
    private void SnapCompletedViews(){bool finalCompletion=false;foreach(string id in _movement.CompletedThisTick){if(_control!.MercenarySession.Registry.TryGetState(id,out MercenaryStateV3? state)&&state!=null){if(_views!.TryGetView(id,out MercenaryEntityV3? view)&&view!=null)view.Position=_grid!.CellToWorldCenter(state.CurrentCell.Value);if(!_control.Commands.TryGetActiveOrder(id,out _)){finalCompletion=true;GD.Print($"[MercenaryControlV3] Movement completed mercenary={id} cell={state.CurrentCell}");}}}if(finalCompletion)_manager?.UpdateDebugHud("Movement completed.");}
    private Vector2 Interpolate(MercenaryMovementStateV3 m)=>_grid!.CellToWorldCenter(m.FromCell.Value).Lerp(_grid.CellToWorldCenter(m.ToCell.Value),m.SegmentProgress01);
    public int PendingPathRequestCount=>_scheduler.PendingCount;public int ActivePathSearchCount=>_scheduler.ActiveCount;private long CurrentOccupancyRevision()=>MercenaryNavigationRevisionPolicyV3.GetRevision(_query!);
}
