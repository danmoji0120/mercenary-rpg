using System;
using System.Collections.Generic;
using GameplayV3.Mercenary;
using GameplayV3.Stockpile;
using WorldV2;

namespace GameplayV3.Equipment;

public enum EquipmentCommandFailureV3
{
    None = 0,
    MercenaryNotFound = 1,
    EquipmentNotFound = 2,
    DefinitionNotFound = 3,
    InvalidSlot = 4,
    SlotMismatch = 5,
    WrongOwner = 6,
    InvalidSession = 7,
    InvalidLocation = 8,
    MissingFacilityBufferEntry = 9,
    AlreadyEquipped = 10,
    EquipmentEquippedByOtherMercenary = 11,
    EmptySlot = 12,
    TransactionFailed = 13
}

public sealed record MercenaryEquipmentLoadoutSnapshotV3(
    string MercenaryId,
    string? MainHandEquipmentInstanceId,
    string? ArmorEquipmentInstanceId,
    string? ToolEquipmentInstanceId,
    long Revision)
{
    public string? GetEquippedInstanceId(EquipmentSlotV3 slot)=>slot switch
    {
        EquipmentSlotV3.MainHand=>MainHandEquipmentInstanceId,
        EquipmentSlotV3.Armor=>ArmorEquipmentInstanceId,
        EquipmentSlotV3.Tool=>ToolEquipmentInstanceId,
        _=>null
    };
}

internal sealed class MercenaryEquipmentLoadoutStateV3
{
    public MercenaryEquipmentLoadoutStateV3(string mercenaryId){MercenaryId=mercenaryId;}
    public string MercenaryId{get;}
    public string? MainHand{get;set;}
    public string? Armor{get;set;}
    public string? Tool{get;set;}
    public long Revision{get;set;}
    public string? Get(EquipmentSlotV3 slot)=>slot switch{EquipmentSlotV3.MainHand=>MainHand,EquipmentSlotV3.Armor=>Armor,EquipmentSlotV3.Tool=>Tool,_=>null};
    public void Set(EquipmentSlotV3 slot,string? instanceId){switch(slot){case EquipmentSlotV3.MainHand:MainHand=instanceId;break;case EquipmentSlotV3.Armor:Armor=instanceId;break;case EquipmentSlotV3.Tool:Tool=instanceId;break;}}
    public MercenaryEquipmentLoadoutSnapshotV3 Snapshot()=>new(MercenaryId,MainHand,Armor,Tool,Revision);
}

public sealed record EquipmentCombatStatSnapshotV3(
    string MercenaryId,
    double BaseAttackPower,
    double EquipmentAttackBonus,
    double FinalAttackPower,
    double BaseDefensePower,
    double EquipmentDefenseBonus,
    double FinalDefensePower,
    long LoadoutRevision);

public sealed class EquipmentLoadoutRuntimeV3:IDisposable
{
    private readonly MercenaryRegistryV3 _mercenaries;
    private readonly EquipmentRuntimeV3 _equipment;
    private readonly Dictionary<string,MercenaryEquipmentLoadoutStateV3> _loadouts=new(StringComparer.Ordinal);
    private readonly Dictionary<string,(string MercenaryId,EquipmentSlotV3 Slot)> _equippedByInstance=new(StringComparer.Ordinal);

    public EquipmentLoadoutRuntimeV3(long sessionRevision,MercenaryRegistryV3 mercenaries,EquipmentRuntimeV3 equipment)
    {
        if(sessionRevision<1||equipment.SessionRevision!=sessionRevision||equipment.IsDisposed)throw new ArgumentException("Equipment loadout session is invalid.");
        SessionRevision=sessionRevision;_mercenaries=mercenaries;_equipment=equipment;_mercenaries.MercenaryRemoved+=OnMercenaryRemoved;
    }

    public long SessionRevision{get;private set;}
    public bool IsDisposed{get;private set;}
    public long Revision{get;private set;}
    public event Action<string,EquipmentSlotV3>? LoadoutChanged;

    public void RebindSessionRevision(long sessionRevision)
    {
        if(IsDisposed)throw new ObjectDisposedException(nameof(EquipmentLoadoutRuntimeV3));
        if(sessionRevision<1||_equipment.SessionRevision!=sessionRevision)throw new ArgumentOutOfRangeException(nameof(sessionRevision));
        SessionRevision=sessionRevision;
    }

