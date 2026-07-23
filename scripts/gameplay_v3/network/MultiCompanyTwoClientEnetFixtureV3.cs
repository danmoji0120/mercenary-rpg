using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameplayV3.Mercenary;
using GameplayV3.Resources;
using GameplayV3.Session;
using Godot;
using WorldV2;

namespace GameplayV3.Network;

public partial class MultiCompanyTwoClientEnetFixtureV3:Node
{
    private static readonly Rect2I Bounds=new(0,0,64,64);
    private readonly List<TestClient> _clients=new();
    private DedicatedServerHostV3? _host;

    public override async void _Ready()
    {
        bool pass;string summary;
        try{summary=await RunFixture();pass=true;}
        catch(Exception exception){pass=false;summary=exception.ToString();}
        finally{foreach(TestClient client in _clients)client.Dispose();_clients.Clear();_host?.Dispose();}
        GD.Print($"[MultiCompanyTwoClientEnetV3] PASS={pass} {summary}");
        GetTree().Quit(pass?0:3);
    }

    private async Task<string> RunFixture()
    {
        _host=new();int port=await Start();
        TestClient clientA=await Connect(port);TestClient clientB=await Connect(port);
        NetworkClientSessionV3 replicaA=new(),replicaB=new();
        const string accountA="multi_company_account_a",accountB="multi_company_account_b";

        Send(clientA,Hello(1,accountA));NetworkMessageV3 helloA=await WaitFor(clientA,NetworkMessageTypeV3.ServerHelloAccepted);Check(replicaA.ApplyServerHello(helloA),"A hello apply failed.");
        Send(clientB,Hello(1,accountB));NetworkMessageV3 helloB=await WaitFor(clientB,NetworkMessageTypeV3.ServerHelloAccepted);Check(replicaB.ApplyServerHello(helloB),"B hello apply failed.");
        Check(helloA.CompanyId!=helloB.CompanyId,"Accounts received the same company.");
        if(!_host.TryGetDevelopmentCompany(accountA,out PlayerCompanyStateV3? companyA)||companyA==null)throw new InvalidOperationException("A company binding missing.");
        if(!_host.TryGetDevelopmentCompany(accountB,out PlayerCompanyStateV3? companyB)||companyB==null)throw new InvalidOperationException("B company binding missing.");
        string estateA=_host.GetEstateRegionId(companyA),estateB=_host.GetEstateRegionId(companyB);
        Check(estateA!=estateB,"Companies received the same estate.");

        Send(clientA,Join(2,estateA));NetworkMessageV3 joinAEstate=await WaitFor(clientA,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaA.ApplyRegionJoin(joinAEstate),"A estate join apply failed.");
        Send(clientB,Join(2,estateA));Check((await WaitFor(clientB,NetworkMessageTypeV3.JoinRegionRejected)).RejectReason==NetworkRejectReasonV3.RegionAccessDenied,"B entered A estate.");
        Send(clientB,Join(3,estateB));NetworkMessageV3 joinBEstate=await WaitFor(clientB,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaB.ApplyRegionJoin(joinBEstate),"B estate join apply failed.");
        Send(clientA,Join(3,estateB));Check((await WaitFor(clientA,NetworkMessageTypeV3.JoinRegionRejected)).RejectReason==NetworkRejectReasonV3.RegionAccessDenied,"A entered B estate.");

        RegionPersistentStateV3 shared=_host.CreateOrGetSharedNeutralRegion();
        Send(clientA,Join(4,shared.RegionId));NetworkMessageV3 joinA=await WaitFor(clientA,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaA.ApplyRegionJoin(joinA),"A shared join apply failed.");
        Send(clientB,Join(4,shared.RegionId));NetworkMessageV3 joinB=await WaitFor(clientB,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaB.ApplyRegionJoin(joinB),"B shared join apply failed.");
        Check(joinA.ActiveSessionRevision==joinB.ActiveSessionRevision&&_host.Connections.GetRegionConnectionCount(shared.RegionId)==2,"Shared membership mismatch.");

        string mercA=AddMercenary(companyA,shared.RegionId,new(2,6),"Worker A",12);
        string mercB=AddMercenary(companyB,shared.RegionId,new(2,10),"Worker B",9);
        string nodeId=AddNode(shared,new(12,8));

        Send(clientA,Snapshot(5,shared.RegionId,joinA.ActiveSessionRevision));NetworkMessageV3 snapshotA=await WaitFor(clientA,NetworkMessageTypeV3.InitialRegionSnapshotAccepted);Check(replicaA.TryApplyInitialSnapshot(snapshotA,out NetworkRejectReasonV3 reasonA),reasonA.ToString());
        Send(clientB,Snapshot(5,shared.RegionId,joinB.ActiveSessionRevision));NetworkMessageV3 snapshotB=await WaitFor(clientB,NetworkMessageTypeV3.InitialRegionSnapshotAccepted);Check(replicaB.TryApplyInitialSnapshot(snapshotB,out NetworkRejectReasonV3 reasonB),reasonB.ToString());
        ValidateVisibility(replicaA,mercA,mercB);ValidateVisibility(replicaB,mercB,mercA);
        Check(!ReferenceEquals(replicaA.CurrentRegionReplica,replicaB.CurrentRegionReplica),"Client replicas share an object.");

        NetworkMessageV3 spoofedA=Command(6,1,shared.RegionId,joinA.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercA,TargetCell=new(6,6)}) with{CompanyId=helloB.CompanyId};
        Send(clientA,spoofedA);await Expect(clientA,NetworkMessageTypeV3.CommandAccepted);
        await PumpReplicas(replicaA,replicaB,()=>Cell(replicaA,mercA)==new SnapshotCellV3(6,6)&&Cell(replicaB,mercA)==new SnapshotCellV3(6,6));

