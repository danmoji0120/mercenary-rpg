using System;
using System.Linq;
using GameplayV3.Company;
using GameplayV3.Mercenary;
using GameplayV3.Control;
using GameplayV3.Resources;
using GameplayV3.Work;
using GameplayV3.Stockpile;
using GameplayV3.Construction;
using GameplayV3.Needs;
using GameplayV3.Farming;
using GameplayV3.Jobs;
using GameplayV3.Rooms;
using GameplayV3.Resources.Ecology;
using GameplayV3.Bases;
using GameplayV3.Time;
using GameplayV3.Objectives;
using GameplayV3.Objectives.Runtime;
using GameplayV3.Production;
using GameplayV3.Equipment;
using Godot;

namespace GameplayV3.Session;

public static class GameplaySessionV3
{
    private static CompanySessionV3? _currentCompanySession;
    private static MercenarySessionV3? _currentMercenarySession;
    private static MercenaryControlSessionV3? _currentControlSession;
    private static ResourceSessionV3? _currentResourceSession;
    private static MercenaryWorkSessionV3? _currentWorkSession;
    private static StockpileSessionV3? _currentStockpileSession;
    private static ConstructionSessionV3? _currentConstructionSession;
    private static MercenaryNeedsSessionV3? _currentNeedsSession;
    private static FarmSessionV3? _currentFarmSession;
    private static JobManagerV3? _currentJobManager;
    private static RoomSessionV3? _currentRoomSession;
    private static ResourceEcologySessionV3? _currentResourceEcologySession;
    private static BaseAreaSessionV3? _currentBaseAreaSession;
    private static BaseRoleSessionV3? _currentBaseRoleSession;
    private static FacilityAffiliationSessionV3? _currentFacilityAffiliationSession;
    private static MercenaryBaseAffiliationSessionV3? _currentMercenaryBaseAffiliationSession;
    private static JobActivityRangePolicyV3? _currentJobActivityRangePolicy;
    private static SimulationClockSessionV3? _currentSimulationClock;
    private static MercenaryScheduleSessionV3? _currentMercenarySchedule;
    private static FrontierSurvivalSessionV3? _currentFrontierSurvival;
    private static FrontierSurvivalRuntimeV3? _currentFrontierSurvivalRuntime;
    private static ProductionSessionV3? _currentProductionSession;
    private static WorkToolReservationSessionV3? _currentWorkToolReservations;
    private static EquipmentDefinitionRegistryV3? _currentEquipmentDefinitions;
    private static EquipmentRuntimeV3? _currentEquipmentRuntime;
    private static EquipmentLoadoutRuntimeV3? _currentEquipmentLoadouts;
    private static PersistentWorldStateV3? _persistentWorld;
    private static ActiveRegionSessionV3? _activeRegionSession;
    private static int _nextWorldSeed;
    private static string _nextGeneratorVersion=PersistentWorldStateV3.DefaultGeneratorVersion;
    private static long _sessionRevision;

    public static long SessionRevision => _sessionRevision;
    public static PersistentWorldStateV3? PersistentWorld=>_persistentWorld;
    public static ActiveRegionSessionV3? ActiveRegion=>_activeRegionSession;
    public static event Action? SessionBegan;

