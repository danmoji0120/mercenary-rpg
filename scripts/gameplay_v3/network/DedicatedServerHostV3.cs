using System;
using System.Collections.Generic;
using System.Linq;
using GameplayV3.Company;
using GameplayV3.Checkpoint;
using GameplayV3.Equipment;
using GameplayV3.Mercenary;
using GameplayV3.Session;
using GameplayV3.Time;
using Godot;

namespace GameplayV3.Network;

public sealed class DedicatedServerDiagnosticsV3
{
    public int AcceptedHandshakeCount{get;internal set;}
    public int RejectedHandshakeCount{get;internal set;}
    public int RejectedPreHandshakeRequestCount{get;internal set;}
    public int RejectedProtocolMismatchCount{get;internal set;}
    public int RejectedDuplicateAccountCount{get;internal set;}
    public int StalePeerRequestAcceptedCount{get;internal set;}
    public int DisconnectCleanupFailureCount{get;internal set;}
    public int SnapshotRequestCount{get;internal set;}
    public int SnapshotAcceptedCount{get;internal set;}
    public int SnapshotRejectedCount{get;internal set;}
    public int SnapshotByteSize{get;internal set;}
    public int SnapshotBuildFailureCount{get;internal set;}
    public int CrossCompanyEstateJoinRejectedCount{get;internal set;}
    public int DisconnectMembershipCleanupFailureCount{get;internal set;}
}

public sealed record DedicatedServerSnapshotV3(
    bool ServerRunning,
    int ProtocolVersion,
    int ConnectedPeerCount,
    int HandshakenConnectionCount,
    int JoinedRegionConnectionCount,
    int PersistentWorldCount,
    int PlayerCompanyCount,
    int RegionCount,
    int ActiveRegionSessionCount,
    DedicatedServerDiagnosticsV3 Diagnostics);

public sealed class DedicatedServerHostV3:IDisposable
{
    private readonly Dictionary<string,PlayerCompanyStateV3> _developmentAccountBindings=new(StringComparer.Ordinal);
    private readonly HashSet<int> _closedPeerIds=new();
    private readonly Dictionary<int,ENetPacketPeer> _transportPeers=new();
    private ENetMultiplayerPeer? _peer;
    private bool _stopping;
    private readonly RegionSnapshotBuilderV3 _snapshotBuilder=new();
    private readonly RegionAccessPolicyV3 _regionAccess=new();
    private ServerGameplayCommandGatewayV3? _commandGateway;
    private RegionSessionManagerV3? _regionSessions;
    private WorldTravelRuntimeV3? _travelRuntime;
    private PlayerCompanyStateV3? _defaultCompany;
    private CompanySessionV3? _companySession;
    private MercenarySessionV3? _mercenaries;
    private EquipmentRuntimeV3? _equipment;
    private EquipmentLoadoutRuntimeV3? _loadouts;
    private SimulationClockSessionV3? _clock;
    private bool _checkpointing;

    public NetworkConnectionRegistryV3 Connections{get;}=new();
    public DedicatedServerDiagnosticsV3 Diagnostics{get;}=new();
    public PersistentWorldStateV3? World{get;private set;}
    public bool IsRunning=>_peer!=null&&!_stopping;
    public int Port{get;private set;}
    public string DefaultDevelopmentPlayerAccountId{get;private set;}=string.Empty;
    public int SnapshotPayloadByteLimit{get;private set;}
    public ServerCommandDeltaDiagnosticsV3? CommandDiagnostics=>_commandGateway?.Diagnostics;
    public int RegionMembershipCount=>Connections.RegionMembershipCount;
    public RegionSessionManagerV3? RegionSessions=>_regionSessions;
    public PlayerCompanyStateV3? DefaultCompany=>_defaultCompany;
    public WorldTravelRuntimeV3? TravelRuntime=>_travelRuntime;

    public DedicatedServerHostV3(int snapshotPayloadByteLimit=RegionSnapshotProtocolV3.MaximumSnapshotPayloadBytes)
    {
        if(snapshotPayloadByteLimit<1024||snapshotPayloadByteLimit>RegionSnapshotProtocolV3.MaximumSnapshotPayloadBytes)throw new ArgumentOutOfRangeException(nameof(snapshotPayloadByteLimit));
        SnapshotPayloadByteLimit=snapshotPayloadByteLimit;
    }

