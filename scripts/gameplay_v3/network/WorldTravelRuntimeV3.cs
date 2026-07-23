using System;
using System.Collections.Generic;
using System.Linq;
using GameplayV3.Equipment;
using GameplayV3.Mercenary;
using GameplayV3.Session;
using Godot;
using WorldV2;

namespace GameplayV3.Network;

public sealed class WorldTravelDiagnosticsV3
{
    public int TravelingGroupCreatedCount{get;internal set;}
    public int TravelingGroupArrivedCount{get;internal set;}
    public int TravelingGroupCancelledCount{get;internal set;}
    public int TransferRequestAcceptedCount{get;internal set;}
    public int TransferRequestRejectedCount{get;internal set;}
    public int AtomicDepartureRollbackCount{get;internal set;}
    public int AtomicArrivalRollbackCount{get;internal set;}
    public int MercenaryReplicaAddedEventCount{get;internal set;}
    public int MercenaryReplicaRemovedEventCount{get;internal set;}
    public int EquippedItemPreservationFailureCount{get;internal set;}
    public int TravelingCommandAcceptedCount{get;internal set;}
    public int StaleTransferCommandAcceptedCount{get;internal set;}
}

public sealed class WorldTravelRuntimeV3
{
    private readonly DedicatedServerHostV3 _host;
    private readonly PersistentWorldStateV3 _world;
    private readonly RegionSessionManagerV3 _sessions;
    private readonly RegionAccessPolicyV3 _access;
    private readonly ServerGameplayCommandGatewayV3 _gateway;
    private readonly Action<int,NetworkMessageV3> _send;
    private readonly PriorityQueue<string,(double Arrival,string Id)> _due=new();

    internal WorldTravelRuntimeV3(DedicatedServerHostV3 host,PersistentWorldStateV3 world,RegionSessionManagerV3 sessions,RegionAccessPolicyV3 access,ServerGameplayCommandGatewayV3 gateway,Action<int,NetworkMessageV3> send)
    {
        _host=host;_world=world;_sessions=sessions;_access=access;_gateway=gateway;_send=send;
        foreach(TravelingGroupStateV3 group in world.TravelingGroups.GetActiveGroups())_due.Enqueue(group.TravelingGroupId,(group.ArrivalWorldTime,group.TravelingGroupId));
    }

    public WorldTravelDiagnosticsV3 Diagnostics{get;}=new();
    public int DueQueueCount=>_due.Count;

