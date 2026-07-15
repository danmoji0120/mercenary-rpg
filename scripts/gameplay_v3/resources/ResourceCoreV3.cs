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
    public static bool IsValid(string? value) => ResourceIdValidationV3.IsCanonical(value, Prefix);
}

public static class GroundResourceStackIdFactoryV3
{
    private const string Prefix = "rstack_";
    public static string Create() => Prefix + Guid.NewGuid().ToString("N");
    public static bool IsValid(string? value) => ResourceIdValidationV3.IsCanonical(value, Prefix);
}

public enum ResourceTypeV3 { Wood, Stone }
public enum ResourceNodeTypeV3 { Tree, StoneOutcrop }

public sealed class ResourceNodeStateV3
{
    private ResourceNodeStateV3(string id, ResourceNodeTypeV3 nodeType, ResourceTypeV3 produced, GlobalCellCoord cell, int remaining, int max, int yield, DateTime created)
    { ResourceNodeId=id;NodeType=nodeType;ProducedResourceType=produced;Cell=cell;RemainingAmount=remaining;MaxAmount=max;YieldPerCycle=yield;CreatedUtc=created.Kind==DateTimeKind.Utc?created:created.ToUniversalTime(); }
    public string ResourceNodeId{get;} public ResourceNodeTypeV3 NodeType{get;} public ResourceTypeV3 ProducedResourceType{get;} public GlobalCellCoord Cell{get;} public int RemainingAmount{get;private set;} public int MaxAmount{get;} public int YieldPerCycle{get;} public bool IsDepleted=>RemainingAmount==0; public DateTime CreatedUtc{get;}
    public static bool TryCreate(string id,ResourceNodeTypeV3 type,GlobalCellCoord cell,int remaining,int max,int yield,Rect2I bounds,DateTime created,out ResourceNodeStateV3? state,out string reason)
    {
        if(!ResourceNodeIdFactoryV3.IsValid(id)){state=null;reason="ResourceNodeId is not canonical.";return false;}
        if(!Enum.IsDefined(type)||max<1||remaining<0||remaining>max||yield<1){state=null;reason="Resource node amounts or type are invalid.";return false;}
        if(!bounds.HasPoint(cell.Value)){state=null;reason="Resource node cell is outside world bounds.";return false;}
        ResourceTypeV3 produced=type==ResourceNodeTypeV3.Tree?ResourceTypeV3.Wood:ResourceTypeV3.Stone;
        state=new(id,type,produced,cell,remaining,max,yield,created);reason=string.Empty;return true;
    }
    public bool TryHarvest(out int amount,out string reason)
    { if(IsDepleted){amount=0;reason="Resource node is depleted.";return false;}amount=Math.Min(YieldPerCycle,RemainingAmount);RemainingAmount-=amount;reason=string.Empty;return true; }
}

