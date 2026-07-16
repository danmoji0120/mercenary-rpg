using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GameplayV3.Resources;
using Godot;
using WorldV2;

namespace GameplayV3.Stockpile;

public static class StockpileZoneIdFactoryV3
{
    private const string Prefix = "stock_";
    public static string Create() => Prefix + Guid.NewGuid().ToString("N");
    public static bool IsValid(string? value) => ResourceIdValidationV3.IsCanonical(value, Prefix);
}

public enum StockpileDesignationModeV3 { None, Add, Remove }

public sealed class StockpileCellReservationV3
{
    internal StockpileCellReservationV3(string zoneId, GlobalCellCoord cell, string requestId, string mercenaryId, ResourceTypeV3 type, long revision, DateTime created)
    { StockpileZoneId=zoneId;Cell=cell;WorkRequestId=requestId;MercenaryId=mercenaryId;ResourceType=type;Revision=revision;CreatedUtc=created; }
    public string StockpileZoneId{get;} public GlobalCellCoord Cell{get;} public string WorkRequestId{get;} public string MercenaryId{get;} public ResourceTypeV3 ResourceType{get;} public long Revision{get;} public DateTime CreatedUtc{get;}
}

public sealed class StockpileCellReservationRegistryV3
{
    private readonly Dictionary<Vector2I,StockpileCellReservationV3> _byCell=new();
    public int Count=>_byCell.Count;
    public bool TryReserve(StockpileCellReservationV3 reservation,out string reason)
    { if(_byCell.TryGetValue(reservation.Cell.Value,out StockpileCellReservationV3? old)){if(old.WorkRequestId==reservation.WorkRequestId&&old.MercenaryId==reservation.MercenaryId){reason=string.Empty;return true;}reason="DestinationReserved";return false;}_byCell.Add(reservation.Cell.Value,reservation);reason=string.Empty;return true; }
    public bool TryGet(GlobalCellCoord cell,out StockpileCellReservationV3? reservation)=>_byCell.TryGetValue(cell.Value,out reservation);
    public bool IsReserved(GlobalCellCoord cell)=>_byCell.ContainsKey(cell.Value);
    public bool IsReservedBy(GlobalCellCoord cell,string requestId)=>_byCell.TryGetValue(cell.Value,out StockpileCellReservationV3? value)&&value.WorkRequestId==requestId;
    public bool TryRelease(GlobalCellCoord cell,string requestId)=>_byCell.TryGetValue(cell.Value,out StockpileCellReservationV3? value)&&value.WorkRequestId==requestId&&_byCell.Remove(cell.Value);
    public int ReleaseByWorkRequest(string requestId){List<Vector2I> cells=new();foreach(var pair in _byCell)if(pair.Value.WorkRequestId==requestId)cells.Add(pair.Key);foreach(Vector2I cell in cells)_byCell.Remove(cell);return cells.Count;}
    public int ReleaseByMercenary(string mercenaryId){List<Vector2I> cells=new();foreach(var pair in _byCell)if(pair.Value.MercenaryId==mercenaryId)cells.Add(pair.Key);foreach(Vector2I cell in cells)_byCell.Remove(cell);return cells.Count;}
    public void Clear()=>_byCell.Clear();
}

public sealed class StockpileZoneStateV3
{
    private readonly HashSet<Vector2I> _cells; private readonly HashSet<ResourceTypeV3> _allowed;
    internal StockpileZoneStateV3(string id,string company,IEnumerable<Vector2I> cells,IEnumerable<ResourceTypeV3> allowed,DateTime created)
    {StockpileZoneId=id;CompanyId=company;_cells=new(cells);_allowed=new(allowed);CreatedUtc=created.Kind==DateTimeKind.Utc?created:created.ToUniversalTime();}
    public string StockpileZoneId{get;} public string CompanyId{get;} public DateTime CreatedUtc{get;} public bool IsEnabled{get;private set;}=true;
    public IReadOnlyCollection<GlobalCellCoord> Cells{get{List<GlobalCellCoord> result=new();foreach(Vector2I cell in _cells)result.Add(new(cell));result.Sort((a,b)=>a.Value.Y!=b.Value.Y?a.Value.Y.CompareTo(b.Value.Y):a.Value.X.CompareTo(b.Value.X));return new ReadOnlyCollection<GlobalCellCoord>(result);}}
    public IReadOnlyCollection<ResourceTypeV3> AllowedResourceTypes{get{List<ResourceTypeV3> result=new(_allowed);result.Sort();return new ReadOnlyCollection<ResourceTypeV3>(result);}}
    public int CellCount=>_cells.Count; public bool Contains(GlobalCellCoord cell)=>_cells.Contains(cell.Value); public bool Allows(ResourceTypeV3 type)=>IsEnabled&&_allowed.Contains(type);
    internal bool Add(Vector2I cell)=>_cells.Add(cell); internal bool Remove(Vector2I cell)=>_cells.Remove(cell); internal void SetEnabled(bool enabled)=>IsEnabled=enabled;
}

