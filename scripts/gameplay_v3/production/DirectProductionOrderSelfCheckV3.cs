using System;
using GameplayV3.Company;
using GameplayV3.Construction;
using GameplayV3.Control;
using GameplayV3.Jobs;
using GameplayV3.Mercenary;
using GameplayV3.Navigation;
using GameplayV3.Production.Runtime;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Production;

public static class DirectProductionOrderSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        Rect2I bounds=new(0,0,20,20);CompanySessionV3 companies=new();if(!companies.TryInitializeLocalSinglePlayer(out _,out reason))return false;
        string player=companies.LocalPlayer!.PlayerId,company=companies.LocalContext.LocalCompanyId!;MercenarySessionV3 mercenaries=new(companies.CompanyRegistry);
        MercenaryAttributeSetV3.TryCreate(10,10,10,10,10,out var attributes,out _);MercenaryWorkSkillSetV3.TryCreate(8,8,8,8,8,8,11,out var skills,out _);string mercenaryId=MercenaryIdFactoryV3.CreateMercenaryId();DateTime now=DateTime.UtcNow;
        MercenaryProfileV3.TryCreate(mercenaryId,"Producer","placeholder",attributes,skills,now,out var profile,out _);MercenaryStateV3.TryCreate(mercenaryId,company,new(new Vector2I(2,2)),MercenaryActivityStateV3.Idle,now,out var state,out _);mercenaries.Registry.TryRegisterMercenary(profile,state,out _);
        ResourceSessionV3 resources=new();StockpileSessionV3 stockpiles=new();ConstructionSessionV3 construction=new();MercenaryControlSessionV3 control=new(9,companies,mercenaries);MercenaryWorkSessionV3 work=new(9,companies,mercenaries,resources,stockpiles,control);control.AttachWorkSession(work);
        construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.ProcessingWorkbenchId,out var definition);var footprint=StructureFootprintResolverV3.Resolve(definition!,new(new Vector2I(5,5)),StructureOrientationV3.Deg0);StructureStateV3 structure=new(StructureIdFactoryV3.Create(),company,definition!.DefinitionId,new(new Vector2I(5,5)),StructureOrientationV3.Deg0,footprint.Cells,definition.RequiredMaterials,definition.BlocksMovement,now,definition.MovementKind);if(!construction.Structures.TryRegister(structure,construction.Blueprints,bounds,out reason))return false;
        ProductionSessionV3 production=new(9,construction,resources,stockpiles);if(!production.TryAddOrder(company,structure.StructureId,"process_wood_plank",1,out _ )||!production.TryDeliverMaterial(structure.StructureId,ResourceTypeV3.Wood,5,out reason))return false;
        ProductionWorkCoordinatorV3 coordinator=new(production,construction,resources,stockpiles,work,control,mercenaries,new TestQuery(bounds),player,company);
        if(coordinator.TryIssueDirect(structure.StructureId,new[]{mercenaryId},8,out _,out reason)||reason!="InvalidSession"||coordinator.ActiveJobCount!=0){reason="Stale direct production command was not rejected.";return false;}
        if(!coordinator.TryIssueDirect(structure.StructureId,new[]{mercenaryId},9,out var snapshot,out reason)||snapshot.CommandSource!=JobCommandSourceV3.DirectOrder||coordinator.DirectJobCount!=1){reason="Direct production command was not created.";return false;}
        if(coordinator.TryIssueDirect(structure.StructureId,new[]{mercenaryId},9,out _,out reason)||reason!="FacilityReserved"||coordinator.ActiveJobCount!=1){reason="Duplicate direct production command was not rejected.";return false;}
        coordinator.CancelForDirectMove(mercenaryId);if(coordinator.ActiveJobCount!=0||work.ActiveReservationCount!=0){reason="Direct production cancellation leaked state.";return false;}
        if(production.Diagnostics.FullFacilityScanCount!=0||production.Diagnostics.FullResourceScanCount!=0||production.Diagnostics.FullMercenaryScanCount!=0||production.Diagnostics.DuplicateJobCount!=0||production.Diagnostics.DuplicateDirectOrderCount!=0||production.Diagnostics.DirectProductionReservationLeakCount!=0||production.Diagnostics.ConservationMismatchCount!=0){reason="Production forbidden diagnostics changed.";return false;}
        production.Dispose();reason=string.Empty;return true;
    }
    private sealed class TestQuery:IMercenaryNavigationWorldQueryV3
    {private readonly Rect2I _bounds;public TestQuery(Rect2I bounds){_bounds=bounds;}public bool IsInsideWorld(Vector2I cell)=>_bounds.HasPoint(cell);public MercenaryNavigationCellInfoV3 GetCellInfo(Vector2I cell)=>new(IsInsideWorld(cell),IsWalkable(cell),1,TileType.Grass,BiomeKindV3.Plains);public bool IsWalkable(Vector2I cell)=>IsInsideWorld(cell);public float GetTraversalMultiplier(Vector2I cell)=>1;}
}
