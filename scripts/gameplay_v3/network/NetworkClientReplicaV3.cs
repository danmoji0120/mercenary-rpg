using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GameplayV3.Network;

public sealed class NetworkClientReplicaDiagnosticsV3
{
    public int ClientSnapshotApplyCount{get;internal set;}
    public int ClientSnapshotRejectCount{get;internal set;}
    public long ClientReplicaRevision{get;internal set;}
    public int ClientReplicaEntityCount{get;internal set;}
    public int DuplicateReplicaIdCount{get;internal set;}
    public int InvalidReplicaReferenceCount{get;internal set;}
    public int PartialApplyCount{get;internal set;}
    public int ServerStateMutationFromClientCount{get;internal set;}
    public int DeltaBatchAppliedCount{get;internal set;}
    public int DeltaBatchRejectedCount{get;internal set;}
    public int DeltaSequenceGapCount{get;internal set;}
    public int ClientNeedsResnapshotCount{get;internal set;}
    public int PartialDeltaApplyCount{get;internal set;}
}

public sealed class NetworkRegionReplicaV3
{
    private NetworkRegionReplicaV3(
        NetworkMessageV3 envelope,InitialRegionSnapshotPayloadV3 payload,
        IReadOnlyDictionary<string,MercenarySnapshotDtoV3> mercenaries,
        IReadOnlyDictionary<string,ResourceNodeSnapshotDtoV3> nodes,
        IReadOnlyDictionary<string,GroundResourceStackSnapshotDtoV3> stacks,
        IReadOnlyDictionary<string,StructureSnapshotDtoV3> structures,
        IReadOnlyDictionary<string,BlueprintSnapshotDtoV3> blueprints,
        IReadOnlyDictionary<string,StockpileSnapshotDtoV3> stockpiles,
        IReadOnlyDictionary<string,ProductionFacilitySnapshotDtoV3> production,
        IReadOnlyDictionary<string,FarmPlotSnapshotDtoV3> farms,
        IReadOnlyDictionary<string,EquipmentSnapshotDtoV3> equipment)
    {
        SnapshotId=envelope.SnapshotId;WorldId=envelope.WorldId;WorldRevision=envelope.WorldRevision;WorldSeed=envelope.WorldSeed;
        GeneratorVersion=envelope.GeneratorVersion;WorldTime=envelope.WorldTime;CompanyId=envelope.CompanyId;RegionId=envelope.RegionId;
        RegionType=envelope.RegionType;RegionRevision=envelope.RegionRevision;ActiveSessionRevision=envelope.ActiveSessionRevision;
        OwnerCompanyId=payload.OwnerCompanyId;TerrainSeed=payload.TerrainSeed;
        Mercenaries=mercenaries;ResourceNodes=nodes;GroundResourceStacks=stacks;Structures=structures;Blueprints=blueprints;
        Stockpiles=stockpiles;ProductionFacilities=production;FarmPlots=farms;Equipment=equipment;
    }

    public string SnapshotId{get;} public string WorldId{get;} public long WorldRevision{get;} public int WorldSeed{get;}
    public string GeneratorVersion{get;} public double WorldTime{get;} public string CompanyId{get;} public string RegionId{get;}
    public GameplayV3.Session.RegionTypeV3 RegionType{get;} public long RegionRevision{get;} public long ActiveSessionRevision{get;}
    public string OwnerCompanyId{get;} public int TerrainSeed{get;}
    public IReadOnlyDictionary<string,MercenarySnapshotDtoV3> Mercenaries{get;}
    public IReadOnlyDictionary<string,ResourceNodeSnapshotDtoV3> ResourceNodes{get;}
    public IReadOnlyDictionary<string,GroundResourceStackSnapshotDtoV3> GroundResourceStacks{get;}
    public IReadOnlyDictionary<string,StructureSnapshotDtoV3> Structures{get;}
    public IReadOnlyDictionary<string,BlueprintSnapshotDtoV3> Blueprints{get;}
    public IReadOnlyDictionary<string,StockpileSnapshotDtoV3> Stockpiles{get;}
    public IReadOnlyDictionary<string,ProductionFacilitySnapshotDtoV3> ProductionFacilities{get;}
    public IReadOnlyDictionary<string,FarmPlotSnapshotDtoV3> FarmPlots{get;}
    public IReadOnlyDictionary<string,EquipmentSnapshotDtoV3> Equipment{get;}
    public int EntityCount=>Mercenaries.Count+ResourceNodes.Count+GroundResourceStacks.Count+Structures.Count+Blueprints.Count+Stockpiles.Count+ProductionFacilities.Count+FarmPlots.Count+Equipment.Count;

