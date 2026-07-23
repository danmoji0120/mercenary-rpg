using System;
using System.Text.Json;
using GameplayV3.Mercenary;

namespace GameplayV3.Network;

public enum GameplayCommandKindV3{MoveMercenary=1,DirectGather=2,CancelMercenaryOrder=3}
public sealed record GameplayCommandPayloadV3
{
    public GameplayCommandKindV3 CommandKind{get;init;}
    public string MercenaryId{get;init;}=string.Empty;
    public SnapshotCellV3? TargetCell{get;init;}
    public string ResourceNodeId{get;init;}=string.Empty;
}

public enum RegionDeltaEventKindV3
{
    MercenaryPositionChanged=1,
    MercenaryOrderStateChanged=2,
    ResourceNodeChanged=3,
    ResourceNodeRemoved=4,
    GroundResourceStackAdded=5,
    GroundResourceStackChanged=6,
    GroundResourceStackRemoved=7,
    MercenaryReplicaAdded=8,
    MercenaryReplicaRemoved=9,
    MercenaryPresenceChanged=10
}

public sealed record RegionDeltaEventV3
{
    public RegionDeltaEventKindV3 EventKind{get;init;}
    public string EntityId{get;init;}=string.Empty;
    public SnapshotCellV3? Cell{get;init;}
    public MercenaryActivityStateV3? ActivityState{get;init;}
    public ResourceNodeSnapshotDtoV3? ResourceNode{get;init;}
    public GroundResourceStackSnapshotDtoV3? GroundResourceStack{get;init;}
    public MercenarySnapshotDtoV3? Mercenary{get;init;}
    public EquipmentSnapshotDtoV3[] Equipment{get;init;}=Array.Empty<EquipmentSnapshotDtoV3>();
}

public sealed record RegionDeltaPayloadV3
{
    public RegionDeltaEventV3[] Events{get;init;}=Array.Empty<RegionDeltaEventV3>();
}

public static class GameplayCommandDeltaProtocolV3
{
    public const int MaximumCommandPayloadBytes=4096;
    public const int MaximumDeltaPayloadBytes=65536;
    private static readonly JsonSerializerOptions Options=RegionSnapshotProtocolV3.JsonOptions;

    public static string SerializeCommand(GameplayCommandPayloadV3 payload)=>JsonSerializer.Serialize(payload,Options);
    public static bool TryDeserializeCommand(string json,out GameplayCommandPayloadV3? payload)
    {
        payload=null;if(string.IsNullOrEmpty(json)||System.Text.Encoding.UTF8.GetByteCount(json)>MaximumCommandPayloadBytes)return false;
        try{payload=JsonSerializer.Deserialize<GameplayCommandPayloadV3>(json,Options);return payload!=null&&Enum.IsDefined(payload.CommandKind);}
        catch(JsonException){return false;}
    }
    public static string SerializeDelta(RegionDeltaPayloadV3 payload)=>JsonSerializer.Serialize(payload,Options);
    public static bool TryDeserializeDelta(string json,out RegionDeltaPayloadV3? payload)
    {
        payload=null;if(string.IsNullOrEmpty(json)||System.Text.Encoding.UTF8.GetByteCount(json)>MaximumDeltaPayloadBytes)return false;
        try{payload=JsonSerializer.Deserialize<RegionDeltaPayloadV3>(json,Options);return payload!=null;}
        catch(JsonException){return false;}
    }
}
