using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using Godot;
using WorldV2;

namespace GameplayV3.Construction;

public readonly record struct DoorPassageSelfCheckResultV3(bool Passed,string Summary,int RegisteredDoorCount,int PassageEventCount,double PerformanceMilliseconds);

public static class DoorPassageSelfCheckV3
{
    public static DoorPassageSelfCheckResultV3 Run()
    {
        List<string> failures=new();void Check(bool value,string name){if(!value)failures.Add(name);}
        Rect2I bounds=new(0,0,8,4);ConstructionSessionV3 session=new();
        session.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.WoodenDoorId,out StructureDefinitionV3? definition);
        Check(definition!=null&&definition.MovementKind==StructureMovementKindV3.PassableDoor&&definition.RoomBoundaryKind==StructureRoomBoundaryKindV3.Door,"door metadata");
        Check(definition?.RequiredMaterials.Count==1&&definition.RequiredMaterials[0].ResourceType==ResourceTypeV3.Wood&&definition.RequiredMaterials[0].RequiredAmount==2&&Mathf.IsEqualApprox(definition.BaseConstructionDurationSeconds,3)&&Mathf.IsEqualApprox(definition.BaseDemolitionDurationSeconds,2),"door definition values");
        StructureStateV3 door=CreateDoor(new(3,1),StructureOrientationV3.Deg90);long before=session.Structures.OccupancyRevision;
        Check(session.Structures.TryRegister(door,session.Blueprints,bounds,out _),"door structure registration");long afterRegister=session.Structures.OccupancyRevision;
        Check(afterRegister==before+1&&session.DoorPassages.Count==1&&!session.Structures.IsMovementBlocked(door.AnchorCell),"door occupancy and passage registration");
        Check(session.DoorPassages.AcquirePassage(door.AnchorCell,"merc_A","SelfCheck",out _),"first acquire");
        Check(session.DoorPassages.AcquirePassage(door.AnchorCell,"merc_A","SelfCheck",out _)&&session.DoorPassages.ActivePassageUserCount==1,"duplicate acquire idempotent");
        session.DoorPassages.AdvanceClock(.12);session.DoorPassages.TryGet(door.StructureId,out DoorPassageStateV3? passage);
        Check(passage?.State==DoorStateV3.Open&&session.Structures.OccupancyRevision==afterRegister,"open without occupancy revision");
        Check(session.DoorPassages.AcquirePassage(door.AnchorCell,"merc_B","SelfCheck",out _)&&passage?.ActivePassageUserCount==2,"multiple passage users");
        session.DoorPassages.ReleasePassage("merc_A",out _);Check(passage?.State==DoorStateV3.Open&&passage.ActivePassageUserCount==1,"first release keeps open");
        session.DoorPassages.ReleasePassage("merc_B",out _);session.DoorPassages.AdvanceClock(.75);Check(passage?.State==DoorStateV3.Closing,"last release delayed close");session.DoorPassages.AdvanceClock(.12);Check(passage?.State==DoorStateV3.Closed,"close transition");
        Check(session.DoorPassages.TrySetMode(door.StructureId,DoorModeV3.HoldOpen,out _),"hold-open mode");session.DoorPassages.AdvanceClock(.12);Check(passage?.State==DoorStateV3.Open&&passage.Mode==DoorModeV3.HoldOpen,"hold-open state");
        session.DoorPassages.OnSegmentStarting("merc_C",new(new Vector2I(2,1)),door.AnchorCell);Check(passage?.ActivePassageUserCount==1,"movement segment acquire");session.DoorPassages.OnMovementEnded("merc_C");Check(passage?.ActivePassageUserCount==0,"movement completion cleanup");

        ConstructionSessionV3 corridor=new();StructureStateV3 corridorDoor=CreateDoor(new(3,1),StructureOrientationV3.Deg0);corridor.Structures.TryRegister(corridorDoor,corridor.Blueprints,bounds,out _);
        corridor.Structures.TryRegister(CreateWall(new(3,0)),corridor.Blueprints,bounds,out _);corridor.Structures.TryRegister(CreateWall(new(3,2)),corridor.Blueprints,bounds,out _);
        DynamicStructureNavigationQueryV3 query=new(new FlatQuery(bounds),corridor.Structures);MercenaryPathRequestV3 request=new("door_path","merc",new(new Vector2I(0,1)),new(new Vector2I(7,1)),1,1){NavigationOccupancyRevision=query.OccupancyRevision};MercenaryPathfindingSchedulerV3 scheduler=new(new(){MaxMillisecondsPerTick=100});scheduler.Enqueue(request);MercenaryPathResultV3? result=null;
        for(int i=0;i<64&&result==null;i++){var tick=scheduler.Tick(query,_=>true,out _);if(tick.Count>0)result=tick[0];}
        Check(result?.Success==true&&result.Path.Any(x=>x.Value==corridorDoor.AnchorCell.Value),"incremental A-star uses closed door corridor");
        long corridorRevision=corridor.Structures.OccupancyRevision;corridor.DoorPassages.AcquirePassage(corridorDoor.AnchorCell,"merc",nameof(DoorPassageSelfCheckV3),out _);corridor.DoorPassages.AdvanceClock(.12);corridor.DoorPassages.ReleasePassage("merc",out _);corridor.DoorPassages.AdvanceClock(.87);Check(corridor.Structures.OccupancyRevision==corridorRevision,"door transitions preserve occupancy revision");
        Check(corridor.Structures.TryRemove(corridorDoor.StructureId,out _)&&corridor.DoorPassages.Count==0&&corridor.Structures.OccupancyRevision==corridorRevision+1,"door removal cleanup and revision");