    public static void BeginNewSession()
    {
        _activeRegionSession?.Dispose();
        _activeRegionSession=null;
        _persistentWorld=null;
        _currentEquipmentLoadouts?.Dispose();
        _currentEquipmentLoadouts = null;
        _currentEquipmentRuntime?.Dispose();
        _currentEquipmentRuntime = null;
        _currentEquipmentDefinitions = null;
        _currentFrontierSurvivalRuntime?.Dispose();
        _currentWorkToolReservations?.Dispose();
        _currentProductionSession?.Dispose();
        _currentFrontierSurvival?.Dispose();
        _currentFrontierSurvivalRuntime=null;_currentFrontierSurvival=null;
        _currentMercenarySchedule?.Dispose();
        _currentSimulationClock?.Dispose();
        _currentBaseRoleSession?.Dispose();
        _currentResourceEcologySession?.Dispose();
        _currentMercenaryBaseAffiliationSession?.Dispose();
        _currentFacilityAffiliationSession?.Dispose();
        _sessionRevision++;
        _currentCompanySession = new CompanySessionV3();
        _currentMercenarySession = new MercenarySessionV3(_currentCompanySession.CompanyRegistry);
        _currentControlSession = new MercenaryControlSessionV3(_sessionRevision, _currentCompanySession, _currentMercenarySession);
        _currentResourceSession = new ResourceSessionV3();
        _currentStockpileSession = new StockpileSessionV3();
        _currentConstructionSession = new ConstructionSessionV3();
        _currentProductionSession = new ProductionSessionV3(_sessionRevision,_currentConstructionSession,_currentResourceSession,_currentStockpileSession);
        _currentEquipmentDefinitions = StarterEquipmentContentV3.CreateRegistry();
        _currentEquipmentRuntime = new EquipmentRuntimeV3(_sessionRevision, _currentEquipmentDefinitions);
        _currentEquipmentLoadouts = new EquipmentLoadoutRuntimeV3(_sessionRevision,_currentMercenarySession.Registry,_currentEquipmentRuntime);
        _currentWorkToolReservations = new WorkToolReservationSessionV3(_sessionRevision,_currentResourceSession,_currentStockpileSession);
        _currentNeedsSession = new MercenaryNeedsSessionV3(_sessionRevision);
        _currentFarmSession = new FarmSessionV3(_sessionRevision);
        _currentJobManager = new JobManagerV3(_sessionRevision);
        _currentRoomSession = new RoomSessionV3(_sessionRevision);
        _currentBaseAreaSession = new BaseAreaSessionV3(_sessionRevision);
        _currentBaseRoleSession = new BaseRoleSessionV3(_sessionRevision,_currentBaseAreaSession);
        _currentFacilityAffiliationSession = new FacilityAffiliationSessionV3(_sessionRevision,_currentBaseAreaSession);
        _currentMercenaryBaseAffiliationSession = new MercenaryBaseAffiliationSessionV3(_sessionRevision,_currentMercenarySession,_currentNeedsSession,_currentBaseAreaSession,_currentFacilityAffiliationSession);
        _currentJobActivityRangePolicy = new JobActivityRangePolicyV3(_currentBaseAreaSession,_currentFacilityAffiliationSession,_currentMercenaryBaseAffiliationSession);
        _currentSimulationClock = new SimulationClockSessionV3(_sessionRevision);
        _currentMercenarySchedule = new MercenaryScheduleSessionV3(_sessionRevision,_currentSimulationClock,_currentMercenarySession.Registry);
        _currentResourceEcologySession = null;
        _currentWorkSession = new MercenaryWorkSessionV3(_sessionRevision,_currentCompanySession,_currentMercenarySession,_currentResourceSession,_currentStockpileSession,_currentControlSession);
        _currentWorkSession.AttachToolReservations(_currentWorkToolReservations);
        _currentWorkSession.AttachEquipmentLoadouts(_currentEquipmentLoadouts);
        _currentWorkSession.AttachEquipmentRuntime(_currentEquipmentRuntime);
        AttachNeedsPolicy();
        _currentControlSession.AttachWorkSession(_currentWorkSession);
        SessionBegan?.Invoke();
    }

    public static bool EnsureCompanyCoreInitialized(
        out CompanySessionV3 companySession,
        out bool createdNow,
        out string reason)
    {
        EnsureSessionBundle();
        companySession = _currentCompanySession!;
        if(!companySession.TryInitializeLocalSinglePlayer(out createdNow,out reason))return false;
        return EnsurePersistentOwnershipBoundary(out reason);
    }

    public static PersistentWorldStateV3 CreateNewPersistentWorld(int worldSeed=0,string? generatorVersion=null)
    {
        _nextWorldSeed=worldSeed;
        _nextGeneratorVersion=string.IsNullOrWhiteSpace(generatorVersion)?PersistentWorldStateV3.DefaultGeneratorVersion:generatorVersion;
        BeginNewSession();
        if(!EnsureCompanyCoreInitialized(out _,out _,out string reason)||_persistentWorld==null)throw new InvalidOperationException(reason.Length==0?"Persistent world creation failed.":reason);
        return _persistentWorld;
    }

    public static RegionPersistentStateV3 CreateOrGetInitialEstateRegion()
    {
        EnsureSessionBundle();
        if(!EnsureCompanyCoreInitialized(out _,out _,out string reason)||_persistentWorld==null)throw new InvalidOperationException(reason);
        if(!_persistentWorld.TryGetRegion(PersistentWorldStateV3.InitialEstateRegionId,out RegionPersistentStateV3? region)||region==null)throw new InvalidOperationException("Initial estate region is unavailable.");
        return region;
    }

    public static ActiveRegionSessionV3 ActivateInitialRegion()
    {
        RegionPersistentStateV3 region=CreateOrGetInitialEstateRegion();
        if(_activeRegionSession is { IsDisposed:false } active&&ReferenceEquals(active.PersistentState,region))return active;
        if(!RehydrateRegionRuntime(region.RegionId,out string reason)||_activeRegionSession==null)throw new InvalidOperationException(reason);
        return _activeRegionSession;
    }

