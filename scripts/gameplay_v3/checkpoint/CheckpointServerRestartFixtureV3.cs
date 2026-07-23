using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GameplayV3.Construction;
using GameplayV3.Equipment;
using GameplayV3.Farming;
using GameplayV3.Mercenary;
using GameplayV3.Network;
using GameplayV3.Production;
using GameplayV3.Resources;
using GameplayV3.Session;
using Godot;
using WorldV2;

namespace GameplayV3.Checkpoint;

public partial class CheckpointServerRestartFixtureV3:Node
{
    private readonly List<TestClient> _clients=new();
    private DedicatedServerHostV3? _host;

    public override async void _Ready()
    {
        bool pass;string summary;
        try{summary=await Run();pass=true;}catch(Exception exception){pass=false;summary=exception.ToString();}
        finally{foreach(TestClient client in _clients)client.Dispose();_clients.Clear();_host?.Dispose();}
        GD.Print($"[CheckpointRestartV3] PASS={pass} {summary}");GetTree().Quit(pass?0:3);
    }

    private async Task<string> Run()
    {
        string directory=Path.Combine(Path.GetTempPath(),"mercenary_rpg_checkpoint_fixture_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);string path=Path.Combine(directory,"world.checkpoint.json");
        ServerCheckpointStoreV3 store=new();_host=new();int port1=await StartNew(43210);
        TestClient a=await Connect(port1),b=await Connect(port1);
        Send(a,Hello(1,"checkpoint_account_a"));NetworkMessageV3 helloA=await Wait(a,NetworkMessageTypeV3.ServerHelloAccepted);
        Send(b,Hello(1,"checkpoint_account_b"));NetworkMessageV3 helloB=await Wait(b,NetworkMessageTypeV3.ServerHelloAccepted);
        Check(helloA.CompanyId!=helloB.CompanyId,"Companies were not distinct.");
        Check(_host.TryGetDevelopmentCompany("checkpoint_account_a",out PlayerCompanyStateV3? companyA)&&companyA!=null,"Company A missing.");
        Check(_host.TryGetDevelopmentCompany("checkpoint_account_b",out PlayerCompanyStateV3? companyB)&&companyB!=null,"Company B missing.");
        string estateA=_host.GetEstateRegionId(companyA!),estateB=_host.GetEstateRegionId(companyB!);
        RegionPersistentStateV3 shared=_host.CreateOrGetSharedNeutralRegion();
        Send(a,Join(2,estateA));NetworkMessageV3 joinA=await Wait(a,NetworkMessageTypeV3.JoinRegionAccepted);
        Send(b,Join(2,estateB));NetworkMessageV3 joinB=await Wait(b,NetworkMessageTypeV3.JoinRegionAccepted);
        Check(_host.RegionSessions!.ActiveSessionCount>=2,"Two regions were not active before checkpoint.");
        RegionPersistentStateV3 regionA=_host.World!.Regions[estateA];
        string mercA=AddMercenary(companyA!,estateA,new(4,4),"Checkpoint A");
        string mercB=AddMercenary(companyB!,estateB,new(5,5),"Checkpoint B");
        _host.RegionSessions.AddRuntimeMercenary(estateA,mercA);_host.RegionSessions.AddRuntimeMercenary(estateB,mercB);
        string resourceId=AddNode(regionA,new(12,8),7);
        Check(regionA.GroundResourceStacks.TryAddStack(ResourceTypeV3.IronOre,5,new(new Vector2I(8,8)),out GroundResourceStackV3? stack,out _,out string reason)&&stack!=null,reason);
        string stockpileId=AddStockpile(regionA,companyA!.CompanyId,new(8,8));
        (string structureId,string orderId)=AddProduction(regionA,companyA.CompanyId,mercA);
        string blueprintId=AddBlueprint(regionA,companyA.CompanyId,new(15,15));
        string farmId=AddFarm(regionA,companyA.CompanyId,new(18,18));
        companyA.Equipment.AttachRegionLocationStore(regionA.EquipmentLocations);
        string equippedId=Equip(companyA,mercA,estateA,"iron_sword",EquipmentSlotV3.MainHand);
        string groundEquipmentId=CreateGroundEquipment(companyA,mercA,estateA,new(10,10));
        string storedEquipmentId=CreateStoredEquipment(companyA,mercA,stockpileId,new(9,8),new(8,8));
        string holdingEquipmentId=CreateHoldingEquipment(companyA,mercA);
        string outputEquipmentId=CreateFacilityOutputEquipment(companyA,mercA,estateA,structureId);
        _host.RegionSessions.TickActiveRegions(2.5);
        double savedWorldTime=_host.World.WorldClock.ElapsedSimulationSeconds;
        string routeId="route_"+estateB+"_shared";
        Check(_host.World.WorldGraph.TryGetRoute(routeId,out WorldRouteV3? route)&&route!=null,"Estate B route missing.");
        Check(_host.World.TravelingGroups.TryCreate(companyB!.CompanyId,new[]{mercB},Array.Empty<string>(),estateB,shared.RegionId,routeId,savedWorldTime,savedWorldTime+300,out TravelingGroupStateV3? traveling,out reason)&&traveling!=null,reason);
        Check(companyB.SetMercenaryTraveling(mercB,traveling!.TravelingGroupId),"Travel presence failed.");
        Check(_host.RegionSessions.RemoveRuntimeMercenary(estateB,mercB),"Travel runtime removal failed.");
        long oldEstateRevision=joinA.ActiveSessionRevision;
        Check(_host.TrySaveCheckpoint(path,store,out reason),reason);
        Check(File.Exists(path)&&new FileInfo(path).Length>0&&store.Diagnostics.SaveSuccessCount==1,"Checkpoint file was not written.");
        Send(a,Snapshot(30,estateA,joinA.ActiveSessionRevision));await Wait(a,NetworkMessageTypeV3.InitialRegionSnapshotAccepted);
        Send(a,new(){MessageType=NetworkMessageTypeV3.SubmitGameplayCommand,RequestId=3,ClientCommandSequence=1,RegionId=estateA,ExpectedSessionRevision=joinA.ActiveSessionRevision,CommandPayload=GameplayCommandDeltaProtocolV3.SerializeCommand(new(){CommandKind=GameplayCommandKindV3.CancelMercenaryOrder,MercenaryId=mercA})});
        Check((await Wait(a,NetworkMessageTypeV3.CommandAccepted)).RequestId==3,"Host 1 did not continue after checkpoint.");
        await Disconnect(a);await Disconnect(b);_host.Stop();_host.Dispose();_host=null;

        DedicatedServerHostV3 host2=new();_host=host2;int port2=await StartRestored(path,store,43310);
        Check(host2.World!.WorldId.Length>0&&Math.Abs(host2.World.WorldClock.ElapsedSimulationSeconds-savedWorldTime)<.001,"World clock did not restore.");
        Check(host2.RegionSessions!.ActiveSessionCount==0&&host2.Connections.Count==0,"Transient server state was restored.");
        Check(host2.World.Regions.Count>=3&&host2.World.PlayerCompanies.Count==2,"World ownership counts did not restore.");
        Check(host2.World.Regions[estateA].ResourceNodes.Contains(resourceId)&&host2.World.Regions[estateA].GroundResourceStacks.Contains(stack!.ResourceStackId),"Resource state did not restore.");
        Check(host2.World.Regions[estateA].Structures.Contains(structureId)&&host2.World.Regions[estateA].Blueprints.Contains(blueprintId)&&host2.World.Regions[estateA].StockpileZones.ContainsZone(stockpileId)&&host2.World.Regions[estateA].FarmPlots.TryGetPlot(farmId,out _),"Region stores did not restore.");
        Check(host2.World.Regions[estateA].Production.TryGetOrder(orderId,out ProductionOrderSnapshotV3? restoredOrder)&&restoredOrder?.RequestedBatches==2&&Math.Abs(restoredOrder.WorkProgressSeconds-.1f)<.001,"Production queue/progress did not restore.");
        Check(host2.World.Regions[estateA].Farming.Plots.TryGetCrop(new(new Vector2I(18,18)),out CropCellStateV3? restoredCrop)&&restoredCrop?.Stage==CropStageV3.Growing&&restoredCrop.GrowthElapsedSeconds>0,"Farm growth did not restore.");
        Check(companyA!.Equipment.TryGetInstance(equippedId,out EquipmentInstanceV3? oldEquipment),"Fixture retained an invalid Host 1 equipment reference.");
        Check(host2.TryGetDevelopmentCompany("checkpoint_account_a",out PlayerCompanyStateV3? restoredA)&&restoredA!=null&&restoredA.CompanyId==helloA.CompanyId,"Account A binding changed.");
        Check(host2.TryGetDevelopmentCompany("checkpoint_account_b",out PlayerCompanyStateV3? restoredB)&&restoredB!=null&&restoredB.CompanyId==helloB.CompanyId,"Account B binding changed.");
        Check(restoredA!.Equipment.TryGetInstance(equippedId,out EquipmentInstanceV3? restoredEquipped)&&restoredEquipped?.LocationKind==EquipmentLocationKindV3.Equipped&&restoredA.EquipmentLoadouts.GetEquippedInstanceId(mercA,EquipmentSlotV3.MainHand)==equippedId,"Equipped equipment/loadout did not restore.");
        Check(restoredA.Equipment.TryGetInstance(groundEquipmentId,out EquipmentInstanceV3? restoredGround)&&restoredGround?.LocationKind==EquipmentLocationKindV3.Ground&&restoredA.Equipment.TryGetInstance(outputEquipmentId,out EquipmentInstanceV3? restoredOutput)&&restoredOutput?.LocationKind==EquipmentLocationKindV3.FacilityOutput,"Equipment locations did not restore.");
        Check(restoredA.Equipment.TryGetInstance(storedEquipmentId,out EquipmentInstanceV3? restoredStored)&&restoredStored?.LocationKind==EquipmentLocationKindV3.Storage&&restoredStored.StorageId==stockpileId&&restoredA.Equipment.TryGetInstance(holdingEquipmentId,out EquipmentInstanceV3? restoredHolding)&&restoredHolding?.LocationKind==EquipmentLocationKindV3.CompanyHolding,"Storage/holding equipment did not restore.");
        Check(restoredA.Equipment.TryValidateInvariants(out EquipmentRuntimeInvariantSnapshotV3 invariant,out reason)&&invariant.DuplicateIndexViolationCount==0&&invariant.OrphanInstanceCount==0,reason);

        TestClient a2=await Connect(port2),b2=await Connect(port2);
        Send(a2,Hello(1,"checkpoint_account_a"));NetworkMessageV3 reconnectA=await Wait(a2,NetworkMessageTypeV3.ServerHelloAccepted);
        Send(b2,Hello(1,"checkpoint_account_b"));NetworkMessageV3 reconnectB=await Wait(b2,NetworkMessageTypeV3.ServerHelloAccepted);
        Check(reconnectA.CompanyId==helloA.CompanyId&&reconnectB.CompanyId==helloB.CompanyId&&!string.IsNullOrWhiteSpace(reconnectB.TravelingGroupId),
            $"Reconnect binding/travel state failed A={reconnectA.CompanyId}/{helloA.CompanyId} B={reconnectB.CompanyId}/{helloB.CompanyId} travel={reconnectB.TravelingGroupId} activeGroups={host2.World.TravelingGroups.ActiveCount}.");
        Send(a2,Join(2,estateA));NetworkMessageV3 restoredJoin=await Wait(a2,NetworkMessageTypeV3.JoinRegionAccepted);Check(restoredJoin.ActiveSessionRevision>oldEstateRevision,"Session revision regressed.");
        Send(a2,Snapshot(3,estateA,restoredJoin.ActiveSessionRevision));NetworkMessageV3 snapshotMessage=await Wait(a2,NetworkMessageTypeV3.InitialRegionSnapshotAccepted);NetworkClientSessionV3 replicaA=new();
        Check(replicaA.ApplyServerHello(reconnectA)&&replicaA.ApplyRegionJoin(restoredJoin),"Replica handshake/join failed.");Check(replicaA.TryApplyInitialSnapshot(snapshotMessage,out NetworkRejectReasonV3 snapshotReason),snapshotReason.ToString());
        Check(replicaA.CurrentRegionReplica!.ResourceNodes.ContainsKey(resourceId)&&replicaA.CurrentRegionReplica.Equipment.ContainsKey(equippedId),"Restored snapshot did not match checkpoint.");
        NetworkMessageV3 arrived=await Wait(b2,NetworkMessageTypeV3.RegionTransferArrived);Check(arrived.TravelingGroupId==traveling.TravelingGroupId,"Restored traveling group did not arrive exactly once.");
        Check(host2.World.TravelingGroups.TryGet(traveling.TravelingGroupId,out TravelingGroupStateV3? arrivedGroup)&&arrivedGroup?.State==TravelingGroupStatusV3.Arrived,"Travel group state was not arrived.");
        int arrivedRevision=(int)arrivedGroup!.Revision;for(int i=0;i<20;i++){host2.Poll();b2.Poll();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}Check(arrivedGroup.Revision==arrivedRevision,"Travel group arrived more than once.");
        Send(a2,new(){MessageType=NetworkMessageTypeV3.SubmitGameplayCommand,RequestId=4,ClientCommandSequence=1,RegionId=estateA,ExpectedSessionRevision=restoredJoin.ActiveSessionRevision,CommandPayload=GameplayCommandDeltaProtocolV3.SerializeCommand(new(){CommandKind=GameplayCommandKindV3.MoveMercenary,MercenaryId=mercA,TargetCell=new(6,6)})});
        Check((await Wait(a2,NetworkMessageTypeV3.CommandAccepted)).RequestId==4,"Restored command gateway failed.");
        await PumpUntil(()=>{while(a2.TryTake(NetworkMessageTypeV3.RegionDeltaBatch,out NetworkMessageV3? delta)&&delta!=null)replicaA.TryApplyRegionDelta(delta,out _);return replicaA.CurrentRegionReplica!.Mercenaries[mercA].Cell==new SnapshotCellV3(6,6);});
        string newEquipment=CreateGroundEquipment(restoredA,mercA,estateA,new(11,11));Check(newEquipment!=groundEquipmentId&&newEquipment!=outputEquipmentId&&newEquipment!=equippedId,"Equipment ID collided after restore.");

        ValidateCorruptFiles(path,directory,store);
        await Disconnect(a2);await Disconnect(b2);host2.Stop();
        Check(store.Diagnostics.PartialWorldApplyCount==0,$"Partial world apply occurred: {store.Diagnostics.PartialWorldApplyCount}.");
        try{Directory.Delete(directory,true);}catch{}
        return $"bytes={store.Diagnostics.LastCheckpointByteSize} companies=2 regions={host2.World.Regions.Count} mercenaries=2 equipment={invariant.InstanceCount}+1 production=1 farm=1 travel=1 arrivalOnce=1 activeAfterLoad=0 checksumRejects={store.Diagnostics.ChecksumFailureCount} partialApply=0";
    }

    private static string AddMercenary(PlayerCompanyStateV3 company,string regionId,Vector2I cell,string name)
    {
        MercenaryAttributeSetV3.TryCreate(11,10,9,12,8,out var attributes,out _);MercenaryWorkSkillSetV3.TryCreate(7,8,9,10,11,6,5,out var skills,out _);
        string id=MercenaryIdFactoryV3.CreateMercenaryId();DateTime now=DateTime.UtcNow;MercenaryProfileV3.TryCreate(id,name,"placeholder",attributes,skills,now,out var profile,out _);
        MercenaryStateV3.TryCreate(id,company.CompanyId,new(cell),MercenaryActivityStateV3.Idle,now,out var state,out _);Check(company.MercenaryProfiles.TryRegisterMercenary(profile,state,out string reason),reason);Check(company.SetMercenaryRegion(id,regionId),"Presence failed.");return id;
    }
    private static string AddNode(RegionPersistentStateV3 region,Vector2I cell,int amount){string id=ResourceNodeIdFactoryV3.Create();Check(ResourceNodeStateV3.TryCreate(id,ResourceNodeTypeV3.IronVein,new(cell),amount,amount,1,new(0,0,64,64),DateTime.UtcNow,out ResourceNodeStateV3? node,out string reason)&&node!=null,reason);Check(region.ResourceNodes.TryRegister(node,out reason),reason);return id;}
    private static string AddStockpile(RegionPersistentStateV3 region,string companyId,Vector2I cell){Check(region.Stockpiles.Zones.TryCreateZone(companyId,new[]{new GlobalCellCoord(cell)},new(0,0,64,64),out var zone,out string reason)&&zone!=null,reason);return zone!.StockpileZoneId;}
    private static (string StructureId,string OrderId) AddProduction(RegionPersistentStateV3 region,string companyId,string mercenaryId)
    {
        Check(region.Construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.ProcessingWorkbenchId,out StructureDefinitionV3? definition)&&definition!=null,"Workbench definition missing.");
        GlobalCellCoord anchor=new(new Vector2I(20,10));string id=StructureIdFactoryV3.Create();StructureStateV3 state=new(id,companyId,definition!.DefinitionId,anchor,StructureOrientationV3.Deg0,new[]{anchor,new GlobalCellCoord(new(21,10))},definition.RequiredMaterials,true,DateTime.UtcNow,definition.MovementKind,definition.RoomBoundaryKind,null,definition.BaseMaxHealth);
        Check(region.Structures.TryRegister(state,region.Blueprints,new(0,0,64,64),out string reason),reason);Check(region.Production.TryAddOrder(companyId,id,"craft_iron_sword",2,out string order),order);
        Check(region.Production.TryDeliverMaterial(id,ResourceTypeV3.IronIngot,2,out reason),reason);Check(region.Production.TryDeliverMaterial(id,ResourceTypeV3.WoodPlank,1,out reason),reason);
        Check(region.Production.TryBeginWork(id,mercenaryId,11,out _,out reason),reason);Check(region.Production.TryAdvanceWork(id,.1f,()=>null,out bool completed,out reason)&&!completed,reason);return(id,order);
    }
    private static string AddBlueprint(RegionPersistentStateV3 region,string companyId,Vector2I cell)
    {
        Check(region.Construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.WoodenWallId,out StructureDefinitionV3? definition)&&definition!=null,"Wall definition missing.");
        string id=ConstructionBlueprintIdFactoryV3.Create();ConstructionMaterialBufferV3 buffer=new(definition!.RequiredMaterials);ConstructionBlueprintStateV3 state=new(id,companyId,definition.DefinitionId,new(cell),StructureOrientationV3.Deg0,new[]{new GlobalCellCoord(cell)},buffer,DateTime.UtcNow);
        Check(region.Blueprints.TryRegister(state,region.Structures,new(0,0,64,64),out string reason),reason);return id;
    }
    private static string AddFarm(RegionPersistentStateV3 region,string companyId,Vector2I cell){Check(region.Farming.Plots.TryCreate(companyId,CropCatalogV3.PotatoCropId,new[]{new GlobalCellCoord(cell)},FarmSessionV3.MaxFarmCellsPerCompany,out FarmPlotV3? plot,out string reason)&&plot!=null,reason);Check(region.Farming.Plots.TryGetCrop(new(cell),out CropCellStateV3? crop)&&crop!=null&&crop.BeginGrowing(),"Farm crop failed.");crop!.AdvanceGrowth(12,120);return plot!.FarmPlotId;}
    private static string Equip(PlayerCompanyStateV3 company,string mercenaryId,string regionId,string definitionId,EquipmentSlotV3 slot)
    {
        const string facility="checkpoint_equip_output";Check(company.Equipment.TryCreateInstanceInFacilityOutput(definitionId,EquipmentQualityV3.Good,8,mercenaryId,11,company.CompanyId,facility,company.Equipment.SessionRevision,out EquipmentInstanceV3? item,out string reason)&&item!=null,reason);
        Check(company.Equipment.TryMoveFacilityOutputToCompanyHolding(facility,item!.EquipmentInstanceId,out reason),reason);Check(company.EquipmentLoadouts.TryEquip(mercenaryId,item.EquipmentInstanceId,company.EquipmentLoadouts.SessionRevision,out EquipmentCommandFailureV3 failure),failure.ToString());Check(company.Equipment.TrySetEquippedTravelRegion(item.EquipmentInstanceId,mercenaryId,regionId,out reason),reason);return item.EquipmentInstanceId;
    }
    private static string CreateGroundEquipment(PlayerCompanyStateV3 company,string crafter,string regionId,Vector2I cell)
    {
        const string facility="checkpoint_ground_output";Check(company.Equipment.TryCreateInstanceInFacilityOutput("iron_pickaxe",EquipmentQualityV3.Normal,4,crafter,11,company.CompanyId,facility,company.Equipment.SessionRevision,out EquipmentInstanceV3? item,out string reason)&&item!=null,reason);
        Check(company.Equipment.TryMoveFacilityOutputToGround(facility,item!.EquipmentInstanceId,cell,out reason),reason);return item.EquipmentInstanceId;
    }
    private static string CreateFacilityOutputEquipment(PlayerCompanyStateV3 company,string crafter,string regionId,string facility)
    {
        Check(company.Equipment.TryCreateInstanceInFacilityOutput("padded_armor",EquipmentQualityV3.Excellent,12,crafter,11,company.CompanyId,facility,company.Equipment.SessionRevision,out EquipmentInstanceV3? item,out string reason)&&item!=null,reason);return item!.EquipmentInstanceId;
    }
    private static string CreateStoredEquipment(PlayerCompanyStateV3 company,string mercenaryId,string stockpileId,Vector2I groundCell,Vector2I storageCell)
    {
        const string facility="checkpoint_storage_output",work="checkpoint_storage_work";
        Check(company.Equipment.TryCreateInstanceInFacilityOutput("padded_armor",EquipmentQualityV3.Normal,4,mercenaryId,11,company.CompanyId,facility,company.Equipment.SessionRevision,out EquipmentInstanceV3? item,out string reason)&&item!=null,reason);
        Check(company.Equipment.TryMoveFacilityOutputToGround(facility,item!.EquipmentInstanceId,groundCell,out reason),reason);
        Check(company.Equipment.TryReserve(item.EquipmentInstanceId,work,mercenaryId,EquipmentReservationPurposeV3.Hauling,company.Equipment.SessionRevision,out reason),reason);
        Check(company.Equipment.TryMoveGroundToStorage(item.EquipmentInstanceId,stockpileId,storageCell,company.CompanyId,work,out reason),reason);return item.EquipmentInstanceId;
    }
    private static string CreateHoldingEquipment(PlayerCompanyStateV3 company,string mercenaryId)
    {
        const string facility="checkpoint_holding_output";Check(company.Equipment.TryCreateInstanceInFacilityOutput("iron_sword",EquipmentQualityV3.Poor,2,mercenaryId,11,company.CompanyId,facility,company.Equipment.SessionRevision,out EquipmentInstanceV3? item,out string reason)&&item!=null,reason);
        Check(company.Equipment.TryMoveFacilityOutputToCompanyHolding(facility,item!.EquipmentInstanceId,out reason),reason);return item.EquipmentInstanceId;
    }
    private static void ValidateCorruptFiles(string good,string directory,ServerCheckpointStoreV3 store)
    {
        byte[] bytes=File.ReadAllBytes(good);string truncated=Path.Combine(directory,"truncated.json");File.WriteAllBytes(truncated,bytes[..(bytes.Length/2)]);Check(!store.LoadAndValidate(truncated,out _,out _),"Truncated checkpoint was accepted.");
        UTF8Encoding utf8NoBom=new(false);
        WorldCheckpointEnvelopeV3 envelope=JsonSerializer.Deserialize<WorldCheckpointEnvelopeV3>(bytes,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase})!;string checksum=Path.Combine(directory,"checksum.json");envelope.PayloadSha256=new string('0',64);File.WriteAllText(checksum,JsonSerializer.Serialize(envelope,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}),utf8NoBom);Check(!store.LoadAndValidate(checksum,out _,out string checksumReason)&&checksumReason=="CheckpointChecksumMismatch",$"Checksum mismatch result was {checksumReason}.");
        envelope=JsonSerializer.Deserialize<WorldCheckpointEnvelopeV3>(bytes,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase})!;string schema=Path.Combine(directory,"schema.json");envelope.CheckpointSchemaVersion=999;File.WriteAllText(schema,JsonSerializer.Serialize(envelope,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}),utf8NoBom);Check(!store.LoadAndValidate(schema,out _,out _),"Unsupported schema was accepted.");
        Check(store.LoadAndValidate(good,out PersistentWorldCheckpointV3? dirty,out string reason)&&dirty!=null,reason);dirty!.Equipment.Add(dirty.Equipment[0]);Check(!PersistentWorldCheckpointMapperV3.Validate(dirty,out _),"Duplicate equipment DTO was accepted.");
        File.WriteAllText(good+".tmp","incomplete",utf8NoBom);Check(store.LoadAndValidate(good,out _,out reason),reason);
    }

    private async Task<int> StartNew(int basePort){for(int i=0;i<24;i++){int port=basePort+i;if(_host!.TryStart(port,51309,out _))return port;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new InvalidOperationException("No ENet port.");}
    private async Task<int> StartRestored(string path,ServerCheckpointStoreV3 store,int basePort){for(int i=0;i<24;i++){int port=basePort+i;if(_host!.TryStartFromCheckpoint(port,path,store,out _))return port;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new InvalidOperationException("Restored host failed to start.");}
    private async Task<TestClient> Connect(int port){TestClient client=new(port);_clients.Add(client);await PumpUntil(()=>client.IsConnected);return client;}
    private async Task Disconnect(TestClient client){int expected=Math.Max(0,_host!.Connections.Count-1);Send(client,new(){MessageType=NetworkMessageTypeV3.ClientDisconnect,RequestId=long.MaxValue});await PumpUntil(()=>_host.Connections.Count==expected);client.Close();_clients.Remove(client);client.Dispose();}
    private async Task<NetworkMessageV3> Wait(TestClient client,NetworkMessageTypeV3 type){NetworkMessageV3? result=null;await PumpUntil(()=>client.TryTake(type,out result));return result!;}
    private async Task PumpUntil(Func<bool> condition){for(int i=0;i<5000;i++){_host!.Poll();foreach(TestClient client in _clients)client.Poll();if(condition())return;if(_clients.Any(x=>x.IsFailed))throw new InvalidOperationException("ENet client failed.");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new TimeoutException("Checkpoint fixture timed out.");}
    private static void Send(TestClient client,NetworkMessageV3 message){Check(client.Send(message),"ENet send failed.");}
    private static NetworkMessageV3 Hello(long request,string account)=>new(){MessageType=NetworkMessageTypeV3.ClientHello,RequestId=request,DevelopmentPlayerAccountId=account};
    private static NetworkMessageV3 Join(long request,string region)=>new(){MessageType=NetworkMessageTypeV3.JoinRegionRequest,RequestId=request,RegionId=region};
    private static NetworkMessageV3 Snapshot(long request,string region,long revision)=>new(){MessageType=NetworkMessageTypeV3.RequestInitialRegionSnapshot,RequestId=request,RegionId=region,ExpectedSessionRevision=revision};
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
