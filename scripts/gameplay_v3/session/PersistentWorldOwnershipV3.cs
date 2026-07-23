using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GameplayV3.Company;
using GameplayV3.Construction;
using GameplayV3.Control;
using GameplayV3.Equipment;
using GameplayV3.Farming;
using GameplayV3.Jobs;
using GameplayV3.Mercenary;
using GameplayV3.Production;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using GameplayV3.Time;
using GameplayV3.Work;

namespace GameplayV3.Session;

public enum RegionTypeV3
{
    PrivateEstate = 1,
    SharedNeutral = 2
}

public sealed class PersistentWorldClockStateV3
{
    public long SimulationTick { get; private set; }
    public double ElapsedSimulationSeconds { get; private set; }
    public int TimeScale { get; private set; }=1;
    public long Revision { get; private set; }

    public void Capture(SimulationClockSnapshotV3 snapshot)
    {
        SimulationTick=snapshot.SimulationTick;
        ElapsedSimulationSeconds=snapshot.ElapsedSimulationSeconds;
        TimeScale=snapshot.TimeScale;
        Revision++;
    }

    internal void Restore(long simulationTick,double elapsedSimulationSeconds,int timeScale,long revision)
    {
        if(simulationTick<0||!double.IsFinite(elapsedSimulationSeconds)||elapsedSimulationSeconds<0||timeScale<0||revision<0)
            throw new ArgumentOutOfRangeException(nameof(revision));
        SimulationTick=simulationTick;ElapsedSimulationSeconds=elapsedSimulationSeconds;TimeScale=timeScale;Revision=revision;
    }
}

public sealed class PlayerCompanyStateV3
{
    private readonly HashSet<string> _ownedRegionIds=new(StringComparer.Ordinal);
    private readonly Dictionary<string,MercenaryPresenceStateV3> _mercenaryPresence=new(StringComparer.Ordinal);

    public PlayerCompanyStateV3(
        CompanyStateV3 company,
        string playerAccountId,
        MercenarySessionV3 mercenaries,
        EquipmentRuntimeV3 equipment,
        EquipmentLoadoutRuntimeV3 loadouts)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(mercenaries);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(loadouts);
        if(company.OwnerPlayerId!=playerAccountId)throw new ArgumentException("Player account does not own the company.",nameof(playerAccountId));
        Company=company;
        PlayerAccountId=playerAccountId;
        Mercenaries=mercenaries;
        Equipment=equipment;
        EquipmentLoadouts=loadouts;
    }

    public string CompanyId=>Company.CompanyId;
    public string PlayerAccountId{get;}
    public CompanyStateV3 Company{get;}
    internal MercenarySessionV3 Mercenaries{get;}
    public MercenaryRegistryV3 MercenaryProfiles=>Mercenaries.Registry;
    public EquipmentRuntimeV3 Equipment{get;}
    public EquipmentLoadoutRuntimeV3 EquipmentLoadouts{get;}
    public IReadOnlyCollection<string> OwnedRegionIds=>new ReadOnlyCollection<string>(new List<string>(_ownedRegionIds));
    public IReadOnlyList<MercenaryPresenceStateV3> GetMercenaryPresences()
    {
        List<MercenaryPresenceStateV3> values=new(_mercenaryPresence.Values);
        values.Sort((left,right)=>string.CompareOrdinal(left.MercenaryId,right.MercenaryId));
        return new ReadOnlyCollection<MercenaryPresenceStateV3>(values);
    }
    internal bool AddOwnedRegion(string regionId)=>_ownedRegionIds.Add(regionId);
    public bool SetMercenaryRegion(string mercenaryId,string regionId)
    {
        if(string.IsNullOrWhiteSpace(mercenaryId)||string.IsNullOrWhiteSpace(regionId)||!MercenaryProfiles.TryGetProfile(mercenaryId,out _))return false;
        _mercenaryPresence[mercenaryId]=MercenaryPresenceStateV3.CreateAtRegion(mercenaryId,regionId);
        return true;
    }
    public bool SetMercenaryTraveling(string mercenaryId,string travelingGroupId)
    {
        if(string.IsNullOrWhiteSpace(mercenaryId)||string.IsNullOrWhiteSpace(travelingGroupId)||!MercenaryProfiles.TryGetProfile(mercenaryId,out _))return false;
        _mercenaryPresence[mercenaryId]=MercenaryPresenceStateV3.CreateTraveling(mercenaryId,travelingGroupId);
        return true;
    }
    public bool TryGetMercenaryPresence(string mercenaryId,out MercenaryPresenceStateV3? presence)=>_mercenaryPresence.TryGetValue(mercenaryId,out presence);
    public IReadOnlyList<string> GetMercenaryIdsAtRegion(string regionId)
    {
        List<string> ids=new();
        foreach(MercenaryPresenceStateV3 presence in _mercenaryPresence.Values)
            if(presence.AtRegion&&presence.CurrentRegionId==regionId)ids.Add(presence.MercenaryId);
        ids.Sort(StringComparer.Ordinal);
        return new ReadOnlyCollection<string>(ids);
    }
}

