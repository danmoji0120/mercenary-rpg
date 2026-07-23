using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameplayV3.Company;
using GameplayV3.Construction;
using GameplayV3.Equipment;
using GameplayV3.Farming;
using GameplayV3.Mercenary;
using GameplayV3.Network;
using GameplayV3.Production;
using GameplayV3.Resources;
using GameplayV3.Session;
using GameplayV3.Stockpile;
using GameplayV3.Time;
using Godot;
using WorldV2;

namespace GameplayV3.Checkpoint;

public sealed class WorldCheckpointEnvelopeV3
{
    public int CheckpointSchemaVersion{get;set;}
    public string GameDataVersion{get;set;}=string.Empty;
    public DateTime CreatedUtc{get;set;}
    public int PayloadByteLength{get;set;}
    public string PayloadSha256{get;set;}=string.Empty;
    public string Payload{get;set;}=string.Empty;
}

public sealed class PersistentWorldCheckpointV3
{
    public string WorldId{get;set;}=string.Empty;
    public int WorldSeed{get;set;}
    public string GeneratorVersion{get;set;}=string.Empty;
    public long WorldRevision{get;set;}
    public long ClockTick{get;set;}
    public double WorldTime{get;set;}
    public int TimeScale{get;set;}=1;
    public long ClockRevision{get;set;}
    public string? ActiveRegionId{get;set;}
    public long EquipmentNextSequence{get;set;}=1;
    public long TravelingGroupNextSequence{get;set;}=1;
    public long RuntimeSessionRevision{get;set;}=1;
    public List<AccountBindingCheckpointV3> AccountBindings{get;set;}=new();
    public List<CompanyCheckpointV3> Companies{get;set;}=new();
    public List<RegionCheckpointV3> Regions{get;set;}=new();
    public List<EquipmentCheckpointV3> Equipment{get;set;}=new();
    public List<WorldRouteCheckpointV3> Routes{get;set;}=new();
    public List<TravelingGroupCheckpointV3> TravelingGroups{get;set;}=new();
    public List<RegionSessionGenerationCheckpointV3> RegionSessionGenerations{get;set;}=new();
}

