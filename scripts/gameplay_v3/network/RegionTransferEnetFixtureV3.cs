using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameplayV3.Construction;
using GameplayV3.Equipment;
using GameplayV3.Mercenary;
using GameplayV3.Resources;
using GameplayV3.Session;
using Godot;
using WorldV2;

namespace GameplayV3.Network;

public partial class RegionTransferEnetFixtureV3:Node
{
    private readonly List<TestClient> _clients=new();
    private DedicatedServerHostV3? _host;

    public override async void _Ready()
    {
        bool pass;string summary;
        try{summary=await Run();pass=true;}catch(Exception exception){pass=false;summary=exception.ToString();}
        finally{foreach(TestClient client in _clients)client.Dispose();_clients.Clear();_host?.Dispose();}
        GD.Print($"[RegionTransferEnetV3] PASS={pass} {summary}");GetTree().Quit(pass?0:3);
    }

    private async Task<string> Run()
    {
        _host=new();int port=await Start();PersistentWorldStateV3 world=_host.World!;RegionSessionManagerV3 manager=_host.RegionSessions!;
        TestClient a=await Connect(port),b=await Connect(port);NetworkClientSessionV3 replicaA=new(),replicaB=new();
        Send(a,Hello(1,"travel_account_a"));NetworkMessageV3 helloA=await Wait(a,NetworkMessageTypeV3.ServerHelloAccepted);Check(replicaA.ApplyServerHello(helloA),"A hello failed.");
        Send(b,Hello(1,"travel_account_b"));NetworkMessageV3 helloB=await Wait(b,NetworkMessageTypeV3.ServerHelloAccepted);Check(replicaB.ApplyServerHello(helloB),"B hello failed.");
        Check(_host.TryGetDevelopmentCompany("travel_account_a",out PlayerCompanyStateV3? companyA)&&companyA!=null,"A company missing.");
        Check(_host.TryGetDevelopmentCompany("travel_account_b",out PlayerCompanyStateV3? companyB)&&companyB!=null,"B company missing.");
        string estateA=_host.GetEstateRegionId(companyA!);RegionPersistentStateV3 shared=_host.CreateOrGetSharedNeutralRegion();
        string routeId="route_"+estateA+"_shared";Check(world.WorldGraph.TryGetRoute(routeId,out WorldRouteV3? route)&&route!=null&&route.Bidirectional,"Estate/shared route missing.");
        Check(world.WorldGraph.AddRoute(new("route_disabled_fixture",estateA,shared.RegionId,true,30,false),out _),"Disabled route fixture setup failed.");

        Send(a,Join(2,estateA));NetworkMessageV3 joinA=await Wait(a,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaA.ApplyRegionJoin(joinA),"A estate join failed.");
        Send(b,Join(2,shared.RegionId));NetworkMessageV3 joinB=await Wait(b,NetworkMessageTypeV3.JoinRegionAccepted);Check(replicaB.ApplyRegionJoin(joinB),"B shared join failed.");
        string mercA1=AddMercenary(companyA!,estateA,new(4,4),"Traveler A1"),mercA2=AddMercenary(companyA!,estateA,new(5,4),"Traveler A2");
        string mercB=AddMercenary(companyB!,shared.RegionId,new(9,9),"Observer B");manager.AddRuntimeMercenary(estateA,mercA1);manager.AddRuntimeMercenary(estateA,mercA2);manager.AddRuntimeMercenary(shared.RegionId,mercB);
        string equipmentId=EquipSword(companyA!,mercA1,estateA);
        string estateNode=AddNode(world.Regions[estateA],new(12,4),3);
        Send(a,Snapshot(3,estateA,joinA.ActiveSessionRevision));Check(replicaA.TryApplyInitialSnapshot(await Wait(a,NetworkMessageTypeV3.InitialRegionSnapshotAccepted),out NetworkRejectReasonV3 ar),ar.ToString());
        Send(b,Snapshot(3,shared.RegionId,joinB.ActiveSessionRevision));Check(replicaB.TryApplyInitialSnapshot(await Wait(b,NetworkMessageTypeV3.InitialRegionSnapshotAccepted),out NetworkRejectReasonV3 br),br.ToString());

        Send(a,Transfer(4,1,estateA,joinA.ActiveSessionRevision,shared.RegionId,"route_missing",mercA1,mercA2));
        Check((await Wait(a,NetworkMessageTypeV3.RegionTransferRejected)).RejectReason==NetworkRejectReasonV3.RouteNotFound,"Missing route transfer was accepted.");
        Send(a,Transfer(5,2,estateA,joinA.ActiveSessionRevision,shared.RegionId,"route_disabled_fixture",mercA1,mercA2));
        Check((await Wait(a,NetworkMessageTypeV3.RegionTransferRejected)).RejectReason==NetworkRejectReasonV3.RouteDisabled,"Disabled route transfer was accepted.");
        Send(a,Transfer(6,3,estateA,joinA.ActiveSessionRevision,_host.GetEstateRegionId(companyB!),routeId,mercA1));
        Check((await Wait(a,NetworkMessageTypeV3.RegionTransferRejected)).RejectReason==NetworkRejectReasonV3.DestinationAccessDenied,"Foreign estate transfer was accepted.");
        Send(a,Transfer(7,4,estateA,joinA.ActiveSessionRevision,shared.RegionId,routeId));
        Check((await Wait(a,NetworkMessageTypeV3.RegionTransferRejected)).RejectReason==NetworkRejectReasonV3.EmptyGroup,"Empty transfer was accepted.");
        Send(a,Transfer(8,5,estateA,joinA.ActiveSessionRevision,shared.RegionId,routeId,mercA1,mercA1));
        Check((await Wait(a,NetworkMessageTypeV3.RegionTransferRejected)).RejectReason==NetworkRejectReasonV3.DuplicateMercenary,"Duplicate mercenary transfer was accepted.");
        Send(a,Transfer(9,6,estateA,joinA.ActiveSessionRevision,shared.RegionId,routeId,mercB));
        Check((await Wait(a,NetworkMessageTypeV3.RegionTransferRejected)).RejectReason==NetworkRejectReasonV3.MercenaryNotOwned,"Foreign mercenary transfer was accepted.");
        Send(a,Transfer(10,7,estateA,joinA.ActiveSessionRevision,shared.RegionId,routeId,mercA1,mercA2));
        NetworkMessageV3 accepted=await Wait(a,NetworkMessageTypeV3.RegionTransferAccepted);Check(replicaA.ApplyRegionTransferAccepted(accepted),"Transfer accepted was not applied.");
        bool foundGroup=world.TravelingGroups.TryGet(accepted.TravelingGroupId,out TravelingGroupStateV3? group);
        Check(foundGroup&&group!=null&&group.State==TravelingGroupStatusV3.Traveling,$"Traveling group missing id={accepted.TravelingGroupId} registry={world.TravelingGroups.Count} found={foundGroup} state={group?.State} departure/arrival/now={accepted.DepartureWorldTime}/{accepted.ArrivalWorldTime}/{world.WorldClock.ElapsedSimulationSeconds}.");
        string[] entryBlockers=BlockEntryCells(shared,companyB!.CompanyId);
        Check(!manager.TryGetActiveRegion(estateA,out _),"Empty origin estate remained active.");
        foreach(string id in new[]{mercA1,mercA2})Check(companyA!.TryGetMercenaryPresence(id,out MercenaryPresenceStateV3? p)&&p?.IsTraveling==true&&!manager.ActiveRegionIds.Any(r=>manager.TryGetActiveRegion(r,out ManagedRegionSessionV3? m)&&m!.ContainsRuntimeMercenary(id)),"Traveling mercenary remained in a runtime.");
        Check(companyA!.Equipment.TryGetInstance(equipmentId,out EquipmentInstanceV3? before)&&before?.LocationKind==EquipmentLocationKindV3.Equipped&&before.EquippedMercenaryId==mercA1,"Equipped item changed on departure.");
        Send(a,Transfer(10,7,estateA,joinA.ActiveSessionRevision,shared.RegionId,routeId,mercA1,mercA2));
        Check((await Wait(a,NetworkMessageTypeV3.RegionTransferAccepted)).TravelingGroupId==accepted.TravelingGroupId&&world.TravelingGroups.Count==1,"Transfer replay created another group.");
        Send(a,Transfer(11,8,estateA,joinA.ActiveSessionRevision,shared.RegionId,routeId,mercA1));
        Check((await Wait(a,NetworkMessageTypeV3.RegionTransferRejected)).RejectReason==NetworkRejectReasonV3.ConnectionAlreadyTraveling,"Second traveling transfer was accepted.");

        Send(a,Command(12,9,estateA,joinA.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercA1,TargetCell=new(6,6)}));
        Check((await Wait(a,NetworkMessageTypeV3.CommandRejected)).RejectReason==NetworkRejectReasonV3.RegionJoinRequired,"Traveling gameplay command was accepted.");
        Send(a,Join(13,shared.RegionId));Check((await Wait(a,NetworkMessageTypeV3.JoinRegionRejected)).RejectReason==NetworkRejectReasonV3.ConnectionAlreadyTraveling,"Traveling direct join was accepted.");