    public bool TrySetSnapshotPayloadByteLimit(int bytes)
    {
        if(bytes<1024||bytes>RegionSnapshotProtocolV3.MaximumSnapshotPayloadBytes)return false;
        SnapshotPayloadByteLimit=bytes;
        return true;
    }

    public bool TryStart(int port,int worldSeed,out string reason)
    {
        reason=string.Empty;
        if(IsRunning){reason="ServerAlreadyRunning";return false;}
        if(port is <1 or >65535){reason="InvalidPort";return false;}
        ENetMultiplayerPeer peer=new();
        Error error=peer.CreateServer(port,8);
        if(error!=Error.Ok){peer.Dispose();reason="ENetCreateServer:"+error;return false;}
        _peer=peer;
        Port=port;
        _stopping=false;
        World=GameplaySessionV3.CreateNewPersistentWorld(worldSeed,"dedicated_memory_v1");
        PlayerCompanyStateV3 company=GameplaySessionV3.GetActiveCompanyState()??throw new InvalidOperationException("Dedicated server company authority missing.");
        if(!GameplaySessionV3.TryGetCompanySession(out CompanySessionV3? companySession)||companySession==null||
           !GameplaySessionV3.TryGetMercenarySession(out MercenarySessionV3? mercenaries)||mercenaries==null||
           !GameplaySessionV3.TryGetEquipmentRuntime(out EquipmentRuntimeV3? equipment)||equipment==null||
           !GameplaySessionV3.TryGetEquipmentLoadouts(out EquipmentLoadoutRuntimeV3? loadouts)||loadouts==null||
           !GameplaySessionV3.TryGetSimulationClock(out GameplayV3.Time.SimulationClockSessionV3? clock)||clock==null)
            throw new InvalidOperationException("Dedicated server runtime dependencies are missing.");
        _defaultCompany=company;_companySession=companySession;_mercenaries=mercenaries;_equipment=equipment;_loadouts=loadouts;_clock=clock;
        GameplaySessionV3.DisposeActiveRegionRuntime();
        _regionSessions=new(World,companySession,mercenaries,equipment,loadouts,clock);
        if(!_regionSessions.GetOrActivateRegion(PersistentWorldStateV3.InitialEstateRegionId,out _,out reason))throw new InvalidOperationException(reason);
        DefaultDevelopmentPlayerAccountId=company.PlayerAccountId;
        _commandGateway=new(this,_regionSessions,Send);
        _travelRuntime=new(this,World,_regionSessions,_regionAccess,_commandGateway,Send);
        return true;
    }