public sealed record AccountBindingCheckpointV3(string AccountId,string CompanyId);
public sealed record CellCheckpointV3(int X,int Y){public Vector2I ToVector()=>new(X,Y);public GlobalCellCoord ToGlobal()=>new(ToVector());public static CellCheckpointV3 From(Vector2I value)=>new(value.X,value.Y);}
public sealed record MaterialCheckpointV3(ResourceTypeV3 Type,int Amount);
public sealed class CompanyCheckpointV3
{
    public string CompanyId{get;set;}=string.Empty;public string OwnerPlayerId{get;set;}=string.Empty;public string PlayerAccountId{get;set;}=string.Empty;
    public string DisplayName{get;set;}=string.Empty;public DateTime CreatedUtc{get;set;}public List<string> OwnedRegionIds{get;set;}=new();
    public List<MercenaryCheckpointV3> Mercenaries{get;set;}=new();public List<LoadoutCheckpointV3> Loadouts{get;set;}=new();
}
public sealed class MercenaryCheckpointV3
{
    public string MercenaryId{get;set;}=string.Empty;public string CompanyId{get;set;}=string.Empty;public string DisplayName{get;set;}=string.Empty;public string AppearanceKey{get;set;}=string.Empty;
    public DateTime CreatedUtc{get;set;}public bool IsInitialSquadMember{get;set;}public int? InitialSquadSlotIndex{get;set;}public CellCheckpointV3 Cell{get;set;}=new(0,0);
    public MercenaryActivityStateV3 ActivityState{get;set;}public int[] Attributes{get;set;}=Array.Empty<int>();public int[] WorkSkills{get;set;}=Array.Empty<int>();
    public MercenaryWorldPresenceV3 PresenceKind{get;set;}public string? CurrentRegionId{get;set;}public string? TravelingGroupId{get;set;}
}
public sealed record LoadoutCheckpointV3(string MercenaryId,string? MainHand,string? Armor,string? Tool,long Revision);
public sealed class EquipmentCheckpointV3
{
    public string InstanceId{get;set;}=string.Empty;public string DefinitionId{get;set;}=string.Empty;public EquipmentQualityV3 Quality{get;set;}public int QualityScore{get;set;}
    public string CrafterMercenaryId{get;set;}=string.Empty;public int CrafterProductionSkill{get;set;}public string OwnerCompanyId{get;set;}=string.Empty;public long CreatedSessionRevision{get;set;}
    public EquipmentLocationKindV3 LocationKind{get;set;}public string? RegionId{get;set;}public CellCheckpointV3? GroundCell{get;set;}public string? StorageId{get;set;}
    public CellCheckpointV3? StorageCell{get;set;}public string? FacilityId{get;set;}public string? EquippedMercenaryId{get;set;}public EquipmentSlotV3? EquippedSlot{get;set;}
}
public sealed class RegionCheckpointV3
{
    public string RegionId{get;set;}=string.Empty;public RegionTypeV3 RegionType{get;set;}public string? OwnerCompanyId{get;set;}public int TerrainSeed{get;set;}
    public long RegionRevision{get;set;}public double LastCommittedWorldTime{get;set;}public long ProductionNextOrderSequence{get;set;}=1;
    public List<ResourceNodeCheckpointV3> ResourceNodes{get;set;}=new();public List<GroundStackCheckpointV3> GroundStacks{get;set;}=new();
    public List<StructureCheckpointV3> Structures{get;set;}=new();public List<BlueprintCheckpointV3> Blueprints{get;set;}=new();
    public List<StockpileCheckpointV3> Stockpiles{get;set;}=new();public List<FarmPlotCheckpointV3> FarmPlots{get;set;}=new();
    public List<ProductionFacilityCheckpointV3> ProductionFacilities{get;set;}=new();
}
public sealed record ResourceNodeCheckpointV3(string Id,ResourceNodeTypeV3 Type,CellCheckpointV3 Cell,int Remaining,int Maximum,int Yield,DateTime CreatedUtc);
public sealed record GroundStackCheckpointV3(string Id,ResourceTypeV3 Type,int Amount,CellCheckpointV3 Cell,DateTime CreatedUtc);
public sealed class StructureCheckpointV3
{
    public string Id{get;set;}=string.Empty;public string CompanyId{get;set;}=string.Empty;public string DefinitionId{get;set;}=string.Empty;public CellCheckpointV3 Anchor{get;set;}=new(0,0);
    public StructureOrientationV3 Orientation{get;set;}public List<CellCheckpointV3> Cells{get;set;}=new();public List<MaterialCheckpointV3> Materials{get;set;}=new();
    public StructureMovementKindV3 MovementKind{get;set;}public StructureRoomBoundaryKindV3 RoomBoundaryKind{get;set;}public ResourceTypeV3? MaterialResourceType{get;set;}
    public float CurrentHealth{get;set;}public float MaxHealth{get;set;}public DateTime CompletedUtc{get;set;}
}
public sealed class BlueprintCheckpointV3
{
    public string Id{get;set;}=string.Empty;public string CompanyId{get;set;}=string.Empty;public string DefinitionId{get;set;}=string.Empty;public CellCheckpointV3 Anchor{get;set;}=new(0,0);
    public StructureOrientationV3 Orientation{get;set;}public List<CellCheckpointV3> Cells{get;set;}=new();public List<MaterialCheckpointV3> Required{get;set;}=new();
    public List<MaterialCheckpointV3> Delivered{get;set;}=new();public ResourceTypeV3? MaterialResourceType{get;set;}public float RequiredWorkSeconds{get;set;}
    public float ExpectedMaxHealth{get;set;}public ConstructionBlueprintStatusV3 Status{get;set;}public float Progress{get;set;}public int Revision{get;set;}public DateTime CreatedUtc{get;set;}
}
public sealed class StockpileCheckpointV3
{
    public string Id{get;set;}=string.Empty;public string CompanyId{get;set;}=string.Empty;public DateTime CreatedUtc{get;set;}public bool Enabled{get;set;}public bool AllowsEquipment{get;set;}
    public List<CellCheckpointV3> Cells{get;set;}=new();public List<ResourceTypeV3> AllowedResources{get;set;}=new();
}
public sealed class CropCellCheckpointV3{public CellCheckpointV3 Cell{get;set;}=new(0,0);public CropStageV3 Stage{get;set;}public float Elapsed{get;set;}public float Normalized{get;set;}public int Revision{get;set;}}
public sealed class FarmPlotCheckpointV3{public string Id{get;set;}=string.Empty;public string CompanyId{get;set;}=string.Empty;public string CropDefinitionId{get;set;}=string.Empty;public int Revision{get;set;}public List<CropCellCheckpointV3> Cells{get;set;}=new();}
public sealed class ProductionFacilityCheckpointV3
{
    public string FacilityId{get;set;}=string.Empty;public long EquipmentCompletionSequence{get;set;}public string LastCommittedBatchToken{get;set;}=string.Empty;
    public long Revision{get;set;}public string LastChangedReason{get;set;}=string.Empty;public List<ProductionOrderCheckpointV3> Queue{get;set;}=new();
    public List<MaterialCheckpointV3> DeliveredMaterials{get;set;}=new();public Dictionary<ResourceTypeV3,int> OutputBuffer{get;set;}=new();
}
public sealed record ProductionOrderCheckpointV3(string OrderId,string FacilityId,string RecipeId,int RequestedBatches,int CompletedBatches,ProductionOrderStateV3 State,float WorkProgressSeconds,long Revision,string LastFailureReason);
public sealed record WorldRouteCheckpointV3(string RouteId,string OriginRegionId,string DestinationRegionId,bool Bidirectional,double TravelDuration,bool Enabled);
public sealed record TravelingGroupCheckpointV3(string Id,string OwnerCompanyId,List<string> MercenaryIds,List<string> EquipmentIds,string OriginRegionId,string DestinationRegionId,string RouteId,double DepartureWorldTime,double ArrivalWorldTime,TravelingGroupStatusV3 State,long Revision);
public sealed record RegionSessionGenerationCheckpointV3(string RegionId,long Generation);

public sealed class RestoredPersistentWorldRuntimeV3
{
    public required PersistentWorldStateV3 World{get;init;}public required CompanySessionV3 Companies{get;init;}public required MercenarySessionV3 Mercenaries{get;init;}
    public required EquipmentRuntimeV3 Equipment{get;init;}public required EquipmentLoadoutRuntimeV3 Loadouts{get;init;}public required SimulationClockSessionV3 Clock{get;init;}
    public required IReadOnlyDictionary<string,string> AccountBindings{get;init;}public required IReadOnlyDictionary<string,long> RegionSessionGenerations{get;init;}
}

public static class PersistentWorldCheckpointMapperV3
{
    private static readonly Rect2I RestoreBounds=new(-1_000_000,-1_000_000,2_000_000,2_000_000);

