using System;
using GameplayV3.Bases;
using GameplayV3.Construction;
using GameplayV3.Farming;
using GameplayV3.Mercenary;
using GameplayV3.Resources;
using GameplayV3.Rooms;
using GameplayV3.Stockpile;

namespace GameplayV3.Objectives.Runtime;

public sealed class FrontierSurvivalRuntimeV3:IDisposable
{
    private readonly FrontierSurvivalSessionV3 _objective;private readonly ResourceSessionV3 _resources;private readonly MercenarySessionV3 _mercenaries;private readonly StockpileSessionV3 _stockpiles;private readonly ConstructionSessionV3 _construction;private readonly FarmSessionV3 _farm;private readonly RoomSessionV3 _rooms;private readonly BaseRoleSessionV3 _roles;private bool _disposed;private bool _stockpileDirty;private bool _bedsDirty;private bool _farmDirty;private bool _roomsDirty;private bool _rolesDirty;
    public FrontierSurvivalRuntimeV3(FrontierSurvivalSessionV3 objective,ResourceSessionV3 resources,MercenarySessionV3 mercenaries,StockpileSessionV3 stockpiles,ConstructionSessionV3 construction,FarmSessionV3 farm,RoomSessionV3 rooms,BaseRoleSessionV3 roles)
    {_objective=objective;_resources=resources;_mercenaries=mercenaries;_stockpiles=stockpiles;_construction=construction;_farm=farm;_rooms=rooms;_roles=roles;resources.GenerationLedger.EntryRecorded+=OnGenerated;stockpiles.Zones.CellsChanged+=OnStockpileChanged;construction.Structures.StructureRegistered+=OnStructureChanged;construction.Structures.StructureRemoved+=OnStructureChanged;farm.Plots.Changed+=OnFarmChanged;rooms.Registry.Remapped+=OnRoomRemapped;rooms.Registry.MetadataChanged+=OnRoomMetadataChanged;roles.BaseRoleChanged+=OnBaseRoleChanged;roles.HeadquartersChanged+=OnHeadquartersChanged;roles.BaseRoleRemoved+=OnBaseRoleRemoved;SubscriptionCount=10;RefreshAll();}
    public int SubscriptionCount{get;private set;}public bool IsDisposed=>_disposed;public int InitialFullRefreshCount{get;private set;}
    private void RefreshAll(){InitialFullRefreshCount++;RefreshStockpile();RefreshBeds();RefreshFarm();RefreshRooms();RefreshHeadquarters();_objective.UpdateSurvival();}
    private void OnGenerated(ResourceGenerationEntryV3 entry){if(_mercenaries.Registry.TryGetState(entry.MercenaryId,out var state)&&state!=null)_objective.RecordGathered(state.CompanyId,entry.ResourceType,entry.Amount,entry.Reason);}
    private void OnStockpileChanged(System.Collections.Generic.IReadOnlyList<WorldV2.GlobalCellCoord> _)=>Queue(ref _stockpileDirty);private void RefreshStockpile(){int count=0;foreach(StockpileZoneStateV3 zone in _stockpiles.Zones.GetZonesByCompany(_objective.CompanyId))if(zone.IsEnabled&&zone.CellCount>=_objective.Settings.StockpileCellTarget)count++;_objective.UpdateStockpile(count);}
    private void OnStructureChanged(StructureStateV3 state){if(state.CompanyId==_objective.CompanyId&&state.DefinitionId==StructureDefinitionCatalogV3.BasicBedId)Queue(ref _bedsDirty);}private void RefreshBeds(){int count=0;foreach(StructureStateV3 state in _construction.Structures.GetStructuresByCompany(_objective.CompanyId))if(state.DefinitionId==StructureDefinitionCatalogV3.BasicBedId)count++;_objective.UpdateBeds(count);}
    private void OnFarmChanged()=>Queue(ref _farmDirty);private void RefreshFarm()=>_objective.UpdateFarmCells(_farm.Plots.GetCellCountByCompany(_objective.CompanyId));
    private void OnRoomRemapped(RoomTopologyRemapV3 _)=>Queue(ref _roomsDirty);private void OnRoomMetadataChanged(string _)=>Queue(ref _roomsDirty);private void RefreshRooms(){int count=0;foreach(RoomTopologyStateV3 room in _rooms.Registry.GetAllRooms())if(room.Status==RoomTopologyStatusV3.Stable&&room.IsIndoor&&_rooms.Registry.TryGetMetadata(room.RoomId,out RoomMetadataStateV3? metadata)&&metadata?.Affiliation.Kind==RoomAffiliationKindV3.SingleCompany&&metadata.Affiliation.DominantCompanyId==_objective.CompanyId)count++;_objective.UpdateRooms(count);}
    private void OnBaseRoleChanged(BaseRoleChangedV3 value){if(value.CompanyId==_objective.CompanyId)Queue(ref _rolesDirty);}private void OnHeadquartersChanged(HeadquartersChangedV3 value){if(value.CompanyId==_objective.CompanyId)Queue(ref _rolesDirty);}private void OnBaseRoleRemoved(BaseRoleRemovedV3 value){if(value.CompanyId==_objective.CompanyId)Queue(ref _rolesDirty);}private void RefreshHeadquarters()=>_objective.UpdateHeadquarters(_roles.TryGetHeadquarters(_objective.CompanyId,out _));
    private void Queue(ref bool dirty){if(dirty)_objective.Diagnostics.EventCoalescedCount++;dirty=true;UpdateDirtyCount();}
    private void UpdateDirtyCount()=>_objective.Diagnostics.ObjectiveDirtyCount=(_stockpileDirty?1:0)+(_bedsDirty?1:0)+(_farmDirty?1:0)+(_roomsDirty?1:0)+(_rolesDirty?1:0);
    public void Flush(){if(_disposed)return;if(_stockpileDirty){_stockpileDirty=false;RefreshStockpile();}if(_bedsDirty){_bedsDirty=false;RefreshBeds();}if(_farmDirty){_farmDirty=false;RefreshFarm();}if(_roomsDirty){_roomsDirty=false;RefreshRooms();}if(_rolesDirty){_rolesDirty=false;RefreshHeadquarters();}UpdateDirtyCount();}
    public void Dispose(){if(_disposed)return;_disposed=true;_resources.GenerationLedger.EntryRecorded-=OnGenerated;_stockpiles.Zones.CellsChanged-=OnStockpileChanged;_construction.Structures.StructureRegistered-=OnStructureChanged;_construction.Structures.StructureRemoved-=OnStructureChanged;_farm.Plots.Changed-=OnFarmChanged;_rooms.Registry.Remapped-=OnRoomRemapped;_rooms.Registry.MetadataChanged-=OnRoomMetadataChanged;_roles.BaseRoleChanged-=OnBaseRoleChanged;_roles.HeadquartersChanged-=OnHeadquartersChanged;_roles.BaseRoleRemoved-=OnBaseRoleRemoved;SubscriptionCount=0;}
}
