using System;
using System.Diagnostics;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using Godot;
using WorldV2;

namespace GameplayV3.Construction;

public static class FloorSelfCheckV3
{
    public static string LastPerformanceSummary { get; private set; } = string.Empty;

    public static bool TryValidate(out string reason)
    {
        try
        {
            var catalog=new FloorDefinitionCatalogV3();
            Check(catalog.TryGet(FloorDefinitionCatalogV3.WoodenFloorId,out var definition)&&definition!=null,"definition missing");
            Check(definition!.ResourceType==ResourceTypeV3.Wood&&definition.ResourceCost==1,"cost drift");
            Check(Mathf.IsEqualApprox(definition.BaseConstructionDurationSeconds,2f)&&Mathf.IsEqualApprox(definition.BaseDemolitionDurationSeconds,1.5f),"duration drift");
            Check(Mathf.IsEqualApprox(definition.MovementSpeedMultiplier,1.10f)&&definition.IsPassable,"movement definition drift");

            Rect2I bounds=new(-100,-100,200,200);var registry=new FloorRegistryV3();GlobalCellCoord cell=new(new Vector2I(3,4));
            Check(registry.TryCreateBlueprint("company",cell,definition,bounds,out var blueprint,out reason)&&blueprint!=null,"blueprint create: "+reason);
            Check(!registry.TryCreateBlueprint("company",cell,definition,bounds,out _,out _),"duplicate blueprint accepted");
            Check(blueprint!.MaterialBuffer.TryDeliver(ResourceTypeV3.Wood,1,out _),"material delivery");blueprint.RefreshMaterialStatus();long movementBefore=registry.MovementCostRevision;
            Check(registry.TryCompleteBlueprint(blueprint.BlueprintId,out var floor,out reason)&&floor!=null,"floor completion: "+reason);
            Check(registry.Count==1&&registry.BlueprintCount==0&&registry.TryGetFloor(cell,out _),"completion indexes");
            Check(registry.MovementCostRevision==movementBefore+1,"movement revision completion");
            Check(registry.TryMarkDemolition("company",cell,out var mark,out _,out reason)&&mark!=null,"mark: "+reason);
            Check(registry.TryBeginDemolition(mark!.MarkId)&&registry.TryAddDemolitionProgress(mark.MarkId,1.5f),"demolition progress");movementBefore=registry.MovementCostRevision;
            Check(registry.TryCompleteDemolition(mark.MarkId,out _,out reason)&&registry.Count==0,"demolition completion: "+reason);
            Check(registry.MovementCostRevision==movementBefore+1,"movement revision removal");
            Check(registry.Diagnostics.PerCellNodeCount==0&&registry.Diagnostics.PerCellProcessCount==0&&registry.Diagnostics.FullFloorRegistryScanCount==0&&registry.Diagnostics.FullFloorBlueprintScanCount==0,"forbidden per-cell runtime");

            var navRegistry=new FloorRegistryV3();navRegistry.TryRegisterCompletedFloorForFixture("company",cell,definition.FloorTypeId);var floorQuery=new FloorNavigationQueryV3(new FlatQuery(bounds),navRegistry,catalog);
            Check(floorQuery.IsWalkable(cell.Value)&&Mathf.IsEqualApprox(floorQuery.GetTraversalMultiplier(cell.Value),1f/1.10f),"floor traversal cost");
            Check(floorQuery.OccupancyRevision==0,"floor changed occupancy revision");

            LastPerformanceSummary=RunFixture(1_000)+"; "+RunFixture(10_000)+"; "+RunFixture(50_000);
            reason=string.Empty;return true;
        }
        catch(Exception ex){reason=ex.Message;return false;}
    }

    private static string RunFixture(int count)
    {
        var sw=Stopwatch.StartNew();var registry=new FloorRegistryV3();
        for(int i=0;i<count;i++)Check(registry.TryRegisterCompletedFloorForFixture("fixture",new(new Vector2I(i%500,i/500)),FloorDefinitionCatalogV3.WoodenFloorId),"fixture duplicate");
        sw.Stop();Check(registry.Count==count,"fixture count");Check(registry.Diagnostics.PerCellNodeCount==0&&registry.Diagnostics.PerCellProcessCount==0&&registry.Diagnostics.FullFloorRegistryScanCount==0,"fixture scan/runtime");
        return $"{count} cells/{registry.ChunkCount} chunks/{sw.Elapsed.TotalMilliseconds:0.00}ms";
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private sealed class FlatQuery:IMercenaryNavigationWorldQueryV3
    {
        private readonly Rect2I _bounds;public FlatQuery(Rect2I bounds)=>_bounds=bounds;public bool IsInsideWorld(Vector2I cell)=>_bounds.HasPoint(cell);public bool IsWalkable(Vector2I cell)=>IsInsideWorld(cell);public float GetTraversalMultiplier(Vector2I cell)=>1f;public MercenaryNavigationCellInfoV3 GetCellInfo(Vector2I cell)=>new(IsInsideWorld(cell),IsWalkable(cell),1f,TileType.Grass,BiomeKindV3.Plains);
    }
}