    public static PersistentWorldCheckpointV3 Capture(PersistentWorldStateV3 world,RegionSessionManagerV3? regionSessions,IReadOnlyDictionary<string,PlayerCompanyStateV3> accountBindings)
    {
        ArgumentNullException.ThrowIfNull(world);
        PersistentWorldCheckpointV3 dto=new()
        {
            WorldId=world.WorldId,WorldSeed=world.WorldSeed,GeneratorVersion=world.GeneratorVersion,WorldRevision=world.WorldRevision,
            ClockTick=world.WorldClock.SimulationTick,WorldTime=world.WorldClock.ElapsedSimulationSeconds,TimeScale=world.WorldClock.TimeScale,ClockRevision=world.WorldClock.Revision,
            ActiveRegionId=world.ActiveRegionId,TravelingGroupNextSequence=world.TravelingGroups.NextSequence
        };
        foreach(var pair in accountBindings.OrderBy(x=>x.Key,StringComparer.Ordinal))dto.AccountBindings.Add(new(pair.Key,pair.Value.CompanyId));
        foreach(PlayerCompanyStateV3 company in world.PlayerCompanies.Values.OrderBy(x=>x.CompanyId,StringComparer.Ordinal))
        {
            CompanyCheckpointV3 c=new(){CompanyId=company.CompanyId,OwnerPlayerId=company.Company.OwnerPlayerId,PlayerAccountId=company.PlayerAccountId,DisplayName=company.Company.DisplayName,CreatedUtc=company.Company.CreatedUtc,OwnedRegionIds=company.OwnedRegionIds.OrderBy(x=>x,StringComparer.Ordinal).ToList()};
            foreach(string mercenaryId in company.MercenaryProfiles.GetMercenariesByCompany(company.CompanyId))
            {
                company.MercenaryProfiles.TryGetProfile(mercenaryId,out MercenaryProfileV3? profile);company.MercenaryProfiles.TryGetState(mercenaryId,out MercenaryStateV3? state);
                company.TryGetMercenaryPresence(mercenaryId,out MercenaryPresenceStateV3? presence);
                if(profile==null||state==null||presence==null)continue;
                c.Mercenaries.Add(new(){MercenaryId=mercenaryId,CompanyId=state.CompanyId,DisplayName=profile.DisplayName,AppearanceKey=profile.AppearanceKey,CreatedUtc=profile.CreatedUtc,
                    IsInitialSquadMember=profile.IsInitialSquadMember,InitialSquadSlotIndex=profile.InitialSquadSlotIndex,Cell=CellCheckpointV3.From(state.CurrentCell.Value),ActivityState=state.ActivityState,
                    Attributes=new[]{profile.Attributes.Strength,profile.Attributes.Agility,profile.Attributes.Endurance,profile.Attributes.Intelligence,profile.Attributes.Mental},
                    WorkSkills=new[]{profile.WorkSkills.Hauling,profile.WorkSkills.Construction,profile.WorkSkills.Gathering,profile.WorkSkills.Farming,profile.WorkSkills.Production,profile.WorkSkills.Medicine,profile.WorkSkills.Guarding},
                    PresenceKind=presence.PresenceKind,CurrentRegionId=presence.CurrentRegionId,TravelingGroupId=presence.TravelingGroupId});
                if(company.EquipmentLoadouts.TryGetLoadout(mercenaryId,out MercenaryEquipmentLoadoutSnapshotV3? loadout)&&loadout!=null)
                    c.Loadouts.Add(new(mercenaryId,loadout.MainHandEquipmentInstanceId,loadout.ArmorEquipmentInstanceId,loadout.ToolEquipmentInstanceId,loadout.Revision));
            }
            dto.Companies.Add(c);dto.EquipmentNextSequence=Math.Max(dto.EquipmentNextSequence,company.Equipment.NextInstanceSequence);
        }
        EquipmentRuntimeV3? equipment=world.PlayerCompanies.Values.FirstOrDefault()?.Equipment;
        if(equipment!=null)foreach(string id in equipment.GetAllInstanceIds())
            if(equipment.TryGetInstance(id,out EquipmentInstanceV3? item)&&item!=null)dto.Equipment.Add(new(){InstanceId=id,DefinitionId=item.EquipmentDefinitionId,Quality=item.Quality,QualityScore=item.QualityScore,CrafterMercenaryId=item.CrafterMercenaryId,
                CrafterProductionSkill=item.CrafterProductionSkillSnapshot,OwnerCompanyId=item.OwnerCompanyId,CreatedSessionRevision=item.CreatedSessionRevision,LocationKind=item.LocationKind,RegionId=item.RegionId,
                GroundCell=item.GroundCell.HasValue?CellCheckpointV3.From(item.GroundCell.Value):null,StorageId=item.StorageId,StorageCell=item.StorageCell.HasValue?CellCheckpointV3.From(item.StorageCell.Value):null,
                FacilityId=item.FacilityId,EquippedMercenaryId=item.EquippedMercenaryId,EquippedSlot=item.EquippedSlot});
        foreach(RegionPersistentStateV3 region in world.Regions.Values.OrderBy(x=>x.RegionId,StringComparer.Ordinal))dto.Regions.Add(CaptureRegion(region));
        foreach(WorldRouteV3 route in world.WorldGraph.Routes)dto.Routes.Add(new(route.RouteId,route.OriginRegionId,route.DestinationRegionId,route.Bidirectional,route.TravelDuration,route.Enabled));
        foreach(TravelingGroupStateV3 group in world.TravelingGroups.GetAllGroups())dto.TravelingGroups.Add(new(group.TravelingGroupId,group.OwnerCompanyId,group.MercenaryIds.ToList(),group.EquippedEquipmentInstanceIds.ToList(),group.OriginRegionId,group.DestinationRegionId,group.RouteId,group.DepartureWorldTime,group.ArrivalWorldTime,group.State,group.Revision));
        if(regionSessions!=null)foreach(var pair in regionSessions.GetSessionGenerationSnapshot().OrderBy(x=>x.Key,StringComparer.Ordinal))dto.RegionSessionGenerations.Add(new(pair.Key,pair.Value));
        dto.RuntimeSessionRevision=Math.Max(1,dto.RegionSessionGenerations.Select(x=>x.Generation+1).DefaultIfEmpty(1).Max());
        return dto;
    }

