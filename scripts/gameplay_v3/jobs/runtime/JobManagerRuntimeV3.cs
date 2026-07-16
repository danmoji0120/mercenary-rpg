using System;
using System.Collections.Generic;
using System.Linq;
using GameplayV3.Construction;
using GameplayV3.Construction.Runtime;
using GameplayV3.Control;
using GameplayV3.Farming;
using GameplayV3.Farming.Runtime;
using GameplayV3.Mercenary;
using GameplayV3.Needs;
using GameplayV3.Needs.Runtime;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using GameplayV3.Session;
using GameplayV3.Stockpile;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Jobs.Runtime;

public sealed class JobSourceBridgeV3
{
    private readonly record struct SourceDelta(JobSourceKeyV3 Key,Vector2I Target,long Revision,bool Valid);
    private readonly JobManagerV3 _jobs; private readonly ResourceSessionV3 _resources; private readonly StockpileSessionV3 _stockpiles;
    private readonly ConstructionSessionV3 _construction; private readonly FarmSessionV3 _farm; private readonly string _companyId;
    private readonly HashSet<JobSourceKeyV3> _knownNodes=new(); private readonly HashSet<JobSourceKeyV3> _knownStacks=new();
    private readonly HashSet<JobSourceKeyV3> _knownBlueprints=new(); private readonly HashSet<JobSourceKeyV3> _knownDemolitions=new(); private readonly HashSet<JobSourceKeyV3> _knownFarmCells=new();
    private long _nodeRevision=-1,_stackRevision=-1,_blueprintRevision=-1,_demolitionRevision=-1,_farmRevision=-1,_stockpileRevision=-1;
    private readonly Queue<JobSourceKeyV3> _pendingKeys=new();private readonly Dictionary<JobSourceKeyV3,SourceDelta> _pendingByKey=new();

    public JobSourceBridgeV3(JobManagerV3 jobs,ResourceSessionV3 resources,StockpileSessionV3 stockpiles,ConstructionSessionV3 construction,FarmSessionV3 farm,string companyId)
    { _jobs=jobs;_resources=resources;_stockpiles=stockpiles;_construction=construction;_farm=farm;_companyId=companyId; }

    public int SynchronizeChangedSources()
    {
        if(_nodeRevision!=_resources.Nodes.Revision){_nodeRevision=_resources.Nodes.Revision;SyncNodes();}
        if(_stackRevision!=_resources.GroundStacks.Revision||_stockpileRevision!=_stockpiles.Zones.Revision){_stackRevision=_resources.GroundStacks.Revision;_stockpileRevision=_stockpiles.Zones.Revision;SyncStacks();}
        if(_blueprintRevision!=_construction.Blueprints.Revision){_blueprintRevision=_construction.Blueprints.Revision;SyncBlueprints();}
        if(_demolitionRevision!=_construction.Demolitions.Revision){_demolitionRevision=_construction.Demolitions.Revision;SyncDemolitions();}
        if(_farmRevision!=_farm.Plots.Revision){_farmRevision=_farm.Plots.Revision;SyncFarm();}
        int processed=0,budget=_jobs.Settings.MaxDirtySourcesPerTick;
        while(processed<budget&&_pendingKeys.Count>0){JobSourceKeyV3 key=_pendingKeys.Dequeue();if(!_pendingByKey.Remove(key,out SourceDelta delta))continue;if(delta.Valid)_jobs.TryUpsertSource(delta.Key,delta.Target,delta.Revision,out _,out _,out _);else _jobs.InvalidateSource(delta.Key);processed++;}
        return processed;
    }

    private int SyncNodes()
    {
        HashSet<JobSourceKeyV3> current=new();int count=0;
        foreach(string id in _resources.Nodes.GetAllNodeIds())if(_resources.Nodes.TryGet(id,out var node)&&node!=null&&!node.IsDepleted)
        {var key=new JobSourceKeyV3(_companyId,JobTypeV3.Gathering,JobSourceKindV3.ResourceNode,id);current.Add(key);Enqueue(new(key,node.Cell.Value,_nodeRevision,true));count++;}
        InvalidateMissing(_knownNodes,current);_knownNodes.Clear();_knownNodes.UnionWith(current);return count;
    }
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
    private void InvalidateMissing(HashSet<JobSourceKeyV3> old,HashSet<JobSourceKeyV3> current){foreach(var key in old)if(!current.Contains(key))Enqueue(new(key,Vector2I.Zero,0,false));}
    private void Enqueue(SourceDelta delta){if(!_pendingByKey.ContainsKey(delta.Key))_pendingKeys.Enqueue(delta.Key);_pendingByKey[delta.Key]=delta;}