    public bool TryGetLoadout(string mercenaryId,out MercenaryEquipmentLoadoutSnapshotV3? snapshot)
    {
        snapshot=null;if(IsDisposed||!_mercenaries.ContainsMercenary(mercenaryId))return false;
        snapshot=_loadouts.TryGetValue(mercenaryId,out MercenaryEquipmentLoadoutStateV3? value)?value.Snapshot():new(mercenaryId,null,null,null,0);return true;
    }

    public string? GetEquippedInstanceId(string mercenaryId,EquipmentSlotV3 slot)=>!IsDisposed&&Enum.IsDefined(slot)&&_loadouts.TryGetValue(mercenaryId,out MercenaryEquipmentLoadoutStateV3? value)?value.Get(slot):null;

    public bool TryEquip(string mercenaryId,string equipmentInstanceId,long commandSessionRevision,out EquipmentCommandFailureV3 failure)
    {
        failure=EquipmentCommandFailureV3.None;
        if(IsDisposed||commandSessionRevision!=SessionRevision||_equipment.IsDisposed||_equipment.SessionRevision!=SessionRevision){failure=EquipmentCommandFailureV3.InvalidSession;return false;}
        if(!_mercenaries.TryGetState(mercenaryId,out MercenaryStateV3? mercenary)||mercenary==null){failure=EquipmentCommandFailureV3.MercenaryNotFound;return false;}
        if(!_equipment.TryGetInstance(equipmentInstanceId,out EquipmentInstanceV3? instance)||instance==null){failure=EquipmentCommandFailureV3.EquipmentNotFound;return false;}
        if(instance.OwnerCompanyId!=mercenary.CompanyId){failure=EquipmentCommandFailureV3.WrongOwner;return false;}
        if(!_equipment.Definitions.TryGetDefinition(instance.EquipmentDefinitionId,out EquipmentDefinitionV3? definition)||definition==null){failure=EquipmentCommandFailureV3.DefinitionNotFound;return false;}
        if(!Enum.IsDefined(definition.Slot)){failure=EquipmentCommandFailureV3.InvalidSlot;return false;}
        if(_equippedByInstance.TryGetValue(equipmentInstanceId,out var equipped))
        {
            failure=equipped.MercenaryId==mercenaryId&&equipped.Slot==definition.Slot?EquipmentCommandFailureV3.AlreadyEquipped:EquipmentCommandFailureV3.EquipmentEquippedByOtherMercenary;return false;
        }
        if(instance.LocationKind==EquipmentLocationKindV3.FacilityOutput&&(instance.FacilityId==null||!_equipment.IsInFacilityOutput(instance.FacilityId,equipmentInstanceId))){failure=EquipmentCommandFailureV3.MissingFacilityBufferEntry;return false;}
        if(instance.LocationKind==EquipmentLocationKindV3.CompanyHolding&&!_equipment.IsInCompanyHolding(instance.OwnerCompanyId,equipmentInstanceId)){failure=EquipmentCommandFailureV3.InvalidLocation;return false;}
        if(instance.LocationKind is not (EquipmentLocationKindV3.FacilityOutput or EquipmentLocationKindV3.CompanyHolding)){failure=instance.LocationKind==EquipmentLocationKindV3.Equipped?EquipmentCommandFailureV3.EquipmentEquippedByOtherMercenary:EquipmentCommandFailureV3.InvalidLocation;return false;}
        if(!_loadouts.TryGetValue(mercenaryId,out MercenaryEquipmentLoadoutStateV3? loadout)){loadout=new(mercenaryId);_loadouts.Add(mercenaryId,loadout);}
        string? replacedId=loadout.Get(definition.Slot);
        if(replacedId==equipmentInstanceId){failure=EquipmentCommandFailureV3.AlreadyEquipped;return false;}
        if(!_equipment.TryEquipFromAvailable(equipmentInstanceId,mercenaryId,definition.Slot,mercenary.CompanyId,replacedId,out _)){failure=EquipmentCommandFailureV3.TransactionFailed;return false;}
        if(replacedId!=null)_equippedByInstance.Remove(replacedId);
        loadout.Set(definition.Slot,equipmentInstanceId);_equippedByInstance.Add(equipmentInstanceId,(mercenaryId,definition.Slot));loadout.Revision++;Revision++;LoadoutChanged?.Invoke(mercenaryId,definition.Slot);return true;
    }

