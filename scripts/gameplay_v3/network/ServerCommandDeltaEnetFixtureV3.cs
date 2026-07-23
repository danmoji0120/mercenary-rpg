using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameplayV3.Company;
using GameplayV3.Equipment;
using GameplayV3.Mercenary;
using GameplayV3.Resources;
using GameplayV3.Session;
using Godot;
using WorldV2;

namespace GameplayV3.Network;

public partial class ServerCommandDeltaEnetFixtureV3:Node
{
    private static readonly Rect2I Bounds=new(0,0,64,64);
    private DedicatedServerHostV3? _host;
    private TestClient? _client;
    private NetworkClientSessionV3? _replicaSession;
    private NetworkMessageV3? _hello,_join,_snapshot,_lastDelta;

    public override async void _Ready()
    {
        bool pass;string summary;
        try{summary=await RunFixture();pass=true;}
        catch(Exception exception){pass=false;summary=exception.ToString();}
        finally{_client?.Dispose();_host?.Dispose();}
        GD.Print($"[ServerCommandDeltaEnetV3] PASS={pass} {summary}");
        GetTree().Quit(pass?0:3);
    }

    private async Task<string> RunFixture()
    {
        _host=new();int port=await Start();
        PersistentWorldStateV3 world=_host.World!;
        PlayerCompanyStateV3 company=_host.DefaultCompany!;
        RegionPersistentStateV3 region=world.Regions[PersistentWorldStateV3.InitialEstateRegionId];
        _client=await Connect(port);_replicaSession=new();
        (string mercenaryId,string wrongOwnerMercenaryId,string nodeId)=Populate(company,region);

        Send(new(){MessageType=NetworkMessageTypeV3.ClientHello,RequestId=1,DevelopmentPlayerAccountId="command_delta_fixture"});
        _hello=await WaitFor(NetworkMessageTypeV3.ServerHelloAccepted);Check(_replicaSession.ApplyServerHello(_hello),"Client hello apply failed.");
        Send(new(){MessageType=NetworkMessageTypeV3.JoinRegionRequest,RequestId=2,RegionId=region.RegionId});
        _join=await WaitFor(NetworkMessageTypeV3.JoinRegionAccepted);Check(_replicaSession.ApplyRegionJoin(_join),"Client join apply failed.");

        Send(Command(3,1,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercenaryId,TargetCell=new(6,5)}));
        Check((await WaitFor(NetworkMessageTypeV3.CommandRejected)).RejectReason==NetworkRejectReasonV3.InitialSnapshotRequired,"Pre-snapshot command was accepted.");

