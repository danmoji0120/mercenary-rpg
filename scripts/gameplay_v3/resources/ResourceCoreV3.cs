using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameplayV3.Deployment;
using Godot;
using WorldV2;

namespace GameplayV3.Resources;

internal static class ResourceIdValidationV3
{
    public static bool IsCanonical(string? value, string prefix)
    {
        if (value == null || value.Length != prefix.Length + 32 || !value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        for (int index = prefix.Length; index < value.Length; index++)
        {
            char character = value[index];
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) return false;
        }
        return true;
    }
}

public static class ResourceNodeIdFactoryV3
{
    private const string Prefix = "rnode_";
    public static string Create() => Prefix + Guid.NewGuid().ToString("N");
    public static string CreateDeterministic(int worldSeed,string definitionId,Vector2I cell)=>WorldV2.ResourcePlacementEvaluatorV3.CreateDeterministicId(worldSeed,definitionId,cell);
    public static bool IsValid(string? value) => ResourceIdValidationV3.IsCanonical(value, Prefix);
}

public static class GroundResourceStackIdFactoryV3
{
    private const string Prefix = "rstack_";
    public static string Create() => Prefix + Guid.NewGuid().ToString("N");
    public static bool IsValid(string? value) => ResourceIdValidationV3.IsCanonical(value, Prefix);
}

public enum ResourceTypeV3 { Wood, Stone, Ration, Potato, IronOre, CopperOre, Coal, Clay, Fiber, MedicinalHerb, WoodPlank, StoneBlock, IronIngot, CopperIngot, Brick, Cloth, HerbPowder, RoastedPotato, PotatoStew, DriedPotato, Bandage, SimpleMedicine, IronAxe, IronPickaxe, IronHammer }
public enum ResourceNodeTypeV3 { Tree, StoneOutcrop, IronVein, CopperVein, CoalSeam, ClayDeposit, FiberBush, MedicinalHerbPatch }

public sealed class ResourceNodeStateV3
{
    private ResourceNodeStateV3(string id, ResourceNodeTypeV3 nodeType, ResourceTypeV3 produced, GlobalCellCoord cell, int remaining, int max, int yield, DateTime created)
    { ResourceNodeId=id;NodeType=nodeType;ProducedResourceType=produced;Cell=cell;RemainingAmount=remaining;MaxAmount=max;YieldPerCycle=yield;CreatedUtc=created.Kind==DateTimeKind.Utc?created:created.ToUniversalTime(); }
    public string ResourceNodeId{get;} public ResourceNodeTypeV3 NodeType{get;} public ResourceTypeV3 ProducedResourceType{get;} public GlobalCellCoord Cell{get;} public int RemainingAmount{get;private set;} public int MaxAmount{get;} public int YieldPerCycle{get;} public bool IsDepleted=>RemainingAmount==0; public DateTime CreatedUtc{get;} public string InitialProfileId{get;private set;}=string.Empty;public int InitialProfileVersion{get;private set;}public int PlacementAlgorithmVersion{get;private set;}public NaturalResourceOriginKindV3 OriginKind{get;private set;}=NaturalResourceOriginKindV3.StartingGuarantee;
    public static bool TryCreate(string id,ResourceNodeTypeV3 type,GlobalCellCoord cell,int remaining,int max,int yield,Rect2I bounds,DateTime created,out ResourceNodeStateV3? state,out string reason)
    {
        if(!ResourceNodeIdFactoryV3.IsValid(id)){state=null;reason="ResourceNodeId is not canonical.";return false;}
        if(!Enum.IsDefined(type)||max<1||remaining<0||remaining>max||yield<1){state=null;reason="Resource node amounts or type are invalid.";return false;}
        if(!bounds.HasPoint(cell.Value)){state=null;reason="Resource node cell is outside world bounds.";return false;}
        if(!NaturalResourceDefinitionCatalogV3.TryGet(type,out NaturalResourceDefinitionV3? definition)||definition==null){state=null;reason="Unknown resource node definition.";return false;}
        ResourceTypeV3 produced=definition.OutputResourceType;
        state=new(id,type,produced,cell,remaining,max,yield,created);reason=string.Empty;return true;
    }
    public bool TryHarvest(out int amount,out string reason)
    { if(IsDepleted){amount=0;reason="Resource node is depleted.";return false;}amount=Math.Min(YieldPerCycle,RemainingAmount);RemainingAmount-=amount;reason=string.Empty;return true; }
    public long EcologyAttemptSequence{get;private set;}public long EcologySuccessfulSpawnSequence{get;private set;}public double EcologySpawnTimeSeconds{get;private set;}
    public void SetInitialPlacementMetadata(string profileId,int profileVersion,int algorithmVersion,NaturalResourceOriginKindV3 origin){InitialProfileId=profileId??string.Empty;InitialProfileVersion=profileVersion;PlacementAlgorithmVersion=algorithmVersion;OriginKind=origin;}
    public void SetRenewalMetadata(string profileId,int profileVersion,int algorithmVersion,NaturalResourceOriginKindV3 origin,long attemptSequence,long successfulSequence,double simulationTime){SetInitialPlacementMetadata(profileId,profileVersion,algorithmVersion,origin);EcologyAttemptSequence=attemptSequence;EcologySuccessfulSpawnSequence=successfulSequence;EcologySpawnTimeSeconds=simulationTime;}
}

