using System;
using System.Collections.Generic;
using System.Linq;
using GameplayV3.Construction;
using GameplayV3.Construction.Runtime;
using GameplayV3.Bases;
using GameplayV3.Control;
using GameplayV3.Farming;
using GameplayV3.Farming.Runtime;
using GameplayV3.Mercenary;
using GameplayV3.Needs;
using GameplayV3.Needs.Runtime;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using GameplayV3.Resources.Runtime;
using GameplayV3.Session;
using GameplayV3.Stockpile;
using GameplayV3.Time;
using GameplayV3.Production;
using GameplayV3.Production.Runtime;
using GameplayV3.Work;
using GameplayV3.Equipment;
using Godot;
using WorldV2;

namespace GameplayV3.Jobs.Runtime;

public enum ProductionAssignmentBlockReasonV3 { NoOrder,WaitingMaterials,NoEligibleMercenary,ProductionDisabled,ScheduleBlocked,OutsideActivityRange,NoPath,FacilityReserved,WorkerBusy,InvalidSession,OutputBlocked,Ready,Producing }

public sealed class JobSourceBridgeV3
{
    private readonly record struct SourceDelta(JobSourceKeyV3 Key,Vector2I Target,long Revision,bool Valid);
    private readonly JobManagerV3 _jobs; private readonly ResourceSessionV3 _resources; private readonly StockpileSessionV3 _stockpiles;
    private readonly ConstructionSessionV3 _construction; private readonly FarmSessionV3 _farm;private readonly ProductionSessionV3 _production;private readonly EquipmentRuntimeV3? _equipment; private readonly string _companyId;
    private readonly HashSet<JobSourceKeyV3> _knownStacks=new();
    private readonly HashSet<JobSourceKeyV3> _knownBlueprints=new(); private readonly HashSet<JobSourceKeyV3> _knownDemolitions=new(); private readonly HashSet<JobSourceKeyV3> _knownFarmCells=new();
    private readonly HashSet<JobSourceKeyV3> _knownProduction=new();private readonly HashSet<JobSourceKeyV3> _knownEquipment=new();private long _stackRevision=-1,_blueprintRevision=-1,_demolitionRevision=-1,_farmRevision=-1,_stockpileRevision=-1,_productionRevision=-1,_equipmentRevision=-1,_equipmentStockpileRevision=-1;
    private readonly GatheringJobMaterializerV3 _gathering;
    private readonly Queue<JobSourceKeyV3> _pendingKeys=new();private readonly Dictionary<JobSourceKeyV3,SourceDelta> _pendingByKey=new();
    public int PendingSourceCount=>_pendingByKey.Count;

    public JobSourceBridgeV3(JobManagerV3 jobs,ResourceSessionV3 resources,StockpileSessionV3 stockpiles,ConstructionSessionV3 construction,FarmSessionV3 farm,ProductionSessionV3 production,EquipmentRuntimeV3? equipment,string companyId,Func<IReadOnlyList<Vector2I>> getMercenaryCells,IEnumerable<Vector2I>? activeChunks)
    { _jobs=jobs;_resources=resources;_stockpiles=stockpiles;_construction=construction;_farm=farm;_production=production;_equipment=equipment;_companyId=companyId;_gathering=new(jobs,resources.Nodes,companyId,getMercenaryCells,activeChunks); }