    internal static bool TryCreate(NetworkMessageV3 envelope,InitialRegionSnapshotPayloadV3 payload,out NetworkRegionReplicaV3? replica,out bool duplicate,out bool invalidReference)
    {
        replica=null;duplicate=false;invalidReference=false;
        if(!TryIndex(payload.Mercenaries,x=>x.MercenaryId,out var mercenaries)||!TryIndex(payload.ResourceNodes,x=>x.ResourceNodeId,out var nodes)||
           !TryIndex(payload.GroundResourceStacks,x=>x.ResourceStackId,out var stacks)||!TryIndex(payload.Structures,x=>x.StructureId,out var structures)||
           !TryIndex(payload.Blueprints,x=>x.BlueprintId,out var blueprints)||!TryIndex(payload.Stockpiles,x=>x.StockpileId,out var stockpiles)||
           !TryIndex(payload.ProductionFacilities,x=>x.FacilityId,out var production)||!TryIndex(payload.FarmPlots,x=>x.FarmPlotId,out var farms)||
           !TryIndex(payload.Equipment,x=>x.EquipmentInstanceId,out var equipment)){duplicate=true;return false;}
        if(structures.Keys.Any(blueprints.ContainsKey)){duplicate=true;return false;}

        foreach(MercenarySnapshotDtoV3 mercenary in mercenaries.Values)
        {
            bool ownershipValid=mercenary.IsOwnedByRecipient?mercenary.CompanyId==envelope.CompanyId:mercenary.CompanyId!=envelope.CompanyId;
            bool privacyValid=mercenary.IsOwnedByRecipient||
                mercenary.Hunger==0&&mercenary.Fatigue==0&&mercenary.Strength==0&&mercenary.Agility==0&&mercenary.Endurance==0&&mercenary.Intelligence==0&&mercenary.Mental==0&&
                mercenary.Hauling==0&&mercenary.Construction==0&&mercenary.Gathering==0&&mercenary.Farming==0&&mercenary.Production==0&&mercenary.Medicine==0&&mercenary.Guarding==0;
            if(!ownershipValid||!privacyValid||!ValidLoadout(mercenary.MainHandEquipmentInstanceId,mercenary.MercenaryId,GameplayV3.Equipment.EquipmentSlotV3.MainHand)||
               !ValidLoadout(mercenary.ArmorEquipmentInstanceId,mercenary.MercenaryId,GameplayV3.Equipment.EquipmentSlotV3.Armor)||
               !ValidLoadout(mercenary.ToolEquipmentInstanceId,mercenary.MercenaryId,GameplayV3.Equipment.EquipmentSlotV3.Tool))
            {invalidReference=true;return false;}
        }
        foreach(EquipmentSnapshotDtoV3 item in equipment.Values)
        {
            bool valid=item.LocationKind switch{
                GameplayV3.Equipment.EquipmentLocationKindV3.Ground=>item.GroundCell!=null&&item.StorageId==null&&item.FacilityId==null&&item.EquippedMercenaryId==null,
                GameplayV3.Equipment.EquipmentLocationKindV3.Storage=>item.StorageId!=null&&item.StorageCell!=null&&stockpiles.TryGetValue(item.StorageId,out StockpileSnapshotDtoV3? storage)&&storage.CompanyId==envelope.CompanyId&&item.GroundCell==null&&item.FacilityId==null&&item.EquippedMercenaryId==null,
                GameplayV3.Equipment.EquipmentLocationKindV3.FacilityOutput=>item.FacilityId!=null&&production.ContainsKey(item.FacilityId)&&item.GroundCell==null&&item.StorageId==null&&item.EquippedMercenaryId==null,
                GameplayV3.Equipment.EquipmentLocationKindV3.Equipped=>item.EquippedMercenaryId!=null&&mercenaries.TryGetValue(item.EquippedMercenaryId,out MercenarySnapshotDtoV3? equippedMercenary)&&equippedMercenary.CompanyId==item.OwnerCompanyId&&item.EquippedSlot!=null&&item.GroundCell==null&&item.StorageId==null&&item.FacilityId==null,
                _=>false};
            if(item.OwnerCompanyId!=envelope.CompanyId&&item.LocationKind is not GameplayV3.Equipment.EquipmentLocationKindV3.Ground and not GameplayV3.Equipment.EquipmentLocationKindV3.Equipped)valid=false;
            if(item.OwnerCompanyId!=envelope.CompanyId&&(item.CrafterMercenaryId.Length!=0||item.CrafterProductionSkillSnapshot!=0))valid=false;
            if(!valid){invalidReference=true;return false;}
        }
        HashSet<string> facilityOutputIds=new(StringComparer.Ordinal);
        foreach(ProductionFacilitySnapshotDtoV3 facility in production.Values)
            foreach(string id in facility.FacilityOutputEquipmentInstanceIds)
                if(!facilityOutputIds.Add(id)||!equipment.TryGetValue(id,out EquipmentSnapshotDtoV3? item)||item.LocationKind!=GameplayV3.Equipment.EquipmentLocationKindV3.FacilityOutput||item.FacilityId!=facility.FacilityId){invalidReference=true;return false;}
        if(equipment.Values.Any(x=>x.LocationKind==GameplayV3.Equipment.EquipmentLocationKindV3.FacilityOutput&&!facilityOutputIds.Contains(x.EquipmentInstanceId))){invalidReference=true;return false;}
        replica=new(envelope,payload,ReadOnly(mercenaries),ReadOnly(nodes),ReadOnly(stacks),ReadOnly(structures),ReadOnly(blueprints),ReadOnly(stockpiles),ReadOnly(production),ReadOnly(farms),ReadOnly(equipment));
        return true;

        bool ValidLoadout(string? id,string mercenaryId,GameplayV3.Equipment.EquipmentSlotV3 slot)=>id==null||(equipment.TryGetValue(id,out EquipmentSnapshotDtoV3? item)&&item.LocationKind==GameplayV3.Equipment.EquipmentLocationKindV3.Equipped&&item.EquippedMercenaryId==mercenaryId&&item.EquippedSlot==slot);
    }

