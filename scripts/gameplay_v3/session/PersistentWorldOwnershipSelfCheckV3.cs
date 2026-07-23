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
using Godot;
using WorldV2;

namespace GameplayV3.Session;

public partial class PersistentWorldOwnershipSelfCheckV3 : Node
{
    public override void _Ready()
    {
        bool pass;
        string reason;
        try{pass=TryValidate(out reason);}
        catch(Exception exception){pass=false;reason=exception.Message;}
        GD.Print($"[PersistentOwnershipV3] PASS={pass} {reason}");
        GetTree().Quit(pass?0:3);
    }

    public static bool TryValidate(out string reason)
    {
        CompanyCoreSelfCheckResultV3 companyCore=CompanyCoreSelfCheckV3.Run();
        MercenaryCoreSelfCheckResultV3 mercenaryCore=MercenaryCoreSelfCheckV3.Run();
        Check(companyCore.Passed,companyCore.Summary);
        Check(mercenaryCore.Passed,mercenaryCore.Summary);
        PersistentWorldStateV3 world=GameplaySessionV3.CreateNewPersistentWorld(12345,"ownership_fixture_v1");
        Check(world.PlayerCompanies.Count==1,"Default company authority was not registered.");
        Check(world.Regions.Count==1&&world.ActiveRegionId==PersistentWorldStateV3.InitialEstateRegionId,"Initial estate authority was not registered.");
        PlayerCompanyStateV3 company=GameplaySessionV3.GetActiveCompanyState()??throw new InvalidOperationException("Active company missing.");
        RegionPersistentStateV3 region=GameplaySessionV3.GetActiveRegionState()??throw new InvalidOperationException("Active region missing.");
        Check(region.RegionType==RegionTypeV3.PrivateEstate&&region.OwnerCompanyId==company.CompanyId,"Initial estate ownership mismatch.");
        Check(company.OwnedRegionIds.Contains(region.RegionId),"Company owned-region index mismatch.");

        string mercenaryId=AddMercenary(company);
        Check(company.MercenaryProfiles.TryGetProfile(mercenaryId,out MercenaryProfileV3? profile)&&profile!=null,"Company mercenary authority missing.");
        Check(GameplaySessionV3.TryGetMercenarySession(out MercenarySessionV3? mercenaries)&&mercenaries!=null&&ReferenceEquals(company.MercenaryProfiles,mercenaries.Registry),"Gameplay mercenary API does not return company authority.");

        Check(company.Equipment.TryCreateInstanceInFacilityOutput(
            StarterEquipmentContentV3.IronSwordDefinitionId,EquipmentQualityV3.Normal,5,mercenaryId,10,
            company.CompanyId,"fixture_output",GameplaySessionV3.SessionRevision,out EquipmentInstanceV3? equipment,out reason)&&equipment!=null,reason);
        Check(company.Equipment.TryGetInstance(equipment!.EquipmentInstanceId,out EquipmentInstanceV3? sameEquipment)&&ReferenceEquals(equipment,sameEquipment),"Equipment authority was copied.");
        Check(GameplaySessionV3.TryGetEquipmentRuntime(out EquipmentRuntimeV3? equipmentApi)&&ReferenceEquals(company.Equipment,equipmentApi),"Gameplay equipment API does not return company authority.");
        Check(region.EquipmentLocations.GetFacilityOutput("fixture_output").Single()==equipment.EquipmentInstanceId&&equipment.RegionId==region.RegionId,"Region equipment location store is not connected to company equipment authority.");

        Rect2I bounds=new(0,0,32,32);
        string nodeId=ResourceNodeIdFactoryV3.Create();
        Check(ResourceNodeStateV3.TryCreate(nodeId,ResourceNodeTypeV3.Tree,new GlobalCellCoord(new Vector2I(3,3)),10,10,2,bounds,DateTime.UtcNow,out ResourceNodeStateV3? node,out reason)&&node!=null,reason);
        Check(region.ResourceNodes.TryRegister(node,out reason),reason);
        Check(region.GroundResourceStacks.TryAddStack(ResourceTypeV3.Wood,4,new GlobalCellCoord(new Vector2I(4,3)),out _,out _,out reason),reason);
        GameplaySessionV3.TryGetConstructionSession(out ConstructionSessionV3? regionConstruction);
        StructureStateV3 bench=CreateStructure(regionConstruction!,company.CompanyId,new Vector2I(8,8));
        Check(region.Structures.TryRegister(bench,region.Blueprints,bounds,out reason),reason);
        Check(region.StockpileZones.TryCreateZone(company.CompanyId,new[]{new GlobalCellCoord(new Vector2I(10,10))},bounds,out StockpileZoneStateV3? stockpile,out reason)&&stockpile!=null,reason);
        Check(region.FarmPlots.TryCreate(company.CompanyId,CropCatalogV3.PotatoCropId,new[]{new GlobalCellCoord(new Vector2I(12,12))},FarmSessionV3.MaxFarmCellsPerCompany,out _,out reason),reason);
        Check(region.Production.GetFacilities(company.CompanyId).Any(facility=>facility.FacilityId==bench.StructureId),"Production facility was not connected to region structures.");

        Check(GameplaySessionV3.TryGetResourceSession(out ResourceSessionV3? resources)&&resources!=null&&ReferenceEquals(resources.Nodes,region.ResourceNodes)&&ReferenceEquals(resources.GroundStacks,region.GroundResourceStacks),"Resource API is not region authority.");
        Check(GameplaySessionV3.TryGetConstructionSession(out ConstructionSessionV3? construction)&&construction!=null&&ReferenceEquals(construction.Structures,region.Structures)&&ReferenceEquals(construction.Blueprints,region.Blueprints),"Construction API is not region authority.");
        Check(GameplaySessionV3.TryGetStockpileSession(out StockpileSessionV3? stockpiles)&&stockpiles!=null&&ReferenceEquals(stockpiles.Zones,region.StockpileZones),"Stockpile API is not region authority.");
        Check(GameplaySessionV3.TryGetProductionSession(out ProductionSessionV3? production)&&ReferenceEquals(production,region.Production),"Production API is not region authority.");
        Check(GameplaySessionV3.TryGetFarmSession(out FarmSessionV3? farming)&&farming!=null&&ReferenceEquals(farming.Plots,region.FarmPlots),"Farming API is not region authority.");

        string[] forbiddenPersistentProperties={"Job","Reservation","Movement","WorkRequest"};
        Check(!typeof(RegionPersistentStateV3).GetProperties().Any(property=>forbiddenPersistentProperties.Any(token=>property.Name.Contains(token,StringComparison.Ordinal))),"Transient runtime authority leaked into RegionPersistentState.");
        Check(!world.TryRegisterCompany(company,out _)&&!world.TryRegisterRegion(region,out _),"Duplicate authority registration was accepted.");

        int nodeCount=region.ResourceNodes.Count,structureCount=region.Structures.Count,stockpileCount=region.StockpileZones.Count,equipmentCount=company.Equipment.Count;
        GameplaySessionV3.DisposeActiveRegionRuntime();
        Check(!GameplaySessionV3.TryGetActiveRegionSession(out _),"Disposed ActiveRegionSession remained active.");
        Check(region.ResourceNodes.Count==nodeCount&&region.Structures.Count==structureCount&&region.StockpileZones.Count==stockpileCount&&company.Equipment.Count==equipmentCount,"Disposing ActiveRegionSession removed persistent state.");
        ActiveRegionSessionV3 replacement=GameplaySessionV3.ActivateInitialRegion();
        Check(ReferenceEquals(replacement.PersistentState,region)&&ReferenceEquals(replacement.CompanyState,company),"ActiveRegion reactivation copied persistent authority.");
        Check(replacement.Work.ActiveWorkRequestCount==0&&replacement.Work.ActiveReservationCount==0&&replacement.Jobs.Count==0,"Reactivated region retained transient work state.");

        reason=$"world={world.WorldId} company={company.CompanyId} region={region.RegionId} mercenaries={company.MercenaryProfiles.Count} equipment={company.Equipment.Count} nodes={region.ResourceNodes.Count} structures={region.Structures.Count} stockpiles={region.StockpileZones.Count}";
        return true;
    }

