using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameplayV3.Construction;
using GameplayV3.Needs;
using GameplayV3.Stockpile;
using Godot;
using WorldV2;

namespace GameplayV3.Rooms;

public static class RoomIdFactoryV3{private const string Prefix="room_";public static string Create()=>Prefix+Guid.NewGuid().ToString("N");}
public enum RoomTopologyStatusV3{Stable,Rebuilding,Removed,TooLarge,Invalid}
public enum RoomRebuildReasonV3{BoundaryAdded,BoundaryRemoved,BoundaryChanged,InitialScan}
public enum RoomTopologyChangeKindV3{BoundaryAdded,BoundaryRemoved,BoundaryChanged}
public enum RoomRoleV3{General,Bedroom,Storage,Mixed}
public enum RoomQualityTierV3{Poor,Basic,Comfortable}
public enum RoomAffiliationKindV3{Unowned,SingleCompany,Mixed,Contested}

public sealed class RoomTopologyStateV3
{
    private readonly ReadOnlyCollection<GlobalCellCoord> _cells;private readonly ReadOnlyCollection<string> _portals,_doors;
    internal RoomTopologyStateV3(string id,long sequence,IEnumerable<GlobalCellCoord> cells,IEnumerable<string> portals,IEnumerable<string> doors,int revision,RoomRebuildReasonV3 reason)
    {RoomId=id;CreatedSequence=sequence;var list=cells.OrderBy(c=>c.Value.Y).ThenBy(c=>c.Value.X).ToList();_cells=list.AsReadOnly();AnchorCell=list[0].Value;int minX=list.Min(c=>c.Value.X),minY=list.Min(c=>c.Value.Y),maxX=list.Max(c=>c.Value.X),maxY=list.Max(c=>c.Value.Y);Bounds=new(minX,minY,maxX-minX+1,maxY-minY+1);_portals=portals.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToList().AsReadOnly();_doors=doors.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToList().AsReadOnly();TopologyRevision=revision;LastRebuildReason=reason;}
    public string RoomId{get;}public int TopologyRevision{get;}public long CreatedSequence{get;}public bool IsIndoor=>true;public RoomTopologyStatusV3 Status=>RoomTopologyStatusV3.Stable;public int CellCount=>_cells.Count;public Vector2I AnchorCell{get;}public Rect2I Bounds{get;}public IReadOnlyList<GlobalCellCoord> Cells=>_cells;public IReadOnlyCollection<string> PortalIds=>_portals;public IReadOnlyCollection<string> DoorStructureIds=>_doors;public int ChunkCount=>_cells.Select(c=>FloorRegistryV3.ChunkOf(c.Value)).Distinct().Count();public RoomRebuildReasonV3 LastRebuildReason{get;}
}
public sealed record RoomFeatureSnapshotV3(int BedCount,int AssignedBedCount,int StockpileCellCount,int FloorCellCount,int WoodenFloorCellCount,int DoorCount,int CompanyStructureCount,IReadOnlyDictionary<string,int> CompanyContributionCounts);
public sealed record RoomQualitySnapshotV3(float Score,RoomQualityTierV3 Tier,float AreaScore,float FloorScore,float MixedUsePenalty);
public sealed record RoomAffiliationSnapshotV3(RoomAffiliationKindV3 Kind,string DominantCompanyId,IReadOnlyDictionary<string,int> CompanyContributionCounts,long Revision);
public sealed class RoomMetadataStateV3
{
    internal RoomMetadataStateV3(string id,RoomFeatureSnapshotV3 features,RoomRoleV3 role,RoomQualitySnapshotV3 quality,RoomAffiliationSnapshotV3 affiliation,long revision){RoomId=id;Features=features;Role=role;Quality=quality;Affiliation=affiliation;MetadataRevision=revision;}
    public string RoomId{get;}public RoomFeatureSnapshotV3 Features{get;}public RoomRoleV3 Role{get;}public RoomQualitySnapshotV3 Quality{get;}public RoomAffiliationSnapshotV3 Affiliation{get;}public long MetadataRevision{get;}
}
public sealed record RoomPortalStateV3(string PortalId,string DoorStructureId,GlobalCellCoord DoorCell,string? SideARoomId,string? SideBRoomId,bool SideAExterior,bool SideBExterior,long Revision);
public sealed record RoomTopologyRemapV3(IReadOnlyList<string> OldRoomIds,IReadOnlyList<string> NewRoomIds,string PreservedRoomId,IReadOnlyList<string> RemovedRoomIds,IReadOnlyList<string> CreatedRoomIds,RoomRebuildReasonV3 Reason,long Revision);
public sealed record RoomTopologyChangeV3(string ChangeId,GlobalCellCoord ChangedCell,RoomTopologyChangeKindV3 Kind,StructureRoomBoundaryKindV3 OldBoundaryKind,StructureRoomBoundaryKindV3 NewBoundaryKind,string SourceStructureId,long EnqueuedSequence);

