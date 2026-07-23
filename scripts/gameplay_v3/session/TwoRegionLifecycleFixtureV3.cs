using System;
using System.Linq;
using GameplayV3.Company;
using GameplayV3.Construction;
using GameplayV3.Equipment;
using GameplayV3.Farming;
using GameplayV3.Mercenary;
using GameplayV3.Production;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Session;

public partial class TwoRegionLifecycleFixtureV3:Node
{
    private const string RegionBId="region_test_frontier_001";
    private static readonly Rect2I Bounds=new(0,0,48,48);

    public override void _Ready()
    {
        bool pass;
        string summary;
        try{pass=TryValidate(out summary);}
        catch(Exception exception){pass=false;summary=exception.ToString();}
        GD.Print($"[TwoRegionLifecycleV3] PASS={pass} {summary}");
        GetTree().Quit(pass?0:3);
    }

    public static bool TryValidate(out string summary)
    {
        PersistentWorldStateV3 world=GameplaySessionV3.CreateNewPersistentWorld(51302,"two_region_fixture_v1");
        PlayerCompanyStateV3 company=GameplaySessionV3.GetActiveCompanyState()??throw new InvalidOperationException("Active company missing.");
        RegionPersistentStateV3 regionA=GameplaySessionV3.GetActiveRegionState()??throw new InvalidOperationException("Region A missing.");
        RegionPersistentStateV3 regionB=GameplaySessionV3.CreateRegion(RegionBId,RegionTypeV3.PrivateEstate,company.CompanyId,71302);
        Check(world.Regions.Count==2,"Region count mismatch.");

        string mercenaryId=AddMercenary(company);
        Check(company.SetMercenaryRegion(mercenaryId,regionA.RegionId),"Mercenary presence registration failed.");
        Check(GameplaySessionV3.SwitchActiveRegion(regionA.RegionId,out string reason),reason);
        Check(GameplaySessionV3.ActiveRegion?.ActiveRuntimeMercenaryCount==1,"Region A runtime mercenary was not rehydrated.");
        RegionFixtureState stateA=PopulateRegion(regionA,company,mercenaryId,new Vector2I(4,4));
        GameplaySessionV3.GetSimulationClock().Advance(12);
        double clockBeforeSwitch=GameplaySessionV3.GetSimulationClock().ElapsedSimulationSeconds;
        long regionARevisionBeforeCommit=regionA.RegionRevision;
        ActiveRegionSessionV3 activeA=GameplaySessionV3.ActiveRegion!;
        long staleRevision=activeA.SessionRevision;
        MercenaryWorkSessionV3 staleWork=activeA.Work;
        Check(company.Equipment.TryReserve(stateA.EquipmentId,"fixture_reservation_a",mercenaryId,EquipmentReservationPurposeV3.Hauling,staleRevision,out reason),reason);

        Check(GameplaySessionV3.SwitchActiveRegion(regionB.RegionId,out reason),reason);
        Check(regionA.RegionRevision>regionARevisionBeforeCommit&&regionA.LastCommittedWorldTime>=clockBeforeSwitch,"Region A commit metadata was not updated.");
        Check(activeA.IsDisposed&&company.Equipment.ActiveReservationCount==0,"Region A transient reservations survived deactivation.");
        ActiveRegionSessionV3 activeB=GameplaySessionV3.ActiveRegion??throw new InvalidOperationException("Region B runtime missing.");
        Check(activeB.Jobs.Count==0&&activeB.Work.ActiveWorkRequestCount==0,"Region B transient runtime did not start empty.");
        Check(activeB.ActiveRuntimeMercenaryCount==0,"Region A mercenary leaked into region B runtime.");
        Check(regionB.ResourceNodes.Count==0&&regionB.Structures.Count==0&&regionB.StockpileZones.Count==0&&regionB.Production.GetFacilities(company.CompanyId).Count==0,"Region A registries leaked into region B.");
        Check(company.Equipment.GetGroundEquipmentAtCell(stateA.EquipmentCell).Count==0,"Region A equipment was visible in region B.");
        Check(!staleWork.TryIssueGathering("none","none",Array.Empty<string>(),"none",null!,GameplaySessionV3.SessionRevision,out _,out string staleReason)&&staleReason=="InvalidSession","Stale region command was accepted.");

        RegionFixtureState stateB=PopulateRegion(regionB,company,mercenaryId,new Vector2I(20,20));
        Check(GameplaySessionV3.GetSimulationClock().ElapsedSimulationSeconds>=clockBeforeSwitch,"World clock moved backwards.");
        Check(GameplaySessionV3.SwitchActiveRegion(regionA.RegionId,out reason),reason);
        ValidateRegion(regionA,stateA,company);
        Check(regionA.LastCommittedWorldTime>=clockBeforeSwitch,"Region A commit time regressed.");
        Check(company.Equipment.GetGroundEquipmentAtCell(stateA.EquipmentCell).Single()==stateA.EquipmentId,"Region A equipment did not rehydrate.");
        Check(!company.Equipment.GetGroundEquipmentAtCell(stateB.EquipmentCell).Contains(stateB.EquipmentId),"Region B equipment leaked into region A.");
        Check(GameplaySessionV3.ActiveRegion?.ActiveRuntimeMercenaryCount==1,"Region A mercenary runtime was not recreated.");
        Check(GameplaySessionV3.ActiveRegion?.Jobs.Count==0&&GameplaySessionV3.ActiveRegion.Work.ActiveReservationCount==0,"Region A transient state was not reset.");

        Check(GameplaySessionV3.SwitchActiveRegion(regionB.RegionId,out reason),reason);
        ValidateRegion(regionB,stateB,company);
        Check(company.Equipment.GetGroundEquipmentAtCell(stateB.EquipmentCell).Single()==stateB.EquipmentId,"Region B equipment did not rehydrate.");
        Check(!company.Equipment.GetGroundEquipmentAtCell(stateA.EquipmentCell).Contains(stateA.EquipmentId),"Region A equipment leaked into region B on second switch.");
        Check(GameplaySessionV3.ActiveRegion?.Jobs.Count==0&&GameplaySessionV3.ActiveRegion.Work.ActiveWorkRequestCount==0&&company.Equipment.ActiveReservationCount==0,"Second region activation retained transient state.");
        Check(company.Equipment.TryValidateInvariants(out EquipmentRuntimeInvariantSnapshotV3 equipmentSnapshot,out reason),reason);
        Check(equipmentSnapshot.DuplicateIndexViolationCount==0&&equipmentSnapshot.OrphanInstanceCount==0,"Equipment location invariants failed.");
        Check(company.Equipment.Count==2,"Equipment company authority duplicated instances.");
        Check(world.WorldClock.ElapsedSimulationSeconds>=clockBeforeSwitch&&regionB.LastCommittedWorldTime>=clockBeforeSwitch,"Persistent world clock or commit time regressed.");

        summary=$"worlds=1 companies={world.PlayerCompanies.Count} regions={world.Regions.Count} active={world.ActiveRegionId} revision={GameplaySessionV3.SessionRevision} A={Describe(regionA)} B={Describe(regionB)} equipment={equipmentSnapshot.InstanceCount}/{equipmentSnapshot.GroundCount} jobs/reservations={GameplaySessionV3.ActiveRegion!.Jobs.Count}/{company.Equipment.ActiveReservationCount} crossRegion=0 orphan=0 duplicate=0 staleAccepted=0";
        return true;
    }