    internal bool TryApplyDelta(NetworkMessageV3 envelope,RegionDeltaPayloadV3 payload,out NetworkRegionReplicaV3? replica)
    {
        replica=null;
        Dictionary<string,MercenarySnapshotDtoV3> mercenaries=Mercenaries.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
        Dictionary<string,ResourceNodeSnapshotDtoV3> nodes=ResourceNodes.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
        Dictionary<string,GroundResourceStackSnapshotDtoV3> stacks=GroundResourceStacks.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
        Dictionary<string,EquipmentSnapshotDtoV3> equipment=Equipment.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
        foreach(RegionDeltaEventV3 change in payload.Events)
        {
            if(string.IsNullOrWhiteSpace(change.EntityId))return false;
            switch(change.EventKind)
            {
                case RegionDeltaEventKindV3.MercenaryPositionChanged:
                    if(change.Cell==null||!mercenaries.TryGetValue(change.EntityId,out MercenarySnapshotDtoV3? moving))return false;
                    mercenaries[change.EntityId]=moving with{Cell=change.Cell};break;
                case RegionDeltaEventKindV3.MercenaryOrderStateChanged:
                    if(change.ActivityState==null||!mercenaries.TryGetValue(change.EntityId,out MercenarySnapshotDtoV3? working))return false;
                    mercenaries[change.EntityId]=working with{ActivityState=change.ActivityState.Value};break;
                case RegionDeltaEventKindV3.ResourceNodeChanged:
                    if(change.ResourceNode==null||change.ResourceNode.ResourceNodeId!=change.EntityId||!nodes.ContainsKey(change.EntityId))return false;
                    nodes[change.EntityId]=change.ResourceNode;break;
                case RegionDeltaEventKindV3.ResourceNodeRemoved:
                    if(!nodes.Remove(change.EntityId))return false;break;
                case RegionDeltaEventKindV3.GroundResourceStackAdded:
                    if(change.GroundResourceStack==null||change.GroundResourceStack.ResourceStackId!=change.EntityId||stacks.ContainsKey(change.EntityId))return false;
                    stacks.Add(change.EntityId,change.GroundResourceStack);break;
                case RegionDeltaEventKindV3.GroundResourceStackChanged:
                    if(change.GroundResourceStack==null||change.GroundResourceStack.ResourceStackId!=change.EntityId||!stacks.ContainsKey(change.EntityId))return false;
                    stacks[change.EntityId]=change.GroundResourceStack;break;
                case RegionDeltaEventKindV3.GroundResourceStackRemoved:
                    if(!stacks.Remove(change.EntityId))return false;break;
                case RegionDeltaEventKindV3.MercenaryReplicaAdded:
                    if(change.Mercenary==null||change.Mercenary.MercenaryId!=change.EntityId||mercenaries.ContainsKey(change.EntityId))return false;
                    foreach(EquipmentSnapshotDtoV3 item in change.Equipment)
                        if(string.IsNullOrWhiteSpace(item.EquipmentInstanceId)||equipment.ContainsKey(item.EquipmentInstanceId)||item.LocationKind!=GameplayV3.Equipment.EquipmentLocationKindV3.Equipped||item.EquippedMercenaryId!=change.EntityId)return false;
                    mercenaries.Add(change.EntityId,change.Mercenary);
                    foreach(EquipmentSnapshotDtoV3 item in change.Equipment)equipment.Add(item.EquipmentInstanceId,item);
                    if(!ValidAddedLoadout(change.Mercenary,equipment))return false;
                    break;
                case RegionDeltaEventKindV3.MercenaryReplicaRemoved:
                    if(!mercenaries.Remove(change.EntityId))return false;
                    foreach(string equipmentId in equipment.Values.Where(x=>x.EquippedMercenaryId==change.EntityId).Select(x=>x.EquipmentInstanceId).ToArray())equipment.Remove(equipmentId);
                    break;
                case RegionDeltaEventKindV3.MercenaryPresenceChanged:
                    if(change.Mercenary==null||change.Mercenary.MercenaryId!=change.EntityId||!mercenaries.ContainsKey(change.EntityId))return false;
                    mercenaries[change.EntityId]=change.Mercenary;break;
                default:return false;
            }
        }
        NetworkMessageV3 metadata=new(){
            SnapshotId=SnapshotId,WorldId=WorldId,WorldRevision=WorldRevision,WorldSeed=WorldSeed,GeneratorVersion=GeneratorVersion,WorldTime=WorldTime,
            CompanyId=CompanyId,RegionId=RegionId,RegionType=RegionType,RegionRevision=envelope.RegionRevision,ActiveSessionRevision=ActiveSessionRevision};
        InitialRegionSnapshotPayloadV3 persistent=new(){OwnerCompanyId=OwnerCompanyId,TerrainSeed=TerrainSeed};
        replica=new(metadata,persistent,ReadOnly(mercenaries),ReadOnly(nodes),ReadOnly(stacks),Structures,Blueprints,Stockpiles,ProductionFacilities,FarmPlots,ReadOnly(equipment));
        return true;
    }