public enum MercenaryWorldPresenceV3{AtRegion=1,Traveling=2}
public sealed record MercenaryPresenceStateV3(string MercenaryId,MercenaryWorldPresenceV3 PresenceKind,string? CurrentRegionId,string? TravelingGroupId)
{
    public bool AtRegion=>PresenceKind==MercenaryWorldPresenceV3.AtRegion;
    public bool IsTraveling=>PresenceKind==MercenaryWorldPresenceV3.Traveling;
    public static MercenaryPresenceStateV3 CreateAtRegion(string mercenaryId,string regionId)=>new(mercenaryId,MercenaryWorldPresenceV3.AtRegion,regionId,null);
    public static MercenaryPresenceStateV3 CreateTraveling(string mercenaryId,string groupId)=>new(mercenaryId,MercenaryWorldPresenceV3.Traveling,null,groupId);
}

public sealed class RegionPersistentStateV3
{
    public RegionPersistentStateV3(
        string regionId,
        RegionTypeV3 regionType,
        string? ownerCompanyId,
        int terrainSeed,
        ResourceSessionV3 resources,
        ConstructionSessionV3 construction,
        StockpileSessionV3 stockpiles,
        ProductionSessionV3 production,
        FarmSessionV3 farming,
        EquipmentRuntimeV3 equipment)
    {
        if(string.IsNullOrWhiteSpace(regionId))throw new ArgumentException("RegionId is required.",nameof(regionId));
        if(!Enum.IsDefined(regionType))throw new ArgumentOutOfRangeException(nameof(regionType));
        RegionId=regionId;
        RegionType=regionType;
        OwnerCompanyId=ownerCompanyId;
        TerrainSeed=terrainSeed;
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(stockpiles);
        ArgumentNullException.ThrowIfNull(farming);
        Resources=resources;
        Construction=construction;
        Stockpiles=stockpiles;
        Farming=farming;
        ResourceNodes=resources.Nodes;
        GroundResourceStacks=resources.GroundStacks;
        Structures=construction.Structures;
        Blueprints=construction.Blueprints;
        StockpileZones=stockpiles.Zones;
        Production=production??throw new ArgumentNullException(nameof(production));
        FarmPlots=farming.Plots;
        ArgumentNullException.ThrowIfNull(equipment);
        EquipmentLocations=new RegionEquipmentLocationStoreV3(regionId);
        Production.BindEquipmentRegion(regionId);
    }

    public string RegionId{get;}
    public RegionTypeV3 RegionType{get;}
    public string? OwnerCompanyId{get;}
    public int TerrainSeed{get;}
    public ResourceNodeRegistryV3 ResourceNodes{get;}
    public GroundResourceStackRegistryV3 GroundResourceStacks{get;}
    public StructureRegistryV3 Structures{get;}
    public ConstructionBlueprintRegistryV3 Blueprints{get;}
    public StockpileZoneRegistryV3 StockpileZones{get;}
    public ProductionSessionV3 Production{get;}
    public FarmPlotRegistryV3 FarmPlots{get;}
    public RegionEquipmentLocationStoreV3 EquipmentLocations{get;}
    internal ResourceSessionV3 Resources{get;}
    internal ConstructionSessionV3 Construction{get;}
    internal StockpileSessionV3 Stockpiles{get;}
    internal FarmSessionV3 Farming{get;}
    public long RegionRevision{get;private set;}
    public double LastCommittedWorldTime{get;private set;}

