using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Godot;

namespace GameplayV3.Equipment;

public enum EquipmentReservationPurposeV3 { Hauling = 1, Equip = 2 }
public sealed record EquipmentReservationV3(string EquipmentInstanceId,string WorkRequestId,string MercenaryId,EquipmentReservationPurposeV3 Purpose,long SessionRevision);
public sealed record EquipmentRuntimeInvariantSnapshotV3(
    int InstanceCount,
    int FacilityOutputCount,
    int GroundCount,
    int StorageCount,
    int EquippedCount,
    int CompanyHoldingCount,
    int ActiveReservationCount,
    int LocationInvariantViolationCount,
    int DuplicateIndexViolationCount,
    int OrphanInstanceCount);

public sealed class EquipmentRuntimeV3 : IDisposable
{
    public const int FacilityOutputCapacity = 4;
    private readonly Dictionary<string, EquipmentInstanceV3> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<string>> _idsByCompany = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<string>> _holdingIdsByCompany = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RegionEquipmentLocationStoreV3> _locationStoresByRegion = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EquipmentReservationV3> _reservationsByInstance = new(StringComparer.Ordinal);
    private RegionEquipmentLocationStoreV3 _activeLocations;
    private long _nextInstanceSequence = 1;

    public EquipmentRuntimeV3(long sessionRevision, EquipmentDefinitionRegistryV3 definitions)
    {
        if (sessionRevision < 1) throw new ArgumentOutOfRangeException(nameof(sessionRevision));
        ArgumentNullException.ThrowIfNull(definitions);
        if (!definitions.IsSealed) throw new ArgumentException("Equipment definitions must be sealed.", nameof(definitions));
        SessionRevision = sessionRevision;
        Definitions = definitions;
        _activeLocations = new RegionEquipmentLocationStoreV3("region_legacy_single");
        _locationStoresByRegion.Add(_activeLocations.RegionId, _activeLocations);
    }

    private Dictionary<string,List<string>> _outputIdsByFacility=>_activeLocations.OutputIdsByFacility;
    private Dictionary<Vector2I,SortedSet<string>> _groundIdsByCell=>_activeLocations.GroundIdsByCell;
    private Dictionary<string,SortedSet<string>> _groundIdsByCompany=>_activeLocations.GroundIdsByCompany;
    private Dictionary<string,SortedSet<string>> _storageIdsByZone=>_activeLocations.StorageIdsByZone;
    private Dictionary<Vector2I,SortedSet<string>> _storageIdsByCell=>_activeLocations.StorageIdsByCell;
    private Dictionary<string,SortedSet<string>> _storedIdsByCompany=>_activeLocations.StoredIdsByCompany;

    public long SessionRevision { get; private set; }
    public string ActiveRegionId=>_activeLocations.RegionId;
    public EquipmentDefinitionRegistryV3 Definitions { get; }
    public int Count => _byId.Count;
    public long NextInstanceSequence=>_nextInstanceSequence;
    public long Revision { get; private set; }
    public bool IsDisposed { get; private set; }
    public int ActiveReservationCount => IsDisposed ? 0 : _reservationsByInstance.Count;

    public void AttachRegionLocationStore(RegionEquipmentLocationStoreV3 store)
    {
        if(IsDisposed)throw new ObjectDisposedException(nameof(EquipmentRuntimeV3));
        ArgumentNullException.ThrowIfNull(store);
        if(_locationStoresByRegion.TryGetValue(store.RegionId,out RegionEquipmentLocationStoreV3? existing)&&!ReferenceEquals(existing,store))
            throw new InvalidOperationException($"A different equipment location store is already registered for region '{store.RegionId}'.");
        _locationStoresByRegion[store.RegionId]=store;
        _activeLocations=store;
    }

    public void RebindSessionRevision(long sessionRevision)
    {
        if(IsDisposed)throw new ObjectDisposedException(nameof(EquipmentRuntimeV3));
        if(sessionRevision<1)throw new ArgumentOutOfRangeException(nameof(sessionRevision));
        ClearReservations();
        SessionRevision=sessionRevision;
    }

