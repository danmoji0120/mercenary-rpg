using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameplayV3.Construction;
using GameplayV3.Equipment;
using GameplayV3.Farming;
using GameplayV3.Mercenary;
using GameplayV3.Resources;
using GameplayV3.Session;
using GameplayV3.Stockpile;
using Godot;
using WorldV2;

namespace GameplayV3.Network;

public partial class InitialRegionSnapshotEnetFixtureV3:Node
{
    private static readonly Rect2I Bounds=new(0,0,48,48);
    private DedicatedServerHostV3? _host;
    private readonly List<TestClient> _clients=new();

    public override async void _Ready()
    {
        bool pass;string summary;
        try{summary=await RunFixture();pass=true;}
        catch(Exception exception){pass=false;summary=exception.ToString();}
        finally{foreach(TestClient client in _clients)client.Dispose();_clients.Clear();_host?.Dispose();}
        GD.Print($"[InitialRegionSnapshotEnetV3] PASS={pass} {summary}");
        GetTree().Quit(pass?0:3);
    }

    private async Task<string> RunFixture()
    {
        _host=new DedicatedServerHostV3();
        int port=await StartOnBoundedPort();
        PersistentWorldStateV3 world=_host.World!;
        PlayerCompanyStateV3 company=_host.DefaultCompany!;
        RegionPersistentStateV3 region=world.Regions[PersistentWorldStateV3.InitialEstateRegionId];

        TestClient preHandshake=await Connect(port);
        Send(preHandshake,new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=1,RegionId=region.RegionId,ExpectedSessionRevision=GameplaySessionV3.SessionRevision});
        Check((await WaitFor(preHandshake,NetworkMessageTypeV3.InitialRegionSnapshotRejected)).RejectReason==NetworkRejectReasonV3.SnapshotRequiresJoinedRegion,"Pre-handshake snapshot was accepted.");
        await Disconnect(preHandshake);