        Send(clientB,Command(6,1,shared.RegionId,joinB.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercA,TargetCell=new(7,6)}));
        Check((await WaitFor(clientB,NetworkMessageTypeV3.CommandRejected)).RejectReason==NetworkRejectReasonV3.MercenaryNotOwned,"B controlled A mercenary.");
        Send(clientB,Command(7,2,shared.RegionId,joinB.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercB,TargetCell=new(6,10)}));
        await Expect(clientB,NetworkMessageTypeV3.CommandAccepted);
        await PumpReplicas(replicaA,replicaB,()=>Cell(replicaA,mercB)==new SnapshotCellV3(6,10)&&Cell(replicaB,mercB)==new SnapshotCellV3(6,10));

        Send(clientA,Command(7,2,shared.RegionId,joinA.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.DirectGather,MercenaryId=mercA,ResourceNodeId=nodeId}));
        Send(clientB,Command(8,3,shared.RegionId,joinB.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.DirectGather,MercenaryId=mercB,ResourceNodeId=nodeId}));
        NetworkMessageV3 gatherA=await WaitForEither(clientA,NetworkMessageTypeV3.CommandAccepted,NetworkMessageTypeV3.CommandRejected);
        NetworkMessageV3 gatherB=await WaitForEither(clientB,NetworkMessageTypeV3.CommandAccepted,NetworkMessageTypeV3.CommandRejected);
        Check((gatherA.MessageType==NetworkMessageTypeV3.CommandAccepted?1:0)+(gatherB.MessageType==NetworkMessageTypeV3.CommandAccepted?1:0)==1,"Concurrent gather did not resolve to one work owner.");
        await PumpReplicas(replicaA,replicaB,()=>shared.ResourceNodes.TryGet(nodeId,out ResourceNodeStateV3? node)&&node?.RemainingAmount==0&&shared.GroundResourceStacks.Count==1&&
            replicaA.CurrentRegionReplica!.ResourceNodes[nodeId].RemainingAmount==0&&replicaB.CurrentRegionReplica!.ResourceNodes[nodeId].RemainingAmount==0&&
            replicaA.CurrentRegionReplica.GroundResourceStacks.Count==1&&replicaB.CurrentRegionReplica.GroundResourceStacks.Count==1);
        int groundTotal=shared.GroundResourceStacks.GetAllStackIds().Sum(id=>shared.GroundResourceStacks.TryGet(id,out GroundResourceStackV3? stack)?stack?.Amount??0:0);
        Check(_host.RegionSessions!.TryGetActiveRegion(shared.RegionId,out ManagedRegionSessionV3? sharedManaged)&&sharedManaged!.Active.Work.ActiveReservationCount==0&&groundTotal==2,"Gather conservation or reservation failed.");

        long aSequenceBeforeDisconnect=replicaA.CurrentDeltaSequence;
        long bOldSequence=replicaB.CurrentDeltaSequence;
        int membershipBefore=_host.Connections.RegionMembershipCount;
        await Disconnect(clientB);
        Check(_host.Connections.GetRegionConnectionCount(shared.RegionId)==1&&_host.Connections.RegionMembershipCount==membershipBefore-1,"B membership leaked.");
        Send(clientA,Command(8,3,shared.RegionId,joinA.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercA,TargetCell=new(8,6)}));
        await Expect(clientA,NetworkMessageTypeV3.CommandAccepted);
        await PumpSingleReplica(clientA,replicaA,()=>Cell(replicaA,mercA)==new SnapshotCellV3(8,6));
        Check(replicaA.CurrentDeltaSequence>aSequenceBeforeDisconnect&&replicaB.CurrentDeltaSequence==bOldSequence,"Per-connection delta state was not isolated.");

        TestClient clientB2=await Connect(port);NetworkClientSessionV3 replicaB2=new();
        Send(clientB2,Hello(1,accountB));NetworkMessageV3 helloB2=await WaitFor(clientB2,NetworkMessageTypeV3.ServerHelloAccepted);Check(helloB2.CompanyId==helloB.CompanyId&&replicaB2.ApplyServerHello(helloB2),"B reconnect company mismatch.");
        Send(clientB2,Join(2,shared.RegionId));NetworkMessageV3 joinB2=await WaitFor(clientB2,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaB2.ApplyRegionJoin(joinB2),"B reconnect join failed.");
        Send(clientB2,Snapshot(3,shared.RegionId,joinB2.ActiveSessionRevision));NetworkMessageV3 snapshotB2=await WaitFor(clientB2,NetworkMessageTypeV3.InitialRegionSnapshotAccepted);Check(replicaB2.TryApplyInitialSnapshot(snapshotB2,out NetworkRejectReasonV3 reconnectReason),reconnectReason.ToString());
        Check(replicaB2.CurrentRegionReplica!.ResourceNodes[nodeId].RemainingAmount==0&&replicaB2.CurrentRegionReplica.GroundResourceStacks.Count==1&&Cell(replicaB2,mercA)==Cell(replicaA,mercA),"B reconnect snapshot disagrees with A.");

        ServerCommandDeltaDiagnosticsV3 diagnostics=_host.CommandDiagnostics!;
        Check(_host.Diagnostics.CrossCompanyEstateJoinRejectedCount==2&&diagnostics.ForeignMercenaryCommandRejectedCount==1&&diagnostics.UnauthorizedCommandAcceptedCount==0,"Permission diagnostics mismatch.");
        Check(diagnostics.ConcurrentGatherRequestCount==1&&diagnostics.ConcurrentGatherDuplicateWorkCount==0,"Concurrent gathering diagnostics mismatch.");
        Check(diagnostics.RegionDeltaFanoutCount>=6&&replicaA.Diagnostics.PartialDeltaApplyCount==0&&replicaB.Diagnostics.PartialDeltaApplyCount==0&&replicaB2.Diagnostics.PartialDeltaApplyCount==0,"Fanout or replica diagnostics mismatch.");
        string gatherWinner=gatherA.MessageType==NetworkMessageTypeV3.CommandAccepted?"A":"B";
        return $"port={port} companies={_host.World!.PlayerCompanies.Count} estates=2 sharedMembers={_host.Connections.GetRegionConnectionCount(shared.RegionId)} estateReject={_host.Diagnostics.CrossCompanyEstateJoinRejectedCount} own/foreign={diagnostics.OwnMercenaryCommandAcceptedCount}/{diagnostics.ForeignMercenaryCommandRejectedCount} fanout={diagnostics.RegionDeltaFanoutCount} gatherAccepted={gatherWinner} remaining=0 ground={groundTotal} seqA/B/B2={replicaA.CurrentDeltaSequence}/{replicaB.CurrentDeltaSequence}/{replicaB2.CurrentDeltaSequence} partial=0 unauthorized=0";
    }

    private static void ValidateVisibility(NetworkClientSessionV3 session,string ownId,string foreignId)
    {
        MercenarySnapshotDtoV3 own=session.CurrentRegionReplica!.Mercenaries[ownId],foreign=session.CurrentRegionReplica.Mercenaries[foreignId];
        Check(own.IsOwnedByRecipient&&!foreign.IsOwnedByRecipient,"Mercenary visibility ownership mismatch.");
        Check(own.Gathering>0&&foreign.Gathering==0&&foreign.Hunger==0&&foreign.Fatigue==0&&foreign.Strength==0,"Foreign private profile leaked.");
    }

    private static string AddMercenary(PlayerCompanyStateV3 company,string regionId,Vector2I cell,string name,int gathering)
    {
        MercenaryAttributeSetV3.TryCreate(10,10,10,10,10,out var attributes,out _);
        MercenaryWorkSkillSetV3.TryCreate(8,8,gathering,8,8,8,8,out var skills,out _);
        string id=MercenaryIdFactoryV3.CreateMercenaryId();DateTime now=DateTime.UtcNow;
        MercenaryProfileV3.TryCreate(id,name,"placeholder",attributes,skills,now,out var profile,out _);
        MercenaryStateV3.TryCreate(id,company.CompanyId,new(cell),MercenaryActivityStateV3.Idle,now,out var state,out _);
        Check(company.MercenaryProfiles.TryRegisterMercenary(profile,state,out string reason),reason);
        Check(company.SetMercenaryRegion(id,regionId),"Mercenary presence failed.");
        return id;
    }

    private static string AddNode(RegionPersistentStateV3 region,Vector2I cell)
    {
        string id=ResourceNodeIdFactoryV3.Create();DateTime now=DateTime.UtcNow;
        Check(ResourceNodeStateV3.TryCreate(id,ResourceNodeTypeV3.IronVein,new(cell),2,2,2,Bounds,now,out ResourceNodeStateV3? node,out string reason)&&node!=null,reason);
        Check(region.ResourceNodes.TryRegister(node,out reason),reason);return id;
    }

    private async Task<int> Start(){int first=35000+(int)(OS.GetProcessId()%1000);for(int i=0;i<24;i++){int port=first+i;if(_host!.TryStart(port,51306,out _))return port;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new InvalidOperationException("No ENet fixture port.");}
    private async Task<TestClient> Connect(int port){TestClient client=new(port);_clients.Add(client);await PumpUntil(()=>client.IsConnected);return client;}
    private async Task Disconnect(TestClient client){int expected=_host!.Connections.Count-1;Send(client,new(){MessageType=NetworkMessageTypeV3.ClientDisconnect,RequestId=long.MaxValue});await PumpUntil(()=>_host.Connections.Count==expected);client.Close();_clients.Remove(client);client.Dispose();}
    private async Task<NetworkMessageV3> WaitFor(TestClient client,NetworkMessageTypeV3 type){NetworkMessageV3? value=null;await PumpUntil(()=>client.TryTake(type,out value));return value!;}
    private async Task<NetworkMessageV3> Expect(TestClient client,NetworkMessageTypeV3 type)=>await WaitFor(client,type);
    private async Task<NetworkMessageV3> WaitForEither(TestClient client,NetworkMessageTypeV3 first,NetworkMessageTypeV3 second){NetworkMessageV3? value=null;await PumpUntil(()=>client.TryTake(first,out value)||client.TryTake(second,out value));return value!;}
    private async Task PumpReplicas(NetworkClientSessionV3 a,NetworkClientSessionV3 b,Func<bool> condition){await PumpUntil(()=>{Drain(_clients[0],a);Drain(_clients[1],b);return condition();});}
    private async Task PumpSingleReplica(TestClient client,NetworkClientSessionV3 replica,Func<bool> condition){await PumpUntil(()=>{Drain(client,replica);return condition();});}
    private static void Drain(TestClient client,NetworkClientSessionV3 replica){while(client.TryTake(NetworkMessageTypeV3.RegionDeltaBatch,out NetworkMessageV3? delta)&&delta!=null)Check(replica.TryApplyRegionDelta(delta,out string reason),reason);}
    private async Task PumpUntil(Func<bool> condition){for(int i=0;i<1200;i++){_host!.Poll();foreach(TestClient client in _clients)client.Poll();if(condition())return;if(_clients.Any(x=>x.IsFailed))throw new InvalidOperationException("ENet client failed.");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new TimeoutException("Two-client ENet fixture timed out.");}
    private static void Send(TestClient client,NetworkMessageV3 message){if(!client.Send(message))throw new InvalidOperationException("ENet send failed.");}
    private static NetworkMessageV3 Hello(long request,string account)=>new(){MessageType=NetworkMessageTypeV3.ClientHello,RequestId=request,DevelopmentPlayerAccountId=account};
    private static NetworkMessageV3 Join(long request,string region)=>new(){MessageType=NetworkMessageTypeV3.JoinRegionRequest,RequestId=request,RegionId=region};
    private static NetworkMessageV3 Snapshot(long request,string region,long revision)=>new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=request,RegionId=region,ExpectedSessionRevision=revision};
    private static NetworkMessageV3 Command(long request,long sequence,string region,long revision,GameplayCommandPayloadV3 payload)=>new(){MessageType=NetworkMessageTypeV3.SubmitGameplayCommand,RequestId=request,ClientCommandSequence=sequence,RegionId=region,ExpectedSessionRevision=revision,CommandPayload=GameplayCommandDeltaProtocolV3.SerializeCommand(payload)};
    private static SnapshotCellV3 Cell(NetworkClientSessionV3 replica,string mercenaryId)=>replica.CurrentRegionReplica!.Mercenaries[mercenaryId].Cell;
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}

    private sealed class TestClient:IDisposable
    {
        private readonly ENetMultiplayerPeer _peer=new();private readonly Queue<NetworkMessageV3> _messages=new();
        public TestClient(int port){Error error=_peer.CreateClient("127.0.0.1",port);if(error!=Error.Ok)throw new InvalidOperationException(error.ToString());}
        public bool IsConnected=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Connected;
        public bool IsFailed=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Disconnected;
        public void Poll(){if(IsFailed)return;_peer.Poll();while(_peer.GetAvailablePacketCount()>0)if(NetworkProtocolV3.TryDeserialize(_peer.GetPacket(),out NetworkMessageV3? message)&&message!=null)_messages.Enqueue(message);}
        public bool Send(NetworkMessageV3 message){if(!IsConnected)return false;_peer.SetTargetPeer(1);return _peer.PutPacket(NetworkProtocolV3.Serialize(message))==Error.Ok;}
        public bool TryTake(NetworkMessageTypeV3 type,out NetworkMessageV3? message){message=null;int count=_messages.Count;for(int i=0;i<count;i++){NetworkMessageV3 current=_messages.Dequeue();if(message==null&&current.MessageType==type)message=current;else _messages.Enqueue(current);}return message!=null;}
        public void Close()=>_peer.Close();public void Dispose()=>_peer.Dispose();
    }
}
