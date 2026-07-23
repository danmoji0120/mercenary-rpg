using System;
using System.Collections.Generic;
using System.Linq;
using GameplayV3.Control;
using GameplayV3.Mercenary;
using GameplayV3.Resources;
using GameplayV3.Session;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Network;

public sealed class ServerCommandDeltaDiagnosticsV3
{
    public int GameplayCommandReceivedCount{get;internal set;}
    public int GameplayCommandAcceptedCount{get;internal set;}
    public int GameplayCommandRejectedCount{get;internal set;}
    public int DuplicateCommandReplayCount{get;internal set;}
    public int CommandReexecutionCount{get;internal set;}
    public int DeltaBatchSentCount{get;internal set;}
    public int DeltaEventSentCount{get;internal set;}
    public int UnauthorizedCommandAcceptedCount{get;internal set;}
    public int StaleSessionCommandAcceptedCount{get;internal set;}
    public int OwnMercenaryCommandAcceptedCount{get;internal set;}
    public int ForeignMercenaryCommandRejectedCount{get;internal set;}
    public int RegionDeltaFanoutCount{get;internal set;}
    public int ConcurrentGatherRequestCount{get;internal set;}
    public int ConcurrentGatherDuplicateWorkCount{get;internal set;}
    public int CrossRegionCommandExecutionCount{get;internal set;}
    public int CrossRegionDeltaLeakCount{get;internal set;}
}

internal sealed class ServerGameplayCommandGatewayV3
{
    private sealed class RegionDirtyState
    {
        public readonly HashSet<string> MercenaryPositions=new(StringComparer.Ordinal);
        public readonly HashSet<string> MercenaryOrders=new(StringComparer.Ordinal);
        public readonly HashSet<string> Nodes=new(StringComparer.Ordinal);
        public readonly HashSet<string> Stacks=new(StringComparer.Ordinal);
        public readonly HashSet<string> RemovedNodes=new(StringComparer.Ordinal);
        public readonly HashSet<string> RemovedStacks=new(StringComparer.Ordinal);
        public readonly HashSet<string> KnownStacks=new(StringComparer.Ordinal);
        public readonly HashSet<string> AddedMercenaries=new(StringComparer.Ordinal);
        public readonly HashSet<string> RemovedMercenaries=new(StringComparer.Ordinal);
    }

    private readonly DedicatedServerHostV3 _host;
    private readonly RegionSessionManagerV3 _sessions;
    private readonly Action<int,NetworkMessageV3> _send;
    private readonly RegionAccessPolicyV3 _regionAccess=new();
    private readonly RegionSnapshotBuilderV3 _snapshotBuilder=new();
    private readonly Dictionary<string,RegionDirtyState> _dirtyByRegion=new(StringComparer.Ordinal);

    public ServerGameplayCommandGatewayV3(DedicatedServerHostV3 host,RegionSessionManagerV3 sessions,Action<int,NetworkMessageV3> send){_host=host;_sessions=sessions;_send=send;}
    public ServerCommandDeltaDiagnosticsV3 Diagnostics{get;}=new();
    public long ServerTick=>_sessions.ServerTick;