    private static RegionCheckpointV3 CaptureRegion(RegionPersistentStateV3 region)
    {
        RegionCheckpointV3 dto=new(){RegionId=region.RegionId,RegionType=region.RegionType,OwnerCompanyId=region.OwnerCompanyId,TerrainSeed=region.TerrainSeed,RegionRevision=region.RegionRevision,LastCommittedWorldTime=region.LastCommittedWorldTime,ProductionNextOrderSequence=region.Production.NextOrderSequence};
        foreach(string id in region.ResourceNodes.GetAllNodeIds())if(region.ResourceNodes.TryGet(id,out ResourceNodeStateV3? node)&&node!=null)dto.ResourceNodes.Add(new(id,node.NodeType,CellCheckpointV3.From(node.Cell.Value),node.RemainingAmount,node.MaxAmount,node.YieldPerCycle,node.CreatedUtc));
        foreach(string id in region.GroundResourceStacks.GetAllStackIds())if(region.GroundResourceStacks.TryGet(id,out GroundResourceStackV3? stack)&&stack!=null)dto.GroundStacks.Add(new(id,stack.ResourceType,stack.Amount,CellCheckpointV3.From(stack.Cell.Value),stack.CreatedUtc));
        foreach(string id in region.Structures.GetAllStructureIds())if(region.Structures.TryGet(id,out StructureStateV3? state)&&state!=null)dto.Structures.Add(new(){Id=id,CompanyId=state.CompanyId,DefinitionId=state.DefinitionId,Anchor=CellCheckpointV3.From(state.AnchorCell.Value),Orientation=state.Orientation,Cells=state.OccupiedCells.Select(x=>CellCheckpointV3.From(x.Value)).ToList(),Materials=state.EmbeddedMaterials.Select(x=>new MaterialCheckpointV3(x.ResourceType,x.RequiredAmount)).ToList(),MovementKind=state.MovementKind,RoomBoundaryKind=state.RoomBoundaryKind,MaterialResourceType=state.MaterialResourceType,CurrentHealth=state.CurrentHealth,MaxHealth=state.MaxHealth,CompletedUtc=state.CompletedUtc});
        foreach(string id in region.Blueprints.GetAllBlueprintIds())if(region.Blueprints.TryGet(id,out ConstructionBlueprintStateV3? state)&&state!=null)dto.Blueprints.Add(new(){Id=id,CompanyId=state.CompanyId,DefinitionId=state.DefinitionId,Anchor=CellCheckpointV3.From(state.AnchorCell.Value),Orientation=state.Orientation,Cells=state.OccupiedCells.Select(x=>CellCheckpointV3.From(x.Value)).ToList(),Required=state.MaterialBuffer.GetRequiredMaterialsSnapshot().Select(x=>new MaterialCheckpointV3(x.ResourceType,x.RequiredAmount)).ToList(),Delivered=state.MaterialBuffer.GetDeliveredMaterialsSnapshot().Select(x=>new MaterialCheckpointV3(x.ResourceType,x.RequiredAmount)).ToList(),MaterialResourceType=state.MaterialResourceType,RequiredWorkSeconds=state.RequiredWorkSeconds,ExpectedMaxHealth=state.ExpectedMaxHealth,Status=state.Status,Progress=state.ConstructionProgressSeconds,Revision=state.Revision,CreatedUtc=state.CreatedUtc});
        foreach(string id in region.StockpileZones.GetAllZoneIds())if(region.StockpileZones.TryGetZone(id,out StockpileZoneStateV3? zone)&&zone!=null)dto.Stockpiles.Add(new(){Id=id,CompanyId=zone.CompanyId,CreatedUtc=zone.CreatedUtc,Enabled=zone.IsEnabled,AllowsEquipment=zone.AllowsEquipment,Cells=zone.Cells.Select(x=>CellCheckpointV3.From(x.Value)).ToList(),AllowedResources=zone.AllowedResourceTypes.ToList()});
        foreach(FarmPlotV3 plot in region.Farming.Plots.GetAllPlots())
            dto.FarmPlots.Add(new(){Id=plot.FarmPlotId,CompanyId=plot.CompanyId,CropDefinitionId=plot.CropDefinitionId,Revision=plot.Revision,Cells=plot.Cells.Select(cell=>{region.Farming.Plots.TryGetCrop(new(cell),out CropCellStateV3? crop);return new CropCellCheckpointV3{Cell=CellCheckpointV3.From(cell),Stage=crop?.Stage??CropStageV3.Empty,Elapsed=crop?.GrowthElapsedSeconds??0,Normalized=crop?.GrowthNormalized??0,Revision=crop?.Revision??0};}).ToList()});
        foreach(ProductionCheckpointFacilityV3 facility in region.Production.GetCheckpointFacilities())dto.ProductionFacilities.Add(new(){FacilityId=facility.FacilityId,EquipmentCompletionSequence=facility.EquipmentCompletionSequence,LastCommittedBatchToken=facility.LastCommittedBatchToken,Revision=facility.Revision,LastChangedReason=facility.LastChangedReason,Queue=facility.Queue.Select(x=>new ProductionOrderCheckpointV3(x.OrderId,x.FacilityId,x.RecipeId,x.RequestedBatches,x.CompletedBatches,x.State,x.WorkProgressSeconds,x.Revision,x.LastFailureReason)).ToList(),DeliveredMaterials=facility.DeliveredMaterials.Select(x=>new MaterialCheckpointV3(x.ResourceType,x.RequiredAmount)).ToList(),OutputBuffer=new(facility.OutputBuffer)});
        return dto;
    }