    private static string AddMercenary(PlayerCompanyStateV3 company)
    {
        MercenaryAttributeSetV3.TryCreate(10,10,10,10,10,out MercenaryAttributeSetV3? attributes,out _);
        MercenaryWorkSkillSetV3.TryCreate(8,8,8,8,8,8,10,out MercenaryWorkSkillSetV3? skills,out _);
        string id=MercenaryIdFactoryV3.CreateMercenaryId();
        DateTime now=DateTime.UtcNow;
        MercenaryProfileV3.TryCreate(id,"Ownership Fixture","placeholder",attributes,skills,now,out MercenaryProfileV3? profile,out _);
        MercenaryStateV3.TryCreate(id,company.CompanyId,new GlobalCellCoord(new Vector2I(2,2)),MercenaryActivityStateV3.Idle,now,out MercenaryStateV3? state,out _);
        if(!company.MercenaryProfiles.TryRegisterMercenary(profile,state,out string reason))throw new InvalidOperationException(reason);
        return id;
    }

    private static StructureStateV3 CreateStructure(ConstructionSessionV3 construction,string companyId,Vector2I anchor)
    {
        construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.ProcessingWorkbenchId,out StructureDefinitionV3? definition);
        ResolvedStructureFootprintV3 footprint=StructureFootprintResolverV3.Resolve(definition!,new GlobalCellCoord(anchor),StructureOrientationV3.Deg0);
        return new(StructureIdFactoryV3.Create(),companyId,definition!.DefinitionId,new GlobalCellCoord(anchor),StructureOrientationV3.Deg0,footprint.Cells,definition.RequiredMaterials,definition.BlocksMovement,DateTime.UtcNow,definition.MovementKind);
    }

    private static void Check(bool value,string reason)
    {
        if(!value)throw new InvalidOperationException(string.IsNullOrWhiteSpace(reason)?"Ownership self-check failed.":reason);
    }
}
