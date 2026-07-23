using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Godot;
using WorldV2;
using GameplayV3.Rooms;

namespace GameplayV3.Bases;

public enum BaseSpatialSourceKindV3 { Structure, Bed, Wall, Door, Floor, StockpileZone, FarmPlot, FutureFacility }
public enum BaseSpatialSourceRoleV3 { Anchor, Attachment, Connector }
public enum BaseAreaRebuildStatusV3 { Stable, Queued, Collecting, Clustering, BuildingAreas, BuildingCellIndex, ReadyToCommit, Committed, Cancelled, Superseded }
public enum BaseAreaChangeKindV3 { Created, Updated, Merged, Split, Removed, Remapped }

public static class BaseAreaIdFactoryV3
{
    private const string Prefix="base_";
    public static string Create()=>Prefix+Guid.NewGuid().ToString("N");
    public static bool IsValid(string? value)=>value is {Length:37}&&value.StartsWith(Prefix,StringComparison.Ordinal)&&value.AsSpan(Prefix.Length).IndexOfAnyExcept("0123456789abcdef")<0;
}

public static class BaseAreaSettingsV3
{
    public const int AnchorConnectionRadiusCells=8;
    public const int AreaPaddingCells=4;
    public const int AttachmentRadiusCells=4;
    public const int StockpileAnchorMinimumCells=4;
    public const int FarmAnchorMinimumCells=9;
    public const int MaxSourceEventsPerTick=128;
    public const int MaxDirtySourcesProcessedPerTick=64;
    public const int MaxBaseRebuildsPerTick=4;
    public const int MaxCellIndexWritesPerTick=8192;
    public const int MaxRecentEvents=16;
}

public sealed class BaseSpatialSourceV3
{
    private readonly ReadOnlyCollection<GlobalCellCoord> _footprint;
    private readonly ReadOnlyCollection<Vector2I> _chunks;
    public BaseSpatialSourceV3(string sourceId,string companyId,BaseSpatialSourceKindV3 kind,BaseSpatialSourceRoleV3 role,IEnumerable<GlobalCellCoord> footprint,int anchorWeight,long creationOrder,long sourceRevision,bool completed=true,bool eligible=true,string definitionId="")
    {
        if(string.IsNullOrWhiteSpace(sourceId)||string.IsNullOrWhiteSpace(companyId))throw new ArgumentException("Source and company ids are required.");
        List<GlobalCellCoord> cells=footprint.Distinct().OrderBy(c=>c.Value.Y).ThenBy(c=>c.Value.X).ToList();
        if(cells.Count==0)throw new ArgumentException("Base source footprint is empty.");
        SourceId=sourceId;CompanyId=companyId;SourceKind=kind;SourceRole=role;AnchorWeight=Math.Max(0,anchorWeight);CreationOrder=creationOrder;SourceRevision=sourceRevision;IsCompleted=completed;IsEligible=eligible;DefinitionId=definitionId;
        _footprint=cells.AsReadOnly();int minX=cells.Min(c=>c.Value.X),minY=cells.Min(c=>c.Value.Y),maxX=cells.Max(c=>c.Value.X),maxY=cells.Max(c=>c.Value.Y);Bounds=new(minX,minY,maxX-minX+1,maxY-minY+1);
        _chunks=cells.Select(c=>ChunkOf(c.Value)).Distinct().OrderBy(c=>c.Y).ThenBy(c=>c.X).ToList().AsReadOnly();
    }
    public string SourceId{get;}public string CompanyId{get;}public BaseSpatialSourceKindV3 SourceKind{get;}public BaseSpatialSourceRoleV3 SourceRole{get;}public IReadOnlyList<GlobalCellCoord> FootprintCells=>_footprint;public Rect2I Bounds{get;}public IReadOnlyList<Vector2I> ChunkCoords=>_chunks;public int AnchorWeight{get;}public long CreationOrder{get;}public long SourceRevision{get;}public bool IsCompleted{get;}public bool IsEligible{get;}public string DefinitionId{get;}
    public bool IsAnchor=>SourceRole==BaseSpatialSourceRoleV3.Anchor&&IsCompleted&&IsEligible;
    public static Vector2I ChunkOf(Vector2I cell)=>WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(cell);
}

