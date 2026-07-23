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

public partial class ConcurrentMultiRegionEnetFixtureV3:Node
{
    private static readonly Rect2I Bounds=new(0,0,64,64);
    private readonly List<TestClient> _clients=new();
    private DedicatedServerHostV3? _host;

    public override async void _Ready()
    {
        bool pass;string summary;
        try{summary=await RunFixture();pass=true;}catch(Exception exception){pass=false;summary=exception.ToString();}
        finally{foreach(TestClient client in _clients)client.Dispose();_clients.Clear();_host?.Dispose();}
        GD.Print($"[ConcurrentMultiRegionEnetV3] PASS={pass} {summary}");GetTree().Quit(pass?0:3);
    }

    private async Task<string> RunFixture()
    {
        _host=new();int port=await Start();PersistentWorldStateV3 world=_host.World!;RegionSessionManagerV3 manager=_host.RegionSessions!;
        TestClient a=await Connect(port),b=await Connect(port);NetworkClientSessionV3 replicaA=new(),replicaB=new();
        const string accountA="multi_region_account_a",accountB="multi_region_account_b";
        Send(a,Hello(1,accountA));NetworkMessageV3 helloA=await WaitFor(a,NetworkMessageTypeV3.ServerHelloAccepted);Check(replicaA.ApplyServerHello(helloA),"A hello failed.");
        Send(b,Hello(1,accountB));NetworkMessageV3 helloB=await WaitFor(b,NetworkMessageTypeV3.ServerHelloAccepted);Check(replicaB.ApplyServerHello(helloB),"B hello failed.");
        Check(_host.TryGetDevelopmentCompany(accountA,out PlayerCompanyStateV3? companyA)&&companyA!=null,"A company missing.");
        Check(_host.TryGetDevelopmentCompany(accountB,out PlayerCompanyStateV3? companyB)&&companyB!=null,"B company missing.");
        string estateA=_host.GetEstateRegionId(companyA!),estateB=_host.GetEstateRegionId(companyB!);Check(estateA!=estateB,"Estate IDs collided.");

        Send(a,Join(2,estateA));NetworkMessageV3 joinA=await WaitFor(a,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaA.ApplyRegionJoin(joinA),"A join failed.");
        Send(b,Join(2,estateB));NetworkMessageV3 joinB=await WaitFor(b,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaB.ApplyRegionJoin(joinB),"B join failed.");
        Check(manager.ActiveSessionCount==2,"Estates were not concurrently active.");
        if(!manager.TryGetActiveRegion(estateA,out ManagedRegionSessionV3? activeA)||activeA==null)throw new InvalidOperationException("Estate A session missing.");
        if(!manager.TryGetActiveRegion(estateB,out ManagedRegionSessionV3? activeB)||activeB==null)throw new InvalidOperationException("Estate B session missing.");
        long estateARevision=activeA.Active.SessionRevision,estateBRevision=activeB.Active.SessionRevision;

        RegionPersistentStateV3 regionA=world.Regions[estateA],regionB=world.Regions[estateB];
        string mercA=AddMercenary(companyA!,estateA,new(2,5),"Estate A Worker"),mercB=AddMercenary(companyB!,estateB,new(2,9),"Estate B Worker");
        string nodeA=AddNode(regionA,new(10,5)),nodeB=AddNode(regionB,new(10,9));
        Send(a,Snapshot(3,estateA,estateARevision));Check(replicaA.TryApplyInitialSnapshot(await WaitFor(a,NetworkMessageTypeV3.InitialRegionSnapshotAccepted),out NetworkRejectReasonV3 snapshotAReason),snapshotAReason.ToString());
        Send(b,Snapshot(3,estateB,estateBRevision));Check(replicaB.TryApplyInitialSnapshot(await WaitFor(b,NetworkMessageTypeV3.InitialRegionSnapshotAccepted),out NetworkRejectReasonV3 snapshotBReason),snapshotBReason.ToString());
        Check(!replicaA.CurrentRegionReplica!.ResourceNodes.ContainsKey(nodeB)&&!replicaB.CurrentRegionReplica!.ResourceNodes.ContainsKey(nodeA),"Initial snapshot crossed region boundary.");

        Send(a,Command(4,1,estateA,estateARevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercA,TargetCell=new(6,5)}));
        await WaitFor(a,NetworkMessageTypeV3.CommandAccepted);await PumpReplica(a,replicaA,()=>Cell(replicaA,mercA)==new SnapshotCellV3(6,5));
        PumpFrames(4);Check(!b.HasDeltaForRegion(estateA)&&!replicaB.NeedsResnapshot,"A movement delta leaked to B estate.");
        Send(b,Command(4,1,estateB,estateBRevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercB,TargetCell=new(6,9)}));
        await WaitFor(b,NetworkMessageTypeV3.CommandAccepted);await PumpReplica(b,replicaB,()=>Cell(replicaB,mercB)==new SnapshotCellV3(6,9));
        PumpFrames(4);Check(!a.HasDeltaForRegion(estateB)&&!replicaA.NeedsResnapshot,"B movement delta leaked to A estate.");

        Send(a,Command(5,2,estateA,estateARevision,new(){CommandKind=GameplayCommandKindV3.DirectGather,MercenaryId=mercA,ResourceNodeId=nodeA}));
        Send(b,Command(5,2,estateB,estateBRevision,new(){CommandKind=GameplayCommandKindV3.DirectGather,MercenaryId=mercB,ResourceNodeId=nodeB}));
        await WaitFor(a,NetworkMessageTypeV3.CommandAccepted);await WaitFor(b,NetworkMessageTypeV3.CommandAccepted);
        Check(activeA.Active.Work.ActiveReservationCount==1&&activeB.Active.Work.ActiveReservationCount==1,"Region reservations were not independent.");
        await PumpBoth(a,replicaA,b,replicaB,()=>regionA.ResourceNodes.TryGet(nodeA,out ResourceNodeStateV3? na)&&na?.RemainingAmount==0&&regionB.ResourceNodes.TryGet(nodeB,out ResourceNodeStateV3? nb)&&nb?.RemainingAmount==0&&
            replicaA.CurrentRegionReplica!.ResourceNodes[nodeA].RemainingAmount==0&&replicaB.CurrentRegionReplica!.ResourceNodes[nodeB].RemainingAmount==0);
        Check(activeA.Active.Work.ActiveReservationCount==0&&activeB.Active.Work.ActiveReservationCount==0&&regionA.GroundResourceStacks.Count==1&&regionB.GroundResourceStacks.Count==1,"Region work completion leaked reservation or output.");

        RegionPersistentStateV3 shared=_host.CreateOrGetSharedNeutralRegion();
        Send(a,Join(10,shared.RegionId));NetworkMessageV3 joinSharedA=await WaitFor(a,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaA.ApplyRegionJoin(joinSharedA),"A shared join failed.");
        Check(!manager.TryGetActiveRegion(estateA,out _)&&manager.TryGetActiveRegion(estateB,out activeB)&&activeB!=null&&manager.TryGetActiveRegion(shared.RegionId,out ManagedRegionSessionV3? activeShared)&&activeShared!=null,"A estate deactivation affected wrong region.");
        Send(a,Snapshot(11,shared.RegionId,joinSharedA.ActiveSessionRevision));Check(replicaA.TryApplyInitialSnapshot(await WaitFor(a,NetworkMessageTypeV3.InitialRegionSnapshotAccepted),out NetworkRejectReasonV3 sharedAReason),sharedAReason.ToString());

        Send(b,Command(10,3,estateB,estateBRevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercB,TargetCell=new(8,9)}));
        await WaitFor(b,NetworkMessageTypeV3.CommandAccepted);await PumpReplica(b,replicaB,()=>Cell(replicaB,mercB)==new SnapshotCellV3(8,9));
        Check(manager.TryGetActiveRegion(shared.RegionId,out activeShared)&&activeShared!=null,"Shared session disappeared while estate B ran.");

        companyA!.SetMercenaryRegion(mercA,shared.RegionId);companyB!.SetMercenaryRegion(mercB,shared.RegionId);
        Send(b,Join(11,shared.RegionId));NetworkMessageV3 joinSharedB=await WaitFor(b,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaB.ApplyRegionJoin(joinSharedB),"B shared join failed.");
        Check(!manager.TryGetActiveRegion(estateB,out _)&&manager.ActiveSessionCount==1&&_host.Connections.GetRegionConnectionCount(shared.RegionId)==2,"B estate did not deactivate independently.");
        Send(b,Snapshot(12,shared.RegionId,joinSharedB.ActiveSessionRevision));Check(replicaB.TryApplyInitialSnapshot(await WaitFor(b,NetworkMessageTypeV3.InitialRegionSnapshotAccepted),out NetworkRejectReasonV3 sharedBReason),sharedBReason.ToString());

        await Disconnect(a);Check(manager.TryGetActiveRegion(shared.RegionId,out _)&&_host.Connections.GetRegionConnectionCount(shared.RegionId)==1,"Shared region stopped while B remained.");
        await Disconnect(b);Check(manager.ActiveSessionCount==0&&_host.Connections.RegionMembershipCount==0,"Last disconnect leaked active region.");
        Check(world.PlayerCompanies.Count==2&&world.Regions.ContainsKey(estateA)&&world.Regions.ContainsKey(estateB)&&world.Regions.ContainsKey(shared.RegionId),"Persistent authority was removed.");

        TestClient a2=await Connect(port);NetworkClientSessionV3 replicaA2=new();
        Send(a2,Hello(1,accountA));NetworkMessageV3 helloA2=await WaitFor(a2,NetworkMessageTypeV3.ServerHelloAccepted);Check(helloA2.CompanyId==helloA.CompanyId&&replicaA2.ApplyServerHello(helloA2),"A reconnect company mismatch.");
        Send(a2,Join(2,estateA));NetworkMessageV3 rejoinA=await WaitFor(a2,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaA2.ApplyRegionJoin(rejoinA)&&rejoinA.ActiveSessionRevision>estateARevision,"Estate A session revision did not advance.");
        Send(a2,Snapshot(3,estateA,rejoinA.ActiveSessionRevision));Check(replicaA2.TryApplyInitialSnapshot(await WaitFor(a2,NetworkMessageTypeV3.InitialRegionSnapshotAccepted),out NetworkRejectReasonV3 rehydrateReason),rehydrateReason.ToString());
        Check(replicaA2.CurrentRegionReplica!.ResourceNodes[nodeA].RemainingAmount==0&&replicaA2.CurrentRegionReplica.GroundResourceStacks.Count==1,"Estate A persistent state was not rehydrated.");
        Send(a2,Command(4,1,estateA,estateARevision,new(){CommandKind=GameplayCommandKindV3.CancelMercenaryOrder,MercenaryId=mercA}));
        Check((await WaitFor(a2,NetworkMessageTypeV3.CommandRejected)).RejectReason==NetworkRejectReasonV3.StaleSession,"Old estate session command was accepted.");
        long serverTick=manager.ServerTick,worldTick=world.WorldClock.SimulationTick;
        Check(serverTick==worldTick&&manager.Diagnostics.WorldClockDuplicateAdvanceCount==0,"World clock advanced more than once per server tick.");
        Check(manager.Diagnostics.MaximumConcurrentSessionCount>=2&&manager.Diagnostics.DuplicateActiveRegionSessionCount==0&&manager.Diagnostics.DeactivatingOneRegionAffectedOtherRegionCount==0,"Region manager diagnostics failed.");
        ServerCommandDeltaDiagnosticsV3 commandDiagnostics=_host.CommandDiagnostics!;
        Check(commandDiagnostics.CrossRegionCommandExecutionCount==0&&commandDiagnostics.CrossRegionDeltaLeakCount==0&&commandDiagnostics.StaleSessionCommandAcceptedCount==0,"Cross-region or stale command diagnostics failed.");
        await Disconnect(a2);Check(manager.ActiveSessionCount==0,"Reactivated estate leaked after disconnect.");
        int persistentRegionCount=world.Regions.Count,persistentCompanyCount=world.PlayerCompanies.Count;
        _host.Stop();Check(_host.RegionSessions==null&&persistentRegionCount==3&&persistentCompanyCount==2,"Server shutdown lost persistent counts or leaked manager.");
        return $"port={port} maxActive={manager.Diagnostics.MaximumConcurrentSessionCount} revisionsA/B/reA={estateARevision}/{estateBRevision}/{rejoinA.ActiveSessionRevision} resourcesA/B=0/0 stacksA/B=1/1 crossCommand/delta=0/0 clockTick={worldTick} duplicateClock=0 deactivateImpact=0 staleAccepted=0 finalActive=0";
    }

    private static string AddMercenary(PlayerCompanyStateV3 company,string regionId,Vector2I cell,string name)
    {
        MercenaryAttributeSetV3.TryCreate(10,10,10,10,10,out var attributes,out _);MercenaryWorkSkillSetV3.TryCreate(8,8,12,8,8,8,8,out var skills,out _);
        string id=MercenaryIdFactoryV3.CreateMercenaryId();DateTime now=DateTime.UtcNow;MercenaryProfileV3.TryCreate(id,name,"placeholder",attributes,skills,now,out var profile,out _);
        MercenaryStateV3.TryCreate(id,company.CompanyId,new(cell),MercenaryActivityStateV3.Idle,now,out var state,out _);Check(company.MercenaryProfiles.TryRegisterMercenary(profile,state,out string reason),reason);Check(company.SetMercenaryRegion(id,regionId),"Presence failed.");return id;
    }
    private static string AddNode(RegionPersistentStateV3 region,Vector2I cell){string id=ResourceNodeIdFactoryV3.Create();Check(ResourceNodeStateV3.TryCreate(id,ResourceNodeTypeV3.IronVein,new(cell),2,2,2,Bounds,DateTime.UtcNow,out ResourceNodeStateV3? node,out string reason)&&node!=null,reason);Check(region.ResourceNodes.TryRegister(node,out reason),reason);return id;}
    private async Task<int> Start(){int first=36000+(int)(OS.GetProcessId()%1000);for(int i=0;i<24;i++){int port=first+i;if(_host!.TryStart(port,51307,out _))return port;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new InvalidOperationException("No ENet fixture port.");}
    private async Task<TestClient> Connect(int port){TestClient client=new(port);_clients.Add(client);await PumpUntil(()=>client.IsConnected);return client;}
    private async Task Disconnect(TestClient client){int expected=_host!.Connections.Count-1;Send(client,new(){MessageType=NetworkMessageTypeV3.ClientDisconnect,RequestId=long.MaxValue});await PumpUntil(()=>_host.Connections.Count==expected);client.Close();_clients.Remove(client);client.Dispose();}
    private async Task<NetworkMessageV3> WaitFor(TestClient client,NetworkMessageTypeV3 type){NetworkMessageV3? value=null;await PumpUntil(()=>client.TryTake(type,out value));return value!;}
    private async Task PumpReplica(TestClient client,NetworkClientSessionV3 replica,Func<bool> condition){await PumpUntil(()=>{Drain(client,replica);return condition();});}
    private async Task PumpBoth(TestClient a,NetworkClientSessionV3 ra,TestClient b,NetworkClientSessionV3 rb,Func<bool> condition){await PumpUntil(()=>{Drain(a,ra);Drain(b,rb);return condition();});}
    private static void Drain(TestClient client,NetworkClientSessionV3 replica){while(client.TryTake(NetworkMessageTypeV3.RegionDeltaBatch,out NetworkMessageV3? delta)&&delta!=null)Check(replica.TryApplyRegionDelta(delta,out string reason),reason);}
    private async Task PumpUntil(Func<bool> condition){for(int i=0;i<1500;i++){_host!.Poll();foreach(TestClient client in _clients)client.Poll();if(condition())return;if(_clients.Any(x=>x.IsFailed))throw new InvalidOperationException("ENet client failed.");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new TimeoutException("Concurrent multi-region fixture timed out.");}
    private void PumpFrames(int count){for(int i=0;i<count;i++){_host!.Poll();foreach(TestClient client in _clients)client.Poll();}}
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
        public bool IsConnected=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Connected;public bool IsFailed=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Disconnected;
        public void Poll(){if(IsFailed)return;_peer.Poll();while(_peer.GetAvailablePacketCount()>0)if(NetworkProtocolV3.TryDeserialize(_peer.GetPacket(),out NetworkMessageV3? message)&&message!=null)_messages.Enqueue(message);}
        public bool Send(NetworkMessageV3 message){if(!IsConnected)return false;_peer.SetTargetPeer(1);return _peer.PutPacket(NetworkProtocolV3.Serialize(message))==Error.Ok;}
        public bool TryTake(NetworkMessageTypeV3 type,out NetworkMessageV3? message){message=null;int count=_messages.Count;for(int i=0;i<count;i++){NetworkMessageV3 current=_messages.Dequeue();if(message==null&&current.MessageType==type)message=current;else _messages.Enqueue(current);}return message!=null;}
        public bool HasDeltaForRegion(string regionId)=>_messages.Any(x=>x.MessageType==NetworkMessageTypeV3.RegionDeltaBatch&&x.RegionId==regionId);
        public void Close()=>_peer.Close();public void Dispose()=>_peer.Dispose();
    }
}