    public bool IsSourceValid(JobRecordV3 job,bool includeReservation=false)
    {
        switch(job.JobType)
        {
            case JobTypeV3.Gathering:return _resources.Nodes.TryGet(job.SourceId,out var node)&&node!=null&&!node.IsDepleted;
            case JobTypeV3.Hauling:return _resources.GroundStacks.TryGet(job.SourceId,out var stack)&&stack!=null&&stack.Amount>0&&!_stockpiles.Zones.IsOwnedStockpileCell(job.CompanyId,stack.Cell)&&_resources.AmountReservations.GetAvailableAmount(job.SourceId)>0;
            case JobTypeV3.Construction:return _construction.Blueprints.TryGet(job.SourceId,out var blueprint)&&blueprint!=null&&blueprint.Status is not (ConstructionBlueprintStatusV3.Completed or ConstructionBlueprintStatusV3.Cancelled)&&(includeReservation||!_construction.Reservations.IsReserved(job.SourceId));
            case JobTypeV3.Demolition:return _construction.Structures.Contains(job.SourceId)&&_construction.Demolitions.TryGet(job.SourceId,out var demolition)&&demolition!=null&&demolition.Status is not (StructureDemolitionStatusV3.Completed or StructureDemolitionStatusV3.Cancelled)&&(includeReservation||!_construction.DemolitionReservations.IsReserved(job.SourceId));
            case JobTypeV3.Sowing:
            case JobTypeV3.Harvesting:
                GlobalCellCoord cell=new(job.TargetCell);if(!_farm.Plots.TryGetCrop(cell,out var crop)||crop==null||crop.CompanyId!=job.CompanyId||_farm.Reservations.IsReserved(cell))return false;
                return job.JobType==JobTypeV3.Sowing?crop.Stage==CropStageV3.Empty&&_resources.GroundStacks.GetStacksAtCell(cell).Count==0:crop.Stage==CropStageV3.Mature;
            default:return false;
        }
    }
}

public partial class JobManagerRuntimeV3:Node
{
    private JobManagerV3? _jobs;private JobSourceBridgeV3? _sources;private MercenarySessionV3? _mercenaries;private MercenaryControlSessionV3? _control;
    private MercenaryWorkSessionV3? _work;private MercenaryNeedsSessionV3? _needs;private FarmSessionV3? _farm;private IMercenaryNavigationWorldQueryV3? _navigation;private ConstructionWorkCoordinatorV3? _constructionWork;
    private DemolitionWorkCoordinatorV3? _demolitionWork;private FarmingWorkCoordinatorV3? _farmingWork;private WorldManagerV2? _manager;
    private float _accumulator;private double _clock;private readonly Queue<string> _deferredWorkers=new();private readonly HashSet<string> _deferredSet=new(StringComparer.Ordinal);

    public int SourceSyncCount{get;private set;}public int RuntimeTickCount{get;private set;}
    public void Initialize(JobManagerV3 jobs,ResourceSessionV3 resources,StockpileSessionV3 stockpiles,ConstructionSessionV3 construction,FarmSessionV3 farm,
        MercenarySessionV3 mercenaries,MercenaryControlSessionV3 control,MercenaryWorkSessionV3 work,MercenaryNeedsSessionV3? needs,
        ConstructionWorkCoordinatorV3 constructionWork,DemolitionWorkCoordinatorV3 demolitionWork,FarmingWorkCoordinatorV3 farmingWork,IMercenaryNavigationWorldQueryV3 navigation,WorldManagerV2 manager)
    {
        _jobs=jobs;_sources=new(jobs,resources,stockpiles,construction,farm,manager.LocalCompanyId);_mercenaries=mercenaries;_control=control;_work=work;_needs=needs;_farm=farm;_navigation=navigation;
        _constructionWork=constructionWork;_demolitionWork=demolitionWork;_farmingWork=farmingWork;_manager=manager;
        SourceSyncCount+=_sources.SynchronizeChangedSources();
        foreach(string id in mercenaries.Registry.GetMercenariesByCompany(manager.LocalCompanyId)){jobs.GetOrCreatePriorityProfile(id);jobs.QueueIdleMercenary(id);}
    }