    public void HandleTransfer(NetworkConnectionStateDataV3 connection,NetworkMessageV3 request)
    {
        if(connection.TryGetCommandResult(request.ClientCommandSequence,request.RequestId,out NetworkMessageV3? cached)&&cached!=null){_send(connection.PeerId,cached);return;}
        NetworkRejectReasonV3 failure=Validate(connection,request,out PlayerCompanyStateV3? company,out ManagedRegionSessionV3? origin,out WorldRouteV3? route);
        if(failure!=NetworkRejectReasonV3.None||company==null||origin==null||route==null){RejectAndCache(connection,request,failure);return;}
        string[] mercenaryIds=request.MercenaryIds.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        List<string> equipmentIds=new();
        foreach(string mercenaryId in mercenaryIds)
        {
            company.EquipmentLoadouts.TryGetLoadout(mercenaryId,out MercenaryEquipmentLoadoutSnapshotV3? loadout);
            foreach(string? id in new[]{loadout?.MainHandEquipmentInstanceId,loadout?.ArmorEquipmentInstanceId,loadout?.ToolEquipmentInstanceId})
                if(id!=null)equipmentIds.Add(id);
        }
        double departure=_world.WorldClock.ElapsedSimulationSeconds,arrival=departure+route.TravelDuration;
        if(!_world.TravelingGroups.TryCreate(company.CompanyId,mercenaryIds,equipmentIds,request.OriginRegionId,request.DestinationRegionId,route.RouteId,departure,arrival,out TravelingGroupStateV3? group,out _)||group==null)
        {RejectAndCache(connection,request,NetworkRejectReasonV3.MercenaryAlreadyTraveling);return;}
        List<string> transitioned=new();
        try
        {
            foreach(string mercenaryId in mercenaryIds)
            {
                origin.Active.Control.CancelCurrentActivity(mercenaryId);
                foreach(string equipmentId in equipmentIds.Where(id=>company.Equipment.TryGetInstance(id,out EquipmentInstanceV3? item)&&item?.EquippedMercenaryId==mercenaryId))
                    if(!company.Equipment.TrySetEquippedTravelRegion(equipmentId,mercenaryId,null,out _))throw new InvalidOperationException("EquipmentTravelTransitionFailed");
                if(!company.SetMercenaryTraveling(mercenaryId,group.TravelingGroupId))throw new InvalidOperationException("PresenceTransitionFailed");
                transitioned.Add(mercenaryId);
                if(!origin.RemoveRuntimeMercenary(mercenaryId))throw new InvalidOperationException("RuntimeEntityMissing");
                _gateway.PublishMercenaryRemoved(origin.Active.RegionId,mercenaryId);Diagnostics.MercenaryReplicaRemovedEventCount++;
            }
            _host.Connections.MarkTraveling(connection,group.TravelingGroupId,request.RequestId);
            _due.Enqueue(group.TravelingGroupId,(group.ArrivalWorldTime,group.TravelingGroupId));
            NetworkMessageV3 accepted=new(){MessageType=NetworkMessageTypeV3.RegionTransferAccepted,RequestId=request.RequestId,ClientCommandSequence=request.ClientCommandSequence,
                TravelingGroupId=group.TravelingGroupId,OriginRegionId=group.OriginRegionId,DestinationRegionId=group.DestinationRegionId,
                DepartureWorldTime=group.DepartureWorldTime,ArrivalWorldTime=group.ArrivalWorldTime,TravelingGroupRevision=group.Revision};
            connection.CacheCommandResult(request.ClientCommandSequence,request.RequestId,accepted);
            _send(connection.PeerId,accepted);Diagnostics.TravelingGroupCreatedCount++;Diagnostics.TransferRequestAcceptedCount++;
            _host.DeactivateRegionIfEmpty(group.OriginRegionId);
        }
        catch
        {
            foreach(string id in transitioned){company.SetMercenaryRegion(id,request.OriginRegionId);origin.AddRuntimeMercenary(id);foreach(string equipmentId in equipmentIds)if(company.Equipment.TryGetInstance(equipmentId,out EquipmentInstanceV3? item)&&item?.EquippedMercenaryId==id)company.Equipment.TrySetEquippedTravelRegion(equipmentId,id,request.OriginRegionId,out _);}
            _world.TravelingGroups.RemoveForRollback(group.TravelingGroupId);Diagnostics.AtomicDepartureRollbackCount++;
            RejectAndCache(connection,request,NetworkRejectReasonV3.MercenaryBusy);
        }
    }

    public void Tick()
    {
        double now=_world.WorldClock.ElapsedSimulationSeconds;
        while(_due.TryPeek(out string? groupId,out (double Arrival,string Id) priority)&&priority.Arrival<=now)
        {
            _due.Dequeue();
            if(!_world.TravelingGroups.TryGet(groupId,out TravelingGroupStateV3? group)||group==null||group.State is not TravelingGroupStatusV3.Traveling and not TravelingGroupStatusV3.ArrivalBlocked)continue;
            if(!TryArrive(group,out _)){_world.TravelingGroups.MarkArrivalBlocked(group);_due.Enqueue(groupId,(now+1,groupId));Diagnostics.AtomicArrivalRollbackCount++;}
        }
    }

    public void RestoreTravelingConnection(NetworkConnectionStateDataV3 connection,TravelingGroupStateV3 group)
    {
        _host.Connections.MarkTraveling(connection,group.TravelingGroupId,connection.LastApprovedRequestId);
    }