public static class BaseSpatialSourceCatalogV3
{
    public static (BaseSpatialSourceKindV3 Kind,BaseSpatialSourceRoleV3 Role,int Weight) ClassifyStructure(string definitionId)
        =>definitionId switch{
            "basic_bed"=>(BaseSpatialSourceKindV3.Bed,BaseSpatialSourceRoleV3.Anchor,4),
            "wooden_wall"=>(BaseSpatialSourceKindV3.Wall,BaseSpatialSourceRoleV3.Connector,0),
            "wooden_door"=>(BaseSpatialSourceKindV3.Door,BaseSpatialSourceRoleV3.Connector,0),
            _=>(BaseSpatialSourceKindV3.Structure,BaseSpatialSourceRoleV3.Attachment,0)};
    public static BaseSpatialSourceRoleV3 ClassifyStockpile(IReadOnlyCollection<GlobalCellCoord> cells)=>cells.Count>=BaseAreaSettingsV3.StockpileAnchorMinimumCells&&IsFourConnected(cells)?BaseSpatialSourceRoleV3.Anchor:BaseSpatialSourceRoleV3.Attachment;
    public static BaseSpatialSourceRoleV3 ClassifyFarm(IReadOnlyCollection<GlobalCellCoord> cells)=>cells.Count>=BaseAreaSettingsV3.FarmAnchorMinimumCells&&IsFourConnected(cells)?BaseSpatialSourceRoleV3.Anchor:BaseSpatialSourceRoleV3.Attachment;
    public static bool IsFourConnected(IReadOnlyCollection<GlobalCellCoord> cells)
    {
        if(cells.Count==0)return false;HashSet<Vector2I> all=new(cells.Select(c=>c.Value));Queue<Vector2I> queue=new();HashSet<Vector2I> seen=new();Vector2I first=all.OrderBy(c=>c.Y).ThenBy(c=>c.X).First();queue.Enqueue(first);seen.Add(first);
        Vector2I[] directions={Vector2I.Up,Vector2I.Right,Vector2I.Down,Vector2I.Left};while(queue.Count>0){Vector2I current=queue.Dequeue();foreach(Vector2I d in directions){Vector2I next=current+d;if(all.Contains(next)&&seen.Add(next))queue.Enqueue(next);}}return seen.Count==all.Count;
    }
}

public sealed class BaseSpatialSourceRegistryV3
{
    private readonly Dictionary<string,BaseSpatialSourceV3> _sources=new(StringComparer.Ordinal);
    private readonly Dictionary<Vector2I,HashSet<string>> _byChunk=new();
    private readonly Dictionary<string,HashSet<string>> _byCompany=new(StringComparer.Ordinal);
    public long Revision{get;private set;}public int Count=>_sources.Count;public int ChunkCount=>_byChunk.Count;
    public bool TryGet(string id,out BaseSpatialSourceV3? source)=>_sources.TryGetValue(id,out source);
    public bool Upsert(BaseSpatialSourceV3 source,out bool changed)
    {changed=false;if(_sources.TryGetValue(source.SourceId,out var old)){if(old.SourceRevision==source.SourceRevision&&Same(old,source))return true;RemoveIndexes(old);}_sources[source.SourceId]=source;AddIndexes(source);Revision++;changed=true;return true;}
    public bool Remove(string id,out BaseSpatialSourceV3? source){if(!_sources.Remove(id,out source)||source==null)return false;RemoveIndexes(source);Revision++;return true;}
    public IReadOnlyList<BaseSpatialSourceV3> GetByCompany(string company)=>_byCompany.TryGetValue(company,out var ids)?ids.Select(id=>_sources[id]).OrderBy(s=>s.CreationOrder).ThenBy(s=>s.SourceId,StringComparer.Ordinal).ToList().AsReadOnly():Array.Empty<BaseSpatialSourceV3>();
    public IEnumerable<BaseSpatialSourceV3> EnumerateChunk(Vector2I chunk){if(!_byChunk.TryGetValue(chunk,out var ids))yield break;foreach(string id in ids)if(_sources.TryGetValue(id,out var source))yield return source;}
    public IReadOnlyList<BaseSpatialSourceV3> GetAll()=>_sources.Values.OrderBy(s=>s.CompanyId,StringComparer.Ordinal).ThenBy(s=>s.CreationOrder).ThenBy(s=>s.SourceId,StringComparer.Ordinal).ToList().AsReadOnly();
    public void Clear(){_sources.Clear();_byChunk.Clear();_byCompany.Clear();Revision++;}
    private void AddIndexes(BaseSpatialSourceV3 source){if(!_byCompany.TryGetValue(source.CompanyId,out var company)){company=new(StringComparer.Ordinal);_byCompany.Add(source.CompanyId,company);}company.Add(source.SourceId);foreach(Vector2I chunk in source.ChunkCoords){if(!_byChunk.TryGetValue(chunk,out var ids)){ids=new(StringComparer.Ordinal);_byChunk.Add(chunk,ids);}ids.Add(source.SourceId);}}
    private void RemoveIndexes(BaseSpatialSourceV3 source){if(_byCompany.TryGetValue(source.CompanyId,out var company)){company.Remove(source.SourceId);if(company.Count==0)_byCompany.Remove(source.CompanyId);}foreach(Vector2I chunk in source.ChunkCoords)if(_byChunk.TryGetValue(chunk,out var ids)){ids.Remove(source.SourceId);if(ids.Count==0)_byChunk.Remove(chunk);}}
    private static bool Same(BaseSpatialSourceV3 a,BaseSpatialSourceV3 b)=>a.CompanyId==b.CompanyId&&a.SourceKind==b.SourceKind&&a.SourceRole==b.SourceRole&&a.AnchorWeight==b.AnchorWeight&&a.IsCompleted==b.IsCompleted&&a.IsEligible==b.IsEligible&&a.FootprintCells.SequenceEqual(b.FootprintCells);
}