    public void Handle(NetworkConnectionStateDataV3 connection,NetworkMessageV3 message)
    {
        Diagnostics.GameplayCommandReceivedCount++;
        if(connection.TryGetCommandResult(message.ClientCommandSequence,message.RequestId,out NetworkMessageV3? cached)&&cached!=null)
        {Diagnostics.DuplicateCommandReplayCount++;_send(connection.PeerId,cached);return;}
        if(connection.HasCommandSequence(message.ClientCommandSequence)){ReplyAndCache(connection,message,Reject(connection,message,NetworkRejectReasonV3.DuplicateCommand),false);return;}
        if(message.ClientCommandSequence<1||message.ClientCommandSequence<=connection.LastClientCommandSequence){ReplyAndCache(connection,message,Reject(connection,message,NetworkRejectReasonV3.CommandSequenceStale),false);return;}
        NetworkRejectReasonV3 validation=ValidateConnection(connection,message,out ManagedRegionSessionV3? managed);
        if(validation!=NetworkRejectReasonV3.None||managed==null){ReplyAndCache(connection,message,Reject(connection,message,validation),false);return;}
        if(!GameplayCommandDeltaProtocolV3.TryDeserializeCommand(message.CommandPayload,out GameplayCommandPayloadV3? payload)||payload==null)
        {ReplyAndCache(connection,message,Reject(connection,message,NetworkRejectReasonV3.InvalidTarget),false);return;}
        if(!TryGetCommandContext(connection,managed,payload,out PlayerCompanyStateV3? company,out MercenaryStateV3? mercenary,out NetworkRejectReasonV3 failure)||company==null||mercenary==null)
        {ReplyAndCache(connection,message,Reject(connection,message,failure),false);return;}
        RegionDirtyState dirty=EnsureTracker(managed);
        ActiveRegionSessionV3 active=managed.Active;
        string serverCommandId;
        bool accepted=payload.CommandKind switch{
            GameplayCommandKindV3.MoveMercenary=>TryMove(managed,company,payload,out serverCommandId,out failure),
            GameplayCommandKindV3.DirectGather=>TryGather(managed,company,payload,out serverCommandId,out failure),
            GameplayCommandKindV3.CancelMercenaryOrder=>TryCancel(active,payload,out serverCommandId,out failure),
            _=>Fail(out serverCommandId,out failure)};
        if(!accepted){ReplyAndCache(connection,message,Reject(connection,message,failure),false);return;}
        if(connection.CurrentRegionId!=active.RegionId){Diagnostics.CrossRegionCommandExecutionCount++;ReplyAndCache(connection,message,Reject(connection,message,NetworkRejectReasonV3.WrongRegion),false);return;}
        dirty.MercenaryOrders.Add(payload.MercenaryId);
        NetworkMessageV3 response=new(){MessageType=NetworkMessageTypeV3.CommandAccepted,RequestId=message.RequestId,ClientCommandSequence=message.ClientCommandSequence,
            ServerCommandId=serverCommandId,RegionId=active.RegionId,ActiveSessionRevision=active.SessionRevision,AcceptedServerTick=ServerTick};
        ReplyAndCache(connection,message,response,true);Diagnostics.OwnMercenaryCommandAcceptedCount++;
    }

    public void FlushDeltas()
    {
        foreach(string regionId in _dirtyByRegion.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray())
        {
            if(!_sessions.TryGetActiveRegion(regionId,out ManagedRegionSessionV3? managed)||managed==null){_dirtyByRegion.Remove(regionId);continue;}
            FlushRegion(managed,_dirtyByRegion[regionId]);
        }
    }

    public void ForgetRegion(string regionId)=>_dirtyByRegion.Remove(regionId);
    public void PublishMercenaryRemoved(string regionId,string mercenaryId)
    {
        if(!_dirtyByRegion.TryGetValue(regionId,out RegionDirtyState? dirty))dirty=EnsureTrackerForRegion(regionId);
        if(dirty==null)return;dirty.AddedMercenaries.Remove(mercenaryId);dirty.RemovedMercenaries.Add(mercenaryId);
    }
    public void PublishMercenaryAdded(string regionId,string mercenaryId)
    {
        if(!_dirtyByRegion.TryGetValue(regionId,out RegionDirtyState? dirty))dirty=EnsureTrackerForRegion(regionId);
        if(dirty==null)return;dirty.RemovedMercenaries.Remove(mercenaryId);dirty.AddedMercenaries.Add(mercenaryId);
    }

    private NetworkRejectReasonV3 ValidateConnection(NetworkConnectionStateDataV3 connection,NetworkMessageV3 message,out ManagedRegionSessionV3? managed)
    {
        managed=null;
        if(connection.State<NetworkConnectionStateV3.Handshaken)return NetworkRejectReasonV3.HandshakeRequired;
        if(connection.State!=NetworkConnectionStateV3.JoinedRegion)return NetworkRejectReasonV3.RegionJoinRequired;
        if(!connection.InitialSnapshotApproved)return NetworkRejectReasonV3.InitialSnapshotRequired;
        if(message.RegionId!=connection.CurrentRegionId)return NetworkRejectReasonV3.WrongRegion;
        if(!_sessions.TryGetActiveRegion(connection.CurrentRegionId,out managed)||managed==null||managed.Active.IsDisposed||message.ExpectedSessionRevision!=managed.Active.SessionRevision)return NetworkRejectReasonV3.StaleSession;
        return NetworkRejectReasonV3.None;
    }