    private static bool ValidAddedLoadout(MercenarySnapshotDtoV3 mercenary,IReadOnlyDictionary<string,EquipmentSnapshotDtoV3> equipment)
    {
        return Valid(mercenary.MainHandEquipmentInstanceId,GameplayV3.Equipment.EquipmentSlotV3.MainHand)&&
               Valid(mercenary.ArmorEquipmentInstanceId,GameplayV3.Equipment.EquipmentSlotV3.Armor)&&
               Valid(mercenary.ToolEquipmentInstanceId,GameplayV3.Equipment.EquipmentSlotV3.Tool);
        bool Valid(string? id,GameplayV3.Equipment.EquipmentSlotV3 slot)=>id==null||equipment.TryGetValue(id,out EquipmentSnapshotDtoV3? item)&&item.EquippedMercenaryId==mercenary.MercenaryId&&item.EquippedSlot==slot;
    }

    private static bool TryIndex<T>(IEnumerable<T> values,Func<T,string> id,out Dictionary<string,T> result)
    {
        result=new(StringComparer.Ordinal);
        foreach(T value in values){string key=id(value);if(string.IsNullOrWhiteSpace(key)||!result.TryAdd(key,value))return false;}
        return true;
    }
    private static IReadOnlyDictionary<string,T> ReadOnly<T>(Dictionary<string,T> values)=>new ReadOnlyDictionary<string,T>(values);
}

