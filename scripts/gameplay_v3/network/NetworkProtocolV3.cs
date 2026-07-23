using System;
using System.Text.Json;
using GameplayV3.Session;

namespace GameplayV3.Network;

public enum NetworkMessageTypeV3
{
    ClientHello=1,
    ServerHelloAccepted=2,
    ServerHelloRejected=3,
    JoinRegionRequest=4,
    JoinRegionAccepted=5,
    JoinRegionRejected=6,
    ProtocolRejected=7,
    ClientDisconnect=8,
    RequestInitialRegionSnapshot=9,
    InitialRegionSnapshotAccepted=10,
    InitialRegionSnapshotRejected=11,
    SubmitGameplayCommand=12,
    CommandAccepted=13,
    CommandRejected=14,
    RegionDeltaBatch=15,
    DeltaResyncRequired=16,
    RequestRegionTransfer=17,
    RegionTransferAccepted=18,
    RegionTransferRejected=19,
    RegionTransferArrived=20
}

public enum NetworkRejectReasonV3
{
    None=0,
    ProtocolMismatch=1,
    UnknownMessageType=2,
    EmptyDevelopmentPlayerAccountId=3,
    HandshakeRequired=4,
    DuplicateAccountConnection=5,
    UnknownRegion=6,
    RegionAccessDenied=7,
    DuplicateRequest=8,
    UnknownPeer=9,
    NoCompanyAvailable=10,
    ServerStopping=11,
    MalformedMessage=12,
    SnapshotRequiresJoinedRegion=13,
    SnapshotRegionMismatch=14,
    SessionRevisionMismatch=15,
    SnapshotTooLarge=16,
    SnapshotBuildFailed=17,
    InvalidSnapshot=18,
    RegionJoinRequired=19,
    InitialSnapshotRequired=20,
    WrongRegion=21,
    StaleSession=22,
    MercenaryNotFound=23,
    MercenaryNotOwned=24,
    MercenaryNotPresent=25,
    TargetNotFound=26,
    InvalidTarget=27,
    CommandSequenceStale=28,
    DuplicateCommand=29,
    ServerShuttingDown=30,
    ResourceDepleted=31,
    WrongOriginRegion=32,
    RouteNotFound=33,
    RouteDisabled=34,
    DestinationAccessDenied=35,
    MercenaryAlreadyTraveling=36,
    MercenaryBusy=37,
    DuplicateMercenary=38,
    EmptyGroup=39,
    ConnectionAlreadyTraveling=40,
    DuplicateTransferRequest=41,
    TravelingCommandBlocked=42,
    ArrivalPlacementBlocked=43
}

public sealed record NetworkMessageV3
{
    public int ProtocolVersion{get;init;}=NetworkProtocolV3.ProtocolVersion;
    public NetworkMessageTypeV3 MessageType{get;init;}
    public long RequestId{get;init;}
    public string DevelopmentPlayerAccountId{get;init;}=string.Empty;
    public string ClientBuild{get;init;}=string.Empty;
    public int PeerId{get;init;}
    public string PlayerAccountId{get;init;}=string.Empty;
    public string CompanyId{get;init;}=string.Empty;
    public string WorldId{get;init;}=string.Empty;
    public string RegionId{get;init;}=string.Empty;
    public long RegionRevision{get;init;}
    public long ActiveSessionRevision{get;init;}
    public long ExpectedSessionRevision{get;init;}
    public string SnapshotId{get;init;}=string.Empty;
    public long WorldRevision{get;init;}
    public int WorldSeed{get;init;}
    public string GeneratorVersion{get;init;}=string.Empty;
    public double WorldTime{get;init;}
    public RegionTypeV3 RegionType{get;init;}
    public string SnapshotPayload{get;init;}=string.Empty;
    public long ClientCommandSequence{get;init;}
    public string ServerCommandId{get;init;}=string.Empty;
    public long AcceptedServerTick{get;init;}
    public string CommandPayload{get;init;}=string.Empty;
    public long DeltaSequence{get;init;}
    public long ServerTick{get;init;}
    public string DeltaPayload{get;init;}=string.Empty;
    public string OriginRegionId{get;init;}=string.Empty;
    public string DestinationRegionId{get;init;}=string.Empty;
    public string RouteId{get;init;}=string.Empty;
    public string[] MercenaryIds{get;init;}=Array.Empty<string>();
    public string TravelingGroupId{get;init;}=string.Empty;
    public double DepartureWorldTime{get;init;}
    public double ArrivalWorldTime{get;init;}
    public long TravelingGroupRevision{get;init;}
    public SnapshotCellV3? EntryCell{get;init;}
    public NetworkRejectReasonV3 RejectReason{get;init;}
}

public static class NetworkProtocolV3
{
    public const int ProtocolVersion=1;
    public const int MaximumNetworkMessageBytes=RegionSnapshotProtocolV3.MaximumSnapshotPayloadBytes+8192;
    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};

    public static byte[] Serialize(NetworkMessageV3 message)=>JsonSerializer.SerializeToUtf8Bytes(message,JsonOptions);

    public static bool TryDeserialize(byte[] payload,out NetworkMessageV3? message)
    {
        message=null;
        if(payload.Length is 0||payload.Length>MaximumNetworkMessageBytes)return false;
        try{message=JsonSerializer.Deserialize<NetworkMessageV3>(payload,JsonOptions);return message!=null;}
        catch(JsonException){return false;}
    }
}