    public bool TryUnequip(string mercenaryId,EquipmentSlotV3 slot,long commandSessionRevision,out string equipmentInstanceId,out EquipmentCommandFailureV3 failure)
    {
        equipmentInstanceId=string.Empty;failure=EquipmentCommandFailureV3.None;
        if(IsDisposed||commandSessionRevision!=SessionRevision||_equipment.IsDisposed){failure=EquipmentCommandFailureV3.InvalidSession;return false;}
        if(!Enum.IsDefined(slot)){failure=EquipmentCommandFailureV3.InvalidSlot;return false;}
        if(!_mercenaries.ContainsMercenary(mercenaryId)){failure=EquipmentCommandFailureV3.MercenaryNotFound;return false;}
        if(!_loadouts.TryGetValue(mercenaryId,out MercenaryEquipmentLoadoutStateV3? loadout)||loadout.Get(slot) is not { } instanceId){failure=EquipmentCommandFailureV3.EmptySlot;return false;}
        if(!_equipment.TryUnequipToCompanyHolding(instanceId,mercenaryId,slot,out _)){failure=EquipmentCommandFailureV3.TransactionFailed;return false;}
        loadout.Set(slot,null);_equippedByInstance.Remove(instanceId);loadout.Revision++;Revision++;equipmentInstanceId=instanceId;LoadoutChanged?.Invoke(mercenaryId,slot);return true;
    }

    public bool TryEquipReservedAt(string mercenaryId,string equipmentInstanceId,string workRequestId,GlobalCellCoord workerCell,StockpileSessionV3 stockpiles,long commandSessionRevision,out EquipmentCommandFailureV3 failure)
    {
        failure=EquipmentCommandFailureV3.None;
        if(IsDisposed||commandSessionRevision!=SessionRevision||_equipment.IsDisposed){failure=EquipmentCommandFailureV3.InvalidSession;return false;}
        if(!_mercenaries.TryGetState(mercenaryId,out MercenaryStateV3? mercenary)||mercenary==null){failure=EquipmentCommandFailureV3.MercenaryNotFound;return false;}
        if(!_equipment.TryGetInstance(equipmentInstanceId,out EquipmentInstanceV3? instance)||instance==null){failure=EquipmentCommandFailureV3.EquipmentNotFound;return false;}
        if(instance.OwnerCompanyId!=mercenary.CompanyId){failure=EquipmentCommandFailureV3.WrongOwner;return false;}
        if(!_equipment.Definitions.TryGetDefinition(instance.EquipmentDefinitionId,out EquipmentDefinitionV3? definition)||definition==null){failure=EquipmentCommandFailureV3.DefinitionNotFound;return false;}
        if(instance.LocationKind is not (EquipmentLocationKindV3.Ground or EquipmentLocationKindV3.Storage)){failure=EquipmentCommandFailureV3.InvalidLocation;return false;}
        if(!_loadouts.TryGetValue(mercenaryId,out MercenaryEquipmentLoadoutStateV3? loadout)){loadout=new(mercenaryId);_loadouts.Add(mercenaryId,loadout);}
        string? replacedId=loadout.Get(definition.Slot);string? returnStorageId=null;
        if(stockpiles.Zones.TryGetZoneAtCell(workerCell,out StockpileZoneStateV3? zone)&&zone!=null&&zone.CompanyId==mercenary.CompanyId&&zone.AllowsEquipment)returnStorageId=zone.StockpileZoneId;
        if(!_equipment.TryEquipReservedFromWorld(equipmentInstanceId,workRequestId,mercenaryId,definition.Slot,mercenary.CompanyId,replacedId,returnStorageId,workerCell.Value,out _)){failure=EquipmentCommandFailureV3.TransactionFailed;return false;}
        if(replacedId!=null)_equippedByInstance.Remove(replacedId);loadout.Set(definition.Slot,equipmentInstanceId);_equippedByInstance[equipmentInstanceId]=(mercenaryId,definition.Slot);loadout.Revision++;Revision++;LoadoutChanged?.Invoke(mercenaryId,definition.Slot);return true;
    }