public sealed class StockpileZoneRegistryV3
{
    private readonly Dictionary<string,StockpileZoneStateV3> _zones=new(StringComparer.Ordinal); private readonly Dictionary<Vector2I,string> _cellIndex=new(); private readonly StockpileCellReservationRegistryV3 _reservations;
    public StockpileZoneRegistryV3(StockpileCellReservationRegistryV3 reservations){_reservations=reservations;}
    public int Count=>_zones.Count; public int CellCount=>_cellIndex.Count; public long Revision{get;private set;}
    public bool TryCreateZone(string companyId,IReadOnlyCollection<GlobalCellCoord> cells,Rect2I bounds,out StockpileZoneStateV3? zone,out string reason)
    {
        zone=null;if(string.IsNullOrWhiteSpace(companyId)){reason="InvalidCompany";return false;}if(cells==null||cells.Count==0){reason="Stockpile requires at least one cell.";return false;}
        HashSet<Vector2I> unique=new();foreach(GlobalCellCoord cell in cells){if(!bounds.HasPoint(cell.Value)){reason="Stockpile cell is outside world bounds.";return false;}if(!unique.Add(cell.Value)){reason="Duplicate stockpile cell.";return false;}if(_cellIndex.ContainsKey(cell.Value)){reason="Stockpile cell overlaps another zone.";return false;}}
        string id=StockpileZoneIdFactoryV3.Create();zone=new(id,companyId,unique,new[]{ResourceTypeV3.Wood,ResourceTypeV3.Stone,ResourceTypeV3.Ration,ResourceTypeV3.Potato},DateTime.UtcNow);_zones.Add(id,zone);foreach(Vector2I cell in unique)_cellIndex.Add(cell,id);Revision++;reason=string.Empty;return true;
    }
    public bool TryGetZone(string id,out StockpileZoneStateV3? zone)=>_zones.TryGetValue(id,out zone); public bool ContainsZone(string id)=>_zones.ContainsKey(id);
    public bool TryGetZoneAtCell(GlobalCellCoord cell,out StockpileZoneStateV3? zone){zone=null;return _cellIndex.TryGetValue(cell.Value,out string? id)&&_zones.TryGetValue(id,out zone);}
    public bool IsStockpileCell(GlobalCellCoord cell)=>_cellIndex.ContainsKey(cell.Value);
    public bool IsOwnedStockpileCell(string companyId,GlobalCellCoord cell)=>TryGetZoneAtCell(cell,out StockpileZoneStateV3? zone)&&zone?.CompanyId==companyId;
    public bool TryAddCells(string zoneId,string companyId,IReadOnlyCollection<GlobalCellCoord> cells,Rect2I bounds,out string reason)
    {if(!_zones.TryGetValue(zoneId,out StockpileZoneStateV3? zone)){reason="InvalidStockpileZone";return false;}if(zone.CompanyId!=companyId){reason="OwnershipDenied";return false;}HashSet<Vector2I> unique=new();foreach(GlobalCellCoord cell in cells){if(!bounds.HasPoint(cell.Value)||!unique.Add(cell.Value)){reason="Invalid stockpile cells.";return false;}if(_cellIndex.TryGetValue(cell.Value,out string? owner)&&owner!=zoneId){reason="Stockpile cell overlaps another zone.";return false;}}bool changed=false;foreach(Vector2I cell in unique)if(zone.Add(cell)){_cellIndex.Add(cell,zoneId);changed=true;}if(changed)Revision++;reason=string.Empty;return true;}
    public bool TryRemoveCells(string zoneId,string companyId,IReadOnlyCollection<GlobalCellCoord> cells,out bool zoneRemoved,out string reason)
    {zoneRemoved=false;if(!_zones.TryGetValue(zoneId,out StockpileZoneStateV3? zone)){reason="InvalidStockpileZone";return false;}if(zone.CompanyId!=companyId){reason="OwnershipDenied";return false;}foreach(GlobalCellCoord cell in cells)if(zone.Contains(cell)&&_reservations.IsReserved(cell)){reason="DestinationReserved";return false;}bool changed=false;foreach(GlobalCellCoord cell in cells)if(zone.Remove(cell.Value)){_cellIndex.Remove(cell.Value);changed=true;}if(zone.CellCount==0){_zones.Remove(zoneId);zoneRemoved=true;}if(changed)Revision++;reason=string.Empty;return true;}
    public bool TryRemoveZone(string id,string companyId,out string reason){if(!_zones.TryGetValue(id,out StockpileZoneStateV3? zone)){reason="InvalidStockpileZone";return false;}if(zone.CompanyId!=companyId){reason="OwnershipDenied";return false;}foreach(GlobalCellCoord cell in zone.Cells)if(_reservations.IsReserved(cell)){reason="DestinationReserved";return false;}foreach(GlobalCellCoord cell in zone.Cells)_cellIndex.Remove(cell.Value);_zones.Remove(id);Revision++;reason=string.Empty;return true;}
    public IReadOnlyList<StockpileZoneStateV3> GetZonesByCompany(string companyId){List<StockpileZoneStateV3> result=new();foreach(StockpileZoneStateV3 zone in _zones.Values)if(zone.CompanyId==companyId)result.Add(zone);result.Sort((a,b)=>{int c=a.CreatedUtc.CompareTo(b.CreatedUtc);return c!=0?c:string.CompareOrdinal(a.StockpileZoneId,b.StockpileZoneId);});return result.AsReadOnly();}
    public IReadOnlyList<string> GetAllZoneIds(){List<string> ids=new(_zones.Keys);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    public void Clear(){_zones.Clear();_cellIndex.Clear();Revision++;}
}

public sealed class StockpileDiagnosticsV3
{public StockpileDesignationModeV3 DesignationMode{get;internal set;} public int CreatedZoneCount{get;internal set;} public int RemovedCellCount{get;internal set;} public int RejectedCellCount{get;internal set;} public string LastFailureReason{get;internal set;}=string.Empty;}

public sealed class StockpileSessionV3
{
    public StockpileSessionV3(){CellReservations=new();Zones=new(CellReservations);}
    public StockpileCellReservationRegistryV3 CellReservations{get;} public StockpileZoneRegistryV3 Zones{get;} public StockpileDiagnosticsV3 Diagnostics{get;}=new();
    public void Clear(){CellReservations.Clear();Zones.Clear();Diagnostics.DesignationMode=StockpileDesignationModeV3.None;}
}