        await Disconnect(a);TestClient a2=await Connect(port);replicaA=new();
        Send(a2,Hello(1,"travel_account_a"));NetworkMessageV3 reconnect=await Wait(a2,NetworkMessageTypeV3.ServerHelloAccepted);
        Check(replicaA.ApplyServerHello(reconnect)&&replicaA.IsTraveling&&reconnect.CompanyId==helloA.CompanyId&&reconnect.TravelingGroupId==accepted.TravelingGroupId,"Traveling reconnect was not restored.");
        await PumpUntil(()=>group!.State==TravelingGroupStatusV3.ArrivalBlocked);
        Check(companyA!.TryGetMercenaryPresence(mercA1,out MercenaryPresenceStateV3? blockedPresence)&&blockedPresence?.IsTraveling==true,"Blocked arrival lost traveling presence.");
        Check(manager.TryGetActiveRegion(shared.RegionId,out ManagedRegionSessionV3? blockedShared)&&blockedShared!=null&&!blockedShared.ContainsRuntimeMercenary(mercA1),"Blocked arrival exposed a runtime entity.");
        foreach(string structureId in entryBlockers)Check(shared.Structures.TryRemove(structureId,out _),"Entry blocker cleanup failed.");
        NetworkMessageV3 arrived=await Wait(a2,NetworkMessageTypeV3.RegionTransferArrived);Check(replicaA.ApplyRegionTransferArrived(arrived),"Arrival was not applied.");
        await PumpUntil(()=>{Drain(b,replicaB);return replicaB.CurrentRegionReplica!.Mercenaries.ContainsKey(mercA1)&&replicaB.CurrentRegionReplica.Mercenaries.ContainsKey(mercA2);});
        Check(group!.State==TravelingGroupStatusV3.Arrived&&companyA.TryGetMercenaryPresence(mercA1,out MercenaryPresenceStateV3? arrivedPresence)&&arrivedPresence?.CurrentRegionId==shared.RegionId,"Arrival presence failed.");
        Check(manager.TryGetActiveRegion(shared.RegionId,out ManagedRegionSessionV3? activeShared)&&activeShared!=null&&activeShared.ContainsRuntimeMercenary(mercA1)&&activeShared.ContainsRuntimeMercenary(mercA2),"Destination runtime entities missing.");
        Check(companyA.Equipment.TryGetInstance(equipmentId,out EquipmentInstanceV3? after)&&ReferenceEquals(before,after)&&after?.LocationKind==EquipmentLocationKindV3.Equipped&&companyA.EquipmentLoadouts.GetEquippedInstanceId(mercA1,EquipmentSlotV3.MainHand)==equipmentId,"Equipment identity/loadout was not preserved.");

