using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GameplayV3.Session;

public sealed record WorldRouteV3(
    string RouteId,
    string OriginRegionId,
    string DestinationRegionId,
    bool Bidirectional,
    double TravelDuration,
    bool Enabled);

public sealed class WorldGraphV3
{
    private readonly Func<string,bool> _regionExists;
    private readonly Action _changed;
    private readonly Dictionary<string,WorldRouteV3> _routes=new(StringComparer.Ordinal);
    private readonly Dictionary<string,HashSet<string>> _routeIdsByRegion=new(StringComparer.Ordinal);

    public WorldGraphV3(Func<string,bool> regionExists,Action? changed=null){_regionExists=regionExists??throw new ArgumentNullException(nameof(regionExists));_changed=changed??(()=>{});}
    public int RouteCount=>_routes.Count;
    public IReadOnlyCollection<WorldRouteV3> Routes=>new ReadOnlyCollection<WorldRouteV3>(_routes.Values.OrderBy(x=>x.RouteId,StringComparer.Ordinal).ToList());

    public bool AddRoute(WorldRouteV3 route,out string reason)
    {
        if(route==null||string.IsNullOrWhiteSpace(route.RouteId)||string.IsNullOrWhiteSpace(route.OriginRegionId)||string.IsNullOrWhiteSpace(route.DestinationRegionId)||
           route.OriginRegionId==route.DestinationRegionId||!double.IsFinite(route.TravelDuration)||route.TravelDuration<=0)
        {reason="InvalidRoute";return false;}
        if(!_regionExists(route.OriginRegionId)||!_regionExists(route.DestinationRegionId)){reason="UnknownRouteRegion";return false;}
        if(!_routes.TryAdd(route.RouteId,route)){reason="DuplicateRoute";return false;}
        AddIndex(route.OriginRegionId,route.RouteId);AddIndex(route.DestinationRegionId,route.RouteId);
        _changed();
        reason=string.Empty;return true;
    }

    public bool TryGetRoute(string routeId,out WorldRouteV3? route)=>_routes.TryGetValue(routeId,out route);

    public bool TryResolveConnectedRoute(string originRegionId,string destinationRegionId,string? requestedRouteId,out WorldRouteV3? route,out string reason)
    {
        route=null;
        if(!_regionExists(originRegionId)||!_regionExists(destinationRegionId)){reason="UnknownRouteRegion";return false;}
        if(!string.IsNullOrEmpty(requestedRouteId))
        {
            if(!_routes.TryGetValue(requestedRouteId,out route)){reason="RouteNotFound";return false;}
            if(!Connects(route,originRegionId,destinationRegionId)){route=null;reason="RouteNotConnected";return false;}
            if(!route.Enabled){route=null;reason="RouteDisabled";return false;}
            reason=string.Empty;return true;
        }
        if(_routeIdsByRegion.TryGetValue(originRegionId,out HashSet<string>? ids))
            foreach(string id in ids.OrderBy(x=>x,StringComparer.Ordinal))
                if(_routes.TryGetValue(id,out WorldRouteV3? candidate)&&candidate.Enabled&&Connects(candidate,originRegionId,destinationRegionId))
                {route=candidate;reason=string.Empty;return true;}
        reason="RouteNotFound";return false;
    }

    private static bool Connects(WorldRouteV3 route,string origin,string destination)=>
        route.OriginRegionId==origin&&route.DestinationRegionId==destination||
        route.Bidirectional&&route.OriginRegionId==destination&&route.DestinationRegionId==origin;
    private void AddIndex(string regionId,string routeId)
    {
        if(!_routeIdsByRegion.TryGetValue(regionId,out HashSet<string>? ids)){ids=new(StringComparer.Ordinal);_routeIdsByRegion.Add(regionId,ids);}
        ids.Add(routeId);
    }
}

public enum TravelingGroupStatusV3
{
    Preparing=1,
    Traveling=2,
    Arrived=3,
    ArrivalBlocked=4,
    Cancelled=5
}

public sealed class TravelingGroupStateV3
{
    internal TravelingGroupStateV3(string id,string companyId,IReadOnlyList<string> mercenaryIds,IReadOnlyList<string> equipmentIds,string origin,string destination,string routeId,double departure,double arrival)
    {
        TravelingGroupId=id;OwnerCompanyId=companyId;MercenaryIds=new ReadOnlyCollection<string>(mercenaryIds.ToArray());
        EquippedEquipmentInstanceIds=new ReadOnlyCollection<string>(equipmentIds.ToArray());OriginRegionId=origin;DestinationRegionId=destination;
        RouteId=routeId;DepartureWorldTime=departure;ArrivalWorldTime=arrival;State=TravelingGroupStatusV3.Traveling;Revision=1;
    }
    public string TravelingGroupId{get;}
    public string OwnerCompanyId{get;}
    public IReadOnlyList<string> MercenaryIds{get;}
    public IReadOnlyList<string> EquippedEquipmentInstanceIds{get;}
    public string OriginRegionId{get;}
    public string DestinationRegionId{get;}
    public string RouteId{get;}
    public double DepartureWorldTime{get;}
    public double ArrivalWorldTime{get;}
    public TravelingGroupStatusV3 State{get;private set;}
    public long Revision{get;private set;}
    internal void MarkArrived(){if(State is TravelingGroupStatusV3.Traveling or TravelingGroupStatusV3.ArrivalBlocked){State=TravelingGroupStatusV3.Arrived;Revision++;}}
    internal void MarkArrivalBlocked(){if(State==TravelingGroupStatusV3.Traveling){State=TravelingGroupStatusV3.ArrivalBlocked;Revision++;}}
    internal void ResumeTravel(){if(State==TravelingGroupStatusV3.ArrivalBlocked){State=TravelingGroupStatusV3.Traveling;Revision++;}}
    internal void RestoreState(TravelingGroupStatusV3 state,long revision){State=state;Revision=revision;}
}