    public static RegionPersistentStateV3 CreateRegion(string regionId,RegionTypeV3 regionType,string? ownerCompanyId,int terrainSeed)
    {
        EnsureSessionBundle();
        if(!EnsureCompanyCoreInitialized(out _,out _,out string reason)||_persistentWorld==null||_currentEquipmentRuntime==null)
            throw new InvalidOperationException(reason);
        if(_persistentWorld.TryGetRegion(regionId,out RegionPersistentStateV3? existing)&&existing!=null)return existing;
        ResourceSessionV3 resources=new();
        ConstructionSessionV3 construction=new();
        StockpileSessionV3 stockpiles=new();
        FarmSessionV3 farming=new(_sessionRevision);
        ProductionSessionV3 production=new(_sessionRevision,construction,resources,stockpiles);
        RegionPersistentStateV3 region=new(regionId,regionType,ownerCompanyId,terrainSeed,resources,construction,stockpiles,production,farming,_currentEquipmentRuntime);
        if(!_persistentWorld.TryRegisterRegion(region,out reason))throw new InvalidOperationException(reason);
        return region;
    }

    public static bool CommitActiveRegion(out string reason)
    {
        reason=string.Empty;
        if(_activeRegionSession is not { IsDisposed:false } active||_persistentWorld==null||_currentSimulationClock is not { IsDisposed:false } clock)
        {
            reason="NoActiveRegion";
            return false;
        }
        active.PersistentState.Production.PrepareForDeactivate();
        _persistentWorld.WorldClock.Capture(clock.GetSnapshot());
        active.PersistentState.MarkCommitted(clock.ElapsedSimulationSeconds);
        return true;
    }

    public static bool DeactivateActiveRegion(out string reason)
    {
        reason=string.Empty;
        if(_activeRegionSession==null)return true;
        _activeRegionSession.Dispose();
        _activeRegionSession=null;
        _persistentWorld?.ClearActiveRegion();
        return true;
    }

    public static bool ActivateRegion(string regionId,out string reason)
    {
        if(_activeRegionSession is { IsDisposed:false })
        {
            reason="ActiveRegionMustBeDeactivated";
            return false;
        }
        return RehydrateRegionRuntime(regionId,out reason);
    }

    public static bool SwitchActiveRegion(string regionId,out string reason)
    {
        reason=string.Empty;
        if(_persistentWorld==null||!_persistentWorld.TryGetRegion(regionId,out RegionPersistentStateV3? target)||target==null)
        {
            reason="UnknownRegion";
            return false;
        }
        if(_activeRegionSession is { IsDisposed:false })
        {
            if(!CommitActiveRegion(out reason)||!DeactivateActiveRegion(out reason))return false;
        }
        return RehydrateRegionRuntime(regionId,out reason);
    }

    public static bool RehydrateRegionRuntime(string regionId,out string reason)
    {
        reason=string.Empty;
        if(_persistentWorld==null||!_persistentWorld.TryGetRegion(regionId,out RegionPersistentStateV3? region)||region==null)
        {
            reason="UnknownRegion";
            return false;
        }
        if(_activeRegionSession is { IsDisposed:false })
        {
            reason="ActiveRegionMustBeDeactivated";
            return false;
        }
        _sessionRevision++;
        _currentResourceSession=region.Resources;
        _currentConstructionSession=region.Construction;
        _currentStockpileSession=region.Stockpiles;
        _currentProductionSession=region.Production;
        _currentFarmSession=region.Farming;
        if(_currentEquipmentRuntime==null||_currentEquipmentLoadouts==null||_currentCompanySession==null||_currentMercenarySession==null||_currentSimulationClock==null)
        {
            reason="ActiveRegionDependenciesMissing";
            return false;
        }
        _currentEquipmentRuntime.AttachRegionLocationStore(region.EquipmentLocations);
        _currentEquipmentRuntime.RebindSessionRevision(_sessionRevision);
        _currentEquipmentLoadouts.RebindSessionRevision(_sessionRevision);
        _currentProductionSession.RebindSessionRevision(_sessionRevision);
        _currentProductionSession.AttachEquipmentRuntime(_currentEquipmentRuntime,_persistentWorld.WorldSeed,out _);
        _currentFarmSession.RebindSessionRevision(_sessionRevision);
        _currentSimulationClock.RebindSessionRevision(_sessionRevision);
        _currentMercenarySchedule?.RebindSessionRevision(_sessionRevision);
        _currentNeedsSession=new MercenaryNeedsSessionV3(_sessionRevision);
        _currentControlSession=new MercenaryControlSessionV3(_sessionRevision,_currentCompanySession,_currentMercenarySession);
        _currentWorkToolReservations=new WorkToolReservationSessionV3(_sessionRevision,_currentResourceSession,_currentStockpileSession);
        _currentWorkSession=new MercenaryWorkSessionV3(_sessionRevision,_currentCompanySession,_currentMercenarySession,_currentResourceSession,_currentStockpileSession,_currentControlSession);
        _currentWorkSession.AttachToolReservations(_currentWorkToolReservations);
        _currentWorkSession.AttachEquipmentLoadouts(_currentEquipmentLoadouts);
        _currentWorkSession.AttachEquipmentRuntime(_currentEquipmentRuntime);
        AttachNeedsPolicy();
        _currentControlSession.AttachWorkSession(_currentWorkSession);
        _currentJobManager=new JobManagerV3(_sessionRevision);
        if(!_persistentWorld.TrySetActiveRegion(regionId,out reason))return false;
        return CreateActiveRegionRuntime(region,out reason);
    }