    public bool TryUnequipAt(string mercenaryId,EquipmentSlotV3 slot,GlobalCellCoord workerCell,StockpileSessionV3 stockpiles,long commandSessionRevision,out string equipmentInstanceId,out EquipmentCommandFailureV3 failure)
    {
        equipmentInstanceId=string.Empty;failure=EquipmentCommandFailureV3.None;
        if(IsDisposed||commandSessionRevision!=SessionRevision||_equipment.IsDisposed){failure=EquipmentCommandFailureV3.InvalidSession;return false;}
        if(!_mercenaries.TryGetState(mercenaryId,out MercenaryStateV3? mercenary)||mercenary==null){failure=EquipmentCommandFailureV3.MercenaryNotFound;return false;}
        if(!_loadouts.TryGetValue(mercenaryId,out MercenaryEquipmentLoadoutStateV3? loadout)||loadout.Get(slot) is not { } instanceId){failure=EquipmentCommandFailureV3.EmptySlot;return false;}
        string? storageId=null;if(stockpiles.Zones.TryGetZoneAtCell(workerCell,out StockpileZoneStateV3? zone)&&zone!=null&&zone.CompanyId==mercenary.CompanyId&&zone.AllowsEquipment)storageId=zone.StockpileZoneId;
        if(!_equipment.TryUnequipToWorld(instanceId,mercenaryId,slot,mercenary.CompanyId,storageId,workerCell.Value,out _)){failure=EquipmentCommandFailureV3.TransactionFailed;return false;}
        loadout.Set(slot,null);_equippedByInstance.Remove(instanceId);loadout.Revision++;Revision++;equipmentInstanceId=instanceId;LoadoutChanged?.Invoke(mercenaryId,slot);return true;
    }

    public int ReturnAllEquipmentToCompanyHolding(string mercenaryId)
    {
        if(IsDisposed||!_loadouts.TryGetValue(mercenaryId,out MercenaryEquipmentLoadoutStateV3? loadout))return 0;
        int moved=0;foreach(EquipmentSlotV3 slot in Enum.GetValues<EquipmentSlotV3>()){string? id=loadout.Get(slot);if(id==null||!_equipment.TryUnequipToCompanyHolding(id,mercenaryId,slot,out _))continue;loadout.Set(slot,null);_equippedByInstance.Remove(id);loadout.Revision++;Revision++;moved++;LoadoutChanged?.Invoke(mercenaryId,slot);}
        _loadouts.Remove(mercenaryId);return moved;
    }

    public double EvaluateEquippedModifier(string mercenaryId,EquipmentModifierKindV3 modifierKind)
    {
        if(IsDisposed)return 0;EquipmentSlotV3 slot=modifierKind switch{EquipmentModifierKindV3.AttackPower=>EquipmentSlotV3.MainHand,EquipmentModifierKindV3.DefensePower=>EquipmentSlotV3.Armor,EquipmentModifierKindV3.GatheringWorkSpeed=>EquipmentSlotV3.Tool,_=>(EquipmentSlotV3)0};
        if(!Enum.IsDefined(slot)||GetEquippedInstanceId(mercenaryId,slot) is not { } id||!_equipment.TryGetInstance(id,out EquipmentInstanceV3? instance)||instance==null||!_equipment.Definitions.TryGetDefinition(instance.EquipmentDefinitionId,out EquipmentDefinitionV3? definition)||definition==null)return 0;
        return EquipmentQualityResolverV3.TryEvaluateModifier(definition,instance.Quality,modifierKind,out double value)?value:0;
    }

    public EquipmentCombatStatSnapshotV3 BuildCombatStatSnapshot(string mercenaryId,double baseAttackPower,double baseDefensePower)
    {
        double attack=EvaluateEquippedModifier(mercenaryId,EquipmentModifierKindV3.AttackPower),defense=EvaluateEquippedModifier(mercenaryId,EquipmentModifierKindV3.DefensePower);long revision=_loadouts.TryGetValue(mercenaryId,out MercenaryEquipmentLoadoutStateV3? loadout)?loadout.Revision:0;
        return new(mercenaryId,baseAttackPower,attack,baseAttackPower+attack,baseDefensePower,defense,baseDefensePower+defense,revision);
    }

