using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameplayV3.Company;
using GameplayV3.Construction;
using GameplayV3.Control;
using GameplayV3.Equipment;
using GameplayV3.Farming;
using GameplayV3.Jobs;
using GameplayV3.Mercenary;
using GameplayV3.Production;
using GameplayV3.Resources;
using GameplayV3.Session;
using GameplayV3.Stockpile;
using GameplayV3.Time;
using GameplayV3.Work;
using Godot;

namespace GameplayV3.Network;

public sealed class RegionSessionManagerDiagnosticsV3
{
    public int MaximumConcurrentSessionCount{get;internal set;}
    public int DuplicateActiveRegionSessionCount{get;internal set;}
    public int WorldClockDuplicateAdvanceCount{get;internal set;}
    public int DeactivatingOneRegionAffectedOtherRegionCount{get;internal set;}
    public int ActiveRegionSessionLeakCount{get;internal set;}
}

public sealed class ManagedRegionSessionV3
{
    private readonly HashSet<string> _runtimeMercenaryIds;
    internal ManagedRegionSessionV3(ActiveRegionSessionV3 active,ServerRegionCommandRuntimeV3 runtime,IEnumerable<string> runtimeMercenaryIds)
    {Active=active;CommandRuntime=runtime;_runtimeMercenaryIds=new(runtimeMercenaryIds,StringComparer.Ordinal);}
    public ActiveRegionSessionV3 Active{get;}
    internal ServerRegionCommandRuntimeV3 CommandRuntime{get;}
    public int RuntimeMercenaryCount=>_runtimeMercenaryIds.Count;
    public bool ContainsRuntimeMercenary(string mercenaryId)=>_runtimeMercenaryIds.Contains(mercenaryId);
    internal bool RemoveRuntimeMercenary(string mercenaryId){CommandRuntime.UntrackMercenary(mercenaryId);return _runtimeMercenaryIds.Remove(mercenaryId);}
    internal bool AddRuntimeMercenary(string mercenaryId){if(!_runtimeMercenaryIds.Add(mercenaryId))return false;CommandRuntime.TrackMercenary(mercenaryId);return true;}
}

public sealed class RegionSessionManagerV3:IDisposable
{
    private readonly PersistentWorldStateV3 _world;
    private readonly CompanySessionV3 _companies;
    private readonly MercenarySessionV3 _mercenaries;
    private readonly EquipmentRuntimeV3 _equipment;
    private readonly EquipmentLoadoutRuntimeV3 _loadouts;
    private readonly SimulationClockSessionV3 _clock;
    private readonly Dictionary<string,ManagedRegionSessionV3> _active=new(StringComparer.Ordinal);
    private readonly Dictionary<string,long> _generationByRegion=new(StringComparer.Ordinal);
    private long _frameToken;
    private bool _disposed;

    public RegionSessionManagerV3(PersistentWorldStateV3 world,CompanySessionV3 companies,MercenarySessionV3 mercenaries,EquipmentRuntimeV3 equipment,EquipmentLoadoutRuntimeV3 loadouts,SimulationClockSessionV3 clock)
    {
        _world=world??throw new ArgumentNullException(nameof(world));_companies=companies??throw new ArgumentNullException(nameof(companies));
        _mercenaries=mercenaries??throw new ArgumentNullException(nameof(mercenaries));_equipment=equipment??throw new ArgumentNullException(nameof(equipment));
        _loadouts=loadouts??throw new ArgumentNullException(nameof(loadouts));_clock=clock??throw new ArgumentNullException(nameof(clock));
    }

    public RegionSessionManagerDiagnosticsV3 Diagnostics{get;}=new();
    public int ActiveSessionCount=>_active.Count;
    public long ServerTick=>_frameToken;
    public IReadOnlyList<string> ActiveRegionIds=>new ReadOnlyCollection<string>(_active.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToList());
    public IReadOnlyDictionary<string,long> GetSessionGenerationSnapshot()=>new ReadOnlyDictionary<string,long>(new Dictionary<string,long>(_generationByRegion,StringComparer.Ordinal));
    internal void RestoreSessionGenerationFloors(IReadOnlyDictionary<string,long> generations)
    {
        if(_active.Count!=0)throw new InvalidOperationException("Session generation floors must be restored before activation.");
        foreach(var pair in generations){if(pair.Value<0||!_world.Regions.ContainsKey(pair.Key))throw new ArgumentOutOfRangeException(nameof(generations));_generationByRegion[pair.Key]=pair.Value;}
    }

    public bool TryGetActiveRegion(string regionId,out ManagedRegionSessionV3? session)=>_active.TryGetValue(regionId,out session);