        Send(new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=4,RegionId=region.RegionId,ExpectedSessionRevision=_join.ActiveSessionRevision});
        _snapshot=await WaitFor(NetworkMessageTypeV3.InitialRegionSnapshotAccepted);Check(_replicaSession.TryApplyInitialSnapshot(_snapshot,out _),"Initial replica apply failed.");
        Check(_replicaSession.CurrentRegionReplica!.ResourceNodes[nodeId].RemainingAmount==2,"Initial node amount mismatch.");

        Send(Command(5,2,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercenaryId,TargetCell=new(6,5)}));
        NetworkMessageV3 moveAccepted=await WaitFor(NetworkMessageTypeV3.CommandAccepted);Check(moveAccepted.ServerCommandId.Length>0,"Move command id missing.");
        await PumpUntilReplica(()=>company.MercenaryProfiles.TryGetState(mercenaryId,out MercenaryStateV3? state)&&state?.CurrentCell.Value==new Vector2I(6,5)&&_replicaSession.CurrentRegionReplica!.Mercenaries[mercenaryId].Cell==new SnapshotCellV3(6,5));

        Send(Command(6,3,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.DirectGather,MercenaryId=mercenaryId,ResourceNodeId=nodeId}));
        NetworkMessageV3 gatherAccepted=await WaitFor(NetworkMessageTypeV3.CommandAccepted);
        await PumpUntilReplica(()=>region.ResourceNodes.TryGet(nodeId,out ResourceNodeStateV3? node)&&node?.RemainingAmount==0&&region.GroundResourceStacks.Count==1&&
            _replicaSession.CurrentRegionReplica!.ResourceNodes[nodeId].RemainingAmount==0&&_replicaSession.CurrentRegionReplica.GroundResourceStacks.Count==1);
        int stackAmount=region.GroundResourceStacks.GetAllStackIds().Select(id=>region.GroundResourceStacks.TryGet(id,out GroundResourceStackV3? stack)?stack?.Amount??0:0).Sum();
        long deltaBeforeReplay=_replicaSession.CurrentDeltaSequence;
        Send(Command(6,3,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.DirectGather,MercenaryId=mercenaryId,ResourceNodeId=nodeId}));
        NetworkMessageV3 replay=await WaitFor(NetworkMessageTypeV3.CommandAccepted);
        Check(replay.ServerCommandId==gatherAccepted.ServerCommandId,"Duplicate command did not replay cached result.");
        await PumpFrames(8);DrainDeltas();
        Check(region.GroundResourceStacks.GetAllStackIds().Select(id=>region.GroundResourceStacks.TryGet(id,out GroundResourceStackV3? stack)?stack?.Amount??0:0).Sum()==stackAmount&&_replicaSession.CurrentDeltaSequence==deltaBeforeReplay,"Duplicate gather re-executed gameplay.");

        Send(Command(7,4,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercenaryId,TargetCell=new(20,5)}));
        await WaitFor(NetworkMessageTypeV3.CommandAccepted);
        int appliedBeforeCancel=_replicaSession.Diagnostics.DeltaBatchAppliedCount;
        Send(Command(8,5,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.CancelMercenaryOrder,MercenaryId=mercenaryId}));
        await WaitFor(NetworkMessageTypeV3.CommandAccepted);
        await PumpUntilReplica(()=>_replicaSession.Diagnostics.DeltaBatchAppliedCount>appliedBeforeCancel&&_replicaSession.CurrentRegionReplica!.Mercenaries[mercenaryId].ActivityState==MercenaryActivityStateV3.Idle);
        Check(_host.RegionSessions!.TryGetActiveRegion(region.RegionId,out ManagedRegionSessionV3? managed)&&managed!.Active.Work.ActiveReservationCount==0,"Cancel leaked gathering reservation.");

        await ExpectRejected(Command(9,6,region.RegionId,_join.ActiveSessionRevision-1,new(){CommandKind=GameplayCommandKindV3.CancelMercenaryOrder,MercenaryId=mercenaryId}),NetworkRejectReasonV3.StaleSession);
        await ExpectRejected(Command(10,7,"region_wrong_command",_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.CancelMercenaryOrder,MercenaryId=mercenaryId}),NetworkRejectReasonV3.WrongRegion);
        await ExpectRejected(Command(11,8,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.CancelMercenaryOrder,MercenaryId="merc_missing"}),NetworkRejectReasonV3.MercenaryNotFound);
        await ExpectRejected(Command(12,9,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.CancelMercenaryOrder,MercenaryId=wrongOwnerMercenaryId}),NetworkRejectReasonV3.MercenaryNotOwned);
        await ExpectRejected(Command(13,10,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.DirectGather,MercenaryId=mercenaryId,ResourceNodeId="rnode_missing"}),NetworkRejectReasonV3.TargetNotFound);
        await ExpectRejected(Command(14,11,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.DirectGather,MercenaryId=mercenaryId,ResourceNodeId=nodeId}),NetworkRejectReasonV3.ResourceDepleted);
        Check(company.SetMercenaryRegion(mercenaryId,"region_elsewhere"),"Presence mutation failed.");
        await ExpectRejected(Command(15,12,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.CancelMercenaryOrder,MercenaryId=mercenaryId}),NetworkRejectReasonV3.MercenaryNotPresent);
        Check(company.SetMercenaryRegion(mercenaryId,region.RegionId),"Presence restore failed.");
        await ExpectRejected(Command(16,0,region.RegionId,_join.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.CancelMercenaryOrder,MercenaryId=mercenaryId}),NetworkRejectReasonV3.CommandSequenceStale);

        ValidateDeltaFailures();
        ServerCommandDeltaDiagnosticsV3 diagnostics=_host.CommandDiagnostics!;
        Check(diagnostics.DuplicateCommandReplayCount==1&&diagnostics.CommandReexecutionCount==0&&diagnostics.UnauthorizedCommandAcceptedCount==0&&diagnostics.StaleSessionCommandAcceptedCount==0,"Command safety diagnostics mismatch.");
        Check(diagnostics.DeltaBatchSentCount>0&&diagnostics.DeltaEventSentCount>0&&_replicaSession.Diagnostics.PartialDeltaApplyCount==0,"Delta diagnostics mismatch.");
        int commandCacheCount=_host.Connections.Connections.Single().CachedCommandResultCount;
        Send(new(){MessageType=NetworkMessageTypeV3.ClientDisconnect,RequestId=long.MaxValue});
        await PumpUntil(()=>_host.Connections.Count==0);
        Check(commandCacheCount>0&&_host.Connections.Count==0,"Disconnect did not clear connection command state.");
        return $"port={port} commands={diagnostics.GameplayCommandAcceptedCount}/{diagnostics.GameplayCommandRejectedCount} replay/reexecute={diagnostics.DuplicateCommandReplayCount}/{diagnostics.CommandReexecutionCount} deltas={diagnostics.DeltaBatchSentCount}/{diagnostics.DeltaEventSentCount} finalSequence={_replicaSession.CurrentDeltaSequence} stack={stackAmount} partial=0 unauthorized=0 staleAccepted=0";
    }

    private static (string MercenaryId,string WrongOwnerMercenaryId,string NodeId) Populate(PlayerCompanyStateV3 company,RegionPersistentStateV3 region)
    {
        MercenaryAttributeSetV3.TryCreate(10,10,10,10,10,out var attributes,out _);MercenaryWorkSkillSetV3.TryCreate(8,8,12,8,8,8,8,out var skills,out _);
        string mercenaryId=MercenaryIdFactoryV3.CreateMercenaryId();DateTime now=DateTime.UtcNow;
        MercenaryProfileV3.TryCreate(mercenaryId,"Network Worker","placeholder",attributes,skills,now,out var profile,out _);
        MercenaryStateV3.TryCreate(mercenaryId,company.CompanyId,new(new Vector2I(2,5)),MercenaryActivityStateV3.Idle,now,out var state,out _);
        Check(company.MercenaryProfiles.TryRegisterMercenary(profile,state,out string reason),reason);Check(company.SetMercenaryRegion(mercenaryId,region.RegionId),"Mercenary presence failed.");
        if(!GameplaySessionV3.TryGetCompanySession(out CompanySessionV3? currentCompanySession)||currentCompanySession==null)throw new InvalidOperationException("Company session missing.");
        CompanySessionV3 companySession=currentCompanySession;
        CompanyStateV3 otherCompany=new(CompanyIdFactoryV3.CreateCompanyId(),CompanyIdFactoryV3.CreatePlayerId(),"Other Company",now);
        Check(companySession.CompanyRegistry.TryRegisterCompany(otherCompany,out reason),reason);
        string wrongOwnerMercenaryId=MercenaryIdFactoryV3.CreateMercenaryId();
        MercenaryProfileV3.TryCreate(wrongOwnerMercenaryId,"Other Worker","placeholder",attributes,skills,now,out var otherProfile,out _);
        MercenaryStateV3.TryCreate(wrongOwnerMercenaryId,otherCompany.CompanyId,new(new Vector2I(3,5)),MercenaryActivityStateV3.Idle,now,out var otherState,out _);
        Check(company.MercenaryProfiles.TryRegisterMercenary(otherProfile,otherState,out reason),reason);
        string nodeId=ResourceNodeIdFactoryV3.Create();Check(ResourceNodeStateV3.TryCreate(nodeId,ResourceNodeTypeV3.IronVein,new(new Vector2I(10,5)),2,2,2,Bounds,now,out var node,out reason)&&node!=null,reason);Check(region.ResourceNodes.TryRegister(node,out reason),reason);
        return(mercenaryId,wrongOwnerMercenaryId,nodeId);
    }

    private void ValidateDeltaFailures()
    {
        Check(_lastDelta!=null&&_snapshot!=null&&_hello!=null&&_join!=null,"No actual delta captured.");
        long sequence=_replicaSession!.CurrentDeltaSequence;Check(_replicaSession.TryApplyRegionDelta(_lastDelta!,out _)&&_replicaSession.CurrentDeltaSequence==sequence,"Duplicate delta was reapplied.");
        NetworkClientSessionV3 gap=FreshReplica();NetworkMessageV3 empty=_lastDelta! with{DeltaSequence=2,DeltaPayload=GameplayCommandDeltaProtocolV3.SerializeDelta(new())};Check(!gap.TryApplyRegionDelta(empty,out _)&&gap.NeedsResnapshot&&gap.Diagnostics.DeltaSequenceGapCount==1,"Delta gap was not detected.");
        NetworkClientSessionV3 wrong=FreshReplica();Check(!wrong.TryApplyRegionDelta(_lastDelta! with{DeltaSequence=1,RegionId="region_wrong"},out _),"Wrong-region delta was accepted.");
        NetworkClientSessionV3 stale=FreshReplica();Check(!stale.TryApplyRegionDelta(_lastDelta! with{DeltaSequence=1,ActiveSessionRevision=_join!.ActiveSessionRevision-1},out _),"Stale-session delta was accepted.");
        NetworkClientSessionV3 corrupt=FreshReplica();NetworkRegionReplicaV3 before=corrupt.CurrentRegionReplica!;
        RegionDeltaPayloadV3 bad=new(){Events=new[]{new RegionDeltaEventV3{EventKind=RegionDeltaEventKindV3.MercenaryPositionChanged,EntityId="merc_missing",Cell=new(1,1)}}};
        Check(!corrupt.TryApplyRegionDelta(_lastDelta! with{DeltaSequence=1,DeltaPayload=GameplayCommandDeltaProtocolV3.SerializeDelta(bad)},out _)&&ReferenceEquals(before,corrupt.CurrentRegionReplica)&&corrupt.Diagnostics.PartialDeltaApplyCount==0,"Corrupt delta partially applied.");
        NetworkClientSessionV3 removed=FreshReplica();string nodeId=removed.CurrentRegionReplica!.ResourceNodes.Keys.Single();
        RegionDeltaPayloadV3 removal=new(){Events=new[]{new RegionDeltaEventV3{EventKind=RegionDeltaEventKindV3.ResourceNodeRemoved,EntityId=nodeId}}};
        Check(removed.TryApplyRegionDelta(_lastDelta! with{DeltaSequence=1,DeltaPayload=GameplayCommandDeltaProtocolV3.SerializeDelta(removal)},out _)&&!removed.CurrentRegionReplica!.ResourceNodes.ContainsKey(nodeId),"Resource removal delta failed.");
    }
    private NetworkClientSessionV3 FreshReplica(){NetworkClientSessionV3 value=new();Check(value.ApplyServerHello(_hello!)&&value.ApplyRegionJoin(_join!)&&value.TryApplyInitialSnapshot(_snapshot!,out _),"Fresh replica setup failed.");return value;}
    private async Task ExpectRejected(NetworkMessageV3 message,NetworkRejectReasonV3 reason){Send(message);Check((await WaitFor(NetworkMessageTypeV3.CommandRejected)).RejectReason==reason,$"Expected rejection {reason}.");}
    private static NetworkMessageV3 Command(long request,long sequence,string region,long revision,GameplayCommandPayloadV3 payload)=>new(){MessageType=NetworkMessageTypeV3.SubmitGameplayCommand,RequestId=request,ClientCommandSequence=sequence,RegionId=region,ExpectedSessionRevision=revision,CommandPayload=GameplayCommandDeltaProtocolV3.SerializeCommand(payload)};
    private async Task<int> Start(){int first=31000+(int)(OS.GetProcessId()%1000);for(int i=0;i<24;i++){int port=first+i;if(_host!.TryStart(port,51305,out _))return port;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new InvalidOperationException("No ENet fixture port.");}
    private async Task<TestClient> Connect(int port){TestClient client=new(port);_client=client;await PumpUntil(()=>client.IsConnected);return client;}
    private async Task<NetworkMessageV3> WaitFor(NetworkMessageTypeV3 type){NetworkMessageV3? value=null;await PumpUntil(()=>_client!.TryTake(type,out value));return value!;}
    private async Task PumpUntilReplica(Func<bool> condition){await PumpUntil(()=>{DrainDeltas();return condition();});}
    private void DrainDeltas(){while(_client!.TryTake(NetworkMessageTypeV3.RegionDeltaBatch,out NetworkMessageV3? delta)&&delta!=null){Check(_replicaSession!.TryApplyRegionDelta(delta,out string reason),reason);_lastDelta=delta;}}
    private async Task PumpUntil(Func<bool> condition){for(int i=0;i<900;i++){_host!.Poll();_client?.Poll();if(condition())return;if(_client?.IsFailed==true)throw new InvalidOperationException("ENet client failed.");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new TimeoutException("Command delta fixture timed out.");}
    private async Task PumpFrames(int count){for(int i=0;i<count;i++){_host!.Poll();_client!.Poll();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
    private void Send(NetworkMessageV3 message){if(!_client!.Send(message))throw new InvalidOperationException("ENet send failed.");}
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}

    private sealed class TestClient:IDisposable
    {
        private readonly ENetMultiplayerPeer _peer=new();private readonly Queue<NetworkMessageV3> _messages=new();
        public TestClient(int port){Error error=_peer.CreateClient("127.0.0.1",port);if(error!=Error.Ok)throw new InvalidOperationException(error.ToString());}
        public bool IsConnected=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Connected;public bool IsFailed=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Disconnected;
        public void Poll(){if(IsFailed)return;_peer.Poll();while(_peer.GetAvailablePacketCount()>0)if(NetworkProtocolV3.TryDeserialize(_peer.GetPacket(),out var message)&&message!=null)_messages.Enqueue(message);}
        public bool Send(NetworkMessageV3 message){if(!IsConnected)return false;_peer.SetTargetPeer(1);return _peer.PutPacket(NetworkProtocolV3.Serialize(message))==Error.Ok;}
        public bool TryTake(NetworkMessageTypeV3 type,out NetworkMessageV3? message){message=null;int count=_messages.Count;for(int i=0;i<count;i++){NetworkMessageV3 current=_messages.Dequeue();if(message==null&&current.MessageType==type)message=current;else _messages.Enqueue(current);}return message!=null;}
        public void Dispose()=>_peer.Dispose();
    }
}