public sealed class BaseAreaV3
{
    private readonly ReadOnlyCollection<string> _anchors,_members,_rooms;
    private readonly ReadOnlyCollection<Vector2I> _chunks;
    private readonly HashSet<Vector2I> _cells;
    internal BaseAreaV3(string id,string company,long creation,IEnumerable<string> anchors,IEnumerable<string> members,IEnumerable<string> rooms,IEnumerable<Vector2I> cells,GlobalCellCoord center,long revision,string reason,double milliseconds,IEnumerable<string>? merged=null,IEnumerable<string>? split=null)
    {BaseAreaId=id;CompanyId=company;CreationOrder=creation;_anchors=anchors.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToList().AsReadOnly();_members=members.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToList().AsReadOnly();_rooms=rooms.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToList().AsReadOnly();_cells=new(cells);CenterCell=center;Revision=revision;LastChangeReason=reason;LastRebuildDurationMilliseconds=milliseconds;LastMergeFrom=(merged??Array.Empty<string>()).ToList().AsReadOnly();LastSplitInto=(split??Array.Empty<string>()).ToList().AsReadOnly();int minX=_cells.Min(c=>c.X),minY=_cells.Min(c=>c.Y),maxX=_cells.Max(c=>c.X),maxY=_cells.Max(c=>c.Y);Bounds=new(minX,minY,maxX-minX+1,maxY-minY+1);_chunks=_cells.Select(BaseSpatialSourceV3.ChunkOf).Distinct().OrderBy(c=>c.Y).ThenBy(c=>c.X).ToList().AsReadOnly();}
    public string BaseAreaId{get;}public string CompanyId{get;}public long CreationOrder{get;}public IReadOnlyList<string> AnchorSourceIds=>_anchors;public IReadOnlyList<string> MemberSourceIds=>_members;public IReadOnlyList<string> RoomIds=>_rooms;public Rect2I Bounds{get;}public GlobalCellCoord CenterCell{get;}public IReadOnlyList<Vector2I> ChunkCoords=>_chunks;public int AnchorCount=>_anchors.Count;public int MemberSourceCount=>_members.Count;public int RoomCount=>_rooms.Count;public int InfluenceCellCount=>_cells.Count;public long Revision{get;}public bool IsValid=>AnchorCount>0&&InfluenceCellCount>0;public BaseAreaRebuildStatusV3 RebuildStatus=>BaseAreaRebuildStatusV3.Stable;public string LastChangeReason{get;}public double LastRebuildDurationMilliseconds{get;}public IReadOnlyList<string> LastMergeFrom{get;}public IReadOnlyList<string> LastSplitInto{get;}public bool Contains(Vector2I cell)=>_cells.Contains(cell);public IReadOnlyCollection<Vector2I> GetInfluenceCells()=>new ReadOnlyCollection<Vector2I>(_cells.OrderBy(c=>c.Y).ThenBy(c=>c.X).ToList());
}

public sealed record BaseAreaChangeEventV3(BaseAreaChangeKindV3 Kind,string CompanyId,string BaseAreaId,IReadOnlyList<string> OldBaseAreaIds,IReadOnlyList<string> NewBaseAreaIds,long Revision,double CpuMilliseconds);
public sealed record BaseSpatialSourceChangeEventV3(string SourceId,string CompanyId,bool Removed,long Revision);

public sealed class BaseAreaDiagnosticsV3
{
    public int InitialSyncProcessed{get;internal set;}public int InitialSyncRemaining{get;internal set;}public int DuplicateSourceRegistrationCount{get;internal set;}public int BaseCreatedCount{get;internal set;}public int BaseUpdatedCount{get;internal set;}public int BaseMergedCount{get;internal set;}public int BaseSplitCount{get;internal set;}public int BaseRemovedCount{get;internal set;}public int BaseRemapCount{get;internal set;}public int ConflictRoomCount{get;internal set;}public int SourceEventsProcessedThisTick{get;internal set;}public int AnchorPairChecksThisTick{get;internal set;}public int AttachmentChecksThisTick{get;internal set;}public int RebuildSourcesProcessedThisTick{get;internal set;}public int CellIndexWritesThisTick{get;internal set;}public int ChunkQueriesThisTick{get;internal set;}public int RebuildSliceCount{get;internal set;}public int StaleRebuildCount{get;internal set;}public double RuntimeTickMs{get;internal set;}public double MaxRuntimeMs{get;internal set;}public int FullWorldBaseScanCount=>0;public int FullCompanySourceScanCount=>0;public int AnchorCartesianPairCount=>0;public int BaseTriggeredChunkGenerationCount=>0;public int PerBaseNodeCount=>0;public int PerBaseProcessCount=>0;
    public void ResetTick(){SourceEventsProcessedThisTick=AnchorPairChecksThisTick=AttachmentChecksThisTick=RebuildSourcesProcessedThisTick=CellIndexWritesThisTick=ChunkQueriesThisTick=0;RuntimeTickMs=0;}
}