    public bool GetOrActivateRegion(string regionId,out ManagedRegionSessionV3? session,out string reason)
    {
        session=null;reason=string.Empty;
        if(_disposed){reason="RegionSessionManagerDisposed";return false;}
        if(_active.TryGetValue(regionId,out session))return true;
        if(!_world.TryGetRegion(regionId,out RegionPersistentStateV3? region)||region==null){reason="UnknownRegion";return false;}
        PlayerCompanyStateV3? company=null;
        if(region.OwnerCompanyId is { } owner)_world.TryGetCompany(owner,out company);
        else company=_world.PlayerCompanies.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Value).FirstOrDefault();
        if(company==null){reason="RegionCompanyUnavailable";return false;}
        long revision=_generationByRegion.GetValueOrDefault(regionId)+1;_generationByRegion[regionId]=revision;
        region.Production.RebindSessionRevision(revision);
        region.Farming.RebindSessionRevision(revision);
        region.Production.AttachEquipmentRuntime(_equipment,_world.WorldSeed,out _);
        MercenaryControlSessionV3 control=new(revision,_companies,_mercenaries);
        WorkToolReservationSessionV3 tools=new(revision,region.Resources,region.Stockpiles);
        MercenaryWorkSessionV3 work=new(revision,_companies,_mercenaries,region.Resources,region.Stockpiles,control);
        work.AttachToolReservations(tools);work.AttachEquipmentLoadouts(_loadouts);work.AttachEquipmentRuntime(_equipment);control.AttachWorkSession(work);
        JobManagerV3 jobs=new(revision);
        ActiveRegionSessionV3 active=new(region,company,revision,control,work,jobs,tools,_clock,region.Resources,region.Construction,region.Stockpiles,region.Farming,false);
        string[] runtimeIds=_world.PlayerCompanies.Values.SelectMany(x=>x.GetMercenaryIdsAtRegion(regionId)).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        session=new(active,new ServerRegionCommandRuntimeV3(active,new(0,0,64,64)),runtimeIds);
        if(!_active.TryAdd(regionId,session)){Diagnostics.DuplicateActiveRegionSessionCount++;session.Active.Dispose();session=null;reason="DuplicateActiveRegionSession";return false;}
        Diagnostics.MaximumConcurrentSessionCount=Math.Max(Diagnostics.MaximumConcurrentSessionCount,_active.Count);
        return true;
    }

    public RegionPersistentStateV3 CreateRegion(string regionId,RegionTypeV3 type,string? ownerCompanyId,int terrainSeed)
    {
        if(_world.TryGetRegion(regionId,out RegionPersistentStateV3? existing)&&existing!=null)return existing;
        ResourceSessionV3 resources=new();ConstructionSessionV3 construction=new();StockpileSessionV3 stockpiles=new();
        FarmSessionV3 farming=new(1);ProductionSessionV3 production=new(1,construction,resources,stockpiles);
        RegionPersistentStateV3 region=new(regionId,type,ownerCompanyId,terrainSeed,resources,construction,stockpiles,production,farming,_equipment);
        if(!_world.TryRegisterRegion(region,out string reason))throw new InvalidOperationException(reason);
        return region;
    }

    public bool CommitRegion(string regionId,out string reason)
    {
        reason=string.Empty;
        if(!_active.TryGetValue(regionId,out ManagedRegionSessionV3? managed)){reason="RegionSessionNotActive";return false;}
        managed.Active.PersistentState.Production.PrepareForDeactivate();
        managed.Active.PersistentState.MarkCommitted(_clock.ElapsedSimulationSeconds);
        _world.WorldClock.Capture(_clock.GetSnapshot());
        return true;
    }

    public bool DeactivateRegion(string regionId,out string reason)
    {
        reason=string.Empty;
        if(!_active.Remove(regionId,out ManagedRegionSessionV3? managed))return true;
        int otherCount=_active.Count;
        CommitDetached(managed);
        _equipment.ClearReservationsForRegion(regionId);
        managed.Active.Dispose();
        if(_active.Count!=otherCount)Diagnostics.DeactivatingOneRegionAffectedOtherRegionCount++;
        return true;
    }

    public void TickActiveRegions(double realDelta)
    {
        if(_disposed)return;
        long beforeDuplicate=_clock.Diagnostics.ClockDuplicateAdvanceFrameCount;
        SimulationClockAdvanceResultV3 advance=_clock.AdvanceFrame(realDelta,++_frameToken);
        if(_clock.Diagnostics.ClockDuplicateAdvanceFrameCount!=beforeDuplicate)Diagnostics.WorldClockDuplicateAdvanceCount++;
        _world.WorldClock.Capture(_clock.GetSnapshot());
        float scaled=(float)advance.ScaledGameplayDeltaSeconds;
        foreach(string regionId in _active.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray())_active[regionId].CommandRuntime.Tick(scaled);
    }

    public void Dispose()
    {
        if(_disposed)return;
        foreach(string regionId in _active.Keys.ToArray())DeactivateRegion(regionId,out _);
        if(_active.Count!=0)Diagnostics.ActiveRegionSessionLeakCount+=_active.Count;
        _disposed=true;
    }

    public bool RemoveRuntimeMercenary(string regionId,string mercenaryId)=>_active.TryGetValue(regionId,out ManagedRegionSessionV3? session)&&session.RemoveRuntimeMercenary(mercenaryId);
    public bool AddRuntimeMercenary(string regionId,string mercenaryId)=>_active.TryGetValue(regionId,out ManagedRegionSessionV3? session)&&session.AddRuntimeMercenary(mercenaryId);

    private void CommitDetached(ManagedRegionSessionV3 managed)
    {
        managed.Active.PersistentState.Production.PrepareForDeactivate();
        managed.Active.PersistentState.MarkCommitted(_clock.ElapsedSimulationSeconds);
        _world.WorldClock.Capture(_clock.GetSnapshot());
    }
}