    public bool TryCreateInstance(
        string equipmentDefinitionId,
        EquipmentQualityV3 quality,
        int qualityScore,
        string crafterMercenaryId,
        int crafterProductionSkillSnapshot,
        string ownerCompanyId,
        long createdSessionRevision,
        out EquipmentInstanceV3? instance,
        out string reason)
    {
        instance = null;
        if (IsDisposed)
        {
            reason = "Equipment runtime is disposed.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(equipmentDefinitionId) || !Definitions.Contains(equipmentDefinitionId))
        {
            reason = "Unknown equipment definition.";
            return false;
        }
        if (!Enum.IsDefined(quality) || qualityScore < 0 || qualityScore > EquipmentQualityResolverV3.MaximumQualityScore ||
            EquipmentQualityResolverV3.GetQualityForScore(qualityScore) != quality)
        {
            reason = "Equipment quality or quality score is invalid.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(crafterMercenaryId) ||
            crafterProductionSkillSnapshot < EquipmentQualityResolverV3.MinimumProductionSkill ||
            crafterProductionSkillSnapshot > EquipmentQualityResolverV3.MaximumProductionSkill)
        {
            reason = "Equipment crafter data is invalid.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(ownerCompanyId))
        {
            reason = "Equipment owner company is required.";
            return false;
        }
        if (createdSessionRevision != SessionRevision)
        {
            reason = "Equipment session revision is invalid.";
            return false;
        }

        string instanceId = CreateNextInstanceId();
        instance = new EquipmentInstanceV3(
            instanceId,
            equipmentDefinitionId,
            quality,
            qualityScore,
            crafterMercenaryId,
            crafterProductionSkillSnapshot,
            ownerCompanyId,
            createdSessionRevision);
        _byId.Add(instanceId, instance);
        if (!_idsByCompany.TryGetValue(ownerCompanyId, out SortedSet<string>? ids))
        {
            ids = new(StringComparer.Ordinal);
            _idsByCompany.Add(ownerCompanyId, ids);
        }
        ids.Add(instanceId);
        reason = string.Empty;
        return true;
    }

    public IReadOnlyList<string> GetAllInstanceIds()
    {
        List<string> ids=new(_byId.Keys);ids.Sort(StringComparer.Ordinal);return new ReadOnlyCollection<string>(ids);
    }

    internal bool TryRestoreInstance(
        string instanceId,string definitionId,EquipmentQualityV3 quality,int qualityScore,string crafterMercenaryId,
        int crafterProductionSkill,string ownerCompanyId,long createdSessionRevision,EquipmentLocationKindV3 locationKind,
        string? regionId,Vector2I? groundCell,string? storageId,Vector2I? storageCell,string? facilityId,
        string? equippedMercenaryId,EquipmentSlotV3? equippedSlot,out string reason)
    {
        if(IsDisposed||string.IsNullOrWhiteSpace(instanceId)||_byId.ContainsKey(instanceId)||!Definitions.Contains(definitionId)||
           !Enum.IsDefined(quality)||EquipmentQualityResolverV3.GetQualityForScore(qualityScore)!=quality||
           string.IsNullOrWhiteSpace(crafterMercenaryId)||crafterProductionSkill is <0 or >20||string.IsNullOrWhiteSpace(ownerCompanyId))
        {reason="InvalidOrDuplicateEquipment";return false;}
        EquipmentInstanceV3 instance=new(instanceId,definitionId,quality,qualityScore,crafterMercenaryId,crafterProductionSkill,ownerCompanyId,createdSessionRevision);
        RegionEquipmentLocationStoreV3? store=regionId!=null&&_locationStoresByRegion.TryGetValue(regionId,out RegionEquipmentLocationStoreV3? found)?found:null;
        bool placed=locationKind switch
        {
            EquipmentLocationKindV3.CompanyHolding=>PlaceHolding(),
            EquipmentLocationKindV3.Ground=>store!=null&&groundCell.HasValue&&PlaceGround(),
            EquipmentLocationKindV3.Storage=>store!=null&&storageCell.HasValue&&!string.IsNullOrWhiteSpace(storageId)&&PlaceStorage(),
            EquipmentLocationKindV3.FacilityOutput=>store!=null&&!string.IsNullOrWhiteSpace(facilityId)&&PlaceOutput(),
            EquipmentLocationKindV3.Equipped=>!string.IsNullOrWhiteSpace(equippedMercenaryId)&&equippedSlot.HasValue&&Enum.IsDefined(equippedSlot.Value)&&PlaceEquipped(),
            _=>false
        };
        if(!placed){reason="InvalidEquipmentLocation";return false;}
        _byId.Add(instanceId,instance);AddIndex(_idsByCompany,ownerCompanyId,instanceId);reason=string.Empty;return true;
        bool PlaceHolding(){instance.MoveToCompanyHolding();AddToHolding(ownerCompanyId,instanceId);return true;}
        bool PlaceGround(){instance.MoveToGround(groundCell!.Value,regionId!);AddIndex(store!.GroundIdsByCell,groundCell.Value,instanceId);AddIndex(store.GroundIdsByCompany,ownerCompanyId,instanceId);return true;}
        bool PlaceStorage(){instance.MoveToStorage(storageId!,storageCell!.Value,regionId!);AddIndex(store!.StorageIdsByZone,storageId!,instanceId);AddIndex(store.StorageIdsByCell,storageCell.Value,instanceId);AddIndex(store.StoredIdsByCompany,ownerCompanyId,instanceId);return true;}
        bool PlaceOutput(){if(!instance.TryPlaceInFacilityOutput(facilityId!,regionId!))return false;if(!store!.OutputIdsByFacility.TryGetValue(facilityId!,out List<string>? ids)){ids=new(FacilityOutputCapacity);store.OutputIdsByFacility.Add(facilityId!,ids);}if(ids.Count>=FacilityOutputCapacity)return false;ids.Add(instanceId);return true;}
        bool PlaceEquipped(){instance.MoveToEquipped(equippedMercenaryId!,equippedSlot!.Value,regionId!);return true;}
    }

    internal void RestoreNextInstanceSequence(long nextSequence)
    {
        if(nextSequence<1)throw new ArgumentOutOfRangeException(nameof(nextSequence));
        _nextInstanceSequence=Math.Max(_nextInstanceSequence,nextSequence);
    }

    public bool TryCreateInstanceInFacilityOutput(
        string equipmentDefinitionId,
        EquipmentQualityV3 quality,
        int qualityScore,
        string crafterMercenaryId,
        int crafterProductionSkillSnapshot,
        string ownerCompanyId,
        string facilityId,
        long createdSessionRevision,
        out EquipmentInstanceV3? instance,
        out string reason)
    {
        instance = null;
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            reason = "Equipment facility is required.";
            return false;
        }
        if (!HasEquipmentOutputCapacity(facilityId))
        {
            reason = "EquipmentOutputBufferFull";
            return false;
        }
        if (!TryCreateInstance(equipmentDefinitionId,quality,qualityScore,crafterMercenaryId,crafterProductionSkillSnapshot,ownerCompanyId,createdSessionRevision,out instance,out reason) || instance == null)
            return false;
        if (!instance.TryPlaceInFacilityOutput(facilityId,_activeLocations.RegionId))
        {
            RemoveInstance(instance.EquipmentInstanceId);
            instance = null;
            reason = "EquipmentFacilityOutputPlacementFailed";
            return false;
        }
        if (!_outputIdsByFacility.TryGetValue(facilityId,out List<string>? ids))
        {
            ids = new(FacilityOutputCapacity);
            _outputIdsByFacility.Add(facilityId,ids);
        }
        if (ids.Count >= FacilityOutputCapacity || ids.Contains(instance.EquipmentInstanceId))
        {
            instance.TryReleaseFromFacilityOutput(facilityId);
            RemoveInstance(instance.EquipmentInstanceId);
            instance = null;
            reason = "EquipmentOutputBufferRegistrationFailed";
            return false;
        }
        ids.Add(instance.EquipmentInstanceId);
        reason = string.Empty;
        return true;
    }

    public int GetEquipmentOutputCount(string facilityId)=>!IsDisposed&&_outputIdsByFacility.TryGetValue(facilityId,out List<string>? ids)?ids.Count:0;
    public int GetEquipmentOutputCount(string regionId,string facilityId)=>!IsDisposed&&_locationStoresByRegion.TryGetValue(regionId,out RegionEquipmentLocationStoreV3? store)&&store.OutputIdsByFacility.TryGetValue(facilityId,out List<string>? ids)?ids.Count:0;
    public int GetEquipmentOutputCapacity(string facilityId)=>IsDisposed||string.IsNullOrWhiteSpace(facilityId)?0:FacilityOutputCapacity;
    public int GetEquipmentOutputCapacity(string regionId,string facilityId)=>IsDisposed||string.IsNullOrWhiteSpace(regionId)||string.IsNullOrWhiteSpace(facilityId)||!_locationStoresByRegion.ContainsKey(regionId)?0:FacilityOutputCapacity;
    public bool HasEquipmentOutputCapacity(string facilityId)=>!IsDisposed&&GetEquipmentOutputCount(facilityId)<FacilityOutputCapacity;
    public IReadOnlyList<string> GetEquipmentOutputInstanceIds(string facilityId)=>!IsDisposed&&_outputIdsByFacility.TryGetValue(facilityId,out List<string>? ids)?new ReadOnlyCollection<string>(new List<string>(ids)):Array.Empty<string>();
    public IReadOnlyList<string> GetEquipmentOutputInstanceIds(string regionId,string facilityId)=>!IsDisposed&&_locationStoresByRegion.TryGetValue(regionId,out RegionEquipmentLocationStoreV3? store)&&store.OutputIdsByFacility.TryGetValue(facilityId,out List<string>? ids)?new ReadOnlyCollection<string>(new List<string>(ids)):Array.Empty<string>();
    public bool IsInFacilityOutput(string facilityId,string instanceId)=>!IsDisposed&&_outputIdsByFacility.TryGetValue(facilityId,out List<string>? ids)&&ids.Contains(instanceId);
    public IReadOnlyList<string> GetCompanyHoldingInstanceIds(string companyId)=>!IsDisposed&&_holdingIdsByCompany.TryGetValue(companyId,out SortedSet<string>? ids)?new ReadOnlyCollection<string>(new List<string>(ids)):Array.Empty<string>();
    public bool IsInCompanyHolding(string companyId,string instanceId)=>!IsDisposed&&_holdingIdsByCompany.TryGetValue(companyId,out SortedSet<string>? ids)&&ids.Contains(instanceId);
    public bool TrySetEquippedTravelRegion(string instanceId,string mercenaryId,string? regionId,out string reason)
    {
        reason=string.Empty;
        if(IsDisposed||!_byId.TryGetValue(instanceId,out EquipmentInstanceV3? instance)||instance==null||!instance.TrySetEquippedRegion(mercenaryId,regionId))
        {reason="InvalidEquippedTravelItem";return false;}
        Revision++;return true;
    }
    public IReadOnlyList<string> GetGroundEquipmentAtCell(Vector2I cell)=>CopyIds(_groundIdsByCell,cell);
    public IReadOnlyList<string> GetGroundEquipmentIdsByCompany(string companyId)=>CopyIds(_groundIdsByCompany,companyId);
    public IReadOnlyList<string> GetStoredEquipmentAtCell(Vector2I cell)=>CopyIds(_storageIdsByCell,cell);
    public IReadOnlyList<string> GetStockpileEquipmentIds(string storageId)=>CopyIds(_storageIdsByZone,storageId);
    public IReadOnlyList<string> GetStoredEquipmentIdsByCompany(string companyId)=>CopyIds(_storedIdsByCompany,companyId);
    public bool IsGroundEquipment(string instanceId,Vector2I cell)=>!IsDisposed&&_groundIdsByCell.TryGetValue(cell,out var ids)&&ids.Contains(instanceId);
    public bool IsStoredEquipment(string instanceId,string storageId,Vector2I cell)=>!IsDisposed&&_storageIdsByZone.TryGetValue(storageId,out var zoneIds)&&zoneIds.Contains(instanceId)&&_storageIdsByCell.TryGetValue(cell,out var cellIds)&&cellIds.Contains(instanceId);

    public bool TryMoveFacilityOutputToGround(string facilityId,string instanceId,Vector2I cell,out string reason)
    {
        reason=string.Empty;
        if(IsDisposed||!_byId.TryGetValue(instanceId,out var instance)||instance.LocationKind!=EquipmentLocationKindV3.FacilityOutput||instance.FacilityId!=facilityId||!IsInFacilityOutput(facilityId,instanceId)){reason="InvalidFacilityOutput";return false;}
        if(instance.RegionId!=_activeLocations.RegionId){reason="InvalidRegion";return false;}
        RemoveFromOutput(facilityId,instanceId);AddIndex(_groundIdsByCell,cell,instanceId);AddIndex(_groundIdsByCompany,instance.OwnerCompanyId,instanceId);instance.MoveToGround(cell,_activeLocations.RegionId);ReleaseReservation(instanceId);Revision++;return true;
    }

    public bool TryMoveCompanyHoldingToGround(string companyId,string instanceId,Vector2I cell,out string reason)
    {
        reason=string.Empty;
        if(IsDisposed||!_byId.TryGetValue(instanceId,out var instance)||instance.OwnerCompanyId!=companyId||instance.LocationKind!=EquipmentLocationKindV3.CompanyHolding||!IsInCompanyHolding(companyId,instanceId)){reason="InvalidCompanyHolding";return false;}
        RemoveIndex(_holdingIdsByCompany,companyId,instanceId);AddIndex(_groundIdsByCell,cell,instanceId);AddIndex(_groundIdsByCompany,companyId,instanceId);instance.MoveToGround(cell,_activeLocations.RegionId);ReleaseReservation(instanceId);Revision++;return true;
    }

    public bool TryMoveGroundToStorage(string instanceId,string storageId,Vector2I storageCell,string companyId,string workRequestId,out string reason)
    {
        reason=string.Empty;
        if(IsDisposed||!_byId.TryGetValue(instanceId,out var instance)||instance.OwnerCompanyId!=companyId||instance.LocationKind!=EquipmentLocationKindV3.Ground||instance.GroundCell is not { } ground||!IsGroundEquipment(instanceId,ground)){reason="InvalidGroundEquipment";return false;}
        if(!_reservationsByInstance.TryGetValue(instanceId,out var reservation)||reservation.WorkRequestId!=workRequestId||reservation.Purpose!=EquipmentReservationPurposeV3.Hauling){reason="EquipmentReservationLost";return false;}
        if(instance.RegionId!=_activeLocations.RegionId){reason="InvalidRegion";return false;}
        RemoveIndex(_groundIdsByCell,ground,instanceId);RemoveIndex(_groundIdsByCompany,companyId,instanceId);AddIndex(_storageIdsByZone,storageId,instanceId);AddIndex(_storageIdsByCell,storageCell,instanceId);AddIndex(_storedIdsByCompany,companyId,instanceId);instance.MoveToStorage(storageId,storageCell,_activeLocations.RegionId);ReleaseReservation(instanceId,workRequestId);Revision++;return true;
    }

    public bool TryMoveStorageToGround(string instanceId,Vector2I groundCell,string companyId,out string reason)
    {
        reason=string.Empty;
        if(IsDisposed||!_byId.TryGetValue(instanceId,out var instance)||instance.OwnerCompanyId!=companyId||instance.LocationKind!=EquipmentLocationKindV3.Storage||instance.StorageId is not { } storageId||instance.StorageCell is not { } storageCell||!IsStoredEquipment(instanceId,storageId,storageCell)){reason="InvalidStoredEquipment";return false;}
        if(instance.RegionId!=_activeLocations.RegionId){reason="InvalidRegion";return false;}
        RemoveStorageIndexes(instance);AddIndex(_groundIdsByCell,groundCell,instanceId);AddIndex(_groundIdsByCompany,companyId,instanceId);instance.MoveToGround(groundCell,_activeLocations.RegionId);ReleaseReservation(instanceId);Revision++;return true;
    }

    public bool TryReserve(string instanceId,string workRequestId,string mercenaryId,EquipmentReservationPurposeV3 purpose,long sessionRevision,out string reason)
    {
        reason=string.Empty;
        if(IsDisposed||sessionRevision!=SessionRevision||!_byId.ContainsKey(instanceId)||string.IsNullOrWhiteSpace(workRequestId)||string.IsNullOrWhiteSpace(mercenaryId)||!Enum.IsDefined(purpose)){reason="InvalidEquipmentReservation";return false;}
        if(_reservationsByInstance.TryGetValue(instanceId,out var existing)){if(existing.WorkRequestId==workRequestId&&existing.MercenaryId==mercenaryId&&existing.Purpose==purpose)return true;reason="Reserved";return false;}
        _reservationsByInstance.Add(instanceId,new(instanceId,workRequestId,mercenaryId,purpose,sessionRevision));return true;
    }
    public bool TryGetReservation(string instanceId,out EquipmentReservationV3? reservation)=>_reservationsByInstance.TryGetValue(instanceId,out reservation);
    public bool IsReservedBy(string instanceId,string workRequestId)=>_reservationsByInstance.TryGetValue(instanceId,out var reservation)&&reservation.WorkRequestId==workRequestId;
    public bool ReleaseReservation(string instanceId,string? workRequestId=null)=>_reservationsByInstance.TryGetValue(instanceId,out var reservation)&&(workRequestId==null||reservation.WorkRequestId==workRequestId)&&_reservationsByInstance.Remove(instanceId);
    public int ReleaseReservationsByMercenary(string mercenaryId){List<string> ids=new();foreach(var pair in _reservationsByInstance)if(pair.Value.MercenaryId==mercenaryId)ids.Add(pair.Key);foreach(string id in ids)_reservationsByInstance.Remove(id);return ids.Count;}
    public int ClearReservationsForRegion(string regionId)
    {
        if(string.IsNullOrWhiteSpace(regionId))return 0;
        List<string> ids=new();
        foreach((string instanceId,EquipmentReservationV3 _) in _reservationsByInstance)
            if(_byId.TryGetValue(instanceId,out EquipmentInstanceV3? instance)&&instance.RegionId==regionId)ids.Add(instanceId);
        foreach(string id in ids)_reservationsByInstance.Remove(id);
        return ids.Count;
    }
    public int ClearReservations(){int count=_reservationsByInstance.Count;_reservationsByInstance.Clear();return count;}

    internal bool TryEquipFromAvailable(string instanceId,string mercenaryId,EquipmentSlotV3 slot,string ownerCompanyId,string? replacedInstanceId,out string reason)
    {
        reason=string.Empty;
        if(IsDisposed||string.IsNullOrWhiteSpace(mercenaryId)||!Enum.IsDefined(slot)||!_byId.TryGetValue(instanceId,out EquipmentInstanceV3? incoming)){reason="InvalidEquipment";return false;}
        if(incoming.OwnerCompanyId!=ownerCompanyId){reason="WrongOwner";return false;}
        bool fromFacility=incoming.LocationKind==EquipmentLocationKindV3.FacilityOutput&&incoming.RegionId==_activeLocations.RegionId&&incoming.FacilityId is { } facilityId&&_outputIdsByFacility.TryGetValue(facilityId,out List<string>? outputIds)&&outputIds.Contains(instanceId);
        bool fromHolding=incoming.LocationKind==EquipmentLocationKindV3.CompanyHolding&&IsInCompanyHolding(ownerCompanyId,instanceId);
        if(!fromFacility&&!fromHolding){reason=incoming.LocationKind==EquipmentLocationKindV3.Equipped?"AlreadyEquipped":"InvalidLocation";return false;}
        EquipmentInstanceV3? replaced=null;
        if(replacedInstanceId!=null)
        {
            if(!_byId.TryGetValue(replacedInstanceId,out replaced)||replaced==null||replaced.OwnerCompanyId!=ownerCompanyId||replaced.LocationKind!=EquipmentLocationKindV3.Equipped||replaced.EquippedMercenaryId!=mercenaryId||replaced.EquippedSlot!=slot){reason="TransactionFailed";return false;}
        }
        if(fromFacility)
        {
            List<string> ids=_outputIdsByFacility[incoming.FacilityId!];ids.Remove(instanceId);if(ids.Count==0)_outputIdsByFacility.Remove(incoming.FacilityId!);
        }
        else
        {
            SortedSet<string> ids=_holdingIdsByCompany[ownerCompanyId];ids.Remove(instanceId);if(ids.Count==0)_holdingIdsByCompany.Remove(ownerCompanyId);
        }
        if(replaced!=null){AddToHolding(replaced.OwnerCompanyId,replaced.EquipmentInstanceId);replaced.MoveToCompanyHolding();}
        incoming.MoveToEquipped(mercenaryId,slot,_activeLocations.RegionId);
        return true;
    }

    internal bool TryUnequipToCompanyHolding(string instanceId,string mercenaryId,EquipmentSlotV3 slot,out string reason)
    {
        reason=string.Empty;
        if(IsDisposed||!_byId.TryGetValue(instanceId,out EquipmentInstanceV3? instance)||instance==null){reason="EquipmentNotFound";return false;}
        if(instance.LocationKind!=EquipmentLocationKindV3.Equipped||instance.EquippedMercenaryId!=mercenaryId||instance.EquippedSlot!=slot){reason="TransactionFailed";return false;}
        AddToHolding(instance.OwnerCompanyId,instanceId);instance.MoveToCompanyHolding();return true;
    }

    internal bool TryEquipReservedFromWorld(string instanceId,string workRequestId,string mercenaryId,EquipmentSlotV3 slot,string ownerCompanyId,string? replacedInstanceId,string? returnStorageId,Vector2I returnCell,out string reason)
    {
        reason=string.Empty;
        if(IsDisposed||!_byId.TryGetValue(instanceId,out var incoming)||incoming.OwnerCompanyId!=ownerCompanyId){reason="InvalidEquipment";return false;}
        if(!_reservationsByInstance.TryGetValue(instanceId,out var reservation)||reservation.WorkRequestId!=workRequestId||reservation.MercenaryId!=mercenaryId||reservation.Purpose!=EquipmentReservationPurposeV3.Equip){reason="EquipmentReservationLost";return false;}
        bool fromGround=incoming.LocationKind==EquipmentLocationKindV3.Ground&&incoming.RegionId==_activeLocations.RegionId&&incoming.GroundCell is { } groundCell&&IsGroundEquipment(instanceId,groundCell);
        bool fromStorage=incoming.LocationKind==EquipmentLocationKindV3.Storage&&incoming.RegionId==_activeLocations.RegionId&&incoming.StorageId is { } storageId&&incoming.StorageCell is { } storageCell&&IsStoredEquipment(instanceId,storageId,storageCell);
        if(!fromGround&&!fromStorage){reason="InvalidLocation";return false;}
        EquipmentInstanceV3? replaced=null;
        if(replacedInstanceId!=null&&(!_byId.TryGetValue(replacedInstanceId,out replaced)||replaced==null||replaced.OwnerCompanyId!=ownerCompanyId||replaced.LocationKind!=EquipmentLocationKindV3.Equipped||replaced.EquippedMercenaryId!=mercenaryId||replaced.EquippedSlot!=slot)){reason="TransactionFailed";return false;}
        if(fromGround){RemoveIndex(_groundIdsByCell,incoming.GroundCell!.Value,instanceId);RemoveIndex(_groundIdsByCompany,ownerCompanyId,instanceId);}else RemoveStorageIndexes(incoming);
        if(replaced!=null)
        {
            if(returnStorageId!=null){AddIndex(_storageIdsByZone,returnStorageId,replaced.EquipmentInstanceId);AddIndex(_storageIdsByCell,returnCell,replaced.EquipmentInstanceId);AddIndex(_storedIdsByCompany,ownerCompanyId,replaced.EquipmentInstanceId);replaced.MoveToStorage(returnStorageId,returnCell,_activeLocations.RegionId);}
            else{AddIndex(_groundIdsByCell,returnCell,replaced.EquipmentInstanceId);AddIndex(_groundIdsByCompany,ownerCompanyId,replaced.EquipmentInstanceId);replaced.MoveToGround(returnCell,_activeLocations.RegionId);}
        }
        incoming.MoveToEquipped(mercenaryId,slot,_activeLocations.RegionId);ReleaseReservation(instanceId,workRequestId);Revision++;return true;
    }

    internal bool TryUnequipToWorld(string instanceId,string mercenaryId,EquipmentSlotV3 slot,string ownerCompanyId,string? storageId,Vector2I returnCell,out string reason)
    {
        reason=string.Empty;
        if(IsDisposed||!_byId.TryGetValue(instanceId,out var instance)||instance.OwnerCompanyId!=ownerCompanyId||instance.LocationKind!=EquipmentLocationKindV3.Equipped||instance.EquippedMercenaryId!=mercenaryId||instance.EquippedSlot!=slot){reason="TransactionFailed";return false;}
        if(instance.RegionId!=_activeLocations.RegionId){reason="InvalidRegion";return false;}
        if(storageId!=null){AddIndex(_storageIdsByZone,storageId,instanceId);AddIndex(_storageIdsByCell,returnCell,instanceId);AddIndex(_storedIdsByCompany,ownerCompanyId,instanceId);instance.MoveToStorage(storageId,returnCell,_activeLocations.RegionId);}
        else{AddIndex(_groundIdsByCell,returnCell,instanceId);AddIndex(_groundIdsByCompany,ownerCompanyId,instanceId);instance.MoveToGround(returnCell,_activeLocations.RegionId);}
        ReleaseReservation(instanceId);Revision++;return true;
    }

    public bool TryRemoveEquipmentOutput(string facilityId,string instanceId,out EquipmentInstanceV3? instance)
    {
        instance = null;
        if (IsDisposed || !_outputIdsByFacility.TryGetValue(facilityId,out List<string>? ids) || !ids.Contains(instanceId) || !_byId.TryGetValue(instanceId,out instance) || instance == null)
            return false;
        if (!instance.TryReleaseFromFacilityOutput(facilityId)) return false;
        ids.Remove(instanceId);
        if (ids.Count == 0) _outputIdsByFacility.Remove(facilityId);
        AddToHolding(instance.OwnerCompanyId,instanceId);
        instance.MoveToCompanyHolding();
        ReleaseReservation(instanceId);
        Revision++;
        return true;
    }

    public bool TryMoveFacilityOutputToCompanyHolding(string facilityId,string instanceId,out string reason)
    {
        reason=string.Empty;
        if(IsDisposed||!_byId.TryGetValue(instanceId,out EquipmentInstanceV3? instance)||instance==null||
           instance.LocationKind!=EquipmentLocationKindV3.FacilityOutput||instance.FacilityId!=facilityId||
           !IsInFacilityOutput(facilityId,instanceId))
        {
            reason="InvalidFacilityOutput";
            return false;
        }
        RemoveFromOutput(facilityId,instanceId);
        AddToHolding(instance.OwnerCompanyId,instanceId);
        instance.MoveToCompanyHolding();
        ReleaseReservation(instanceId);
        Revision++;
        return true;
    }

    public bool TryValidateInvariants(out EquipmentRuntimeInvariantSnapshotV3 snapshot,out string reason)
    {
        int facility=0,ground=0,storage=0,equipped=0,holding=0,locationViolations=0,duplicates=0,orphans=0;
        foreach((string id,EquipmentInstanceV3 instance) in _byId)
        {
            int indexed=0;
            RegionEquipmentLocationStoreV3? store=instance.RegionId is { } regionId&&_locationStoresByRegion.TryGetValue(regionId,out RegionEquipmentLocationStoreV3? found)?found:null;
            bool inFacility=store!=null&&instance.FacilityId is { } facilityId&&ContainsOutput(store,facilityId,id);
            bool inGround=store!=null&&instance.GroundCell is { } groundCell&&Contains(store.GroundIdsByCell,groundCell,id)&&Contains(store.GroundIdsByCompany,instance.OwnerCompanyId,id);
            bool inStorage=store!=null&&instance.StorageId is { } storageId&&instance.StorageCell is { } storageCell&&
                           Contains(store.StorageIdsByZone,storageId,id)&&Contains(store.StorageIdsByCell,storageCell,id)&&Contains(store.StoredIdsByCompany,instance.OwnerCompanyId,id);
            bool inHolding=IsInCompanyHolding(instance.OwnerCompanyId,id);
            if(inFacility)indexed++;
            if(inGround)indexed++;
            if(inStorage)indexed++;
            if(inHolding)indexed++;
            bool valid=instance.LocationKind switch
            {
                EquipmentLocationKindV3.FacilityOutput=>inFacility&&instance.RegionId!=null&&instance.GroundCell==null&&instance.StorageId==null&&instance.StorageCell==null&&instance.EquippedMercenaryId==null&&instance.EquippedSlot==null,
                EquipmentLocationKindV3.Ground=>inGround&&instance.RegionId!=null&&instance.FacilityId==null&&instance.StorageId==null&&instance.StorageCell==null&&instance.EquippedMercenaryId==null&&instance.EquippedSlot==null,
                EquipmentLocationKindV3.Storage=>inStorage&&instance.RegionId!=null&&instance.FacilityId==null&&instance.GroundCell==null&&instance.EquippedMercenaryId==null&&instance.EquippedSlot==null,
                EquipmentLocationKindV3.Equipped=>indexed==0&&instance.FacilityId==null&&instance.GroundCell==null&&instance.StorageId==null&&instance.StorageCell==null&&instance.EquippedMercenaryId!=null&&instance.EquippedSlot!=null,
                EquipmentLocationKindV3.CompanyHolding=>inHolding&&instance.RegionId==null&&instance.FacilityId==null&&instance.GroundCell==null&&instance.StorageId==null&&instance.StorageCell==null&&instance.EquippedMercenaryId==null&&instance.EquippedSlot==null,
                _=>false
            };
            if(!valid)locationViolations++;
            if(indexed>1)duplicates++;
            if(instance.LocationKind==EquipmentLocationKindV3.Unplaced||(instance.LocationKind!=EquipmentLocationKindV3.Equipped&&indexed==0))orphans++;
            switch(instance.LocationKind)
            {
                case EquipmentLocationKindV3.FacilityOutput:facility++;break;
                case EquipmentLocationKindV3.Ground:ground++;break;
                case EquipmentLocationKindV3.Storage:storage++;break;
                case EquipmentLocationKindV3.Equipped:equipped++;break;
                case EquipmentLocationKindV3.CompanyHolding:holding++;break;
            }
        }
        foreach(EquipmentReservationV3 reservation in _reservationsByInstance.Values)
            if(reservation.SessionRevision!=SessionRevision||!_byId.ContainsKey(reservation.EquipmentInstanceId))locationViolations++;
        snapshot=new(_byId.Count,facility,ground,storage,equipped,holding,_reservationsByInstance.Count,locationViolations,duplicates,orphans);
        reason=locationViolations==0&&duplicates==0&&orphans==0?string.Empty:
            $"location={locationViolations} duplicate={duplicates} orphan={orphans}";
        return reason.Length==0;
    }

    public bool TryGetInstance(string instanceId, out EquipmentInstanceV3? instance)
    {
        instance = null;
        return !IsDisposed && _byId.TryGetValue(instanceId, out instance);
    }
    public bool ContainsInstance(string instanceId) => !IsDisposed && _byId.ContainsKey(instanceId);

    public bool RemoveInstance(string instanceId)
    {
        if (IsDisposed || !_byId.Remove(instanceId, out EquipmentInstanceV3? instance)) return false;
        RegionEquipmentLocationStoreV3? locationStore=instance.RegionId is { } instanceRegion&&_locationStoresByRegion.TryGetValue(instanceRegion,out RegionEquipmentLocationStoreV3? foundStore)?foundStore:null;
        if (instance.LocationKind==EquipmentLocationKindV3.FacilityOutput&&instance.FacilityId is { } facilityId&&locationStore!=null&&locationStore.OutputIdsByFacility.TryGetValue(facilityId,out List<string>? outputIds))
        {
            outputIds.Remove(instanceId);
            if(outputIds.Count==0)locationStore.OutputIdsByFacility.Remove(facilityId);
        }
        if (_idsByCompany.TryGetValue(instance.OwnerCompanyId, out SortedSet<string>? ids))
        {
            ids.Remove(instanceId);
            if (ids.Count == 0) _idsByCompany.Remove(instance.OwnerCompanyId);
        }
        if(_holdingIdsByCompany.TryGetValue(instance.OwnerCompanyId,out SortedSet<string>? holdingIds)){holdingIds.Remove(instanceId);if(holdingIds.Count==0)_holdingIdsByCompany.Remove(instance.OwnerCompanyId);}
        if(instance.LocationKind==EquipmentLocationKindV3.Ground&&instance.GroundCell is { } ground&&locationStore!=null){RemoveIndex(locationStore.GroundIdsByCell,ground,instanceId);RemoveIndex(locationStore.GroundIdsByCompany,instance.OwnerCompanyId,instanceId);}
        if(instance.LocationKind==EquipmentLocationKindV3.Storage&&locationStore!=null)RemoveStorageIndexes(locationStore,instance);
        ReleaseReservation(instanceId);
        return true;
    }

    public IReadOnlyList<string> GetOwnedInstanceIds(string companyId)
    {
        if (IsDisposed || !_idsByCompany.TryGetValue(companyId, out SortedSet<string>? ids))
            return Array.Empty<string>();
        return new ReadOnlyCollection<string>(new List<string>(ids));
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _byId.Clear();
        _idsByCompany.Clear();
        _holdingIdsByCompany.Clear();
        foreach(RegionEquipmentLocationStoreV3 store in _locationStoresByRegion.Values)
        {
            store.OutputIdsByFacility.Clear();
            store.GroundIdsByCell.Clear();
            store.GroundIdsByCompany.Clear();
            store.StorageIdsByZone.Clear();
            store.StorageIdsByCell.Clear();
            store.StoredIdsByCompany.Clear();
        }
        _locationStoresByRegion.Clear();
        _reservationsByInstance.Clear();
    }

    private string CreateNextInstanceId()
    {
        while (true)
        {
            long sequence = _nextInstanceSequence++;
            string id = "equip_" + SessionRevision.ToString("x16", CultureInfo.InvariantCulture) + "_" +
                        sequence.ToString("x16", CultureInfo.InvariantCulture);
            if (!_byId.ContainsKey(id)) return id;
        }
    }
    private void AddToHolding(string companyId,string instanceId){if(!_holdingIdsByCompany.TryGetValue(companyId,out SortedSet<string>? ids)){ids=new(StringComparer.Ordinal);_holdingIdsByCompany.Add(companyId,ids);}ids.Add(instanceId);}
    private void RemoveFromOutput(string facilityId,string instanceId){if(!_outputIdsByFacility.TryGetValue(facilityId,out var ids))return;ids.Remove(instanceId);if(ids.Count==0)_outputIdsByFacility.Remove(facilityId);}
    private void RemoveStorageIndexes(EquipmentInstanceV3 instance){if(instance.StorageId is { } storageId)RemoveIndex(_storageIdsByZone,storageId,instance.EquipmentInstanceId);if(instance.StorageCell is { } cell)RemoveIndex(_storageIdsByCell,cell,instance.EquipmentInstanceId);RemoveIndex(_storedIdsByCompany,instance.OwnerCompanyId,instance.EquipmentInstanceId);}
    private static void RemoveStorageIndexes(RegionEquipmentLocationStoreV3 store,EquipmentInstanceV3 instance){if(instance.StorageId is { } storageId)RemoveIndex(store.StorageIdsByZone,storageId,instance.EquipmentInstanceId);if(instance.StorageCell is { } cell)RemoveIndex(store.StorageIdsByCell,cell,instance.EquipmentInstanceId);RemoveIndex(store.StoredIdsByCompany,instance.OwnerCompanyId,instance.EquipmentInstanceId);}
    private static bool ContainsOutput(RegionEquipmentLocationStoreV3 store,string facilityId,string instanceId)=>store.OutputIdsByFacility.TryGetValue(facilityId,out List<string>? ids)&&ids.Contains(instanceId);
    private static IReadOnlyList<string> CopyIds<TKey>(Dictionary<TKey,SortedSet<string>> index,TKey key) where TKey:notnull=>index.TryGetValue(key,out var ids)?new ReadOnlyCollection<string>(new List<string>(ids)):Array.Empty<string>();
    private static void AddIndex<TKey>(Dictionary<TKey,SortedSet<string>> index,TKey key,string instanceId) where TKey:notnull{if(!index.TryGetValue(key,out var ids)){ids=new(StringComparer.Ordinal);index.Add(key,ids);}ids.Add(instanceId);}
    private static void RemoveIndex<TKey>(Dictionary<TKey,SortedSet<string>> index,TKey key,string instanceId) where TKey:notnull{if(!index.TryGetValue(key,out var ids))return;ids.Remove(instanceId);if(ids.Count==0)index.Remove(key);}
    private static bool Contains<TKey>(Dictionary<TKey,SortedSet<string>> index,TKey key,string instanceId) where TKey:notnull=>index.TryGetValue(key,out var ids)&&ids.Contains(instanceId);
}