        ConstructionSessionV3 demolition=new();ResourceSessionV3 demolitionResources=new();StructureStateV3 demolishedDoor=CreateDoor(new(2,2),StructureOrientationV3.Deg180);demolition.Structures.TryRegister(demolishedDoor,demolition.Blueprints,bounds,out _);string demolitionWork=GameplayV3.Work.WorkRequestIdFactoryV3.Create();
        Check(definition!=null&&demolition.Demolitions.TryDesignate(demolishedDoor,definition,"company",out _,out _,out _)&&demolition.DemolitionReservations.TryReserve(new(demolishedDoor.StructureId,demolitionWork,"merc","company",DateTime.UtcNow,1),out _)&&demolition.Demolitions.TryBeginDemolition(demolishedDoor.StructureId,out _)&&demolition.Demolitions.TryAddProgress(demolishedDoor.StructureId,2,out _),"door demolition setup");
        DemolitionCompletionResultV3 demolished=DemolitionCompletionServiceV3.TryComplete(demolition,demolitionResources,demolishedDoor.StructureId,demolitionWork,bounds,2);Check(demolished.Succeeded&&demolition.Structures.Count==0&&demolition.DoorPassages.Count==0&&demolitionResources.GroundStacks.GetTotalAmount(ResourceTypeV3.Wood)==2,"door demolition uses common salvage");

        foreach(WorldMapSizePresetV2 preset in Enum.GetValues<WorldMapSizePresetV2>()){Rect2I presetBounds=WorldMapSizeDefinitionV2.FromPreset(preset).CellBounds;Vector2I center=presetBounds.GetCenter();ConstructionSessionV3 presetSession=new();StructureStateV3 presetDoor=CreateDoor(center,StructureOrientationV3.Deg270);DynamicStructureNavigationQueryV3 presetQuery=new(new FlatQuery(presetBounds),presetSession.Structures);Check(presetSession.Structures.TryRegister(presetDoor,presetSession.Blueprints,presetBounds,out _)&&presetSession.DoorPassages.Count==1&&presetQuery.IsWalkable(center),$"{preset} door smoke");}

        DoorPassageRegistryV3 perf=new();Stopwatch sw=Stopwatch.StartNew();List<StructureStateV3> perfDoors=new(1000);for(int i=0;i<1000;i++){StructureStateV3 item=CreateDoor(new(i,10),StructureOrientationV3.Deg0);perfDoors.Add(item);Check(perf.TryRegister(item,out _),"1000 door registration");}
        for(int i=0;i<200;i++){perf.AcquirePassage(perfDoors[i].AnchorCell,$"merc_{i}","Performance",out _);perf.ReleasePassage($"merc_{i}",out _);}perf.AdvanceClock(.75);perf.AdvanceClock(.12);sw.Stop();
        Check(perf.Count==1000&&perf.ActivePassageUserCount==0&&perf.Diagnostics.FullDoorScanCount==0,"bounded 1000-door passage runtime");
        return new(failures.Count==0,failures.Count==0?"PASS":"FAIL: "+string.Join(" | ",failures),perf.Count,200,sw.Elapsed.TotalMilliseconds);
    }
    private static StructureStateV3 CreateDoor(Vector2I cell,StructureOrientationV3 rotation)=>new(StructureIdFactoryV3.Create(),"company",StructureDefinitionCatalogV3.WoodenDoorId,new(cell),rotation,new[]{new GlobalCellCoord(cell)},new[]{new StructureMaterialRequirementV3(ResourceTypeV3.Wood,2)},false,DateTime.UtcNow,StructureMovementKindV3.PassableDoor,StructureRoomBoundaryKindV3.Door);
    private static StructureStateV3 CreateWall(Vector2I cell)=>new(StructureIdFactoryV3.Create(),"company",StructureDefinitionCatalogV3.WoodenWallId,new(cell),StructureOrientationV3.Deg0,new[]{new GlobalCellCoord(cell)},new[]{new StructureMaterialRequirementV3(ResourceTypeV3.Wood,5)},true,DateTime.UtcNow,StructureMovementKindV3.Blocking,StructureRoomBoundaryKindV3.Solid);
    private sealed class FlatQuery:IMercenaryNavigationWorldQueryV3
    {private readonly Rect2I _bounds;public FlatQuery(Rect2I bounds)=>_bounds=bounds;public bool IsInsideWorld(Vector2I cell)=>_bounds.HasPoint(cell);public MercenaryNavigationCellInfoV3 GetCellInfo(Vector2I cell)=>new(IsInsideWorld(cell),IsInsideWorld(cell),1,TileType.Grass,BiomeKindV3.Plains);public bool IsWalkable(Vector2I cell)=>IsInsideWorld(cell);public float GetTraversalMultiplier(Vector2I cell)=>1;}
}
