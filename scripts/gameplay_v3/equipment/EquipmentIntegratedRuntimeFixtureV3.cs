using System;
using System.Collections.Generic;
using System.Linq;
using GameplayV3.Company;
using GameplayV3.Construction;
using GameplayV3.Control;
using GameplayV3.Mercenary;
using GameplayV3.Navigation;
using GameplayV3.Production;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Equipment;

public partial class EquipmentIntegratedRuntimeFixtureV3 : Node
{
    public override void _Ready()
    {
        bool pass;
        string reason;
        string summary;
        try{pass=TryRun(out reason,out summary);}
        catch(Exception exception){pass=false;reason=exception.Message;summary="fixture exception";}
        GD.Print($"[EquipmentIntegratedV3] PASS={pass} {summary}");
        if(!pass)GD.PushError($"[EquipmentIntegratedV3] {reason}");
        GetTree().Quit(pass?0:3);
    }

    private static bool TryRun(out string reason,out string summary)
    {
        const long revision=41;
        Rect2I bounds=new(0,0,32,32);
        CompanySessionV3 companies=new();
        Check(companies.TryInitializeLocalSinglePlayer(out _,out reason),reason);
        string company=companies.LocalContext.LocalCompanyId!;
        MercenarySessionV3 mercenaries=new(companies.CompanyRegistry);
        string crafter=AddMercenary(mercenaries,company,"Crafter",new Vector2I(2,2),13);
        string wearer=AddMercenary(mercenaries,company,"Wearer",new Vector2I(8,8),7);
        string contender=AddMercenary(mercenaries,company,"Contender",new Vector2I(9,8),6);

        EquipmentDefinitionRegistryV3 definitions=StarterEquipmentContentV3.CreateRegistry();
        EquipmentRuntimeV3 equipment=new(revision,definitions);
        EquipmentLoadoutRuntimeV3 loadouts=new(revision,mercenaries.Registry,equipment);
        ConstructionSessionV3 construction=new();
        ResourceSessionV3 resources=new();
        StockpileSessionV3 stockpiles=new();
        ProductionSessionV3 production=new(revision,construction,resources,stockpiles);
        Check(production.AttachEquipmentRuntime(equipment,8675309,out reason),reason);
        StructureStateV3 bench=CreateStructure(construction,StructureDefinitionCatalogV3.ProcessingWorkbenchId,company,new Vector2I(5,5));
        Check(construction.Structures.TryRegister(bench,construction.Blueprints,bounds,out reason),reason);
        GlobalCellCoord storageCell=new(new Vector2I(8,8));
        Check(stockpiles.Zones.TryCreateZone(company,new[]{storageCell},bounds,out StockpileZoneStateV3? zone,out reason)&&zone!=null,reason);

        Dictionary<string,ImmutableEquipmentData> immutable=new(StringComparer.Ordinal);
        string sword=Produce("craft_iron_sword",13,new Vector2I(6,5),true);
        string armor=Produce("craft_padded_armor",13,new Vector2I(6,6),true);
        string pickaxe=Produce("craft_iron_pickaxe",13,new Vector2I(5,6),true);
        Check(equipment.Count==3,"Three equipment recipes did not produce exactly three instances.");
        Check(new HashSet<string>(new[]{sword,armor,pickaxe},StringComparer.Ordinal).Count==3,"Equipment instance IDs are not unique.");

        MoveGroundToStorage(sword,"haul_sword");
        MoveGroundToStorage(armor,"haul_armor");
        MoveGroundToStorage(pickaxe,"haul_pickaxe");

        Check(equipment.TryReserve(sword,"equip_sword",wearer,EquipmentReservationPurposeV3.Equip,revision,out reason),reason);
        Check(!equipment.TryReserve(sword,"equip_sword_conflict",contender,EquipmentReservationPurposeV3.Equip,revision,out string conflict)&&conflict=="Reserved","Concurrent equipment reservation was not rejected.");
        Check(equipment.TryGetInstance(sword,out EquipmentInstanceV3? swordBeforeEquip)&&swordBeforeEquip?.LocationKind==EquipmentLocationKindV3.Storage,"Equip command changed location before arrival.");
        Check(loadouts.TryEquipReservedAt(wearer,sword,"equip_sword",storageCell,stockpiles,revision,out EquipmentCommandFailureV3 equipFailure),equipFailure.ToString());
        EquipStored(armor,"equip_armor");
        EquipStored(pickaxe,"equip_pickaxe");
        EquipmentCombatStatSnapshotV3 combat=loadouts.BuildCombatStatSnapshot(wearer,5,2);
        Check(equipment.TryGetInstance(sword,out EquipmentInstanceV3? swordInstance)&&swordInstance!=null,"Sword vanished.");
        Check(equipment.TryGetInstance(armor,out EquipmentInstanceV3? armorInstance)&&armorInstance!=null,"Armor vanished.");
        double expectedAttack=10*EquipmentQualityResolverV3.GetQualityMultiplier(swordInstance!.Quality);
        double expectedDefense=8*EquipmentQualityResolverV3.GetQualityMultiplier(armorInstance!.Quality);
        Check(Near(combat.EquipmentAttackBonus,expectedAttack)&&Near(combat.EquipmentDefenseBonus,expectedDefense),"Combat modifier quality multiplier was not applied exactly once.");
        Check(equipment.TryGetInstance(pickaxe,out EquipmentInstanceV3? pickaxeInstance)&&pickaxeInstance!=null,"Pickaxe vanished.");
        float expectedGathering=1f+(float)(.2*EquipmentQualityResolverV3.GetQualityMultiplier(pickaxeInstance!.Quality));
        Check(Math.Abs(loadouts.GetGatheringWorkSpeedMultiplier(wearer)-expectedGathering)<.0001f,"Gathering modifier quality multiplier was not applied exactly once.");
        Check(Math.Abs(loadouts.GetGatheringWorkSpeedMultiplier(contender)-1f)<.0001f,"Equipment bonus leaked to another mercenary.");

        GlobalCellCoord dropCell=new(new Vector2I(9,9));
        Check(loadouts.TryUnequipAt(wearer,EquipmentSlotV3.Tool,dropCell,stockpiles,revision,out string unequipped,out equipFailure)&&unequipped==pickaxe,equipFailure.ToString());
        Check(equipment.IsGroundEquipment(pickaxe,dropCell.Value),"Unequipped item did not return to ground.");
        Check(Math.Abs(loadouts.GetGatheringWorkSpeedMultiplier(wearer)-1f)<.0001f,"Gathering bonus remained after unequip.");
        MoveGroundToStorage(pickaxe,"haul_pickaxe_again");
        EquipStored(pickaxe,"equip_pickaxe_again");

        string replacement=Produce("craft_iron_pickaxe",9,new Vector2I(6,5),true);
        MoveGroundToStorage(replacement,"haul_replacement");
        Check(equipment.TryReserve(replacement,"equip_replacement",wearer,EquipmentReservationPurposeV3.Equip,revision,out reason),reason);
        Check(loadouts.TryEquipReservedAt(wearer,replacement,"equip_replacement",storageCell,stockpiles,revision,out equipFailure),equipFailure.ToString());
        Check(equipment.IsStoredEquipment(pickaxe,zone!.StockpileZoneId,storageCell.Value),"Replaced tool did not return to storage.");
        Check(loadouts.GetEquippedInstanceId(wearer,EquipmentSlotV3.Tool)==replacement,"Replacement tool was not equipped.");

        string blockedOutput=Produce("craft_iron_pickaxe",11,new Vector2I(0,0),false);
        Check(equipment.IsInFacilityOutput(bench.StructureId,blockedOutput),"Blocked output did not remain in the facility buffer.");
        Check(!production.TryEjectNextEquipmentOutput(bench.StructureId,()=>null,out _),"Equipment ejected without an output cell.");
        Check(equipment.GetEquipmentOutputCount(bench.StructureId)==1&&equipment.Count==5,"Blocked output was duplicated or lost.");
        construction.Definitions.TryGetDefinition(bench.DefinitionId,out StructureDefinitionV3? benchDefinition);
        Check(benchDefinition!=null&&construction.Demolitions.TryDesignate(bench,benchDefinition,company,out _,out _,out reason),reason);
        string demolitionWork=WorkRequestIdFactoryV3.Create();
        Check(construction.DemolitionReservations.TryReserve(new(bench.StructureId,demolitionWork,wearer,company,DateTime.UtcNow,1),out reason),reason);
        Check(construction.Demolitions.TryBeginDemolition(bench.StructureId,out reason)&&construction.Demolitions.TryAddProgress(bench.StructureId,100,out reason),reason);
        DemolitionCompletionResultV3 blockedDemolition=DemolitionCompletionServiceV3.TryComplete(construction,resources,bench.StructureId,demolitionWork,bounds,1,id=>equipment.GetEquipmentOutputCount(id)>0);
        Check(!blockedDemolition.Succeeded&&blockedDemolition.FailureReason=="EquipmentOutputNotEmpty","Facility demolition was not blocked by equipment output.");
        Check(production.TryEjectNextEquipmentOutput(bench.StructureId,()=>new GlobalCellCoord(new Vector2I(4,5)),out string? released)&&released==blockedOutput,"Existing blocked output did not eject after a cell became available.");
        Check(equipment.GetEquipmentOutputCount(bench.StructureId)==0&&equipment.IsGroundEquipment(blockedOutput,new Vector2I(4,5)),"OutputBlocked release left a duplicate facility entry.");

        MercenaryControlSessionV3 logisticsControl=new(revision,companies,mercenaries);
        MercenaryWorkSessionV3 logisticsWork=new(revision,companies,mercenaries,resources,stockpiles,logisticsControl);
        logisticsControl.AttachWorkSession(logisticsWork);
        logisticsWork.AttachEquipmentRuntime(equipment);
        logisticsWork.AttachEquipmentLoadouts(loadouts);
        TestQuery query=new(bounds);
        string player=companies.LocalPlayer!.PlayerId;
        Check(logisticsWork.TryIssueEquipmentHauling(player,company,new[]{contender},blockedOutput,query,revision,out WorkRequestV3? haulingRequest,out reason)&&haulingRequest!=null,reason);
        Check(equipment.ActiveReservationCount==1&&stockpiles.CellReservations.Count==1,"Equipment hauling did not reserve source and destination.");
        Check(!logisticsWork.TryIssueDirectEquipmentEquip(player,company,wearer,blockedOutput,query,revision,out _,out string reservedReason)&&reservedReason=="Reserved","Equip command bypassed an active hauling reservation.");
        logisticsWork.CancelForDirectMove(contender);
        Check(equipment.ActiveReservationCount==0&&stockpiles.CellReservations.Count==0&&equipment.IsGroundEquipment(blockedOutput,new Vector2I(4,5)),"Cancelled hauling leaked reservation or moved equipment.");
        Check(logisticsWork.TryIssueDirectEquipmentEquip(player,company,contender,pickaxe,query,revision,out WorkRequestV3? equipRequest,out reason)&&equipRequest!=null,reason);
        Check(equipment.ActiveReservationCount==1&&equipment.IsStoredEquipment(pickaxe,zone.StockpileZoneId,storageCell.Value),"Direct equip changed storage location before movement arrival.");
        logisticsWork.CancelForDirectMove(contender);
        Check(equipment.ActiveReservationCount==0&&equipment.IsStoredEquipment(pickaxe,zone.StockpileZoneId,storageCell.Value),"Cancelled direct equip leaked reservation or moved equipment.");

        string removalFallback=Produce("craft_iron_sword",10,new Vector2I(0,0),false);
        Check(construction.Structures.TryRemove(bench.StructureId,out _),"Fixture facility removal failed.");
        Check(equipment.GetEquipmentOutputCount(bench.StructureId)==0&&equipment.IsGroundEquipment(removalFallback,bench.AnchorCell.Value),"Unexpected facility removal orphaned its equipment output.");

        foreach((string id,ImmutableEquipmentData before) in immutable)
        {
            Check(equipment.TryGetInstance(id,out EquipmentInstanceV3? current)&&current!=null,$"Equipment {id} disappeared.");
            Check(before.Equals(ImmutableEquipmentData.From(current!)),$"Immutable equipment data changed for {id}.");
        }
        Check(equipment.TryValidateInvariants(out EquipmentRuntimeInvariantSnapshotV3 diagnostics,out reason),reason);
        Check(diagnostics.InstanceCount==6&&diagnostics.LocationInvariantViolationCount==0&&diagnostics.DuplicateIndexViolationCount==0&&diagnostics.OrphanInstanceCount==0&&diagnostics.ActiveReservationCount==0,"Equipment invariant counters are non-zero.");
        Check(production.Diagnostics.ConservationMismatchCount==0,"Production conservation mismatch.");

        long persistentRevision=equipment.Revision;
        EquipmentRuntimeV3 sameRuntime=equipment;
        Check(ReferenceEquals(sameRuntime,equipment)&&equipment.Revision==persistentRevision,"View-style rebind changed equipment runtime state.");
        EquipmentRuntimeV3 staleRuntime=new(revision+1,definitions);
        Check(!staleRuntime.TryReserve(sword,"stale",wearer,EquipmentReservationPurposeV3.Equip,revision,out _),"Stale session reservation was accepted.");
        staleRuntime.Dispose();

        summary=$"instances={diagnostics.InstanceCount} facility={diagnostics.FacilityOutputCount} ground={diagnostics.GroundCount} storage={diagnostics.StorageCount} equipped={diagnostics.EquippedCount} holding={diagnostics.CompanyHoldingCount} reservations={diagnostics.ActiveReservationCount} duplicate={diagnostics.DuplicateIndexViolationCount} orphan={diagnostics.OrphanInstanceCount}";
        reason=string.Empty;
        production.Dispose();
        loadouts.Dispose();
        equipment.Dispose();
        return true;

        string Produce(string recipeId,int skill,Vector2I outputCell,bool eject)
        {
            Check(StarterProcessingContentV3.TryGet(recipeId,out ProductionRecipeDefinitionV3? recipe)&&recipe!=null,$"Missing recipe {recipeId}.");
            Check(production.TryAddOrder(company,bench.StructureId,recipeId,1,out _),$"Order rejected: {recipeId}.");
            foreach(StructureMaterialRequirementV3 input in recipe!.Inputs)
            {
                bool delivered=production.TryDeliverMaterial(bench.StructureId,input.ResourceType,input.RequiredAmount,out string operationReason);
                Check(delivered,operationReason);
            }
            bool began=production.TryBeginWork(bench.StructureId,crafter,skill,out _,out string beginReason);
            Check(began,beginReason);
            string workId="fixture_"+recipeId+"_"+equipment.Count;
            ProductionCompletionWorkerV3 worker=new(crafter,company,skill,revision,workId);
            bool advanced=production.TryAdvanceWork(bench.StructureId,recipe.BaseWorkSeconds,()=>new GlobalCellCoord(outputCell),worker,out bool completed,out string advanceReason);
            Check(advanced&&completed,advanceReason);
            IReadOnlyList<string> output=equipment.GetEquipmentOutputInstanceIds(bench.StructureId);
            Check(output.Count==1,$"Expected one facility output for {recipeId}, got {output.Count}.");
            string id=output[0];
            Check(equipment.TryGetInstance(id,out EquipmentInstanceV3? instance)&&instance!=null,$"Missing produced instance for {recipeId}.");
            Check(instance!.EquipmentDefinitionId==recipe.OutputEquipmentDefinitionId&&instance.OwnerCompanyId==company&&instance.CrafterMercenaryId==crafter&&instance.CrafterProductionSkillSnapshot==skill&&instance.CreatedSessionRevision==revision,$"Production metadata mismatch for {recipeId}.");
            immutable[id]=ImmutableEquipmentData.From(instance);
            if(eject)Check(production.TryEjectNextEquipmentOutput(bench.StructureId,()=>new GlobalCellCoord(outputCell),out string? ejected)&&ejected==id,$"Ejection failed for {recipeId}.");
            return id;
        }

        void MoveGroundToStorage(string id,string workId)
        {
            Check(equipment.TryGetInstance(id,out EquipmentInstanceV3? instance)&&instance?.GroundCell!=null,$"{id} is not on ground.");
            Vector2I ground=instance!.GroundCell!.Value;
            bool reserved=equipment.TryReserve(id,workId,wearer,EquipmentReservationPurposeV3.Hauling,revision,out string reserveReason);
            Check(reserved,reserveReason);
            bool moved=equipment.TryMoveGroundToStorage(id,zone!.StockpileZoneId,storageCell.Value,company,workId,out string moveReason);
            Check(moved,moveReason);
            Check(!equipment.IsGroundEquipment(id,ground)&&equipment.IsStoredEquipment(id,zone.StockpileZoneId,storageCell.Value),$"{id} storage index mismatch.");
        }

        void EquipStored(string id,string workId)
        {
            bool reserved=equipment.TryReserve(id,workId,wearer,EquipmentReservationPurposeV3.Equip,revision,out string reserveReason);
            Check(reserved,reserveReason);
            Check(loadouts.TryEquipReservedAt(wearer,id,workId,storageCell,stockpiles,revision,out EquipmentCommandFailureV3 failure),failure.ToString());
        }

        void Check(bool value,string message)
        {
            if(!value)throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)?"Fixture check failed.":message);
        }
    }

    private static string AddMercenary(MercenarySessionV3 mercenaries,string company,string name,Vector2I cell,int production)
    {
        MercenaryAttributeSetV3.TryCreate(10,10,10,10,10,out MercenaryAttributeSetV3? attributes,out _);
        MercenaryWorkSkillSetV3.TryCreate(8,8,8,8,8,8,production,out MercenaryWorkSkillSetV3? skills,out _);
        string id=MercenaryIdFactoryV3.CreateMercenaryId();
        DateTime now=DateTime.UtcNow;
        MercenaryProfileV3.TryCreate(id,name,"placeholder",attributes,skills,now,out MercenaryProfileV3? profile,out _);
        MercenaryStateV3.TryCreate(id,company,new GlobalCellCoord(cell),MercenaryActivityStateV3.Idle,now,out MercenaryStateV3? state,out _);
        if(!mercenaries.Registry.TryRegisterMercenary(profile,state,out string reason))throw new InvalidOperationException(reason);
        return id;
    }

    private static StructureStateV3 CreateStructure(ConstructionSessionV3 construction,string definitionId,string company,Vector2I anchor)
    {
        construction.Definitions.TryGetDefinition(definitionId,out StructureDefinitionV3? definition);
        ResolvedStructureFootprintV3 footprint=StructureFootprintResolverV3.Resolve(definition!,new GlobalCellCoord(anchor),StructureOrientationV3.Deg0);
        return new(StructureIdFactoryV3.Create(),company,definitionId,new GlobalCellCoord(anchor),StructureOrientationV3.Deg0,footprint.Cells,definition!.RequiredMaterials,definition.BlocksMovement,DateTime.UtcNow,definition.MovementKind);
    }

    private static bool Near(double a,double b)=>Math.Abs(a-b)<.000001;

    private sealed class TestQuery : IMercenaryNavigationWorldQueryV3
    {
        private readonly Rect2I _bounds;
        public TestQuery(Rect2I bounds){_bounds=bounds;}
        public bool IsInsideWorld(Vector2I cell)=>_bounds.HasPoint(cell);
        public bool IsWalkable(Vector2I cell)=>IsInsideWorld(cell);
        public MercenaryNavigationCellInfoV3 GetCellInfo(Vector2I cell)=>new(IsInsideWorld(cell),IsWalkable(cell),1,TileType.Grass,BiomeKindV3.Plains);
        public float GetTraversalMultiplier(Vector2I cell)=>1;
    }

    private readonly record struct ImmutableEquipmentData(
        string InstanceId,string DefinitionId,EquipmentQualityV3 Quality,int QualityScore,string CrafterId,int CrafterSkill,string OwnerCompanyId,long SessionRevision)
    {
        public static ImmutableEquipmentData From(EquipmentInstanceV3 instance)=>new(
            instance.EquipmentInstanceId,instance.EquipmentDefinitionId,instance.Quality,instance.QualityScore,
            instance.CrafterMercenaryId,instance.CrafterProductionSkillSnapshot,instance.OwnerCompanyId,instance.CreatedSessionRevision);
    }
}