    public static RegionPersistentStateV3? GetActiveRegionState()=>_activeRegionSession?.PersistentState;
    public static PlayerCompanyStateV3? GetActiveCompanyState()=>_activeRegionSession?.CompanyState;
    public static bool TryGetPersistentWorldState(out PersistentWorldStateV3? world){world=_persistentWorld;return world!=null;}
    public static bool TryGetActiveRegionSession(out ActiveRegionSessionV3? active){active=_activeRegionSession;return active is { IsDisposed:false };}

    public static void DisposeActiveRegionRuntime()
    {
        if(_activeRegionSession==null)return;
        CommitActiveRegion(out _);
        DeactivateActiveRegion(out _);
    }

    public static bool TryGetCompanySession(out CompanySessionV3? companySession)
    {
        companySession = _currentCompanySession;
        return companySession?.IsInitialized == true;
    }

    public static bool EnsureMercenarySession(
        CompanySessionV3 companySession,
        out MercenarySessionV3 mercenarySession,
        out string reason)
    {
        if (!ReferenceEquals(companySession, _currentCompanySession))
        {
            mercenarySession = null!;
            reason = "MercenarySession must use the current GameplaySession CompanySession.";
            return false;
        }

        _currentMercenarySession ??= new MercenarySessionV3(companySession.CompanyRegistry);
        _currentControlSession ??= new MercenaryControlSessionV3(_sessionRevision, companySession, _currentMercenarySession);
        mercenarySession = _currentMercenarySession;
        reason = string.Empty;
        return true;
    }

    public static bool TryGetMercenarySession(out MercenarySessionV3? mercenarySession)
    {
        mercenarySession = _currentMercenarySession;
        return mercenarySession != null;
    }

    public static bool EnsureControlSession(CompanySessionV3 companySession, MercenarySessionV3 mercenarySession, out MercenaryControlSessionV3 controlSession, out string reason)
    {
        if (!ReferenceEquals(companySession, _currentCompanySession) || !ReferenceEquals(mercenarySession, _currentMercenarySession))
        { controlSession=null!;reason="ControlSession must use the current GameplaySession sessions.";return false; }
        _currentControlSession ??= new MercenaryControlSessionV3(_sessionRevision,companySession,mercenarySession);
        controlSession=_currentControlSession;reason=string.Empty;return true;
    }