    private bool TryGetCommandContext(NetworkConnectionStateDataV3 connection,ManagedRegionSessionV3 managed,GameplayCommandPayloadV3 payload,out PlayerCompanyStateV3? company,out MercenaryStateV3? mercenary,out NetworkRejectReasonV3 failure)
    {
        company=null;mercenary=null;failure=NetworkRejectReasonV3.None;
        if(_host.World==null||!_host.World.TryGetCompany(connection.CompanyId,out company)||company==null){failure=NetworkRejectReasonV3.MercenaryNotOwned;return false;}
        if(!managed.Active.CompanyState.MercenaryProfiles.TryGetState(payload.MercenaryId,out mercenary)||mercenary==null){failure=NetworkRejectReasonV3.MercenaryNotFound;return false;}
        if(mercenary.CompanyId!=connection.CompanyId){Diagnostics.ForeignMercenaryCommandRejectedCount++;failure=NetworkRejectReasonV3.MercenaryNotOwned;return false;}
        if(!_regionAccess.CanCommandMercenary(company,managed.Active.PersistentState,payload.MercenaryId)){failure=NetworkRejectReasonV3.MercenaryNotPresent;return false;}
        if(!managed.ContainsRuntimeMercenary(payload.MercenaryId)&&!managed.AddRuntimeMercenary(payload.MercenaryId)){failure=NetworkRejectReasonV3.MercenaryNotPresent;return false;}
        return true;
    }

    private bool TryMove(ManagedRegionSessionV3 managed,PlayerCompanyStateV3 company,GameplayCommandPayloadV3 payload,out string commandId,out NetworkRejectReasonV3 failure)
    {
        commandId=string.Empty;failure=NetworkRejectReasonV3.InvalidTarget;if(payload.TargetCell==null)return false;
        GlobalCellCoord cell=new(new Vector2I(payload.TargetCell.X,payload.TargetCell.Y));var query=managed.CommandRuntime.NavigationQuery;
        if(!query.IsInsideWorld(cell.Value)||!query.IsWalkable(cell.Value))return false;
        ActiveRegionSessionV3 active=managed.Active;
        if(!active.Control.TryIssueDirectMove(company.PlayerAccountId,company.CompanyId,new[]{payload.MercenaryId},cell,query,active.SessionRevision,out DirectMoveCommandV3? command,out _ )||command==null)return false;
        commandId=command.CommandId;failure=NetworkRejectReasonV3.None;return true;
    }

    private bool TryGather(ManagedRegionSessionV3 managed,PlayerCompanyStateV3 company,GameplayCommandPayloadV3 payload,out string commandId,out NetworkRejectReasonV3 failure)
    {
        commandId=string.Empty;ActiveRegionSessionV3 active=managed.Active;
        if(!active.Resources.Nodes.TryGet(payload.ResourceNodeId,out ResourceNodeStateV3? node)||node==null){failure=NetworkRejectReasonV3.TargetNotFound;return false;}
        if(node.IsDepleted){failure=NetworkRejectReasonV3.ResourceDepleted;return false;}
        bool wasReserved=active.Work.Reservations.IsReserved(payload.ResourceNodeId);if(wasReserved)Diagnostics.ConcurrentGatherRequestCount++;
        if(!active.Work.TryIssueGathering(company.PlayerAccountId,company.CompanyId,new[]{payload.MercenaryId},payload.ResourceNodeId,managed.CommandRuntime.NavigationQuery,active.SessionRevision,out WorkRequestV3? request,out string reason)||request==null)
        {failure=reason=="ResourceDepleted"?NetworkRejectReasonV3.ResourceDepleted:NetworkRejectReasonV3.InvalidTarget;return false;}
        if(wasReserved)Diagnostics.ConcurrentGatherDuplicateWorkCount++;
        commandId=request.WorkRequestId;failure=NetworkRejectReasonV3.None;return true;
    }

