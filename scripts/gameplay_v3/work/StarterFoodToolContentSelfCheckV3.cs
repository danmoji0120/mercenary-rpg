using System;
using System.Collections.Generic;
using GameplayV3.Needs;
using GameplayV3.Production;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using Godot;
using WorldV2;

namespace GameplayV3.Work;

public static class StarterFoodToolContentSelfCheckV3
{
    public static string LastSummary{get;private set;}=string.Empty;
    public static bool TryValidate(out string reason)
    {
        try
        {
            Check(FoodCoreSelfCheckV3.TryValidate(out reason),reason);
            FoodCatalogV3 food=new();Check(food.Count==5,"food count");Check(!food.TryGet(ResourceTypeV3.Bandage,out _)&&!food.TryGet(ResourceTypeV3.SimpleMedicine,out _)&&!food.TryGet(ResourceTypeV3.HerbPowder,out _),"medical resources must not be edible");
            string[] recipes={"cook_roasted_potato","cook_potato_stew","cook_dried_potato","craft_bandage","craft_simple_medicine","craft_iron_axe","craft_iron_pickaxe","craft_iron_hammer"};HashSet<string> outputs=new(StringComparer.Ordinal);foreach(string id in recipes){Check(StarterProcessingContentV3.TryGet(id,out var recipe)&&recipe!=null,"missing recipe "+id);string output=recipe!.OutputResource?.ToString()??recipe.OutputEquipmentDefinitionId??string.Empty;Check(output.Length>0&&outputs.Add(output),"duplicate starter output");}
            ResourceSessionV3 resources=new();StockpileSessionV3 stockpiles=new();GlobalCellCoord cell=new(new Vector2I(2,2));Check(stockpiles.Zones.TryCreateZone("company",new[]{cell},new Rect2I(0,0,8,8),out _,out _),"stockpile");Check(resources.GroundStacks.TryAddStack(ResourceTypeV3.IronAxe,1,cell,out _,out _,out _),"axe stack");
            WorkToolReservationSessionV3 tools=new(1,resources,stockpiles);Check(tools.TryReserveForGathering("company","merc_a","work_a",ResourceNodeTypeV3.Tree,out float axe)&&Math.Abs(axe-1.20f)<.001f,"axe reserve");Check(!tools.TryReserveForGathering("company","merc_b","work_b",ResourceNodeTypeV3.Tree,out float noTool)&&Math.Abs(noTool-1f)<.001f,"tool concurrency");Check(resources.GroundStacks.GetTotalAmount(ResourceTypeV3.IronAxe)==1,"tool consumed");Check(tools.Release("work_a")&&tools.TryReserveForGathering("company","merc_b","work_b",ResourceNodeTypeV3.Tree,out _),"tool release");Check(!tools.TryReserveForGathering("company","merc_c","work_c",ResourceNodeTypeV3.FiberBush,out _),"axe scope");Check(tools.Diagnostics.FullStackScanCount==0&&tools.Diagnostics.DuplicateReservationCount==0,"tool counters");tools.Dispose();Check(resources.AmountReservations.Count==0&&tools.ActiveReservationCount==0,"tool leak");
            LastSummary="PASS food=5 newRecipes=8 toolDefinitions=3 fullScans=0 leaks=0";reason=string.Empty;return true;
        }
        catch(Exception e){reason=e.Message;LastSummary="FAIL "+reason;return false;}
    }
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
}
