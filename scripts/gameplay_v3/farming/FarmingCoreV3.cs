using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameplayV3.Construction;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using Godot;
using WorldV2;

namespace GameplayV3.Farming;

public sealed record CropDefinitionV3(string CropDefinitionId,string DisplayName,ResourceTypeV3 HarvestResourceType,int HarvestYieldAmount,float GrowthDurationSeconds,float BaseSowingDurationSeconds,float BaseHarvestDurationSeconds,bool RequiresSeed,ResourceTypeV3? SeedResourceType,int SeedAmount)
{public bool IsValid=>!string.IsNullOrWhiteSpace(CropDefinitionId)&&!string.IsNullOrWhiteSpace(DisplayName)&&HarvestYieldAmount>0&&float.IsFinite(GrowthDurationSeconds)&&GrowthDurationSeconds>0&&float.IsFinite(BaseSowingDurationSeconds)&&BaseSowingDurationSeconds>0&&float.IsFinite(BaseHarvestDurationSeconds)&&BaseHarvestDurationSeconds>0&&(!RequiresSeed||(SeedResourceType.HasValue&&SeedAmount>0));}
public sealed class CropCatalogV3
{
    public const string PotatoCropId="potato_crop";private readonly Dictionary<string,CropDefinitionV3> _definitions=new(StringComparer.Ordinal){{PotatoCropId,new(PotatoCropId,"감자",ResourceTypeV3.Potato,3,120f,3f,4f,false,null,0)}};
    public int Count=>_definitions.Count;public bool TryGet(string id,out CropDefinitionV3? definition)=>_definitions.TryGetValue(id,out definition);
}
public enum CropStageV3{Empty,Growing,Mature}
public sealed class CropCellStateV3
{
    internal CropCellStateV3(Vector2I cell,string plot,string company,string crop){Cell=cell;FarmPlotId=plot;CompanyId=company;CropDefinitionId=crop;}
    public Vector2I Cell{get;}public string FarmPlotId{get;}public string CompanyId{get;}public string CropDefinitionId{get;}public CropStageV3 Stage{get;private set;}public float GrowthElapsedSeconds{get;private set;}public float GrowthNormalized{get;private set;}public int Revision{get;private set;}
    public bool BeginGrowing(){if(Stage!=CropStageV3.Empty)return false;Stage=CropStageV3.Growing;GrowthElapsedSeconds=0;GrowthNormalized=0;Revision++;return true;}
    public bool AdvanceGrowth(float seconds,float duration){if(Stage!=CropStageV3.Growing||!float.IsFinite(seconds)||seconds<=0||duration<=0)return false;GrowthElapsedSeconds=Math.Min(duration,GrowthElapsedSeconds+seconds);GrowthNormalized=Math.Clamp(GrowthElapsedSeconds/duration,0,1);if(GrowthNormalized>=1){Stage=CropStageV3.Mature;GrowthNormalized=1;}Revision++;return true;}
    public bool ResetAfterHarvest(){if(Stage!=CropStageV3.Mature)return false;Stage=CropStageV3.Empty;GrowthElapsedSeconds=0;GrowthNormalized=0;Revision++;return true;}
    internal void ForceMature(float duration){Stage=CropStageV3.Mature;GrowthElapsedSeconds=duration;GrowthNormalized=1;Revision++;}
}
public sealed class FarmPlotV3
{
    private readonly HashSet<Vector2I> _cells;internal FarmPlotV3(string id,string company,string crop,IEnumerable<Vector2I> cells){FarmPlotId=id;CompanyId=company;CropDefinitionId=crop;_cells=new(cells);CreatedUtc=DateTime.UtcNow;}
    public string FarmPlotId{get;}public string CompanyId{get;}public string CropDefinitionId{get;}public DateTime CreatedUtc{get;}public int Revision{get;internal set;}public IReadOnlyCollection<Vector2I> Cells=>new ReadOnlyCollection<Vector2I>(_cells.OrderBy(x=>x.Y).ThenBy(x=>x.X).ToList());internal bool Remove(Vector2I cell){bool changed=_cells.Remove(cell);if(changed)Revision++;return changed;}public int CellCount=>_cells.Count;
}
public sealed class FarmPlotRegistryV3
{
    private readonly Dictionary<string,FarmPlotV3> _plots=new(StringComparer.Ordinal);private readonly Dictionary<Vector2I,string> _cellToPlot=new();private readonly Dictionary<Vector2I,CropCellStateV3> _crops=new();public int Count=>_plots.Count;public int CellCount=>_cellToPlot.Count;public long Revision{get;private set;}public event Action? Changed;
    public bool TryCreate(string company,string crop,IReadOnlyCollection<GlobalCellCoord> cells,int maxCompanyCells,out FarmPlotV3? plot,out string reason){plot=null;if(string.IsNullOrWhiteSpace(company)||cells.Count==0||cells.Count>256){reason="InvalidFarmCell";return false;}if(GetCellCountByCompany(company)+cells.Count>maxCompanyCells){reason="FarmCellLimitExceeded";return false;}HashSet<Vector2I> unique=new();foreach(var cell in cells)if(!unique.Add(cell.Value)||_cellToPlot.ContainsKey(cell.Value)){reason="OccupiedByFarmPlot";return false;}string id="farm_"+Guid.NewGuid().ToString("N");plot=new(id,company,crop,unique);_plots.Add(id,plot);foreach(var cell in unique){_cellToPlot.Add(cell,id);_crops.Add(cell,new(cell,id,company,crop));}Revision++;Changed?.Invoke();reason=string.Empty;return true;}
    public bool TryGetPlot(string id,out FarmPlotV3? plot)=>_plots.TryGetValue(id,out plot);public bool TryGetPlotAt(GlobalCellCoord cell,out FarmPlotV3? plot){plot=null;return _cellToPlot.TryGetValue(cell.Value,out string? id)&&_plots.TryGetValue(id,out plot);}public bool TryGetCrop(GlobalCellCoord cell,out CropCellStateV3? crop)=>_crops.TryGetValue(cell.Value,out crop);public bool ContainsCell(Vector2I cell)=>_cellToPlot.ContainsKey(cell);public int GetCellCountByCompany(string company)=>_plots.Values.Where(x=>x.CompanyId==company).Sum(x=>x.CellCount);
    public IReadOnlyList<FarmPlotV3> GetPlotsByCompany(string company)=>_plots.Values.Where(x=>x.CompanyId==company).OrderBy(x=>x.CreatedUtc).ThenBy(x=>x.FarmPlotId,StringComparer.Ordinal).ToList().AsReadOnly();public IReadOnlyList<CropCellStateV3> GetAllCrops()=>_crops.Values.OrderBy(x=>x.Cell.Y).ThenBy(x=>x.Cell.X).ToList().AsReadOnly();public IReadOnlyList<CropCellStateV3> GetGrowingCrops()=>_crops.Values.Where(x=>x.Stage==CropStageV3.Growing).OrderBy(x=>x.Cell.Y).ThenBy(x=>x.Cell.X).ToList().AsReadOnly();
    public int GetStageCount(CropStageV3 stage){int count=0;foreach(CropCellStateV3 crop in _crops.Values)if(crop.Stage==stage)count++;return count;}
    public bool TryRemoveEmpty(GlobalCellCoord cell,FarmCellWorkReservationRegistryV3 reservations,out string reason){if(!_cellToPlot.TryGetValue(cell.Value,out string? id)||!_plots.TryGetValue(id,out var plot)||!_crops.TryGetValue(cell.Value,out var crop)){reason="FarmCellMissing";return false;}if(crop.Stage!=CropStageV3.Empty){reason="CropPresent";return false;}if(reservations.IsReserved(cell)){reason="FarmCellBusy";return false;}plot.Remove(cell.Value);_cellToPlot.Remove(cell.Value);_crops.Remove(cell.Value);if(plot.CellCount==0)_plots.Remove(id);Revision++;Changed?.Invoke();reason=string.Empty;return true;}public void NotifyCropChanged(){Revision++;Changed?.Invoke();}public void Clear(){_plots.Clear();_cellToPlot.Clear();_crops.Clear();Revision++;Changed?.Invoke();}
}
public enum FarmingActionV3{Sowing,Harvesting}
public enum FarmingWorkPhaseV3{MovingToFarmCell,Sowing,Harvesting,Completed,Failed,Cancelled}
public sealed record FarmCellWorkReservationV3(GlobalCellCoord TargetCell,string FarmPlotId,string MercenaryId,string WorkRequestId,FarmingActionV3 Action,DateTime CreatedUtc,long Revision);
public sealed class FarmCellWorkReservationRegistryV3
{
    private readonly Dictionary<Vector2I,FarmCellWorkReservationV3> _byCell=new();public int Count=>_byCell.Count;public bool TryReserve(FarmCellWorkReservationV3 value,out string reason){if(_byCell.ContainsKey(value.TargetCell.Value)){reason="FarmCellBusy";return false;}_byCell.Add(value.TargetCell.Value,value);reason=string.Empty;return true;}public bool TryGet(GlobalCellCoord cell,out FarmCellWorkReservationV3? value)=>_byCell.TryGetValue(cell.Value,out value);public bool IsReserved(GlobalCellCoord cell)=>_byCell.ContainsKey(cell.Value);public bool IsReservedBy(GlobalCellCoord cell,string work)=>_byCell.TryGetValue(cell.Value,out var value)&&value.WorkRequestId==work;public int ReleaseByWorkRequest(string work){var keys=_byCell.Where(x=>x.Value.WorkRequestId==work).Select(x=>x.Key).ToList();foreach(var key in keys)_byCell.Remove(key);return keys.Count;}public int ReleaseByMercenary(string id){var keys=_byCell.Where(x=>x.Value.MercenaryId==id).Select(x=>x.Key).ToList();foreach(var key in keys)_byCell.Remove(key);return keys.Count;}public bool ReleaseByCell(GlobalCellCoord cell)=>_byCell.Remove(cell.Value);public void Clear()=>_byCell.Clear();
}
public sealed class FarmingWorkStateV3
{
    public required string WorkRequestId{get;init;}public required string MercenaryId{get;init;}public required string FarmPlotId{get;init;}public required GlobalCellCoord TargetCell{get;init;}public required string CropDefinitionId{get;init;}public required FarmingActionV3 Action{get;init;}public float RequiredDurationSeconds{get;init;}public float ProgressSeconds{get;internal set;}public int CropRevisionAtStart{get;init;}public long Revision{get;init;}public string MovementRequestId{get;internal set;}=string.Empty;public FarmingWorkPhaseV3 Phase{get;internal set;}=FarmingWorkPhaseV3.MovingToFarmCell;public string FailureReason{get;internal set;}=string.Empty;public int FarmingSkill{get;init;}public float BaseExecutionRate{get;init;}
}
public sealed class FarmingDiagnosticsV3{public int GrowthTickCount{get;internal set;}public float LastGrowthTickDuration{get;internal set;}public int CompletedSowingCount{get;internal set;}public int CompletedHarvestCount{get;internal set;}public string LastFailureReason{get;internal set;}=string.Empty;}
public sealed class FarmingWorkRegistryV3
{
    private readonly Dictionary<string,FarmingWorkStateV3> _byMercenary=new(StringComparer.Ordinal);public int Count=>_byMercenary.Count;public bool TryAdd(FarmingWorkStateV3 state,out string reason){if(_byMercenary.ContainsKey(state.MercenaryId)){reason="WorkerBusy";return false;}_byMercenary.Add(state.MercenaryId,state);reason=string.Empty;return true;}public bool TryGet(string mercenary,out FarmingWorkStateV3? state)=>_byMercenary.TryGetValue(mercenary,out state);public IReadOnlyList<FarmingWorkStateV3> GetAll()=>_byMercenary.Values.OrderBy(x=>x.MercenaryId,StringComparer.Ordinal).ToList().AsReadOnly();public bool Remove(string mercenary)=>_byMercenary.Remove(mercenary);public void Clear()=>_byMercenary.Clear();
}
public sealed class FarmSessionV3
{
    public const int MaxFarmCellsPerCompany=4096;public long SessionRevision{get;}public CropCatalogV3 Crops{get;}=new();public FarmCellWorkReservationRegistryV3 Reservations{get;}=new();public FarmPlotRegistryV3 Plots{get;}=new();public FarmingWorkRegistryV3 Works{get;}=new();public FarmingDiagnosticsV3 Diagnostics{get;}=new();public FarmSessionV3(long revision){SessionRevision=revision;}
}
public static class FarmDesignationValidationV3
{
    public static bool CanAdd(GlobalCellCoord cell,string company,FarmSessionV3 farm,ConstructionSessionV3 construction,ResourceSessionV3 resources,StockpileSessionV3 stockpiles,IMercenaryNavigationWorldQueryV3 query,out string reason){if(!query.IsInsideWorld(cell.Value)){reason="OutOfBounds";return false;}if(!query.IsWalkable(cell.Value)){reason="InvalidTerrain";return false;}if(construction.Structures.IsStructureCell(cell)){reason="OccupiedByStructure";return false;}if(construction.Blueprints.IsBlueprintCell(cell)){reason="OccupiedByBlueprint";return false;}if(resources.Nodes.ContainsCell(cell.Value)){reason="OccupiedByResourceNode";return false;}if(resources.GroundStacks.GetStacksAtCell(cell).Count>0){reason="OccupiedByGroundStack";return false;}if(stockpiles.Zones.IsStockpileCell(cell)){reason="OccupiedByStockpile";return false;}if(farm.Plots.ContainsCell(cell.Value)){reason="OccupiedByFarmPlot";return false;}if(farm.Plots.GetCellCountByCompany(company)>=FarmSessionV3.MaxFarmCellsPerCompany){reason="FarmCellLimitExceeded";return false;}reason=string.Empty;return true;}
}