    private static RegionFixtureState PopulateRegion(RegionPersistentStateV3 region,PlayerCompanyStateV3 company,string mercenaryId,Vector2I origin)
    {
        Check(ReferenceEquals(GameplaySessionV3.GetActiveRegionState(),region),"Fixture populated an inactive region.");
        string reason;
        string nodeId=ResourceNodeIdFactoryV3.Create();
        Check(ResourceNodeStateV3.TryCreate(nodeId,ResourceNodeTypeV3.Tree,new GlobalCellCoord(origin),7,10,2,Bounds,DateTime.UtcNow,out ResourceNodeStateV3? node,out reason)&&node!=null,reason);
        Check(region.ResourceNodes.TryRegister(node,out reason),reason);
        GlobalCellCoord stackCell=new(origin+new Vector2I(1,0));
        Check(region.GroundResourceStacks.TryAddStack(ResourceTypeV3.Wood,3,stackCell,out GroundResourceStackV3? stack,out _,out reason)&&stack!=null,reason);
        StructureStateV3 structure=CreateStructure(region.Construction,company.CompanyId,origin+new Vector2I(3,0));
        Check(region.Structures.TryRegister(structure,region.Blueprints,Bounds,out reason),reason);
        GlobalCellCoord storageCell=new(origin+new Vector2I(5,0));
        Check(region.StockpileZones.TryCreateZone(company.CompanyId,new[]{storageCell},Bounds,out StockpileZoneStateV3? stockpile,out reason)&&stockpile!=null,reason);
        Check(region.FarmPlots.TryCreate(company.CompanyId,CropCatalogV3.PotatoCropId,new[]{new GlobalCellCoord(origin+new Vector2I(7,0))},FarmSessionV3.MaxFarmCellsPerCompany,out FarmPlotV3? farm,out reason)&&farm!=null,reason);
        Check(region.Production.TryAddOrder(company.CompanyId,structure.StructureId,"process_wood_plank",1,out string orderId),orderId);
        Check(region.Production.TryDeliverMaterial(structure.StructureId,ResourceTypeV3.Wood,5,out reason),reason);
        Check(region.Production.TryBeginWork(structure.StructureId,mercenaryId,8,out _,out reason),reason);
        Check(region.Production.TryAdvanceWork(structure.StructureId,2,()=>new GlobalCellCoord(origin+new Vector2I(8,0)),out bool completed,out reason)&&!completed,reason);
        Check(company.Equipment.TryCreateInstanceInFacilityOutput(StarterEquipmentContentV3.IronPickaxeDefinitionId,EquipmentQualityV3.Normal,5,mercenaryId,8,company.CompanyId,structure.StructureId,GameplaySessionV3.SessionRevision,out EquipmentInstanceV3? equipment,out reason)&&equipment!=null,reason);
        Vector2I equipmentCell=origin+new Vector2I(2,2);
        Check(company.Equipment.TryMoveFacilityOutputToGround(structure.StructureId,equipment!.EquipmentInstanceId,equipmentCell,out reason),reason);
        return new(nodeId,stack!.ResourceStackId,structure.StructureId,stockpile!.StockpileZoneId,farm!.FarmPlotId,orderId,equipment.EquipmentInstanceId,equipmentCell);
    }