public sealed class NetworkClientSessionV3
{
    public string PlayerAccountId{get;private set;}=string.Empty;
    public string CompanyId{get;private set;}=string.Empty;
    public string WorldId{get;private set;}=string.Empty;
    public string CurrentRegionId{get;private set;}=string.Empty;
    public long CurrentSessionRevision{get;private set;}
    public long CurrentDeltaSequence{get;private set;}
    public bool NeedsResnapshot{get;private set;}
    public bool IsTraveling{get;private set;}
    public string CurrentTravelingGroupId{get;private set;}=string.Empty;
    public string LastDeltaFailureReason{get;private set;}=string.Empty;
    public NetworkRegionReplicaV3? CurrentRegionReplica{get;private set;}
    public NetworkClientReplicaDiagnosticsV3 Diagnostics{get;}=new();

    public bool ApplyServerHello(NetworkMessageV3 message)
    {
        if(message.ProtocolVersion!=NetworkProtocolV3.ProtocolVersion||message.MessageType!=NetworkMessageTypeV3.ServerHelloAccepted)return false;
        PlayerAccountId=message.PlayerAccountId;CompanyId=message.CompanyId;WorldId=message.WorldId;CurrentRegionId=message.RegionId;CurrentSessionRevision=message.ActiveSessionRevision;
        CurrentTravelingGroupId=message.TravelingGroupId;IsTraveling=CurrentTravelingGroupId.Length>0;return true;
    }
    public bool ApplyRegionJoin(NetworkMessageV3 message)
    {
        if(message.ProtocolVersion!=NetworkProtocolV3.ProtocolVersion||message.MessageType!=NetworkMessageTypeV3.JoinRegionAccepted||message.WorldId!=WorldId||message.CompanyId!=CompanyId)return false;
        CurrentRegionId=message.RegionId;CurrentSessionRevision=message.ActiveSessionRevision;CurrentTravelingGroupId=string.Empty;IsTraveling=false;CurrentRegionReplica=null;CurrentDeltaSequence=0;NeedsResnapshot=false;return true;
    }
    public bool ApplyRegionTransferAccepted(NetworkMessageV3 message)
    {
        if(message.ProtocolVersion!=NetworkProtocolV3.ProtocolVersion||message.MessageType!=NetworkMessageTypeV3.RegionTransferAccepted||string.IsNullOrWhiteSpace(message.TravelingGroupId)||message.OriginRegionId!=CurrentRegionId)return false;
        CurrentTravelingGroupId=message.TravelingGroupId;IsTraveling=true;CurrentRegionId=string.Empty;CurrentSessionRevision=0;CurrentRegionReplica=null;CurrentDeltaSequence=0;NeedsResnapshot=false;return true;
    }
    public bool ApplyRegionTransferArrived(NetworkMessageV3 message)
    {
        if(message.ProtocolVersion!=NetworkProtocolV3.ProtocolVersion||message.MessageType!=NetworkMessageTypeV3.RegionTransferArrived||!IsTraveling||message.TravelingGroupId!=CurrentTravelingGroupId)return false;
        CurrentTravelingGroupId=string.Empty;IsTraveling=false;CurrentRegionId=message.DestinationRegionId;CurrentSessionRevision=message.ActiveSessionRevision;CurrentRegionReplica=null;CurrentDeltaSequence=0;NeedsResnapshot=false;return true;
    }
    public bool TryApplyInitialSnapshot(NetworkMessageV3 message,out NetworkRejectReasonV3 reason)
    {
        reason=NetworkRejectReasonV3.InvalidSnapshot;
        if(message.ProtocolVersion!=NetworkProtocolV3.ProtocolVersion||message.MessageType!=NetworkMessageTypeV3.InitialRegionSnapshotAccepted||
           string.IsNullOrWhiteSpace(message.SnapshotId)||message.WorldId!=WorldId||message.CompanyId!=CompanyId||message.RegionId!=CurrentRegionId||message.ActiveSessionRevision!=CurrentSessionRevision||
           (CurrentRegionReplica!=null&&(message.RegionRevision<CurrentRegionReplica.RegionRevision||message.WorldRevision<CurrentRegionReplica.WorldRevision)))
        {Diagnostics.ClientSnapshotRejectCount++;return false;}
        if(!RegionSnapshotProtocolV3.TryDeserialize(message.SnapshotPayload,out InitialRegionSnapshotPayloadV3? payload)||payload==null)
        {Diagnostics.ClientSnapshotRejectCount++;return false;}
        if(message.RegionType==GameplayV3.Session.RegionTypeV3.PrivateEstate&&payload.OwnerCompanyId!=CompanyId)
        {Diagnostics.ClientSnapshotRejectCount++;return false;}
        if(!NetworkRegionReplicaV3.TryCreate(message,payload,out NetworkRegionReplicaV3? next,out bool duplicate,out bool invalidReference)||next==null)
        {
            Diagnostics.ClientSnapshotRejectCount++;
            if(duplicate)Diagnostics.DuplicateReplicaIdCount++;
            if(invalidReference)Diagnostics.InvalidReplicaReferenceCount++;
            return false;
        }
        CurrentRegionReplica=next;
        CurrentDeltaSequence=0;NeedsResnapshot=false;LastDeltaFailureReason=string.Empty;
        Diagnostics.ClientSnapshotApplyCount++;
        Diagnostics.ClientReplicaRevision=next.RegionRevision;
        Diagnostics.ClientReplicaEntityCount=next.EntityCount;
        reason=NetworkRejectReasonV3.None;
        return true;
    }
    public bool TryApplyRegionDelta(NetworkMessageV3 message,out string reason)
    {
        reason=string.Empty;
        if(message.ProtocolVersion!=NetworkProtocolV3.ProtocolVersion||message.MessageType!=NetworkMessageTypeV3.RegionDeltaBatch||
           CurrentRegionReplica==null||message.RegionId!=CurrentRegionId||message.ActiveSessionRevision!=CurrentSessionRevision)
            return RejectDelta("DeltaContextMismatch",out reason);
        if(message.DeltaSequence<=CurrentDeltaSequence)return true;
        if(message.DeltaSequence!=CurrentDeltaSequence+1)
        {
            Diagnostics.DeltaSequenceGapCount++;
            return RejectDelta("DeltaSequenceGap",out reason);
        }
        if(!GameplayCommandDeltaProtocolV3.TryDeserializeDelta(message.DeltaPayload,out RegionDeltaPayloadV3? payload)||payload==null||
           !CurrentRegionReplica.TryApplyDelta(message,payload,out NetworkRegionReplicaV3? next)||next==null)
            return RejectDelta("InvalidDeltaEvent",out reason);
        CurrentRegionReplica=next;CurrentDeltaSequence=message.DeltaSequence;
        Diagnostics.DeltaBatchAppliedCount++;Diagnostics.ClientReplicaRevision=next.RegionRevision;Diagnostics.ClientReplicaEntityCount=next.EntityCount;
        return true;
    }
    private bool RejectDelta(string failure,out string reason)
    {
        reason=failure;LastDeltaFailureReason=failure;NeedsResnapshot=true;
        Diagnostics.DeltaBatchRejectedCount++;Diagnostics.ClientNeedsResnapshotCount++;
        return false;
    }
    public void ClearConnection()
    {
        PlayerAccountId=CompanyId=WorldId=CurrentRegionId=CurrentTravelingGroupId=string.Empty;CurrentSessionRevision=0;CurrentDeltaSequence=0;CurrentRegionReplica=null;NeedsResnapshot=false;IsTraveling=false;LastDeltaFailureReason=string.Empty;
    }
}
