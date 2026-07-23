using System;
using System.Collections.Generic;
using GameplayV3.Construction;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using Godot;
using WorldV2;

namespace GameplayV3.Production;

public static class StarterProductionSelfCheckV3
{
    public static string LastSummary{get;private set;}=string.Empty;
    public static bool TryValidate(out string reason)
    {
        try
        {
            Check(StarterProcessingContentV3.GetAll().Count==17,"recipe count");
            HashSet<string> ids=new(StringComparer.Ordinal);HashSet<ResourceTypeV3> outputs=new();
            foreach(var recipe in StarterProcessingContentV3.GetAll()){Check(ids.Add(recipe.RecipeId),"recipe id duplicate");if(recipe.OutputResource is { } resource)Check(outputs.Add(resource),"output duplicate");Check(recipe.Inputs.Count>0&&recipe.Inputs.TrueForAll(x=>x.RequiredAmount>0)&&recipe.OutputAmount>0&&recipe.BaseWorkSeconds>0,"recipe amounts");}
            Check(outputs.Count==14,"processed resource count");
            ConstructionSessionV3 construction=new();ResourceSessionV3 resources=new();StockpileSessionV3 stockpiles=new();ProductionSessionV3 session=new(9,construction,resources,stockpiles);
            Rect2I bounds=new(-20,-20,40,40);StructureStateV3 bench=Make(StructureDefinitionCatalogV3.ProcessingWorkbenchId,new(0,0));StructureStateV3 furnace=Make(StructureDefinitionCatalogV3.BasicFurnaceId,new(5,0));StructureStateV3 kitchen=Make(StructureDefinitionCatalogV3.FieldKitchenId,new(0,5));StructureStateV3 apothecary=Make(StructureDefinitionCatalogV3.ApothecaryTableId,new(5,5));
            Check(construction.Structures.TryRegister(bench,construction.Blueprints,bounds,out _),"workbench registration");Check(construction.Structures.TryRegister(furnace,construction.Blueprints,bounds,out _),"furnace registration");Check(construction.Structures.TryRegister(kitchen,construction.Blueprints,bounds,out _),"kitchen registration");Check(construction.Structures.TryRegister(apothecary,construction.Blueprints,bounds,out _),"apothecary registration");Check(session.GetFacilities("company").Count==4,"facility registration");
            Check(session.TryAddOrder("company",bench.StructureId,"process_wood_plank",2,out string orderId),"add order");Check(session.TryAddOrder("company",bench.StructureId,"process_wood_plank",1,out string mergedId)&&mergedId==orderId,"merge order");Check(session.GetQueue(bench.StructureId)[0].RequestedBatches==3,"merged batches");Check(!session.TryAddOrder("company",bench.StructureId,"smelt_iron_ingot",1,out _),"facility recipe validation");
            Check(session.TryDeliverMaterial(bench.StructureId,ResourceTypeV3.Wood,5,out _),"material delivery");Check(session.TryBeginWork(bench.StructureId,"mercenary",out _,out _),"production begin");Check(session.TryAdvanceWork(bench.StructureId,5,()=>new GlobalCellCoord(2,0),out bool completed,out _)&&completed,"production complete");Check(resources.GroundStacks.GetTotalAmount(ResourceTypeV3.WoodPlank)==3,"output amount");Check(session.GetQueue(bench.StructureId)[0].CompletedBatches==1,"batch transition");
            Check(session.TryDeliverMaterial(bench.StructureId,ResourceTypeV3.Wood,5,out _),"second material delivery");Check(session.TryBeginWork(bench.StructureId,"mercenary",out _,out _),"second begin");Check(session.TryAdvanceWork(bench.StructureId,5,()=>null,out completed,out string blocked)&&blocked=="OutputBlocked","output blocked");Check(session.TryFlushOutput(bench.StructureId,()=>new GlobalCellCoord(2,0)),"output flush");Check(resources.GroundStacks.GetTotalAmount(ResourceTypeV3.WoodPlank)==6,"output conservation");
            Check(session.TryCancelOrder("company",bench.StructureId,orderId,out _),"remaining cancel");
            Check(session.TryAddOrder("company",kitchen.StructureId,"cook_roasted_potato",1,out _),"food recipe");Check(session.TryAddOrder("company",apothecary.StructureId,"craft_bandage",1,out _),"medical recipe");Check(session.TryAddOrder("company",bench.StructureId,"craft_iron_axe",1,out _),"tool recipe");
            Check(session.TryAddOrder("company",furnace.StructureId,"smelt_iron_ingot",1,out _),"furnace order");Check(session.TryDeliverMaterial(furnace.StructureId,ResourceTypeV3.IronOre,3,out _)&&session.TryDeliverMaterial(furnace.StructureId,ResourceTypeV3.Coal,1,out _),"multi material");Check(construction.Structures.TryRemove(furnace.StructureId,out _),"facility removal");Check(resources.GroundStacks.GetTotalAmount(ResourceTypeV3.IronOre)==3&&resources.GroundStacks.GetTotalAmount(ResourceTypeV3.Coal)==1,"removal refund");
            Check(session.Diagnostics.FullFacilityScanCount==0&&session.Diagnostics.FullResourceScanCount==0&&session.Diagnostics.ReservationLeakCount==0&&session.Diagnostics.ConservationMismatchCount==0,"forbidden counters");session.Dispose();Check(session.IsDisposed,"local session disposal");LastSummary="PASS recipes=15 facilities=4 queue/buffer/output/refund/conservation/local-lifecycle";reason=string.Empty;return true;
            StructureStateV3 Make(string definitionId,Vector2I anchor){construction.Definitions.TryGetDefinition(definitionId,out var definition);var footprint=StructureFootprintResolverV3.Resolve(definition!,new(anchor),StructureOrientationV3.Deg0);return new(StructureIdFactoryV3.Create(),"company",definitionId,new(anchor),StructureOrientationV3.Deg0,footprint.Cells,definition!.RequiredMaterials,definition.BlocksMovement,DateTime.UtcNow,definition.MovementKind);}
        }
        catch(Exception e){reason=e.Message;LastSummary="FAIL "+reason;return false;}
    }
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static bool TrueForAll(this IReadOnlyList<StructureMaterialRequirementV3> values,Func<StructureMaterialRequirementV3,bool> predicate){foreach(var value in values)if(!predicate(value))return false;return true;}
}