    public override void _PhysicsProcess(double delta)
    {
        if(_jobs==null||_jobs.SessionRevision!=GameplaySessionV3.SessionRevision)return;
        _clock+=Math.Max(0,delta);_accumulator+=Math.Max(0,(float)delta);
        int catchup=0;while(_accumulator>=_jobs.Settings.AssignmentIntervalSeconds&&catchup++<4){_accumulator-=_jobs.Settings.AssignmentIntervalSeconds;TickManager();}
    }
    private void TickManager()
    {
        RuntimeTickCount++;int sourceChanges=_sources!.SynchronizeChangedSources();SourceSyncCount+=sourceChanges;if(sourceChanges>0)foreach(string id in _mercenaries!.Registry.GetMercenariesByCompany(_manager!.LocalCompanyId))_jobs!.QueueIdleMercenary(id);ReconcileAssignments();RequeueDeferred();
        _jobs!.TickAssignments(_clock,GetCell,GetCompany,CanAccept,IsValid,GetRate,Dispatch);
    }
    private Vector2I? GetCell(string id)=>_mercenaries!.Registry.TryGetState(id,out var state)&&state!=null?state.CurrentCell.Value:null;
    private string GetCompany(string id)=>_mercenaries!.Registry.TryGetState(id,out var state)&&state!=null?state.CompanyId:string.Empty;
    private bool CanAccept(string id)
    {
        if(!_mercenaries!.Registry.TryGetState(id,out var state)||state==null||state.ActivityState!=MercenaryActivityStateV3.Idle)return Defer(id);
        if(_control!.Movements.TryGet(id,out _)||_control.ExternalMovements.TryGetActive(id,out _)||_work!.TryGetAssignment(id,out _)||_constructionWork!.TryGetHudSnapshot(id,out _)||_demolitionWork!.TryGetHudSnapshot(id,out _)||_farm!.Works.TryGet(id,out _))return Defer(id);
        if(_needs!=null&&(_needs.Hunger.GetHunger(id)>=_needs.HungerConfig.AutoEatThreshold||_needs.Fatigue.GetValue(id)>=_needs.Settings.AutoRestThreshold||_needs.TryGetActiveRest(id,out _)))return Defer(id);
        return true;
    }
    private bool Defer(string id){if(_deferredSet.Add(id))_deferredWorkers.Enqueue(id);return false;}
    private void RequeueDeferred(){int budget=Math.Min(8,_deferredWorkers.Count);while(budget-->0){string id=_deferredWorkers.Dequeue();_deferredSet.Remove(id);if(CanAccept(id))_jobs!.QueueIdleMercenary(id);}}
    private bool IsValid(JobRecordV3 job,string worker)=>job.CompanyId==_manager!.LocalCompanyId&&GetCompany(worker)==job.CompanyId&&_sources!.IsSourceValid(job);
    private float GetRate(JobRecordV3 job,string worker)
    {
        if(!_mercenaries!.Registry.TryGetProfile(worker,out var profile)||profile==null)return 0;
        MercenaryWorkSkillTypeV3 skill=job.JobType switch{JobTypeV3.Hauling=>MercenaryWorkSkillTypeV3.Hauling,JobTypeV3.Construction or JobTypeV3.Demolition=>MercenaryWorkSkillTypeV3.Construction,JobTypeV3.Gathering=>MercenaryWorkSkillTypeV3.Gathering,JobTypeV3.Sowing or JobTypeV3.Harvesting=>MercenaryWorkSkillTypeV3.Farming,_=>MercenaryWorkSkillTypeV3.Hauling};
        return MercenaryDerivedStatsCalculatorV3.GetWorkScore(profile,skill)*MercenaryDerivedStatsCalculatorV3.Calculate(profile).WorkSpeedMultiplier;
    }
    private JobDispatchResultV3 Dispatch(JobRecordV3 job,string worker)
    {
        IReadOnlyList<string> selected=new[]{worker};string reason;
        switch(job.JobType)
        {
            case JobTypeV3.Gathering:
                if(_work!.TryIssueGathering(_manager!.LocalPlayerId,job.CompanyId,selected,job.SourceId,_navigation!,GameplaySessionV3.SessionRevision,out var gather,out reason)&&gather!=null)return JobDispatchResultV3.Success(gather.WorkRequestId);break;
            case JobTypeV3.Hauling:
                if(_work!.TryIssueHauling(_manager!.LocalPlayerId,job.CompanyId,selected,job.SourceId,_navigation!,GameplaySessionV3.SessionRevision,out var haul,out reason)&&haul!=null)return JobDispatchResultV3.Success(haul.WorkRequestId);break;
            case JobTypeV3.Construction:
                if(_constructionWork!.TryIssue(job.SourceId,selected,out reason)&&_constructionWork.TryGetHudSnapshot(worker,out var construction))return JobDispatchResultV3.Success(construction.WorkRequestId);break;
            case JobTypeV3.Demolition:
                if(_demolitionWork!.TryIssue(job.SourceId,selected,out reason)&&_demolitionWork.TryGetHudSnapshot(worker,out var demolition))return JobDispatchResultV3.Success(demolition.WorkRequestId);break;
            case JobTypeV3.Sowing:
            case JobTypeV3.Harvesting:
                if(_farmingWork!.TryIssue(new(job.TargetCell),selected,out reason)&&_farm!.Works.TryGet(worker,out var farming)&&farming!=null)return JobDispatchResultV3.Success(farming.WorkRequestId);break;
            default:reason="UnsupportedJobType";break;
        }
        return JobDispatchResultV3.Failure(string.IsNullOrWhiteSpace(reason)?"DispatchFailed":reason);
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
                _=>false
            };
            if(!active)_jobs.MarkAssignmentTerminal(id,_sources!.IsSourceValid(job,true),_clock,"SourceStillRequiresWork");
        }
    }
}