public sealed class BaseAreaRegistryV3
{
    private readonly Dictionary<string,BaseAreaV3> _areas=new(StringComparer.Ordinal);private readonly Dictionary<string,List<string>> _company=new(StringComparer.Ordinal);private readonly Dictionary<string,string> _source=new(StringComparer.Ordinal);private readonly Dictionary<Vector2I,List<string>> _cell=new();private readonly Dictionary<Vector2I,HashSet<string>> _chunk=new();
    public int Count=>_areas.Count;public int CellIndexEntryCount=>_cell.Count;public int ChunkIndexCount=>_chunk.Count;public bool HasCompanyAreas(string company)=>_company.TryGetValue(company,out var ids)&&ids.Count>0;public bool TryGet(string id,out BaseAreaV3? area)=>_areas.TryGetValue(id,out area);public bool TryGetForSource(string id,out BaseAreaV3? area){area=null;return _source.TryGetValue(id,out var baseId)&&_areas.TryGetValue(baseId,out area);}public IReadOnlyList<BaseAreaV3> GetForCompany(string company)=>_company.TryGetValue(company,out var ids)?ids.Select(id=>_areas[id]).OrderBy(a=>a.CreationOrder).ThenBy(a=>a.BaseAreaId,StringComparer.Ordinal).ToList().AsReadOnly():Array.Empty<BaseAreaV3>();public bool TryGetAtCell(string company,GlobalCellCoord cell,out BaseAreaV3? area){area=null;if(!_cell.TryGetValue(cell.Value,out var ids))return false;foreach(string id in ids)if(_areas.TryGetValue(id,out var value)&&value.CompanyId==company){area=value;return true;}return false;}public IReadOnlyList<BaseAreaV3> GetAtCell(GlobalCellCoord cell)=>_cell.TryGetValue(cell.Value,out var ids)?ids.Select(id=>_areas[id]).OrderBy(a=>a.CompanyId,StringComparer.Ordinal).ThenBy(a=>a.BaseAreaId,StringComparer.Ordinal).ToList().AsReadOnly():Array.Empty<BaseAreaV3>();public IReadOnlyList<string> GetIdsInChunk(Vector2I chunk)=>_chunk.TryGetValue(chunk,out var ids)?ids.OrderBy(x=>x,StringComparer.Ordinal).ToList().AsReadOnly():Array.Empty<string>();public IReadOnlyList<BaseAreaV3> GetAll()=>_areas.Values.OrderBy(a=>a.CompanyId,StringComparer.Ordinal).ThenBy(a=>a.CreationOrder).ToList().AsReadOnly();
    internal void ReplaceCompany(string company,IReadOnlyList<BaseAreaV3> replacements,BaseAreaDiagnosticsV3 diagnostics){List<BaseAreaV3> old=GetForCompany(company).ToList();foreach(var area in old)RemoveIndexes(area);foreach(var area in replacements)AddIndexes(area);diagnostics.CellIndexWritesThisTick+=old.Sum(a=>a.InfluenceCellCount)+replacements.Sum(a=>a.InfluenceCellCount);}
    public void Clear(){_areas.Clear();_company.Clear();_source.Clear();_cell.Clear();_chunk.Clear();}
    private void AddIndexes(BaseAreaV3 area){_areas[area.BaseAreaId]=area;if(!_company.TryGetValue(area.CompanyId,out var list)){list=new();_company.Add(area.CompanyId,list);}list.Add(area.BaseAreaId);foreach(string id in area.MemberSourceIds)_source[id]=area.BaseAreaId;foreach(Vector2I c in area.GetInfluenceCells()){if(!_cell.TryGetValue(c,out var ids)){ids=new();_cell.Add(c,ids);}if(!ids.Contains(area.BaseAreaId,StringComparer.Ordinal))ids.Add(area.BaseAreaId);}foreach(Vector2I chunk in area.ChunkCoords){if(!_chunk.TryGetValue(chunk,out var ids)){ids=new(StringComparer.Ordinal);_chunk.Add(chunk,ids);}ids.Add(area.BaseAreaId);}}
    private void RemoveIndexes(BaseAreaV3 area){_areas.Remove(area.BaseAreaId);if(_company.TryGetValue(area.CompanyId,out var list)){list.Remove(area.BaseAreaId);if(list.Count==0)_company.Remove(area.CompanyId);}foreach(string id in area.MemberSourceIds)if(_source.GetValueOrDefault(id)==area.BaseAreaId)_source.Remove(id);foreach(Vector2I c in area.GetInfluenceCells())if(_cell.TryGetValue(c,out var ids)){ids.Remove(area.BaseAreaId);if(ids.Count==0)_cell.Remove(c);}foreach(Vector2I chunk in area.ChunkCoords)if(_chunk.TryGetValue(chunk,out var ids)){ids.Remove(area.BaseAreaId);if(ids.Count==0)_chunk.Remove(chunk);}}
}

