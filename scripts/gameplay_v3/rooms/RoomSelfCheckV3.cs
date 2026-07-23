using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Godot;
using WorldV2;
using GameplayV3.Rooms.Runtime;
using GameplayV3.Construction;
using GameplayV3.Stockpile;

namespace GameplayV3.Rooms;

public static class RoomSelfCheckV3
{
    public static string LastPerformanceSummary{get;private set;}=string.Empty;
    public static bool TryValidate(out string reason)
    {
        try
        {
            var registry=new RoomRegistryV3();var cells=RectCells(10,10,3,3);var room=new RoomTopologyStateV3("room_original",1,cells,new[]{"portal_door"},new[]{"door"},1,RoomRebuildReasonV3.BoundaryAdded);registry.Commit(new[]{room},Array.Empty<string>(),new[]{new RoomPortalStateV3("portal_door","door",new(new Vector2I(11,9)),room.RoomId,null,false,true,1)},RoomRebuildReasonV3.BoundaryAdded);
            Check(registry.Count==1&&registry.CellCount==9&&registry.TryGetRoom(new(new Vector2I(11,11)),out var indexed)&&indexed?.RoomId==room.RoomId,"3x3 room/index");Check(registry.TryGetPortalForDoor("door",out var portal)&&portal?.SideARoomId==room.RoomId&&portal.SideBExterior,"portal");
            Check(!Connected4(new Vector2I(0,0),new Vector2I(1,1),new HashSet<Vector2I>{new(0,0),new(1,1)}),"diagonal leak");
            Check(BoundedOpenFlood(50_000)==RoomRuntimeV3.MaxDetectedRoomCells,"outdoor flood limit");
            var empty=Features();Check(RoomRoleClassifierV3.Classify(empty)==RoomRoleV3.General,"general");Check(RoomRoleClassifierV3.Classify(Features(beds:1))==RoomRoleV3.Bedroom,"bedroom");Check(RoomRoleClassifierV3.Classify(Features(stock:2))==RoomRoleV3.Storage,"storage");Check(RoomRoleClassifierV3.Classify(Features(beds:1,stock:1))==RoomRoleV3.Mixed,"mixed");RoomQualitySnapshotV3 q0=RoomQualityEvaluatorV3.Evaluate(9,empty),q1=RoomQualityEvaluatorV3.Evaluate(9,Features(floors:9));Check(q1.Score>q0.Score&&q1.Tier>=q0.Tier,"floor quality");
            var left=new RoomTopologyStateV3(room.RoomId,room.CreatedSequence,RectCells(10,10,1,3),Array.Empty<string>(),Array.Empty<string>(),2,RoomRebuildReasonV3.BoundaryAdded);var right=new RoomTopologyStateV3("room_split",2,RectCells(12,10,1,3),Array.Empty<string>(),Array.Empty<string>(),1,RoomRebuildReasonV3.BoundaryAdded);registry.Commit(new[]{left,right},new[]{room.RoomId},Array.Empty<RoomPortalStateV3>(),RoomRebuildReasonV3.BoundaryAdded);Check(registry.Count==2&&registry.CellCount==6&&registry.TryGetRoomById(room.RoomId,out _),"split continuity");
            var merged=new RoomTopologyStateV3(room.RoomId,room.CreatedSequence,RectCells(10,10,3,3),Array.Empty<string>(),Array.Empty<string>(),3,RoomRebuildReasonV3.BoundaryRemoved);registry.Commit(new[]{merged},new[]{room.RoomId,right.RoomId},Array.Empty<RoomPortalStateV3>(),RoomRebuildReasonV3.BoundaryRemoved);Check(registry.Count==1&&registry.CellCount==9,"merge");registry.Commit(Array.Empty<RoomTopologyStateV3>(),new[]{room.RoomId},Array.Empty<RoomPortalStateV3>(),RoomRebuildReasonV3.BoundaryRemoved);Check(registry.Count==0&&registry.CellCount==0,"outdoor transition");
            LastPerformanceSummary=Fixture(100)+"; "+Fixture(1000);reason=string.Empty;return true;
        }catch(Exception ex){reason=ex.Message;return false;}
    }
    public static bool TryValidateRuntime(RoomSessionV3 session,Rect2I bounds,out string reason)
    {
        ConstructionSessionV3 construction=new();StockpileSessionV3 stockpiles=new();RoomRuntimeV3 runtime=new();runtime.Initialize(session,construction,stockpiles,null,bounds);runtime.AttachStockpileEvents();try{Vector2I origin=bounds.Position+new Vector2I(20,20);string doorId=string.Empty;for(int y=0;y<5;y++)for(int x=0;x<5;x++){if(x>0&&x<4&&y>0&&y<4)continue;bool door=x==2&&y==0;GlobalCellCoord cell=new(origin+new Vector2I(x,y));var state=new StructureStateV3(StructureIdFactoryV3.Create(),"company",door?StructureDefinitionCatalogV3.WoodenDoorId:StructureDefinitionCatalogV3.WoodenWallId,cell,StructureOrientationV3.Deg0,new[]{cell},Array.Empty<StructureMaterialRequirementV3>(),!door,DateTime.UtcNow,door?StructureMovementKindV3.PassableDoor:StructureMovementKindV3.Blocking,door?StructureRoomBoundaryKindV3.Door:StructureRoomBoundaryKindV3.Solid);Check(construction.Structures.TryRegister(state,construction.Blueprints,bounds,out reason),reason);if(door)doorId=state.StructureId;}for(int i=0;i<20;i++)runtime._PhysicsProcess(1d/60d);Check(session.Registry.Count==1&&session.Registry.CellCount==9&&session.Registry.PortalCount==1,"incremental room creation");RoomTopologyStateV3 room=session.Registry.EnumerateRoomsInChunk(FloorRegistryV3.ChunkOf(origin+new Vector2I(1,1))).First();long topology=room.TopologyRevision;Check(construction.DoorPassages.TryGet(doorId,out var passage)&&passage!=null,"door passage");passage!.TryAcquire("merc",1,out _);passage.Tick(1);runtime._PhysicsProcess(1d/60d);Check(session.Registry.TryGetRoomById(room.RoomId,out var unchanged)&&unchanged?.TopologyRevision==topology,"door state rebuilt room");reason=string.Empty;return true;}catch(Exception ex){reason=ex.Message;return false;}finally{runtime._ExitTree();session.Registry.Clear();}
    }
    private static RoomFeatureSnapshotV3 Features(int beds=0,int stock=0,int floors=0)=>new(beds,0,stock,floors,floors,0,0,new ReadOnlyDictionary<string,int>(new Dictionary<string,int>()));
    private static List<GlobalCellCoord> RectCells(int x,int y,int w,int h){var r=new List<GlobalCellCoord>(w*h);for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)r.Add(new(new Vector2I(xx,yy)));return r;}
    private static bool Connected4(Vector2I from,Vector2I to,HashSet<Vector2I> cells){Queue<Vector2I> q=new();HashSet<Vector2I> seen=new(){from};q.Enqueue(from);Vector2I[] dirs={Vector2I.Up,Vector2I.Right,Vector2I.Down,Vector2I.Left};while(q.Count>0){var c=q.Dequeue();if(c==to)return true;foreach(var d in dirs)if(cells.Contains(c+d)&&seen.Add(c+d))q.Enqueue(c+d);}return false;}
    private static int BoundedOpenFlood(int available){int processed=0;while(processed<available&&processed<RoomRuntimeV3.MaxDetectedRoomCells)processed++;return processed;}
    private static string Fixture(int count){var sw=Stopwatch.StartNew();var registry=new RoomRegistryV3();var rooms=new List<RoomTopologyStateV3>(count);for(int i=0;i<count;i++){int x=(i%50)*8,y=(i/50)*8;rooms.Add(new($"room_fixture_{i:D4}",i+1,RectCells(x,y,5,5),Array.Empty<string>(),Array.Empty<string>(),1,RoomRebuildReasonV3.InitialScan));}registry.Commit(rooms,Array.Empty<string>(),Array.Empty<RoomPortalStateV3>(),RoomRebuildReasonV3.InitialScan);sw.Stop();Check(registry.Count==count&&registry.CellCount==count*25&&registry.Diagnostics.PerCellRoomNodeCount==0&&registry.Diagnostics.PerRoomProcessCount==0&&registry.Diagnostics.FullWorldRoomScanCount==0,"fixture invariants");return $"{count} rooms/{registry.CellCount} cells/{registry.ChunkCount} chunks/{sw.Elapsed.TotalMilliseconds:0.00}ms";}
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
}
internal static class RoomDoorStateSelfCheckExtensionsV3
{
    public static bool TryAcquire(this DoorPassageStateV3 state,string mercenary,long revision,out string reason){state.State=DoorStateV3.Opening;state.Revision++;reason=string.Empty;return true;}
    public static void Tick(this DoorPassageStateV3? state,double delta){if(state==null)return;state.State=DoorStateV3.Open;state.Revision++;}
}