public sealed class ResourceNodeRegistryV3
{
    private readonly Dictionary<string,ResourceNodeStateV3> _byId=new(StringComparer.Ordinal);private readonly SortedSet<string> _debugIds=new(StringComparer.Ordinal);private readonly Dictionary<Vector2I,string> _byCell=new();private readonly Dictionary<Vector2I,HashSet<string>> _byChunk=new();private readonly HashSet<string> _depletionNotified=new(StringComparer.Ordinal);private readonly Queue<ResourceNodeRegistryChangeV3> _changes=new();private readonly int[] _countsByType=new int[Enum.GetValues<ResourceNodeTypeV3>().Length];private readonly int[] _depletedByType=new int[Enum.GetValues<ResourceNodeTypeV3>().Length];
    public int Count=>_byId.Count;public int TreeCount=>GetCount(ResourceNodeTypeV3.Tree);public int StoneCount=>GetCount(ResourceNodeTypeV3.StoneOutcrop);public int DepletedCount{get;private set;} public long Revision{get;private set;} public event Action? Changed;public event Action<ResourceNodeStateV3>? NodeRegistered;public event Action<ResourceNodeStateV3>? NodeDepleted;public event Action<ResourceNodeStateV3>? NodeRemoved;
    public int GetCount(ResourceNodeTypeV3 type)=>Enum.IsDefined(type)?_countsByType[(int)type]:0;public int GetDepletedCount(ResourceNodeTypeV3 type)=>Enum.IsDefined(type)?_depletedByType[(int)type]:0;
    public bool TryRegister(ResourceNodeStateV3? state,out string reason){if(state==null){reason="Resource node is required.";return false;}if(_byId.ContainsKey(state.ResourceNodeId)){reason="Duplicate ResourceNodeId.";return false;}if(_byCell.ContainsKey(state.Cell.Value)){reason="Resource node cell is occupied.";return false;}_byId.Add(state.ResourceNodeId,state);_debugIds.Add(state.ResourceNodeId);_countsByType[(int)state.NodeType]++;if(state.IsDepleted){DepletedCount++;_depletedByType[(int)state.NodeType]++;_depletionNotified.Add(state.ResourceNodeId);}_byCell.Add(state.Cell.Value,state.ResourceNodeId);Vector2I chunk=WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(state.Cell.Value);if(!_byChunk.TryGetValue(chunk,out var chunkIds)){chunkIds=new(StringComparer.Ordinal);_byChunk.Add(chunk,chunkIds);}chunkIds.Add(state.ResourceNodeId);_changes.Enqueue(new(state.ResourceNodeId,state.Cell.Value,!state.IsDepleted));Touch();NodeRegistered?.Invoke(state);reason=string.Empty;return true;}
    public bool TryGet(string id,out ResourceNodeStateV3? state)=>_byId.TryGetValue(id,out state);
    public bool Contains(string id)=>_byId.ContainsKey(id);
    public bool TryRemove(string id,out ResourceNodeStateV3? state){if(!_byId.Remove(id,out state)||state==null)return false;_debugIds.Remove(id);_countsByType[(int)state.NodeType]--;if(state.IsDepleted){DepletedCount--;_depletedByType[(int)state.NodeType]--;}_byCell.Remove(state.Cell.Value);Vector2I chunk=WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(state.Cell.Value);if(_byChunk.TryGetValue(chunk,out var ids)){ids.Remove(id);if(ids.Count==0)_byChunk.Remove(chunk);}_depletionNotified.Remove(id);_changes.Enqueue(new(id,state.Cell.Value,false));Touch();NodeRemoved?.Invoke(state);return true;}
    public int GetBoundedDebugNodeIds(int maximum,List<string> output){output.Clear();if(maximum<=0)return _debugIds.Count;int count=0;foreach(string id in _debugIds){if(count++>=maximum)break;output.Add(id);}return Math.Max(0,_debugIds.Count-output.Count);}
    public IReadOnlyList<string> GetAllNodeIds(){List<string> ids=new(_byId.Keys);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    public IReadOnlyList<string> GetNodesByType(ResourceNodeTypeV3 type){List<string> ids=new();foreach(var pair in _byId)if(pair.Value.NodeType==type)ids.Add(pair.Key);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    public bool ContainsCell(Vector2I cell)=>_byCell.ContainsKey(cell);
    public bool TryGetAtCell(Vector2I cell,out ResourceNodeStateV3? state){state=null;return _byCell.TryGetValue(cell,out string? id)&&_byId.TryGetValue(id,out state);}
    public IReadOnlyList<string> GetNodeIdsInChunk(Vector2I chunk){if(!_byChunk.TryGetValue(chunk,out var set))return Array.Empty<string>();List<string> ids=new(set);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    public int CountAliveInChunk(Vector2I chunk,string definitionId){int count=0;if(!_byChunk.TryGetValue(chunk,out var ids))return 0;foreach(string id in ids)if(_byId.TryGetValue(id,out var state)&&!state.IsDepleted&&DefinitionId(state.NodeType)==definitionId)count++;return count;}
    public int CountAliveInRadius(Vector2I cell,ResourceNodeTypeV3 type,int radius,int maximum,List<ResourceNodeStateV3>? output=null){int count=0;Vector2I min=WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(cell-new Vector2I(radius,radius)),max=WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(cell+new Vector2I(radius,radius));for(int cy=min.Y;cy<=max.Y&&count<maximum;cy++)for(int cx=min.X;cx<=max.X&&count<maximum;cx++)if(_byChunk.TryGetValue(new(cx,cy),out var ids))foreach(string id in ids){if(count>=maximum)break;if(_byId.TryGetValue(id,out var state)&&!state.IsDepleted&&state.NodeType==type&&Math.Max(Math.Abs(state.Cell.Value.X-cell.X),Math.Abs(state.Cell.Value.Y-cell.Y))<=radius){count++;output?.Add(state);}}return count;}
    public int GetAliveInChunk(Vector2I chunk,ResourceNodeTypeV3 type,int maximum,List<ResourceNodeStateV3> output){int count=0;if(!_byChunk.TryGetValue(chunk,out var ids))return 0;foreach(string id in ids){if(count>=maximum)break;if(_byId.TryGetValue(id,out var state)&&!state.IsDepleted&&state.NodeType==type){output.Add(state);count++;}}output.Sort((a,b)=>string.CompareOrdinal(a.ResourceNodeId,b.ResourceNodeId));return count;}
    public void NotifyChanged(string id){if(_byId.TryGetValue(id,out ResourceNodeStateV3? state)){_changes.Enqueue(new(id,state.Cell.Value,!state.IsDepleted));Touch();if(state.IsDepleted&&_depletionNotified.Add(id)){DepletedCount++;_depletedByType[(int)state.NodeType]++;NodeDepleted?.Invoke(state);}}}
    public int PendingChangeCount=>_changes.Count;public int DrainChanges(int max,List<ResourceNodeRegistryChangeV3> output){int count=0;while(count<Math.Max(0,max)&&_changes.Count>0){output.Add(_changes.Dequeue());count++;}return count;}public void DiscardPendingChanges()=>_changes.Clear();
    public void Clear(){if(_byId.Count==0)return;foreach(var pair in _byId){_changes.Enqueue(new(pair.Key,pair.Value.Cell.Value,false));NodeRemoved?.Invoke(pair.Value);}_byId.Clear();_debugIds.Clear();_byCell.Clear();_byChunk.Clear();_depletionNotified.Clear();Array.Clear(_countsByType);Array.Clear(_depletedByType);DepletedCount=0;Touch();}
    private static string DefinitionId(ResourceNodeTypeV3 type)=>NaturalResourceDefinitionCatalogV3.TryGet(type,out NaturalResourceDefinitionV3? definition)&&definition!=null?definition.DefinitionId:string.Empty;
    private void Touch(){Revision++;Changed?.Invoke();}
}
public readonly record struct ResourceNodeRegistryChangeV3(string ResourceNodeId,Vector2I Cell,bool IsValid);

public sealed class GroundResourceStackV3
{
    internal GroundResourceStackV3(string id,ResourceTypeV3 type,int amount,GlobalCellCoord cell,DateTime created){ResourceStackId=id;ResourceType=type;Amount=amount;Cell=cell;CreatedUtc=created.Kind==DateTimeKind.Utc?created:created.ToUniversalTime();}
    public string ResourceStackId{get;} public ResourceTypeV3 ResourceType{get;} public int Amount{get;private set;} public GlobalCellCoord Cell{get;} public DateTime CreatedUtc{get;}
    internal bool TryMerge(int amount,out string reason){if(amount<1||Amount>int.MaxValue-amount){reason="Ground stack amount is invalid or would overflow.";return false;}Amount+=amount;reason=string.Empty;return true;}
    internal bool TryTake(int requested,out int taken,out string reason){taken=0;if(requested<1){reason="Requested amount must be positive.";return false;}if(Amount<1){reason="SourceStackEmpty";return false;}taken=Math.Min(requested,Amount);Amount-=taken;reason=string.Empty;return true;}
}

public sealed class GroundResourceStackRegistryV3
{
    private readonly Dictionary<string,GroundResourceStackV3> _byId=new(StringComparer.Ordinal);private readonly Dictionary<(Vector2I,ResourceTypeV3),string> _mergeIndex=new();private readonly Dictionary<Vector2I,HashSet<string>> _byCell=new();private readonly Dictionary<ResourceTypeV3,HashSet<string>> _byType=new();
    public int Count=>_byId.Count; public long Revision{get;private set;} public event Action? Changed;
    public bool TryAddStack(ResourceTypeV3 type,int amount,GlobalCellCoord cell,out GroundResourceStackV3? stack,out bool merged,out string reason)
    {stack=null;merged=false;if(!Enum.IsDefined(type)||amount<1){reason="Ground stack type or amount is invalid.";return false;}var key=(cell.Value,type);if(_mergeIndex.TryGetValue(key,out string? existing)&&_byId.TryGetValue(existing,out stack)){merged=stack.TryMerge(amount,out reason);if(merged)Touch();return merged;}string id=GroundResourceStackIdFactoryV3.Create();stack=new(id,type,amount,cell,DateTime.UtcNow);_byId.Add(id,stack);_mergeIndex.Add(key,id);if(!_byCell.TryGetValue(cell.Value,out HashSet<string>? ids)){ids=new(StringComparer.Ordinal);_byCell.Add(cell.Value,ids);}ids.Add(id);if(!_byType.TryGetValue(type,out HashSet<string>? typeIds)){typeIds=new(StringComparer.Ordinal);_byType.Add(type,typeIds);}typeIds.Add(id);Touch();reason=string.Empty;return true;}
    internal bool TryRestoreStack(string id,ResourceTypeV3 type,int amount,GlobalCellCoord cell,DateTime created,out string reason)
    {
        if(!GroundResourceStackIdFactoryV3.IsValid(id)||!Enum.IsDefined(type)||amount<1||_byId.ContainsKey(id)||_mergeIndex.ContainsKey((cell.Value,type))){reason="InvalidOrDuplicateGroundStack";return false;}
        GroundResourceStackV3 stack=new(id,type,amount,cell,created);_byId.Add(id,stack);_mergeIndex.Add((cell.Value,type),id);
        if(!_byCell.TryGetValue(cell.Value,out HashSet<string>? ids)){ids=new(StringComparer.Ordinal);_byCell.Add(cell.Value,ids);}ids.Add(id);
        if(!_byType.TryGetValue(type,out HashSet<string>? typeIds)){typeIds=new(StringComparer.Ordinal);_byType.Add(type,typeIds);}typeIds.Add(id);
        Touch();reason=string.Empty;return true;
    }
    public bool TryGet(string id,out GroundResourceStackV3? stack)=>_byId.TryGetValue(id,out stack);
    public bool Contains(string id)=>_byId.ContainsKey(id);
    public bool TryTakeAmount(string id,int requested,out int taken,out GroundResourceStackV3? changed,out bool removed,out string reason)
    {taken=0;changed=null;removed=false;if(!_byId.TryGetValue(id,out GroundResourceStackV3? stack)){reason="InvalidSourceStack";return false;}if(!stack.TryTake(requested,out taken,out reason))return false;changed=stack;if(stack.Amount==0){TryRemove(id,out changed);removed=true;}else Touch();return true;}
    public bool TryAddOrMerge(ResourceTypeV3 type,int amount,GlobalCellCoord cell,out GroundResourceStackV3? stack,out bool merged,out string reason)=>TryAddStack(type,amount,cell,out stack,out merged,out reason);
    public bool TryAddBatchAtomic(IReadOnlyList<(ResourceTypeV3 Type,int Amount,GlobalCellCoord Cell)> entries,out IReadOnlyList<string> affectedStackIds,out string reason)
    {List<(string Id,int Amount)> committed=new();List<string> affected=new();foreach(var entry in entries){if(!Enum.IsDefined(entry.Type)||entry.Amount<1){reason="InvalidSalvageAmount";Rollback();affectedStackIds=Array.Empty<string>();return false;}if(_mergeIndex.TryGetValue((entry.Cell.Value,entry.Type),out string? existingId)&&_byId.TryGetValue(existingId,out GroundResourceStackV3? existing)&&existing.Amount>int.MaxValue-entry.Amount){reason="SalvageAmountOverflow";Rollback();affectedStackIds=Array.Empty<string>();return false;}if(!TryAddStack(entry.Type,entry.Amount,entry.Cell,out GroundResourceStackV3? stack,out _,out reason)||stack==null){Rollback();affectedStackIds=Array.Empty<string>();return false;}committed.Add((stack.ResourceStackId,entry.Amount));affected.Add(stack.ResourceStackId);}affectedStackIds=affected.Distinct(StringComparer.Ordinal).ToList().AsReadOnly();reason=string.Empty;return true;void Rollback(){for(int i=committed.Count-1;i>=0;i--){var c=committed[i];if(!_byId.TryGetValue(c.Id,out GroundResourceStackV3? stack)||stack==null)continue;stack.TryTake(c.Amount,out _,out _);if(stack.Amount==0)TryRemove(c.Id,out _);}}}
    public bool TryGetSingleStackAtCellAndType(GlobalCellCoord cell,ResourceTypeV3 type,out GroundResourceStackV3? stack){stack=null;return _mergeIndex.TryGetValue((cell.Value,type),out string? id)&&_byId.TryGetValue(id,out stack);}
    public IReadOnlyList<string> GetAllStackIds(){List<string> ids=new(_byId.Keys);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    public IReadOnlyList<string> GetStackIdsByType(ResourceTypeV3 type){if(!_byType.TryGetValue(type,out var set))return Array.Empty<string>();List<string> ids=new(set);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    public IReadOnlyList<GroundResourceStackV3> GetStacksAtCell(GlobalCellCoord cell){List<GroundResourceStackV3> result=new();if(_byCell.TryGetValue(cell.Value,out HashSet<string>? ids))foreach(string id in ids)result.Add(_byId[id]);result.Sort((a,b)=>a.ResourceType.CompareTo(b.ResourceType));return result.AsReadOnly();}
    public bool TryRemove(string id,out GroundResourceStackV3? stack){if(!_byId.Remove(id,out stack)||stack==null)return false;_mergeIndex.Remove((stack.Cell.Value,stack.ResourceType));if(_byCell.TryGetValue(stack.Cell.Value,out HashSet<string>? ids)){ids.Remove(id);if(ids.Count==0)_byCell.Remove(stack.Cell.Value);}if(_byType.TryGetValue(stack.ResourceType,out var typeIds)){typeIds.Remove(id);if(typeIds.Count==0)_byType.Remove(stack.ResourceType);}Touch();return true;}
    public int GetTotalAmount(ResourceTypeV3 type){int total=0;foreach(GroundResourceStackV3 stack in _byId.Values)if(stack.ResourceType==type)total+=stack.Amount;return total;}
    public void Clear(){if(_byId.Count==0)return;_byId.Clear();_mergeIndex.Clear();_byCell.Clear();_byType.Clear();Touch();}
    private void Touch(){Revision++;Changed?.Invoke();}
}

public enum ResourceAmountReservationPurposeV3 { HaulingPickup, ConstructionSupply, FoodConsumption, ProductionSupply, WorkTool }
public sealed record ResourceAmountReservationV3(string ReservationId,string GroundStackId,string MercenaryId,string WorkRequestId,ResourceTypeV3 ResourceType,int Amount,ResourceAmountReservationPurposeV3 Purpose,DateTime CreatedUtc);
public static class ResourceAmountReservationIdFactoryV3
{
    private const string Prefix="rsv_";public static string Create()=>Prefix+Guid.NewGuid().ToString("N");public static bool IsValid(string? value)=>ResourceIdValidationV3.IsCanonical(value,Prefix);
}
public sealed class ResourceAmountReservationRegistryV3
{
    private readonly GroundResourceStackRegistryV3 _stacks;private readonly Dictionary<string,ResourceAmountReservationV3> _byId=new(StringComparer.Ordinal);private readonly Dictionary<string,HashSet<string>> _byStack=new(StringComparer.Ordinal);private readonly Dictionary<string,string> _byWork=new(StringComparer.Ordinal);private readonly Dictionary<string,string> _consumptionByMercenary=new(StringComparer.Ordinal);
    public ResourceAmountReservationRegistryV3(GroundResourceStackRegistryV3 stacks){_stacks=stacks;}public int Count=>_byId.Count;public long Revision{get;private set;}public event Action? Changed;
    public int GetReservedAmount(string stackId,ResourceAmountReservationPurposeV3? purpose=null){int total=0;if(_byStack.TryGetValue(stackId,out var ids))foreach(string id in ids)if(_byId.TryGetValue(id,out var value)&&(purpose==null||value.Purpose==purpose.Value))total+=value.Amount;return total;}
    public int GetAvailableAmount(string stackId){return _stacks.TryGet(stackId,out var stack)&&stack!=null?Math.Max(0,stack.Amount-GetReservedAmount(stackId)):0;}
    public bool TryReserve(string stackId,string mercenaryId,string workId,ResourceTypeV3 type,int amount,ResourceAmountReservationPurposeV3 purpose,out ResourceAmountReservationV3? reservation,out string reason)
    {reservation=null;if(_byWork.TryGetValue(workId,out string? existing)&&_byId.TryGetValue(existing,out reservation)){reason=reservation.GroundStackId==stackId&&reservation.MercenaryId==mercenaryId&&reservation.Amount==amount&&reservation.Purpose==purpose?string.Empty:"ReservationMismatch";return reason.Length==0;}if(!_stacks.TryGet(stackId,out var stack)||stack==null){reason="InvalidFoodStack";return false;}if(stack.ResourceType!=type||amount<1){reason="ReservationMismatch";return false;}if(purpose==ResourceAmountReservationPurposeV3.FoodConsumption&&_consumptionByMercenary.ContainsKey(mercenaryId)){reason="FoodAlreadyReserved";return false;}if(GetAvailableAmount(stackId)<amount){reason="InsufficientAvailableAmount";return false;}reservation=new(ResourceAmountReservationIdFactoryV3.Create(),stackId,mercenaryId,workId,type,amount,purpose,DateTime.UtcNow);_byId.Add(reservation.ReservationId,reservation);_byWork.Add(workId,reservation.ReservationId);if(!_byStack.TryGetValue(stackId,out var ids)){ids=new(StringComparer.Ordinal);_byStack.Add(stackId,ids);}ids.Add(reservation.ReservationId);if(purpose==ResourceAmountReservationPurposeV3.FoodConsumption)_consumptionByMercenary[mercenaryId]=reservation.ReservationId;Revision++;Changed?.Invoke();reason=string.Empty;return true;}
    public bool TryGet(string reservationId,out ResourceAmountReservationV3? value)=>_byId.TryGetValue(reservationId,out value);public bool TryGetByWork(string workId,out ResourceAmountReservationV3? value){value=null;return _byWork.TryGetValue(workId,out string? id)&&_byId.TryGetValue(id,out value);}public bool IsReserved(string stackId)=>GetReservedAmount(stackId)>0;public bool IsReservedBy(string stackId,string workId)=>TryGetByWork(workId,out var value)&&value?.GroundStackId==stackId;
    public bool TryConsume(string reservationId,string mercenaryId,string workId,out GroundResourceStackV3? changed,out bool removed,out string reason){changed=null;removed=false;if(!_byId.TryGetValue(reservationId,out var value)){reason="ReservationLost";return false;}if(value.MercenaryId!=mercenaryId||value.WorkRequestId!=workId||value.Purpose!=ResourceAmountReservationPurposeV3.FoodConsumption){reason="ReservationMismatch";return false;}if(!_stacks.TryGet(value.GroundStackId,out var stack)||stack==null){reason="FoodStackMissing";return false;}if(stack.ResourceType!=value.ResourceType){reason="FoodTypeChanged";return false;}if(stack.Amount<value.Amount){reason="FoodAmountInsufficient";return false;}if(!_stacks.TryTakeAmount(value.GroundStackId,value.Amount,out int taken,out changed,out removed,out reason)||taken!=value.Amount){reason="ConsumptionFailed";return false;}Release(reservationId);reason=string.Empty;return true;}
    public bool TryReduceByWork(string workId,int amount){if(amount<1||!_byWork.TryGetValue(workId,out string? id)||!_byId.TryGetValue(id,out var value)||amount>value.Amount)return false;if(amount==value.Amount)return Release(id);_byId[id]=value with{Amount=value.Amount-amount};Revision++;Changed?.Invoke();return true;}
    public bool Release(string reservationId){if(!_byId.Remove(reservationId,out var value))return false;_byWork.Remove(value.WorkRequestId);if(_byStack.TryGetValue(value.GroundStackId,out var ids)){ids.Remove(reservationId);if(ids.Count==0)_byStack.Remove(value.GroundStackId);}if(value.Purpose==ResourceAmountReservationPurposeV3.FoodConsumption)_consumptionByMercenary.Remove(value.MercenaryId);Revision++;Changed?.Invoke();return true;}public int ReleaseByWorkRequest(string work){return _byWork.TryGetValue(work,out string? id)&&Release(id)?1:0;}public int ReleaseByMercenary(string mercenary){var ids=_byId.Values.Where(x=>x.MercenaryId==mercenary).Select(x=>x.ReservationId).ToList();foreach(string id in ids)Release(id);return ids.Count;}public int ReleaseByGroundStack(string stack){var ids=_byStack.TryGetValue(stack,out var set)?set.ToList():new List<string>();foreach(string id in ids)Release(id);return ids.Count;}public void Clear(){if(_byId.Count==0)return;_byId.Clear();_byStack.Clear();_byWork.Clear();_consumptionByMercenary.Clear();Revision++;Changed?.Invoke();}
}
public sealed record ResourceConsumptionEntryV3(ResourceTypeV3 ResourceType,int Amount,string Reason,string MercenaryId,string WorkRequestId,DateTime TimestampUtc);
public sealed class ResourceConsumptionLedgerV3
{
    private readonly List<ResourceConsumptionEntryV3> _entries=new();private readonly HashSet<string> _workIds=new(StringComparer.Ordinal);public int Count=>_entries.Count;public bool TryRecord(ResourceTypeV3 type,int amount,string reason,string mercenaryId,string workId){if(amount<1||string.IsNullOrWhiteSpace(workId)||!_workIds.Add(workId))return false;_entries.Add(new(type,amount,reason,mercenaryId,workId,DateTime.UtcNow));return true;}public int GetConsumedAmount(ResourceTypeV3 type)=>_entries.Where(x=>x.ResourceType==type).Sum(x=>x.Amount);public IReadOnlyList<ResourceConsumptionEntryV3> GetEntries()=>_entries.AsReadOnly();public void Clear(){_entries.Clear();_workIds.Clear();}
}
public sealed record ResourceGenerationEntryV3(ResourceTypeV3 ResourceType,int Amount,string Reason,string MercenaryId,string WorkRequestId,string SourceId,GlobalCellCoord Cell,DateTime TimestampUtc);
public sealed class ResourceGenerationLedgerV3
{
    private readonly List<ResourceGenerationEntryV3> _entries=new();private readonly HashSet<string> _workIds=new(StringComparer.Ordinal);private readonly int[] _generatedByType=new int[Enum.GetValues<ResourceTypeV3>().Length];public int Count=>_entries.Count;public event Action<ResourceGenerationEntryV3>? EntryRecorded;
    public bool TryRecord(ResourceTypeV3 type,int amount,string reason,string mercenaryId,string workId,string sourceId,GlobalCellCoord cell,string? idempotencyKey=null){string key=idempotencyKey??workId;if(amount<1||string.IsNullOrWhiteSpace(workId)||!_workIds.Add(key))return false;ResourceGenerationEntryV3 entry=new(type,amount,reason,mercenaryId,workId,sourceId,cell,DateTime.UtcNow);_entries.Add(entry);_generatedByType[(int)type]+=amount;EntryRecorded?.Invoke(entry);return true;}
    public int GetGeneratedAmount(ResourceTypeV3 type)=>Enum.IsDefined(type)?_generatedByType[(int)type]:0;public IReadOnlyList<ResourceGenerationEntryV3> GetEntries()=>_entries.AsReadOnly();public void Clear(){_entries.Clear();_workIds.Clear();Array.Clear(_generatedByType);}
}

public sealed class InitialGatheringPatchResultV3
{
    public InitialGatheringPatchResultV3(bool success,bool reused,IReadOnlyList<string> ids,int candidates,string reason){Succeeded=success;ReusedExisting=reused;NodeIds=new ReadOnlyCollection<string>(new List<string>(ids));CandidatesChecked=candidates;FailureReason=reason;}
    public bool Succeeded{get;} public bool ReusedExisting{get;} public IReadOnlyList<string> NodeIds{get;} public int CandidatesChecked{get;} public string FailureReason{get;}
}

public sealed class ResourceSessionV3
{
    public ResourceSessionV3(){AmountReservations=new(GroundStacks);}public ResourceNodeRegistryV3 Nodes{get;}=new();public GroundResourceStackRegistryV3 GroundStacks{get;}=new();public ResourceAmountReservationRegistryV3 AmountReservations{get;}public ResourceConsumptionLedgerV3 ConsumptionLedger{get;}=new();public ResourceGenerationLedgerV3 GenerationLedger{get;}=new();public InitialGatheringPatchResultV3? InitialPatchResult{get;internal set;}public bool InitialRationPlacementInitialized{get;internal set;}public string InitialRationStackId{get;internal set;}=string.Empty;
}

public sealed class InitialGatheringPatchPlacementSettingsV3
{
    public int WorldSeed{get;init;}public int TreeCount{get;init;}=6;public int StoneCount{get;init;}=4;public int MinimumDistance{get;init;}=10;public int MaximumDistance{get;init;}=48;public int MaxCandidates{get;init;}=9409;public int TreeAmount{get;init;}=15;public int StoneAmount{get;init;}=20;public int YieldPerCycle{get;init;}=5;
}

public static class InitialGatheringPatchPlacementServiceV3
{
    private readonly record struct Candidate(Vector2I Cell,float Preference,float Distance);
    public static InitialGatheringPatchResultV3 Place(ResourceSessionV3 session,CompanyDeploymentStateV3 deployment,StartingDeploymentPlacementResultV3 placement,Rect2I bounds,Func<Vector2I,FlatlandCellSampleV2> sample,InitialGatheringPatchPlacementSettingsV3? settings=null)
    {
        settings??=new();if(session.Nodes.Count==settings.TreeCount+settings.StoneCount){var existing=session.Nodes.GetAllNodeIds();var reused=new InitialGatheringPatchResultV3(true,true,existing,0,string.Empty);session.InitialPatchResult=reused;return reused;}if(session.Nodes.Count!=0){var inconsistent=new InitialGatheringPatchResultV3(false,false,Array.Empty<string>(),0,"Resource patch is partially initialized.");session.InitialPatchResult=inconsistent;return inconsistent;}
        HashSet<Vector2I> excluded=new(){deployment.DeploymentAnchorCell.Value,placement.SettlementCenterCell.Value};foreach(GlobalCellCoord cell in deployment.FormationCells)excluded.Add(cell.Value);
        List<Candidate> trees=new(),stones=new();Vector2I center=deployment.DeploymentAnchorCell.Value;int checkedCount=0;
        for(int y=-settings.MaximumDistance;y<=settings.MaximumDistance&&checkedCount<settings.MaxCandidates;y++)for(int x=-settings.MaximumDistance;x<=settings.MaximumDistance&&checkedCount<settings.MaxCandidates;x++)
        {Vector2I cell=center+new Vector2I(x,y);float distance=MercenaryDistance(cell,center);if(distance<settings.MinimumDistance||distance>settings.MaximumDistance)continue;checkedCount++;if(!bounds.HasPoint(cell)||excluded.Contains(cell))continue;FlatlandCellSampleV2 s=sample(cell);if(!s.IsWalkable||s.IsRoad||s.IsVillage||s.IsStartingVillage)continue;float treePreference=s.ForestStrength*10f+(s.BiomeKind==BiomeKindV3.ForestLand?3f:0f);float stonePreference=(s.BiomeKind==BiomeKindV3.RockyHills?10f:0f)+(s.BiomeKind==BiomeKindV3.Dryland?2f:0f);trees.Add(new(cell,treePreference,distance));stones.Add(new(cell,stonePreference,distance));}
        Comparison<Candidate> compare=(a,b)=>{int c=b.Preference.CompareTo(a.Preference);if(c!=0)return c;c=a.Distance.CompareTo(b.Distance);if(c!=0)return c;c=a.Cell.Y.CompareTo(b.Cell.Y);return c!=0?c:a.Cell.X.CompareTo(b.Cell.X);};trees.Sort(compare);stones.Sort(compare);HashSet<Vector2I> used=new(excluded);List<string> created=new();string lastAddReason=string.Empty;
        bool AddFrom(List<Candidate> candidates,ResourceNodeTypeV3 type,int count,int amount){int added=0;var sameTypeCells=new List<Vector2I>();string definitionId=type==ResourceNodeTypeV3.Tree?NaturalResourceDefinitionCatalogV3.TreeId:NaturalResourceDefinitionCatalogV3.StoneId;foreach(Candidate candidate in candidates){if(added>=count)break;if(used.Contains(candidate.Cell))continue;bool tooClose=false;foreach(Vector2I placed in sameTypeCells)if(MercenaryDistance(placed,candidate.Cell)<3){tooClose=true;break;}if(tooClose)continue;string id=ResourceNodeIdFactoryV3.CreateDeterministic(settings.WorldSeed,definitionId,candidate.Cell);if(!ResourceNodeStateV3.TryCreate(id,type,new(candidate.Cell),amount,amount,settings.YieldPerCycle,bounds,DateTime.UtcNow,out ResourceNodeStateV3? state,out lastAddReason)||state==null)continue;state.SetInitialPlacementMetadata("starting.guarantee",1,ResourcePlacementEvaluatorV3.AlgorithmVersion,NaturalResourceOriginKindV3.StartingGuarantee);if(!session.Nodes.TryRegister(state,out lastAddReason))continue;used.Add(candidate.Cell);sameTypeCells.Add(candidate.Cell);created.Add(id);added++;}if(added<count&&lastAddReason.Length==0)lastAddReason=$"Only {added}/{count} candidates registered.";return added==count;}
        if(!AddFrom(trees,ResourceNodeTypeV3.Tree,settings.TreeCount,settings.TreeAmount)||!AddFrom(stones,ResourceNodeTypeV3.StoneOutcrop,settings.StoneCount,settings.StoneAmount)){foreach(string id in created)session.Nodes.TryRemove(id,out _);var failed=new InitialGatheringPatchResultV3(false,false,Array.Empty<string>(),checkedCount,$"Not enough bounded resource patch candidates. {lastAddReason}");session.InitialPatchResult=failed;return failed;}
        var result=new InitialGatheringPatchResultV3(true,false,created,checkedCount,string.Empty);session.InitialPatchResult=result;return result;
    }
    private static float MercenaryDistance(Vector2I a,Vector2I b)=>Math.Max(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y));
}