    private static void ValidateRegion(RegionPersistentStateV3 region,RegionFixtureState expected,PlayerCompanyStateV3 company)
    {
        Check(region.ResourceNodes.Contains(expected.ResourceNodeId),"Resource node was not retained.");
        Check(region.GroundResourceStacks.TryGet(expected.StackId,out _),"Ground stack was not retained.");
        Check(region.Structures.TryGet(expected.StructureId,out _),"Structure was not retained.");
        Check(region.StockpileZones.TryGetZone(expected.StockpileId,out _),"Stockpile was not retained.");
        Check(region.FarmPlots.TryGetPlot(expected.FarmId,out _),"Farm plot was not retained.");
        Check(region.Production.GetQueue(expected.StructureId).Any(order=>order.OrderId==expected.OrderId&&order.State==ProductionOrderStateV3.Ready&&Math.Abs(order.WorkProgressSeconds-2)<0.001f),"Production bill or resumable progress was not retained.");
        Check(region.Production.GetFacilities(company.CompanyId).Count==1,"Production facility was duplicated or lost.");
    }

    private static string AddMercenary(PlayerCompanyStateV3 company)
    {
        MercenaryAttributeSetV3.TryCreate(10,10,10,10,10,out MercenaryAttributeSetV3? attributes,out _);
        MercenaryWorkSkillSetV3.TryCreate(8,8,8,8,8,8,8,out MercenaryWorkSkillSetV3? skills,out _);
        string id=MercenaryIdFactoryV3.CreateMercenaryId();
        DateTime now=DateTime.UtcNow;
        MercenaryProfileV3.TryCreate(id,"Two Region Fixture","placeholder",attributes,skills,now,out MercenaryProfileV3? profile,out _);
        MercenaryStateV3.TryCreate(id,company.CompanyId,new GlobalCellCoord(new Vector2I(2,2)),MercenaryActivityStateV3.Idle,now,out MercenaryStateV3? state,out _);
        Check(company.MercenaryProfiles.TryRegisterMercenary(profile,state,out string reason),reason);
        return id;
    }

    private static StructureStateV3 CreateStructure(ConstructionSessionV3 construction,string companyId,Vector2I anchor)
    {
        construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.ProcessingWorkbenchId,out StructureDefinitionV3? definition);
        ResolvedStructureFootprintV3 footprint=StructureFootprintResolverV3.Resolve(definition!,new GlobalCellCoord(anchor),StructureOrientationV3.Deg0);
        return new(StructureIdFactoryV3.Create(),companyId,definition!.DefinitionId,new GlobalCellCoord(anchor),StructureOrientationV3.Deg0,footprint.Cells,definition.RequiredMaterials,definition.BlocksMovement,DateTime.UtcNow,definition.MovementKind);
    }

    private static string Describe(RegionPersistentStateV3 region)=>$"{region.ResourceNodes.Count}/{region.Structures.Count}/{region.StockpileZones.Count}/{region.Production.GetFacilities(region.OwnerCompanyId!).Count}/{region.FarmPlots.Count}/{region.EquipmentLocations.GroundCount}";
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(string.IsNullOrWhiteSpace(reason)?"Two-region lifecycle fixture failed.":reason);}
    private sealed record RegionFixtureState(string ResourceNodeId,string StackId,string StructureId,string StockpileId,string FarmId,string OrderId,string EquipmentId,Vector2I EquipmentCell);
}