    public int SynchronizeChangedSources()
    {
        int gatheringChanges=_gathering.Synchronize();
        if(_stackRevision!=_resources.GroundStacks.Revision||_stockpileRevision!=_stockpiles.Zones.Revision){_stackRevision=_resources.GroundStacks.Revision;_stockpileRevision=_stockpiles.Zones.Revision;SyncStacks();}
        if(_blueprintRevision!=_construction.Blueprints.Revision){_blueprintRevision=_construction.Blueprints.Revision;SyncBlueprints();}
        if(_demolitionRevision!=_construction.Demolitions.Revision){_demolitionRevision=_construction.Demolitions.Revision;SyncDemolitions();}
        if(_farmRevision!=_farm.Plots.Revision){_farmRevision=_farm.Plots.Revision;SyncFarm();}
        if(_productionRevision!=_production.Revision){_productionRevision=_production.Revision;SyncProduction();}
        if(_equipment!=null&&(_equipmentRevision!=_equipment.Revision||_equipmentStockpileRevision!=_stockpiles.Zones.Revision)){_equipmentRevision=_equipment.Revision;_equipmentStockpileRevision=_stockpiles.Zones.Revision;SyncEquipment();}
        List<FloorSourceChangeV3> floorChanges=new();_construction.Floors.DrainSourceChanges(Math.Min(32,_jobs.Settings.MaxDirtySourcesPerTick),floorChanges);foreach(FloorSourceChangeV3 change in floorChanges){JobTypeV3 type=change.Kind==FloorSourceChangeKindV3.Blueprint?JobTypeV3.Construction:JobTypeV3.Demolition;JobSourceKindV3 kind=change.Kind==FloorSourceChangeKindV3.Blueprint?JobSourceKindV3.FloorBlueprint:JobSourceKindV3.FloorDemolitionMark;var key=new JobSourceKeyV3(change.CompanyId,type,kind,change.SourceId);Enqueue(new(key,change.Cell.Value,change.Revision,change.IsValid));}
        int processed=0,budget=_jobs.Settings.MaxDirtySourcesPerTick;
        while(processed<budget&&_pendingKeys.Count>0){JobSourceKeyV3 key=_pendingKeys.Dequeue();if(!_pendingByKey.Remove(key,out SourceDelta delta))continue;if(delta.Valid)_jobs.TryUpsertSource(delta.Key,delta.Target,delta.Revision,out _,out _,out _);else _jobs.InvalidateSource(delta.Key);processed++;}
        return processed+gatheringChanges;
    }
    public void OnResourceChunkAttached(Vector2I chunk)=>_gathering.OnChunkAttached(chunk);
    public void OnResourceChunkDetached(Vector2I chunk)=>_gathering.OnChunkDetached(chunk);
    private int SyncStacks()
    {
        HashSet<JobSourceKeyV3> current=new();int count=0;bool hasDestination=_stockpiles.Zones.GetZonesByCompany(_companyId).Count>0;
        if(hasDestination)foreach(string id in _resources.GroundStacks.GetAllStackIds())if(_resources.GroundStacks.TryGet(id,out var stack)&&stack!=null&&stack.Amount>0&&!_stockpiles.Zones.IsOwnedStockpileCell(_companyId,stack.Cell))
        {var key=new JobSourceKeyV3(_companyId,JobTypeV3.Hauling,JobSourceKindV3.GroundResourceStack,id);current.Add(key);Enqueue(new(key,stack.Cell.Value,_stackRevision,true));count++;}
        InvalidateMissing(_knownStacks,current);_knownStacks.Clear();_knownStacks.UnionWith(current);return count;
    }
    private int SyncBlueprints()
    {
        HashSet<JobSourceKeyV3> current=new();int count=0;
        foreach(var blueprint in _construction.Blueprints.GetBlueprintsByCompany(_companyId))if(blueprint.Status is not (ConstructionBlueprintStatusV3.Completed or ConstructionBlueprintStatusV3.Cancelled))
        {var key=new JobSourceKeyV3(_companyId,JobTypeV3.Construction,JobSourceKindV3.Blueprint,blueprint.BlueprintId);current.Add(key);Enqueue(new(key,blueprint.AnchorCell.Value,blueprint.Revision,true));count++;}
        InvalidateMissing(_knownBlueprints,current);_knownBlueprints.Clear();_knownBlueprints.UnionWith(current);return count;
    }
    private int SyncDemolitions()
    {
        HashSet<JobSourceKeyV3> current=new();int count=0;
        foreach(var demolition in _construction.Demolitions.GetByCompany(_companyId))if(demolition.Status is not (StructureDemolitionStatusV3.Completed or StructureDemolitionStatusV3.Cancelled)&&_construction.Structures.TryGet(demolition.StructureId,out var structure)&&structure!=null)
        {var key=new JobSourceKeyV3(_companyId,JobTypeV3.Demolition,JobSourceKindV3.Structure,demolition.StructureId);current.Add(key);Enqueue(new(key,structure.AnchorCell.Value,demolition.Revision,true));count++;}
        InvalidateMissing(_knownDemolitions,current);_knownDemolitions.Clear();_knownDemolitions.UnionWith(current);return count;
    }
    private int SyncFarm()
    {
        HashSet<JobSourceKeyV3> current=new();int count=0;
        foreach(var crop in _farm.Plots.GetAllCrops())
        {
            if(crop.CompanyId!=_companyId)continue;JobTypeV3? type=crop.Stage switch{CropStageV3.Empty=>JobTypeV3.Sowing,CropStageV3.Mature=>JobTypeV3.Harvesting,_=>null};if(type==null)continue;
            if(type==JobTypeV3.Sowing&&_resources.GroundStacks.GetStacksAtCell(new(crop.Cell)).Count>0)continue;
            string sourceId=$"{crop.FarmPlotId}:{crop.Cell.X}:{crop.Cell.Y}";var key=new JobSourceKeyV3(_companyId,type.Value,JobSourceKindV3.FarmCell,sourceId);current.Add(key);Enqueue(new(key,crop.Cell,crop.Revision,true));count++;
        }
        InvalidateMissing(_knownFarmCells,current);_knownFarmCells.Clear();_knownFarmCells.UnionWith(current);return count;
    }
    private int SyncProduction(){HashSet<JobSourceKeyV3> current=new();int count=0;foreach(var f in _production.GetFacilities(_companyId))if(_production.CanDispatch(f.FacilityId)){var key=new JobSourceKeyV3(_companyId,JobTypeV3.Production,JobSourceKindV3.ProductionFacility,f.FacilityId);current.Add(key);Enqueue(new(key,f.AnchorCell.Value,f.Revision,true));count++;}InvalidateMissing(_knownProduction,current);_knownProduction.Clear();_knownProduction.UnionWith(current);return count;}
    private int SyncEquipment(){HashSet<JobSourceKeyV3> current=new();int count=0;bool hasDestination=_stockpiles.Zones.GetZonesByCompany(_companyId).Any(x=>x.IsEnabled&&x.AllowsEquipment);if(hasDestination&&_equipment!=null)foreach(string id in _equipment.GetGroundEquipmentIdsByCompany(_companyId))if(_equipment.TryGetInstance(id,out var instance)&&instance?.GroundCell is { } cell&&!_equipment.TryGetReservation(id,out _)){var key=new JobSourceKeyV3(_companyId,JobTypeV3.Hauling,JobSourceKindV3.GroundEquipment,id);current.Add(key);Enqueue(new(key,cell,_equipmentRevision,true));count++;}InvalidateMissing(_knownEquipment,current);_knownEquipment.Clear();_knownEquipment.UnionWith(current);return count;}
    private void InvalidateMissing(HashSet<JobSourceKeyV3> old,HashSet<JobSourceKeyV3> current){foreach(var key in old)if(!current.Contains(key))Enqueue(new(key,Vector2I.Zero,0,false));}
    private void Enqueue(SourceDelta delta){if(!_pendingByKey.ContainsKey(delta.Key))_pendingKeys.Enqueue(delta.Key);_pendingByKey[delta.Key]=delta;}