public sealed class TravelingGroupRegistryV3
{
    private readonly Dictionary<string,TravelingGroupStateV3> _groups=new(StringComparer.Ordinal);
    private readonly Dictionary<string,string> _groupByMercenary=new(StringComparer.Ordinal);
    private long _sequence;
    private readonly Action _changed;
    public TravelingGroupRegistryV3(Action? changed=null)=>_changed=changed??(()=>{});
    public int Count=>_groups.Count;
    public long NextSequence=>_sequence+1;
    public int ActiveCount=>_groups.Values.Count(x=>x.State is TravelingGroupStatusV3.Traveling or TravelingGroupStatusV3.ArrivalBlocked);
    public bool TryGet(string groupId,out TravelingGroupStateV3? group)=>_groups.TryGetValue(groupId,out group);
    public bool TryGetByMercenary(string mercenaryId,out TravelingGroupStateV3? group)
    {
        group=null;return _groupByMercenary.TryGetValue(mercenaryId,out string? id)&&_groups.TryGetValue(id,out group);
    }
    public IReadOnlyList<TravelingGroupStateV3> GetActiveGroups()=>new ReadOnlyCollection<TravelingGroupStateV3>(_groups.Values.Where(x=>x.State is TravelingGroupStatusV3.Traveling or TravelingGroupStatusV3.ArrivalBlocked).OrderBy(x=>x.ArrivalWorldTime).ThenBy(x=>x.TravelingGroupId,StringComparer.Ordinal).ToList());
    public IReadOnlyList<TravelingGroupStateV3> GetAllGroups()=>new ReadOnlyCollection<TravelingGroupStateV3>(_groups.Values.OrderBy(x=>x.TravelingGroupId,StringComparer.Ordinal).ToList());
    public TravelingGroupStateV3? GetActiveForCompany(string companyId)=>_groups.Values.Where(x=>x.OwnerCompanyId==companyId&&x.State is TravelingGroupStatusV3.Traveling or TravelingGroupStatusV3.ArrivalBlocked).OrderBy(x=>x.TravelingGroupId,StringComparer.Ordinal).FirstOrDefault();
    internal bool TryCreate(string companyId,IReadOnlyList<string> mercenaryIds,IReadOnlyList<string> equipmentIds,string origin,string destination,string routeId,double departure,double arrival,out TravelingGroupStateV3? group,out string reason)
    {
        group=null;
        if(mercenaryIds.Count==0||mercenaryIds.Distinct(StringComparer.Ordinal).Count()!=mercenaryIds.Count){reason="InvalidMercenarySet";return false;}
        if(mercenaryIds.Any(_groupByMercenary.ContainsKey)){reason="MercenaryAlreadyTraveling";return false;}
        string id=$"travel_{++_sequence:x16}";
        group=new(id,companyId,mercenaryIds,equipmentIds,origin,destination,routeId,departure,arrival);
        _groups.Add(id,group);foreach(string mercenaryId in mercenaryIds)_groupByMercenary.Add(mercenaryId,id);
        _changed();
        reason=string.Empty;return true;
    }
    internal void MarkArrived(TravelingGroupStateV3 group){group.MarkArrived();foreach(string id in group.MercenaryIds)_groupByMercenary.Remove(id);_changed();}
    internal bool TryRestore(string id,string companyId,IReadOnlyList<string> mercenaryIds,IReadOnlyList<string> equipmentIds,string origin,string destination,string routeId,double departure,double arrival,TravelingGroupStatusV3 state,long revision,out string reason)
    {
        if(string.IsNullOrWhiteSpace(id)||_groups.ContainsKey(id)||mercenaryIds.Count==0||mercenaryIds.Distinct(StringComparer.Ordinal).Count()!=mercenaryIds.Count||
           state is TravelingGroupStatusV3.Traveling or TravelingGroupStatusV3.ArrivalBlocked&&mercenaryIds.Any(_groupByMercenary.ContainsKey))
        {reason="InvalidOrDuplicateTravelingGroup";return false;}
        TravelingGroupStateV3 group=new(id,companyId,mercenaryIds,equipmentIds,origin,destination,routeId,departure,arrival);group.RestoreState(state,revision);
        _groups.Add(id,group);if(state is TravelingGroupStatusV3.Traveling or TravelingGroupStatusV3.ArrivalBlocked)foreach(string mercenaryId in mercenaryIds)_groupByMercenary.Add(mercenaryId,id);
        _changed();reason=string.Empty;return true;
    }
    internal void RestoreNextSequence(long nextSequence){if(nextSequence<1)throw new ArgumentOutOfRangeException(nameof(nextSequence));_sequence=Math.Max(_sequence,nextSequence-1);}
    internal void MarkArrivalBlocked(TravelingGroupStateV3 group){group.MarkArrivalBlocked();_changed();}
    internal bool RemoveForRollback(string groupId)
    {
        if(!_groups.Remove(groupId,out TravelingGroupStateV3? group))return false;
        foreach(string id in group.MercenaryIds)_groupByMercenary.Remove(id);
        _changed();
        return true;
    }
}