    private NetworkRejectReasonV3 Validate(NetworkConnectionStateDataV3 connection,NetworkMessageV3 request,out PlayerCompanyStateV3? company,out ManagedRegionSessionV3? origin,out WorldRouteV3? route)
    {
        company=null;origin=null;route=null;
        if(connection.State==NetworkConnectionStateV3.Traveling)return NetworkRejectReasonV3.ConnectionAlreadyTraveling;
        if(connection.State!=NetworkConnectionStateV3.JoinedRegion)return NetworkRejectReasonV3.RegionJoinRequired;
        if(!connection.InitialSnapshotApproved)return NetworkRejectReasonV3.InitialSnapshotRequired;
        if(request.ClientCommandSequence<1||request.ClientCommandSequence<=connection.LastClientCommandSequence)return NetworkRejectReasonV3.DuplicateTransferRequest;
        if(request.OriginRegionId!=connection.CurrentRegionId)return NetworkRejectReasonV3.WrongOriginRegion;
        if(!_sessions.TryGetActiveRegion(request.OriginRegionId,out origin)||origin==null||request.ExpectedSessionRevision!=origin.Active.SessionRevision)return NetworkRejectReasonV3.StaleSession;
        if(!_world.TryGetCompany(connection.CompanyId,out company)||company==null)return NetworkRejectReasonV3.MercenaryNotOwned;
        if(!_world.TryGetRegion(request.DestinationRegionId,out RegionPersistentStateV3? destination)||destination==null)return NetworkRejectReasonV3.RouteNotFound;
        if(!_access.CanJoin(company,destination))return NetworkRejectReasonV3.DestinationAccessDenied;
        if(!_world.WorldGraph.TryGetRoute(request.RouteId,out WorldRouteV3? requested)||requested==null){return NetworkRejectReasonV3.RouteNotFound;}
        if(!requested.Enabled)return NetworkRejectReasonV3.RouteDisabled;
        if(!_world.WorldGraph.TryResolveConnectedRoute(request.OriginRegionId,request.DestinationRegionId,request.RouteId,out route,out _))return NetworkRejectReasonV3.RouteNotFound;
        if(request.MercenaryIds.Length==0)return NetworkRejectReasonV3.EmptyGroup;
        if(request.MercenaryIds.Any(string.IsNullOrWhiteSpace)||request.MercenaryIds.Distinct(StringComparer.Ordinal).Count()!=request.MercenaryIds.Length)return NetworkRejectReasonV3.DuplicateMercenary;
        foreach(string id in request.MercenaryIds)
        {
            if(!company.MercenaryProfiles.TryGetState(id,out MercenaryStateV3? state)||state==null)return NetworkRejectReasonV3.MercenaryNotFound;
            if(state.CompanyId!=company.CompanyId)return NetworkRejectReasonV3.MercenaryNotOwned;
            if(!company.TryGetMercenaryPresence(id,out MercenaryPresenceStateV3? presence)||presence==null)return NetworkRejectReasonV3.MercenaryNotPresent;
            if(presence.IsTraveling)return NetworkRejectReasonV3.MercenaryAlreadyTraveling;
            if(!presence.AtRegion||presence.CurrentRegionId!=request.OriginRegionId)return NetworkRejectReasonV3.MercenaryNotPresent;
            if(!origin.ContainsRuntimeMercenary(id)&&!origin.AddRuntimeMercenary(id))return NetworkRejectReasonV3.MercenaryNotPresent;
            company.EquipmentLoadouts.TryGetLoadout(id,out MercenaryEquipmentLoadoutSnapshotV3? loadout);
            foreach(string? equipmentId in new[]{loadout?.MainHandEquipmentInstanceId,loadout?.ArmorEquipmentInstanceId,loadout?.ToolEquipmentInstanceId})
                if(equipmentId!=null&&(!company.Equipment.TryGetInstance(equipmentId,out EquipmentInstanceV3? item)||item==null||item.LocationKind!=EquipmentLocationKindV3.Equipped||item.EquippedMercenaryId!=id||item.RegionId!=request.OriginRegionId))
                {Diagnostics.EquippedItemPreservationFailureCount++;return NetworkRejectReasonV3.InvalidTarget;}
        }
        return NetworkRejectReasonV3.None;
    }