public static class RoomRoleClassifierV3{public static RoomRoleV3 Classify(RoomFeatureSnapshotV3 f)=>f.BedCount>0&&f.StockpileCellCount>0?RoomRoleV3.Mixed:f.BedCount>0?RoomRoleV3.Bedroom:f.StockpileCellCount>0?RoomRoleV3.Storage:RoomRoleV3.General;}
public static class RoomQualityEvaluatorV3
{
    public static RoomQualitySnapshotV3 Evaluate(int cells,RoomFeatureSnapshotV3 f){float area=Math.Clamp(cells/25f,0,1)*35f;float floor=cells==0?0:f.FloorCellCount/(float)cells*55f;float mixed=f.BedCount>0&&f.StockpileCellCount>0?10f:0;float score=Math.Clamp(10+area+floor-mixed,0,100);return new(score,score<35?RoomQualityTierV3.Poor:score<75?RoomQualityTierV3.Basic:RoomQualityTierV3.Comfortable,area,floor,mixed);}
}
public sealed class RoomDiagnosticsV3
{
    public int FloodCellsProcessedThisTick{get;internal set;}public int MaxFloodCellsProcessedInTick{get;internal set;}public int TopologyCommitsThisTick{get;internal set;}public int MetadataCommitsThisTick{get;internal set;}public int OutdoorCandidateCount{get;internal set;}public int TooLargeCandidateCount{get;internal set;}public int FloodLimitHitCount{get;internal set;}public int FailedCommitCount{get;internal set;}public int DuplicateCellOwnershipCount{get;internal set;}public int MissingRoomMappingCount{get;internal set;}public int DoorStateTriggeredRebuildCount{get;internal set;}public int FullWorldRoomScanCount{get;internal set;}public int FullStructureRoomScanCount{get;internal set;}public int PerRoomProcessCount=>0;public int PerCellRoomNodeCount=>0;public int RoomOverlayFullMapRebuildCount{get;internal set;}public double RuntimeTickMs{get;internal set;}public double MaxRoomRuntimeTickMs{get;internal set;}
}
public sealed class RoomRegistryV3
{
    private readonly Dictionary<string,RoomTopologyStateV3> _rooms=new(StringComparer.Ordinal);private readonly Dictionary<string,RoomMetadataStateV3> _metadata=new(StringComparer.Ordinal);private readonly Dictionary<Vector2I,string> _roomByCell=new();private readonly Dictionary<Vector2I,HashSet<string>> _roomsByChunk=new();private readonly Dictionary<string,RoomPortalStateV3> _portals=new(StringComparer.Ordinal);private readonly Dictionary<string,string> _portalByDoor=new(StringComparer.Ordinal);private readonly HashSet<Vector2I> _dirtyVisualChunks=new();
    public event Action<RoomTopologyRemapV3>? Remapped;public event Action<string>? MetadataChanged;public int Count=>_rooms.Count;public int CellCount=>_roomByCell.Count;public int PortalCount=>_portals.Count;public int ChunkCount=>_roomsByChunk.Count;public long Revision{get;private set;}public long MetadataRevision{get;private set;}public RoomDiagnosticsV3 Diagnostics{get;}=new();
    public bool TryGetRoomId(GlobalCellCoord cell,out string id)=>_roomByCell.TryGetValue(cell.Value,out id!);public bool TryGetRoom(GlobalCellCoord cell,out RoomTopologyStateV3? room){room=null;return TryGetRoomId(cell,out string id)&&_rooms.TryGetValue(id,out room);}public bool TryGetRoomById(string id,out RoomTopologyStateV3? room)=>_rooms.TryGetValue(id,out room);public bool TryGetMetadata(string id,out RoomMetadataStateV3? metadata)=>_metadata.TryGetValue(id,out metadata);public bool TryGetPortalForDoor(string doorId,out RoomPortalStateV3? portal){portal=null;return _portalByDoor.TryGetValue(doorId,out string? id)&&_portals.TryGetValue(id,out portal);}public IReadOnlyList<RoomPortalStateV3> GetAdjacentRoomsForPortal(string id)=>_portals.TryGetValue(id,out var p)?new[]{p}:Array.Empty<RoomPortalStateV3>();public IReadOnlyList<RoomTopologyStateV3> GetAllRooms()=>_rooms.Values.OrderBy(r=>r.CreatedSequence).ThenBy(r=>r.RoomId,StringComparer.Ordinal).ToList().AsReadOnly();
    public IEnumerable<RoomTopologyStateV3> EnumerateRoomsInChunk(Vector2I chunk){if(!_roomsByChunk.TryGetValue(chunk,out var ids))yield break;foreach(string id in ids)if(_rooms.TryGetValue(id,out var room))yield return room;}public IReadOnlyCollection<Vector2I> DirtyVisualChunks=>_dirtyVisualChunks;
    internal IReadOnlyList<RoomTopologyStateV3> GetOverlapping(HashSet<Vector2I> cells){HashSet<string> ids=new(StringComparer.Ordinal);foreach(Vector2I c in cells)if(_roomByCell.TryGetValue(c,out string? id))ids.Add(id);return ids.Select(id=>_rooms[id]).ToList();}
    internal void Commit(IReadOnlyList<RoomTopologyStateV3> replacements,IReadOnlyCollection<string> removeIds,IReadOnlyList<RoomPortalStateV3> portals,RoomRebuildReasonV3 reason)
    {var old=removeIds.Where(_rooms.ContainsKey).Select(id=>_rooms[id]).ToList();foreach(var room in old)RemoveIndexes(room);foreach(var room in replacements)AddIndexes(room);foreach(var p in _portals.Values.Where(p=>removeIds.Contains(p.SideARoomId??"")||removeIds.Contains(p.SideBRoomId??"")).ToList()){_portals.Remove(p.PortalId);_portalByDoor.Remove(p.DoorStructureId);}foreach(var p in portals){_portals[p.PortalId]=p;_portalByDoor[p.DoorStructureId]=p.PortalId;}Revision++;string preserved=replacements.FirstOrDefault(r=>old.Any(o=>o.RoomId==r.RoomId))?.RoomId??string.Empty;Remapped?.Invoke(new(old.Select(r=>r.RoomId).ToList(),replacements.Select(r=>r.RoomId).ToList(),preserved,old.Select(r=>r.RoomId).Except(replacements.Select(r=>r.RoomId)).ToList(),replacements.Select(r=>r.RoomId).Except(old.Select(r=>r.RoomId)).ToList(),reason,Revision));}
    internal void SetMetadata(RoomMetadataStateV3 value){_metadata[value.RoomId]=value;MetadataRevision++;if(_rooms.TryGetValue(value.RoomId,out var room))foreach(var c in room.Cells)_dirtyVisualChunks.Add(FloorRegistryV3.ChunkOf(c.Value));MetadataChanged?.Invoke(value.RoomId);}
    internal void ReplacePortals(IEnumerable<RoomPortalStateV3> portals){_portals.Clear();_portalByDoor.Clear();foreach(var p in portals){_portals[p.PortalId]=p;_portalByDoor[p.DoorStructureId]=p.PortalId;}Revision++;}
    private void AddIndexes(RoomTopologyStateV3 room){_rooms[room.RoomId]=room;foreach(var c in room.Cells){if(!_roomByCell.TryAdd(c.Value,room.RoomId))Diagnostics.DuplicateCellOwnershipCount++;Vector2I chunk=FloorRegistryV3.ChunkOf(c.Value);if(!_roomsByChunk.TryGetValue(chunk,out var ids)){ids=new(StringComparer.Ordinal);_roomsByChunk.Add(chunk,ids);}ids.Add(room.RoomId);_dirtyVisualChunks.Add(chunk);}}
    private void RemoveIndexes(RoomTopologyStateV3 room){_rooms.Remove(room.RoomId);_metadata.Remove(room.RoomId);foreach(var c in room.Cells){if(_roomByCell.GetValueOrDefault(c.Value)==room.RoomId)_roomByCell.Remove(c.Value);Vector2I chunk=FloorRegistryV3.ChunkOf(c.Value);if(_roomsByChunk.TryGetValue(chunk,out var ids)){ids.Remove(room.RoomId);if(ids.Count==0)_roomsByChunk.Remove(chunk);}_dirtyVisualChunks.Add(chunk);}}
    public void Clear(){_rooms.Clear();_metadata.Clear();_roomByCell.Clear();_roomsByChunk.Clear();_portals.Clear();_portalByDoor.Clear();_dirtyVisualChunks.Clear();Revision++;MetadataRevision++;}
}
public sealed class RoomSessionV3{public RoomSessionV3(long revision){SessionRevision=revision;}public long SessionRevision{get;}public RoomRegistryV3 Registry{get;}=new();}