    public bool TryStartFromCheckpoint(int port,string checkpointPath,ServerCheckpointStoreV3 store,out string reason)
    {
        reason=string.Empty;
        if(IsRunning){reason="ServerAlreadyRunning";return false;}
        if(port is <1 or >65535){reason="InvalidPort";return false;}
        if(!store.LoadAndValidate(checkpointPath,out PersistentWorldCheckpointV3? checkpoint,out reason)||checkpoint==null||
           !PersistentWorldCheckpointMapperV3.TryRestoreFresh(checkpoint,out RestoredPersistentWorldRuntimeV3? restored,out reason)||restored==null)return false;
        ENetMultiplayerPeer peer=new();Error error=peer.CreateServer(port,8);if(error!=Error.Ok){peer.Dispose();reason="ENetCreateServer:"+error;return false;}
        _peer=peer;Port=port;_stopping=false;World=restored.World;_companySession=restored.Companies;_mercenaries=restored.Mercenaries;_equipment=restored.Equipment;_loadouts=restored.Loadouts;_clock=restored.Clock;
        foreach(var pair in restored.AccountBindings)
            if(World.TryGetCompany(pair.Value,out PlayerCompanyStateV3? company)&&company!=null)_developmentAccountBindings.Add(pair.Key,company);
        _defaultCompany=_developmentAccountBindings.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Value).FirstOrDefault()??World.PlayerCompanies.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Value).FirstOrDefault();
        DefaultDevelopmentPlayerAccountId=_developmentAccountBindings.Keys.OrderBy(x=>x,StringComparer.Ordinal).FirstOrDefault()??_defaultCompany?.PlayerAccountId??string.Empty;
        _regionSessions=new(World,_companySession,_mercenaries,_equipment,_loadouts,_clock);_regionSessions.RestoreSessionGenerationFloors(restored.RegionSessionGenerations);
        _commandGateway=new(this,_regionSessions,Send);_travelRuntime=new(this,World,_regionSessions,_regionAccess,_commandGateway,Send);
        return true;
    }

    public bool TrySaveCheckpoint(string checkpointPath,ServerCheckpointStoreV3 store,out string reason)
    {
        reason=string.Empty;if(!IsRunning||World==null||_regionSessions==null){reason="ServerNotRunning";return false;}
        _checkpointing=true;
        try
        {
            foreach(string regionId in _regionSessions.ActiveRegionIds)if(!_regionSessions.CommitRegion(regionId,out reason))return false;
            PersistentWorldCheckpointV3 checkpoint=PersistentWorldCheckpointMapperV3.Capture(World,_regionSessions,_developmentAccountBindings);
            return store.SaveAtomic(checkpointPath,checkpoint,out reason);
        }
        finally{_checkpointing=false;}
    }

    public void Poll()
    {
        if(!IsRunning||_peer==null)return;
        _peer.Poll();
        while(_peer.GetAvailablePacketCount()>0)
        {
            int peerId=_peer.GetPacketPeer();
            byte[] packet=_peer.GetPacket();
            HandlePacket(peerId,packet);
        }
        List<int>? disconnected=null;
        foreach((int peerId,ENetPacketPeer transportPeer) in _transportPeers)
            if((int)transportPeer.GetState()==0)(disconnected??=new()).Add(peerId);
        if(disconnected!=null)foreach(int peerId in disconnected)
        {
            _transportPeers.Remove(peerId);
            string previousRegion=Connections.TryGet(peerId,out NetworkConnectionStateDataV3? disconnectedConnection)?disconnectedConnection?.CurrentRegionId??string.Empty:string.Empty;
            if(!Connections.Remove(peerId))Diagnostics.DisconnectMembershipCleanupFailureCount++;
            DeactivateEmptyRegion(previousRegion);
            _closedPeerIds.Add(peerId);
        }
        _regionSessions?.TickActiveRegions(.25f);
        _travelRuntime?.Tick();
        _commandGateway?.FlushDeltas();
    }

    public DedicatedServerSnapshotV3 GetSnapshot()=>new(
        IsRunning,
        NetworkProtocolV3.ProtocolVersion,
        Connections.Count,
        Connections.HandshakenCount,
        Connections.JoinedRegionCount,
        World==null?0:1,
        World?.PlayerCompanies.Count??0,
        World?.Regions.Count??0,
        _regionSessions?.ActiveSessionCount??0,
        Diagnostics);

    public void Stop()
    {
        if(_peer==null)return;
        _stopping=true;
        _peer.Close();
        _peer.Dispose();
        _peer=null;
        Connections.Clear();
        _closedPeerIds.Clear();
        _transportPeers.Clear();
        _commandGateway=null;
        _travelRuntime=null;
        _regionSessions?.Dispose();
        _regionSessions=null;
        _defaultCompany=null;
        _companySession=null;_mercenaries=null;_equipment=null;_loadouts=null;_clock=null;_developmentAccountBindings.Clear();_checkpointing=false;
        Port=0;
    }

    public void Dispose()=>Stop();

    private void HandlePacket(int peerId,byte[] payload)
    {
        if(_stopping||_checkpointing)return;
        if(_closedPeerIds.Contains(peerId))return;
        if(!_transportPeers.ContainsKey(peerId))
        {
            ENetPacketPeer? transportPeer=_peer?.GetPeer(peerId);
            if(transportPeer==null)return;
            _transportPeers.Add(peerId,transportPeer);
        }
        NetworkConnectionStateDataV3 connection=Connections.GetOrAddConnected(peerId);
        if(!NetworkProtocolV3.TryDeserialize(payload,out NetworkMessageV3? message)||message==null)
        {
            Send(peerId,Reject(NetworkMessageTypeV3.ProtocolRejected,0,NetworkRejectReasonV3.MalformedMessage));
            return;
        }
        if(message.ProtocolVersion!=NetworkProtocolV3.ProtocolVersion)
        {
            Diagnostics.RejectedProtocolMismatchCount++;
            if(message.MessageType==NetworkMessageTypeV3.ClientHello)Diagnostics.RejectedHandshakeCount++;
            Send(peerId,Reject(message.MessageType==NetworkMessageTypeV3.ClientHello?NetworkMessageTypeV3.ServerHelloRejected:NetworkMessageTypeV3.ProtocolRejected,message.RequestId,NetworkRejectReasonV3.ProtocolMismatch));
            return;
        }
        if(!Enum.IsDefined(message.MessageType))
        {
            Send(peerId,Reject(NetworkMessageTypeV3.ProtocolRejected,message.RequestId,NetworkRejectReasonV3.UnknownMessageType));
            return;
        }
        switch(message.MessageType)
        {
            case NetworkMessageTypeV3.ClientHello:HandleHello(connection,message);break;
            case NetworkMessageTypeV3.JoinRegionRequest:HandleJoin(connection,message);break;
            case NetworkMessageTypeV3.RequestInitialRegionSnapshot:HandleSnapshot(connection,message);break;
            case NetworkMessageTypeV3.SubmitGameplayCommand:
                if(_commandGateway==null)Send(peerId,Reject(NetworkMessageTypeV3.CommandRejected,message.RequestId,NetworkRejectReasonV3.ServerShuttingDown));
                else _commandGateway.Handle(connection,message);
                break;
            case NetworkMessageTypeV3.RequestRegionTransfer:
                if(_travelRuntime==null)Send(peerId,Reject(NetworkMessageTypeV3.RegionTransferRejected,message.RequestId,NetworkRejectReasonV3.ServerShuttingDown));
                else _travelRuntime.HandleTransfer(connection,message);
                break;
            case NetworkMessageTypeV3.ClientDisconnect:
                string previousRegion=connection.CurrentRegionId;
                if(!Connections.Remove(peerId)){Diagnostics.DisconnectCleanupFailureCount++;Diagnostics.DisconnectMembershipCleanupFailureCount++;}
                DeactivateEmptyRegion(previousRegion);
                _transportPeers.Remove(peerId);
                _closedPeerIds.Add(peerId);
                break;
            default:Send(peerId,Reject(NetworkMessageTypeV3.ProtocolRejected,message.RequestId,NetworkRejectReasonV3.UnknownMessageType));break;
        }
    }

    private void HandleHello(NetworkConnectionStateDataV3 connection,NetworkMessageV3 message)
    {
        if(string.IsNullOrWhiteSpace(message.DevelopmentPlayerAccountId))
        {
            Diagnostics.RejectedHandshakeCount++;
            Send(connection.PeerId,Reject(NetworkMessageTypeV3.ServerHelloRejected,message.RequestId,NetworkRejectReasonV3.EmptyDevelopmentPlayerAccountId));
            return;
        }
        string account=message.DevelopmentPlayerAccountId.Trim();
        if(Connections.IsDevelopmentAccountConnected(account,connection.PeerId))
        {
            Diagnostics.RejectedHandshakeCount++;
            Diagnostics.RejectedDuplicateAccountCount++;
            Send(connection.PeerId,Reject(NetworkMessageTypeV3.ServerHelloRejected,message.RequestId,NetworkRejectReasonV3.DuplicateAccountConnection));
            return;
        }
        if(connection.State>=NetworkConnectionStateV3.Handshaken||message.RequestId<=connection.LastApprovedRequestId)
        {
            Diagnostics.RejectedHandshakeCount++;
            Send(connection.PeerId,Reject(NetworkMessageTypeV3.ServerHelloRejected,message.RequestId,NetworkRejectReasonV3.DuplicateRequest));
            return;
        }
        if(!TryGetOrCreateDevelopmentCompany(account,out PlayerCompanyStateV3? company)||company==null)
        {
            Diagnostics.RejectedHandshakeCount++;
            Send(connection.PeerId,Reject(NetworkMessageTypeV3.ServerHelloRejected,message.RequestId,NetworkRejectReasonV3.NoCompanyAvailable));
            return;
        }
        string estateRegionId=GetEstateRegionId(company);
        Connections.MarkHandshaken(connection,account,company.PlayerAccountId,company.CompanyId,message.RequestId);
        TravelingGroupStateV3? traveling=World!.TravelingGroups.GetActiveForCompany(company.CompanyId);
        if(traveling!=null)_travelRuntime?.RestoreTravelingConnection(connection,traveling);
        Diagnostics.AcceptedHandshakeCount++;
        Send(connection.PeerId,new NetworkMessageV3{
            MessageType=NetworkMessageTypeV3.ServerHelloAccepted,
            RequestId=message.RequestId,
            PeerId=connection.PeerId,
            PlayerAccountId=company.PlayerAccountId,
            CompanyId=company.CompanyId,
            WorldId=World!.WorldId,
            RegionId=traveling==null?estateRegionId:string.Empty,
            TravelingGroupId=traveling?.TravelingGroupId??string.Empty,
            OriginRegionId=traveling?.OriginRegionId??string.Empty,
            DestinationRegionId=traveling?.DestinationRegionId??string.Empty,
            ArrivalWorldTime=traveling?.ArrivalWorldTime??0,
            ActiveSessionRevision=traveling==null?GameplaySessionV3.SessionRevision:0});
    }

    private void HandleJoin(NetworkConnectionStateDataV3 connection,NetworkMessageV3 message)
    {
        if(connection.State==NetworkConnectionStateV3.Traveling)
        {
            Send(connection.PeerId,Reject(NetworkMessageTypeV3.JoinRegionRejected,message.RequestId,NetworkRejectReasonV3.ConnectionAlreadyTraveling,message.RegionId));
            return;
        }
        if(connection.State<NetworkConnectionStateV3.Handshaken)
        {
            Diagnostics.RejectedPreHandshakeRequestCount++;
            Send(connection.PeerId,Reject(NetworkMessageTypeV3.JoinRegionRejected,message.RequestId,NetworkRejectReasonV3.HandshakeRequired,message.RegionId));
            return;
        }
        if(message.RequestId<=connection.LastApprovedRequestId)
        {
            Send(connection.PeerId,Reject(NetworkMessageTypeV3.JoinRegionRejected,message.RequestId,NetworkRejectReasonV3.DuplicateRequest,message.RegionId));
            return;
        }
        if(World==null||!World.TryGetRegion(message.RegionId,out RegionPersistentStateV3? region)||region==null)
        {
            Send(connection.PeerId,Reject(NetworkMessageTypeV3.JoinRegionRejected,message.RequestId,NetworkRejectReasonV3.UnknownRegion,message.RegionId));
            return;
        }
        if(!World.TryGetCompany(connection.CompanyId,out PlayerCompanyStateV3? company)||company==null)
        {
            Send(connection.PeerId,Reject(NetworkMessageTypeV3.JoinRegionRejected,message.RequestId,NetworkRejectReasonV3.NoCompanyAvailable,message.RegionId));
            return;
        }
        if(!_regionAccess.CanJoin(company,region))
        {
            if(region.RegionType==RegionTypeV3.PrivateEstate)Diagnostics.CrossCompanyEstateJoinRejectedCount++;
            Send(connection.PeerId,Reject(NetworkMessageTypeV3.JoinRegionRejected,message.RequestId,NetworkRejectReasonV3.RegionAccessDenied,message.RegionId));
            return;
        }
        if(_regionSessions==null||!_regionSessions.GetOrActivateRegion(region.RegionId,out ManagedRegionSessionV3? managed,out _)||managed==null)
        {
            Send(connection.PeerId,Reject(NetworkMessageTypeV3.JoinRegionRejected,message.RequestId,NetworkRejectReasonV3.UnknownRegion,message.RegionId));
            return;
        }
        string previousRegion=connection.CurrentRegionId;
        Connections.MarkJoined(connection,region.RegionId,message.RequestId);
        if(previousRegion!=region.RegionId)DeactivateEmptyRegion(previousRegion);
        Send(connection.PeerId,new NetworkMessageV3{
            MessageType=NetworkMessageTypeV3.JoinRegionAccepted,
            RequestId=message.RequestId,
            CompanyId=connection.CompanyId,
            WorldId=World.WorldId,
            RegionId=region.RegionId,
            RegionRevision=region.RegionRevision,
            ActiveSessionRevision=managed.Active.SessionRevision});
    }

    private void Send(int peerId,NetworkMessageV3 message)
    {
        if(_peer==null||_stopping)return;
        _peer.SetTargetPeer(peerId);
        _peer.PutPacket(NetworkProtocolV3.Serialize(message));
    }

    private void HandleSnapshot(NetworkConnectionStateDataV3 connection,NetworkMessageV3 message)
    {
        Diagnostics.SnapshotRequestCount++;
        if(connection.State!=NetworkConnectionStateV3.JoinedRegion)
        {
            Diagnostics.RejectedPreHandshakeRequestCount++;
            RejectSnapshot(connection,message,NetworkRejectReasonV3.SnapshotRequiresJoinedRegion);
            return;
        }
        if(message.RequestId<=connection.LastApprovedRequestId){RejectSnapshot(connection,message,NetworkRejectReasonV3.DuplicateRequest);return;}
        if(World==null||!World.TryGetRegion(message.RegionId,out RegionPersistentStateV3? region)||region==null){RejectSnapshot(connection,message,NetworkRejectReasonV3.UnknownRegion);return;}
        if(message.RegionId!=connection.CurrentRegionId){RejectSnapshot(connection,message,NetworkRejectReasonV3.SnapshotRegionMismatch);return;}
        if(!World.TryGetCompany(connection.CompanyId,out PlayerCompanyStateV3? company)||company==null){RejectSnapshot(connection,message,NetworkRejectReasonV3.NoCompanyAvailable);return;}
        if(!_regionAccess.CanView(company,region)){RejectSnapshot(connection,message,NetworkRejectReasonV3.RegionAccessDenied);return;}
        if(_regionSessions==null||!_regionSessions.TryGetActiveRegion(region.RegionId,out ManagedRegionSessionV3? managed)||managed==null||
           managed.Active.IsDisposed||message.ExpectedSessionRevision!=managed.Active.SessionRevision)
        {
            RejectSnapshot(connection,message,NetworkRejectReasonV3.SessionRevisionMismatch);
            return;
        }
        ActiveRegionSessionV3 active=managed.Active;
        if(!_snapshotBuilder.TryBuild(World,active,company,message.RequestId,SnapshotPayloadByteLimit,out string snapshotId,out string payload,out int payloadBytes,out NetworkRejectReasonV3 failure))
        {
            Diagnostics.SnapshotBuildFailureCount++;
            RejectSnapshot(connection,message,failure);
            return;
        }
        Connections.MarkInitialSnapshotApproved(connection,message.RequestId);
        Diagnostics.SnapshotAcceptedCount++;
        Diagnostics.SnapshotByteSize=payloadBytes;
        Send(connection.PeerId,new NetworkMessageV3{
            MessageType=NetworkMessageTypeV3.InitialRegionSnapshotAccepted,RequestId=message.RequestId,
            SnapshotId=snapshotId,WorldId=World.WorldId,WorldRevision=World.WorldRevision,WorldSeed=World.WorldSeed,
            GeneratorVersion=World.GeneratorVersion,WorldTime=World.WorldClock.ElapsedSimulationSeconds,
            CompanyId=company.CompanyId,RegionId=region.RegionId,RegionType=region.RegionType,
            RegionRevision=region.RegionRevision,ActiveSessionRevision=active.SessionRevision,SnapshotPayload=payload});
    }

    private void RejectSnapshot(NetworkConnectionStateDataV3 connection,NetworkMessageV3 message,NetworkRejectReasonV3 reason)
    {
        Diagnostics.SnapshotRejectedCount++;
        Send(connection.PeerId,new NetworkMessageV3{
            MessageType=NetworkMessageTypeV3.InitialRegionSnapshotRejected,RequestId=message.RequestId,
            RegionId=message.RegionId,RejectReason=reason,ActiveSessionRevision=GetRegionSessionRevision(message.RegionId)});
    }

    private static NetworkMessageV3 Reject(NetworkMessageTypeV3 type,long requestId,NetworkRejectReasonV3 reason,string regionId="")=>new(){
        MessageType=type,
        RequestId=requestId,
        RejectReason=reason,
        RegionId=regionId};

    public RegionPersistentStateV3 CreateOrGetSharedNeutralRegion()
    {
        if(World==null)throw new InvalidOperationException("Server world is unavailable.");
        if(World.TryGetRegion(PersistentWorldStateV3.SharedNeutralRegionId,out RegionPersistentStateV3? existing)&&existing!=null){EnsureEstateRoutes(existing.RegionId);return existing;}
        if(_regionSessions==null)throw new InvalidOperationException("Region session manager is unavailable.");
        RegionPersistentStateV3 created=_regionSessions.CreateRegion(PersistentWorldStateV3.SharedNeutralRegionId,RegionTypeV3.SharedNeutral,null,World.WorldSeed^0x51f15e);
        EnsureEstateRoutes(created.RegionId);
        return created;
    }

    public bool TryGetDevelopmentCompany(string accountId,out PlayerCompanyStateV3? company)=>_developmentAccountBindings.TryGetValue(accountId,out company);

    public string GetEstateRegionId(PlayerCompanyStateV3 company)
    {
        string? estate=company.OwnedRegionIds.OrderBy(x=>x,StringComparer.Ordinal)
            .FirstOrDefault(id=>World?.TryGetRegion(id,out RegionPersistentStateV3? region)==true&&region?.RegionType==RegionTypeV3.PrivateEstate);
        if(estate!=null)return estate;
        throw new InvalidOperationException("Company estate is unavailable.");
    }

    private bool TryGetOrCreateDevelopmentCompany(string account,out PlayerCompanyStateV3? company)
    {
        if(_developmentAccountBindings.TryGetValue(account,out company))return true;
        if(World==null){company=null;return false;}
        if(_developmentAccountBindings.Count==0)
        {
            company=_defaultCompany;
            if(company==null)return false;
            _developmentAccountBindings.Add(account,company);
            return true;
        }
        CompanySessionV3? companySession=_companySession;MercenarySessionV3? mercenaries=_mercenaries;EquipmentRuntimeV3? equipment=_equipment;EquipmentLoadoutRuntimeV3? loadouts=_loadouts;
        if(companySession==null||mercenaries==null||equipment==null||loadouts==null)
        {company=null;return false;}
        string playerId=CompanyIdFactoryV3.CreatePlayerId();
        if(!companySession.CompanyRegistry.TryCreateCompany(playerId,$"Company {_developmentAccountBindings.Count+1}",out CompanyStateV3? companyCore,out _)||companyCore==null)
        {company=null;return false;}
        company=new PlayerCompanyStateV3(companyCore,playerId,mercenaries,equipment,loadouts);
        if(!World.TryRegisterCompany(company,out _)){company=null;return false;}
        string estateId="region_estate_"+company.CompanyId[4..];
        if(_regionSessions==null){company=null;return false;}
        _regionSessions.CreateRegion(estateId,RegionTypeV3.PrivateEstate,company.CompanyId,World.WorldSeed^StableTextHash(company.CompanyId));
        if(World.TryGetRegion(PersistentWorldStateV3.SharedNeutralRegionId,out _))EnsureEstateRoute(estateId,PersistentWorldStateV3.SharedNeutralRegionId);
        _developmentAccountBindings.Add(account,company);
        return true;
    }

    private static int StableTextHash(string value)
    {
        unchecked
        {
            uint hash=2166136261;
            foreach(char character in value){hash^=character;hash*=16777619;}
            return (int)hash;
        }
    }

    private long GetRegionSessionRevision(string regionId)=>_regionSessions!=null&&_regionSessions.TryGetActiveRegion(regionId,out ManagedRegionSessionV3? managed)&&managed!=null?managed.Active.SessionRevision:0;

    private void DeactivateEmptyRegion(string regionId)
    {
        if(regionId.Length==0||Connections.GetRegionConnectionCount(regionId)>0||_regionSessions==null)return;
        _commandGateway?.ForgetRegion(regionId);
        _regionSessions.DeactivateRegion(regionId,out _);
    }

    internal void DeactivateRegionIfEmpty(string regionId)=>DeactivateEmptyRegion(regionId);
    private void EnsureEstateRoutes(string sharedRegionId)
    {
        foreach(RegionPersistentStateV3 region in World!.Regions.Values.Where(x=>x.RegionType==RegionTypeV3.PrivateEstate))EnsureEstateRoute(region.RegionId,sharedRegionId);
    }
    private void EnsureEstateRoute(string estateRegionId,string sharedRegionId)
    {
        string routeId="route_"+estateRegionId+"_shared";
        if(World!.WorldGraph.TryGetRoute(routeId,out _))return;
        World.WorldGraph.AddRoute(new(routeId,estateRegionId,sharedRegionId,true,200.0,true),out _);
    }
}