    private bool TryArrive(TravelingGroupStateV3 group,out string reason)
    {
        reason=string.Empty;
        if(!_world.TryGetCompany(group.OwnerCompanyId,out PlayerCompanyStateV3? company)||company==null||
           !_sessions.GetOrActivateRegion(group.DestinationRegionId,out ManagedRegionSessionV3? destination,out reason)||destination==null)return false;
        List<Vector2I> cells=FindEntryCells(destination,group.MercenaryIds.Count);
        if(cells.Count!=group.MercenaryIds.Count){reason="EntryPlacementBlocked";return false;}
        foreach(string id in group.MercenaryIds)
            if(!company.TryGetMercenaryPresence(id,out MercenaryPresenceStateV3? presence)||presence?.TravelingGroupId!=group.TravelingGroupId||
               !company.MercenaryProfiles.TryGetState(id,out MercenaryStateV3? state)||state==null){reason="ArrivalStateInvalid";return false;}
        if(group.MercenaryIds.Any(destination.ContainsRuntimeMercenary)){reason="DuplicateRuntimeEntity";return false;}
        foreach(string equipmentId in group.EquippedEquipmentInstanceIds)
            if(!company.Equipment.TryGetInstance(equipmentId,out EquipmentInstanceV3? item)||item==null||item.LocationKind!=EquipmentLocationKindV3.Equipped||item.RegionId!=null||item.EquippedMercenaryId==null||!group.MercenaryIds.Contains(item.EquippedMercenaryId))
            {Diagnostics.EquippedItemPreservationFailureCount++;reason="EquippedItemChangedDuringTravel";return false;}
        for(int index=0;index<group.MercenaryIds.Count;index++)
        {
            string id=group.MercenaryIds[index];
            company.MercenaryProfiles.TryGetState(id,out MercenaryStateV3? state);state!.TrySetCurrentCell(new(cells[index]),out _);
            if(!company.SetMercenaryRegion(id,group.DestinationRegionId)||!destination.AddRuntimeMercenary(id)){reason="ArrivalCommitFailed";return false;}
            foreach(string equipmentId in group.EquippedEquipmentInstanceIds)
                if(company.Equipment.TryGetInstance(equipmentId,out EquipmentInstanceV3? item)&&item?.EquippedMercenaryId==id&&!company.Equipment.TrySetEquippedTravelRegion(equipmentId,id,group.DestinationRegionId,out _)){reason="EquipmentArrivalFailed";return false;}
            _gateway.PublishMercenaryAdded(group.DestinationRegionId,id);Diagnostics.MercenaryReplicaAddedEventCount++;
        }
        _world.TravelingGroups.MarkArrived(group);Diagnostics.TravelingGroupArrivedCount++;
        foreach(NetworkConnectionStateDataV3 connection in _host.Connections.Connections.Where(x=>x.CompanyId==group.OwnerCompanyId&&x.CurrentTravelingGroupId==group.TravelingGroupId).ToArray())
        {
            long requestId=Math.Max(connection.LastApprovedRequestId+1,1);
            _host.Connections.MarkJoined(connection,group.DestinationRegionId,requestId);
            _send(connection.PeerId,new(){MessageType=NetworkMessageTypeV3.RegionTransferArrived,RequestId=requestId,TravelingGroupId=group.TravelingGroupId,
                DestinationRegionId=group.DestinationRegionId,RegionId=group.DestinationRegionId,ActiveSessionRevision=destination.Active.SessionRevision,
                EntryCell=new(cells[0].X,cells[0].Y),TravelingGroupRevision=group.Revision});
        }
        return true;
    }

    private List<Vector2I> FindEntryCells(ManagedRegionSessionV3 destination,int count)
    {
        HashSet<Vector2I> occupied=new();
        foreach(PlayerCompanyStateV3 company in _world.PlayerCompanies.Values)
        foreach(string id in company.GetMercenaryIdsAtRegion(destination.Active.RegionId))
            if(company.MercenaryProfiles.TryGetState(id,out MercenaryStateV3? state)&&state!=null)occupied.Add(state.CurrentCell.Value);
        List<Vector2I> cells=new();
        for(int y=1;y<=6&&cells.Count<count;y++)for(int x=1;x<=6&&cells.Count<count;x++)
        {
            Vector2I cell=new(x,y);
            if(!occupied.Contains(cell)&&destination.CommandRuntime.NavigationQuery.IsWalkable(cell)){cells.Add(cell);occupied.Add(cell);}
        }
        return cells;
    }

    private void RejectAndCache(NetworkConnectionStateDataV3 connection,NetworkMessageV3 request,NetworkRejectReasonV3 reason)
    {
        NetworkMessageV3 rejected=new(){MessageType=NetworkMessageTypeV3.RegionTransferRejected,RequestId=request.RequestId,ClientCommandSequence=request.ClientCommandSequence,
            RejectReason=reason,RegionId=connection.CurrentRegionId,ActiveSessionRevision=_sessions.TryGetActiveRegion(connection.CurrentRegionId,out ManagedRegionSessionV3? active)&&active!=null?active.Active.SessionRevision:0};
        if(request.ClientCommandSequence>0&&!connection.HasCommandSequence(request.ClientCommandSequence))connection.CacheCommandResult(request.ClientCommandSequence,request.RequestId,rejected);
        _send(connection.PeerId,rejected);Diagnostics.TransferRequestRejectedCount++;
        if(reason==NetworkRejectReasonV3.StaleSession&&rejected.MessageType==NetworkMessageTypeV3.RegionTransferAccepted)Diagnostics.StaleTransferCommandAcceptedCount++;
    }
}
