using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameplayV3.Construction;
using GameplayV3.Equipment;
using GameplayV3.Farming;
using GameplayV3.Session;

namespace GameplayV3.Network;

public sealed class RegionSnapshotBuilderV3
{
    public bool TryBuildMercenaryDelta(
        PersistentWorldStateV3 world,RegionPersistentStateV3 region,PlayerCompanyStateV3 recipient,string mercenaryId,
        out MercenarySnapshotDtoV3? mercenary,out EquipmentSnapshotDtoV3[] equipment)
    {
        mercenary=null;equipment=Array.Empty<EquipmentSnapshotDtoV3>();
        PlayerCompanyStateV3? owner=world.PlayerCompanies.Values.FirstOrDefault(x=>x.TryGetMercenaryPresence(mercenaryId,out MercenaryPresenceStateV3? presence)&&presence?.AtRegion==true&&presence.CurrentRegionId==region.RegionId);
        if(owner==null||!owner.MercenaryProfiles.TryGetMercenary(mercenaryId,out var profile,out var state)||profile==null||state==null)return false;
        bool owned=owner.CompanyId==recipient.CompanyId;
        owner.EquipmentLoadouts.TryGetLoadout(mercenaryId,out MercenaryEquipmentLoadoutSnapshotV3? loadout);
        GameplaySessionV3.TryGetNeedsSession(out var needs);
        mercenary=new(mercenaryId,state.CompanyId,owned,profile.DisplayName,Cell(state.CurrentCell.Value),true,state.ActivityState,
            owned?needs?.Hunger.GetHunger(mercenaryId)??0:0,owned?needs?.Fatigue.GetValue(mercenaryId)??0:0,
            owned?profile.Attributes.Strength:0,owned?profile.Attributes.Agility:0,owned?profile.Attributes.Endurance:0,owned?profile.Attributes.Intelligence:0,owned?profile.Attributes.Mental:0,
            owned?profile.WorkSkills.Hauling:0,owned?profile.WorkSkills.Construction:0,owned?profile.WorkSkills.Gathering:0,owned?profile.WorkSkills.Farming:0,owned?profile.WorkSkills.Production:0,owned?profile.WorkSkills.Medicine:0,owned?profile.WorkSkills.Guarding:0,
            loadout?.MainHandEquipmentInstanceId,loadout?.ArmorEquipmentInstanceId,loadout?.ToolEquipmentInstanceId);
        List<EquipmentSnapshotDtoV3> items=new();
        foreach(string id in new[]{loadout?.MainHandEquipmentInstanceId,loadout?.ArmorEquipmentInstanceId,loadout?.ToolEquipmentInstanceId}.Where(x=>x!=null).Cast<string>())
            if(owner.Equipment.TryGetInstance(id,out EquipmentInstanceV3? item)&&item!=null)
                items.Add(new(id,item.EquipmentDefinitionId,item.Quality,item.QualityScore,owned?item.CrafterMercenaryId:string.Empty,owned?item.CrafterProductionSkillSnapshot:0,item.OwnerCompanyId,item.LocationKind,
                    item.GroundCell is { } ground?Cell(ground):null,item.StorageId,item.StorageCell is { } storage?Cell(storage):null,item.FacilityId,item.EquippedMercenaryId,item.EquippedSlot));
        equipment=items.OrderBy(x=>x.EquipmentInstanceId,StringComparer.Ordinal).ToArray();
        return true;
    }