    private bool TryCancel(ActiveRegionSessionV3 active,GameplayCommandPayloadV3 payload,out string commandId,out NetworkRejectReasonV3 failure)
    {active.Control.CancelCurrentActivity(payload.MercenaryId);commandId=$"cancel_{active.SessionRevision:x}_{ServerTick:x}_{payload.MercenaryId}";failure=NetworkRejectReasonV3.None;return true;}
    private static bool Fail(out string commandId,out NetworkRejectReasonV3 failure){commandId=string.Empty;failure=NetworkRejectReasonV3.InvalidTarget;return false;}

    private RegionDirtyState EnsureTracker(ManagedRegionSessionV3 managed)
    {
        string regionId=managed.Active.RegionId;
        if(_dirtyByRegion.TryGetValue(regionId,out RegionDirtyState? dirty))return dirty;
        dirty=new();_dirtyByRegion.Add(regionId,dirty);
        foreach(string id in managed.Active.Resources.GroundStacks.GetAllStackIds())dirty.KnownStacks.Add(id);
        managed.CommandRuntime.MercenaryChanged+=id=>dirty.MercenaryPositions.Add(id);
        managed.CommandRuntime.MercenaryOrderChanged+=id=>dirty.MercenaryOrders.Add(id);
        managed.CommandRuntime.ResourceNodeChanged+=id=>dirty.Nodes.Add(id);
        managed.CommandRuntime.GroundStackChanged+=id=>dirty.Stacks.Add(id);
        return dirty;
    }
    private RegionDirtyState? EnsureTrackerForRegion(string regionId)=>_sessions.TryGetActiveRegion(regionId,out ManagedRegionSessionV3? managed)&&managed!=null?EnsureTracker(managed):null;

    private void FlushRegion(ManagedRegionSessionV3 managed,RegionDirtyState dirty)
    {
        ActiveRegionSessionV3 active=managed.Active;List<RegionDeltaEventV3> commonEvents=new();
        foreach(string id in dirty.MercenaryPositions.OrderBy(x=>x,StringComparer.Ordinal))
            if(active.CompanyState.MercenaryProfiles.TryGetState(id,out MercenaryStateV3? state)&&state!=null)commonEvents.Add(new(){EventKind=RegionDeltaEventKindV3.MercenaryPositionChanged,EntityId=id,Cell=new(state.CurrentCell.Value.X,state.CurrentCell.Value.Y)});
        foreach(string id in dirty.MercenaryOrders.OrderBy(x=>x,StringComparer.Ordinal))
            if(active.CompanyState.MercenaryProfiles.TryGetState(id,out MercenaryStateV3? state)&&state!=null)commonEvents.Add(new(){EventKind=RegionDeltaEventKindV3.MercenaryOrderStateChanged,EntityId=id,ActivityState=state.ActivityState});
        foreach(string id in dirty.Nodes.OrderBy(x=>x,StringComparer.Ordinal))
            if(active.Resources.Nodes.TryGet(id,out ResourceNodeStateV3? node)&&node!=null)commonEvents.Add(new(){EventKind=RegionDeltaEventKindV3.ResourceNodeChanged,EntityId=id,ResourceNode=NodeDto(node)});
        foreach(string id in dirty.RemovedNodes.OrderBy(x=>x,StringComparer.Ordinal))commonEvents.Add(new(){EventKind=RegionDeltaEventKindV3.ResourceNodeRemoved,EntityId=id});
        foreach(string id in dirty.Stacks.OrderBy(x=>x,StringComparer.Ordinal))
            if(active.Resources.GroundStacks.TryGet(id,out GroundResourceStackV3? stack)&&stack!=null)
            {bool known=dirty.KnownStacks.Contains(id);commonEvents.Add(new(){EventKind=known?RegionDeltaEventKindV3.GroundResourceStackChanged:RegionDeltaEventKindV3.GroundResourceStackAdded,EntityId=id,GroundResourceStack=StackDto(stack)});dirty.KnownStacks.Add(id);}
        foreach(string id in dirty.RemovedStacks.OrderBy(x=>x,StringComparer.Ordinal))commonEvents.Add(new(){EventKind=RegionDeltaEventKindV3.GroundResourceStackRemoved,EntityId=id});
        if(commonEvents.Count==0&&dirty.AddedMercenaries.Count==0&&dirty.RemovedMercenaries.Count==0){Clear(dirty);return;}
        foreach(NetworkConnectionStateDataV3 connection in _host.Connections.GetJoinedConnections(active.RegionId))
        {
            if(!connection.InitialSnapshotApproved)continue;
            if(connection.CurrentRegionId!=active.RegionId){Diagnostics.CrossRegionDeltaLeakCount++;continue;}
            if(_host.World==null||!_host.World.TryGetCompany(connection.CompanyId,out PlayerCompanyStateV3? recipient)||recipient==null)continue;
            List<RegionDeltaEventV3> events=new(commonEvents);
            foreach(string id in dirty.RemovedMercenaries.OrderBy(x=>x,StringComparer.Ordinal))events.Add(new(){EventKind=RegionDeltaEventKindV3.MercenaryReplicaRemoved,EntityId=id});
            foreach(string id in dirty.AddedMercenaries.OrderBy(x=>x,StringComparer.Ordinal))
                if(_snapshotBuilder.TryBuildMercenaryDelta(_host.World,active.PersistentState,recipient,id,out MercenarySnapshotDtoV3? mercenary,out EquipmentSnapshotDtoV3[] equipment)&&mercenary!=null)
                    events.Add(new(){EventKind=RegionDeltaEventKindV3.MercenaryReplicaAdded,EntityId=id,Mercenary=mercenary,Equipment=equipment});
            if(events.Count==0)continue;
            string payload=GameplayCommandDeltaProtocolV3.SerializeDelta(new(){Events=events.ToArray()});
            long sequence=++connection.LastDeltaSequence;
            _send(connection.PeerId,new(){MessageType=NetworkMessageTypeV3.RegionDeltaBatch,RegionId=active.RegionId,ActiveSessionRevision=active.SessionRevision,
                DeltaSequence=sequence,ServerTick=ServerTick,RegionRevision=active.PersistentState.RegionRevision,DeltaPayload=payload});
            Diagnostics.DeltaBatchSentCount++;Diagnostics.DeltaEventSentCount+=events.Count;Diagnostics.RegionDeltaFanoutCount++;
        }
        Clear(dirty);
    }