public sealed class ResourceNodeRegistryV3
{
    private readonly Dictionary<string,ResourceNodeStateV3> _byId=new(StringComparer.Ordinal);private readonly Dictionary<Vector2I,string> _byCell=new();
    public int Count=>_byId.Count;
    public bool TryRegister(ResourceNodeStateV3? state,out string reason){if(state==null){reason="Resource node is required.";return false;}if(_byId.ContainsKey(state.ResourceNodeId)){reason="Duplicate ResourceNodeId.";return false;}if(_byCell.ContainsKey(state.Cell.Value)){reason="Resource node cell is occupied.";return false;}_byId.Add(state.ResourceNodeId,state);_byCell.Add(state.Cell.Value,state.ResourceNodeId);reason=string.Empty;return true;}
    public bool TryGet(string id,out ResourceNodeStateV3? state)=>_byId.TryGetValue(id,out state);
    public bool Contains(string id)=>_byId.ContainsKey(id);
    public bool TryRemove(string id,out ResourceNodeStateV3? state){if(!_byId.Remove(id,out state)||state==null)return false;_byCell.Remove(state.Cell.Value);return true;}
    public IReadOnlyList<string> GetAllNodeIds(){List<string> ids=new(_byId.Keys);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    public IReadOnlyList<string> GetNodesByType(ResourceNodeTypeV3 type){List<string> ids=new();foreach(var pair in _byId)if(pair.Value.NodeType==type)ids.Add(pair.Key);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    public bool ContainsCell(Vector2I cell)=>_byCell.ContainsKey(cell);
    public void Clear(){_byId.Clear();_byCell.Clear();}
}

public sealed class GroundResourceStackV3
{
    internal GroundResourceStackV3(string id,ResourceTypeV3 type,int amount,GlobalCellCoord cell,DateTime created){ResourceStackId=id;ResourceType=type;Amount=amount;Cell=cell;CreatedUtc=created.Kind==DateTimeKind.Utc?created:created.ToUniversalTime();}
    public string ResourceStackId{get;} public ResourceTypeV3 ResourceType{get;} public int Amount{get;private set;} public GlobalCellCoord Cell{get;} public DateTime CreatedUtc{get;}
    internal bool TryMerge(int amount,out string reason){if(amount<1||Amount>int.MaxValue-amount){reason="Ground stack amount is invalid or would overflow.";return false;}Amount+=amount;reason=string.Empty;return true;}
    internal bool TryTake(int requested,out int taken,out string reason){taken=0;if(requested<1){reason="Requested amount must be positive.";return false;}if(Amount<1){reason="SourceStackEmpty";return false;}taken=Math.Min(requested,Amount);Amount-=taken;reason=string.Empty;return true;}
}

public sealed class GroundResourceStackRegistryV3
{
    private readonly Dictionary<string,GroundResourceStackV3> _byId=new(StringComparer.Ordinal);private readonly Dictionary<(Vector2I,ResourceTypeV3),string> _mergeIndex=new();private readonly Dictionary<Vector2I,HashSet<string>> _byCell=new();
    public int Count=>_byId.Count;
    public bool TryAddStack(ResourceTypeV3 type,int amount,GlobalCellCoord cell,out GroundResourceStackV3? stack,out bool merged,out string reason)
    {stack=null;merged=false;if(!Enum.IsDefined(type)||amount<1){reason="Ground stack type or amount is invalid.";return false;}var key=(cell.Value,type);if(_mergeIndex.TryGetValue(key,out string? existing)&&_byId.TryGetValue(existing,out stack)){merged=stack.TryMerge(amount,out reason);return merged;}string id=GroundResourceStackIdFactoryV3.Create();stack=new(id,type,amount,cell,DateTime.UtcNow);_byId.Add(id,stack);_mergeIndex.Add(key,id);if(!_byCell.TryGetValue(cell.Value,out HashSet<string>? ids)){ids=new(StringComparer.Ordinal);_byCell.Add(cell.Value,ids);}ids.Add(id);reason=string.Empty;return true;}
    public bool TryGet(string id,out GroundResourceStackV3? stack)=>_byId.TryGetValue(id,out stack);
    public bool Contains(string id)=>_byId.ContainsKey(id);
    public bool TryTakeAmount(string id,int requested,out int taken,out GroundResourceStackV3? changed,out bool removed,out string reason)
    {taken=0;changed=null;removed=false;if(!_byId.TryGetValue(id,out GroundResourceStackV3? stack)){reason="InvalidSourceStack";return false;}if(!stack.TryTake(requested,out taken,out reason))return false;changed=stack;if(stack.Amount==0){TryRemove(id,out changed);removed=true;}return true;}
    public bool TryAddOrMerge(ResourceTypeV3 type,int amount,GlobalCellCoord cell,out GroundResourceStackV3? stack,out bool merged,out string reason)=>TryAddStack(type,amount,cell,out stack,out merged,out reason);
    public bool TryAddBatchAtomic(IReadOnlyList<(ResourceTypeV3 Type,int Amount,GlobalCellCoord Cell)> entries,out IReadOnlyList<string> affectedStackIds,out string reason)
    {List<(string Id,int Amount)> committed=new();List<string> affected=new();foreach(var entry in entries){if(!Enum.IsDefined(entry.Type)||entry.Amount<1){reason="InvalidSalvageAmount";Rollback();affectedStackIds=Array.Empty<string>();return false;}if(_mergeIndex.TryGetValue((entry.Cell.Value,entry.Type),out string? existingId)&&_byId.TryGetValue(existingId,out GroundResourceStackV3? existing)&&existing.Amount>int.MaxValue-entry.Amount){reason="SalvageAmountOverflow";Rollback();affectedStackIds=Array.Empty<string>();return false;}if(!TryAddStack(entry.Type,entry.Amount,entry.Cell,out GroundResourceStackV3? stack,out _,out reason)||stack==null){Rollback();affectedStackIds=Array.Empty<string>();return false;}committed.Add((stack.ResourceStackId,entry.Amount));affected.Add(stack.ResourceStackId);}affectedStackIds=affected.Distinct(StringComparer.Ordinal).ToList().AsReadOnly();reason=string.Empty;return true;void Rollback(){for(int i=committed.Count-1;i>=0;i--){var c=committed[i];if(!_byId.TryGetValue(c.Id,out GroundResourceStackV3? stack)||stack==null)continue;stack.TryTake(c.Amount,out _,out _);if(stack.Amount==0)TryRemove(c.Id,out _);}}}
    public bool TryGetSingleStackAtCellAndType(GlobalCellCoord cell,ResourceTypeV3 type,out GroundResourceStackV3? stack){stack=null;return _mergeIndex.TryGetValue((cell.Value,type),out string? id)&&_byId.TryGetValue(id,out stack);}
    public IReadOnlyList<string> GetAllStackIds(){List<string> ids=new(_byId.Keys);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    public IReadOnlyList<GroundResourceStackV3> GetStacksAtCell(GlobalCellCoord cell){List<GroundResourceStackV3> result=new();if(_byCell.TryGetValue(cell.Value,out HashSet<string>? ids))foreach(string id in ids)result.Add(_byId[id]);result.Sort((a,b)=>a.ResourceType.CompareTo(b.ResourceType));return result.AsReadOnly();}
    public bool TryRemove(string id,out GroundResourceStackV3? stack){if(!_byId.Remove(id,out stack)||stack==null)return false;_mergeIndex.Remove((stack.Cell.Value,stack.ResourceType));if(_byCell.TryGetValue(stack.Cell.Value,out HashSet<string>? ids)){ids.Remove(id);if(ids.Count==0)_byCell.Remove(stack.Cell.Value);}return true;}
    public int GetTotalAmount(ResourceTypeV3 type){int total=0;foreach(GroundResourceStackV3 stack in _byId.Values)if(stack.ResourceType==type)total+=stack.Amount;return total;}
    public void Clear(){_byId.Clear();_mergeIndex.Clear();_byCell.Clear();}
}

public sealed class InitialGatheringPatchResultV3
{
    public InitialGatheringPatchResultV3(bool success,bool reused,IReadOnlyList<string> ids,int candidates,string reason){Succeeded=success;ReusedExisting=reused;NodeIds=new ReadOnlyCollection<string>(new List<string>(ids));CandidatesChecked=candidates;FailureReason=reason;}
    public bool Succeeded{get;} public bool ReusedExisting{get;} public IReadOnlyList<string> NodeIds{get;} public int CandidatesChecked{get;} public string FailureReason{get;}
}

public sealed class ResourceSessionV3
{
    public ResourceNodeRegistryV3 Nodes{get;}=new();public GroundResourceStackRegistryV3 GroundStacks{get;}=new();public InitialGatheringPatchResultV3? InitialPatchResult{get;internal set;}
}

public sealed class InitialGatheringPatchPlacementSettingsV3
{
    public int TreeCount{get;init;}=6;public int StoneCount{get;init;}=4;public int MinimumDistance{get;init;}=10;public int MaximumDistance{get;init;}=24;public int MaxCandidates{get;init;}=2401;public int TreeAmount{get;init;}=15;public int StoneAmount{get;init;}=20;public int YieldPerCycle{get;init;}=5;
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
        Comparison<Candidate> compare=(a,b)=>{int c=b.Preference.CompareTo(a.Preference);if(c!=0)return c;c=a.Distance.CompareTo(b.Distance);if(c!=0)return c;c=a.Cell.Y.CompareTo(b.Cell.Y);return c!=0?c:a.Cell.X.CompareTo(b.Cell.X);};trees.Sort(compare);stones.Sort(compare);HashSet<Vector2I> used=new(excluded);List<string> created=new();
        bool AddFrom(List<Candidate> candidates,ResourceNodeTypeV3 type,int count,int amount){int added=0;foreach(Candidate candidate in candidates){if(added>=count)break;if(!used.Add(candidate.Cell))continue;string id=ResourceNodeIdFactoryV3.Create();if(!ResourceNodeStateV3.TryCreate(id,type,new(candidate.Cell),amount,amount,settings.YieldPerCycle,bounds,DateTime.UtcNow,out ResourceNodeStateV3? state,out _)||!session.Nodes.TryRegister(state,out _))continue;created.Add(id);added++;}return added==count;}
        if(!AddFrom(trees,ResourceNodeTypeV3.Tree,settings.TreeCount,settings.TreeAmount)||!AddFrom(stones,ResourceNodeTypeV3.StoneOutcrop,settings.StoneCount,settings.StoneAmount)){foreach(string id in created)session.Nodes.TryRemove(id,out _);var failed=new InitialGatheringPatchResultV3(false,false,Array.Empty<string>(),checkedCount,"Not enough bounded resource patch candidates.");session.InitialPatchResult=failed;return failed;}
        var result=new InitialGatheringPatchResultV3(true,false,created,checkedCount,string.Empty);session.InitialPatchResult=result;return result;
    }
    private static float MercenaryDistance(Vector2I a,Vector2I b)=>Math.Max(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y));
}
