using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using GameplayV3.Company;
using GameplayV3.Construction;
using GameplayV3.Farming;
using GameplayV3.Mercenary;
using GameplayV3.Navigation;
using GameplayV3.Rooms;
using GameplayV3.Session;
using GameplayV3.Stockpile;
using WorldV2;

namespace GameplayV3.Resources.Ecology.Runtime;

public sealed partial class ResourceEcologyRuntimeV3 : Node, IResourceEcologyWorldQueryV3, IResourceRenewalExclusionQueryV3
{
    public const int MaxReachabilityCellsPerCompanyEvaluation=4096,MaxResourceNodesCheckedPerCompanyEvaluation=256,MaxCompaniesEvaluatedPerTick=2;
    private static readonly Vector2I[] Neighbors={Vector2I.Up,Vector2I.Right,Vector2I.Down,Vector2I.Left,new(1,-1),new(1,1),new(-1,1),new(-1,-1)};
    private ResourceEcologySessionV3? _ecology;private ResourceSessionV3? _resources;private ConstructionSessionV3? _construction;private FarmSessionV3? _farm;private StockpileSessionV3? _stockpiles;private RoomSessionV3? _rooms;private MercenarySessionV3? _mercenaries;private CompanySessionV3? _companies;private IMercenaryNavigationWorldQueryV3? _navigation;private readonly Dictionary<Vector2I,ChunkDataV2> _activeChunks=new();private readonly HashSet<Vector2I> _mercenaryCells=new(),_dynamicBlockerCells=new();private double _occupancyAccumulator,_shortageAccumulator;
    public event Action<string>? ResourceSpawned;
    public double PendingOccupancyTickCredit=>_occupancyAccumulator;public double PendingShortageTickCredit=>_shortageAccumulator;
    public void Initialize(ResourceEcologySessionV3 ecology,ResourceSessionV3 resources,ConstructionSessionV3 construction,FarmSessionV3 farm,StockpileSessionV3 stockpiles,RoomSessionV3 rooms,MercenarySessionV3 mercenaries,IMercenaryNavigationWorldQueryV3? navigation=null,CompanySessionV3? companies=null){_ecology=ecology;_resources=resources;_construction=construction;_farm=farm;_stockpiles=stockpiles;_rooms=rooms;_mercenaries=mercenaries;_navigation=navigation;_companies=companies;SetPhysicsProcess(false);RefreshMercenaryCells();ecology.ResourceSpawned+=OnResourceSpawned;}
    public override void _ExitTree(){if(_ecology!=null)_ecology.ResourceSpawned-=OnResourceSpawned;_activeChunks.Clear();_mercenaryCells.Clear();}
    public void AdvanceSimulation(double delta){if(_ecology==null)return;if(!GameplaySessionV3.IsCurrentResourceEcologySession(_ecology)){SetPhysicsProcess(false);_activeChunks.Clear();_mercenaryCells.Clear();_dynamicBlockerCells.Clear();return;}double safe=Math.Max(0,delta);_occupancyAccumulator+=safe;_shortageAccumulator+=safe;if(_occupancyAccumulator>=ResourceEcologySessionV3.TickIntervalSeconds){_occupancyAccumulator%=ResourceEcologySessionV3.TickIntervalSeconds;RefreshMercenaryCells();}_ecology.Advance(safe,this);if(_shortageAccumulator>=5){_shortageAccumulator%=5;EvaluateShortages();}}
    public void OnChunkDataReady(ChunkDataV2 data){if(_ecology==null)return;foreach(var capacity in data.ResourceEcologyCapacities)_ecology.RegisterCapacity(capacity);_ecology.SynchronizeChunk(data.GlobalChunkCoord);}
    public void OnChunkAttached(ChunkDataV2 data){_activeChunks[data.GlobalChunkCoord]=data;_ecology?.SetChunkActive(data.GlobalChunkCoord,true);}
    public void OnChunkDetached(Vector2I chunk){_activeChunks.Remove(chunk);_ecology?.SetChunkActive(chunk,false);}
    public void RunShortageEvaluationNow(){RefreshMercenaryCells();EvaluateShortages();}
    public bool TryGetActiveChunk(Vector2I chunk,out ChunkDataV2? data)=>_activeChunks.TryGetValue(chunk,out data);
    public bool IsPersistentExcluded(Vector2I cell,out string reason){var result=Evaluate(new(cell,FloorRegistryV3.ChunkOf(cell),string.Empty,NaturalResourceRenewalClassV3.VegetativeSpread,null,_ecology?.SimulationTimeSeconds??0,0));reason=result.BlockingSourceId??result.Kind.ToString();return !result.IsAllowed&&!result.IsTransient;}
    public bool IsTransientExcluded(Vector2I cell,out string reason){var result=Evaluate(new(cell,FloorRegistryV3.ChunkOf(cell),string.Empty,NaturalResourceRenewalClassV3.VegetativeSpread,null,_ecology?.SimulationTimeSeconds??0,0));reason=result.BlockingSourceId??result.Kind.ToString();return !result.IsAllowed&&result.IsTransient;}
    public ResourceRenewalExclusionResultV3 EvaluateExclusion(ResourceRenewalExclusionContextV3 context)=>Evaluate(context);
    public ResourceRenewalExclusionResultV3 Evaluate(ResourceRenewalExclusionContextV3 context)
    {
        Vector2I cell=context.GlobalCell;GlobalCellCoord c=new(cell);int revision=GetOccupancyRevision(cell);
        if(_resources?.Nodes.ContainsCell(cell)==true)return Block(ResourceRenewalExclusionKindV3.ExistingResource,false,60,"ExistingResource");
        if(_construction?.Structures.TryGetStructureAtCell(c,out var structure)==true&&structure!=null)return Block(ResourceRenewalExclusionKindV3.Structure,false,60,structure.StructureId);
        if(_construction?.Blueprints.TryGetBlueprintAtCell(c,out var blueprint)==true&&blueprint!=null)return Block(ResourceRenewalExclusionKindV3.Blueprint,false,60,blueprint.BlueprintId);
        if(_construction?.Floors.TryGetFloor(c,out var floor)==true&&floor!=null)return Block(ResourceRenewalExclusionKindV3.Floor,false,60,$"floor:{cell.X}:{cell.Y}");
        if(_construction?.Floors.TryGetBlueprint(c,out var floorBlueprint)==true&&floorBlueprint!=null)return Block(ResourceRenewalExclusionKindV3.Floor,false,60,floorBlueprint.BlueprintId);
        if(_construction?.Floors.TryGetMark(c,out var mark)==true&&mark!=null)return Block(ResourceRenewalExclusionKindV3.Floor,false,60,mark.MarkId);
        if(_farm?.Plots.TryGetPlotAt(c,out var farm)==true&&farm!=null)return Block(ResourceRenewalExclusionKindV3.Farm,false,60,farm.FarmPlotId);
        if(_stockpiles?.Zones.TryGetZoneAtCell(c,out var zone)==true&&zone!=null)return Block(ResourceRenewalExclusionKindV3.Stockpile,false,60,zone.StockpileZoneId);
        if(_rooms?.Registry.TryGetRoom(c,out var room)==true&&room!=null)return Block(ResourceRenewalExclusionKindV3.IndoorRoom,false,60,room.RoomId);
        if(_mercenaryCells.Contains(cell))return Block(ResourceRenewalExclusionKindV3.Mercenary,true,2,"Mercenary");
        if(_dynamicBlockerCells.Contains(cell))return Block(ResourceRenewalExclusionKindV3.Mercenary,true,2,"DynamicBlocker");
        if(_farm?.Reservations.IsReserved(c)==true||_stockpiles?.CellReservations.IsReserved(c)==true)return Block(ResourceRenewalExclusionKindV3.InteractionReservation,true,2,"InteractionReservation");
        if(_resources!=null&&(HasStack(ResourceTypeV3.Wood)||HasStack(ResourceTypeV3.Stone)||HasStack(ResourceTypeV3.Ration)||HasStack(ResourceTypeV3.Potato)))return Block(ResourceRenewalExclusionKindV3.GroundStack,true,3,"GroundStack");
        return ResourceRenewalExclusionResultV3.Allowed(revision);
        ResourceRenewalExclusionResultV3 Block(ResourceRenewalExclusionKindV3 kind,bool transient,double retry,string source)=>new(false,kind,transient,retry,source,revision);
        bool HasStack(ResourceTypeV3 type)=>_resources.GroundStacks.TryGetSingleStackAtCellAndType(c,type,out _);
    }
    public int GetOccupancyRevision(Vector2I cell){GlobalCellCoord c=new(cell);HashCode hash=new();if(_resources?.Nodes.TryGetAtCell(cell,out var node)==true&&node!=null){hash.Add(node.ResourceNodeId,StringComparer.Ordinal);hash.Add(node.IsDepleted);}if(_construction?.Structures.TryGetStructureAtCell(c,out var structure)==true&&structure!=null)hash.Add(structure.StructureId,StringComparer.Ordinal);if(_construction?.Blueprints.TryGetBlueprintAtCell(c,out var blueprint)==true&&blueprint!=null)hash.Add(blueprint.BlueprintId,StringComparer.Ordinal);if(_construction?.Floors.TryGetFloor(c,out var floor)==true&&floor!=null){hash.Add(floor.FloorTypeId,StringComparer.Ordinal);hash.Add(floor.Revision);}if(_construction?.Floors.TryGetBlueprint(c,out var floorBlueprint)==true&&floorBlueprint!=null)hash.Add(floorBlueprint.BlueprintId,StringComparer.Ordinal);if(_construction?.Floors.TryGetMark(c,out var mark)==true&&mark!=null)hash.Add(mark.MarkId,StringComparer.Ordinal);if(_farm?.Plots.TryGetPlotAt(c,out var farm)==true&&farm!=null)hash.Add(farm.FarmPlotId,StringComparer.Ordinal);if(_stockpiles?.Zones.TryGetZoneAtCell(c,out var zone)==true&&zone!=null)hash.Add(zone.StockpileZoneId,StringComparer.Ordinal);if(_rooms?.Registry.TryGetRoomId(c,out string roomId)==true)hash.Add(roomId,StringComparer.Ordinal);hash.Add(_mercenaryCells.Contains(cell));hash.Add(_dynamicBlockerCells.Contains(cell));if(_resources!=null)foreach(ResourceTypeV3 type in Enum.GetValues<ResourceTypeV3>())hash.Add(_resources.GroundStacks.TryGetSingleStackAtCellAndType(c,type,out _));return hash.ToHashCode();}
    private void EvaluateShortages()
    {
        if(_ecology==null||_resources==null||_mercenaries==null||_companies==null||_navigation==null)return;var watch=Stopwatch.StartNew();int evaluated=0;foreach(var company in _companies.CompanyRegistry.GetAllCompanies()){if(evaluated>=MaxCompaniesEvaluatedPerTick)break;if(_companies.LocalPlayer==null||company.OwnerPlayerId!=_companies.LocalPlayer.PlayerId)continue;var mercenaryIds=_mercenaries.Registry.GetMercenariesByCompany(company.CompanyId);if(mercenaryIds.Count==0)continue;if(!TryResolveAnchor(company.CompanyId,mercenaryIds,out Vector2I anchor,out string source))continue;evaluated++;var reachWatch=Stopwatch.StartNew();BuildReachability(anchor,56,out var reachable,out bool limitHit);reachWatch.Stop();_ecology.Diagnostics.ReachabilityMs=reachWatch.Elapsed.TotalMilliseconds;_ecology.Diagnostics.MaxReachabilityMs=Math.Max(_ecology.Diagnostics.MaxReachabilityMs,_ecology.Diagnostics.ReachabilityMs);_ecology.Diagnostics.ReachabilityCellsVisitedThisTick+=reachable.Count;EvaluateResource(company.CompanyId,anchor,source,NaturalResourceDefinitionCatalogV3.TreeId,ResourceNodeTypeV3.Tree,40,reachable,limitHit);EvaluateResource(company.CompanyId,anchor,source,NaturalResourceDefinitionCatalogV3.StoneId,ResourceNodeTypeV3.StoneOutcrop,48,reachable,limitHit);}
        watch.Stop();_ecology.Diagnostics.CompaniesEvaluatedThisTick+=evaluated;_ecology.Diagnostics.ShortageEvaluationMs=watch.Elapsed.TotalMilliseconds;_ecology.Diagnostics.MaxShortageEvaluationMs=Math.Max(_ecology.Diagnostics.MaxShortageEvaluationMs,_ecology.Diagnostics.ShortageEvaluationMs);
    }
    private bool TryResolveAnchor(string companyId,IReadOnlyList<string> ids,out Vector2I anchor,out string source)
    {
        List<(string Id,Vector2I Cell)> members=new(ids.Count);foreach(string id in ids)if(_mercenaries!.Registry.TryGetState(id,out var state)&&state!=null)members.Add((id,state.CurrentCell.Value));if(members.Count==0){if(_companies!.DeploymentRegistry.TryGetDeployment(companyId,out var deployment)&&deployment!=null){anchor=deployment.DeploymentAnchorCell.Value;source="StartingDeployment";return true;}anchor=default;source="Unavailable";return false;}members.Sort((a,b)=>string.CompareOrdinal(a.Id,b.Id));long sx=0,sy=0;foreach(var member in members){sx+=member.Cell.X;sy+=member.Cell.Y;}double mx=sx/(double)members.Count,my=sy/(double)members.Count;var best=members[0];double bestDistance=double.MaxValue;foreach(var member in members){double dx=member.Cell.X-mx,dy=member.Cell.Y-my,d=dx*dx+dy*dy;if(d<bestDistance||(d==bestDistance&&string.CompareOrdinal(member.Id,best.Id)<0)){best=member;bestDistance=d;}}anchor=best.Cell;source="MercenaryMedoid";return true;
    }
    private void BuildReachability(Vector2I anchor,int radius,out HashSet<Vector2I> reached,out bool limitHit)
    {
        reached=new();limitHit=false;if(!IsActiveCell(anchor)||!_navigation!.IsWalkable(anchor))return;Queue<Vector2I> queue=new();queue.Enqueue(anchor);reached.Add(anchor);while(queue.Count>0){Vector2I current=queue.Dequeue();for(int i=0;i<Neighbors.Length;i++){Vector2I next=current+Neighbors[i];if(reached.Contains(next)||Math.Max(Math.Abs(next.X-anchor.X),Math.Abs(next.Y-anchor.Y))>radius||!IsActiveCell(next)||!_navigation.IsWalkable(next))continue;if(i>=4&&(!_navigation.IsWalkable(new(next.X,current.Y))||!_navigation.IsWalkable(new(current.X,next.Y))))continue;if(reached.Count>=MaxReachabilityCellsPerCompanyEvaluation){limitHit=true;return;}reached.Add(next);queue.Enqueue(next);}}}
    private void EvaluateResource(string companyId,Vector2I anchor,string source,string definitionId,ResourceNodeTypeV3 type,int radius,HashSet<Vector2I> reached,bool limitHit)
    {
        int minChunkX=FloorRegistryV3.ChunkOf(anchor-new Vector2I(radius,radius)).X,maxChunkX=FloorRegistryV3.ChunkOf(anchor+new Vector2I(radius,radius)).X,minChunkY=FloorRegistryV3.ChunkOf(anchor-new Vector2I(radius,radius)).Y,maxChunkY=FloorRegistryV3.ChunkOf(anchor+new Vector2I(radius,radius)).Y;int alive=0,accessible=0,checkedNodes=0;for(int cy=minChunkY;cy<=maxChunkY&&checkedNodes<MaxResourceNodesCheckedPerCompanyEvaluation;cy++)for(int cx=minChunkX;cx<=maxChunkX&&checkedNodes<MaxResourceNodesCheckedPerCompanyEvaluation;cx++){Vector2I chunk=new(cx,cy);if(!_activeChunks.ContainsKey(chunk))continue;foreach(string id in _resources!.Nodes.GetNodeIdsInChunk(chunk)){if(checkedNodes>=MaxResourceNodesCheckedPerCompanyEvaluation)break;if(!_resources.Nodes.TryGet(id,out var node)||node==null||node.IsDepleted||node.NodeType!=type)continue;checkedNodes++;if(Math.Max(Math.Abs(node.Cell.Value.X-anchor.X),Math.Abs(node.Cell.Value.Y-anchor.Y))>radius)continue;alive++;if(reached.Contains(node.Cell.Value))accessible++;}}
        _ecology!.Diagnostics.ResourceNodesCheckedThisTick+=checkedNodes;ResourceShortageEvaluationStatusV3 status=reached.Count==0?ResourceShortageEvaluationStatusV3.Unknown:ResourceShortageEvaluationStatusV3.Valid;_ecology.UpdateShortageState(companyId,definitionId,anchor,source,accessible,alive,reached.Count,limitHit,status,status==ResourceShortageEvaluationStatusV3.Valid?string.Empty:"ReachabilityUnavailable");
    }
    private bool IsActiveCell(Vector2I cell)=>_activeChunks.ContainsKey(FloorRegistryV3.ChunkOf(cell));
    private void RefreshMercenaryCells(){if(_mercenaries==null)return;_mercenaryCells.Clear();_dynamicBlockerCells.Clear();foreach(string id in _mercenaries.Registry.GetAllMercenaryIds())if(_mercenaries.Registry.TryGetState(id,out var state)&&state!=null)_mercenaryCells.Add(state.CurrentCell.Value);if(GameplaySessionV3.TryGetControlSession(out var control)&&control!=null)foreach(string id in control.Movements.GetActiveIds())if(control.Movements.TryGet(id,out var movement)&&movement!=null)_dynamicBlockerCells.Add(movement.ToCell.Value);}
    private void OnResourceSpawned(string id)=>ResourceSpawned?.Invoke(id);
}

public static class ResourceRenewalExclusionSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        Rect2I bounds=new(0,0,32,32);CompanySessionV3 companies=new();companies.TryInitializeLocalSinglePlayer(out _,out _);string company=companies.LocalContext.LocalCompanyId;MercenarySessionV3 mercenaries=new(companies.CompanyRegistry);ResourceSessionV3 resources=new();ConstructionSessionV3 construction=new();FarmSessionV3 farms=new(1);StockpileSessionV3 stockpiles=new();RoomSessionV3 rooms=new(1);ResourceEcologySessionV3 ecology=new(resources.Nodes,1,bounds,true);ResourceEcologyRuntimeV3 runtime=new();runtime.Initialize(ecology,resources,construction,farms,stockpiles,rooms,mercenaries);
        try
        {
            ResourceNodeStateV3.TryCreate(ResourceNodeIdFactoryV3.CreateDeterministic(1,NaturalResourceDefinitionCatalogV3.TreeId,new(1,1)),ResourceNodeTypeV3.Tree,new(new Vector2I(1,1)),5,5,5,bounds,DateTime.UtcNow,out var node,out _);resources.Nodes.TryRegister(node,out _);
            var structure=new StructureStateV3(StructureIdFactoryV3.Create(),company,StructureDefinitionCatalogV3.WoodenWallId,new(new Vector2I(2,2)),StructureOrientationV3.Deg0,new[]{new GlobalCellCoord(new Vector2I(2,2))},Array.Empty<StructureMaterialRequirementV3>(),true,DateTime.UtcNow);construction.Structures.TryRegister(structure,construction.Blueprints,bounds,out _);
            construction.Definitions.TryGetDefinition(StructureDefinitionCatalogV3.WoodenWallId,out var definition);var blueprint=new ConstructionBlueprintStateV3(ConstructionBlueprintIdFactoryV3.Create(),company,definition!.DefinitionId,new(new Vector2I(3,3)),StructureOrientationV3.Deg0,new[]{new GlobalCellCoord(new Vector2I(3,3))},new(definition.RequiredMaterials),DateTime.UtcNow);construction.Blueprints.TryRegister(blueprint,construction.Structures,bounds,out _);
            construction.Floors.TryRegisterCompletedFloorForFixture(company,new(new Vector2I(4,4)),FloorDefinitionCatalogV3.WoodenFloorId);farms.Plots.TryCreate(company,CropCatalogV3.PotatoCropId,new[]{new GlobalCellCoord(new Vector2I(5,5))},FarmSessionV3.MaxFarmCellsPerCompany,out _,out _);stockpiles.Zones.TryCreateZone(company,new[]{new GlobalCellCoord(new Vector2I(6,6))},bounds,out _,out _);rooms.Registry.Commit(new[]{new RoomTopologyStateV3("room_fixture",1,new[]{new GlobalCellCoord(new Vector2I(7,7))},Array.Empty<string>(),Array.Empty<string>(),1,RoomRebuildReasonV3.InitialScan)},Array.Empty<string>(),Array.Empty<RoomPortalStateV3>(),RoomRebuildReasonV3.InitialScan);resources.GroundStacks.TryAddStack(ResourceTypeV3.Wood,1,new(new Vector2I(8,8)),out _,out _,out _);
            Check(new(1,1),ResourceRenewalExclusionKindV3.ExistingResource);Check(new(2,2),ResourceRenewalExclusionKindV3.Structure);Check(new(3,3),ResourceRenewalExclusionKindV3.Blueprint);Check(new(4,4),ResourceRenewalExclusionKindV3.Floor);Check(new(5,5),ResourceRenewalExclusionKindV3.Farm);Check(new(6,6),ResourceRenewalExclusionKindV3.Stockpile);Check(new(7,7),ResourceRenewalExclusionKindV3.IndoorRoom);Check(new(8,8),ResourceRenewalExclusionKindV3.GroundStack);if(!runtime.Evaluate(Context(new(9,9))).IsAllowed){reason="Free ecology cell was excluded.";return false;}int blockedRevision=runtime.GetOccupancyRevision(new(2,2)),unrelatedRevision=runtime.GetOccupancyRevision(new(9,9));construction.Structures.TryRemove(structure.StructureId,out _);if(!runtime.Evaluate(Context(new(2,2))).IsAllowed||runtime.GetOccupancyRevision(new(2,2))==blockedRevision||runtime.GetOccupancyRevision(new(9,9))!=unrelatedRevision){reason="Cell-local occupancy revision invalidation failed.";return false;}reason=string.Empty;return true;
            void Check(Vector2I cell,ResourceRenewalExclusionKindV3 expected){var result=runtime.Evaluate(Context(cell));if(result.IsAllowed||result.Kind!=expected)throw new InvalidOperationException($"Exclusion {expected} failed at {cell}: {result.Kind}");}
            ResourceRenewalExclusionContextV3 Context(Vector2I cell)=>new(cell,FloorRegistryV3.ChunkOf(cell),NaturalResourceDefinitionCatalogV3.TreeId,NaturalResourceRenewalClassV3.VegetativeSpread,company,0,1);
        }
        catch(Exception ex){reason=ex.Message;return false;}
        finally{runtime._ExitTree();runtime.Free();ecology.Dispose();}
    }
}