    public bool IsSourceValid(JobRecordV3 job,bool includeReservation=false)
    {
        switch(job.JobType)
        {
            case JobTypeV3.Gathering:return _resources.Nodes.TryGet(job.SourceId,out var node)&&node!=null&&!node.IsDepleted;
            case JobTypeV3.Hauling:return job.SourceKind==JobSourceKindV3.GroundEquipment?_equipment!=null&&_equipment.TryGetInstance(job.SourceId,out var equipment)&&equipment?.LocationKind==EquipmentLocationKindV3.Ground&&equipment.OwnerCompanyId==job.CompanyId&&!_equipment.TryGetReservation(job.SourceId,out _):_resources.GroundStacks.TryGet(job.SourceId,out var stack)&&stack!=null&&stack.Amount>0&&!_stockpiles.Zones.IsOwnedStockpileCell(job.CompanyId,stack.Cell)&&_resources.AmountReservations.GetAvailableAmount(job.SourceId)>0;
            case JobTypeV3.Construction:return job.SourceKind==JobSourceKindV3.FloorBlueprint?_construction.Floors.TryGetBlueprint(job.SourceId,out var floorBlueprint)&&floorBlueprint!=null&&floorBlueprint.Status is not (ConstructionBlueprintStatusV3.Completed or ConstructionBlueprintStatusV3.Cancelled)&&(includeReservation||!_construction.Reservations.IsReserved(job.SourceId)):_construction.Blueprints.TryGet(job.SourceId,out var blueprint)&&blueprint!=null&&blueprint.Status is not (ConstructionBlueprintStatusV3.Completed or ConstructionBlueprintStatusV3.Cancelled)&&(includeReservation||!_construction.Reservations.IsReserved(job.SourceId));
            case JobTypeV3.Demolition:return job.SourceKind==JobSourceKindV3.FloorDemolitionMark?_construction.Floors.TryGetMark(job.SourceId,out var floorMark)&&floorMark!=null&&floorMark.Status is not (FloorDemolitionStatusV3.Completed or FloorDemolitionStatusV3.Cancelled)&&(includeReservation||!_construction.DemolitionReservations.IsReserved(job.SourceId)):_construction.Structures.Contains(job.SourceId)&&_construction.Demolitions.TryGet(job.SourceId,out var demolition)&&demolition!=null&&demolition.Status is not (StructureDemolitionStatusV3.Completed or StructureDemolitionStatusV3.Cancelled)&&(includeReservation||!_construction.DemolitionReservations.IsReserved(job.SourceId));
            case JobTypeV3.Sowing:
            case JobTypeV3.Harvesting:
                GlobalCellCoord cell=new(job.TargetCell);if(!_farm.Plots.TryGetCrop(cell,out var crop)||crop==null||crop.CompanyId!=job.CompanyId||_farm.Reservations.IsReserved(cell))return false;
                return job.JobType==JobTypeV3.Sowing?crop.Stage==CropStageV3.Empty&&_resources.GroundStacks.GetStacksAtCell(cell).Count==0:crop.Stage==CropStageV3.Mature;
            case JobTypeV3.Production:return _production.CanDispatch(job.SourceId);
            default:return false;
        }
    }
}