        Send(a2,Snapshot(3,shared.RegionId,arrived.ActiveSessionRevision));Check(replicaA.TryApplyInitialSnapshot(await Wait(a2,NetworkMessageTypeV3.InitialRegionSnapshotAccepted),out NetworkRejectReasonV3 arrivalSnapshotReason),arrivalSnapshotReason.ToString());
        Check(replicaA.CurrentRegionReplica!.Mercenaries.ContainsKey(mercA1)&&replicaA.CurrentRegionReplica.Equipment.ContainsKey(equipmentId),"Arrival snapshot omitted traveler or equipment.");
        Send(a2,Command(4,1,shared.RegionId,arrived.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercA1,TargetCell=new(8,4)}));
        await Wait(a2,NetworkMessageTypeV3.CommandAccepted);await PumpUntil(()=>{Drain(a2,replicaA);Drain(b,replicaB);return replicaA.CurrentRegionReplica!.Mercenaries[mercA1].Cell==new SnapshotCellV3(8,4)&&replicaB.CurrentRegionReplica!.Mercenaries[mercA1].Cell==new SnapshotCellV3(8,4);});

        Send(a2,Transfer(5,2,shared.RegionId,arrived.ActiveSessionRevision,estateA,routeId,mercA1,mercA2));
        NetworkMessageV3 returnAccepted=await Wait(a2,NetworkMessageTypeV3.RegionTransferAccepted);Check(replicaA.ApplyRegionTransferAccepted(returnAccepted),"Return transfer failed.");
        await PumpUntil(()=>{Drain(b,replicaB);return !replicaB.CurrentRegionReplica!.Mercenaries.ContainsKey(mercA1)&&!replicaB.CurrentRegionReplica.Mercenaries.ContainsKey(mercA2);});
        NetworkMessageV3 returned=await Wait(a2,NetworkMessageTypeV3.RegionTransferArrived);Check(replicaA.ApplyRegionTransferArrived(returned)&&returned.DestinationRegionId==estateA,"Return arrival failed.");
        Send(a2,Snapshot(7,estateA,returned.ActiveSessionRevision));Check(replicaA.TryApplyInitialSnapshot(await Wait(a2,NetworkMessageTypeV3.InitialRegionSnapshotAccepted),out NetworkRejectReasonV3 returnSnapshotReason),returnSnapshotReason.ToString());
        Check(replicaA.CurrentRegionReplica!.ResourceNodes.ContainsKey(estateNode)&&companyA.Equipment.TryGetInstance(equipmentId,out EquipmentInstanceV3? finalItem)&&ReferenceEquals(before,finalItem),"Estate state or equipment was lost after round trip.");
        Send(a2,Command(8,1,estateA,joinA.ActiveSessionRevision,new(){CommandKind=GameplayCommandKindV3.CancelMercenaryOrder,MercenaryId=mercA1}));
        Check((await Wait(a2,NetworkMessageTypeV3.CommandRejected)).RejectReason==NetworkRejectReasonV3.StaleSession,"Stale origin command was accepted.");
        Send(a2,Transfer(9,2,estateA,joinA.ActiveSessionRevision,shared.RegionId,routeId,mercA1));
        Check((await Wait(a2,NetworkMessageTypeV3.RegionTransferRejected)).RejectReason==NetworkRejectReasonV3.StaleSession,"Stale transfer command was accepted.");

        WorldTravelDiagnosticsV3 diagnostics=_host.TravelRuntime!.Diagnostics;
        Check(world.TravelingGroups.Count==2&&world.TravelingGroups.ActiveCount==0&&diagnostics.TravelingGroupCreatedCount==2&&diagnostics.TravelingGroupArrivedCount==2,"Traveling group lifecycle counts failed.");
        Check(diagnostics.EquippedItemPreservationFailureCount==0&&manager.Diagnostics.WorldClockDuplicateAdvanceCount==0,"Equipment or clock invariant failed.");
        Check(companyA.Equipment.TryValidateInvariants(out EquipmentRuntimeInvariantSnapshotV3 equipmentInvariant,out string equipmentReason)&&equipmentInvariant.LocationInvariantViolationCount==0&&equipmentInvariant.DuplicateIndexViolationCount==0&&equipmentInvariant.OrphanInstanceCount==0,equipmentReason);
        await Disconnect(a2);await Disconnect(b);Check(manager.ActiveSessionCount==0,"Active region leaked.");
        return $"port={port} routes={world.WorldGraph.RouteCount} groups={world.TravelingGroups.Count}/active0 created/arrived={diagnostics.TravelingGroupCreatedCount}/{diagnostics.TravelingGroupArrivedCount} arrivalBlocked={diagnostics.AtomicArrivalRollbackCount} added/removed={diagnostics.MercenaryReplicaAddedEventCount}/{diagnostics.MercenaryReplicaRemovedEventCount} equipment={equipmentId} reconnect=traveling stale=0 clockDuplicate=0";
    }

    private static string AddMercenary(PlayerCompanyStateV3 company,string regionId,Vector2I cell,string name)
    {
        MercenaryAttributeSetV3.TryCreate(10,10,10,10,10,out var attributes,out _);MercenaryWorkSkillSetV3.TryCreate(8,8,10,8,8,8,8,out var skills,out _);
        string id=MercenaryIdFactoryV3.CreateMercenaryId();DateTime now=DateTime.UtcNow;MercenaryProfileV3.TryCreate(id,name,"placeholder",attributes,skills,now,out var profile,out _);
        MercenaryStateV3.TryCreate(id,company.CompanyId,new(cell),MercenaryActivityStateV3.Idle,now,out var state,out _);Check(company.MercenaryProfiles.TryRegisterMercenary(profile,state,out string reason),reason);Check(company.SetMercenaryRegion(id,regionId),"Presence failed.");return id;
    }
    private static string EquipSword(PlayerCompanyStateV3 company,string mercenaryId,string regionId)
    {
        const string facility="travel_fixture_output";
        Check(company.Equipment.TryCreateInstanceInFacilityOutput("iron_sword",EquipmentQualityV3.Good,8,mercenaryId,10,company.CompanyId,facility,company.Equipment.SessionRevision,out EquipmentInstanceV3? item,out string reason)&&item!=null,reason);
        Check(company.Equipment.TryMoveFacilityOutputToCompanyHolding(facility,item!.EquipmentInstanceId,out reason),reason);
        Check(company.EquipmentLoadouts.TryEquip(mercenaryId,item.EquipmentInstanceId,company.EquipmentLoadouts.SessionRevision,out EquipmentCommandFailureV3 failure),failure.ToString());
        Check(company.Equipment.TrySetEquippedTravelRegion(item.EquipmentInstanceId,mercenaryId,regionId,out reason),reason);
        return item.EquipmentInstanceId;
    }
    private static string AddNode(RegionPersistentStateV3 region,Vector2I cell,int amount){string id=ResourceNodeIdFactoryV3.Create();Check(ResourceNodeStateV3.TryCreate(id,ResourceNodeTypeV3.IronVein,new(cell),amount,amount,1,new(0,0,64,64),DateTime.UtcNow,out ResourceNodeStateV3? node,out string reason)&&node!=null,reason);Check(region.ResourceNodes.TryRegister(node,out reason),reason);return id;}
    private static string[] BlockEntryCells(RegionPersistentStateV3 region,string companyId)
    {
        Check(region.Construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.WoodenWallId,out StructureDefinitionV3? wall)&&wall!=null,"Wall definition missing.");
        List<string> ids=new();
        for(int y=1;y<=6;y++)for(int x=1;x<=6;x++)
        {
            GlobalCellCoord cell=new(new Vector2I(x,y));string id=StructureIdFactoryV3.Create();
            StructureStateV3 structure=new(id,companyId,wall!.DefinitionId,cell,StructureOrientationV3.Deg0,new[]{cell},wall.RequiredMaterials,true,DateTime.UtcNow,StructureMovementKindV3.Blocking);
            Check(region.Structures.TryRegister(structure,region.Blueprints,new(0,0,64,64),out string reason),reason);ids.Add(id);
        }
        return ids.ToArray();
    }
    private async Task<int> Start(){int first=38000+(int)(OS.GetProcessId()%1000);for(int i=0;i<24;i++){int port=first+i;if(_host!.TryStart(port,51308,out _))return port;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new InvalidOperationException("No ENet fixture port.");}
    private async Task<TestClient> Connect(int port){TestClient client=new(port);_clients.Add(client);await PumpUntil(()=>client.IsConnected);return client;}
    private async Task Disconnect(TestClient client){int expected=_host!.Connections.Count-1;Send(client,new(){MessageType=NetworkMessageTypeV3.ClientDisconnect,RequestId=long.MaxValue});await PumpUntil(()=>_host.Connections.Count==expected);client.Close();_clients.Remove(client);client.Dispose();}
    private async Task<NetworkMessageV3> Wait(TestClient client,NetworkMessageTypeV3 type){NetworkMessageV3? result=null;await PumpUntil(()=>client.TryTake(type,out result));return result!;}
    private async Task PumpUntil(Func<bool> condition){for(int i=0;i<4000;i++){_host!.Poll();foreach(TestClient c in _clients)c.Poll();if(condition())return;if(_clients.Any(x=>x.IsFailed))throw new InvalidOperationException("ENet client failed.");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new TimeoutException("Region transfer fixture timed out.");}
    private static void Drain(TestClient client,NetworkClientSessionV3 replica){while(client.TryTake(NetworkMessageTypeV3.RegionDeltaBatch,out NetworkMessageV3? delta)&&delta!=null)Check(replica.TryApplyRegionDelta(delta,out string reason),reason);}
    private static void Send(TestClient client,NetworkMessageV3 message){Check(client.Send(message),"ENet send failed.");}
    private static NetworkMessageV3 Hello(long request,string account)=>new(){MessageType=NetworkMessageTypeV3.ClientHello,RequestId=request,DevelopmentPlayerAccountId=account};
    private static NetworkMessageV3 Join(long request,string region)=>new(){MessageType=NetworkMessageTypeV3.JoinRegionRequest,RequestId=request,RegionId=region};
    private static NetworkMessageV3 Snapshot(long request,string region,long revision)=>new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=request,RegionId=region,ExpectedSessionRevision=revision};
    private static NetworkMessageV3 Command(long request,long sequence,string region,long revision,GameplayCommandPayloadV3 payload)=>new(){MessageType=NetworkMessageTypeV3.SubmitGameplayCommand,RequestId=request,ClientCommandSequence=sequence,RegionId=region,ExpectedSessionRevision=revision,CommandPayload=GameplayCommandDeltaProtocolV3.SerializeCommand(payload)};
    private static NetworkMessageV3 Transfer(long request,long sequence,string origin,long revision,string destination,string route,params string[] mercenaries)=>new(){MessageType=NetworkMessageTypeV3.RequestRegionTransfer,RequestId=request,ClientCommandSequence=sequence,OriginRegionId=origin,RegionId=origin,ExpectedSessionRevision=revision,DestinationRegionId=destination,RouteId=route,MercenaryIds=mercenaries};
    private static void Check(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}

    private sealed class TestClient:IDisposable
    {
        private readonly ENetMultiplayerPeer _peer=new();private readonly Queue<NetworkMessageV3> _messages=new();
        public TestClient(int port){Error error=_peer.CreateClient("127.0.0.1",port);if(error!=Error.Ok)throw new InvalidOperationException(error.ToString());}
        public bool IsConnected=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Connected;public bool IsFailed=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Disconnected;
        public void Poll(){if(IsFailed)return;_peer.Poll();while(_peer.GetAvailablePacketCount()>0)if(NetworkProtocolV3.TryDeserialize(_peer.GetPacket(),out NetworkMessageV3? message)&&message!=null)_messages.Enqueue(message);}
        public bool Send(NetworkMessageV3 message){if(!IsConnected)return false;_peer.SetTargetPeer(1);return _peer.PutPacket(NetworkProtocolV3.Serialize(message))==Error.Ok;}
        public bool TryTake(NetworkMessageTypeV3 type,out NetworkMessageV3? message){message=null;int count=_messages.Count;for(int i=0;i<count;i++){NetworkMessageV3 current=_messages.Dequeue();if(message==null&&current.MessageType==type)message=current;else _messages.Enqueue(current);}return message!=null;}
        public void Close()=>_peer.Close();public void Dispose()=>_peer.Dispose();
    }
}