    public static bool TryGetControlSession(out MercenaryControlSessionV3? controlSession){controlSession=_currentControlSession;return controlSession!=null;}
    public static bool EnsureResourceAndWorkSessions(CompanySessionV3 company,MercenarySessionV3 mercenary,MercenaryControlSessionV3 control,out ResourceSessionV3 resources,out MercenaryWorkSessionV3 work,out string reason)
    {if(!ReferenceEquals(company,_currentCompanySession)||!ReferenceEquals(mercenary,_currentMercenarySession)||!ReferenceEquals(control,_currentControlSession)){resources=null!;work=null!;reason="Resource/Work sessions must use the current GameplaySession bundle.";return false;}_currentResourceSession??=new();_currentStockpileSession??=new();_currentNeedsSession??=new(_sessionRevision);_currentEquipmentDefinitions??=StarterEquipmentContentV3.CreateRegistry();_currentEquipmentRuntime??=new(_sessionRevision,_currentEquipmentDefinitions);_currentEquipmentLoadouts??=new(_sessionRevision,mercenary.Registry,_currentEquipmentRuntime);_currentWorkToolReservations??=new(_sessionRevision,_currentResourceSession,_currentStockpileSession);_currentWorkSession??=new(_sessionRevision,company,mercenary,_currentResourceSession,_currentStockpileSession,control);_currentWorkSession.AttachToolReservations(_currentWorkToolReservations);_currentWorkSession.AttachEquipmentLoadouts(_currentEquipmentLoadouts);_currentWorkSession.AttachEquipmentRuntime(_currentEquipmentRuntime);AttachNeedsPolicy();control.AttachWorkSession(_currentWorkSession);resources=_currentResourceSession;work=_currentWorkSession;reason=string.Empty;return true;}
    public static bool TryGetResourceSession(out ResourceSessionV3? resources){resources=_currentResourceSession;return resources!=null;}
    public static bool TryGetWorkSession(out MercenaryWorkSessionV3? work){work=_currentWorkSession;return work!=null;}
    public static bool TryGetStockpileSession(out StockpileSessionV3? stockpiles){stockpiles=_currentStockpileSession;return stockpiles!=null;}
    public static bool TryGetConstructionSession(out ConstructionSessionV3? construction){construction=_currentConstructionSession;return construction!=null;}
    public static bool TryGetProductionSession(out ProductionSessionV3? production){production=_currentProductionSession;return production!=null&&!production.IsDisposed&&production.SessionRevision==_sessionRevision;}
    public static bool TryGetEquipmentDefinitions(out EquipmentDefinitionRegistryV3? definitions){definitions=_currentEquipmentDefinitions;return definitions!=null&&definitions.IsSealed;}
    public static bool TryGetEquipmentRuntime(out EquipmentRuntimeV3? equipment){equipment=_currentEquipmentRuntime;return equipment!=null&&!equipment.IsDisposed&&equipment.SessionRevision==_sessionRevision;}
    public static EquipmentRuntimeV3 GetEquipmentRuntime(){EnsureSessionBundle();return _currentEquipmentRuntime!;}
    public static bool IsCurrentEquipmentRuntime(EquipmentRuntimeV3 equipment)=>ReferenceEquals(equipment,_currentEquipmentRuntime)&&!equipment.IsDisposed&&equipment.SessionRevision==_sessionRevision;
    public static bool TryGetEquipmentLoadouts(out EquipmentLoadoutRuntimeV3? loadouts){loadouts=_currentEquipmentLoadouts;return loadouts!=null&&!loadouts.IsDisposed&&loadouts.SessionRevision==_sessionRevision;}
    public static bool IsCurrentEquipmentLoadouts(EquipmentLoadoutRuntimeV3 loadouts)=>ReferenceEquals(loadouts,_currentEquipmentLoadouts)&&!loadouts.IsDisposed&&loadouts.SessionRevision==_sessionRevision;
    public static bool TryGetWorkToolReservations(out WorkToolReservationSessionV3? tools){tools=_currentWorkToolReservations;return tools!=null&&!tools.IsDisposed&&tools.SessionRevision==_sessionRevision;}
    public static bool IsCurrentProductionSession(ProductionSessionV3 production)=>ReferenceEquals(production,_currentProductionSession)&&!production.IsDisposed&&production.SessionRevision==_sessionRevision;
    public static bool TryGetNeedsSession(out MercenaryNeedsSessionV3? needs){needs=_currentNeedsSession;return needs!=null;}
    public static bool TryGetFarmSession(out FarmSessionV3? farm){farm=_currentFarmSession;return farm!=null;}
    public static bool TryGetJobManager(out JobManagerV3? jobs){jobs=_currentJobManager;return jobs!=null&&jobs.SessionRevision==_sessionRevision;}
    public static bool TryGetRoomSession(out RoomSessionV3? rooms){rooms=_currentRoomSession;return rooms!=null&&rooms.SessionRevision==_sessionRevision;}
    public static bool TryGetBaseAreaSession(out BaseAreaSessionV3? bases){bases=_currentBaseAreaSession;return bases!=null&&bases.SessionRevision==_sessionRevision;}
    public static bool TryGetBaseRoleSession(out BaseRoleSessionV3? roles){roles=_currentBaseRoleSession;return roles!=null&&roles.SessionRevision==_sessionRevision&&!roles.IsDisposed;}
    public static bool TryGetFacilityAffiliationSession(out FacilityAffiliationSessionV3? facilities){facilities=_currentFacilityAffiliationSession;return facilities!=null&&facilities.SessionRevision==_sessionRevision;}
    public static bool TryGetMercenaryBaseAffiliationSession(out MercenaryBaseAffiliationSessionV3? affiliations){affiliations=_currentMercenaryBaseAffiliationSession;return affiliations!=null&&affiliations.SessionRevision==_sessionRevision;}
    public static bool TryGetJobActivityRangePolicy(out JobActivityRangePolicyV3? policy){policy=_currentJobActivityRangePolicy;return policy!=null;}
    public static bool TryGetSimulationClock(out SimulationClockSessionV3? clock){clock=_currentSimulationClock;return clock!=null&&!clock.IsDisposed&&clock.SessionRevision==_sessionRevision;}
    public static SimulationClockSessionV3 GetSimulationClock(){EnsureSessionBundle();return _currentSimulationClock!;}
    public static bool TryGetMercenarySchedule(out MercenaryScheduleSessionV3? schedule){schedule=_currentMercenarySchedule;return schedule!=null&&!schedule.IsDisposed&&schedule.SessionRevision==_sessionRevision;}
    public static MercenaryScheduleSessionV3 GetMercenarySchedule(){EnsureSessionBundle();return _currentMercenarySchedule!;}
    public static bool IsCurrentMercenarySchedule(MercenaryScheduleSessionV3 schedule)=>ReferenceEquals(schedule,_currentMercenarySchedule)&&!schedule.IsDisposed&&schedule.SessionRevision==_sessionRevision;
    public static FrontierSurvivalSessionV3 EnsureFrontierSurvivalSession(string companyId)
    {
        EnsureSessionBundle();if(_currentFrontierSurvival!=null&&!_currentFrontierSurvival.IsDisposed&&_currentFrontierSurvival.CompanyId==companyId)return _currentFrontierSurvival;_currentFrontierSurvivalRuntime?.Dispose();_currentFrontierSurvival?.Dispose();_currentFrontierSurvival=new FrontierSurvivalSessionV3(_sessionRevision,companyId,_currentSimulationClock!);_currentFrontierSurvivalRuntime=new FrontierSurvivalRuntimeV3(_currentFrontierSurvival,_currentResourceSession!,_currentMercenarySession!,_currentStockpileSession!,_currentConstructionSession!,_currentFarmSession!,_currentRoomSession!,_currentBaseRoleSession!);return _currentFrontierSurvival;
    }
    public static bool TryGetFrontierSurvivalSession(out FrontierSurvivalSessionV3? objective){objective=_currentFrontierSurvival;return objective!=null&&!objective.IsDisposed&&objective.SessionRevision==_sessionRevision;}
    public static bool IsCurrentFrontierSurvivalSession(FrontierSurvivalSessionV3 objective)=>ReferenceEquals(objective,_currentFrontierSurvival)&&!objective.IsDisposed&&objective.SessionRevision==_sessionRevision;
    public static void FlushFrontierSurvivalDirty()=>_currentFrontierSurvivalRuntime?.Flush();
    public static bool IsCurrentSimulationClock(SimulationClockSessionV3 clock)=>ReferenceEquals(clock,_currentSimulationClock)&&!clock.IsDisposed&&clock.SessionRevision==_sessionRevision;
    public static ResourceEcologySessionV3 EnsureResourceEcologySession(int worldSeed,Rect2I bounds,bool accelerated=false){EnsureSessionBundle();return _currentResourceEcologySession??=new ResourceEcologySessionV3(_currentResourceSession!.Nodes,worldSeed,bounds,accelerated);}
    public static bool TryGetResourceEcologySession(out ResourceEcologySessionV3? ecology){ecology=_currentResourceEcologySession;return ecology!=null;}
    public static bool IsCurrentResourceEcologySession(ResourceEcologySessionV3 ecology)=>ReferenceEquals(ecology,_currentResourceEcologySession);
    public static bool IsCurrentRoomSession(RoomSessionV3 rooms)=>ReferenceEquals(rooms,_currentRoomSession)&&rooms.SessionRevision==_sessionRevision;
    public static bool IsCurrentBaseAreaSession(BaseAreaSessionV3 bases)=>ReferenceEquals(bases,_currentBaseAreaSession)&&bases.SessionRevision==_sessionRevision;
    public static bool IsCurrentBaseRoleSession(BaseRoleSessionV3 roles)=>ReferenceEquals(roles,_currentBaseRoleSession)&&roles.SessionRevision==_sessionRevision&&!roles.IsDisposed;
    public static bool IsCurrentFacilityAffiliationSession(FacilityAffiliationSessionV3 facilities)=>ReferenceEquals(facilities,_currentFacilityAffiliationSession)&&facilities.SessionRevision==_sessionRevision;
    public static bool IsCurrentMercenaryBaseAffiliationSession(MercenaryBaseAffiliationSessionV3 affiliations)=>ReferenceEquals(affiliations,_currentMercenaryBaseAffiliationSession)&&affiliations.SessionRevision==_sessionRevision;
    public static bool IsCurrentWorkSession(MercenaryWorkSessionV3 work)=>ReferenceEquals(work,_currentWorkSession)&&work.SessionRevision==_sessionRevision;
    public static bool IsCurrentControlSession(MercenaryControlSessionV3 session)=>ReferenceEquals(session,_currentControlSession)&&session.SessionRevision==_sessionRevision;
    public static bool IsCurrentConstructionSession(ConstructionSessionV3 session)=>ReferenceEquals(session,_currentConstructionSession);