public partial class JobManagerRuntimeV3:Node
{
    private JobManagerV3? _jobs;private JobSourceBridgeV3? _sources;private MercenarySessionV3? _mercenaries;private MercenaryControlSessionV3? _control;
    private MercenaryWorkSessionV3? _work;private MercenaryNeedsSessionV3? _needs;private FarmSessionV3? _farm;private IMercenaryNavigationWorldQueryV3? _navigation;private ConstructionWorkCoordinatorV3? _constructionWork;
    private DemolitionWorkCoordinatorV3? _demolitionWork;private FarmingWorkCoordinatorV3? _farmingWork;private ProductionSessionV3? _production;private ProductionWorkCoordinatorV3? _productionWork;private WorldManagerV2? _manager;
    private JobActivityRangePolicyV3? _activityPolicy;private MercenaryBaseAffiliationSessionV3? _baseAffiliations;private FacilityAffiliationSessionV3? _facilityAffiliations;
    private MercenaryScheduleSessionV3? _schedules;
    private float _accumulator;private double _clock;private readonly Queue<string> _deferredWorkers=new();private readonly HashSet<string> _deferredSet=new(StringComparer.Ordinal);private readonly HashSet<string> _loggedDispatchExceptions=new(StringComparer.Ordinal);

    public int SourceSyncCount{get;private set;}public int RuntimeTickCount{get;private set;}public int DispatchExceptionCount{get;private set;}public int DirtyJobSourceCount=>_sources?.PendingSourceCount??0;public JobManagerV3? Jobs=>_jobs;
    public void Initialize(JobManagerV3 jobs,ResourceSessionV3 resources,StockpileSessionV3 stockpiles,ConstructionSessionV3 construction,FarmSessionV3 farm,
        MercenarySessionV3 mercenaries,MercenaryControlSessionV3 control,MercenaryWorkSessionV3 work,MercenaryNeedsSessionV3? needs,
        ConstructionWorkCoordinatorV3 constructionWork,DemolitionWorkCoordinatorV3 demolitionWork,FarmingWorkCoordinatorV3 farmingWork,ProductionSessionV3 production,ProductionWorkCoordinatorV3 productionWork,IMercenaryNavigationWorldQueryV3 navigation,WorldManagerV2 manager,IEnumerable<Vector2I>? activeResourceChunks=null)
    {
        GameplaySessionV3.TryGetEquipmentRuntime(out EquipmentRuntimeV3? equipment);_jobs=jobs;_mercenaries=mercenaries;_sources=new(jobs,resources,stockpiles,construction,farm,production,equipment,manager.LocalCompanyId,GetLocalMercenaryCells,activeResourceChunks);_control=control;_work=work;_needs=needs;_farm=farm;_navigation=navigation;
        _constructionWork=constructionWork;_demolitionWork=demolitionWork;_farmingWork=farmingWork;_production=production;_productionWork=productionWork;_manager=manager;
        GameplaySessionV3.TryGetJobActivityRangePolicy(out _activityPolicy);GameplaySessionV3.TryGetMercenaryBaseAffiliationSession(out _baseAffiliations);GameplaySessionV3.TryGetFacilityAffiliationSession(out _facilityAffiliations);GameplaySessionV3.TryGetMercenarySchedule(out _schedules);if(_baseAffiliations!=null)_baseAffiliations.MercenaryBaseAffiliationChanged+=OnMercenaryBaseChanged;if(_facilityAffiliations!=null){_facilityAffiliations.FacilityAffiliationChanged+=OnFacilityChanged;_facilityAffiliations.BaseActivityRangeChanged+=OnActivityRangeChanged;}if(_schedules!=null)_schedules.CurrentScheduleStateChanged+=OnScheduleStateChanged;
        SourceSyncCount+=_sources.SynchronizeChangedSources();
        foreach(string id in mercenaries.Registry.GetMercenariesByCompany(manager.LocalCompanyId)){jobs.GetOrCreatePriorityProfile(id);jobs.QueueIdleMercenary(id);}
    }
    public void OnResourceChunkAttached(Vector2I chunk)=>_sources?.OnResourceChunkAttached(chunk);
    public void OnResourceChunkDetached(Vector2I chunk)=>_sources?.OnResourceChunkDetached(chunk);
    private IReadOnlyList<Vector2I> GetLocalMercenaryCells(){List<Vector2I> cells=new();if(_mercenaries==null||_manager==null)return cells;foreach(string id in _mercenaries.Registry.GetMercenariesByCompany(_manager.LocalCompanyId))if(_mercenaries.Registry.TryGetState(id,out MercenaryStateV3? state)&&state!=null)cells.Add(state.CurrentCell.Value);return cells;}