    public float GetGatheringWorkSpeedMultiplier(string mercenaryId)=>1f+(float)Math.Max(0,EvaluateEquippedModifier(mercenaryId,EquipmentModifierKindV3.GatheringWorkSpeed));
    internal bool TryRestoreLoadout(string mercenaryId,string? mainHand,string? armor,string? tool,long revision,out string reason)
    {
        if(IsDisposed||!_mercenaries.ContainsMercenary(mercenaryId)||_loadouts.ContainsKey(mercenaryId)||revision<0){reason="InvalidOrDuplicateLoadout";return false;}
        MercenaryEquipmentLoadoutStateV3 state=new(mercenaryId){MainHand=mainHand,Armor=armor,Tool=tool,Revision=revision};
        foreach((EquipmentSlotV3 slot,string? id) in new[]{(EquipmentSlotV3.MainHand,mainHand),(EquipmentSlotV3.Armor,armor),(EquipmentSlotV3.Tool,tool)})
        {
            if(id==null)continue;
            if(_equippedByInstance.ContainsKey(id)||!_equipment.TryGetInstance(id,out EquipmentInstanceV3? instance)||instance==null||
               instance.LocationKind!=EquipmentLocationKindV3.Equipped||instance.EquippedMercenaryId!=mercenaryId||instance.EquippedSlot!=slot)
            {reason="InvalidLoadoutEquipmentReference";return false;}
        }
        _loadouts.Add(mercenaryId,state);
        if(mainHand!=null)_equippedByInstance.Add(mainHand,(mercenaryId,EquipmentSlotV3.MainHand));
        if(armor!=null)_equippedByInstance.Add(armor,(mercenaryId,EquipmentSlotV3.Armor));
        if(tool!=null)_equippedByInstance.Add(tool,(mercenaryId,EquipmentSlotV3.Tool));
        Revision=Math.Max(Revision,revision);reason=string.Empty;return true;
    }
    public static string GetFailureDisplayText(EquipmentCommandFailureV3 failure)=>failure switch{EquipmentCommandFailureV3.EquipmentNotFound=>"\uc7a5\ube44\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.",EquipmentCommandFailureV3.WrongOwner=>"\ud574\ub2f9 \uc6a9\ubcd1\uc774 \uc18c\uc720\ud55c \uc7a5\ube44\uac00 \uc544\ub2d9\ub2c8\ub2e4.",EquipmentCommandFailureV3.SlotMismatch=>"\uc7a5\ube44 \ubd80\uc704\uac00 \ub9de\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4.",EquipmentCommandFailureV3.EquipmentEquippedByOtherMercenary=>"\ub2e4\ub978 \uc6a9\ubcd1\uc774 \uc774\ubbf8 \uc7a5\ucc29 \uc911\uc785\ub2c8\ub2e4.",EquipmentCommandFailureV3.InvalidLocation or EquipmentCommandFailureV3.MissingFacilityBufferEntry=>"\uc7a5\ube44 \uc704\uce58 \uc815\ubcf4\uac00 \uc62c\ubc14\ub974\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4.",EquipmentCommandFailureV3.EmptySlot=>"\ud574\uc81c\ud560 \uc7a5\ube44\uac00 \uc5c6\uc2b5\ub2c8\ub2e4.",_=>failure==EquipmentCommandFailureV3.None?string.Empty:"\uc7a5\ube44 \uba85\ub839\uc744 \uc218\ud589\ud560 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4."};

    public void Dispose(){if(IsDisposed)return;_mercenaries.MercenaryRemoved-=OnMercenaryRemoved;foreach(string id in new List<string>(_loadouts.Keys))ReturnAllEquipmentToCompanyHolding(id);_loadouts.Clear();_equippedByInstance.Clear();IsDisposed=true;}
    private void OnMercenaryRemoved(string mercenaryId,string companyId){_equipment.ReleaseReservationsByMercenary(mercenaryId);ReturnAllEquipmentToCompanyHolding(mercenaryId);}
}