    public bool TryBuild(
        PersistentWorldStateV3 world,
        ActiveRegionSessionV3 active,
        PlayerCompanyStateV3 company,
        long requestId,
        int maximumPayloadBytes,
        out string snapshotId,
        out string payload,
        out int payloadBytes,
        out NetworkRejectReasonV3 failure)
    {
        snapshotId=string.Empty;payload=string.Empty;payloadBytes=0;failure=NetworkRejectReasonV3.SnapshotBuildFailed;
        if(active.IsDisposed||active.RegionId!=active.PersistentState.RegionId)return false;
        RegionPersistentStateV3 region=active.PersistentState;
        if(region.OwnerCompanyId!=null&&region.OwnerCompanyId!=company.CompanyId){failure=NetworkRejectReasonV3.RegionAccessDenied;return false;}

        List<MercenarySnapshotDtoV3> mercenaries=new();
        HashSet<string> equipmentIds=new(region.EquipmentLocations.GetAllLocatedInstanceIds(),StringComparer.Ordinal);
        HashSet<string> visibleMercenaryIds=new(StringComparer.Ordinal);
        IEnumerable<PlayerCompanyStateV3> visibleCompanies=region.RegionType==RegionTypeV3.SharedNeutral
            ?world.PlayerCompanies.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Value)
            :new[]{company};
        foreach(PlayerCompanyStateV3 visibleCompany in visibleCompanies)
        foreach(string id in visibleCompany.GetMercenaryIdsAtRegion(region.RegionId))
        {
            if(!visibleCompany.MercenaryProfiles.TryGetMercenary(id,out var profile,out var state)||profile==null||state==null)continue;
            bool owned=visibleCompany.CompanyId==company.CompanyId;
            visibleCompany.TryGetMercenaryPresence(id,out MercenaryPresenceStateV3? presence);
            visibleCompany.EquipmentLoadouts.TryGetLoadout(id,out MercenaryEquipmentLoadoutSnapshotV3? loadout);
            AddEquipment(loadout?.MainHandEquipmentInstanceId);AddEquipment(loadout?.ArmorEquipmentInstanceId);AddEquipment(loadout?.ToolEquipmentInstanceId);
            visibleMercenaryIds.Add(id);
            GameplaySessionV3.TryGetNeedsSession(out var needs);
            mercenaries.Add(new(id,state.CompanyId,owned,profile.DisplayName,Cell(state.CurrentCell.Value),presence?.AtRegion==true,state.ActivityState,
                owned?needs?.Hunger.GetHunger(id)??0:0,owned?needs?.Fatigue.GetValue(id)??0:0,
                owned?profile.Attributes.Strength:0,owned?profile.Attributes.Agility:0,owned?profile.Attributes.Endurance:0,owned?profile.Attributes.Intelligence:0,owned?profile.Attributes.Mental:0,
                owned?profile.WorkSkills.Hauling:0,owned?profile.WorkSkills.Construction:0,owned?profile.WorkSkills.Gathering:0,owned?profile.WorkSkills.Farming:0,owned?profile.WorkSkills.Production:0,owned?profile.WorkSkills.Medicine:0,owned?profile.WorkSkills.Guarding:0,
                loadout?.MainHandEquipmentInstanceId,loadout?.ArmorEquipmentInstanceId,loadout?.ToolEquipmentInstanceId));
        }