    private static void EnsureSessionBundle()
    {
        if(_currentCompanySession!=null){_currentSimulationClock??=new SimulationClockSessionV3(_sessionRevision);_currentMercenarySchedule??=new MercenaryScheduleSessionV3(_sessionRevision,_currentSimulationClock,_currentMercenarySession!.Registry);_currentProductionSession??=new ProductionSessionV3(_sessionRevision,_currentConstructionSession!,_currentResourceSession!,_currentStockpileSession!);_currentEquipmentDefinitions??=StarterEquipmentContentV3.CreateRegistry();_currentEquipmentRuntime??=new EquipmentRuntimeV3(_sessionRevision,_currentEquipmentDefinitions);_currentEquipmentLoadouts??=new EquipmentLoadoutRuntimeV3(_sessionRevision,_currentMercenarySession!.Registry,_currentEquipmentRuntime);_currentWorkToolReservations??=new WorkToolReservationSessionV3(_sessionRevision,_currentResourceSession!,_currentStockpileSession!);_currentWorkSession?.AttachToolReservations(_currentWorkToolReservations);_currentWorkSession?.AttachEquipmentLoadouts(_currentEquipmentLoadouts);_currentWorkSession?.AttachEquipmentRuntime(_currentEquipmentRuntime);return;}
        _sessionRevision++;
        _currentCompanySession=new CompanySessionV3();
        _currentMercenarySession=new MercenarySessionV3(_currentCompanySession.CompanyRegistry);
        _currentControlSession=new MercenaryControlSessionV3(_sessionRevision,_currentCompanySession,_currentMercenarySession);
        _currentResourceSession=new ResourceSessionV3();
        _currentStockpileSession=new StockpileSessionV3();
        _currentConstructionSession=new ConstructionSessionV3();
        _currentProductionSession=new ProductionSessionV3(_sessionRevision,_currentConstructionSession,_currentResourceSession,_currentStockpileSession);_currentWorkToolReservations=new WorkToolReservationSessionV3(_sessionRevision,_currentResourceSession,_currentStockpileSession);
        _currentEquipmentDefinitions=StarterEquipmentContentV3.CreateRegistry();
        _currentEquipmentRuntime=new EquipmentRuntimeV3(_sessionRevision,_currentEquipmentDefinitions);
        _currentEquipmentLoadouts=new EquipmentLoadoutRuntimeV3(_sessionRevision,_currentMercenarySession.Registry,_currentEquipmentRuntime);
        _currentNeedsSession=new MercenaryNeedsSessionV3(_sessionRevision);
        _currentFarmSession=new FarmSessionV3(_sessionRevision);
        _currentJobManager=new JobManagerV3(_sessionRevision);
        _currentRoomSession=new RoomSessionV3(_sessionRevision);
        _currentBaseAreaSession=new BaseAreaSessionV3(_sessionRevision);
        _currentBaseRoleSession=new BaseRoleSessionV3(_sessionRevision,_currentBaseAreaSession);
        _currentFacilityAffiliationSession=new FacilityAffiliationSessionV3(_sessionRevision,_currentBaseAreaSession);
        _currentMercenaryBaseAffiliationSession=new MercenaryBaseAffiliationSessionV3(_sessionRevision,_currentMercenarySession,_currentNeedsSession,_currentBaseAreaSession,_currentFacilityAffiliationSession);
        _currentJobActivityRangePolicy=new JobActivityRangePolicyV3(_currentBaseAreaSession,_currentFacilityAffiliationSession,_currentMercenaryBaseAffiliationSession);
        _currentSimulationClock=new SimulationClockSessionV3(_sessionRevision);
        _currentMercenarySchedule=new MercenaryScheduleSessionV3(_sessionRevision,_currentSimulationClock,_currentMercenarySession.Registry);
        _currentWorkSession=new MercenaryWorkSessionV3(_sessionRevision,_currentCompanySession,_currentMercenarySession,_currentResourceSession,_currentStockpileSession,_currentControlSession);
        _currentWorkSession.AttachToolReservations(_currentWorkToolReservations);
        _currentWorkSession.AttachEquipmentLoadouts(_currentEquipmentLoadouts);
        _currentWorkSession.AttachEquipmentRuntime(_currentEquipmentRuntime);
        AttachNeedsPolicy();
        _currentControlSession.AttachWorkSession(_currentWorkSession);
        SessionBegan?.Invoke();
    }