    public void MarkCommitted(double worldTime)
    {
        if(!double.IsFinite(worldTime)||worldTime<0)throw new ArgumentOutOfRangeException(nameof(worldTime));
        LastCommittedWorldTime=worldTime;
        RegionRevision++;
    }

    public void MarkGameplayChanged()=>RegionRevision++;
    internal void RestoreRevision(long regionRevision,double lastCommittedWorldTime)
    {
        if(regionRevision<0||!double.IsFinite(lastCommittedWorldTime)||lastCommittedWorldTime<0)throw new ArgumentOutOfRangeException(nameof(regionRevision));
        RegionRevision=regionRevision;LastCommittedWorldTime=lastCommittedWorldTime;
    }
}


public sealed class PersistentWorldStateV3
{
    public const string DefaultGeneratorVersion="world_v2";
    public const string InitialEstateRegionId="region_estate_001";
    public const string SharedNeutralRegionId="region_shared_neutral_001";
    private readonly Dictionary<string,PlayerCompanyStateV3> _companies=new(StringComparer.Ordinal);
    private readonly Dictionary<string,RegionPersistentStateV3> _regions=new(StringComparer.Ordinal);

    public PersistentWorldStateV3(string worldId,int worldSeed,string generatorVersion)
    {
        if(string.IsNullOrWhiteSpace(worldId))throw new ArgumentException("WorldId is required.",nameof(worldId));
        if(string.IsNullOrWhiteSpace(generatorVersion))throw new ArgumentException("GeneratorVersion is required.",nameof(generatorVersion));
        WorldId=worldId;
        WorldSeed=worldSeed;
        GeneratorVersion=generatorVersion;
        WorldGraph=new(regionId=>_regions.ContainsKey(regionId),MarkWorldChanged);
        TravelingGroups=new(MarkWorldChanged);
    }

    public string WorldId{get;}
    public int WorldSeed{get;}
    public string GeneratorVersion{get;}
    public long WorldRevision{get;private set;}
    public PersistentWorldClockStateV3 WorldClock{get;}=new();
    public WorldGraphV3 WorldGraph{get;}
    public TravelingGroupRegistryV3 TravelingGroups{get;}
    public string? ActiveRegionId{get;private set;}
    public IReadOnlyDictionary<string,PlayerCompanyStateV3> PlayerCompanies=>new ReadOnlyDictionary<string,PlayerCompanyStateV3>(_companies);
    public IReadOnlyDictionary<string,RegionPersistentStateV3> Regions=>new ReadOnlyDictionary<string,RegionPersistentStateV3>(_regions);

    public bool TryRegisterCompany(PlayerCompanyStateV3 company,out string reason)
    {
        if(company==null||_companies.ContainsKey(company.CompanyId)){reason="DuplicateCompanyAuthority";return false;}
        _companies.Add(company.CompanyId,company);
        WorldRevision++;
        reason=string.Empty;
        return true;
    }

    public bool TryRegisterRegion(RegionPersistentStateV3 region,out string reason)
    {
        if(region==null||_regions.ContainsKey(region.RegionId)){reason="DuplicateRegionAuthority";return false;}
        _regions.Add(region.RegionId,region);
        if(region.OwnerCompanyId is { } owner&&_companies.TryGetValue(owner,out PlayerCompanyStateV3? company))company.AddOwnedRegion(region.RegionId);
        WorldRevision++;
        reason=string.Empty;
        return true;
    }

    public bool TryGetCompany(string companyId,out PlayerCompanyStateV3? company)=>_companies.TryGetValue(companyId,out company);
    public bool TryGetRegion(string regionId,out RegionPersistentStateV3? region)=>_regions.TryGetValue(regionId,out region);

    public bool TrySetActiveRegion(string regionId,out string reason)
    {
        if(!_regions.ContainsKey(regionId)){reason="UnknownRegion";return false;}
        if(ActiveRegionId==regionId){reason=string.Empty;return true;}
        ActiveRegionId=regionId;
        WorldRevision++;
        reason=string.Empty;
        return true;
    }