    public static bool Validate(PersistentWorldCheckpointV3 dto,out string reason)
    {
        reason=string.Empty;
        if(dto==null||string.IsNullOrWhiteSpace(dto.WorldId)||string.IsNullOrWhiteSpace(dto.GeneratorVersion)||dto.WorldRevision<0||dto.WorldTime<0||dto.RuntimeSessionRevision<1){reason="InvalidWorldMetadata";return false;}
        if(HasDuplicate(dto.Companies,x=>x.CompanyId)||HasDuplicate(dto.Companies,x=>x.OwnerPlayerId)||HasDuplicate(dto.Regions,x=>x.RegionId)||HasDuplicate(dto.Equipment,x=>x.InstanceId)||HasDuplicate(dto.Routes,x=>x.RouteId)||HasDuplicate(dto.TravelingGroups,x=>x.Id)){reason="DuplicatePersistentId";return false;}
        HashSet<string> companies=dto.Companies.Select(x=>x.CompanyId).ToHashSet(StringComparer.Ordinal),regions=dto.Regions.Select(x=>x.RegionId).ToHashSet(StringComparer.Ordinal);
        HashSet<string> mercenaries=dto.Companies.SelectMany(x=>x.Mercenaries).Select(x=>x.MercenaryId).ToHashSet(StringComparer.Ordinal),equipment=dto.Equipment.Select(x=>x.InstanceId).ToHashSet(StringComparer.Ordinal);
        if(dto.Companies.Sum(x=>x.Mercenaries.Count)!=mercenaries.Count||HasDuplicate(dto.AccountBindings,x=>x.AccountId)||dto.AccountBindings.Any(x=>!companies.Contains(x.CompanyId))||
           dto.Regions.Any(x=>x.OwnerCompanyId!=null&&!companies.Contains(x.OwnerCompanyId))||dto.Companies.Any(x=>x.OwnedRegionIds.Any(id=>!regions.Contains(id)))){reason="InvalidCompanyReference";return false;}
        if(dto.Equipment.Any(x=>!companies.Contains(x.OwnerCompanyId)||!mercenaries.Contains(x.CrafterMercenaryId))){reason="InvalidEquipmentOwnerOrCrafter";return false;}
        if(dto.Routes.Any(x=>!regions.Contains(x.OriginRegionId)||!regions.Contains(x.DestinationRegionId))){reason="InvalidRouteRegion";return false;}
        Dictionary<string,WorldRouteCheckpointV3> routes=dto.Routes.ToDictionary(x=>x.RouteId,StringComparer.Ordinal);
        if(dto.TravelingGroups.Any(x=>!companies.Contains(x.OwnerCompanyId)||!regions.Contains(x.OriginRegionId)||!regions.Contains(x.DestinationRegionId)||!routes.TryGetValue(x.RouteId,out WorldRouteCheckpointV3? route)||
           !(route.OriginRegionId==x.OriginRegionId&&route.DestinationRegionId==x.DestinationRegionId||route.Bidirectional&&route.OriginRegionId==x.DestinationRegionId&&route.DestinationRegionId==x.OriginRegionId)||
           x.MercenaryIds.Any(id=>!mercenaries.Contains(id))||x.EquipmentIds.Any(id=>!equipment.Contains(id)))){reason="InvalidTravelReference";return false;}
        if(dto.TravelingGroups.Where(x=>x.State is TravelingGroupStatusV3.Traveling or TravelingGroupStatusV3.ArrivalBlocked).SelectMany(x=>x.MercenaryIds).GroupBy(x=>x,StringComparer.Ordinal).Any(x=>x.Count()>1)){reason="DuplicateTravelingMercenary";return false;}
        if(dto.Companies.SelectMany(x=>x.Mercenaries).Any(x=>x.PresenceKind==MercenaryWorldPresenceV3.AtRegion&&!regions.Contains(x.CurrentRegionId??string.Empty)||x.PresenceKind==MercenaryWorldPresenceV3.Traveling&&string.IsNullOrWhiteSpace(x.TravelingGroupId))){reason="InvalidMercenaryPresence";return false;}
        Dictionary<string,RegionCheckpointV3> regionById=dto.Regions.ToDictionary(x=>x.RegionId,StringComparer.Ordinal);
        HashSet<string> loadoutEquipment=new(StringComparer.Ordinal);
        EquipmentDefinitionRegistryV3 definitions=StarterEquipmentContentV3.CreateRegistry();
        foreach(LoadoutCheckpointV3 loadout in dto.Companies.SelectMany(x=>x.Loadouts))
            foreach((string? id,EquipmentSlotV3 slot) in new[]{(loadout.MainHand,EquipmentSlotV3.MainHand),(loadout.Armor,EquipmentSlotV3.Armor),(loadout.Tool,EquipmentSlotV3.Tool)})
                if(id!=null&&(!loadoutEquipment.Add(id)||!dto.Equipment.Any(x=>x.InstanceId==id&&x.LocationKind==EquipmentLocationKindV3.Equipped&&x.EquippedMercenaryId==loadout.MercenaryId&&x.EquippedSlot==slot&&
                    definitions.TryGetDefinition(x.DefinitionId,out EquipmentDefinitionV3? definition)&&definition?.Slot==slot)))
                {reason="InvalidLoadoutReference";return false;}
        if(dto.Equipment.Any(x=>x.LocationKind==EquipmentLocationKindV3.Equipped&&!loadoutEquipment.Contains(x.InstanceId))){reason="OrphanEquippedEquipment";return false;}
        foreach(EquipmentCheckpointV3 item in dto.Equipment)
        {
            bool valid=item.LocationKind switch
            {
                EquipmentLocationKindV3.CompanyHolding=>item.RegionId==null&&item.GroundCell==null&&item.StorageId==null&&item.StorageCell==null&&item.FacilityId==null&&item.EquippedMercenaryId==null&&item.EquippedSlot==null,
                EquipmentLocationKindV3.Ground=>item.RegionId!=null&&regions.Contains(item.RegionId)&&item.GroundCell!=null&&item.StorageId==null&&item.StorageCell==null&&item.FacilityId==null&&item.EquippedMercenaryId==null&&item.EquippedSlot==null,
                EquipmentLocationKindV3.Storage=>item.RegionId!=null&&regions.Contains(item.RegionId)&&item.GroundCell==null&&item.StorageId!=null&&item.StorageCell!=null&&item.FacilityId==null&&item.EquippedMercenaryId==null&&item.EquippedSlot==null,
                EquipmentLocationKindV3.FacilityOutput=>item.RegionId!=null&&regions.Contains(item.RegionId)&&item.GroundCell==null&&item.StorageId==null&&item.StorageCell==null&&item.FacilityId!=null&&item.EquippedMercenaryId==null&&item.EquippedSlot==null,
                EquipmentLocationKindV3.Equipped=>item.GroundCell==null&&item.StorageId==null&&item.StorageCell==null&&item.FacilityId==null&&item.EquippedMercenaryId!=null&&mercenaries.Contains(item.EquippedMercenaryId)&&item.EquippedSlot.HasValue,
                _=>false
            };
            if(!valid){reason="InvalidEquipmentLocation";return false;}
            if(item.LocationKind==EquipmentLocationKindV3.Storage&&!regionById[item.RegionId!].Stockpiles.Any(x=>x.Id==item.StorageId)){reason="InvalidEquipmentStorageReference";return false;}
            if(item.LocationKind==EquipmentLocationKindV3.FacilityOutput&&!regionById[item.RegionId!].ProductionFacilities.Any(x=>x.FacilityId==item.FacilityId)){reason="InvalidEquipmentFacilityReference";return false;}
        }
        foreach(RegionCheckpointV3 region in dto.Regions)
        {
            if(HasDuplicate(region.ResourceNodes,x=>x.Id)||HasDuplicate(region.GroundStacks,x=>x.Id)||HasDuplicate(region.Structures,x=>x.Id)||HasDuplicate(region.Blueprints,x=>x.Id)||HasDuplicate(region.Stockpiles,x=>x.Id)||HasDuplicate(region.FarmPlots,x=>x.Id)||HasDuplicate(region.ProductionFacilities,x=>x.FacilityId)){reason="DuplicateRegionObjectId";return false;}
            foreach(ProductionFacilityCheckpointV3 facility in region.ProductionFacilities)
                if(!region.Structures.Any(x=>x.Id==facility.FacilityId)||facility.Queue.Any(x=>!StarterProcessingContentV3.TryGet(x.RecipeId,out _))){reason="InvalidProductionReference";return false;}
        }
        long maxEquipmentSequence=dto.Equipment.Select(x=>TryParseTrailingHex(x.InstanceId)).DefaultIfEmpty(0).Max();
        long maxTravelSequence=dto.TravelingGroups.Select(x=>TryParseTrailingHex(x.Id)).DefaultIfEmpty(0).Max();
        if(dto.EquipmentNextSequence<=maxEquipmentSequence||dto.TravelingGroupNextSequence<=maxTravelSequence){reason="AllocatorRegression";return false;}
        return true;
    }