    private static void Clear(RegionDirtyState dirty){dirty.MercenaryPositions.Clear();dirty.MercenaryOrders.Clear();dirty.Nodes.Clear();dirty.Stacks.Clear();dirty.RemovedNodes.Clear();dirty.RemovedStacks.Clear();dirty.AddedMercenaries.Clear();dirty.RemovedMercenaries.Clear();}
    private void ReplyAndCache(NetworkConnectionStateDataV3 connection,NetworkMessageV3 request,NetworkMessageV3 response,bool accepted)
    {connection.CacheCommandResult(request.ClientCommandSequence,request.RequestId,response);connection.LastApprovedRequestId=Math.Max(connection.LastApprovedRequestId,request.RequestId);if(accepted)Diagnostics.GameplayCommandAcceptedCount++;else Diagnostics.GameplayCommandRejectedCount++;_send(connection.PeerId,response);}
    private NetworkMessageV3 Reject(NetworkConnectionStateDataV3 connection,NetworkMessageV3 request,NetworkRejectReasonV3 reason)
    {
        long revision=_sessions.TryGetActiveRegion(connection.CurrentRegionId,out ManagedRegionSessionV3? managed)&&managed!=null?managed.Active.SessionRevision:0;
        return new(){MessageType=NetworkMessageTypeV3.CommandRejected,RequestId=request.RequestId,ClientCommandSequence=request.ClientCommandSequence,RejectReason=reason,RegionId=connection.CurrentRegionId,ActiveSessionRevision=revision};
    }
    private static ResourceNodeSnapshotDtoV3 NodeDto(ResourceNodeStateV3 node){NaturalResourceDefinitionCatalogV3.TryGet(node.NodeType,out NaturalResourceDefinitionV3? definition);return new(node.ResourceNodeId,definition?.DefinitionId??node.NodeType.ToString(),node.NodeType,node.ProducedResourceType,new(node.Cell.Value.X,node.Cell.Value.Y),node.RemainingAmount,node.MaxAmount);}
    private static GroundResourceStackSnapshotDtoV3 StackDto(GroundResourceStackV3 stack)=>new(stack.ResourceStackId,stack.ResourceType,new(stack.Cell.Value.X,stack.Cell.Value.Y),stack.Amount);
}