        List<ResourceNodeSnapshotDtoV3> nodes=new();
        foreach(string id in region.ResourceNodes.GetAllNodeIds())
            if(region.ResourceNodes.TryGet(id,out var node)&&node!=null)
            {
                WorldV2.NaturalResourceDefinitionCatalogV3.TryGet(node.NodeType,out var definition);
                nodes.Add(new(id,definition?.DefinitionId??node.NodeType.ToString(),node.NodeType,node.ProducedResourceType,Cell(node.Cell.Value),node.RemainingAmount,node.MaxAmount));
            }
        List<GroundResourceStackSnapshotDtoV3> stacks=new();
        foreach(string id in region.GroundResourceStacks.GetAllStackIds())
            if(region.GroundResourceStacks.TryGet(id,out var stack)&&stack!=null)stacks.Add(new(id,stack.ResourceType,Cell(stack.Cell.Value),stack.Amount));
        List<StructureSnapshotDtoV3> structures=new();
        foreach(string id in region.Structures.GetAllStructureIds())
            if(region.Structures.TryGet(id,out var structure)&&structure!=null)structures.Add(new(id,structure.DefinitionId,structure.CompanyId,Cell(structure.AnchorCell.Value),structure.Orientation,Cells(structure.OccupiedCells.Select(x=>x.Value)),structure.CurrentHealth,structure.MaxHealth));
        List<BlueprintSnapshotDtoV3> blueprints=new();
        foreach(string id in region.Blueprints.GetAllBlueprintIds())
            if(region.Blueprints.TryGet(id,out var blueprint)&&blueprint!=null)
            {
                Dictionary<GameplayV3.Resources.ResourceTypeV3,int> delivered=blueprint.MaterialBuffer.GetDeliveredMaterialsSnapshot().ToDictionary(x=>x.ResourceType,x=>x.RequiredAmount);
                SnapshotMaterialV3[] materials=blueprint.MaterialBuffer.GetRequiredMaterialsSnapshot().Select(x=>new SnapshotMaterialV3(x.ResourceType,x.RequiredAmount,delivered.GetValueOrDefault(x.ResourceType))).ToArray();
                blueprints.Add(new(id,blueprint.DefinitionId,blueprint.CompanyId,Cell(blueprint.AnchorCell.Value),blueprint.Orientation,Cells(blueprint.OccupiedCells.Select(x=>x.Value)),materials,blueprint.Status,blueprint.ConstructionProgressSeconds,blueprint.RequiredWorkSeconds));
            }
        List<StockpileSnapshotDtoV3> stockpiles=new();
        foreach(string id in region.StockpileZones.GetAllZoneIds())
            if(region.StockpileZones.TryGetZone(id,out var zone)&&zone!=null)
            {
                if(region.RegionType==RegionTypeV3.SharedNeutral&&zone.CompanyId!=company.CompanyId)continue;
                HashSet<Godot.Vector2I> cells=new(zone.Cells.Select(x=>x.Value));
                string[] storedStacks=stacks.Where(x=>cells.Contains(new Godot.Vector2I(x.Cell.X,x.Cell.Y))).Select(x=>x.ResourceStackId).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                stockpiles.Add(new(id,zone.CompanyId,Cells(cells),zone.IsEnabled,zone.AllowsEquipment,zone.AllowedResourceTypes.ToArray(),storedStacks));
            }
        List<ProductionFacilitySnapshotDtoV3> production=new();
        foreach(var facility in region.Production.GetFacilities(company.CompanyId))
        {
            ProductionOrderSnapshotDtoV3[] orders=facility.Queue.Select(x=>new ProductionOrderSnapshotDtoV3(x.OrderId,x.RecipeId,x.RequestedBatches,x.CompletedBatches,x.State,x.WorkProgressSeconds)).ToArray();
            region.Structures.TryGet(facility.StructureId,out StructureStateV3? facilityStructure);
            production.Add(new(facility.FacilityId,facilityStructure?.DefinitionId??string.Empty,facility.CompanyId,(int)facility.FacilityKind,Cell(facility.AnchorCell.Value),orders,region.EquipmentLocations.GetFacilityOutput(facility.FacilityId).ToArray()));
        }
        List<FarmPlotSnapshotDtoV3> farms=new();
        foreach(FarmPlotV3 plot in region.FarmPlots.GetPlotsByCompany(company.CompanyId).OrderBy(x=>x.FarmPlotId,StringComparer.Ordinal))
        {
            HashSet<Godot.Vector2I> cells=new(plot.Cells);
            FarmCropSnapshotDtoV3[] crops=region.FarmPlots.GetAllCrops().Where(x=>x.FarmPlotId==plot.FarmPlotId).Select(x=>new FarmCropSnapshotDtoV3(Cell(x.Cell),x.Stage,x.GrowthNormalized)).ToArray();
            farms.Add(new(plot.FarmPlotId,plot.CompanyId,plot.CropDefinitionId,Cells(cells),crops));
        }
        List<EquipmentSnapshotDtoV3> equipment=new();
        foreach(string id in equipmentIds.OrderBy(x=>x,StringComparer.Ordinal))
            if(company.Equipment.TryGetInstance(id,out EquipmentInstanceV3? item)&&item!=null&&(item.RegionId==region.RegionId||item.LocationKind==EquipmentLocationKindV3.Equipped&&item.EquippedMercenaryId is { } equipped&&visibleMercenaryIds.Contains(equipped)))
            {
                bool owned=item.OwnerCompanyId==company.CompanyId;
                if(!owned&&item.LocationKind!=EquipmentLocationKindV3.Ground&&item.LocationKind!=EquipmentLocationKindV3.Equipped)continue;
                equipment.Add(new(id,item.EquipmentDefinitionId,item.Quality,item.QualityScore,owned?item.CrafterMercenaryId:string.Empty,owned?item.CrafterProductionSkillSnapshot:0,item.OwnerCompanyId,item.LocationKind,
                    item.GroundCell is { } ground?Cell(ground):null,item.StorageId,item.StorageCell is { } storage?Cell(storage):null,item.FacilityId,item.EquippedMercenaryId,item.EquippedSlot));
            }

        InitialRegionSnapshotPayloadV3 dto=new(){
            OwnerCompanyId=region.OwnerCompanyId??string.Empty,TerrainSeed=region.TerrainSeed,
            Mercenaries=mercenaries.ToArray(),ResourceNodes=nodes.ToArray(),GroundResourceStacks=stacks.ToArray(),
            Structures=structures.ToArray(),Blueprints=blueprints.ToArray(),Stockpiles=stockpiles.ToArray(),
            ProductionFacilities=production.OrderBy(x=>x.FacilityId,StringComparer.Ordinal).ToArray(),
            FarmPlots=farms.ToArray(),Equipment=equipment.ToArray()};
        byte[] bytes=RegionSnapshotProtocolV3.Serialize(dto);payloadBytes=bytes.Length;
        if(payloadBytes>maximumPayloadBytes){failure=NetworkRejectReasonV3.SnapshotTooLarge;return false;}
        snapshotId=$"snap_{active.SessionRevision:x}_{region.RegionRevision:x}_{requestId:x}";
        payload=Encoding.UTF8.GetString(bytes);failure=NetworkRejectReasonV3.None;return true;

        void AddEquipment(string? id){if(!string.IsNullOrEmpty(id))equipmentIds.Add(id);}
    }

    private static SnapshotCellV3 Cell(Godot.Vector2I cell)=>new(cell.X,cell.Y);
    private static SnapshotCellV3[] Cells(IEnumerable<Godot.Vector2I> cells)=>cells.OrderBy(x=>x.Y).ThenBy(x=>x.X).Select(Cell).ToArray();
}