    public static bool TryRestoreFresh(PersistentWorldCheckpointV3 dto,out RestoredPersistentWorldRuntimeV3? restored,out string reason)
    {
        restored=null;if(!Validate(dto,out reason))return false;
        try
        {
            CompanySessionV3 companies=new();MercenarySessionV3 mercenaries=new(companies.CompanyRegistry);
            foreach(CompanyCheckpointV3 c in dto.Companies)
                if(!companies.CompanyRegistry.TryRegisterCompany(new CompanyStateV3(c.CompanyId,c.OwnerPlayerId,c.DisplayName,c.CreatedUtc),out reason))return false;
            foreach(CompanyCheckpointV3 c in dto.Companies)foreach(MercenaryCheckpointV3 m in c.Mercenaries)
            {
                if(m.Attributes.Length!=5||m.WorkSkills.Length!=7||!MercenaryAttributeSetV3.TryCreate(m.Attributes[0],m.Attributes[1],m.Attributes[2],m.Attributes[3],m.Attributes[4],out MercenaryAttributeSetV3? attributes,out reason)||
                   !MercenaryWorkSkillSetV3.TryCreate(m.WorkSkills[0],m.WorkSkills[1],m.WorkSkills[2],m.WorkSkills[3],m.WorkSkills[4],m.WorkSkills[5],m.WorkSkills[6],out MercenaryWorkSkillSetV3? skills,out reason)||
                   !MercenaryProfileV3.TryCreate(m.MercenaryId,m.DisplayName,m.AppearanceKey,attributes,skills,m.CreatedUtc,out MercenaryProfileV3? profile,out reason,m.IsInitialSquadMember,m.InitialSquadSlotIndex)||
                   !MercenaryStateV3.TryCreate(m.MercenaryId,m.CompanyId,m.Cell.ToGlobal(),m.ActivityState,m.CreatedUtc,out MercenaryStateV3? state,out reason)||
                   !mercenaries.Registry.TryRegisterMercenary(profile,state,out reason))return false;
            }
            EquipmentRuntimeV3 equipment=new(dto.RuntimeSessionRevision,StarterEquipmentContentV3.CreateRegistry());
            EquipmentLoadoutRuntimeV3 loadouts=new(dto.RuntimeSessionRevision,mercenaries.Registry,equipment);
            SimulationClockSessionV3 clock=new(dto.RuntimeSessionRevision);clock.RestorePersistentState(dto.ClockTick,dto.WorldTime,dto.TimeScale,dto.ClockRevision);
            PersistentWorldStateV3 world=new(dto.WorldId,dto.WorldSeed,dto.GeneratorVersion);
            Dictionary<string,PlayerCompanyStateV3> companyStates=new(StringComparer.Ordinal);
            foreach(CompanyCheckpointV3 c in dto.Companies)
            {
                companies.CompanyRegistry.TryGetCompany(c.CompanyId,out CompanyStateV3? core);
                PlayerCompanyStateV3 company=new(core!,c.PlayerAccountId,mercenaries,equipment,loadouts);if(!world.TryRegisterCompany(company,out reason))return false;companyStates.Add(c.CompanyId,company);
            }
            foreach(RegionCheckpointV3 r in dto.Regions)
            {
                ResourceSessionV3 resources=new();ConstructionSessionV3 construction=new();StockpileSessionV3 stockpiles=new();FarmSessionV3 farming=new(dto.RuntimeSessionRevision);
                foreach(ResourceNodeCheckpointV3 n in r.ResourceNodes){if(!ResourceNodeStateV3.TryCreate(n.Id,n.Type,n.Cell.ToGlobal(),n.Remaining,n.Maximum,n.Yield,RestoreBounds,n.CreatedUtc,out ResourceNodeStateV3? node,out reason)||!resources.Nodes.TryRegister(node,out reason))return false;}
                foreach(GroundStackCheckpointV3 s in r.GroundStacks)if(!resources.GroundStacks.TryRestoreStack(s.Id,s.Type,s.Amount,s.Cell.ToGlobal(),s.CreatedUtc,out reason))return false;
                foreach(StructureCheckpointV3 s in r.Structures){StructureStateV3 state=new(s.Id,s.CompanyId,s.DefinitionId,s.Anchor.ToGlobal(),s.Orientation,s.Cells.Select(x=>x.ToGlobal()),s.Materials.Select(x=>new StructureMaterialRequirementV3(x.Type,x.Amount)),s.MovementKind==StructureMovementKindV3.Blocking,s.CompletedUtc,s.MovementKind,s.RoomBoundaryKind,s.MaterialResourceType,s.MaxHealth);state.RestoreHealth(s.CurrentHealth);if(!construction.Structures.TryRegister(state,construction.Blueprints,RestoreBounds,out reason))return false;}
                foreach(BlueprintCheckpointV3 b in r.Blueprints){ConstructionMaterialBufferV3 buffer=new(b.Required.Select(x=>new StructureMaterialRequirementV3(x.Type,x.Amount)));foreach(MaterialCheckpointV3 item in b.Delivered)if(!buffer.TryDeliver(item.Type,item.Amount,out reason))return false;ConstructionBlueprintStateV3 state=new(b.Id,b.CompanyId,b.DefinitionId,b.Anchor.ToGlobal(),b.Orientation,b.Cells.Select(x=>x.ToGlobal()),buffer,b.CreatedUtc,b.MaterialResourceType,b.RequiredWorkSeconds,b.ExpectedMaxHealth);state.RestoreProgress(b.Status,b.Progress,b.Revision);if(!construction.Blueprints.TryRegister(state,construction.Structures,RestoreBounds,out reason))return false;}
                foreach(StockpileCheckpointV3 s in r.Stockpiles)if(!stockpiles.Zones.TryRestoreZone(s.Id,s.CompanyId,s.Cells.Select(x=>x.ToGlobal()).ToList(),s.AllowedResources,s.CreatedUtc,s.Enabled,s.AllowsEquipment,out reason))return false;
                foreach(FarmPlotCheckpointV3 f in r.FarmPlots)if(!farming.Plots.TryRestorePlot(f.Id,f.CompanyId,f.CropDefinitionId,f.Cells.Select(x=>(x.Cell.ToGlobal(),x.Stage,x.Elapsed,x.Normalized,x.Revision)).ToList(),f.Revision,out reason))return false;
                ProductionSessionV3 production=new(dto.RuntimeSessionRevision,construction,resources,stockpiles);
                RegionPersistentStateV3 region=new(r.RegionId,r.RegionType,r.OwnerCompanyId,r.TerrainSeed,resources,construction,stockpiles,production,farming,equipment);
                if(!world.TryRegisterRegion(region,out reason))return false;equipment.AttachRegionLocationStore(region.EquipmentLocations);
                foreach(ProductionFacilityCheckpointV3 p in r.ProductionFacilities)
                {
                    ProductionCheckpointFacilityV3 checkpoint=new(p.FacilityId,p.EquipmentCompletionSequence,p.LastCommittedBatchToken,p.Revision,p.LastChangedReason,p.Queue.Select(x=>new ProductionOrderSnapshotV3(x.OrderId,x.FacilityId,x.RecipeId,x.RequestedBatches,x.CompletedBatches,Math.Max(0,x.RequestedBatches-x.CompletedBatches),x.State,x.WorkProgressSeconds,null,null,x.Revision,x.LastFailureReason)).ToList(),p.DeliveredMaterials.Select(x=>new StructureMaterialRequirementV3(x.Type,x.Amount)).ToList(),new ReadOnlyDictionary<ResourceTypeV3,int>(p.OutputBuffer));
                    if(!production.TryRestoreCheckpointFacility(checkpoint,out reason))return false;
                }
                production.RestoreNextOrderSequence(r.ProductionNextOrderSequence);region.RestoreRevision(r.RegionRevision,r.LastCommittedWorldTime);
            }
            foreach(EquipmentCheckpointV3 item in dto.Equipment)if(!equipment.TryRestoreInstance(item.InstanceId,item.DefinitionId,item.Quality,item.QualityScore,item.CrafterMercenaryId,item.CrafterProductionSkill,item.OwnerCompanyId,item.CreatedSessionRevision,item.LocationKind,item.RegionId,item.GroundCell?.ToVector(),item.StorageId,item.StorageCell?.ToVector(),item.FacilityId,item.EquippedMercenaryId,item.EquippedSlot,out reason))return false;
            equipment.RestoreNextInstanceSequence(dto.EquipmentNextSequence);
            foreach(CompanyCheckpointV3 c in dto.Companies)
            {
                PlayerCompanyStateV3 company=companyStates[c.CompanyId];
                foreach(MercenaryCheckpointV3 m in c.Mercenaries)
                    if(m.PresenceKind==MercenaryWorldPresenceV3.AtRegion?!company.SetMercenaryRegion(m.MercenaryId,m.CurrentRegionId!):!company.SetMercenaryTraveling(m.MercenaryId,m.TravelingGroupId!)){reason="MercenaryPresenceRestoreFailed";return false;}
                foreach(LoadoutCheckpointV3 l in c.Loadouts)if(!loadouts.TryRestoreLoadout(l.MercenaryId,l.MainHand,l.Armor,l.Tool,l.Revision,out reason))return false;
            }
            foreach(WorldRouteCheckpointV3 route in dto.Routes)if(!world.WorldGraph.AddRoute(new(route.RouteId,route.OriginRegionId,route.DestinationRegionId,route.Bidirectional,route.TravelDuration,route.Enabled),out reason))return false;
            foreach(TravelingGroupCheckpointV3 group in dto.TravelingGroups)if(!world.TravelingGroups.TryRestore(group.Id,group.OwnerCompanyId,group.MercenaryIds,group.EquipmentIds,group.OriginRegionId,group.DestinationRegionId,group.RouteId,group.DepartureWorldTime,group.ArrivalWorldTime,group.State,group.Revision,out reason))return false;
            world.TravelingGroups.RestoreNextSequence(dto.TravelingGroupNextSequence);world.WorldClock.Restore(dto.ClockTick,dto.WorldTime,dto.TimeScale,dto.ClockRevision);world.RestoreMetadata(dto.WorldRevision,dto.ActiveRegionId);
            Dictionary<string,string> bindings=dto.AccountBindings.ToDictionary(x=>x.AccountId,x=>x.CompanyId,StringComparer.Ordinal);
            Dictionary<string,long> generations=dto.RegionSessionGenerations.ToDictionary(x=>x.RegionId,x=>x.Generation,StringComparer.Ordinal);
            restored=new(){World=world,Companies=companies,Mercenaries=mercenaries,Equipment=equipment,Loadouts=loadouts,Clock=clock,AccountBindings=new ReadOnlyDictionary<string,string>(bindings),RegionSessionGenerations=new ReadOnlyDictionary<string,long>(generations)};
            return true;
        }
        catch(Exception exception){reason="CheckpointRestoreException:"+exception.GetType().Name;return false;}
    }