    public override void _PhysicsProcess(double delta)
    {
        if(_jobs==null||_jobs.SessionRevision!=GameplaySessionV3.SessionRevision)return;
        _clock+=Math.Max(0,delta);_accumulator+=Math.Max(0,(float)delta);
        int catchup=0;while(_accumulator>=_jobs.Settings.AssignmentIntervalSeconds&&catchup++<4){_accumulator-=_jobs.Settings.AssignmentIntervalSeconds;TickManager();}
    }
    private void TickManager()
    {
        RuntimeTickCount++;int sourceChanges=_sources!.SynchronizeChangedSources();SourceSyncCount+=sourceChanges;if(_activityPolicy!=null)_activityPolicy.Diagnostics.DirtyJobSourceCount=_sources.PendingSourceCount;if(sourceChanges>0)foreach(string id in _mercenaries!.Registry.GetMercenariesByCompany(_manager!.LocalCompanyId))_jobs!.QueueIdleMercenary(id);DrainScheduleDirty();ReconcileAssignments();RequeueDeferred();
        _jobs!.TickAssignments(_clock,GetCell,GetCompany,CanAccept,IsValid,GetRate,Dispatch);
    }
    private Vector2I? GetCell(string id)=>_mercenaries!.Registry.TryGetState(id,out var state)&&state!=null?state.CurrentCell.Value:null;
    private string GetCompany(string id)=>_mercenaries!.Registry.TryGetState(id,out var state)&&state!=null?state.CompanyId:string.Empty;
    private bool CanAccept(string id)
    {
        if(!_mercenaries!.Registry.TryGetState(id,out var state)||state==null||state.ActivityState!=MercenaryActivityStateV3.Idle)return Defer(id);
        if(_schedules!=null&&!_schedules.IsAutomaticJobEligible(id)){_schedules.Diagnostics.BlockedAutoAssignmentCount++;return false;}
        if(_control!.Movements.TryGet(id,out _)||_control.ExternalMovements.TryGetActive(id,out _)||_work!.TryGetAssignment(id,out _)||_constructionWork!.TryGetHudSnapshot(id,out _)||_demolitionWork!.TryGetHudSnapshot(id,out _)||_productionWork!.TryGetHudSnapshot(id,out _)||_farm!.Works.TryGet(id,out _))return Defer(id);
        if(_needs!=null&&(_needs.Hunger.GetHunger(id)>=_needs.HungerConfig.AutoEatThreshold||_needs.Fatigue.GetValue(id)>=_needs.Settings.AutoRestThreshold||_needs.TryGetActiveRest(id,out _)))return Defer(id);
        return true;
    }
    private bool Defer(string id){if(_deferredSet.Add(id))_deferredWorkers.Enqueue(id);return false;}
    private void RequeueDeferred(){int budget=Math.Min(8,_deferredWorkers.Count);while(budget-->0){string id=_deferredWorkers.Dequeue();_deferredSet.Remove(id);if(CanAccept(id))_jobs!.QueueIdleMercenary(id);}}
    private void DrainScheduleDirty(){if(_schedules==null)return;foreach(string id in _schedules.DrainDirty())if(_schedules.IsAutomaticJobEligible(id)&&_mercenaries!.Registry.TryGetState(id,out var state)&&state?.ActivityState==MercenaryActivityStateV3.Idle)_jobs!.QueueIdleMercenary(id);}
    private bool IsValid(JobRecordV3 job,string worker)=>job.CompanyId==_manager!.LocalCompanyId&&GetCompany(worker)==job.CompanyId&&(_activityPolicy?.Evaluate(job,worker,JobCommandSourceV3.Automatic).Allowed??true)&&_sources!.IsSourceValid(job);
    private void OnMercenaryBaseChanged(MercenaryBaseAffiliationChangedV3 value){if(value.CompanyId==_manager?.LocalCompanyId&&_mercenaries?.Registry.TryGetState(value.MercenaryId,out var state)==true&&state?.ActivityState==MercenaryActivityStateV3.Idle&&!_jobs!.TryGetAssignedJob(value.MercenaryId,out _))_jobs.QueueIdleMercenary(value.MercenaryId);}
    private void OnFacilityChanged(FacilityAffiliationChangedV3 value){QueueIdleLocal(value.CompanyId);}
    private void OnActivityRangeChanged(BaseActivityRangeChangedV3 value){QueueIdleLocal(value.CompanyId);}
    private void OnScheduleStateChanged(MercenaryScheduleEventV3 value){if(_jobs==null||_mercenaries==null)return;if(_work!.TryGetAssignment(value.MercenaryId,out _)||_constructionWork!.TryGetHudSnapshot(value.MercenaryId,out _)||_demolitionWork!.TryGetHudSnapshot(value.MercenaryId,out _)||_productionWork!.TryGetHudSnapshot(value.MercenaryId,out _)||_farm!.Works.TryGet(value.MercenaryId,out _)){if(!_schedules!.IsAutomaticJobEligible(value.MercenaryId))_schedules.Diagnostics.DelayedScheduleReleaseCount++;return;}if(_schedules!.IsAutomaticJobEligible(value.MercenaryId))_jobs.QueueIdleMercenary(value.MercenaryId);}
    private void QueueIdleLocal(string company){if(company!=_manager?.LocalCompanyId||_mercenaries==null||_jobs==null)return;foreach(string id in _mercenaries.Registry.GetMercenariesByCompany(company))if(_mercenaries.Registry.TryGetState(id,out var state)&&state?.ActivityState==MercenaryActivityStateV3.Idle&&!_jobs.TryGetAssignedJob(id,out _))_jobs.QueueIdleMercenary(id);}
    private float GetRate(JobRecordV3 job,string worker)
    {
        if(!_mercenaries!.Registry.TryGetProfile(worker,out var profile)||profile==null)return 0;
        MercenaryWorkSkillTypeV3 skill=job.JobType switch{JobTypeV3.Hauling=>MercenaryWorkSkillTypeV3.Hauling,JobTypeV3.Construction or JobTypeV3.Demolition=>MercenaryWorkSkillTypeV3.Construction,JobTypeV3.Gathering=>MercenaryWorkSkillTypeV3.Gathering,JobTypeV3.Sowing or JobTypeV3.Harvesting=>MercenaryWorkSkillTypeV3.Farming,JobTypeV3.Production=>MercenaryWorkSkillTypeV3.Production,_=>MercenaryWorkSkillTypeV3.Hauling};
        return MercenaryDerivedStatsCalculatorV3.GetWorkScore(profile,skill)*MercenaryDerivedStatsCalculatorV3.Calculate(profile).WorkSpeedMultiplier;
    }
    public ProductionAssignmentBlockReasonV3 GetProductionBlockReason(string facilityId)
    {
        if(_jobs==null||_jobs.SessionRevision!=GameplaySessionV3.SessionRevision||_production==null||_mercenaries==null||_manager==null)return ProductionAssignmentBlockReasonV3.InvalidSession;
        if(!_production.TryGetFacility(facilityId,out ProductionFacilitySnapshotV3? facility)||facility==null||facility.Queue.Count==0)return ProductionAssignmentBlockReasonV3.NoOrder;
        ProductionOrderSnapshotV3 order=facility.Queue[0];if(order.State==ProductionOrderStateV3.WaitingMaterials)return ProductionAssignmentBlockReasonV3.WaitingMaterials;if(order.State==ProductionOrderStateV3.OutputBlocked)return ProductionAssignmentBlockReasonV3.OutputBlocked;if(order.State==ProductionOrderStateV3.Producing)return ProductionAssignmentBlockReasonV3.Producing;if(_productionWork?.IsFacilityReserved(facilityId)==true)return ProductionAssignmentBlockReasonV3.FacilityReserved;
        bool any=false,enabled=false,schedule=false,inRange=false,available=false;
        foreach(string id in _mercenaries.Registry.GetMercenariesByCompany(facility.CompanyId))
        {
            any=true;if(!_jobs.GetOrCreatePriorityProfile(id).IsEnabled(JobTypeV3.Production))continue;enabled=true;if(_schedules!=null&&!_schedules.IsAutomaticJobEligible(id))continue;schedule=true;if(_activityPolicy?.Evaluate(facility.CompanyId,JobTypeV3.Production,JobSourceKindV3.ProductionFacility,facilityId,facility.AnchorCell,id,JobCommandSourceV3.Automatic).Allowed==false)continue;inRange=true;if(IsWorkerBusy(id))continue;available=true;if(_productionWork?.HasApproach(id,facilityId)!=true)continue;return ProductionAssignmentBlockReasonV3.Ready;
        }
        if(!any)return ProductionAssignmentBlockReasonV3.NoEligibleMercenary;if(!enabled)return ProductionAssignmentBlockReasonV3.ProductionDisabled;if(!schedule)return ProductionAssignmentBlockReasonV3.ScheduleBlocked;if(!inRange)return ProductionAssignmentBlockReasonV3.OutsideActivityRange;if(!available)return ProductionAssignmentBlockReasonV3.WorkerBusy;return ProductionAssignmentBlockReasonV3.NoPath;
    }
    public string GetProductionBlockReasonText(string facilityId)=>GetProductionBlockReason(facilityId) switch
    {
        ProductionAssignmentBlockReasonV3.NoOrder=>"\uC81C\uC791 \uBA85\uB839\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.",ProductionAssignmentBlockReasonV3.WaitingMaterials=>"\uC7AC\uB8CC\uB97C \uAE30\uB2E4\uB9AC\uB294 \uC911\uC785\uB2C8\uB2E4.",ProductionAssignmentBlockReasonV3.NoEligibleMercenary=>"\uC81C\uC791 \uC791\uC5C5\uC774 \uD5C8\uC6A9\uB41C \uC6A9\uBCD1\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.",ProductionAssignmentBlockReasonV3.ProductionDisabled=>"\uBAA8\uB4E0 \uC6A9\uBCD1\uC758 \uC81C\uC791 \uC791\uC5C5\uC774 \uBE44\uD65C\uC131\uD654\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4.",ProductionAssignmentBlockReasonV3.ScheduleBlocked=>"\uD604\uC7AC \uC2DC\uAC04\uD45C\uC0C1 \uC791\uC5C5 \uC2DC\uAC04\uC774 \uC544\uB2D9\uB2C8\uB2E4.",ProductionAssignmentBlockReasonV3.OutsideActivityRange=>"\uD65C\uB3D9 \uBC94\uC704 \uC548\uC5D0 \uC81C\uC791 \uAC00\uB2A5\uD55C \uC6A9\uBCD1\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.",ProductionAssignmentBlockReasonV3.NoPath=>"\uC2DC\uC124\uAE4C\uC9C0 \uC774\uB3D9\uD560 \uACBD\uB85C\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",ProductionAssignmentBlockReasonV3.FacilityReserved=>"\uB2E4\uB978 \uC6A9\uBCD1\uC774 \uC2DC\uC124\uC744 \uC0AC\uC6A9 \uC911\uC785\uB2C8\uB2E4.",ProductionAssignmentBlockReasonV3.WorkerBusy=>"\uC81C\uC791 \uAC00\uB2A5\uD55C \uC6A9\uBCD1\uB4E4\uC774 \uB2E4\uB978 \uC791\uC5C5\uC744 \uC218\uD589 \uC911\uC785\uB2C8\uB2E4.",ProductionAssignmentBlockReasonV3.OutputBlocked=>"\uACB0\uACFC\uBB3C\uC744 \uB193\uC744 \uACF5\uAC04\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.",ProductionAssignmentBlockReasonV3.Producing=>"\uC81C\uC791 \uC911\uC785\uB2C8\uB2E4.",ProductionAssignmentBlockReasonV3.Ready=>"\uC81C\uC791 \uC900\uBE44\uAC00 \uC644\uB8CC\uB418\uC5C8\uC2B5\uB2C8\uB2E4.",_=>"\uC138\uC158\uC774 \uBCC0\uACBD\uB418\uC5B4 \uBA85\uB839\uC744 \uAC31\uC2E0\uD588\uC2B5\uB2C8\uB2E4."
    };
    private bool IsWorkerBusy(string id)=>!_mercenaries!.Registry.TryGetState(id,out var state)||state==null||state.ActivityState!=MercenaryActivityStateV3.Idle||_control!.Movements.TryGet(id,out _)||_control.ExternalMovements.TryGetActive(id,out _)||_work!.TryGetAssignment(id,out _)||_constructionWork!.TryGetHudSnapshot(id,out _)||_demolitionWork!.TryGetHudSnapshot(id,out _)||_productionWork!.TryGetHudSnapshot(id,out _)||_farm!.Works.TryGet(id,out _);
    private JobDispatchResultV3 Dispatch(JobRecordV3 job,string worker)
    {
        try
        {
            IReadOnlyList<string> selected=new[]{worker};string reason;
            switch(job.JobType)
            {
                case JobTypeV3.Gathering:
                    if(_work!.TryIssueGathering(_manager!.LocalPlayerId,job.CompanyId,selected,job.SourceId,_navigation!,GameplaySessionV3.SessionRevision,out var gather,out reason)&&gather!=null)return JobDispatchResultV3.Success(gather.WorkRequestId);break;
                case JobTypeV3.Hauling:
                    if(job.SourceKind==JobSourceKindV3.GroundEquipment){if(_work!.TryIssueEquipmentHauling(_manager!.LocalPlayerId,job.CompanyId,selected,job.SourceId,_navigation!,GameplaySessionV3.SessionRevision,out var equipmentHaul,out reason)&&equipmentHaul!=null)return JobDispatchResultV3.Success(equipmentHaul.WorkRequestId);}else if(_work!.TryIssueHauling(_manager!.LocalPlayerId,job.CompanyId,selected,job.SourceId,_navigation!,GameplaySessionV3.SessionRevision,out var haul,out reason)&&haul!=null)return JobDispatchResultV3.Success(haul.WorkRequestId);break;
                case JobTypeV3.Construction:
                    if(_constructionWork!.TryIssue(job.SourceId,selected,out reason)&&_constructionWork.TryGetHudSnapshot(worker,out var construction))return JobDispatchResultV3.Success(construction.WorkRequestId);break;
                case JobTypeV3.Demolition:
                    if(_demolitionWork!.TryIssue(job.SourceId,selected,out reason)&&_demolitionWork.TryGetHudSnapshot(worker,out var demolition))return JobDispatchResultV3.Success(demolition.WorkRequestId);break;
                case JobTypeV3.Sowing:
                case JobTypeV3.Harvesting:
                    if(_farmingWork!.TryIssue(new(job.TargetCell),selected,out reason)&&_farm!.Works.TryGet(worker,out var farming)&&farming!=null)return JobDispatchResultV3.Success(farming.WorkRequestId);break;
                case JobTypeV3.Production:
                    if(_productionWork!.TryIssue(job.SourceId,selected,out reason)&&_productionWork.TryGetHudSnapshot(worker,out var production))return JobDispatchResultV3.Success(production.WorkRequestId);break;
                default:reason="UnsupportedJobType";break;
            }
            return JobDispatchResultV3.Failure(string.IsNullOrWhiteSpace(reason)?"DispatchFailed":reason);
        }
        catch(Exception exception)
        {
            DispatchExceptionCount++;
            if(_loggedDispatchExceptions.Count<8&&_loggedDispatchExceptions.Add(job.JobId))GD.PushError($"[JobManagerV3] Dispatch failed job={job.JobId} type={job.JobType}: {exception.GetType().Name}");
            return JobDispatchResultV3.Failure("DispatchException");
        }
    }
    private void ReconcileAssignments()
    {
        foreach(string id in _mercenaries!.Registry.GetMercenariesByCompany(_manager!.LocalCompanyId))
        {
            if(!_jobs!.TryGetAssignedJob(id,out var job)||job==null)continue;
            bool active=job.JobType switch
            {
                JobTypeV3.Gathering or JobTypeV3.Hauling=>_work!.TryGetAssignment(id,out var a)&&a?.WorkRequestId==job.WorkRequestId,
                JobTypeV3.Construction=>_constructionWork!.TryGetHudSnapshot(id,out var c)&&c.WorkRequestId==job.WorkRequestId,
                JobTypeV3.Demolition=>_demolitionWork!.TryGetHudSnapshot(id,out var d)&&d.WorkRequestId==job.WorkRequestId,
                JobTypeV3.Sowing or JobTypeV3.Harvesting=>_farm!.Works.TryGet(id,out var f)&&f?.WorkRequestId==job.WorkRequestId,
                JobTypeV3.Production=>_productionWork!.TryGetHudSnapshot(id,out var p)&&p.WorkRequestId==job.WorkRequestId,
                _=>false
            };
            if(!active)_jobs.MarkAssignmentTerminal(id,_sources!.IsSourceValid(job,true),_clock,"SourceStillRequiresWork");
        }
    }
    public override void _ExitTree(){if(_baseAffiliations!=null)_baseAffiliations.MercenaryBaseAffiliationChanged-=OnMercenaryBaseChanged;if(_facilityAffiliations!=null){_facilityAffiliations.FacilityAffiliationChanged-=OnFacilityChanged;_facilityAffiliations.BaseActivityRangeChanged-=OnActivityRangeChanged;}if(_schedules!=null)_schedules.CurrentScheduleStateChanged-=OnScheduleStateChanged;}
}