public sealed class BaseAreaSessionV3
{
    private readonly HashSet<string> _dirtyCompanies=new(StringComparer.Ordinal);private readonly HashSet<string> _retiredIds=new(StringComparer.Ordinal);private readonly Queue<BaseAreaChangeEventV3> _recent=new();private readonly Dictionary<string,long> _companyRevision=new(StringComparer.Ordinal);private readonly Dictionary<string,BaseRebuildWorkV3> _rebuilds=new(StringComparer.Ordinal);private IReadOnlyList<RoomTopologyStateV3> _rooms=Array.Empty<RoomTopologyStateV3>();private long _creationOrder,_areaRevision,_sourceCreationOrder;private Rect2I _worldBounds=new(-1_000_000,-1_000_000,2_000_000,2_000_000);
    public BaseAreaSessionV3(long sessionRevision){SessionRevision=sessionRevision;}public long SessionRevision{get;}public BaseSpatialSourceRegistryV3 Sources{get;}=new();public BaseAreaRegistryV3 Areas{get;}=new();public BaseAreaDiagnosticsV3 Diagnostics{get;}=new();public int DirtyCompanyCount=>_dirtyCompanies.Count;public int ActiveRebuildCount=>_rebuilds.Count;public IReadOnlyCollection<string> RetiredBaseAreaIds=>_retiredIds;public event Action<BaseAreaChangeEventV3>? Changed;public event Action<BaseSpatialSourceChangeEventV3>? SourceChanged;public event Action? RoomSnapshotChanged;public event Action<string>? RebuildCompleted;
    public void SetWorldBounds(Rect2I bounds){if(bounds.Size.X>0&&bounds.Size.Y>0)_worldBounds=bounds;}
    public void UpdateRoomSnapshot(IReadOnlyList<RoomTopologyStateV3> rooms){_rooms=rooms;foreach(string company in Sources.GetAll().Where(s=>s.IsAnchor).Select(s=>s.CompanyId).Distinct(StringComparer.Ordinal))MarkDirty(company);RoomSnapshotChanged?.Invoke();}
    public IReadOnlyList<RoomTopologyStateV3> GetRoomSnapshot()=>_rooms;
    public bool IsCompanyDirty(string company)=>_dirtyCompanies.Contains(company);
    public long NextSourceCreationOrder()=>++_sourceCreationOrder;
    public bool UpsertSource(BaseSpatialSourceV3 source){Sources.TryGet(source.SourceId,out var old);Sources.Upsert(source,out bool changed);if(!changed){Diagnostics.DuplicateSourceRegistrationCount++;return false;}MarkDirty(old?.CompanyId);MarkDirty(source.CompanyId);SourceChanged?.Invoke(new(source.SourceId,source.CompanyId,false,Sources.Revision));return true;}
    public bool RemoveSource(string id){if(!Sources.Remove(id,out var old)||old==null)return false;MarkDirty(old.CompanyId);SourceChanged?.Invoke(new(id,old.CompanyId,true,Sources.Revision));return true;}
    public void MarkDirty(string? company){if(string.IsNullOrWhiteSpace(company))return;_dirtyCompanies.Add(company);_companyRevision[company]=_companyRevision.GetValueOrDefault(company)+1;}
    public int RebuildDirtyCompanies(int budget=BaseAreaSettingsV3.MaxBaseRebuildsPerTick){int count=0;foreach(string company in _dirtyCompanies.OrderBy(x=>x,StringComparer.Ordinal).Take(Math.Max(0,budget)).ToList()){long token=_companyRevision.GetValueOrDefault(company);int total=Math.Max(1,Sources.GetByCompany(company).Count);if(!_rebuilds.TryGetValue(company,out var work)||work.Token!=token){if(work!=null)Diagnostics.StaleRebuildCount++;work=new(token,total);_rebuilds[company]=work;}int processed=Math.Min(2048,work.Total-work.Cursor);work.Cursor+=processed;Diagnostics.RebuildSourcesProcessedThisTick+=processed;Diagnostics.RebuildSliceCount++;if(work.Cursor>=work.Total){RebuildCompany(company,token);if(token==_companyRevision.GetValueOrDefault(company)){_dirtyCompanies.Remove(company);_rebuilds.Remove(company);}}count++;}return count;}
    public void RebuildCompanyNow(string company){MarkDirty(company);int guard=0;while(_dirtyCompanies.Contains(company)&&guard++<100000)RebuildDirtyCompanies(1);if(guard>=100000)throw new InvalidOperationException("Base rebuild did not converge.");}
    public IReadOnlyList<BaseAreaChangeEventV3> GetRecentEvents()=>_recent.ToList().AsReadOnly();
    public void Clear(){Sources.Clear();Areas.Clear();_dirtyCompanies.Clear();_retiredIds.Clear();_recent.Clear();_companyRevision.Clear();_rebuilds.Clear();_creationOrder=_areaRevision=_sourceCreationOrder=0;}
    private void RebuildCompany(string company,long token)
    {
        Stopwatch watch=Stopwatch.StartNew();IReadOnlyList<BaseSpatialSourceV3> sources=Sources.GetByCompany(company);List<BaseSpatialSourceV3> anchors=sources.Where(s=>s.IsAnchor).ToList();List<BaseSpatialSourceV3> attachments=sources.Where(s=>!s.IsAnchor&&s.IsCompleted&&s.IsEligible).ToList();List<List<BaseSpatialSourceV3>> components=BuildAnchorComponents(anchors);List<BaseAreaV3> old=Areas.GetForCompany(company).ToList();List<ComponentBuildV3> builds=components.Select(c=>new ComponentBuildV3(c)).ToList();AssignAttachments(builds,attachments,old);AssignIdentities(builds,old);AssignRooms(builds,company);if(token!=_companyRevision.GetValueOrDefault(company)){Diagnostics.StaleRebuildCount++;MarkDirty(company);return;}watch.Stop();List<BaseAreaV3> next=new();foreach(ComponentBuildV3 build in builds){HashSet<Vector2I> cells=BuildInfluence(build);GlobalCellCoord center=CalculateCenter(build.Anchors);BaseAreaV3? previous=old.FirstOrDefault(a=>a.BaseAreaId==build.BaseAreaId);string reason=previous==null?"Created":build.MergedFrom.Count>0?"Merged":old.Count>1&&builds.Count>1?"Split":"Updated";next.Add(new(build.BaseAreaId,company,build.CreationOrder,build.Anchors.Select(s=>s.SourceId),build.AllSources.Select(s=>s.SourceId),build.RoomIds,cells,center,++_areaRevision,reason,watch.Elapsed.TotalMilliseconds,build.MergedFrom,build.SplitInto));}
        Areas.ReplaceCompany(company,next,Diagnostics);PublishDiff(company,old,next,watch.Elapsed.TotalMilliseconds);RebuildCompleted?.Invoke(company);Diagnostics.RebuildSliceCount++;
    }
    private List<List<BaseSpatialSourceV3>> BuildAnchorComponents(List<BaseSpatialSourceV3> anchors)
    {
        Dictionary<string,BaseSpatialSourceV3> byId=anchors.ToDictionary(a=>a.SourceId,StringComparer.Ordinal);HashSet<string> remaining=new(byId.Keys,StringComparer.Ordinal);List<List<BaseSpatialSourceV3>> result=new();
        while(remaining.Count>0){string seed=remaining.OrderBy(id=>id,StringComparer.Ordinal).First();remaining.Remove(seed);Queue<BaseSpatialSourceV3> queue=new();queue.Enqueue(byId[seed]);List<BaseSpatialSourceV3> component=new();while(queue.Count>0){var current=queue.Dequeue();component.Add(current);foreach(var candidate in QueryNearbyAnchors(current,byId,remaining)){Diagnostics.AnchorPairChecksThisTick++;if(FootprintDistance(current,candidate)<=BaseAreaSettingsV3.AnchorConnectionRadiusCells&&remaining.Remove(candidate.SourceId))queue.Enqueue(candidate);}}component.Sort((a,b)=>string.CompareOrdinal(a.SourceId,b.SourceId));result.Add(component);}
        result.Sort((a,b)=>string.CompareOrdinal(a[0].SourceId,b[0].SourceId));return result;
    }
    private IEnumerable<BaseSpatialSourceV3> QueryNearbyAnchors(BaseSpatialSourceV3 source,Dictionary<string,BaseSpatialSourceV3> byId,HashSet<string> remaining){HashSet<string> yielded=new(StringComparer.Ordinal);foreach(Vector2I chunk in ChunksForExpanded(source.Bounds,BaseAreaSettingsV3.AnchorConnectionRadiusCells)){Diagnostics.ChunkQueriesThisTick++;foreach(var candidate in Sources.EnumerateChunk(chunk))if(candidate.IsAnchor&&candidate.CompanyId==source.CompanyId&&remaining.Contains(candidate.SourceId)&&byId.ContainsKey(candidate.SourceId)&&yielded.Add(candidate.SourceId))yield return candidate;}}
    private void AssignAttachments(List<ComponentBuildV3> builds,List<BaseSpatialSourceV3> attachments,List<BaseAreaV3> old)
    {
        Dictionary<Vector2I,List<ComponentBuildV3>> byChunk=new();foreach(var build in builds)foreach(var anchor in build.Anchors)foreach(Vector2I chunk in ChunksForExpanded(anchor.Bounds,BaseAreaSettingsV3.AttachmentRadiusCells)){if(!byChunk.TryGetValue(chunk,out var list)){list=new();byChunk.Add(chunk,list);}if(!list.Contains(build))list.Add(build);}
        foreach(var source in attachments){HashSet<ComponentBuildV3> candidates=new();foreach(Vector2I chunk in source.ChunkCoords)if(byChunk.TryGetValue(chunk,out var list))foreach(var build in list)candidates.Add(build);ComponentBuildV3? best=null;int bestDistance=int.MaxValue;foreach(var build in candidates){Diagnostics.AttachmentChecksThisTick++;int distance=build.Anchors.Min(anchor=>FootprintDistance(anchor,source));if(distance>BaseAreaSettingsV3.AttachmentRadiusCells)continue;if(best==null||distance<bestDistance||distance==bestDistance&&CompareAttachmentTarget(build,best,source,old)<0){best=build;bestDistance=distance;}}best?.Attachments.Add(source);}
    }
    private static int CompareAttachmentTarget(ComponentBuildV3 a,ComponentBuildV3 b,BaseSpatialSourceV3 source,List<BaseAreaV3> old){string existing=old.FirstOrDefault(x=>x.MemberSourceIds.Contains(source.SourceId,StringComparer.Ordinal))?.BaseAreaId??"";bool ae=a.OverlapIds.Contains(existing),be=b.OverlapIds.Contains(existing);if(ae!=be)return ae?-1:1;int weight=b.Anchors.Sum(x=>x.AnchorWeight).CompareTo(a.Anchors.Sum(x=>x.AnchorWeight));if(weight!=0)return weight;return string.CompareOrdinal(a.CanonicalSourceId,b.CanonicalSourceId);}
    private void AssignIdentities(List<ComponentBuildV3> builds,List<BaseAreaV3> old)
    {
        foreach(var build in builds)foreach(var area in old)if(build.AllSources.Any(s=>area.MemberSourceIds.Contains(s.SourceId,StringComparer.Ordinal)))build.OverlapIds.Add(area.BaseAreaId);
        HashSet<string> claimed=new(StringComparer.Ordinal);foreach(var build in builds.OrderByDescending(b=>b.Anchors.Count).ThenBy(b=>b.CanonicalSourceId,StringComparer.Ordinal)){var candidates=old.Where(a=>!claimed.Contains(a.BaseAreaId)).Select(a=>new{Area=a,Anchor=build.Anchors.Count(s=>a.AnchorSourceIds.Contains(s.SourceId,StringComparer.Ordinal)),Member=build.AllSources.Count(s=>a.MemberSourceIds.Contains(s.SourceId,StringComparer.Ordinal))}).Where(x=>x.Anchor+x.Member>0).OrderByDescending(x=>x.Anchor).ThenByDescending(x=>x.Member).ThenBy(x=>x.Area.CreationOrder).ThenBy(x=>x.Area.BaseAreaId,StringComparer.Ordinal).ToList();if(candidates.Count>0){var winner=candidates[0].Area;build.BaseAreaId=winner.BaseAreaId;build.CreationOrder=winner.CreationOrder;claimed.Add(winner.BaseAreaId);build.MergedFrom.AddRange(build.OverlapIds.Where(id=>id!=winner.BaseAreaId));}else{do build.BaseAreaId=BaseAreaIdFactoryV3.Create();while(_retiredIds.Contains(build.BaseAreaId));build.CreationOrder=++_creationOrder;}}
        foreach(var area in old.Where(a=>!builds.Any(b=>b.BaseAreaId==a.BaseAreaId)))_retiredIds.Add(area.BaseAreaId);
        foreach(var oldArea in old){var descendants=builds.Where(b=>b.AllSources.Any(s=>oldArea.MemberSourceIds.Contains(s.SourceId,StringComparer.Ordinal))).ToList();if(descendants.Count>1)foreach(var build in descendants)build.SplitInto.AddRange(descendants.Where(x=>x!=build).Select(x=>x.BaseAreaId));}
    }
    private void AssignRooms(List<ComponentBuildV3> builds,string company){if(builds.Count==0||_rooms.Count==0)return;Dictionary<Vector2I,HashSet<ComponentBuildV3>> anchorsByCell=new();foreach(var build in builds)foreach(Vector2I cell in build.Anchors.SelectMany(a=>a.FootprintCells).Select(c=>c.Value)){if(!anchorsByCell.TryGetValue(cell,out var set)){set=new();anchorsByCell.Add(cell,set);}set.Add(build);}HashSet<Vector2I> foreign=new(Sources.GetAll().Where(s=>s.IsAnchor&&s.CompanyId!=company).SelectMany(s=>s.FootprintCells).Select(c=>c.Value));foreach(var room in _rooms){HashSet<ComponentBuildV3> owners=new();bool conflict=false;foreach(var cell in room.Cells){if(foreign.Contains(cell.Value))conflict=true;if(anchorsByCell.TryGetValue(cell.Value,out var set))foreach(var build in set)owners.Add(build);}if(conflict||owners.Count>1){if(owners.Count>0)Diagnostics.ConflictRoomCount++;continue;}if(owners.Count==1){ComponentBuildV3 owner=owners.First();owner.RoomIds.Add(room.RoomId);foreach(var cell in room.Cells)owner.RoomCells.Add(cell.Value);}}}
    private HashSet<Vector2I> BuildInfluence(ComponentBuildV3 build){HashSet<Vector2I> cells=new();foreach(var anchor in build.Anchors)foreach(var c in anchor.FootprintCells)for(int y=-BaseAreaSettingsV3.AreaPaddingCells;y<=BaseAreaSettingsV3.AreaPaddingCells;y++)for(int x=-BaseAreaSettingsV3.AreaPaddingCells;x<=BaseAreaSettingsV3.AreaPaddingCells;x++){Vector2I value=c.Value+new Vector2I(x,y);if(_worldBounds.HasPoint(value))cells.Add(value);}foreach(var attachment in build.Attachments)foreach(var c in attachment.FootprintCells)if(_worldBounds.HasPoint(c.Value))cells.Add(c.Value);foreach(Vector2I cell in build.RoomCells)if(_worldBounds.HasPoint(cell))cells.Add(cell);return cells;}
    private static GlobalCellCoord CalculateCenter(List<BaseSpatialSourceV3> anchors){double sumX=0,sumY=0,total=0;foreach(var source in anchors){double x=source.FootprintCells.Average(c=>c.Value.X),y=source.FootprintCells.Average(c=>c.Value.Y),weight=Math.Max(1,source.AnchorWeight);sumX+=x*weight;sumY+=y*weight;total+=weight;}double targetX=sumX/total,targetY=sumY/total;return anchors.SelectMany(a=>a.FootprintCells.Select(c=>new{Cell=c,Weight=a.AnchorWeight,Order=a.CreationOrder,Id=a.SourceId,Distance=(c.Value.X-targetX)*(c.Value.X-targetX)+(c.Value.Y-targetY)*(c.Value.Y-targetY)})).OrderBy(x=>x.Distance).ThenByDescending(x=>x.Weight).ThenBy(x=>x.Order).ThenBy(x=>x.Id,StringComparer.Ordinal).ThenBy(x=>x.Cell.Value.Y).ThenBy(x=>x.Cell.Value.X).First().Cell;}
    public static int FootprintDistance(BaseSpatialSourceV3 a,BaseSpatialSourceV3 b){int dx=Math.Max(0,Math.Max(a.Bounds.Position.X-b.Bounds.End.X,b.Bounds.Position.X-a.Bounds.End.X)+1),dy=Math.Max(0,Math.Max(a.Bounds.Position.Y-b.Bounds.End.Y,b.Bounds.Position.Y-a.Bounds.End.Y)+1);int lower=Math.Max(dx,dy);if(lower>BaseAreaSettingsV3.AnchorConnectionRadiusCells&&lower>BaseAreaSettingsV3.AttachmentRadiusCells)return lower;int best=int.MaxValue;foreach(var ca in a.FootprintCells)foreach(var cb in b.FootprintCells){int d=Math.Max(Math.Abs(ca.Value.X-cb.Value.X),Math.Abs(ca.Value.Y-cb.Value.Y));if(d<best)best=d;}return best;}
    private static IEnumerable<Vector2I> ChunksForExpanded(Rect2I bounds,int radius){Vector2I min=BaseSpatialSourceV3.ChunkOf(bounds.Position-new Vector2I(radius,radius)),max=BaseSpatialSourceV3.ChunkOf(bounds.End-Vector2I.One+new Vector2I(radius,radius));for(int y=min.Y;y<=max.Y;y++)for(int x=min.X;x<=max.X;x++)yield return new(x,y);}
    private void PublishDiff(string company,List<BaseAreaV3> old,List<BaseAreaV3> next,double ms){foreach(var area in next){var previous=old.FirstOrDefault(x=>x.BaseAreaId==area.BaseAreaId);BaseAreaChangeKindV3 kind=previous==null?BaseAreaChangeKindV3.Created:area.LastMergeFrom.Count>0?BaseAreaChangeKindV3.Merged:BaseAreaChangeKindV3.Updated;if(kind==BaseAreaChangeKindV3.Created)Diagnostics.BaseCreatedCount++;else if(kind==BaseAreaChangeKindV3.Merged){Diagnostics.BaseMergedCount++;Diagnostics.BaseRemapCount+=area.LastMergeFrom.Count;}else Diagnostics.BaseUpdatedCount++;Emit(new(kind,company,area.BaseAreaId,area.LastMergeFrom,new[]{area.BaseAreaId},area.Revision,ms));}foreach(var area in old.Where(a=>next.All(n=>n.BaseAreaId!=a.BaseAreaId))){Diagnostics.BaseRemovedCount++;Emit(new(BaseAreaChangeKindV3.Removed,company,area.BaseAreaId,new[]{area.BaseAreaId},Array.Empty<string>(),++_areaRevision,ms));}if(next.Count>old.Count&&old.Count>0){Diagnostics.BaseSplitCount++;Emit(new(BaseAreaChangeKindV3.Split,company,next[0].BaseAreaId,old.Select(a=>a.BaseAreaId).ToList(),next.Select(a=>a.BaseAreaId).ToList(),++_areaRevision,ms));}}
    private void Emit(BaseAreaChangeEventV3 value){_recent.Enqueue(value);while(_recent.Count>BaseAreaSettingsV3.MaxRecentEvents)_recent.Dequeue();Changed?.Invoke(value);}
    private sealed class ComponentBuildV3{public ComponentBuildV3(List<BaseSpatialSourceV3> anchors){Anchors=anchors;}public List<BaseSpatialSourceV3> Anchors{get;}public List<BaseSpatialSourceV3> Attachments{get;}=new();public IEnumerable<BaseSpatialSourceV3> AllSources=>Anchors.Concat(Attachments);public string CanonicalSourceId=>Anchors[0].SourceId;public string BaseAreaId{get;set;}="";public long CreationOrder{get;set;}public HashSet<string> OverlapIds{get;}=new(StringComparer.Ordinal);public List<string> MergedFrom{get;}=new();public List<string> SplitInto{get;}=new();public List<string> RoomIds{get;}=new();public HashSet<Vector2I> RoomCells{get;}=new();}
    private sealed class BaseRebuildWorkV3{public BaseRebuildWorkV3(long token,int total){Token=token;Total=total;}public long Token{get;}public int Total{get;}public int Cursor{get;set;}}
}