    private static bool EnsurePersistentOwnershipBoundary(out string reason)
    {
        reason=string.Empty;
        if(_currentCompanySession?.LocalPlayer==null||_currentCompanySession.LocalContext.LocalCompanyId is not { } companyId||
           !_currentCompanySession.CompanyRegistry.TryGetCompany(companyId,out CompanyStateV3? company)||company==null||
           _currentMercenarySession==null||_currentEquipmentRuntime==null||_currentEquipmentLoadouts==null||
           _currentResourceSession==null||_currentConstructionSession==null||_currentStockpileSession==null||
           _currentProductionSession==null||_currentFarmSession==null)
        {
            reason="PersistentOwnershipDependenciesMissing";
            return false;
        }
        if(_persistentWorld==null)
        {
            _persistentWorld=new PersistentWorldStateV3("world_"+Guid.NewGuid().ToString("N"),_nextWorldSeed,_nextGeneratorVersion);
            _nextWorldSeed=0;
            _nextGeneratorVersion=PersistentWorldStateV3.DefaultGeneratorVersion;
        }
        if(!_persistentWorld.TryGetCompany(companyId,out PlayerCompanyStateV3? companyState)||companyState==null)
        {
            companyState=new PlayerCompanyStateV3(company,_currentCompanySession.LocalPlayer.PlayerId,_currentMercenarySession,_currentEquipmentRuntime,_currentEquipmentLoadouts);
            if(!_persistentWorld.TryRegisterCompany(companyState,out reason))return false;
        }
        if(!_persistentWorld.TryGetRegion(PersistentWorldStateV3.InitialEstateRegionId,out RegionPersistentStateV3? region)||region==null)
        {
            region=new RegionPersistentStateV3(PersistentWorldStateV3.InitialEstateRegionId,RegionTypeV3.PrivateEstate,companyId,_persistentWorld.WorldSeed,_currentResourceSession,_currentConstructionSession,_currentStockpileSession,_currentProductionSession,_currentFarmSession,_currentEquipmentRuntime);
            if(!_persistentWorld.TryRegisterRegion(region,out reason))return false;
        }
        foreach(string mercenaryId in companyState.MercenaryProfiles.GetAllMercenaryIds())
            if(!companyState.TryGetMercenaryPresence(mercenaryId,out _))companyState.SetMercenaryRegion(mercenaryId,region.RegionId);
        if(_persistentWorld.ActiveRegionId==null&&!_persistentWorld.TrySetActiveRegion(region.RegionId,out reason))return false;
        string activeRegionId=_persistentWorld.ActiveRegionId??region.RegionId;
        if(!_persistentWorld.TryGetRegion(activeRegionId,out RegionPersistentStateV3? activeRegion)||activeRegion==null){reason="ActiveRegionAuthorityMissing";return false;}
        if(_activeRegionSession is not { IsDisposed:false })
        {
            _currentEquipmentRuntime.AttachRegionLocationStore(activeRegion.EquipmentLocations);
            activeRegion.Production.AttachEquipmentRuntime(_currentEquipmentRuntime,_persistentWorld.WorldSeed,out _);
            if(!CreateActiveRegionRuntime(activeRegion,out reason))return false;
        }
        return true;
    }