        TestClient client=await Connect(port);
        Send(client,new(){MessageType=NetworkMessageTypeV3.ClientHello,RequestId=1,DevelopmentPlayerAccountId="snapshot_fixture_account"});
        NetworkMessageV3 hello=await WaitFor(client,NetworkMessageTypeV3.ServerHelloAccepted);
        Send(client,new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=2,RegionId=region.RegionId,ExpectedSessionRevision=hello.ActiveSessionRevision});
        Check((await WaitFor(client,NetworkMessageTypeV3.InitialRegionSnapshotRejected)).RejectReason==NetworkRejectReasonV3.SnapshotRequiresJoinedRegion,"Pre-join snapshot was accepted.");
        Send(client,new(){MessageType=NetworkMessageTypeV3.JoinRegionRequest,RequestId=3,RegionId=region.RegionId});
        NetworkMessageV3 joined=await WaitFor(client,NetworkMessageTypeV3.JoinRegionAccepted);
        NetworkClientSessionV3 clientSession=new();
        Check(clientSession.ApplyServerHello(hello)&&clientSession.ApplyRegionJoin(joined),"Client connection metadata apply failed.");

        FixtureIds ids=Populate(region,company);
        RegionPersistentStateV3 other=GameplaySessionV3.CreateRegion("region_snapshot_hidden_001",RegionTypeV3.PrivateEstate,company.CompanyId,90210);
        string hiddenNodeId=ResourceNodeIdFactoryV3.Create();
        Check(ResourceNodeStateV3.TryCreate(hiddenNodeId,ResourceNodeTypeV3.StoneOutcrop,new(new Vector2I(30,30)),5,5,1,Bounds,DateTime.UtcNow,out var hiddenNode,out string reason)&&hiddenNode!=null,reason);
        Check(other.ResourceNodes.TryRegister(hiddenNode,out reason),reason);

        Send(client,new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=4,RegionId=region.RegionId,ExpectedSessionRevision=joined.ActiveSessionRevision});
        NetworkMessageV3 accepted=await WaitFor(client,NetworkMessageTypeV3.InitialRegionSnapshotAccepted);
        Check(clientSession.TryApplyInitialSnapshot(accepted,out NetworkRejectReasonV3 replicaReason),replicaReason.ToString());
        NetworkRegionReplicaV3 replica=clientSession.CurrentRegionReplica!;
        Check(replica.WorldId==world.WorldId&&replica.RegionId==region.RegionId&&replica.ActiveSessionRevision==joined.ActiveSessionRevision,"Snapshot metadata mismatch.");
        Check(replica.Mercenaries.ContainsKey(ids.MercenaryId)&&replica.ResourceNodes.ContainsKey(ids.NodeId)&&replica.GroundResourceStacks.ContainsKey(ids.StackId),"Mercenary/resource replica missing.");
        Check(replica.Structures.ContainsKey(ids.StructureId)&&replica.Blueprints.ContainsKey(ids.BlueprintId)&&replica.Stockpiles.ContainsKey(ids.StockpileId),"Construction/stockpile replica missing.");
        Check(replica.ProductionFacilities.ContainsKey(ids.StructureId)&&replica.FarmPlots.ContainsKey(ids.FarmId),"Production/farming replica missing.");
        Check(replica.Equipment.ContainsKey(ids.GroundEquipmentId)&&replica.Equipment.ContainsKey(ids.EquippedEquipmentId),"Equipment replica missing.");
        Check(!replica.ResourceNodes.ContainsKey(hiddenNodeId),"Other-region resource leaked into snapshot.");
        Check(!ReferenceEquals(region.ResourceNodes,replica.ResourceNodes)&&!ReferenceEquals(company.Equipment,replica.Equipment),"Server authority object was shared with client replica.");
        int serverRemaining=region.ResourceNodes.TryGet(ids.NodeId,out var serverNode)&&serverNode!=null?serverNode.RemainingAmount:-1;
        Check(serverRemaining==replica.ResourceNodes[ids.NodeId].RemainingAmount,"Replica resource value mismatch.");

        NetworkRegionReplicaV3 original=replica;
        Check(clientSession.TryApplyInitialSnapshot(accepted,out replicaReason)&&clientSession.CurrentRegionReplica!.EntityCount==original.EntityCount,"Repeated snapshot accumulated replicas.");
        NetworkRegionReplicaV3 afterRepeat=clientSession.CurrentRegionReplica!;
        Check(!clientSession.TryApplyInitialSnapshot(accepted with{RegionRevision=accepted.RegionRevision-1},out _)&&ReferenceEquals(afterRepeat,clientSession.CurrentRegionReplica),"Older replica revision was accepted.");
        ValidateCorruptPayloads(clientSession,accepted);

        Send(client,new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=5,RegionId=region.RegionId,ExpectedSessionRevision=joined.ActiveSessionRevision-1});
        Check((await WaitFor(client,NetworkMessageTypeV3.InitialRegionSnapshotRejected)).RejectReason==NetworkRejectReasonV3.SessionRevisionMismatch,"Stale session snapshot was accepted.");
        Send(client,new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=6,RegionId=other.RegionId,ExpectedSessionRevision=joined.ActiveSessionRevision});
        Check((await WaitFor(client,NetworkMessageTypeV3.InitialRegionSnapshotRejected)).RejectReason==NetworkRejectReasonV3.SnapshotRegionMismatch,"Non-joined region snapshot was accepted.");
        Send(client,new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=7,RegionId="region_missing_snapshot",ExpectedSessionRevision=joined.ActiveSessionRevision});
        Check((await WaitFor(client,NetworkMessageTypeV3.InitialRegionSnapshotRejected)).RejectReason==NetworkRejectReasonV3.UnknownRegion,"Missing region snapshot was accepted.");
        Send(client,new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=4,RegionId=region.RegionId,ExpectedSessionRevision=joined.ActiveSessionRevision});
        Check((await WaitFor(client,NetworkMessageTypeV3.InitialRegionSnapshotRejected)).RejectReason==NetworkRejectReasonV3.DuplicateRequest,"Duplicate snapshot request was accepted.");
        Check(_host.TrySetSnapshotPayloadByteLimit(1024),"Fixture could not lower snapshot byte limit.");
        Send(client,new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=8,RegionId=region.RegionId,ExpectedSessionRevision=joined.ActiveSessionRevision});
        Check((await WaitFor(client,NetworkMessageTypeV3.InitialRegionSnapshotRejected)).RejectReason==NetworkRejectReasonV3.SnapshotTooLarge,"Oversized snapshot was accepted.");
        Send(client,new(){ProtocolVersion=NetworkProtocolV3.ProtocolVersion+1,MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=9,RegionId=region.RegionId,ExpectedSessionRevision=joined.ActiveSessionRevision});
        Check((await WaitFor(client,NetworkMessageTypeV3.ProtocolRejected)).RejectReason==NetworkRejectReasonV3.ProtocolMismatch,"Snapshot protocol mismatch was accepted.");

        DedicatedServerSnapshotV3 diagnostics=_host.GetSnapshot();
        Check(diagnostics.Diagnostics.SnapshotAcceptedCount==1&&diagnostics.Diagnostics.SnapshotRejectedCount>=7&&diagnostics.Diagnostics.SnapshotByteSize>1024,"Snapshot diagnostics mismatch.");
        Check(clientSession.Diagnostics.PartialApplyCount==0&&clientSession.Diagnostics.DuplicateReplicaIdCount==1&&clientSession.Diagnostics.InvalidReplicaReferenceCount==3,"Client atomic validation diagnostics mismatch.");
        await Disconnect(client);
        Check(_host.Connections.Count==0&&world.Regions.Count==2&&world.PlayerCompanies.Count==1,"Disconnect damaged persistent state.");
        return $"port={port} bytes={diagnostics.Diagnostics.SnapshotByteSize} accepted/rejected={diagnostics.Diagnostics.SnapshotAcceptedCount}/{diagnostics.Diagnostics.SnapshotRejectedCount} entities={replica.EntityCount} crossRegion=0 duplicate={clientSession.Diagnostics.DuplicateReplicaIdCount} invalidRef={clientSession.Diagnostics.InvalidReplicaReferenceCount} partial={clientSession.Diagnostics.PartialApplyCount}";
    }

    private static FixtureIds Populate(RegionPersistentStateV3 region,PlayerCompanyStateV3 company)
    {
        string reason;DateTime now=DateTime.UtcNow;
        MercenaryAttributeSetV3.TryCreate(10,11,12,13,14,out var attributes,out _);
        MercenaryWorkSkillSetV3.TryCreate(8,9,10,11,12,7,6,out var skills,out _);
        string mercenaryId=MercenaryIdFactoryV3.CreateMercenaryId();
        MercenaryProfileV3.TryCreate(mercenaryId,"Snapshot Fixture","placeholder",attributes,skills,now,out var profile,out _);
        MercenaryStateV3.TryCreate(mercenaryId,company.CompanyId,new(new Vector2I(2,2)),MercenaryActivityStateV3.Idle,now,out var state,out _);
        Check(company.MercenaryProfiles.TryRegisterMercenary(profile,state,out reason),reason);
        Check(company.SetMercenaryRegion(mercenaryId,region.RegionId), "Mercenary presence failed.");

        string nodeId=ResourceNodeIdFactoryV3.Create();
        Check(ResourceNodeStateV3.TryCreate(nodeId,ResourceNodeTypeV3.IronVein,new(new Vector2I(4,4)),7,10,2,Bounds,now,out var node,out reason)&&node!=null,reason);
        Check(region.ResourceNodes.TryRegister(node,out reason),reason);
        Check(region.GroundResourceStacks.TryAddStack(ResourceTypeV3.IronOre,3,new(new Vector2I(5,4)),out var stack,out _,out reason)&&stack!=null,reason);

        region.Construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.ProcessingWorkbenchId,out StructureDefinitionV3? bench);
        ResolvedStructureFootprintV3 benchFootprint=StructureFootprintResolverV3.Resolve(bench!,new(new Vector2I(8,4)),StructureOrientationV3.Deg0);
        StructureStateV3 structure=new(StructureIdFactoryV3.Create(),company.CompanyId,bench!.DefinitionId,new(new Vector2I(8,4)),StructureOrientationV3.Deg0,benchFootprint.Cells,bench.RequiredMaterials,bench.BlocksMovement,now,bench.MovementKind);
        Check(region.Structures.TryRegister(structure,region.Blueprints,Bounds,out reason),reason);
        Check(region.Production.TryAddOrder(company.CompanyId,structure.StructureId,"process_wood_plank",1,out _),"Production bill failed.");

        region.Construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.WoodenWallId,out StructureDefinitionV3? wall);
        ResolvedStructureFootprintV3 wallFootprint=StructureFootprintResolverV3.Resolve(wall!,new(new Vector2I(11,4)),StructureOrientationV3.Deg0);
        ConstructionBlueprintStateV3 blueprint=new(ConstructionBlueprintIdFactoryV3.Create(),company.CompanyId,wall!.DefinitionId,new(new Vector2I(11,4)),StructureOrientationV3.Deg0,wallFootprint.Cells,new ConstructionMaterialBufferV3(wall.RequiredMaterials),now);
        Check(region.Blueprints.TryRegister(blueprint,region.Structures,Bounds,out reason),reason);

        Check(region.StockpileZones.TryCreateZone(company.CompanyId,new[]{new GlobalCellCoord(new Vector2I(6,6))},Bounds,out var stockpile,out reason)&&stockpile!=null,reason);
        Check(region.FarmPlots.TryCreate(company.CompanyId,CropCatalogV3.PotatoCropId,new[]{new GlobalCellCoord(new Vector2I(13,4))},FarmSessionV3.MaxFarmCellsPerCompany,out var farm,out reason)&&farm!=null,reason);

        Check(company.Equipment.TryCreateInstanceInFacilityOutput(StarterEquipmentContentV3.IronPickaxeDefinitionId,EquipmentQualityV3.Good,9,mercenaryId,12,company.CompanyId,structure.StructureId,GameplaySessionV3.SessionRevision,out var groundEquipment,out reason)&&groundEquipment!=null,reason);
        Check(company.Equipment.TryMoveFacilityOutputToGround(structure.StructureId,groundEquipment!.EquipmentInstanceId,new Vector2I(7,7),out reason),reason);
        Check(company.Equipment.TryCreateInstanceInFacilityOutput(StarterEquipmentContentV3.IronSwordDefinitionId,EquipmentQualityV3.Excellent,13,mercenaryId,12,company.CompanyId,structure.StructureId,GameplaySessionV3.SessionRevision,out var equippedEquipment,out reason)&&equippedEquipment!=null,reason);
        Check(company.EquipmentLoadouts.TryEquip(mercenaryId,equippedEquipment!.EquipmentInstanceId,GameplaySessionV3.SessionRevision,out _),"Equipment loadout failed.");
        return new(mercenaryId,nodeId,stack!.ResourceStackId,structure.StructureId,blueprint.BlueprintId,stockpile!.StockpileZoneId,farm!.FarmPlotId,groundEquipment.EquipmentInstanceId,equippedEquipment.EquipmentInstanceId);
    }

    private static void ValidateCorruptPayloads(NetworkClientSessionV3 session,NetworkMessageV3 valid)
    {
        Check(RegionSnapshotProtocolV3.TryDeserialize(valid.SnapshotPayload,out InitialRegionSnapshotPayloadV3? payload)&&payload!=null,"Valid payload decode failed.");
        NetworkRegionReplicaV3 before=session.CurrentRegionReplica!;
        EquipmentSnapshotDtoV3 first=payload!.Equipment[0];
        ApplyRejected(payload with{Equipment=payload.Equipment.Concat(new[]{first}).ToArray()},"Duplicate equipment snapshot was accepted.");
        ApplyRejected(payload with{Mercenaries=payload.Mercenaries.Select((x,i)=>i==0?x with{CompanyId="cmp_private_other"}:x).ToArray()},"Other-company private mercenary was accepted.");
        ApplyRejected(payload with{Equipment=payload.Equipment.Select((x,i)=>i==0?x with{LocationKind=EquipmentLocationKindV3.Storage,GroundCell=null,StorageId="stockpile_missing",StorageCell=new(1,1)}:x).ToArray()},"Missing stockpile reference was accepted.");
        ApplyRejected(payload with{Equipment=payload.Equipment.Select((x,i)=>i==0?x with{LocationKind=EquipmentLocationKindV3.Equipped,GroundCell=null,EquippedMercenaryId="merc_missing",EquippedSlot=EquipmentSlotV3.Tool}:x).ToArray()},"Missing mercenary reference was accepted.");
        Check(ReferenceEquals(before,session.CurrentRegionReplica),"Failed snapshot partially replaced the replica.");
        void ApplyRejected(InitialRegionSnapshotPayloadV3 corrupt,string message)
        {
            NetworkMessageV3 envelope=valid with{SnapshotPayload=System.Text.Encoding.UTF8.GetString(RegionSnapshotProtocolV3.Serialize(corrupt))};
            Check(!session.TryApplyInitialSnapshot(envelope,out _),message);
        }
    }

    private async Task<int> StartOnBoundedPort()
    {
        int first=32000+(int)(OS.GetProcessId()%1000);
        for(int offset=0;offset<24;offset++){int port=first+offset;if(_host!.TryStart(port,51304,out _))return port;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        throw new InvalidOperationException("No bounded localhost ENet test port was available.");
    }
    private async Task<TestClient> Connect(int port){TestClient client=new(port);_clients.Add(client);await PumpUntil(()=>client.IsConnected,client);return client;}
    private async Task<NetworkMessageV3> WaitFor(TestClient client,NetworkMessageTypeV3 type){NetworkMessageV3? value=null;await PumpUntil(()=>client.TryTake(type,out value),client);return value!;}
    private async Task Disconnect(TestClient client){int expected=Math.Max(0,_host!.Connections.Count-1);Send(client,new(){MessageType=NetworkMessageTypeV3.ClientDisconnect,RequestId=long.MaxValue});await PumpUntil(()=>_host.Connections.Count<=expected,client);client.Close();_clients.Remove(client);client.Dispose();}
    private async Task PumpUntil(Func<bool> condition,TestClient client){for(int i=0;i<600;i++){_host!.Poll();client.Poll();if(condition())return;if(client.IsFailed)throw new InvalidOperationException("ENet client failed.");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new TimeoutException("Snapshot ENet fixture timed out.");}
    private static void Send(TestClient client,NetworkMessageV3 message){if(!client.Send(message))throw new InvalidOperationException("ENet send failed.");}
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private sealed record FixtureIds(string MercenaryId,string NodeId,string StackId,string StructureId,string BlueprintId,string StockpileId,string FarmId,string GroundEquipmentId,string EquippedEquipmentId);

    private sealed class TestClient:IDisposable
    {
        private readonly ENetMultiplayerPeer _peer=new();private readonly Queue<NetworkMessageV3> _messages=new();
        public TestClient(int port){Error error=_peer.CreateClient("127.0.0.1",port);if(error!=Error.Ok)throw new InvalidOperationException(error.ToString());}
        public bool IsConnected=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Connected;
        public bool IsFailed=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Disconnected;
        public void Poll(){if(IsFailed)return;_peer.Poll();while(_peer.GetAvailablePacketCount()>0)if(NetworkProtocolV3.TryDeserialize(_peer.GetPacket(),out var message)&&message!=null)_messages.Enqueue(message);}
        public bool Send(NetworkMessageV3 message){if(!IsConnected)return false;_peer.SetTargetPeer(1);return _peer.PutPacket(NetworkProtocolV3.Serialize(message))==Error.Ok;}
        public bool TryTake(NetworkMessageTypeV3 type,out NetworkMessageV3? message){message=null;int count=_messages.Count;for(int i=0;i<count;i++){NetworkMessageV3 current=_messages.Dequeue();if(message==null&&current.MessageType==type)message=current;else _messages.Enqueue(current);}return message!=null;}
        public void Close()=>_peer.Close();public void Dispose()=>_peer.Dispose();
    }
}