    private static bool HasDuplicate<T>(IEnumerable<T> values,Func<T,string> keySelector){HashSet<string> ids=new(StringComparer.Ordinal);foreach(T value in values)if(!ids.Add(keySelector(value)))return true;return false;}
    private static long TryParseTrailingHex(string value)
    {
        int separator=value.LastIndexOf('_');return separator>=0&&long.TryParse(value.AsSpan(separator+1),System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out long parsed)?parsed:0;
    }
}

public sealed class CheckpointStoreDiagnosticsV3
{
    public int SaveAttemptCount{get;internal set;}public int SaveSuccessCount{get;internal set;}public int LoadAttemptCount{get;internal set;}public int LoadSuccessCount{get;internal set;}
    public int ValidationFailureCount{get;internal set;}public int ChecksumFailureCount{get;internal set;}public int LastCheckpointByteSize{get;internal set;}public int PartialWorldApplyCount{get;internal set;}
}

public sealed class ServerCheckpointStoreV3
{
    public const int SchemaVersion=1;
    public const int MaximumCheckpointBytes=8*1024*1024;
    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=false};
    public CheckpointStoreDiagnosticsV3 Diagnostics{get;}=new();

    public bool SaveAtomic(string path,PersistentWorldCheckpointV3 checkpoint,out string reason)
    {
        Diagnostics.SaveAttemptCount++;reason=string.Empty;
        if(string.IsNullOrWhiteSpace(path)||!PersistentWorldCheckpointMapperV3.Validate(checkpoint,out reason)){Diagnostics.ValidationFailureCount++;return false;}
        string payload=JsonSerializer.Serialize(checkpoint,JsonOptions);byte[] payloadBytes=Encoding.UTF8.GetBytes(payload);
        if(payloadBytes.Length>MaximumCheckpointBytes){reason="CheckpointTooLarge";return false;}
        WorldCheckpointEnvelopeV3 envelope=new(){CheckpointSchemaVersion=SchemaVersion,GameDataVersion=checkpoint.GeneratorVersion,CreatedUtc=DateTime.UtcNow,PayloadByteLength=payloadBytes.Length,PayloadSha256=Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant(),Payload=payload};
        byte[] bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope,JsonOptions));if(bytes.Length>MaximumCheckpointBytes){reason="CheckpointTooLarge";return false;}
        string fullPath=Path.GetFullPath(path),tempPath=fullPath+".tmp",backupPath=fullPath+".bak";Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        try
        {
            using(FileStream stream=new(tempPath,FileMode.Create,System.IO.FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}
            if(!TryReadEnvelope(tempPath,out _,out reason))return false;
            if(File.Exists(fullPath))File.Copy(fullPath,backupPath,true);
            File.Move(tempPath,fullPath,true);
            if(!TryReadEnvelope(fullPath,out _,out reason)){if(File.Exists(backupPath))File.Copy(backupPath,fullPath,true);return false;}
            Diagnostics.SaveSuccessCount++;Diagnostics.LastCheckpointByteSize=bytes.Length;return true;
        }
        catch(Exception exception){reason="CheckpointWriteFailed:"+exception.GetType().Name;return false;}
        finally{try{if(File.Exists(tempPath))File.Delete(tempPath);}catch{}}
    }

    public bool LoadAndValidate(string path,out PersistentWorldCheckpointV3? checkpoint,out string reason)
    {
        Diagnostics.LoadAttemptCount++;checkpoint=null;
        if(!TryReadEnvelope(path,out WorldCheckpointEnvelopeV3? envelope,out reason)||envelope==null)return false;
        try{checkpoint=JsonSerializer.Deserialize<PersistentWorldCheckpointV3>(envelope.Payload,JsonOptions);}
        catch(JsonException){reason="CheckpointPayloadInvalid";Diagnostics.ValidationFailureCount++;return false;}
        if(checkpoint==null||!PersistentWorldCheckpointMapperV3.Validate(checkpoint,out reason)){checkpoint=null;Diagnostics.ValidationFailureCount++;return false;}
        Diagnostics.LoadSuccessCount++;return true;
    }

    private bool TryReadEnvelope(string path,out WorldCheckpointEnvelopeV3? envelope,out string reason)
    {
        envelope=null;reason=string.Empty;
        try
        {
            FileInfo info=new(path);if(!info.Exists){reason="CheckpointNotFound";return false;}if(info.Length<=0||info.Length>MaximumCheckpointBytes){reason="CheckpointSizeInvalid";return false;}
            envelope=JsonSerializer.Deserialize<WorldCheckpointEnvelopeV3>(File.ReadAllBytes(path),JsonOptions);
            if(envelope==null||envelope.CheckpointSchemaVersion!=SchemaVersion){reason="UnsupportedCheckpointSchema";return false;}
            byte[] payload=Encoding.UTF8.GetBytes(envelope.Payload);
            if(payload.Length!=envelope.PayloadByteLength){reason="CheckpointPayloadLengthMismatch";Diagnostics.ChecksumFailureCount++;return false;}
            string hash=Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
            if(!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash),Encoding.ASCII.GetBytes(envelope.PayloadSha256))){reason="CheckpointChecksumMismatch";Diagnostics.ChecksumFailureCount++;return false;}
            return true;
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException or JsonException){reason="CheckpointReadFailed:"+exception.GetType().Name;return false;}
    }
}