    public void ClearActiveRegion()
    {
        if(ActiveRegionId==null)return;
        ActiveRegionId=null;
        WorldRevision++;
    }
    internal void RestoreMetadata(long worldRevision,string? activeRegionId)
    {
        if(worldRevision<0||activeRegionId!=null&&!_regions.ContainsKey(activeRegionId))throw new ArgumentOutOfRangeException(nameof(worldRevision));
        WorldRevision=worldRevision;ActiveRegionId=activeRegionId;
    }
    private void MarkWorldChanged()=>WorldRevision++;
}

public sealed class ActiveRegionSessionV3 : IDisposable
{
    public ActiveRegionSessionV3(
        RegionPersistentStateV3 persistentState,
        PlayerCompanyStateV3 companyState,
        long sessionRevision,
        MercenaryControlSessionV3 control,
        MercenaryWorkSessionV3 work,
        JobManagerV3 jobs,
        WorkToolReservationSessionV3 toolReservations,
        SimulationClockSessionV3 clock,
        ResourceSessionV3 resources,
        ConstructionSessionV3 construction,
        StockpileSessionV3 stockpiles,
        FarmSessionV3 farming,
        bool clearSharedEquipmentReservationsOnDispose=true)
    {
        if(sessionRevision<1)throw new ArgumentOutOfRangeException(nameof(sessionRevision));
        PersistentState=persistentState??throw new ArgumentNullException(nameof(persistentState));
        CompanyState=companyState??throw new ArgumentNullException(nameof(companyState));
        if(persistentState.OwnerCompanyId!=null&&persistentState.OwnerCompanyId!=companyState.CompanyId)throw new ArgumentException("Active company does not own the region.");
        SessionRevision=sessionRevision;
        Control=control??throw new ArgumentNullException(nameof(control));
        Work=work??throw new ArgumentNullException(nameof(work));
        Jobs=jobs??throw new ArgumentNullException(nameof(jobs));
        ToolReservations=toolReservations??throw new ArgumentNullException(nameof(toolReservations));
        Clock=clock??throw new ArgumentNullException(nameof(clock));
        Resources=resources??throw new ArgumentNullException(nameof(resources));
        Construction=construction??throw new ArgumentNullException(nameof(construction));
        Stockpiles=stockpiles??throw new ArgumentNullException(nameof(stockpiles));
        Farming=farming??throw new ArgumentNullException(nameof(farming));
        ClearSharedEquipmentReservationsOnDispose=clearSharedEquipmentReservationsOnDispose;
        ActiveMercenaryIds=companyState.GetMercenaryIdsAtRegion(persistentState.RegionId);
    }

    public string RegionId=>PersistentState.RegionId;
    public long SessionRevision{get;}
    public RegionPersistentStateV3 PersistentState{get;}
    public PlayerCompanyStateV3 CompanyState{get;}
    public MercenaryControlSessionV3 Control{get;}
    public MercenaryWorkSessionV3 Work{get;}
    public JobManagerV3 Jobs{get;}
    public WorkToolReservationSessionV3 ToolReservations{get;}
    public SimulationClockSessionV3 Clock{get;}
    public ResourceSessionV3 Resources{get;}
    public ConstructionSessionV3 Construction{get;}
    public StockpileSessionV3 Stockpiles{get;}
    public FarmSessionV3 Farming{get;}
    public IReadOnlyList<string> ActiveMercenaryIds{get;}
    public int ActiveRuntimeMercenaryCount=>ActiveMercenaryIds.Count;
    public bool IsDisposed{get;private set;}
    private bool ClearSharedEquipmentReservationsOnDispose{get;}

    public void Dispose()
    {
        if(IsDisposed)return;
        Work.CancelAllTransientWork("ActiveRegionDisposed");
        Jobs.Clear();
        Resources.AmountReservations.Clear();
        Stockpiles.CellReservations.Clear();
        Farming.Reservations.Clear();
        Farming.Works.Clear();
        if(ClearSharedEquipmentReservationsOnDispose)CompanyState.Equipment.ClearReservations();
        ToolReservations.Dispose();
        IsDisposed=true;
    }
}