    private static bool CreateActiveRegionRuntime(RegionPersistentStateV3 region,out string reason)
    {
        reason=string.Empty;
        PlayerCompanyStateV3? companyState=null;
        if(_persistentWorld!=null)
        {
            if(region.OwnerCompanyId is { } ownerCompanyId)_persistentWorld.TryGetCompany(ownerCompanyId,out companyState);
            else companyState=_persistentWorld.PlayerCompanies.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Value).FirstOrDefault();
        }
        if(_persistentWorld==null||companyState==null||
           _currentCompanySession==null||_currentMercenarySession==null||_currentResourceSession==null||_currentStockpileSession==null||
           _currentConstructionSession==null||_currentFarmSession==null||_currentEquipmentRuntime==null||_currentEquipmentLoadouts==null||_currentSimulationClock==null)
        {
            reason="ActiveRegionDependenciesMissing";
            return false;
        }
        _activeRegionSession?.Dispose();
        if(_currentControlSession==null||_currentWorkSession==null||_currentJobManager==null||_currentWorkToolReservations is not { IsDisposed:false })
        {
            _currentControlSession=new MercenaryControlSessionV3(_sessionRevision,_currentCompanySession,_currentMercenarySession);
            _currentWorkToolReservations=new WorkToolReservationSessionV3(_sessionRevision,_currentResourceSession,_currentStockpileSession);
            _currentWorkSession=new MercenaryWorkSessionV3(_sessionRevision,_currentCompanySession,_currentMercenarySession,_currentResourceSession,_currentStockpileSession,_currentControlSession);
            _currentWorkSession.AttachToolReservations(_currentWorkToolReservations);
            _currentWorkSession.AttachEquipmentLoadouts(_currentEquipmentLoadouts);
            _currentWorkSession.AttachEquipmentRuntime(_currentEquipmentRuntime);
            AttachNeedsPolicy();
            _currentControlSession.AttachWorkSession(_currentWorkSession);
            _currentJobManager=new JobManagerV3(_sessionRevision);
        }
        _activeRegionSession=new ActiveRegionSessionV3(region,companyState,_sessionRevision,_currentControlSession,_currentWorkSession,_currentJobManager,_currentWorkToolReservations,_currentSimulationClock,_currentResourceSession,_currentConstructionSession!,_currentStockpileSession,_currentFarmSession!);
        return true;
    }
    private static void AttachNeedsPolicy(){if(_currentWorkSession==null||_currentNeedsSession==null)return;_currentWorkSession.AttachStartPolicy((id,type)=>(_currentNeedsSession.CanStartWork(id,type,out string reason),reason));}
}
